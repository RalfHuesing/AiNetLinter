# Analyse-Cache: Inkrementelle Laufzeitoptimierung

## Motivation

Agents (Cursor, Copilot etc.) ändern pro Iteration typischerweise 1–20 Dateien.
AiNetLinter analysiert trotzdem immer alle Dateien der Solution — bei 1 000+ Dokumenten dauert das
mehrere Minuten und bremst den Feedback-Loop.

**Kernidee:** Ergebnis jeder Datei-Analyse an ihrer SHA-256-Checksum festmachen.
Unveränderte Dateien liefern beim nächsten Run dieselben Ergebnisse wie beim letzten —
wir können sie aus einem persistenten Cache bedienen und müssen weder `GetSemanticModelAsync()`
noch `LinterAnalyzer` aufrufen.

## Messbasis

Sechs Referenzläufe auf *San.smart.Planner.Platform2* (1 019 Dokumente, alle 0 Violations):

| Phase | Wert |
| :--- | :--- |
| Workspace Loading | ~4,0 s (stabil, unvermeidbar) |
| Document Analysis | 4 – 15 s (stark variabel, **der Hebel**) |
| Post-Analysis Checks | < 60 ms (vernachlässigbar) |

Die Top-16-Slowpokes sind ausnahmslos Test-Dateien aus demselben Projekt.
Sie brauchen je 7–8 s, weil Roslyn beim ersten `GetSemanticModelAsync()`-Aufruf
**die komplette Projekt-Compilation** triggert — alle Dateien blockieren auf diese eine
Compilation. Mit Cache wird `GetSemanticModelAsync()` für unveränderte Dateien gar nicht
mehr aufgerufen, Roslyn kompiliert das Test-Projekt also überhaupt nicht.

Erwarteter Effekt bei ~99 % Cache-Trefferquote (Agent ändert 10–20 Dateien):
```
Heute:      ~4 s Workspace  +  ~13 s Analysis  =  ~17 s
Mit Cache:  ~4 s Workspace  +  ~0,3 s Analysis  =  ~4,5 s  (≈ 4× schneller)
```

## Dateiübergreifende Prüfungen — kein Feature-Verlust

Es gibt vier Post-Analysis-Checks, die Daten über Datei-Grenzen hinweg zusammenführen:

| Check | Cross-File-Natur | Lösung im Cache |
| :--- | :--- | :--- |
| `TestSentinel` | Source-Klasse ↔ Test-Datei | `TestSignals` pro Datei cachen |
| `PartialClassLineAggregator` | Zeilensumme über alle Parts | `PartialClassPart`-Records cachen |
| `AIContextFootprint` | Transitive Typ-Abhängigkeiten | `ClassInfo` inkl. Footprint-Details cachen |
| `InheritanceDepth` | Basisklasse ggf. in anderer Datei | `ClassInfo.InheritanceDepth` cachen |

`PostAnalysisChecks.Run()` läuft weiterhin vollständig gegen den **aggregierten State**
(gecachte + frisch analysierte Daten). Da er < 60 ms benötigt, entsteht kein Overhead.

**Bekannte Einschränkung `AIContextFootprint`:**
Ändert sich Datei A, könnte der gecachte Footprint von Datei B (abhängig von A) leicht
veraltet sein. In der Praxis ist das akzeptabel: der Schwellwert liegt bei 5 000 Zeilen,
und kleine Änderungen verschieben selten den Footprint so stark, dass ein bislang
unkritischer Wert plötzlich das Limit überschreitet.

## Cache-Ort und Dateiname

Der Cache liegt **neben der `.exe`**, analog zu `measurements\`:

```
C:\Daten\AiNetLinter-win-x64\
  AiNetLinter.exe
  measurements\
  cache\
    San.smart.Planner.Platform2-a1b2c3d4.json   ← ein File pro Projekt
    OtherSolution-f9e7c123.json
```

### Dateinamen-Schema

```
{SolutionFileNameWithoutExtension}-{hash8}.json
```

`hash8` = erste 8 Hex-Zeichen von SHA-256 über:

```
absoluterSolutionPfad (lowercase, normalisiert) + rulesJsonInhalt
```

Das deckt beide Fälle ab:
- Gleicher Solution-Name in unterschiedlichen Verzeichnissen → unterschiedliche Hashes
- Custom `--config` mit anderem `rules.json` → anderer Hash (weil anderer Inhalt)

Kein automatisches Aufräumen — der Ordner ist klein, alter Cache schadet nicht.

## Cache-Invalidierung

| Bedingung | Auswirkung |
| :--- | :--- |
| `SchemaVersion` im File ≠ aktuelle Konstante | Gesamten Cache-File verwerfen (Neu-Analyse aller Dateien) |
| Datei fehlt im Cache (neue Datei) | Diese Datei analysieren, Eintrag anlegen |
| Datei-Checksum weicht ab | Diese Datei analysieren, Eintrag überschreiben |
| Datei aus Cache, aber nicht mehr in Solution | Eintrag still ignorieren (nicht in Output) |

Da `hash8` im Dateinamen `rules.json`-Inhalt einbezieht, ist bei Regeländerung automatisch
eine andere Cache-Datei aktiv — alter Cache wird ignoriert und mit der Zeit verdrängt.

## CLI-Option

```
--no-cache    Cache deaktivieren (erzwingt vollständige Neu-Analyse)
```

Default: Cache **aktiviert**. Kein Eintrag in `rules.json` nötig — das ist ein
Tool-Laufzeit-Concern, kein Projekt-Concern.

## Was pro Datei gecacht wird

```json
{
  "relativePath": "src/Auth/JwtService.cs",
  "checksum": "3f7a9b2e...",
  "violations": [
    {
      "filePath": "...",
      "lineNumber": 42,
      "ruleName": "MaxMethodLineCount",
      "details": "...",
      "guidance": "..."
    }
  ],
  "classes": [
    {
      "name": "JwtService",
      "filePath": "...",
      "lineNumber": 10,
      "maxCognitiveComplexity": 3,
      "inheritanceDepth": 1,
      "aiContextFootprint": 1240,
      "aiContextFootprintDetails": [
        { "name": "TokenValidator", "lines": 420 }
      ],
      "hasTestMethods": false,
      "isPartial": false,
      "isStatic": false,
      "baseTypeNames": ["ITokenService"],
      "projectName": "MyProject"
    }
  ],
  "partialParts": [
    { "typeName": "LinterAnalyzer", "filePath": "...", "lineNumber": 1, "fileLineCount": 380 }
  ],
  "testSignals": {
    "testClassNames": ["JwtServiceTests"],
    "referencedTypeNames": ["JwtService"],
    "coversComments": []
  }
}
```

## Implementierungsplan

### Neue Dateien

#### `src/AiNetLinter/Cache/AnalysisCacheEntry.cs`

DTO für einen Datei-Cache-Eintrag (System.Text.Json-serialisierbar):

```csharp
#nullable enable
namespace AiNetLinter.Cache;

internal sealed record AnalysisCacheEntry
{
    public required string RelativePath { get; init; }
    public required string Checksum { get; init; }
    public IReadOnlyList<RuleViolationDto> Violations { get; init; } = [];
    public IReadOnlyList<ClassInfoDto> Classes { get; init; } = [];
    public IReadOnlyList<PartialPartDto> PartialParts { get; init; } = [];
    public TestSignalsDto TestSignals { get; init; } = new();
}

internal sealed record RuleViolationDto(
    string FilePath, int LineNumber, string RuleName, string Details, string Guidance);

internal sealed record ClassInfoDto(
    string Name, string FilePath, int LineNumber,
    int MaxCognitiveComplexity, int InheritanceDepth, int AiContextFootprint,
    IReadOnlyList<FootprintDetailDto> AiContextFootprintDetails,
    bool HasTestMethods, bool IsPartial, bool IsStatic,
    IReadOnlyList<string> BaseTypeNames, string? ProjectName);

internal sealed record FootprintDetailDto(string Name, int Lines);

internal sealed record PartialPartDto(
    string TypeName, string FilePath, int LineNumber, int FileLineCount);

internal sealed record TestSignalsDto
{
    public IReadOnlyList<string> TestClassNames { get; init; } = [];
    public IReadOnlyList<string> ReferencedTypeNames { get; init; } = [];
    public IReadOnlyList<string> CoversComments { get; init; } = [];
}
```

#### `src/AiNetLinter/Cache/AnalysisCacheFile.cs`

Root-Objekt der JSON-Datei:

```csharp
#nullable enable
namespace AiNetLinter.Cache;

internal sealed record AnalysisCacheFile
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public Dictionary<string, AnalysisCacheEntry> Files { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
```

#### `src/AiNetLinter/Cache/AnalysisCacheManager.cs`

Lädt, befüllt und speichert den Cache:

```csharp
#nullable enable
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AiNetLinter.Cache;

internal sealed class AnalysisCacheManager
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly string _cachePath;
    private AnalysisCacheFile _cache;
    private bool _dirty;

    private AnalysisCacheManager(string cachePath, AnalysisCacheFile cache)
    {
        _cachePath = cachePath;
        _cache = cache;
    }

    public static AnalysisCacheManager Load(string exeDir, string solutionPath, string rulesJsonContent)
    {
        var cacheDir = Path.Combine(exeDir, "cache");
        Directory.CreateDirectory(cacheDir);

        var fileName = BuildCacheFileName(solutionPath, rulesJsonContent);
        var cachePath = Path.Combine(cacheDir, fileName);

        var cache = TryReadCache(cachePath) ?? new AnalysisCacheFile();
        return new AnalysisCacheManager(cachePath, cache);
    }

    public bool TryGet(string relativePath, string currentChecksum, out AnalysisCacheEntry? entry)
    {
        entry = null;
        if (!_cache.Files.TryGetValue(relativePath, out var cached)) return false;
        if (cached.Checksum != currentChecksum) return false;
        entry = cached;
        return true;
    }

    public void Set(string relativePath, AnalysisCacheEntry entry)
    {
        _cache.Files[relativePath] = entry;
        _dirty = true;
    }

    public void SaveIfDirty()
    {
        if (!_dirty) return;
        var json = JsonSerializer.Serialize(_cache, JsonOptions);
        File.WriteAllText(_cachePath, json, Encoding.UTF8);
    }

    private static string BuildCacheFileName(string solutionPath, string rulesJsonContent)
    {
        var solutionName = Path.GetFileNameWithoutExtension(solutionPath);
        var hashInput = solutionPath.ToLowerInvariant() + rulesJsonContent;
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(hashInput));
        var hash8 = Convert.ToHexString(hashBytes)[..8].ToLowerInvariant();
        return $"{solutionName}-{hash8}.json";
    }

    private static AnalysisCacheFile? TryReadCache(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            var file = JsonSerializer.Deserialize<AnalysisCacheFile>(json);
            if (file?.SchemaVersion != AnalysisCacheFile.CurrentSchemaVersion) return null;
            return file;
        }
        catch
        {
            return null;
        }
    }
}
```

#### `src/AiNetLinter/Cache/CacheEntryMapper.cs`

Konvertierung zwischen Domain-Typen und Cache-DTOs:

```csharp
#nullable enable
using AiNetLinter.Core;
using AiNetLinter.Models;

namespace AiNetLinter.Cache;

internal static class CacheEntryMapper
{
    public static RuleViolation ToViolation(RuleViolationDto dto) => new()
    {
        FilePath = dto.FilePath,
        LineNumber = dto.LineNumber,
        RuleName = dto.RuleName,
        Details = dto.Details,
        Guidance = dto.Guidance,
    };

    public static ClassInfo ToClassInfo(ClassInfoDto dto) => new()
    {
        Name = dto.Name,
        FilePath = dto.FilePath,
        LineNumber = dto.LineNumber,
        MaxCognitiveComplexity = dto.MaxCognitiveComplexity,
        InheritanceDepth = dto.InheritanceDepth,
        AIContextFootprint = dto.AiContextFootprint,
        AIContextFootprintDetails = dto.AiContextFootprintDetails
            .Select(d => (d.Name, d.Lines)).ToArray(),
        HasTestMethods = dto.HasTestMethods,
        IsPartial = dto.IsPartial,
        IsStatic = dto.IsStatic,
        BaseTypeNames = dto.BaseTypeNames.ToArray(),
        ProjectName = dto.ProjectName,
    };

    public static PartialClassPart ToPartialPart(PartialPartDto dto) =>
        new(dto.TypeName, dto.FilePath, dto.LineNumber, dto.FileLineCount);

    public static void RestoreToState(AnalysisCacheEntry entry, AnalysisState state)
    {
        foreach (var v in entry.Violations)
            state.Violations.Add(ToViolation(v));

        foreach (var c in entry.Classes)
        {
            var cls = ToClassInfo(c);
            state.SourceClasses.Add(cls);
        }

        foreach (var p in entry.PartialParts)
            state.PartialClassParts.Add(ToPartialPart(p));

        var signals = entry.TestSignals;
        foreach (var n in signals.TestClassNames)
            state.TestCoverage.AddTestClass(n);
        foreach (var n in signals.ReferencedTypeNames)
            state.TestCoverage.AddReferencedType(n);
        foreach (var n in signals.CoversComments)
            state.TestCoverage.AddCoversComment(n);
    }

    public static AnalysisCacheEntry BuildEntry(
        string relativePath,
        string checksum,
        LinterAnalyzer analyzer,
        IEnumerable<PartialClassPart> partialParts,
        TestSignalsDto testSignals)
    {
        return new AnalysisCacheEntry
        {
            RelativePath = relativePath,
            Checksum = checksum,
            Violations = analyzer.Violations.Select(v => new RuleViolationDto(
                v.FilePath, v.LineNumber, v.RuleName, v.Details, v.Guidance)).ToArray(),
            Classes = analyzer.Classes.Select(c => new ClassInfoDto(
                c.Name, c.FilePath, c.LineNumber,
                c.MaxCognitiveComplexity, c.InheritanceDepth, c.AIContextFootprint,
                c.AIContextFootprintDetails.Select(d => new FootprintDetailDto(d.Name, d.Lines)).ToArray(),
                c.HasTestMethods, c.IsPartial, c.IsStatic,
                c.BaseTypeNames.ToArray(), c.ProjectName)).ToArray(),
            PartialParts = partialParts.Select(p =>
                new PartialPartDto(p.TypeName, p.FilePath, p.LineNumber, p.FileLineCount)).ToArray(),
            TestSignals = testSignals,
        };
    }
}
```

### Geänderte Dateien

#### `src/AiNetLinter/Cli/LinterArgs.cs` — neues Property

```csharp
/// <summary>
/// Deaktiviert den Analyse-Cache (erzwingt vollständige Neu-Analyse aller Dateien).
/// </summary>
public bool NoCache { get; init; }
```

#### `src/AiNetLinter/Cli/CliCommandBuilder.cs` — neues CLI-Flag

```csharp
var noCacheOption = new Option<bool>("--no-cache",
    "Cache deaktivieren — erzwingt vollständige Neu-Analyse aller Dateien.");
command.AddOption(noCacheOption);

// Im Handler:
NoCache = ctx.ParseResult.GetValueForOption(noCacheOption),
```

#### `src/AiNetLinter/Core/LinterEngine.cs` — Cache-Integration

`RunInternalAsync` erhält einen optionalen `AnalysisCacheManager?`:

```csharp
private async Task<IReadOnlyCollection<RuleViolation>> RunInternalAsync(
    Solution solution, SourceFileCatalog? catalog, AnalysisCacheManager? cache)
{
    var state = CreateAnalysisState(solution);
    await AnalyzeSolutionAsync(state, catalog, cache);
    PostAnalysisChecks.Run(state, _config);
    cache?.SaveIfDirty();
    return state.Violations.ToArray();
}
```

`AnalyzeDocumentAsync` — Cache-Lookup vor Roslyn:

```csharp
private async Task AnalyzeDocumentAsync(
    Document document, bool isTestProj, AnalysisState state,
    AnalysisCacheManager? cache, string outputRoot)
{
    var filePath = document.FilePath ?? document.Name;
    if (FileFilterEvaluator.IsExcluded(filePath, _config.FileFilters)) return;

    var relativePath = PathNormalizer.ToRelative(outputRoot, filePath);
    var checksum = FileChecksumCalculator.ComputeSha256Hex(filePath);

    if (cache != null && cache.TryGet(relativePath, checksum, out var cached))
    {
        CacheEntryMapper.RestoreToState(cached!, state);
        return;  // Roslyn wird nicht aufgerufen
    }

    // --- Vollanalyse (Cache-Miss oder Cache deaktiviert) ---
    var semanticModel = await document.GetSemanticModelAsync();
    if (semanticModel == null) return;

    var sourceText = await document.GetTextAsync();
    state.FileContents[filePath] = sourceText.ToString();

    bool isTestFile = isTestProj || IsTestFile(filePath);
    var effectiveConfig = ProjectConfigResolver.ResolveForDocument(document, _config);
    var context = new DocumentContext(filePath, semanticModel, isTestFile, effectiveConfig, document.Project.Name);

    var analyzer = new LinterAnalyzer(context.FilePath, context.SemanticModel,
        context.EffectiveConfig, context.IsTestFile, context.ProjectName);
    analyzer.RunAnalysis();

    CollectAnalyzerResults(analyzer, context, state);

    if (cache != null)
    {
        var testSignals = BuildTestSignals(analyzer, semanticModel, effectiveConfig);
        var partialParts = analyzer.PartialClassParts;
        cache.Set(relativePath, CacheEntryMapper.BuildEntry(
            relativePath, checksum, analyzer, partialParts, testSignals));
    }
}
```

`RunAsync(string path)` — Cache erzeugen:

```csharp
public async Task<IReadOnlyCollection<RuleViolation>> RunAsync(string path, bool noCache = false)
{
    using var catalog = await SourceFileCatalog.LoadAsync(path);
    var cache = noCache ? null : BuildCache(catalog, path);
    return await RunInternalAsync(catalog.Solution, catalog, cache);
}

private AnalysisCacheManager? BuildCache(SourceFileCatalog catalog, string path)
{
    var exeDir = Path.GetDirectoryName(
        System.Reflection.Assembly.GetExecutingAssembly().Location)!;
    var solutionPath = catalog.Solution.FilePath ?? path;
    var rulesContent = LoadRulesJsonContent();
    return AnalysisCacheManager.Load(exeDir, solutionPath, rulesContent);
}
```

## Reihenfolge der Umsetzung

1. `AnalysisCacheEntry`, `AnalysisCacheFile`, `AnalysisCacheManager` anlegen und unit-testen
2. `CacheEntryMapper` anlegen — Konvertierungs-Tests mit realen `ClassInfo`-Instanzen
3. `LinterArgs.NoCache` + CLI-Flag hinzufügen
4. `LinterEngine` integrieren (Cache-Pfad als optionaler Parameter)
5. Integrations-Test: zwei aufeinanderfolgende Runs auf demselben Pfad — zweiter Run
   darf `GetSemanticModelAsync` für unveränderte Dateien nicht aufrufen (via Mock oder
   eigenes Zähler-Flag im Engine)
6. Performance-Messung mit `--performance-log` gegen San.smart.Planner.Platform — Ziel: < 6 s
