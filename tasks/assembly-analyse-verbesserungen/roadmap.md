## Primäraufgabe

Verbessere die verifizierte, agentenfreundliche Source-Backed-Analyse externer Assemblies.

## Betriebsart

Großkonzept-Modus. Grundlage ist `Konzept.md` mit `status: ready`.

## Epics

### Epic 1: Analysevertrag und Agenten-UX

- Ziel: Source-/Vollständigkeitsvertrag, strukturierte Assembly-Antworten, Antwortbudgets, stabile Identitäten, Paging und den Composite-Einstieg konsistent umsetzen.
- Abhängigkeiten: aktuelle Assembly-Session-/Response-Verträge und bestehende Tests.
- Betroffene Bereiche: Assembly-Analyse-MCP, DTOs/Envelopes, Navigation, Capabilities, Fast-/Integration-Tests.
- Muss-/Akzeptanzkriterien: Konzept-Kriterien 1, 4, 8, 10–19, 21–26, 30; insbesondere rückwärtskompatible CLR-/Wire-Verträge und sichtbare Truncation/Scope-/Provenienzangaben.
- Verifikation: gezielte MCP-Checks, Contract-/Fast-Tests und passende Integrationstests nach der letzten Codeänderung.
- Status: done (ein P1 `BudgetProjection/Envelope` nach fünf Versuchen `accepted-deferred`; übrige P1 fixed)

### Epic 2: Source, Cache und Mehrdaemon-Betrieb

- Ziel: Verifizierten Git-Source-Pfad, Hard-Error-/`source_required`-Semantik, deterministische Profil-Caches, Prozesskoordination, Cleanup und Quarantäne belastbar machen.
- Abhängigkeiten: Epic 1 für gemeinsame Herkunfts-, Status- und Diagnoseverträge.
- Betroffene Bereiche: Source-Provider, Repository-Acquirer, Cache/Generation/Locking, Health/Diagnose, Referenz-Session-Lebenszyklus, Tests.
- Muss-/Akzeptanzkriterien: Konzept-Kriterien 1–9, 18–19, 27–29; insbesondere kein unbestätigter Source- oder Cachezustand und Windows-resiliente Bereinigung.
- Verifikation: Source-Backed- und Fehler-Matrix, Cache-/Cleanup-/Mehrdaemon-Integration sowie gezielte MCP-Prüfungen.
- Status: done (Implementierung, echte Zwei-Prozess-Lease-Verifikation und unabhängige Review approved; P2 IPC-Probe accepted-deferred)

### Epic 3: Suche, E2E-Verifikation und Dokumentation

- Ziel: Assembly-Volltext-/Datenzugriffs-Suche, reale MCP-End-to-End-/Mehrdaemon-Regressionen und konsistente Anwender-/Integrationsdokumentation abschließen.
- Abhängigkeiten: Epic 1 und Epic 2.
- Betroffene Bereiche: Assembly-Suche, MCP-Registrierungen/Capabilities, Testinfrastruktur, `Docs/integration.md`, betroffene MCP-Vertragsdokumentation.
- Muss-/Akzeptanzkriterien: Konzept-Kriterien 10–30, insbesondere 15, 20–24 und die vollständige Source-/Cache-/Antwortbudget-Abdeckung.
- Verifikation: gezielte E2E-/Integration-/Stress-Tests nach Kategorien, Dokumentationsabgleich, anschließend vollständige Nicht-Stress-Gates.
- Status: in_progress (Implementierung abgeschlossen; Review ausstehend)

## Abschluss-Checkliste

- [ ] Konzept-Verifikationsbereiche A/A1/A2/A3/A4, B, C, D, E, F und G ausgeführt oder gemäß Konzept-Fallback dokumentiert.
- [ ] `dotnet build` nach dem letzten Codezustand grün.
- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` nach dem letzten Codezustand grün.
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` nach dem letzten Codezustand grün.
- [ ] Abschluss-Audit ausgeführt und Findings disponiert.

## Ausführungsstand

- current_epic: Epic 3 Review
- letzter Commit: `2866bd40` (Epic-2-Abschlusscheckpoint); Epic-3-Checkpoint folgt
- current_debt_item: P1 `BudgetProjection/Envelope` in `tech-debt.md`, `accepted-deferred`, Attempts 0
- debt_attempts: 0
- Blocker: keiner
