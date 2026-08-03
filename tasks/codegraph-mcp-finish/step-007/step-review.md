---
status: done
type: step-review
task: codegraph-mcp-finish
step: 007
epic: EPIC-02
step_type: single
reviewed_by: kritiker
reviewed_by_model: claude-sonnet-5
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-03
verdict: issues
tech_debt_ids: []
---

# Review Step 007: Einheit-011-Abschluss — Verifikation + nachgeholtes Review der 6 Commits

## Verdict

- [ ] **approved** — alle vier Prüfebenen ok
- [x] **issues** — Fix-Step `step-007/fix-01` angelegt mit Fix-Plan
- [ ] **blocked** — Nutzer-Entscheidung nötig (siehe Frage unten)

**Wichtiger Hinweis zur Konsequenz:** Der Fund betrifft ausschließlich die
6 bereits gepushten Einheit-011-Commits (`4bcd5ab`, `075a8a0`, `af41a6b`,
`1201840`, `a530b4f`, `8a663c7`), nicht die Coder-Verifikationsschritte
dieses Steps selbst (die sind alle sauber, siehe unten). Der Push ist
bereits erfolgt und nicht rückgängig zu machen — der resultierende
Fix-Step korrigiert die bereits gepushte Code-Basis nachträglich, ganz
regulärer Workflow trotz des ungewöhnlichen zeitlichen Ablaufs.

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `<rules_dir>/**` eingehalten — **mit Ausnahme des Findings unten**
- [x] Logische Korrektheit: Code macht was er soll, nicht nur „grün"
- [x] Konzept-Treue: passt die Umsetzung zu `konzept.md` (Scope, Non-Goals, Muss-Haben)
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft anhand `step-result.md`-Dokumentation (1186/1186), Build zusätzlich selbst frisch gefahren

## Befund

### Plan-Erfüllung

Alle drei Verifikationsschritte des `step-007`-Plans wurden erfüllt:
Prozess-Check (keine offenen `AiNetLinter.exe`/`testhost.exe`), frischer
Build (0 Warnungen, selbst nachgeprüft: `dotnet build AiNetLinter.slnx`
→ grün, 0 Warnungen, 1.3s), frischer Volllauf (1186 Tests, 0 Fehler,
laut `step-result.md` dokumentiert), Review-Grundlage für die 6 Commits
zusammengestellt. Kein Push (korrekt, wie gefordert). `PathOverride`-Stand
14 (statt 13 in `Konzept.md`) korrekt gegengeprüft: `grep -c
"MaxAIContextFootprint\": 2700" rules.json` = 14, verifiziert.

### Rules-Konformität

Das eigentliche inhaltliche Review betrifft die 6 Einheit-011-Commits
(nicht den Doku-Commit `7b3f193` von step-007 selbst). Dabei ein
bestätigter Verstoß gegen `AiNetLinterRichtlinien.mdc` §5 — siehe
Finding 1 unten. Alle übrigen Rules (`MaxConstructorDependencies`,
`AIContextFootprint`, `EnforceSealedClasses`, `EnforceNullableEnable`,
`BanBlockingTaskAccess`, Zero-Warning-Direktive) werden eingehalten —
verifiziert per Diff-Lektüre und frischem Build.

### Logische Korrektheit

- **(a) Konstruktor-Migration:** `McpCodeGraphServer`-Konstruktor nimmt
  jetzt exakt 1 Parameter (`McpCodeGraphServerOptions`). Alle Call-Sites
  im aktuellen Repo-Stand verifiziert konsistent migriert (`grep -rn "new
  McpCodeGraphServer(" src/` liefert nur noch den bewusst getesteten
  `null!`-Fall in `McpCodeGraphServerConstructorTests.cs`). Migration ist
  1:1, `consoleOverride` korrekt entfernt (kein Call-Site nutzte ihn,
  laut Commit-Kommentar verifiziert plausibel — kein Rest-Verweis
  gefunden). Strukturelle Absicherung per
  `McpCodeGraphServerConstructorTests.cs` (Reflection auf Konstruktor-
  Signatur + `ArgumentNullException`-Test). Sauber.
- **(b) Builder/Factory-Aufteilung:** `McpServerOptionsFactory.Create()`
  delegiert jetzt vollständig an `McpServerOptionsBuilder` (Fluent-API,
  `WithServerVersion`/`WithServerInstructions`/`WithToolCollection`/
  `Build()`), die Factory selbst enthält nur noch Const-String +
  Tool-Collection-Aufbau. Das entspricht dem Geist des „dünner
  Dispatch"-Gegenmusters aus `Konzept.md` TD-005 (dort zwar für
  Tool-`ExecuteAsync`-Signaturen formuliert, das Prinzip — Verantwortung
  aus der footprint-kritischen Klasse auslagern statt inline wachsen
  lassen — ist hier sinngemäß richtig angewendet). Sauber.
- **(c) Retry-Logik in `McpTestClient`:** Die Retry-Schleife in
  `ConnectAsync` fängt in der `when`-Klausel sehr breit (`ex is not
  OperationCanceledException || !cancellationToken.IsCancellationRequested`)
  — das bedeutet, auch ein permanenter Fehler (z. B. die
  `FileNotFoundException`, falls `AiNetLinter.exe` grundsätzlich fehlt)
  würde bis zu `MaxRetries`-mal wiederholt, bevor er als
  `InvalidOperationException` mit `lastException` als `InnerException`
  weitergereicht wird. Das ist **keine verdeckte Symptombekämpfung** im
  Sinne der Regel (kein Test wird stillschweigend grün gemacht, keine
  Assertion abgeschwächt — bei endgültigem Scheitern wird weiterhin eine
  aussagekräftige Exception geworfen, inkl. Ursache), sondern eine bewusst
  breite, aber am Ende transparente Absicherung gegen einen dokumentierten
  Flake. Einzige Nebenwirkung: bei einem echten Konfigurationsfehler
  verzögert sich die sichtbare Fehlermeldung um bis zu ~3.5s (Default)
  bzw. ~31s (Fixture-Konfiguration mit 5 Retries) statt sofort zu
  scheitern — das ist eine MINOR-Beobachtung, kein Blocker (siehe
  „Sonstige Beobachtungen").
- **(d) `PathOverride`-Erweiterung:** 9 neue Einträge in `rules.json`
  (Commit `8a663c7`), Gesamtstand 14 (5 vorbestehend + 9 neu) —
  verifiziert per Diff und `grep`. Wie in `Konzept.md` Zeile 121-124
  entschieden, wird das als akzeptierte Pragmatik gewertet, nicht
  blockiert. Die Diskrepanz zu den 13 in `Konzept.md` genannten
  ist durch die 14. Zeile (`McpServerOptionsFactory.cs`, neu durch
  Commit `8a663c7` selbst hinzugefügt) erklärt — kein Rätsel, keine
  weitere Klärung nötig.
- **(e) TD-019-Restunschärfe:** Wie in `Konzept.md`/Step-Plan gefordert,
  wird der A3-Nachweis für TD-019 als nicht abschließend (Flake nicht
  deterministisch reproduzierbar, Retry ist Absicherung, kein bewiesener
  Fix) akzeptiert — kein Blocker.

### Konzept-Treue (Ebene 4)

Die 6 Commits decken TD-009, TD-014 und TD-019 wie in `Konzept.md`
„Codiert, aber nicht reviewt" beschrieben ab, Scope passt zur dort
dokumentierten Erwartung. Kein Non-Goal umgesetzt, kein Muss-Haben-Punkt
aus Muss-Haben A fehlt inhaltlich. Der einzige Abzug betrifft nicht den
Scope, sondern eine an anderer Stelle in `Konzept.md`/`AiNetLinterRichtlinien.mdc`
bereits explizit verbotene Praxis, siehe Finding 1.

### Build-/Test-Status

```
dotnet build AiNetLinter.slnx           → grün, 0 Warnungen (selbst nachgeprüft)
dotnet test AiNetLinter.slnx --no-build → grün (1186 Tests, 0 Fehler, laut step-result.md, konsistent mit step-004/005/006-Baseline)
```

## Findings

1. `src/AiNetLinter/Mcp/McpCodeGraphServerOptions.cs:22-24,40-42` (neu in
   Commit `075a8a0`) — [MAJOR] [Rules-Konformität] Der Klassen-XML-Doc-
   Kommentar enthält `Eingefuehrt mit TD-009, weil der vorherige...` sowie
   in der `From()`-Methoden-Doku den wörtlichen Verweis `siehe
   Plan-Abweichung 8 in units/011/plan.md`. Das verstößt gegen
   `AiNetLinterRichtlinien.mdc` §5: „Verboten: Jede Referenz auf
   Task-/Planungsartefakte, die den Code überleben soll — step-008,
   TD-005, EPIC-06, unit 009, Ticket-/Issue-IDs o. Ä. Diese
   Ordner/Dokumente werden nach Task-Abschluss gelöscht; der Verweis wird
   dann bedeutungslos." Der Fall ist bereits eingetreten: `units/011/plan.md`
   existiert nicht mehr (der Quell-Ordner `tasks/codegraph-mcp-server/`
   wurde bereits vor Beginn dieses Tasks gelöscht, siehe `step-plan.md`
   „Aktueller Projektzustand"), der Verweis im Code ist damit bereits
   heute für jeden Leser unauflösbar. **Fix:** XML-Doc-Kommentar auf
   ID-freies *Why* umschreiben, z. B. „Input-Record ersetzt den früheren
   5-Parameter-Konstruktor, der am projektweiten
   `MaxConstructorDependencies: 5`-Limit lag (siehe `AiNetLinter.mdc`).
   `consoleOverride` wurde entfernt, da kein Call-Site ihn nutzte."
   TD-Nummer und Datei-/Ordnerverweise ersatzlos streichen.
2. `src/AiNetLinter/Mcp/McpCodeGraphServer.cs:29` (neu in Commit
   `075a8a0`) — [MAJOR] [Rules-Konformität] Inline-Kommentar über dem
   Konstruktor: „Eingefuehrt mit TD-009: Input-Record ersetzt den
   frueheren 5-Parameter-Konstruktor, der am projektweiten
   `MaxConstructorDependencies: 5`-Limit lag...". Gleicher Regel-Verstoß
   wie Finding 1 (`AiNetLinterRichtlinien.mdc` §5, TD-Referenz im
   Produktionscode). **Fix:** `TD-009` aus dem Kommentar entfernen,
   restlichen *Why*-Inhalt (Limit-Begründung) beibehalten.
3. `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs:11` (neu in Commit
   `4bcd5ab`) — [MAJOR] [Rules-Konformität] Klassen-XML-Doc: „...und
   durch `McpServerOptionsBuilder` in eine schlanke Factory + Builder
   aufgeteilt (TD-014): ...". Gleicher Regel-Verstoß wie Finding 1/2.
   **Fix:** `(TD-014)` ersatzlos streichen, Rest des Satzes bleibt
   inhaltlich korrekt und ID-frei.

**Einordnung als MAJOR statt CRITICAL:** Die Regel selbst benennt den
Verstoß explizit als verboten, betrifft ausschließlich Produktionscode
(3 Fundstellen in `src/AiNetLinter/Mcp/`, nicht in `*.Tests`), bricht
weder Build noch Tests und ist mit überschaubarem Aufwand behebbar (reine
Kommentar-Bereinigung, keine Verhaltensänderung) — daher MAJOR
(„Explizite Rules-Verletzung im Produktionscode"), nicht CRITICAL.

## Sonstige Beobachtungen / MINOR / NITPICK

- Dieselbe TD-Referenz-Praxis (`TD-009`, `TD-019`) findet sich auch in
  mehreren **Test**-Dateien der 6 Commits (u. a.
  `McpCodeGraphServerConstructorTests.cs:9-11`,
  `McpTestClient.cs:27`, `McpTestClientRetryOptions.cs:8`,
  `McpTestClientParallelTests.cs`, `McpTestClientRetryTests.cs`). Die
  Regel unterscheidet textlich nicht zwischen Produktions- und Testcode,
  die Severity-Gating-Vorgabe des Kritikers bindet MAJOR aber explizit an
  „Produktionscode" — daher hier nur als MINOR vermerkt, kein
  Findings-Posten, aber beim Fix-Step sinnvollerweise gleich mit
  bereinigt, da ohnehin dieselbe Ursache/dasselbe Muster betroffen ist.
- `McpTestClient.ConnectAsync`s Retry-`when`-Klausel fängt sehr breit
  (siehe Logik-Ebene (c) oben) — bei einem echten, nicht-transienten
  Fehler verzögert sich die sichtbare Fehlermeldung unnötig um bis zu
  mehrere Sekunden. Kein Blocker, da Testcode und Ende der Kette
  transparent bleibt (Original-Exception als `InnerException`
  erhalten).

## Frage an Nutzer

Keine — Fund ist eindeutig, Fix mechanisch (Kommentartext ändern, keine
Verhaltensänderung, kein Interpretationsspielraum).

## Tech-Debt-Einträge aus diesem Review

Keine neuen Einträge — der Fund ist ein reguläres `issues`-Finding
(Ebene 2, Rules-Konformität), kein Architektur-/Anti-Pattern-Fund
außerhalb des Step-Scopes.
