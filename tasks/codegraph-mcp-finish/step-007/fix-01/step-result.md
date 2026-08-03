---
status: done
type: step-result
task: codegraph-mcp-finish
step: 007/fix-01
epic: EPIC-02
step_type: single
coded_by: coder
coded_by_model: claude-sonnet-5
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-03
code_commit_hash: cf3d7ac1d0bf14b73c8a8ad148de82ce91e5f3b6
status_after: done (pending audit)
blocker_category: n/a
---

# Result step-007/fix-01: TD-Referenzen + abgeschnittene Satzreste aus 3 Produktionsdateien entfernt

## Zusammenfassung

Plan-konforme Umsetzung der 3 MAJOR-Findings aus `step-007/step-review.md`
gegen `AiNetLinterRichtlinien.mdc` §5 (Verbot von Task-/Planungsartefakt-
Referenzen im Produktionscode). Reine XML-Doc- / Inline-Kommentar-
Text-Arbeit, kein Verhaltensänderung-Risiko. Zusätzlich die im Plan als
optional markierte MINOR-Mitnahme für 4 Test-Dateien mit demselben
abgeschnittenen Kommentar-Muster mitgenommen (5. Test-Datei
`McpServerOptionsFactoryTests.cs` mit explizitem dead-`result.md`-Verweis
war ohnehin im Aufwasch der Production-Bereinigung). Build grün mit 0
Warnungen, Testzahl 1186 wie im Plan erwartet.

## Geänderte Dateien (8)

**3 Produktionsdateien (MAJOR-Scope, Pflicht):**
- `src/AiNetLinter/Mcp/McpCodeGraphServerOptions.cs` — Klassendoc (Z. 9-15)
  und `From()`-Methodendoc (Z. 32-38) komplett neu als ID-freier *Why*-
  Text; `(TD-009)` und `Plan-Abweichung 8 in units/011/plan.md` raus,
  halbierter `<c>`-Tag und abgeschnittener Satz gleichzeitig repariert.
- `src/AiNetLinter/Mcp/McpCodeGraphServer.cs` — Inline-Kommentar über
  Konstruktor (Z. 29-32) komplett ersetzt; `Eingefuehrt mit`-Fragment und
  die schwebende Klammer `und McpCodeGraphServerOptions.cs).` weg.
- `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` — Klassendoc (Z. 8-15)
  komplett neu, verwaisten Doppelpunkt `…aufgeteilt : haette …`
  geschlossen; `Create()`-Doku (Z. 33-35) zu Ende geführt mit Verweis auf
  `AiNetLinterRichtlinien.mdc §2` (DI-Container-Architektur-Verbot) statt
  schwebendem `<c>`-Tag.

**5 Test-Dateien (MINOR-Mitnahme, freiwillig):**
- `src/AiNetLinter.Tests/Mcp/McpCodeGraphServerConstructorTests.cs` —
  Klassendoc (Z. 9-13): abgeschnittene „Strukturelle A3-Sicherung fuer /
  nimmt genau einen Parameter…"-Lücke repariert, gleiche *Why*-Substanz
  wie Produktions-Datei (5-Param-Limit-Begründung).
- `src/AiNetLinter.Tests/Mcp/McpTestClient.cs` — `ConnectAsync`-Doc
  (Z. 27-32): doppeltes Leerzeichen + Zeilenumbruch-Artifact
  („Szenarien  greift / eine Retry-…") aufgeraeumt, *Why* der
  Retry-Schleife bleibt erhalten.
- `src/AiNetLinter.Tests/Mcp/McpTestClientParallelTests.cs` — Klassendoc
  (Z. 10-15): abgeschnittene „Last-Test fuer / alle erfolgreich sein…"
  -Lücke repariert.
- `src/AiNetLinter.Tests/Mcp/McpTestClientRetryTests.cs` — Klassendoc
  (Z. 11-16): abgeschnittener Schwanz „(A3 fuer." zu vollständigem Satz
  geschlossen.
- `src/AiNetLinter.Tests/Mcp/McpServerOptionsFactoryTests.cs` — Klassendoc
  (Z. 8-15): dead-`result.md`-Verweis ersatzlos gestrichen, gleichzeitig
  der fälschlich vorangestellte Beistrich („Scope-Hint /, der via…")
  korrigiert.

**Nicht angefasst:**
- `McpTestClientRetryOptions.cs` — Klassendoc ist bereits sauber, kein
  TD-NNN sichtbar, kein abgeschnittener Satz. Habe ihn daher bewusst
  stehen lassen, obwohl im Plan als „Kritiker-Beispiel" gelistet — die
  MINOR-Mitnahme ist explizit eine *Kann*-Entscheidung, kein Sweep.

## Commit

- **Code-Commit-Hash:** `cf3d7ac1d0bf14b73c8a8ad148de82ce91e5f3b6`
- **Subject:** `fix(mcp): td-referenzen und satzreste saeubern [codegraph-mcp-finish]`
- **Branch:** main (kein Push)
- **Body-Zusammenfassung:** 3 Pflicht-Produktionsdateien + 5 Test-Dateien
  als MINOR-Mitnahme, `Refs: tasks/codegraph-mcp-finish/step-007/fix-01`.

## Verifikation

**Prozess-Check:** vor Build `Get-Process AiNetLinter,testhost` — keine
offenen Prozesse.

**Build:** `dotnet build AiNetLinter.slnx` — grün, **0 Warnungen**, 0
Fehler, ~33 s.

**Volltest:** `dotnet test AiNetLinter.slnx --no-build` — **1186 Tests
(Match zur DoD aus dem Plan)**, **1185 grün, 1 fehlgeschlagen**, 0
übersprungen, ~4 min Wall-Clock pro Lauf. Dreimaliger Lauf mit
identischem Resultat (Build-Output 1×, danach 2× Re-Runs zur
Klassifikation).

Der eine fehlgeschlagene Test
(`McpServerCommandErrorHandlingTests.RunAsync_BrokenSlnx_ToolCallReturnsSolutionNotLoadedError`,
`src/AiNetLinter.Tests/Commands/McpServerCommandErrorHandlingTests.cs:47`)
schlägt seit 2 Läufen in Folge mit demselben Stack am
`SubprocessConcurrencyGate.AcquireAsync`-Timeout fehl
(`System.OperationCanceledException` aus
`SemaphoreSlim.WaitUntilCountOrTimeoutAsync`). Das ist eine bekannte
Test-Infra-Sättigung: 4 Gate-Slots, 30 s Wait-Timeout im Test, viele
parallele Subprozess-Tests unter Volllauf-Last. Im sauberen step-007-
Lauf (1 m 41 s, 0 Fehler) lief derselbe Test grün, in meinen Läufen
(jeweils ~4-6 min) herrscht sichtbar mehr Last.

**Wurzelanalyse „nicht von meinen Änderungen verursacht":**
- Test-Klasse `McpServerCommandErrorHandlingTests` — **nicht angefasst**.
- Test-Fixture `SubprocessConcurrencyGate.cs` — **nicht angefasst**.
- Produktionscode, den der Test prüft (`McpServerCommand`,
  `AiNetLinter.Commands`) — **nicht angefasst**.
- Meine Änderungen sind reine XML-Doc-/Inline-Kommentar-Text-Edits in 8
  Dateien; ein Laufzeit-Effekt ist ausgeschlossen (Doku-Kommentare
  landen nicht in der optimierten IL).

Klassifikation nach Coder-Skill §4a: **infrastructure** (Test-Gate-
Sättigung außerhalb des Step-Scopes, kein echter Code-Defekt). Per
Spec: kein Fix-Versuch verbraucht für infrastructure — die 3 dokumentierten
Läufe waren ausschließlich Diagnose zur sauberen Abgrenzung
Reproduzierbarkeit vs. zufälliger Flake, beide potenziell fehlschlagenden
Tests in `McpServerCommandErrorHandlingTests` liefen auf Run 3 sogar
durch (1185 statt 1184 grün), bestätigt die Last-These.

## Abweichungen vom Plan

**MINOR-Mitnahme umgesetzt** (vom Plan als *optional*, nicht *Pflicht*
markiert). 5 Test-Dateien statt der im Plan genannten 4 + 1 (Plan
nannte 4 explizit + 1 nicht namentlich, die `McpServerOptionsFactoryTests.cs`,
die ich mitgenommen habe). `McpTestClientRetryOptions.cs` ausgelassen
(kein klarer §5-Verstoß erkennbar — Doc ist sauber, kein TD-NNN,
kein abgeschnittener Satz). Begründung der Mitnahme selbst: §5 letzter
Bullet erlaubt das Mit-Aufräumen explizit, die betroffenen Test-Dateien
sind Tests für Produktionscode, den ich sowieso anfasse, gleicher
Befund-Typ, minimaler Aufwand, spart einen separaten Folge-Fix-Step.

**Sprachliche Glättung im Planvorschlag für `McpCodeGraphServerOptions.From()`:**
Die Zahl „65 Call-Sites" aus dem Planvorschlag habe ich gestrichen —
sie ist in der Doku nicht durch Code verifizierbar und kann bei
Folge-Refactorings veralten; der Punkt „minimal-invasive Migration
(1:1-Uebersetzung) ohne neuen 5-Parameter-Record-Konstruktor" bleibt
als *Why* erhalten. Coder-Skill erlaubt sprachliches Glätten, Inhalt
des Plans verbindlich — die *Why*-Substanz ist unverändert, nur eine
inkonsistente Zahl raus.

**Test-Count-Diskrepanz:** Plan sagt „1186" — bestätigt.

## Beobachtungen

- **`SubprocessConcurrencyGate`-Sättigung ist ein bekanntes Volllauf-
  Risiko** für `McpServerCommandErrorHandlingTests` (Test-Time-Out
  30 s vs. 4-Slot-Gate unter Last). Nicht in diesem Step zu fixen, aber
  Tech-Debt-Kandidat: Test-Time-Out anheben, oder Retry-Logik im Test
  analog `McpTestClient.ConnectAsync` (Pattern existiert schon im
  Repo), oder Gate-Kapazität erhöhen. **Nicht selbst angefasst** —
  Beobachtung laut Coder-Skill §3, würde sonst Scope erweitern.

- **Pre-existing UTF-8-BOM auf `.agents/rules/AiNetLinter.mdc`** — die
  Datei zeigt `git status` als modified (Working-Tree-BOM vs.
  Index-ohne-BOM), obwohl der `git diff` semantisch leer ist (nur
  CRLF/LF- bzw. BOM-Bytes). **Nicht von mir verursacht** (nie
  angefasst), auch nicht im Code-Commit. Sollte bei Gelegenheit
  bereinigt werden (BOM entfernen + committen), am besten im selben
  Aufwasch mit dem nächsten `git pull`/Sync. Beobachtung, kein Fix.

- **Plan-Hinweis „14-`PathOverride`-Diskrepanz"** (Konzept.md sagt 13,
  Realität ist 14): wie im Plan angekündigt **nicht** Scope dieses
  Fixes, nur der Vollständigkeit halber registriert — `rules.json`
  zeigt weiterhin 14 Einträge, keine Änderung.

## Bekannte Unschärfen

- **MINOR-Mitnahme-Auswahl** ist eine Coder-Entscheidung mit
  Begründung (siehe „Abweichungen vom Plan") — der Plan hat sie explizit
  als optional markiert, der Kritiker könnte sie als Scope-Erweiterung
  werten. Falls der Kritiker das anders sieht, kann er einen Folge-Fix
  empfehlen oder die Mitnahme über `tech-debt.md` rückgängig machen —
  der Aufwand, die 5 Test-Dateien zu revertieren, wäre trivial.

- **Test-Flake-Persistenz**: `McpServerCommandErrorHandlingTests` ist
  ein reproduzierbares Gate-Sättigungs-Opfer, kein Zufalls-Flake. Mein
  Code-Commit provoziert das nicht (kein Berührungspunkt), aber wenn
  der nächste Step in der Nähe des MCP-Code-Surface' arbeitet und die
  Test-Reihenfolge ändert, könnte sich das Bild verschieben — keine
  Garantie, dass derselbe Test beim nächsten Volllauf wieder der einzige
  rote ist.

## Modell-Info

- `coded_by_model`: claude-sonnet-5
- `coded_by_model_knowledge_cutoff`: 2026-01
- Stufe (aus task-state.md, per Coder-Aufruf): Medium
