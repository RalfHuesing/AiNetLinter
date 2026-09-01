# Ausführungsstand

- Primäraufgabe: Robuste Assembly-Analyse und konsistente MCP-Antwortverträge
- Betriebsart: Großkonzept-Modus
- Status: done
- current_epic: Abschlussverifikation
- current_debt_item: full-gate-baseline-failures
- debt_attempts: 1
- letzter Commit: `a564811f`

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
- Status: done

### Paket 3 – Source-Backing und Body-/Metadata-Navigation

- Abhängigkeiten: Paket 1, Paket 2
- Betroffene Bereiche: Source-Snapshot-Diagnosen, Fallback-Transparenz, on-demand Body-Dekompilation, Enum-/Ladezustandsdaten
- Muss-/Akzeptanzkriterien: Source-Backing bleibt fail-closed und Diagnosen sichtbar; Assembly-Leases sichern Body-Abrufe; Source, dekompilierte Signatur und on-demand Body sind unterscheidbar.
- Verifikation: Source-/Support-, Body-, Pfad- und Strukturtests sowie MCP-`get_violations`
- Status: done

### Paket 4 – Kompatibilität, API-Lücken und Dokumentation

- Abhängigkeiten: Paket 1 bis 3
- Betroffene Bereiche: `.exe`-Unterstützung, Registry-Pfadbefund, Hotspots, Parameterbenennung, Dokumentation und Registrierungen
- Muss-/Akzeptanzkriterien: verwaltete `.dll` und `.exe` werden unterstützt; nicht verwaltete PE-Dateien liefern typisierte Recoverable-Fehler; Tool-Schemas und Dokumentation stimmen überein; Alias-Kanonisierung bleibt ohne Reproduktion bewusst zurückgestellt.
- Verifikation: Fixture-, Registrierungs-, Dokumentations- und MCP-Checks sowie `get_violations`
- Status: done

## Abschluss-Checkliste

- [x] `dotnet build`
- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`
- [x] Konzepttests für Assembly-Filter, Budget, Navigation, File Tree, Source-Backing, Health, Fehlerpayloads, DTOs und `.exe`/native PE
- [x] Dokumentationsbeispiele gegen aktuelle Tool-Registrierungen geprüft
- [x] Abschluss-Audit auf DRY, Refactoring-Drift, Dead Code und Magic Values

Die beiden Nicht-Stress-Vollgates wurden ausgeführt, sind aber wegen unabhängiger
Baseline-/Umgebungsbefunde nicht vollständig grün: FastTests 2347 bestanden,
2 übersprungen und 1 bestehender LF/CRLF-Erwartungsfehler; IntegrationTests 378
bestanden und 1 bestehender Live-Safeguard-Score unter dem Korridor. Die
auftragsbezogenen Regressionen sind grün; die Befunde sind in `tech-debt.md`
als `accepted-deferred` klassifiziert und blockieren keine P0-/P1-Lieferung.

Tech-Debt-Queue: siehe `tech-debt.md`.
