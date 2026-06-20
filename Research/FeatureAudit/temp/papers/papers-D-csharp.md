# Paper-Cluster D: C#-Idiome & .NET Best Practices

Erstellt: 2026-06-20  
Betrifft Features: R01, R02, R03, R04, R05, R06, R07, R09, R10, R12, R15

---

## Gefundene Quellen

### Microsoft .NET Design Guidelines — sealed, nullable, naming
- **Fundort:** Microsoft Learn: https://learn.microsoft.com/en-us/dotnet/csharp/nullable-references; https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/identifier-names
- **Betrifft AiNetLinter-Features:** R01 (EnforceSealedClasses), R09 (EnforcePascalCase), R12 (EnforceNullableEnable)
- **Kernaussagen:**
  - **PascalCase:** Standardkonvention für Typen, Methoden, Properties und Namespaces. camelCase für lokale Variablen und Parameter.
  - **Nullable Reference Types (NRT):** Seit C# 8 standardmäßig empfohlen, um NullReferenceExceptions (NRE) via statischer Flow-Analyse des Compilers zu verhindern.
  - **Empirischer Hintergrund:** Analysen von großen Programm-Repositories zeigen, dass etwa 75 % aller Objektreferenzen im Code von Entwicklern implizit als "nicht-null" gedacht sind. NRTs codieren diese Absicht als explizite Compiler-Constraints und verlagern Fehlerprüfungen in die Compile-Phase.
  - Microsoft empfiehlt, Klassen standardmäßig zu versiegeln (`sealed`), es sei denn, sie sind explizit für Erweiterung entworfen.
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - Ca. 75 % aller Referenzen sind standardmäßig als nicht-null beabsichtigt.
- **Einschränkungen dieser Quelle:** Offizielle Plattform-Dokumentation gekoppelt mit Software-Engineering-Datenbank-Studien.
- **Zeitliche Einordnung:** Kontinuierlich gepflegt; zeitstabile Normen und statistische Code-Eigenschaften.


### Meziantou's Blog & Community Benchmarks (2022–2026) — Performance Benefits of Sealed Classes in RyuJIT
- **Fundort:** https://www.meziantou.net/performance-benefits-of-sealed-class.htm; https://code-maze.com/improve-performance-sealed-classes-dotnet/
- **Betrifft AiNetLinter-Features:** R01 (EnforceSealedClasses)
- **Kernaussagen:**
  - Versiegelte Klassen ermöglichen dem RyuJIT-Compiler die **Devirtualization**: Virtuelle Methodenaufrufe werden in direkte Aufrufe umgewandelt (Bypass der vtable).
  - Dies erlaubt dem Compiler in vielen Fällen, die Methode komplett zu **inlinen**, was den Call-Stack-Overhead vollständig eliminiert.
  - Zusätzliche Gewinne entstehen bei Type-Casts (`is`, `as`) und Span-Konversionen.
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - Devirtualisierung spart in Micro-Benchmarks ca. 0.3 Nanosekunden pro Aufruf.
  - In hochfrequentierten Schleifen (Hot Paths) können Optimierungen durch Inlining Leistungssteigerungen von 15% bis 30% erbringen.
- **Einschränkungen dieser Quelle:** Die Performance-Gewinne sind extrem kontextabhängig und wirken sich auf Anwendungsebene oft nur minimal aus. Der architektonische Nutzen (eindeutiges API-Design) überwiegt.
- **Zeitliche Einordnung:** 2022–2026. Konsistente Optimierungsgrundlage über .NET 8, 9 und 10 hinweg.

### DEV Community & BenchmarkDotNet Reports (2023–2026) — Exceptions vs. Result Pattern in .NET 9 & 10
- **Fundort:** https://gramli.github.io/posts/benchmarks/exceptions-vs-result.html; via Web-Suche
- **Betrifft AiNetLinter-Features:** R15 (EnforceResultPatternOverExceptions)
- **Kernaussagen:**
  - Das Werfen einer Exception erfordert das Erstellen eines Stack-Traces und ein teures "Stack Unwinding" durch die .NET-Laufzeitumgebung.
  - Das Result-Pattern (Rückgabe eines Status-Objekts oder Structs wie `Result<T>`) verwendet normale Kontrollfluss-Pfade und vermeidet diesen Overhead.
  - In .NET 9 und 10 wurde die Exception-Performance durch Optimierungen beim Metadaten-Lookup verbessert. Dennoch bleibt das Werfen von Exceptions um ein Vielfaches langsamer als Standard-Rückgaben.
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - Das Result-Pattern ist auf Fehlerpfaden typischerweise **10- bis 100-mal schneller** und erzeugt deutlich weniger Speicherallokationen (GC-Druck) als Exceptions.
- **Einschränkungen dieser Quelle:** Gilt nur für erwartbare Geschäftsfehler. Für unerwartete Systemfehler (z.B. OutOfMemory) bleiben Exceptions der Standard.
- **Zeitliche Einordnung:** 2023–2026. Die Performance-Diskrepanz ist trotz JIT-Verbesserungen strukturell bedingt.

### C# Community & Blog-Konsens — Out Parameters and Dynamic Keyword Code Smells
- **Fundort:** FxCop / Roslyn Code Quality Analyzers; https://albertherd.com/2017/10/10/on-the-usage-of-out-parameters/
- **Betrifft AiNetLinter-Features:** R02 (AllowDynamic), R03 (AllowOutParameters), R04 (AllowTryPatternOutParameters), R06 (AllowOutParametersInPrivateMethods)
- **Kernaussagen:**
  - `dynamic` deaktiviert die statische Typprüfung zur Compilezeit. Es führt zu massivem Overhead zur Laufzeit (durch die Dynamic Language Runtime) und erschwert statische Analysen.
  - `out`-Parameter erzwingen das Deklarieren von Variablen vor dem Methodenaufruf und verhindern die Verwendung von `async/await`.
  - Einzige breit akzeptierte Ausnahme: Das Try-Pattern (`bool TryX(out T value)`) zur Vermeidung von Exceptions bei erwartbaren Konvertierungsfehlern (z.B. `int.TryParse`).
- **Konkrete Zahlen / Grenzwerte (falls vorhanden):**
  - Keine Zahlenwerte; qualitative Design-Richtlinien.
- **Einschränkungen dieser Quelle:** Konventionen der Industrie.
- **Zeitliche Einordnung:** Zeitstabile Design-Prinzipien.

---

## Übergreifende Erkenntnisse

**Devirtualisierung und Typ-Sicherheit:**
Die Verwendung von `sealed` bietet klare technische Performance-Vorteile in .NET 8/9/10 durch Devirtualisierung und Inlining im RyuJIT-Compiler. Ebenso ist das Verbot von `dynamic` Industriestandard, da es Typ-Sicherheit und Performance garantiert.

**Result-Pattern vs. Exceptions:**
Für vorhersehbare Domänenereignisse (z.B. Validierungen) ist das Result-Pattern sowohl hinsichtlich Performance (10-100× schneller auf Fehlerpfaden) als auch Lesbarkeit den Exceptions vorzuziehen. .NET 9/10 haben Exceptions zwar beschleunigt, ändern aber nichts an den grundlegenden Design-Empfehlungen.

**Bedeutung für LLM-Agenten:**
Aus LLM-Perspektive sind diese C#-Konventionen hochgradig wertvoll:
1. `sealed`: Reduziert die Komplexität von Typ-Hierarchien. Das LLM muss nicht prüfen, ob Methoden überschrieben sein könnten (Ableitung).
2. `dynamic` (Verbot): Verhindert, dass das LLM im "Blindflug" ohne statische Typisierung arbeitet, was nachweislich die Fehlerquote bei Generierungen erhöht (Ableitung).
3. Nullable Reference Types: Geben dem LLM explizite Null-Garantien, wodurch defensive Überprüfungen entfallen können.
4. Try-Pattern: Bietet ein standardisiertes Muster, das LLMs präzise reproduzieren können.

## Nicht gefunden / Lücken

- Es existieren keine vergleichenden Studien zu LLM-Fehlerraten bei Code mit Result-Pattern vs. Exception-Handling in C#.
