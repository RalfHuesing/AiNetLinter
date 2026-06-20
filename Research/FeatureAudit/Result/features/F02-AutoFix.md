# Auto-Fix (F02)

**Kategorie:** CLI-Feature  
**CLI-Flag / Konfiguration:** `--fix`, `--dry-run`  
**Status:** Vorhanden

---

## Bewertung

🟢 **WERTVOLL**

**Fazit:** Auto-Fix für triviale, strukturell eindeutige Verstöße (sealed hinzufügen, #nullable enable einfügen, PascalCase korrigieren) ist eine Kernfunktion jedes modernen Linters und besonders wertvoll für LLM-Agenten, die direkte Korrektur-Aktionen benötigen.

---

## Empfehlung

**Aktion:** Beibehalten und ggf. erweitern  
**Begründung:** Die aktuell abgedeckten Fix-Typen (sealed, nullable, PascalCase) sind die häufigsten trivialen Verstöße in einem LLM-generierten Code-Batch. Der `--dry-run`-Modus ist essenziell für Agenten, die Änderungen vor der Anwendung prüfen wollen.

---

## Nutzen-Analyse

Auto-Fix deckt drei explizit genannte Kategorien ab:

1. **`sealed` hinzufügen:** Rein mechanisch — wenn eine Klasse keine Unterklassen hat und kein Basis-Suffix trägt, ist das Hinzufügen von `sealed` sicher und idempotent.
2. **`#nullable enable` einfügen:** Einfügung am Dateianfang, falls nicht vorhanden — sicher und eindeutig.
3. **PascalCase korrigieren:** Umbenennung von öffentlichen Typen/Methoden/Properties — potenziell riskanter als die anderen beiden (Umbenennung kann Abhängigkeiten brechen, wenn externe Projekte nicht mitgeprüft werden).

**Szenarien wo es wertvoll ist:**
- Nachbearbeitung von LLM-generiertem Code: Ein Agent generiert Code, der strukturell korrekt aber linter-inkonform ist. `--fix` korrigiert triviale Verstöße ohne manuellen Eingriff.
- CI/CD-Pipeline: Auto-Fix vor dem Commit reduziert Review-Aufwand.
- Greenfield-Projekte: Zu Beginn eines Projekts einfach alle trivialen Regeln einzuhalten.

**Szenarien wo es irrelevant/riskant ist:**
- PascalCase-Fix in Projekten mit externen Abhängigkeiten (breaking change)
- Projekte mit komplexen Abhängigkeitsgraphen (sealed-Fix könnte Framework-Patterns brechen)

---

## Vergleich: Andere Tools

| Tool | Auto-Fix-Fähigkeit | Ansatz |
|------|-------------------|--------|
| **ESLint** | `--fix` | Breite Abdeckung; ~80% der Regeln haben Auto-Fix |
| **Prettier** | Vollständig | Ausschließlich Formatierung; keine semantischen Fixes |
| **Roslyn Code Fixes** | IDE-integriert | Via CodeFixProvider; der Standard in .NET-Ökosystem |
| **dotnet format** | `--fix-analyzers` | Integriert in .NET SDK; korrigiert Analyzer-Verstöße |
| **StyleCop** | Teilweise (via IDE) | Meist manuelle Korrekturen; kein eigenständiges --fix |
| **SonarLint** | Quick Fixes (IDE) | Nur interaktiv via IDE; kein CLI-Fix |
| **AiNetLinter** | `--fix` + `--dry-run` | CLI-basiert; auf wenige, sichere Fixes beschränkt |

AiNetLinters Ansatz ist pragmatisch und sicher: Nur eindeutig sichere Transformationen werden automatisiert. Im Vergleich zu Roslyn-basierten Alternativen (`dotnet format`) hat AiNetLinter den Vorteil, dass die Fixes auf die eigenen Regeln zugeschnitten sind.

**Lücke:** ESLint's `--fix` deckt ~80% aller Regeln ab; AiNetLinter deckt nur 3 von 26 aktiven Regeln per Auto-Fix ab. Hier besteht Erweiterungspotenzial (z.B. Auto-Fix für R02 AllowDynamic-Verstöße wäre technisch schwierig, für R07 ValueObject-Suffix jedoch einfach).

---

## KI-Agenten-Perspektive

Auto-Fix ist für LLM-Agenten besonders wertvoll — empirisch belegt durch die Erkenntnis, dass Agenten direktes, maschinelles Feedback benötigen:

1. **Geschlossener Feedback-Loop:** Ein Agent kann nach einer Code-Generierung `AiNetLinter --fix` ausführen, die trivialen Verstöße automatisch beheben und dann den Linter erneut ausführen, um zu prüfen ob noch nicht-automatisch-fixbare Verstöße verbleiben. Dieser Loop ist für autonome Agenten essenziell.

2. **Reduzierung kognitiver Last:** Agenten müssen bei trivialen Verstößen keine semantischen Entscheidungen treffen. Die Studie "Inside the Scaffold" (arXiv:2511.00872, 2024) zeigt, dass Agenten bei klaren, mechanischen Korrekturen deutlich effizienter sind als bei solchen, die semantisches Verständnis erfordern.

3. **`--dry-run` als Sicherheitsnetz:** Der `--dry-run`-Modus erlaubt dem Agenten, die geplanten Änderungen zu inspizieren, bevor er sie anwendet — analog zu `git diff` vor einem Commit. Das ist ein wichtiges Sicherheitsmerkmal in agentic Workflows.

---

## Zeitliche Einordnung

**Grundlagenstabilität:** Zeitlos

Der Bedarf nach automatischer Korrektur eindeutiger Code-Verstöße ist unabhängig von Modellgenerationen. Sogar sehr leistungsfähige Modelle werden gelegentlich triviale Regeln vergessen (sealed, nullable). Der mechanische Fix bleibt sinnvoll.

---

## Quellen

- arXiv:2511.00872 (2024) — Empirical Agent Framework Studies: Inside the Scaffold — Agent Failure Taxonomy
- arXiv:2601.20404 (2025) — On the Impact of AGENTS.md Files on the Efficiency of AI Coding Agents
- ESLint Documentation (2024) — Fixable Rules: https://eslint.org/docs/latest/use/command-line-interface#--fix
- dotnet format Documentation (2024): https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-format
