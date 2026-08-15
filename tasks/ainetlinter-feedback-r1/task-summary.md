---
task: ainetlinter-feedback-r1
type: task-summary
produced_by: globaler-kritiker
last_updated: 2026-08-15T19:58:00+02:00
verdict: needs-correction
---

# Task Summary: ainetlinter-feedback-r1

## Verdict: needs-correction

Build und Tests sind grün, sechs von sechs Epics technisch implementiert —
aber drei Konzept-Verstöße rechtfertigen einen Korrektur-Step (step-008),
bevor der Task als final `completed` markiert werden kann.

## Test-Stand (Ground-Truth)

| Suite | Result | Dauer | Hinweis |
|---|---|---|---|
| `dotnet build AiNetLinter.slnx` | ✅ 0 Fehler, 0 Warnungen | 14s | `TreatWarningsAsErrors = true` greift |
| `AiNetLinter.FastTests` (1345) | ✅ 1345 grün, 0 fehlgeschlagen, 0 übersprungen | 13s | mit `--filter Category!=Stress`, `--no-build` |
| `AiNetLinter.IntegrationTests` (310) | ✅ 310 grün, 0 fehlgeschlagen, 0 übersprungen | 1m 51s | mit `--filter Category!=Stress`, `--no-build` |
| **Gesamt** | **1655 grün** | **~2m 5s** | kein Ausuern, im bisherigen Korridor |

## Was gut lief (Pattern-Reuse hat funktioniert)

- **`IsTestFile`-Skip-Pattern** sauber auf `MiddleManChecker` (FB-02,
  step-001) und `PublicMembersChecker` (FB-03, step-002) ausgerollt —
  exakt das im Konzept unter „Entdeckte Mängel/Redundanzen"
  dokumentierte Pattern.
- **`MetricsConfig.MaxPublicMembersPerTypeApplyToTestFiles` mit Default
  `false`** + Eintrag in `ConfigOverrides`/`rules.json`/Baseline — sauber
  komplementiert zum Pattern-Reuse-Fund.
- **`scopeType`-Filter** in `find_duplicates` nutzt pragmatisch
  `PathNormalizer.IsTestFile` + Project-Name-Suffix-Match statt der
  `IsTestFile`-CheckerContext-Propagation. Funktional identisch,
  Performance etwas besser (kein voller LinterEngine-Roundtrip). Akzeptable
  Implementierungs-Abweichung vom Konzept-Pointer.
- **Heuristik in `AIContextFootprintCalculator` (FB-01, step-006)**:
  `IsDeclarationOnlyType` prüft korrekt „nur dann declaration-only, wenn
  **gar keine** ordinary methods vorhanden sind" — exakt die im Konzept
  festgehaltene Edge-Case-Regel („Heuristik nur anwenden, wenn **alle**
  Member declaration-only sind").
- **Step-007 als reines Refactoring + Doku-Sync** (233 Zeilen Refactor in
  `GetClassStructureTool.cs` + `Mcp/AnalysisToolRegistrations.cs` u. a.) —
  keine Verhaltens-Änderung, klarer Doku-Commit.

## Befund: drei Konzept-Verstöße

### 1. ROT — `get_class_structure` ohne `maxMembers` (Konzept-Verstoß)

**Konzept-Zitat** (konzept.md, „Muss-Haben" → A, Edge-Cases):
> „`maxMembers` (Default 50, max 200): begrenzt die Member-Liste
> konsistent mit `McpTruncation`-Mechanik. Bei Überschreitung
> Truncation-Meta-Zeile mit „weitere N Member" Hinweis."

**Implementiert** (`FileStructureToolRegistrations.cs:46`):
```csharp
async (string? symbol = null, string? sortBy = "lines", CancellationToken ct = default) =>
```

`maxMembers` fehlt komplett. `ExtractMembers` (Zeile 132) liefert
**alle** Member ohne Cap. Bei einer Konfigurations-Klasse mit 200
public constants oder einer großen Service-Klasse mit 150 Methoden
bekommt der Aufrufer die volle Liste als Markdown-Tabelle + JSON-Array
in `StructuredContent` — kein Token-Budget-Schutz.

**Konsequenz:** Token-Budget-Garantie des Konzepts (Definition of Done
→ „Token-Budget (harter Test)") ist verletzt. Die im Konzept explizit
als „hartes Test-Kriterium" formulierte Worst-Case-Begrenzung
(`maxMembers = 200` → Antwort bleibt < 50 KB) ist nicht prüfbar, weil
der Parameter nicht existiert.

**Test-Coverage-Lücke:** `GetClassStructureToolTests.cs` hat keinen
einzigen Truncation-Test. Suche nach `maxMembers`/`Truncat` in der
Datei liefert 0 Treffer.

### 2. GELB — `get_violations`-Snippet-Defaults konservativer als Konzept

**Konzept-Zitat** (konzept.md, „Muss-Haben" → B, Edge-Cases):
> „`contextLines` als Tool-Parameter (Default 2, max 5) — Snippet
> zeigt `N` Zeilen davor, die verletzende Zeile, `N` Zeilen danach."

**Implementiert** (`AnalysisToolRegistrations.cs:65`):
```csharp
async (string? scopeFilter = null, int maxResults = ...,
       int contextLines = 0, bool includeSnippet = false,
       CancellationToken ct = default) => ...
```

**Defaults:** `contextLines = 0`, `includeSnippet = false`.

**Effekt:** Mit Defaults bekommt der Aufrufer **kein** Snippet — er muss
explizit `includeSnippet=true` setzen, dann bekommt er mit
`contextLines=0` nur die verletzende Zeile (kein Kontext davor/danach).
Das Konzept wollte „Default 2 Zeilen Kontext, Snippet per Default an".

**Bewertung:** Die Implementierung ist **token-schonender** als das
Konzept — wer nicht aktiv `includeSnippet=true` setzt, bekommt keine
Snippet-Daten. Das ist semantisch verteidigbar als „kein Snippet, bis
der User es explizit anfordert", aber wörtlich gegen das Konzept.

**Sekundärer Längen-Cap:** `ExtractSnippetAsync` in
`GetViolationsScanner.cs` cappt bei `> 15` Zeilen Total auf 15 Zeilen
(`endLineIndex = startLineIndex + 14`). Konzept sagte max 5 Kontextzeilen
× 200 Zeichen. Der jetzige 15-Zeilen-Cap ist **strenger** als das
Konzept (deckelt die Snippet-Größe effektiv auf ~3 KB bei 200
Zeichen/Zeile). Akzeptabel.

### 3. GELB — `get_class_structure` ignoriert Record-Primary-Constructor-Parameter

**Konzept-Zitat** (konzept.md, „Muss-Haben" → A, Edge-Cases):
> „`record` mit Primary Constructor → Parameter des Primary
> Constructors als eigene Zeile vor den restlichen Membern."

**Implementiert** (`GetClassStructureTool.cs:142`):
```csharp
if (m.IsImplicitlyDeclared && m is not IMethodSymbol { MethodKind: MethodKind.Constructor })
{
    continue;
}
```

**Effekt:** Der implizit deklarierte Primary-Constructor eines Records
fällt durch `IsImplicitlyDeclared && m is not IMethodSymbol { ... }`
durch, weil `MethodKind.Constructor` zwar erlaubt ist, aber der
Primary-Constructor eines `record class Foo(string Bar, int Baz)` ist
per Roslyn `MethodKind.Constructor` mit `IsImplicitlyDeclared = true`.
Er wird also **durchgereicht**. Aber seine **Parameter** sind nicht als
eigene Member sichtbar — sie hängen am Constructor-Symbol, nicht am
Typ.

**Test-Coverage-Lücke:** `GetClassStructureToolTests.cs` enthält keinen
einzigen `record`-Test (`PrimaryConstructor|IsRecord|record` → 0
Treffer). Die im Konzept vorgesehene „Parameter vor restlichen Membern"-
Darstellung ist nirgends implementiert oder getestet.

**Bewertung:** Kleinere Konzept-Lücke. Records sind in modernem C# häufig
(Value Objects, DTOs). Wer `get_class_structure` auf einen Record
anwendet, bekommt eine Member-Liste ohne die Konstruktor-Parameter — das
ist irreführend.

## Was bewusst NICHT beanstandet wird

- **`scopeType`-Implementierung nutzt `PathNormalizer.IsTestFile` + Project-Name-Suffix** statt der im Konzept genannten `CheckerContext.IsTestFile`-Propagation über `DocumentContext`. Pragmatisch besser (kein LinterEngine-Roundtrip), Ergebnis identisch. Pattern-Reuse-Fund aktualisiert.
- **`includeAttributes`-Parameter** für `get_class_structure` fehlt ebenfalls. Im Konzept als „opt-in Attribut-Liste pro Member" mit Hinweis „kostet Token" — Nice-to-Have, nicht zwingend im selben Korrektur-Step zu beheben. Vorschlag: in einer späteren Runde, falls konkreter Bedarf entsteht.
- **Snippet-Längen-Cap von 15 Zeilen** in B — strenger als Konzept, also kein Verstoß gegen die Token-Garantie.

## Empfohlener Korrektur-Step (step-008)

| # | Fix | Aufwand | Korrektur-Bezug |
|---|---|---|---|
| 1 | `get_class_structure`: `maxMembers`-Parameter (Default 50, max 200) + Truncation-Meta-Zeile via `McpTruncation` | mittel | corrects: step-005 (A) |
| 2 | `get_class_structure`: Record-Primary-Constructor-Parameter als eigene Member-Zeile voranstellen (Kind „PrimaryCtor-Param") | klein–mittel | corrects: step-005 (A) |
| 3 | `get_violations`: `contextLines` Default von 0 auf 2 anheben (entspricht dem Konzept) | trivial | corrects: step-004 (B) |

**Gebündelt in EINEM Commit** gemäß User-Vorgabe („mache Große Steps!
Keine Kleinen/Mini Änderungen"). Tests werden mit angepasst — neue
Tests für `maxMembers`-Truncation und für Record-Primary-Constructor.
`Docs/agent-api.md` muss die neuen Parameter dokumentieren. Konzept
bleibt unverändert; die GELB-Befunde werden im step-review explizit als
„Konzept vs. Implementierung dokumentiert" festgehalten (kein
Konzept-Nachtrag, weil der Konsens zum Default-Wert im Team noch zu
bilden ist).

## Tech-Debt-Übergabe an nächste Runde

Nicht in step-008 enthalten, für künftige Erweiterung dokumentiert:

- **`get_class_structure`: `includeAttributes` (Default false)** — Konzept-Punkt, opt-in, niedrige Priorität.
- **`find_duplicates` und `get_violations` haben getrennte
  Test-Erkennungs-Mechanismen** (PathNormalizer vs. Project-Name-Suffix).
  Mittelfristig: `ITestDetector` interface mit einer Implementierung, die
  beide nutzt. Nicht dringend.
- **`AIContextFootprint`-Heuristik** deckt Records gut ab, aber
  `record struct` mit expliziten Methoden wird per `ordinaryMethods.Any()`
  korrekt als „nicht declaration-only" klassifiziert. Kein Bug, nur
  Beobachtung.

## Verdict-Begründung

Der Task ist **nicht** als „abgeschlossen" zu markieren. Drei von sechs
Epics weichen messbar vom Konzept ab, eines davon (A: `maxMembers`)
verletzt die Definition of Done. Build und Tests sind grün, also ist
der Fix-Aufwand überschaubar und birgt kein Risiko — alle Änderungen
sind additiv (neue Parameter, neue Truncation-Logik, neue
Default-Werte). Korrektur-Step ist der saubere Pfad zum sauberen
Abschluss.
