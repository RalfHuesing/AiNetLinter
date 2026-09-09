# Usage-Audit-Prompt: Sicherer MCP-Entwicklungsworkflow

Du arbeitest im Repository `C:\Daten\Entwicklung\Ralf\AiNetLinter`.

## Auftrag

Führe einen echten Usage-Audit des aktuell aktiven AiNetLinter-MCP-Servers
durch — aus der Sicht eines Agenten, der den veröffentlichten Workflow für
Source-Änderungen tatsächlich bedient — und behebe anschließend proaktiv alle
dabei gefundenen in-scope Befunde.

Das Ziel ist nicht nur ein Auditbericht. Beweise und repariere die vollständige
Schleife:

`Kontext → Impact → statische Testkandidaten → Änderung → Violations/Metriken → gezielte Quality-Prüfung`

Prüfe dabei Runtime, MCP-Schemas, `tools/list`, Runtime-Validatoren,
StructuredContent, Textprojektion, Navigation, Registrierungen, Tests,
Fixtures, aktive Dokumentation, README, MCP-Resources, Server-Instructions
und aktive Agentenregeln. Ein sichtbarer Treffer ist nur dann ein Folgeinput,
wenn er als kanonischer Handoff für genaues Target und Snapshot ausgewiesen ist.

## Maßgeblicher Vertrag

Lies und befolge vor jeder Änderung vollständig:

- `AGENTS.md`
- `.agents/rules/AiNetLinterRichtlinien.mdc`
- `.agents/rules/AiNetLinter-McpWorkflow.mdc`
- `tasks/03-mcp-development-workflow/Konzept.md`
- `tasks/03-mcp-development-workflow/Vertragskatalog.json`
- `tasks/03-mcp-development-workflow/Legacy-Entfernungsmatrix.md`
- `tasks/ainetlinter-mcp-usage-audit/shared/MCP-Verbesserungsrahmen.md`
- `tasks/ainetlinter-mcp-usage-audit/shared/Befundmatrix.md`

`Konzept.md` und `Vertragskatalog.json` sind die normative Task-03-Quelle.
Die Entfernungsmatrix ist eine read-only Prüfgrundlage. Historische Audit- und
Taskdateien unter `tasks/**` sind keine aktive Produktoberfläche und dürfen
nicht geändert oder als Contract-Beleg gewertet werden.

Task 03 ist ein atomarer Hard Cut. Es gibt keine Migration, Aliasauflösung,
Kompatibilitätsphase, Dual-Read-/Dual-Write-Logik, Fallbackroute oder
Laufzeitumschaltung. Alte Requests, Response-Felder, IDs, Statuswerte,
Konfigurationspfade und Adapter werden weder akzeptiert noch erzeugt.

## Arbeitsregeln

1. Erfasse vor Änderungen `git status --short` und `git log -1 --oneline`.
   Bewahre fremde oder parallele Änderungen. Stagiere niemals pauschal mit
   `git add .`, `git add -A` oder `git commit -a`.
2. Bestimme die konkrete `.sln`/`.slnx`-Datei aus der sichtbaren
   Workspace-Struktur. Jeder zielgebundene MCP-Aufruf verwendet genau ihren
   absoluten, existierenden `targetPath`; kein Target wird geraten.
3. Verwende für C#-Semantik zuerst den aktiven AiNetLinter-MCP. `rg` und
   gezieltes Lesen bleiben für Nicht-C#-Text, exakte Editstellen,
   Dokumentation, Konfiguration und Diff-Prüfung erlaubt.
4. Verwende nur aktuell veröffentlichte Schemas und Parameter aus `tools/list`.
   Rate keine Aliasfelder, Defaultwerte oder Responseformen hinzu.
5. Ändere Dateien mit `apply_patch`. Committe keine Ad-hoc-MCP-Skripte,
   privaten Testartefakte oder generierten Ausgabedateien.
6. Behebe Ursachen zentral. Formatter-only- oder Markdown-only-Patches sind
   unzureichend, wenn Runtime, Schema und StructuredContent weiterhin eine
   andere Wahrheit liefern.
7. `get_test_context` und `get_feature_context` liefern statische
   Testkandidaten, niemals Runtime-Coverage oder Testausführung.
   `find_dead_code` und `find_duplicates` liefern Kandidaten, niemals
   Löschaufträge. `safeguard`-Scores ersetzen weder Scope noch Violations noch
   Konfigurationsstatus.
8. Ziehe keine Task-04-Assembly-Fachqualität in diesen Audit. Schütze aber
   Assembly-Regressionen, wenn gemeinsame Navigation oder Wire-Infrastruktur
   geändert wird.
9. Stoppe nicht nach dem ersten Finding. Ein Befund ist erst erledigt, wenn
   Produktursache, relevanter Test, Raw-Wire-/Dogfood-Pfad und aktive
   Dokumentation konsistent sind.

## Phase 1: Baseline und aktive MCP-Oberfläche

1. Prüfe `get_server_health` und die aktuelle Discovery-/Schema-Oberfläche.
2. Verifiziere, dass die Ziel-Solution geladen wird und `origin`, Snapshot,
   Capabilities, `operationStatus`, `completeness` und `next` zum Target
   passen.
3. Halte für jedes betroffene Tool eine Baseline fest:

   | Tool/Call | tatsächliches Agentenverhalten | Katalogvertrag | Befund | Root Cause | Test |
   | --- | --- | --- | --- | --- | --- |

4. Prüfe, dass die aktive Oberfläche ausschließlich den Vertragskatalog
   veröffentlicht. Insbesondere müssen `targetPath` und — wo gefordert —
   `symbolIdentifier` beziehungsweise `symbolIdentifiers` kanonisch sein.
5. Wenn der MCP nicht erreichbar ist, untersuche zuerst Registrierung,
   Serverstart und vorhandene C#-Testinfrastruktur. Melde keinen
   Produktbefund, solange nur der Testaufbau fehlschlägt.

## Phase 2: Gemeinsame Agenten-Usage-Prüfung

Sichere bei jedem Call die vollständige Raw-Wire-Antwort mit Text und
StructuredContent. Folgecalls dürfen IDs ausschließlich aus
StructuredContent kopieren. Prüfe, dass Text und StructuredContent dieselben
Counts, IDs, Statuswerte, Scopeaussagen und nächsten Schritte ausdrücken.

### A. Workflow-Schleife

Führe mindestens diese Ketten gegen eine kontrollierte Source-Fixture aus:

1. `get_feature_context -> get_impact`
2. `get_feature_context -> get_test_context`
3. `get_feature_context -> get_violations`
4. `get_feature_context -> metrics_lookup`
5. `get_impact -> get_violations`
6. `get_test_context -> get_violations`

Prüfe dabei:

- `get_feature_context` enthält genau die begrenzten Abschnitte
  `declaration`, `impact`, `testContext`, `violations` und `metrics`;
- `get_impact` trennt Symbolimpact und kanonischen Git-/Change-Kontext;
  ein leerer Diff-Scope bedeutet nie „kein Impact im Repository“;
- `get_test_context` und `testContext` statische Kandidaten, Heuristik,
  Scope, Counts, Confidence, Evidenzgrenze, Truncation und `next` ausweisen;
- `get_violations`, `metrics_lookup` und `safeguard` denselben fachlichen
  Source-Scope und denselben Snapshot verwenden;
- fehlende oder ungültige Regeln `not_configured` beziehungsweise einen klaren
  Fehler liefern, nie „0 Violations“ oder grünes Quality-Urteil;
- `metrics_lookup` ausschließlich das kanonische Array
  `symbolIdentifiers` akzeptiert;
- kein Composite Caller-/Body-Daten dupliziert oder einen Detailcall ersetzt.

### B. Precision-Tools und False-Green-Schutz

Führe Raw-Wire-Calls für alle fünf Precision-Tools aus:

- `safeguard`
- `pattern_detect`
- `find_dead_code`
- `find_duplicates`
- `find_magic_values`

Prüfe mindestens:

- `safeguard`: Score, Scope, Violations, Remediation und
  Konfigurationsstatus sind getrennt; fehlende Regeln sind niemals grün;
- `pattern_detect`: jede angeforderte Kategorie besitzt ihren eigenen Status,
  Ursache, Confidence, `truncatedBy` und nächsten Schritt;
- `find_dead_code` und `find_duplicates`: `resultType=candidate`, Confidence,
  Evidenzgrenze und Gegenprüfung; keine Lösch- oder globale Negativbehauptung;
- `find_magic_values`: ausschließlich disjunkte Katalogkategorien; Kategorie,
  Scope, Evidenz und Empfehlung dürfen Security-, Localization-, Framework-
  und Config-Aussagen nicht vermischen;
- leere, partialle und nicht entscheidbare Scopes werden nicht als global
  sauber dargestellt.

### C. Scope, Snapshot, Target und Handoff

Prüfe explizit:

- Root-, Projekt-, Datei-, Namespace- und Symbolscope;
- Produktions-, Test-, Artefakt- und Diagnostic-Trennung samt getrennten
  Counts und Ranking;
- `target_mismatch` für eine ID aus einem anderen Target;
- `stale_snapshot` nach Änderung einer geladenen Source-Datei, niemals
  `symbol_not_found`;
- `invalid_argument` für unbekannte, alte oder nichtkanonische Inputs;
- `unsupported` für capability-fremde Aufrufe;
- `loading`, `empty`, `partial`, `truncated`, `not_configured` und
  `not_decidable` mit exaktem Code und sicherem nächsten Schritt;
- jede navigierbare ID mit `handoff=true`, kanonischem Identifier,
  Target-/Snapshotbindung und erlaubten Folge-Tools;
- nicht navigierbare Treffer mit `handoff=false`, Grund und sicherem nächsten
  Schritt;
- keine künstliche ID bei leerem, nicht konfiguriertem oder nicht
  entscheidbarem Abschnitt;
- keine Lease-, Session- oder Cache-Generation in Workflow-DTOs oder
  Agenteninputs.

### D. Status-, Count- und Truncation-Wahrheit

Für jeden Composite-Abschnitt und jeden Precision-Resultattyp gilt:

- vollständig geprüfte Leermenge: `operationStatus=ok`, `completeness=empty`,
  `totalCount=0` und keine Aussage außerhalb des Scopes;
- bekannter begrenzter Rest: `completeness=truncated` mit `totalCount`,
  `returnedCount`, `truncatedBy` und genau einem passenden `next`;
- fehlende Evidenz oder dynamische Grenzen: `partial` oder `not_decidable`
  mit Ursache, Confidence und sicherer Alternative;
- `complete`, `partial` und `truncated` beziehen sich immer auf einen
  benannten Abschnitt, nie unpräzise auf eine Session;
- kein `maxResults + 1`-Schein-Count, kein stilles Abschneiden und kein
  `isTruncated`/`truncated` ohne benannten Abschnitt;
- Textprojektion und StructuredContent verwenden dieselben sichtbaren Listen
  und konsistente Wire-Budget-Zähler.

## Phase 3: Hard-Cut- und Aktivscan

Prüfe die aktiven Pfade in `src/`, produktiven MCP-Tests und Fixtures,
`Docs/`, `README.md`, `.agents/rules/`, `ainetlinter-rules.json`,
MCP-Resources, Registrierungen und Server-Instructions. Schließe `tasks/**`
aus; historische Treffer dort sind keine Produktbefunde.

Die folgenden aktiven Legacyformen müssen null Treffer ergeben oder als
unvermeidbare technische Nicht-Contract-Begriffe belegt sein:

- `targetType`, `projectRoot`, `configPath`, `solutionPath` als Toolinput;
- `query`, `searchPattern`, `fileFilter`, `includePattern` als Suchalias;
- Aliasrouten und alte Optionsnamen allgemein;
- `isTruncated`/`truncated` ohne benannten Abschnitt;
- Test-„Coverage“, „abgedeckt“ oder ähnliche Runtime-Behauptungen für statische
  Kandidaten;
- Score als vollständiges Quality-/Lint-Urteil;
- Dead-Code-/Duplicate-Treffer als Löschauftrag;
- `usedDefaultConfig`, automatische Regeldateisuche und alte Fallbackpfade;
- Dual-Read-/Dual-Write- und Kompatibilitätsadapter.

Prüfe zusätzlich Schema-/Response-Allowlist und einen generischen Raw-Wire-
Test, der unbekannte Properties mit `invalid_argument`, Feldpfad und sicherem
`next` ablehnt. Entferne keinen historischen Auditbeleg und füge keine alten
Requests als Fixtures wieder hinzu.

## Phase 4: Vertrag, Budget und Dokumentation

Für jedes geänderte Tool müssen `Vertragskatalog.json`, Registrierung,
`tools/list`, Runtime-Validator, StructuredContent, Formatter, Text,
MCP-Resource, Server-Instructions, Tests, README und aktive Docs dieselben
Aussagen treffen zu:

- Required-/Optional-Inputs, Typen, Defaults, Caps und verbotene Inputs;
- `navigation`, Target, Origin, Snapshot und Capabilities;
- Status, Error-Code, Completeness, Confidence, Scope und Evidenzgrenze;
- Counts, Ranking, Truncation und genau ein nächster Schritt;
- Handoff-Fähigkeit und erlaubte Folgecalls.

Prüfe jedes Composite nach UTF-8-Serialisierung:

- maximal 12 KiB für die vollständige Nutzlast;
- maximal 4 KiB je benanntem Abschnitt;
- Diagnostics zählen zum Budget und folgen den Nutzdaten;
- keine doppelte Caller-/Body-Nutzlast;
- nach Wire-Truncation stimmen sichtbare Listen und Counts weiterhin überein.

Aktualisiere bei Contract- oder CLI-Änderungen die betroffenen aktiven
Dokumente, insbesondere `Docs/agent-api.md`, `Docs/integration.md`,
`Docs/ROADMAP.md`, README, MCP-Resources, Server-Instructions und manuelle
Agentenregeln. Wenn `ainetlinter-rules.json` betroffen ist, synchronisiere
`.agents/rules/AiNetLinter.mdc` ausschließlich über den vorgesehenen Generator.

## Phase 5: Tests und Verifikation

Ergänze oder korrigiere Tests für jede geänderte Logik. Erforderlich sind,
soweit fachlich betroffen:

- eine gemeinsame Source-Fixture mit Produktion, Tests, Artefakt,
  Violation, Reflection-/DI-Grenze und statischem Testsignal;
- Workflow- und Precision-Contract-/Schema-Tests;
- mindestens fünf Raw-Wire-Folgecall-Ketten;
- positive und negative Handoff-Fälle;
- unbekannt, target-fremd, stale, empty, partial, truncated, unsupported,
  not_configured und not_decidable;
- Text-/StructuredContent-Parität;
- getrennte Prod-/Test-/Artefakt-/Diagnostic-Counts;
- False-Green-Schutztests für jede Precision-Familie;
- Wire-Budget- und Response-Allowlist-Tests;
- Source-/Assembly-Regressionstests, ohne Task-04-Fachpayload oder Lifecycle
  zu verändern.

Führe danach in dieser Reihenfolge aus:

1. fokussierte Fast-/Component-Tests;
2. `dotnet build`;
3. `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`;
4. `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`;
5. frisch gestartete MCP-Dogfood-/Raw-Wire-Verifikation gegen die kontrollierte
   Source-Fixture;
6. aktiver Legacy-Rest-Scan und Dokumentationsprüfung;
7. `git diff --check`.

Stress-Tests werden niemals automatisch ausgeführt. Bei abgeschnittenen oder
fehlgeschlagenen Ausgaben verwende eine TRX-Datei zur Diagnose und wiederhole
nicht ungezielt denselben Lauf.

## Release-Gate

Der Release ist blockiert, sobald einer dieser Befunde verbleibt:

- ein alter Input, Alias, Responsewert, ID-, Status- oder Konfigurationspfad
  wird im aktiven Prüfpfad akzeptiert, erzeugt, registriert, dokumentiert oder
  still umgedeutet;
- Workflowtools widersprechen sich bei Target, Snapshot, Scope, Counts,
  Lintstatus, Completeness oder Handoff;
- Text und StructuredContent unterscheiden sich bei sichtbaren Daten oder
  Folgeschritten;
- ein begrenztes, partielles oder nicht entscheidbares Ergebnis behauptet
  Sauberkeit, globale Abwesenheit, Runtime-Coverage oder Löschbarkeit;
- ein Composite überschreitet sein Wirebudget, dupliziert Nutzdaten,
  erfindet IDs oder meldet inkonsistente Counts;
- fehlende Regeln erscheinen als `0 Violations` oder grüner Safeguard;
- eine Precision-Kategorie ist überlappend oder fachlich falsch zugeordnet;
- Task-04-Assembly-Fachpayload, Decompilersemantik, Lease oder Lifecycle
  wurde durch Task 03 verändert;
- Generator-, Build-, Nicht-Stress-, Raw-Wire-, Dogfood-, Dokumentations- oder
  Diff-Gate ist nicht grün.

## Abschlussbericht

Beende erst, wenn alle Gates grün sind oder ein echter externer Blocker
vorliegt. Berichte kompakt:

1. welche Agentenpfade vor dem Fix fehlschlugen oder irreführten;
2. Root Causes und geänderte Dateien;
3. behobene Vertrags- und Legacy-Befunde;
4. grüne Raw-Wire-/Dogfood-Ketten;
5. exakte Build-/Testbefehle und Ergebnisse;
6. verbleibende Findings mit konkretem Blocker;
7. Baseline-, Commit- und Arbeitsbaumstatus.

Behaupte keinen erfolgreichen Task-03-Usage-Audit, wenn nur Unit-Tests grün
sind. Der Nachweis muss zeigen, dass ein Agent den veröffentlichten
StructuredContent-Handoff kopieren und die versprochenen Workflow- und
Precision-Folgecalls im selben Target-/Snapshotvertrag sicher ausführen kann.
