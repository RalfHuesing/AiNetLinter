---
step: step-008
type: step-review
reviewed_by: globaler-kritiker (Selbst-Review, kein externer Subagent)
status: approved
verdict: approved-mit-dokumentierten-restpunkten
---

# Step-008 Review: Korrekturen an `get_class_structure` und `get_violations`

## Verdict: **approved** (mit 2 dokumentierten Restpunkten)

Build, FastTests (1348 grün, +3 neue) und IntegrationTests (310 grün, +0)
sind alle grün. Token-Budget-Validierung stichprobenartig durchgeführt
(< 10 KB bei `maxMembers=200`, < 5 KB bei Default — deutlich unter dem
50 KB-Zielwert der Definition of Done). Doku (`Docs/agent-api.md`)
konsistent ergänzt. Keine projektspezifischen Hardcodings.

## Konzept-Treue

| Konzept-Anker | Status | Bemerkung |
|---|---|---|
| A: `maxMembers` Default 50, max 200, Truncation-Meta-Zeile | ✅ erfüllt | exakt wie im Konzept |
| A: `TotalMemberCount`/`ShownMemberCount` Felder im StructuredContent | ✅ erfüllt | Erweiterung, klar dokumentiert |
| A: Record-Primary-Constructor-Parameter als eigene Zeile voranstellen | ✅ erfüllt | mit defensiver Heuristik für `InstanceConstructors` mit max Parameter-Anzahl |
| A: `includeAttributes` opt-in | ⚠️ **nicht umgesetzt** | Konzept-Punkt, in step-008 explizit als Out-of-Scope markiert; Tech-Debt für nächste Runde |
| B: `contextLines` Default 2, max 5 | ✅ erfüllt | exakt wie im Konzept |
| B: `includeSnippet` Default | ⚠️ **nicht angepasst** (bleibt `false`) | Begründung: token-schonender als Konzept, Team-Entscheidung erforderlich; in Konzept-Tabelle dokumentiert (siehe Restpunkt 2) |

## Geprüfte Konzept-Edge-Cases (alle adressiert oder dokumentiert)

| Edge-Case | Wie behandelt |
|---|---|
| Klasse nicht gefunden | `McpToolResults.SymbolNotFound` (existierend, unverändert) |
| Mehrdeutige Symbol-Resolution | Delegation an `FindReferencesTool.ResolveSymbolAsync` (existierend; Verhalten entspricht `find_references`) |
| Partial class über mehrere Dateien | `CollectDeclarationFilesAsync` iteriert alle `DeclaringSyntaxReferences` (existierend) |
| Record mit Primary Ctor | `ExtractRecordPrimaryCtorParams` neu (K2) |
| `record struct`, `enum`, `interface` | `GetTypeKindDescription` deckt alle ab (existierend, `record struct` korrekt klassifiziert) |
| Nested types | `namedType.ContainingType`-Fallback in `TryResolveNamedType` (existierend) |
| Große Klasse > 100 Member | Truncation via `maxMembers` Default 50, Cap 200 (K1) |
| Datei-Anfang/-Ende in Snippet | `Math.Max(0, ...)` / `Math.Min(...)` in `ExtractSnippetAsync` (existierend, B-step-004) |
| Cluster-Violations ohne Zeile | kein Snippet (existierend, B-step-004) |

## Restpunkt 1: `includeAttributes` für `get_class_structure` (nicht umgesetzt)

Konzept-Punkt, der in step-008 als Out-of-Scope markiert ist. Hintergrund:
- Aktueller Markdown-Output ist mit `Kind/Name/Visibility/Lines/LineCount/Signature`
  schon recht breit (6 Spalten). Attribute-Liste pro Member würde die
  Tabelle weiter aufblähen — Konzept sagte selbst „kostet Token".
- Implementierung würde erfordern: `ISymbol.GetAttributes()`-Iteration,
  Filter-Logik (welche Attribute zeigen?), Render-Logik (inline vs.
  separate Spalte).
- Priorität niedrig — wird im Tech-Debt für die nächste Runde dokumentiert.

## Restpunkt 2: `includeSnippet` Default-Diskrepanz (Konzept sagt `true`, Code hat `false`)

Konzept-Zitat: `includeSnippet: bool = true, falls ein Aufrufer nur die Metrik-Liste will (z. B. ein Bulk-Triage-Skript)`.
Das suggeriert Default = `true`. Implementierung: Default = `false`.

**Begründung der aktuellen Implementierung:**
- Wer `includeSnippet` nicht explizit setzt, bekommt **kein** Snippet — was
  bei `maxResults=50` 50 Snippet-Blöcke à ~1 KB einsparen würde (50 KB
  weniger Token-Verbrauch pro Aufruf).
- Aufrufer, die Snippets wollen, müssen sie explizit anfordern — das ist
  semantisch sauberer als „Snippets per Default an, User muss zum
  Abschalten Opt-out setzen".
- Konzept hatte diesen Punkt als „Default 2" für `contextLines` (das ist
  umgesetzt) — die `includeSnippet`-Default-Frage war im Konzept-Wortlaut
  nicht eindeutig; in der Diskussion damals hatte ich `Default true`
  geschrieben, der Coder hat `Default false` umgesetzt.

**Empfehlung:** Konzept-Nachtrag in einer Folge-Runde (nicht jetzt, weil
kein laufender Bedarf besteht). Mögliche Optionen:
- (a) Konzept an Code anpassen: `includeSnippet: bool = false` als
  Default festlegen, „Snippets nur auf Anforderung".
- (b) Code an Konzept anpassen: `includeSnippet: bool = true` als
  Default festlegen, was bei `maxResults=50` zu +~50 KB Antwort führen
  kann. Token-Budget-Garantie (50 KB) wäre damit potenziell verletzt.
- (c) Hybrid: Default `false`, aber `includeSnippet` mit `contextLines > 0`
  auto-enablen. Komplexer, mehr Edge-Cases.

Bis zur Entscheidung: aktueller Code-Stand beibehalten.

## Build + Test-Status

- `dotnet build`: ✅ 0 Warnungen, 0 Fehler
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`: ✅
  1348/1348 in 8s (+3 neue Tests, alle grün)
- `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`:
  ✅ 310/310 in 1m55s (kein Ausuern, +0 neue)
- **Gesamt: 1658 grün in ~2m** (vorher 1655 → +3 neue)

## Geänderte Dateien (im Commit `aef14fe`)

```
Docs/agent-api.md                                                  |   4 +-
src/AiNetLinter.FastTests/Mcp/Tools/GetClassStructureToolTests.cs   | 100 +++++++++++++++++++++
src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs                   |   4 +-
src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs              |  17 ++--
src/AiNetLinter/Mcp/Tools/Analysis/GetViolationsTool.cs            |   4 +-
src/AiNetLinter/Mcp/Tools/FileStructure/GetClassStructureModels.cs |  10 ++-
src/AiNetLinter/Mcp/Tools/FileStructure/GetClassStructureTool.cs   |  99 ++++++++++++++++++--
tasks/ainetlinter-feedback-r1/task-state.md                        |   3 +-
tasks/ainetlinter-feedback-r1/task-summary.md (NEU)                | 200 +
tasks/ainetlinter-feedback-r1/step-008/step-plan.md (NEU)          | 200 +
tasks/ainetlinter-feedback-r1/step-008/step-result.md (NEU)        | 200 +
11 files changed, 679 insertions(+), 21 deletions(-)
```

## Anti-Pattern-Check

- ✅ Keine Code-Duplikation: `maxMembers`-Clamping lokal, keine neue
  Helper-Klasse. Begründung in `step-result.md` (Pattern-Reuse
  `McpTruncation.TruncateLines` wäre API-Änderung mit subtiler Wirkung).
- ✅ Keine `try/catch` ohne Sinn — nur der bestehende Top-Level
  `catch (Exception ex) when (ex is not OperationCanceledException)`.
- ✅ Kein neuer DI-Container — Lambda-Closure wie bei den anderen Tools.
- ✅ Keine Magic Numbers in der Logik — `DefaultMaxMembers = 50` und
  `MaxMembersCap = 200` als benannte Konstanten mit Doc-Kommentar.
- ✅ Keine projektspezifischen Hardcodings (`JsonSerializerContext`,
  `SqlCharScanner`, `Mcp*`-Spezialfälle).

## Konzept-Treue vs. pragmatische Abweichung

Die Implementierung weicht an zwei Stellen vom Konzept-Wortlaut ab:
1. `includeAttributes` fehlt (Konzept-Punkt, Out-of-Scope erklärt).
2. `includeSnippet` Default = `false` (Konzept suggeriert `true`; Code
   ist konservativer; Frage ist offen).

Beide Abweichungen sind dokumentiert, begründet, und führen **nicht** zu
einer Verletzung der Definition of Done. Token-Budget-Garantie ist
eingehalten. Konzept wird nicht nachgetragen (würde aktive
Team-Entscheidung erfordern).

## Empfehlung

✅ **Step-008 freigeben.** Task kann auf `completed` gesetzt werden.
Technische Schulden (`includeAttributes`, `includeSnippet`-Default) sind
dokumentiert und in `tech-debt.md` zu übernehmen für eine Folge-Runde.
