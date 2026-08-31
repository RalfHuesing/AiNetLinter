# Auditbericht – Linse 4: Checkout-Sicherheit und Lebenszyklus

**Verdikt: issues** — ein bestätigter S1-Befund im Cancellation-Übergang nach erfolgreicher Checkout-Akquisition.

## Audit-Metadaten

- **Linse:** Repository-Checkout und Attestation, Reparse-Points, Pfadschutz, Besitz, atomare Veröffentlichung und Cleanup-Verträge.
- **Geprüfter Scope:** die C#-Kette unter `src/AiNetLinter/Mcp/Assemblies/ExternalSource/` für Reservierung, Besitzprüfung, Checkout-Akquisition, Attestation, Cache-Publikation, Snapshot-Lebensdauer, Provider-Orchestrierung und Prozess-/Prozessbaum-Cleanup; zugeordnete Fast- und Integrationstests.
- **Geprüfte Revision:** `8a9fbddaeba6fff26c4c6f8d3ab2d3f87e7c2193`.
- **Working Tree:** Die Source-, Test-, Konfigurations- und Dokumentationsdateien waren unverändert. Während der parallelen Audit-Welle war zusätzlich eine fremde Änderung an einer nicht zu dieser Linse gehörenden `code-map.md`-Routingzeile sichtbar; sie wurde weder gelesen noch geändert. Dieser Report ist das einzige von mir angelegte Artefakt.
- **MCP-Initialisierung:** Projektbaum vollständig gescannt; der relevante ExternalSource-Baum umfasste 50 Dateien. Der Symbolscope meldete 845 C#-Dateien vollständig abgedeckt. Der gezielte Violation-Check meldete 0 Verstöße in 66 Dateien.
- **Nicht geprüft:** tatsächliches Verhalten einer dekompilierten Assembly/DLL, Verhalten eines entfernten Transportservers, vollständige Solution-/CLI-End-to-End-Läufe sowie ein adversarialer lokaler Administrator mit absichtlichen TOCTOU-Manipulationen. Die Prozessausführung wurde nur hinsichtlich Startfehler-, Timeout-, Cancellation- und Cleanup-Vertrag geprüft; kein allgemeines Prozess-Review außerhalb dieser Linse.

## Executive Summary

### Befund

- **CHK-001:** Nach einer erfolgreich zurückgegebenen Checkout-Akquisition kann die Provider-Orchestrierung bei einer direkt danach beobachteten Cancellation den bereits erzeugten Checkout-Handle nicht mehr erreichen. Der Cancellation-Handler entsorgt deshalb weder Handle noch Ownership. Der reservierte Checkout bleibt bestehen. **S1 / U2 / Beweissicherheit hoch.**

### Bestätigte Erwartungen

- Die Besitzprüfung verlangt einen Nachfahrenpfad innerhalb der Staging-Wurzel, gültige Ownership-Daten sowie reparse-freie Pfade. Unbekannte oder nicht lesbare Dateisystemzustände werden für Sicherheitsentscheidungen geschlossen abgelehnt.
- Reparse-Punkte werden bei der Checkout-Validierung und beim Löschen separat behandelt: Ein echter Reparse-Punkt führt zu `ProviderUnavailable`; ein Cleanup löscht höchstens den Link selbst und greift nicht in das referenzierte externe Ziel ein.
- Die Checkout-Reservierung erfolgt unter der Staging-Wurzel mit atomarer Verzeichnisanlage und einem exklusiv erzeugten Ownership-Marker. Der Marker wird vor der Weitergabe synchronisiert.
- Transport-Attestation und Cache-Attestation binden Revision, Vertrauensstatus und erwartete Dateiinhalte. Der Solution-Pfad wird zusätzlich auf Checkout- und Staging-Nachfahrenschaft sowie Reparse-Freiheit geprüft.
- Die Cache-Publikation schreibt einen temporären Pointer und veröffentlicht ihn über atomare Ersetzung bzw. Verschiebung; temporäre Artefakte werden im Fehlerpfad bereinigt.
- Der Checkout-Handle hält Materialisierungsnutzung über einen Lease fest und wiederholt Cleanup nach Freigabe der letzten Nutzung. Cancellation während der Materialisierung wird in dem vorhandenen Provider-Test korrekt weitergereicht und bereinigt.
- Der Prozess-Executor beendet bei Timeout/Cancellation den lokalen Prozessbaum mit begrenztem Cleanup-Wartefenster; Cleanup-Fehler werden sichtbar gemacht. Der gezielte Integrationstestlauf hierfür war grün.

### Abdeckungsgrenzen

- Für `ExternalSourceRepositoryPathGuard` und `ExternalSourceRepositoryCheckoutReservation` fand der MCP-Testkontext keine direkten Tests. Die indirekten Acquirer-Tests decken Normal- und viele Fehlerpfade ab, aber nicht jeden Guard-/Reservation-Zweig isoliert.
- Die beiden echten Reparse-Laufzeittests wurden übersprungen, weil der Capability-Gate für Directory-Symlinks die lokale Berechtigung nicht nachweisen konnte. Der Gate-Code dokumentiert ausdrücklich, dass dieser Skip kein Sicherheitsnachweis ist.
- Die fünf Provider-Tests decken Erfolg, Akquisitionsfehler, Materialisierungsfehler, Cancellation während der Materialisierung und einen Snapshot ohne Owner ab. Die Cancellation-Grenze zwischen `AcquireAsync`-Rückkehr und lokaler Handle-Zuweisung ist nicht enthalten.
- Die direkte Unit-Zuordnung für Startfehler-Cleanup und Startup-Ressourcen ist leer; die Executor-Integrationstests decken diese Verträge nur indirekt über native Test-Seams bzw. reale lokale Prozessbäume ab.
- Kein Befund wurde aus nicht reproduzierbaren Annahmen über einen entfernten Transport oder einen privilegierten Angreifer abgeleitet.

## Befund CHK-001

### Titel und Komponente

**Cancellation zwischen Akquisitionsrückgabe und Ownership-Bindung lässt Checkout unbereinigt**

Komponente: Provider-Orchestrierung, symbolisch redigiert als `ExternalSource.Providers.<provider-orchestrator>.ResolveAsync`; gekoppelte Komponenten sind `ExternalSourceRepositoryAcquirer` und `ExternalSourceCheckoutHandle`.

- **Schweregrad:** S1 — eine wesentliche End-to-End-Cleanup-Zusage bricht unter realistischer Cancellation.
- **Umfang:** U2 — mehrere eng gekoppelte Komponenten und der gesamte Akquisitions-/Ownership-Lebenszyklus.
- **Beweissicherheit:** hoch — MCP-Symbolbody, konkrete Quellzeilen, Ownership-Transfer im Acquirer und ein bestehender Gegenbeleg für die spätere Cancellation-Phase bestätigen den Kontrollfluss. Die Lücke lässt sich über das injizierbare Acquirer-Interface mit einem Testdouble deterministisch auslösen; dafür wurde kein neuer Test angelegt.
- **Umgebungsabhängigkeit:** normale lokale Dateisystemumgebung; keine Netzwerk- oder Sonderberechtigung erforderlich. Im produktiven Ablauf ist das Auftreten ein Scheduling-Fenster, mit einem rückgabefähigen Test-Acquirer aber deterministisch.

### Erwartetes Verhalten

Sobald eine erfolgreiche Akquisition einen lebenden Checkout-Handle zurückgibt, muss jede anschließende Cancellation entweder nach gebundener lokaler Ownership-Referenz bereinigen oder den Rückgabewert selbst zuverlässig entsorgen. Nach dem Cancellation-Pfad dürfen Ownership-Marker und Checkout-Verzeichnis nicht als zurückgelassene eigene Ressourcen bestehen bleiben.

### Beobachtetes Verhalten

In `ResolveAsync` wird der Handle zunächst als `null` lokalisiert. Nach dem `await acquirer.AcquireAsync(...)` erfolgt an Zeile 40 erneut `cancellationToken.ThrowIfCancellationRequested()`. Erst danach, ab Zeile 47, wird der Handle über `TryGetCheckout` in `checkout` gebunden. Wird die Cancellation zwischen erfolgreicher Akquisition und dieser Prüfung beobachtet, springt der Code direkt in den Handler an Zeile 72. `DisposeFailedResources(snapshot, checkout)` erhält dann `checkout == null`; der erzeugte Handle und seine Ownership werden nicht entsorgt.

Der Acquirer erzeugt den Handle vor der Rückgabe an `ExternalSourceRepositoryAcquirer.cs:181-194`. Die tatsächliche Bereinigung hängt an `ExternalSourceCheckoutHandle.Dispose()` (`ExternalSourceRepositoryAcquisitionModels.cs:136-159`) und ruft dort die Ownership-Bereinigung auf. Diese Kette wird im beschriebenen Fenster nicht erreicht.

### Auswirkung

Der reservierte Checkout samt Ownership-Marker bleibt im Staging-Bereich. Wiederholte abgebrochene Auflösungen können dadurch lokale Ressourcen akkumulieren und den Cleanup-Vertrag gegenüber dem aufrufenden Analyseweg verletzen. Der Befund ist kein Nachweis für Zugriff auf ein fremdes Ziel: Die Pfad- und Ownership-Guards bleiben intakt; betroffen ist die fehlende Freigabe einer bereits eigenen, gültig reservierten Ressource.

### Konkrete Reproduktion

1. Erzeuge über den vorhandenen Provider-Konstruktor einen Test-Acquirer, der einen erfolgreichen `ExternalSourceRepositoryAcquisitionResult` mit einem lebenden `ExternalSourceCheckoutHandle` zurückgibt.
2. Lasse der Test-Acquirer nach Erzeugung des erfolgreichen Ergebnisses, aber vor Rückgabe des abgeschlossenen `ValueTask`, den für `ResolveAsync` verwendeten `CancellationTokenSource` canceln. Der Acquirer-Test-Seam muss die Cancellation für diese gezielte Grenzprüfung nicht selbst als Fehler behandeln.
3. Rufe `ResolveAsync` mit diesem Token auf. Die Prüfung an `<provider-orchestrator>.cs:40` (Dateiname im Report redigiert) wirft vor `TryGetCheckout` an Zeile 47 `OperationCanceledException`.
4. Prüfe im Test, dass der Handle nicht disposed ist, sein Cleanup-State nicht auf erfolgreich steht und der von ihm besessene Checkout-Pfad bzw. Ownership-Marker noch existiert. Erwartet wäre ein bereinigter Pfad.

Der bestehende Test `ResolveAsync_CancellationFromMaterializer_RethrowsAndCleansCheckout` (`src/AiNetLinter.FastTests/Mcp/Assemblies/<provider-tests>.cs:136`, Dateiname redigiert) bestätigt nur die spätere Phase: Dort ist `checkout` bereits gesetzt und der Handler kann ihn bereinigen. Er reproduziert diese frühere Grenze nicht.

### Belege

#### Semantische MCP-Belege

- `get_symbol_body`, `targetType: project`, `targetPath: <project-root>`, `symbolIdentifiers: ["ExternalSource.Providers.<provider-orchestrator>.ResolveAsync(...)"]`: bestätigt die Reihenfolge `AcquireAsync` → Cancellation-Prüfung → `TryGetCheckout` sowie den Cancellation-Handler mit `DisposeFailedResources(snapshot, checkout)`.
- `find_references`, `targetType: project`, `targetPath: <project-root>`, Symbol `ExternalSource.Providers.<provider-orchestrator>.ResolveAsync(...)`: 13 Aufrufstellen, `completeness.truncatedByMaxResults=false`, `truncatedByNodeLimit=false`; darunter der Produktionsaufruf in `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblySourceSelectionOrchestrator.cs:235` und die Provider-/Attestation-Tests.
- `get_symbol_body`, `targetType: project`, `targetPath: <project-root>`, Symbole `ExternalSourceRepositoryAcquirer.AcquireReservedCheckoutAsync` und `...CompleteTransportResultAsync`: bestätigt Cancellation-Cleanup innerhalb des Acquirer-Fensters sowie Handle-Erzeugung und Weitergabe nach erfolgreicher Checkout-Validierung.
- `get_symbol_body`, `targetType: project`, `targetPath: <project-root>`, Symbol `ExternalSourceCheckoutHandle.Dispose`: bestätigt, dass erst `Dispose` die Ownership- und Checkout-Bereinigung ausführt.
- `get_test_context`, `targetType: project`, `targetPath: <project-root>`, Symbol `ExternalSource.Providers.<provider-orchestrator>`: fünf zugeordnete Component-Tests; keiner benennt die Grenzphase nach erfolgreicher Akquisition und vor `TryGetCheckout`.

#### Datei-/Zeilenbelege

- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/Providers/<provider-orchestrator>.cs:37-47`: erfolgreiche Akquisition, zweite Cancellation-Prüfung, erst danach Ownership-Bindung.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/Providers/<provider-orchestrator>.cs:71-75`: Cancellation-Handler entsorgt nur `snapshot` und die lokale `checkout`-Variable.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/Repository/ExternalSourceRepositoryAcquirer.cs:181-194`: Erzeugung und Rückgabe des lebenden Checkout-Handles.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/Repository/ExternalSourceRepositoryAcquisitionModels.cs:136-159`: Cleanup-Ausführung ausschließlich über `ExternalSourceCheckoutHandle.Dispose()`.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/Repository/ExternalSourceRepositoryPathGuard.cs:141-159`: Besitzvalidierung und geschütztes Löschen; diese Absicherung kann den hier nicht aufgerufenen Cleanup nicht ersetzen.

#### Test- und PowerShell-Belege

- `dotnet test src/AiNetLinter.FastTests --no-restore --no-build --filter "FullyQualifiedName~ExternalSourceRepositoryAcquirerTests|FullyQualifiedName~ExternalSourceRepositoryCacheWriterTests|FullyQualifiedName~ExternalSourceRepositoryCheckoutAttestationTests|FullyQualifiedName~<provider-tests>" --logger "console;verbosity=minimal"` → 98 erfolgreich, 2 übersprungen, 100 gesamt. Der Provider-Filter ist im Report redigiert.
- `dotnet test src/AiNetLinter.IntegrationTests --no-restore --no-build --filter "FullyQualifiedName~ExternalSourceSnapshotMaterializerTests" --logger "console;verbosity=minimal"` → 6 erfolgreich, 0 übersprungen.
- `dotnet test src/AiNetLinter.IntegrationTests --no-restore --no-build --filter "FullyQualifiedName~ExternalSourceGitProcessExecutorTests" --logger "console;verbosity=minimal"` → 8 erfolgreich, 0 übersprungen.
- `rg -n "var acquisition|ThrowIfCancellationRequested|TryGetCheckout|DisposeFailedResources" src/AiNetLinter/Mcp/Assemblies/ExternalSource/Providers/<provider-orchestrator>.cs` → Treffer an den oben genannten Zeilen.
- `rg -n "ExternalSourceCheckoutHandle|TryCleanup|TryDeleteOwnedCheckout" src/AiNetLinter/Mcp/Assemblies/ExternalSource/Repository/ExternalSourceRepositoryAcquirer.cs src/AiNetLinter/Mcp/Assemblies/ExternalSource/Repository/ExternalSourceRepositoryAcquisitionModels.cs src/AiNetLinter/Mcp/Assemblies/ExternalSource/Repository/ExternalSourceRepositoryPathGuard.cs` → bestätigt Handle-Erzeugung, Disposal-Kette und geschütztes Löschen.

### Nicht umgesetzte Remediation-Hypothese

Die Ownership-Referenz sollte vor einer post-akquisitionsbezogenen Cancellation-Prüfung lokal gebunden werden, oder der Acquisition-Result-Handle muss bei dieser Prüfung explizit als noch nicht übertragener Rückgabewert entsorgt werden. Dabei müssen die bestehenden Unavailable-/Failure-Projektionen und der Transfer bei erfolgreicher Materialisierung unverändert bleiben. Es wurde keine Änderung vorgenommen.

## Code-Map-Abgleich

Die für diese Linse relevanten Navigationsanker in `tasks/decompiled-assembly-analysis-audit/code-map.md` sind unverändert korrekt. Die dort aufgeführten Anker für `Analysis`, `ExternalSource`, `ProcessExecution`, `Providers`, `Repository`, `Snapshots`, FastTests, IntegrationTests und TestKit existieren weiterhin; eine PowerShell-`Test-Path`-Prüfung ergab für alle neun Anker `True`. Die tatsächlichen MCP-Funde liegen genau in diesen Bereichen. Während der parallelen Welle wurde eine nicht zu dieser Linse gehörende Assembly-Routingzeile in der Map von einem anderen Reviewer geändert; ich habe diese Datei nicht editiert und keine relevante Korrektur vorgenommen.

## Mögliche Cross-Lens-Überschneidungen

| Überschneidung | Relevanz |
|---|---|
| Provider-/Materialisierungs-Linse | Die spätere Cancellation-Phase ist korrekt bereinigt; die vorgelagerte Ownership-Grenze sollte bei der Prüfung des Providervertrags mitgeführt werden. |
| Transport-/Cache-Linse | Der Acquirer erzeugt den Handle nach Transportvalidierung und vor Cache-/Snapshot-Nutzung; ein Review dieser Übergabe darf den Befund nicht nur auf Transportfehler beschränken. |
| Assembly-Selection-Linse | Der Produktionsaufruf in `AssemblySourceSelectionOrchestrator.cs:235` konsumiert den Providervertrag; die Ressource wird dort nicht zusätzlich sichtbar entsorgt. |

## Coverage-/Limitations-Tabelle

| Bereich | Ergebnis | Beleg / Grenze |
|---|---|---|
| Pfadschutz und Besitz | Erwartung bestätigt; kein zusätzlicher Befund | MCP-Bodies zu `IsOwnedCheckout`, Reparse-Inspektion und Cleanup; direkte Testzuordnung für `ExternalSourceRepositoryPathGuard` fehlt. |
| Checkout-Reservierung | Erwartung bestätigt; kein zusätzlicher Befund | Atomare Reservation, Ownership-Marker und Fresh-Reservation-Cleanup per MCP geprüft; keine direkten Reservation-Tests. |
| Reparse-Points | Fail-closed-Code bestätigt; Laufzeitabdeckung begrenzt | `AcquireAsync_ActualReparseEntry_IsRejectedAndExternalSentinelRemains` und `PublishAsync_ActualReparseEntryFailsClosed` wurden übersprungen, weil der Capability-Gate echte Symlinks nicht nachweisen konnte. |
| Attestation und atomare Cache-Veröffentlichung | Erwartung bestätigt | Attestation-/Cache-Writer-MCP-Bodies, 98 gezielte FastTests; echte Reparse-Writer-Prüfung war übersprungen. |
| Snapshot- und Lease-Lebensdauer | Erwartung bestätigt | 6/6 `ExternalSourceSnapshotMaterializerTests`; Handle- und Materialisierungs-Use-Tests in FastTests. |
| Prozessbaum und Startfehler-Cleanup | Erwartung bestätigt, direkte Unit-Abdeckung begrenzt | 8/8 `ExternalSourceGitProcessExecutorTests`; MCP-Body für Startfehler-Cleanup; direkte Testzuordnung für die Cleanup-Hilfstypen fehlt. |
| Cancellation nach erfolgreicher Akquisition, vor Ownership-Bindung | **CHK-001 bestätigt** | MCP-Kontrollfluss und bestehende spätere-Cancellation-Gegenprobe; kein bestehender Test für genau diese Grenze. |
| Dekomplizierte Assembly/DLL und entfernter Transport | Nicht geprüft | Außerhalb der konkreten Checkout-Sicherheitsprüfung; keine Schlussfolgerungen aus fehlender Reproduktion. |
