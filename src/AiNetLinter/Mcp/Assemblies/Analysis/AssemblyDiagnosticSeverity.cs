#nullable enable

using System;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

internal enum AssemblyDiagnosticSeverity
{
    Warning,
    Error,
}

internal static class AssemblyDiagnosticSeverityExtensions
{
    internal const string WarningWireValue = "warning";
    internal const string ErrorWireValue = "error";

    internal static string ToWireValue(this AssemblyDiagnosticSeverity severity) => severity switch
    {
        AssemblyDiagnosticSeverity.Warning => WarningWireValue,
        AssemblyDiagnosticSeverity.Error => ErrorWireValue,
        _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, "Unbekannte Assembly-Diagnoseschwere.")
    };

    internal static bool TryParseWireValue(string? value, out AssemblyDiagnosticSeverity severity)
    {
        if (string.Equals(value, WarningWireValue, StringComparison.OrdinalIgnoreCase))
        {
            severity = AssemblyDiagnosticSeverity.Warning;
            return true;
        }

        if (string.Equals(value, ErrorWireValue, StringComparison.OrdinalIgnoreCase))
        {
            severity = AssemblyDiagnosticSeverity.Error;
            return true;
        }

        severity = default;
        return false;
    }
}
