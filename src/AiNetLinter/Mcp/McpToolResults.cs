#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AiNetLinter.Mcp.Wire;
using AiNetLinter.Mcp.Registration;
using AiNetLinter.Mcp.Handoffs;
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
internal static partial class McpToolResults
{
    private static readonly AgentContentRenderer ContentRenderer = new();

    internal const string WorkspaceDiagnosticHint =
        "Einmal erneut versuchen; bleibt der Fehler bestehen, Datei pruefen — Compile-Fehler blockieren Symbolaufloesung.";

    internal const string InvalidAssemblyHint =
        "Keine Wiederholung nötig: Das Ziel ist keine verwaltete .NET-Assembly mit IL; " +
        "eine passende .dll oder .exe mit .NET-Metadaten angeben.";

    /// <summary>Hinweis fuer fehlenden/leeren <c>symbolIdentifier</c> (Einzel-Symbol-Tools).</summary>
    internal const string SymbolIdentifierHint =
        "symbolIdentifier angeben: \"M:Namespace.Klasse.Methode\", \"Datei.cs:42:10\" oder \"Klasse.Methode\".";

    /// <summary>Hinweis fuer fehlende <c>symbolIdentifiers</c>-Batch-Argumente.</summary>
    internal const string SymbolIdentifiersBatchHint =
        "symbolIdentifiers: [\"M:Klasse.Methode\"] oder symbolIdentifiers: [\"M:Klasse.MethodeA\", \"M:Klasse.MethodeB\"].";

    /// <summary>Hinweis fuer fehlende <c>filePaths</c>-Batch-Argumente.</summary>
    internal const string FilePathsBatchHint =
        "filePaths: [\"src/MyClass.cs\"] oder filePaths: [\"src/ClassA.cs\", \"src/ClassB.cs\"].";

    /// <summary>Hinweis fuer fehlende <c>namePatterns</c>-Batch-Argumente.</summary>
    internal const string NamePatternsBatchHint =
        "namePatterns: [\"Greeter\"] oder namePatterns: [\"Greeter\", \"GreetingService\"].";

    /// <summary>
    /// Baut ein Fehlerergebnis: <see cref="CallToolResult.IsError"/> ist <see langword="true"/>, der
    /// Text folgt dem bestehenden <c>[ERROR]</c>-Format aus <see cref="LinterErrorFormatter"/>. Nur
    /// fuer die drei in <c>IsErrorPolicy.md</c> definierten Faelle verwenden (SOLUTION_NOT_LOADED,
    /// Sicherheitsverweigerung, echte Malfunction) — fuer erwartbare/recoverable Bedingungen
    /// <see cref="Recoverable"/> nutzen.
    /// </summary>
    internal static CallToolResult Error(
        string code,
        string message,
        string? context = null,
        string? hint = null)
    {
        return BuildResult(code, message, new McpErrorParameters(context, hint), isError: true);
    }

    internal static CallToolResult Error(string code, string message, McpErrorParameters parameters) =>
        BuildResult(code, message, parameters, isError: true);

    /// <summary>
    /// Baut ein Ergebnis fuer eine erwartbare/recoverable Bedingung (Symbol nicht gefunden,
    /// mehrdeutiger Identifikator, ungueltiges Argument, Datei nicht gefunden, ...):
    /// <see cref="CallToolResult.IsError"/> bleibt <see langword="false"/>, obwohl derselbe
    /// strukturierte <c>[ERROR]</c>-Text wie bei <see cref="Error"/> verwendet wird — der Agent
    /// soll den Aufruf als erfolgreich verarbeitet betrachten (mit Handlungsanleitung im Text),
    /// nicht als Tool-Ausfall. Siehe <c>IsErrorPolicy.md</c> fuer die vollstaendige Tabelle.
    /// </summary>
    internal static CallToolResult Recoverable(
        string code,
        string message,
        string? context = null,
        string? hint = null)
    {
        return BuildResult(code, message, new McpErrorParameters(context, hint), isError: false);
    }

    internal static CallToolResult Recoverable(string code, string message, McpErrorParameters parameters) =>
        BuildResult(code, message, parameters, isError: false);

    internal static CallToolResult RecoverableWorkspaceDiagnostic(
        string message,
        string? context = null,
        string? hint = null) =>
        Recoverable(
            LinterErrorCodes.WorkspaceDiagnostic,
            message,
            context: context,
            hint: hint ?? WorkspaceDiagnosticHint);

    internal static CallToolResult InvalidAssembly(
        string message,
        string? context = null) =>
        Recoverable(
            LinterErrorCodes.InvalidAssembly,
            message,
            context,
            InvalidAssemblyHint);

    internal static CallToolResult TargetUnreadable(
        string message,
        string? context = null) =>
        Recoverable(
            LinterErrorCodes.TargetUnreadable,
            message,
            context,
            "Ziel konnte nicht gelesen werden; Zugriffsrechte, Dateisperre und Lesbarkeit prüfen und danach erneut anfordern.");

    private static CallToolResult BuildResult(
        string code,
        string message,
        McpErrorParameters parameters,
        bool isError)
    {
        var safeMessage = LimitErrorValue(message) ?? string.Empty;
        var safeContext = LimitErrorValue(parameters.Context);
        var safeHint = LimitErrorValue(parameters.Hint);
        var text = LinterErrorFormatter.Format(code, safeMessage, safeContext, safeHint);
        if (!string.IsNullOrWhiteSpace(parameters.FieldPath))
        {
            text += $"\n  fieldPath: {parameters.FieldPath}";
        }
        if (parameters.MinimumResponseBytes is { } minimumResponseBytes)
        {
            text += $"\n  minimumResponseBytes: {minimumResponseBytes}";
            text += $"\n  retry: denselben Aufruf mit maxResponseBytes={minimumResponseBytes} wiederholen.";
        }
        return new CallToolResult
        {
            IsError = isError,
            Content = CreateTextContent(text),
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
        if (SymbolHandoffIdentifier.HasWirePrefix(identifier))
        {
            return Recoverable(
                LinterErrorCodes.SymbolNotFound,
                "Die angegebene Handoff-ID konnte im aktuellen Analyse-Snapshot nicht aufgelöst werden.",
                hint: "Das Symbol im aktuellen Snapshot erneut mit 'find_symbol' suchen.");
        }

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

    internal static CallToolResult NotConfigured(string? targetPath) =>
        Recoverable(
            LinterErrorCodes.NotConfigured,
            "Diese Operation ist fuer die Solution nicht konfiguriert: neben der Solution wurde keine ainetlinter-rules.json gefunden.",
            context: targetPath,
            hint: "ainetlinter-rules.json neben der adressierten Solution anlegen und den Aufruf erneut starten.");

    internal static CallToolResult TargetMismatch(string identifier) =>
        Recoverable(
            McpHandoffErrorCodes.TargetMismatch,
            "Die angegebene Handoff-ID gehört zu einem anderen Target.",
            hint: "Eine Handoff-ID aus demselben targetPath verwenden.");

    internal static CallToolResult StaleSnapshot(string identifier) =>
        Recoverable(
            McpHandoffErrorCodes.StaleSnapshot,
            "Die angegebene Handoff-ID gehört zu einem veralteten Analyse-Snapshot.",
            hint: "Das Symbol im aktuellen Snapshot erneut mit 'find_symbol' suchen.");

    private static string? LimitErrorValue(string? value)
    {
        const int maxLength = 256;
        return value is null || value.Length <= maxLength
            ? value
            : value[..maxLength] + "…";
    }

    /// <summary>
    /// Kurzform fuer den Fall, dass ein Tool-Aufruf ungueltige oder unvollstaendige Argumente enthaelt.
    /// IsError=false (recoverable) — ein Nutzer-/Agentenfehler bei den Argumenten, kein
    /// Tool-Ausfall.
    /// </summary>
    internal static CallToolResult InvalidArgument(
        string message,
        string? hint = null,
        string? fieldPath = null)
    {
        return Recoverable(
            LinterErrorCodes.InvalidArgument,
            message,
            new McpErrorParameters(
                Hint: hint ?? "Parameter pruefen und gemaess Spezifikation uebergeben.",
                FieldPath: fieldPath));
    }

    /// <summary>
    /// Kurzform fuer den Fall, dass ein per Dateipfad angegebenes Tool-Argument (z. B.
    /// <c>get_file_skeleton</c>s <c>filePaths</c>-Array) auf kein <see cref="Microsoft.CodeAnalysis.Document"/>
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

    internal static CallToolResult AmbiguousPath(string path, IEnumerable<string> candidates)
    {
        return Recoverable(
            LinterErrorCodes.InvalidArgument,
            $"Der Dokumentpfad '{path}' ist in der Solution nicht eindeutig.",
            context: string.Join("\n", candidates),
            hint: "Einen eindeutigen relativen Pfad einschließlich Projekt-/Verzeichnisanteil angeben.");
    }

    internal static CallToolResult Text(string text)
    {
        return new CallToolResult
        {
            Content = CreateTextContent(text),
        };
    }

    /// <summary>Kompatibler Aufruf fuer bestehende typisierte Tool-Payloads; nur der Text ist öffentlich.</summary>
    internal static CallToolResult Text<T>(string text, T payload)
    {
        return new CallToolResult
        {
            Content = CreateTextContent(text),
        };
    }

    internal static CallToolResult ReplaceText(CallToolResult result, string text) => new()
    {
        IsError = result.IsError,
        Content = CreateTextContent(text),
    };

    private static List<ContentBlock> CreateTextContent(string text)
    {
        var rendered = ContentRenderer.Render(new AgentContentRenderRequest(
            IsError: false,
            Evidence: [new AgentContentEvidence(text, IsRequired: true)]));
        return [new TextContentBlock { Text = rendered.Text }];
    }

    /// <summary>
    /// Ergaenzt eine zielgebundene Antwort um einen kompakten Navigationshinweis im Text.
    /// </summary>
    internal static CallToolResult WithNavigation(
        CallToolResult result,
        AnalysisTarget? target = null,
        int maxResponseBytes = 0,
        Func<CallToolResult, int, CallToolResult>? postNavigationResponseBudget = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        var navigated = ProjectNavigation(result, target);
        if (postNavigationResponseBudget is null || maxResponseBytes <= 0) return navigated;

        var budgeted = postNavigationResponseBudget(navigated, maxResponseBytes);
        return budgeted.IsError == true ? ProjectNavigation(budgeted, target) : budgeted;
    }

    internal static CallToolResult WithNavigation(
        CallToolResult result,
        string? targetPath)
    {
        ArgumentNullException.ThrowIfNull(result);
        return ProjectNavigation(result, null, targetPath);
    }

    private static CallToolResult ProjectNavigation(
        CallToolResult result,
        AnalysisTarget? target,
        string? targetPath = null)
    {
        var navigation = McpNavigationProjection.Create(result, target, targetPath);
        var navigationText = McpNavigationText.Format(navigation);
        return new CallToolResult
        {
            IsError = navigation.Status.Operation == "error",
            Content = AppendNavigationText(result.Content, navigationText),
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
    internal static CallToolResult CompilationError(
        string message,
        string? context = null,
        string? hint = null) =>
        CompilationError(message, new McpErrorParameters(context, hint));

    internal static CallToolResult CompilationError(string message, McpErrorParameters parameters)
    {
        var effectiveParameters = parameters.Hint is null
            ? parameters with
            {
                Hint = WorkspaceDiagnosticHint,
            }
            : parameters;
        return Error(
            LinterErrorCodes.WorkspaceDiagnostic,
            message,
            effectiveParameters);
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
            Content = CreateTextContent(
                "[INFO]: Server laedt die Solution noch. Bitte in wenigen Sekunden erneut versuchen."),
        };
    }
}

internal readonly record struct McpErrorParameters(string? Context = null, string? Hint = null, string? TargetPath = null, string? FieldPath = null, int? RequestedBytes = null, int? MinimumResponseBytes = null);

internal sealed record McpErrorPayload(string Code, string Message, string? Context, string? Hint, bool Recoverable, string? TargetPath = null, string? FieldPath = null, int? RequestedBytes = null, int? MinimumResponseBytes = null);

internal static class McpHandoffErrorCodes
{
    internal const string TargetMismatch = "TARGET_MISMATCH";
    internal const string StaleSnapshot = "STALE_SNAPSHOT";
}
