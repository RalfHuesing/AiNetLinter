---
status: draft
type: konzept
project_kind: brownfield
estimated_scope: large
rules_dir: .agents/rules
last_updated: 2026-08-05
open_questions:
  - "P1 Razor-Fix: volle Source-Generator-Integration in den MSBuildWorkspace-Load, oder pragmatischer Workaround (Rausch-Hinweis gezielt unterdruecken/praezisieren, echte Generator-Integration als Tech-Debt verschieben)?"
  - "Task-Zuschnitt: alle 6 Muss-Haben-Punkte (P1-P3) in einem Task, oder Priorisierung/Aufteilung in mehrere Tasks (z. B. P1 zuerst, P2/P3 als Folge-Task)?"
  - "Verifikation von P1 (Blazor-Partials): Zugriff auf San.smart.Planner.Platform fuer manuellen Vergleich vorhanden, oder soll eine synthetische .razor/.razor.cs-Fixture in src/AiNetLinter.Tests entstehen?"
  - "Non-Goals: gibt es Punkte aus dem urspruenglichen Bug-Report, die bewusst NICHT in diesem Task behoben werden sollen?"
audience: implementierender-agent
solution_referenz: San.smart.Planner.Platform.slnx
config_referenz: San.smart.Planner.Platform.Tests.Logic/AiNetLinter/rules/platform-default.rules.json
mcp_exe_referenz: C:\Daten\AiNetLinter-win-x64\AiNetLinter.exe
---

# Konzept: AiNetLinter MCP-Server — Bekannte Probleme beheben

## Ziel (Was)

Die beim Dogfooding des MCP-Servers (`AiNetLinter.exe --mcp-server`) gegen die
San.smart.Planner.Platform-Solution (1835 `.cs`-Dateien) gefundenen Defekte im
eigenen AiNetLinter-Repo beheben: unzuverlässiger Symbolgraph bei Blazor-
Partials, inkonsistente Symbol-Identifikatoren über `find_references`/
`get_symbol_body`/`get_impact`, sowie mehrere kleinere Konsistenz- und
Dokumentations-Lücken einzelner Tools.

## Warum / Kontext

Ursprung ist [`Konzept.md`](Konzept.md) selbst — ursprünglich ein reiner
Bug-Report (`type: bug-report`), jetzt im Rahmen des `dev-loop`-Konzept-
Workflows in die Task-Doku-Struktur überführt. Kein synthetischer Befund,
sondern reale Reibung im Agenten-Workflow beim Einsatz gegen ein
Produktions-Repo:

- **Symbolgraph-Unzuverlässigkeit bei Blazor:** `dotnet build` ist grün,
  der MCP-interne Symbolgraph meldet aber 1322 Compile-Fehler auf
  Partial-Klassen mit `.razor`-Gegenstück — das kontaminiert praktisch
  **jeden** Tool-Call mit einem irreführenden globalen Hinweis und macht
  `find_references`/`get_type_hierarchy`/`get_impact`/Lint auf diesen
  Dateien potenziell unvollständig.
- **Gebrochener Identifikator-Roundtrip:** Der dokumentierte
  Agenten-Workflow „Skeleton lesen → ID kopieren → `find_references`
  aufrufen" schlägt fehl, weil nicht alle laut Tool-Doku unterstützten
  Identifikator-Formate in allen Tools funktionieren.
- **Kleinere Inkonsistenzen** (siehe Scope) erschweren zusätzlich robuste
  Agenten-Workflows, sind aber unabhängig von den beiden Punkten oben lösbar.

## Scope

### Muss-Haben

- **P1 — Blazor-Partials:** Razor-Source-Generator-Output beim
  Solution-Load einbeziehen, sodass der Symbolgraph mit `dotnet build`
  übereinstimmt (Tiefe abhängig von offener Frage 1).
- **P1 — Einheitlicher Symbol-Identifikator-Parser:** Alle drei
  dokumentierten Formate (qualifizierter Name, `Datei:Zeile:Spalte`,
  DocumentationCommentId) funktionieren für **dasselbe** Symbol in
  `find_references`, `get_symbol_body` **und** `get_impact` gleichermaßen.
- **P2 — `get_symbol_body`-ID-Korruption beheben:** Bei
  `Datei:Zeile:Spalte`-Identifikator muss die zurückgegebene `id:`-Zeile
  mit der von `get_file_skeleton` für dasselbe Symbol übereinstimmen
  (keine verschachtelte/doppelte Methoden-ID mehr).
- **P2 — `get_violations`-Meldung präzisieren:** siehe „Entdeckte Mängel"
  unten — kein Matching-Bug, sondern eine irreführende Meldung, die
  „keine Datei im Scope" und „Dateien im Scope, aber 0 Violations"
  vermischt.
- **P2 — Globaler Rausch-Hinweis eindämmen:** Der
  1322-Errors-Hinweis darf nicht mehr bei jedem Tool-Call auf unrelated
  Dateien erscheinen (entfällt vermutlich automatisch nach P1, siehe
  Konzept.md-Original „Erwartung").
- **P3 — `ainetlinter://overview`-Status synchronisieren:** Anzeige muss
  den tatsächlichen `ServerLoadState` widerspiegeln, auch unmittelbar nach
  Serverstart.
- **P3 — `find_references`/`get_impact` depth-Hard-Cap dokumentieren:**
  Der intransparente Cap bei 200 Treffern (`CallGraphTraversal.
  MaxRecursionNodes`) muss im Tool-Schema/-Output für den Agenten
  ersichtlich sein.

### Nice-to-Have (optional, spätere Iteration)

- **P3 — Lesbarere ID-Darstellung für explizite Interface-Implementierungen:**
  aktuell Standard-Roslyn-`#`-Encoding (kein Bug, siehe „Entdeckte Mängel"),
  aber unbequem für Agenten-Copy-Paste.

### Non-Goals (bewusst NICHT Teil davon)

- Wird in Runde 2 anhand offener Frage 4 ergänzt.

## Zielplattformen / Technischer Rahmen

.NET 9 / Roslyn (`Microsoft.CodeAnalysis`, `MSBuildWorkspace`) — bestehende
MCP-Server-Architektur unter `src/AiNetLinter/Mcp/**` und
`src/AiNetLinter/Baseline/**`. Keine neuen Frameworks/Abhängigkeiten
vorgesehen; alle Fixes bauen auf bestehenden Roslyn-APIs und dem
`ModelContextProtocol`-SDK auf, mit dem der Server bereits arbeitet.

## Verworfene Alternativen

- Noch keine besprochen — betrifft insbesondere die Lösungstiefe für P1
  (siehe offene Frage 1), wird nach der ersten Antwortrunde ergänzt.

## Wo im Projekt

**Pointer-Prinzip:** Fundstellen, keine Verhaltensbehauptungen — der
Planer im `drift-loop` prüft den dann aktuellen Code-Stand selbst nach.

- [`src/AiNetLinter/Baseline/SourceFileCatalog.cs:44-51`](../../src/AiNetLinter/Baseline/SourceFileCatalog.cs) —
  `MSBuildWorkspace.Create(...)` + `OpenSolutionAsync`: Solution-Load-Stelle,
  an der aktuell keine Razor-Source-Generator-Ausgabe einbezogen wird (P1).
- [`src/AiNetLinter/Core/LinterEngine.cs`](../../src/AiNetLinter/Core/LinterEngine.cs) —
  `CreateWorkspaceProperties()`, von `SourceFileCatalog` genutzt; relevant
  für evtl. MSBuild-Properties, die Source-Generator-Läufe beeinflussen.
- [`src/AiNetLinter/Mcp/Tools/McpCompileDiagnostics.cs`](../../src/AiNetLinter/Mcp/Tools/McpCompileDiagnostics.cs) —
  vermutliche Quelle des globalen „180 Dateien haben Compile-Fehler"-Hinweises
  (P1/P2 Rausch-Hinweis).
- [`src/AiNetLinter/Mcp/Tools/SymbolIdentifierResolver.cs`](../../src/AiNetLinter/Mcp/Tools/SymbolIdentifierResolver.cs) —
  zentrale Stelle für Positions-Parsing (`TryParsePosition`) und
  DocumentationCommentId-Resolution (`TryResolveByStableIdAsync`); aktuell
  laut Dateikommentar nur für `FindReferencesTool` ausgelagert, nicht
  einheitlich über alle Tools mit Identifikator-Input genutzt (P1).
- [`src/AiNetLinter/Mcp/Tools/GetSymbolBodyTool.cs`](../../src/AiNetLinter/Mcp/Tools/GetSymbolBodyTool.cs) —
  vermutliche Quelle der korrupten, verschachtelten DocumentationCommentId
  bei `Datei:Zeile:Spalte`-Aufruf (P2).
- [`src/AiNetLinter/Mcp/Tools/GetViolationsScanner.cs:113-121`](../../src/AiNetLinter/Mcp/Tools/GetViolationsScanner.cs)
  (`FormatReport`) vs.
  [`src/AiNetLinter/Mcp/Tools/GetHotspotsScanner.cs`](../../src/AiNetLinter/Mcp/Tools/GetHotspotsScanner.cs)
  (`CollectFiles`/`MatchesScope`) — **Fund:** `MatchesScope` ist in beiden
  Dateien nahezu identisch (case-insensitive `Contains` auf Projektname
  oder relativem Pfad). Siehe „Entdeckte Mängel" für die tatsächliche
  Ursache der im Bug-Report beobachteten Diskrepanz.
- [`src/AiNetLinter/Mcp/OverviewResourceRegistration.cs:104-112`](../../src/AiNetLinter/Mcp/OverviewResourceRegistration.cs)
  (`DescribeSolution`) — Status-Ableitung für `ainetlinter://overview`
  aus `McpCodeGraphServer.LoadState` (P3).
- [`src/AiNetLinter/Maps/Skeleton/SkeletonSyntaxWalker.cs:227`](../../src/AiNetLinter/Maps/Skeleton/SkeletonSyntaxWalker.cs)
  (`TryCreateDeclarationId`) — nutzt `DocumentationCommentId.
  CreateDeclarationId` direkt; siehe „Entdeckte Mängel" zur Einordnung.
- [`src/AiNetLinter/Mcp/Tools/CallGraphTraversal.cs:25`](../../src/AiNetLinter/Mcp/Tools/CallGraphTraversal.cs)
  (`MaxRecursionNodes = 200`) — der im Bug-Report beobachtete, im
  Tool-Schema nicht dokumentierte Hard-Cap (P3).
- [`src/AiNetLinter.Tests/Web/RazorAnalyzerTests.cs`](../../src/AiNetLinter.Tests/Web/RazorAnalyzerTests.cs) /
  `RazorAnalyzerTests.Extended.cs` — bestehende Razor-Testinfrastruktur
  deckt nur Markup-Linting ab, keine Fixture mit `.razor`+`.razor.cs`-
  Partial-Klasse für den Symbolgraph-Anwendungsfall — relevant für die
  Verifikationsfrage (offene Frage 3).
- `src/AiNetLinter.Tests/Mcp/**`, `src/AiNetLinter.Tests/Commands/Mcp*` —
  bestehende Test-Suiten-Struktur, an die neue Regressionstests je
  behobenem Punkt anknüpfen sollten (siehe Definition of Done).

## Entdeckte Mängel/Redundanzen

- **Irreführende „Keine Dateien im Scope"-Meldung in `get_violations`**
  - **Gefunden:** `GetViolationsScanner.FormatReport`
    (`src/AiNetLinter/Mcp/Tools/GetViolationsScanner.cs:118-121`) meldet
    „Keine Dateien im Scope", sobald nach dem Scope-Filter **keine
    Violations** übrig bleiben (Filter auf bereits berechneten
    `RuleViolation`s, Zeile 113-116) — unabhängig davon, ob überhaupt
    Dateien im Scope lagen.
  - **Bezug:** Kein `.agents/rules/**`-Verstoß, sondern eine irreführende
    Fehlermeldung. Der Bug-Report (P2) interpretiert den Befund als
    Matching-Inkonsistenz zu `get_hotspots` — `MatchesScope` selbst ist in
    beiden Tools aber praktisch identisch; die Diskrepanz in der
    Repro-Tabelle (`San.smart.Planner.Platform.Tests.Logic`, `Handlers`
    etc.) erklärt sich vermutlich dadurch, dass diese Scopes Dateien,
    aber zufällig keine Violations enthalten.
  - **Vorschlag:** Meldung differenzieren (z. B. „N Dateien im Scope, 0
    Violations" vs. „Keine Dateien im Scope, Filter '…' prüfen") statt
    die Matching-Logik selbst zu ändern.
  - **Entscheidung:** übernommen ins Scope (→ Muss-Haben
    „`get_violations`-Meldung präzisieren")
- **EII-Skeleton-IDs sind Standard-Roslyn-Verhalten, kein eigener Bug**
  - **Gefunden:** `SkeletonSyntaxWalker.TryCreateDeclarationId`
    (`src/AiNetLinter/Maps/Skeleton/SkeletonSyntaxWalker.cs:227`) ruft
    `DocumentationCommentId.CreateDeclarationId` direkt auf; das
    `#`-Encoding für explizite Interface-Implementierungen ist
    ECMA-335/Roslyn-Standardformat.
  - **Bezug:** Kein `.agents/rules/**`-Verstoß, kein Roundtrip-Defekt —
    `SymbolIdentifierResolver.TryResolveByStableIdAsync` vergleicht mit
    derselben API, das Format ist also intern konsistent. Reine
    Agenten-Ergonomie, keine Korrektheitsfrage.
  - **Vorschlag:** Falls gewünscht, optionale menschen-/agentenlesbare
    Zusatzdarstellung neben der Standard-ID (kein Ersatz dafür).
  - **Entscheidung:** übernommen als Nice-to-Have (→ siehe Scope)

## Wie (grober Ansatz)

Folgt nach Klärung der offenen Fragen — insbesondere Lösungstiefe für P1
(volle Source-Generator-Integration vs. pragmatischer Workaround) und
Task-Zuschnitt (offene Fragen 1 und 2).

## Definition of Done / Erfolgskriterien

- `dotnet build` weiterhin grün (0 Fehler/Warnungen); `dotnet test`
  (Volllauf) grün — Pflicht laut [`AGENTS.md`](../../AGENTS.md) §2.
- Je behobenem Muss-Haben-Punkt mindestens ein Regressionstest, angelehnt
  an bestehende Suiten unter `src/AiNetLinter.Tests/Mcp/**` bzw.
  `src/AiNetLinter.Tests/Commands/Mcp*`.
- Der „Schnell-Check nach Fix" aus dem ursprünglichen Bug-Report ist die
  konkrete Abnahme-Checkliste (gegen San.smart.Planner.Platform, sofern
  laut offener Frage 3 verfügbar — sonst gegen die neue Fixture):
  1. `get_index_scope` → kein 1322-Errors-Hinweis mehr
  2. `get_file_skeleton(SiteView.razor.cs)` → kein `CS0115`, Basisklasse
     `ComponentBase` sichtbar
  3. `find_references(id aus skeleton)` → Treffer > 0
  4. `get_violations(scopeFilter: Tests.Logic)` → nicht „Keine Dateien im
     Scope" (sofern Dateien existieren)
  5. `get_symbol_body(Datei:Zeile:Spalte)` → `id:` identisch zu
     `get_file_skeleton`
- Falls CLI-Optionen oder `rules.json`-Schema betroffen sind (aktuell
  nicht absehbar): `Docs/configuration.md`/`Docs/ROADMAP.md` aktualisieren
  (`AGENTS.md` §3).
- Commit-Konventionen aus
  [`.agents/rules/AiNetLinterRichtlinien.mdc`](../../.agents/rules/AiNetLinterRichtlinien.mdc)
  §4 eingehalten (u. a. Pflicht-Commit-Vorschlag-Block, deutsche
  Conventional Commits).

## Offene Punkte

Siehe `open_questions` im Frontmatter — vier Punkte, die die weitere
Konkretisierung (Scope-Grenzen, Lösungstiefe P1, Verifikationsstrategie)
steuern.
