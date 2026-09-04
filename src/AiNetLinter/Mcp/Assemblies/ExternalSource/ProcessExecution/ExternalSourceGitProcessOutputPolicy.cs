#nullable enable

using System;

namespace AiNetLinter.Mcp.Assemblies.ExternalSource.ProcessExecution;

internal static class ExternalSourceGitProcessOutputPolicy
{
    internal static bool IsHarmlessStandardError(
        string? standardError,
        string? operation = null)
    {
        if (string.IsNullOrWhiteSpace(standardError)) return true;
        foreach (var line in standardError.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (ContainsRepositorySafetyError(trimmed)
                || !IsAllowedStandardErrorLine(trimmed, operation))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAllowedStandardErrorLine(string line, string? operation) =>
        line.StartsWith("warning:", StringComparison.OrdinalIgnoreCase)
        || line.StartsWith("hint:", StringComparison.OrdinalIgnoreCase)
        || operation is "clone" && IsCloneProgressLine(line);

    private static bool IsCloneProgressLine(string line) =>
        line.StartsWith("Cloning into '", StringComparison.Ordinal)
        && line.EndsWith("'...", StringComparison.Ordinal);

    private static bool ContainsRepositorySafetyError(string line) =>
        line.Contains("dubious ownership", StringComparison.OrdinalIgnoreCase)
        || line.Contains("unsafe repository", StringComparison.OrdinalIgnoreCase)
        || line.StartsWith("fatal:", StringComparison.OrdinalIgnoreCase)
        || line.StartsWith("error:", StringComparison.OrdinalIgnoreCase);
}
