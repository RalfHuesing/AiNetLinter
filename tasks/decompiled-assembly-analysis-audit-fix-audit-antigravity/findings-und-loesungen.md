# AiNetLinter MCP-Server — Findings & Lösungsvorschläge

> **Fokus:** Ausschließlich identifizierte Probleme und konkrete Lösungsvorschläge mit Code-Verweisen, sortiert nach der **Wirkung und Hebelkraft für LLM-Agenten** (Token-Ersparnis, Kontext-Schonung, Fehlverhaltensprävention, funktionale Korrektheit).

---

## Rang 1: Konfigurierter Git-Source-Flow fällt trotz erfolgreichem Checkout auf Decompilation zurück

### Problem & Auswirkung auf LLM-Agenten
- **Befund-ID:** `EXTSRC-001`
- **Schweregrad / Dringlichkeit:** `S1` / `P1`
- **Symptom:** Für in `external-sources.json` gemappte Assemblys (z. B. Repository-Bereitstellung) stößt der Server bei Anfragen (`inspect_assembly`, `get_server_health`) erfolgreich den Git-Download an (liegt verifiziert im Cache-Ordner vor). Trotzdem meldet der MCP-Server in allen Antworten:
  `[ASSEMBLY] ... origin=decompiled; sourcePath=none; snapshot=none; confidence=medium; trust=untrusted; status=partial; completeness=partial`
- **Agentischer Schaden:** Der LLM-Agent analysiert dekompilierte Stubs und synthetischen C#-Code mit `untrusted`-Status, obwohl der echte Original-Quelltext im lokalen Git-Checkout physisch bereitliegt. Dadurch fehlen echte Kommentare, interne Typen, Methodenkörper und Attestierungen.

### Betroffener Code
- [src/AiNetLinter/Mcp/Assemblies/ExternalSource/Repository/ExternalSourceSnapshotMaterializer.cs:138-149](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/ExternalSource/Repository/ExternalSourceSnapshotMaterializer.cs#L138-L149)
  ```csharp
  var workspaceFailed = 0;
  workspace.RegisterWorkspaceFailedHandler(_ => Interlocked.Exchange(ref workspaceFailed, 1));
  var solution = await workspace.OpenSolutionAsync(checkout.SolutionPath, cancellationToken);
  if (Volatile.Read(ref workspaceFailed) != 0)
  {
      throw new ExternalSourceSnapshotMaterializationException(
          checkoutTrust: ExternalSourceCheckoutTrust.Clean,
          WorkspaceDiagnosticFailureReason);
  }
  ```
  *(Die strikte Zero-Tolerance-Prüfung verwirft das gesamte Solution-Snapshot, sobald `MSBuildWorkspace` beim Laden der Solution eine Diagnose meldet — was bei älteren Framework- oder externen Solutions ohne explizite SDKs im .NET 9 Host unvermeidlich auftritt).*
- [src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblySourceSelectionOrchestrator.cs:100-108](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblySourceSelectionOrchestrator.cs#L100-L108)
  - Fällt bei `providerResult.IsAvailable == false` lautlos auf `AssemblyDecompilationAdapter` (`origin=decompiled`) zurück, ohne den Grund (z. B. Solution-Ladefehler) im Header oder Text auszuweisen.

### Lösungsvorschlag
1. **Tolerante Materialisierung:** `workspaceFailed` nicht als harten Abbruch werten, wenn `solution.Projects.Any(p => p.Documents.Any())` gültige C#-Dokumente enthält.
2. **AdhocWorkspace Fallback:** Falls `MSBuildWorkspace` an der `.sln` scheitert, einen `AdhocWorkspace` direkt aus den `.cs`-Dateien des gemappten Quellordners aufbauen, um `origin=source-backed` zu garantieren.
3. **Transparenz:** Materialisierungsfehler in `AssemblyContext.Diagnostics` aufnehmen, damit der Agent sieht, warum auf Decompilation ausgewichen wurde.

---

## Rang 2: Referenz-Listen-Bloat bei Typfiltern in `inspect_assembly`

### Problem & Auswirkung auf LLM-Agenten
- **Befund-ID:** `TOK-001` / `ASM-001`
- **Schweregrad / Dringlichkeit:** `S1` / `P1`
- **Symptom:** Wenn ein Agent gezielt nach einem einzelnen Typen oder Member sucht (z. B. `typeName="ArtikelDisposition"`, `exactTypeName=true`), sendet `inspect_assembly` die vollständige 32-teilige Referenzliste und 32 Referenz-Sessions inklusive ausführlicher CS-Fehlermeldungen und Diagnosetexte im Fließtext mit (~18,4 KB / ~4.500 Tokens).
- **Agentischer Schaden:** Der tatsächlich angeforderte Typ belegt nur ~300 Bytes (< 2% der Antwort). Über 98% der Payload sind redundanter Ballast. Bei 10 sequentiellen Typabfragen derselben Assembly werden ~45.000 Tokens unnötig verbraucht und das LLM-Kontextfenster überflutet.

### Betroffener Code
- [src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/InspectAssemblyFormatter.cs:20-24](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/InspectAssemblyFormatter.cs#L20-L24)
  - `AppendReferences` und `AppendReferenceSessions` werden im Text-Formatter unabhängig von vorhandenen Typ-/Member-Filtern immer voll expandiert.
- [src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/InspectAssemblyTool.cs:60-100](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/InspectAssemblyTool.cs#L60-L100)
  - `InspectAssemblyTool.ExecuteAsync` übergibt die Argumente nicht zur selektiven Kompaktierung an `InspectAssemblyFormatter.FormatText`.

### Lösungsvorschlag
1. In `InspectAssemblyFormatter.FormatText` prüfen, ob ein Filter aktiv ist:
   ```csharp
   var hasSpecificFilter = !string.IsNullOrEmpty(arguments.TypeName) || !string.IsNullOrEmpty(arguments.MemberName);
   ```
2. Wenn `hasSpecificFilter == true` und nicht explizit `includeReferences == true` angefordert wurde, die Ausgabe von `Referenzen` und `Referenz-Sessions` auf eine einzeilige Zusammenfassung reduzieren (analog zu `find_assembly_extensions`):
   ```text
   Referenzen: 32 von 250 (gekürzt)
   Referenz-Sessions: 32 von 7675 (gekürzt)
   ```
3. **Ergebnis:** Reduziert die Payload gefilterter Typabfragen von ~18,4 KB auf ~0,8 KB (Token-Einsparung: **~90% / ~4.000 Tokens pro Aufruf**).

---

## Rang 3: Ungedeckelter Diagnose-Dump bei `find_references(includeReferences=true)` & `get_call_tree`

### Problem & Auswirkung auf LLM-Agenten
- **Befund-ID:** `TOK-002` / `NAV-001`
- **Schweregrad / Dringlichkeit:** `S1` / `P1`
- **Symptom:** Wird `find_references` oder `get_call_tree` auf einer Assembly mit `includeReferences=true` aufgerufen, gibt der Text-Formatter sämtliche aufgetretenen Facade-/Versionsabweichungen (`Kein identitätsgleicher Kandidat für 'System.Collections' ...`) zeilenweise im Textoutput aus.
- **Agentischer Schaden:** Eine einzelne Referenzabfrage erzeugt 103 Zeilen Textausgabe mit 24.815 Bytes (~6.000 Tokens), selbst wenn **0 Aufrufstellen** gefunden wurden. Dies verstopft das Kontextfenster des Agenten massiv mit Framework-Versionsmeldungen.

### Betroffener Code
- [src/AiNetLinter/Mcp/Tools/SymbolGraph/TransitiveCallGraphFormatter.cs:61-64](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/SymbolGraph/TransitiveCallGraphFormatter.cs#L61-L64)
  ```csharp
  if (completeness.Diagnostics is { Count: > 0 })
  {
      lines.AddRange(completeness.Diagnostics.Select(diagnostic => $"[Assembly-Diagnostic] {diagnostic}"));
  }
  ```
  *(Fügt alle Diagnosen unbegrenzt in die Textzeilen ein).*
- [src/AiNetLinter/Mcp/Tools/CallTree/AssemblyGetCallTreeTool.cs:114-119](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/CallTree/AssemblyGetCallTreeTool.cs#L114-L119)
  ```csharp
  Diagnostics = navigation.Diagnostics.Concat(diagnostics).Distinct().Take(100).ToList();
  ```
  *(Deckelt erst bei 100 Diagnosen im Fließtext).*

### Lösungsvorschlag
1. Analog zu `get_server_health` und `AssemblyAnalysisResponseLimits.AppendDiagnostics` ein striktes Limit für Text-Diagnosen einführen (z. B. max. 3–5 Zeilen):
   ```csharp
   var shownDiagnostics = completeness.Diagnostics.Take(5).ToList();
   foreach (var diagnostic in shownDiagnostics)
   {
       lines.Add($"[Assembly-Diagnostic] {diagnostic}");
   }
   if (completeness.Diagnostics.Count > 5)
   {
       lines.Add($"[Diagnosen: 5 von {completeness.Diagnostics.Count} gezeigt (gekürzt)]");
   }
   ```
2. Alle vollständigen Diagnosedaten bleiben weiterhin im `structuredContent` JSON-Payload verfügbar, ohne den Fließtext zu fluten.
3. **Ergebnis:** Reduziert die Text-Payload von ~25 KB auf ~1,2 KB (Token-Einsparung: **~95% / ~5.500 Tokens pro Aufruf**).

---

## Rang 4: Fehlende syntaktische `receiverType`-Vorfilterung in `find_assembly_extensions`

### Problem & Auswirkung auf LLM-Agenten
- **Befund-ID:** `ASM-002`
- **Schweregrad / Dringlichkeit:** `S2` / `P2`
- **Symptom:** Bei Standalone-Assembly-Aufrufen (ohne Consumer-Projekt) liefert Roslyn für alle Extension-Methoden `not_decidable`. Übergibt der Agent `receiverType="SqlConnection"`, ignoriert das Tool diesen Filter und gibt sämtliche Extensions der DLL zurück (selbst jene für völlig fremde Receiver wie `GenericDevice`).
- **Agentischer Schaden:** Der Agent erhält semantischen Noise und falsche Kandidaten, die er manuell herausfiltern muss.

### Betroffener Code
- [src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/FindAssemblyExtensionsTool.cs:62-64](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/FindAssemblyExtensionsTool.cs#L62-L64)
  - `arguments.ReceiverType` wird nicht an `AssemblyExtensionSearchOptions` übergeben.
- [src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisService.cs:102-120](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisService.cs#L102-L120)
  - `FindExtensions` filtert nur nach `NamespaceFilter` und `ExtensionName`. Ein syntaktischer Abgleich auf `receiverType` fehlt komplett, wenn `context.Receiver == null` ist.

### Lösungsvorschlag
1. `AssemblyExtensionSearchOptions` um `string? ReceiverType` erweitern.
2. In `AssemblyAnalysisService.FindExtensions` einen Fallback-Filter integrieren:
   ```csharp
   .Where(pair => string.IsNullOrEmpty(options.ReceiverType) ||
                  (pair.Method.Parameters.Length > 0 &&
                   (pair.Method.Parameters[0].Type.Name.Equals(options.ReceiverType, StringComparison.OrdinalIgnoreCase) ||
                    pair.Method.Parameters[0].Type.ToDisplayString().EndsWith(options.ReceiverType, StringComparison.OrdinalIgnoreCase))))
   ```
3. **Ergebnis:** Der Agent erhält bei Angabe von `receiverType` nur Methoden, deren erster Parameter tatsächlich dem gesuchten Typnamen entspricht.

---

## Rang 5: Veraltetes JSON-RPC Tool-Call-Beispiel in `Docs/agent-api.md`

### Problem & Auswirkung auf LLM-Agenten
- **Befund-ID:** `DOC-001`
- **Schweregrad / Dringlichkeit:** `S2` / `P2`
- **Symptom:** In `Docs/agent-api.md` (Zeilen 731–742) zeigt das JSON-RPC-Beispiel für `find_symbol` nur `namePatterns` und `maxResults`. Die obligatorischen Parameter `targetType` und `targetPath` fehlen.
- **Agentischer Schaden:** Wenn ein LLM-Agent (z. B. bei MCP-Client-Implementierung oder Custom Tool Integration) dieses Snippet aus der Dokumentation kopiert, schlägt der Aufruf sofort mit `INVALID_ARGUMENT: Der Parameter 'targetType' ist erforderlich` fehl.

### Betroffener Code
- [Docs/agent-api.md:731-743](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/Docs/agent-api.md#L731-L743)

### Lösungsvorschlag
Das Dokumentations-Snippet aktualisieren:
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "find_symbol",
    "arguments": {
      "targetType": "project",
      "targetPath": "C:/Daten/MeinProjekt",
      "namePatterns": ["LinterEngine"],
      "maxResults": 5
    }
  }
}
```

---

## Rang 6: Diskrepanz beim `maxResults`-Default in `get_file_tree`

### Problem & Auswirkung auf LLM-Agenten
- **Befund-ID:** `DOC-002` / `DISCO-001`
- **Schweregrad / Dringlichkeit:** `S3` / `P3`
- **Symptom:** Im JSON-Schema `get_file_tree.json` und im Code ist `DefaultMaxResults = 200` definiert. Im Beschreibungstext der Tool-Registrierung steht hingegen `(Default 100, Maximum 2000)`.
- **Agentischer Schaden:** Geringfügige Verwirrung bei LLMs, die Schema-Properties gegen den Beschreibungstext abgleichen.

### Betroffener Code
- [src/AiNetLinter/Mcp/Tools/FileStructure/GetFileTreeTool.cs:19](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/FileStructure/GetFileTreeTool.cs#L19): `internal const int DefaultMaxResults = 200;`
- [src/AiNetLinter/Mcp/Registration/FileStructureToolRegistrations.cs:90](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Registration/FileStructureToolRegistrations.cs#L90): `"maxResults: Begrenzung (Default 100, Maximum 2000). "`

### Lösungsvorschlag
Beschreibungstext in `FileStructureToolRegistrations.cs` von `Default 100` auf `Default 200` anpassen (oder umgekehrt `DefaultMaxResults` auf 100 vereinheitlichen).

---

## Rang 7: Fehlender Kontext-Hinweis bei dekompilierten Metadata-Only-Stubs in `get_symbol_body`

### Problem & Auswirkung auf LLM-Agenten
- **Befund-ID:** `NAV-002`
- **Schweregrad / Dringlichkeit:** `S3` / `P3`
- **Symptom:** `get_symbol_body` auf dekompilierten DLLs liefert den Methodenkopf als Semikolon-terminierten Stub (`private static decimal CalculateBestand(...);`).
- **Agentischer Schaden:** LLMs könnten annehmen, es handle sich um ein Interface oder eine `abstract`/`partial`-Methode, obwohl es eine konkrete Methode ohne verfügbaren Quelltext ist.

### Betroffener Code
- [src/AiNetLinter/Mcp/Tools/GetSymbolBodyTool.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/GetSymbolBodyTool.cs)

### Lösungsvorschlag
Wenn das Symbol aus einer dekompilierten Assembly stammt und keinen Body hat, einen kurzen Inline-Kommentar voranstellen:
```csharp
// [Hinweis: Metadata-only Dekompilation — Methodenrumpf erfordert Source-Backing oder CIL-Decompiler]
private static decimal CalculateBestand(ArtikelItem artikelItem, ArtikelVariantenItem artikelvarianteItem);
```

---

## Rang 8: Parameter-Aliase in Dokumentation vereinheitlichen (`symbol` vs `symbolIdentifier`)

### Problem & Auswirkung auf LLM-Agenten
- **Befund-ID:** `SRC-001`
- **Schweregrad / Dringlichkeit:** `S3` / `P3`
- **Symptom:** `get_feature_context` und `get_test_context` nennen in Beispielen oft `symbol`, während `find_references`, `get_call_tree`, `get_class_structure` und `metrics_lookup` `symbolIdentifier` bzw. `symbolIdentifiers` erwarten.
- **Agentischer Schaden:** Leichte Reibung beim Kontextwechsel zwischen Tools.

### Betroffener Code
- [src/AiNetLinter/Mcp/Registration/FeatureContextToolRegistrations.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Registration/FeatureContextToolRegistrations.cs)
- [src/AiNetLinter/Mcp/Registration/TestContextToolRegistrations.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Registration/TestContextToolRegistrations.cs)

### Lösungsvorschlag
Server-seitig beide Aliase tolerant unterstützen (ist bereits der Fall), aber in allen Tool-Beschreibungen und `.mdc`-Regeln primär `symbolIdentifier` als einheitlichen Standard dokumentieren.

---

## Rang 9: Fehlende Sortier- und Filteroptionen in `get_hotspots`

### Problem & Auswirkung auf LLM-Agenten
- **Befund-ID:** `MET-001`
- **Schweregrad / Dringlichkeit:** `S3` / `P3`
- **Symptom:** `get_hotspots` liefert immer eine statische Liste aller Dateien >= 80% des `MaxLineCount`. In sehr großen Repositories kann diese Liste hunderte Zeilen lang werden.
- **Agentischer Schaden:** Unnötig lange Listen, wenn der Agent nur nach den Top-5 kritischsten Dateien sucht.

### Betroffener Code
- [src/AiNetLinter/Mcp/Tools/FileStructure/GetHotspotsScanner.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/FileStructure/GetHotspotsScanner.cs)
- [src/AiNetLinter/Mcp/Tools/FileStructure/GetHotspotsTool.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/FileStructure/GetHotspotsTool.cs)

### Lösungsvorschlag
Optionale Parameter `maxResults` (z. B. Default 20) und `minLinePercentage` (z. B. 90) ergänzen.
