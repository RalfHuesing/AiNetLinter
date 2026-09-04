---
status: draft
task: 02-source-zuordnung-und-session-lifecycle
priority: 2
---

# Konzept: Source-Zuordnung, Health-Projektion & Session-Lifecycle

## 1. Ziel & Nutzen

Dieses Konzept verbindet die physisch bereitgestellten Git-Repositories (aus Task `01`) mit der Roslyn-Codeanalyse und dem MCP-Daemon-Lebenszyklus.

**Kernnutzen:**
- **Wahrheitsgetreue Status-Projektion**: Behebung des P0-Befunds `EXTERNAL-SOURCE-HEALTH-MISLEADING`. Der Server meldet dem Anwender präzise, ob ein Mapping fehlt, der Git-Clone scheiterte oder kein passendes C#-Projekt gefunden wurde – statt fälschlich immer `not-configured` zu behaupten.
- **Transparente Assembly-zu-Projekt-Auflösung**: Eine übergebene Assembly (z. B. `San.OfficeLine.Core.dll`) wird deterministisch über `<AssemblyName>` oder Dateinamen der passenden `.csproj` in der geklonten Solution zugeordnet.
- **Laufzeit-Aktualisierung (Daemon-Reload)**: Konfigurationsänderungen in `appsettings.json` oder `external-sources.json` werden zur Laufzeit wirksam (via `reload_config` oder Datei-Zeitstempel-Check), ohne dass der Hintergrund-Daemon manuell per Task-Manager getötet werden muss.
- **Beseitigung von Zombie-Caches**: Fehlerhafte Bereitstellungsversuche werden nach Konfigurations-Updates sauber invalidiert und sperren nicht für 45 Minuten die Analyse (`cachedNegativeFallback`).

---

## 2. Betroffene Projektbereiche & Ist-Zustand

### 2.1 Problembereiche im Code
1. **[AssemblyHealthProjection.cs:72](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/ServerMaintenance/Projection/AssemblyHealthProjection.cs#L72)**:
   ```csharp
   MappingStatus: source is null ? "not-configured" : "verified"
   ```
   Ignoriert die tatsächliche Konfiguration und blendet jeden Bereitstellungsfehler als angeblich "nicht konfiguriert" aus.
2. **[AssemblySourceSelectionOrchestrator.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/SourceSelection/AssemblySourceSelectionOrchestrator.cs)**:
   Verwaltet verschachtelte negative In-Memory-Ergebnisse (`providerCoordinator.RememberNegativeResult`), die bei Konfigurationsänderungen nicht verworfen werden.
3. **[AssemblyAnalysisHostComposition.cs:201](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisHostComposition.cs#L201)**:
   Liest `appsettings.json` nur ein einziges Mal statisch beim Hochfahren der Host-Komposition.
4. **[ReloadConfigTool.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/ServerMaintenance/ReloadConfigTool.cs)**:
   Lädt aktuell ausschließlich `rules.json` für Linter-Regeln nach, ignoriert aber externe Source-Mappings.

---

## 3. Muss-Kriterien & Akzeptanzkriterien

### 3.1 Muss-Kriterien (Funktional)
1. **Differenzierte Health-Meldungen in `get_server_health`**:
   - `Mapping-Status` muss folgende Zustände differenziert ausweisen:
     - `"not-configured"`: Für die Assembly existiert kein Eintrag in `external-sources.json`.
     - `"configured"`: Mapping existiert, Download/Verbindung steht noch aus.
     - `"clone-failed"`: Mapping existiert, aber Git-Clone/Netzwerk ist gescheitert (inklusive Ausgabe der Original-Fehlermeldung).
     - `"project-not-found"`: Repository geklont, aber keine passende `.csproj` für die Assembly gefunden.
     - `"verified"`: Quellcode erfolgreich geladen und mit Roslyn-Workspace verknüpft.
   - `Checkout-Status`: Unterscheidet sauber zwischen `not-applicable` (wenn kein Mapping existiert), `failed` und `verified`.
   - `Next-Action`: Konkrete Handlungsanweisung passend zum tatsächlichen Fehler.
2. **Deterministischer Assembly-zu-Projekt-Match**:
   - Wenn eine Solution geklont ist:
     1. Suche nach `.csproj`-Dateien, deren Name mit der Assembly übereinstimmt (z. B. `San.OfficeLine.Core.csproj` -> `San.OfficeLine.Core.dll`).
     2. Falls kein direkter Dateinamens-Match vorliegt: Prüfung des XML-Tags `<AssemblyName>` in den Projekten der Solution.
   - Tritt Mehrdeutigkeit auf, wird ein klarer Fehler `SourceProjectAmbiguous` gemeldet.
3. **Config-Reload & Cache-Invalidierung**:
   - Das MCP-Tool `reload_config` wird erweitert: Es lädt neben `rules.json` auch `appsettings.json` und `external-sources.json` frisch von der Platte.
   - Bei Reload werden alle negativen Cache-Einträge (`cachedNegativeFallback`) gelöscht, sodass ein erneuter Abruf sofort die neuen Einstellungen nutzt.
4. **Schlanker Orchestrator**:
   - Direkte Kopplung von `GitEngine` (aus Task 01) und Roslyn-Workspace ohne Zwischen-Koordinatoren.

### 3.2 Akzeptanzkriterien (Verifikation)
- [ ] Unit-Tests in `AiNetLinter.FastTests` belegen: Wenn ein Mapping existiert, aber der Clone fehlschlägt, meldet `get_server_health` `clone-failed` und **nicht** `not-configured`.
- [ ] Ein Test beweist: Nach Aufruf von `reload_config` wird eine nachträglich in `external-sources.json` eingetragene Assembly sofort erkannt.
- [ ] Ein Test beweist: Eine Assembly wird erfolgreich der richtigen `.csproj` innerhalb einer Test-Solution zugeordnet.

---

## 4. Non-Goals (Scope-Grenzen)

- **Keine Neugestaltung des Git-Transports**: Dieser wird vollständig aus Task `01` (`GitEngine`) übernommen.
- **Keine Paginierung von MCP-Ergebnissen**: Dies ist Gegenstand von Task `03`.
- **Keine Heuristik-Änderungen an AST-/Such-Funktionen**: Dies ist Gegenstand von Task `04`.

---

## 5. Geplante Verifikation

1. **Automatisierte Tests**:
   - `dotnet test src/AiNetLinter.FastTests --filter Category=Unit` (insb. `AssemblyHealthProjectionTests` und `AssemblySourceSelectionTests`).
   - `dotnet test src/AiNetLinter.IntegrationTests --filter Category=Integration`
2. **Build-Prüfung**:
   - `dotnet build` (warnungs- und fehlerfrei).

---

## 6. Arbeitsgedächtnis (nur Draft)

### Kontextanker & Evidenz
- Aus `tasks/assembly-analyse-verbesserungen/audit-findings-und-ideen.md`:
  - P0-Befund `EXTERNAL-SOURCE-HEALTH-MISLEADING`: [AssemblyHealthProjection.cs:72](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/ServerMaintenance/Projection/AssemblyHealthProjection.cs#L72).
  - Ralf hat `external-sources.json` editiert, aber der Daemon lief bereits und bot keinen Reload dafür an.
- Zu bearbeitende Dateien:
  - `src/AiNetLinter/Mcp/Tools/ServerMaintenance/Projection/AssemblyHealthProjection.cs`
  - `src/AiNetLinter/Mcp/Tools/ServerMaintenance/ReloadConfigTool.cs`
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/SourceSelection/AssemblySourceSelectionOrchestrator.cs`
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisHostComposition.cs`
