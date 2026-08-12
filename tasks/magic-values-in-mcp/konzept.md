---
status: draft
type: konzept
project_kind: brownfield
estimated_scope: medium
rules_dir: .agents/rules
last_updated: 2026-08-12
open_questions: []
---

# Konzept: Magic Values On-Demand Audit MCP-Tool

## Ziel (Was)

Ein Roslyn-basiertes MCP-Server-Tool (`find_magic_values`), das als **On-Demand-Audit-Werkzeug** (keine störende/blockierende Linter-Build-Regel) dient. Es befähigt KI-Agenten und Entwickler dazu, auf Anforderung eine C# / .NET 10 Codebase gezielt nach festen Werten (Literalen wie Strings, Zahlen, Pfaden, Timeouts) zu scannen — **auch bei Einzelvorkommen ($\text{minOccurrences} = 1$)** —, diese fachlich zu klassifizieren und strukturierte Refactoring-Empfehlungen (Constants, `appsettings.json`, Enums, `nameof(...)`, Lokalisierung, Security-Warnung bei Secrets) zu liefern.

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
- **Security-Priorität:** Hartcodierte Secrets/Credentials (CWE-798) sind ein Sonderfall mit höherer Dringlichkeit als ein gewöhnlicher Config-Kandidat — alle etablierten SAST-Tools (SonarQube, Semgrep, Checkmarx) führen das als eigene Hochrisiko-Kategorie. Deshalb eigene Kategorie `security_candidates` mit eigener Severity statt Vermischung mit `config_candidates`.

## Erkenntnisse aus der `src/`-Bestandscode-Analyse (Erweiterte Muster & Edge Cases)

Bei einer Durchsuchung der eigenen Codebase (`src/`) nach bestehenden Literalen und Magic Values sind folgende praxisrelevanten Muster aufgefallen:

1. **Einheitliche `maxResults`-Kappung (`McpTruncation`):**
   - *Bestandscode-Konvention:* Alle bestehenden AiNetLinter MCP-Tools (`get_violations`, `find_symbol`, `find_references`, `find_duplicates`, `search_pattern`) nutzen konsistent `maxResults` (Default: 50 bzw. 20) und kappen große Ergebnismengen mit der zentralen `McpTruncation`-Hilfsklasse (exakter Text laut `McpTruncation.cs`: `"[N Treffer gesamt, M gezeigt — Pattern verfeinern oder maxResults erhöhen]"`).
   - *Übernahme:* `find_magic_values` übernimmt exakt diesen Standard-Parameter `maxResults` (Default: 50), um den Agenten vor riesigen JSON-Dumps zu schützen.
2. **In-String-Magic-Values & Interpolations-Fragmente (`$"..."`):**
   - *Fundstelle:* In `HotspotMapBuilder.cs` und `GetHotspotsScanner.cs` steht `private const double WarnThreshold = 0.80;`, gleichzeitig wird in Strings inline `">80% des Limits"` hartcodiert.
   - *Roslyn-Bedarf:* Der Scanner muss auch statische Textteile in `InterpolatedStringExpressionSyntax` (`$"..."`) analysieren.
3. **Duplizierte `private const`-Felder über Klassengrenzen hinweg:**
   - *Fundstelle:* `WarnThreshold = 0.80` ist in `HotspotMapBuilder.cs` und `GetHotspotsScanner.cs` als jeweils lokales `private const` definiert.
   - *Erweiterte Heuristik:* Das MCP-Tool sollte nicht nur anonyme Literale finden, sondern auch warnen, wenn **mehrere private/internal `const` Felder in verschiedenen Klassen identische Werte definieren** und die Hochstufung in eine gemeinsame Konstanten-Klasse empfehlen.
4. **`nameof(...)`-Kandidaten:**
   - *Fundstelle:* Parameter- und Typ-Namen in Exceptions, Loggern oder Sentinel-String-Vergleichen (z. B. `"StaticTestSentinel"` oder Parameter-Strings).
   - *Neue Kategorie `nameof_candidates`:* String-Literale, die exakt einem Parameter, Member oder Typnamen im aktuellen Scope entsprechen, werden als Kandidat für `nameof(...)` klassifiziert.
5. **C# / .NET 10 Raw String Literals (`"""..."""`):**
   - *Roslyn-Bedarf:* Unterstützung von C# 11/12/13/14 Raw String Literals, die für JSON, Multi-Line-Prompts oder SQL verwendet werden.

## Scope

### Muss-Haben

- **Vollständige Erfassung (Default: `minOccurrences = 1`):**
  - Alle Literale erfassen, auch wenn sie nur 1x im Code stehen.
- **Kappung großer Dumps (`maxResults: 50` Default via `McpTruncation`):**
  - Schützt das Context-Window des Agenten. Übersteigt die Trefferzahl `maxResults`, wird das Resultat sauber gekappt und eine informative Meta-Zeile angehängt (`"[N Treffer gesamt, M gezeigt — scopeFilter/categoryFilter verfeinern oder maxResults erhöhen]"`).
- **Strukturierte Response als Objekt-Wrapper:**
  - `StructuredContent` liefert ein Objekt (`{ MagicValues: [...] }`), niemals ein nacktes Array — Konsistenz mit `get_violations` (`{ Violations: [...] }`). Ein nacktes Array in `StructuredContent` war bei einem anderen AiNetLinter-Tool bereits einmal eine reale Fehlerquelle.
- **Dauerhafte Unterdrückung (Suppression-Support):**
  - Unterdrückung über das bestehende AiNetLinter-Kommentarsystem: `// ainetlinter-disable MagicValues` (oder `/* ainetlinter-disable MagicValues */`).
  - *Architektur-Entscheidung:* Anstatt den globalen zeilenbasierten `SuppressionScanner` zu nutzen, werten wir direkt im `SyntaxWalker` die Roslyn `SyntaxTrivia` (Kommentare) am jeweiligen SyntaxNode aus. Das ist signifikant performanter (da wir den AST ohnehin durchlaufen) und erlaubt eine präzise knotenbasierte Evaluierung ohne zusätzliche File-IO-Leseoperationen.
  - Wenn ein Entwickler/Agent ein Vorkommen als beabsichtigt oder False Positive bewertet und kommentiert, wird es in allen zukünftigen MCP-Audits ignoriert (`includeSuppressed: false` als Default).
- **Ziel-Fokus (Nur C# Code):**
  - Der Scan beschränkt sich streng auf **reine C#-Dateien** und deren AST.
  - Razor (`.razor`), HTML, CSS, WPF/XAML, Blazor oder JavaScript werden *nicht* durchsucht, da diese Dateitypen gänzlich andere Parser, Semantiken und Interpolations-Muster benötigen. Dieser strenge Fokus garantiert 100% korrekte Empfehlungen für den Backend-Code.
- **Gezielte Parameter-Steuerung für Audits:**
  - Der Aufrufer (Agent/Entwickler) kann den Audit gezielt nach Typen (`strings`, `numbers`, `all`) und fachlichen Kategorien filtern, um Token-Budget zu sparen und schrittweise vorzugehen.
  - **Diff-Scope (`changedOnly`):** Zusätzlich zum Pfad-Filter kann der Scan auf per Git-Diff geänderte Dateien beschränkt werden — Wiederverwendung derselben Diff-Scanning-Logik wie im bestehenden `get_impact`-Tool. Damit können Agenten nach jedem Änderungs-Batch gezielt nur neu eingeführte Magic Values prüfen, ohne die ganze Solution erneut zu scannen.
- **Kategorisierung & Ziel-Empfehlungen:**
  - **Konfigurations-Kandidaten (`appsettings.json` / Environment):** URLs (`"https://..."`), Pfade (`"C:\\Data\\..."`), Connection Strings/Keys, Timeouts/Limits (`TimeSpan.FromSeconds(30)`, `30000` ms, `MaxRetries = 5`), Feature-Flags.
  - **Konstanten-Kandidaten (Zentrale `Constants.cs` / `[Domain]Constants.cs`):** Mehrmals oder prägnant verwendete Strings/Zahlen (z. B. `"X-Correlation-ID"`), domänenspezifische Schwellenwerte (`0.19`), Format-Strings (`"yyyy-MM-dd"`), sowie duplizierte `private const`-Felder in mehreren Klassen.
  - **`nameof(...)`-Kandidaten:** Strings, die Variablennamen, Parametern oder Typnamen im aktuellen Scope entsprechen (`throw new ArgumentNullException("foo")` $\rightarrow$ `nameof(foo)`).
  - **Enum-Kandidaten (`enum`):** Diskrete Wertebereiche in `switch`-Statements oder `if-else`-Kaskaden (`"Pending"`, `"Active"`, `"Failed"` oder `1`, `2`, `3`).
  - **Lokalisierungs-Kandidaten (`IStringLocalizer` / `.resx`):** User-Facing Nachrichtentexte in Exceptions, UI-Prompts oder Logins.
  - **Standard-Kandidaten (`standard_candidates`):** HTTP-Statuscodes (`404`, `500` $\rightarrow$ `StatusCodes.Status404NotFound`). *(Leere Strings `""` fallen unter Rausch-Filterung/Trivialwerte und werden hier bewusst NICHT gemeldet — Auflösung eines Widerspruchs aus einer früheren Fassung dieses Dokuments, siehe unten.)*
  - **Security-Kandidaten (`security_candidates`):** Hartcodierte Secrets/Credentials (CWE-798) — erkannt über Namens-Heuristik (Parameter/Variable/Feld heißt `password`, `secret`, `apiKey`, `token`, `connectionString` o. ä.) kombiniert mit einem nicht-trivialen String-Literal als Wert, plus bekannte Präfix-Muster (`AKIA...`, `sk-...`). Eigene, höhere Severity als bei normalen Config-Kandidaten.
- **Rausch-Filterung (False Positives Vermeidung - SEHR WICHTIG):**
  - Triviale Werte ignorieren (`0`, `1`, `-1`, `""`, `" "`, `"\n"`, `true`, `false`, `null`).
  - **Index- & Schleifen-Ausnahmen:** Literale, die als Array-Index (`args[2]`), Tuple-Zugriff (`Item3`) oder in einfachen Schleifen-Initialisierungen (`for (int i = 2; ...)`) verwendet werden, ignorieren. Hier handelt es sich meist um strukturelle Navigations-Zahlen, die nicht sinnvoll durch benannte Konstanten ersetzt werden können.
  - **Exit-Codes & Mathematik:** Einfache Rückgabewerte wie `Environment.ExitCode = 2` oder Faktoren wie `radius * 2` sollten idealerweise durch Heuristiken (z.B. check auf mathematische Operatoren oder `ExitCode`) in der Basis-Stufe entschlackt werden.
  - **Konfigurierbare Well-Known-Number-Ignore-Liste:** Zusätzlich zur festen Trivial-Liste (`0`, `1`, `-1`, ...) kann der Aufrufer über `ignoreNumbers` etablierte Zeit-/Bit-Konstanten (z. B. `24`, `60`, `360`, `1000`) ergänzen, die projektspezifisch nicht als Magic Value gelten sollen — analog zu Checkstyles `ignoreNumbers`/PMDs `ignoreMagicNumbers`.
  - **`GetHashCode()`-Sonderfall:** Literale innerhalb eines `GetHashCode()`-Overrides (z. B. idiomatische Primzahlen wie `17`, `23`, `31` in `hash = hash * 31 + ...`) werden automatisch ignoriert, unabhängig von der Ignore-Liste.
  - Attribut-Argumente isolieren (`[JsonPropertyName("foo")]`, `[Route("...")]`, `[Obsolete("...")]`).
  - Tests vs. Production Code Trennung (`includeTests: false` als Default).
  - Bereits definierte *eindeutige* Konstanten ausschließen (außer bei Duplikation über verschiedene Klassen hinweg).

### MCP-Tool Schnittstellen-Spezifikation (`find_magic_values`)

```json
{
  "name": "find_magic_values",
  "description": "Führt einen On-Demand-Audit nach Magic Values (Strings, Zahlen, Pfaden, Timeouts, Secrets) in C#-Quellcode durch und liefert fachlich klassifizierte Refactoring-Empfehlungen.",
  "parameters": {
    "scopeFilter": "Optional. Pfad/Projekt-Filter (z. B. 'src/AiNetLinter/Mcp'). Benennung konsistent mit scopeFilter bei get_violations/pattern_detect.",
    "changedOnly": "boolean (Default: false). Beschränkt den Scan auf per Git-Diff geänderte Dateien (Wiederverwendung der get_impact-Diff-Logik) — für gezielte Audits direkt nach einem Änderungs-Batch.",
    "valueType": "all | strings | numbers (Default: 'all'). Filtert nach Datentyp der Literale.",
    "categoryFilter": "all | config_candidates | constant_candidates | enum_candidates | nameof_candidates | localization_candidates | standard_candidates | security_candidates (Default: 'all'). Filtert nach fachlichem Refactoring-Ziel.",
    "minOccurrences": "Minimales Vorkommen (Default: 1 für alle Einzelwerte; kann z. B. auf 2 gesetzt werden um nur Duplikate zu finden).",
    "maxResults": "Maximale Anzahl der zurückgegebenen Ergebnisse (Default: 50). Schützt vor Context-Window-Überlauf.",
    "includeTests": "boolean (Default: false). Einbeziehung von Test-Projekten.",
    "includeSuppressed": "boolean (Default: false). Einbeziehung von '// ainetlinter-disable MagicValues' markierten Stellen.",
    "ignoreNumbers": "number[] (Optional). Ergänzt die eingebaute Trivial-Liste um projektspezifische 'well-known' Zahlen (z. B. [24, 60, 360, 1000])."
  }
}
```

### Agenten-Audit Workflow

1. **Beauftragung & Parameterwahl:** Der Entwickler beauftragt den Agenten mit einem fokussierten Audit (z. B. *"Prüfe alle magischen Zahlen/Timeouts"* oder *"Lagere alle Config-Parameter aus"*).
2. **Zielgerichtete MCP-Abfrage:** 
   - Agent ruft z. B. `find_magic_values(valueType="numbers", maxResults=50)` auf.
   - Oder `find_magic_values(categoryFilter="config_candidates")`.
   - Oder direkt nach einem Änderungs-Batch: `find_magic_values(changedOnly=true)` — prüft nur neu eingeführte Magic Values in geänderten Dateien.
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
- **Kein automatisches Blockieren bei Security-Funden:** Auch `security_candidates` führen nur zu einer Meldung mit höherer Severity, nicht zu einem automatischen Commit-Block oder einer Integration in ein SIEM/Secret-Scanning-System — bleibt konsistent mit dem generellen Non-Goal "kein blockierendes Linting".
- **Kein echter Entropie-Algorithmus für Secret-Erkennung:** `security_candidates` nutzt reine Namens-/Muster-Heuristik (Feldname + bekannte Präfixe), keine statistische Entropie-Berechnung wie dedizierte Secret-Scanner (z. B. TruffleHog/Gitleaks). Deckt die offensichtlichen Fälle ab, ersetzt aber kein dediziertes Secret-Scanning-Tool.

## Zielplattformen / Technischer Rahmen

- **Stack:** C# / .NET 10, Roslyn AST (`SyntaxWalker`) & `SemanticModel` APIs.
- **Integration:** Einbindung in den AiNetLinter MCP-Server (`src/AiNetLinter`).

## Machbarkeitsanalyse (Roslyn-Check)

| Feature / Vorschlag | Technischer Roslyn-Mechanismus | Umsetzbarkeit & Grenzen |
| :--- | :--- | :--- |
| **Literal-Erkennung & AST-Scan** | `SyntaxWalker` über `LiteralExpressionSyntax`, Raw String Literals & `InterpolatedStringExpressionSyntax`. | 🟢 **100% machbar.** Sehr schnell & trivial. |
| **Typ- & Kategorie-Filterung** | Auswertung der Parameter `valueType` und `categoryFilter` direkt im AST-Walker / Evaluation Pass. | 🟢 **100% machbar.** Spart Tokens & Rechenzeit. |
| **`maxResults`-Kappung (`McpTruncation`)** | Wiederverwendung der `McpTruncation`-Klasse der AiNetLinter MCP Engine. | 🟢 **100% machbar.** Engine bereits vorhanden. |
| **Suppression-Auswertung (`// ainetlinter-disable`)** | Auswertung der `SyntaxTrivia` (Kommentare) direkt am besuchten Roslyn `SyntaxNode`. | 🟢 **100% machbar.** Sehr performant, da im AST-Durchlauf integriert; ersetzt den zeilenbasierten Legacy-`SuppressionScanner` für dieses Tool. |
| **`nameof(...)`-Erkennung** | Scope-Check des AST-Knotens gegen Parameter, lokale Variablen & Member-Identifier. | 🟢 **Gut machbar.** Prüft, ob String-Inhalt einem Symbol-Namen im aktuellen Context entspricht. |
| **Erkennung duplizierter `private const`** | Aggregation von `FieldDeclarationSyntax` mit `const`-Modifier über mehrere Klassen. | 🟢 **Gut machbar.** Erkennt doppelt definierte Konstanten (wie `WarnThreshold = 0.80`). |
| **Attribut-Filtern** | Prüfen, ob `node.FirstAncestorOrSelf<AttributeSyntax>() != null`. | 🟢 **100% machbar.** Reine AST-Prüfung ohne Semantik. |
| **Parameter-Kontext (z. B. `Thread.Sleep(5000)`)** | `SemanticModel.GetSymbolInfo(invocation)` $\rightarrow$ `IMethodSymbol.Parameters[idx].Name`. | 🟢 **Gut machbar.** `millisecondsTimeout`, `timeout` oder `delay` lassen sich verlässlich identifizieren. |
| **Variablenzuweisung (`var port = 8080`)** | `EqualsValueClauseSyntax` $\rightarrow$ `VariableDeclaratorSyntax.Identifier.Text`. | 🟢 **Gut machbar.** Variablenname (z. B. `port`, `connectionString`, `secret`) gibt Aufschluss auf Config-Charakter. |
| **Enum-Kandidaten** | `SwitchStatementSyntax`, `SwitchExpressionSyntax` & `IfStatementSyntax` nach Mehrfachvergleichen durchsuchen. | 🟡 **Mittel.** AST-Vergleich von Variablen-Identifiern gegen Literale in Verzweigungen ist machbar. |
| **Sektionen-Namen in Config (`ApiSettings:BaseUrl`)** | Namen aus Klasse (`UserService`) + Parameter/Variable (`baseUrl`) zusammensetzen. | 🟡 **Heuristisch.** Roslyn liefert den AST-Kontext, die Pfadstruktur (`ApiSettings:BaseUrl`) ist ein synthetischer *Vorschlag*. |
| **Lokalisierung / User-Facing Strings** | Check auf Konstruktoren von Exceptions (`ArgumentException(message)`), UI-Methoden & String-Länge (> 15 Zeichen). | 🟡 **Heuristisch.** Roslyn identifiziert den Methoden-Konstruktor verlässlich. |
| **Cross-Project Duplikats- & Einzelwert-Aggregation** | In-Memory `Solution`-Workspace durchlaufen und Literale bündeln. | 🟢 **Gut machbar.** In-Memory-Aggregation über Solution-Workspace ist extrem schnell. |
| **Diff-Scope (`changedOnly`)** | Wiederverwendung der bestehenden Git-Diff-Scanning-Logik aus `get_impact`. | 🟢 **Gut machbar.** Mechanismus existiert bereits im Projekt, nur auf Literal-Scan statt Signatur-Diff anzuwenden. |
| **Security-Kandidaten (Secrets)** | Namens-Heuristik (Parameter/Feld/Variable via `SemanticModel`) kombiniert mit String-Muster-Check (Präfixe, Länge, Zeichensatz) auf `LiteralExpressionSyntax`. | 🟡 **Heuristisch.** Kein echter Entropie-Algorithmus in Scope (Non-Goal), reine Namens-/Muster-Heuristik — reduziert False Negatives nicht auf SAST-Tool-Niveau, deckt aber die offensichtlichen Fälle ab. |

## Verworfene Alternativen

- **Kontinuierliche Linter-Build-Regel:** verworfen, weil Entwickler bei hunderten Literal-Warnungen im normalen Build Linter-Fatigue entwickeln.
- **Pauschal-Regex / Grep-Scanner:** verworfen, weil er ohne semantischen Roslyn-Kontext enorm viele False Positives (Testdaten, Attribute, Schleifenindizes) liefert.
- **Automatischer Roslyn CodeFixer im MCP-Tool:** verworfen, weil der KI-Agent im Chat selbst entscheidet, wie und wohin er Werte am saubersten refactored.
- **Wiederverwendung der dateiweiten `SuppressionScanner`-Semantik für dieses Tool:** verworfen. Der bestehende `SuppressionScanner` unterdrückt eine Regel für die **gesamte Datei**, sobald irgendwo ein `// ainetlinter-disable`-Kommentar steht (`SuppressionEvaluator.cs`). Bei dutzenden Magic-Value-Funden pro Datei wäre das praktisch nutzlos — ein einzelner Kommentar würde alle Funde der Datei stumm schalten. `find_magic_values` nutzt deshalb bewusst eine abweichende, feinere Granularität (pro Fundstelle via `SyntaxTrivia`). Diese Abweichung von der sonst projektweiten Suppression-Semantik ist eine bewusste Ausnahme und muss in `Docs/agent-api.md` explizit als solche benannt werden, damit sie nicht als Inkonsistenz missverstanden wird.

## Wo im Projekt

- **MCP-Tool Handler & Scanner:** `src/AiNetLinter/Mcp/Tools/MagicValues/`.
- **Roslyn-Walker & Semantic Evaluator:** `src/AiNetLinter/Mcp/Tools/MagicValues/`.
- **Truncation Wiederverwendung:** `src/AiNetLinter/Mcp/McpTruncation.cs`.
- **Tool-Registrierung:** `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs` — dort werden Tools zentral per `McpServerTool.Create(...)` registriert, analog zu `get_violations`/`search_pattern`.
- **Diff-Scope-Logik zum Wiederverwenden:** Git-Diff-Scanning aus dem bestehenden `get_impact`-Tool (`src/AiNetLinter/Mcp/Tools/SymbolGraph/GetImpactTool.cs`) — als Vorlage für `changedOnly`.
- **Tests:** `src/AiNetLinter.Tests/Mcp/Tools/` — dort liegen die Tests der Nachbar-Tools (`PatternDetectToolTests.cs`, `SearchPatternToolTests.cs`, `GetViolationsToolTests.cs`), nicht in `FastTests`/`IntegrationTests` wie in einer früheren Fassung dieses Dokuments angenommen.
- **Docs:** `Docs/agent-api.md`, Abschnitt „Die N Tools" (aktuell 18, wird zu 19) — dort muss `find_magic_values` tabellarisch ergänzt und die abweichende Pro-Fundstelle-Suppression-Semantik explizit dokumentiert werden.
- **Referenz zu aktualisieren:** `src/AiNetLinter/Mcp/Tools/PatternDetect/PatternCatalog.cs` (Kommentar nennt „magic-numbers" aktuell als Pattern ohne existierende Erkennung — nach Fertigstellung von `find_magic_values` sollte der Kommentar diese Lücke nicht mehr als offen ausweisen).

## Entdeckte Mängel/Redundanzen

- Keine Redundanz zu bestehender Tool-/Regel-Infrastruktur: `PatternCatalog.cs` weist „magic-numbers" bereits selbst als Pattern ohne existierende Erkennung aus (bewusst zurückgestellt) — `find_magic_values` füllt diese dokumentierte Lücke, statt etwas Bestehendes zu duplizieren. Wiederverwendung der existierenden `McpTruncation`-Engine (Suppression-Mechanismus wird bewusst NICHT wiederverwendet, siehe „Verworfene Alternativen") verhindert weitere Code-Duplikation.
- **Duplizierte `WarnThreshold`-Konstante**
  - **Gefunden:** `private const double WarnThreshold = 0.80;` ist identisch in `HotspotMapBuilder.cs:23` und `GetHotspotsScanner.cs:27` definiert, inkl. identischem Interpolations-String `"...80% des Limits..."`.
  - **Bezug:** Klassisches DRY-Duplikat zweier `private const`-Felder über Klassengrenzen hinweg — genau das Muster, das `find_magic_values` als „Konstanten-Kandidat" später selbst erkennen und melden soll.
  - **Vorschlag:** Hochstufung in eine gemeinsame Konstante (z. B. `HotspotConstants.WarnThreshold`).
  - **Entscheidung:** bewusst verschoben (später) — wird als natürlicher erster Praxis-Fund von `find_magic_values` selbst aufgedeckt, sobald das Tool existiert; kein Vorziehen als isolierter Mini-Fix nötig.

## Wie (grober Ansatz)

1. `SyntaxWalker` durchläuft alle C#-Syntaxbäume der Solution für `LiteralExpressionSyntax` (inkl. Raw String Literals & static text in `InterpolatedStringExpressionSyntax`).
2. AST-Filterung schließt Attribute, eindeutige `const`/`readonly`-Initialisierer und Test-Dateien (sofern `includeTests: false`) aus.
3. Check auf Suppression: Über die `GetLeadingTrivia()` / `GetTrailingTrivia()` des aktuellen `SyntaxNode` wird geprüft, ob ein `// ainetlinter-disable MagicValues` Kommentar vorliegt. Zeilenbasierte File-IO-Scanner (`SuppressionScanner`) werden bewusst vermieden.
4. Kontextanalyse via `SemanticModel` bestimmt Methoden-Parameternamen, `nameof(...)`-Übereinstimmungen und Zuweisungsvariablen für Config-, Timeout- und Enum-Heuristiken.
5. Duplikate, Einzelwerte und doppelte `private const` Felder werden aggregiert (`minOccurrences = 1` als Standard).
6. Ergebnisse werden mit `McpTruncation` bei `maxResults` (Default: 50) gekappt und als kompaktes JSON-Resultat zurückgegeben.

## Definition of Done / Erfolgskriterien

- `find_magic_values` ist in `AnalysisToolRegistrations.cs` registriert und über `tools/list` im MCP-Server auffindbar.
- Target Framework .NET 10 wird unterstützt.
- Gezielte Filter-Parameter (`valueType`, `categoryFilter`, `scopeFilter`, `changedOnly`, `minOccurrences`, `maxResults`, `ignoreNumbers`) sind voll funktionsfähig.
- `maxResults`-Kappung nutzt die zentrale `McpTruncation`-Hilfsklasse.
- Suppress-Kommentare (`// ainetlinter-disable MagicValues`) schalten Fundstellen zuverlässig stumm.
- Scans mit `minOccurrences = 1` finden auch Einzelvorkommen treffsicher.
- `nameof(...)`-Kandidaten und duplizierte `private const`-Felder werden erkannt.
- `security_candidates` (Secrets-Heuristik) und `standard_candidates` (HTTP-Statuscodes) liefern korrekte, unterscheidbare Ergebnisse; Leere-String-Literale werden dabei nirgends gemeldet (Rausch-Filterung).
- `changedOnly` beschränkt den Scan nachweislich auf per Git-Diff geänderte Dateien.
- `ignoreNumbers` erweitert die Trivial-Liste nachweislich um projektspezifische Werte; `GetHashCode()`-Overrides werden automatisch ausgenommen.
- `StructuredContent` liefert ein Objekt (`{ MagicValues: [...] }`), kein nacktes Array.
- Unit- & Integrationstests in `src/AiNetLinter.Tests/Mcp/Tools/` bestätigen korrekte Klassifizierung und Rausch-Filterung.
- Tool-Dokumentation in `Docs/agent-api.md` (Tools-Tabelle + Suppression-Sonderfall-Hinweis) aktualisiert.
- Kommentar in `PatternCatalog.cs` zu „magic-numbers ohne Erkennung" aktualisiert.

## Offene Punkte

*Keine offenen Punkte.*
