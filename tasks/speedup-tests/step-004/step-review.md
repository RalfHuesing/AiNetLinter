---
status: done
type: step-review
task: speedup-tests
step: 004
epic: EPIC-1
step_type: single
reviewed_by: kritiker
reviewed_by_model: claude-sonnet-5
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-12
verdict: issues
tech_debt_ids: []
---

# Review Step 004: Minimum Safety Envelope, Legacy-Build-Gate, InternalsVisibleTo und Gate-Switch

## Verdict

- [ ] **approved** — alle vier Prüfebenen ok
- [x] **issues** — Korrektur-Step `step-005` angelegt (`corrects: step-004`)
- [ ] **blocked** — Nutzer-Entscheidung nötig (siehe Frage unten)

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `<rules_dir>/**` eingehalten (mit einer Ausnahme, siehe Findings)
- [x] Logische Korrektheit: Code macht was er soll, nicht nur „grün"
- [x] Konzept-Treue: passt die Umsetzung zu `konzept.md` (mit einer Ausnahme, siehe Findings)
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün

## Befund

### Plan-Erfüllung

Alle sieben „Konkrete Änderungen" aus dem Plan sind umgesetzt und decken sich mit dem Diff von
`a303edb`:

- **Datei 1** (`LinterEngine.cs`): beide neuen `InternalsVisibleTo`-Einträge exakt wie gefordert,
  nichts zusätzlich geöffnet — erfüllt.
- **Datei 2** (`LinterEngineSolutionAnalysisTests.cs`): Component-Test, `AdhocWorkspace`-Zwei-
  Klassen-Solution, `LinterEngine.RunAsync(Solution)`, prüft Verletzungs- und Konformitätspfad in
  einem Test wie im Plan skizziert — erfüllt.
- **Datei 3** (`CliAdapterExitCodeTests.cs`): `Program.Main(string[])` in-process gegen eine
  kopierte `BaselineMini`-Fixture, Exit-Code-Kontrast — erfüllt, mit dokumentierter und sauber
  isolierter Abweichung (siehe „Logische Korrektheit").
- **Datei 4** (`McpHandshakeToolRegistrationTests.cs`): echter `AiNetLinter.exe --mcp-server`-
  Subprozess, JSON-RPC-Handshake über `McpClient.CreateAsync`, `tools/list`, eigener schlanker
  Client ohne TestKit-Extraktion — erfüllt.
- **Datei 5** (`LegacyProjectBuildGateTests.cs`): liest `AiNetLinter.slnx`, prüft Solution-
  Mitgliedschaft und `.csproj`-Existenz von `AiNetLinter.Tests`, bewusster Bypass bei
  `pending == 0` — erfüllt, deckt sich mit Plan-Text.
- **Datei 6** (`AGENTS.md`): Gate-Switch korrekt umgesetzt — neues Standard-Gate ist
  `FastTests`/`IntegrationTests` je `Category!=Stress`, Legacy-Projekt ausdrücklich als
  quarantiniert (baubar, Teil der Solution, gezielter Filter) dokumentiert, Stress-Hinweis auf
  neue Struktur bezogen — erfüllt.
- **Datei 7** (`AiNetLinterRichtlinien.mdc` §3): TRX-Diagnoseregel verweist jetzt auf `AGENTS.md`
  als alleinige Quelle der Gate-Kommandos, statt Kommandos zu duplizieren — erfüllt für den im
  Plan explizit benannten Abschnitt §3. Siehe jedoch Finding 1 zu einem *nicht* im Plan
  adressierten, aber inhaltlich zusammenhängenden Abschnitt (§4).

`test-migration-ledger.md` unverändert (`pending = 183`, verifiziert per `grep -c pending`),
`codemap.md` für alle vier neuen Dateien sowie die drei geänderten Bestandsdateien aktualisiert
(zweiter Commit `59dcff9`). Commit-Message folgt Conventional-Commits-Format mit Task-Suffix.
`step-plan.md`-Status korrekt auf `done (pending audit)` gesetzt.

### Rules-Konformität

Gegen die im Plan zitierten Rules-Refs geprüft:

- `AiNetLinterRichtlinien.mdc#4-updates--tests`: xUnit-v3-Pflicht erfüllt (alle vier neuen Dateien
  `[Fact]`/xUnit v3), Parallelitäts-/Collection-Regel eingehalten (kein `[Collection(...)]` auf dem
  MCP-Handshake-Test, obwohl er einen echten Subprozess startet — konsistent mit der im Plan
  explizit begründeten Ausnahme), MCP-Test-Pflicht über C#-Infrastruktur erfüllt (kein Ad-hoc-
  Skript). Kommentarstil („Leitplanke N", „MSE-Baustein") folgt bereits etabliertem Muster aus
  step-002 (`TestMigrationLedgerConsistencyTests.cs`, `TestCategoryProfileGuardTests.cs`), keine
  neue Task-/Step-ID-Referenz (kein `step-004`, `TD-XXX` o. ä. im Code) — konform zur
  Kommentarregel in §5.
  **Aber:** Siehe Finding 1 — §4 selbst (Abschnitt „MCP & Dogfood Testing") enthält nach diesem
  Step einen sachlichen Widerspruch zur eigenen Umsetzung.
- `AiNetLinterRichtlinien.mdc#3-windows-umgebung--tool-regeln`: Datei-7-Änderung korrekt, siehe
  oben.
- `AiNetLinterRichtlinien.mdc#5-qualitätsdrift-prävention`: Zero-Warning eingehalten (`dotnet
  build` 0 Warnungen selbst nachgeprüft), keine Task-/Step-ID-Kommentare in den vier neuen Dateien.

### Logische Korrektheit

- **CLI-Adapter-Test / Abweichung vom Plan:** Der Coder ersetzt die kopierte `rules.json` der
  `BaselineMini`-Fixture durch eine minimale, vollständig kontrollierte Konfiguration (nur
  `EnforceSealedClasses` aktiv). Selbst nachvollzogen: `CopyFixtureDirectory` kopiert nach
  `Path.GetTempPath()/ainetlinter-cli-adapter-<guid>`, `File.WriteAllText(configPath, ...)`
  schreibt ausschließlich in die Kopie, `Directory.Delete(rootPath, recursive: true)` läuft in
  `finally`. Die echte Fixture unter `tests/Fixtures/BaselineMini/` bleibt unverändert
  (`git status --porcelain tests/Fixtures/BaselineMini` liefert nach lokalem Testlauf keine
  Änderung). Keine Kopplung an globale Projektkonfiguration, keine versehentliche Mutation der
  kanonischen Fixture. Die Begründung (Default-Config ist scharf genug, dass ein ungeprüfter
  „sauberer" Lauf unsicher wäre) ist nachvollziehbar und im `step-result.md` korrekt als
  Abweichung deklariert, nicht verschwiegen.
- **`LinterEngineSolutionAnalysisTests`:** `internal`-Konstruktor wird konsumiert, nicht in einer
  neuen `public`-API weitergereicht (CS0050-Falle aus den Plan-Notes vermieden). Assertion prüft
  sowohl Positiv- als auch Negativfall über `RuleName`/`FilePath`-Suffix — aussagekräftig.
  `AdhocWorkspace` wird korrekt lokal erzeugt, nicht geteilt, kein Leak in andere Tests.
  `Solution` selbst wird nicht disposed (der `Workspace`, der sie besitzt, verlässt aber ohnehin
  den Scope am Testende) — unkritisch für einen Einzeltest ohne Fixture-Sharing.
- **`McpHandshakeToolRegistrationTests`:** prüft nur zwei repräsentative Tool-Namen statt der
  vollständigen Liste (18 beim Legacy-Pendant). Plan verlangt nur „dass die erwarteten Tools
  registriert sind", keine Vollständigkeitsprüfung — im Rahmen des MSE-Zwecks (Handshake +
  Toolregistrierung als Baustein, nicht als Vollständigkeitsvertrag) ausreichend, siehe auch
  „Sonstige Beobachtungen".
- **`LegacyProjectBuildGateTests`:** liest `test-migration-ledger.md` per Zeilen-Split auf
  Markdown-Tabellenspalten (`cells.Length == 6 && cells[3] == "pending"`) — funktional
  nachvollzogen gegen das tatsächliche Ledger-Format (6 Spalten, vierte Spalte Status), Zählung von
  183 stimmt mit `grep -c pending` überein. Bypass bei `pending == 0` ist explizit im Plan verlangt.

### Konzept-Treue (Ebene 4)

Grundsätzlich konsistent mit `konzept.md` Leitplanke 8: Quarantäne bedeutet „kein Solution-
Ausschluss", `AiNetLinter.Tests` bleibt Teil von `AiNetLinter.slnx`, Build-Gate mechanisch
abgesichert. MSE-Kette (Config laden, Solution analysieren, CLI-Adapter, MCP-Handshake) laut
Leitplanke-8-Absatz „Die Minimum Safety Envelope ist erst erreicht, wenn…" vollständig abgedeckt.
`InternalsVisibleTo` exakt gemäß Leitplanke 0 nur für die beiden Assemblies erweitert, die
tatsächlich `internal` Seams nutzen (`AiNetLinter.TestKit` bewusst ausgelassen, da leer — konsistent
mit Notes-Abschnitt des Plans).

**Ein Punkt widerspricht sich jedoch nach diesem Step (Finding 1):** `AGENTS.md` beschreibt den
neuen Standard-Gate-Pfad korrekt und konsistent mit der Quarantäne-Definition aus Leitplanke 8.
`.agents/rules/AiNetLinterRichtlinien.mdc` §4 „MCP & Dogfood Testing" (unverändert von diesem
Step, aber Teil der im Plan selbst zitierten Rules-Refs `#4-updates--tests`) behauptet weiterhin,
MCP-Funktionalitäten und Dogfooding würden „ausschließlich" über `McpTestClient` in
`AiNetLinter.Tests` geprüft — genau das Projekt, das dieser Step gerade als nicht mehr Teil des
Standard-Gates dokumentiert, während gleichzeitig `McpHandshakeToolRegistrationTests` (dieser
Step) den MCP-Handshake bereits in `AiNetLinter.IntegrationTests` abdeckt. Das ist ein echter
Widerspruch zu einer von diesem Step selbst getroffenen Entscheidung, keine bloß kosmetische
Auslassung — siehe Kritiker-Skill Schritt 2 Ebene 1 „außer sie verdeckt einen echten Widerspruch zu
einer bereits dokumentierten Entscheidung, dann Ebene-4-Fund". Datei 7 des Plans hat exakt diese
Art von Drift für §3 bereits behoben (mit der Begründung „Verhindert Drift zwischen zwei
Dokumenten, die beide das Gate beschreiben"); §4 blieb dabei unberücksichtigt, obwohl der Plan
denselben Rules-Ref-Abschnitt zitiert.

### Build-/Test-Status

```
dotnet build AiNetLinter.slnx (nach dotnet clean)                                    → grün, 0 Warnungen/Fehler, 5 Projekte (selbst nachvollzogen)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress        → grün (10 Tests) — selbst nachvollzogen
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → grün (12 Tests) — selbst nachvollzogen
```

Beide Epic-Grenz-Kommandos aus der Test-Checkliste des Plans selbst ausgeführt und grün bestätigt
(Testzahlen decken sich mit `step-result.md`). Die übrigen, engeren Testkommandos aus der Checkliste
wurden nicht einzeln erneut ausgeführt, da sie in der Obermenge `Category!=Stress` enthalten sind
und dort grün liefen.

## Findings

1. `.agents/rules/AiNetLinterRichtlinien.mdc:94` — **[MAJOR]** **[Konzept-Treue / Rules-
   Konformität]** §4 „MCP & Dogfood Testing" behauptet weiterhin, MCP-Funktionalitäten und
   Live-Verifikationen würden „ausschließlich über die C#-Testinfrastruktur (`McpLiveRepositoryTests`
   und `McpTestClient` in `AiNetLinter.Tests`)" geprüft. Das widerspricht der von diesem Step selbst
   getroffenen Entscheidung: `AiNetLinter.Tests` ist ab jetzt quarantiniert (nicht mehr Teil des
   Standard-Gates), und der MSE-Baustein „MCP-Handshake/Toolregistrierung" lebt bereits in
   `AiNetLinter.IntegrationTests` (`McpHandshakeToolRegistrationTests`). Ein Agent, der sich an
   dieser Zeile orientiert, könnte künftige MCP-Tests fälschlich im quarantänierten Legacy-Projekt
   statt in `AiNetLinter.IntegrationTests` verorten. **Fix:** Zeile so umformulieren, dass sie (a)
   den Kern der Regel (keine Ad-hoc-Skripte, MCP-Tests ausschließlich über C#-Testinfrastruktur)
   beibehält, aber (b) nicht mehr `AiNetLinter.Tests` als exklusiven bzw. maßgeblichen Ort nennt —
   z. B.: „MCP-Funktionalitäten und Live-Verifikationen (Dogfooding gegen das eigene Repo) werden
   ausschließlich über die C#-Testinfrastruktur umgesetzt (aktuell u. a. `McpHandshakeToolRegistrationTests`
   in `AiNetLinter.IntegrationTests`; die verbleibenden `pending`-MCP-Verträge liegen bis zu ihrer
   Migration in `McpLiveRepositoryTests`/`McpTestClient`, `AiNetLinter.Tests`). Das Anlegen von
   Ad-hoc-Skripten (z. B. im `.todos/`-Ordner) ist verboten." Analog zur bereits in diesem Step
   vollzogenen Konsistenzpflege von §3 (Verweis auf `AGENTS.md` statt Doppelpflege).

## Sonstige Beobachtungen / MINOR / NITPICK

- `McpHandshakeToolRegistrationTests` prüft nur zwei Tool-Namen statt der vollständigen 18er-Liste
  des Legacy-Pendants — für den MSE-Zweck ausreichend (Plan verlangt keine Vollständigkeitsprüfung),
  vom Coder selbst transparent als „Bekannte Unschärfe" dokumentiert. Kein Fix nötig.
- `LegacyProjectBuildGateTests` wird ab `pending == 0` stillschweigend wirkungslos (früher Return) —
  wie im Plan vorgesehen, vom Coder selbst als für EPIC-3+ relevant dokumentiert. Kein Fix in diesem
  Step nötig.
