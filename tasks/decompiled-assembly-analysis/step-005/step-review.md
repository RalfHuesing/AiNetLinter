---
status: done
type: step-review
task: decompiled-assembly-analysis
step: 005
epic: EPIC-03
step_type: single
reviewed_by: kritiker
reviewed_by_model: gpt-5 (Codex)
reviewed_by_model_knowledge_cutoff: nicht angegeben
reviewed_at: 2026-08-28T17:44:49+02:00
verdict: issues
tech_debt_ids: []
---

# Review Step 005: Expliziten External-Source-Mappingvertrag mit strikter Validierung vorbereiten

## Verdict

- [ ] **approved** — alle vier Prüfebenen ok
- [x] **issues** — Korrektur-Step erforderlich (`corrects: step-005`)
- [ ] **blocked** — Nutzer-Entscheidung nötig

## Geprüft

- [ ] Plan-Erfüllung: der Mapping-/Validierungsschnitt ist weitgehend umgesetzt, enthält aber einen In-Scope-DRY-Verstoß
- [ ] Rules-Konformität: die referenzierten Regeln sind überwiegend eingehalten; die DRY-Regel wird im neuen Produktionscode verletzt
- [ ] Logische Korrektheit: Normalisierung, Zustands- und Providerverträge sind stimmig; die Duplikatdiagnostik hat eine kleinere Ungenauigkeit
- [x] Konzept-Treue: Snapshot-/Session-/MCP-/Gitea-Folgegrenzen und Non-Goals wurden eingehalten
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: beide vollständigen Nicht-Stress-Gates selbst nachgeprüft, grün

## Befund

### Plan-Erfüllung

Die drei geplanten Schichten — immutable Mapping-/Diagnosevertrag mit fokussiertem `appsettings.json`-Loader, injizierbarer Unavailable-Provider sowie netzwerkfreie Loader-/Provider-Tests und minimale Konfigurationsdoku — sind vorhanden. Die Tests verwenden `TestTempDirectory`; `ainetlinter.project.json`, `rules.json`, Session-/MCP-Wiring und die bestehende Assembly-Session wurden nicht erweitert. Der Step-Plan und die CodeMap wurden aktualisiert.

Die Plan-Erfüllung ist dennoch nicht vollständig: Der Coder behauptet in `step-result.md` eine Konsolidierung der gemeinsamen Diagnose-/Duplikatlogik, tatsächlich bestehen drei exakt gleiche `Diagnostic(string, string, string, string)`-Methoden im neuen Konfigurationspaket. Das ist ein autorisierter In-Scope-DRY-Befund und kein Tech-Debt-Fall.

### Rules-Konformität

Die Verträge sind als `sealed record` mit `ImmutableArray` bzw. init-only/readonly Properties modelliert; Fehler werden über explizite Result-/Diagnosewerte transportiert. Der Loader löst Pfade deterministisch relativ zur gelesenen Settings-Datei auf, Tests nutzen die zentrale Temp-Infrastruktur, und der Provider verwendet weder DI noch Netzwerk- oder Runtime-Ladeinfrastruktur. Die gezielten `get_violations`-Abfragen melden für alle fünf neuen Produktionsdateien null Regelverstöße.

Die referenzierte Qualitätsdrift-Regel aus `.agents/rules/AiNetLinterRichtlinien.mdc#5` ist aber verletzt: `Diagnostic` ist in `ExternalSourceJsonValidation`, `ExternalSourceConfigurationLoader` und `ExternalSourceMappingValidator` identisch dupliziert. Der begrenzte AiNetLinter-DRY-Audit meldete im Scope `src/AiNetLinter/Configuration` genau einen `exact`-Cluster mit Score 1,00 und diesen drei Methoden.

### Logische Korrektheit

Fehlende `appsettings.json` bzw. ein fehlender optionaler `ExternalSources`-Abschnitt ergeben eine leere erfolgreiche Konfiguration; ein fehlender/ungültiger Mapping-Pfad, ungültiges Mapping-JSON und Validatorfehler ergeben `Configuration = null` mit sichtbaren strukturierten Diagnosen. Relative Mapping-Dateipfade werden gegen das Settings-Verzeichnis, absolute Pfade per `GetFullPath` aufgelöst. Repository-URLs werden als absolute HTTP(S)-URLs validiert und getrimmt; Solution-Pfade werden slash-normalisiert, intern gefaltete `..`-Segmente werden zugelassen, ein Escape aus dem Repository sowie falsche Endungen werden verworfen; Assembly-Namen werden getrimmt, ohne `.dll`-Suffix gespeichert und case-insensitiv auf interne bzw. übergreifende Duplikate geprüft. Der Port reicht Mapping und Cancellation weiter, und `UnavailableExternalSourceProvider` liefert deterministisch `ProviderUnavailable` ohne Source-, Snapshot- oder Sessionsemantik.

Die Diagnoseprüfung hat eine kleinere Ungenauigkeit: Durch die Kombination aus `ValidateKnownFields` und `TryGetUniqueProperty` werden doppelte JSON-Properties mehrfach diagnostiziert. Beim doppelten Root-Feld `repositories` kommt zusätzlich `RequiredFieldMissing`, obwohl das Feld vorhanden ist (`ExternalSourceMappingValidator.cs:30-44`). Die Konfiguration bleibt dadurch korrekt ungültig, aber der strukturierte Diagnosevertrag beschreibt den konkreten Fehler nicht präzise.

Die neuen Tests decken 17 Testfälle ab (14 Loader-Fälle einschließlich drei Theory-Fällen und drei Provider-Vertragsfälle). Es fehlt ein direkter Regressionstest für doppelte JSON-Properties, leere/Whitespace-Assembly-Namen und defektes `appsettings.json`; die Produktionslogik für leere Assembly-Namen und Settings-JSON ist vorhanden, die Testabdeckung dafür aber nicht direkt nachgewiesen.

### Konzept-Treue (Ebene 4)

Die Umsetzung bleibt bei der expliziten globalen Mapping-Datei mit `url`, `solutionPath` und `assemblies`; `ainetlinter.project.json` wird nicht erweitert und es gibt keine Repository-Discovery. Es wurden keine ausgeschlossenen Non-Goals umgesetzt: kein Assembly-Load, keine Reflection-Ausführung, kein `AssemblyLoadContext`, kein Netzwerk, kein Solution-/Project-Load und keine Snapshot-/Registry-/Session-/MCP-Verdrahtung. Die spätere kanonische Snapshot-Identität und Gitea-Akquisition bleiben damit sauber abgegrenzt. Eine weitergehende URL-Kanonisierung über Trim und HTTP(S)-Validierung hinaus ist in diesem Step nicht erforderlich, weil sie zur späteren Snapshot-/Provider-Identität gehört.

## Findings (nur bei `issues`)

1. `src/AiNetLinter/Configuration/ExternalSourceConfiguration.cs:172-177`, `src/AiNetLinter/Configuration/ExternalSourceConfigurationLoader.cs:251-256` und `src/AiNetLinter/Configuration/ExternalSourceMappingValidator.cs:383-388` — **[MAJOR] [Rules-Konformität / Plan-Erfüllung]** Die drei neuen Produktionsklassen enthalten den exakt gleichen Diagnose-Fabrikcode. Das verletzt die referenzierte DRY-Regel und widerspricht der in `step-result.md` dokumentierten Konsolidierung. **Fix:** Eine einzige gemeinsame `Diagnostic`-Fabrik im Mapping-/Diagnosevertrag beibehalten bzw. zugreifbar machen und die beiden lokalen Kopien entfernen; alle drei Aufrufer müssen weiterhin denselben Code, die Nachricht, den Schweregrad `error` und die Fundstelle transportieren.

## Sonstige Beobachtungen / MINOR / NITPICK

- `src/AiNetLinter/Configuration/ExternalSourceConfigurationLoader.cs:106-113`, `src/AiNetLinter/Configuration/ExternalSourceMappingValidator.cs:30-44,103-127` — **[MINOR] [Logik]** Die Schichten erkennen doppelte JSON-Properties teilweise parallel; dadurch entstehen doppelte `DuplicateField`-Diagnosen und beim doppelten `repositories`-Feld ein irreführendes `RequiredFieldMissing`. Die Korrekturrunde sollte die Duplikaterkennung genau einer Helper-Schicht überlassen und `RequiredFieldMissing` nur bei tatsächlich fehlenden Feldern erzeugen.
- `src/AiNetLinter.FastTests/Configuration/ExternalSourceConfigurationLoaderTests.cs` — **[MINOR] [Plan-Erfüllung]** Die Implementierung behandelt leere Assembly-Namen und defektes Settings-JSON, aber dafür fehlen direkte Regressionstests; ein Test für duplicate JSON-Properties würde den Diagnosebefund zusätzlich absichern.

### Tech-Debt-Prüfung

Der `find_duplicates`-Cluster im Konfigurationsordner ist wegen seines direkten Bezugs zum neuen Mapping-/Validierungscode als blockierendes In-Scope-Finding erfasst, nicht als Tech-Debt. Der separate Assembly-Ordner lieferte keinen Duplikat-Cluster. Der Magic-Values-Audit meldete nur 21 einzelne Diagnosecode-Strings in bewusst benannten `const`-Feldern; daraus ergibt sich kein architektonisch sinnvoller neuer Befund. Die Dead-Code-Heuristik meldete `ExternalSourceConfigurationLoader.Load()` nur mit LOW-Confidence; dieser Einstieg ist laut Plan die beabsichtigte spätere Loader-Grenze. Ein weiterer LOW-Fund (`AssemblyOrigin.Kind`) liegt im alten Assembly-Code außerhalb des Step-Scopes und ist kein neuer Befund. Es wird daher kein `tech-debt.md` angelegt oder geändert.

### Sicherheits- und Scope-Prüfung

Die gezielte Textprüfung der sieben neuen Code-/Testdateien fand keine `Assembly.Load`-, `AssemblyLoadContext`-, Reflection-, Netzwerk-, Prozessstart- oder Solution-Load-Muster. Die AiNetLinter-Abhängigkeitsgraphen zeigen für Loader und Provider nur den neuen Konfigurationsvertrag bzw. den Provider-Port; es gibt keine neue Abhängigkeit in Session-, Snapshot-, MCP- oder Gitea-Komponenten.

### Build-/Test-Status

```text
dotnet build → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress → grün (1885 Tests, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → grün (360 Tests, 0 Fehler; Dauer 2 m 33 s)
```

Stress-Tests wurden nicht ausgeführt.
