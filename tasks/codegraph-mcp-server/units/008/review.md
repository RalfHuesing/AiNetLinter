---
unit: 008
task: codegraph-mcp-server
workflow: dynamic-loop
type: review
created_by: kritiker
created_at: 2026-08-02
verdict: issues
---

# Review Einheit 008 — EPIC-08 Doku (MCP-Modus)

## 1. Verdict

**`issues`** — ein MAJOR-Fund (interner Doku-Drift in `agent-api.md:238`),
mehrere MINOR (Dokumentations-Asymmetrien, ein Pfad-Hinweis). Sonst ist die
Einheit sauber: Volllauf 1164/1164 grün, alle 10 Pflicht-Punkte des
Doku-Plans abgedeckt, Parameternamen 1:1 zu den Code-Signaturen,
ServerInstructions und Trunkierungs-Meta-Zeilen wortwörtlich aus dem
Code, 15 Error-Codes 1:1, A3-Pfade für alle 3 neuen Tests dokumentiert,
A7 (kein Konzept-Edit) eingehalten.

**Empfehlung an Orchestrator:** `fix-01` für die Doku-Korrektur in
`agent-api.md:238` (eine halbe Zeile), kein Tech-Debt-Eintrag nötig
(Doku-Drift im eigenen Scope dieser Einheit, nicht außerhalb).

## 2. Plan-Erfüllung

| Pflicht-Punkt (Plan Schritt 2-8) | Soll | Ist | Status |
|---|---|---|---|
| `Docs/agent-api.md` neue Sektion „MCP-Server-Modus" | +250-400 Z. | +130 Z. | ✓ (kompakter, alle 10 Punkte abgedeckt) |
| `Docs/integration.md` neue Sektion „MCP-Server registrieren" | +150-300 Z. | +64 Z. | ✓ (Tool-vs-`rg`-Empfehlung enthalten, Pflicht-DoD Z. 659-660) |
| `Docs/ROADMAP.md` Status-Update | +50-150 Z. | +34 Z. | ✓ (EPIC-01..08 + P0/P1-Rest, „nächste Phase" benannt) |
| `README.md` Kurz-Hinweis | +20-40 Z. | +4 Z. | ✓ (knapp, Cross-Links) |
| A3-Test-Datei `McpDocumentationSmokeTests.cs` | ~120 Z., 3-5 Tests, Integration | 73 Z., 3 Tests, Integration, `IClassFixture<McpLiveRepositoryFixture>` | ✓ |
| A3-Pfade dokumentiert (A3-1, A3-2, A3-3) | je 1 rot + grün mit Fehlertext | 3 dokumentiert (siehe MINOR-1 unten) | ✓ mit Asymmetrie |
| Volllauf grün (AGENTS.md §2) | 1161+3 = 1164/1164 | 1164/1164 in 6:50 min | ✓ (gemessen 2026-08-02 ~18:18, `volllauf.log` zeigt `Bestanden! : Fehler: 0, erfolgreich: 1164`) |
| `git push` unterlassen (A4) | kein Push | Working-Tree clean, `main` weiterhin 1 Commit ahead of `origin/main` | ✓ |
| `konzept.md` nicht editiert (A7) | nicht anfassen | nicht angefasst | ✓ |
| `kernel.md`/Rollen-Dateien/.agents/rules nicht editiert (A8) | nicht anfassen | nicht angefasst | ✓ |
| Code-Dateien außer neuer Test-Datei nicht editiert (A5) | nicht anfassen | nicht angefasst | ✓ |
| Self-Lint gegen `BaselineMini` | 0 Regress | 1 erwartete Violation in `ViolatingClass.cs` (Test-Fixture) | ✓ |
| Konzept-Diskrepanzen dokumentiert (A7) | mind. 2 (Planer fand 2) | 3 (Coder fand Z. 564 zusätzlich) | ✓ (vollständiger als verlangt) |
| Commit-Disziplin | Conventional Commits, Imperativ, Suffix | 4× `docs(mcp):`, 1× `test(mcp):`, 2× `chore(task):` | ✓ |

**A3-1 wortwörtliche Bestätigung** (aus `result.md` A3-Block):
- Build grün (0/0) → Test rot: `Assert.Contains() Failure: Sub-string not found` / `Not found: "LinterEnginXYZ"` → Pfad bestätigt.
- A3-2: identisches Muster, `.cs` → `.csXYZ`, Output enthält wortwörtlich `.cs: 331 Dateien (voll vom Symbolgraph abgedeckt)` — exakt die Doku-Formulierung in `FileStructureToolRegistrations.cs:45-48` (`agent-api.md:249`).
- A3-3: `Treffer gesamt`/`gezeigt` umgebogen, Pfad bestätigt.

## 3. Findings

### MAJOR

**F-001: Doku-Drift in `Docs/agent-api.md:238` — C#-only-Zählung widerspricht
Tabelle und zitierter ServerInstructions-Quelle.**

Aktueller Wortlaut (Z. 238):

> Konsequenz für den Agent-Loop: 7 Tools sind C#-only (find_symbol,
> find_references, get_impact, get_type_hierarchy, get_file_skeleton,
> get_violations, search_pattern nutzt auch Nicht-C#-Dateien), 2 Tools
> sind Struktur-orientiert und nicht C#-beschränkt. Für Treffer in
> `.js`/`.razor`/`.cshtml`/`.xaml`/`.html`/`.css` ist `search_pattern`
> der vorgesehene Fallback.

Befund:

1. **Tabelle in Z. 242-252 sagt 6×ja, 3×nein** (get_index_scope, get_hotspots,
   search_pattern mit „nein (Fallback)").
2. **Wortwörtlich zitierter ServerInstructions-Block in Z. 236 listet 6
   Symbolgraph-Tools** (find_symbol, find_references, get_impact,
   get_type_hierarchy, get_file_skeleton, get_violations) — also
   konsistent zur Tabelle (6 C#-only), **widerspricht aber der Aussage
   "7 Tools sind C#-only"**.
3. **search_pattern ist KEIN C#-only-Tool** — es ist der explizite
   Nicht-C#-Fallback (Tabelle Z. 252: „nein (Fallback)", Description
   in `AnalysisToolRegistrations.cs:50-56`).
4. Die Klammer-Aufzählung in Z. 238 listet 6+1 = 7 Items, aber Item 7
   (`search_pattern nutzt auch Nicht-C#-Dateien`) ist kein C#-only
   Tool — Zählung ist falsch und Klammer-Liste ist semantisch
   gemischt.

**Konsequenz:** Eine Doku-Einheit, deren expliziter Zweck die Beseitigung
von Doku-Drift ist (`konzept.md` Z. 622-624 DoD), hat selbst Doku-Drift
zwischen Fließtext (Z. 238), Tabelle (Z. 242-252) und wortwörtlich
zitierter Quelle (`McpServerOptionsFactory.cs:26-31`, Z. 236) eingebaut.
Die ServerInstructions-Block-Zitat-Quelle `McpServerOptionsFactory.cs:26-31`
enthält **6** C#-only-Symbolgraph-Tools, nicht 7. Ein Agent, der die
ServerInstructions mit dem Fließtext abgleicht, sieht eine Diskrepanz.

**Severity-Begründung (MAJOR, nicht CRITICAL):**
- Tool-Tabelle und ServerInstructions-Wortlaut sind korrekt.
- Build/Tests grün, A3-Tests decken diesen Satz nicht ab (sie testen
  Tool-Output, nicht Doku-Fließtext — methodisch korrekt, aber
  dadurch fängt der A3-Pfad den Doku-Drift nicht).
- Der Fehler ist ein **eigener** Schreibfehler des Coders beim
  Ausformulieren der Doku (kein Drift zu `konzept.md`, das diesen
  Satz nicht enthält), also ein klassischer Doku-interner Logikfehler.

**Empfohlener Korrekturtext (für Orchestrator, ein Zeile):**

> Konsequenz für den Agent-Loop: 6 Tools sind C#-only (find_symbol,
> find_references, get_impact, get_type_hierarchy, get_file_skeleton,
> get_violations), 2 Tools sind Struktur-orientiert und nicht
> C#-beschränkt (get_index_scope, get_hotspots). `search_pattern` ist
> der vorgesehene Fallback für Treffer in `.js`/`.razor`/`.cshtml`/
> `.xaml`/`.html`/`.css` und ist selbst nicht C#-only.

(Entscheidung des Coders in `fix-01`; Pflicht-Inhalt: Zählung auf 6,
search_pattern aus der C#-only-Aufzählung raus, ggf. eigener Satz
wie oben.)

### MINOR

**F-002: A3-Dokumentation asymmetrisch.**
`result.md` A3-Block dokumentiert für A3-1 (rot) Build grün + Test rot,
für A3-2 (rot) nur Test rot, für A3-3 (rot) nur Test rot; den
„zurückgebogen"-Schritt zeigt nur A3-2 explizit („3/3 in 5 s"). A3-1
und A3-3 dokumentieren den grünen Schritt implizit durch den
Build-Status vor dem Umbiegen. Methodisch korrekt, aber
Dokumentations-Konsistenz zu `units/007/result.md` A3-Vorlage
wäre besser (gleicher Dreischritt je Test). Empfehlung: in `fix-01`
oder bei nächster Gelegenheit angleichen. Severity: MINOR (kein
Test-Risiko, keine inhaltliche Lücke).

**F-003: Self-Lint-Pfad-Differenz Plan vs. Result.**
Plan Schritt 8.5 nennt `tests/Fixtures/BaselineMini`, Result
nennt `src/BaselineMini/ViolatingClass.cs` (1 erwartete Violation).
Inhaltlich konsistent (1 gewollte Fixture-Violation, kein Regress),
aber Pfad-Unterschied zwischen Plan und Coder-Ausführung — möglicher
Tippfehler im Plan oder der Coder hat den realen Pfad anders
aufgelöst. Empfehlung: in `state.md`/nächster `plan.md`-Vorlage den
tatsächlichen Pfad notieren. Severity: MINOR (kein Regress,
kein Inhalt-Verlust).

**F-004: Konzept-Diskrepanz-Liste vollständig (positiv), aber
Aufzählung in Z. 238 unsauber.** Siehe F-001 — derselbe Wurzel-Fehler
in zwei Doku-Stellen: Fließtext-Aussage + Klammer-Inkonsistenz.
Mit F-001 behoben.

## 4. Sonstige Beobachtungen (informativ)

### Konzept-Diskrepanzen — A7-konform, vollständig dokumentiert

Der Coder hat 3 Diskrepanzen dokumentiert (Planer fand 2, Coder
fand 1 zusätzlich):

1. **`konzept.md` Z. 539-552 Tool-Status-Tabelle** — `search_pattern`
   als „offen" gelistet (faktisch fertig seit 002), `get_violations`
   als „Review offen" (Review seit 001 `approved`). Doku spiegelt
   den Code-Stand. **Korrekt:** A7 eingehalten, keine `konzept.md`-
   Änderung. Coder hat `konzept.md` nicht editiert (`git diff
   origin/main..main --stat` zeigt keine Änderung an
   `tasks/codegraph-mcp-server/konzept.md`).

2. **`konzept.md` Z. 564 — suggeriert bereits umgesetzte
   Kaltstart-Entkopplung** („Transport/Handshake stehen dabei
   unabhängig vom Ladezustand sofort bereit"). Realität: Kaltstart
   ist unter P0/P1-Rest (Konzept Z. 265-275) als „geplant" markiert
   und **noch nicht umgesetzt** (`McpServerCommand.RunAsync` wartet
   `TryLoadSolutionAsync` synchron ab, `McpCodeGraphServer` hat
   keinen dritten „lädt noch"-Zustand). Coder-Vorschlag:
   Umformulierung in Z. 564 zu „sollen unabhängig vom Ladezustand
   sofort bereitstehen — Fix siehe P0/P1-Rest (Kaltstart entkoppeln)".
   **Bewertung:** Vorschlag sinnvoll, ist Sache des Nutzers (A7).
   Diese Diskrepanz **war im Planer-Check 6 nicht explizit
   aufgeführt** — der Coder hat sie selbst entdeckt. Positiv.

3. **`konzept.md` Z. 550 — `get_impact` Input-Beschreibung veraltet**
   („Datei-/Symbol-Scope" vs. real exklusive `gitRef`/`symbolIdentifier`-
   Parameter). Doku spiegelt Code-Stand korrekt. A7-konform.

**Bewertung:** Diskrepanz-Liste ist **vollständiger als verlangt**
(Planer-Check 6 listet nur 1 explizite Diskrepanz, der Coder fügt
eine zweite selbst-entdeckte hinzu, der Planer-Plan hatte bereits
eine zweite implizit — also 3 insgesamt). A7 ist eingehalten.
Empfehlung an den Nutzer: `konzept.md` Z. 539-552, 550, 564 bei
Gelegenheit an Code-Stand anpassen — gehört in eine Folge-Einheit,
nicht in `fix-01`.

### Commit-Disziplin (A4)

| Aspekt | Bewertung |
|---|---|
| 7 Commits in der Reihenfolge 4×Doku, 1×Test, 2×Result | ✓ konsistent zu 001-007-Pattern (siehe `units/007/result.md`) |
| Conventional Commits englisch, Imperativ, Suffix `[codegraph-mcp-server]` | ✓ |
| Kein Push, kein Amend, kein `-A` | ✓ (`main` weiterhin 1 Commit ahead of `origin/main`) |
| `volllauf.log` als gezielter Anhang im `result.md`-Commit (`6f2a4b9`) | ✓ A4-konform (bewusst hinzugefügt, nicht im Verzeichnis gedrifted; binary-safe weil UTF-16-LE) |
| Working-Tree nach Commits clean | ✓ |

### Doku-Treue zum Code (Stichproben)

| Doku-Aussage | Code-Quelle | Treue |
|---|---|---|
| `ServerInstructions`-Wortlaut (Z. 236) | `McpServerOptionsFactory.cs:26-31` | wortwörtlich 1:1 ✓ |
| Listen-Trunkierungs-Meta-Zeile (Z. 278) | `McpTruncation.cs:40` | wortwörtlich 1:1 ✓ |
| Datei-Listen-Trunkierungs-Meta-Zeile (Z. 284) | `McpTruncation.cs:66` | wortwörtlich 1:1 ✓ |
| 15 Error-Codes-Tabelle (Z. 321-335) | `LinterErrorCodes.cs:10-24` | 1:1, je 15 Einträge ✓ |
| `find_symbol` Signatur (Z. 244) | `SymbolGraphToolRegistrations.cs:26` | Parameternamen + Defaults 1:1 ✓ |
| `find_references` Signatur (Z. 245) | `SymbolGraphToolRegistrations.cs:39` | ✓ |
| `get_impact` Signatur (Z. 246) | `SymbolGraphToolRegistrations.cs:52` | exklusive-Parameter-Hinweis 1:1 ✓ |
| `get_type_hierarchy` Signatur (Z. 247) | `SymbolGraphToolRegistrations.cs:66` | ✓ |
| `get_file_skeleton` Signatur (Z. 248) | `FileStructureToolRegistrations.cs:29` | ✓ |
| `get_index_scope` Signatur (Z. 249) | `FileStructureToolRegistrations.cs:40` | ✓ (Description in Registrierung enthält wortwörtlich „.cs (voll vom Symbolgraph abgedeckt)" — vom A3-2-Test geprüft) |
| `get_hotspots` Signatur (Z. 250) | `FileStructureToolRegistrations.cs:52` | ✓ |
| `get_violations` Signatur (Z. 251) | `AnalysisToolRegistrations.cs:32` | ✓ |
| `search_pattern` Signatur (Z. 252) | `AnalysisToolRegistrations.cs:46` | ✓ |
| 9-Tool-Liste vollständig (Z. 240-252) | Konzept Z. 539-552 + Code | ✓ alle 9 Tools |

**Außer dem MAJOR-F-001 ist die Doku-Treue zum Code bemerkenswert
sauber.** Parameternamen, Defaults, Wortlaute, Error-Codes, Tool-Liste
— alles 1:1. Das ist exakt, was die A3-Tests beabsichtigen zu sichern.

### Test-Disziplin (A3)

- 3 Tests, alle `Category=Integration`, `[Collection("ConsoleTestCollection")]`,
  `IClassFixture<McpLiveRepositoryFixture>` — Pattern konsistent zu
  `McpLiveRepositoryTests.cs` aus 006/007.
- Test 1 (`FindSymbol_ReturnsLinterEngineHit`): Doku-Beispiel-Wortlaut
  `LinterEngine` case-insensitiv gesucht, A3-Pfad mit `LinterEnginXYZ`
  dokumentiert. A3-Pfad bestätigt durch wortwörtliche Fehlerausgabe
  im `result.md`.
- Test 2 (`GetIndexScope_ListsCsAsLargestCategory`): A3-Pfad mit
  `.csXYZ`, Fehlerausgabe zeigt wortwörtlich
  `.cs: 331 Dateien (voll vom Symbolgraph abgedeckt)` — exakt die
  Doku-Formulierung in `FileStructureToolRegistrations.cs:45-48` und
  in der Doku-Tabelle (Z. 249). **Bestätigt den Wert des A3-Ansatzes.**
- Test 3 (`FindSymbol_WithWidePattern_TruncatesWithMetaLine`): A3-Pfad
  mit `Treffer gesamt XYZ`/`gezeigtXYZ`, „Get" mit `maxResults=1`
  erzwingt Trunkierung in der echten AiNetLinter-Solution (> 50
  Treffer). Trunkierungs-Wortlaut stimmt mit `McpTruncation.cs:40`
  überein.
- Smoke-Slice: 3/3 in 5-6 s (zwei Läufe dokumentiert). Volllauf
  1164/1164 in 6:50 min.

### Build-Verifikation

- Baseline Build: 0/0 grün (Doku-Edits dürfen Build nicht brechen,
  bewiesen).
- Volllauf 1164/1164 grün in 6:50 min.
- Self-Lint: 1 erwartete Violation in `ViolatingClass.cs`
  (Test-Fixture, kein Regress).

## 5. Konzept-Diskrepanzen-Bewertung

Der Coder hat 3 Konzept-Diskrepanzen dokumentiert (siehe Abschnitt 4).
Bewertung:

- **Vollständigkeit:** Ja, alle drei vom Coder gefundenen sind
  substantiiert (nicht „rein stilistisch"). Der Planer-Check 6 listet
  nur Z. 539-552 explizit; Z. 564 ist eine Coder-Eigenentdeckung
  (verdienstvoll), Z. 550 ist im Plan implizit (get_impact-Input-
  Tabelle Z. 105-107 weist auf P0-Spec). **Keine vierte oder
  fünfte Diskrepanz übersehen** (gesichtet: Z. 105-107, 188-190,
  215-233, 234-240, 257-264, 265-275, 305-315, 316-324, 539-552,
  550, 564, 622-624, 659-660 — alle sind entweder umgesetzt, korrekt
  in Doku abgebildet, oder als „geplant" markiert; der einzige
  inhaltliche Drift ist Z. 564, dokumentiert).

- **A7-Konformität bestätigt:** `konzept.md` ist im Working-Tree
  nicht modifiziert (`git diff origin/main..main --stat` zeigt
  nur Doku + neue Test-Datei + Task-Artefakte). Coder hat das
  Konzept nicht angefasst.

- **Empfehlung des Coders für Z. 564 (Umformulierung) sinnvoll:**
  Ja — die Formulierung suggeriert eine Funktionalität, die noch
  nicht existiert. Vorschlag „sollen unabhängig vom Ladezustand
  sofort bereitstehen — Fix siehe P0/P1-Rest" ist sachlich korrekt
  und sollte vom Nutzer bei nächster Konzept-Pflege-Gelegenheit
  übernommen werden (nicht in `fix-01` — gehört in eine
  Konzept-Pflege-Einheit).

## 6. Tech-Debt-Vorschläge

**Keine neuen TD-Einträge.** Begründung:

- F-001 (Zählfehler in `agent-api.md:238`) ist ein Doku-Drift im
  **eigenen Scope** dieser Einheit, nicht außerhalb. Korrekte
  Behandlung: `fix-01` in Einheit 008, kein TD-Eintrag.
- Die existierenden TD-001..TD-016a sind alle unverändert (kein
  neuer Anlass durch 008, da 008 keine Code-Änderungen außer der
  neuen Test-Datei enthält).
- Tool-Descriptions in den 3 Registrar-Klassen sind konsistent
  (C#-only-Hinweis in 6/9 Tools, Trunkierungs-Hinweis in 4
  Listen-Tools, kein `--mcp-log` versprochen, keine falschen
  Tool-Namen).
- `McpServerOptionsFactory.ServerInstructions` ist sachlich
  korrekt und stimmt 1:1 mit der Doku überein.
- `McpTruncation.cs:40, 66` und `LinterErrorCodes.cs:10-24` sind
  wortwörtlich in die Doku übernommen.

## 7. Zusammenfassung (für Orchestrator)

### Verdict

`issues` — ein MAJOR-F-001 (Doku-Drift in `agent-api.md:238`),
kein CRITICAL, drei MINOR, sonst alles sauber.

### Empfohlene Commits / nächste Einheit

**`fix-01` für Einheit 008** (kurz, ~10 min Coder-Aufwand):

1. Korrektur `Docs/agent-api.md:238` — Zählung „7 Tools sind
   C#-only" → „6 Tools sind C#-only", `search_pattern` aus der
   C#-only-Aufzählung raus, ggf. eigener Fallback-Satz.
2. (Optional, falls Konsistenz gewünscht) A3-Block in
   `units/008/result.md` angleichen: Dreischritt „Build grün →
   Test rot → Build grün + Test grün" für A3-1, A3-2, A3-3
   symmetrisch dokumentieren.
3. Neuer Commit: `docs(mcp): agent-api C#-only-zaehlung korrigiert
   [codegraph-mcp-server]` (Conventional Commits, Suffix).
4. Erneuter Volllauf: `dotnet test AiNetLinter.slnx --no-build`
   (1164/1164 grün, AGENTS.md §2).
5. `result.md` für `fix-01` schreiben (kurz: was + A3-Verifikation
   der Korrektur — z. B. Assertion auf den korrekten Wortlaut
   erweitern, sodass der Zählfehler künftig rot wird).
6. `review.md` für `fix-01` (Kritiker, Standard-Prozedere).

### Aufruf-Budget

Aktueller Stand: 1 Coder (008) + 1 Kritiker (008) verbraucht.
Mit `fix-01` zusätzlich: 1 Coder + 1 Kritiker = 23/40 verbraucht,
**17/40 verbleibend** für die P0/P1-Rest-Erweiterungen aus
der Roadmap (Kaltstart, Auto-Discovery, mtime-Sweep,
Verzeichnis-Sweep neu/gelöscht, `ILintConsole`, Last-Fixture,
`--mcp-log`, 7 weitere Punkte gemäß `Docs/ROADMAP.md`).

### Hinweis an den Nutzer (A7-Sache, nicht Teil von 008)

`konzept.md` enthält 3 veraltete Stellen (Z. 539-552 Tool-Status-
Tabelle, Z. 550 `get_impact`-Beschreibung, Z. 564 Kaltstart-Suggestion).
Empfehlung: bei nächster Konzept-Pflege-Gelegenheit in einer
eigenen Einheit an Code-Stand anpassen — nicht in `fix-01`,
weil A7 Konzept-Edits durch den Coder verbietet.

### Working-Tree / Push-Status

Stand jetzt: 7 Commits lokal, kein Push, Branch `main` 1 Commit
ahead of `origin/main`. Coder wartet auf `approved` (oder
`fix-01`-Freigabe). **Kein Push durch Kritiker** (A4).
