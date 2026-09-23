# Orchestrierte Umsetzung

Du führst **Schritt 3 von 3** aus. Du implementierst nicht. Du startest keinen anderen Workflow-Schritt.

## Start

Sinngemäß: `Führe 03-orchestrierte-umsetzung.md aus. Task: tasks/<name>`

Ohne Taskverzeichnis: nur danach fragen. Lies [README.md](README.md) in diesem Ordner, `AGENTS.md` und die Regeln des Repos, dann Konzept und Roadmap. Dieser Prompt ist die Commit-Freigabe für Leaf-Slices.

## Ablauf

1. Nimm den ersten offenen ausführbaren Punkt (Leaf-Datei, sonst die erste offene Checkbox ohne offene Kinder). Manuelle Gates ohne `-T` / ohne Implementierungsauftrag nicht an Agenten geben.
2. Starte **genau einen** Sub-Agenten. Warte auf das Ende. Keine parallelen Schreiber im selben Worktree.
3. Prüfe Diff und Checkboxen. Lücken oder unbelegte `[x]`: denselben Punkt nacharbeiten. Erst dann der nächste.
4. Nach jedem Milestone — ohne Schnitt: nach dem letzten fachlichen Punkt — einen Audit-Agenten, danach höchstens **einen** Korrektur-Implementierer.
5. Stoppen bei echter Blockade, offenem Fork (nicht erfinden) oder wenn die Roadmap durch ist. Nicht pushen, nicht amenden, Historie nicht umschreiben.

Resume: erste offene Checkbox. Kein extra Log, keine Tech-Debt-Datei, keine Code-Map.

## Auftrag an den Leaf-Agenten

- Genau dieser Punkt; Nachbarfeatures nicht mitnehmen. Kein Optional nachrüsten.
- Konzept, Punkt-Datei bzw. Roadmap-Ausschnitt, Projektregeln lesen.
- Ist-Stand prüfen. Plantext nicht gegen den Code durchsetzen.
- Sich am Ende selbst prüfen: eigene Checkboxen, Konzept (Nicht-Ziele, Invarianten), Anwendung (Code und die Tests, die das Repo für diesen Change verlangt).
- Nur abhaken, was geprüft ist. Parent-Checkboxen nur wenn alle Kinder und die Abnahme stimmen.
- Atomar committen: Slice samt Doku und Checkboxen. Conventional Commits wie das Repo es verlangt.

## Audit

Nur lesen, Feedback mit Fundstelle. Kein Produktionscode. Ergebnis darf in die Roadmap bzw. eine `audit.md` und die Audit-Checkbox.

Findings → genau **ein** weiterer Implementierer für diesen Milestone, dann weiter. Kein Loop. Ohne Findings keinen Korrektur-Agenten.
