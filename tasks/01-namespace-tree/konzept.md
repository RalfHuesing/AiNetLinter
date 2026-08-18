---
status: ready
type: konzept
project_kind: brownfield
estimated_scope: medium
rules_dir: .agents/rules
last_updated: 2026-08-18T23:12:00+02:00
open_questions: []
---

# Konzept: Hierarchische Code-Exploration (`get_namespace_tree`)

## Ziel (Was)

Einführung des 23. residenten MCP-Tools `get_namespace_tree` in AiNetLinter zur hierarchischen, semantischen Exploration von C#-Codebases entlang der logischen Architektur: **Solution ➔ Projekte ➔ Namespaces ➔ Typen**.
Das Tool implementiert das Progressive-Disclosure-Prinzip, damit KI-Agenten und Entwickler ohne vorherige Kenntnis konkreter Symbol- oder Dateinamen in 1–3 zielgerichteten Zoom-Stufen die relevanten Klassen eines Features auffinden können.

## Warum / Kontext

Bisherige Tools stoßen bei der initialen Exploration oder Orientierung in großen Codebases an Grenzen:
1. `find_symbol` verlangt bereits einen Substring-Namen und liefert bei allgemeinen Begriffen (wie `"Tool"`, `"Service"`, `"Handler"`) unübersichtliche, flache Listen aus Produktions- und Testcode.
2. `metrics_tree` bildet die physische Ordnerstruktur mit Zeilenzahlen ab, nicht die logische Namespace- und Typ-Architektur.
3. `get_file_skeleton` und `get_class_structure` setzen voraus, dass man die genaue Datei oder den Typnamen bereits kennt.

`get_namespace_tree` schließt diese Lücke, spart pro Orientierungs-Phase bis zu 90% der Tokens (~100–300 statt ~2.000–4.000 Tokens) und beschleunigt den Agent-Workflow signifikant.

## Scope

### Muss-Haben

- **Neues MCP-Tool `get_namespace_tree`:**
  - Registrierung in [FileStructureToolRegistrations.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs).
  - Parameter:
    - `project` (`string?`, Default `null`): Filter auf Projektname/Substring (z. B. `"AiNetLinter"`, `"FastTests"`).
    - `namespacePrefix` (`string?`, Default `null`): Einstiegs-Namespace/Startpunkt für Drilldown (z. B. `"AiNetLinter.Mcp"` oder `"AiNetLinter.Mcp.Tools"`).
    - `depth` (`int`, Default `1`, Cap `3`): Wie viele Namespace-Ebenen ab dem Startpunkt in einem Aufruf traversiert werden. Beliebig tiefe Exploration erfolgt agentenseitig durch Fortsetzen mit `namespacePrefix`.
    - `includeTypes` (`bool`, Default `true`): Ob Typen im Ziel-Namespace ausgegeben werden oder nur die Sub-Namespace-Struktur.
    - `kind` (`string`, Default `"all"`): Filter nach Typ-Art (`class`/`klasse`, `interface`, `record`, `struct`, `enum`, `all` — case-insensitive wie in `find_symbol`).
    - `maxResults` (`int`, Default `50`, Cap `200`): Obergrenze angezeigter Einträge mit Truncation-Meta-Zeile.
- **3 Zoom-Stufen (Progressive Disclosure):**
  - **Stufe 1 (Solution-Level):** Aufruf ohne Parameter listet alle Projekte inkl. Typ- und Namespace-Zahlen sowie Projekt-Kategorie (`Exe`, `Test`, `Lib`).
  - **Stufe 2 (Projekt-Level):** Aufruf mit `project` (und optional `includeTypes=false`) liefert die strukturierte Namespace-Hierarchie des Projekts.
  - **Stufe 3 (Namespace-Level):** Aufruf mit `project` + `namespacePrefix` + `kind` listet die konkreten Typen mit Dateipfad und Deklarationszeile.
- **Synthetische Typen & Filterung:**
  - Compiler-generierte Typen (`<Program>$`, `<>c`, DisplayClasses, synthetische Record-Hilfstypen mit `CompilerGeneratedAttribute` oder Sonderzeichen) werden standardmäßig ausgeblendet.
- **Source-Only-Isolation (Roslyn):**
  - Strikte Filterung auf Quellcode-Symbole (`Locations.Any(l => l.IsInSource)` / `DeclaringSyntaxReferences.Length > 0`), um externe BCL-/NuGet-Referenzen (`System.*`, `Microsoft.*`) vollständig auszublenden.
- **Projekt-Klassifizierung:**
  - Automatische Heuristik für Projekt-Typen (`Test` via Projektname/`PathNormalizer.IsTestFile`/Testframework-Referenzen, `Exe` bei `OutputKind.ConsoleApplication`/`WindowsApplication`, sonst `Lib`).
- **Partial Types & Deklarations-Auflösung:**
  - Partial Classes werden dedupliziert; Ausgabe referenziert die primäre Deklaration (`file.cs:line`).
- **Output & StructuredContent:**
  - Formatierter, kompakter Markdown-Text mit suffizientem Navigations-Tipp für die nächste Zoom-Stufe.
  - `StructuredContent` als JSON-Objekt (`NamespaceTreePayload` mit `Project`, `NamespacePrefix`, `Depth`, `Kind`, `Namespaces`, `Types`, `TotalCount`, `ShownCount`, `Truncated`).
  - Anfügen von `McpSufficiencyHints.Append` zur Agent-Entlastung.
  - Einbettung von Diagnose-Warnungen via `McpCompileDiagnostics`, falls fehlerhafte Syntax in Dateien vorliegt.
- **Mcp-Infrastruktur-Sync:**
  - Aktualisierung von [ServerInstructions.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/ServerInstructions.cs) (Tool-Liste & Workflow "Code erkunden").
  - Aktualisierung von [OverviewResourceRegistration.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/OverviewResourceRegistration.cs).
  - Aktualisierung von [Docs/configuration.md](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/Docs/configuration.md), [Docs/integration.md](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/Docs/integration.md) und [Docs/ROADMAP.md](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/Docs/ROADMAP.md).
- **Test-Abdeckung:**
  - Mindestens 15 Unit- & Component-Tests in `AiNetLinter.FastTests` (`GetNamespaceTreeToolTests`).
  - Integration in `McpServerOptionsFactoryTests` (Tool-Count 22 ➔ 23) und `McpHandshakeToolRegistrationTests` in `AiNetLinter.IntegrationTests`.
- **Task-Abschluss & Backlog-Pflege:**
  - Kennzeichnung des Features in [tasks/features/00-uebersicht.md](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/tasks/features/00-uebersicht.md) als erledigt (Tabelle "Bereits erledigt & im Code verifiziert").
  - Kennzeichnung in [tasks/features/01-namespace-tree.md](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/tasks/features/01-namespace-tree.md) als umgesetzt.

### Nice-to-Have (Zwischenspeicher — vor `status: ready` aufgelöst)

*(Leer)*

### Non-Goals (bewusst NICHT Teil davon)

- Keine Modifikation oder Code-Generierung über das Tool (reines Read/Exploration-Tool).
- Kein physisches File-System-Browsing (dafür existiert `metrics_tree` und die IDE-Dateistruktur).
- Kein Auslesen von Methoden- oder Property-Signaturen (dafür existieren `get_file_skeleton` und `get_class_structure`).
- Keine externen DLL-Dekompilierungen oder Reflection-Lookups (reine Roslyn Compilation der Solution-Projekte).

## Zielplattformen / Technischer Rahmen

- **Target Framework:** .NET 10, C# 13, Roslyn (`Microsoft.CodeAnalysis.CSharp.Workspaces`).
- **Architektur-Vorgaben gemäß `.agents/rules/AiNetLinterRichtlinien.mdc`:**
  - Keine DI-Container, keine `AssemblyLoadContext`-Nutzung, statische Dispatch-Methoden auf `McpCodeGraphServer`.
  - Zero-Warning-Direktive (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`).
  - Fehlerbehandlung ausschließlich über `McpToolResults` (Recoverable vs. Error Policy, z. B. bei mehrdeutigen Projektnamen mit Kandidaten-Liste).
  - xUnit v3 Tests mit sauberer Kategorisierung (`Category=Unit` / `Category=Component` / `Category=Integration`).

## Verworfene Alternativen

- **`find_symbol` um Namespace-Hierarchie erweitern:** verworfen, weil `find_symbol` ein flaches Suchwerkzeug ist; eine Vermischung würde die Parameterliste überfrachten und den AIContextFootprint unnötig aufblähen.
- **Dateipfad-basierte Ordnernavigation:** verworfen, weil physische Ordnerstrukturen in C#-Projekten nicht zwingend der logischen Namespace- und Typ-Architektur entsprechen.

## Wo im Projekt

- **[FileStructureToolRegistrations.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs):** Registrierung des neuen MCP-Tools `get_namespace_tree`.
- **`src/AiNetLinter/Mcp/Tools/FileStructure/GetNamespaceTreeTool.cs` [NEU]:** Kernlogik des Tools, Argument-Validierung, Roslyn-Traversierung und Markdown-Rendering.
- **`src/AiNetLinter/Mcp/Tools/FileStructure/GetNamespaceTreeModels.cs` [NEU]:** DTOs & Records für StructuredContent-Payloads (`NamespaceTreePayload`, `ProjectSummary`, `NamespaceNode`, `TypeEntry`).
- **`src/AiNetLinter/Mcp/Tools/FileStructure/ProjectTypeClassifier.cs` [NEU]:** Wiederverwendbare, generische Klassifizierung von Projekten (`Exe`, `Test`, `Lib`) basierend auf `OutputKind`, Name und `PathNormalizer.IsTestFile`.
- **[PathNormalizer.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Output/PathNormalizer.cs):** Bereits vorhandener Helper für Test-Erkennung.
- **[ServerInstructions.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/ServerInstructions.cs):** Tool-Auflistung und Workflow-Tipps in den Server-Instruktionen.
- **[OverviewResourceRegistration.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/OverviewResourceRegistration.cs):** Tool-Metadaten für die Übersicht-Ressource.
- **`src/AiNetLinter.FastTests/Mcp/Tools/GetNamespaceTreeToolTests.cs` [NEU]:** Unit-Tests für alle 3 Stufen, Filter, Truncation und Fehlerfälle.
- **[McpServerOptionsFactoryTests.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter.FastTests/Mcp/McpServerOptionsFactoryTests.cs):** Update des erwarteten Tool-Counts von 22 auf 23.
- **[Docs/configuration.md](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/Docs/configuration.md), [Docs/integration.md](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/Docs/integration.md), [Docs/ROADMAP.md](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/Docs/ROADMAP.md):** Dokumentation und Roadmap-Aktualisierung.
- **[tasks/features/00-uebersicht.md](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/tasks/features/00-uebersicht.md), [tasks/features/01-namespace-tree.md](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/tasks/features/01-namespace-tree.md):** Backlog-Status-Update nach Fertigstellung.

## Entdeckte Mängel/Redundanzen

- **Pattern-Reuse (Test-Erkennung & Klassifizierung):**
  - **Gefunden:** `PathNormalizer.IsTestFile` und Ad-hoc-String-Vergleiche auf `"Test"` in `McpCodeGraphServerRefresh.cs`.
  - **Bezug:** DRY & Konsistenz (`.agents/rules/AiNetLinterRichtlinien.mdc` §1).
  - **Vorschlag:** Eine generische, saubere Projekt-Klassifizierungs-Komponente (`ProjectTypeClassifier`) bereitstellen, die OutputKind und `PathNormalizer.IsTestFile` zentral nutzt.
  - **Entscheidung:** übernommen ins Scope (→ siehe Muss-Haben „Projekt-Klassifizierung").
- **Pattern-Reuse (MCP-Infrastruktur):**
  - **Gefunden:** `GetClassStructureTool`, `GetHotspotsScanner`, `SolutionFileWalker` und `FindSymbolTool`.
  - **Bezug:** DRY & Konsistenz (`.agents/rules/AiNetLinterRichtlinien.mdc` §1).
  - **Vorschlag:** Einheitliche Truncation-Mechanismen (`McpTruncation`), `McpSufficiencyHints.Append`, `McpJsonOptions.Default` und `McpToolResults` strikt wiederverwenden.
  - **Entscheidung:** übernommen ins Scope.

## Edge Cases & Spezifikations-Details

1. **Externe Assembly-Referenzen ausschließen (Source-Only Isolation):**
   - Roslyn-`Compilation.GlobalNamespace` enthält alle importierten Typen (`System.*`, `Microsoft.*`).
   - Das Tool filtert Namespaces und Typen strikt darauf, ob sie im Quellcode des aktuellen Projekts deklariert sind (`Locations.Any(l => l.IsInSource)` bzw. `DeclaringSyntaxReferences.Length > 0`).
2. **Root- / Global-Namespace (`<global>`):**
   - Typen ohne Namespace-Deklaration oder Top-Level-Statements (`Program.cs`) werden unter `<global>` gelistet und sind per `namespacePrefix=""` oder `namespacePrefix="<global>"` ansteuerbar.
3. **Leere Parent-Namespaces:**
   - Namespaces, die selbst keine Typen deklarieren, aber Sub-Namespaces mit Typen enthalten (z. B. `AiNetLinter` mit 0 Typen, aber `AiNetLinter.Mcp` mit 4 Typen), werden mit `(0 Typen)` aufgeführt, damit der Pfad navigierbar bleibt. Reine leere Blätter ohne Typen werden ignoriert.
4. **Mehrdeutige oder unbekannte Projekt-Namen:**
   - Bei nicht eindeutigem Substring (z. B. `project="Tests"`) liefert das Tool `McpToolResults.Recoverable(LinterErrorCodes.AmbiguousSymbol, ...)` mit der Liste der gefundenen Projekte als Auswahlliste.
   - Bei unbekanntem Projektnamen liefert das Tool `McpToolResults.InvalidArgument` mit Liste der vorhandenen Projekte.
5. **Case-Insensitivität:**
   - Sowohl `project`, `namespacePrefix` als auch `kind` arbeiten case-insensitive.
6. **Partial Types:**
   - Partial Types werden im Typenzähler und in der Typen-Liste als ein Eintrag geführt; der Pfad zeigt auf die primäre Deklaration.
7. **Compile-Fehler-Toleranz:**
   - Befindet sich das Projekt in einem fehlerhaften Zustand, wird eine `[WARN]`-Diagnosezeile über `McpCompileDiagnostics` vorangestellt.

## Wie (grober Ansatz)

1. **Stufe 1 (Projekte):** Aus `state.GetCurrentSolution().Projects` die Projekte ermitteln, Typ via `ProjectTypeClassifier` (`Exe`, `Test`, `Lib`) bestimmen, Typ- und Namespace-Zahlen via Roslyn `Compilation.GlobalNamespace` (Source-Only) zählen.
2. **Stufe 2 (Namespaces):** Wenn `project` angegeben ist, `Project.GetCompilationAsync()` abrufen. `compilation.GlobalNamespace.GetNamespaceMembers()` bis zur angegebenen `depth` traversieren, dabei Compiler-Generierte Typen und Quellcode-fremde Namespaces ignorieren.
3. **Stufe 3 (Typen):** Für den Ziel-Namespace `INamespaceSymbol.GetTypeMembers()` auslesen, nach `kind` filtern, Deklarationsdateien und Startzeilen ermitteln, sortieren und bei Bedarf mit Truncation deckeln.
4. **Rendering:** Markdown-Renderer mit visueller Baumstruktur (Einrückungen) und Hinweistexten erstellen.
5. **FastTests:** Mit InMemory-Lösungen (`RoslynTestSolutionFactory` / Adhoc-Workspace) alle Kombinationen und Fehlerfälle testen.

## Definition of Done / Erfolgskriterien

1. `get_namespace_tree` ist als Tool #23 in MCP registriert und funktionsfähig.
2. Stufe 1 (keine Parameter): Liefert alle Projekte der Solution mit Typ-/Namespace-Zahlen und Klassifizierung (`Exe`, `Test`, `Lib`).
3. Stufe 2 (`project`): Liefert die Namespace-Hierarchie des Zielprojekts.
4. Stufe 3 (`project` + `namespacePrefix`): Liefert die Typen des Ziel-Namespaces mit Datei/Zeile.
5. Compiler-generierte / synthetische Typen sowie BCL/NuGet-Referenzen werden zuverlässig gefiltert (Source-Only).
6. `includeTypes=false` gibt ausschließlich die Namespaces ohne Typen zurück.
7. `kind`-Filter filtert zuverlässig nach `class`/`klasse`, `interface`, `record`, `struct`, `enum`, `all`.
8. `depth` beschränkt die Traversierung pro Call auf maximal 3 Ebenen (weitere Ebenen über `namespacePrefix` ansteuerbar).
9. `maxResults` trunkiert sauber und meldet `Truncated` im `StructuredContent`.
10. `StructuredContent` liefert ein valides JSON-Objekt (`NamespaceTreePayload`).
11. Mindestens 15 FastTests in `AiNetLinter.FastTests` belegen alle Funktionen.
12. `dotnet build` baut mit 0 Warnungen (`TreatWarningsAsErrors=true`).
13. `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` und `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` sind 100% grün.
14. Dokumentation in `Docs/configuration.md`, `Docs/integration.md`, `Docs/ROADMAP.md` und `README.md` ist synchronisiert.
15. Das Feature ist in `tasks/features/00-uebersicht.md` und `tasks/features/01-namespace-tree.md` als erledigt markiert.

## Offene Punkte

*(Keine)*
