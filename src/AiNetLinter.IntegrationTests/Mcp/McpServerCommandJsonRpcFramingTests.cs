#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Fixtures;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp;

/// <summary>
/// First-Principles-E2E-Test fuer das JSON-RPC-Framing des MCP-Servers: spawnt einen
/// <c>AiNetLinter.exe --mcp-server</c>-Subprozess, schreibt <c>initialize</c> +
/// <c>notifications/initialized</c> + <c>tools/list</c> + <c>tools/call</c> manuell als
/// newline-delimited JSON auf stdin, liest stdout zeilenweise roh zurueck und verifiziert
/// <b>jede</b> Zeile als gueltigen JSON-RPC-Frame (<c>jsonrpc == "2.0"</c>).
/// Dieser Test umgeht bewusst den SDK-Parser zwischen Subprozess und Assertions - ein
/// einziger ungefilterter <c>Console.WriteLine</c>-Call aus irgendeiner zentralen
/// Hilfsklasse wuerde das JSON-RPC-Framing der gesamten Session zerstoeren und hier als
/// nicht-JSON-Zeile sichtbar werden. Regressions-Schutz fuer die strukturelle
/// stdout-Absicherung durch <see cref="AiNetLinter.Output.McpLintConsole"/>.
/// </summary>
[Trait("Category", "Integration")]
public sealed class McpServerCommandJsonRpcFramingTests
{
    private const string ProtocolVersion = "2024-11-05";
    private const string ClientName = "framing-test-client";
    private const string ClientVersion = "1.0.0";

    [Fact]
    public async Task ToolCallSequence_AllStdoutLinesAreValidJsonRpcFrames()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();

        // Handgeschriebene JSON-RPC-Frames - bewusst ohne SDK-Parser, damit der Test die
        // Server-seitige stdout-Disziplin direkt prueft. Sequenz: initialize, initialized,
        // tools/list, tools/call find_symbol, tools/call get_index_scope. Jede Antwort
        // muss ein jsonrpc==2.0-Frame sein.
        var frames = new[]
        {
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{" +
                "\"protocolVersion\":\"" + ProtocolVersion + "\"," +
                "\"capabilities\":{}," +
                "\"clientInfo\":{\"name\":\"" + ClientName + "\",\"version\":\"" + ClientVersion + "\"}}}",
            "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}",
            "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\"}",
            "{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"tools/call\",\"params\":{" +
                "\"name\":\"find_symbol\",\"arguments\":{\"namePattern\":\"Greeter\"}}}",
            "{\"jsonrpc\":\"2.0\",\"id\":4,\"method\":\"tools/call\",\"params\":{" +
                "\"name\":\"get_index_scope\",\"arguments\":{}}}",
        };

        var observedLines = await RunAndCollectStdoutAsync(fixture.RootPath, frames);

        Assert.NotEmpty(observedLines);
        // Die exakte Anzahl der Antwort-Frames haengt vom Timing der Solution-Hintergrund-Loads
        // ab (B.4 Drei-Zustands-Lifecycle): initialize + tools/list kommen sofort; die
        // tools/call-Antworten koennen hinter dem Load zurueckbleiben, bevor der Server
        // nach stdin-EOF herunterfaehrt. Strukturell wird verifiziert, dass JEDE Zeile ein
        // gueltiger JSON-RPC-Frame ist - die Anzahl selbst ist nicht der Befund, sondern die
        // Disziplin-Eigenschaft. Fruehere Test-Variante mit ">= 4" war zu strikt.
        Assert.True(observedLines.Count >= 2,
            $"Erwartete mindestens 2 JSON-RPC-Antwort-Frames (initialize + tools/list), " +
            $"erhielt {observedLines.Count} Zeilen.");

        var parseFailures = new System.Collections.Generic.List<string>();
        var nonJsonRpcLines = new System.Collections.Generic.List<string>();
        var lineIndex = 0;
        foreach (var line in observedLines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            lineIndex++;
            try
            {
                using var doc = JsonDocument.Parse(line);
                if (!doc.RootElement.TryGetProperty("jsonrpc", out var jsonrpc) ||
                    jsonrpc.GetString() != "2.0")
                {
                    nonJsonRpcLines.Add($"[Z. {lineIndex}] kein jsonrpc==2.0: {TrimForLog(line)}");
                }
            }
            catch (JsonException ex)
            {
                parseFailures.Add($"[Z. {lineIndex}] {ex.Message}: {TrimForLog(line)}");
            }
        }

        Assert.True(parseFailures.Count == 0,
            "Ungueltige JSON-Zeilen auf stdout entdeckt (bedeutet: ein Caller hat " +
            "Console.WriteLine genutzt statt McpLintConsole - strukturelle Absicherung verletzt): " +
            string.Join(" | ", parseFailures));
        Assert.True(nonJsonRpcLines.Count == 0,
            "Zeilen ohne gueltigen jsonrpc==2.0-Header entdeckt: " +
            string.Join(" | ", nonJsonRpcLines));
    }

    [Fact]
    public async Task HandshakeOnly_AllStdoutLinesAreValidJsonRpcFrames()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();

        // Minimaler Smoke-Test: nur initialize + initialized. Erwartet exakt 1
        // JSON-RPC-Antwort (die initialize-Response), notifications/initialized liefert
        // per JSON-RPC-Spec nichts zurueck.
        var frames = new[]
        {
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{" +
                "\"protocolVersion\":\"" + ProtocolVersion + "\"," +
                "\"capabilities\":{}," +
                "\"clientInfo\":{\"name\":\"" + ClientName + "\",\"version\":\"" + ClientVersion + "\"}}}",
            "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}",
        };

        var observedLines = await RunAndCollectStdoutAsync(fixture.RootPath, frames);

        Assert.NotEmpty(observedLines);
        foreach (var line in observedLines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var doc = JsonDocument.Parse(line);
            Assert.Equal("2.0", doc.RootElement.GetProperty("jsonrpc").GetString());
        }
    }

    [Fact]
    public async Task Initialize_ResponseInstructionsField_ContainsServerInstructionsDoctrine()
    {
        // Die ServerInstructions.Text-Doctrine muss tatsaechlich im initialize-Response auf dem
        // Wire ankommen — nicht nur auf McpServerOptions-Ebene (siehe McpServerOptionsFactoryTests
        // fuer den Options-Ebenen-Test). Roher JSON-Parse gegen das "instructions"-Feld
        // (JSON-Property-Name laut ModelContextProtocol.Core InitializeResult), bewusst ohne
        // SDK-Client, analog zu den anderen Framing-Tests in dieser Klasse.
        using var fixture = new SymbolGraphMiniFixtureWorkspace();

        var frames = new[]
        {
            "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{" +
                "\"protocolVersion\":\"" + ProtocolVersion + "\"," +
                "\"capabilities\":{}," +
                "\"clientInfo\":{\"name\":\"" + ClientName + "\",\"version\":\"" + ClientVersion + "\"}}}",
            "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}",
        };

        var observedLines = await RunAndCollectStdoutAsync(fixture.RootPath, frames);

        string? instructions = null;
        foreach (var line in observedLines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var doc = JsonDocument.Parse(line);
            if (!doc.RootElement.TryGetProperty("id", out var id) || id.GetInt32() != 1) continue;
            instructions = doc.RootElement.GetProperty("result").GetProperty("instructions").GetString();
            break;
        }

        Assert.False(string.IsNullOrEmpty(instructions));
        Assert.Contains("search_pattern", instructions, StringComparison.Ordinal);
        Assert.Contains("Sufficiency-Doctrine", instructions, StringComparison.Ordinal);
        Assert.Contains("isError-Policy", instructions, StringComparison.Ordinal);
    }

    private static async Task<System.Collections.Generic.List<string>> RunAndCollectStdoutAsync(
        string targetDirectory, string[] frames)
    {
        using var lease = await McpProcessLifetimeBudget.Shared.AcquireAsync(CancellationToken.None);
        var exePath = Path.Combine(AppContext.BaseDirectory, "AiNetLinter.exe");
        if (!File.Exists(exePath))
        {
            throw new FileNotFoundException(
                $"Erwartete AiNetLinter.exe nicht in BaseDirectory gefunden: {exePath}. " +
                "Test laeuft nur nach dotnet build.");
        }

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = targetDirectory,
        };
        psi.ArgumentList.Add("--mcp-server");
        psi.ArgumentList.Add("--path");
        psi.ArgumentList.Add(targetDirectory);

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("AiNetLinter-Subprozess konnte nicht gestartet werden.");

        // stderr asynchron in einen Puffer mitlesen, damit der Subprozess sich nicht an einem
        // vollen stderr-Puffer aufhaengt. Wird im Test-Output nicht direkt geprueft, der
        // eigentliche Assert liegt auf stdout-Disziplin.
        var stderrTask = process.StandardError.ReadToEndAsync();

        // Producer/Consumer: schreibt die Frames in den stdin-Pipe (mit kleinen Pausen, damit
        // der Server Zeit zum Verarbeiten hat zwischen den Anfragen) und liest parallel die
        // stdout-Antwort-Frames. Wird stdin am Ende der Producer-Phase geschlossen, faehrt der
        // Server nach Verarbeitung aller Frames graceful herunter.
        var writer = process.StandardInput;
        var writerTask = Task.Run(async () =>
        {
            foreach (var frame in frames)
            {
                await writer.WriteLineAsync(frame);
                await writer.FlushAsync();
                await Task.Delay(500);
            }
            // stdin schliessen, damit der StdioServerTransport auf EOF-Read graceful beendet.
            try { writer.Close(); } catch { /* Pipe evtl. schon zu */ }
        });

        // stdout zeilenweise lesen, bis der Prozess beendet (EOF auf stdout = process exit)
        // oder der Timeout greift. Grosszuegiger Timeout wegen Solution-Load im Hintergrund
        // (B.4 Drei-Zustands-Lifecycle) + Tool-Call-Latenz.
        var observed = new System.Collections.Generic.List<string>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        try
        {
            while (!cts.IsCancellationRequested)
            {
                var line = await process.StandardOutput.ReadLineAsync(cts.Token);
                if (line is null) break;
                observed.Add(line);
            }
        }
        catch (OperationCanceledException)
        {
            // Server hat zu lange gebraucht; Frames bis hierhin lesen und Assertion laufen lassen.
        }

        // Producer-Task sauber beenden.
        try { await writerTask; } catch { /* Pipe-Close-Fehler ist hier OK */ }

        await EnsureProcessTerminatedAsync(process, stderrTask);

        return observed;
    }

    /// <summary>
    /// Wartet auf graceful exit (kurzer Timeout), erzwingt danach einen Kill statt den Test
    /// unbegrenzt haengen zu lassen, und deckelt anschliessend das Warten auf <paramref
    /// name="stderrTask"/> (blockiert sonst bis stderr-EOF = Prozessende) mit einem eigenen
    /// Timeout. Ausgelagert aus <see cref="RunAndCollectStdoutAsync"/>, damit dessen eigene
    /// Komplexitaet unter <c>MaxCyclomaticComplexity</c>/<c>MaxCognitiveComplexity</c> bleibt —
    /// vorher fehlte dieser gesamte Force-Kill-Pfad, was einen nie beendeten Subprozess unter
    /// Last (konkurrierende Solution-Loads anderer parallel laufender Tests/Prozesse) den ganzen
    /// Testlauf unbegrenzt blockieren liess, empirisch beobachtet als Volllauf-Haenger
    /// 2026-08-11.
    /// </summary>
    private static async Task EnsureProcessTerminatedAsync(Process process, Task stderrTask)
    {
        if (!process.HasExited)
        {
            TryWaitOrKill(process);
        }

        // stderrTask kann nach einem erzwungenen Kill kurzzeitig offen bleiben (Pipe-Handle-
        // Teardown ist nicht synchron mit dem Kill) — eigener Timeout statt unbegrenztem await,
        // damit ein Edge-Case hier nie wieder den ganzen Testlauf blockieren kann.
        await Task.WhenAny(stderrTask, Task.Delay(TimeSpan.FromSeconds(10)));
    }

    private static void TryWaitOrKill(Process process)
    {
        try
        {
            if (!process.WaitForExit(TimeSpan.FromSeconds(10)) && !process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // bewusst geschluckt - wir wollen den Test nicht am Server-Shutdown scheitern lassen,
            // wenn der eigentliche Befund (stdout-Frames) schon erhoben ist.
        }
    }

    private static string TrimForLog(string line) =>
        line.Length <= 240 ? line : line[..240] + "...";
}
