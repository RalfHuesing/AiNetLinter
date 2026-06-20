# AiNetLinter — Neue Feature-Vorschläge

Erstellt: 2026-06-20  
Basis: Synthese aus Paper-Clustern A–H + Lückenanalyse der bestehenden 46 Features

---

## Recherche-Ansatz

Die bestehenden Features (M01–M17, R01–R20, F01–F09) decken klassische Qualitätsmetriken (Komplexität, Größe, Kopplung) und C#-Idiome (sealed, nullable, naming) gut ab. Die Lückenanalyse konzentrierte sich auf:

1. **Async/Await-Anti-Patterns:** Der gesamte Bereich asynchroner Programmierung in C# fehlt als Regelkategorie vollständig, obwohl er empirisch zu den häufigsten LLM-Fehlerquellen gehört.
2. **LINQ-Komplexität:** LINQ-Chains erzeugen Cognitive Complexity die weder CC noch CogC zuverlässig messen.
3. **Magic Numbers / Strings:** Kontextfreie Literale die LLM-Agenten keine Semantik geben.
4. **Generics-Komplexität:** Verschachtelte Generics die Tokenizer und Reasoning erschweren.

Gemäß Phase-6-Auftrag wurden nur Muster berücksichtigt, die im AiNetLinter-Architekturstil (monolithisch, statisch kompiliert, kein DI, Roslyn-Syntaxanalyse) implementierbar sind.

---

## Vorschläge im Überblick

| ID | Name | Typ | Priorität | Implementierungsaufwand | Evidenz |
|----|------|-----|-----------|------------------------|---------|
| N01 | BanAsyncVoid | Boolean-Regel | 🟢 EMPFOHLEN | Gering | Stark: Microsoft, Roslyn-Analyzer, Community-Konsens |
| N02 | BanBlockingTaskAccess | Boolean-Regel | 🟢 EMPFOHLEN | Gering | Stark: Microsoft, .NET async-Docs, LLM-Halluzinations-Muster |
| N03 | MaxLinqChainLength | Numerische Metrik | 🟡 PRÜFEN | Mittel | Moderat: Readability-Studien, LM-CC-Analogie |

---

## Verworfene Kandidaten

### Magic Numbers / Magic Strings
**Warum nicht:** Das Problem ist real — kontextfreie Literale verhindern LLMs das Ableiten semantischer Bedeutung. Aber ein syntaktischer Check (Literal-Wert ≠ 0, 1, -1, "") ohne semantischen Kontext produziert extrem viele False Positives (HTTP-Statuscodes, Array-Indizes, Timeout-Werte als Literale in Tests). Andere Linter (SonarQube, StyleCop SA1400) haben dieses Problem durch Konfigurationslisten gelöst — der Aufwand für eine sinnvolle Implementierung übersteigt den Nutzen für AiNetLiners Scope.

### Generics-Komplexität (verschachtelte Generics)
**Warum nicht:** `Dictionary<string, List<Tuple<int, CustomType>>>` ist objektiv LLM-unfreundlich, aber die syntaktische Messung der Verschachtelungstiefe von Generics ist nicht trivial in Roslyn und die Grenzwert-Kalibrierung ist sehr schwer ohne empirische Baseline. Kein Paper aus dem Audit hat einen validen Threshold für Generic-Verschachtelungstiefe geliefert. Aufwand zu hoch, Evidenz zu schwach.

### catch (Exception) ohne Typ-Einschränkung
**Warum nicht:** R13 (EnforceNoSilentCatch) deckt den Schaden-Aspekt bereits ab. Das Problem des "Overly Broad Catch" (catch ohne Typ-Einschränkung) ist zwar verwandt, aber als separate Regel würde es mit R13 und R05 interagieren und die Konfigurations-Komplexität erhöhen. Als Extension nach dem Audit sinnvoller.

---

## Hinweis zur Implementierung

Alle drei empfohlenen Vorschläge (N01, N02, N03) sind als Roslyn-Syntaxanalyse implementierbar:

- **N01/N02:** Syntaktisches Matching auf `AsyncKeyword` + `VoidReturnType` (N01) bzw. `.Wait()`, `.Result`, `.GetAwaiter().GetResult()` als Member-Access-Expressions (N02). Beide sind triviale Syntax-Checks.
- **N03:** Zählen verketteter LINQ-Methoden via Invocation-Walk über `MemberAccessExpression`-Ketten. Mittel-komplex, aber in Roslyn gut machbar.

Alle drei folgen dem bestehenden AiNetLinter-Analysemuster (SyntaxWalker oder Visitor-Pattern auf SyntaxTree).
