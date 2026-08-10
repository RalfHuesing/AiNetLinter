#nullable enable

using System.Collections.Generic;
using System.Text.Json;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp;

/// <summary>
/// Wiederverwendbare Hilfsmethoden zum Bauen von <see cref="CallToolResult"/>-Instanzen fuer
/// MCP-Tools — buendelt sowohl die Protokoll-Ebene (<see cref="CallToolResult.IsError"/>) als auch
/// das bestehende Text-Fehlerformat (<see cref="LinterErrorFormatter"/>), damit jedes Tool dasselbe
/// Boilerplate nicht einzeln nachbaut. Die Wahl zwischen <see cref="Error"/> (IsError=true) und
/// <see cref="Recoverable"/> (IsError=false) folgt der Policy in
/// <c>src/AiNetLinter/Mcp/IsErrorPolicy.md</c>: IsError=true ist reserviert fuer
/// SOLUTION_NOT_LOADED, Sicherheitsverweigerungen und echte Malfunctions (unerwartete
/// Exceptions) — alle anderen erwartbaren/recoverable Bedingungen (Symbol nicht gefunden,
/// mehrdeutiger Identifikator, ungueltiges Argument, Datei nicht gefunden) liefern
/// IsError=false mit derselben strukturierten Anleitung im Text, damit ein Agent sie nicht als
/// Tool-Ausfall interpretiert und das Tool vorzeitig aufgibt (CodeGraph-Lehre, siehe Policy-Doc).
/// </summary>
internal static class McpToolResults
{
    /// <summary>
    /// Baut ein Fehlerergebnis: <see cref="CallToolResult.IsError"/> ist <see langword="true"/>, der
    /// Text folgt dem bestehenden <c>[ERROR]</c>-Format aus <see cref="LinterErrorFormatter"/>. Nur
    /// fuer die drei in <c>IsErrorPolicy.md</c> definierten Faelle verwenden (SOLUTION_NOT_LOADED,
    /// Sicherheitsverweigerung, echte Malfunction) — fuer erwartbare/recoverable Bedingungen
    /// <see cref="Recoverable"/> nutzen.
    /// </summary>
    internal static CallToolResult Error(string code, string message, string? context = null, string? hint = null)
    {
        return BuildResult(code, message, context, hint, isError: true);
    }

    /// <summary>
    /// Baut ein Ergebnis fuer eine erwartbare/recoverable Bedingung (Symbol nicht gefunden,
    /// mehrdeutiger Identifikator, ungueltiges Argument, Datei nicht gefunden, ...):
    /// <see cref="CallToolResult.IsError"/> bleibt <see langword="false"/>, obwohl derselbe
    /// strukturierte <c>[ERROR]</c>-Text wie bei <see cref="Error"/> verwendet wird — der Agent
    /// soll den Aufruf als erfolgreich verarbeitet betrachten (mit Handlungsanleitung im Text),
    /// nicht als Tool-Ausfall. Siehe <c>IsErrorPolicy.md</c> fuer die vollstaendige Tabelle.
    /// </summary>
    internal static CallToolResult Recoverable(string code, string message, string? context = null, string? hint = null)
    {
        return BuildResult(code, message, context, hint, isError: false);
    }

    private static CallToolResult BuildResult(string code, string message, string? context, string? hint, bool isError)
    {
        var text = LinterErrorFormatter.Format(code, message, context, hint);
        return new CallToolResult
        {
            IsError = isError,
            Content = new List<ContentBlock> { new TextContentBlock { Text = text } },
        };
    }

    /// <summary>
    /// Kurzform fuer den in jedem Tool wiederkehrenden Fall, dass beim Serverstart keine Solution
    /// geladen werden konnte (<see cref="McpCodeGraphServer.IsLoaded"/> ist <see langword="false"/>).
    /// IsError=true (Policy-Kategorie SOLUTION_NOT_LOADED) — ohne Solution kann kein Tool sinnvoll
    /// antworten, das ist kein per Handlungsanleitung behebbarer Nutzerfehler.
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
    /// IsError=false (recoverable) — der Hinweis nennt den naechsten Schritt (find_symbol).
    /// </summary>
    internal static CallToolResult SymbolNotFound(string identifier)
    {
        return Recoverable(
            LinterErrorCodes.SymbolNotFound,
            $"Kein Symbol gefunden fuer Identifikator '{identifier}'.",
            context: identifier,
            hint: "Schreibweise pruefen oder 'find_symbol' zur Suche nutzen.");
    }

    /// <summary>
    /// Kurzform fuer den Fall, dass ein Symbol-Identifikator auf mehrere Symbole aufloest —
    /// <paramref name="candidateLines"/> listet die Fundstellen (z. B. via
    /// <see cref="Tools.FindSymbolTool.FormatSymbolLocations"/>) als Entscheidungshilfe.
    /// IsError=false (recoverable) — die Kandidatenliste selbst ist die Handlungsanleitung.
    /// </summary>
    internal static CallToolResult AmbiguousSymbol(string identifier, IEnumerable<string> candidateLines)
    {
        return Recoverable(
            LinterErrorCodes.AmbiguousSymbol,
            $"Identifikator '{identifier}' ist mehrdeutig — mehrere Symbole gefunden.",
            context: string.Join("\n", candidateLines),
            hint: "Identifikator praezisieren (voll qualifizierter Name oder Datei:Zeile:Spalte).");
    }

    /// <summary>
    /// Kurzform fuer den Fall, dass ein Tool-Aufruf gegenseitig exklusive Parameter verletzt
    /// (z. B. <c>get_impact</c>s <c>gitRef</c> und <c>symbolIdentifier</c> beide gesetzt).
    /// IsError=false (recoverable) — ein Nutzer-/Agentenfehler bei den Argumenten, kein
    /// Tool-Ausfall.
    /// </summary>
    internal static CallToolResult InvalidArgument(string message)
    {
        return Recoverable(
            LinterErrorCodes.InvalidArgument,
            message,
            hint: "Entweder gitRef ODER symbolIdentifier angeben, nie beide.");
    }

    /// <summary>
    /// Kurzform fuer den Fall, dass ein per Dateipfad angegebenes Tool-Argument (z. B.
    /// <c>get_file_skeleton</c>s <c>filePath</c>) auf kein <see cref="Microsoft.CodeAnalysis.Document"/>
    /// in der Solution aufloest. IsError=false (recoverable) — Pfad korrigieren oder find_symbol
    /// zur Orientierung nutzen.
    /// </summary>
    internal static CallToolResult FileNotFound(string relativePath)
    {
        return Recoverable(
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
    /// Wie <see cref="Text(string)"/>, ergaenzt zusaetzlich <see cref="CallToolResult.StructuredContent"/>
    /// (MCP-Protokoll-Feature) — additiv, ohne den bisherigen Text-Vertrag zu aendern (S1.3
    /// Structured-Output-Mode). <paramref name="payload"/> wird ueber <see cref="McpJsonOptions.Default"/>
    /// serialisiert, damit alle Tools dieselben CamelCase-/Kompakt-Optionen teilen (Pattern von
    /// <see cref="Tools.SafeguardTool"/> uebernommen, dort urspruenglich als Erstes eingefuehrt).
    /// Clients, die nur Text konsumieren, ignorieren das zusaetzliche Feld einfach.
    /// WICHTIG: <paramref name="payload"/> muss zu einem JSON-Objekt serialisieren, niemals zu
    /// einem Top-Level-Array/einer nackten Liste — das MCP-Protokoll verlangt
    /// <c>structuredContent</c> als Objekt, reale Clients lehnen den kompletten Tool-Call
    /// schema-seitig ab, wenn ein Array ankommt. Eine Liste immer in ein benanntes Objekt wrappen,
    /// z. B. <c>new { Violations = list }</c> statt <c>list</c> direkt zu uebergeben.
    /// </summary>
    internal static CallToolResult Text<T>(string text, T payload)
    {
        return new CallToolResult
        {
            Content = new List<ContentBlock> { new TextContentBlock { Text = text } },
            StructuredContent = JsonSerializer.SerializeToElement(payload, McpJsonOptions.Default),
        };
    }

    /// <summary>
    /// Kurzform fuer eine echte Malfunction: ein unerwarteter Roslyn-/Laufzeit-Fehler wurde in
    /// einem defensiven try/catch abgefangen (z. B. Symbol existiert nur in einer fehlerhaften
    /// Datei und Roslyn kann es nicht aufloesen). IsError=true (Policy-Kategorie "echte
    /// Malfunction") — anders als SYMBOL_NOT_FOUND/AMBIGUOUS_SYMBOL/INVALID_ARGUMENT ist das
    /// kein erwartbarer Nutzerfehler, sondern ein Grenzfall, den der Aufrufer nicht durch
    /// praezisere Argumente vermeiden kann. Hint enthaelt bewusst den Retry-once-Hinweis aus der
    /// Policy: ein einmaliger erneuter Versuch klaert transiente Faelle, bevor die Datei
    /// inspiziert werden muss. Liefert ein <c>[ERROR]: WORKSPACE_DIAGNOSTIC</c>-Ergebnis mit dem
    /// bestehenden <see cref="LinterErrorCodes.WorkspaceDiagnostic"/>-Code (wiederverwendet, nicht
    /// neu angelegt — Duplikat-Vermeidung).
    /// </summary>
    internal static CallToolResult CompilationError(string message, string? context = null)
    {
        return Error(
            LinterErrorCodes.WorkspaceDiagnostic,
            message,
            context: context,
            hint: "Einmal erneut versuchen; bleibt der Fehler bestehen, Datei pruefen — Compile-Fehler blockieren Symbolaufloesung.");
    }

    /// <summary>
    /// Antwort fuer den transienten Wartezustand, in dem der MCP-Server gerade die Solution
    /// im Hintergrund laedt. Bewusst kein <see cref="CallToolResult.IsError"/>, weil der
    /// Tool-Aufruf nicht falsch war — der Server braucht nur wenige Sekunden, bis die
    /// Loesung resident ist. Clients (MCP-Hosts wie Claude Desktop, eigene Test-Harness)
    /// erkennen den Text und koennen den Aufruf nach kurzer Pause wiederholen.
    /// </summary>
    internal static CallToolResult Loading()
    {
        return new CallToolResult
        {
            IsError = false,
            Content = new List<ContentBlock>
            {
                new TextContentBlock
                {
                    Text = "[INFO]: Server laedt die Solution noch. " +
                           "Bitte in wenigen Sekunden erneut versuchen.",
                },
            },
        };
    }
}
