#nullable enable

using System;

namespace AiNetLinter.Mcp.Assemblies;

internal static class AssemblySessionStatusExtensions
{
    internal static string ToWireValue(this AssemblySessionStatus status) =>
        status.ToString().ToLowerInvariant();

    internal static string ToCompletenessLabel(this AssemblySessionStatus status) =>
        status is AssemblySessionStatus.Complete
            or AssemblySessionStatus.Partial
            or AssemblySessionStatus.Degraded
            ? status.ToWireValue()
            : AssemblySessionStatus.Failed.ToWireValue();

    internal static bool TryParsePersisted(string value, out AssemblySessionStatus status)
    {
        if (!Enum.TryParse(value, ignoreCase: true, out status)) return false;
        return status is AssemblySessionStatus.Complete
            or AssemblySessionStatus.Partial
            or AssemblySessionStatus.Degraded;
    }
}
