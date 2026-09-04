#nullable enable

using System;
using System.Linq;
using System.Text;
using System.Threading;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis.Dispatch;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.AssemblyAnalysis;

public sealed partial class AssemblyAnalysisToolTests
{
    [Fact]
    public void FinalWireTrim_RecalculatesFileTreeCountsAndContinuation()
    {
        var files = Enumerable.Range(0, 40)
            .Select(index => new { path = $"src/very-long-file-name-{index:D2}.cs", extension = ".cs", sizeBytes = 123L, lineCount = 4, depth = 1 })
            .ToArray();
        var result = McpToolResults.Text(
            "file tree",
            new
            {
                fileTree = new
                {
                    root = ".",
                    effectiveRoot = "src",
                    view = "files",
                    summary = new
                    {
                        scannedFileCount = 40,
                        matchedFileCount = 40,
                        scannedDirectoryCount = 2,
                        matchedDirectoryCount = 1,
                        matchedBytes = 4920L,
                        byExtension = new[] { new { extension = ".cs", count = 40, bytes = 4920L } },
                    },
                    directories = new[] { new { path = "src", depth = 0, matchedFileCount = 40, matchedBytes = 4920L, childDirectoryCount = 0 } },
                    files,
                    completeness = new
                    {
                        scanCompleted = true,
                        truncated = false,
                        truncatedBy = Array.Empty<string>(),
                        shownFileCount = 40,
                        inaccessibleSubtreeCount = 0,
                        skippedExcludedDirectoryCount = 0,
                        skippedReparsePointCount = 0,
                        warnings = Array.Empty<string>(),
                    },
                },
            });

        var projected = AssemblyAnalysisResponse.ApplyWireBudget(result, AssemblyAnalysisResponseLimits.MinimumResponseBytes, 0);
        var tree = projected.StructuredContent!.Value.GetProperty("fileTree");
        var returned = tree.GetProperty("files").GetArrayLength();

        Assert.True(returned < 40);
        Assert.Equal(returned, tree.GetProperty("returnedCount").GetInt32());
        Assert.Equal(returned, tree.GetProperty("completeness").GetProperty("shownFileCount").GetInt32());
        Assert.True(tree.GetProperty("completeness").GetProperty("truncated").GetBoolean());
        Assert.Contains("responseBudget", tree.GetProperty("completeness").GetProperty("truncatedBy").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(returned.ToString(), tree.GetProperty("continuationToken").GetString());
    }

    [Fact]
    public void FinalWireTrim_PreservesUnknownArraysWithTruncationEnvelopes()
    {
        var result = McpToolResults.Text(
            "assembly member",
            new
            {
                parameters = Enumerable.Range(0, 100).Select(index => $"parameter-{index:D3}").ToArray(),
                attributes = Enumerable.Range(0, 100).Select(index => $"attribute-{index:D3}").ToArray(),
                genericParameters = Enumerable.Range(0, 100).Select(index => $"T{index:D3}").ToArray(),
                constraints = Enumerable.Range(0, 100).Select(index => $"T{index:D3}:class").ToArray(),
            });

        var projected = AssemblyAnalysisResponse.ApplyWireBudget(
            result,
            AssemblyAnalysisResponseLimits.MinimumResponseBytes,
            0);
        var payload = projected.StructuredContent!.Value;
        foreach (var collectionName in new[] { "parameters", "attributes", "genericParameters", "constraints" })
        {
            var collection = payload.GetProperty(collectionName);
            var envelope = payload.GetProperty($"{collectionName}Envelope");
            var returned = collection.GetArrayLength();

            Assert.True(returned < 100);
            Assert.Equal(100, envelope.GetProperty("totalCount").GetInt32());
            Assert.Equal(returned, envelope.GetProperty("returnedCount").GetInt32());
            Assert.True(envelope.GetProperty("isTruncated").GetBoolean());
            Assert.Contains("responseBudget", envelope.GetProperty("truncatedBy").EnumerateArray().Select(item => item.GetString()));
            Assert.Equal(returned.ToString(), envelope.GetProperty("continuationToken").GetString());
            Assert.Contains("maxResponseBytes", envelope.GetProperty("detailHint").GetString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void FinalWireTrim_RecalculatesFileTreeDirectoryEnvelope()
    {
        var directories = Enumerable.Range(0, 40)
            .Select(index => new
            {
                path = $"src/very-long-directory-name-{index:D2}",
                depth = 1,
                matchedFileCount = 1,
                matchedBytes = 123L,
                childDirectoryCount = 0,
            })
            .ToArray();
        var result = McpToolResults.Text(
            "file tree",
            new
            {
                fileTree = new
                {
                    root = ".",
                    effectiveRoot = "src",
                    view = "summary",
                    summary = new
                    {
                        scannedFileCount = 40,
                        matchedFileCount = 40,
                        scannedDirectoryCount = 40,
                        matchedDirectoryCount = 40,
                        matchedBytes = 4920L,
                        byExtension = Array.Empty<object>(),
                    },
                    directories,
                    files = Array.Empty<object>(),
                    completeness = new
                    {
                        scanCompleted = true,
                        truncated = false,
                        truncatedBy = Array.Empty<string>(),
                        shownFileCount = 0,
                        inaccessibleSubtreeCount = 0,
                        skippedExcludedDirectoryCount = 0,
                        skippedReparsePointCount = 0,
                        warnings = Array.Empty<string>(),
                    },
                },
            });

        var projected = AssemblyAnalysisResponse.ApplyWireBudget(
            result,
            AssemblyAnalysisResponseLimits.MinimumResponseBytes,
            0);
        var tree = projected.StructuredContent!.Value.GetProperty("fileTree");
        var completeness = tree.GetProperty("completeness");
        var returned = tree.GetProperty("directories").GetArrayLength();

        Assert.True(returned < 40);
        Assert.Equal(40, tree.GetProperty("totalDirectoryCount").GetInt32());
        Assert.Equal(returned, tree.GetProperty("returnedDirectoryCount").GetInt32());
        Assert.True(tree.GetProperty("directoriesTruncated").GetBoolean());
        Assert.Contains("responseBudget", tree.GetProperty("directoriesTruncatedBy").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(returned.ToString(), tree.GetProperty("directoriesContinuationToken").GetString());
        Assert.Contains("maxResponseBytes", tree.GetProperty("directoriesDetailHint").GetString(), StringComparison.Ordinal);
        Assert.Equal(40, completeness.GetProperty("totalDirectoryCount").GetInt32());
        Assert.Equal(returned, completeness.GetProperty("shownDirectoryCount").GetInt32());
        Assert.True(completeness.GetProperty("directoryTruncated").GetBoolean());
        Assert.True(completeness.GetProperty("truncated").GetBoolean());
        Assert.Contains("responseBudget", completeness.GetProperty("truncatedBy").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(returned.ToString(), completeness.GetProperty("directoryContinuationToken").GetString());
        Assert.Contains("maxResponseBytes", completeness.GetProperty("directoryDetailHint").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void FinalWireTrim_RecalculatesCallSitesAndBodyResults()
    {
        var callSites = Enumerable.Range(0, 24)
            .Select(index => new { filePath = $"src/Caller{index:D2}.cs", line = index + 1, symbolName = "Probe.Run", projectName = "Probe", depth = 1, reachedFromSymbolId = "M:Probe.Run" })
            .ToArray();
        var bodyResults = Enumerable.Range(0, 12)
            .Select(index => new { requestedIdentifier = $"Probe.Run{index:D2}", id = $"M:Probe.Run{index:D2}", filePath = $"src/Body{index:D2}.cs", startLine = 1, body = new string('x', 400), bodyAvailability = "available", contentMode = "source", isTruncated = false })
            .ToArray();
        var result = McpToolResults.Text(
            "composite",
            new
            {
                callers = new
                {
                    callSites,
                    completeness = new
                    {
                        requestedDepth = 1,
                        effectiveDepth = 1,
                        visitedNodeCount = 24,
                        totalCallSiteCount = 24,
                        shownCallSiteCount = 24,
                        truncatedByMaxResults = false,
                        truncatedByNodeLimit = false,
                        depthWasClamped = false,
                    },
                },
                body = new { results = bodyResults, requestedCount = 12 },
            });

        var projected = AssemblyAnalysisResponse.ApplyWireBudget(result, AssemblyAnalysisResponseLimits.MinimumResponseBytes, 0);
        var payload = projected.StructuredContent!.Value;
        var callers = payload.GetProperty("callers");
        var shownCallSites = callers.GetProperty("callSites").GetArrayLength();
        var body = payload.GetProperty("body");
        var shownBodies = body.GetProperty("results").GetArrayLength();

        Assert.True(shownCallSites < 24 || shownBodies < 12);
        Assert.Equal(shownCallSites, callers.GetProperty("completeness").GetProperty("shownCallSiteCount").GetInt32());
        Assert.Equal(shownCallSites < 24, callers.GetProperty("completeness").GetProperty("truncated").GetBoolean());
        Assert.Equal(shownBodies, body.GetProperty("returnedCount").GetInt32());
        Assert.Equal(shownBodies < 12, body.GetProperty("isTruncated").GetBoolean());
        if (shownBodies < 12) Assert.Equal(shownBodies.ToString(), body.GetProperty("continuationToken").GetString());
    }

    [Fact]
    public void FinalWireTrim_MergesOuterAndInnerCompositeTruncation()
    {
        var result = McpToolResults.Text(
            "composite",
            new
            {
                isTruncated = true,
                truncatedBy = new[] { "responseBudget" },
                assemblyAnalysis = new
                {
                    totalCount = 1,
                    returnedCount = 1,
                    isTruncated = false,
                    truncatedBy = Array.Empty<string>(),
                    types = new[] { new { id = "T:Probe.Run", name = "Run", signature = new string('x', 8000) } },
                },
            });

        var projected = AssemblyAnalysisResponse.ApplyWireBudget(result, AssemblyAnalysisResponseLimits.MinimumResponseBytes, 0);
        var payload = projected.StructuredContent!.Value;

        Assert.True(payload.GetProperty("isTruncated").GetBoolean());
        Assert.Contains("responseBudget", payload.GetProperty("truncatedBy").EnumerateArray().Select(item => item.GetString()));
        Assert.True(payload.GetProperty("wireBudget").GetProperty("totalBytes").GetInt32() <= AssemblyAnalysisResponseLimits.MinimumResponseBytes);
    }

    [Fact]
    public void FinalWireTrim_RejectsUnrepresentableMinimumBudget()
    {
        var result = AssemblyAnalysisResponse.ApplyWireBudget(
            McpToolResults.Text("too small", new { value = "payload" }),
            1,
            0);

        Assert.False(result.IsError);
        Assert.Contains(AssemblyAnalysisResponseLimits.MinimumResponseBytes.ToString(), AssemblyAnalysisTestSupport.TextOf(result), StringComparison.Ordinal);
        Assert.Equal("INVALID_ARGUMENT", result.StructuredContent!.Value.GetProperty("code").GetString());
    }

    [Fact]
    public async Task InspectAssembly_GlobalResponseBudgetUsesOneTypedSelectionForTextAndJson()
    {
        using var temp = TestTempDirectory.Create("assembly-analysis-response-budget-");
        var types = Enumerable.Range(0, 180)
            .Select(index => $"public sealed class Type{index:D3} {{ public string Value{index:D3} => \"value\"; public void Reset{index:D3}(string input) {{ }} }}");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "ResponseBudgetProbe",
            $"namespace Probe.Budget; {string.Join(Environment.NewLine, types)}");

        var result = await InspectAssemblyToolDispatch.ExecuteAsync(
            null,
            new InspectAssemblyArguments(assemblyPath, null, null, null, true, 1000, MaxMembers: 1000),
            CancellationToken.None);

        var payload = AssemblyAnalysisTestSupport.Deserialize<InspectAssemblyPayload>(result);
        var text = AssemblyAnalysisTestSupport.TextOf(result);
        var structuredBytes = Encoding.UTF8.GetByteCount(result.StructuredContent!.Value.GetRawText());

        Assert.True(payload.TotalTypes > payload.ShownCount || payload.Types.Any(type => type.MembersTruncated));
        Assert.True(payload.Truncated);
        Assert.Contains("responseBudget", payload.TruncatedBy);
        Assert.Equal(payload.Types.Count, payload.ShownCount);
        Assert.True(structuredBytes <= AssemblyAnalysisResponseLimits.MaxResponseBytes);
        Assert.True(Encoding.UTF8.GetByteCount(text) <= AssemblyAnalysisResponseLimits.MaxResponseBytes);
        Assert.Contains(payload.Types, type => type.Members.Count > 0);
        Assert.All(
            payload.Types.SelectMany(type => type.Members),
            member =>
            {
                Assert.NotNull(member.Name);
                Assert.NotNull(member.Signature);
                Assert.NotNull(member.Parameters);
                Assert.NotNull(member.GenericParameters);
                Assert.NotNull(member.Constraints);
                Assert.Contains(member.Signature, text, StringComparison.Ordinal);
            });
        Assert.All(
            payload.Types,
            type => Assert.Contains($"`{type.Namespace}.{type.Name}`", text, StringComparison.Ordinal));
    }

    [Fact]
    public async Task InspectAssembly_CompactsLargeNamespaceListAndKeepsTypesWithinBudget()
    {
        using var temp = TestTempDirectory.Create("assembly-analysis-namespace-budget-");
        var namespaces = Enumerable.Range(0, 32)
            .Select(index =>
                $"namespace Probe.Budget.Namespace{index:D2} {{ public sealed class Type{index:D2} {{ public string Value => \"value\"; }} }}");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "NamespaceResponseBudgetProbe",
            string.Join(Environment.NewLine, namespaces));

        var result = await InspectAssemblyToolDispatch.ExecuteAsync(
            null,
            new InspectAssemblyArguments(
                assemblyPath,
                null,
                null,
                null,
                true,
                1000,
                MaxMembers: 1000,
                IncludeReferences: false),
            CancellationToken.None);

        var payload = AssemblyAnalysisTestSupport.Deserialize<InspectAssemblyPayload>(result);
        var text = AssemblyAnalysisTestSupport.TextOf(result);
        var summary = "Top 10 Namespaces und 22 weitere";

        Assert.Equal(32, payload.TotalNamespaces);
        Assert.Equal(11, payload.Namespaces.Count);
        Assert.Equal(summary, payload.Namespaces[^1]);
        Assert.Contains(summary, text, StringComparison.Ordinal);
        Assert.True(payload.Types.Count > 10);
        Assert.Contains(payload.Types, type => type.Members.Count > 0);
        Assert.True(Encoding.UTF8.GetByteCount(text) <= AssemblyAnalysisResponseLimits.MaxResponseBytes);
        Assert.True(Encoding.UTF8.GetByteCount(result.StructuredContent!.Value.GetRawText()) <= AssemblyAnalysisResponseLimits.MaxResponseBytes);
    }

    [Fact]
    public async Task FindAssemblyExtensions_GlobalResponseBudgetKeepsCountsAndSharedSelection()
    {
        using var temp = TestTempDirectory.Create("assembly-analysis-extension-budget-");
        var extensions = Enumerable.Range(0, 180)
            .Select(index => $"public static string Extend{index:D3}(this object value, string input) => input;");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "ExtensionResponseBudgetProbe",
            $"namespace Probe.Budget; public static class Extensions {{ {string.Join(Environment.NewLine, extensions)} }}");

        var result = await FindAssemblyExtensionsToolDispatch.ExecuteAsync(
            null,
            new FindAssemblyExtensionsArguments(assemblyPath, null, null, null, 1000),
            CancellationToken.None);

        var payload = AssemblyAnalysisTestSupport.Deserialize<FindAssemblyExtensionsPayload>(result);
        var text = AssemblyAnalysisTestSupport.TextOf(result);
        var structuredBytes = Encoding.UTF8.GetByteCount(result.StructuredContent!.Value.GetRawText());

        Assert.True(payload.TotalExtensions > payload.ShownCount);
        Assert.True(payload.Truncated);
        Assert.Equal(payload.Extensions.Count, payload.ShownCount);
        Assert.Contains("responseBudget", payload.TruncatedBy);
        Assert.True(structuredBytes <= AssemblyAnalysisResponseLimits.MaxResponseBytes);
        Assert.True(Encoding.UTF8.GetByteCount(text) <= AssemblyAnalysisResponseLimits.MaxResponseBytes);
        Assert.All(
            payload.Extensions,
            extension =>
            {
                Assert.NotNull(extension.Name);
                Assert.NotNull(extension.Signature);
                Assert.NotNull(extension.Parameters);
                Assert.NotNull(extension.GenericParameters);
                Assert.NotNull(extension.Constraints);
                Assert.Contains(extension.Signature, text, StringComparison.Ordinal);
            });
    }

    [Fact]
    public async Task InspectAssembly_GlobalResponseBudgetRemovesOversizedSingletonMember()
    {
        using var temp = TestTempDirectory.Create("assembly-analysis-singleton-budget-");
        var parameters = Enumerable.Range(0, 500)
            .Select(index => $"string parameter{index:D3}");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "SingletonResponseBudgetProbe",
            $"namespace Probe.Budget; public sealed class Oversized {{ public void Consume({string.Join(", ", parameters)}) {{ }} }}");

        var result = await InspectAssemblyToolDispatch.ExecuteAsync(
            null,
            new InspectAssemblyArguments(
                assemblyPath,
                "Probe.Budget",
                "Oversized",
                null,
                true,
                1000,
                ExactTypeName: true,
                MemberNames: ["Consume"],
                MaxMembers: 1000),
            CancellationToken.None);

        var payload = AssemblyAnalysisTestSupport.Deserialize<InspectAssemblyPayload>(result);
        var type = Assert.Single(payload.Types);
        var text = AssemblyAnalysisTestSupport.TextOf(result);
        var structuredBytes = Encoding.UTF8.GetByteCount(result.StructuredContent!.Value.GetRawText());

        Assert.Equal(1, payload.TotalTypes);
        Assert.Equal(1, payload.ShownCount);
        Assert.True(payload.Truncated);
        Assert.Contains("responseBudget", payload.TruncatedBy);
        Assert.Equal(1, type.TotalMembers);
        Assert.Empty(type.Members);
        Assert.True(type.MembersTruncated);
        Assert.Contains("responseBudget", type.TruncatedBy!);
        Assert.True(Encoding.UTF8.GetByteCount(text) <= AssemblyAnalysisResponseLimits.MaxResponseBytes);
        Assert.True(structuredBytes <= AssemblyAnalysisResponseLimits.MaxResponseBytes);
        Assert.DoesNotContain("API-Typen: 1 von 1 (gekürzt", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InspectAssembly_BudgetTrimAdvancesContinuationByReturnedItems()
    {
        using var temp = TestTempDirectory.Create("assembly-analysis-paging-budget-");
        var types = Enumerable.Range(0, 120)
            .Select(index => $"public sealed class Page{index:D3} {{ public void Run{index:D3}() {{ }} }}");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "BudgetPagingProbe",
            $"namespace Probe.Budget; {string.Join(Environment.NewLine, types)}");

        var arguments = new InspectAssemblyArguments(
            assemblyPath,
            null,
            null,
            null,
            true,
            1000,
            MaxMembers: 1000,
            MaxResponseBytes: 8192);
        var first = AssemblyAnalysisTestSupport.Deserialize<InspectAssemblyPayload>(
            await InspectAssemblyToolDispatch.ExecuteAsync(null, arguments, CancellationToken.None));

        Assert.True(first.ReturnedCount > 0);
        Assert.True(first.IsTruncated);
        Assert.Equal(first.ReturnedCount.ToString(), first.ContinuationToken);

        var second = AssemblyAnalysisTestSupport.Deserialize<InspectAssemblyPayload>(
            await InspectAssemblyToolDispatch.ExecuteAsync(
                null,
                arguments with { Cursor = first.ContinuationToken },
                CancellationToken.None));

        Assert.NotEmpty(second.Types);
        Assert.DoesNotContain(
            second.Types.Select(type => type.Id),
            id => first.Types.Any(type => type.Id == id));
    }

    [Fact]
    public void ApplyWireBudget_PreservesReadableTextInsteadOfDeletingIt()
    {
        var text = "# Klasse MyType\n| Kind | Name | Lines |\n| Method | DoWork | 1-10 |\n";
        var result = McpToolResults.Text(
            text,
            new
            {
                types = Enumerable.Range(0, 50).Select(index => new { id = $"T{index}", name = $"Type{index}" }).ToArray(),
                members = Enumerable.Range(0, 50).Select(index => new { id = $"M{index}", name = $"Member{index}" }).ToArray(),
            });

        var projected = AssemblyAnalysisResponse.ApplyWireBudget(result, 4096, 0);
        var projectedText = AssemblyAnalysisTestSupport.TextOf(projected);

        Assert.Contains("# Klasse MyType", projectedText, StringComparison.Ordinal);
        Assert.DoesNotContain("StructuredContent ist die kanonische Nutzlast", projectedText, StringComparison.Ordinal);
    }

    [Fact]
    public void TrimUtf8_HandlesMultiByteAndEdgeCasesWithoutException()
    {
        // Multi-byte string (100 'ä' = 200 Bytes in UTF-8, aber nur 100 Zeichen lang)
        var umlautText = new string('ä', 100);
        // maxBytes = 150: früher Absturz mit ArgumentOutOfRangeException wegen limit = 147 > value.Length (100)
        var trimmedUmlaut = AssemblyAnalysisResponse.TrimUtf8(umlautText, 150);
        Assert.EndsWith("…", trimmedUmlaut, StringComparison.Ordinal);
        Assert.True(Encoding.UTF8.GetByteCount(trimmedUmlaut) <= 150);

        // Sehr kleine Budgets
        Assert.Equal(string.Empty, AssemblyAnalysisResponse.TrimUtf8("Hallo", 0));
        Assert.Equal(".", AssemblyAnalysisResponse.TrimUtf8("Hallo", 1));
        Assert.Equal(".", AssemblyAnalysisResponse.TrimUtf8("Hallo", 2));
        Assert.Equal("…", AssemblyAnalysisResponse.TrimUtf8("Hallo", 3));

        // Surrogate pair
        var emojiText = "A\U0001F600B"; // 4 Bytes fuer Emoji
        var trimmedEmoji = AssemblyAnalysisResponse.TrimUtf8(emojiText, 6);
        Assert.True(Encoding.UTF8.GetByteCount(trimmedEmoji) <= 6);
    }
}

