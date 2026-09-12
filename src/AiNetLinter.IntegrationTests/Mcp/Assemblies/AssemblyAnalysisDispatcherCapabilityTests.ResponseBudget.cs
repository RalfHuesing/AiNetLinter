#nullable enable

using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.TestKit;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Assemblies;

public sealed partial class AssemblyAnalysisDispatcherCapabilityTests
{
    [Fact]
    public async Task InspectAssembly_ResponseBudgetContractUsesRequestedVisibilityAndWholeUnitRetries()
    {
        using var temp = TestTempDirectory.Create("assembly-inspect-budget-contract-");
        await using var fixture = await SyntheticAssemblyFixture.CreateAsync(temp, [], sourceCode: BudgetContractSource());

        await AssertResponseBudgetContractAsync(
            budget => fixture.ExecuteInspectAsync(new InspectionExecutionOptions(
                MaxResponseBytes: budget,
                PublicOnly: false,
                ApplyPostNavigationResponseBudget: true)),
            "types",
            inspectUsesAllVisibilities: true);
    }

    [Fact]
    public async Task FindAssemblyExtensions_ResponseBudgetContractUsesWholeUnitRetries()
    {
        using var temp = TestTempDirectory.Create("assembly-extensions-budget-contract-");
        await using var fixture = await SyntheticAssemblyFixture.CreateAsync(temp, [], sourceCode: BudgetContractSource());

        await AssertResponseBudgetContractAsync(
            budget => fixture.ExecuteExtensionsAsync(new ExtensionExecutionOptions(
                IncludeReferences: false,
                MaxResponseBytes: budget,
                ApplyPostNavigationResponseBudget: true)),
            "extensions",
            inspectUsesAllVisibilities: false);
    }

    [Fact]
    public async Task AssemblyRoute_MaxResponseBytesBelowMinimumReturnsRecoverableArgument()
    {
        using var temp = TestTempDirectory.Create("assembly-dispatcher-minimum-budget-");
        await using var fixture = await SyntheticAssemblyFixture.CreateAsync(temp, []);

        var result = await fixture.ExecuteInspectAsync(new InspectionExecutionOptions(MaxResponseBytes: 1));
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;

        Assert.False(result.IsError);
        Assert.Contains("maxResponseBytes", text, StringComparison.Ordinal);
        Assert.Contains(AssemblyAnalysisResponseLimits.MinimumResponseBytes.ToString(), text, StringComparison.Ordinal);
        Assert.Equal("INVALID_ARGUMENT", result.StructuredContent!.Value.GetProperty("code").GetString());
    }

    [Fact]
    public async Task AssemblyRoute_BudgetsFinalEnrichedResponseThroughDispatcher()
    {
        using var temp = TestTempDirectory.Create("assembly-dispatcher-response-budget-");
        var types = Enumerable.Range(0, 180)
            .Select(index => $"public sealed class Type{index:D3} {{ public string Value{index:D3} => \"value\"; public void Reset{index:D3}(string input) {{ }} }}");
        await using var fixture = await SyntheticAssemblyFixture.CreateAsync(
            temp,
            [],
            sourceCode: $"namespace Probe.Budget; {string.Join(Environment.NewLine, types)}");

        var result = await fixture.ExecuteInspectAsync();
        var payload = Structured(result);
        Assert.True(payload.GetProperty("totalTypes").GetInt32() > payload.GetProperty("shownCount").GetInt32());
        Assert.True(payload.GetProperty("truncated").GetBoolean());
        Assert.Contains("responseBudget", payload.GetProperty("truncatedBy").EnumerateArray().Select(item => item.GetString()));
        AssertFinalCombinedBudget(result, AssemblyAnalysisResponseLimits.DefaultResponseBytes);
        Assert.Equal("decompiled", payload.GetProperty("analysis").GetProperty("origin").GetString());
        Assert.Equal(payload.GetProperty("types").GetArrayLength(), payload.GetProperty("shownCount").GetInt32());
    }


    [Fact]
    public async Task AssemblyRoute_FinalWireTrimRecalculatesCountsAndCursorAt4096Bytes()
    {
        using var temp = TestTempDirectory.Create("assembly-dispatcher-final-wire-budget-");
        var types = Enumerable.Range(0, 180)
            .Select(index => $"public sealed class Page{index:D3} {{ }}");
        await using var fixture = await SyntheticAssemblyFixture.CreateAsync(
            temp,
            [],
            sourceCode: $"namespace Probe.Budget; {string.Join(Environment.NewLine, types)}");

        var first = Structured(await fixture.ExecuteInspectAsync(new InspectionExecutionOptions(MaxResponseBytes: 4096)));
        var firstTypes = first.GetProperty("types").EnumerateArray().ToArray();
        var firstCount = first.GetProperty("returnedCount").GetInt32();
        var firstToken = first.GetProperty("continuationToken").GetString();

        Assert.NotEmpty(firstTypes);
        Assert.Equal(firstTypes.Length, firstCount);
        Assert.True(first.GetProperty("totalCount").GetInt32() > firstCount);
        Assert.StartsWith("v1.", firstToken, StringComparison.Ordinal);

        var second = Structured(await fixture.ExecuteInspectAsync(new InspectionExecutionOptions(
            MaxResponseBytes: 4096,
            Cursor: firstToken)));
        Assert.NotEmpty(second.GetProperty("types").EnumerateArray());
        Assert.DoesNotContain(
            second.GetProperty("types").EnumerateArray().Select(type => type.GetProperty("id").GetString()),
            id => firstTypes.Any(type => type.GetProperty("id").GetString() == id));
    }

    [Fact]
    public async Task AssemblyContext_PostNavigationBudgetDropsOnlyWholeOptionalSections()
    {
        using var temp = TestTempDirectory.Create("assembly-context-section-budget-");
        await using var fixture = await SyntheticAssemblyFixture.CreateAsync(
            temp,
            [],
            sourceCode: "namespace Probe; public static class Probe { public static int Run() => 42; }");

        var result = await fixture.ExecuteRootOnlyAsync(lease => AssemblyAnalysisContextTool.ExecuteAsync(
            lease,
            new AssemblyAnalysisContextArguments(
                "Probe.Run",
                IncludeMetrics: true,
                IncludeReferences: false,
                IncludeCallers: true,
                IncludeImpact: true,
                IncludeBody: true,
                IncludeClassStructure: true,
                MaxResults: 100,
                MaxBodyLines: 80,
                MaxCallers: 100,
                Depth: 1,
                TopN: 10,
                MaxResponseBytes: 4096,
                DetailLevel: null,
                Cursor: null),
            CancellationToken.None),
            maxResponseBytes: 4096);

        var payload = Structured(result);
        AssertFinalCombinedBudget(result, 4096);
        Assert.False(payload.TryGetProperty("body", out _), payload.GetRawText());
        var navigation = payload.GetProperty("navigation");
        Assert.Equal("truncated", navigation.GetProperty("status").GetProperty("completeness").GetString());
        Assert.Equal("request_detail", navigation.GetProperty("next").GetProperty("kind").GetString());
    }

    [Fact]
    public async Task AssemblyContext_ResponseBudgetTooSmallPreservesNavigationAndExactRetry()
    {
        using var temp = TestTempDirectory.Create("assembly-context-minimum-budget-");
        var methodName = "ResponseBudget" + new string('X', 768);
        await using var fixture = await SyntheticAssemblyFixture.CreateAsync(
            temp,
            [],
            sourceCode: $"namespace Probe; public static class Probe {{ public static int {methodName}() => 42; }}");

        Task<CallToolResult> ExecuteAsync(int maxResponseBytes) => fixture.ExecuteRootOnlyAsync(
            lease => AssemblyAnalysisContextTool.ExecuteAsync(
                lease,
                new AssemblyAnalysisContextArguments(
                    $"Probe.{methodName}",
                    IncludeMetrics: false,
                    IncludeReferences: false,
                    IncludeCallers: false,
                    IncludeImpact: false,
                    IncludeBody: false,
                    IncludeClassStructure: false,
                    MaxResults: 100,
                    MaxBodyLines: 80,
                    MaxCallers: 100,
                    Depth: 1,
                    TopN: 10,
                    MaxResponseBytes: maxResponseBytes,
                    DetailLevel: null,
                    Cursor: null),
                CancellationToken.None),
            maxResponseBytes: maxResponseBytes);

        var minimumResponseBytes = AssertBudgetFailureNavigation(
            await ExecuteAsync(AssemblyAnalysisResponseLimits.MinimumResponseBytes));

        var retry = await ExecuteAsync(minimumResponseBytes);
        Assert.False(retry.IsError == true, retry.ToString());
        AssertFinalCombinedBudget(retry, minimumResponseBytes);
    }

    [Fact]
    public async Task AssemblyContext_TopNLimitsCallerAndImpactSelection()
    {
        using var temp = TestTempDirectory.Create("assembly-context-topn-");
        await using var fixture = await SyntheticAssemblyFixture.CreateAsync(
            temp,
            [],
            sourceCode: "namespace Probe; public static class Probe { public static int Run() => 42; public static int First() => Run(); public static int Second() => Run(); }");

        var result = await fixture.ExecuteRootOnlyAsync(lease => AssemblyAnalysisContextTool.ExecuteAsync(
            lease,
            new AssemblyAnalysisContextArguments(
                "Probe.Run",
                IncludeMetrics: false,
                IncludeReferences: false,
                IncludeCallers: true,
                IncludeImpact: true,
                IncludeBody: false,
                IncludeClassStructure: false,
                MaxResults: 100,
                MaxBodyLines: 80,
                MaxCallers: 100,
                Depth: 1,
                TopN: 1,
                MaxResponseBytes: 16 * 1024,
                DetailLevel: null,
                Cursor: null),
            CancellationToken.None));

        var payload = Structured(result);
        Assert.Equal(1, payload.GetProperty("callers").GetProperty("callSites").GetArrayLength());
        Assert.True(payload.GetProperty("impact").GetProperty("completeness").GetProperty("shownCallSiteCount").GetInt32() <= 1);
    }

    [Fact]
    public async Task AssemblyContext_RendersSectionContentInText()
    {
        using var temp = TestTempDirectory.Create("assembly-context-text-");
        await using var fixture = await SyntheticAssemblyFixture.CreateAsync(
            temp,
            [],
            sourceCode: "namespace Probe; public static class Probe { public static int Run() => 42; }");

        var result = await fixture.ExecuteRootOnlyAsync(lease => AssemblyAnalysisContextTool.ExecuteAsync(
            lease,
            new AssemblyAnalysisContextArguments(
                "Probe.Run",
                IncludeMetrics: true,
                IncludeReferences: false,
                IncludeCallers: false,
                IncludeImpact: false,
                IncludeBody: true,
                IncludeClassStructure: false,
                MaxResults: 100,
                MaxBodyLines: 80,
                MaxCallers: 100,
                Depth: 1,
                TopN: 10,
                MaxResponseBytes: 0,
                DetailLevel: null,
                Cursor: null),
            CancellationToken.None));

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Abschnitt: metrics", text, StringComparison.Ordinal);
        Assert.Contains("Abschnitt: body", text, StringComparison.Ordinal);
        Assert.Contains("return 42;", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Abschnitt: truncatedBy", text, StringComparison.Ordinal);
    }

    private static async Task AssertResponseBudgetContractAsync(
        Func<int, Task<CallToolResult>> call,
        string itemProperty,
        bool inspectUsesAllVisibilities)
    {
        var defaultResult = await call(0);
        Assert.False(defaultResult.IsError == true, defaultResult.ToString());
        AssertFinalCombinedBudget(defaultResult, AssemblyAnalysisResponseLimits.DefaultResponseBytes);

        var belowMinimum = await call(512);
        Assert.False(belowMinimum.IsError == true, Structured(belowMinimum).GetRawText());
        Assert.Equal("INVALID_ARGUMENT", Structured(belowMinimum).GetProperty("code").GetString());

        var constrained = await call(AssemblyAnalysisResponseLimits.MinimumResponseBytes);
        var minimumResponseBytes = AssertBudgetFailureNavigation(constrained);
        Assert.True(minimumResponseBytes > AssemblyAnalysisResponseLimits.MinimumResponseBytes);

        var between = await call((AssemblyAnalysisResponseLimits.MinimumResponseBytes + minimumResponseBytes) / 2);
        Assert.True(between.IsError);
        Assert.Equal(LinterErrorCodes.ResponseBudgetTooSmall, Structured(between).GetProperty("code").GetString());

        var retry = await call(minimumResponseBytes);
        Assert.False(retry.IsError == true, retry.ToString());
        AssertFinalCombinedBudget(retry, minimumResponseBytes);

        var maximum = await call(AssemblyAnalysisResponseLimits.MaxResponseBytes);
        Assert.False(maximum.IsError == true, maximum.ToString());
        AssertFinalCombinedBudget(maximum, AssemblyAnalysisResponseLimits.MaxResponseBytes);

        var defaultUnits = Structured(defaultResult).GetProperty(itemProperty).EnumerateArray().Select(item => item.GetRawText()).ToArray();
        var retryUnits = Structured(retry).GetProperty(itemProperty).EnumerateArray().Select(item => item.GetRawText()).ToArray();
        var maximumUnits = Structured(maximum).GetProperty(itemProperty).EnumerateArray().Select(item => item.GetRawText()).ToArray();
        Assert.NotEmpty(defaultUnits);
        Assert.True(defaultUnits.Length <= maximumUnits.Length);
        Assert.Equal(defaultUnits, maximumUnits.Take(defaultUnits.Length));
        Assert.True(retryUnits.Length <= maximumUnits.Length);
        Assert.Equal(retryUnits, maximumUnits.Take(retryUnits.Length));

        if (!inspectUsesAllVisibilities) return;
        var defaultText = Assert.IsType<TextContentBlock>(Assert.Single(defaultResult.Content)).Text;
        Assert.Contains("API-Typen:", defaultText, StringComparison.Ordinal);
        Assert.DoesNotContain("Öffentliche API-Typen:", defaultText, StringComparison.Ordinal);
    }

    private static void AssertFinalCombinedBudget(CallToolResult result, int maxResponseBytes)
    {
        var payload = Structured(result);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        var totalBytes = Encoding.UTF8.GetByteCount(text) + JsonSerializer.SerializeToUtf8Bytes(payload, McpJsonOptions.Default).Length;

        Assert.True(totalBytes <= maxResponseBytes, $"{totalBytes} Bytes überschreiten {maxResponseBytes} Bytes.");
        Assert.True(payload.TryGetProperty("navigation", out _), payload.GetRawText());
    }

    private static int AssertBudgetFailureNavigation(CallToolResult result)
    {
        Assert.True(result.IsError);
        var payload = Structured(result);
        Assert.Equal(LinterErrorCodes.ResponseBudgetTooSmall, payload.GetProperty("code").GetString());
        var navigation = payload.GetProperty("navigation");
        Assert.Equal(2, navigation.GetProperty("contractVersion").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(navigation.GetProperty("target").GetProperty("targetPath").GetString()));
        var status = navigation.GetProperty("status");
        Assert.Equal("error", status.GetProperty("operation").GetString());
        Assert.Equal("not_applicable", status.GetProperty("completeness").GetString());
        Assert.Equal(LinterErrorCodes.ResponseBudgetTooSmall, status.GetProperty("code").GetString());
        return payload.GetProperty("minimumResponseBytes").GetInt32();
    }

    private static string BudgetContractSource()
    {
        var declarations = Enumerable.Range(0, 80)
            .Select(index =>
                $"public sealed class VisibleType{index:D2} {{ }} internal sealed class InternalType{index:D2} {{ }} " +
                $"public static class ExtensionHost{index:D2} {{ public static string Extend{index:D2}(this string value) => value; }}");
        return $"namespace Probe.Budget; {string.Join(Environment.NewLine, declarations)}";
    }
}
