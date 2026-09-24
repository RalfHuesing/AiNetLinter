#nullable enable

using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.FeatureContext;
using AiNetLinter.Mcp.Tools.FileStructure;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Mcp.Tools.TypeHierarchy;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.SymbolGraph;

[Trait("Category", "Component")]
public sealed class SymbolHandoffRelationshipPrecisionTests
{
    [Fact]
    public async Task InterfaceMethodHandoff_SeparatesCallSitesFromImplementation()
    {
        using var scenario = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\InterfaceMethodHandoff.slnx",
            new ProjectSpec("App", [
                ("Contracts.cs", """
                    namespace TestNs;
                    public interface IProcessor { string Process(int value); }
                    public sealed class Processor : IProcessor
                    {
                        public string Process(int value) => value.ToString();
                    }
                    public static class Consumer
                    {
                        public static string ThroughContract(IProcessor processor) => processor.Process(1);
                        public static string ThroughConcrete(Processor processor) => processor.Process(2);
                        public static Type MentionType() => typeof(IProcessor);
                    }
                    """)
            ], VirtualProjectDirectory: "src/App"));
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(null, ReadOnlySolutionSnapshot: scenario.Solution)));

        var contractStructure = await GetClassStructureTool.ExecuteAsync(
            state, "TestNs.IProcessor", "name", CancellationToken.None);
        var contractText = TextOf(contractStructure);
        var contractRow = contractText.Split('\n').Single(line => line.StartsWith("| Method | Process |", StringComparison.Ordinal));
        var contractHandoff = ExtractHandoff(contractRow);

        var references = await FindReferencesTool.ExecuteAsync(
            state, new FindReferencesRequest(contractHandoff, MaxResults: 50, Depth: 1), CancellationToken.None);
        var implementationResult = await FindImplementationsTool.ExecuteAsync(
            state, contractHandoff, 50, CancellationToken.None);
        var referencesText = TextOf(references);
        var implementationText = TextOf(implementationResult);

        Assert.False(references.IsError is true, referencesText);
        Assert.Contains("Contracts.cs:9", referencesText, StringComparison.Ordinal);
        Assert.Contains("Contracts.cs:10", referencesText, StringComparison.Ordinal);
        Assert.DoesNotContain("Contracts.cs:11", referencesText, StringComparison.Ordinal);
        Assert.DoesNotContain("Processor.Process(int value)", referencesText, StringComparison.Ordinal);
        Assert.Contains("Processor.Process", implementationText, StringComparison.Ordinal);
        Assert.Matches(@"handoffId: `h:[^`]+`", implementationText);
    }

    [Fact]
    public async Task OverrideHandoff_MapsBaseAndOverrideAndReportsPolymorphicCallSites()
    {
        using var scenario = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\OverrideMethodHandoff.slnx",
            new ProjectSpec("App", [
                ("Methods.cs", """
                    namespace TestNs;
                    public class BaseWorker { public virtual string Run(int value) => value.ToString(); }
                    public sealed class SpecializedWorker : BaseWorker
                    {
                        public override string Run(int value) => $"special:{value}";
                    }
                    public static class Consumer
                    {
                        public static string ThroughBase(BaseWorker worker) => worker.Run(1);
                        public static string ThroughOverride(SpecializedWorker worker) => worker.Run(2);
                    }
                    """)
            ], VirtualProjectDirectory: "src/App"));
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(null, ReadOnlySolutionSnapshot: scenario.Solution)));

        var baseStructure = await GetClassStructureTool.ExecuteAsync(
            state, "TestNs.BaseWorker", "name", CancellationToken.None);
        var baseText = TextOf(baseStructure);
        var baseRow = baseText.Split('\n').Single(line => line.StartsWith("| Method | Run |", StringComparison.Ordinal));
        var baseHandoff = ExtractHandoff(baseRow);
        var implementations = await FindImplementationsTool.ExecuteAsync(
            state, baseHandoff, 50, CancellationToken.None);
        var implementationText = TextOf(implementations);
        Assert.Contains("SpecializedWorker.Run", implementationText, StringComparison.Ordinal);
        var overrideHandoff = ExtractHandoff(implementationText);

        var baseReferences = await FindReferencesTool.ExecuteAsync(
            state, new FindReferencesRequest(baseHandoff, MaxResults: 50, Depth: 1), CancellationToken.None);
        var overrideReferences = await FindReferencesTool.ExecuteAsync(
            state, new FindReferencesRequest(overrideHandoff, MaxResults: 50, Depth: 1), CancellationToken.None);
        var overrideBody = await GetSymbolBodyTool.ExecuteAsync(state, [overrideHandoff], 80, CancellationToken.None);
        var overrideContext = await GetFeatureContextTool.ExecuteAsync(
            state, new FeatureContextOptions(overrideHandoff), CancellationToken.None);
        var baseReferencesText = TextOf(baseReferences);
        var overrideReferencesText = TextOf(overrideReferences);

        Assert.False(baseReferences.IsError is true, baseReferencesText);
        Assert.False(overrideReferences.IsError is true, overrideReferencesText);
        Assert.Contains("Methods.cs:9", baseReferencesText, StringComparison.Ordinal);
        Assert.Contains("Methods.cs:10", baseReferencesText, StringComparison.Ordinal);
        Assert.Contains("Methods.cs:9", overrideReferencesText, StringComparison.Ordinal);
        Assert.Contains("Methods.cs:10", overrideReferencesText, StringComparison.Ordinal);
        Assert.Contains("special:", TextOf(overrideBody), StringComparison.Ordinal);
        Assert.Contains("SpecializedWorker.Run", TextOf(overrideContext), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ComplexGenericMethodHandoff_ResolvesExactOverloadAcrossFollowUpTools()
    {
        using var scenario = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\ComplexGenericMethodHandoff.slnx",
            new ProjectSpec("App", [
                ("ComplexMethods.cs", """
                    #nullable enable
                    using System;
                    using System.Collections.Generic;
                    namespace TestNs;
                    public sealed class ComplexMethods
                    {
                        public T? Convert<T>(Dictionary<string, List<T?[]?>>? values, Func<string, T?>[]? callbacks) where T : class
                            => values is null || callbacks is null ? default : callbacks[0](values.Count.ToString());
                        public string Convert(Dictionary<string, List<int?[]?>> values) => values.Count.ToString();
                    }
                    public static class Consumer
                    {
                        public static string Use(ComplexMethods value) => value.Convert<string>(null, null)!;
                        public static string UseOverload(ComplexMethods value) => value.Convert(new Dictionary<string, List<int?[]?>>());
                    }
                    """)
            ], VirtualProjectDirectory: "src/App"));
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(null, ReadOnlySolutionSnapshot: scenario.Solution)));

        var structure = await GetClassStructureTool.ExecuteAsync(
            state, "TestNs.ComplexMethods", "name", CancellationToken.None);
        var structureText = TextOf(structure);
        var row = structureText.Split('\n').SingleOrDefault(line =>
            line.StartsWith("| Method | Convert |", StringComparison.Ordinal)
            && line.Contains("callbacks", StringComparison.Ordinal));
        Assert.True(row is not null, structureText);
        var handoffId = ExtractHandoff(row!);

        var references = await FindReferencesTool.ExecuteAsync(
            state, new FindReferencesRequest(handoffId, MaxResults: 50, Depth: 1), CancellationToken.None);
        var body = await GetSymbolBodyTool.ExecuteAsync(state, [handoffId], 80, CancellationToken.None);
        var context = await GetFeatureContextTool.ExecuteAsync(
            state, new FeatureContextOptions(handoffId), CancellationToken.None);
        var referencesText = TextOf(references);
        var bodyText = TextOf(body);
        var contextText = TextOf(context);

        Assert.False(references.IsError is true, referencesText);
        Assert.False(body.IsError is true, bodyText);
        Assert.False(context.IsError is true, contextText);
        Assert.Contains(
            "src/App/ComplexMethods.cs:13 - Aufruf von 'ComplexMethods.Convert'",
            referencesText,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "src/App/ComplexMethods.cs:14 - Aufruf von 'ComplexMethods.Convert'",
            referencesText,
            StringComparison.Ordinal);
        Assert.Contains("Convert<T>", bodyText, StringComparison.Ordinal);
        Assert.Contains("Convert<T>", contextText, StringComparison.Ordinal);
    }

    private static string ExtractHandoff(string text)
    {
        var match = Regex.Match(text, @"handoffId: `(?<id>h:[^`]+)`", RegexOptions.CultureInvariant);
        Assert.True(match.Success, text);
        return match.Groups["id"].Value;
    }

    private static string TextOf(ModelContextProtocol.Protocol.CallToolResult result) =>
        string.Join(Environment.NewLine, result.Content.OfType<ModelContextProtocol.Protocol.TextContentBlock>().Select(block => block.Text));
}
