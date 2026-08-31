# Linse 05 — Cache, Snapshot, Refresh, Generation, Capacity und TTL

- Reviewstatus: Orchestrator-Fallback; kein unabhängiger Reviewer verfügbar (`collab spawn failed: agent thread limit reached`).
- Revision: `d738a45e`; Produktionsquellen blieben seit der Audit-Baseline unverändert.
- MCP-Parameter: projektgebundene Abfragen mit `targetType=project`, `targetPath=<repo-root-redacted>`. Cache-Identitäten und lokale Pfade werden nicht ausgeschrieben.

## Abdeckung

Geprüft wurden `SourceSnapshotRegistry`, `ExternalResourceRegistry`, `AssemblyAnalysisSession`, Cache-Reader/Writer, Refresh-Policy, Materialisierungsreservationen und die Generation-/Lease-Modelle. Bewertet wurden Identität, Deduplication, Resident-/Operation-Limits, Eviction, TTL, aktive Leases, atomare Publikation, Refresh-Races, Cancellation und Dispose.

## Befundlage

Es wurde kein bestätigter S0–S2-Defekt gefunden.

`SourceSnapshotRegistry.cs:29-70` kapselt Residentbestand und Operation-Limit; `:73-100` reserviert Materialisierung mit möglicher Eviction und räumt verdrängte Einträge vor dem Abschluss auf. `:140-232` behandelt Resident-Deduplication, Identitätsvergleich und Reservation-Promotion getrennt vom neuen Erwerb. Dadurch bleiben Snapshot-Identität und Ressourcenbuchung gekoppelt.

`AssemblyAnalysisSession.cs:16-128` serialisiert Refreshes über ein Gate, kann unveränderte Fingerprints wiederverwenden und erzeugt neue Generationen nur bei geänderten Bytes. `:189-322` installiert neue Generationen, hält alte Generationen bei aktiven Leases und entsorgt sie nach dem letzten Release. Fehlerpfade in `:358-375` behalten einen letzten guten Snapshot als `degraded`, statt einen teilweise gebauten Snapshot zu veröffentlichen.

Die Testabdeckung ist für die Konzeptkriterien ungewöhnlich breit: `SourceSnapshotRegistryTests` prüfen Alias-Deduplication, verschiedene Revisionen/Solutionpfade, Eviction, aktive Lease-Erhaltung, Reservation-Promotion, Kapazität und Dispose. `AssemblyAnalysisSessionTests` prüfen Mtime-Reuse, Generationwechsel, Cache-Publish/Readback, inkompatible Manifestdaten, Missing-Reference-Partial, Größen-/Komplexitätsbudgets, Cancellation ohne Teilgeneration und Last-good-Degradierung. Refresh-Tests prüfen stale/fresh, Fetch-/Publish-/Integritätsfehler, Cancellation und konkurrierende Generationen.

## Abdeckungsgrenze CACHE-001

- Typ: verbleibende Laufzeitabdeckung, kein bestätigter Produktdefekt
- Schweregrad: S3
- Umfang: U3 — Langzeitbetrieb unter realer Ressourcensättigung
- Konfidenz: mittel
- Evidenz: Deterministische Tests erzeugen Kapazitäts- und Konkurrenzsituationen; ein mehrstündiger Langzeitlauf mit realer OS-/Datenträgerauslastung wurde nicht durchgeführt.
- Auswirkung: Interaktionen zwischen TTL, Dateisystemdruck und vielen gleichzeitig ablaufenden externen Source-Operationen sind nicht als Produktionslastprofil belegt.
- Reproduktion: In einer isolierten Umgebung niedrige Disk-/Memory-/Resident-Limits konfigurieren, parallel verschiedene Snapshots und Refreshes anfordern und anschließend Dispose/Lease-Drain prüfen.
- Disposition: Als Lastprofil-Grenze dokumentiert; keine Cache- oder Ressourcenänderung im Audit-only-Scope.

## Verifikation

Die Codepfade zeigen Generation-/Lease-Bindung, atomare Cache-Publikation und fail-closed Readback-Prüfungen. Die vorhandenen Tests decken die für diesen Audit geforderten Success-, Failure-, Race-, Cancellation- und Cleanup-Fälle ab; kein konkreter Cache- oder Snapshot-Befund wurde in Tech-Debt hochgestuft.
