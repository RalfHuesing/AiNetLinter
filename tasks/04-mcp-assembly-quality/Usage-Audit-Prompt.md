# Usage-Audit-Prompt: Verlässlicher Assembly-MCP-Vertrag

Arbeite im aktuellen Repository und prüfe ausschließlich den Assembly-Vertrag
aus Task 04. Der zu prüfende lokale Assembly-Target wird außerhalb dieses
Dokuments und außerhalb des Auditberichts bereitgestellt.

## Datenschutz- und Copyright-Schranke

Der konkrete `targetPath` ist vertraulich und darf niemals in Markdown,
Quellcode, Tests, Fixtures, Commit-Messages, Commit-Bodies oder den Auditbericht
geschrieben werden. Verwende in allen dauerhaften Artefakten ausschließlich
die neutrale Bezeichnung `private assembly target`.

Übernimm weder den Dateinamen, den Assemblynamen, Produktnamen, Namespace- oder
Typnamen noch dekompilierten Quelltext, Strings, Attribute, Kommentare,
Diagnostics oder Pfade aus dem Target in dauerhafte Artefakte. Rohe Toolantworten
dürfen nicht in Dateien kopiert werden. Befunde müssen redigiert werden:

- konkrete Targetpfade werden zu `<private-assembly-target>`;
- Assembly-, Produkt-, Namespace- und Typnamen werden zu `<target-symbol>`;
- Quelltext und decompilerzeugte Details werden zu `<target-data-redacted>`;
- externe Pfade, Benutzer- und Maschinenkennungen werden zu `<path-redacted>`.

Wenn ein Tool sensible Nutzdaten liefert, bewerte sie lokal und dokumentiere
nur Status, Feldnamen, Counts, Codes, Scope, Navigation und die Aussagegrenze.
Beende den Audit nicht mit einer kopierten Raw-Wire-Antwort.

## Auftrag

Führe einen echten Agenten-Usage-Audit gegen den vertraulich bereitgestellten
Assembly-Target durch. Prüfe den veröffentlichten MCP-Vertrag aus Sicht eines
Agents, der eine fremde verwaltete Assembly statisch untersucht. Behebe danach
alle in-scope Befunde direkt, sofern sie den Task-04-Vertrag verletzen.

Der Audit gilt nur für Assembly-Analyse. Source-Workflow, Git-Analyse,
Lintfachlichkeit, Testausführung, Runtime-Coverage, Consumerprojekte,
Sourcezuordnung und Änderungen an Source-Payloads gehören nicht zum Scope.
Die Assembly darf weder ausgeführt, geladen, initialisiert noch über ein
Consumerprojekt interpretiert werden.

## Maßgeblicher Vertrag

Lies vor Änderungen vollständig:

- `AGENTS.md`;
- `.agents/rules/AiNetLinterRichtlinien.mdc`;
- `.agents/rules/AiNetLinter-McpWorkflow.mdc`;
- `tasks/04-mcp-assembly-quality/Konzept.md`;
- `tasks/ainetlinter-mcp-usage-audit/shared/MCP-Verbesserungsrahmen.md`;
- `tasks/ainetlinter-mcp-usage-audit/shared/Befundmatrix.md`.

Der Task-04-Vertrag ist die normative Quelle. Aktive Registrierung,
`tools/list`, Runtimevalidierung, StructuredContent, Textprojektion,
Resources, Server-Instructions, Tests und Dokumentation müssen dieselbe
Aussage treffen. Historische Auditdateien sind keine Produktoberfläche.

## Arbeitsregeln

1. Erfasse vor Änderungen `git status --short` und `git log -1 --oneline`.
   Bewahre fremde Änderungen. Stagiere nur explizite Task-Dateien.
2. Bestätige den vertraulich übergebenen Targetpfad lokal mit `Test-Path`.
   Gib ihn danach nicht wieder aus und schreibe ihn nirgends hin.
3. Lies `tools/list` und verwende ausschließlich die aktuell veröffentlichten
   Properties. Rate keine Aliasfelder, Defaultwerte oder Responseformen.
4. Verwende für C#-Semantik zuerst den aktiven MCP-Server. Nutze `rg` und
   gezieltes Lesen für Registrierungen, Text, Dokumentation und Diff-Prüfung.
5. Ändere Dateien ausschließlich mit `apply_patch`. Erzeuge keine ad-hoc
   MCP-Skripte, privaten Testartefakte oder generierten Target-Ausgaben.
6. Behebe Ursachen zentral. Formatter-only-Patches genügen nicht, wenn Schema,
   Runtimevalidator oder StructuredContent weiterhin abweichen.
7. `find_dead_code` und `find_duplicates` liefern Kandidaten, keine
   Löschaufträge. `safeguard` ist ein Quality-Gate, aber kein Scope- oder
   Vollständigkeitsbeweis.
8. Jeder Befund wird redigiert dokumentiert. Keine Zielnamen oder Zielinhalte
   in Markdown, Quelltext, Tests, Logs, Commit oder Abschlussantwort.
9. Stoppe nicht nach dem ersten Befund. Prüfe alle Assembly-Gruppen und behebe
   Critical-/Major-Befunde vor dem Abschluss.

## Erwarteter Assembly-Vertrag

Der Agent muss erkennen können:

- dass jeder zielgebundene Call exakt den absoluten `targetPath` verwendet;
- dass `.dll` oder `.exe` ausschließlich `origin=decompiled` bestimmt;
- dass nur statische Metadaten und Decompilerdaten verarbeitet werden;
- welcher Snapshot aus kanonischem Pfad und Content-Hash gilt;
- ob ein Treffer ein kopierbarer Handoff für dasselbe Target und denselben
  Snapshot ist;
- ob ein Ergebnis `empty`, `partial`, `truncated` oder `not_decidable` ist;
- welcher einzelne nächste Schritt sicher und fachlich passend ist;
- dass Lease, Registry-Generation, Cache-, Workspace- und Materialisatpfade
  keine öffentlichen Agenteninputs sind.

Öffentliche Assemblyantworten dürfen keine Produkt- oder Targetdaten anderer
Agenten offenlegen. Globales Health liefert nur serverweite Aggregate,
Kapazitätszustand und begrenzte Fehlerzähler. Zielgebundenes Health zeigt nur
das angefragte Target.

## Phase 1: Baseline und Discovery

1. Lies `tools/list` und erfasse die Assembly-Capabilitymatrix.
2. Prüfe `get_server_health` ohne Target und mit dem privaten Assembly-Target.
   Globales Health darf keine Targetdetails, Pfade, Hashes, Diagnostics,
   Generationen oder Lease-Informationen enthalten.
3. Prüfe die Schemas von `inspect_assembly`, `search_assembly`,
   `get_assembly_context` und `find_assembly_extensions`.
4. Prüfe die Assemblyzweige von `find_symbol`, `get_symbol_body`,
   `get_class_structure`, `find_references`, `get_call_tree`, `get_impact`,
   `resolve_type_origin` und den unterstützten Metriktools.
5. Halte nur redigierte Befunde fest:

   | Tool/Call | Agentenverhalten | Vertrag | Befund | Root Cause | Test |
   | --- | --- | --- | --- | --- | --- |

6. Prüfe, dass alle zielgebundenen Antworten denselben Navigationkern
   projizieren: `target`, `origin`, `snapshot`, `capabilities`,
   `operationStatus`, `result`, `completeness` und `next`.

## Phase 2: Agenten-Usage-Prüfung

Sichere Raw-Wire-Antworten nur temporär im Arbeitsspeicher. Folgecalls kopieren
IDs und Tokens ausschließlich aus StructuredContent. Dokumentiere später nur
redigierte Metadaten.

### A. Katalog und Detailnavigation

Führe mindestens diese Ketten aus:

1. `inspect_assembly → get_assembly_context → get_symbol_body → get_call_tree`;
2. `search_assembly → get_class_structure → find_references`;
3. `find_assembly_extensions → resolve_type_origin` soweit die Antwort einen
   zulässigen Folgecall anbietet;
4. `get_assembly_context → get_impact` mit einer ausgegebenen kanonischen ID;
5. `get_assembly_context → metrics_lookup` mit einem StructuredContent-Input.

Prüfe je Kette:

- Handoff-IDs sind opaque, stabil und an Target, Snapshot und Symbolbindung
  gekoppelt;
- Folgecalls akzeptieren ausschließlich IDs aus StructuredContent;
- Text und StructuredContent zeigen dieselben sichtbaren IDs, Counts,
  Statuswerte, Scopeaussagen und nächsten Schritte;
- eine fehlende Referenz, ein fehlender Body oder eine leere Caller-Liste
  erzeugt keine globale Negativaussage;
- `includeReferences=true` bleibt statisch, begrenzt und transparent;
- Consumer- oder Laufzeitwissen wird nicht behauptet.

### B. Status, Continuation und Budgets

Prüfe gezielt:

- `continuationToken` ist die einzige öffentliche Continuation-Property;
- Tokens binden Target, Snapshot, Tool, Query, Filter, Sortierung und Position;
- `cursor`, `isTruncated`, öffentliche Generationen, Materialisatpfade und
  Sourcezuordnungen werden weder veröffentlicht noch als Input akzeptiert;
- `empty` enthält `totalCount=0` und nur den geprüften Scope;
- `truncated` enthält wahre Counts, `returnedCount`, `truncatedBy` und genau
  einen passenden nächsten Schritt;
- `partial` und `not_decidable` benennen Abschnitt, Ursache, Confidence und
  sichere Alternative;
- die Wirebudgets erhalten zuerst Katalog, IDs, Counts, Scope und Status;
- Diagnostics zählen zum Budget und bleiben begrenzt und redigiert.

### C. Target- und Snapshotbindung

Prüfe mit lokalen, neutral bezeichneten Fixtures oder dem privaten Target:

- identischer Pfad und identische Bytes erzeugen reproduzierbare Handoff-IDs;
- identische Bytes an verschiedenen Pfaden bleiben getrennte Targets;
- ein Bytewechsel führt zu `stale_snapshot`, niemals zu einer stillen
  Leermenge oder zur Verwendung eines fremden Snapshots;
- ein falsches Target führt zu `target_mismatch`;
- ungültige oder alte Tokens führen zu `invalid_argument`;
- der Serverneustart verändert bei identischem Pfad und identischen Bytes nicht
  den öffentlichen Handoff-Vertrag;
- Linkpfade werden kanonisch und sicher behandelt.

### D. Fehler- und Kapazitätsfälle

Prüfe ohne sensible Nutzdaten zu persistieren:

- nicht vorhandenes Target, Verzeichnis, leerer Pfad und falscher Dateityp;
- nicht verwaltete, beschädigte, gesperrte oder nicht lesbare Eingabe;
- ungültiges Pflichtfeld, unbekannte Property, falscher Typ und ungültiger
  Enum-Wert;
- `unsupported`, `target_mismatch`, `stale_snapshot`,
  `capacity_exhausted`, `invalid_assembly` und `target_unreadable`;
- genau ein Feld- oder Abschnittshinweis und ein sicherer nächster Schritt;
- kein Stack-Trace, kein Materialisatpfad und keine Zielidentität in der
  redigierten Dokumentation.

### E. Health, Lifecycle und Isolation

Prüfe seriell und, wo der Vertrag es verlangt, nebenläufig:

- eine parallele Erstöffnung desselben Keys erzeugt genau eine
  Materialisierung;
- parallele andere Keys teilen keine IDs, Diagnostics oder Lease-Zähler;
- Abbruch eines wartenden Callers beendet nicht die geteilte Erzeugung;
- aktive Leases werden weder durch TTL noch durch LRU verdrängt;
- Ressourcen werden nach Erfolg, Fehler und Abbruch vollständig freigegeben;
- globales Health zeigt keine Daten anderer Targets;
- zielgebundenes Health zeigt ausschließlich sein Target und verlängert keine
  Sessionlebensdauer.

## Phase 3: Freie Erkundung

Nach dem Pflichtkatalog erkunde die Assemblyoberfläche kreativ:

- wiederhole identische Calls zur Determinismusprüfung;
- teste Grenzwerte wie `maxResults=0`, sehr große Werte, leere Filter,
  Unicode und ungültige ContinuationTokens;
- führe nach einem Fehler denselben Call korrekt erneut aus;
- kombiniere `includeReferences`, Detailstufen und Antwortbudgets;
- prüfe, ob ein Agent ohne Zusatzdokumentation den nächsten Schritt erkennt.

Dokumentiere interessante Beobachtungen als `[Minor]`, aber immer redigiert.

## Phase 4: Red-Test-First und Behebung

Für jeden Critical- oder Major-Befund:

1. dokumentiere Severity, Tool, redigierte Evidenz und Reproduktion;
2. ergänze zuerst einen passenden Unit-, Component-, Integration- oder
   Raw-Wire-Test und weise den roten Zustand nach;
3. behebe die Ursache in Handler, Registry, Projektion, Validator,
   Registrierung oder Formatter;
4. weise den grünen Test nach;
5. prüfe Text-/StructuredContent-Parität und aktive Dokumentation;
6. entferne aus Testnamen, Fixtures und Ausgaben alle Ziel- und Produktnamen.

Keine Änderung darf den Assemblynamen, Targetpfad oder dekompilierten Inhalt
als Testdaten einchecken. Verwende neutrale synthetische Fixture-Bezeichnungen.

## Phase 5: Aktiver Scan und Dokumentation

Prüfe in `src/`, produktiven Tests, Fixtures, `Docs/`, `README.md`,
`.agents/rules/`, Resources, Registrierungen und Server-Instructions:

- keine öffentlichen Generationen, Lease-, Cache-, Workspace- oder
  Materialisatpfade;
- keine zweite Continuation-Form neben `continuationToken`;
- keine globale Health-Offenlegung von Target- oder Diagnosedaten;
- keine Behauptung von Source-, Consumer- oder Runtimewissen;
- keine produkt- oder targetbezogenen Namen aus dem privaten Target;
- keine nicht redigierten Assemblyantworten in Markdown oder Committexten.

Historische Taskdateien dürfen nicht als Produktoberfläche bewertet werden.
Der Auditbericht enthält nur redigierte Befunde und keine Zielquelltexte.

## Phase 6: Verifikation

Führe nach allen Änderungen seriell aus:

1. fokussierte Assembly-Contract- und Raw-Wire-Tests;
2. `dotnet build`;
3. `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`;
4. `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`;
5. frisch gestartete MCP-Dogfood-Ketten gegen den vertraulichen Target;
6. redigierter Legacy- und Dokumentationsscan;
7. `git diff --check`.

Stress-Tests werden nicht automatisch ausgeführt. Persistiere keine Raw-Wire-
oder Decompilerausgaben. Falls ein Testlauf scheitert, verwende eine
redigierte TRX-Diagnose und prüfe vor jeder Wiederholung die Ursache.

## Release-Gate

Der Audit ist nicht erfolgreich, solange einer dieser Befunde verbleibt:

- Schema, Runtimevalidator, StructuredContent, Text, Resource,
  Server-Instructions oder Dokumentation widersprechen dem Task-04-Vertrag;
- ID, Token oder Antwort ist nicht eindeutig an Target und Snapshot gebunden;
- Status, Scope, Count, Completeness oder `next` ist bei einem Assemblyfall
  irreführend;
- globales Health offenbart fremde Targetdaten oder Diagnostics;
- aktive Leases, Registry-Isolation, Kapazität oder Ressourcenfreigabe sind
  fehlerhaft;
- eine Antwort veröffentlicht Generation, Cache-, Workspace- oder
  Materialisatpfad, Sourcezuordnung oder eine zweite Continuation-Property;
- sensible Targetnamen, Produktnamen oder dekompilierte Nutzdaten landen in
  Markdown, Quelltext, Tests, Logs, Commit oder Bericht;
- Build, Nicht-Stress-Tests, Raw-Wire-, Dogfood-, Dokumentations- oder
  Diff-Gate ist nicht grün.

## Abschlussbericht

Erstelle ausschließlich einen redigierten Bericht. Nenne:

1. beobachtete Agentenprobleme ohne Zielidentität;
2. Root Causes und geänderte Dateien;
3. behobene Vertragsbefunde;
4. grüne Raw-Wire-Ketten nur mit neutralen Toolnamen;
5. exakte Build-/Testbefehle und Ergebnisse;
6. verbleibende Findings oder externe Blocker;
7. Baseline-, Commit- und Arbeitsbaumstatus.

Der Bericht darf weder den konkreten Assemblynamen, den Targetpfad,
Produktnamen, Typnamen, Namespace-Namen noch dekompilierten Quelltext enthalten.

Committe nur explizite Dateien mit einem deutschen Conventional Commit, zum
Beispiel `test(mcp-ux): prüfe Assemblyvertrag`, und kontrolliere vor dem
Commit `git diff --cached --check`. Der Committext bleibt vollständig neutral.
