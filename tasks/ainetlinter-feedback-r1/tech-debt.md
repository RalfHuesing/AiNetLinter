---
task: ainetlinter-feedback-r1
type: tech-debt-log
maintained_by: kritiker
last_updated: 2026-08-15T20:15:00+02:00
---

# Tech-Debt-Log: ainetlinter-feedback-r1

## Index

| ID | Bereich / Datei | Priorität | Auto-Fixable | Kurzfassung |
|---|---|---|---|---|
| TD-01 | `src/AiNetLinter/Mcp/Tools/FileStructure/GetClassStructureTool.cs` | niedrig | nein | `includeAttributes` opt-in Parameter fehlt (Konzept-Punkt, Out-of-Scope in step-008) |
| TD-02 | `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs` + Konzept | mittel | nein | `includeSnippet` Default `false` weicht von Konzept-Wortlaut ab; Team-Entscheidung erforderlich |
| TD-03 | `src/AiNetLinter/Mcp/Tools/DuplicateDetection/DuplicateDetectionEngine.cs` + `GetViolationsScanner` | niedrig | nein | Test-Erkennung läuft in `find_duplicates` über `PathNormalizer.IsTestFile` + Project-Name-Suffix, in `get_violations` über `CheckerContext.IsTestFile`; mittelfristig `ITestDetector` interface mit einer Implementierung |

## Einträge

### TD-01: `includeAttributes` für `get_class_structure`

- **Gefunden in:** step-008 (Konzept-Punkt, explizit als Out-of-Scope markiert)
- **Bezug:** `konzept.md` → A → Edge-Cases: „`includeAttributes` (Default `false`): opt-in für Attribut-Listen pro Member (kostet Token, nicht jeder Agent braucht das)."
- **Beschreibung:** Aktueller Markdown-Output hat 6 Spalten (`Kind`, `Name`, `Visibility`, `Lines`, `LineCount`, `Signature`); eine 7. Spalte `Attributes` würde die Tabelle weiter aufblähen. Implementierung würde `ISymbol.GetAttributes()`-Iteration + Filter-Logik + Render-Logik erfordern.
- **Aufwand:** ~1 Commit (analog step-008 Größenordnung).
- **Nicht-blockierend:** Kein laufender Bedarf; Token-Budget-Garantie ist bereits eingehalten.

### TD-02: `includeSnippet`-Default-Diskrepanz

- **Gefunden in:** step-008-Review (Konzept-Wortlaut vs. Code-Default)
- **Bezug:** `konzept.md` → B → Edge-Cases: „Opt-out via `includeSnippet: bool = true`, falls ein Aufrufer nur die Metrik-Liste will (z. B. ein Bulk-Triage-Skript)."
- **Beschreibung:** Implementierung hat `includeSnippet` Default = `false` (token-schonender), Konzept-Wortlaut suggeriert `true`. Drei Optionen zur Auflösung:
  - (a) Konzept an Code anpassen: `includeSnippet: bool = false` als Default festlegen. Saubere semantische Lesart: „Snippets nur auf Anforderung".
  - (b) Code an Konzept anpassen: `includeSnippet: bool = true` als Default. Erhöht Token-Verbrauch bei `maxResults=50` um ~50 KB; Token-Budget-Garantie (50 KB) wäre potenziell verletzt.
  - (c) Hybrid: Default `false`, aber `contextLines > 0` auto-enablen.
- **Aufwand:** 1 Commit + Konzept-Nachtrag, je nach Entscheidung.
- **Nicht-blockierend:** Beide Optionen sind technisch sauber, die Frage ist semantisch/UX.

### TD-03: Geteilte `ITestDetector`-Schnittstelle

- **Gefunden in:** step-003 (find_duplicates) und step-004 (get_violations) Review.
- **Bezug:** `konzept.md` → Entdeckte Mängel/Redundanzen: „9 Checker mit `IsTestFile`-Skip-Pattern als Vorlage für FB-02/FB-03" — das Pattern existiert in den Lint-Checkern einheitlich, ist aber in den MCP-Tools fragmentiert.
- **Beschreibung:** `find_duplicates` testet via `PathNormalizer.IsTestFile(path) || document.Project.Name.EndsWith("Tests") || document.Project.Name.EndsWith(".TestKit")`. `get_violations` nutzt `CheckerContext.IsTestFile` (das intern `TestProjectDetector` + `TestSentinelConfig` nutzt). Die beiden Mechanismen sollten in eine `ITestDetector`-Schnittstelle mit einer Implementierung konsolidiert werden, damit neue Tools nicht selbst entscheiden müssen, wie sie „Test-Code" erkennen.
- **Aufwand:** ~1-2 Commits, mittel. Refactoring inkl. Tests-Anpassung.
- **Nicht-blockierend:** Aktueller Stand funktioniert für alle Anwendungsfälle (Step-003/Step-004-Tests grün); die Fragmentierung ist ästhetisch, nicht funktional.
