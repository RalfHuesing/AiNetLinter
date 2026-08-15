---
step: step-008
type: step-plan
corrects: [step-005, step-004]
epic: EPIC-08 (Korrektur)
status: planned
---

# Step-008 Plan: Korrekturen an `get_class_structure` und `get_violations`

## Anlass

Globaler Kritiker (`task-summary.md`, Verdict `needs-correction`) hat
drei Konzept-Verstöße identifiziert. Einer davon (A: `maxMembers` fehlt
in `get_class_structure`) verletzt die Token-Budget-Garantie der
Definition of Done. Fix wird gebündelt in einem Commit, weil die drei
Punkte alle „klein + logisch zusammenhängend" sind und ein Splitting
drei Mini-Commits produzieren würde (User-Vorgabe: „mache Große Steps!
Keine Kleinen/Mini Änderungen").

## Korrekturen

### K1. `get_class_structure` — `maxMembers` Parameter + Truncation
(corrects: step-005, A)

- **Konzept-Anker:** `konzept.md` → A → „`maxMembers` (Default 50, max
  200): begrenzt die Member-Liste konsistent mit `McpTruncation`-Mechanik.
  Bei Überschreitung Truncation-Meta-Zeile mit „weitere N Member"
  Hinweis."
- **Implementierung:**
  - `FileStructureToolRegistrations.AddGetClassStructure` (Lambda-Header):
    `int maxMembers = 50` als Parameter.
  - `GetClassStructureTool.ExecuteAsync`: nimmt `maxMembers` entgegen
    (clamped auf 1..200).
  - `ExtractMembers` / SortMembers / Payload: vor dem Bau des Payloads
    die Member-Liste auf `maxMembers` trunkieren.
  - Markdown-Output: Truncation-Meta-Zeile anhängen (Pattern-Reuse:
    `McpTruncation.TruncateLines` — konvertiert Members in Strings,
    ruft `TruncateLines` auf, integriert Meta-Zeile).
  - `ClassStructurePayload`: neues Feld `TotalMemberCount` (alle Member
    vor Truncation) + `ShownMemberCount` (nach Truncation) +
    `Truncated` (bool). `Members` enthält nur die gezeigten Member.
  - Tests in `GetClassStructureToolTests.cs`:
    - Synthetische Klasse mit 60 privaten Methoden → `maxMembers=50`
      liefert 50 Member + Truncation-Meta-Zeile.
    - `maxMembers=200` (Cap-Test) wird auf 200 begrenzt auch wenn User
      1000 setzt.

### K2. `get_class_structure` — Record-Primary-Constructor-Parameter
(corrects: step-005, A)

- **Konzept-Anker:** `konzept.md` → A → Edge-Cases → „`record` mit
  Primary Constructor → Parameter des Primary Constructors als eigene
  Zeile vor den restlichen Membern."
- **Implementierung:**
  - In `ExtractMembers`: vor der normalen `GetMembers()`-Schleife prüfen
    ob `namedType.IsRecord` und ob ein implizit deklarierter
    `InstanceConstructors`-Eintrag existiert (Primary-Constructor). Für
    jeden Parameter eine `ClassStructureMemberEntry` mit
    `Kind = "PrimaryCtor-Param"`, `Name = parameterName`,
    `Visibility = "public"` (positional records), StartLine/EndLine =
    Record-Deklarations-Zeile, `Signature` =
    `param.Name : param.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)`.
  - Tests: neuer Test mit `public record Person(string FirstName, string
    LastName, int Age)` → Member-Liste enthält 3 PrimaryCtor-Param-Zeilen
    vor den sonstigen Membern (Equals, GetHashCode, etc.).

### K3. `get_violations` — `contextLines` Default 2 (statt 0)
(corrects: step-004, B)

- **Konzept-Anker:** `konzept.md` → B → Edge-Cases → „`contextLines` als
  Tool-Parameter (Default 2, max 5) — Snippet zeigt `N` Zeilen davor,
  die verletzende Zeile, `N` Zeilen danach."
- **Implementierung:**
  - `AnalysisToolRegistrations.AddGetViolations` (Lambda-Header):
    `int contextLines = 0` → `int contextLines = 2`.
  - `GetViolationsTool.cs`: 2-arg-`ExecuteAsync`-Overload (Backward-Compat
    Helper) muss analog von `0` auf `2` umgestellt werden, damit Tests,
    die diesen Helper direkt aufrufen, nicht versehentlich den alten
    Default reproduzieren.
  - **Hinweis:** `includeSnippet` bleibt Default `false` — die
    Konzept-Konsensbildung zum Default-Wert von `includeSnippet` ist
    eine separate Frage (token-schonender Default vs.
    Konzept-wörtlich). Der Befund wird in `step-review.md` als
    „Konzept vs. Implementierung dokumentiert" festgehalten; das
    Konzept wird NICHT nachgetragen, weil das eine Designentscheidung
    ist, die im Team zu treffen ist.
  - Tests: neuer Test in `GetViolationsToolTests.cs` der ohne
    `includeSnippet`-Parameter aufgerufen wird und kein Snippet
    enthält, sowie mit `includeSnippet=true` und Default
    `contextLines=2` ein Snippet mit 5 Zeilen (2 + 1 + 2) liefert.

## Doku

- `Docs/agent-api.md`:
  - `get_class_structure`: Signatur um `maxMembers` erweitern,
    `TotalMemberCount` + `Truncated` Felder im StructuredContent
    dokumentieren.
  - `get_violations`: `contextLines` Default korrigieren (2 statt 0).

## Tests-Laufzeit-Constraint

Keine neuen Integration-Tests, nur FastTests. Die existierenden
`GetClassStructureToolTests` nutzen `InMemory`-Workspaces, die unter
50 ms pro Test laufen — neue Truncation-Tests fügen < 1 s zur
FastTests-Suite hinzu (geschätzt 0.3 s pro Test × 3 neue Tests).

## Risiken

- **K1 Truncation-Implementierung:** Wenn der `Members`-Filter
  vor `RenderMarkdown` geschieht, muss `TotalMemberCount` vorab
  festgehalten werden. Kein Datenverlust, aber UI-Logik muss
  sortiert sein.
- **K2 Record-Constructor-Erkennung:** Roslyn-API für „Primary vs.
  explizit deklarierter Constructor" ist subtil. Mitigation:
  Defensiv — wenn die Heuristik nicht greift, einfach kein
  PrimaryCtor-Param-Block ausgeben (kein Crash, kein irreführender
  Output).
- **K3 Default-Änderung:** Bestehende Aufrufer, die `contextLines=0`
  explizit gesetzt haben, sind nicht betroffen (Parameter wird
  ohnehin übergeben). Aufrufer, die den Default genutzt haben, sehen
  jetzt bei `includeSnippet=true` ein Snippet mit 5 Zeilen statt
  1 Zeile. Token-Impact: 4 zusätzliche Zeilen × ~80 Zeichen = ~320
  Bytes pro Violation. Bei 50 Violations: ~16 KB zusätzlich. Liegt
  im Korridor der 50 KB Token-Budget-Garantie.

## Definition of Done

- [ ] K1, K2, K3 implementiert in EINEM Commit.
- [ ] `dotnet build` grün, 0 Warnungen.
- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` grün,
      < 20 s Gesamtlaufzeit (1345 + ~3 neue Tests).
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` grün,
      < 2m 30s Gesamtlaufzeit (keine neuen Integration-Tests).
- [ ] `Docs/agent-api.md` aktualisiert.
- [ ] Konzept bleibt unverändert; die Default-Wert-Diskrepanz für
      `includeSnippet` wird in `step-review.md` dokumentiert.
- [ ] `task-summary.md` wird aktualisiert (Verdict `completed`).

## Out-of-Scope

- `includeAttributes` für `get_class_structure` (Konzept-Punkt, aber
  Nice-to-Have).
- Geteilte `ITestDetector`-Schnittstelle für `find_duplicates` +
  `get_violations` (Tech-Debt-Übergabe an nächste Runde).
- Konzept-Nachtrag zur `includeSnippet`-Default-Frage (Team-Entscheidung).
