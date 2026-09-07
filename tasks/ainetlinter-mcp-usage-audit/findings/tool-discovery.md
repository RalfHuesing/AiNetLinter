# Finding: Schema-Entdeckung (`GetDynamicTools`)

Datum: 2026-09-07. Nur `GetDynamicTools` (Katalog, Namespace, Einzelschema, Pattern, Namespace+Pattern, Fehlerpfade). Kein `CallDynamicTool` auf Analyse-Tools, kein Build, kein Test.

Oberfläche: Cursor-Wrapper `GetDynamicTools` über MCP-Namespace `user-AiNetLinter`. Die Schemas/Beschreibungen stammen vom AiNetLinter-Server; Truncation der Katalog-/Pattern-Texte ist Cursor-seitig dokumentiert (200 Zeichen + `... [truncated]`).

---

## 1. Funktion und Schema-Kurzfazit

**Soll:** Agent findet Toolnamen, liest Pflichtfelder und ruft danach `CallDynamicTool` korrekt auf — ohne zu raten.

**Steuert der Lookup den Call richtig?** Teilweise. Der **zweistufige** Weg (Name finden → Einzelschema holen) reicht für `targetType`/`targetPath` und die Parameterliste. Katalog oder Pattern **allein** reichen nicht zum Aufruf.

| Quelle | Was der Agent sieht | Reicht zum ersten Call? |
|---|---|---|
| Katalog (ohne Argumente) | Alle Tool**namen** vollständig; Descriptions auf ~200 Zeichen gekürzt; **kein** `inputSchema` | Nein |
| `namespace=user-AiNetLinter` | Volle Descriptions + volle JSON-Schemas aller Tools + volle Namespace-Beschreibung | Ja, aber teuer |
| `namespace` + `toolName` | Volle Description + volles Schema **eines** Tools | Ja (beste Stufe) |
| `pattern` / `namespace`+`pattern` | Nur Treffer in **Namespace- und Toolnamen** (RE2); Descriptions gekürzt; **kein** Schema | Nein |

**Was Katalog/Pattern abschneiden:** Der `Zielvertrag` (erlaubte `targetType`-Werte, Assembly-Unsupported, Alias-Listen, Caps) steht am **Ende** der Description. Nach 200 Zeichen ist er weg. Beispiel `find_symbol` Katalog: bricht bei `pattern als Strin…` ab — Batch-Cap 10, `kind`, `includeReferences`, StructuredContent, Zielvertrag fehlen.

**JSON-Schema vs. Prosa (auch im Einzelschema):**

- Property-Objekte haben **keine** `description` und **kein** `enum`. `targetType` ist nacktes `"type": "string"` — Werte `project`/`assembly` stehen nur in der Prosa.
- `required` listet bei den meisten Tools nur `targetType`+`targetPath`. Semantisch pflichtige Felder stehen in der Description (`namePatterns`/`pattern` bei `find_symbol`, `symbolIdentifier` bei `get_class_structure`/`find_references`/`get_feature_context`, `pattern` bei `search_pattern`) — JSON sagt optional (`null`-Default).
- Ausnahme mit ehrlichem `required`: `resolve_type_origin` (`targetType`, `targetPath`, `typeName`); `report_observability_feedback` (`feedbackType`, `title`, `description`).
- Assembly-only (`inspect_assembly`, `find_assembly_extensions`, `search_assembly`, `get_assembly_context`): JSON-`required` nur `targetPath`; `targetType` optional/nullable. Passt zur Einzeldescription, **nicht** zur Namespace-Formel „JEDEM zielgebundenen Tool `targetType` **und** `targetPath`“.
- `get_server_health`: kein `required` — passt zur Description (beide oder keines).
- Viele Aliase als Geschwister-Properties (`find_symbol`: `namePatterns`, `namePattern`, `pattern`, `symbol`, `query`, `name`) ohne Schema-Hinweis, dass es **ein** Slot ist.

**Namespace-Beschreibung vs. Einzelschema (Widerspruch):**

Volle Namespace-Description (Namespace-Modus) nennt eine **13er Cross-Target-Matrix** und **3** Assembly-only-Tools. Einzeldescriptions behaupten zusätzlich Assembly-Support für `find_implementations`, `resolve_type_origin`, `get_file_tree`. `get_assembly_context` ist faktisch Assembly-only (`required` nur `targetPath`), fehlt in der Namespace-Liste „Assembly-only: inspect_assembly, find_assembly_extensions, search_assembly“.

Namespace-Text verweist auf `tools/list`; Cursor-Agenten sehen `GetDynamicTools`, nicht natives `tools/list`.

**Ist `targetType`/`targetPath` vor dem ersten Fehlschlag klar?**

- **Ja**, wenn der Agent die volle Namespace-Description oder ein Einzelschema mit `required` liest. Im Katalog ist der Satz „JEDEM zielgebundenen Tool-Aufruf sind targetType und targetPath beizufuegen“ noch in den ersten 200 Zeichen der Namespace-Blurb sichtbar (`targetType='project' … oder target… [truncated]`). Die **Ausnahmen** (Health ohne Ziel, Feedback nicht zielgebunden, Assembly-only ohne `targetType`, 13er-Matrix) liegen hinter der Truncation.
- **Nein**, wenn der Agent nur die gekürzte Tool-Description nimmt: dort fehlt der Zielvertrag. JSON-`required` der Zusatzfelder fehlt auch im vollen Schema — der erste Analyse-Call kann trotzdem mit `INVALID_ARGUMENT` scheitern (`find_symbol` ohne Muster; belegt in `findings/find_symbol.md`, hier nicht erneut aufgerufen).

---

## 2. Ausgeführte Calls

Alle Lookups **schnell**, kein Timeout. Größen = sichtbarer JSON-Text der Tool-Antwort (Cursor schreibt große Payloads zusätzlich in `agent-tools/*.txt`).

| # | Modus / Argumente | Größe (grob) | Truncation / completeness | Wartezeit |
|---|---|---|---|---|
| 1 | Katalog (keine Argumente) | 13,9 KB, 237 Zeilen | Alle langen Descriptions + Namespace-Blurbs `… [truncated]` (~200 Zeichen). Tool**liste** vollständig: 34 Namen in `user-AiNetLinter` (33 Analyse + `mcp_auth`), plus `cursor` und `user-SqlToAi` | schnell |
| 2 | `namespace=user-AiNetLinter` | 77,8 KB, 2271 Zeilen | **Keine** Description-Kürzung; jedes Tool mit vollem `inputSchema`. `namespaceStatus=ready`. Enthält `mcp_auth` | schnell |
| 3 | `namespace` + `toolName=find_symbol` | ~2,5–3 KB, ~90 Zeilen | Volltext + Schema. `required`: nur `targetType`,`targetPath` | schnell |
| 4 | `pattern=find_symbol` (global) | ~0,5 KB | 1 Treffer, Description gekürzt, kein Schema | schnell |
| 5 | `namespace` + `pattern=find_` | ~2 KB | 7 Treffer (`find_assembly_extensions` … `find_symbol`), Descriptions gekürzt, kein Schema | schnell |
| 6 | `namespace` + `toolName=get_server_health` | ~1,5 KB | Voll. Kein `required`. `targetType`/`targetPath` nullable | schnell |
| 7 | `namespace` + `toolName=report_observability_feedback` | ~2 KB | Voll. `required`: `feedbackType`,`title`,`description`. Kein Target | schnell |
| 8 | `namespace` + `toolName=inspect_assembly` | ~4 KB | Voll. `required`: nur `targetPath`. Description: `includeReferences` Default „bei Type-/Member-Filter false, sonst true“; JSON-Default `false` | schnell |
| 9 | `namespace` + `toolName=get_file_tree` | ~4,5 KB | Voll. `required`: `targetType`,`targetPath`. Description erlaubt `assembly` | schnell |
| 10 | `namespace` + `toolName=get_impact` | ~3 KB | Voll. `required` nur Target; `gitRef`/`symbolIdentifier` nur in Prosa „nie beide“ | schnell |
| 11 | `namespace` + `toolName=search_pattern` | ~3,5 KB | Voll. `pattern` nicht in `required`. Zielvertrag: Assembly **unsupported** | schnell |
| 12 | `namespace` + `toolName=reload_config` | ~1 KB | Voll. Target Pflicht; Assembly unsupported | schnell |
| 13 | `namespace` + `toolName=get_assembly_context` | ~3,5 KB | Voll. `required` nur `targetPath` — Assembly-only, fehlt in Namespace-„Assembly-only“-Liste | schnell |
| 14 | `namespace` + `toolName=find_implementations` | ~2 KB | Voll. Description: project **oder** assembly — steht **nicht** in der 13er-Matrix | schnell |
| 15 | `namespace` + `toolName=resolve_type_origin` | ~1,2 KB | Voll. `required` inkl. `typeName`. Description: beide Target-Typen — **nicht** in der 13er-Matrix | schnell |
| 16 | `namespace=does-not-exist` | Fehler, wenige Zeilen | `Error: namespace "does-not-exist" not found. Available namespaces: cursor, user-AiNetLinter, user-SqlToAi` | schnell |
| 17 | `namespace=AiNetLinter` (ohne `user-`) | Fehler, wie #16 | Derselbe Text, listet `user-AiNetLinter` | schnell |
| 18 | `namespace=user-AiNetLinter`, `toolName=does_not_exist` | Fehler, 1 Zeile | `Error: Tool "does_not_exist" not found in namespace "user-AiNetLinter".` — **keine** Namensvorschläge | schnell |
| 19 | `pattern=zzzz_no_such_tool_xyz` | `matches: []` | Kein Error, leere Menge | schnell |
| 20 | `pattern=targetType` und `namespace`+`pattern=targetType` | `matches: []` | Pattern sucht **nur Namen**, nicht Schemafelder/Descriptions. Agent, der „welche Tools haben targetType?“ sucht, sieht Stille | schnell |
| 21 | `pattern=^find_` (RE2) | 7 Treffer, gekürzt | Regex auf Toolnamen funktioniert | schnell |
| 22 | `namespace` + `toolName=find_symbol` + `pattern=find_` | wie #3 | `toolName` gewinnt; Pattern wird still ignoriert (kein Fehler) | schnell |

Inventar im Katalog `user-AiNetLinter` (34): die 33 Analyse-Tools aus dem Konzept plus Cursor-`mcp_auth`. Kein Toolname fehlte.

---

## 3. Verdict

**teilweise** — `GetDynamicTools` liefert in allen dokumentierten Modi reproduzierbare Antworten. Zum korrekten ersten Analyse-Call braucht der Agent das **Einzelschema** (oder den teuren Namespace-Dump). Katalog/Pattern sind nur Namenssuche. JSON-`required`/Enums/Property-Descriptions bleiben hinter der Prosa zurück; die Namespace-Capability-Matrix widerspricht mehreren Einzel-Zielverträgen.

---

## 4. Schwere

**friction**

Nicht `broken`: wer `namespace`+`toolName` vor `CallDynamicTool` holt, kommt zum Ziel. Katalog-Truncation ist in der `GetDynamicTools`-Beschreibung angekündigt. Reibung: unvollständiges JSON-`required`, fehlende Enums, Alias-Wolken, Matrix vs. Einzeltool, leere Pattern-Suche ohne Hinweis „nur Namen“.

---

## 5. Nutzbarkeit

**nur mit Workaround**

Pflichtfolge aus Agentensicht:

1. Namen: Katalog oder `pattern` (RE2 auf Toolnamen).
2. **Immer** danach `GetDynamicTools` mit `namespace=user-AiNetLinter` und `toolName=<tool>` — nicht aus der gekürzten Description raten.
3. `targetType`/`targetPath` aus JSON-`required` **und** Zielvertrag der vollen Description (Assembly-only: oft nur `targetPath`; Health: beides weglassen erlaubt; Feedback: kein Target).
4. Semantische Pflichtfelder (Muster, Symbol-ID, `typeName`) aus der **Prosa** lesen, nicht aus `required`.
5. Namespace-Dump nur, wenn viele Schemas in einem Turn nötig sind (78 KB).

---

## 6. Bugs (reproduzierbar)

False Positives/Negatives hier = Schema/Beschreibung vs. das, was `GetDynamicTools` selbst liefert (kein Abgleich gegen Platform-Quelltext; keine Analyse-Calls).

1. **Capability-Matrix unvollständig (Namespace vs. Einzelschema).** Call #2 vs. #14/#15/#9/#13. 13er-Liste und „Assembly-only: 3 Tools“ decken `find_implementations`, `resolve_type_origin`, `get_file_tree`, `get_assembly_context` nicht ab. Agent, der der Namespace-Blurb folgt, lässt gültige Assembly-Calls weg oder setzt fälschlich `targetType=project` auf Assembly-only-Tools.
2. **JSON-`required` lügt bei Zusatzfeldern.** Call #3: `find_symbol` ohne Muster ist schema-valide, Laufzeit (andere Finding-Datei) verlangt `namePatterns`. Gleiches Muster bei `search_pattern.pattern`, `get_class_structure.symbolIdentifier`, `find_references.symbolIdentifier`. Gegenbeispiel Call #15: `typeName` ist korrekt in `required`.
3. **Kein `enum` / keine Property-`description`.** Call #3/#9/#10: `targetType` nacktes String. Werte und „nie beide“-Regeln nur in Prosa. `kind` bei `find_symbol` verspricht „deutsche und englische Werte“ im Text, JSON gibt keine Werteliste.
4. **`inspect_assembly.includeReferences` Default widerspricht sich.** Call #8: Description „Default bei Type-/Member-Filter false, sonst true“; JSON `"default": false`.
5. **Pattern-Suche über Schemafelder ist stumm.** Call #20: `targetType` → `[]`. Die `GetDynamicTools`-Doku sagt „namespace and tool names“ — für einen Agenten, der die Doku nicht wörtlich nimmt, wirkt das wie ein Fehlschlag.
6. **Unbekanntes `toolName` ohne Hilfsliste.** Call #18 nennt den Namespace, nicht die 34 Toolnamen. Unbekannter Namespace (Call #16/#17) listet Namespaces — asymmetrischer Fehlerpfad, aber brauchbar.
7. **Leere Pattern-Menge ist kein Error.** Call #19: `matches: []`. Verwechselbar mit „Suche kaputt“, im Gegensatz zu #16.
8. **Katalog blendet Ausnahmen weg.** Call #1: Pflicht-Satz für zielgebundene Tools sichtbar; Health/Feedback/Assembly-Inferenz hinter `… [truncated]`. Agent mit nur Katalog setzt `targetType` auch auf `get_server_health`/`report_observability_feedback` oder vergisst optionales `targetType` bei Assembly-only.
9. **`mcp_auth` im Inventar.** Call #1/#2: Cursor-Wrapper zwischen den Analyse-Tools. Konzept schließt ihn aus; ein Agent kann ihn für ein AiNetLinter-Analyse-Tool halten.
10. **`toolName` + `pattern` still.** Call #22: Pattern wird verworfen, Einzelschema zurück. Kein Hinweis auf ignoriertes Argument.

Kein Listen-Truncation der 34 Toolnamen beobachtet. Kein Timeout.

---

## 7. Token-Effizienz / Folge-Call-Tauglichkeit

| Artefakt | Zeichen/Zeilen | Folge-Call |
|---|---|---|
| Katalog | 13,9 KB / 237 Zeilen (3 Namespaces) | Liefert **Namen**. Keine IDs, kein Schema. Nächster Schritt: Einzelschema. |
| Pattern-Treffer | wenige hundert Zeichen bis ~2 KB | Wie Katalog, gefiltert. `tool` + `namespace` sind stabile Schlüssel für den Follow-up. |
| Einzelschema | ca. 1–5 KB / 40–130 Zeilen | Direkt `CallDynamicTool`-tauglich. Keine Symbol-IDs (noch kein Analyse-Call). |
| Namespace-Dump | 77,8 KB / 2271 Zeilen | ~6× Katalog, alle 34 Schemas. Für **ein** Tool Verschwendung; für Batch-Planung ok. |

Truncation ist bei Katalog/Pattern **markiert** (`… [truncated]`), bei Namespace/Einzelschema nicht nötig. Es gibt keinen Continuation-Token für Schemas — unnötig, weil Einzelschema klein bleibt.

StructuredContent der Analyse-Tools ist in dieser Discovery-Schicht nicht sichtbar (kein Analyse-Call). Stabile Follow-up-Keys: `namespace` + `tool` (String).

**Agenten-Regel, die sich aus den Größen ergibt:** Nicht den 78-KB-Dump ziehen, wenn ein 3-KB-Einzelschema reicht. Nicht aus 200-Zeichen-Stummeln parameterisieren.

---

## 8. Roslyn-konforme Wünsche

Kein LLM hinter den Tools. Alles deterministisch in MCP-Registration/JSON-Schema/Namespace-Text (AiNetLinter) bzw. Katalog-Truncation (Cursor):

1. JSON-Schema: `targetType` als `enum: ["project","assembly"]`; Property-`description` je Feld; semantische Pflicht als `required` oder `oneOf` über Alias-Gruppen (`pattern` \| `namePattern` \| `namePatterns` \| …).
2. Namespace-`namespaceUseInstructions` / Capability-Matrix **aus denselben Zielverträgen** erzeugen wie die Einzeltools (keine 13er-Liste, die `find_implementations`/`resolve_type_origin`/`get_file_tree`/`get_assembly_context` vergisst).
3. Defaults in Description und JSON identisch halten (`inspect_assembly.includeReferences`).
4. `GetDynamicTools`-Pattern: in der Antwort einer leeren Suche einen Satz „nur Namespace-/Toolnamen, nicht Schema/Description“; bei unbekanntem `toolName` Fuzzy-Vorschläge wie beim unbekannten Namespace.
5. Katalog: statt nur 200 Zeichen Prosa die JSON-`required` plus erlaubte `targetType`-Werte maschinenlesbar mitliefern (Cursor-seitig oder als kurzer Schema-Stub). Dann überlebt der Zielvertrag die Truncation.
6. `mcp_auth` in der Analyse-Liste kennzeichnen oder aus dem AiNetLinter-Toolset filtern, damit Agenten es nicht als Roslyn-Tool behandeln.

---

## 9. Phase 3

Welle 3: AiNetLinter-Pfad/Symbol + Ansatz (read-only `C:\Daten\Entwicklung\Ralf\AiNetLinter`). Discovery-Schemas sind MCP-SDK-Reflexion über `McpServerTool.Create`-Lambdas, kein Roslyn-Symbolgraph. Cursor-Katalog (`GetDynamicTools` 200-Zeichen-Truncation, Pattern nur auf Namen, `mcp_auth`) liegt **nicht** in diesem Repo; Gegenmittel unten nur dort, wo AiNetLinter den Wire-Text liefert.

### Capability-Matrix unvollständig (Namespace vs. Einzelschema)

- **Pfad:** `src\AiNetLinter\Mcp\ServerInstructions.cs`; verdrahtet über `src\AiNetLinter\Mcp\McpServerOptionsFactory.cs`; Test zementiert die Lücke: `src\AiNetLinter.FastTests\Mcp\Wiring\WiringProjectContractTests.cs`. Einzelverträge: `src\AiNetLinter\Mcp\Tools\McpToolRegistrationOptions.cs`; `src\AiNetLinter\Mcp\Registration\SymbolGraphToolRegistrations.cs`; `src\AiNetLinter\Mcp\Registration\FileStructureToolRegistrations.cs`; `src\AiNetLinter\Mcp\Registration\AssemblyAnalysisToolRegistrations.cs`
- **Symbol:** `ServerInstructions.Text` (hartcodierte „13 Cross-Target-Tools“ + „Assembly-only: inspect_assembly, find_assembly_extensions, search_assembly“); `McpServerOptionsFactory.Create` → `WithServerInstructions`; `WiringProjectContractTests.ServerInstructions_TextStaysWithinBudgetAndCarriesContract`. Dagegen `TargetedReadOnlyTool` an `AddFindImplementations` / `AddResolveTypeOrigin` / `AddGetFileTree`; `AssemblyTool` an `AddGetAssemblyContext` (vierte Assembly-only, in `WiringToolCollectionContractTests` bereits so geführt)
- **Roslyn-Ansatz:** Matrix aus denselben Registrierungsoptionen erzeugen (`ReadOnlyTool` / `TargetedReadOnlyTool` / `AssemblyTool` / `ServerHealthTool` / `FeedbackTool`), nicht als Literalliste pflegen. `get_file_tree`/`find_implementations`/`resolve_type_origin` in die Cross-Target-Liste; `get_assembly_context` in Assembly-only. Budget `ServerInstructions.MaxUtf8Bytes` (2557) bleibt — Namen statt Prosa.

### JSON-`required` lügt bei Zusatzfeldern; kein `enum` / keine Property-`description`; Alias-Wolken

- **Pfad:** jeweilige `*ToolRegistrations.cs` unter `src\AiNetLinter\Mcp\Registration\`; Optionen `McpToolRegistrationOptions.cs` (nur Description-Suffix, kein JSON-Overlay)
- **Symbol:** z. B. `SymbolGraphToolRegistrations.AddFindSymbol` (`string[]? namePatterns = null` plus Aliase `namePattern`/`pattern`/`symbol`/`query`/`name` — alle optional; `string targetType` ohne Enum); `AddFindReferences` / `FileStructureToolRegistrations.AddGetClassStructure` (`string? symbolIdentifier = null`); `AnalysisToolRegistrations` `search_pattern` (`pattern` optional). Gegenbeispiel Pflicht: `AddResolveTypeOrigin` mit nicht-optionalem `typeName`
- **Roslyn-Ansatz:** nicht Semantik, Schema-Export. SDK baut `inputSchema` aus Lambda-Parametern. Ansatz: semantische Pflicht als non-nullable oder `oneOf` über eine Alias-Gruppe (ein Slot); `targetType` als Enum `project`/`assembly` (Assembly-only: nur `assembly` oder optional + Inferenz wie `AssemblyAnalysisToolRegistrations.ResolveTargetType`); `[Description]` (oder SDK-Äquivalent) je Parameter, damit Werte/`kind`-Listen nicht nur in Prosa stehen.

### `inspect_assembly.includeReferences` Default widerspricht sich

- **Pfad:** `src\AiNetLinter\Mcp\Registration\AssemblyAnalysisToolRegistrations.cs`
- **Symbol:** `AddInspectAssembly` (`bool includeReferences = false` → JSON-Default false); `InspectAssemblyDescription` („Default: bei Type-/Member-Filter false, sonst true“)
- **Roslyn-Ansatz:** Default im Lambda an die Description angleichen (Filter vorhanden → false, sonst true **im Handler**, JSON-Default weglassen oder ehrlich `false` plus Description-Korrektur). Kein zweites Default-System.

### Katalog blendet Ausnahmen weg (Health/Feedback/Assembly-Inferenz hinter Truncation)

- **Pfad:** `src\AiNetLinter\Mcp\ServerInstructions.cs` (Text, den Cursor als Namespace-Blurb kürzt); Zielvertrag am **Ende** der Tool-Descriptions: `McpToolRegistrationOptions.ProjectTargetContract` / `ReadOnlyTargetContract` / `AssemblyTargetContract`
- **Symbol:** `ServerInstructions.Text` (Pflichtsatz `targetType`/`targetPath` steht vorn, Ausnahmen Health/Feedback in Satz 2, Matrix später); `McpToolRegistrationOptions.Create` hängt den Zielvertrag an die Description
- **Roslyn-Ansatz:** nicht anwendbar. Cursor kürzt auf ~200 Zeichen (nicht in diesem Repo). Gegenmittel in AiNetLinter: erlaubte `targetType`-Werte und `required` **im JSON-Schema** (überlebt Truncation); Description-Zielvertrag nach vorn oder weglassen, wenn Schema ihn trägt.

### Pattern auf Schemafeldern stumm; unbekanntes `toolName` ohne Liste; leere Pattern-Menge; `toolName`+`pattern` still; `mcp_auth`

- **Pfad:** nicht in `src\AiNetLinter\Mcp\` — Cursor-`GetDynamicTools` / Namespace-Wrapper
- **Symbol:** —
- **Roslyn-Ansatz:** nicht anwendbar. Kein Patch im AiNetLinter-Handler. Optionaler Hinweis in `ServerInstructions.Text`: Pattern sucht nur Toolnamen; Schemas über `tools/list` bzw. Einzelschema. `mcp_auth` kommt nicht aus `McpServerOptionsFactory`-Tool-Collection (33 Analyse-Tools + Resources).
