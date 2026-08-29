---
status: done
type: step-result
task: decompiled-assembly-analysis
step: 026
corrects: null
epic: EPIC-04
step_type: single
coded_by: coder
coded_by_model: gpt-5 (Codex)
coded_at: 2026-08-29
code_commit_hash: siehe finalen Commit (Abschlussantwort)
status_after: done (pending audit)
blocker_category: n/a
---

# Result Step 026: Persistente Repository-Cache-Generation atomar veröffentlichen

## Zusammenfassung

Der bestehende erfolgreiche Clone-/Acquirer-Pfad kann nun einen vollständigen,
lokalen Repository-Snapshot als neue Cachegeneration veröffentlichen. Der
credentialfreie Cache-Key wird aus eigener Schema-Version, bereits
normalisierter Repository-URL und sicherem repository-relativem Solution-Pfad
gebildet. Jede Generation enthält ein internes Manifest mit Identität,
geladener Revision, Generation, UTC-Zeitstempel und einem vollständigen
relativen Datei-Inventar aus Länge und SHA-256-Inhaltshash.

Die Veröffentlichung arbeitet unter einem injizierbaren
`IExternalSourceRepositoryCacheWriter`-Vertrag mit kontrollierter Cache-Wurzel.
Sie staged ausschließlich in einer neuen `generation-*`-Directory, validiert
Manifest und Inhalt per Read-back und ersetzt `current` erst danach atomar.
Der bestehende Current bleibt bei Validierungsfehlern, Pointer-Fehlern und
Cancellation unverändert. Die Synchronisation ist lokal pro Entry-/Cache-Key;
es gibt keinen globalen Host- oder Registry-Lock.

## Geänderte Dateien

- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheContract.cs` —
  eigene Schema-/Dateinamen-/Bounds-/Pfadkonstanten, stabile Key-Ableitung und
  sichere relative Pfad-/Generation-Prüfung.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheModels.cs` —
  interne Key-, Manifest-, Datei-Inventar-, Publish- und Read-back-Verträge
  sowie typisierte Publish-Fehlerdiagnosen.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheStorage.cs` —
  kontrolliertes Staging, vollständiges Kopieren ohne Ownership-Marker,
  Hashing, Manifest-/Pointer-Schreiben, atomare Pointer-Ersetzung und
  fail-closed Cleanup-/Root-/Reparse-Guards.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheReader.cs` —
  bounded JSON-Read-back, strikte Identitäts-/Revisions-/Solution-Prüfung und
  vollständige Inventar-/Hash-Prüfung gegen den Generation-Inhalt.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheWriter.cs` —
  konkreter lokaler Generation-Writer, per-Key-Synchronisation und
  Publish-/Rollback-Lifecycle.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs` —
  injizierter Write-through-Aufruf ausschließlich nach bestehender erfolgreicher
  Transport-, Checkout-, Solution- und Revision-Prüfung; Cache-Fehler bleiben
  typed/warnend sichtbar, entziehen dem Acquirer aber keinen gültigen Erfolg.
- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheWriterTests.cs` —
  lokale Tests für Key, Manifest/Hash, Pfade, Pointer, Atomicity, Konkurrenz,
  Cancellation, Ownership und Reparse-Verhalten.
- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheAcquirerTests.cs` —
  lokale Write-through-, Ownership-, Cache-Fehler- und No-Publish-bei-
  Transportfehler-Regressionen.
- `tasks/decompiled-assembly-analysis/step-026/step-result.md` — dieser
  Nachweis.

Nicht geändert wurden Cache-/AssemblyCache-Reuse, Fetch/Refresh/Transport,
Configuration-/Credential-Schema, CheckoutHandle-/Snapshot-/Registry-Cleanup,
Provider/Materializer/Host-/MCP-Wiring, Orchestrator, EPIC-05, Dirty/Health/
degraded, Retention/GC/Invalidierung/Telemetrie sowie
`task-state.md`, `roadmap.md` und `tech-debt.md`.

## Kriterienabdeckung

- **Key:** `ExternalSourceRepositoryCacheKey` normalisiert die URL über die
  bestehende URL-Policy, normalisiert und begrenzt den Solution-Pfad und hasht
  Schema/URL/Pfad deterministisch. Userinfo wird abgewiesen; Key, Entry-Pfad
  und Diagnosen enthalten keine Credentials.
- **Manifest und Inventar:** Das Manifest hält Schema, 64-stelligen Key,
  kanonische URL, SolutionPath, geladene Revision, Generation und UTC-Zeitpunkt.
  Jede kopierte Datei wird beim Kopieren gehasht; der Read-back prüft Länge,
  Hash, Pfadmenge, Duplikate, Bounds und exakte Datei-Menge.
- **Isolation und Guards:** Cache-Root und Entry-Root werden kanonisiert und
  als sichere Nachfahren geprüft. Staging erfolgt ausschließlich unter einer
  neuen `generation-*`-Directory. Ownership-Marker werden nicht kopiert.
  Reparse-Punkte, unsichere relative Pfade, fremde/fehlende Solution-Dateien,
  unbounded JSON und unbounded Inventare führen fail-closed zu typed failures.
- **Atomarity:** Manifest und Content werden vollständig geschrieben und
  gelesen, bevor `current` veröffentlicht wird. Der Pointer wird als temporäre
  Datei geschrieben und per `File.Replace` bzw. initialem `File.Move` atomar
  veröffentlicht. Ein fehlgeschlagener Post-Publish-Read-back stellt den
  vorherigen Generation-Namen wieder her; Cancellation nach Pointer-Publish
  rollt ebenfalls auf den vorherigen Current zurück.
- **Ownership:** Der Request-Checkout bleibt beim Acquirer/Handle. Der Writer
  liest ihn nur, entsorgt weder Handle noch Ownership und übernimmt keinen
  persistenten Cachepfad in einen `ExternalSourceCheckoutHandle`. Der Cache
  bleibt nach Handle-Dispose lesbar. Write-through wird nur nach erfolgreicher
  bestehender Clone-/Revision-Validierung aufgerufen.
- **Fehlersemantik:** Publish-Fehler werden als `FailureKind` und sichere
  Warning-Diagnose sichtbar. Ein Cache-Fehler lässt den validen Acquirer-
  Checkout-Erfolg bestehen; bei Transportfehler oder Cancellation vor dem
  Publish wird kein Cache-Write als Acquisition-Erfolg erzeugt.
- **Out-of-Scope:** Kein Current-Eintrag wird als Acquisition-Erfolg
  wiederverwendet; es gibt keinen Fetch-/Refresh-Pfad, keine neue Konfiguration,
  keine Assembly.Load-/Reflection-/Restore-/Build-/Test-Ausführung fremder
  Checkouts und keine Netzwerk-/Git-Prozesse in den neuen Tests.

## Teststatus

```text
Fokussierter Cache-/Acquirer-Lauf
  dotnet test src/AiNetLinter.FastTests --filter
    "FullyQualifiedName~ExternalSourceRepositoryCacheWriterTests|
     FullyQualifiedName~ExternalSourceRepositoryAcquirerTests"
  45 bestanden, 2 übersprungen, 0 Fehler, 47 gesamt
  Skips: zwei echte Reparse-/Symlink-Fälle wegen Win32
         ERROR_PRIVILEGE_NOT_HELD (1314)

dotnet build
  erfolgreich; 0 Warnungen, 0 Fehler

dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
  2.013 bestanden, 2 übersprungen, 0 Fehler, 2.015 gesamt
  Skips: zwei echte Reparse-/Symlink-Fälle wegen Win32
         ERROR_PRIVILEGE_NOT_HELD (1314)

dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
  370 bestanden, 0 übersprungen, 0 Fehler

Stress-Kategorie
  nicht ausgeführt
```

Die neuen Tests sind vollständig lokal und netzwerkfrei. Es wurden keine
echten Remote-/Gitea-/Git-Prozesse, keine Netzwerkzugriffe und keine fremden
Checkout-Builds ausgeführt. Nach dem Lauf existieren keine aktuellen
`external-source-*`-Testverzeichnisse und keine Testhost-/VSTest-Prozesse.
Die sichtbaren `dotnet`-Prozesse sind wiederverwendete MSBuild-Nodes
(`/nodeReuse:true`), keine von diesem Step gestarteten Testprozesse.

## MCP- und Qualitätsbefunde

- `get_feature_context` bestätigte für
  `ExternalSourceRepositoryCacheKey` und
  `LocalExternalSourceRepositoryCacheWriter` vollständige Deklaration,
  direkte Aufrufer, Testzuordnung und 0 Datei-Violations. Der Acquirer-
  Kontext bestätigt 0 Violations; der Writer ist direkt vom Acquirer und den
  lokalen Tests erreichbar.
- `get_impact` für den lokalen Writer zeigte den direkten Write-through-
  Aufrufer im Acquirer und die lokalen Cache-/Acquirer-Tests. Es wurden keine
  Provider-, Materializer-, Host- oder Orchestrator-Wiring-Änderungen
  benötigt.
- `get_violations` im direkten Cache-Scope (7 Dateien) und auf
  `ExternalSourceRepositoryAcquirer.cs` melden jeweils 0 Violations.
  `safeguard` im direkten Cache-Scope ist PASS; sichtbar bleiben nur die
  bestehenden Warnungen zum übergroßen `Mcp/Assemblies`-Verzeichnis,
  `DaemonHostCommand`-Footprint und `tasks/decompiled-assembly-analysis`-
  Verzeichnis. Diese liegen außerhalb des Steps.
- **DRY / `find_duplicates`:** Der nach der Implementierung solutionweite
  Produktionsscan mit `scopeDir=src`, `minTokens=20` fand 1 Exact-Cluster
  zwischen den bestehenden `FindAssemblyExtensionsTool`-/
  `InspectAssemblyTool`-Methoden. Der Near-Scan zeigte denselben bestehenden
  Cluster; der optionale Structural-Scan fand 9 bestehende Kandidaten in
  Daemon-, MetricsTree-, Transport-, DuplicateDetection-, AssemblyTool-,
  DependencyGraph-, RuleRegistry-, Baseline- und Native-Code. Kein Befund
  liegt im neuen Cache-Paket.
- **MagicValues / `find_magic_values`:** Im direkten Produktions-/Testscope
  wurden 27 eindeutige Literale gefunden: bewusstes Cache-Schema, Generation-
  und Diagnosekonstanten sowie lokale Test-Fixture-/URL-/Revision- und
  temporäre Prefix-Werte. Es gibt keinen Security-Candidate für ein Secret;
  die Credential-URL ist ausschließlich ein negativer Policy-Testwert und
  wird nicht in Key, Pfad, Manifest oder Diagnose übernommen.
- **DeadCode / `find_dead_code`:** Im direkten Scope wurden 23 Symbole
  geprüft; es gibt 0 unreferenzierte Symbole (0 high, 0 low).
- Der direkte MCP- und fokussierte `rg`-Review bestätigt keinen neuen
  Assembly.Load-/ALC-/Reflection-/Restore-/Build-/Test-/Netzwerk- oder
  Prozesszugriff. Bestehende Low-Confidence-/Magic-/Directory-Befunde wurden
  nicht global bereinigt.

## Offene Risiken

- Der echte Symlink-/Reparse-Test bleibt auf diesem Host wegen Win32 1314
  übersprungen. Der Test ist capability-gated und meldet den Skip transparent;
  die Guard-Logik wird zusätzlich durch lokale Attribute-/Path-Tests und
  fail-closed Read-/Write-Pfade abgedeckt.
- Rollback kann bei einem unabhängigen, externen Dateisystemfehler nicht
  erzwingen, dass ein bereits beschädigter fremder Pointer repariert wird. Im
  kontrollierten lokalen Writer-Lifecycle erfolgt keine Pointer-Veröffentlichung
  vor erfolgreichem Generation-Read-back; eigene Cancellation-/Read-back-
  Fehler rollen den vorherigen Current zurück.
- Die drei bestehenden Safeguard-Warnungen bleiben bewusst außerhalb dieses
  Steps. Kein globaler DRY-, MagicValues- oder DeadCode-Sweep wurde ausgelöst.

## Commit

Der Commit enthält ausschließlich die Produktions-/Teständerungen dieses
revidierten Step-026-Pakets und diesen Result-Nachweis. Kein Push.
