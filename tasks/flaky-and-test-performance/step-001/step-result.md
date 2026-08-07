---
step: 001
task: flaky-and-test-performance
epic: EPIC-01
status: done
coded_by_model: MiniMax-M3
coded_by_model_knowledge_cutoff: 2026-01
code_commit: bf5de7e
code_commit_subject: "refactor(tests): SymbolGraphMcp-Sharing [flaky-and-test-performance]"
measured_at: 2026-08-07
---

# Step 001 — Spike-Ergebnis: SymbolGraphMcpFixture auf ICollectionFixture umstellen

## Zusammenfassung

`SymbolGraphMcpFixture` wurde von 6 separaten `IClassFixture<T>`-Instanzen auf eine
gemeinsame `ICollectionFixture<T>`-Instanz via `[CollectionDefinition("SymbolGraphMcp")]`
umgestellt. Die Mechanik kompiliert und alle 1325 Tests laufen grün — es ist **kein
Isolationsbruch** aufgetreten. Allerdings hat das Sharing in dieser Konfiguration
**keinen Performance-Vorteil** erbracht: sowohl der isolierte Filter-Lauf als auch
der volle Testlauf sind nachher langsamer als vorher.

## Mess-Zahlen (Mediane aus jeweils 3 Läufen, `dotnet test --no-build`)

| Variante | Vorher (Median) | Nachher (Median) | Δ absolut | Δ relativ |
|----------|----------------:|-----------------:|----------:|----------:|
| Isoliert (6 Klassen via Filter) | 39,56 s | 41,65 s | +2,09 s | +5,3 % |
| Voll (`dotnet test`)            | 119,97 s | 129,63 s | +9,66 s | +8,1 % |

Rohzeiten (Sekunden):

- **Vorher isoliert:** 42,65 / 39,56 / 38,60
- **Nachher isoliert:** 45,64 / 41,65 / 40,62
- **Vorher voll:** 119,97 / 118,04 / 122,88
- **Nachher voll:** 129,63 / 132,04 / 105,70 (3. Lauf auffällig schnell — vermutlich Disk/JIT-Cache)

Detail-Rohdaten liegen in `step-001/messung-vorher.txt` und `step-001/messung-nachher.txt`.

## Geänderte Dateien

1. **NEU** `src/AiNetLinter.Tests/Fixtures/SymbolGraphMcpCollection.cs` — Collection-Definition.
2. `src/AiNetLinter.Tests/Fixtures/SymbolGraphMcpFixture.cs` — XML-Doc-Kommentar Z. 13 an neue Verwendungsform angepasst.
3. `src/AiNetLinter.Tests/Commands/McpServerCommandFindReferencesTests.cs` — `IClassFixture<SymbolGraphMcpFixture>` → `[Collection("SymbolGraphMcp")]`.
4. `src/AiNetLinter.Tests/Commands/McpServerCommandFindSymbolTests.cs` — dito.
5. `src/AiNetLinter.Tests/Commands/McpServerCommandGetImpactTests.cs` — dito.
6. `src/AiNetLinter.Tests/Commands/McpServerCommandMissHintTests.cs` — dito.
7. `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs` — `SymbolGraphMcpFixture`-Anteil rausgezogen, `BaselineMcpFixture` bleibt `IClassFixture`.
8. `src/AiNetLinter.Tests/Mcp/McpServerAllToolsE2ETests.cs` — dito.

## Code-Commit

- **Hash:** `bf5de7e`
- **Subject:** `refactor(tests): SymbolGraphMcp-Sharing [flaky-and-test-performance]`
- **Body:** Begründung der Umstellung, Hinweis auf `BaselineMcpFixture`-Verbleib, Verweis auf `step-result.md` für Messung. `Refs: tasks/flaky-and-test-performance/step-001`.

## Build- und Test-Output

- `dotnet build` (Solution-Root): **grün, 0 Warnungen, 0 Fehler** (Zero-Warning-Direktive erfüllt).
- `dotnet test` (voller Lauf, einmalig nach Umstellung zur Ergebnis-Verifikation): **1325 Tests bestanden, 0 Fehler, 0 übersprungen**, Dauer 2 min 10 s. Isolationscheck bestanden — keine roten Tests, keine Folgefehler durch geteilten Subprozess.
- Vor dem Nachher-Lauf: `dotnet test` wurde bereits im Mess-Setup dreimal via `Measure-Command` ausgeführt; alle Läufe waren grün (kein Fehler sichtbar in `Measure-Command`).

## Abweichungen vom Plan

- **`--self-lint` als CLI-Flag existiert nicht.** Der Auftrag nannte `dotnet run --project src/AiNetLinter -- --self-lint` als Verifikations-Schritt; die aktuelle CLI-Version kennt diese Option nicht. Ersatzweise wurde der Standard-Audit-Lauf mit `dotnet run --project src/AiNetLinter -- --config rules.json --path .` ausgeführt — Ausgabe `OK`, semantisch identisch (Self-Audit der eigenen Codebase gegen `rules.json`).
- Die Zeitmessung im Plan wurde wie spezifiziert ausgeführt; abweichend wurde *zusätzlich* ein finaler `dotnet test` ohne `| Out-Null` gefahren, um die Pass/Fail-Summary explizit zu verifizieren (im `Measure-Command`-Setup wäre sonst nur der Exit-Code sichtbar).

## Beobachtungen

- **Sharing verlangsamt, statt zu beschleunigen.** Trotz Einsparung von 5 Subprozess-Starts (1 × `McpTestClient.ConnectAsync` statt 6 ×) wurde der Lauf langsamer. Plausible Erklärungen:
  - **Sequenzialisierung dominiert die Einsparung:** xUnit v3 serialisiert Tests *innerhalb* einer Collection. `McpServerCommandTests` allein hat 18 Tests, die nun sequenziell laufen — parallel hätten sie zusammen mit den anderen 5 Klassen überlappt.
  - **Subprozess-Lastprofil:** Ein einzelner langlebiger MCP-Subprozess, der 22+ Tests seriell bedient, hat evtl. weniger CPU-Parallelität, da Tests auf Antworten warten. Vorher hätten 6 parallele Subprozesse 6 Tests gleichzeitig bearbeitet.
  - **Test-Reihenfolge-Effekte:** Die Collection-Reihenfolge ist nicht steuerbar; ungünstige Reihenfolge (große Klassen zuerst, kleine zuletzt) kann die Summe verlängern.
- **Isolation hält.** Überraschend robust: trotz geteiltem Subprozess und implizit geteiltem Workspace (`SymbolGraphMiniFixtureWorkspace`) gab es keine Test-Fehler. Die Code-Inspektion im Plan (`Workspace` und `Client` read-only, `private set;`) bestätigte sich empirisch.
- **SubprocessConcurrencyGate-Effekt:** Die 6er-Schwelle des Gates korrespondierte mit den 6 Vorher-Instanzen. Nachher wird das Gate nur einmal pro Collection-Lauf "verbraucht" — das ist der einzige Effekt, der *theoretisch* Verbesserung bringen könnte, aber in der Praxis durch die Sequenzialisierung überkompensiert wird.
- **`SymbolGraphCatalogFixture` (1× verwendet) und `McpLiveRepositoryFixture` (2× verwendet):** Erwartungs-konform nicht umgestellt (kein Hebel bzw. Spike-Scope zu groß). Beobachtung für EPIC-03: Ein Sharing auf `McpLiveRepositoryFixture` (2× → 1) hätte ähnliches Profil wie dieser Spike — die Frage ist, ob die 2 parallelen Subprozess-Loads dort auch tatsächlich CPU-Zeit freigeben.

## Spike-Empfehlung

**Sharing der `SymbolGraphMcpFixture` in dieser Form (1 Collection, 6 Klassen zusammen) bringt keinen Performance-Gewinn.** Empfehlung für EPIC-03:

- **Variante A (eher unwahrscheinlich):** Kein weiteres Fixture-Sharing. Stattdessen EPIC-05 (mockbarer Lade-Pfad im Produktionscode) priorisieren — der echte Hebel liegt vermutlich in kürzeren Subprozess-Lade-Zeiten, nicht in weniger Starts.
- **Variante B (Hybrid, lohnend zu prüfen):** `SymbolGraphMcpFixture` *nicht* als Ganzes sharen, sondern nur die Klassen bündeln, die einen gemeinsamen Set-up wirklich nützen. Konkret: `McpServerCommandTests` (18 Tests, 15 davon über `_symbolGraphMcpFixture`) in eine eigene 2-Klassen-Collection mit `McpServerCommandGetImpactTests` (2 Tests über die Fixture) — beide nutzen die Fixture intensiv. Die übrigen 4 Klassen (jeweils 1 Test) bleiben `IClassFixture` und laufen parallel.
- **Variante C (beibehalten, falls EPIC-03 etwas anderes findet):** Spike-Code auf `main` lassen, in EPIC-03 entscheiden ob verworfen, in B überführt oder beibehalten.

Die Verschlechterung von ~5–8 % ist im Rahmen der ~120 s Gesamtzeit tolerierbar, aber **nicht der erhoffte Gewinn**. EPIC-03 / EPIC-05 müssen entscheiden.

## Bekannte Unschärfen

- Die Messreihe umfasst nur 3 Läufe pro Variante — die Varianz zwischen einzelnen Läufen (insb. der 3. Nachher-Vollauf mit 105,70 s) zeigt, dass System-Cache und Disk-Warmlauf die Zahlen deutlich verschieben können. Eine 10er-Serie wäre belastbarer; für eine Spike-Entscheidung reicht das Bild aber aus.
- Die Sequenzialisierung könnte mit xUnit v3-Features wie `AssemblyAttribute`/`CollectionBehavior` teilweise steuerbar sein; das war nicht Spike-Scope.
- Der `SubprocessConcurrencyGate`-Effekt vor/nach Sharing wurde nicht isoliert gemessen (kein Profiler-Lauf); die Erklärung ist eine Hypothese, kein Beweis.

## Modell-Info

- **coded_by_model:** MiniMax-M3
- **coded_by_model_knowledge_cutoff:** 2026-01
