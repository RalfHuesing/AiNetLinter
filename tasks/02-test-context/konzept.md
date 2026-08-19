---
status: ready
type: konzept
project_kind: brownfield
estimated_scope: small
rules_dir: .agents/rules
last_updated: 2026-08-19T21:03:30+02:00
open_questions: []
---

# Konzept: `get_test_context` (Gezielte Test-Coverage-Awareness)

## Ziel (Was)
Ein eigenstaendiges MCP-Tool `get_test_context`, das fuer ein bestimmtes C#-Symbol (Klasse, Interface, Record, Methode oder `Datei.cs:Zeile`) in einem einzigen residenten Aufruf alle zugeordneten Test-Dateien, Test-Klassen, Test-Methoden (`[Fact]`, `[Theory]`, `[Test]`) und Test-Kategorien (Unit, Component, Integration) ermittelt.

Das Tool liefert:
1. Einen uebersichtlichen, kompakten Markdown-Report mit Zuordnungsgruenden (`Direct typeof Reference`, `Naming Convention Match`, `Explicit @covers Comment`, `Direct Member Match / Invocation`), aggregierten Test-Kategorien und direkt ausfuehrbaren `dotnet test`-Filterbefehlen.
2. Ein typisiertes `StructuredContent`-Objekt (`TestContextPayload`) zur programmatischen Weiterverarbeitung durch Agenten.
3. Diagnosehinweise, falls keine Tests gefunden wurden (mit konkreter Empfehlung fuer den Speicherort neuer Unit-Tests).

## Warum / Kontext
Wenn ein Entwickler oder Coding-Agent ein Refactoring, einen Bugfix oder ein Feature umsetzt, muss er wissen:
- Welche Testdateien und Testmethoden sichern das betroffene Symbol aktuell ab?
- Liegen fuer die Klasse Unit-Tests in `FastTests` oder Integrations-Tests in `IntegrationTests` vor?
- Mit welchem Test-Befehl/Filter kann die Aenderung sofort gezielt validiert werden?

Bisher musste der Agent entweder Text-Grep nutzen, manuell nach Dateinamen suchen oder das breitere Composite-Tool `get_feature_context` aufrufen (welches zusaetzlich Callers, Metrics und Violations aggregiert). `get_test_context` bietet einen fokussierten, leichtgewichtigen One-Shot-Einstiegspunkt ausschliesslich fuer Test-Zuordnungen.

## Scope

### Muss-Haben
- **MCP-Tool `get_test_context`**: Registriert in `AnalysisToolRegistrations.cs` im Namespace `AiNetLinter.Mcp.Tools.TestContext`.
- **Flexible Symbol-Auflösung**: Wiederverwendung von `FindReferencesTool.ResolveSymbolAsync` (unterstuetzt Klassenname, `Namespace.Klasse`, Methode, `Datei.cs:Zeile`, `DocCommentId`).
- **Wiederverwendung des Test-Discovery-Kerns**: Nutzung von `TestCoverageScanner.FindTestsForSymbolAsync` aus `AiNetLinter.Core` ohne Codeduplizierung.
- **Parametrisierung**:
  - `symbol` (string, optional/alias): Symbol-Bezeichner.
  - `symbolIdentifier` (string, optional/alias): Alternativer Symbol-Bezeichner gemaess MCP-Standardkonvention.
  - `maxResults` (int, default: `30`, cap: `100`): Maximale Anzahl gelisteter Test-Referenzen.
- **Output-Formate**:
  - Kompakter Markdown-Report (Header, Testdateien gruppiert mit Testmethoden, Match-Gruende, Kategorien, Gesamtanzahl und kopierbare `dotnet test`-Befehle).
  - `StructuredContent` DTO (`TestContextPayload`).
- **Fehler- und Sonderfallbehandlung**:
  - `McpToolResults.SymbolNotFound`, `AmbiguousSymbol`, `InvalidArgument`, `CompilationError`.
  - Erkennung ungetesteter Symbole mit konkretem Hinweis und Empfehlungspfad in `AiNetLinter.FastTests`.
- **Proaktiver Tech-Debt-Abbau**: Einhaltung von DRY, Auslagerung von Magic Strings (Match-Reasons und Kategorien) in typsichere `const string` Definitionen (`TestCoverageMatchReasons`, `TestCategories`).
- **Automatisierte Tests**: Vollstaendige Unit- und FastTests in `AiNetLinter.FastTests/Mcp/Tools/TestContext/GetTestContextToolTests.cs`.
- **Dokumentation**: Aktualisierung von `Docs/configuration.md`, `Docs/integration.md`, `Docs/ROADMAP.md` und `README.md`.

### Nice-to-Have (Zwischenspeicher — vor `status: ready` aufgelöst)
*(Leer – alle Punkte vor `status: ready` entschieden)*

### Non-Goals (bewusst NICHT Teil davon)
- **Keine Test-Ausfuehrung**: Das Tool fuehrt keine Tests aus (`dotnet test` bleibt der Gate-Mechanismus), sondern analysiert die statische Zuordnung.
- **Keine automatische Test-Generierung**: Das Tool generiert keine Test-Rumpfdateien oder Mocks, sondern liefert Kontext fuer den Agenten.
- **Keine Duplizierung des Scanners**: Kein zweiter Scanner neben `TestCoverageScanner`.

## Zielplattformen / Technischer Rahmen
- **.NET 10 / C# 13**: Roslyn-basierter residenter In-Memory Server (`McpCodeGraphServer`).
- **Wiederverwendete Kern-Komponenten**:
  - `TestCoverageScanner` (`src/AiNetLinter/Core/TestCoverageScanner.cs`)
  - `FindReferencesTool.ResolveSymbolAsync` (`src/AiNetLinter/Mcp/Tools/SymbolGraph/FindReferencesTool.cs`)
  - `McpToolResults` (`src/AiNetLinter/Mcp/McpToolResults.cs`)

## Verworfene Alternativen
- **Eigenstaendiger zweiter Test-Scanner**: Verworfen zur Vermeidung von Drift und Codeduplizierung; `TestCoverageScanner` deckt bereits Typeof, @covers, Namenskonventionen und Invocation-Matches ab.
- **Nur Text-Rueckgabe ohne StructuredContent**: Verworfen, da strukturierte DTOs fuer programmatische Agenten-Workflows essenziell sind.
- **Nur ein einziger Parameter-Name ohne Alias**: Verworfen, um maximale Kompatibilitaet sowohl zu `tasks/features/02-test-context.md` (`symbol`) als auch zu `find_references`/`get_feature_context` (`symbolIdentifier`) zu gewaehrleisten.

## Wo im Projekt
- `src/AiNetLinter/Mcp/Tools/TestContext/` [NEW]:
  - `GetTestContextTool.cs` (MCP Tool-Handler und Optionen)
  - `TestContextFormatter.cs` (Markdown-Renderer und Hilfsmethoden)
  - `TestContextModels.cs` (DTOs fuer StructuredContent)
- `src/AiNetLinter/Core/TestCoverageScanner.cs` (Gemeinsamer residenter Test-Scanner + Konstanten `TestCoverageMatchReasons`, `TestCategories`)
- `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs` (Tool-Registrierung `get_test_context`)
- `src/AiNetLinter.FastTests/Mcp/Tools/TestContext/` [NEW]:
  - `GetTestContextToolTests.cs` (Unit-Tests fuer Parameter, Formate, Ungetestet-Hinweise und Truncation)
- `Docs/configuration.md`, `Docs/integration.md`, `Docs/ROADMAP.md`, `README.md` (Dokumentation)

## Entdeckte Mängel/Redundanzen
- **Magic Strings in TestCoverageScanner**:
  - **Gefunden**: Match-Reason Strings (`"Direct typeof Reference"`, `"Naming Convention Match"`, `"Explicit @covers Comment"`, `"Direct Member Match / Invocation"`) und Category Strings (`"Unit"`, `"Integration"`) sind im Scanner als Literale vorhanden.
  - **Vorschlag**: Auslagern in typsichere `const string` Definitionen (`TestCoverageMatchReasons` / `TestCategories`), die sowohl von `TestCoverageScanner`, `get_feature_context` als auch von `get_test_context` wiederverwendet werden.
  - **Entscheidung**: Uebernommen in Scope (Muss-Haben) als proaktiver Tech-Debt-Abbau.

## Wie (grober Ansatz)
1. **Tech-Debt-Bereinigung**:
   - `TestCoverageMatchReasons` und `TestCategories` als typsichere Konstanten in `TestCoverageScanner.cs` definieren und dort sowie in `FeatureContextScanner.cs` anwenden.
2. **Tool-Architektur**:
   - `GetTestContextTool.ExecuteAsync` empfaengt `TestContextOptions` (`Symbol`, `SymbolIdentifier`, `MaxResults`).
   - Loest das Zielsymbol ueber `FindReferencesTool.ResolveSymbolAsync` auf.
   - Ruft `TestCoverageScanner.FindTestsForSymbolAsync(symbol, solution, ct)` auf.
   - Begrenzt die Ergebnisse gemaess `MaxResults` (mit Truncation-Flag).
3. **Payload & Formatierung**:
   - Erstellt `TestContextPayload` mit DTOs fuer Testdateien, Methoden, Kategorien, Empfehlungs-Befehle und Gesamtstatistik.
   - `TestContextFormatter.FormatReport` erzeugt kompakten Markdown-Output inklusive Ausfuehrungs-Tipps (`dotnet test`-Filter).
4. **Registrierung & Tests**:
   - Registrierung in `AnalysisToolRegistrations.cs` als `get_test_context`.
   - Umfassende FastTests zur Absicherung aller Pfade (`GetTestContextToolTests.cs`).
5. **Dokumentation & Dogfooding**:
   - Doku in `Docs/configuration.md`, `Docs/integration.md`, `Docs/ROADMAP.md` und `README.md`.
   - Volle Test-Gates (`FastTests` + `IntegrationTests`).

## Definition of Done / Erfolgskriterien
1. Tool `get_test_context` ist ueber MCP registriert und lauffaehig.
2. Symbol-Eingaben (Klassenname, `Namespace.Klasse`, Methode, `Datei.cs:Zeile`) loesen zuverlaessig auf.
3. Test-Dateien und Methoden werden gemaess `TestCoverageScanner` korrekt zurueckgegeben (Text + JSON StructuredContent).
4. Ungetestete Symbole erzeugen eine informative Rueckmeldung mit Diagnosehinweis und Empfehlung.
5. Magic Values fuer Match-Reasons / Test-Kategorien sind in Konstanten konsolidiert.
6. Alle Unit- und Component-Tests in `AiNetLinter.FastTests` sowie Integrations-Tests sind 100% gruen.
7. Dokumentation in `Docs/configuration.md`, `Docs/integration.md`, `Docs/ROADMAP.md` und `README.md` ist synchronisiert.

## Offene Punkte
*Keine. Konzept ist vollständig und freigegeben.*
