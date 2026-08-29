---
status: done (pending audit)
type: step-plan
task: decompiled-assembly-analysis
step: 020
corrects: step-019
title: "Git-Prozesslebenszyklus und statusbewusste Fehlerklassifikation an der Transportgrenze korrigieren"
epic: EPIC-04
estimated_risk: high
step_type: single
items: []
created_by: planer
created_by_model: gpt-5 (Codex)
created_by_model_knowledge_cutoff: nicht angegeben
created_at: 2026-08-29T08:02:00+02:00
related_to:
  - ../step-019/step-review.md
  - ../step-019/step-result.md
  - ../step-019/step-plan.md
  - ../step-018/step-review.md
  - ../step-018/step-result.md
  - ../follow-up-strategy.md
  - ../Konzept.md
---

# Step 020: Git-Prozesslebenszyklus und statusbewusste
# Fehlerklassifikation an der Transportgrenze korrigieren

## Bezug

- **Task:** `decompiled-assembly-analysis`
- **Epic:** `EPIC-04` — Gitea-Source-of-Truth, Refresh und Fehlersemantik.
- **Korrektur von:** `step-019`, Review `step-019/step-review.md`, Verdict
  `issues` mit einem CRITICAL- und zwei MAJOR-Findings.
- **Geltungsgrenze:** Der bestehende initiale Git-over-HTTP(S)-Clone bleibt
  der einzige fachliche Ablauf. Dieser Step korrigiert dessen
  Prozess-/Ergebnisvertrag und die zentrale Fehlerprojektion; er verdrahtet
  keinen erfolgreichen Checkout mit Snapshot, Provider oder Host.

## Scope und Out-of-scope

### In scope

- Einen bounded, ausnahmesicheren Lebenszyklus für den realen
  `ExternalSourceGitProcessExecutor` herstellen: Ausgabe-Drain,
  Cancellation, Timeout, Nicht-Cancellation-Ausnahmen, Prozessbaum-Abbruch
  und kontrolliertes Teardown.
- Den realen `ProcessStartInfo`-/`ArgumentList`-/Umgebungsweg mit einem
  lokalen, deterministischen Child-/Grandchild-Testdouble nachweisen, ohne
  Git, Gitea, Remote oder Netzwerk zu verbinden.
- `ExternalSourceRepositoryFailurePolicy` auf strukturierte, strikt
  statusbewusste Git-/HTTP-Evidenz mit definierter Priorität umstellen.
  HTTP 400/500 werden als ungültige Protokollantwort, 401/403/404 als
  Auth-/AccessDenied-/RepositoryNotFound-Zustände klassifiziert; reine
  Verbindungsfehler bleiben NetworkUnavailable.
- TD-005 innerhalb dieser Grenze auflösen: eine gemeinsame strikte
  HTTP(S)-Repository-URL-Policy für Acquirer und Transport sowie einen
  gemeinsamen `ExternalSourceRepositoryTransportResult.Success`-Builder für
  Produktion und Tests.
- Direkte Regressionen für Cleanup-/Cancellation-Beobachtbarkeit, Secret-
  Schutz, Fehlerpriorität und die unveränderte 1314-/Reparse-Projektion.

### Out of scope

- `IExternalSourceProvider`-Erfolgspfad, Acquirer→Snapshot-/Workspace-
  Wiring, Registry, Lease-Lifetime, Provider, Host-Komposition und MCP-
  Registrierung.
- Fetch, Refresh, persistenter Cache, Manifest-/Integritätsprüfung,
  Generation/Pointer und atomare Source-of-Truth-Veröffentlichung.
- Änderungen an `task-state.md`, `codemap.md`, `tech-debt.md` oder
  `roadmap.md`; die Fix-Modus-Regel lässt die Roadmap unverändert. Der
  spätere Kritiker kann TD-005 erst nach bestätigter Umsetzung schließen.
- Neue Produktions-HTTP-Clients oder eine echte Remote-Verbindung in Tests;
  kein Gitea-/Git-Testserver, kein Restore und kein Stress-Test.
- Änderung der repository-spezifischen 1314-/Reparse-Regel, globale
  Capability-Preflights, lokale Arbeitskopien als Source-of-Truth oder
  Credential-Speicherung.
- `Assembly.Load`, `AssemblyLoadContext`, Reflection oder sonstiges
  Runtime-Laden fremder Assembly-Inhalte.

## Aktueller Projektzustand (JIT-Kontext)

Die semantische MCP-Prüfung mit
`projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter` bestätigt:

- `ExternalSourceGitProcessExecutor` liegt in
  `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessExecutor.cs:91-237`.
  `ExecuteAsync` startet den Prozess mit Redirects, liest beide Pipes aktuell
  mit `ReadToEndAsync()` ohne eigenes Limit und fängt nach dem Start nur die
  beiden Cancellation-/Timeout-Zweige. `AbortProcessAsync` nutzt bereits
  `Kill(entireProcessTree: true)`, aber die anschließenden `Task.WhenAll`-
  Drains sind nicht bounded.
- `GiteaGitRepositoryTransport` verwendet den Executor zweimal (Clone und
  `rev-parse HEAD`) und projiziert dessen Resultat zentral über
  `ExternalSourceRepositoryFailurePolicy`. Die vorhandenen acht Transport-
  Regressionen injizieren ausschließlich `RecordingGitExecutor`; sie starten
  keinen realen `ProcessStartInfo`-Pfad.
- `ExternalSourceRepositoryFailurePolicy.ClassifyGitProcessFailure` in
  `:66-113` entscheidet aktuell über allgemeine stderr-Teilstrings. Dadurch
  können URL-/Textvorkommen von `404` oder `not found` vor Auth-Evidenz
  gewinnen und `unable to access` trotz HTTP 400/500 als Netzwerkfehler
  enden. Der vorhandene Enum-Vertrag bietet `AuthenticationRequired`,
  `AccessDenied`, `RepositoryNotFound`, `NetworkUnavailable`, `Timeout` und
  `InvalidResponse`, aber noch keine strukturierte HTTP-Evidenz.
- Der Acquirer besitzt weiterhin Staging, Ownership, Solution-/Reparse-
  Prüfung und Cleanup. Die Audit-Ergebnisse zeigen den exakten strukturellen
  URL-Klon zwischen Acquirer und Transport sowie den doppelten Success-Builder
  in Transport und Testcode; diese Befunde sind als TD-005 erfasst.
- Die aktuellen scoped MCP-Audits fanden keine neue Produktions-Violation und
  nur die bekannten Low-Confidence-Dead-Code-Kandidaten außerhalb dieses
  Korrekturscopes. Neue Magic Values oder Duplikate dürfen nur im unmittelbar
  berührten Prozess-/Transportpfad architektonisch sinnvoll mitbereinigt
  werden.

## Entscheidung zur Bündelung

Dieser Step bleibt ein gebündelter `step_type: single`-Korrektur-Step mit
`estimated_risk: high`, kein Micro-Batch. Alle drei Findings hängen am
selben Vertrag: Der Transport kann Fehler erst sicher klassifizieren, wenn
der Executor bounded und ausnahmesicher ein Prozessresultat liefert; der
Real-Executor-Test muss genau diesen Ablauf sowie die daraus sichtbare
Cancellation-/Cleanup-Garantie prüfen. Ein Split würde entweder den
unsicheren Prozesspfad oder die falsche Fehlerprojektion zwischen den
Review-Runden bestehen lassen.

Das Split-Gate bleibt eingehalten:

- **Fachverträge:** genau zwei eng gekoppelte Verträge — (1) der interne
  Prozess-/Output-/Termination-Vertrag und (2) der bestehende
  Git-Transport-/Fehlerprojektionsvertrag. URL-Policy und Success-Factory
  sind interne DRY-Helfer innerhalb dieser Grenze, keine neuen Fachports.
- **Schichten:** (1) realer Prozess-Executor, (2) Git-Transport-/Fehler-
  und URL-Projektion, (3) direkte netzwerkfreie FastTests.
- **Akzeptanzkriterien:** genau acht, siehe unten.
- **Kontextbudget:** zwölf `read_first`-Dateien, siehe unten.

## Intention

Nach diesem Step darf kein gestarteter Git-Prozess wegen eines Output-,
Wait-, Timeout- oder Cancellation-Pfads unbounded weiterlaufen oder einen
Aufrufer hängen lassen. Das Transportresultat soll HTTP-/Git-Fehler anhand
strukturierter bzw. streng statusbewusster Evidenz deterministisch und ohne
Secret- oder Rohdiagnoseleck klassifizieren; der reale Executor- und
Prozessbaum-Nachweis muss durch lokale Regressionen belegt sein.

## Akzeptanzkriterien

1. Nach erfolgreichem Prozessstart führt jeder nicht erfolgreiche Ausgang —
   einschließlich Output-/Wait-Ausnahme, Cancellation und Timeout — über
   einen gemeinsamen Cleanup-Pfad. Dieser beendet den gesamten Prozessbaum
   mit `Kill(entireProcessTree: true)` und wartet sowohl auf Prozessende als
   auch auf Pipe-Teardown nur mit einer endlichen Grenze; kein unobserved
   Drain-Task und kein hängendes `Task.WhenAll` bleibt zurück.
2. stdout und stderr werden cancellation-/timeout-aware und mit einem
   zentral benannten Capture-Limit gelesen. Die gespeicherte Ausgabe bleibt
   bounded und eine Trunkierung ist deterministisch erkennbar; ein
   Cleanupfehler verschluckt weder die primäre Nicht-Cancellation-Ausnahme
   noch die ursprüngliche Cancellation. Caller-Cancellation bleibt eine
   `OperationCanceledException` mit dem Caller-Token, Timeout bleibt als
   Timeout-/`WasTimedOut`-Semantik unterscheidbar.
3. Ein direkter Test des realen `ExternalSourceGitProcessExecutor` startet
   ein lokales Child-/Grandchild-Testdouble über den echten
   `ProcessStartInfo`-Pfad. Er weist `UseShellExecute=false`, Redirects,
   deaktiviertes stdin, sichere `ArgumentList`-Übergabe, Arbeitsverzeichnis
   und die bounded stdout-/stderr-Erfassung nach; er verwendet keine
   externe Remote-Verbindung.
4. Der reale Prozess-Test weist unter einer eng begrenzten, wiederhergestellten
   Testumgebungs-Sperre nach, dass geerbte `GIT_*`-Variablen nicht in den
   Child gelangen und explizit angeforderte Nicht-Secret-Variablen erhalten
   bleiben. Ein lokales Grandchild, das Pipes offen hält, ist nach Timeout
   und Cancellation beendet; Rückkehr und Cleanup werden jeweils mit
   endlichen Wait-Grenzen beobachtet.
5. Die Git-/HTTP-Klassifikation erzeugt zuerst typisierte Evidenz aus
   vollständigen, kontextgebundenen Statusmerkmalen und verwendet diese
   Priorität: Timeout; 401 ohne Credential = `AuthenticationRequired`, 401
   mit Credential = `AccessDenied`, 403 = `AccessDenied`, 404 =
   `RepositoryNotFound`, 400 oder 500 = `InvalidResponse`; nur ein
   statusloser, echter Verbindungsfehler wird `NetworkUnavailable`. Ein
   beliebiges `404`, `not found` oder `unable to access` in URL/Text darf
   keinen Status vortäuschen.
6. Direkte Transport-/Policy-Regressionen decken 400, 401 ohne und mit
   Credential, 403, 404, 500, bekannte statuslose Netzwerkfehler,
   `Timeout`, Protokollfehler sowie lokalisierte und unbekannte Ausgaben ab.
   Auth-, AccessDenied-, Network-, Timeout-, Protocol- und Fallback-
   Erwartungen sind explizit; 401 gewinnt bei widersprüchlicher Evidenz vor
   einem bloßen 404-Text. Rohes stderr und Credential-Secrets erscheinen
   weiterhin weder in Diagnosen noch Logs.
7. TD-005 ist ohne öffentliche Mapping-Erweiterung behoben: Acquirer und
   Transport verwenden dieselbe strikte HTTP(S)-URL-Policy einschließlich
   Userinfo-, Query- und Fragment-Ausschluss, und Produktions-/Testcode
   verwenden denselben Success-Builder. Der Acquirer behält seine bisherige
   Ownership-/Cleanup-Verantwortung; die 1314-/Reparse-Projektion bleibt
   repository-spezifisch unverändert.
8. Die Korrektur bleibt auf die drei Schichten dieser Transportgrenze
   beschränkt, führt keine Provider-/Snapshot-/Refresh-/Cache-/Host-Logik,
   keine Secrets in Args/Logs/Diagnosen und kein Runtime-Assembly-Laden ein.
   `dotnet build` sowie die beiden vollständigen Nicht-Stress-Gates sind nach
   der Implementierung grün; Stress-Tests bleiben ausgeschlossen.

## Konkrete Änderungen

### Schicht 1: Prozesslebenszyklus und bounded Output

#### `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessExecutor.cs:59-237`

- **Was:** Den vorhandenen Prozessresultat-/Executorpfad so strukturieren,
  dass nach `Process.Start()` alle normalen, Timeout-, Cancellation- und
  sonstigen Ausnahmeausgänge in einen einzigen bounded Cleanup-Ablauf
  münden. `Kill(entireProcessTree: true)` bleibt die einzige
  Prozessbaum-Abbruchoperation; eine bereits beendete Prozessinstanz wird
  race-sicher behandelt.
- **Was:** stdout und stderr über einen kleinen, cancellation-aware
  Reader mit zentral benanntem Capture-Limit und endlichem Teardown lesen.
  Der Reader muss beide Tasks beobachten, Trunkierung deterministisch
  markieren und nach Abbruch niemals unbounded auf geerbte Pipe-Handles
  warten. `ProcessTerminationTimeout` und ein ggf. nötiges Output-Limit
  bleiben als benannte Prozessvertragswerte zentralisiert.
- **Was:** Nicht-Cancellation-Ausnahmen nach dem Prozessstart dürfen den
  Cleanup nicht überspringen; die primäre Ausnahme bleibt sichtbar und wird
  durch einen Cleanupfehler höchstens als Inner-/Zusatzfehler ergänzt.
  Caller-Cancellation wird erst nach sicherem Cleanup erneut mit dem
  Originaltoken ausgelöst; Timeout liefert weiterhin die bestehende
  `WasTimedOut`-/Transportsemantik.
- **Warum:** Das behebt das CRITICAL-Finding, verhindert hängende Drains und
  bewahrt die bestehende Injektion über `IExternalSourceGitProcessExecutor`.
  Der fachliche `IGiteaRepositoryTransport`-Port muss dafür nicht erweitert
  werden.

### Schicht 2: Statusbewusste Projektion und TD-005

#### `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryFailurePolicy.cs:66-113`

- **Was:** `ClassifyGitProcessFailure` auf einen internen, strukturierten
  Evidenzschritt umstellen. HTTP-Status wird nur aus einem vollständigen
  Git-/HTTP-Kontext erkannt, nicht aus beliebigen Zahlen, URL-Segmenten oder
  allgemeinen Teilstrings. Statuswerte werden typisiert behandelt; bekannte
  statuslose Git-Auth-/Verbindungsmerkmale dürfen nur als eng begrenzte
  Parser-Evidenz dienen, nicht als frei suchbare Substrings.
- **Was:** Die in Kriterium 5 festgelegte Priorität und den sicheren
  Fallback `InvalidResponse` für lokalisierte/unbekannte/protokollarisch
  nicht belegte Ausgaben zentral implementieren. Die existierenden
  Diagnosecodes und die Secret-freie Projektion bleiben unverändert.
- **Warum:** Auth-, AccessDenied-, RepositoryNotFound-, Network- und
  Protocol-Zustände dürfen nicht durch Sprache, URL-Inhalt oder Markerreihen-
  folge verfälscht werden.

#### `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryUrlPolicy.cs` (neu)

- **Was:** Einen internen, kleinen Normalisierungs-/Validierungshelper für
  getrimmte absolute HTTP(S)-Repository-URLs mit Host, ohne Userinfo,
  Query oder Fragment anlegen. Der Helper liefert bei Erfolg den
  normalisierten URL-Wert zurück, damit Transport und Acquirer keine
  unterschiedliche Prüfung oder Trim-Logik pflegen.
- **Warum:** Das löst den URL-Teil von TD-005 ohne neue öffentliche
  Konfiguration und bewahrt die strengere Transportsemantik auch im
  Acquirer.

#### `src/AiNetLinter/Mcp/Assemblies/GiteaGitRepositoryTransport.cs:52-342`

- **Was:** Die gemeinsame URL-Policy verwenden und die lokale
  `IsSupportedRepositoryUrl`-Kopie entfernen. Clone-/HEAD-/Credential-
  Semantik, `ArgumentList`, geschützte Child-Umgebung und die bisherige
  1314-/Reparse-Grenze bleiben ansonsten unverändert.
- **Was:** Den privaten Success-Builder entfernen und den gemeinsamen
  `ExternalSourceRepositoryTransportResult.Success`-Builder verwenden.
- **Warum:** Der Transport bleibt an derselben Grenze, während URL- und
  Result-Duplikate nicht weiter auseinanderdriften.

#### `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs:43,458`

- **Was:** Ausschließlich die Mapping-URL-Prüfung auf die gemeinsame Policy
  umstellen. Staging, Ownership, Solution-/Reparse-Prüfung, Cleanup und
  die `ProviderUnavailable`-Projektion für 1314 bleiben unangetastet.
- **Warum:** Der Acquirer erhält dieselbe validierte URL-Regel, ohne seine
  Rolle als alleiniger Checkout-Besitzer zu erweitern.

#### `src/AiNetLinter/Mcp/Assemblies/IGiteaRepositoryTransport.cs:20-73`

- **Was:** Einen internen statischen `Success`-Builder auf dem bestehenden
  `ExternalSourceRepositoryTransportResult` ergänzen, der die leeren
  Diagnosen und `FailureKind.None` zentral setzt. Die bestehende
  Konstruktorvalidierung bleibt die einzige Result-Invariante.
- **Warum:** Produktions- und Testcode verwenden dieselbe Result-Semantik;
  der fachliche Port und sein Wire-/Mapping-Vertrag ändern sich nicht.

### Schicht 3: Direkte Regressionen

#### `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceGitProcessExecutorTests.cs` (neu)

- **Was:** Einen lokalen Testhelper über `TestTempDirectory` verwenden, der
  nur mit `pwsh`/Windows-Prozessmitteln arbeitet, kontrollierte Marker nach
  stdout/stderr schreibt, ein Child und Grandchild erzeugt und die
  jeweiligen Prozess-IDs für die Prüfung meldet. Kein Git-Aufruf, kein
  HTTP-/Gitea-Endpunkt und kein Netzwerkzugriff.
- **Was:** Den realen Executor direkt testen: Argumente mit Leerzeichen und
  Shell-Metazeichen, Arbeitsverzeichnis, Redirects, stdin-Deaktivierung,
  geerbte `GIT_*`-Bereinigung und explizite Testumgebung. Die globale
  Testumgebung darf nur unter einem engen Lock geändert und in `finally`
  vollständig zurückgesetzt werden; keine zwangsserialisierte Test-
  Collection.
- **Was:** Erfolgreichen bounded Output, Output-Trunkierung, Timeout und
  Caller-Cancellation prüfen. Ein Grandchild hält Pipes bis zum Abbruch
  offen; die Tests assertieren bounded Rückkehr, Original-Cancellationtoken,
  `WasTimedOut` und dass Parent/Child/Grandchild innerhalb einer endlichen
  Wartegrenze beendet sind. Alle Testpfade räumen ihre lokalen Prozesse und
  Dateien ebenfalls endlich und resilient auf.
- **Warum:** Damit wird der MAJOR-Nachweis für den realen Executor erbracht
  und das CRITICAL-Szenario mit offenem Prozessbaum/Pipe reproduzierbar
  abgesichert.

#### `src/AiNetLinter.FastTests/Mcp/Assemblies/GiteaGitRepositoryTransportTests.cs`

- **Was:** Die bestehenden Recording-Tests auf die neue strukturierte
  Klassifikation ausrichten. Die Matrix aus Kriterium 6 enthält explizite
  HTTP-Statuskontexte, widersprüchliche URL-/Textvorkommen, lokalisierte/
  unbekannte Ausgaben, Credential-Varianten, Timeout und Secret-Redaktion.
- **Was:** Die Clone-/HEAD-Erwartungen auf den gemeinsamen Success-Builder
  und die unveränderte Credential-/Prompt-Semantik ausrichten. Der
  bestehende `@covers`-Nachweis darf nicht mehr den fehlenden Real-Executor-
  Test vortäuschen; die neue direkte Testklasse deckt diesen Typ ab.

#### `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryAcquirerTests.cs`

- **Was:** Den vorhandenen Test-Success-Builder auf die gemeinsame Factory
  umstellen und direkte Query-/Fragment-/Userinfo-URL-Regressionen für die
  gemeinsame Policy ergänzen. Bestehende Ownership-, Cleanup-, Cancellation-
  und 1314-/Reparse-Assertions bleiben erhalten.
- **Warum:** TD-005 wird über beide Verbraucher verifiziert, ohne einen
  Acquirer→Snapshot-Anschluss oder einen globalen Capability-Preflight zu
  öffnen.

### Proaktiver Debt-Check im Scope

- `find_duplicates`, `find_magic_values` und `find_dead_code` sind nach der
  Änderung nur über den berührten Prozess-/Transportbereich zu prüfen.
  Neue oder durch die Korrektur direkt verursachte Duplikate, Magic Values
  und tote Hilfsmittel sind im selben Paket mechanisch bzw. architektonisch
  sinnvoll zu bereinigen; vorhandene Kandidaten außerhalb dieses Pakets
  bleiben dokumentierte Nutzerentscheidung.
- TD-001 bis TD-003 werden nicht geöffnet. TD-005 ist die einzige bestehende
  Tech-Debt-Position dieses Korrekturscopes; `tech-debt.md` selbst wird von
  diesem Planer nicht geändert.

## Tests

- [ ] Direkter Real-Executor-Test für `ProcessStartInfo`, sichere
  `ArgumentList`, Redirects, stdin, Arbeitsverzeichnis, geerbte
  `GIT_*`-Bereinigung und explizite Nicht-Secret-Umgebung.
- [ ] Lokaler Child-/Grandchild-Test für bounded stdout/stderr, Timeout,
  Cancellation, `Kill(entireProcessTree: true)`, finite Rückkehr und
  beobachtbares Prozess-/Pipe-Cleanup.
- [ ] Transport-/Policy-Matrix für HTTP 400/401/403/404/500, Credential-
  abhängige 401-Semantik, statuslose Netzwerkfehler, Timeout,
  Protokollfehler sowie lokalisierte/unbekannte Ausgaben; keine generischen
  URL-/Texttreffer.
- [ ] Gemeinsame URL-Policy für HTTP(S), Userinfo-, Query- und Fragment-
  Ausschluss sowie gemeinsamer Success-Builder in Produktions- und
  Testverbrauchern.
- [ ] Bestehende Acquirer-Regressionen für Ownership, Cleanup, Cancellation
  und repository-spezifischen 1314-/Reparse-Fallback bleiben grün.

Der Planer führt keine Tests aus. Der Coder führt nach der Implementierung
die projektweiten Gates aus:

```powershell
dotnet test src/AiNetLinter.FastTests --filter Category=Unit
dotnet test src/AiNetLinter.FastTests --filter Category=Component
dotnet build
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
```

Die neuen Real-Executor-Tests bleiben lokal und deterministisch; sie starten
weder Git noch Gitea und greifen nicht auf einen Remote zu. Stress-Tests,
Systemprivilegienänderungen und externe Restore-Aktivität sind ausgeschlossen.

## Definition of Done

- [ ] Alle drei Findings aus `step-019/step-review.md` sind in diesem einen
  Korrekturpaket behoben und durch direkte Regressionen prüfbar.
- [ ] Jeder nach Prozessstart mögliche Fehlerpfad beendet den Prozessbaum
  bounded, beobachtet Output-Tasks und lässt keinen hängenden Prozess oder
  unbounded Drain zurück.
- [ ] Caller-Cancellation, Timeout und Nicht-Cancellation-Ausnahme behalten
  ihre getrennte Semantik; Cleanupfehler werden nicht still verschluckt.
- [ ] Fehlerklassifikation ist statusbewusst, priorisiert 401/403/404 vor
  bloßen Texttreffern und ordnet 400/500 als `InvalidResponse` ein.
- [ ] TD-005 ist durch gemeinsame URL-Policy und Result-Factory erledigt;
  die öffentliche Mapping-JSON- und Provider-/Snapshot-Grenze bleibt gleich.
- [ ] 1314-/Reparse-Fallback, Ownership und Acquirer-Cleanup bleiben
  unverändert und direkt regressionstauglich.
- [ ] `dotnet build` sowie beide vollständigen Nicht-Stress-Testläufe sind
  grün; Stress-Tests wurden nicht ausgeführt.
- [ ] Der Coder schreibt `step-020/step-result.md`, dokumentiert tatsächliche
  Abweichungen und setzt den Planstatus auf `done (pending audit)`.

## Kontextbudget

### `read_first` (maximal 12 Dateien)

1. `tasks/decompiled-assembly-analysis/step-019/step-review.md` — konkrete
   CRITICAL-/MAJOR-Findings, ihre Zeilen und die Korrekturrichtung.
2. `tasks/decompiled-assembly-analysis/step-019/step-result.md` — tatsächlich
   gelieferter Executor-/Transportstand und bisherige Testgrenzen.
3. `tasks/decompiled-assembly-analysis/step-019/step-plan.md` — ursprüngliche
   Prozess-, Secret-, 1314- und Scope-Invarianten.
4. `tasks/decompiled-assembly-analysis/step-018/step-review.md` — genehmigte
   Reparse-/1314-Grenze und unveränderter Fallback-Nachweis.
5. `tasks/decompiled-assembly-analysis/step-018/step-result.md` — konkrete
   implementierte Failure-Projektion vor Step 019.
6. `tasks/decompiled-assembly-analysis/follow-up-strategy.md` — Split-Gate,
   Kontextbudget und Handoff-Regel.
7. `tasks/decompiled-assembly-analysis/Konzept.md` — Phase 4, Sicherheits-,
   Auth- und netzwerkfreie Testleitplanken.
8. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessExecutor.cs` —
   Request-/Result-Vertrag und aktueller realer Prozesslebenszyklus.
9. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryFailurePolicy.cs` —
   zentrale Fehler-/Diagnoseprojektion und 1314-Helfer.
10. `src/AiNetLinter/Mcp/Assemblies/GiteaGitRepositoryTransport.cs` — Clone-,
    HEAD-, Credential- und Result-Verbraucher.
11. `src/AiNetLinter.FastTests/Mcp/Assemblies/GiteaGitRepositoryTransportTests.cs`
    — bestehende Recording-Tests und Secret-/Cancellation-Muster.
12. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs` —
    Ownership-, Solution-, Cleanup- und ProviderUnavailable-Grenze.

### `read_on_demand`

- `src/AiNetLinter/Mcp/Assemblies/IGiteaRepositoryTransport.cs` und
  `ExternalSourceRepositoryAcquisitionModels.cs` nur für die gemeinsame
  Result-Factory und die vorhandenen Result-Invarianten.
- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryAcquirerTests.cs`
  und `ExternalSourceRepositoryCancellationTests.cs` für konkrete
  Cleanup-/URL-Regressionen; `TestTempDirectory` und `TestWaiter` für das
  etablierte lokale Fixture-/Wait-Muster.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryPathGuard.cs` sowie
  Step-018-Plan/Result nur, falls die unveränderte Reparse-/Ownership-Kante
  für eine Assertion nachgeschlagen werden muss.
- `src/AiNetLinter/Configuration/ExternalSourceConfiguration.cs` und
  `ExternalSourceMappingValidator.cs` nur zur Bestätigung, dass kein
  öffentliches Mapping- oder Credential-Feld geändert wird.
- Bestehende lokale Prozess-Harnesses in
  `src/AiNetLinter.IntegrationTests/` nur als Muster; keine Übernahme ihrer
  MCP-/Daemon-Scope-Logik in diesen Transport-Step.

### `out_of_scope`

- `IExternalSourceProvider`, `AssemblySourceSelectionOrchestrator`,
  `AssemblyAnalysisHostComposition`, `SourceSnapshotRegistry`,
  `ExternalSourceSnapshot`, MCP-Registrierung und alle Projekt-/Assembly-
  Sessionpfade.
- Refresh, Fetch, Cache, Source-of-Truth, Dirty-/Unbuilt-Checkout,
  Generation, Manifest, Health- und Capability-Matrix außerhalb des
  bestehenden 1314-/Reparse-Vertrags.
- Externe HTTP-/Git-/Gitea-Verbindungen, Remote-Repositories, externe
  Restore-/Testprojekte, Stress-Tests und Systemprivilegienänderungen.
- `task-state.md`, `codemap.md`, `tech-debt.md`, `roadmap.md` und die
  konzeptionelle Mapping-/Credential-Konfiguration.
- Breite DRY-/MagicValues-/DeadCode-Sweeps sowie TD-001 bis TD-003.

## Risiken und Gegenmaßnahmen

- **Pipe- oder Prozessbaum-Race:** Prozessende, Tree-Kill und Reader-
  Teardown getrennt, aber mit derselben endlichen Cleanup-Grenze beobachten;
  keine unbounded `WhenAll`-Wartephase nach Timeout/Cancellation zulassen.
- **Cleanup-Ausnahme verdeckt Primärfehler:** Primärfehler und
  Cancellation-Token zuerst sichern; Cleanupfehler nur kontrolliert anhängen
  oder in eine definierte Folgefehlersemantik überführen.
- **Output-Limit verdeckt relevante Statusinformation:** Head-/Tail- oder
  gleichwertige bounded Erfassung mit expliziter Trunkierungsinformation
  verwenden; unbekannte bzw. nicht vollständig belegte Evidenz sicher als
  `InvalidResponse` behandeln, nie als geratenen Status.
- **Falsche HTTP-Klassifikation:** nur kontextgebundene Statuszeilen parsen,
  Status vor statuslosen Markern priorisieren und URL-/allgemeine
  `not found`-Treffer ausschließen; die vollständige Matrix direkt testen.
- **Testumgebung hinterlässt Prozesse oder GIT-Variablen:** lokale Marker-
  Handshakes, finite Waits, `try/finally`-Cleanup und ein enger Lock um die
  einzige temporäre Prozessumgebungsänderung verwenden. Keine globale
  Testserialisierung und keine Secrets als Testmarker verwenden.
- **Scope-/Code-Drift:** nur den bestehenden Executor-/Transport-/Acquirer-
  Anschluss berühren; neue Hilfen müssen verwendet werden, damit kein
  DeadCode entsteht. Provider, Snapshot, Cache und Host bleiben geschlossen.

## Coder-Handoff

### Sicherer Einstieg

1. Zuerst diesen Handoff und die zwölf `read_first`-Dateien lesen. Danach
   `get_feature_context`, `get_file_skeleton` und `get_symbol_body` mit dem
   absoluten Projektroot für `ExternalSourceGitProcessExecutor`,
   `ExternalSourceRepositoryFailurePolicy`,
   `GiteaGitRepositoryTransport` und den Acquirer ausführen; für Aufrufer
   und Testzuordnung `find_references`/`get_test_context` verwenden. `rg`
   bleibt auf konkrete Text-/Prozessmuster begrenzt.
2. Den Executor-Lifecycle zuerst korrigieren und dabei Result-/Output-
   Metadaten sowie die bestehende `IExternalSourceGitProcessExecutor`-
   Injektion klein halten. Prüfe jeden Pfad nach `Process.Start`, besonders
   Reader-Ausnahme, Wait-Ausnahme, Timeout und Caller-Cancellation.
3. Danach die statusbewusste Failure-Evidenz und die gemeinsame URL-Policy
   einführen. Ändere weder `ExternalSourceProviderFailureKind` noch die
   1314-/Reparse-Projektion; erweitere den Result-/Mapping-Wire nicht.
4. Dann den realen lokalen Prozessbaumtest und die Statusmatrix ergänzen.
   Verwende `TestTempDirectory`, finite `TestWaiter`-Grenzen und einen
   engen Environment-Lock; kein `Process.Start` auf Git und keine Remote-
   Verbindung. Entferne den irreführenden Executor-Coverage-Anspruch aus
   den Recording-Tests nur, wenn der neue direkte Test die Abdeckung trägt.
5. Nach der Implementierung die scoped DRY-/MagicValues-/DeadCode-Prüfung
   wiederholen. Bereinige nur neue oder direkt verursachte Befunde in diesem
   Paket; aktualisiere `tech-debt.md` nicht selbst.

### Übergabeinvarianten

- Nach Prozessstart endet jeder nicht erfolgreiche Pfad bounded im gesamten
  Prozessbaum; stdout/stderr-Tasks bleiben beobachtet und können keinen
  Aufrufer unbounded blockieren.
- Caller-Cancellation bleibt echte Cancellation mit dem Originaltoken;
  Timeout, Prozessfehler und Cleanupfehler bleiben unterscheidbar.
- Keine Secrets in URL, `ArgumentList`, Logs, Diagnosen, Result-Texten oder
  Test-Markern; geerbte `GIT_*`-Umgebung wird vor dem expliziten Setzen
  bereinigt.
- Die Klassifikationsreihenfolge ist statusbewusst: 400/500 sind
  `InvalidResponse`, 401/403/404 werden nur aus belastbarer Evidenz
  projiziert, statuslose Verbindungsfehler sind `NetworkUnavailable`,
  unbekannte/lokalisierte Ausgaben sind sicherer `InvalidResponse`.
- `IGiteaRepositoryTransport`, Mapping-JSON, Acquirer-Ownership, Cleanup und
  der repository-spezifische 1314-/Reparse-Fallback bleiben unverändert in
  ihrer fachlichen Rolle.
- Kein Provider-/Snapshot-/Cache-/Refresh-/Host-Wiring, keine Reflection und
  kein Runtime-Laden fremder Assemblys.

### Nächster sicherer Einstiegspunkt

Beginne in `ExternalSourceGitProcessExecutor.ExecuteAsync` und seinen
privaten Cleanup-/Reader-Helfern. Nach dem Real-Executor-Lifecycle ist
`ExternalSourceRepositoryFailurePolicy.ClassifyGitProcessFailure` der nächste
isolierte Einstieg; erst danach werden URL-/Success-DRY und die direkten
Regressionen angepasst. Der Acquirer bleibt bis zum gezielten URL-Aufruf
unverändert.

## Rules-Refs

- `.agents/rules/AiNetLinter-McpWorkflow.mdc` — C#-Feature-, Symbol-,
  Referenz- und Impact-Fragen zuerst über den AiNetLinter-MCP mit absolutem
  `projectRoot`; `rg` nur ergänzend für konkrete Textarbeit.
- `.agents/rules/AiNetLinter.mdc` — Nullable, kurze Methoden, begrenzte
  Komplexität, keine stillen Catch-Blöcke, keine Runtime-Assembly-Ladung und
  deterministische Testabdeckung.
- `.agents/rules/AiNetLinterRichtlinien.mdc` — statische Architektur,
  Result-/Fehlersemantik, sichere Prozesse, Cancellation, Secret-Schutz,
  TestTempDirectory und proaktiver DRY-/MagicValues-/DeadCode-Abbau.
- `.agents/Agent-Scaffolding/AGENTS.md` — deutsche Dokumentprosa,
  Pfad-/Commitkonventionen und unveränderte Git-Historie.
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/spec.md` — Fix-Modus,
  Korrektur-Scope, Split-Gates, Kontextbudget, Handoff und Review-Gating.
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/orchestrator.md` — flache
  `corrects`-Kette, serieller Coder-/Kritiker-Ablauf und Commitverantwortung.
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/skills/planer/SKILL.md` —
  JIT-/Fix-Modus, Pointer-Referenzen und keine Roadmap-Änderung im Fix-Modus.

## Bekannte Ausnahmen

- Der bestehende echte Reparse-Test darf ausschließlich bei
  `ERROR_PRIVILEGE_NOT_HELD (1314)` überspringen. Dieser Step ändert weder
  den Test noch das Capability-Gate; unter berechtigter Umgebung muss der
  Test weiterhin ohne Skip laufen.
- Die Planer-Ausführung führt keine Tests aus. Der Coder muss die im Plan
  genannten Gates nach dem Code-Commit ausführen; fehlende Infrastruktur oder
  ein nicht erreichbares Tool ist gemäß Workflow als `blocked` zu behandeln.
- `TD-001` bis `TD-003` bleiben offen und werden nicht künstlich erweitert.
  Nur neue, unmittelbar im berührten Prozess-/Transportpfad entstandene
  DRY-, MagicValues- oder DeadCode-Befunde sind Teil dieses Pakets.

## Notes

Step 020 schließt ausschließlich die Korrekturlücke von Step 019. Nach
Genehmigung bleibt als nächster EPIC-04-Schnitt der erfolgreiche
Acquirer→Snapshot-/Workspace-Anschluss offen; danach folgen Refresh, Cache,
Integrität und atomare Veröffentlichung. Der Prozess-/Fehlervertrag dieses
Steps darf diese späteren Lebenszyklusentscheidungen nicht vorwegnehmen.
