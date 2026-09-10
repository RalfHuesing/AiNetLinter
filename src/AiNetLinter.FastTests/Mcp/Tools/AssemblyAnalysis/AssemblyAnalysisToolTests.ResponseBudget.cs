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
    public void FinalWireTrim_OverridesExistingCompleteNavigation()
    {
        var result = McpToolResults.Text(
            "assembly",
            new
            {
                navigation = new { contractVersion = 1, status = new { operation = "ok", completeness = "complete" } },
                types = Enumerable.Range(0, 200)
                    .Select(index => new { id = $"item-{index:D3}", value = new string('x', 80) }),
            });

        var projected = AssemblyAnalysisResponse.ApplyWireBudget(
            result,
            4096,
            0);
        var payload = projected.StructuredContent!.Value;

        Assert.True(payload.GetProperty("wireTruncated").GetBoolean());
        Assert.Equal("truncated", payload.GetProperty("navigation").GetProperty("status").GetProperty("completeness").GetString());
    }

    [Fact]
    public void TextOnlyWireTrim_MarksEnvelopeAndNavigationAsTruncated()
    {
        var result = McpToolResults.Text(
            new string('x', 8_000),
            new
            {
                navigation = new { contractVersion = 1, status = new { operation = "ok", completeness = "complete" } },
            });

        var projected = AssemblyAnalysisResponse.ApplyWireBudget(result, 4096, 0);
        var payload = projected.StructuredContent!.Value;
        var wireBudget = payload.GetProperty("wireBudget");

        Assert.Contains("…", AssemblyAnalysisTestSupport.TextOf(projected), StringComparison.Ordinal);
        Assert.True(payload.GetProperty("wireTruncated").GetBoolean());
        Assert.True(wireBudget.GetProperty("truncated").GetBoolean());
        Assert.Contains("responseBudget", payload.GetProperty("truncatedBy").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal("truncated", payload.GetProperty("navigation").GetProperty("status").GetProperty("completeness").GetString());
        Assert.True(wireBudget.GetProperty("totalBytes").GetInt32() <= wireBudget.GetProperty("limitBytes").GetInt32());
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
}
