---
unit: 008
task: codegraph-mcp-server
workflow: dynamic-loop
type: plan
created_by: planer
created_at: 2026-08-02
epic: EPIC-08 (Doku)
extends:
  - konzept.md Z. 105-107 (EPIC-08 Scope, einzige noch offene Muss-Have)
  - konzept.md Z. 188-190 (Muss-Habe Doku: agent-api.md, integration.md, ROADMAP.md, README.md)
  - konzept.md Z. 215-233 (Trunkierung + maxResults + Meta-Zeile Format, P0)
  - konzept.md Z. 234-240 (Regel-ID in get_violations, Doku-Hinweis)
  - konzept.md Z. 257-264 (rules.json-Auto-Discovery — **offen**, in Doku als „geplant" markieren, nicht versprechen)
  - konzept.md Z. 265-275 (Kaltstart entkoppeln — **offen**, in Doku als „geplant" markieren)
  - konzept.md Z. 276-283 (Verzeichnis-mtime + Verzeichnis-Sweep neu/gelöscht — **offen**, in Doku als „geplant" markieren)
  - konzept.md Z. 284-293 (stdout strukturell als Protokollkanal — **offen**, in Doku als „geplant" markieren)
  - konzept.md Z. 305-315 (--mcp-log Call-Log — **offen**, in Doku nicht versprechen, nur erwähnen wenn implementiert)
  - konzept.md Z. 316-324 (Tool-vs-rg-Empfehlung in Docs/integration.md, P0)
  - konzept.md Z. 622-624 (DoD Doku-Update)
  - konzept.md Z. 659-660 (DoD Tool-vs-rg-Empfehlung)
  - units/006/plan.md (E2E-Verifikations-Pattern via McpTestClient)
  - units/006/review.md (A3-Disziplin: struktur vs. Symptom)
  - units/007/plan.md (Test-Kategorien-Pattern, EPIC-07-E2E-Aufteilung)
  - AiNetLinterRichtlinien.mdc §1 (Doku vor tiefgreifenden Änderungen konsultieren)
  - AiNetLinterRichtlinien.mdc §4 (MCP-Dogfooding NUR via C#-Test-Infrastruktur, keine Python-Skripte)
  - AiNetLinterRichtlinien.mdc §4 (Update-Pflicht: Docs/ROADMAP.md, README.md + Commit-Vorschlag-Pflicht)
---

# Plan Einheit 008 — EPIC-08 Doku (MCP-Modus)

## Ziel der Einheit

Die einzige noch offene Muss-Have-Säule aus dem Ursprungs-Scope
(`konzept.md` Z. 105-107) wird geschlossen: die **Doku** des seit 001
inkrementell aufgebauten MCP-Server-Modus. Konkret:

- **`Docs/agent-api.md`** bekommt einen neuen Abschnitt „MCP-Server-Modus"
  mit Beschreibung aller 9 Tools, Trunkierungs-Format, Error-Codes und
  Server-Lifecycle (`initialize` → `tools/list` → `tools/call`).
- **`Docs/integration.md`** bekommt einen neuen Abschnitt „MCP-Server
  registrieren" mit JSON-Config-Beispiel, `cwd`-Verhalten, Mehrdeutigkeit
  und der **Tool-vs-`rg`-Empfehlung** aus `konzept.md` Z. 316-324.
- **`Docs/ROADMAP.md`** wird auf den aktuellen Stand aktualisiert
  (EPIC-01..07 abgeschlossen, EPIC-08 in 008, P0/P1-Rest als nächste
  Phase).
- **`README.md`** bekommt einen knappen Hinweis auf den MCP-Modus (Link
  auf `Docs/agent-api.md`).

**Keine Code-Änderungen** — reine Doku, aber **verifiziert** durch
`McpTestClient` + `McpLiveRepositoryFixture` (Dogfooding gegen die echte
`AiNetLinter.slnx`), damit die Doku nicht zur Lüge wird
(A3-Methodik, Details unten).

## Scope-Entscheidung

**Gewählt: EPIC-08 (Doku), reine Markdown-Updates, kein Code.**

Begründung (in Reihenfolge der Konzept-Logik + Tech-Debt-Logik):

- **Konzept-Reihenfolge zwingend:** `konzept.md` Z. 105-107 listet
  EPIC-08 als letzte offene Position aus dem Ursprungs-Scope. EPIC-04
  (4/4 Tools), EPIC-05 (Scope-Kommunikation), EPIC-06 (Robustheit) und
  EPIC-07 (Tests) sind alle abgenommen. **EPIC-08 ist die einzige
  verbliebene Muss-Have-Säule.** Konzept Z. 622-624 macht es als
  DoD-Pflicht verbindlich.
- **Tool-vs-`rg`-Empfehlung (Konzept Z. 316-324) ist explizit als
  Doku-Punkt im Konzept markiert** (nicht als Feature), und in
  `konzept.md` Z. 659-660 als DoD-Kriterium für 008 festgeschrieben.
  Sie gehört in `Docs/integration.md` — semantisch der
  Registrierungs-/Setup-Block, nicht der API-Block.
- **P0/P1-Rest-Erweiterungen sind ungleich besser einzeln aufgehoben.**
  Jede hat eigenes Risiko: Kaltstart-Entkopplung = `McpCodeGraphServer`-
  API-Change (triggert TD-009 → Constructor-Record-Investition nötig);
  `--mcp-log` = neuer CLI-Flag + neue Persistenz;
  Last-Fixture = Test-Infrastruktur-Generierung mit großen
  Solutions. Sie einzeln zu planen ist sauberer als eine
  „Alles-oder-nichts"-Einheit.
- **TD-016a (Fixture-Refactor) ist Aufräumarbeit ohne User-Mehrwert.**
  Kann nach 008 standalone laufen oder inline beim nächsten
  Fixture-Block. Passt nicht in dieselbe Einheit — saubere
  Trennung.
- **Tech-Debt-Refactors (TD-008/TD-009) sind nur sinnvoll, wenn
  `McpCodeGraphServer` ohnehin angefasst wird** — und das ist in 008
  nicht der Fall. Daher: bewusst zurückstellen.
- **Risikoarm + nutzbar:** Reine Doku → kein Build-Risiko, kein
  Test-Risiko, kein Architektur-Risiko. Die A3-Verifikation (durch
  `McpTestClient`) stellt aber sicher, dass die Doku nicht erfunden
  wird.

**Bewusst NICHT in 008 (Pflicht-Begründung für jeden Punkt):**

- **Keine P0/P1-Rest-Erweiterungen** (Kaltstart, Auto-Discovery,
  Staleness-mtime-Sweep, `--mcp-log`, Verzeichnis-Sweep für
  neu/gelöschte Dateien, `ILintConsole` für MCP, Last-Fixture). Sie
  sind im Konzept Z. 207-324 als eigene Punkte markiert und
  brauchen jeweils eigene Einheiten — keine „Mitreff"-Einheit.
  A6 (im Zweifel fragen) gebietet die Trennung, weil jede
  Erweiterung die `McpCodeGraphServer`-API oder den
  Server-Lifecycle verändert.
- **Keine Code-Änderungen.** Auch nicht „kleine" — die
  Doku-Aktualisierung ist Selbstzweck, nicht Vorarbeit für etwas
  anderes. A5 (Fertig ist fertig) und A7 (Eingaben sind Eingaben)
  verbieten das.
- **Kein TD-016a (Fixture-Refactor).** Eigenständige
  Folge-Einheit oder inline beim nächsten Fixture-Block —
  `tech-debt.md` Z. 165-169 hält den Vorschlag fest.
- **Kein Auto-Generate der Doku aus `rules.json` (analog
  `AiNetLinter.mdc` → `docs/rules/AiNetLinter.mdc`).** Das wäre ein
  eigenes Feature, das im Konzept nicht erwähnt ist — keine
  scope-Erweiterung ohne Nutzer-Freigabe.
- **Kein Überschreiben bestehender Doku-Struktur.** `Docs/agent-api.md`
  hat heute schon eine `Discovery-Commands`-Sektion; der
  MCP-Modus-Abschnitt wird **als neuer Abschnitt** angefügt, nicht
  als Ersatz. A5 (fertig-melden statt verschönern).
- **Kein „P0/P1-Rest in Doku versprechen."** Was noch nicht
  implementiert ist (Kaltstart-Entkopplung, Verzeichnis-Sweep,
  `--mcp-log`), wird in der Doku **nicht** als Feature beworben,
  höchstens als „geplant für künftige Version" markiert — und das
  auch nur, wenn es in `Docs/ROADMAP.md` explizit als Roadmap-Punkt
  steht. A7 (Konzept ist Eingabe; eine Lüge in der Doku über
  künftige Features wäre Drift).
- **Kein Push** (A4). Working-Tree bleibt lokal bis zum
  Kritiker-`approved`.

## Vor-der-Planung-Checks (Kernel Teil B „Drift" / „Duplikate durch Blindheit")

### Check 1 — Welche 9 Tools existieren wirklich? (Drift-Schutz)

**Befund (gelesen, Stand `ed58ba0`):**

| Tool | Klassen-Datei (verifiziert) | Status |
|---|---|---|
| `get_index_scope` | `src/AiNetLinter/Mcp/Tools/GetIndexScopeTool.cs` + `GetIndexScopeScanner.cs` | fertig (002) |
| `find_symbol` | `src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs` + `FindSymbolScanner.cs` | fertig (003/004) |
| `find_references` | `src/AiNetLinter/Mcp/Tools/FindReferencesTool.cs` | fertig (005) |
| `get_impact` | `src/AiNetLinter/Mcp/Tools/GetImpactTool.cs` | fertig (005) |
| `get_type_hierarchy` | `src/AiNetLinter/Mcp/Tools/GetTypeHierarchyTool.cs` | fertig (EPIC-03) |
| `get_file_skeleton` | `src/AiNetLinter/Mcp/Tools/GetFileSkeletonTool.cs` | fertig (EPIC-03) |
| `get_hotspots` | `src/AiNetLinter/Mcp/Tools/GetHotspotsTool.cs` | fertig (EPIC-04) |
| `get_violations` | `src/AiNetLinter/Mcp/Tools/GetViolationsTool.cs` + `GetViolationsScanner.cs` | fertig (001) |
| `search_pattern` | `src/AiNetLinter/Mcp/Tools/SearchPatternTool.cs` + `SearchPatternScanner.cs` | fertig (002) |

Plus 3 Registrar-Klassen: `SymbolGraphToolRegistrations.cs`,
`FileStructureToolRegistrations.cs`, `AnalysisToolRegistrations.cs`.

**Konsequenz für 008:**

- Doku-Aufzählung „9 Tools" ist **aktuell korrekt** — keine
  Konzept-Diskrepanz, kein Drift.
- Coder muss die **exakten Parameternamen** aus den
  `ExecuteAsync`-Signaturen lesen (nicht aus `konzept.md` Z. 539-552
  — das ist eine grobe Tabelle, nicht die Quelle der Wahrheit). Die
  Parameternamen aus `konzept.md` sind „gitRef" / „kind" /
  „namePattern" / „maxResults" — die müssen gegen den Code
  verifiziert werden.

### Check 2 — Trunkierungs-Format-Quelle (Duplikate durch Blindheit)

**Befund (gelesen, `src/AiNetLinter/Mcp/McpTruncation.cs:40, 66`):**

- Listen-Trunkierung: `$"[{totalMatches} Treffer gesamt, {maxResults}
  gezeigt — Pattern verfeinern oder maxResults erhöhen]"`
- Datei-Listen-Trunkierung: `$"[{totalFiles} Dateien mit Textfund,
  {maxFiles} gezeigt — search_pattern fuer Details]"`

**Konsequenz für 008:**

- Doku muss diese **wortwörtlich** übernehmen, nicht „sinngemäß
  paraphrasieren". `konzept.md` Z. 232-233 ist eine Vorgabe des
  Formats, nicht der Wortlaut-Quelle — der Wortlaut-Quelle ist
  `McpTruncation.cs`. A3 sichert das.
- Doku soll klarstellen, dass **beide** Meta-Zeilen existieren
  (Treffer vs. Datei-Liste), weil sie semantisch unterschiedlich
  sind (die Datei-Liste-Meta-Zeile verweist auf `search_pattern`,
  die Treffer-Meta-Zeile nicht).

### Check 3 — Error-Codes-Quelle

**Befund (gelesen, `src/AiNetLinter/Output/LinterErrorCodes.cs:8-25`):**

Insgesamt 15 Codes: `CONFIG_REQUIRED`, `CONFIG_NOT_FOUND`,
`CONFIG_INVALID`, `CONFIG_SMELL`, `BASELINE_NOT_FOUND`,
`BASELINE_INVALID`, `WORKSPACE_DIAGNOSTIC`, `ANALYSIS_FAILED`,
`RESOURCE_NOT_FOUND`, `DRIFT_DETECTED`, `AMBIGUOUS_SOLUTION`,
`SOLUTION_NOT_LOADED`, `SYMBOL_NOT_FOUND`, `AMBIGUOUS_SYMBOL`,
`INVALID_ARGUMENT`.

**Konsequenz für 008:**

- Doku listet die **im MCP-Kontext relevanten** Error-Codes
  (alle 15 sind potenziell relevant, da `LinterErrorFormatter`
  auch im MCP-Server-Pfad genutzt wird). Format:
  `[ERROR]: <CODE>: <Kurzmeldung>` — aus `Docs/agent-api.md:147`
  schon heute dokumentiert, wird für MCP-Modus **erweitert** um
  die Bedeutung jedes Codes im MCP-Kontext.

### Check 4 — C#-only-Hinweis-Quelle (EPIC-05)

**Befund (gelesen, `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs:26-31`):**

```
"Symbolgraph-Tools (find_symbol, find_references, get_impact,
get_type_hierarchy, get_file_skeleton, get_violations) arbeiten
ausschliesslich auf C#/.cs-Quellcode. Fuer Namen, die nur in .js,
.razor, .cshtml, .xaml, .html oder .css vorkommen, ist
search_pattern der passende Fallback. Struktur-Tools ohne
C#-Beschraenkung: get_index_scope, get_hotspots."
```

**Konsequenz für 008:**

- Doku übernimmt diesen Wortlaut **1:1** als zentralen
  Scope-Hinweis im MCP-Modus-Abschnitt. A3 sichert das. (Coder
  darf paraphrasieren für den Fließtext — aber der
  Const-String-Wortlaut muss erkennbar im API-Abschnitt stehen,
  damit der Agent ihn mit dem Server-Output abgleichen kann.)
- Die Tool-Liste **korrekt abgrenzen**: 5+1 Tools sind
  C#-only, 2 Tools (`get_index_scope`, `get_hotspots`) sind
  nicht C#-beschränkt, und `search_pattern` ist der
  Nicht-C#-Fallback.

### Check 5 — Bestehende Doku-Struktur (Duplikate durch Blindheit)

**Befund (gelesen, alle 4 Doku-Dateien):**

- `Docs/agent-api.md` (9154 Z.): heute Sektionen
  `Discovery-Commands`, `Eval-Befehle`, `Fehler-Reporting`,
  `Exit-Codes`, `Discovery-Beispiele`. → MCP-Modus-Abschnitt
  wird als **neue Sektion** angefügt, **nicht** als
  Replacement. A5.
- `Docs/integration.md` (9320 Z.): heute Sektionen
  `Voraussetzungen`, `Schritt 1-5` (Verzeichnisstruktur,
  Doku versionieren, Startkonfiguration, AIContext-Verdrahtung,
  Auto-Sync), `CI-Integration`. → MCP-Registrierungs-Sektion
  wird als **neue Sektion** angefügt. A5.
- `Docs/ROADMAP.md` (46012 Z.): heute sehr lang, mit vielen
  P0/P1/P2-Markern. → **Status-Update der MCP-EPICs** (EPIC-01
  bis EPIC-07 abgeschlossen, EPIC-08 in 008, P0/P1-Rest als
  nächste Phase) wird in die bestehende Roadmap-Struktur
  eingefügt, nicht als neue Datei. A5.
- `README.md`: heute mehrere Sektionen („Wann einsetzen?",
  „Schnellstart", „Agentische Integration", „Linter-Regeln
  synchronisieren"). → **Kurzer Hinweis auf MCP-Modus** mit
  Link auf `Docs/agent-api.md#mcp-server-modus`. A5.

**Konsequenz für 008:**

- Doku-Coder passt sich der **bestehenden** Struktur an
  (Sprache, Sektions-Tiefe, Listen-Stil), statt eine eigene
  Hierarchie zu erfinden. Plan-Abweichung unten erlaubt das
  explizit.
- Bestehende Cross-Links (`[Siehe ...](...)`) müssen
  funktional bleiben.

### Check 6 — Konzept-Diskrepanzen (Drift-Schutz)

**Befund (gelesen, `konzept.md` Z. 539-552 „Tool-Set-Tabelle"):**

- Tabelle listet `search_pattern` als „offen" — **falsch**,
  wurde in 002 abgeschlossen. → Coder dokumentiert den
  realen Stand (`search_pattern`: fertig, 002), **nicht** den
  Konzept-Stand. **Konzept-Änderung** der Status-Spalte ist
  Sache des Nutzers (A7), nicht des Coders.
- Tabelle listet `get_violations` als „codiert, Review offen"
  — **veraltet**, Review in 001 abgeschlossen. → Coder
  dokumentiert auch hier den realen Stand (`get_violations`:
  fertig, 001), nicht den Konzept-Stand.
- Konsequenz: Doku spiegelt den **Code-Stand**, nicht den
  Konzept-Stand. Der Planer markiert im `result.md` diese
  Konzept-Diskrepanz, damit der Nutzer die Konzept-Tabelle
  bei Gelegenheit anpassen kann — A7 blockiert den
  direkten Edit.

### Check 7 — Test-Kategorien (AGENTS.md §2)

**Befund:**

- 9 Test-Klassen mit `Category=Integration`, 18 mit
  `Category=Unit`. Volllauf 1161/1161 in 5:55 min.
- Unit-Slice 80/80 in ~21 s.

**Konsequenz für 008:**

- Reine Doku → keine neuen **funktionalen** Tests.
- A3-Verifikation nutzt `McpTestClient` + `McpLiveRepositoryFixture`
  (`Category=Integration`) für Dogfooding-Calls. Coder darf
  diese Calls in einem **eigenen kleinen** `xUnit`-Test
  einbetten (analog `McpLiveRepositoryTests.cs`), der die
  Tool-Outputs **wortwörtlich** mit Doku-Text vergleicht.
  Das ist A3-Beweis **und** Regression-Schutz (bei künftigen
  Doku-Edits zeigt der Test sofort, wenn der Output
  abweicht).
- Volllauf-Pflicht (AGENTS.md §2): Coder muss den Volllauf
  `dotnet test AiNetLinter.slnx --no-build` **vor dem
  Fertig-Melden** ausführen — grünes Ergebnis ist Pflicht
  für `approved`. Doku-Edits dürfen den Build/Tests nicht
  brechen (es gibt keinen Grund dafür, aber
  A5-Selbst-Audit).
- Coder darf während der Arbeit `dotnet test --filter
  Category=Unit` (80 Tests, ~21 s) für die schnelle
  Iteration nutzen, falls er einen Test anfasst (sollte
  er nicht, aber falls). Der **einzige** Test, den der
  Coder voraussichtlich schreibt, ist der
  A3-Doku-Output-Vergleichs-Test (siehe oben).

## Betroffene Dateien/Module

Doku-Dateien (alle 4 ändern sich):

| Datei | Heute (Z.) | Aktion | Erwartete Änderung (geschätzt) |
|---|---:|---|---:|
| `Docs/agent-api.md` | 9154 | +1 neue Sektion „MCP-Server-Modus" | +250-400 Z. |
| `Docs/integration.md` | 9320 | +1 neue Sektion „MCP-Server registrieren" | +150-300 Z. |
| `Docs/ROADMAP.md` | 46012 | +1-3 Status-Update-Blöcke | +50-150 Z. |
| `README.md` | ~150 | +1 Absatz „MCP-Server" | +20-40 Z. |

A3-Verifikations-Datei (NEU):

| Datei | Aktion | Zweck |
|---|---|---|
| `src/AiNetLinter.Tests/Mcp/McpDocumentationSmokeTests.cs` (NEU, ~120 Z., geschätzt) | E2E-Test, der 3-5 repräsentative Tool-Calls gegen `McpLiveRepositoryFixture` ausführt und die Outputs gegen hartkodierte Erwartungs-Strings (kopiert aus der neuen Doku) assertiert. | A3-Nachweis: Doku-Aussagen, die falsch wären, würden wortwörtlich fehlschlagen. |

**Nicht ändern (A7):**

- `konzept.md` (Eingabe, A7 verbietet Edit — auch wenn die
  Tool-Status-Tabelle veraltet ist, s. Check 6).
- `tasks/codegraph-mcp-server/tech-debt.md` (Pflege durch
  Kritiker/Orchestrator, nicht durch Coder).
- `rules.json` (in 008 kein Anlass).
- `.agents/rules/AiNetLinter.mdc` + `.agents/rules/AiNetLinterRichtlinien.mdc`
  (A7).
- Code-Dateien (`src/AiNetLinter/Mcp/*`, `Commands/McpServerCommand.cs`,
  etc.) — reine Doku-Einheit, A5/A7.

## Konkretes Vorgehen

### Schritt 0 — Baseline (1×, vom Coder vor jeder Änderung)

```powershell
cd C:\Daten\Entwicklung\Ralf\AiNetLinter
git status --short              # muss clean sein
dotnet build AiNetLinter.slnx   # muss 0/0 sein
dotnet test AiNetLinter.slnx --no-build --filter Category=Unit
                             # muss 80/80 grün sein
```

Bei Abweichung: **stopp**, melden. Kein Abarbeiten auf
rotem Stand (Kernel A3).

### Schritt 1 — Werkzeugkasten-Inventar (Coder liest, dokumentiert NICHT)

Coder liest die folgenden Dateien, um die Doku-Quelle der
Wahrheit zu kennen (KEIN Code-Edit, nur Lesen):

1. `src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs` +
   `FindSymbolScanner.cs` → Signatur, Parameter, Defaults,
   Miss-Hint-Verhalten.
2. `src/AiNetLinter/Mcp/Tools/SearchPatternTool.cs` +
   `SearchPatternScanner.cs` → Signatur, Parameter, Defaults.
3. `src/AiNetLinter/Mcp/Tools/FindReferencesTool.cs` →
   Signatur, `maxResults`-Default (aus `McpTruncation`-Aufruf).
4. `src/AiNetLinter/Mcp/Tools/GetImpactTool.cs` →
   Signatur, `gitRef`-Parameter-Verhalten.
5. `src/AiNetLinter/Mcp/Tools/GetTypeHierarchyTool.cs` →
   Signatur.
6. `src/AiNetLinter/Mcp/Tools/GetFileSkeletonTool.cs` →
   Signatur.
7. `src/AiNetLinter/Mcp/Tools/GetIndexScopeTool.cs` +
   `GetIndexScopeScanner.cs` → Signatur, Output-Format.
8. `src/AiNetLinter/Mcp/Tools/GetHotspotsTool.cs` →
   Signatur, optionaler Filter.
9. `src/AiNetLinter/Mcp/Tools/GetViolationsTool.cs` +
   `GetViolationsScanner.cs` → Signatur, Regel-ID-Output.
10. `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` →
    `ServerInstructions` (Check 4).
11. `src/AiNetLinter/Mcp/McpTruncation.cs` → beide
    Meta-Zeilen-Wortlaute (Check 2).
12. `src/AiNetLinter/Output/LinterErrorCodes.cs` → 15 Codes
    (Check 3).
13. `src/AiNetLinter/Commands/McpServerCommand.cs` →
    `ResolveSolutionPathOrError`-Verhalten, `TryLoadSolutionAsync`-
    Best-Effort-Semantik, Exit-Codes.
14. `src/AiNetLinter/Mcp/McpCodeGraphServer.cs` →
    `IsLoaded`-Semantik für den Fall „Solution nicht geladen"
    (Konzept Z. 612-613 DoD), Staleness-Invalidierung
    (`GetCurrentSolution` ruft `RefreshStaleDocuments`).

**Output von Schritt 1:** Eine **kurze** interne Notiz (nicht
committed) mit:
- 9 Tool-Signaturen (Parametername + Typ + Default) in
  tabellarischer Form.
- Den wortwörtlichen Text beider Trunkierungs-Meta-Zeilen.
- Den wortwörtlichen Text der `ServerInstructions`.
- Die 15 Error-Codes mit ihrer Bedeutung (aus
  `LinterErrorCodes.cs`-Kontext, nicht erfunden).

### Schritt 2 — `Docs/agent-api.md` um MCP-Sektion erweitern

Am Ende der Datei (oder als neue Sektion nach „Discovery-Commands",
je nach bestehender Hierarchie) eine neue Sektion
`## MCP-Server-Modus` einfügen mit folgendem Pflicht-Inhalt:

1. **Kurzeinleitung** (1 Absatz): Was der MCP-Server ist, wofür
   er da ist, Verweis auf `Docs/integration.md` für Setup.
2. **Server-Lifecycle**: `initialize` → `tools/list` → `tools/call`.
   Stdio-Transport. Erwartete Latenz beim ersten `initialize`
   (Solution-Load: mehrere Sekunden, abhängig von
   Solution-Größe). Hinweis, dass der Server resident läuft
   und die `MSBuildWorkspace` **nicht** pro Tool-Call neu lädt
   (Konzept Z. 139-141).
3. **Scope-Hinweis (C#-only)**: Den `ServerInstructions`-Wortlaut
   **1:1** übernehmen, in einem `>`-Block. A3 sichert das.
4. **9 Tool-Übersicht** als Tabelle: Tool-Name | Input | Output
   | Hinweis. Spaltenüberschriften und Zeilenformat konsistent
   zum bestehenden Stil in `Docs/agent-api.md` (z. B.
   `Discovery-Commands`-Tabelle).
5. **Trunkierungs-Format**: Erklären, dass Listen-Tools
   (`find_symbol`, `find_references`, `get_impact`,
   `search_pattern`) `maxResults` (Default 50) respektieren und
   bei Überschreitung die Meta-Zeile anhängen. **Beide**
   Meta-Zeilen-Wortlaute aus `McpTruncation.cs:40, 66` wortwörtlich
   in Code-Blöcken.
6. **Miss-Hint** (Konzept Z. 167-174, EPIC-05 in 003): Erklären,
   dass `find_symbol` bei fehlendem C#-Treffer die Datei-Liste
   der Nicht-C#-Treffer in der **trunkierten Form** zurückgibt
   (Meta-Zeile aus Punkt 5). Verweis auf `search_pattern` als
   Folge-Schritt.
7. **Compile-Fehler-Verhalten** (EPIC-06 in 006): Erklären,
   dass 8/9 Tools einen `[WARN]: Workspace-Diagnose`-Hinweis
   voranstellen, wenn die Solution Compile-Fehler enthält, und
   für nicht-betroffene Dateien korrekte Antworten liefern.
   `get_violations` Negativtest-Hinweis (siehe Review 006).
8. **Staleness-Invalidierung** (EPIC-02 in `81cf007`):
   Erklären, dass vor jeder Tool-Antwort die
   `RefreshStaleDocuments`-Logik läuft und geänderte Dateien
   inkrementell aktualisiert werden — kein Komplett-Reload.
9. **Error-Reporting** (Konzept Z. 147-149): Format
   `[ERROR]: <CODE>: <Kurzmeldung>` auf stderr.
   Tabelle der 15 Error-Codes mit Bedeutung im MCP-Kontext.
10. **Verhalten bei nicht-ladbarer Solution** (Konzept
    Z. 612-613 DoD): Server startet trotzdem, jeder Tool-Call
    liefert einen strukturierten Fehler statt Crash.

**Stil-Vorgaben:**

- Sprache: Deutsch (konsistent mit bestehender Doku).
- Überschriften-Hierarchie: konsistent zum Rest der Datei.
- Code-Beispiele in Bash-Blöcken mit ` ```bash `.
- Beispiel-Tool-Calls in JSON-Argument-Form (analog
  bestehender Eval-Befehle-Sektion).

### Schritt 3 — `Docs/integration.md` um MCP-Registrierungs-Sektion erweitern

Am Ende der Datei (oder als neue Sektion, je nach Hierarchie)
eine neue Sektion `## MCP-Server registrieren` mit folgendem
Pflicht-Inhalt:

1. **JSON-Config-Beispiel** für Claude Code / Cursor / andere
   MCP-Hosts:

   ```json
   {
     "mcpServers": {
       "ainetlinter": {
         "command": "ainetlinter",
         "args": ["--mcp-server"]
       }
     }
   }
   ```

   Pfad-Angabe **explizit weglassen** — `cwd` = Projekt-Root
   reicht aus, weil `ResolveSolutionPathOrError` ohne `--path`
   das aktuelle Verzeichnis nach `.sln`/`.slnx` durchsucht
   (Konzept Z. 128-138).
2. **`cwd`-Verhalten** erklären: Der Server läuft im
   `cwd` des Host-Prozesses. Empfehlung: MCP-Server pro
   Projekt registrieren, nicht global, damit das `cwd` zum
   jeweiligen Projekt-Root passt.
3. **Mehrdeutigkeits-Verhalten** (Konzept Z. 133-138): Bei
   mehr als einer `.sln`/`.slnx` im `cwd` bricht der
   Server-Start mit `[ERROR]: AMBIGUOUS_SOLUTION` ab.
   Abhilfe: explizit `--path <Datei>` in den `args` setzen.
4. **Tool-vs-`rg`-Empfehlung** (Konzept Z. 316-324, P0, Pflicht-DoD):
   Reihenfolge für Agenten-Loops im Zielprojekt:
   - **Zuerst** `find_symbol` / `get_file_skeleton` (semantisch,
     schnell, präzise).
   - **`rg`/`grep` nur für Nicht-Symbole**: Konfigurationswerte,
     Kommentare, Strings, Nicht-C#-Dateien (`.json`, `.yml`,
     `.md`).
   - **Niemals `rg` für C#-Symbole** (Klassennamen, Methoden,
     Properties) — produziert False Positives in Strings/
     Kommentaren/gleichnamigen Symbolen anderswo.
5. **Mehrere parallele Server-Instanzen**: Pro Solution ein
   eigener Server-Prozess. Cache-Isolation zwischen
   verschiedenen Solutions ist SHA256-basiert (Konzept
   Z. 573-578) — der Nutzer braucht nichts zu konfigurieren.

### Schritt 4 — `Docs/ROADMAP.md` Status-Update

In der Roadmap-Datei (bestehende Struktur erhalten, A5) den
Status der MCP-EPICs aktualisieren:

- **EPIC-01 bis EPIC-07 als „abgeschlossen" markieren** mit
  Verweis auf die Commits im `state.md`. Falls die Roadmap
  eigene Phasen/Status-Marker hat, in das bestehende Schema
  einpassen.
- **EPIC-08 als „in Umsetzung (Einheit 008)"** markieren.
- **P0/P1-Rest-Erweiterungen** (Konzept Z. 207-324) als
  **nächste Phase** eintragen, mit einer kurzen Aufzählung
  der 7-8 Punkte (Kaltstart, Auto-Discovery, mtime-Sweep,
  --mcp-log, Verzeichnis-Sweep, ILintConsole, Last-Fixture,
  Tool-vs-rg-Empfehlung — letzteres wird in 008 erledigt).

**Wichtig:** Falls `Docs/ROADMAP.md` keinen eigenen
MCP-Server-Block hat, einen neuen Block „MCP-Codegraph-Server
(EPIC-01..08, Erweiterungen)" an passender Stelle einfügen —
**nicht** als Ersatz für bestehende Blöcke.

### Schritt 5 — `README.md` Kurz-Hinweis

In der Sektion „Agentische Integration" (oder einer passenden
Stelle) **einen kurzen Absatz** einfügen:

```markdown
## MCP-Server-Modus

AiNetLinter kann auch als stdio-basierter MCP-Server
gestartet werden, um die Roslyn-basierte Solution-Analyse
als 9 granular abfragbare Tools für AI-Coding-Agenten
bereitzustellen. Details und Registrierungs-Anleitung in
[Docs/agent-api.md](Docs/agent-api.md#mcp-server-modus) und
[Docs/integration.md](Docs/integration.md#mcp-server-registrieren).
```

Cross-Link **muss** funktionieren (Anker `#mcp-server-modus`
bzw. `#mcp-server-registrieren` müssen in den Doku-Dateien
existieren — Coder verifiziert das).

### Schritt 6 — A3-Verifikations-Test schreiben (NEU)

`src/AiNetLinter.Tests/Mcp/McpDocumentationSmokeTests.cs`
anlegen mit folgendem Pflicht-Inhalt:

```csharp
#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using AiNetLinter.Tests.Fixtures;
using Xunit;

namespace AiNetLinter.Tests.Mcp;

/// <summary>
/// A3-Nachweis fuer die MCP-Doku in 008: fuehrt eine kleine Anzahl
/// repräsentativer Tool-Calls gegen die echte AiNetLinter.slnx aus
/// und assertiert wortwoertlich gegen Erwartungs-Strings, die aus
/// der Doku uebernommen sind. Aenderung an der Doku ohne
/// Anpassung dieser Strings = Test wird rot. Doku-Luege = Test
/// wird rot.
/// </summary>
[Trait("Category", "Integration")]
[Collection("ConsoleTestCollection")]
public sealed class McpDocumentationSmokeTests : IClassFixture<McpLiveRepositoryFixture>
{
    private readonly McpLiveRepositoryFixture _fixture;

    public McpDocumentationSmokeTests(McpLiveRepositoryFixture fixture) { _fixture = fixture; }

    [Fact]
    public async Task FindSymbol_ReturnsLinterEngineHit()
    {
        // Erwartung: "LinterEngine" ist ein Symbol, das in der
        // Doku als Beispiel genannt wird. Output enthaelt den Namen.
        var text = await _fixture.Client.CallToolGetTextAsync(
            "find_symbol",
            new Dictionary<string, object?> { ["namePattern"] = "LinterEngine" });
        Assert.Contains("LinterEngine", text);
    }

    [Fact]
    public async Task GetIndexScope_ListsCsAsLargestCategory()
    {
        // Erwartung: "Index-Scope" listet .cs als groesste Kategorie.
        var text = await _fixture.Client.CallToolGetTextAsync(
            "get_index_scope", new Dictionary<string, object?>());
        Assert.Contains(".cs", text);
    }

    [Fact]
    public async Task FindSymbol_WithWidePattern_TruncatesWithMetaLine()
    {
        // Erwartung: Trunkierung nutzt die exakte Meta-Zeile aus
        // McpTruncation.cs:40. Hartkodiert hier als A3-Beweis.
        var text = await _fixture.Client.CallToolGetTextAsync(
            "find_symbol",
            new Dictionary<string, object?>
            {
                ["namePattern"] = "Get",  // sehr verbreitet -> > maxResults Treffer
                ["maxResults"] = 1
            });
        Assert.Contains("Treffer gesamt", text);
        Assert.Contains("gezeigt", text);
    }
}
```

**A3-Verifikation durch den Coder (vor `result.md`-Schreiben):**

```powershell
# 1. Test umbenennen, damit er garantiert rot wird:
#    In FindSymbol_ReturnsLinterEngineHit das "LinterEngine" im
#    Assert durch "LinterEnginXYZ" ersetzen.
# 2. Test laufen lassen:
dotnet test --filter FullyQualifiedName~McpDocumentationSmokeTests --no-build
# Erwartung: rot, weil "LinterEnginXYZ" nicht im Output vorkommt.
# 3. Zurueckbenennen, nochmal laufen:
# Erwartung: gruen.
```

Die wortwörtliche A3-Sequenz (Schritt 1/2/3) muss im
`result.md` dokumentiert sein — wortwörtlich, wie in
`units/007/result.md` A3-1 als Vorbild.

### Schritt 7 — Build + Tests (vor `result.md`)

```powershell
dotnet build AiNetLinter.slnx
# Erwartung: 0/0 (Doku-Edits dürfen den Build nicht beeinflussen,
# aber das ist die Pflicht-Verifikation)

dotnet test AiNetLinter.slnx --no-build
# Erwartung: 1161+1/1161+1 grün (AGENTS.md §2 Volllauf-Pflicht
# vor Task-Beendigung — hier: vor Einheit-Beendigung)
```

Falls Build oder Tests rot: stopp, melden, kein
`result.md` schreiben.

### Schritt 8 — `result.md` schreiben

`units/008/result.md` mit folgendem Pflicht-Inhalt:

1. **Summary** (1 Absatz): Was wurde dokumentiert, mit
   Wortzahl-Diff pro Datei.
2. **What changed**: Tabelle der 4 Doku-Dateien + der
   neuen Test-Datei mit Diff-Größen.
3. **A3-Nachweis** für `McpDocumentationSmokeTests.cs`:
   - A3-1: Test mit umbenanntem Assert → rot (wortwörtliche
     Fehlermeldung).
   - A3-2: Test mit korrektem Assert → grün.
4. **Konzept-Diskrepanzen** (Check 6): Liste der Stellen in
   `konzept.md`, die jetzt veraltet sind (Tool-Status-Tabelle
   Z. 539-552). **Nicht selbst korrigieren** (A7), nur
   melden — der Nutzer entscheidet, ob die Konzept-Tabelle
   angepasst wird.
5. **Self-Lint**: `dotnet run --project src/AiNetLinter
   -- --config rules.json --path tests/Fixtures/BaselineMini`
   oder analog → muss 0 Violations zeigen (Doku-Dateien
   sind `.md`, nicht im Lint-Scope, aber der Lauf beweist,
   dass die Code-Basis weiterhin grün ist).
6. **Commit-Vorschlag** (Pflicht, AiNetLinterRichtlinien.mdc §4):
   - Vorschlag 1: `docs(mcp): EPIC-08 mcp-server-modus + tool-vs-rg-empfehlung [codegraph-mcp-server]`
     → 4 Doku-Dateien + neue Test-Datei.

**Hinweis:** Der Coder darf die Commits selbst anlegen
(Conventional Commits auf Deutsch, imperativ, mit
`[codegraph-mcp-server]`-Suffix wie in 001-007 etabliert),
**kein Push** (A4). Working-Tree clean halten.

## Erwartete Tests

### Neue Tests

**1 Datei NEU:** `src/AiNetLinter.Tests/Mcp/McpDocumentationSmokeTests.cs`
(geschätzt ~120 Z., 3 Tests, alle `[Trait("Category",
"Integration")]`).

- `FindSymbol_ReturnsLinterEngineHit`: Beweist, dass
  `find_symbol` gegen die echte `AiNetLinter.slnx` ein
  erwartetes Symbol liefert (für Doku-Beispiel „LinterEngine").
- `GetIndexScope_ListsCsAsLargestCategory`: Beweist die
  Doku-Aussage „.cs ist die größte Datei-Kategorie".
- `FindSymbol_WithWidePattern_TruncatesWithMetaLine`: Beweist
  die Trunkierungs-Meta-Zeile aus Doku wortwörtlich.

**Erwartete Test-Anzahl:**

- 1161 (vor 008) + 3 = **1164/1164 grün** im Volllauf
  (AGENTS.md §2).
- Unit-Slice: 80/80 grün (neue Tests sind Integration).

### A3-Methodik für die 3 neuen Tests

A3-Pfad: **Lüge in Doku / geändertes Tool-Verhalten
macht den Test rot.** Drei Szenarien, je 1 Test:

| Test | Was er beweist | A3-Pfad (vom Coder dokumentiert) |
|---|---|---|
| `FindSymbol_ReturnsLinterEngineHit` | `find_symbol` liefert für ein bekanntes Symbol ein Ergebnis | Assert auf `"LinterEnginXYZ"` umbiegen → Test rot. Zurückbiegen → grün. |
| `GetIndexScope_ListsCsAsLargestCategory` | `get_index_scope` listet `.cs`-Dateien | Assert auf `".csXYZ"` umbiegen → Test rot. Zurückbiegen → grün. |
| `FindSymbol_WithWidePattern_TruncatesWithMetaLine` | Trunkierungs-Meta-Zeile hat exakt diesen Wortlaut | Assert auf `"Treffer gesamt XYZ"` umbiegen → Test rot. Zurückbiegen → grün. |

Alle 3 wortwörtlich im `result.md` A3-Block
dokumentieren, wie in `units/007/result.md` A3-1 als
Vorbild.

### Reflection-Pfad / no-op-Pfad

Nicht relevant (die Tests sind funktional, nicht
strukturell — sie testen das beobachtbare
Server-Verhalten, nicht die Implementierungs-Struktur).

## Plan-Abweichungen, die explizit erlaubt sind

1. **Sektions-Platzierung in `Docs/agent-api.md` /
   `Docs/integration.md`:** Falls die bestehende Hierarchie
   eine andere Stelle als „ans Ende anfügen" nahelegt (z. B.
   weil eine „Erweiterte Modi"-Über-Sektion existiert), darf
   der Coder die Sektion dort einordnen — solange der
   Cross-Link aus `README.md` und aus `Docs/ROADMAP.md`
   weiterhin funktioniert.
2. **Tabellen-Spalten-Reihenfolge** in der neuen
   Tool-Übersicht in `Docs/agent-api.md`: Der Coder darf die
   Spalten an die bestehende Tabellen-Konvention anpassen
   (z. B. `Tool | Input | Output | Status` statt der
   Konzept-Tabellen-Struktur), solange alle Pflicht-Inhalte
   aus Schritt 2 abgedeckt sind.
3. **JSON-Config-Format in `Docs/integration.md`:** Falls
   der Host-spezifische Wrapper (z. B. `.cursor/mcp.json` für
   Cursor, `.mcp.json` für Claude Code) aus dem
   bestehenden `Docs/integration.md`-Stil hervorgeht, darf
   der Coder das berücksichtigen — solange die Kern-Aussage
   (cwd = Projekt-Root, args=["--mcp-server"]) klar ist.
4. **Beispiel-Tool-Calls in der Doku:** Falls der Coder beim
   Schreiben feststellt, dass die Doku mehr als 1 Beispiel
   pro Tool braucht, um verständlich zu sein, darf er das
   tun — solange das **erste** Beispiel dem tatsächlichen
   Verhalten entspricht (A3 sichert das durch den Test in
   Schritt 6).
5. **Mehr Tests als die 3 im Plan:** Falls der Coder
   während der Arbeit feststellt, dass ein
   weiterer Doku-Punkt durch einen Test abgesichert werden
   sollte (z. B. „Miss-Hint-Datei-Liste wird trunkiert
   zurückgegeben"), darf er bis zu **2 weitere** Tests
   ergänzen — Plan-Abweichung explizit erlaubt, im
   `result.md` zu dokumentieren. Maximal **5 Tests
   gesamt**, weil die Integration-Category-Laufzeit
   sonst spürbar wächst.
6. **`--mcp-log` in Doku NICHT erwähnen**, wenn der Coder
   bei der Code-Lektüre feststellt, dass das Flag noch
   nicht implementiert ist (was nach Stand 007 der Fall
   ist). Stattdessen: nur erwähnen, wenn die Implementierung
   im Repo existiert. Konzept Z. 305-315 ist P1, nicht
   umgesetzt — keine Versprechen in der Doku.

## Bezug zu Projektregeln

| Regel | Datei | Kurzgrund |
|---|---|---|
| **§1 — Doku vor tiefgreifenden Änderungen konsultieren** | `AiNetLinterRichtlinien.mdc` Z. 12-21 | Coder liest in Schritt 1 die gesamte bestehende `Docs/`-Landschaft, bevor er editiert. Doku-Aktualisierung ist explizit Selbstzweck dieser Einheit. |
| **§4 — MCP-Dogfooding NUR via C#-Test-Infrastruktur** | `AiNetLinterRichtlinien.mdc` Z. 70-75 | A3-Verifikation nutzt `McpTestClient` + `McpLiveRepositoryFixture` (Schritt 6), **kein** Python-Skript im `.todos/`. |
| **§4 — Update-Pflicht** | `AiNetLinterRichtlinien.mdc` Z. 77-78 | Genau das, was 008 macht. |
| **§4 — Commit-Vorschlag-Pflicht** | `AiNetLinterRichtlinien.mdc` Z. 83-86 | `result.md` endet mit konkretem Commit-Vorschlag (Schritt 8). |
| **`MaxLineCount: 500`** | `AiNetLinter.mdc` Z. 24 | Doku-Dateien sind Markdown, nicht im Lint-Scope — aber Coder liest die Code-Dateien in Schritt 1 und darf dort **nicht** editieren (A5/A7). |
| **Konzept-Pflicht:** alle 9 Tools in Doku | `konzept.md` Z. 539-552 | Pflicht-Inhalt in Schritt 2.4. |
| **Konzept-Pflicht:** Trunkierungs-Format | `konzept.md` Z. 215-233 | Pflicht-Inhalt in Schritt 2.5. |
| **Konzept-Pflicht:** Tool-vs-`rg`-Empfehlung | `konzept.md` Z. 316-324 | Pflicht-Inhalt in Schritt 3.4. |
| **Konzept-Pflicht:** Doku-Update DoD | `konzept.md` Z. 622-624 | Direktes DoD-Kriterium, das 008 schließt. |
| **AGENTS.md §2 — Test-Kategorien** | `AGENTS.md` Z. 35-49 | Coder darf `Category=Unit` für schnelle Iterationen nutzen; **Volllauf-Pflicht** für finale Verifikation (Schritt 7). |

## Tech-Debt-Aktionen

**Keine TD-Schließungen erwartet.** Doku-Edits lösen
normalerweise keinen bestehenden TD-Eintrag.

**Mögliche neue TD-Einträge** (Coder dokumentiert im
`result.md`, was er findet — A2):

- Falls beim Schreiben der Doku auffällt, dass eine
  Tool-Beschreibung im Code (`description`-Attribut im
  `McpServerTool`-Builder) von der Realität abweicht
  (z. B. ein vergessenes `description`-Feld, ein
  ungenauer Wortlaut): **neuer TD-Eintrag** im
  `result.md`-Block „Tech-Debt-Beobachtungen" — der
  Coder darf den Code **nicht** anfassen (A2/A5/A7).
- Falls `McpServerOptionsFactory.ServerInstructions`
  einen subtilen Fehler hat (z. B. ein Tool namentlich
  auflistet, das es nicht mehr gibt oder umgekehrt):
  ebenfalls **TD-Eintrag**, kein Code-Edit.

**Kein direkter TD-Edit** — der Coder schlägt vor, der
Kritiker / Orchestrator pflegt in `tech-debt.md` ein
(gemäß A2).

## Risiken

- **Risiko 1 (mittel): Doku-Lüge durch Parameternamen-Drift.**
  Der Coder schreibt Parameternamen aus dem Kopf oder aus
  `konzept.md` (grobe Tabelle), nicht aus den
  `ExecuteAsync`-Signaturen. → **Gegenmaßnahme:** Schritt 1
  ist explizit „Werkzeugkasten-Inventar" mit Quellen-Liste;
  Schritt 6 (A3-Test) fängt die häufigsten Fälle ab. Falls
  der Coder beim Schreiben einen Parameternamen verwendet,
  der im Code `gitRef` heißt, aber in der Doku `git_ref`
  — der A3-Test würde das nicht zwingend fangen, weil er
  nur 3 Tools testet. → **Ergänzende Maßnahme:** Coder
  muss beim Schreiben jeden Parameternamen gegen die
  Signatur verifizieren und im `result.md` (Block
  „Parameternamen-Verifikation") behaupten, dass er das
  getan hat. Der Kritiker prüft das stichprobenartig
  (z. B. 3 zufällige Parameternamen nachschlagen).

- **Risiko 2 (niedrig): Cross-Link-Anker falsch.** `README.md`
  linkt auf `#mcp-server-modus` und `#mcp-server-registrieren`
  in den Doku-Dateien. Falls der Coder die Sektion anders
  benennt (z. B. „MCP-Modus" statt „MCP-Server-Modus"),
  stimmen die Anker nicht. → **Gegenmaßnahme:** Schritt 5
  nennt die Anker wortwörtlich; Plan-Abweichung 1 erlaubt
  lokale Anpassung, **verbietet aber** Anker-Umbenennung
  ohne entsprechende README-Link-Anpassung.

- **Risiko 3 (niedrig): Volllauf dauert lange.** 1161+3 Tests
  in ~6 min, aber reproduzierbar. Falls der Coder den
  Volllauf in CI nicht abwarten will, darf er Unit-Slice +
  gezielten Integration-Slice (`--filter
  Category=Integration&FullyQualifiedName~McpDocumentation`)
  fahren — Pflicht-Doku im `result.md`. Volllauf bleibt
  Pflicht.

- **Risiko 4 (sehr niedrig): Konzept-Diskrepanz Tool-Tabelle
  wird zu großem Diskussionspunkt.** Wenn der Kritiker die
  Konzept-Diskrepanz (Check 6) anders bewertet als der
  Planer und eine Konzept-Änderung fordert → der Planer
  hat in Schritt 6 / Block „Konzept-Diskrepanzen" klar
  gemacht, dass der Coder **nicht** das Konzept editiert
  (A7). Der Nutzer entscheidet. Falls der Kritiker
  `issues` verdictet mit „Konzept muss angepasst werden" →
  Folge-Runde (`008/fix-01`) für die Konzept-Diskussion,
  nicht für Doku-Inhalt.

## Bewusst-NICHT-in-008-Liste (zur Wiederholung, kurz)

1. Keine P0/P1-Rest-Erweiterungen (Kaltstart,
   Auto-Discovery, mtime-Sweep, `--mcp-log`,
   Verzeichnis-Sweep, `ILintConsole`, Last-Fixture).
2. Keine Code-Änderungen.
3. Kein TD-016a (Fixture-Refactor).
4. Kein TD-008/TD-009-Refactor
   (`ILinterEngineConfig` / Constructor-`record`).
5. Kein Auto-Generate der Doku aus `rules.json`.
6. Kein Konzept-Edit (`A7`).
7. Kein Push (`A4`).
8. Keine englische Übersetzung der Doku (deutsch
   bleibt, konsistent zum Rest).
9. Kein `--mcp-log`-Flag in Doku (nicht implementiert).
10. Kein Versprechen von Features, die noch nicht im
    Code sind.

---

## Zusammenfassung (für Orchestrator)

- **Wahl:** EPIC-08 (Doku), reine Markdown-Updates.
- **Scope:** 4 Doku-Dateien + 1 neuer A3-Verifikations-Test.
- **Risiko:** Niedrig (reine Doku), A3 sichert
  Parameter-/Format-Korrektheit gegen den laufenden Server.
- **Erwarteter Aufwand:** ~4-6h für den Coder
  (Schritt 1 ist Lesen, Schritte 2-5 sind Doku-Schreiben,
  Schritt 6 ist Test, Schritt 7 ist Verifikation, Schritt 8
  ist `result.md`).
- **Aufruf-Budget:** 3 (Planer + Coder + Kritiker) — passt
  in die verbleibenden 18 von 40.
- **Konzept-Treue:** Achtet A7 (kein Konzept-Edit),
  erkennt 2 veraltete Stellen in der Konzept-Tabelle und
  meldet sie (Konzept-Diskrepanzen-Block im `result.md`).
- **TD-Aktionen:** Keine Schließungen; eventuell 1-2 neue
  Vorschläge, wenn der Coder Inkonsistenzen findet
  (A2-Modus: vorschlagen, nicht fixen).
