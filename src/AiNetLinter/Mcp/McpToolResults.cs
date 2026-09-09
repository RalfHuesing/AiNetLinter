#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;
using AiNetLinter.Mcp.Registration;
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
    internal const int CompositeWireBudgetBytes = 12 * 1024;
    internal const int CompositeSectionBudgetBytes = 4 * 1024;

    internal const string WorkspaceDiagnosticHint =
        "Einmal erneut versuchen; bleibt der Fehler bestehen, Datei pruefen — Compile-Fehler blockieren Symbolaufloesung.";

    internal const string NativePeAssemblyHint =
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

    internal static CallToolResult NativePeAssembly(
        string message,
        string? context = null) =>
        RecoverableWorkspaceDiagnostic(message, context, NativePeAssemblyHint);

    private static CallToolResult BuildResult(
        string code,
        string message,
        McpErrorParameters parameters,
        bool isError)
    {
        var text = LinterErrorFormatter.Format(code, message, parameters.Context, parameters.Hint);
        if (!string.IsNullOrWhiteSpace(parameters.FieldPath))
        {
            text += $"\n  fieldPath: {parameters.FieldPath}";
        }
        return new CallToolResult
        {
            IsError = isError,
            Content = new List<ContentBlock> { new TextContentBlock { Text = text } },
            StructuredContent = JsonSerializer.SerializeToElement(
                new McpErrorPayload(
                    code,
                    message,
                    parameters.Context,
                    parameters.Hint,
                    Recoverable: !isError,
                    parameters.TargetPath,
                    parameters.FieldPath),
                McpJsonOptions.Default),
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

    internal static CallToolResult NotConfigured(string? targetPath) =>
        Recoverable(
            LinterErrorCodes.NotConfigured,
            "Diese Operation ist fuer die Solution nicht konfiguriert: neben der Solution wurde keine ainetlinter-rules.json gefunden.",
            context: targetPath,
            hint: "ainetlinter-rules.json neben der adressierten Solution anlegen und den Aufruf erneut starten.");

    internal static CallToolResult TargetMismatch(string identifier) =>
        Recoverable(
            McpHandoffErrorCodes.TargetMismatch,
            $"Die Handoff-ID '{identifier}' gehört zu einem anderen Target.",
            context: identifier,
            hint: "Eine Handoff-ID aus demselben targetPath verwenden.");

    internal static CallToolResult StaleSnapshot(string identifier) =>
        Recoverable(
            McpHandoffErrorCodes.StaleSnapshot,
            $"Die Handoff-ID '{identifier}' gehört zu einem veralteten Analyse-Snapshot.",
            context: identifier,
            hint: "Das Symbol im aktuellen Snapshot erneut mit 'find_symbol' suchen.");

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
            Content = new List<ContentBlock> { new TextContentBlock { Text = text } },
        };
    }

    /// <summary>
    /// Wie <see cref="Text(string)"/>, ergaenzt zusaetzlich <see cref="CallToolResult.StructuredContent"/>
    /// (MCP-Protokoll-Feature) — additiv, ohne den Text-Vertrag zu aendern (Structured-Output-Mode).
    /// <paramref name="payload"/> wird ueber <see cref="McpJsonOptions.Default"/> serialisiert, damit
    /// alle Tools dieselben CamelCase-/Kompakt-Optionen teilen (Pattern mit
    /// <see cref="Tools.SafeguardTool"/>).
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
    /// Wendet das gemeinsame UTF-8-Wirebudget fuer Source-Composites an. Die Projektion arbeitet
    /// ausschliesslich auf den bereits erzeugten StructuredContent-Objekten: Root-Felder bleiben
    /// erhalten, Listen werden deterministisch vom Ende her gekuerzt und es werden keine IDs oder
    /// Fortsetzungstoken erzeugt.
    /// </summary>
    internal static CallToolResult ApplyCompositeWireBudget(
        CallToolResult result,
        IReadOnlyList<string> sectionNames,
        string? rootSectionName = null)
    {
        if (result.StructuredContent is not { ValueKind: JsonValueKind.Object } structured)
        {
            return result;
        }

        var payload = JsonNode.Parse(structured.GetRawText()) as JsonObject;
        if (payload is null) return result;

        var originalText = ReadText(result);
        var truncatedSections = new SortedSet<string>(StringComparer.Ordinal);
        var rootTruncated = false;
        var textLimit = CompositeWireBudgetBytes / 2;

        foreach (var sectionName in sectionNames)
        {
            var section = FindCompositeSection(payload, sectionName, rootSectionName);
            if (section is null) continue;

            while (MeasureSectionNode(section, string.Equals(sectionName, rootSectionName, StringComparison.Ordinal)) > CompositeSectionBudgetBytes)
            {
                if (TryTrimSectionOnce(section))
                {
                    MarkSectionTruncated(section, sectionName);
                    truncatedSections.Add(sectionName);
                    continue;
                }

                if (rootSectionName != sectionName)
                {
                    payload[sectionName] = CreateTruncatedSection(sectionName);
                    MarkSectionTruncated((JsonObject)payload[sectionName]!, sectionName);
                    truncatedSections.Add(sectionName);
                }

                break;
            }
        }

        if (truncatedSections.Count > 0)
        {
            MarkRootTruncated(payload, truncatedSections);
            rootTruncated = true;
        }

        var projectedText = originalText;
        for (var attempt = 0; attempt < 4096; attempt++)
        {
            var hint = BuildCompositeBudgetHint(truncatedSections, rootTruncated);
            projectedText = truncatedSections.Count > 0 || rootTruncated
                ? BuildBudgetSafeText(payload, sectionNames, rootSectionName, hint, textLimit)
                : originalText;

            var candidate = ReplaceText(
                ReplaceStructured(result, JsonSerializer.SerializeToElement(payload, McpJsonOptions.Default)),
                projectedText);
            candidate = UpdateCompositeWireBudget(
                candidate,
                payload,
                sectionNames,
                rootSectionName,
                truncatedSections.Count > 0 || rootTruncated);

            var measurement = MeasureComposite(candidate);
            if (measurement.TotalBytes <= CompositeWireBudgetBytes
                && SectionsFit(payload, sectionNames, rootSectionName))
            {
                return candidate;
            }

            if (TryTrimLargestSection(payload, sectionNames, rootSectionName, truncatedSections))
            {
                MarkRootTruncated(payload, truncatedSections);
                rootTruncated = true;
                continue;
            }

            var textBudget = Math.Max(1, CompositeWireBudgetBytes - measurement.StructuredBytes);
            textLimit = Math.Min(textLimit, textBudget);
            rootTruncated = true;
            MarkRootTruncated(payload, truncatedSections);
            var nextText = BuildBudgetSafeText(
                payload,
                sectionNames,
                rootSectionName,
                BuildCompositeBudgetHint(truncatedSections, true),
                textLimit);
            if (nextText == projectedText && measurement.TotalBytes > CompositeWireBudgetBytes)
            {
                return candidate;
            }
        }

        return UpdateCompositeWireBudget(
            ReplaceText(
                ReplaceStructured(result, JsonSerializer.SerializeToElement(payload, McpJsonOptions.Default)),
                projectedText),
            payload,
            sectionNames,
            rootSectionName,
            truncatedSections.Count > 0 || rootTruncated);
    }

    private static JsonObject? FindCompositeSection(JsonObject payload, string sectionName, string? rootSectionName) =>
        string.Equals(sectionName, rootSectionName, StringComparison.Ordinal)
            ? payload
            : payload[sectionName] as JsonObject;

    private static bool SectionsFit(
        JsonObject payload,
        IReadOnlyList<string> sectionNames,
        string? rootSectionName) =>
        sectionNames
            .Select(name => FindCompositeSection(payload, name, rootSectionName))
            .Where(section => section is not null)
            .All(section => MeasureSectionNode(
                section!,
                ReferenceEquals(section, payload)) <= CompositeSectionBudgetBytes);

    private static bool TryTrimLargestSection(
        JsonObject payload,
        IReadOnlyList<string> sectionNames,
        string? rootSectionName,
        ISet<string> truncatedSections)
    {
        var sections = sectionNames
            .Select(name => (Name: name, Node: FindCompositeSection(payload, name, rootSectionName)))
            .Where(item => item.Node is not null)
            .OrderByDescending(item => MeasureSectionNode(
                item.Node!,
                ReferenceEquals(item.Node, payload)))
            .ThenBy(item => item.Name, StringComparer.Ordinal)
            .ToList();

        foreach (var item in sections)
        {
            var section = item.Node!;
            if (TryTrimSectionOnce(section))
            {
                MarkSectionTruncated(section, item.Name);
                truncatedSections.Add(item.Name);
                return true;
            }

            if (!string.Equals(item.Name, rootSectionName, StringComparison.Ordinal))
            {
                payload[item.Name] = CreateTruncatedSection(item.Name);
                MarkSectionTruncated((JsonObject)payload[item.Name]!, item.Name);
                truncatedSections.Add(item.Name);
                return true;
            }
        }

        return false;
    }

    private static bool TryTrimSectionOnce(JsonNode section)
    {
        var arrays = new List<ArrayCandidate>();
        CollectArrays(section, "$", arrays);
        var candidate = arrays
            .Where(item => item.Array.Count > 0)
            .OrderByDescending(item => item.LargestItemBytes)
            .ThenBy(item => item.Path, StringComparer.Ordinal)
            .FirstOrDefault();
        if (candidate is not null)
        {
            candidate.Array.RemoveAt(candidate.Array.Count - 1);
            return true;
        }

        var strings = new List<StringCandidate>();
        CollectStrings(section, "$", strings);
        var stringCandidate = strings
            .Where(item => item.Value.Length > 16 && !IsProtectedWireString(item.Key))
            .OrderByDescending(item => Encoding.UTF8.GetByteCount(item.Value))
            .ThenBy(item => item.Path, StringComparer.Ordinal)
            .FirstOrDefault();
        if (stringCandidate is not null)
        {
            var currentBytes = Encoding.UTF8.GetByteCount(stringCandidate.Value);
            stringCandidate.Parent[stringCandidate.Key] = TrimUtf8(
                stringCandidate.Value,
                Math.Max(1, currentBytes / 2));
            return true;
        }

        return false;
    }

    private static void CollectArrays(JsonNode? node, string path, ICollection<ArrayCandidate> result)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var property in obj)
                {
                    if (property.Key is "wireBudget" or "truncatedBy") continue;
                    CollectArrays(property.Value, path + "." + property.Key, result);
                }

                break;
            case JsonArray array:
                var largestItemBytes = array
                    .Select(item => item is null ? 0 : MeasureNode(item))
                    .DefaultIfEmpty(0)
                    .Max();
                result.Add(new ArrayCandidate(array, path, largestItemBytes));
                for (var index = 0; index < array.Count; index++)
                {
                    CollectArrays(array[index], path + "[" + index + "]", result);
                }

                break;
        }
    }

    private static void CollectStrings(JsonNode? node, string path, ICollection<StringCandidate> result)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var property in obj)
                {
                    if (property.Key == "wireBudget") continue;
                    if (property.Value is JsonValue value
                        && value.TryGetValue<string>(out var stringValue)
                        && stringValue is not null)
                    {
                        result.Add(new StringCandidate(obj, property.Key, stringValue, path + "." + property.Key));
                    }

                    CollectStrings(property.Value, path + "." + property.Key, result);
                }

                break;
            case JsonArray array:
                for (var index = 0; index < array.Count; index++)
                {
                    CollectStrings(array[index], path + "[" + index + "]", result);
                }

                break;
        }
    }

    private static bool IsProtectedWireString(string key) =>
        key.Contains("id", StringComparison.OrdinalIgnoreCase)
        || key.Contains("symbol", StringComparison.OrdinalIgnoreCase)
        || key is "completeness" or "status" or "nextStep" or "semantics" or "evidenceBoundary";

    private static JsonObject CreateTruncatedSection(string sectionName) =>
        new()
        {
            ["completeness"] = "truncated",
            ["isTruncated"] = true,
            ["truncatedBy"] = new JsonArray("responseBudget"),
            ["nextStep"] = BuildSectionNextStep(sectionName),
        };

    private static void MarkSectionTruncated(JsonObject section, string sectionName)
    {
        section["completeness"] = "truncated";
        section["isTruncated"] = true;
        if (section.ContainsKey("status")) section["status"] = "truncated";
        AddReason(section, "responseBudget");
        section["nextStep"] = BuildSectionNextStep(sectionName, ReadString(section, "nextStep"));
    }

    private static void MarkRootTruncated(JsonObject payload, IEnumerable<string> sectionNames)
    {
        var current = ReadString(payload, "completeness");
        if (current is null or "complete" or "empty" or "truncated")
        {
            payload["completeness"] = "truncated";
        }

        payload["isTruncated"] = true;
        AddReason(payload, "responseBudget");
        var sections = string.Join(", ", sectionNames.OrderBy(name => name, StringComparer.Ordinal));
        payload["nextStep"] =
            (sections.Length == 0
                ? "Wire-Budget der Gesamtantwort erreicht"
                : $"Wire-Budget für {sections} erreicht")
            + ": gezielte Detailabfrage mit kleinerem Scope oder engerem Limit wiederholen.";
    }

    private static void AddReason(JsonObject owner, string reason)
    {
        var reasons = owner["truncatedBy"] as JsonArray;
        if (reasons is null)
        {
            reasons = new JsonArray();
            owner["truncatedBy"] = reasons;
        }

        if (!reasons.Any(item => item is JsonValue value
                && value.TryGetValue<string>(out var current)
                && string.Equals(current, reason, StringComparison.Ordinal)))
        {
            reasons.Add(reason);
        }
    }

    private static string BuildSectionNextStep(string sectionName, string? existing = null) =>
        string.IsNullOrWhiteSpace(existing) || existing.Contains("responseBudget", StringComparison.OrdinalIgnoreCase)
            ? $"Abschnitt {sectionName}: Wire-Budget erreicht; Detailabfrage mit kleinerem Scope oder engerem Limit wiederholen."
            : $"{existing} Wire-Budget für Abschnitt {sectionName} erreicht; Detailabfrage gezielt wiederholen.";

    private static string BuildCompositeBudgetHint(ISet<string> sections, bool rootTruncated)
    {
        var affected = sections.Count == 0
            ? "Gesamtantwort"
            : string.Join(", ", sections.OrderBy(section => section, StringComparer.Ordinal));
        return $"- **Wire-Budget:** UTF-8-Payload gekürzt (Gesamtlimit {CompositeWireBudgetBytes} Bytes, Abschnittslimit {CompositeSectionBudgetBytes} Bytes); betroffene Abschnitte: {affected}; Begrenzung: `responseBudget`. " +
               "**Nächster sicherer Schritt:** gezielte Detailabfrage mit kleinerem Scope oder engerem Limit wiederholen.";
    }

    private static string AppendBudgetHint(string originalText, string hint, int maxBytes)
    {
        var suffix = "\n\n" + hint;
        var suffixBytes = Encoding.UTF8.GetByteCount(suffix);
        if (maxBytes == int.MaxValue) return originalText.TrimEnd() + suffix;
        if (suffixBytes >= maxBytes) return TrimUtf8(suffix, maxBytes);

        var prefix = TrimUtf8(originalText, maxBytes - suffixBytes);
        return prefix.TrimEnd() + suffix;
    }

    private static string BuildBudgetSafeText(
        JsonObject payload,
        IReadOnlyList<string> sectionNames,
        string? rootSectionName,
        string hint,
        int maxBytes)
    {
        var builder = new StringBuilder();
        var title = (payload["declaration"] as JsonObject)?["name"]?.GetValue<string>()
            ?? payload["targetSymbol"]?.GetValue<string>();
        builder.AppendLine(title is null
            ? "# Composite-Antwort (Wire-Budget)"
            : $"# Composite-Antwort (Wire-Budget): {title}");
        if (payload["navigation"] is JsonObject navigation)
        {
            var operationStatus = ReadString(navigation, "operationStatus") ?? "ok";
            var completeness = ReadString(navigation, "completeness") ?? "complete";
            builder.AppendLine($"- **Navigation:** Status: {operationStatus}; Completeness: {completeness}");
            if (navigation["next"] is JsonObject next)
            {
                var nextKind = ReadString(next, "kind");
                var nextAction = ReadString(next, "action");
                if (!string.IsNullOrWhiteSpace(nextKind) || !string.IsNullOrWhiteSpace(nextAction))
                {
                    builder.AppendLine($"- **Navigation next:** {nextKind ?? "none"} — {nextAction ?? ""}");
                }
            }
        }
        foreach (var sectionName in sectionNames)
        {
            var section = FindCompositeSection(payload, sectionName, rootSectionName);
            if (section is null) continue;

            var status = ReadString(section, "completeness")
                ?? ReadString(section, "status")
                ?? "complete";
            builder.AppendLine($"- **Abschnitt {sectionName}:** Status: {status}");
            AppendCountSummary(builder, section, sectionName);
            var nextStep = ReadString(section, "nextStep");
            if (!string.IsNullOrWhiteSpace(nextStep))
            {
                builder.AppendLine($"- **Nächster sicherer Schritt ({sectionName}):** {nextStep}");
            }
        }

        builder.AppendLine(hint);
        return TrimUtf8(builder.ToString().TrimEnd(), maxBytes);
    }

    private static void AppendCountSummary(StringBuilder builder, JsonObject section, string sectionName)
    {
        if (sectionName == "callers")
        {
            builder.AppendLine($"- **Counts:** {CountArray(section, "callSites")} von {ReadInt(section, "totalCallers", 0)} statischen Referenzen/Call-Sites zurückgegeben.");
            return;
        }

        if (sectionName == "violations")
        {
            var status = ReadString(section, "status") ?? ReadString(section, "completeness") ?? "complete";
            if (status is not ("complete" or "empty" or "truncated"))
            {
                builder.AppendLine($"- **Counts:** nicht entscheidbar (Status: {status}; keine Sauberkeitsaussage).");
                return;
            }

            var total = ReadInt(section, "totalViolationsOnFile", 0);
            builder.AppendLine($"- **Counts:** {CountArray(section, "violations")} von {total} Verstoesse; {ReadInt(section, "violationsOnSymbol", 0)} direkt auf dem Symbol.");
            builder.AppendLine($"- ({total} Verstoesse; Status: {status}).");
            return;
        }

        var counts = sectionName == "testContext"
            ? $"{ReadInt(section, "returnedTestFiles", CountArray(section, "testFiles"))} von {ReadInt(section, "totalTestFiles", 0)} Testdateien und {ReadInt(section, "returnedTestMethods", ReadInt(section, "displayedTestMethods", 0))} von {ReadInt(section, "totalMatchingTests", 0)} Testmethoden"
            : $"{ReadInt(section, "returnedCount", CountArray(section, "callSites", "violations"))} sichtbare Treffer von {ReadInt(section, "totalCount", 0)}";
        builder.AppendLine($"- **Counts:** {counts}.");
    }

    private static int ReadInt(JsonObject owner, string propertyName, int fallback) =>
        owner[propertyName] is JsonValue value && value.TryGetValue<int>(out var number)
            ? number
            : fallback;

    private static int CountArray(JsonObject owner, params string[] propertyNames) =>
        propertyNames.Select(name => owner[name] as JsonArray).FirstOrDefault(array => array is not null)?.Count ?? 0;

    private static CallToolResult UpdateCompositeWireBudget(
        CallToolResult result,
        JsonObject payload,
        IReadOnlyList<string> sectionNames,
        string? rootSectionName,
        bool truncated)
    {
        var candidate = result;
        for (var attempt = 0; attempt < 16; attempt++)
        {
            var provisional = ReplaceStructured(
                candidate,
                JsonSerializer.SerializeToElement(payload, McpJsonOptions.Default));
            var measurement = MeasureComposite(provisional);
            payload["wireBudget"] = new JsonObject
            {
                ["limitBytes"] = CompositeWireBudgetBytes,
                ["sectionLimitBytes"] = CompositeSectionBudgetBytes,
                ["textBytes"] = measurement.TextBytes,
                ["structuredBytes"] = measurement.StructuredBytes,
                ["totalBytes"] = 0,
                ["truncated"] = truncated,
                ["sections"] = BuildSectionBudgetMetadata(payload, sectionNames, rootSectionName),
            };

            var next = ReplaceStructured(
                provisional,
                JsonSerializer.SerializeToElement(payload, McpJsonOptions.Default));
            var finalMeasurement = MeasureComposite(next);
            if (payload["wireBudget"] is JsonObject wireBudget)
            {
                wireBudget["textBytes"] = finalMeasurement.TextBytes;
                wireBudget["structuredBytes"] = finalMeasurement.StructuredBytes;
                wireBudget["totalBytes"] = finalMeasurement.TotalBytes;
            }

            next = ReplaceStructured(
                next,
                JsonSerializer.SerializeToElement(payload, McpJsonOptions.Default));
            if (next.StructuredContent?.GetRawText() == candidate.StructuredContent?.GetRawText())
            {
                return next;
            }

            candidate = next;
        }

        return candidate;
    }

    private static CallToolResult ReapplyCompositeWireBudgetAfterNavigation(CallToolResult result)
    {
        if (result.StructuredContent is not { ValueKind: JsonValueKind.Object } structured)
        {
            return result;
        }

        var payload = JsonNode.Parse(structured.GetRawText()) as JsonObject;
        if (payload is null || payload["wireBudget"] is not JsonObject wireBudget)
        {
            return result;
        }

        var hasFeatureShape = payload.ContainsKey("callers") && payload.ContainsKey("testContext");
        var hasTestContextShape = !hasFeatureShape
            && payload.ContainsKey("testContext")
            && wireBudget["sections"] is JsonObject sections
            && sections.ContainsKey("testContext");
        return hasFeatureShape
            ? ApplyCompositeWireBudget(result, ["declaration", "metrics", "callers", "testContext", "violations"])
            : hasTestContextShape
                ? ApplyCompositeWireBudget(result, ["testContext"], "testContext")
                : result;
    }

    private static JsonObject BuildSectionBudgetMetadata(
        JsonObject payload,
        IReadOnlyList<string> sectionNames,
        string? rootSectionName)
    {
        var sections = new JsonObject();
        foreach (var sectionName in sectionNames)
        {
            var section = FindCompositeSection(payload, sectionName, rootSectionName);
            if (section is null) continue;
            sections[sectionName] = new JsonObject
            {
                ["limitBytes"] = CompositeSectionBudgetBytes,
                ["bytes"] = MeasureSectionNode(section, ReferenceEquals(section, payload)),
                ["truncated"] = ReadString(section, "completeness") == "truncated"
                    || ReadBoolean(section, "isTruncated"),
            };
        }

        return sections;
    }

    private static string ReadText(CallToolResult result) =>
        string.Join("\n", result.Content.OfType<TextContentBlock>().Select(block => block.Text));

    private static string? ReadString(JsonObject owner, string propertyName) =>
        owner[propertyName] is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : null;

    private static bool ReadBoolean(JsonObject owner, string propertyName) =>
        owner[propertyName] is JsonValue value
        && value.TryGetValue<bool>(out var flag)
        && flag;

    private static int MeasureNode(JsonNode node) =>
        Encoding.UTF8.GetByteCount(node.ToJsonString(McpJsonOptions.Default));

    private static int MeasureSectionNode(JsonNode node, bool rootSection)
    {
        if (!rootSection || node is not JsonObject owner || !owner.ContainsKey("wireBudget"))
        {
            return MeasureNode(node);
        }

        var copy = JsonNode.Parse(owner.ToJsonString(McpJsonOptions.Default))!.AsObject();
        copy.Remove("wireBudget");
        return MeasureNode(copy);
    }

    private static CompositeMeasurement MeasureComposite(CallToolResult result)
    {
        var textBytes = result.Content
            .OfType<TextContentBlock>()
            .Sum(block => Encoding.UTF8.GetByteCount(block.Text));
        var structuredBytes = result.StructuredContent is { } structured
            ? Encoding.UTF8.GetByteCount(structured.GetRawText())
            : 0;
        return new CompositeMeasurement(textBytes, structuredBytes, textBytes + structuredBytes);
    }

    private static CallToolResult ReplaceStructured(CallToolResult result, JsonElement structured) =>
        new()
        {
            IsError = result.IsError,
            Content = result.Content,
            StructuredContent = structured,
        };

    private static CallToolResult ReplaceText(CallToolResult result, string text) =>
        new()
        {
            IsError = result.IsError,
            Content = result.Content
                .Select(block => block is TextContentBlock
                    ? new TextContentBlock { Text = text }
                    : block)
                .ToList(),
            StructuredContent = result.StructuredContent,
        };

    private static string TrimUtf8(string value, int maxBytes)
    {
        if (maxBytes <= 0) return string.Empty;
        if (Encoding.UTF8.GetByteCount(value) <= maxBytes) return value;

        var low = 0;
        var high = value.Length;
        while (low < high)
        {
            var middle = low + ((high - low + 1) / 2);
            if (Encoding.UTF8.GetByteCount(value.AsSpan(0, middle)) <= maxBytes)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }

        if (low > 0 && low < value.Length && char.IsHighSurrogate(value[low - 1])) low--;
        return value[..low];
    }

    private sealed record ArrayCandidate(JsonArray Array, string Path, int LargestItemBytes);

    private sealed record StringCandidate(JsonObject Parent, string Key, string Value, string Path);

    private readonly record struct CompositeMeasurement(int TextBytes, int StructuredBytes, int TotalBytes);

    /// <summary>
    /// Ergaenzt eine zielgebundene Antwort um den gemeinsamen Navigation-Kern. Die vorhandene
    /// tool-spezifische StructuredContent-Nutzlast bleibt dabei unveraendert am Root; dadurch
    /// bleiben bestehende Clients kompatibel, waehrend Agenten einen einheitlichen Handoff
    /// unter <c>navigation</c> erhalten.
    /// </summary>
    internal static CallToolResult WithNavigation(CallToolResult result, AnalysisTarget target)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(target);

        var navigation = McpNavigationProjection.Create(result, target);
        var payload = result.StructuredContent is { ValueKind: JsonValueKind.Object } structured
            ? JsonNode.Parse(structured.GetRawText()) as JsonObject ?? new JsonObject()
            : new JsonObject();
        var navigationNode = JsonSerializer.SerializeToNode(navigation, McpJsonOptions.Default) as JsonObject
            ?? new JsonObject();
        if (payload["navigation"] is JsonObject existingNavigation)
        {
            foreach (var property in navigationNode)
            {
                existingNavigation[property.Key] = property.Value?.DeepClone();
            }
        }
        else
        {
            payload["navigation"] = navigationNode;
        }

        var navigationText = McpNavigationText.Format(navigation);
        var text = result.Content
            .Select(block => block is TextContentBlock textBlock
                ? new TextContentBlock { Text = textBlock.Text.TrimEnd() + "\n\n" + navigationText }
                : block)
            .ToList();

        var navigated = new CallToolResult
        {
            IsError = result.IsError,
            Content = text,
            StructuredContent = JsonSerializer.SerializeToElement(payload, McpJsonOptions.Default),
        };
        return ReapplyCompositeWireBudgetAfterNavigation(navigated);
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

internal readonly record struct McpErrorParameters(
    string? Context = null,
    string? Hint = null,
    string? TargetPath = null,
    string? FieldPath = null);

/// <summary>
/// Typisierter Fehlervertrag fuer MCP-Antworten. Die Payload wird fuer harte und recoverable
/// Fehler identisch serialisiert; nur <see cref="CallToolResult.IsError"/> folgt weiterhin der
/// bestehenden IsError-Policy.
/// </summary>
internal sealed record McpErrorPayload(
    string Code,
    string Message,
    string? Context,
    string? Hint,
    bool Recoverable,
    string? TargetPath = null,
    string? FieldPath = null);

internal static class McpHandoffErrorCodes
{
    internal const string TargetMismatch = "TARGET_MISMATCH";
    internal const string StaleSnapshot = "STALE_SNAPSHOT";
}
