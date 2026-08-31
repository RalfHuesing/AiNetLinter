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
- Disposition: `fix-now`
- Risiko: vier aktive Produktionsregelverletzungen verhindern die Freigabe des
  EPIC-B-Stands, auch ohne gemeldete Laufzeitregression.
- Nächster Schritt: Formatter-Verantwortung in sichere Hilfsfunktionen
  schneiden, danach Tests, Impact und Violations erneut prüfen.
- Log-Anker: `execution-log.md`, completed EPIC-B Implementierer vom
  2026-08-31.

## TD-EPIC-B-003 — Health-Statusprojektion bei Detaildiagnostics

- Schweregrad: P1
- Beschreibung: Gezielt angeforderte transitive Diagnostics werden ergänzt,
  ohne den Rohstatus über `ResolveEffectiveStatus` zu `partial` zu projizieren.
- Fundstelle/Scope: `GetServerHealthResponseBuilder.cs`, gezielter Health-
  Detailpfad.
- Evidenz: unabhängiger Review belegte `completeness=complete` trotz
  vorhandener Diagnostics.
- Disposition: `fix-now`
- Risiko: strukturierte Health-Antwort kann Vollständigkeit falsch behaupten.
- Nächster Schritt: zentrale Statusprojektion auch auf den Detailpfad anwenden
  und mit Root-/transitiven Diagnostics testen.
- Log-Anker: `execution-log.md`, completed EPIC-B Reviewer vom 2026-08-31.

## TD-EPIC-B-004 — Compact-Health `ShownCount`

- Schweregrad: P1
- Beschreibung: Der kompakte Health-Modus leert Samples, übernimmt aber ihren
  alten `ShownCount`; StructuredContent und Textdarstellung widersprechen sich.
- Fundstelle/Scope: `AssemblyAnalysisResponseLimits.cs`, Compact-Health-Projektion.
- Evidenz: unabhängiger Review belegte leere Samples mit Textdarstellung wie
  `4 von 4`.
- Disposition: `fix-now`
- Risiko: maschinenlesbare und textuelle Antwortverträge sind inkonsistent.
- Nächster Schritt: `ShownCount` an tatsächlich ausgegebene Samples koppeln und
  Response-/E2E-Regression ergänzen.
- Log-Anker: `execution-log.md`, completed EPIC-B Reviewer vom 2026-08-31.

## TD-EPIC-B-005 — Globales Diagnostics-Sample-Budget

- Schweregrad: P1
- Beschreibung: Root-/transitive-/Aggregate-Samples werden separat begrenzt
  und anschließend erneut dupliziert; Deduplizierung gilt nur für Counts.
- Fundstelle/Scope: `AssemblyAnalysisResponseLimits.ProjectDiagnostics` und
  Diagnostics-Antwortmodell.
- Evidenz: unabhängiger Review belegte mögliche `ShownCount > TotalCount` und
  fehlendes globales 4-KiB-Budget.
- Disposition: `fix-now`
- Risiko: Antwortgrößen- und Count-Vertrag kann unter Diagnostics-Last verletzt
  werden.
- Nächster Schritt: gemeinsame deduplizierte Sample-Projektion mit globalem
  Bytebudget implementieren und mit Größen-/Truncationstests absichern.
- Log-Anker: `execution-log.md`, completed EPIC-B Reviewer vom 2026-08-31.

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
