# Execution-Log: 360-Grad-Audit der dekompilierten Assembly-Unterstützung

## 2026-09-01 — Initialisierung und Audit-Start

- **Konzept-Prüfung:** `Konzept.md` gelesen und Freigabestatus (`status: ready`) verifiziert.
- **Redaktions- und Copyright-Regel aktiviert:** Externe Prüffälle werden strikt über die opaken Labels `GIT-01`, `LOCAL-01`, `LOCAL-02`, `LOCAL-03` und `FALSE-01` geführt.
- **Live MCP-Inspektion durchgeführt:**
  - `get_server_health` mit `includeSessions=true` und `includeDiagnostics=true` ausgeführt. Daemon-Status: Version 1.0.158, Uptime aktiv.
  - Prüffall `LOCAL-01` mit `inspect_assembly`, `find_assembly_extensions`, `find_symbol`, `get_symbol_body`, `get_class_structure`, `get_namespace_tree`, `find_references`, `get_call_tree`, `get_type_hierarchy`, `dependency_graph`, `get_file_skeleton`, `metrics_lookup`, `metrics_tree` analysiert.
  - Prüffall `LOCAL-02` mit `inspect_assembly` und Navigationstools analysiert.
  - Prüffall `LOCAL-03` mit `inspect_assembly` analysiert (managed EXE-Verhalten und 8-KB-Response-Budget-Trunkierung verifiziert).
  - Prüffall `FALSE-01` mit `inspect_assembly` analysiert (`WORKSPACE_DIAGNOSTIC` / "Datei enthält keine .NET-Metadaten" als sicherer Negativfall verifiziert).
- **Code-Inspektion der Engine:**
  - `AssemblyDecompilationAdapter.cs`, `AssemblyDecompiledBodyResolver.cs`, `AssemblyDecompilationCache.cs`, `AssemblyReferenceResolver.cs`, `AssemblyRoslynWorkspaceFactory.cs`, `AssemblyAnalysisRegistry.cs`, `AssemblyAnalysisResponseLimits.cs`, `AssemblyAnalysisService.cs` und Registrierungen im Detail analysiert.
- **Wesentliche Befunde identifiziert:**
  - Bug in `AssemblyDecompiledBodyResolver.cs`: Top-Level-Klassen führen bei `get_symbol_body` zu `InvalidOperationException` wegen `symbol.ContainingType == null`.
  - Bug in `AssemblyDecompilationCache.cs`: `Publish` löscht neue Generationen im `finally`-Block, wenn `TryRead` für eine bestehende Generation anschlägt.
  - Bug/Einschränkung in `AssemblyReferenceResolver.cs`: Exakter Versionsvergleich blockiert Framework-Assembly-Unification und erzeugt kaskadierende `version_mismatch`-Fehler.
  - Lücke in `find_assembly_extensions`: Fehlende Steuerung von `includeReferences` (erzwingt immer teure Referenzexpansion).
  - Fehlleitende Sufficiency-Hints bei `find_references` auf dekompilierten Snapshots ohne Rümpfe.
