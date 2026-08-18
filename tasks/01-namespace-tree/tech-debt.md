---
task: 01-namespace-tree
type: tech-debt-log
maintained_by: kritiker
last_updated: 2026-08-19T00:50:00+02:00
---

# Tech-Debt-Log: 01-namespace-tree

Append-only. Jeder Eintrag ist eine vom Kritiker während eines
Step-Reviews beobachtete, aber bewusst **nicht** gefixte Auffälligkeit
außerhalb des Scopes des jeweiligen Steps (Architektur, Anti-Pattern,
Duplikation, Konsistenz) — siehe `../spec.md` §8.3/§9.

## Index

| ID | Bereich / Datei | Priorität | Auto-Fixable | Kurzfassung |
|---|---|---|---|---|
| TD-001 | `src/AiNetLinter/Mcp/Tools/FileStructure/GetNamespaceTreeScanner.cs:428` | mittel | ja | Redundante private `ResolveVisibility`-Methode statt Nutzung des bestehenden `SymbolVisibilityResolver` |
| TD-002 | `src/AiNetLinter/Mcp/Tools/` | mittel | nein | Fragmentierte `TypeKind` & `IsRecord` String-Formatierungen ("Klasse", "class", "record class", etc.) über diverse MCP-Tools |
| TD-003 | `src/AiNetLinter/Mcp/Tools/` | niedrig | nein | Parallele Kind-Filter-Parser für MCP-Tool-Parameter (`kind`) ohne gemeinsame Parser-Abstraktion |
| TD-004 | `src/AiNetLinter/Mcp/Tools/FileStructure/ProjectTypeClassifier.cs:24` | mittel | nein | Heuristik-Duplikation bei Test-Projekt-Erkennung (`ProjectTypeClassifier` vs. `TestProjectDetector`) |

## Einträge

### TD-001 — Redundante private `ResolveVisibility`-Methode statt `SymbolVisibilityResolver` [Priorität: mittel] [Auto-Fixable: ja]

- **Gefunden in:** step-001 / DRY-Audit vom 2026-08-19
- **Ort:** `src/AiNetLinter/Mcp/Tools/FileStructure/GetNamespaceTreeScanner.cs:428` (Aufruf in Zeile 329), `src/AiNetLinter/Mcp/Tools/FileStructure/SymbolVisibilityResolver.cs:12` (Aufruf in Zeile 149)
- **Befund:** In Step-001 wurde `SymbolVisibilityResolver.cs` als zentraler Helper für Accessibility-to-String angelegt und in Zeile 149 auch aufgerufen. Gleichzeitig wurde in `GetNamespaceTreeScanner.cs` (Zeilen 428–440) eine identische private Hilfsmethode `ResolveVisibility(ISymbol m)` deklariert, die in Zeile 329 genutzt wird. (Zusätzlich existiert ein sehr ähnliches `GetAccessibilityString` in `DeadCodeFilters.cs:90`).
- **Warum nicht sofort gefixt:** Wurde im Step-001 Coder-Commit eingeführt und im Review übersehen.
- **Vorschlag:** Private Methode in `GetNamespaceTreeScanner.cs` entfernen und Aufruf in Zeile 329 auf `SymbolVisibilityResolver.ResolveVisibility(t)` umstellen.
- **Auto-Fixable:** ja
- **Status:** offen

### TD-002 — Fragmentierte `TypeKind` & `IsRecord` String-Formatierungen über MCP-Tools [Priorität: mittel] [Auto-Fixable: nein]

- **Gefunden in:** step-001 / DRY-Audit vom 2026-08-19
- **Ort:**
  - `src/AiNetLinter/Mcp/Tools/FileStructure/GetNamespaceTreeScanner.cs:411` (`DescribeTypeKind`)
  - `src/AiNetLinter/Mcp/Tools/FileStructure/GetClassStructureTool.cs:144` (`GetTypeKindDescription`)
  - `src/AiNetLinter/Mcp/Tools/SymbolGraph/FindSymbolTool.cs:129` (`DescribeKind`)
  - `src/AiNetLinter/Mcp/Tools/DeadCode/DeadCodeFilters.cs:77` (`GetNamedTypeKindString`)
- **Befund:** Verschiedene MCP-Tools mappen Roslyn-`TypeKind` und `IsRecord` unabhängig voneinander auf Text-Darstellungen mit leicht abweichendem Vokabular (deutsch `"Klasse"` vs. englisch `"class"`, `"record class"` vs. `"record"`, `"record struct"` etc.). Dies führt zu Redundanz und Inkonsistenzen in den Ausgaben.
- **Warum nicht sofort gefixt:** Eine Vereinheitlichung berührt mehrere Tools und deren Ausgabeverträge/Dokumentation (`Docs/agent-api.md`).
- **Vorschlag:** Zentrale Helper-Klasse für Typ-Deskriptoren (z. B. `McpTypeKindFormatter` mit standardisierten Bezeichnungen für englische/deutsche Tool-Outputs) definieren und konsolidieren.
- **Auto-Fixable:** nein
- **Status:** offen

### TD-003 — Parallele Kind-Filter-Parser für MCP-Tool-Parameter (`kind`) [Priorität: niedrig] [Auto-Fixable: nein]

- **Gefunden in:** step-001 / DRY-Audit vom 2026-08-19
- **Ort:**
  - `src/AiNetLinter/Mcp/Tools/FileStructure/GetNamespaceTreeScanner.cs:393` (`MatchesKindFilter`)
  - `src/AiNetLinter/Mcp/Tools/SymbolGraph/FindSymbolScanner.cs:111` (`FilterByKind`)
  - `src/AiNetLinter/Mcp/Tools/DeadCode/DeadCodeModels.cs:88` (`ParseKind`)
- **Befund:** Jedes Tool mit einem `kind`-Parameter parst Eingabestrings wie `"class"`, `"klasse"`, `"interface"`, `"record"`, `"struct"`, `"enum"` separat mit eigenen `switch`-Statements oder `HashSet<string> ValidKinds`.
- **Warum nicht sofort gefixt:** Die Tools unterstützen leicht unterschiedliche Teilmengen (z. B. `find_symbol` unterstützt auch `"method"`/`"property"`, während `get_namespace_tree` nur Type-Level-Kinds filtert).
- **Vorschlag:** Gemeinsamer `McpKindFilter`-Parser oder Flags-Enum, der Aliasse ("klasse"/"class", "methode"/"method") zentral normalisiert und gegen `ISymbol`/`ITypeSymbol` evaluiert.
- **Auto-Fixable:** nein
- **Status:** offen

### TD-004 — Heuristik-Duplikation bei Test-Projekt-Erkennung (`ProjectTypeClassifier` vs. `TestProjectDetector`) [Priorität: mittel] [Auto-Fixable: nein]

- **Gefunden in:** step-001 / DRY-Audit vom 2026-08-19
- **Ort:**
  - `src/AiNetLinter/Mcp/Tools/FileStructure/ProjectTypeClassifier.cs:24` (`IsTestProject`)
  - `src/AiNetLinter/Core/TestProjectDetector.cs:28` (`IsTestProject`)
- **Befund:** `ProjectTypeClassifier` baut für `get_namespace_tree` eine eigene Namens-Token-Prüfung (`.Tests`, `.FastTests`, `.IntegrationTests`, `.TestKit`, `.Specs`), anstatt den bereits in `Core` etablierten `TestProjectDetector.cs` wiederzuverwenden (der zusätzlich Metadatenreferenzen wie xunit/nunit prüft).
- **Warum nicht sofort gefixt:** `ProjectTypeClassifier` läuft im MCP-Kontext; eine Konsolidierung erfordert das Abstimmen der Token-Mengen (z. B. `.TestKit`, `.FastTests`).
- **Vorschlag:** `ProjectTypeClassifier` auf `TestProjectDetector` umstellen bzw. `TestProjectDetector` um fehlende projektspezifische Suffixe erweitern.
- **Auto-Fixable:** nein
- **Status:** offen
