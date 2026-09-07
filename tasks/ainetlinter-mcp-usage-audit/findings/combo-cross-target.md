# Finding: Kombination `project` vs. `assembly` (Cross-Target)

Datum: 2026-09-07. Nur Live-Calls dieser Kombination plus Schema-Lookup der drei beteiligten Tools. Kein Build, kein Test, keine Synthese anderer Findings.

**Thema (eines):** Typ `DataExecutor` im Platform-Projekt vs. Typ `Record` in `Example.External.Process.dll`. Gleiche Navigationsfrage, dieselben Toolnamen, zwei Targets:

`find_symbol` (`pattern=^Name$`, `kind=class`) → `get_class_structure` (`maxMembers` klein, `sortBy=name`) → `get_symbol_body` (`maxBodyLines` klein). Danach ID-Austausch (Projekt-ID am Assembly-Target und umgekehrt).

- project: `C:\Workspace\Sample.Project`
- assembly: `C:\ExternalAssemblies\Version-9\Example.External.Process.dll`

---

## 1. Schema-Kurzfazit

Alle drei Tools tragen denselben Zielvertrag: `targetType=project|assembly` plus absoluter `targetPath`. Die Beschreibungen sagen **nicht**, dass zurückgegebene Symbol-IDs **zielgebunden** sind. Ein Agent, der `id:` aus der Antwort kopiert, sieht drei Familien:

| Familie | Beispiel aus diesem Lauf | Wo gültig |
|---|---|---|
| DocCommentId | `T:Sample.Project.Infrastructure.Sql.DataExecutor` | nur `project` |
| Assembly-Session-ID | `assembly:2AFC08…AAC35:7:T:Example.External.Process.Record` | nur **diese** DLL **und** Generation `7` |
| Innere DocId | `T:Example.External.Process.Record` | am Assembly-Target (wird kanonisiert); am Projekt **nicht** |

Steuert der Call richtig? **Teilweise.** `targetType`+`targetPath` reichen, um denselben Toolnamen zweimal korrekt zu adressieren. Die ID-Semantik (`T:` vs. `assembly:sha:gen:T:`) steht nicht im Schema; `generation` und `completeness=partial` erscheinen nur im Assembly-Antwortheader, nicht als Agenten-Regel „diese ID nicht ins andere Target kleben“.

Irreführung für die Kombination:

- Assembly-IDs am **Projekt**-Target scheitern als `INVALID_ARGUMENT` „gehört nicht zur aktuellen Assembly-Generation“ — auch wenn Generation und SHA **aktuell** sind. Das ist ein Target-Mismatch, kein Generationsfehler.
- Falscher SHA mit passender Generation: **dieselbe** Generation-Meldung. Hash, Generation und Target werden in einem Satz zusammengeworfen.
- Schema verspricht bei Assembly Herkunft/Snapshot/Vollständigkeit. Live ist `completeness=partial` **immer** im Header, selbst bei einem exakten Klassentreffer `^Record$`.
- `get_class_structure` liefert auf beiden Targets **keine** Member-DocIds; der Folgecall `get_symbol_body` braucht selbst gebaute oder aus Ambiguity kopierte IDs.

---

## 2. Ausgeführte Calls

Wartezeit überall **schnell**, kein Timeout. Größen = sichtbarer Markdown-Text.

### A. Gleiche Frage, passendes Target (Happy Path)

| # | Tool | Target | Identifikator / Muster | Ergebnis | Größe (grob) | Completeness |
|---|------|--------|------------------------|----------|--------------|--------------|
| A1 | `find_symbol` | project | `pattern=^DataExecutor$`, `kind=class`, `maxResults=10` | 2 Zeilen, **dieselbe** `T:`-ID, zwei Partial-Dateien (`DataExecutor.cs:9`, `…OptimisticConcurrency.cs:11`) | ~0,5k | vollständig (kein Header) |
| A2 | `find_symbol` | assembly | `pattern=^Record$`, `kind=class`, `maxResults=10` | 1 Klasse; `id: assembly:2AFC08F102173788D9E46E20F091664C06737E05E5BF6715377BB45C089AAC35:7:T:Example.External.Process.Record` | ~0,9k + Header | Header: `origin=decompiled; generation=7; status=partial; completeness=partial` |
| A3 | `get_class_structure` | project | `T:…DataExecutor`, `maxMembers=5`, `sortBy=name` | Klasse, 2 Dateien, **5 von 33**, Total Lines 584 | ~1,5k | Truncation-Zeile + widersprüchlicher „vollständig, kein Read/Grep“ |
| A4 | `get_class_structure` | assembly | Session-ID aus A2, `maxMembers=5`, `sortBy=name` | Klasse, 1 Datei `Record.cs`, **5 von 316**, Total Lines 7139; 2 Ctors + 3 Properties | ~1,2k + Header | Header `partial`; Truncation 5/316 + dieselbe Vollständig-Lüge |
| A5 | `get_symbol_body` | project | `T:…DataExecutor`, `maxBodyLines=8` | `id` bleibt `T:…`; `contentMode=source`; **1–8 von 292** | ~0,8k | Fenster explizit |
| A6 | `get_symbol_body` | assembly | Session-ID aus A2, `maxBodyLines=8` | `id` bleibt `assembly:…:7:T:…`; `contentMode=decompiledProject`; Cache-Pfad `…\generation-a6ca2d4d…\Record.cs`; **1–8 von 7145** | ~1,2k + Header | Header `partial`; Fenster explizit |

### B. ID / Name am **fremden** Target (dürfen nicht still mischen)

| # | Tool | Target | Identifikator | Ergebnis |
|---|------|--------|---------------|----------|
| B1 | `get_class_structure` | **project** | Assembly-Session-ID `assembly:…:7:T:…Record` (aktuell!) | `INVALID_ARGUMENT` Generation — **kein** Record-Treffer |
| B2 | `get_symbol_body` | **project** | dieselbe `assembly:…:7:T:…Record` | identisch B1 |
| B3 | `get_symbol_body` | **project** | `assembly:…:7:P:…DcmContextRecord.Record` | identisch B1 (auch Property-IDs) |
| B4 | `get_class_structure` | **assembly** | `T:…DataExecutor` | `SYMBOL_NOT_FOUND` (Header trotzdem `generation=7`, `completeness=partial`, `bodyAvailability=available`) |
| B5 | `get_symbol_body` | **assembly** | `T:…DataExecutor` | `SYMBOL_NOT_FOUND` (erster Versuch: Cursor Auto-review blockiert Cross-Target-Read; nach Freigabe derselbe NOT_FOUND) |
| B6 | `get_class_structure` | **assembly** | `P:…FormLoadQueryRequest.DataExecutor` | `SYMBOL_NOT_FOUND` |
| B7 | `get_class_structure` | **project** | nacktes `T:Example.External.Process.Record` | `SYMBOL_NOT_FOUND` |
| B8 | `get_symbol_body` | **project** | nacktes `T:…Record` | `SYMBOL_NOT_FOUND` |
| B9 | `find_symbol` | **project** | `pattern=^Record$`, `kind=class` | 0 Treffer + Fuzzy (`ScheduleRecording…`, `RecordPersistence…`) — **kein** extern-`Record` |
| B10 | `find_symbol` | **assembly** | `pattern=^DataExecutor$`, `kind=class` | 0 Treffer, **kein** Fuzzy |
| B11 | `get_class_structure` | **project** | `Record.cs:31` | `SYMBOL_NOT_FOUND` |
| B12 | `get_class_structure` | **assembly** | `DataExecutor.cs:9` | `SYMBOL_NOT_FOUND` |

Kein Call hat still den anderen Index bedient. Kein extern-`Record` aus dem Projekt, kein `DataExecutor` aus der DLL.

### C. Innere DocId und Generation **am richtigen** Assembly-Target

| # | Tool | Identifikator | Ergebnis |
|---|------|---------------|----------|
| C1 | `get_class_structure` | nacktes `T:Example.External.Process.Record`, `maxMembers=3` | Hit: **3 von 316**, Default-Reihenfolge (Nested + Field), **ohne** `assembly:`-Prefix in der Tabellenantwort |
| C2 | `get_symbol_body` | nacktes `T:…Record`, `maxBodyLines=5` | `angefordert: T:…Record`; kanonische `id: assembly:…:7:T:…Record`; 1–5 von 7145 |
| C3 | `get_class_structure` | `assembly:…:3:T:…Record` (stale, Welle-1-Generation) | `INVALID_ARGUMENT` Generation; Header zeigt aktuell `generation=7` |
| C4 | `get_class_structure` | `assembly:DEADBEEF…:7:T:…Record` (falscher SHA, Generation 7) | **dieselbe** Generation-Meldung wie C3/B1 |
| C5 | `get_symbol_body` | aktuelle Session-ID `…:7:T:…Record`, `maxBodyLines=3` | Roundtrip ok, 1–3 von 7145 |
| C6 | `get_class_structure` | `Record.cs:31` am Assembly-Target | Hit wie C1 (Dekompilat-Dateiname reicht **in** der Session) |
| C7 | `get_class_structure` | Kurzname `Record` | `AMBIGUOUS_SYMBOL`: Klasse + 2 Properties, alle mit `assembly:…:7:T|P:…` |
| C8 | `get_class_structure` | Kurzname `DataExecutor` am Projekt | `AMBIGUOUS_SYMBOL`: 7× `P:` + 2× Partial-`T:` (gleiche DocId) |
| C9 | `find_symbol` | assembly `^Record$`, `includeReferences=true`, `maxResults=3` | 1 Root-Treffer + Block „Assemblies: 16 von 16; Vollständigkeit: partial“ + 5 Decompiler-Diagnosen (CS0535, CS0227, CS1525, …); ID unverändert `…:7:T:…Record` |

---

## 3. Verdict

**teilweise** — dieselbe Navigationskette funktioniert auf beiden Targets, sobald ID-Familie und Target zusammenpassen. Cross-Target-IDs werden **nicht still vermischt**. Abweichend: Fehlertext (Generation statt Target/Hash), instabile `generation` über Sessions (`3` in älteren Läufen, hier `7`), Assembly-`completeness=partial` auch bei eindeutigem 1-Treffer, asymmetrisches Leer-Verhalten (`find_symbol` Fuzzy nur im Projekt), fehlende Member-IDs in der Klassentabelle auf **beiden** Seiten.

---

## 4. Schwere

**degraded**

Nicht `broken`: Agent kommt mit `T:` am Projekt und `T:` **oder** frischer `assembly:sha:gen:`-ID an der DLL zum Typ. Kein stiller Fremdindex, kein falscher Body.

Nicht nur `friction`: Generation steigt zwischen Sessions; kopierte `assembly:…:3:`-IDs aus einem früheren Chat sind tot. Der Generation-Fehler am **Projekt**-Target schickt den Agenten in die falsche Reparatur (neue Assembly-ID holen statt Target wechseln). `Record` 316 Member / 7145 Body-Zeilen vs. `DataExecutor` 33 / 292 — Default-Limits wirken auf der DLL systematisch unvollständig, der Header ruft trotzdem `partial` auch dann, wenn die **Trefferliste** vollständig ist.

---

## 5. Nutzbarkeit

**nur mit Workaround**

Wieder aufrufen: ja, für externe API vs. Platform-Typ — aber strikt:

1. `targetType`/`targetPath` **und** ID-Familie nie mischen. `assembly:`-IDs nur an dieselbe DLL; `T:Sample.smart…` nur an `project`.
2. Am Assembly-Target die **nackte** `T:externde…Record` bevorzugen: sie überlebt Generation-Sprünge besser als `assembly:sha:gen:`. Die Antwort kanonisiert auf die Session-ID.
3. Kurznamen `Record` / `DataExecutor` nicht als `get_class_structure`-Anker (Ambiguity Klasse vs. Properties auf beiden Targets).
4. `find_symbol` mit `^Name$` + `kind=class`, dann ID kopieren.
5. Truncation-Zeile (`5 von 33` / `5 von 316`) ernst nehmen; Header-`partial` und den Satz „kein Read/Grep“ ignorieren.
6. `includeReferences=true` nur wenn Referenzassemblies wirklich gebraucht werden — sonst Diagnosen-Lärm ohne Extra-`Record`.

---

## 6. Bugs (reproduzierbar) und FP/FN

Stichprobe = diese Live-Antworten gegeneinander (kein separates `rg`).

**Bugs**

1. **Target-Mismatch als Generationsfehler.** B1–B3: aktuelle `assembly:…:7:T|P:…Record` am Projekt → „gehört nicht zur aktuellen Assembly-Generation“. Es gibt am Projekt keine Assembly-Generation. Hint fordert eine ID „aus dem Assembly-Ziel“ — der Agent bleibt im falschen Target.
2. **SHA-Mismatch = Generation.** C4: `DEADBEEF…:7:` an der richtigen DLL, Header `generation=7` → dieselbe Meldung wie stale `:3:` (C3) und wie Target-Mismatch (B1). Drei Ursachen, ein Satz.
3. **`completeness=partial` am exakten Hit.** A2: ein Klassentreffer `^Record$`, Liste vollständig, Header trotzdem `status=partial; completeness=partial`. Session-Qualität ≠ Treffer-Vollständigkeit.
4. **Session-Header bei Miss.** B4/B5: `bodyAvailability=available` obwohl `SYMBOL_NOT_FOUND`. Verfügbarkeit der Dekompilat-Session, nicht des Symbols.
5. **Completeness-Lüge in `get_class_structure` auf beiden Targets.** A3 5/33 und A4 5/316 plus „Daten sind vollständig … kein Read/Grep“.
6. **Generation session-labil.** Dieselbe DLL, ID-Prefix `assembly:2AFC08…AAC35` (SHA offenbar Datei-stabil); `:3:` tot, `:7:` live. Folge-Chats mit kopierter ID failen hart.
7. **Asymmetrisches Leer.** B9 Projekt `^Record$` → Fuzzy-Ähnliche; B10 Assembly `^DataExecutor$` → stummes Leer. Agent glaubt an der DLL fälschlich „Index lückenhaft“, im Projekt „Tippfehler“.
8. **Cursor Auto-review** hat B5 (`get_symbol_body`, Projekt-`T:` an Assembly) einmal blockiert. Das ist nicht der MCP-Server, aber echte Agenten-Reibung genau auf dem Cross-Target-Pfad. Nach Freigabe korrekt `SYMBOL_NOT_FOUND`.

**Kein stilles Mischen (Positivbefund)**

- B1–B12: kein extern-Typ aus dem Projektindex, kein Platform-Typ aus der DLL. Nacktes `T:…Record` am Projekt ist NOT_FOUND, nicht ein zufälliger Platform-`Record`.

**FP / FN vs. Ist**

- FP Ambiguity: Kurzname `Record` (C7) und `DataExecutor` (C8) listen Properties gleichen Namens. Für eine *Klassen*-Navigation False Positives in der Kandidatenmenge; die `T:`-Zeile darin ist korrekt.
- FN Member-Kette: Klassentabelle ohne `M:`/`P:`/`F:`-IDs. Overloads (`DataExecutor.QueryAsync`, extern-`Record`-Methoden) sind aus A3/A4 nicht eindeutig an `get_symbol_body` weiterreichbar.
- FN Vollständigkeit extern: 311 von 316 Membern und ~7137 von 7145 Body-Zeilen in diesem Lauf unsichtbar (gewollt durch Limits) — ohne Offset/Cursor nicht nachziehbar.
- `get_class_structure` Total Lines 7139 vs. `get_symbol_body` 7145 für `Record`: Off-by-small zwischen Tools, nicht erklärt.
- Nested Compiler-Closure `_Closure_0024__496_002D0` in C1/C6: Roslyn-Dekompilat, Inventar-Rauschen.

Fakten, die stimmig wirkten: Partial-`DataExecutor` zwei Dateien / eine `T:`-ID; `Record` eine Dekompilat-Datei; `contentMode` `source` vs. `decompiledProject`; C2 kanonisiert `T:` → `assembly:…:7:T:`.

---

## 7. Token / IDs

- Happy-Path-Paar (A1–A6, kleine Limits): kompakt, für den Agenten tragbar. Token-Risiko sitzt in extern-Defaults (50 Member, 80 Body-Zeilen von 7145), nicht im Cross-Target an sich.
- C9 `includeReferences`: ~2,5k extra Diagnosen, 0 zusätzliche `Record`-Typen, ID unverändert — schlechtes Token/Nutzen-Verhältnis für diese Frage.
- **ID-Stabilität**
  - Projekt-`T:` / `P:` / `M:`: in diesem Lauf unverändert zwischen `find_symbol`, Ambiguity und `get_symbol_body`. Partial-Dateien teilen eine `T:`.
  - Assembly: SHA `2AFC08F102173788D9E46E20F091664C06737E05E5BF6715377BB45C089AAC35` (64 Hex) ≠ Cache-Ordner `d9d577aba8c99199` / `89aefd622e7d4663…` / `generation-a6ca2d4d…`. Generation-Zähler `7` ist Session, nicht Datei-Hash.
  - Innere DocId `T:externde…Record` ist die einzige Form, die am Assembly-Target **ohne** frische Generation funktioniert (C1/C2).
- **Folge-Call:** A2-ID → A4/A6/C5 Roundtrip in **dieser** Session ja. Dieselbe ID am Projekt nein (B1). `:3:` an der DLL nein (C3). `get_class_structure` → `get_symbol_body` für **Member** ohne ID-Spalte: nur über Datei:Zeile (`Record.cs:31` wirkt in der Session, nicht im Projekt) oder selbst gebautes `T:`/`M:`.
- StructuredContent war in der Agenten-Antwort nicht sichtbar; alles über Markdown-`id:` / Ambiguity-`context`.

---

## 8. Roslyn-Wünsche

Alles statisch, kein LLM:

1. **ID-Ziel prüfen vor Generation.** `assembly:`-Prefix am `targetType=project` (und umgekehrt Projekt-DocId am Assembly-Target, sofern Prefix fehlt und Namespace nicht in der Compilation liegt): `INVALID_ARGUMENT` mit Code `TARGET_MISMATCH`, Text „ID gehört zu targetType=assembly / dieser DLL, Request ist project“.
2. **Drei Fehler trennen:** Target ≠ ID-Target; SHA ≠ Session-SHA; Generation ≠ Session-Generation. Hint jeweils: Target wechseln / nacktes `T:` verwenden / `find_symbol` neu.
3. **Nacktes `T:` am Assembly-Target dokumentieren** als generation-tolerante Form; Antwort darf weiter auf `assembly:sha:gen:T:` kanonisieren.
4. Header-`completeness` der **Session** von Treffer-`completeness` trennen (`hitsComplete=true` bei A2). `bodyAvailability` nicht auf Miss-Antworten der Session kleben.
5. `get_class_structure`: Member-DocIds (`T|M|P|F`, am Assembly-Target mit aktuellem Prefix oder zumindest innerer DocId) für den direkten Body-Folgecall.
6. Offset/`continuationToken` für Member > Cap und Body-Fenster — sonst ist `Record` nie vollständig, `DataExecutor` nur zufällig.
7. Leer-`find_symbol`: Fuzzy entweder auf beiden Targets oder auf keinem; Assembly-Miss nicht als Indexlücke verkaufen, wenn der Name im Modul schlicht fehlt.
8. Completeness-Satz „kein Read/Grep“ unterdrücken, sobald Shown < Total.

---

## 9. Phase 3

AiNetLinter read-only `C:\Daten\Entwicklung\Ralf\AiNetLinter`. Je Nicht-ok-Kettenbefund: Pfad + Symbol + Roslyn-Ansatz. Cursor Auto-review (B5) ist kein Server-Code — hier ausgelassen.

### Target-Mismatch als Generationsfehler (B1–B3)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\SymbolGraph\SymbolIdentifierResolver.cs`; `src\AiNetLinter\Mcp\AnalysisSymbolIdentity.cs`
- **Symbol:** `SymbolIdentifierResolver.TryNormalizeAssemblyId`; `StaleAssemblyId`; `AnalysisSymbolIdentity.Matches`
- **Ansatz:** Vor `Matches`: wenn `value` mit `assembly:` beginnt und `expectedIdentity` **null** ist (`targetType=project`), `INVALID_ARGUMENT` mit Code `TARGET_MISMATCH` („ID gehört zu targetType=assembly / dieser DLL, Request ist project“). Hint: Target wechseln oder nacktes `T:` am Assembly-Ziel. Kein Generationswortlaut.

### SHA-Mismatch = Generation (C4 vs. C3)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\SymbolGraph\SymbolIdentifierResolver.cs`; `src\AiNetLinter\Mcp\AnalysisSymbolIdentity.cs`
- **Symbol:** `TryNormalizeAssemblyId` (ein `if`: Parse fehlgeschlagen **oder** `expectedIdentity` null **oder** `!Matches`); `AnalysisSymbolIdentity.Matches` (Hash **und** Generation)
- **Ansatz:** Drei Zweige nach erfolgreichem `TryParse`: (1) `expectedIdentity is null` → Target-Mismatch; (2) Hash ≠ Session-SHA → `ASSEMBLY_HASH_MISMATCH`, Hint nacktes `T:` oder `find_symbol` an **dieser** DLL; (3) Hash ok, Generation ≠ → `STALE_ASSEMBLY_SNAPSHOT` mit der **aktuellen** `assembly:sha:gen:T:` in der Antwort. `Format` unverändert Session-kanonisch.

### `completeness=partial` am exakten Hit (A2)

- **Pfad:** `src\AiNetLinter\Mcp\Assemblies\Analysis\AssemblyAnalysisResponse.cs`; `src\AiNetLinter\Mcp\Assemblies\Analysis\AssemblySessionStatusExtensions.cs`; `src\AiNetLinter\Mcp\AnalysisToolCall.cs`
- **Symbol:** `AssemblyAnalysisResponse.CreateEnriched` / `FormatHeader`; `ResolveEffectiveStatus` (`Complete` + Diagnosen → `Partial`); `AnalysisToolCall` wrapped **jeden** Assembly-Call inkl. 1-Treffer-`find_symbol`
- **Ansatz:** Session-Felder (`status`, `completeness`, `generation`) von Treffer-Completeness trennen: `hitsComplete=true` wenn die Trefferliste unter `maxResults` liegt. Header darf Session-`partial` behalten (Decompiler-Diagnosen), muss aber nicht die Trefferliste als unvollständig verkaufen.

### Session-Header bei Miss: `bodyAvailability=available` + `SYMBOL_NOT_FOUND` (B4/B5)

- **Pfad:** `src\AiNetLinter\Mcp\Assemblies\Analysis\AssemblyAnalysisResponse.cs`; `src\AiNetLinter\Mcp\AnalysisToolCall.cs`; `src\AiNetLinter\Mcp\McpToolResults.cs`
- **Symbol:** `CreateEnriched` hängt `FormatHeader` an **jeden** Textblock inkl. Recoverable-Fehler; `origin.BodyAvailability` kommt aus der Session, nicht aus dem Resolve
- **Ansatz:** Bei `SYMBOL_NOT_FOUND` / `INVALID_ARGUMENT` Envelope ohne `bodyAvailability=available`, oder `bodyAvailability=n/a` plus Satz „Session-Dekompilat, nicht das Symbol“. `Enrich` Fehlercodes erkennen (`LinterErrorCodes.SymbolNotFound`) und Header kürzen.

### Completeness-Lüge `get_class_structure` beide Targets (A3 5/33, A4 5/316)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\FileStructure\GetClassStructureTool.cs`; `src\AiNetLinter\Mcp\McpSufficiencyHints.cs`
- **Symbol:** `GetClassStructureTool.ExecuteAsync` — `McpSufficiencyHints.Append(markdown)` **unbedingt**, obwohl `RenderMarkdown` bereits `[TotalMemberCount … gezeigt]` schreibt
- **Ansatz:** `Append` nur wenn `!payload.Truncated` (wie `GetNamespaceTreeTool`). Truncation-Zeile bleibt die einzige Vollständigkeitsaussage. Gilt projekt- und assembly-seitig, weil dasselbe Tool über `AnalysisToolDispatch` läuft.

### Generation session-labil (`:3:` tot, `:7:` live)

- **Pfad:** `src\AiNetLinter\Mcp\Assemblies\Analysis\AssemblyAnalysisSession.Generation.cs`; `src\AiNetLinter\Mcp\AnalysisSymbolIdentity.cs`
- **Symbol:** `CreateAndInstallGenerationAsync` — `Interlocked.Increment(ref nextGeneration)` je frischer Decompile/Install; `AnalysisSymbolIdentity.Format` schreibt die Zähler in die ID
- **Ansatz:** Lookup über SHA + innere DocId (`T:…`), Generation nur intern/Cache. Öffentliche ID ohne Generation, oder Stale-Fehler liefert sofort die aktuelle volle ID. Nacktes `T:` am Assembly-Target ist bereits generation-tolerant (`TryNormalizeAssemblyId` setzt `isAssemblyId` bei DocComment-Prefix) — das in Tool-Beschreibungen und Ambiguity-Zeilen als Copy-Paste neben `assembly:sha:gen:` ausgeben (`FindSymbolTool.FormatEntry`).

### Asymmetrisches Leer: Fuzzy nur im Projekt (B9 vs. B10)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\SymbolGraph\FindSymbolScanner.cs`; `src\AiNetLinter\Mcp\Tools\SymbolGraph\SymbolNameMatcher.cs`
- **Symbol:** `FindSymbolScanner.AppendMissHintAsync` / `FormatMissMessageAsync` (beide Targets); `SymbolNameMatcher.FindSimilarSymbolNamesAsync` (`SymbolFinder.FindSourceDeclarationsAsync`, Name enthält CamelCase-Wort ≥4 Zeichen, max. 5, nur `SymbolFilter.Type`)
- **Ansatz:** Derselbe Code; Assembly-Miss ist still, weil `DataExecutor` keine Teilwörter in der extern-Compilation trifft, `Record` im Projekt `Recordung*` findet. Bei 0 Suggestions explizit „kein ähnlicher Typname in dieser Compilation / diesem Target“ plus `hitsComplete=true`, damit der Agent keinen Index-Loch-Schluss zieht. Optional Fuzzy auf beiden abschalten oder `includeSimilar=false` Default.

### FN Member-Kette: Klassentabelle ohne `M:`/`P:`/`F:` (A3/A4 → Body)

- **Pfad:** `src\AiNetLinter\Mcp\Tools\FileStructure\GetClassStructureTool.cs`; `src\AiNetLinter\Mcp\Tools\FileStructure\GetClassStructureModels.cs`; `src\AiNetLinter\Mcp\Tools\GetSymbolBodyTool.cs`
- **Symbol:** `CreateMemberEntry` / `ClassStructureMemberEntry` (kein Id); `GetSymbolBodyTool.RenderResolvedSymbol` (akzeptiert `T:`/`M:`/`assembly:…`)
- **Ansatz:** Memberzeile `id: \`M:…\`` bzw. am Assembly-Target `assembly:{sha}:{gen}:M:…` **oder** zumindest innere DocId (generation-tolerant). `ISymbol.GetDocumentationCommentId()` / `DocumentationCommentId.CreateDeclarationId`. Offset/`continuationToken` für `Shown < Total` analog Search — sonst bleibt extern-`Record` (316 Member) hinter `maxMembers` stecken.

### Off-by-small Total Lines 7139 vs. 7145

- **Pfad:** `src\AiNetLinter\Mcp\Tools\FileStructure\GetClassStructureTool.cs`; `src\AiNetLinter\Mcp\Tools\GetSymbolBodyTool.cs` (bzw. `SourceSymbolBodyResolver`)
- **Symbol:** `CollectDeclarationFilesAsync` summiert `DeclaringSyntaxReferences`-LineSpans der Typdeklaration; Body zählt Dekompilat-Dateizeilen
- **Ansatz:** Dieselbe Quelle: bei Assembly `contentMode=decompiledProject` die Dateizeilenzahl des SourceTrees, nicht die Syntax-Span-Summe. Differenz dokumentieren oder eine Zahl.
