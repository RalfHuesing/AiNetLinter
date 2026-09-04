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
        operation switch
        {
            "clone" => IsCloneProgressLine(line),
            "status" => string.Equals(
                line,
                "warning: repository maintenance is disabled",
                StringComparison.Ordinal),
            _ => false,
        };

    private static bool IsCloneProgressLine(string line) =>
        string.Equals(
            line,
            "Cloning into '.ainetlinter-git-clone'...",
            StringComparison.Ordinal);

    private static bool ContainsRepositorySafetyError(string line) =>
        line.Contains("dubious ownership", StringComparison.OrdinalIgnoreCase)
        || line.Contains("unsafe repository", StringComparison.OrdinalIgnoreCase)
        || line.StartsWith("fatal:", StringComparison.OrdinalIgnoreCase)
        || line.StartsWith("error:", StringComparison.OrdinalIgnoreCase);
}
