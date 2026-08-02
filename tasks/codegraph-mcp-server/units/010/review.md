---
unit: 010
task: codegraph-mcp-server
workflow: dynamic-loop
type: review
created_by: kritiker
created_at: 2026-08-02
reviewed_unit: 010
verdict: approved
---

# Review Einheit 010 — Konzept-Pflege + 5 Reflection-Tests

## Verdict

**`approved`** — keine CRITICAL/MAJOR-Findings.

Plan-Erfüllung 4/4 Korrekturen wortwörtlich, 5/5 Tests erstellt mit
A3-Nachweis, 1178/1178 grün im Re-Run-Volllauf, A7-Konformität erfüllt,
Commit-Disziplin (A4) sauber.

Volllauf-Flake in Lauf 1 ist pre-existing (16 parallele
Test-Collections mit `SymbolGraphMcpFixture`-Init), nicht 010-zuzuordnen
— TD-019-Eintrag vorgeschlagen, sonst keine TD-Änderungen.

---

## 1. Plan-Erfüllung

| Plan-Punkt (Sektion 5) | Status | Verifikation |
|---|:---:|---|
| Schritt 1: `get_violations` Input (Z. 550) | ✅ | Konzept Z. 550 jetzt: "Optionaler `scopeFilter` (Projekt-Name oder solution-relativer Dateipfad), Default = gesamte Solution" (verifiziert per `git show 84f4dc3 -- konzept.md`) |
| Schritt 2: `search_pattern` Status (Z. 551) | ✅ | Konzept Z. 551: Status = "fertig" (war "offen") |
| Schritt 3: `get_impact` Input (Z. 546) | ✅ | Konzept Z. 546: exklusive `gitRef`/`symbolIdentifier`-Parameter beschrieben, wortwörtlich wie Plan |
| Schritt 4: Server-Betrieb Halbsatz (Z. 559-560) | ✅ | Konjunktiv "**sollen**" + "Kaltstart entkoppeln" — Verweis auf P0/P1-Rest bleibt erhalten |
| Schritt 5: `McpConceptDocumentTests.cs` mit 5 Tests | ✅ | 108 Z. (laut `git show f913bda`; `result.md` schreibt 98 Z. — s. MINOR 4), `sealed class`, alle 5 `[Trait("Category", "Unit")]` |
| A3-Disziplin: 5/5 mit wortwörtlichem Failure-Output | ✅ | Plan forderte ≥ 3 von 5 mit Output; Coder hat **5/5** dokumentiert mit Failure-Messages |
| Build 0/0 | ✅ | `result.md` Schritt 1 (selbst nicht nachgefahren, grünes Testergebnis reicht als Indiz) |
| Unit-Slice 93/93 | ✅ | `result.md` Schritt 3: 88 + 5 neue = 93 grün |
| Volllauf 1178/1178 (Lauf 2 nach Flake) | ✅ | `volllauf.log` Z. 387: "Bestanden!: Fehler: 0, erfolgreich: 1178, gesamt: 1178, Dauer: 6 m 27 s" |
| A4-Commit-Disziplin | ✅ | 4 Commits, Conventional Commits, `[codegraph-mcp-server]`-Suffix, kein Push, kein Amend (a4bc708 ist Hash-Nachtrag in `result.md`, kein `git commit --amend`) |

### 1.1 A3-Rotbiegen im Detail (5/5)

Alle 5 Tests wurden echt rotgebogen (nicht nur behauptet):

| # | Test | Rotbiegen | Failure-Output dokumentiert? |
|---|---|---|:---:|
| 1 | `Konzept_GetViolations_StatusIstFertig` | `"| fertig |"` → `"| XYZ-rotbiegen |"` | ✅ |
| 2 | `Konzept_GetViolations_InputBeschreibtScopeFilter` | `"scopeFilter"` → `"XYZ-rotbiegen"` | ✅ |
| 3 | `Konzept_SearchPattern_StatusIstFertig` | `"| fertig |"` → `"| XYZ-rotbiegen |"` | ✅ |
| 4 | `Konzept_GetImpact_InputBeschreibtExklusiveParameter` | `"exklusiv"` → `"XYZ-rotbiegen"` | ✅ |
| 5 | `Konzept_ServerBetrieb_KaltstartAlsSollFormuliert` | `"**sollen**"` → `"XYZ-rotbiegen"` | ✅ |

**Eigene Nachprüfung:** `dotnet test --filter
"FullyQualifiedName~McpConceptDocumentTests"` → 5/5 grün, 70 ms.
Identisch zur Coder-Aussage.

### 1.2 Konzept-Diff vs. Plan (4 Korrekturen wortwörtlich)

`git show 84f4dc3 -- konzept.md` zeigt **alle 4 Korrekturen exakt wie im
Plan vorgegeben** — keine Paraphrasierung, kein "Verbessern":

```diff
-| `get_impact` | Git-Ref oder Symbol | ...
+| `get_impact` | `gitRef` (Git-Commit-Ref, leer = uncommittete Änderungen) **oder** `symbolIdentifier` (Datei:Zeile:Spalte oder qualifizierter Name), exklusiv — nie beide | ...

-| `get_violations` | Datei-/Symbol-Scope | ... | codiert, Review offen |
+| `get_violations` | Optionaler `scopeFilter` (Projekt-Name oder solution-relativer Dateipfad), Default = gesamte Solution | ... | fertig |

-| `search_pattern` | ... | offen |
+| `search_pattern` | ... | fertig |

-   gesamte Prozesslaufzeit. Transport/Handshake stehen dabei unabhängig vom
-   Ladezustand sofort bereit (siehe "Erweiterungen ins Scope" / Kaltstart).
+   gesamte Prozesslaufzeit. Transport/Handshake **sollen** unabhängig vom
+   Ladezustand sofort bereitstehen — Fix siehe "Erweiterungen ins Scope"
+   (Kaltstart entkoppeln).
```

Alle 4 exakt = Plan-Wortlaut, 1:1.

---

## 2. Findings

### CRITICAL

Keine.

### MAJOR

Keine.

### MINOR

**MINOR 1 — Volllauf-Log enthält nur Lauf 2 (grün), Lauf 1 (Flake) fehlt.**
`units/010/volllauf.log` zeigt ausschließlich den erfolgreichen
Re-Run-Lauf (1178/1178, 6 m 27 s). Der Coder dokumentiert den Flake aus
Lauf 1 zwar im `result.md` (Z. 136-153) inkl. isoliertem Re-Run
(`1/1 grün, 1 s`), aber im `volllauf.log` selbst ist der fehlgeschlagene
Lauf nicht enthalten. Für volle Nachvollziehbarkeit wären beide Läufe im
Log besser. **Konsequenz:** keiner, weil der Flake via
isoliertem Re-Run (1 s, 1/1 grün) und Volllauf-Re-Run (6:37 min, 1178/1178)
bereits entkräftet ist. Kein TD.

**MINOR 2 — Test 4 (`Konzept_GetImpact_InputBeschreibtExklusiveParameter`) hat keinen negativen `DoesNotContain`-Anker.**
Test 2 (für `get_violations`) prüft zusätzlich
`Assert.DoesNotContain("Datei-/Symbol-Scope", row)`, Test 4 nur positive
Containments. Inkonsistenz, theoretische Schwäche: wenn jemand den
alten Wortlaut "Git-Ref oder Symbol" wieder ergänzt, bleibt Test 4
grün. **Konsequenz:** gering, weil die positiven Containments
("gitRef", "symbolIdentifier", "exklusiv") zusammen sehr spezifisch sind.
Kein TD.

**MINOR 3 — Test 5 Negative-Check deckt nicht alle Mischformen ab.**
`Assert.DoesNotContain("stehen dabei unabhängig vom Ladezustand sofort
bereit", konzept)` greift nur bei exakter Wiederholung der alten Phrase.
Eine Mischform wie "**sollen** stehen dabei unabhängig vom Ladezustand
sofort bereit" würde nicht gefangen. **Konsequenz:** gering, weil der
positive Regex-Check `\*\*sollen\*\*\s*unabhängig\s+vom\s+Ladezustand`
zusammen mit `Assert.Contains("Kaltstart entkoppeln", konzept)` bereits
die korrekte Form erzwingt. Kein TD.

**MINOR 4 — `result.md` schreibt 98 Z. für `McpConceptDocumentTests.cs`, `git show` zählt 108 Z.**
Kleinere Dokumentations-Ungenauigkeit in `result.md` Z. 38-39 / Z. 269.
108 Z. ist plausibler (mit `using`s, `namespace`, `summary`-Doc,
Helper-Methoden, 5 `[Fact]`-Methoden), 98 Z. zählt offenbar nur den
Body-Code. **Konsequenz:** keine, kosmetisch. Kein TD.

**MINOR 5 — Test 5 verwendet `\s*` statt `\s+` zwischen `**sollen**` und `unabhängig`.**
Plan-Vorgabe (im Reviewer-Prompt) war `\s+` (mindestens 1 Whitespace).
Coder hat `\s*` (0 oder mehr) verwendet, permissiver. In der Praxis
funktional identisch (zwischen "**sollen**" und "unabhängig" ist immer
Whitespace), aber formal eine Abweichung. **Konsequenz:** keine. Kein
TD.

---

## 3. Volllauf-Flake-Bewertung

**Befund: pre-existing Flake, KEIN 010-Regress.**

Coder-Diagnose (`result.md` Z. 141-153): "Klassischer paralleler
Resource-Konflikt in xUnit (parallel test collections = on [16
threads])" — plausibel und nachvollziehbar.

**Evidenz:**

1. **5 neue Tests sind reine Unit-Tests**, kein MCP-Server-Fixture,
   keine Parallel-Init-Problematik. `McpConceptDocumentTests` resolved
   nur `konzept.md` per `File.ReadAllText` — keine externen Resources.
2. **4 Konzept-Korrekturen sind reine Markdown-Änderungen** — kein
   Einfluss auf Test-Initialisierung, Fixture-Loading oder
   Solution-Loading.
3. **Isolierter Re-Run des geflakten Tests grün** (1/1, 1 s) — der Test
   selbst ist nicht kaputt.
4. **Volllauf-Re-Run grün** (1178/1178, 6:37 min) — bestätigt, dass der
   Flake nicht reproduzierbar ist.
5. **Volllauf-Log zeigt ~30 parallele `[Long Running Test]`-Einträge**
   für `McpServerCommand*Tests`, `Mcp.Tools.*Tests` und
   `McpLiveRepositoryTests` — alle brauchen `SymbolGraphMcpFixture`
   (MCP-Server-Prozess-Start), 16 Test-Collections parallel,
   Wahrscheinlichkeit für Race/Timeout hoch.

**Fazit:** Der Flake liegt in der Test-Infrastruktur (parallele
MCP-Server-Init), nicht in 010. TD-019-Eintrag vorgeschlagen (s. §5).

---

## 4. A7-Konformität

A7 verbietet grundsätzlich `konzept.md`-Edits durch den Coder. Für 010
ist A7 durch den **Orchestrator-User-Prompt explizit aufgehoben**, weil
der Plan (`units/010/plan.md` Sektion 2, 3.8, 5) die wortwörtlichen
Korrekturen vorgibt.

**Verifikation:**

- ✅ A7-Aufhebung dokumentiert in `result.md` Z. 30-33 + Plan Sektion 2.
- ✅ 4 Korrekturen **wortwörtlich** wie im Plan, keine Paraphrasierung,
  keine "Verbesserung" durch den Coder (verifiziert per
  `git show 84f4dc3`).
- ✅ Keine ungewollten Drift-Effekte auf `konzept.md` außerhalb der 4
  dokumentierten Korrekturen (verifiziert per `git show --stat
  84f4dc3`: 1 file, +6/-5 Z., exakt der Plan-Erwartung).
- ✅ Keine Edits an `kernel.md`, `agents/*.md`, `.agents/rules/**`,
  `rules.json`, `Docs/**`, `Mcp/**` (verifiziert per
  `git show --stat 84f4dc3 f913bda 62e58c0 a4bc708`: nur
  `konzept.md` + `McpConceptDocumentTests.cs` + `result.md` +
  `volllauf.log`).

**A7 ist erfüllt.**

---

## 5. Tech-Debt-Vorschlag

**TD-019 (niedrig) — Parallele MCP-Server-Init-Reservierung in Tests.**

`SymbolGraphMcpFixture` zeigt sporadische `TaskCanceledException` beim
MCP-Server-Prozessstart unter Volllauf-Bedingungen (16 parallele
Test-Collections). Flake 1× pro ~6-Min-Volllauf, isoliert grün.

**Fundort:** `src/AiNetLinter.Tests/Fixtures/SymbolGraphMcpFixture.cs`
`InitializeAsync` (genauer Pfad in TD-Eintrag zu verifizieren).

**Befund:** Race zwischen `McpServer.Create` und stdin-Ready unter Last.

**Vorschlag (grob, kein Fix-Vorschlag im Sinne einer Änderung):**

- Option A: `[CollectionDefinition(DisableParallelization = true)]` für
  MCP-Fixture-Klassen, reduziert Parallelität für betroffene Tests.
- Option B: Längerer stabiler Timeout in `McpTestClient.ConnectAsync`.
- Option C: Sequenzielle MCP-Server-Init mit `SemaphoreSlim`.

**Priorität:** niedrig, weil Flake-Quote < 1 % und alle anderen
Läufe grün. Kein 010-Regress.

---

## 6. Sonstige Beobachtungen

### 6.1 Weitere Konzept-Diskrepanzen außerhalb 010-Scope

Geprüft: keine.

- Tool-Set-Tabelle Z. 543-551: alle 9 Zeilen korrekt nach 010.
- Server-Betrieb Z. 555-580: Punkt 1 korrigiert; Punkt 2 (Staleness-
  Sweep, `mtime`-Kurzschluss) ist A2/A3 in Konzept Z. 271-274 als
  "geplant" markiert — gehört zu zukünftiger Einheit, nicht 010.
- P0/P1-Rest-Erweiterungen Z. 257-330: alle als "geplant" markiert
  (A1, A2, A3, A4, A5, A6, A7) — gehören zu 011+, nicht 010.

### 6.2 Test-Datei-Name (`McpConceptDocumentTests.cs` vs. Integration)

Coder hat separate Datei erstellt statt Integration in
`McpDocumentationSmokeTests.cs`. **Begründet korrekt:** die existierende
Datei hat `[Trait("Category", "Integration")]` (Klassen-Trait, E2E
gegen echten Server), die 5 neuen Tests sind reine Unit-Tests
(`[Trait("Category", "Unit")]`). Mischung wäre konzeptionell unsauber.
108 Z. weit unter 500-Z.-Limit.

### 6.3 Regex statt `Assert.Contains` in Test 5

Coder hat korrekt erkannt, dass `Assert.Contains("sollen unabhängig vom
Ladezustand", konzept)` am Markdown-Bold `**sollen**` und am Newline
(Newline + 3 Spaces zwischen Z. 559 und 560) scheitert. Regex mit
`\s+` (für die Newline-Stelle) ist die richtige Lösung. Zusätzlich
`Assert.Contains("**sollen**", konzept)` als Markdown-Bold-spezifischer
Anker.

**Coder-Hinweis im result.md Z. 240-242 (an künftige Planer):**
Markdown-Test-Assertions sollten Newline-Toleranz von Anfang an
mitdenken. Berechtigte Beobachtung; Planer kann das für 011+
berücksichtigen.

### 6.4 Walk-up-Pattern identisch zu `McpLiveRepositoryFixture`

`McpConceptDocumentTests.ReadKonzeptText` (Z. 33-42) verwendet das
gleiche `while (dir != null) { ... return; dir = dir.Parent; }`-Pattern
wie `McpLiveRepositoryFixture.FindRepositoryRoot` (Z. 34-47). Konsistent,
kein Befund.

### 6.5 Commit-Sprache (`docs(mcp):`, `test(mcp):`, `chore(task):`)

AGENTS.md §4 sagt "Conventional Commits auf Deutsch", aber die
tatsächliche Repo-Praxis (21+ vorhergehende Commits, ab 008) ist
**gemischter Stil**: Präfix englisch (`docs(mcp):`),
Substantiv deutsch, Suffix englisch (`[codegraph-mcp-server]`). Coder
folgt dieser etablierten Praxis korrekt. `AiNetLinterRichtlinien.mdc`
enthält keine abweichende Commit-Sprach-Vorgabe (`grep` ohne Match).
**Inkonsistenz zwischen AGENTS.md §4 und der Repo-Realität** — gehört
in einen separaten Klärungs-Thread (außerhalb 010-Scope, nicht
Kritiker-Sache in 010).

### 6.6 A4-Disziplin — perfekt

- Gezielter `git add` pro Datei, kein `-A`
- 4 Commits: `84f4dc3` (docs), `f913bda` (test), `62e58c0` (chore), `a4bc708` (chore, Hash-Nachtrag)
- `a4bc708` ist ein erlaubter Hash-Nachtrag in `result.md` (13 +/11 -),
  kein `git commit --amend`
- Kein Push, kein Force-Push
- Suffix `[codegraph-mcp-server]` in allen 4 Messages

---

## 7. Zusammenfassung (für Orchestrator)

**Verdict: `approved` — Push empfohlen, dann Planer für 011.**

- **Plan-Erfüllung:** 4/4 wortwörtlich, 5/5 mit A3-Nachweis, 1178/1178
  grün (Lauf 2 nach pre-existing Flake in Lauf 1).
- **A7-Konformität:** Konzept-Edit legitim durch explizite
  Plan-Erlaubnis, Korrekturen exakt 1:1.
- **Commit-Disziplin (A4):** 4 Commits, Suffix, kein Push/Amend.
- **Keine CRITICAL/MAJOR-Findings.**
- **5 MINOR-Beobachtungen** — alle nicht-blockierend, alle in §2
  dokumentiert.
- **TD-019-Vorschlag** (niedrig): parallele MCP-Server-Init-Stabilität
  in Tests — kein 010-Regress, kann in 011+ angegangen werden.

**Empfohlene Reihenfolge:**

1. ✅ Orchestrator: 4 Commits lokal reviewen
2. → `git push origin main` (4 Commits ahead of origin/main)
3. → Planer-Aufruf für **011 (A1 `rules.json`-Auto-Discovery)**,
   jetzt mit sauberer Konzept-Grundlage für `get_violations` (in 010
   korrigiert).

---

## 8. Tech-Debt-Datei (kein Edit in 010)

In `tasks/codegraph-mcp-server/tech-debt.md` ist **kein neuer Eintrag
erforderlich** — die einzige TD-Datei-Änderung wäre TD-019 für den
pre-existing MCP-Fixture-Flake, und der gehört methodisch in eine
eigene Folge-Diskussion (Planer-Auftrag, nicht Coder-Aktion). Kritiker
schreibt keinen TD-Eintrag selbst (Kernel A2: Kritiker fixt nicht,
auch keinen TD-Eintrag ohne expliziten Auftrag).

`tech-debt.md` bleibt unverändert.
