---
title: MarkdownBuilder — Fluenter Dokument-Builder für MCP-/CLI-Markdown-Output
status: ready
type: konzept
last_updated: 2026-08-19
rules_dir: .agents/rules
project_kind: brownfield
estimated_scope: small
open_questions: []
---

# Konzept: Interner `MarkdownBuilder` — Fluenter Dokument-Builder

## 1. Motivation & Analyse-Ergebnis

### Ausgangslage (Stand 2026-08-19)

Das Projekt baut Markdown-Output an **10 Tabellen-Stellen** und **4 Code-Block-Stellen** mit rohem String-Gebastel zusammen — plus diverse Headings/Bullets/Key-Value-Lines, die alle dasselbe Anti-Pattern teilen (manuelles `sb.AppendLine("| " + … + " |")` ohne Escaping, ohne zentrale Format-Verantwortung).

Bei der ursprünglichen Konzept-Erstellung (Commit `8c648a02`) waren es 7 Tabellen- und 3 Code-Block-Stellen. Seitdem sind dazugekommen:

| Stelle | Pattern | Bemerkung |
|:---|:---|:---|
| `MetricsLookupFormatter.cs` Z.37 | Tabelle + Headings + Bullets + Key-Value-Lines | **komplett übersehen** — perfekter Showcase für Builder-Anwendungsbreite |
| `AgentRulesGenerator.cs` `AppendCompoundSuppressions` Z.182 | 5-Spalten-Tabelle | **übersehen** — einfache Tabelle, direkt abbildbar |
| `GetSymbolBodyTool.cs` Z.62 | single-line Code-Block | **übersehen** — direkt ersetzbar |
| `GetHotspotsScanner.cs` + `HotspotMapBuilder.cs` | delegieren an `HotspotSectionFormatter` | doppelte Aufrufer — siehe HotspotSectionFormatter-Entscheidung unten |

**Aktive Mängel (Escaping-Bugs):**

| Bug | Stelle | Auswirkung |
|:---|:---|:---|
| `v.Signature` mit `|` zerschießt Tabelle | `GetClassStructureTool.cs` `FormatMemberRow` | Tabelle defekt bei generics mit `where T : IFoo<X, Y>`-artigen Signaturen |
| `v.Details` unescaped | `GetViolationsScanner.cs:255` | Details mit `|`-Zeichen zerreißen die Tabelle |
| `FormatMemberRow` ist dead code nach Builder-Einführung | `GetClassStructureTool.cs` | Existiert nur, weil es keinen Builder gibt |

### Verzeichnis-Umbenennung

`tasks/markdown-table-builder/` → `tasks/markdown-builder/`. Begründung: der Builder ist nicht nur ein Tabellen-Helper, sondern ein **Dokument-Builder** (Tabellen + Headings + Bullets + Code-Blöcke + freie Zeilen). Der alte Name `markdown-table-builder` ist zu eng und würde später wieder irreführen. Konzept-Datei entsprechend `Konzept.md` → `konzept.md` (Konsistenz mit `tasks/metrics-lookup/konzept.md`).

### Entscheidungen (final)

| Frage | Entscheidung | Begründung |
|:---|:---|:---|
| Table-only oder allgemein? | **Allgemeiner `MarkdownBuilder`** | Tabellen + Code-Blöcke + Headings + Bullets + Key-Value-Lines teilen dasselbe Problem. Ein gemeinsamer Builder ist konsistenter. |
| Klassen-Name | **`MarkdownBuilder`** | Passt zur Projekt-Konvention (`DebtReportBuilder`, `HotspotMapBuilder`, `ViolationSummaryBuilder`). Kein `Table` im Namen — zu eng. |
| Verzeichnis-Name | **`tasks/markdown-builder/`** | Siehe oben. |
| Platzierung Code | **`src/AiNetLinter/Output/MarkdownBuilder.cs`** | Nah an bestehenden `*Formatter`/`*Builder`-Klassen; kein neuer Ordner. Namespace `AiNetLinter.Output`. |
| Fluent API? | **Ja** | `Heading()`, `Table(t => ...)`, `CodeBlock()`, `Line()`, `BulletList()`. Konsistent mit dem Builder-Pattern. |
| `AppendTo` vs. `Build()`? | **Beide** | `AppendTo(StringBuilder)` für Callsites (alle nutzen äußeren sb); `Build()` → string für Tests + Scanner/Formatter-Returns. Overhead: 0. |
| Externe Bibliothek | **Nein** | Parser, keine Builder. Dependency-Overhead. |
| Eingerückter Code-Block (`ViolationMarkdownFormatter.AppendViolationItem`) | **Nicht im Builder** | 2-Leerzeichen-Einrückung + zeilenweiser `TrimEnd` ist Darstellungslogik eines Violation-Items. Bleibt direkter sb-Code. |
| `HotspotSectionFormatter` | **Ersetzen** | Heading + Tabelle + Sortierung als atomare Einheit ist kein Wert an sich — Sortierung wandert in den Aufrufer. Sonst zwei parallele Tabellen-Wege. |
| Reihenfolge | **Builder → Tests → Bug-Fix-Stellen (Prio 1, 2, 3) → Restliche Callsites → Cleanup `HotspotSectionFormatter`** | Tests sichern Verhalten ab, bevor viele Callsites umgebaut werden. |
| Escaping-Strategie | **Hartcodiert (Pipe + CRLF → Space)** | CommonMark-Standard; aktuelle Aufrufer brauchen nichts anderes. Konfigurierbarkeit wäre YAGNI. |

### Netto-Effekt

- **Builder + Test-File:** ~120 Zeilen neuer Code
- **Ersparnis in 10 Callsites:** ~70–90 Zeilen entfallender Tabellen-Boilerplate
- **Bug-Fixes:** 2 aktive Escaping-Bugs (`v.Signature`, `v.Details`) automatisch behoben
- **`HotspotSectionFormatter` (44 Zeilen) gelöscht** — beide Aufrufer nutzen direkt `MarkdownBuilder`

---

## 2. API-Design (vollständig, für Coder-Agent)

### 2a. `ColumnAlign` Enum + `MarkdownTableBuilder`

```csharp
// Datei: src/AiNetLinter/Output/MarkdownBuilder.cs
// Namespace: AiNetLinter.Output

internal enum ColumnAlign { Left, Right, Center }

internal sealed class MarkdownTableBuilder
{
    private readonly List<(string Header, ColumnAlign Align)> _columns = new();
    private readonly List<string[]> _rows = new();

    internal MarkdownTableBuilder AddColumn(string header, ColumnAlign align = ColumnAlign.Left)
    {
        _columns.Add((header, align));
        return this;
    }

    internal MarkdownTableBuilder AddRow(params object?[] cells)
    {
        var row = new string[_columns.Count];
        for (int i = 0; i < _columns.Count; i++)
            row[i] = EscapeCell(i < cells.Length ? cells[i]?.ToString() ?? string.Empty : string.Empty);
        _rows.Add(row);
        return this;
    }

    internal void AppendTo(StringBuilder sb)
    {
        if (_columns.Count == 0) return;
        sb.Append("| ").Append(string.Join(" | ", _columns.Select(c => EscapeCell(c.Header)))).AppendLine(" |");
        sb.Append('|');
        foreach (var (_, align) in _columns)
            sb.Append(align switch
            {
                ColumnAlign.Right  => "---:|",
                ColumnAlign.Center => ":---:|",
                _                  => ":---|"
            });
        sb.AppendLine();
        foreach (var row in _rows)
            sb.Append("| ").Append(string.Join(" | ", row)).AppendLine(" |");
    }

    internal string Build()
    {
        var sb = new StringBuilder();
        AppendTo(sb);
        return sb.ToString();
    }

    internal static string EscapeCell(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "-";
        return text.Replace("\r", string.Empty).Replace("\n", " ").Replace("|", "\\|").Trim();
    }
}
```

> **Hinweis:** `EscapeCell` ist `internal static` — damit können Tests es direkt testen ohne Instanz. Für Header-Werte wird es ebenfalls aufgerufen (defensive Programmierung).

### 2b. `MarkdownBuilder` — Fluenter Dokument-Builder

```csharp
internal sealed class MarkdownBuilder
{
    private readonly StringBuilder _sb = new();

    // Headings
    internal MarkdownBuilder Heading(int level, string text)
    {
        _sb.Append(new string('#', level)).Append(' ').AppendLine(text);
        return this;
    }

    // Blank line / separator
    internal MarkdownBuilder BlankLine()
    {
        _sb.AppendLine();
        return this;
    }

    // Raw text line (no escaping — caller's responsibility)
    internal MarkdownBuilder Line(string text)
    {
        _sb.AppendLine(text);
        return this;
    }

    // Bullet list
    internal MarkdownBuilder BulletList(IEnumerable<string> items)
    {
        foreach (var item in items)
            _sb.AppendLine($"- {item}");
        return this;
    }

    // Code block (fenced)
    internal MarkdownBuilder CodeBlock(string language, string content)
    {
        _sb.AppendLine($"```{language}");
        _sb.Append(content);
        if (content.Length > 0 && content[^1] != '\n') _sb.AppendLine();
        _sb.AppendLine("```");
        return this;
    }

    // Table — callback-based so conditional columns work naturally
    internal MarkdownBuilder Table(Action<MarkdownTableBuilder> configure)
    {
        var table = new MarkdownTableBuilder();
        configure(table);
        table.AppendTo(_sb);
        return this;
    }

    // Output
    internal void AppendTo(StringBuilder sb) => sb.Append(_sb);
    internal string Build() => _sb.ToString();
}
```

> **Design-Entscheidung `Table(Action<MarkdownTableBuilder>)`:** Der Callback-Ansatz erlaubt bedingte Spalten (`if (isMultiFile) table.AddColumn(...)`) ohne dass der äußere Builder wissen muss, wie viele Spalten die Tabelle hat. Das ist der sauberste Weg für die bestehenden bedingten Spalten-Logiken.

> **`Line(string text)` vs. `Paragraph(string text)`:** `Line` ist bewusst neutral benannt — in den bestehenden Stellen wird plain text, blockquotes (`> ...`), bold (`**...**`) etc. direkt eingebaut. Ein eigener `Bold()` oder `Blockquote()` Helper würde nur 1–2 Stellen abdecken und ist Overengineering. Caller nutzen `Line($"**Warum:** {entry.Warum}")` direkt.

---

## 3. Vollständige Aufrufstellen & Umbau-Anleitung

### Priorität 1 — `GetClassStructureTool.cs` (aktiver Bug, bedingte Spalten)

**Datei:** `src/AiNetLinter/Mcp/Tools/FileStructure/GetClassStructureTool.cs` Z.322–351

**Vor:**
```csharp
private static void AppendMemberRows(StringBuilder sb, IReadOnlyList<ClassStructureMemberEntry> members, bool isMultiFile)
{
    if (isMultiFile)
    {
        sb.AppendLine("| Kind | Name | Visibility | File | Lines | LineCount | Signature |");
        sb.AppendLine("|:---|:---|:---|:---|---:|---:|:---|");
    }
    else
    {
        sb.AppendLine("| Kind | Name | Visibility | Lines | LineCount | Signature |");
        sb.AppendLine("|:---|:---|:---|---:|---:|:---|");
    }
    foreach (var m in members)
        sb.AppendLine(FormatMemberRow(m, isMultiFile));
}

private static string FormatMemberRow(ClassStructureMemberEntry m, bool isMultiFile) { /* … */ }
```

**Nach:**
```csharp
private static void AppendMemberRows(StringBuilder sb, IReadOnlyList<ClassStructureMemberEntry> members, bool isMultiFile)
{
    var table = new MarkdownTableBuilder()
        .AddColumn("Kind")
        .AddColumn("Name")
        .AddColumn("Visibility");

    if (isMultiFile)
        table.AddColumn("File");

    table.AddColumn("Lines", ColumnAlign.Right)
         .AddColumn("LineCount", ColumnAlign.Right)
         .AddColumn("Signature");

    foreach (var m in members)
    {
        var linesStr = m.StartLine > 0 ? $"{m.StartLine}-{m.EndLine}" : "-";
        var countStr = m.LineCount > 0 ? m.LineCount.ToString() : "-";
        if (isMultiFile)
        {
            var fileName = !string.IsNullOrEmpty(m.FilePath) ? Path.GetFileName(m.FilePath) : "-";
            table.AddRow(m.Kind, m.Name, m.Visibility, fileName, linesStr, countStr, m.Signature);
        }
        else
        {
            table.AddRow(m.Kind, m.Name, m.Visibility, linesStr, countStr, m.Signature);
        }
    }
    table.AppendTo(sb);
}
// FormatMemberRow entfällt ersatzlos — nach Umbau löschen.
```

---

### Priorität 2 — `GetViolationsScanner.cs` (aktiver Escaping-Bug in `v.Details`)

**Datei:** `src/AiNetLinter/Mcp/Tools/Analysis/GetViolationsScanner.cs` Z.248–261 (in `AppendSection`)

**Vor:**
```csharp
sb.AppendLine("| Datei | Zeile | Regel | Details |");
sb.AppendLine("|:---|---:|:---|:---|");
foreach (var v in violations.OrderBy(...))
{
    var relativePath = Path.GetRelativePath(solutionDir, v.FilePath).Replace('\\', '/');
    sb.AppendLine($"| {relativePath} | {v.LineNumber} | {v.RuleName} | {v.Details} |");
    if (!string.IsNullOrWhiteSpace(v.Snippet))
    {
        sb.AppendLine();
        sb.AppendLine("```csharp");
        sb.AppendLine(v.Snippet);
        sb.AppendLine("```");
    }
}
```

**Nach:**
```csharp
var table = new MarkdownTableBuilder()
    .AddColumn("Datei")
    .AddColumn("Zeile", ColumnAlign.Right)
    .AddColumn("Regel")
    .AddColumn("Details");

var mb = new MarkdownBuilder();
foreach (var v in violations.OrderBy(x => x.FilePath, StringComparer.OrdinalIgnoreCase)
                                 .ThenBy(x => x.LineNumber)
                                 .ThenBy(x => x.RuleName ?? string.Empty, StringComparer.Ordinal))
{
    var relativePath = Path.GetRelativePath(solutionDir, v.FilePath).Replace('\\', '/');
    table.AddRow(relativePath, v.LineNumber, v.RuleName, v.Details);
    if (!string.IsNullOrWhiteSpace(v.Snippet))
    {
        mb.CodeBlock("csharp", v.Snippet!);
        mb.BlankLine();
    }
}
mb.Table(table);
mb.AppendTo(sb);
```

> **Reihenfolge-Entscheidung (geklärt):** Snippet-Block bleibt **innerhalb desselben `foreach`** wie die Tabellen-Row (Snippets gehören kontextuell zur jeweiligen Violation-Zeile). Die Ausgabe-Reihenfolge "Zeile → Snippet → Zeile → Snippet" bleibt identisch zur Originalimplementierung. Das war die intendierte Reihenfolge — keine Trennung in "erst alle Zeilen, dann alle Snippets".

> **Hinweis:** Der `MarkdownBuilder` wird hier genutzt, um die Tabelle und die Snippets in **einen** StringBuilder-Output zu mergen. Die `AppendSection`-Methode nimmt einen `StringBuilder sb` als Parameter; daher am Ende `mb.AppendTo(sb)`. Reihenfolge: zuerst alle Tabellen-Rows (über `MarkdownTableBuilder`), dann inline nach jeder Row der Snippet-Codeblock — das Resultat ist `Table + Snippet1 + Table + Snippet2 + ...` in der Ausgabe.

---

### Priorität 3 — `ViolationMarkdownFormatter.cs` (bedingte Spalte `hasStructural`)

**Datei:** `src/AiNetLinter/Output/ViolationMarkdownFormatter.cs` Z.60–94 (`BuildSummaryTable`)

**Vor:** Bedingte `if (hasStructural)`-Verzweigung für Header + jede Row.

**Nach:**
```csharp
var table = new MarkdownTableBuilder()
    .AddColumn("Regel")
    .AddColumn("Gesamt", ColumnAlign.Right)
    .AddColumn("Prod", ColumnAlign.Right)
    .AddColumn("Tests", ColumnAlign.Right);

if (hasStructural)
    table.AddColumn("Struktur", ColumnAlign.Center);

foreach (var r in byRule)
{
    var structMarker = StructuralRules.Contains(r.RuleName) ? "⚠" : string.Empty;
    if (hasStructural)
        table.AddRow(r.RuleName, r.Count, prodCount, testCount, structMarker);
    else
        table.AddRow(r.RuleName, r.Count, prodCount, testCount);
}
table.AppendTo(sb);
```

> **Hinweis:** Der eingerückte Code-Block in `AppendViolationItem` (Z.263–268) bleibt **unverändert** als direkter sb-Code (Sonderlogik mit 2-Leerzeichen-Präfix).

---

### Priorität 4 — `HotspotSectionFormatter.cs` ERSETZEN

**Datei:** `src/AiNetLinter/Output/HotspotSectionFormatter.cs` (44 Zeilen) — **komplett löschen** nach Umbau.

**Aufrufer 1:** `src/AiNetLinter/Mcp/Tools/FileStructure/GetHotspotsScanner.cs` Z.108–109 (in `FormatReport`)

**Vor:**
```csharp
HotspotSectionFormatter.AppendSection(sb, "Kritische Dateien (>=95% des Limits)", critical.Select(f => (f.RelativePath, f.Lines)).ToList(), maxLineCount);
HotspotSectionFormatter.AppendSection(sb, "Warnungs-Dateien (>=80% des Limits)", warning.Select(f => (f.RelativePath, f.Lines)).ToList(), maxLineCount);
```

**Nach:**
```csharp
AppendHotspotSection(sb, "Kritische Dateien (>=95% des Limits)", critical, maxLineCount);
AppendHotspotSection(sb, "Warnungs-Dateien (>=80% des Limits)", warning, maxLineCount);
```

Mit neuer privater Methode (im Scanner):
```csharp
private static void AppendHotspotSection(StringBuilder sb, string heading, IReadOnlyList<HotspotFileInfo> files, int maxLineCount)
{
    var mb = new MarkdownBuilder();
    mb.Heading(2, heading);
    mb.BlankLine();
    if (files.Count == 0)
    {
        mb.Line("Keine.");
    }
    else
    {
        mb.Table(t => t
            .AddColumn("Datei")
            .AddColumn("Zeilen", ColumnAlign.Right)
            .AddColumn("Auslastung", ColumnAlign.Right)
            .AddColumn("Verbleibend", ColumnAlign.Right));
        foreach (var f in files.OrderByDescending(x => x.Lines).ThenBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            var pct = (double)f.Lines / maxLineCount * 100;
            var remaining = maxLineCount - f.Lines;
            // Zweite Tabelle anhängen — Builder-Table akzeptiert nur eine Konfiguration,
            // daher zweite Tabelle in eigenem MarkdownBuilder:
            var pctStr = $"{pct:F0} %";
            var remainingStr = $"{remaining} Zeilen";
            // (siehe Hinweis unten)
        }
    }
    mb.AppendTo(sb);
}
```

> **Hinweis zur Builder-API-Erweiterung:** Der bestehende `Table(Action<MarkdownTableBuilder>)` ist single-shot — d. h. erzeugt **eine** Tabelle. Für variable Row-Anzahl innerhalb derselben Tabelle (z. B. hier: Header + dynamisch viele Rows) muss `MarkdownTableBuilder` selbst im Aufrufer instanziiert und mit `mb.Table(t => { t.AddColumn(...); foreach (var f in files) t.AddRow(...); })` aufgebaut werden. Siehe finales Beispiel unten.

**Finale Form (korrekt mit dynamischen Rows):**
```csharp
private static void AppendHotspotSection(StringBuilder sb, string heading, IReadOnlyList<HotspotFileInfo> files, int maxLineCount)
{
    var mb = new MarkdownBuilder();
    mb.Heading(2, heading).BlankLine();
    if (files.Count == 0)
    {
        mb.Line("Keine.");
    }
    else
    {
        mb.Table(t =>
        {
            t.AddColumn("Datei")
             .AddColumn("Zeilen", ColumnAlign.Right)
             .AddColumn("Auslastung", ColumnAlign.Right)
             .AddColumn("Verbleibend", ColumnAlign.Right);
            foreach (var f in files.OrderByDescending(x => x.Lines).ThenBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase))
            {
                var pct = (double)f.Lines / maxLineCount * 100;
                var remaining = maxLineCount - f.Lines;
                t.AddRow(f.RelativePath, f.Lines, $"{pct:F0} %", $"{remaining} Zeilen");
            }
        });
    }
    mb.AppendTo(sb);
    sb.AppendLine(); // für die Leerzeile nach der Tabelle
}
```

**Aufrufer 2:** `src/AiNetLinter/Maps/HotspotMapBuilder.cs` Z.45–46

**Vor:**
```csharp
HotspotSectionFormatter.AppendSection(sb, "🔴 Kritische Dateien (>95% des Limits)", critical.Select(...).ToList(), maxLineCount);
HotspotSectionFormatter.AppendSection(sb, "⚠ Warnungs-Dateien (>80% des Limits)", warning.Select(...).ToList(), maxLineCount);
```

**Nach:** Lokale private Methode im `HotspotMapBuilder` mit identischer Implementierung wie in `GetHotspotsScanner` (siehe oben). Die beiden Methoden bleiben getrennt, weil die Aufrufer unterschiedliche Datenstrukturen haben (`(string, int)`-Tupel vs. `HotspotFileInfo`) — Konsolidierung wäre eine größere Refactoring-Runde, nicht Teil dieses Konzepts.

> **Alternative (Konzept-Eskalation):** Statt die Logik zu duplizieren, könnte `HotspotMapBuilder` auf `GetHotspotsScanner` umsteigen — aber das würde eine Abhängigkeit `Maps → Mcp.Tools` einführen, was die Schichten-Architektur verletzt. Daher: Duplikation akzeptieren, in Tech-Debt-Log aufnehmen.

---

### Priorität 5 — `RepoPlaybookGenerator.cs`

**Datei:** `src/AiNetLinter/Generators/RepoPlaybookGenerator.cs` Z.317–333 (`AppendAgentPriority`)

**Vor:** Bedingte Behandlung "leer"-Fall mit Sonder-Row.

**Nach:**
```csharp
var table = new MarkdownTableBuilder()
    .AddColumn("Intent")
    .AddColumn("Offene Verstöße (wave-ready)", ColumnAlign.Right)
    .AddColumn("Regeln");

if (intentGroups.Count == 0)
{
    table.AddRow("-", 0, "Keine offenen Verstöße");
}
else
{
    foreach (var group in intentGroups)
        table.AddRow(group.Intent, group.Count, group.Rules);
}
mb.Table(table);
```

> **Wichtig:** `mb.Table(t => t.AddRow(...))` für **jede** Row aufzurufen erzeugt mehrere separate Tabellen untereinander — das ist **nicht** was wir wollen. Statt dessen: **eine** `MarkdownTableBuilder`-Instanz halten, alle Rows per `AddRow()` anhängen, dann **einmal** `mb.Table(table)` aufrufen. Hier nutzen wir die `Table(MarkdownTableBuilder)`-Überladung (siehe API-Erweiterung am Ende von §2).

---

### Priorität 6 — `ListRulesCommand.cs`

**Datei:** `src/AiNetLinter/Commands/ListRulesCommand.cs` Z.22–28

**Nach:**
```csharp
var table = new MarkdownTableBuilder()
    .AddColumn("RuleId")
    .AddColumn("Bezeichnung")
    .AddColumn("Intent")
    .AddColumn("Severity")
    .AddColumn("Auto-Fix");

foreach (var rule in RuleRegistry.All)
{
    var autoFix = rule.HasAutoFix ? "ja (--fix)" : "-";
    table.AddRow(rule.RuleId, rule.DisplayName, rule.Intent, rule.Severity, autoFix);
}
mb.Table(table);
```

---

### Priorität 7 — `AgentRulesGenerator.cs` `AppendMetricsTable`

**Datei:** `src/AiNetLinter/Generators/AgentRulesGenerator.cs` Z.261–267

**Nach:**
```csharp
var table = new MarkdownTableBuilder()
    .AddColumn("Regel")
    .AddColumn("Limit", ColumnAlign.Center)
    .AddColumn("Praxis");

foreach (var metric in RuleRegistry.All.Where(r => r.IsMetric))
{
    var val = metric.GetMetricLimit != null ? metric.GetMetricLimit(config) : 0;
    table.AddRow($"`{metric.RuleId}`", $"**{val}**", metric.AgentHint);
}
mb.Table(table);
```

> Backtick- und Bold-Formatierung (`` `...` ``, `**...**`) wird von `EscapeCell` **nicht** entfernt — nur `|`, `\r`, `\n` werden behandelt. Das ist korrekt.

---

### Priorität 8 — `AgentRulesGenerator.cs` `AppendCompoundSuppressions` (NEU, war übersehen)

**Datei:** `src/AiNetLinter/Generators/AgentRulesGenerator.cs` Z.182–194

**Vor:**
```csharp
sb.AppendLine("| Regel | Bedingung | Effektives Limit | Severity | Grund |");
sb.AppendLine("|:--|:--|:--|:--|:--|");
foreach (var s in suppressions)
{
    var condParts = s.WhenAllOf.Select(c =>
        c.AtMost.HasValue ? $"{c.Metric} ≤ {c.AtMost}" : $"{c.Metric} ≥ {c.AtLeast}");
    var conditions = string.Join(" AND ", condParts);
    var limit = s.RelaxedLimit.HasValue ? $"**{s.RelaxedLimit}**" : "supprimiert";
    var severity = s.SeverityOverride != null ? $"`{s.SeverityOverride}`" : "—";
    var reason = s.Reason ?? "—";
    sb.AppendLine($"| `{s.TargetRule}` | {conditions} | {limit} | {severity} | {reason} |");
}
```

**Nach:**
```csharp
var table = new MarkdownTableBuilder()
    .AddColumn("Regel")
    .AddColumn("Bedingung")
    .AddColumn("Effektives Limit")
    .AddColumn("Severity")
    .AddColumn("Grund");

foreach (var s in suppressions)
{
    var condParts = s.WhenAllOf.Select(c =>
        c.AtMost.HasValue ? $"{c.Metric} ≤ {c.AtMost}" : $"{c.Metric} ≥ {c.AtLeast}");
    var conditions = string.Join(" AND ", condParts);
    var limit = s.RelaxedLimit.HasValue ? $"**{s.RelaxedLimit}**" : "supprimiert";
    var severity = s.SeverityOverride != null ? $"`{s.SeverityOverride}`" : "—";
    var reason = s.Reason ?? "—";
    table.AddRow($"`{s.TargetRule}`", conditions, limit, severity, reason);
}
mb.Table(table);
```

---

### Priorität 9 — `MetricsLookupFormatter.cs` (NEU, komplett übersehen)

**Datei:** `src/AiNetLinter/Mcp/Tools/MetricsLookup/MetricsLookupFormatter.cs`

Diese Stelle generiert **vier verschiedene Patterns** in einer Methode — perfekter Showcase, warum der Builder ein Dokument-Builder ist und nicht nur ein Tabellen-Helper.

**Vor:** `Format(dto)` baut mit `sb.Append("### ")` + `sb.AppendLine("| Metrik | … |")` + `sb.Append("- **Ort:** ")` etc.

**Nach:** Schrittweiser Umbau mit `MarkdownBuilder`:

```csharp
internal static string Format(MetricsLookupResultDto dto)
{
    var mb = new MarkdownBuilder();

    // === Heading + Key-Value-Block ===
    mb.Heading(3, $"{dto.SymbolKind}: {dto.QualifiedName}").BlankLine();

    if (dto.Location != null)
    {
        mb.Line($"- **Ort:** `{dto.Location.FilePath}:{dto.Location.StartLine}-{dto.Location.EndLine}`");
    }
    if (!string.IsNullOrEmpty(dto.DocCommentId))
    {
        mb.Line($"- **Id:** `{dto.DocCommentId}`");
    }
    mb.BlankLine();

    // === Threshold-Check-Tabelle ===
    if (dto.ThresholdChecks.Count > 0)
    {
        mb.Heading(4, "Schwellwert-Abgleich & Metriken").BlankLine();
        var table = new MarkdownTableBuilder()
            .AddColumn("Metrik")
            .AddColumn("Wert", ColumnAlign.Right)
            .AddColumn("Grenzwert")
            .AddColumn("Status", ColumnAlign.Center)
            .AddColumn("Regel");
        foreach (var check in dto.ThresholdChecks)
        {
            var limitStr = check.Limit > 0 ? $"<= {check.Limit}" : "-";
            var statusBadge = $"[{check.Status}]";
            var ruleStr = !string.IsNullOrEmpty(check.RuleId) ? check.RuleId : "-";
            table.AddRow(
                FormatMetricDisplayName(check.Metric),
                check.Value,
                limitStr,
                statusBadge,
                ruleStr);
        }
        mb.Table(table);
        mb.BlankLine();
    }

    // === Detail-Sektionen pro Symbol-Typ ===
    if (dto.MethodMetrics != null)
    {
        FormatMethodDetails(mb, dto.MethodMetrics);
    }
    else if (dto.TypeMetrics != null)
    {
        FormatTypeDetails(mb, dto.TypeMetrics);
    }
    else if (dto.PropertyMetrics != null)
    {
        FormatPropertyDetails(mb, dto.PropertyMetrics);
    }

    return mb.Build().TrimEnd();
}

private static void FormatMethodDetails(MarkdownBuilder mb, MethodMetricsDto method) { /* … */ }
private static void FormatTypeDetails(MarkdownBuilder mb, TypeMetricsDto type) { /* … */ }
private static void FormatPropertyDetails(MarkdownBuilder mb, PropertyMetricsDto prop) { /* … */ }
```

> **Wichtig:** `FormatMethodDetails` / `FormatTypeDetails` / `FormatPropertyDetails` ändern ihre Signatur von `StringBuilder sb` zu `MarkdownBuilder mb` — alle internen `sb.AppendLine($"- **XYZ:** {…}")` werden zu `mb.Line($"- **XYZ:** {…}")` und `sb.AppendLine("#### Heading")` zu `mb.Heading(4, "Heading")`.

> **Bonus-Bug-Fix:** Aktuell schreibt der Code `sb.Append(" | ").Append(ruleStr).AppendLine(" |")` — `dto.QualifiedName` und `dto.Location.FilePath` werden in der Tabelle *nicht* escaped. Nach dem Umbau entfällt das Risiko, weil `MarkdownTableBuilder.EscapeCell` automatisch `|` in Cell-Content escaped.

---

### Priorität 10 — `GetSymbolBodyTool.cs` (NEU, single-line Code-Block)

**Datei:** `src/AiNetLinter/Mcp/Tools/GetSymbolBodyTool.cs` Z.60–64

**Vor:**
```csharp
var markdown = $"### {symbol.Kind}: {symbol.ToDisplayString()} — `{Path.GetFileName(outputRoot)}/{ToRelative(outputRoot, symbol)}`\n\n" +
               (idSuffix is null ? "" : $"id: `{idSuffix}`\n\n") +
               "```csharp\n" +
               body +
               "\n```";
```

**Nach:**
```csharp
var mb = new MarkdownBuilder();
mb.Heading(3, $"{symbol.Kind}: {symbol.ToDisplayString()} — `{Path.GetFileName(outputRoot)}/{ToRelative(outputRoot, symbol)}`");
mb.BlankLine();
if (idSuffix is not null)
{
    mb.Line($"id: `{idSuffix}`");
    mb.BlankLine();
}
mb.CodeBlock("csharp", body);
var markdown = mb.Build();
```

> **Achtung:** `body` kann den `TruncationMarker` enthalten (z. B. `"// ⚠ truncated, maxBodyLines erhoehen"`). `MarkdownBuilder.CodeBlock` schreibt den Content unverändert in den Block — `EscapeCell` ist hier nicht aktiv, weil Code-Blöcke ein anderes Escaping-Regime haben. Der Marker bleibt sichtbar. ✓

---

### Code-Block-Stellen, die NICHT umgebaut werden

| Stelle | Grund |
|:---|:---|
| `SkeletonMarkdownRenderer.cs` Z.71–82 | Inhalt wird zeilenweise direkt in sb geschrieben (`AppendMembersOfKind` schreibt direkt in sb). `MarkdownBuilder.CodeBlock()` nimmt einen fertigen string-Content. Hier ist der Content aber kein fertiger String. |
| `ViolationMarkdownFormatter.cs` Z.263–268 (`AppendViolationItem`) | Eingerückt (2 Leerzeichen) — Sonderlogik für Bullet-List-Item-Darstellung. |
| `GetViolationsScanner.cs` Z.259–261 (Snippet-Block) | Wird in Prio 2 zusammen mit dem Tabellen-Umbau mit `MarkdownBuilder.CodeBlock()` ersetzt. |

---

### API-Erweiterung (notwendig geworden durch Prio 4 + 5)

`MarkdownBuilder.Table(MarkdownTableBuilder instance)` als zweite Überladung, damit der Aufrufer eine vorbereitete `MarkdownTableBuilder`-Instanz übergeben kann (Pattern-Reuse: z. B. dieselbe Tabellen-Konfiguration mehrfach verwenden, oder die Tabelle in einer separaten Methode zusammenbauen):

```csharp
internal MarkdownBuilder Table(MarkdownTableBuilder table)
{
    table.AppendTo(_sb);
    return this;
}
```

Damit lässt sich `Table(Action<>)` als Convenience-Methode behalten, ohne den zweiten Pfad zu erzwingen.

---

## 4. Teststrategie

**Neue Testklasse:** `src/AiNetLinter.FastTests/Output/MarkdownBuilderTests.cs`
```csharp
[Trait("Category", "Unit")]
```
Keine externen Abhängigkeiten.

### Mindest-Testfälle `MarkdownTableBuilder`
1. `EscapeCell` — Pipe: `"int | string"` → `"int \| string"`
2. `EscapeCell` — Zeilenumbruch `\r\n`: → `"foo bar"` (normiert auf Space)
3. `EscapeCell` — leer/whitespace: `""` / `"   "` → `"-"`
4. `EscapeCell` — Generics: `"List<int>"` → `"List<int>"` (keine Veränderung — kein `<>` Escaping)
5. `EscapeCell` — Bold/Backticks bleiben: `"**bold**"`, `` "`code`" `` → unverändert
6. Alignment-Row: Left → `:---|`, Right → `---:|`, Center → `:---:|`
7. Spaltenanzahl-Diskrepanz: Zu wenig Cells in `AddRow` → fehlende → `"-"`
8. Leere Tabelle (kein `AddColumn`): `AppendTo` schreibt nichts
9. `Build()`: Gibt vollständige Tabelle als String zurück (Smoke-Test)
10. Vollständige Tabelle: Header + Separator + Rows — Snapshot-Assert des gesamten Outputs

### Mindest-Testfälle `MarkdownBuilder`
1. `Heading(1, "Titel")` → `"# Titel\n"`
2. `Heading(3, "Sub")` → `"### Sub\n"`
3. `CodeBlock("csharp", "var x = 1;")` → korrekte `` ``` ``-Umrahmung
4. `CodeBlock` — Content ohne trailing newline: Kein doppeltes `\n` vor `` ``` ``
5. `CodeBlock` — Content mit TruncationMarker: Marker bleibt sichtbar
6. `BlankLine()` → leere Zeile
7. `BulletList(["a", "b"])` → `"- a\n- b\n"`
8. `Table(t => t.AddColumn("X").AddRow("val"))` → korrekte Tabelle in Output
9. `Table(MarkdownTableBuilder instance)` (Überladung) — gleicher Output wie Callback-Variante
10. `AppendTo(sb)` — Output landet im äußeren StringBuilder
11. `Build()` — Gesamtausgabe als String
12. **Dokument-Mix:** `Heading(2, "Titel")` + `BulletList(...)` + `Table(...)` + `Line("**bold**")` — Snapshot-Assert

### Bestehende Tests, die unverändert grün bleiben müssen
- `ViolationMarkdownFormatterTests` (Prio 3 + eingerückter Code-Block)
- `McpServerCommand*Tests` (Prio 9 — `MetricsLookupFormatter`-Output muss identisch sein)
- `McpServerCommandCallLogTests`, `McpServerCommandCacheBypassTests`, `McpServerCommandJsonRpcFramingTests` (Prio 10 — `GetSymbolBodyTool`-Markdown-Format)

---

## 5. Entdeckte Mängel / Redundanzen

| Fund | Ort | Entscheidung |
|:---|:---|:---|
| `FormatMemberRow` existiert nur wegen fehlendem Builder | `GetClassStructureTool.cs` | Löschen nach Umbau (Prio 1) |
| `v.Details` landet unescaped in Tabellenzelle | `GetViolationsScanner.cs:255` | Fix via Builder (Prio 2) |
| `v.Signature` mit `|` zerschießt Tabelle | `GetClassStructureTool.cs FormatMemberRow` | Fix via Builder (Prio 1) |
| `dto.QualifiedName` / `dto.Location.FilePath` unescaped in Tabelle | `MetricsLookupFormatter.cs:46-51` | Fix via Builder (Prio 9) — als Bonus-Effekt |
| Eingerückter Code-Block mit 2-Space-Prefix | `ViolationMarkdownFormatter.cs Z.263–268` | Kein Umbau — Sonderlogik bleibt |
| Code-Block in `SkeletonMarkdownRenderer` — Inhalt wird zeilenweise direkt in sb geschrieben | `SkeletonMarkdownRenderer.cs Z.71–82` | Kein Umbau — `CodeBlock(string)` nicht anwendbar |
| `HotspotSectionFormatter` ist dedizierter Wrapper | `Output/HotspotSectionFormatter.cs` | **Ersetzen** durch direkten `MarkdownBuilder`-Einsatz in beiden Aufrufern (Prio 4) — Duplikation der `AppendHotspotSection`-Logik in `GetHotspotsScanner` und `HotspotMapBuilder` wird in Tech-Debt-Log aufgenommen (Schicht-Trennung verhindert gemeinsamen Helper) |
| Zweite Tabelle in `AgentRulesGenerator` (CompoundSuppressions) im Konzept übersehen | `AgentRulesGenerator.cs:182-194` | Prio 8 — jetzt im Scope |
| `GetSymbolBodyTool` Code-Block im Konzept übersehen | `GetSymbolBodyTool.cs:62` | Prio 10 — jetzt im Scope |

---

## 6. Verworfene Alternativen

- **Externe Bibliothek:** Parser, keine Builder. Abgelehnt.
- **`MarkdownTableBuilder` only (kein allgemeiner Builder):** Code-Block-Pattern + Headings + Bullets wären weiterhin dupliziert. Abgelehnt.
- **`MarkdownDocument`-Wrapper (Heading als Methode erzwingt komplette Migration):** Zu invasiv. `MarkdownBuilder` nutzt eigenen sb und kann per `AppendTo` in bestehende Code-Flows integriert werden — keine erzwungene Komplett-Migration.
- **Extension-Methods auf `StringBuilder`:** Schlechtere Auffindbarkeit, kein Escaping-Schutz ohne expliziten Wrapper-Typ.
- **`HotspotSectionFormatter` als dedizierten Helper behalten:** Zwei parallele Tabellen-Wege. Sortierung ist keine Builder-Verantwortung. Abgelehnt — Ersetzen.
- **Verzeichnis `markdown-table-builder/` behalten:** Name zu eng, deckt nur 1 von 4 Patterns ab. Abgelehnt — Umbenennen zu `markdown-builder/`.
- **Konzept auf `ready` lassen nach Überarbeitung:** Substantielle Änderungen (neue Stellen + HotspotSectionFormatter-Entscheidung) + es war nie umgesetzt. Status ehrlich auf `draft` bis offene Fragen geklärt sind.

---

## 7. Wo im Projekt

**Neue Datei:**
- `src/AiNetLinter/Output/MarkdownBuilder.cs` — `ColumnAlign`, `MarkdownTableBuilder`, `MarkdownBuilder`

**Neue Tests:**
- `src/AiNetLinter.FastTests/Output/MarkdownBuilderTests.cs`

**Zu ändernde Dateien (10 Callsites + 1 Helfer-Löschung):**
- `src/AiNetLinter/Mcp/Tools/FileStructure/GetClassStructureTool.cs` (Prio 1)
- `src/AiNetLinter/Mcp/Tools/Analysis/GetViolationsScanner.cs` (Prio 2)
- `src/AiNetLinter/Output/ViolationMarkdownFormatter.cs` (Prio 3 — nur `BuildSummaryTable`, nicht `AppendViolationItem`)
- `src/AiNetLinter/Mcp/Tools/FileStructure/GetHotspotsScanner.cs` (Prio 4 — `AppendHotspotSection` privat)
- `src/AiNetLinter/Maps/HotspotMapBuilder.cs` (Prio 4 — `AppendHotspotSection` privat, dupliziert)
- `src/AiNetLinter/Generators/RepoPlaybookGenerator.cs` (Prio 5)
- `src/AiNetLinter/Commands/ListRulesCommand.cs` (Prio 6)
- `src/AiNetLinter/Generators/AgentRulesGenerator.cs` (Prio 7 + 8 — `AppendMetricsTable` + `AppendCompoundSuppressions`)
- `src/AiNetLinter/Mcp/Tools/MetricsLookup/MetricsLookupFormatter.cs` (Prio 9 — `Format`, `FormatMethodDetails`, `FormatTypeDetails`, `FormatPropertyDetails` Signaturen ändern)
- `src/AiNetLinter/Mcp/Tools/GetSymbolBodyTool.cs` (Prio 10)

**Zu löschende Datei:**
- `src/AiNetLinter/Output/HotspotSectionFormatter.cs` (Prio 4 — vollständig gelöscht, beide Aufrufer nutzen eigene `AppendHotspotSection`)

**Unverändert (3 Code-Block-Sonderfälle):**
- `src/AiNetLinter/Maps/Skeleton/SkeletonMarkdownRenderer.cs` (Sonderlogik)
- `src/AiNetLinter/Output/ViolationMarkdownFormatter.cs` `AppendViolationItem` (eingerückt)
- `src/AiNetLinter/Output/ViolationMarkdownFormatter.cs` `AppendViolationItem` Snippet-Block (eingerückt)

---

## 8. Implementierungsreihenfolge für Coder-Agent

```
1. tasks/markdown-builder/konzept.md existiert bereits (diese Datei)

2. src/AiNetLinter/Output/MarkdownBuilder.cs anlegen
   → ColumnAlign, MarkdownTableBuilder, MarkdownBuilder
   → API-Erweiterung Table(MarkdownTableBuilder) berücksichtigen

3. src/AiNetLinter.FastTests/Output/MarkdownBuilderTests.cs anlegen
   → alle Unit-Tests grün

4. GetClassStructureTool.cs umstellen (Prio 1 — Bug-Fix)
   → FormatMemberRow löschen

5. GetViolationsScanner.cs umstellen (Prio 2 — Bug-Fix)
   → Snippet-Loop-Trennung klären, dann umbauen

6. ViolationMarkdownFormatter.cs umstellen (Prio 3 — BuildSummaryTable)
   → AppendViolationItem unverändert lassen

7. HotspotSectionFormatter.cs: Aufrufer umstellen (Prio 4)
   → GetHotspotsScanner.cs: AppendHotspotSection privat
   → HotspotMapBuilder.cs: AppendHotspotSection privat
   → HotspotSectionFormatter.cs löschen

8. RepoPlaybookGenerator.cs umstellen (Prio 5)

9. ListRulesCommand.cs umstellen (Prio 6)

10. AgentRulesGenerator.cs umstellen (Prio 7 + 8)
    → AppendMetricsTable + AppendCompoundSuppressions

11. MetricsLookupFormatter.cs umstellen (Prio 9)
    → Format, FormatMethodDetails, FormatTypeDetails, FormatPropertyDetails

12. GetSymbolBodyTool.cs umstellen (Prio 10)

13. Verifikation: dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
14. Verifikation: dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
    (Schritt 13/14 NICHT durch Konzept-Workflow — erst nach Drift-Loop-Start durch Coder-Agent)
```

---

## 9. Offene Punkte

Keine. Alle während der Konzept-Iteration aufgekommenen Fragen sind in den jeweiligen Abschnitten entschieden:

- **Sortierung in Hotspot-Aufrufern** (war `open_questions` #1): **Aufrufer sortiert**, Builder bleibt dumm. Siehe Prio 4 (`AppendHotspotSection` ruft `OrderByDescending().ThenBy()` selbst auf).
- **Snippet-Reihenfolge in `GetViolationsScanner`** (war `open_questions` #2): **inner-foreach** — Snippet-Block bleibt im selben Loop wie die Tabellen-Row, Ausgabe-Reihenfolge "Zeile → Snippet → Zeile → Snippet" bleibt identisch zur Originalimplementierung. Siehe Prio 2.
- **Escaping-Strategie** (war `open_questions` #3): **hartcodiert** (`|` → `\|`, `\r`/`\n` → Leerzeichen). CommonMark-konform, alle aktuellen Aufrufer brauchen nichts anderes. YAGNI für Konfigurierbarkeit.
