# PreventContextDependentOverloads (R17)

**Kategorie:** Boolean-Regel  
**Aktueller Wert:** false (deaktiviert) | **Status:** Deaktiviert  
**Severity:** error (wenn aktiviert)  
**Paper-Cluster genutzt:** D, C

---

## Bewertung

🟡 **UNPRAKTIKABEL**

**Fazit:** Das Problem ist real — kontextabhängige Overloads (die sich nur durch Infrastructure-Typen wie `HttpContext` oder `DbContext` unterscheiden) vergrößern die API-Oberfläche sinnlos und verwirren LLM-Agenten bei der Auflösung von Overload-Sets — aber die Regel ist zu eng scoped und schwer implementierbar ohne lange Konfigurationslisten.

---

## Empfehlung

**Aktion:** Deaktiviert lassen (optional: als `warning` aktivieren wenn Overload-Komplexität ein bekanntes Problem im Projekt ist)  
**Begründung:** Kontextabhängige Overloads sind in der Praxis selten genug, dass eine `error`-Severity unverhältnismäßig wäre; das Problem wird besser durch Code-Reviews als durch Linter-Erzwingung adressiert.

---

## Wissenschaftliche / Empirische Grundlage

**M07 (MaxMethodOverloads)** begrenzt bereits die Gesamtanzahl von Overloads pro Methode auf 3. Das ist die stärkere und allgemeinere Regel. R17 würde einen Spezialfall davon abdecken: Overloads die sich nur durch Kontext-Typen unterscheiden.

**Warum ist das problematisch?** Overloads, die sich nur durch `HttpContext` vs. Kein-HttpContext unterscheiden, signalisieren typischerweise schlechtes Separation-of-Concerns: Die Methode müsste nicht den HttpContext kennen — der Aufrufer sollte stattdessen die benötigten Daten extrahieren und als primitive Parameter übergeben. Das ist dasselbe Argument wie bei R14 (EnforceMinimalApiAsParameters).

**Empirische Grundlage:** Es gibt keine direkte Studie zu "Overloads die sich durch Context-Typen unterscheiden" als eigenständiges Muster. Die Evidenz ist abgeleitet aus:
1. Microsoft Design Guidelines: Overloads sollten denselben konzeptionellen Task mit unterschiedlichen Parametern ausführen — nicht denselben Task für unterschiedliche Aufrufkontexte.
2. M07-Logik: Mehr als 3 Overloads vergrößern Overload-Resolution-Komplexität für Compiler und LLM gleichermaßen.

(Ableitung, kein direktes Paper für den spezifischen Fall)

## KI-Agenten-Perspektive

Aus Cluster C ("LLM Hallucinations in Practical Code Generation", Liu et al. 2025): **Project Context Conflicts** entstehen, wenn ein LLM-Agent das falsche Overload aufruft. Kontextabhängige Overloads (mit `HttpContext`, `DbContext`, `IServiceProvider`) vergrößern den Suchraum des Agenten bei der Overload-Auflösung und erhöhen die Wahrscheinlichkeit, das falsche zu wählen.

**Praktische Relevanz:** In AiNetLiners Zieldomäne (C#-Qualitätstools für LLM-Agenten) sind HttpContext/DbContext-Overloads nicht das dominante Muster. Die Regel hätte höheren Wert in Web-API-Projekten.

## Zeitliche Einordnung

**Grundlagenstabilität:** Offen

Das Prinzip "Kontextfreiheit in Methoden-Signaturen" ist zeitlos (Separation of Concerns). Die Relevanz der spezifischen Kontext-Typen (`HttpContext`, `DbContext`) hängt von der .NET-Ökosystem-Entwicklung ab — neue Framework-Muster könnten andere Kontext-Typen einführen, die nicht auf der Verbotsliste stehen.

## Risiken / Gegenargumente

**Konfigurationsaufwand für Context-Typen-Liste:** Um wirksam zu sein, müsste die Regel eine Liste verbotener "Context-Typen" führen. Diese Liste muss gepflegt werden wenn neue Framework-Typen hinzukommen.

**Legitime Use Cases:** In bestimmten Integrations-Patterns (z.B. Extension Methods für IServiceCollection) können HttpContext-Overloads legitim sein. Die Trennlinie zwischen "schlechtem Overload" und "sinnvoller Overload-Variante" ist kontextabhängig.

**Überlappung mit M07:** MaxMethodOverloads (aktuell: 3) begrenzt bereits die Gesamtanzahl und macht R17 teilweise redundant.

---

## Quellen

- Microsoft .NET Design Guidelines — Method Overloads: https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/member-overloading
- Liu et al., 2024/2025, "LLM Hallucinations in Practical Code Generation" — arXiv:2409.20550
- C# Community & Blog-Konsens (via Cluster D) — Out Parameters and Dynamic Keyword Code Smells
