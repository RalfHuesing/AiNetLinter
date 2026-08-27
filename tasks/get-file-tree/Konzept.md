---
status: ready
type: konzept
project_kind: brownfield
estimated_scope: large
rules_dir: .agents/rules
last_updated: 2026-08-26
open_questions: []
---

# Konzept: MCP-Tool `get_file_tree` – physische Projektdatei-Landkarte

## Ziel (Was)

AiNetLinter erhält ein read-only MCP-Tool namens `get_file_tree`. Das Tool enumeriert
die physisch vorhandenen Dateien und Verzeichnisse unterhalb eines absoluten
`projectRoot`-Pfads, optional begrenzt auf einen relativen Unterpfad. Es liefert eine
kompakte, strukturierte Landkarte mit Extension-Verteilung, Verzeichnishierarchie,
Dateipfaden und ausgewählten Metadaten.

Das Tool ergänzt die vorhandene semantische C#-Exploration. `get_namespace_tree`,
`get_file_skeleton`, `metrics_tree` und die Symbolgraph-Tools beantworten Fragen über
geladene C#-Dokumente; `get_file_tree` beantwortet die vorgelagerte physische Frage:
„Welche Dateien und Verzeichnisse existieren überhaupt in diesem Projektbereich?“

## Kurzentscheidung

Der Name wird **`get_file_tree`**. Er beschreibt für ein agentisches LLM die erwartete
Antwortform besser als `list_files` (zu flach), `explore_files` (zu unbestimmt) oder
`get_project_structure` (kann mit semantischer Projektstruktur verwechselt werden).

Das Tool arbeitet auf dem Dateisystem des `projectRoot`, nicht nur auf der residenten
Roslyn-Solution. Dadurch werden insbesondere Markdown-, Konfigurations-, Script-,
Template- und sonstige Dateien sichtbar, die kein C#-Document der Solution sind.

Die Standardausgabe folgt Progressive Disclosure:

1. Zusammenfassung und Extension-Verteilung,
2. kompakter Verzeichnisbaum,
3. konkrete Dateitreffer nur auf Anforderung oder bei passendem Filter.

Ein separater Orientierungs-/Klassifikationsmodus wie `purpose: "orientation"` gehört
bewusst nicht zum Scope dieses Konzepts.

## Warum / Kontext

### Problem für agentische LLMs

Ein agentisches LLM besitzt keine automatische, vollständige Landkarte des lokalen
Dateisystems. Es sieht nur:

- Dateien, die der Host explizit liefert,
- Pfade, die durch andere Tools bereits bekannt sind,
- Suchtreffer, wenn ein passender Suchbegriff vorhanden ist,
- oder die Ausgabe eines vom Host erlaubten Shell-/Dateiwerkzeugs.

Das führt bei unbekannten Repositories zu wiederkehrenden Problemen:

- Tief verschachtelte `README.md`- oder `AGENTS.md`-Dateien bleiben unentdeckt.
- Dokumentation wird mit Code-Symbolsuche verwechselt oder gar nicht gefunden.
- Konfigurations- und Build-Dateien werden vermutet statt sicher lokalisiert.
- Eine Textsuche kann nicht zwischen „Datei existiert nicht“ und „Datei enthält den
  Suchbegriff nicht“ unterscheiden.
- Eine vollständige Shell-Ausgabe erzeugt bei größeren Repositories zu viel Kontext
  und hat keinen stabilen, MCP-spezifischen Fehler- und Trunkierungsvertrag.
- Die physische Struktur und die semantische C#-Struktur werden nicht gemeinsam
  betrachtet.

### Befund im aktuellen AiNetLinter-Repository

Die aktive AiNetLinter-MCP-Analyse am 2026-08-26 zeigt die konkrete Lücke:

- `get_index_scope` meldet für die Solution 664 `.cs`-Dateien; die dort ausgewiesenen
  nicht-C#-Kategorien sind nicht die vollständige Projektdatei-Landkarte.
- `get_namespace_tree` liefert Solution → Projekte → Namespaces → Typen, aber keine
  Verzeichnis- oder Dokumentationshierarchie.
- `metrics_tree` aggregiert Code-Metriken und ist auf den Roslyn-/C#-Scope ausgerichtet.
- `search_pattern` kann `.md`-Dateien im Projektroot durchsuchen und fand hier
  verschachtelte Dokumentationsdateien, liefert aber nur Dateien mit passenden
  Textzeilen. Dateigröße, leere Dateien, nicht passende Dateien und Verzeichnis-
  aggregation sind nicht sein Zweck.

Damit besteht bereits eine nützliche semantische C#-Landkarte und eine brauchbare
Textsuche. Was fehlt, ist der deterministische physische Zwischenlayer für das
Dateisystem.

### Nutzen

Das Tool ist insbesondere nützlich für:

- Erstkontakt mit einem unbekannten Repository.
- Auffinden tief verschachtelter Projektanweisungen und Dokumentation.
- Auswahl geeigneter Folgedateien vor einem Refactoring oder einer Konfigurations-
  änderung.
- Exploration von `.md`, `.json`, `.yml`, `.xml`, `.csproj`, `.slnx`, `.props`,
  `.targets`, Scripts, Templates und projektspezifischen Dateitypen.
- Erkennen ungewöhnlicher oder unerwarteter Dateitypen.
- Begrenzung des anschließenden Inhaltslesens auf relevante Dateien.
- Unterscheidung zwischen physisch vorhandenen Dateien und Dateien, die lediglich
  in der Roslyn-Solution geladen sind.
- Wiederholbare MCP-Aufrufe ohne Abhängigkeit von Shell-Syntax des jeweiligen Hosts.

## Scope

### Muss-Haben

- Neues read-only MCP-Tool `get_file_tree`.
- Pflichtparameter `projectRoot` als absoluter Pfad gemäß dem bestehenden MCP-
  Projektvertrag.
- Optionaler Parameter `root` als Pfad relativ zu `projectRoot`; Standard `.`.
- Physischer rekursiver Dateisystem-Walk, unabhängig davon, ob eine Datei in der
  Roslyn-Solution als C#-Document geladen ist.
- Filterung nach Dateiendungen, einschließlich `*` für alle Extensionen.
- Optionaler Pfad-/Dateifilter als Glob gegen den normalisierten relativen Pfad.
- Additive benutzerdefinierte Ausschlussmuster zusätzlich zu den zentralen
  Standardausschlüssen.
- Drei kontrollierte Ausgabeansichten: `summary`, `tree` und `files`.
- Kompakte Extension- und Verzeichnisaggregation.
- Relative, stabile, mit `/` normalisierte Dateipfade in der MCP-Antwort.
- Dateigröße in Bytes als Standardmetadatum bei Dateitreffern und Aggregaten.
- Deterministische Sortierung.
- Harte Antwortgrenzen mit expliziter Completeness-/Truncation-Metadaten.
- Schutz vor `..`-Traversal, absoluten Unterpfaden außerhalb des Roots und Reparse-
  Point-/Symlink-Traversierung.
- Wiederverwendung der vorhandenen Dateisystem-Ausschlusslogik statt einer neuen,
  konkurrierenden Ausschlussliste.
- Structured Content als JSON-Objekt und zusätzlich eine kompakte menschenlesbare
  Textdarstellung.
- Cancellation-Unterstützung sowie partielle Ergebnisse bei unzugänglichen
  Teilbäumen.
- Registrierung in der vorhandenen `FileStructureToolRegistrations`-Gruppe.
- Unit-, Integrations- und MCP-Vertragstests bei der späteren Implementierung.

### Bewusst nicht im ersten Implementierungsschritt

- Dokumentklassifikation oder Priorisierung nach vermuteter Bedeutung.
- `purpose: "orientation"` oder ein vergleichbarer semantischer Empfehlungsmodus.
- Automatisches Lesen oder Vorschauen von Dateiinhalten.
- Semantische C#-Analyse, Symbolauflösung oder Referenzgraphen.
- Git-Historie, Git-Status oder „zuletzt geändert in Commit X“.
- Inhaltssuche; dafür bleibt `search_pattern` zuständig.
- Pagination-Cursor über mehrere MCP-Aufrufe.
- Dauerhafter Dateisystem-Index oder Watcher-Cache.
- Aufhebung sicherheitsrelevanter Standardausschlüsse per frei formulierter Negation.
- Enumeration beliebiger absoluter Pfade außerhalb des registrierten `projectRoot`.
- Änderung von `rules.json` für rein operative Toolparameter.

### Non-Goals (bewusst NICHT Teil davon)

- **`purpose: "orientation"`:** bewusst nicht implementieren. Die dafür nötige
  Definition von Dokumenttypen, Prioritätsheuristiken, Vertrauensstufen und der
  Frage „was soll als Nächstes gelesen werden?“ ist ein eigenes Konzept.
- **`read_file`:** Inhaltsextraktion hat andere Größen-, Encoding- und Secret-Risiken
  und soll nicht in den Dateibaum-Contract hineingemischt werden.
- **Vollständige Roslyn-Projektstruktur:** bleibt Aufgabe von `get_namespace_tree`,
  `get_file_skeleton` und den Symbolgraph-Tools.
- **Build-/Artefaktanalyse:** `bin`, `obj`, Worktrees und ähnliche Verzeichnisse
  bleiben nach den bestehenden Scanregeln standardmäßig ausgeschlossen.
- **Beliebige Dateisystemfreigabe:** `projectRoot` ist Sicherheits- und Scope-Grenze;
  das Tool wird kein allgemeines Remote-Dateisystem-Browsing anbieten.

## Zielplattformen / Technischer Rahmen

### MCP- und Projektvertrag

Das Tool wird als projektgebundenes MCP-Tool mit `projectRoot` registriert. Der
Parameter bleibt absolut, wie bei den übrigen projektgebundenen Tools. Alle vom
Aufrufer kontrollierten Pfade (`root`, Filter) werden dagegen relativ zum
`projectRoot` interpretiert.

Das Tool benötigt für die eigentliche Enumeration keine Roslyn-Solution. Es darf
aber nicht zu einem beliebigen Root-Browser werden. Deshalb wird ein spezieller,
schlanker Dateisystem-Dispatch im bestehenden Projekt-Dispatch vorgesehen:

- absolute Root-Validierung und Registry-/Projektbindung bleiben zentral,
- die physische Enumeration erhält den kanonischen `projectRoot`,
- der Dispatch wartet nicht auf `ServerLoadState.Loading`, wenn die Enumeration
  bereits sicher möglich ist,
- ein Roslyn-Load-Fehler blockiert die Dateilandkarte nicht, sofern der Projektroot
  und die Projektdefinition selbst aufgelöst werden können,
- bestehende Roslyn-Tools behalten ihren bisherigen Load-State-Vertrag.

Vorgesehener interner Einstiegspunkt:

```csharp
internal static Task<CallToolResult> ExecuteFilesystemAsync(
    ProjectRegistry registry,
    string? projectRoot,
    Func<ProjectLease, Task<CallToolResult>> call)
```

Der konkrete Name ist ein Implementierungsdetail. Entscheidend ist die Trennung
zwischen „Projektroot ist adressierbar“ und „Roslyn-Solution ist geladen“. Eine
zweite, frei zugängliche Root-API außerhalb des Projektvertrags wird nicht angelegt.

### Frischer Walk statt residentem Datei-Cache

Die Antwort wird on demand aus dem aktuellen Dateisystem erzeugt. Das vermeidet
Stale-Results nach Edits und passt zum MCP-Verhalten der bestehenden Suchtools.
Ein dauerhafter Cache ist für den ersten Umfang nicht erforderlich. Falls spätere
Messungen einen Cache rechtfertigen, muss er einen sichtbaren Snapshot-/Stale-Vertrag
erhalten und darf nicht stillschweigend mit der Roslyn-Solution verwechselt werden.

### Windows- und .NET-Rahmen

Das Repository läuft auf Windows und verwendet .NET. Der Walk soll deshalb:

- `EnumerationOptions` bzw. eine darauf aufbauende zentrale Traversierung nutzen,
- `FileAttributes.ReparsePoint` nicht traversieren,
- case-insensitive Pfadvergleiche verwenden,
- intern Windows-Pfade akzeptieren,
- in MCP-Ausgaben stets `/` verwenden,
- `IOException` und `UnauthorizedAccessException` pro Teilbaum behandeln,
- `CancellationToken` an jedem längeren Walk-Punkt prüfen.

## Vorgeschlagener MCP-Vertrag

### Toolsignatur

Der folgende Vertrag ist der konkrete Vorschlag für `tools/list` und die Registrierung.
Die Namen sind bewusst semantisch und nicht an interne Klassennamen gebunden.

```text
get_file_tree(
    projectRoot: string,
    root?: string,
    view?: "summary" | "tree" | "files",
    includeExtensions?: string[],
    fileFilter?: string,
    excludePatterns?: string[],
    maxDepth?: integer,
    treeDepth?: integer,
    maxResults?: integer,
    sortBy?: "path" | "size_desc" | "extension",
    includeMetadata?: boolean,
    includeLineCount?: boolean
)
```

### Parametersemantik

| Parameter | Default | Semantik |
|---|---:|---|
| `projectRoot` | Pflicht | Absoluter, registrierter Projektroot. Nicht als relativer Pfad akzeptieren. |
| `root` | `.` | Relativer Unterpfad unter `projectRoot`; absolute Werte und Ausbruch via `..` ablehnen. |
| `view` | `tree` | `summary` nur Aggregation, `tree` Aggregation plus kompakter Baum, `files` flache Dateitreffer. |
| `includeExtensions` | alle | Extension-Filter, z. B. `[".md", ".json"]`; `*` bedeutet alle. |
| `fileFilter` | keiner | Ein Glob gegen den normalisierten `projectRoot`-relativen Dateipfad. |
| `excludePatterns` | leer | Additive Globs für Dateien/Pfade; zentrale Sicherheits-/Standardausschlüsse bleiben aktiv. |
| `maxDepth` | unbegrenzt logisch | Rekursionstiefe; serverseitig mit hartem Sicherheitslimit begrenzen. Root hat Tiefe 0. |
| `treeDepth` | 2 | Maximale Darstellungstiefe der Directory-Nodes in `view=tree`; begrenzt nicht den Scan. |
| `maxResults` | 200 | Maximal ausgegebene Dateieinträge; serverseitig z. B. auf 2.000 begrenzen. |
| `sortBy` | `path` | Stabile Sortierung; `path` ist die deterministische Standardausgabe. |
| `includeMetadata` | `true` | Liefert Größe und weitere günstige Dateimetadaten. |
| `includeLineCount` | `false` | Liest Textdateien für Zeilenanzahl; wegen zusätzlicher I/O bewusst opt-in. |

`includeExtensions` und `fileFilter` werden mit logischem AND kombiniert. Ein
Dateitreffer muss beide Bedingungen erfüllen. `excludePatterns` wird danach als
zusätzlicher Ausschluss angewendet.

Extensions werden normalisiert:

- `md` wird zu `.md`,
- `.MD` wird case-insensitive wie `.md` behandelt,
- `*` deaktiviert den Extension-Filter,
- `null` oder fehlendes Array bedeutet alle Extensions,
- die Extension ist die letzte Extension aus `Path.GetExtension`,
- extensionlose Dateien erhalten `extension: null`; ohne Extensionfilter werden sie
  mitgelistet, ein expliziter Extensionfilter schließt sie aus. Ein Glob allein kann
  extensionlose Dateien über ihren Dateinamen matchen.

Für die erste Implementierung wird empfohlen, extensionlose Dateien standardmäßig
mitzulisten, wenn kein Extension-Filter angegeben ist. Ein expliziter Filter wie
`[".md"]` soll sie dagegen nicht matchen. Ein eigener Schalter dafür ist zunächst
nicht erforderlich.

`fileFilter` ist ein Pfadfilter, kein Inhaltsfilter. Beispiele:

```text
**/README.md
Docs/**/*.md
src/**/*Test*.cs
*.slnx
```

Der Filter wird gegen den gesamten `projectRoot`-relativen Pfad geprüft, auch wenn
`root` auf einen Unterordner gesetzt ist. Dadurch bleiben Filter in wiederholbaren
Agenten-Loops eindeutig.

### Views

#### `summary`

Liefert:

- effektiven Root,
- Anzahl durchsuchter und gematchter Dateien,
- Anzahl besuchter Verzeichnisse,
- Gesamtgröße der gematchten Dateien,
- Extension-Verteilung,
- Top-Level- oder bis zur angefragten Tiefe aggregierte Verzeichnisse,
- Completeness-Metadaten.

Keine vollständige Dateiliste.

#### `tree`

Liefert die Summary und einen kompakten, nach Verzeichnissen gruppierten Baum. Im
Structured Content bleibt der Baum als flache Liste von Directory-Nodes und
File-Nodes mit `depth` modelliert; die Textdarstellung rendert daraus ASCII oder
Markdown. Das vermeidet tief verschachtelte, schwer budgetierbare JSON-Objekte.

Verzeichnisse ohne gematchte Dateien werden nicht ausgegeben. Der angefragte Root
wird dennoch immer als oberster Node ausgegeben.

`treeDepth` steuert, wie viele Directory-Nodes angezeigt werden. `maxDepth` begrenzt
den eigentlichen Scan und kann unabhängig davon tiefer laufen, damit Summary-Zahlen
korrekt bleiben. Bei einer Trunkierung wird das ausdrücklich in `completeness`
markiert.

#### `files`

Liefert die flache, sortierte Trefferliste. Diese Ansicht ist für Folgeaktionen
geeignet, bei denen das LLM konkrete Pfade an `search_pattern`, `get_file_skeleton`
oder ein späteres Lese-Tool weitergibt.

### Structured Content

Die kanonische Payload soll in ein Objekt gewrappt werden, nicht als Top-Level-Array
übertragen werden. Das folgt dem bereits bestehenden MCP-Vertrag von
`McpToolResults.Text<T>`.

```json
{
  "fileTree": {
    "root": ".",
    "effectiveRoot": "C:/repo",
    "view": "files",
    "summary": {
      "scannedFileCount": 147,
      "matchedFileCount": 18,
      "scannedDirectoryCount": 34,
      "matchedDirectoryCount": 9,
      "matchedBytes": 284912,
      "byExtension": [
        { "extension": ".md", "count": 18, "bytes": 284912 }
      ]
    },
    "directories": [
      {
        "path": "Docs",
        "depth": 1,
        "matchedFileCount": 12,
        "matchedBytes": 190432,
        "childDirectoryCount": 4
      }
    ],
    "files": [
      {
        "path": "Docs/integration.md",
        "extension": ".md",
        "sizeBytes": 23841,
        "lineCount": null,
        "depth": 2
      }
    ],
    "completeness": {
      "scanCompleted": true,
      "truncated": false,
      "truncatedBy": [],
      "shownFileCount": 18,
      "inaccessibleSubtreeCount": 0,
      "skippedExcludedDirectoryCount": 4,
      "skippedReparsePointCount": 0,
      "warnings": []
    }
  }
}
```

Die Feldnamen dienen als Vertragsvorschlag. Die bestehende JSON-Konfiguration soll
die CamelCase-Serialisierung übernehmen.

### Dateieintrag

Vorgeschlagene interne Struktur:

```csharp
internal sealed record FileTreeFileEntry(
    string Path,
    string? Extension,
    long? SizeBytes,
    int? LineCount,
    int Depth);
```

Bei `includeMetadata=false` wird `SizeBytes` pro Dateieintrag intern nicht gesetzt
und wegen der gemeinsamen MCP-JSON-Optionen im ausgegebenen `structuredContent`
ausgelassen. Die Summary- und Directory-Aggregate bleiben trotzdem erhalten, weil
sie für Sortierung und Agentenorientierung benötigt werden.

`LastWriteTime` wird nicht standardmäßig aufgenommen. Zeitstempel sind für eine
statische Projektlandkarte meist nicht semantisch relevant und machen Antworten
unnötig flüchtig. Ein späterer, gezielter Sortiermodus kann Zeitstempel intern
verwenden, ohne sie standardmäßig auszugeben.

### Directory- und Summary-Einträge

```csharp
internal sealed record FileTreeDirectoryEntry(
    string Path,
    int Depth,
    int MatchedFileCount,
    long MatchedBytes,
    int ChildDirectoryCount);

internal sealed record FileTreeExtensionEntry(
    string? Extension,
    int Count,
    long Bytes);

internal sealed record FileTreeSummary(
    int ScannedFileCount,
    int MatchedFileCount,
    int ScannedDirectoryCount,
    int MatchedDirectoryCount,
    long MatchedBytes,
    IReadOnlyList<FileTreeExtensionEntry> ByExtension);

internal sealed record FileTreeCompleteness(
    bool ScanCompleted,
    bool Truncated,
    IReadOnlyList<string> TruncatedBy,
    int ShownFileCount,
    int InaccessibleSubtreeCount,
    int SkippedExcludedDirectoryCount,
    int SkippedReparsePointCount,
    IReadOnlyList<string> Warnings);

internal sealed record FileTreePayload(
    string Root,
    string EffectiveRoot,
    string View,
    FileTreeSummary Summary,
    IReadOnlyList<FileTreeDirectoryEntry> Directories,
    IReadOnlyList<FileTreeFileEntry> Files,
    FileTreeCompleteness Completeness);
```

Die Records sind unveränderliche Ausgabemodelle. Interne Akkumulation darf mutable
sein, soll aber vor dem Renderer in diese stabilen Records überführt werden.

## Konkrete Code-Struktur

### Vorgeschlagene Dateien

```text
src/AiNetLinter/Mcp/Tools/FileStructure/
├── GetFileTreeTool.cs
├── GetFileTreeScanner.cs
├── GetFileTreeRenderer.cs
├── GetFileTreeRecords.cs
├── GetFileTreeInputValidator.cs
└── FileTreeFilter.cs             (nur falls die Filterlogik nicht in den Scanner passt)
```

Die vorhandene `FileStructureToolRegistrations.cs` bleibt der Registrierungsort.
Die bestehenden File-Structure-Tools werden nicht in einen allgemeinen Mega-Scanner
zusammengeführt. Das neue Tool erhält einen klar begrenzten Scanner, verwendet aber
die gemeinsamen Baseline-Helfer für Traversierung, Pfadnormalisierung und Globs.

### Dünner Tool-Dispatch

`GetFileTreeTool` soll keine Traversierungs-, Filter- oder Renderinglogik enthalten.
Der grobe Aufbau ist:

```csharp
internal static class GetFileTreeTool
{
    internal const int DefaultMaxResults = 200;
    internal const int MaxResultsCap = 2_000;
    internal const int MaxDepthCap = 32;

    internal static async Task<CallToolResult> ExecuteAsync(
        string projectRoot,
        GetFileTreeInput input,
        CancellationToken cancellationToken)
    {
        var validation = GetFileTreeInputValidator.Validate(projectRoot, input);
        if (validation is not null) return validation;

        try
        {
            var scan = GetFileTreeScanner.Scan(projectRoot, input, cancellationToken);
            var text = GetFileTreeRenderer.Render(scan);
            return McpToolResults.Text(text, new { FileTree = scan.Payload });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.ResourceNotFound,
                $"Dateisystem konnte nicht vollstaendig gelesen werden: {ex.Message}",
                context: projectRoot,
                hint: "Root, Berechtigungen und Ausschlussmuster pruefen.");
        }
    }
}
```

Der Ausschnitt ist ein Strukturvorschlag, kein fertiger Implementierungscode. Die
konkrete Exception-Strategie soll die vorhandene `isError`-Policy beachten:
erwartbare fehlende Roots und partielle Zugriffsprobleme sind recoverable bzw.
Completeness-Warnungen; echte unerwartete Malfunctions bleiben `IsError=true`.

### Input-Record und Validierung

```csharp
internal sealed record GetFileTreeInput(
    string Root,
    string View,
    IReadOnlyList<string>? IncludeExtensions,
    string? FileFilter,
    IReadOnlyList<string>? ExcludePatterns,
    int? MaxDepth,
    int TreeDepth,
    int MaxResults,
    string SortBy,
    bool IncludeMetadata,
    bool IncludeLineCount);
```

Die Validierung soll vor dem Walk erfolgen:

- `projectRoot` leer oder relativ → vorhandener Root-Guard.
- `root` leer → `.`.
- `root` absolut → `INVALID_ARGUMENT`.
- aufgelöster Root außerhalb des `projectRoot` → `INVALID_ARGUMENT`.
- Root nicht vorhanden oder keine Directory → recoverable `RESOURCE_NOT_FOUND`.
- `view`, `sortBy` und Extensions mit ungültiger Form → recoverable
  `INVALID_ARGUMENT`.
- `maxResults` kleiner als 1 → `INVALID_ARGUMENT`.
- `maxDepth` kleiner als 0 → `INVALID_ARGUMENT`.
- Werte oberhalb der Caps werden entweder recoverable abgelehnt oder deterministisch
  auf den Cap begrenzt; empfohlen wird ein recoverable Hinweis, damit der Agent die
  tatsächlich angefragte Begrenzung kennt.

### Scanner-Ablauf

Der Scanner soll in einem Walk gleichzeitig zählen, aggregieren und die ersten
Treffer sammeln:

```csharp
internal static FileTreeScanResult Scan(
    string projectRoot,
    GetFileTreeInput input,
    CancellationToken cancellationToken)
{
    var resolvedRoot = FileTreePathResolver.ResolveRoot(projectRoot, input.Root);
    var accumulator = new FileTreeAccumulator(projectRoot, resolvedRoot, input);

    var walkOptions = FileSystemWalkOptions.ForFileTree(
        maxDepth: input.MaxDepth,
        cancellationToken);

    var walkStats = FileSystemExclusionHelpers.WalkFilteredTree(
        [resolvedRoot],
        walkOptions,
        directory => accumulator.VisitDirectory(directory),
        file => accumulator.VisitFile(file));

    return accumulator.Build(walkStats);
}
```

`FileTreeAccumulator.VisitFile` prüft in dieser Reihenfolge:

1. relativen, normalisierten Pfad erzeugen,
2. benutzerdefinierte Ausschlussmuster prüfen,
3. Extension prüfen,
4. `fileFilter` prüfen,
5. günstige Dateimetadaten lesen,
6. Trefferzähler und Directory-Aggregate aktualisieren,
7. nur bis `maxResults` konkrete File-Nodes aufnehmen.

Die Summary soll trotzdem die tatsächliche Zahl der gematchten Dateien enthalten,
wenn der Walk vollständig abgeschlossen wurde. Falls aus Performancegründen vor
dem Ende abgebrochen wird, muss `scanCompleted=false` und die Gesamtzahl als nicht
vollständig erkennbar sein. Die erste Implementierung soll bevorzugt vollständig
zählen und nur die Antwortliste begrenzen.

## Wiederverwendung vorhandener Infrastruktur

### `FileSystemExclusionHelpers`

Die zentrale Wiederverwendung ist:

- `SearchExcludedDirectories` als gemeinsame Standardliste,
- `IsExcludedDirectoryName` für frühes Überspringen ganzer Teilbäume,
- `IsTraversableSubDirectory` für Reparse-Point-/Symlink-Schutz,
- `WalkFilteredTree` für Warnungen, Traversierung und Deduplizierung von Wurzeln,
- `TreeWalkStats` für unzugängliche Teilbäume.

Die Liste enthält bereits unter anderem `.git`, `.hg`, `.svn`, `.vs`, `.idea`,
`obj`, `bin`, `node_modules`, `worktrees`, `.worktrees`, `TestResults`, `artifacts`,
`coverage`, `temp` und `packages`.

Wichtig: `SafeEnumerateFiles` ist nicht gleichbedeutend mit „sicherer Standard-
ausgeschlossener Walk“. Der bestehende Testname
`SafeEnumerateFiles_ExistingDir_ReturnsAllFilesIncludingGenerated` dokumentiert,
dass dieser Helper auch generierte Dateien liefert. Für `get_file_tree` darf er
daher nicht allein verwendet werden. Entweder wird `WalkFilteredTree` geeignet
erweitert oder der gemeinsame Walk-Kern wird minimal zu einer konfigurierbaren
Collector-Variante generalisiert.

### Minimal sinnvolle Generalisierung des Walk-Kerns

Die bestehende Signatur soll nicht unkontrolliert durch viele optionale Parameter
aufgebläht werden. Ein sinnvoller Vorschlag ist ein Options-Record mit kompatibler
Alt-Überladung:

```csharp
internal sealed record FileSystemWalkOptions(
    int MaxDepth,
    bool SkipExcludedDirectories,
    CancellationToken CancellationToken)
{
    internal static FileSystemWalkOptions Default(CancellationToken cancellationToken)
        => new(int.MaxValue, SkipExcludedDirectories: true, cancellationToken);

    internal static FileSystemWalkOptions ForFileTree(
        int? maxDepth,
        CancellationToken cancellationToken)
        => new(maxDepth ?? int.MaxValue, SkipExcludedDirectories: true, cancellationToken);
}
```

Der bestehende `WalkFilteredTree`-Contract kann als delegierende Überladung erhalten
bleiben. Die neue Variante muss:

- die bestehende Ausschlussliste standardmäßig weiterverwenden,
- die vorhandenen Call-Sites nicht semantisch verändern,
- Tiefe und Cancellation vor Directory- und File-Besuchen prüfen,
- Warnungszählung und Reparse-Point-Schutz unverändert beibehalten.

Ein `includeExcludedDirectories`-Schalter gehört nicht in den ersten Contract. Er
würde die Bedeutung der bestehenden Sicherheits- und Performance-Defaults verwässern.

### Glob-Wiederverwendung

`FileFilterEvaluator` besitzt bereits Glob-Logik und `MatchesGlobForWeb` unterstützt
relative Pfade sowie `**`. Diese Logik soll nicht ein zweites Mal im neuen Scanner
implementiert werden.

Da der Name `MatchesGlobForWeb` fachlich zu eng ist, ist folgende kleine DRY-
Generalisierung sinnvoll:

```csharp
internal static class PathGlobMatcher
{
    internal static bool Matches(string normalizedPath, string pattern)
    {
        // Bestehende case-insensitive *, ?, **-Semantik zentral ausführen.
    }
}
```

`FileFilterEvaluator.MatchesGlobForWeb` delegiert danach auf `PathGlobMatcher`, und
`get_file_tree` verwendet denselben Matcher. Die bisherige Konfigurationssemantik
bleibt durch Delegation erhalten.

### Pfadnormalisierung und Sicherheitsgrenze

`PathNormalizer.NormalizeSeparators` und die relative Ausgabeform können
wiederverwendet werden. Die vorhandene `PathNormalizer.ToRelative` ist jedoch kein
geeigneter alleiniger Sicherheits-Guard: Der Stringvergleich mit `StartsWith` hat
keine explizite Verzeichnisgrenze. Ein Root wie `C:/repo` darf nicht versehentlich
`C:/repository` als Unterpfad akzeptieren.

Dafür ist ein kleiner, dedizierter Resolver vorgesehen:

```csharp
internal static class FileTreePathResolver
{
    internal static string ResolveRoot(string projectRoot, string relativeRoot)
    {
        var fullProjectRoot = Path.GetFullPath(projectRoot);
        var candidate = Path.GetFullPath(
            Path.Combine(fullProjectRoot, relativeRoot));

        var relative = Path.GetRelativePath(fullProjectRoot, candidate);
        if (relative == ".."
            || relative.StartsWith("../", StringComparison.Ordinal)
            || Path.IsPathRooted(relative))
        {
            throw new InvalidOperationException("Root liegt ausserhalb des projectRoot.");
        }

        return candidate;
    }
}
```

In echtem Produktionscode soll der Resolver statt einer rohen Exception einen
validierten Result-/Fehlerpfad verwenden. Zusätzlich wird der Root selbst auf
Reparse-Point-Eigenschaften geprüft; innerhalb des Walks werden Reparse-Point-
Verzeichnisse nicht betreten.

## Textdarstellung

Die Textantwort ist für Hosts wichtig, die `structuredContent` nicht prominent
anzeigen. Sie soll kurz, stabil und ohne Zeitstempel sein.

Beispiel:

```text
get_file_tree: root=. view=tree
147 Dateien gescannt, 18 Treffer, 278.2 KB gematcht
Extensions: .md 18

.
├── .agents/              6 Dateien | 42.1 KB
├── Docs/                 12 Dateien | 190.4 KB
└── README.md              1 Datei  | 23.3 KB

[vollstaendig: 18 Dateien gezeigt]
```

Bei Trunkierung:

```text
[WARN]: 418 Dateien gematcht, 200 gezeigt.
[HINWEIS]: fileFilter/root verfeinern oder maxResults erhöhen.
```

Die Textansicht darf keine andere Zählung als `StructuredContent` verwenden. Beide
werden aus demselben `FileTreeScanResult` gerendert.

## Fehler- und Completeness-Vertrag

### Recoverable Eingabefehler

Erwartbare Aufruferfehler liefern `IsError=false` mit dem bestehenden strukturierten
Fehlerformat:

- `INVALID_ARGUMENT`: ungültige View, ungültiger Glob, negativer Depth, Root außerhalb
  des Projektroots.
- `RESOURCE_NOT_FOUND`: relativer Root existiert nicht oder ist kein Verzeichnis.
- `PROJECT_ROOT_REQUIRED` / `PROJECT_ROOT_INVALID`: bestehender Root-Guard.

### Partielle Dateisystemfehler

Ein unzugänglicher Unterbaum macht den gesamten Aufruf nicht automatisch zu einem
Toolfehler. Der Walk fährt mit erreichbaren Bereichen fort und meldet:

- `scanCompleted: false` oder eine eigene Warnung,
- `inaccessibleSubtreeCount`,
- begrenzte, nicht unnötig detailreiche Warnungspfade in `warnings`.

Das Verhalten soll dem bestehenden `TreeWalkStats`-Vertrag entsprechen.

### Unerwartete Fehler

Unerwartete Exceptions bleiben echte Malfunctions und verwenden den bestehenden
`McpToolResults.Error`-Pfad. `CompilationError` soll für dieses Tool nicht als
semantisch falscher Spezialfall wiederverwendet werden.

### Cancellation

Bei Cancellation darf der Scanner keine scheinbar vollständige Antwort erzeugen.
Wenn die MCP-Laufzeit Cancellation als normalen Abbruch behandelt, soll die
Operation den CancellationToken respektieren und nicht in eine recoverable
„Datei nicht gefunden“-Antwort umwandeln.

## Performance- und Größenbudget

### Grundsätze

- Ein Walk pro Aufruf.
- Keine Inhaltslesevorgänge im Standardmodus.
- `FileInfo.Length` nur für erforderliche Treffer/Aggregate lesen.
- `includeLineCount=false` als Standard.
- Lazy Enumeration statt zuerst alle Pfade in eine Liste zu laden.
- Konkrete Treffer bei `maxResults` begrenzen.
- Scan- und Antwortgrenze getrennt darstellen.
- Cancellation während Enumeration und Metadatenabfrage prüfen.

### Warum kein Zeitstempel im Default

Zeitstempel erhöhen die Antwortgröße, ändern sich unabhängig von semantischen
Codeänderungen und helfen dem Agenten bei der initialen Strukturorientierung selten.
Dateigröße ist für die Entscheidung „welche Datei könnte groß/relevant sein?“
unmittelbar nützlicher. Änderungszeiten können später gezielt für Sortierung oder
Change-Context ergänzt werden.

### Große Repositories

Die Antwort benötigt harte Caps. Der Scanner darf nicht stillschweigend nur die
ersten Treffer liefern und diese als vollständig markieren. Mindestens folgende
Felder sind Pflicht:

- `scanCompleted`,
- `truncated`,
- `truncatedBy`,
- `shownFileCount`,
- `matchedFileCount` nur bei vollständigem Zählen als belastbare Gesamtzahl.

Wenn die Enumeration selbst wegen eines Sicherheitslimits abgebrochen wird, muss
`truncatedBy` einen stabilen Wert wie `maxDepth`, `maxResults` oder `scanBudget`
enthalten.

## Sicherheitskonzept

### Root-Grenze

Der effektive Scanroot wird ausschließlich aus `projectRoot` plus relativem `root`
gebildet. Absolute `root`-Werte sind nicht als Komfortsyntax erlaubt, weil sie die
Scope-Grenze unnötig aufweichen.

### Traversierung

- Reparse-Point-Verzeichnisse nicht betreten.
- Junction-/Symlink-Zyklen nicht verfolgen.
- Root selbst auf zulässige Lage und Directory-Status prüfen.
- Pfade vor Filterung kanonisch und mit `/` normalisieren.
- Keine vom Dateinamen interpretierte Shell-Syntax.

### Datenminimierung

Das Tool liefert nur Pfade, Zähler und optionale Dateimetadaten. Es liest und
überträgt standardmäßig keine Inhalte und damit auch keine Secret-Inhalte aus
`.env`, Zertifikaten oder Konfigurationsdateien. Dateinamen können dennoch sensibel
sein; die Ausgabe bleibt auf den registrierten Projektroot beschränkt.

### Standardausschlüsse

Die bestehenden Ausschlüsse bleiben aktiv. Benutzerdefinierte `excludePatterns`
können weitere Pfade ausblenden, aber die sicherheits- und performancebezogenen
Standardausnahmen werden im MVP nicht per Negationssyntax aufgehoben.

## Zusammenspiel mit bestehenden MCP-Tools

| Frage | Tool |
|---|---|
| Welche physischen Dateien und Verzeichnisse gibt es? | `get_file_tree` |
| Welche Dateitypen sieht der aktuelle Index? | `get_index_scope` |
| Welche C#-Namespaces und Typen gibt es? | `get_namespace_tree` |
| Welche Signaturen liegen in bekannten C#-Dateien? | `get_file_skeleton` |
| Wo kommt ein Text-/Namensmuster vor? | `search_pattern` |
| Welche C#-Metriken liegen vor? | `metrics_tree`, `metrics_lookup` |

`get_file_tree` ersetzt keines dieser Tools. Es ist der Discovery-Schritt vor der
gezielten semantischen oder inhaltlichen Analyse.

### Typischer Agenten-Loop

```text
1. get_file_tree(projectRoot, view="tree")
2. get_file_tree(projectRoot, root="Docs", view="files", includeExtensions=[".md"])
3. search_pattern(projectRoot, pattern="...", scope="Docs")
4. gezieltes Inhalts-/Symboltool auf den gefundenen Pfaden
```

Der erste Aufruf soll keine vollständige Dokumentation lesen. Er liefert nur genug
Kontext, damit der Agent nicht blind raten muss, wo Dokumentation und relevante
Dateitypen liegen.

## Registrierung und Wiring

Die Registrierung wird in `FileStructureToolRegistrations.Register` ergänzt:

```csharp
private static void AddGetFileTree(
    McpServerPrimitiveCollection<McpServerTool> tools,
    ProjectRegistry registry)
{
    tools.Add(McpServerTool.Create(
        async (
            string projectRoot,
            string? root = null,
            string view = "tree",
            string[]? includeExtensions = null,
            string? fileFilter = null,
            string[]? excludePatterns = null,
            int? maxDepth = null,
            int treeDepth = 2,
            int maxResults = GetFileTreeTool.DefaultMaxResults,
            string sortBy = "path",
            bool includeMetadata = true,
            bool includeLineCount = false,
            CancellationToken ct = default) =>
            await ProjectToolCall.ExecuteFilesystemAsync(
                registry,
                projectRoot,
                lease => GetFileTreeTool.ExecuteAsync(
                    projectRoot,
                    new GetFileTreeInput(
                        root ?? ".",
                        view,
                        includeExtensions,
                        fileFilter,
                        excludePatterns,
                        maxDepth,
                        treeDepth,
                        maxResults,
                        sortBy,
                        includeMetadata,
                        includeLineCount),
                    ct)),
        McpToolRegistrationOptions.ReadOnlyTool(
            "get_file_tree",
            GetFileTreeDescription)));
}
```

Die Lambda-Signatur ist ein konkreter Verdrahtungsvorschlag. Falls der Compiler wegen
des ungenutzten Leases warnt, soll der Dispatch den kanonischen Root über einen
kleinen Context-Record weiterreichen oder die Parameterform entsprechend anpassen;
kein neuer DI-Container ist erforderlich.

Die Beschreibung für `tools/list` muss explizit festhalten:

- wann das Tool zu verwenden ist,
- dass `root` relativ zu `projectRoot` ist,
- dass `fileFilter` ein Pfadglob und keine Textsuche ist,
- dass Standardausschlüsse aktiv sind,
- dass `view=tree` die Defaultansicht ist,
- dass Antwortgrenzen und partielle Scans über `completeness` sichtbar sind.

## Tests und Verifikation der späteren Implementierung

Diese Konzeptaufgabe schreibt ausschließlich Markdown; Build und Tests sind dafür
nicht erforderlich. Die spätere Implementierung benötigt jedoch folgende Abdeckung.

### Fast-/Unit-Tests

- Extension-Normalisierung für `md`, `.md`, `.MD`, `*` und extensionlose Dateien.
- AND-Semantik von Extension- und `fileFilter`.
- `*`, `?` und `**` in relativen Pfad-Globs.
- Forward-/Backslash-Normalisierung.
- Root `.` und verschachtelte relative Roots.
- Ablehnung von `..`, absoluten Unterpfaden und Root-Präfix-Sibling-Fällen.
- Directory-Tiefe und Root-Tiefe 0.
- stabile Sortierung nach Pfad, Größe und Extension.
- Aggregation von Count und Bytes.
- Trunkierung mit korrektem `completeness`-Contract.
- fehlende oder unzugängliche Verzeichnisse.
- Reparse-Point-Verzeichnisse werden nicht betreten.
- `includeLineCount` verändert die Standard-I/O-Semantik nicht.
- Text- und Structured-Content stammen aus demselben Scanresult.

### Integrationstests

- Tool ist in `tools/list` registriert und als read-only/idempotent annotiert.
- MCP-Argumente binden an den vorgeschlagenen Vertrag.
- Structured Content ist ein JSON-Objekt mit `fileTree`, kein Top-Level-Array.
- Root-Pfade werden relativ zum `projectRoot` interpretiert.
- Markdown- und Nicht-C#-Dateien außerhalb der Roslyn-Dokumentliste erscheinen.
- Ein Roslyn-Load-Fehler blockiert den physischen File-Tree-Aufruf nicht, sofern die
  Projektbindung den Root noch auflösen kann.
- Projektroot mit Solution in einem Unterverzeichnis listet auch Root-Dokumentation
  außerhalb des Solution-Verzeichnisses korrekt.

### Dogfood-Test

Ein Live-Test gegen das AiNetLinter-Repository soll nur stabile Eigenschaften prüfen:

- Root-/Projektname ist korrekt,
- bekannte Top-Level-Bereiche wie `src`, `Docs` und `.agents` können über Filter
  gefunden werden,
- `README.md`- und `.md`-Filter liefern nicht-leere, strukturierte Ergebnisse,
- die Antwort ist nicht als vollständig markiert, wenn ein absichtlicher kleiner
  `maxResults`-Wert Trunkierung auslöst.

Exakte Dateizahlen gehören nicht in einen fragilen Dogfood-Test, weil sich das
Repository weiterentwickelt.

### Stress-Kategorie

Ein normaler File-Tree-Integrationstest gehört nicht automatisch in `Stress`. Nur
ein Test, der absichtlich hohe parallele Last oder sehr große künstliche Bäume
erzeugt, wäre separat als `Stress` zu klassifizieren.

## Dokumentation und Release-Folgen bei der Implementierung

Die Konzeptdatei selbst ändert keine produktive Schnittstelle. Bei einer späteren
Implementierung sind gemäß Projektregeln mindestens zu aktualisieren:

- `Docs/agent-api.md` mit MCP-Contract, Parametern und Response-Modell,
- `Docs/integration.md` mit Einbindung und Toolwahl,
- `README.md` mit der Toolübersicht,
- `Docs/ROADMAP.md` mit der umgesetzten MCP-Erweiterung.

`rules.json` muss nur geändert werden, wenn tatsächlich konfigurierbare Linter-
Regeln oder globale Toolparameter eingeführt werden. Für die in diesem Konzept
vorgeschlagenen Request-Parameter ist das nicht erforderlich.

Eine Synchronisierung von `.agents/rules/AiNetLinter.mdc` ist nur nötig, wenn durch
die spätere Umsetzung `rules.json` geändert wird.

## Verworfene Alternativen

- **`list_files`:** verworfen als Hauptname, weil er eine flache Liste nahelegt und
  die Aggregations-/Baumfunktion nicht ausdrückt.
- **`explore_files`:** verworfen als Hauptname, weil „explore“ die Aktion nicht
  eindeutig von Inhaltssuche oder Dokumentklassifikation abgrenzt.
- **`get_project_structure`:** verworfen, weil der bestehende semantische Namespace-
  und Projektbaum darunter verstanden werden könnte.
- **Nur `get_index_scope` erweitern:** verworfen, weil Index-/Roslyn-Scope und
  physische Dateisystemstruktur unterschiedliche Wahrheiten und Lebenszyklen haben.
- **`search_pattern` mit Regex `.*` missbrauchen:** verworfen, weil das nur Dateien
  mit lesbaren Matching-Zeilen approximiert, Metadaten und leere Dateien verliert
  und die Semantik für Agenten unnötig indirekt macht.
- **Nur `SolutionFileWalker` wiederverwenden:** verworfen, weil dieser Walker
  absichtlich gültige Roslyn-Quelldokumente sammelt und damit gerade die Dateien
  außerhalb des C#-Solution-Snapshots nicht abdeckt.
- **Shell-Command-Wrapper:** verworfen als MCP-Kernlösung, weil Shell-Quoting,
  Plattformverhalten, Security-Grenzen und strukturierte Completeness dann vom Host
  statt vom AiNetLinter kontrolliert würden.
- **Zeitstempel als Standardfeld:** verworfen wegen Rauschen und instabiler Antwort-
  snapshots; gezielte Zeit-Sortierung bleibt eine spätere Erweiterung.
- **Automatische Dokumentklassifikation:** verworfen und als eigenes Folgekonzept
  zurückgestellt; entspricht ausdrücklich nicht dem aktuellen Scope.

## Wo im Projekt

Die folgenden Pointer sind die relevanten Bestandscode-Stellen für die spätere
Umsetzung:

- `src/AiNetLinter/Mcp/Registration/FileStructureToolRegistrations.cs` – vorhandene
  Registrierung der dateistrukturorientierten MCP-Tools.
- `src/AiNetLinter/Mcp/Tools/FileStructure/GetIndexScopeTool.cs` – dünnes Tool-Dispatch-
  Muster mit Structured Content und Serverzustand.
- `src/AiNetLinter/Mcp/Tools/FileStructure/GetIndexScopeScanner.cs` – Scanner-/Record-
  Ablage im bestehenden File-Structure-Bereich.
- `src/AiNetLinter/Mcp/Tools/FileStructure/SolutionFileWalker.cs` – vorhandener
  Roslyn-Document-Walk; als physische Lösung bewusst nur Referenz, nicht direkte
  Implementierungsbasis.
- `src/AiNetLinter/Baseline/FileSystemExclusionHelpers.cs` – zentrale physische
  Traversierung, Standardausschlüsse, Reparse-Point-Schutz und Warnungszählung.
- `src/AiNetLinter/Baseline/TreeWalkStats.cs` – Ergebnis-/Warnungsmodell für
  unzugängliche Teilbäume.
- `src/AiNetLinter/Configuration/FileFilterEvaluator.cs` – vorhandene Glob- und
  Ausschlusslogik, deren gemeinsame Kernsemantik nicht dupliziert werden soll.
- `src/AiNetLinter/Configuration/Config.ValueTypes.cs` – bestehendes
  `FileFiltersConfig`-Modell als Referenz für Datei-/Verzeichnisfilter.
- `src/AiNetLinter/Output/PathNormalizer.cs` – vorhandene Pfadnormalisierung und
  Output-Konventionen; für die neue Sicherheitsgrenze ist ein Boundary-sicherer
  Resolver erforderlich.
- `src/AiNetLinter/Mcp/Projects/ProjectToolCall.cs` – bestehender projektgebundener
  Dispatch und Root-Guard; für den filesystem-only Load-State wird eine eng
  begrenzte Variante benötigt.
- `src/AiNetLinter/Mcp/McpToolResults.cs` – gemeinsamer Fehler- und Structured-
  Content-Vertrag.
- `src/AiNetLinter/Mcp/McpTruncation.cs` – vorhandenes Trunkierungsprinzip; für den
  neuen Dateibaum ist ein eigener strukturierter Completeness-Contract vorzusehen.

## Entdeckte Mängel/Redundanzen

### Vorhandenen Ausschluss-Walk wiederverwenden

- **Gefunden:** `FileSystemExclusionHelpers` enthält bereits `SearchExcludedDirectories`,
  `IsExcludedDirectoryName`, Reparse-Point-Schutz, `WalkFilteredTree` und
  `TreeWalkStats`.
- **Bezug:** Die Architekturregeln verlangen DRY und zentrale, verständliche
  Dateisystemregeln; eine neue `bin`-/`obj`-Liste wäre eine konkurrierende Quelle.
- **Vorschlag:** `get_file_tree` auf diesem Walk aufbauen und ihn nur um Options-
  Record, Tiefe und Cancellation erweitern.
- **Entscheidung:** übernommen ins Scope; siehe Muss-Haben und „Wiederverwendung
  vorhandener Infrastruktur“.

### `SafeEnumerateFiles` nicht mit Standardexklusion verwechseln

- **Gefunden:** `SafeEnumerateFiles` verwendet eine rekursive Enumeration, deren
  bestehender Integrationstest ausdrücklich auch generierte Dateien erwartet.
- **Bezug:** Ein blinder Einsatz würde die gewünschte Standardausnahme von `bin`,
  `obj` und ähnlichen Verzeichnissen umgehen.
- **Vorschlag:** `WalkFilteredTree` beziehungsweise dessen generalisierte Variante
  verwenden; keine zusätzliche Exclusion-Implementierung im MCP-Tool.
- **Entscheidung:** übernommen ins Scope; direkte Nutzung von `SafeEnumerateFiles`
  als alleiniger Collector ist ausgeschlossen.

### Glob-Logik nicht ein zweites Mal implementieren

- **Gefunden:** `FileFilterEvaluator` besitzt bereits Wildcard- und `**`-Semantik,
  die von `WebFileCatalog` verwendet wird.
- **Bezug:** Eine zweite Regex-Übersetzung im neuen Tool würde bei `*`, `**`,
  Separatoren oder Case-Semantik auseinanderdriften.
- **Vorschlag:** Glob-Kern in einen neutral benannten `PathGlobMatcher` extrahieren;
  bestehende Aufrufer delegieren auf diesen Kern.
- **Entscheidung:** übernommen als Implementierungsleitplanke; Extraktionsumfang
  bleibt im Planer zu konkretisieren.

### Roslyn-File-Walker nicht als physische Enumeration verwenden

- **Gefunden:** `SolutionFileWalker` sammelt `Document`-Objekte und filtert über
  `SourceFileCatalog.IsValidDocument`.
- **Bezug:** Das würde Markdown, Konfigurationen und andere Dateien außerhalb des
  C#-Document-Snapshots erneut unsichtbar machen.
- **Vorschlag:** Für `get_file_tree` physisch enumerieren; `SolutionFileWalker` nur
  als Abgrenzungsreferenz behandeln.
- **Entscheidung:** bewusst nicht wiederverwenden.

### Pfad-Output ist nicht automatisch ein Security-Guard

- **Gefunden:** `PathNormalizer.ToRelative` normalisiert Output-Pfade, prüft die
  Root-Zugehörigkeit aber über einen Stringpräfix ohne explizite Directory-Grenze.
- **Bezug:** Für einen vom Agenten kontrollierten Unterpfad reicht eine reine
  Ausgabe-Normalisierung nicht als Traversal-Schutz.
- **Vorschlag:** Boundary-sicheren `FileTreePathResolver` über
  `Path.GetRelativePath` und explizite `..`-/Root-Prüfung einführen; Output danach
  weiterhin über Forward-Slash normalisieren.
- **Entscheidung:** übernommen ins Scope für die spätere Implementierung; keine
  unaufgeforderte Reparatur des bestehenden Helpers in dieser Konzeptänderung.

### Load-State-Kopplung physischer Tools

- **Gefunden:** Der bestehende `ProjectToolCall` gate-t derzeit alle registrierten
  projektgebundenen Tool-Lambdas über den residenten Serverzustand und meldet Loading
  beziehungsweise Load-Failed vor dem eigentlichen Tool-Call.
- **Bezug:** Ein Dateilandkarten-Tool braucht Roslyn nicht und wäre gerade beim
  Diagnostizieren eines fehlerhaften Solution-Loads wertvoll.
- **Vorschlag:** Eng begrenzten `ExecuteFilesystemAsync`-Dispatch ergänzen, der die
  Projektbindung/Root-Prüfung behält, aber die reine Enumeration nicht vom geladenen
  Roslyn-Snapshot abhängig macht.
- **Entscheidung:** übernommen ins Scope als Architekturentscheidung; bestehende
  Solution-Tools dürfen dabei keinen geänderten Load-State-Vertrag erhalten.

## Wie (grober Ansatz)

1. `get_file_tree` in der bestehenden File-Structure-Registration als read-only Tool
   registrieren.
2. Projektroot-Dispatch um den filesystem-only Pfad ergänzen, ohne die vorhandenen
   Roslyn-Toolverträge zu verändern.
3. Relativen `root` mit einem boundary-sicheren Resolver unterhalb des
   `projectRoot` auflösen.
4. Den vorhandenen physischen Exclusion-/Reparse-Point-Walk mit einem kleinen
   Options-Record für Tiefe, Cancellation und Collector-Nutzung generalisieren.
5. Glob-Kern aus der vorhandenen Filterlogik wiederverwenden beziehungsweise neutral
   extrahieren.
6. In einem einzigen Walk Dateien filtern, Größen aggregieren, Verzeichnisse
   aufbauen und begrenzte File-Nodes sammeln.
7. Aus dem gleichen Scanresult Structured Content und Text rendern.
8. Partielle Walks, Trunkierung und Warnungen im Completeness-Objekt offenlegen.
9. Tests in FastTests, IntegrationTests und MCP-Vertragstests ergänzen.
10. Nach erfolgreicher Implementierung die MCP-Dokumentation und Roadmap gemäß den
    Projektregeln synchronisieren.

## Definition of Done / Erfolgskriterien

Die spätere Implementierung ist erst fertig, wenn alle folgenden Aussagen gelten:

- `get_file_tree` ist über MCP registriert und wird als read-only/idempotent
  beschrieben.
- Ein Aufruf mit nur `projectRoot` liefert eine begrenzte, verständliche physische
  Projektlandkarte ohne Dateiinhalte.
- `root` wird ausschließlich relativ zum `projectRoot` aufgelöst.
- `includeExtensions: [".md"]` findet Markdown-Dateien auch außerhalb der Roslyn-
  Solution, sofern sie nicht durch Standardausschlüsse verborgen sind.
- `fileFilter: "**/README.md"` findet verschachtelte Readmes unabhängig von ihrer
  Tiefe innerhalb des erlaubten Scopes.
- `*` und fehlende Extensionfilter haben die dokumentierte Semantik.
- Standardausschlüsse stammen aus der vorhandenen zentralen Infrastruktur; es gibt
  keine neue parallele `bin`-/`obj`-Liste im MCP-Tool.
- Reparse-Point-Verzeichnisse werden nicht traversiert.
- Unzugängliche Teilbäume führen zu sichtbaren Completeness-Warnungen und nicht zu
  einer falschen Vollständigkeitsbehauptung.
- Antwortlimits sind hart, deterministisch und in Structured Content sichtbar.
- Pfade sind relativ beziehungsweise als kanonischer effektiver Root dokumentiert
  und in Ausgaben mit `/` normalisiert.
- Dateigrößen werden korrekt aggregiert; Zeitstempel erscheinen nicht ungefragt.
- Standardmäßig werden keine Dateiinhalte gelesen.
- `structuredContent` ist ein JSON-Objekt mit `fileTree`.
- Der Tool-Aufruf funktioniert auch dann physisch, wenn Roslyn noch lädt oder der
  Solution-Load fehlschlägt, sofern die Projektbindung selbst gültig ist.
- Bestehende Roslyn-Tools behalten ihren bisherigen Load-State- und Fehlervertrag.
- Fast-, Integrations- und MCP-Vertragstests decken Filter, Sicherheit, Trunkierung,
  Structured Content und Dogfood-Grundverhalten ab.
- `Docs/agent-api.md`, `Docs/integration.md`, `README.md` und `Docs/ROADMAP.md` sind
  bei der Produktimplementierung aktualisiert.

## Offene Punkte

Keine offenen Produktentscheidungen für dieses Konzept. Die folgenden Themen sind
bewusst als spätere, eigene Erweiterungen markiert:

- Dokument-/Orientierungsheuristiken einschließlich `purpose: "orientation"`.
- Pagination-Cursor für sehr große Trefferlisten.
- Optionaler Git-Change-Context.
- Separate, sichere Inhalts-/Preview-Tools.
- Konfigurierbare oder explizit überschreibbare Standardausschlüsse.
