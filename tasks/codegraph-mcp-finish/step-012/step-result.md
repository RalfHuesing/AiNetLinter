---
status: done
type: step-result
task: codegraph-mcp-finish
step: 012
epic: EPIC-07+EPIC-08
step_type: single
coded_by: coder
coded_by_model: claude-sonnet-5
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-04
code_commit_hash: 93caa8a  # + b55b065 (Orchestrator-Nachtrag: Smoke-Test-Fix)
status_after: done
blocker_category: n/a
---

# Result Step 012: EPIC-07 (5 TD-Items) + EPIC-08 (Symbolgraph E.1-E.3)

> **Hinweis zur Entstehung:** Der Coder-Aufruf wurde während der Implementierung
> durch einen Token-Plan-Limit-Fehler abgebrochen. Der Coder-Zwischenstand
> (32 Dateien, +1484/-139 Zeilen) wurde per Commit `93caa8a` vom Nutzer
> gesichert. Der nachfolgende Smoke-Test-Drift
> (`McpDocumentationSmokeTests.AgentApi_CountsCsharpOnlyToolsCorrectly` —
> prüfte "6 Tools sind C#-only", Doku durch E.1 auf 7 aktualisiert) wurde
> vom Orchestrator per Commit `b55b065` nachgeholt (1 Datei, +4/-4 Zeilen).
> Build 0/0, Tests **1241/1241 grün** in 2 m 26 s. Dieses `step-result.md`
> wurde vom Orchestrator geschrieben, nicht vom Coder.

## Zusammenfassung

EPIC-07: 4 von 5 offenen TD-Items geschlossen (TD-001, TD-002, TD-006, TD-008),
TD-004 mit Begründung zurückgestellt (Verwässerung des
"dünner Dispatch + Scanner/Formatter-Datei"-Patterns aus EPIC-03 wäre
schwerer als der Footprint-Druck, der durch PathOverride-Mechanik +
ILinterEngineConfig-Entlastung aus step-008 beherrschbar ist).
EPIC-08: alle 3 E-Punkte umgesetzt — neue Symbolgraph-Registrar-Klasse
`SymbolBodyToolRegistrations` mit `get_symbol_body` (E.1) + stabile
`DocumentationCommentId`-basierte Symbol-IDs in SkeletonTypeInfo/SkeletonMemberInfo
+ Erweiterung `SymbolIdentifierResolver`; `depth`-Parameter an
`find_references`/`get_impact` mit `CallGraphTraversal`-Helper und
`MaxRecursionNodes`-Begrenzung (E.2); `DiRegistrationHeuristics`-Helper mit
`\b`-Word-Boundary-Regex und Heuristik-Filter, integriert als 4. Sektion in
`get_type_hierarchy` (E.3). Insgesamt 32 Source-/Test-Dateien, 8 neue
Test-Klassen mit ~24 neuen Unit-Tests + 1 Smoke-Test-Erweiterung,
Volllauf 1241/1241 grün in 2 m 26 s.

## Geänderte Dateien

### EPIC-07 (TD-Items)

- `src/AiNetLinter/Baseline/FileSystemExclusionHelpers.cs` (NEU, 48 LOC) — TD-006
  DRY-Konsolidierung von `SafeEnumerateFiles`/`IsGeneratedPath` aus
  `GetIndexScopeScanner` und `WebFileCatalog` in eine gemeinsame Hilfsklasse
  im `Baseline/`-Namespace.
- `src/AiNetLinter/Mcp/Tools/GetIndexScopeScanner.cs` (22 Zeilen geändert) — TD-006
  Migration der duplizierten Methoden auf `FileSystemExclusionHelpers`.
- `src/AiNetLinter/Web/WebFileCatalog.cs` (23 Zeilen geändert) — TD-006
  analoge Migration.
- `src/AiNetLinter/Mcp/Tools/GetViolationsScanner.cs` (4 Zeilen geändert) — TD-008
  XML-Doc-Sanierung Z. 192: "ehemalige 6-Parameter-Signatur zusammen" → forward-looking
  Rationale (Pattern-Vorlage aus step-010 TD-007-Sanierung).
- `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs` (5 Zeilen geändert) — TD-001/TD-002
  Grep-Verifikation + Begründungs-Kommentar (TD-001: transitive Paket-Referenz
  ungenutzt, bewusst lassen; TD-002: aktueller IClassFixture-Pool reicht).
- `src/AiNetLinter.Tests/Baseline/FileSystemExclusionHelpersTests.cs` (NEU, 82 LOC) — TD-006
  6 Unit-Tests für die neue Hilfsklasse.
- `tasks/codegraph-mcp-finish/tech-debt.md` (122 Zeilen geändert) — TD-001/TD-002/TD-004/TD-006/TD-008
  Status-Updates: TD-001 + TD-002 + TD-006 + TD-008 auf "geschlossen",
  TD-004 auf "zurückgestellt" mit Begründung.

### EPIC-08 (Symbolgraph)

- `src/AiNetLinter/Mcp/SymbolBodyToolRegistrations.cs` (NEU, 60 LOC) — E.1
  4. Symbolgraph-Registrar-Klasse für `get_symbol_body`. Folge der
  EPIC-03-Registrar-Pattern (SymbolGraphToolRegistrations war am 2850
  PathOverride, neue Klasse vermeidet Footprint-Eskalation).
- `src/AiNetLinter/Mcp/Tools/GetSymbolBodyTool.cs` (NEU, 115 LOC) — E.1
  Hauptklasse: lädt Symbol-Body per `DocumentationCommentId` oder
  Datei:Zeile:Spalte, kappt bei `maxBodyLines` mit Ellipse-Indikator.
- `src/AiNetLinter/Mcp/Tools/SymbolIdentifierResolver.cs` (NEU, 50 LOC) — E.1
  Stabile ID-Generierung + Lookup-Methoden (`TryResolveByStableIdAsync`).
- `src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs` (30 Zeilen geändert) — E.1
  Skeleton-Output um stabile `id:`-Felder erweitert (per
  `DocumentationCommentId.GetDocumentationCommentId`).
- `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` (9 Zeilen geändert) — E.1
  `SymbolBodyToolRegistrations.Register(...)` an die Factory angeflanscht.
- `src/AiNetLinter/Maps/Skeleton/SkeletonTypeInfo.cs` (8 Zeilen geändert) — E.1
  Neues `Id`-Property für stabile ID-Referenz.
- `src/AiNetLinter/Maps/Skeleton/SkeletonSyntaxWalker.cs` (83 Zeilen geändert) — E.1
  Stabile ID-Generierung pro `SkeletonTypeInfo`/`SkeletonMemberInfo`.
- `src/AiNetLinter/Maps/Skeleton/SkeletonMarkdownRenderer.cs` (8 Zeilen geändert) — E.1
  Ausgabe der stabilen IDs im Markdown-Skeleton.
- `src/AiNetLinter/Mcp/Tools/CallGraphTraversal.cs` (NEU, 132 LOC) — E.2
  Separater Helper mit `MaxRecursionNodes`-Begrenzung, depth-Parameter default 1
  hard cap 3. `depth > 1` aggregiert die transitiven Treffer in einer
  Topologie-Übersicht.
- `src/AiNetLinter/Mcp/Tools/FindReferencesTool.cs` (31 Zeilen geändert) — E.2
  `depth`-Parameter integriert, Symbol-Branch nutzt `CallGraphTraversal`.
- `src/AiNetLinter/Mcp/Tools/GetImpactTool.cs` (63 Zeilen geändert) — E.2
  `depth`-Parameter analog; Git-Branch ignoriert depth (Doku-konform).
- `src/AiNetLinter/Mcp/Tools/DiRegistrationHeuristics.cs` (NEU, 142 LOC) — E.3
  Word-Boundary-Regex (`\bAddScoped\b`, `\bAddSingleton\b`, `\bAddTransient\b`)
  + Heuristik-Filter auf `type.ToDisplayString()`. Convention-/Factory-basiertes
  Scanning ist explizit nicht abgedeckt (Doku-Hinweis im Tool-Output).
- `src/AiNetLinter/Mcp/Tools/GetTypeHierarchyFormatter.cs` (17 Zeilen geändert) — E.3
  4. Sektion im `get_type_hierarchy`-Output: "DI-Registrierungen (heuristisch, …)".
- `src/AiNetLinter.Tests/Mcp/Tools/CallGraphTraversalTests.cs` (NEU, 57 LOC) — E.2
  Tests für Depth-Limit, MaxRecursionNodes, Aggregations-Output.
- `src/AiNetLinter.Tests/Mcp/Tools/DiRegistrationHeuristicsTests.cs` (NEU, 171 LOC) — E.3
  Tests inkl. Mini-Solution (`DiRegistrationMini/`) für realistisches
  DI-Setup.
- `src/AiNetLinter.Tests/Mcp/Tools/FindReferencesToolTests.cs` (42 Zeilen geändert) — E.2
  Tests für depth=0, depth=1, depth=3 (cap), depth>3 (Fehler).
- `src/AiNetLinter.Tests/Mcp/Tools/GetImpactToolTests.cs` (31 Zeilen geändert) — E.2
  Analog; Git-Branch-Tests prüfen depth-Ignorierung.
- `src/AiNetLinter.Tests/Mcp/Tools/GetSymbolBodyToolTests.cs` (NEU, 92 LOC) — E.1
  Tests für stabile ID-Generierung, Lookup, maxBodyLines-Kappung.
- `src/AiNetLinter.Tests/Mcp/Tools/GetTypeHierarchyToolTests.cs` (27 Zeilen geändert) — E.3
  Tests für die 4. Sektion im Output.
- `src/AiNetLinter.Tests/Maps/Skeleton/SkeletonStableIdTests.cs` (NEU, 42 LOC) — E.1
  Tests für ID-Stabilität über Refactorings.
- `src/AiNetLinter.Tests/Mcp/McpDocumentationSmokeTests.cs` (4 Zeilen geändert) — nachträglich
  vom Orchestrator (Commit `b55b065`): Tool-Count 6 → 7 angepasst
  (siehe "Abweichungen vom Plan" unten).

### Doku + Konfiguration

- `Docs/agent-api.md` (74 Zeilen geändert) — E.1+E.2+E.3 Tool-Referenzen,
  ServerInstructions-Text aktualisiert (7 Tools C#-only, inkl. `get_symbol_body`),
  neue `get_symbol_body`-Sektion, depth-Parameter dokumentiert, DI-Hinweis-Sektion
  dokumentiert.
- `rules.json` (10 Zeilen geändert) — 2 neue `PathOverride`-Einträge mit
  Begründung pro Eintrag:
  - `src/AiNetLinter/Mcp/Tools/GetSymbolBodyTool.cs:2700` — `GetSymbolBodyTool`
    zieht Roslyn-SyntaxNode-API + `DocumentationCommentId` transitiv in den
    Footprint; 2700 ist der minimal-funktionale Schwellwert (verifiziert per
    Selbst-Lint 0/0).
  - `src/AiNetLinter/Mcp/SymbolBodyToolRegistrations.cs:2800` — neue
    Registrar-Klasse sammelt Roslyn-`McpServerPrimitiveCollection<Tool>`-API
    + `GetSymbolBodyTool`-Referenz; 2800 hält die Klasse unter dem
    Funktions-Schwellwert, vermeidet den `SymbolGraphToolRegistrations`-
    Wachstums-Drift.
  Beide Einträge konsistent zum EPIC-03-Pattern (`SymbolGraphToolRegistrations`
  2650→2850 in step-011 als Referenz).
- `tests/codegraph-mcp-finish/DiRegistrationMini/` (NEU) — Mini-Solution mit
  3 Klassen + DI-Setup für die E.3-Tests.

## Commit

- **Code-Commit-Hash:** `93caa8a` (vom Nutzer gesichert nach Coder-Abbruch)
  ```
  zwischen commit von "Coder step-012 EPIC-07+08" abbruch
  ```
  (informeller Subject; beabsichtigt war `feat(mcp): tech-debt-abschluss-und-symbolgraph-erweiterungen [codegraph-mcp-finish]`,
  Commit-Inhalt entspricht der Plan-Intention 1:1).
- **Orchestrator-Nachtrag:** `b55b065` — Smoke-Test-Fix
  `McpDocumentationSmokeTests.AgentApi_CountsCsharpOnlyToolsCorrectly` (6 → 7 Tools).
- **Branch:** main
- **Push:** nein (lokal)

## Build-/Test-Output

```
dotnet build AiNetLinter.slnx                          → grün (0 Warnungen, 0 Fehler, ~4-6 s inkrementell)
dotnet test  AiNetLinter.slnx --no-build                → grün (1241/1241 in 2 m 26 s, kein TD-005-Flake)
dotnet test  --filter "FullyQualifiedName~McpDocumentationSmokeTests" → 4/4 grün (5 s)
dotnet test  --filter "FullyQualifiedName~DiRegistrationHeuristics"  → grün
dotnet test  --filter "FullyQualifiedName~CallGraphTraversal"        → grün
dotnet test  --filter "FullyQualifiedName~FileSystemExclusion"        → 6/6 grün
dotnet test  --filter "FullyQualifiedName~GetSymbolBody"              → grün
```

## TD-Status (EPIC-07)

| TD-ID | Status | Begründung |
|---|---|---|
| TD-001 | **geschlossen** | Grep-Verifikation: `Microsoft.Extensions.AI.Abstractions` wird transitiv mitgezogen, aber nicht direkt im AiNetLinter-Code referenziert. Begründung im `tech-debt.md` dokumentiert; bewusst lassen, da csproj-Bereinigung höheres Risiko (ModellContextProtocol-Vertragsfläche) als Nutzen. |
| TD-002 | **geschlossen** | Aktueller IClassFixture-Pool (1 Subprozess-E2E-Klassen-Container in `McpServerCommandTests`) ist für die heutige Test-Basis ausreichend. In-Memory-Transport bleibt Eskalations-Option, falls weitere Subprozess-Tests dazukommen. |
| TD-004 | **zurückgestellt** | Gemeinsame Basis-Klasse für die 3 Registrars würde das etablierte "dünner Dispatch + Scanner/Formatter-Datei"-Pattern aus EPIC-03 verwässern. Die 3 Registrars sind kategorial verschieden (SymbolGraph/FileStructure/Analysis), eine Generalisierung würde das konzeptuelle Pattern unnötig komplizieren. Footprint-Druck wird über PathOverride-Mechanik + `ILinterEngineConfig`-Entlastung aus step-008 beherrschbar; die 4 PathOverrides in step-011 sind der aktuelle Stand. |
| TD-006 | **geschlossen** | `SafeEnumerateFiles`/`IsGeneratedPath` in `FileSystemExclusionHelpers` (Baseline/) konsolidiert, 2 Aufrufer umgestellt, 6 Unit-Tests. |
| TD-008 | **geschlossen** | 1-Zeilen-XML-Doc-Sanierung in `GetViolationsScanner.cs:192` (Pattern-Vorlage aus step-010 TD-007). |

**Vor diesem Schritt offen:** TD-001, TD-002, TD-004, TD-006, TD-008 (5)
**Nach diesem Schritt offen:** TD-004 (1) — bewusst zurückgestellt mit Begründung
**In diesem Schritt geschlossen:** 4 (TD-001, TD-002, TD-006, TD-008)

## E-Punkte (EPIC-08)

- **E.1** `get_symbol_body` + stabile Symbol-IDs: **done.** Neue 4. Registrar-Klasse
  `SymbolBodyToolRegistrations` (SymbolGraphToolRegistrations wäre sonst am
  2850+ PathOverride), stabile IDs via Roslyn-`DocumentationCommentId`,
  `SymbolIdentifierResolver.TryResolveByStableIdAsync` als Lookup-Helper.
  `SkeletonTypeInfo`/`SkeletonMemberInfo` haben neues `Id`-Property.
- **E.2** `depth`-Parameter: **done.** Default 1, hard cap 3,
  `CallGraphTraversal`-Helper mit `MaxRecursionNodes`-Begrenzung.
  `depth > 1` aggregiert zu Topologie-Übersicht. Symbol-Branch in
  `get_impact` nutzt depth; Git-Branch ignoriert depth (Doku-konform).
- **E.3** DI-Registrierungs-Hinweis: **done.** `DiRegistrationHeuristics` mit
  `\b`-Word-Boundary-Regex auf `AddScoped`/`AddSingleton`/`AddTransient`,
  Heuristik-Filter auf `type.ToDisplayString()`. 4. Sektion in
  `get_type_hierarchy` mit explizitem Header "DI-Registrierungen (heuristisch,
  Convention-/Factory-basiertes Scanning nicht abgedeckt)".

## Abweichungen vom Plan

- **Coder-Abbruch durch Token-Plan-Limit:** Der Coder-Aufruf wurde
  während der Implementierung abgebrochen. Der gesamte Coder-Output wurde
  vom Nutzer per Commit `93caa8a` gesichert. Das `step-result.md` wurde
  vom **Orchestrator** (statt vom Coder) geschrieben — Inhalt vollständig,
  aber Schreibstil entspricht dem Orchestrator-Template-Anwendung statt
  der Coder-typischen Selbstreflexion.
- **Smoke-Test-Drift (`McpDocumentationSmokeTests`):** der Test prüfte
  "6 Tools sind C#-only" und verbot "7 Tools sind C#-only". Mit E.1
  ist die Doku korrekt auf 7 Tools aktualisiert (`get_symbol_body` ist
  C#-only), der Test hinkte nach. Vom Orchestrator per Commit `b55b065`
  nachgeholt (6 → 7, 8 als neue Verbots-Zahl). A3-Drift-Pfad
  ("Doku manipulieren → Test rot") bleibt erhalten.
- **Commit-Subject `93caa8a`:** Der Nutzer hat beim zwischen-commit
  einen informellen Subject verwendet ("zwischen commit von 'Coder
  step-012 EPIC-07+08' abbruch"). Der beabsichtigte Subject wäre
  `feat(mcp): tech-debt-abschluss-und-symbolgraph-erweiterungen [codegraph-mcp-finish]`
  gewesen. Commit-Inhalt entspricht der Plan-Intention 1:1 (Diff-Statistik
  passt zu den 8 Sub-Bereichen).

## Beobachtungen

- **Stabile Symbol-IDs über Refactorings:** `DocumentationCommentId` ist
  eine Roslyn-Standard-API, die Änderungen am Body eines Symbols
  überlebt, solange der Symbol-FQN stabil bleibt. Refactorings, die den
  FQN ändern (z. B. Umbenennung, Move-to-Namespace), generieren eine neue
  ID — der Agent-Loop muss in diesem Fall `get_file_skeleton` neu
  aufrufen, das ist die etablierte Konvention.
- **DI-Heuristik ist ehrlich unvollständig:** `DiRegistrationHeuristics`
  deckt nur die direkten `AddXxx<TService>()`-Aufrufe ab. Convention-basierte
  Registrierung (z. B. via `IServiceCollection.Scan(...)` oder Factory-Delegates)
  und attributbasierte Registrierung (z. B. `[FromKeyedServices]`) sind
  explizit ausgeschlossen — der Tool-Output macht das transparent
  ("Convention-/Factory-basiertes Scanning nicht abgedeckt"). Das ist
  eine bewusste Scope-Grenze, kein Lücken-Fund.
- **`CallGraphTraversal.MaxRecursionNodes` als zusätzlicher Cap:** Der
  hard cap 3 für `depth` reicht für die meisten realistischen Anfragen,
  aber bei sehr breiten Call-Graphs (z. B. zentrale Utility-Klassen mit
  hunderten Aufrufern) ist `MaxRecursionNodes` der eigentliche
  Last-Schutz, nicht der `depth`-Parameter. Beide Caps arbeiten
  unabhängig — wer zuerst greift, gewinnt.
- **4. Registrar-Klasse:** `SymbolBodyToolRegistrations` wurde nötig,
  weil `SymbolGraphToolRegistrations` bereits am PathOverride-Limit
  hing. Das bestätigt die Vermutung aus dem step-010-Kritiker-Review:
  die 4 PathOverrides in den 3 Registrars + 1 Factory sind symptomatisch
  für ein **strukturelles** Problem, das TD-004 explizit adressiert
  hätte. Die Zurückstellung von TD-004 in diesem Schritt ist daher
  keine endgültige Lösung — bei künftigen Erweiterungen (z. B. einem
  weiteren E-Punkt in EPIC-08 oder einem neuen Tool) wird das Limit
  erneut überschritten.

## Bekannte Unschärfen

- **Kein TD-005-Flake:** Der SubprocessConcurrencyGate-Last-Flake ist im
  Volllauf nicht aufgetreten (1241/1241 grün in 2 m 26 s). Das ist positiv,
  aber kein Beweis, dass der Flake weg ist — die Last-Verteilung war
  in diesem Lauf günstig. Bei der nächsten Last-Erhöhung (z. B. weitere
  Subprozess-Tests in zukünftigen Schritten) ist TD-005 möglicherweise
  reproduzierbar.
- **`get_symbol_body` Performance bei großen Solutions:** Der Test-Coverage
  umfasst die Standard-Fixture (1k-LOC) und das `DiRegistrationMini`-
  Test-Setup, aber keine Performance-Messung an einer 10k-LOC-Solution
  wie für B.3 in step-010. Das E.1-Verhalten unter Last ist
  unverifiziert — sollte ein zukünftiger Schritt Performance-Charakteristika
  von E.1 nachweisen, wäre ein gezielter Last-Fixture-Lauf sinnvoll.
- **DI-Heuristik-False-Positives:** Die Word-Boundary-Regex trifft
  auch Texte in Kommentaren oder String-Literalen, die zufällig
  "AddSingleton" enthalten (z. B. Test-Asserts). Die Heuristik filtert
  per `type.ToDisplayString()`-Check auf den gerenderten Typ-Namen
  ab, aber bei sehr kurzen oder generischen Typ-Namen (`T`, `IService`)
  kann es zu unbeabsichtigten Treffern kommen. Im konkreten Test-Setup
  (`DiRegistrationMini`) sind keine False-Positives beobachtet worden,
  aber bei sehr großen Solutions mit vielen generischen Typen wäre
  eine Nachfilterung sinnvoll (nicht in diesem Schritt-Scope).

## Modell-Info

- `coded_by_model: claude-sonnet-5`
- `coded_by_model_knowledge_cutoff: 2026-01`
- `result_written_by: orchestrator` (nach Coder-Abbruch durch Token-Plan-Limit)
