## Primäre Einstiegspunkte

- Assembly-Analyse-MCP und bestehende Assembly-Session-/Source-Provider-Komponenten; konkrete Symbole werden durch die Implementierer per MCP verifiziert.

## Betroffene Dateien und Symbole

- Noch nicht verifiziert; erwartete Bereiche sind Assembly-Analyse, Source-/Cache-Lifecycle, Antwortmodelle und MCP-Registrierungen.

## Aufrufer und Abhängigkeiten

- Noch nicht verifiziert; relevante Beziehungen werden im MCP-first-Kontext des jeweils aktiven Epics ergänzt.

## Relevante Tests, Konfiguration und Dokumentation

- Konzept-Vertrag: `tasks/assembly-analyse-verbesserungen/Konzept.md`.
- Abschluss-Gates: `src/AiNetLinter.FastTests`, `src/AiNetLinter.IntegrationTests`, `dotnet build`.
- Erwartete Dokumentation: `Docs/integration.md` und fachlich betroffene MCP-Verträge.

## Invarianten, Risiken und Unsicherheiten

- Externe Assemblies und Repositories bleiben read-only.
- Source darf nur bei verifiziertem Mapping/Checkout als Originalquelle ausgewiesen werden.
- Mehrdaemon-/Windows-Cleanup-Semantik und bestehende Wire-Kompatibilität sind zu verifizieren.

## Verifikation

- Noch nicht ausgeführt; jede Rolle aktualisiert diesen Abschnitt mit konkreten Checks und Scope.

