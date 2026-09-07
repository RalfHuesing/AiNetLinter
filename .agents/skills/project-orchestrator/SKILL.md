---
name: project-orchestrator
description: Orchestrate a complete bounded AiNetLinter task through explicit slices, fresh roles, read-only review, verification, and controlled commits.
---

# AiNetLinter-Orchestrator

Verwende diesen Skill, wenn der Nutzer eine Aufgabe ausdrücklich als
Orchestrator ausführen lassen oder Implementer, Reviewer beziehungsweise
Auditor einsetzen möchte. Der Standard ist der vollständige freigegebene Task,
nicht nur der erste Slice.

## Vor der Delegation

1. Lies `AGENTS.md`, die relevanten `.agents/rules/`, diesen Skill und die
   benötigten Dateien unter `.agents/roles/`.
2. Prüfe Working Tree, Branch, Diff und vorhandene Nutzeränderungen.
3. Definiere Task-Ziel, Scope, Ausschlüsse, Abschlussbedingung und nötige
   Verifikation. Erfinde keinen Taskpfad oder zusätzliche Anforderungen.
4. Wähle den nächsten fachlich zusammenhängenden Slice. Task-lokale Roadmaps
   oder Notizen sind optional und werden nur bei echtem Bedarf verwendet.

## Delegation

- Höchstens drei aktive Subagenten pro Slice.
- Schreibende Arbeit bleibt im gemeinsamen Working Tree sequentiell.
- Rollen delegieren niemals selbst weiter.
- Der Implementer ändert den Slice.
- Der Reviewer prüft read-only.
- Der Auditor wird bei größeren Tasks am Ende des Scopes eingesetzt.
- Jeder neue Korrekturversuch erhält einen frischen Implementer.

Für C#-Semantik lesen Implementer, Reviewer und Auditor
`.agents/rules/AiNetLinter-McpWorkflow.mdc` und verwenden aktuelle MCP-Schemas.
`rg` bleibt für Nicht-C#-Text, Konfiguration und exakte Diff-Arbeit erlaubt.

## Ablauf

1. Task- und Slice-Vertrag festlegen.
2. Implementer mit klaren Dateigrenzen und Akzeptanzkriterien starten.
3. Gezielte Tests, Build und erforderliche MCP-Prüfungen ausführen.
4. Reviewer unabhängig auf den tatsächlichen Diff ansetzen.
5. Findings mit höchstens zwei begrenzten Korrektur-/Reviewzyklen behandeln.
6. Bei einem größeren Task den Auditor ausführen und Befunde disponieren.
7. Die maßgeblichen Abschluss-Gates aus `AGENTS.md` nach der letzten
   Codeänderung ausführen.
8. `git diff --check` und `git status` prüfen und den fachlich abgeschlossenen
   Slice committen.
9. Nach einem erfolgreichen Slice den nächsten bereiten Slice bestimmen; erst
   bei erfüllter Task-Abschlussbedingung den Task beenden.

## AiNetLinter-Abschluss

Bei Produktions- oder Testcodeänderungen umfasst der Abschluss `dotnet build`
sowie die in `AGENTS.md` geforderten Nicht-Stress-Testläufe. Stress-Tests
werden nur auf ausdrückliche Anforderung ausgeführt. Bei reinen Markdown-,
Dokumentations- oder Agenteninfrastrukturänderungen genügen Referenzprüfungen,
`git diff --check` und die sachliche Diff-Prüfung. Rote relevante Checks,
offene P0/P1-Findings oder fehlende Nachweise werden nicht als Erfolg
ausgegeben.

Keine Step-Dateien, künstlichen Übergabearchive oder verpflichtenden
`execution-log.md`-/`tech-debt.md`-Dateien anlegen. Ein Task darf seine eigenen
Notizen verwenden, wenn Umfang oder Nutzerauftrag das rechtfertigt.
