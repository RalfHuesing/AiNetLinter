# 04 — LLM/Agent-Optimierung

> Spezifische Befunde und Vorschläge zur Optimierung der **Agent-Integration** und **LLM-Consumption**.
> Aufbauend auf [`01-Architektur-Befunde.md`](01-Architektur-Befunde.md), [`02-Code-Qualitaet.md`](02-Code-Qualitaet.md) und [`03-Architektur-Refactoring-Vorschlaege.md`](03-Architektur-Refactoring-Vorschlaege.md).

---

## 🎯 Zielsetzung

AiNetLinter richtet sich explizit an **AI-Agent-Workflows** (Cursor, Claude Code, GitHub Copilot, MCP-Server). Die Frage des Audits: **Wie gut ist der Code strukturiert für LLM-Consumption?**

### Antwort in einem Satz

Der Linter **produziert** gute LLM-Ausgabe, aber seine **Struktur** ist nicht LLM-/Agent-freundlich: Regel-Lookup erfordert 3 Datei-Suchen, neue Regeln erfordern Modifikation an 5+ Stellen, und es gibt keine programmatische Discovery-Schnittstelle für Agenten.

### 🎯 **Use-Case-Klärung (Stand 19.06.2026)**

Nach Diskussion mit dem Projekt-Owner wurde folgender **primärer Use-Case** identifiziert:

- AiNetLinter wird als **UnitTest im Projekt** integriert
- Output wird in eine **Datei** umgeleitet (`> output.foo`)
- **Agent liest die Datei** und behebt die Violations direkt
- **Keine maschinelle Weiterverarbeitung** des Outputs in der Tooling-Kette

**Konsequenz für die Output-Strategie:**

→ **Markdown ist das einzige Output-Format.** JSON, NDJSON, Text und SARIF wurden gestrichen.

---

## L3 — `IRuleRegistry` als Agent-Discovery-API

### Befund

Agenten haben aktuell **keine** programmatische Möglichkeit, alle verfügbaren Regeln aufzulisten. Sie müssten 3 Generator-Dateien lesen + parsen.

### Empfehlung

**Mit R1 (`IRuleRegistry`)** automatisch verfügbar:

```bash
ainetlinter --list-rules
ainetlinter --list-rules --intent agent-context
ainetlinter --describe-rule EnforceSealedClasses
````

**`--list-rules` Output (text):**

```
Available Rules (30 total)

By Intent: agent-context
  EnforceSealedClasses          [error]  Konkrete Klassen muessen 'sealed' sein
  BanPublicNestedTypes          [error]  Verbot oeffentlicher nested Typen
  MaxLineCount                  [error]  Maximale Dateilaenge
  ...

By Intent: agent-resilience
  EnforceNoSilentCatch          [error]  Keine stummen catch-Bloecke
  ...

By Intent: control-flow
  EnforceResultPatternOverExceptions [warning]  Result-Pattern statt throw
  ...
```

**`--describe-rule EnforceSealedClasses` Output (text):**

```
Rule: EnforceSealedClasses
Severity: error
Intent: agent-context
HasAutoFix: true
ExemptSuffixes: ["Base", "Foundation", "Host"]

Description:
  Konkrete Klasse ist nicht 'sealed'.

Guidance:
  Fuege den 'sealed' Modifikator zur Klassendeklaration hinzu, um
  unkontrollierte Vererbung zu verhindern.

Default-Value:  true (aus GlobalConfig.cs)
Override-Path:  Global.EnforceSealedClasses
```

**JSON-Output:**

```json
{
  "ruleId": "EnforceSealedClasses",
  "severity": "error",
  "intent": "agent-context",
  "hasAutoFix": true,
  "exemptSuffixes": ["Base", "Foundation", "Host"],
  "description": "Konkrete Klasse ist nicht 'sealed'.",
  "guidance": "Fuege den 'sealed' Modifikator zur Klassendeklaration hinzu ...",
  "defaultValue": true,
  "overridePath": "Global.EnforceSealedClasses"
}
```

**Nutzen für Agenten:**

- Agent kann **vor** Edit-Calls prüfen, welche Regeln aktiv sind
- Agent kann gezielt nach Regel-Details fragen
- Agent kann auto-fixes gezielt einsetzen
- Konsumiert von MCP-Server (siehe L5)

**Aufwand:** S (1 Tag, basierend auf R1)
**Nutzen:** ★★★★★

---

## L4 — Verbesserte LLM-Output-Qualität

### Befund

Aktueller Text-Output (`ViolationTextFormatter.cs`) hat:

- 26+ Regeln in `RuleInstructions` (Z. 105–134) — manuell gepflegt, nicht aus `LinterConfig` abgeleitet
- Generischer Fallback `"-> {ruleName}: Bitte behebe diesen Verstoss gemaess den Richtlinien."` (Z. 142) für unbekannte Regeln
- LLM erhält keine **strukturierten Anweisungen** in welcher Reihenfolge zu beheben ist

### Empfehlung

Mit R1 + R3 wird die Instruction-Generierung **konfigurierbar** und **vollständig**.

**Neue Felder in `RuleMetadata`:**

```csharp
public sealed record RuleMetadata(
    string RuleId,
    string DisplayName,
    string ShortDescription,
    string DetailedGuidance,
    string Intent,
    string Severity,
    string CursorHint,
    bool HasAutoFix,
    IReadOnlyList<string> ExemptSuffixes,
    string? ExampleFix = null,        // NEU
    string? ExampleViolation = null,  // NEU
    int SortPriority = 100            // NEU: Reihenfolge fuer LLM
);
```

**Beispiel für `EnforceSealedClasses`:**

```csharp
new(
    RuleId: "EnforceSealedClasses",
    ...
    ExampleViolation: "public class UserService { }",
    ExampleFix: "public sealed class UserService { }",
    SortPriority: 10  // hoechste Prio: sealed ist einfachster Fix
)
```

**Output-Generierung:**

````csharp
private static string FormatGuidance(RuleMetadata rule, RuleViolation violation)
{
    var sb = new StringBuilder();
    sb.AppendLine($"-> {rule.RuleId}: {rule.ShortDescription}");
    sb.AppendLine($"   Severity: {rule.Severity} | Intent: {rule.Intent}");
    sb.AppendLine();
    sb.AppendLine($"   Guidance: {rule.DetailedGuidance}");

    if (rule.ExampleViolation != null)
    {
        sb.AppendLine();
        sb.AppendLine("   Violation:");
        sb.AppendLine($"   ```csharp");
        sb.AppendLine($"   {rule.ExampleViolation}");
        sb.AppendLine($"   ```");
    }

    if (rule.ExampleFix != null)
    {
        sb.AppendLine();
        sb.AppendLine("   Fix:");
        sb.AppendLine($"   ```csharp");
        sb.AppendLine($"   {rule.ExampleFix}");
        sb.AppendLine($"   ```");
    }

    if (rule.HasAutoFix)
    {
        sb.AppendLine();
        sb.AppendLine("   Auto-Fix: `ainetlinter --fix` (siehe Violation-Kontext)");
    }

    return sb.ToString();
}
````

**Nutzen für Agenten:**

- Inline-Code-Beispiele direkt im Output
- Klar markierte Auto-Fix-Verfügbarkeit
- Sortierbare Prio für gestaffelte Bearbeitung

**Aufwand:** S (mit R1)
**Nutzen:** ★★★★★

---

## L5 — MCP-Server-Modus für Native Agent-Integration

### Befund

Aktuell ist AiNetLinter ein **CLI-Tool**. Agenten wie Cursor/Claude Code können es über Shell-Befehle aufrufen, aber es gibt keine **standardisierte Agent-Schnittstelle**.

### Empfehlung

**Neuer Modus `--mcp-server`:** startet einen MCP-konformen JSON-RPC-Server über stdio.

```bash
ainetlinter --mcp-server
```

**Unterstützte MCP-Methoden:**

```typescript
// 1. Liste alle Regeln
{ "method": "tools/call", "params": { "name": "list_rules", "arguments": { "intent": "agent-context" }}}
  -> { "rules": [ { "ruleId": "EnforceSealedClasses", "severity": "error", ... }, ... ] }

// 2. Lint eine Datei
{ "method": "tools/call", "params": { "name": "lint_file", "arguments": { "path": "src/MyApp/Services/UserService.cs" }}}
  -> { "violations": [ ... ] }

// 3. Auto-Fix anwenden
{ "method": "tools/call", "params": { "name": "apply_fix", "arguments": { "path": "...", "ruleId": "EnforceSealedClasses" }}}
  -> { "fixed": 3, "errors": [] }

// 4. Regel-Beschreibung
{ "method": "tools/call", "params": { "name": "describe_rule", "arguments": { "ruleId": "EnforceSealedClasses" }}}
  -> { "ruleId": "...", "guidance": "...", "exampleFix": "..." }
```

**Skelett:**

```csharp
public static class McpServer
{
    public static async Task RunAsync(CancellationToken ct)
    {
        var rules = BuiltInRules.All;
        var ruleRegistry = new RuleRegistry(rules);

        // JSON-RPC Server auf stdio
        while (!ct.IsCancellationRequested)
        {
            var request = await JsonRpcReader.ReadAsync(Console.In, ct);
            var response = request.Method switch
            {
                "tools/call" => HandleToolCall(request, ruleRegistry),
                "tools/list" => ListTools(),
                _ => JsonRpcResponse.Error(request.Id, "Unknown method")
            };
            await JsonRpcWriter.WriteAsync(Console.Out, response, ct);
        }
    }
}
```

**Nutzen:**

- Native Integration in Claude Code, Cursor, GitHub Copilot via MCP-Protokoll
- Agenten können Lint-Operationen ohne Shell-Aufrufe durchführen
- Streaming für große Lint-Runs
- Standardisierte Schema-Validierung

**Aufwand:** L (3–5 Tage, inkl. MCP-Protokoll-Implementation)
**Nutzen:** ★★★★★

---

## L6 — Cache-aware "Pre-Lint-Check" für Agent-Loops

### Befund

Agent-Loops editieren oft **dieselbe Datei mehrfach**. Aktuell wird der Linter pro Aufruf komplett gestartet (MSBuildWorkspace.OpenSolutionAsync ist teuer). Der `AnalysisCacheManager` ist da, aber nicht optimal.

### Empfehlung

**Neuer Modus `--watch`** für inkrementelle Lint-Loops:

```bash
ainetlinter --watch --path ./MyApp.slnx
# Output:
# Watching src/ for changes...
# [13:45:01] src/Foo.cs: 2 violations (EnforceSealedClasses, MaxCognitiveComplexity)
# [13:45:15] src/Bar.cs: clean
# [13:47:32] src/Baz.cs: 1 violation
```

**ODER:** `--lint-file <path>` für Single-File-Operationen (kein ganzer Solution-Walk nötig).

```bash
ainetlinter --lint-file src/MyApp/Services/UserService.cs --config rules.json
```

**Nutzen für Agent-Loops:**

- Schnelle Feedback-Schleife (1 Datei = 100ms)
- Kein MSBuild-Workspace-Reload
- Agent kann nach jedem Edit sofort re-lint-en

**Aufwand:** M (2 Tage; benötigt L1 für JSON-Output)
**Nutzen:** ★★★★

---

## L7 — Per-Datei-Token-Budget-Hinweise

### Befund

AiNetLinter gibt **keine Token-Budget-Schätzungen** aus. Für LLMs ist es schwer einzuschätzen, wie viel Kontext eine Korrektur kosten wird.

### Empfehlung

**Im Violation-Output** (JSON und text) ein Feld `estimatedTokenCost`:

```json
{
  "rule": "EnforceSealedClasses",
  "file": "src/MyApp/Services/UserService.cs",
  "line": 12,
  "details": "...",
  "estimatedTokenCost": 25, // ~5 Zeilen Code, ~25 Tokens
  "autoFixAvailable": true
}
```

**Berechnung:**

```csharp
private static int EstimateTokenCost(SyntaxNode node)
{
    // 1 Zeile ≈ 5 Tokens (grobe Schätzung)
    var lineCount = node.GetLocation().GetLineSpan().EndLinePosition.Line -
                    node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
    return lineCount * 5;
}
```

**Nutzen für Agenten:**

- Agent kann Low-Cost-Fixes bevorzugen
- Bei hohem `estimatedTokenCost` ist vielleicht ein Refactor sinnvoller
- Bessere Triage bei vielen Violations

**Aufwand:** S
**Nutzen:** ★★★

---

## L8 — Semantische Suche über Rule-Metadaten

### Befund

Aktuell muss ein Agent wissen, dass `EnforceSealedClasses` die richtige Regel ist. Es gibt keine **Suche** über Beschreibung/Intent.

### Empfehlung

**Neuer Modus `--search-rules "<query>"`:**

```bash
ainetlinter --search-rules "vererbung"
# Output:
# EnforceSealedClasses [agent-context, error]
#   Verhindert unkontrollierte Vererbung
# MaxInheritanceDepth [agent-context, error]
#   Verbot tiefer Vererbungshierarchien

ainetlinter --search-rules "komplexität"
# Output:
# MaxCyclomaticComplexity [agent-context, error]
# MaxCognitiveComplexity [agent-context, error]
# MaxMethodOverloads [agent-context, error]
```

**Skelett:**

```csharp
public static IReadOnlyList<RuleMetadata> SearchRules(string query, RuleRegistry registry)
{
    var q = query.ToLowerInvariant();
    return registry.All
        .Where(r =>
            r.RuleId.ToLowerInvariant().Contains(q) ||
            r.DisplayName.ToLowerInvariant().Contains(q) ||
            r.ShortDescription.ToLowerInvariant().Contains(q) ||
            r.Intent.ToLowerInvariant().Contains(q))
        .ToList();
}
```

**Nutzen für Agenten:**

- Agent kann **konzeptionell** suchen: "Was prüft das Tool zu Vererbung?"
- Natürlichsprachige Suche statt nur `RuleId`
- Bessere Discovery-Erfahrung

**Aufwand:** S
**Nutzen:** ★★★

---

## L9 — Strukturiertes Error-Reporting für Failures

### Befund

Wenn der Linter scheitert (MSBuild-Fehler, Config-Fehler), wird einfach `Console.Error.WriteLine(...)` aufgerufen. Für Agenten schwer zu parsen.

### Empfehlung

**Strikt definiertes Error-Schema** (auch im Text-Output):

```
[ERROR]: <error_code>: <short_message>
  context: <file or step>
  hint: <actionable_suggestion>
  docs: <url_or_doc_path>
```

**Beispiel:**

```
[ERROR]: CONFIG_INVALID: rules.json enthaelt unbekannte Option "MaxFoo"
  context: rules.json (line 23)
  hint: Entferne die Option oder aktualisiere auf AiNetLinter 1.0.50
  docs: ainetlinter --readme
```

**ODER im JSON-Modus:**

```json
{
  "error": {
    "code": "CONFIG_INVALID",
    "message": "rules.json enthaelt unbekannte Option 'MaxFoo'",
    "context": "rules.json:23",
    "hint": "Entferne die Option oder aktualisiere auf AiNetLinter 1.0.50",
    "docs": "ainetlinter --readme"
  }
}
```

**Nutzen für Agenten:**

- Agent kann Fehler programmatisch klassifizieren
- `hint` ist direkt actionable für den Agent
- `error_code` ermöglicht Decision-Trees

**Aufwand:** M (1–2 Tage)
**Nutzen:** ★★★

---

## L10 — Inline-Auto-Fix in Violation-Output

### Befund

Aktuell: `--fix` ist ein separater Modus. Agent muss zweimal aufrufen: erst lint, dann fix.

### Empfehlung

**Im LLM-Output (text/markdown/json) pro Violation:**

````
### EnforceSealedClasses × 1 — src/UserService.cs:12

**Violation:**
```csharp
public class UserService
{
    // ...
}
````

**Suggested Fix:**

```csharp
public sealed class UserService  // <- sealed Modifikator
{
    // ...
}
```

**Auto-fixable:** yes (run `ainetlinter --fix`)

````

**ODER als strukturierte JSON-Property:**

```json
{
  "rule": "EnforceSealedClasses",
  "line": 12,
  "beforeSnippet": "public class UserService",
  "afterSnippet": "public sealed class UserService",
  "autoFixable": true
}
````

**Nutzen für Agenten:**

- Agent kann Auto-Fix direkt anwenden ohne LLM-Code-Edit
- Inline-Snippets geben dem LLM klaren Kontext
- `autoFixable`-Flag ermöglicht Bulk-Operationen

**Aufwand:** L (mit R2/R4 Refactoring)
**Nutzen:** ★★★★★

---

## L11 — Bessere Test-Coverage für LLM-Use-Cases

### Befund

Tests prüfen aktuell **funktionale Korrektheit** (Regel feuert bei X, Regel feuert nicht bei Y). Sie prüfen nicht **LLM-Output-Qualität**.

### Empfehlung

**Snapshot-Tests für LLM-Output:**

```csharp
[Fact]
public void Lint_ProducesLLMReadableOutput()
{
    var violations = /* ... */;
    var output = ViolationTextFormatter.Format(violations, outputRoot, config);
    Approvals.Verify(output);  // ApprovalTests / Verify
}

[Theory]
[InlineData("EnforceSealedClasses")]
[InlineData("MaxMethodParameterCount")]
public void Rule_ProducesCompleteGuidance(string ruleId)
{
    var registry = new RuleRegistry(BuiltInRules.All);
    var rule = registry.Resolve(ruleId);
    Assert.NotNull(rule.DetailedGuidance);
    Assert.NotEmpty(rule.ExampleFix);
    Assert.NotEmpty(rule.ExampleViolation);
}
```

**LLM-Integration-Tests** (mit echtem LLM oder Mock):

```csharp
[Fact]
public async Task Violation_HasAllFieldsForLLM()
{
    var violations = await LintAsync(fixture);
    foreach (var v in violations)
    {
        Assert.NotEmpty(v.RuleName);
        Assert.NotEmpty(v.Details);
        Assert.NotEmpty(v.Guidance);
        Assert.True(v.LineNumber > 0);
    }
}
```

**Aufwand:** S
**Nutzen:** ★★★

---

## L12 — Doc-Updates für Agent-Workflows

### Befund

Aktuelle `README.md` und `Docs/configuration.md` sind **menschen-orientiert**. Agenten brauchen:

- Strukturiertes `--help`-Output
- API-Referenz für alle Optionen
- Beispiele für häufige Workflows

### Empfehlung

**1. Strukturiertes `--help`:**

```bash
ainetlinter --help
# Output: Markdown-formatted, mit Beispielen
```

**2. Neue Datei `Docs/agent-api.md`:**

- Liste aller `--format`-Optionen mit Schema
- Liste aller `--list-rules` / `--describe-rule` / `--search-rules` Optionen
- JSON-Beispiele für alle Modi
- Auto-Fix-Workflow
- MCP-Server-Setup

**3. Cursor-/Claude-Snippets** in `.cursor/rules/`:

- Workflow: "Code schreiben → lint → fix"
- Workflow: "Tiefenanalyse einzelner Regel"
- Workflow: "Baseline-Pflege"

**Aufwand:** S
**Nutzen:** ★★★

---

## 📊 Zusammenfassung LLM-Optimierung

| Vorschlag                      | Aufwand | Nutzen | Abhängig von |
| ------------------------------ | ------- | ------ | ------------ |
| L3 — `--list-rules` API        | S       | ★★★★★  | R1           |
| L4 — Bessere LLM-Output        | S       | ★★★★★  | R1           |
| L5 — MCP-Server                | L       | ★★★★★  | R1, R3       |
| L6 — Watch-Modus               | M       | ★★★★   | —            |
| L7 — Token-Budget              | S       | ★★★    | —            |
| L8 — Semantische Suche         | S       | ★★★    | R1           |
| L9 —- Strukturiertes Error     | M       | ★★★    | —            |
| L10 — Inline-Auto-Fix-Snippets | L       | ★★★★★  | R2, R4       |
| L11 — Snapshot-Tests           | S       | ★★★    | —            |
| L12 — Doc-Updates              | S       | ★★★    | —            |

### Empfohlene Reihenfolge

1. **R1 + L3 + L4** (RuleRegistry + Discovery) — Fundament
2. **L8** (Suche) — auf R1 aufbauend
3. **L5** (MCP-Server) — langfristige Strategie
4. **L6, L7, L9, L10, L11, L12** — Polish

---

## 🎯 Strategische Empfehlung

**Die Zukunft von AiNetLinter ist agent-zentriert**, nicht CLI-zentriert. Aktuell ist es ein gutes CLI-Tool mit LLM-Output. Die zukunftssichere Variante ist ein **MCP-Server mit CLI-Front-End**.

Zwei kritische Investitionen, um dies zu erreichen:

1. **R1 — `IRuleRegistry`** als Fundament (3 Tage)
2. **L5 — MCP-Server** für native Integration (4–5 Tage)

→ **Total: 1,5 Wochen für eine vollständige Agent-Integration**.

→ Damit wird AiNetLinter nicht nur ein **Linter** für AI-generierten Code, sondern ein **erstklassiger Agent-Service** in der AI-Tool-Landschaft.
`````
