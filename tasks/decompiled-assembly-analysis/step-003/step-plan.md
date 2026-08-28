---
status: done (pending audit)
type: step-plan
task: decompiled-assembly-analysis
step: 003
corrects: null
title: "Statische Assembly-Session mit Fingerprint, Decompilation und Roslyn-Snapshot"
epic: EPIC-02
estimated_risk: high
step_type: single
items: []
created_by: planer
created_by_model: gpt-5 (Codex)
created_by_model_knowledge_cutoff: nicht angegeben
created_at: 2026-08-28T12:55:00+02:00
related_to: [step-002]
---

# Step 003: Statische Assembly-Session mit Fingerprint, Decompilation und Roslyn-Snapshot

## Bezug

- **Task:** `decompiled-assembly-analysis`
- **Epic:** `EPIC-02` aus `roadmap.md` — das noch offene Fundament für eine
  statische Decompilation unbekannter DLLs mit reproduzierbaren Snapshots.
- **Konzept-Referenz:** `Konzept.md` §§784–879 (Fingerprint, Cache-Key und
  Manifest), §§881–967 (Decompilation, synthetisches Roslyn-Projekt und
  Referenzen), §§1025–1135 (Generationen und Symbol-Origin) sowie
  §§1137–1161 und §§1389–1419 (Sicherheits-, Zustands- und
  Abnahmekriterien).

## Aktueller Projektzustand (JIT-Kontext)

`step-001` und die genehmigte Korrektur `step-002` haben den gemeinsamen
`targetType`/`targetPath`-Vertrag und die Dispatch-Grenze abgeschlossen; die
Roadmap markiert `EPIC-01` deshalb mit beiden Step-Referenzen als erledigt.

Der aktuelle Assembly-Pfad ist dagegen noch ein kurzlebiger Metadata-Pfad:
`AssemblyAnalysisContextFactory` (`src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextFactory.cs`)
liest PE-Metadaten, lokale DLLs und Trusted-Platform-Assemblies und baut pro
Aufruf eine Compilation. `AssemblyAnalysisToolSupport` erhält im direkten
Assembly-Dispatch keinen Projektzustand, und
`AssemblyAnalysisToolRegistrations` übergibt für `inspect_assembly` und
`find_assembly_extensions` weiterhin `null`; es gibt noch keinen residenten
Assembly-Registry oder eine Decompilation in Quelltext-Dokumente.

Die bestehende `AssemblyAnalysisService`-Traversierung, die beiden
Assembly-Tools und die vorhandenen Filter-/Trunkierungsmodelle sind die
Wiederverwendungsgrenze. Die Referenzauflösung aus der Context-Factory wird in
eine eigenständige, tool-unabhängige Resolver-Komponente extrahiert, statt eine
zweite ähnliche PE-/TPA-Implementierung einzuführen. `ProjectRegistry` und
`McpCodeGraphServer` bleiben projektgebunden; dieser Step baut noch keinen
zweiten Registry-Lifecycle neben ihnen auf.

Im Projekt ist `ICSharpCode.Decompiler` noch nicht referenziert. Die lokal
vorhandene Version `10.0.1.8346` stellt `CSharpDecompiler` bereit und wird als
explizit gepinnte Adapter-Abhängigkeit verwendet. `TestTempDirectory`,
`RoslynTestSolutionFactory` und die vorhandenen Assembly-Analyse-Component-
Tests liefern bereits die passenden Testmuster; Test-DLLs werden weiterhin
statisch aus Roslyn-Quelltext erzeugt.

## Intention

Nach diesem Step kann eine einzelne, von ihrem Aufrufer gehaltene
`AssemblyAnalysisSession` eine unbekannte DLL ohne Projektdefinition über eine
deterministische Fingerprint-/Cache-Generation statisch dekompilieren und als
readonly Adhoc-Roslyn-Projekt bereitstellen. Die bestehenden Assembly-Tools
bleiben über ihre Context-Grenze kompatibel, können den neuen Snapshot zunächst
auch kurzlebig nutzen; der Prozess-Registry, MCP-Wiring und die Wiederverwendung
zwischen getrennten Tool-Aufrufen werden in einem anschließenden EPIC-02-Step
angeschlossen.

## Konkrete Änderungen

### Paketgrenze und unveränderliche Session-Modelle — `Directory.Packages.props`, `src/AiNetLinter/AiNetLinter.csproj` und neue Dateien unter `src/AiNetLinter/Mcp/Assemblies/`

- **Was:** `ICSharpCode.Decompiler` in Version `10.0.1.8346` zentral pinnen
  und nur im produktiven AiNetLinter-Projekt referenzieren. Neue
  `internal`/`sealed` Records und Value-Objekte für Fingerprint, Cache-Key,
  Decompilation-Optionen, Session-Generation, Status/Diagnosen sowie
  `AssemblyOrigin` mit `originKind`, canonical path, content hash, generated
  document path und confidence anlegen.
- **Warum:** Die Session braucht eine stabile Identität über mtime-/Cache-
  Änderungen hinweg und muss Herkunft und Vertrauenszustand bis zur späteren
  Tool-Capability-Matrix transportieren können. ILSpy-Typen dürfen nicht in
  Tool- oder allgemeine Roslyn-Verträge auslaufen.

### Fingerprint- und Cache-Generationen — neue `AssemblyFingerprint.cs` und `AssemblyDecompilationCache.cs`

- **Was:** Den absoluten DLL-Pfad kanonisieren, mtime/Größe als schnellen
  Vorcheck und SHA-256 der Bytes als belastbare Identität berechnen. Der
  Decompilation-Key muss mindestens canonical path, content hash,
  Decompiler-Version, Optionen und Cache-Schema enthalten; mtime-Änderung bei
  identischen Bytes darf keine neue Decompilation auslösen. Unter
  `AppContext.BaseDirectory/cache/assembly` einen vom bestehenden
  `AnalysisCacheManager` getrennten Cache mit Manifest anlegen.
- **Was:** Das Manifest muss Pfad, Größe, mtime, Hash, Assembly-Identity und
  Referenzliste, Decompiler-/Options-/Schema-Version, generierte Dateien und
  Encoding, Warnungen/Fehler, unresolved references, Zeitstempel und
  `complete`/`partial`/`degraded`/`failed` enthalten. Unvollständige oder
  inkonsistente Einträge werden verworfen; neue Generationen werden über
  temporäres Verzeichnis plus atomaren Publish sichtbar gemacht. Automatische
  Disk-Bereinigung ist ausdrücklich nicht Teil dieses Steps.
- **Warum:** Cache-Treffer, Partial-Diagnosen und Refresh müssen prüfbar und
  crash-sicher sein, ohne den bestehenden Projekt-Cache oder fremde DLLs zu
  laden.

### Statischer Adapter und Referenzauflösung — neue `AssemblyDecompilationAdapter.cs` und `AssemblyReferenceResolver.cs`

- **Was:** Einen kleinen Adapter um `CSharpDecompiler` implementieren, der nur
  Pfad/Fingerprint/Optionen entgegennimmt und dekompilierten Quelltext,
  generierte Dokument-Einheiten und strukturierte Warnungen/Fehler zurückgibt.
  Er darf weder `Assembly.Load`, `AssemblyLoadContext`, Reflection-Ausführung
  noch sonstige Codeausführung verwenden.
- **Was:** Decompilation in begrenzte Einheiten aufteilen und vor/nach jeder
  Einheit Cancellation-/Deadline-Prüfungen sowie harte Größen-, Typ-/Member-
  und Komplexitätsgrenzen anwenden. Eine abgebrochene oder fehlgeschlagene
  Generation wird nicht publiziert; die diagnostizierte Generation bleibt
  sichtbar als `partial`, `degraded` oder `failed`.
- **Was:** Die aktuelle PEReader-/lokale DLL-/TPA-Referenzlogik aus
  `AssemblyAnalysisContextFactory.cs` nach `AssemblyReferenceResolver.cs`
  extrahieren. Der Resolver kennt nur Ziel-DLL, Zielverzeichnis, TPA bzw.
  passende Framework-References und optional sichtbare lokale Dependencies;
  fehlende Referenzen werden gesammelt statt verschluckt. Rekursive/transitive
  Referenz-Sessions bleiben EPIC-05.
- **Warum:** Decompiler- und Cache-Details bleiben hinter einer testbaren
  Grenze; Roslyn erhält reproduzierbare Metadaten-Referenzen und sichtbare
  Grenzen, ohne eine zweite Resolver-Implementierung zu erzeugen.

### Readonly-Roslyn-Snapshot — neue `AssemblyRoslynWorkspaceFactory.cs` und `AssemblyAnalysisSession.cs`

- **Was:** Aus den generierten Quellen ein synthetisches `AdhocWorkspace`-
  Projekt mit `ProjectInfo`, moderner C#-Parse-/Compilation-Option und den
  aufgelösten MetadataReferences erzeugen. Pro Typ bzw. sinnvoller
  Decompiled-Einheit ein `Document` anlegen, Dokumente und Solution nur
  lesbar veröffentlichen und den `AssemblyOrigin` je Dokument/Generation
  erhalten.
- **Was:** `AssemblyAnalysisSession` als zustandsbehaftete, aber
  thread-sicher lesbare Hülle ausführen: Initialisierung/Refresh erzeugen
  genau eine neue Generation, der aktuelle Snapshot wird erst nach vollständigem
  Cache-/Workspace-Aufbau atomar ersetzt, und bestehende Snapshot-Leases
  bleiben auf ihrer alten Generation lesbar. Loading-, Partial-, Degraded- und
  Failed-Zustände samt `last good`-Generation und Diagnosen müssen explizit
  auslesbar sein.
- **Was:** Die Session-Fabrik und die bestehende
  `AssemblyAnalysisContextFactory.cs` so verbinden, dass die aktuelle
  `AssemblyAnalysisContext` aus dem synthetischen Snapshot und dem neuen
  `AssemblyReferenceResolver` gebildet werden kann. Bestehende
  `AssemblyAnalysisService`-Traversierung, Identitäts-/Referenz-DTOs,
  Typ-/Member-Filter und Trunkierungsgrenzen bleiben fachlich unverändert.
- **Warum:** Damit wird die spätere Registry- und MCP-Anbindung auf einen
  stabilen Roslyn-Snapshot aufgesetzt, ohne die beiden bereits ausgelieferten
  Assembly-Toolverträge gleichzeitig mit einer neuen Tool-Architektur zu
  vermischen.

### Test-Infrastruktur und Component-Abdeckung — `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/`

- **Was:** Unit-Tests für Fingerprint/Cache-Key, mtime-only versus Byte-Änderung,
  Manifest-Validierung, atomaren Publish und Partial-/Failed-Cachefälle
  ergänzen. Session-Tests prüfen Generationstausch, alte readonly Snapshots,
  Cancellation/Größenlimits, fehlende Referenzen und die sichtbaren Zustände.
- **Was:** `AssemblyAnalysisToolTests.cs` auf die Session-/Workspace-Fabrik
  umstellen bzw. erweitern: eine Roslyn-emittierte DLL mit Methode und
  Extension wird ohne Projektdefinition statisch dekompiliert, bleibt für
  `inspect_assembly` und `find_assembly_extensions` analysierbar, und die
  bestehenden Filter-/Applicability-/Partial-Assertions bleiben grün.
  Zusätzlich wird geprüft, dass die Ziel-DLL nach der Analyse nicht in den
  geladenen Prozess-Assemblies auftaucht und die Origin-Daten auf
  `decompiled`/generierte Dokumente zeigen.
- **Was:** Ausschließlich `TestTempDirectory` und die bestehenden
  `RoslynTestSolutionFactory`-/TestKit-Patterns verwenden; keine Sleeps,
  Netzwerkabhängigkeit oder unbounded parallelism einführen.

## Tests

- [ ] `AssemblyFingerprintTests` — kanonischer Pfad, SHA-256, mtime-only
  Wiederverwendung und Byte-Änderung als neue Generation.
- [ ] `AssemblyDecompilationCacheTests` — vollständiges/partielles Manifest,
  inkompatibler Key, temporärer Publish und fehlgeschlagene Generation ohne
  Sichtbarwerden.
- [ ] `AssemblyAnalysisSessionTests` — synthetisches Roslyn-Projekt,
  Symbolauflösung, Origin, fehlende Referenzen, Zustandswechsel und alte
  Generation nach Refresh.
- [ ] `AssemblyAnalysisToolTests` — bestehende Inspect-/Extension-Ausgaben
  auf dem statisch dekompilierten Snapshot sowie Nachweis ohne Runtime-Loading.
- [ ] `dotnet build` — Warnungen und Fehler müssen wegen
  `TreatWarningsAsErrors` bei null liegen.
- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` und
  `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` —
  vollständige Nicht-Stress-Abschlussverifikation gemäß `roadmap.md`.

## Definition of Done

- [ ] Alle unter „Konkrete Änderungen“ beschriebenen Session-, Fingerprint-,
  Cache-, Decompiler- und Workspace-Grenzen sind implementiert.
- [ ] Eine unbekannte, lokal vorhandene DLL kann ohne
  `ainetlinter.project.json` statisch in einen readonly Roslyn-Snapshot mit
  sichtbarer `decompiled`-Origin überführt werden.
- [ ] Cache-Key, Manifest, Partial-/Failed-Diagnosen und atomarer
  Generationstausch erfüllen die genannten mtime-/Hash-Regeln; eine
  unvollständige Generation wird nicht als aktuelle Generation publiziert.
- [ ] Kein Ziel-Assembly wird geladen, ausgeführt oder per Reflection
  instanziiert; Grenzen für Dateigröße, Komplexität und Zeit/Cancellation sind
  vor und zwischen Decompilationseinheiten wirksam.
- [ ] Die bestehenden `inspect_assembly`- und
  `find_assembly_extensions`-Verträge sowie ihre Filter-/Trunkierungssemantik
  bleiben kompatibel.
- [ ] Build-Command aus Tech-Stack-Notiz (`roadmap.md`) grün.
- [ ] Test-Commands aus Tech-Stack-Notiz grün.
- [ ] Commit auf aktuellem Branch mit deutschem Conventional Commit wird vom
  ausführenden Coder erstellt.
- [ ] `step-003/step-result.md` geschrieben.
- [ ] `status` in `step-003/step-plan.md` während der Ausführung von `in_progress`
  auf `done (pending audit)` gesetzt.

## Rules-Refs

- `.agents/rules/AiNetLinter-McpWorkflow.mdc#Verbindliche Priorität und Werkzeugwahl` — semantische Grenzen und absolute Zielpfade bleiben MCP-first; die Assembly-Tools dürfen keinen versteckten Consumer-Projektkontext erhalten.
- `.agents/rules/AiNetLinter-McpWorkflow.mdc#Assembly-Tools` — Assembly-Analyse bleibt metadata-/statisch orientiert, Diagnosen und Partialness müssen sichtbar sein.
- `.agents/rules/AiNetLinterRichtlinien.mdc#1 Grundprinzipien` — kleine, fokussierte Abstraktionen, deterministisches Verhalten und defensive Eingabegrenzen.
- `.agents/rules/AiNetLinterRichtlinien.mdc#2 Architektur-Verbote` — kein Assembly-Loading, keine `AssemblyLoadContext`-/Reflection-Ausführung und kein unnötiger DI-/Plugin-Unterbau.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4 Updates & Tests` — `TreatWarningsAsErrors`, xUnit v3, TestKit und vollständige Nicht-Stress-Verifikation.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5 Qualitätsdrift-Prävention` — vorhandene Resolver-, Workspace- und Test-Infrastruktur wiederverwenden; keine parallele Duplikatstruktur.
- `.agents/rules/AiNetLinter.mdc#Kurz-Stil`, `#Grenzwerte` und `#architecture` — nullable/sealed C#, kurze Methoden, begrenzte Dateigrößen und keine dynamische Ausführung.

## Bekannte Ausnahmen

- Der verwendete `CSharpDecompiler` stellt synchrone, begrenzte Decompilation-
  Operationen bereit. Der Step prüft Cancellation/Deadline zwischen diesen
  Einheiten und publiziert bei Überschreitung keinen neuen Snapshot; ein
  einzelner bereits laufender Bibliotheksaufruf wird nicht durch
  `Task.Run`/Thread-Abbruch erzwungen.
- Prozessweite Assembly-Registry, MCP-Daemon-Wiring, TTL/LRU-Eviction und die
  Wiederverwendung derselben Session über getrennte Tool-Aufrufe sind bewusst
  nicht Teil dieses Steps, sondern der nächste JIT-Cluster in `EPIC-02`.
- Gitea-/Source-Snapshot-Mapping, rekursive externe Referenzen und die
  vollständige Herkunfts-Capability-Matrix bleiben in `EPIC-03` bis `EPIC-05`.

## Code-Skizze (optional)

```text
AssemblyAnalysisContextFactory
        -> AssemblyAnalysisSession.RefreshAsync
        -> AssemblyFingerprint + AssemblyDecompilationCache
        -> AssemblyDecompilationAdapter (CSharpDecompiler; kein Runtime-Load)
        -> AssemblyReferenceResolver
        -> AssemblyRoslynWorkspaceFactory (AdhocWorkspace/Project/Documents)
        -> atomarer AssemblySessionGeneration-Snapshot
        -> bestehende AssemblyAnalysisService / Inspect / Extensions
```

## Notes

Der Step ist absichtlich ein einzelnes High-Risk-Fundamentpaket und keine
Datei-für-Datei-Aufteilung. Er liefert eine prüfbare statische
Decompilationseinheit, ohne bereits das gesamte EPIC-02 durch Registry-,
Daemon-, transitive Referenz- und Capability-Arbeiten vorwegzunehmen. Der
folgende JIT-Plan soll erst nach dem realen Ergebnis entscheiden, wie die
`AssemblyRegistry`-Lebensdauer und die MCP-Registrierung an diese Session
angeschlossen werden.
