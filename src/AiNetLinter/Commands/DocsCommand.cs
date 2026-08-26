#nullable enable

using System;
using System.Collections.Generic;
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
        { "rules-json", "rules.json" },
        { "mcp-bootstrap", "Docs/mcp-bootstrap.md" },
        { "mcp-rule", "AgentRules/AiNetLinter-McpWorkflow.mdc" },
        // Kompatibilitätsalias für den bisherigen Namen des Bootstrap-Leitfadens.
        { "mcp-workflow", "Docs/mcp-bootstrap.md" }
    };

    /// <summary>
    /// Gibt die angegebene eingebettete Markdown-Datei aus.
    /// </summary>
    internal static int Run(string? docName, ILintConsole? console = null)
    {
        var c = console ?? LinterConsole.Instance;

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

        var text = EmbeddedResourceReader.TryRead(resourceName);
        if (text is null)
        {
            c.WriteError($"[ERROR]: '{resourceName}' wurde nicht als eingebettete Ressource gefunden.");
            return 1;
        }

        c.WriteLine(text);
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
