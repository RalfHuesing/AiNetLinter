---
status: open
type: step-plan
task: decompiled-assembly-analysis
step: 034
corrects: step-033
title: "Strikter CacheRoot-Vertrag und fail-closed Konfigurationsweitergabe bis zum Assembly-Tool"
epic: EPIC-04
estimated_risk: high
step_type: single
items: []
created_by: planer
created_by_model: gpt-5 (Codex)
created_by_model_knowledge_cutoff: nicht angegeben
created_at: 2026-08-30T01:29:10+02:00
related_to:
  - ../step-033/step-plan.md
  - ../step-033/step-result.md
  - ../step-033/step-review.md
---

# Step 034: Strikter CacheRoot-Vertrag und fail-closed Konfigurationsweitergabe bis zum Assembly-Tool

## Bezug und Bündelungsentscheidung

- **Task:** `decompiled-assembly-analysis`
- **Epic:** `EPIC-04`; Gitea bleibt die Source of Truth, der lokale
  Source-Cache bleibt eine validierte und besitzgeschützte Zwischenstufe.
- **Korrekturziel:** Step-033, Review `d57f5aab`.
- **MAJOR-001:** `ExternalSourceConfigurationPath` prüft nur einen Teil der
  rohen Pfadform. URI-/Credential-artige und reservierte Formen wie
  `https:/user:secret@example.invalid/cache`, `file:/C:/secret`,
  `C:/temp/a:secret` und `C:/temp/a?b` können noch als Dateipfade enden;
  das widerspricht dem dokumentierten geheimnisfreien `CacheRoot`-Vertrag.
- **MAJOR-002:** `AssemblySourceSelectionOrchestrator` und
  `AssemblyAnalysisToolSupport` behandeln `Configuration == null` derzeit
  noch wie eine gewöhnliche leere Source-Auswahl. Dadurch kann die
  `AssemblyAnalysisContextFactory` bis zur statischen Decompilation gelangen
  und ein erfolgreiches Ergebnis liefern; ein bestehender Test schreibt
  dieses Fehlverhalten fest.

Das ist ein gemeinsames Korrekturpaket mit einem Primärvertrag: **Eine explizit
vorhandene, ungültige ExternalSources-Konfiguration ist terminal; sie darf
weder durch eine schwächere CacheRoot-Normalisierung noch durch die statische
Decompilation in einen erfolgreichen Assembly-Tool-Aufruf umgewandelt werden.**
Die Rohpfadvalidierung, der Statusmarker an der Auswahlgrenze und die lokale
End-to-End-Regression gehören deshalb zusammen. Ein isolierter Assertion-Fix,
ein Audit-only-Step oder ein separater Pfad-/Resultat-Mini-Step würde genau die
Vertragsgrenze auseinanderziehen, die der Kritiker beanstandet hat.

## Split-Gate und Kontextbudget

Dieser Step ist ein größeres vertikales Paket mit genau einem Primärvertrag und
drei gekoppelten Schichten:

1. strikte, gemeinsame Roh-`CacheRoot`-/Optionsvalidierung;
2. terminale Konfigurationsstatus-Weitergabe von Loader über Selection bis zum
   Assembly-Tool-Resultat;
3. lokale adversariale Pfadtests und eine reale Loader-zu-Tool-Regression.

Es gibt höchstens acht Abnahmekriterien (siehe unten). Der Coder liest vor
seinem ersten Edit genau die zehn `read_first`-Dateien. Höchstens zwei weitere
unmittelbar gekoppelte Dateien dürfen danach initial nachgeladen werden;
`max_initial_files: 12`. Weitere Dateien werden erst bei einem konkreten
Symbol- oder Testbezug gelesen. Die unten genannten Zeilen sind nur aktuelle
Anker und müssen vor dem Patch gegen den tatsächlichen Stand neu geprüft
werden.

### `read_first` (10 Dateien)

1. `tasks/decompiled-assembly-analysis/codemap.md`
2. `tasks/decompiled-assembly-analysis/step-033/step-result.md`
3. `tasks/decompiled-assembly-analysis/step-033/step-review.md`
4. `src/AiNetLinter/Configuration/ExternalSourceConfiguration.cs`
5. `src/AiNetLinter/Configuration/ExternalSourceConfigurationLoader.cs`
6. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheOptionsFactory.cs`
7. `src/AiNetLinter/Mcp/Assemblies/AssemblySourceSelectionOrchestrator.cs`
8. `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupport.cs`
9. `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextFactory.cs`
10. `src/AiNetLinter/Mcp/IsErrorPolicy.md`

Die beiden verpflichtenden `read_on_demand`-Dateien sind zunächst:

- `src/AiNetLinter.FastTests/Configuration/ExternalSourceConfigurationLoaderTests.cs`
- `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupportTests.cs`

Vor der Implementierung muss der Coder zusätzlich per projektgebundenem MCP
die Bodies von `McpToolResults.Error`, `McpToolResults.Recoverable` und
`McpToolResults.CompilationError` sowie die beteiligten Selection-Symbole
prüfen. Falls die aktuelle Resultat-Policy ohne eine Änderung am globalen
Resultatvertrag keine terminale Konfigurationsdiagnose ausdrücken kann, ist
`src/AiNetLinter/Mcp/McpToolResults.cs` die einzige dafür nachzuladende
Ersatzdatei; kein globales Resultat-Redesign eröffnen.

## Aktueller JIT-Projektzustand

Die Review-Befunde sind im aktuellen Stand reproduzierbar begrenzt:

- `ExternalSourceCacheOptions` konstruiert in
  `ExternalSourceConfiguration.cs` (aktuell etwa Zeilen 12–43) aus dem
  kanonisierten Root. `ExternalSourceConfigurationPath` (aktuell etwa
  Zeilen 169–249) weist leere Werte, `://` und `.`/`..`-Segmente ab, aber
  nicht jede URI-/Credential-artige oder Windows-reservierte Rohform.
- Der Loader liest in `TryReadCacheOptions` und `TryReadCacheRoot` (aktuell
  etwa Zeilen 196–253) die strukturierten Diagnosen und liefert bei einem
  ungültigen Feld bereits `Configuration == null`; die fehlende
  Konfigurationsgültigkeit wird erst später semantisch verwischt.
- `ExternalSourceRepositoryCacheOptionsFactory.Create` verwendet den
  konfigurierten Root nochmals über die allgemeinere
  `TryCanonicalizeAbsoluteRoot`-Grenze. Der Coder muss deshalb eine einzige
  CacheRoot-Sicherheitssemantik herstellen, ohne die generische Root-Prüfung
  für bestehende Staging-/Reparse-/Ownership-Pfade unbedacht zu verschärfen.
  `<CacheRoot>/source`, die vorhandene Writer-Sicherheitsgrenze und die
  Step-033-Refresh-/Factory-/Policy-Verdrahtung bleiben erhalten.
- `AssemblySourceSelectionOrchestrator.ResolveAsync` (aktuell etwa Zeilen
  39–102) erzeugt bei `!configurationResult.Succeeded` eine leere Scope. Die
  Scope-Konstruktion (`CreateScope`, aktuell etwa Zeilen 104–112) unterscheidet
  diesen Zustand derzeit nicht von No-Match oder Provider-/Capability-Failure.
- `AssemblyAnalysisToolSupport.ExecuteAsync` mit Orchestrator (aktuell etwa
  Zeilen 33–76) ruft die Context-Erzeugung auch für diese leere Scope auf.
  Die Factory darf für gewöhnliche No-Match-, ProviderUnavailable- und
  Capability-Fälle weiterhin statisch dekompilieren; genau diese Fallback-
  Semantik ist zu erhalten, während nur der explizite Config-Failure vor dem
  Factory-Aufruf terminal wird.
- Die vorhandenen Loader-Tests verwenden `TestTempDirectory` und stehen
  aktuell nahe der 500-Zeilen-Grenze. Die vorhandenen
  `AssemblyAnalysisToolSupportTests` enthalten den fehlerhaften synthetischen
  Config-Failure-Teil zusammen mit gültigen No-Match-/Ambiguous-Fallbacks und
  stehen ebenfalls nahe der Dateigrenze. Die neuen adversarialen und
  End-to-End-Fälle gehören daher in fokussierte neue Testdateien; die
  bestehenden Fallback-Tests dürfen nicht durch bloßes Verschieben oder
  Überschreiben ihre Aussage verlieren.
- Die Dokumentation behauptet bereits, dass URI-artige bzw. unsichere
  `CacheRoot`-Formen abgewiesen werden und ein ungültiger Abschnitt nicht
  erfolgreich weiterläuft. Step 034 soll diese Aussage durch den Code wahr
  machen; `Docs/configuration.md` wird nur angepasst, wenn die nach der
  Vorabprüfung bestätigte endgültige Grammatik eine andere Nutzerbeschreibung
  erfordert.

## Vorabprüfung des Coders (verbindliche Übergabe)

Vor jeder Produktions- oder Teständerung muss der frische Coder-Agent:

1. die oben genannten aktuellen Zeilen-/Dateigrenzen und die tatsächlichen
   Dateigrößen prüfen; Zeilennummern aus diesem Plan nie als Patchziel
   voraussetzen;
2. den realen Loader-Fehlerzustand verfolgen:
   `ExternalSourceConfigurationLoadResult.Succeeded`, `Configuration == null`,
   Diagnosecode `CacheRootInvalid`, Diagnose-Text und die Stelle, an der die
   `ExternalSourceSelectionScope` daraus eine Auswahl erzeugt;
3. die vorhandene `McpToolResults`-/`IsErrorPolicy`-Darstellung prüfen und
   anhand dieses Vertrags festlegen, welches strukturierte, nicht erfolgreiche
   Resultat für einen terminalen Config-Failure verwendet wird. Der neue Test
   darf weder eine erfolgreiche `AssemblyContext`-/Decompilation-Payload noch
   einen nur textuell angehängten Fehler als ausreichend akzeptieren;
4. die Testisolation und die bestehende Fallback-Semantik anhand der beiden
   Testdateien verifizieren: zentraler `TestTempDirectory`, keine externen
   Netzwerk-/Credential-/Assembly-Ladeaktionen, keine erzwungene
   Collection-Serialisierung, deterministisches Dispose von Scope/Registry;
5. prüfen, ob der derzeitige Optionskonstruktor, die Options-Fabrik und der
   Loader dieselbe Rohwertklasse sehen. Die allgemeine
   `ExternalSourceRepositoryCacheContract`-Kanonisierung darf nicht durch
   einen globalen Security-Sweep verändert werden, wenn sie für direkte
   Staging-/Source-Roots einen breiteren, bereits geprüften Vertrag hat.

**Sicherer Einstiegspunkt:** erst die gemeinsame Klassifikation am
`ExternalSourceConfigurationPath`-/`ExternalSourceCacheOptions`-Rand klären,
dann den expliziten terminalen Status an der `ResolveAsync`-Scope setzen und
schließlich direkt vor `AssemblyAnalysisService.CreateContextAsync` im
Orchestrator-Overload von `AssemblyAnalysisToolSupport` abfangen. Die
`AssemblyAnalysisContextFactory` ist dabei zunächst eine zu lesende
Fallback-Grenze, kein erwarteter Änderungsort.

## Konkrete Änderungen

### Schicht 1: Strikte Roh-CacheRoot- und Optionsvalidierung

**Produktionsbereich:**

- `ExternalSourceConfiguration.cs`: Eine gemeinsame, immutable/side-effect-
  freie Prüfung für den rohen `CacheRoot`-Wert vor der Kanonisierung
  definieren oder an einer neutralen Stelle extrahieren. Sie muss zwischen
  erlaubten Dateipfaden und nicht erlaubten URI-/Credential-/Device-Formen
  unterscheiden, ohne die bestehende Auflösungsbasis für relative Werte zu
  verändern.
- Als verbindliche Negativfälle abdecken: `https:/...`, `file:/...`,
  Authority-/Userinfo-artige Formen, `?`/`#`-Segmente, einen Doppelpunkt
  außerhalb des Windows-Drive-Präfixes (`C:/temp/a:secret`) sowie Windows-
  Device-/reservierte Pfadformen und `.`/`..`-Segmente. Die Prüfung soll
  nicht nur auf `://` oder einer späteren `Path.GetFullPath`-Interpretation
  beruhen. Gültige relative Cache-Verzeichnisse und echte absolute
  Laufwerks-/UNC-Pfade müssen entsprechend der bestehenden Dokumentation
  weiter funktionieren.
- `ExternalSourceCacheOptions` muss dieselbe Sicherheitssemantik direkt am
  internen Konstruktionsrand erzwingen. Ein direkter Options-Aufruf darf die
  Loaderprüfung nicht umgehen; Fehler bleiben generisch und enthalten weder
  den Rohwert noch ein mögliches Secret.
- `ExternalSourceRepositoryCacheOptionsFactory.Create` muss eine bereits
  validierte CacheRoot verwenden oder dieselbe gemeinsame Prüfung aufrufen.
  Keine zweite, schwächere oder leicht abweichende Normalisierung einführen.
  Die `source`-Unterwurzel, Default-Root, Refresh-Interval-Weitergabe und
  Step-033-Fabrik-/Policy-Verträge nicht verändern.
- Bestehende `RefreshIntervalMinutes`-, Duplicate-/Unknown-Field- und
  `MappingsPath`-Semantik unverändert lassen. Ein vorhandener ungültiger
  `CacheRoot` bleibt ein Fehler und darf nicht auf Default zurückfallen.

**Gezielte Tech-Debt-Entscheidung:** Falls bei dieser Extraktion eine kleine
gemeinsame Klassifikations-/Diagnosehilfe entsteht, soll sie als einzige
Architekturverbesserung in diesem Paket die doppelte Rohpfadlogik und
Magic-Strings beseitigen. Kein Sweep von `TD-001` bis `TD-003`, keine neue
Abstraktionsschicht und kein künstliches Dead-Code-Aufräumen.

### Schicht 2: Fail-closed vom Loader über Selection bis zum Tool-Resultat

**Produktionsbereich:**

- `AssemblySourceSelectionOrchestrator`: Den Status „Configuration failed“
  explizit und immutable an der `AssemblySourceSelectionScope` modellieren
  oder eine gleichwertige, nicht mit No-Match/ProviderUnavailable verwechsel-
  bare Grenze schaffen. `ResolveAsync` gibt ihn bei
  `!configurationResult.Succeeded` mit den Loader-Diagnosen weiter und ruft
  keinen Provider und keine Registry-Acquisition auf.
- Die bestehenden Provider-/Capability-/No-Match-Pfade bleiben semantisch
  getrennt. Sie dürfen weiterhin eine leere Auswahl erzeugen, damit der
  statische Decompilation-Fallback unverändert funktioniert.
- `AssemblyAnalysisToolSupport`: Vor `CreateContextAsync` den expliziten
  Config-Failure abfangen und über die tatsächlich geltende
  `McpToolResults`-/`IsErrorPolicy`-Darstellung ein strukturiertes terminales
  Resultat liefern. Diagnosecode und sichere Meldung müssen erhalten bleiben;
  ein `AssemblyContext`, ein `OriginKind=decompiled`-Erfolg oder ein
  `BuildResult` darf aus diesem Pfad nicht entstehen. Die Entscheidung über
  `IsError` versus `Recoverable` folgt dem bestehenden Policy-Vertrag und wird
  im Test explizit dokumentiert; ein stiller Erfolg ist in keinem Fall zulässig.
- `AssemblyAnalysisContextFactory`, Decompilation-Session und statische
  Fallback-Logik nicht ändern, sofern der neue terminale Check am Tool-Rand
  ausreicht. Eine Änderung dort wäre nur mit konkretem Symbolnachweis und
  innerhalb dieses einen Config-Failure-Vertrags zulässig.

### Schicht 3: Lokale adversariale Tests und echte Loader-zu-Tool-Regression

**Testbereich:**

- Neue fokussierte Datei unter
  `src/AiNetLinter.FastTests/Configuration/` für eine adversariale
  `CacheRoot`-Matrix. Sie prüft Loader und direkten `ExternalSourceCacheOptions`
  -Konstruktor mindestens mit den Reviewformen, leeren/dot-Segmenten,
  ungültigen Segmentzeichen, Device-/reservierten Formen sowie gültigen
  relativen und absoluten Roots. Jede ungültige Form muss fail-closed,
  `Configuration == null` bzw. eine generische Argument-Exception am direkten
  Konstruktor ergeben; Diagnosecode und Text dürfen kein `secret` oder den
  rohen Pfad enthalten. `TestTempDirectory` wiederverwenden.
- Den bestehenden Loader-Test nicht über die 500-Zeilen-Grenze aufblasen;
  gemeinsame kleine Testdaten-Helfer nur dann verschieben, wenn dadurch
  tatsächlich Duplikation an beiden neuen Nahtstellen sinkt.
- Neue fokussierte Datei unter
  `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/` für den realen
  End-to-End-Pfad: eine lokale Settings-Datei mit gültigem Mapping und einem
  adversarialen `CacheRoot` wird durch den echten Loader in
  `AssemblySourceSelectionOrchestrator.CreateFromSettings` geladen und in
  `AssemblyAnalysisToolSupport.ExecuteAsync` gegeben. Der Provider muss
  unaufgerufen bleiben; Registry/Scope müssen sauber beendet werden; das
  Toolresultat muss terminal und strukturiert sein, ohne Secret und ohne
  erfolgreiche statische Decompilation-Payload.
- Im bestehenden `AssemblyAnalysisToolSupportTests` den synthetischen
  Config-Failure-Vertrag so korrigieren, dass er keine Decompilation mehr
  erwartet. Die angrenzenden No-Match-, Ambiguous-, ProviderUnavailable- und
  Capability-Fallback-Assertions unverändert als positive Regressionen
  erhalten; bei Bedarf den kombinierten Test nur in klar benannte Fälle
  trennen, ohne die Datei über die Grenzwerte zu treiben.
- Keine neuen Stress-Tests, keine realen Netzwerk-/Gitea-/Git-Zugriffe, keine
  echten Credentials, kein `Assembly.Load`/`AssemblyLoadContext` und keine
  globale Reparse-/Win32-Sperre. Die beiden bekannten Win32-1314-Reparse-
  Skips bleiben hosttransparent und repository-spezifisch.

## Scope

- Strikte rohe `ExternalSources:CacheRoot`-Validierung einschließlich direkter
  Options- und Cache-Fabrik-Grenze.
- Explizite terminale Weitergabe eines fehlgeschlagenen Config-Loads bis zum
  Assembly-Tool-Resultat.
- Lokale Pfad-/Secret-Regressionsmatrix und reale Loader-zu-Tool-Regression.
- Erhalt der gültigen Step-033-RefreshInterval-/Factory-/Policy-
  Verdrahtung, des korrigierten Step-032-Nachweises und der bestehenden
  Decompilation-Fallbacks für nicht-konfigurative Provider-/Auswahlfehler.
- Nur direkt berührte Dry-/MagicValues-/DeadCode-Korrekturen an dieser
  Ownership-/Security-/Options-/Tool-Vertragsgrenze.

## Out of Scope

- Host-/MCP-Wiring in `AssemblyAnalysisHostComposition`,
  `McpServerCommand`, `DaemonHostCommand`, `McpServerOptionsFactory` oder
  Registrierungen.
- Health-/degraded-/Dirty-/unbuilt-Semantik, Retention/GC, Invalidierung,
  Telemetrie, Provider-Matrix, transitive Referenzen und EPIC-05.
- Änderungen an Fetch/Refresh/Publish/Rollback, Generation/Current-Pointer,
  Ownership, Reparse-Policy oder der statischen Decompilation selbst.
- Globale Änderungen an `McpToolResults`, sofern die bestehende Policy den
  terminalen Config-Failure bereits strukturiert ausdrücken kann; falls nicht,
  höchstens die kleinste direkt nötige Resultatverfeinerung innerhalb dieses
  Vertrags.
- Globale Dry-/MagicValues-/DeadCode- oder Tech-Debt-Sweeps und Änderungen an
  `TD-001` bis `TD-003` ohne direkten Bezug zu dieser Grenze.
- Step-032-Evidenz-Neubewertung, breiter Safeguard-Repair und Stress-Ausführung.
  Der bestehende Safeguard-FAIL `5,80/10` bei Threshold `8,00` bleibt ehrlich
  dokumentiert.
- Produktionsänderungen, Testläufe sowie Coder-/Kritikerarbeit während dieses
  Planer-Schritts.

## Architekturgrenze

Die Grenze liegt zwischen dem validierten Settings-Modell und dem Assembly-
Tool-Aufruf. Die Configuration-Schicht klassifiziert den rohen CacheRoot-Wert
und liefert entweder immutable gültige Options oder strukturierte, sichere
Diagnosen. Die Selection-Schicht bewahrt diesen Zustand als terminalen
Konfigurationsstatus und hält ihn von gewöhnlichen leeren/providerbedingten
Scopes getrennt. Die Tool-Schicht beendet nur diesen Status vor der
Context-Erzeugung als strukturiertes MCP-Resultat. Cache-Generation,
Decompilation-Factory, Host-Wiring und Provider bleiben außerhalb der neuen
Entscheidungslogik.

## Invarianten

1. Kein roher `CacheRoot` mit URI-Schema, Authority/Userinfo, Query/Fragment,
   Nicht-Drive-Doppelpunkt, Device-/reserviertem Root oder `.`/`..`-Segment
   erreicht eine gültige Cache-Options-/Writer-Konstruktion.
2. Loader, direkter `ExternalSourceCacheOptions`-Konstruktor und
   `ExternalSourceRepositoryCacheOptionsFactory` verwenden dieselbe
   Sicherheitssemantik; die gültige relative/absolute Auflösung und
   `<CacheRoot>/source` bleiben deterministisch.
3. Ein expliziter Config-Fehler bleibt `Succeeded == false` und
   `Configuration == null`; Diagnosecode/-struktur bleiben erhalten, rohe
   Werte und Secrets werden nie ausgegeben und kein Default kaschiert den
   Fehler.
4. Ein terminaler Config-Failure ist an der Selection-Scope von No-Match,
   ProviderUnavailable und Capability-Failure unterscheidbar; für ihn gibt es
   keinen Provider-Aufruf und keine Registry-Lease.
5. Das Assembly-Tool erzeugt aus einem terminalen Config-Failure keinen
   erfolgreichen Context, keine statische Decompilation-Payload und keinen
   `BuildResult`; es verwendet die geprüfte bestehende MCP-Resultat-Policy.
6. No-Match, Ambiguous, ProviderUnavailable und Capability-Failure behalten
   den bisher getesteten erfolgreichen statischen Decompilation-Fallback.
7. Step-033-RefreshInterval-/Factory-/Policy-Verdrahtung, Step-032-Evidenz,
   Reparse-/Ownership-Grenze und die bekannten 1314-Skips bleiben erhalten.
8. Alle neuen Tests sind lokal, isoliert und nicht stresslastig; kein Netzwerk,
   Credential, fremdes Assembly-Laden oder globaler Host-/MCP-Umbau kommt hinzu.

## Abnahmekriterien (maximal 8)

1. Die adversariale `CacheRoot`-Matrix weist die vier MAJOR-Formen sowie
   Query/Fragment-, Nicht-Drive-Doppelpunkt-, Device-/reservierte und
   Dot-Segment-Varianten vor der Kanonisierung ab; gültige relative und
   absolute Roots bleiben gültig.
2. Loader, direkter Optionskonstruktor und Cache-Options-Fabrik haben keinen
   schwächeren Umgehungspfad. Ungültige Werte liefern fail-closed generische
   Fehler ohne Rohpfad/Secret; `<CacheRoot>/source`, Default und gültige
   Refresh-Policy-Verdrahtung bleiben unverändert.
3. Ein real geladener `CacheRootInvalid`-Fehler ergibt
   `Configuration == null`, `Succeeded == false`, strukturierte Diagnose und
   keinen stillen Default-Fallback; `MappingsPath`-/Refresh-/Unknown-/Duplicate-
   Verträge bleiben regressionsfrei.
4. Der Config-Failure wird als eigener terminaler Selection-Status bis
   `AssemblyAnalysisToolSupport` weitergegeben; Provider, Registry und
   `AssemblyAnalysisContextFactory` werden in diesem Pfad nicht erfolgreich
   erreicht, und das Toolresultat ist gemäß der aktuellen
   `McpToolResults`-/`IsErrorPolicy` nicht erfolgreich sowie secret-frei.
5. Die bestehenden No-Match-, Ambiguous-, ProviderUnavailable- und
   Capability-Fallback-Tests liefern weiterhin statische Decompilation;
   insbesondere wird kein gewöhnlicher Fallback versehentlich fail-closed.
6. Neue Loader- und Tool-Regressionen verwenden `TestTempDirectory`, bleiben
   ohne Netzwerk/echtes Credential/Assembly-Laden, beseitigen keine globale
   Reparse-Fähigkeit und halten die betroffenen Testdateien unter den
   projektspezifischen Dateigrenzen.
7. Die gültige Step-033-Refresh-/Factory-/Policy-Verdrahtung, die korrigierte
   Step-032-Evidenz und der Baseline-Safeguard-FAIL `5,80/10` werden nicht
   zurückgedreht oder unbelegt als verbessert dargestellt; die zwei bekannten
   Win32-1314-Reparse-Skips bleiben transparent dokumentiert.
8. `dotnet build`,
   `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` und
   `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`
   sind grün; `Category=Stress` wird nicht ausgeführt. Die finale
   Step-Result-Notiz enthält exakten Commit, Test-/Skip-Zahlen und die
   fokussierten MCP-/DRY-/MagicValues-/DeadCode-Nachweise.

## Teststrategie

Der frische Coder führt zuerst nur die fokussierte neue Loader-/Tool-Regression
und die angrenzenden bestehenden Fallback-Tests aus. Danach folgen Build und
die beiden vorgeschriebenen Nicht-Stress-Abschlussgates. Der Planer führt in
diesem Schritt keine Tests aus.

- Fokussiert: neue adversariale `CacheRoot`-/Options-Tests, reale
  Loader-zu-Tool-Regression und die bestehende
  `AssemblyAnalysisToolSupportTests`-Fallbackgruppe.
- Build: `dotnet build` mit `TreatWarningsAsErrors`.
- Abschluss:
  `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`
  und
  `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`.
- Nicht ausführen: `dotnet test ... --filter Category=Stress`.
- Testdoubles, Settings-Dateien und erzeugte DLLs bleiben unter der zentralen
  Test-Temp-Isolation; keine lokalen Secrets in erwarteten Texten oder Logs.

## MCP-, DRY-, Magic-Values- und Dead-Code-Disposition

Semantische C#-Prüfungen verwenden den absoluten
`projectRoot: C:/Daten/Entwicklung/Ralf/AiNetLinter` und beginnen mit
`get_feature_context`, `find_symbol`, `get_symbol_body`,
`find_references`/`get_impact` und `safeguard` für die tatsächlich geänderten
Symbole. Text- und Dokumentationsprüfungen bleiben bei `rg`.

Der Coder dokumentiert nur scoped Findings der geänderten
Configuration-/Selection-/Tool-Symbole. Ein kleiner gemeinsamer Validator oder
ein Statusmarker soll vorhandene Rohpfad-/Statusduplikation reduzieren; neue
Dry-, MagicValues- oder DeadCode-Befunde in genau diesem Scope werden im
Paket behoben. Die bekannten breiten Safeguard-Werte und `TD-001` bis `TD-003`
werden nicht als Grund für einen globalen Sweep verwendet. Ein neuer
Tech-Debt-Eintrag entsteht nur, wenn die Korrektur einen direkt verbleibenden
Vertragsschuldposten nachweist.

## Definition of Done für den Folge-Coder

- [ ] Vorabprüfung und MCP-Symbol-/Resultat-Policy-Prüfung abgeschlossen;
      keine Zeilenannahme ungeprüft übernommen.
- [ ] Gemeinsame strikte Roh-`CacheRoot`-Semantik in Loader, Optionsrand und
      Cache-Fabrik wirksam; gültige Step-033-Verdrahtung erhalten.
- [ ] Terminaler Config-Failure bis zum Toolresultat umgesetzt; gewöhnliche
      statische Fallbacks erhalten.
- [ ] Lokale adversariale Matrix und reale Loader-zu-Tool-Regression ergänzt;
      bestehende Fallback-Regression korrigiert und Testdateigrenzen geprüft.
- [ ] `Docs/configuration.md` ist zur tatsächlich implementierten Grammatik
      wahr; nur bei notwendiger Abweichung geändert.
- [ ] Build und beide Nicht-Stress-Gates ausgeführt und mit exakten Zahlen,
      bekannten Skips und Stress-Nichtausführung dokumentiert.
- [ ] Scoped MCP-/DRY-/MagicValues-/DeadCode-Nachweise sowie der unveränderte
      Safeguard-FAIL ehrlich im Step-Result festgehalten.
- [ ] Coder-Commit und anschließend frischer Kritiker gemäß Orchestrator-
      Ablauf; kein bestehender Agent wiederverwendet.

## Regelreferenzen

- `.agents/rules/AiNetLinter-McpWorkflow.mdc` — projektgebundene MCP-Nutzung,
  absoluter `projectRoot`, Symbol-/Impact- und Safeguard-Prüfungen.
- `.agents/rules/AiNetLinter.mdc` — C#-Grenzwerte, strukturierte Diagnosen,
  fail-closed Fehlerbehandlung, keine stillen Fallbacks an Sicherheitsgrenzen.
- `.agents/rules/AiNetLinterRichtlinien.mdc` — vertikale Dev-Loops, lokale
  Testisolation, Dokumentationswahrheit, DRY/MagicValues/DeadCode-Disposition,
  Nicht-Stress-Abschlussgates und deutscher Commit-Vertrag.
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/orchestrator.md` — frische
  Coder-/Kritiker-Agenten, Step-Result-/Review-Übergabe und keine
  Mini-/Audit-only-Split-Gates.
