# Linse 02 — External-Source-Konfiguration, Mapping und Provider

- Reviewstatus: Orchestrator-Fallback; kein unabhängiger Reviewer verfügbar (`collab spawn failed: agent thread limit reached`).
- Revision: `73c1b5ab`; Produktionsquellen blieben seit der Audit-Baseline unverändert.
- MCP-Parameter: projektgebundene Abfragen mit `targetType=project`, `targetPath=<repo-root-redacted>`. Konfigurationswerte mit URLs, Zugangsdaten und lokalen Cachepfaden werden nicht wiedergegeben.

## Abdeckung

Geprüft wurden `ExternalSourceResourceOptions`, `ExternalSourceCacheOptions`, die JSON-/Mapping-Validierung, `AssemblySourceSelectionOrchestrator`, die Provider-Fehlerprojektion und `ExternalSourceRepositoryAcquirer`. Die Prüfung umfasste Default-Limits, Pfad-/URL-Syntax, Aliasauflösung, Providerzustände, Fallback auf Decompilation und die Sichtbarkeit von Degradierung.

## Befundlage

Es wurde kein bestätigter S0–S2-Defekt gefunden.

Die Konfiguration erzwingt in `src/AiNetLinter/Configuration/ExternalSourceConfiguration.cs:12-70` positive Disk-/Memory-/Parallelitäts-/Resident-Limits, einen nichtnegativen Idle-TTL und einen kanonisierten absoluten Cache-Root. Die Pfadlogik in `:242-407` weist relative/unsichere Cache-Segmente, Device-Pfade und problematische Rohsegmente zurück.

Die Mappingprüfung validiert Repository-URL, repository-relativen `.sln`/`.slnx`-Pfad und nichtleere Assembly-Aliase. `ExternalSourceRepositoryAcquirer.cs:299-347` validiert diese Annahmen vor Transportausführung erneut. Die vorhandenen Tests prüfen unter anderem doppelte bzw. mehrdeutige Aliase, ungültige URL-/Solutionformen und URLs mit Credentials, ohne diese Werte in die Ausgabe zu übernehmen.

`AssemblySourceSelectionOrchestrator` trennt Konfigurationsfehler, Provider-unavailable, Provider-degraded, No-Match und nutzbare Source-Auswahl. Die Tests `AssemblyAnalysisToolSupportTests`, `AssemblyAnalysisContextFactoryTests`, `AssemblyAnalysisConfigurationFailureTests` und `ExternalSourceProviderContractTests` decken sowohl source-backed Auswahl als auch deterministische Decompilation-Fallbacks ab.

## Abdeckungsgrenze SRC-001

- Typ: externe Voraussetzung, kein bestätigter Produktdefekt
- Schweregrad: S3
- Umfang: U3 — reale Provider-/Netzwerkumgebung
- Konfidenz: hoch
- Evidenz: Die statischen/fake-basierten Verträge liegen im Fast-Testbestand vor; ein real erreichbares source-backed Repository mit gültiger Zuordnung wurde in diesem Audit nicht verwendet. Die lokale `external-sources.json`-Beispielkonfiguration wurde nicht als Livequelle behandelt.
- Auswirkung: Erfolgreiche Ende-zu-Ende-Bestätigung der realen Source-Zuordnung, Provider-Credentials und der Rückgabe einer source-backed Roslyn-Compilation bleibt offen. Der reine Decompilation-Fallback wurde separat getestet, aber die Live-Probe war wegen Audit-Scope und Geheimnisschutz nicht reproduzierbar.
- Reproduktion: In einer kontrollierten Testumgebung eine gültige Mappingkonfiguration mit einem lokalen/fake Provider einsetzen und `AssemblyAnalysisSourceToolSupport.ExecuteAsync` über den matched-Selection-Pfad ausführen; für die echte Providerprobe sind sichere Test-Credentials und ein freigegebenes Repository erforderlich.
- Disposition: Als externe Abdeckungsgrenze dokumentiert, nicht in Produktionscode eingegriffen. Vor einer Produktfreigabe ist ein geschützter E2E-Lauf mit redigierter Testquelle nachzuholen.

## Verifikation

Die Tests decken Konfigurationsfehler vor Provider-/Decompilationstart, Provider-Cancellation, typed failures, No-Match, Ambiguität, Lease-Halten bis zum Resultat und deterministische Fallbacks ab. Eine Ausweitung auf echte Zugangsdaten oder externe Dienste wäre außerhalb des Audit-Auftrags.
