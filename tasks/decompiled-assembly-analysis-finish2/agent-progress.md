# Agent Arbeitsplan & Fortschrittsdokumentation

Dieses Dokument dient als Arbeitsplan und Nachweis für die abgearbeiteten Arbeitspakete im Rahmen von `decompiled-assembly-analysis-finish2`.

## Übersicht der Arbeitspakete

| Nr. | Paket | Bereich | Status |
|:---|:---|:---|:---|
| 1 | **Test-Vertragskorrekturen** | `AiNetLinter.IntegrationTests` (Beschreibungstexte `ambiguous` & `sortBy`) | ✅ completed |
| 2 | **`TD-EPIC-C-007` (Tick-Normalisierung)** | `ThinClientProxy` / Handshake-Limitvergleich | ✅ completed |
| 3 | **`TD-EPIC-B-008` (Truncation-Reason)** | `AssemblyAnalysisResponseLimits.CreateSummary` | ✅ completed |
| 4 | **`TD-EPIC-B-009` (Health-Detail E2E-Test)** | `McpServerAssemblyHealthE2ETests` / E2E-Health | ✅ completed |
| 5 | **`TD-EPIC-C-006` (Materializer+Registry E2E-Test)** | Gekoppelte Materializer- & Registry-Integration | ✅ completed |
| 6 | **EPIC-E: Klassenstruktur-Filter (BEF-09)** | `GetClassStructureTool` Kind-/Name-Filter | ✅ completed |
| 7 | **EPIC-E: In-Memory-Metrikengröße (BEF-10)** | `MetricsTreeScanner` SourceText-Größe | ✅ completed |

---

## Detaillierte Fortschrittsdokumentation

### Paket 1: Test-Vertragskorrekturen (Beschreibungstexte `ambiguous` & `sortBy`)
- **Ziel:** Die 2 bekannten fehlschlagenden Nicht-Stress IntegrationTests analysieren und beheben, sodass die Testsuite zu 100% grün durchläuft.
- **Status:** ✅ completed
- **Änderungen:**
  - `src/AiNetLinter/Mcp/Registration/AnalysisToolRegistrations.cs`: `SearchPatternDescription` aktualisiert, um `ambiguous` und `unavailable` im `resolution:` Feld der Beschreibung aufzunehmen (`resolution: resolved, not_applicable, unknown, ambiguous, unavailable`).
  - `src/AiNetLinter.IntegrationTests/Mcp/McpHandshakeToolRegistrationTests.cs`: Assertions für `sortBy` an die kanonische Tool-Registrierung angepasst (`sortBy: 'path' [Default], 'size_desc', 'extension'`).

---

### Paket 2: `TD-EPIC-C-007` Tick-Normalisierung im Handshake-Limitvergleich
- **Ziel:** Flaky Float-/Decimal-Vergleiche bei `ExternalIdleTtlMinutes` eliminieren durch tick-genaue Normalisierung (`TimeSpan.Ticks`).
- **Status:** ✅ completed
- **Änderungen:**
  - `src/AiNetLinter/Mcp/Daemon/DaemonProtocol.cs`: Hilfsmethode `OptionalMatchesNormalizedMinutes(double? left, double? right)` implementiert, die optionale Double-Minutenwerte auf `TimeSpan.FromMinutes(...).Ticks` normalisiert vergleicht.
  - `DaemonSessionHandshake.MatchesLimits`: Nutzt `OptionalMatchesNormalizedMinutes(ExternalIdleTtlMinutes, limits.ExternalIdleTtlMinutes)` für robuste Vergleiche.

---

### Paket 3: `TD-EPIC-B-008` Truncation-Reason Präzisierung
- **Ziel:** `TruncatedBy` in `AssemblyAnalysisResponseLimits.CreateSummary` darf `"maxDiagnosticBytes"` nur enthalten, wenn tatsächlich das Byte-Limit gegriffen hat (und nicht bei bloßer Slot-Limit-Ausschöpfung).
- **Status:** ✅ completed
- **Änderungen:**
  - `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.cs`: `SelectSamples` liefert `ByteTruncated` Status zurück. `CreateSummary` prüft `byteTruncated == true`, bevor `"maxDiagnosticBytes"` an `TruncatedBy` angehängt wird.
  - `src/AiNetLinter.FastTests/Mcp/Assemblies/AssemblyAnalysisDispatcherCapabilityTests.cs`: Regressionstest `DiagnosticsProjection_TruncatedBy_DoesNotIncludeMaxDiagnosticBytesWhenOnlySlotLimitHit` hinzugefügt.

---

### Paket 4: `TD-EPIC-B-009` Health-Detail E2E-Test
- **Ziel:** E2E-Integrationstest für `get_server_health` mit `includeDiagnostics: true` und `maxDiagnostics: N`.
- **Status:** ✅ completed
- **Änderungen:**
  - `src/AiNetLinter.IntegrationTests/Mcp/Tools/McpServerAssemblyHealthE2ETests.cs`: Neuer E2E-Test `GetServerHealth_WithIncludeDiagnostics_ReturnsDetailedDiagnosticsPayload` hinzugefügt, der den MCP-Server-Aufruf mit `includeDiagnostics` und `maxDiagnostics` verifiziert.

---

### Paket 5: `TD-EPIC-C-006` Gekoppelter Materializer- & Registry-E2E-Integrationstest
- **Ziel:** E2E-Test zur Verifikation des Zusammenspiels von `ExternalSourceSnapshotMaterializer`, `ExternalResourceRegistry` und `SourceSnapshotRegistry` (Reservierung, Promotion zur residenten Lease, Freigabe/Eviction beim Dispose).
- **Status:** ✅ completed
- **Änderungen:**
  - `src/AiNetLinter.IntegrationTests/Mcp/Assemblies/ExternalSourceSnapshotMaterializerTests.cs`: Test `MaterializeAsync_CoupledWithSourceSnapshotRegistry_PromotesReservationToResidentLease` hinzugefügt. Verifiziert den vollständigen Lebenszyklus von Checkout-Materialisierung bis Lease-Dispose.

---

### Paket 6: EPIC-E Klassenstruktur-Filter (BEF-09)
- **Ziel:** `get_class_structure` um optionale Filter `kindFilter` (Method, Property, Field, Constructor, all) und `nameFilter` (Substring-Filter) erweitern.
- **Status:** ✅ completed
- **Änderungen:**
  - `src/AiNetLinter/Mcp/Tools/FileStructure/GetClassStructureTool.cs`: Parameter `kindFilter` und `nameFilter` in `GetClassStructureArgs` aufgenommen, `FilterMembers` und `MatchesKind` implementiert.
  - `src/AiNetLinter/Mcp/Registration/FileStructureToolRegistrations.cs`: CLI/MCP-Tool-Registrierung und Tool-Beschreibung für `get_class_structure` um `kindFilter` und `nameFilter` ergänzt.
  - `src/AiNetLinter.FastTests/Mcp/Tools/FileStructure/GetClassStructureToolTests.cs`: Unit-Tests `ExecuteAsync_WithKindFilter_FiltersMembersByKind` und `ExecuteAsync_WithNameFilter_FiltersMembersByName` hinzugefügt.

---

### Paket 7: EPIC-E In-Memory-Metrikengröße (BEF-10)
- **Ziel:** In-Memory-Dokumente ohne physischen Pfad auf der Festplatte dürfen in `metrics_tree` / `metrics_lookup` nicht fälschlich `0 B` anzeigen, sondern berechnen ihre Dateigröße semantisch aus dem `SourceText`.
- **Status:** ✅ completed
- **Änderungen:**
  - `src/AiNetLinter/Mcp/Tools/MetricsTree/MetricsTreeScanner.cs`: `TryGetFileSize` prüft `File.Exists(f.AbsolutePath)` und fällt bei Nicht-Existenz oder `IOException` sauber auf `f.Document.TryGetText(...)` -> `sourceText.Length` zurück (unter Beachtung von `EnforceNoSilentCatch`).
  - `src/AiNetLinter.FastTests/Mcp/Tools/MetricsTree/MetricsTreeToolTests.cs`: Test `ExecuteAsync_InMemoryDocument_CalculatesSizeFromSourceText` hinzugefügt.
