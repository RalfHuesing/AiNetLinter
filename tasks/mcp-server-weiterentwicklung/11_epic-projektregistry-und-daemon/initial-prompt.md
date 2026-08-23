# Persistierter Initialauftrag

Dieser Auftragsanker bewahrt den Nutzerauftrag für spätere Fortsetzungen nach
Kontextkompaktierung. Bei einer Wiederaufnahme zuerst diese Datei, danach
`task-state.md`, `roadmap.md` und den aktuellen Step-Plan lesen. Das frühere
Übergabe-Dokument ist absichtlich kein Bestandteil dieses Auftragsankers.

## Auftrag

Beachte die Regeln in `.agents/rules/*`.

Deine Rolle: `.agents/Agent-Scaffolding/dev-loop/drift-loop/orchestrator.md`.

Setze diesen Task um:
`tasks/mcp-server-weiterentwicklung/11_epic-projektregistry-und-daemon`

Der vollständige Task ist umzusetzen: EPIC-A vollständig abschließen und
anschließend auch EPIC-B vollständig planen, implementieren, verifizieren und
dokumentieren. Nicht nach EPIC-A oder an einem Zwischen-Check-in stoppen.

## Effizienzvorgaben

Diese Vorgaben sind in JEDE Subagenten-Prompt zu übernehmen:

1. Große Steps: Der Planer baut wenige, große Steps (Ziel: Epic A in 3–5
   Steps, Epic B in 3–5). Doku-/Sync-Pflichten landen in dem Step, der sie
   fachlich berührt — keine eigenen Mini-Doku-Steps.
2. Tests wirtschaftlich: Coder entwickelt mit gefilterten Läufen
   (`Category=Unit` bzw. gezielte Filter); den kompletten Nicht-Stress-Stack
   gibt es EINMAL pro Step vor Abschluss. Der Kritiker prüft
   Verträge/Qualität anhand von `step-result.md` plus Stichproben und
   wiederholt NICHT den kompletten Testlauf.
3. Kein Overhead bei Kleinem: Eindeutige Korrektur-Findings laufen über den
   mechanischen Transkript-Pfad ohne Planer-Aufruf. Tech-Debt nur
   dokumentieren, nicht selbst beheben. drift-audit
   (`find_duplicates`/`find_magic_values`/`find_dead_code`) einmal pro Epic,
   nicht pro Step.
4. Nutzt durchgehend den AiNetLinter-MCP-Server (`find_symbol`, `get_impact`,
   `get_violations`, …) statt grep/Volltext-Lesen — Quality-Gates vor jedem
   Commit.

Die Agenten nutzen dasselbe Modell wie der Orchestrator: Vertraue auf ihre
Ergebnisse und kontrolliere über die Verträge im Konzept, nicht durch
Wiederholung. Qualitätsstandards (`TreatWarningsAsErrors`, DoD je Epic,
Testkatalog) bleiben unverändert.

Arbeite autonom weiter bis zum nächsten `blocked`- oder Check-in-Punkt.

## Erweiterte Korrekturfreigabe

Der Nutzer hebt die Verdict-/Workflow-Schranken für diesen Task bewusst auf
das Doppelte an. Die lokalen Task-Overrides sind daher:

- `max_fix_rounds_per_step`: 3 → 6
- `soft_step_checkin_interval`: 40 → 80
- `max_batch_items`: 8 → 16
- `max_batch_diff_lines`: 40 → 80

Bei weiteren `issues`-Verdicts die Korrektursteps bis zum neuen Kettenlimit
normal fortsetzen und nicht vorzeitig wegen der ursprünglichen Limits
unterbrechen. Die globalen Defaults in den Workflow-Spezifikationen bleiben
unverändert; nur dieser Task nutzt die verdoppelten Overrides.

## Fortsetzungsregeln

- Keine Historienmanipulation und kein Push.
- Fremde offene Änderungen unberührt lassen; nur eigene Step-Dateien gezielt
  staggen, niemals `git add -A`.
- Bei Kontextverlust den aktuellen Stand aus `task-state.md`, `roadmap.md`,
  `codemap.md`, dem letzten `step-result.md`/`step-review.md` und diesem
  Initialauftrag rekonstruieren.
- Der Scope endet erst, wenn EPIC-A und anschließend EPIC-B gemäß ihrer
  jeweiligen Definition of Done abgeschlossen und reviewed sind.
- Vor jedem Commit die MCP-Quality-Gates ausführen; Stress-Tests nur auf
  ausdrückliche Anforderung.
