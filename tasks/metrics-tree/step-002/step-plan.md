---
status: done
type: step-plan
task: metrics-tree
step: 002
corrects: step-001
title: "Korrektur: MaxMethodParameterCount in MetricsTreeScanner/-Tool + TD-002 (WalkedFile-Extraktion)"
epic: EPIC-01
estimated_risk: low
step_type: batch
items:
  - id: item-01
    title: "MetricsTreeScanner.BuildTree: Parameter in MetricsTreeQuery-Record buendeln (Finding 1)"
    source: "tasks/metrics-tree/step-001/step-review.md#Findings Finding 1"
  - id: item-02
    title: "MetricsTreeTool.ExecuteAsync: Parameter in eigenen MetricsTreeToolArgs-Record buendeln (Finding 2)"
    source: "tasks/metrics-tree/step-001/step-review.md#Findings Finding 2"
  - id: item-03
    title: "TD-002: WalkedFile aus SolutionFileWalker.cs in eigene Datei extrahieren (BanPublicNestedTypes)"
    source: "tasks/metrics-tree/tech-debt.md#TD-002"
created_by: planer
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-08
related_to: [tasks/metrics-tree/step-001/step-review.md]
---

# Step 002: Korrektur MaxMethodParameterCount (metrics_tree) + TD-002 (EPIC-01)

## Bezug

- **Task:** `metrics-tree`
- **Epic:** `EPIC-01` (übernommen vom korrigierten Step `step-001`)
- **Korrigiert:** `step-001` — Kritiker-Verdict `issues`, 2 MAJOR-Findings
  (`MaxMethodParameterCount`-Verstöße in `MetricsTreeScanner.BuildTree` und
  `MetricsTreeTool.ExecuteAsync`), siehe `step-001/step-review.md`.
- **Konzept-Referenz:** keine erneute Prüfung — Fix-Modus, Scope ist exakt
  auf die zwei Findings + das opportunistisch angehängte `TD-002` begrenzt
  (siehe `SKILL.md` §Fix-Modus).

## Aktueller Projektzustand (JIT-Kontext)

Aus `step-review.md` Findings + eigener Sichtung des tatsächlichen Codes
(nicht nur des Plan-Pseudocodes aus `step-001/step-plan.md`, der sich in
Details bereits vom umgesetzten Code unterscheidet):

- `src/AiNetLinter/Mcp/Tools/MetricsTreeScanner.cs:21-22` — `BuildTree(Solution
  solution, string? root, MetricsTreeMode mode, int depth, int topN, Regex?
  fileFilter)`, 6 Parameter. Referenziert intern `root`/`mode`/`depth`/`topN`/
  `fileFilter` nur innerhalb dieser einen Methode (nicht in `NormalizeRoot`
  o. ä. — die nehmen bereits reduzierte Einzelwerte).
- `src/AiNetLinter/Mcp/Tools/MetricsTreeTool.cs:23-25` — `ExecuteAsync(
  McpCodeGraphServer state, string? root, string mode, int depth, int topN,
  string? fileFilter, CancellationToken ct)`, 7 Parameter (6 gewertet,
  `CancellationToken` ausgenommen laut Grenzwert-Definition). `mode`/
  `fileFilter` sind hier noch **roh** (ungeparste Strings) — werden erst
  innerhalb der Methode zu `MetricsTreeMode`/`Regex?` validiert.
- `src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs:120-140`
  (`AddMetricsTree`) — Registrierungs-Lambda mit 5 benannten Einzelparametern
  (`root`, `mode`, `depth = 1`, `topN = 10`, `fileFilter = null`) fürs
  MCP-Tool-Schema, ruft `MetricsTreeTool.ExecuteAsync` zweimal auf (CallLog-
  Zweig + Direkt-Zweig) mit denselben sechs Einzelwerten.
- `src/AiNetLinter/Mcp/Tools/SolutionFileWalker.cs:23` — `internal readonly
  record struct WalkedFile(string RelativePath, string AbsolutePath)`,
  genestet in der `SolutionFileWalker`-Klasse. Referenziert (qualifiziert als
  `SolutionFileWalker.WalkedFile`) nur an zwei Stellen in
  `MetricsTreeScanner.cs:61,68`. Keine Testdatei referenziert `WalkedFile`
  direkt (geprüft: keine Treffer in `src/AiNetLinter.Tests/**`) — rein
  mechanische Verschiebung ohne Testanpassung.
- Bereits vorhandenes Projekt-Muster: `MetricsTreeScanner.cs` selbst nutzt
  bereits `private sealed record FileMetric(...)`/`BuilderNode(...)` als
  **private** (nicht internal) genestete Records für interne Zwischenwerte —
  das ist laut `TD-002`-Befund unproblematisch (`BanPublicNestedTypes` griff
  nur bei `WalkedFile`, weil `internal`, nicht bei den privaten Records).
  Die neuen Parameter-Records dieses Steps sind aber **internal** (werden
  klassenübergreifend zwischen `MetricsTreeTool` und `MetricsTreeScanner`
  bzw. innerhalb der Registrierung gebraucht) — müssen also, um denselben
  Fehler nicht sofort neu einzuführen, **auf Namespace-Ebene** deklariert
  werden (nicht genestet in der jeweiligen `static class`).

## Intention

Nach diesem Step sind beide `MaxMethodParameterCount`-Verstöße behoben
(je ein Parameter-Record pro betroffener Methode, analog zur im Rules-Ref
vorgegebenen Lösung „record als Parameter-Object"), ohne dabei denselben
`BanPublicNestedTypes`-Fehler neu einzuführen, den `TD-002` gerade behebt.
Zusätzlich ist `WalkedFile` aus `SolutionFileWalker.cs` in eine eigene Datei
extrahiert (TD-002, opportunistisch angehängt, da exakt derselbe Dateibereich
wie Item 1/2).

## Entscheidung zu Finding 2 (Begründung)

Finding 2 nennt zwei Optionen: (a) „denselben `MetricsTreeQuery`-Record ...
für die Tool-Ebene verwenden" oder (b) einen separaten
`MetricsTreeToolArgs`-Record.

**Entscheidung: Option (b) — separater `MetricsTreeToolArgs`-Record.**

Begründung: Der in Finding 1 vorgeschlagene `MetricsTreeQuery`-Record trägt
bereits **validierte** Werte (`MetricsTreeMode Mode`, `Regex? FileFilter`).
Finding 2 beschreibt für die Tool-Ebene aber ausdrücklich **rohe, ungeparste**
Feldtypen (`string Mode`, `string? FileFilter`) — das sind zwangsläufig zwei
unterschiedliche Typsignaturen, „denselben Record" ist damit wörtlich nicht
umsetzbar, ohne entweder (i) `MetricsTreeQuery` selbst auf rohe Typen
zurückzustufen (würde die Validierungs-Klarheit aus Finding 1 zunichtemachen —
`MetricsTreeScanner.BuildTree` müsste dann selbst parsen/validieren, was
laut `step-001`-Musterbeschreibung explizit Aufgabe des dünnen Tool-Dispatch
ist, nicht des Scanners) oder (ii) einen `object`/generischen Ansatz zu
wählen (unnötige Komplexität für zwei Felder). Ein eigener, kleiner
`MetricsTreeToolArgs`-Record mit rohen Feldern hält die bestehende
Verantwortungstrennung (Tool validiert, Scanner bekommt bereits geprüfte
Werte) exakt so bei, wie sie in `step-001` bewusst umgesetzt wurde — kein
Bruch mit dem Ist-Zustand, keine neue Abstraktion, nur ein zusätzlicher
kleiner Typ.

## Konkrete Änderungen

### item-01: MetricsTreeScanner.BuildTree — `MetricsTreeQuery`-Record — `src/AiNetLinter/Mcp/Tools/MetricsTreeScanner.cs`

- **Was:**
  - Neuer Record auf Namespace-Ebene (nicht genestet in `MetricsTreeScanner`),
    z. B. direkt über der `MetricsTreeScanner`-Klasse in derselben Datei:
    ```csharp
    internal sealed record MetricsTreeQuery(
        string? Root, MetricsTreeMode Mode, int Depth, int TopN, Regex? FileFilter);
    ```
  - Signatur `BuildTree(Solution solution, string? root, MetricsTreeMode mode,
    int depth, int topN, Regex? fileFilter)` →
    `BuildTree(Solution solution, MetricsTreeQuery query)`.
  - Im Methodenkörper alle Verweise auf `root`/`mode`/`depth`/`topN`/
    `fileFilter` auf `query.Root`/`query.Mode`/`query.Depth`/`query.TopN`/
    `query.FileFilter` umstellen (`NormalizeRoot(query.Root)`,
    `mode == MetricsTreeMode.CodeSize` → `query.Mode == MetricsTreeMode.CodeSize`,
    `BuildNode(..., depth)` → `BuildNode(..., query.Depth)`,
    `MetricsTreeRenderer.Render(treeRoot, topN, ...)` → `..., query.TopN, ...`,
    `SolutionFileWalker.CollectFiles(solution, solutionDir, scopeFilter: null,
    fileFilter)` → `..., query.FileFilter)`).
  - Keine Verhaltensänderung — reine Signatur-/Zugriffsumstellung.
- **Warum:** Behebt Finding 1 (`MaxMethodParameterCount` 4, 6 Parameter vorher)
  exakt wie im Finding-Fix-Vorschlag beschrieben.

### item-02: MetricsTreeTool.ExecuteAsync — `MetricsTreeToolArgs`-Record — `src/AiNetLinter/Mcp/Tools/MetricsTreeTool.cs`

- **Was:**
  - Neuer Record auf Namespace-Ebene (nicht genestet in `MetricsTreeTool`),
    mit rohen, ungeparsten Werten (siehe Entscheidung oben):
    ```csharp
    internal sealed record MetricsTreeToolArgs(
        string? Root, string Mode, int Depth, int TopN, string? FileFilter);
    ```
  - Signatur `ExecuteAsync(McpCodeGraphServer state, string? root, string mode,
    int depth, int topN, string? fileFilter, CancellationToken ct)` →
    `ExecuteAsync(McpCodeGraphServer state, MetricsTreeToolArgs args,
    CancellationToken ct)`.
  - Im Methodenkörper: `MetricsTreeModeParser.TryParse(mode)` →
    `MetricsTreeModeParser.TryParse(args.Mode)`, `$"Unbekannter mode
    '{mode}'."` → `$"Unbekannter mode '{args.Mode}'."`, `depth is < 1 or > 5`
    → `args.Depth is < 1 or > 5`, `topN < 1` → `args.TopN < 1`,
    `TryBuildFileFilter(fileFilter)` → `TryBuildFileFilter(args.FileFilter)`,
    abschließender Scanner-Aufruf `MetricsTreeScanner.BuildTree(solution,
    root, parsedMode.Value, depth, topN, filterResult.Regex)` wird zu
    `MetricsTreeScanner.BuildTree(solution, new MetricsTreeQuery(args.Root,
    parsedMode.Value, args.Depth, args.TopN, filterResult.Regex))` — das ist
    die Stelle, an der aus dem rohen `MetricsTreeToolArgs` der validierte
    `MetricsTreeQuery` aus item-01 gebaut wird (identisch zu Finding 2s
    Formulierung „erst intern in den validierten Record überführen", nur mit
    zwei getrennten Record-Typen statt einem).
  - `McpDrillDownHints.Append(text, depth)` → `..., args.Depth)`.
- **Was (Aufrufstelle):** `src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs`
  (`AddMetricsTree`, Zeile ~126-133): Registrierungs-Lambda bleibt mit
  benannten Einzelparametern bestehen (MCP-Tool-Schema-Bindung, wie im
  Finding explizit vorgegeben) — nur die beiden `MetricsTreeTool.ExecuteAsync`-
  Aufrufe bauen jetzt `new MetricsTreeToolArgs(root, mode, depth, topN,
  fileFilter)` und übergeben das Objekt statt der fünf Einzelwerte:
  ```csharp
  var args = new MetricsTreeToolArgs(root, mode, depth, topN, fileFilter);
  if (callLog is null)
  {
      return await MetricsTreeTool.ExecuteAsync(mcpState, args, ct);
  }
  return await callLog.ExecuteCallAsync("metrics_tree", $"{root}|{mode}|{depth}|{topN}|{fileFilter}",
      () => MetricsTreeTool.ExecuteAsync(mcpState, args, ct));
  ```
  (Callback-String für `ExecuteCallAsync` bleibt unverändert — nutzt weiter
  die rohen Einzelwerte, nicht `args`, da das nur Logging-Text ist.)
- **Warum:** Behebt Finding 2 (`MaxMethodParameterCount`, 6 gewertete
  Parameter vorher) — Begründung für die Record-Wahl siehe „Entscheidung zu
  Finding 2" oben.

### item-03: TD-002 — WalkedFile-Extraktion — `src/AiNetLinter/Mcp/Tools/SolutionFileWalker.cs` → neue Datei

- **Was:**
  - Neue Datei `src/AiNetLinter/Mcp/Tools/WalkedFile.cs`:
    ```csharp
    #nullable enable

    namespace AiNetLinter.Mcp.Tools;

    /// <summary>Relativer und absoluter Pfad einer per Walk gefundenen Datei.</summary>
    internal readonly record struct WalkedFile(string RelativePath, string AbsolutePath);
    ```
    (XML-Doc-Kommentar 1:1 aus der bisherigen Nested-Type-Deklaration
    übernommen, keine inhaltliche Änderung.)
  - `src/AiNetLinter/Mcp/Tools/SolutionFileWalker.cs`: die genestete
    `internal readonly record struct WalkedFile(...)`-Zeile (aktuell Zeile 23,
    inkl. ihres XML-Doc-Kommentars) entfernen. Die drei Verwendungsstellen
    innerhalb der Datei (`List<WalkedFile>` Rückgabetyp, `new List<WalkedFile>()`,
    `new WalkedFile(relativePath, document.FilePath!)`) bleiben unqualifiziert
    `WalkedFile` — kompiliert unverändert, da derselbe Namespace.
  - `src/AiNetLinter/Mcp/Tools/MetricsTreeScanner.cs:61,68`: die zwei
    Parametertyp-Referenzen `SolutionFileWalker.WalkedFile f` →
    `WalkedFile f` (Qualifikation entfällt, Typ liegt jetzt direkt im
    Namespace).
- **Warum:** `TD-002` — `WalkedFile` als `internal` genesteter Typ verletzt
  `BanPublicNestedTypes` (Error-Severity im eigenen Linter). Rein mechanische
  Verschiebung ohne Verhaltensänderung, `auto_fixable: ja` laut
  `tech-debt.md`. Liegt im selben Dateibereich (`SolutionFileWalker.cs`/
  `MetricsTreeScanner.cs`), den item-01/item-02 ohnehin anfassen.

## Tests

- [ ] Keine neuen Tests — alle drei Items sind reine Signatur-/Struktur-
      Umstellungen ohne Verhaltensänderung (Parameter-Bündelung in Records,
      Typ-Verschiebung). Bestehende Tests sind die Regressionsabsicherung:
      `src/AiNetLinter.Tests/Mcp/Tools/MetricsTreeToolTests.cs` (13 Tests,
      decken `ExecuteAsync` inkl. aller Validierungspfade bereits ab —
      müssen nach der Signaturänderung unverändert grün bleiben, da nur
      interne Aufrufe/Wiring betroffen sind, keine beobachtbaren
      Rückgabewerte) und `src/AiNetLinter.Tests/Mcp/Tools/MetricsTreeRendererTests.cs`
      (unberührt von diesem Step).
- [ ] Gezielter Lauf: `dotnet test --filter "FullyQualifiedName~MetricsTree"`
      muss grün bleiben (28 Tests aus `step-001` minus die nicht
      MetricsTree-Namen — mindestens die 13+4 `MetricsTree*`-Tests).

## Definition of Done

- [ ] Alle drei Items (`item-01`, `item-02`, `item-03`) umgesetzt
- [ ] `dotnet build AiNetLinter.slnx` (TreatWarningsAsErrors) grün
- [ ] Gezielter Testlauf grün: `dotnet test --filter Category=Unit` (kein
      Volllauf — abweichendes Gate für diesen Task, siehe `roadmap.md`
      Tech-Stack-Notiz)
- [ ] Per `ainetlinter`-MCP (`get_violations`, Scope auf die 3 geänderten/
      neuen Dateien) verifiziert: beide `MaxMethodParameterCount`-Verstöße
      sowie der `BanPublicNestedTypes`-Verstoß auf `WalkedFile` sind
      verschwunden, keine neuen Verstöße eingeführt (insbesondere: die neuen
      `internal sealed record`-Typen `MetricsTreeQuery`/`MetricsTreeToolArgs`
      dürfen selbst keinen `BanPublicNestedTypes`-Verstoß auslösen — daher
      bewusst nicht genestet, siehe „Aktueller Projektzustand" oben)
- [ ] Commit auf aktuellem Branch (Conventional Commit, Deutsch, imperativ)
- [ ] `tasks/metrics-tree/step-002/step-result.md` geschrieben
- [ ] `tasks/metrics-tree/codemap.md` um die neue Datei `WalkedFile.cs`
      ergänzt (Coder-Pflicht vor dem Commit)
- [ ] `tasks/metrics-tree/tech-debt.md`: `TD-002`-Status von `offen` auf
      `erledigt (step-002)` gesetzt
- [ ] `status` in diesem `step-plan.md` von `open` über `in_progress` auf
      `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc` §„Grenzwerte" — `MaxMethodParameterCount`
  (4): „Ab Überschreitung: `record` als Parameter-Object" — exakte Vorgabe
  für item-01/item-02. Dieselbe Datei auch für `BanPublicNestedTypes`
  (item-03, TD-002-Ursache) und implizit für die neuen Records selbst
  (`sealed`-Vorgabe für konkrete Typen — bei `record`/`record struct` bereits
  automatisch erfüllt, wie in `step-001` bereits für `MetricsTreeNode`/
  `FileMetric`/`WalkedFile` gehandhabt).

## Bekannte Ausnahmen

<keine>

## Notes

- **Scope-Disziplin (Fix-Modus):** Dieser Step plant ausschließlich die zwei
  Findings aus `step-001/step-review.md` plus das opportunistisch angehängte
  `TD-002`. Die „Sonstige Beobachtungen"-Zeile im Review (unvollständige
  `AIContextFootprint`-Beobachtung im `step-result.md`) sowie `TD-001`
  (Facade-Extraktion) sind **nicht** Teil dieses Steps — `TD-001` ist
  `auto_fixable: nein` (Architektur-Ermessen), bleibt dem Nutzer vorbehalten.
- **Reihenfolge der Items:** item-01 vor item-02 umsetzen (item-02 baut den
  `MetricsTreeQuery` aus item-01 in seinem Aufruf an `MetricsTreeScanner.BuildTree`),
  item-03 ist unabhängig und kann in beliebiger Reihenfolge dazu erfolgen.
