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

- [X] 03 – F-04 bis F-07 bereinigen: ungenutzte Structured-Navigationspfade, `McpErrorPayload` und JSON-Usings entfernen; nach Abschluss der Umsetzung das Konzeptfrontmatter auf `status: completed` setzen. Tests bzw. Dokumentationsprüfungen passend ausführen und committen.
  - Evidenz: `McpNavigationText.Format` besitzt nur noch die Content-Navigation ohne `JsonElement`-Überladung oder JSON-Fallbackpfade; die existierende Content-Semantik für `empty` wird ausschließlich aus `McpNavigationPayload.Scope` bestimmt. `McpErrorPayload` hatte keine Referenzen und ist entfernt; aus `FindSymbolResponseBudget` sind die drei ungenutzten `System.Text.Json`-Usings entfernt. Das Konzeptfrontmatter trägt `status: completed`.
  - Gates: Build grün (0 Warnungen/Fehler); Fast Non-Stress 2507/2507 grün; Integration Non-Stress 216/216 grün (TRX `TestResults/FollowUpF04F07.trx`). Ein initialer Testprozess lief nach dem Tool-Zeitfenster weiter und sperrte Ausgabedateien; der anschließende einzelne No-Build-TRX-Lauf beendete sich vollständig grün.
  - MCP-Quality: Für jede geänderte Produktionsdatei meldet `get_violations` 0 Verstöße. `find_dead_code` und `find_magic_values` für `McpNavigationText.cs` liefern jeweils 0 Kandidaten. `safeguard(minScore: 10)` bleibt bei 7,00/10 und zeigt ausschließlich die für Schritt 04 geplanten Befunde `DecompiledProjectPaths` und `McpNavigationProjection.Create` (scoreIsNotScope=true).

- [X] 04 – Safeguard auf 10,0/10 bringen: Testevidenz für `DecompiledProjectPaths` ergänzen und die sechs Parameter von `McpNavigationProjection.Create` in ein Parameter-Record überführen. Betroffene Tests und vollständige Non-Stress-Gates ausführen, dann committen.
  - Evidenz: `DecompiledProjectPathsTests` belegt die Ableitung von Projektverzeichnis, Projektdatei und gemeinsamem Source-Root aus realistischen dekompilierten Dokumentpfaden. `McpNavigationProjectionParameters` ist ein eigener unveränderlicher Namespace-Record; die Projektion und beide Resource-Aufrufer verwenden ihn, während der Verhaltenstest Status, Vollständigkeit, Code, Hint und Target-Pfad absichert.
  - Red/Green: Der initiale fokussierte Test war rot, weil der Parameter-Record noch nicht existierte; beide fokussierten FastTests sind nach der Migration grün.
  - Gates: Build grün (0 Warnungen/Fehler); Fast Non-Stress 2509/2509 grün; Integration Non-Stress 216/216 grün (TRX `TestResults/FollowUpStep04Integration.trx`).
  - MCP-Quality: Globales `safeguard(minScore: 10)` 10,00/10 mit 0 Verstößen; globales `get_violations` 0. `find_dead_code` im Registration-Scope 0 Kandidaten und `find_magic_values` im präzisen `McpNavigationProjection`-Scope 0 Kandidaten. `git diff --check` grün.

- [X] 05 – Abschlussprüfung: `dotnet build`, beide Non-Stress-Testprojekte, `safeguard(minScore: 10)`, scoped `get_violations`, `find_dead_code`, `find_magic_values` sowie `git diff --check` prüfen; keine offenen auftragsbezogenen Änderungen hinterlassen.
  - Vertragscheck F-01 bis F-07: Die sechs betroffenen Budgetprojektionen rechnen ausschließlich die UTF-8-Größe des finalen Textes; alle sieben Budget-Fehler liefern `RESPONSE_BUDGET_TOO_SMALL` mit `minimumResponseBytes`. Der Assembly-Composite enthält keine `JsonObject`-Reprojektion, Navigation keinen Structured-Fallback, `McpErrorPayload` und die F-06-JSON-Usings fehlen, und das Konzept steht auf `status: completed`.
  - Gates: Build grün (0 Warnungen/Fehler); Fast Non-Stress 2509/2509 grün; Integration Non-Stress 216/216 grün (TRX `TestResults/FollowUpStep05Integration.trx`); `git diff --check` grün.
  - MCP-Quality: globales `safeguard(minScore: 10)` 10,00/10 mit 0 Verstößen; `get_violations` global 0 und im Scope `src/AiNetLinter/Mcp` 0. Die scoped Dead-Code- und Magic-Value-Scans lieferten ausschließlich heuristische Bestandskandidaten, aber keinen belastbaren auftragsbezogenen Befund.

## Ausklammerungen

Keine. Alle Befunde F-01 bis F-07 sind im aktuellen Stand bestätigt; `Findings.md` selbst bleibt als ungetrackte Nutzerdatei unverändert und wird nie gestaged.
