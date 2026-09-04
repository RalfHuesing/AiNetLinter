#nullable enable

using System;

namespace AiNetLinter.Mcp.Assemblies.ExternalSource.ProcessExecution;

internal static class ExternalSourceGitProcessOutputPolicy
{
    internal static bool IsHarmlessStandardError(string? standardError)
    {
        if (string.IsNullOrWhiteSpace(standardError)) return true;
        foreach (var line in standardError.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (ContainsRepositorySafetyError(trimmed)
                || (!trimmed.StartsWith("warning:", StringComparison.OrdinalIgnoreCase)
                    && !trimmed.StartsWith("hint:", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ContainsRepositorySafetyError(string line) =>
        line.Contains("dubious ownership", StringComparison.OrdinalIgnoreCase)
        || line.Contains("unsafe repository", StringComparison.OrdinalIgnoreCase)
        || line.StartsWith("fatal:", StringComparison.OrdinalIgnoreCase)
        || line.StartsWith("error:", StringComparison.OrdinalIgnoreCase);
}
