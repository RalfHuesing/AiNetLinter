#nullable enable

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Mcp.Scope;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.TypeHierarchy;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.TestKit;
using static AiNetLinter.TestKit.McpTestResultText;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.SymbolGraph;

[Trait("Category", "Component")]
public sealed class GetTypeHierarchyToolTests
{
    private readonly McpInMemoryTestContext _fixture;

    public GetTypeHierarchyToolTests() { _fixture = new McpInMemoryTestContext(); }

    [Fact]
    public async Task ExecuteAsync_ScopeFiltersGeneratedAndTestSubtypesBeforeLimit()
    {
        using var solution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\ScopeHierarchy.slnx",
            new ProjectSpec("App", [
                ("Contracts.cs", "namespace App; public interface IService { void Run(); }"),
                ("Production.cs", "namespace App; public class ProductionService : IService { public void Run() {} }")]),
            new ProjectSpec("App.Tests", [
                ("ServiceTests.cs", "namespace App.Tests; public class TestService : App.IService { public void Run() {} }")],
                ["App"]),
            new ProjectSpec("Generated", [
                ("Generated.g.cs", "namespace App; public class GeneratedService : IService { public void Run() {} }")],
                ["App"]));
        using var fixture = new McpInMemoryTestContext(solution);

        var all = await GetTypeHierarchyTool.ExecuteAsync(
            new GetTypeHierarchyRequest(
                fixture.CreateServer(), "App.IService", 1, McpScopeType.All, false, CancellationToken.None));
        Assert.Equal(2, all.StructuredContent!.Value.GetProperty("totalSubtypeCount").GetInt32());
        Assert.Contains("ProductionService", all.StructuredContent!.Value.GetProperty("subtypes")[0].GetProperty("name").GetString(), StringComparison.Ordinal);
        Assert.Equal("all", all.StructuredContent!.Value.GetProperty("scope").GetProperty("requestedType").GetString());

        var tests = await GetTypeHierarchyTool.ExecuteAsync(
            new GetTypeHierarchyRequest(
                fixture.CreateServer(), "App.IService", 50, McpScopeType.Tests, false, CancellationToken.None));
        Assert.Equal(1, tests.StructuredContent!.Value.GetProperty("totalSubtypeCount").GetInt32());
        Assert.Equal("tests", tests.StructuredContent!.Value.GetProperty("subtypes")[0].GetProperty("scopeType").GetString());

        var generated = await GetTypeHierarchyTool.ExecuteAsync(
            new GetTypeHierarchyRequest(
                fixture.CreateServer(), "App.IService", 50, McpScopeType.All, true, CancellationToken.None));
        Assert.Equal(3, generated.StructuredContent!.Value.GetProperty("totalSubtypeCount").GetInt32());
        Assert.Contains(
            generated.StructuredContent!.Value.GetProperty("subtypes").EnumerateArray(),
            item => item.GetProperty("sourceKind").GetString() == "generated");
    }

    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));

        var result = await GetTypeHierarchyTool.ExecuteAsync(state, "irrelevant", GetTypeHierarchyTool.DefaultMaxResults, CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownTypeIdentifier_ReturnsRecoverableSymbolNotFound()
    {
        var state = _fixture.CreateServer();

        var result = await GetTypeHierarchyTool.ExecuteAsync(state, "DoesNotExistXyz", GetTypeHierarchyTool.DefaultMaxResults, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SYMBOL_NOT_FOUND", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_IdentifierResolvesToMethodNotType_ReturnsRecoverableInvalidArgument()
    {
        var state = _fixture.CreateServer();

        var result = await GetTypeHierarchyTool.ExecuteAsync(state, "BaseGreeting.Greet", GetTypeHierarchyTool.DefaultMaxResults, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_ClassWithBaseAndDerived_ReturnsInterfaceAndDerivedClass()
    {
        var state = _fixture.CreateServer();

        var result = await GetTypeHierarchyTool.ExecuteAsync(state, "BaseGreeting", GetTypeHierarchyTool.DefaultMaxResults, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("IGreeting", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("SpecialGreeting", textContent.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Diese Daten sind vollstaendig", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ClassWithBaseAndDerived_ReturnsStructuredSuccessPayload()
    {
        var state = _fixture.CreateServer();

        var result = await GetTypeHierarchyTool.ExecuteAsync(
            state, "BaseGreeting", GetTypeHierarchyTool.DefaultMaxResults, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var payload = JsonSerializer.Deserialize<TypeHierarchyPayload>(
            result.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default);
        Assert.NotNull(payload);
        Assert.Equal("SymbolGraphMini.BaseGreeting", payload!.TypeName);
        Assert.Contains(payload.Interfaces, value => value.Name.Contains("IGreeting", StringComparison.Ordinal));
        var subtype = Assert.Single(payload.Subtypes);
        Assert.Contains("SpecialGreeting", subtype.Name, StringComparison.Ordinal);
        Assert.Equal(1, payload.ShownSubtypeCount);
        Assert.Equal(1, payload.TotalSubtypeCount);
        Assert.False(payload.SubtypesTruncated);
        Assert.Empty(payload.SubtypesTruncatedBy);
    }

    [Fact]
    public async Task ExecuteAsync_HandoffIsStructuredOnly_AndHierarchyEntriesAreSelectable()
    {
        var result = await GetTypeHierarchyTool.ExecuteAsync(
            _fixture.CreateServer(), "BaseGreeting", GetTypeHierarchyTool.DefaultMaxResults, CancellationToken.None);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.DoesNotContain("handoff=", text, StringComparison.Ordinal);
        Assert.DoesNotContain("id: `s:", text, StringComparison.Ordinal);

        var payload = result.StructuredContent!.Value;
        var interfaceEntry = Assert.Single(payload.GetProperty("interfaces").EnumerateArray());
        Assert.StartsWith("s:", interfaceEntry.GetProperty("id").GetString(), StringComparison.Ordinal);
        Assert.Equal("type", interfaceEntry.GetProperty("handoffKind").GetString());
        Assert.False(interfaceEntry.TryGetProperty("targetPath", out _));
        Assert.False(interfaceEntry.TryGetProperty("snapshot", out _));
        Assert.False(interfaceEntry.TryGetProperty("allowedFollowUpTools", out _));

        var subtypeEntry = Assert.Single(payload.GetProperty("subtypes").EnumerateArray());
        Assert.StartsWith("s:", subtypeEntry.GetProperty("id").GetString(), StringComparison.Ordinal);
        Assert.Equal("type", subtypeEntry.GetProperty("handoffKind").GetString());
    }

    [Fact]
    public async Task ExecuteAsync_StructuredHierarchyIdCanBeUsedForImplementationsFollowUp()
    {
        var state = _fixture.CreateServer();
        var hierarchy = await GetTypeHierarchyTool.ExecuteAsync(
            state, "BaseGreeting", GetTypeHierarchyTool.DefaultMaxResults, CancellationToken.None);
        var id = hierarchy.StructuredContent!.Value.GetProperty("interfaces")[0]
            .GetProperty("id").GetString();

        var implementations = await FindImplementationsTool.ExecuteAsync(
            state, id, FindImplementationsTool.DefaultMaxResults, CancellationToken.None);

        Assert.NotEqual(true, implementations.IsError);
        Assert.Contains(
            "BaseGreeting",
            Assert.IsType<TextContentBlock>(Assert.Single(implementations.Content)).Text,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_InterfaceWithMultipleImplementers_MaxResultsBelowCount_TruncatesAndSuppressesSufficiencyHint()
    {
        // Bei vielen Implementierern muss get_type_hierarchy seine Ausgabe begrenzen, damit
        // der Client-Token-Guard nicht ueberlaufen kann. IGreeting hat in dieser Fixture zwei transitive
        // Implementierer (BaseGreeting direkt, SpecialGreeting via Vererbung von BaseGreeting).
        var state = _fixture.CreateServer();

        var result = await GetTypeHierarchyTool.ExecuteAsync(state, "IGreeting", 1, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Typen gesamt, 1 gezeigt", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("maxResults erhoehen", textContent.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Diese Daten sind vollstaendig", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_StableTypeIdentifier_ReturnsInterfaceAndDerivedClass()
    {
        var state = _fixture.CreateServer();
        var (resolved, _) = await FindReferencesTool.ResolveSymbolAsync(
            _fixture.Solution, "BaseGreeting", CancellationToken.None);
        var stableId = Microsoft.CodeAnalysis.DocumentationCommentId.CreateDeclarationId(resolved!);

        var result = await GetTypeHierarchyTool.ExecuteAsync(state, stableId!, GetTypeHierarchyTool.DefaultMaxResults, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("IGreeting", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("SpecialGreeting", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_InterfaceType_ReturnsImplementingClasses()
    {
        var state = _fixture.CreateServer();

        var result = await GetTypeHierarchyTool.ExecuteAsync(state, "IGreeting", GetTypeHierarchyTool.DefaultMaxResults, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("BaseGreeting", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_LeafClassWithoutDerivedTypes_ReturnsNoDerivedTypesMessage()
    {
        var state = _fixture.CreateServer();

        var result = await GetTypeHierarchyTool.ExecuteAsync(state, "SpecialGreeting", GetTypeHierarchyTool.DefaultMaxResults, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("BaseGreeting", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Keine abgeleiteten Typen.", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ClassWithImplicitObjectBase_ReturnsExternalBaseTypeInsteadOfEmptyMessage()
    {
        var state = _fixture.CreateServer();

        var result = await GetTypeHierarchyTool.ExecuteAsync(state, "BaseGreeting", GetTypeHierarchyTool.DefaultMaxResults, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("object", textContent.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Keine Basisklasse.", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_TypeWithExternalInterface_ReturnsExternalInterfaceInsteadOfEmptyMessage()
    {
        var state = _fixture.CreateServer();

        var result = await GetTypeHierarchyTool.ExecuteAsync(state, "DisposableGreeting", GetTypeHierarchyTool.DefaultMaxResults, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("IDisposable", textContent.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Keine Interfaces.", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_TypeWithDiRegistration_IncludesDiRegistrationSection()
    {
        using var context = new McpInMemoryTestContext(DiRegistrationMiniSolutionSpec.Create());
        using var state = context.CreateServer();

        var result = await GetTypeHierarchyTool.ExecuteAsync(state, "ConsoleReporter", GetTypeHierarchyTool.DefaultMaxResults, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("DI-Registrierungen (heuristisch", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("AddScoped", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_TypeWithoutDiRegistration_OmitsDiRegistrationSection()
    {
        var state = _fixture.CreateServer();

        var result = await GetTypeHierarchyTool.ExecuteAsync(state, "BaseGreeting", GetTypeHierarchyTool.DefaultMaxResults, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.DoesNotContain("DI-Registrierungen", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_CompileErrorFixture_ReturnsResultsWithoutCompileErrorHint()
    {
        using var context = new McpInMemoryTestContext(CompileErrorMiniSolutionSpec.CreatePlural());
        using var state = context.CreateServer();

        var result = await GetTypeHierarchyTool.ExecuteAsync(state, "ValidClassA", GetTypeHierarchyTool.DefaultMaxResults, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.DoesNotContain("Compile-Fehler", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteRouted_AssemblyAndProjectRoutes_ValidateAssemblySymbolIdentityAndAllowProjectSymbols()
    {
        using var temp = TestTempDirectory.Create("get-type-hierarchy-route-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "HierarchyProbe",
            "namespace Probe; public interface IService { } public class Service : IService { }");

        await using var assemblyRegistry = new AssemblyAnalysisRegistry();
        await using var projectRegistry = ProjectWiringFixtures.CreateLoadedRegistry();
        var targetRoute = AnalysisToolCall.CreateTargetPathRoute(
            ProjectAnalysisDispatcher.CreateRoute(projectRegistry),
            AssemblyAnalysisDispatcher.CreateRoute(assemblyRegistry));

        // 1. Aktuelle verpackte Assembly-ID auf Assembly-Ziel ist erfolgreich
        var firstLeaseResult = await assemblyRegistry.LeaseAsync(assemblyPath);
        Assert.NotNull(firstLeaseResult.Lease);
        using var firstLease = firstLeaseResult.Lease!;
        var serviceSymbol = firstLease.Context.Compilation.GetTypeByMetadataName("Probe.Service")!;
        var firstAssemblySymbolId = CallGraphTraversal.GetStableSymbolId(serviceSymbol, firstLease.Server.AssemblySymbolIdentity);
        Assert.StartsWith("a:", firstAssemblySymbolId, StringComparison.Ordinal);

        var assemblyCallResult = await AnalysisToolCall.ExecuteRouted(
            targetRoute,
            new AnalysisToolCallRequest(
                new AnalysisTargetRequest(assemblyPath),
                new AnalysisToolDispatch(
                    ProjectCall: lease => GetTypeHierarchyTool.ExecuteAsync(lease.Server, firstAssemblySymbolId, GetTypeHierarchyTool.DefaultMaxResults, default),
                    AssemblySessionCall: lease => GetTypeHierarchyTool.ExecuteAsync(lease.Server, firstAssemblySymbolId, GetTypeHierarchyTool.DefaultMaxResults, default))));

        Assert.NotEqual(true, assemblyCallResult.IsError);
        var assemblyText = TextOf(assemblyCallResult);
        Assert.True(assemblyText.Contains("IService", StringComparison.Ordinal), assemblyText);
        Assert.Contains("Probe.IService", assemblyText, StringComparison.Ordinal);

        // 2. Generationenwechsel ueber A -> B -> A
        AssemblyTestHelper.EmitAssembly(
            temp,
            "HierarchyProbe",
            "namespace Probe; public interface IOther { } public class Other : IOther { }");
        var secondLeaseResult = await assemblyRegistry.LeaseAsync(assemblyPath);
        secondLeaseResult.Lease!.Dispose();

        AssemblyTestHelper.EmitAssembly(
            temp,
            "HierarchyProbe",
            "namespace Probe; public interface IService { } public class Service : IService { }");
        var thirdLeaseResult = await assemblyRegistry.LeaseAsync(assemblyPath);
        var thirdLease = thirdLeaseResult.Lease!;
        var currentServiceSymbol = thirdLease.Context.Compilation.GetTypeByMetadataName("Probe.Service")!;
        var currentAssemblySymbolId = CallGraphTraversal.GetStableSymbolId(currentServiceSymbol, thirdLease.Server.AssemblySymbolIdentity);
        thirdLease.Dispose();

        // 3. Die aktuelle Snapshot-ID bleibt nach interner Generationseviction gültig.
        var stableCallResult = await AnalysisToolCall.ExecuteRouted(
            targetRoute,
            new AnalysisToolCallRequest(
                new AnalysisTargetRequest(assemblyPath),
                new AnalysisToolDispatch(
                    ProjectCall: lease => GetTypeHierarchyTool.ExecuteAsync(lease.Server, currentAssemblySymbolId, GetTypeHierarchyTool.DefaultMaxResults, default),
                    AssemblySessionCall: lease => GetTypeHierarchyTool.ExecuteAsync(lease.Server, currentAssemblySymbolId, GetTypeHierarchyTool.DefaultMaxResults, default))));

        Assert.NotEqual(true, stableCallResult.IsError);
        var stableText = TextOf(stableCallResult);
        Assert.True(stableText.Contains("IService", StringComparison.Ordinal), stableText);

        // 4. Eine bare DocumentationCommentId bleibt eine direkte fachliche Suchanfrage.
        var unwrappedCallResult = await AnalysisToolCall.ExecuteRouted(
            targetRoute,
            new AnalysisToolCallRequest(
                new AnalysisTargetRequest(assemblyPath),
                new AnalysisToolDispatch(
                    ProjectCall: lease => GetTypeHierarchyTool.ExecuteAsync(lease.Server, "T:Probe.Service", GetTypeHierarchyTool.DefaultMaxResults, default),
                    AssemblySessionCall: lease => GetTypeHierarchyTool.ExecuteAsync(lease.Server, "T:Probe.Service", GetTypeHierarchyTool.DefaultMaxResults, default))));

        Assert.NotEqual(true, unwrappedCallResult.IsError);
        var unwrappedText = TextOf(unwrappedCallResult);
        Assert.True(unwrappedText.Contains("IService", StringComparison.Ordinal), unwrappedText);

        // 5. Konkreter Solution-Pfad auf Projekt-Ziel bleibt weiterhin erfolgreich
        var projectRoot = ProjectRegistryFixture.CreateProjectRoot(temp, "probe-proj");
        var solutionPath = Path.Combine(projectRoot, "app.slnx");
        var projectCallResult = await AnalysisToolCall.ExecuteRouted(
            targetRoute,
            new AnalysisToolCallRequest(
                new AnalysisTargetRequest(solutionPath),
                new AnalysisToolDispatch(
                    ProjectCall: lease => GetTypeHierarchyTool.ExecuteAsync(lease.Server, "BaseGreeting", GetTypeHierarchyTool.DefaultMaxResults, default),
                    AssemblySessionCall: lease => GetTypeHierarchyTool.ExecuteAsync(lease.Server, "BaseGreeting", GetTypeHierarchyTool.DefaultMaxResults, default))));

        Assert.NotEqual(true, projectCallResult.IsError);
        var projectText = TextOf(projectCallResult);
        Assert.Contains("IGreeting", projectText, StringComparison.Ordinal);
        Assert.Contains("SpecialGreeting", projectText, StringComparison.Ordinal);
    }
}
