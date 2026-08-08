---
task: metrics-tree
type: tech-debt-log
maintained_by: kritiker
last_updated: 2026-08-08
---

# Tech-Debt-Log: metrics-tree

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
| TD-001 | `src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs` + `MetricsTreeTool.cs` | mittel | nein | `AIContextFootprint` auf zwei Klassen knapp über/nahe dem Limit — Config-Override-Kette via `McpCodeGraphServer` als gemeinsame Ursache, Facade-Extraktion prüfen. |
| TD-002 | `src/AiNetLinter/Mcp/Tools/SolutionFileWalker.cs:23` | niedrig | ja | `WalkedFile`-Record-Struct verletzt `BanPublicNestedTypes` (internal nested Type) — Extraktion in eigene Datei. |

## Einträge

### TD-001 — AIContextFootprint-Druck durch Config-Override-Kette [Priorität: mittel] [Auto-Fixable: nein]

- **Gefunden in:** step-001 (Kritiker-Review vom 2026-08-08)
- **Ort:** `src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs:21` (2894 > 2890) und
  `src/AiNetLinter/Mcp/Tools/MetricsTreeTool.cs:21` (2532 > 2500) — beide `AIContextFootprint`-Warnungen
  (nicht Fehler, `dotnet build` bleibt grün), verifiziert per `get_violations`.
- **Befund:** Beide Klassen überschreiten das `AIContextFootprint`-Limit (2500 transitive Zeilen)
  knapp, mit identischem Top-Treiber: `GlobalConfigOverride`/`MetricsConfigOverride`/
  `TestSentinelConfigOverride` (je 354 Zeilen), transitiv über `McpCodeGraphServer` eingeschleppt.
  `FileStructureToolRegistrations` war laut Coder-Beobachtung in `step-result.md` bereits vor diesem
  Step knapp am Limit — die neue `metrics_tree`-Registrierung war dort „der letzte Tropfen". Die
  zweite Warnung auf `MetricsTreeTool` selbst (gleicher Grund, gleiche drei Config-Override-Typen) war
  im Coder-Bericht nicht erwähnt, ändert aber nichts an der Einschätzung — beide sind Symptom
  derselben Ursache, keine zwei unabhängigen Probleme.
- **Warum nicht sofort gefixt:** Der Step-Plan hatte dieses Risiko in „Aktueller Projektzustand" Punkt
  6 bereits als bekannt vermerkt, aber bewusst keine Gegenmaßnahme (Facade o. ä.) für diesen Step
  vorgesehen — eine Facade-Extraktion für die drei Config-Override-Typen ist Architektur-Ermessen und
  beträfe mehrere, auch außerhalb dieses Steps liegende Aufrufer von `McpCodeGraphServer`.
- **Vorschlag:** Facade/Aggregations-Typ für `GlobalConfigOverride`/`MetricsConfigOverride`/
  `TestSentinelConfigOverride` prüfen, der von `McpCodeGraphServer`-Konsumenten referenziert wird statt
  aller drei Einzeltypen — reduziert den Footprint-Beitrag für alle betroffenen Klassen gleichzeitig.
  Spätestens vor EPIC-02 (zieht zusätzlich `LinterEngine` nach, laut Step-Plan bereits als Prüfpunkt
  für den nächsten Step vermerkt) relevant.
- **Auto-Fixable:** nein — Facade-Design ist Architektur-Ermessen, keine mechanische Korrektur.
- **Status:** offen

### TD-002 — `WalkedFile` verletzt BanPublicNestedTypes [Priorität: niedrig] [Auto-Fixable: ja]

- **Gefunden in:** step-001 (Kritiker-Review vom 2026-08-08)
- **Ort:** `src/AiNetLinter/Mcp/Tools/SolutionFileWalker.cs:23` — `internal readonly record struct
  WalkedFile(string RelativePath, string AbsolutePath)`, verschachtelt in `SolutionFileWalker`.
- **Befund:** `get_violations` meldet einen Fehler (Severity `error` in `rules.json`, `Intent:
  agent-context`) auf `BanPublicNestedTypes`: `WalkedFile` ist ein `internal` nested Type, die Regel
  verlangt Extraktion auf Namespace-Ebene, damit der Typ per Datei-Listing/Grep für LLMs sichtbar
  bleibt. Diese Regel ist nicht Teil der im `step-plan.md` unter „Rules-Refs" zitierten Abschnitte
  (`AiNetLinter.mdc` §„Kurz-Stil"/§„Grenzwerte") und taucht in der aktuellen `AiNetLinter.mdc`-Kurz-
  fassung überhaupt nicht auf — daher kein Ebene-2-Finding in diesem Review, sondern Tech-Debt.
  `dotnet build` bleibt grün, da dies eine reine Linter-Meldung (eigener Dogfooding-Lauf) ist, kein
  Compiler-Diagnostic.
- **Warum nicht sofort gefixt:** Außerhalb des vom Planer für diesen Step kuratierten Rules-Refs-
  Scopes — kein Blocker für dieses Review, aber ein reales, aktives Projekt-Rule mit Error-Severity.
- **Vorschlag:** `WalkedFile` aus `SolutionFileWalker.cs` in eine eigene Datei
  `SolutionFileWalker.WalkedFile.cs` (oder `WalkedFile.cs` auf Namespace-Ebene) extrahieren — reine
  Verschiebung, keine Verhaltensänderung.
- **Auto-Fixable:** ja — mechanische Extraktion ohne Architektur-Ermessen, keine Verhaltensänderung.
- **Status:** offen
