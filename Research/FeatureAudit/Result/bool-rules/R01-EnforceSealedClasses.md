# EnforceSealedClasses (R01)

**Kategorie:** Boolean-Regel  
**Aktueller Wert:** true | **Status:** Aktiv (in `*.Tests`-Projekten deaktiviert)  
**Severity:** error  
**Paper-Cluster genutzt:** C, D

---

## Bewertung

🟢 **WERTVOLL**

**Fazit:** Klar belegt — `sealed` verbessert JIT-Performance durch Devirtualisierung, reduziert Typhierarchie-Komplexität und minimiert das Risiko von Agenten-Halluzinationen über potenzielle Unterklassen; Regel beibehalten.

---

## Empfehlung

**Aktion:** Aktiviert lassen  
**Begründung:** Die Kombination aus messbarem Performance-Gewinn (Devirtualisierung/Inlining), klar reduzierter OO-Komplexität und Microsofts eigenem Best-Practice-Konsens ergibt eine starke, mehrschichtige Grundlage. Die Ausnahme für Test-Projekte und Suffix-basierte Ausnahmen (`Base`, `Foundation`, `Host`) sind pragmatisch korrekt gesetzt.

---

## Wissenschaftliche / Empirische Grundlage

Microsoft empfiehlt in den offiziellen .NET Design Guidelines, Klassen standardmäßig als `sealed` zu deklarieren, es sei denn, sie sind explizit für Vererbung entworfen. Diese Empfehlung spiegelt einen Paradigmenwechsel wider: Vererbbarkeit ist keine Grundannahme mehr, sondern eine bewusste Design-Entscheidung.

Der technische Haupteffekt von `sealed` ist die JIT-Devirtualisierung im RyuJIT-Compiler. Virtuelle Methodenaufrufe erfordern einen teuren vtable-Lookup; der Compiler kann diesen bei versiegelten Klassen durch direkte Aufrufe ersetzen und die Methode vollständig inlinen. Benchmarks aus der .NET-Community (Meziantou 2022–2026, code-maze.com) zeigen Einsparungen von ~0,3 Nanosekunden pro Aufruf und bis zu 15–30 % Leistungssteigerung auf Hot Paths. Auf Anwendungsebene ist der Effekt meistens gering, auf Bibliotheks- und Framework-Ebene jedoch signifikant.

Aus architektonischer Perspektive verdeutlicht `sealed` den Typ als "intern geschlossene Einheit". Dies reduziert die kognitive Last beim Lesen von Code: Ein Entwickler (oder Agent) muss nicht prüfen, ob eine Methode möglicherweise in einer abgeleiteten Klasse überschrieben wurde. Der Verhaltensvertrag ist vollständig in der versiegelten Klasse enthalten.

Die Ausnahmen für `Base`-, `Foundation`- und `Host`-Klassen sind designtechnisch korrekt: Diese Suffixe signalisieren explizit die Vererbungsabsicht.

## KI-Agenten-Perspektive

Für LLM-Agenten wie Claude Code, Cursor oder GitHub Copilot vereinfacht `sealed` die Reasoning-Aufgabe erheblich. Wenn ein Agent eine Methode aufgerufen sieht, muss er bei versiegelten Klassen nicht den gesamten Vererbungsbaum traversieren, um das tatsächliche Verhalten zu bestimmen — die Klasse ist die vollständige Quelle der Wahrheit. Liu et al. (2024/2025) zeigen, dass "Project Context Conflicts" die häufigste Kategorie von Code-Halluzinationen sind; komplexe Typhierarchien, bei denen das Verhalten je nach Unterklasse variiert, sind ein direkter Treiber dieses Problems. `sealed` eliminiert diese Ambiguität vollständig (Ableitung, kein direktes Paper zu `sealed` + LLM-Fehlerrate).

Zusätzlich neigen LLM-Agenten bei nicht versiegelten Klassen dazu, fälschlicherweise Unterklassen zu generieren oder anzunehmen, wenn sie Code in einem Kontext anpassen sollen. Die Studie zu PureAI-Projekten (arXiv:2511.00872, 2025) zeigt, dass LLMs Vererbungshierarchien stark vereinfachen und häufig unstrukturierte monolithische Klassen erzeugen. Eine Regel die `sealed` erzwingt, verhindert, dass Agenten inadäquate Hierarchien einführen.

## Zeitliche Einordnung

**Grundlagenstabilität:** Zeitlos

`sealed` ist eine fundamentale Eigenschaft des .NET-Typsystems und der JIT-Kompilierung — technisch verankert und nicht modellgenerations-spezifisch. Selbst wenn LLMs deutlich leistungsfähiger werden, bleibt die Reduktion von Verhaltensambiguität durch geschlossene Typen ein strukturelles Designprinzip, das unabhängig von AI-Fähigkeiten gilt.

## Risiken / Gegenargumente

Das häufigste Gegenargument: `sealed` verhindert Testbarkeit, da Mocking-Frameworks (Moq, NSubstitute) keine versiegelten Klassen mocken können. Dies ist ein reales Problem in Test-Projekten — daher ist die Deaktivierung in `*.Tests`-Projekten korrekt und notwendig. In Produktions-Code ist die Einschränkung jedoch als Design-Signal erwünscht: Wenn eine Klasse gemockt werden muss, sollte sie hinter einem Interface stehen, nicht vererbt werden. Dieses Gegenargument ist bekannt und gut gehandhabt. Ein weiteres Argument: Legacy-Code-Migration kann schwierig sein, wenn vorhandener Code von nicht-versiegelten Klassen ableitet — hier greift der Baseline/Ratchet-Mechanismus (F01) als Übergangslösung.

---

## Quellen

- Microsoft .NET Design Guidelines — Identifier names & Coding Style, 2024 (https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/identifier-names)
- Meziantou's Blog — Performance Benefits of Sealed Classes in RyuJIT, 2022–2026 (https://www.meziantou.net/performance-benefits-of-sealed-class.htm)
- Liu et al. — LLM Hallucinations in Practical Code Generation, arXiv:2409.20550, 2024/2025 (https://dl.acm.org/doi/epdf/10.1145/3728894)
- Empirical Agent Framework Studies — Inside the Scaffold: Agent Failure Taxonomy, arXiv:2604.03515, 2025
- Concordia University — OpenClassGen: A Large-Scale Dataset for Class-Level Code Generation, arXiv:2511.00872, 2025/2026
