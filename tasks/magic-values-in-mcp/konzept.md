---
status: draft
type: konzept
project_kind: brownfield
estimated_scope: medium
rules_dir: .agents/rules
last_updated: 2026-08-12
open_questions:
  - Exakte Namensgebung des MCP-Tools (find_magic_values vs. audit_magic_values).
---

# Konzept: Magic Values On-Demand Audit MCP-Tool

## Ziel (Was)

Ein Roslyn-basiertes MCP-Server-Tool (`find_magic_values`), das als **On-Demand-Audit-Werkzeug** (keine störende/blockierende Linter-Build-Regel) dient. Es befähigt KI-Agenten und Entwickler dazu, auf Anforderung eine C# / .NET 10 Codebase gezielt nach festen Werten (Literalen wie Strings, Zahlen, Pfaden, Timeouts) zu scannen — **auch bei Einzelvorkommen ($\text{minOccurrences} = 1$)** —, diese fachlich zu klassifizieren und strukturierte Refactoring-Empfehlungen (Constants, `appsettings.json`, Enums, `nameof(...)`, Lokalisierung) zu liefern.

Fundstellen, die bewusst im Code verbleiben sollen (False Positives oder beabsichtigte Literale), können über den bestehenden AiNetLinter-Suppression-Mechanismus (`// ainetlinter-disable MagicValues`) dauerhaft stumm geschaltet werden, damit sie in Folge-Audits nicht wiederholt gemeldet werden.

## Warum / Kontext

Beim Entwickeln und Refactorn mit KI-Agenten entstehen häufig unbeabsichtigt **Magic Values**:
- Feste Strings (URLs, Pfade, Error-Messages, SQL/Regex-Muster, Config-Keys)
- Feste Zahlen (Timeouts, Batch-Größen, Status-Codes, magische Offsets/Grenzwerte)

### Warum ein Audit-Tool statt einer Linter-Regel?
- **Keine "Linter-Fatigue":** Klassische Linter, die bei jedem Build hunderte Warnungen für Literale werfen, werden von Entwicklern schnell ignoriert oder deaktiviert.
- **On-Demand im Agent-Loop:** Das MCP-Tool wird gezielt vom Agenten aufgerufen, wenn dieser einen Refactoring- oder Qualitätssicherungs-Audit durchführt.
- **Vollständigkeit bei Einzelvorkommen ($\text{minOccurrences} = 1$):** Schon ein einziger hardcodierter API-Endpunkt (`"https://api.example.com"`), Timeout (`30000` ms) oder Connection-String ist kritisch und muss ausgelagert werden. Das Tool darf nichts übersehen, nur weil ein Wert erst 1x vorkommt.
- **Entscheidungsunterstützung:** Das Tool gibt **konkrete Ziel-Empfehlungen** (`appsettings.json`, `Constants.cs`, `enum`, `nameof(...)`, `.resx`) statt blinder Meckerei.

## Erkenntnisse aus der `src/`-Bestandscode-Analyse (Erweiterte Muster & Edge Cases)

Bei einer Durchsuchung der eigenen Codebase (`src/`) nach bestehenden Literalen und Magic Values sind folgende praxisrelevanten Muster aufgefallen:

1. **In-String-Magic-Values & Interpolations-Fragmente (`$"..."`):**
   - *Fundstelle:* In `HotspotMapBuilder.cs` und `GetHotspotsScanner.cs` steht `private const double WarnThreshold = 0.80;`, gleichzeitig wird in Strings inline `">80% des Limits"` hartcodiert.
   - *Roslyn-Bedarf:* Der Scanner muss auch statische Textteile in `InterpolatedStringExpressionSyntax` (`$"..."`) analysieren.
2. **Duplizierte `private const`-Definitionen über Klassengrenzen hinweg:**
   - *Fundstelle:* `WarnThreshold = 0.80` ist in `HotspotMapBuilder.cs` und `GetHotspotsScanner.cs` als jeweils lokales `private const` definiert.
   - *Erweiterte Heuristik:* Das MCP-Tool sollte nicht nur anonyme Literale finden, sondern auch warnen, wenn **mehrere private/internal `const` Felder in verschiedenen Klassen identische Werte definieren** und die Hochstufung in eine gemeinsame Konstanten-Klasse empfehlen.
3. **`nameof(...)`-Kandidaten:**
   - *Fundstelle:* Parameter- und Typ-Namen in Exceptions, Loggern oder Sentinel-String-Vergleichen (z. B. `"StaticTestSentinel"` oder Parameter-Strings).
   - *Neue Kategorie `nameof_candidates`:* String-Literale, die exakt einem Parameter, Member oder Typnamen im aktuellen Scope entsprechen, werden als Kandidat für `nameof(...)` klassifiziert.
4. **C# / .NET 10 Raw String Literals (`"""..."""`):**
   - *Roslyn-Bedarf:* Unterstützung von C# 11/12/13/14 Raw String Literals, die für JSON, Multi-Line-Prompts oder SQL verwendet werden.

## Scope

### Muss-Haben

- **Vollständige Erfassung (Default: `minOccurrences = 1`):**
  - Alle Literale erfassen, auch wenn sie nur 1x im Code stehen.
- **Dauerhafte Unterdrückung (Suppression-Support):**
  - Unterdrückung über das bestehende AiNetLinter-Kommentarsystem: `// ainetlinter-disable MagicValues` (oder `/* ainetlinter-disable MagicValues */`).
  - Wenn ein Entwickler/Agent ein Vorkommen als beabsichtigt oder False Positive bewertet und kommentiert, wird es in allen zukünftigen MCP-Audits ignoriert (`includeSuppressed: false` als Default).
- **Kategorisierung & Ziel-Empfehlungen:**
  - **Konfigurations-Kandidaten (`appsettings.json` / Environment):** URLs (`"https://..."`), Pfade (`"C:\\Data\\..."`), Connection Strings/Keys, Timeouts/Limits (`TimeSpan.FromSeconds(30)`, `30000` ms, `MaxRetries = 5`), Feature-Flags.
  - **Konstanten-Kandidaten (Zentrale `Constants.cs` / `[Domain]Constants.cs`):** Mehrmals oder prägnant verwendete Strings/Zahlen (z. B. `"X-Correlation-ID"`), domänenspezifische Schwellenwerte (`0.19`), Format-Strings (`"yyyy-MM-dd"`), sowie duplizierte `private const`-Felder in mehreren Klassen.
  - **`nameof(...)`-Kandidaten:** Strings, die Variablennamen, Parametern oder Typnamen im aktuellen Scope entsprechen (`throw new ArgumentNullException("foo")` $\rightarrow$ `nameof(foo)`).
  - **Enum-Kandidaten (`enum`):** Diskrete Wertebereiche in `switch`-Statements oder `if-else`-Kaskaden (`"Pending"`, `"Active"`, `"Failed"` oder `1`, `2`, `3`).
  - **Lokalisierungs-Kandidaten (`IStringLocalizer` / `.resx`):** User-Facing Nachrichtentexte in Exceptions, UI-Prompts oder Logins.
  - **Standard-HTTP / System-Standard:** HTTP-Statuscodes (`404`, `500` $\rightarrow$ `StatusCodes.Status404NotFound`), Leere Strings (`""` $\rightarrow$ `string.Empty`).
- **Rausch-Filterung (False Positives Vermeidung):**
  - Triviale Werte ignorieren (`0`, `1`, `-1`, `""`, `" "`, `"\n"`, `true`, `false`, `null`).
  - Attribut-Argumente isolieren (`[JsonPropertyName("foo")]`, `[Route("...")]`, `[Obsolete("...")]`).
  - Tests vs. Production Code Trennung (`includeTests: false` als Default).
  - Bereits definierte *eindeutige* Konstanten ausschließen (außer bei Duplikation über verschiedene Klassen hinweg).
- **MCP-Tool-Schnittstelle (`find_magic_values`):**
  - Parameter: `scope`, `valueType` (`all|strings|numbers`), `minOccurrences` (Default: 1), `categoryFilter`, `includeTests` (Default: false), `includeSuppressed` (Default: false).
  - Strukturierter JSON-Output mit `summary` und `recommendations` (inkl. `category`, `value`, `suggestedTarget`, `occurrences`, `locations`, `isSuppressed`).

### Agenten-Audit Workflow

1. **Beauftragung:** Der Entwickler beauftragt den Agenten mit einem Audit (z. B. *"Führe einen Magic-Values-Audit durch und bereinige Config-Parameter"*).
2. **Abfrage:** Der Agent ruft das MCP-Tool `find_magic_values` auf.
3. **Bewertung & Aktion:**
   - **Echter Magic Value:** Agent verlagert den Wert in `appsettings.json`, `Constants.cs`, `nameof(...)` etc.
   - **Beabsichtigter / Akzeptierter Wert:** Agent versieht die Stelle mit `// ainetlinter-disable MagicValues`.
4. **Ergebnis:** Folgende Audit-Scans bleiben 100% sauber und frei von wiederkehrenden Meldungen.

### Nice-to-Have (Zwischenspeicher — vor `status: ready` aufgelöst)

*Keine offenen Nice-to-Have-Punkte.*

### Non-Goals (bewusst NICHT Teil davon)

- **Kein automatisches Linter-Build-Erroring:** Kein Blockieren von Builds oder Fluten der IDE-Fehlerliste. Der Scan ist rein auf Anforderung (MCP).
- **Keine automatischen Code-Fixer direkt im MCP-Tool:** Der MCP-Server liefert Evidenz & Empfehlungen; das Refactoring oder Einfügen von Suppression-Kommentaren führt der Agent durch.
- **Keine Redundanz zu `find_duplicates`:** `find_duplicates` sucht nach AST-Strukturblöcken; `find_magic_values` fokussiert sich atomar auf Datenliterale und deren fachlichen Bestimmungsort.

## Zielplattformen / Technischer Rahmen

- **Stack:** C# / .NET 10, Roslyn AST (`SyntaxWalker`) & `SemanticModel` APIs.
- **Integration:** Einbindung in den AiNetLinter MCP-Server (`src/AiNetLinter`).

## Machbarkeitsanalyse (Roslyn-Check)

| Feature / Vorschlag | Technischer Roslyn-Mechanismus | Umsetzbarkeit & Grenzen |
| :--- | :--- | :--- |
| **Literal-Erkennung & AST-Scan** | `SyntaxWalker` über `LiteralExpressionSyntax`, Raw String Literals & `InterpolatedStringExpressionSyntax`. | 🟢 **100% machbar.** Sehr schnell & trivial. |
| **Suppression-Auswertung (`// ainetlinter-disable`)** | Einbindung des bestehenden `SuppressionScanner` / `Trivia` Check in Roslyn. | 🟢 **100% machbar.** Die Engine existiert bereits im Codebase (`src/AiNetLinter/Suppression`). |
| **`nameof(...)`-Erkennung** | Scope-Check des AST-Knotens gegen Parameter, lokale Variablen & Member-Identifier. | 🟢 **Gut machbar.** Prüft, ob String-Inhalt einem Symbol-Namen im aktuellen Context entspricht. |
| **Erkennung duplizierter `private const`** | Aggregation von `FieldDeclarationSyntax` mit `const`-Modifier über mehrere Klassen. | 🟢 **Gut machbar.** Erkennt doppelt definierte Konstanten (wie `WarnThreshold = 0.80`). |
| **Attribut-Filtern** | Prüfen, ob `node.FirstAncestorOrSelf<AttributeSyntax>() != null`. | 🟢 **100% machbar.** Reine AST-Prüfung ohne Semantik. |
| **Parameter-Kontext (z. B. `Thread.Sleep(5000)`)** | `SemanticModel.GetSymbolInfo(invocation)` $\rightarrow$ `IMethodSymbol.Parameters[idx].Name`. | 🟢 **Gut machbar.** `millisecondsTimeout`, `timeout` oder `delay` lassen sich verlässlich identifizieren. |
| **Variablenzuweisung (`var port = 8080`)** | `EqualsValueClauseSyntax` $\rightarrow$ `VariableDeclaratorSyntax.Identifier.Text`. | 🟢 **Gut machbar.** Variablenname (z. B. `port`, `connectionString`, `secret`) gibt Aufschluss auf Config-Charakter. |
| **Enum-Kandidaten** | `SwitchStatementSyntax`, `SwitchExpressionSyntax` & `IfStatementSyntax` nach Mehrfachvergleichen durchsuchen. | 🟡 **Mittel.** AST-Vergleich von Variablen-Identifiern gegen Literale in Verzweigungen ist machbar. |
| **Sektionen-Namen in Config (`ApiSettings:BaseUrl`)** | Namen aus Klasse (`UserService`) + Parameter/Variable (`baseUrl`) zusammensetzen. | 🟡 **Heuristisch.** Roslyn liefert den AST-Kontext, die Pfadstruktur (`ApiSettings:BaseUrl`) ist ein synthetischer *Vorschlag*. |
| **Lokalisierung / User-Facing Strings** | Check auf Konstruktoren von Exceptions (`ArgumentException(message)`), UI-Methoden & String-Länge (> 15 Zeichen). | 🟡 **Heuristisch.** Roslyn identifiziert den Methoden-Konstruktor verlässlich. |
| **Cross-Project Duplikats- & Einzelwert-Aggregation** | In-Memory `Solution`-Workspace durchlaufen und Literale bündeln. | 🟢 **Gut machbar.** In-Memory-Aggregation über Solution-Workspace ist extrem schnell. |

## Verworfene Alternativen

- **Kontinuierliche Linter-Build-Regel:** verworfen, weil Entwickler bei hunderten Literal-Warnungen im normalen Build Linter-Fatigue entwickeln.
- **Pauschal-Regex / Grep-Scanner:** verworfen, weil er ohne semantischen Roslyn-Kontext enorm viele False Positives (Testdaten, Attribute, Schleifenindizes) liefert.
- **Automatischer Roslyn CodeFixer im MCP-Tool:** verworfen, weil der KI-Agent im Chat selbst entscheidet, wie und wohin er Werte am saubersten refactored.

## Wo im Projekt

- **MCP-Tool Handler & Scanner:** `src/AiNetLinter/Mcp/` (bzw. Handlers-Verzeichnis für MCP Tools).
- **Roslyn-Walker & Semantic Evaluator:** `src/AiNetLinter/Rules/` bzw. `src/AiNetLinter/Generators/`.
- **Suppression-Engine Wiederverwendung:** `src/AiNetLinter/Suppression/` (`SuppressionScanner`, `SuppressionEvaluator`).
- **Tests:** `src/AiNetLinter.FastTests` (Unit-Scans) & `src/AiNetLinter.IntegrationTests` (MCP JSON-RPC Integration).

## Entdeckte Mängel/Redundanzen

- Keine Mängel im Bestandscode identifiziert; Wiederverwendung der bereits existierenden `Suppression`-Engine verhindert Code-Duplikation.

## Wie (grober Ansatz)

1. `SyntaxWalker` durchläuft alle C#-Syntaxbäume der Solution für `LiteralExpressionSyntax` (inkl. Raw String Literals & static text in `InterpolatedStringExpressionSyntax`).
2. AST-Filterung schließt Attribute, eindeutige `const`/`readonly`-Initialisierer und Test-Dateien (sofern `includeTests: false`) aus.
3. Check gegen `SuppressionScanner`: Zeilen mit `// ainetlinter-disable MagicValues` werden ausgeblendet (`includeSuppressed: false`).
4. Kontextanalyse via `SemanticModel` bestimmt Methoden-Parameternamen, `nameof(...)`-Übereinstimmungen und Zuweisungsvariablen für Config-, Timeout- und Enum-Heuristiken.
5. Duplikate, Einzelwerte und doppelte `private const` Felder werden aggregiert (`minOccurrences = 1` als Standard).
6. Das Tool formatiert die Fundstellen als kompaktes JSON-Resultat mit konkreten `suggestedTarget`-Hinweisen.

## Definition of Done / Erfolgskriterien

- `find_magic_values` ist als MCP-Tool in `.mcp.json` / MCP Server Tool-Registry verfügbar.
- Target Framework .NET 10 wird unterstützt.
- Suppress-Kommentare (`// ainetlinter-disable MagicValues`) schalten Fundstellen zuverlässig stumm.
- Scans mit `minOccurrences = 1` finden auch Einzelvorkommen treffsicher.
- `nameof(...)`-Kandidaten und duplizierte `private const`-Felder werden erkannt.
- Unit- & Integrationstests in `FastTests` und `IntegrationTests` bestätigen korrekte Klassifizierung und Rausch-Filterung.
- Tool-Dokumentation in `Docs/` aktualisiert.

## Offene Punkte

- Exakte Namensgebung des MCP-Tools (`find_magic_values` vs. `audit_magic_values`).
