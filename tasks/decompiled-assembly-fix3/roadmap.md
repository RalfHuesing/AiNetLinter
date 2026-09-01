# Ausführungsstand

- Primäraufgabe: Robuste Assembly-Analyse und konsistente MCP-Antwortverträge
- Betriebsart: Großkonzept-Modus
- Status: executing
- current_epic: Paket 2 – Progressive Disclosure, Diagnosen und Health
- current_debt_item: package2-regression-test-contract-drift
- debt_attempts: 4
- letzter Commit: `20bd5cc1`

## Epics

### Paket 1 – Vertragsintegrität und P1-Korrektheit

- Abhängigkeiten: keine
- Betroffene Bereiche: typisierte Fehlerpayloads, Budgetprojektion, Receiver-Filter, Folge-ID/Pfadauflösung, `get_file_tree`
- Muss-/Akzeptanzkriterien: Fehlerdaten sind strukturiert und behalten die bestehende `isError`-Policy; Text und JSON verwenden dieselbe Auswahl; Receiver- und Folgeaufruf-Verträge funktionieren; Baumtiefe und Summary-Grenzen stimmen.
- Verifikation: gezielte Assembly-, Budget-, Navigations- und File-Tree-Tests sowie MCP-`get_violations` nach der letzten Codeänderung
- Status: done

### Paket 2 – Progressive Disclosure, Diagnosen und Health

- Abhängigkeiten: Paket 1
- Betroffene Bereiche: Assembly-Detailflags, Diagnoseprojektionen, globaler Health, strukturierte Erfolgspayloads
- Muss-/Akzeptanzkriterien: gezielte Antworten bleiben kompakt; Diagnose-Samples sind begrenzt und transparent; globaler Health aggregiert standardmäßig; text-only Erfolgsdaten sind strukturiert verfügbar.
- Verifikation: gezielte Tool-/DTO-/Registrierungs- und Health-Tests sowie MCP-`get_violations`
- Status: in_progress

### Paket 3 – Source-Backing und Body-/Metadata-Navigation

- Abhängigkeiten: Paket 1, Paket 2
- Betroffene Bereiche: Source-Snapshot-Diagnosen, Fallback-Transparenz, on-demand Body-Dekompilation, Enum-/Ladezustandsdaten
- Muss-/Akzeptanzkriterien: Source-Backing bleibt fail-closed und Diagnosen sichtbar; Assembly-Leases sichern Body-Abrufe; Source, dekompilierte Signatur und on-demand Body sind unterscheidbar.
- Verifikation: Source-/Support-, Body-, Pfad- und Strukturtests sowie MCP-`get_violations`
- Status: open

### Paket 4 – Kompatibilität, API-Lücken und Dokumentation

- Abhängigkeiten: Paket 1 bis 3
- Betroffene Bereiche: `.exe`-Unterstützung, Registry-Pfadbefund, Hotspots, Parameterbenennung, Dokumentation und Registrierungen
- Muss-/Akzeptanzkriterien: verwaltete `.dll` und `.exe` werden unterstützt; nicht verwaltete PE-Dateien liefern typisierte Recoverable-Fehler; Tool-Schemas und Dokumentation stimmen überein; Alias-Kanonisierung bleibt ohne Reproduktion bewusst zurückgestellt.
- Verifikation: Fixture-, Registrierungs-, Dokumentations- und MCP-Checks sowie `get_violations`
- Status: open

## Abschluss-Checkliste

- [ ] `dotnet build`
- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`
- [ ] Konzepttests für Assembly-Filter, Budget, Navigation, File Tree, Source-Backing, Health, Fehlerpayloads, DTOs und `.exe`/native PE
- [ ] Dokumentationsbeispiele gegen aktuelle Tool-Registrierungen geprüft
- [ ] Abschluss-Audit auf DRY, Refactoring-Drift, Dead Code und Magic Values

Tech-Debt-Queue: siehe `tech-debt.md`.
