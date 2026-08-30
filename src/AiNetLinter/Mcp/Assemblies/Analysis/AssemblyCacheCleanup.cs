#nullable enable

using System;
using System.IO;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

internal static class AssemblyCacheCleanup
{
    internal static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"Assembly-Cache-Tempdatei konnte nicht entfernt werden: {ex.Message}");
        }
    }

    internal static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"Assembly-Cache-Generation konnte nicht entfernt werden: {ex.Message}");
        }
    }
}
