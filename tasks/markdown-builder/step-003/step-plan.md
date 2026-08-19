---
status: open
type: step-plan
task: markdown-builder
step: 003
corrects: null
title: "EPIC-02 Welle 1 — HotspotSectionFormatter löschen + ListRulesCommand + GetSymbolBodyTool"
epic: EPIC-02
estimated_risk: low
step_type: single
created_by: planer
created_by_model: MiniMax-M3
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-19
related_to:
  - step-001/step-plan.md    # Muster für GetClassStructureTool-Migration (Callback-Variante `Table(t => ...)` + Bedingte-Spalten-Logik)
  - step-002/step-plan.md    # Muster für `MarkdownTableBuilder`/AppendTo-Single-Source-of-Truth + Unit-Tests-Verriegelung
---

# Step 003: EPIC-02 Welle 1 — HotspotSectionFormatter löschen + ListRulesCommand + GetSymbolBodyTool

## Bezug

- **Task:** `markdown-builder`
- **Epic:** `EPIC-02` aus `roadmap.md` — „Verbleibende Callsites umstellen + HotspotSectionFormatter entfernen"
- **Konzept-Referenz:** `konzept.md` §3 Prio 4 (HotspotSectionFormatter-Löschung), §3 Prio 6 (ListRulesCommand), §3 Prio 10 (GetSymbolBodyTool). §3 Prio 5/7/8/9 + TD-001 bleiben für step-004/step-005.

## Aktueller Projektzustand (JIT-Kontext)

EPIC-01 ist vollständig abgeschlossen (step-001 `fc603681` + step-002 `b1a39ab1` beide `approved`): `MarkdownBuilder.cs` (167 Z., Namespace `AiNetLinter.Output`) liefert `ColumnAlign`, `MarkdownTableBuilder` mit `BuildHeaderLine`/`BuildSeparatorLine`/`BuildRowLine(params)` + `FormatRow`-Helper, und `MarkdownBuilder` mit `Heading`/`BlankLine`/`Line`/`BulletList`/`CodeBlock`/`Table(Action<>)`/`Table(MarkdownTableBuilder)`/`AppendTo`/`Build`. 30/30 `MarkdownBuilderTests` grün, 16/16 `GetViolationsToolTests` grün (Byte-Stabilität `AppendSection` bestätigt), 31/31 `ViolationMarkdownFormatterTests` grün.

Der für diesen Step relevante Bestand:

- `src/AiNetLinter/Output/HotspotSectionFormatter.cs` (44 Z.) — `internal static class` mit `AppendSection(StringBuilder, string heading, IReadOnlyList<(string, int)>, int maxLineCount)`. Schreibt `## {heading}\n\n` + Tabelle (4 Spalten, Datei/Zeilen/Auslastung/Verbleibend) + Sortierung `OrderByDescending(Lines).ThenBy(RelativePath, OrdinalIgnoreCase)`. Wird heute von zwei Aufrufern genutzt (s.u.).
- `src/AiNetLinter/Mcp/Tools/FileStructure/GetHotspotsScanner.cs` Z.108-109 — `FormatReport` ruft `HotspotSectionFormatter.AppendSection` zweimal (kritisch ≥95 %, warnend ≥80 %) mit `HotspotFileInfo.Select(f => (f.RelativePath, f.Lines)).ToList()`. Hat _daneben_ raw-`sb.AppendLine("## ...")`-Code für die „Alle anderen Dateien"-Sektion (Z.119-121), der unverändert bleibt.
- `src/AiNetLinter/Maps/HotspotMapBuilder.cs` Z.45-46 — gleiches Aufrufermuster, aber mit Emojis im Heading (`🔴 Kritische Dateien`, `⚠ Warnungs-Dateien`) und Pfad-Information im `Gescannt:`-Block. Auch hier bleibt die „Alle anderen Dateien"-Sektion raw-`sb`.
- `src/AiNetLinter/Commands/ListRulesCommand.cs` Z.22-28 — `ListAll` schreibt `| RuleId | Bezeichnung | Intent | Severity | Auto-Fix |` + `|:--|:--|:--|:--|:--|` (kein Alignment, linksbündig) + Foreach-Row mit `sb.AppendLine($"| ... |")`. 5 Spalten, kein `string.Join`, keine Escaping-Sorgen (Rule-IDs/Severity-Enum sind trivial). Einfachster Migrationsfall in EPIC-02.
- `src/AiNetLinter/Mcp/Tools/GetSymbolBodyTool.cs` Z.60-64 — `ExecuteAsync` baut Markdown-Output per `string.Concat` mit `### ...\n\n` + optional `id: ...\n\n` + ` ```csharp\n{body}\n``` `. Body kann `TruncationMarker` enthalten, der erhalten bleiben muss. MCP-Token-Vertrag gegenüber Agenten — Output muss byte-stabil bleiben (verifiziert via `McpServerCommandContractTests`).

**Bestehende Strukturen, die wiederverwendet werden:**

- `MarkdownBuilder.Table(Action<MarkdownTableBuilder>)` Callback-Variante (genutzt in Prio 1, Prio 2) — passt für Prio 4 (Hotspot: dynamische Row-Anzahl pro Tabelle) und Prio 6 (ListRules: alle Rows in einer Schleife).
- `MarkdownBuilder.Heading(int level, string text)` emittiert `#…# {text}\n` — exakt deckungsgleich mit dem bestehenden `$"## {heading}\n"`-Pattern in `HotspotSectionFormatter` (für `level=2`) bzw. dem `$"### ...\n\n"`-Pattern in `GetSymbolBodyTool` (für `level=3` + nachfolgendem `BlankLine`).
- `MarkdownBuilder.CodeBlock(string language, string content)` — fenced Block mit Trailing-`\n` nach ` ``` ` (siehe `MarkdownBuilder.cs:137-147`). _Achtung_: Die aktuelle Implementierung in `GetSymbolBodyTool` schreibt `\n``` ohne abschließenden Zeilenumbruch`; die Builder-Variante hängt ein `\n` an. Siehe „Bekannte Ausnahmen" unten — der Coder muss die Byte-Stabilität verifizieren und ggf. mit `.TrimEnd()` ausgleichen.

**Anti-Loop-Check gegen CodeMap:**

- CodeMap-Eintrag `src/AiNetLinter/Output/HotspotSectionFormatter.cs` sagt explizit „am Ende zu löschen" — dieser Step setzt das um. Kein Widerspruch.
- CodeMap-Eintrag `src/AiNetLinter/Maps/HotspotMapBuilder.cs` sagt „eigene `AppendHotspotSection` wegen Schicht-Trennung" — dieser Step folgt dem. Kein Widerspruch.
- Kein EPIC-01-CodeMap-Eintrag wird durch diesen Step invalidiert.

## Intention

EPIC-02 in **drei committbaren Wellen** statt einem Mega-Step abzuarbeiten. Welle 1 (dieser Step) räumt das niedrig-hängende Obst ab: die Datei-Löschung von `HotspotSectionFormatter.cs` (etabliert das Pattern „Aufrufer besitzt seine eigene Formatierung", kein Shared Helper), den trivialsten Tabellen-Migrationsfall (`ListRulesCommand`), und den einzigen MCP-Token-Vertrag-Callsite, der gleichzeitig winzig und isoliert testbar ist (`GetSymbolBodyTool`). Damit ist nach diesem Step bereits die Hälfte der EPIC-02-Callsites abgehakt und das Lösch-Pattern etabliert. Welle 2 (step-004) migriert die drei verbleibenden `Generators`-Callsites + nutzt die `Table(MarkdownTableBuilder)`-Instanz-Überladung produktiv (obsoleted TD-002). Welle 3 (step-005) nimmt den risikoreichsten Block (`MetricsLookupFormatter`, vier Pattern-Arten, byte-stabil) + hängt opportunistisch TD-001 an (Header raw `sb.Append` in derselben Datei).

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Output/HotspotSectionFormatter.cs` (komplett löschen)

- **Was:** Datei ersatzlos löschen.
- **Warum:** Konzept §3 Prio 4 + §5 + §6: Sortierung ist keine Builder-Verantwortung, zwei parallele Tabellen-Wege sind Drift-Risiko, der Helper ist nur Wrapper um ein Pattern, das beide Aufrufer 1:1 reinkarnieren können. Schicht-Trennung verbietet gemeinsamen Helper zwischen `Maps` und `Mcp.Tools` (siehe Konzept §6 + Roadmap Tech-Stack-Notiz), also bekommen beide Aufrufer jeweils eine private `AppendHotspotSection`.
- **Hinweis:** Verwendungsnachweise vor dem Löschen per `rg "HotspotSectionFormatter"` prüfen — sollten exakt 2 sein (`GetHotspotsScanner.cs:108` + `HotspotMapBuilder.cs:45`). Jeder weitere Treffer ist ein Befund, der zuerst behoben werden muss. Auch `using`-Imports in beiden Dateien (`using AiNetLinter.Output;` in `HotspotMapBuilder.cs:8`) prüfen — `HotspotMapBuilder` braucht `Output` weiterhin für nichts anderes (siehe aktuelle Imports: `System.*` + `AiNetLinter.Output`). Wenn nach dem Löschen der `using AiNetLinter.Output;`-Import ungenutzt wird, in derselben Datei mit entfernen (DRY).

### Datei 2: `src/AiNetLinter/Mcp/Tools/FileStructure/GetHotspotsScanner.cs` (Z.96-124)

- **Was:** In `FormatReport` Z.108-109 die zwei `HotspotSectionFormatter.AppendSection(sb, "Kritische Dateien (>=95% des Limits)", critical.Select(f => (f.RelativePath, f.Lines)).ToList(), maxLineCount)`-Aufrufe durch lokale `AppendHotspotSection(sb, "Kritische Dateien (>=95% des Limits)", critical, maxLineCount)` ersetzen. Neue private Methode `private static void AppendHotspotSection(StringBuilder sb, string heading, IReadOnlyList<HotspotFileInfo> files, int maxLineCount)` (Scanner-Variante, nimmt `HotspotFileInfo`-Records) hinzufügen, die intern `MarkdownBuilder` nutzt. Die „Alle anderen Dateien"-Sektion Z.119-121 bleibt raw-`sb` (anderes Heading-Level H2 + Bullet-List-Charakter, kein Tabellen-Pattern).
- **Warum:** Konzept §3 Prio 4 + §6: Sortierung wandert in den Aufrufer, Builder bleibt dumm, atomare Einheit (Heading + Tabelle) bleibt als Helper-Methode im Aufrufer sichtbar. _Schicht-Trennung_ ist hier irrelevant, weil `GetHotspotsScanner` bereits in `Mcp.Tools.FileStructure` lebt — `Output`-Namespace darf referenziert werden.
- **Code-Skizze** (siehe auch Code-Skizze-Sektion unten):
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
      sb.AppendLine();  // Trailing-Blank nach Tabelle, byte-stabil zum Original
  }
  ```
- **Byte-Stabilität:** Das Original emittiert nach der Tabelle ein `sb.AppendLine()` (Z.43 in `HotspotSectionFormatter.cs`) für die Leerzeile vor dem nächsten Block. `mb.AppendTo(sb)` allein schreibt **keine** trailing Leerzeile, weil `MarkdownTableBuilder.AppendTo` nur Header + Separator + Rows emittiert. Der explizite `sb.AppendLine()` am Ende der neuen Methode repliziert das. Verifikation: `GetHotspotsToolTests` müssen unverändert grün sein.

### Datei 3: `src/AiNetLinter/Maps/HotspotMapBuilder.cs` (Z.45-46 + Imports)

- **Was:** In `Build` Z.45-46 die zwei `HotspotSectionFormatter.AppendSection(sb, "🔴 Kritische Dateien (>95% des Limits)", ...)`-Aufrufe durch lokale `AppendHotspotSection(sb, "🔴 Kritische Dateien (>95% des Limits)", critical, maxLineCount)` ersetzen. Neue private Methode `private static void AppendHotspotSection(StringBuilder sb, string heading, IReadOnlyList<StructureFileInfo> files, int maxLineCount)` (Map-Variante, nimmt `StructureFileInfo`-Records) hinzufügen, identische Builder-Logik wie in `GetHotspotsScanner`, aber Eingabe-Typ `StructureFileInfo` (hat `RelativePath` + `Lines` + `Directory` — `Directory` wird ignoriert, nur die ersten zwei Felder gehen in die Tabelle). Die `## ✓ Alle Dateien im grünen Bereich`/`## Alle anderen Dateien`-Sektionen Z.48-58 bleiben raw-`sb` (gleiche Begründung wie Datei 2). `using AiNetLinter.Output;` (Z.8) wird ungenutzt → entfernen.
- **Warum:** Konzept §3 Prio 4 + §6: Schicht-Trennung `Maps →` darf nicht `Mcp.Tools` referenzieren — die Scanner-`AppendHotspotSection` ist nicht wiederverwendbar, also lokale Duplikation. Wird in Tech-Debt-Log aufgenommen (siehe step-result „Beobachtungen" als Pflicht-Notiz; Konzept §3 Prio 4 letzter Bullet ist explizit so geplant).
- **Hinweis Konsolidierung:** Die Duplikation ist _gering_ — beide Methoden sind ~20 Z. mit identischer Builder-Konfiguration. Coder kann in einem privaten Helper innerhalb derselben Datei einen `MarkdownBuilder`-Aufbau kapseln, wenn das die Lesbarkeit verbessert; das ändert nichts an der Schicht-Trennung, weil der Helper lokal in der jeweiligen Datei lebt.

### Datei 4: `src/AiNetLinter/Commands/ListRulesCommand.cs` (Z.22-28)

- **Was:** In `ListAll` den Header-`sb.AppendLine` + Separator-`sb.AppendLine` + Foreach-Row-`sb.AppendLine`-Block ersetzen durch `var table = new MarkdownTableBuilder().AddColumn("RuleId").AddColumn("Bezeichnung").AddColumn("Intent").AddColumn("Severity").AddColumn("Auto-Fix"); foreach (var rule in RuleRegistry.All) { var autoFix = rule.HasAutoFix ? "ja (--fix)" : "-"; table.AddRow(rule.RuleId, rule.DisplayName, rule.Intent, rule.Severity, autoFix); } table.AppendTo(sb);`. Falls `ListAll` aktuell mit `var sb = new StringBuilder();` arbeitet, diesen `sb` weiterverwenden. Falls die Methode bereits einen `MarkdownBuilder` nutzt, stattdessen `mb.Table(table)`.
- **Warum:** Konzept §3 Prio 6 — trivialster Fall in EPIC-02. Kein Alignment (alle Spalten linksbündig → Default), keine `string.Join`-Verschachtelung, keine Escaping-Sorgen, `Auto-Fix` hat einen 2-Werte-Bool, der als fertiger String (`"ja (--fix)"` / `"-"`) reingereicht wird. _Bestätigung_, dass die Builder-Migration in einfachen Fällen ohne Alignment ergonomisch ist.
- **Hinweis:** Vor dem Umbau kurz prüfen, ob `ListAll` ein `return sb.ToString();` macht oder `Console.WriteLine(sb.ToString())` — die Builder-Variante `table.AppendTo(sb)` ist in beiden Fällen der direkte Ersatz; `MarkdownBuilder.Table(MarkdownTableBuilder)`-Instanz-Überladung ist hier _nicht_ nötig (kein `mb`-Objekt vorhanden).

### Datei 5: `src/AiNetLinter/Mcp/Tools/GetSymbolBodyTool.cs` (Z.60-64)

- **Was:** Den `string.Concat`-Block in `ExecuteAsync` Z.60-64 ersetzen durch:
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
  var markdown = isTruncated ? mb.Build().TrimEnd() : McpSufficiencyHints.Append(mb.Build().TrimEnd());
  ```
  Siehe „Bekannte Ausnahmen" unten für die `.TrimEnd()`-Begründung.
- **Warum:** Konzept §3 Prio 10. `Heading(3, "...\n")` + `BlankLine()` = `"...\n\n"` (byte-stabil zu `$"...\n\n"`). `Line("id: ...")` + `BlankLine()` = `"id: ...\n\n"` (byte-stabil zu `$"id: ...\n\n"`). `CodeBlock("csharp", body)` entspricht strukturell dem bestehenden ` ```csharp\n{body}\n``` `, mit einer wichtigen Subtilität bzgl. Trailing-Newline.
- **Risiko-Hinweis Byte-Stabilität:** `MarkdownBuilder.CodeBlock` (siehe `MarkdownBuilder.cs:137-147`) hängt _immer_ ein `\n` nach ` ``` ` an, das aktuelle `GetSymbolBodyTool` aber _nicht_. Wenn der Output 1:1 byte-stabil sein muss (MCP-Token-Vertrag), dann `mb.Build().TrimEnd()` vor der Übergabe an `McpSufficiencyHints.Append` (oder vorheriger Pfad) — das entfernt genau das überschüssige `\n`. `.TrimEnd()` ist hier sicher, weil der Original-Output nach `\n``` ` endet (kein anderes trailing whitespace). Alternative wäre `mb.Build()[..^1]` — unsicherer, weil bei leerem `body` (unmöglich) oder bei zukünftigen Builder-Änderungen. **Verifikation: das Coder-Ergebnis muss via `GetSymbolBodyToolTests` (FastTests) + `McpServerCommandContractTests` (Integration) grün laufen; sind die Tests gegen einen exakten String-Vergleich (nicht Contains/Trim), dann ist `.TrimEnd()` zwingend; sind sie tolerant, kann es entfallen — der Coder entscheidet nach Lesen der Test-Assertions.**

## Tests

- [ ] **Keine neuen Tests** — bestehende Tests verriegeln den byte-stabilen Vertrag:
  - `src/AiNetLinter.FastTests/Mcp/Tools/GetHotspotsToolTests` (Prio 4) — verifiziert `GetHotspotsScanner.FormatReport` byte-stabil (Heading + Tabelle + Sortierung identisch zur `HotspotSectionFormatter`-Variante).
  - `src/AiNetLinter.FastTests/Commands/ListRulesCommandTests` (Prio 6) — verifiziert `ListRulesCommand.ListAll` byte-stabil (Spalten, Reihenfolge, Trenner, Auto-Fix-Markierung).
  - `src/AiNetLinter.FastTests/Mcp/Tools/GetSymbolBodyToolTests` (Prio 10) — verifiziert `GetSymbolBodyTool.ExecuteAsync` byte-stabil (Heading + id-Zeile + Code-Block + Truncation-Marker).
  - `src/AiNetLinter.IntegrationTests/McpServerCommandContractTests` (Prio 10) — MCP-Token-Vertrag `get_symbol_body` End-to-End.
  - `src/AiNetLinter.IntegrationTests/CliRepositoryDogfoodTests` / `BaselineCliTests` (Prio 4 Map-Variante indirekt) — verifiziert CLI-Aufrufe `ainetlinter hotspot-map` byte-stabil.
- [ ] **Begründung „keine neuen Tests":** Die Migrationen sind 1:1-Replatzierungen, die Builders emittieren in diesen drei Fällen (einfache Tabelle, byte-stabiler Heading, byte-stabiler Code-Block) dieselben Strings wie die manuelle Implementierung. Tests würden nur das testen, was die Tests ohnehin schon testen.

## Definition of Done

- [ ] Alle „Konkrete Änderungen" (5 Dateien: 1 gelöscht + 4 geändert) umgesetzt
- [ ] `HotspotSectionFormatter.cs` ist aus dem Working Tree entfernt (per `git status` verifizieren — keine untracked Rest-Datei, keine Referenzen mehr in `rg "HotspotSectionFormatter"`)
- [ ] `using AiNetLinter.Output;` in `HotspotMapBuilder.cs` entfernt, falls nach Schritt 3 ungenutzt
- [ ] Build-Command aus Tech-Stack-Notiz (`roadmap.md`): `dotnet build` grün (0 Warnungen, 0 Fehler, `TreatWarningsAsErrors = true`)
- [ ] Test-Command FastTests: `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` grün — insbesondere `MarkdownBuilderTests` 30/30, `GetHotspotsToolTests` alle grün, `ListRulesCommandTests` alle grün, `GetSymbolBodyToolTests` alle grün, `GetViolationsToolTests` 16/16 (Regressions-Schutz EPIC-01), `ViolationMarkdownFormatterTests` 31/31 (Regressions-Schutz EPIC-01)
- [ ] Test-Command IntegrationTests: `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` grün — insbesondere `McpServerCommandContractTests` + `McpLiveRepositoryTests` (Prio 10 MCP-Token-Vertrag) + `CliRepositoryDogfoodTests` + `BaselineCliTests` (Prio 4 Map-Variante)
- [ ] Dogfood-Suite: `dotnet run -- --config rules.json --path .` grün (Linter findet keine Verletzungen im geänderten Code)
- [ ] Commit auf aktuellem Branch (Conventional Commit, deutsch, imperativ, Suffix `[markdown-builder]`, Body-Trailer `Refs: tasks/markdown-builder/step-003`)
- [ ] `step-003/step-result.md` geschrieben (inkl. Begründung für `.TrimEnd()` oder dessen Verzicht in Prio 10)
- [ ] Beobachtung in `step-result.md` zur Duplikation der `AppendHotspotSection` in `GetHotspotsScanner` + `HotspotMapBuilder` mit Verweis auf Schicht-Trennung `Maps → Mcp.Tools` (Konzept §3 Prio 4 letzter Bullet) — _kein_ Tech-Debt-Eintrag, weil Konzept das schon abdeckt (ist _Teil_ des Plans, nicht _nicht-gefixte_ Beobachtung)
- [ ] `status` in `step-plan.md` von `open` auf `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc` §1 (Dogfooding, MCP-Server-Pflicht) — C#-Symbol-Queries via `ainetlinter`-MCP-Server (`find_symbol`/`find_references`) **vor** `rg`/`grep`, gilt für Prio 4 (zwei Aufrufer + Helper) und Prio 10 (Aufrufer + Vertrags-Tests)
- `.agents/rules/AiNetLinterRichtlinien.mdc` (Schicht-Trennung) — `Maps →` darf nicht `Mcp.Tools` referenzieren: erzwingt die private Duplikation in Datei 3, verbietet eine `internal static`-Helper-Klasse in `Mcp.Tools.FileStructure` für beide Aufrufer
- `.agents/rules/AiNetLinter.mdc` (auto-generierte Metriken) — `sealed` auf alle konkreten Klassen (in diesem Step: keiner neuen Klassen, `HotspotSectionFormatter` ist gelöscht; bestehende `GetHotspotsScanner`/`HotspotMapBuilder`/`ListRulesCommand` sind bereits `internal static` und damit nicht betroffen), `MaxMethodLineCount: 60` für die neue `AppendHotspotSection` (~18 Z., weit drunter), `MaxMethodParameterCount: 4` (Prio 4 neue Methode hat 4 Parameter: `sb`, `heading`, `files`, `maxLineCount` — Grenze, aber zulässig)
- Konvention Pflicht-Suffix `[markdown-builder]` + Body-Trailer `Refs: tasks/markdown-builder/step-003` + Pflicht-`### Commit-Vorschlag`-Block in der Coder-Antwort

## Bekannte Ausnahmen

- **`MarkdownBuilder.CodeBlock`-Trailing-Newline (Prio 10):** Die Builder-Implementierung emittiert nach ` ``` ` immer ein `\n`, das aktuelle `GetSymbolBodyTool` aber nicht. Lösung: `mb.Build().TrimEnd()` einmalig vor der Übergabe an `McpSufficiencyHints.Append`. Coder muss _vor_ dem Commit kurz die Assertions in `GetSymbolBodyToolTests` (FastTests) lesen — wenn dort ein exakter String-Vergleich steht, ist `.TrimEnd()` zwingend; wenn nur Contains/Trim-Vergleich, entfällt es. Diese Entscheidung gehört in `step-result.md` dokumentiert. Risiko: niedrig — `TrimEnd()` ist hier semantisch sicher (kein anderes trailing whitespace im Original), und selbst wenn die Tests tolerant sind, schadet der `.TrimEnd()` nicht.
- **`HotspotSectionFormatter` Test-Lücke:** Es gibt keine dedizierte `HotspotSectionFormatterTests`-Klasse (verifiziert per `Get-ChildItem` — nur `GetHotspotsToolTests` deckt die `FormatReport`-Route byte-stabil ab). Beim Löschen der Datei gehen keine Test-Dateien verloren; das Verhalten ist vollständig über die Integration der beiden Aufrufer getestet.
- **TD-001 wird in diesem Step NICHT angehängt:** TD-001 betrifft `ViolationMarkdownFormatter.cs:40` (Header raw `sb.Append`). Diese Datei wird in step-003 _nicht_ angefasst (Prio 3 ist in EPIC-01 erledigt, nur `BuildSummaryTable` migriert; der Header blieb raw). Die opportunistische TD-001-Anhängung gehört zu step-005, in dem dieselbe Datei wegen TD-001 selbst erneut angefasst wird (siehe step-005-Plan, sobald er existiert). Hier nur: TD-001-Index _nicht_ verändern, Status bleibt „offen".
- **TD-002 wird in diesem Step NICHT obsoleted:** TD-002 ist „`Table(MarkdownTableBuilder)`-Überladung ungenutzt". Prio 4 (dieser Step) nutzt die Callback-Variante `Table(t => ...)` für die Hotspot-Tabellen — die Instanz-Überladung bleibt ungenutzt. TD-002 wird in step-004 (Prio 5 `RepoPlaybookGenerator.AppendAgentPriority`) produktiv eingesetzt und dort automatisch obsolet. Hier nur: TD-002-Index _nicht_ verändern, das Obsoleting ist die Konsequenz von step-004, dokumentiert dort.

## Code-Skizze (optional)

**`HotspotSectionFormatter` → `GetHotspotsScanner.AppendHotspotSection` (Migration, ~20 Z., identisch für `HotspotMapBuilder` mit `StructureFileInfo` statt `HotspotFileInfo`):**

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
    sb.AppendLine();  // Trailing-Blank nach Tabelle, byte-stabil zum Original
}
```

**`ListRulesCommand.ListAll` Migration (Prio 6, ~10 Z.):**

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
table.AppendTo(sb);
```

**`GetSymbolBodyTool.ExecuteAsync` Migration (Prio 10, ~10 Z., siehe „Bekannte Ausnahmen" für `.TrimEnd()`-Begründung):**

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
var markdown = mb.Build().TrimEnd();  // TrimEnd entfernt Trailing-\n aus CodeBlock
```

## Notes

- **Schritt-Größe vs. Anti-Loop:** Ralf-Vorgabe „sinnvoll große Code Steps, keine Mini oder Micro Steps" (2026-08-19). EPIC-02 umfasst 7 Callsites + 1 Helper-Löschung — das in _einem_ Step zu machen wäre 200+ LoC Diff über 5+ Dateien, _über der_ 8-Items/40-LoC-Batch-Deckelung aus `spec.md §10.6` und jenseits einer Review-Runde. Die 3-Wellen-Aufteilung (dieser Step + step-004 + step-005) balanciert: jede Welle ist 50-100 LoC, multi-file, in sich geschlossen, byte-stabil testbar. Eine _einzige_ Mega-Step-Variante wurde verworfen (siehe „Intention" oben).
- **Reihenfolge der Wellen ist bewusst:**
  1. **Welle 1 (step-003, dieser Plan):** Helper-Löschung + zwei einfache Migrationsfälle + ein byte-stabiler MCP-Fall → etabliert das Pattern, schafft eine Datei-Löschung (sichtbarer Fortschritt), testet das MCP-Token-Vertrags-Risiko isoliert an einem kleinen Call.
  2. **Welle 2 (step-004, geplant):** Drei `Generators`-Callsites (Prio 5, 7, 8) → konsistente Domäne, nutzt `Table(MarkdownTableBuilder)`-Instanz-Überladung produktiv (obsoleted TD-002).
  3. **Welle 3 (step-005, geplant):** `MetricsLookupFormatter` (Prio 9, komplette Datei, 4 Pattern-Arten, byte-stabil) + opportunistisch TD-001 (Header raw `sb.Append` in `ViolationMarkdownFormatter.cs`).
- **Bezug zu existierenden Patterns:**
  - Prio 4 folgt exakt dem Pattern aus Konzept §3 Prio 1/2 (`Table(t => ...)` Callback-Variante + dynamische Rows im Callback).
  - Prio 6 ist die Minimalversion davon (kein Alignment, alle Spalten Default-Left).
  - Prio 10 nutzt `Heading` + `Line` + `BlankLine` + `CodeBlock` — kein einziges Tabellen-Pattern, demonstriert den Builder-Anwendungsbereich über Tabellen hinaus (Konzept §1 Hauptmotivation).
- **Bezug zu TD-001 / TD-002 (Tech-Debt-Index, `tech-debt.md`):** Siehe „Bekannte Ausnahmen" — TD-001 gehört zu step-005, TD-002 wird durch step-004 obsolet. Kein Index-Eintrag in diesem Step anzufassen.
- **Bezug zu `MarkdownBuilderTests`:** Keine neuen Unit-Tests in diesem Step, weil die drei Callsites keine _neuen_ Builder-APIs einführen — sie konsumieren nur bestehende (`Heading`, `Line`, `BlankLine`, `CodeBlock`, `Table(t => ...)`). Die 30 bestehenden Tests in `MarkdownBuilderTests` verriegeln die API; byte-stabile Verifikation der Callsite-Outputs liegt bei den bestehenden Domain-Tests (`GetHotspotsToolTests`, `ListRulesCommandTests`, `GetSymbolBodyToolTests`, `McpServerCommandContractTests`).
- **Verifikations-Reihenfolge** (Empfehlung an den Coder):
  1. Erst `dotnet build` (Typ-Check).
  2. Dann `dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~GetHotspotsToolTests|FullyQualifiedName~ListRulesCommandTests|FullyQualifiedName~GetSymbolBodyToolTests|FullyQualifiedName~MarkdownBuilderTests|FullyQualifiedName~GetViolationsToolTests|FullyQualifiedName~ViolationMarkdownFormatterTests"` (zielt auf die hier betroffenen Tests + Regressions-Schutz, <30 s).
  3. Dann `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` (volle FastTests-Suite, <10 s).
  4. Dann `dotnet test src/AiNetLinter.IntegrationTests --filter "FullyQualifiedName~McpServerCommandContractTests|FullyQualifiedName~McpLiveRepositoryTests|FullyQualifiedName~CliRepositoryDogfoodTests|FullyQualifiedName~BaselineCliTests"` (MCP + CLI-Byte-Verträge, ~60 s).
  5. Dann `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` (volle Integration-Suite, ~120 s).
  6. Dann `dotnet run -- --config rules.json --path .` (Dogfood, ~5 s).
- **Commit-Pattern:** Schritt deckt 1 Löschung + 4 Datei-Änderungen mit zwei klaren Themen (Hotspot-Cleanup, ListRules-Migration, GetSymbolBody-Migration) ab — _ein_ Commit ist die richtige Wahl (logische Einheit, sonst würde ein Zwischen-Commit Hotspot-Aufrufer ohne Helper = Compile-Error erzeugen, kein valider Zwischen-State). Begründung in `step-result.md` analog zu step-002-Abweichung #2 („atomarer Commit, weil X").
