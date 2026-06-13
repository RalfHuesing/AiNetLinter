# Epic: RefineCounterproductiveRules (Kontraproduktive Regeln entfernen oder anpassen)

## Big Picture & Intention
Manche Linter-Regeln, die auf den ersten Blick die Code-Qualität heben sollen, wirken sich in der Praxis mit autonomen KI-Agenten kontraproduktiv aus. Zwei Regeln in `AiNetLinter` müssen dringend überarbeitet werden, da sie das Agenten-Resilience-Verhalten schwächen und das Kontextfenster mit redundantem Text verstopfen:

### 1. Die Exception-Phobie vs. Fail-Fast (`EnforceResultPatternOverExceptions` verfeinern)
Die aktuelle Regel `EnforceResultPatternOverExceptions` verbietet jegliches `throw` außerhalb von Konstruktoren und `Guard`/`Validate`-Methoden. 
* **Das Problem:** Das zwingt Entwickler und KI-Agenten dazu, selbst für unvorhersehbare, exogene oder fatale Systemzustände (z. B. Netzwerk-Timeouts, nicht unterstützte Operationen, Programmierfehler wie Null-Referenzen) das Result-Pattern zu verwenden. Das führt zu unlesbaren Signaturen und kognitivem Over-Engineering.
* **Die KI-Kognition:** Autonome Agenten sind durch SFT/RLVR darauf getrimmt, Abstürze um jeden Preis zu verhindern. Wenn sie gezwungen sind, alles in `Result<T>` zu verpacken, neigen sie dazu, Fehler stumm zu schlucken, was zu **Silent Failures (Kategorie 9)** führt. Der Prozess beendet sich mit Code 0, aber das Programm läuft im korrupten Zustand weiter. Bei echten Fehlern soll das Programm lautstark abstürzen (**Fail-Fast**), damit der Agent den Stacktrace im Terminal sieht und sich selbst korrigieren kann.

### 2. XML-Dokumentations-Overhead (`EnforceXmlDocumentation` verfeinern oder entfernen)
Die Regel `EnforceXmlDocumentation` verlangt an jeder öffentlichen Stelle XML-Doc-Kommentare (`/// <summary>`).
* **Das Problem:** Bei internen Applikationen führt dies zu tonnenweise redundantem Boilerplate-Code (z. B. `/// <summary> Holt oder setzt den Bezeichner. </summary> public int Id { get; set; }`).
* **Die KI-Kognition:** Jede Kommentarzeile bläht den Quelltext auf. Dadurch wird die Datei künstlich verlängert (und überschreitet schneller die `MaxLineCount`-Schranke von 500 Zeilen). Außerdem verbrauchen diese inhaltslosen Kommentare wertvolle Tokens im LLM-Kontextfenster und erhöhen die Generierungs-Latenz, da das LLM Zeit darauf verwenden muss, diese trivialen Kommentare zu erzeugen. Moderne LLMs verstehen exzellent benannte Klassen und Methoden auch ohne zusätzliche textuelle Erläuterung.

---

## Realistischer Impact
* **Stabilerer Self-Correction-Loop:** Extrem hoch. Durch das gezielte Zulassen von fatalen Exceptions stürzt das Test-Setup bei echten Fehlern ab, liefert der CLI einen Fehler-Code und zwingt den Agenten zum Debugging auf Basis des echten Stacktraces.
* **Kontext-Einsparung (Token-Kosten):** Hoch. Die Befreiung von trivialen XML-Dokumentationen spart ca. 10–15 % der Token-Menge pro Datei und verringert die Generierungszeit von Code-Änderungen.

---

## Konkrete Änderungen

### Änderung A: Ausnahme für fatale/technische Exceptions bei `EnforceResultPatternOverExceptions`
Exceptions für fachliche Kontrollflüsse (z. B. Validierungsfehler) bleiben über das Result-Pattern verboten. Technische Exceptions (wie `ArgumentNullException`, `InvalidOperationException`, `NotSupportedException`, `KeyNotFoundException` oder `HttpRequestException`) dürfen jedoch jederzeit geworfen werden.

#### Technische Umsetzung
Wir führen eine konfigurierbare Liste erlaubter Exceptions ein. Standardmäßig sind C#-Standard-Laufzeitausnahmen erlaubt.

In [LinterConfig.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Configuration/LinterConfig.cs#L34-L55):
```csharp
public sealed record GlobalConfig
{
    // ...
    public bool EnforceResultPatternOverExceptions { get; init; } = true;
    
    /// <summary>
    /// Liste von Exception-Klassennamen, die trotz ResultPattern-Regel geworfen werden duerfen (Fail-Fast).
    /// </summary>
    public IReadOnlyCollection<string> AllowedExceptions { get; init; } = new[]
    {
        "ArgumentException",
        "ArgumentNullException",
        "ArgumentOutOfRangeException",
        "InvalidOperationException",
        "NotSupportedException",
        "KeyNotFoundException",
        "IndexOutOfRangeException",
        "TimeoutException",
        "ObjectDisposedException",
        "NotImplementedException"
    };
}
```

In [LinterAnalyzer.ControlFlow.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Core/LinterAnalyzer.ControlFlow.cs#L80-L104):
Wir passen die Validierung an, um den Exception-Typ im Throw-Statement zu analysieren.

```csharp
private void CheckResultPatternViolation(SyntaxNode node)
{
    if (!_config.Global.EnforceResultPatternOverExceptions) return;

    if (IsAllowedFatalExceptionThrow(node)) return;

    if (!IsThrowAllowed(node))
    {
        _violations.Add(new RuleViolation
        {
            FilePath = _filePath,
            LineNumber = GetLineNumber(node),
            RuleName = "EnforceResultPatternOverExceptions",
            Details = "Unzulaessige Verwendung von 'throw' fuer fachlichen Kontrollfluss.",
            Guidance = "Verwende fuer fachliche Fehlerzustaende das Result-Pattern (Result<T>). Technische Laufzeitausnahmen (z.B. ArgumentNullException) sind erlaubt. 'throw' von fachlichen Exceptions ist nur in Konstruktoren oder Validierungs-Guards (Suffix 'Guard' oder 'Validate') zulaessig."
        });
    }
}

private bool IsAllowedFatalExceptionThrow(SyntaxNode node)
{
    ExpressionSyntax? exceptionExpression = null;

    if (node is ThrowStatementSyntax throwStatement)
    {
        exceptionExpression = throwStatement.Expression;
    }
    else if (node is ThrowExpressionSyntax throwExpression)
    {
        exceptionExpression = throwExpression.Expression;
    }

    if (exceptionExpression is ObjectCreationExpressionSyntax creation)
    {
        var typeSymbol = _semanticModel.GetTypeInfo(creation).Type;
        if (typeSymbol != null)
        {
            var typeName = typeSymbol.Name;
            return _config.Global.AllowedExceptions.Contains(typeName);
        }
    }

    return false;
}
```

---

### Änderung B: Verfeinerung von `EnforceXmlDocumentation`
Die Regel wird so angepasst, dass sie:
1. Standardmäßig für alle Projekte **deaktiviert** ist und nur bei echten Library-Projekten (wo NuGet-Pakete exportiert werden) opt-in aktiviert werden kann.
2. Falls aktiviert, auf **Typ- und Schnittstellen-Deklarationen** beschränkt wird und einfache Properties (get/set) sowie Methoden überspringt, deren Name bereits selbsterklärend ist.

#### Technische Umsetzung
In [LinterAnalyzer.Naming.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Core/LinterAnalyzer.Naming.cs#L112-L121) passen wir `ShouldSkipXmlDoc` an:

```csharp
private bool ShouldSkipXmlDoc(SyntaxNode node)
{
    if (!_config.Global.EnforceXmlDocumentation) return true;
    if (_isTestFile) return true;

    // Nur bei Klassen, Interfaces, Structs und Records erzwingen.
    // Properties und Methoden ueberspringen (spart drastisch Tokens im LLM-Kontext)
    if (node is not BaseTypeDeclarationSyntax)
    {
        return true; 
    }

    return !IsInPublicContext(node);
}
```
In [LinterConfig.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Configuration/LinterConfig.cs#L34-L55) setzen wir die Voreinstellung von `EnforceXmlDocumentation` standardmäßig auf `false`.
