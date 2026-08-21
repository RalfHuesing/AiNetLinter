---
status: done
type: step-result
task: 04_repositoryweite-hybridsuche-und-kontextbudget
step: 005
epic: EPIC-06
step_type: single
coded_by: coder
coded_by_model: GPT-5 (Codex)
coded_by_model_knowledge_cutoff: nicht angegeben
coded_at: 2026-08-21
code_commit_hash: 4899cf58
status_after: done
review_commit_hash: dc11d39b
review_verdict: approved
blocker_category: n/a
---

# Result Step 005: Wirksamkeits-, Performance- und Abschlussvalidierung

## Zusammenfassung

Die Evaluation ergänzt einen isolierten FastTests-Scanner-Harness und einen direkten IntegrationTests-Tool-Harness. Ein unbudgetierter Fixture-Lauf dient als Oracle; budgetierte Läufe vergleichen sichtbare Treffer-/Dateizahlen und erklären Verluste über Completeness-, Skip-, Timeout- oder Cancellation-Metadaten. Die Toolmessung erfasst Legacy-Text, Structured-Payload und deren kombinierte UTF-8-Bytewerte getrennt sowie einen zustandslosen, definierten Folgeaufruf.

## Geänderte Dateien

- `src/AiNetLinter.FastTests/Mcp/Tools/Analysis/SearchPatternScannerEvaluationTests.cs` (neu) — isolierte `SymbolGraphMini`-Kopie mit Overlay-Dateien, Oracle-/Budget-/Semantic-/Timeout-/Cancellation-Fällen und sieben Messiterationen.
- `src/AiNetLinter.IntegrationTests/Mcp/Tools/SearchPatternEvaluationTests.cs` (neu) — `LoadedFixture`-/MCP-Evaluation für gemischte Dateitypen, Wire-Bytes, Wiederholungsstabilität und einen definierten Folgeaufruf.

## Messbedingungen

- Windows/PowerShell, Debug-Build, .NET 10.0.7, dieselbe lokale Arbeitskopie am 2026-08-21.
- FastTests verwenden `IsolatedFixtureLease`/`TestTempDirectory` und `RoslynTestSolution`; IntegrationTests verwenden `SymbolGraphCatalogFixture`/`LoadedFixture` beziehungsweise eine isolierte `SymbolGraphMiniFixtureWorkspace`-Kopie.
- Zeitmessungen: 1 Warmup + 7 getimte Aufrufe je markiertem Toolfall; `p95` ist bei sieben Werten der größte sortierte Wert. Die Werte sind keine allgemeine Performanceaussage.
- `combinedToolUtf8Bytes` ist die Summe aus Legacy-Text und Structured-JSON-Rohtext ohne modell- oder protokollabhängige Tokenumrechnung.
- Der direkte Tool-Oracle nutzt `maxResults=50`, weil der bestehende Toolvertrag Werte kleiner als 1 auf 1 normalisiert; die Scanner-Oracles nutzen intern `maxResults=0`, `maxFiles=0`, `maxResponseBytes=0`.

## Feste Messmatrix

| caseId | Fixture-/Snapshot-Quelle | pattern / flags | Scope / Limits | Oracle Dateien/Zeilen | sichtbar Dateien/Zeilen | Verlust / Erklärung | Skip B/U | Cancel/Timeout | Legacy / Structured / Combined UTF-8 | Warmup / Iterationen; min / median / p95 ms | followUpCalls | rgStatus |
|---|---|---|---|---:|---:|---|---|---|---:|---|---:|---|
| plain-oracle | SymbolGraphMini, resident snapshot | `search-anchor`, plain, enrich=false | `.`; unbudgetiert | 2/2 | 2/2 | 0 | 0/0 | nein/nein | 168 / 1232 / 1400 | 1/7; 26.014 / 28.926 / 57.833 | 0 | not-run |
| regex-oracle | SymbolGraphMini, resident snapshot | `search-anchor`, regex, enrich=false | `.`; unbudgetiert | 2/2 | 2/2 | 0 | 0/0 | nein/nein | 168 / 1232 / 1400 | 1/7; 32.747 / 39.177 / 47.307 | 0 | not-run |
| mehrfachbereiche-kontext | SymbolGraphMini, resident snapshot | `search-anchor`, plain; zwei Bereiche je Zeile; `contextLines=1` | `.`; unbudgetiert | 2/2 | 2/2 | 0; Kontext vor/nach dem Markdown-Treffer sichtbar | 0/0 | nein/nein | 168 / 1321 / 1489 | 0/0; n/a | 0 | not-run |
| enrich-csharp | SymbolGraphMini, resident snapshot | `Greeter`, plain, enrich=true | `.`; unbudgetiert | 2/4 | 2/4 | 0; Declaration/Reference resolved | 0/0 | nein/nein | 583 / 3015 / 3598 | 0/0; n/a | 0 | not-run |
| max-results | SymbolGraphMini, resident snapshot | `search-anchor`, plain | `.`; maxResults=1 | 2/2 | 1/1 | 1 Zeile/1 Datei; `maxResults` | 0/0 | nein/nein | 146 / 952 / 1098 | 0/0; n/a | 0 | not-run |
| max-files | SymbolGraphMini, resident snapshot | `search-anchor`, plain | `.`; maxFiles=1 | 2/2 | 1/1 | 1 Zeile/1 Datei; `maxFiles` | 0/0 | nein/nein | 155 / 950 / 1105 | 0/0; n/a | 0 | not-run |
| max-response-bytes | SymbolGraphMini, resident snapshot | `search-anchor`, plain | `.`; maxResponseBytes=200 | 2/2 | 0/0 | 2 Zeilen/2 Dateien; `maxResponseBytes` | 0/0 | nein/nein | 81 / 720 / 801 | 1/7; 22.361 / 25.443 / 35.792 | 0 | not-run |
| problem-overlays | isolierte Kopie + Overlay | `problem-anchor`, plain | `.`; unbudgetiert | 1/1 | 1/1 | 0; generated/obj/minified ausgeschlossen | 1/1 | nein/nein | n/a / n/a / n/a | 0/0; n/a | 0 | not-run |
| regex-timeout | isolierte Kopie + `large-search.txt` | `^(a+)+$`, regex | `.`; unbudgetiert | nicht entscheidbar | 0/0 | Abbruch durch `regexTimeout`, kein Trefferverlust als vollständig gewertet | 0/0 | nein/ja | n/a / n/a / n/a | 0/0; n/a | 0 | not-run |
| pre-cancellation | SymbolGraphMini, resident snapshot | `search-anchor`, plain; Token vor Scan abgebrochen | `.`; unbudgetiert | 2/2 | 0/0 | 2 Zeilen/2 Dateien; `cancellation` | 0/0 | ja/nein | n/a / n/a / n/a | 0/0; n/a | 0 | not-run |
| post-cancellation | isolierter Roslyn-Snapshot | `Greeter`, enrich=true; Abbruch an Enrichment-Grenze | `.`; lexical payload erhalten | 2/4 | 2/4 | 0; `cancellation`, lexical payload unverändert, keine Semantic-Felder | 0/0 | ja/nein | n/a / n/a / n/a | 0/0; n/a | 0 | not-run |
| follow-up-proxy | SymbolGraphMini, resident snapshot | `search-anchor`, plain | erster Aufruf maxFiles=1; zweiter Scope + `**/*.json` | 2/2 | 1/1 + Ziel 1/1 | 1 definierter Folgeaufruf wegen `maxFiles` | 0/0 | nein/nein | n/a / n/a / n/a | 0/0; n/a | 1 | not-run |
| mixed-filetypes | SymbolGraphMini, resident snapshot | `userService`, plain, enrich=false | `.`; unbudgetiert | 3/3 | 3/3 | 0; JS/Razor/XAML | 0/0 | nein/nein | 232 / 1398 / 1630 | 0/0; n/a | 0 | not-run |

## Entscheidungen

| Aussage | Entscheidung | Befund |
|---|---|---|
| Oracle-Reihenfolge, MatchRanges, Plain-/Regex-Parität und gemischte Dateitypen | bestätigt | Alle definierten sichtbaren Mengen und Forward-Slash-Pfade stimmen mit dem Fixture-Orakel überein; die Legacy-Antwort und das Structured-Objekt bleiben vorhanden. |
| Sichtbarer Treffer-/Dateiverlust ist erklärbar | bestätigt | Jeder absichtliche Verlust trägt `maxResults`, `maxFiles`, `maxResponseBytes` oder `cancellation`; Binary/invalid UTF-8 erscheinen als getrennte Skip-Zähler, generated/obj/minified werden ausgeschlossen. |
| C#-Enrichment sowie Pre-/Post-Cancellation | bestätigt | Resolved-Semantik bleibt opt-in; Pre-Cancellation ist unvollständig; Post-Cancellation gibt den bereits erzeugten lexicalen Payload ohne zweiten Scannerlauf zurück. |
| `maxResponseBytes` als harte Grenze der finalen Structured-JSON-Antwort | nicht bestätigt | Bei Budget 200 werden Treffer bis auf 0 entfernt und `maxResponseBytes` ausgewiesen, aber der finale Structured-Rohtext misst 720 Bytes; die 200-Byte-Grenze ist damit nicht als End-to-End-Wire-Grenze belegt. Keine Produktionsänderung in diesem reinen Evaluation-Step. |
| Allgemeine Laufzeitüberlegenheit oder Tokenersparnis | nicht entscheidbar | Es gibt nur einen lokalen Messlauf mit je sieben Wiederholungen und sichtbarer Streuung; es wurde keine Tokenzahl berechnet und keine allgemeine Aussage abgeleitet. |
| Öffentliche Dokuänderung | nicht bestätigt / nicht vorgenommen | Die Bedingungen tragen keine auf mindestens drei unabhängigen Wiederholungen gestützte, begrenzte öffentliche Aussage. `README.md` und `Docs/ROADMAP.md` bleiben unverändert. |

## Optionaler `rg`-Status

`rgStatus: not-run`. Es gab keine offene Reichweitenfrage, für die ein diagnostischer Vergleich den Evaluation-Befund sinnvoll erweitert hätte. `rg` wurde weder aus Produktionscode noch als Test-Gate verwendet; Installation/Verfügbarkeit ist für diesen Step nicht vorausgesetzt.

## Abweichungen vom Plan

- Keine Produktionsdatei, keine Toolregistrierung, kein Backend und keine öffentliche Dokumentation wurde geändert.
- Für den Tool-Oracle wurde wegen der bestehenden `maxResults`-Normalisierung 50 statt 0 verwendet; der Scanner-Oraclesemantik mit 0 wurde separat abgedeckt.
- Der optionale `rg`-Vergleich wurde nicht ausgeführt, weil die verwaltete Evaluation keine ungeklärte Reichweiten- oder Laufzeitfrage offenließ.

## Beobachtungen

- Die finale Structured-Payload bleibt beim `maxResponseBytes=200`-Fall mit 720 UTF-8-Bytes größer als das angeforderte Budget, obwohl die Matchliste vollständig entfernt und der Trunkierungsgrund ausgewiesen wird. Das ist ein reproduzierter Befund für einen möglichen späteren Vertrags-/Budget-Step, nicht Teil dieses Evaluation-only-Steps.
- Die neuen Testdateien erzeugen im projektinternen Lintlauf keine Violations; der Drift-Audit fand keinen Exact-Clone und keinen mechanisch sicheren neuen Konsolidierungsbefund. Die 25 Near-Cluster sowie strukturellen Kandidaten liegen außerhalb des aktuellen SearchPattern-Evaluationsumfangs oder sind Testvarianten.

## Bekannte Unschärfen

- Die Zeitwerte gelten nur für die konkrete Windows-/Debug-/Fixture-Kombination und wurden nicht über drei unabhängige Prozessläufe stabilisiert.
- `combinedToolUtf8Bytes` ist eine transparente Summenmetrik, nicht die vollständige MCP-Framegröße und kein Tokenproxy.
- Für Regex-Timeout und Cancellation existiert bewusst kein vollständiges Oracle, weil der Scan vor einer vollständigen Enumeration endet; die Zustandsmetadaten sind der relevante Befund.
- Gitignorierte Testartefakte unter `temp` werden durch die zentrale TestKit-Bereinigung erzeugt/entfernt beziehungsweise bleiben als vorhandener, nicht versionierter Arbeitsbestand außerhalb des Commits; sie sind kein Fixture-Bestandteil. `tasks/mcp-server-weiterentwicklung` wurde nicht gelesen, geändert, wiederhergestellt oder gestaged.
- Der Volltestlauf erzeugte zusätzlich `temp_sdk_check.txt` im Repository-Root als 124-Byte-SDK-Diagnoseprobe; die Datei wurde nach der Prüfung gezielt entfernt und nicht gestaged.

## Tech-Debt

Keine neuen Einträge angelegt. `TD-003-001` bleibt erledigt; der `maxResponseBytes`-Befund ist als Beobachtung für eine mögliche spätere Scope-Entscheidung dokumentiert.

## Commit

- **Code-Commit-Hash:** `4899cf58`
- **Message:** `test: Suche messen [04_repositoryweite-hybridsuche-und-kontextbudget]`
- **Branch:** `main`
- **Push:** nein (lokal)
- **Doku-Commit:** separat nach diesem Result-Update.

## Build-/Test-Output

- `dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~SearchPatternScannerEvaluationTests"` → grün (4 Tests, 0 Fehler).
- `dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~SearchPatternScannerTests"` → grün (15 Tests, 0 Fehler).
- `dotnet test src/AiNetLinter.IntegrationTests --filter "FullyQualifiedName~SearchPatternEvaluationTests"` → grün (3 Tests, 0 Fehler).
- `dotnet test src/AiNetLinter.IntegrationTests --filter "FullyQualifiedName~SearchPatternToolTests"` → grün (18 Tests, 0 Fehler).
- `dotnet build` → grün (0 Warnungen, 0 Fehler).
- `dotnet run --project src/AiNetLinter -- --config rules.json --path .` → `OK`.
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` → grün (1566 Tests, 0 Fehler, 0 übersprungen).
- `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` → grün (341 Tests, 0 Fehler, 0 übersprungen).
- Projektinterner Drift-Audit: `find_duplicates` exact 0; near 25 bestehende Cluster; struktureller Scan ohne neuen mechanisch sicheren Fix im Scope.
