---
status: done
type: step-review
task: speedup-tests
step: 023
epic: EPIC-5
step_type: batch
reviewed_by: kritiker
reviewed_by_model: gpt-5.6-terra
reviewed_by_model_knowledge_cutoff: nicht ausgewiesen
reviewed_at: 2026-08-13
verdict: issues
tech_debt_ids: [TD-011]
---

# Review Step 023: Config-/Suppression-Dateikohorte und EPIC-5-Grenzgate

## Verdict

- [ ] **approved**
- [x] **issues** — die zwei verbindlichen EPIC-5-Korrektheitsprofile sind nicht gruen nachgewiesen.
- [ ] **blocked**

## Geprüft

- [x] Plan-Erfüllung und Batch-Inventar: 21 Zielklassen vorhanden, 12 in FastTests und 9 in IntegrationTests; keine der 21 Quellen bleibt im Legacy-Projekt.
- [x] Rules-Konformität: Kategorien entsprechen den Zielassemblies; keine neue Ziel-Callsite fuer `SourceFileCatalog.LoadAsync`, Prozessstart oder globale Collection gefunden.
- [x] Logische Korrektheit: Rename-Diff `312b652` erhaelt Assertions weitgehend unveraendert; die 98/41 Ziel-Runner sowie statischer Fast-Dependency- und Ledger-/Legacy-/Kategorieguard sind laut Result gruen.
- [x] Konzept-Treue: die reine Policy-/Roslyn- gegen Datei-/Commandadapter-Grenze, Temp-/Fixture-Isolation und der Ausschluss von Dogfood/Performance/Stress folgen Konzept und Plan.
- [x] TD-003: `rules.json` und der generierte Abschnitt in `.agents/rules/AiNetLinter.mdc` enthalten beide Override-Keys `*Tests` und `AiNetLinter.TestKit`; der engere Generatorvertrag deckt beide ab.
- [x] TD-010: bleibt korrekt offen; nach dem Entfall von `DisableAllCliTests` sind die dokumentierten 20 Legacy-Konsumenten der alten Workspace-Familie weiterhin vorhanden.
- [x] Dokumentierte Einzel-Gates: Build, 139 Legacy-Runner, Fast 98, Integration 41, Ledger-/Legacy-/Kategorieguard 6, statischer Fast-Dependencyguard 2 und Roslyn-Teilmenge 55 gruen; `git diff --check` fuer beide Step-Commits ist sauber.

## Befund

### Plan-Erfüllung

Die Kohortenmigration, Ledger-Status und die notwendige Config-Synchronisation sind vollstaendig; Item 08 verlangt jedoch ausdruecklich beide EPIC-5-Profilgrenzen gruen (`step-plan.md:265-268`) und die Definition of Done wiederholt dies (`step-plan.md:287-288`).

### Rules-Konformität

Die verschobenen Klassen tragen konsistent `Unit` beziehungsweise `Integration`; der Step fuehrt weder eine vorsorgliche Serialisierung noch die ausgeschlossenen Prozess-/MSBuild-/Dogfood-Wege ein.

### Logische Korrektheit

Die gezielten Zielkohorten belegen die Migration; der volle Fast-Lauf hat zwar 777 fachliche Tests bestanden, endet aber danach reproduzierbar im Runtime-Dependency-Guard wegen dynamisch geladener `Microsoft.CodeAnalysis.Workspaces.MSBuild`; der volle Integration-Lauf haengt bzw. scheitert in realen MSBuild-/MCP-Loadbudget-Klassen.

### Konzept-Treue (Ebene 4)

Kein Non-Goal wurde umgesetzt; die fehlschlagenden Profile sind nach Commit-Diff und den separat gruennen Step-023-Roslyn-55 beziehungsweise Ziel-41 nicht ursächlich auf die verschobene Kohorte zurueckfuehrbar, aber ihre Gruenheit ist als EPIC-Grenze zugesagt.

### Build-/Test-Status

Die folgenden Ergebnisse sind durch `step-result.md` dokumentiert und wurden im Review statisch gegen Diff, Kohorte und Guards plausibilisiert; kein Voll-, Stress-, Dogfood- oder Performance-Lauf wurde erneut gestartet.

```
dotnet build → gruen (dokumentiert)
21er Legacy-Filter → gruen (139 Runner, dokumentiert)
12er Fast-Zielkohorte / 9er Integration-Zielkohorte → gruen (98 / 41, dokumentiert)
Ledger-/Legacy-/Kategorieguard / statischer Fast-Dependencyguard / Roslyn-Teilmenge → gruen (6 / 2 / 55, dokumentiert)
Category=Unit|Category=Component (Fast) → nicht gruen: RuntimeDependencyGuard-Cleanup
Category=Integration (Integration) → nicht gruen: vorbestehende MSBuild-/MCP-Loadbudget-Haenger bzw. -Fehler
```

## Findings

1. **item-08** — [MAJOR] [Plan-Erfüllung] `tasks/speedup-tests/step-023/step-plan.md:265-268,287-288`: Das verbindliche Fast-Grenzgate ist nicht gruen. Die beobachtete Signatur folgt erst nach 777 fachlich gruennen Tests und betrifft den bereits vor Step 023 vorhandenen Runtime-Guard; sie ist damit kein belegter Migrationsdefekt, aber ein unerfuelltes DoD-Gate. **Fix:** In einem flachen Korrektur-Step den ausloesenden Fast-Test bzw. Assembly-Load reproduzierbar isolieren und die zulässige Guard-/Testisolation so korrigieren, dass der vollstaendige Unit/Component-Filter ohne Cleanup-Fehler endet; danach das Gate neu dokumentieren.
2. **item-08** — [MAJOR] [Plan-Erfüllung] `tasks/speedup-tests/step-023/step-plan.md:267-268,287-288`: Das verbindliche Integration-Grenzgate ist nicht gruen. Die betroffenen realen MSBuild-/MCP-Loadbudget-Klassen sind nicht Teil von `312b652` und die neun Step-023-Klassen sind separat mit 41 Tests gruen, doch ein gezieltes Beenden haengender Prozesse ersetzt den geforderten erfolgreichen Profil-Lauf nicht. **Fix:** In einem flachen Korrektur-Step die konkreten vorhandenen Haenger/Loadbudget-Fehler in Einzelprozessen eingrenzen, ihre Kategorie bzw. begrenzte Load-/Fixture-Lebensdauer korrekt reparieren und den vollstaendigen `Category=Integration`-Lauf gruen nachweisen; keine Dogfood-, Performance- oder Stresskohorte in das Gate ziehen.

## Tech-Debt-Einträge aus diesem Review

- `TD-011` (siehe `tech-debt.md`) — Die exakte private `FindSolutionRoot`-Duplikation von LoadedFixture und ihrem Test liegt ausserhalb der Step-023-Kohorte.

## Drift-Audit

Der durch den Hauptagenten ausgefuehrte MCP-Scan (`scopeDir=src`, `minTokens=20`, 12 exact/20 near) ist vollstaendig bewertet: TD-006 (CategoryTraits), TD-007 (Skeleton-CreateConfig), TD-008 (sechs Fast/Legacy-Helfer plus CompileErrorHeader nun auch mit lokalem Integration-Helper) und TD-010 (CopyDirectory/IsGeneratedPath) bleiben begruendet offen; TD-011 erfasst die exakte LoadedFixture-Rootsuche. Die Near-Cluster der Step-023-AgentFeatures-, CompoundSuppression- und SuppressionResolver-Matrix sind fachlich unterschiedliche Vertragsfaelle und kein Konsolidierungsfall.
