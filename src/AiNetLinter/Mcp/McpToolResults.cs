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
    /// <summary>
    /// Baut ein Fehlerergebnis: <see cref="CallToolResult.IsError"/> ist <see langword="true"/>, der
    /// Text folgt dem bestehenden <c>[ERROR]</c>-Format aus <see cref="LinterErrorFormatter"/>.
    /// </summary>
    internal static CallToolResult Error(string code, string message, string? context = null, string? hint = null)
    {
        var text = LinterErrorFormatter.Format(code, message, context, hint);
        return new CallToolResult
        {
            IsError = true,
            Content = new List<ContentBlock> { new TextContentBlock { Text = text } },
        };
    }

    /// <summary>
    /// Kurzform fuer den in jedem Tool wiederkehrenden Fall, dass beim Serverstart keine Solution
    /// geladen werden konnte (<see cref="McpCodeGraphServer.IsLoaded"/> ist <see langword="false"/>).
    /// </summary>
    internal static CallToolResult SolutionNotLoaded()
    {
        return Error(
            LinterErrorCodes.SolutionNotLoaded,
            "Solution ist nicht geladen — der MCP-Server konnte beim Start keine gueltige Solution laden.",
            hint: "Server-Log auf [WARN]-Zeilen zum Ladefehler pruefen.");
    }

    /// <summary>
    /// Kurzform fuer den Fall, dass ein Symbol-Identifikator (Datei:Zeile:Spalte oder
    /// qualifizierter/teil-qualifizierter Name) auf kein Symbol aufloest (z. B. <c>find_references</c>).
    /// </summary>
    internal static CallToolResult SymbolNotFound(string identifier)
    {
        return Error(
            LinterErrorCodes.SymbolNotFound,
            $"Kein Symbol gefunden fuer Identifikator '{identifier}'.",
            context: identifier,
            hint: "Schreibweise pruefen oder 'find_symbol' zur Suche nutzen.");
    }

    /// <summary>
    /// Kurzform fuer den Fall, dass ein Symbol-Identifikator auf mehrere Symbole aufloest —
    /// <paramref name="candidateLines"/> listet die Fundstellen (z. B. via
    /// <see cref="Tools.FindSymbolTool.FormatSymbolLocations"/>) als Entscheidungshilfe.
    /// </summary>
    internal static CallToolResult AmbiguousSymbol(string identifier, IEnumerable<string> candidateLines)
    {
        return Error(
            LinterErrorCodes.AmbiguousSymbol,
            $"Identifikator '{identifier}' ist mehrdeutig — mehrere Symbole gefunden.",
            context: string.Join("\n", candidateLines),
            hint: "Identifikator praezisieren (voll qualifizierter Name oder Datei:Zeile:Spalte).");
    }

    /// <summary>
    /// Kurzform fuer den Fall, dass ein Tool-Aufruf gegenseitig exklusive Parameter verletzt
    /// (z. B. <c>get_impact</c>s <c>gitRef</c> und <c>symbolIdentifier</c> beide gesetzt).
    /// </summary>
    internal static CallToolResult InvalidArgument(string message)
    {
        return Error(
            LinterErrorCodes.InvalidArgument,
            message,
            hint: "Entweder gitRef ODER symbolIdentifier angeben, nie beide.");
    }

    /// <summary>
    /// Kurzform fuer den Fall, dass ein per Dateipfad angegebenes Tool-Argument (z. B.
    /// <c>get_file_skeleton</c>s <c>filePath</c>) auf kein <see cref="Microsoft.CodeAnalysis.Document"/>
    /// in der Solution aufloest.
    /// </summary>
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

    /// <summary>
    /// Kurzform fuer den Fall, dass ein Tool wegen Compile-Fehlern gar nicht sinnvoll antworten
    /// kann (z. B. das angefragte Symbol existiert nur in einer fehlerhaften Datei und Roslyn kann
    /// es nicht aufloesen). Liefert ein <c>[ERROR]: WORKSPACE_DIAGNOSTIC</c>-Ergebnis mit dem
    /// bestehenden <see cref="LinterErrorCodes.WorkspaceDiagnostic"/>-Code (wiederverwendet, nicht
    /// neu angelegt — Duplikat-Vermeidung).
    /// </summary>
    internal static CallToolResult CompilationError(string message, string? context = null)
    {
        return Error(
            LinterErrorCodes.WorkspaceDiagnostic,
            message,
            context: context,
            hint: "Datei pruefen — Compile-Fehler blockieren Symbolaufloesung.");
    }
}
