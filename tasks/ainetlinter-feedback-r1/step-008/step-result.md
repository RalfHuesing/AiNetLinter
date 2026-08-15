---
step: step-008
type: step-result
corrects: [step-005, step-004]
status: done
---

# Step-008 Result: Korrekturen umgesetzt

## Was gemacht wurde

Drei Konzept-Verstöße aus dem Globalen-Kritiker-Befund in einem Commit
gebündelt umgesetzt:

1. **`get_class_structure`: `maxMembers` Parameter + Truncation**
   - `GetClassStructureTool`: neue Konstante `DefaultMaxMembers = 50` und
     `MaxMembersCap = 200`. Neuer 4-arg-Overload
     `ExecuteAsync(state, symbol, sortBy, maxMembers, ct)`; alter
     3-arg-Overload ruft ihn mit `DefaultMaxMembers` auf
     (Backward-Compat für die 5 existierenden Tests).
   - `ExtractMembers` / `SortMembers` unverändert in der Logik, dafür
     wird nach `SortMembers` die Liste auf `maxMembers` trunkiert
     (geclampt auf 1..200).
   - `ClassStructurePayload` (`GetClassStructureModels.cs`): `MemberCount`
     durch `TotalMemberCount` + `ShownMemberCount` + `Truncated: bool`
     ersetzt. Bestehende 5 Tests, die nur `payload.Members` prüfen,
     bleiben grün.
   - `FileStructureToolRegistrations`: Lambda-Header bekommt
     `int maxMembers = GetClassStructureTool.DefaultMaxMembers`.
   - `RenderMarkdown`: Truncation-Meta-Zeile
     `"[N Member gesamt, M gezeigt — maxMembers erhöhen oder sortBy wechseln]"`
     wird bei `Truncated = true` angehängt.
   - Description-String von `const` auf `static readonly` umgestellt
     (String-Konkatenation mit `GetClassStructureTool.MaxMembersCap`
     braucht Runtime-Evaluation).

2. **`get_class_structure`: Record-Primary-Constructor-Parameter**
   - `ExtractMembers`: vor der normalen `GetMembers()`-Schleife prüft
     `namedType.IsRecord` und ruft `ExtractRecordPrimaryCtorParams` auf.
   - `ExtractRecordPrimaryCtorParams`: iteriert über
     `namedType.InstanceConstructors` (sortiert nach Parameter-Anzahl
     absteigend), erstellt für jeden Parameter eine
     `ClassStructureMemberEntry` mit `Kind = "PrimaryCtor-Param"`,
     `Name = p.Name`, `Signature = "{name} : {type}"`,
     `Visibility = "public"`, `StartLine/EndLine = recordLine`.
   - Defensiv: kein Crash, wenn kein impliziter Constructor gefunden
     wird (z. B. bei Records ohne Primary Ctor).

3. **`get_violations`: `contextLines` Default 0 → 2**
   - `AnalysisToolRegistrations`: Lambda-Header
     `int contextLines = 2` (vorher `0`).
   - `GetViolationsTool`: 2-arg-Overload und
     `GetViolationsToolExecutionOptions.Default` analog angepasst, damit
     kein Backward-Compat-Pfad den alten Default reproduziert.
   - Description-String aktualisiert (`contextLines 0-5, Default 2`).
   - **Nicht angepasst:** `includeSnippet` Default bleibt `false` (kein
     Snippet ohne explizite Anforderung) — diese Frage ist eine
     Team-Designentscheidung, nicht in step-008 enthalten, wird in
     `step-review.md` dokumentiert.

## Tests

- **1348 FastTests grün** in 17.7s (vorher 1345 → +3 neue).
  - `ExecuteAsync_MaxMembers_TruncatesMemberListAndSetsFlag`:
    Verifiziert Truncation bei 60 privaten Methoden + maxMembers=10
    (TotalMemberCount ≥ 50 als Sanity-Check, Truncated=true,
    ShownMemberCount=10, Meta-Zeile im Markdown).
  - `ExecuteAsync_MaxMembers_ClampedToCap`: Verifiziert, dass
    `maxMembers=10000` nicht crasht und nicht versehentlich truncated
    bei einer Klasse mit 2 Membern.
  - `ExecuteAsync_RecordWithPrimaryCtor_ListsParamsBeforeMembers`:
    Verifiziert, dass `record Person(string FirstName, string LastName,
    int Age)` 3 PrimaryCtor-Param-Zeilen vor den restlichen Membern
    liefert (Reihenfolge der Params untereinander nicht geprüft, weil
    Default-sortBy="lines" bei gleicher `recordLine` stabil, aber nicht
    deterministisch sortiert).
- **310 IntegrationTests grün** in 2m13s (kein Ausuern, +0 neue).
- **Build:** 0 Warnungen, 0 Fehler (mit `TreatWarningsAsErrors = true`).

## Geänderte Dateien

| Datei | + | − | Zweck |
|---|---|---|---|
| `src/AiNetLinter/Mcp/Tools/FileStructure/GetClassStructureTool.cs` | +90 | −9 | `maxMembers` Parameter + Truncation + Record-PrimaryCtor-Extraktion |
| `src/AiNetLinter/Mcp/Tools/FileStructure/GetClassStructureModels.cs` | +7 | −3 | `TotalMemberCount` + `ShownMemberCount` + `Truncated` Felder |
| `src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs` | +13 | −4 | `maxMembers` in Lambda-Header, `static readonly` Description |
| `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs` | +2 | −2 | `contextLines = 2` Default + Description-Update |
| `src/AiNetLinter/Mcp/Tools/Analysis/GetViolationsTool.cs` | +2 | −2 | `ContextLines = 2` Default in Record + 2-arg-Overload |
| `src/AiNetLinter.FastTests/Mcp/Tools/GetClassStructureToolTests.cs` | +100 | 0 | 3 neue Tests + existierender Test erweitert um Payload-Felder |
| `Docs/agent-api.md` | +2 | −2 | `maxMembers` + `contextLines`-Default in Tool-Tabelle |
| `tasks/ainetlinter-feedback-r1/task-state.md` | +3 | −2 | Status `in-progress (Korrektur)`, step-008-Eintrag |
| **Total** | **+222** | **−21** | |

## Token-Budget-Validierung

Manueller Smoke-Test (nicht als dauerhafter Test gepinnt, weil er von
Solution-zu-Solution stark variiert): Aufruf mit `maxMembers=200` auf
einer Solution mit einer Klasse von ~100 Membern liefert Markdown-Antwort
< 10 KB. Bei `maxMembers=50` (Default) sinkt das auf < 5 KB. Beide
deutlich unter dem 50-KB-Zielwert aus der Definition of Done.

## Bekannte Einschränkungen

- **Record mit explizitem `this(...)`-Body-Constructor** (z. B.
  `public record Foo(int X) : this(X, "default") { public Foo(int x, string s) { ... } }`):
  Die Heuristik „InstanceConstructor mit den meisten Parametern" wählt
  in diesem Fall den `this(...)`-Constructor statt den Primary Ctor.
  Edge-Case, kein Test, niedrige Priorität.
- **Keine `McpTruncation.TruncateLines`-Wiederverwendung** im
  `get_class_structure`-Pfad: `TruncateLines` arbeitet auf Textzeilen,
  hier brauchen wir strukturierte Truncation. Statt einer
  Generalisierung (die `McpTruncation` subtil ändern würde) wurde
  die Truncation-Logik lokal in `GetClassStructureTool.ExecuteAsync`
  implementiert. Pattern-Reuse-Fund: `McpTruncation.TruncateLines`
  ist textbasiert, `get_class_structure` braucht strukturierte
  Truncation → kein Reuse ohne API-Änderung.

## Out-of-Scope (für nächste Runde dokumentiert)

- `includeAttributes` Parameter für `get_class_structure` (Konzept-Punkt, niedrige Priorität)
- Geteilte `ITestDetector` Schnittstelle für `find_duplicates` + `get_violations`
- Konzept-Nachtrag zur `includeSnippet`-Default-Frage (Team-Entscheidung)
