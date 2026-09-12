#nullable enable

using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Composition;
using AiNetLinter.Mcp.Tools.Analysis;
using AiNetLinter.Mcp.Tools.MagicValues;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.FindMagicValues;

[Trait("Category", "Component")]
public sealed class FindMagicValuesToolContractTests
{
    [Fact]
    public async Task ExecuteAsync_LoadingState_ReturnsIsErrorFalseWithLoadingHint()
    {
        var pending = new TaskCompletionSource<SourceFileCatalog?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var defaults = McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null));
        using var state = new McpCodeGraphServer(new McpCodeGraphServerOptions
        {
            Catalog = defaults.Catalog,
            Console = defaults.Console,
            MaxLineCount = defaults.MaxLineCount,
            Config = defaults.Config,
            ResolvedConfigPath = defaults.ResolvedConfigPath,
            LoadFunc = async token => await pending.Task.WaitAsync(token),
        });

        var result = await FindMagicValuesTool.ExecuteAsync(state, DefaultArgs(), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.Contains("laedt", TextOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownFilters_ReturnRecoverableInvalidArgument()
    {
        using var context = new McpInMemoryTestContext();
        using var state = context.CreateServer();

        foreach (var args in new[]
        {
            DefaultArgs() with { ValueType = "foo" },
            DefaultArgs() with { CategoryFilter = "foo" },
        })
        {
            var result = await FindMagicValuesTool.ExecuteAsync(state, args, CancellationToken.None);
            Assert.NotEqual(true, result.IsError);
            Assert.Contains("INVALID_ARGUMENT", TextOf(result), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ClampAndDefaultArguments_ReturnStructuredPayload()
    {
        using var context = new McpInMemoryTestContext();
        using var state = context.CreateServer();

        foreach (var args in new[]
        {
            DefaultArgs(),
            DefaultArgs() with { MaxResults = 0 },
            DefaultArgs() with { MaxResults = -5 },
            DefaultArgs() with { MinOccurrences = 0 },
        })
        {
            var result = await FindMagicValuesTool.ExecuteAsync(state, args, CancellationToken.None);
            Assert.NotEqual(true, result.IsError);
            Assert.Equal(JsonValueKind.Object, PayloadOf(result).ValueKind);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ScopeFilterNoMatch_ReturnsStructuredNotDecidableWithoutError()
    {
        using var context = new McpInMemoryTestContext();
        using var state = context.CreateServer();

        var result = await FindMagicValuesTool.ExecuteAsync(
            state, DefaultArgs() with { ScopeFilter = "DoesNotExistAnywhere_zzz" }, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var payload = PayloadOf(result);
        Assert.Equal("not_decidable", payload.GetProperty("summary").GetProperty("status").GetString());
        Assert.Equal(0, payload.GetProperty("summary").GetProperty("filesInScope").GetInt32());
        Assert.Equal(7, payload.GetProperty("categories").GetArrayLength());
        Assert.Contains("Keine Dateien im Scope", TextOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public void Registration_AdvertisesMagicValuesSchemaWithoutSyntheticArgsWrapper()
    {
        var registry = ProjectRegistryFixture.CreateInspectionRegistry();
        var tools = McpServerToolCollectionFactory.Build(
            registry,
            AnalysisToolCall.CreateTargetRoute(
                ProjectAnalysisDispatcher.CreateRoute(registry),
                AssemblyAnalysisDispatcher.CreateRoute(null)));
        var tool = tools.Single(item => item.ProtocolTool.Name == "find_magic_values").ProtocolTool;

        Assert.Contains("Magic", tool.Description, StringComparison.Ordinal);
        Assert.Contains("scopeFilter", tool.InputSchema.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("\"args\"", tool.InputSchema.ToString(), StringComparison.Ordinal);
    }

    private static FindMagicValuesToolArgs DefaultArgs() => new(
        null, "all", "all", 1, FindMagicValuesScanner.DefaultMaxResults, null, false, false, false);

    private static JsonElement PayloadOf(CallToolResult result)
    {
        Assert.NotNull(result.StructuredContent);
        return result.StructuredContent!.Value;
    }

    private static string TextOf(CallToolResult result) =>
        Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
}
