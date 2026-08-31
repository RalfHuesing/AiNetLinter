# Tech-Debt-Register: decompiled-assembly-test

Kuratierte Queue actionable Findings. Dispositionen: `fixed`, `accepted-deferred`,
`rejected/not-applicable`, `blocked/needs-user-decision`, `promoted-to-project-debt`.
Neue Einträge werden hinten angehängt; aktivierte Einträge erhalten 5 Korrekturversuche.

## Einträge

### TD-001 — Consumer-Applicability-Testabdeckung entfallen (P3, accepted-deferred)
- **Befund:** Der ersetzte rote Test `FindAssemblyExtensions_UsesConsumerCompilationForApplicability` war die einzige Abdeckung der `ReduceExtensionMethod`-Pfade (applicable/not_applicable via Consumer-Projektauflösung). Produktionslogik unverändert (Run-1), aber Testlücke.
- **Scope/Fundstelle:** `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolTests.cs` (ersetzter Test), Produktion `AssemblyAnalysisService`/`FindAssemblyExtensionsTool`.
- **Evidenz:** Run-2-Hand-off (Log-Anker: Run-2-Eintrag), MCP-verifiziert.
- **Disposition:** accepted-deferred (P3, blockiert keinen Abschluss).
- **Nächster Schritt:** Optional später ergänzen: `RoslynTestSolutionFactory` + Consumer-Solution + passende Probe (`receiverType="Probe.Extensions.Person"`), Assertion auf applicable/not_applicable-Zuordnung.

### TD-002 — find_duplicates-Cluster 9 in AnalysisTargetResolverTests (P3, accepted-deferred)
- **Befund:** Duplikat-Cluster (Score 0.85) zwischen zwei Tests, die bereits vor Run-2 existierten (Altbestand, außerhalb Änderungsbereich).
- **Scope/Fundstelle:** `src/AiNetLinter.FastTests/Mcp/AnalysisTargetResolverTests.cs`.
- **Evidenz:** MCP `find_duplicates` (Run-2-Hand-off).
- **Disposition:** accepted-deferred — wird im Abschluss-Audit des Tasks geprüft (dort scope-relevant, da geänderte Testdatei betroffen ist).
- **Nächster Schritt:** Im Abschluss-Audit bewerten (safe fix möglich: Test-Fixture zentralisieren) oder als Projekt-Debt promoten.