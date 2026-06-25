#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AiNetLinter.Evals;

/// <summary>
/// Lädt und konkateniert Spezifikations-Inhalte aus Dateien und Verzeichnissen.
/// Verzeichnisse: nur erste Ebene, nur .md-Dateien, alphabetisch sortiert.
/// Fehlt jede Quelle: standardisierter Fallback-Text für das LLM.
/// </summary>
internal static class SpecLoader
{
    private const string FallbackText =
        "> **Spezifikation fehlt.** Verlange die Projektdokumentation " +
        "(README, relevante Docs-Dateien) vom Nutzer und füge sie hier ein " +
        "bevor du mit dem Audit fortfährst.";

    internal static string Load(IReadOnlyList<string> specPaths)
    {
        if (specPaths.Count == 0)
            return FallbackText;

        var parts = new List<string>();

        foreach (var path in specPaths)
        {
            if (File.Exists(path))
            {
                parts.Add(File.ReadAllText(path, Encoding.UTF8).Trim());
            }
            else if (Directory.Exists(path))
            {
                var mdFiles = Directory
                    .EnumerateFiles(path, "*.md", SearchOption.TopDirectoryOnly)
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);

                foreach (var file in mdFiles)
                    parts.Add(File.ReadAllText(file, Encoding.UTF8).Trim());
            }
            // Nicht-existente Pfade werden stillschweigend übersprungen
        }

        return parts.Count > 0
            ? string.Join("\n\n---\n\n", parts)
            : FallbackText;
    }
}
