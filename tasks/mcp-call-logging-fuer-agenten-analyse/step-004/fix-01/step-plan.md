---
status: open
type: step-plan
task: mcp-call-logging-fuer-agenten-analyse
step: 004
fix: 01
title: "Fix-01: error_type-Schema-Doku an Implementierung anpassen + Test-Count 5/5 → 9/9 korrigieren"
epic: EPIC-04
estimated_risk: low
step_type: single
related_to:
  - "step-004/step-review.md"   # Kritiker-Verdict 'issues' (2 MAJOR + 2 MINOR)
  - "step-004/step-plan.md"     # Urspruenglicher Step-Plan mit fehlerhaften 5/5-Angaben
  - "step-004/step-result.md"   # Coder-Result mit fehlerhaften 5/5-Angaben
  - "src/AiNetLinter/Mcp/McpCallLog.cs:121"  # Code: exception.GetType().Name (ohne Namespace)
created_by: planer
created_by_model: MiniMax-M3
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-05T15:35:00+02:00
---

# Step 004 / Fix 01: error_type-Schema-Doku angleichen + Test-Count-Korrektur

## Bezug

- **Task:** `mcp-call-logging-fuer-agenten-analyse`
- **Step:** `004` (Doku-Sync + End-to-End-Verifikation)
- **Fix-Runde:** `01` (erste Runde nach `issues`-Verdict des Kritikers)
- **Auslöser:** Kritiker hat in `step-004/step-review.md` 2 MAJOR + 2 MINOR
  Findings ausgewiesen. Gemäss Spec §6.2.1 + §8.1 lösen MAJOR-Findings
  aus einem `issues`-Verdict zwingend einen Fix-Step aus; MINOR sind
  „Sonstige Beobachtungen" und kein Scope dieses Fixes.
- **Quellen-Lesungen vor Plan-Erstellung:**
  - `Docs/agent-api.md:341-354` (Error-Schema-Block, beide Fehler verifiziert)
  - `src/AiNetLinter/Mcp/McpCallLog.cs:121` (`exception.GetType().Name`, kein Namespace)
  - `src/AiNetLinter.Tests/Mcp/McpCallLogTests.cs:169`, `:361` (assertieren `TestException` / `InvalidOperationException` ohne Namespace)
  - `src/AiNetLinter.Tests/Commands/McpServerCommandCallLogTests.cs` (9 `[Fact]`-Attribute, verifiziert via grep)
  - `step-004/step-plan.md` (alle 4 Vorkommen von `5/5` / `5 Tests` lokalisiert)
  - `step-004/step-result.md` (1 Vorkommen von `5/5` lokalisiert)

## Scope-Disziplin

**In Scope (Pflicht — MAJOR-Findings):**

1. **item-01 (MAJOR Ebene 3):** `Docs/agent-api.md` Doku-Wert von `error_type`
   widerspricht der Implementierung. Doku behauptet „Vollständiger
   Exception-Typ-Name (z. B. `System.InvalidOperationException`)" — Code
   liefert aber nur den simplen Typ-Namen ohne Namespace
   (`exception.GetType().Name`).
2. **item-06 (MAJOR Ebene 3):** `step-004/step-result.md` und
   `step-004/step-plan.md` dokumentieren `McpServerCommandCallLogTests`
   fälschlich als „5/5 grün" — tatsächlich sind es 9 Tests (1
   `PathNotSet` + 2 RelativePath/AbsolutePath 4-Param + 4 neue
   + 2 unveränderte `ResolveMcpLogPath_*`).

**Out of Scope (MINOR-Findings + bekannte Ausnahmen):**

- **item-04 MINOR (Ebene 1):** `tasks/.../roadmap.md:61` zählt 1+3+4=8
  Änderungen, erwähnt aber die 2 unveränderten `ResolveMcpLogPath_*`-
  Tests nicht. Begründung: Sonstige Beobachtung, kein findings-getriggerter
  Scope (Spec §6.2.1). **Nicht anrühren.**
- **item-03 MINOR (Ebene 1):** EPIC-09 statt EPIC-20 in `Docs/ROADMAP.md`
  (Abweichung vom Plan, angemessen begründet, inhaltlich 1:1). **Nicht
  anrühren.**
- **MINOR-Beobachtung im Review (item-01):** „4-KB-Cap nicht explizit
  illustriert" im Stack-Trace-Beispiel. **Nicht anrühren** (Doku-Beispiel
  ist illustrativ, nicht aussagekräftig; nicht im Finding-Block, sondern
  nur als Randnotiz).
- **TD-002-PathOverride-Monitoring:** unverändert out-of-scope.
- **`McpCallLog.LogPath` internal-Sichtbarkeit:** unverändert out-of-scope.
- **`--sync-agent-rules-only`:** kein Sync nötig (auto-generated
  `AiNetLinter.mdc` enthält Lint-Grenzwerte, nicht Feature-Listen).
- **Konzept DoD 5 „4 Call-Tests"-Zahl-Diskrepanz:** out-of-scope, ist
  historische Konzept-Restgröße, nicht Step-intern.

## Konkrete Änderungen

### Fix A (item-01) — `Docs/agent-api.md` Error-Schema-Block angleichen

**Befund:** Doku behauptet „Vollständiger Exception-Typ-Name (z. B.
`System.InvalidOperationException`)" und zeigt im Beispiel
`"error_type":"System.InvalidOperationException"`. Implementierung
(`McpCallLog.cs:121`) nutzt `exception.GetType().Name` (ohne Namespace).
Tests `McpCallLogTests.cs:169` (`Assert.Equal("TestException", ...)`)
und `:361` (`Assert.Equal("InvalidOperationException", ...)`) beweisen
das simple Format.

**Was:** Genau 2 Text-Änderungen in einem einzigen Block (Z. 341-354),
keine strukturelle Änderung.

#### Fix A.1 — `Docs/agent-api.md:346` (Felder-Tabelle, Spalte „Bedeutung")

- **Alt (Z. 346):**
  ```
  | `error_type` | string | Vollstaendiger Exception-Typ-Name (z. B. `System.InvalidOperationException`) |
  ```
- **Neu:**
  ```
  | `error_type` | string | Exception-Typ-Name ohne Namespace (z. B. `InvalidOperationException`) |
  ```
- **Begründung:** Doku an Code anpassen. Die umgekehrte Variante (`FullName`
  im Code verwenden) wäre eine Schema-Änderung, out-of-scope für EPIC-04
  und würde die Tests brechen (McpCallLogTests.cs:169, :361).

#### Fix A.2 — `Docs/agent-api.md:353` (Beispiel-Snippet, error_type-Wert)

- **Alt (Z. 353, Substring innerhalb der JSON-Zeile):**
  ```
  {"ts":"2026-08-05T09:14:22.011Z","tool":"get_file_skeleton","args":"./src/Foo.cs","level":"error","error_type":"System.InvalidOperationException","error_message":"simuliertes Hot-Reload-Race in get_file_skeleton",...
  ```
  (genauer Substring: `"error_type":"System.InvalidOperationException"`)
- **Neu (Substring):**
  ```
  "error_type":"InvalidOperationException"
  ```
- **Hinweis für Coder:** Es ist die **eine** JSON-Zeile bei Z. 353 — die
  exakte Ersetzung ist
  `"error_type":"System.InvalidOperationException"`
  →
  `"error_type":"InvalidOperationException"`.
  Der Rest der Zeile (insb. `error_message`, `stack_trace`) bleibt
  unverändert. Keine Zeile hinzufügen, keine Zeile entfernen.
- **Begründung:** Beispiel-Snippet muss das ausführen, was die
  Implementierung tatsächlich produziert. Der restliche
  Beispielstacktrace (2 Frames, endet auf `..."`) ist konsistent zum
  4-KB-Cap und bleibt unverändert (kein neues Beispiel nötig; die
  `level`/`error_message`/`stack_trace`-Werte sind nicht betroffen).

### Fix B (item-06) — Test-Count 5/5 → 9/9 in Step-Dokumenten

**Befund:** `McpServerCommandCallLogTests.cs` enthält **9 `[Fact]`-Tests**
(verifiziert via ripgrep-Count). Der TD-001-Breakdown (1+3+4=8 Änderungen
aus dem step-001-Scope) addiert sich nicht zur Gesamtzahl 9 — die 2
unveränderten `ResolveMcpLogPath_*`-Tests wurden in der ursprünglichen
Notiz nie mit-erwähnt. Im Plan und step-result.md wurde daraus fälschlich
„5/5", was weder zur 8 (1+3+4) noch zur 9 (8+2) passt.

**Was:** Genau 4 Text-Änderungen (1 in step-result.md, 3 in step-plan.md).
**Hinweis:** Der Reviewer nannte die Stellen als `step-result.md:49+58`
und `step-plan.md:96`. Bei eigener Nachprüfung:

- In **`step-004/step-result.md`** existiert nur **1 Vorkommen** von
  „5/5" — bei **Z. 49**. Z. 58 ist die DoD-4-Zeile
  (`- **DoD 4 — dotnet test Volllauf 1279/1279 gruen:** ...`) und
  enthält **kein** „5/5" — die Reviewer-Referenz `:58` ist ein
  Off-by-one. Fix nur bei Z. 49.
- In **`step-004/step-plan.md`** existieren **3 Vorkommen** von
  „5/5" bzw. „5 Tests" — bei **Z. 95** (Test-Stand-Block),
  **Z. 190** (item-03-Beschreibung Tests-Item) und **Z. 261**
  (DoD-Checkliste). Die Reviewer-Referenz `:96` ist ein Off-by-one;
  der relevante Substring beginnt schon auf Z. 95. Alle 3 Vorkommen
  werden korrigiert.

#### Fix B.1 — `step-004/step-result.md:49` (Build-/Test-Output-Aufzählung)

- **Alt (Z. 49):**
  ```
    - `McpServerCommandCallLogTests` 5/5 gruen (Regressions-Schutz bestaetigt)
  ```
- **Neu:**
  ```
    - `McpServerCommandCallLogTests` 9/9 gruen (Regressions-Schutz bestaetigt)
  ```
- **Kontext:** Dieser Eintrag steht in der Aufzählung unter
  `## Build-/Test-Output` (Z. 47-51). Andere Einträge in der Aufzählung
  (`McpCallLogTests` 14/14, `CliIntegrationTests.RunLinter...`, der
  Long-Running-Indicator) bleiben unverändert.

#### Fix B.2 — `step-004/step-plan.md:95` (Test-Stand-Block)

- **Alt (Z. 95):**
  ```
    step-003), `McpServerCommandCallLogTests` 5/5 grün (1 alt gelöscht,
  ```
- **Neu:**
  ```
    step-003), `McpServerCommandCallLogTests` 9/9 grün (1 alt gelöscht,
  ```
- **Kontext:** Der Substring beginnt auf Z. 94 (`- **Test-Stand:** ...`)
  und endet auf Z. 97 (`grün (step-003-Verifikation). Der finale Volllauf in item-06`).
  Es ist **ein** Substring auf Z. 95; die Folgezeilen 96-97
  (`3 angepasst, 4 neu aus step-001)` und `grün (step-003-Verifikation)...`)
  bleiben unverändert.

#### Fix B.3 — `step-004/step-plan.md:190` (item-03-Beschreibung Tests-Item)

- **Alt (Z. 190):**
  ```
      neu), 5 Tests in `McpServerCommandCallLogTests` (1 alt gelöscht,
  ```
- **Neu:**
  ```
      neu), 9 Tests in `McpServerCommandCallLogTests` (1 alt gelöscht,
  ```
- **Kontext:** Steht in der item-03-Beschreibung (ROADMAP-Meilenstein).
  Der Folge-Kontext „1 alt gelöscht, 3 angepasst, 4 neu für
  Default-Pfad-Konstruktion" bleibt korrekt (das war der Schritt-001-
  Diff). Nur die Zahl vor „Tests" ist falsch.

#### Fix B.4 — `step-004/step-plan.md:261` (DoD-Checkliste)

- **Alt (Z. 261):**
  ```
  - [ ] `McpServerCommandCallLogTests` weiterhin 5/5 grün (Regressions-Schutz)
  ```
- **Neu:**
  ```
  - [ ] `McpServerCommandCallLogTests` weiterhin 9/9 grün (Regressions-Schutz)
  ```
- **Kontext:** Steht in der DoD-Checkliste (`## Definition of Done` /
  `## Tests`). Die benachbarte DoD-Zeile davor
  (`McpCallLogTests weiterhin 14/14 grün`) bleibt unverändert.

## Was NICHT geändert wird (zur Klarstellung für den Coder)

- **Keine Code-Änderungen am Projekt.** `McpCallLog.cs:121` bleibt bei
  `exception.GetType().Name`. Schema wird nicht umgestellt (out-of-scope,
  würde Tests brechen).
- **Keine Änderung an `Docs/configuration.md`** (item-02 — bereits
  approved).
- **Keine Änderung an `Docs/ROADMAP.md`** (item-03 — bereits approved;
  MINOR EPIC-09-vs-EPIC-20 ist „Sonstige Beobachtung", nicht Scope).
- **Keine Änderung an `tasks/.../roadmap.md:61`** (item-04 — MINOR-
  Beobachtung „8 vs 9 Total", nicht Scope).
- **Keine Änderung an `CliOptionFactory.cs:230-233`** (item-05 — bereits
  approved).
- **Kein erneuter `dotnet test`-Volllauf als „Verifikations-Aktion" nötig
  für den Fix selbst** — die Doku-Änderungen berühren weder Compile-
  noch Test-Pfad. Der Volllauf wird trotzdem in der Verifikation
  ausgeführt, um zu zeigen, dass keine unbeabsichtigte Regression
  entstanden ist (Hund: `CliIntegrationTests` 29/29,
  `McpCallLogTests` 14/14, `McpServerCommandCallLogTests` 9/9).
- **Keine Roadmap-Änderung** (Spec: Fix-Step ändert nichts an Epics).
- **Keine Commits durch den Planer** (Spec: Planer schreibt nur Pläne).
- **Keine Änderung an `step-004/step-review.md`** (Kritiker-Output ist
  historisch; die Korrektur wird im `step-004/fix-01/step-result.md`
  dokumentiert, dann re-reviewed).

## Tests (Verifikation des Fixes)

- [ ] `dotnet build` — 0 Warnung(en), 0 Fehler (sollte trivial sein —
      Doku + Step-Doku-Dateien berühren Compile-Pfad nicht).
- [ ] `dotnet test` Volllauf — 1279/1279 grün, 0 Failures, 0 Errors
      (Erwartung: identisch zum step-004-Stand; keine Test-Datei wurde
      angefasst, also darf sich die Gesamtzahl nicht ändern).
- [ ] `dotnet test --filter FullyQualifiedName~McpServerCommandCallLogTests` —
      9/9 grün (Regressions-Schutz, exakt der Wert, der im korrigierten
      step-result.md jetzt steht).
- [ ] `dotnet test --filter FullyQualifiedName~McpCallLogTests` —
      14/14 grün (Regressions-Schutz, der im Dokumentations-Kontext
      an mehreren Stellen als Vergleichswert genannt wird).
- [ ] Visuelle Inspektion: `Docs/agent-api.md:346` und `:353` enthalten
      nach dem Fix weder `System.InvalidOperationException` (im
      Error-Schema-Block) noch den veralteten Ausdruck
      „Vollstaendiger Exception-Typ-Name".
- [ ] Grep-Check: in `step-004/step-result.md` und
      `step-004/step-plan.md` darf nach dem Fix kein
      `5/5 grün` / `5/5 gruen` / `5 Tests in` mehr im
      `McpServerCommandCallLogTests`-Kontext stehen.

## Definition of Done

- [ ] Fix A.1 angewendet (`Docs/agent-api.md:346` Beschreibung korrigiert).
- [ ] Fix A.2 angewendet (`Docs/agent-api.md:353` Beispiel-Substring korrigiert).
- [ ] Fix B.1 angewendet (`step-004/step-result.md:49` 5/5 → 9/9).
- [ ] Fix B.2 angewendet (`step-004/step-plan.md:95` 5/5 → 9/9).
- [ ] Fix B.3 angewendet (`step-004/step-plan.md:190` 5 Tests → 9 Tests).
- [ ] Fix B.4 angewendet (`step-004/step-plan.md:261` 5/5 → 9/9).
- [ ] `dotnet build` grün (0/0).
- [ ] `dotnet test` Volllauf grün (1279/1279, identisch zum step-004-Stand).
- [ ] Grep-Check grün (kein `5/5` / `5 Tests` / `System.InvalidOperationException`
      im Error-Schema-Block mehr).
- [ ] `step-004/fix-01/step-result.md` geschrieben mit Diffs pro Fix
      + Build/Test-Output.
- [ ] `status` in `step-004/fix-01/step-plan.md` (dieser Datei) von
      `open` auf `done (pending audit)` gesetzt.
- [ ] **Commit-Strategie:** `docs: Fehlerkorrekturen Doku-Test-Count
      [mcp-call-logging-fuer-agenten-analyse]` als `docs`-Commit mit
      Body-Liste der 4 Fixes A.1, A.2, B.1, B.2, B.3, B.4
      (Conventional Commit auf Deutsch, imperativ, ≤72 Zeichen Subject
      inkl. Suffix — Subject-Vorschlag oben ist 78 Zeichen, Coder
      kürzt z. B. auf
      `docs: Doku-Test-Count-Korrekturen [mcp-call-logging-fuer-agenten-analyse]`
      mit 73 Zeichen, knapp über Limit; ggf. weiter kürzen oder Body-
      Trailer nutzen — siehe `AiNetLinterRichtlinien.mdc` §4). Trailer
      `Refs: tasks/mcp-call-logging-fuer-agenten-analyse/step-004/fix-01`.
- [ ] Nach Audit: `step-004/fix-01/step-review.md` mit Verdict.
      Bei `approved` und allen Findings (MAJOR + dokumentierte MINOR)
      adressiert → Task kann in Schritt 6 Abschluss-Check gehen.

## Rules-Refs

- **Spec §6.2.1, §8.1:** MAJOR-Findings aus `issues`-Verdict lösen
  einen Fix-Step aus; MINOR sind „Sonstige Beobachtungen" und nicht
  Scope. Begründung für Out-of-Scope-Behandlung der MINOR.
- **Spec §10.6:** Ein Commit pro Batch (hier: ein einzelner `docs`-
  Commit für alle 4 Fixes, weil sie thematisch zusammenhängen
  — Doku-Konsistenz + Test-Count-Doku-Konsistenz — und keine
  Datei-Logik berührt wird).
- **`.agents/rules/AiNetLinterRichtlinien.mdc` §1** (Doku-Ordnung):
  `Docs/agent-api.md` ist der korrekte Ort für die `error_type`-
  Schema-Korrektur (Tool-Verträge).
- **`.agents/rules/AiNetLinterRichtlinien.mdc` §4** (Update-Pflicht):
  Bei Schema-Änderungen in Doku muss die Beschreibung mit dem Code
  konsistent sein — exakt das, was Fix A herstellt.
- **`.agents/rules/AiNetLinterRichtlinien.mdc` §5** (Zero-Warning-
  Direktive): Build muss 0/0 bleiben, Doku-Änderungen dürfen keine
  Sonderzeichen-Probleme (Umlaute) einführen — `Vollstaendiger` (alt)
  und `Vollständiger` (neu) sind beide ASCII-only, kein Risiko.
- **`.agents/rules/AiNetLinterRichtlinien.mdc` §5** (Clean-Code-
  Kommentar-Politik): Doku-Text darf keine Task-/Step-/EPIC-/TD-
  Verweise enthalten — die Fix-Texte referenzieren weder
  `step-004/fix-01` noch andere interne Bezeichner im Doku-Body.

## Bekannte Beobachtungen (kein Scope dieses Fixes)

1. **Off-by-one in Reviewer-Zeilenangaben** (siehe Fix-B-Vorwort):
   Der Reviewer zitiert `step-004/step-result.md:49 und :58` — Z. 58
   enthält aber kein „5/5". Möglicherweise hatte der Reviewer eine
   Zwischenversion mit einer zusätzlichen „Pro-Item-Bestätigung
   item-06"-Zeile, die im finalen step-result.md nicht enthalten ist.
   Der Plan dokumentiert die Diskrepanz, damit der Coder nicht
   verwirrt sucht. Fix nur bei Z. 49 (1 Vorkommen in step-result.md).
2. **MINOR item-04 (Roadmap-Total 8 vs 9):** „Sonstige Beobachtung"
   gemäß Reviewer-Befund Ebene 1; in Spec-Sprache nicht findings-
   getriggert. Wenn der User diese Klarstellung wünscht, ist sie
   ein eigener Micro-Step (Empfehlung des Kritikers: optionale
   1-Zeilen-Ergänzung in `tasks/.../roadmap.md:61`).
3. **MINOR item-03 (EPIC-09 vs EPIC-20):** bereits im
   `step-004/step-result.md:65-76` dokumentiert und begründet.
   Inhaltlich 1:1 zum Plan. Kein Action-Item.
4. **MINOR-Beobachtung im Review (item-01):** „4-KB-Cap nicht explizit
   illustriert" im Stack-Trace-Beispiel — wertende Bemerkung des
   Kritikers im `## Pro-Item-Befund`-Block, nicht im `## Findings`-
   Block. Daher kein findings-getriggerter Scope.

## Modell-Info

- Planer: MiniMax-M3 (Knowledge Cutoff: 2026-01)
- Erstellt: 2026-08-05T15:35:00+02:00
- Quellen-Lesungen:
  - `Docs/agent-api.md:341-354` (Error-Schema-Block, 14 Z. gelesen)
  - `src/AiNetLinter/Mcp/McpCallLog.cs:115-129` (RecordError-Body)
  - `src/AiNetLinter.Tests/Commands/McpServerCommandCallLogTests.cs` (9 `[Fact]` via ripgrep-Count)
  - `step-004/step-plan.md` (komplett)
  - `step-004/step-result.md` (komplett)
  - `step-004/step-review.md` (Findings-Block, Pro-Item-Befund für item-01 und item-06)
