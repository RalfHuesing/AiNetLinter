# PathOverrides (F05)

**Kategorie:** Konfigurationsfeature  
**CLI-Flag / Konfiguration:** `rules.json → PathOverrides`  
**Status:** Vorhanden, aktuell leer

---

## Bewertung

🟡 **UNPRAKTIKABEL**

**Fazit:** PathOverrides hat legitimen Nutzen (Ausnahmen auf Verzeichnis- oder Dateiebene unterhalb der Projektgrenze), ist aber aktuell ungenutzt und bietet überlappende Funktionalität mit ProjectOverrides — der Mehrwert ist gering bis der erste konkrete Use-Case auftritt.

---

## Empfehlung

**Aktion:** Beibehalten, aber nicht aktiv bewerben  
**Begründung:** Das Feature ist vorhanden und kostet im aktuellen Zustand (leer) nichts. Es gibt legitime Szenarien (z.B. Ausnahmen für einen `Generated/`-Unterordner innerhalb eines ansonsten regulären Projekts), aber F07 FileFilters deckt den häufigsten Use-Case bereits ab. Nur wenn konkrete Anforderungen entstehen, die F07 nicht abdeckt, lohnt sich die aktive Nutzung.

---

## Nutzen-Analyse

PathOverrides adressiert ein spezifischeres Granularitätsniveau als ProjectOverrides:

- **ProjectOverrides** = Ausnahme für ein ganzes Teilprojekt (z.B. `*.Tests`)
- **PathOverrides** = Ausnahme für ein bestimmtes Verzeichnis oder Datei-Pattern innerhalb eines Projekts (z.B. `src/MyProject/Legacy/**`)

**Szenarien wo es wertvoll sein könnte:**
- Ein bestimmtes Unterverzeichnis enthält Legacy-Code, der nicht aktuell migriert werden kann, aber kein eigenes Teilprojekt ist.
- Spezielle Verzeichnisse wie `Migrations/` haben andere Regeln als der Rest des Projekts.
- Framework-generierte Dateien die nicht per Dateiname (`*.g.cs`) identifizierbar sind, aber in einem bekannten Verzeichnis liegen.

**Überlappung mit anderen Features:**
- F07 FileFilters deckt bereits Dateinamen-basierte Ausnahmen (`*.g.cs`, `[GeneratedCode]`) ab
- F04 ProjectOverrides deckt Projekt-Level-Ausnahmen ab
- PathOverrides ist die Lücke dazwischen: Verzeichnis-Level innerhalb eines Projekts

**Warum aktuell leer:**
Das deutet darauf hin, dass die bestehende Kombination aus F04 und F07 die meisten realen Use-Cases ausreichend abdeckt. PathOverrides ist ein "Nice-to-have" für komplexere Setups.

---

## Vergleich: Andere Tools

| Tool | Pfad-spezifische Overrides | Ansatz |
|------|---------------------------|--------|
| **ESLint** | `overrides[].files` mit Glob | Sehr granular; Regex auf Dateiebene möglich |
| **dotnet format** | `.editorconfig` per Verzeichnis | Vererbbare Konfiguration; sehr granular |
| **StyleCop** | Dateiebene-Suppressions | Keine Verzeichnis-Konfiguration; nur Datei-Attribut |
| **SonarQube** | Source Exclusions per Path-Pattern | Ausschluss ganzer Verzeichnisse; keine Regelanpassung |
| **AiNetLinter** | `PathOverrides` in `rules.json` | Verzeichnis-Granularität; aktuell ungenutzt |

ESLints Ansatz (Glob auf Dateiebene) ist mächtiger als AiNetLinters PathOverrides — ESLint erlaubt unterschiedliche Regeln für beliebige Datei-Patterns. AiNetLinters PathOverrides hat einen engeren Scope (Verzeichnisse, keine Einzel-Dateien per Pattern), was einfacher zu konfigurieren aber weniger flexibel ist.

---

## KI-Agenten-Perspektive

PathOverrides hat für LLM-Agenten einen indirekten Nutzen:

1. **Reduzierung von Konfigurations-Rauschen:** Wenn ein Agent einen Linter-Fehler erhält, der auf Legacy-Code in einem bekannten Verzeichnis zurückzuführen ist, sollte dieser via PathOverride ausgenommen sein — damit der Agent nicht versucht, unzumutbaren Legacy-Code zu reparieren.

2. **Konfigurierbarkeit durch Agenten:** Ein Agent könnte PathOverrides selbst setzen — z.B. beim Einrichten von AiNetLinter in einem Projekt mit einem bekannten Legacy-Unterverzeichnis. Dafür wäre maschinenlesbares Discovery des PathOverrides-Formats hilfreich.

Der Nutzen ist real aber nicht dringend — solange PathOverrides leer bleibt, hat es keinen direkten Einfluss auf den agentic Workflow.

---

## Zeitliche Einordnung

**Grundlagenstabilität:** Zeitlos

Der Bedarf nach pfadgranularen Ausnahmen ist strukturell (Legacy-Verzeichnisse, Framework-generierter Code in regulären Projekten). Das Feature bleibt nützlich, sobald Use-Cases auftreten.

---

## Quellen

- ESLint Documentation (2024) — Configuration Overrides with File Globs: https://eslint.org/docs/latest/use/configure/configuration-files#configuration-objects
- dotnet format Documentation (2024) — .editorconfig: https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/configuration-files
- SonarQube Documentation (2024) — Excluding Files and Directories: https://docs.sonarsource.com/sonarqube/latest/project-administration/analysis-scope/
