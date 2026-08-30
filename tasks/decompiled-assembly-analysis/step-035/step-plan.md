---
status: open
type: step-plan
task: decompiled-assembly-analysis
step: 035
corrects: step-034
title: "ConfigurationFailure unabhängig von Diagnosen terminal bis zum Assembly-Tool propagieren"
epic: EPIC-04
estimated_risk: high
step_type: single
items: []
created_by: planer
created_by_model: gpt-5 (Codex)
created_by_model_knowledge_cutoff: nicht angegeben
created_at: 2026-08-30T02:47:07+02:00
related_to:
  - ../step-034/step-plan.md
  - ../step-034/step-result.md
  - ../step-034/step-review.md
---

# Step 035: ConfigurationFailure unabhängig von Diagnosen terminal bis zum Assembly-Tool propagieren

## Bezug und Bündelungsentscheidung

- **Task:** `decompiled-assembly-analysis`
- **Epic:** `EPIC-04`; Gitea bleibt Source of Truth, der lokale Cache bleibt
  eine validierte und besitzgeschützte Zwischenstufe.
- **Korrekturziel:** Step-034, Review `ff5fb2e5`.
- **MAJOR:** `ExternalSourceConfigurationLoadResult.Failure([])` ist zulässig,
  wird aber in `AssemblySourceSelectionScope.Status` wegen der leeren
  Diagnoseliste als `NoMatch` klassifiziert. Dadurch kann der erfolgreiche
  statische Decompilation-Fallback einen expliziten Config-Failure kaschieren.
- **MINOR, direkt gekoppelt:** Die URI-/UNC-Matrix trennt die Authority- und
  unvollständigen UNC-Zweige noch nicht exakt genug; die Failure-Regressionen
  prüfen `IsError=false` nur indirekt über `NotEqual(true, ...)`.

Die drei Befunde bilden ein vertikales Korrekturpaket an derselben
Ownership-/Security-/Options-/Tool-Vertragsgrenze: Der Loaderzustand wird als
expliziter Statusmarker bis zum Tool geführt, die bestehende
`Recoverable`-Policy bleibt dabei `IsError=false`, und die lokalen Tests
beweisen sowohl die adversariale Pfadklassifikation als auch die
End-to-End-Entscheidung. Ein einzelner Assertion-Fix oder ein Audit-only-Step
würde die Terminalitätslücke nicht schließen.

Im Fix-Modus wird `roadmap.md` nicht geändert. Die Step-034-Resultat- und
Review-Evidenz bleibt historische Evidenz; außer den direkt betroffenen
aktuellen Resultat-Assertions erfolgt keine Neubewertung.

## Split-Gate und Kontextbudget

Dieser Step hat genau einen Primärvertrag und drei unmittelbar gekoppelte
Schichten:

1. ein unveränderlicher `ConfigurationFailure`-Marker an der Selection-Scope,
   unabhängig von der Anzahl der Loader-Diagnosen;
2. die vorhandene terminale Tool-Grenze vor `CreateContextAsync` mit der
   dokumentierten `Recoverable`-/`IsError=false`-Semantik;
3. die lokale URI-/UNC-Matrix sowie direkte und echte Loader-zu-Tool-
   Regressionen einschließlich der positiven Fallbacks.

`max_initial_files: 12`

Der Coder liest vor dem ersten Edit genau die zehn `read_first`-Dateien. Zwei
weitere Dateien sind für die unmittelbar betroffenen Testfälle
`read_on_demand`; damit bleibt `max_initial_files: 12`. Keine vollständige
Solution-Lektüre und kein globaler Audit-Kontext ist vorgesehen.

### `read_first` (10 Dateien)

1. `tasks/decompiled-assembly-analysis/step-034/step-plan.md`
2. `tasks/decompiled-assembly-analysis/step-034/step-result.md`
3. `tasks/decompiled-assembly-analysis/step-034/step-review.md`
4. `src/AiNetLinter/Configuration/ExternalSourceConfiguration.cs`
5. `src/AiNetLinter/Configuration/ExternalSourceConfigurationLoader.cs`
6. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheOptionsFactory.cs`
7. `src/AiNetLinter/Mcp/Assemblies/AssemblySourceSelectionOrchestrator.cs`
8. `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupport.cs`
9. `src/AiNetLinter/Mcp/IsErrorPolicy.md`
10. `src/AiNetLinter.FastTests/Configuration/ExternalSourceCacheRootValidationTests.cs`

### `read_on_demand` (2 Dateien)

- `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisConfigurationFailureTests.cs`
- `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupportTests.cs`

Die vom Coder vor dem Edit auszuführenden MCP-Abfragen verwenden immer den
absoluten `projectRoot` `C:/Daten/Entwicklung/Ralf/AiNetLinter`; die bereits
konsultierte CodeMap ist kein zusätzlicher Initialkontext.

## Aktueller Projektzustand (JIT-Kontext)

- `ExternalSourceConfigurationLoadResult.Succeeded` ist bereits ein
  unveränderlicher abgeleiteter Wert aus `Configuration != null` und leerer
  Diagnoseliste; `Failure` lässt bewusst auch `[]` zu. Der Loader liefert bei
  seinen aktuellen Fehlerstellen meist Diagnosen, aber diese zufällige
  Nichtleere ist kein Vertragsmarker.
- `AssemblySourceSelectionOrchestrator.ResolveAsync` beendet einen nicht
  erfolgreichen Config-Load bereits vor Assembly-Auflösung, Provider und
  Registry. `CreateScope` übergibt dabei nur die Loader-Diagnosen.
- `AssemblySourceSelectionScope.Status` entscheidet bei leerer Auswahl aktuell
  zwischen `ProviderUnavailable`, `NoMatch` und `ConfigurationFailure` anhand
  des Provider-Failure-Kinds bzw. `LoaderDiagnostics.IsEmpty`. Genau diese
  Ableitung muss durch ein explizites Scope-Merkmal ersetzt oder überlagert
  werden; `NoMatch` darf weiterhin die gültige leere Konfiguration bedeuten.
- `AssemblyAnalysisToolSupport.ExecuteAsync` besitzt bereits den richtigen
  frühen Check vor `AssemblyAnalysisService.CreateContextAsync` und liefert
  über `McpToolResults.Recoverable` ein strukturiertes, korrigierbares
  Resultat mit `IsError=false`. Nach der Marker-Korrektur darf dieser Pfad
  nicht weiter geöffnet oder global in `McpToolResults` umgebaut werden.
- `ExternalSourceConfigurationPath` wird von Loader, direkten Options und
  Cache-Fabrik gemeinsam verwendet. Die Factory erhält bereits validierte
  `ExternalSourceCacheOptions`; ein invalides Roh-Objekt darf nicht per
  Reflection oder Test-Seam künstlich erzeugt werden. Die Matrix muss daher
  ungültige Rohwerte an Loader/Options prüfen und die akzeptierten Drive-/UNC-
  Repräsentanten zusätzlich durch die Factory unverändert kanonisieren.
- `ExternalSourceCacheRootValidationTests.cs` enthält bereits die vier
  Reviewformen, einige Device-/reservierte/dot-Fälle sowie einen gültigen
  Backslash-UNC-Pfad. Es fehlen eigenständige `https://`-,
  `//user:...@host/...`- und unvollständige UNC-Fälle; die doppelte Inline-
  Datenliste ist eine lokale Test-Duplikation, die mit der Matrix konsolidiert
  werden soll.
- `AssemblyAnalysisConfigurationFailureTests.cs` und die kombinierte Gruppe
  in `AssemblyAnalysisToolSupportTests.cs` beweisen den nichtleeren Failure-
  Fall und die positiven NoMatch-/Ambiguous-/Provider-/Capability-Fallbacks.
  Die Failure-Assertions verwenden an den betroffenen Stellen noch
  `Assert.NotEqual(true, result.IsError)`.

## Intention

Ein expliziter Config-Failure bleibt unabhängig von `Diagnostics.Length`
terminal und wird als eigener `ConfigurationFailure`-Status bis
`AssemblyAnalysisToolSupport` propagiert. Der vorhandene `Recoverable`-
Vertrag bleibt dabei ausdrücklich `IsError=false`: korrigierbar bedeutet hier
eine behebbare Konfiguration, nicht einen erfolgreichen Analyse-Fallback.
Die präzise URI-/UNC-Matrix und die End-to-End-Assertions sollen diese Grenze
reproduzierbar machen, ohne gültige leere oder nicht-quellfähige Auswahlpfade
zu verändern.

## Konkrete Änderungen

### 1. Selection-Status explizit und diagnoseunabhängig machen

**Datei:** `src/AiNetLinter/Mcp/Assemblies/AssemblySourceSelectionOrchestrator.cs`

- Ergänze an `AssemblySourceSelectionScope` ein unveränderliches Merkmal für
  den Config-Failure, oder übergib den Status äquivalent explizit beim Aufbau
  der Scope. Es darf nicht aus `LoaderDiagnostics.IsEmpty` abgeleitet werden.
- Der Branch `!configurationResult.Succeeded` in `ResolveAsync` erzeugt eine
  terminale Config-Failure-Scope mit allen vorhandenen Loader-Diagnosen und
  ohne Selection, Provider-Aufruf, Registry-Acquisition oder Lease. Das gilt
  auch für `Failure([])`.
- Lege die Statusrangfolge deterministisch fest: echte Selection-Zustände
  bleiben maßgeblich; bei leerer Selection hat der explizite Config-Failure-
  Marker Vorrang vor Provider-Failure und `NoMatch`; erst danach gelten
  `ProviderUnavailable` bzw. `NoMatch`.
- `LoaderDiagnostics` darf bei `Failure([])` leer bleiben. Es ist kein
  synthetischer Diagnoseeintrag und keine Änderung an
  `ExternalSourceConfigurationLoadResult.Failure` erforderlich, nur damit die
  Statusklassifikation funktioniert.

### 2. Bestehende Tool-Policy am terminalen Gate festhalten

**Datei:** `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupport.cs`

- Stelle sicher, dass der vorhandene Status-Check die explizite
  `ConfigurationFailure`-Scope vor `AssemblyAnalysisService.CreateContextAsync`
  und vor `parameters.BuildResult` beendet. Die Factory darf in diesem Pfad
  nicht erfolgreich erreicht werden.
- Verwende weiterhin ausschließlich `McpToolResults.Recoverable` mit dem
  vorhandenen Diagnosecode oder dem bestehenden sicheren Fallback-Code und
  einem sicheren Korrektur-Hint. Der Resultatvertrag ist strukturiert,
  secret-frei, `IsError=false` und enthält weder Context-,
  `OriginKind=decompiled`- noch Build-Payload.
- `src/AiNetLinter/Mcp/McpToolResults.cs` bleibt unverändert. Die genaue
  Bedeutung wird in `src/AiNetLinter/Mcp/IsErrorPolicy.md` nur dann minimal
  präzisiert, wenn die vorhandene Zeile den Unterschied zwischen terminaler
  Analyseentscheidung und `IsError=false` nicht ausdrücklich genug festhält.

### 3. URI-/UNC-Matrix und Options-/Factory-Grenzen schärfen

**Datei:** `src/AiNetLinter/Configuration/ExternalSourceConfiguration.cs`

- Erhalte die vorhandene side-effect-freie Rohprüfung vor
  `Path.GetFullPath`. Sie muss die bereits dokumentierten URI-/Credential-,
  Query-/Fragment-, Nicht-Drive-Doppelpunkt-, Device-, reservierten und
  Dot-Segment-Grenzen unverändert fail-closed halten.
- Definiere die UNC-Grenze exakt: `\\server\\share` und
  `\\server\\share\\cache` sowie die äquivalente Slash-Schreibweise sind
  vollständige UNC-Pfade; server-only Formen wie `\\server`/`//server`
  ohne Share sind unvollständig und werden vor der Kanonisierung abgewiesen.
  Authority-/Userinfo-Formen wie `//user:secret@host/share/cache` bleiben
  URI-artig und werden nicht als UNC akzeptiert. Eine zusätzliche Prüfung darf
  nur diese Rohformgrenze schärfen und die allgemeine Root-/Reparse-/Ownership-
  Prüfung nicht global verändern.
- `ExternalSourceCacheOptions` und
  `ExternalSourceRepositoryCacheOptionsFactory` bleiben an derselben
  gemeinsamen CacheRoot-Semantik. Defaults, relative Auflösung,
  `CacheRoot/source` und `RefreshIntervalMinutes` werden nicht umdefiniert.
  Wenn die bestehende Implementierung die UNC-Grenze bereits exakt erfüllt,
  genügt der Testnachweis; kein redundanter zweiter Validator wird angelegt.

### 4. Lokale Regressionen und Testdateigrenzen

**Datei:** `src/AiNetLinter.FastTests/Configuration/ExternalSourceCacheRootValidationTests.cs`

- Konsolidiere die wiederholten invaliden Rohwerte in eine gemeinsame,
  datengetriebene Matrix und ergänze eigenständige Fälle für:
  `https://user:secret@example.invalid/cache`,
  `https:/user:secret@example.invalid/cache`,
  `file:/C:/secret`,
  `//user:secret@host/share/cache`, `?`/`#`, Nicht-Drive-Doppelpunkt,
  Device-/reservierte/Dot-Segmente sowie `\\server` und `//server`.
- Halte positive Vertreter getrennt fest: ein gültiger relativer Loader-Root,
  ein absoluter Laufwerks-Root, `\\server\\share\\cache`,
  `//server/share/cache` und, falls als Root unterstützt, `\\server\\share`.
  Loader und direkter Optionskonstruktor müssen dieselben invaliden Rohwerte
  ablehnen; nur der Loader darf relative Werte gegen das Settings-Verzeichnis
  auflösen, während Options und Factory weiterhin absolute Werte erwarten.
- Prüfe für die akzeptierten absoluten Vertreter zusätzlich den direkten
  `ExternalSourceRepositoryCacheOptionsFactory.Create`-Pfad auf identische
  kanonische Cache-/Source-Roots und Refresh-Weitergabe. Ungültige Rohwerte
  werden nicht künstlich in ein bereits konstruiertes Options-Objekt injiziert.
- Jede negative Diagnose/Exception bleibt generisch und enthält weder den
  Rohwert noch `secret`; kein Test greift auf Netzwerk, UNC-Inhalte oder
  Betriebssystem-Temp zu. `TestTempDirectory` bleibt der einzige lokale
  Testroot.

**Datei:** `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisConfigurationFailureTests.cs`

- Ergänze eine direkte `Failure([])`-Regression mit lokaler Test-DLL,
  Recording-Provider und Registry. Sie prüft `ConfigurationFailure`, leere
  Selection, `LoaderDiagnostics.IsEmpty`, null Provider-Aufrufe, null
  Registry-Leases/Residents, keinen Context-/Build-Aufruf, deterministisches
  Scope-Dispose und ein strukturiertes terminales Resultat.
- Ergänze im selben Testbereich die Gegenprobe
  `Success(ExternalSourceConfiguration.Empty)`: Sie bleibt `NoMatch` und
  liefert den gewöhnlichen erfolgreichen statischen Decompilation-Fallback.
- Ersetze im Failure-Pfad `Assert.NotEqual(true, result.IsError)` durch die
  exakte `Assert.False(result.IsError)`-Aussage und prüfe zusätzlich Code,
  sicheren Text/Hint und fehlende `StructuredContent`-/Decompilation-Payload.

**Datei:** `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupportTests.cs`

- Korrigiere ausschließlich die direkt betroffene synthetische
  Config-Failure-Assertion auf `Assert.False(result.IsError)` und lasse die
  positiven NoMatch-, Ambiguous-, ProviderUnavailable- und
  Capability-Fallback-Assertions als aktive Regressionen bestehen.
- Keine weiteren Testfälle in diese bereits 482-zeilige Datei verschieben;
  sie bleibt unter der Test-Dateigrenze. Neue Failure([])- und Empty-Success-
  Fälle gehören in die fokussierte ConfigurationFailure-Testdatei.

**Datei:** `src/AiNetLinter/Mcp/IsErrorPolicy.md`

- Präzisiere die bestehende ExternalSources-Zeile ausdrücklich: Ein
  `ConfigurationFailure` stoppt die Assembly-Analyse vor der Context-
  Erzeugung, bleibt aber als korrigierbarer Konfigurationsfehler
  `isError=false` über `Recoverable`. Keine Änderung der übrigen Policy-Tabelle.

## Scope

- Diagnoseunabhängige, immutable `ConfigurationFailure`-Weitergabe von
  Loader/Selection bis zum Assembly-Tool.
- Exakte rohe URI-/UNC-CacheRoot-Klassifikation sowie direkte Options-/Factory-
  Nachweise an derselben bestehenden Validierungsgrenze.
- End-to-End-Resultatassertions mit explizitem `IsError=false` und Erhalt der
  positiven NoMatch-, Ambiguous-, ProviderUnavailable- und Capability-
  Decompilation-Fallbacks.
- Lokale, isolierte Regressionen und nur direkt berührte DRY-/MagicValues-/
  DeadCode-Korrekturen innerhalb dieser Vertragsgrenze.

## Out of Scope

- Jede globale Änderung an `McpToolResults.cs`, neue Resultattypen oder ein
  Host-/MCP-Wiring in `AssemblyAnalysisHostComposition`,
  `McpServerCommand`, `DaemonHostCommand`, Registrierungen oder SDK-Bindings.
- Änderungen an Provider-/Registry-Akquisition, Snapshot-/Workspace-Lifetime,
  Fetch/Refresh/Publish/Rollback, Current-Pointer, Retention/GC,
  Dirty/Unbuilt-, Health/Degraded-, Reparse- oder EPIC-05-Verträgen.
- Änderung der statischen Decompilation, `AssemblyAnalysisContextFactory`,
  `AssemblyAnalysisService` oder der positiven Fallback-Logik.
- Neue Netzwerk-, Credential-, Git-, Assembly-Load-, Reflection- oder globale
  Reparse-/Win32-Aktionen.
- Globale DRY-, MagicValues-, DeadCode- oder Safeguard-Sweeps sowie eine
  Neubewertung der Step-034-Evidenz; nur direkte aktuelle Resultatassertions
  dürfen präzisiert werden.
- Stress-Ausführung und jede Änderung an `TD-001` bis `TD-003`.
- Produktionsänderungen, Testläufe sowie Coder-/Kritikerarbeit während dieses
  Planer-Schritts.

## Architekturgrenze

Die neue Entscheidung liegt zwischen validiertem
`ExternalSourceConfigurationLoadResult` und dem Assembly-Tool. Die
Configuration-Schicht liefert weiterhin immutable Optionen oder sichere
Diagnosen. Die Selection-Schicht trägt den expliziten terminalen
Config-Failure-Marker unabhängig von `Diagnostics.Length` und trennt ihn von
gewöhnlichen leeren/providerbedingten Scopes. Die Tool-Schicht konsumiert nur
diesen Status, beendet ihn vor Context/Build und nutzt den bestehenden
`Recoverable`-Vertrag. Provider, Registry, Cache-Generationen, Host-Wiring und
Decompilation bleiben außerhalb.

## Invarianten

1. `ExternalSourceConfigurationLoadResult.Failure([])` bleibt
   `Succeeded == false` und `Configuration == null`; seine Scope ist trotzdem
   eindeutig `ConfigurationFailure`.
2. Ein Config-Failure wird nie anhand der Anzahl oder des Inhalts von
   Loader-Diagnosen als `NoMatch` klassifiziert und erreicht weder Provider
   noch Registry-Acquisition/Lease.
3. `AssemblyAnalysisToolSupport` beendet jeden terminalen Config-Failure vor
   `AssemblyAnalysisService.CreateContextAsync` und `BuildResult`; es entsteht
   kein Context, keine statische Decompilation-Payload und kein BuildResult.
4. Das terminale Toolresultat verwendet die bestehende `Recoverable`-Policy:
   strukturierter Diagnosecode, sicherer Hint, `IsError == false`, keine
   `StructuredContent`- oder Secret-Ausgabe. `isError=false` bedeutet hier
   korrigierbare Konfiguration, nicht erfolgreiche Analyse.
5. `Success(ExternalSourceConfiguration.Empty)` bleibt `NoMatch` und darf den
   erfolgreichen statischen Decompilation-Fallback erreichen.
6. `NoMatch`, `Ambiguous`, `ProviderUnavailable` und
   `RepositoryCapabilityUnavailable` behalten ihre bisherige erfolgreiche
   statische Decompilation und ihre Diagnose-/Providersemantik.
7. Nur vollständige UNC-Pfade mit Server und Share werden akzeptiert; URI-/
   Authority-/Userinfo-Formen, server-only UNC, Query/Fragment,
   Nicht-Drive-Doppelpunkt, Device-/reservierte und Dot-Segment-Formen werden
   vor der Kanonisierung verworfen. Gültige relative Loader-Werte sowie
   absolute Drive- und UNC-Werte bleiben möglich.
8. Alle neuen Regressionen bleiben lokal und isoliert: zentrale
   `TestTempDirectory`, keine Netzwerk-/Credential-/Assembly-Ladeaktion,
   deterministisches Dispose, keine globale Testserialisierung und kein
   Stress-Test.

## Abnahmekriterien (7)

1. Die gemeinsame URI-/UNC-Matrix weist `https://...`,
   `https:/...`, `file:/...`, Authority/Userinfo, Query/Fragment,
   Nicht-Drive-Doppelpunkt, Device-/reservierte/Dot-Segmente und server-only
   UNC (`\\server`, `//server`) vor der Kanonisierung ab; vollständige
   `\\server\\share`-/`\\server\\share\\cache`- sowie
   `//server/share/cache`-Formen sowie ein gültiger relativer Loader-Root und
   absolute Drive-Roots bleiben gültig.
2. Loader, direkter `ExternalSourceCacheOptions`-Konstruktor und die
   `ExternalSourceRepositoryCacheOptionsFactory` teilen dieselbe geprüfte
   CacheRoot-Semantik; kein ungültiger Rohwert fällt auf Default zurück,
   und Diagnose/Exception bleiben ohne Rohpfad und Secret. `source`-Unterroot,
   Default und Refresh-Weitergabe bleiben unverändert.
3. `Failure([])` erzeugt eine disposed Scope mit
   `AssemblySourceSelectionStatus.ConfigurationFailure`, leerer Selection
   und ggf. leeren Loader-Diagnosen; Provider bleibt unaufgerufen und die
   Registry resident/lease-frei. `Success(ExternalSourceConfiguration.Empty)`
   bleibt dagegen `NoMatch`.
4. Der reale Toolpfad beendet `ConfigurationFailure` vor Context-Fabrik und
   Result-Builder mit einem strukturierten `Recoverable`-Resultat,
   `Assert.False(result.IsError)`, sicherem Diagnosecode/Hint und ohne
   `StructuredContent`, Context oder `decompiled`-Payload.
5. Die bestehenden positiven NoMatch-, Ambiguous-, ProviderUnavailable- und
   Capability-Fallbacks bleiben erfolgreiche statische Decompilation; kein
   gewöhnlicher Fallback wird durch den neuen Marker fail-closed.
6. Die Regressionen sind vollständig lokal/testisoliert, verwenden
   `TestTempDirectory`, halten die drei betroffenen Testdateien unter der
   projektspezifischen MaxLineCount-Grenze und führen weder Netzwerk,
   Credentials, Assembly-Loading noch globale Reparse-Aktionen aus.
7. `dotnet build`,
   `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` und
   `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`
   laufen grün; `Category=Stress` wird nicht ausgeführt. Das Step-Result
   dokumentiert exakte Zahlen, bekannte Win32-1314-Skips, den direkten
   `IsError=false`-Nachweis und nur scoped MCP-/DRY-/MagicValues-/DeadCode-
   Ergebnisse; der bestehende Safeguard-FAIL wird nicht umgedeutet.

## Teststrategie

Der Coder führt zuerst nur die drei fokussierten Testgruppen aus:

- `ExternalSourceCacheRootValidationTests`
- `AssemblyAnalysisConfigurationFailureTests`
- der direkt betroffene Failure-Fall in `AssemblyAnalysisToolSupportTests`

Danach folgen `dotnet build` und die beiden vollständigen Nicht-Stress-Gates
aus der Roadmap. Bei einem roten Gate wird die konkrete Ursache im selben
Step behoben oder der Step nach den Workflow-Regeln als blockiert markiert;
Assertions werden nicht abgeschwächt. Die beiden bekannten
Win32-1314-Reparse-Skips bleiben hosttransparent und werden nicht simuliert.

## MCP-, DRY-, Magic-Values- und Dead-Code-Disposition

Vor und nach Änderungen fragt der Coder die betroffenen C#-Symbole über
projektgebundenes MCP mit `projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter`
ab: `get_feature_context`, `get_symbol_body`, `find_references` und
`get_impact`; nach dem Patch zusätzlich gezielt `get_violations` bzw.
`safeguard` für den relevanten Scope. Die zuständigen Symbole sind:

- `T:AiNetLinter.Configuration.ExternalSourceConfigurationLoadResult`
- `M:AiNetLinter.Configuration.ExternalSourceConfigurationLoadResult.Failure`
  (bei Auflösungsabweichung über `find_symbol` ermitteln)
- `T:AiNetLinter.Mcp.Assemblies.AssemblySourceSelectionOrchestrator`
- `T:AiNetLinter.Mcp.Assemblies.AssemblySourceSelectionScope`
- `P:AiNetLinter.Mcp.Assemblies.AssemblySourceSelectionScope.Status`
- `M:AiNetLinter.Mcp.Assemblies.AssemblySourceSelectionOrchestrator.ResolveAsync`
- `T:AiNetLinter.Mcp.Tools.AssemblyAnalysis.AssemblyAnalysisToolSupport`
- `M:AiNetLinter.Mcp.Tools.AssemblyAnalysis.AssemblyAnalysisToolSupport.ExecuteAsync`
- `M:AiNetLinter.Mcp.Tools.AssemblyAnalysis.AssemblyAnalysisToolSupport.CreateConfigurationFailureResult`

Der Coder darf die doppelte Matrix-Testdatenliste in
`ExternalSourceCacheRootValidationTests` zentralisieren und einen nur hier
entstehenden Validator-/Status-Magic-Value konsolidieren. Sonstige
DRY-/MagicValues-/DeadCode-Funde werden nur behoben, wenn sie direkt an dieser
Options-/Status-/Tool-Grenze liegen und ohne neuen Vertragsumfang beseitigt
werden können. `TD-001` bis `TD-003`, der breite Safeguard-FAIL und bestehende
semantisch getrennte Helper werden nicht in einen globalen Sweep umgewandelt.

## Definition of Done für den Folge-Coder

- [ ] Vorabprüfung, Testdateigrößen und MCP-Symbol-/Resultat-Policy-Prüfung
      abgeschlossen; keine Zeilenannahme ungeprüft übernommen.
- [ ] `ConfigurationFailure` ist als expliziter Scope-Status unabhängig von
      leerer/nichtleerer Diagnoseliste bis zum Tool sichtbar.
- [ ] Der terminale Config-Failure ruft weder Provider/Registry noch
      Context-Fabrik/Build-Result auf und liefert `Recoverable` mit exakt
      `IsError=false`.
- [ ] URI-/Authority-/UNC-Matrix ist vollständig und klassifiziert
      server-only UNC getrennt von vollständigem `server/share`; Loader,
      Options und Factory behalten die bestehende Root-/Refresh-Semantik.
- [ ] Positive NoMatch-, Ambiguous-, ProviderUnavailable- und Capability-
      Fallbacks bleiben durch bestehende bzw. fokussierte Tests grün.
- [ ] `IsErrorPolicy.md` ist zur tatsächlich geltenden Recoverable-Semantik
      explizit; `McpToolResults.cs` bleibt unverändert.
- [ ] Build und beide Nicht-Stress-Gates sind mit exakten Zahlen, bekannten
      Skips und Stress-Nichtausführung dokumentiert; scoped MCP-/Qualitäts-
      Nachweise sind reproduzierbar.
- [ ] Coder-Commit und danach ein frischer Kritiker gemäß Orchestrator-
      Ablauf; kein bestehender Agent wird wiederverwendet.

## Regelreferenzen

- `.agents/rules/AiNetLinter-McpWorkflow.mdc` — projektgebundenes MCP vor
  ergänzender Textsuche, absolute Projektziele sowie scoped Symbol-/Impact-
  und Safeguard-Prüfungen.
- `.agents/rules/AiNetLinter.mdc` — C#-Grenzwerte, keine stillen Fehlerpfade,
  strukturierte Resultate und Vermeidung von DuplicateCode/MagicValues.
- `.agents/rules/AiNetLinterRichtlinien.mdc` — lokale Testisolation,
  Dokumentationswahrheit, Result-Pattern, DRY-/MagicValues-/DeadCode-
  Disposition, deutsche Commit-Konvention und Nicht-Stress-Abschlussgates.
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/orchestrator.md` — genau
  ein frischer Coder und Kritiker seriell, Fix-Scope, Statuspflege und kein
  Audit-only-/Mini-Step.

## Sicherer Handoff

Der nächste sichere Einstieg ist ein neuer Coder-Agent im Branch `main`:

1. Lies zuerst den Handoff, diesen Plan und die drei Step-034-Artefakte sowie
   die zehn `read_first`-Dateien; öffne `read_on_demand` erst für den
   jeweiligen Testpatch.
2. Starte die MCP-Semantikprüfung am Branch
   `!configurationResult.Succeeded` in `ResolveAsync`, anschließend an Scope-
   Konstruktor/`Status`, dann am vorhandenen Tool-Gate. Der erste Patch soll
   nur den expliziten Statusmarker und die direkte Regression herstellen.
3. Prüfe danach die Matrix gegen die exakt festgelegte UNC-Grenze und ändere
   die Rohvalidierung nur, wenn der aktuelle Code den server-only-Fall nicht
   bereits vor der Kanonisierung abweist. Keine neue globale Root-API.
4. Verifiziere die bestehende `Recoverable`-/`IsError=false`-Policy mit
   `Assert.False`, halte die positiven Fallbacks und die bekannten Skips
   sichtbar und führe erst danach die Abschlussgates aus.

Der Planer führt in diesem Schritt keine Produktionsänderung, keinen Testlauf
und keine Coder-/Kritikerarbeit aus. Der Orchestrator übergibt den Plan an
einen frischen Coder, schließt ihn nach Abschluss und startet für die Prüfung
einen neuen, separaten Kritiker.
