---
task: verbesserungen-mcp
completed_at: 2026-08-05T11:50:00Z
final_status: done  # done | aborted
total_iterations: 1
total_commits: 26
total_epics: 3
total_tech_debt_entries: 5
---

# Task Summary: verbesserungen-mcp

## Ergebnis

Alle sechs Muss-Haben-Punkte aus `konzept.md` Scope sind addressiert
und durch dedizierte Regressionstests abgesichert: P1 Blazor-Partials
(EPIC-01) durch Roslyn-Paket-Bump 5.3.0 → 5.6.0 plus semantischen
Basistyp-Fallback in `SkeletonSyntaxWalker`, P1 Einheitlicher
Symbol-Identifikator-Parser (EPIC-02) durch zentralen Stable-ID-Zweig in
`FindReferencesTool.ResolveSymbolAsync`, P2/P3-Batch (EPIC-03) mit
ID-Korruption-Fix, präzisierter `get_violations`-Meldung, behobener
Overview-Status-Race und dokumentiertem 200-Knoten-Hard-Cap. Der einzige
MAJOR-Befund während des Tasks (step-002 zeigte `ComponentBase` in
`get_file_skeleton` zunächst nicht) wurde durch `step-002/fix-01` in
derselben Iteration vollständig behoben. Der gesamte Befund passt zur
ursprünglichen Intention: Roslyn-Symbolgraph für Blazor stimmt mit
`dotnet build` überein, alle vier Symbolgraph-Tools (`find_references`,
`get_impact`, `get_type_hierarchy`, `get_symbol_body`) akzeptieren jetzt
einheitlich alle drei dokumentierten Identifikator-Formate, und die
kleinen Tool-Konsistenz-Fixes sind geliefert. Voller `dotnet test`-Lauf
vom Kritiker selbst reproduziert: 1267 Tests, 0 Fehler, 0 übersprungen.

## Roadmap-Status

Alle drei Epics abgehakt (`[x]`) und auf Status `done`. Das in
`konzept.md` „Nice-to-Have" genannte EII-`#`-Encoding-Lesbarkeits-Thema
ist **bewusst außerhalb dieses Tasks** geblieben (kein
Definition-of-Done-Punkt; roslyn-Standardverhalten, kein Bug laut
`konzept.md` „Entdeckte Mängel"; größerer Gestaltungsspielraum für eine
separate Entscheidung). Siehe `roadmap.md` EPIC-03 für die
ausdrückliche Begründung des Ausschlusses.

## Steps-Übersicht

| Step | Epic | Status | Title | Commit | Notiz |
|------|------|--------|-------|--------|-------|
| step-001 | EPIC-01 | done | Blazor-Partial-Fixture anlegen und Symbolgraph-Diskrepanz reproduzieren | `fbc399f` | approved — Fixture + 3 grüne Reproduktionstests, 0 Iterationen |
| step-002 | EPIC-01 | done | Roslyn-Paket-Versions-Bump: Razor-Source-Generator-Integration tatsächlich zum Laufen bringen | `7f4d6ba` | approved nach 1 Fix-Runde (siehe step-002/fix-01) — MAJOR-Finding zu `get_file_skeleton`/`ComponentBase` |
| step-002/fix-01 | EPIC-01 | done | SkeletonSyntaxWalker: semantischen Fallback für Basistyp bei fehlender Basisliste ergänzen | `c614348` | approved — `NormalizeToOwningMember` per `SpecialType`-Guard, beide DoD-Hälften erfüllt |
| step-003 | EPIC-02 | done | Einheitlicher Symbol-Identifikator-Parser | `48d596c` | approved, 0 Iterationen — Stable-ID-Zweig zentral in `ResolveSymbolAsync`, duplizierte `GetSymbolBodyTool`-Vorstufe entfernt |
| step-004 | EPIC-03 | done | EPIC-03-Batch: get_symbol_body-ID-Korruption, get_violations-Meldung, ainetlinter://overview-Status-Race, depth-Hard-Cap-Doku | `e1d0124` | approved, 0 Iterationen — 4 Items, zwei dokumentierte Plan-Abweichungen (item-01 zentralisiert, item-03 LoadState statt DescribeSolution) vom Reviewer als korrekt verifiziert |

## Globale Audit-Befunde (Kritiker, Modus `global`)

### Konzept erfüllt?

Vollständig. Alle sechs Muss-Haben-Punkte aus `konzept.md` Scope P1/P2/P3
adressiert. Der Schnell-Check nach Fix (5-Punkte-Abnahme-Checkliste aus
`konzept.md` „Definition of Done") ist zu 100% erfüllt — jeder Punkt
durch dedizierten Test verifiziert: (1) `get_index_scope` ohne
1322-Errors-Hinweis (`SourceFileCatalogBlazorPartialTests.
GetIndexScope_BlazorPartialFixture_ShowsNoCompileErrorHint`),
(2) `get_file_skeleton(SiteView.razor.cs)` ohne `CS0115` und mit
sichtbarer `ComponentBase`-Basisklasse (`GetFileSkeleton_
SiteViewRazorCs_ShowsComponentBaseAndNoCompileError`),
(3) `find_references(id aus skeleton)` Treffer > 0
(`FindReferencesToolTests.ExecuteAsync_StableId_ReturnsCallSiteInCaller`),
(4) `get_violations(scopeFilter: ...)` differenziert korrekt
(`GetViolationsToolTests.
FormatReport_FilesInScopeButZeroViolations_DistinguishesFromNoFilesInScope`),
(5) `get_symbol_body(Datei:Zeile:Spalte)` liefert Property-ID statt
Accessor-ID (`GetSymbolBodyToolTests.
ExecuteAsync_PositionOnPropertyAccessorKeyword_ReturnsPropertyIdNotAccessorId`).
EII-Nice-to-Have ist explizit „außerhalb dieses Tasks" laut Konzept und
Roadmap, keine Lücke im Sinne der DoD.

### Seiteneffekte / Regressionen

Keine. Build (`dotnet build AiNetLinter.slnx`) grün, 0 Fehler, 0
Warnungen — vom Kritiker selbst reproduziert (Zero-Warning-Direktive
eingehalten, auch nach dem Roslyn-Paket-Bump auf 5.6.0). Voller
`dotnet test AiNetLinter.slnx` grün, 1267 Tests / 0 Fehler / 0
übersprungen, Dauer ~1:56 — vom Kritiker selbst reproduziert. Der in
TD-003 dokumentierte intermittierende Testhost-Absturz ist diesmal nicht
aufgetreten, kein TD-003-Workaround nötig. Blast-Radius-Hinweis des
Coders in step-004 (Verhalten von `get_impact`/`get_type_hierarchy` nach
item-01-Zentralisierung) hat sich als unauffällig erwiesen — keine
bestehenden Tests verwenden Accessor-Keyword-Positionen, keine
Regressions-Reports. Doku-Pflicht laut `konzept.md` „Docs/configuration.md
+ Docs/ROADMAP.md aktualisieren, falls CLI-Optionen/rules.json betroffen"
war nicht ausgelöst (keine Änderung an `rules.json` oder CLI-Optionen in
diesem Task) — keine Doku-Änderung fällig.

### Rules-Konformität (Stichproben)

Geprüft an step-001, step-003, step-004 gegen die in deren Plänen
zitierten `AiNetLinterRichtlinien.mdc`-Abschnitte (§4 Updates & Tests,
§5 Qualitätsdrift-Prävention) und die in `AiNetLinter.mdc` codierten
Grenzwerte. Alle Stichproben ok: **step-001** — xUnit v3 Pflicht
eingehalten, neue Testklasse ohne serialisierende Collection, `sealed`
auf beiden neuen Klassen, ID-freier Why-Kommentar in der Testklasse
(§5), Commit mit Conventional-Format + `[verbesserungen-mcp]`-Suffix.
**step-003** — neuer Stable-ID-Zweig rein additiv und früh-verlassend
(kein Umbau der bestehenden Position/Name-Zweige), duplizierte
`GetSymbolBodyTool`-Vorstufe vollständig entfernt (`AvoidExcessiveMiddleMen`),
Methodengrenzwerte eingehalten, Tool-Beschreibungen aktualisiert
(„Doku-Ehrlichkeit"). **step-004** — keine Task-/Step-/Epic-IDs in
neuen Code-Kommentaren (§5 Verbot), die zwei `ainetlinter-disable`-
Kommentare (in `McpCodeGraphServer.cs` und `GetViolationsScanner.cs`)
sind mit substantieller Begründung versehen und entsprechen der §5-
Ausnahme „unkonventionelles Why"; `BanBlockingTaskAccess`-Disable
verwendet dasselbe Begründungs-Pattern wie der bereits bestehende
Eintrag in `GetCurrentSolution()`; `EnforceSealedClasses` an der neuen
Testklasse `SymbolGraphToolRegistrationsTests` eingehalten. Commit-
Vorschlag-Pflicht (§4) in jedem Code-Commit erfüllt (alle Commits
haben Conventional-Commit-Format, deutsch, Suffix, Refs-Zeile).

## Tech-Debt-Zusammenfassung

- **Hoch:** 0 Einträge
- **Mittel:** 3 Einträge — `TD-001` (Regex `Dateien?` in
  Aggregat-Warnung-Tests, maskierter Singular-Fall in 6 bestehenden
  Testklassen), `TD-003` (intermittierender Testhost-Absturz im vollen
  `dotnet test`-Lauf — Sandbox-Infrastruktur, nicht durch den Bump
  verursacht; in dieser Review-Session nicht aufgetreten),
  `TD-005` (`AIContextFootprint`-Schwellwert 2800 für
  `AnalysisToolRegistrations.cs` + fragiler
  `CliIntegrationTests.RunLinterCli_OnWholeSolution_ReturnsSuccess`,
  konkret im step-004 als Workaround mit einzeiliger Suppression
  umschifft)
- **Niedrig:** 2 Einträge — `TD-002` (Produktionscode
  `GetIndexScopeScanner.FormatBreakdown` pluralisiert „1 Datei" falsch),
  `TD-004` (vorbestehender, zerrissener XML-Doc-Kommentar an
  `FindReferencesTool.ExecuteAsync`)

**Blick-würdige Hinweise (keine Empfehlung, nur Sortierhilfe):**
`TD-001` und `TD-002` sind verwandt (Pluralisierungs-Kategorie) und
jeweils sehr klein, gut abgrenzbar, geringer Aufwand — wenn Aufräumen
gewünscht ist, würden sich beide zusammen in einem Mini-Step erledigen
lassen. `TD-005` hat das konkrete Risiko, dass bereits eine einzige
weitere Code-Zeile in einer transitiv abhängigen Datei
(`McpCodeGraphServer.cs`, `GetViolationsTool` → `GetViolationsScanner`,
etc.) den Schwellwert reißt; die im step-004 gewählte Mitigation
(Suppression einzeilig) ist fragil. Ein Folge-Task, der entweder den
Schwellwert moderat anhebt (z. B. auf 2820) oder den
`CliIntegrationTest` auf `--scope-filter` umstellt, würde diese
Wartungslast entschärfen. `TD-003` ist ein bestehendes
Sandbox-Problem, das außerhalb dieses Tasks liegt.

## Offene Punkte

- **EII-Nice-to-Have** (lesbarere ID-Darstellung für explizite
  Interface-Implementierungen): kein offener Punkt im Sinne des Tasks —
  in `konzept.md` ausdrücklich als „Nice-to-Have (optional, spätere
  Iteration)" markiert und in `roadmap.md` EPIC-03-Beschreibung
  ausdrücklich als „außerhalb dieses Tasks" deklariert. Bewusst kein
  Muss-Haven aus `konzept.md` Definition of Done; wartet auf
  Nutzer-Entscheidung, ob ein eigener Folge-Step gewünscht ist. Hier
  nicht als „[ ]" aufgenommen, weil es nicht zu diesem Tasks DoD gehört.

## Empfehlungen

- **TD-005** könnte als eigenes Epic in einem Folge-Task aufgenommen
  werden (Schwellwert-Anhebung 2800 → z. B. 2820 in `rules.json`
  PathOverride für `AnalysisToolRegistrations.cs`, **oder** Umbau des
  `CliIntegrationTests` auf einen `--scope-filter`-basierten Smoke-Test).
  Der im step-004 gewählte einzeilige Suppression-Workaround ist
  fragil und reißt beim nächsten inhaltlichen Patch in einer transitiv
  abhängigen Datei.
- **TD-001 + TD-002** (Pluralisierungs-Paar) ließen sich in einem
  kleinen Aufräum-Schritt zusammen erledigen (Test-Regex auf
  `Datei(en)?` + `FormatBreakdown` mit Singular/Plural-Unterscheidung).
  Kein Eile-Bedarf, nur falls der Nutzer Aufräumarbeiten ohnehin
  angehen will.
- **Vor dem Push** einen finalen `dotnet test`-Volllauf auf der
  integrierten Branch durchführen (TD-003-Risiko in
  Sandbox-Umgebungen, im aktuellen Review-Lauf nicht aufgetreten).
- Falls **EII-Nice-to-Have** doch gewünscht ist: eigener Planer-Lauf
  mit eigenem `step-…`-Verzeichnis — größerer Gestaltungsspielraum
  (Anzeige in `get_symbol_body` und/oder `get_file_skeleton`?

  reine Zusatzdarstellung neben der Standard-ID, kein Ersatz).

## Statistik

- **Anzahl Epics:** 3, davon abgehakt: 3
- **Anzahl Steps:** 5 (step-001, step-002, step-002/fix-01, step-003,
  step-004)
- **Davon approved:** 5
- **Davon blocked:** 0
- **Anzahl Commits:** 26 (alle mit `[verbesserungen-mcp]`-Suffix;
  Zusammensetzung: 5 Code-/Test-Commits, 4 Step-Abschluss-Doku-Commits,
  8 Roadmap-/Task-State-Doku, 7 Chore, 2 Initial-Konzept-Doku aus
  Konzept-Initial-Runden)
- **Anzahl Tech-Debt-Einträge:** 5 (3 mittel, 2 niedrig, 0 hoch)
- **Loop-Iterationen (Fix-Runden):** 1 / 12 (Task-Not-Anker) — eine
  einzelne Fix-Runde in `step-002/fix-01`, alle anderen Steps ohne
  Iterationen approved
- **Laufzeit:** 2026-08-05 (alle Schritte am selben Tag; konkrete
  Timestamps in den jeweiligen `step-result.md`-Dateien)
