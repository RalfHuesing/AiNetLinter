#nullable enable

using System.IO;
using System.Text.Json;
using AiNetLinter.Commands;
using AiNetLinter.TestKit;

namespace AiNetLinter.FastTests.Commands;

[Trait("Category", "Unit")]
public sealed class AnalyzeMcpLogCommandTests
{
    [Fact]
    public void Run_JsonFormatWritesParseableReportWithoutHeaderNoise()
    {
        using var tempDir = TestTempDirectory.Create("mcp-log-command-");
        var logPath = tempDir.CreateFile("calls.jsonl", "{\"recordType\":\"tool_call\",\"toolName\":\"find_symbol\"}");
        var console = new RecordingLintConsole();

        var exitCode = AnalyzeMcpLogCommand.Run(logPath, "json", console);

        Assert.Equal(0, exitCode);
        Assert.Empty(console.ErrorLines);
        using var document = JsonDocument.Parse(console.OutputText);
        Assert.Equal(1, document.RootElement.GetProperty("toolCallCount").GetInt32());
    }

    [Fact]
    public void Run_InvalidFormatReturnsRecoverableCliError()
    {
        using var tempDir = TestTempDirectory.Create("mcp-log-command-format-");
        var logPath = tempDir.CreateFile("calls.jsonl", "{}");
        var console = new RecordingLintConsole();

        var exitCode = AnalyzeMcpLogCommand.Run(logPath, "xml", console);

        Assert.Equal(1, exitCode);
        Assert.Contains("INVALID_ARGUMENT", console.ErrorText);
    }
}
