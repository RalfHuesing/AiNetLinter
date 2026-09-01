# 360-Grad-Audit: Composite Context, Testing & Metrics Tools

## Scope und untersuchte MCP-Tools

- `get_feature_context`: Ganzheitlicher Kontext für ein Symbol (Deklaration, Budgets, Metriken, direkte Aufrufer, zugeordnete Tests, offene Linter-Violations).
- `get_test_context`: Statische Test-Zuordnung für ein Symbol/Klasse (über Namenskonventionen und `@covers`-Tags) inklusive kopierbarem `dotnet test`-Befehl.
- `metrics_lookup`: Detaillierter Schwellwert-Abgleich für ausgewählte Typen/Member (LOC, AI-Context-Footprint, Public Members, Komplexität).
- `metrics_tree`: Hierarchischer Metrik-Baum (`code_size`, `comment_density`, `violation_density`, `complexity`) über das Dateisystem.
- `get_server_health`: Diagnose-Schnappschuss über den Server-Prozess, Daemon-Verbindungen, geladene Projekte und Assembly-Sessions.
- `reload_config`: Hot-Reload der `rules.json`-Regelkonfiguration ohne Server-Neustart.
- `report_observability_feedback`: Strukturiertes Feedback-Logging für Observability- und Telemetrie-Zwecke.

---

## Befunde & Begründungen

### 1. Bugs

#### FINDING-CTX-01: `get_server_health` mit `targetType='project'` scheitert im Proxy-Modus vor lokalem Zugriff

- **Kategorie:** Bug
- **Priorität:** P1
- **Größe:** S
- **Vertrauensgrad:** Hoch
- **Betroffene Komponenten:**
  - `src/AiNetLinter/Mcp/Registration/ServerMaintenanceToolRegistrations.cs` (Zeilen 94–108)
  - `src/AiNetLinter/Mcp/Tools/ServerMaintenance/GetServerHealthTool.cs` (Zeilen 75–89)
- **Soll-Ist-Abweichung:**
  Im Daemon-/Proxy-Betrieb hält der Hintergrund-Daemon die residenten Projekt-Snapshots. Ruft ein Client `get_server_health` mit `targetType='project'` und `targetPath='...'` auf, delegiert `ServerMaintenanceToolRegistrations` die Anfrage an die lokale `ProjectRegistry` des Client-Prozesses statt an den Daemon. Wenn dieser Client-Kanal das Projekt noch nicht selbst geladen hat, antwortet das Tool fälschlicherweise mit:
  `[ERROR]: PROJECT_NOT_INITIALIZED: Fuer '...' existiert kein residenter Projekt-Key.`
  obwohl das Projekt im Daemon längst geladen und aktiv ist.
- **Evidenz:**
  - Live-Aufruf von `get_server_health` mit `targetType='project'` scheiterte reproduzierbar mit `PROJECT_NOT_INITIALIZED`, während `get_server_health` ohne Target den Daemon mit dem geladenen Projekt korrekt auflistete.
- **Auswirkung:**
  Gezielte Health-Checks auf bestehende Projekte schlagen im Mehrkanal- oder Proxy-Betrieb fehl.
- **Empfehlung & Wunsch:**
  `ExecuteGetServerHealthAsync` muss im Daemon-Modus auch bei projektgebundenen Anfragen den Status aus dem `DaemonRuntimeContext` bzw. über den Daemon-Channel abrufen.
- **Abgrenzung:** Routing- und Registrierungs-Bug im ServerMaintenance-Modul.

---

### 2. Optimierungen

#### FINDING-CTX-02: Fehlender Default-Wert für `mode` in `metrics_tree`

- **Kategorie:** Optimierung
- **Priorität:** P3
- **Größe:** S
- **Vertrauensgrad:** Hoch
- **Betroffene Komponenten:**
  - `src/AiNetLinter/Mcp/Registration/FileStructureToolRegistrations.cs`
  - `src/AiNetLinter/Mcp/Tools/MetricsTree/MetricsTreeTool.cs`
- **Soll-Ist-Abweichung:**
  Wird `metrics_tree` ohne den Parameter `mode` aufgerufen, bricht das Tool mit `INVALID_ARGUMENT: Pflichtparameter 'mode' fehlt oder ist leer` ab, anstatt den intuitiven Standardwert `code_size` zu verwenden.
- **Evidenz:**
  - Live-Aufruf ohne `mode` liefert: `Gueltige Werte: code_size, comment_density, violation_density, complexity.`
- **Auswirkung:**
  Vermeidbare Fehlversuche bei schnellen Repository-Erkundungen.
- **Empfehlung & Wunsch:**
  Default-Wert `mode = "code_size"` im Schema und Registrar setzen.
- **Abgrenzung:** Ergonomie- und Default-Wert-Optimierung.

---

### 3. Missing Features

Alle wesentlichen Composite- und Maintenance-Funktionen sind vorhanden und arbeiten performant.

---

## Verifikations-Matrix der Context, Testing & Maintenance Tools

| Werkzeug | Getestetes Szenario | Ergebnis & Performanz | Bewertung |
|---|---|---|---|
| `get_feature_context` | Abfrage für `AssemblyAnalysisRegistry` | **55 ms**; aggregiert 429 Zeilen Deklaration, 3 Budgets, 30 Aufrufer, 20 Tests in 3 Dateien und offene Violations in einem einzigen Tool-Call. | **Exzellent** |
| `get_test_context` | Abfrage für `AssemblyAnalysisRegistry` | **38 ms**; liefert 20 Tests und generiert direkt den ausführbaren `dotnet test`-Filterbefehl. | **Exzellent** |
| `metrics_lookup` | Abfrage für `T:AssemblyAnalysisRegistry` | **15 ms**; exakte LOC-, Footprint- und Member-Werte mit Grenzwertabgleich. | **Sehr gut** |
| `metrics_tree` | Verzeichnisbaum über `src/AiNetLinter/Mcp` mit `depth=2` | **35 ms**; hierarchische LoC- und Dateigrößen-Aufschlüsselung aller 311 MCP-Dateien. | **Sehr gut** |
| `get_server_health` | Globaler Status & zielgebundener Status | Uptime (50min), PID (50456), Version (1.0.158), Verbindungen (2); leidet unter Finding `FINDING-CTX-01`. | **Gut** (Bug P1) |
| `reload_config` | Hot-Reload von `rules.json` | **18 ms**; lädt Regeln neu und meldet Vorher-/Nachher-Delta (17 Regeln aktiv). | **Hervorragend** |
| `report_observability_feedback` | Senden von Telemetrie-Feedback | **5 ms**; strukturiertes Loggen bestätigt. | **Sehr gut** |
