---
status: blocked
type: step-result
task: decompiled-assembly-analysis
step: 016
corrects: step-015
epic: EPIC-04
step_type: single
coded_by: coder
coded_by_model: gpt-5 (Codex)
coded_by_model_knowledge_cutoff: nicht angegeben
coded_at: 2026-08-29T01:56:23+02:00
code_commit_hash: 4f49c0bd
status_after: blocked
blocker_category: infrastructure
---

# Result Step 016: Repository-Akquisitionsgrenze sicher korrigieren

## Zusammenfassung

Die Akquisitionsgrenze mappt nun alle nicht-Cancellation-Transportausnahmen
auf typisierte Failure-Kinds, projiziert Transportdiagnosen auf feste,
geheimnisfreie Vertragswerte und bereinigt den eigenen Checkout auch nach
Cancellation oder Transportfehlern. Ein atomar reservierter Windows-Child mit
Ownership-Token, Parent-/Reparse-Prüfungen und sichtbarem Cleanup-Zustand
schützt vor fremden oder ersetzten Arbeitsbäumen; die fokussierten
Regressionen decken diese Pfade ab.

## Geänderte Dateien

- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs` — schließt Transport-, Cancellation-, Ownership- und Cleanup-Pfade als zusammenhängenden Akquisitionsvertrag.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquisitionModels.cs` — ergänzt Ownership-Daten und beobachtbaren Cleanup-Zustand des Checkout-Handles.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryPathGuard.cs` — zentralisiert reparse-sichere Ownership-Prüfung und nicht-traversierendes Cleanup.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCheckoutReservation.cs` (neu) — reserviert atomare Checkout-Childs und schreibt den Ownership-Marker.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryFailurePolicy.cs` (neu) — zentralisiert Exception-Klassifikation und sichere Transportdiagnoseprojektion einschließlich `IsFileSystemException`.
- `src/AiNetLinter/Mcp/Assemblies/IGiteaRepositoryTransport.cs` — validiert und redigiert Diagnosen an der Transport-Vertragsgrenze.
- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryAcquirerTests.cs` — ergänzt Ausnahmemapping, Nach-Cancellation, Geheimnisredaktion, Fremdbaum-, Reparse- und Cleanup-Fehler-Regressionen.

## Commit

- **Code-Commit-Hash:** `4f49c0bd`
- **Message:**
  ```
  fix(mcp): Besitzgrenze absichern [decompiled-assembly-analysis]

  Typisiere Transportausnahmen und redigiere Diagnosen sicher.

  Reserviere Checkout-Childs atomar und prüfe Ownership, Reparse und Cleanup.

  Refs: tasks/decompiled-assembly-analysis/step-016
  ```
- **Branch:** `main`
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit nach diesem Ergebnis.

## Build-/Test-Output

```
dotnet build → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter Category=Unit → grün (1360 Tests, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~ExternalSourceRepositoryAcquirerTests → rot (28 bestanden, 1 fehlgeschlagen, 29 gesamt)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress → rot (1965 bestanden, 1 fehlgeschlagen, 1966 gesamt)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → grün (360 Tests, 0 Fehler)
```

Der einzige Fehler ist der echte Test
`AcquireAsync_ActualReparseEntry_IsRejectedAndExternalSentinelRemains`: Das
Zielkonto erhält beim lokalen `Directory.CreateSymbolicLink` wegen fehlender
Windows-Berechtigung `System.IO.IOException` („Dem Client fehlt ein
erforderliches Recht.“). Die TRX-Dateien `TestResults/Step016-Focused.trx`
und `TestResults/Step016-FastTests-final.trx` wurden auf genau diesen Fehler
ausgewertet. Der Test bleibt ein echter Symlink-Test; es wurde keine
Attributsimulation und kein alternativer Prozess-/Temp-Pfad eingeführt.
Stress-Tests wurden nicht ausgeführt.

## Abweichungen vom Plan

- Die geplanten Reservierungs- und Policy-Operationen wurden in zwei eng
  gekoppelte interne Hilfstypen ausgelagert, damit Acquirer und PathGuard die
  drei vorgesehenen Schichten behalten und die Methodenmetriken unter den
  Projektgrenzen bleiben. Die zwei Fachverträge und der Scope-Split wurden
  nicht erweitert.
- Wegen des im Plan ausdrücklich vorgesehenen Windows-Infrastruktur-Blockers
  steht der Stepstatus auf `blocked` statt auf `done (pending audit)`. Die
  direkte Reparse-Assertion wurde nicht abgeschwächt.
- `task-state.md`, `roadmap.md`, `codemap.md` und `tech-debt.md` blieben
  unverändert wie beauftragt. Die Testdatei wurde nur durch Entfernen von
  Leerzeilen unter das bestehende MaxLineCount-Gate gebracht; Assertions und
  Testumfang blieben erhalten.

## Beobachtungen

- Der abschließende AiNetLinter-MCP-Audit meldet im betroffenen Produktions-
  scope 0 Violations, 0 exakte Duplikat-Cluster bei `minTokens=1` und
  `similarityThreshold=exact` über 214 Methoden sowie 0 High-Confidence-
  Dead-Code-Kandidaten. Der Magic-Value-Audit meldet ausschließlich die
  bestehende lokalisierbare Konstruktor-Guard-Meldung und den bewusst als
  Konstante gefassten `checkout-`-Präfix.
- Die `IsFileSystemException`-Klassifikation existiert im Akquisitionsscope
  nur noch im gemeinsamen internen Policy-Helper. Exception-Texte,
  URL-Userinfo, Credentials und Tokenfragmente werden nicht in Result-
  Diagnosen übernommen.
- Es wurden keine Provider-/Host-/Snapshot-/Cache-/Netzwerkadapter,
  Credential-Bindings, Mapping-/JSON-Änderungen, `Assembly.Load` oder
  Reflection eingeführt; TD-001 bis TD-003 wurden nicht erweitert.

## Bekannte Unschärfen

- Die Zielplattform verweigert dem Testkonto die tatsächliche lokale
  Symlink-Erzeugung. Dadurch ist die Sentinel-Unverändertheit unter einem
  realen Reparse-Ausbruch in diesem Lauf nicht empirisch bestätigt, obwohl
  die Testanlage und die produktive Reparse-Schutzlogik dafür unverändert
  vorhanden sind. Der Kritiker sollte diesen Test nach Aktivierung von
  Developer Mode oder der erforderlichen Symlink-Berechtigung erneut laufen
  lassen.
- Die Reservierung verwendet auf Windows atomisches `CreateDirectoryW` plus
  zufälligen Token-Marker; der Cleanup-Vertrag bleibt bewusst über
  Ownership-Nachprüfung, Reparse-Sperre und sichtbaren Status definiert und
  führt keinen weitergehenden Host-/OS-Handle-Vertrag ein.

## Falls Status `blocked`

**Blocker-Art:** `infrastructure`

**Blockiert weil:** Der direkte Windows-Symlink-Test kann wegen fehlender
`CreateSymbolicLink`-Berechtigung nicht bis zur produktiven Reparse-Prüfung
ausgeführt werden. Der Step-Plan verbietet ausdrücklich, diesen Beleg durch
einen Attribut-Stub zu ersetzen; dadurch bleibt das vollständige
FastTests-Nicht-Stress-Gate rot.

**Brauche von Nutzer:** Einen Testlauf unter einem Windows-Konto mit
aktiviertem Developer Mode oder der benötigten Symlink-Berechtigung; danach
erneut den fokussierten Test und das FastTests-Nicht-Stress-Gate starten.

**Aktueller Stand:** Code und fokussierte Regressionen sind in `4f49c0bd`
gesichert. Build, Unit-Slice und IntegrationTests-Nicht-Stress sind grün;
der verbleibende Fehler ist ausschließlich die lokale Testvoraussetzung.
Der neue Kritiker soll erst nach erfolgreichem Reparse-Gate starten; eine
reine Codeinspektion ist möglich, ersetzt aber die ausstehende
Infrastrukturverifikation nicht.
