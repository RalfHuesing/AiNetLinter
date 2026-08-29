---
status: done
type: step-review
task: decompiled-assembly-analysis
step: 016
epic: EPIC-04
step_type: single
reviewed_by: kritiker
reviewed_by_model: gpt-5 (Codex)
reviewed_by_model_knowledge_cutoff: unbekannt
reviewed_at: 2026-08-29T02:09:22+02:00
verdict: blocked
tech_debt_ids: []
---

# Review Step 016: Repository-Akquisitionsgrenze sicher korrigieren

## Verdict

- [ ] **approved** — alle vier Prüfebenen ok
- [ ] **issues** — Korrektur-Step erforderlich
- [x] **blocked** — Infrastruktur-Nachweis und Nutzerentscheidung nötig

Die Code- und Teständerungen wurden gegen alle fünf Findings aus
`step-015/step-review.md` sowie die acht Step-016-Kriterien geprüft. Die
Abnahme ist blockiert, weil der einzige echte Reparse-Test auf diesem Host
keinen Symlink anlegen kann und deshalb das vollständige FastTests-Gate rot
bleibt. Der Test wurde weder abgeschwächt noch durch eine Attributsimulation
ersetzt.

Zusätzlich bleibt im Cancellation-Cleanup ein in-scope Nachweisdefizit: Der
Rückgabewert von `ownership.TryCleanup()` wird im
`OperationCanceledException`-Pfad verworfen. Bei fehlgeschlagenem Cleanup
gibt es dort weder ein Resultat noch eine sichtbare Diagnose. Das ist nach
dem Infrastrukturblocker der nächste zu klärende Punkt für das Kriterium
„sichtbarer Cleanup“.

## Geprüft

- [ ] Plan-Erfüllung: der Reparse-Nachweis ist wegen der Hostvoraussetzung
  nicht vollständig erbracht; die übrigen Punkte sind statisch und durch
  die erreichbaren Regressionen geprüft
- [x] Rules-Konformität: die im Plan referenzierten Regeln sind im
  Produktionsscope eingehalten; MCP meldet keine Violations
- [ ] Logische Korrektheit: die erreichbaren Pfade sind geprüft, der echte
  Reparse-Pfad und Cleanup-Fehler bei Cancellation bleiben offen
- [x] Konzept-Treue: kein Scope-Drift in Richtung Netzwerk, Git, Cache,
  Snapshot, Host-Wiring, Assembly-Loading oder Reflection
- [x] Build: selbst nachgeprüft, grün
- [ ] Tests: 28 von 29 fokussierten Tests und 1965 von 1966 FastTests
  bestanden; ein Test ist umgebungsbedingt fehlgeschlagen

## Befund

### Plan-Erfüllung

Die fünf Step-015-Findings sind im geänderten Code weitgehend adressiert:

1. `ExternalSourceRepositoryAcquirer.ExecuteTransportAsync` behandelt
   `OperationCanceledException` separat und bildet alle übrigen Exceptions
   über `ExternalSourceRepositoryFailurePolicy` auf vorhandene typed
   Failure-Kinds ab. Die erreichbaren HTTP-, Timeout-, Berechtigungs- und
   sonstigen Exception-Regressionen prüfen Ergebnis und Cleanup.
2. Nach dem Transport-`await` wird das Cancellation-Token in
   `AcquireReservedCheckoutAsync` erneut geprüft. Der direkte Test für einen
   scheinbar erfolgreichen, aber danach abgebrochenen Transport besteht.
3. `ExternalSourceRepositoryTransportResult` projiziert Code, Severity,
   Location und Nachricht auf feste, geheimnisfreie Vertragswerte. Die
   direkte Diagnose-Regression besteht.
4. `CreateDirectoryW` reserviert den Checkout-Child atomar; Ownership-Marker,
   Parent-/Reparse-Prüfung, Tree-Prüfung und fremdbaum-sicheres Cleanup sind
   im Code vorhanden. Der Fremdbaum-Test besteht. Der echte Symlink-Test kann
   auf diesem Host nicht bis zu dieser Prüfung gelangen.
5. `IsFileSystemException` ist in
   `ExternalSourceRepositoryFailurePolicy` zentralisiert und wird von den
   Acquirer-, Reservation-, Model- und PathGuard-Pfaden verwendet.

Damit sind Kriterien 1, 2, 3, 5 und 8 durch Codeprüfung sowie erreichbare
Tests belegt. Kriterium 4 und der Reparse-Anteil von Kriterium 6 bleiben bis
zu einem berechtigten Windows-Lauf offen. Kriterium 7 ist durch den grünen
Integration-Gate und die unveränderte Abgrenzung erfüllt.

### Rules-Konformität

Der betroffene Produktionsscope hat keine MCP-Linter-Violations. Nullable-,
Sealed-, Methoden- und Architekturvorgaben sind eingehalten; es wurden kein
DI-/Plugin-System, kein Runtime-Assembly-Laden und keine Reflection-
Ausführung eingeführt. Die Testklasse nutzt weiterhin TestKit-
Tempverzeichnisse und erzeugt den Reparse-Punkt nur als echten Windows-
Symlink.

### Logische Korrektheit

Die erreichbaren Fehlerpfade liefern typed Failure-Kinds, feste Diagnosen
und bereinigen den eigenen reservierten Child. Die atomare Erstreservierung
über `CreateDirectoryW`, der Ownership-Marker und die Nachprüfung gegen
Fremdbaum/Reparse sind nachvollziehbar; der Fremdbaum-Test bestätigt, dass
der ersetzte fremde Pfad nicht gelöscht wird. Der Handle-Dispose setzt einen
beobachtbaren Cleanup-Zustand und bleibt idempotent.

Der echte Reparse-Ausbruch ist nicht empirisch bestätigt: In
`ExternalSourceRepositoryAcquirerTests.cs:246` wirft
`Directory.CreateSymbolicLink` auf diesem Host wegen fehlender Berechtigung
eine `IOException`. Diese wird vom Transport-Catch als normaler Transport-
Fehler behandelt; dadurch scheitert die erwartete
`RepositoryCheckoutInvalid`-Assertion in Zeile 254. Das ist ein
Infrastrukturfehler des Testhosts, kein Anlass für eine schwächere Assertion.

Im Abbruchpfad
`ExternalSourceRepositoryAcquirer.cs:90-94` wird das Cleanup ausgeführt,
aber sein boolesches Ergebnis verworfen. Schlägt Cleanup nach einer
Cancellation fehl, kann dieser Zustand nicht über das Acquisition-Result
gemeldet werden, weil die Cancellation unverändert weitergereicht wird. Der
Step-Plan verlangt jedoch sichtbare Cleanup-Fehler; dafür fehlt eine
definierte Beobachtbarkeit oder Logging-Strategie in diesem Pfad.

### Konzept-Treue (Ebene 4)

Die Umsetzung bleibt innerhalb der in Konzept Phase 4 vorgesehenen
Akquisitionsgrenze. Sie behauptet keine konkrete Gitea-/HTTP-/Git-
Implementierung, führt keine fremden Assemblies aus und greift keine
nachgelagerte Snapshot-, Cache- oder Source-of-Truth-Semantik vor. Die
verbleibende Unsicherheit betrifft ausschließlich den fehlenden privilegierten
Reparse-Nachweis und die Sichtbarkeit eines Cleanup-Fehlers während echter
Cancellation.

### Build-/Test-Status

```text
dotnet build
→ grün (0 Warnungen, 0 Fehler)

dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~ExternalSourceRepositoryAcquirerTests --logger "trx;LogFileName=Step016-Review-Focused.trx"
→ rot (28 bestanden, 1 fehlgeschlagen, 29 gesamt, 0 übersprungen)

dotnet test src/AiNetLinter.FastTests --filter Category!=Stress --logger "trx;LogFileName=Step016-Review-FastTests.trx"
→ rot (1965 bestanden, 1 fehlgeschlagen, 1966 gesamt, 0 übersprungen)

dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress --logger "trx;LogFileName=Step016-Review-IntegrationTests.trx"
→ grün (360 Tests, 0 Fehler, 0 übersprungen)
```

Stress-Tests wurden nicht ausgeführt. Der eine Fehler betrifft ausschließlich
`AcquireAsync_ActualReparseEntry_IsRejectedAndExternalSentinelRemains`.
Die TRX-Dateien bestätigen dieselbe fehlgeschlagene Assertion; die
ursächliche Hostvoraussetzung ist die verweigerte
`Directory.CreateSymbolicLink`-Operation. Die read-only-Prüfung dieses Hosts
zeigt kein `SeCreateSymbolicLinkPrivilege`; ein aktivierter Developer Mode ist
nicht vorhanden.

## Findings / Blocker

1. `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryAcquirerTests.cs:246,254` — **[BLOCKER] [Plan/Logik]** Der geforderte echte Reparse-Test kann auf dem aktuellen Windows-Host keinen Symlink erzeugen. Dadurch wird der Transport vor der produktiven Reparse-Prüfung als fehlgeschlagen beendet, die `RepositoryCheckoutInvalid`-Assertion greift nicht und beide FastTests-Gates bleiben rot. **Fix:** Den unveränderten Test unter einem Windows-Konto mit aktiviertem Developer Mode oder `SeCreateSymbolicLinkPrivilege` ausführen und anschließend den fokussierten Test sowie das FastTests-Nicht-Stress-Gate wiederholen. Keine Attributsimulation, Fake-Assertion, Privilegienänderung oder Systemänderung durchführen.

2. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs:90-94` — **[MAJOR] [Logik]** Der Cancellation-Cleanup-Pfad verwirft den Rückgabewert von `ownership.TryCleanup()`. Bei verlorener Ownership oder einem Dateisystemfehler wird die echte Cancellation zwar korrekt weitergereicht, der Cleanup-Fehler ist aber nicht sichtbar. **Fix:** Für diesen Pfad einen expliziten, mit dem unveränderten Cancellation-Vertrag kompatiblen Beobachtungsmechanismus festlegen und implementieren, mindestens eine Regression für fehlgeschlagenes Cleanup während Cancellation ergänzen. Die Cancellation darf dabei nicht in einen Provider-Failure umgewandelt werden.

## Frage an Nutzer

Bitte den fokussierten Test unter einer berechtigten Windows-Umgebung
ausführen lassen. Eine sichere spätere Gating-Lösung wäre nur auf Testebene
vertretbar: Ein Capability-Preflight darf den Test ausschließlich bei
fehlender Symlink-Berechtigung als übersprungen markieren, während der
Testkörper und seine echte Reparse-Assertion unverändert bleiben; zusätzlich
ist ein privilegierter CI-/Abschlusslauf ohne Skip erforderlich. Im aktuellen
Review wurde keine solche Teständerung vorgenommen, da ausschließlich diese
Review-Dokumentation geändert werden durfte. Ohne privilegierten Lauf bleibt
der Step blockiert.

## MCP- und Scope-Audits

- `get_file_tree` und `get_index_scope` wurden mit absolutem
  `projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter` verwendet; der
  Assembly-Produktionsscope ist vollständig sichtbar.
- `find_symbol`, `get_feature_context`, `get_symbol_body`,
  `find_references`, `get_impact`, `get_violations` und `safeguard` wurden
  für Acquirer, Transportresultat, Reservation, Ownership/Handle, Policy und
  PathGuard verwendet. Die Symbol-/Referenzresultate zeigen die erwarteten
  Produktions- und Testaufrufer. `get_violations` meldet 0 Violations im
  betroffenen Produktionsscope; `safeguard` meldet 8,50/10 und nur den
  bekannten, außerhalb liegenden `DaemonHostCommand`-Footprint.
- Der Git-Diff-Modus von `get_impact` konnte den lokalen Commit
  `4f49c0bd` im MCP-Index nicht laden und meldete „kein Git-Repository oder
  leerer Diff“. Der Commit-Diff wurde deshalb zusätzlich direkt mit
  `git --no-pager show` geprüft; er enthält genau die sieben erwarteten
  Code-/Testdateien.
- Der begrenzte Exact-DRY-Scan mit
  `find_duplicates(scopeDir="src/AiNetLinter/Mcp/Assemblies",
  minTokens=1, similarityThreshold="exact", scopeType="production")`
  findet 0 Cluster über 214 Methoden. `IsFileSystemException` ist per
  gezieltem `rg` nur einmal deklariert und zentral verwendet.
- Der Magic-Value-Audit im Assembly-Produktionsordner findet 69 bestehende
  Einzelkandidaten über 26 Dateien. In den geänderten Dateien verbleiben nur
  lokalisierbare Guard-/Vertragsmeldungen sowie der als Konstante geführte
  `checkout-`-Präfix; kein neuer sicherer Magic-Value-Fund bleibt offen.
- `find_dead_code` findet im Assembly-Produktionsordner 0 High-Confidence-
  Dead-Code-Kandidaten bei 76 geprüften Symbolen.

