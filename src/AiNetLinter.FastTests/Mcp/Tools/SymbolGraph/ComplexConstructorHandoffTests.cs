#nullable enable

using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.FastTests;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Scope;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.FeatureContext;
using AiNetLinter.Mcp.Tools.FileStructure;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Mcp.Tools.Verify;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.SymbolGraph;

[Trait("Category", "Component")]
public sealed class ComplexConstructorHandoffTests
{
    [Fact]
    public async Task ComplexConstructorHandoff_FromSkeleton_PreservesExactSymbolAndCallSites()
    {
        using var scenario = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\ComplexConstructorHandoff.slnx",
            new ProjectSpec("App", [
                ("Constructors.cs", """
                    #nullable enable
                    using System;
                    using System.Collections.Generic;

                    namespace TestNs;

                    public sealed class Envelope<T> { }

                    public sealed class ComplexOverload
                    {
                        public ComplexOverload(
                            Dictionary<string, List<Envelope<int?[]?[]>>?>? values,
                            Func<string, int?>[]? callbacks)
                        {
                            _ = values;
                            _ = callbacks;
                        }

                        public ComplexOverload(Envelope<int?[]> value) { _ = value; }
                    }

                    public static class Consumer
                    {
                        public static void Use()
                        {
                            _ = new ComplexOverload(null, null);
                            _ = new ComplexOverload(new Envelope<int?[]>());
                        }

                        public static void MentionOnly(ComplexOverload value) => _ = typeof(ComplexOverload);
                    }
                    """)
            ], VirtualProjectDirectory: "src/App"));
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(null, ReadOnlySolutionSnapshot: scenario.Solution)));

        var skeleton = await GetFileSkeletonTool.ExecuteAsync(state, ["Constructors.cs"], CancellationToken.None);
        var skeletonText = TextOf(skeleton);
        var row = skeletonText.Split('\n').SingleOrDefault(line =>
            line.Contains("ComplexOverload(", StringComparison.Ordinal)
            && line.Contains("callbacks", StringComparison.Ordinal));
        Assert.True(row is not null, skeletonText);

        var handoffId = Regex.Match(row!, @"handoffId: `(?<id>h:[^`]+)`", RegexOptions.CultureInvariant)
            .Groups["id"].Value;
        Assert.NotEmpty(handoffId);
        var otherOverloadRow = skeletonText.Split('\n').SingleOrDefault(line =>
            line.Contains("ComplexOverload(Envelope<int?[]> value", StringComparison.Ordinal));
        Assert.True(otherOverloadRow is not null, skeletonText);
        var otherOverloadHandoffId = Regex.Match(
            otherOverloadRow!, @"handoffId: `(?<id>h:[^`]+)`", RegexOptions.CultureInvariant)
            .Groups["id"].Value;
        Assert.NotEmpty(otherOverloadHandoffId);

        var references = await FindReferencesTool.ExecuteAsync(
            state, new FindReferencesRequest(handoffId, MaxResults: 50, Depth: 1), CancellationToken.None);
        var otherOverloadReferences = await FindReferencesTool.ExecuteAsync(
            state, new FindReferencesRequest(otherOverloadHandoffId, MaxResults: 50, Depth: 1), CancellationToken.None);
        var body = await GetSymbolBodyTool.ExecuteAsync(state, [handoffId], 80, CancellationToken.None);
        var context = await GetFeatureContextTool.ExecuteAsync(
            state, new FeatureContextOptions(handoffId), CancellationToken.None);
        var referencesText = TextOf(references);
        var bodyText = TextOf(body);
        var contextText = TextOf(context);
        var otherOverloadReferencesText = TextOf(otherOverloadReferences);

        Assert.False(references.IsError is true, referencesText);
        Assert.False(otherOverloadReferences.IsError is true, otherOverloadReferencesText);
        Assert.False(body.IsError is true, bodyText);
        Assert.False(context.IsError is true, contextText);
        Assert.Contains(
            "src/App/Constructors.cs:26 - Aufruf von 'ComplexOverload..ctor'",
            referencesText,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "src/App/Constructors.cs:27 - Aufruf von 'ComplexOverload..ctor'",
            referencesText,
            StringComparison.Ordinal);
        Assert.Contains(
            "src/App/Constructors.cs:27 - Aufruf von 'ComplexOverload..ctor'",
            otherOverloadReferencesText,
            StringComparison.Ordinal);
        Assert.DoesNotContain("MentionOnly", referencesText, StringComparison.Ordinal);
        Assert.Contains("ComplexOverload.ComplexOverload", bodyText, StringComparison.Ordinal);
        Assert.Contains("ComplexOverload.ComplexOverload", contextText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ComplexConstructorDocCommentId_IsAcceptedAndUnknownDocIdIsRejected()
    {
        using var scenario = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\ConstructorDocCommentId.slnx",
            new ProjectSpec("App", [
                ("Constructors.cs", """
                    #nullable enable
                    using System.Collections.Generic;
                    namespace TestNs;
                    public sealed class Container<T> { }
                    public sealed class ComplexDocId
                    {
                        public ComplexDocId(Dictionary<string, Container<int?[]?>[]> values) { _ = values; }
                    }
                    public static class Consumer
                    {
                        public static object Create() => new ComplexDocId(new Dictionary<string, Container<int?[]?>[]>());
                    }
                    """)
            ], VirtualProjectDirectory: "src/App"));
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(null, ReadOnlySolutionSnapshot: scenario.Solution)));
        var compilation = await scenario.Solution.Projects.Single().GetCompilationAsync(CancellationToken.None);
        var complexType = compilation!.GetTypeByMetadataName("TestNs.ComplexDocId");
        Assert.NotNull(complexType);
        var constructor = Assert.Single(complexType!.InstanceConstructors.Where(symbol => symbol.Parameters.Length == 1));
        var validDocId = DocumentationCommentId.CreateDeclarationId(constructor);
        Assert.NotNull(validDocId);

        var valid = await FindReferencesTool.ExecuteAsync(
            state,
            new FindReferencesRequest(
                validDocId!,
                MaxResults: 50,
                Depth: 1),
            CancellationToken.None);
        var unknown = await FindReferencesTool.ExecuteAsync(
            state,
            new FindReferencesRequest("M:TestNs.Absent.#ctor(System.Int32)", MaxResults: 50, Depth: 1),
            CancellationToken.None);
        var validText = TextOf(valid);
        var unknownText = TextOf(unknown);

        Assert.False(valid.IsError is true, validText);
        Assert.Contains("Constructors.cs:11", validText, StringComparison.Ordinal);
        Assert.True(unknown.IsError is true || unknownText.Contains("SYMBOL_NOT_FOUND", StringComparison.Ordinal), unknownText);
    }

    [Fact]
    public async Task VerifyComplexConstructorHandoff_IsReusableWithoutChangingItsIdentifier()
    {
        using var scenario = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\VerifyComplexConstructorHandoff.slnx",
            new ProjectSpec("App", [
                ("Constructors.cs", """
                    #nullable enable
                    using System;
                    using System.Collections.Generic;
                    namespace TestNs;
                    public sealed class UnusedComplex
                    {
                        public UnusedComplex(Dictionary<string, List<int?[]?>?>? values, Func<string, int?>[]? callbacks)
                        {
                            _ = values;
                            _ = callbacks;
                        }
                    }
                    """)
            ], VirtualProjectDirectory: "src/App"));
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(
                null,
                Config: TestHelper.CreateDefaultConfig(),
                ReadOnlySolutionSnapshot: scenario.Solution)));

        var verify = await VerifyTool.ExecuteAsync(state, VerifyScope.Solution, CancellationToken.None);
        var verifyText = TextOf(verify);
        var candidate = verifyText.Split('\n').SingleOrDefault(line =>
            line.Contains("category=dead_code", StringComparison.Ordinal)
            && line.Contains("Constructors.cs:7", StringComparison.Ordinal));
        Assert.True(candidate is not null, verifyText);

        var handoffId = Regex.Match(candidate!, @"symbolIdentifier=(?<id>h:[A-Za-z0-9]+)", RegexOptions.CultureInvariant)
            .Groups["id"].Value;
        Assert.NotEmpty(handoffId);
        Assert.Contains("usage=unreferenced", candidate, StringComparison.Ordinal);

        await ConstructorHandoffLifecycleTests.AssertFollowUpToolsResolveConstructorAsync(
            state,
            handoffId,
            "UnusedComplex.UnusedComplex");
    }

    [Fact]
    public async Task GenericPrimaryConstructorHandoff_ResolvesAsConstructorAndNotAsTypeReference()
    {
        using var scenario = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\GenericPrimaryConstructorHandoff.slnx",
            new ProjectSpec("App", [
                ("Primary.cs", """
                    #nullable enable
                    namespace TestNs;
                    public sealed record GenericPrimary<T>(T?[] Values);
                    public static class Consumer
                    {
                        public static GenericPrimary<string> Create() => new(new string?[] { "value" });
                        public static System.Type MentionOnly() => typeof(GenericPrimary<string>);
                    }
                    """)
            ], VirtualProjectDirectory: "src/App"));
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(null, ReadOnlySolutionSnapshot: scenario.Solution)));

        var structure = await GetClassStructureTool.ExecuteAsync(
            state, "TestNs.GenericPrimary", "name", CancellationToken.None);
        var structureText = TextOf(structure);
        var row = structureText.Split('\n').SingleOrDefault(line =>
            line.StartsWith("| Constructor | .ctor |", StringComparison.Ordinal)
            && line.Contains("Values", StringComparison.Ordinal));
        Assert.True(row is not null, structureText);
        var handoffId = Regex.Match(row!, @"handoffId: `(?<id>h:[^`]+)`", RegexOptions.CultureInvariant)
            .Groups["id"].Value;
        Assert.NotEmpty(handoffId);

        var references = await FindReferencesTool.ExecuteAsync(
            state, new FindReferencesRequest(handoffId, MaxResults: 50, Depth: 1), CancellationToken.None);
        var body = await GetSymbolBodyTool.ExecuteAsync(state, [handoffId], 80, CancellationToken.None);
        var context = await GetFeatureContextTool.ExecuteAsync(
            state, new FeatureContextOptions(handoffId), CancellationToken.None);
        var referencesText = TextOf(references);

        Assert.False(references.IsError is true, referencesText);
        Assert.False(body.IsError is true, TextOf(body));
        Assert.False(context.IsError is true, TextOf(context));
        Assert.Contains("Primary.cs:", referencesText, StringComparison.Ordinal);
        Assert.DoesNotContain("MentionOnly", referencesText, StringComparison.Ordinal);
        Assert.Contains("GenericPrimary", TextOf(body), StringComparison.Ordinal);
        Assert.Contains("GenericPrimary", TextOf(context), StringComparison.Ordinal);
    }

    private static string TextOf(CallToolResult result) =>
        string.Join(Environment.NewLine, result.Content.OfType<TextContentBlock>().Select(block => block.Text));
}
