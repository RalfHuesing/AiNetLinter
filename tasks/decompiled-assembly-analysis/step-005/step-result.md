---
status: done (pending audit)
type: step-result
task: decompiled-assembly-analysis
step: 005
epic: EPIC-03
step_type: single
coded_by: coder
coded_by_model: gpt-5 (Codex)
coded_by_model_knowledge_cutoff: nicht angegeben
coded_at: 2026-08-28T17:32:00+02:00
code_commit_hash: 7d40cacb
status_after: done (pending audit)
blocker_category: n/a
---

# Result Step 005: Expliziten External-Source-Mappingvertrag mit strikter Validierung vorbereiten

## Zusammenfassung

Der Step führt einen unveränderlichen External-Source-Mappingvertrag mit fokussiertem
`appsettings.json`-Loader und strikter JSON-, Pfad- und Assembly-Validierung ein.
Ungültige Mappings werden mit Code, Severity, Nachricht und Fundstelle sichtbar
zurückgegeben und nicht als Provider-Eingang weitergereicht. Der kleine Provider-Port
inklusive nicht verfügbarem Default-Adapter bleibt frei von Netzwerk-, Solution- und
Sessionsemantik.

## Geänderte Dateien

- `src/AiNetLinter/Configuration/ExternalSourceConfiguration.cs` — immutable Records, Result-/Diagnosevertrag, Diagnosecodes und gemeinsamer JSON-Feldhelfer.
- `src/AiNetLinter/Configuration/ExternalSourceConfigurationLoader.cs` — fokussiertes Laden von `appsettings.json` und der referenzierten Mapping-Datei mit deterministischer Pfadauflösung.
- `src/AiNetLinter/Configuration/ExternalSourceMappingValidator.cs` — Validierung von Repository-URL, Solution-Pfad, Assembly-Normalisierung und Duplikaten.
- `src/AiNetLinter/Mcp/Assemblies/IExternalSourceProvider.cs`, `UnavailableExternalSourceProvider.cs` — injizierbarer Provider-Port und netzwerkfreier Unavailable-Adapter.
- `src/AiNetLinter.FastTests/Configuration/ExternalSourceConfigurationLoaderTests.cs` — netzwerkfreie Unit-/Component-Tests für Loader und Validator.
- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceProviderContractTests.cs` — Provider-Vertragstests für Mapping-Übergabe, Cancellation und sichtbare Nichtverfügbarkeit.
- `Docs/configuration.md` — minimale Beschreibung des implementierten External-Source-Mappingvertrags.
- `tasks/decompiled-assembly-analysis/codemap.md` — Pointer für Mapping-/Provider-Code und die neuen Tests ergänzt.
- `tasks/decompiled-assembly-analysis/step-005/step-plan.md` — Status auf `done (pending audit)` gesetzt.

## Commit

- **Code-Commit-Hash:** `7d40cacb`
- **Message:**
  ```
  feat: External-Source-Mapping strikt validieren [decompiled-assembly-analysis]

  Refs: tasks/decompiled-assembly-analysis/step-005
  ```
- **Branch:** `main`
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit.

## Build-/Test-Output

- `dotnet build` → grün, 0 Warnungen, 0 Fehler.
- `dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~ExternalSourceConfigurationLoaderTests|FullyQualifiedName~ExternalSourceProviderContractTests" --no-restore` → grün, 17/17 Tests.
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` → grün, 1.885/1.885 Tests.
- `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` → ohne Fehlerausgabe beendet; Stress nicht ausgeführt.
- `find_duplicates` im neuen Configuration-Bereich → keine Duplikat-Cluster.
- `get_violations` im neuen Configuration-Bereich → 0 Regelverstöße.

## Abweichungen vom Plan

Die gemeinsame JSON-Feld- und Duplikatprüfung wurde als kleiner wiederverwendbarer
Helper in `ExternalSourceConfiguration.cs` gebündelt, weil der Drift-Audit dieselbe
Logik zunächst in Loader und Validator gefunden hatte. Zusätzlich wurden ungenutzte
Komfort-Aliase im Vertrag entfernt; der explizite `Load()`-Einstieg bleibt als
beabsichtigte spätere Loader-Grenze erhalten. Die geplante MCP-/Session-Verdrahtung,
Source-Akquisition und Snapshot-Semantik wurden nicht erweitert.

## Tech-Debt

Im neu angelegten Mapping-/Validierungsbereich verblieben nach der Konsolidierung
keine Duplikat-Cluster und keine Linter-Verstöße. Die verbleibende Low-Confidence-
Dead-Code-Heuristik für den noch nicht verdrahteten `Load()`-Einstieg wurde als
beabsichtigte zukünftige Integrationsgrenze nicht künstlich entfernt.

## Beobachtungen

Der Default-Provider meldet deterministisch `ProviderUnavailable` und führt weder
Netzwerkzugriffe noch Solution-/Assembly-Ladevorgänge aus. Die Existenz und der
Inhalt des repository-relativen `solutionPath` bleiben wie geplant Aufgabe des
späteren Provider-/Snapshot-Schnitts.

## Bekannte Unschärfen

Der Integration-Test-Runner wurde nach dem vollständigen Nicht-Stress-Lauf beendet
und zeigte keine Fehlerausgabe, der Terminal-Wrapper gab jedoch keine abschließende
Testzählung aus. Die neue Implementierung berührt keine Integrationstest- oder
Sessionverdrahtung; der Kritiker sollte den finalen Gate-Nachweis bei Bedarf anhand
seines eigenen Auditlaufs ergänzen.
