# Razor-Linting in AiNetLinter

Dieses Dokument spezifiziert die Implementierung von Razor/Blazor-Komponenten-Linting-Regeln mit Fokus auf Markup-Qualität und Strukturmetriken.

---

## 1. Intention & Scope

### Was wir linten

`.razor`-Dateien enthalten **ausschließlich Markup** — C# gehört per `BlazorRequireCodeBehind` (Epic 22) in die `.razor.cs`-Begleitdatei. Die `.razor.cs`-Dateien sind reguläre `.cs`-Dateien und werden vom bestehenden Roslyn-Pipeline bereits vollständig analysiert (alle C#-Regeln greifen dort automatisch).

**Kein Doppelaufwand:** C#-Regeln auf `.razor`-Dateien anzuwenden wäre redundant. Dieser Analyzer prüft ausschließlich die Qualität des Markups selbst.

### Typische KI-Fehlerquellen in Razor-Markup

* **Große Dateien:** Lange Markup-Strukturen → Lost-in-the-Middle-Effekt beim Generieren von Diffs.
* **Tiefe HTML-Verschachtelung:** KIs verlieren beim Erzeugen von Edits den Überblick über die Tag-Hierarchie → falsch geschlossene oder verschobene Tags.
* **Zu viele Control-Flow-Blöcke:** Viele `@if`/`@foreach`/`@switch` in einer Datei → KI kann nicht mehr vorhersagen, welche HTML-Elemente in welchem Branch gerendert werden.
* **Verschachtelte `@foreach`-Loops:** Jede Ebene multipliziert die Komplexität der Render-Vorhersage.
* **Inline-Event-Lambdas:** `@onclick="() => { ... }"` führt zu Syntaxfehlern wenn die KI C# innerhalb von HTML-Attributen generiert.
* **Zu viele Komponenten-Parameter:** `<MyComp A="..." B="..." C="..." D="..." E="..." />` — entspricht dem `MaxMethodParameterCount`-Problem: opake Aufrufstellen ohne semantischen Kontext.
* **Inline-Ternary in Attributen:** `class="base @(flag ? "active" : "")"` — Mixed-Context-Fehler bei KI-Generierung.

---

## 2. Parser: Microsoft.AspNetCore.Razor.Language

### Warum dieser Parser

`.razor`-Dateien sind **kein valides HTML**. Sie enthalten Razor-spezifische Konstrukte:
- Direktiven: `@page`, `@inject`, `@using`, `@typeparam`
- Control Flow: `@if`, `@else`, `@foreach`, `@for`, `@while`, `@switch`
- Komponenten-Attribute: `@bind-Value`, `@onclick`, `@ref`
- Razor-Ausdrücke: `@Model.Name`, `@(expression)`

Standard-HTML-Parser (AngleSharp MIT, HtmlAgilityPack MIT) würden an diesen Konstrukten scheitern oder fehlerhafte AST-Strukturen erzeugen. **`Microsoft.AspNetCore.Razor.Language`** (MIT-Lizenz, First-Party) ist der einzige Parser, der `.razor`-Dateien korrekt versteht.

| Parser | Lizenz | Razor-Support | Entscheidung |
| :--- | :--- | :--- | :--- |
| `Microsoft.AspNetCore.Razor.Language` | MIT | Vollständig | ✅ Gewählt |
| `AngleSharp` | MIT | Keiner — scheitert an `@if`, `@foreach` | ❌ |
| `HtmlAgilityPack` | MIT | Keiner — parst Razor-Syntax als Attribut-Garbage | ❌ |
| Text/Regex | — | Rudimentär, fehleranfällig | ❌ |

### Scope der Nutzung

Wir nutzen `Microsoft.AspNetCore.Razor.Language` **ausschließlich für den AST-Walk über das Markup**. C#-Code wird nicht generiert, `SourceMappings` werden nicht ausgewertet, `BuildRenderTree` wird nicht analysiert.

---

## 3. Architektur

```csharp
public sealed class RazorAnalyzer
{
    public IReadOnlyList<RuleViolation> Analyze(string razorContent, string filePath, WebConfig config)
    {
        var violations = new List<RuleViolation>();

        // Regel 1: Zeilenzählung direkt auf Rohtext (kein Parser nötig)
        CheckLineCount(razorContent, filePath, violations, config.Razor);

        // Razor-AST aufbauen (für alle strukturellen Checks)
        var projectEngine = RazorProjectEngine.Create(
            RazorConfiguration.Default,
            RazorProjectFileSystem.Create(Path.GetDirectoryName(filePath)!));

        var codeDocument = projectEngine.Process(
            RazorSourceDocument.Create(razorContent, filePath),
            importSyntaxTrees: null,
            tagHelpers: null);

        var root = codeDocument.GetSyntaxTree().Root;

        // Alle weiteren Checks auf dem AST
        CheckCodeBlock(root, razorContent, filePath, violations, config.Razor);
        CheckMarkupNestingDepth(root, razorContent, filePath, violations, config.Razor);
        CheckInlineEventLambdas(root, razorContent, filePath, violations, config.Razor);
        CheckControlFlowBlocks(root, razorContent, filePath, violations, config.Razor);
        CheckForeachNestingDepth(root, razorContent, filePath, violations, config.Razor);
        CheckComponentParameterCount(root, razorContent, filePath, violations, config.Razor);
        CheckInlineTernaryAttributes(root, razorContent, filePath, violations, config.Razor);

        return violations;
    }
}
```

---

## 4. Spezifikation der Regeln

### Regel 1: `RAZOR_MaxRazorLineCount`

* **Intention:** Große Razor-Dateien überlasten das Kontextfenster der KI (Lost-in-the-Middle).
* **Default-Limit:** `300` Zeilen.
* **Fehlermeldung:** *"Razor-Datei hat {actual} Zeilen (erlaubt: {limit}). Extrahiere eigenständige UI-Bereiche in separate Blazor-Komponenten."*
* **Implementierung:** `razorContent.Split('\n').Length` — kein Parser nötig.

---

### Regel 2: `RAZOR_MaxRazorCodeBlockLines`

* **Intention:** Guard-Regel für den Fall, dass jemand trotz `BlazorRequireCodeBehind` einen `@code`-Block anlegt (z.B. nach Suppression). Der Block darf dann zumindest nicht groß sein.
* **Default-Limit:** `20` Zeilen (deutlich restriktiver als in Projekten ohne Code-Behind-Pflicht).
* **Fehlermeldung:** *"@code-Block hat {actual} Zeilen. Verschiebe die Logik in die Code-Behind-Datei '{Name}.razor.cs' (partial class)."*
* **Zeilennummer:** Startzeile des @code-Blocks via `node.Span.AbsoluteIndex` → `GetLineNumber(content, offset)`.

---

### Regel 3: `RAZOR_MaxMarkupNestingDepth`

* **Intention:** Tiefe HTML-Hierarchien führen bei KIs zu Tag-Mismatch-Halluzinationen — falsch geschlossene oder verschobene Elemente.
* **Default-Limit:** `6` Ebenen (HTML-Elemente und Razor-Komponenten zählen; reine Razor-Direktiven wie `@if` zählen nicht als Ebene).
* **Fehlermeldung:** *"HTML-Verschachtelungstiefe beträgt {actual} Ebenen (erlaubt: {limit}). Extrahiere innere Bereiche in eigenständige Blazor-Komponenten."*
* **Hinweis:** Blazor-Komponenten (`<Counter />`, `<MudButton>`) zählen als eine Ebene, da sie aus KI-Sicht ein HTML-Element sind.

---

### Regel 4: `RAZOR_BanInlineEventLambdas`

* **Intention:** C#-Ausdrücke innerhalb von HTML-Attributen sind ein Mixed-Context, in dem KIs regelmäßig Syntaxfehler produzieren.
* **Erlaubt:** Methodenreferenz (`@onclick="HandleClick"`) oder triviales Einzeiler-Lambda ohne Semikolon (`@onclick="() => Count++"`).
* **Verboten:** Mehrzeilige Lambdas oder Ausdrücke mit Semikolon (`@onclick="() => { Count++; Save(); }"`).
* **Fehlermeldung:** *"Inline-Event-Lambda in '{attributeName}' ist zu komplex. Extrahiere die Logik in eine Methode in der Code-Behind-Datei."*

---

### Regel 5: `RAZOR_MaxControlFlowBlocks` *(neu)*

* **Intention:** Viele `@if`/`@foreach`/`@switch`/`@for`/`@while` in einer Datei signalisieren, dass die Komponente zu viel bedingte Render-Logik enthält. KIs können bei komplexem konditionalen Rendering nicht vorhersagen, welche HTML-Elemente tatsächlich ausgegeben werden.
* **Zählung:** Summe aller `@if`, `@else if`, `@foreach`, `@for`, `@while`, `@switch` auf oberster und verschachtelter Ebene in der gesamten Datei.
* **Default-Limit:** `8`.
* **Fehlermeldung:** *"Datei enthält {actual} Control-Flow-Blöcke (@if/@foreach/@switch, erlaubt: {limit}). Extrahiere Teilbereiche in eigenständige Komponenten mit klar definierten Eingabe-Parametern."*

---

### Regel 6: `RAZOR_MaxForeachNestingDepth` *(neu)*

* **Intention:** Verschachtelte `@foreach`-Schleifen im Markup multiplizieren die KI-Komplexität bei der Render-Vorhersage. Jede Ebene fügt eine Collection-Iteration hinzu, die die KI beim Erzeugen von Diffs konsistent durchdenken muss.
* **Default-Limit:** `2` Ebenen (äußere + eine innere Schleife).
* **Fehlermeldung:** *"@foreach-Verschachtelungstiefe beträgt {actual} Ebenen (erlaubt: {limit}). Extrahiere die innere Schleife in eine Kind-Komponente."*

---

### Regel 7: `RAZOR_MaxComponentParameterCount` *(neu)*

* **Intention:** Ein Komponenten-Aufruf mit vielen Parametern ist das Markup-Äquivalent von `MaxMethodParameterCount`. KIs verlieren die Zuordnung von Werten zu Parametern und generieren häufig falsch geordnete oder vergessene Bindings.
* **Zählung:** Anzahl der Attribute eines einzelnen Komponenten-Tags (`<MyComp Param1="..." Param2="..." />`). Native HTML-Attribute (z.B. `class`, `style`, `id`) werden nicht mitgezählt.
* **Default-Limit:** `5`.
* **Fehlermeldung:** *"Komponentenaufruf '<{name}>' hat {actual} Parameter (erlaubt: {limit}). Fasse verwandte Parameter in ein Parameter-Objekt zusammen oder reduziere die öffentliche API der Komponente."*

---

### Regel 8: `RAZOR_BanInlineTernaryInAttributes` *(neu)*

* **Intention:** Ternary-Ausdrücke innerhalb von HTML-Attributwerten (`class="base @(flag ? "active" : "")"`) erzeugen Mixed-Context — KIs müssen gleichzeitig HTML-String-Kontext und C#-Expressions-Kontext auflösen. Das führt zu typischen Fehlerklassen: fehlende Anführungszeichen, vertauschte Klammern, beschädigte Attributwerte.
* **Prüfung:** Attributwerte, die `@(` mit einem Ternary-Operator `?` enthalten.
* **Erlaubt:** Einfache Ausdrücke ohne Operator: `class="@CssClass"`, `value="@Count"`.
* **Verboten:** `class="base @(isActive ? "active" : "")"`.
* **Default:** `true` (aktiviert).
* **Empfohlene Behebung:** Berechne den Attributwert in einer Property oder Methode der Code-Behind-Datei: `private string CssClass => isActive ? "base active" : "base";`
* **Fehlermeldung:** *"Ternary-Ausdruck im Attributwert '{attributeName}'. Berechne den Wert in einer Property der Code-Behind-Datei."*

---

## 5. Suppression

Suppression in `.razor`-Dateien verwendet die Razor-Kommentar-Syntax. **Nicht** HTML-Kommentare (`<!-- -->`), da diese von Blazor als Markup gerendert werden.

```razor
@* ainetlinter-disable RAZOR_MaxMarkupNestingDepth *@
<div class="outer">
    <div class="inner-1">
        <div class="inner-2">
            @* Semantisch notwendige Verschachtelung für ARIA-Struktur *@
        </div>
    </div>
</div>
```

```razor
@* ainetlinter-disable RAZOR_MaxControlFlowBlocks *@
@* Legacy-Komponente, Refactoring in Sprint 8 geplant *@
@if (StateA) { ... }
@if (StateB) { ... }
```

---

## 6. `rules.json`-Konfiguration

```json
"Razor": {
  "MaxRazorLineCount": 300,
  "MaxRazorCodeBlockLines": 20,
  "MaxMarkupNestingDepth": 6,
  "BanInlineEventLambdas": true,
  "MaxControlFlowBlocks": 8,
  "MaxForeachNestingDepth": 2,
  "MaxComponentParameterCount": 5,
  "BanInlineTernaryInAttributes": true
}
```

---

## 7. Abgrenzung zu Epic 22

| Prüfebene | Zuständige Regel | Zuständiges Epic |
| :--- | :--- | :--- |
| Hat `.razor` eine `.razor.cs`? | `BlazorRequireCodeBehind` | Epic 22 (vorhanden) |
| Hat `.razor` eine `.razor.css`? | `BlazorRequireCssIsolation` | Epic 22 (vorhanden) |
| Wie groß ist das Markup? | `RAZOR_MaxRazorLineCount` | Phase 3 (neu) |
| Wie tief ist die Verschachtelung? | `RAZOR_MaxMarkupNestingDepth` | Phase 3 (neu) |
| Wie komplex ist der Control Flow? | `RAZOR_MaxControlFlowBlocks` | Phase 3 (neu) |
| C#-Qualität in `.razor.cs` | Alle bestehenden C#-Regeln | Roslyn-Pipeline (vorhanden) |

---

## 8. Unit-Tests & Edge-Cases

Testprojekt `AiNetLinter.Tests` wird um `Web/RazorAnalyzerTests.cs` erweitert.

| Szenario | Erwartetes Ergebnis |
| :--- | :--- |
| **A** — Saubere Komponente: <100 Zeilen, flaches Markup, Methoden-Referenzen bei Events | Keine Violations |
| **B** — Razor-Datei mit 350 Zeilen | `RAZOR_MaxRazorLineCount` |
| **C** — 7-fach verschachteltes HTML | `RAZOR_MaxMarkupNestingDepth` |
| **D** — `@onclick="() => { Count++; Save(); }"` | `RAZOR_BanInlineEventLambdas` |
| **E** — `@onclick="() => Count++"` (triviales Einzeiler-Lambda) | Keine Violation |
| **F** — 10 `@if`-Blöcke in einer Datei | `RAZOR_MaxControlFlowBlocks` |
| **G** — Dreifach-verschachtelte `@foreach`-Schleifen | `RAZOR_MaxForeachNestingDepth` |
| **H** — `<MyComp A="a" B="b" C="c" D="d" E="e" F="f" />` | `RAZOR_MaxComponentParameterCount` |
| **I** — `class="base @(flag ? "active" : "")"` | `RAZOR_BanInlineTernaryInAttributes` |
| **J** — `class="@CssClass"` (einfacher Ausdruck ohne Ternary) | Keine Violation |
| **K** — Direktiven am Dateianfang (`@page`, `@inject`, `@typeparam`) | Kein Crash, korrekt ignoriert |
| **L** — Razor ohne `@code`-Block | Kein Crash, korrekte Ergebnisse |
| **M** — `@* ainetlinter-disable RAZOR_MaxControlFlowBlocks *@` | Violation unterdrückt |
| **N** — `<button class="btn" type="submit" aria-label="Save" disabled="@IsLoading" />` | Kein `RAZOR_MaxComponentParameterCount` (HTML-Native-Attribute zählen nicht) |
