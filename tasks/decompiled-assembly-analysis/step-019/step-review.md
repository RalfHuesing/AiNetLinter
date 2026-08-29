---
status: done
type: step-review
task: decompiled-assembly-analysis
step: 019
epic: EPIC-04
step_type: single
reviewed_by: kritiker
reviewed_by_model: gpt-5 (Codex)
reviewed_by_model_knowledge_cutoff: nicht angegeben
reviewed_at: 2026-08-29T07:44:34+02:00
verdict: issues
tech_debt_ids: [TD-005]
---

# Review Step 019: Produktiver Gitea-Git-Repository-Transport

## Verdict

- [ ] **approved** — alle vier Prüfebenen ok
- [x] **issues** — Korrektur-Step durch die Orchestrierung erforderlich
- [ ] **blocked** — Nutzer-Entscheidung nötig

Step 019 wird nicht freigegeben. Der Transport erfüllt die grundlegende
HTTP(S)-Clone-/HEAD- und Credential-Vertragsform, aber der reale
Child-Process-Lifecycle hat einen reproduzierbaren Hänge- und Cleanup-Pfad.
Zusätzlich belegen die vorhandenen Test-Doubles weder den realen
Prozessbaum-Abbruch noch die sichere `ProcessStartInfo`-Ausführung. Die
Fehlerklassifikation bleibt für HTTP-/Git-Ausgaben sprach- und
textmarkerabhängig. Ein Korrektur-Step ist erforderlich; er wurde gemäß dem
Auftrag, nur Review-Doku und gegebenenfalls Tech-Debt zu ändern, hier nicht
angelegt.

## Geprüft

- [ ] Plan-Erfüllung: drei in-Scope-Vertragslücken bleiben offen
- [ ] Rules-Konformität: statische Gates grün, aber Klassifikationsvertrag
  driftet von der geforderten stabilen Transportsemantik
- [ ] Logische Korrektheit: Prozessabbruch und Ausgabe-Drain sind nicht
  auf allen Ausnahmewegen bounded
- [x] Konzept-Treue: kein Provider-/Snapshot-/Cache-/Refresh-/Source-of-Truth-
  Wiring, keine echte Remote-Verbindung, keine Secrets und keine
  `Assembly.Load`-/Reflection-Nutzung hinzugefügt
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün; der bekannte Win32-1314-Skip ist
  transparent und kein Step-019-Remote-/Transportblocker

## Plan-Erfüllung

Erfüllt sind die produktive Implementierung hinter `IGiteaRepositoryTransport`,
der initiale `git clone` mit `--single-branch`, `--no-tags` und `--`, die
anschließende Prüfung von `HEAD` auf eine 40- oder 64-stellige
Hex-Revision, die getrennte Credential-Auflösung, die kontrollierten
Git-Umgebungsvariablen sowie die sichere Diagnose-Projektion. Die
Acquirer-Schicht übernimmt Staging, Besitzprüfung und Cleanup. Die
Step-018-Entscheidung zu repository-spezifischem 1314/Reparse-
`ProviderUnavailable` mit Decompilation-Fallback bleibt unverändert.

Nicht erfüllt ist der Nachweis, dass der echte Prozess-Executor unter
Timeout, Cancellation, Pipe-Ausnahme und Prozessbaum-Abbruch sicher arbeitet.
Die typed Fehlerarten sind vorhanden, aber ihre Git-/HTTP-Erkennung ist nicht
stabil genug für den im Plan beschriebenen Transportvertrag.

## Rules-Konformität

AiNetLinter-MCP meldet im Produktionsscope und im Step-019-Testscope keine
Violations. Es gibt keine globale Symlink-Sperre, keine Reflection und keine
Assembly-Ladeoperation. Die Regelabweichung ist fachlich: Die neue
Fehlerklassifikation nutzt rohe, englische stderr-Teilstrings als
Transportsemantik, obwohl der Plan sprach- und hostunabhängige Merkmale
fordert.

## Logische Korrektheit

### [CRITICAL] Prozessbaum kann bei Ausgabe-/Nicht-Cancellation-Fehlern weiterlaufen

- **Ort:** `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessExecutor.cs:109-118` und `:125-153`
- **Was:** Nach dem Start werden stdout und stderr mit unbounded
  `ReadToEndAsync()` gelesen. Der `try`-Block fängt ausschließlich
  `OperationCanceledException`. Eine Ausnahme eines Output-Tasks oder ein
  nicht schließendes geerbtes Pipe-Handle verlässt `ExecuteAsync`, ohne
  `AbortProcessAsync` aufzurufen; `using` entsorgt nur das `Process`-Objekt.
  Außerdem gilt der verknüpfte Timeout nur für `WaitForExitAsync`, nicht für
  die anschließenden `Task.WhenAll`-Drains in den Cancellation-/Timeout-
  Zweigen.
- **Auswirkung:** Ein Git-Prozess oder ein von ihm gestarteter Nachfahre kann
  nach dem Fehlschlag weiterlaufen. Bei offenem Pipe-Handle kann der Aufrufer
  in `Task.WhenAll` hängen; Acquirer-Cleanup und die Zusage „keine
  halbfertigen Checkouts“ werden dann nicht deterministisch erreicht.
- **Korrekturscope:** Den Lifecycle in einen ausnahmesicheren Cleanup-Pfad
  bringen, der nach Prozessstart jeden nicht erfolgreichen Ausgang bounded
  über den gesamten Prozessbaum beendet. Output-Drains müssen cancellation-
  und timeout-aware sein oder einen eigenen bounded Teardown besitzen. Einen
  lokalen Child-/Grandchild-Prozess als deterministischen Regressionstest
  verwenden.

### [MAJOR] Test-Doubles belegen den realen Executor und Prozessbaum-Abbruch nicht

- **Ort:** `src/AiNetLinter.FastTests/Mcp/Assemblies/GiteaGitRepositoryTransportTests.cs:14-16, 23-245`; realer Code in `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessExecutor.cs:91-237`
- **Was:** Alle Step-019-Transporttests injizieren `RecordingGitExecutor`.
  Der Kommentar `@covers ExternalSourceGitProcessExecutor` ist nur ein
  statisches Coverage-Mapping. Timeout wird als
  `ExternalSourceGitProcessResult.WasTimedOut = true` eingespeist und
  Cancellation wird vom Double geworfen. Kein Test startet den echten
  `ProcessStartInfo`-Pfad und kein Test beweist `ArgumentList`, Entfernung
  geerbter `GIT_*`-Variablen, bounded stdout/stderr-Drain oder
  `Kill(entireProcessTree: true)`.
- **Auswirkung:** Die zentrale Sicherheitsbehauptung des Steps bleibt trotz
  grüner Tests unbewiesen; insbesondere kann der CRITICAL-Fund unentdeckt
  bleiben. Ein Test-Double, das den Fehler bereits als Ergebnis liefert,
  testet nicht die Ursache im realen Executor.
- **Korrekturscope:** Deterministische lokale Prozessfixtures ergänzen, die
  ohne Git, Netzwerk oder Gitea einen langlebigen Child-/Grandchild-Prozess
  sowie kontrollierte stdout/stderr-Ausgabe erzeugen. Timeout und
  Cancellation müssen Prozessbaum-Ende und bounded Rückkehr assertieren;
  Argumente und bereinigte Git-Umgebung müssen am realen Startpfad geprüft
  werden.

### [MAJOR] Typed Git-/HTTP-Fehlerklassifikation ist textmarker- und
sprachabhängig

- **Ort:** `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryFailurePolicy.cs:66-113`
- **Was:** `ClassifyGitProcessFailure` entscheidet ausschließlich über rohe
  englische Teilstrings. `"unable to access"` wird pauschal als
  `NetworkUnavailable` klassifiziert, auch wenn Git danach einen HTTP-400-
  oder HTTP-500-Response meldet. Die Prüfung von `"404"`/`"not found"` läuft
  vor Authentifizierungsmerkmalen; eine URL oder Ausgabe mit 404-Text und
  anschließendem 401 kann dadurch als `RepositoryNotFound` enden.
  Lokalisierte Git-Ausgaben fallen ohne passende Marker auf
  `InvalidResponse` zurück.
- **Auswirkung:** Auth-, AccessDenied-, RepositoryNotFound-, Network- und
  Protocol-Fehler können den falschen stabilen Fehlercode erhalten. Damit
  werden Diagnose, Fallback-Entscheidung und spätere Retry-/Policy-Nutzung
  fachlich falsch, obwohl keine geheimen Rohmeldungen nach außen gelangen.
- **Korrekturscope:** Strukturierte Prozess-/HTTP-Ergebnismerkmale oder ein
  strikt statusbewusster Parser mit definierter Priorität einführen, generische
  URL-/Texttreffer ausschließen und 400/401/403/404/500 sowie unbekannte und
  lokalisierte Ausgaben deterministisch testen. Rohes stderr darf weiterhin
  nicht in Diagnosen gelangen.

## Konzept-Treue und Scope

Die positive Scope-Prüfung bleibt bestehen: Es gibt kein Provider-, Snapshot-,
Cache-, Refresh- oder Source-of-Truth-Wiring. Es wurde keine echte Remote-
Verbindung ausgeführt und kein Geheimnis verwendet. Die globale
Symlink-Capability-Sperre wurde nicht eingeführt; die 1314-/Reparse-Regel
bleibt repository-spezifisch und der Decompilation-Fallback erreichbar.

## Build- und Teststatus

- `dotnet build` — **grün**, 0 Warnungen, 0 Fehler.
- `dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~GiteaGitRepositoryTransportTests"` — **12 bestanden**, 0 übersprungen, 0 fehlgeschlagen.
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` — **1.981 bestanden**, 1 übersprungen, 0 fehlgeschlagen.
- `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` — **360 bestanden**, 0 übersprungen, 0 fehlgeschlagen.
- Stress-Tests wurden nicht ausgeführt. Der Skip von
  `ExternalSourceRepositoryAcquirerTests.AcquireAsync_ActualReparseEntry_IsRejectedAndExternalSentinelRemains` beruht transparent auf Win32-1314 und blockiert diesen Review nicht.

## MCP-, DRY-, MagicValues- und DeadCode-Ergebnis

- **MCP:** `get_feature_context`, `get_file_skeleton`, `get_symbol_body`,
  `find_symbol`, `find_references`, symbolischer `get_impact`,
  `get_violations` und `safeguard` wurden mit dem absoluten
  `projectRoot` ausgeführt. Die betroffenen Features haben 0 Violations;
  der Produktionsscope erreicht 8,89/10 und der Testscope 9,50/10. Die
  einzige Safeguard-Warnung betrifft den bestehenden, außerhalb des Scopes
  liegenden `DaemonHostCommand`-Footprint. Der historische `gitRef`-
  Impact-Aufruf lieferte wegen eines leeren MCP-Diffs keinen Dateisatz;
  Symbol-Impact und der lokale Commit-Diff wurden separat geprüft.
- **DRY/Refactoring-Drift:** Der vorgeschriebene solutionweite
  `find_duplicates`-Audit (`scopeDir=src`, `minTokens=20`) fand 195 Cluster,
  darunter den exakten Produktions-/Test-`Success`-Builder. Der begrenzte
  Produktionsaudit fand den URL-Validator als einzigen near-Klon; der
  strukturelle Audit und der gezielte Refactoring-Drift-Check bestätigten
  diesen Kandidaten. Diese beiden Beobachtungen sind als TD-005 erfasst;
  weitere Step-019-Duplikationsbefunde wurden nicht als echte Verstöße
  bewertet.
- **MagicValues:** Im vollständigen Assembly-Scope wurden 83 Kandidaten in
  29 Dateien gemeldet. Für die neuen Step-019-Dateien verbleiben vier
  Git-Protokollargumente, fünf Executor-Lokalisierungskandidaten und zwei
  Credential-Lokalisierungskandidaten als Audit-Hinweise. Die
  credentialbezogenen Umgebungs-/Helper-Literale sind bewusst sicherheits-
  dokumentiert unterdrückt; sie enthalten keine Secrets und wurden nicht als
  neue Tech-Debt-Verstöße gewertet.
- **DeadCode:** Der Produktionsaudit meldet ausschließlich die zwei bereits
  bekannten Low-Confidence-Funde `AssemblyOrigin.Kind` und
  `AssemblySourceSelectionOrchestrator.CreateFromSettings`; kein neuer
  Step-019-Dead-Code-Fund.

## Geänderte Dateien

- `tasks/decompiled-assembly-analysis/step-019/step-review.md`
- `tasks/decompiled-assembly-analysis/tech-debt.md` — neuer Eintrag TD-005

Produktionscode, `task-state.md`, `roadmap.md` und `codemap.md` wurden nicht
geändert.

## Folgeaktion

Einen Korrektur-Step für die drei priorisierten Findings anlegen. Danach die
realen lokalen Prozessfixtures und die statusbewusste Fehlerklassifikation
implementieren, dieselben Gates erneut ausführen und Step 019 erst nach
erneuter Kritiker-Prüfung freigeben.
