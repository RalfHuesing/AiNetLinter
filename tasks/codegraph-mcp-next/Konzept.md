---
type: konzept-vorstufe
status: draft
depends_on: tasks/codegraph-mcp   # muss abgeschlossen sein, bevor hieraus ein Task wird
last_updated: 2026-07-31
---

# AiNetLinter MCP Codegraph Server — Next-Step-Konzept (`codegraph-mcp-next`)

Nachfolge-Arbeiten zum laufenden Task `tasks/codegraph-mcp`. **Kein Eingriff in
den laufenden Flow** — alles hier ist explizit *danach*.

---

## 0. Lesehinweis

### Delta-Prinzip

Dieses Dokument enthält **nur, was nicht schon in
[`tasks/codegraph-mcp/roadmap.md`](../codegraph-mcp/roadmap.md) geplant oder
bereits umgesetzt ist**. Punkte, die sich beim Abgleich mit dem Ist-Stand als
erledigt oder eingeplant erwiesen haben, sind ersatzlos gestrichen (siehe §6) —
zwei parallele Wahrheiten wären für den nächsten Planer schlimmer als eine
unvollständige Liste.

### Prioritäten

| Prio | Bedeutung |
| :--- | :--- |
| **P0** | Muss entschieden/eingeplant werden, **bevor** der Haupt-Task fertig ist — Retrofit-Kosten steigen mit jedem weiteren fertigen Tool. |
| **P1** | Hoch. Führt heute zu **falschen oder unbrauchbaren Antworten** in realer Nutzung, bzw. entscheidet, ob der Server überhaupt benutzt wird. |
| **P2** | Mittel. Echter Mehrwert, aber ohne entsteht kein Schaden. |
| **P3** | Niedrig / später, nur bei belegtem Bedarf. |

### Belegprinzip

Jedes Defizit in §2 ist am tatsächlichen Code belegt (Datei:Zeile), nicht aus
dem Konzept abgeleitet. Die dort genannten Stellen sind Stand **2026-07-31**,
vor Abschluss von step-010 — vor Umsetzung erneut prüfen.

---

## 1. P0 — vor Abschluss des Haupt-Tasks entscheiden

### P0-1 · `max_results` + Trunkierung für alle Listen-Tools

**Problem.** [`FindSymbolTool.cs:44`](../../src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs)
sucht per `Contains` über alle Quell-Deklarationen und gibt **eine Zeile pro
Fundstelle** aus — ohne jede Obergrenze. `find_symbol("Get")` gegen eine
160k-LOC-Solution schüttet dem Agenten vierstellig viele Zeilen in den Context.
Dasselbe gilt für `find_references` auf `ToString`/`ExecuteAsync`/`ILogger`, für
`get_impact` bei breiten Diffs und für `search_pattern`. Das ist das exakte
Gegenteil des Projektziels („weniger verbrannte Tokens pro Agenten-Task").

**Warum P0.** Rein additiv, aber **querschnittlich**: der Retrofit betrifft
jedes Listen-Tool *und* dessen Integrationstests. Heute sind das 8 Tools, nach
Abschluss von EPIC-04 sind es 9 plus die in EPIC-07 dazukommenden Tests. Der
Aufwand wächst monoton — er wird nie billiger als jetzt.

**Umsetzungsempfehlung.**
- Ein gemeinsamer Trunkierungs-Helper neben
  [`McpToolResults`](../../src/AiNetLinter/Mcp/McpToolResults.cs) — dort liegt
  bereits das geteilte Ergebnis-Boilerplate, das ist der etablierte Ort.
- Optionaler Parameter `maxResults` an jedem Listen-Tool, Default **50**.
  Optional statt Pflicht, weil ein LLM einen Pflichtparameter mit
  Fantasiewerten füllt; ein Default, den der Agent bewusst hochsetzen kann, ist
  robuster.
- Abgeschnittene Antworten enden mit **einer** Meta-Zeile, die dem Agenten den
  nächsten Zug nahelegt statt nur zu melden — Format siehe P0-2.
- **Wichtig für die Zählung:** `find_symbol` gibt heute pro Symbol *n* Zeilen
  aus (eine je `Location`). Das Limit muss auf **Ausgabezeilen** wirken, nicht
  auf Symbole, sonst greift es bei `partial`-Typen nicht.

### P0-2 · Ausgabeformat verbindlich festlegen: Text, nicht JSON

**Problem.** Die vorige Fassung dieses Dokuments hat Trunkierungs-Metadaten als
JSON skizziert (`"truncated": true, "total_count": 342`). Die Umsetzung liefert
durchgängig **Plain-Text-Zeilen** über `McpToolResults.Text`. Bleibt das
ungeklärt, baut der nächste Coder JSON, weil das Konzept es so andeutet — und
der Server hat zwei Ausgabeformate.

**Entscheidung (Empfehlung): bei Text bleiben.**
- Token-günstiger als JSON (keine Klammern, Quotes, Feldnamen pro Zeile).
- LLMs lesen `Pfad:Zeile - Kind: Signatur` zuverlässig; der Mehrwert von JSON
  entstünde erst bei programmatischen Konsumenten, die es hier nicht gibt.
- Konsistent mit dem bestehenden `[ERROR]`-Textformat aus
  [`LinterErrorFormatter`](../../src/AiNetLinter/Output/LinterErrorFormatter.cs),
  das der Server für Fehlerfälle bereits nutzt.

**Umsetzungsempfehlung.** Eine feste, in allen Tools identische Meta-Zeile am
Ende, sinngemäß: `[342 Treffer gesamt, 50 gezeigt — Pattern verfeinern oder
maxResults erhöhen]`. Das gehört als verbindliche Format-Regel in
`Docs/agent-api.md` (EPIC-08), damit spätere Tools nicht davon abweichen.

### P0-3 · Regel-ID in der `get_violations`-Ausgabe

**Problem & Chance.** Die gesamte `rules.json`-Verzahnung aus der Vorfassung
(vier Ideen, alle mit Nutzer-Vorbehalt) löst sich auf, **wenn `get_violations`
die Regel-ID/den Regelnamen pro Verstoß ausgibt**: der Agent hat den zugehörigen
Regeltext über die ohnehin geladene `.agents/rules/AiNetLinter.mdc` bereits im
Kontext. Damit braucht es kein `agent_hint`-Feld, keine `mcp_config`-Filterung
und kein `get_active_rules`-Tool — das Argument „redundant zu `AiNetLinter.mdc`",
mit dem `get_active_rules` verworfen wurde, erledigt die anderen drei gleich mit.

**Warum P0.** `get_violations` entsteht gerade in step-010. Gibt es die Regel-ID
schon aus, ist dieser Punkt kostenlos erledigt; wenn nicht, ist es ein kleiner
Retrofit — aber einer, der die komplette rules.json-Diskussion offen hält.

**Umsetzungsempfehlung.** Nach Abschluss von step-010 einmal die
`get_violations`-Ausgabe prüfen. Fehlt die Regel-ID: als erster Punkt in den
Folge-Task. Kein neues Feld in `rules.json`, keine Prosa-Hinweise — nur die ID,
die der Linter intern ohnehin führt.

---

## 2. P1 — Belegte Defizite der aktuellen Umsetzung

Diese Punkte sind **nicht** im bestehenden
[`tech-debt.md`](../codegraph-mcp/tech-debt.md) erfasst (dort stehen TD-001…007,
überwiegend `AIContextFootprint`- und Test-Infrastruktur-Themen) und auch in
keinem offenen Epic adressiert.

### P1-1 · Neu angelegte und gelöschte `.cs`-Dateien werden nie sichtbar

**Befund.** `RefreshStaleDocuments()` in
[`McpCodeGraphServer.cs:126`](../../src/AiNetLinter/Mcp/McpCodeGraphServer.cs)
iteriert ausschließlich über `_catalog.Solution.Projects` → `project.Documents`,
also über die Dokumente, die **beim Serverstart** in der Solution waren. Eine
Datei, die danach entsteht, existiert für den Server bis zum Neustart nicht.

**Warum das der gefährlichste Fall ist.** Der Server antwortet nicht mit einem
Fehler, sondern mit einer **plausiblen Lüge**: „Keine Treffer für
`NewOrderValidator`" — für eine Klasse, die der Agent zwei Schritte vorher
selbst geschrieben hat. Und im Agenten-Dev-Loop, für den der Server gebaut wird,
sind neue Dateien der Normalfall, nicht der Grenzfall. Die Vorfassung dieses
Dokuments hat das Thema nur als „`.csproj` geändert / NuGet-Paket hinzugefügt"
gerahmt — das ist der seltenere und harmlosere Teil des Problems.

**Umsetzungsempfehlung.**
1. Zusätzlich zum bestehenden Dokument-Sweep die Projektverzeichnisse auf
   `.cs`-Dateien prüfen, die in keinem `Document` der Solution vorkommen, und
   sie über das Roslyn-Solution-API als Dokument in das passende Projekt
   einhängen (Projekt-Zuordnung über den längsten gemeinsamen Pfad-Präfix mit
   dem Projektverzeichnis).
2. Umgekehrt: Dokumente, deren Datei nicht mehr existiert, aus der Solution
   entfernen. Heute liefert `TryRefreshDocument` bei fehlender Datei schlicht
   `false` — das Symbol bleibt im Graph und wird weiter als Treffer gemeldet.
3. **Bewusste Grenze:** Dateien, die per `<Compile Remove=...>` o. ä. gar nicht
   zum Projekt gehören, würden so fälschlich aufgenommen. Für die realen
   SDK-style-Projekte im Zielumfeld ist Glob-Inklusion der Normalfall; die
   Abweichung ist als bekannte Einschränkung zu dokumentieren, statt dafür einen
   vollen `.csproj`-Parser zu bauen.
4. Als Kurzschluss gegen die Kosten: Verzeichnis-`mtime` je Projektverzeichnis
   cachen und die Datei-Enumeration überspringen, solange sie unverändert ist —
   das löst gleichzeitig P1-4.

### P1-2 · `rules.json` wird ohne `--config` still ignoriert

**Befund.** [`McpServerCommand.ResolveConfig`](../../src/AiNetLinter/Commands/McpServerCommand.cs)
liefert bei leerem `args.ConfigPath` einen **Default-`Config`**. `--config` hat
in [`CliOptionFactory.cs:12`](../../src/AiNetLinter/Cli/CliOptionFactory.cs)
keinen Default und es gibt **keine Auto-Suche** nach `rules.json`. Für den
Audit-Lauf ist `--config` dagegen *erforderlich* — `ConfigLoader.TryLoadConfig`
gibt dort mit `isRequired: true` einen expliziten Fehler aus.

**Konsequenz.** Der im Konzept als Normalfall beschriebene Start (MCP-Host
startet `ainetlinter --mcp-server` mit `cwd` = Projekt-Root, ohne weitere Args)
führt dazu, dass `get_violations` gegen **Default-Regeln statt gegen die
Projekt-`rules.json`** prüft und `get_hotspots` den Default-`MaxLineCount` (700)
statt des konfigurierten Werts verwendet — beides ohne jeden Hinweis. Der
XML-Doc-Kommentar an `ResolveMaxLineCount` („derselbe Grenzwert, den auch ein
CLI-Lint-Lauf respektieren würde") gilt nur bei explizit übergebenem `--config`.
Damit widerspricht der MCP-Modus in seiner Default-Konfiguration dem Rest des
Werkzeugs — und liefert still falsche Ergebnisse, wo die CLI sich weigern würde.

**Umsetzungsempfehlung.**
1. Fehlt `--config`, nach `rules.json` **neben der aufgelösten Solution-Datei**
   suchen (nicht im cwd — die Solution ist der verlässliche Anker) und bei
   Fund verwenden.
2. Wird dabei keine gefunden, eine `[WARN]`-Zeile auf **stderr** ausgeben, die
   benennt, dass mit Default-Regeln gearbeitet wird.
3. Zusätzlich sollte `get_violations` diesen Zustand **in der Antwort selbst**
   vermerken (eine Zeile, z. B. „Basis: Default-Regeln, keine `rules.json`
   gefunden") — der Agent sieht das Server-Log nicht, und eine Verstoßliste ohne
   Kenntnis ihrer Regelbasis ist irreführend.

### P1-3 · Kaltstart blockiert vor dem Protokoll-Handshake

**Befund.** In
[`McpServerCommand.RunAsync`](../../src/AiNetLinter/Commands/McpServerCommand.cs)
wird `TryLoadSolutionAsync` **vollständig abgewartet**, bevor
`StdioServerTransport` und `McpServer.Create` überhaupt entstehen. Bis der
MSBuild-Load fertig ist, ist auf stdio niemand da.

**Konsequenz.** Ein MCP-Host schickt direkt nach Prozessstart `initialize`.
Bei 160k LOC dauert der `MSBuildWorkspace`-Load leicht 30–60 s — in dieser Zeit
antwortet der Prozess auf nichts. Hosts mit Startup-Timeout markieren den Server
als fehlgeschlagen; im besten Fall sieht der Nutzer einen scheinbar hängenden
Server. Das Projekt hatte in step-005 bereits einen stdio-Hang — dieselbe
Fehlerklasse, andere Ursache. Getestet ist das bisher nur gegen Mini-Fixtures,
wo der Load Millisekunden dauert und der Effekt strukturell nicht auftreten kann.

**Umsetzungsempfehlung.**
1. Reihenfolge umdrehen: Transport und Server **zuerst** aufsetzen, den
   Solution-Load als Hintergrund-Task starten.
2. `McpCodeGraphServer` bekommt neben `IsLoaded` einen dritten Zustand
   („lädt noch"). `GetCurrentSolution()` blockiert dann nicht, sondern die Tools
   liefern über einen neuen `McpToolResults`-Kurzformer eine strukturierte
   Antwort im bestehenden `[ERROR]`-Format, sinngemäß: „Solution wird noch
   geladen (seit N s) — Aufruf in Kürze wiederholen."
3. Bewusst **kein** Blockieren-mit-Timeout: eine sofortige, ehrliche Antwort ist
   für einen Agenten verwertbar (er kann etwas anderes tun), eine 45-Sekunden-
   Blockade nicht.
4. Der `instructions`-Text aus EPIC-05 sollte diesen Zustand einmal erwähnen,
   damit der Agent ihn nicht als „Server kaputt" interpretiert.

### P1-4 · Staleness-Sweep über alle Dateien bei jedem Tool-Call

**Befund.** `RefreshStaleDocuments()` ruft `File.GetLastWriteTimeUtc` für
**jede** Datei **jedes** Projekts — bei **jedem** `GetCurrentSolution()`, also
bei jedem einzelnen Tool-Call. Das Konzept des Haupt-Tasks sagt „für die
betroffene(n) Datei(en)"; implementiert ist ein voller Sweep.

**Konsequenz.** Bei ~3.000 Dateien sind das 3.000 Datei-Metadaten-Zugriffe pro
Tool-Call. Auf lokaler SSD unkritisch; mit Virenscanner-Interception oder auf
Netzlaufwerken wird das pro Aufruf spürbar — und der Server ist genau dafür
gebaut, viele kleine Aufrufe zu beantworten.

**Umsetzungsempfehlung.** Verzeichnis-`mtime`-Kurzschluss (siehe P1-1, Punkt 4):
Änderungen an einer Datei aktualisieren die `mtime` ihres Verzeichnisses —
unveränderte Verzeichnisse können komplett übersprungen werden. Das löst P1-1
und P1-4 in einem Zug und ist der Grund, beide zusammen anzugehen statt einzeln.
Ein gezielter Pfad-Filter (nur die vom Tool angefragte Datei prüfen) wäre die
Alternative, funktioniert aber nicht für Tools ohne Dateibezug (`find_symbol`,
`get_hotspots`).

### P1-5 · Kein struktureller Schutz gegen stdout-Schreiber

**Befund.** `LinterConsole.WriteLine` schreibt nach
[stdout](../../src/AiNetLinter/Output/LinterConsole.cs), `WriteError` nach
stderr. Im stdio-MCP-Modus ist stdout der **Protokollkanal** — eine einzige
Textzeile dorthin zerstört das JSON-RPC-Framing und damit die Session.
`DiffImpactAnalyzer` (Basis von `get_impact`) enthält ein direktes
`Console.WriteLine`; es ist heute nur deshalb harmlos, weil `GetImpactTool` mit
`verbose: false` aufruft.

**Warum das trotzdem P1 ist.** Der Schutz beruht aktuell auf Disziplin, nicht
auf Struktur. Der MCP-Modus verwendet wachsend viele bestehende CLI-Komponenten
(`LinterEngine`, `HotspotMapBuilder`, `SkeletonMapBuilder`, `DiffImpactAnalyzer`)
— jede davon kann bei einer künftigen, völlig MCP-fremden Änderung eine
stdout-Ausgabe bekommen. Die Testsuite würde das nicht bemerken: der einzige
echte E2E-Test prüft laut TD-002 nur, dass eine Tool-Liste zurückkommt.

**Umsetzungsempfehlung.**
1. Eine `ILintConsole`-Implementierung für den MCP-Modus, die **auch**
   `WriteLine` nach stderr leitet, und sie in `McpServerCommand` verdrahten.
   Danach ist stdout im MCP-Prozess ausschließlich Protokollkanal.
2. Ergänzend ein E2E-Test, der den Serverprozess mit einer Abfolge echter
   Tool-Calls füttert und assertiert, dass **jede** stdout-Zeile ein gültiger
   JSON-RPC-Frame ist. Das ist die einzige Prüfung, die diese Fehlerklasse
   dauerhaft abfängt, und passt thematisch in EPIC-07 — gehört aber, da dort
   nicht vorgesehen, in diesen Folge-Task.

---

## 3. P1/P2 — Lücken im Plan (nicht im Code)

### P1-6 · Es gibt keinerlei Skalierungsnachweis

**Befund.** EPIC-09 (Praxistest gegen ~160k LOC) wurde gestrichen, weil die
vorgesehene externe Solution in diesem Checkout nur ~3.600 Zeilen hatte. Ersetzt
wurde es durch Dogfooding gegen `AiNetLinter.slnx` — sinnvoll für Korrektheit,
aber der Haupt-Task hält selbst fest, dass die Skalierungsfrage damit **offen
bleibt**. Alle Kernbegründungen des Projekts (Token-Ersparnis, resident statt
Batch, Kaltstart-Amortisation) sind Aussagen über große Solutions.

**Konsequenz.** Genau die Defizite P1-3 und P1-4 sind Effekte, die unterhalb
einiger tausend Dateien strukturell unsichtbar bleiben. Ohne Skalierungsziel
wird der Server für einen Anwendungsfall optimiert, den niemand gemessen hat.

**Umsetzungsempfehlung.** Kein „Praxistest" mit Nutzerbericht — das war schon
einmal der Fehler. Stattdessen ein **generierter** Last-Fixture: ein
Test-Hilfsmittel, das eine synthetische Solution definierter Größe (z. B. 500 /
5.000 Dateien mit realistischen Referenzketten) erzeugt, plus ein Messlauf, der
Kaltstart-Zeit und die Dauer je Tool-Call protokolliert. Agentenseitig
reproduzierbar, kein externes Repo nötig, und der einzige Weg, P1-3/P1-4
überhaupt zu belegen statt zu vermuten.

### P2-1 · Definition of Done kennt kein Antwortgrößen-Kriterium

Die DoD des Haupt-Tasks fordert je Tool „korrekte Ergebnisse". Kein einziges
Kriterium betrifft die **Größe** der Antwort — obwohl Token-Ersparnis die
Projektbegründung ist. **Empfehlung:** in die DoD des Folge-Tasks ein
Obergrenzen-Kriterium aufnehmen (jedes Listen-Tool liefert bei generischer
Anfrage gegen die Last-Fixture aus P1-6 eine Antwort unter N Zeilen).

### P2-2 · Staleness-Tests decken nur Änderung ab, nicht Anlegen/Löschen

EPIC-07 plant Tests für „Änderung zwischen zwei Tool-Calls" — genau der Fall,
der bereits funktioniert. Die in P1-1 beschriebene Lücke bliebe auch nach
vollständigem EPIC-07 untestet. **Empfehlung:** Testfälle für „Datei angelegt"
und „Datei gelöscht" gemeinsam mit dem P1-1-Fix, nicht nachgelagert.

---

## 4. P2 — Neue Fähigkeiten (Markt-Benchmark, reduziert)

Von den ursprünglich fünf Benchmark-Zeilen bleiben drei; die anderen beiden sind
in §6 gestrichen.

### P2-3 · `get_symbol_body` + stabile Symbol-IDs — **ein** System, nicht zwei Features

`get_symbol_body` (Serena) und stabile `DocumentationCommentId`s waren in der
Vorfassung getrennte Abschnitte. Sie ergeben nur zusammen Sinn, und zusammen
sind sie der **größte Token-Hebel** in diesem gesamten Dokument — vor
Blast-Radius:

1. Der Agent holt `get_file_skeleton` — Signaturen ohne Bodies, günstig.
2. Das Skelett liefert pro Member die stabile Symbol-ID gleich mit.
3. Der Agent holt mit genau einer weiteren Abfrage die 15 Zeilen Body, die er
   wirklich braucht — statt einer 500-Zeilen-Datei.

Die stabile ID trägt dabei zwei Lasten gleichzeitig: sie überlebt die
Zeilenverschiebungen durch die eigenen Edits des Agenten, und sie
disambiguiert Overloads (`ProcessOrder(int)` vs. `ProcessOrder(OrderDto)`) ohne
Ratespiel.

**Umsetzungsempfehlung.**
- Zuerst `get_file_skeleton` um die ID pro Member erweitern (kleiner Eingriff,
  sofort nützlich, auch ohne das neue Tool).
- Dann `get_symbol_body`, das **beide** Identifikator-Formen akzeptiert: die
  stabile ID *und* das bereits etablierte `Datei:Zeile:Spalte`-Format, das
  `SymbolIdentifierResolver` heute schon auflöst. Der bestehende Resolver ist
  die richtige Stelle für die zusätzliche ID-Form — kein zweiter Auflösungsweg.
- Ausgabe hart begrenzen (siehe P0-1); ein Body kann eine 800-Zeilen-Methode
  sein, und genau die will man nicht ungefiltert im Context haben.

### P2-4 · Blast-Radius als `depth`-Parameter, nicht als neues Tool

Transitive Auswirkungsanalyse ist wertvoll („wenn ich diese Signatur ändere, was
bricht über N Ebenen?"). Sie gehört aber als optionaler `depth`-Parameter an
`find_references`/`get_impact` — **nicht** als zusätzliches Tool. Je mehr
ähnliche Tools ein Server anbietet, desto häufiger greift das LLM zum falschen;
ein Parameter mehr ist billiger als ein Tool mehr.

**Umsetzungsempfehlung.**
- Default `depth = 1` (heutiges Verhalten, keine Verhaltensänderung),
  Obergrenze fest verdrahtet (z. B. 3) statt frei wählbar.
- Zusätzlich ein Knotenlimit, unabhängig von `maxResults` — transitive Suche
  kann exponentiell wachsen, bevor überhaupt formatiert wird.
- **Ab `depth > 1` aggregiert ausgeben**, nicht flach: „37 Aufrufer in 12
  Dateien, davon 9 in 3 anderen Projekten", danach die Top-N nach Betroffenheit.
  Eine flache Liste bei Tiefe 3 ist der nächste Token-Brand — die Antwort auf
  „was bricht" ist ein Überblick, keine Aufzählung.

### P2-5 · DI-Registrierung als Zusatzzeile in `get_type_hierarchy`

„Welche konkrete Klasse steckt hinter `IFoo`?" ist zu ~80 % bereits von
`get_type_hierarchy` beantwortet (`FindImplementationsAsync` wird dort schon
genutzt). Der fehlende Teil ist die DI-Registrierung — und der ist **keine
Roslyn-Hierarchiefrage**, sondern eine Textsuche nach
`AddScoped<IFoo`/`AddSingleton<IFoo`/`AddTransient<IFoo`.

**Umsetzungsempfehlung.** Kein eigenes Tool. `get_type_hierarchy` hängt bei
Interfaces eine Zeile an, sofern eine Registrierung gefunden wurde („Registriert
in `Program.cs:42` als Scoped"). Als reine Textsuche implementiert, mit klarer
Kennzeichnung, dass es sich um einen heuristischen Fund handelt — Factory-
Registrierungen und Convention-based-Scanning erkennt das bewusst nicht.

---

## 5. P1 — Wirkung & Verankerung

Zwei Punkte, die in keinem der bisherigen Dokumente standen, aber darüber
entscheiden, ob die Arbeit Wirkung hat.

### P1-7 · Der Server hat kein Feedback darüber, ob er hilft

**Problem.** Das Projekt begründet sich mit „~69 % weniger Tokens" — aus einer
fremden Studie. Es gibt keinerlei eigene Messung: nicht, welche der 9 Tools ein
Agent tatsächlich aufruft, nicht, welche nie, nicht, wo er Leermengen bekommt,
nicht, wie oft er dieselbe Frage zweimal stellt. Damit werden alle
Priorisierungen in diesem Dokument aus Markt-Benchmark-Tabellen abgeleitet statt
aus eigenen Daten — inklusive meiner.

**Umsetzungsempfehlung.**
- Ein schlankes Call-Log: pro Tool-Call eine Zeile (Zeitstempel, Tool-Name,
  Parameter gekürzt, Ergebniszeilen, ob trunkiert, Dauer, ob Leermenge) in eine
  Datei pro Server-Session.
- Ablageort neben dem bestehenden `cache/`-Verzeichnis, mit demselben
  Solution-Hash im Dateinamen — die Isolationslogik dafür existiert bereits in
  `AnalysisCacheManager` und muss nicht neu erfunden werden.
- **Per Flag opt-in** (z. B. `--mcp-log`), standardmäßig aus: sonst ist es ein
  ungefragter Schreibzugriff im Projektverzeichnis des Nutzers.
- Auswertung bewusst **nicht** automatisieren. Nach zwei Wochen Eigennutzung
  einmal draufschauen reicht, um P2-Prioritäten neu zu ordnen — ein
  Auswertungswerkzeug zu bauen wäre genau die Überkonstruktion, die
  `AiNetLinterRichtlinien.mdc` §1 vermeiden will.

### P1-8 · Niemand ruft den Server auf

**Problem.** Der `drift-loop` — der konkrete Agenten-Workflow, für den der
Server gebaut wird — exploriert Code per `rg`/`grep`. Es existiert kein Schritt,
der ihn auf die MCP-Tools umstellt. Ohne diesen Schritt bleibt der Server ein
fertiges Feature, das niemand benutzt, und P1-7 hat nichts zu messen.

**Umsetzungsempfehlung.**
1. Registrierung des Servers dokumentieren (gehört ohnehin in EPIC-08 /
   `Docs/integration.md`) — der eigentliche Schritt kommt danach.
2. In der Explorations-Anweisung des `drift-loop` (bzw. der entsprechenden
   Prompt-Datei unter `.agents/`) die Reihenfolge **explizit** vorgeben: erst
   `find_symbol`/`get_file_skeleton`, `rg` nur für das, was kein Symbol ist
   (Konfigwerte, Kommentare, Nicht-C#-Dateien). Ein Agent wählt sonst das
   Werkzeug, das er aus dem Training kennt — und das ist `rg`.
3. **Konditionierung nicht vergessen:** die Anweisung darf nur greifen, wenn der
   MCP-Server im Zielprojekt überhaupt registriert ist. Der `drift-loop` läuft
   auch in Nicht-.NET-Projekten.
4. Zuerst im eigenen Repo aktivieren (das ist bereits gelebte Praxis über das
   Step-Dogfooding), dann im .NET-Zielprojekt.

---

## 6. Bewusst gestrichen

Nicht mehr Teil des Scopes. Festgehalten in einer Zeile je Punkt, damit sie
nicht in der nächsten Runde erneut vorgeschlagen werden — nicht als offene Idee.

| Gestrichen | Grund |
| :--- | :--- |
| Thread-Safety / `SemaphoreSlim` | **Bereits erledigt.** `McpCodeGraphServer` hält ein `Lock`, `GetCurrentSolution()` gibt eine immutable `Solution` heraus — die Roslyn-Arbeit läuft danach lockfrei und korrekt parallel. Ein zweiter Synchronisationsmechanismus wäre Schaden. |
| `instructions` im Handshake, Tool-Descriptions, Miss-Hint | **Bereits geplant** als EPIC-05 im laufenden Task. |
| `get_call_tree` | Redundant zu P2-4 — dieselbe Frage, gelöst durch einen Parameter statt ein zweites Tool. |
| `.csproj`/`.sln`-Hash-Invalidierung | Aufgegangen in P1-1: das reale Problem sind neue/gelöschte Dateien, nicht geänderte Projektdateien. |
| Duplicate-Symbol-Drift-Warnung | „>1 Treffer bei gleichem Namensmuster" ist in echtem C# der Regelfall (Overloads, Interface+Impl, `partial`, Test-Doubles, generische Varianten). Überwiegend Rauschen — und trainiert den Agenten, Warnungen zu überlesen. Drift-Erkennung gehört als Linterregel mit scharfer Definition, nicht als Nebeneffekt einer Suchabfrage. |
| Dead-Code-Detection | Nicht nur nutzlos, sondern riskant: „0 Referenzen" gilt nur ohne Reflection, DI-Registrierung, Serialisierung und XAML-Bindings — genau der vorliegende Codebestand. Ein Agent liest so einen Befund als Löschauftrag. |
| PageRank / Symbol-Centrality | Ein Agent mit konkretem Task braucht keine repo-weite Wichtigkeits-Rangliste; `HotspotMapBuilder` deckt den realen Bedarf. |
| `agent_hint` in `rules.json` | Erledigt durch P0-3: Regel-ID genügt, den Regeltext hat der Agent über `AiNetLinter.mdc`. |
| `mcp_config`-Rauschfilterung | Nutzer-Vorbehalt trägt: `rules.json` ist bereits zu groß, und Verstecken schafft verdeckte Tech-Debt. |
| No-New-Violations-Ratchet | Nutzer-Vorbehalt trägt: Duldungs-Pattern schleppt Tech-Debt über Jahre mit. |
| `get_active_rules` | Redundant zu `.agents/rules/AiNetLinter.mdc` (`--sync-agent-rules-only`). |
| Editier-Tools | Non-Goal des Haupt-Tasks, unverändert gültig. |

---

## 7. Prio-Übersicht

| Prio | ID | Thema |
| :--- | :--- | :--- |
| **P0** | P0-1 | `max_results` + Trunkierung für alle Listen-Tools |
| **P0** | P0-2 | Ausgabeformat verbindlich auf Text festlegen |
| **P0** | P0-3 | Regel-ID in `get_violations` (löst die rules.json-Frage komplett) |
| **P1** | P1-1 | Neu angelegte/gelöschte `.cs`-Dateien sichtbar machen |
| **P1** | P1-2 | `rules.json`-Auto-Discovery statt stiller Default-Regeln |
| **P1** | P1-3 | Kaltstart entkoppeln, „lädt noch"-Antwort statt Blockade |
| **P1** | P1-4 | Staleness-Sweep über Verzeichnis-`mtime` kurzschließen |
| **P1** | P1-5 | stdout strukturell freihalten + E2E-Frame-Test |
| **P1** | P1-6 | Generierte Last-Fixture als Skalierungsnachweis |
| **P1** | P1-7 | Call-Log (opt-in) — Datenbasis für alle künftigen Prioritäten |
| **P1** | P1-8 | `drift-loop` tatsächlich auf die MCP-Tools umstellen |
| **P2** | P2-1 | Antwortgrößen-Kriterium in die DoD |
| **P2** | P2-2 | Staleness-Tests für Anlegen/Löschen |
| **P2** | P2-3 | `get_symbol_body` + stabile Symbol-IDs (mit Skeleton als System) |
| **P2** | P2-4 | Blast-Radius als `depth`-Parameter |
| **P2** | P2-5 | DI-Registrierung als Zusatzzeile in `get_type_hierarchy` |

**Empfohlene Reihenfolge für den Folge-Task:** P0 komplett → P1-1/P1-4
gemeinsam (ein Mechanismus) → P1-2, P1-3, P1-5 → P1-6/P1-7 (schaffen die
Messbarkeit für alles Weitere) → P1-8 → P2 nach Datenlage aus P1-7.

---

## 8. Referenzen

### Microsoft .NET / Roslyn Compiler API

1. **`DocumentationCommentId.CreateDeclarationId`** — erzeugt den eindeutigen
   XML-Signatur-String für ein `ISymbol` (Basis für P2-3):
   https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.documentationcommentid.createdeclarationid
2. **`DocumentationCommentId.GetFirstSymbolForDeclarationId`** — löst eine
   Symbol-ID deterministisch gegen eine `Compilation` auf:
   https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.documentationcommentid.getfirstsymbolfordeclarationid
3. **`Solution.AddDocument` / `Solution.RemoveDocument`** — In-Memory-Aufnahme
   und -Entfernung von Dokumenten ohne Workspace-Reload (Basis für P1-1):
   https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.solution
4. **`SymbolFinder.FindImplementationsAsync`** — Interface → konkrete
   Implementierungen (bereits in `get_type_hierarchy` genutzt, Kontext für P2-5):
   https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.findsymbols.symbolfinder

### MCP-Protokoll & SDK

5. **MCP-Spezifikation, Lifecycle/Initialization** — maßgeblich für P1-3
   (Verhalten zwischen Prozessstart und `initialize`-Antwort) und für das
   `instructions`-Feld aus EPIC-05: https://modelcontextprotocol.io/specification
6. **MCP C# SDK (`ModelContextProtocol`)** — das im Projekt eingesetzte Paket;
   Referenz für Server-Optionen, Transport und Tool-Registrierung:
   https://github.com/modelcontextprotocol/csharp-sdk

### KI-Forschung & Agenten-Architektur

7. **Anthropic, „Building Effective Agents" (Dez. 2024)** — Orchestrator-Workers,
   Context-Window-Spill, Prompt-Pruning in Tool-Calling-Loops. Relevant für die
   Tool-Anzahl-Abwägung in P2-4:
   https://www.anthropic.com/research/building-effective-agents
8. **„RepoGraph: Repository-Level Code Graph for AI Software Engineering"
   (ICLR 2025)** — deterministisches Context-Engineering über Code Property
   Graphs schlägt reines Modell-Scaling auf SWE-bench:
   https://arxiv.org/abs/2410.02678
9. **Sourcegraph SCIP** — Standard für symbolbasierte Navigation und eindeutige
   Symbol-Bezeichner; konzeptioneller Nachbar zu P2-3:
   https://github.com/sourcegraph/scip

### Etablierte MCP-Codegraph-Implementierungen

10. **Serena** — Vorbild für Symbol-Level Body Reading (P2-3).
11. **kirograph** — Vorbild für Blast-Radius-Traversal (P2-4).
12. **coa-codenav-mcp** — Roslyn-basierter MCP-Server, Call Hierarchy und
    C#-Inheritance-Navigation.

> ⚠ **Hinweis zu 10–12:** Die in der Vorfassung notierten Repository-URLs sind
> nicht verifiziert; mindestens eine sieht falsch aus (Serena liegt nach meiner
> Kenntnis nicht unter `oriserena`). Die Projektnamen stimmen — vor Verwendung
> als Referenz die tatsächlichen Repository-Pfade einmal nachschlagen, statt die
> alten URLs weiterzutragen.

### Quelle der Token-Zahlen aus dem Haupt-Konzept

13. Anthony West, „Code Intelligence & Code-Graph Indexing for AI Agents" (2026) —
    Ursprung der „~60 % Kosten / ~69 % Tokens"-Angabe, die den Haupt-Task
    begründet. Genau die Zahl, die P1-7 durch eigene Daten ersetzen soll:
    https://anthonywest.co.uk/research/code-intelligence-indexing-2026-openai
