# EnablePerformanceProfiling (F09)

**Kategorie:** Konfigurationsfeature  
**CLI-Flag / Konfiguration:** `rules.json → Global.EnablePerformanceProfiling: true`  
**Status:** Aktiv

---

## Bewertung

🟡 **UNPRAKTIKABEL**

**Fazit:** Performance-Profiling des Linter-Laufs selbst ist ein nützliches Entwicklerwerkzeug für die Linter-Implementierung, hat aber keinen direkten Nutzen für End-Nutzer oder LLM-Agenten — und als Default-aktives Feature erzeugt es dauerhaft `measurements/`-Dateien im Projektverzeichnis, was den Build-Artefakt-Bereich unnötig befüllt.

---

## Empfehlung

**Aktion:** Deaktivieren (Default auf `false` setzen)  
**Begründung:** Performance-Profiling ist eine Debugging-Funktion für AiNetLinter-Entwickler, keine Feature für End-Nutzer. Es sollte nur bei konkretem Bedarf aktiviert werden (z.B. wenn ein Nutzer über langsame Lint-Läufe berichtet). Die permanente Aktivierung erzeugt unnötigen Noise im Projektverzeichnis.

---

## Nutzen-Analyse

EnablePerformanceProfiling schreibt Messungen pro Lint-Lauf in ein `measurements/`-Verzeichnis:

**Wertvoll für:**
- **AiNetLinter-Entwickler:** Um Bottlenecks in der Linter-Implementierung zu identifizieren — welche Regeln sind besonders langsam?
- **Diagnose-Szenarios:** Wenn ein Nutzer meldet, dass ein bestimmtes Projekt extrem lang zum Lint benötigt.

**Nicht wertvoll für:**
- **End-Nutzer im regulären Betrieb:** Sie haben kein Interesse an den Performance-Metriken des Linters selbst. Die `measurements/`-Dateien sind Lärm.
- **LLM-Agenten:** Ein Agent der AiNetLinter als Qualitäts-Feedback-Tool nutzt, braucht keine Linter-Performance-Daten. Er benötigt Regel-Verstöße, keine Timing-Daten.
- **CI/CD-Pipelines:** Performance-Messungen haben in einem regulären CI-Lauf keinen Platz.

**Problem des aktuellen Status (aktiv):**
- Jeder Lint-Lauf schreibt Dateien nach `measurements/` — diese müssen in `.gitignore` sein, sonst verschmutzen sie das Repository.
- Bei LLM-Agenten die AiNetLinter wiederholt aufrufen (Korrektur-Loop), akkumulieren sich diese Dateien.

---

## Vergleich: Andere Tools

| Tool | Performance-Profiling | Ansatz |
|------|----------------------|--------|
| **ESLint** | `--print-execution-time` | Opt-in Flag; kein persistenter Output |
| **Roslyn Analyzers** | Visual Studio Diagnostics | IDE-integriert; kein CLI-Persistenz |
| **NDepend** | Analyse-Report | Integriert in Report-System; nicht separat persistiert |
| **SonarQube** | Server-Logs | Backend-Side; für Nutzer nicht sichtbar |
| **AiNetLinter** | `measurements/`-Verzeichnis | Immer-an (wenn aktiviert); persistente Dateien |

ESLints Ansatz (Opt-in Flag, kein persistenter Output) ist deutlich besser geeignet für ein CLI-Tool: Performance-Timing wird auf dem Terminal ausgegeben, wenn der Nutzer es anfordert (`--print-execution-time`). Kein dauerhafter Datei-Output.

**Empfehlung:** AiNetLinter könnte von ESLints Ansatz lernen — Performance-Profiling als `--profile`-Flag statt als persistente Konfiguration mit Datei-Output.

---

## KI-Agenten-Perspektive

EnablePerformanceProfiling ist für LLM-Agenten neutral bis negativ:

1. **Nicht nützlich:** Agenten benötigen Regel-Verstöße als Feedback, keine Linter-Performance-Daten. Die `measurements/`-Dateien sind irrelevant für den agentic Workflow.

2. **Potenziell störend:** Wenn ein Agent eine Tool-Call-Schleife ausführt (AiNetLinter mehrfach aufruft), akkumulieren sich Messungs-Dateien. Das könnte zu unerwarteten Dateiänderungen führen, die der Agent als "untracked changes" interpretiert.

3. **Rauschen im Kontext:** Wenn ein Agent das Projektverzeichnis scannt (z.B. um Dateien zu verstehen), findet er `measurements/`-Dateien die keine relevante Information über den Anwendungscode enthalten.

---

## Zeitliche Einordnung

**Grundlagenstabilität:** Zeitlos

Performance-Profiling-Bedarf für Tool-Entwicklung bleibt dauerhaft bestehen. Die Frage ist nur, ob es als Default-an oder Default-aus konfiguriert sein sollte — die Antwort ist klar: Default-aus.

---

## Quellen

- ESLint Documentation (2024) — Performance: https://eslint.org/docs/latest/extend/custom-rules#performance-testing
- .NET Diagnostics und Performance-Tools (2024): https://learn.microsoft.com/en-us/dotnet/core/diagnostics/
- (Ableitung, kein direktes Paper) — Prinzip der minimalen Seiteneffekte: Ein Tool sollte standardmäßig keine persistenten Artefakte erzeugen, die nicht Teil seiner primären Ausgabe sind.
