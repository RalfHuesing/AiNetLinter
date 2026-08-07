---
status: done
type: step-plan
task: flaky-and-test-performance
step: 001
title: "Spike — SymbolGraphMcpFixture auf ICollectionFixture umstellen, Vorher/Nachher messen"
epic: EPIC-01          # Spike — Fixture-Sharing validieren (Vorarbeit) — siehe roadmap.md
estimated_risk: medium  # Explorativ; Code-Inspektion zeigt keine sichtbare Fixture-Mutation, ABER Sequenzialisierung der 6 Klassen in einer Collection kann Isolations-Vorteil der Parallelisierung zunichtemachen — exakt das, was der Spike messen soll.
step_type: single  # ein in sich geschlossener Spike, keine Sammlung trivialer Mini-Befunde
items: []
created_by: planer
created_by_model: MiniMax-M3
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-07T09:02:00+02:00
related_to: []
---

# Step 001: Spike — SymbolGraphMcpFixture auf ICollectionFixture umstellen, Vorher/Nachher messen

## Bezug

- **Task:** `flaky-and-test-performance`
- **Epic:** `EPIC-01` aus `roadmap.md` — Spike — Fixture-Sharing validieren (Vorarbeit). Spike-Ergebnis entscheidet, ob EPIC-03 (Sharing im großen Stil) ausreicht oder ob zusätzlich EPIC-05 (Produktionscode-mockbarer Lade-Pfad) nötig wird.
- **Konzept-Referenz:** `konzept.md` §"Wie" Schritt 1 (Explorations-/Spike-Schritt zuerst), §"Muss-Haben" Punkt 3 (Reduktion der ~60-80 unabhängigen Lade-/Subprozessvorgänge mindestens für `SymbolGraphCatalogFixture` 18× und `SymbolGraphMcpFixture` 6×), §"Wo im Projekt" (Hauptverdacht: kein Fixture-Sharing).

## Aktueller Projektzustand (JIT-Kontext)

Beim Lesen des aktuellen Code-Stands vorgefunden — weicht an einer Stelle von `konzept.md` ab:

**`SymbolGraphCatalogFixture`** (`src/AiNetLinter.Tests/Fixtures/SymbolGraphCatalogFixture.cs:15-31`)

- Lädt einmalig pro Testklasse ein `SymbolGraphMiniFixtureWorkspace` + `SourceFileCatalog.LoadAsync(Workspace.RootPath)`.
- **Tatsächliche Verwendungen: 1 Testklasse** — `Commands/McpServerCommandLoadingStateTests.cs:21` (`IClassFixture<SymbolGraphCatalogFixture>`). Die im `konzept.md` und in `roadmap.md` referenzierten "18×" treffen auf den heutigen Stand **nicht** mehr zu — die übrigen ehemaligen Verwender wurden vermutlich bereits in vorherigen Refactorings entfernt. **Der Hebel auf dieses Fixture ist daher minimal** (Sharing von 1× auf 1× ändert nichts), wird aber im Spike methodisch mit-validiert, um zu bestätigen, dass die Umstellungs-Mechanik sauber durchläuft.
- State (`Workspace`, `Catalog`) ist `private set;` — von außen immutable. In der einzigen Verwendungsstelle (`McpServerCommandLoadingStateTests.cs:25-28`) wird `_fixture.Catalog` nur gelesen. **Keine Mutation beobachtbar.**

**`SymbolGraphMcpFixture`** (`src/AiNetLinter.Tests/Fixtures/SymbolGraphMcpFixture.cs:15-36`)

- Lädt einmalig pro Testklasse ein `SymbolGraphMiniFixtureWorkspace` und startet einen `McpTestClient.ConnectAsync(Workspace.RootPath, timeoutSeconds: 60, retryOptions: new McpTestClientRetryOptions(MaxRetries: 5, BaseDelayMs: 1000, BackoffFactor: 2.0))`. Der `timeoutSeconds: 60` und der Retry-Backoff (5×1s, 2.0×) machen den Subprozess-Start potenziell teuer, wenn der Handshake unter Last langsam ist.
- **Tatsächliche Verwendungen: 6 Testklassen** (passt zu `konzept.md` und `roadmap.md`):
  - `Commands/McpServerCommandFindReferencesTests.cs:9` — `IClassFixture<SymbolGraphMcpFixture>`, 1 Test, lesend (`_fixture.Client.CallToolGetTextAsync`).
  - `Commands/McpServerCommandFindSymbolTests.cs:9` — `IClassFixture<SymbolGraphMcpFixture>`, 1 Test, lesend.
  - `Commands/McpServerCommandGetImpactTests.cs:13` — `IClassFixture<SymbolGraphMcpFixture>`, 2 Tests, **beide lesend** auf `_fixture.Client`; die übrigen Test-Daten kommen aus einer separaten `GitImpactMiniFixtureWorkspace`, die lokal im Test aufgebaut wird (nicht aus der Fixture).
  - `Commands/McpServerCommandMissHintTests.cs:12` — `IClassFixture<SymbolGraphMcpFixture>`, 1 Test, lesend.
  - `Commands/McpServerCommandTests.cs:18` — `IClassFixture<SymbolGraphMcpFixture>, IClassFixture<BaselineMcpFixture>`, 18 Tests. **Achtung:** Hier sind 2 Fixtures kombiniert. Im Spike nur `SymbolGraphMcpFixture` rausziehen; `BaselineMcpFixture` bleibt `IClassFixture` (das ist 1× verwendet → kein Sharing-Potenzial, kein Spike-Ziel).
  - `Mcp/McpServerAllToolsE2ETests.cs:18` — `IClassFixture<SymbolGraphMcpFixture>`, mehrere Tests, lesend.
- State (`Workspace`, `Client`) ist `private set;` — von außen immutable. **In allen 6 Verwendungsstellen ist nur lesender Zugriff auf `_fixture.Client.CallTool*Async(...)` beobachtbar.** Kein Test mutiert `Workspace` oder `Client` — die Mini-Solution wird nirgends modifiziert.
- **Konsequenz für Spike:** Sharing-Voraussetzungen (Read-Only-Nutzung) sind erfüllt. Wenn die Tests beim Sharing dennoch brechen, liegt es nicht an einer Fixture-Mutation, sondern an impliziten Annahmen (z. B. dass jeder Test seinen eigenen Subprozess sieht, oder dass temporäre Workspace-Pfade pro Testklasse eindeutig sind).

**`xunit.runner.json` (`src/AiNetLinter.Tests/xunit.runner.json`)**

- `parallelizeTestCollections: true`, `maxParallelThreads: 0` (Prozessorzahl), `longRunningTestSeconds: 3`. xUnit v3 serialisiert Tests *innerhalb* einer Collection; Tests *zwischen* Collections laufen parallel. Eine `ICollectionFixture` über die 6 Klassen bündelt deren Tests in **eine** Collection → sie laufen untereinander sequenziell. Das ist der zentrale Trade-off des Spikes: weniger Subprozess-Starts (gut) gegen weniger Parallelität (schlecht, falls Test-Laufzeit dominiert).

**`SubprocessConcurrencyGate` (`src/AiNetLinter.Tests/Fixtures/SubprocessConcurrencyGate.cs:15-57`)**

- Globale `SemaphoreSlim(6, 6)` — begrenzt gleichzeitige `AiNetLinter.exe`-Subprozessstarts auf 6 (60s-Timeout am Gate). Die heutige 6er-Schwelle korrespondiert exakt mit den 6 `SymbolGraphMcpFixture`-Instanzen + weiteren Subprozess-Tests (`McpTestClientParallelTests`, `CliProcessRunner`). Eine Reduktion der Fixture-Instanzen von 6 auf 1 verändert das Last-Profil des Gates; das ist Teil der Spike-Messung.

**`xunit v3` / Spike-Werkzeugkasten**

- `ICollectionFixture<T>`: xUnit v3 unterstützt es; erfordert eine `[CollectionDefinition("Name")]`-Klasse, die `ICollectionFixture<T>` implementiert. An jeder Testklasse: `[Collection("Name")]` anstelle von `IClassFixture<T>`. Tests innerhalb der Collection laufen sequenziell, Collections untereinander parallel.
- Spike-Werkzeuge: `Measure-Command { dotnet test ... }` (PowerShell) für Zeitmessung; `TestResults/latest.trx` für Test-Ergebnisse; `dotnet run --project src/AiNetLinter -- --self-lint` für Self-Lint-Verifikation.

**Was beeinflusst das den Plan?**

1. Der "echte" Hebel ist `SymbolGraphMcpFixture` (6×), nicht `SymbolGraphCatalogFixture` (1×) — die Empfehlung im Konzept, mit `SymbolGraphCatalogFixture` anzufangen, ist auf den heutigen Stand nicht mehr anwendbar. **Der Spike fokussiert auf `SymbolGraphMcpFixture`.** `SymbolGraphCatalogFixture` wird im Spike nicht umgestellt (kein Hebel; gesonderte Beobachtung im `step-result.md`, dass die ein-Klassen-Verwendung keinen Sharing-Druck hat).
2. Code-Inspektion der 6 Verwendungsstellen zeigt keine Mutation → Sharing-Voraussetzungen sind gut. **Risiko liegt im Parallelitätsverlust innerhalb der Collection** (exakt das, was der Spike messen soll).
3. Da der Spike-Code laut Konzept-Auftrag **committed** wird (nicht verworfen), muss die Spike-Empfehlung im `step-result.md` auch eine Aussage zur Rückroll-Notwendigkeit für EPIC-03 enthalten (siehe "Bekannte Ausnahmen").

## Intention

In diesem Spike wird `SymbolGraphMcpFixture` probeweise von `IClassFixture<T>` (6 separate Instanzen) auf `ICollectionFixture<T>` via `[CollectionDefinition]` (1 geteilte Instanz) umgestellt. Anschließend wird die tatsächliche Zeitersparnis **isoliert** (nur die 6 Klassen) und **unter Volllast** (gesamter `dotnet test`) gemessen sowie geprüft, ob die geteilte Fixture zu Isolationsbrüchen führt. Das Spike-Ergebnis ist die empirische Grundlage dafür, ob EPIC-03 das Sharing im großen Stil umsetzt oder ob zusätzlich EPIC-05 (mockbarer Produktionscode-Lade-Pfad) nötig wird, um Performance zu gewinnen.

## Konkrete Änderungen

### Datei 1 (NEU): `src/AiNetLinter.Tests/Fixtures/SymbolGraphMcpCollection.cs`

- **Was:** Neue Datei anlegen mit minimaler Collection-Definition.
- **Inhalt (sinngemäß, keine wörtliche Vorlage):**
  - `#nullable enable` am Dateianfang.
  - Namespace `AiNetLinter.Tests.Fixtures`.
  - `[CollectionDefinition("SymbolGraphMcp")]`-Attribut auf der Klasse.
  - `public sealed class SymbolGraphMcpCollection : ICollectionFixture<SymbolGraphMcpFixture> { }` — leerer Body, dient als Marker, damit xUnit v3 `SymbolGraphMcpFixture` einmal pro Collection instanziert.
- **Warum:** xUnit-v3-Voraussetzung für `ICollectionFixture<T>`. Eine separate Klasse ist Pflicht, ein Attribut direkt auf der Fixture-Klasse reicht nicht.

### Datei 2: `src/AiNetLinter.Tests/Commands/McpServerCommandFindReferencesTests.cs:9-16`

- **Was:**
  - Zeile 9: `IClassFixture<SymbolGraphMcpFixture>` ersetzen durch `[Collection("SymbolGraphMcp")]`.
  - Sicherstellen, dass `using AiNetLinter.Tests.Fixtures;` (Zeile 4) erhalten bleibt (wird für den Konstruktor-Parametertyp weiterhin gebraucht).
  - Konstruktor und Body unverändert — xUnit v3 injiziert die Collection-Fixture über den gleichen Konstruktor-Pfad.
- **Warum:** Erste der 6 Klassen, die auf Sharing umgestellt wird. Mechanik soll früh sichtbar werden.

### Datei 3: `src/AiNetLinter.Tests/Commands/McpServerCommandFindSymbolTests.cs:9-16`

- **Was:** Analog zu Datei 2 — `IClassFixture<SymbolGraphMcpFixture>` (Zeile 9) ersetzen durch `[Collection("SymbolGraphMcp")]`.
- **Warum:** Sharing-Umstellung, zweiter Verwender.

### Datei 4: `src/AiNetLinter.Tests/Commands/McpServerCommandGetImpactTests.cs:13-20`

- **Was:** Analog zu Datei 2 — `IClassFixture<SymbolGraphMcpFixture>` (Zeile 13) ersetzen durch `[Collection("SymbolGraphMcp")]`.
- **Warum:** Sharing-Umstellung, dritter Verwender. Hinweis im Plan: Diese Klasse erzeugt zusätzlich eigene `GitImpactMiniFixtureWorkspace`-Instanzen in den Tests (Zeilen 36-39, 241-244, 257-260); das bleibt unverändert, da unabhängig von der geteilten `SymbolGraphMcpFixture`.

### Datei 5: `src/AiNetLinter.Tests/Commands/McpServerCommandMissHintTests.cs:12-19`

- **Was:** Analog zu Datei 2 — `IClassFixture<SymbolGraphMcpFixture>` (Zeile 12) ersetzen durch `[Collection("SymbolGraphMcp")]`.
- **Warum:** Sharing-Umstellung, vierter Verwender.

### Datei 6: `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs:18-29`

- **Was:**
  - Zeile 18: `IClassFixture<SymbolGraphMcpFixture>, IClassFixture<BaselineMcpFixture>` ersetzen durch `[Collection("SymbolGraphMcp")], IClassFixture<BaselineMcpFixture>` — d. h. `SymbolGraphMcpFixture`-Anteil raus, `[Collection]`-Anteil rein, `BaselineMcpFixture` (1× verwendet) bleibt `IClassFixture`.
  - Konstruktor und Body unverändert.
- **Warum:** Sharing-Umstellung für `SymbolGraphMcpFixture`; `BaselineMcpFixture` wird **nicht** in den Spike einbezogen, da nur 1× verwendet → kein Hebel, würde die Spike-Messung nur verwässern.
- **Wichtig:** Sicherstellen, dass die Reihenfolge in der Klassen-Deklaration kompiliert (mehrere Attribute sind kommagetrennt erlaubt, Reihenfolge beliebig).

### Datei 7: `src/AiNetLinter.Tests/Mcp/McpServerAllToolsE2ETests.cs:18-25`

- **Was:** Analog zu Datei 2 — `IClassFixture<SymbolGraphMcpFixture>` (Zeile 18) ersetzen durch `[Collection("SymbolGraphMcp")]`.
- **Warum:** Sharing-Umstellung, sechster und letzter Verwender.

### Mess- und Validierungs-Logik (im Coder-Schritt, kein Datei-Output)

- **Vorher-Messung (vor den 7 Änderungen oben):**
  1. `Measure-Command { dotnet test --filter "FullyQualifiedName~McpServerCommandFindSymbol|FullyQualifiedName~McpServerCommandFindReferences|FullyQualifiedName~McpServerCommandGetImpact|FullyQualifiedName~McpServerCommandMissHint|FullyQualifiedName~McpServerCommandTests|FullyQualifiedName~McpServerAllToolsE2E" --no-build }` (3× ausführen, Median notieren) — **isolierte** Zeit der 6 Klassen.
  2. `Measure-Command { dotnet test --no-build }` (3× ausführen, Median notieren) — **voller** Testlauf.
  3. Beide Mediane in `step-001/messung-vorher.txt` (oder direkt im späteren `step-result.md`) festhalten.
- **Nachher-Messung (nach den 7 Änderungen):** exakt dieselben beiden Messungen, dokumentiert als `messung-nachher.txt` (bzw. im `step-result.md`).
- **Isolationscheck (im Nachher-Lauf):**
  - Grüner Lauf = kein offensichtlicher Isolationsbruch.
  - Falls rot: gezielt die Fehlermeldung lesen — häufigstes erwartetes Muster wäre "Assertion failed, weil Test A den Subprozess in einen Zustand gebracht hat, den Test B als Initialzustand voraussetzt". Falls ein solcher Bruch auftritt, im `step-result.md` die genaue Testkombination und die Fehlermeldung dokumentieren.
  - **Explizit nicht** im Spike-Scope: produktive Fehlerbehebung des Isolationsbruchs. Spike-Code bleibt auch bei rotem Spike-Ergebnis auf `main` (siehe "Bekannte Ausnahmen"); EPIC-03 / EPIC-05 entscheiden dann, was passiert.
- **Self-Lint:** `dotnet run --project src/AiNetLinter -- --self-lint` — muss `OK` bleiben, vor und nach.

## Tests

- **Keine neuen Tests** — der Spike wird durch die bereits existierenden Tests der 6 umgestellten Klassen validiert. Wenn sie nach der Umstellung weiterhin grün laufen, ist die Sharing-Mechanik verträglich. Wenn sie rot laufen, ist das der eigentliche Spike-Befund (Isolationsbruch).
- Konkret vorhandene Tests, die nach der Umstellung mit-validieren (alle lesend, keine Mutationen beobachtbar):
  - `McpServerCommandFindSymbolTests.RunAsync_ValidFixture_FindSymbolWithMaxResultsTruncates`
  - `McpServerCommandFindReferencesTests.RunAsync_ValidFixture_FindReferencesWithMaxResultsTruncates`
  - `McpServerCommandGetImpactTests.RunAsync_ValidFixture_GetImpactSymbolBranchWithMaxResultsTruncates`
  - `McpServerCommandGetImpactTests.RunAsync_ValidFixture_GetImpactGitBranchWithMaxResultsTruncates`
  - `McpServerCommandMissHintTests.RunAsync_NonCsOnlyMatch_ReturnsExplicitMissHint`
  - `McpServerCommandTests` (18 Tests, davon die 15 lesenden über `_symbolGraphMcpFixture.Client` und 3 über `_baselineMcpFixture.Client`).
  - `McpServerAllToolsE2ETests` (mehrere Tests über `_fixture.Client`).

## Definition of Done

- [ ] Alle 7 "Konkrete Änderungen" (1 neue Datei, 6 geänderte Testklassen) umgesetzt.
- [ ] `dotnet build` grün (Zero-Warning-Direktive aus `AiNetLinterRichtlinien.mdc` §5).
- [ ] `dotnet test` (voller Lauf) grün — sowohl vorher als auch nachher. Roter Lauf ist explizit erlaubt **nur** als dokumentierter Spike-Befund (Isolationsbruch), nicht als DoD.
- [ ] Vorher- und Nachher-Messungen dokumentiert (Zeit-Mediane, isoliert + voller Lauf, jeweils 3 Läufe).
- [ ] `dotnet run --project src/AiNetLinter -- --self-lint` grün.
- [ ] `step-001/step-result.md` geschrieben mit: Mess-Zahlen, Beobachtungen zur Isolation, Empfehlung "Sharing reicht für EPIC-03" oder "zusätzlich EPIC-05 nötig", sowie ggf. Rückroll-Hinweis für EPIC-03 (siehe "Bekannte Ausnahmen").
- [ ] `status` in `step-plan.md` von `open` → `in_progress` (durch Coder) → `done (pending audit)` (durch Coder) gesetzt.

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc` §4 ("Updates & Tests") — konkret der Abschnitt "Testsuite-Parallelität bewahren": Neue Testklassen werden standardmäßig nicht in eine zwangsserialisierende Collection aufgenommen. **Wichtig für den Spike:** `[Collection("...")]` ist *kein* `DisableParallelization` für die ganze Assembly, sondern nur eine Serialisierungs-Granularität für die 6 Tests in dieser Collection. Andere Collections laufen weiterhin parallel. Spike-Ergebnis muss diese Parallelitätswirkung dokumentieren.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §4 (zweiter Absatz, MCP & Dogfood Testing) — `McpTestClient`-basierte Tests sind genau die, die dieser Spike umstellt; die Regel bestätigt, dass die xUnit-v3-Infrastruktur der richtige Ort ist.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §5 (Sparsame Kommentare) — relevant für den XML-Doc-Kommentar auf der neuen `SymbolGraphMcpCollection`-Klasse: nur *Why* (Sharing-Reason), keine `step-001`/`EPIC-01`-Verweise. Ebenso: bestehende Kommentare in `SymbolGraphMcpFixture.cs` (`Wird in Read-Only E2E-Tests via IClassFixture<...> verwendet`) müssen angepasst werden, da die Verwendungsform jetzt `[Collection("...")]` ist.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §5 (Zero-Warning) — alle Änderungen müssen warnungsfrei kompilieren.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §5 (Symptom-Fixing verboten) — falls ein Test im Nachher-Lauf rot wird, **nicht** den Test abschwächen oder die Assertion lockern, um den Spike grün zu bekommen. Den Befund stattdessen im `step-result.md` dokumentieren.

## Bekannte Ausnahmen

- **`SymbolGraphCatalogFixture` (1× verwendet) wird im Spike NICHT umgestellt.** Begründung: kein Sharing-Hebel, und die ein-Klassen-Verwendung rechtfertigt keinen `CollectionDefinition`-Aufwand. Beobachtung wird im `step-result.md` kurz festgehalten, damit EPIC-03 nicht aus Gewohnheit auch dort Sharing einzieht.
- **`McpLiveRepositoryFixture` (2× verwendet) wird im Spike NICHT umgestellt.** Begründung: größeres Risiko (echte `AiNetLinter.slnx`-Lade-Vorgänge, längere Initialisierung) und für einen Spike mit begrenzter Aussagekraft zu teuer. Gehört in EPIC-03.
- **Spike-Code wird auf `main` committed** (Auftragstext, Konzept §"Wie" Schritt 1). Falls die Spike-Empfehlung im `step-result.md` lautet "Sharing reicht nicht" (z. B. weil die Sequenzialisierung der 6 Klassen die Parallelitätsverluste nicht kompensiert), bleibt der Spike-Code trotzdem auf `main`. **EPIC-03 muss dann entscheiden**, ob der Spike-Code zurückgerollt, in eine Hybrid-Lösung (z. B. nur ein Teil der 6 Klassen wird Collection, Rest bleibt `IClassFixture`) überführt oder beibehalten wird. Diese Rückroll-/Anpassungspflicht ist explizit im `step-result.md` zu dokumentieren.
- **Bestehende Kommentare in `SymbolGraphMcpFixture.cs` Zeile 13** (`Wird in Read-Only E2E-Tests via IClassFixture<SymbolGraphMcpFixture> verwendet`) sind nach der Umstellung formal unzutreffend — die Verwendungsform ist jetzt `[Collection("SymbolGraphMcp")]`. Anpassung an Ort und Stelle (Zeile 13) ist Teil des Spikes, nicht ein "nice-to-have" — `AiNetLinterRichtlinien.mdc` §5 verlangt sparsame, aber zutreffende Kommentare.

## Code-Skizze (optional)

```csharp
// src/AiNetLinter.Tests/Fixtures/SymbolGraphMcpCollection.cs
#nullable enable

using Xunit;

namespace AiNetLinter.Tests.Fixtures;

// Eine geteilte SymbolGraphMcpFixture-Instanz pro Collection; xUnit v3 serialisiert die
// 6 zugehoerigen Testklassen untereinander, instanziiert die Fixture aber nur einmal.
// Reduziert 6 eigenstaendige MCP-Subprozess-Starts auf 1.
[CollectionDefinition("SymbolGraphMcp")]
public sealed class SymbolGraphMcpCollection : ICollectionFixture<SymbolGraphMcpFixture>
{
}
```

```csharp
// src/AiNetLinter.Tests/Commands/McpServerCommandFindSymbolTests.cs:9 (nachher)
[Collection("SymbolGraphMcp")]
public sealed class McpServerCommandFindSymbolTests
```

## Notes

- **Reihenfolge der Datei-Änderungen im Coder-Schritt:** Empfohlen, zuerst Datei 1 (neue Collection-Definition) anzulegen und `dotnet build` auszuführen, dann die 6 Testklassen-Änderungen in einem Commit zusammenzufassen. So bleibt jeder Zwischenschritt kompilierbar.
- **Test-Collection-Konflikt mit xUnit v3:** Wenn ein Test versehentlich sowohl `IClassFixture<SymbolGraphMcpFixture>` als auch `[Collection("SymbolGraphMcp")]` trägt, gibt es einen Kompilierfehler (`Fixture already declared`). Daher die Attribut-Liste in `McpServerCommandTests.cs:18` besonders sorgfältig prüfen (zwei Fixtures vorhanden, nur eine davon wird Collection).
- **`SymbolGraphMcpFixture.cs:13`-Kommentar** muss angepasst werden (sonst widerspricht der Code seinem eigenen Doc-Kommentar). Gehört in den Coder-Schritt, nicht in den Spike-Review.
- **Messen unter "isoliert" vs. "Volllast":** Der "isolierte" Lauf filtert auf die 6 Klassen und ist *vor* der Umstellung **nicht** repräsentativ — die 6 Klassen laufen dann weiterhin parallel (separate `IClassFixture`-Instanzen). Erst *nach* der Umstellung laufen sie sequenziell innerhalb der Collection. Der **Vergleich vorher/nachher bei isoliertem Filter** zeigt daher genau, was die Sequenzialisierung kostet; der **Vergleich vorher/nachher beim Volllauf** zeigt den Netto-Effekt im realen Lauf. Beide Zahlen werden im `step-result.md` nebeneinander dokumentiert.
- **Beobachtungspunkt für EPIC-03:** Falls die Spike-Empfehlung "Sharing reicht" lautet, ist die Erweiterung auf `BaselineMcpFixture` (1× → keine Auswirkung) und `McpLiveRepositoryFixture` (2× → potenziell relevanter Hebel) der nächste logische Schritt. Falls "Sharing reicht nicht", ist EPIC-05 (mockbarer Lade-Pfad im Produktionscode) der wahrscheinlichste Pfad.
- **Kein Roadmap-Update durch diesen Step:** EPIC-01 ist mit diesem Step-Plan in Bearbeitung, nicht abgeschlossen; der Abschluss-Haken wird erst nach `step-review.md` durch den Orchestrator gesetzt.
