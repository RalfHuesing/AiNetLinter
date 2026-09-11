# Gruppe B – Discovery-Tools (Agenten-UX-Audit)

## Ziel und Soll

Geprüft wurde der heutige MCP-Server gegen `tasks/usage-audit/Konzept.md`, insbesondere AK 18–23 und AK 31–35: Discovery soll progressive, handlungsrelevante Ergebnisse liefern; Scope, Generated/Editable, Populationen, Namespace- und Assembly-Herkunft sowie Completeness müssen maschinenlesbar und ehrlich sein. Zielpfade waren `AiNetLinter.slnx` (Source) und die vorhandene produktive DLL `src/AiNetLinter/bin/Release/net10.0/AiNetLinter.dll` (Assembly). Es wurden ausschließlich MCP-Reads ausgeführt.

## Positive Verifikationen

- `get_file_tree(view=summary)` liefert kompakte Aggregation (1.067 physische Dateien, Extensions/Verzeichnisse), ohne Dateiliste, mit `completeness=complete` und `next=null`.
- `get_file_tree(view=files, root=., maxResults=10)` und `view=tree, treeDepth=2` liefern bei Kürzung konkrete Counts, `truncatedBy` und einen verfeinerbaren `next`-Hinweis. `fileFilter=*.cs` beschränkt Treffer tatsächlich auf `.cs`.
- `get_index_scope` trennt `.cs` (912, Symbolgraph) von nicht-C#-Dateien und nennt je Routing-Tool, Query-Feld und Scope.
- `get_namespace_tree(project=AiNetLinter, namespacePrefix=AiNetLinter.Mcp.Tools.TestContext, includeTypes=true)` liefert die erwarteten 7 direkten Typen. Ein ungültiger `kind`-Wert und `depth=0` werden feldbezogen als `INVALID_ARGUMENT` erklärt.
- `find_symbol(TestCoverageScanner)` zeigt in `all` die editierbare Production vor Tests; `scopeType=production` und `tests` sind disjunkt. `includeGenerated=true` erweitert die Population (84 → 86 Treffer) und der Scope ist sichtbar.
- `inspect_assembly` und `find_assembly_extensions` funktionieren gegen die DLL ohne Crash, kennzeichnen Decompilation/Abhängigkeiten und geben Assembly-Herkunft, Hash und Trunkierungsinformationen aus. `maxResults=0` wird bei File-Tree korrekt abgelehnt; Assembly-0 bedeutet dort dokumentiert Default.

## Findings

### [Major] Befund 1: Namespace-Overflow signalisiert fälschlich eine leere Population

- **Tools:** `get_namespace_tree`
- **Konzeptbezug:** AK 31 (Namespace-/Populationsemantik), Leitprinzip 5 (ehrliches `complete`/`truncated`), B1/Slice 19.
- **Evidenz:** `get_namespace_tree(targetPath=..., namespacePrefix="AiNetLinter.Mcp.Tools.TestContext", depth=2, includeTypes=true)` mit Defaultbudget liefert `totalCount=0`, `shownCount=0`, `namespaces=[]`, aber `truncated=true`, `truncatedBy=["maxResponseBytes"]`. Derselbe fachlich gültige Namespace liefert mit `project="AiNetLinter", depth=1, maxResponseBytes=65536` sieben Typen.
- **Problem aus Agentensicht:** Ein Agent erhält bei Defaultparametern eine formal erfolgreiche, aber inhaltlich wie „Namespace leer“ wirkende Antwort. `totalCount=0` ist mit dem real vorhandenen Ergebnis unvereinbar; der `next`-Hinweis sagt nur allgemein „Budget erhöhen“. Der Agent muss raten, dass die Population nicht leer, sondern wegen Projektion/Budget unsichtbar ist.
- **Reproduktion:** 1. `get_namespace_tree` mit `namespacePrefix=AiNetLinter.Mcp.Tools.TestContext`, `depth=2`, `includeTypes=true`, ohne `project` und ohne `maxResponseBytes` aufrufen. 2. Mit `project=AiNetLinter`, `depth=1`, `maxResponseBytes=65536` wiederholen. 3. Antworten bezüglich `totalCount`/Typen vergleichen.
- **Empfohlene Richtung:** Bei Budgettrunkierung Counts der vollständigen gefilterten Namespace-Population beibehalten (oder explizit `countUnknown`/`populationOmitted` ausweisen); niemals `totalCount=0` zusammen mit `truncatedBy=maxResponseBytes` für einen existierenden Namespace melden.

### [Major] Befund 2: Assembly-Completeness ist zwischen Fachpayload und Navigation widersprüchlich

- **Tools:** `inspect_assembly`, `find_assembly_extensions`
- **Konzeptbezug:** AK 8/31 (gleicher Envelope für Assembly und Status), Leitprinzip 5, Cross-Tool-Konsistenz.
- **Evidenz:** `inspect_assembly(targetPath=<AiNetLinter.dll>, maxResults=20)` meldet im Text `status=partial; completeness=partial`; `structuredContent.completeness="partial"` und `analysis.completeness="partial"`, gleichzeitig aber `structuredContent.navigation.status.completeness="truncated"` (dieser Teil ist noch nachvollziehbar wegen maxResults). Bei einem exakt gefilterten Assembly-Call (`typeName=AiNetLinter.Program`, `exactTypeName=true`, `publicOnly=false`, `detailLevel=compact`) steht `analysis.completeness="partial"` wegen Decompilerdiagnosen, während `navigation.status.completeness="complete"` und der Text-Footer `completeness=complete` lautet. `find_assembly_extensions` zeigt dasselbe Muster: Payload `completeness="partial"`/`sessionStatus="partial"`, Navigation und Text `complete`.
- **Problem aus Agentensicht:** Ein Agent kann die fachliche Teilvollständigkeit als „vollständig erfolgreich“ interpretieren und Folgeentscheidungen (z. B. keine weitere Referenz-/Detailabfrage) abbrechen. Das verletzt den im Konzept geforderten gemeinsamen Status-/Envelope-Vertrag und macht `complete` semantisch nicht belastbar.
- **Reproduktion:** 1. `inspect_assembly` mit `typeName=AiNetLinter.Program`, `exactTypeName=true`, `publicOnly=false`, `detailLevel=compact`, `maxResponseBytes=65536` aufrufen. 2. `analysis.completeness`, Root-`completeness` und `navigation.status.completeness` nebeneinander lesen. 3. `find_assembly_extensions` ohne Filter wiederholen und `structuredContent.completeness` mit Navigation vergleichen.
- **Empfohlene Richtung:** Decompiler-/Referenz-Teilstatus in den gemeinsamen Navigation-Status projizieren; `complete` nur bei vollständiger fachlicher Population verwenden. Text, Rootpayload und Navigation müssen aus demselben Status stammen.

### [Minor] Befund 3: Discovery-Populationen sind sichtbar, aber bei File-Tree nicht vollständig selbsterklärend

- **Tools:** `get_file_tree`, `get_index_scope`
- **Konzeptbezug:** AK 31/B3 (benannte Populationen und Ausschlüsse), Slice 18.
- **Evidenz:** Root-Tree ohne Filter meldet `scannedFileCount=1067`, `matchedFileCount=1067`, `population.physicalFileCount=1067`, `excludedCount=0`, aber `skippedExcludedDirectoryCount=28`. Der `.cs`-Filter meldet 931 physische Treffer, während `get_index_scope` 912 `.cs`-Dateien im Symbolgraphen ausweist und zusätzlich `generatedDocumentCount=23`, `testDocumentCount=432` nennt.
- **Problem aus Agentensicht:** Die Zahlen sind wahrscheinlich fachlich korrekt, aber ein Erstnutzer sieht nicht sofort, dass File-Tree physische Dateien, Index Roslyn-Dokumente und Generated/Test-Dokumente unterschiedliche Populationen messen. Für Discovery-/SNR-Entscheidungen ist eine direkte Zuordnung nötig; insbesondere `excludedCount=0` neben 28 übersprungenen Excluded-Directories wirkt widersprüchlich.
- **Reproduktion:** `get_file_tree(view=summary)`, `get_file_tree(fileFilter="*.cs", maxResults=20)` und `get_index_scope` aus derselben Session ausführen; Population- und Exclusion-Felder vergleichen.
- **Empfohlene Richtung:** Populationstyp in den File-Tree-Counts explizit benennen und übersprungene Standardverzeichnisse von nutzerseitig angewandten `excludePatterns` klar trennen.

## Freie Erkundung / Determinismus / Chaining

- Identische `find_symbol(TestCoverageScanner)`-Aufrufe waren deterministisch; Production-first-Ranking blieb stabil.
- Namespace-Chaining ist möglich: `AiNetLinter.Mcp.Tools` liefert seine Subnamespace inklusive `AiNetLinter.Mcp.Tools.TestContext`; der direkte Prefix-Aufruf mit ausreichendem Budget liefert danach die direkten Typen. Der Defaultbudget-Fall aus Befund 1 bleibt jedoch ein riskanter erster Agentenaufruf.
- `inspect_assembly` liefert bei gefilterter interner/dekompilierter Typbezeichnung `AiNetLinter.Mcp.Tools.TestContext.GetTestContextTool` keine Typen, obwohl `publicOnly=false` gesetzt wurde; das ist als Assembly-/Decompilation-Einschränkung beobachtet, aber wegen fehlender expliziter „nicht gefunden vs. nicht darstellbar“-Kennzeichnung nicht als eigener Critical-Befund gewertet.
- Keine Codeänderungen, Tests, Builds, Commits oder Observability-Feedbacks wurden ausgeführt.

## Externe Assembly-Referenzfälle (vertraulich abstrahiert)

Die vier lokalen Referenzfälle aus `temp/decompiled-assembly-audit-examples.md` wurden nur unter den Labels GIT-01/LOCAL-01/LOCAL-02/LOCAL-03/FALSE-01 ausgeführt; konkrete Namen, Pfade, Namespaces und Identitäten werden hier nicht wiedergegeben.

- Zwei externe Testassemblies wurden erfolgreich metadata-only dekompiliert. `inspect_assembly` meldete jeweils `origin=decompiled`, `bodyAvailability=available`, `completeness=partial` und maschinenlesbare Typ-/Budgetkürzung. `find_assembly_extensions` lief ohne Fehler und lieferte eine leere Ergebnisliste bei partialer Herkunft/Diagnosen. Das bestätigt die gewünschte sichere Fallback-Funktion, reproduziert aber die in Befund 2 beschriebene Statusdiskrepanz.
- Eine externe verwaltete EXE lieferte bei `find_assembly_extensions` mehrere Extension-Kandidaten mit `not_decidable`, `completeness=partial` und Budget-/Referenzkürzung. Die Trennung `applicable`/`not_applicable`/`not_decidable` ist für Agenten nützlich; der partiale Status muss jedoch auch im Navigation-Envelope maßgeblich bleiben.
- Der nicht verwaltete Negativfall wurde von beiden Assembly-Tools mit `INVALID_ASSEMBLY`, `completeness=not_applicable` und recoverablem Hinweis abgelehnt; es gab keine Ausführung der Datei. Dieser Sollfall ist erfüllt.
