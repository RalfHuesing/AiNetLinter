# BanBlockingTaskAccess (N02)

**Kategorie:** Neuer Feature-Vorschlag  
**Typ:** Boolean-Regel  
**Vorgeschlagener rules.json-Schlüssel:** `BanBlockingTaskAccess`  
**Implementierungsaufwand:** Gering

---

## Bewertung

🟢 **EMPFOHLEN**

**Fazit:** `.Wait()`, `.Result` und `.GetAwaiter().GetResult()` auf `Task`/`ValueTask` sind in async-Kontexten dead-lock-anfällig, in sync-Kontexten un-idiomatisch — und LLM-Agenten erzeugen diese Muster systematisch wenn sie synchrone Methoden mit async-APIs verbinden müssen.

---

## Was würde dieses Feature tun?

Die Regel meldet einen Fehler (`error`) für jeden Aufruf von:
- `.Wait()` auf einer `Task`- oder `ValueTask`-Instanz
- `.Result`-Property-Zugriff auf einer `Task<T>`- oder `ValueTask<T>`-Instanz
- `.GetAwaiter().GetResult()`-Kette auf einer `Task`- oder `ValueTask`-Instanz

**Ausnahmen (konfigurierbar):**
- `static void Main(string[] args)` — Programm-Einstiegspunkte die nicht async sein können
- Methoden mit explizitem Suppression-Kommentar (via CompoundSuppression-Mechanismus)
- Test-Setup-/Teardown-Methoden die kein `async`-Support haben (konfigurierbar via `AllowInTestSyncMethods: true`)

**Konfigurationsbeispiel in rules.json:**
```json
"BanBlockingTaskAccess": true,
"BanBlockingTaskAccessAllowInMain": true,
"BanBlockingTaskAccessAllowInTestSync": true
```

---

## Evidenz: Warum ist das Problem real?

**Deadlock-Mechanismus (Microsoft, Stephen Cleary, David Fowler):** Das Aufrufen von `.Wait()` oder `.Result` in einem Thread der einen `SynchronizationContext` besitzt (ASP.NET Classic, Windows-Forms, WPF) blockiert den Thread und wartet auf eine `Task`-Completion, die selbst auf demselben Thread fortsetzen muss. Das Ergebnis: Deadlock. Dieser Mechanismus ist in der .NET-Community als "classic ASP.NET async deadlock" bekannt und trat in tausenden Production-Incidents auf.

**In modernen Kontexten (.NET Core, ASP.NET Core):** Kein `SynchronizationContext` → kein klassischer Deadlock. Aber `.Wait()`/`.Result` unterbrechen den async-Kontrollfluss, blockieren einen ThreadPool-Thread (statt ihn freizugeben) und sind damit schlechter für Skalierbarkeit.

**LLM-Agenten-Muster:** Aus der Analyse von LLM-generiertem C#-Code (Paper-Cluster C, indirekt) ist das häufigste Szenario: Ein Agent soll eine async API aufrufen, aber die aufrufende Methode ist synchron. Ohne Linter-Feedback wählt der Agent fast immer `.Wait()` oder `.Result` statt die Aufrufkette korrekt zu async zu machen. Dies ist ein empirisch häufiges Muster in SWE-bench-ähnlichen Tasks.

**Roslyn-Analyzer-Evidenz:** Microsoft's eigener `VSTHRD002`-Analyzer (Visual Studio Thread Helper), `AsyncFixer02`, und die empfohlene Roslyn-Regel `CA2007` (ConfigureAwait) adressieren verwandte Probleme. Das Blocking-Pattern ist in allen führenden async-Linter-Suites als Error-Level-Regel gelistet.

**Xie et al. (2026) — branching-induced divergence:** `.Wait()` in async-Kontexten ist eine Control-Flow-Bifurkation: Der Thread-Scheduler muss entscheiden, ob der wartende Thread oder die fortsetzende Continuation als nächstes ausgeführt wird. Dieses nicht-deterministische Verhalten ist für LLM-Agenten, die einen vorhersagbaren Ausführungspfad erwarten, eine klassische Fehlerquelle.

---

## Abgrenzung zu bestehenden Features

R03 (AllowOutParameters/Verbot) und R13 (EnforceNoSilentCatch) adressieren andere Aspekte der Methoden-Kontrollfluss-Sauberkeit. N02 ergänzt diese durch den Fokus auf blockierende Task-Zugriffe — ein Muster das R03 und R13 nicht erfassen, weil es kein `out`-Parameter-Problem ist und kein `catch`-Block-Problem.

M04 (MaxCyclomaticComplexity) und M05 (MaxCognitiveComplexity) messen Verzweigungskomplexität, nicht Blocking-Patterns. Ein Code mit `task.Wait()` kann CC=1 und CogC=1 haben und trotzdem einen Deadlock verursachen.

N01 (BanAsyncVoid) und N02 sind komplementär: N01 schützt vor falschem `async`-Rückgabetyp; N02 schützt vor falschem Blocking in async-Kontexten. Zusammen schließen sie die häufigsten async-Anti-Patterns in AiNetLinter ab.

---

## Zeitliche Einordnung

**Grundlagenstabilität:** Zeitlos

Das `.Wait()`/`.Result`-Problem ist in der .NET-Laufzeitarchitektur (ThreadPool, SynchronizationContext, `await`-Continuation-Scheduling) verankert. Solange C# async/await auf diesem Mechanismus basiert, bleibt das Anti-Pattern real. Auch zukünftig werden LLM-Agenten dieses Muster als "naheliegende Lösung" produzieren, wenn sie synchron-zu-async-Brücken schreiben müssen.

---

## Implementierungshinweis

**Roslyn-Analyse:**
```csharp
// SyntaxWalker besucht InvocationExpressionSyntax und MemberAccessExpressionSyntax
//
// Muster 1: .Wait() 
//   node ist InvocationExpressionSyntax
//   node.Expression ist MemberAccessExpressionSyntax mit .Name.Identifier.Text == "Wait"
//   Optional: Semantic-Check ob Receiver Task/ValueTask ist (für höhere Präzision)
//
// Muster 2: .Result
//   node ist MemberAccessExpressionSyntax mit .Name.Identifier.Text == "Result"
//   Optional: Semantic-Check ob Receiver Task<T>/ValueTask<T> ist
//
// Muster 3: .GetAwaiter().GetResult()
//   Kette aus zwei InvocationExpressionSyntax: GetAwaiter() + GetResult()
```

Für maximale Einfachheit kann der Check zunächst rein syntaktisch sein (Methodenname-Matching ohne Semantic Model) und später durch Type-Check verfeinert werden. Das reduziert den Implementierungsaufwand auf ~50 Zeilen Roslyn-Code.

**Ausnahme-Implementierung:** Die `static Main`-Ausnahme erkennt man daran, dass der enthaltende Methoden-Knoten `static`, `Main`, und im Top-Level-Kontext ist — kein Semantic Model nötig.

---

## Quellen

- Stephen Cleary — Don't Block on Async Code (2012, aktuell 2024): https://blog.stephencleary.com/2012/07/dont-block-on-async-code.html
- Microsoft VSTHRD002 — Avoid problematic synchronous waits: https://github.com/microsoft/vs-threading/blob/main/doc/analyzers/VSTHRD002.md
- AsyncFixer02 — Long-running or blocking operations inside an async method: https://github.com/semihokur/AsyncFixer
- David Fowler (Microsoft) — Async Guidance for .NET: https://github.com/davidfowl/AspNetCoreDiagnosticScenarios/blob/master/AsyncGuidance.md
- Liu et al. (2024/2025) — LLM Hallucinations in Practical Code Generation — arXiv:2409.20550
- Xie et al. (2026) — Rethinking Code Complexity Through the Lens of Large Language Models — arXiv:2601.20404
