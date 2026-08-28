---
status: done
type: step-review
task: decompiled-assembly-analysis
step: 015
epic: EPIC-04
step_type: single
reviewed_by: kritiker
reviewed_by_model: gpt-5 (Codex)
reviewed_by_model_knowledge_cutoff: unbekannt
reviewed_at: 2026-08-29T01:01:19.8888236+02:00
verdict: issues
tech_debt_ids: []
---

# Review Step 015: Repository-Akquisition kapseln

## Verdict

- [ ] **approved** — alle vier Prüfebenen ok
- [x] **issues** — Korrektur-Step erforderlich (`corrects: step-015`); ein neuer Coder-Step ist noch nicht angelegt
- [ ] **blocked** — Nutzer-Entscheidung nötig

## Geprüft

- [ ] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `.agents/rules/**` eingehalten, abgesehen von den unten genannten in-scope DRY- und Sicherheitsbefunden
- [ ] Logische Korrektheit: Code macht was er soll, nicht nur „grün"
- [ ] Konzept-Treue: passt die Umsetzung zu `Konzept.md` (Scope, Non-Goals, Muss-Haben)
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün (Stress nicht ausgeführt)

## Befund

### Plan-Erfüllung

Die vertikale Struktur ist grundsätzlich vorhanden: Transport-Port, Acquirer-Fassade, sichere Staging-/Checkout-Prüfungen, typed failures und direkte deterministische Tests wurden ergänzt. Die Scope-Grenzen wurden eingehalten; es gibt keine neue produktive HTTP-/Git-/Prozess-/Credential- oder Host-Verdrahtung.

Die Abnahmekriterien sind jedoch nicht vollständig erfüllt. Der Fehlervertrag deckt nicht alle realistischen Transportausnahmen mit typed failure und Cleanup ab, Cancellation wird nach einem erfolgreichen Transport nicht nochmals als Abbruchbedingung geprüft, und Transport-Diagnosen werden ungefiltert in das Ergebnis übernommen. Die Reparse-/Ownership-Garantie wird nur teilweise verifiziert: Der Checkout-Pfad wird nicht atomar reserviert, und der Test prüft lediglich Reparse-Attribute als Bitmasken statt eines tatsächlichen Reparse-/Symlink-/Junction-Falls. Zusätzlich bleibt ein neuer exakter DRY-Klon der Dateisystem-Ausnahmeerkennung bestehen.

Die behauptete Pfadzentralisierung ist korrekt, aber eng begrenzt: `ExternalSourcePathRules.IsDriveQualified` wird von Mapping-Validator, Snapshot-Modell und Acquirer gemeinsam verwendet. Die weitergehende Lösungspfad-Normalisierung ist nicht zentralisiert; das ist wegen der absichtlich strengeren Traversal-Semantik des Acquirers kein automatisch sicherer Refactoring-Kandidat.

### Rules-Konformität

Die C#-MCP-Prüfungen für den betroffenen Scope meldeten keine Linter-Violations. Nullable-/Sealed-/Architekturvorgaben sowie die Non-Goals (kein Netzwerk, kein Prozessstart, kein Assembly-Load/ALC) sind eingehalten. Der MCP-Safeguard lief mit 9,1/10; der einzige dort sichtbare Befund betraf `DaemonHostCommand` außerhalb des Step-Scopes.

Die DRY-Regel aus dem Step-Plan ist verletzt: `IsFileSystemException` ist in Acquirer und PathGuard exakt dupliziert. Das ist ein sicherer, in-scope konsolidierbarer Befund und kein Anlass, TD-001 bis TD-003 künstlich erneut als Sweep aufzunehmen. Es wurde kein neuer Out-of-Scope-Tech-Debt-Eintrag angelegt.

Die Diagnose-Sicherheitsregel ist logisch nicht vollständig abgesichert: Mapping-Credentials und Exception-Texte werden zwar nicht direkt übernommen bzw. validiert, die vom Transport gelieferten Diagnosen werden aber unverändert weitergereicht.

### Logische Korrektheit

Die happy-path- und die getesteten typed-failure-Pfade sind nachvollziehbar. Die Cleanup-Kapselung löscht grundsätzlich nur eigene Child-Pfade unter dem kontrollierten Staging-Root und behandelt erkannte Reparse-Punkte nicht traversierend. Diese Eigenschaften reichen aber nicht für die behauptete Gesamtgarantie unter allen Fehler- und Race-Szenarien.

Insbesondere kann eine nicht von `IsTransportException` erfasste Transportausnahme aus `ExecuteTransportAsync` herausfallen; dann wird `FailAfterCleanup` nicht erreicht. Ebenso kann ein Transport trotz signalisierter Cancellation ein gültiges Ergebnis liefern, worauf `AcquireAsync` ohne nachgelagerte Token-Prüfung Erfolg zurückgibt. Beide Fälle verletzen die Vertragsanforderungen für typed failure, Ownership/Cleanup und Cancellation.

### Konzept-Treue (Ebene 4)

Das Ergebnis bildet das geplante vertikale Paket und überschreitet die ausdrücklich gesetzten Non-Goals nicht. Die Abweichungen liegen innerhalb der vorgesehenen Akquisitionsgrenze, betreffen aber Muss-Have-Sicherheits- und Ressourcenverträge. Die Schritt-Dokumentation behauptet für Reparse-/Ownership-Schutz mehr, als die direkte Testabdeckung und die nicht reservierte Checkout-Identität tatsächlich belegen.

### Build-/Test-Status

```text
dotnet build → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress → grün (1957 Tests, 0 Fehler, 0 übersprungen)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → grün (360 Tests, 0 Fehler, 0 übersprungen)
dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~ExternalSourceRepositoryAcquirerTests → grün (20 Tests, 0 Fehler, 0 übersprungen)
```

Stress-Tests wurden nicht ausgeführt.

## Findings (nur bei `issues`)

1. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs:102-129` — [CRITICAL] [Logik] `ExecuteTransportAsync` fängt nur Datei-/Argument-/Berechtigungsfehler und `InvalidOperationException`. Typische Transportfehler wie `HttpRequestException`, `TimeoutException` oder andere nicht erwartete Provider-Ausnahmen verlassen den Acquirer, ohne typed `FailureKind`, sichere Diagnose oder `FailAfterCleanup`; ein partieller Checkout kann liegen bleiben. **Fix:** Den zulässigen Transport-Ausnahmevertrag vollständig festlegen und alle nicht-Cancellation-Transportfehler in einen typed Failure (`NetworkUnavailable`/`Timeout`/`InvalidResponse`) mit generischer, geheimnisfreier Diagnose überführen; jeder solche Pfad muss den eigenen Checkout bereinigen. Ergänze dafür einen deterministischen Double-Test mit einer bisher ungefangenen Transportausnahme und verifiziere Ergebnis plus Cleanup.

2. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs:56-99` — [MAJOR] [Logik] Nach dem `await` des Transports erfolgt keine erneute Cancellation-Prüfung. Liefert ein Transport-Doppel nach gesetzter Cancellation noch einen gültigen Checkout, gibt die Fassade Erfolg zurück und übergibt Ownership an den Handle. **Fix:** Nach dem Transportergebnis innerhalb des cleanup-besitzenden Pfads `cancellationToken.ThrowIfCancellationRequested()` ausführen, den Child-Checkout dabei sicher bereinigen und die Cancellation unverändert weiterreichen; ergänze einen Test „cancelled transport returns success“.

3. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs:71-76,97-99` und `src/AiNetLinter/Mcp/Assemblies/IGiteaRepositoryTransport.cs:20-60` — [CRITICAL] [Logik] `ExternalSourceRepositoryTransportResult` akzeptiert beliebige Diagnosen, und der Acquirer reicht `transportResult.Diagnostics` unverändert an das öffentliche Acquisition-Ergebnis weiter. Die vorhandene Prüfung gegen Userinfo in der Mapping-URL schützt diese Transportdiagnose nicht; ein Transport kann daher Credential-URLs oder Secret-Fragmente zurückgeben. **Fix:** Die Diagnosegrenze am Transport-/Acquirer-Vertrag validieren oder redigieren (insbesondere URL-Userinfo, Secret-/Token-Muster und exception-nahe Details), nur stabile Codes und sichere Locations übernehmen und einen direkten Test mit geheimhaltiger Transportdiagnose hinzufügen.

4. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs:217-230,257-282`, `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquisitionModels.cs:38-43` und `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryAcquirerTests.cs:229-234` — [MAJOR] [Plan] [Logik] Der Checkout wird vor der Übergabe an den Transport nur per „exists“-Prüfung ausgewählt, nicht als eigener Child reserviert; nach dem Transport wird der Parent-/Ownership-Pfad nicht identitätsfest gegen konkurrierende Reparse-/Replacement-Szenarien verifiziert. Der einzige Reparse-Test prüft Flag-Erkennung, nicht einen echten Symlink/Junction/Reparse-Ausbruch oder Cleanup eines solchen Baums. `Dispose` ignoriert zudem das Cleanup-Ergebnis. Damit ist die Sicherheits- und Ownership-Aussage aus Kriterien 3 und 6 nicht direkt belegt. **Fix:** Einen sicheren Reservierungs-/Ownership-Schritt und eine vollständige Nachprüfung der Parent-Kette bzw. der Checkout-Identität festlegen und implementieren; Cleanup-Fehler müssen im Vertrag bewusst behandelt werden. Ergänze auf dem Zielbetriebssystem direkte Reparse-/fremder-Arbeitsbaum-Tests sowie einen Test für den tatsächlichen Cleanup-Fehlerpfad. Falls Race-Freiheit ausdrücklich außerhalb dieses Steps bleiben soll, muss die Dokumentation die Garantie entsprechend enger formulieren.

5. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs:406-410` und `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryPathGuard.cs:150-154` — [MAJOR] [Rules] [Plan] `IsFileSystemException` ist ein neuer exakter Methodenklon in zwei direkt gekoppelten Produktionsdateien. Der gründlichere Drift-Audit mit `find_duplicates` bei `minTokens=1` findet genau diesen Cluster; der reguläre Lint-Schwellenwert hat ihn wegen der Kürze nicht erfasst. **Fix:** Die Ausnahmeklassifikation in genau einem gemeinsamen internen Helper zentralisieren und beide Aufrufer darauf umstellen; keinen breiteren, themenfremden Refactoring-Sweep starten.

## Zusatz-Audits

- **MCP:** `get_file_tree`, `find_symbol`, `get_feature_context`, `get_symbol_body`, `find_references`, `get_impact`, `dependency_graph`, `get_violations`, `safeguard` und `find_dead_code` wurden mit absolutem `projectRoot` verwendet. Der symbolische Impact-/Git-Ref-Aufruf meldete für den Commit-Diff eine leere bzw. nicht geladene Git-Basis; der Commit wurde deshalb zusätzlich direkt über Git geprüft. Die gezielten Symbol-, Referenz- und Dependency-Ergebnisse waren vollständig genug für den Step-Scope.
- **DRY:** Der solution-weite `find_duplicates`-Scan bei `minTokens=20` enthielt überwiegend bestehende, fachfremde Cluster. Der gezielte Produktionsscan bei `minTokens=1` ergab genau den oben genannten exakten 10-Token-Klon. Strukturelle Kandidaten zwischen den beiden Result-Konstruktoren sind legitime getrennte Verträge; kein weiterer sicherer Konsolidierungsfund.
- **MagicValues:** Im Produktionsscope wurden nur der bereits als Konstante geführte Präfix `checkout-` und eine lokalisierbare Fehlermeldung angezeigt; kein neuer sicherer Produktionsfund. Die Testtreffer (15 eindeutige Werte/20 Vorkommen) sind deterministische Fixture-, Canary- und Contract-Literale.
- **DeadCode:** Im neuen Acquirer-/PathGuard-Produktionsscope wurden keine toten Symbole gefunden. Der breitere Scan zeigte nur `ExternalSourceConfigurationLoader.Load` als bestehenden Low-Confidence-Kandidaten außerhalb des Steps; daraus wurde kein Tech-Debt-Eintrag abgeleitet.
