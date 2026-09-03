#nullable enable

using System;
using System.Linq;
using System.Text;
using System.Threading;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis.Dispatch;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.AssemblyAnalysis;

public sealed partial class AssemblyAnalysisToolTests
{
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
}
