#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using AiNetLinter.Core;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Models;
using Microsoft.CodeAnalysis;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.SymbolGraph;

/// <summary>
/// Reine Mapping-Tests der change-context-Antwortmodelle: serialisiertes Payload traegt EXAKT die
/// vertraglichen JSON-Feldnamen (inkl. Verschachtelung), Accessibility als String, Violations ohne
/// Snippet/Source-Ausschnitt, deterministische Test-Kappung samt Vollstaendigkeitsmetadaten und
/// Cap-Normalisierung. Kein Git, kein Lint.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ChangeContextResponseModelTests
{
    private const string PlaceAsyncId = "M:App.OrderService.PlaceAsync";

    [Theory]
    [InlineData(150, 99, 100, 50)]
    [InlineData(0, 0, 20, 10)]
    [InlineData(-5, 7, 20, 7)]
    [InlineData(100, 50, 100, 50)]
    public void NormalizeCaps_ClampsToContractLimits(int inputSymbols, int inputTests, int expectedSymbols, int expectedTests)
    {
        var (maxChangedSymbols, maxTestsPerSymbol) = ChangeContextContract.NormalizeCaps(inputSymbols, inputTests);

        Assert.Equal(expectedSymbols, maxChangedSymbols);
        Assert.Equal(expectedTests, maxTestsPerSymbol);
    }

    [Fact]
    public void BuildEmptyPayload_SerializesAsCompleteEmptyContractStructure()
    {
        var json = Serialize(ChangeContextResponseMapper.BuildEmptyPayload());

        Assert.Equal("gitDiff", json.GetProperty("mode").GetString());
        Assert.Equal("change-context", json.GetProperty("detailLevel").GetString());
        foreach (var listName in (string[])[
                    "changedFiles", "changedSymbols", "callSites", "testAssociations",
                    "violations", "recommendedTestCommands"])
        {
            Assert.Empty(json.GetProperty(listName).EnumerateArray());
        }

        var completeness = json.GetProperty("completeness");
        Assert.Equal(0, completeness.GetProperty("changedSymbolsTotal").GetInt32());
        Assert.Equal(0, completeness.GetProperty("changedSymbolsShown").GetInt32());
        Assert.False(completeness.GetProperty("symbolsTruncated").GetBoolean());
        Assert.False(completeness.GetProperty("callSitesTruncated").GetBoolean());
        Assert.False(completeness.GetProperty("testsTruncated").GetBoolean());
    }

    [Fact]
    public void BuildPayload_SerializesWithExactContractFieldNames()
    {
        var payload = ChangeContextResponseMapper.BuildPayload(CreateInput(maxTestsPerSymbol: 1));
        var json = Serialize(payload);

        Assert.Equal(
            new[]
            {
                "mode", "detailLevel", "changedFiles", "changedSymbols", "callSites",
                "testAssociations", "violations", "recommendedTestCommands", "completeness"
            },
            json.EnumerateObject().Select(property => property.Name).ToArray());

        var changedFile = Assert.Single(json.GetProperty("changedFiles").EnumerateArray().ToArray());
        Assert.Equal("src/App/OrderService.cs", changedFile.GetProperty("filePath").GetString());
        var range = Assert.Single(changedFile.GetProperty("ranges").EnumerateArray().ToArray());
        Assert.Equal(40, range.GetProperty("startLine").GetInt32());
        Assert.Equal(8, range.GetProperty("lineCount").GetInt32());

        var symbol = Assert.Single(json.GetProperty("changedSymbols").EnumerateArray().ToArray());
        Assert.Equal(
            new[]
            {
                "documentationCommentId", "displayName", "kind", "accessibility",
                "projectName", "filePath", "startLine", "endLine"
            },
            symbol.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal(PlaceAsyncId, symbol.GetProperty("documentationCommentId").GetString());
        Assert.Equal("Public", symbol.GetProperty("accessibility").GetString());
        Assert.Equal(JsonValueKind.String, symbol.GetProperty("accessibility").ValueKind);

        var association = Assert.Single(json.GetProperty("testAssociations").EnumerateArray().ToArray());
        Assert.Equal(
            new[] { "symbolId", "filePath", "testMethods", "matchReason" },
            association.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.Equal(PlaceAsyncId, association.GetProperty("symbolId").GetString());

        var violation = Assert.Single(json.GetProperty("violations").EnumerateArray().ToArray());
        Assert.Equal(
            new[] { "filePath", "lineNumber", "ruleName", "severity", "details" },
            violation.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.DoesNotContain(violation.EnumerateObject(), property => property.Name is "snippet" or "guidance");
    }

    [Fact]
    public void BuildPayload_MapsAccessibilityAndSeverityAsStringsWithoutSnippet()
    {
        var payload = ChangeContextResponseMapper.BuildPayload(CreateInput(maxTestsPerSymbol: 10));

        Assert.Equal("Public", payload.ChangedSymbols.Single().Accessibility);
        var violation = Assert.Single(payload.Violations);
        Assert.Equal("error", violation.Severity);
        Assert.True(violation.Details.Length > 0);
    }

    [Fact]
    public void BuildPayload_ReflectsCompletenessMetadata()
    {
        // Gekapptes Szenario: total=2 vor Kappung, shown=1; Call-Site-Trunkierung wird
        // aus dem Traversal-Ergebnis gespiegelt. Der Test-Cap greift hier nicht (nur ein Treffer) —
        // der Methoden-Cap-Fall ist im dedizierten Kappungs-Test unten abgesichert.
        var capped = ChangeContextResponseMapper.BuildPayload(CreateInput(maxTestsPerSymbol: 1, callSitesTruncated: true));

        Assert.Equal(2, capped.Completeness.ChangedSymbolsTotal);
        Assert.Equal(1, capped.Completeness.ChangedSymbolsShown);
        Assert.True(capped.Completeness.SymbolsTruncated);
        Assert.True(capped.Completeness.CallSitesTruncated);
        Assert.False(capped.Completeness.TestsTruncated);

        var complete = ChangeContextResponseMapper.BuildPayload(
            CreateInput(maxTestsPerSymbol: 10, callSitesTruncated: false, changedSymbolsTotal: 1));
        Assert.False(complete.Completeness.SymbolsTruncated);
        Assert.False(complete.Completeness.CallSitesTruncated);
        Assert.False(complete.Completeness.TestsTruncated);
    }

    [Fact]
    public void BuildPayload_CapsTestMethodsPerSymbol_AndBuildsCommandsFromShownHitsOnly()
    {
        const string projectDir = @"C:\repo\tests";
        var files = new[]
        {
            new TestFileCoverageResult(
                "tests/OrderServiceInvocationTests.cs", "OrderServiceInvocationTests", "Unit",
                TestCoverageMatchReasons.DirectMemberMatch,
                ["Place_Hits_Direct_1", "Place_Hits_Direct_2", "Place_Hits_Direct_3"], 3, projectDir),
            new TestFileCoverageResult(
                "tests/OrderServiceTests.cs", "OrderServiceTests", "Unit",
                TestCoverageMatchReasons.NamingConventionMatch,
                ["Place_Names_1", "Place_Names_2"], 2, projectDir),
            new TestFileCoverageResult(
                "tests/OrderServiceCoversTests.cs", "OrderServiceCoversTests", "Unit",
                TestCoverageMatchReasons.ExplicitCoversComment,
                ["Place_Covers_1"], 1, projectDir),
        };
        var batch = new TestCoverageBatchScanResult([new TestCoverageBatchSymbolResult(PlaceAsyncId, 6, files)], 3, []);

        var payload = ChangeContextResponseMapper.BuildPayload(new ChangeContextResponseInput(
            CreateAnalysis(changedSymbolsTotal: 1), batch, [], MaxTestsPerSymbol: 4));

        // Cap 4: Datei 1 voll (3), Datei 2 teilweise (1), Datei 3 komplett weggekapppt.
        Assert.True(payload.Completeness.TestsTruncated);
        Assert.Equal(2, payload.TestAssociations.Count);
        Assert.Equal(3, payload.TestAssociations[0].TestMethods.Count);
        Assert.Equal(["Place_Names_1"], payload.TestAssociations[1].TestMethods);
        Assert.All(payload.TestAssociations, association => Assert.Equal(PlaceAsyncId, association.SymbolId));

        // Empfohlene Befehle stammen nur aus den NACH Cap gezeigten Treffern — die komplett
        // weggekappte Klasse taucht nirgends auf.
        var command = Assert.Single(payload.RecommendedTestCommands);
        Assert.Contains("FullyQualifiedName~OrderServiceInvocationTests", command, StringComparison.Ordinal);
        Assert.Contains("FullyQualifiedName~OrderServiceTests", command, StringComparison.Ordinal);
        Assert.DoesNotContain("OrderServiceCoversTests", command, StringComparison.Ordinal);
    }

    private static ChangeContextResponseInput CreateInput(
        int maxTestsPerSymbol, bool callSitesTruncated = false, int changedSymbolsTotal = 2)
    {
        var files = new[]
        {
            new TestFileCoverageResult(
                "tests/App.Tests/OrderServiceTests.cs", "OrderServiceTests", "Unit",
                TestCoverageMatchReasons.DirectMemberMatch,
                ["PlaceAsync_ValidOrder_Persists"], 1, @"C:\repo\tests\App.Tests"),
        };
        var batch = new TestCoverageBatchScanResult(
            [new TestCoverageBatchSymbolResult(PlaceAsyncId, 1, files)], 1, files.Select(f => f.FilePath).ToList());
        var violation = new RuleViolation
        {
            FilePath = @"C:\repo\src\App\OrderService.cs",
            LineNumber = 40,
            RuleName = "SomeUnknownRuleForSeverityDefaultXyz",
            Details = "details",
            Guidance = "guidance",
            EffectiveSeverity = "error",
        };
        return new ChangeContextResponseInput(
            CreateAnalysis(callSitesTruncated, changedSymbolsTotal), batch, [violation], maxTestsPerSymbol);
    }

    private static DiffImpactAnalysis CreateAnalysis(bool callSitesTruncated = false, int changedSymbolsTotal = 2) =>
        new(
            @"C:\repo",
            SinceRef: null,
            [new ChangedFileRange("src/App/OrderService.cs", [new HunkRange(40, 8)])],
            [new ChangedSymbolEntry(
                PlaceAsyncId, "OrderService.PlaceAsync", "Method", Accessibility.Public,
                "App", "src/App/OrderService.cs", 37, 61)],
            new ReferenceTraversalResult(
                [],
                new TraversalCompleteness(1, 1, 1, 1, 1, callSitesTruncated, false, false)),
            ChangedSymbolsTotal: changedSymbolsTotal,
            ShownSymbolHandles: []);

    private static JsonElement Serialize(ChangeContextPayload payload) =>
        JsonSerializer.SerializeToElement(payload, McpJsonOptions.Default);
}
