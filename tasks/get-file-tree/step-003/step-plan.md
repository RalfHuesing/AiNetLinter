---
status: blocked
type: step-plan
task: get-file-tree
step: 003
corrects: null
title: "Gemeinsame Walk-/Optionen-/Glob-Grundlage extrahieren"
epic: EPIC-02
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: GPT-5 (Codex)
created_by_model_knowledge_cutoff: nicht im Systemkontext angegeben
created_at: 2026-08-26T23:10:33+02:00
related_to: [step-002]
---

# Step 003: Gemeinsame Walk-/Optionen-/Glob-Grundlage extrahieren

## Bezug

- **Task:** `get-file-tree`
- **Epic:** `EPIC-02` aus `roadmap.md` — die vorhandene physische
  Traversierung erhält eine kompatible Optionen-Nahtstelle für Tiefe,
  Cancellation und Walk-Statistiken; die vorhandene Glob-Semantik bekommt einen
  neutralen, gemeinsam nutzbaren Kern.
- **Konzept-Referenz:** `Konzept.md`, „Wiederverwendung vorhandener
  Infrastruktur“, „Minimal sinnvolle Generalisierung des Walk-Kerns“ und
  „Glob-Wiederverwendung“.

## Aktueller Projektzustand (JIT-Kontext)

- EPIC-01 ist durch Step 001 und Step 002 erledigt; beide Reviews sind
  `approved`, und der zuletzt dokumentierte vollständige Build-/Fast-/
  Integration-Gate-Lauf ist grün. Der Root-Resolver und der
  filesystem-only-Dispatch aus Step 001 bleiben unverändert.
- `src/AiNetLinter/Baseline/FileSystemExclusionHelpers.cs` enthält aktuell den
  zentralen `WalkFilteredTree` ohne Optionen, Tiefenmodell oder
  CancellationToken. Der Walk dedupliziert Wurzeln und besuchte Verzeichnisse,
  überspringt `SearchExcludedDirectories`, betritt keine Reparse Points und
  sammelt pro zugriffsgestörtem Teilbaum Warnungen. Die bestehende Vier-
  Parameter-Signatur wird von `McpCodeGraphServer`,
  `McpCodeGraphServerRefresh` und `StalenessTreeWalkerTests` verwendet.
- `TreeWalkStats` besteht derzeit aus `Warnings` und dem daraus abgeleiteten
  `InaccessibleSubtreeCount`. Für den späteren Scanner fehlen noch explizite
  Zustände für Cancellation sowie gezählte Standard-/Reparse-Skips; diese
  Ergänzung gehört als Walk-Grundlage in diesen Step, nicht in das spätere
  Scanresult.
- `SafeEnumerateFiles` und `SafeEnumerateFilesWithErrors` haben bewusst einen
  anderen Vertrag: ihre rekursive `EnumerationOptions`-Enumeration liefert
  auch generierte bzw. standard-ausgeschlossene Dateien. Sie werden von
  `SearchPatternLegacyFileHitScanner`, `SearchPatternScanner`,
  `GetIndexScopeScanner` und `WebFileCatalog` verwendet und dürfen deshalb
  weder auf den standard-ausschließenden Walk umgebogen noch semantisch
  verändert werden.
- `src/AiNetLinter/Configuration/FileFilterEvaluator.cs` enthält zwei
  Regex-Übersetzungen: die private Dateinamen-Globlogik mit `*`/`?` und die
  öffentliche `MatchesGlobForWeb`-Variante für normalisierte Pfade und `**`.
  Direkte Produktionsaufrufer sind `LinterEngine`, `WebFileCatalog` und
  `SearchPatternScanner`; die bestehenden Filter- und SearchPattern-Tests
  frieren ihre beobachtete Semantik ein.
- Der aufgerufene DRY-Audit über `find_duplicates(scopeDir="src",
  minTokens=20, similarityThreshold="near")` fand keinen relevanten
  Produktions-Exact-/Near-Cluster. Der strukturelle Scan lieferte nur
  fachfremde Kandidaten; es gibt daher keinen Tech-Debt-Eintrag und keinen
  Anlass für eine zusätzliche Exclusion- oder Glob-Implementierung.
- `codemap.md` enthält bereits alle betroffenen Bestandsbereiche
  (`FileSystemExclusionHelpers`, `TreeWalkStats`, `FileFilterEvaluator`, die
  Walk-/Filter-Aufrufer und ihre Testbereiche). Eine Ergänzung ist für den
  aktuellen Ist-Zustand nicht erforderlich; die beiden neuen Produktionsdateien
  werden erst durch die Umsetzung angelegt.

## Intention

Der Step schafft genau eine interne Walk-Optionen- und Statistikgrundlage, auf
der EPIC-03 später den physischen File-Tree-Scanner aufbauen kann. Der bisherige
Walk-Aufruf bleibt als delegierende Kompatibilitätsüberladung erhalten, während
die neue Variante Root-Tiefe, Cancellation, Standardausschlüsse,
Reparse-Point-Schutz und partielle Warnungen deterministisch ausweist.

Zusätzlich wird die Glob-Regex-Übersetzung in einen neutralen
`PathGlobMatcher` extrahiert; `FileFilterEvaluator` bleibt mit seinen
bestehenden Signaturen der kompatible Einstieg. Es entstehen in diesem Step
weder ein Scanresult noch ein `get_file_tree`-Tool, eine Registrierung oder
Produktdokumentation zum noch nicht implementierten Tool.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Baseline/FileSystemWalkOptions.cs` (neu)

- **Was:** Ein internes unveränderliches `FileSystemWalkOptions`-Record mit
  `MaxDepth`, `SkipExcludedDirectories` und `CancellationToken` anlegen.
  `Default(CancellationToken)` liefert die bisherige unbeschränkte Tiefe und
  aktivierte Standardausschlüsse; `ForFileTree(int?, CancellationToken)` bildet
  die spätere File-Tree-Nutzung mit `int.MaxValue` als fehlender Tiefengrenze
  ab. Negative Tiefen werden an der internen Factory als ungültige
  Programmkonfiguration abgewiesen.
- **Warum:** Die bestehende Walk-Signatur soll nicht mit weiteren optionalen
  Parametern wachsen. Das Options-Record hält die neue Semantik geschlossen und
  lässt den späteren Scanner dieselbe Traversierung verwenden.

### Datei 2: `src/AiNetLinter/Baseline/FileSystemExclusionHelpers.cs` (Zeilen 27-227)

- **Was:** Eine neue interne `WalkFilteredTree`-Überladung mit
  `FileSystemWalkOptions` ergänzen. Die bestehende Vier-Parameter-Überladung
  delegiert ausschließlich an `FileSystemWalkOptions.Default(CancellationToken.None)`;
  alle aktuellen Aufrufer behalten damit ihre Signatur und ihr Verhalten.
- **Was:** Den bestehenden Stack um die Root-Tiefe erweitern, wobei die Root
  Tiefe `0` hat. Verzeichnisse und Dateien bis einschließlich `MaxDepth` werden
  besucht; am Tiefenlimit werden keine Kindverzeichnisse mehr enumeriert.
  Cancellation wird vor weiteren Directory-/File-Besuchen und vor dem Einreihen
  neuer Kinder geprüft. Der Walk bricht ohne Ausnahme mit partiellen Ergebnissen
  ab und markiert die Cancellation in `TreeWalkStats`.
- **Was:** `SearchExcludedDirectories`, `IsExcludedDirectoryName`,
  `IsTraversableSubDirectory`, die Warnungsbehandlung von
  `TryEnumerateSubDirectories`/`VisitSafely`, die Pfadnormalisierung und die
  bestehende `visited`-/Root-Deduplizierung weiterverwenden. Bei aktivierten
  Standardausschlüssen und Reparse-Point-Schutz werden die jeweiligen Skip-
  Zähler für `TreeWalkStats` erhöht.
- **Was:** `SafeEnumerateFiles`, `SafeEnumerateFilesWithErrors`,
  `FileSystemEnumerationResult`, `SearchExcludedDirectories` selbst sowie die
  direkte Root-Deduplizierungslogik nicht auf den neuen Contract umstellen oder
  neu entwerfen. Insbesondere bleibt der bestehende
  `SafeEnumerateFiles_ExistingDir_ReturnsAllFilesIncludingGenerated`-Vertrag
  erhalten.
- **Warum:** Der neue Scanner braucht einen einzigen physischen Walk mit
  sichtbarer Vollständigkeitsgrundlage, während die vorhandenen Search-/Web-
  Aufrufer weiterhin ihre bewusst breitere Enumeration verwenden.

### Datei 3: `src/AiNetLinter/Baseline/TreeWalkStats.cs` (Zeilen 13-16)

- **Was:** Unveränderliche Statistikfelder für `CancellationRequested`,
  `SkippedExcludedDirectoryCount` und `SkippedReparsePointCount` ergänzen,
  ohne den bestehenden `Warnings`-Konstruktor und
  `InaccessibleSubtreeCount`-Vertrag zu entfernen. Die bisherige Warnungs-
  und Fehlerzählung bleibt unverändert.
- **Warum:** EPIC-03 kann damit die spätere `completeness`-Struktur aus dem
  Walkresult ableiten, ohne erneut eigene Ausschluss- oder Reparse-Zählung zu
  implementieren.

### Datei 4: `src/AiNetLinter/Configuration/PathGlobMatcher.cs` (neu)

- **Was:** Einen neutral benannten internen Matcher mit einer einzigen
  case-insensitiven, separator-normalisierenden Pfad-Glob-Übersetzung anlegen.
  `*` matcht nicht über `/`, `?` genau ein Nicht-Separator-Zeichen und `**`
  darf Pfadsegmente überqueren; `\\` und `/` werden vor dem Vergleich auf `/`
  vereinheitlicht. Die bestehende Behandlung leerer Eingaben/Muster bleibt
  `false`; die Regex-Erzeugung wird nicht in einem neuen Aufrufer wiederholt.
- **Warum:** `get_file_tree` und die vorhandenen Web-/SearchPattern-Filter
  sollen dieselbe Glob-Grundlage verwenden. Der Typ bleibt intern und führt
  noch kein File-Tree-Filtering ein.

### Datei 5: `src/AiNetLinter/Configuration/FileFilterEvaluator.cs` (Zeilen 14-73)

- **Was:** Die private Dateinamen-Globprüfung und die öffentliche
  `MatchesGlobForWeb(string, string)` auf `PathGlobMatcher` delegieren. Die
  öffentlichen Signaturen, die case-insensitive Filterung, Separator-
  Normalisierung und die bestehende `IsExcluded`-Reihenfolge bleiben erhalten.
- **Was:** `MatchesDirectoryPattern` unverändert lassen. Diese Prüfung ist eine
  konfigurierte Directory-Segment-/Pfad-Ausschlusssemantik und nicht die
  wiederzuverwendende Glob-Regex-Grundlage.
- **Warum:** Der bestehende `FileFilterEvaluator` bleibt der Vertrag für
  `LinterEngine`, `WebFileCatalog` und `SearchPatternScanner`, enthält aber
  keine zweite Regex-Implementierung neben dem neutralen Kern.

### Datei 6: `src/AiNetLinter.FastTests/Baseline/StalenessTreeWalkerTests.cs`

- **Was:** Component-Tests für die neue Options-Überladung ergänzen:
  Root-Tiefe `0`/verschachtelte Tiefe, Cancellation vor weiteren Callbacks,
  aktivierte Standardausschlüsse samt Skip-Zähler und die unveränderte
  Reparse-Point-Entscheidung. Der bestehende Nested-Root-Test bleibt als
  Kompatibilitätsnachweis bestehen.
- **Warum:** Die sicherheits- und performance-relevanten Walk-Entscheidungen
  sind mit `TestTempDirectory` und ohne echte Junction-Abhängigkeit
  deterministisch prüfbar.

### Datei 7: `src/AiNetLinter.FastTests/Configuration/PathGlobMatcherTests.cs` (neu)

- **Was:** Unit-Tests für `*`, `?`, `**`, Case-Insensitivity,
  Forward-/Backslash-Normalisierung, Segmentgrenze von `*`, verschachtelte und
  root-level `**/`-Muster sowie leere Eingaben ergänzen.
- **Warum:** Der extrahierte Kern ist eine gemeinsame Semantikquelle und muss
  unabhängig von Web- oder SearchPattern-Fixtures regressionsfest sein.

### Datei 8: `src/AiNetLinter.FastTests/Configuration/FileFilterEvaluatorTests.cs`

- **Was:** Die bestehenden Dateinamen-/Directory-Filtertests um explizite
  Wrapper-Regressionsfälle für `?`, `**` und Separator-Normalisierung ergänzen;
  die LinterEngine-Tests mit ausgeschlossenen Dateien unverändert beibehalten.
- **Warum:** Die Delegation darf weder die bestehende Konfigurationssemantik
  noch die Ausschlussreihenfolge des Linter-Kerns verändern.

### Datei 9: `src/AiNetLinter.IntegrationTests/Baseline/FileSystemExclusionHelpersTests.cs`

- **Was:** Einen physischen Integrationstest für die neue Walk-Überladung mit
  realem Temp-Dateibaum ergänzen: sichtbare Datei wird besucht, ein
  standard-ausgeschlossenes Verzeichnis nicht betreten und der Skip-Zähler
  entspricht dem tatsächlich übersprungenen Teilbaum. Die vorhandenen
  `SafeEnumerateFiles`-Tests bleiben unverändert.
- **Warum:** Die Component-Tests prüfen die Walk-Entscheidungen isoliert; dieser
  Test friert den kombinierten Dateisystemvertrag und die Legacy-Abgrenzung auf
  Integrationsebene ein.

### Bestehende direkte Aufrufer als Regressionen

- `src/AiNetLinter/Mcp/McpCodeGraphServer.cs` und
  `src/AiNetLinter/Mcp/McpCodeGraphServerRefresh.cs` bleiben unverändert; die
  Staleness- und Refresh-Integrationstests müssen weiterhin dieselben mtime-,
  Warnungs- und Projektverzeichnisgrenzen beobachten.
- `src/AiNetLinter/Mcp/Tools/Analysis/SearchPatternScanner.cs`,
  `SearchPatternLegacyFileHitScanner.cs`,
  `src/AiNetLinter/Mcp/Tools/FileStructure/GetIndexScopeScanner.cs` und
  `src/AiNetLinter/Web/WebFileCatalog.cs` bleiben unverändert. Ihre vorhandenen
  Filter-/Exclusion-Tests sichern, dass `SafeEnumerateFiles*` nicht versehentlich
  auf Standardausschluss-Semantik umgestellt wird.

## Tests

- [ ] `Walk_MaxDepth_VisitsFilesAtLimitButNotDeeperDirectories` prüft Root-Tiefe
      `0` und eine verschachtelte Tiefengrenze.
- [ ] `Walk_Cancellation_ReturnsPartialStatsBeforeFurtherCallbacks` prüft den
      nicht-werfenden Partial-Walk und `CancellationRequested`.
- [ ] `Walk_ExcludedDirectoryNames_AreNotTraversed` prüft zusätzlich den neuen
      `SkippedExcludedDirectoryCount`; der vorhandene Reparse-Point-Pure-Test
      bleibt grün.
- [ ] `PathGlobMatcherTests` deckt `*`, `?`, `**`, Separatoren, Case und
      Segmentgrenzen direkt ab.
- [ ] `FileFilterEvaluatorTests` und die bestehenden
      `SearchPatternScannerTests` sichern die Delegations- und
      SearchPattern-Filtersemantik.
- [ ] `FileSystemExclusionHelpersTests` deckt den realen physischen
      Options-Walk; `SafeEnumerateFiles_ExistingDir_ReturnsAllFilesIncludingGenerated`
      bleibt als Legacy-Vertrag unverändert grün.
- [ ] Bestehende Integrationsregressionen in
      `McpCodeGraphServerStalenessMtimeCacheTests`, `McpServerCommandStalenessTests`,
      `GetIndexScopeToolTests` und `SearchPatternToolTests` bleiben grün; es gibt
      noch keinen neuen MCP-Inventory-/Handshake-Test, weil kein Tool registriert
      wird.
- [ ] `dotnet build`
- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`
- [ ] Kein `Category=Stress`-Lauf ohne ausdrückliche Anforderung.

## Definition of Done

- [ ] `FileSystemWalkOptions` und die neue Walk-Überladung sind intern,
      unveränderlich und ohne Signaturänderung aller vorhandenen Aufrufer
      eingeführt.
- [ ] Der Options-Walk besucht mit Root-Tiefe `0` deterministisch bis zur
      konfigurierten Tiefe, prüft Cancellation an längeren Walk-Punkten und
      liefert partielle Stats statt einer scheinbar vollständigen Fortsetzung.
- [ ] Standardausschlüsse, Reparse-Point-Schutz, Warnungsbehandlung und
      bestehende Root-/Visited-Deduplizierung bleiben zentral in
      `FileSystemExclusionHelpers`; es entsteht keine zweite Exclusion-Liste.
- [ ] `SafeEnumerateFiles*` behält einschließlich der generierten Dateien und
      des bestehenden Cancellation-Verhaltens seinen Legacy-Vertrag.
- [ ] `TreeWalkStats` stellt Cancellation- und Skip-Metadaten für den späteren
      Scanner bereit, ohne den bestehenden Warnungs-/Inaccessible-Vertrag zu
      brechen.
- [ ] `PathGlobMatcher` ist die einzige neue Glob-Übersetzung; beide
      `FileFilterEvaluator`-Einstiege delegieren darauf und alle direkten
      Aufrufer bleiben unverändert.
- [ ] Alle genannten Unit-/Component-/Integration-Tests sowie Build und beide
      Nicht-Stress-Gates sind grün.
- [ ] Es gibt keine Änderung an Scanresult-/Renderer-/MCP-Registrierungslogik
      und keine Vorwegnahme von EPIC-03 oder EPIC-04.
- [ ] Der Coder schreibt `step-003/step-result.md`, setzt diesen Plan auf
      `done (pending audit)` und erstellt die vorgesehenen Code-/Doku-Commits
      nach dem grünen Gate-Lauf; der Kritiker übernimmt den grünen Nachweis und
      wiederholt den Vollauf nicht routinemäßig.

## Rules-Refs

- `.agents/rules/AiNetLinter-McpWorkflow.mdc#Verbindliche Priorität` und
  `#Standard` — semantische Symbol-/Aufrufer-/Testfragen zuerst über den
  AiNetLinter-MCP mit absolutem `projectRoot`; `rg`/Read nur ergänzend für
  konkrete Textarbeit.
- `.agents/rules/AiNetLinter.mdc#Kurz-Stil` und `#agent-resilience` — interne
  sealed/nullable Typen, kurze Methoden, unveränderliche Records und sichtbare
  Cancellation-/Fehlerpfade ohne stille `catch`-Blöcke.
- `.agents/rules/AiNetLinterRichtlinien.mdc#1 Grundprinzipien` und `#2
  Architektur-Verbote` — direkte, schlanke Wiederverwendung ohne parallele
  Infrastruktur, DI-Overhead oder repo-spezifische Hardcodings.
- `.agents/rules/AiNetLinterRichtlinien.mdc#3 Windows-Umgebung & Tool-Regeln` —
  Windows-/PowerShell-kompatible Pfade, zentrale Enumeration und Git-/Test-
  Konventionen.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4 Updates & Tests` — xUnit-v3,
  `TestTempDirectory`, erhaltene Testparallelität und die verbindlichen
  Nicht-Stress-Gates.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5 Qualitätsdrift-Prävention` —
  Zero-Warning, Result-/Partial-Pattern, keine Symptombehebung und aktive
  DRY-Konsolidierung.

## Code-Skizze

```csharp
internal static TreeWalkStats WalkFilteredTree(
    IEnumerable<string> roots,
    FileSystemWalkOptions options,
    Action<string>? visitDirectory,
    Action<string>? visitFile);

internal static TreeWalkStats WalkFilteredTree(
    IEnumerable<string> roots,
    string? filePattern,
    Action<string>? visitDirectory,
    Action<string>? visitFile)
    => WalkFilteredTree(
        roots,
        FileSystemWalkOptions.Default(CancellationToken.None),
        visitDirectory,
        visitFile);
```

## Notes

- `GetDistinctTopLevelRoots` und der bestehende `visited`-Mechanismus werden in
  diesem Step nicht neu bewertet oder umsortiert. Der Step extrahiert Optionen
  und Statistik, aber keine zusätzliche Root-Boundary- oder Dedupe-Variante.
- Der `PathGlobMatcher` ist eine neutrale Baseline-Grundlage. Der eigentliche
  File-Tree-Filter, die Extension-/Exclude-Inputvalidierung, Aggregation,
  Scanresult-/Completeness-Records, Renderer und MCP-Wiring folgen erst in
  EPIC-03/04.
- Produktdokumentation wird in diesem internen Vorbereitungs-Step nicht
  vorgezogen. Bei der späteren Produktimplementierung sind die in
  `prompt-reference.md` und den Projektregeln genannten MCP-Dokumente mit dem
  tatsächlich implementierten Verhalten zu synchronisieren.
