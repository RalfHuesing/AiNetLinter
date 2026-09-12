# Follow-up-Roadmap: MCP-Payload-Verschlankung

Stand: 2026-09-12
Baseline: `3e3c97b` (Release 1.0.195)

Jeder Schritt wird strikt seriell durch einen frischen Agenten umgesetzt, nach den passenden Gates geprüft und mit einem ausschließlich auftragsbezogenen deutschen Conventional Commit abgeschlossen.

- [X] 00 – Findings F-01 bis F-07 und Safeguard-Baseline gegen den aktuellen Quellstand validieren.
  - Evidenz: `safeguard(minScore: 10)` meldet 7,10/10 mit genau `StaticTestSentinel` für `DecompiledProjectPaths` und `MaxMethodParameterCount` für `McpNavigationProjection.Create`.
  - Evidenz: MCP-Suche belegt die sechs internen JSON-Responsebudget-Berechnungen (F-01); Budgetfehler führen in den betroffenen Pfaden keinen deterministischen `minimumResponseBytes`-Retry mit (F-02).
  - Evidenz: `AssemblyAnalysisContextTool` serialisiert und liest die Composite-Daten über `JsonObject` zurück (F-03); die Structured-Navigationsüberladung (F-04), `McpErrorPayload` (F-05) und drei JSON-Usings (F-06) sind ungenutzt; Konzeptfrontmatter bleibt `status: ready` (F-07).

- [ ] 01 – F-01 und F-02 beheben: alle sechs Responsebudget-Projektionen auf die UTF-8-Größe des finalen Content-Texts umstellen sowie für alle sieben Budget-Recovery-Pfade `RESPONSE_BUDGET_TOO_SMALL` mit einem deterministischen `minimumResponseBytes` liefern. Verhalten mit fokussierten FastTests absichern und committen.

- [ ] 02 – F-03 beheben: `AssemblyAnalysisContextTool` auf eine typisierte Composite-Aggregation ohne `JsonObject`-Serialisieren/Zurücklesen umstellen. Regressionsschutz auf FastTest-Ebene ergänzen, passende Gates ausführen und committen.

- [ ] 03 – F-04 bis F-07 bereinigen: ungenutzte Structured-Navigationspfade, `McpErrorPayload` und JSON-Usings entfernen; nach Abschluss der Umsetzung das Konzeptfrontmatter auf `status: completed` setzen. Tests bzw. Dokumentationsprüfungen passend ausführen und committen.

- [ ] 04 – Safeguard auf 10,0/10 bringen: Testevidenz für `DecompiledProjectPaths` ergänzen und die sechs Parameter von `McpNavigationProjection.Create` in ein Parameter-Record überführen. Betroffene Tests und vollständige Non-Stress-Gates ausführen, dann committen.

- [ ] 05 – Abschlussprüfung: `dotnet build`, beide Non-Stress-Testprojekte, `safeguard(minScore: 10)`, scoped `get_violations`, `find_dead_code`, `find_magic_values` sowie `git diff --check` prüfen; keine offenen auftragsbezogenen Änderungen hinterlassen.

## Ausklammerungen

Keine. Alle Befunde F-01 bis F-07 sind im aktuellen Stand bestätigt; `Findings.md` selbst bleibt als ungetrackte Nutzerdatei unverändert und wird nie gestaged.
