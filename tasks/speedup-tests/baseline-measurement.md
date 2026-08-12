---
task: speedup-tests
type: baseline-measurement
created_at: 2026-08-12
created_by: coder (step-002)
---

# Baseline-Messung vor dem ersten Refactoring

Nach `konzept.md` Leitplanke 10: gemessen werden die heutigen, im Alltag verbindlichen Profile
(`AGENTS.md` §2) auf derselben Maschine, Build getrennt von Testzeit, Median über mindestens drei
Läufe je Profil. Diese Zahlen sind der Vorher-Wert für den späteren relativen Vorher-/Nachher-Nachweis
(spätestens EPIC-7).

## Maschinen-/Umgebungskontext

- .NET SDK: `10.0.203`
- Logische CPU-Kerne: 16
- OS: Windows 11 Enterprise 10.0.22631
- Solution zum Messzeitpunkt: 5 Projekte (`AiNetLinter`, `AiNetLinter.Tests`,
  `AiNetLinter.FastTests`, `AiNetLinter.IntegrationTests`, `AiNetLinter.TestKit`) — die drei neuen
  Zielprojekte enthalten nach step-001/step-002 nur Proof- und Guard-Tests (6 Unit + 3 Component in
  FastTests, 5 Integration in IntegrationTests), noch keine migrierte Fachkohorte. Die Baseline misst
  also bewusst den heutigen, überwiegend im Legacy-Projekt konzentrierten Zustand.
- Kein anderer nennenswerter Prozess absichtlich parallel gestartet; die Messläufe liefen als
  kompakter Block direkt nacheinander (kein Kalibrierungslauf nötig, da nur ein Messblock — die
  A/B/A-Problematik aus Leitplanke 10 betrifft erst den späteren Vergleich mit der Endmessung).

## Methodik

1. `dotnet build AiNetLinter.slnx` einmal separat zeitgestoppt (nach `dotnet clean`, damit die
   Messung einen echten Kompilierlauf statt eines No-Op-Inkrementalbuilds zeigt).
2. `dotnet test --filter Category=Unit --no-build` dreimal, je eigener
   `--logger "trx;LogFileName=baseline-unit-runN.trx"`.
3. `dotnet test --filter Category!=Stress --no-build` dreimal, je eigener
   `--logger "trx;LogFileName=baseline-nostress-runN.trx"` (heutiges Abschlussgate, enthält
   Unit+Integration aus allen drei aktiven Testprojekten).
4. Beide Testkommandos laufen an der Solution-Wurzel und starten dabei nacheinander einen
   Testhost-Prozess je Projekt mit `Category`-Treffern (`AiNetLinter.FastTests`,
   `AiNetLinter.IntegrationTests`, `AiNetLinter.Tests`) — Wall Clock ist die Summe der von `dotnet
   test` selbst berichteten Pro-Projekt-Dauer, da alle drei sequentiell laufen.
5. Stress-Kategorie bewusst nicht gemessen (läuft laut `AGENTS.md` §2 nie automatisch, wird erst bei
   der Abschlussverifikation erfasst).

**Bekannte Einschränkung der aggregierten Testzeit:** `--logger LogFileName` gilt pro `dotnet
test`-Aufruf, nicht pro Projekt. Da ein Profil-Lauf mehrere Testhost-Prozesse (einen je Projekt)
nacheinander im selben Aufruf startet, überschreibt der letzte Prozess (`AiNetLinter.Tests`) die TRX
der vorherigen — die aggregierte Testzeit unten stammt deshalb ausschließlich aus der
`AiNetLinter.Tests`-TRX (dominiert das Profil ohnehin: 1510 von 1527 bzw. 1347 von 1353 Tests). Die
Pro-Projekt-Wall-Clock von `AiNetLinter.FastTests`/`AiNetLinter.IntegrationTests` ist trotzdem aus der
Konsolenausgabe jedes Laufs vollständig erfasst (siehe Rohdaten) und fließt in die Gesamt-Wall-Clock
ein. Diese Kollision ist derselbe Mechanismus, den Leitplanke 10 für den globalen
`.runsettings`-Default beschreibt — deren vollständige Behebung (`AGENTS.md`/Diagnoseregel auf
pro-Profil-`LogFileName` umstellen) ist laut Konzept ein separates, späteres Epic, nicht Teil dieses
Steps.

## Rohdaten

### Build (einmalig, nach `dotnet clean`)

| Lauf | Wall Clock |
|---|---|
| 1 | 20,47 s (von `dotnet build` selbst berichtet: 20,47 s / extern gemessen: 21,71 s) |

### Profil `Category=Unit`

| Lauf | FastTests | Tests (Legacy) | Wall Clock gesamt | Aggregierte Testzeit (Tests-TRX) | Tests gesamt | Status |
|---|---|---|---|---|---|---|
| 1 | 0,21 s (6 Tests) | 81 s (1347 Tests) | 81,21 s | 206,44 s | 1353 | grün |
| 2 | 0,21 s (6 Tests) | 74 s (1347 Tests) | 74,21 s | 195,81 s | 1353 | grün |
| 3 | 0,21 s (6 Tests) | 74 s (1347 Tests) | 74,21 s | 194,68 s | 1353 | grün |
| **Median** | | | **74,21 s** | **195,81 s** | 1353 | |

(`AiNetLinter.IntegrationTests` enthält 0 Tests mit `Category=Unit` — erwartet, das Projekt ist laut
Konzept ausschließlich für Integration/Dogfood/Performance/Stress vorgesehen.)

### Profil `Category!=Stress` (heutiges Abschlussgate)

| Lauf | FastTests | IntegrationTests | Tests (Legacy) | Wall Clock gesamt | Aggregierte Testzeit (Tests-TRX) | Tests gesamt | Status |
|---|---|---|---|---|---|---|---|
| 1 | 1 s (9 Tests) | 27 s (8 Tests) | 196 s (1510 Tests) | 224 s | 1247,12 s | 1527 | grün |
| 2 | 0,69 s (9 Tests) | 27 s (8 Tests) | 194 s (1508/1510, 2 Fehler) | 221,69 s | 1297,95 s | 1527 | **rot (Ausreißer, siehe unten)** |
| 3 | 1 s (9 Tests) | 33 s (8 Tests) | 192 s (1510 Tests) | 226 s | 1138,27 s | 1527 | grün |
| **Median** | | | | **224 s** | **1247,12 s** | 1527 | |

## Ausreißer / Fremdlast-Hinweis

Lauf 2 des `Category!=Stress`-Profils zeigt 2 Fehlschläge in
`AiNetLinter.Tests.Mcp.McpServerCommandJsonRpcFramingTests` (`HandshakeOnly_AllStdoutLinesAreValid­JsonRpcFrames`,
`Initialize_ResponseInstructionsField_ContainsServerInstructionsDoctrine`). Isoliert nachgestellt
(`dotnet test --filter FullyQualifiedName~McpServerCommandJsonRpcFramingTests`) laufen beide Tests
sofort grün — die Fehlschläge treten ausschließlich unter der Prozess-/Subprozess-Last des vollen
Parallel-Laufs auf (stdout-Framing-Test gegen einen echten `AiNetLinter.exe`-MCP-Subprozess, siehe
`AGENTS.md` §2 zur Empfindlichkeit paralleler Subprozessstarts). Das ist eine bereits vor step-002
bestehende Flakiness der Legacy-Suite unter Volllast, keine durch step-002 eingeführte Regression;
gemäß Leitplanke 10 wird der Ausreißer hier dokumentiert statt still entfernt. Die Wall-Clock-/
Aggregatzahlen aus Lauf 2 bleiben in der Median-Berechnung, weil Testfehler die gemessene Laufzeit
nicht verfälschen (die fehlgeschlagenen Tests liefen vollständig durch, nur die Assertion schlug fehl).

Für das separate DoD-Kriterium „`dotnet test --filter Category!=Stress` grün" (siehe
`step-002/step-plan.md`) zählt ein sauberer Lauf ohne Fremdlast-Fehlschlag — Lauf 1 und Lauf 3 oben
erfüllen das.

## Vergleichsgrößen für den späteren Vorher-/Nachher-Nachweis

- **Unit-Profil:** Median-Wall-Clock 74,21 s, aggregierte Testzeit 195,81 s, 1353 Tests.
- **Abschlussgate-Profil (`Category!=Stress`):** Median-Wall-Clock 224 s, aggregierte Testzeit
  1247,12 s, 1527 Tests.
- **Build:** 20,47 s (einmalig, nach `dotnet clean`).
- **Dogfood** wird erst relevant, sobald eine eigene Dogfood-Kategorie migrierter Tests existiert
  (aktuell 0 solche Tests in `AiNetLinter.IntegrationTests`) — laut Leitplanke 10 dann getrennt
  auszuweisen, hier noch nicht anwendbar.

Diese Zahlen sind der Referenzpunkt für den Abschlussbericht (EPIC-7); Kennzahlen mit Testanzahl im
Nenner werden bewusst nicht gebildet, da die Testanzahl während der Migration keine Invariante ist.
