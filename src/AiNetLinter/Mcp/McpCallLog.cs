#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp;

/// <summary>
/// Opt-in Call-Log fuer den MCP-Server. Schreibt pro Tool-Aufruf eine JSONL-Zeile mit
/// Zeitstempel, Tool-Name, gekuerzten Argumenten, Ergebniszeilen, Trunkierungs- und
/// Leermenge-Flags und Dauer in eine konfigurierbare Datei. Default: deaktiviert
/// (kein File I/O). Aktivierung ueber das <c>--mcp-log &lt;pfad&gt;</c>-Flag; leere
/// Log-Dateien werden beim Dispose automatisch geloescht. Trunkierungs-Erkennung
/// wiederverwendet die Strings aus <see cref="McpTruncation"/>.
/// </summary>
internal sealed class McpCallLog : IAsyncDisposable
{
    private const int MaxArgsLength = 200;
    private const string ArgsEllipsis = "...";
    private const int MaxStackTraceLength = 4096;
    private const string StackTraceTruncationMarker = "...";

    private readonly StreamWriter _writer;
    private readonly string _logPath;
    private readonly Lock _writeLock = new();
    private int _entryCount;
    private bool _disposed;

    internal McpCallLog(string logPath)
    {
        _logPath = logPath;
        var dir = Path.GetDirectoryName(logPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        _writer = new StreamWriter(
            new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.Read),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    /// <summary>
    /// Startet einen Aufzeichnungs-Scope fuer einen Tool-Aufruf. Das zurueckgegebene
    /// <see cref="McpCallLogScope"/> misst die Dauer und nimmt das <see cref="CallToolResult"/>
    /// entgegen, sobald das Tool geantwortet hat.
    /// </summary>
    internal McpCallLogScope StartRecording(string toolName, string args)
    {
        return new McpCallLogScope(this, toolName, args, Stopwatch.StartNew());
    }

    internal int EntryCount => _entryCount;

    internal string LogPath => _logPath;

    internal void RecordEnd(McpCallLogScope scope, CallToolResult result)
    {
        scope.Stopwatch.Stop();
        var args = scope.Args.Length > MaxArgsLength
            ? scope.Args[..MaxArgsLength] + ArgsEllipsis
            : scope.Args;

        var text = ExtractText(result);
        var lines = text is null ? 0 : CountLines(text);
        var truncated = text is not null && McpTruncationResult.IsTruncated(text);
        var empty = result.IsError != true && lines == 0;

        var entry = new
        {
            ts = DateTime.UtcNow.ToString("O"),
            tool = scope.ToolName,
            args,
            lines,
            truncated,
            duration_ms = scope.Stopwatch.Elapsed.TotalMilliseconds,
            empty,
        };
        var json = JsonSerializer.Serialize(entry);

        lock (_writeLock)
        {
            if (_disposed) return;
            _writer.WriteLine(json);
            _writer.Flush();
            _entryCount++;
        }
    }

    /// <summary>
    /// Persistiert einen Fehler-Eintrag in derselben JSONL-Datei wie <see cref="RecordEnd"/>.
    /// Schema erweitert den Call-Eintrag um level/error_type/error_message/stack_trace;
    /// gemeinsame Felder (ts/tool/args) bleiben identisch. Selber Lock wie RecordEnd
    /// serialisiert die zeitliche Reihenfolge; der Stack-Trace wird auf 4 KB gekappt,
    /// damit eine einzelne Exception das Log nicht aufblaet.
    /// </summary>
    internal void RecordError(string toolName, string args, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var argsTruncated = args.Length > MaxArgsLength
            ? args[..MaxArgsLength] + ArgsEllipsis
            : args;

        var stackTrace = exception.StackTrace ?? string.Empty;
        if (stackTrace.Length > MaxStackTraceLength)
        {
            stackTrace = string.Concat(
                stackTrace.AsSpan(0, MaxStackTraceLength - StackTraceTruncationMarker.Length),
                StackTraceTruncationMarker);
        }

        var entry = new
        {
            ts = DateTime.UtcNow.ToString("O"),
            tool = toolName,
            args = argsTruncated,
            level = "error",
            error_type = exception.GetType().Name,
            error_message = exception.Message,
            stack_trace = stackTrace,
        };
        var json = JsonSerializer.Serialize(entry);

        lock (_writeLock)
        {
            if (_disposed) return;
            _writer.WriteLine(json);
            _writer.Flush();
            _entryCount++;
        }
    }

    /// <summary>
    /// Zentrale try/catch-Huelle fuer die Tool-Handler in den
    /// <c>*ToolRegistrations</c>-Klassen. Startet einen Aufzeichnungs-Scope, ruft
    /// das Tool auf, schliesst den Scope bei Erfolg (schreibt regulaeren
    /// Call-Eintrag via <see cref="RecordEnd"/>) und persistiert bei unbehandelter
    /// Exception einen Error-Eintrag (level=error) via <see cref="RecordError"/>.
    /// Re-Throw der Exception nach dem Logging, damit das SDK sie wie ueblich als
    /// JSON-RPC-Error zurueckgeben kann. <see cref="OperationCanceledException"/>
    /// wird herausgefiltert, damit Shutdown-/Cancellation-Signale nicht als
    /// Tool-Fehler ins Call-Log laufen.
    /// </summary>
    internal async Task<CallToolResult> ExecuteCallAsync(
        string toolName, string args, Func<Task<CallToolResult>> toolFn)
    {
        ArgumentNullException.ThrowIfNull(toolFn);
        var scope = StartRecording(toolName, args);
        try
        {
            var result = await toolFn().ConfigureAwait(false);
            scope.Complete(result);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            RecordError(toolName, args, ex);
            throw;
        }
        finally
        {
            await scope.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static int CountLines(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var count = 1;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n') count++;
        }
        return count;
    }

    private static string? ExtractText(CallToolResult result)
    {
        if (result.Content is not { Count: > 0 }) return null;
        return result.Content[0] is TextContentBlock t ? t.Text : null;
    }

    public async ValueTask DisposeAsync()
    {
        lock (_writeLock)
        {
            if (_disposed) return;
            _disposed = true;
        }
        await _writer.DisposeAsync();
        if (_entryCount == 0)
        {
            try { File.Delete(_logPath); }
            catch (IOException ex)
            {
                // Log-Delete-Fehler ist kein Blocker (z. B. Datei zwischenzeitlich von einem
                // externen Tool geloescht). Sichtbar auf stderr, damit der Agent-Loop darauf
                // reagieren kann, ohne den Server-Shutdown zu blockieren.
                Console.Error.WriteLine($"[WARN]: MCP-Call-Log konnte nicht geloescht werden ({_logPath}): {ex.Message}");
            }
        }
    }
}

/// <summary>
/// Pro-Tool-Aufnahme-Scope: misst die Dauer und nimmt das finale <see cref="CallToolResult"/>
/// entgegen, das beim Dispose an <see cref="McpCallLog"/> weitergereicht wird.
/// </summary>
internal sealed class McpCallLogScope : IAsyncDisposable
{
    private readonly McpCallLog _log;
    private CallToolResult? _result;

    internal string ToolName { get; }
    internal string Args { get; }
    internal Stopwatch Stopwatch { get; }

    internal McpCallLogScope(McpCallLog log, string toolName, string args, Stopwatch sw)
    {
        _log = log;
        ToolName = toolName;
        Args = args;
        Stopwatch = sw;
    }

    /// <summary>
    /// Uebergibt das finale Tool-Ergebnis an den Scope, damit es beim Dispose geschrieben
    /// wird. Wird VOR <see cref="DisposeAsync"/> aufgerufen, typischerweise direkt nach
    /// <c>await</c> auf das Tool-Resultat.
    /// </summary>
    internal void Complete(CallToolResult result)
    {
        _result = result;
    }

    public ValueTask DisposeAsync()
    {
        if (_result is { } r) _log.RecordEnd(this, r);
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Wiederverwendbarer String-Match gegen die Trunkierungs-Meta-Zeilen aus
/// <see cref="McpTruncation"/>. Bewusst als Helper extrahiert (nicht inlined), damit
/// <see cref="McpCallLog"/> ohne direkten Zugriff auf interne Konstanten testbar bleibt
/// und kein Duplikat-String im Call-Log-Pfad entsteht.
/// </summary>
internal static class McpTruncationResult
{
    // Die echten Meta-Zeilen aus McpTruncation.cs beginnen mit "[<Zahl> Treffer gesamt, <Zahl>
    // gezeigt -" (bzw. Dateien-Variante). Statt die exakten Strings zu duplizieren, wird auf
    // die literalen Praefix-Markierungen "Treffer gesamt, " und "Dateien mit Textfund, "
    // gematcht - das ist die gleiche Information und zukunftssicher gegen Zahlenvariationen.
    private const string ListTruncationMarker = "Treffer gesamt, ";
    private const string FileListTruncationMarker = "Dateien mit Textfund, ";

    internal static bool IsTruncated(string text)
    {
        return text.Contains(ListTruncationMarker, StringComparison.Ordinal)
            || text.Contains(FileListTruncationMarker, StringComparison.Ordinal);
    }
}
