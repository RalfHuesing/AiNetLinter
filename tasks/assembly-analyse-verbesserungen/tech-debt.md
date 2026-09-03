# Tech-Debt-Register

Noch keine actionable Findings erfasst. Befunde aus Implementierung, Review und Audit werden hier mit Schweregrad, Evidenz, Disposition und Log-Anker ergänzt.

## P2 – Vier bestehende Budget-Parallelcluster

- Schweregrad: P2
- Beschreibung: Vier DRY-Cluster im Budget-Code könnten gemeinsame Logik teilen.
- Scope/Fundstelle: `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.Budget.cs`.
- Evidenz: DRY-Audit des Epic-1-Implementierers meldet vier bestehende Budget-Parallelcluster.
- Disposition: accepted-deferred
- Nächster Schritt: Im passenden späteren Audit-/Refactoring-Scope prüfen, ob ein verhaltensneutrales Zusammenführen möglich ist; keine Scope-Erweiterung des Epic-1-Vertrags.
- Attempts: 0
- Log-Anker: `execution-log.md`, Run 2026-09-03 / Epic 1 / Implementierer – Abschluss.

## Aktive P1-Findings – Korrekturrunde 1

Die folgenden fünf Ursachen werden in der unmittelbar folgenden frischen Implementierer-/Reviewer-Runde bearbeitet. Sie sind bis zur Bestätigung des Folge-Reviews nicht als `fixed` disponiert.

Implementierer Korrekturrunde 1 meldet alle fünf als behoben; Disposition bleibt bis zum Folge-Review `fix-now`, Attempts bleiben bei 1.

### P1 – BudgetProjection/Envelope

- Schweregrad: P1
- Beschreibung: Budget-Trim und kanonische Paging-/Legacy-Felder werden nicht gemeinsam neu berechnet.
- Scope/Fundstelle: `InspectAssemblyResponseBuilder.cs`, `FindAssemblyExtensionsResponseBuilder.cs`, `AssemblyAnalysisResponseLimits.Budget.cs`.
- Evidenz: Reviewer reproduziert übersprungene oder unerreichbare Ergebnisse bei kleinem `maxResponseBytes`.
- Disposition: fix-now (aktive Korrekturrunde)
- Nächster Schritt: Rückgabeoffset, Counts, Truncation und Continuation-Token nach der finalen Projektion gemeinsam ableiten; Regressionstest ergänzen.
- Attempts: 1
- Log-Anker: `execution-log.md`, Run 2026-09-03 / Epic 1 / Reviewer – Abschluss, Signatur `BudgetProjection/Envelope`.

### P1 – AssemblyWireBudget/Coverage

- Schweregrad: P1
- Beschreibung: Antwortbudget wird nicht als gemeinsames MCP-Wire-Budget über Text und Structured sowie alle Assembly-Routen durchgesetzt.
- Scope/Fundstelle: `AssemblyAnalysisResponse.cs`, `AssemblyAnalysisResponseLimits.Budget.cs`, Symbol-Body-/Symbolgraph-/Assembly-`get_file_tree`-Registrierungen, `InspectAssemblyFormatter.cs`.
- Evidenz: Reviewer weist getrennte Messung und redundante umfangreiche Textdaten nach.
- Disposition: fix-now (aktive Korrekturrunde)
- Nächster Schritt: zentralen, rückwärtskompatiblen Budgetpfad oder kurze Textzusammenfassung mit Structured als kanonischer Nutzlast umsetzen und messen.
- Attempts: 1
- Log-Anker: `execution-log.md`, Run 2026-09-03 / Epic 1 / Reviewer – Abschluss, Signatur `AssemblyWireBudget/Coverage`.

### P1 – CompositeBudget/Enrichment

- Schweregrad: P1
- Beschreibung: Composite-Budgetierung erfolgt vor dem finalen Envelope; entfernte Sektionen tragen keinen Status oder Fortsetzungshinweis.
- Scope/Fundstelle: `AssemblyAnalysisContextTool.cs`, `AnalysisToolCall.cs`.
- Evidenz: Reviewer beschreibt erneutes Überschreiten durch spätere Metadaten und transparente Sektionsverluste bei kleinem Budget.
- Disposition: fix-now (aktive Korrekturrunde)
- Nächster Schritt: finalen Envelope vor dem gemeinsamen Trim erzeugen und jeden optionalen Abschnitt mit Status/Truncation/Detailhinweis versehen; Regressionstest ergänzen.
- Attempts: 1
- Log-Anker: `execution-log.md`, Run 2026-09-03 / Epic 1 / Reviewer – Abschluss, Signatur `CompositeBudget/Enrichment`.

### P1 – CompositeTraversal/TopN

- Schweregrad: P1
- Beschreibung: Öffentlich akzeptierter `topN`-Parameter verändert die Caller-/Impact-Auswahl nicht.
- Scope/Fundstelle: `AssemblyAnalysisToolRegistrations.cs`, `AssemblyAnalysisContextTool.cs`.
- Evidenz: Reviewer reproduziert identische Auswahl für `topN=1` und `topN=100`.
- Disposition: fixed
- Nächster Schritt: `topN` semantisch anwenden oder aus dem Vertrag entfernen und Regressionstest ergänzen.
- Attempts: 1
- Log-Anker: `execution-log.md`, Run 2026-09-03 / Epic 1 / Folge-Reviewer 1 – Abschluss, Signatur `CompositeTraversal/TopN`.

### P1 – AssemblyProvenance/BodyMode

- Schweregrad: P1
- Beschreibung: Structured Body kann bei dekompilierten Assemblies `source` melden, obwohl der äußere Analysekontext `decompiledProject` ausweist.
- Scope/Fundstelle: `SourceSymbolBodyResolver.cs`, `AssemblyAnalysisContextFactory.cs`, `GetSymbolBodyTool.cs`.
- Evidenz: Reviewer weist widersprüchliche Herkunftsangaben im Fallback-Pfad nach.
- Disposition: fixed
- Nächster Schritt: Body-Herkunft aus dem Assembly-Kontext ableiten und Assembly-Body-Regressionstest ergänzen.
- Attempts: 1
- Log-Anker: `execution-log.md`, Run 2026-09-03 / Epic 1 / Folge-Reviewer 1 – Abschluss, Signatur `AssemblyProvenance/BodyMode`.

Folge-Reviewer 1 bestätigt `BudgetProjection/Envelope`, `AssemblyWireBudget/Coverage` und `CompositeBudget/Enrichment` weiterhin als P1 offen; `CompositeTraversal/TopN` und `AssemblyProvenance/BodyMode` sind `fixed`. Die drei offenen Einträge bleiben für Korrekturrunde 2 mit Attempts 1 aktiv.
