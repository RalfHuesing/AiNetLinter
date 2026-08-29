---
status: done
type: step-review
task: decompiled-assembly-analysis
step: 017
epic: EPIC-04
step_type: single
reviewed_by: kritiker
reviewed_by_model: gpt-5 (Codex)
reviewed_by_model_knowledge_cutoff: nicht angegeben
reviewed_at: 2026-08-29T02:58:39+02:00
verdict: blocked
tech_debt_ids: []
---

# Review Step 017: Cancellation-Cleanup beobachten und Reparse-Capability-Gate

## Verdict

- [ ] **approved** — alle vier Prüfebenen ok
- [ ] **issues** — Korrektur-Step erforderlich
- [x] **blocked** — privilegierter Reparse-Nachweis fehlt

Die zwei Vertragsänderungen sind statisch und durch die erreichbare
Regression nachvollziehbar korrekt. Das Capability-Gate führt einen echten
Directory-Symlink-Preflight aus und überspringt ausschließlich den unter
Windows erkannten Rechtefehler `ERROR_PRIVILEGE_NOT_HELD` (1314). Der
aktuelle Host besitzt weder sichtbar `SeCreateSymbolicLinkPrivilege` noch
einen auslesbaren Developer-Mode-Nachweis; dadurch wurde der unveränderte
Reparse-/Sentinel-Test in beiden Läufen nicht ausgeführt. Der Skip ist kein
Sicherheitsnachweis, daher ist eine Genehmigung nach Step-Plan und Konzept
nicht zulässig.

## Geprüft

- [ ] Plan-Erfüllung: alle Code-/Regressionsteile sind umgesetzt; der
  geforderte privilegierte Lauf ohne Skip fehlt.
- [x] Rules-Konformität: die im Plan referenzierten Regeln sind im
  Produktions- und Testscope eingehalten.
- [ ] Logische Korrektheit: Cancellation-, Cleanup- und Gate-Logik sind
  erreichbar geprüft; der privilegierte echte Reparse-Pfad bleibt empirisch
  unbestätigt.
- [x] Konzept-Treue: kein Scope-Drift und keine Umsetzung eines Non-Goals;
  der Sicherheitsvertrag wird wegen des fehlenden Nachweises nicht als
  erfüllt behauptet.
- [x] Build: selbst nachgeprüft, grün.
- [ ] Tests: alle erreichbaren Tests sind grün, aber der privilegierte
  Reparse-Nachweis wurde übersprungen.

## Befund

### Plan-Erfüllung

Die Cleanup-Beobachtung ist in
`src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs:94-103`
umgesetzt: `TryCleanup()` wird im Cancellation-Catch einmal ausgewertet und
bei `false` mit dem stabilen Code `RepositoryCleanupFailed` geloggt; danach
folgt das unveränderte `throw;`. Die direkte Regression in
`src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCancellationTests.cs:18-68`
prüft dieselbe Exception-Instanz, ihren CancellationToken, den verbliebenen
Checkout ohne Ownership-Marker, genau ein lokales Warning-Event und die
Redaktion untrusted Werte.

Der echte Reparse-Test in
`src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryAcquirerTests.cs:233-260`
ist bis auf den vorgeschalteten
`WindowsReparseCapabilityGate.Require()`-Aufruf bei Zeile 236 unverändert.
`Directory.CreateSymbolicLink`, die produktive Reparse-Prüfung, der externe
Sentinel und die Assertions wurden nicht ersetzt oder abgeschwächt. Die
fachliche Plan-Erfüllung bleibt dennoch offen, weil der notwendige
privilegierte Lauf ohne Skip nicht vorliegt.

### Rules-Konformität

Der Cancellation-Catch verletzt `EnforceNoSilentCatch` nicht: Der
Cleanup-Fehler wird sichtbar geloggt und die ursprüngliche Cancellation wird
weitergereicht. Die lokale Logger-Injektion ist optional intern und lässt die
bestehenden Produktionsaufrufer kompatibel; der Test setzt weder
`Log.Logger` noch eine globale Test-Collection um. Alle neuen Tests verwenden
`TestTempDirectory`; es wurde kein OS-Temp-Pfad angelegt.

Der MCP-Workflow wurde mit absolutem
`projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter` eingehalten. Die
Violation-Abfragen melden 0 Treffer im Produktionsscope
(`src/AiNetLinter/Mcp/Assemblies`, 26 Dateien) und 0 Treffer im geänderten
Testscope (7 Dateien).

### Logische Korrektheit

`ExecuteTransportAsync` reicht `OperationCanceledException` mit `throw;`
unverändert weiter; der äußere Catch versucht Cleanup genau einmal und
maskiert die Cancellation nicht durch ein Provider-Failure-Result. Das
Cleanup bleibt bei verlorener Ownership ablehnend, sodass der Test-Checkout
nicht gelöscht wird. Der instanzlokale Serilog-Sink beweist, dass nur ein
Warning mit dem stabilen Code und ohne Exception, Checkout-Pfad,
Ownership-Token, URL oder Exception-Text entsteht.

Der Capability-Preflight in
`src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryTestSupport.cs:38-76`
legt ein isoliertes Testtemp-Verzeichnis an, ruft den echten
`Directory.CreateSymbolicLink` auf, prüft `FileAttributes.ReparsePoint` und
entfernt den Link im `finally`. Der Skip-Filter ist auf Windows und das
HResult-Low-Word `1314` begrenzt; andere Fehler und andere Exception-Codes
werden nicht übersprungen. Der lokale Preflight meldet jedoch genau
`ERROR_PRIVILEGE_NOT_HELD (1314)`.

### Konzept-Treue (Ebene 4)

Die Änderung bleibt innerhalb der in Konzept Phase 4 festgelegten
Akquisitions-/Cancellation- und Testharness-Grenze. Netzwerk, Git,
Credentials, Refresh, Cache, Snapshot, Workspace, Assembly-Loading und
Reflection wurden nicht erweitert; ebenso wurde keine Fake-Reparse-
Assertion und kein Systemeingriff eingeführt. Der Konzept-Sicherheitsvertrag
verlangt zusätzlich einen privilegierten Lauf ohne Skip. Dieser Nachweis ist
auf dem aktuellen Host nicht erbracht und wird nicht aus dem technisch
grünen Skip-Lauf abgeleitet.

### Build-/Test-Status

```text
dotnet build
→ grün (0 Warnungen, 0 Fehler)

dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~ExternalSourceRepositoryCancellationTests" --logger "trx;LogFileName=Step017-Cancellation-review.trx"
→ grün (1 bestanden, 0 übersprungen, 0 Fehler; 1 gesamt)

dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~ExternalSourceRepositoryAcquirerTests" --logger "trx;LogFileName=Step017-Acquirer-review.trx"
→ grün (28 bestanden, 1 übersprungen, 0 Fehler; 29 gesamt)

dotnet test src/AiNetLinter.FastTests --filter "Category!=Stress" --logger "trx;LogFileName=Step017-FastTests-review.trx"
→ grün (1966 bestanden, 1 übersprungen, 0 Fehler; 1967 gesamt)

dotnet test src/AiNetLinter.IntegrationTests --filter "Category!=Stress" --logger "trx;LogFileName=Step017-IntegrationTests-review.trx"
→ grün (360 bestanden, 0 übersprungen, 0 Fehler; 360 gesamt)
```

Der einzige Skip in Acquirer- und FastTests-Lauf ist
`ExternalSourceRepositoryAcquirerTests.AcquireAsync_ActualReparseEntry_IsRejectedAndExternalSentinelRemains`.
Die TRX-Dateien enthalten als Skip-Grund ausschließlich
`ERROR_PRIVILEGE_NOT_HELD (1314)` aus `Directory.CreateSymbolicLink` und
die ausdrückliche Aussage, dass kein Sicherheitsnachweis erbracht wurde.
Stress-Tests wurden nicht ausgeführt.

## Frage an Nutzer

Bitte lasse denselben unveränderten fokussierten Acquirer-Test unter einem
Windows-Host mit aktiviertem Developer Mode oder sichtbarem
`SeCreateSymbolicLinkPrivilege` laufen. Der Test muss dann ohne Skip
ausgeführt werden und den externen Sentinel unverändert erhalten. Danach ist
das vollständige FastTests-Nicht-Stress-Gate erneut auszuführen:

```text
dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~ExternalSourceRepositoryAcquirerTests"
dotnet test src/AiNetLinter.FastTests --filter "Category!=Stress"
```

Keine Privilegien-, Registry-, ACL- oder Developer-Mode-Änderung und keine
Assertion-Abschwächung im Rahmen dieses Steps vornehmen. Anschließend ist ein
neuer Kritikerlauf erforderlich; erst ein privilegierter Pass ohne Skip
erlaubt die Entscheidung zwischen `approved` und einem weiteren Befund.

## Sonstige Beobachtungen / MINOR / NITPICK

Die Testdatei enthält eine im Commit entfernte Leerzeile ohne semantische
Auswirkung; daraus entsteht kein Finding.

## MCP-/DRY-/MagicValues-/DeadCode-Ergebnis

- `find_symbol`, `get_feature_context`, `get_symbol_body`,
  `find_references` und `get_impact` bestätigen die Acquirer-/Ownership-
  Symbole, den neuen Cancellation-Test und den einzigen Gate-Aufrufer. Für
  den Acquirer-Cancellation-Helper wurden 15 relevante Aufrufstellen ohne
  Trunkierung angezeigt; der Gate-Aufruf liegt ausschließlich im echten
  Reparse-Test.
- `find_duplicates` mit `minTokens=1` und `similarityThreshold=exact` findet
  0 Cluster bei 214 Produktionsmethoden und 0 Cluster bei 65 Methoden im
  betroffenen Testscope.
- `find_magic_values` meldet 69 bestehende Kandidaten über 26 Produktions-
  dateien sowie 103 Treffer in 56 eindeutigen Einträgen über 7 Testdateien.
  Die neuen Testwerte sind isolierte Fixture-/Capabilitydaten; der
  `1314`-Wert ist als benannte Konstante geführt. Kein neuer sicherheits-
  relevanter Leak oder unmittelbar zu behebender In-Scope-Magic-Value wurde
  festgestellt.
- `find_dead_code` findet 0 High-Confidence-Kandidaten bei 76 Produktions-
  symbolen und 0 bei 13 Testsymbolen.
- `safeguard` liefert 8,83/10 (`PASS`); der einzige angezeigte Warning-
  Befund betrifft den bestehenden `DaemonHostCommand`-Footprint außerhalb
  des Step-Scopes.

Es entsteht kein neuer oder geänderter Tech-Debt-Fund. `tech-debt.md` bleibt
unverändert; `TD-001` bis `TD-003` werden weder ausgeweitet noch neu
bewertet.
