#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.Tests.Mcp;

/// <summary>
/// Unit-Tests fuer <see cref="McpCallLog"/>: verifiziert die JSONL-Schreib-Mechanik
/// (Konzept-Felder ts/tool/args/lines/truncated/duration_ms/empty), die
/// Trunkierungs-/Leermenge-Erkennung und das automatische Loeschen leerer Log-Files.
/// </summary>
public sealed class McpCallLogTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task RecordStart_ThenEnd_WritesJsonLineWithAllFields()
    {
        var logPath = CreateTempLogPath();
        try
        {
            await using (var log = new McpCallLog(logPath))
            {
                await using var scope = log.StartRecording("find_symbol", "LinterEngine|null|50");
                var result = McpToolResults.Text("hit1\nhit2");
                scope.Complete(result);
            }

            var lines = await File.ReadAllLinesAsync(logPath);
            var entry = ParseSingleEntry(lines);

            Assert.Equal("find_symbol", entry.GetProperty("tool").GetString());
            Assert.Equal("LinterEngine|null|50", entry.GetProperty("args").GetString());
            Assert.Equal(2, entry.GetProperty("lines").GetInt32());
            Assert.False(entry.GetProperty("truncated").GetBoolean());
            Assert.False(entry.GetProperty("empty").GetBoolean());
            Assert.True(entry.GetProperty("duration_ms").GetDouble() >= 0);
            Assert.False(string.IsNullOrEmpty(entry.GetProperty("ts").GetString()));
        }
        finally
        {
            TryDelete(logPath);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RecordEnd_TruncatedResult_SetsTruncatedTrue()
    {
        var logPath = CreateTempLogPath();
        try
        {
            await using (var log = new McpCallLog(logPath))
            {
                await using var scope = log.StartRecording("find_references", "X|50");
                var truncatedText = "x1\nx2\n[3 Treffer gesamt, 2 gezeigt — Pattern verfeinern oder maxResults erhöhen]";
                scope.Complete(McpToolResults.Text(truncatedText));
            }

            var lines = await File.ReadAllLinesAsync(logPath);
            var entry = ParseSingleEntry(lines);

            Assert.True(entry.GetProperty("truncated").GetBoolean());
            Assert.False(entry.GetProperty("empty").GetBoolean());
        }
        finally
        {
            TryDelete(logPath);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RecordEnd_EmptyResult_SetsEmptyTrue()
    {
        var logPath = CreateTempLogPath();
        try
        {
            await using (var log = new McpCallLog(logPath))
            {
                await using var scope = log.StartRecording("get_index_scope", "");
                // 0 Zeilen, kein IsError -> empty
                scope.Complete(new CallToolResult
                {
                    IsError = false,
                    Content = new List<ContentBlock> { new TextContentBlock { Text = "" } },
                });
            }

            var lines = await File.ReadAllLinesAsync(logPath);
            var entry = ParseSingleEntry(lines);

            Assert.True(entry.GetProperty("empty").GetBoolean());
            Assert.Equal(0, entry.GetProperty("lines").GetInt32());
        }
        finally
        {
            TryDelete(logPath);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Dispose_NoRecords_DeletesLogFile()
    {
        var logPath = CreateTempLogPath();
        await using var log = new McpCallLog(logPath);

        // Datei wurde zwar initial angelegt (FileMode.Append oeffnet), aber kein Record -> Dispose loescht.
        // Wichtig: kein StartRecording aufgerufen.
        await log.DisposeAsync();

        Assert.False(File.Exists(logPath), $"Leeres Log-File wurde nicht geloescht: {logPath}");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RecordStart_LongArgs_TruncatedToTwoHundredPlusEllipsis()
    {
        var logPath = CreateTempLogPath();
        try
        {
            var longArgs = new string('a', 250);
            await using (var log = new McpCallLog(logPath))
            {
                await using var scope = log.StartRecording("find_symbol", longArgs);
                scope.Complete(McpToolResults.Text("hit"));
            }

            var lines = await File.ReadAllLinesAsync(logPath);
            var entry = ParseSingleEntry(lines);
            var args = entry.GetProperty("args").GetString();

            Assert.NotNull(args);
            Assert.Equal(203, args!.Length); // 200 + "..."
            Assert.EndsWith("...", args);
        }
        finally
        {
            TryDelete(logPath);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RecordError_BasicException_WritesJsonLineWithAllFields()
    {
        var logPath = CreateTempLogPath();
        try
        {
            var ex = new TestException("something went wrong");
            ex.SetStackTrace("at Foo.Bar() in C:\\\\Foo.cs:line 42");
            await using (var log = new McpCallLog(logPath))
            {
                log.RecordError("get_file_skeleton", "args|42", ex);
            }

            var lines = await File.ReadAllLinesAsync(logPath);
            var entry = ParseSingleEntry(lines);

            Assert.Equal("error", entry.GetProperty("level").GetString());
            Assert.Equal("TestException", entry.GetProperty("error_type").GetString());
            Assert.Equal("something went wrong", entry.GetProperty("error_message").GetString());
            Assert.Contains("Foo.Bar()", entry.GetProperty("stack_trace").GetString());
            Assert.Equal("get_file_skeleton", entry.GetProperty("tool").GetString());
            Assert.Equal("args|42", entry.GetProperty("args").GetString());
            Assert.False(string.IsNullOrEmpty(entry.GetProperty("ts").GetString()));
            Assert.False(entry.TryGetProperty("lines", out _));
            Assert.False(entry.TryGetProperty("truncated", out _));
            Assert.False(entry.TryGetProperty("duration_ms", out _));
            Assert.False(entry.TryGetProperty("empty", out _));
        }
        finally
        {
            TryDelete(logPath);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RecordError_StackTraceExceeds4KB_TruncatesToCap()
    {
        var logPath = CreateTempLogPath();
        try
        {
            var ex = new TestException("boom");
            ex.SetStackTrace(new string('a', 100_000));
            await using (var log = new McpCallLog(logPath))
            {
                log.RecordError("find_symbol", "X", ex);
            }

            var lines = await File.ReadAllLinesAsync(logPath);
            var entry = ParseSingleEntry(lines);
            var stackTrace = entry.GetProperty("stack_trace").GetString();

            Assert.NotNull(stackTrace);
            Assert.True(stackTrace!.Length <= 4096, $"stack_trace.Length = {stackTrace.Length} > 4096");
            Assert.EndsWith("...", stackTrace);
        }
        finally
        {
            TryDelete(logPath);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RecordError_AfterRecordEnd_PreservesOrderInJsonl()
    {
        var logPath = CreateTempLogPath();
        try
        {
            var ex = new InvalidOperationException("late failure");
            await using (var log = new McpCallLog(logPath))
            {
                var scope = log.StartRecording("find_symbol", "args");
                scope.Complete(McpToolResults.Text("hit"));
                await scope.DisposeAsync();
                log.RecordError("find_symbol", "args", ex);
            }

            var lines = await File.ReadAllLinesAsync(logPath);
            Assert.Equal(2, lines.Length);

            using var doc0 = JsonDocument.Parse(lines[0]);
            using var doc1 = JsonDocument.Parse(lines[1]);
            Assert.Equal("find_symbol", doc0.RootElement.GetProperty("tool").GetString());
            Assert.False(doc0.RootElement.TryGetProperty("level", out _));
            Assert.Equal("error", doc1.RootElement.GetProperty("level").GetString());
        }
        finally
        {
            TryDelete(logPath);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RecordError_BeforeRecordEnd_PreservesOrderInJsonl()
    {
        var logPath = CreateTempLogPath();
        try
        {
            var ex = new InvalidOperationException("early failure");
            await using (var log = new McpCallLog(logPath))
            {
                log.RecordError("find_symbol", "args", ex);
                await using var scope = log.StartRecording("find_symbol", "args");
                scope.Complete(McpToolResults.Text("hit"));
            }

            var lines = await File.ReadAllLinesAsync(logPath);
            Assert.Equal(2, lines.Length);

            using var doc0 = JsonDocument.Parse(lines[0]);
            using var doc1 = JsonDocument.Parse(lines[1]);
            Assert.Equal("error", doc0.RootElement.GetProperty("level").GetString());
            Assert.False(doc1.RootElement.TryGetProperty("level", out _));
            Assert.Equal("find_symbol", doc1.RootElement.GetProperty("tool").GetString());
        }
        finally
        {
            TryDelete(logPath);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RecordError_ParallelCallsDoNotInterleaveJsonLines()
    {
        var logPath = CreateTempLogPath();
        try
        {
            const int pairs = 50;
            await using (var log = new McpCallLog(logPath))
            {
                var tasks = new List<Task>(pairs * 2);
                for (var i = 0; i < pairs; i++)
                {
                    var idx = i;
                    tasks.Add(Task.Run(async () =>
                    {
                        await using var scope = log.StartRecording("parallel_tool", $"arg|{idx}");
                        scope.Complete(McpToolResults.Text($"hit{idx}"));
                    }));
                    tasks.Add(Task.Run(() =>
                    {
                        log.RecordError(
                            "parallel_tool",
                            $"arg|{idx}",
                            new InvalidOperationException($"err {idx}"));
                    }));
                }
                await Task.WhenAll(tasks);
            }

            var lines = await File.ReadAllLinesAsync(logPath);
            Assert.Equal(pairs * 2, lines.Length);

            // Ohne atomaren _writeLock wuerden halbe Zeilen entstehen, die JsonDocument.Parse
            // scheitern lassen. Jede Zeile muss als eigenstaendiges JSONL-Record parsebar sein.
            for (var i = 0; i < lines.Length; i++)
            {
                using var doc = JsonDocument.Parse(lines[i]);
                Assert.True(doc.RootElement.TryGetProperty("tool", out _));
            }
        }
        finally
        {
            TryDelete(logPath);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExecuteCallAsync_SuccessCall_WritesCallEntryAndReturnsResult()
    {
        var logPath = CreateTempLogPath();
        try
        {
            await using (var log = new McpCallLog(logPath))
            {
                var result = await log.ExecuteCallAsync("get_file_skeleton", "src/Foo.cs",
                    () => Task.FromResult(McpToolResults.Text("hit")));
                Assert.Equal("hit", ((TextContentBlock)result.Content[0]).Text);
            }
            var entry = ParseSingleEntry(await File.ReadAllLinesAsync(logPath));
            Assert.Equal("get_file_skeleton", entry.GetProperty("tool").GetString());
            Assert.Equal("src/Foo.cs", entry.GetProperty("args").GetString());
            Assert.False(entry.TryGetProperty("level", out _));
            Assert.True(entry.GetProperty("duration_ms").GetDouble() >= 0);
        }
        finally { TryDelete(logPath); }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExecuteCallAsync_ThrowingCall_WritesErrorEntryAndRethrows()
    {
        var logPath = CreateTempLogPath();
        try
        {
            var ex = new InvalidOperationException("simuliertes Hot-Reload-Race in get_file_skeleton");
            await using (var log = new McpCallLog(logPath))
            {
                var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    log.ExecuteCallAsync("get_file_skeleton", "src/Foo.cs",
                        () => Task.FromException<CallToolResult>(ex)));
                Assert.Same(ex, thrown);
            }
            var entry = ParseSingleEntry(await File.ReadAllLinesAsync(logPath));
            Assert.Equal("error", entry.GetProperty("level").GetString());
            Assert.Equal("InvalidOperationException", entry.GetProperty("error_type").GetString());
            Assert.Equal("simuliertes Hot-Reload-Race in get_file_skeleton",
                entry.GetProperty("error_message").GetString());
            Assert.Equal("get_file_skeleton", entry.GetProperty("tool").GetString());
            Assert.Equal("src/Foo.cs", entry.GetProperty("args").GetString());
            Assert.False(string.IsNullOrEmpty(entry.GetProperty("stack_trace").GetString()));
        }
        finally { TryDelete(logPath); }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExecuteCallAsync_OperationCanceled_NotLoggedAndRethrown()
    {
        var logPath = CreateTempLogPath();
        try
        {
            await using (var log = new McpCallLog(logPath))
            {
                await Assert.ThrowsAsync<TaskCanceledException>(() =>
                    log.ExecuteCallAsync("find_symbol", "Foo|null|50",
                        () => Task.FromCanceled<CallToolResult>(new CancellationToken(canceled: true))));
            }
            // OCE darf weder Call- noch Error-Eintrag erzeugen - File leer, beim Dispose geloescht.
            Assert.False(File.Exists(logPath), "Log-File wurde fuer OCE-Call angelegt");
        }
        finally { TryDelete(logPath); }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ExecuteCallAsync_ParallelThrowingCallsDoNotInterleaveJsonLines()
    {
        var logPath = CreateTempLogPath();
        try
        {
            const int parallel = 50;
            await using (var log = new McpCallLog(logPath))
            {
                var tasks = new List<Task>(parallel);
                for (var i = 0; i < parallel; i++)
                {
                    var idx = i;
                    tasks.Add(Task.Run(async () =>
                    {
                        try { await log.ExecuteCallAsync("parallel_throw", $"arg|{idx}",
                            () => Task.FromException<CallToolResult>(new InvalidOperationException($"err {idx}"))); }
                        catch (InvalidOperationException) { /* expected */ }
                    }));
                }
                await Task.WhenAll(tasks);
            }
            var lines = await File.ReadAllLinesAsync(logPath);
            Assert.Equal(parallel, lines.Length);
            for (var i = 0; i < lines.Length; i++)
            {
                using var doc = JsonDocument.Parse(lines[i]);
                var entry = doc.RootElement;
                Assert.True(entry.TryGetProperty("tool", out _));
                Assert.Equal("error", entry.GetProperty("level").GetString());
                Assert.Equal("parallel_throw", entry.GetProperty("tool").GetString());
            }
        }
        finally { TryDelete(logPath); }
    }

    private static string CreateTempLogPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mcp-call-log-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "calls.log");
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
            var dir = Path.GetDirectoryName(path);
            if (dir is not null && Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
        catch
        {
            // best-effort cleanup, kein Test-Fail
        }
    }

    private static JsonElement ParseSingleEntry(string[] lines)
    {
        Assert.Single(lines);
        using var doc = JsonDocument.Parse(lines[0]);
        return doc.RootElement.Clone();
    }

    /// <summary>
    /// Synthetische Exception mit kontrollierbarem <see cref="Exception.StackTrace"/>.
    /// In aktuellen .NET-Versionen ist <c>StackTrace</c> get-only und ohne
    /// ueberschreibbaren Setter; der interne Cache-Field <c>_stackTraceString</c>
    /// wird daher per Reflection beschrieben, damit Tests Strings jenseits der
    /// 4 KB Cap (bzw. beliebige Sub-Strings) einspeisen koennen.
    /// </summary>
    private sealed class TestException : Exception
    {
        private static readonly System.Reflection.FieldInfo StackTraceStringField =
            typeof(Exception).GetField(
                "_stackTraceString",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Exception._stackTraceString field not found");

        public TestException(string message)
            : base(message)
        {
        }

        public void SetStackTrace(string stackTrace)
        {
            StackTraceStringField.SetValue(this, stackTrace);
        }
    }
}
