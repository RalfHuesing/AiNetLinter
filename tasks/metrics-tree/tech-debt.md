---
task: metrics-tree
type: tech-debt-log
maintained_by: kritiker
last_updated: 2026-08-08 (step-003)
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
| TD-001 | `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs` + `Mcp/Tools/MetricsTree/MetricsTreeTool.cs` | mittel | nein | `AIContextFootprint` auf zwei Klassen knapp über/nahe dem Limit — Config-Override-Kette via `McpCodeGraphServer` als gemeinsame Ursache, Facade-Extraktion prüfen. Betroffene Dateien seit step-003 verschoben, Druck unverändert/leicht gestiegen. **Status: erledigt (Tech-Debt-Fix TD-001, 2026-08-10)** |
| TD-002 | `src/AiNetLinter/Mcp/Tools/SolutionFileWalker.cs:23` | niedrig | ja | `WalkedFile`-Record-Struct verletzt `BanPublicNestedTypes` (internal nested Type) — Extraktion in eigene Datei. **Status: erledigt (step-002)** |

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
- **Status:** erledigt (Tech-Debt-Fix TD-001, 2026-08-10)
- **Update (step-003, Kritiker-Review vom 2026-08-08):** Registrierung von `metrics_tree` von
  `FileStructureToolRegistrations` nach `AnalysisToolRegistrations` verschoben (EPIC-02, gleicher
  Grund wie bei `get_violations`). Selbst verifiziert per `get_violations` (voller Scope): aktuell
  **keine** `AIContextFootprint`-Warnungen mehr, weil beide betroffenen Klassen frische
  `PathOverrides` in `rules.json` bekommen haben. Der Befund selbst ist dadurch **nicht entschärft,
  sondern verlagert und in der Tendenz verschärft**: `FileStructureToolRegistrations.cs` ist raus
  aus der Betroffenheit (kein Override mehr nötig, echter Fortschritt), aber
  `AnalysisToolRegistrations.cs`s Override musste bereits von 2870 auf 2910 angehoben werden
  (tatsächlicher Footprint jetzt 2905) und die neue `Mcp/Tools/MetricsTree/MetricsTreeTool.cs`
  braucht jetzt ebenfalls einen Override (2910, tatsächlicher Footprint 2897) — beide Werte liegen
  nur noch ca. 5-13 Zeilen unter ihrem jeweiligen Override, praktisch keine Reserve mehr für
  künftiges Wachstum in diesem Codebereich. Die zugrunde liegende Ursache (drei
  Config-Override-Typen `GlobalConfigOverride`/`MetricsConfigOverride`/`TestSentinelConfigOverride`,
  transitiv über `McpCodeGraphServer`) ist unverändert offen — der Facade-Vorschlag bleibt gültig,
  Priorität weiterhin `mittel`, jetzt mit geringerer Restreserve als zuvor.
- **Update (Tech-Debt-Fix TD-001, 2026-08-10):** Genau die im vorherigen Update vorgeschlagene
  Facade-Extraktion umgesetzt — allerdings nicht als eigener Aggregations-Typ, sondern durch
  vollständige Entfernung der `Apply`-Instanzmethoden aus den Config-Records selbst
  (`GlobalConfig`, `MetricsConfig`, `TestSentinelConfig`, `UiSeparationConfig`, `WebConfig`,
  `CssConfig`, `JsConfig`, `RazorConfig`). Die Merge-Logik lebt jetzt in neuen `internal static
  class *ConfigApplier`-Klassen (`GlobalConfigApplier.cs`, `TestSentinelConfigApplier.cs`,
  `UiSeparationConfigApplier.cs`, `WebConfigApplier.cs`, plus Erweiterung von
  `MetricsConfigApplier.cs` um eine `Apply`-Einstiegsmethode — analog zum bereits bestehenden
  Präzedenzfall für `MetricsConfig`). Dadurch referenzieren die Config-Record-Typen selbst keine
  `*ConfigOverride`-Typen mehr als Member, wodurch diese (353 + 95 Zeilen) nicht mehr transitiv in
  jeden `ILinterEngineConfig`-Konsumenten (u. a. `McpCodeGraphServer` und alle MCP-Tool-Klassen)
  gezogen werden. Einzige Call-Site aller `.Apply(...)`-Aufrufe war
  `ProjectConfigResolver.MergeConfig`, umgestellt auf die neuen statischen Aufrufe; zusätzlich
  3 Call-Sites in `PathOverridesTests.cs` angepasst. Verifiziert per `get_violations` (MCP-Server,
  `MaxAIContextFootprint` testweise auf 1 gesetzt um die tatsächlichen Werte sichtbar zu machen):
  - `AnalysisToolRegistrations.cs`: 2905 (PathOverride 2910) → **2363** (PathOverride entfernt)
  - `Mcp/Tools/MetricsTree/MetricsTreeTool.cs`: 2897 (PathOverride 2910) → **2354** (PathOverride entfernt)
  - `Mcp/Tools/SafeguardTool.cs`: ~2795 (PathOverride 2800) → **2004** (PathOverride entfernt)
  - `FileStructureToolRegistrations.cs`: ~2885 (PathOverride 2890) → **2320** (PathOverride entfernt)

  Alle vier Dateien liegen jetzt ca. 140–500 Zeilen unter dem globalen Default-Limit (2500) —
  echte Reduktion statt Grenzwert-Anhebung, alle vier `PathOverrides` für diese Dateien aus
  `rules.json` entfernt (keine reduziert, weil alle unter den Default fielen). `dotnet build`
  bleibt grün (0 Warnungen/0 Fehler), `dotnet test --filter Category!=Stress` grün,
  `get_violations` (voller Scope) zeigt nur die 3 vorbestehenden, unveränderten
  `AllowDynamic`-Fixture-Fehler in `tests/Fixtures/DiRegistrationMini/`. Root-Cause vollständig
  behoben — kein Facade-Aggregations-Typ nötig, die einfachere Lösung (Merge-Logik aus dem Record
  entfernen statt Override-Typen bündeln) reicht aus, weil die Records selbst nie eine `*Apply`-API
  nach außen anbieten mussten.

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
- **Status:** erledigt (step-002) — `WalkedFile` in eigene Datei
  `src/AiNetLinter/Mcp/Tools/WalkedFile.cs` extrahiert, `get_violations` bestätigt keinen
  `BanPublicNestedTypes`-Verstoß mehr.
