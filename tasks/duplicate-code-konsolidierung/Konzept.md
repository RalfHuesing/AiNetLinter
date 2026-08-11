---
status: ready
type: konzept
project_kind: brownfield
estimated_scope: small
rules_dir: .agents/rules
last_updated: 2026-08-11
open_questions:
---

# Konzept: Echte DuplicateCode-Funde im eigenen Repo konsolidieren

## Ziel (Was)

Das gerade fertiggestellte MCP-Feature M9 (`find_duplicates`-Tool + `DuplicateCodeChecker`-
Linter-Regel, siehe `tasks/features/05-roadmap.md` §3 M9) hat beim ersten Lauf gegen das eigene
AiNetLinter-Repo 9 echte `exact`-Cluster (Jaccard-Score 1,00, also byte-für-byte identische
Methoden-Bodies bis auf Bezeichner) gefunden. Für jeden dieser 9 Cluster wird entschieden und
umgesetzt: entweder echte Konsolidierung (gemeinsame Methode/Klasse extrahieren) oder bewusste,
kurz begründete Suppression per `// ainetlinter-disable DuplicateCode`.

## Warum / Kontext

Diese Duplikate existierten größtenteils schon vor M9 — das neue Tool macht sie nur zum ersten
Mal sichtbar (Dogfooding-Erfolg: das Tool funktioniert wie vorgesehen). Sie sind bewusst NICHT
im Zuge der M9-Umsetzung selbst mitkorrigiert worden, weil das den Feature-Scope gesprengt hätte
(9 Cluster über ~18 unbeteiligte Dateien, nichts davon M9-Code) — siehe
`tasks/features/05-roadmap.md` Akzeptanzkriterien-Notiz zu M9. Stattdessen als eigener,
abgeschlossener Task hier nachgezogen.

Aktueller Nachweis (jederzeit reproduzierbar):
```bash
dotnet run --project src/AiNetLinter -c Release -- -p AiNetLinter.slnx -c rules.json
```

## Scope

### Muss-Haben

- Für jeden der 9 unten aufgeführten Cluster: Konsolidierung ODER begründete Suppression.
- Nach jeder Änderung gezielter Test-Lauf für die betroffene(n) Klasse(n) (`dotnet test
  AiNetLinter.slnx -c Release --filter "(FullyQualifiedName~<Klasse>)&Category!=Stress"`).
- Abschluss-Verifikation: `find_duplicates`/`DuplicateCodeChecker` zeigt für diese 9 Fälle keine
  offenen (weder konsolidierten noch bewusst unterdrückten) Funde mehr.
- Finaler Volllauf `dotnet test AiNetLinter.slnx -c Release --filter "Category!=Stress"` grün.

### Nice-to-Have (Zwischenspeicher — vor `status: ready` aufgelöst)

(leer — alle Punkte sind bereits Muss-Haben oder Non-Goal)

### Non-Goals (bewusst NICHT Teil davon)

- **Weitere Duplicate-Code-Suche über diese 9 Cluster hinaus** — verworfen, weil das ein neuer,
  unbegrenzter Scope wäre. Falls `find_duplicates` künftig neue Cluster findet, ist das ein
  eigener Folge-Task (idealerweise über den Drift-Audit-Skill, `.agents/skills/drift-audit/
  SKILL.md`, vor dem nächsten Epic-Abschluss).
- **`near`-/`fuzzy`-Cluster** — nicht Teil dieses Tasks (der `DuplicateCodeChecker` meldet
  ohnehin nur `exact` automatisch, siehe M9-Entscheidung; `near`/`fuzzy` bleiben bewusst
  informell/manuell über `find_duplicates` einsehbar).
- **Änderungen an `DuplicateDetectionEngine`/`DuplicateCodeChecker` selbst** — das ist M9-Code,
  bereits abgeschlossen und verifiziert, hier nur Konsument der Ergebnisse.

## Zielplattformen / Technischer Rahmen

Unverändert: .NET 10, C#, bestehende Projektstruktur (`src/AiNetLinter/`, `src/AiNetLinter.Tests/`).
Keine neuen Abhängigkeiten.

## Verworfene Alternativen

- **Alles pauschal per `// ainetlinter-disable DuplicateCode` unterdrücken:** verworfen — bei den
  zwei 3-fachen Klonen (`ResolveSeverity`, `CountLines`) ist echte Konsolidierung klar die
  bessere Lösung (drei statt eine Stelle bei künftigen Änderungen pflegen ist ein reales Risiko,
  kein Stilproblem). Pauschale Suppression würde das Tool selbst entwerten.
- **Alles pauschal konsolidieren, auch `BoolParameterChecker.CheckMethod`/`CheckConstructor`:**
  verworfen als Automatismus — die beiden Methoden arbeiten auf unterschiedlichen Roslyn-Node-
  Typen (`MethodDeclarationSyntax` vs. `ConstructorDeclarationSyntax`); eine erzwungene
  Zusammenlegung könnte den Code über ein gemeinsames Interface/Delegate verkomplizieren, ohne
  echten Wiederverwendungsgewinn. Bewusste Einzelfallprüfung statt Automatismus (siehe Cluster 3
  unten).

## Wo im Projekt

9 `exact`-Cluster (Jaccard-Score 1,00), gefunden via `find_duplicates`/`DuplicateCodeChecker`:

1. `AiNetLinter.Core.DiffImpactAnalyzer.FindGitRoot(string)` (`src/AiNetLinter/Core/
   DiffImpactAnalyzer.cs:77`) vs. `AiNetLinter.Scope.GitChangedFilesResolver.FindGitRoot(string)`
   (`src/AiNetLinter/Scope/GitChangedFilesResolver.cs:26`)
2. `AiNetLinter.Commands.SyncAgentRulesCommand.ResolveBaseDirectory(string)`
   (`src/AiNetLinter/Commands/SyncAgentRulesCommand.cs:88`) vs.
   `AiNetLinter.Generators.AgentRulesGenerator.ResolveBaseDirectory(string)`
   (`src/AiNetLinter/Generators/AgentRulesGenerator.cs:131`)
3. `AiNetLinter.Core.Checkers.BoolParameterChecker.CheckMethod(...)`
   (`src/AiNetLinter/Core/Checkers/BoolParameterChecker.cs:12`) vs. `.CheckConstructor(...)`
   (`:18`) — dieselbe Datei/Klasse, unterschiedliche Roslyn-Node-Typen
4. `AiNetLinter.Core.DiffImpactAnalyzer.FindDocumentByPath(Solution, string)`
   (`src/AiNetLinter/Core/DiffImpactAnalyzer.cs:217`) vs.
   `AiNetLinter.Core.LinterAutoFixer.FindDocumentByPath(Solution, string)`
   (`src/AiNetLinter/Core/LinterAutoFixer.cs:63`)
5. `AiNetLinter.Maps.HotspotMapBuilder.AppendSection(...)`
   (`src/AiNetLinter/Maps/HotspotMapBuilder.cs:87`) vs.
   `AiNetLinter.Mcp.Tools.FileStructure.GetHotspotsScanner.AppendSection(...)`
   (`src/AiNetLinter/Mcp/Tools/FileStructure/GetHotspotsScanner.cs:125`)
6. `AiNetLinter.Mcp.Tools.Analysis.GetViolationsScanner.ResolveSeverity(RuleViolation)`
   (`src/AiNetLinter/Mcp/Tools/Analysis/GetViolationsScanner.cs:176`) vs.
   `AiNetLinter.Mcp.Tools.MetricsTree.MetricsTreeRoslynScanner.ResolveSeverity(...)`
   (`src/AiNetLinter/Mcp/Tools/MetricsTree/MetricsTreeRoslynScanner.cs:96`) vs.
   `AiNetLinter.Mcp.Tools.Safeguard.SafeguardScanner.ResolveSeverity(...)`
   (`src/AiNetLinter/Mcp/Tools/Safeguard/SafeguardScanner.cs:290`) — **3-facher Klon**
7. `AiNetLinter.Web.CssAnalyzer.CountLines(string)` (`src/AiNetLinter/Web/CssAnalyzer.cs:137`)
   vs. `AiNetLinter.Web.JsAnalyzer.CountLines(string)` (`src/AiNetLinter/Web/JsAnalyzer.cs:195`)
   vs. `AiNetLinter.Web.RazorAnalyzer.CountLines(string)`
   (`src/AiNetLinter/Web/RazorAnalyzer.Parsing.cs:262`) — **3-facher Klon**
8. `AiNetLinter.Tests.Commands.PlaybookCheckCommandTests.FindSlnxFile()`
   (`src/AiNetLinter.Tests/Commands/PlaybookCheckCommandTests.cs:57`) vs.
   `AiNetLinter.Tests.Maps.Skeleton.SkeletonMapBuilderTests.FindSlnxFile()`
   (`src/AiNetLinter.Tests/Maps/Skeleton/SkeletonMapBuilderTests.cs:52`) — Test-Helper
9. `AiNetLinter.Tests.Core.Checkers.MaxSwitchArmsTests.CreateSemanticModel(string)` vs.
   `SwitchDispatcherDetectorTests.CreateSemanticModel(string)` vs.
   `NullCoalescingInitializerClassifierTests.CreateSemanticModel(string)`
   (`src/AiNetLinter.Tests/Core/Checkers/`) — **3-facher Test-Helper-Klon**

## Entdeckte Mängel/Redundanzen

- **Cluster 1 — `FindGitRoot` doppelt**
  - **Gefunden:** identische Implementierung in `DiffImpactAnalyzer` und
    `GitChangedFilesResolver` (Verzeichnis-Aufwärtssuche nach `.git`).
  - **Bezug:** `DuplicateCode`-Regel, `exact` (1,00).
  - **Vorschlag:** in eine gemeinsame, kleine Utility-Klasse extrahieren (z. B.
    `Core/GitRootLocator.cs`, `internal static string? FindGitRoot(string startDir)`), von
    beiden Aufrufern referenzieren.
  - **Entscheidung:** übernommen ins Scope.

- **Cluster 2 — `ResolveBaseDirectory` doppelt**
  - **Gefunden:** identisch in `SyncAgentRulesCommand` und `AgentRulesGenerator`.
  - **Bezug:** `DuplicateCode`-Regel, `exact` (1,00).
  - **Vorschlag:** in eine gemeinsame Stelle extrahieren — da `AgentRulesGenerator` bereits von
    `SyncAgentRulesCommand` genutzt wird (oder umgekehrt, im Code prüfen), am naheliegendsten als
    `internal static`-Methode direkt auf `AgentRulesGenerator` oder einer neuen kleinen Utility,
    von `SyncAgentRulesCommand` aufgerufen statt dupliziert.
  - **Entscheidung:** übernommen ins Scope.

- **Cluster 3 — `BoolParameterChecker.CheckMethod`/`CheckConstructor`**
  - **Gefunden:** strukturell identische Logik für zwei unterschiedliche Roslyn-Node-Typen
    (Methode vs. Konstruktor).
  - **Bezug:** `DuplicateCode`-Regel, `exact` (1,00).
  - **Vorschlag:** prüfen, ob eine gemeinsame private Hilfsmethode auf Basis der gemeinsamen
    Schnittmenge (`ParameterList`, `Modifiers`) beide Fälle sauber abdeckt, ohne die Lesbarkeit zu
    verschlechtern. Falls das eine unnötige Abstraktion wäre (zwei Aufrufer, klar getrennte
    Bedeutung): bewusst per `// ainetlinter-disable DuplicateCode` mit kurzer Begründung
    unterdrücken statt erzwungen zusammenlegen.
  - **Entscheidung:** übernommen ins Scope — Einzelfallprüfung, Ergebnis (Extraktion ODER
    Suppression) im Umsetzungs-Task dokumentieren.

- **Cluster 4 — `FindDocumentByPath` doppelt**
  - **Gefunden:** identisch in `DiffImpactAnalyzer` und `LinterAutoFixer`.
  - **Bezug:** `DuplicateCode`-Regel, `exact` (1,00).
  - **Vorschlag:** in eine gemeinsame Stelle extrahieren (z. B. als `internal static` Methode auf
    `DiffImpactAnalyzer`, von `LinterAutoFixer` aufgerufen — je nachdem, welche Klasse die
    "natürlichere" Heimat ist).
  - **Entscheidung:** übernommen ins Scope.

- **Cluster 5 — `AppendSection` doppelt**
  - **Gefunden:** identisch in `HotspotMapBuilder` und `GetHotspotsScanner` (leicht
    unterschiedliche generische Parameter-Typen laut Signatur — prüfen, ob eine gemeinsame
    generische Methode oder ein gemeinsames Interface für die Elemente nötig ist).
  - **Bezug:** `DuplicateCode`-Regel, `exact` (1,00).
  - **Vorschlag:** gemeinsame generische Hilfsmethode extrahieren (z. B. in einer kleinen
    `internal static class MapSectionFormatter`), falls die Elementtypen kompatibel gemacht
    werden können; sonst begründete Suppression.
  - **Entscheidung:** übernommen ins Scope.

- **Cluster 6 — `ResolveSeverity` dreifach**
  - **Gefunden:** identisch in `GetViolationsScanner`, `MetricsTreeRoslynScanner` und
    `SafeguardScanner` — alle drei lösen `RuleViolation` → Severity-String über
    `RuleRegistry.TryResolve` mit demselben Fallback auf.
  - **Bezug:** `DuplicateCode`-Regel, `exact` (1,00), höchste Priorität (3-facher Klon, zentrale
    MCP-Scanner-Logik).
  - **Vorschlag:** gemeinsame Utility-Klasse (z. B. `internal static class RuleSeverityResolver`
    in `Core/` oder `Mcp/`) mit einer Methode `ResolveSeverity(RuleViolation)`, von allen drei
    Scannern aufgerufen. Guter Kandidat für einen kleinen, risikoarmen Refactor.
  - **Entscheidung:** übernommen ins Scope, höchste Priorität.

- **Cluster 7 — `CountLines` dreifach**
  - **Gefunden:** identisch in `CssAnalyzer`, `JsAnalyzer`, `RazorAnalyzer`.
  - **Bezug:** `DuplicateCode`-Regel, `exact` (1,00), 3-facher Klon.
  - **Vorschlag:** gemeinsame Utility (z. B. `internal static class LineCounter` in `Web/`), von
    allen drei Analyzern aufgerufen.
  - **Entscheidung:** übernommen ins Scope.

- **Cluster 8 — `FindSlnxFile` (Test-Helper) doppelt**
  - **Gefunden:** identisch in `PlaybookCheckCommandTests` und `SkeletonMapBuilderTests`.
  - **Bezug:** `DuplicateCode`-Regel, `exact` (1,00).
  - **Vorschlag:** in eine gemeinsame Test-Utility-Klasse verschieben (prüfen, ob bereits eine
    `TestHelper`/`Fixtures`-Klasse existiert, die sich anbietet, statt eine neue anzulegen).
  - **Entscheidung:** übernommen ins Scope.

- **Cluster 9 — `CreateSemanticModel` (Test-Helper) dreifach**
  - **Gefunden:** identisch in `MaxSwitchArmsTests`, `SwitchDispatcherDetectorTests`,
    `NullCoalescingInitializerClassifierTests`.
  - **Bezug:** `DuplicateCode`-Regel, `exact` (1,00), 3-facher Klon.
  - **Vorschlag:** in eine gemeinsame Test-Utility verschieben (gleiche Prüfung wie Cluster 8:
    existierende Helper-Klasse wiederverwenden statt neu anlegen).
  - **Entscheidung:** übernommen ins Scope.

## Wie (grober Ansatz)

Pro Cluster (außer ggf. Cluster 3, siehe oben): gemeinsame Logik in eine neue oder bestehende
`internal static`-Methode/Klasse extrahieren, beide/alle Aufrufer darauf umstellen, Duplikat
entfernen. Reihenfolge nach Risiko/Wert: zuerst die beiden 3-fachen Klone (Cluster 6, 7 — größter
Wartungsgewinn), dann die einfachen 2-fachen Klone (1, 2, 4, 5), dann die Test-Helper (8, 9),
zuletzt die Einzelfallentscheidung (3). Nach jedem Cluster gezielter Test-Lauf, Commit pro Cluster
oder sinnvoll gebündelt (z. B. alle Test-Helper-Fälle in einem Commit).

## Definition of Done / Erfolgskriterien

- Alle 9 Cluster bearbeitet (konsolidiert oder bewusst mit Kommentar unterdrückt).
- `dotnet run --project src/AiNetLinter -c Release -- -p AiNetLinter.slnx -c rules.json` zeigt für
  diese 9 Fälle keine offenen `DuplicateCode`-Funde mehr (weder als echten Verstoß noch als
  stillschweigend ignorierten — Suppression-Fälle sind im Code sichtbar begründet).
- `dotnet test AiNetLinter.slnx -c Release --filter "Category!=Stress"` (Volllauf) grün.
- Commits im Conventional-Commit-Stil auf Deutsch, passend zur bisherigen Historie.

## Offene Punkte

(keine — `status: ready`)
