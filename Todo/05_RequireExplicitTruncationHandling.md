# Epic: RequireExplicitTruncationHandling (Explizite Behandlungsprüfung für Daten-Abschneiden erzwingen)

## Big Picture & Intention
Ein Hauptgrund für katastrophale Fehlschläge bei komplexen Coding-Tasks sind **Spiraling Hallucination Loops (Kontext-Fehlanpassungen)**. Diese entstehen primär dann, wenn Agenten unvollständige Daten als vollständig ansehen – wie abgeschnittene Datei-Inhalte, verkürzte Terminal-Logs oder Paginierungs-Teilmengen – und daraus falsche Schlussfolgerungen ableiten. 

SurgeHQ dokumentierte ein markantes Beispiel: Ein Agent las eine Datei, die vom Terminal trunkiert ausgegeben wurde. Statt zu erkennen, dass die Datei unvollständig war, halluzinierte das Modell eine nicht-existente Basisklasse namens `BaseWriter` und fing an, Phantom-Methoden aufzurufen. Als dies abstürzte, begann der Agent, sogar die Terminal-Ausgaben selbst zu halluzinieren, um seine Hypothese zu rechtfertigen.

**Die Lösung:** 
Der Linter erzwingt bei allen Datei-Lese-, Stream-Lese- oder Netzwerk-Operationen die unmittelbare Präsenz von **Limit-Guards** oder **Längen-Prüfungen** im Code. Wenn ein Agent beispielsweise `Stream.Read` aufruft, muss er zwingend prüfen, ob das Ergebnis 0 ist (EOF) oder ob eine Puffer-Grenze erreicht wurde. Bei Web-APIs muss eine Paginierungs-Schleife (oder ein Limit/Take-Parameter) vorhanden sein.

Dies verankert einen strukturellen "algorithmischen Selbstzweifel" direkt im Code der KI. Sie wird gezwungen, das Szenario unvollständiger oder abgeschnittener Daten aktiv im Kontrollfluss zu berücksichtigen.

---

## Realistischer Impact
* **Stoppen von Halluzinations-Spiralen:** Hoch. Verhindert, dass Agenten unendliche Fehlerschleifen drehen, wenn Streams oder Puffergrenzen überschritten werden.
* **Resilienz gegen Datenkorruption:** Sehr hoch. Erhöht die Robustheit bei Netzwerk-Schwankungen oder großen Datei-Uploads.

---

## Konkrete Regeln & Heuristik
1. **Identifikation von I/O-Leseaufrufen:** Der Linter scannt nach Methodenaufrufen wie:
   - `File.ReadAllText`, `File.ReadLines`, `File.ReadAllBytes` (sowie asynchrone Varianten).
   - `Stream.Read`, `Stream.ReadAsync`, `StreamReader.ReadLine`, `StreamReader.ReadToEnd`.
   - HttpClient-Antwortauswertungen (z. B. `HttpContent.ReadAsStringAsync`).
2. **Erzwingung von Guards:** In derselben Methode, die den Leseaufruf tätigt, muss:
   - Entweder ein Längencheck auf das Ergebnis erfolgen (z. B. `string.Length`, `bytes.Length` oder Rückgabewert von `Read` > 0).
   - Oder der Aufruf muss sich innerhalb eines `try-catch`-Blocks befinden, der explizit I/O-Ausnahmen abfängt.
   - Oder das Ergebnis wird an eine Validierungs-Methode (Suffix `Guard` oder `Validate`) übergeben.

---

## Code-Vorschläge & Referenzen

### 1. Erweiterung der Konfiguration
In [LinterConfig.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Configuration/LinterConfig.cs#L34-L55) fügen wir die neue Regel hinzu:

```csharp
public record GlobalConfig
{
    // ... bestehende Regeln ...
    
    /// <summary>
    /// Erzwingt, dass I/O- und Stream-Leseoperationen unmittelbare Laengen- oder Puffer-Validierungen besitzen.
    /// </summary>
    public bool RequireExplicitTruncationHandling { get; init; } = true;
}
```

In [RuleMetadataRegistry.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Configuration/RuleMetadataRegistry.cs#L8-L28):
```csharp
private static readonly IReadOnlyDictionary<string, RuleMetadataEntry> Defaults =
    new Dictionary<string, RuleMetadataEntry>(StringComparer.Ordinal)
    {
        // ...
        ["RequireExplicitTruncationHandling"] = new() { Severity = "warning", Intent = "agent-resilience" },
    };
```

### 2. Implementierung des Analyzers
Wir implementieren die Prüfung in einer neuen Datei `LinterAnalyzer.Safety.cs` im Verzeichnis [Core](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Core):

```csharp
using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Models;

namespace AiNetLinter.Core;

public sealed partial class LinterAnalyzer : CSharpSyntaxWalker
{
    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        base.VisitInvocationExpression(node);

        CheckTruncationHandling(node);
    }

    private void CheckTruncationHandling(InvocationExpressionSyntax node)
    {
        if (!_config.Global.RequireExplicitTruncationHandling) return;
        if (_isTestFile) return;

        var symbol = _semanticModel.GetSymbolInfo(node).Symbol;
        if (symbol == null) return;

        var typeName = symbol.ContainingType?.Name ?? "";
        var methodName = symbol.Name;

        if (IsReadOperation(typeName, methodName))
        {
            if (!IsGuardOrCheckPresentForInvocation(node))
            {
                _violations.Add(new RuleViolation
                {
                    FilePath = _filePath,
                    LineNumber = GetLineNumber(node),
                    RuleName = "RequireExplicitTruncationHandling",
                    Details = $"Der I/O-Leseaufruf '{methodName}' von '{typeName}' besitzt keine unmittelbare Validierung der Laenge oder Vollstaendigkeit (Truncation-Schutz).",
                    Guidance = "Prüfe die Anzahl gelesener Bytes/Zeichen (z.B. '> 0' oder 'Length') unmittelbar nach dem Aufruf, um Halluzinationen bei Teil-Daten zu verhindern."
                });
            }
        }
    }

    private static bool IsReadOperation(string typeName, string methodName)
    {
        if (typeName == "File" && (methodName.StartsWith("Read") || methodName.StartsWith("Open")))
        {
            return true;
        }

        if ((typeName.Contains("Stream") || typeName == "HttpContent") && 
            (methodName.StartsWith("Read") || methodName == "CopyToAsync"))
        {
            return true;
        }

        return false;
    }

    private static bool IsGuardOrCheckPresentForInvocation(InvocationExpressionSyntax invocation)
    {
        // Einfache AST-Heuristik: Pruefe, ob das Ergebnis in einer lokalen Variable zugewiesen wird
        // und ob diese Variable in einem darauffolgenden IfStatement, BinaryExpression oder ReturnStatement verwendet wird.
        var parent = invocation.Parent;
        
        // Direkt in einem If oder Binary-Vergleich genutzt? (z.B. if (stream.Read(buf) > 0))
        if (IsInsideCondition(invocation)) return true;

        // Variable zugewiesen?
        if (parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator })
        {
            var varName = declarator.Identifier.Text;
            var enclosingBlock = declarator.Ancestors().OfType<BlockSyntax>().FirstOrDefault();
            if (enclosingBlock != null)
            {
                // Suche nach einer Verwendung der Variable in Bedingungen/Guards im selben Block
                var references = enclosingBlock.DescendantNodes()
                    .OfType<IdentifierNameSyntax>()
                    .Where(id => id.Identifier.Text == varName && id.SpanStart > declarator.Span.End);

                foreach (var r in references)
                {
                    if (IsInsideCondition(r))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool IsInsideCondition(SyntaxNode node)
    {
        var current = node.Parent;
        while (current != null && current is not BlockSyntax)
        {
            if (current is IfStatementSyntax || 
                current is BinaryExpressionSyntax || 
                current is ConditionalExpressionSyntax ||
                current is SwitchStatementSyntax)
            {
                return true;
            }
            current = current.Parent;
        }
        return false;
    }
}
```
