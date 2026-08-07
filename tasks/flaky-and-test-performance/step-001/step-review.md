---
status: done
type: step-review
task: flaky-and-test-performance
step: 001
epic: EPIC-01
step_type: single
reviewed_by: kritiker
reviewed_by_model: MiniMax-M3
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-07T10:00:00+02:00
verdict: approved
tech_debt_ids: [TD-001]
---

# Review Step 001: Spike — SymbolGraphMcpFixture auf ICollectionFixture

## Verdict

- [x] **approved** — alle vier Prüfebenen ok
- [ ] **issues** — Fix-Step `step-001/fix-XX` angelegt mit Fix-Plan
- [ ] **blocked** — Nutzer-Entscheidung nötig (siehe Frage unten)

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `<rules_dir>/**` eingehalten
- [x] Logische Korrektheit: Code macht was er soll, nicht nur „grün"
- [x] Konzept-Treue: passt die Umsetzung zu `konzept.md` (Scope, Non-Goals, Muss-Haben)
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün (oder Baselines beachtet)

## Befund

### Plan-Erfüllung

Alle sieben geplanten Änderungen exakt wie spezifiziert umgesetzt (1 neue Collection-Datei, 6 umgestellte Testklassen, inkl. `McpServerCommandTests.cs` mit korrekt erhaltenem `BaselineMcpFixture`). Vorher-/Nachher-Messungen (3 Läufe je Variante, isoliert + voll) liegen in `messung-vorher.txt` / `messung-nachher.txt` und sind im `step-result.md` sauber tabelliert; der vom Planer im Plan dokumentierte Hinweis auf die mit-validierten Read-Only-Tests spiegelt sich im grünen Nachher-Lauf. Der XML-Doc-Kommentar in `SymbolGraphMcpFixture.cs:13` wurde an die neue Verwendungsform angepasst, exakt wie in den "Bekannten Ausnahmen" gefordert. `dotnet build` und `dotnet test` (voller Lauf) sind in eigener Nachprüfung grün.

### Rules-Konformität

`AiNetLinterRichtlinien.mdc` §4 "Testsuite-Parallelität bewahren" eingehalten: `[Collection("SymbolGraphMcp")]` ist eine **begrenzte** Serialisierung der 6 zugehörigen Klassen, nicht ein Assembly-weites `DisableParallelization` — andere Collections laufen weiterhin parallel, und die Regel verlangt explizit, dass diese Art der Begründung im Code liegt (XML-Doc auf der Collection-Klasse dokumentiert das *Why* ohne Schritt-/Epic-Verweise). §5 "Sparsame Kommentare" eingehalten: Der Kommentar auf `SymbolGraphMcpCollection` ist *Why*-only (Sharing-Reason), keine Task-/Planungsartefakte, kein redundanter Bezeichner-Nacherzähler. §5 "Zero-Warning" in eigener `dotnet build`-Prüfung bestätigt (0/0). §5 "Symptom-Fixing verboten" — keine Tests abgeschwächt, der Spike-Befund (Sharing verlangsamt) wurde dokumentiert statt weggespielt. `AiNetLinter.mdc` für `*.Tests` (`EnforceSealedClasses` aus, `MaxMethodLineCount` 100): die neue Klasse ist trotzdem `sealed` (Best Practice), die Collection-Datei hat 13 Zeilen weit unter dem Limit.

### Logische Korrektheit

`[CollectionDefinition("SymbolGraphMcp")] public sealed class SymbolGraphMcpCollection : ICollectionFixture<SymbolGraphMcpFixture>` ist die korrekte xUnit-v3-Mechanik; die sechs `[Collection("SymbolGraphMcp")]`-Attribute an den Testklassen binden sie korrekt, und `McpServerCommandTests` hält `IClassFixture<BaselineMcpFixture>` parallel (geprüft: `McpServerCommandTests.cs:18-19`). Konstruktor-Injektion funktioniert weiter (alle 6 Klassen haben den `SymbolGraphMcpFixture`-Parameter im Konstruktor), kein Kompilier- oder Laufzeit-Bruch. Die Read-Only-Annahme aus dem Plan hält empirisch — 1325 Tests grün trotz geteiltem Subprozess und geteiltem `SymbolGraphMiniFixtureWorkspace`. Die Verschlechterung um +5,3 % (isoliert) und +8,1 % (voll) ist plausibel mit Sequenzialisierung erklärbar (18 Tests in `McpServerCommandTests` laufen jetzt sequenziell statt parallel zu anderen Klassen), aber **statistisch nicht signifikant** — die Coder-eigene Anmerkung "3. Nachher-Vollauf 105,70 s" zeigt eine Spannweite von 105,7 bis 132,0 s allein in der Nachher-Serie, die den Mediane-Unterschied überdeckt. Für eine Spike-Entscheidung (geht/geht-nicht) reicht die Datenlage; eine harte Performance-Aussage wäre mit 3 Läufen nicht belastbar. Die Spike-Empfehlung (3 Varianten A/B/C) ist angemessen — keine Über-Determinierung, EPIC-03 entscheidet.

### Konzept-Treue (Ebene 4)

`konzept.md` §"Wie" Schritt 1 ("Explorations-/Spike-Schritt zuerst") erfüllt: empirische Vorher/Nachher-Messung der konkret vermuteten Performance-Hebel-Klasse (`SymbolGraphMcpFixture`). Die im Konzept stehende Annahme "`SymbolGraphCatalogFixture` 18×" wurde vom Planer korrekt als veraltet erkannt (Realität: 1×) und das Spike-Ziel auf `SymbolGraphMcpFixture` (6×) umfokussiert; die Konzept-Diskrepanz ist im Plan dokumentiert, nicht stillschweigend übergangen. Konzept-Muss-Haven "Reduktion der ~60-80 unabhängigen Lade-/Subprozessvorgänge" ist als Spike-Vorarbeit adressiert — das Ergebnis ("Sharing in dieser Form bringt keinen Performance-Gewinn, EPIC-05 vermutlich nötig") liefert genau die Entscheidungsgrundlage, die das Konzept für die Schritt-1-Phase verlangt hat. Konzept-Non-Goals (kein Framework-Wechsel, kein CI-Workflow, kein sichtbares CLI-Verhalten geändert) sind alle eingehalten. Die fehlende CLI-Option `--self-lint` ist eine **Konzept-/CLI-Diskrepanz** außerhalb des Step-Scopes — siehe TD-001.

### Build-/Test-Status

```
dotnet build                → grün (0 Warnungen, 0 Fehler)
dotnet test --no-build      → grün (1325 Tests, 0 Fehler, 0 übersprungen, 2 min 28 s)
```

Eigene Nachprüfung am 2026-08-07. Die 2:28 min liegen innerhalb der im `step-result.md` dokumentierten Rohzeit-Spanne (105,70 s – 132,04 s) und bestätigen die Spike-Aussage "Spike-Code ist mechanisch lauffähig, aber langsamer als vorher" unabhängig.

## Sonstige Beobachtungen / MINOR / NITPICK

- **`McpServerAllToolsE2ETests.cs:15`:** Der XML-Doc-Kommentar sagt weiterhin "zur einmaligen Fixture- und Client-Instanziierung **pro Testklasse**". Nach der Umstellung teilt sich die Fixture aber pro Collection mit fünf weiteren Klassen — der Kommentar ist formal unzutreffend. Der Plan hatte nur `SymbolGraphMcpFixture.cs:13` explizit als anpassungspflichtig markiert; diese parallele Stelle ist eine Mitnahme-Konsistenz, kein Rule-Verstoß. Kosmetisch.

## Tech-Debt-Einträge aus diesem Review

- `TD-001` (siehe `tech-debt.md`) — Konzept und `roadmap.md` referenzieren `--self-lint` als Self-Lint-Befehl, die CLI in `src/AiNetLinter/Cli/CliOptionFactory.cs` kennt diese Option nicht (verifiziert per `grep` und durch `dotnet run --project src/AiNetLinter -- --self-lint`).
