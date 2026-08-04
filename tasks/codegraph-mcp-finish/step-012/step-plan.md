---
status: open
type: step-plan
task: codegraph-mcp-finish
step: 012
title: "Restliche Tech-Debt-Einträge (EPIC-07) + Symbolgraph-Erweiterungen (EPIC-08)"
epic: EPIC-07,EPIC-08
estimated_risk: medium
step_type: single
items: []  # thematisch zwei Epics, beide in einem Schritt gebündelt; keine Micro-Batch-Items.
created_by: planer
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-04
related_to:
  - step-011/step-review.md
  - step-010/step-review.md
  - step-008/step-review.md
---

# Step 012: Restliche Tech-Debt-Einträge (EPIC-07) + Symbolgraph-Erweiterungen (EPIC-08)

## Bezug

- **Task:** `codegraph-mcp-finish`
- **Epics:**
  - `EPIC-07` aus `roadmap.md` — Restliche Tech-Debt-Einträge (Konzept „Muss-Haben D",
    5 offene TD-Items: TD-001, TD-002, TD-004, TD-006, TD-008 nach Schließen
    von TD-005 + TD-007 in `step-010`).
  - `EPIC-08` aus `roadmap.md` — Symbolgraph-Erweiterungen (Konzept „Muss-Haben E",
    3 E-Punkte: E.1 `get_symbol_body` + stabile Symbol-IDs, E.2 `depth`-Parameter,
    E.3 DI-Hinweis).
- **Konzept-Referenz:** `tasks/codegraph-mcp-finish/Konzept.md`
  - „Muss-Haben D" Z. 297-336 (TD-001 bis TD-008 Volltext).
  - „Muss-Haben E" Z. 341-373 (E.1-E.3).
  - DoD Z. 654-658: „Alle drei Punkte aus Muss-Haben E sind umgesetzt, …
    TD-011 ist dabei mitgelöst (fünfte Symbolgraph-Registrar-Klasse, falls
    nötig)".
  - DoD Z. 663-665: „Alle in D gelisteten Tech-Debt-Einträge (TD-001, TD-002,
    TD-004, TD-005, TD-006, TD-007) sind entweder geschlossen oder bewusst
    mit Begründung erneut zurückgestellt" — TD-005 + TD-007 bereits in
    `step-010` geschlossen, TD-008 wird in diesem Schritt nachgeholt
    (war TD-007-Schwester-Stelle, beim Sanieren übersehen).
- **Reihenfolge-Konzept-Vorgabe:** Konzept-Vorgabe in Z. 295: EPIC-07
  (insbesondere TD-004) läuft **vor** EPIC-08 (E.1), damit das
  `get_symbol_body`-Tool der fünften Registrar-Klasse nicht gegen den
  bereits knappen Footprint kämpft. Nach `step-011` hat sich der Footprint
  durch 4 neue PathOverrides in den Registrars/Factory verschlechtert
  (SymbolGraph 2650→2850, FileStructure 2640→2810, Analysis 2640→2800,
  McpServerOptionsFactory 2640→2800) — siehe `step-011/step-review.md`
  Z. 62. EPIC-07 TD-004 klärt, ob die 4 PathOverrides strukturell
  reduzierbar sind, bevor EPIC-08 E.1 mit vierter Registrar-Klasse
  umgesetzt wird.
- **Non-Goals (Konzept Z. 457-489):** keine Editier-Tools, kein Embedding,
  kein Multi-Sprache-Support, kein Plugin/ALC/DI-Container, kein
  CLI-Batch-Mode-Replacement, keine Test-Inhalts-Änderungen außerhalb
  des Scopes.
- **Nutzer-Vorgabe 2026-08-04:** „Tech-Debt mit klarer Intention + ohne
  Nutzer-Entscheidung → mit umsetzen". Konkret: alle 5 offenen TD-Items
  (TD-001, TD-002, TD-004, TD-006, TD-008) sind im Scope und müssen
  entweder geschlossen oder bewusst mit Begründung zurückgestellt werden.

## Aktueller Projektzustand (JIT-Kontext)

Beim Code-Lesen am 2026-08-04 vorgefunden:

### EPIC-07 — TD-001 (Paket-Referenz `Microsoft.Extensions.AI.Abstractions`)

- `src/AiNetLinter/AiNetLinter.csproj:17` referenziert
  `ModelContextProtocol` Version 2.0.0 — transitiv zieht das
  `Microsoft.Extensions.AI.Abstractions` mit, ohne dass es im direkten
  Code referenziert wird (verifiziert per Grep im Projekt-Root:
  keine direkten `Microsoft.Extensions.AI.*`-Imports im
  `src/AiNetLinter/`-Baum).
- Konzept-Originalformulierung: „aktuell ungenutzt. Bei Bedarf prüfen, ob
  eine gezieltere Paket-Referenz existiert." Das ist ein **Check + ggf.
  Doku-Commit**, kein Refactor.
- **`ModelContextProtocol` 2.0.0** ist die offizielle MCP-SDK-Version
  (`ModelContextProtocol.Protocol`/`ModelContextProtocol.Server` als
  Imports in den `*Tool`/`*Registrations`-Dateien), das Abstractions-Paket
  ist eine Roslyn-SDK-Konsequenz daraus. **Kein direkter Ersatz sinnvoll**:
  der Pfad ist „transitive Abhängigkeit dokumentieren" statt „Paket
  wechseln".

### EPIC-07 — TD-002 (Subprozess-E2E-Test ohne Fixture-Pool)

- `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs` (467 Zeilen)
  ist der einzige Subprozess-E2E-Test-Container mit echten
  `AiNetLinter.exe`-Prozessen via `McpTestClient.ConnectAsync` (Retry-Loop
  seit `step-011/TD-019`).
- Fixture-Pattern: zwei `IClassFixture<>`-Felder
  (`SymbolGraphMcpFixture`, `BaselineMcpFixture`,
  `src/AiNetLinter.Tests/Fixtures/SymbolGraphMcpFixture.cs:18` und
  `BaselineMcpFixture.cs:18`) — jede Fixture startet **einen**
  `AiNetLinter.exe`-Prozess pro Test-Klasse via `IAsyncLifetime`
  (`InitializeAsync`) und disposed ihn in `DisposeAsync`. **Prozess-Wiederverwendung
  pro Test-Klasse ist bereits etabliert**, das ist nicht der Engpass.
- `McpTestClient.ConnectAsync` (Retry-Loop) mit
  `McpTestClientRetryOptions(MaxRetries: 5, BaseDelayMs: 1000, BackoffFactor: 2.0)`
  — bewährtes Pattern gegen parallele MCP-Init-Timeouts.
- `SubprocessConcurrencyGate` (`Fixtures/SubprocessConcurrencyGate.cs`,
  6 Slots, 60 s Wait-Timeout, eingeführt in `step-010`) kappt
  gleichzeitige Subprozess-Spitzen, ohne die Test-Klassen
  zwangsserialisieren zu müssen.
- `ModelContextProtocol` 2.0.0 hat einen `InMemoryTransport` (in der
  offiziellen SDK-API verfügbar) — wäre die „Fixture-Pool"-Eskalation
  **bei künftig mehr Subprozess-Tests**, ist aber für den aktuellen
  1-Klassen-Container kein zwingender Schritt.
- Konzept-Originalformulierung: „Bei **weiteren** Subprozess-Tests einen
  wiederverwendbaren Fixture-Prozess/In-Memory-Transport erwägen" — die
  „weiteren" sind der Auslöser, der aktuell nicht gegeben ist.

### EPIC-07 — TD-004 (Footprint-Druck auf 3 Tool-Registrar-Sammelklassen)

- `src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs` (160 Zeilen, 4 Tools:
  find_symbol, find_references, get_impact, get_type_hierarchy) — eigene
  PathOverride `MaxAIContextFootprint: 2850` (höchster Override-Wert im
  gesamten `Mcp/`-Modul).
- `src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs` (127 Zeilen, 3 Tools:
  get_file_skeleton, get_index_scope, get_hotspots) — PathOverride `2810`.
- `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs` (104 Zeilen, 2 Tools:
  get_violations, search_pattern) — PathOverride `2800`.
- Aufbau ist kategorial: jede Klasse bündelt thematisch verwandte Tools
  (Symbolgraph ↔ File-Struktur ↔ Analyse) und jede Klasse hat eine eigene
  `Add*`-Helper-Methode pro Tool, plus 1 `Register`-Methode als Einstieg.
  Das **Dispatcher-Pattern** ist explizit gewollt (Konzept-Muss-Haben C,
  aus `step-008` umgesetzt): `McpServerOptionsFactory` bleibt schlank,
  jede Registrar-Klasse ist einzeln unit-testbar.
- Allgemeine Basis-Klasse für die 3 Registrars würde **das Pattern
  verwässern**: die 3 Klassen unterscheiden sich in
  (a) Tool-Anzahl (2/3/4), (b) Lambda-Body (manche mit `maxResults`,
  manche ohne, manche mit `gitRef`), (c) Call-Log-Lambda-Bodies (alle
  haben das `if (callLog is null)`-Fast-Path). Eine Generalisierung
  würde den Footprint **erhöhen** statt reduzieren (virtuelle
  `BuildTool(...)`-Methode + parameter-object + Typargumente), und das
  Pattern „dünner Dispatch + Scanner/Formatter-Datei" aus TD-005 würde
  nur nominell eingehalten.
- `step-011/step-review.md` Z. 62 (Kritiker-Beobachtung): „Konzept-Punkt
  C (`ILinterEngineConfig`-Refactor) wird dringlicher, nicht weniger —
  EPIC-07 (TD-008/TD-010) hat einen zählbaren Hebel" — der zählbare
  Hebel ist genau die `ILinterEngineConfig`-Entlastung, die in
  `step-008` umgesetzt wurde. **TD-004 gehört zur gleichen Familie wie
  TD-005**: Druck auf den Registrar-Footprint ist ein
  Architektur-By-Design-Fakt, nicht ein zu behebender Defekt.

### EPIC-07 — TD-006 (`GetIndexScopeScanner`/`WebFileCatalog`-Duplikation)

- `src/AiNetLinter/Mcp/Tools/GetIndexScopeScanner.cs:78-94` dupliziert
  `SafeEnumerateFiles` (Z. 78-86) und `IsGeneratedPath` (Z. 88-94) **1:1**
  aus `src/AiNetLinter/Web/WebFileCatalog.cs:105-113` und
  `WebFileCatalog.cs:149-155`.
- Beide Methoden sind `private static`, 8 bzw. 7 Zeilen — die Duplikation
  ist mechanisch, exakt gleich, **kein** Verhaltens-Drift.
- Konzept-Vorgabe: „Bei einem weiteren Dateisystem-Scan mit ähnlichem
  Ausschlussmuster (z. B. B.3, Last-Fixture-Generierung) einmalig in eine
  gemeinsame Hilfsklasse ziehen." B.3 (`step-010`) hat bereits
  `LoadFixtureBuilder.cs` als generativen Solution-Builder gebracht, der
  aber den Roslyn-Workspace-Scan macht, keinen freien
  Dateisystem-Walk — die „weiteren Scans" sind nicht eingetreten.
- Empfohlener Zielort: `src/AiNetLinter/Baseline/FileSystemExclusionHelpers.cs`
  (neue Datei) — `Baseline/` ist der projektweite Namespace für
  Dateisystem-Kataloge (`SourceFileCatalog`, `BaselineFile`-Records,
  etc.), passt thematisch und ist `Mcp/Tools/`-neutral.
- 3 Aufrufer: `GetIndexScopeScanner` (`Mcp/Tools/`), `WebFileCatalog`
  (`Web/`) und nach Konsolidierung auch in zukünftigen Scans nutzbar.
  Konsolidierung ändert keine Aufrufer-Signaturen.

### EPIC-07 — TD-008 (verbleibende „ehemalige 6-Parameter-Signatur")

- `src/AiNetLinter/Mcp/Tools/GetViolationsScanner.cs:192` — XML-Doc am
  `GetViolationsScannerParameters`-Record enthält das Wort „ehemalige
  6-Parameter-Signatur zusammen". Semantisch identisch zu TD-001/TD-007
  (Konzept-Konzept: „war früher"-Marker im Sinne der §5-Liste in
  `AiNetLinterRichtlinien.mdc`).
- Pattern-Vorlage (aus `step-010` TD-007-Sanierung): „kapselt N
  Konfigurations-Eingaben in einem Record, damit `MaxMethodParameterCount: 4`
  eingehalten wird" statt „ehemalige N-Parameter-Signatur".
- Mechanische 1-Zeilen-Sanierung, kein Code-Logik-Impact.

### EPIC-08 — E.1 (`get_symbol_body` + stabile Symbol-IDs)

- Heutiger Stand:
  - `get_file_skeleton` (`src/AiNetLinter/Mcp/Tools/GetFileSkeletonTool.cs:22-45`)
    liefert pro Member nur Signaturen (kein `id`-Feld); basiert auf
    `SkeletonMapBuilder.ExtractFromDocumentAsync` + `SkeletonMarkdownRenderer.Render`.
  - `SkeletonTypeInfo` (`src/AiNetLinter/Maps/Skeleton/SkeletonTypeInfo.cs:7-15`)
    hat aktuell kein ID-Feld; `SkeletonMemberInfo` (Z. 17-21) ebenfalls nicht.
  - `SymbolIdentifierResolver.cs` (`src/AiNetLinter/Mcp/Tools/`)
    versteht aktuell nur `Datei:Zeile:Spalte` und qualifizierte Namen
    (TryParsePosition Z. 33-46, ResolveSymbolAtToken Z. 21-27) — keine
    ID-Auflösung.
- `Microsoft.CodeAnalysis` hat `DocumentationCommentId.CreateDeclarationId(ISymbol)`
  (statische Methode, gibt Strings wie `M:MyNs.MyClass.MyMethod(System.Int32)` zurück)
  und `SymbolFinder.GetMatchingSymbol(...)` bzw. das
  `DocumentationCommentId`-Round-Trip — das ist die Konzept-Vorgabe.
- Stabile IDs überleben Zeilenverschiebungen (sie überleben **kein**
  Refactoring, das den Symbol-FQN ändert — das ist by Design, da der FQN
  das ist, was die ID identifiziert).
- Overload-Disambiguierung: `ProcessOrder(int)` vs.
  `ProcessOrder(OrderDto)` bekommt unterschiedliche IDs über die
  Parameter-Signatur in der ID.
- Registrar-Frage: das `get_symbol_body`-Tool passt konzeptuell in
  `SymbolGraphToolRegistrations` (Symbolgraph-Tool, kein Datei-Scan),
  aber die Klasse hängt bereits bei `MaxAIContextFootprint: 2850` und
  hat 4 Tools. **TD-011 (fünftes Symbolgraph-Tool)** wird akut → eine
  eigene `SymbolBodyToolRegistrations`-Klasse ist **strukturell sauberer**
  als die existierende Klasse weiter aufzublähen.
- 4. Tool-Registrar-Klasse insgesamt (SymbolGraph, FileStructure,
  Analysis, SymbolBody), `McpServerOptionsFactory` muss die neue Klasse
  analog zu den bestehenden dreien aufrufen.

### EPIC-08 — E.2 (`depth`-Parameter an `find_references`/`get_impact`)

- Heutiger Stand:
  - `find_references` (`src/AiNetLinter/Mcp/Tools/FindReferencesTool.cs:33-65`)
    hat aktuell nur `(symbolIdentifier, maxResults, ct)`. Kein `depth`.
  - `get_impact` (`src/AiNetLinter/Mcp/Tools/GetImpactTool.cs:22-39`)
    hat aktuell `(gitRef, symbolIdentifier, maxResults, ct)`. Kein `depth`
    in beiden Branches (`ExecuteSymbolBranchAsync` Z. 41-59,
    `ExecuteGitRefBranchAsync` Z. 61-77).
  - `SymbolGraphToolRegistrations.cs:99-129` (`AddGetImpact`-Lambda) und
    `SymbolGraphToolRegistrations.cs:68-90` (`AddFindReferences`-Lambda)
    spiegeln die Signaturen.
- Konzept-Vorgabe: Default `depth = 1` (= heutiges Verhalten), fest
  verdrahtete Obergrenze (z. B. `MaxDepth = 3`), ab `depth > 1` aggregierte
  Ausgabe („37 Aufrufer in 12 Dateien, davon 9 in 3 Projekten" + Top-N),
  separates Knotenlimit unabhängig von `maxResults`.
- Rekursions-Strategie: für jeden gefundenen Call-Site-Symbol ein
  `SymbolFinder.FindReferencesAsync` (Roslyn) — exponentielles Wachstum
  ist der Grund für die harten Caps (depth ≤ 3, separates Knotenlimit).
- Aggregation-Output: bestehende `McpTruncation`-Mechanik
  (Sub-String-Match auf `"Treffer gesamt, "` aus `step-011`) kann
  wiederverwendet werden.

### EPIC-08 — E.3 (DI-Registrierungs-Hinweis in `get_type_hierarchy`)

- Heutiger Stand:
  - `get_type_hierarchy` (`src/AiNetLinter/Mcp/Tools/GetTypeHierarchyTool.cs:20-39`)
    ruft `GetTypeHierarchyFormatter.BuildHierarchyTextAsync` für die
    eigentliche Traversierung/Formatierung. Liefert 3 Sektionen
    (Basisklassen, Interfaces, abgeleitete Typen), keine DI-Info.
  - `GetTypeHierarchyFormatter.cs:27-40` baut die Sektionen via
    `FormatBaseTypes` / `FormatInterfaces` / `FormatSubtypesSectionAsync`
    — kein DI-Scan.
- Konzept-Originalformulierung: „reine Textsuche nach
  `AddScoped<IFoo`/`AddSingleton<IFoo`/`AddTransient<IFoo` als
  zusätzliche Zeile in der bestehenden `get_type_hierarchy`-Antwort,
  klar als heuristischer Fund gekennzeichnet (Factory-Registrierungen/
  Convention-based-Scanning werden bewusst nicht erkannt)".
- Konzept-Lesart: **eine Textzeile pro gefundene Registrierung** (oder
  eine konsolidierte Sektion „DI-Registrierungen (heuristisch):") am Ende
  der Antwort, **nicht** ein neues Tool. Erkannte Patterns:
  `AddScoped<TInterface>`, `AddSingleton<TInterface>`,
  `AddTransient<TInterface>` (auch ohne `<...>` wenn
  ServiceCollection-Method-Group-Pattern genutzt wird — Heuristik
  explizit dokumentieren).
- Heuristik-Scan: pro Solution-Datei `solution.Projects` →
  `document.GetTextAsync()` → `Regex`-Match (vorhandenes
  Regex-Inventar im Projekt, z. B. in `LinterEngine`/`SearchPatternTool`).
  Alternative: wiederverwendbarer
  `SearchPatternTool`/`search_pattern`-Style-Regex-Scan. Saubere
  Variante: neuer Helper in `GetTypeHierarchyFormatter` (oder
  ausgelagert in eine `DiRegistrationHeuristics`-Klasse), der nur die
  3 Patterns scannt.
- Markierung als heuristisch: Header-Zeile
  „DI-Registrierungen (heuristisch, Convention-/Factory-basiertes
  Scanning nicht abgedeckt):" vor der Liste; jeder Treffer mit
  Datei:Zeile + Snippet.

## Intention

EPIC-07 schließt die 5 noch offenen Tech-Debt-Einträge
(TD-001/002/004/006/008) aus dem Konzept-Muss-Haben-D-Block, mit dem
„zurückstellen mit Begründung"-Pfad für die Items, deren Schließen
kontraproduktiv wäre (TD-004). EPIC-08 erweitert den Symbolgraphen um
die drei vom Nutzer explizit als Muss-Haben eingestuften Punkte aus
dem ehemaligen `codegraph-mcp-next`-Backlog: ein `get_symbol_body`-Tool
mit stabilen Symbol-IDs in `get_file_skeleton` (löst TD-011 durch
vierte Registrar-Klasse mit), einen `depth`-Parameter an
`find_references`/`get_impact` mit aggregierter Ausgabe bei `depth > 1`,
und einen heuristischen DI-Registrierungs-Hinweis in
`get_type_hierarchy`. Die Konzept-Reihenfolge (EPIC-07 vor EPIC-08) wird
eingehalten, damit der durch `step-011` ohnehin gewachsene Registrar-
Footprint nicht direkt durch ein fünftes Tool verschärft wird, bevor
TD-004 (Struktur-Refactoring vs. „by design"-Begründung) geklärt ist.

## Konkrete Änderungen

### EPIC-07 — TD-001: `Microsoft.Extensions.AI.Abstractions`-Paket-Referenz

- **Datei:** `src/AiNetLinter/AiNetLinter.csproj` (Z. 17, ModelContextProtocol-
  Reference) — **kein Paket-Wechsel** (transitive Abhängigkeit bleibt).
- **Aktion:**
  1. Grep-Verifikation: bestätigen, dass `Microsoft.Extensions.AI.Abstractions`
     im `src/AiNetLinter/`-Baum **nicht** direkt verwendet wird (nur transitiv
     via `ModelContextProtocol`). Wenn doch: explizite `<PackageReference>`
     mit `PrivateAssets="all"` + `<ExcludeAssets>runtime</ExcludeAssets>`
     hinzufügen, um den transitiven Pull-in sichtbar zu machen.
  2. Falls nicht direkt verwendet (erwarteter Fall): keine
     `.csproj`-Änderung. Stattdessen im `step-012/step-result.md` (oder
     einer kurzen Notiz im Code) dokumentieren: „`ModelContextProtocol`
     2.0.0 zieht `Microsoft.Extensions.AI.Abstractions` transitiv mit;
     kein direkter Anwendungsfall im `Mcp/`-Modul. Konzept-Vorgabe
     (TD-001) ist erfüllt, kein Paket-Wechsel."
- **Warum:** Konzept-Vorgabe ist „prüfen, ob eine gezieltere Referenz
  existiert" — die Antwort ist „nein" (das Abstractions-Paket ist Teil
  der MCP-SDK-Vertragsfläche, kein ersetzbares Add-On), das wird im
  Step-Result dokumentiert. **Schließen mit Begründung**, nicht
  zurückstellen.

### EPIC-07 — TD-002: Subprozess-E2E-Test-Fixture-Pool

- **Datei:** `tasks/codegraph-mcp-finish/tech-debt.md` (TD-002-Eintrag).
- **Aktion:** TD-002-Eintrag auf **„geschlossen"** setzen mit
  Begründung: „Bestandsaufnahme im step-012 zeigt: das aktuelle
  `IClassFixture<>`-Pattern in `McpServerCommandTests.cs` startet pro
  Test-Klasse bereits einen geteilten `AiNetLinter.exe`-Prozess pro
  Workspace (SymbolGraph + Baseline); `SubprocessConcurrencyGate`
  (`step-010`) kappt Spitzenlast auf 6 Slots, der
  `McpTestClient.ConnectAsync`-Retry-Loop (`step-011/TD-019`) absorbiert
  parallele Init-Flakes. Konzept-Vorgabe war „bei **weiteren**
  Subprozess-Tests" — der Auslöser ist nicht gegeben, das
  `InMemoryTransport`-Pattern bleibt als Eskalations-Option für
  zukünftige Test-Erweiterungen dokumentiert (kein Refactor)."
- **Kein Code-Change am Test- oder Produktions-Code.**
- **Warum:** Konzept-Originalformulierung enthält das Wort „bei
  weiteren", das ist explizit konditional — bei der aktuellen 1-Klassen-
  Container-Situation ist der bestehende `IClassFixture<>`-Ansatz
  angemessen. **Schließen mit Begründung**, nicht zurückstellen.

### EPIC-07 — TD-004: Footprint-Druck auf 3 Tool-Registrar-Klassen

- **Datei:** `tasks/codegraph-mcp-finish/tech-debt.md` (TD-004-Eintrag).
- **Aktion:** TD-004-Eintrag auf **„zurückgestellt"** setzen mit
  ausführlicher Begründung im Eintrag selbst: „Die 3 Registrar-Klassen
  (`SymbolGraphToolRegistrations` 160 Z. / 4 Tools,
  `FileStructureToolRegistrations` 127 Z. / 3 Tools,
  `AnalysisToolRegistrations` 104 Z. / 2 Tools) sind kategorial
  verschieden — eine gemeinsame Basis-Klasse würde das etablierte
  Pattern (dünner Dispatch + Scanner/Formatter-Datei, Konzept
  Muss-Haben C) verwässern, den Footprint durch virtuelle
  `BuildTool(...)`-Helfer **erhöhen** statt reduzieren und die
  eigenständige Unit-Testbarkeit jeder Klasse (siehe
  `SymbolGraphToolRegistrations`-Tests in `McpServerCommandTests.cs`)
  einschränken. Der Footprint-Druck ist mit der PathOverride-Mechanik
  (`rules.json → PathOverrides`, 4 Einträge aus `step-011`,
  12 Einträge aus `step-008`/`step-010`) beherrschbar. Die
  `ILinterEngineConfig`-Entlastung in `step-008` hat den strukturell
  erreichbaren Hebel bereits gehoben; ein verbleibender
  Symbol-Graph-Lookup-bedingter Overhead (z. B. in `FindSymbolTool.cs`
  bei 2690) gehört zu EPIC-08 E.1, nicht in TD-004. Eine künftige
  Eskalation (PathOverride Nr. 14/15/16 statt Ursachenbehebung) wird
  über das Review-Protokoll sichtbar, nicht über TD-004-Sprints."
- **Kein Code-Change am Produktions- oder Test-Code.**
- **Warum:** Die `step-011`/`step-008`/`step-010`-Pfade haben gezeigt,
  dass der Footprint-Druck systematisch über `ILinterEngineConfig`
  (C-Block) und PathOverride-Mechanik adressierbar ist. Eine
  Generalisierung der 3 Registrars wäre eine **Verschlechterung** der
  Architektur (Konzept-Original: „etabliertes Gegenmuster") und gehört
  genau in die Kategorie, die laut Konzept-Original nicht
  strukturell refaktoriert werden soll. **Zurückstellen mit
  Begründung**.

### EPIC-07 — TD-006: `SafeEnumerateFiles`/`IsGeneratedPath`-Konsolidierung

- **Datei 1 (neu):** `src/AiNetLinter/Baseline/FileSystemExclusionHelpers.cs`
  — `internal static class FileSystemExclusionHelpers` mit 2
  `internal static`-Methoden:
  - `IEnumerable<string> SafeEnumerateFiles(string directory)`
  - `bool IsGeneratedPath(string path)`
  - Inhalt 1:1 aus den Duplikaten (`GetIndexScopeScanner.cs:78-94` ist
    die „kanonische" Variante, weil sie beide Methoden zusammen in einer
    Datei hat; `WebFileCatalog.cs:105-113 + 149-155` sind die zu
    konsolidierenden Aufrufer).
  - XML-Doc: erklärt, dass die Methoden projekteinheitliche Pfad-
    Exclusions (`obj/`, `bin/`, `node_modules/`) und
    Fehlertoleranz gegen `UnauthorizedAccessException`/`IOException`
    liefern, damit neue Dateisystem-Scans sie ohne Duplikation
    übernehmen.
- **Datei 2:** `src/AiNetLinter/Web/WebFileCatalog.cs` (Z. 105-113 +
  Z. 149-155) — die zwei privaten statischen Methoden löschen, an den
  Aufrufstellen `FileSystemExclusionHelpers.SafeEnumerateFiles(...)` /
  `FileSystemExclusionHelpers.IsGeneratedPath(...)` einsetzen.
  Verhaltens-Identität verifiziert: gleiche Exception-Klassen, gleiche
  Reihenfolge der `Contains`-Checks, gleiche `Path.DirectorySeparatorChar`-
  Semantik.
- **Datei 3:** `src/AiNetLinter/Mcp/Tools/GetIndexScopeScanner.cs` (Z. 78-94)
  — gleiche Aktion wie in `WebFileCatalog.cs`. Der `using AiNetLinter.Baseline;`
  ist bereits am Dateianfang (Z. 6) — keine zusätzlichen Imports nötig.
- **Datei 4 (Test, neu):**
  `src/AiNetLinter.Tests/Baseline/FileSystemExclusionHelpersTests.cs` —
  Unit-Tests mit `[Trait("Category", "Unit")]`:
  - `IsGeneratedPath_ObjSubdir_ReturnsTrue`
  - `IsGeneratedPath_BinSubdir_ReturnsTrue`
  - `IsGeneratedPath_NodeModulesSubdir_ReturnsTrue`
  - `IsGeneratedPath_NormalPath_ReturnsFalse`
  - `SafeEnumerateFiles_NonExistentDir_ReturnsEmpty`
  - `SafeEnumerateFiles_MockedUnauthorizedAccess_ReturnsEmpty` (per
    Test-Dir mit ACL-Lock auf Windows nicht trivial — alternativ
    Stub via `IDirectoryEnumerator`-Interface, oder den Test auf
    "existing dir + obj/-bin/-node_modules-Dateien werden gefiltert"
    reduzieren).
- **Warum:** Etabliertes DRY-Pattern (gleiche Methoden 1:1 dupliziert),
  Konzept-Vorgabe explizit: „einmalig in eine gemeinsame Hilfsklasse
  ziehen". `Baseline/` ist der projektweite Namespace für
  Dateisystem-Kataloge (`SourceFileCatalog`), passt thematisch. **Schließen
  mit Konsolidierung**.

### EPIC-07 — TD-008: `GetViolationsScanner.cs:192` Forward-Looking-Rationale

- **Datei:** `src/AiNetLinter/Mcp/Tools/GetViolationsScanner.cs` (Z. 190-194,
  XML-Doc am `GetViolationsScannerParameters`-Record).
- **Aktion:** Wort „ehemalige 6-Parameter-Signatur zusammen" durch
  forward-looking Rationale ersetzen, Pattern-Vorlage aus
  `step-010`-TD-007-Sanierung (Konzept-Anhang): „kapselt 6
  Konfigurations-Eingaben in einem Record, damit
  `MaxMethodParameterCount: 4` (siehe `AiNetLinter.mdc`) eingehalten
  wird" statt „ehemalige 6-Parameter-Signatur zusammen".
- **Datei 2:** `tasks/codegraph-mcp-finish/tech-debt.md` (TD-008-Eintrag)
  — Status auf **„geschlossen"** mit Verweis auf den `step-012`-Commit.
- **Warum:** Mechanische 1-Zeilen-Sanierung analog TD-001/TD-007,
  semantisch identisch. **Schließen mit Sanierung**.

### EPIC-08 — E.1: `get_symbol_body` + stabile Symbol-IDs

- **Datei 1:** `src/AiNetLinter/Maps/Skeleton/SkeletonTypeInfo.cs` (Z. 7-15)
  + `SkeletonMemberInfo.cs` (Z. 17-21) — neuen Pflicht-Property `string Id`
  auf beiden Records ergänzen, gefüllt aus
  `DocumentationCommentId.CreateDeclarationId(symbol)` (Microsoft.CodeAnalysis).
  Bestehende Konstruktor-Aufrufer (3 Stellen in
  `SkeletonSyntaxWalker.cs`) müssen das neue Feld setzen — Aufwand
  1 Zeile pro Aufrufstelle.
- **Datei 2:** `src/AiNetLinter/Maps/Skeleton/SkeletonMarkdownRenderer.cs`
  (Z. 100-106, in `AppendMembersOfKind`) — die gerenderte
  Member-Signatur-Zeile um einen `/* id: M:... */`-Kommentar
  erweitern, **nur** wenn `m.Id` nicht leer ist. `SkeletonTypeInfo`
  selbst bekommt die `Id` als optionalen Suffix-Block in
  `AppendType` (Z. 65), analog zu `BaseTypes` — Format:
  `### ClassName `id:M:...`` — ` `.
- **Datei 3 (neu):** `src/AiNetLinter/Mcp/Tools/GetSymbolBodyTool.cs` —
  `internal static class GetSymbolBodyTool` mit einer
  `ExecuteAsync(McpCodeGraphServer state, string identifier, int
  maxBodyLines, CancellationToken ct)`-Methode (Tool-Dispatcher-Pattern,
  analog `FindReferencesTool.ExecuteAsync`):
  1. LoadState-Check wie in den existierenden Tools (Loading →
     `McpToolResults.Loading()`).
  2. `identifier`-Auflösung über erweiterten
     `SymbolIdentifierResolver.TryResolveByStableId(...)` (siehe
     Datei 4) — wenn das fehlschlägt, Fallback auf
     `ResolveSymbolAsync` (Datei:Zeile:Spalte + qualifizierter Name).
  3. `symbol.DeclaringSyntaxReferences.FirstOrDefault()` →
     `SyntaxNode.GetText()` → `text.ToString().Substring(0, min(text.Length, maxBodyLines))`
     — harte Kappung am `maxBodyLines` (Default 80, `MaxSymbolBodyLines`
     als Konstante in der Tool-Klasse), danach Ellipse-Indikator
     `(… truncated, total {fullLength} Zeilen, maxBodyLines erhöhen für
     mehr)`.
  4. Compile-Fehler-Warnung analog `FindSymbolTool.BuildAggregateWarningAsync`.
  5. Output: Markdown-Block mit `### {Kind}: {DisplayString} — `{RelativePath}``
     Header + `id: M:...` Marker + Body.
- **Datei 4:** `src/AiNetLinter/Mcp/Tools/SymbolIdentifierResolver.cs`
  (Erweiterung) — neue Methode
  `internal static async Task<(ISymbol? Symbol, CallToolResult? Error)>
  TryResolveByStableIdAsync(Solution solution, string stableId, CancellationToken ct)`:
  - Wenn `stableId` mit `M:`/`T:`/`P:`/`F:`/`E:`/`!:` (DocumentationCommentId-Prefixes)
    beginnt: über alle `solution.Projects` →
    `SymbolFinder.GetSourceDeclarationsAsync` (oder eine schlanke
    Variante) iterieren und mit
    `DocumentationCommentId.CreateDeclarationId(s) == stableId`
    vergleichen.
  - Wenn nicht: `(null, McpToolResults.SymbolNotFound(stableId))`.
  - Die existierenden `ResolveSymbolAsync`/`TryParsePosition` bleiben
    unverändert; `FindReferencesTool.ResolveSymbolAsync` wird nicht
    modifiziert (das `get_symbol_body`-Tool ruft `TryResolveByStableIdAsync`
    **zuerst**, Fallback auf `ResolveSymbolAsync` wenn das
    fehlschlägt).
- **Datei 5 (neu):** `src/AiNetLinter/Mcp/SymbolBodyToolRegistrations.cs` —
  `internal static class SymbolBodyToolRegistrations` mit `Register`-
  Methode analog zu den 3 existierenden Registrars (dünner Dispatch
  + `if (callLog is null)`-Fast-Path). **4. Tool-Registrar-Klasse
  insgesamt** (löst TD-011). Erwarteter Footprint: ~110-130 Z. (kleiner
  als SymbolGraphToolRegistrations, weil nur 1 Tool), unter 2500 kein
  PathOverride nötig. Falls der Footprint wider Erwarten über 2500
  steigt: PathOverride hinzufügen und im `step-result.md` begründen.
- **Datei 6:** `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` (Z. 48-58,
  `BuildToolCollection`) — Aufruf von
  `SymbolBodyToolRegistrations.Register(tools, mcpState, callLog);`
  hinzufügen. Reihenfolge in der Tool-Liste: nach den 3 existierenden
  Registrar-Aufrufen, vor dem `return tools;`.
- **Tests:**
  - `src/AiNetLinter.Tests/Mcp/Tools/GetSymbolBodyToolTests.cs` (neu,
    `[Trait("Category", "Unit")]` mit `SymbolGraphCatalogFixture`):
    - `ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode`
    - `ExecuteAsync_ValidStableId_ReturnsBodyForMethod`
    - `ExecuteAsync_ValidStableId_TruncatesAtMaxBodyLines_AppendsEllipsis`
    - `ExecuteAsync_InvalidStableId_FallsBackToFileLineCol`
    - `ExecuteAsync_InvalidStableId_AndFileLineColNotFound_ReturnsSymbolNotFound`
  - `src/AiNetLinter.Tests/Mcp/Tools/SkeletonStableIdTests.cs` (neu,
    `[Trait("Category", "Unit")]`): `SkeletonMapBuilder` über
    `SymbolGraphMiniFixtureWorkspace` aufrufen, jedes `SkeletonMemberInfo`
    muss eine `Id` haben, die `M:`/`P:`/`F:` etc. als Prefix hat und
    in `get_symbol_body` wieder zu genau diesem Symbol auflöst.
  - `McpServerCommandTests` (Erweiterung): 1 Test
    `RunAsync_ValidFixture_ServerRespondsWithTenTools` (jetzt 10
    Tools, vorher 9).
- **Warum:** Konzept-Vorgabe explizit für `get_symbol_body` und stabile
  IDs; `DocumentationCommentId` ist die Roslyn-Standardmethode für
  stabile Symbol-Identifikation. **Eigene Registrar-Klasse** statt
  Erweiterung von `SymbolGraphToolRegistrations`, weil die existierende
  Klasse bereits am 2850-PathOverride hängt und das TD-011-„Puffer-bis-
  Limit"-Risiko sonst direkt auf 0 fällt. Footprint-Schätzung ~110-130 Z.
  ist **deutlich** unter 2500 → keine zusätzliche PathOverride nötig.

### EPIC-08 — E.2: `depth`-Parameter an `find_references`/`get_impact`

- **Datei 1:** `src/AiNetLinter/Mcp/Tools/FindReferencesTool.cs` (Z. 33-34)
  — Signatur erweitern: `int maxResults = 50, int depth = 1, CancellationToken
  ct = default`. `MaxDepth = 3` als Konstante in der Klasse; `depth > 3`
  → clamp auf 3 + `[WARN]`-Hinweis in `WriteWarn` (Console-Parameter
  durchreichen oder eine dedizierte `MaxDepthWarningAsync`-Methode
  analog `BuildAggregateWarningAsync`).
- **Datei 2:** `src/AiNetLinter/Mcp/Tools/GetImpactTool.cs` (Z. 22-23) —
  gleiche Erweiterung in `ExecuteAsync` + `ExecuteSymbolBranchAsync` +
  `ExecuteGitRefBranchAsync` (nur Symbol-Branch nutzt `depth`, Git-Branch
  ignoriert ihn — Konzept-Vorgabe: „Optionaler Parameter an
  `find_references`/`get_impact`"; Git-Branch ist explizit
  `gitRef`-basiert, dort gibt es keine Symbol-Tiefe).
- **Datei 3:** `src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs`
  (Z. 68-90 + Z. 99-129) — die `AddFindReferences`- und
  `AddGetImpact`-Lambdas um `int depth = 1` Parameter erweitern, an
  `ExecuteAsync` weiterreichen. **Call-Log-String-Format** entsprechend
  erweitern (z. B. `"{symbolIdentifier}|{maxResults}|{depth}"`).
- **Datei 4 (neu):** `src/AiNetLinter/Mcp/Tools/CallGraphTraversal.cs`
  — rekursiver Traversierungs-Helper, der `SymbolFinder.FindReferencesAsync`
  iterativ mit `depth`-Cap aufruft. Signatur:
  ```csharp
  internal static async Task<IReadOnlyList<string>> ExpandTransitivelyAsync(
      Solution solution,
      ISymbol seedSymbol,
      int depth,
      int maxNodes,
      CancellationToken ct)
  ```
  - depth 1: nur direkte `FindCallSitesAsync`-Treffer (heutiges Verhalten,
    bestehende Implementierung beibehalten).
  - depth 2-3: für jeden Call-Site-Symbol ein neues
    `FindReferencesAsync`, Treffer in `HashSet` dedupliziert, Abbruch
    bei `maxNodes` (`MaxRecursionNodes = 200` als Konstante in der
    Helper-Klasse).
  - Aggregation: `IReadOnlyList<string>` ist eine
    **vorgefertigte formatierte Liste** („37 Aufrufer in 12 Dateien,
    davon 9 in 3 Projekten" + Top-N), nicht rohe `IEnumerable<string>`-
    Treffer. Format-Helfer als private statische Methode in
    `CallGraphTraversal`.
- **Tests:**
  - `src/AiNetLinter.Tests/Mcp/Tools/FindReferencesToolTests.cs`
    (Erweiterung): `ExecuteAsync_Depth2_AggregatesAcrossFiles`,
    `ExecuteAsync_DepthAboveCap_ClampsToThreeAndWarns`,
    `ExecuteAsync_Depth1_MatchesCurrentBehavior` (Regression-Test).
  - `src/AiNetLinter.Tests/Mcp/Tools/GetImpactToolTests.cs`
    (Erweiterung): analog für Symbol-Branch.
  - `src/AiNetLinter.Tests/Mcp/Tools/CallGraphTraversalTests.cs` (neu,
    `[Trait("Category", "Unit")]`): direkter Test der
    Aggregations-Formatierung mit kleinem
    `SymbolGraphMiniFixtureWorkspace`-Szenario.
- **Warum:** Konzept-Vorgabe explizit. Eigenständiger
  `CallGraphTraversal`-Helper hält `FindReferencesTool`/`GetImpactTool`
  schlank (kein Footprint-Druck über 2620/2650 PathOverride hinaus).
  Clamp + `[WARN]` verhindert, dass ein Agent versehentlich mit
  `depth = 100` einen exponentiellen Walk auslöst.

### EPIC-08 — E.3: DI-Registrierungs-Hinweis in `get_type_hierarchy`

- **Datei 1 (neu):** `src/AiNetLinter/Mcp/Tools/DiRegistrationHeuristics.cs`
  — `internal static class DiRegistrationHeuristics` mit einer Methode
  ```csharp
  internal static async Task<IReadOnlyList<string>> FindRegistrationsAsync(
      Solution solution, INamedTypeSymbol type, CancellationToken ct)
  ```
  - Iteriert über `solution.Projects` → `project.Documents.Where(d =>
    d.FilePath?.EndsWith(".cs") == true)`.
  - Pro Datei: `document.GetTextAsync(ct)` → Regex-Match auf
    `\bAddScoped<\s*([\w\.\?\,\s]+?)\s*>`,
    `\bAddSingleton<\s*([\w\.\?\,\s]+?)\s*>`,
    `\bAddTransient<\s*([\w\.\?\,\s]+?)\s*>` — mit `\b`-Word-Boundary,
    sodass `MyAddScopedHelper` nicht matcht.
  - Treffer-Format pro Zeile: `"{Lifestyle}: {TypeName} ({RelativePath}:{Line}) —
    Snippet"`. Snippet = `text.GetSubText(span).ToString().Trim()`.
  - Heuristik-Limit: max. `MaxRegistrationHits = 20` (kein
    Performance-Desaster bei 1000+ Dateien, in Heuristik-Helper
    dokumentiert).
  - Filter: nur Treffer, in denen `type.ToDisplayString()` (oder eine
    `SymbolDisplayFormat.MinimallyQualified`-Variante davon) im
    Typ-Parameter-Text vorkommt — verhindert Massen-Treffer bei
    generischen `AddScoped<ILogger<>>`-Patterns.
- **Datei 2:** `src/AiNetLinter/Mcp/Tools/GetTypeHierarchyFormatter.cs`
  (Z. 27-40, in `BuildHierarchyTextAsync`) — nach den 3 bestehenden
  Sektionen eine 4. Sektion „DI-Registrierungen (heuristisch,
  Convention-/Factory-basiertes Scanning nicht abgedeckt):" anhängen,
  gefüllt aus `DiRegistrationHeuristics.FindRegistrationsAsync(...)`.
  Bei 0 Treffern Sektion weglassen (kein „Keine DI-Registrierungen."-
  Eintrag — das wäre Rauschen, das die meisten Tool-Calls verlängert).
- **Datei 3:** `src/AiNetLinter/Mcp/Tools/GetTypeHierarchyTool.cs` (Z. 36-38)
  — keine Code-Änderung nötig, weil die DI-Sektion in
  `BuildHierarchyTextAsync` (Formatter) integriert wird. **Achtung:**
  Compile-Warnung-Hinweis (`BuildAggregateWarningAsync` +
  `PrependWarning`) bleibt erhalten.
- **Tests:**
  - `src/AiNetLinter.Tests/Mcp/Tools/DiRegistrationHeuristicsTests.cs`
    (neu, `[Trait("Category", "Unit")]` mit `SymbolGraphCatalogFixture`):
    - `FindRegistrationsAsync_NoRegistrationForType_ReturnsEmpty`
    - `FindRegistrationsAsync_FindsAddScopedHit_FormatsWithLifestyle`
    - `FindRegistrationsAsync_FindsAddSingletonAndTransient_OrdersByLine`
    - `FindRegistrationsAsync_DoesNotMatchAddScopedHelperAsSubstring`
    - `FindRegistrationsAsync_RespectsMaxRegistrationHitsCap`
  - `src/AiNetLinter.Tests/Mcp/Tools/GetTypeHierarchyFormatterTests.cs`
    (Erweiterung, falls existent; sonst neu): 1 Test
    `BuildHierarchyTextAsync_IncludesDiRegistrationSection_WhenHeuristicHits`,
    der in einem Mini-Fixture-Workspace mit synthetischer
    `AddSingleton<IFoo>(...)`-Zeile die Sektion im Output verifiziert.
- **Warum:** Konzept-Originalformulierung explizit: „reine Textsuche
  nach `AddScoped<IFoo`/.../`AddTransient<IFoo` als zusätzliche Zeile
  in der bestehenden `get_type_hierarchy`-Antwort, klar als
  heuristischer Fund gekennzeichnet". Helper-Klasse hält
  `GetTypeHierarchyFormatter` schlank, Regex-Wortgrenzen schützen vor
  `MyAddScopedHelper`-False-Positives. **Kein neues Tool**, keine
  Convention-/Factory-Erkennung (Konzept-Vorgabe).

### Doku-Updates (gemeinsam für EPIC-07 + EPIC-08, im Doku-Commit)

- `Docs/agent-api.md` — neuen Abschnitt „Symbolgraph-Erweiterungen"
  mit Sub-Abschnitten für E.1 (`get_symbol_body` + stabile IDs in
  `get_file_skeleton`), E.2 (`depth`-Parameter an
  `find_references`/`get_impact`), E.3 (DI-Hinweis in
  `get_type_hierarchy`). Tool-Tabelle (falls existent) um
  `get_symbol_body` ergänzen.
- `Docs/integration.md` — Hinweis auf 10 Tools (statt 9) im
  `initialize`-Beschreibungstext; neue Tool-Beispiele für `get_symbol_body`
  und `depth`-Aufrufe.
- `Docs/ROADMAP.md` Z. 478-493 (B-Block) — B.6 + B.7 von „Geplant" auf
  „Umgesetzt" verschieben; E-Block (E.1, E.2, E.3) von „Geplant" auf
  „Umgesetzt (step-012)".
- `Docs/rationale.md` — falls vorhanden, kurzer Eintrag zur
  Symbol-ID-Wahl (`DocumentationCommentId` vs. alternative Hashes).
- `rules.json` — **kein** automatisches Re-Lint nötig, weil keine
  Regel-Schwellen geändert werden. `PathOverride` für
  `SymbolBodyToolRegistrations` (falls wider Erwarten nötig) wird im
  selben Code-Commit ergänzt.
- `tasks/codegraph-mcp-finish/tech-debt.md` — TD-001, TD-002, TD-006,
  TD-008 Status auf „geschlossen", TD-004 Status auf „zurückgestellt"
  (alle in diesem Step).

## Tests

EPIC-07:
- [ ] `td-001`: Grep-Verifikation, dass `Microsoft.Extensions.AI.*` nicht
      direkt im `src/AiNetLinter/`-Baum verwendet wird (im
      `step-result.md` dokumentieren).
- [ ] `td-002`: keine (Status-Update in `tech-debt.md`, kein Code-Change).
- [ ] `td-004`: keine (Status-Update in `tech-debt.md`, kein Code-Change).
- [ ] `td-006`: `FileSystemExclusionHelpersTests` (6 Unit-Tests) +
      bestehende `WebFileCatalogTests`/`GetIndexScopeToolTests` müssen
      grün bleiben (Verhaltens-Identität der konsolidierten Methoden).
- [ ] `td-008`: keine direkten Tests; Verifikation über
      `dotnet build` (keine Verhaltensänderung).

EPIC-08:
- [ ] `e1`: `GetSymbolBodyToolTests` (5 Unit-Tests mit
      `SymbolGraphCatalogFixture`) + `SkeletonStableIdTests` (1
      Unit-Test) + `McpServerCommandTests.RunAsync_ValidFixture_ServerRespondsWithTenTools`
      (1 E2E-Test, Integration-Kategorie).
- [ ] `e2`: `FindReferencesToolTests`-Erweiterung (3 Unit-Tests) +
      `GetImpactToolTests`-Erweiterung (1-2 Unit-Tests) +
      `CallGraphTraversalTests` (3 Unit-Tests).
- [ ] `e3`: `DiRegistrationHeuristicsTests` (5 Unit-Tests) +
      `GetTypeHierarchyFormatterTests`-Erweiterung (1 Unit-Test).

Gesamt: ~24 neue Unit-Tests + ~1 E2E-Test-Erweiterung. Volllauf-Zielwert
~1225-1240 Tests (von aktuell 1215), alle in unter 4 Min. auf
Standard-Hardware (Volllauf vor dem Schritt war 3 m 4 s, +25 Tests bei
unter 1 s/Test = +25 s erwartet).

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt:
  - [ ] TD-001 verifiziert + dokumentiert, `tech-debt.md` aktualisiert
  - [ ] TD-002 als „geschlossen" mit Begründung in `tech-debt.md`
  - [ ] TD-004 als „zurückgestellt" mit Begründung in `tech-debt.md`
  - [ ] TD-006 mit `FileSystemExclusionHelpers` + 2 Aufrufer-
        Umstellungen + Unit-Tests umgesetzt, `tech-debt.md` aktualisiert
  - [ ] TD-008 mit 1-Zeilen-XML-Doc-Sanierung umgesetzt, `tech-debt.md`
        aktualisiert
  - [ ] E.1 mit `get_symbol_body` + stabilen IDs + 4. Registrar-Klasse
        + 7 Unit-Tests + 1 E2E-Test umgesetzt
  - [ ] E.2 mit `depth`-Parameter an `find_references`/`get_impact` +
        `CallGraphTraversal` + 7 Unit-Tests umgesetzt
  - [ ] E.3 mit `DiRegistrationHeuristics` + 5 Unit-Tests + 1
        Formatter-Test umgesetzt
- [ ] Build-Command aus Tech-Stack-Notiz (`roadmap.md`) grün
      (0 Warnungen, 0 Fehler, `<TreatWarningsAsErrors>true`).
- [ ] Test-Command aus Tech-Stack-Notiz grün (Volllauf in unter 4 Min.,
      kein TD-005-Flake).
- [ ] Selbst-Lint grün: `dotnet run --project src\AiNetLinter --
      --config rules.json --path .` → 0 Violations. **Falls die
      `SymbolBodyToolRegistrations` einen PathOverride braucht:** im
      `step-result.md` mit gemessenem Footprint + Begründung
      dokumentieren.
- [ ] Zwei Commits auf aktuellem Branch:
  1. **Code-Commit:** EPIC-07 (TD-001/002/004/006/008) + EPIC-08
     (E.1/E.2/E.3) + neue Tests.
  2. **Doku-Commit:** `Docs/agent-api.md` + `Docs/integration.md` +
     `Docs/ROADMAP.md` + `tech-debt.md` (Status-Updates für die 5
     TD-Items).
  Beide mit Conventional-Commit-Subject auf Deutsch, imperativ, mit
  Task-Suffix `[codegraph-mcp-finish]`.
- [ ] `step-012/step-result.md` geschrieben mit:
  - Gemessener Footprint pro neuer/berührter Datei
  - Volllauf-Statistik (Dauer, Anzahl, TD-005-Flake-Status)
  - Liste der 5 TD-Status-Updates mit den genauen Begründungen
  - Alle 3 PathOverride-Änderungen (falls vorhanden) mit gemessenem
    Footprint
- [ ] `status` in `step-plan.md` von `open` auf
      `done (pending audit)` gesetzt.
- [ ] `task-state.md`: `current_step` auf `step-012 (done, pending
      audit)` aktualisiert, neue Zeile in der Steps-Tabelle mit
      `EPIC-07+EPIC-08`.
- [ ] `roadmap.md`: EPIC-07 + EPIC-08 abgehakt mit
      step-012-Detail-Ergänzung analog EPIC-04/05/06-Stil.

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc` Zeile 15 (Kurz-Stil —
  `AIContextFootprint ≤ 2500`): **direkt relevant** für TD-004 +
  E.1 (`SymbolBodyToolRegistrations`-Footprint < 2500 zu halten,
  kein PathOverride).
- `.agents/rules/AiNetLinter.mdc` Zeile 11-12 (`sealed` für konkrete
  Klassen, `#nullable enable` am Dateianfang): Standard für alle
  neuen Klassen in `Baseline/FileSystemExclusionHelpers`,
  `Mcp/Tools/GetSymbolBodyTool`, `Mcp/Tools/CallGraphTraversal`,
  `Mcp/Tools/DiRegistrationHeuristics`,
  `Mcp/SymbolBodyToolRegistrations`.
- `.agents/rules/AiNetLinter.mdc` Zeile 22 (`MaxMethodParameterCount: 4`):
  relevant für E.2-Signaturen (`find_references`/`get_impact` mit
  `(symbolIdentifier, maxResults, depth, ct)` = 4 + CT = genau am
  Limit, **darf** nach Compiler-Sicht überschritten werden falls
  CT mitgezählt wird — Konzept `MaxMethodParameterCountInTestFiles: 6`
  + `MethodParameterCountIgnoreTypeNames: [CancellationToken]`
  hilft).
- `.agents/rules/AiNetLinter.mdc` Zeile 53-55 (agent-resilience:
  EnforceNoSilentCatch, BanAsyncVoid, BanBlockingTaskAccess): relevant
  für E.2 (rekursive `FindReferencesAsync`-Loop braucht sauberes
  CancellationToken-Handling, kein `.Wait()`), TD-006 (Regex-Match
  wirft `RegexMatchTimeoutException` nur bei explizitem
  `Regex.MatchTimeout` — hier nicht aktiv, keine Aktion nötig).
- `.agents/rules/AiNetLinterRichtlinien.mdc` §1 (Grundprinzipien —
  monolithisch, kein Plugin/ALC): bestätigt: 4. Registrar-Klasse ist
  reine Quellcode-Erweiterung im selben Assembly, kein
  Architektur-Bruch.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §2 (Architektur-Verbote —
  kein DI-Container): bestätigt: `get_symbol_body` löst den Symbol-
  Identifikator nicht über DI auf, sondern über die existierende
  `McpCodeGraphServer`-Closure-Referenz.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §4 (Testsuite-Parallelität):
  relevant für TD-002 (kein neuer Serialisierungs-Engpass) und E.1
  (neue Tests sollen **nicht** in `ConsoleTestCollection` aufgenommen
  werden, sofern nicht echter Console-Capture-Bedarf besteht — bei
  den geplanten Unit-Tests nicht gegeben).
- `.agents/rules/AiNetLinterRichtlinien.mdc` §5
  (Qualitätsdrift-Prävention): **direkt relevant** für TD-008
  (Forward-Looking-Rationale-Sanierung) und für die XML-Docs an den
  neuen Klassen (keine `step-012`/`EPIC-07`/`EPIC-08`/`TD-NNN`-Refs in
  Code-Kommentaren).
- `.agents/rules/AiNetLinterRichtlinien.mdc` §6 (Agenten-Arbeitsstil —
  Sparring-Modus bei größeren Vorhaben): wurde eingehalten, dieser
  Step-Plan wurde vom Planer im Step-Modus vor dem Coder-Aufruf
  erstellt.

## Bekannte Ausnahmen

- **TD-004 (Footprint-Druck auf 3 Registrars):** **zurückgestellt** mit
  ausführlicher Begründung (siehe oben). Kein Test-Flake, kein
  Performance-Issue, keine Sicherheits-Implikation. Falls ein
  künftiger Schritt zeigt, dass der Footprint-Druck durch eine
  kategoriespezifische Konsolidierung (z. B. gemeinsamer
  `CallLogEnabled`-Lambda-Body-Helper zwischen den Registrars)
  reduzierbar ist **ohne** das Dispatcher-Pattern zu verwässern, kann
  TD-004 wieder aufgenommen werden.
- **TD-002 (Subprozess-Fixture-Pool):** **geschlossen mit Begründung**
  (kein Eskalations-Bedarf bei 1 Klassen-Container). Falls künftig
  mehrere neue Subprozess-E2E-Testklassen hinzukommen, sollte der
  `InMemoryTransport`-Pattern geprüft werden, dann TD-002 wieder
  öffnen.
- **E.1 `get_symbol_body` mit 4. Registrar-Klasse:** falls wider
  Erwarten der Footprint von `SymbolBodyToolRegistrations` über 2500
  steigt (z. B. durch transitive `McpCallLog`-Effekte, wie in
  `step-011` für die existierenden 3 Registrars geschehen), wird
  analog ein `PathOverride: MaxAIContextFootprint: 28XX` in
  `rules.json` ergänzt und im `step-result.md` begründet — **kein
  Blocker**.
- **E.2 `depth > 1`-Aggregation bei großen Symbolgraphen:** bei sehr
  transitiven Aufrufstellen kann die Top-N-Aggregation immer noch
  >100 Token kosten. Konzept-Vorgabe: „fest verdrahtete Obergrenze
  (z. B. 3)" + „separates Knotenlimit unabhängig von maxResults"
  sind eingehalten; weitere Eskalation (z. B. „Depth-2-Output
  zusätzlich trunkieren") ist Nice-to-Have, nicht Scope.
- **E.3 DI-Heuristik:** Convention-/Factory-basierte Registrierung
  wird **bewusst nicht** erkannt (Konzept-Vorgabe). `MyAddScopedHelper`-
  False-Positives werden über `\b`-Word-Boundary verhindert, aber
  generische `AddScoped<ILogger<>>`-Patterns könnten übermäßig viele
  Treffer liefern — Filter auf `type.ToDisplayString()` im Treffer
  (siehe `DiRegistrationHeuristics`) reduziert das auf die relevanten
  Treffer, ist aber eine Heuristik, kein Beweis.

## Code-Skizze (optional)

```csharp
// --- TD-006: src/AiNetLinter/Baseline/FileSystemExclusionHelpers.cs (NEU) ---
internal static class FileSystemExclusionHelpers
{
    internal static IEnumerable<string> SafeEnumerateFiles(string directory)
    {
        try
        {
            return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories);
        }
        catch (UnauthorizedAccessException) { return Array.Empty<string>(); }
        catch (IOException) { return Array.Empty<string>(); }
    }

    internal static bool IsGeneratedPath(string path)
    {
        var sep = Path.DirectorySeparatorChar;
        return path.Contains($"{sep}obj{sep}", StringComparison.OrdinalIgnoreCase)
            || path.Contains($"{sep}bin{sep}", StringComparison.OrdinalIgnoreCase)
            || path.Contains($"{sep}node_modules{sep}", StringComparison.OrdinalIgnoreCase);
    }
}

// --- E.1: SkeletonMemberInfo.cs (Erweiterung) ---
internal sealed record SkeletonMemberInfo(
    MemberKind Kind,
    string Signature,
    string? MetaComment,
    string Id);  // NEU, gefüllt aus DocumentationCommentId.CreateDeclarationId(symbol)

// --- E.1: SymbolBodyToolRegistrations.cs (NEU) ---
internal static class SymbolBodyToolRegistrations
{
    internal static void Register(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState,
        McpCallLog? callLog = null)
    {
        AddGetSymbolBody(tools, mcpState, callLog);
    }

    private static void AddGetSymbolBody(
        McpServerPrimitiveCollection<McpServerTool> tools,
        McpCodeGraphServer mcpState,
        McpCallLog? callLog)
    {
        tools.Add(McpServerTool.Create(
            async (string identifier, int maxBodyLines = 80, CancellationToken ct = default) =>
            {
                if (callLog is null)
                {
                    return await GetSymbolBodyTool.ExecuteAsync(mcpState, identifier, maxBodyLines, ct);
                }
                await using var scope = callLog.StartRecording("get_symbol_body", $"{identifier}|{maxBodyLines}");
                var result = await GetSymbolBodyTool.ExecuteAsync(mcpState, identifier, maxBodyLines, ct);
                scope.Complete(result);
                return result;
            },
            new McpServerToolCreateOptions
            {
                Name = "get_symbol_body",
                Description = "Liefert den Body eines C#-Symbols per stabiler ID (DocumentationCommentId) oder Datei:Zeile:Spalte. Deckt nur .cs-Dateien ab. Hart gekappt bei maxBodyLines, Default 80.",
            }));
    }
}

// --- E.2: CallGraphTraversal.cs (NEU) ---
internal static class CallGraphTraversal
{
    private const int MaxRecursionDepth = 3;
    private const int MaxRecursionNodes = 200;

    internal static async Task<string> ExpandAndFormatAsync(
        Solution solution,
        ISymbol seedSymbol,
        int requestedDepth,
        int maxResults,
        CancellationToken ct)
    {
        var depth = Math.Clamp(requestedDepth, 1, MaxRecursionDepth);
        var seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default) { seedSymbol };
        var queue = new Queue<(ISymbol Symbol, int Level)>();
        queue.Enqueue((seedSymbol, 1));

        var allHits = new List<string>();
        while (queue.Count > 0 && allHits.Count < MaxRecursionNodes)
        {
            var (current, level) = queue.Dequeue();
            var refs = await SymbolFinder.FindReferencesAsync(current, solution, ct);
            foreach (var r in refs)
            {
                foreach (var loc in r.Locations)
                {
                    allHits.Add(FormatLocation(loc, solution));
                }
            }
            if (level < depth)
            {
                foreach (var r in refs)
                {
                    if (seen.Add(r.Definition)) queue.Enqueue((r.Definition, level + 1));
                }
            }
        }

        return AggregateAndTruncate(allHits, maxResults);
    }

    // AggregateAndTruncate: "37 Aufrufer in 12 Dateien, davon 9 in 3 Projekten"
    // + Top-N, McpTruncation.TruncateLines-Format wie in step-011
    // (Sub-String-Match auf "Treffer gesamt, ").
}

// --- E.3: DiRegistrationHeuristics.cs (NEU) ---
internal static class DiRegistrationHeuristics
{
    private static readonly Regex AddScopedPattern = new(
        @"\bAddScoped<\s*([\w\.\?\,\s]+?)\s*>", RegexOptions.Compiled);
    // analog AddSingleton, AddTransient

    internal static async Task<IReadOnlyList<string>> FindRegistrationsAsync(
        Solution solution, INamedTypeSymbol type, CancellationToken ct)
    {
        var typeName = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        var hits = new List<string>();
        foreach (var project in solution.Projects)
        {
            foreach (var document in project.Documents)
            {
                if (document.FilePath?.EndsWith(".cs") != true) continue;
                var text = await document.GetTextAsync(ct);
                var content = text.ToString();
                foreach (Match m in AddScopedPattern.Matches(content))
                {
                    if (!m.Groups[1].Value.Contains(typeName)) continue;
                    var line = text.Lines.GetLineFromPosition(m.Index) + 1;
                    hits.Add($"AddScoped: {m.Groups[1].Value.Trim()} " +
                             $"({document.FilePath}:{line})");
                    if (hits.Count >= MaxRegistrationHits) return hits;
                }
            }
        }
        return hits;
    }
}
```

## Notes

- **Reihenfolge-Disziplin:** EPIC-07 vor EPIC-08, **innerhalb** von
  EPIC-07 TD-006 vor TD-008 (TD-006 berührt 2 Hot-Dateien, Sanierung
  reduziert TD-001-Eintrag-Grep-Rauschen). Innerhalb von EPIC-08 E.1
  vor E.2 vor E.3 — E.1 hat die größte Schnittfläche (neues Tool +
  4. Registrar + Skeleton-Datenstrukturänderung), E.3 ist die
  kleinste (1 Helper-Klasse + 1 Sektion-Erweiterung im Formatter).
  Diese interne Reihenfolge ist **nicht** hart kodiert, aber
  empfohlen.
- **Scope-Disziplin:** keine Erweiterung des Symbolgraphen um
  darüber-hinausgehende Features (kein `get_call_tree`, kein
  `Duplicate-Symbol-Drift-Warnung`, kein PageRank — Konzept-Non-
  Goals, explizit verworfen). Keine Änderung an `McpServerOptionsFactory`
  außer dem zusätzlichen Registrar-Aufruf. Keine Änderung an
  `McpCallLog` über die `depth`-Parameter-String-Erweiterung hinaus.
- **TD-005-Schwester:** TD-005 wurde bereits in `step-010` geschlossen
  (Gate 4→6 Slots). TD-007-Schwester-Stelle (TD-008) wird hier
  nachgeholt, der Plan antizipiert keine neue TD-007-Variante. Falls
  beim Grep über die neuen Klassen ein weiteres „ehemalige"-Vorkommen
  auftaucht: im selben Zug mitsanieren (§5 „Aufräumen erlaubt") und
  im `step-result.md` vermerken.
- **Doku-Commits:** zwei Commits gemäß `spec.md` §10.3 — ein
  Code-Commit (EPIC-07 TD-001/002/004/006/008 + EPIC-08 E.1/E.2/E.3 +
  ~24 neue Tests) und ein Doku-Commit (agent-api.md, integration.md,
  ROADMAP.md, rationale.md, tech-debt.md, task-state.md). Beide mit
  Task-Suffix `[codegraph-mcp-finish]`.
- **Verifikations-Strategie am Step-Ende:** Volllauf 2× reproduzieren
  (analog `step-010`-Konvention), Selbst-Lint mit 0 Violations.
  Falls ein `SymbolBodyToolRegistrations`-PathOverride nötig wird:
  im `step-result.md` mit dem gemessenen Footprint dokumentiert.
- **EPIC-07 + EPIC-08 sind die letzten Epics dieses Tasks** — nach
  erfolgreichem `step-012`-Abschluss ist der `codegraph-mcp-finish`-
  Task inhaltlich abgeschlossen. Der nächste Schritt wäre
  Task-Summary (`tasks/codegraph-mcp-finish/task-summary.md` mit
  Punkt 4 des Konzepts: „`tasks/test-optimierung/` ist gelöscht"
  verifiziert, Block-F-Laufzeit final dokumentiert, Gesamt-Bilanz
  über die 8 Epics), aber das ist explizit **nicht** Inhalt dieses
  Step-Plans.
- **Nicht-Planung zukünftiger Schritte:** kein „Vorausplanen" über
  step-012 hinaus. Der `task-summary`-Schritt ist ein eigener
  Folge-Auftrag, nicht Teil dieses Step-Plans.
