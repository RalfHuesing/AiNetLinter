#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp;

/// <summary>
/// E2E-Beweis fuer den dritten Server-Zustand <see cref="ServerLoadState.Loading"/>:
/// Waehrend der MCP-Server die Solution im Hintergrund laedt, antworten Tool-Calls mit
/// <see cref="McpToolResults.Loading"/> (kein Fehler, nur transienter Wartezustand).
/// Nach Abschluss des Loads liefern dieselben Tools reguläre Antworten. Beide Pfade
/// sind ueber das Test-Subprozess-Protokoll (kein Mocking) abgesichert.
/// </summary>
[Trait("Category", "Component")]
public sealed class McpServerCommandLoadingStateTests
{
    [Fact]
    public void RunAsync_LoadFuncStillRunning_ToolReturnsLoadingInfo()
    {
        // Hintergrund-Load, der nie innerhalb des Testzeitfensters abschliesst, damit der
        // Tool-Aufruf mit Sicherheit in den Loading-Zustand trifft. Der Server bleibt
        // waehrend dieser Zeit erreichbar (Transport-Handshake sofort, Tools reagieren
        // mit Loading).
        var neverCompletes = new TaskCompletionSource<AiNetLinter.Baseline.SourceFileCatalog?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        using var server = new McpCodeGraphServer(new McpCodeGraphServerOptions
        {
            Catalog = null,
            Console = AiNetLinter.Output.LinterConsole.Instance,
            MaxLineCount = 700,
            Config = new AiNetLinter.Configuration.Config
            {
                Global = new AiNetLinter.Configuration.GlobalConfig(),
                Metrics = new AiNetLinter.Configuration.MetricsConfig(),
            },
            UsedDefaultConfig = false,
            LoadFunc = async token =>
            {
                await neverCompletes.Task.WaitAsync(token);
                return null;
            },
        });

        Assert.Equal(ServerLoadState.Loading, server.LoadState);

        var loadingResult = McpServerCommandLoadingStateHarness.CallFindSymbolDirect(server);

        Assert.NotNull(loadingResult);
        Assert.NotEqual(true, loadingResult!.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(loadingResult.Content!)).Text;
        Assert.Contains("Server laedt die Solution noch", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Dispose_CancelsAndAwaitsBackgroundLoad()
    {
        var loadStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var loadCanceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var server = new McpCodeGraphServer(new McpCodeGraphServerOptions
        {
            Catalog = null,
            Console = AiNetLinter.Output.LinterConsole.Instance,
            MaxLineCount = 700,
            Config = new AiNetLinter.Configuration.Config
            {
                Global = new AiNetLinter.Configuration.GlobalConfig(),
                Metrics = new AiNetLinter.Configuration.MetricsConfig(),
            },
            UsedDefaultConfig = false,
            LoadFunc = async token =>
            {
                loadStarted.TrySetResult(true);
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    loadCanceled.TrySetResult(true);
                    throw;
                }

                return null;
            },
        });

        Assert.True(loadStarted.Task.Wait(TimeSpan.FromSeconds(5)));

        server.Dispose();

        Assert.True(loadCanceled.Task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task RunAsync_LoadFuncCompletes_ServerLeavesLoadingState()
    {
        // Verzoegerter Load-Abschluss: beweist, dass der Server den Loading-Zustand
        // verlaesst, sobald das LoadFunc-Result vorliegt — auch wenn das Result selbst
        // null ist (kein Lade-Fehler, einfach keine Loesung). Der Wechsel von
        // Loading zu LoadFailed ist hier der erwartete Lifecycle-Pfad.
        var release = new TaskCompletionSource<AiNetLinter.Baseline.SourceFileCatalog?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        using var server = new McpCodeGraphServer(new McpCodeGraphServerOptions
        {
            Catalog = null,
            Console = AiNetLinter.Output.LinterConsole.Instance,
            MaxLineCount = 700,
            Config = new AiNetLinter.Configuration.Config
            {
                Global = new AiNetLinter.Configuration.GlobalConfig(),
                Metrics = new AiNetLinter.Configuration.MetricsConfig(),
            },
            UsedDefaultConfig = false,
            LoadFunc = _ => release.Task,
        });

        Assert.Equal(ServerLoadState.Loading, server.LoadState);

        // Tool-Aufruf im Loading-Zustand muss die Loading-Antwort liefern, nicht
        // SolutionNotLoaded — sonst waere der Zustand semantisch von "kein Catalog"
        // nicht unterscheidbar.
        var loading = McpServerCommandLoadingStateHarness.CallFindSymbolDirect(server);
        var loadingText = Assert.IsType<TextContentBlock>(Assert.Single(loading!.Content!)).Text;
        Assert.Contains("Server laedt", loadingText, StringComparison.Ordinal);

        // Load-Abschluss freigeben und deterministisch auf den Abschluss des Load-Task
        // warten (statt auf LoadState zu pollen) — der Timeout ist ein reines
        // Sicherheitsnetz gegen einen echten Hänger, nicht die Wartebedingung selbst.
        release.SetResult(null);

        var safetyTimeout = Task.Delay(TimeSpan.FromSeconds(20));
        var winner = await Task.WhenAny(server.LoadTask!, safetyTimeout);
        Assert.Same(server.LoadTask, winner);

        // Terminaler Zustand erreicht; konkret LoadFailed, weil der Load erfolgreich war
        // (kein Faulted) aber kein Catalog geliefert wurde.
        Assert.Equal(ServerLoadState.LoadFailed, server.LoadState);
        Assert.False(server.IsLoaded);
    }

    [Fact]
    public async Task LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately()
    {
        // Zeitfenster "Load bereits erfolgreich abgeschlossen, aber _catalog noch nicht
        // adoptiert": sobald der Load-Task erfolgreich war, muss LoadState Loaded melden,
        // ohne dass GetCurrentSolution() aufgerufen wurde — sonst sieht die
        // ainetlinter://overview-Resource unmittelbar nach Serverstart fälschlich LoadFailed.
        // TCS-Pattern (statt Task.FromResult) noetig, weil McpCodeGraphServer den LoadFunc
        // via Task.Run auf den Thread-Pool schedulet; Task.FromResult waere im Fenster
        // zwischen Konstruktor und Thread-Pool-Dispatch noch nicht IsCompletedSuccessfully.
        var release = new TaskCompletionSource<AiNetLinter.Baseline.SourceFileCatalog?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        using var server = new McpCodeGraphServer(new McpCodeGraphServerOptions
        {
            Catalog = null,
            Console = AiNetLinter.Output.LinterConsole.Instance,
            MaxLineCount = 700,
            Config = new AiNetLinter.Configuration.Config
            {
                Global = new AiNetLinter.Configuration.GlobalConfig(),
                Metrics = new AiNetLinter.Configuration.MetricsConfig(),
            },
            UsedDefaultConfig = false,
            LoadFunc = _ => release.Task,
        });

        Assert.Equal(ServerLoadState.Loading, server.LoadState);

        using var context = new McpInMemoryTestContext();
        release.SetResult(new SourceFileCatalog(context.Solution, hasLoadingErrors: false));

        var safetyTimeout = Task.Delay(TimeSpan.FromSeconds(20));
        var winner = await Task.WhenAny(server.LoadTask!, safetyTimeout);
        Assert.Same(server.LoadTask, winner);

        Assert.Equal(ServerLoadState.Loaded, server.LoadState);
    }
}

/// <summary>
/// Direkter Aufruf von <see cref="AiNetLinter.Mcp.Tools.FindSymbolTool"/> ohne den
/// MCP-Transport, damit der Loading-Zustand ohne das vollstaendige stdio-Setup geprueft
/// werden kann. Die Tool-Implementierung selbst ist die zu testende Einheit.
/// </summary>
internal static class McpServerCommandLoadingStateHarness
{
    public static CallToolResult? CallFindSymbolDirect(McpCodeGraphServer server)
    {
        // ainetlinter-disable BanBlockingTaskAccess — der Harness muss synchron bleiben,
        // damit der Test-State direkt nach dem Aufruf beobachtbar ist; das hier ist ein
        // expliziter Sync-Adapter, kein async-faehiger Kontext.
        return AiNetLinter.Mcp.Tools.SymbolGraph.FindSymbolTool.ExecuteAsync(
            server,
            namePattern: "Anything",
            kind: null,
            maxResults: 10,
            CancellationToken.None).GetAwaiter().GetResult();
    }
}
