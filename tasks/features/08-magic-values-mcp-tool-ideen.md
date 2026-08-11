---
task: magic-values-mcp-tool-ideen
type: ideen-sammlung
status: draft
created: 2026-08-12
updated: 2026-08-12
purpose: Ideensammlung für ein Roslyn-basiertes MCP-Server-Tool zur Erkennung von Magic Values (Strings, Zahlen, Pfade, Timeouts), automatischen Refactoring-Empfehlungen (Constants vs. appsettings.json vs. Enums vs. Localization) und kritische technische Machbarkeitsanalyse.
---

# Magic Values Detection MCP Tool — Ideensammlung & Machbarkeitsanalyse

> **Status:** Ideensammlung & kritische Machbarkeitsanalyse (12.08.2026).
> **Ziel:** Ein gezieltes MCP-Tool (`find_magic_values` / `analyze_magic_values`), das die Codebase nach festen Werten (Literalen) durchsucht, Duplikate identifiziert und Handlungsempfehlungen gibt (z. B. Verschieben in Konstanten-Klasse, `appsettings.json`, Enums oder Lokalisierung).

---

## 1. Motivation & Kernidee

Beim Entwickeln und Refactorn mit KI-Agenten entstehen häufig unbeabsichtigt **Magic Values**:
- Feste Strings (URLs, Pfade, Error-Messages, SQL/Regex-Muster, Config-Keys)
- Feste Zahlen (Timeouts, Batch-Größen, Status-Codes, magische Offsets/Grenzwerte)

Ein dediziertes MCP-Tool soll dem Agenten (und dem Entwickler) auf Anforderung Antworten auf konkrete Fragen liefern:
1. **Wo haben wir Magic Strings / Zahlen im Code, die mehrfach vorkommen und in eine Konstanten-Klasse gehören?**
2. **Welche Werte sind Konfigurationsparameter und sollten in eine `appsettings.json` / `settings.json` ausgelagert werden?**
3. **Welche magischen Zahlen/Strings repräsentieren eigentlich einen Typen-/Zustandssatz und sollten als `enum` refactored werden?**
4. **Welche benutzerseitigen Textnachrichten sollten in Ressourcen/Lokalisierung wandern?**

---

## 2. Strategische Beurteilung & Mehrwert

### 🥇 Warum diese Idee hervorragend ist
- **Kuriert "Lazy Agent Habits":** KI-Agenten wählen beim Schreiben von neuem Code oft den "Weg des geringsten Widerstands" und fügen Literale direkt inline ein. `find_magic_values` gibt dem Agenten ein Werkzeug zur Selbstkorrektur vor Commits.
- **Konstruktiv statt meckernd:** Klassische Linter (Sonar, StyleCop) sagen nur pauschal "Avoid Magic Number". Dieses MCP-Tool klassifiziert den Wert und gibt **konkrete Ziel-Empfehlungen** (`appsettings.json`, `Constants.cs`, `enum`, `.resx`).
- **Token- & Cost-Saver:** Ein einziger Tool-Call (~300 Tokens Output) ersetzt das Scannen von Dutzenden Quellcodedateien im Context.

---

## 3. Kategorisierung & Ziel-Empfehlungen

Ein einfaches "String/Number Grep" erzeugt enorm viel Noise. Das Tool muss über Roslyn-Semantik und Musterschablonen **intelligente Kategorien** bilden:

### A. Konfigurations-Kandidaten (`appsettings.json` / Environment)
- **URLs & Endpunkte:** `"https://..."`, `"http://localhost:5000"`, `"api/v1/..."`
- **Pfade & Dateien:** `"C:\\Data\\..."`, `"/var/logs/..."`, `".tmp/cache"`
- **Connection Strings & Keys:** `"Server=...;Database=..."`, secret/token-Verdacht.
- **Timeouts & Limits:** `TimeSpan.FromSeconds(30)`, `30000` (ms in HTTP-Clients), `MaxRetries = 5`, `BatchSize = 100`.
- **Feature-Flags & Environment-Namen:** `"Development"`, `"Production"`, `"FeatureX_Enabled"`.

### B. Konstanten-Kandidaten (Zentrale `Constants.cs` / `[Domain]Constants.cs`)
- **Mehrmals verwendete Strings/Zahlen:** Der gleiche String/Wert taucht an $\ge 2$ Stellen im Code auf (z. B. HTTP-Header `"X-Correlation-ID"`, Event-Namen, Regex-Patterns).
- **Domänenspezifische Schwellenwerte:** Mathematische/fachliche Werte (z. B. `0.19` für MwSt, `MAX_TITLE_LENGTH = 255`).
- **Standard-Dateiendungen / Format-Strings:** `".json"`, `"yyyy-MM-dd HH:mm:ss"`.

### C. Enum-Kandidaten (`enum`)
- **Diskrete Wertebereiche:** Strings oder Zahlen, die in `switch`-Statements oder `if-else`-Kaskaden verglichen werden (z. B. `"Pending"`, `"Active"`, `"Failed"` oder `1`, `2`, `3` als Status-Codes).

### D. Lokalisierungs-Kandidaten (`IStringLocalizer` / `.resx`)
- **User-Facing Nachrichten:** Lange Strings in Exceptions, UI-Prompts oder Logins (`"Benutzername oder Passwort falsch"`).

### E. Standard-HTTP / System-Standard-Kandidaten
- **HTTP-Statuscodes:** `404`, `500`, `401` $\rightarrow$ Empfehlung: `StatusCodes.Status404NotFound` verwenden.
- **Leere Strings / Whitespace:** `""` $\rightarrow$ Empfehlung: `string.Empty`.

---

## 4. Rausch-Filterung (False Positives Vermeiden)

Damit das MCP-Tool im Agent-Loop nützlich ist und das Token-Budget schont, müssen irrelevante Literale gefilterd werden:

1. **Triviale Werte ignorieren (standardmäßig):**
   - Zahlen: `0`, `1`, `-1` (oft Schleifenindizes oder Initialisierungen).
   - Strings: `""` (sofern nicht explizit gesucht), `" "`, `"\n"`.
   - Bools & Nulls: `true`, `false`, `null`.
2. **Attribut-Argumente isolieren:**
   - Literale in C#-Attributen (`[JsonPropertyName("foo")]`, `[Route("api/[controller]")]`, `[Obsolete("...")]`) erfordern syntaktisch oft Literale und sind meist keine Magic Values im klassischen Sinn.
3. **Tests vs. Production Code:**
   - Option zur Trennung (`includeTests: false` als Default). In Test-Dateien sind Testdaten-Literale gewollt; im Prod-Code sind sie ein Smell.
4. **Bereits definierte Konstanten/Fields ausschließen:**
   - Initialisierer von `const` oder `static readonly` Feldern (z. B. `public const string Version = "1.0";`) sind bereits Konstanten-Definitionen und keine Magic-Value-Verstöße.

---

## 5. Kritische technische Machbarkeitsanalyse (Roslyn-Check)

Kann Roslyn alle vorgeschlagenen Heuristiken und Kontext-Analysen überhaupt technisch verlässlich umsetzen?

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

### Fazit der Machbarkeitsprüfung:
Alle Kernfunktionen sind **zu 100% mit der Roslyn-API im bestehenden AiNetLinter-Server umsetzbar**. Roslyn liefert die exakte AST- und Semantik-Grundlage. Wo Roslyn aufhört (z. B. Wunschnamen in `appsettings.json` oder Lokalisierungsnotwendigkeit), greifen einfache, transparente Heuristiken, die dem KI-Agenten als strukturierte Empfehlungen übergeben werden.

---

## 6. Entwurf der MCP-Tool-Schnittstelle (`find_magic_values`)

```json
{
  "name": "find_magic_values",
  "description": "Analysiert C#-Code nach Magic Values (Literalen), Duplikaten und liefert strukturierte Empfehlungen für Refactoring (Constants, appsettings.json, Enums).",
  "parameters": {
    "scope": "Optional. Projekt- oder Verzeichnispfad.",
    "valueType": "all | strings | numbers (Default: all)",
    "minOccurrences": "Minimales Vorkommen für Duplikats-Filterung (Default: 2 für Duplikate, 1 für Config/Url/Timeout-Scans)",
    "categoryFilter": "all | config_candidates | constant_candidates | enum_candidates | localization_candidates",
    "includeTests": "boolean (Default: false)"
  }
}
```

### Beispielhafter Output für den AI-Agenten:
```json
{
  "summary": {
    "totalMagicValues": 14,
    "configCandidates": 4,
    "constantCandidates": 6,
    "enumCandidates": 4
  },
  "recommendations": [
    {
      "category": "config_candidate",
      "value": "https://api.example.com/v2",
      "suggestedTarget": "appsettings.json (Section: ApiSettings:BaseUrl)",
      "occurrences": 3,
      "locations": [
        "src/Services/UserService.cs:L42",
        "src/Services/OrderService.cs:L88"
      ]
    },
    {
      "category": "constant_candidate",
      "value": "X-Correlation-ID",
      "suggestedTarget": "Constants.Headers.CorrelationId",
      "occurrences": 5,
      "locations": [ ... ]
    }
  ]
}
```

---

## 7. Synergien im AiNetLinter-Ökosystem

- **Ergänzung zu `find_duplicates`:** `find_duplicates` sucht nach redundanten Codeblöcken/AST-Strukturen; `find_magic_values` fokussiert sich auf atomare Datenliterale und deren fachlichen Bestimmungsort.
- **Integration in `safeguard` / `rules.json`:** Optional können schwere Magic-Value-Verstöße (z. B. hardcoded Connection Strings oder Passwörter) als Roslyn-Regeln in `rules.json` eingebunden werden.
- **Agenten-Workflow:** Ein Refactoring-Agent kann vor einer Aufräum-Session `find_magic_values` ausführen, um eine Liste von Aufräum-Tasks (z. B. "Auslagern von 5 URLs in Config") autonom abzuarbeiten.
