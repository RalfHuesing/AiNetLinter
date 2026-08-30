#nullable enable

using System;
using System.Collections.Generic;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

internal static class AssemblySessionStatusExtensions
{
    internal static AssemblySessionStatus ResolveEffectiveStatus(
        this AssemblySessionStatus status,
        IReadOnlyCollection<string> diagnostics) =>
        status == AssemblySessionStatus.Complete && diagnostics.Count > 0
            ? AssemblySessionStatus.Partial
            : status;

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
