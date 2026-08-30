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
- **Ursache:** Die vorherigen Analysen von `CloudStorage.dll` und `BelegEngine.dll` haben über die transitive Referenzauflösung alle 32 Slots der `AssemblyAnalysisRegistry` gefüllt. Da kein LRU-Cache (Least Recently Used) existiert, ist der Server für jede weitere neue DLL dauerhaft blockiert!

---

## 3. Identifizierte Anforderungen & Lösungsansätze

### 1. LRU-Eviction & Child-Session Lifecycle (Dringend)
- **Problem:** Transitive Kind-Sessions belegen vollwertige Registry-Slots und blockieren neue Analysen.
- **Lösung:**
  - Kind-Sessions sollten an die Lebensdauer der Root-Lease gebunden sein oder in einem separaten Cache-Pool laufen.
  - Die `AssemblyAnalysisRegistry` muss ein **automatisches LRU-Verfahren** implementieren: Wenn 32 Einträge erreicht sind, werden die am längsten ungenutzten Sessions freigegeben (`EvictLeastRecentlyUsed`), statt neue Anfragen mit `ANALYSIS_FAILED` abzuweisen.

### 2. Cross-Assembly Symbolsuche (`searchReferencedAssemblies`)
- **Anforderung:** In `find_symbol` einen optionalen Parameter `includeReferences: bool` (Default `false`) ergänzen.
- **Nutzen:** Ermöglicht einem Agenten mit einer einzigen Anfrage herauszufinden:
  ```text
  Symbol 'LagerJob' deklariert in:
  -> Sagede.OfficeLine.Wawi.LagerEngine.dll (Typ: Sagede.OfficeLine.Wawi.LagerEngine.LagerJob)
  ```

### 3. Cross-Assembly Call-Tree Traversal
- **Anforderung:** Wenn `get_call_tree` (nach Behebung des `relativeTo`-Bugs) aufgerufen wird, soll es über Assembly-Grenzen hinweg Aufrufe in referenzierte DLLs anzeigen können.
- **Nutzen:** Ein Agent kann direkt sehen:
  `Beleg.Save` -> `Beleg.SavePositionen` -> `LagerJob.Execute()` (in `LagerEngine.dll`).

### 4. Health-Check Diagnose-Kappung
- **Problem:** `get_server_health` lieferte bei 32 residenten Sessions **289 KB Text** (~70.000 Tokens), weil für jede Session hunderte Decompiler-Diagnosen ungefiltert mitgedumpt wurden.
- **Lösung:** Im Health-Check pro Session nur `LoadState`, `Generation` und Fehleranzahl (`395 Diagnosen`) ausweisen, statt den vollen Diagnosetext zu emittieren.
