#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Handoffs;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.FeatureContext;
using AiNetLinter.Mcp.Tools.FileStructure;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Mcp.Tools.Verify;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.SymbolGraph
{

[Trait("Category", "Component")]
public sealed class MetadataConstructorHandoffTests
{
    [Fact]
    public async Task ConstructorHandoff_WithMetadataParameterType_ResolvesReferencesAndBody()
    {
        var metadataReference = MetadataReference.CreateFromFile(typeof(MetadataFixtures.MetadataParameterType).Assembly.Location);
        using var scenario = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\MetadataConstructorHandoff.slnx",
            new ProjectSpec("App", [
                ("Constructors.cs", """
                    #nullable enable
                    namespace TestNs;
                    public sealed class Target
                    {
                        public Target(MetadataFixtures.MetadataParameterType dependency) => _ = dependency;
                    }

                    public static class Consumer
                    {
                        public static Target Create() => new(new MetadataFixtures.MetadataParameterType());
                    }
                    """)
            ], AdditionalReferences: [metadataReference], VirtualProjectDirectory: "src/App"));
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(null, ReadOnlySolutionSnapshot: scenario.Solution)));

        var structure = await GetClassStructureTool.ExecuteAsync(
            state, "TestNs.Target", "name", CancellationToken.None);
        var structureText = TextOf(structure);
        var row = structureText.Split('\n').SingleOrDefault(line =>
            line.StartsWith("| Constructor | .ctor |", StringComparison.Ordinal));
        Assert.True(row is not null, structureText);
        var handoffId = System.Text.RegularExpressions.Regex.Match(
            row!, @"handoffId: `(?<id>h:[^`]+)`", System.Text.RegularExpressions.RegexOptions.CultureInvariant)
            .Groups["id"].Value;
        Assert.NotEmpty(handoffId);

        var references = await FindReferencesTool.ExecuteAsync(
            state, new FindReferencesRequest(handoffId, MaxResults: 20, Depth: 1), CancellationToken.None);
        var body = await GetSymbolBodyTool.ExecuteAsync(state, [handoffId], 80, CancellationToken.None);

        Assert.False(references.IsError is true, TextOf(references));
        Assert.False(body.IsError is true, TextOf(body));
        Assert.Contains("Aufruf von 'Target..ctor'", TextOf(references), StringComparison.Ordinal);
        Assert.Contains("Target.Target", TextOf(body), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PrimaryConstructorHandoff_WithMetadataParameterType_ResolvesReferences()
    {
        var metadataReference = MetadataReference.CreateFromFile(typeof(MetadataFixtures.MetadataParameterType).Assembly.Location);
        using var scenario = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\MetadataPrimaryConstructorHandoff.slnx",
            new ProjectSpec("App", [
                ("Primary.cs", """
                    #nullable enable
                    namespace TestNs;
                    public sealed class Payload { }
                    public sealed class PrimaryTarget(MetadataFixtures.MetadataContainer<Payload> dependency) { }

                    """)
            ], AdditionalReferences: [metadataReference], VirtualProjectDirectory: "src/App"));
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(
                null,
                Config: TestHelper.CreateDefaultConfig(),
                ReadOnlySolutionSnapshot: scenario.Solution)));

        var verify = await VerifyTool.ExecuteAsync(state, VerifyScope.Solution, CancellationToken.None);
        var verifyText = TextOf(verify);
        var rows = verifyText.Split('\n').Where(line =>
            line.Contains("category=dead_code", StringComparison.Ordinal)
            && line.Contains("Primary.cs:4", StringComparison.Ordinal)).ToArray();
        Assert.True(rows.Length > 0, verifyText);
        var candidate = Assert.Single(rows);
        var typeId = System.Text.RegularExpressions.Regex.Match(candidate, @"symbolIdentifier=(?<id>h:[A-Za-z0-9]+)",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant).Groups["id"].Value;
        Assert.NotEmpty(typeId);
        var handoffId = await ConstructorHandoffLifecycleTests.ConstructorFromTypeAsync(state, typeId, "PrimaryTarget.PrimaryTarget");
        var body = await GetSymbolBodyTool.ExecuteAsync(state, [handoffId], 80, CancellationToken.None);
        var constructorBodyText = TextOf(body);
        Assert.False(body.IsError is true, constructorBodyText);

        var references = await FindReferencesTool.ExecuteAsync(
            state, new FindReferencesRequest(handoffId!, MaxResults: 20, Depth: 1), CancellationToken.None);
        var context = await GetFeatureContextTool.ExecuteAsync(
            state, new FeatureContextOptions(handoffId!), CancellationToken.None);
        var contextText = TextOf(context);

        Assert.False(references.IsError is true, TextOf(references));
        Assert.False(context.IsError is true, contextText);
        Assert.Contains("usage=unreferenced", candidate, StringComparison.Ordinal);
        Assert.Contains("PrimaryTarget.PrimaryTarget", constructorBodyText!, StringComparison.Ordinal);
        Assert.Contains("PrimaryTarget.PrimaryTarget", contextText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConstructorHandoff_WithMixedSourceAndMetadataParameters_ResolvesAcrossProjects()
    {
        var metadataReference = MetadataReference.CreateFromFile(typeof(MetadataFixtures.MetadataParameterType).Assembly.Location);
        using var scenario = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\MultiProjectMetadataConstructorHandoff.slnx",
            new ProjectSpec("App", [
                ("Services.cs", """
                    #nullable enable
                    namespace Probe;
                    public sealed class Payload { }
                    public interface ISourceDependency { }
                    public sealed class Resolver
                    {
                        public Resolver(
                            ISourceDependency dependency,
                            MetadataFixtures.MetadataContainer<Payload> options)
                        {
                            _ = dependency;
                            _ = options;
                        }
                    }
                    """)
            ], AdditionalReferences: [metadataReference], VirtualProjectDirectory: "src/App"),
            new ProjectSpec("App.Tests", [
                ("ResolverTests.cs", """
                    #nullable enable
                    namespace Probe.Tests;
                    public sealed class ResolverTests
                    {
                        public object Create(Probe.ISourceDependency dependency) =>
                            new Probe.Resolver(dependency, new MetadataFixtures.MetadataContainer<Probe.Payload>());
                    }
                    """)
            ], ProjectReferences: ["App"], AdditionalReferences: [metadataReference], VirtualProjectDirectory: "tests/App.Tests"));
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(null, ReadOnlySolutionSnapshot: scenario.Solution)));

        var appProject = scenario.Solution.Projects.Single(project => project.Name == "App");
        var compilation = await appProject.GetCompilationAsync(CancellationToken.None);
        Assert.NotNull(compilation);
        var resolverType = compilation!.GetTypeByMetadataName("Probe.Resolver");
        Assert.NotNull(resolverType);
        var constructorSymbol = resolverType!.InstanceConstructors.Single(symbol => !symbol.IsStatic);
        var constructorDocId = DocumentationCommentId.CreateDeclarationId(constructorSymbol);
        Assert.NotNull(constructorDocId);

        var structure = await GetClassStructureTool.ExecuteAsync(
            state, "Probe.Resolver", "name", CancellationToken.None);
        var structureText = TextOf(structure);
        var row = structureText.Split('\n').SingleOrDefault(line =>
            line.StartsWith("| Constructor | .ctor |", StringComparison.Ordinal));
        Assert.True(row is not null, structureText);
        var handoffId = System.Text.RegularExpressions.Regex.Match(
            row!, @"handoffId: `(?<id>h:[^`]+)`", System.Text.RegularExpressions.RegexOptions.CultureInvariant)
            .Groups["id"].Value;
        Assert.NotEmpty(handoffId);
        var restored = HandoffHandleRegistry.Default.RestoreInternalHandoffForInput(handoffId);
        Assert.True(restored.IsSuccess, restored.Error?.Message);
        Assert.Contains($"{constructorDocId}~p:{AnalysisSymbolIdentity.GetStableProjectMarker(appProject)}", restored.Value!, StringComparison.Ordinal);

        var (resolved, resolutionError) = await SymbolIdentifierResolver.TryResolveByStableIdAsync(
            scenario.Solution,
            restored.Value!,
            CancellationToken.None,
            state.HandoffSymbolIdentity);
        Assert.Null(resolutionError);
        Assert.True(SymbolEqualityComparer.Default.Equals(constructorSymbol, resolved));

        var references = await FindReferencesTool.ExecuteAsync(
            state, new FindReferencesRequest(handoffId, MaxResults: 20, Depth: 1), CancellationToken.None);
        var body = await GetSymbolBodyTool.ExecuteAsync(state, [handoffId], 80, CancellationToken.None);
        var context = await GetFeatureContextTool.ExecuteAsync(
            state, new FeatureContextOptions(handoffId), CancellationToken.None);
        var bodyText = TextOf(body);
        var contextText = TextOf(context);

        Assert.False(references.IsError is true, TextOf(references));
        Assert.False(body.IsError is true, bodyText);
        Assert.False(context.IsError is true, contextText);
        Assert.Contains("Aufruf von 'Resolver..ctor'", TextOf(references), StringComparison.Ordinal);
        Assert.Contains("ResolverTests.cs:", TextOf(references), StringComparison.Ordinal);
        Assert.Contains("Resolver.Resolver", bodyText, StringComparison.Ordinal);
        Assert.Contains("Resolver.Resolver", contextText, StringComparison.Ordinal);
    }

    private static string TextOf(CallToolResult result) =>
        string.Join(Environment.NewLine, result.Content.OfType<TextContentBlock>().Select(block => block.Text));
}
}

namespace MetadataFixtures
{

public sealed class MetadataParameterType { }

public sealed class MetadataContainer<T> { }
}
