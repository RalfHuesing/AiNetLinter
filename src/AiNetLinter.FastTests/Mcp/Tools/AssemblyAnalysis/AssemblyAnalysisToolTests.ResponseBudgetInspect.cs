#nullable enable

using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis.Dispatch;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.AssemblyAnalysis;

public sealed partial class AssemblyAnalysisToolTests
{
    [Fact]
    public async Task InspectAssembly_GlobalResponseBudgetUsesVisibleTextSelection()
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

        var text = AssemblyAnalysisTestSupport.TextOf(result);
        Assert.Contains("Öffentliche API-Typen:", text, StringComparison.Ordinal);
        Assert.Contains("von 180 (gekürzt: responseBudget)", text, StringComparison.Ordinal);
        Assert.Contains("Fortsetzung: continuationToken:", text, StringComparison.Ordinal);
        Assert.True(Encoding.UTF8.GetByteCount(text) <= AssemblyAnalysisResponseLimits.MaxResponseBytes);
        Assert.Contains("`Probe.Budget.Type000`; handoffId: `a:", text, StringComparison.Ordinal);
        Assert.Contains("method: `void Probe.Budget.Type000.Reset000(string input)`", text, StringComparison.Ordinal);
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

        var text = AssemblyAnalysisTestSupport.TextOf(result);
        var summary = "Top 10 Namespaces und 22 weitere";

        Assert.Contains(summary, text, StringComparison.Ordinal);
        Assert.Contains("Öffentliche Namespaces: 32", text, StringComparison.Ordinal);
        Assert.Contains("`Probe.Budget.Namespace00.Type00`", text, StringComparison.Ordinal);
        Assert.True(Encoding.UTF8.GetByteCount(text) <= AssemblyAnalysisResponseLimits.MaxResponseBytes);
    }

    [Fact]
    public async Task FindAssemblyExtensions_GlobalResponseBudgetUsesVisibleTextSelection()
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

        var text = AssemblyAnalysisTestSupport.TextOf(result);
        Assert.Contains("Assembly-Extensions:", text, StringComparison.Ordinal);
        Assert.Contains("von 180 (gekürzt: responseBudget)", text, StringComparison.Ordinal);
        Assert.Contains("Fortsetzung: continuationToken:", text, StringComparison.Ordinal);
        Assert.True(Encoding.UTF8.GetByteCount(text) <= AssemblyAnalysisResponseLimits.MaxResponseBytes);
        Assert.Contains("`Probe.Budget.Extend000`", text, StringComparison.Ordinal);
        Assert.Contains("Signatur: `string Probe.Budget.Extensions.Extend000(object value, string input)`", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindAssemblyExtensions_PublishesHandoffIdOnceInContent()
    {
        using var temp = TestTempDirectory.Create("assembly-analysis-extension-handoff-id-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "ExtensionHandoffIdProbe",
            "namespace Probe; public static class Extensions { public static string Extend(this object value) => value.ToString(); }");

        var result = await FindAssemblyExtensionsToolDispatch.ExecuteAsync(
            null,
            new FindAssemblyExtensionsArguments(assemblyPath, null, null, null, 10),
            CancellationToken.None);

        var text = AssemblyAnalysisTestSupport.TextOf(result);
        const string expectedSignature = "string Probe.Extensions.Extend(object value)";

        Assert.Contains("`Probe.Extend` für `object` — not_decidable; handoffId: `a:", text, StringComparison.Ordinal);
        Assert.Contains($"Signatur: `{expectedSignature}`", text, StringComparison.Ordinal);
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

        var text = AssemblyAnalysisTestSupport.TextOf(result);

        Assert.Contains("Öffentliche API-Typen: 1 von 1", text, StringComparison.Ordinal);
        Assert.Contains("`Probe.Budget.Oversized`", text, StringComparison.Ordinal);
        Assert.Contains("method: `void Probe.Budget.Oversized.Consume(", text, StringComparison.Ordinal);
        Assert.DoesNotContain("responseBudget", text, StringComparison.Ordinal);
        Assert.True(Encoding.UTF8.GetByteCount(text) <= AssemblyAnalysisResponseLimits.MaxResponseBytes);
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
        var first = await InspectAssemblyToolDispatch.ExecuteAsync(null, arguments, CancellationToken.None);
        var firstText = AssemblyAnalysisTestSupport.TextOf(first);
        var firstToken = AssemblyAnalysisTestSupport.ContinuationTokenOf(first);

        Assert.Contains("von 120 (gekürzt: responseBudget)", firstText, StringComparison.Ordinal);
        Assert.StartsWith("v1.", firstToken, StringComparison.Ordinal);

        var second = await InspectAssemblyToolDispatch.ExecuteAsync(
                null,
                arguments with { Cursor = firstToken },
                CancellationToken.None);
        var secondText = AssemblyAnalysisTestSupport.TextOf(second);

        Assert.DoesNotContain("`Probe.Budget.Page000`", secondText, StringComparison.Ordinal);
    }
}
