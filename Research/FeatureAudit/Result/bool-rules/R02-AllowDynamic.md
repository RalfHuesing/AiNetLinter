# AllowDynamic / Verbot von `dynamic` (R02)

**Kategorie:** Boolean-Regel  
**Aktueller Wert:** false (= `dynamic` ist verboten) | **Status:** Aktiv, kein Opt-out  
**Severity:** error  
**Paper-Cluster genutzt:** C, D

---

## Bewertung

🟢 **WERTVOLL**

**Fazit:** Das vollständige Verbot von `dynamic` ist klar begründet — es eliminiert einen Laufzeit-Typ-Overhead, verhindert statische Analyse-Blindstellen und reduziert Agenten-Halluzinationen in typbehafteten Kontexten; Behalten und kein Opt-out sinnvoll.

---

## Empfehlung

**Aktion:** Aktiviert lassen  
**Begründung:** `dynamic` deaktiviert die statische Typprüfung zur Compilezeit, erzeugt Laufzeit-Overhead durch die Dynamic Language Runtime (DLR) und macht Code für statische Analyse-Tools — inklusive LLM-Agenten — erheblich schwerer verständlich. Es gibt keine C#-Szenarien im Produktionscode, die `dynamic` erfordern und nicht durch Generics, Interfaces oder explizite Typen besser lösbar wären.

---

## Wissenschaftliche / Empirische Grundlage

Das `dynamic`-Schlüsselwort in C# ist eine Brücke zur Common Language Runtime's Dynamic Language Runtime (DLR). Jeder Aufruf auf einem `dynamic`-Objekt wird zur Laufzeit durch Reflection aufgelöst — die Compilezeit-Typprüfung entfällt vollständig. Die direkte Konsequenz ist:

1. **Kein Compiler-Schutz:** Fehler die bei statischen Typen als Compilefehler erscheinen (falscher Methodenname, falsche Signatur) werden zu Laufzeit-Exceptions.
2. **Laufzeit-Overhead:** Der DLR-Dispatch ist erheblich langsamer als direkte Methodenaufrufe. Die Kosten sind kontextabhängig, aber nicht vernachlässigbar.
3. **Statische Analyse-Blindstellen:** Roslyn-Analyzer, FxCop und alle auf der Kompilationszeit basierenden Tools können `dynamic`-Aufrufe nicht prüfen. Die Analysen "sehen" diese Stellen nicht.

Microsoft selbst empfiehlt in den Roslyn Code Quality Analyzers (CA-Regeln) die Vermeidung von `dynamic` außerhalb explizit COM-Interop-Szenarien. Die Roslyn-Analyzer-Regel `CA1050` und verwandte Checks spiegeln diesen Konsens wider. Dedizierte empirische Studien zur Fehlerrate bei `dynamic`-Verwendung existieren nicht (Cluster D: "Keine empirischen Studien gefunden; nur Microsoft-Designempfehlungen"), aber die theoretische Begründung — Verlust statischer Garantien — ist in der Literatur unbestritten.

## KI-Agenten-Perspektive

`dynamic` ist für LLM-Agenten besonders problematisch. Liu et al. (2024/2025) klassifizieren "Project Context Conflicts" — fehlerhafte Annahmen über Typen und Methoden-Signaturen — als die häufigste Halluzinationsart in echten Projekten. `dynamic`-Typen verstärken dieses Problem direkt: Ein Agent der `dynamic`-Code liest, kann keine verlässlichen Aussagen über die verfügbaren Operationen machen, weil diese erst zur Laufzeit bestimmt werden. Dies führt zu spekulativen Annahmen und erhöhter Halluzinationswahrscheinlichkeit.

Die Agent-Failure-Taxonomie (arXiv:2604.03515, 2025) zeigt, dass Repository-Kontext-Fehler über 50 % der Gesamtausfälle ausmachen. `dynamic`-Code erzeugt systematisch solche Lücken im Typ-Verständnis. Ein totales Verbot von `dynamic` ist aus Agent-Perspektive eine der klarsten und wirksamsten Qualitätsregeln überhaupt — sie stellt sicher, dass das LLM im statischen Typsystem arbeitet, das es aus dem Training bestens kennt.

## Zeitliche Einordnung

**Grundlagenstabilität:** Zeitlos

Das Problem ist strukturell: `dynamic` ist eine Designentscheidung die statische Typ-Garantien aufgibt. Auch deutlich leistungsfähigere Modelle können keine verlässlichen Aussagen über `dynamic`-Objekte machen, weil die Information schlicht nicht im Quellcode steht. Diese Einschränkung ist keine Schwäche aktueller Modellgenerationen, sondern eine fundamentale Eigenschaft von Laufzeit-Polymorphismus.

## Risiken / Gegenargumente

Das einzige legitime Verwendungsfeld von `dynamic` in C# sind COM-Interop-Szenarien (Office-Automatisierung, ältere Windows-APIs), wo die Typen zur Compilezeit nicht bekannt sind. In modernen .NET-Projekten (CLI-Tools, Web-APIs, gRPC-Services) gibt es diesen Bedarf praktisch nie. Falls COM-Interop tatsächlich benötigt wird, ist es ein gerechtfertigter lokaler Sonderfall der via Baseline (F01) oder explizite Ausnahme gehandhabt werden kann. Das vollständige Verbot mit "kein Opt-out" erscheint für ein CLI-Tool wie AiNetLinter (kein COM, kein Office-Automatisierungs-Scope) korrekt. Für allgemeinere C#-Projekte könnte eine zielgerichtete Opt-out-Option (via `PathOverrides`) für COM-Interop-Layer sinnvoll sein.

---

## Quellen

- C# Community & Blog-Konsens — Out Parameters and Dynamic Keyword Code Smells; FxCop / Roslyn Code Quality Analyzers (https://albertherd.com/2017/10/10/on-the-usage-of-out-parameters/)
- Microsoft .NET Design Guidelines, 2024 (https://learn.microsoft.com/en-us/dotnet/csharp/)
- Liu et al. — LLM Hallucinations in Practical Code Generation, arXiv:2409.20550, 2024/2025 (https://dl.acm.org/doi/epdf/10.1145/3728894)
- Empirical Agent Framework Studies — Inside the Scaffold: Agent Failure Taxonomy, arXiv:2604.03515, 2025
