---
status: draft
type: konzept
project_kind: brownfield
estimated_scope: medium
rules_dir: .agents/rules
last_updated: 2026-08-12
open_questions:
  - Optimale Standard-Schwellwerte für minOccurrences (Default 2 für Duplikate, 1 für Scans) und String-Mindestlänge für Lokalisierung.
  - Exakte Namensgebung des MCP-Tools (find_magic_values vs. analyze_magic_values).
---

# Konzept: Magic Values Detection MCP-Tool

## Ziel (Was)

Ein Roslyn-basiertes MCP-Server-Tool (`find_magic_values`), das C#-Quellcode gezielt nach festen Werten (Literalen wie Strings, Zahlen, Pfaden, Timeouts) durchsucht, Duplikate identifiziert und strukturierte, fachlich klassifizierte Refactoring-Empfehlungen gibt (z. B. Verschieben in Constants-Klassen, `appsettings.json`, Enums oder Lokalisierung/`.resx`).

## Warum / Kontext

Beim Entwickeln und Refactorn mit KI-Agenten entstehen häufig unbeabsichtigt **Magic Values**:
- Feste Strings (URLs, Pfade, Error-Messages, SQL/Regex-Muster, Config-Keys)
- Feste Zahlen (Timeouts, Batch-Größen, Status-Codes, magische Offsets/Grenzwerte)

Ein dediziertes MCP-Tool liefert dem Agenten und Entwickler auf Anforderung Antworten:
1. **Wo haben wir Magic Strings / Zahlen im Code, die mehrfach vorkommen und in eine Konstanten-Klasse gehören?**
2. **Welche Werte sind Konfigurationsparameter und sollten in `appsettings.json` ausgelagert werden?**
3. **Welche magischen Zahlen/Strings repräsentieren eigentlich einen Typen-/Zustandssatz und sollten als `enum` refactored werden?**
4. **Welche benutzerseitigen Textnachrichten sollten in Ressourcen/Lokalisierung wandern?**

### Strategischer Mehrwert
- **Kuriert "Lazy Agent Habits":** Gibt dem Agenten ein Werkzeug zur Selbstkorrektur vor Commits.
- **Konstruktiv statt meckernd:** Klassische Linter sagen nur pauschal "Avoid Magic Number". Dieses MCP-Tool klassifiziert den Wert und gibt **konkrete Ziel-Empfehlungen** (`appsettings.json`, `Constants.cs`, `enum`, `.resx`).
- **Token- & Cost-Saver:** Ein einziger Tool-Call (~300 Tokens Output) ersetzt das Scannen von Dutzenden Quellcodedateien im Context.

## Scope

### Muss-Haben

- **Kategorisierung & Ziel-Empfehlungen:**
  - **Konfigurations-Kandidaten (`appsettings.json` / Environment):** URLs (`"https://..."`), Pfade (`"C:\\Data\\..."`), Connection Strings/Keys, Timeouts/Limits (`TimeSpan.FromSeconds(30)`, `30000` ms, `MaxRetries = 5`), Feature-Flags.
  - **Konstanten-Kandidaten (Zentrale `Constants.cs` / `[Domain]Constants.cs`):** Mehrmals verwendete Strings/Zahlen ($\ge 2$ Stellen, z. B. `"X-Correlation-ID"`), domänenspezifische Schwellenwerte (`0.19`), Format-Strings (`"yyyy-MM-dd"`).
  - **Enum-Kandidaten (`enum`):** Diskrete Wertebereiche in `switch`-Statements oder `if-else`-Kaskaden (`"Pending"`, `"Active"`, `"Failed"` oder `1`, `2`, `3`).
  - **Lokalisierungs-Kandidaten (`IStringLocalizer` / `.resx`):** User-Facing Nachrichtentexte in Exceptions, UI-Prompts oder Logins.
  - **Standard-HTTP / System-Standard:** HTTP-Statuscodes (`404`, `500` $\rightarrow$ `StatusCodes.Status404NotFound`), Leere Strings (`""` $\rightarrow$ `string.Empty`).
- **Rausch-Filterung (False Positives Vermeidung):**
  - Triviale Werte ignorieren (`0`, `1`, `-1`, `""`, `" "`, `"\n"`, `true`, `false`, `null`).
  - Attribut-Argumente isolieren (`[JsonPropertyName("foo")]`, `[Route("...")]`, `[Obsolete("...")]`).
  - Tests vs. Production Code Trennung (`includeTests: false` als Default).
  - Bereits definierte Konstanten/Fields ausschließen (`const` / `static readonly` Felddefinitionen).
- **MCP-Tool-Schnittstelle (`find_magic_values`):**
  - Parameter: `scope`, `valueType` (`all|strings|numbers`), `minOccurrences`, `categoryFilter`, `includeTests`.
  - Strukturierter JSON-Output mit `summary` und `recommendations` (inkl. `category`, `value`, `suggestedTarget`, `occurrences`, `locations`).

### Nice-to-Have (Zwischenspeicher — vor `status: ready` aufgelöst)

*Keine offenen Nice-to-Have-Punkte.*

### Non-Goals (bewusst NICHT Teil davon)

- **Keine automatischen Code-Fixer / Code-Umschreibung direkt im MCP-Tool:** Der MCP-Server liefert Evidenz & Empfehlungen; das Durchführen des Refactorings bleibt Aufgabe des KI-Agenten / Entwicklers.
- **Keine Redundanz zu `find_duplicates`:** `find_duplicates` sucht nach AST-Strukturblöcken; `find_magic_values` fokussiert sich atomar auf Datenliterale und deren fachlichen Bestimmungsort.

## Zielplattformen / Technischer Rahmen

- **Stack:** C# / .NET 9, Roslyn AST (`SyntaxWalker`) & `SemanticModel` APIs.
- **Integration:** Einbindung in den AiNetLinter MCP-Server (`src/AiNetLinter`).

## Machbarkeitsanalyse (Roslyn-Check)

| Feature / Vorschlag | Technischer Roslyn-Mechanismus | Umsetzbarkeit & Grenzen |
| :--- | :--- | :--- |
| **Literal-Erkennung & AST-Scan** | `SyntaxWalker` über `LiteralExpressionSyntax` (`StringLiteralExpression`, `NumericLiteralExpression`). | 🟢 **100% machbar.** Sehr schnell & trivial. |
| **Attribut-Filtern** | Prüfen, ob `node.FirstAncestorOrSelf<AttributeSyntax>() != null`. | 🟢 **100% machbar.** Reine AST-Prüfung ohne Semantik. |
| **Ausschluss von `const` / `static readonly`** | Prüfen auf `FieldDeclarationSyntax` / `PropertyDeclarationSyntax` mit `const` / `readonly` Modifier. | 🟢 **100% machbar.** Reine AST-Prüfung. |
| **Parameter-Kontext (z. B. `Thread.Sleep(5000)`)** | `SemanticModel.GetSymbolInfo(invocation)` $\rightarrow$ `IMethodSymbol.Parameters[idx].Name`. | 🟢 **Gut machbar.** `millisecondsTimeout`, `timeout` oder `delay` lassen sich verlässlich identifizieren. Setzt geladenes `SemanticModel` voraus. |
| **Variablenzuweisung (`var port = 8080`)** | `EqualsValueClauseSyntax` $\rightarrow$ `VariableDeclaratorSyntax.Identifier.Text`. | 🟢 **Gut machbar.** Variablenname (z. B. `port`, `connectionString`, `secret`) gibt Aufschluss auf Config-Charakter. |
| **Enum-Kandidaten** | `SwitchStatementSyntax`, `SwitchExpressionSyntax` & `IfStatementSyntax` nach Mehrfachvergleichen durchsuchen. | 🟡 **Mittel.** AST-Vergleich von Variablen-Identifiern gegen Literale in Verzweigungen ist machbar, erfordert aber sauberes Pattern-Matching für komplexe Bools. |
| **Sektionen-Namen in Config (`ApiSettings:BaseUrl`)** | Namen aus Klasse (`UserService`) + Parameter/Variable (`baseUrl`) zusammensetzen. | 🟡 **Heuristisch.** Roslyn liefert den AST-Kontext, die Pfadstruktur (`ApiSettings:BaseUrl`) ist aber ein synthetischer *Vorschlag*. Das LLM nutzt diesen Vorschlag als Orientierung. |
| **Lokalisierung / User-Facing Strings** | Check auf Konstruktoren von Exceptions (`ArgumentException(message)`), UI-Methoden & String-Länge (> 15 Zeichen mit Leerzeichen). | 🟡 **Heuristisch.** Roslyn identifiziert den Methoden-Konstruktor verlässlich. Ob eine Meldung übersetzt werden muss, bleibt eine textuelle Heuristik. |
| **Cross-Project Duplikats-Aggregation** | In-Memory `Solution`-Workspace durchlaufen und Literale in einem `Dictionary<string, List<Location>>` bündeln. | 🟢 **Gut machbar.** AiNetLinter hält die Solution im Speicher. Aggregation ist in $\mathcal{O}(N)$ über alle Literale extrem schnell. |

## Verworfene Alternativen

- **Pauschal-Regex / Grep-Scanner:** verworfen, weil er ohne semantischen Roslyn-Kontext enorm viele False Positives (Testdaten, Attribute, Schleifenindizes) liefert.
- **Automatischer Roslyn CodeFixer im MCP-Tool:** verworfen, weil der KI-Agent im Chat selbst entscheidet, wie und wohin er Werte am saubersten refactored.

## Wo im Projekt

- **MCP-Tool Handler & Scanner:** `src/AiNetLinter/Mcp/` (bzw. Handlers-Verzeichnis für MCP Tools).
- **Roslyn-Walker & Semantic Evaluator:** `src/AiNetLinter/Rules/` bzw. `src/AiNetLinter/Generators/`.
- **Tests:** `src/AiNetLinter.FastTests` (Unit-Scans) & `src/AiNetLinter.IntegrationTests` (MCP JSON-RPC Integration).

## Entdeckte Mängel/Redundanzen

- Keine Mängel im Bestandscode identifiziert; die Ergänzung ist eine rein additive Erweiterung des MCP-Server-Toolsets.

## Wie (grober Ansatz)

1. `SyntaxWalker` durchläuft alle C#-Syntaxbäume der Solution für `LiteralExpressionSyntax`.
2. AST-Filterung schließt Attribute, `const`/`readonly`-Initialisierer und Test-Dateien (sofern `includeTests: false`) aus.
3. Kontextanalyse via `SemanticModel` bestimmt Methoden-Parameternamen und Zuweisungsvariablen für Config- und Timeout-Heuristiken.
4. Duplikate und Kategorien werden über ein In-Memory-Dictionary aggregiert.
5. Das Tool formatiert die Fundstellen als kompaktes JSON-Resultat mit konkreten `suggestedTarget`-Hinweisen.

## Definition of Done / Erfolgskriterien

- `find_magic_values` ist als MCP-Tool in `.mcp.json` / MCP Server Tool-Registry verfügbar.
- Unit- & Integrationstests in `FastTests` und `IntegrationTests` bestätigen korrekte Klassifizierung und Rausch-Filterung.
- Tool-Dokumentation in `Docs/` aktualisiert.

## Offene Punkte

- Optimale Standard-Schwellwerte für `minOccurrences` (Default 2 für Duplikate, 1 für Scans) und String-Mindestlänge für Lokalisierung.
- Exakte Namensgebung des MCP-Tools (`find_magic_values` vs. `analyze_magic_values`).
