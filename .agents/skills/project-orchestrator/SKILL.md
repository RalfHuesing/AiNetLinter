---
name: project-orchestrator
description: Orchestrate a complete bounded AiNetLinter task through explicit slices, fresh roles, read-only review, verification, and controlled commits.
---

# AiNetLinter-Orchestrator

Verwende diesen Skill, wenn der Nutzer eine Aufgabe ausdrücklich als
Orchestrator ausführen lassen oder Implementer, Reviewer beziehungsweise
Auditor einsetzen möchte. Der Standard ist der vollständige freigegebene Task,
nicht nur der erste Slice.

Nutzeranweisungen haben Vorrang vor allgemeinen Skill-Vorgaben. Der Skill darf
den Auftrag nicht ohne konkreten Risiko- oder Scopegrund stoppen oder erweitern.

## Vor der Delegation

1. Lies `AGENTS.md`, die relevanten `.agents/rules/`, diesen Skill und die
   benötigten Dateien unter `.agents/roles/`.
2. Prüfe Working Tree, Branch, Index, Diff und vorhandene Nutzeränderungen und
   halte `HEAD` sowie den Ausgangsstatus als Baseline fest.
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
- Bei geändertem Scope, anderer Ursache oder verlorenem Kontext erhält ein
  Korrekturversuch einen frischen Implementer; bei unverändertem Scope darf
  derselbe Agent fortgesetzt werden.

## Delegationsvertrag

Jeder Rollenauftrag nennt knapp:

- erwartetes Ergebnis und fachlichen Scope
- erlaubte und verbotene Pfade beziehungsweise Nebenwirkungen
- Akzeptanzkriterien und erforderliche Checks
- Stop-Bedingung und erwartetes Berichtsformat

Für C#-Semantik lesen Implementer, Reviewer und Auditor
`.agents/rules/AiNetLinter-McpWorkflow.mdc` und verwenden aktuelle MCP-Schemas.
`rg` bleibt für Nicht-C#-Text, Konfiguration und exakte Diff-Arbeit erlaubt.

## Testnachweise

Der Implementer führt die zum Slice passenden Prüfungen nach seiner letzten
Codeänderung aus und übergibt Befehl/Filter, Scope und Ergebnis. Der Reviewer
prüft diesen Nachweis gegen den tatsächlichen Diff. Ein frischer, erfolgreicher
und scope-passender Lauf wird von Folgeagenten wiederverwendet und nicht blind
wiederholt.

Ein Nachweis wird durch relevante Produktions-, Test-, Projekt- oder
Konfigurationsänderungen ungültig. Ebenfalls erneut prüfen bei unzureichendem
Scope, fehlgeschlagenem/abgeschnittenem/flaky Lauf oder konkreter
Gegenhypothese. Reine Dokumentationsänderungen entwerten Code-Testnachweise
nicht. Die vollständigen Abschluss-Gates laufen einmal nach der letzten
relevanten Codeänderung.

## Ablauf

1. Task- und Slice-Vertrag festlegen.
2. Implementer mit klaren Dateigrenzen und Akzeptanzkriterien starten.
3. Gezielte Tests, Build und erforderliche MCP-Prüfungen ausführen.
4. Reviewer unabhängig auf den tatsächlichen Diff ansetzen.
5. Findings mit höchstens zwei begrenzten Korrektur-/Reviewzyklen behandeln.
6. Bei einem größeren Task den Auditor ausführen und Befunde disponieren.
7. Die maßgeblichen Abschluss-Gates aus `AGENTS.md` nach der letzten
   Codeänderung ausführen.
8. `git diff --check` und `git status` prüfen. Stage ausschließlich die eigene,
   seit der Baseline entstandene Änderung über explizite Pfade oder Hunk-
   Auswahl und committe erst nach erneuter Prüfung von Index und Diff. Nutze
   niemals `git add .`, `git add -A` oder `git commit -a`; fremde staged oder
   nicht sicher trennbare parallele Änderungen blockieren den Auto-Commit.
   Wenn ein anderer schreibender Agent im selben Working Tree aktiv ist, darf
   der Commit nur mit einer exklusiven Staging-/Commit-Sperre erfolgen;
   andernfalls separaten Worktree verwenden oder den Commit zurückstellen.
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
