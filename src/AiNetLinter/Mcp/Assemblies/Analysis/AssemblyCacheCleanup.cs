#nullable enable

using System;
using System.IO;
using Serilog;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

internal static class AssemblyCacheCleanup
{
    internal static void DeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warning(ex, "Assembly-Cache-Cleanup fehlgeschlagen: Art={CleanupKind}, Pfad={Path}", "Datei", path);
        }
    }

    internal static void DeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warning(ex, "Assembly-Cache-Cleanup fehlgeschlagen: Art={CleanupKind}, Pfad={Path}", "Verzeichnis", directory);
        }
    }
}
