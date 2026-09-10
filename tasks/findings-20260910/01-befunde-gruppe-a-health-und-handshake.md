# Gruppe A: Health & Handshake — Befundbericht

## Geprüfte Tools & Ressourcen
- `get_server_health`
- `report_observability_feedback`
- Resource `ainetlinter://agent-guide`
- Resource `ainetlinter://overview{?targetPath}`
- Resource `ainetlinter://rules{?targetPath}`

---

### [Major] Befund F-05: `get_server_health` wirft `PROJECT_NOT_INITIALIZED` bei noch nicht geladenem Source-Target (Asymmetrie zu Assemblies)
- **Tool(s)**: `get_server_health`
- **Quellcode**:
  - [GetServerHealthTool.cs:123-125](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/ServerMaintenance/GetServerHealthTool.cs#L123-L125)
  - [GetServerHealthTool.cs:91](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/ServerMaintenance/GetServerHealthTool.cs#L91)
  - [ServerMaintenanceToolRegistrations.cs:87](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Registration/ServerMaintenanceToolRegistrations.cs#L87)
- **Evidenz**:
  Aufruf von `get_server_health` direkt nach dem Start des Daemon/Servers mit einem gültigen `.slnx`-Target:
  ```text
  [ERROR]: PROJECT_NOT_INITIALIZED: Fuer 'C:\Daten\Entwicklung\Ralf\AiNetLinter\AiNetLinter.slnx' existiert kein residenter Projekt-Key.
  hint: Ersten zielgebundenen Tool-Aufruf mit diesem targetPath senden; die Solution wird direkt geladen...
  ```
  Dagegen Aufruf von `get_server_health` mit einem Assembly-Target (`LOCAL-01`):
  ```text
  # AiNetLinter MCP-Server — Health
  ...
  ## Assembly-Sessions (1)
  ...
  LoadState: partial, Vollständigkeit: partial, Hash: ...
  ```
- **Problem**: Ein Agent, der zu Beginn einer Session prüfen möchte, ob sein Projekt gesund ist (`get_server_health(targetPath)`), erhält einen verwirrenden Fehler `PROJECT_NOT_INITIALIZED`, weil `FindSnapshot` im In-Memory-Dictionary sucht, statt die Solution zu laden. Bei Assemblies hingegen führt `ExecuteAssemblyAsync` automatisch `assemblyRegistry.LeaseAsync` aus und lädt die Assembly on demand!
- **Reproduktion**:
  1. Server neu starten.
  2. `get_server_health(targetPath: "<pfad-zu-solution>.slnx")` aufrufen.
- **Empfehlung**:
  In [GetServerHealthTool.cs:123](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/ServerMaintenance/GetServerHealthTool.cs#L123): Entweder wie bei Assemblies ein transparentes Lease/Laden auslösen oder in der Antwort ein strukturiertes Snapshot-DTO mit `LoadState: not_loaded` / `fresh: false` ohne harten Error-Return liefern.

---

### [Minor] Befund F-07: Schema-Description vs. Validierungs-Fehlermeldung bei `report_observability_feedback`
- **Tool(s)**: `report_observability_feedback`
- **Quellcode**:
  - [ServerMaintenanceToolRegistrations.cs:246](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Registration/ServerMaintenanceToolRegistrations.cs#L246)
  - [ReportObservabilityFeedbackTool.cs:81](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/ServerMaintenance/ReportObservabilityFeedbackTool.cs#L81)
- **Evidenz**:
  Tool-Description in der Registrierung:
  ```text
  "feedbackType: bug, false_positive, confusing_output, feature_request, performance."
  ```
  Validierungsfehler bei leerem/fehlendem Parameter in `ReportObservabilityFeedbackTool.ValidateParameters`:
  ```text
  "Gueltige Werte: 'issue', 'feature_request', 'confusing_output', 'false_positive'."
  ```
- **Problem**:
  1. In der Description steht `bug`, in der Fehlermeldung `issue`.
  2. `performance` steht in der Description, fehlt aber in der Fehlermeldung.
  3. `ValidateParameters` prüft zur Laufzeit gar nicht auf die Enum-Werte, sondern lässt jeden beliebigen String durch (`string.IsNullOrWhiteSpace`).
- **Reproduktion**:
  `report_observability_feedback(feedbackType="", title="T", description="D")` aufrufen.
- **Empfehlung**:
  Wertebereich in [ReportObservabilityFeedbackTool.cs:81](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/ServerMaintenance/ReportObservabilityFeedbackTool.cs#L81) und [ServerMaintenanceToolRegistrations.cs:246](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Registration/ServerMaintenanceToolRegistrations.cs#L246) abgleichen: `['bug', 'feature_request', 'confusing_output', 'false_positive', 'performance']` und optional als striktes String-Enum im Tool-Schema annotieren.

---

### Beobachtungen aus Freier Erkundung (Gruppe A)
- **Positiv**: `get_server_health` ohne Target liefert eine saubere globale Übersicht mit Uptime, Daemon-Keys, PID und Status aller residenten Sessions ohne Fehler.
- **Positiv**: Resource `ainetlinter://agent-guide` ist offline und ohne Target lesbar und bietet perfekten Bootstrap-Kontext für neue Agenten.
- **Hinweis zu Resources**: Die Resources `overview` und `rules` verlangen RFC-6570 Query-Expansion (`ainetlinter://overview?targetPath=...`). Agenten, die MCP-Resources über standardisiertes URI-Matching lesen, müssen den Query-String URL-encoden. Dies ist in den Schemas korrekt dokumentiert.
