#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AiNetLinter.Output;

namespace AiNetLinter.Commands;

/// <summary>
/// Gibt die eingebettete Dokumentation auf der Konsole aus.
/// </summary>
internal static class DocsCommand
{
    private static readonly Dictionary<string, string> DocResources = new(StringComparer.OrdinalIgnoreCase)
    {
        { "integration", "Docs/integration.md" },
        { "readme", "README.md" },
        { "agent-api", "Docs/agent-api.md" },
        { "configuration", "Docs/configuration.md" },
        { "rationale", "Docs/rationale.md" },
        { "roadmap", "Docs/ROADMAP.md" },
        { "rules-json", "rules.json" }
    };

    /// <summary>
    /// Gibt die angegebene eingebettete Markdown-Datei aus.
    /// </summary>
    internal static int Run(string? docName, ILintConsole? console = null)
    {
        var c = console ?? ConsoleLintConsole.Instance;

        if (string.IsNullOrWhiteSpace(docName))
        {
            c.WriteError("[ERROR]: --docs benötigt den Namen eines Dokuments.");
            PrintAvailableDocs(c);
            return 1;
        }

        var key = docName.Trim();
        if (!DocResources.TryGetValue(key, out var resourceName))
        {
            c.WriteError($"[ERROR]: Dokumentation '{docName}' wurde nicht gefunden.");
            PrintAvailableDocs(c);
            return 1;
        }

        using var stream = typeof(DocsCommand).Assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            c.WriteError($"[ERROR]: '{resourceName}' wurde nicht als eingebettete Ressource gefunden.");
            return 1;
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        c.WriteLine(reader.ReadToEnd());
        return 0;
    }

    private static void PrintAvailableDocs(ILintConsole c)
    {
        c.WriteLine("Verfügbare Dokumente:");
        foreach (var key in DocResources.Keys)
        {
            c.WriteLine($"- {key}");
        }
    }
}
