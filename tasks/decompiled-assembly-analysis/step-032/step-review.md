---
status: done
type: step-review
task: decompiled-assembly-analysis
step: 032
epic: EPIC-04
step_type: single
reviewed_by: kritiker-agent
reviewed_by_model: gpt-5 (Codex)
reviewed_by_model_knowledge_cutoff: nicht angegeben
reviewed_at: 2026-08-30T00:06:47+02:00
verdict: issues
tech_debt_ids: []
---

# Review Step 032: Validated Refresh/Fetch in neue Cache-Generation

## Verdict

- [ ] **approved** — die Implementierung ist plausibel, aber der Commit enthält keinen vollständig reproduzierbaren MCP-/Safeguard-Nachweis.
- [x] **issues** — ein gebündelter Korrekturscope für die Review-/Audit-Evidenz ist erforderlich; es wurde kein Produktionsfix vorgenommen.
- [ ] **blocked** — keine Nutzerentscheidung oder Infrastrukturfreigabe erforderlich.

Geprüft wurde Commit `59d979b76ea8cabb32a119db5341e4bce8955675` gegen den
Step-032-Plan und `Konzept.md`. Die Refresh-/Fetch-Implementierung erfüllt die
fachlichen Pfade im normalen lokalen Prozess; die dokumentierten MCP-/Safeguard-
Zähler sind am geprüften Commit jedoch nicht vollständig reproduzierbar. Das
verhindert gemäß DoD-Kriterium 8 die Freigabe.

## Geprüft

- [ ] Plan-Erfüllung: die drei Implementierungsschichten und die acht Verhalten-
  kriterien sind vorhanden; der dokumentierte MCP-/Safeguard-Nachweis ist wegen
  Finding 1 nicht vollständig belastbar.
- [x] Rules-Konformität: `get_violations(scopeFilter=ExternalSourceRepository)`
  meldet 0 Codeverletzungen; die separate Verzeichniswarnung wird als
  Bestands-/Strukturbefund getrennt ausgewiesen.
- [x] Logische Korrektheit: Fresh-/Stale-Entscheidung, isolierter Fetch,
  Generation-Publish, Expected-Current-Precondition, Fehlerpfade und Ownership
  wurden durch Code, Tests und semantische MCP-Abfragen nachvollzogen.
- [x] Konzept-Treue: keine Änderung an öffentlicher Cache-Konfiguration,
  Retention/GC/Invalidierung, Dirty-/Health-/degraded-Policy, Host-/MCP-Wiring,
  EPIC-05, AssemblyCache oder Cross-Process-Garantien.
- [x] Build: selbst nachgeprüft, grün.
- [x] Tests: selbst nachgeprüft, grün; beide echten Win32-1314-Reparse-Skips
  bleiben transparent.

## Plan- und Konzeptbewertung

### Fresh / Current / Stale

Erfüllt. `CreatedUtc` wird nur aus einem strikt gelesenen Current-Manifest
bewertet. Die Policy verwendet den benannten internen Default von 60 Minuten,
eine injizierbare Uhr, die inklusive Grenze `now >= CreatedUtc + 60 Minuten`,
und behandelt zukünftige sowie nicht-UTC-Zeitwerte fail-closed. Ein frischer
Current bleibt im vorhandenen Reuse-Pfad; Fetch und Publish werden dort nicht
aufgerufen. Fehlender oder ungültiger Current bleibt der bestehende Cache-Miss-
und Clone-/Write-through-Fallback.

Ein fälliger Current wird in einem neuen ownership-markierten Checkout
materialisiert. Die alte Generation und ihr Pointer werden nicht als Git-
Arbeitsverzeichnis verwendet; der erfolgreiche Pfad erzeugt eine neue
Generation und lässt die alte Generation für Retention erhalten.

### Fetch

Erfüllt. `FetchDefaultBranchAsync` nutzt den bestehenden
`ExternalSourceGitProcessExecutor`, die vorhandene Credential-/Environment-
Isolation, bounded Output, Timeout, Cancellation, Prozessbaum-/Handle-Cleanup,
Reparse- und 1314-Grenzen sowie die typisierte HTTP-/Git-Klassifikation. Die
lokal geprüfte Sequenz ist `fetch --no-tags origin`, `reset --hard origin/HEAD`
und `rev-parse --verify HEAD`. Credentials stehen weder in Argumenten noch in
Diagnosen und werden vor Reset/HEAD verworfen.

### Generation, Pointer und Race

Erfüllt für die zugesicherte Prozessgrenze. Der Writer validiert Manifest,
Inventory, Content, Revision, SolutionPath und Cache-Key vor sowie nach dem
Write-through. `ExpectedCurrentGeneration` wird beim Refresh gesetzt; ein
zwischenzeitlich geänderter oder verschwundener Current wird als
`CurrentChanged` behandelt und kann einen neueren, nach derselben Policy
frischen Current höchstens wiederverwenden. Es gibt keinen zweiten Remote-Fetch
und keine falsche Cross-Process-Lease-Garantie.

### Fehler, Ownership und Cleanup

Erfüllt in den geprüften Pfaden. Fetch-, Integritäts-, Publish- und
Cancellation-Fehler geben keinen stale Current als Erfolg zurück und starten
keinen Clone-Retry. Der request-eigene Checkout wird nach Fehlern bereinigt;
bei Erfolg bleibt er der vom Ergebnis besessene Handle. Staging war nach den
geprüften Läufen leer. Die reale Writer-/Pointer-Race-Regression und die
vorhandenen 1314-/Reparse-Grenzen blieben grün.

## Findings

1. `tasks/decompiled-assembly-analysis/step-032/step-result.md:82-103` —
   **[MAJOR] [Plan/Rules] MCP-/Safeguard-Nachweis ist am geprüften Commit nicht
   vollständig reproduzierbar.** Die beiden vollständigen Test-Gates und der
   exakte Fokus stimmen, aber die Audit-Evidenz weicht ab: Der reproduzierte
   `safeguard`-Lauf mit
   `projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter` liefert
   `5.833333333333333/10`, Threshold `8.00`, `FAIL` und drei Befunde, nicht den
   dokumentierten `5,87/10`. Der `find_duplicates`-Lauf mit `minTokens=20`,
   `similarityThreshold=exact` über den Produktionsscope
   `src/AiNetLinter/Mcp/Assemblies` scannt 369 Methoden, nicht 368; der
   Testscope liefert 140 Methoden. Ein enger
   `get_violations(scopeFilter=ExternalSourceRepository)`-Lauf liefert 0, der
   separate Verzeichnis-Scope `src/AiNetLinter/Mcp/Assemblies` enthält dagegen
   den Bestandsbefund `MaxDirectoryChildren` für 56 Einträge. Der breite
   `find_magic_values`-Lauf im `ExternalSourceRepository`-Scope liefert 151
   Kandidaten inklusive Tests; der im Step-Result behauptete changed-only-Wert
   9 ist nach dem Commit nicht erneut abrufbar, weil das saubere `HEAD` keinen
   Git-Diff mehr enthält. `get_impact` meldet ebenfalls fälschlich einen leeren
   Git-Diff trotz des vorhandenen Repositorys; dieser MCP-Befund wurde als
   Low-Severity-Observability-Feedback protokolliert und mit symbolischen
   Feature-/Dependency-Abfragen umgangen. **Auswirkung:** Das DoD-Kriterium
   „Scoped MCP-Safeguard sowie DRY-/MagicValues-/DeadCode-Audits ... wahrheits-
   gemäß dokumentiert“ ist nicht nachweisbar erfüllt; der Safeguard-Score darf
   nicht durch einen älteren oder unklaren Scope-Zähler ersetzt werden. Die
   drei Safeguard-Befunde sind getrennt zu bewerten: der Assemblies-Ordner hatte
   bereits vor dem Commit einen übergroßen Verzeichnisumfang (der Commit fügt
   zwei Dateien hinzu), der Task-Ordner mit 40 Einträgen und der
   `DaemonHostCommand`-Footprint von 2.975 > 2.500 sind bestehende, ausdrücklich
   out-of-scope Befunde. **Gebündelter Korrekturscope:**
   `step-result.md` mit den exakten MCP-Scopes, Optionen und Ergebnissen des
   finalen Commit-Stands aktualisieren; den narrower `ExternalSourceRepository`-
   Violationsscope vom breiteren Safeguard-Verzeichnisscope trennen, den realen
   Score `5,83/10 FAIL` samt drei Bestandsbefunden ausweisen und für den
   Magic-Value-Changed-only-Lauf entweder die exakten geänderten Dateipfade
   dokumentieren oder klar festhalten, dass der Commit-Modus nicht aus
   `HEAD` reproduzierbar ist. Dabei keine Produktionsdatei, keinen Task-State,
   keine Roadmap und kein globales Cleanup ändern.

## Test- und Laufnachweise

```text
dotnet build → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~ExternalSourceRepositoryCacheRefreshTests|FullyQualifiedName~GiteaGitRepositoryTransportTests|FullyQualifiedName~ExternalSourceRepositoryCacheReuseTests|FullyQualifiedName~ExternalSourceRepositoryAcquirerTests|FullyQualifiedName~ExternalSourceRepositoryCancellationTests" → grün (69 bestanden, 1 Skip, 70 gesamt, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~ExternalSourceRepositoryCacheRefreshTests|FullyQualifiedName~GiteaGitRepositoryTransportTests|FullyQualifiedName~ExternalSourceRepositoryCacheWriterTests|FullyQualifiedName~ExternalSourceRepositoryCacheAcquirerTests|FullyQualifiedName~ExternalSourceRepositoryCacheReuseTests|FullyQualifiedName~ExternalSourceRepositoryAcquirerTests|FullyQualifiedName~ExternalSourceRepositoryCancellationTests" → grün (125 bestanden, 2 Skips, 127 gesamt, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress → grün (2.071 bestanden, 2 Skips, 2.073 gesamt, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → grün (370 bestanden, 0 Skips, 370 gesamt, 0 Fehler)
Stress → nicht ausgeführt
```

Die zwei FastTest-Skips sind ausschließlich echte Reparse-/Symlink-Fälle:

- `ExternalSourceRepositoryAcquirerTests.AcquireAsync_ActualReparseEntry_IsRejectedAndExternalSentinelRemains`
- `ExternalSourceRepositoryCacheWriterTests.PublishAsync_ActualReparseEntryFailsClosed`

Beide melden transparent `ERROR_PRIVILEGE_NOT_HELD` / Win32 1314. Der Skip ist
kein Sicherheitsnachweis; privilegiert muss ohne Skip wiederholt werden.

Nach allen Läufen waren keine `testhost.exe`-, `vstest.console.exe`- oder Test-
`dotnet.exe`-Prozesse aktiv. Es gab keine Einträge oder Owner-Marker im
Repository-`temp`; die drei vorhandenen idle `dotnet MSBuild.dll`-Node-Reuse-
Prozesse blieben unangetastet. Der bestehende Default-Cache wurde nicht als
Test-Ownership übernommen.

## MCP-/DRY-/MagicValues-/DeadCode-Befunde

- **MCP:** `get_feature_context` mit absolutem projectRoot bestätigt für
  `ExternalSourceRepositoryCacheRefresh` 353 semantische Zeilen, 317 Codezeilen,
  0 Violations und 8 statische Tests; der Acquirer meldet 484 semantische
  Zeilen/445 Codezeilen und 0 Violations. Der Dependency-Graph zeigt die
  erwarteten Acquirer-/Reuse-/Writer-/Materializer-/Transport-Kanten. Der
  Commit-Impact blieb wegen des protokollierten MCP-Leer-Diff-Befunds nicht
  verfügbar.
- **Violations:** 0 im engeren
  `get_violations(scopeFilter=ExternalSourceRepository)`-Produktions-/Testscope.
  Der breitere Assemblies-Verzeichnisscope meldet genau einen bestehenden
  `MaxDirectoryChildren`-Befund; er wird nicht als neuer C#-Verstoß gewertet.
- **Safeguard:** 5,83/10, `FAIL` bei Threshold 8,00, drei Befunde. Der
  Assemblies-Verzeichnisbefund (56 Einträge), der Task-Verzeichnisbefund (40)
  und der bestehende `DaemonHostCommand`-Footprint (2.975 > 2.500) sind klar
  vom Refresh-/Fetch-Vertrag zu trennen; es wurde kein Cleanup vorgenommen.
- **DRY:** Token-basierter `find_duplicates`-Audit mit `minTokens=20` und
  `exact` liefert 0 Produktionscluster bei 369 Methoden sowie 0 Testcluster bei
  140 Methoden. Der Refactoring-Drift-Check für
  `ExternalSourceRepositoryAcquirer.FailAfterCleanup` liefert 0 Kandidaten.
  Der zusätzliche strukturelle Scan zeigt nur die legitime Ähnlichkeit zweier
  Result-Factories und eines testseitig erweiterten Acquirer-Factories; daraus
  entsteht kein neuer Tech-Debt-Befund.
- **MagicValues:** Die gezielten geänderten Produktionsdateien enthalten nur
  erwartete benannte Git-Argumente, Diagnose-/Contract-Werte und vorhandene
  Fehlermeldungen; keine Secrets wurden in Argumente, Pfade, Keys oder
  Diagnosen eingeführt. Der breite aktuelle ExternalSource-Scope ist mit 151
  Kandidaten nicht mit dem im Step-Result genannten changed-only-Wert 9
  gleichzusetzen.
- **DeadCode:** `find_dead_code(scopeFilter=ExternalSourceRepository,
  includeTests=true, confidence=high)` meldet 0 hochkonfidente Funde bei 67
  Symbolen in 29 Dokumenten.

## Geprüfte Dateien und Scope

Der Commit umfasst ausschließlich die Refresh-/Fetch-/Cache-Produktionsdateien
unter `src/AiNetLinter/Mcp/Assemblies`, die zugehörigen lokalen FastTests und
`tasks/decompiled-assembly-analysis/step-032/step-result.md`. Nicht verändert
wurden `task-state.md`, `roadmap.md`, `codemap.md`, `tech-debt.md`, öffentliche
Konfiguration, Host-/MCP-Komposition, Snapshot-/Registry-Code,
`AssemblyDecompilationCache` und EPIC-05. `git diff --check` ist sauber; das
Arbeitsverzeichnis blieb nach den Prüfungen sauber.

## Tech-Debt

Keine neuen in-scope Tech-Debt-Einträge. Die bekannten Directory-/Footprint-
Befunde sind bestehende bzw. ausdrücklich out-of-scope Strukturbefunde. Die
DRY-/MagicValues-/DeadCode-Prüfungen liefern keinen eigenständigen neuen
Architektur- oder Duplikationsbefund.

## Folgeaktion

Einen einzelnen Korrekturscope für die Review-Evidenz ausführen: die MCP-/Audit-
Scopes und finalen Zähler in `step-result.md` eindeutig und commit-reproduzierbar
festhalten, den realen Safeguard-Score 5,83/10 mit den drei getrennten
Bestandsbefunden dokumentieren und anschließend Fokus, Build sowie beide
vollständigen Nicht-Stress-Gates erneut ausführen. Bis dahin kein `approved`.
