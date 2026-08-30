# Cross-Assembly Analyse: Workflow, Lücken & Anforderungen

## 1. Ausgangsszenario & Zielsetzung
- **Ausgangspunkt:** Analyse des Beleg-Speichervorgangs (`Document.Save`) in `ThirdParty.ERP.DocumentEngine.dll`.
- **Ziel:** Nachvollziehen, wie `DocumentEngine.dll` externe Typen aus referenzierten DLLs (z. B. `WarehouseJob` aus der Lager-Engine oder `Mandant` aus `CoreEngine.dll`) aufruft und was diese dort auslösen.
- **Fragestellung:** Lässt sich ein solcher Workflow über mehrere abhängige DLLs hinweg mit den aktuellen MCP-Tools analysieren – und welche Lücken existieren?

---

## 2. Praktische Erkenntnisse aus dem Test

### Schritt 1: Erkennen externer Typen in der Ausgangs-DLL
- In `DocumentEngine.dll` nutzt `Document.cs` unter anderem folgende externe Typen:
  - `WarehouseJob _warehouseJob`
  - `Mandant _mandant`
  - `WarehouseJobPos`
- `find_symbol` auf `DocumentEngine.dll` zeigt die Verwendung in Feldern und Properties, liefert aber (korrekt) keine Typ-Definition, da der Typ extern ist.

### Schritt 2: Wo ist der Typ deklariert? (Aktuelle Lücke)
- **Problem:** Der Agent hat aktuell keine Möglichkeit, über MCP abzufragen:
  > *"In welcher der 158 referenzierten DLLs ist `WarehouseJob` oder `Mandant` definiert?"*
- **Workaround:** Der Agent muss DLL-Namen im Dateisystem erraten (z. B. `ThirdParty.ERP.WarehouseEngine.dll` vermuten) und jede DLL einzeln per MCP anfragen.

### Schritt 3: Öffnen der Ziel-DLL führt zum Totalausfall (Ressourcen-Erschöpfung)
- **Kritischer Blocker:** Beim Versuch, `ThirdParty.ERP.WarehouseEngine.dll` zu analysieren, schlug der Server fehl mit:
  ```text
  [ERROR]: ANALYSIS_FAILED: Assembly-Session konnte nicht aufgebaut werden:
  Das externe Ressourcenlimit ist ausgeschöpft (32 Einträge).
  ```
- **Ursprung des Limits:**
  - Definiert in [`ExternalResourceRegistryDefaults.MaxResidentResources = 32`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/ExternalResourceRegistry.cs#L15) und `IdleTtl = 45min`.
  - Aktuell **hardcodiert** als Default und nicht über `appsettings.json` oder CLI anpassbar.
  - Das Register prüft nur eine zeitbasierte 45-Minuten-Idle-TTL (`EvictExpiredNoLock`). Bei `TryAcquire` wird bei Erreichen von 32 Einträgen sofort `CapacityExceeded` geworfen, statt den ältesten ungenutzten Eintrag (LRU) freizugeben.

---

## 3. Identifizierte Anforderungen & Lösungsansätze

### 1. Behebung des 32er-Limits: Konfigurierbarkeit per `appsettings.json` + Echtes LRU-Eviction (Dringend)
1. **Konfigurierbarkeit über `appsettings.json`:**
   - Grundsätzlich sollten alle Lifecycle- und Kapazitätswerte (`IdleTtlMinutes`, `MaxResidentResources`, `MaxDiskBytesMb`, `MaxMemoryBytesMb`, `MaxParallelOperations`) in `appsettings.json` definierbar sein:
     ```json
     {
       "McpServer": {
         "ProjectTtlMinutes": 45,
         "MaxProjects": 4,
         "AssemblyAnalysis": {
           "IdleTtlMinutes": 45,
           "MaxResidentResources": 64,
           "MaxDiskBytesMb": 1024,
           "MaxMemoryBytesMb": 1024,
           "MaxParallelOperations": 4
         }
       }
     }
     ```
   - Mit CLI-Overrides: `--mcp-max-assemblies <n>` und `--mcp-assembly-ttl-minutes <min>`.
2. **Automatisches LRU-Eviction bei Kapazitätsgrenze (`EvictLeastRecentlyUsed`):**
   - Wenn `entries.Count >= MaxResidentResources`, darf `TryAcquire` nicht fehlschlagen, sondern muss den am längsten nicht genutzten Eintrag mit `LeaseCount == 0` verdrängen und dessen Ressourcen freigeben.
3. **Child-Session Lifecycle / Sub-Scoping:**
   - Kind-Sessions aus transitiven Referenzen sollten als abhängige Sub-Ressourcen des Root-Leases geführt werden, damit sie nicht unbemerkt die globalen Registry-Slots für neue Root-Abfragen blockieren.

---

### 2. Cross-Assembly Symbolsuche (`searchReferencedAssemblies`)
- **Anforderung:** In `find_symbol` einen optionalen Parameter `includeReferences: bool` (Default `false`) ergänzen.
- **Nutzen:** Ermöglicht einem Agenten mit einer einzigen Anfrage herauszufinden:
  ```text
  Symbol 'WarehouseJob' deklariert in:
  -> ThirdParty.ERP.WarehouseEngine.dll (Typ: ThirdParty.ERP.WarehouseEngine.WarehouseJob)
  ```

---

### 3. Cross-Assembly Call-Tree Traversal
- **Anforderung:** Wenn `get_call_tree` (nach Behebung des `relativeTo`-Bugs) aufgerufen wird, soll es über Assembly-Grenzen hinweg Aufrufe in referenzierte DLLs anzeigen können.
- **Nutzen:** Ein Agent kann direkt sehen:
  `Document.Save` -> `Document.SavePositionen` -> `WarehouseJob.Execute()` (in `WarehouseEngine.dll`).

---

### 4. Health-Check Diagnose-Kappung
- **Problem:** `get_server_health` lieferte bei 32 residenten Sessions **289 KB Text** (~70.000 Tokens), weil für jede Session hunderte Decompiler-Diagnosen ungefiltert mitgedumpt wurden.
- **Lösung:** Im Health-Check pro Session nur `LoadState`, `Generation` und Fehleranzahl (`395 Diagnosen`) ausweisen, statt den vollen Diagnosetext zu emittieren.
