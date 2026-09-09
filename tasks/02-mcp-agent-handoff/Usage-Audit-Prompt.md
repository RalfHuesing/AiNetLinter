# Usage-Audit-Prompt: MCP-Agenten-Handoff und bounded Navigation

Du arbeitest im Repository `C:\Daten\Entwicklung\Ralf\AiNetLinter`.

## Auftrag

Führe einen echten Usage-Audit des aktuell aktiven AiNetLinter-MCP-Servers
durch — aus der Sicht eines Agenten, der die veröffentlichten MCP-Tools
tatsächlich bedient — und behebe anschließend proaktiv alle dabei gefundenen
in-scope Befunde.

Das Ziel ist nicht nur ein Auditbericht. Du sollst die Ursachen im Produkt,
im Contract, in Runtime-Validierung, StructuredContent, Textprojektion,
Registrierung, Tests und Dokumentation beheben und den reparierten Zustand
erneut aus Agentensicht verifizieren.

Der maßgebliche Zielvertrag ist:

- `tasks/02-mcp-agent-handoff/Konzept.md`
- `tasks/ainetlinter-mcp-usage-audit/shared/MCP-Verbesserungsrahmen.md`
- `tasks/ainetlinter-mcp-usage-audit/shared/Befundmatrix.md`
- die einzelnen Befunde unter
  `tasks/ainetlinter-mcp-usage-audit/findings/`
- die aktuellen Regeln unter `.agents/rules/`

Behandle das Konzept als umzusetzenden Hard-Cut-Vertrag. Kompatibilitäts-
oder Übergangslösungen sind nicht zulässig.

## Arbeitsregeln

1. Lies vor jeder Änderung `AGENTS.md`,
   `.agents/rules/AiNetLinterRichtlinien.mdc`,
   `.agents/rules/AiNetLinter-McpWorkflow.mdc`, das Konzept und den gemeinsamen
   Rahmen vollständig.
2. Erfasse vor Änderungen `git status --short` und `git log -1 --oneline` als
   Baseline. Bewahre fremde oder parallele Änderungen. Stagiere und committe
   niemals pauschal mit `git add .`, `git add -A` oder `git commit -a`.
3. Verwende den aktiven AiNetLinter-MCP-Server für die semantische Prüfung.
   Nutze die aktuell mit `tools/list` veröffentlichten Schemas als
   Vertragsquelle; rate keine Parameter oder Aliasfelder.
4. Jeder zielgebundene MCP-Aufruf verwendet genau einen absoluten,
   existierenden `targetPath` auf die konkrete `.sln`/`.slnx`-Datei oder —
   sofern für einen ausdrücklich in-scope Assembly-Fall erforderlich — auf die
   konkrete `.dll`/`.exe`-Datei. Der Server darf kein Target erraten.
5. Nutze für C#-Semantik zuerst die AiNetLinter-MCP-Tools. `rg` und gezieltes
   Lesen bleiben für Nicht-C#-Text, exakte Editstellen, Konfiguration und
   Diff-Prüfung erlaubt.
6. Ändere Dateien mit `apply_patch`. Keine Ad-hoc-MCP-Testskripte oder privaten
   Testartefakte committen. MCP- und Dogfood-Verifikation läuft über die
   vorhandene C#-Testinfrastruktur.
7. Behebe Ursachen zentral. Ein nachträgliches Markdown-Patching oder eine
   Formatter-only-Lösung reicht nicht, wenn Text und StructuredContent aus
   unterschiedlichen Wahrheiten entstehen.
8. Ändere keine Lintheuristik, keine Regel und keine Assembly-
   Decompiler-/Lease-Qualität, sofern das nicht unmittelbar zur Erfüllung des
   Task-02-Vertrags nötig ist. Task-04-Fachqualität bleibt abgegrenzt.
9. Stoppe nicht nach dem ersten Finding. Ein Finding gilt erst als erledigt,
   wenn die Produktursache behoben, der relevante Test ergänzt/angepasst, der
   Raw-Wire- oder Dogfood-Pfad grün und die Dokumentation konsistent ist.

## Phase 1: Baseline und aktive MCP-Oberfläche

1. Bestimme die konkrete Solution-Datei aus der sichtbaren Workspace-
   Dateistruktur. Verwende danach in allen zielgebundenen Calls ihren
   absoluten Pfad.
2. Prüfe den aktiven Serverstatus über `get_server_health` und die aktuellen
   MCP-Registrierungen/Schemas über die vorhandene Discovery-Möglichkeit.
   Verwende nur tatsächlich veröffentlichte Tools und Felder.
3. Prüfe, ob die Ziel-Solution geladen werden kann und ob der Serverstatus,
   `origin`, Snapshot und Capabilities zur Antwort passen.
4. Halte die Baseline als kurze Tabelle fest: Tool/Call, tatsächliches
   Agentenverhalten, erwartetes Verhalten aus dem Konzept, Befund, vermutete
   Ursache und geplanter Test.

Wenn die MCP-Oberfläche nicht erreichbar ist, untersuche zuerst die
Registrierung, den Serverstart und die vorhandene Testinfrastruktur. Melde
nicht vorschnell einen Produktfehler, wenn nur der Testaufbau fehlerhaft ist.

## Phase 2: Verbindlicher Agenten-Usage-Audit

Führe die folgenden Ketten wirklich über den MCP-Server aus und sichere die
komplette Raw-Wire-Antwort einschließlich Text und StructuredContent. IDs für
Folgecalls dürfen ausschließlich aus StructuredContent kopiert werden; niemals
aus Markdown, sichtbaren Zeilen, FQNs oder Pfadpositionen.

### A. Discovery, Scope und Routing

- `get_file_tree` auf Root-Ebene zunächst nur als kompakte Summary oder Tree-
  Ansicht mit kleiner Tiefe; prüfe Root-/Elternaggregate, `childDirectoryCount`,
  Datei- und Verzeichnisfilter sowie Truncation.
- `get_index_scope` für die Solution; prüfe Indexabdeckung, Counts und den
  maschinenlesbaren Routinghinweis für C# gegenüber Razor/JavaScript.
- `search_pattern` für einen C#-Begriff, einen Razor-/HTML-Begriff und einen
  JavaScript-/Textbegriff; prüfe Plain-First/Regex-Autodetect, `scopeType`,
  Glob-/Dateifilter, Produktionsranking, Counts und bounded Antwortgröße.
- Beweise die Kette `file_tree -> index_scope -> search_pattern` bei einem
  Nicht-C#-Fallback und bei null C#-Treffern.
- Prüfe Root Discovery unter dem vereinbarten Wire-Budget und mit höchstens
  20 Primärzeilen.

### B. Source-Handoff und Folgecalls

Führe mindestens diese Raw-Wire-Ketten aus:

1. `get_file_tree -> get_index_scope -> find_symbol -> get_symbol_body`
2. `find_symbol -> find_references`
3. `find_symbol -> get_call_tree`
4. `find_symbol -> get_class_structure` oder `get_file_skeleton`
5. `find_symbol -> find_implementations` beziehungsweise
   `get_type_hierarchy`

Prüfe für jeden Treffer und jeden Call-Tree-/Referenz-/Strukturknoten:

- `handoff=true|false`
- kanonische, kopierbare ID
- Targetbindung und Snapshotbindung
- Symbolart
- ausdrücklich erlaubte Folge-Tools
- belastbarer `totalCount`/`returnedCount`
- echte `completeness`
- genau ein sinnvoller nächster Schritt bei Kürzung

Prüfe zusätzlich gezielt:

- unbekanntes Symbol
- ambiges Symbol
- target-fremde ID
- veraltete Source-ID nach Änderung eines geladenen Sourcefiles
- capability-fremde ID
- ungültiger Parametername/Legacy-Alias
- `loading`, `empty`, `partial`, `truncated`, `unsupported` und
  `not_configured`
- dass kein begrenztes Ergebnis als `ok/complete` mit einem
  `maxResults + 1`-Schein-Count erscheint
- dass Text und StructuredContent dieselben IDs, Counts, Statuswerte und
  Folgeschritte ausdrücken

### C. Snapshot, ID und Continuation

- Ändere in einem isolierten Test-/Fixture-Szenario eine geladene Source-Datei
  und prüfe, dass der Snapshotwechsel `stale_snapshot` ergibt, nicht
  `symbol_not_found`.
- Prüfe, dass eine andere Solution beziehungsweise ein anderes Target
  `target_mismatch` ergibt.
- Prüfe, dass Continuations Target, Snapshot, Query, Filter und Sortierung
  binden und nach Ablauf nicht still neu interpretiert werden.
- Prüfe, dass Source-IDs auf der kanonischen DocComment-ID beruhen.
- Prüfe, dass Assembly-IDs — soweit Task-02-Handoff betroffen — Pfad, Hash und
  DocComment-ID binden, aber niemals eine öffentliche Cache-Generation
  enthalten.

### D. Status, Capability und regeloses Target

- Prüfe ein regeloses Source-Fixture: Navigation bleibt möglich, Lint meldet
  ausschließlich `not_configured`.
- Prüfe, dass `usedDefaultConfig`, automatische Regeldateisuche,
  Default-/Fallback-Konfiguration, alte Aliasparameter und Dual-Read-/
  Dual-Write-Reste weder im aktiven Contract noch in Runtime, Tests oder
  Dokumentation verbleiben.
- Prüfe Assembly- und Nicht-C#-Capabilities auf ehrliche Zustände:
  `unsupported`, `partial`, `not_decidable` und Textsuche-Fallback dürfen nicht
  wie eine leere, vollständige C#-Analyse erscheinen.

### E. Contract-Konsistenz

Für jede betroffene Registrierung müssen Schema-Snapshot, Runtime-Validator,
Toolbeschreibung, StructuredContent und Text dieselben Aussagen treffen:

- Required-Felder
- Enums
- kanonische Parameternamen
- Target- und Snapshotbindung
- Capability
- Status und Completeness
- nächster Schritt

## Phase 3: Befunde bewerten und sofort beheben

Ordne jeden reproduzierten Befund einer Root Cause zu. Priorisiere in dieser
Reihenfolge:

1. falsche oder nicht kopierbare Handoff-IDs,
2. Target-/Snapshot-Verwechslung und falsche Fehlercodes,
3. widersprüchliche Text-/StructuredContent-Projektion,
4. falsche Completeness-, Count- und Truncation-Aussagen,
5. widersprüchlicher Contract zwischen Schema, Runtime, Registrierung und
   Dokumentation,
6. Discovery-, Routing-, Ranking- und Budgetprobleme.

Behebe anschließend alle in-scope Findings aus den Auditpaketen 01 bis 03 und
den Task-02-Kombinationen. Das umfasst insbesondere `get_file_tree`,
`get_index_scope`, `search_pattern`, `find_symbol`, `get_file_skeleton`,
`get_symbol_body`, `find_references`, `get_call_tree`,
`find_implementations`, `get_type_hierarchy`, `dependency_graph`,
`get_namespace_tree`, `get_test_context`, `get_violations`,
`get_feature_context`, `get_impact`, `metrics_lookup`, Health/Resources und
die Batch-/Cross-Target-Handoffpfade, soweit sie den Task-02-Vertrag berühren.

Dabei gilt zwingend:

- Gemeinsame Navigation entsteht vor Text- und StructuredContent-Projektion.
- `operationStatus=ok` sagt nichts über Trefferzahl aus.
- `empty` gibt es nur bei vollständig geprüftem, benanntem Scope mit
  `totalCount=0`.
- `complete`, `partial` und `truncated` gelten für das konkrete Ergebnisfeld
  beziehungsweise den Composite-Abschnitt.
- Bei Truncation sind `totalCount`, `returnedCount`, `truncatedBy` und genau
  ein passender nächster Schritt Pflicht.
- `continue` ist nur bei stabiler, deterministischer Sortierung erlaubt.
- Defaults liefern maximal 20 gerankte Primärtreffer oder 8 KiB Nutzdaten,
  jeweils serverseitig gedeckelt; Diagnose-Samples bleiben nachgeordnet und
  begrenzt.
- Produktion darf nicht hinter Tests, Artefakten oder Diagnosen verschwinden;
  getrennte Counts machen diese Segmente sichtbar.
- Alte Parameter, Aliasfelder, `targetType`, `projectRoot`, Defaultpfade,
  unbekannte Altkeys und öffentliche Generationen werden hart entfernt.
- Keine stillen Fallbacks und kein Verhalten „erst alt, dann neu“.

Wenn ein Auditbefund nur Dokumentation statt Produktcode benötigt, korrigiere
die Dokumentation trotzdem auf den tatsächlich ausgelieferten Vertrag. Keine
Migrationssprache, historischen Zustände, veralteten Feldnamen oder zweiten
Contract-Wahrheiten hinterlassen.

## Phase 4: Tests und Verifikation

Ergänze oder korrigiere Verhaltenstests für jede geänderte Logik. Die Tests
müssen insbesondere die Raw-Wire-Verträge beweisen:

- mindestens fünf Source-Folgecall-Ketten,
- mindestens zwei Assembly-Folgecall-Ketten, soweit passende Fixtures
  vorhanden sind,
- positive und negative Handoff-Fälle,
- unbekannt, ambig, target-fremd, stale, loading, empty, partial,
  unsupported, not_configured und truncation,
- Schema, Runtime-Validator, Text und StructuredContent,
- C#-/Razor-/JavaScript-Routing,
- Root-/Parent-Aggregate und Filterverhalten,
- keine öffentliche Cache-Generation.

Führe danach in dieser Reihenfolge aus:

1. fokussierte Fast-/Component-Tests während der Reparatur,
2. `dotnet build`,
3. `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`,
4. `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`,
5. frisch gestartete MCP-Dogfood-/Raw-Wire-Verifikation,
6. Dokumentations- und aktiver Legacy-Rest-Scan,
7. `git diff --check`.

Bei fehlgeschlagenen oder abgeschnittenen Testausgaben verwende eine TRX-Datei
zur Diagnose, statt denselben Lauf ungezielt zu wiederholen. Stress-Tests
werden nur auf ausdrücklichen Auftrag ausgeführt.

Prüfe den aktiven Bestand abschließend textuell auf entfernte Reste, unter
anderem:

- `targetType`
- `projectRoot`
- alte Konfigurationsschlüssel
- Aliasparameter
- ungebundene ID-Formate
- `usedDefaultConfig`
- automatische Regeldateisuche
- öffentliche Cache-Generation
- Dual-Read-/Dual-Write- und Kompatibilitätsadapter
- auskommentierte oder unreferenzierte Vertragsreste

Ein gezielter Scan muss für die Hard-Cut-Reste 0 aktive Treffer ergeben; reine
historische Audit-/Konzeptreferenzen sind davon nur dann ausgenommen, wenn sie
keinen ausgelieferten Contract, Codepfad, Test oder eine aktuelle
Dokumentation beschreiben.

## Abschlussbericht

Beende erst, wenn entweder alle Gates grün sind oder ein echter externer
Blocker vorliegt. Berichte kompakt:

1. welche Agentenpfade vor dem Fix fehlschlugen oder irreführend waren,
2. welche Root Causes und Dateien geändert wurden,
3. welche Findings damit behoben sind,
4. welche Raw-Wire-/Dogfood-Ketten nach dem Fix grün sind,
5. die exakten Build-/Testbefehle und Ergebnisse,
6. verbleibende Findings mit Begründung und konkretem Blocker,
7. Baseline- und Commitstatus.

Behaupte keinen erfolgreichen Usage-Audit, wenn nur Unit-Tests grün sind. Der
Nachweis muss zeigen, dass ein Agent die vom MCP-Server ausgegebenen
StructuredContent-IDs kopieren und die versprochenen Folgecalls im selben
Target-/Snapshotvertrag erfolgreich oder mit dem richtigen sicheren Fehler
ausführen kann.
