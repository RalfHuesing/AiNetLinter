---
status: done
type: step-review
task: decompiled-assembly-analysis
step: 020
epic: EPIC-04
step_type: single
reviewed_by: kritiker
reviewed_by_model: gpt-5 (Codex)
reviewed_by_model_knowledge_cutoff: nicht angegeben
reviewed_at: 2026-08-29T09:01:53+02:00
verdict: issues
tech_debt_ids: []
---

# Review Step 020: Git-Prozesslebenszyklus und statusbewusste
# Fehlerklassifikation an der Transportgrenze

## Verdict

- [ ] **approved** — alle vier Prüfebenen ok
- [x] **issues** — Korrektur-Step durch die Orchestrierung erforderlich
- [ ] **blocked** — Nutzer-Entscheidung nötig

Step 020 wird nicht freigegeben. Die drei Step-019-Vertragslücken sind weitgehend
behoben: bounded Output, der direkte lokale Real-Executor-Nachweis und die
statusbewusste Fehlerklassifikation sind vorhanden. Der Prozessbaum-Cleanup ist
jedoch bei einem bereits beendeten Parent mit weiterlaufendem Nachfahren nicht
ausnahmesicher; außerdem liegt ein möglicher Timeout-Setup-Fehler nach
`Process.Start()` außerhalb des Cleanup-`try`. Das ist ein CRITICAL-Finding
gegen die Vertragsgrenze, obwohl Build und alle Standard-Gates grün sind.

## Geprüft

- [ ] Plan-Erfüllung: Prozessbaum-Cleanup ist für zwei post-start Fehlerpfade
  nicht vollständig garantiert
- [ ] Rules-Konformität: statische Linter-Regeln sind grün, der sichere
  Prozess-/Fehlervertrag ist an der Cleanup-Grenze verletzt
- [ ] Logische Korrektheit: Parent-exit/Grandchild-pipe-Race kann einen
  Nachfahren weiterlaufen lassen
- [ ] Konzept-Treue: der kontrollierte Cancellation-/Cleanup-Vertrag aus dem
  Sicherheits- und Fehlerkonzept ist dadurch nicht vollständig erfüllt
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün; der bekannte Win32-1314-Skip ist
  transparent und kein Blocker dieses Reviews

## Befund

### Plan-Erfüllung

Die Abnahmekriterien 2 bis 8 sind erfüllt: stdout/stderr werden mit einem
zentralen Capture-Limit gelesen, Timeout und Caller-Cancellation bleiben
unterscheidbar, der direkte Real-Executor-Harness liegt wegen des
FastTests-Dependency-Gates regelkonform in `IntegrationTests`, die lokale
Child-/Grandchild-Fixture verwendet weder Git noch Remote, und die Matrix für
400/401/403/404/500 sowie lokalisierte und unbekannte Ausgaben ist vorhanden.

Die gemeinsame URL-Policy und der gemeinsame Success-Builder beheben TD-005;
die Acquirer-Ownership-/Cleanup-Kante, die gültige HEAD-Revision und die
repository-spezifische 1314-/Reparse-Projektion bleiben fachlich erhalten.
Abnahmekriterium 1 ist nicht vollständig erfüllt, weil der Tree-Kill bei einem
bereits beendeten Parent übersprungen wird. Der direkte Harness deckt nur einen
noch laufenden Parent ab und reproduziert diesen Race nicht.

### Rules-Konformität

Die scoped AiNetLinter-Abfragen melden 0 Violations für den geänderten
Produktions-, FastTests- und Integration-Executor-Scope. Nullable, sealed,
Catch-/Cancellation- und Runtime-Assembly-Ladegrenzen sind eingehalten; der
direkte `System.Diagnostics.Process`-Zugriff bleibt auf den zulässigen
Integration-Harness beschränkt. Die fachliche Sicherheitsregel für einen
vollständigen Prozessbaum-Cleanup ist wegen des Findings unten dennoch nicht
erfüllt.

### Logische Korrektheit

Der normale Timeout-/Cancellation-Pfad beendet einen laufenden Parent samt
Nachfahren bounded und beobachtet beide Reader. Die statusbewusste Policy parst
nur vollständige Git-HTTP-Zeilen, priorisiert Timeout und 401/403/404 vor
statuslosen Markern, ordnet 400/500 als `InvalidResponse` ein und ignoriert
beliebige Statusvorkommen in URL/Text. Secrets werden weder in Argumente noch
Diagnosen projiziert.

Der Cleanup-Vertrag ist dennoch nicht geschlossen: `TryKillProcessTree` setzt
`HasExited == true` mit Erfolg gleich, ohne `Kill(entireProcessTree: true)`
auszuführen. Ein Parent, der einen Grandchild-Prozess mit geerbten stdout- oder
stderr-Handles startet und danach endet, lässt dadurch den Grandchild trotz des
nachfolgenden Output-Timeouts weiterlaufen. Zusätzlich werden
`CancellationTokenSource(request.Timeout)` und die verknüpfte CTS nach
`Process.Start()` aber vor dem geschützten `try` erzeugt; ein vom
`ExternalSourceGitProcessRequest` erlaubter übergroßer Timeout kann daher den
gestarteten Prozess ohne Cleanup zurücklassen.

### Konzept-Treue (Ebene 4)

Der Step überschreitet den Konzept-Scope nicht: Es gibt keine Provider-,
Snapshot-, Refresh-, Cache- oder Host-Erweiterung, keine echte Remote-/Gitea-
Verbindung, keine Secrets und kein Runtime-Laden fremder Assemblies. Die
konzeptionelle Sicherheitsgrenze kontrollierter Cancellation und fehlender
weiterlaufender Fremdprozesse ist durch das Parent-exit-Race jedoch noch nicht
vollständig umgesetzt.

### Build-/Test-Status

- `dotnet build` → grün (0 Warnungen, 0 Fehler)
- `dotnet test src/AiNetLinter.IntegrationTests --filter "FullyQualifiedName~ExternalSourceGitProcessExecutorTests"` → grün (4 Tests, 0 Fehler, 0 übersprungen)
- `dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~GiteaGitRepositoryTransportTests|FullyQualifiedName~ExternalSourceRepositoryAcquirerTests"` → grün (54 Tests, 0 Fehler, 1 übersprungen)
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` → grün (1.994 Tests, 0 Fehler, 1 übersprungen)
- `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` → grün (364 Tests, 0 Fehler, 0 übersprungen)
- Stress-Tests wurden nicht ausgeführt. Der eine FastTests-Skip ist der
  bestehende echte Reparse-Test wegen `ERROR_PRIVILEGE_NOT_HELD (1314)`.

## Findings (nur bei `issues`)

1. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessExecutor.cs:400-407` —
   [CRITICAL] [Logik/Plan] `TryKillProcessTree` gibt bei einem bereits
   beendeten überwachten Parent sofort `true` zurück und ruft keinen
   `Kill(entireProcessTree: true)` auf. Reproduktion: Der lokale Harness muss
   den Parent nach dem Start eines Grandchilds beenden, während der Grandchild
   die geerbten stdout-/stderr-Pipes offen hält; danach läuft
   `WaitForOutputAsync` in den 5-Sekunden-Timeout und der Cleanup-Pfad meldet
   den bereits beendeten Parent als erfolgreich bereinigt, ohne den Grandchild
   zu beenden. **Auswirkung:** Ein Git-Nachfahre kann nach Timeout,
   Cancellation oder Reader-Fehler weiterlaufen und Pipes bzw. Ressourcen
   offenhalten; die Zusage eines vollständigen Prozessbaum- und Checkout-
   Cleanups ist damit nicht deterministisch. **Fix:** Den Cleanup-Vertrag so
   implementieren, dass `HasExited` des Parents nicht als Tree-Cleanup gilt;
   den Prozessbaum bereits beim Start zuverlässig erfassen/einkapseln und
   innerhalb der endlichen Cleanup-Grenze alle Nachfahren beenden. Eine
   direkte Regression muss den Parent-exit/Grandchild-open-pipe-Fall mit
   endlichen PID-/Pipe-Waits abdecken.

2. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessExecutor.cs:42-46` —
   [CRITICAL] [Logik/Plan] `CancellationTokenSource(request.Timeout)` und
   `CreateLinkedTokenSource` liegen nach `Process.Start()`, aber außerhalb des
   `try`, das den Cleanup garantiert. Der Request-Vertrag weist nur positive
   Timeouts zurück; ein übergroßer positiver Timeout, den die CTS nicht
   akzeptiert, wirft deshalb erst nach dem Prozessstart und überspringt
   `CleanupProcessAsync`. **Auswirkung:** Auch ein nicht durch Reader/Wait
   ausgelöster Setup-Fehler kann einen gestarteten Git-Prozess ohne
   Prozessbaum-Abbruch zurücklassen. **Fix:** Timeout-/Linked-CTS vor dem
   Prozessstart validieren/erzeugen oder deren Erzeugung in einen
   post-start Cleanup-geschützten Ablauf verschieben; für den Fall muss ein
   direkter Test sicherstellen, dass kein Prozess überlebt.

## MCP-/DRY-/MagicValues-/DeadCode-Ergebnis

- **MCP:** `find_symbol`, `get_feature_context`, `get_file_skeleton`,
  `get_symbol_body`, `find_references`, `get_impact`, `get_test_context`,
  `get_violations` und `safeguard` wurden mit absolutem
  `projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter` ausgeführt. Die
  betroffenen Produktionsdateien und der Integration-Harness melden 0
  Violations; der begrenzte Safeguard besteht mit 8,89/10. Der einzige
  Safeguard-Hinweis ist der bekannte außerhalb des Scopes liegende
  `DaemonHostCommand`-Footprint.
- **DRY/Refactoring-Drift:** Der Step-Scope-Clone-Audit über die betroffenen
  Produktionsdateien meldet 0 Cluster; der Integration-Testscope meldet 0
  Cluster. Der strukturelle Scope-Audit und beide gezielten
  Refactoring-Drift-Abfragen für URL-Policy und Success-Factory liefern keine
  Kandidaten innerhalb des Step-Scopes. Der vorgeschriebene solutionweite
  `find_duplicates(scopeDir="src", minTokens=20)`-Kontrollscan zeigt einen
  bestehenden Exact-Cluster außerhalb dieses Steps; er wird wegen der
  dokumentierten Scope-Grenze nicht verändert.
- **MagicValues:** Der dateischarfe Produktionsaudit findet 15 Kandidaten in
  sechs geänderten Produktionsdateien: benannte Git-Argumente, Buffer- und
  CTS-/Fehlermeldungswerte sowie bestehende Result-/Validierungsfehlertexte.
  Es gibt keinen neuen, unbenannten Secret- oder HTTP-Status-Magic-Value;
  die Klassifikation verwendet `HttpStatusCode`.
- **DeadCode:** Der dateischarfe Produktionsaudit findet 0 tote Symbole in
  den sechs geänderten Produktionsdateien. Die zwei bekannten Low-Confidence-
  Kandidaten außerhalb des Scopes wurden nicht erneut als Step-Fund eröffnet.

## Geänderte Dateien

- `tasks/decompiled-assembly-analysis/step-020/step-review.md`
- `tasks/decompiled-assembly-analysis/tech-debt.md` — TD-005 als erledigt
  dokumentiert

Produktionscode, `task-state.md`, `roadmap.md` und `codemap.md` wurden nicht
geändert.

## Folgeaktion

Einen flachen Korrektur-Step für die beiden CRITICAL-Cleanup-Findings anlegen.
Der Korrektur-Step muss den Parent-exit/Grandchild-open-pipe-Race direkt
testen, alle post-start Setup-Fehler in den bounded Cleanup einbeziehen und
danach Build, fokussierte Regressionen sowie beide vollständigen
Nicht-Stress-Gates erneut ausführen. Die statusbewusste HTTP-/Git-Matrix,
Credential-Sicherheit, gemeinsame URL-Policy, Success-Factory und die
1314-/Reparse-Fallback-Grenze bleiben als bereits bestätigte Invarianten
erhalten.
