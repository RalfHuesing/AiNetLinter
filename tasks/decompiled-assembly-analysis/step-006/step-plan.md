---
status: open
type: step-plan
task: decompiled-assembly-analysis
step: 006
corrects: step-005
title: "Mapping-Diagnosevertrag und direkte JSON-Regressionen korrigieren"
epic: EPIC-03
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: gpt-5 (Codex)
created_by_model_knowledge_cutoff: nicht angegeben
created_at: 2026-08-28T17:55:04+02:00
related_to: [step-005/step-review.md]
context_budget:
  read_first:
    - "tasks/decompiled-assembly-analysis/step-005/step-review.md"
    - "tasks/decompiled-assembly-analysis/step-005/step-plan.md"
    - "tasks/decompiled-assembly-analysis/step-005/step-result.md"
    - "src/AiNetLinter/Configuration/ExternalSourceConfiguration.cs"
    - "src/AiNetLinter/Configuration/ExternalSourceConfigurationLoader.cs"
    - "src/AiNetLinter/Configuration/ExternalSourceMappingValidator.cs"
    - "src/AiNetLinter.FastTests/Configuration/ExternalSourceConfigurationLoaderTests.cs"
    - "src/AiNetLinter.TestKit/TestTempDirectory.cs"
    - ".agents/rules/AiNetLinter-McpWorkflow.mdc"
    - ".agents/rules/AiNetLinterRichtlinien.mdc"
    - ".agents/rules/AiNetLinter.mdc"
    - "tasks/decompiled-assembly-analysis/codemap.md"
  read_on_demand:
    - "tasks/decompiled-assembly-analysis/Konzept.md — nur die Mapping-/Validierungsabschnitte, falls eine Invariante aus dem Handoff unklar ist"
    - "src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceProviderContractTests.cs — nur zur Bestätigung, dass der Provider-Vertrag unverändert bleibt"
    - "src/AiNetLinter/Mcp/Assemblies/IExternalSourceProvider.cs und UnavailableExternalSourceProvider.cs — nur zur Scope-Prüfung, nicht zur Änderung"
    - "appsettings.json und Docs/configuration.md — nur falls die Implementierung wider Erwarten den dokumentierten Konfigurationsvertrag verändert"
    - "rules.json — nur bei einem direkt berührten MagicValues-/DeadCode-Fund im Mapping-/Validierungscode"
  out_of_scope:
    - "Jede Änderung an Snapshot-, Source-Cache-, Revision-, Lease-, TTL-, Generation- oder Session-Grenzen"
    - "AssemblyAnalysisSession, AssemblyAnalysisContextFactory, AnalysisToolCall, MCP-Registrierungen, Daemon-/Projekt-Wiring und externe Provider-Portmodelle"
    - "Gitea-Clone/Fetch, Authentifizierung, Refresh, Netzwerk, Source-of-Truth und vollständige Solution-/Project-Auflösung"
    - "Änderung des External-Source-Schemas, von Pfadauflösung, Assembly-Matching, Provider-Verfügbarkeit oder vorhandener Konfigurationsdokumentation"
    - "Breiter DRY-, MagicValues- oder DeadCode-Sweep; nur ein direkt betroffener Fund im Mapping-/Validierungscode darf opportunistisch mitbereinigt werden"
    - "Änderungen an task-state.md, roadmap.md, codemap.md, Produktionsdateien durch diesen Planer, neuen Tech-Debt-Einträgen oder weiteren Steps"
---

# Step 006: Mapping-Diagnosevertrag und direkte JSON-Regressionen korrigieren

## Bezug

- **Task:** `decompiled-assembly-analysis`
- **Epic:** `EPIC-03` aus `roadmap.md` — Korrektur des bereits umgesetzten
  Mapping-/Validierungsschnitts aus Step 005; der Snapshot-/Session-Folge-Schnitt
  bleibt offen.
- **Korrektur:** `step-005/step-review.md` mit Verdict `issues`; dieser Step
  bündelt ausschließlich die dort aufgeführten MAJOR-/MINOR-Befunde.
- **Konzept-Referenz:** `Konzept.md` „Gitea-Register und wartungsarmes Mapping“
  und „Source-Auflösung vor der Dekompilation“ — explizite, sichtbare und
  konservative Mappingdiagnosen ohne neue Source- oder Sessionsemantik.
- **Split-Gate:** Genau ein primärer Vertrag, drei unmittelbar betroffene
  Schichten und höchstens acht Akzeptanzkriterien. Es wird kein neues
  Snapshot-, Session-, MCP-, Gitea- oder Provider-Fachmodell geplant.

## Aktueller Projektzustand (JIT-Kontext)

Die semantische MCP-Prüfung gegen das absolute Projektroot
`C:\Daten\Entwicklung\Ralf\AiNetLinter` zeigt den vorhandenen Vertrag und seine
Grenzen:

- `ExternalSourceConfigurationDiagnostic` liegt bereits im Mapping-
  Vertragsfile `ExternalSourceConfiguration.cs`; die drei Klassen
  `ExternalSourceJsonValidation`, `ExternalSourceConfigurationLoader` und
  `ExternalSourceMappingValidator` erzeugen derzeit trotzdem jeweils dieselbe
  vierparametrige private `Diagnostic`-Methode.
- `ExternalSourceJsonValidation.ValidateKnownFields` diagnostiziert ein
  wiederholtes Property, während `TryGetUniqueProperty` dasselbe wiederholte
  Property erneut diagnostiziert. In `ExternalSourceMappingValidator.Validate`
  wird das daraus resultierende `false` zusätzlich wie ein fehlendes
  `repositories`-Feld behandelt.
- `ExternalSourceConfigurationLoaderTests` deckt bereits Mapping-JSON-Fehler,
  Assembly-Duplikate und strukturierte Diagnosen ab, enthält aber keinen direkten
  Test für doppelte JSON-Properties, leere/Whitespace-Assembly-Namen oder
  defektes `appsettings.json`.
- Der Provider-Port und die Assembly-Session haben keine fachliche Abhängigkeit
  von dieser Korrektur. Die Korrektur bleibt auf Mappingdiagnose und deren
  direkte Tests beschränkt.

Die vorhandene `ExternalSourceJsonValidation`-Schicht wird daher erweitert und
wiederverwendet; es wird weder ein zweiter Diagnosevertrag noch ein paralleler
Provider-/Sessionpfad eingeführt.

## Intention

Der bestehende Mapping-/Diagnosevertrag erhält genau eine gemeinsame Fehlerfabrik
für Severity- und Fundstellenformatierung. Die JSON-Validierung unterscheidet
fehlende, eindeutige und doppelte Properties so, dass ein doppeltes Property
genau eine `DuplicateField`-Diagnose erzeugt und niemals zusätzlich als fehlend
gemeldet wird. Drei direkte, temporär isolierte Regressionen sichern diese
Diagnosekorrektur sowie die bereits vorhandene Behandlung leerer Assembly-Namen
und defekter Settings-JSON-Dateien ab.

## Kontext-Handoff

### Invarianten

- Der primäre Vertrag bleibt der bestehende immutable
  `ExternalSourceConfigurationDiagnostic`-/JSON-Validierungsvertrag; die
  Mappingfelder `url`, `solutionPath` und `assemblies` sowie ihre Codes bleiben
  unverändert.
- Eine vorhandene Property ist niemals `RequiredFieldMissing`: bei genau einer
  Property wird ihr Wert validiert, bei mehreren wird genau einmal
  `DuplicateField` gemeldet und die Property gilt als nicht eindeutig.
- `RequiredFieldMissing` entsteht ausschließlich beim Status `Missing`; ein
  doppeltes `repositories`, `ExternalSources`, `MappingsPath`, `url`,
  `solutionPath` oder `assemblies` darf diesen Code nicht zusätzlich erzeugen.
- Ungültige Mappings liefern weiterhin kein verwendbares
  `ExternalSourceConfiguration`; fehlendes optionales `ExternalSources` bleibt
  die erfolgreiche leere Konfiguration.
- Der Provider erhält keine neue Information und wird nicht verdrahtet; es gibt
  weiterhin keinen Snapshot-, Revision-, Session-, MCP- oder Gitea-Vertrag in
  diesem Step.
- Es gibt keine Assembly-Ausführung, kein Reflection-/ALC-Laden, keinen
  Netzwerkzugriff und keinen breiten Qualitäts-Sweep.

### Relevante MCP-Symbole

- `T:AiNetLinter.Configuration.ExternalSourceConfigurationDiagnostic` —
  bestehender unveränderlicher Diagnosewert; sicherer Ort für die eine
  gemeinsame Fehlerfabrik.
- `T:AiNetLinter.Configuration.ExternalSourceJsonValidation` — bestehender
  JSON-Helper mit `ValidateKnownFields` und `TryGetUniqueProperty`; Eigentümer
  der zentralen Property-Status-/Duplikatlogik.
- `T:AiNetLinter.Configuration.ExternalSourceConfigurationLoader` — lädt
  `appsettings.json` und reicht Mappingdiagnosen weiter.
- `T:AiNetLinter.Configuration.ExternalSourceMappingValidator` — validiert
  Mapping-Properties und required-field-Zustände.
- `T:AiNetLinter.FastTests.Configuration.ExternalSourceConfigurationLoaderTests`
  — vorhandener deterministischer Component-Testpunkt mit
  `TestTempDirectory`.

### Sicherer Einstiegspunkt

Zuerst im bestehenden Diagnosewert in
`ExternalSourceConfiguration.cs` eine einzige interne Fehlerfabrik mit der
aktuellen Code-/Message-/Severity-/Location-Semantik verankern und alle drei
lokalen `Diagnostic`-Kopien auf diesen Aufruf umstellen. Danach die bereits
vorhandene `ExternalSourceJsonValidation`-Schicht so ordnen, dass pro JSON-
Objekt nur ein Property-Scan den Status `Missing`, `Unique` oder `Duplicate`
besitzt; Loader und Validator konsumieren diesen Status, ohne erneut
`DuplicateField` oder `RequiredFieldMissing` zu erzeugen. Abschließend nur die
direkten Loader-Regressionen mit `TestTempDirectory` ergänzen. Nicht in
Provider-, Session- oder MCP-Komposition einsteigen.

## Konkrete Änderungen

### Schicht 1 — Gemeinsamer Mapping-/Diagnosevertrag

#### `src/AiNetLinter/Configuration/ExternalSourceConfiguration.cs`

- **Was:** Im bestehenden `ExternalSourceConfigurationDiagnostic`-Vertrag eine
  einzige interne Fehlerfabrik (z. B. `CreateError`) für `code`, `message`,
  `sourcePath` und `jsonPath` verankern. Sie setzt zentral `Severity = "error"`
  und das bestehende Fundstellenformat; die drei lokalen
  `Diagnostic(string, string, string, string)`-Methoden werden entfernt.
- **Was:** `ExternalSourceJsonValidation` bleibt die einzige JSON-Helper-Schicht
  für Property-Ermittlung und Duplikatbesitz. Die Property-Auswertung muss
  fehlend/eindeutig/doppelt unterscheidbar machen, ohne ein neues fachliches
  Mappingmodell einzuführen; `DuplicateField` wird bei einem wiederholten Namen
  exakt einmal aus dieser Schicht erzeugt.
- **Warum:** Diagnoseformatierung und Duplikatsemantik gehören an eine
  gemeinsame, leicht prüfbare Grenze. Loader und Validator sollen keine
  semantisch konkurrierenden Diagnosefabriken oder Property-Scans besitzen.

### Schicht 2 — Loader-/Validator-Verbraucher

#### `src/AiNetLinter/Configuration/ExternalSourceConfigurationLoader.cs`

- **Was:** Alle bisherigen `Diagnostic(...)`-Aufrufe auf die gemeinsame Fabrik
  umstellen. `ExternalSources` und `MappingsPath` verwenden den zentralen
  Property-Status; ein doppeltes Property wird als Duplicate-Fall propagiert,
  ohne `RequiredFieldMissing` oder eine zweite `DuplicateField`-Diagnose.
- **Was:** Die bestehende Semantik für fehlendes optionales
  `ExternalSources`, ungültiges Mapping-JSON und Pfadauflösung unverändert
  lassen; nur die Diagnosezählung und die Unterscheidung „fehlt“ versus „nicht
  eindeutig“ korrigieren.

#### `src/AiNetLinter/Configuration/ExternalSourceMappingValidator.cs`

- **Was:** Alle bisherigen `Diagnostic(...)`-Aufrufe auf die gemeinsame Fabrik
  umstellen. `repositories` sowie `url`, `solutionPath` und `assemblies`
  behandeln den Property-Status konsistent; ein vorhandenes doppeltes Feld
  erzeugt keine Required-Diagnose.
- **Was:** Die vorhandene Prüfung für tatsächlich fehlende required Properties
  ausdrücklich erhalten und gegen den Status `Missing` absichern. URL-,
  Solution-, Assembly-Normalisierung, Duplikat-/Ambiguitätscodes und Result-
  Vertrag bleiben ansonsten unverändert.

### Schicht 3 — Direkte Regressionstests

#### `src/AiNetLinter.FastTests/Configuration/ExternalSourceConfigurationLoaderTests.cs`

- **Was:** Einen direkten Test für doppelte JSON-Properties ergänzen, mindestens
  mit zweimaligem Root-Feld `repositories`; assertieren, dass die Konfiguration
  ungültig ist, genau eine `DuplicateField`-Diagnose für dieses Feld entsteht
  und kein `RequiredFieldMissing` für das vorhandene Feld entsteht. Einen
  echten Missing-Fall separat so absichern, dass nur der fehlende Status den
  Required-Code erzeugt.
- **Was:** Einen direkten `[Theory]`-Fall für leere und Whitespace-
  Assembly-Namen ergänzen und `AssemblyNameInvalid` bei weiterhin ungültiger
  Konfiguration prüfen.
- **Was:** Einen direkten Test für defektes `appsettings.json` ergänzen und
  `SettingsJsonInvalid`, fehlende Konfiguration sowie die Settings-Fundstelle
  prüfen; die bestehende Mapping-JSON-Regression bleibt davon getrennt.
- **Warum:** Die drei vom Kritiker benannten Eingabeklassen werden an der
  öffentlichen Loader-Grenze reproduzierbar abgesichert, ohne neue Fixture-,
  Provider- oder MCP-Infrastruktur.

## Tests

- [ ] `ExternalSourceConfigurationLoaderTests` — doppelte
  `repositories`-Properties: genau eine `DuplicateField`-Diagnose und keine
  irreführende `RequiredFieldMissing`-Diagnose.
- [ ] `ExternalSourceConfigurationLoaderTests` — tatsächlich fehlendes
  `repositories`-Feld: `RequiredFieldMissing` bleibt erhalten und wird nicht
  mit einem Duplicate-Fall vermischt.
- [ ] `ExternalSourceConfigurationLoaderTests` — leere und Whitespace-
  Assembly-Namen als direkte `[Theory]`-Regression mit `AssemblyNameInvalid`.
- [ ] `ExternalSourceConfigurationLoaderTests` — defektes `appsettings.json`
  mit `SettingsJsonInvalid` und strukturierter Settings-Fundstelle.
- [ ] Schneller gezielter Testlauf des Configuration-Testbereichs grün;
  ausschließlich `TestTempDirectory`, kein OS-Temp-Pfad, kein Netzwerk und
  kein Restore eines Fremdprojekts.
- [ ] Abschlussverifikation nach Implementierung: `dotnet build`,
  `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` und
  `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`;
  Stress bleibt ausgeschlossen.

## Definition of Done / Akzeptanzkriterien

- [ ] Eine einzige gemeinsame Fehlerfabrik im bestehenden
  Mapping-/Diagnosevertrag wird von `ExternalSourceJsonValidation`, Loader und
  Validator verwendet; die drei identischen lokalen `Diagnostic`-Methoden sind
  entfernt.
- [ ] Fehlercode, Nachricht, Severity `error` und Fundstellenformat bleiben für
  alle bestehenden Diagnosepfade stabil.
- [ ] Jedes doppelte JSON-Property erzeugt genau eine `DuplicateField`-
  Diagnose; die beteiligten Schichten erzeugen weder Duplikatdiagnosen doppelt
  noch zusätzliche Missing-Diagnosen.
- [ ] `RequiredFieldMissing` wird nur bei tatsächlich fehlenden required
  Properties erzeugt; ein doppeltes vorhandenes `repositories`-Feld erzeugt
  diesen Code nicht.
- [ ] Leere und Whitespace-Assembly-Namen sind durch einen direkten Test als
  `AssemblyNameInvalid` abgedeckt und bleiben für den Provider unbrauchbar.
- [ ] Defektes `appsettings.json` ist durch einen direkten Test als
  `SettingsJsonInvalid` mit korrekter Fundstelle abgedeckt.
- [ ] Der Mapping-/Validator-Resultvertrag, optionale leere Konfiguration,
  Pfadauflösung und Assembly-Normalisierung bleiben unverändert; es entsteht
  kein neues Snapshot-, Session-, MCP-, Gitea- oder Provider-Fachmodell.
- [ ] Build und beide vollständigen Nicht-Stress-Testläufe sind grün; keine
  Änderungen an `task-state.md`, keine breiten Qualitäts-Sweeps und keine
  Runtime-/Netzwerk-/Assembly-Ausführung wurden eingeführt.

## Rules-Refs

- `.agents/rules/AiNetLinter-McpWorkflow.mdc#Verbindliche Priorität` und
  `#Werkzeugwahl` — C#-Symbole und Aufrufer zuerst semantisch über MCP mit dem
  absoluten Projektroot prüfen; Textsuche bleibt ergänzend.
- `.agents/rules/AiNetLinterRichtlinien.mdc#1 Grundprinzipien` und
  `#5 Qualitätsdrift-Prävention` — kleine verständliche Wiederverwendung,
  Result-/Diagnosewerte und DRY-Konsolidierung ohne breiten Sweep.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4 Updates & Tests` — xUnit-v3,
  zentrales `TestTempDirectory`, keine Ad-hoc-MCP-/Temp-Skripte und vollständige
  Nicht-Stress-Gates.
- `.agents/rules/AiNetLinter.mdc#DuplicateCode` und
  `#agent-resilience` — identische Produktionshelper konsolidieren und keine
  neue stille Fehlerbehandlung einführen.
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/skills/planer/SKILL.md#Fix-Modus`
  — flacher neuer Korrektur-Step, `corrects: step-005`, Review-Findings-only-
  Scope und unveränderte Roadmap im Fix-Modus.

## Bekannte Ausnahmen

- `roadmap.md` wird nicht geändert: EPIC-03 bleibt offen, und die
  Korrekturhistorie ist durch `corrects: step-005`, den Review-Pointer und den
  neuen Step-Plan nachvollziehbar. `task-state.md` bleibt ebenfalls unverändert.
- `Docs/configuration.md`, `README.md` und `rules.json` werden nicht berührt,
  solange die Implementierung ausschließlich Diagnosezählung, Required-vs-
  Duplicate-Semantik und Testabdeckung korrigiert und keine Konfigurationsfelder
  oder Regelverträge ändert.
- Bestehende Provider-, Snapshot-, Session- und MCP-Tests bleiben unverändert;
  der Korrekturtest endet an der Loader-/Validator-Grenze.

## Notes

- Der Coder/Kritiker muss die aktuelle semantische Lage nicht aus dem
  `step-result.md` übernehmen: `find_symbol`/`get_feature_context` bestätigten
  die drei real vorhandenen `Diagnostic`-Methoden, und `get_symbol_body`
  bestätigte die doppelte Diagnoseerzeugung sowie den irreführenden
  `RequiredFieldMissing`-Pfad.
- Die gemeinsame Fabrik darf als statische Factory am bestehenden
  `ExternalSourceConfigurationDiagnostic`-Vertrag oder als gleichwertiger
  interner Bestandteil derselben Mapping-Vertragsdatei umgesetzt werden; eine
  neue fachliche Diagnose-/Provider-/Sessionabstraktion ist nicht zulässig.
- Nur direkt betroffene DRY-/MagicValues-/DeadCode-Funde im Mapping-/
  Validierungscode dürfen opportunistisch mitbereinigt werden. Ein Fund in
  Assembly-, Session-, MCP- oder Provider-Code gehört nicht in diesen Step.
- Nach Abschluss schreibt der Coder das reguläre `step-result.md` und
  committet die Implementierung separat; dieser Planer startet keinen Coder und
  keinen Kritiker.
