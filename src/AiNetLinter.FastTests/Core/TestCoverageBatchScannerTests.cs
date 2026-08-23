#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Core;
using AiNetLinter.Mcp.Tools.TestContext;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Core;

[Trait("Category", "Unit")]
public sealed class TestCoverageBatchScannerTests
{
    [Fact]
    public async Task FindTestsForSymbolsAsync_TwoTargets_MatchInOneScan_WithSeparatedEvidenceKinds()
    {
        using var scenario = ChangeContextScenarioFactory.CreateScenario();
        var symbols = await ChangeContextScenarioFactory.ResolveSymbolsAsync(scenario.Solution);
        var counters = new DiffImpactCounters();

        var batch = await TestCoverageScanner.FindTestsForSymbolsCoreAsync(
            [symbols.PlaceAsync, symbols.LogInternal], scenario.Solution, counters);

        Assert.Equal(1, counters.TestSolutionScans);
        Assert.Equal(3, batch.DistinctTestFileCount);

        var placeAsyncResult = batch.Symbols[0];
        Assert.Equal(2, placeAsyncResult.TotalMatchingTests);
        Assert.Equal(2, placeAsyncResult.TestFiles.Count);

        var directFile = placeAsyncResult.TestFiles[0];
        Assert.Equal(TestCoverageMatchReasons.DirectMemberMatch, directFile.MatchReason);
        Assert.Equal($"App.Tests/{ChangeContextScenarioFactory.InvocationTestsFileName}", directFile.FilePath);
        Assert.Equal(["PlacesOrder_ThroughService"], directFile.TestMethods);

        var namingFile = placeAsyncResult.TestFiles[1];
        Assert.Equal(TestCoverageMatchReasons.NamingConventionMatch, namingFile.MatchReason);
        Assert.Equal($"App.Tests/{ChangeContextScenarioFactory.OrderServiceTestsFileName}", namingFile.FilePath);
        Assert.Equal(["PlaceOrder_PersistsDraft"], namingFile.TestMethods);
    }

    [Fact]
    public async Task FindTestsForSymbolsAsync_PrivateMethodWithoutCallSites_GetsNamingConventionHit()
    {
        using var scenario = ChangeContextScenarioFactory.CreateScenario();
        var symbols = await ChangeContextScenarioFactory.ResolveSymbolsAsync(scenario.Solution);

        var batch = await TestCoverageScanner.FindTestsForSymbolsAsync(
            [symbols.LogInternal], scenario.Solution, CancellationToken.None);

        var logInternalResult = Assert.Single(batch.Symbols);
        Assert.NotEmpty(logInternalResult.SymbolId);

        var testFile = Assert.Single(logInternalResult.TestFiles);
        Assert.Equal(TestCoverageMatchReasons.NamingConventionMatch, testFile.MatchReason);
        Assert.Equal($"App.Tests/{ChangeContextScenarioFactory.AuditLoggerTestsFileName}", testFile.FilePath);
        Assert.Equal(ChangeContextScenarioFactory.TestClassNameForPrivateMethod, testFile.TestClassName);
        Assert.Equal(["WritesEntry_ForEveryMessage"], testFile.TestMethods);
    }

    [Fact]
    public async Task FindTestsForSymbolAsync_Wrapper_ReturnsFieldIdenticalResultAsBatchEntry()
    {
        using var scenario = ChangeContextScenarioFactory.CreateScenario();
        var symbols = await ChangeContextScenarioFactory.ResolveSymbolsAsync(scenario.Solution);

        var wrapped = await TestCoverageScanner.FindTestsForSymbolAsync(
            symbols.PlaceAsync, scenario.Solution, CancellationToken.None);
        var batched = await TestCoverageScanner.FindTestsForSymbolsAsync(
            [symbols.PlaceAsync], scenario.Solution, CancellationToken.None);

        var batchEntry = Assert.Single(batched.Symbols);
        Assert.Equal(wrapped.TotalMatchingTests, batchEntry.TotalMatchingTests);
        Assert.Equal(wrapped.TestFiles.Count, batchEntry.TestFiles.Count);

        for (var i = 0; i < wrapped.TestFiles.Count; i++)
        {
            var expected = wrapped.TestFiles[i];
            var actual = batchEntry.TestFiles[i];
            Assert.Equal(expected.FilePath, actual.FilePath);
            Assert.Equal(expected.TestClassName, actual.TestClassName);
            Assert.Equal(expected.Category, actual.Category);
            Assert.Equal(expected.MatchReason, actual.MatchReason);
            Assert.Equal(expected.TotalClassTests, actual.TotalClassTests);
            Assert.Equal(expected.ProjectDirectory, actual.ProjectDirectory);
            Assert.Equal(expected.TestMethods, actual.TestMethods);
        }
    }

    [Fact]
    public async Task BuildDotNetTestCommands_TwoHitClassesInSameProject_YieldOneDeduplicatedCommand()
    {
        using var scenario = ChangeContextScenarioFactory.CreateScenario();
        var symbols = await ChangeContextScenarioFactory.ResolveSymbolsAsync(scenario.Solution);

        var batch = await TestCoverageScanner.FindTestsForSymbolsAsync(
            [symbols.PlaceAsync, symbols.LogInternal], scenario.Solution, CancellationToken.None);
        var allHits = batch.Symbols.SelectMany(symbol => symbol.TestFiles).ToList();

        var commands = TestRecommendationBuilder.BuildDotNetTestCommands(allHits);
        var commandsAgain = TestRecommendationBuilder.BuildDotNetTestCommands(allHits);

        var command = Assert.Single(commands);
        Assert.Equal(
            "dotnet test App.Tests"
            + " --filter \"FullyQualifiedName~AuditLoggerTests"
            + "|FullyQualifiedName~OrderServiceInvocationTests"
            + "|FullyQualifiedName~OrderServiceTests\"",
            command);
        Assert.Equal(commands.ToArray(), commandsAgain.ToArray());
    }

    [Fact]
    public async Task FindTestsForSymbolsCoreAsync_EmptyTargetList_PerformsNoScan()
    {
        using var scenario = ChangeContextScenarioFactory.CreateScenario();
        var counters = new DiffImpactCounters();

        var batch = await TestCoverageScanner.FindTestsForSymbolsCoreAsync([], scenario.Solution, counters);

        Assert.Empty(batch.Symbols);
        Assert.Equal(0, batch.DistinctTestFileCount);
        Assert.Empty(batch.DistinctTestFilePaths);
        Assert.Equal(0, counters.TestSolutionScans);
    }
}
