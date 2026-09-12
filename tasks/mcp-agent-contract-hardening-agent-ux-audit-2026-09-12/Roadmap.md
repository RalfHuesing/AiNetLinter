# Roadmap & Implementierungsplan: MCP Contract Hardening UX Audit

Dieses Dokument bewertet die Befunde aus dem [Gesamt-Audit](README.md) und den fünf Teilberichten:
- [Gruppe A – Health & Handshake](gruppe-a-health-handshake.md)
- [Gruppe B – Discovery](gruppe-b-discovery.md)
- [Gruppe C – Symbol-Chaining](gruppe-c-symbol-chaining.md)
- [Gruppe D – Fehlerbehandlung](gruppe-d-fehlerbehandlung.md)
- [Gruppe E – Cross-Tool-Konsistenz](gruppe-e-konsistenz.md)

---

## 1. Klassifizierung & Eignungsbeurteilung

Die Befunde wurden anhand der zwei Vorgabekriterien gefiltert:
- **Kriterium (a)**: Ohne weitere Konzeption ein kleiner Bug oder ein fehlendes Feature, das im Kontext und der Intention von AiNetLinter klar sinnvoll ist.
- **Kriterium (b)**: Für ein LLM ohne tiefe Reasoning-Fähigkeiten deterministisch, isoliert und sauber via **Red-Test-First** umsetzbar.

### In Scope für direkte Umsetzung (10 Punkte)

| Nr. | ID / Befund | Schweregrad | Quelle | Begründung für direkte Eignung |
|---|---|---|---|---|
| 1 | **B-01: Non-C#-Routing Schema-Mismatch** | Critical | [Gruppe B](gruppe-b-discovery.md#critical-b-01-index-scope-liefert-kein-direkt-ausführbares-non-c-routing) | `get_index_scope` gibt `fileFilter` aus, `search_pattern` akzeptiert ausschließlich `includePatterns` (`string[]`). Klarer Schema-Bug. |
| 2 | **D-01: Frühvalidierung meldet `isError=false` & fehlt Statusowner** | Critical | [Gruppe D](gruppe-d-fehlerbehandlung.md#critical-d-01-frühe-validierungsfehler-sind-im-mcp-envelope-als-erfolg-markiert) | In `McpArgumentValidationFilter` wird `Recoverable(...)` statt `isError=true` und Contract-v2 `navigation.status` (`operation=error`) geliefert. |
| 3 | **D-02: `get_class_structure` Budgetfehler ohne exakten Retry** | Major | [Gruppe D](gruppe-d-fehlerbehandlung.md#major-d-02-budgetfehler-liefert-keinen-ausführbaren-exakten-retry) | Liefert fälschlich `INVALID_ARGUMENT` statt `RESPONSE_BUDGET_TOO_SMALL` mit `requestedBytes` und `minimumResponseBytes`. |
| 4 | **D-03: Assembly-Fehler spiegelt vollen Zielpfad** | Major | [Gruppe D](gruppe-d-fehlerbehandlung.md#major-d-03-assembly-fehler-spiegelt-den-vollständigen-vertraulichen-zielpfad-zurück) | `inspect_assembly` leakt ungekürzte absolute Dateipfade in `context`. Datensparsamkeit verlangt Maskierung/Sanitisierung. |
| 5 | **C-02 / E-01: `find_symbol` Mindestbudget liefert 0 Treffer** | Major | [Gruppe C](gruppe-c-symbol-chaining.md#major-r1--budget-mindestwert-liefert-eine-leere-erfolgsprojektion) / [Gruppe E](gruppe-e-konsistenz.md#major-e-01-gemeldetes-mindestbudget-führt-zu-semantisch-leerem-symbol-retry) | Budgetprojektion stutzt Matches bis auf 0 herunter und deklariert diese leere Hülle als `minimumResponseBytes`. Mindestbudget muss mindestens 1 Match garantieren. |
| 6 | **C-03: Assembly-`find_symbol` Navigation unvollständig** | Major | [Gruppe C](gruppe-c-symbol-chaining.md#major-r2--contract-v2--assembly-suche-spiegelt-referenz-scope-nicht) | Spiegelt `requestedIncludeReferences`, `effectiveSearchMode` und Assembly-Counts nicht konsistent in `navigation` wider. |
| 7 | **C-04: `get_class_structure` ohne direkte Member-Handoff-ID** | Minor | [Gruppe C](gruppe-c-symbol-chaining.md#minor-chaining--klassenstruktur-enthält-keine-direkt-konsumierbare-member-id) | Member enthalten Name, Zeile, Datei, aber keine `id` / `handoffKind` für `get_symbol_body`. Reine Datenfeld-Erweiterung. |
| 8 | **B-02: `get_file_tree` ignoriert Min-Budget stillschweigend** | Major | [Gruppe B](gruppe-b-discovery.md#major-b-02-get_file_tree-ignoriert-sehr-kleine-response-budgets-stillschweigend) | Hält Wire-Budget nicht ein und liefert bei unpassender Minimalrepräsentation kein `RESPONSE_BUDGET_TOO_SMALL`. |
| 9 | **A-01: Ziel-Health veröffentlicht keine Capability-Entscheidung** | Major | [Gruppe A](gruppe-a-health-handshake.md#major-befund-1-ziel-health-veröffentlicht-keine-capability-entscheidung) | `target.Capabilities` existiert bereits intern im Resolver, wird aber in der Antwort von `get_server_health` nicht strukturiert exponiert. |
| 10 | **A-03: Globaler Health enthält nicht aggregierte Laufzeitdaten** | Major | [Gruppe A](gruppe-a-health-handshake.md#major-befund-3-globaler-health-enthält-nicht-aggregierte-laufzeitidentitäten) | Unnötige Einzelidentitäten (PID, Host, Connection-Details) verletzen Datensparsamkeit. Reduktion auf handlungsrelevante Aggregate. |

---

### Zurückgestellt für separates Konzept / tieferes Reasoning

Die folgenden beiden Befunde sind **nicht** für eine unkonzipierte Ad-hoc-Umsetzung durch ein einfaches LLM geeignet:

1. **C-01 / R2 [Critical]: Referenz-Handoff aus `find_symbol(includeReferences=true)` wird als `TARGET_MISMATCH` abgewiesen**
   - *Grund*: Betrifft den Lebenszyklus und die Verknüpfung von Referenz-Leases über das Root-Target hinweg (`AssemblyAnalysisRegistry`, `AssemblySearchRouting`, `AssemblyAnalysisLease`, Transitive vs. direkte Referenzen, Eviction). Eine Ad-hoc-Änderung birgt hohes Regressionsrisiko bezüglich des Assembly-Session-Managements und sollte nach den klaren Quick-Wins in einem dedizierten Konzept sauber durchdacht werden.
2. **A-02 [Major]: Assembly-Health hat konkurrierende Completeness-Signale**
   - *Grund*: `assembly.completeness=partial` vs. `navigation.status.completeness=truncated`. Dies berührt das Zusammenspiel der Statusowner-Invariante in Contract v2 (Response-Ebene) mit der fachlichen Analyse-Achse. Bedarf einer klaren konzeptionellen Vorgabe der Status-Taxonomie.

---

## 2. Roadmap (Abarbeitungs-Checkliste)

Die Umsetzung erfolgt strikt nach den [AiNetLinter-Richtlinien](../../.agents/rules/AiNetLinterRichtlinien.mdc): **Red-Test-First**, isolierte Commits, volle Gate-Verifikation.

### Paket 1: Schema- & Envelope-Härtung (Critical Fixes)
- [x] **1. B-01**: `get_index_scope` Non-C#-Routing von `fileFilter` auf `includePatterns` (Array) korrigieren
- [ ] **2. D-01**: `McpArgumentValidationFilter` Envelope auf `isError=true` mit Contract-v2 Statusowner umstellen

### Paket 2: Budget- & Recovery-Härtung (R1)
- [ ] **3. D-02**: `get_class_structure` Budgetfehler auf `RESPONSE_BUDGET_TOO_SMALL` mit `minimumResponseBytes` umstellen
- [ ] **4. C-02 / E-01**: `find_symbol` Mindestbudgetprojektion so korrigieren, dass mindestens 1 Match garantiert wird
- [ ] **5. B-02**: `get_file_tree` Budgettreue herstellen und bei Budgetunterschreitung `RESPONSE_BUDGET_TOO_SMALL` liefern

### Paket 3: Datensparsamkeit & Handoff-Ergonomie
- [ ] **6. D-03**: Pfad-Maskierung/Sanitisierung bei Assembly-Fehlern in `inspect_assembly`
- [ ] **7. C-04**: `get_class_structure` Member-Objekte um kanonische Handoff-ID (`id`, `handoffKind`) erweitern

### Paket 4: Assembly- & Health-Konsistenz
- [ ] **8. C-03**: Assembly-`find_symbol` Navigation um `requestedIncludeReferences`, `effectiveSearchMode` und Assembly-Counts ergänzen
- [ ] **9. A-01**: Ziel-Health (`get_server_health`) um maschinenlesbare `capabilities` (Syntax & Lint-Status) erweitern
- [ ] **10. A-03**: Globalen Health-Handshake von vertraulichen/ungefilterten Prozess- und Hostidentitäten bereinigen

---

## 3. Detaillierte Implementierungspläne

---

### Punkt 1 (B-01): `get_index_scope` Non-C#-Routing korrigieren

- **Ziel**: Der von `get_index_scope` für Non-C#-Dateien ausgegebene strukturierte Routing-Vorschlag verweist exakt auf das Schema von `search_pattern`.
- **Betroffene Dateien**:
  - `src/AiNetLinter/Mcp/Tools/FileStructure/GetIndexScopeScanner.cs`
  - `src/AiNetLinter.IntegrationTests/Mcp/Tools/GetIndexScopeToolTests.cs`
  - `src/AiNetLinter.IntegrationTests/Mcp/McpServerCommandJsonRpcFramingTests.cs`
- **Red-Test**:
  - Neuer / angepasster Integrationstest in `GetIndexScopeToolTests.cs`: Prüft, dass `routing.nonCSharp` das Feld `includePatterns` als String-Array enthält (z. B. `["**/*.json"]`), statt eines Strings `fileFilter`. Ein direkter Aufruf von `search_pattern` mit diesen Parametern muss erfolgreich sein.
- **Implementierung**:
  1. In `FileTypeBreakdownEntry` und `IndexScopeRoute` das Feld `FileFilter` durch `IReadOnlyList<string> IncludePatterns` (oder `string[]`) ersetzen (JSON-Property `includePatterns`).
  2. Im Scanner für Nicht-C#-Endungen `[ $"**/*{extension}" ]` setzen.
  3. Text-Rendering in `GetIndexScopeScanner.cs` auf das Format anpassen: `routing=search_pattern(pattern, scopeType=all, includePatterns=[**/*.ext])`.
- **Verifikation**:
  - `dotnet test src/AiNetLinter.IntegrationTests --filter GetIndexScope`

---

### Punkt 2 (D-01): Frühvalidierung im MCP-Envelope auf `isError=true` & Statusowner umstellen

- **Ziel**: Vor der SDK-Parameterbindung abgefangene Fehler (fehlende Pflichtparameter wie `targetPath`, `symbolIdentifier`) liefern `IsError = true` und besitzen einen Contract-v2 `navigation.status`-Knoten mit `operation = "error"`, `completeness = "not_applicable"`, `code = "INVALID_ARGUMENT"`.
- **Betroffene Dateien**:
  - `src/AiNetLinter/Mcp/Registration/McpArgumentValidationFilter.cs`
  - `src/AiNetLinter/Mcp/McpToolResults.cs`
  - `src/AiNetLinter.IntegrationTests/Mcp/Tools/McpServerArgumentValidationE2ETests.cs`
- **Red-Test**:
  - Test in `McpServerArgumentValidationE2ETests.cs`: `find_symbol` ohne `targetPath` und `get_feature_context` ohne `symbolIdentifier` aufrufen. Assertiert: `result.IsError == true`, `structuredContent.status.operation == "error"` bzw. `navigation.status.operation == "error"`.
- **Implementierung**:
  1. In `McpToolResults` eine Methode `ValidationError(string code, string message, McpErrorParameters parameters)` (oder Anpassung von `InvalidArgument`) bereitstellen, die `isError: true` setzt.
  2. Einen leichten `McpNavigationStatus`-Knoten (`operation="error"`, `completeness="not_applicable"`, `code=LinterErrorCodes.InvalidArgument`) in das StructuredContent einbetten, auch wenn kein gültiges Target existiert.
  3. `McpArgumentValidationFilter.ValidateRequiredArguments` und `ValidateArgument` auf die neue v2-konforme Fehlererzeugung umstellen.
- **Verifikation**:
  - `dotnet test src/AiNetLinter.FastTests --filter McpArgumentValidationFilter`
  - `dotnet test src/AiNetLinter.IntegrationTests --filter McpServerArgumentValidationE2E`

---

### Punkt 3 (D-02): `get_class_structure` Budgetfehler auf `RESPONSE_BUDGET_TOO_SMALL` umstellen

- **Ziel**: Wenn `maxResponseBytes` zu klein für den Klassenstruktur-Envelope ist, wird deterministisch `RESPONSE_BUDGET_TOO_SMALL` mit `requestedBytes` und `minimumResponseBytes` zurückgegeben.
- **Betroffene Dateien**:
  - `src/AiNetLinter/Mcp/Tools/FileStructure/GetClassStructureResponseBudget.cs`
  - `src/AiNetLinter.FastTests/Mcp/Tools/FileStructure/GetClassStructureResponseBudgetTests.cs` (oder IntegrationTests)
- **Red-Test**:
  - Test auf `get_class_structure` mit `maxResponseBytes = 512`. Assertiert: `result.IsError == true`, Code ist `RESPONSE_BUDGET_TOO_SMALL`, `structuredContent.minimumResponseBytes > 512`, `structuredContent.requestedBytes == 512`. Ein Retry mit exakt `minimumResponseBytes` gelingt.
- **Implementierung**:
  1. In `GetClassStructureResponseBudget.cs` (Zeilen 23–29) den Aufruf `McpToolResults.InvalidArgument(...)` ersetzen durch:
     ```csharp
     var minimumBytes = CombinedResponseBytes(finalText, finalEnvelope);
     return McpToolResults.Error(
         LinterErrorCodes.ResponseBudgetTooSmall,
         $"maxResponseBytes={maxResponseBytes} ist zu klein für die Klassenstruktur-Mindestprojektion; Mindestwert: {minimumBytes} Bytes.",
         new McpErrorParameters(
             Hint: $"maxResponseBytes auf mindestens {minimumBytes} setzen.",
             FieldPath: "$.maxResponseBytes",
             RequestedBytes: maxResponseBytes,
             MinimumResponseBytes: minimumBytes));
     ```
- **Verifikation**:
  - `dotnet test src/AiNetLinter.FastTests --filter GetClassStructure`

---

### Punkt 4 (D-03): Pfad-Maskierung/Datensparsamkeit bei Assembly-Fehlern

- **Ziel**: `inspect_assembly` und andere Assembly-Fehler spiegeln bei ungültigen Dateien (`INVALID_ASSEMBLY`) nicht den vollständigen absoluten Dateipfad wider, sondern eine bereinigte/datensparsame Form (Dateiname oder anonymisierter Pfad).
- **Betroffene Dateien**:
  - `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupport.cs`
  - `src/AiNetLinter/Mcp/McpToolResults.cs`
  - `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/ManagedAssemblyBinaryTests.cs`
- **Red-Test**:
  - Test mit gefakter nativer PE-Datei oder ungültigem Target: Prüft, dass im Fehlertext `context` nicht der gesamte Kunden-/Arbeitsverzeichnispfad steht, sondern z. B. nur der Dateiname (`Path.GetFileName(fullPath)`).
- **Implementierung**:
  1. In `AssemblyAnalysisToolSupport.cs` beim Aufruf von `McpToolResults.InvalidAssembly(message, fullPath)` den Kontext sanitizen: `Path.GetFileName(fullPath)`.
- **Verifikation**:
  - `dotnet test src/AiNetLinter.FastTests --filter ManagedAssemblyBinary`

---

### Punkt 5 (C-02 / E-01): `find_symbol` Mindestbudgetprojektion korrigieren

- **Ziel**: Ein berechneter `minimumResponseBytes`-Wert in `find_symbol` garantiert Platz für mindestens 1 vollständigen Treffer (sofern die Gesamtzahl > 0 ist). Ein Retry mit diesem Wert liefert niemals `operation=ok` mit 0 Treffern.
- **Betroffene Dateien**:
  - `src/AiNetLinter/Mcp/Tools/SymbolGraph/FindSymbolResponseBudget.cs`
  - `src/AiNetLinter.FastTests/Mcp/Tools/SymbolGraph/FindSymbolResponseBudgetTests.cs`
- **Red-Test**:
  - Test: Suche mit Resultaten bei knappem Budget -> `RESPONSE_BUDGET_TOO_SMALL` mit Mindestwert $M$. Folgeaufruf mit exakt $M$ muss mindestens 1 Treffer (`returnedCount >= 1`) liefern und darf nicht `returnedCount == 0` bei `totalCount > 0` anzeigen.
- **Implementierung**:
  1. In `FindSymbolResponseBudget.cs` sicherstellen, dass die While-Schleife `TryRemoveLastMatch` stoppt, sobald nur noch 1 Match übrig ist (bzw. für den Fall, dass selbst 1 Match nicht ins Budget passt, genau die Größe von 1 Match + Navigation als `minimumResponseBytes` berechnet wird, statt auf 0 Matches zu reduzieren).
  2. Nur wenn `totalCount == 0` war, darf die Mindestprojektion 0 Matches enthalten.
- **Verifikation**:
  - `dotnet test src/AiNetLinter.FastTests --filter FindSymbolResponseBudget`
  - `dotnet test src/AiNetLinter.IntegrationTests --filter FindSymbol`

---

### Punkt 6 (C-03): Assembly-`find_symbol` Navigation vervollständigen

- **Ziel**: `find_symbol` auf ein Assembly-Target weist in `structuredContent.navigation` stets `requestedIncludeReferences`, `effectiveSearchMode` (`"root_only"` bzw. `"bounded_reference_closure"`) und `searchedAssemblyCount` aus – konsistent mit `find_references` und `get_impact`.
- **Betroffene Dateien**:
  - `src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblyFindSymbolTool.cs`
  - `src/AiNetLinter.IntegrationTests/Mcp/Assemblies/Navigation/AssemblyAnalysisRouteTests.cs`
- **Red-Test**:
  - Test in `AssemblyAnalysisRouteTests.cs`: `find_symbol` auf Assembly-Target mit `includeReferences = false` und `true` ausführen. Prüft `navigation.requestedIncludeReferences`, `navigation.effectiveSearchMode` und `navigation.searchedAssemblyCount`.
- **Implementierung**:
  1. Wenn `request.IncludeReferences == false`: Nicht direkt an `FindSymbolTool.ExecuteAsync` delegieren, sondern ein `AssemblyNavigationSummary(false, 1, 1, false, "complete", [])` mit `EffectiveSearchMode = "root_only"` erzeugen.
  2. Im Resultat-Envelope sicherstellen, dass `AssemblyNavigationSummary` unter dem Schlüssel `navigation` gemerged wird.
- **Verifikation**:
  - `dotnet test src/AiNetLinter.IntegrationTests --filter AssemblyAnalysisRouteTests`

---

### Punkt 7 (C-04): `get_class_structure` Member um Handoff-IDs erweitern

- **Ziel**: Jeder Eintrag in `Members` enthält maschinenlesbare Felder `id` (kanonische Handoff-ID) und `handoffKind` (`"symbol"`), damit ein Agent direkt ohne String-Synthese `get_symbol_body` aufrufen kann.
- **Betroffene Dateien**:
  - `src/AiNetLinter/Mcp/Tools/FileStructure/GetClassStructureModels.cs`
  - `src/AiNetLinter/Mcp/Tools/FileStructure/GetClassStructureTool.cs`
  - `src/AiNetLinter.FastTests/Mcp/Tools/FileStructure/GetClassStructureToolTests.cs`
- **Red-Test**:
  - Test in `GetClassStructureToolTests.cs`: Ruft `get_class_structure` auf einen bekannten Typ auf und prüft, dass jedes Member ein nicht-leeres `id` besitzt, das erfolgreich an `get_symbol_body` übergeben werden kann.
- **Implementierung**:
  1. In `ClassStructureMemberEntry` optionale Felder `string? Id = null` und `string? HandoffKind = null` ergänzen.
  2. In `CreateMemberEntry(ISymbol m, ...)` die ID über `DocumentationCommentId.CreateDeclarationId(m)` oder den vorhandenen Identity-Formatter generieren und `handoffKind: "symbol"` setzen.
- **Verifikation**:
  - `dotnet test src/AiNetLinter.FastTests --filter GetClassStructure`

---

### Punkt 8 (B-02): `get_file_tree` Response-Budget durchsetzen

- **Ziel**: `get_file_tree` beachtet `maxResponseBytes`. Reicht das Budget nicht einmal für die minimale Projektion (Minimal-Tree oder Root-Summary), wird `RESPONSE_BUDGET_TOO_SMALL` mit `requestedBytes` und `minimumResponseBytes` geliefert.
- **Betroffene Dateien**:
  - `src/AiNetLinter/Mcp/Tools/FileStructure/GetFileTreeScanner.cs`
  - `src/AiNetLinter/Mcp/Tools/FileStructure/GetFileTreeTool.cs`
  - `src/AiNetLinter.IntegrationTests/Mcp/Tools/GetFileTreeToolTests.cs`
- **Red-Test**:
  - Test mit `get_file_tree(view="summary", maxResponseBytes=1)`. Assertiert: `result.IsError == true`, Code `RESPONSE_BUDGET_TOO_SMALL`, `structuredContent.minimumResponseBytes > 1`.
- **Implementierung**:
  1. Nach dem Kürzen prüfen, ob `SerializedSize(payload) > _input.MaxResponseBytes`.
  2. Falls ja: `McpToolResults.Error(LinterErrorCodes.ResponseBudgetTooSmall, ...)` mit dem berechneten Mindestwert liefern.
- **Verifikation**:
  - `dotnet test src/AiNetLinter.IntegrationTests --filter GetFileTree`

---

### Punkt 9 (A-01): Ziel-Health um maschinenlesbare Capabilities erweitern

- **Ziel**: `get_server_health(targetPath=...)` liefert in `structuredContent` ein `capabilities`-Objekt mit maschinenlesbarem Support- und Konfigurationsstatus für Syntax und Linting.
- **Betroffene Dateien**:
  - `src/AiNetLinter/Mcp/Tools/ServerHealth/GetServerHealthTool.cs`
  - `src/AiNetLinter/Mcp/Tools/ServerHealth/ServerHealthModels.cs`
  - `src/AiNetLinter.IntegrationTests/Mcp/Tools/GetServerHealthToolTests.cs`
- **Red-Test**:
  - Test: Ziel-Health für Source-Ziel (mit/ohne ainetlinter-rules.json) und Assembly-Ziel aufrufen. Assertiert: `structuredContent.capabilities.lint` ist `"supported"`, `"not_configured"` oder `"unsupported"`.
- **Implementierung**:
  1. `AnalysisTarget.Capabilities` aus dem aufgelösten Ziel in das `ServerHealthPayload` übernehmen.
  2. In `structuredContent` als `capabilities: { syntax: "supported", lint: "supported"|"not_configured"|"unsupported" }` serialisieren.
- **Verifikation**:
  - `dotnet test src/AiNetLinter.IntegrationTests --filter GetServerHealth`

---

### Punkt 10 (A-03): Globalen Health-Handshake bereinigen (Datensparsamkeit)

- **Ziel**: Der globale `get_server_health`-Aufruf liefert aggregierte Server- und Session-Metriken, verzichtet jedoch auf rohe Prozess-IDs, Hostnamen, Connection-IDs und unmaskierte Betriebsdaten.
- **Betroffene Dateien**:
  - `src/AiNetLinter/Mcp/Tools/ServerHealth/GetServerHealthTool.cs`
  - `src/AiNetLinter/Mcp/Daemon/DaemonSessionDiagnostic.cs`
  - `src/AiNetLinter.IntegrationTests/Mcp/Tools/GetServerHealthToolTests.cs`
- **Red-Test**:
  - Test auf globalen Health-Aufruf: Assertiert Abwesenheit von PID, internen Thread-IDs oder unverarbeiteten Verbindungs-Tokens im Markdown und StructuredContent.
- **Implementierung**:
  1. Bereinigung des Payload-Modells für globalen Health: Nur Aggregatzahlen (z. B. aktive Sessions, Uptime-Bucket, Serverstatus) erhalten.
- **Verifikation**:
  - `dotnet test src/AiNetLinter.IntegrationTests --filter GetServerHealth`

---

## 4. Getroffene Abstimmungsentscheidungen

Die vorgeschlagenen Entscheidungen wurden vom Nutzer bestätigt:

1. **B-01 Feldname**: Vollständiger Hard-Cut auf `includePatterns: string[]` in `IndexScopeRoute` und `FileTypeBreakdownEntry` (kein Legacy-`fileFilter`).
2. **C-04 Member-ID Format**: `id` mit kanonischer DocCommentId / HandoffId und `handoffKind: "symbol"` in `ClassStructureMemberEntry`.
3. **Ablauf**: Schrittweise Umsetzung der Pakete mit Red-Test-First, Abhaken der Checkliste und automatischen Commits nach Verifikation. Start mit **Paket 1 (Critical Fixes B-01 & D-01)**.
