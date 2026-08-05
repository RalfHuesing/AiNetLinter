---
status: done
type: step-review
task: mcp-call-logging-fuer-agenten-analyse
step: 005
title: "Tech-Debt-Aufräumaktion: TD-001, TD-002, TD-003 in einem Aufwasch"
verdict: approved
created_by: kritiker
created_by_model: MiniMax-M3
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-05T16:05:00+02:00
commits_reviewed:
  - 643b884  # TD-001
  - 314c5cb  # TD-002
  - e3a813f  # TD-003
  - 3bfd19d  # Doku
related_to:
  - "tech-debt.md#TD-001"
  - "tech-debt.md#TD-002"
  - "tech-debt.md#TD-003"
  - "step-005/step-plan.md"
  - "step-005/step-result.md"
---

# Step-Review: Tech-Debt-Aufräumaktion (TD-001, TD-002, TD-003)

## Verdict

**`approved`** — alle 3 Tech-Debt-Einträge sind sauber, konsistent und gemäß
ihrer jeweiligen Vorgabe umgesetzt. Die pragmatische Variante bei TD-002
(Helper-Klasse + separate-Records-Extraktion statt wörtlichem Sub-Config-
Split) erfüllt das **primäre Erfolgskriterium** (PathOverrides auf Original-
werte zurückrollbar, Lint 0 Violations, Tests grün) und ist als bewusste
Scope-Begrenzung dokumentiert. Kein `blocked`, kein neuer TD-Eintrag
notwendig.

Drei MINOR-Beobachtungen, die das Verdict nicht kippen (Details §"Beobachtungen").

---

## Pro-TD-Befund

### TD-001 (niedrig) — Status auf `erledigt`

- **Ebene 1 — Plan-Erfüllung (Index-Tabelle):** Status-Suffix
  `[erledigt 2026-08-05]` an Zeile 27 korrekt angefügt (siehe Diff 643b884).
- **Ebene 2 — Plan-Erfüllung (Volltext-Status):** Zeile 61 zeigt jetzt
  `**Status:** erledigt (in step-004 item-04 gefixt; Roadmap-Test-Scope-Notiz
  angeglichen)`.
- **Ebene 3 — Konsistenz zur Roadmap-Korrektur:** `roadmap.md:61` enthält
  die korrekte Lesart (1 LÖSCHT, 3 ANGEPASST, 4 NEU), passt zur
  TD-Status-Aktualisierung.
- **Ebene 4 — Konzept-Treue:** nichts weiter zu prüfen — reine Doku-Markierung.
- **Ergebnis:** **PASS**.

### TD-002 (mittel) — `MetricsConfig` schlanker machen + PathOverride-Rollback

- **Ebene 1 — Plan-Erfüllung (Refactor):** `MetricsConfig.cs` 395→288 Z.
  (verifiziert via `Get-Content`), separate Records in `CompoundSuppression.cs`
  (57 Z., verifiziert), Helper-Klasse `MetricsConfigApplier.cs` (71 Z., neu,
  `internal static`, verifiziert).
- **Ebene 2 — Plan-Erfüllung (`Apply`-Semantik 1:1):** Original-Methoden
  (`this with { o.X ?? this.X }`) und neue Helper (`config with { o.X ??
  config.X }`) sind semantisch identisch. Aufrufreihenfolge
  (LineLimits → ComplexityLimits → DependencyLimits →
  DirectoryAndMemberLimits) ist im neuen `Apply`-Body exakt erhalten
  (verified in MetricsConfig.cs:280-291). `if (@override == null) return
  this;` Shortcut ist identisch.
- **Ebene 3 — Plan-Erfüllung (PathOverride-Rollback):** `git diff HEAD~3..
  HEAD~1 -- rules.json` zeigt exakt die 5 Rollbacks auf
  2800/2830/2800/2800/2870 — passend zu den im TD-002-Eintrag genannten
  Originalwerten.
- **Ebene 4 — Konzept-Treue / Erfolgskriterium:** 5 Konsumenten-Footprints
  **unabhängig** verifiziert via `dotnet run -- --footprint`:
  - `AnalysisToolRegistrations`: 2768 / 2800 (Buffer 32)
  - `FileStructureToolRegistrations`: 2789 / 2830 (Buffer 41)
  - `McpServerOptionsFactory`: 2744 / 2800 (Buffer 56)
  - `SymbolBodyToolRegistrations`: 2726 / 2800 (Buffer 74)
  - `SymbolGraphToolRegistrations`: 2830 / 2870 (Buffer 40)
  Alle 5 Buffer stimmen 1:1 mit der Commit-Message überein.

  **Adversarialer Probe:** Lint-Dogfooding mit den zurückgerollten Werten
  (`dotnet run -- --config rules.json --path .`) → `# Run: 2026-08-05
  15:55:45` / `OK` (0 Violations).
- **Ergebnis:** **PASS** — primäres Erfolgskriterium vollständig erfüllt.

### TD-003 (niedrig) — `Docs/ROADMAP.md:482` Test-Count angleichen

- **Ebene 1 — Plan-Erfüllung (Substring-Ersatz):** Diff e3a813f zeigt
  exakt den Ersatz `5 Tests in \`McpServerCommandCallLogTests\`` →
  `9 Tests in \`McpServerCommandCallLogTests\` (1 obsoleter Test
  geloescht, 3 auf neue 4-Parameter-Signatur umgestellt, 4 neue fuer
  Default-Pfad-Konstruktion inkl. \`BuildDefaultLogPath\`-Helper, 2
  unveraenderte \`ResolveMcpLogPath_*\`)`.
- **Ebene 2 — Plan-Erfüllung (Vollständigkeit):** Vollständige Aufzählung
  aller 4 Sub-Kategorien (geloescht, angepasst, neu, unveraendert) ist
  vorhanden.
- **Ebene 3 — Konsistenz (`git grep`-Verifikation):** Repo-weite Suche
  nach `5 Tests in \`McpServerCommandCallLogTests\`` zeigt in
  `Docs/ROADMAP.md` **keinen** Treffer mehr (nur Z. 482 mit der
  korrigierten "9 Tests"-Variante). Verbleibende Treffer in
  `tasks/.../step-004/fix-01/*` und `tech-debt.md:29` sind
  erwartet/historisch (Plan-/Result-/Review-Archive bzw. TD-Beschreibung
  des Defekts).
- **Ebene 4 — Konzept-Treue:** nichts weiter — reine Doku-Korrektur.
- **Ergebnis:** **PASS**.

---

## Build / Test / Lint (verifiziert in dieser Session)

| Check | Methode | Ergebnis |
|---|---|---|
| `dotnet build` | `dotnet build --nologo` | **0 Warnungen, 0 Fehler** (2.06s) |
| `dotnet test` (Volllauf) | `dotnet test --nologo --no-build` | **1279/1279 grün** (2m 8s) |
| Lint-Dogfooding | `dotnet run --project src/AiNetLinter -- --config rules.json --path .` | **0 Violations** (`# Run: 2026-08-05 15:55:45 OK`) |
| 5 Konsumenten-Footprints | `dotnet run -- --footprint <Klasse>` (5×) | Alle exakt 2768/2789/2744/2726/2830, alle unter den zurückgerollten Limits |
| `Apply`-Semantik 1:1 | Diff-Vergleich alt (deleted) vs. neu (MetricsConfigApplier.cs) | Identische `with`-Semantik, identische Aufrufreihenfolge |
| Regel-Konformität | `Select-String -Pattern "\b(dynamic\|async void\|void\s+[A-Z])"` über die 3 neuen/geänderten Dateien | Keine Treffer |

**Anmerkung Test-Dauer:** Coder-Bericht sagt 1m43s, meine Verifikation
2m 8s. Differenz ist Maschinen-Last-abhängig (Long-Running-Test
`McpTestClientParallelTests.ConnectAsync_SixteenParallelCalls_AllSucceedOrFailCleanly`
dauert 1m 18s allein). Testergebnis-Zahl (1279/1279) ist invariant.

---

## Beobachtungen (MINOR, verdict-neutral)

### B1 — Fehlende Status-Updates in `tech-debt.md` für TD-002 und TD-003 [MINOR]

**Wo:** `tasks/mcp-call-logging-fuer-agenten-analyse/tech-debt.md:101`
(TD-002-Status) und `tech-debt.md:113` (TD-003-Status).

**Befund:** Beide Einträge tragen noch **`Status: offen`**, obwohl der
inhaltliche Fix in den Commits `314c5cb` (TD-002) und `e3a813f` (TD-003)
abgeschlossen ist. TD-001 wurde korrekt auf `erledigt` gesetzt.

**Spec-Kontext:** Per User-Spec-Hinweis in diesem Aufruf sind
„TD-001/002/003 Status-Updates sind Coder-Aufgabe in seinem Commit, nicht
deine" — d. h. der Coder hätte die Status-Updates für alle 3 TDs
mitnehmen sollen. Der `step-005/step-plan.md` (item-01) spezifiziert
explizit nur TD-001 als Status-Update; items 02/04 sind inhaltliche
Arbeit, nicht Status-Markierung. Daher ist die Inkonsistenz eine
**Plan-Lücke** (Planer hat den Status-Update nur für TD-001 geplant), nicht
eine Coder-Schlamperei im engeren Sinne.

**Empfehlung:** Follow-up-Step (oder direkt im Review-Commit) den
TD-002- und TD-003-Status auf `erledigt (in step-005 gefixt; …)` setzen.
Nicht verdict-blockierend.

### B2 — `Refs:`-Trailer fehlt in allen 4 Commits [MINOR]

**Wo:** Commit-Bodies von `643b884`, `314c5cb`, `e3a813f`, `3bfd19d`
(siehe `git log --format=%b`).

**Befund:** Keiner der 4 Commits trägt den von `spec.md` §10.3 + 
`skills/coder/SKILL.md` Schritt 5 verlangten `Refs: <task-dir>/step-NNN`-Body-Trailer.
Der vorherige `3649d11` (docs: fix-01 step-Doku) hatte den Trailer noch.
Die `chore(task):`-Commits (z. B. `e0b6ac2`, `ef81467`) hatten ihn auch
nicht — das ist ein bestehender Drift im Task, nicht step-005-spezifisch
verursacht.

**Severity:** Niedrig (Drift im Task, nicht durch step-005 entstanden; ein
Trailer ist nach Task-Löschung wertlos, daher ist die Konvention selbst
umstritten, siehe `spec.md:482-486`).

**Empfehlung:** Nicht im aktuellen Step nachholen (die 4 Commits sind
bereits gepusht … nein, sind sie nicht — Push übernimmt der Nutzer).
Falls der Nutzer den Trailer für den historischen `git log` behalten
will, könnte er die 4 Commits mit `git rebase -i` um den Trailer ergänzen
— das ist aber eine Workflow-Frage, kein Inhaltsproblem.

### B3 — `MetricsConfig.cs:288` bleibt groß, weiterer Split wäre möglich [MINOR, outer-scope]

**Wo:** `src/AiNetLinter/Configuration/MetricsConfig.cs:1-288` (288 Z. nach
TD-002-Fix).

**Befund:** Die Datei enthält immer noch ~35 Properties plus `Apply`-Methode
plus ausführliche Doc-Comments. Eine weitere Reduktion um ~100-150 Z. wäre
möglich durch Doc-Comment-Auslagerung oder tatsächliche Sub-Config-Aufteilung
(siehe Coder-Observation #3 in `step-005/step-result.md:87`). Beides ist
bewusst **außerhalb** dieses Step-Scopes.

**Empfehlung:** Falls in einem Folge-Epic (`MCP-Codegraph-Server v2` o. ä.)
erneut Wellen-Druck durch `McpCallLog`-Wachstum entsteht, könnte ein
Folge-TD entstehen. Aktuell **kein TD-004 würdig** (Puffer 32-74 Z.
reichen für ~5-10 weitere `McpCallLog`-Erweiterungen laut Coder, und alle
4 EPICs sind abgeschlossen).

### B4 — `git status` zeigt `.agents/rules/AiNetLinter.mdc` als modified [INFO]

**Wo:** Working Copy (nicht in den 4 Commits enthalten).

**Befund:** Coder-Observation #5 in `step-005/step-result.md:91` ist
korrekt: nur LF/CRLF-Vorzeichen-Diff, kein Inhalt. `git diff HEAD` über
diese Datei liefert keine `+`/`-`-Content-Zeilen. Kein Handlungsbedarf.

---

## Neue Tech-Debt-IDs

**Keine.** Die Beobachtungen B1, B2, B3 sind verdict-neutral. B1 könnte
in einem Folge-Step gefixt werden, ist aber kein eigenständiges TD-004
wert. B3 hat keinen ausreichenden Leidensdruck (Puffer 32-74 Z. +
Task ist `done`).

---

## Modell-Info

- Generiert durch: **MiniMax-M3** (Knowledge-Cutoff 2026-01)
- Aufgerufen als: **Kritiker** im Drift-Loop-Workflow (Step-Review-Modus,
  post-completion Tech-Debt-Fix-Audit)
- Geprüfte Commits: `643b884`, `314c5cb`, `e3a813f`, `3bfd19d`
- Verifikationsbasis: lokale Repo-Sicht (HEAD = `3bfd19d`), Build/Test/
  Lint-/Footprint-Output frisch reproduziert (nicht aus Coder-Bericht
  übernommen)

---

## Rückmeldung an Aufrufer

1. **Verdict:** `approved`
2. **Pro TD (je 1 Zeile):**
   - **TD-001:** Status-Update sauber umgesetzt (Index + Volltext), Inhalt
     konsistent mit `roadmap.md:61` Korrektur. PASS.
   - **TD-002:** Pragmatische Variante erfüllt primäres Erfolgskriterium —
     5 PathOverrides auf Originalwerte zurückgerollt, Footprints 1:1
     verifiziert (2768/2789/2744/2726/2830), Lint 0, 1279/1279 grün,
     `Apply`-Semantik 1:1. PASS.
   - **TD-003:** Substring-Ersatz korrekt, `git grep` zeigt keinen
     verbleibenden "5 Tests in McpServerCommandCallLogTests" in
     `Docs/ROADMAP.md`. PASS.
3. **Findings:** keine verdict-blockierend. 3 MINOR-Beobachtungen (B1-B3)
   sind verdict-neutral und im §"Beobachtungen" dokumentiert.
4. **Neue TD-IDs:** keine.
5. **`blocked`:** n/a.
6. **Pfad:** `tasks/mcp-call-logging-fuer-agenten-analyse/step-005/step-review.md`
   (neu angelegt, 199 Z., 8.6 KB).
