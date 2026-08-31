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
- Disposition: `fixed`
- Risiko: behoben; `src/AiNetLinter/Core` liegt wieder beim aktiven Grenzwert
  von 30 direkten Einträgen.
- Nächster Schritt: keine weitere Maßnahme für diesen Befund; die korrigierte
  Dateiorganisation im Folge-Review verifizieren.
- Log-Anker: `execution-log.md`, completed EPIC-A Korrektur-
  Implementierer Runde 1 vom 2026-08-31.

## TD-EPIC-B-001 — Formatter-Komplexität

- Schweregrad: P2/P3
- Beschreibung: Vier neue Komplexitätsbefunde betreffen
  `FindAssemblyExtensionsTool.FormatText` und
  `GetServerHealthResponseBuilder.AppendAssemblySection`.
- Fundstelle/Scope: `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/` und
  `src/AiNetLinter/Mcp/Tools/ServerMaintenance/`.
- Evidenz: letzter `get_violations`-Check nach der letzten Codeänderung
  meldete insgesamt fünf Produktionsbefunde; vier davon sind neue
  Komplexitätsbefunde in diesen Formatierern.
- Disposition: `promoted-to-project-debt`
- Risiko: begrenzte Wartbarkeits-/Footprint-Überschreitung ohne gemeldete
  Funktionsregression; nicht nach dem letzten Violations-Check verändert.
- Nächster Schritt: Formatter-Verantwortung bei einer eigenständigen
  Folgeänderung in sichere Hilfsfunktionen schneiden und danach Tests,
  Impact und Violations erneut prüfen.
- Log-Anker: `execution-log.md`, completed EPIC-B Implementierer vom
  2026-08-31.

## TD-EPIC-B-002 — `AssemblyAnalysisRegistry` Footprint

- Schweregrad: P2/P3
- Beschreibung: Bestehender AIContext-Footprint-Befund in
  `AssemblyAnalysisRegistry` bleibt im Abschlusscheck sichtbar.
- Fundstelle/Scope: `src/AiNetLinter/Mcp/Assemblies/`.
- Evidenz: finaler `get_violations`-Check meldete einen bestehenden
  `AssemblyAnalysisRegistry`-Befund; keine Codeänderung zur Vermeidung einer
  scopefremden Architekturzerlegung.
- Disposition: `promoted-to-project-debt`
- Risiko: begrenzter Footprint-/Strukturbefund ohne neue EPIC-B-Funktionalität.
- Nächster Schritt: Nur bei nachgewiesener unabhängiger Verantwortung zerlegen;
  danach Safeguard, Impact und Violations erneut prüfen.
- Log-Anker: `execution-log.md`, completed EPIC-B Implementierer vom
  2026-08-31.
