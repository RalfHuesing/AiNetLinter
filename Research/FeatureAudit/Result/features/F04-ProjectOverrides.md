# ProjectOverrides (F04)

**Kategorie:** Konfigurationsfeature  
**CLI-Flag / Konfiguration:** `rules.json → ProjectOverrides`  
**Status:** Vorhanden (aktives Beispiel: `*.Tests` mit lockeren Limits)

---

## Bewertung

🟢 **WERTVOLL**

**Fazit:** Projektscharfe Regelabweichungen per Glob-Pattern sind ein essenzielles Feature für Mono-Repository-Setups und heterogene Codebases — ohne sie müsste jedes Teilprojekt eine eigene rules.json führen, was die zentrale Konfigurierbarkeit torpediert.

---

## Empfehlung

**Aktion:** Beibehalten  
**Begründung:** Das aktuelle `*.Tests`-Beispiel zeigt den Kern-Use-Case perfekt: Test-Code hat legitim andere Anforderungen (lockerere Komplexitätsgrenzen, keine sealed-Anforderung). ProjectOverrides löst dieses fundamentale Problem ohne Konfigurationsduplizierung.

---

## Nutzen-Analyse

ProjectOverrides ermöglicht projektspezifische Regelabweichungen durch Glob-Pattern-Matching auf dem Projektnamen. Der primäre Nutzen liegt in drei Szenarien:

1. **Test-Projekte:** Test-Code hat strukturell andere Anforderungen. Testmethoden dürfen länger sein (Setup, Arrange-Act-Assert), mehr Parameter haben (Testfall-Parameter) und benötigen kein sealed (Testklassen erben oft von Basis-Klassen). Das aktive `*.Tests`-Override ist ein Paradebeispiel.

2. **Generator-Projekte:** Generierter Code (z.B. EF Migrations, Protobuf-Stubs, Swagger-generierte Clients) kann per ProjectOverride aus bestimmten Regeln ausgenommen werden, ohne dass der Entwickler jede Datei einzeln suppressieren muss.

3. **Legacy-Projekte im Mono-Repo:** Ein Teilprojekt das noch nicht auf moderne Standards migriert wurde, kann temporär mit lockereren Grenzwerten geführt werden — als Zwischenschritt zur vollständigen Compliance.

**Szenarien wo es irrelevant ist:**
- Single-Projekt-Repositories ohne Teilprojekte
- Projekte bei denen alle Teilprojekte identische Anforderungen haben

---

## Vergleich: Andere Tools

| Tool | Projektspezifische Overrides | Ansatz |
|------|------------------------------|--------|
| **ESLint** | `.eslintrc` per Verzeichnis | Vererbbare Konfiguration; Override per `overrides`-Array in Config |
| **SonarQube** | Quality Profiles pro Projekt | Separate Profile pro Projekt; zentral verwaltbar |
| **StyleCop** | `stylecop.json` pro Projekt | Separate Konfigurationsdatei; keine Vererbung |
| **dotnet format** | `.editorconfig` + Vererbung | Verzeichnis-basierte Konfiguration; gut für Mono-Repos |
| **NDepend** | Rules per Projekt-Snapshot | Projekt-Ebene; komplexere Konfiguration |
| **AiNetLinter** | `ProjectOverrides` in `rules.json` | Zentralisiert; Glob-Pattern; einfach konfigurierbar |

AiNetLinters Ansatz — zentralisierte Konfiguration mit Glob-Pattern — ist ähnlich wie ESLints `overrides`-Array, aber in einer einzigen Konfigurationsdatei. Das ist ein Vorteil gegenüber dem StyleCop-Ansatz (separate Dateien pro Projekt), aber eine Einschränkung gegenüber ESLint (ESLint erlaubt auch regex-basierte Dateiname-Muster, nicht nur Projektname-Globs).

---

## KI-Agenten-Perspektive

Für LLM-Agenten ist ProjectOverrides ein wichtiges Konfigurationsfeature, aber kein direktes Workflow-Feature:

1. **Konfigurationsverständnis:** Ein Agent der `--list-rules` verwendet, sollte idealerweise auch die projektspezifischen Overrides sehen — damit er weiß, dass in `*.Tests`-Projekten andere Regeln gelten. Falls `--list-rules` nicht projektkontext-sensitiv ist, könnte der Agent falsche Annahmen treffen.

2. **Reduktion von False-Positives:** Wenn ein Agent Test-Code generiert und der Linter fehlerhafte Verstöße meldet (weil die Test-Overrides nicht wirken), bricht der agentic Workflow zusammen. ProjectOverrides verhindert diesen False-Positive-Pfad.

3. **Konfigurationsautomatisierung:** Ein Agent könnte ProjectOverrides selbst konfigurieren — z.B. beim Einrichten von AiNetLinter in einem Projekt. Discovery-Commands sollten dafür maschinenlesbare Output-Optionen bieten.

---

## Zeitliche Einordnung

**Grundlagenstabilität:** Zeitlos

Der Bedarf nach projektspezifischen Konfigurationsabweichungen in Multi-Projekt-Setups ist strukturell und unabhängig von Modellgenerationen. Er entsteht aus der heterogenen Natur realer Software-Repositories (Test vs. Produktion, Generated vs. Handgeschrieben, Legacy vs. Modern).

---

## Quellen

- Microsoft .NET Design Guidelines (2024) — Test Project Best Practices: https://learn.microsoft.com/en-us/dotnet/core/testing/
- ESLint Documentation (2024) — Configuration Overrides: https://eslint.org/docs/latest/use/configure/configuration-files#configuration-objects
- SonarQube Documentation (2024) — Quality Profiles: https://docs.sonarsource.com/sonarqube/latest/instance-administration/quality-profiles/
- Ben Morris (2023) — Writing ArchUnit-Style Tests for .NET and C#: https://www.ben-morris.com/writing-archunit-style-tests-for-net-and-c-for-self-testing-architectures/
