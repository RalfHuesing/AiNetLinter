#nullable enable

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.Mcp.Scope;
using AiNetLinter.Mcp.Tools.TypeHierarchy;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.TypeHierarchy;

[Trait("Category", "Component")]
public sealed class FindImplementationsTests
{
    [Fact]
    public async Task ExecuteAsync_ScopeFiltersGeneratedAndTestImplementationsBeforeLimit()
    {
        using var solution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\ScopeImplementations.slnx",
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
        var state = fixture.CreateServer();

        var productionFirst = await FindImplementationsTool.ExecuteAsync(
            new FindImplementationsRequest(
                state, "App.IService", 1, McpScopeType.All, false, CancellationToken.None));
        var productionPayload = productionFirst.StructuredContent!.Value;
        Assert.Equal(2, productionPayload.GetProperty("totalCount").GetInt32());
        Assert.Contains("ProductionService", productionPayload.GetProperty("implementations")[0].GetProperty("typeName").GetString(), StringComparison.Ordinal);

        var tests = await FindImplementationsTool.ExecuteAsync(
            new FindImplementationsRequest(
                state, "App.IService", 50, McpScopeType.Tests, false, CancellationToken.None));
        Assert.Equal(1, tests.StructuredContent!.Value.GetProperty("totalCount").GetInt32());
        Assert.Equal("tests", tests.StructuredContent!.Value.GetProperty("implementations")[0].GetProperty("scopeType").GetString());

        var generated = await FindImplementationsTool.ExecuteAsync(
            new FindImplementationsRequest(
                state, "App.IService", 50, McpScopeType.All, true, CancellationToken.None));
        Assert.Equal(3, generated.StructuredContent!.Value.GetProperty("totalCount").GetInt32());
        Assert.Contains(
            generated.StructuredContent!.Value.GetProperty("implementations").EnumerateArray(),
            item => item.GetProperty("sourceKind").GetString() == "generated");
    }
    [Fact]
    public async Task ExecuteAsync_InterfaceType_ReturnsAllImplementingClasses()
    {
        using var fixture = new McpInMemoryTestContext(TransitiveSymbolGraphMiniSolutionSpec.Create());
        var server = fixture.CreateServer();

        var result = await FindImplementationsTool.ExecuteAsync(
            server, "IProcessor", maxResults: 50, ct: CancellationToken.None);

        Assert.True(result.IsError is null or false);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("BaseProcessor", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("DerivedProcessor", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("MoreDerivedProcessor", textContent.Text, StringComparison.Ordinal);

        var dto = JsonSerializer.Deserialize<FindImplementationsResultDto>(
            result.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default);
        Assert.NotNull(dto);
        Assert.Equal(3, dto!.TotalCount);
        Assert.False(dto.IsTruncated);
        Assert.Contains(dto.Implementations, i => i.TypeName.Contains("BaseProcessor", StringComparison.Ordinal));
        Assert.Contains(dto.Implementations, i => i.TypeName.Contains("DerivedProcessor", StringComparison.Ordinal));
        Assert.Contains(dto.Implementations, i => i.TypeName.Contains("MoreDerivedProcessor", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_HandoffIsStructuredOnly_AndEntriesDoNotRepeatRootMetadata()
    {
        using var fixture = new McpInMemoryTestContext(TransitiveSymbolGraphMiniSolutionSpec.Create());
        var result = await FindImplementationsTool.ExecuteAsync(
            fixture.CreateServer(), "IProcessor", maxResults: 50, ct: CancellationToken.None);

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.DoesNotContain("handoff=", text, StringComparison.Ordinal);
        Assert.DoesNotContain("id: `s:", text, StringComparison.Ordinal);

        var entry = result.StructuredContent!.Value.GetProperty("implementations")[0];
        Assert.StartsWith("s:", entry.GetProperty("id").GetString(), StringComparison.Ordinal);
        Assert.Equal("type", entry.GetProperty("handoffKind").GetString());
        Assert.False(entry.TryGetProperty("handoff", out _));
        Assert.False(entry.TryGetProperty("targetPath", out _));
        Assert.False(entry.TryGetProperty("snapshot", out _));
        Assert.False(entry.TryGetProperty("allowedFollowUpTools", out _));
    }

    [Fact]
    public async Task ExecuteAsync_StructuredImplementationIdCanBeUsedForHierarchyFollowUp()
    {
        using var fixture = new McpInMemoryTestContext(TransitiveSymbolGraphMiniSolutionSpec.Create());
        var state = fixture.CreateServer();
        var implementations = await FindImplementationsTool.ExecuteAsync(
            state, "IProcessor", maxResults: 50, ct: CancellationToken.None);
        var id = implementations.StructuredContent!.Value.GetProperty("implementations")[0]
            .GetProperty("id").GetString();

        var hierarchy = await GetTypeHierarchyTool.ExecuteAsync(
            state, id, GetTypeHierarchyTool.DefaultMaxResults, CancellationToken.None);

        Assert.NotEqual(true, hierarchy.IsError);
        Assert.Contains(
            "IProcessor",
            Assert.IsType<TextContentBlock>(Assert.Single(hierarchy.Content)).Text,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_SourceHandoffIdFromFindSymbol_ReturnsImplementations()
    {
        using var fixture = new McpInMemoryTestContext(TransitiveSymbolGraphMiniSolutionSpec.Create());
        var server = fixture.CreateServer();

        var discovery = await FindSymbolTool.ExecuteAsync(
            server,
            ["IProcessor"],
            kind: "interface",
            maxResults: 50,
            CancellationToken.None);
        var handoffId = discovery.StructuredContent!.Value
            .GetProperty("results")[0]
            .GetProperty("matches")[0]
            .GetProperty("id")
            .GetString();
        Assert.StartsWith("s:", handoffId, StringComparison.Ordinal);

        var result = await FindImplementationsTool.ExecuteAsync(
            server,
            handoffId,
            maxResults: 50,
            ct: CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var dto = JsonSerializer.Deserialize<FindImplementationsResultDto>(
            result.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default);
        Assert.NotNull(dto);
        Assert.Equal(3, dto!.TotalCount);
    }

    [Fact]
    public async Task ExecuteAsync_InterfaceMethod_ReturnsImplementingMethods()
    {
        using var fixture = new McpInMemoryTestContext(TransitiveSymbolGraphMiniSolutionSpec.Create());
        var server = fixture.CreateServer();

        var result = await FindImplementationsTool.ExecuteAsync(
            server, "IProcessor.Execute", maxResults: 50, ct: CancellationToken.None);

        Assert.True(result.IsError is null or false);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("BaseProcessor.Execute", textContent.Text, StringComparison.Ordinal);

        var dto = JsonSerializer.Deserialize<FindImplementationsResultDto>(
            result.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default);
        Assert.NotNull(dto);
        Assert.NotEmpty(dto!.Implementations);
        Assert.Contains(dto.Implementations, i => i.MemberName == "Execute");
    }

    [Fact]
    public async Task ExecuteAsync_VirtualMethod_ReturnsOverrides()
    {
        using var fixture = new McpInMemoryTestContext(TransitiveSymbolGraphMiniSolutionSpec.Create());
        var server = fixture.CreateServer();

        var result = await FindImplementationsTool.ExecuteAsync(
            server, "BaseProcessor.Execute", maxResults: 50, ct: CancellationToken.None);

        Assert.True(result.IsError is null or false);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("DerivedProcessor.Execute", textContent.Text, StringComparison.Ordinal);

        var dto = JsonSerializer.Deserialize<FindImplementationsResultDto>(
            result.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default);
        Assert.NotNull(dto);
        Assert.True(dto!.TotalCount >= 1);
        Assert.Contains(dto.Implementations, i => i.TypeName.Contains("DerivedProcessor", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_AbstractClass_ReturnsDerivedClassesWithStatus()
    {
        var code = """
            namespace Shapes;

            public abstract class BaseShape
            {
                public abstract double Area();
            }

            public abstract class Polygon : BaseShape
            {
            }

            public class Circle : BaseShape
            {
                public override double Area() => 3.14;
            }
            """;

        using var solution = RoslynTestSolutionFactory.CreateSolution(code, "Shapes", "Shapes.cs");
        using var fixture = new McpInMemoryTestContext(solution);
        var server = fixture.CreateServer();

        var result = await FindImplementationsTool.ExecuteAsync(
            server, "BaseShape", maxResults: 50, ct: CancellationToken.None);

        Assert.True(result.IsError is null or false);
        var dto = JsonSerializer.Deserialize<FindImplementationsResultDto>(
            result.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default);
        Assert.NotNull(dto);
        Assert.Equal(2, dto!.TotalCount);

        var polygon = Assert.Single(dto.Implementations, i => i.TypeName.Contains("Polygon", StringComparison.Ordinal));
        Assert.Equal("abstract", polygon.Status);

        var circle = Assert.Single(dto.Implementations, i => i.TypeName.Contains("Circle", StringComparison.Ordinal));
        Assert.Equal("concrete", circle.Status);
    }

    [Fact]
    public async Task ExecuteAsync_MaxResultsTruncates_ReturnsTruncatedResult()
    {
        using var fixture = new McpInMemoryTestContext(TransitiveSymbolGraphMiniSolutionSpec.Create());
        var server = fixture.CreateServer();

        var result = await FindImplementationsTool.ExecuteAsync(
            server, "IProcessor", maxResults: 1, ct: CancellationToken.None);

        Assert.True(result.IsError is null or false);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Ergebnis trunkiert", textContent.Text, StringComparison.Ordinal);

        var dto = JsonSerializer.Deserialize<FindImplementationsResultDto>(
            result.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default);
        Assert.NotNull(dto);
        Assert.True(dto!.IsTruncated);
        Assert.Equal(3, dto.TotalCount);
        Assert.Single(dto.Implementations);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownSymbol_ReturnsSymbolNotFound()
    {
        using var fixture = new McpInMemoryTestContext(TransitiveSymbolGraphMiniSolutionSpec.Create());
        var server = fixture.CreateServer();

        var result = await FindImplementationsTool.ExecuteAsync(
            server, "NonExistentTypeXyz", maxResults: 50, ct: CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SYMBOL_NOT_FOUND", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_NonVirtualMethod_ReturnsInvalidArgument()
    {
        var code = """
            namespace App;

            public class Service
            {
                public void NonVirtualMethod() {}
            }
            """;

        using var solution = RoslynTestSolutionFactory.CreateSolution(code, "App", "Service.cs");
        using var fixture = new McpInMemoryTestContext(solution);
        var server = fixture.CreateServer();

        var result = await FindImplementationsTool.ExecuteAsync(
            server, "Service.NonVirtualMethod", maxResults: 50, ct: CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_AssemblyTarget_FindsImplementationsInAssembly()
    {
        using var temp = TestTempDirectory.Create("find-impl-asm-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "ImplProbe",
            "namespace Probe; public interface IGreeter { string Greet(); } public class EnglishGreeter : IGreeter { public string Greet() => \"Hello\"; }");

        await using var assemblyRegistry = new AssemblyAnalysisRegistry();
        var leaseResult = await assemblyRegistry.LeaseAsync(assemblyPath);
        Assert.NotNull(leaseResult.Lease);
        using var lease = leaseResult.Lease!;

        var result = await FindImplementationsTool.ExecuteAsync(
            lease.Server, "IGreeter", maxResults: 50, ct: CancellationToken.None);

        Assert.True(result.IsError is null or false);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("EnglishGreeter", textContent.Text, StringComparison.Ordinal);

        var dto = JsonSerializer.Deserialize<FindImplementationsResultDto>(
            result.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default);
        Assert.NotNull(dto);
        Assert.Equal(1, dto!.TotalCount);
        Assert.Contains(dto.Implementations, i => i.TypeName.Contains("EnglishGreeter", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_MetadataInterface_ReturnsImplementingClasses()
    {
        var code = """
            using System;
            namespace App;
            public class DisposableWorker : IDisposable
            {
                public void Dispose() { }
            }
            """;

        using var solution = RoslynTestSolutionFactory.CreateSolution(code, "App", "Worker.cs");
        using var fixture = new McpInMemoryTestContext(solution);
        var server = fixture.CreateServer();

        var result = await FindImplementationsTool.ExecuteAsync(
            server, "IDisposable", maxResults: 50, ct: CancellationToken.None);

        Assert.True(result.IsError is null or false);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("DisposableWorker", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_GenericInterface_ReturnsImplementingClasses()
    {
        var code = """
            namespace App;
            public interface IRepository<T> { void Save(T item); }
            public class OrderRepository : IRepository<string>
            {
                public void Save(string item) { }
            }
            """;

        using var solution = RoslynTestSolutionFactory.CreateSolution(code, "App", "Repo.cs");
        using var fixture = new McpInMemoryTestContext(solution);
        var server = fixture.CreateServer();

        var resultWithGenericParam = await FindImplementationsTool.ExecuteAsync(
            server, "IRepository<T>", maxResults: 50, ct: CancellationToken.None);

        Assert.True(resultWithGenericParam.IsError is null or false);
        var textWithParam = Assert.IsType<TextContentBlock>(Assert.Single(resultWithGenericParam.Content));
        Assert.Contains("OrderRepository", textWithParam.Text, StringComparison.Ordinal);

        var resultWithoutGenericParam = await FindImplementationsTool.ExecuteAsync(
            server, "IRepository", maxResults: 50, ct: CancellationToken.None);

        Assert.True(resultWithoutGenericParam.IsError is null or false);
        var textWithoutParam = Assert.IsType<TextContentBlock>(Assert.Single(resultWithoutGenericParam.Content));
        Assert.Contains("OrderRepository", textWithoutParam.Text, StringComparison.Ordinal);
    }
}
