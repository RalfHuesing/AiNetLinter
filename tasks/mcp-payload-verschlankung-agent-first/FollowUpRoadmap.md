# Follow-up-Roadmap: MCP-Payload-Verschlankung

Stand: 2026-09-12
Baseline: `3e3c97b` (Release 1.0.195)

Jeder Schritt wird strikt seriell durch einen frischen Agenten umgesetzt, nach den passenden Gates geprüft und mit einem ausschließlich auftragsbezogenen deutschen Conventional Commit abgeschlossen.

- [X] 00 – Findings F-01 bis F-07 und Safeguard-Baseline gegen den aktuellen Quellstand validieren.
  - Evidenz: `safeguard(minScore: 10)` meldet 7,10/10 mit genau `StaticTestSentinel` für `DecompiledProjectPaths` und `MaxMethodParameterCount` für `McpNavigationProjection.Create`.
  - Evidenz: MCP-Suche belegt die sechs internen JSON-Responsebudget-Berechnungen (F-01); Budgetfehler führen in den betroffenen Pfaden keinen deterministischen `minimumResponseBytes`-Retry mit (F-02).
  - Evidenz: `AssemblyAnalysisContextTool` serialisiert und liest die Composite-Daten über `JsonObject` zurück (F-03); die Structured-Navigationsüberladung (F-04), `McpErrorPayload` (F-05) und drei JSON-Usings (F-06) sind ungenutzt; Konzeptfrontmatter bleibt `status: ready` (F-07).

- [X] 01 – F-01 und F-02 beheben: alle sechs Responsebudget-Projektionen auf die UTF-8-Größe des finalen Content-Texts umstellen sowie für alle sieben Budget-Recovery-Pfade `RESPONSE_BUDGET_TOO_SMALL` mit einem deterministischen `minimumResponseBytes` liefern. Verhalten mit fokussierten FastTests absichern und committen.
  - Evidenz: Die finalen Text-Projektionen zählen UTF-8-Bytes, und die sieben Recovery-Pfade liefern bei zu kleinem Budget deterministisch `minimumResponseBytes`; fokussierte FastTests sichern den Vertrag ab.
  - Gates: `dotnet build` grün (0 Warnungen/Fehler); Fast Non-Stress 2506/2506 grün; Integration Non-Stress 216/216 grün. Der isolierte `get_index_scope`-Smoke-Test ist nach 33,9 s grün; eine erste Suite-Ausführung scheiterte einmalig in einem fremden Daemon-Contract, dessen isolierte Wiederholung grün war, und die vollständige Wiederholung grün durchlief.
  - MCP-Quality: Die F-01/F-02-Änderungen verursachen keine neuen Violations. `safeguard(minScore: 10)` bleibt erwartungsgemäß bei 7,10/10 wegen der in Schritt 04 geplanten Befunde `DecompiledProjectPaths` und `McpNavigationProjection.Create`; `get_violations` zeigt exakt diese zwei Warnungen. Dead-Code- und Magic-Value-Scans liefern nur bestehende heuristische Kandidaten außerhalb dieses Slices.

- [X] 02 – F-03 beheben: `AssemblyAnalysisContextTool` auf eine typisierte Composite-Aggregation ohne `JsonObject`-Serialisieren/Zurücklesen umstellen. Regressionsschutz auf FastTest-Ebene ergänzen, passende Gates ausführen und committen.
  - Evidenz: `AssemblyAnalysisContextTextModel` übernimmt Zähler, Scope, Vollständigkeit, Symbol, Continuation und Abschnitte direkt aus dem typisierten Inspection-Payload; `JsonObject`, `JsonNode` und `JsonSerializer` sind aus dem Composite entfernt. Der FastTest sichert Text, Handoff und Continuation; die Integrationstests sichern Content-only und den Body-/Handoff-Pfad.
  - Gates: Build grün (0 Warnungen/Fehler); Fast Non-Stress 2507/2507 grün; Integration Non-Stress 216/216 grün (TRX `TestResults/FollowUpF03-Retry.trx`). Ein zuvor abgebrochener Lauf hinterließ zwei Testprozesse; der TRX-Retry nach deren gezieltem Beenden ist vollständig grün.
  - MCP-Quality (Production-File-Scope): `safeguard(minScore: 10)` 10,00/10, `get_violations` 0, `find_dead_code` 0 Kandidaten und `find_magic_values` 0 Kandidaten.

- [ ] 03 – F-04 bis F-07 bereinigen: ungenutzte Structured-Navigationspfade, `McpErrorPayload` und JSON-Usings entfernen; nach Abschluss der Umsetzung das Konzeptfrontmatter auf `status: completed` setzen. Tests bzw. Dokumentationsprüfungen passend ausführen und committen.

- [ ] 04 – Safeguard auf 10,0/10 bringen: Testevidenz für `DecompiledProjectPaths` ergänzen und die sechs Parameter von `McpNavigationProjection.Create` in ein Parameter-Record überführen. Betroffene Tests und vollständige Non-Stress-Gates ausführen, dann committen.

- [ ] 05 – Abschlussprüfung: `dotnet build`, beide Non-Stress-Testprojekte, `safeguard(minScore: 10)`, scoped `get_violations`, `find_dead_code`, `find_magic_values` sowie `git diff --check` prüfen; keine offenen auftragsbezogenen Änderungen hinterlassen.

## Ausklammerungen

Keine. Alle Befunde F-01 bis F-07 sind im aktuellen Stand bestätigt; `Findings.md` selbst bleibt als ungetrackte Nutzerdatei unverändert und wird nie gestaged.
