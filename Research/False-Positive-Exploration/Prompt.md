---
Mein Prompt:

Schau dir mal dieses Projekt an.

Ziel: Ich hätte gerne Explorationstests die typischen Programmier Szenarien umsetzen die von dem AiNetLinter als Fehler gemeldet werden würden - die aber grundsätzlich eigentlich Okay sind.
Quasi das der AiNetLinter einen False-Positiv meldet.
Anhand dieser Tests würden dann erkennen was in unserem AiNetLinter noch "falsch" läuft und was wir ändern müssen.

Ein Beispiel aktuell ist `Research\Verbesserungen\cursor-ideen-umsetzungsplan.md`  - hier wurden Probleme aufgedeckt.

Ich würde das nur gerne pro-aktiv angehen.

Weißt du was ich meine?

Ich stelle mir vor es wird verschiedener code "erdacht" der "okay" wäre (für ein llm gut lesbar usw.) und den machen wir als unit test und jagen den gegen den AiNetLinter .. natürlich halbwegs ziel gerichtet.

wie wollen wir das angehen?
ich hätte dich (nur web llm) oder claude code.
wenn du schon arbeit abnehmen könntest wäre das für mich token effizient.

---

Gemini Flash 3.5 High Antwort:

Ja, ich verstehe ganz genau, was du vorhast. Du willst eine **"False-Positive-Matrix"** aufbauen – eine Suite von Explorationstests mit absolut legitimem, hochgradig lesbarem C#-Code, der aber aufgrund der aktuellen, rigiden Linter-Regeln fälschlicherweise anschlägt.

Das ist ein extrem smarter und token-effizienter Ansatz! Wenn wir diese Test-Snippets jetzt vorab definieren, kannst du sie gesammelt in deine Test-Suite werfen. Claude Code (oder dein lokaler Runner) muss dann nicht mühsam nach Fehlern _suchen_, sondern hat sofort eine glasklare "Fehler-Abschussliste", die er im Roslyn-Analyzer abarbeiten kann.

Hier ist der strukturierte Angriffsplan und die konkreten Code-Szenarien, die wir als Explorationstests gegen den `AiNetLinter` jagen sollten.

---

## Der Angriffsplan: Strategisches Vorgehen

Wir legen im Testprojekt einen dedizierten Ordner an: `src/AiNetLinter.Tests/Exploration/FalsePositives/`.
Jeder Test folgt dem Muster:

1. Validen, KI-freundlichen C#-Code als String einlesen.
2. Den `LinterAnalyzer` darauf ansetzen.
3. Asserten, dass **keine** Rules violated werden (`Diagnostics.Length == 0`).

---

## 5 konkrete Szenarien für die Test-Suite (Code-Vorschläge)

Hier sind fünf typische Programmierszenarien, bei denen der AiNetLinter aufgrund seiner aktuellen Dogmen (`Result<T>`-Zwang, Immutability, kognitive Komplexität < 5) sehr wahrscheinlich fälschlicherweise "Fehler" meldet.

### Szenario 1: Deklaratives Pattern Matching vs. Kognitive Komplexität

- **Das Problem:** Der Linter fordert eine kognitive Komplexität von maximal 5. Ein flaches, wunderschön lesbares `switch`-Expression-Konstrukt mit komplexeren relationalen Patterns ist für LLMs extrem einfach zu verstehen. Klassische McCabe-Metriken oder naive AST-Zähler neigen aber dazu, bei jedem Pattern-Zweig den Zähler hochzujagen.

```csharp
// TEST: Complexity_ValidPatternMatching_ShouldNotViolate
public record Order(decimal TotalAmount, int ItemCount, bool IsPremiumCustomer);

public static class DiscountEngine
{
    // Für LLMs perfekt linear lesbar, jagt aber oft die Komplexität hoch
    public static decimal CalculateDiscount(Order order) => order switch
    {
        { IsPremiumCustomer: true, TotalAmount: > 500 } => 0.20m,
        { IsPremiumCustomer: true }                     => 0.15m,
        { TotalAmount: > 200, ItemCount: > 5 }          => 0.10m,
        { TotalAmount: > 100 }                          => 0.05m,
        _                                               => 0.00m
    };
}

```

### Szenario 2: Fluent APIs / LINQ / Builder-Chaining vs. LineCount

- **Das Problem:** Eine einzige Anweisung (Statement) wird über viele Zeilen gestreckt (z.B. Entity Framework Configurations, FluentValidation oder komplexe LINQ-Queries). Für die KI ist das hochgradig lesbar, weil es deklarativ ist. Wenn der Linter aber stumpf auf Zeilenanzahl pro Methode (`MaxMethodLineCount`) prüft, schlägt er fälschlicherweise an.

```csharp
// TEST: LineCount_FluentApiChaining_ShouldNotViolate
public class CustomerValidator : AbstractValidator<Customer>
{
    public CustomerValidator()
    {
        // Ein einziges Statement, aber über 10 Zeilen lang.
        // Absolut KI-lesbar, darf keinen LineCount-Fehler werfen!
        RuleFor(customer => customer.Email)
            .NotEmpty()
            .WithMessage("Email ist Pflicht.")
            .EmailAddress()
            .WithMessage("Ungültiges Format.")
            .Must(email => email.EndsWith(".com"))
            .WithMessage("Nur .com Domains erlaubt.")
            .MaximumLength(100);
    }
}

```

### Szenario 3: Guard Clauses (Fail-Fast) vs. Result-Pattern-Zwang

- **Das Problem:** Der Linter verbietet "Exceptions für Control Flow" und fordert das `Result<T>`-Pattern. Aber: Das Absichern von System-Invarianten via `ArgumentNullException.ThrowIfNull` am Methoden-Anfang ist _kein_ Kontrollfluss, sondern technischer Selbstschutz (Fail-Fast). Wenn der Linter hier ein `Result<T>` erzwingt, führt das zu unlesbarem Code.

```csharp
// TEST: ControlFlow_TechnicalGuardClauses_ShouldNotViolate
public class UserService
{
    public Result<User> CreateUser(string username, string email)
    {
        // Technische Guards (Fail-Fast). LLMs verstehen das sofort.
        // Das darf NICHT als "Exception für Kontrollfluss" gewertet werden.
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentNullException.ThrowIfNull(email);

        if (username.Length < 3)
            return Result<User>.Failure("Username zu kurz."); // Fachlicher Fehler -> Result

        return Result<User>.Success(new User(username, email));
    }
}

```

### Szenario 4: C# Records mit Collections (`with`-Expression-Mutation)

- **Das Problem:** Der Linter erzwingt strikte Immutability. Wenn wir nun ein nicht-mutierbares `record` haben, das eine `IReadOnlyCollection` besitzt, und wir erzeugen via `with`-Expression eine Kopie mit veränderten Daten, arbeiten wir rein funktional. Erkennt das der Linter, oder schlägt er fälschlicherweise bei `.Append()` oder Listen-Initialisierungen an?

```csharp
// TEST: Immutability_RecordWithExpression_ShouldNotViolate
public record GroupState(string Name, IReadOnlyCollection<string> Members);

public static class GroupEvaluator
{
    public static GroupState AddUser(GroupState currentState, string newUser)
    {
        // Rein funktionale Zustandstransition via 'with'.
        // Manche Linter triggern hier fälschlicherweise auf "State Mutation".
        return currentState with
        {
            Members = currentState.Members.Append(newUser).ToList().AsReadOnly()
        };
    }
}

```

### Szenario 5: Lokale Funktionen & Closures vs. Parameter-Reassignment

- **Das Problem:** Der Fehler mit `ref`/`in` wurde im Umsetzungsplan ja schon entlarvt. Ein verwandtes Problem sind _Lokale Funktionen_, die Variablen aus dem äußeren Scope einfangen (Closures) und manipulieren. Für LLMs ist das oft ein sauberes, lokal gekapseltes Pattern. Erkennt der Linter den Unterschied zwischen dem Überschreiben eines Parameters und der erlaubten Modifikation einer lokalen Variable durch eine Lambda-Closure?

```csharp
// TEST: State_LocalFunctionClosure_ShouldNotViolate
public class InventoryProcessor
{
    public int ProcessStock(IEnumerable<Item> items)
    {
        int totalProcessed = 0; // Lokale Variable, kein Parameter!

        // Lokale Hilfsfunktion (Closure)
        void Aggregate(Item item)
        {
            if (item.IsValid)
                totalProcessed++; // Verändert äußeren Zustand, das ist OK!
        }

        foreach (var item in items)
            Aggregate(item);

        return totalProcessed;
    }
}

```

---

## Wie machen wir jetzt weiter?

1. **Test-Skelette anlegen:** Kopiere diese 5 Code-Snippets als Inline-Strings in deine Test-Suite (als erwartete "Null-Fehler"-Tests).
2. **Gegen die Wand fahren lassen:** Jage die Tests lokal hoch. Diejenigen, die fehlschlagen, sind deine echten, bewiesenen False-Positives.
3. **Claude Code füttern:** Wenn du Claude Code startest, gibst du ihm einfach den Prompt: _"Fixe die fehlschlagenden Explorationstests im Ordner XY, indem du den Roslyn-Analyzer im Projekt anpasst. Achte darauf, dass bestehende Tests nicht brechen."_

Was hältst du von der Auswahl? Sollen wir für einen bestimmten Bereich (z.B. Blazor oder asynchronen Code) noch ein spezifischeres Szenario dazupacken?
