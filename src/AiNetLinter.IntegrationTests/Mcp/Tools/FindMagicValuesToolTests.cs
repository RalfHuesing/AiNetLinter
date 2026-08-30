#nullable enable

using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.IntegrationTests.Platform;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Composition;
using AiNetLinter.Mcp.Tools.MagicValues;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Tools;

/// <summary>
/// Integration-Tests fuer <see cref="FindMagicValuesTool"/> — gegen die
/// <see cref="SymbolGraphCatalogFixture"/> (ReadOnly-MCP-Server auf der SymbolGraphMini-Fixture).
/// Prueft Loading-/NotLoaded-Pfade, Parameter-Validierung (recoverable), Clamping,
/// <c>StructuredContent</c>-Shape und Tool-Registrierung in <c>tools/list</c>.
/// </summary>
[Trait("Category", "Integration")]
public sealed class FindMagicValuesToolTests
{
    private readonly SymbolGraphCatalogFixture _fixture;

    public FindMagicValuesToolTests(SymbolGraphCatalogFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsIsErrorTrueWithSolutionNotLoadedCode()
    {
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));

        var result = await FindMagicValuesTool.ExecuteAsync(
            state, DefaultArgs(), CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_LoadingState_ReturnsIsErrorFalseWithLoadingHint()
    {
        // LoadFunc, der nie completed -> Server bleibt im Loading-Zustand.
        var neverCompletes = new TaskCompletionSource<SourceFileCatalog?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var options = McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null));
        // LoadFunc wird per with-Expression gesetzt, weil es im From()-Parameter nicht enthalten ist.
        var optionsWithLoad = new McpCodeGraphServerOptions
        {
            Catalog = options.Catalog,
            Console = options.Console,
            MaxLineCount = options.MaxLineCount,
            Config = options.Config,
            UsedDefaultConfig = options.UsedDefaultConfig,
            ResolvedConfigPath = options.ResolvedConfigPath,
            LoadFunc = async token =>
            {
                await neverCompletes.Task.WaitAsync(token);
                return null;
            },
        };
        using var state = new McpCodeGraphServer(optionsWithLoad);

        var result = await FindMagicValuesTool.ExecuteAsync(
            state, DefaultArgs(), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("laedt", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownValueType_ReturnsRecoverableInvalidArgument()
    {
        using var state = _fixture.CreateReadOnlyServer();

        var args = DefaultArgs() with { ValueType = "foo" };
        var result = await FindMagicValuesTool.ExecuteAsync(state, args, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("all, strings, numbers", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownCategoryFilter_ReturnsRecoverableInvalidArgument()
    {
        using var state = _fixture.CreateReadOnlyServer();

        var args = DefaultArgs() with { CategoryFilter = "foo" };
        var result = await FindMagicValuesTool.ExecuteAsync(state, args, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("config_candidates", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_MaxResultsClamped_NoCrashOnZeroOrNegative()
    {
        using var state = _fixture.CreateReadOnlyServer();

        var argsZero = DefaultArgs() with { MaxResults = 0 };
        var resultZero = await FindMagicValuesTool.ExecuteAsync(state, argsZero, CancellationToken.None);

        Assert.NotEqual(true, resultZero.IsError);
        Assert.NotNull(resultZero.StructuredContent);

        var argsNeg = DefaultArgs() with { MaxResults = -5 };
        var resultNeg = await FindMagicValuesTool.ExecuteAsync(state, argsNeg, CancellationToken.None);

        Assert.NotEqual(true, resultNeg.IsError);
        Assert.NotNull(resultNeg.StructuredContent);
    }

    [Fact]
    public async Task ExecuteAsync_MinOccurrencesClamped_NoCrashOnZero()
    {
        using var state = _fixture.CreateReadOnlyServer();

        var args = DefaultArgs() with { MinOccurrences = 0 };
        var result = await FindMagicValuesTool.ExecuteAsync(state, args, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
    }

    [Fact]
    public async Task ExecuteAsync_StructuredContentShape_IsJsonObjectNotArray()
    {
        using var state = _fixture.CreateReadOnlyServer();

        var result = await FindMagicValuesTool.ExecuteAsync(state, DefaultArgs(), CancellationToken.None);

        Assert.NotNull(result.StructuredContent);
        Assert.Equal(JsonValueKind.Object, result.StructuredContent!.Value.ValueKind);

        // Erwartete Felder: MagicValues-Array + Summary-Objekt.
        var payload = result.StructuredContent.Value;
        Assert.True(payload.TryGetProperty("magicValues", out var magicValues));
        Assert.Equal(JsonValueKind.Array, magicValues.ValueKind);
        Assert.True(payload.TryGetProperty("summary", out var summary));
        Assert.Equal(JsonValueKind.Object, summary.ValueKind);
    }

    [Fact]
    public async Task ExecuteAsync_ScopeFilterNoMatch_ReturnsIsErrorFalseWithoutStructuredContent()
    {
        using var state = _fixture.CreateReadOnlyServer();

        var args = DefaultArgs() with { ScopeFilter = "DoesNotExistAnywhere_zzz" };
        var result = await FindMagicValuesTool.ExecuteAsync(state, args, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.Null(result.StructuredContent);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Keine Dateien im Scope", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddFindMagicValues_ToolAppearsInRegistrationList()
    {
        await using var registry = ProjectRegistryFixture.CreateInspectionRegistry();
        var options = McpServerOptionsFactory.Create(
            McpServerToolCollectionFactory.Build(
                registry,
                AnalysisToolCall.CreateTargetRoute(
                    ProjectAnalysisDispatcher.CreateRoute(registry),
                    AssemblyAnalysisDispatcher.CreateRoute(null))),
            McpServerResourceCollectionFactory.Build(registry));

        var findMagicValues = options.ToolCollection!.SingleOrDefault(t =>
            string.Equals(t.ProtocolTool.Name, "find_magic_values", StringComparison.Ordinal));

        Assert.NotNull(findMagicValues);
        Assert.Contains("Magic", findMagicValues!.ProtocolTool.Description, StringComparison.Ordinal);
        Assert.Contains("scopeFilter", findMagicValues.ProtocolTool.InputSchema.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("\"args\"", findMagicValues.ProtocolTool.InputSchema.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_AllFiltersDefault_RunsWithoutError()
    {
        using var state = _fixture.CreateReadOnlyServer();

        // Default-Args fuehren einen vollstaendigen Audit-Lauf aus; SymbolGraphMini enthaelt keine
        // Magic Values, also wird das Ergebnis leer sein, aber das Tool muss strukturiert antworten.
        var result = await FindMagicValuesTool.ExecuteAsync(state, DefaultArgs(), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var payload = result.StructuredContent!.Value;
        Assert.True(payload.TryGetProperty("magicValues", out var magicValues));
        Assert.Equal(JsonValueKind.Array, magicValues.ValueKind);
    }

    private static FindMagicValuesToolArgs DefaultArgs() => new(
        ScopeFilter: null,
        ValueType: "all",
        CategoryFilter: "all",
        MinOccurrences: 1,
        MaxResults: FindMagicValuesScanner.DefaultMaxResults,
        IgnoreNumbers: null,
        IncludeTests: false,
        IncludeSuppressed: false,
        ChangedOnly: false);
}
