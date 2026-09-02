# Agentic Usability & Token-Cost Audit: AiNetLinter MCP-Server

> **Geprüfter & konsolidierter Mängel- und Backlog-Katalog**  
> Dieser Bericht enthält ausschließlich verifizierte architektonische Schwachstellen, Reibungsverluste und Token-Verschwendungen des AiNetLinter MCP-Servers.  
> Alle Befunde wurden gegen den aktuellen Quellcode von `src/AiNetLinter/` verifiziert; technisch unvermeidbare Randbedingungen (z. B. absolute Pfade für Multi-Projekt-Daemons) sowie konzeptionelle Fehlanwendungen wurden bereinigt.

---

## 1. Management Summary & Backlog-Matrix

| ID | Kategorie | Schweregrad | Aufwand | Betroffenes Tool | Kurzbeschreibung |
|---|---|---|---|---|---|
| `[F-01]` | `[Token-Waste & Payload-Bloat]` | P1 | S | `inspect_assembly` | Ungefilterter Aufruf löst 50s Latenz und sofortigen Budget-Blowout durch rekursive Referenz-Sessions aus. |
| `[F-02]` | `[Token-Waste & Payload-Bloat]` | P1 | M | `inspect_assembly` | Starres 8KB-Budget stutzt fast alle Typen auf 0 Member (`Member 0 von X gezeigt`). |
| `[F-03]` | `[API & Parameter]` | P2 | S | `inspect_assembly`, `find_assembly_extensions` | Irreführende `(gekürzt: responseBudget)`-Kennzeichnung bei 100% vollständigen Teillisten (`1 von 1`, `0 von 0`). |
| `[F-04]` | `[Agenten-Sackgasse / Graph-Bruch]` | P1 | M | `get_call_tree` | Silent Call-Tree-Failure bei dekompilierten Assemblies mit irreführendem Sufficiency-Hinweis (`kein zusätzliches Read/Grep nötig`). |
| `[F-05]` | `[Token-Waste & Payload-Bloat]` | P1 | S | `find_symbol` | `includeReferences: true` flutet Output mit bis zu 100 Zeilen interner Memberbudget-Adapterlogs. |
| `[F-06]` | `[Agenten-Sackgasse / Graph-Bruch]` | P1 | S | `find_references`, `get_feature_context`, `get_call_tree`, `find_symbol` | `Datei.cs:Zeile` wirft `AMBIGUOUS_SYMBOL` auf Methodendeklarationen; DocCommentId scheitert an `~ReturnType`; `find_symbol` unterschlägt `id:`. |
| `[F-07]` | `[Fehlende Capability / Falsche Annahme]` | P2 | M | `get_feature_context` | Callers unterschlagen aufrufende Methode; Test-Zuordnung scheitert an zusammengesetzten Klassennamen (`Target*Tests`). |
| `[F-08]` | `[Agenten-Sackgasse / Graph-Bruch]` | P1 | S | `get_file_tree` | Default-Depth-Falle (`treeDepth=2`) maskiert 95% des Projekts bei gleichzeitiger Falschmeldung `[vollstaendig]`. |
| `[F-09]` | `[Token-Waste & Payload-Bloat]` | P2 | S | `find_magic_values` | Ertrinkt in CLI-Optionen und Einzelfunden durch bindestrichbasierte Heuristik und `minOccurrences=1`. |
| `[F-10]` | `[Token-Waste & Payload-Bloat]` | P3 | S | `find_duplicates` | Scope-Default `"all"` wird von Test-Boilerplate dominiert und verdrängt Produktionscode-Klone. |

---

## 2. Detaillierte Mängelberichte

### `[F-01]` Ungefilterter `inspect_assembly`-Aufruf löst 50s Latenz und sofortigen Budget-Blowout durch rekursive Referenz-Sessions aus
- **Kategorie:** `[Token-Waste & Payload-Bloat]`
- **Schweregrad:** P1 (Blocker / massiver Token-Waste & Latenz-Trap)
- **Geschätzter Aufwand:** S (Stunden)
- **Betroffene MCP-Tools & Parameter:** `inspect_assembly` mit Aufruf `{ targetType: "assembly", targetPath: "[LOCAL-01]" }` (ohne expliziten Filter oder mit Default `includeReferences: null`).
- **Symptom & Agentic Friction:** Ein Agent, der eine Assembly erstmals erkundet, setzt typischerweise keinen Typ- oder Member-Filter. In der Tool-Registrierung ist definiert:
  `ExpandAssemblyReferences: includeReferences ?? (string.IsNullOrWhiteSpace(typeName) && string.IsNullOrWhiteSpace(memberName) && ...)`
  Da kein Filter übergeben wurde, evaluierte der Server `ExpandAssemblyReferences = true` und versuchte für 1.300 transitiv gefundene Referenzen Sessions zu öffnen. Der Aufruf blockierte **51 Sekunden** lang. Als Ergebnis erhielt der Agent lediglich 1 von 172 Referenzen und 1 von 1.300 Sessions, während gleichzeitig das Response-Budget gesprengt wurde, sodass bei allen Typen 0 Member angezeigt wurden. Mit explizitem `includeReferences: false` antwortete der Server in unter 1 Sekunde.
- **Token-Impact:** Extrem hohe Latenz (51s) und eine fast leere Antwort (1 Referenz, 0 Member bei allen Typen). Der Agent muss Folgeaufrufe absetzen, um Typdetails zu erfahren.
- **Verifizierte Codestellen (AiNetLinter):**
  - [`src/AiNetLinter/Mcp/Registration/AssemblyAnalysisToolRegistrations.cs:55-58`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Registration/AssemblyAnalysisToolRegistrations.cs#L55-L58): `ExpandAssemblyReferences: includeReferences ?? (string.IsNullOrWhiteSpace(typeName) && ...)` schaltet die Referenzerweiterung bei ungesetzten Filtern automatisch scharf.
  - [`src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisModels.cs:22`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisModels.cs#L22): `IncludeReferenceDetails => IncludeReferences ?? (...)`.
  - [`src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.cs:16-19`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.cs#L16-L19): Limits für Sessions und Payload-Bytes.
- **Konkreter, architektonisch sauberer Lösungsvorschlag:**
  1. Den Default für `includeReferences` in `inspect_assembly` ausnahmslos auf `false` setzen (wie bereits in `find_assembly_extensions`, `find_symbol` und `find_references`).
  2. Selbst bei explizitem `includeReferences: true` dürfen standardmäßig nur die direkten Referenz-Assembly-Identitäten als Metadaten-Liste ausgegeben werden. Das Öffnen transitiver Referenz-Sessions darf nur bei separatem Opt-in (`includeReferenceSessions: true`) und mit striktem Hard-Cap (z. B. max. 5) erfolgen.

---

### `[F-02]` Starres 8KB-Budget in `inspect_assembly` stutzt fast alle Typen auf 0 Member (`Member 0 von X gezeigt`)
- **Kategorie:** `[Token-Waste & Payload-Bloat]`
- **Schweregrad:** P1 (Blocker / Zerstörung der API-Inspektion)
- **Geschätzter Aufwand:** M (1-2 Tage)
- **Betroffene MCP-Tools & Parameter:** `inspect_assembly` (`targetType: "assembly"`, `targetPath: "[LOCAL-01]"` bzw. `"[LOCAL-03]"`).
- **Symptom & Agentic Friction:** Bei der Inspektion realer Assemblies wurden zwar 8 bis 19 öffentliche Typen aufgelistet, bei über 80% der Klassen stand jedoch: `Member 0 von 42 gezeigt (gekürzt: responseBudget)`. Sogar Interfaces mit nur 1 Member (`[LOCAL-03] Interface_A`, 1 Member) oder Enums mit 3 Membern wurden auf 0 Member gekürzt. Der Agent sieht also Klassennamen, kann aber keinerlei Schnittstellen, Methoden oder Properties ablesen.
- **Token-Impact:** Zwingt den Agenten dazu, für jeden einzelnen Typ separate Folgecalls (`get_class_structure` oder `inspect_assembly` mit `typeName`) abzusetzen. Bei 10 Typen entspricht dies einem 10-fachen Roundtrip- und Token-Overhead.
- **Verifizierte Codestellen (AiNetLinter):**
  - [`src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.cs:19`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.cs#L19): `MaxResponseBytes = 8192` (8 KB).
  - [`src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.Budget.cs:120-124`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.Budget.cs#L120-L124): `FitsResponseBudget` verlangt, dass **sowohl** der Markdown-Text **als auch** das JSON-serialisierte `InspectAssemblyPayload`-Objekt `<= 8192 Bytes` sind. Da JSON mit DTO-Feldnamen (`assemblyPath`, `qualifiedName`, `parameters` etc.) viel voluminöser als Markdown ist, überschreitet das JSON bereits bei wenigen Typen 8 KB.
  - [`src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.Budget.cs:153-175`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.Budget.cs#L153-L175): `TryRemoveLastMember` entfernt rückwärts Member von allen Typen `index > 0`, bis `Members.Count == 0` erreicht ist, um das 8KB-Limit einzuhalten. Nur Typ 0 wird geschont.
- **Konkreter, architektonisch sauberer Lösungsvorschlag:**
  1. `MaxResponseBytes` auf einen praxistauglichen Standardwert für MCP-Werkzeuge anheben (z. B. 32 KB oder 48 KB).
  2. Die Trimming-Strategie in `ProjectResponseBudget` überarbeiten: Statt alle Folgetypen auf 0 Member zu rasieren, jedem Typ ein Mindest-Kontingent (z. B. die ersten 3-5 Member/Signaturen) garantieren. Reicht das Budget nicht aus, soll die Typenanzahl (`TotalTypes`) verringert werden (`TryRemoveLastType`), sodass die verbleibenden Typen für den Agenten semantisch nutzbar bleiben, statt leere Hüllen darzustellen.

---

### `[F-03]` Irreführende `(gekürzt: responseBudget)`-Kennzeichnung bei 100% vollständigen Teillisten
- **Kategorie:** `[API & Parameter]`
- **Schweregrad:** P2 (Mittelschwer / Ergonomie-Hürde)
- **Geschätzter Aufwand:** S (Stunden)
- **Betroffene MCP-Tools & Parameter:** `inspect_assembly` (mit `typeName`), `find_assembly_extensions`.
- **Symptom & Agentic Friction:**
  - Ruft ein Agent `inspect_assembly` mit einem spezifischen `typeName` auf, meldet das Tool: `Öffentliche API-Typen: 1 von 1 (gekürzt: responseBudget)`.
  - Ruft er `find_assembly_extensions` auf einer Assembly ohne Extension Methods auf, meldet das Tool: `Assembly-Extensions: 0 von 0 (gekürzt: responseBudget)`.
  Ein Agent interpretiert `(gekürzt: responseBudget)` als Signal, dass Ergebnisse unvollständig sind. Er sucht nach Paginierungsparametern oder weiteren Treffern, obwohl 100% aller vorhandenen Elemente dargestellt wurden.
- **Token-Impact:** Führt zu unnötigen Folgeabfragen und Verwirrung im LLM-Reasoning.
- **Verifizierte Codestellen (AiNetLinter):**
  - [`src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/InspectAssemblyFormatter.cs:121-125`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/InspectAssemblyFormatter.cs#L121-L125): `AppendTypes` formatiert den Header mit `FormatTruncation(payload.Truncated, payload.TruncatedBy)`. `payload.Truncated` ist jedoch ein globales Flag auf Payload-Ebene (das z. B. wahr wird, wenn Diagnosen oder Referenzen beschnitten wurden), weshalb auch eine vollzählige Typenliste als "gekürzt" deklariert wird.
  - [`src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/Responses/FindAssemblyExtensionsResponseBuilder.cs:77`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/Responses/FindAssemblyExtensionsResponseBuilder.cs#L77): `builder.AppendLine($"Assembly-Extensions: {payload.ShownCount} von {payload.TotalExtensions}{(payload.Truncated ? $" (gekürzt: {string.Join(", ", payload.TruncatedBy)})" : string.Empty)}");` prüft ebenfalls nur das globale `payload.Truncated`.
- **Konkreter, architektonisch sauberer Lösungsvorschlag:**
  In `AppendTypes` und `FindAssemblyExtensionsResponseBuilder` darf der Kürzungs-Zusatz nur gerendert werden, wenn die jeweilige Liste tatsächlich unvollständig ist:
  `payload.ShownCount < payload.TotalTypes` bzw. `payload.ShownCount < payload.TotalExtensions`.

---

### `[F-04]` Silent Call-Tree-Failure bei dekompilierten Assemblies mit irreführendem Sufficiency-Hinweis (`kein zusätzliches Read/Grep nötig`)
- **Kategorie:** `[Agenten-Sackgasse / Graph-Bruch]`
- **Schweregrad:** P1 (Blocker / falsche Annahmen)
- **Geschätzter Aufwand:** M (1-2 Tage)
- **Betroffene MCP-Tools & Parameter:** `get_call_tree` (`targetType: "assembly"`, `symbolIdentifier: "[LOCAL-01] Type_A.Method_B"`, `direction: "outgoing"` / `"incoming"`).
- **Symptom & Agentic Friction:** Dekompilierte Assemblies werden standardmäßig im Modus `contentMode=decompiledSignatureOnly` geladen. Da keine Methodenrümpfe im Roslyn-Workspace vorliegen, kann Roslyn keine Methodenaufrufe finden. Ruft der Agent `get_call_tree` auf, liefert das Tool einen leeren Baum (nur die Wurzel). Anstatt den Agenten auf die fehlenden Methodenrümpfe hinzuweisen, schließt die Antwort mit:
  `[HINWEIS]: Diese Daten sind vollstaendig fuer den angefragten Scope — kein zusaetzliches Read/Grep noetig.`
  Dies ist eine Falschinformation: `get_symbol_body` beweist, dass die Methode dutzende andere Methoden aufruft. Der Agent verlässt sich auf den Sufficiency-Hinweis und folgert fehlerhaft, dass keine Abhängigkeiten existieren.
- **Token-Impact:** Schwerwiegende Fehlentscheidungen im Agenten-Plan; anschließende Halluzinationen über Methodenbeziehungen.
- **Verifizierte Codestellen (AiNetLinter):**
  - [`src/AiNetLinter/Mcp/Tools/CallTree/GetCallTreeTool.cs:60-65`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/CallTree/GetCallTreeTool.cs#L60-L65): `ExecuteAsync` ruft bedingungslos `McpSufficiencyHints.Append(body)` auf, wenn `truncated` und `topNTruncated` falsch sind.
  - Im Gegensatz dazu besitzt [`src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblyFindReferencesTool.cs:30`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblyFindReferencesTool.cs#L30) das Flag `SuppressSufficiencyHint: IsSignatureOnly(lease)`, um diesen Fehler in `find_references` zu verhindern.
  - [`src/AiNetLinter/Mcp/McpSufficiencyHints.cs:28-41`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/McpSufficiencyHints.cs#L28-L41): `DecompiledSignatureOnlyLimitation` ist als Hinweistext vorhanden, wird aber in `GetCallTreeTool` nicht eingesetzt.
- **Konkreter, architektonisch sauberer Lösungsvorschlag:**
  In `GetCallTreeTool` prüfen, ob die zugrundeliegende Assembly-Session im Modus `decompiledSignatureOnly` betrieben wird. In diesem Fall darf niemals `CompleteDataHint` ausgegeben werden, sondern stattdessen `McpSufficiencyHints.AppendDecompiledSignatureOnlyLimitation(body)`.

---

### `[F-05]` `find_symbol` mit `includeReferences: true` flutet Output mit bis zu 100 Zeilen interner Memberbudget-Adapterlogs
- **Kategorie:** `[Token-Waste & Payload-Bloat]`
- **Schweregrad:** P1 (Massiver Token-Waste)
- **Geschätzter Aufwand:** S (Stunden)
- **Betroffene MCP-Tools & Parameter:** `find_symbol` (`targetType: "assembly"`, `targetPath: "[LOCAL-01]"`, `namePatterns: ["..."]`, `includeReferences: true`).
- **Symptom & Agentic Friction:** Sucht ein Agent in einer Assembly inklusive Referenzen nach einem Symbol, enthält die Antwort bei fehlenden Treffern über 70 Zeilen mit internen Diagnosen der Form:
  `- Typbaum 'System.Buffers.SearchValues' benötigt 12 von 1 verbleibenden Memberbudgets.`
  `- Typbaum 'Internal.Console' benötigt 6 von 0 verbleibenden Memberbudgets.`
- **Token-Impact:** Verschwendung von ca. 15 KB (~3.500 Tokens) reinem internen Allokationsmüll in einem einzigen Turn, was das Kontextfenster belastet.
- **Verifizierte Codestellen (AiNetLinter):**
  - [`src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblyFindSymbolTool.cs:94-95`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblyFindSymbolTool.cs#L94-L95): `BuildResponseAsync`:
    `foreach (var diagnostic in summary.Diagnostics) markdown.Line($"- {diagnostic}");`
    Hier fehlt jegliches Sampling und jeglicher Schwellwert.
  - [`src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblyNavigationSupport.cs:23`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblyNavigationSupport.cs#L23): `MaxNavigationDiagnostics = 100` erlaubt bis zu 100 ungefilterte Diagnosen.
  - [`src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyDecompilationAdapter.cs:246`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyDecompilationAdapter.cs#L246): Erzeugt diese Low-Level-Budgetwarnungen via `AssemblyDiagnosticCodes.For(...)`.
- **Konkreter, architektonisch sauberer Lösungsvorschlag:**
  1. In `AssemblyFindSymbolTool.BuildResponseAsync` eine strukturierte Diagnose-Zusammenfassung mit Sampling einziehen (z. B. max. 5 repräsentative Samples analog zu `find_references`: `[X Diagnosen gesamt, 5 Samples gezeigt]`).
  2. Interne Adapter-Budget-Diagnosen (`AssemblyDiagnosticCodes.For(...)`) standardmäßig nicht in den agentenseitigen Output einsteuern, sondern nur bei erhöhtem Debug-/Verbose-Level.

---

### `[F-06]` `Datei.cs:Zeile` wirft `AMBIGUOUS_SYMBOL` auf Methodendeklarationen; DocCommentId scheitert an `~ReturnType`; `find_symbol` unterschlägt `id:`
- **Kategorie:** `[Agenten-Sackgasse / Graph-Bruch]`
- **Schweregrad:** P1 (Blocker für Symbol-Navigation)
- **Geschätzter Aufwand:** S (Stunden)
- **Betroffene MCP-Tools & Parameter:** `find_references`, `get_feature_context`, `get_call_tree`, `find_symbol` mit `symbolIdentifier: "src/.../LinterEngine.cs:45"` oder `DocCommentId`.
- **Symptom & Agentic Friction:**
  1. Die Dokumentation empfiehlt das Format `"Datei.cs:Zeile"`. Zeigt die Zeile auf eine Methodendeklaration (`public async Task<IReadOnlyCollection<RuleViolation>> RunAsync(...)`), bricht der Aufruf mit `AMBIGUOUS_SYMBOL` ab, weil auf der Zeile die Methode, der Rückgabetyp (`RuleViolation`) und alle Parameter liegen. Als Hint wird `Datei:Zeile:Spalte` verlangt – eine Spaltennummer (hier: 59), die kein Agent erraten kann (Spalte 51 liefert den Rückgabetyp, Spalte 58 liefert `SYMBOL_NOT_FOUND`).
  2. Übergibt der Agent die standardmäßige Roslyn-DocCommentId (`M:AiNetLinter.Core.LinterEngine.RunAsync(System.String,System.Boolean,System.Int32,System.Threading.CancellationToken)`), meldet das Tool `SYMBOL_NOT_FOUND`. Grund: Der Server generiert intern IDs mit angehängtem Rückgabetyp (`~Task{...}`), vergleicht per exaktem String-Match und schlägt ohne diesen Suffix fehl.
  3. Bei `find_symbol` auf Projekten gibt das Tool im Gegensatz zu Assemblies kein `id:`-Feld mit der kanonischen ID aus. Der Agent hat somit bei überladenen Methoden keinerlei Möglichkeit, das gewünschte Symbol stabil zu referenzieren.
- **Token-Impact:** Vollständige Blockade bei überladenen Methoden; unnötige explorative Umwege.
- **Verifizierte Codestellen (AiNetLinter):**
  - [`src/AiNetLinter/Mcp/Tools/SymbolGraph/FindReferencesTool.cs:256-276`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/SymbolGraph/FindReferencesTool.cs#L256-L276): `ResolveByLineAsync` bricht bei `symbols.Count > 1` sofort ab, ohne Deklarationen gegenüber Referenzen zu priorisieren.
  - [`src/AiNetLinter/Mcp/Tools/SymbolGraph/SymbolIdentifierResolver.cs:94-109`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/SymbolGraph/SymbolIdentifierResolver.cs#L94-L109): `ResolveSymbolsOnLine` sammelt alle Tokens auf der Zeile, deren Symbol Quelltext-Fundstellen hat (also auch Rückgabetypen wie `RuleViolation` und Parametertypen wie `Config`).
  - [`src/AiNetLinter/Mcp/Tools/SymbolGraph/SymbolIdentifierResolver.cs:180-203`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/SymbolGraph/SymbolIdentifierResolver.cs#L180-L203): `FindExactStableIdAsync` vergleicht `declarationId == stableId` strikt und scheitert, wenn der Agent eine Standard-DocCommentId ohne `~ReturnType`-Suffix übergibt.
  - [`src/AiNetLinter/Mcp/Tools/SymbolGraph/FindSymbolTool.cs:157-164`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/SymbolGraph/FindSymbolTool.cs#L157-L164): `FormatEntry` formatiert für Projekt-Symbole nur `Datei:Zeile - Kind: Name`, unterschlägt aber die eindeutige ID.
- **Konkreter, architektonisch sauberer Lösungsvorschlag:**
  1. In `ResolveByLineAsync`: Wenn auf der Zeile ein Member deklariert wird (`MethodDeclarationSyntax`, `PropertyDeclarationSyntax`, `BaseTypeDeclarationSyntax`), muss dessen deklariertes Symbol vor allen auf der Zeile referenzierten Typen und Parametern bevorzugt werden.
  2. In `SymbolIdentifierResolver.TryResolveByStableIdAsync`: Beim Abgleich von `stableId` den optionalen `~ReturnType`-Suffix normalisieren, sodass sowohl Standard-DocCommentIds als auch Server-interne IDs auflösen.
  3. In `FindSymbolTool.FormatEntry`: Auch für Projekt-Symbole stets `id: <DocCommentId>` ausgeben, genau wie es für Assemblies bereits implementiert ist.

---

### `[F-07]` `get_feature_context` Callers unterschlagen aufrufende Methode; Test-Zuordnung scheitert an zusammengesetzten Klassennamen
- **Kategorie:** `[Fehlende Capability / Falsche Annahme]`
- **Schweregrad:** P2 (Mittelschwer)
- **Geschätzter Aufwand:** M (1-2 Tage)
- **Betroffene MCP-Tools & Parameter:** `get_feature_context` (`symbolIdentifier: "AiNetLinter.Core.LinterEngine"`).
- **Symptom & Agentic Friction:**
  1. Abschnitt 3 (*Direkte Aufrufer*) nennt nur Dateinamen und Zeilennummern (z. B. `src/.../PatternCatalog.cs:17 — Aufruf in AiNetLinter`). Es wird nicht angegeben, *welche Methode* in `PatternCatalog` den Aufruf tätigt.
  2. Abschnitt 4 (*Test-Kontext*) fand für `LinterEngine` nur eine einzige Testklasse (`LinterEngineTests.cs`). Vier weitere hochrelevante Testklassen (`LinterEngineSolutionAnalysisTests`, `LinterEngineCacheTests`, `LinterEngineFileSuppressionTests`, `LinterEngineProjectRestoreTests`) wurden ignoriert, weil der Test-Detector nur auf starre Klassennamen (`TargetName + "Tests"`) prüft.
- **Token-Impact:** Unvollständige Test- und Aufruferübersicht; Agent übersieht relevante Tests bei Refactorings und muss `find_references` bemühen.
- **Verifizierte Codestellen (AiNetLinter):**
  - [`src/AiNetLinter/Core/DiffImpactAnalyzer.cs:422-423`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Core/DiffImpactAnalyzer.cs#L422-L423): `FindCallSiteEntriesAsync` setzt in `CallSiteEntry` als `SymbolName` den Namen des *Ziel-Symbols* (`FormatMemberDisplayName(symbol)`) ein, statt die umschließende Methode der Aufrufstelle zu ermitteln.
  - [`src/AiNetLinter/Mcp/Tools/FeatureContext/FeatureContextFormatter.cs:85-88`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/FeatureContext/FeatureContextFormatter.cs#L85-L88): `AppendCallersSection` formatiert nur `call.FilePath`, `call.Line` und `call.ProjectName`.
  - [`src/AiNetLinter/Core/TestDetector.cs:17-23`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Core/TestDetector.cs#L17-L23): `MatchesTestClassName` prüft nur `testClassName.Equals(targetTypeName + affix)` etc., unterstützt aber keine Teilstring-Muster wie `targetTypeName + "*" + affix`.
- **Konkreter, architektonisch sauberer Lösungsvorschlag:**
  1. In `DiffImpactAnalyzer.FindCallSiteEntriesAsync`: Anhand von `location.Location.SourceSpan` über `semanticModel.GetEnclosingSymbol` den Namen des aufrufenden Members ermitteln und in `CallSiteEntry` speichern. In `AppendCallersSection` ausgeben: `- {call.FilePath}:{call.Line} — {call.CallerMethod}() in {call.ProjectName}`.
  2. In `TestDetector.MatchesTestClassName`: Präfix-Suffix-Konvention etablieren: Wenn `testClassName.StartsWith(targetTypeName, StringComparison.OrdinalIgnoreCase)` und die Klasse auf eines der Affixe (`Tests`, `Test`, `Fixture`) endet, als Naming-Match werten.

---

### `[F-08]` `get_file_tree` Default-Depth-Falle (`treeDepth=2`) maskiert 95% des Projekts bei gleichzeitiger Falschmeldung `[vollstaendig]`
- **Kategorie:** `[Agenten-Sackgasse / Graph-Bruch]`
- **Schweregrad:** P1 (Blocker / falsche Annahme)
- **Geschätzter Aufwand:** S (Stunden)
- **Betroffene MCP-Tools & Parameter:** `get_file_tree` (`view: "tree"`, `view: "summary"`, `view: "files"`).
- **Symptom & Agentic Friction:**
  - Standardmäßig nutzt das Tool `treeDepth = 2`. Bei Ausführung im AiNetLinter-Repository gibt das Tool aus:  
    `63 Dateien gescannt, [vollstaendig: 63 Dateien aggregiert]`
    In Wahrheit enthält das Repository **1.048 Dateien** (davon 921 `.cs`-Dateien). Der Agent verlässt sich auf die explizite Aussage `[vollstaendig]` und übersieht 95% der Codebasis (inkl. aller Module unter `src/AiNetLinter/Mcp/...`).
  - In `view: "tree"` wird zuerst eine Verzeichnisstruktur ausgegeben und danach nochmals *alle* Dateien als flache Liste mit `├── ` angehängt, was zu einer unstrukturierten Doppelung führt.
- **Token-Impact:** Irreführende Orientierung; Agent übersieht bestehende Architekturmodule und implementiert redundanten Code.
- **Verifizierte Codestellen (AiNetLinter):**
  - [`src/AiNetLinter/Mcp/Tools/FileStructure/GetFileTreeScanner.cs:139-150`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/FileStructure/GetFileTreeScanner.cs#L139-L150): `BuildTruncationReasons` erfasst nur `maxResults`, `inaccessibleSubtree` und `cancellation`. Dass Verzeichnisse wegen `effectiveDepth` ausgelassen wurden, wird ignoriert.
  - [`src/AiNetLinter/Mcp/Tools/FileStructure/GetFileTreeRenderer.cs:113`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/FileStructure/GetFileTreeRenderer.cs#L113): `AppendCompleteness` stempelt das Ergebnis als `[vollstaendig]` ab, weil `completeness.ScanCompleted` wahr ist.
  - [`src/AiNetLinter/Mcp/Tools/FileStructure/GetFileTreeRenderer.cs:49-59`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/FileStructure/GetFileTreeRenderer.cs#L49-L59): `AppendTree` rendert nach `AppendDirectories` nochmals alle Dateien flach mit `├──`.
- **Konkreter, architektonisch sauberer Lösungsvorschlag:**
  1. `FileTreeAccumulator` bzw. `TreeWalkStats` müssen registrieren, wenn Unterverzeichnisse aufgrund von `depth >= effectiveDepth` nicht betreten wurden. In diesem Fall muss `"maxDepth"` in `TruncatedBy` aufgenommen werden.
  2. In `AppendCompleteness`: Wenn `TruncatedBy` den Eintrag `"maxDepth"` enthält, muss zwingend `[partiell: Tiefe auf X begrenzt, tiefere Ebenen nicht gescannt]` ausgegeben werden – niemals `[vollstaendig]`.
  3. In `AppendTree`: Auf die redundante flache Dateiliste am Ende der Verzeichnisstruktur verzichten oder Dateien hierarchisch in die jeweiligen Verzeichnisknoten einbetten.

---

### `[F-09]` `find_magic_values` ertrinkt in CLI-Optionen und Einzelfunden durch bindestrichbasierte Heuristik und `minOccurrences=1`
- **Kategorie:** `[Token-Waste & Payload-Bloat]`
- **Schweregrad:** P2 (Ergonomie-Hürde / Rauschen)
- **Geschätzter Aufwand:** S (Stunden)
- **Betroffene MCP-Tools & Parameter:** `find_magic_values` (Standardaufruf ohne Filter).
- **Symptom & Agentic Friction:** Bei Standardaufruf liefert das Tool 372 Treffer. Über 30 Treffer entfallen auf legitime CLI-Optionen (`--daemon-start`, `--mcp-server`, `-sar` etc.) in `CliOptionFactory.cs`, die als `constant_candidates` ("Constants.cs (Header-/Identifier-Konstante)") geflaggt werden. Weil `minOccurrences` per Default `1` ist, wird jedes beliebige einmalige Stringliteral als Refactoring-Kandidat vorgeschlagen.
- **Token-Impact:** Der Prompt wird mit trivialen Stringliteralen und False Positives geflutet, was echte Duplikate verdeckt.
- **Verifizierte Codestellen (AiNetLinter):**
  - [`src/AiNetLinter/Mcp/Tools/MagicValues/MagicValuesStringHeuristics.cs:111-123`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/MagicValues/MagicValuesStringHeuristics.cs#L111-L123): `ClassifyHeaderIdentifierCandidate` klassifiziert jeden String mit Bindestrich zwischen 2 und 64 Zeichen als Header-Konstante – einschließlich aller CLI-Optionen mit führendem `-` oder `--`.
  - [`src/AiNetLinter/Mcp/Tools/MagicValues/FindMagicValuesTool.cs:18`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/MagicValues/FindMagicValuesTool.cs#L18): Default `MinOccurrences = 1` in `FindMagicValuesToolArgs`.
- **Konkreter, architektonisch sauberer Lösungsvorschlag:**
  1. CLI-Optionen (Strings, die mit `-` oder `--` beginnen) explizit von der Header-Identifier-Klassifikation ausschließen. Header-Identifier-Heuristiken sollten auf echte HTTP-Header-Muster prüfen (z. B. PascalCase mit Binde- und ohne führenden Bindestrich).
  2. `minOccurrences` standardmäßig auf mindestens `2` anheben (ein einzelnes Stringliteral ist per Definition kein wiederholter "Magic Value").

---

### `[F-10]` `find_duplicates` Scope-Default `"all"` wird von Test-Boilerplate dominiert
- **Kategorie:** `[Token-Waste & Payload-Bloat]`
- **Schweregrad:** P3 (Minor / Ineffizienz)
- **Geschätzter Aufwand:** S (Stunden)
- **Betroffene MCP-Tools & Parameter:** `find_duplicates` (Standardaufruf).
- **Symptom & Agentic Friction:** 18 der ersten 20 Duplikat-Cluster entfallen auf Test-Methoden in `FastTests` und `IntegrationTests`, die testtypische Assert-/Arrange-Muster wiederholen. Echte Code-Duplikate im Produktivcode werden aus den Top-Ergebnissen verdrängt.
- **Token-Impact:** Der Agent muss das Tool erneut mit `scopeType: "production"` aufrufen; der erste Call ist weitgehend verschwendet.
- **Verifizierte Codestellen (AiNetLinter):**
  - [`src/AiNetLinter/Mcp/Registration/DuplicateDetectionToolRegistrations.cs:33`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Registration/DuplicateDetectionToolRegistrations.cs#L33): `string? scopeType = "all"`.
- **Konkreter, architektonisch sauberer Lösungsvorschlag:**
  `scopeType` analog zu `find_dead_code` (`includeTests: false`) und `find_magic_values` (`includeTests: false`) standardmäßig auf `"production"` setzen. Tests sollten nur bei explizitem Opt-in (`scopeType: "all"` oder `scopeType: "tests"`) einbezogen werden.
