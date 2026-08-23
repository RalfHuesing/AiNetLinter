---
status: done
type: step-result
task: 11_epic-projektregistry-und-daemon
step: 001              # flach, Task-weite Sequenz — auch Korrekturen liegen hier, nie in einem Unterordner
epic: EPIC-A
step_type: single  # single | batch — aus step-plan.md übernehmen
coded_by: coder
coded_by_model: stealth/ox-alpha (openrouter)
coded_by_model_knowledge_cutoff: nicht deklariert (kein Cutoff im eigenen System-Prompt angegeben)
coded_at: 2026-08-23T13:52:00+02:00
code_commit_hash: e0b25033  # Commit mit Code+Tests
status_after: done  # done | blocked
blocker_category: n/a  # n/a | content | infrastructure
---

# Result Step 001: Projektregistry-Grundlage: Definitionsdatei, Loader, Fehlerverträge, Config-Materialisierung

## Zusammenfassung

Die Definitionsdatei-Ebene des Epics A steht als getestete Einheit: `ProjectDefinitionLoader`
liest `ainetlinter.project.json`, verlangt beide Pflichtfelder, löst relative Pfade
ausschließlich relativ zur Definitionsdatei auf, prüft die Existenz beider Zieldateien und
antwortet bei allen vier loader-seitigen Fehlerfällen deterministisch mit dem vorgeschriebenen
Textaufbau (`PROJECT_NOT_INITIALIZED` inkl. wörtlichem Template-Block). `ProjectInstanceFactory`
materialisiert daraus `McpCodeGraphServerOptions` und ist zugleich der gemeinsame Kern, auf den
`ResolveConfig`/`ResolveMaxLineCount` im Batch-Kommando delegieren — eine Pipeline statt zweier.
Contract-Tests wurden zuerst rot angelegt (fehlende Typen = Compilefehler), dann grün gemacht;
die beiden projectRoot-Codes liegen als Konstanten bereit, werden aber erst mit dem Wiring
ausgegeben.

## Geänderte Dateien

- `src/AiNetLinter/Mcp/Projects/ProjectDefinition.cs` (neu) — Record `SolutionPath`/`RulesPath`,
  Absolutheits-/Existenzgarantie liegt beim Loader.
- `src/AiNetLinter/Mcp/Projects/ProjectErrorCodes.cs` (neu) — alle sechs A.5-Codes als `const`;
  vier loader-seitig genutzt, `PROJECT_ROOT_*` für den späteren Wiring-Step.
- `src/AiNetLinter/Mcp/Projects/ProjectDefinitionLoadResult.cs` (neu) — flacher Result-Record
  (`Succeeded` + statische Fabriken `Success`/`Failure`), keine Exceptions für erwartbare Fehler.
- `src/AiNetLinter/Mcp/Projects/ProjectDefinitionLoader.cs` (neu) — Laden/Parsen/Pflichtfelder/
  Anker-Auflösung/Existenzprüfung, kein Fallback-Zweig; mehrzeilige Agentenmeldungen.
- `src/AiNetLinter/Mcp/Projects/ProjectInstanceFactory.cs` (neu) — `MaterializeRules(rulesPath,
  isRequired)` als geteilter Kern (Batch + Registry) und `Create(ProjectDefinition)` über die
  bestehende `McpCodeGraphServerOptions.From(...)`-Pipeline (`UsedDefaultConfig=false`,
  `ResolvedConfigPath`=aufgelöster rules-Pfad); plus `MaterializedRules`-Record.
- `src/AiNetLinter/Commands/McpServerCommand.cs` — `ResolveMaxLineCount`/`ResolveConfig`
  delegieren auf `ProjectInstanceFactory.MaterializeRules`; Signaturen unverändert,
  Pfadauflösung/Auto-Suche/Nachbar-Fallback vollständig batch-seitig belassen (F8).
- `src/AiNetLinter.FastTests/Mcp/Projects/ProjectDefinitionLoaderTests.cs` (neu) — 11 Contract-
  Tests: Ankerregel gegen die Lage der Definitionsdatei (nicht cwd), absolute Pfade unverändert,
  fehlende Felder/defektes JSON/nicht-string Feld → `PROJECT_DEFINITION_INVALID` ohne Teil-
  Initialisierung, fehlende Datei UND fehlendes Root → `PROJECT_NOT_INITIALIZED` mit
  Text-Assertion auf den exakten Template-Block, `SOLUTION_NOT_FOUND`/`RULES_NOT_FOUND` mit
  aufgelöstem absolutem Pfad, Kein-Fallback-Vertrag (Nachbarn ignoriert, F8).
- `src/AiNetLinter.FastTests/Mcp/Projects/ProjectInstanceFactoryTests.cs` (neu) — 3 Tests:
  Options aus Definition (Config/rules-Pfad, MaxLineCount, `ResolvedConfigPath`,
  `UsedDefaultConfig=false`), MaxLineCount-Gleichheit mit der bisherigen Batch-Pipeline,
  Default-Rückfallebene bei leerem Pfad.

## Commit

- **Code-Commit-Hash:** `e0b25033`
- **Message:**
  ```
  feat(mcp): Lege Projektregistry-Basis mit Definitionsloader an

  Neu unter src/AiNetLinter/Mcp/Projects/: ProjectDefinition,
  ProjectDefinitionLoader (beide Felder Pflicht, Anker Definitionsdatei,
  Existenzpruefung beider Ziele, kein Fallback), ProjectErrorCodes (alle
  sechs Fehlervertraege) und ProjectInstanceFactory als gemeinsame
  Config-Materialisierung fuer Batch- und Registry-Pfad.
  ResolveConfig/ResolveMaxLineCount delegieren auf den geteilten Kern;
  das Batch-Verhalten bleibt unveraendert (Contract-Tests pinnen es).

  Refs: tasks/mcp-server-weiterentwicklung/11_epic-projektregistry-und-daemon/step-001
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier
  drin — Selbstbezug, siehe `git log`).

## Build-/Test-Output

```
dotnet build                                                              → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter Category=Unit              → grün (1154 Tests, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter "Category=Unit&
  FullyQualifiedName~Projects"                                            → grün (16 Tests, 0 Fehler)
Gate: dotnet test src/AiNetLinter.FastTests --filter Category!=Stress     → grün (1642 Tests, 0 Fehler)
Gate: dotnet test src/AiNetLinter.IntegrationTests --filter
  Category!=Stress                                                        → grün (350 Tests, 0 Fehler)
```

Quality-Gates (vor dem Code-Commit): `get_violations` → 0 Verstöße (614 Dateien);
`safeguard` (Scope `src/AiNetLinter/Mcp/Projects`) → 10/10 PASS;
`metrics_lookup` (7 neue/geänderte Symbole) → alle Grenzwerte OK (größter Footprint 762 ≤ 2500,
größte Datei 99 LOC ≤ 500, CC/CogC/Parameter weit unter Limit).

## Abweichungen vom Plan

Keine — Plan 1:1 umgesetzt. Die vom Plan ausdrücklich dem Coder überlassene Wahl fiel so aus:
Result-Form = eigenes Record `ProjectDefinitionLoadResult` (flach, mit `Succeeded`/Fabriken),
statt out-Paar; `MaterializedRules` als Rückgabe des geteilten Kerns.

## Beobachtungen

- **Dogfooding griff sofort:** Der erste Entwurf des Result-Typs nutzte verschachtelte Records
  (`Loaded`/`Failed`) — `get_violations` meldete `BanPublicNestedTypes` (2 Fehler), woraufhin
  auf den flachen Record umgestellt wurde. Gate-Workflow funktioniert wie gedacht.
- Defekte `rules.json` (lesbar, aber ungültiger Inhalt) laufen auch künftig über
  `TryLoadConfig` → stderr-Meldung + stummer Default-Fallback. Für den Batch-Pfad ist das
  gepinnt korrekt; im Registry-Pfad führt der Wiring-Step vermutlich einen Wunsch nach einem
  eigenen Vertrag dafür heran (Konzept A.5 kennt nur `RULES_NOT_FOUND` für fehlende Dateien).
  Hinweis für den Kritiker als potenziellen Tech-Debt-/Konzeptpunkt, nicht in diesem Step
  lösbar ohne Vertragsänderung.
- `ConfigLoader.TryLoadConfig` schreibt Diagnosen selbst nach `Console.Error` (kein injizierbarer
  Kanal). Wenn der Daemon (Epic B) mehrere Projekte bedient, mischen sich dessen Meldungen mit
  Tool-Antwortpfaden; eine Injektion des Ausgabekanals wäre dort ggf. nötig. Außerhalb dieses
  Steps.

## Bekannte Unschärfen

- Bei leerem/Whitespace-`projectRoot` baut der Loader einen relativen Definitionsdatei-Pfad und
  der Existenzcheck läuft implizit gegen den Prozess-cwd — der Anker-Vertrag ist formal verletzt.
  Der Pfad ist erst mit dem Wiring erreichbar (dort fangen `PROJECT_ROOT_REQUIRED`/
  `PROJECT_ROOT_INVALID` das ab); bis dahin unerreichbar und nicht contract-getestet.
- Die erste Zeile des `PROJECT_NOT_INITIALIZED`-Templates substituiert `<root>` durch den
  tatsächlichen Definitionspfad (Konzept fordert „erwarteter Pfad + kopierfähiges Template");
  JSON-Block und Schlusssatz sind wörtlich übernommen und per Text-Assertion gepinnt. Falls der
  Kritiker `<root>` literal erwartet: nur die erste Zeile ändert sich.
- Mehrzeilige Fehlermeldungen verwenden `Environment.NewLine` (Windows: CRLF) — konsistent mit
  dem Windows-only-Final-Pass; über MCP-JSON unverändert transportiert.

## Falls Status `blocked`

**Blocker-Art:** n/a

**Blockiert weil:** n/a — Step fertiggestellt.

**Brauche von Nutzer:** n/a

**Aktueller Stand:** n/a
