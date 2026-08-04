#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Tests.Fixtures;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Tests.Mcp;

/// <summary>
/// Sauberer C#-Harness für E2E- und Integrationstests von MCP-Tools über <see cref="StdioClientTransport"/>.
/// Ersetzt ad-hoc Python-Dogfooding-Skripte und stellt typsichere Helper für xUnit-Tests bereit.
/// </summary>
public sealed class McpTestClient : IAsyncDisposable
{
    /// <summary>
    /// Praefix des Loading-Info-Texts, den der MCP-Server waehrend des Hintergrund-Loads an
    /// Tool-Aufrufer zurueckgibt. Per String-Match gegen den ersten Text-Content-Block
    /// erkannt, weil das MCP-Protokoll keinen eigenen Loading-Statuscode vorsieht und die
    /// Server-Seite <c>IsError = false</c> setzt (kein Fehler, nur transienter Wartezustand).
    /// </summary>
    private const string LoadingMessagePrefix = "[INFO]: Server laedt die Solution noch.";

    /// <summary>Wie oft <see cref="CallToolAsync"/> auf eine Loading-Antwort hin retryt.</summary>
    private const int LoadingRetryMaxAttempts = 30;

    private readonly McpClient _client;

    private McpTestClient(McpClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Verbindet sich mit dem kompilierten <c>AiNetLinter.exe --mcp-server</c> für den Zielpfad.
    /// Bei flake-anfaelligen Parallel-Init-Szenarien greift eine Retry-Schleife mit exponentiellem
    /// Backoff: Default 3 Retries (0.5s/1s/2s) reichen im Median, im Worst-Case werden ~3.5s
    /// zusaetzliche Wartezeit pro Connect verbrannt.
    /// </summary>
    public static async Task<McpTestClient> ConnectAsync(
        string targetDirectory,
        int timeoutSeconds = 30,
        CancellationToken cancellationToken = default,
        McpTestClientRetryOptions? retryOptions = null)
    {
        retryOptions ??= new McpTestClientRetryOptions();
        var attempt = 0;
        Exception? lastException = null;

        while (attempt <= retryOptions.MaxRetries)
        {
            // Slot wird nur fuer den eigentlichen Subprozess-Start plus Handshake belegt und
            // sofort danach freigegeben (nicht erst bei DisposeAsync) — die Bremse begrenzt so
            // die Zahl gleichzeitiger Start-Vorgaenge (der teure, ressourcenintensive Moment),
            // nicht die Zahl insgesamt gleichzeitig verbundener Clients. Waere die Freigabe an
            // DisposeAsync gekoppelt, wuerde jeder Aufrufer mit mehr gleichzeitigen Connects als
            // Gate-Slots (z. B. McpTestClientParallelTests mit 16) dauerhaft blockieren: alle
            // erfolgreichen Connects haetten ihre Slots noch belegt und die wartenden Connects
            // koennten nie einen freien Slot bekommen, weil dessen Freigabe erst nach Abschluss
            // aller Connects erfolgen wuerde — klassischer Deadlock.
            using var lease = await SubprocessConcurrencyGate.AcquireAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var exePath = Path.Combine(AppContext.BaseDirectory, "AiNetLinter.exe");
                if (!File.Exists(exePath))
                {
                    throw new FileNotFoundException($"Erwartete AiNetLinter.exe nicht in BaseDirectory gefunden: {exePath}");
                }

                var transport = new StdioClientTransport(new StdioClientTransportOptions
                {
                    Name = "ainetlinter-mcp-test-client",
                    Command = exePath,
                    Arguments = ["--mcp-server", "--path", targetDirectory],
                });

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

                var client = await McpClient.CreateAsync(transport, cancellationToken: cts.Token);
                return new McpTestClient(client);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                lastException = ex;
                attempt++;
                if (attempt > retryOptions.MaxRetries) break;

                var delayMs = retryOptions.BaseDelayMs * Math.Pow(retryOptions.BackoffFactor, attempt - 1);
                await Task.Delay(TimeSpan.FromMilliseconds(delayMs), cancellationToken);
            }
        }

        throw new InvalidOperationException(
            $"MCP-Client-Connect scheiterte nach {retryOptions.MaxRetries + 1} Versuchen gegen '{targetDirectory}'.",
            lastException);
    }

    /// <summary>
    /// Führt ein MCP-Tool aus und gibt das rohe <see cref="CallToolResult"/> zurück. Antwortet
    /// der Server waehrend des Hintergrund-Loads mit <see cref="LoadingMessagePrefix"/>, wird
    /// die Anfrage nach kurzem Backoff transparent wiederholt — Tests muessen sich nicht um
    /// den Loading-Zustand kuemmern und sehen das erste echte Ergebnis.
    /// </summary>
    public async Task<CallToolResult> CallToolAsync(
        string toolName,
        IReadOnlyDictionary<string, object?>? arguments = null,
        int timeoutSeconds = 30,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt < LoadingRetryMaxAttempts; attempt++)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            var result = await _client.CallToolAsync(toolName, arguments, cancellationToken: cts.Token);

            if (!IsLoadingResponse(result))
            {
                return result;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
        }

        // Nach Erreichen des Retry-Limits die letzte Loading-Antwort zurueckgeben — der
        // Aufrufer sieht dann den Info-Text und kann selbst entscheiden, wie er damit
        // umgehen will (fail oder noch laenger warten).
        using var finalCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        finalCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        return await _client.CallToolAsync(toolName, arguments, cancellationToken: finalCts.Token);
    }

    private static bool IsLoadingResponse(CallToolResult result)
    {
        if (result.IsError == true) return false;
        if (result.Content is not { Count: > 0 }) return false;
        return result.Content[0] is TextContentBlock textBlock
            && textBlock.Text is { } text
            && text.StartsWith(LoadingMessagePrefix, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ruft die Liste aller vom MCP-Server bereitgestellten Tools ab.
    /// </summary>
    public async Task<IList<McpClientTool>> ListToolsAsync(
        int timeoutSeconds = 30,
        CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        return await _client.ListToolsAsync(cancellationToken: cts.Token);
    }

    /// <summary>
    /// Führt ein MCP-Tool aus und liefert den String des ersten <see cref="TextContentBlock"/> zurück.
    /// Wirft eine Exception bei MCP-Fehlerstatus.
    /// </summary>
    public async Task<string> CallToolGetTextAsync(
        string toolName,
        IReadOnlyDictionary<string, object?>? arguments = null,
        int timeoutSeconds = 30,
        CancellationToken cancellationToken = default)
    {
        var result = await CallToolAsync(toolName, arguments, timeoutSeconds, cancellationToken);
        if (result.IsError == true)
        {
            throw new InvalidOperationException($"MCP Tool '{toolName}' lieferte einen Fehler-Result zurück.");
        }

        if (result.Content == null || result.Content.Count == 0)
        {
            return string.Empty;
        }

        if (result.Content[0] is TextContentBlock textBlock)
        {
            return textBlock.Text ?? string.Empty;
        }

        return string.Empty;
    }

    public ValueTask DisposeAsync()
    {
        return _client.DisposeAsync();
    }
}
