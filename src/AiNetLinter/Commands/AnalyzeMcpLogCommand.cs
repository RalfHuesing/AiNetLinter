#nullable enable

using System;
using AiNetLinter.Observability;
using AiNetLinter.Output;

namespace AiNetLinter.Commands;

internal static class AnalyzeMcpLogCommand
{
    internal static int Run(string inputPath, string? format, ILintConsole? console = null)
    {
        var output = console ?? LinterConsole.Instance;
        try
        {
            var report = McpLogAnalyzer.Analyze(inputPath);
            output.WriteLine(McpLogReportFormatter.Format(report, format));
            return 0;
        }
        catch (ArgumentException ex)
        {
            output.WriteError($"[ERROR]: INVALID_ARGUMENT: {ex.Message}");
            return 1;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            output.WriteError($"[ERROR]: RESOURCE_NOT_FOUND: {ex.Message}");
            return 1;
        }
    }
}
