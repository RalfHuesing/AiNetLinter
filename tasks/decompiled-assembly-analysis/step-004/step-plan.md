---
status: done (pending audit)
type: step-plan
task: decompiled-assembly-analysis
step: 004
corrects: step-003
title: "Assembly-Session-Fundament korrigieren: Cache, Limits, Referenzen und Identität"
epic: EPIC-02
estimated_risk: high
step_type: single
items: []
created_by: planer
created_by_model: gpt-5 (Codex)
created_by_model_knowledge_cutoff: nicht angegeben
created_at: 2026-08-28T13:56:21+02:00
related_to: [step-003, step-003/step-review.md]
---

# Step 004: Assembly-Session-Fundament korrigieren: Cache, Limits, Referenzen und Identität

## Bezug

- **Task:** `decompiled-assembly-analysis`
- **Epic:** `EPIC-02` aus `roadmap.md` — Korrektur des statischen
  Assembly-Session-Fundaments aus `step-003`.
- **Korrigierter Step:** `step-003`; der Scope ist ausschließlich durch die
  sechs Findings aus `step-003/step-review.md` bestimmt.

## Aktueller Projektzustand (JIT-Kontext)

`AssemblyAnalysisSession` besitzt bereits einen optionalen `cacheRoot`, den die
Fast-Tests über `TestTempDirectory` verwenden. Der produktive Context-Pfad
erzeugt die Session jedoch ohne Override, weshalb
`AssemblyDecompilationCache` standardmäßig unter
`AppContext.BaseDirectory/cache/assembly` schreibt. Die beiden betroffenen
Integration-Gates lesen `.cs`-Dateien noch über freie rekursive
`Directory.EnumerateFiles`-Scans; der vorhandene zentrale
`FileSystemExclusionHelpers.IsGeneratedPath` wird dort nicht angewendet.

Das Manifest liegt derzeit als flaches Record mit 20 öffentlichen Properties in
`AssemblyAnalysisSessionModels.cs`. `AssemblyDecompilationCache` prüft beim
Lesen nur Schlüssel-/Statusfelder, verschiebt bei der Veröffentlichung ein
bestehendes Zielverzeichnis und veröffentlicht vor dem Aufbau des Roslyn-
Workspace. `AssemblyAnalysisSession` adoptiert deshalb auch inhaltlich leere
oder unvollständig geprüfte Partial-Manifeste und stellt den Cache vor der
Workspace-Prüfung sichtbar.

`AssemblyDecompilationAdapter` zählt nur Methoden und Felder von Top-Level-
Typen. Bei einer durch die Member-Auswahl geleerten Typmenge ruft er
`DecompileWholeModuleAsString` auf; verschachtelte Typen und weitere Member
können damit die angegebenen Budgets umgehen. `AssemblyReferenceResolver` liest
Referenznamen per `PEReader`, akzeptiert aber den ersten gleichnamigen
Dateikandidaten ohne Versions-/Kulturvergleich, markiert ihn vor erfolgreicher
`MetadataReference`-Erzeugung als aufgelöst und verwirft eine lokale
Enumerierungs-Ausnahme ohne sichtbare Diagnose.

Die echte PE-Identität ist im Resolver und im Manifest vorhanden, wird aber
beim Aufbau von `AssemblyContext` durch
`generation.Snapshot.Compilation.Assembly.Identity` aus der synthetischen
Roslyn-Compilation ersetzt. Die vorhandene Session-, Cache-, Workspace- und
TestKit-Infrastruktur wird erweitert; `AssemblyAnalysisService`-Traversierung,
Tool-Dispatch, Registry-/Daemon-Lifecycle und transitive Sessions bleiben
unverändert.

## Intention

Nach diesem Step wird nur eine vollständig validierte, immutable Cache-
Generation über einen atomaren Current-Pointer sichtbar und als Session-
Snapshot installiert. Decompiler-Budgets gelten für vollständige
Typbäume ohne Whole-Module-Bypass, Referenzen sind anhand statischer
Assembly-Identität nachvollziehbar, und `inspect_assembly`/Extensions verwenden
die echte PE-Identität statt der synthetischen Roslyn-Identität. Die
Integration-Gates ignorieren generierte Cachequellen deterministisch und laufen
aus einem sauberen Build-/Cache-Zustand grün.

## Konkrete Änderungen

### Finding 1 — Generierte Assembly-Cachequellen aus freien Integration-Scans ausschließen

#### `src/AiNetLinter.IntegrationTests/Architecture/McpProcessArchitectureGuardTests.cs:24-45`

- **Was:** Den rekursiven Quellscan vor dem Lesen der Dateien über den
  bestehenden zentralen `FileSystemExclusionHelpers.IsGeneratedPath` filtern.
  Dadurch werden `bin`-/`obj`-Dateien und die darin erzeugten
  `cache/assembly/.../source/*.cs` nicht als Architektur-Testquellen gezählt.
- **Warum:** Die beiden zusätzlichen `Process.Start(`-Treffer entstehen nicht
  aus Testquellcode, sondern aus dekompilierten Cacheartefakten unter
  `AppContext.BaseDirectory`.

#### `src/AiNetLinter.IntegrationTests/Platform/LoadedFixtureTests.cs:90-108`

- **Was:** Den Scan für `SourceFileCatalog.LoadAsync(` mit demselben zentralen
  Generated-/`bin`-Filter ausführen, bevor Pfade in die erwartete Caller-Liste
  gelangen.
- **Warum:** Das Gate muss ausschließlich versionierte bzw. nicht generierte
  Integrationstestquellen prüfen und darf vom Laufzeitcache nicht abhängig sein.

Der produktive Default-Cachepfad bleibt unverändert; die vorhandene
`cacheRoot`-Injektion für isolierte Session-Tests bleibt bestehen. Es wird kein
globaler Test-Environment-Hook und keine dauerhafte Ausnahme für die beiden
Gates eingeführt. Die bestehende
`src/AiNetLinter.IntegrationTests/Baseline/FileSystemExclusionHelpersTests.cs`
wird nur ergänzt, falls für die gewählte Filterreihenfolge eine Regression
fehlt.

### Findings 2 und 3 — Manifest kapseln und Cache-Generationen atomar sowie vollständig validieren

#### `src/AiNetLinter/Mcp/Assemblies/AssemblyAnalysisSessionModels.cs:105-198`

- **Was:** `AssemblyDecompilationManifest` in wenige interne, fachlich
  gruppierte Records kapseln, z. B. Eingangs-/Fingerprintdaten,
  Referenzdaten, Decompiler-/Ausgabeformat, Diagnosen und
  Veröffentlichungsstatus. Die Gruppen und das Manifest selbst dürfen jeweils
  höchstens 15 öffentliche Member besitzen; bevorzugt werden interne
  immutable Properties und keine neue öffentliche API.
- **Was:** Die bisherigen flachen JSON-Felder unverändert als Wire-Format
  erhalten: `cacheKey`, `canonicalPath`, `originalPath`, `length`, `mtimeUtc`,
  `sha256`, `assemblyIdentity`, `references`, `decompilerVersion`,
  `optionsIdentity`, `cacheSchemaVersion`, `generatedFiles`, `encoding`,
  `warnings`, `errors`, `unresolvedReferences`, `createdUtc`, `lastAccessUtc`,
  `status` und `complete`.
- **Warum:** Die Linter-Grenze wird eingehalten, ohne bestehende Cacheartefakte
  oder deren prüfbare Felder durch ein ungewolltes verschachteltes JSON-Schema
  zu verlieren.

#### Neue Datei `src/AiNetLinter/Mcp/Assemblies/AssemblyDecompilationManifestJsonConverter.cs`

- **Was:** Einen fokussierten `JsonConverter<AssemblyDecompilationManifest>`
  einführen, der die gekapselten internen Gruppen auf die obigen flachen
  camelCase-Felder schreibt und beim Lesen alle Felder typisiert zurückführt.
  Fehlende, doppelte oder falsch typisierte Pflichtwerte werden als ungültiges
  Manifest diagnostiziert; keine stillschweigende Default-Erzeugung für
  Identität, Fingerprint, Status oder Dokumentliste.
- **Warum:** System.Text.Json soll nicht durch öffentliche DTO-Properties
  wieder die MaxPublicMembers-Verletzung oder ein geändertes JSON-Layout
  erzwingen.

#### `src/AiNetLinter/Mcp/Assemblies/AssemblyDecompilationCache.cs:13-229`

- **Was:** Den bisherigen Wechsel `targetDirectory -> retiredDirectory ->
  targetDirectory` durch immutable Generation-Verzeichnisse ersetzen. Unter
  dem Schlüsselverzeichnis wird jede Kandidatengeneration in einem eigenen
  eindeutigen Verzeichnis geschrieben; der Schreibvorgang erzeugt alle
  `source/*.cs`-Dateien und das Manifest vollständig, schließt die Dateien
  sicher und validiert anschließend genau diese staged Generation.
- **Was:** Einen kleinen `current.json`-Pointer im Schlüsselverzeichnis
  verwenden, der ausschließlich auf eine fertige Generation im selben
  Dateisystem zeigt. Der Pointer wird über eine gleichlautende temporäre Datei
  und auf Windows über `File.Replace` ersetzt, wenn er schon existiert; beim
  ersten Publish wird nach einem Existenz-Recheck atomar verschoben. Bekannte
  Replace-/Sharing-Rennen werden begrenzt wiederholt und nach jedem Konflikt
  erneut gelesen; es gibt keinen unbeschränkten Retry und kein vorheriges
  Löschen des aktuell sichtbaren Eintrags.
- **Was:** `TryRead` ausschließlich über den Current-Pointer führen. Eine
  unreferenzierte, abgebrochene oder unvollständige Generation darf niemals
  adoptiert werden; alte Generationen bleiben bis zu einer späteren
  Bereinigung unberührt.
- **Was:** Das Cache-Schema für die neue Pointer-/Manifestrepräsentation
  versionieren, damit alte Verzeichnislayouts nicht als neue valide
  Generationen interpretiert werden.
- **Warum:** Ein Prozessabbruch oder konkurrierender Writer darf weder ein
  sichtbares Loch noch einen fälschlich erfolgreichen Publish erzeugen.

Die Manifest-/Dokumentvalidierung muss vor dem Lesen als aktuelle Generation
mindestens prüfen:

- Pointer und Generation liegen unter dem erwarteten Schlüsselroot; kein
  absoluter Pfad, `..`-Segment oder Pfad außerhalb des Roots.
- `GeneratedFiles` ist nicht leer, eindeutig und enthält ausschließlich
  nichtleere `.cs`-Dateien unter `source/`; jede Datei existiert, ist lesbar und
  wird mit strikt validiertem UTF-8 eingelesen.
- Anzahl und Reihenfolge der eingelesenen Dokumente entsprechen der Manifest-
  liste; `cacheKey`, kanonischer Pfad, SHA-256 und Dateigröße stimmen mit dem
  angefragten Fingerprint überein. `mtimeUtc` bleibt Metadatum und darf die
  mtime-only-Wiederverwendung nicht verhindern.
- Status, `complete`, Fehler, Warnungen und `unresolvedReferences` sind
  konsistent. `failed` sowie ein `complete`-Manifest mit Fehlern,
  ungelösten Referenzen oder leerer Dokumentliste sind nicht lesbar.
- Assembly-Identität und Referenzliste einschließlich Auflösungszustand und
  verwendetem Pfad stimmen mit der aktuellen statischen Referenzauflösung
  überein.

#### `src/AiNetLinter/Mcp/Assemblies/AssemblyAnalysisSession.cs:148-300`

- **Was:** Den Refresh-Ablauf in Staging, Workspace-/Compilation-Validierung,
  atomaren Pointer-Publish und Session-Installierung trennen. Ein Fresh-Path
  schreibt zunächst nur eine nicht sichtbare immutable Generation, baut daraus
  den Roslyn-Snapshot auf und prüft, dass alle erwarteten Dokumente im Projekt
  vorhanden sind und eine Compilation erzeugt wird. Erst danach wird der
  Current-Pointer ersetzt und erst nach erfolgreichem Publish die Generation
  unter `current` installiert.
- **Was:** Bei Cache-Hits dieselbe Workspace-Prüfung vor der Session-Adoption
  durchführen. Ein beschädigter/inhaltlich leerer Partial-Cache wird mit einer
  `assembly-cache-invalid`-Diagnose verworfen und führt zum Fresh-Path oder,
  falls dieser ebenfalls scheitert, zu `failed`/`degraded` ohne neuen Snapshot.
  Ein last-good Snapshot bleibt bei jedem Publish-/Workspace-Fehler erhalten.
- **Was:** Workspace-Diagnosen in den bestehenden strukturierten
  `AssemblySessionDiagnostic`-Vertrag überführen und bei einer Compilation mit
  fachlich nicht erklärbaren Fehlern den Status sichtbar auf `partial` bzw.
  `failed` absenken; eine solche Generation darf nicht als `complete`
  veröffentlicht werden.
- **Warum:** Der Cache darf nie erfolgreicher aussehen als der Roslyn-Snapshot,
  den die Session tatsächlich analysieren kann.

### Finding 4 — Typ-, Member- und Komplexitätsbudgets ohne Whole-Module-Bypass erzwingen

#### `src/AiNetLinter/Mcp/Assemblies/AssemblyDecompilationAdapter.cs:15-215`

- **Was:** Die Metadata-Auswahl auf vollständige Top-Level-Decompilationseinheiten
  umstellen. Für jede Einheit werden der komplette verschachtelte Typbaum,
  alle darin enthaltenen Typdefinitionen und alle relevanten
  Methoden-/Feld-/Property-/Event-Member deterministisch gezählt. Die bisherige
  Top-Level-Zählung sowie die Auswahl, die bei einem zu großen Typ eine leere
  Liste erzeugt, werden entfernt.
- **Was:** In Metadatenreihenfolge nur Einheiten auswählen, deren aggregierte
  `TypeCount`, `MemberCount` und `ComplexityCost` die verbleibenden
  `MaxTypes`, `MaxMembers` und `MaxComplexity` nicht überschreiten. Eine
  einzelne zu große Einheit wird übersprungen und diagnostiziert; bereits
  ausgewählte Einheiten bleiben begrenzt. Die Decompiler-Anfrage erhält nur
  die ausgewählten Root-Handles und kann keine nicht budgetierten Top-Level-
  oder verschachtelten Typen erreichen.
- **Was:** `DecompileWholeModuleAsString` und
  `AddModuleDocumentIfRequired` für diesen Pfad entfernen. Wenn wegen eines
  Limits keine Einheit dekompiliert werden darf, wird nicht auf das ganze Modul
  ausgewichen: bei noch vorhandenen Dokumenten ist das Ergebnis `partial`, bei
  null Dokumenten `failed`, jeweils mit einer sichtbaren Diagnose wie
  `assembly-type-limit`, `assembly-member-limit` oder
  `assembly-complexity-limit`.
- **Was:** Die bestehende Dokumentgrößenprüfung nach einer einzelnen,
  bereits budgetierten Einheit beibehalten und ihre Diagnose in derselben
  Partial-/Failed-Semantik weiterreichen.
- **Warum:** Limits müssen die tatsächliche Decompilationseinheit abdecken;
  eine Whole-Module-Ausgabe oder ein ungezählter Nested-Type-Baum darf sie
  nicht aushebeln.

#### `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisSessionTests.cs`

- **Was:** Component-Tests für einen Top-Level-Typ mit verschachteltem Typ,
  Properties/Events und Methoden ergänzen. Ein zu kleines Typ-, Member- oder
  Komplexitätsbudget muss ohne Whole-Module-Ausgabe sichtbar abbrechen; eine
  teilweise passende Auswahl muss `partial` mit Limitdiagnose liefern.
- **Was:** Den bestehenden Nachweis für leere/fehlgeschlagene Generationen um
  die Bedingung erweitern, dass bei Limitabbruch kein Manifest als aktueller
  Cacheeintrag und kein neuer Roslyn-Snapshot entsteht.

### Finding 5 — Referenzen statisch und identitätstreu auflösen

#### `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisModels.cs:52-62`

- **Was:** `AssemblyReferenceDto` um ein optionales, serialisierbares
  `ResolvedPath` ergänzen. Die bisherigen Felder `Name`, `Version`, `Culture`
  und `Resolved` bleiben unverändert; der neue Pfad wird nur nach erfolgreicher
  Einbindung gesetzt und bleibt in Manifest, Sessionzustand und
  `inspect_assembly`-Payload sichtbar.
- **Warum:** Ein Aufrufer kann damit nachvollziehen, welche Datei tatsächlich
  als MetadataReference verwendet wurde, ohne einen zweiten Referenzvertrag
  einzuführen.

#### `src/AiNetLinter/Mcp/Assemblies/AssemblyReferenceResolver.cs:16-182`

- **Was:** Kandidaten aus lokalem Assembly-Verzeichnis und TPA-Liste nicht
  länger nur nach Dateinamen auswählen. Jeden Kandidaten ausschließlich über
  `PEReader` prüfen und mindestens Name, Version und Kultur gegen die
  angeforderte Referenzidentität vergleichen; Kulturwerte wie `neutral`
  deterministisch normalisieren und gleichnamige falsche Kandidaten verwerfen.
- **Was:** Eine lokale Enumerierungs-Ausnahme mit dem bestehenden
  `AssemblySessionDiagnostic`-Mechanismus als eigener Code, Pfad und
  Fehlermeldung sichtbar machen; der TPA-Fallback darf danach weiter versucht
  werden. Das `_ = ex`-Muster entfällt.
- **Was:** `Resolved = true` und `ResolvedPath` erst setzen, nachdem
  `MetadataReference.CreateFromFile` für genau den identitätsgeprüften
  Kandidaten erfolgreich war. Bei einem Fehler bleiben Referenz und Pfad
  ungelöst, der MetadataReference-Eintrag wird nicht verwendet, und die
  strukturierte Diagnose enthält Kandidat, erwartete Identität und Fehler.
- **Was:** Die deterministische Reihenfolge der verwendeten Pfade erhalten und
  diese Referenzzustände beim Cache-Read mit dem Manifest vergleichen. Der
  gesamte Resolver bleibt metadata-only; kein `Assembly.Load`, keine
  `AssemblyLoadContext`-Instanz und keine Reflection-Ausführung.

#### `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/InspectAssemblyTool.cs:88-104`

- **Was:** Den bereits im Payload enthaltenen `ResolvedPath` auch in der
  Textdarstellung der Referenzzeilen ausgeben, wenn ein Pfad vorhanden ist;
  ungelöste Referenzen bleiben mit Status und Diagnose sichtbar.
- **Warum:** Die Referenzentscheidung muss sowohl maschinenlesbar als auch im
  normalen MCP-Text nachvollziehbar sein.

#### `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolTests.cs`

- **Was:** Tests für eine fehlende Dependency sowie für gleichnamige
  Kandidaten mit falscher Version/Kultur ergänzen. Prüfen, dass der falsche
  Kandidat `Resolved == false` bleibt, nicht als MetadataReference eingeht,
  die Diagnose sichtbar ist und ein korrekt eingebundener Kandidat seinen
  absoluten `ResolvedPath` im Payload und Text trägt.

### Finding 6 — Echte PE-Assembly-Identität bis zum Inspect-Payload transportieren

#### `src/AiNetLinter/Mcp/Assemblies/AssemblyAnalysisSessionModels.cs:125-151`

- **Was:** `AssemblySessionGeneration` um die von
  `AssemblyReferenceResolution.Identity` gelieferte `AssemblyIdentityDto`
  erweitern. Fresh- und Cache-Pfade setzen dieses Feld ausschließlich aus der
  statischen PE-Metadatenauflösung; die Manifest-Identität wird vor Verwendung
  gegen dieselbe Quelle validiert.
- **Warum:** Die Generation muss die fachliche Assembly-Identität unabhängig
  von der synthetischen Roslyn-Compilation tragen.

#### `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextFactory.cs:22-45`

- **Was:** `AssemblyContext.Identity` aus `generation.Identity` befüllen und
  `ToIdentityDto(Microsoft.CodeAnalysis.AssemblyIdentity)` für den
  Assembly-Payload nicht mehr verwenden. `AssemblyContext.Assembly` und seine
  synthetische Compilation bleiben als Roslyn-Quellgraph erhalten; nur deren
  Identität wird nicht mehr als Ziel-Assembly-Identität ausgegeben.
- **Was:** `inspect_assembly` und `find_assembly_extensions` weiterhin auf den
  bestehenden Context-/Payload-Vertrag setzen, sodass keine neue Tool- oder
  Registry-Struktur entsteht.

#### `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolTests.cs`

- **Was:** Eine Test-DLL mit expliziter `AssemblyVersion` und neutraler Kultur
  erzeugen und in `InspectAssembly` sowie im Session-Generation-State prüfen,
  dass die PE-Version unverändert erscheint und nicht durch die Default-
  Identität der synthetischen Roslyn-Compilation ersetzt wird. Der Text muss
  dieselbe Version nennen.

## Tests

- [ ] Manifest-Roundtrip: alle bisherigen flachen JSON-Felder bleiben nach
  Kapselung vorhanden; `AssemblyDecompilationManifest` und die Gruppentypen
  bleiben unter `MaxPublicMembersPerType`.
- [ ] Cache-Validierung: fehlende/duplizierte/leere/nicht unter `source/`
  liegende Dokumente, unsichere Pfade, inkompatibler Fingerprint,
  inkonsistenter Status und falsche Referenzdaten werden nicht adoptiert.
- [ ] Cache-Publish: staged Generationen sind vor Pointer-Update nicht lesbar;
  Pointer-Replace mit bestehendem Eintrag sowie zwei begrenzte konkurrierende
  Writer hinterlassen immer einen lesbaren vollständigen Current-Snapshot.
- [ ] Session-Reihenfolge: Workspace-/Compilation-Fehler veröffentlichen keine
  neue Generation; ein gültiger last-good Snapshot bleibt bei Publish-/Refresh-
  Fehlern erhalten.
- [ ] `AssemblyAnalysisSessionTests`: Nested-Type-, Property-/Event-, Typ-,
  Member- und Komplexitätsbudgets erzeugen keine Whole-Module-Ausgabe und
  liefern sichtbare `partial`-/`failed`-Diagnosen.
- [ ] `AssemblyAnalysisToolTests`: korrekte/falsche Referenzidentitäten,
  `ResolvedPath`, sichtbare Resolverdiagnosen und unveränderte PE-Identität im
  Inspect-Payload.
- [ ] `McpProcessArchitectureGuardTests` und `LoadedFixtureTests`: freie
  Quellscans ignorieren generierte `bin`-/Cacheartefakte; die erwarteten
  Callsite-Zahlen bleiben stabil.
- [ ] `dotnet clean` vor der Abschlussverifikation, damit der Gate-Lauf nicht
  von einem alten `bin`-Cache ausgeht; danach nur die vorhandene zentrale
  Generated-/`bin`-Filterung für Laufzeitcachequellen verwenden.
- [ ] `dotnet build` — null Warnungen und null Fehler bei
  `TreatWarningsAsErrors`.
- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` — alle
  Unit-/Component-Tests grün.
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`
  — vollständiger sauberer Integration-/Dogfood-/Performance-Gate-Lauf grün;
  Stress bleibt ausgeschlossen.

## Definition of Done

- [ ] Findings 1–6 aus `step-003/step-review.md` sind vollständig umgesetzt;
  kein zusätzlicher Tech-Debt- oder Konzeptpunkt wurde in diesen Step
  aufgenommen.
- [ ] Jeder aktuelle Cache-Pointer zeigt nur auf eine vollständig validierte
  Generation; beschädigte, leere oder abgebrochene Generationen werden nicht
  als aktueller Snapshot adoptiert.
- [ ] Das Manifest behält sein flaches JSON-Feldset und verletzt die
  öffentliche Membergrenze nicht.
- [ ] Typ-, Member- und Komplexitätsgrenzen budgetieren verschachtelte Typen
  und alle gezählten Member; Whole-Module-Fallback ist entfernt und jede
  Begrenzung bleibt als `partial`/`failed`-Diagnose sichtbar.
- [ ] Referenzen werden nur nach statischer Identitätsprüfung und erfolgreicher
  `MetadataReference`-Erzeugung als aufgelöst markiert; verwendete Pfade und
  Resolverfehler sind sichtbar.
- [ ] `AssemblySessionGeneration` und `AssemblyContext` transportieren die
  echte PE-Identität unverändert; die Roslyn-Compilation-Identität bleibt auf
  den synthetischen Quellgraphen beschränkt.
- [ ] `dotnet build` grün.
- [ ] Beide vollständigen Nicht-Stress-Testcommands grün.
- [ ] Der ausführende Coder erstellt den vorgesehenen lokalen deutschen
  Conventional-Commit; dieser Planungs-Agent nimmt keinen Commit vor.
- [ ] `step-004/step-result.md` geschrieben.
- [ ] `status` in `step-004/step-plan.md` nach Ausführung auf
  `done (pending audit)` gesetzt.

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc#Kurz-Stil` — sealed/immutable C#,
  kurze Methoden, kein stilles Fehlerverhalten.
- `.agents/rules/AiNetLinter.mdc#Grenzwerte` — insbesondere
  `MaxPublicMembersPerType`, `MaxMethodLineCount`,
  `MaxCyclomaticComplexity` und `MaxCognitiveComplexity` für die gekapselten
  Manifest-/Cache-/Resolver-Helfer.
- `.agents/rules/AiNetLinter.mdc#agent-resilience` — `EnforceNoSilentCatch`
  verlangt sichtbare Diagnose oder erneutes Werfen bei lokalen
  Enumerierungsfehlern.
- `.agents/rules/AiNetLinterRichtlinien.mdc#2 Architektur-Verbote` — statische
  PE-/Roslyn-Analyse ohne `Assembly.Load`, `AssemblyLoadContext`, Reflection
  oder unnötigen DI-Container.
- `.agents/rules/AiNetLinterRichtlinien.mdc#3 Windows-Umgebung & Tool-Regeln`
  — Windows-kompatible Dateiatomizität und zentrale Generated-/`bin`-Filter.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4 Updates & Tests` — xUnit-v3-
  Abdeckung, `TestTempDirectory`, Parallelitätsregeln und vollständige
  Nicht-Stress-Gates.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5 Qualitätsdrift-Prävention` —
  Zero-Warning-Gate, explizite Fehlerzustände, keine abgeschwächten
  Assertions und Wiederverwendung der bestehenden Ausschluss-/Testhelfer.
- `.agents/rules/AiNetLinter-McpWorkflow.mdc#Verbindliche Priorität` und
  `#Werkzeugwahl` — C#-Semantik, Referenzen und Impact MCP-first prüfen;
  Assembly-Tools bleiben metadata-only.

## Bekannte Ausnahmen

- Unreferenzierte alte Generation-Verzeichnisse werden in diesem Step nicht
  automatisch bereinigt; sie sind durch den Current-Pointer unsichtbar und
  gehören nicht zum Cache-Publish-Fix.

## Code-Skizze

```text
PEReader/Fingerprint
        -> identitätsgeprüfte Referenzen + absolute ResolvedPath-Werte
        -> begrenzte Root-Typbäume (inkl. Nested-Typen und Memberbudgets)
        -> immutable staged Generation mit vollständig validiertem Flat-Manifest
        -> Roslyn-Workspace-/Compilation-Prüfung
        -> atomarer current.json-Pointer
        -> AssemblySessionGeneration(PE-Identity, Roslyn-Snapshot)
        -> bestehende Inspect-/Extensions-Tools
```

## Notes

Der Step bleibt absichtlich ein einzelnes High-Risk-Korrekturpaket. Die sechs
Findings werden gemeinsam bearbeitet, weil Cache-Manifest, Workspace-Publish,
Limitdiagnosen, Referenzzustand und Assembly-Identität denselben
Generation-/Context-Vertrag bilden. `roadmap.md` wird im Fix-Modus nicht
geändert; `codemap.md` wurde nur um den bereits verwendeten zentralen
`FileSystemExclusionHelpers`-Bereich ergänzt. Produktionscode, Tests und
Task-Dokumentation werden erst von einem nachgelagerten ausführenden Agenten
geändert; dieser Planer hat keinen Commit erstellt.
