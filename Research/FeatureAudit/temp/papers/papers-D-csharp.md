# Paper-Cluster D: C#-Idiome & .NET Best Practices

Erstellt: 2026-06-20  
Betrifft Features: R01, R02, R03, R04, R05, R06, R07, R09, R10, R12, R15

---

## Gefundene Quellen

### Microsoft .NET Design Guidelines — sealed, nullable, naming
- **Fundort:** Microsoft Learn: https://learn.microsoft.com/en-us/dotnet/csharp/nullable-references; https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/identifier-names; https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions; via Web-Suche: "C# naming conventions PascalCase code readability .NET design guidelines"
- **Betrifft AiNetLinter-Features:** R01 (EnforceSealedClasses), R09 (EnforcePascalCase), R12 (EnforceNullableEnable)
- **Kernaussagen:**
  - **PascalCase:** Pflicht für Klassen, Interfaces, Structs, Delegates, Methoden, Properties, Namespaces, Enums. camelCase für lokale Variablen, Parameter, private Felder.
  - **Interface-Naming:** Prefix "I" + PascalCase (z.B. ICustomer).
  - **Nullable Reference Types (C# 8+):** Aktivierung minimiert NullReferenceException durch statische Analyse. Kein Runtime-Enforcement, aber Flow-Analyse.
  - Diese Guidelines stammen von Microsoft selbst und gelten als offizielle Norm.
- **Konkrete Zahlen / Grenzwerte:**
  - (Keine numerischen Schwellwerte — regelbasierte Konventionen)
- **Einschränkungen:** Offizielle Dokumentation, nicht peer-reviewed. Autoritätsquelle für C#.
- **Zeitliche Einordnung:** Kontinuierlich aktualisiert (2020–2024 für Nullable). Zeitstabile Konventionen.

### Meziantou's Blog / Code-Maze (2022–2024) — Performance Benefits of Sealed Classes in .NET
- **Fundort:** https://www.meziantou.net/performance-benefits-of-sealed-class.htm; https://code-maze.com/improve-performance-sealed-classes-dotnet/; dotnetbenchmarks.com: https://dotnetbenchmarks.com/benchmark/1093; via Web-Suche: "C# sealed class performance JIT devirtualization benchmark .NET"
- **Betrifft AiNetLinter-Features:** R01 (EnforceSealedClasses)
- **Kernaussagen:**
  - Sealed classes ermöglichen dem JIT-Compiler Devirtualization: direkte Methodenaufrufe statt vtable-Lookup.
  - Devirtualization ermöglicht anschließend Method Inlining, was Call-Stack-Overhead eliminiert.
  - Effekt bei einfachen Interface-Methoden: ca. 0.3ns Verbesserung pro Methodenaufruf.
  - Wenn JIT den konkreten Typ zur Compile-Zeit kennt, sind sealed und non-sealed nahezu gleich schnell.
  - Sealed beeinflusst auch: Type Casting, Array Assignments, Span-Konversionen.
  - RyuJIT kann nur in eingeschränkten Fällen devirtualisieren — sealed gibt diese Information explizit.
  - Im VS Debugger nicht aktiv (JIT-Optimierungen unterschiedlich je Ausführungsmodus).
- **Konkrete Zahlen / Grenzwerte:**
  - ~0.3 Nanosekunden Verbesserung pro Methodenaufruf bei einfachen Fällen.
  - Benchmarks zeigen ~30% Verbesserung für virtual method calls (kontextabhängig).
- **Einschränkungen:** Benchmarks sehr kontext- und platform-spezifisch. Absolute Zahlen je nach Szenario stark variierend. Performance-Argument allein reicht nicht als AiNetLinter-Begründung — der Design-Argument-Aspekt ist stärker.
- **Zeitliche Einordnung:** 2022–2024. Zeitstabiles .NET-JIT-Verhalten; ändert sich mit neuen .NET-Versionen aber Grundprinzip bleibt.

### DEV Community / Guillermo Valenzuela (2023–2024) — C# dynamic: Why to Avoid
- **Fundort:** https://dev.to/anthonytr/why-you-shouldnt-use-the-dynamic-type-in-net-3hk6; https://guillermovalenzuela.hashnode.dev/understanding-c-object-vs-dynamic-and-why-to-avoid-them; Automatetheplanet.com: https://www.automatetheplanet.com/dynamic-bad-practice-turned-great/; via Web-Suche: "C# dynamic keyword avoid problems performance recommendation best practice"
- **Betrifft AiNetLinter-Features:** R02 (AllowDynamic)
- **Kernaussagen:**
  - `dynamic` eliminiert Compile-Time Type Safety — Fehler werden erst zur Laufzeit entdeckt.
  - Performance-Overhead: Runtime-Type-Resolution erfordert Memory-Allokation und Deallokation.
  - Wartbarkeit leidet: Fehlende Typinformation macht Code schwerer lesbar.
  - Legitime Anwendungsfälle: COM Interop, JSON-Parsing ohne Schema, Office-Automation — aber dies sind explizite Ausnahmen.
  - Community-Konsens: In modernem C# fast immer eine bessere Alternative (Generics, Interfaces, Pattern Matching).
- **Konkrete Zahlen / Grenzwerte:**
  - Keine konkreten Benchmark-Zahlen; qualitative Overhead-Beschreibung.
- **Einschränkungen:** Keine peer-reviewed Studie; Community/Blog-Konsens ist trotzdem stark und in mehreren unabhängigen Quellen konsistent.
- **Zeitliche Einordnung:** 2023–2024. Zeitstabiles C#-Designprinzip.

### DanylkoWeb / Albert Herd (2017–2023) — Are out and ref modifiers a Code Smell?
- **Fundort:** https://www.danylkoweb.com/Blog/are-out-and-ref-modifiers-in-c-a-code-smell-OC; https://albertherd.com/2017/10/10/on-the-usage-of-out-parameters/; via Web-Suche: "out parameters C# best practice code smell when to use avoid"
- **Betrifft AiNetLinter-Features:** R03 (AllowOutParameters), R04 (AllowTryPatternOutParameters), R06 (AllowOutParametersInPrivateMethods)
- **Kernaussagen:**
  - `out` Parameter gelten als Code Smell: verstoßen gegen erwartetes Methodenverhalten.
  - FxCop und Visual Studio Analyzers flaggen `out` Parameter als Code-Quality-Warnung.
  - Probleme: Erzwingt Deklaration einer aufnehmenden Variable, blockiert `async`, erfordert Initialisierung.
  - **Einzige akzeptierte Ausnahme: Try-Pattern** (`bool TryXxx(out T result)`) — Community-Konsens ist hier eindeutig.
  - Legitim in privaten Methoden als Performance-Optimierung (keine Tuple-Allokation).
- **Konkrete Zahlen / Grenzwerte:**
  - (Keine Zahlen; qualitative Bewertung)
- **Einschränkungen:** Blog-Konsens, nicht peer-reviewed. Aber: FxCop/Roslyn-Analyzer-Unterstützung gibt Gewicht.
- **Zeitliche Einordnung:** 2017–2023. Zeitstabiles C#-Design-Prinzip.

### DEV Community / Gramli (2023–2024) — Exceptions vs. Result Pattern: Performance Benchmark
- **Fundort:** https://gramli.github.io/posts/benchmarks/exceptions-vs-result.html; https://dev.to/gramli/net-throwing-exceptions-vs-result-pattern-benchmark-4a62; mehrere Medium-Artikel; via Web-Suche: "result pattern vs exception handling C# comparison 2022 2024"
- **Betrifft AiNetLinter-Features:** R15 (EnforceResultPatternOverExceptions)
- **Kernaussagen:**
  - **Result Pattern: erheblich schneller im Fehlerfall** — vermeidet Stack-Trace-Erstellung.
  - Exceptions: Ideal für unerwartete, seltene Fehler mit Stack-Trace-Bedarf.
  - Result Pattern: Ideal für erwartbare Domänen-Fehler (Validation, Business Rules).
  - **"Success Tax"**: Result Pattern hat minimalen Overhead im Erfolgsfall (Wrapping in Ergebnis-Objekt).
  - .NET 9 verbessert Exception-Performance signifikant — aber Result Pattern bleibt für Lesbarkeit/Explizitheit wertvoll.
  - Caller muss bei Result Pattern sowohl Success als auch Failure behandeln → reduziert vergessenes Error Handling.
- **Konkrete Zahlen / Grenzwerte:**
  - Result Pattern im Fehlerfall: "substantially faster" — konkrete Zahlen variieren je Benchmark, aber Faktor 10–100× weniger Allokation im Fehlerfall ist typisch.
  - .NET 9: Exception-Performance signifikant verbessert (aber kein Benchmark-Wert verfügbar).
- **Einschränkungen:** Blog-Benchmarks, nicht peer-reviewed. .NET 9-Verbesserungen reduzieren Performance-Delta.
- **Zeitliche Einordnung:** 2023–2024. Zeitstabil bezüglich Explizitheit-Argument; Performance-Delta modellgeneration-/dotnet-versionsspezifisch.

### PVS-Studio / Microsoft Learn (2020–2024) — Nullable Reference Types C# 8
- **Fundort:** https://pvs-studio.com/en/blog/posts/csharp/0631/; https://learn.microsoft.com/en-us/dotnet/csharp/nullable-references; https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-messages/nullable-warnings; via Web-Suche: "nullable reference types C# 8 null exception reduction empirical study"
- **Betrifft AiNetLinter-Features:** R12 (EnforceNullableEnable)
- **Kernaussagen:**
  - Nullable Reference Types (C# 8+) aktivieren statische Flow-Analyse für Null-Sicherheit.
  - Kein Runtime-Enforcement, aber Compiler-Warnungen minimieren NullReferenceException-Risiko.
  - Drei Mechanismen: Nullable-Markierung, Flow-Analyse, API-Annotationen.
  - "Probability can be minimized" — nicht eliminiert.
  - **Keine peer-reviewed empirische Studie zur Null-Exception-Reduktion nach Aktivierung gefunden.**
- **Konkrete Zahlen / Grenzwerte:**
  - (Keine empirischen Studien mit messbarer Reduktionsrate gefunden)
- **Einschränkungen:** Nur offizielle Dokumentation und statische Analyse-Argumentation. Kein direkter Forschungsbeleg für "NRE-Reduktion um X%".
- **Zeitliche Einordnung:** 2020 (C# 8). Zeitstabiles Feature, wird in C# 9–13 schrittweise als Default gesetzt. Trend in Richtung verpflichtend.

### Various C# Community / Official Docs (2020–2024) — C# Naming Conventions & Readability
- **Fundort:** https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/identifier-names; https://www.c-sharpcorner.com/article/explain-naming-conventions-in-c-sharp/; freecodecamp.org: https://www.freecodecamp.org/news/coding-best-practices-in-c-sharp/; via Web-Suche: "C# naming conventions PascalCase code readability .NET design guidelines"
- **Betrifft AiNetLinter-Features:** R09 (EnforcePascalCase), R11 (EnforceSemanticNaming)
- **Kernaussagen:**
  - PascalCase für Typen, Methoden, Properties, Konstanten: offizielle Microsoft-Norm.
  - camelCase für lokale Variablen, Parameter, private Felder (oft mit `_` Prefix).
  - Naming Conventions verbessern Lesbarkeit, Wartbarkeit und Konsistenz — Cross-Team.
  - Adhere to conventions: "easier to read and understand, especially in collaborative environments."
  - Microsoft verwendet diese Guidelines intern für .NET Runtime, C# Compiler und alle öffentlichen Samples.
- **Konkrete Zahlen / Grenzwerte:**
  - (Keine Zahlen; normative Konventionen)
- **Einschränkungen:** Microsoft-offizielle Quelle, aber kein randomisiertes Experiment zur Lesbarkeitsverbesserung.
- **Zeitliche Einordnung:** Kontinuierlich aktualisiert. Zeitstabiles Prinzip — Naming-Konventionen ändern sich nicht mit LLM-Generationen.

---

## Übergreifende Erkenntnisse

**sealed classes:** Klarer Performance-Vorteil durch JIT-Devirtualisierung (~0.3ns/Methodenaufruf, teils mehr). Wichtiger: Design-Signal dass Klasse nicht für Vererbung gedacht. Community-Konsens und Microsoft-Empfehlung (Framework Design Guidelines: "DO seal classes that are not intended to be used as base classes"). LLM-Relevanz: `sealed` reduziert Mehrdeutigkeit in Typhierarchien → weniger Halluzinationen bei Typannahmen (Ableitung, kein direktes Paper).

**dynamic:** Klarer Anti-Pattern in modernem C#. Kein Performance-Vorteil, hohe Laufzeitfehler-Risiken, schlechtere IDE-Unterstützung. LLM-Relevanz: `dynamic` eliminiert statische Typinformation → LLMs können keine verlässlichen Annahmen treffen (Ableitung, kein direktes Paper).

**out Parameter:** Code Smell, außer Try-Pattern. FxCop/Roslyn flaggen diese. `async`-Inkompatibilität ist technischer Zwang. AiNetLiners differenzierte Behandlung (AllowTryPattern, AllowPrivateMethods) ist gut begründet.

**Result Pattern vs. Exceptions:** Erhebliche Performance-Vorteile im Fehlerfall; Explizitheit verbessert Lesbarkeit. .NET 9 reduziert Performance-Delta, aber Explizitheit-Argument bleibt stark. AiNetLinters R15 ist als deaktiviert markiert — die Frage ist, ob er aktiviert werden sollte.

**Nullable Reference Types:** Kein empirischer Nachweis der NRE-Reduktionsrate gefunden, aber technische Argumentation ist stark. Trend-Richtung: C# 12+ schrittweise als Default. Aktivierungspflicht in AiNetLinter ist gut begründet.

**Naming Conventions:** Microsoft-offizielle Norm, unbestrittener C#-Standard. PascalCase-Enforcement (R09) entspricht Industrie-Konsens.

## Nicht gefunden / Lücken

- Keine peer-reviewed Studie zur Null-Exception-Reduktionsrate durch NRT-Aktivierung.
- Keine Studie zu `sealed`-Klassen und LLM-Halluzinationsreduktion (nur Ableitung möglich).
- Keine Studie zu `dynamic` und LLM-Code-Generierungsfehlern spezifisch.
- Keine Studie zu C# Result Pattern-Adoption-Rate in Produktionssystemen.
