# AiNetLinter MCP — 360°-Usage-Test aus Agentensicht

- **Datum:** 2026-09-10
- **Server:** AiNetLinter MCP 1.0.192, Mode `daemon`, Profil `cursor`, PID 18988
- **Ziel:** [..entfernt..]
- **Snapshot:** `3710EAFD…C25F7B` (`source-files`, `fresh: true`)
- **Regeln:** `ainetlinter-rules.json` neben der slnx, Status geladen
- **Code geändert:** nein
- **Autor:** Cursor-Agent (Grok 4.6), als würde er eine echte Programmieraufgabe vorbereiten

## Kurzurteil

**Der Server funktioniert.** Er ist erreichbar, die Solution ist geladen, Schemas stimmen mit dem Workflow überein, Fehler sind feldgenau (`INVALID_ARGUMENT` + `fieldPath`), Navigation/Completeness sind ehrlich.

**Für das Programmieren ist er kein Ersatz für Grep/Read/Glob, sondern ein semantisches Zusatzwerkzeug.** Der Gewinn sitzt dort, wo interne Tools raten müssen: Typen, Overrides, Call-Trees, Tests, Lint, Metriken, Assembly-Herkunft. Der Verlust sitzt bei Textsuche, Dateilisten und überall, wo Markdown-Antworten undurchsichtige Symbol-IDs und Test-Treffer den Kontext aufblähen.

**Ohne den Server hätte ich dieselbe Fachfrage schneller und tokenärmer beantwortet, aber mit mehr Unsicherheit bei Polymorphie und „wer ruft wirklich wen“.** Mit dem Server weiß ich die Dispatch-Kette *sicherer*. Ob das den Mehrpreis rechtfertigt, hängt stark vom Tool und vom Scope ab.

**Note 1–10 (Agentennutzen in diesem Repo):** **6,5 / 10.** Nicht kaputt. Nicht magisch. Bedingt lohnend, wenn man die richtigen Tools mit engen Limits nimmt und die falschen meidet.

---

## Szenario (erfunden, aber realistisch)

„Ich will verstehen, wie ein Site-Komponenten-Command von der UI zum Handler kommt, bevor ich einen neuen Befehl ergänze.“

Das ist typische Agentenarbeit in diesem Repo: unbekannter Einstieg, Vererbung, virtuelle Methode, viele Tests, Blazor-Bridge.

**Soll-Pfad laut Workflow:** `find_symbol` → `get_feature_context` → `get_symbol_body` / `get_call_tree` → `get_test_context` / `get_impact`.

**Was ohne MCP passiert wäre:** `Grep ExecuteCommandAsync`, `Grep class SiteComponentHandler`, `Read` der Trefferdateien, ggf. `Glob *Handler*.cs`. Das hätte in 3–5 Calls den gleichen groben Pfad ergeben.

---

## Was der Server in diesem Szenario tatsächlich geliefert hat

Fachlich korrekte Kette, die ich danach belegen kann:

1. Es gibt **kein** `ISiteComponentHandler`. Der Vertrag ist die Klasse `SiteComponentHandler` mit virtuellem Default-`ExecuteCommandAsync` (Fail: „nicht unterstützt“).
2. Scheduler überschreibt in `SchedulerSiteComponentHandler.ExecuteCommandAsync` und lässt `move-item` / `add-item` / `update-item` / `remove-item` als Ok durch, sonst `base`.
3. UI-Einstieg ist `SiteComponentCommandDispatcher.DispatchCommandAsync`: Access-Gate → Handler auflösen → `ExecuteCommandAsync` inkl. Prompt/Form-Schleife → Finalize (Messages, Peer-Commands).
4. Konkrete Produktionsableitungen existieren (Domain-Scheduler, Admin, DataTable, Form, GenericScheduler, …) plus sehr viele Test-Stubs (**97 Implementierungen**).
5. Zugeordnete Tests für den Dispatcher sind konkret und mit `dotnet test --filter` kopierbar.

Das ist echter semantischer Wert. Grep allein hätte Punkt 1–3 auch gefunden, Punkt 4 (vollständige Ableitungsliste inkl. nested Test-Stubs) und Punkt 5 (statische Testzuordnung + Filter) nicht zuverlässig.

---

## Setup und Betriebsqualität

| Check | Ergebnis |
|---|---|
| Namespace `user-AiNetLinter` | `ready` |
| Global Health ohne Target | läuft, 0 Projekte bis zum ersten Target-Call |
| Health mit slnx + Diagnostics | Solution `Loaded`, Regeln gefunden, Snapshot fresh |
| Resource `ainetlinter://overview` | dünn, kaum mehr als Health |
| Resource `ainetlinter://rules` | nützlich: effektive Limits und aktive Regeln |
| Relativer `targetPath` | klar abgelehnt |
| Verzeichnis als `targetPath` | klar abgelehnt |
| `maxResults=0` | klar abgelehnt |
| Unbekanntes Argument | klar abgelehnt (`maxBodyLines` an `get_class_structure`) |
| Ungültiges Enum (`minSeverity=critical`) | klar abgelehnt mit erlaubten Werten |
| `reload_config` | ok, „18 aktivierte Regeln, unverändert“ |
| Daemon | Warmstart spürbar; Folgelatenz niedrig |

**Gegen interne Tools:** Health/Rules haben kein Gegenstück. Das ist MCP-only und in Ordnung.

**Hinweis:** `reload_config` spricht von 18 aktivierten Regeln, die Rules-Resource listet deutlich mehr (inkl. Metrik-Limits). Die Zahl 18 wirkt wie ein Teilzähler (boolesche Global-Flags), nicht wie das volle wirksame Regelset. Für den Agenten leicht irreführend, nicht kaputt.

---

## Tool-für-Tool (ehrlich)

Legende Nutzen: **hoch** / **mittel** / **niedrig** / **schädlich** (Kontext verschwendet oder irreführt).
Vergleich: was ich stattdessen intern getan hätte.

### Navigation / Discovery

| Tool | Status | Nutzen | Ohne MCP | Kommentar |
|---|---|---|---|---|
| `get_index_scope` | ok | mittel | Glob + Erfahrung | 2288 `.cs` im Symbolgraph vs. `get_file_tree` Summary 897 `.cs`. Die Diskrepanz ist erklärbar (Roslyn-Dokumente vs. Dateiscan mit Truncation/`Release`), aber ein Agent könnte die Zahlen falsch addieren. Routing-Hinweise (`.razor` → `search_pattern`) sind gut. |
| `get_file_tree` summary | ok, truncated | mittel | Glob | Kompakte Top-Level-Landkarte. `Release/` mit 610 MB ist Rauschen. |
| `get_file_tree` files + Filter | ok | **hoch** | Glob | `*SiteComponentCommandDispatcher*` → 5 Dateien, klein, korrekt. Hier ist MCP mindestens so gut wie Glob. |
| `get_namespace_tree` | ok | mittel | Ordner lesen | Semantische NS-Sicht. `includeTypes=true` hat trotzdem nur Typ**zahlen** geliefert, keine Typnamen. Drilldown nötig. |
| `find_symbol` | ok | **hoch**, mit Fallen | Grep | Semantisch richtig, Batch (`namePatterns`) spart Roundtrips. Ranking bevorzugt oft **Tests**. Glob `*Handler` + `kind=interface` lieferte nur 6 Interfaces und `completeness: complete` — das ist leicht als „es gibt nur diese Handler-Interfaces“ zu lesen, obwohl der eigentliche Vertrag eine **Klasse** ist. |
| `search_pattern` | ok | mittel | **Grep ist meist besser** | Gleicher Textvertrag wie Grep. Default-Scope `all` trifft zuerst Docs. 281 Treffer inkl. `Release/`. Binary-Skip-Hinweis ist fair. `includePatterns` funktioniert. `HandlerResponse.Fail` → 0 Treffer, korrekt, weil der Quelltext `HandlerResponse<object?>.Fail` heißt. Grep mit demselben Literal ebenfalls 0. Semantische Suche ist das **nicht**. |

### Semantik (der eigentliche Job)

| Tool | Status | Nutzen | Ohne MCP | Kommentar |
|---|---|---|---|---|
| `get_symbol_body` | ok | **hoch** | Read | Bester Alltags-Call. Exakter Rumpf, Batch möglich, Hinweis „kein zusätzliches Read“. Token-effizienter als ganze Datei, *wenn* man das Symbol schon hat. |
| `get_class_structure` | ok | **hoch** | Read + Skimmen | Tabelle Kind/Name/Zeilen/Signatur. Für den Dispatcher die sieben Dispatch-Methoden sofort sichtbar. |
| `get_file_skeleton` | ok | **niedrig bis schädlich** | Read | Inhaltlich gut (27 Member, Uses-Hinweise). In der **Markdown-Antwort** hängt an jedem Member eine ca. 200-Zeichen-Opaque-ID. Für eine 538-Zeilen-Datei ist `Read` kleiner und nützlicher. Die Workflow-Regel „IDs nicht aus Markdown parsen“ hilft dem Agenten nicht, wenn die IDs trotzdem im Text landen. |
| `get_feature_context` | truncated | **niedrig** (in diesem Fall) | mehrere gezielte Calls | Beworben als One-Shot. Bei `SiteComponentHandler.ExecuteCommandAsync` (112 Call-Sites, 125 Testmethoden) kam nur ein **Budget-Summary** (12288 Bytes gesamt / 4096 pro Abschnitt). Keine Caller-Namen, keine Testnamen, 0 Metriken. Für Hot-Symbols unbrauchbar, bis man Limits klein macht — dann ist es kein One-Shot mehr. |
| `get_call_tree` incoming auf virtuelle Basismethode | ok, 114 KB | **schädlich** | Grep | Polymorphie + Tests + **dieselben Kinder unter jedem Knoten wiederholt**. Opaque-IDs in jeder Zeile. Das ist der teuerste Fehlgriff des Tests. |
| `get_call_tree` outgoing auf konkrete Methode | ok | **hoch** | Read + Grep + Raten | Das Killer-Feature. `DispatchCommandAsync` → Access, Resolve, `ExecuteCommandAsync`, Prompt/Form, Peer-Commands. Das hätte ich intern nur durch Lesen der Datei rekonstruiert, unsicherer bei indirekten Calls. |
| `get_call_tree` auf **Typ** statt Methode | ok, leer | niedrig | — | Nur der Typknoten, `completeness: complete`. Agent muss eine Methode wählen. Nicht falsch, aber leicht leer zu interpretieren. |
| `find_references` | ok | **hoch**, testlastig | Grep | Semantisch präziser als Grep (kein String-Zufall). Kein `scopeType=production`. Tests zuerst. Jede Zeile mit voller Opaque-ID. Grep `DispatchCommandAsync(` im Platform-Projekt war **kompakter** und zeigte Produktions-Callers sofort. MCP liefert dafür die Gewissheit „echter Aufruf dieses Symbols“. |
| `find_implementations` | ok | **hoch** | Grep `: SiteComponentHandler` | 97 Treffer, inkl. nested Test-Stubs. Das kann Grep nur näherungsweise. Truncation bei `maxResults` ist korrekt. |
| `get_type_hierarchy` | ok | **hoch** | Lesen + Grep | Basisklasse `object`, keine Interfaces, abgeleitete Klassen, heuristische DI-Registrierungen. Partial-Klassen erscheinen mehrfach (Datei pro Partial). DI-Liste ist testlastig (`AddSingleton` in bUnit-Setups), nicht das Composition-Root-Bild. Trotzdem nützlich. |
| `dependency_graph` | ok | **hoch** | using-Lesen | Echte Typkanten: `SiteComponentHandler` → `HandlerResponse`, `SiteComponentDataPayload`, `SiteHandlerContext`. Kompakt, genau das, was Grep nicht kann. |
| `resolve_type_origin` | ok | **hoch** | NuGet raten | `MudDialog` → `MudBlazor 9.9.0` mit vollem DLL-Pfad. `SiteComponentHandler` → Assembly-Name, aber nur Dateiname `[..entfernt..].dll` ohne Pfad. Asymmetrie. |
| `get_test_context` Hot-Symbol | truncated | niedrig | Grep Testdateien | Wieder 12-KB-Budget, keine Namen. |
| `get_test_context` konkreter Dispatcher | ok | **hoch** | Grep Testdateien | 4 Dateien, 9 Methoden, Gründe (Direct Member Match / Invocation), fertiger `dotnet test --filter`. Das rechtfertigt MCP allein schon vor einem Edit. |
| `metrics_lookup` | ok | **hoch** | Kopfzählen | LOC/CC/CogC/Parameter vs. Projektlimits. `DispatchHandlerWithUserInteractionsAsync`: 55/60 Zeilen, 6/6 Parameter — genau die Info vor einem Edit. Intern nicht vorhanden. |
| `metrics_tree` | ok | mittel | — | Gute Hotspot-Landkarte (Contracts Ø CC 3.0). Truncated by design. |
| `get_hotspots` | ok | **hoch** | Zeilen zählen | `SchedulerBindings.cs` 600/600, weitere Scheduler-Dateien ≥95 %. Einzigartig und agentenrelevant vor Dateierweiterung. |

### Lint / Audit

| Tool | Status | Nutzen | Ohne MCP | Kommentar |
|---|---|---|---|---|
| `get_violations` | ok | **hoch** nach Edits | — | Contracts error: 0. Dispatcher warning: 0. Scope über Dateiname funktioniert. Einzigartig. |
| `safeguard` | ok | mittel | — | Contracts 10/10 PASS, mit ehrlichem `scoreIsNotScope=true`. Für Merge-Gates gedacht, nicht für Exploration. |
| `find_dead_code` | empty | mittel | — | 0 Kandidaten in Contracts, Heuristik mit `deletionClaim=false`. Gut, dass nichts Gelöschtes behauptet wird. |
| `find_duplicates` | ok | **hoch** | manuell | Near-Clone Score 0,88: `QueryDataAsync` vs. `QuerySchedulerEventsWindowAsync`. Plausibel, als Kandidat gekennzeichnet. |
| `find_magic_values` | empty | **niedrig** | Grep Literale | 0 Treffer, aber eine lange Leerkategorie-Liste. Leere Audits sollten eine Zeile sein, nicht eine Seite Remediation-Text. `minOccurrences=2` erklärt fehlende Einzel-Fail-Strings. |
| `pattern_detect` | empty | mittel | — | Contracts clean für god-class/async-void/empty-catch/long-method. Nützlich im Audit, nicht im Alltags-Edit. |
| `get_impact` working tree | empty, irreführend | mittel | `git status` / `git diff` | Meldung: „Kein Git-Repository oder leerer Diff“. Das Repo **ist** ein Git-Repo; der Working Tree war leer. Zwei Fälle in einem Satz. Internes `git status` ist klarer. |
| `get_impact` `gitRef=HEAD` | ok, leer | niedrig | `git show` | „Keine betroffenen Aufrufstellen für HEAD“ — technisch leer, sprachlich unscharf. |

### Assembly / Fremdcode

| Tool | Status | Nutzen | Ohne MCP | Kommentar |
|---|---|---|---|---|
| `inspect_assembly` Contracts.dll | partial/truncated | mittel | Read Source | `typeName=SiteComponentHandler` zeigte zuerst `SchedulerSiteComponentHandler`. Dekompilat, Diagnosen (init-only). Für **dieses** Repo ist Source-MCP besser als die eigene DLL. Sinnvoll für geschlossene Third-Party-DLLs. |
| `search_assembly` MudBlazor `ShowAsync` | ok | **hoch** | lokaler MudBlazor-Clone + rg | 29 Treffer, Overloads von `DialogService.ShowAsync` sichtbar. Das ist genau der MudBlazor-Lookup aus den Projektrichtlinien — ohne den GitHub-Clone zu durchsuchen. |
| `find_assembly_extensions` `AddMudServices` | ok | mittel | rg im Clone | Zwei Overloads, `not_decidable` ohne Consumer. Ehrliche Diagnosen (fehlende ASP.NET-Refs, Decompiler-Syntaxfehler). |

### Beworbene, aber nicht vorhandene Tools

Die Workflow-Rule nennt `view_file` als bevorzugtes Tool für kleine Edits. **Im Namespace `user-AiNetLinter` existiert `view_file` nicht** (Pattern-Suche leer). Der Agent fällt korrekt auf internes `Read` zurück. Die Rule ist hier voraus.

---

## Tokenökonomie (grobe, aber belegte Thesen)

Nicht token-gezählt im Tokenizer, sondern an beobachteter Nutzlast:

| Situation | MCP | Interne Tools | Wer gewinnt? |
|---|---|---|---|
| Datei finden (`*Dispatcher*`) | `get_file_tree` files, ca. 5 Zeilen | Glob, ähnlich | unentschieden |
| Symbol finden, Name ungefähr bekannt | `find_symbol`, semantisch, oft testfirst | Grep, oft kompakter | **Grep**, außer bei Overloads/Kind-Filter |
| Methodenkörper | `get_symbol_body`, ca. 40 Zeilen | `Read` ganzer Datei (Dispatcher 538 Zeilen) | **MCP**, klar |
| Aufrufer einer konkreten Methode | `find_references` mit IDs, Tests zuerst | Grep im Produktionsordner | **Grep für Überblick**, MCP für Gewissheit |
| Incoming-Tree auf virtuelle Basis | 114 KB Wiederholung | Grep | **Grep, klar** |
| Outgoing-Tree konkrete Methode | mittelgroß, aber gehaltvoll | Datei lesen | **MCP** (Sicherheit) |
| Skeleton einer großen Klasse | IDs sprengen den Gewinn | `Read` | **Read** |
| Tests vor Edit | `get_test_context` + Filter | Testdateien greppen | **MCP** |
| Lint nach Edit | `get_violations` | nicht vorhanden | **MCP** |
| Razor/JS/JSON | `search_pattern` | Grep | **Grep** (schneller, weniger Schema-Tax) |
| Fremd-DLL API | `search_assembly` | Clone + rg | **MCP**, wenn kein Clone geladen ist |

**Schema-Tax:** Vor fast jedem neuen Tool war `GetDynamicTools` nötig. Die Beschreibungen sind ausführlich und korrekt, aber teuer. Das ist teils Client-Vertrag (Schema vor Invoke), teils Server-Verbose.

**Opaque-IDs im Markdown:** Jede Referenzzeile trägt `source:<base64-slnx>:<fingerprint>:M:<DocCommentId>`. Das sind oft mehr als 300 Zeichen pro Treffer, die der Agent laut Rule nicht als ID-Quelle nutzen soll, weil StructuredContent die IDs schon hat. Im sichtbaren Tool-Text sind sie trotzdem da. Das ist der größte systematische Token-Leak.

**Wire-Budget 12 KB bei Composite-Tools:** `get_feature_context` / großes `get_test_context` kollabieren genau dann, wenn das Symbol interessant ist (viele Caller/Tests). Dann zahlt man einen Roundtrip für eine Meta-Antwort ohne Nutzinhalt.

---

## Richtigkeit vs. Vollständigkeit

Beobachtete Wahrheiten:

- Nicht-existierendes `ISiteComponentHandler` wurde **nicht halluziniert**, sondern mit ähnlichen Symbolen beantwortet.
- `ProcessCommand` → 0 Treffer plus ähnliche Namen. Gut.
- Text-0-Treffer bei `executeCommand` in JS stimmt mit Grep überein.
- Completeness ist meist ehrlich (`truncated` / `empty` / `complete`).
- Dead-Code und Duplicates kennzeichnen sich als Kandidaten, nicht als Löschauftrag.

Beobachtete Schwächen:

- `completeness: complete` bei `*Handler` Interfaces kann „es gibt nur 6“ bedeuten, obwohl der Agent den falschen `kind` gewählt hat.
- Incoming-Call-Tree auf virtuelle Methoden mischt Overrides, `base`-Calls und Tests zu einem dichten, redundanten Baum.
- `get_impact` formuliert fehlendes Diff als mögliches „kein Git-Repo“.
- `get_index_scope` und `get_file_tree` zählen `.cs` sehr unterschiedlich.
- `find_symbol` / `find_references` haben keinen Produktionsfilter; in diesem Repo (viele Tests) ist das spürbar.

---

## Würde ich den Server beim nächsten Programmierauftrag nutzen?

**Ja, gezielt. Nicht als Default für jede Suche.**

### Immer zuerst MCP

- Unbekannter C#-Typ / Methode: `find_symbol` (danach sofort `kind` und engeres Pattern)
- Body: `get_symbol_body`
- „Wer erbt / implementiert“: `find_implementations`, `get_type_hierarchy`
- „Wen ruft diese konkrete Methode“: `get_call_tree direction=outgoing`
- Tests vor Edit, wenn das Symbol nicht hyper-hot ist: `get_test_context`
- Nach Edit: `get_violations`, `metrics_lookup`, `get_hotspots` wenn Datei wächst
- Fremd-DLL ohne geladenen Source-Clone: `resolve_type_origin` → `search_assembly` / `inspect_assembly`

### Intern lassen (Grep / Read / Glob)

- Razor, JS, JSON, SQL, Markdown, csproj
- Dateiname bekannt, nur 20 Zeilen ändern → `Read`
- Produktions-Callers grob sammeln → `Grep` mit Pfad + Glob
- Arbeitsbaum → `git status` / `git diff`, nicht `get_impact` ohne Änderungen

### Meiden oder stark begrenzen

- `get_call_tree incoming` auf virtuelle/stark überladene Basismethoden (`topN=5`, `depth=1` oder gar nicht)
- `get_feature_context` auf Hot-Symbols (lieber die Einzeltools)
- `get_file_skeleton` ohne Bedarf an Member-IDs
- `search_pattern` ohne `includePatterns` / `scopeType` in diesem Repo
- `find_magic_values` ohne engeren Filter, wenn man nur „gibt es Treffer?“ will

---

## Was intern nicht ersetzen kann

Das sind die Stellen, an denen der MCP-Server den Agenten nachweislich besser macht als die eingebauten Tools:

1. **Roslyn-Identität** statt String. `ExecuteCommandAsync` auf der Basis vs. Override vs. Testmethode gleichen Namens.
2. **Outgoing Call-Tree** einer konkreten Methode.
3. **Implementierungsliste** einer Basisklasse inklusive nested Types.
4. **Statische Testzuordnung + kopierbarer Filter**.
5. **Lint/Metriken gegen `ainetlinter-rules.json`**, live, ohne CLI-Subprozess.
6. **Typ → DLL-Pfad** (MudBlazor) und anschließende Dekompilat-Suche.

Ohne MCP hätte ich (1) und (2) durch Lesen vermutet, (3) unvollständig per Grep, (4) durch Dateinamen geraten, (5) und (6) weggelassen oder den MudBlazor-Clone per `rg` durchsucht.

---

## Was intern oft effizienter ist

1. **Grep** für Text, auch in `.cs`, wenn das Literal stabil ist und der Ordner bekannt.
2. **Read** für Dateien, deren Pfad schon feststeht — besonders unter ca. 150 Zeilen oder wenn man sowieso den Kontext um die Methode braucht.
3. **Glob** für Dateinamen; `get_file_tree` ist gleichwertig, nicht überlegen.
4. **Git-Bordmittel** für Diff/Impact bei leerem Working Tree.

Der Workflow sagt „MCP-first, pragmatisch“. Pragmatisch heißt hier: MCP für Semantik, intern für Text und Bytes. Ein hartes „immer MCP zuerst“ kostet in diesem Repo Roundtrips und Tokens, ohne bessere Antworten bei Razor/JS/Docs.

---

## Agentenergonomie (Cursor-spezifisch)

- `CallDynamicTool` verlangt `mcpDetails.description`. Extra Reibung, nicht AiNetLinter-Schuld.
- Tool-Schemas sind lang; Namespace-List war 64,9 KB. Ein Agent, der „alle Tools einmal anfassen“ will, verbrennt Budget in der Discovery, nicht in der Analyse.
- StructuredContent wird in der Rule als ID-Quelle empfohlen. Im Chat sehe ich primär Markdown. Wenn StructuredContent beim Modell ankommt, sind die Markdown-IDs trotzdem Duplikatkosten.
- Handoff-`next: request_detail` ist oft richtig, aber bei leeren Audits (`find_magic_values`, `pattern_detect`) klingt es nach „du hast nicht genug gesucht“, obwohl der Scope fertig ist.

---

## Funktioniert der Server überhaupt?

Ja. Konkrete Nachweise:

- Solution-Key wird beim ersten Target-Call residiert.
- Snapshot-Fingerprint bleibt über die Session stabil und `fresh`.
- Source-Route (`.slnx`) und Assembly-Route (`.dll`) werden an der Endung unterschieden; Assembly-Health/Tools nutzen andere Snapshots.
- Recoverable Errors sind deterministisch und nennen das JSON-Feld.
- Fachliche Ergebnisse am Szenario waren nachprüfbar (Bodies stimmen mit dem, was Grep/Read später bestätigt).

Nicht getestet (bewusst): `mcp_auth` (unnötig), `report_observability_feedback`, `get_assembly_context`, Live-Edit + `get_impact change-context` auf einem echten Diff (Working Tree war leer; kein Code geändert).

---

## Verbesserungsvorschläge (nur Bewertung, kein Patch)

Priorität aus Agentensicht:

1. **Opaque-IDs nicht in Markdown-Trefferlisten.** Kurzform `Datei:Zeile — Symbolname`, IDs nur in StructuredContent.
2. **`scopeType=production|tests`** für `find_references`, `find_symbol`, `get_call_tree`, `find_implementations`.
3. **Incoming-Tree:** keine Kopie derselben Kindliste unter jedem polymorphen Caller; Tests default aus oder nach hinten.
4. **Composite-Budget:** bei Truncation die *ersten* Caller-/Testnamen liefern, nicht nur Zähler. Oder Default-Budget anheben.
5. **`get_impact`:** „Working tree clean“ vs. „kein Git-Repo“ trennen.
6. **Leere Audits kürzen** (`find_magic_values`, `pattern_detect`).
7. **`find_symbol`-Ranking:** Produktionsdeklaration vor Testdeklaration / vor Testmethoden gleichen Substrings.
8. Workflow-Rule: `view_file` streichen oder das Tool wirklich exponieren.
9. `get_file_skeleton`: IDs optional / `detailLevel=compact`.
10. `resolve_type_origin` für In-Solution-Typen: voller DLL- oder Source-Pfad, analog zu NuGet.

---

## Fazit in einem Satz

Der AiNetLinter-MCP ist in diesem Workspace **betriebsbereit und semantisch nützlich**, aber **nicht tokenärmer als interne Tools als Default**; er lohnt sich, sobald die Frage „welches Symbol, welche Aufrufer, welche Tests, welche Regel“ ist — und er schadet, sobald man ihn wie ein besseres Grep oder als unbegrenzten Call-Tree auf virtuelle Basismethoden benutzt.
