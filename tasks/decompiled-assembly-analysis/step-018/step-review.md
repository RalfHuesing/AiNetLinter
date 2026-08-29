---
status: done
type: step-review
task: decompiled-assembly-analysis
step: 018
epic: EPIC-04
step_type: single
reviewed_by: kritiker
reviewed_by_model: gpt-5 (Codex)
reviewed_by_model_knowledge_cutoff: nicht angegeben
reviewed_at: 2026-08-29T06:33:28+02:00
verdict: approved
tech_debt_ids: []
---

# Review Step 018: Repository-Capability-Fallback zum Decompilation-Fallback

## Verdict

- [x] **approved** — alle vier Prüfebenen ok
- [ ] **issues** — Korrektur-Step erforderlich
- [ ] **blocked** — reproduzierbarer Blocker fehlt

Die Implementierung erfüllt die beiden Step-018-Verträge unter der
expliziten Laufzeit-Fallback-Policy. Genau `ERROR_PRIVILEGE_NOT_HELD` (1314)
und tatsächlich erkannte Reparse-Checkouts werden für die betroffene Source
als `ProviderUnavailable` mit dem stabilen, geheimnisfreien Code
`RepositoryCapabilityUnavailable` behandelt. Authentifizierungs-,
AccessDenied-, sonstige Transportfehler und Cancellation behalten ihre
Semantik. Die Failure-only-Projection reicht den Zustand bis zum bestehenden
Orchestrator weiter; der statische Decompilation-Fallback bleibt erreichbar.

## Geprüft

- [x] Plan-Erfüllung: Akquisitionsklassifikation, Reparse-Guard,
  Failure-only-Projection und Fallback-Regression sind umgesetzt.
- [x] Rules-Konformität: Produktions- und Testscope sind regelkonform; es
  gibt keine globale Capability-Sperre und keinen privilegierten
  Systemeingriff.
- [x] Logische Korrektheit: 1314 und tatsächliche Reparse-Funde sind eng
  abgegrenzt; übrige Fehler und Cancellation werden nicht maskiert.
- [x] Konzept-Treue: Erfolgreiches Acquirer→Snapshot-Wiring bleibt wie
  geplant außerhalb des Steps; normale Sources bleiben nutzbar.
- [x] Build: selbst nachgeprüft, grün ohne Warnungen oder Fehler.
- [x] Tests: fokussierte Contracts und beide vollständigen Nicht-Stress-Gates
  sind grün; der einzige Skip ist transparent und policy-konform.

## Befund

### Plan-Erfüllung

`ExternalSourceRepositoryFailurePolicy` erkennt auf Windows ausschließlich den
exakten Win32-Code 1314, entweder als `Win32Exception.NativeErrorCode` oder
als Low-Word eines `IOException`-/`UnauthorizedAccessException`-HResults.
Nur diese Klassifikation liefert `ProviderUnavailable` und den stabilen Code
`RepositoryCapabilityUnavailable`; die bestehenden Zuordnungen für
`HttpRequestException`, `TimeoutException`, `UnauthorizedAccessException`
und sonstige Fehler bleiben erhalten.

`ExternalSourceRepositoryAcquirer.ValidateCheckout` unterscheidet sichere,
nicht auswertbare und tatsächlich gefundene Reparse-Punkte. Nur der Zustand
`Found` auf Checkout-Pfad oder Checkout-Baum wird als
`ProviderUnavailable`/`RepositoryCapabilityUnavailable` projiziert. Ein
Inspektionsfehler oder ein Ownership-/Checkout-Verstoß bleibt
`InvalidResponse`; damit wird nicht jede fehlende lokale Dateisystemauskunft
zu einer Capability-Aussage.

`ExternalSourceProviderFailureProjection.FromUnavailableAcquisition` nimmt
nur ein nicht verfügbares Acquisition-Ergebnis an, erzeugt keinen Snapshot
und redigiert die Diagnostik über die bestehende Failure-Policy. Der
Orchestrator erhält weiterhin keine Source-Auswahl, behält Failure-Kind und
normalisierte Diagnose, und `AssemblyAnalysisToolSupport` fällt anschließend
auf statische Decompilation zurück. Das erfolgreiche Acquirer→Snapshot-Wiring
ist nicht vorgezogen.

### Rules-Konformität

Der Code führt weder eine globale Capability-Probe noch eine
Privilegienänderung, Fake-Reparse-Assertion oder Attribut-Manipulation ein.
`AssemblyAnalysisHostComposition` und die Default-Komposition bleiben ohne
globalen Symlink-/Reparse-Schalter; eine nicht verfügbare Source wird lokal
behandelt, während normale source-backed Repositories weiterhin verwendbar
sind. Der Testtransport bleibt netzwerk-, Git- und Gitea-frei.

Die MCP-Abfragen wurden mit absolutem
`projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter` ausgeführt. Die
Violations-Abfragen melden 0 Treffer im geprüften Produktionsscope und 0
Treffer im geprüften Testscope. `get_feature_context`, Symbolkörper,
Referenzen und Impact bestätigen die Acquirer-/Failure-/Projection-/
Orchestrator-Kette. Der Commit-basierte `get_impact`-Aufruf meldete für den
angegebenen Git-Ref keinen Diff; deshalb wurde die Auswirkungsprüfung über
Symbol-Impact und Referenzen ohne Trunkierung abgesichert.

### Logische Korrektheit

Der deterministische 1314-Transporttest besteht mit
`ProviderUnavailable`, `RepositoryCapabilityUnavailable` und ohne geheime
Diagnoseinhalte. Der Contract-Test übergibt absichtlich geheime Exception-
und Location-Daten und bestätigt `$repository`, keinen Snapshot und keine
Secret-Leaks. Der Support-Test bestätigt `decompiled`, einen gültigen
statischen Typ, keine Source-Auswahl und den weitergereichten
`ProviderUnavailable`-Zustand.

Die bestehenden Auth-/AccessDenied-/Network-/Timeout-/InvalidResponse- und
Cancellation-Tests bleiben grün. Cancellation wird weitergereicht; Cleanup
wird nicht in ein Provider-Failure-Result umgewandelt. Die produktive
Erfolgskette wurde nicht an die neue Projection gekoppelt.

### Konzept-Treue (Ebene 4)

Die Änderung bleibt auf die im Plan benannten Akquisitions-,
Reparse-Klassifikations-, Provider-Vertrags- und Fallback-Grenzen beschränkt.
Cache, Refresh, Workspace, Assembly-Loading, HTTP/Git/Gitea und globale
Host-Komposition wurden nicht erweitert. Der aktuelle Host-Skip wird nicht
als privilegierter Reparse-Sicherheitsnachweis ausgegeben.

## Skip- und Policy-Bewertung

Der echte Test
`ExternalSourceRepositoryAcquirerTests.AcquireAsync_ActualReparseEntry_IsRejectedAndExternalSentinelRemains`
führt weiterhin `Directory.CreateSymbolicLink` und die produktive
Reparse-/Sentinel-Prüfung aus. Auf diesem Host liefert der reale Win32-Aufruf
`ERROR_PRIVILEGE_NOT_HELD (1314)`; der Test überspringt dann transparent und
meldet ausdrücklich, dass die Capability nicht nachgewiesen und der Skip kein
Sicherheitsnachweis ist. Andere Fehler würden nicht übersprungen.

Dieser Skip ist deshalb ein separat offener optionaler Sicherheitsnachweis,
aber unter der hier geltenden Policy kein Step-018-Blocker: Der lokale
Gate-Lauf bleibt transparent, die deterministische 1314-Klassifikation ist
getestet, und die Laufzeit-Fallback-Policy sperrt weder normale Repositories
global noch verlangt sie Privilegienänderungen.

## Build-/Test-Status

```text
dotnet build
→ grün (0 Warnungen, 0 Fehler)

dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~ExternalSourceRepositoryAcquirerTests" --logger "trx;LogFileName=Step018-review-Acquirer.trx"
→ grün (29 bestanden, 1 übersprungen, 0 Fehler; 30 gesamt)

dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~ExternalSourceProviderContractTests" --logger "trx;LogFileName=Step018-review-ProviderContract.trx"
→ grün (15 bestanden, 0 übersprungen, 0 Fehler; 15 gesamt)

dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~AssemblyAnalysisToolSupportTests" --logger "trx;LogFileName=Step018-review-Support.trx"
→ grün (15 bestanden, 0 übersprungen, 0 Fehler; 15 gesamt)

dotnet test src/AiNetLinter.FastTests --filter "Category!=Stress" --logger "trx;LogFileName=Step018-review-FastTests.trx"
→ grün (1969 bestanden, 1 übersprungen, 0 Fehler; 1970 gesamt)

dotnet test src/AiNetLinter.IntegrationTests --filter "Category!=Stress" --logger "trx;LogFileName=Step018-review-IntegrationTests.trx"
→ grün (360 bestanden, 0 übersprungen, 0 Fehler; 360 gesamt)
```

Der einzige Skip im Acquirer- und FastTests-Lauf ist der echte
Reparse-/Sentinel-Test wegen des transparenten 1314-Preflights. Stress-Tests
wurden nicht ausgeführt.

## MCP-/DRY-/MagicValues-/DeadCode-Ergebnis

- `get_feature_context`, `get_symbol_body`, `find_symbol`,
  `find_references` und `get_impact` bestätigen die relevanten Symbole,
  Call-Sites und den unveränderten Orchestrator-/Fallback-Pfad.
- `find_duplicates` im Produktionsscope findet mit `minTokens=1` und
  `similarityThreshold=exact` 0 Cluster; der
  `refactoring-drift`-Scan für `ProjectTransportDiagnostics` findet 0
  Kandidaten. Die strukturellen Near-Kandidaten sind bestehende oder
  semantisch getrennte Konstrukte; kein neuer DRY-Fund entsteht.
- `find_magic_values` meldet im gezielten Scope 37 Einträge der bestehenden
  Diagnosecode-Tabelle (einschließlich der absichtlich neuen stabilen
  Capability-ID), eine unveränderte Acquirer-Lokalisierungsnachricht und
  zwei unveränderte Provider-Validierungsnachrichten. 1314 und `0xFFFF` sind
  benannte Konstanten; kein neuer sicherheitsrelevanter In-Scope-Fund ist
  offen.
- `find_dead_code` meldet nur zwei Low-Confidence-Bestandskandidaten
  außerhalb des geänderten Step-Scope; kein neuer High-Confidence- oder
  Step-bezogener Dead-Code-Fund.
- `safeguard` liefert 8,79/10 (`PASS`); der angezeigte bestehende
  `DaemonHostCommand`-Footprint liegt außerhalb des Step-Scopes.

Es entsteht kein neuer oder geänderter Tech-Debt-Fund. `tech-debt.md` bleibt
unverändert.

## Empfohlene Folgeaktion

Step 018 kann abgeschlossen werden. Optional sollte der unveränderte echte
Reparse-/Sentinel-Test auf einem Windows-Host mit vorhandener Symlink-
Capability erneut ausgeführt werden; dieser separate Nachweis darf weder als
globale Laufzeitvoraussetzung noch durch Privilegien- oder Assertion-
Änderungen ersetzt werden.
