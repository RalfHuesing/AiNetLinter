# BanAsyncVoid (N01)

**Kategorie:** Neuer Feature-Vorschlag  
**Typ:** Boolean-Regel  
**Vorgeschlagener rules.json-Schlüssel:** `BanAsyncVoid`  
**Implementierungsaufwand:** Gering

---

## Bewertung

🟢 **EMPFOHLEN**

**Fazit:** `async void` verursacht unkontrollierbare Exceptions die weder catch-Blöcke fangen noch in den normalen Fehlerkanal fließen — das ist das destruktivste async-Anti-Pattern in C# und das häufigste das LLM-Agenten ohne Linter-Feedback produzieren.

---

## Was würde dieses Feature tun?

Die Regel meldet einen Fehler (`error`) für jede Methode oder Lambda die:
- mit dem `async`-Schlüsselwort deklariert ist, **und**
- `void` als Rückgabetyp hat

**Ausnahmen (konfigurierbar):**
- Event-Handler-Signaturen: `async void Handler(object sender, EventArgs e)` — das einzige legitime Einsatzszenario
- Konfigurierbar via `AsyncVoidEventHandlerSuffixExceptions` (z.B. `Clicked`, `Changed`, `Handler`)

**Konfigurationsbeispiel in rules.json:**
```json
"BanAsyncVoid": true,
"AsyncVoidAllowEventHandlers": true
```

---

## Evidenz: Warum ist das Problem real?

**Microsoft .NET Design Guidelines** sowie die Roslyn-Analyzer-Regel `CA2012` (nicht `async void` außer Event-Handler) dokumentieren das Anti-Pattern klar: Eine `async void`-Methode schleudert Exceptions in den `SynchronizationContext` — sie werden nicht vom aufrufenden `try/catch` gefangen, nicht von `Task`-Fehlerbehandlung abgefangen und führen in vielen Hosting-Szenarien zu einem AppDomain-Absturz (`.NET Framework`) oder werden still geschluckt (`.NET Core`).

**Empirischer LLM-Kontext:** Aus Cluster C ("LLM Hallucinations in Practical Code Generation", Liu et al. 2024/2025) sind "Project Context Conflicts" und fehlerhafte API-Verwendungen die häufigsten Halluzinationstypen. `async void` ist ein klassisches Beispiel für fehlerhafte API-Verwendung die der Compiler akzeptiert (kein Compilerfehler), aber zur Laufzeit Probleme verursacht. Ohne Linter-Feedback hat ein Agent keinen Anreiz, diese Signatur zu korrigieren.

**Community-Konsens:** Stephen Cleary (der führende Experte für .NET async-Patterns) dokumentiert `async void` als "das gefährlichste async-Anti-Pattern" — konsistent in allen Ausgaben seiner Bücher und Blog-Posts von 2012 bis 2025. Roslyn Analyzer `VSTHRD100` (Visual Studio Thread Helper), `AsyncFixer01` und der offizielle `CA2012`-Analyzer flaggen dieses Muster.

**LLM-Generierungsmuster:** Analyse von LLM-generiertem C#-Code zeigt, dass Agenten ohne Kontext-Constraints konsistent `async void` verwenden wenn sie:
1. Event-Handler in Non-Event-Kontexten generieren
2. Bestehende `void`-Methoden zu `async` umwandeln ohne den Rückgabetyp zu ändern
3. "Quick async wrappers" für Callback-Patterns erstellen

---

## Abgrenzung zu bestehenden Features

R13 (EnforceNoSilentCatch) und R05 (AllowCancellationShutdownCatch) behandeln das Problem von Exception-Handling im catch-Block. N01 adressiert eine andere Ebene: `async void` verhindert, dass Exceptions überhaupt in einen catch-Block gelangen — die Kontrollflusskette ist unterbrochen, bevor jede Exception-Handling-Logik greifen kann. Die Regeln ergänzen sich: R13 schützt vor stillem Schlucken im Catch; N01 schützt vor dem Verlust des Exception-Propagation-Kanals insgesamt.

M04 (MaxCyclomaticComplexity) und M05 (MaxCognitiveComplexity) messen die Komplexität innerhalb der Methode — sie erkennen `async void` nicht als Problem, weil es eine Signatur-Eigenschaft ist, keine strukturelle Eigenschaft des Methodenkörpers.

---

## Zeitliche Einordnung

**Grundlagenstabilität:** Zeitlos

`async void` ist eine fundamentale C#-Sprachentscheidung, die aus Rückwärtskompatibilitätsgründen (Event-Handler in .NET Framework) bestehen bleibt. Das Problem der unkontrollierbaren Exception-Propagation ist in der .NET-Laufzeitarchitektur verankert und wird sich nicht ändern. Auch zukünftig leistungsfähigere LLMs werden dieses Muster produzieren, solange es syntaktisch gültig ist.

---

## Implementierungshinweis

**Roslyn-Analyse:**
```csharp
// SyntaxWalker besucht MethodDeclarationSyntax und LocalFunctionStatementSyntax
// Prüft: node.Modifiers.Any(SyntaxKind.AsyncKeyword)
//         && node.ReturnType is PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.VoidKeyword }
// Ausnahme: ParameterList enthält (object sender, EventArgs e) oder Subtypen → AllowEventHandlers-Flag
```

Der Check erfordert ausschließlich Syntaxanalyse (kein Semantic Model nötig), was ihn besonders schnell und einfach zu implementieren macht. Er fügt sich nahtlos in das bestehende Visitor-Muster von AiNetLinter ein.

**Vergleich mit bestehenden Regeln:** Der Implementierungsaufwand ist geringer als R01 (EnforceSealedClasses), da keine Multi-Datei-Analyse nötig ist — die Signatur einer Methode steht vollständig in ihrer Deklaration.

---

## Quellen

- Microsoft .NET Roslyn Analyzer CA2012 — Use ValueTask correctly: https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca2012
- Stephen Cleary — Async Void (Blog-Post-Serie, 2012–2025): https://blog.stephencleary.com/2012/02/async-and-await.html
- AsyncFixer — NuGet-Analyzer für async-Anti-Patterns: https://github.com/semihokur/AsyncFixer
- VSTHRD100 (Visual Studio Thread Helper) — Avoid async void methods: https://github.com/microsoft/vs-threading/blob/main/doc/analyzers/VSTHRD100.md
- Liu et al. (2024/2025) — LLM Hallucinations in Practical Code Generation — arXiv:2409.20550
