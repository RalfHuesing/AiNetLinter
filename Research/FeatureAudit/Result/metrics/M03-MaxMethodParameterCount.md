# MaxMethodParameterCount (M03)

**Kategorie:** Numerische Metrik  
**Aktueller Wert:** 4 (in Tests: 6; `CancellationToken` wird nicht gezählt) | **Severity:** error | **Status:** Aktiv  
**Paper-Cluster genutzt:** A, D, E, F

---

## Bewertung

🟢 **WERTVOLL**

**Fazit:** Der Grenzwert von 4 Parametern ist empirisch gut begründet und entspricht dem Industriekonsens; die Whitelist für `CancellationToken` und die höhere Grenze in Tests sind sinnvolle Pragmatismus-Ausnahmen.

---

## Empfohlene Range

| | Wert | Begründung |
|--|------|-----------|
| **Untergrenze (sinnlos darunter)** | 2 | Unter 2 Pflichtparameter ist die Regel so restriktiv, dass selbst einfache Fabrik-Methoden Violations produzieren |
| **Empfehlung (beste Evidenz)** | 4 | Fowler (1999): „mehr als 3–4 Parameter → Refactoring-Kandidat"; Microsoft DI-Guidelines: 3–4 Abhängigkeiten als Richtwert; Industriekonsens |
| **Obergrenze (Nutzen geht verloren)** | 6 | Ab 7 Parametern sind alle Quellen (Fowler, Clean Code, Microsoft) einig: Refactoring zwingend; bei 6 ist das Risiko noch überschaubar |
| **Aktueller Wert** | 4 | Angemessen — trifft den Industriekonsens exakt |

---

## Wissenschaftliche Grundlage

Fowler & Beck (1999/2018) benennen „Long Parameter List" explizit als Code Smell mit der Empfehlung: Ab 4–5 Parametern ist ein Parameter-Objekt (Record/DTO) zu bevorzugen. Dies ist konzeptuelle Grundlage, keine empirische Studie mit Grenzwert-Validierung.

Yamashita & Moonen (2013) zeigen, dass „Long Parameter List" oft gemeinsam mit anderen Smells auftritt (Inter-Smell-Relationen), was den Wartungsaufwand kumulativ erhöht. Palomba et al. (2017) identifizieren Methoden-bezogene Smells als signifikante Defektprädikatoren. Eine direkte Studie zu maximalen Parameterzahlen in C# fehlt; der Grenzwert 4 ist industrieller Konsens, kein statistisch validierter Schwellenwert.

Die Whitelist-Funktion (`MethodParameterCountIgnoreTypeNames`) für Infrastruktur-Typen (z. B. `ILogger`, `IOptions`) ist sinnvoll, da diese Typen von Entwicklern nicht bewusst ausgewählt werden, sondern durch DI-Frameworks vorgegeben sind. Ihre Zählung würde valide Klassen fälschlicherweise bestrafen.

## KI-Agenten-Perspektive

Aus LLM-Perspektive sind viele Parameter ein Orientierungsproblem: Ein Agent der eine Methode aufrufen soll, muss alle Parameter kennen, die richtigen Werte ableiten und die Reihenfolge korrekt einhalten. Bei 7+ Parametern steigt die Wahrscheinlichkeit, dass der Agent einen Parameter verwechselt oder vergisst — insbesondere wenn Parameter ähnliche Typen haben (z. B. mehrere `string`-Parameter).

Die Studie von Xie et al. (2026) zur „branching-induced divergence" ist hier indirekt relevant: Methoden mit vielen Parametern haben oft auch viele Verzweigungen (verschiedene Kombinationen von Parametern führen zu verschiedenen Pfaden), was die Pfad-Komplexität für LLMs erhöht.

Liu et al. (2024/2025) zu LLM-Halluzinationen identifizieren „Project Context Conflicts" als häufigste Halluzinationsart — Agenten verwechseln Methoden-Signaturen in komplexen Projekten. Methoden mit wenigen, klar benannten Parametern reduzieren diese Verwechslungsgefahr.

(Ableitung: Kein direktes Paper belegt den Zusammenhang zwischen Parameteranzahl und LLM-Fehlerrate für C#.)

## Zeitliche Einordnung

**Grundlagenstabilität:** Zeitlos

Das kognitive Problem mit vielen Parametern ist strukturell: Das Arbeitsgedächtnis von Menschen wird überfordert; die Attention-Mechanismen von LLMs müssen mehr potenzielle Argument-Kombinationen berücksichtigen. Beide Aspekte sind unabhängig von Modellgenerationen stabil. Parameter-Records als Alternative bleiben auch in zukünftigen C#-Versionen ein valides Pattern.

---

## Empfehlung

**Aktion:** Wert beibehalten (4 / 6 in Tests)  
**Begründung:** Der Wert 4 entspricht dem Industriekonsens und der Empfehlung von Fowler (1999/2018); die Ausnahmen für Tests und CancellationToken sind pragmatisch korrekt.

---

## Quellen

- Fowler & Beck (1999/2018): „Refactoring: Improving the Design of Existing Code" — Addison-Wesley
- Yamashita & Moonen (2013): „Exploring the Impact of Inter-Smell Relations on Software Maintainability" — IEEE ICSM 2013
- Palomba et al. (2017): „On the Diffuseness and the Impact on Maintainability of Code Smells" — Empirical Software Engineering, Springer, DOI:10.1007/s10664-017-9535-z
- Microsoft .NET Dependency Injection Guidelines — learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/guidelines
- Liu et al. (2024/2025): „LLM Hallucinations in Practical Code Generation" — arXiv:2409.20550
- Xie et al. (2026): „Rethinking Code Complexity Through the Lens of Large Language Models" — arXiv:2601.20404
