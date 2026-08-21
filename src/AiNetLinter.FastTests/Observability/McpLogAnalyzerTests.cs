#nullable enable

using System;
using System.IO;
using System.Text.Json;
using AiNetLinter.Observability;

namespace AiNetLinter.FastTests.Observability;

[Trait("Category", "Unit")]
public sealed class McpLogAnalyzerTests
{
    [Fact]
    public void Analyze_DirectoryAggregatesCallsErrorsRetriesAndCompleteness()
    {
        using var tempDir = TestTempDirectory.Create("mcp-log-analysis-");
        var firstDay = Directory.CreateDirectory(Path.Combine(tempDir.DirectoryPath, "2026-08-20"));
        var secondDay = Directory.CreateDirectory(Path.Combine(tempDir.DirectoryPath, "2026-08-21"));
        var firstLog = Path.Combine(firstDay.FullName, "ainetlinter_10_abc.jsonl");
        var secondLog = Path.Combine(secondDay.FullName, "ainetlinter_20_def.jsonl");

        File.WriteAllText(firstLog, string.Join(Environment.NewLine, new[]
        {
            "{\"recordType\":\"tool_call\",\"timestamp\":\"2026-08-20T10:00:00.000Z\",\"processId\":10,\"instanceId\":\"abc\",\"toolName\":\"find_symbol\",\"durationMs\":1,\"success\":true,\"isErrorResult\":false,\"response\":\"[HINWEIS]: Diese Daten sind vollstaendig fuer den angefragten Scope\",\"responseTruncated\":false}",
            "{\"recordType\":\"tool_call\",\"timestamp\":\"2026-08-20T10:00:01.000Z\",\"processId\":10,\"instanceId\":\"abc\",\"toolName\":\"get_symbol_body\",\"durationMs\":2,\"success\":true,\"isErrorResult\":false,\"response\":\"[INFO]: Server laedt die Solution noch.\",\"responseTruncated\":false}",
            "{\"recordType\":\"tool_call\",\"timestamp\":\"2026-08-20T10:00:02.000Z\",\"processId\":10,\"instanceId\":\"abc\",\"toolName\":\"get_symbol_body\",\"durationMs\":3,\"success\":true,\"isErrorResult\":false,\"response\":\"[INFO]: Server laedt die Solution noch.\",\"responseTruncated\":false}",
            "{\"recordType\":\"tool_call\",\"timestamp\":\"2026-08-20T10:00:04.000Z\",\"processId\":10,\"instanceId\":\"abc\",\"toolName\":\"get_symbol_body\",\"durationMs\":4,\"success\":true,\"isErrorResult\":false,\"response\":\"[INFO]: Server laedt die Solution noch.\",\"responseTruncated\":false}",
            "{\"recordType\":\"tool_call\",\"timestamp\":\"2026-08-20T10:00:06.000Z\",\"processId\":10,\"instanceId\":\"abc\",\"toolName\":\"get_symbol_body\",\"durationMs\":5,\"success\":true,\"isErrorResult\":false,\"response\":\"[ERROR]: INVALID_ARGUMENT: bad\",\"responseTruncated\":true}",
            "{\"recordType\":\"feedback\",\"title\":\"ignored\"}",
            "not-json",
        }));
        File.WriteAllText(secondLog,
            "{\"recordType\":\"tool_call\",\"timestamp\":\"2026-08-21T10:00:00.000Z\",\"processId\":20,\"instanceId\":\"def\",\"toolName\":\"find_symbol\",\"durationMs\":6,\"success\":true,\"isErrorResult\":true,\"response\":\"[ERROR]: SOLUTION_NOT_LOADED: failed\",\"responseTruncated\":false}");
        File.WriteAllText(Path.Combine(secondDay.FullName, "ainetlinter_20_def.feedback.jsonl"), "{not-call-log}");

        var report = McpLogAnalyzer.Analyze(tempDir.DirectoryPath);

        Assert.Equal(2, report.LogFiles.Count);
        Assert.Equal(6, report.ToolCallCount);
        Assert.Equal(1, report.InvalidLineCount);
        Assert.Single(report.InvalidLineDetails);
        Assert.Equal(1, report.IgnoredRecordCount);
        Assert.Equal(4, report.CallsPerTool["get_symbol_body"]);
        Assert.Equal(2, report.CallsPerTool["find_symbol"]);
        Assert.Equal(1, report.ErrorResultCount);
        Assert.Equal(1d / 6d, report.IsErrorRate, 6);
        Assert.Equal(1, report.ErrorCodes["INVALID_ARGUMENT"]);
        Assert.Equal(1, report.ErrorCodes["SOLUTION_NOT_LOADED"]);
        Assert.Equal(1, report.LoadingRetryBurstCount);
        Assert.Equal(3, report.LoadingRetryCallCount);
        Assert.Equal(1, report.ResponseTruncatedCount);
        Assert.Equal(1, report.CompletenessCompleteCount);
        Assert.Equal(1, report.CompletenessTruncatedCount);
        Assert.Equal(4, report.CompletenessUnknownCount);
        Assert.Equal(2, report.Sessions.Count);
        Assert.Equal(6, report.Sessions[0].DurationSeconds);
        Assert.Equal(15, report.Sessions[0].TotalDurationMs);
    }

    [Fact]
    public void Analyze_GlobSelectsNestedProcessLogs()
    {
        using var tempDir = TestTempDirectory.Create("mcp-log-glob-");
        var day = Directory.CreateDirectory(Path.Combine(tempDir.DirectoryPath, "2026-08-20"));
        var logPath = Path.Combine(day.FullName, "ainetlinter_10_abc.jsonl");
        File.WriteAllText(logPath, "{\"recordType\":\"tool_call\",\"toolName\":\"find_symbol\"}");

        var glob = Path.Combine(tempDir.DirectoryPath, "*", "ainetlinter_*_*.jsonl");
        var report = McpLogAnalyzer.Analyze(glob);

        Assert.Single(report.LogFiles);
        Assert.Equal(logPath, report.LogFiles[0]);
    }

    [Fact]
    public void Formatter_JsonAndTextAreDeterministicAndMachineReadable()
    {
        using var tempDir = TestTempDirectory.Create("mcp-log-format-");
        var logPath = tempDir.CreateFile("calls.jsonl", "{\"recordType\":\"tool_call\",\"toolName\":\"find_symbol\"}");
        var report = McpLogAnalyzer.Analyze(logPath);

        var json = McpLogReportFormatter.Format(report, "json");
        using var document = JsonDocument.Parse(json);
        Assert.Equal(1, document.RootElement.GetProperty("toolCallCount").GetInt32());
        Assert.Contains("# MCP-Call-Log-Auswertung", McpLogReportFormatter.Format(report, "text"));
    }
}
