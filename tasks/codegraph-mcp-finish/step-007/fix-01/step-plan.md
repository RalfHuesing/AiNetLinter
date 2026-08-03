---
status: done (pending audit)
type: step-plan
task: codegraph-mcp-finish
step: 007/fix-01
title: "TD-Referenzen + abgeschnittene Satzreste aus 3 Produktionsdateien entfernen (Rules §5-Konformität)"
epic: EPIC-02
estimated_risk: low
step_type: single
items: []
created_by: planer
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-03
related_to:
  - step-007/step-review.md
---

# Step 007/fix-01: TD-Referenzen + abgeschnittene Satzreste aus 3 Produktionsdateien entfernen

## Bezug

- **Task:** `codegraph-mcp-finish`
- **Epic:** `EPIC-02` aus `roadmap.md` — Einheit-011-Abschluss (Muss-Haben A).
  Der äußere Step 007 hat das nachgeholte Review der 6 gepushten
  Einheit-011-Commits abgeschlossen; dieser Fix-Step räumt die dabei
  aufgefallenen, eindeutigen Rules-Verstöße im Produktionscode auf.
- **Auslöser:** `step-007/step-review.md` Abschnitt „Findings" — 3
  MAJOR-Findings (Regel-Konformität, alle drei explizite Verstöße gegen
  `AiNetLinterRichtlinien.mdc` §5 wegen TD-/Plan-Artefakt-Referenzen im
  Produktionscode).
- **Konzept-Referenz:** nicht direkt — Fix-Modus plant ausschließlich gegen
  den Review-Befund, nicht gegen `Konzept.md` (siehe `spec.md` §6.2.1 und
  `skills/planer/SKILL.md` „Fix-Modus").

## Aktueller Projektzustand (JIT-Kontext)

Beim Lesen der 3 betroffenen Dateien direkt vor diesem Plan wurden drei
Beobachtungen gemacht, die über die Findings-Beschreibung hinausgehen
und daher hier explizit festgehalten werden, damit der Coder sie beim
Säubern mitadressiert (alles im selben Aufwasch, kein Mehraufwand):

1. **Die drei Findings-Kommentare sind nicht nur TD-referenzhaltig, sondern
   zusätzlich strukturell defekt** — abgeschnittene Sätze, schwebende
   Klammern, halbierte cref-Tags. Konkret:
   - `McpCodeGraphServerOptions.cs:10-14` — Klassendoc. Zwischen
     „Eingefuehrt mit" (Z. 10) und „der vorherige 5-Parameter-Konstruktor"
     (Z. 11) klafft eine Lücke (vermutlich wurde hier
     „`(TD-009)`, weil" o. Ä. entfernt, ohne den Satz neu zu
     schließen). Z. 12 beginnt mit „`(siehe <c>`" und endet mit
     „`exakt erreichte —`" — der `<c>`-Tag hat keinen Inhalt und kein
     schließendes `</c>`, der Satz bricht mitten im Wort ab.
   - `McpCodeGraphServer.cs:29-32` — Konstruktor-Inline-Kommentar.
     Zwischen „Eingefuehrt mit" (Z. 29) und „der am projektweiten…-Limit
     lag" (Z. 30) klafft dieselbe Lücke. Z. 30 endet mit „lag " (mit
     Leerzeichen), Z. 31 beginnt mit „und McpCodeGraphServerOptions.cs)."
     — die schließende Klammer schwebt syntaktisch im Leeren.
   - `McpServerOptionsFactory.cs:9-15` — Klassendoc. Z. 11 endet mit
     „aufgeteilt", Z. 12 beginnt mit „: haette <see cref="McpCodeGraphServer"/>…"
     — der Doppelpunkt schließt an nichts an (vermutlich hieß es
     hier mal „aufgeteilt **(TD-014):** weil ohne diese Auslagerung…").
     Im aktuellen Stand ist das `(TD-014)`-Token nicht mehr sichtbar,
     der abgeschnittene Bereich wurde aber stehengelassen.

   → Der Coder muss den jeweiligen Text **komplett** als ID-freies
   *Why* neu schreiben, nicht nur ein Token oder eine Klammer löschen.
   Andernfalls bleiben sinnlose, grammatisch kaputte XML-Doc-Kommentare
   stehen, die formal §5 zwar nicht mehr verletzen, aber inhaltlich
   genauso unbrauchbar sind wie vorher.

2. **Im aktuellen Repo-Stand ist der TD-014-Token in
   `McpServerOptionsFactory.cs` nicht mehr sichtbar** — entweder durch
   einen unabhängigen Edit entfernt oder im abgeschnittenen Bereich
   verschwunden. Der Finding-Text des Kritikers geht aber davon aus,
   dass `(TD-014)` dort steht. Für den Fix-Step ist das unerheblich:
   der Bereich, in dem der Token stand bzw. stehen würde, ist im
   aktuellen Stand so oder so defekt und muss ersatzlos umgeschrieben
   werden.

3. **MINOR-Beobachtung des Kritikers zu Test-Dateien** (siehe
   `step-007/step-review.md` „Sonstige Beobachtungen", erster
   Aufzählungspunkt): Der Kritiker nennt TD-Referenzen in
   `McpCodeGraphServerConstructorTests.cs:9-11`,
   `McpTestClient.cs:27`, `McpTestClientRetryOptions.cs:8`,
   `McpTestClientParallelTests.cs` und `McpTestClientRetryTests.cs`.
   Direkt geprüft (siehe „Notes → Entscheidung MINOR-Mitnahme"
   unten) sind die TD-Tokens im aktuellen Stand in den vier zuerst
   genannten Dateien **nicht** (mehr) sichtbar — die abgeschnittenen
   XML-Docs dort tragen aber dieselbe Struktur wie die
   Produktionsdateien und sind entsprechend mit aufzuräumen. In
   `McpServerOptionsFactoryTests.cs:13-14` (vom Kritiker nicht namentlich
   genannt, aber in derselben Kategorie) steht ein abgeschnittener
   Verweis „Plan-Abweichung im <c>result.md</c> von." (die referenzierte
   `result.md` existiert nicht mehr, gelöscht mit
   `tasks/codegraph-mcp-server/`). Die MINOR-Mitnahme ist im
   Fix-Plan als **optionaler Sub-Punkt** aufgenommen, nicht als
   Pflicht-Scope — siehe „Konkrete Änderungen" Abschnitt „Optional:
   MINOR-Testdatei-Mitnahme" und „Notes → Entscheidung MINOR-Mitnahme".

## Intention

Nach diesem Fix-Step sind die drei im Review benannten MAJOR-Findings
beseitigt: die drei Produktionsdateien tragen wieder ID-freie
*Why*-Kommentare, die syntaktisch vollständig sind und keine
Verweise mehr auf gelöschte Planungsartefakte (`units/011/plan.md`)
oder wegwerf-bedeutungslose IDs (`TD-009`, `TD-014`) enthalten.
Gleichzeitig sind die beim Säubern ohnehin anzufassenden, abgeschnittenen
Satzreste mitrepariert — reine Kommentar-Text-Arbeit, kein
Verhaltensänderung-Risiko, keine Build- oder Test-Risiken.

Der Push der bereits lokal vorliegenden 11 Task-Doku-Commits ist
**nicht** Scope dieses Steps (separate Nutzer-Entscheidung).

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Mcp/McpCodeGraphServerOptions.cs` (Finding 1)

- **Was:** Den abgeschnittenen Klassen-XML-Doc (Z. 9-15) komplett durch
  einen ID-freien *Why*-Text ersetzen, der die Begrenzung des
  Vorgänger-Konstruktors und den Nutzen des Input-Records erklärt.
  Analog den abgeschnittenen `From()`-Methoden-Doc (Z. 32-38) reparieren:
  den Verweis „Plan-Abweichung 8 in `<c>units/011/plan.md</c>`"
  ersatzlos streichen und den `consoleOverride`-Hinweis als
  beobachtbaren Fakt (kein Call-Site nutzt ihn) sauber neu
  formulieren. Konkret-Vorschlag (Coder darf sprachlich glätten,
  Inhalt verbindlich):

  ```csharp
  /// <summary>
  /// Input-Parametersatz fuer <see cref="McpCodeGraphServer"/>. Eingefuehrt als
  /// Ersatz fuer den frueheren 5-Parameter-Konstruktor, der am projektweiten
  /// <c>MaxConstructorDependencies: 5</c>-Limit (siehe <c>AiNetLinter.mdc</c>)
  /// exakt angelangt war — jede weitere P0/P1-Erweiterung am Konstruktor
  /// haette den Build gebrochen. Mit diesem Record wachsen kuenftige
  /// Konfigurations-Properties additiv, ohne die Konstruktor-Signatur zu aendern.
  /// </summary>
  ```

  ```csharp
  /// <summary>
  /// Factory-Methode mit identischer Parameter-Signatur wie der vorherige
  /// <c>McpCodeGraphServer</c>-Konstruktor. Erlaubt minimal-invasive Migration
  /// der Call-Sites (1:1-Uebersetzung) ohne neuen 5-Parameter-Record-Konstruktor.
  /// <c>consoleOverride</c> wurde bewusst entfernt: kein einziger Call-Site
  /// uebergibt ihn.
  /// </summary>
  ```

- **Warum:** Findings 1 verbieten TD-/Planungsartefakt-Verweise im
  Produktionscode. Der konkrete Verweis `units/011/plan.md` ist bereits
  heute für jeden Leser unauflösbar (Ordner existiert nicht mehr).
  Beim Säubern werden zugleich die abgeschnittenen Sätze und der
  halbierte `<c>`-Tag repariert, die im aktuellen Stand syntaktisch
  defekt sind (siehe „Aktueller Projektzustand" Punkt 1).

### Datei 2: `src/AiNetLinter/Mcp/McpCodeGraphServer.cs` (Finding 2)

- **Was:** Den abgeschnittenen Inline-Kommentar über dem Konstruktor
  (Z. 29-32) ersatzlos durch einen ID-freien *Why*-Kommentar ersetzen,
  der den Nutzen des Input-Record-Konstruktors erklärt. Die schwebende
  Klammer „…Options.cs)." (Z. 31) und der abgeschnittene Vordersatz
  „Eingefuehrt mit" (Z. 29) verschwinden dabei. Konkret-Vorschlag
  (Coder darf sprachlich glätten, Inhalt verbindlich):

  ```csharp
  // Input-Record ersetzt den frueheren 5-Parameter-Konstruktor, der am
  // projektweiten MaxConstructorDependencies: 5-Limit lag (siehe
  // McpCodeGraphServerOptions.cs). Erlaubt additive P0/P1-Erweiterungen an der
  // Config, ohne die Konstruktor-Signatur zu aendern.
  ```

- **Warum:** Finding 2 verbietet TD-Referenzen im Produktionscode.
  `TD-009` ersatzlos raus, die *Why*-Substanz (Limit-Begründung +
  Verweis auf den Options-Record) bleibt. Beim Säubern werden zugleich
  die abgeschnittenen Satzreste mitrepariert (siehe „Aktueller
  Projektzustand" Punkt 1).

### Datei 3: `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` (Finding 3)

- **Was:** Den abgeschnittenen Klassen-XML-Doc (Z. 8-15) komplett durch
  einen ID-freien *Why*-Text ersetzen, der die Auslagerung aus
  `McpServerCommand` und die Footprint-Begründung sauber erklärt. Den
  verwaisten Doppelpunkt „…aufgeteilt **:** haette …" (Z. 11-12) dabei
  zu einem vollständigen Satz schließen. Konkret-Vorschlag (Coder darf
  sprachlich glätten, Inhalt verbindlich):

  ```csharp
  /// <summary>
  /// Baut die <see cref="McpServerOptions"/> inkl. der registrierten Tool-Collection.
  /// Bewusst aus <see cref="AiNetLinter.Commands.McpServerCommand"/> ausgelagert
  /// und durch <see cref="McpServerOptionsBuilder"/> in eine schlanke Factory + Builder
  /// aufgeteilt: ohne diese Auslagerung waechst der AIContextFootprint von
  /// <see cref="McpCodeGraphServer"/> durch die Tool-Registrierungs-Abhaengigkeiten
  /// ueber das projektweite Limit (siehe <c>AiNetLinter.mdc</c>).
  /// </summary>
  ```

  Zusätzlich: den abgeschnittenen Verweis am Ende der `Create()`-Doku
  (Z. 33-35, „…kein DI-Container (siehe <c>." — schwebt ohne Ziel)
  prüfen und entweder zu Ende schreiben oder streichen, sofern der
  begonnene Verweis keinen erkennbaren Sinn ergibt. Entscheidung des
  Coders anhand des Leseflusses, beides ist begründbar — Hauptsache,
  kein schwebender Kommentar-Rest.

- **Warum:** Finding 3 verbietet TD-Referenzen im Produktionscode. Im
  aktuellen Stand ist `(TD-014)` zwar nicht mehr sichtbar (Token ggf.
  schon entfernt), aber die Umgebung des Tokens (Z. 11-12) ist mit
  abgeschnittenem Doppelpunkt + schwebender Begründung genauso
  unbrauchbar — der Bereich muss als Ganzes umgeschrieben werden.
  Außerdem hängt am Ende der `Create()`-Methode ein zweiter,
  thematisch benachbarter Schaden, der im selben Aufwasch mit
  beseitigt wird (kein separater Findings-Posten, aber gleiches
  Muster).

### Optional: MINOR-Testdatei-Mitnahme (MINOR-Beobachtung, nicht Pflicht-Scope)

> **Hinweis für den Coder:** Dies ist eine **freiwillige Mitnahme**
> (MINOR-Beobachtung aus `step-007/step-review.md` „Sonstige
> Beobachtungen"). Der Kritiker hat sie explizit **nicht** als
> Findings-Posten geführt, sondern nur als MINOR vermerkt — die
> Severity-Gating-Vorgabe bindet MAJOR an Produktionscode, die
> Test-Dateien sind Testcode. Der Coder kann diesen Sub-Punkt
> **überspringen**, wenn er strikt nur die 3 MAJOR-Findings
> abarbeiten will; genauso begründbar ist die Mitnahme, weil §5
> letzter Bullet ausdrücklich erlaubt: „Trifft ein Agent im Zuge
> einer Änderung im berührten Code auf eine bestehende Verletzung
> dieser Regel, darf er sie im selben Zug entfernen bzw. umschreiben
> — auch wenn das nicht Teil des eigentlichen Auftrags war."
> Entscheidung liegt beim Coder, der Orchestrator erwartet keine
> Begründung im step-result.md, falls übersprungen.

Falls der Coder die Mitnahme wählt:

- **Was:** In den vom Kritiker explizit genannten Test-Dateien die
  abgeschnittenen XML-Doc-Kommentare mit TD-/Plan-Artefakt-Bezug auf
  ID-freie *Why*-Texte umschreiben, analog zum Vorgehen in den 3
  Produktionsdateien. Konkret:
  - `src/AiNetLinter.Tests/Mcp/McpCodeGraphServerConstructorTests.cs:9-12` —
    „Eingefuehrt mit … <c>MaxConstructorDependencies: 5</c>-Limit lag"
    analog Finding 2 (gleiche Erzählstruktur, gleicher Eingriff).
  - `src/AiNetLinter.Tests/Mcp/McpTestClient.cs:27-32` — den XML-Doc
    von `ConnectAsync` auf ID-freies *Why* zur Retry-Schleife
    umschreiben (TD-019-Bezug raus, „warum Retry" als Begründung der
    Test-Infrastruktur stehen lassen).
  - `src/AiNetLinter.Tests/Mcp/McpTestClientRetryOptions.cs:5-9` —
    analog `McpTestClient.cs`, gleiche Retry-Begründung ID-frei.
  - `src/AiNetLinter.Tests/Mcp/McpTestClientParallelTests.cs` und
    `McpTestClientRetryTests.cs` — XML-Docs prüfen und analog
    bereinigen, sofern sie TD-019-Bezug enthalten.

  Zusätzlich — **nicht** vom Kritiker namentlich genannt, aber in
  derselben Kategorie und im aktuellen Stand mit klarem §5-Verstoß:

  - `src/AiNetLinter.Tests/Mcp/McpServerOptionsFactoryTests.cs:8-15` —
    Klassendoc endet mit „…brechen wuerde (siehe Plan-Abweichung im
    <c>result.md</c> von." — der Verweis auf die nicht mehr existente
    `result.md` (Einheit 011, Ordner `tasks/codegraph-mcp-server/`
    ist weg) ist bereits heute unauflösbar. Streichen und den Satz
    zu Ende führen.

- **Warum:** MINOR-Beobachtung aus dem Review. Mitnahme spart einen
  separaten Folge-Fix-Step, weil dieselbe Ursache (vergessene
  Aufräum-Pflicht) hier dieselbe Wirkung hat wie in den 3
  Produktionsdateien.

## Tests

Keine neuen Tests. Begründung: Reine Kommentar-Text-Änderung, keine
Verhaltensänderung. Der bestehende `dotnet build` + `dotnet test … --no-build`
-Vollauf (Tech-Stack-Notiz in `roadmap.md`) ist die ausreichende
Verifikation — er bestätigt, dass (a) die Kommentare XML-Doc-syntaktisch
sauber parsen und (b) kein Verhalten verschoben wurde. Der Lauf ist
ohnehin Teil der Definition of Done.

## Definition of Done

- [ ] `McpCodeGraphServerOptions.cs:9-15` und `:32-38` — ID-frei,
      syntaktisch vollständig, abgeschnittene Reste beseitigt
- [ ] `McpCodeGraphServer.cs:29-32` — ID-frei, schwebende Klammer
      beseitigt
- [ ] `McpServerOptionsFactory.cs:8-15` — ID-frei, Doppelpunkt-
      Lücke geschlossen; `Create()`-Doku (Z. 33-35) entweder zu Ende
      geführt oder gestrichen
- [ ] Optional (MINOR-Mitnahme, falls Coder sich dafür entscheidet):
      die 5 vom Kritiker genannten Test-Dateien +
      `McpServerOptionsFactoryTests.cs` analog bereinigt
- [ ] Build-Command aus Tech-Stack-Notiz (`roadmap.md`) grün,
      Zero-Warning-Direktive eingehalten
- [ ] Test-Command aus Tech-Stack-Notiz grün (Volllauf, gleiche
      Testzahl wie in `step-007/step-result.md` = 1186)
- [ ] Code-Commit auf aktuellem Branch (Conventional Commit auf
      Deutsch, imperativ, Task-Suffix `[codegraph-mcp-finish]`)
- [ ] Doku-Commit auf aktuellem Branch (status-Update in
      `step-007/fix-01/step-plan.md` auf `in_progress` →
      `done (pending audit)` + `step-007/fix-01/step-result.md`)
- [ ] `step-007/fix-01/step-result.md` geschrieben
- [ ] **Kein Push** in diesem Fix-Step

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc` §5 (Qualitätsdrift-
  Prävention) — verbietet explizit TD-/Planungsartefakt-Referenzen
  im Code-Kommentar, fordert ID-freie *Why*-Begründungen statt
  Verweisen auf Planungsdokumente, und erlaubt im letzten Bullet
  ausdrücklich das Aufräumen im selben Zug (Begründung für die
  MINOR-Mitnahme-Option).

## Bekannte Ausnahmen

- Falls der Coder die MINOR-Testdatei-Mitnahme auslässt: keine — die
  MAJOR-Findings im Produktionscode sind die einzigen Findings, die
  das `issues`-Verdict ausgelöst haben. MINOR-Beobachtungen lösen
  kein Verdict aus und sind daher nicht zwingend im selben Fix zu
  beheben.

## Notes

- **Commit-Strategie:** Zwei lokale Commits in dieser Reihenfolge
  (gemäß `spec.md` §10.3):
  1. **Code-Commit** (Coder) — die eigentlichen Kommentar-Änderungen
     in den 3 Produktionsdateien (+ ggf. 5 Test-Dateien bei
     MINOR-Mitnahme). Conventional Commit auf Deutsch, imperativ,
     mit Task-Suffix `[codegraph-mcp-finish]`. Beispiel:
     `fix(mcp): td-referenzen und abgeschnittene satzreste aus kommentaren entfernt [codegraph-mcp-finish]`.
  2. **Doku-Commit** (Coder) — Status-Update in diesem
     `step-plan.md` (von `in_progress` auf `done (pending audit)`) +
     `step-007/fix-01/step-result.md`. Conventional Commit, Beispiel:
     `docs(task): step-007/fix-01 abgeschlossen [codegraph-mcp-finish]`.

- **Push:** keiner. Weder die bereits gepushten 6 Einheit-011-Commits
  (kein neuer Push nötig — die Korrekturen landen als neue Commits
  auf `main`) noch die 11 aktuell lokal vorliegenden Task-Doku-Commits
  (separate Nutzer-Entscheidung, nicht Scope).

- **Entscheidung MINOR-Mitnahme (Begründung für den Orchestrator):**
  Ich nehme die MINOR-Beobachtung des Kritikers zu den Test-Dateien
  als **optionalen** Sub-Punkt mit auf (nicht als Pflicht-Scope), und
  zwar aus folgenden Gründen:
  1. §5 letzter Bullet (`AiNetLinterRichtlinien.mdc`) erlaubt das
     Mit-Aufräumen explizit, und der Coder fasst die nahegelegenen
     Produktionsdateien sowieso an.
  2. Die MINOR-Beobachtung betrifft denselben Befund-Typ (abgeschnittene
     Kommentare, §5-Verstöße) — der Aufwand, sie im selben Aufwasch
     mitzunehmen, ist minimal.
  3. Sie nicht mitzunehmen, hieße, in 1-2 weiteren Schritten einen
     weiteren Mini-Fix-Step zu erzeugen — schlechteres
     Aufwand-Nutzen-Verhältnis.
  4. Aber: der Coder hat die Hoheit. Wenn er strikt MAJOR-only arbeiten
     will (z. B. weil er die Test-Dateien nochmal gründlicher prüfen
     möchte), ist das gleichwertig begründbar — daher im Plan klar
     als optional markiert, nicht als Pflicht.

- **Beobachtung zur 14-`PathOverride`-Diskrepanz** (aus
  `step-007/step-review.md`): bewusst **nicht** Scope dieses Fixes
  (kein Finding, keine §5-Verletzung), nur der Vollständigkeit halber
  erwähnt, falls der Coder beim `dotnet build`-Output darauf stößt.

- **Sonderfall zeitlicher Ablauf:** Die Fixes korrigieren die
  bereits gepushte Code-Basis nachträglich. Das ist ein regulärer
  Workflow-Schritt trotz ungewöhnlichen zeitlichen Ablaufs — der
  Orchestrator pusht die hier entstehenden Commits nicht, der Nutzer
  entscheidet separat.
