---
title: MarkdownBuilder — Fluenter Markdown-Helper
status: ready
last_updated: 2026-08-19
rules_dir: .agents/rules
project_kind: brownfield
estimated_scope: small
open_questions: []
---

# Konzept: Interner `MarkdownBuilder` — Fluenter Markdown-Helper

## 1. Motivation & Analyse-Ergebnis

### Ausgangslage
Das Projekt baut Markdown-Output an **mindestens 3 verschiedenen Pattern-Typen** mit rohem String-Gebastel zusammen:
1. **Tabellen** (9 Separator-Zeilen in 7 Dateien) — aktiver Escaping-Bug bei Signaturen/Details mit `|`.
2. **Code-Blöcke** (4+ Stellen in 3 Dateien) — dreifach dupliziertes `` ```csharp `` / `` ``` ``-Pattern.
3. **Eingerückte Code-Blöcke** (1 Stelle) — Sonderfall in `ViolationMarkdownFormatter`: Code-Block als Teil einer Bullet-List (2 Leerzeichen Indent), kein generischer Block.

### Entscheidungen (final)
| Frage | Entscheidung | Begründung |
|:---|:---|:---|
| Table-only oder allgemein? | **Allgemeiner `MarkdownBuilder`** | Tabellen + Code-Blöcke teilen dasselbe Problem (String-Assembling ohne Abstraktion). Ein gemeinsamer Builder ist konsistenter als zwei isolierte Klassen. |
| Klassen-Name | **`MarkdownBuilder`** | Passt zur Projekt-Konvention (`DebtReportBuilder`, `HotspotMapBuilder`, `ViolationSummaryBuilder`). Kein `Table` im Namen — zu eng. |
| Platzierung | **`src/AiNetLinter/Output/MarkdownBuilder.cs`** | Nah an den bestehenden `*Formatter`/`*Builder`-Klassen; kein neuer Ordner nötig. Namespace `AiNetLinter.Output`. |
| Fluent API? | **Ja** | Spalten/Zeilen deklarativ definieren, `AddColumn().AddRow()...AppendTo()`. Konsistent mit dem Builder-Pattern. |
| `AppendTo` vs. `Build()`? | **Beide** | `AppendTo(StringBuilder)` für Callsites (alle nutzen äußeren sb); `Build()` → string für Tests und Stellen die String zurückgeben. Overhead: 0. |
| Externe Bibliothek | **Nein** | Parser, keine Builder. Dependency-Overhead. |
| Eingerückter Code-Block | **Nicht in Builder** | Ist Darstellungslogik eines Violation-Items (`AppendViolationItem`) — kein allgemeines Pattern. Bleibt direkter sb-Code. |

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

**Datei:** `src/AiNetLinter/Mcp/Tools/FileStructure/GetClassStructureTool.cs`  
**Zeilen:** 322–351

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

private static string FormatMemberRow(ClassStructureMemberEntry m, bool isMultiFile)
{
    var linesStr = m.StartLine > 0 ? $"{m.StartLine}-{m.EndLine}" : "-";
    var countStr = m.LineCount > 0 ? m.LineCount.ToString() : "-";
    if (isMultiFile)
    {
        var fileName = !string.IsNullOrEmpty(m.FilePath) ? Path.GetFileName(m.FilePath) : "-";
        return $"| {m.Kind} | {m.Name} | {m.Visibility} | {fileName} | {linesStr} | {countStr} | {m.Signature} |";
    }
    return $"| {m.Kind} | {m.Name} | {m.Visibility} | {linesStr} | {countStr} | {m.Signature} |";
}
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

**Datei:** `src/AiNetLinter/Mcp/Tools/Analysis/GetViolationsScanner.cs`  
**Zeilen:** 248–255 (in Methode `AppendSection`)

**Vor:**
```csharp
sb.AppendLine("| Datei | Zeile | Regel | Details |");
sb.AppendLine("|:---|---:|:---|:---|");
foreach (var v in violations.OrderBy(x => x.FilePath, ...).ThenBy(x => x.LineNumber).ThenBy(x => x.RuleName, ...))
{
    var relativePath = Path.GetRelativePath(solutionDir, v.FilePath).Replace('\\', '/');
    sb.AppendLine($"| {relativePath} | {v.LineNumber} | {v.RuleName} | {v.Details} |");
    // ... Snippet-Block folgt danach
}
```

**Nach:**
```csharp
var table = new MarkdownTableBuilder()
    .AddColumn("Datei")
    .AddColumn("Zeile", ColumnAlign.Right)
    .AddColumn("Regel")
    .AddColumn("Details");

foreach (var v in violations.OrderBy(x => x.FilePath, ...).ThenBy(x => x.LineNumber).ThenBy(x => x.RuleName, ...))
{
    var relativePath = Path.GetRelativePath(solutionDir, v.FilePath).Replace('\\', '/');
    table.AddRow(relativePath, v.LineNumber, v.RuleName, v.Details);
}
table.AppendTo(sb);
sb.AppendLine();
// Snippet-Blöcke: Der foreach-Loop für Snippets wird separat danach beibehalten (Snippets stehen NACH der Tabelle)
```

> **Achtung beim Umbau:** In der Originalimplementierung stehen Snippet-Blöcke (`if (!string.IsNullOrWhiteSpace(v.Snippet))`) innerhalb desselben `foreach`. Nach dem Umbau existieren zwei separate Schleifen: erst die Tabelle (alle Violations), dann die Snippets. Das ändert die Ausgabereihenfolge (Tabelle komplett → dann alle Snippets). **Prüfen ob das gewollt ist** oder ob Snippets zur jeweiligen Tabellen-Zeile gehören müssen. Falls letzteres: Snippet-Ausgabe bleibt als separater `foreach` mit direktem sb-Append, direkt nach `table.AppendTo(sb)`.

---

### Priorität 3 — `ViolationMarkdownFormatter.cs` (bedingte Spalte `hasStructural`)

**Datei:** `src/AiNetLinter/Output/ViolationMarkdownFormatter.cs`  
**Zeilen:** 60–94 (Methode `BuildSummaryTable`)

**Vor:**
```csharp
if (hasStructural)
{
    sb.Append("| Regel | Gesamt | Prod | Tests | Struktur |\n");
    sb.Append("|---|---:|---:|---:|:---:|\n");
}
else
{
    sb.Append("| Regel | Gesamt | Prod | Tests |\n");
    sb.Append("|---|---:|---:|---:|\n");
}
foreach (var r in byRule)
{
    ...
    if (hasStructural)
        sb.Append($"| {r.RuleName} | {r.Count} | {prodCount} | {testCount} | {structMarker} |\n");
    else
        sb.Append($"| {r.RuleName} | {r.Count} | {prodCount} | {testCount} |\n");
}
```

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

---

### Priorität 4 — `HotspotSectionFormatter.cs`

**Datei:** `src/AiNetLinter/Output/HotspotSectionFormatter.cs`  
**Zeilen:** 35–41

```csharp
// Vor:
sb.AppendLine("| Datei | Zeilen | Auslastung | Verbleibend |");
sb.AppendLine("|:---|---:|---:|---:|");
foreach (var f in files.OrderByDescending(...))
    sb.AppendLine($"| {f.RelativePath} | {f.Lines} | {pct:F0} % | {remaining} Zeilen |");

// Nach:
var table = new MarkdownTableBuilder()
    .AddColumn("Datei")
    .AddColumn("Zeilen", ColumnAlign.Right)
    .AddColumn("Auslastung", ColumnAlign.Right)
    .AddColumn("Verbleibend", ColumnAlign.Right);

foreach (var f in files.OrderByDescending(x => x.Lines).ThenBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase))
{
    var pct = (double)f.Lines / maxLineCount * 100;
    var remaining = maxLineCount - f.Lines;
    table.AddRow(f.RelativePath, f.Lines, $"{pct:F0} %", $"{remaining} Zeilen");
}
table.AppendTo(sb);
```

---

### Priorität 5 — `RepoPlaybookGenerator.cs`

**Datei:** `src/AiNetLinter/Generators/RepoPlaybookGenerator.cs`  
**Zeilen:** 317–333 (Methode `AppendAgentPriority`)

```csharp
// Vor:
sb.AppendLine("| Intent | Offene Verstöße (wave-ready) | Regeln |");
sb.AppendLine("| :--- | ---: | :--- |");
...
sb.AppendLine($"| {group.Intent} | {group.Count} | {group.Rules} |");

// Nach:
var table = new MarkdownTableBuilder()
    .AddColumn("Intent")
    .AddColumn("Offene Verstöße (wave-ready)", ColumnAlign.Right)
    .AddColumn("Regeln");

if (intentGroups.Count == 0)
    table.AddRow("-", 0, "Keine offenen Verstöße");
else
    foreach (var group in intentGroups)
        table.AddRow(group.Intent, group.Count, group.Rules);

table.AppendTo(sb);
```

---

### Priorität 6 — `ListRulesCommand.cs`

**Datei:** `src/AiNetLinter/Commands/ListRulesCommand.cs`  
**Zeilen:** 22–28 (Methode `ListAll`)

```csharp
// Vor:
sb.AppendLine("| RuleId | Bezeichnung | Intent | Severity | Auto-Fix |");
sb.AppendLine("|:---|:---|:---|:---|:---|");
foreach (var rule in RuleRegistry.All)
    sb.AppendLine($"| {rule.RuleId} | {rule.DisplayName} | {rule.Intent} | {rule.Severity} | {autoFix} |");

// Nach:
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
table.AppendTo(sb);
```

---

### Priorität 7 — `AgentRulesGenerator.cs`

**Datei:** `src/AiNetLinter/Generators/AgentRulesGenerator.cs`  
**Zeilen:** 261–267 (Methode `AppendMetricsTable`)

```csharp
// Vor:
sb.AppendLine("| Regel | Limit | Praxis |");
sb.AppendLine("| :--- | :---: | :--- |");
foreach (var metric in RuleRegistry.All.Where(r => r.IsMetric))
    sb.AppendLine($"| `{metric.RuleId}` | **{val}** | {metric.AgentHint} |");

// Nach:
var table = new MarkdownTableBuilder()
    .AddColumn("Regel")
    .AddColumn("Limit", ColumnAlign.Center)
    .AddColumn("Praxis");

foreach (var metric in RuleRegistry.All.Where(r => r.IsMetric))
{
    var val = metric.GetMetricLimit != null ? metric.GetMetricLimit(config) : 0;
    table.AddRow($"`{metric.RuleId}`", $"**{val}**", metric.AgentHint);
}
table.AppendTo(sb);
```

> **Hinweis:** Backtick- und Bold-Formatierung (`\`...\``, `**...**`) wird von `EscapeCell` **nicht** entfernt — nur `|`, `\r`, `\n` werden behandelt. Das ist korrekt.

---

### Code-Block-Stellen

**`SkeletonMarkdownRenderer.cs` Z.71–82:**
```csharp
// Vor:
sb.AppendLine("```csharp");
// ... member-Zeilen direkt in sb ...
sb.AppendLine("```");

// Nach: NICHT mit MarkdownBuilder.CodeBlock() ersetzen.
// Begründung: Zwischen ``` und ``` werden Member-Zeilen zeilenweise direkt in sb geschrieben
// (AppendMembersOfKind schreibt direkt in sb). MarkdownBuilder.CodeBlock() nimmt einen
// fertigen string-Content — hier ist der Content aber kein fertiger String.
// → Diese Stelle bleibt als direkter sb-Code. Kein Umbau.
```

**`GetViolationsScanner.cs` Z.259–261 und `ViolationMarkdownFormatter.cs` Z.263–268:**
```csharp
// GetViolationsScanner (nach Tabellen-Umbau prüfen ob Snippet-Block noch passt):
sb.AppendLine("```csharp");
sb.AppendLine(v.Snippet);
sb.AppendLine("```");
// → Kann mit MarkdownBuilder.CodeBlock() ersetzt werden, WENN Snippet ein fertiger String ist.
// Prüfen: v.Snippet kann mehrzeilig sein → CodeBlock() normiert korrekt.

// ViolationMarkdownFormatter (eingerückt, 2 Spaces Indent):
sb.Append("  ```csharp\n");
// ... Zeilen mit "  " Prefix ...
sb.Append("  ```\n");
// → NICHT mit MarkdownBuilder.CodeBlock() ersetzen — Einrückung ist Sonderlogik.
// Diese Stelle bleibt direkter sb-Code.
```

**Netto-Umbau Code-Blöcke:** Nur `GetViolationsScanner` (nach Tabellen-Umbau evaluieren), `SkeletonMarkdownRenderer` und `ViolationMarkdownFormatter` bleiben unverändert.

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
5. Alignment-Row: Left → `:---|`, Right → `---:|`, Center → `:---:|`
6. Spaltenanzahl-Diskrepanz: Zu wenig Cells in `AddRow` → fehlende → `"-"`
7. Leere Tabelle (kein `AddColumn`): `AppendTo` schreibt nichts
8. `Build()`: Gibt vollständige Tabelle als String zurück (Smoke-Test)
9. Vollständige Tabelle: Header + Separator + Rows — Snapshot-Assert des gesamten Outputs

### Mindest-Testfälle `MarkdownBuilder`
1. `Heading(1, "Titel")` → `"# Titel\n"`
2. `Heading(3, "Sub")` → `"### Sub\n"`
3. `CodeBlock("csharp", "var x = 1;")` → korrekte `` ``` ``-Umrahmung
4. `CodeBlock` — Content ohne trailing newline: Kein doppeltes `\n` vor `` ``` ``
5. `BlankLine()` → leere Zeile
6. `BulletList(["a", "b"])` → `"- a\n- b\n"`
7. `Table(t => t.AddColumn("X").AddRow("val"))` → korrekte Tabelle in Output
8. `AppendTo(sb)` — Output landet im äußeren StringBuilder
9. `Build()` — Gesamtausgabe als String

---

## 5. Entdeckte Mängel / Redundanzen

| Fund | Ort | Entscheidung |
|:---|:---|:---|
| `FormatMemberRow` existiert nur wegen fehlendem Builder | `GetClassStructureTool.cs` Z.341 | Löschen nach Umbau |
| `v.Details` landet unescaped in Tabellenzelle | `GetViolationsScanner.cs` Z.255 | Fix via Builder — im Scope |
| Eingerückter Code-Block mit 2-Space-Prefix | `ViolationMarkdownFormatter.cs` Z.263–268 | Kein Umbau — Sonderlogik bleibt |
| Code-Block in `SkeletonMarkdownRenderer` — Inhalt wird zeilenweise direkt in sb geschrieben | `SkeletonMarkdownRenderer.cs` Z.71–82 | Kein Umbau — `CodeBlock(string)` nicht anwendbar |

---

## 6. Verworfene Alternativen

- **Externe Bibliothek:** Parser, keine Builder. Abgelehnt.
- **`MarkdownTableBuilder` only (kein allgemeiner Builder):** Code-Block-Pattern wäre weiterhin dupliziert. Abgelehnt.
- **`MarkdownDocument`-Wrapper (Heading als Methode erzwingt komplette Migration):** Zu invasiv. `MarkdownBuilder` nutzt eigenen sb und kann per `AppendTo` in bestehende Code-Flows integriert werden — keine erzwungene Komplett-Migration.
- **Extension-Methods auf `StringBuilder`:** Schlechtere Auffindbarkeit, kein Escaping-Schutz ohne expliziten Wrapper-Typ.

---

## 7. Implementierungsreihenfolge für Coder-Agent

```
1. src/AiNetLinter/Output/MarkdownBuilder.cs anlegen
   → ColumnAlign, MarkdownTableBuilder, MarkdownBuilder

2. src/AiNetLinter.FastTests/Output/MarkdownBuilderTests.cs anlegen
   → alle Unit-Tests grün

3. GetClassStructureTool.cs umstellen (Prio 1 — Bug-Fix)
   → FormatMemberRow löschen

4. GetViolationsScanner.cs umstellen (Prio 2 — Bug-Fix)
   → Snippet-Loop-Trennung beachten (siehe Hinweis oben)

5. ViolationMarkdownFormatter.cs umstellen (Prio 3)

6. HotspotSectionFormatter.cs umstellen (Prio 4)

7. RepoPlaybookGenerator.cs umstellen (Prio 5)

8. ListRulesCommand.cs umstellen (Prio 6)

9. AgentRulesGenerator.cs umstellen (Prio 7)

10. Verifikation: dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
11. Verifikation: dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
```
