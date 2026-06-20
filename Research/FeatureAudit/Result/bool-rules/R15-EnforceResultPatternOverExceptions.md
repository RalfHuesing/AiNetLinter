# EnforceResultPatternOverExceptions (R15)

**Kategorie:** Boolean-Regel  
**Aktueller Wert:** false (deaktiviert) | **Status:** Deaktiviert  
**Severity:** error (wenn aktiviert)  
**Paper-Cluster genutzt:** D, C

---

## Bewertung

🟡 **UNPRAKTIKABEL**

**Fazit:** Das Result-Pattern ist für vorhersehbare Domänenfehler empirisch besser (10–100× schneller auf Fehlerpfaden, klarere Signaturen), aber die flächendeckende Erzwingung mittels Linter ist zu invasiv und zu umstritten, um sinnvoll als `error`-Regel zu laufen; der deaktivierte Zustand spiegelt den realen C#-Community-Konsens korrekt wider.

---

## Empfehlung

**Aktion:** Deaktiviert lassen  
**Begründung:** Die C#-Community ist gespalten; das Erzwingen des Result-Patterns in öffentlichen APIs widerspricht etablierten .NET-Framework-Konventionen (die durchgängig auf Exceptions setzen) und würde in gemischten Codebases zu massiven Migrationskonflikten führen; der Mehrwert übersteigt den Reibungsaufwand nicht.

---

## Wissenschaftliche / Empirische Grundlage

**DEV Community & BenchmarkDotNet Reports (2023–2026)** zeigen klar: Das Werfen einer Exception kostet Stack-Trace-Erstellung und Stack-Unwinding. Das Result-Pattern nutzt normale Kontrollfluss-Pfade und ist auf Fehlerpfaden typischerweise **10- bis 100-mal schneller** bei deutlich geringerem GC-Druck.

Diese Performance-Aussage ist klar und empirisch belegbar. **Warum trotzdem Deaktivierung?** Weil Performance auf Fehlerpfaden selten der limitierende Faktor ist — Exceptions sollen für unerwartete Situationen reserviert bleiben, nicht als häufiger Hot-Path auftreten. Wenn Exceptions im Hot-Path liegen, ist das Design-Problem der Fehler, nicht die Exception selbst.

**C#-Community-Konsens:** Es gibt keine einheitliche Empfehlung. Microsoft nutzt in den eigenen .NET-Bibliotheken durchgängig Exceptions (z.B. `FileNotFoundException`, `ArgumentNullException`). Drittanbieter-Libraries wie FluentResults, OneOf oder LanguageExt popularierten das Result-Pattern in C#, haben aber keinen Mehrheitskonsens erreicht. Functional-Programming-Communities (F#, Scala-Beeinflusste) favorisieren es; .NET-traditionalisten lehnen es ab.

**Fazit:** Für neue Projekte mit domänengetriebenem Design (DDD) kann das Result-Pattern wertvoll sein; für Bibliotheken oder Mixed-Codebases ist es kontraproduktiv.

## KI-Agenten-Perspektive

Aus Cluster C (Liu et al. 2025): LLM-Agenten haben erhebliche Schwierigkeiten mit **inkonsistenten Fehlerbehandlungsmustern in einer Codebasis**. Wenn Teile der API Exceptions werfen und andere Result-Typen zurückgeben, erhöht sich die Fehlerrate beim Generieren von Call-Sites signifikant (Project Context Conflicts).

Das Result-Pattern bietet für LLMs einen Vorteil in der Signatur-Lesbarkeit: `Result<Customer, ValidationError> CreateCustomer(...)` ist selbst-dokumentierend bzgl. möglicher Fehler. Exceptions hingegen sind oft nur durch Dokumentation oder try/catch-Analyse erkennbar.

**Aber:** Wenn das Pattern nur partiell erzwungen wird (wie es ein Linter tut, der nicht alle Framework-Aufrufe kontrolliert), entsteht eine gemischte Codebasis, die den LLM-Agenten mehr verwirrt als ein konsistentes Exception-Pattern.

## Zeitliche Einordnung

**Grundlagenstabilität:** Offen

Das Performance-Argument ist zeitstabil (.NET optimiert Exceptions, aber die Diskrepanz bleibt). Das Design-Argument (Signaturen als Vertrag) ist ebenfalls zeitstabil. Was sich ändern könnte: C# könnte zukünftig ein sprachintegriertes Result-Pattern einführen (ähnlich Rusts `?`-Operator oder Kotlins `Result<T>`), was die Linter-Erzwingung überflüssig machen würde.

## Risiken / Gegenargumente

**Reibung mit .NET-Framework-Exceptions:** `ArgumentNullException`, `InvalidOperationException` etc. gehören zur .NET-API und können nicht wegabstrahiert werden. Ein Linter, der Result-Pattern erzwingt, muss massiv mit Ausnahmelisten arbeiten, was die Konfiguration komplex macht.

**Migrations-Aufwand in bestehenden Projekten:** In einer Codebasis die Exceptions als Primärmuster nutzt, ist der Wechsel zu Result-Pattern ein signifikantes Refactoring. AiNetLiners Baseline-Mechanismus kann bestehende Stellen einfrieren, aber neue Erweiterungen müssen dann im Result-Pattern bleiben — was zu inkonsistentem Code führt.

**Teamkompetenz:** Nicht alle Entwickler (und nicht alle LLM-Generierungsmuster) sind mit Result-Typen vertraut. Die Nutzung von FluentResults, OneOf oder ähnlichen Libraries setzt Kenntnis voraus.

Die `mdc`-Datei hat diese Regel bereits als "nicht erzwingen" klassifiziert — diese Einschätzung ist korrekt.

---

## Quellen

- DEV Community & BenchmarkDotNet, 2023–2026, "Exceptions vs. Result Pattern in .NET 9 & 10" — https://gramli.github.io/posts/benchmarks/exceptions-vs-result.html
- Liu et al., 2024/2025, "LLM Hallucinations in Practical Code Generation" — arXiv:2409.20550
- Microsoft .NET Documentation — Exception-Design-Guidelines: https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/exceptions
