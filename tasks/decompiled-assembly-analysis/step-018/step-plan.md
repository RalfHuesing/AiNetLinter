---
status: done (pending audit)
type: step-plan
task: decompiled-assembly-analysis
step: "018"
corrects: null
title: "Repository-spezifische Capability-Nichtverfügbarkeit zum Decompilation-Fallback"
epic: EPIC-04
estimated_risk: high
step_type: single
items:
  - "Acquirer-Failure-Vertrag für 1314 und erkannte Reparse-Checkouts"
  - "Failure-Projection zum bestehenden Provider-/Orchestrator-Fallback"
created_by: planer
created_by_model: "gpt-5 (Codex)"
created_by_model_knowledge_cutoff: "nicht angegeben"
created_at: "2026-08-29T05:39:59.6385637+02:00"
related_to:
  - "../step-015/step-plan.md"
  - "../step-017/step-result.md"
  - "../step-017/step-review.md"
  - "../follow-up-strategy.md"
  - "../Konzept.md"
  - "../roadmap.md"
---

# Step 018 — Repository-spezifische Capability-Nichtverfügbarkeit zum
# Decompilation-Fallback

## Bezug

Step 017 hat die Entscheidung über einen Host ohne Symlink-/Reparse-
Capability offengelassen. Die Nutzerentscheidung für Step 018 lautet:
Ein Fehler ERROR_PRIVILEGE_NOT_HELD (1314) oder ein tatsächlich erkannter
Reparse-Checkout macht nur die betroffene Source unavailable; normale
Repositories bleiben nutzbar, und die bestehende statische Decompilation
bleibt der Fallback. Es gibt keine globale Capability-Sperre, keinen
Attribut-Fake und keine Änderung von Systemprivilegien.

Die MCP-Untersuchung mit projectRoot
C:/Daten/Entwicklung/Ralf/AiNetLinter ergibt:

- ExternalSourceRepositoryAcquirer ist eine sichere Staging-/Clone-Fassade
  mit typed AcquisitionResult, aber ohne produktiven Aufrufer.
- ExternalSourceRepositoryFailurePolicy klassifiziert Transportfehler
  zentral; 1314 ist dort noch kein eigener erkannter Sonderfall.
- AssemblySourceSelectionOrchestrator ruft den bestehenden
  IExternalSourceProvider auf und leitet IsAvailable=false samt FailureKind
  und Diagnosen bereits an AssemblyAnalysisContextFactory weiter.
- Ein fehlendes Source-Selection-Ergebnis löst dort den vorhandenen
  statischen Decompilation-Pfad aus. Der relevante Produktionsscope hat
  aktuell keine Lint-Verstöße.

Das ist kein Review-Korrekturschnitt: Step 018 öffnet den nächsten wirksamen
Vertrags-/Adapter-Schnitt auf Grundlage der neuen Nutzerentscheidung.

## Split-Gate

- Gekoppelte Verträge: genau zwei.
  1. Acquirer/FailurePolicy liefert eine stabile, typed und sichere
     Capability-Nichtverfügbarkeit.
  2. Diese Failure-Information wird als bestehendes
     ExternalSourceProviderResult zum unveränderten
     AssemblySourceSelectionOrchestrator-Fallback projiziert.
- Schichten: genau drei.
  1. Acquirer und zentrale Failure-Klassifikation.
  2. Provider-facing Failure-Projection.
  3. netzwerkfreie Component-/Unit-Regression vom Provider über den
     Orchestrator bis zur statischen Decompilation.
- Akzeptanzkriterien: genau acht, siehe unten.
- read_first: genau zwölf Dateien, siehe context_budget.

## Intention

Der Step soll die kleinste belastbare Grenze schaffen, an der ein lokaler
Checkout-Capabilityfehler als repository-spezifisches unavailable sichtbar
bleibt und den vorhandenen Fallback erreicht. Der Acquirer darf dabei keine
anderen Fehler verschlucken und keinen Prozesszustand über Repositories
hinweg setzen.

### Entscheidung zum Acquirer-/Provider-Wiring

Ein Fehlerpfad-Wiring gehört zwingend in Step 018. Ohne die Projektion des
AcquisitionResult in den vorhandenen Provider-Result-Vertrag bliebe der
Acquirer trotz korrekter eigener Klassifikation ein nicht angeschlossener
Dead-End; der Orchestrator könnte den konkreten Fehler nicht sehen.

Das vollständige produktive Acquirer-zu-IExternalSourceProvider-Wiring gehört
nicht in diesen Step. Im aktuellen Code fehlt sowohl ein produktiver
Acquirer-Aufrufer als auch die Snapshot-/Workspace-Materialisierung, die ein
erfolgreiches AcquisitionResult in ein ExternalSourceSnapshot überführen
würde. Ein vollständiger Adapter würde deshalb zwangsläufig Refresh, Cache,
Source-of-Truth oder Materialisierung vorwegnehmen.

Step 018 verdrahtet daher nur die Failure-Projection: eine interne,
einheitliche Provider-facing Projektion für IsAvailable=false und ein
netzwerkfreier Test-Provider, der genau diesen Fehlerpfad durch den
bestehenden Orchestrator ausführt. Erfolgreiche Acquisitions werden nicht
als scheinbar source-backed Provider-Ergebnisse ausgegeben. Der spätere
source-backed Provider verwendet dieselbe Projektion als seinen
Fehlerzweig; seine Erfolgs-/Materialisierungsverdrahtung ist ein klar
abzugrenzendes Folgepaket.

## Failure-Semantik

Es wird kein neues ExternalSourceProviderFailureKind eingeführt.
ProviderUnavailable beschreibt bereits exakt die Routing-Semantik
„für diese Source steht kein nutzbarer Provider-Inhalt zur Verfügung“ und
führt im Orchestrator zu keiner Source Selection. Ein weiterer Enum-Wert
würde das Fallback nicht unterscheiden, aber alle Failure-Switches und
Verträge unnötig erweitern.

Stattdessen wird ein stabiler, geheimnisfreier Diagnostic-Code für
RepositoryCapabilityUnavailable ergänzt und zentral über die bestehende
FailurePolicy projiziert:

- Ein exakt erkannter ERROR_PRIVILEGE_NOT_HELD-Wert (1314) aus dem
  Clone-/Transportversuch wird als ProviderUnavailable mit diesem
  Diagnostic-Code zurückgegeben. Die Erkennung erfolgt nur in diesem
  Acquisitionpfad, nicht über eine globale Vorabprobe und nicht durch
  pauschales Umdeuten jedes UnauthorizedAccessException.
- Ein tatsächlich durch die bestehende Checkout-Prüfung erkannter
  Reparse-Point wird als dieselbe Capability-Nichtverfügbarkeit gemeldet,
  bevor ein Checkout freigegeben wird. Die bestehende besitzsichere
  Bereinigung bleibt verpflichtend; bei Bereinigungsfehlern bleibt die
  Primärursache erhalten und die bestehende Cleanup-Diagnose wird ergänzt.
- Andere Checkout-Invalidität bleibt bei ihrer bisherigen Semantik.
  NetworkUnavailable, Timeout, AuthenticationRequired, AccessDenied,
  InvalidResponse und Cancellation werden nicht in Capability-Unavailable
  umklassifiziert. OperationCanceledException wird weiterhin unverändert
  erneut ausgelöst.
- Die Provider-Projection erzeugt für den Fehlerfall ein Ergebnis ohne
  Snapshot, mit ProviderUnavailable und nur den bereits sicher
  normalisierten Diagnosen. Keine Exception-Nachricht, Credential,
  vollständige Repository-URL oder lokale Staging-Information darf in
  die Diagnose gelangen; als Ort bleibt die bestehende sichere
  Repository-Markierung erhalten.
- Der Orchestrator muss FailureKind und Diagnosen in seiner Scope-Antwort
  erhalten, aber keine Selection registrieren. Die ContextFactory nutzt
  daraufhin unverändert die statische Decompilation.

## Scope

### In scope

1. Die zentrale, magic-value-freie Erkennung des exakten Win32-Fehlers
   1314 im Acquirer-Transportpfad.
2. Die typed Abgrenzung zwischen erkanntem Reparse-Checkout und sonstiger
   Checkout-Invalidität einschließlich besitzsicherer Bereinigung.
3. Der eine stabile Diagnostic-Code und seine Safe-Projection in den
   vorhandenen ExternalSourceProviderResult-Vertrag.
4. Die minimale Failure-Projection zum bestehenden Provider-Port sowie
   der netzwerkfreie Test-Double für die Pfadprüfung.
5. Component-/Unit-Regressionen für Acquirer, Provider-Projection,
   Orchestrator und statischen Decompilation-Fallback.
6. Der Nachweis, dass ein Fehler nur die angefragte Mapping-Source
   unavailable macht und ein normal nutzbares Repository weiterhin
   source-backed verarbeitet werden kann.

### Out of scope

- Produktiver Gitea-/Git-/HTTP-Transport, Credentials, Netzwerkzugriff,
  Clone-/Fetch-/Default-Branch-Semantik und echte Remote-Ausführung.
- Refresh, Retry, persistenter Cache, Manifest-/Snapshot-Integrität,
  Source-of-Truth-Veröffentlichung sowie Snapshot-/Workspace-
  Materialisierung.
- Eine Änderung an AssemblyAnalysisHostComposition, die global einen
  Provider sperrt, oder eine globale Capability-Probe.
- Änderung, Umgehung oder künstliches Erzeugen des privilegierten
  Reparse-Tests aus Step 017; insbesondere kein Attribut-Fake,
  keine Junction-/Symlink-Erzeugung und keine Systemprivilegienänderung.
- Änderungen an task-state.md, codemap.md oder tech-debt.md.
  TD-001 bis TD-003 bleiben unverändert.
- Ein unabhängiger DRY-, MagicValues- oder DeadCode-Sweep. Solche
  Befunde werden nur behoben, wenn sie unmittelbar durch diesen
  Adapter-/Failure-Schnitt entstehen und die gemeinsame Policy dadurch
  tatsächlich zentralisiert wird.

## Konkrete Änderungen

### Schicht 1 — Acquirer und FailurePolicy

- Die 1314-Erkennung in ExternalSourceRepositoryFailurePolicy bündeln,
  mit einer benannten Konstante statt eines verteilten Literals, und nur
  vor der generischen Transportklassifikation anwenden.
- ExecuteTransportAsync so begrenzen, dass ausschließlich der exakte
  1314-HResult aus dem Cloneversuch als
  RepositoryCapabilityUnavailable/ProviderUnavailable projiziert wird.
  Alle anderen Ausnahmen bleiben bei der bestehenden Klassifikation.
- Die Reparse-Verzweigung von TryValidateCheckout typed als Capability-
  Nichtverfügbarkeit ausgeben. Nicht-Reparse-Fälle behalten ihren
  bisherigen RepositoryCheckoutInvalid-/InvalidResponse-Vertrag.
- Den neuen Diagnostic-Code durch die bestehende Safe-Diagnostic-
  Normalisierung und die AcquisitionResult-Konstruktoren führen.
  Cleanup darf weder die Primärursache ersetzen noch einen Checkout
  zurückgeben.

### Schicht 2 — Provider-Failure-Projection

- Eine einzige interne Failure-Projection an der Provider-Grenze
  vorsehen, vorzugsweise als benannten Mapper nahe dem bestehenden
  ExternalSourceProviderResult-Vertrag. Sie akzeptiert ausschließlich
  ein nicht verfügbares AcquisitionResult und erzeugt kein Snapshot.
- Erfolgreiche AcquisitionResults in dieser Projection ausdrücklich
  nicht als Provider-Erfolg behandeln. Die fehlende
  Snapshot-/Workspace-Materialisierung bleibt ein Folgepaket.
- Keine Änderung am Routing des AssemblySourceSelectionOrchestrator
  vornehmen, sofern der vorhandene IsAvailable=false-Pfad den neuen
  Diagnostic-Code bereits unverändert trägt. Nur falls die konkrete
  Projection es erfordert, die kleinstmögliche Vertragsanpassung
  ergänzen; keine zusätzliche FailureKind-Variante.

### Schicht 3 — Regressionen

- In ExternalSourceRepositoryAcquirerTests den 1314-Transportfall,
  Reparse-Ablehnung/Bereinigung und unveränderte Fremdfehler abdecken.
  Der vorhandene reale Reparse-Test darf nur seine erwartete typed
  Semantik aktualisieren; seine Umgebung und Schutzvorkehrungen bleiben
  unverändert.
- In ExternalSourceProviderContractTests die Failure-Projection auf
  ProviderUnavailable, fehlenden Snapshot, stabilen Diagnostic-Code
  und redigierte Diagnosen prüfen.
- In AssemblyAnalysisToolSupportTests oder einer eng begrenzten
  benachbarten Component-Testdatei den Weg Provider-Failure →
  Orchestrator-Scope ohne Selection → statische Decompilation prüfen.
  Einen zweiten, normal verfügbaren Provider-Fall als Gegenprobe
  ausführen, damit kein globaler Gate-Effekt unbemerkt bleibt.
- Keine Netzwerk-, Git- oder Gitea-Ausführung; alle Tests verwenden
  vorhandene deterministische Test-Doubles und TestTempDirectory.

## Akzeptanzkriterien

1. Ein Clone-/Transport-Double mit exakt ERROR_PRIVILEGE_NOT_HELD (1314)
   liefert für genau das angefragte Repository IsAvailable=false,
   FailureKind=ProviderUnavailable und den stabilen Capability-Diagnostic-
   Code; kein Credential- oder Exception-Text wird ausgegeben.
2. Ein tatsächlich erkannter Reparse-Checkout wird vor Freigabe abgelehnt,
   der eigene Checkout wird mit der bestehenden Ownership-Regel bereinigt,
   und das Ergebnis ist ebenfalls ProviderUnavailable mit derselben
   Capability-Ursache.
3. Ein anderer Transport-, Auth-, Netzwerk-, Timeout- oder Cancellation-
   Fall behält seine bisherige Failure-Semantik; kein Fehler wird still
   verschluckt oder pauschal als Capabilityfehler maskiert.
4. Die Failure-Projection überführt genau diese nicht verfügbare
   Acquisition in ein ExternalSourceProviderResult ohne Snapshot,
   einschließlich FailureKind und sicher normalisierter Diagnosen.
5. AssemblySourceSelectionOrchestrator gibt für diesen Provider-Failure
   keine Source Selection zurück und erhält FailureKind sowie Diagnosen
   in seinem Scope.
6. AssemblyAnalysisContextFactory/AssemblyAnalysisToolSupport führen
   nach diesem Scope-Failure nachweisbar den vorhandenen statischen
   Decompilation-Pfad aus.
7. Ein normal verfügbares Repository bleibt source-backed nutzbar, und
   der Capabilityfehler eines anderen Mappings setzt keinen globalen
   Provider- oder Host-Schalter.
8. Die fokussierten Tests, der Build sowie beide vollständigen
   Nicht-Stress-Testläufe sind grün; es werden keine Netzwerk-/Git-/
   Gitea-Aktionen und keine Systemprivilegien benötigt.

## Tests

Der Coder führt zuerst die fokussierten Regressionen aus:

    dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~ExternalSourceRepositoryAcquirerTests
    dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~ExternalSourceProviderContractTests
    dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~AssemblyAnalysisToolSupportTests

Danach folgen:

    dotnet build
    dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
    dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress

Zusätzlich sind die MCP-Checks auf dem absoluten projectRoot
C:/Daten/Entwicklung/Ralf/AiNetLinter nach dem Edit zu wiederholen:
get_violations im geänderten Produktionsscope und ein gezielter
find_duplicates-/Refactoring-Drift-Check nur für den neuen Adapterpfad.

## Definition of Done

- Die zwei Contracts sind im Code eindeutig benannt und auf die drei
  beschriebenen Schichten begrenzt.
- 1314 und erkannte Reparse-Checkouts sind repository-spezifisch
  unavailable, secret-free diagnostiziert und an den bestehenden
  Decompilation-Fallback angeschlossen.
- Fremdfehler, Cancellation, Ownership-Bereinigung und normale
  source-backed Repositories behalten ihre bisherigen Invarianten.
- Die acht Akzeptanzkriterien sind durch deterministische Tests und die
  angegebenen Build-/Test-Gates belegt.
- Keine Produktivverdrahtung von Refresh, Cache, Source-of-Truth,
  Materialisierung oder produktivem Gitea-Transport ist hinzugekommen.

## Invarianten

- Keine globale Capability-Probe und kein globaler Provider-/Host-Schalter.
- Nur die aktuelle Mapping-Source darf unavailable werden.
- ProviderUnavailable bedeutet immer: kein nutzbarer Source-Inhalt für
  diesen Provider-Aufruf; es ist kein Auth-, Netzwerk- oder
  InvalidResponse-Ersatz.
- Reparse-Checkouts werden nicht als verifizierter Checkout freigegeben;
  die Cleanup-Ownership bleibt erhalten.
- Keine Rohdaten aus URL, Credentials, Exception oder Stagingpfad
  gelangen in öffentliche Diagnosen.
- Cancellation bleibt Cancellation; Bereinigungsfehler ergänzen, aber
  überschreiben nicht die primäre Failure-Ursache.
- Statische Decompilation bleibt ohne Source Selection der vorhandene
  und unveränderte Fallback.

## context_budget

### read_first

1. tasks/decompiled-assembly-analysis/step-017/step-result.md
2. tasks/decompiled-assembly-analysis/step-017/step-review.md
3. tasks/decompiled-assembly-analysis/step-015/step-plan.md
4. tasks/decompiled-assembly-analysis/follow-up-strategy.md
5. tasks/decompiled-assembly-analysis/Konzept.md
6. src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs
7. src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryFailurePolicy.cs
8. src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquisitionModels.cs
9. src/AiNetLinter/Mcp/Assemblies/IExternalSourceProvider.cs
10. src/AiNetLinter/Mcp/Assemblies/AssemblySourceSelectionOrchestrator.cs
11. src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextFactory.cs
12. src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupportTests.cs

### read_on_demand

ExternalSourceRepositoryAcquirerTests.cs, ExternalSourceProviderContractTests.cs,
AssemblyAnalysisContextFactoryTests.cs, AssemblyAnalysisHostComposition.cs,
AssemblyAnalysisHostCompositionTests.cs, UnavailableExternalSourceProvider.cs,
SourceSnapshotModels.cs, SourceSnapshotRegistry.cs und die konkreten
Diagnostic-Code-Definitionen, falls die Projection dort verankert wird.
Nur die von den zwei Contracts direkt betroffenen Ausschnitte nachladen.

### out_of_scope

Alle Dateien zu produktivem Transport, Refresh, Cache, Source-of-Truth,
Workspace-/Snapshot-Materialisierung, Gitea-Credentials, Daemon-globaler
Capability und den unveränderten Task-/Debt-/Codemap-Dateien.

## Risiken und Gegenmaßnahmen

- Risiko: Jedes UnauthorizedAccessException wird fälschlich als 1314
  behandelt. Gegenmaßnahme: nur den exakt erkannten Win32-Wert aus dem
  Cloneversuch vor der bestehenden Klassifikation prüfen.
- Risiko: Jeder RepositoryCheckoutInvalid-Fall wird zum Capabilityfehler.
  Gegenmaßnahme: ausschließlich den nachgewiesenen Reparse-Zweig
  umklassifizieren; übrige Invalidität unverändert lassen.
- Risiko: Die Projection erfindet bei erfolgreicher Akquisition einen
  Snapshot. Gegenmaßnahme: Failure-only Mapper mit Guard und explizitem
  Materialisierungs-Folgeschnitt.
- Risiko: Ein Host-weites unavailable entsteht durch die Default-
  Composition. Gegenmaßnahme: AssemblyAnalysisHostComposition nicht
  global ändern; den Nachweis pro Mapping im Test führen.
- Risiko: Diagnosen leaken geheime oder lokale Daten. Gegenmaßnahme:
  bestehende Safe-Diagnostic-Policy und feste Repository-Markierung
  wiederverwenden; keine Roh-Exceptiontexte.
- Risiko: Ein Test simuliert unzulässig Reparse über Attribute. Gegenmaßnahme:
  den realen Step-017-Test unverändert lassen und neue Provider-Tests nur
  mit typed Acquisition-Ergebnissen/Transport-Doubles ausführen.
- Risiko: Der kleine Adapter erzeugt eine neue Duplikations- oder
  Magic-Value-Stelle. Gegenmaßnahme: Klassifikation und Projection je
  einmal zentralisieren; kein unabhängiger Debt-Sweep.

## Vorgesehene Dateien

### Produktionscode

- src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryFailurePolicy.cs
- src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs
- src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquisitionModels.cs
- src/AiNetLinter/Mcp/Assemblies/IExternalSourceProvider.cs oder eine
  unmittelbar benachbarte interne Failure-Adapter-Datei.

Der AssemblySourceSelectionOrchestrator und die ContextFactory sollen
unverändert bleiben, sofern die vorhandene false/null-Fallbackkette den
neuen Vertrag direkt trägt. AssemblyAnalysisHostComposition erhält keine
globale Capability-Logik.

### Tests

- src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryAcquirerTests.cs
- src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceProviderContractTests.cs
- src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupportTests.cs
- bei notwendiger enger Trennung eine neue, auf den Failure-Projection-
  Vertrag begrenzte Testdatei im selben FastTests-Scope.

## Rules-Refs

- .agents/rules/AiNetLinter.mdc
- .agents/rules/AiNetLinterRichtlinien.mdc
- .agents/rules/AiNetLinter-McpWorkflow.mdc
- .agents/Agent-Scaffolding/AGENTS.md
- .agents/Agent-Scaffolding/dev-loop/drift-loop/spec.md
- .agents/Agent-Scaffolding/dev-loop/drift-loop/skills/planer/SKILL.md
- .agents/Agent-Scaffolding/dev-loop/drift-loop/templates/step-plan.md

## Bekannte Ausnahmen

Der privilegierte echte Reparse-Nachweis aus Step 017 bleibt eine
Umgebungs-/Host-Ausnahme und wird in Step 018 weder erzwungen noch durch
einen Fake ersetzt. Step 018 beweist die typed Weiterleitung und den
Fallback mit deterministischen Doubles; die reale Erkennung bleibt im
bestehenden Acquirer-Testpfad. Das ist kein Grund für eine globale Sperre
normaler Repositories.

## Coder-Handoff

Arbeite vom aktuellen sauberen Arbeitsbaum aus und lies zuerst die zwölf
Dateien aus context_budget/read_first. Implementiere genau die zwei
Contracts und drei Schichten dieses Plans. Beginne bei FailurePolicy und
Acquirer, verankere danach die eine Failure-Projection am bestehenden
Provider-Result und schließe den Weg mit den vorhandenen Orchestrator-/
Decompilation-Tests.

Verändere die Host-Default-Composition nicht, führe keine globale
Capability-Probe ein und erweitere den Step nicht um erfolgreiche
Snapshot-Materialisierung. Wenn der aktuelle Code für die Projection
zusätzliche Erfolgspfad-Verträge verlangt, halte dort an und liefere diesen
Befund als Folgepaket; erfinde keinen Snapshot- oder Cache-Vertrag.

Nutze für alle semantischen Nachprüfungen
projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter. Führe die fokussierten
Tests, den Build und die beiden vollständigen Nicht-Stress-Gates aus.
Prüfe vor Abschluss, dass die tatsächliche Reparse-Testumgebung nicht
verändert, keine Systemprivilegien angefordert und kein Netzwerkzugriff
ausgeführt wurde.
