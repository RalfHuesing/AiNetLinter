---
status: done (pending audit)
type: step-plan
task: decompiled-assembly-analysis
step: 019
corrects: null
title: "Produktiven Git-over-HTTP-Transport mit injizierbarer Authentifizierung für den Default-Branch-Clone bauen"
epic: EPIC-04
estimated_risk: high
step_type: single
items: []
created_by: planer
created_by_model: gpt-5 (Codex)
created_by_model_knowledge_cutoff: nicht angegeben
created_at: 2026-08-29T06:50:19+02:00
related_to:
  - ../step-018/step-result.md
  - ../step-018/step-review.md
  - ../step-018/step-plan.md
  - ../follow-up-strategy.md
  - ../Konzept.md
  - ../roadmap.md
---

# Step 019: Produktiven Git-over-HTTP-Transport mit injizierbarer Authentifizierung für den Default-Branch-Clone bauen

## Bezug

- **Task:** `decompiled-assembly-analysis`
- **Epic:** `EPIC-04` — Gitea-Source-of-Truth, Refresh und Fehlersemantik.
- **Vorgänger:** Step 018 ist genehmigt. Die repository-spezifische
  Capability-Nichtverfügbarkeit für `ERROR_PRIVILEGE_NOT_HELD` (1314) und
  erkannte Reparse-Checkouts bleibt `ProviderUnavailable` mit statischem
  Decompilation-Fallback; normale Repositories bleiben nutzbar.
- **Offener Anschluss:** Der produktive Acquirer→Snapshot-Anschluss ist noch
  nicht implementiert und wird durch diesen Transport-Schnitt nicht vorweggenommen.

## Aktueller Projektzustand (JIT-Kontext)

Die AiNetLinter-MCP-Abfragen mit absolutem `projectRoot`
`C:/Daten/Entwicklung/Ralf/AiNetLinter` bestätigen folgende Grenze:

- `IGiteaRepositoryTransport.CloneDefaultBranchAsync` ist ein schmaler,
  injizierbarer Port. In produktivem Code gibt es derzeit nur den Aufruf aus
  `ExternalSourceRepositoryAcquirer`; alle übrigen Implementierungen sind
  deterministische Test-Doubles.
- `ExternalSourceRepositoryAcquirer` besitzt kontrolliertes Staging,
  Checkout-Ownership, Solution-Prüfung, Reparse-Schutz und Cleanup. Sein
  Transportergebnis kann bereits Revision, Failure-Kind und Diagnosen tragen.
- `IExternalSourceProvider`, `AssemblySourceSelectionOrchestrator` und
  `AssemblyAnalysisHostComposition` sind nicht mit einem erfolgreichen
  Acquirer-Ergebnis verbunden. Der Default bleibt der unavailable Provider.
- `ExternalSourceMapping` und der strikte Loader erlauben weiterhin nur
  Repository-URL, Solution-Pfad und Assembly-Aliase. Credentials werden nicht
  in Mapping-JSON, URLs oder Ergebnissen gespeichert.
- Gezieltes `rg` fand keinen produktiven `HttpClient`-, LibGit-, Git-Clone-,
  Credential- oder Prozess-Transport im Assembly-Source-Pfad. Eine bestehende
  synchrone Git-Diff-Prozessnutzung ist kein wiederverwendbarer Clone-Vertrag.
- Die vorhandenen Failure-Klassen aus Step 014 und die Acquirer-Verträge aus
  Step 015/018 sind wiederzuverwenden; kein paralleles Failure- oder
  Provider-Modell ist erforderlich.

## Intention und Scope

Der Step bringt den ersten produktiven Adapter hinter den vorhandenen
Acquisition-Port:

1. Eine neue `GiteaGitRepositoryTransport`-Implementierung führt einen
   initialen Clone über Git-HTTP(S) aus. Sie verwendet die vom Acquirer
   kontrollierte Zielwurzel, klont nur den Default-Branch-Zustand und ermittelt
   danach die tatsächlich geladene `HEAD`-Revision.
2. Ein eng gekoppelter
   `IExternalSourceCredentialResolver`-Vertrag erlaubt dem späteren Host,
   Credentials zur Laufzeit sicher einzuspritzen. Ein fehlendes Credential
   kann einen öffentlichen Clone zulassen oder zu einer typisierten
   `AuthenticationRequired`-Antwort führen; interaktive Prompts werden
   unterbunden.
3. Der Prozessstart bleibt testbar über eine technische, interne
   Executor-Injektion. Diese ist kein dritter fachlicher Port: Der fachliche
   Vertrag bleibt `IGiteaRepositoryTransport` plus der Auth-Auflösungsvertrag.
4. Fehler, Cancellation und Secret-Schutz werden gegen den bestehenden
   Acquirer und seine Cleanup-/Failure-Semantik deterministisch geprüft.

Nicht Bestandteil ist die Verwendung eines erfolgreichen Checkouts als
`ExternalSourceSnapshot`. Das bleibt der nächste eigene Wiring-Schnitt.

## Entscheidung zum Split-Gate

Der nächste Schnitt ist der produktive Transport-/Auth-Adapter und nicht
Refresh/Cache/atomare Veröffentlichung. Der Transport ist die kleinste
sinnvolle vertikale Grenze, weil der vorhandene Akquisitionsvertrag sonst
weiterhin ausschließlich ein Test-Doppel besitzt. Refresh, persistenter Cache,
Manifest-/Integritätsprüfung und atomare Source-of-Truth-Veröffentlichung
würden zusätzlich Snapshot-/Generation-/Lifetime-Verträge öffnen und das
Kontextbudget überschreiten.

Das Gate bleibt eingehalten:

- **Primäre Fachverträge:** höchstens zwei eng gekoppelte Verträge — der
  bestehende `IGiteaRepositoryTransport` samt Ergebnis und der neue
  `IExternalSourceCredentialResolver` samt geheimnishaltigem
  In-Memory-Wert. Die interne Prozess-Executor-Injektion ist lediglich ein
  Test-/Implementierungsseam.
- **Schichten:** (1) Credential-/Transportadapter, (2) Git-Prozess- und
  Fehlerübersetzung hinter dem Acquirer-Port, (3) deterministische
  FastTests und Regressionen.
- **Akzeptanzkriterien:** genau acht, siehe unten.
- **Kontextbudget:** höchstens zwölf `read_first`-Dateien, siehe unten.

## Akzeptanzkriterien

1. `GiteaGitRepositoryTransport` implementiert den bestehenden
   `IGiteaRepositoryTransport`-Port und ist über Konstruktor-/Seam-Injektion
   deterministisch testbar; `IExternalSourceProvider`, Orchestrator und Host-
   Komposition bleiben unverändert.
2. Der Erfolgspfad führt genau einen initialen Git-Clone über die gemappte
   HTTP(S)-Repository-URL mit Single-Branch-/No-Tag-Semantik aus, lässt Git den
   Default-Branch bestimmen und liefert anschließend eine nichtleere
   `HEAD`-Revision; Branchwechsel, Fetch und Refresh sind nicht enthalten.
3. Ein injizierter Credential-Resolver kann Credentials nur im Speicher
   liefern. Secret-Material gelangt ausschließlich über den geschützten
   Child-Process-Umgebungs-/Credential-Kanal, niemals in URL, Argumentliste,
   Mapping, Result, Diagnose oder Log; interaktive Credential-Prompts sind
   deaktiviert.
4. Fehlende Credentials, Auth-/Zugriffsfehler, nicht gefundene Repositories,
   Netzwerk-/Timeout-/ungültige Git-Antworten werden in die bestehenden
   `ExternalSourceProviderFailureKind`-Werte und zentralen Diagnosen übersetzt;
   rohe Git-Ausgaben werden nicht als Benutzerdiagnose durchgereicht.
5. Cancellation wird an Resolver und Prozessausführung weitergereicht,
   beendet den gestarteten Child-Prozess kontrolliert und bleibt echte
   `OperationCanceledException`; der Acquirer behält alleinige Verantwortung
   für Cleanup des eigenen Staging-Besitzes.
6. Erfolgreiche Transportergebnisse sind revisionsbelegt und
   `FailureKind.None`; ungültige oder unvollständige Git-Ergebnisse bleiben
   snapshot-freie Fehler und können keinen halbfertigen Checkout als Erfolg
   veröffentlichen.
7. Neue FastTests verwenden nur `TestTempDirectory`/bestehende TestKit-
   Infrastruktur sowie Executor- und Credential-Doubles und decken Erfolg,
   Default-Branch-Argumente, Auth, Secret-Nichtleck, typisierte Fehler,
   Cancellation und Acquirer-Cleanup ab; kein Test startet Git, Netzwerk,
   Gitea, externe Restore-Quellen oder einen MCP-Server.
8. `dotnet build` sowie beide vollständigen Nicht-Stress-Testläufe bleiben
   grün; Step 018s repository-spezifischer 1314-/Reparse-Fallback bleibt
   unverändert, und es entstehen weder Snapshot-/Workspace-Wiring noch
   Refresh-, Cache- oder atomare Veröffentlichungslogik.

## Konkrete Änderungen

### Schicht 1: Auth- und Transportadapter

#### `src/AiNetLinter/Mcp/Assemblies/IExternalSourceCredentialResolver.cs` (neu)

- Einen internen Resolver-Vertrag mit Cancellation definieren, der für ein
  bestehendes `ExternalSourceMapping` optional einen flüchtigen
  `ExternalSourceCredential`-Wert liefert.
- Username/Secret als kurzlebigen In-Memory-Wert modellieren; der Vertrag
  schreibt weder Ablageort noch Persistenz, Profil-JSON oder Secret-Logging
  vor. Ein Resolver darf auf eine vom Host kontrollierte sichere Quelle
  zugreifen; diese Quelle wird nicht in diesem Step implementiert.
- Keine Credential-Felder im Mapping-JSON und keine Secret-Werte in Result,
  Scope, Snapshot oder Diagnose einführen.

#### `src/AiNetLinter/Mcp/Assemblies/GiteaGitRepositoryTransport.cs` (neu)

- Den bestehenden Transport-Port produktiv implementieren. Der Adapter
  akzeptiert nur die vom Acquirer vorgegebene Zielwurzel und reicht die
  vorhandene Mapping-URL an Git weiter; er erzeugt keinen zweiten
  Repository-/Provider-Port.
- Git mit sicherer Argumenttrennung und deaktiviertem interaktivem Prompt
  starten. Der initiale Clone verwendet die Single-Branch-/No-Tag-Semantik
  ohne vom Mapping erfundene Branchauswahl. Danach `HEAD` aus dem Checkout
  ermitteln und als geladene Revision zurückgeben.
- Credentials über den Resolver beziehen und ausschließlich im
  Child-Process-Umgebungs-/Credential-Kanal verwenden. URL-Userinfo,
  Commandline-Secrets, globale Prozessumgebungsänderungen und Secret-Inhalte
  in Logs sind ausgeschlossen.
- Prozessstart, Exitcode und sichere Fehlerprojektion über einen internen
  Executor-Seam injizieren, damit FastTests keine echte Git-/Netzwerkumgebung
  benötigen. Die produktive Standardausführung nutzt asynchrone
  Prozessbeobachtung und kontrolliertes Abbrechen bei Cancellation.
- Git-/HTTP-Ergebnisse auf die bereits vorhandenen Failure-Klassen und
  zentralen Diagnosecodes abbilden. Keine sprach- oder hostabhängigen rohen
  Fehlermeldungen als stabile Fachsemantik verwenden.

### Schicht 2: Acquirer-Anschluss ohne Source-of-Truth-Wiring

- `ExternalSourceRepositoryAcquirer` nur soweit prüfen oder minimal anpassen,
  wie der produktive Transport das bestehende Result-/Cancellation-Protokoll
  benötigt. Die bestehende Ownership-, Pfad-, Reparse-, Solution- und Cleanup-
  Logik bleibt die einzige Staging-Verantwortung.
- `IExternalSourceProvider`,
  `AssemblySourceSelectionOrchestrator`,
  `AssemblyAnalysisHostComposition`, `SourceSnapshotRegistry` und
  `ExternalSourceSnapshot` nicht an den neuen Adapter verdrahten.
- Kein erfolgreicher Checkout wird als lokale parallele Source-of-Truth
  veröffentlicht; das Acquirer-Ergebnis bleibt ein interner, besitzender
  Checkout bis zum späteren Snapshot-Wiring.

### Schicht 3: Deterministische Verifikation

- Neue fokussierte FastTests für `GiteaGitRepositoryTransport` mit einem
  aufgezeichneten Executor und einem Credential-Double anlegen. Die Tests
  prüfen Befehlsfolge, Zielpfad, Default-Branch-Semantik, Revision,
  Cancellation, Fehlerprojektion und dass Secrets in keinem sichtbaren
  Ausgabeweg vorkommen.
- Bestehende `ExternalSourceRepositoryAcquirerTests` und
  `ExternalSourceRepositoryCancellationTests` nur bei einem konkreten
  Vertragsanschluss erweitern; die bereits genehmigte 1314-/Reparse-Regel
  darf nicht auf einen globalen Preflight geändert werden.
- Keine Integrationstest-Server, echte Git-Repositories, Netzwerkzugriffe,
  Systemprivilegienwechsel oder externe Restore-Quellen einführen.

## Invarianten

- `IGiteaRepositoryTransport` bleibt die einzige fachliche
  Repository-Akquisitionsgrenze; `IExternalSourceProvider` bleibt die einzige
  Provider-Injektionsgrenze.
- Ein erfolgreicher Transport liefert eine nichtleere geladene Revision und
  keine Failure-Klasse. Ein Fehler liefert keinen verwendbaren Erfolgspfad;
  Snapshot-, Registry- und Lease-Ownership bleiben unberührt.
- Der Transport schreibt nur in die vom Acquirer reservierte Zielwurzel. Er
  übernimmt weder Staging-Cleanup fremder Pfade noch die Lebensdauer eines
  späteren Snapshots.
- Keine Credential-Daten werden in URL, Mapping, `ArgumentList`, Result,
  Diagnose, Logging oder Testausgabe materialisiert. Prozessumgebung und
  kurzlebige Credential-Hilfsdaten werden nach dem Lauf verworfen.
- Cancellation bleibt Cancellation und wird nicht als Timeout,
  `ProviderUnavailable` oder Netzwerkfehler maskiert.
- Kein globaler 1314-/Reparse-Preflight: Die bestehende
  repository-spezifische `ProviderUnavailable`-Projektion und der statische
  Decompilation-Fallback bleiben unverändert.
- Kein `Assembly.Load`, `AssemblyLoadContext`, Reflection-Ausführung oder
  sonstiges Runtime-Laden externer Repository-Inhalte.

## Kontextbudget

### `read_first` (maximal 12 Dateien)

1. `tasks/decompiled-assembly-analysis/step-018/step-result.md` — genehmigte
   Failure-Projection und offene Acquirer→Snapshot-Folgegrenze.
2. `tasks/decompiled-assembly-analysis/step-018/step-review.md` — Review-
   Grenzen für 1314/Reparse und bestehende Regressionen.
3. `tasks/decompiled-assembly-analysis/step-018/step-plan.md` — bisheriger
   Split und explizit ausgelassene erfolgreiche Wiring-Semantik.
4. `tasks/decompiled-assembly-analysis/follow-up-strategy.md` — verbindliche
   Paketgröße, Kontext- und Handoff-Gates.
5. `tasks/decompiled-assembly-analysis/Konzept.md` — Phase 4, Auth-,
   Source-of-Truth- und Testleitplanken.
6. `src/AiNetLinter/Mcp/Assemblies/IGiteaRepositoryTransport.cs` — bestehender
   Transport-Port und Ergebnisinvarianten.
7. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs` —
   Ziel-, Ownership-, Revision- und Cleanup-Vertrag.
8. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquisitionModels.cs`
   — Checkout-Handle und Acquirer-Ergebnis.
9. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryFailurePolicy.cs`
   — zentrale Failure-/Secret-/1314-Projektion.
10. `src/AiNetLinter/Configuration/ExternalSourceConfiguration.cs` — Mapping-
    und Diagnosegrenzen.
11. `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryAcquirerTests.cs`
    — bestehender deterministischer Transport- und Cleanup-Testvertrag.
12. `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryAcquirerTestTransport.cs`
    — vorhandener Double und Aufzeichnungs-/Cancellation-Muster.

### `read_on_demand`

- `ExternalSourceConfigurationLoader.cs` und
  `ExternalSourceMappingValidator.cs` nur zur Bestätigung, dass das
  öffentliche Mapping-JSON unverändert bleibt.
- `ExternalSourceRepositoryCancellationTests.cs` für konkrete Prozess-/Token-
  Anschlüsse; `TestTempDirectory` und zugehörige TestKit-Helper nur bei
  zusätzlichem Fixture-Bedarf.
- `IExternalSourceProvider.cs`, `UnavailableExternalSourceProvider.cs`,
  `AssemblySourceSelectionOrchestrator.cs` und
  `AssemblyAnalysisHostComposition.cs` ausschließlich für den Nachweis,
  dass kein Host-/Snapshot-Wiring geöffnet wird.
- `Directory.Packages.props`, `src/AiNetLinter/AiNetLinter.csproj` und
  vorhandene Prozessnutzung nur zur Abhängigkeitsprüfung. Kein neues Git-/HTTP-
  Paket ohne gesonderte Architekturentscheidung.
- `Docs/configuration.md` nur falls der konkrete Adapter entgegen diesem Plan
  doch ein nutzerseitiges Mapping-/Credential-Feld benötigt; ein solches Feld
  ist in Step 019 nicht vorgesehen.

### `out_of_scope`

- `IExternalSourceProvider`-Erfolgspfad, Acquirer→Snapshot-Wiring,
  `MSBuildWorkspace`, Projekt-/Assembly-Matching, Registry, Lease-Lifetime,
  Host-Komposition und MCP-Tool-Integration;
- Refresh, Fetch in bestehende Checkouts, persistenter Cache, Cache-Key,
  Manifest-/Integritätsprüfung, Korruptionsheilung, Generation/Pointer und
  atomare Source-of-Truth-Veröffentlichung;
- lokale Arbeitskopien als Source-of-Truth, dirty/unbuilt-Regeln,
  transparente Health-/Capability-Matrix über die bestehende Step-018-Regel
  hinaus;
- neues öffentliches Mapping-JSON, Credential-Speicherung, Secret-Profile,
  Systemprivilegienänderungen und globale 1314-/Reparse-Preflights;
- reale Netzwerk-/Gitea-/Git-Testläufe, externe Restore-Quellen,
  MCP-Server- oder Stress-Tests;
- `Assembly.Load`, Reflection oder Ausführung/Inspektion fremder Assemblies;
- Änderungen an `task-state.md`, `codemap.md` oder `tech-debt.md`. `TD-001` bis
  `TD-003` werden nicht als unabhängiger Sweep angefasst; nur ein unmittelbar
  neu entstehender DRY-, MagicValues- oder DeadCode-Befund im Adapter darf
  innerhalb dieses Pakets architektonisch behoben werden.

## Risiken und Gegenmaßnahmen

- **Credential-Leak über Git-Aufruf:** Secrets nie in URL oder Argumentliste
  aufnehmen; nur Child-Process-Umgebung bzw. etablierter Credential-Kanal,
  mit deaktiviertem Prompt, bereinigter Lebensdauer und Tests auf sichtbare
  Nichtleckage.
- **Git-Prozess hängt oder überlebt Cancellation:** asynchronen Executor mit
  Cancellation, Prozessbaum-Abbruch und kontrollierter Rückgabe verwenden;
  keine synchronen `WaitForExit`-Aufrufe, Sleeps oder unbounded Retries.
- **Fehlerklassifikation wird sprachabhängig:** stabile Exit-/Transport-
  merkmale und zentralisierte Projektion verwenden; rohe stderr-Texte nur
  intern für sichere Klassifikationshilfe, niemals als Vertragsdiagnose.
- **Default-Branch wird unbemerkt zur Refresh-Logik:** ausschließlich neuer
  initialer Clone und anschließendes `HEAD`; keine Fetch-, Branchwechsel- oder
  Cache-Wiederverwendung modellieren.
- **Acquirer-/Snapshot-Lifetime vermischt sich:** der Adapter liefert nur das
  bestehende Transportergebnis. Snapshot-Erzeugung und Source-of-Truth bleiben
  in einem eigenen Folge-Step.
- **Testdouble driftet vom Produktionsprozess:** Executor-Double zeichnet
  strukturierte Befehle und sichere Umgebungsmetadaten auf; Tests prüfen
  Zielpfad, Optionen, Tokenweitergabe ohne Secret-Ausgabe und Cancellation.
- **Technische Überdehnung durch Credential-Speicher:** Resolver definiert
  nur die injizierbare Laufzeitgrenze. Konkrete Secret-Speicherung,
  Profilverwaltung oder benutzerseitige Konfiguration bleibt separat.

## Tests und Verifikation

Der Plan führt keine Tests aus. Der Coder soll nach der Implementierung
mindestens folgende Gates ausführen:

```powershell
dotnet test src/AiNetLinter.FastTests --filter Category=Unit
dotnet test src/AiNetLinter.FastTests --filter Category=Component
dotnet build
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
```

Die Transporttests müssen vollständig ohne echte Netzwerk-, Git-, Gitea- oder
Restore-Aktivität laufen. Stress-Tests werden nicht ausgeführt. Build und
beide vollständigen Nicht-Stress-Läufe sind Teil des Coder-Handoffs, nicht der
Planer-Ausführung.

## Definition of Done

- Der produktive, injizierbare Git-over-HTTP(S)-Adapter implementiert den
  bestehenden Default-Branch-Clone-Port und liefert die geladene Revision.
- Authentifizierung ist über einen flüchtigen Resolver gebunden; Secrets
  erscheinen nicht in Mapping, URL, Argumenten, Ergebnissen, Diagnosen, Logs
  oder Tests.
- Fehler-, Timeout-, Auth-, Zugriffs-, Repository-, Cancellation- und
  Cleanup-Semantik ist typisiert, zentral und deterministisch getestet.
- Acquirer, 1314-/Reparse-Fallback, Provider, Snapshot, Host und Source-of-
  Truth bleiben innerhalb ihrer bestehenden Grenze.
- Keine neue Git-/HTTP-Abhängigkeit ohne begründete Architekturentscheidung,
  kein Runtime-Assembly-Laden und kein echter Netzwerk-Test.
- `dotnet build` sowie beide vorgeschriebenen Nicht-Stress-Testläufe sind
  grün; der Coder dokumentiert die tatsächlich geänderten Dateien und jeden
  begründeten Planabweichungspunkt im Result.
- `task-state.md`, `codemap.md` und `tech-debt.md` bleiben unverändert; ihre
  Pflege erfolgt außerhalb dieses Planer-Artefakts.

## Geplante Dateien für den Coder

### Produktionscode

- `src/AiNetLinter/Mcp/Assemblies/IExternalSourceCredentialResolver.cs` (neu)
- `src/AiNetLinter/Mcp/Assemblies/GiteaGitRepositoryTransport.cs` (neu)
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs` nur
  bei einem nachgewiesenen Result-/Cancellation-Anschluss
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryFailurePolicy.cs`
  nur bei zentral notwendiger Git-Fehlerprojektion

### Tests

- `src/AiNetLinter.FastTests/Mcp/Assemblies/GiteaGitRepositoryTransportTests.cs`
  (neu)
- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryAcquirerTests.cs`
  und/oder `ExternalSourceRepositoryCancellationTests.cs` nur bei direktem
  Vertragsbezug

Die Liste ist ein begrenzter Änderungsrahmen, keine Erlaubnis für Host-,
Snapshot-, Cache- oder Mapping-Schema-Ausweitung.

## Coder-Handoff

### Sicherer Einstieg

1. Die zwölf `read_first`-Dateien prüfen und mit AiNetLinter-MCP erneut die
   Symbole/Referenzen für `IGiteaRepositoryTransport`, Acquirer, Failure-Policy
   und die vorhandenen Test-Doubles bestätigen; `rg` nur für Text-/Projekt-
   und Abhängigkeitsfragen verwenden.
2. Zuerst `IExternalSourceCredentialResolver` und den flüchtigen
   Credential-Wert definieren. Die Entscheidung explizit festhalten, wie ein
   fehlender Resolver zwischen öffentlichem Clone, Git-Credential-Helper und
   `AuthenticationRequired` unterschieden wird, ohne Prompt oder Secret-Log.
3. Danach `GiteaGitRepositoryTransport` mit internem, asynchronem Executor-
   Seam implementieren: Argumentliste, Child-Umgebung, Single-Branch-/No-Tag-
   Clone, `HEAD`, Cancellation und Failure-Projektion.
4. Fokussierte Transporttests mit Executor-/Credential-Doubles schreiben und
   erst danach vorhandene Acquirer-Cleanup-/Cancellation-Regressionen
   aktualisieren. Host-/Provider-/Snapshot-Dateien nicht öffnen, außer für
   einen reinen unveränderten Regressionstest.

### Übergabeinvarianten

- Kein Credential, Token oder Passwort wird persistent, in Mapping-JSON,
  URL, Argumenten, Result, Diagnose, Log oder Testoutput abgelegt.
- Der Transport verwendet ausschließlich die vom Acquirer übergebene
  Zielwurzel und delegiert Cleanup nicht an einen fremden Besitzer.
- Ein erfolgreiches Ergebnis trägt eine nichtleere Revision; ein fehlerhaftes
  Ergebnis ist nicht als Snapshot-/Source-of-Truth-Kandidat verwendbar.
- Cancellation bleibt echte Cancellation. 1314/Reparse bleibt ausschließlich
  repository-spezifische Nichtverfügbarkeit mit Decompilation-Fallback.
- Keine Assembly-Lade-/Reflection-Operation und keine globale
  Systemprivilegien- oder Preflight-Änderung.

### Erwarteter Result-/Review-Inhalt

Der Coder dokumentiert geänderte Code-/Testdateien, die konkrete
Credential-Kanal-Entscheidung, Git-Befehls-/Default-Branch-Semantik,
Fehlerprojektion, Secret-Nichtleck-Nachweis, Cancellation-/Cleanup-Nachweis,
alle ausgeführten Gates sowie offene Folgegrenzen für Acquirer→Snapshot,
Refresh/Cache und atomare Source-of-Truth-Veröffentlichung.

## Rules-Refs

- `.agents/rules/AiNetLinter-McpWorkflow.mdc` — C#-Symbole, Referenzen und
  Impact zuerst über AiNetLinter-MCP mit absolutem `projectRoot`; `rg` für
  Text-/Nicht-C#-Suchen.
- `.agents/rules/AiNetLinterRichtlinien.mdc` — einfache statische Architektur,
  injizierbare Verträge, Result-/Diagnosemodell, sichere Prozesse,
  Cancellation, keine fremden Source-of-Truths.
- `.agents/rules/AiNetLinter.mdc` — Nullable-/Warning-Grenze, kurze Methoden,
  keine Reflection/Assembly-Ladung, deterministische xUnit3-Tests.
- `.agents/Agent-Scaffolding/AGENTS.md` — Dokument-/Commitkonventionen und
  Planungsartefakt-Regeln.
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/spec.md` — JIT-Kontext,
  Split-Gate, drei Schichten, Handoff und Zustandsabgrenzung.
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/orchestrator.md` —
  sequenzieller Planer-/Coder-/Review-Ablauf und Commitgrenze.
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/skills/planer/SKILL.md` —
  genau ein nächster Step mit begrenztem Kontext.

## Bekannte Ausnahmen

- Diese Planer-Ausführung ändert absichtlich weder `task-state.md`,
  `codemap.md` noch `tech-debt.md`, obwohl sie im allgemeinen Dev-Loop später
  durch die zuständigen Rollen gepflegt werden.
- Es wird kein Mapping-JSON-Credential-Feld geplant. Falls die konkrete
  Host-Integration später eine nutzerseitige Credential-Referenz benötigt,
  ist dafür ein eigener kontextbegrenzter Konfigurations-/Dokumentationsschnitt
  erforderlich.
- `TD-001` bis `TD-003` bleiben offen. DRY-, MagicValues- und DeadCode-Arbeit
  ist nur zulässig, wenn sie im tatsächlich neuen Transportpfad unmittelbar
  entsteht und ohne Scope-Erweiterung architektonisch behoben werden kann.

## Notes

Step 019 liefert einen produktiven, aber noch nicht in den Provider-/Host-
Lebenszyklus eingehängten Akquisitionsadapter. Der sichere Folgeweg ist:

1. Step 019: Git-over-HTTP(S), Default-Branch-Clone, Runtime-Credential-
   Resolver und typisierte Transportfehler.
2. eigener Step: erfolgreiches Acquirer→Snapshot-/Workspace-Wiring mit
   Source-of-Truth- und Lease-Entscheidung.
3. eigener Step: Refresh, persistenter Cache, Integrität und atomare
   Veröffentlichung.

Diese Reihenfolge bewahrt den genehmigten Step-018-Vertrag und hält externe
Repository-Inhalte aus einer konkurrierenden lokalen Source-of-Truth heraus.
