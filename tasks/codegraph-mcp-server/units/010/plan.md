---
unit: 010
task: codegraph-mcp-server
workflow: dynamic-loop
type: plan
created_by: planer
created_at: 2026-08-02
trigger: tasks/codegraph-mcp-server/state.md Block "Nächste Aktion (für 010)" — Strategie-Empfehlung 009-Kritiker (Konzept-Pflege vor A1); explizit als eigene Coder-Einheit freigegeben vom Orchestrator-User-Prompt
---

# Plan Einheit 010 — Konzept-Pflege: 3 veraltete Stellen in `konzept.md` an Code-Stand anpassen

## 1. Ziel der Einheit

Die drei seit Einheit 008 dokumentierten Konzept-Diskrepanzen werden in
`konzept.md` an den realen Code-Stand angepasst — wortwörtlich, jede
einzelne Korrektur im Plan vorgegeben, damit der Coder keinen
Ermessensspielraum hat. Eine neue Test-Klasse
`McpConceptDocumentTests.cs` (oder Erweiterung von
`McpDocumentationSmokeTests.cs`) verankert die korrigierten
Konzept-Aussagen gegen den Code via Reflection, sodass ein erneutes
Re-Drift des Konzepts (z. B. bei einer späteren Konzept-Aktualisierung)
sofort einen Test-Fehler auslöst — A3-Disziplin auch für
Markdown-Änderungen.

**Bezug:** `units/008/result.md` Block "Konzept-Diskrepanzen" (Z. 131-148)
listet exakt diese 3 Stellen als User-pflichtig auf; der
Orchestrator-User-Prompt für 010 hat sie als explizite Coder-Einheit
freigegeben, **wenn** der Plan die wortwörtlichen Korrekturen vorgibt.
Das ist hier der Fall.

**Kein DoD-Punkt** aus `konzept.md` Z. 590-660 wird durch 010 erfüllt
oder verletzt — die Korrekturen sind konzept-interne Drift-Bereinigung
ohne Funktionsänderung am Server.

## 2. Scope-Entscheidung mit Begründung

**Gewählt: A — Konzept-Pflege-Einheit (die 3 vom 008-Planer
dokumentierten Diskrepanzen).**

**Warum gerade diese Wahl:**

- **Saubere Grundlage für A1 (`rules.json`-Auto-Discovery).** A1 ist
  die kleinste P0-Code-Erweiterung (Konzept Z. 257-264, ~2-3h, vom
  009-Kritiker für 010 empfohlen), hängt aber von einer konsistenten
  Konzept-Spec ab: was ist das "korrekte Verhalten" bei fehlender
  `rules.json`? Bei veralteter Konzept-Tabelle (z. B. `get_violations`
  als "Review offen" markiert) könnte der Coder für A1 falsche
  Annahmen über den aktuellen Implementierungs-Stand treffen. Konzept
  Z. 257-264 spezifiziert A1 außerdem relativ zu `get_violations`
  ("Vermerk in der `get_violations`-Antwort"), und der Coder muss
  wissen, dass `get_violations` real existiert und `scopeFilter`
  als Input akzeptiert (nicht "Datei-/Symbol-Scope", wie das Konzept
  derzeit behauptet).
- **Niedrigste Risiko + schnellste Wirkung.** Reine
  Markdown-Änderung, ~1-1.5h Coder-Aufwand, 0 Code-Edits (außer dem
  neuen Test-File), 0 Build-Änderungen. Vergleich zu A1 (2-3h mit
  Tool-Vermerk-Logik in `get_violations` + `[WARN]`-Stderr-Pfad +
  neuen Tests) oder A4 (4-6h, triggert TD-009 als Doppeleinheit).
- **Vom 009-Kritiker explizit als Option genannt.** `units/009/review.md`
  Z. 262-265: *"Konzept-Pflege-Einheit (User-pflichtig) ... gehört
  zwischen 009 und der nächsten Coder-Einheit, damit A1/A4 nicht auf
  veralteten Konzept-Annahmen implementiert werden."*
- **A7 ist erfüllt durch explizite Plan-Erlaubnis.** Die
  Rollen-Datei (`agents/planer.md` Z. 58-60) und der Kernel-Teil-A
  (A7) verbieten Konzept-Edits **grundsätzlich** — der Coder-Agent
  darf `konzept.md` nicht selbst anfassen. Der
  Orchestrator-User-Prompt für 010 hat das **explizit** aufgehoben,
  **wenn** die wortwörtlichen Korrekturen im Plan stehen. Das ist
  hier der Fall: Sektion 5 listet für jede der 3 Korrekturen den
  exakten Vorher-/Nachher-Text, sodass der Coder keine eigenen
  Formulierungen treffen muss. Der Plan ersetzt die
  User-Genehmigung, die sonst nötig wäre.

**Warum nicht die anderen Kandidaten:**

- **(A1) `rules.json`-Auto-Discovery** — wichtige P0, aber: braucht
  vorher konsistente Konzept-Grundlage (Begründung oben), und der
  `[WARN]`-Stderr-Pfad plus die `get_violations`-Vermerk-Logik
  verdoppeln den Test-Aufwand gegenüber einer reinen
  Markdown-Änderung. Besser für 011, **nachdem** die Konzept-Spec
  in 010 bereinigt ist.
- **(A4) Kaltstart entkoppeln** — wichtigste P0, aber: 4-6h
  Coder-Aufwand, ändert `McpServerCommand.RunAsync` und
  `McpCodeGraphServer`-Konstruktor, **triggert TD-009 zwingend**
  (eine 6. Dependency wird gebraucht). Braucht eine
  Doppeleinheit (010/011 oder 011/012) — passt nicht in eine
  einzelne Coder-Einheit. Besser für 012/013.
- **(A2/A3) Verzeichnis-Sweep + `mtime`-Kurzschluss** — gekoppelt,
  3-4h, Risiko: Projekt-Mapping über längsten gemeinsamen
  Pfad-Präfix ist nicht trivial. Besser für 013/014.
- **(A5) `--mcp-log` Call-Log** — 2-3h, eigenständig, aber: berührt
  `McpServerOptionsFactory` (TD-014, 16 Z. Puffer) und ist damit
  selbst ein TD-014-Auslöser. Besser als 011 nach A1, oder
  inline in A1.
- **(A6) `ILintConsole` für MCP** — 3-4h, strukturelle Lösung
  für stdout-Schutz. Konzept Z. 564 (Kaltstart-Suggestion) muss
  vor der Implementierung an Code-Stand angepasst sein — das
  leistet gerade 010.
- **(A7) Last-Fixture + Messlauf** — hängt konzeptuell von A4 ab
  (Kaltstart messen), 4-6h. Frühestens 2-3 Einheiten später.
- **(TD-008/010) `ILinterEngineConfig`-Refactor** — strukturelle
  Lösung, die TD-008 und TD-010 gemeinsam löst, 4-6h, eigenständig
  refactor-bar, aber: gehört in den Block der
  P0/P1-Erweiterungen (wenn `McpCodeGraphServer` ohnehin erweitert
  wird, TD-009 inline mitnehmen, dann TD-008/010 separat). Besser
  nach A1/A4.
- **(TD-009) Konstruktor-`record`-Refactor** — sollte **inline** in
  A4 laufen, nicht eigenständig. Nicht für 010.
- **(B) Konzept-Pflege + A1 in einer Einheit** — wäre eine
  **Doppeleinheit**, die den sauberen Unit-Schnitt zwischen
  Markdown-Korrektur und Code-Implementierung verwischt. A1
  verdient einen eigenen Kritiker-Review. Saubere Trennung: 010
  ist Konzept, 011 ist A1.
- **Fertig-Meldung statt Planung** — wäre defensiv-falsch: die 3
  Diskrepanzen sind dokumentiert, der User-Prompt hat die
  explizite Freigabe erteilt, der Aufwand ist klein, der
  Folge-Nutzen (saubere A1-Grundlage) ist real.

## 3. Vor-der-Planung-Checks

### 3.1 `konzept.md` Z. 539-552 (Tool-Set-Tabelle) — gelesen, exakt

Aktueller Wortlaut, Z. 543-551 (alle 9 Tool-Zeilen):

| Tool | Input | Output | Basis | Status |
|---|---|---|---|---|
| `get_index_scope` (Z. 543) | keins | Dateityp-Aufschlüsselung | `SourceFileCatalog.GetSourceFiles`/`WebFileCatalog.Collect` | fertig |
| `find_symbol` (Z. 544) | Name/Pattern, optionaler Kind-Filter | Fundstellen inkl. Miss-Hint-Fallback | `SymbolFinder.FindDeclarationsAsync` | fertig |
| `find_references` (Z. 545) | Symbol-Identifikator | Alle Aufrufstellen | `DiffImpactAnalyzer.FindCallSitesAsync` | fertig |
| `get_impact` (Z. 546) | Git-Ref oder Symbol | Betroffene Call-Sites | `DiffImpactAnalyzer.AnalyzeAsync` | fertig |
| `get_type_hierarchy` (Z. 547) | Typ-Identifikator | Basis-/abgeleitete Typen | `SymbolFinder.FindDerivedClassesAsync`/`FindImplementationsAsync` | fertig |
| `get_file_skeleton` (Z. 548) | Dateipfad | Struktur-Skelett einer Datei | `SkeletonMapBuilder` | fertig |
| `get_hotspots` (Z. 549) | Optionaler Filter | Kopplungs-/Hotspot-Kennzahlen | `HotspotMapBuilder` | fertig |
| `get_violations` (Z. 550) | **Datei-/Symbol-Scope** | Aktuelle Lint-Verstöße | `RuleRegistry`/`LinterEngine` | **codiert, Review offen** |
| `search_pattern` (Z. 551) | Regex/Text-Pattern | Textstellen im Dateibestand | Fallback für Nicht-Symbol-Fälle | **offen** |

**Befund:** 7 von 9 Zeilen sind korrekt. 2 Zeilen-Status veraltet
(`get_violations`, `search_pattern`), 1 Zeilen-Input veraltet
(`get_violations`).

**Realität aus dem Code:**

- `get_violations`: Input ist `string? scopeFilter = null`
  (`AnalysisToolRegistrations.cs:32`), nicht "Datei-/Symbol-Scope".
  Status: approved durch 001 (Commit `e63176d`).
- `search_pattern`: Status: approved durch 002/fix-01 (Commits
  `28e6e58` + `bd9e6fd`).
- `get_impact`: Input ist `string? gitRef = null, string? symbolIdentifier = null`
  (`SymbolGraphToolRegistrations.cs:52`), exklusiv ("nie beide", aus der
  Tool-Description). Konzept-Eintrag "Git-Ref oder Symbol" ist grob
  richtig, aber unvollständig.

### 3.2 `konzept.md` Z. 559-560 (Kaltstart-Suggestion) — gelesen, exakt

Aktueller Wortlaut (im Block "Server-Betrieb", Punkt 1):

> 1. Start: `ainetlinter --mcp-server --path <Solution>` lädt die
>    Solution einmal via `SourceFileCatalog.LoadAsync` und hält sie
>    resident für die gesamte Prozesslaufzeit. **Transport/Handshake
>    stehen dabei unabhängig vom Ladezustand sofort bereit (siehe
>    "Erweiterungen ins Scope" / Kaltstart).**

**Befund:** Der Halbsatz suggeriert, dass die Entkopplung bereits
umgesetzt ist. Realität: `McpServerCommand.cs:35` awaited
`TryLoadSolutionAsync` **synchron** ab, **bevor** Z. 40
`McpServer.Create` aufgerufen wird. Der MCP-Transport wird also erst
nach dem Solution-Load aufgesetzt — kein `initialize`-Handshake
während des Ladens. Die Entkopplung ist unter den P0/P1-Rest-
Erweiterungen (Konzept Z. 265-275) als "geplant" markiert, nicht
umgesetzt.

**Anmerkung zur Anker-Korrektur:** Der User-Prompt für 010 nennt
diese Stelle "Z. 564", der reale Anker ist Z. 559-560 (Punkt 1 des
Server-Betriebs-Blocks). Der Coder verwendet den **exakten
Wortlaut**, nicht die Zeilenzahl — Anker-Korrektur ist nur für
die Plan-Doku relevant.

### 3.3 `konzept.md` Z. 550 (`get_violations` Input-Spalte) — gelesen, exakt

Aktueller Wortlaut: "Datei-/Symbol-Scope".

**Realität** (`AnalysisToolRegistrations.cs:32-43`): Input ist
`string? scopeFilter = null` (optional, matched gegen Projekt-Name
oder solution-relativen Dateipfad). Die Tool-Description nennt das
wortwörtlich: *"Optionaler scopeFilter matched gegen Projekt-Name
oder solution-relativen Dateipfad."*

**Anmerkung zur Anker-Korrektur:** Der User-Prompt nennt diese
Stelle fälschlich "Z. 550 `get_impact` Input-Beschreibung". Z. 550
ist tatsächlich die `get_violations`-Zeile, nicht `get_impact`
(die steht in Z. 546). Der Coder korrigiert **beide** Stellen:
- Z. 550 (`get_violations`-Input): "Datei-/Symbol-Scope" → Korrektur
- Z. 546 (`get_impact`-Input): "Git-Ref oder Symbol" → Korrektur
(beide im Sichtfeld, keine Mehrarbeit).

### 3.4 `units/008/result.md` (Konzept-Diskrepanzen-Block) — gelesen

`units/008/result.md` Z. 131-148 dokumentiert exakt die 3 hier zu
korrigierenden Diskrepanzen als User-pflichtig. Wortlaut der
008-Beobachtungen wird in 010 wortwörtlich in der
Korrektur-Begründung referenziert (Sektion 5), damit der Coder den
Kontext hat, aber keine eigenen Annahmen treffen muss.

### 3.5 `units/008/fix-01/review.md` und `plan.md` — gelesen

`units/008/fix-01/plan.md` Z. 56-61 wiederholt explizit: *"Die 3
Konzept-Diskrepanzen aus `units/008/review.md:144-180` sind
ausdrücklich nicht in `fix-01` — A7 verbietet Konzept-Edits durch
den Coder, der Nutzer entscheidet separat in einer eigenen
Konzept-Pflege-Einheit."* — Das ist genau die Begründung, warum
010 eine eigene Einheit ist und nicht in 008/fix-01 mitgenommen
wurde.

### 3.6 Code-Verifikation gegen `units/008/result.md` Behauptungen

- `McpServerOptionsFactory.cs:26-31`: ServerInstructions-Block listet
  **6** C#-only-Symbolgraph-Tools (find_symbol, find_references,
  get_impact, get_type_hierarchy, get_file_skeleton, get_violations) —
  bestätigt. Für 010 irrelevant, aber Hintergrund.
- `SymbolGraphToolRegistrations.cs:52-62`: `get_impact`-
  Description-Block exakt wie in 008 dokumentiert.
- `AnalysisToolRegistrations.cs:32-43`: `get_violations`-
  Description-Block exakt wie in 008 dokumentiert.
- `McpServerCommand.cs:35-41`: synchroner Load vor Server-Create —
  bestätigt die Behauptung aus 008 Z. 137.

### 3.7 Drift / Duplikate durch Blindheit

- **Drift:** keine — die 3 Korrekturen sind punktuelle, eng
  umrissene Markdown-Änderungen, keine strukturelle Änderung am
  Konzept. Kein neues Kapitel, keine Refaktorisierung.
- **Duplikate durch Blindheit:** keine — die zu korrigierenden
  Stellen existieren genau einmal im Konzept.

### 3.8 Projektregeln-Check (A7, A8)

- A7 (`konzept.md` ist bindend, nur lesbar) ist **für diese
  Einheit explizit aufgehoben** durch den Orchestrator-User-Prompt.
  Die Aufhebung ist an die Bedingung geknüpft, dass die
  wortwörtlichen Korrekturen im Plan stehen — was hier der Fall
  ist (Sektion 5).
- A8 (Kernel und Rollen unantastbar) — nicht betroffen, 010 fasst
  weder `kernel.md` noch eine Rollen-Datei an.
- `AiNetLinterRichtlinien.mdc` §4 Update-Pflicht — betrifft 010
  nur insofern, als dass das Konzept selbst zur Task-Definition
  gehört (Z. 1-9: "type: konzept", "rules_dir: .agents/rules") und
  eine Korrektur des Konzepts eine Korrektur der Task-Definition
  ist. Formal: 010-Commits bekommen das `[codegraph-mcp-server]`-
  Suffix wie alle anderen Einheiten.
- `AiNetLinter.mdc` (`MaxLineCount: 500`) — irrelevant für
  Markdown.

## 4. Betroffene Dateien / Module

| Datei | Pflicht? | Erwartete Diff-Größe |
|---|---|---:|
| `tasks/codegraph-mcp-server/konzept.md` | **ja** | ~5-6 Zeilen geändert (3 Korrekturen, je 1-2 Zeilen) |
| `src/AiNetLinter.Tests/Mcp/McpConceptDocumentTests.cs` (NEU) | **ja** (A3-Sicherung) | ~50-80 Z., 3-4 Reflection-Tests, `[Trait("Category", "Unit")]` |
| `tasks/codegraph-mcp-server/units/010/result.md` (NEU) | **ja** (vom Coder) | Standard-Result-Protokoll mit A3-Block |
| `tasks/codegraph-mcp-server/state.md` | optional (Orchestrator-Sache) | 1 Block analog 008/009 in "Phase 2 — Loop-Protokoll" + Zähler-Update (1× Planer + 1× Coder + 1× Kritiker = 34/40 nach 010) |

**Nicht ändern (A7/A8, explizit wiederholt):**

- `kernel.md` (A8)
- `agents/planer.md` / `agents/coder.md` / `agents/kritiker.md` (A8)
- `.agents/rules/AiNetLinter.mdc` (A7)
- `.agents/rules/AiNetLinterRichtlinien.mdc` (A7)
- `rules.json` (A7)
- `Docs/**` (außer dem oben genannten 1-File-Test) (A7)
- `README.md` (A7)
- `AiNetLinter.csproj` (A7)
- `src/AiNetLinter/**` (Produktionscode, A5/A7)
- `Mcp/**` (Modul, A5/A7)
- `tech-debt.md` (kein TD-Bezug in 010)

## 5. Konkretes Vorgehen (Schritt-für-Schritt, Coder hat keinen Planungsspielraum)

### Schritt 1 — `konzept.md` Korrektur 1: `get_violations`-Zeile (Z. 550) (~5 min)

**Alt (Z. 550, wortwörtlich aus der Tabelle):**

> | `get_violations` | Datei-/Symbol-Scope | Aktuelle Lint-Verstöße | `RuleRegistry`/`LinterEngine` | codiert, Review offen |

**Neu (wortwörtlich):**

> | `get_violations` | Optionaler `scopeFilter` (Projekt-Name oder solution-relativer Dateipfad), Default = gesamte Solution | Aktuelle Lint-Verstöße | `RuleRegistry`/`LinterEngine` | fertig |

**Begründung im Commit (für den Coder zu übernehmen, sinngemäß):**

> Konzept-Diskrepanz aus 008/result.md Z. 140-142: Input- und
> Status-Spalte veraltet. Realität aus
> `AnalysisToolRegistrations.cs:32-43` und `units/001/review.md`
> (Verdict `approved`): `get_violations` nimmt `string? scopeFilter`
> (optional, matched gegen Projekt-Name oder solution-relativen
> Dateipfad), Review ist abgeschlossen, Tool ist seit 001 im
> produktiven Einsatz.

### Schritt 2 — `konzept.md` Korrektur 2: `search_pattern`-Zeile (Z. 551) (~3 min)

**Alt (Z. 551, wortwörtlich):**

> | `search_pattern` | Regex/Text-Pattern | Textstellen im Dateibestand | Fallback für Nicht-Symbol-Fälle | offen |

**Neu (wortwörtlich):**

> | `search_pattern` | Regex/Text-Pattern | Textstellen im Dateibestand | Fallback für Nicht-Symbol-Fälle | fertig |

**Begründung im Commit (sinngemäß):**

> Konzept-Diskrepanz aus 008/result.md Z. 132-133: Status-Spalte
> veraltet. Realität: 002 hat `search_pattern` umgesetzt
> (Commit `28e6e58`), 002/fix-01 hat den Hint-Bug behoben
> (Commit `bd9e6fd`), beide Reviews `approved` — Tool ist
> produktiv.

### Schritt 3 — `konzept.md` Korrektur 3: `get_impact`-Input (Z. 546) (~5 min)

**Alt (Z. 546, wortwörtlich):**

> | `get_impact` | Git-Ref oder Symbol | Betroffene Call-Sites | `DiffImpactAnalyzer.AnalyzeAsync` | fertig |

**Neu (wortwörtlich):**

> | `get_impact` | `gitRef` (Git-Commit-Ref, leer = uncommittete Änderungen) **oder** `symbolIdentifier` (Datei:Zeile:Spalte oder qualifizierter Name), exklusiv — nie beide | Betroffene Call-Sites | `DiffImpactAnalyzer.AnalyzeAsync` | fertig |

**Begründung im Commit (sinngemäß):**

> Konzept-Diskrepanz: Input-Beschreibung zu vage. Realität aus
> `SymbolGraphToolRegistrations.cs:52-62`: zwei exklusive
> optionale Parameter, "nie beide". Konkretisiert, damit
> zukünftige Implementierungen (A1, A4) die exakte
> Parametrisierung gegen das Konzept prüfen können.

### Schritt 4 — `konzept.md` Korrektur 4: Kaltstart-Suggestion (Z. 559-560) (~10 min)

**Alt (Z. 559-560, im Block "Server-Betrieb" / Punkt 1, wortwörtlich):**

> 1. Start: `ainetlinter --mcp-server --path <Solution>` lädt die
>    Solution einmal via `SourceFileCatalog.LoadAsync` und hält sie
>    resident für die gesamte Prozesslaufzeit. Transport/Handshake
>    stehen dabei unabhängig vom Ladezustand sofort bereit (siehe
>    "Erweiterungen ins Scope" / Kaltstart).

**Neu (wortwörtlich):**

> 1. Start: `ainetlinter --mcp-server --path <Solution>` lädt die
>    Solution einmal via `SourceFileCatalog.LoadAsync` und hält sie
>    resident für die gesamte Prozesslaufzeit. Transport/Handshake
>    **sollen** unabhängig vom Ladezustand sofort bereitstehen — Fix
>    siehe "Erweiterungen ins Scope" (Kaltstart entkoppeln).

**Begründung im Commit (sinngemäß):**

> Konzept-Diskrepanz aus 008/result.md Z. 136-139: Halbsatz
> suggeriert umgesetzte Entkopplung, real wartet
> `McpServerCommand.cs:35` `TryLoadSolutionAsync` synchron ab,
> bevor `McpServer.Create` aufgerufen wird
> (`units/008/result.md` Z. 137). Konjunktiv ("sollen") statt
> Indikativ macht den Plan-Charakter deutlich, Verweis auf den
> P0/P1-Rest bleibt für künftige Planer erhalten.

### Schritt 5 — Neue Test-Datei `McpConceptDocumentTests.cs` (~30-45 min)

**Datei:** `src/AiNetLinter.Tests/Mcp/McpConceptDocumentTests.cs` (NEU)

**Zweck:** A3-Sicherung gegen Re-Drift des Konzepts. Jeder Test
parst die relevante Konzept-Stelle via Reflection (oder direktes
File-Read + Pattern-Match) und assertiert gegen den Code-Stand.
Bei zukünftiger Re-Drift wird der Test rot.

**Vorbedingung:** Die Datei muss `konzept.md` lesen können. Da
`konzept.md` nicht Teil des Build-Outputs ist, sondern im
Working-Tree liegt: der Test resolved den Pfad relativ zum
`AppContext.BaseDirectory` (wie `McpLiveRepositoryFixture` es für
die `AiNetLinter.slnx` bereits tut — siehe
`src/AiNetLinter.Tests/Fixtures/McpLiveRepositoryFixture.cs:18-32`).

**Test-Klasse (Struktur, illustrative Vorlage — nicht wortwörtlich zu kopieren):**

```csharp
#nullable enable

using System;
using System.IO;
using System.Linq;
using Xunit;

namespace AiNetLinter.Tests.Mcp;

/// <summary>
/// Verankert die Konzept-Aussagen in tasks/codegraph-mcp-server/konzept.md
/// gegen den realen Code-Stand. A3-Sicherung gegen Re-Drift: jede spaetere
/// Aenderung am Konzept, die eine der 3 korrigierten Stellen zurueck auf den
/// veralteten Stand bringt, wird durch einen dieser Tests gefangen.
/// </summary>
public sealed class McpConceptDocumentTests
{
    private static string ReadKonzeptText()
    {
        // Repo-Root finden, identisch zu McpLiveRepositoryFixture (siehe
        // src/AiNetLinter.Tests/Fixtures/McpLiveRepositoryFixture.cs:18-32).
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName,
            "tasks", "codegraph-mcp-server", "konzept.md")))
        {
            dir = dir.Parent;
        }
        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(dir!.FullName,
            "tasks", "codegraph-mcp-server", "konzept.md"));
    }

    private static string ExtractToolTableRow(string konzept, string toolName)
    {
        // Einfache Zeilen-Extraktion: Zeile, die mit "| `toolName` |" anfaengt.
        var line = konzept.Split('\n').FirstOrDefault(l =>
            l.TrimStart().StartsWith($"| `{toolName}` |"));
        Assert.NotNull(line);
        return line!;
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Konzept_GetViolations_StatusIsFertig()
    {
        var row = ExtractToolTableRow(ReadKonzeptText(), "get_violations");
        Assert.Contains("| fertig |", row);
        Assert.DoesNotContain("Review offen", row);
        Assert.DoesNotContain("| offen |", row);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Konzept_GetViolations_InputBeschreibtScopeFilter()
    {
        var row = ExtractToolTableRow(ReadKonzeptText(), "get_violations");
        Assert.Contains("scopeFilter", row);
        Assert.DoesNotContain("Datei-/Symbol-Scope", row);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Konzept_SearchPattern_StatusIsFertig()
    {
        var row = ExtractToolTableRow(ReadKonzeptText(), "search_pattern");
        Assert.Contains("| fertig |", row);
        Assert.DoesNotContain("| offen |", row);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Konzept_GetImpact_InputBeschreibtExklusiveParameter()
    {
        var row = ExtractToolTableRow(ReadKonzeptText(), "get_impact");
        Assert.Contains("gitRef", row);
        Assert.Contains("symbolIdentifier", row);
        Assert.Contains("exklusiv", row);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Konzept_ServerBetrieb_KaltstartAlsSollFormuliert()
    {
        var konzept = ReadKonzeptText();
        // Der fragliche Block beginnt mit "1. Start:" und enthaelt den
        // "Transport/Handshake"-Halbsatz. Wir assertieren, dass der
        // Konjunktiv ("sollen") und der Verweis auf P0/P1-Rest da sind,
        // und der irre fuehrende Indikativ ("stehen") weg ist.
        Assert.Contains("sollen unabhängig vom Ladezustand", konzept);
        Assert.Contains("Kaltstart entkoppeln", konzept);
        Assert.DoesNotContain("stehen dabei unabhängig vom Ladezustand sofort bereit", konzept);
    }
}
```

**5 Tests, alle `[Trait("Category", "Unit")]`** — laufen in
`<1 s`, behindern Unit-Slice-Iteration nicht. Reflection auf
`konzept.md` als String, Pattern-Match mit `Assert.Contains` /
`Assert.DoesNotContain`.

**A3-Methodik pro Test (vom Coder im `result.md` zu dokumentieren):**

- **Test 1** (`Konzept_GetViolations_StatusIsFertig`): Assertion
  `| fertig |` temporär auf `| XYZ |` umbiegen → Test rot mit
  Failure-Message "Sub-string not found: '| fertig |'". Zurückbiegen
  → grün.
- **Test 2** (`Konzept_GetViolations_InputBeschreibtScopeFilter`):
  analog.
- **Test 3** (`Konzept_SearchPattern_StatusIsFertig`): analog.
- **Test 4** (`Konzept_GetImpact_InputBeschreibtExklusiveParameter`):
  analog.
- **Test 5** (`Konzept_ServerBetrieb_KaltstartAlsSollFormuliert`):
  analog.

Mindestens **3 der 5 Tests** müssen mit wortwörtlichem
Failure-Output im `result.md` A3-Block dokumentiert sein (analog
008-Resultat-Block "A3-Nachweis pro neuem Test"). Die anderen 2
dürfen mit "analog zu Test N" zusammengefasst werden, damit der
`result.md`-Block lesbar bleibt.

### Schritt 6 — Verifikation (Pflicht, AGENTS.md §2)

**Reihenfolge:**

1. **Build:** `dotnet build AiNetLinter.slnx` — grün, 0 Warnungen,
   0 Fehler.
2. **Unit-Slice (schnelle Iteration):** `dotnet test AiNetLinter.slnx
   --no-build --filter "Category=Unit"` — grün, alle bisherigen
   Unit-Tests + die 5 neuen = mindestens 88 + 5 = 93 Tests.
3. **Gezielter Konzept-Test-Slice:** `dotnet test AiNetLinter.slnx
   --no-build --filter "FullyQualifiedName~McpConceptDocumentTests"`
   — 5/5 grün in <1 s.
4. **Volllauf (AGENTS.md §2 Pflicht):** `dotnet test
   AiNetLinter.slnx --no-build` — grün, 1173 + 5 = 1178/1178,
   ca. 6:20 min + <1 s.
5. **Self-Lint:** `dotnet run --project src/AiNetLinter -- --config
   rules.json --path src/BaselineMini` (gemäß 008/fix-01-Plan
   F-003-Korrektur: realer Pfad ist `src/BaselineMini/`, nicht
   `tests/Fixtures/...`). Erwartet: 1 gewollte Violation in
   `src/BaselineMini/ViolatingClass.cs`, kein 010-Regress.

### Schritt 7 — `result.md` schreiben (vom Coder)

Standard-Result-Protokoll mit:
- **What changed:** 4 Konzept-Korrekturen + 1 neue Test-Datei
  (5 Tests).
- **Commit-Hashes:** 3-4 Commits erwartet:
  1× `docs(mcp): konzept tool-status-tabelle an code-stand angepasst
     [codegraph-mcp-server]`
  2× optional aufteilbar in 1× für Input-Korrekturen + 1× für
     Status-Korrekturen + 1× für Kaltstart-Suggestion, falls
     logisch trennbar — Coder entscheidet (Plan-Abweichung
     erlaubt, in `result.md` zu dokumentieren).
  3× `test(mcp): konzept-vs-code reflection-tests (a3-sicherung)
     [codegraph-mcp-server]`
  4× `chore(task): unit 010 result, konzept-pflege abgeschlossen
     [codegraph-mcp-server]`
- **A3-Block:** mindestens 3 der 5 Tests mit wortwörtlichem
  Failure-Output, alle 5 mit kurzem "Test rot → grün"-
  Zusammenfassung.
- **Build/Test-Ergebnis:** Tabelle analog 008-Resultat-Tabelle
  Z. 119-128.
- **Plan-Abweichungen:** Coder dokumentiert jede Abweichung von
  der hier vorgegebenen Wortlaut-Korrektur, von der 5-Test-Struktur
  in Schritt 5 oder von der Commit-Aufteilung.
- **Commit-Disziplin (A4):** Tabelle analog 008-Resultat
  Z. 167-176.
- **Nächste Aktion:** "Kritiker-Aufruf für 010 (Review-Datei
  `units/010/review.md`)".

### Schritt 8 — Volllauf-Log sichern (für den Kritiker)

`volllauf.log` (analog `units/008/volllauf.log`) im
`units/010/`-Ordner ablegen, damit der Kritiker die
Build/Test-Outputs nachprüfen kann, ohne den Lauf zu
wiederholen.

## 6. Erwartete Tests

### A3-Disziplin (Pflicht, vom Coder im `result.md` zu dokumentieren)

5 neue Tests in `McpConceptDocumentTests.cs`, alle mit
`[Trait("Category", "Unit")]`:

| Test | A3-Methode (Failure-Output) | Pass-Kriterium |
|---|---|---|
| `Konzept_GetViolations_StatusIsFertig` | Assertion `\| fertig \|` temporär durch `\| XYZ \|` ersetzen | Wortwörtliche `Assert.Contains`-Failure-Message im `result.md` |
| `Konzept_GetViolations_InputBeschreibtScopeFilter` | Assertion `scopeFilter` temporär durch `XYZ` ersetzen | analog |
| `Konzept_SearchPattern_StatusIsFertig` | Assertion `\| fertig \|` temporär durch `\| XYZ \|` ersetzen | analog |
| `Konzept_GetImpact_InputBeschreibtExklusiveParameter` | Assertion `exklusiv` temporär durch `XYZ` ersetzen | analog |
| `Konzept_ServerBetrieb_KaltstartAlsSollFormuliert` | Assertion `sollen unabhängig` temporär durch `XYZ` ersetzen | analog |

Mindestens 3 der 5 A3-Schritte mit wortwörtlichem Failure-Output.
Verifikation nach Korrektur: alle 5 Assertions zurückbiegen, alle 5
Tests grün.

### Bestehende Tests (Regressions-Schutz)

Alle 1173 bisherigen Tests müssen grün bleiben. Konzept-Pflege
ändert **keinen** Produktionscode, also kein Risiko für
Funktionsregression — die bestehenden Tests sind die
automatische A3-Sicherung gegen versehentliche Code-Edits.

### Reflection-Pfad / no-op-Pfad

Nicht relevant (kein Test rot ohne Konzept-Drift).

## 7. Plan-Abweichungen, die explizit erlaubt sind

1. **Commit-Aufteilung:** Coder darf die 4 Konzept-Korrekturen in
   **1, 2 oder 3 Commits** aufteilen, solange jeder Commit für
   sich **kompiliert** und die Tests grün bleiben (Markdown-Edits
   kompilieren trivial, daher leicht erfüllbar). Begründung im
   `result.md` Pflicht.
2. **Test-Datei-Name:** `McpConceptDocumentTests.cs` ist
   Vorschlag; Coder darf sie stattdessen in die bestehende
   `McpDocumentationSmokeTests.cs` integrieren, **wenn** das
   die Datei nicht über 500 Z. treibt. Aktueller Stand
   `McpDocumentationSmokeTests.cs` 66 Z. + 5 neue Reflection-Tests
   à ~5-10 Z. = ~95-115 Z. insgesamt — beide Optionen tragbar.
   Entscheidung im `result.md` dokumentieren.
3. **Exakter Assertion-Wortlaut:** Coder darf die Test-Assertions
   minimal anpassen (z. B. `Assert.Contains(" fertig ", row)` mit
   Spaces, falls die Markdown-Pipe-Syntax das verlangt), solange
   die Test-Intention erhalten bleibt. A3-Disziplin mit
   wortwörtlichem Failure-Output bleibt Pflicht.
4. **Repo-Root-Resolution:** Falls `AppContext.BaseDirectory`-
   basierte Resolution (siehe `McpLiveRepositoryFixture.cs:18-32`)
   in dieser Datei nicht greift (z. B. weil der Walk eine andere
   Tiefe hat), darf der Coder eine alternative Resolution nutzen
   (z. B. `Environment.CurrentDirectory` oder
   `Path.GetFullPath("tasks/codegraph-mcp-server/konzept.md")`),
   solange der Test lokal und in CI grün ist. Begründung im
   `result.md` Pflicht.

## 8. Bezug zu Projektregeln

| Regel | Datei | Kurzgrund |
|---|---|---|
| **A7 (Kernel)** — `konzept.md` bindend, nur lesbar | `kernel.md` Z. 107-124 | Für 010 **explizit aufgehoben** durch Orchestrator-User-Prompt, weil wortwörtliche Korrekturen im Plan stehen (Sektion 5). |
| **A8 (Kernel)** — Kernel und Rollen unantastbar | `kernel.md` Z. 126-139 | 010 fasst weder `kernel.md` noch Rollen-Dateien an. |
| **A5 (Kernel)** — Fertig ist fertig, keine kosmetischen Edits | `kernel.md` Z. 82-98 | 010 ändert nur die 3 dokumentierten Stellen, keine "Verschönerungen". |
| **A3 (Kernel)** — Tests müssen fehlschlagen können | `kernel.md` Z. 55-72 | 5 neue Reflection-Tests mit dokumentiertem A3-Pfad. |
| **A4 (Kernel)** — Nichts Unwiederbringliches | `kernel.md` Z. 74-80 | Gezielter `git add`, kein `-A`/`.`, kein Push, kein Amend. |
| **A2 (Kernel)** — Wer prüft, fixt nicht | `kernel.md` Z. 47-53 | 010 macht keine Code-Änderungen, also keine TD-Beobachtungen zu erwarten. |
| **§1 — Einfachheit vor Abstraktion** | `AiNetLinterRichtlinien.mdc` §1 | Konzept-Korrektur ist die einfachste Form der Drift-Bereinigung — keine Tool-Erweiterung, keine Refaktorisierung. |
| **§4 — Update-Pflicht / Commit-Vorschlag-Pflicht** | `AiNetLinterRichtlinien.mdc` Z. 77-86 | `result.md` endet mit konkretem Commit-Vorschlag. |
| **`MaxLineCount: 500`** | `AiNetLinter.mdc` Z. 24 | Neue Test-Datei bleibt unter 500 Z. (geschätzt 50-80 Z.). |
| **AGENTS.md §2 — Test-Kategorien** | `AGENTS.md` Z. 35-49 | Coder nutzt `Category=Unit` für schnelle Iterationen; Volllauf-Pflicht für finale Verifikation (Schritt 6). |
| **Konzept-Pflicht:** DoD-Konzept-Treue | `konzept.md` Z. 590-660 | 010 korrigiert die Konzept-Tabelle als **Voraussetzung** für A1 (das in A1 dokumentierte Verhalten muss gegen das Konzept prüfbar sein). |
| **Konzept-Pflicht:** DoD-Code-Konsistenz | `konzept.md` Z. 195-204 (Dogfooding) | 010 ist explizit nicht-code-relevante Drift-Korrektur; Dogfooding-Logik in der Konzept-Spec ändert sich nicht. |

## 9. Tech-Debt-Aktionen

**Keine TD-Schließungen, keine neuen TD-Einträge.**

- 010 schließt **keinen** bestehenden TD-Eintrag (Konzept-Pflege
  ist orthogonal zu den 12 verbleibenden offenen TD-001, 002, 004,
  005, 006, 007, 008, 009, 010, 011, 014; die in 008-Resultat
  erwähnte TD-003/012/013/015/016-Schließung ist bereits in den
  jeweiligen Einheiten erfolgt).
- 010 öffnet **keinen** neuen TD-Eintrag: die zu korrigierenden
  Stellen sind Konzept-Interna, keine Code-Beobachtungen. Falls
  der Coder beim Lesen von `McpServerCommand.cs` oder
  `McpServerOptionsFactory.cs` eine subtile Inkonsistenz findet
  (z. B. Wortlaut-Drift zwischen Code-Kommentar und Konzept
  Z. 188-190 "Dokumentation"), darf er das im `result.md`-
  Tech-Debt-Beobachtungen-Block **vorschlagen** (kein direkter
  Edit, A2). Wahrscheinlichkeits-Einschätzung: niedrig — die
  Code-Kommentare in den genannten Dateien sind in den
  vorherigen Reviews schon gegen das Konzept geprüft worden.

## 10. Risiken

- **Risiko 1 (mittel): Anker-Versatz im User-Prompt.** Der
  User-Prompt nennt teils andere Zeilennummern als die
  tatsächlichen Konzept-Stellen (`get_impact` ist Z. 546, nicht
  Z. 550; Kaltstart-Suggestion ist Z. 559-560, nicht Z. 564).
  Der Coder verwendet den **exakten Wortlaut** aus Sektion 5,
  nicht die Zeilennummern. → **Gegenmaßnahme:** Schritt 1-4
  zitieren wortwörtlich den Vorher-Text, der Coder sucht ihn
  via `String.Contains` oder direktem Augen-Scan, nicht via
  Zeilennummer. Der Planer hat in Sektion 3 die korrekten
  Anker verifiziert.

- **Risiko 2 (niedrig): Re-Drift nach 010.** Wenn nach 010
  jemand anderes am Konzept editiert (z. B. der User selbst
  oder ein späterer Coder), könnten die 3 Korrekturen wieder
  rückgängig gemacht werden, ohne dass es bemerkt wird. →
  **Gegenmaßnahme:** die 5 Reflection-Tests in
  `McpConceptDocumentTests.cs` schlagen bei Re-Drift sofort
  rot. Der Unit-Slice-Lauf vor jedem Commit fängt das ab.

- **Risiko 3 (niedrig): Volllauf dauert lange.** 1178 Tests
  in ~6:20 min, aber reproduzierbar. Falls der Coder den
  Volllauf nicht abwarten will, darf er Unit-Slice + gezielten
  Konzept-Test-Slice fahren — Pflicht-Doku im `result.md`.
  Volllauf bleibt Pflicht (AGENTS.md §2).

- **Risiko 4 (sehr niedrig): User-Reaktion auf
  Konzept-Änderungen.** Der User hat den "weitermachen"-
  Workflow bestätigt und der Orchestrator-User-Prompt hat die
  A7-Aufhebung explizit erteilt. Falls der User die
  Korrekturen anders haben will (z. B. Z. 550 Input-Spalte
  soll statt "Optionaler `scopeFilter`..." anders formuliert
  sein), ist das eine Folge-Diskussion, nicht ein 010-Blocker.
  Der Coder dokumentiert die Korrekturen im `result.md`,
  damit der User sie bei Gelegenheit reviewen kann.

- **Risiko 5 (sehr niedrig): C#-only-Indikativ-Block im
  ServerInstructions nicht mitkorrigiert.** Konzept
  Z. 154-166 beschreibt die Scope-Kommunikation mit Indikativ
  ("decken ausschließlich .cs ab"). Das ist real umgesetzt
  (siehe 003) und konsistent — keine Drift. Falls der Coder
  beim Lesen anderer Konzept-Stellen weitere Diskrepanzen
  findet, dokumentiert er sie im `result.md` als Beobachtung
  (kein Edit, A7), Folge-Diskussion.

## 11. Bewusst-NICHT-in-010-Liste

1. **Keine Code-Änderungen** (außer dem neuen Test-File).
2. **Kein A1 (`rules.json`-Auto-Discovery)** — folgt in 011 nach
   sauberer Konzept-Grundlage.
3. **Kein A4 (Kaltstart entkoppeln)** — 012/013, ggf. als
   Doppeleinheit mit TD-009-Refactor.
4. **Kein A2/A3 (Verzeichnis-Sweep + `mtime`)** — 013/014.
5. **Kein A5/A6/A7 (`--mcp-log`, `ILintConsole`, Last-Fixture)**
   — später, je nach Bedarf.
6. **Kein TD-008/009/010/011/014-Refactor** — alle unverändert
   offen, separate Folge-Einheiten.
7. **Keine Edits an `konzept.md` außer den 4 wortwörtlich in
   Sektion 5 vorgegebenen Stellen.** Insbesondere: keine
   "Verschönerungen" am Fließtext, keine Umformulierungen
   anderer Sätze, keine Hinzufügungen.
8. **Kein Push** (A4).
9. **Keine Edits an `kernel.md`, Rollen-Dateien, `.agents/rules/**`,
   `rules.json`, `Docs/**` (außer dem neuen Test-File)**
   (A7, A8).
10. **Keine englische Übersetzung des Konzepts** (deutsch
    bleibt, konsistent zum Rest).
11. **Keine "Verbesserung" der Konzept-Struktur** (z. B. keine
    Aufteilung der Tool-Tabelle in zwei Tabellen für
    C#-only vs. nicht-C#-only) — das wäre Scope-Creep.
12. **Keine A3-Tests gegen den laufenden MCP-Server** (alle
    neuen Tests sind Unit-Tests, keine E2E-Tests) — Konzept ist
    Datei-Stand, nicht Server-Verhalten. Wenn der User das
    will, ist das eine Folge-Diskussion.

## 12. Synergien mit Folge-Einheiten

- **011 = A1 `rules.json`-Auto-Discovery** (P0, Konzept Z. 257-264):
  hat nach 010 eine saubere Konzept-Spec, auf die sich die
  Coder-Implementierung stützen kann. Insbesondere die
  korrigierte Status-Spalte in der Tool-Tabelle (alle 9 Tools
  "fertig") macht klar, dass `get_violations` ein
  production-reifer Code-Pfad ist, in dem ein "Basis: Default-
  Regeln, keine `rules.json` gefunden"-Vermerk integriert werden
  kann. Risiko: A1 triggert **nicht** TD-009 (ändert nur
  `McpServerCommand.ResolveConfig` Z. 72-79 und
  `get_violations`-Output-Format) — also keine Doppeleinheit
  nötig.
- **012/013 = A4 Kaltstart entkoppeln + TD-009 inline**: hat
  nach 010 eine konsistente Konzept-Beschreibung, die
  klarmacht, dass die Entkopplung "soll", nicht "ist" — der
  Coder kann die Konzept-Formulierung in 012 wortwörtlich
  übernehmen, wenn er die Implementation dokumentiert.
- **013/014 = A2/A3 Verzeichnis-Sweep + `mtime`**: unabhängig
  von 010, aber durch die Konzept-Bereinigung
  (Kaltstart-Suggestion korrigiert) ist klar, dass
  `RefreshStaleDocuments` weiterhin im **sequenziellen**
  Startpfad läuft — die Entkopplung passiert erst in A4, nicht
  in A2/A3. Verhindert Missinterpretation.
- **Last-Fixture (A7)**: hängt konzeptuell von A4 ab, kommt
  frühestens nach 013. 010 liefert keinen direkten Beitrag.

---

## Zusammenfassung (für Orchestrator)

- **Wahl:** Konzept-Pflege-Einheit, 3 (+1) veraltete Stellen
  in `konzept.md` an Code-Stand anpassen.
- **Scope:** 4 wortwörtliche Markdown-Korrekturen + 1 neue
  Reflection-Test-Klasse (5 Tests, alle Unit-Slice).
- **A7-Aufhebung:** durch Orchestrator-User-Prompt erteilt;
  wortwörtliche Vorher-/Nachher-Texte in Sektion 5 vorgegeben,
  Coder hat keinen Ermessensspielraum.
- **Risiko:** Niedrig (reine Markdown + Reflection-Tests).
- **Erwarteter Aufwand:** ~1-1.5h für den Coder.
- **Aufruf-Budget:** 3 (Planer + Coder + Kritiker) — passt
  in die verbleibenden 9 von 40.
- **Konzept-Treue:** A7 eingehalten (Aufhebung explizit
  begründet), A5/A4/A3/A8 alle eingehalten.
- **TD-Aktionen:** Keine.
- **Synergie:** Saubere Grundlage für A1 (011) und A4 (012/013);
  verhindert Drift in die entgegengesetzte Richtung.
- **Bewusst NICHT:** Alle P0/P1-Code-Erweiterungen, alle
  TD-Refactors, alle Code-Edits außer dem neuen Test-File.
