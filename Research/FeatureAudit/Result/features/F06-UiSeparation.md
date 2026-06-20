# UiSeparation — Blazor / WPF (F06)

**Kategorie:** Konfigurationsfeature  
**CLI-Flag / Konfiguration:** `rules.json → UiSeparation`  
**Status:** Vorhanden

---

## Bewertung

🟢 **WERTVOLL**

**Fazit:** Die erzwungene Code-Behind-Trennung für Blazor und WPF ist eine direkte Umsetzung etablierter UI-Architekturprinzipien (MVVM, MVC) und verhindert das häufigste LLM-Antipattern beim Generieren von UI-Code: das Vermischen von UI-Markup und Geschäftslogik in einer Datei.

---

## Empfehlung

**Aktion:** Beibehalten  
**Begründung:** Das Feature erzwingt eine strukturelle Trennungslinie, die LLMs beim Generieren von Blazor/WPF-Code häufig ignorieren — sie tendieren dazu, Logik direkt in `.razor`-Dateien zu schreiben (da das syntaktisch einfacher ist). Die Regel zwingt zur korrekten `.razor.cs`-Trennung.

---

## Nutzen-Analyse

UiSeparation erzwingt zwei verwandte Strukturprinzipien:

1. **Blazor Code-Behind:** UI-Markup in `.razor`, Logik ausschließlich in `.razor.cs`. Verhindert Code-Bloat in Markup-Dateien.
2. **WPF minimaler Code-Behind:** UI-Definition in XAML, minimale Ereignisbehandlung in `.xaml.cs`, Logik in ViewModels. Optional: CSS-Isolation für Blazor.

**Warum das bei LLMs besonders relevant ist:**

LLM-Agenten generieren bei UI-Code besonders häufig Anti-Pattern, weil die Grenze zwischen Markup und Code im Trainingsdatensatz häufig verwischt ist — viele Online-Tutorials und Stack-Overflow-Antworten zeigen pragmatische "Quick-and-Dirty"-Lösungen mit Code direkt in `.razor`-Dateien. AiNetLinters UiSeparation-Regel korrigiert diesen Bias direkt.

**Szenarien wo es wertvoll ist:**
- Projekte mit Blazor-Server oder Blazor-WebAssembly-Komponenten
- WPF-Anwendungen mit MVVM-Architektur
- Teams die Code-Reviews für UI-Layer effizienter gestalten wollen

**Szenarien wo es irrelevant ist:**
- Projekte ohne UI (CLI-Tools, Bibliotheken, Microservices)
- Blazor-Projekte die bewusst das Inline-Code-Pattern verwenden (Single-File-Components)

**Besonderheit:** AiNetLinter ist mit diesem Feature in einer Nische die andere Linter nicht abdecken — UI-Architektur-Enforcement auf Linter-Ebene.

---

## Vergleich: Andere Tools

| Tool | UI-Architektur-Enforcement | Ansatz |
|------|---------------------------|--------|
| **StyleCop** | Keine UI-spezifischen Regeln | Fokus auf C#-Code-Stil; ignoriert Markup |
| **SonarQube** | Begrenzte Blazor-Unterstützung | Einige C#-Regeln; kein spezifisches Code-Behind-Enforcement |
| **Roslyn Analyzers** | Keine UI-Architektur-Regeln | Syntax-Level; keine Cross-File-Strukturprüfung |
| **FxCop** | Keine UI-spezifischen Regeln | Generische .NET-Regeln |
| **NetArchTest** | Nur in Unit-Tests | Architektur-Tests müssen manuell geschrieben werden |
| **AiNetLinter** | UiSeparation in rules.json | Direktes CLI-basiertes Enforcement; Blazor + WPF |

AiNetLinter ist hier einzigartig: Kein anderes der verglichenen Tools bietet eine direkte, konfigurierbare Regel für die Code-Behind-Trennung in Blazor/WPF als CLI-Feature. Das ist ein echter Differenzierungspunkt.

**Einschränkung:** NetArchTest/ArchUnitNET könnten ähnliche Regeln als Unit-Tests formulieren, aber das erfordert manuellen Aufwand und ist weniger "out of the box" nutzbar.

---

## KI-Agenten-Perspektive

UiSeparation ist für LLM-Agenten ein besonders wertvolles Feature:

1. **Korrektur von Trainings-Bias:** LLMs wurden auf großen Mengen Tutorial-Code trainiert, der häufig Code direkt in `.razor`-Dateien zeigt. AiNetLinters UiSeparation-Regel korrigiert diesen Bias systematisch — der Agent bekommt einen Linter-Fehler und lernt, Code-Behind zu verwenden.

2. **Kontext-Klarheit:** Wenn Markup und Logik strikt getrennt sind, kann ein Agent effizienter navigieren. Er weiß: Logik ist in `.razor.cs`, Markup ist in `.razor`. Diese Vorhersagbarkeit reduziert die "Kontext-Verwirrung" (der häufigste Fehlertyp laut "Inside the Scaffold", arXiv:2511.00872).

3. **Halluzinations-Prävention:** Ein Agent der Blazor-Komponenten generiert, halluziniert seltener, wenn die Dateistruktur vorhersagbar ist. Wenn er weiß, dass Logik immer in `.razor.cs` liegt, sucht er dort und findet die richtigen Typen.

---

## Zeitliche Einordnung

**Grundlagenstabilität:** Zeitlos

Die Trennung von UI-Markup und Geschäftslogik ist ein fundamentales Software-Engineering-Prinzip (Separation of Concerns, SoC), das unabhängig von Modellgenerationen gilt. Auch zukünftige LLM-Agenten werden von einer klaren UI-Architektur profitieren.

---

## Quellen

- Microsoft Documentation (2024) — Blazor Code-Behind: https://learn.microsoft.com/en-us/aspnet/core/blazor/components/?view=aspnetcore-9.0#code-behind
- Microsoft Documentation (2024) — WPF MVVM Pattern: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/data/?view=netdesktop-9.0
- arXiv:2511.00872 (2024) — Inside the Scaffold: Agent Failure Taxonomy
- arXiv:2601.20404 (2025) — On the Impact of AGENTS.md Files on the Efficiency of AI Coding Agents
