#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
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
}
