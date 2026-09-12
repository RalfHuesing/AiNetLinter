#nullable enable

using System;
using System.Collections.Generic;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp;
using AiNetLinter.Output;

namespace AiNetLinter.Commands;

/// <summary>
/// Gibt die eingebettete Dokumentation auf der Konsole aus.
/// </summary>
internal static class DocsCommand
{
    private const string McpBootstrapDocument = "mcp-bootstrap";

    private static readonly Dictionary<string, string> DocResources = new(StringComparer.OrdinalIgnoreCase)
    {
        // Linter
        { "configuration", "Docs/linter/configuration.md" },
        { "linter-config", "Docs/linter/configuration.md" },
        { "cli", "Docs/linter/cli.md" },
        { "linter-cli", "Docs/linter/cli.md" },
        { "integration", "Docs/linter/integration.md" },
        { "linter-integration", "Docs/linter/integration.md" },

        // MCP
        { "agent-api", "Docs/mcp/tools.md" },
        { "mcp-tools", "Docs/mcp/tools.md" },
        { "mcp-server", "Docs/mcp/server.md" },
        { "mcp-integration", "Docs/mcp/integration.md" },
        { McpBootstrapDocument, "Docs/mcp/mcp-bootstrap.md" },

        // Allgemein & Regeln
        { "readme", "README.md" },
        { "rationale", "Docs/rationale.md" },
        { "ainetlinter-rules-json", ConfigLoader.FileName },
        { "mcp-rule", "AgentRules/AiNetLinter-McpWorkflow.mdc" }
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

        c.WriteLine(key.Equals(McpBootstrapDocument, StringComparison.OrdinalIgnoreCase)
            ? McpRegistrationInstructions.AppendRuntimeBlock(text)
            : text);
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
