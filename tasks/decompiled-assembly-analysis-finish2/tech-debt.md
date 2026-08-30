# Tech-Debt-Register: decompiled-assembly-analysis-finish2

Die EPIC-A-Implementierung meldete bislang keine neuen actionable P2/P3-
Befunde. Die zwei offenen Regressionen (Bare-Path-Snapshot und fehlende
Stable-Member-ID) sind Muss-Kriterien und bleiben als offene Korrekturbefunde
im `execution-log.md`; sie werden nicht als `accepted-deferred` verschleiert.

Historische Übergaben und neue actionable P2/P3-Befunde werden nach dem
jeweiligen Rollenbericht mit Evidenz, Disposition und Log-Anker ergänzt;
unbelegte oder rein kosmetische Vorschläge bleiben ausschließlich im
Ausführungsprotokoll.

## TD-EPIC-A-001 — `MaxDirectoryChildren` im Core-Scope

- Schweregrad: P1
- Beschreibung: Der neue `SolutionDocumentPathResolver` erhöht die Zahl der
  Einträge im betroffenen Core-Verzeichnis auf 31 und löst damit die
  `MaxDirectoryChildren`-Strukturregel aus.
- Fundstelle/Scope: `src/AiNetLinter/Core/`; neuer Resolver neben den bereits
  vorhandenen Core-Dateien.
- Evidenz: letzter gezielter `get_violations`-Check nach der letzten
  Codeänderung meldete genau 1 Befund; Testscope meldete 0 Violations.
- Disposition: `fix-now`
- Risiko: konkreter aktiver Produktions-Regelverstoß; EPIC-A ist bis zur
  Bereinigung nicht freigabefähig, auch wenn kein fachliches Laufzeitrisiko
  beobachtet wurde.
- Nächster Schritt: Resolver in einen fachlich geeigneten Unterordner
  gruppieren oder eine gleichwertige scope-nahe Korrektur vornehmen und danach
  Impact, `get_violations` und betroffene Tests erneut prüfen.
- Log-Anker: `execution-log.md`, completed EPIC-A Implementierer-
  Fortsetzung vom 2026-08-31.
