---
status: done
type: step-result
task: 03_get-impact-zum-diff-kontext-erweitern
step: 009
epic: EPIC-7
step_type: single
coded_by: coder
coded_by_model: stealth/ox-alpha
coded_by_model_knowledge_cutoff: unbekannt
coded_at: 2026-08-23T12:25:44+02:00
code_commit_hash: e9ed0fe9
status_after: done
blocker_category: n/a
---

# Result Step 009: Doku — change-context-Vertrag, Grenzen & Verhaltenskorrektur

## Zusammenfassung

Der ausgelieferte Stand ist in der Agent-Doku vollständig sichtbar:
`Docs/agent-api.md` dokumentiert den change-context-Vertrag zeichenexakt
(get_impact-Tabellenzeile mit allen drei Parametern und korrigierter
depth-Klammer; neuer Detailabschnitt „get_impact (`detailLevel=change-context`)
— Structured Output im Detail" mit JSON-Beispiel, Vertragsregeln und allen
sechs Grenzen), weist die depth>1-Korrektur im E.2-Abschnitt ausdrücklich als
**Verhaltenskorrektur** aus (Audit B), ergänzt die Reached-From-`#lf:`-
Anmerkung im transitiven Abschnitt, nennt die statische Test-Zuordnung auch
für `testAssociations` und präzisiert die Trunkierungs-Ausnahme des Modus.
README-Zeile und ROADMAP-Abschnitt spiegeln den Feature-Stand. Jede
Beschreibung wurde vor dem Schreiben gegen den Code verifiziert
(ChangeContextResponseModels.cs/-Contract, GetImpactTool.cs,
DiffImpactAnalyzer.ApplyChangedSymbolCap, TestCoverageMatchReasons,
TestRecommendationBuilder, CallGraphTraversal.GetStableSymbolId,
FilePathPrefix-Parsing) — nicht aus dem Plan abgeschrieben.

## Geänderte Dateien

- `Docs/agent-api.md` — Tool-Tabelle get_impact (Parameter + Output-Spalte),
  statische-Zuordnung-Notiz um change-context erweitert, Structured-Output-
  Intro präzisiert (CallSiteEntry-Form nur für callers), NEU
  change-context-Detailabschnitt (nach dem transitiven Abschnitt, vor
  safeguard), transitive Response um `#lf:`-Reached-From-Sonderfall ergänzt,
  E.2 mit Verhaltenskorrektur-Absatz + Schlusswortlaut „im gesamten
  Git-Branch", Trunkierungs-Format um die change-context-Ausnahme ergänzt.
- `README.md` — get_impact-Zeile (Z. 96) um change-context-Erwähnung
  erweitert.
- `Docs/ROADMAP.md` — neuer Abschnitt „Strukturierter Diff-Kontext:
  `get_impact` mit `detailLevel=change-context`" vor dem Footer, Format wie
  bestehende Abschnitte, ohne Task-/Step-/Epic-IDs.

## Commit

- **Code-Commit-Hash:** `e9ed0fe9`
- **Message:**
  ```
  docs: change-context-Doku [03_get-impact-zum-diff-kontext-erweitern]

  get_impact-Vertrag detailLevel=change-context in agent-api.md dokumentiert:
  Tabellenzeile, neuer Detailabschnitt mit exakten Feldnamen, Vertragsregeln,
  sechs Grenzen, depth>1-Verhaltenskorrektur und Reached-From-#lf:-Anmerkung;
  README-Toolzeile und ROADMAP-Abschnitt synchronisiert.

  Refs: tasks/mcp-server-weiterentwicklung/03_get-impact-zum-diff-kontext-erweitern/step-009
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier
  drin — Selbstbezug, siehe `git log`).

## Build-/Test-Output

```
(entfallen)
```

**Gate-Entscheidung:** Build- UND Test-Gates sind auf explizite
Nutzeranweisung hin entfallen (Nutzerbeschleunigung während des Steps):
der Step berührt ausschließlich .md-Dateien — kein C#-Artefakt der Solution
wird von Doku-Änderungen beeinflusst, ein `dotnet build` kann daran nichts
ändern oder brechen. Die im Plan genannten Verifikationen (Feldnamen gegen
`ChangeContextResponseModels.cs`, Validierung/Fehlerfälle gegen
`GetImpactTool`, Verhaltenskorrektur gegen step-001, Grenzen gegen
step-003/step-008) wurden trotzdem ausgeführt — siehe Zusammenfassung.
Letzter grüner Gate-Stand des Codes: step-008 (`e9ed0fe9` berührt keinen
Code). Der letzte Commit dieser Session ist rein additiv an Markdown.

## Abweichungen vom Plan

1. **Gate entfällt (Nutzeranweisung):** Plan-DoD forderte `dotnet build`
   plus beide Nicht-Stress-Testläufe; der Orchestrator hatte bereits
   build-only vorgesehen, der Nutzer hat während des Steps auch das
   Build-Gate ersatzlos gestrichen („rein Doku"). Dokumentiert oben.
2. **Trunkierungs-Format-Abschnitt angepasst (im Plan nur als Prüfauftrag
   vorgesehen):** Die Vier-Tools-Aussage stimmt für den change-context-Modus
   faktisch nicht mehr unverändert (dort kappet `maxResults` nur die
   Text-Toplisten und es hängt eine eigene `[Teilergebnis: …]`-Zeile statt
   der einheitlichen McpTruncation-Meta-Zeile) — Plan-Note erlaubte genau
   diesen Fall („nur bei faktischer Abweichung anpassen"); ein Satz Ergänzung.
3. **matchReason-Literale vollständiger als im Plan skizziert:** Der Plan
   nannte zwei Evidenzarten („direkte Invocation, Namenskonvention"); gegen
   `TestCoverageMatchReasons` verifiziert gibt es VIER Literal-Formen
   (`Direct Member Match / Invocation`, `Naming Convention Match`,
   `Explicit @covers Comment`, `Direct typeof Reference`) mit Priorität in
   dieser Reihenfolge — alle vier benannt, statt Beispielwerte als Vertrag
   auszugeben (Plan verlangte die Verifikation gegen diese Konstanten).
4. **CodeMap über die Marker hinaus aktualisiert:** Plan-Note sah nur die
   „zuletzt"-Marker vor; die Ist-Stand-Beschreibungen der drei Doku-Einträge
   beschrieben aber den Defizit-Zustand VOR diesem Step („kennt die Parameter
   nicht" usw.) und hätten den nächsten Leser aktiv irregeführt — auf den
   Jetzt-Stand umgeschrieben (Pointer-Prinzip bleibt gewahrt).
5. Sonst Plan 1:1 umgesetzt (alle sechs Konkreten-Änderungen-Punkte,
   JSON-Feldnamen zeichenexakt gegen die DTOs geprüft, README-Wortlaut wie
   skizziert, ROADMAP ohne IDs).

## Beobachtungen

- **Fremdänderung erledigt sich selbst:** Die uncommittete Nutzeränderung an
  `tasks/mcp-server-weiterentwicklung/11_epic-projektregistry-und-daemon/Konzept.md`
  war bei Sessionstart NICHT MEHR uncommittet — der Nutzer hat sie zwischenzeitlich
  selbst committet (`0eb5851b`, `638a7c13` u. a.). Das Vorsichtsgebot blieb
  beachtet (nur explizite Pfade gestaged, `git status` vor beiden Commits
  geprüft); ein Risiko bestand faktisch keins.
- **JSON-Beispiel nutzt reale Werte wo verifizierbar:** `matchReason` trägt
  das echte Literal `Direct Member Match / Invocation`, `recommendedTestCommands`
  folgt dem realen `TestRecommendationBuilder`-Format (`dotnet test <projekt>
  --filter FullyQualifiedName~<Klasse>`); `ruleName`/`details` bleiben `...`
  (wie im safeguard-Vorbild) — Regelnamen sind konfigurierbar, keine
  Vertragssache.
- **accessibility-Dokumentation über Planumfang hinaus präzisiert:** Der
  String-Charakter von `accessibility` (keine Zahl — die zentrale JSON-Policy
  hat keinen Enum-Converter) steht so im XML-Doc des DTOs und ist jetzt
  ebenfalls im Detailabschnitt benannt; die optionale Nuance „lokale
  Funktionen lesen sich als private (Roslyn-Default)" habe ich dagegen
  bewusst NICHT übernommen (Plan: „kein Muss").
- **Gelöschte-Dateien-Grenze technisch belegt:** `ParseGitDiffHunkRanges`
  matcht Dateizeilen nur über `+++ b/` (`FilePathPrefix`) — `+++ /dev/null`
  fällt durch, daher erscheinen gelöschte Dateien weder in `changedFiles`
  noch in `changedSymbols`. Dokumentiert als Grenze (a), exakt wie im Plan.

## Bekannte Unschärfen

- **Umbenennungs-Grenze (b) ist Verhaltensbeschreibung nach Git-Semantik,
  nicht per Test belegt:** Mit Rename-Detection landen Hunks unter dem neuen
  Pfad (`+++ b/neu.cs`), ohne Detection erscheinen Löschung (unsichtbar,
  Grenze a) und Neuanlage getrennt — abgeleitet aus dem Parser-Verhalten und
  den step-008-Result-Angaben, nicht durch einen eigenen Renaming-Test
  abgesichert.
- **„Kein Repo / leerer Diff"-Wortlaut:** Die Textantwort sagt „Kein
  Git-Repository oder leerer Diff"; die Doku beschreibt das Verhalten
  (leeres vertragsgültiges Objekt, Sufficiency-Hint) nach
  `BuildEmptyPayload`/Tool-Pfad — ob in jeder Umgebungsvariante (fehlendes
  git-Binary vs. echtes Null-Repo) derselbe Pfad greift, habe ich nicht
  einzeln verfolgt; der Vertragssatz (kein Fehlerfall, leere Struktur)
  stimmt zu beiden.
- **Gate-Lücke:** Durch die Nutzeranweisung lief in diesem Step KEIN Build/
  Testlauf. Theoretisch könnte eine seit step-008 fremdverändertes Working-
  Tree-Datei unbemerkt geblieben sein — `git status` zeigte außer meinen
  drei Doku-Dateien jedoch nichts an, d. h. der Baum war vor meinem Commit
  code-seitig clean gegenüber HEAD.
