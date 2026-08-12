---
status: done
type: step-plan
task: speedup-tests
step: 004
corrects: null
title: "Minimum Safety Envelope, Legacy-Build-Gate, InternalsVisibleTo und Gate-Switch"
epic: EPIC-1
estimated_risk: high
step_type: single
items: []
created_by: planer
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-12
related_to: []
---

# Step 004: Minimum Safety Envelope, Legacy-Build-Gate, InternalsVisibleTo und Gate-Switch

## Bezug

- **Task:** `speedup-tests`
- **Epic:** `EPIC-1` aus `roadmap.md` — offen sind noch genau die vier
  Punkte, die dieser Step abschließt: Legacy-Build-Gate, Minimum Safety
  Envelope (Config laden, vorbereitete Solution analysieren, CLI-Adapter,
  MCP-Handshake), `InternalsVisibleTo` für die neuen Assemblies und das
  tatsächliche Umschalten des normalen Gates auf die neuen schnellen
  Profile inkl. Legacy-Quarantäne. Nach diesem Step ist EPIC-1
  vollständig, wenn Definition of Done erfüllt ist.
- **Konzept-Referenz:** `konzept.md` Leitplanke 8 „Strangler-Migration
  des bisherigen Testprojekts" (insbesondere die zwei technischen
  Verankerungen „Legacy-Build-Gate" und „Ledger-Konsistenzguard" sowie
  der MSE-Absatz „Die Minimum Safety Envelope ist erst erreicht, wenn…"),
  Leitplanke 0 (`InternalsVisibleTo`-Randbedingung), Leitplanke 9
  (Step-Exit-Invariante).

## Aktueller Projektzustand (JIT-Kontext)

- `LinterEngine` (`src/AiNetLinter/Core/LinterEngine.cs`) hat einen
  **`internal`** Konstruktor und aktuell genau ein
  `[assembly: InternalsVisibleTo("AiNetLinter.Tests")]` (Zeile 18). Jeder
  MSE-Test, der `LinterEngine` direkt gegen eine vorbereitete `Solution`
  laufen lassen will (statt über `Program.Main`), braucht denselben
  Zugriff für `AiNetLinter.FastTests`/`AiNetLinter.IntegrationTests` —
  das ist der konkrete, im Code verifizierte Auslöser für das bisher
  zurückgestellte `InternalsVisibleTo`-Item aus der Roadmap-Notiz.
  `LinterEngine.RunAsync(Solution ...)` (Zeile 64) ist dagegen bereits
  `public` und nimmt direkt einen Roslyn-`Solution`-Snapshot entgegen —
  kein neuer Produkt-Seam nötig, nur der Konstruktorzugriff fehlt.
- `Program.Main(string[])` (`src/AiNetLinter/Program.cs`) ist bereits
  `public static async Task<int>` und wird in der Legacy-Suite
  in-process aufgerufen (`src/AiNetLinter.Tests/Cli/ProgramTests.cs`,
  z. B. `Main_WithEmptyArgs_ReturnsExitCodeOne`) — dasselbe Muster
  liefert den CLI-Adapter-Baustein der MSE ohne Subprozessstart.
- Für den MCP-Handshake-Baustein gibt es noch **keine** geteilte
  Client-Infrastruktur außerhalb der Legacy-Suite:
  `src/AiNetLinter.Tests/Mcp/McpTestClient.cs` kapselt Prozessstart,
  JSON-RPC-Framing und Handshake, ist aber Teil von
  `AiNetLinter.Tests` und wird laut Konzept §9/Leitplanke 11 **nicht**
  vorsorglich ins noch leere `AiNetLinter.TestKit` gehoben, solange nur
  eine Assembly (hier: `IntegrationTests`) sie braucht. Der MSE-Test
  bekommt deshalb einen eigenen, schlanken Handshake-Client lokal in
  `AiNetLinter.IntegrationTests` — kein Kopieren des vollen
  `McpTestClient`-Funktionsumfangs (Retry/Loading-State/Call-Log sind
  nicht Teil der MSE).
- `AiNetLinter.slnx` referenziert weiterhin fünf Projekte inkl.
  `AiNetLinter.Tests`; es existiert noch kein Guard, der das Verschwinden
  des Legacy-Projekts aus der Solution automatisch rot macht (das ist
  exakt die Lücke, die das Legacy-Build-Gate schließt).
- `AGENTS.md` empfiehlt heute noch solutionweite Kommandos
  (`dotnet test --filter Category=Unit` / `--filter Category!=Stress`
  gegen die ganze `AiNetLinter.slnx`) als normales Gate — das läuft
  weiterhin über `AiNetLinter.Tests` mit. Die drei neuen Zielprojekte
  sind zwar in der Solution, aber noch nicht als der maßgebliche
  Standard-Gate-Pfad dokumentiert.
- `TestMigrationLedgerConsistencyTests` (step-002) und die beiden
  Architekturguards/Profilguards (step-002) bleiben unverändert —
  dieser Step erweitert die Sicherheitsnetz-Ebene, ersetzt sie nicht.
  `test-migration-ledger.md` bleibt bei `pending = 183` — dieser Step
  migriert **keine** Legacy-Kohorte, das ist EPIC-3+.
- `tech-debt.md`: TD-001/TD-002 betreffen Legacy-MCP-Framing bzw. den
  Selbstlint-Testglob — keine Überschneidung mit den hier geänderten
  Dateien. TD-003 (`auto_fixable: ja`, `.agents/rules/AiNetLinter.mdc`
  veraltet nach der `rules.json`-Änderung aus step-001) betrifft eine
  Datei, die dieser Step **nicht** berührt (kein `rules.json`-Zugriff,
  keine `ProjectConfigResolver`-Änderung) — deshalb **nicht**
  angehängt, liegen gelassen wie in Schritt 3 des Skills vorgesehen.

## Intention

EPIC-1 „Fundament" abschließen: Nach diesem Step existiert die von
Leitplanke 8 geforderte Minimum Safety Envelope (Konfiguration laden,
vorbereitete Solution analysieren mit regelkonformem Ergebnis und
deterministischem Fehlerweg, ein repräsentativer CLI-Adapter mit
Exit-Code, MCP-Handshake/Toolregistrierung gegen eine Mini-Solution),
ein maschineller Legacy-Build-Gate-Nachweis und die dafür nötigen
`InternalsVisibleTo`-Einträge. Erst danach wird das normale Gate in
`AGENTS.md` tatsächlich auf die neuen schnellen Profile umgeschaltet —
das Legacy-Projekt bleibt Teil der Solution, baubar und gezielt über den
Ledger-Filter ausführbar, läuft aber nicht mehr standardmäßig mit
(Quarantäne im Sinne von Leitplanke 8, kein Entfernen aus der Solution).

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Core/LinterEngine.cs` (Zeile 18)

- **Was:** Ergänzt `[assembly: InternalsVisibleTo("AiNetLinter.FastTests")]`
  und `[assembly: InternalsVisibleTo("AiNetLinter.IntegrationTests")]`
  neben dem bestehenden Eintrag für `AiNetLinter.Tests`.
- **Warum:** Der `LinterEngine`-Konstruktor ist `internal`; ohne diesen
  Eintrag kann kein MSE-Test die Engine direkt gegen eine vorbereitete
  `Solution` instanziieren (CS0122). Das ist die im Code verifizierte
  Randbedingung aus Leitplanke 0 „`InternalsVisibleTo` … Jede neue
  Assembly, die `internal` Seams nutzt, braucht einen eigenen Eintrag."

### Datei 2 (neu): `src/AiNetLinter.FastTests/Core/LinterEngineSolutionAnalysisTests.cs`

- **Was:** Component-Test (`[Trait("Category", "Component")]`), der über
  eine deklarativ per `AdhocWorkspace` aufgebaute Zwei-Projekt-Solution
  (ein Dokument mit absichtlicher Regelverletzung, z. B. eine
  nicht-`sealed` konkrete Klasse bei aktivem `EnforceSealedClasses`; ein
  zweites, regelkonformes Dokument) `LinterEngine.RunAsync(Solution)`
  aufruft und beide Fälle deterministisch prüft: die verletzende Klasse
  erscheint in den `RuleViolation`s mit der erwarteten Regel-ID, die
  konforme Klasse liefert keine Verletzung. Deckt den MSE-Baustein
  „vorbereitete Solution analysieren, regelkonformes Ergebnis und
  deterministischer Fehlerweg" ab, ohne MSBuild oder Dateisystem zu
  benötigen (Component-Ebene laut Leitplanke 1).
- **Warum:** Bisher ist „Solution analysieren" nur indirekt über
  `ProjectOverrideResolutionTests`/`ProjectOverrideRealSolutionTests`
  (Config-Auflösung, kein Analyse-Ergebnis) abgedeckt; die MSE fordert
  explizit den Analyse-Erfolgspfad und den Fehlerpfad.

### Datei 3 (neu): `src/AiNetLinter.IntegrationTests/Cli/CliAdapterExitCodeTests.cs`

- **Was:** Integration-Test (`[Trait("Category", "Integration")]`), der
  `Program.Main(string[])` in-process gegen eine kopierte Mini-Fixture
  aufruft (z. B. `tests/Fixtures/BaselineMini` oder `CompileErrorMini`,
  je nachdem welche Fixture einen klaren grün/rot-Kontrast liefert) und
  den Exit-Code prüft: `0` für einen sauberen Lauf,
  ungleich `0` für einen Lauf mit erwarteten Verstößen/Ladefehlern.
  Folgt dem in-process-Muster aus `src/AiNetLinter.Tests/Cli/
  ProgramTests.cs` (`Main_WithEmptyArgs_ReturnsExitCodeOne`).
- **Warum:** MSE-Baustein „ein repräsentativer CLI-Adapter mit
  Exit-Code" — bisher von keinem Test in den drei neuen Projekten
  abgedeckt.

### Datei 4 (neu): `src/AiNetLinter.IntegrationTests/Mcp/McpHandshakeToolRegistrationTests.cs`

- **Was:** Integration-Test (`[Trait("Category", "Integration")]`), der
  `AiNetLinter.exe` als echten MCP-Subprozess gegen eine Mini-Fixture
  startet, den JSON-RPC-`initialize`-Handshake durchführt und
  `tools/list` aufruft, um zu prüfen, dass die erwarteten Tools
  registriert sind. Eigener, schlanker Client (nur Start, Handshake,
  ein `tools/list`-Call, Dispose) — **kein** Kopieren des vollen
  `McpTestClient`-Funktionsumfangs aus der Legacy-Suite und **keine**
  TestKit-Extraktion (Leitplanke 11: erst bei zwei echten Konsumenten).
- **Warum:** MSE-Baustein „MCP-Handshake/Toolregistrierung gegen eine
  Mini-Solution" — heute nur in der Legacy-Suite abgedeckt, die nach dem
  Gate-Switch (Datei 6) nicht mehr standardmäßig läuft.

### Datei 5 (neu): `src/AiNetLinter.IntegrationTests/Migration/LegacyProjectBuildGateTests.cs`

- **Was:** Integration-Test (`[Trait("Category", "Integration")]`), der
  `AiNetLinter.slnx` liest (Wiederverwendung des in `test-migration-
  ledger.md`/`TestMigrationLedgerConsistencyTests` etablierten
  Roslyn-Scan- bzw. Solution-Lademusters) und prüft: (a) das Projekt
  `AiNetLinter.Tests` ist weiterhin Teil der Solution, (b) seine
  `.csproj`-Datei existiert auf der Platte. Schlägt rot, sobald jemand
  das Legacy-Projekt aus der Solution entfernt, solange
  `test-migration-ledger.md` noch `pending`-Zeilen enthält — der
  eigentliche „bleibt kompilierbar"-Nachweis kommt weiterhin vom
  ohnehin bei jedem Step laufenden `dotnet build AiNetLinter.slnx`
  (baut alle fünf Projekte); dieser Guard sichert nur die dafür
  notwendige Solution-Mitgliedschaft mechanisch ab, statt sie
  stillschweigend vorauszusetzen.
- **Warum:** Konzept Leitplanke 8 „Legacy-Build-Gate … ein nicht mehr
  baubares Legacy-Projekt ist ein Sicherheitsnetz, das nur noch im
  Dokument existiert." Ohne diesen Guard bemerkt niemand ein
  versehentliches Entfernen aus der `.slnx`, solange `pending > 0`.

### Datei 6: `AGENTS.md` (Abschnitt zu Testkommandos/Gate, ca. Zeile 30-61)

- **Was:** Schaltet das dokumentierte **normale** Gate von den
  solutionweiten `Category`-Filtern auf die neuen Zielprofile um:
  Standard-Iterationslauf wird `dotnet test src/AiNetLinter.FastTests
  --filter Category=Unit` (bzw. `Category!=Stress` für den vollen
  Fast-Slice), Abschluss-Gate wird die Kombination aus
  `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` und
  `dotnet test src/AiNetLinter.IntegrationTests --filter
  Category!=Stress`. `AiNetLinter.Tests` (Legacy) wird explizit als
  quarantiniert dokumentiert: weiterhin Teil der Solution und baubar,
  aber nicht mehr Teil des normalen Gates — gezielte Ausführung nur
  noch über den in `test-migration-ledger.md` genannten engsten
  Legacy-Filter (Leitplanke 7 „Bei Änderung noch nicht migrierten
  Produktcodes"). Stress-Profil-Hinweis (Zeile 51/53) bleibt inhaltlich
  bestehen, wird nur auf die neue Projektstruktur bezogen.
- **Warum:** Das ist der eigentliche Gate-Switch aus der Roadmap-Notiz
  „Umschalten des normalen Gates auf die neuen schnellen Profile inkl.
  tatsächlicher Legacy-Quarantäne" — ohne diese Änderung bleibt die
  ganze Fundament-Arbeit aus step-001/002/003/004 wirkungslos, weil
  Coder/Kritiker weiterhin den alten, langsamen Vollpfad als Gate lesen.

### Datei 7: `.agents/rules/AiNetLinterRichtlinien.mdc` (§3, TRX-Diagnoseregel)

- **Was:** Ergänzt die bestehende `TestResults/latest.trx`-Diagnoseregel
  um einen Hinweis, dass ab jetzt mehrere Zielprojekte existieren und
  `latest.trx` sich pro `dotnet test`-Aufruf überschreibt (bereits als
  bekannte Unschärfe in `step-002/step-result.md` dokumentiert) — die
  Regel verweist ab sofort auf `AGENTS.md` als maßgebliche Quelle für
  die aktuell gültigen Gate-Kommandos, statt die Kommandos hier ein
  zweites Mal zu pflegen.
- **Warum:** Verhindert Drift zwischen zwei Dokumenten, die beide das
  Gate beschreiben, direkt an der Stelle, an der Datei 6 den Gate-Text
  bereits ändert. Kein eigener Verankerungsmechanismus wie das
  eigentliche Ledger — reine Konsistenzpflege derselben Änderung.

## Tests

- [ ] `dotnet build` (nach `dotnet clean`) — 0 Warnungen/Fehler, alle
      fünf Projekte bauen weiterhin (belegt Legacy-Build-Gate-Aussage
      unabhängig vom neuen Guard-Test).
- [ ] `dotnet test src/AiNetLinter.FastTests --filter
      FullyQualifiedName~LinterEngineSolutionAnalysisTests`
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter
      FullyQualifiedName~CliAdapterExitCodeTests`
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter
      FullyQualifiedName~McpHandshakeToolRegistrationTests`
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter
      FullyQualifiedName~LegacyProjectBuildGateTests`
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter
      FullyQualifiedName~TestMigrationLedgerConsistencyTests` (Regression
      — stellt sicher, dass die `InternalsVisibleTo`-Änderung und die
      neuen Dateien den bestehenden Ledger-Guard nicht stören)
- [ ] Epic-Grenze (Leitplanke 7 „An Epic-/Architekturgrenzen"), da
      dieser Step EPIC-1 abschließt und den Gate-Text umschaltet:
      einmal `dotnet test src/AiNetLinter.FastTests --filter
      Category!=Stress` **und** `dotnet test
      src/AiNetLinter.IntegrationTests --filter Category!=Stress`
      als Nachweis, dass der neue Standard-Gate-Pfad aus Datei 6
      tatsächlich grün ist. **Kein** solutionweiter
      `Category!=Stress`-Lauf über die Legacy-Suite — die ist laut
      diesem Step bewusst nicht mehr Teil des Standardpfads und ihr
      Zustand ist durch step-002 bereits dokumentiert.

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt
- [ ] Build-Command aus Tech-Stack-Notiz (`roadmap.md`) grün
- [ ] Alle oben gelisteten Testkommandos grün
- [ ] `test-migration-ledger.md` unverändert (`pending = 183`, keine
      Kohorte migriert — reines Fundament-Item)
- [ ] `AGENTS.md` beschreibt die drei neuen Zielprojekte als
      maßgeblichen Standard-Gate-Pfad, Legacy-Projekt ausdrücklich als
      quarantiniert (baubar, gezielt ausführbar, nicht im normalen Gate)
- [ ] Commit auf aktuellem Branch (Conventional Commit)
- [ ] `step-004/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf
      `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc#4-updates--tests` — Pflicht
  zu xUnit-v3-Tests je Logikänderung, Parallelitäts-/Collection-Regel
  (neue Testklassen aus diesem Step dürfen nicht pauschal in eine
  serialisierende Collection gepackt werden — der MCP-Handshake-Test
  startet zwar einen echten Subprozess, braucht dafür aber keine
  Collection-weite Serialisierung, solange er isoliert bleibt),
  MCP-Test-Pflicht über C#-Infrastruktur (keine Ad-hoc-Skripte),
  Commit-Vorschlag-Pflicht.
- `.agents/rules/AiNetLinterRichtlinien.mdc#3-windows-umgebung--tool-regeln`
  — betroffen durch Datei 7 (TRX-Diagnoseregel-Anpassung).
- `.agents/rules/AiNetLinterRichtlinien.mdc#5-qualitätsdrift-prävention`
  — Zero-Warning-Direktive gilt für die drei neuen Testdateien; sparsame
  Kommentare ohne Task-/Step-ID-Referenzen im neuen Testcode.

## Bekannte Ausnahmen

- Keine bekannten flaky Tests in den hier neu angelegten Dateien. Die
  bereits dokumentierte Framing-Flakiness (TD-001) betrifft ausschließlich
  Legacy-Code und wird durch den Gate-Switch dieses Steps eher entschärft
  (Legacy läuft nicht mehr standardmäßig mit), nicht verschärft.

## Notes

- **Warum ein Step statt zwei:** Die Roadmap listet Legacy-Build-Gate,
  MSE, `InternalsVisibleTo` und Gate-Switch als vier zusammengehörige
  Restpunkte desselben Fundament-Epics. `InternalsVisibleTo` existiert
  ausschließlich, weil die MSE-Tests es brauchen; das Legacy-Build-Gate
  und der Gate-Switch sind laut Leitplanke 8 kausal aneinandergekettet
  (erst MSE, dann Quarantäne). Sie separat zu planen würde denselben
  Zusammenhang künstlich über zwei Step-Reviews aufteilen — das
  widerspricht der Vorgabe „wenige große, vertikale Steps" (Konzept §9,
  `feedback-grosse-steps-drift-loop.md`).
- **Warum kein TestKit-Zugriff:** Weder der CLI-Adapter- noch der
  MCP-Handshake-Test brauchen einen geteilten Helper, den heute zwei
  Assemblies konsumieren würden (`AiNetLinter.TestKit` bleibt in diesem
  Step leer) — das ist bewusst konsistent mit Leitplanke 11 und dem
  Notes-Eintrag aus `step-001`.
- **Nach diesem Step ist EPIC-1 fertig**, sofern Review `approved`
  zurückkommt — der nächste Planer-Aufruf sollte dann direkt EPIC-2
  (`RoslynTestSolutionFactory`, `PreparedSolutionFixture`, gecachte
  `MetadataReference`n, lazy Materialisierung, `FilterMini`-Fixture)
  anstoßen. Die endgültige Abhak-Entscheidung trifft aber der nächste
  Planer-Durchlauf anhand des tatsächlichen Step-Review-Ergebnisses,
  nicht dieser Plan.
- **CS0050-Falle laut Leitplanke 0 vermeiden:** Die neuen
  MSE-Tests dürfen `internal` Produkttypen nur konsumieren, nicht in
  einer eigenen `public`-API weiterreichen — betrifft hier vor allem
  Datei 2 (`LinterEngine`-Konstruktor bleibt intern genutzt, nicht neu
  exponiert).
