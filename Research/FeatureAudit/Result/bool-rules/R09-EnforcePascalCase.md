# EnforcePascalCase (R09)

**Kategorie:** Boolean-Regel  
**Aktueller Wert:** true | **Status:** Aktiv  
**Severity:** error  
**Paper-Cluster genutzt:** D, C

---

## Bewertung

🟢 **WERTVOLL**

**Fazit:** Empirisch direkt belegt durch Du et al. (2025) — inkonsistente Case-Konventionen verschlechtern LLM-Verständnis und Generierungsqualität messbar; PascalCase ist der unbestrittene C#-Standard für alle öffentlichen Bezeichner; Behalten.

---

## Empfehlung

**Aktion:** Aktiviert lassen  
**Begründung:** PascalCase für öffentliche Typen, Methoden und Properties ist die .NET-Kernkonvention seit Framework 1.0 — ein Abweichen erzeugt sofort Inkonsistenz mit der gesamten .NET-Standardbibliothek. Zusätzlich belegt Du et al. (2025) empirisch dass Case-Inkonsistenzen Tokenisierungsfehler in LLMs verursachen und SBERT-Scores (semantische Ähnlichkeit) signifikant senken.

---

## Wissenschaftliche / Empirische Grundlage

PascalCase für öffentliche C#-Bezeichner ist in den Microsoft .NET Identifier Naming Guidelines als verbindliche Konvention definiert. Diese Konvention gilt für Typen, Methoden, Properties, Events und Namespaces. camelCase ist für lokale Variablen und private Felder vorgesehen. Die Konsistenz mit der gesamten .NET-Standardbibliothek (alle BCL-Typen und -Methoden folgen PascalCase) macht die Konvention praktisch zwingend für C#-Code der zusammen mit .NET-Bibliotheken eingesetzt wird.

Die LLM-spezifische Evidenz ist direkt: Du et al. (arXiv:2503.17407, 2025) belegten in einer kontrollierten Studie mit Claude 3.5 Sonnet, GPT-4o, Gemini 2.0 Flash und DeepSeek-V3, dass inkonsistente Bezeichner-Case-Konventionen Tokenisierungsfehler verursachen. Transformer-Modelle splitten Bezeichner an Case-Grenzen in Subtoken auf — `GetUserName` wird zu `[Get, User, Name]`, `getUserName` zu `[get, User, Name]`, `getusername` zu einem einzelnen unbekannten Token. Abweichungen von der erwarteten Case-Konvention verwirren die Attention-Pfade und senken BLEU und SBERT-Scores signifikant.

Cluster F (Du et al., 2025 — ebenfalls referenziert) bestätigt: LLM-generierter Code weist häufig generische, schlechtformatierte Namen auf, wenn keine Linter-Regeln erzwingen was die Konvention sein soll. Die Interaktion zwischen R09 und R11 (EnforceSemanticNaming) ist hier besonders wertvoll: R09 erzwingt die Struktur, R11 erzwingt den Inhalt der Namen.

## KI-Agenten-Perspektive

Für LLM-Agenten ist PascalCase in C# eine der am stärksten im Training verankerten Konventionen — alle bekannten .NET-Bibliotheken, alle Microsoft-Dokumentations-Snippets und alle C#-Open-Source-Projekte folgen ihr. Ein Agent der neuen Code schreibt wird PascalCase in der Regel korrekt anwenden. Der Wert von R09 liegt weniger im Schutz vor Agent-Fehlern, sondern im Schutz vor:

1. **Menschlichen Fehlern bei der Namensgebung** die ein Agent dann imitiert (Muster-Reproduktion aus Kontext).
2. **Schnittstellen zu Fremd-Code** der andere Konventionen hat (z.B. Python-generierter Code mit snake_case).
3. **Langfristiger Konsistenz** wenn mehrere Agenten und Entwickler an derselben Codebase arbeiten.

Du et al. (2025) zeigen konkret: Selbst kleine Inkonsistenzen in Case-Konventionen senken die Generierungsqualität nachfolgender LLM-Aufrufe — ein Agent der in einem inkonsistent formatierten Kontext arbeitet, produziert schlechtere Outputs. R09 schützt diesen Kontext.

## Zeitliche Einordnung

**Grundlagenstabilität:** Zeitlos

PascalCase ist keine temporäre Konvention — sie ist strukturell in der .NET-BCL verankert und wird sich nicht ändern. Die LLM-Tokenisierungsabhängigkeit von Code-Formatierung (Du et al. 2025) ist ebenfalls ein zeitstabiles Charakteristikum von Transformer-Architekturen.

## Risiken / Gegenargumente

Das einzige Gegenargument: In bestimmten Szenarien ist abweichendes Casing technisch erzwungen — z.B. bei der Implementierung von Interfaces aus Bibliotheken mit anderen Konventionen, oder bei JSON-Serialisierung wo Property-Namen dem JSON-Schema entsprechen müssen. Diese Fälle sind durch Attribute (`[JsonPropertyName]`) oder explizite Serializer-Konfiguration lösbar, ohne die Benennung selbst zu ändern. Die Severity `error` ist für diese Regel korrekt und angemessen — Naming-Inkonsistenz ist kein marginales Problem.

---

## Quellen

- Microsoft .NET Identifier Naming Guidelines, 2024 (https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/identifier-names)
- Du et al. — The Hidden Cost of Readability: How Code Formatting Silently Affects LLMs, arXiv:2503.17407, 2025 (https://xiaoningdu.github.io/assets/pdf/format.pdf)
- Empirical LLM Code Smell Analysis — Naming Habits and Hybrid Detection Systems, via Web-Suche, 2024/2025
