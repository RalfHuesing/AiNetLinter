# Review-Report – Linse 3: Git-Transport und Repository-Akquisition

**Review-Status:** `approved` – keine bestätigten Befunde der Schweregrade S0–S3.

## Prüfrahmen

- **Linse:** Transport- und Akquisitionspfad eines externen Repository-Quellbezugs: Argumente, Umgebungsvariablen, Prompt-Unterdrückung, Exit-Codes, Diagnose-Redaktion, Timeout/Cancel, Prozessbaum und native Handles.
- **Geprüfter Scope:** `src/AiNetLinter/Mcp/Assemblies/ExternalSource/ProcessExecution/`, `.../Providers/` und `.../Repository/` sowie die zugehörigen vorhandenen Fast- und Integrationstests. Die Prüfung umfasste insbesondere `ExternalSourceGitProcessExecutor`, `ExternalSourceGitProcessLauncher`, den Provider-Transport (Symbol-/Dateiname redigiert) und `ExternalSourceRepositoryAcquirer`.
- **Revision:** `8a9fbddaeba6fff26c4c6f8d3ab2d3f87e7c2193`.
- **Working Tree:** Zu Beginn der Prüfung sauber; keine bestehenden Änderungen wurden bearbeitet. Während der parallelen Audit-Welle kamen weitere Audit-Commits hinzu; ein Vergleich der geprüften Source-/Test-Projekte mit der Prüf-Revision zeigte keine Scope-Differenz. Andere Reports und Task-Artefakte wurden nicht bearbeitet.
- **Nicht geprüft:** externe Remote-Systeme, echte authentifizierte Clone-/Fetch-Vorgänge, unbekannte Credential-Helper-Implementierungen, Betriebssysteme außerhalb der aktuellen Windows-Umgebung, nicht zum Transport gehörige Daemon-/MCP-Prozesspfade sowie Änderungen außerhalb der genannten Komponenten.

## Executive Summary

### Befunde

Es wurde kein reproduzierbarer Befund bestätigt. Insbesondere ergab sich kein S0-/S1-Risiko und kein eigenständiger S2-/S3-Befund für den geprüften Transportpfad.

### Bestätigte Erwartungen

- Der Prozessstart verwendet keine Shell, trennt Standardausgabe und Fehlerausgabe, übergibt Argumente strukturiert und entfernt geerbte `GIT_`-Variablen, während explizit angeforderte Variablen erhalten bleiben.
- Die Transportumgebung unterdrückt interaktive Prompts, deaktiviert globale/System-Konfiguration und übergibt Zugangsdaten nicht als Prozessargumente.
- Ausgabe ist begrenzt; Timeout und Abbruch beenden den lokalen Prozessbaum. Der ursprüngliche Cancel-Token bleibt erhalten.
- Nicht erfolgreiche Exit-Codes, Timeout und diagnostische Prozessausgaben werden in typisierte, redigierte Transportfehler überführt. Roh-URLs, Rohmeldungen und Zugangsdaten erscheinen nicht in den geprüften öffentlichen Diagnosen.
- Native Job-/Prozess- und Pipe-Ressourcen werden sowohl im Erfolgs- als auch im Fehlerpfad aufgeräumt; Cleanup-Fehler werden sichtbar gemacht und nicht still verschluckt.

### Abdeckungsgrenzen

Die Aussagen zu Exit-Code-Klassifikation, Diagnose-Redaktion und Credential-Isolation beruhen auf deterministischen Fake-Executor-Tests und Quellsemantik. Es gab keinen echten Remote-Server und keinen realen authentifizierten Transport. Die native Fehlerbehandlung für tatsächlich vom Betriebssystem zurückgewiesene Handle-Schließungen wurde über injizierte Fehlerpfade auf Sichtbarkeit geprüft, nicht über eine reproduzierbare Leckmessung. Ein Reparse-Capability-Test wurde übersprungen; das ist eine Umgebungsgrenze und kein bestätigter Transportfehler.

## Befundregister

Keine bestätigten Befunde. Daher sind für diesen Review keine Befund-IDs, Reproduktionen oder Remediation-Hypothesen erforderlich. Nicht reproduzierbare Vermutungen wurden ausschließlich als Coverage-Grenzen geführt.

## Belege und Gegenprüfungen

### MCP-first-Navigation

Verwendete MCP-Abfragen mit redigierten Parametern:

```text
get_file_tree
  targetType=project
  targetPath=<absoluter Repository-Root, redigiert>
  view=summary/files
  Ergebnis: scanCompleted=true, isTruncated=false;
           relevante ProcessExecution-/Providers-/Repository-Dateien gefunden

get_index_scope
  targetType=project
  targetPath=<absoluter Repository-Root, redigiert>
  Ergebnis: C#-Scope vollständig indiziert; 845 C#-Dateien im Symbolgraph

get_feature_context
  targetType=project
  targetPath=<absoluter Repository-Root, redigiert>
  symbolIdentifier=<Executor|Launcher|Provider-Transport|Repository-Acquirer>
  includeCallers=true, includeTests=true, includeMetrics=true, includeViolations=true
  Ergebnis: violations=0; Vollständigkeit für die angeforderten Features gegeben.
           Begrenzte Caller-Listen beim Provider-Transport und Acquirer waren als
           truncated=true wegen maxCallers=15 markiert und wurden nicht als globale
           Vollständigkeitsbehauptung verwendet.

get_symbol_body / get_class_structure
  targetType=project
  targetPath=<absoluter Repository-Root, redigiert>
  Ergebnis: Bodies und Member von Executor, Launcher, Transport, Failure-Policy,
           Checkout-Status und Acquirer gelesen.

get_violations
  targetType=project
  targetPath=<absoluter Repository-Root, redigiert>
  scope=<ExternalSource, redigiert>
  Ergebnis: 0 Regelverletzungen in 66 Dateien; vollständige Antwort.

safeguard
  targetType=project
  targetPath=<absoluter Repository-Root, redigiert>
  scope=<ExternalSource, redigiert>
  Ergebnis: PASS, Score 8.87/10. Der einzige Hinweis betraf einen außerhalb dieses
           Reviews liegenden Server-Health-Typen-Footprint und wurde nicht dem
           Transport zugerechnet.
```

Wesentliche semantische Belege:

- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/ProcessExecution/ExternalSourceGitProcessExecutor.cs:22` (`ExecuteAsync`) und `:39` (`ExecuteAsyncCore`): verknüpfter Timeout-/Caller-Token, Start ohne Shell, parallele Ausgabe-Leser, getrennte Behandlung von Caller-Cancel und Timeout.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/ProcessExecution/ExternalSourceGitProcessExecutor.cs:189` (`WaitForOutputAsync`) und `:208` (`ReadOutputAsync`): begrenzte Ausgabeaufnahme, Truncation-Markierung und kontrolliertes Warten auf beide Pipes.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/ProcessExecution/ExternalSourceGitProcessExecutor.cs:253` (`CleanupProcessAsync`): Prozessbaum-Beendigung, Stream-Schließung, bounded Cleanup-Wartezeit, Reader-Warten und Sichtbarkeit zusammengesetzter Fehler.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/ProcessExecution/ExternalSourceGitProcessExecutor.cs:382` (`CreateStartInfo`) und `:408` (`RemoveInheritedGitEnvironment`): `UseShellExecute=false`, Redirects, strukturierte `ArgumentList` und Bereinigung geerbter `GIT_`-Variablen.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/ProcessExecution/ExternalSourceGitProcessLauncher.cs:18` (`ExternalSourceGitProcessLauncher`): suspendierter Start, Job-Zuordnung vor Resume, geschützte Handle-Liste, native Prozess-/Thread-/Pipe-Ressourcen und Fehlerpfad-Cleanup.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/ProcessExecution/ExternalSourceGitProcessStartFailureCleanup.cs:13` (`ExternalSourceGitProcessStartFailureCleanup`): getrennte Cleanup-Beweise für zugeordnete und nicht zugeordnete Prozesse sowie begrenzter PID-Fallback.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/ProcessExecution/ExternalSourceGitProcessNativeMethods.cs:1` und `ExternalSourceGitProcessTreeScope`: Safe-Handle-/Job-Lebenszyklus, idempotentes Close-Verhalten und sichtbare Close-Fehler.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/Providers/<ProviderTransport-Datei>.cs:12` (MCP-Symbol redigiert): URL-Normalisierung, Clone-/Fetch-/Reset-/Revision-Argumente, Credential-Environment, Timeout-/Exit-Code-Klassifikation und sichere öffentliche Fehlerdiagnosen.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/Providers/<ProviderTransport-Datei>.cs:409` (`CreateEnvironment`): Prompt-Unterdrückung, Null-Konfiguration für globale/System-Konfiguration und Credentials ausschließlich als Child-Environment.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/Providers/ExternalSourceRepositoryFailurePolicy.cs:1` sowie `ProjectTransportDiagnostics`: aus untrusted Transportdaten werden begrenzte neutrale Fehlercodes/-texte gebildet; Rohdiagnostik wird nicht weitergereicht.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/Repository/ExternalSourceRepositoryAcquirer.cs:13` (`ExternalSourceRepositoryAcquirer`): Abbruchbehandlung, Ownership-Cleanup und Validierung des transportierten Checkouts vor Übergabe an nachgelagerte Verarbeitung.

### Vorhandene Testbelege

Integrationstests, `src/AiNetLinter.IntegrationTests/Mcp/Assemblies/ExternalSourceGitProcessExecutorTests.cs`:

- `ExecuteAsync_UsesRealProcessStartInfoAndIsolatesEnvironment`: prüft realen Start ohne Shell, Redirects, strukturierte Argumente, Arbeitsverzeichnis, geerbte/ explizite Umgebung und getrennte Marker in stdout/stderr.
- `ExecuteAsync_BoundsCapturedOutputAndMarksTruncation`: prüft Begrenzung und Truncation-Metadatum.
- `ExecuteAsync_TimeoutKillsLocalChildAndGrandchild`: prüft Timeout und lokalen Prozessbaum.
- `ExecuteAsync_CancellationKillsLocalChildAndGrandchildAndPreservesToken`: prüft Cancel, Prozessbaum und Token-Identität.
- `ExecuteAsync_PostCreateOwnershipFailureUsesBoundedFallback`: prüft Cleanup nach Ownership-Fehler sowie begrenzten Fallback.
- `ExecuteAsync_ResumedParentExitRequiresJobProofAndReportsCloseFailure`: prüft fehlenden Job-Beweis und sichtbaren Handle-Close-Fehler.
- `ExecuteAsync_TreeScopeCloseFailureIsVisibleAfterRealTreeCleanup`: prüft, dass Cleanup-Fehler nach realer Prozessbereinigung nicht verschwinden.
- `ExecuteAsync_RejectsUnrepresentableTimeoutBeforeProcessStart`: prüft Validierung vor Prozessstart.

Komponententests, `src/AiNetLinter.FastTests/Mcp/Assemblies/<ProviderTransportTests-Datei>.cs`:

- Clone-/Fetch-Argumenttests prüfen Single-Branch, keine Tags, Reset auf den attestierten Remote-Head und dass Credentials nicht in Argumenten stehen.
- Umgebungstests prüfen Prompt-Unterdrückung, isolierte Konfiguration, explizite Credential-Übergabe und Credential-Freiheit bei Status/Head/Reset.
- Fehlerklassifikationstests prüfen Timeout, Authentifizierung, Zugriff, Nichtauffindbarkeit, Netzwerk-/Antwortfehler sowie redigierte Diagnosen.
- Cancellationstests prüfen Token-Weitergabe und typed result mapping.

Ergänzende Komponententests, `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryAcquirerTests.cs`, prüfen Transportfehler, Ausnahme-/Cancel-Mapping, Ownership-Cleanup, Pfad-/Reparse-Grenzen, fehlende Lösung und Diagnose-Redaktion.

### Verifikationskommandos

Ausgeführt wurden:

```powershell
dotnet build
dotnet test src/AiNetLinter.IntegrationTests --filter "FullyQualifiedName~ExternalSourceGitProcessExecutorTests" --no-restore
dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~<ProviderTransportTests>|FullyQualifiedName~ExternalSourceRepositoryAcquirerTests" --no-restore
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress --no-build --no-restore --verbosity quiet
```

Ergebnisse:

- Build: erfolgreich, 0 Warnungen, 0 Fehler.
- Prozess-Executor-Fokus: 8 bestanden, 0 fehlgeschlagen, 0 übersprungen.
- Transport-/Acquirer-Fokus: 57 bestanden, 0 fehlgeschlagen, 1 Capability-Skip.
- FastTests ohne Stress: 2.274 bestanden, 0 fehlgeschlagen, 2 Capability-Skips.
- IntegrationTests ohne Stress: 377 bestanden, 0 fehlgeschlagen, 0 übersprungen.
- Die beiden FastTest-Skips betreffen ausschließlich `WindowsReparseCapabilityGate.Require()` und sind in der Coverage-Tabelle ausgewiesen.

## Schweregrad-, Umfangs- und Beweissicherheitsbewertung

Da kein Befund bestätigt wurde, gibt es keine Befundklassifikation. Die Review-Entscheidung basiert auf:

- **S0/S1:** kein reproduzierbarer Ausfall, keine nachgewiesene Datenintegritäts- oder Geheimnisverletzung.
- **S2/S3:** keine bestätigte relevante Semantik- oder Robustheitsabweichung; verbleibende Unsicherheiten sind als Abdeckungsgrenzen dokumentiert.
- **Umfang:** kein U1–U4-Befund.
- **Beweissicherheit:** hoch für die durch Quellsemantik und bestehende Tests direkt abgedeckten lokalen Pfade; mittel für OS-/native Fehlerpfade mit injizierten Fehlern; niedrig bzw. nicht bewertet für echte Remote- und Credential-Provider-Interoperabilität.
- **Umgebungsabhängigkeit:** Prozessbaum-, Job- und Handle-Aussagen gelten für den geprüften Windows-Pfad. Die Remote- und Credential-Interoperabilität wurde nicht in einer externen Umgebung ausgeführt.

## Code-Map-Abgleich

`tasks/decompiled-assembly-analysis-audit/code-map.md` ist für diese Linse unverändert korrekt. Die dort genannten ExternalSource-Bereiche, die MCP-first-Navigation, die Prozess-/Checkout-/Reparse-/Credential-Sicherheitsgrenzen und die Testbereiche stimmen mit den tatsächlich gefundenen Symbolen, Dateien und Tests überein. Es waren keine konkreten veralteten Navigationsfakten vorhanden; deshalb wurde `code-map.md` nicht geändert.

## Mögliche Cross-Lens-Überschneidungen

| Andere Linse | Berührungspunkt | Ergebnis dieser Linse |
|---|---|---|
| Linse 1 – Assembly-/Analysegrenze | Der Transport liefert den Quell-Checkout als vorgelagerte Eingabe. | Keine Analysegrenze bewertet; nur Prozess-/Übergabevoraussetzungen geprüft. |
| Linse 2 – Quelle/Provider/Authentifizierung | URL-Normalisierung, Credential-Umgebung und typisierte Fehlerdiagnose. | Keine zusätzliche Abweichung; Provider- und Auth-Interoperabilität mit echtem Remote bleibt offen. |
| Linse 4 – Checkout/Pfad/Reparse | Transport-Cleanup und Übergabe des reservierten Checkouts. | Cleanup- und Validierungsaufrufe geprüft; vollständige Pfad-/Reparse-Bewertung bleibt dort. |
| Linse 5 – Cache/Snapshot/Persistenz | Erfolgreiche Akquisition ist Vorstufe für Cache-/Snapshot-Erzeugung. | Nachgelagerte Persistenz nicht bewertet. |
| Linse 6 – MCP-/Wire-Vertrag | Öffentliche Fehlerdiagnosen und Cancel-Semantik werden weitergereicht. | Nur die Transportseite geprüft; Wire-Vertrag nicht erneut auditiert. |

## Coverage-/Limitations-Tabelle

| Bereich | Status | Beleg | Restgrenze / Umgebungsabhängigkeit |
|---|---|---|---|
| Argumente und Windows-Quoting | hoch abgedeckt | Executor-Integration plus Clone-/Fetch-Komponententests | Nicht jede denkbare Kombination aus leerem Argument, Backslash am Ende und Quote wurde als Einzelfall ausgeführt. |
| Environment und Prompt-Unterdrückung | hoch für den Vertrag | reale Executor-Prüfung plus Transport-Environment-Tests | Keine Ausführung mit einer echten Remote-Instanz oder fremdem Credential Helper. |
| Credential-Isolation und Diagnose-Redaktion | hoch für lokale Typen | Transport-/Acquirer-Tests mit geheimen Platzhaltern und Rohdiagnose-Prüfung | Echte externe Fehlermeldungen und unbekannte Helper-Ausgaben nicht abgedeckt. |
| Exit-Codes und Timeout | hoch für deterministische Pfade | Timeout-Test, typed-failure-Tests | Remote-spezifische Exit-Code-/Meldungsvarianten nur über Fakes. |
| Caller-Cancel und Prozessbaum | hoch im lokalen Windows-Pfad | reale Parent-/Grandchild-Integrationstests | Andere Betriebssysteme und externe Prozessbaum-Semantik nicht bewertet. |
| Native Handles, Job und Cleanup | mittel bis hoch | Start-/Resume-/Close-Fehler mit injizierten Native-Operationen plus reale Cleanup-Tests | Tatsächliche OS-Verweigerung eines Handle-Close und daraus resultierende Handle-Leak-Dauer nicht live gemessen. |
| Repository-Akquisition und Ownership | hoch für lokale Fehlerpfade | Acquirer-Komponententests | Kein echter Remote-Checkout; ein Reparse-Test wurde wegen fehlender Capability übersprungen. |
| Vollsuite / Regression | hoch | Build, 2.274 FastTests und 377 IntegrationTests ohne Stress grün | Die zwei Capability-Skips bleiben bestehen; keine Stress-Suite ausgeführt. |
| Code-Map-Navigation | hoch | MCP-Dateibaum, Index-Scope und Symbolauflösung | Caller-Auflistungen mit bewusstem MCP-Limit wurden nicht als vollständig behandelt. |

Eine Remediation-Hypothese ist mangels bestätigtem Befund nicht erforderlich.
