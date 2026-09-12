# Task-Orchestrator Prompt

Kompakter Copy-Paste-Prompt für die autonome, serielle Umsetzung eines freigegebenen Tasks.
Einfach in den Chat kopieren und `<PFAD_ZU_KONZEPT.MD>` durch den Pfad zur freigegebenen `Konzept.md` ersetzen.

---

```text

Verwende **KEINE** Skills (.agents/skills/*).
Beachte AGENTS.md + .agents/rules/*.mdc und nutze den AiNetLinter MCP-Server proaktiv.

Du bist Orchestrator. Setze <PFAD_ZU_KONZEPT.MD> vollständig um:

1. Erstelle eine Roadmap.md im Task-Ordner (neben Konzept.md):
   - Oben: Die Roadmap-Checkliste mit [ ]-Checks.
   - Darunter: Ein token-effizientes Durchführungsprotokoll ohne Rauschen (pro Step nur 2–3 kurze Stichpunkte: Änderungen, Commit-Hash oder Grund, falls kein Commit).
2. Arbeite die Steps strikt seriell ab:
   - Pro Step genau EINEN frischen Subagenten starten und geduldig auf das Ende warten (nie parallel!).
   - Dem Subagenten nur das Nötige übergeben: Step-Ziel, betroffene Dateien, Akzeptanzkriterien, Regeln beachten, keine Skills.
   - Nach dem Step: Roadmap abhaken ([X]), kompakten Protokolleintrag ergänzen und commiten, wenn grün (deutsche Conventional Commits, sauberes Staging).
3. Abschluss:
   - Ein frischer Review/Audit-Subagent prüft den gesamten Scope und behebt Findings proaktiv.
   - Finales Gate laut AGENTS.md sicherstellen (FastTests & IntegrationTests ohne Stress, Build warnungsfrei).
   
```
