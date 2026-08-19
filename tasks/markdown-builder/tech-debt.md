---
task: markdown-builder
type: tech-debt-log
maintained_by: kritiker
last_updated: 2026-08-19
---

# Tech-Debt-Log: markdown-builder

Append-only. Jeder Eintrag ist eine vom Kritiker während eines
Step-Reviews beobachtete, aber bewusst **nicht** gefixte Auffälligkeit
außerhalb des Scopes des jeweiligen Steps (Architektur, Anti-Pattern,
Duplikation, Konsistenz) — siehe `../spec.md` §8.3/§9.

**Priorität ist reine Sortierhilfe für den Menschen, kein Auslöser.**
Bewusst `hoch`/`mittel`/`niedrig` (deutsch) statt `CRITICAL`/`MAJOR`/
`MINOR`, um jede Verwechslung mit den blockierenden Findings in
`step-review.md` auszuschließen — kein Eintrag hier führt automatisch zu
einem eigenen Korrektur-Step oder einem neuen Epic. Das entscheidet
grundsätzlich der Nutzer (manuell, z. B. durch Ergänzen eines Epics in
`roadmap.md` mit Verweis auf die Tech-Debt-ID).

**`auto_fixable` (`ja`/`nein`, siehe `../spec.md` §9.1) ist die einzige
Ausnahme:** rein mechanische, entscheidungsfreie Fixes ohne
Architektur-Ermessen dürfen vom Planer opportunistisch an einen ohnehin
laufenden Step angehängt werden (§10.6) — kein eigener Step, kein
eigener Sweep. Default bei Unsicherheit ist `nein`.

## Index

| ID | Bereich / Datei | Priorität | Auto-Fixable | Kurzfassung |
|---|---|---|---|---|
| TD-001 | `src/AiNetLinter/Output/ViolationMarkdownFormatter.cs:40` | niedrig | ja | `# AiNetLinter - N violations`-Header ist `sb.Append($"...")` statt `MarkdownBuilder.Heading(1, ...)` — Builder-Kandidat für EPIC-02+. |
| TD-002 | `src/AiNetLinter/Output/MarkdownBuilder.cs:141` | niedrig | nein | ~~`Table(MarkdownTableBuilder)`-Instanz-Überladung wird in EPIC-01 nur getestet, nicht produktiv genutzt; produktive Nutzung erst in EPIC-02 (Prio 4/5).~~ **Obsolet durch step-004** (Prio 5/7/8): Überladung wird in 4 produktiven Callsites genutzt. |

## Einträge

### TD-001 — `ViolationMarkdownFormatter` Header auf `MarkdownBuilder` umstellen [Priorität: niedrig] [Auto-Fixable: ja]

- **Gefunden in:** step-001 (Kritiker-Review vom 2026-08-19)
- **Ort:** `src/AiNetLinter/Output/ViolationMarkdownFormatter.cs:40`
  (`output.Append($"# AiNetLinter - {violations.Count} violations\n")`)
- **Befund:** Der Top-Level-Report-Header wird weiterhin mit raw `sb.Append`
  gebaut, während der Rest der Datei in EPIC-01 auf `MarkdownBuilder`
  umgestellt wurde (nur `BuildSummaryTable` im Scope, nicht `Format`).
  Inkonsistenz im selben File: Heading-Format geht durch den Builder,
  Heading-Format darüber geht dran vorbei.
- **Warum nicht sofort gefixt:** Außerhalb des Scopes von step-001 — Plan
  deckt nur `BuildSummaryTable` (Prio 3) ab, nicht den `Format`-Header.
  Gehört zur Konzept-Aufräumung in EPIC-02 (siehe CodeMap-Eintrag zum
  Header im step-result „Beobachtungen") oder als eigenes Mini-Refactor
  in einem EPIC-02-Substep.
- **Vorschlag:** `MarkdownBuilder.Heading(1, $"AiNetLinter — {violations.Count} violations")` plus `BlankLine` ersetzt die Zeile — rein mechanisch,
  keine Verhaltensänderung, deshalb `auto_fixable: ja`.
- **Auto-Fixable:** ja — rein mechanische Ersetzung, kein
  Architektur-Ermessen, keine Verhaltensänderung (Heading-Output bleibt
  byte-stabil, weil `MarkdownBuilder.Heading` bare `\n` emittiert wie
  das aktuelle `Append($"…\n")`).
- **Status:** offen

### TD-002 — `MarkdownBuilder.Table(MarkdownTableBuilder)` ungenutzt in EPIC-01 [Priorität: niedrig] [Auto-Fixable: nein]

- **Gefunden in:** step-001 (Kritiker-Review vom 2026-08-19)
- **Ort:** `src/AiNetLinter/Output/MarkdownBuilder.cs:141-145`
- **Befund:** Die `Table(MarkdownTableBuilder)`-Instanz-Überladung ist
  implementiert und durch `TableInstanceUeberladung_GibtOutputInBuilder`
  und `TableCallback_und_InstanceUeberladung_GleicherOutput` getestet,
  wird aber in keinem der drei EPIC-01-Callsites produktiv genutzt.
  Konzept §2 API-Erweiterung sieht sie für Prio 4 (`HotspotSectionFormatter`)
  und Prio 5 (`RepoPlaybookGenerator.AppendAgentPriority`) vor — beides
  EPIC-02. Anti-Loop-Hinweis: der EPIC-02-Planer darf die Überladung
  nicht versehentlich zurückdrehen, weil die Konzept-Skizze sie
  vorsieht. Die jetzige Implementierung ist API-vorausschauend, aber
  „toter Code" aus Test-Sicht.
- **Warum nicht sofort gefixt:** Außerhalb des Scopes von step-001 — die
  Überladung muss in EPIC-01 existieren, weil Prio 1 (bedingt Spalte
  zwischen Header und Rows) sie konzeptuell benötigt. Die Tatsache,
  dass Prio 1 in der finalen Implementierung stattdessen die Spalte
  vor `AddRow`-Aufrufen hinzufügt und die Instanz-Übergabe nicht
  nutzt, ist eine Erkenntnis aus der Umsetzung — die Überladung bleibt
  als API-Reserve für EPIC-02.
- **Vorschlag:** Nichts in EPIC-01. In EPIC-02 step-002 (Prio 4/5)
  produktiv einsetzen, dann ist TD-002 automatisch obsolet. Falls EPIC-02
  die Überladung doch nicht braucht: `MarkdownBuilder.Table(MarkdownTableBuilder)`
  + zugehörige Tests entfernen (dann Auto-Fixable, aber architektonische
  Entscheidung, also `nein`).
- **Auto-Fixable:** nein — Entfernen wäre Architektur-Ermessen (LoC
  gegen API-Erweiterbarkeit); Behalten ist API-Stabilität. Keine
  rein mechanische Korrektur.
- **Status:** obsolet (aufgelöst durch step-004 — produktive Nutzung in 4 Callsites)
