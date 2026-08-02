#nullable enable

using System.Collections.Generic;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp;

/// <summary>
/// Wiederverwendbare Hilfsmethoden zum Bauen von <see cref="CallToolResult"/>-Instanzen fuer
/// MCP-Tools — buendelt sowohl die Protokoll-Ebene (<see cref="CallToolResult.IsError"/>) als auch
/// das bestehende Text-Fehlerformat (<see cref="LinterErrorFormatter"/>), damit jedes Tool dasselbe
/// Boilerplate nicht einzeln nachbaut.
/// </summary>
internal static class McpToolResults
{
    internal static CallToolResult Error(string code, string message, string? context = null, string? hint = null)
    {
        var text = LinterErrorFormatter.Format(code, message, context, hint);
        return new CallToolResult
        {
            IsError = true,
            Content = new List<ContentBlock> { new TextContentBlock { Text = text } },
        };
    }

    internal static CallToolResult SolutionNotLoaded()
    {
        return Error(
            LinterErrorCodes.SolutionNotLoaded,
            "Solution ist nicht geladen — der MCP-Server konnte beim Start keine gueltige Solution laden.",
            hint: "Server-Log auf [WARN]-Zeilen zum Ladefehler pruefen.");
    }

    internal static CallToolResult SymbolNotFound(string identifier)
    {
        return Error(
            LinterErrorCodes.SymbolNotFound,
            $"Kein Symbol gefunden fuer Identifikator '{identifier}'.",
            context: identifier,
            hint: "Schreibweise pruefen oder 'find_symbol' zur Suche nutzen.");
    }

    internal static CallToolResult AmbiguousSymbol(string identifier, IEnumerable<string> candidateLines)
    {
        return Error(
            LinterErrorCodes.AmbiguousSymbol,
            $"Identifikator '{identifier}' ist mehrdeutig — mehrere Symbole gefunden.",
            context: string.Join("\n", candidateLines),
            hint: "Identifikator praezisieren (voll qualifizierter Name oder Datei:Zeile:Spalte).");
    }

    internal static CallToolResult InvalidArgument(string message)
    {
        return Error(
            LinterErrorCodes.InvalidArgument,
            message,
            hint: "Entweder gitRef ODER symbolIdentifier angeben, nie beide.");
    }

    internal static CallToolResult FileNotFound(string relativePath)
    {
        return Error(
            LinterErrorCodes.ResourceNotFound,
            $"Datei '{relativePath}' nicht in der Solution gefunden.",
            context: relativePath,
            hint: "Pfad relativ zum Solution-Verzeichnis angeben (Forward- oder Backslash), 'find_symbol' zur Orientierung nutzen.");
    }

    internal static CallToolResult Text(string text)
    {
        return new CallToolResult
        {
            Content = new List<ContentBlock> { new TextContentBlock { Text = text } },
        };
    }

    internal static CallToolResult CompilationError(string message, string? context = null)
    {
        return Error(
            LinterErrorCodes.WorkspaceDiagnostic,
            message,
            context: context,
            hint: "Datei pruefen — Compile-Fehler blockieren Symbolaufloesung.");
    }
}
