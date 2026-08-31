#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    internal static void RetainGenerations(string entryDirectory, string currentGeneration)
    {
        try
        {
            if (!Directory.Exists(entryDirectory)) return;
            var generations = Directory.EnumerateDirectories(
                    entryDirectory,
                    AssemblyCacheContract.GenerationDirectoryPrefix + "*",
                    SearchOption.TopDirectoryOnly)
                .Where(path => AssemblyCacheContract.IsSafeGenerationName(Path.GetFileName(path)))
                .OrderByDescending(Directory.GetLastWriteTimeUtc)
                .ThenByDescending(Path.GetFileName, StringComparer.Ordinal)
                .ToList();
            var retained = new HashSet<string>(StringComparer.Ordinal)
            {
                currentGeneration,
            };
            foreach (var generation in generations)
            {
                var name = Path.GetFileName(generation);
                if (retained.Contains(name))
                {
                    continue;
                }

                if (retained.Count < AssemblyCacheContract.MaxRetainedGenerations)
                {
                    retained.Add(name);
                    continue;
                }

                DeleteDirectory(generation);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Log.Warning(ex, "Assembly-Cache-Cleanup fehlgeschlagen: Art={CleanupKind}, Pfad={Path}", "Retention", entryDirectory);
        }
    }
}
