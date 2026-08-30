# Cross-Assembly Analyse: Workflow, Lücken & Anforderungen

## 1. Ausgangsszenario & Zielsetzung
- **Ausgangspunkt:** Analyse des Beleg-Speichervorgangs (`Beleg.Save`) in `Sagede.OfficeLine.Wawi.BelegEngine.dll`.
- **Ziel:** Nachvollziehen, wie `BelegEngine.dll` externe Typen aus referenzierten DLLs (z. B. `LagerJob` aus der Lager-Engine oder `Mandant` aus `Engine.dll`) aufruft und was diese dort auslösen.
- **Fragestellung:** Lässt sich ein solcher Workflow über mehrere abhängige DLLs hinweg mit den aktuellen MCP-Tools analysieren – und welche Lücken existieren?

---

## 2. Praktische Erkenntnisse aus dem Test

### Schritt 1: Erkennen externer Typen in der Ausgangs-DLL
- In `BelegEngine.dll` nutzt `Beleg.cs` unter anderem folgende externe Typen:
  - `LagerJob _lagerJob`
  - `Mandant _mandant`
  - `LagerJobPos`
- `find_symbol` auf `BelegEngine.dll` zeigt die Verwendung in Feldern und Properties, liefert aber (korrekt) keine Typ-Definition, da der Typ extern ist.

### Schritt 2: Wo ist der Typ deklariert? (Aktuelle Lücke)
- **Problem:** Der Agent hat aktuell keine Möglichkeit, über MCP abzufragen:
  > *"In welcher der 158 referenzierten DLLs ist `LagerJob` oder `Mandant` definiert?"*
- **Workaround:** Der Agent muss DLL-Namen im Dateisystem erraten (z. B. `Sagede.OfficeLine.Wawi.LagerEngine.dll` vermuten) und jede DLL einzeln per MCP anfragen.

### Schritt 3: Öffnen der Ziel-DLL führt zum Totalausfall (Ressourcen-Erschöpfung)
- **Kritischer Blocker:** Beim Versuch, `Sagede.OfficeLine.Wawi.LagerEngine.dll` zu analysieren, schlug der Server fehl mit:
  ```text
  [ERROR]: ANALYSIS_FAILED: Assembly-Session konnte nicht aufgebaut werden:
  Das externe Ressourcenlimit ist ausgeschöpft (32 Einträge).
  ```
- **Ursprung des Limits:**
  - Definiert in [`ExternalResourceRegistryDefaults.MaxResidentResources = 32`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/ExternalResourceRegistry.cs#L15) in `ExternalResourceRegistry.cs`.
  - Aktuell **hardcodiert** als Default und nicht über CLI oder Konfiguration anpassbar.
  - Das Register prüft nur eine zeitbasierte 45-Minuten-Idle-TTL (`EvictExpiredNoLock`). Bei `TryAcquire` wird bei Erreichen von 32 Einträgen sofort `CapacityExceeded` geworfen, statt den ältesten ungenutzten Eintrag (LRU) freizugeben.

---

## 3. Identifizierte Anforderungen & Lösungsansätze

### 1. Behebung des 32er-Limits: Konfigurierbarkeit + Echtes LRU-Eviction (Dringend)
1. **Konfigurierbarkeit (CLI / Settings):**
   - Einführung von `--mcp-max-assemblies <n>` (analog zu `--mcp-max-projects 4`) und `--mcp-assembly-ttl-minutes <min>` beim Serverstart.
   - Optional konfigurierbar in `ainetlinter.settings.json` / `rules.json`.
2. **Automatisches LRU-Eviction bei Kapazitätsgrenze (`EvictLeastRecentlyUsed`):**
   - Wenn `entries.Count >= MaxResidentResources`, darf `TryAcquire` nicht fehlschlagen, sondern muss den am längsten nicht genutzten Eintrag mit `LeaseCount == 0` verdrängen und dessen Ressourcen freigeben.
3. **Child-Session Lifecycle / Sub-Scoping:**
   - Kind-Sessions aus transitiven Referenzen sollten als abhängige Sub-Ressourcen des Root-Leases geführt werden, damit sie nicht unbemerkt die globalen Registry-Slots für neue Root-Abfragen blockieren.

---

### 2. Cross-Assembly Symbolsuche (`searchReferencedAssemblies`)
- **Anforderung:** In `find_symbol` einen optionalen Parameter `includeReferences: bool` (Default `false`) ergänzen.
- **Nutzen:** Ermöglicht einem Agenten mit einer einzigen Anfrage herauszufinden:
  ```text
  Symbol 'LagerJob' deklariert in:
  -> Sagede.OfficeLine.Wawi.LagerEngine.dll (Typ: Sagede.OfficeLine.Wawi.LagerEngine.LagerJob)
  ```

---

### 3. Cross-Assembly Call-Tree Traversal
- **Anforderung:** Wenn `get_call_tree` (nach Behebung des `relativeTo`-Bugs) aufgerufen wird, soll es über Assembly-Grenzen hinweg Aufrufe in referenzierte DLLs anzeigen können.
- **Nutzen:** Ein Agent kann direkt sehen:
  `Beleg.Save` -> `Beleg.SavePositionen` -> `LagerJob.Execute()` (in `LagerEngine.dll`).

---

### 4. Health-Check Diagnose-Kappung
- **Problem:** `get_server_health` lieferte bei 32 residenten Sessions **289 KB Text** (~70.000 Tokens), weil für jede Session hunderte Decompiler-Diagnosen ungefiltert mitgedumpt wurden.
- **Lösung:** Im Health-Check pro Session nur `LoadState`, `Generation` und Fehleranzahl (`395 Diagnosen`) ausweisen, statt den vollen Diagnosetext zu emittieren.
