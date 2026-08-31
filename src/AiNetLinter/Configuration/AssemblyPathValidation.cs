#nullable enable

using System;
using System.IO;

namespace AiNetLinter.Configuration;

/// <summary>
/// Zentrales Regelwerk fuer Assembly-Datei-Erweiterungen. Unterstuetzt werden verwaltete
/// .NET-Assemblies mit CLI/PE-Metadaten: .dll und .exe gelten als gleichwertige Ziele.
/// </summary>
internal static class AssemblyPathValidation
{
    /// <summary>Prueft, ob der Pfad auf eine unterstuetzte Assembly-Erweiterung (.dll/.exe) endet.</summary>
    internal static bool IsSupportedAssemblyPath(string path) =>
        HasSupportedAssemblyExtension(Path.GetExtension(path));

    /// <summary>Prueft, ob die Dateiendung eine unterstuetzte Assembly-Erweiterung (.dll/.exe) ist.</summary>
    internal static bool HasSupportedAssemblyExtension(string? extension) =>
        string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase)
        || string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Entfernt ein optionales Assembly-Suffix (.dll/.exe) von einem Assembly-Namen oder -Alias.
    /// </summary>
    internal static string WithoutAssemblyExtension(string value)
    {
        if (value.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return value[..^4];
        }

        return value;
    }
}