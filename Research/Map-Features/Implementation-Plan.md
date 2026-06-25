# Map-Features — Implementierungsplan

**Erstellt:** 2026-06-25  
**Status:** Vorschlag — noch nicht implementiert

---

## 1. Intent & Motivation

### Das Problem

Die Eval-Prompts in `Research\Evals\` (E02, E03) brauchen strukturierte Evidence aus der Codebase:
- E02 braucht eine Liste aller Typ-Identifiers (Klassen, Interfaces, Records)
- E03 braucht die Verzeichnisstruktur mit Dateigrößen

Aktuell muss der Nutzer dafür manuell PowerShell-Befehle ausführen (`rg`, `Get-ChildItem`). Das ist:
- Fehleranfällig (bin/obj nicht ausgefiltert, Encoding-Probleme)
- Inkonsistent (jeder macht es anders)
- Nicht agentisch erkundbar

### Die Lösung

Drei neue `--map <typ>` Befehle, die **direkt copy-pasteable Markdown-Output** für Eval-Prompts erzeugen:

| Befehl | Eval | Frage |
|---|---|---|
| `--map vocabulary` | E02 | Haben sich Domain-Begriffe vom Spec-Vokabular entfernt? |
| `--map structure` | E03 | Entspricht die Struktur noch dem Design-Intent? |
| `--map hotspots` | Neu | Welche Dateien nähern sich ihren Grenzen? |

### Agentische Erkundbarkeit

AiNetLinter ist vollständig agentisch erkundbar — kein Netz, kein Lesen von Source:
```
ainetlinter --list-rules              → Regelübersicht
ainetlinter --describe-rule <Id>      → Regeldetail
ainetlinter --docs agent-api          → CLI-Referenz
ainetlinter --map vocabulary --path . → Typ-Landkarte  ← NEU
ainetlinter --map structure  --path . → Struktur-Landkarte  ← NEU
ainetlinter --map hotspots   --path . → Hotspot-Landkarte  ← NEU
```

Die neuen Befehle müssen in `--docs agent-api` dokumentiert sein.

---

## 2. CLI-Interface

### Neuer Parameter

```
--map <typ>   Codebase-Landkarte generieren. Erfordert --path.
              Typen: vocabulary | structure | hotspots
```

Kombination mit bestehendem `--path`:
```powershell
ainetlinter --map vocabulary --path .\src\MeinProjekt.slnx
ainetlinter --map structure  --path .\src\
ainetlinter --map hotspots   --path .\src\MeinProjekt.slnx --config .\rules.json
```

Hinweis: `hotspots` profitiert von `--config` (kennt dann die konfigurierten Limits), kann aber auch ohne laufen (nutzt dann Defaults).

### Error-Verhalten

```
[ERROR]: CONFIG_REQUIRED: --path fehlt für --map
  context: --map vocabulary
  hint:    Pfad zur Solution oder zum Verzeichnis mit --path angeben.

[ERROR]: RESOURCE_NOT_FOUND: Unbekannter Map-Typ 'foo'
  context: --map foo
  hint:    Gültige Typen: vocabulary, structure, hotspots
```

---

## 3. Architektur

### Neue Dateien

```
src/AiNetLinter/
  Commands/
    MapCommand.cs              ← Routing + Dispatch
  Maps/
    VocabularyMapBuilder.cs    ← --map vocabulary
    StructureMapBuilder.cs     ← --map structure
    HotspotMapBuilder.cs       ← --map hotspots

src/AiNetLinter.Tests/
  Maps/
    VocabularyMapBuilderTests.cs
    StructureMapBuilderTests.cs
    HotspotMapBuilderTests.cs
```

### Bestehende Dateien mit Änderungen

| Datei | Änderung |
|---|---|
| `Cli/CliOptions.cs` | Property `MapType` hinzufügen |
| `Cli/LinterArgs.cs` (oder Äquivalent) | `MapType` propagieren |
| `Program.cs` / `AppShell.cs` | Dispatch zu `MapCommand` |
| `Docs/agent-api.md` | Neue Flags + Beispiele |
| `Docs/configuration.md` | Map-Sektion |
| `README.md` | Map-Feature erwähnen |

### Kein Roslyn für vocabulary und structure

`VocabularyMapBuilder` und `StructureMapBuilder` brauchen **kein Roslyn-Workspace** — sie arbeiten direkt auf dem Dateisystem. Das hat zwei Vorteile:
1. Schnell (kein MSBuild-Load)
2. Funktioniert auch ohne Solution-Datei (reines Verzeichnis)

`HotspotMapBuilder` nutzt Roslyn **optional** wenn eine Solution geladen ist, und fällt auf Dateigrößen zurück wenn nicht.

---

## 4. Detaildesign: `--map vocabulary`

### Zweck

Listet alle Typ-Deklarationen aus dem Produktionscode, gruppiert nach Suffix-Muster. Dient als direkter Input für Eval-Prompt E02.

### Algorithmus

1. Alle `.cs`-Dateien unter `--path` finden, `bin/` und `obj/` ausschließen
2. Jede Datei mit Regex nach Typ-Deklarationen scannen
3. Typ-Namen extrahieren und nach PascalCase-Segmenten analysieren
4. Gruppen nach letztem Segment (Suffix) bilden
5. Markdown ausgeben

### Regex für Typ-Extraktion

```csharp
// Erkennt: public/internal/private/protected + optional sealed/static/abstract
// + class/interface/record/enum + Name
private static readonly Regex TypeDeclarationPattern = new(
    @"^\s*(public|internal|private|protected)\s+(sealed\s+|static\s+|abstract\s+)?"
    + @"(class|interface|record|enum)\s+(?<name>\w+)",
    RegexOptions.Multiline | RegexOptions.Compiled);
```

### Output-Format

```markdown
# AiNetLinter — Vocabulary Map

Gescannt: 47 .cs-Dateien | 89 Typ-Deklarationen | Pfad: src/AiNetLinter

## Typ-Gruppen nach Suffix

### *Checker (23)
| Typ | Datei |
|:---|:---|
| AsyncVoidChecker | Checkers/AsyncVoidChecker.cs |
| BoolParameterChecker | Checkers/BoolParameterChecker.cs |
| ComplexityChecker | Checkers/ComplexityChecker.cs |
... (alle)

### *Detector (4)
| Typ | Datei |
|:---|:---|
| GeneratedCodeDetector | Checkers/GeneratedCodeDetector.cs |
| TestProjectDetector | Sentinel/TestProjectDetector.cs |
...

### *Builder (3)
...

### *Command (9)
...

### *Scanner (1)
...

### Ohne Suffix-Muster (N)
Typen ohne erkennbares Kategorisierungs-Suffix:
| Typ | Datei |
|:---|:---|
| Program | Program.cs |
| LinterEngine | Core/LinterEngine.cs |
...

## Suffix-Statistik

| Suffix | Anzahl | Anteil |
|:---|---:|---:|
| Checker | 23 | 26 % |
| Command | 9 | 10 % |
| Builder | 3 | 3 % |
| Detector | 4 | 4 % |
| Scanner | 1 | 1 % |
| (kein Suffix) | 49 | 55 % |

## Hinweise

⚠ Gemischte Patterns für Prüf-Klassen: Checker (23), Detector (4), Scanner (1)
  → Potenzieller Naming-Drift. Prüfen ob diese Unterscheidung intentional ist.
```

### Wichtige Details

- **Testdateien ausschließen:** Dateien unter Pfaden mit `Tests` oder `Test` im Projektnamen nicht mitzählen (oder als separate Sektion)
- **Kein Roslyn:** Reine Datei-Scan-Lösung → kein MSBuild-Load, läuft in <1 Sekunde
- **Suffix-Erkennung:** PascalCase-Splitting: `AsyncVoidChecker` → `["Async", "Void", "Checker"]` → letztes Segment = Suffix

---

## 5. Detaildesign: `--map structure`

### Zweck

Gibt Verzeichnisstruktur mit Dateigrößen aus. Dient als direkter Input für Eval-Prompt E03.

### Algorithmus

1. Alle `.cs`-Dateien unter `--path` finden, `bin/` und `obj/` ausschließen
2. Nach Verzeichnis gruppieren
3. LOC zählen (Zeilenzahl)
4. Markdown ausgeben

### Output-Format

```markdown
# AiNetLinter — Structure Map

Gescannt: 47 .cs-Dateien | 4.231 Zeilen gesamt | Pfad: src/AiNetLinter

## Verzeichnis-Übersicht

| Verzeichnis | Dateien | Zeilen |
|:---|---:|---:|
| Commands/ | 9 | 423 |
| Core/Checkers/ | 24 | 1.847 |
| Core/ | 3 | 201 |
| Maps/ | 3 | 289 |
| Metrics/ | 2 | 198 |
| Models/ | 8 | 312 |
| Output/ | 4 | 389 |
| Suppression/ | 7 | 412 |
| (Root) | 2 | 160 |

## Alle Dateien (sortiert nach Größe)

| Datei | Zeilen | Warnung |
|:---|---:|:---|
| Core/Checkers/RuleRegistry.cs | 483 | ⚠ >80% von 500 |
| Core/Checkers/RuleRegistry.General.cs | 446 | ⚠ >80% von 500 |
| Suppression/SuppressionFileResolver.cs | 90 | ✓ |
| Commands/ListRulesCommand.cs | 110 | ✓ |
... (alle Dateien)

## Hotspot-Verzeichnisse (>50% der Dateien nahe am Limit)

Keine kritischen Verzeichnisse gefunden.
```

### Limit-Erkennung

Wenn `--config` angegeben: `MaxLineCount` aus `rules.json` als Limit verwenden.  
Ohne `--config`: Default 500 (aus den eingebetteten Defaults).

Schwellenwerte für Warnungen:
- `>80% des Limits` → ⚠ Warnung
- `>95% des Limits` → 🔴 Kritisch

---

## 6. Detaildesign: `--map hotspots`

### Zweck

Zeigt Dateien und Verzeichnisse die sich dem konfigurierten Limit nähern. Proaktives Drift-Signal: Wachstum vor dem Regelverstoß sichtbar machen.

### Unterschied zu `--map structure`

`structure` zeigt alle Dateien. `hotspots` zeigt nur die **gefährdeten** — als fokussierter Alert.

### Output-Format

```markdown
# AiNetLinter — Hotspot Map

Gescannt: 47 .cs-Dateien | Limit: MaxLineCount = 500 | Pfad: src/AiNetLinter

## Kritische Dateien (>95% des Limits)

Keine.

## Warnungs-Dateien (>80% des Limits)

| Datei | Zeilen | Auslastung | Verbleibend |
|:---|---:|---:|---:|
| Core/Checkers/RuleRegistry.cs | 483 | 97 % | 17 Zeilen |
| Core/Checkers/RuleRegistry.General.cs | 446 | 89 % | 54 Zeilen |

## Wachstums-Warnung

RuleRegistry.cs hat 483 Zeilen (97% des Limits).
Wenn diese Datei noch 2 Klassen hinzubekommt, wird das Limit überschritten.
Empfehlung: Aufteilen in thematische Sub-Registries.

## Alle anderen Dateien: im grünen Bereich
```

### Erweiterung für v2 (mit Roslyn)

In v2 könnte `hotspots` auch Methoden-Komplexität anzeigen:
- Methoden mit `MaxCyclomaticComplexity` > 80% des Limits
- Klassen mit `MaxAIContextFootprint` > 80% des Limits

Das erfordert einen Roslyn-Workspace-Load. Für v1 reicht Dateigrößen-Analyse.

---

## 7. Konkrete Code-Vorschläge

### 7.1 `MapCommand.cs`

```csharp
#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Cli;
using AiNetLinter.Configuration;
using AiNetLinter.Maps;
using AiNetLinter.Output;

namespace AiNetLinter.Commands;

/// <summary>
/// Generiert Codebase-Landkarten für Drift-Erkennung und Eval-Prompts.
/// </summary>
internal static class MapCommand
{
    internal static async Task<int> RunAsync(
        LinterArgs args,
        CancellationToken ct = default,
        ILintConsole? console = null)
    {
        var c = console ?? ConsoleLintConsole.Instance;

        if (string.IsNullOrWhiteSpace(args.TargetPath))
        {
            c.WriteError("[ERROR]: CONFIG_REQUIRED: --path fehlt für --map\n"
                + "  hint: Pfad zur Solution oder Verzeichnis mit --path angeben.");
            return 1;
        }

        var mapType = args.MapType?.ToLowerInvariant();

        return mapType switch
        {
            "vocabulary" => VocabularyMapBuilder.Build(args.TargetPath, c),
            "structure"  => StructureMapBuilder.Build(args.TargetPath, ResolveMaxLineCount(args), c),
            "hotspots"   => HotspotMapBuilder.Build(args.TargetPath, ResolveMaxLineCount(args), c),
            _ => ReportUnknownType(mapType, c)
        };
    }

    private static int ResolveMaxLineCount(LinterArgs args)
    {
        if (string.IsNullOrWhiteSpace(args.ConfigPath))
            return LinterConfig.DefaultMaxLineCount;

        var config = LinterConfigLoader.TryLoadConfig(args.ConfigPath, isRequired: false);
        return config?.Metrics.MaxLineCount ?? LinterConfig.DefaultMaxLineCount;
    }

    private static int ReportUnknownType(string? mapType, ILintConsole c)
    {
        c.WriteError($"[ERROR]: RESOURCE_NOT_FOUND: Unbekannter Map-Typ '{mapType}'\n"
            + "  hint: Gültige Typen: vocabulary, structure, hotspots");
        return 1;
    }
}
```

### 7.2 `VocabularyMapBuilder.cs`

```csharp
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AiNetLinter.Output;

namespace AiNetLinter.Maps;

/// <summary>
/// Erzeugt eine Vocabulary Map: Typ-Deklarationen gruppiert nach Suffix-Muster.
/// Dient als direkter Input für Eval-Prompt E02 (Naming-Drift-Audit).
/// </summary>
internal static class VocabularyMapBuilder
{
    private static readonly Regex TypePattern = new(
        @"^\s*(public|internal|private|protected)\s+(sealed\s+|static\s+|abstract\s+)?"
        + @"(class|interface|record|enum)\s+(?<name>\w+)",
        RegexOptions.Multiline | RegexOptions.Compiled);

    internal static int Build(string targetPath, ILintConsole c)
    {
        var csFiles = CollectCsFiles(targetPath);
        var entries = ExtractTypeEntries(csFiles, targetPath);
        var grouped = GroupBySuffix(entries);

        c.WriteLine(BuildMarkdown(entries, grouped, targetPath));
        return 0;
    }

    internal static IReadOnlyList<TypeEntry> ExtractTypeEntries(
        IEnumerable<string> files, string rootPath)
    {
        var entries = new List<TypeEntry>();
        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            var relativePath = Path.GetRelativePath(rootPath, file).Replace('\\', '/');
            var isTest = relativePath.Contains("/Tests/") || relativePath.Contains(".Tests/");

            foreach (Match match in TypePattern.Matches(content))
            {
                entries.Add(new TypeEntry(
                    Name: match.Groups["name"].Value,
                    RelativePath: relativePath,
                    IsTest: isTest));
            }
        }
        return entries;
    }

    private static IReadOnlyDictionary<string, List<TypeEntry>> GroupBySuffix(
        IReadOnlyList<TypeEntry> entries)
    {
        var result = new Dictionary<string, List<TypeEntry>>(StringComparer.Ordinal);

        foreach (var entry in entries.Where(e => !e.IsTest))
        {
            var suffix = ExtractSuffix(entry.Name);
            if (!result.TryGetValue(suffix, out var list))
            {
                list = [];
                result[suffix] = list;
            }
            list.Add(entry);
        }

        return result;
    }

    internal static string ExtractSuffix(string typeName)
    {
        // PascalCase-Splitting: "AsyncVoidChecker" → ["Async", "Void", "Checker"]
        // Letztes Segment ab 4 Zeichen = Suffix-Kandidat
        var segments = SplitPascalCase(typeName);
        if (segments.Length >= 2)
        {
            var last = segments[^1];
            if (last.Length >= 4)
                return last;
        }
        return "(kein Suffix)";
    }

    private static string[] SplitPascalCase(string name)
    {
        var parts = new List<string>();
        var current = new StringBuilder();

        foreach (var ch in name)
        {
            if (char.IsUpper(ch) && current.Length > 0)
            {
                parts.Add(current.ToString());
                current.Clear();
            }
            current.Append(ch);
        }
        if (current.Length > 0)
            parts.Add(current.ToString());

        return [.. parts];
    }

    private static string BuildMarkdown(
        IReadOnlyList<TypeEntry> all,
        IReadOnlyDictionary<string, List<TypeEntry>> grouped,
        string targetPath)
    {
        var prodTypes = all.Where(e => !e.IsTest).ToList();
        var sb = new StringBuilder();

        sb.AppendLine("# AiNetLinter — Vocabulary Map");
        sb.AppendLine();
        sb.AppendLine($"Gescannt: {all.Count(e => !e.IsTest)} Produktions-Typen"
            + $" | {all.Count(e => e.IsTest)} Test-Typen"
            + $" | Pfad: {targetPath}");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Typ-Gruppen nach Suffix (Produktionscode)");

        foreach (var (suffix, types) in grouped.OrderByDescending(kv => kv.Value.Count))
        {
            sb.AppendLine();
            sb.AppendLine($"### *{suffix} ({types.Count})");
            sb.AppendLine();
            sb.AppendLine("| Typ | Datei |");
            sb.AppendLine("|:---|:---|");
            foreach (var t in types.OrderBy(t => t.Name))
                sb.AppendLine($"| {t.Name} | {t.RelativePath} |");
        }

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Suffix-Statistik");
        sb.AppendLine();
        sb.AppendLine("| Suffix | Anzahl | Anteil |");
        sb.AppendLine("|:---|---:|---:|");
        foreach (var (suffix, types) in grouped.OrderByDescending(kv => kv.Value.Count))
        {
            var pct = (double)types.Count / prodTypes.Count * 100;
            sb.AppendLine($"| {suffix} | {types.Count} | {pct:F0} % |");
        }

        AppendHints(grouped, sb);

        return sb.ToString().TrimEnd();
    }

    private static void AppendHints(
        IReadOnlyDictionary<string, List<TypeEntry>> grouped, StringBuilder sb)
    {
        // Verwandte Suffixe mit potenziell überlappender Bedeutung
        var checkerLike = new[] { "Checker", "Detector", "Scanner", "Analyzer", "Validator" };
        var found = checkerLike
            .Where(s => grouped.ContainsKey(s))
            .Select(s => $"{s} ({grouped[s].Count})")
            .ToList();

        if (found.Count > 1)
        {
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## ⚠ Hinweise");
            sb.AppendLine();
            sb.AppendLine($"Gemischte Patterns für Prüf-Klassen: {string.Join(", ", found)}");
            sb.AppendLine("→ Prüfen ob diese Unterscheidung intentional ist (z.B. Checker = erzeugt Violations, Detector = identifiziert Zustände).");
        }
    }

    private static IEnumerable<string> CollectCsFiles(string targetPath) =>
        Directory.EnumerateFiles(
            Directory.Exists(targetPath) ? targetPath : Path.GetDirectoryName(targetPath)!,
            "*.cs",
            SearchOption.AllDirectories)
        .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                 && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

    internal sealed record TypeEntry(string Name, string RelativePath, bool IsTest);
}
```

### 7.3 `StructureMapBuilder.cs` (Skelett)

```csharp
#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AiNetLinter.Output;

namespace AiNetLinter.Maps;

/// <summary>
/// Erzeugt eine Structure Map: Verzeichnisstruktur mit Dateigrößen.
/// Dient als direkter Input für Eval-Prompt E03 (Architecture-Intent-Audit).
/// </summary>
internal static class StructureMapBuilder
{
    internal static int Build(string targetPath, int maxLineCount, ILintConsole c)
    {
        var root = ResolveRoot(targetPath);
        var files = CollectFileInfos(root);
        c.WriteLine(BuildMarkdown(files, root, maxLineCount));
        return 0;
    }

    internal sealed record FileInfo(string RelativePath, int Lines, string Directory);

    internal static IReadOnlyList<FileInfo> CollectFileInfos(string root)
    {
        return Directory
            .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Select(f => new FileInfo(
                RelativePath: Path.GetRelativePath(root, f).Replace('\\', '/'),
                Lines: File.ReadAllLines(f).Length,
                Directory: Path.GetRelativePath(root, Path.GetDirectoryName(f)!).Replace('\\', '/')))
            .OrderByDescending(f => f.Lines)
            .ToList();
    }

    private static string BuildMarkdown(
        IReadOnlyList<FileInfo> files, string root, int maxLineCount)
    {
        var sb = new StringBuilder();
        var totalLines = files.Sum(f => f.Lines);

        sb.AppendLine("# AiNetLinter — Structure Map");
        sb.AppendLine();
        sb.AppendLine($"Gescannt: {files.Count} .cs-Dateien"
            + $" | {totalLines:N0} Zeilen gesamt"
            + $" | MaxLineCount: {maxLineCount}"
            + $" | Pfad: {root}");
        sb.AppendLine();

        // Verzeichnis-Übersicht
        var byDir = files
            .GroupBy(f => string.IsNullOrEmpty(f.Directory) ? "(Root)" : f.Directory)
            .OrderByDescending(g => g.Sum(f => f.Lines));

        sb.AppendLine("## Verzeichnis-Übersicht");
        sb.AppendLine();
        sb.AppendLine("| Verzeichnis | Dateien | Zeilen |");
        sb.AppendLine("|:---|---:|---:|");
        foreach (var dir in byDir)
            sb.AppendLine($"| {dir.Key}/ | {dir.Count()} | {dir.Sum(f => f.Lines):N0} |");

        // Alle Dateien mit Warnstufe
        sb.AppendLine();
        sb.AppendLine("## Alle Dateien (sortiert nach Größe)");
        sb.AppendLine();
        sb.AppendLine("| Datei | Zeilen | Status |");
        sb.AppendLine("|:---|---:|:---|");
        foreach (var f in files)
        {
            var pct = (double)f.Lines / maxLineCount;
            var status = pct >= 0.95 ? "🔴 Kritisch" : pct >= 0.80 ? "⚠ Warnung" : "✓";
            sb.AppendLine($"| {f.RelativePath} | {f.Lines} | {status} |");
        }

        return sb.ToString().TrimEnd();
    }

    private static string ResolveRoot(string targetPath) =>
        Directory.Exists(targetPath)
            ? targetPath
            : Path.GetDirectoryName(targetPath) ?? targetPath;
}
```

### 7.4 `HotspotMapBuilder.cs` (Skelett)

```csharp
#nullable enable

using System.IO;
using System.Linq;
using System.Text;
using AiNetLinter.Output;

namespace AiNetLinter.Maps;

/// <summary>
/// Erzeugt eine Hotspot Map: Dateien die sich ihrem konfigurierten Limit nähern.
/// Proaktives Drift-Signal — sichtbar bevor ein Regelverstoß entsteht.
/// </summary>
internal static class HotspotMapBuilder
{
    private const double WarnThreshold     = 0.80;
    private const double CriticalThreshold = 0.95;

    internal static int Build(string targetPath, int maxLineCount, ILintConsole c)
    {
        var root = Directory.Exists(targetPath) ? targetPath : Path.GetDirectoryName(targetPath)!;
        var files = StructureMapBuilder.CollectFileInfos(root);
        var critical = files.Where(f => (double)f.Lines / maxLineCount >= CriticalThreshold).ToList();
        var warning  = files.Where(f => (double)f.Lines / maxLineCount is >= WarnThreshold and < CriticalThreshold).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("# AiNetLinter — Hotspot Map");
        sb.AppendLine();
        sb.AppendLine($"Gescannt: {files.Count} .cs-Dateien | MaxLineCount: {maxLineCount} | Pfad: {root}");
        sb.AppendLine();

        AppendSection(sb, "🔴 Kritische Dateien (>95% des Limits)", critical, maxLineCount);
        AppendSection(sb, "⚠ Warnungs-Dateien (>80% des Limits)", warning, maxLineCount);

        if (critical.Count == 0 && warning.Count == 0)
        {
            sb.AppendLine("## ✓ Alle Dateien im grünen Bereich");
            sb.AppendLine();
            sb.AppendLine($"Keine Datei überschreitet 80% des Limits ({(int)(maxLineCount * WarnThreshold)} Zeilen).");
        }

        sb.AppendLine();
        sb.AppendLine($"## Alle anderen Dateien: {files.Count - critical.Count - warning.Count} Dateien im grünen Bereich");

        c.WriteLine(sb.ToString().TrimEnd());
        return 0;
    }

    private static void AppendSection(
        StringBuilder sb,
        string heading,
        System.Collections.Generic.IReadOnlyList<StructureMapBuilder.FileInfo> files,
        int maxLineCount)
    {
        sb.AppendLine($"## {heading}");
        sb.AppendLine();

        if (files.Count == 0)
        {
            sb.AppendLine("Keine.");
            sb.AppendLine();
            return;
        }

        sb.AppendLine("| Datei | Zeilen | Auslastung | Verbleibend |");
        sb.AppendLine("|:---|---:|---:|---:|");
        foreach (var f in files.OrderByDescending(x => x.Lines))
        {
            var pct = (double)f.Lines / maxLineCount * 100;
            var remaining = maxLineCount - f.Lines;
            sb.AppendLine($"| {f.RelativePath} | {f.Lines} | {pct:F0} % | {remaining} Zeilen |");
        }
        sb.AppendLine();
    }
}
```

### 7.5 CLI-Integration (Änderungen)

**In `CliOptions.cs`** — neue Property:
```csharp
[Option("map", HelpText = "Codebase-Landkarte: vocabulary | structure | hotspots")]
public string? MapType { get; init; }
```

**In `AppShell.cs` / `Program.cs`** — Dispatch:
```csharp
// Nach den bestehenden Discovery-Befehlen:
if (args.MapType != null)
    return await MapCommand.RunAsync(args, ct, console);
```

**In `LinterArgs.cs`** (falls separates Mapping-Record existiert):
```csharp
public string? MapType { get; init; }
// + Mapping aus CliOptions
```

---

## 8. Test-Strategie

### Prinzip

Tests arbeiten mit temporären Verzeichnissen die minimale `.cs`-Testdateien enthalten. Keine Roslyn-Abhängigkeit, kein MSBuild.

### `VocabularyMapBuilderTests.cs`

```csharp
public sealed class VocabularyMapBuilderTests
{
    [Fact]
    public void ExtractSuffix_PascalCaseWithSuffix_ReturnsLastSegment()
    {
        Assert.Equal("Checker", VocabularyMapBuilder.ExtractSuffix("AsyncVoidChecker"));
        Assert.Equal("Builder", VocabularyMapBuilder.ExtractSuffix("StructureMapBuilder"));
        Assert.Equal("Detector", VocabularyMapBuilder.ExtractSuffix("GeneratedCodeDetector"));
    }

    [Fact]
    public void ExtractSuffix_ShortSingleWord_ReturnsNoSuffix()
    {
        Assert.Equal("(kein Suffix)", VocabularyMapBuilder.ExtractSuffix("Program"));
        Assert.Equal("(kein Suffix)", VocabularyMapBuilder.ExtractSuffix("Foo"));
    }

    [Fact]
    public void ExtractTypeEntries_SimpleClass_ExtractsName()
    {
        var tempDir = CreateTempDir(("TestClass.cs", "public sealed class MyChecker {}"));
        var entries = VocabularyMapBuilder.ExtractTypeEntries(
            Directory.EnumerateFiles(tempDir, "*.cs"), tempDir);
        Assert.Single(entries);
        Assert.Equal("MyChecker", entries[0].Name);
    }

    [Fact]
    public void Build_WithMixedCheckerDetector_EmitsHint()
    {
        var tempDir = CreateTempDir(
            ("A.cs", "public sealed class FooChecker {}"),
            ("B.cs", "internal sealed class BarDetector {}"));
        var console = new TestLintConsole();
        VocabularyMapBuilder.Build(tempDir, console);
        Assert.Contains("Gemischte Patterns", console.Output);
    }

    [Fact]
    public void Build_ExcludesBinAndObj()
    {
        // Dateien unter bin/ dürfen nicht auftauchen
        var tempDir = Path.GetTempPath();
        var binFile = Path.Combine(tempDir, "bin", "Generated.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(binFile)!);
        File.WriteAllText(binFile, "public class ShouldNotAppear {}");

        var mainFile = Path.Combine(tempDir, "Real.cs");
        File.WriteAllText(mainFile, "public sealed class RealClass {}");

        var entries = VocabularyMapBuilder.ExtractTypeEntries(
            VocabularyMapBuilder.CollectCsFilesPublic(tempDir), tempDir);

        Assert.DoesNotContain(entries, e => e.Name == "ShouldNotAppear");
        // Cleanup...
    }
}
```

### `StructureMapBuilderTests.cs`

```csharp
public sealed class StructureMapBuilderTests
{
    [Fact]
    public void CollectFileInfos_CountsLinesCorrectly()
    {
        var tempDir = CreateTempDir(("Test.cs", "line1\nline2\nline3"));
        var infos = StructureMapBuilder.CollectFileInfos(tempDir);
        Assert.Single(infos);
        Assert.Equal(3, infos[0].Lines);
    }

    [Fact]
    public void CollectFileInfos_ExcludesBinAndObj()
    {
        // bin/ und obj/ Dateien erscheinen nicht in der Liste
        ...
    }

    [Fact]
    public void Build_FilesAbove80Percent_ShowsWarning()
    {
        // Datei mit 420 Zeilen bei MaxLineCount=500 → ⚠ Warnung
        ...
    }
}
```

### `HotspotMapBuilderTests.cs`

```csharp
public sealed class HotspotMapBuilderTests
{
    [Fact]
    public void Build_NoHotspots_ShowsGreenMessage()
    {
        var tempDir = CreateTempDir(("Small.cs", "namespace Foo;"));
        var console = new TestLintConsole();
        HotspotMapBuilder.Build(tempDir, 500, console);
        Assert.Contains("grünen Bereich", console.Output);
    }

    [Fact]
    public void Build_CriticalFile_ShowsCriticalSection()
    {
        // 476 Zeilen bei Limit 500 → kritisch (>95%)
        ...
    }
}
```

---

## 9. Dokumentations-Updates

### `Docs/agent-api.md`

In der Sektion **Discovery-Commands** ergänzen:

```markdown
# Map-Befehle (Codebase-Landkarten)

Erzeugen strukturierte Markdown-Outputs die direkt als Evidence in Eval-Prompts
verwendet werden können. Erfordern --path.

ainetlinter --map vocabulary --path <pfad>
ainetlinter --map structure  --path <pfad>
ainetlinter --map hotspots   --path <pfad> [--config <rules.json>]
```

In der **Alle CLI-Flags** Tabelle ergänzen:

| `--map <typ>` | string | Codebase-Landkarte generieren (`vocabulary`, `structure`, `hotspots`) |

### `Docs/configuration.md`

Neue Sektion **Map-Ausgaben**:

```markdown
## Map-Ausgaben

Die `--map`-Befehle erzeugen Markdown-Landkarten der Codebase ohne Lint-Lauf.
Sie benötigen kein `--config` (außer `--map hotspots` für präzise Grenzwerte).

| Befehl | Zweck | Eval-Input |
|---|---|---|
| `--map vocabulary` | Typ-Namen nach Suffix gruppiert | E02 Naming-Drift |
| `--map structure`  | Verzeichnisstruktur + Dateigrößen | E03 Architecture-Intent |
| `--map hotspots`   | Dateien nahe am Limit | Proaktiv |
```

### `README.md`

Im Feature-Überblick:

```markdown
- **Codebase-Landkarten (`--map`):** Vocabulary-, Struktur- und Hotspot-Maps
  für Drift-Erkennung und Eval-Prompts — kein manuelles Grep mehr nötig.
```

### `Research/Evals/E02-Naming-Drift.md` und `E03-Architecture-Intent.md`

Den Evidence-Abschnitt aktualisieren:

```markdown
**IDENTIFIERS:** Mit AiNetLinter direkt generieren (empfohlen):

    ainetlinter --map vocabulary --path <solution-oder-verzeichnis>

Alternativ manuell (PowerShell 7):
    rg ...
```

---

## 10. Nicht-Ziele / Abgrenzung

- **Kein Roslyn für v1** von `vocabulary` und `structure` — Datei-Scan ist ausreichend und deutlich schneller
- **Kein `--map dependencies`** — wird bereits durch `--playbook` (Mermaid-Graph) abgedeckt
- **Kein JSON-Output** — Markdown ist der einzige Output-Typ (konsistent mit allen anderen Befehlen)
- **Kein Caching** der Map-Outputs — sie sind schnell genug ohne Cache
- **Kein Schreiben in Dateien** — alles geht nach stdout; Redirect mit `>` ist Aufgabe des Aufrufers

---

## 11. Umsetzungsreihenfolge

1. `VocabularyMapBuilder` + Tests (kein Roslyn, schnell umsetzbar)
2. `StructureMapBuilder` + Tests (baut auf FileInfo-Pattern auf)
3. `HotspotMapBuilder` + Tests (nutzt `StructureMapBuilder.CollectFileInfos` wieder)
4. `MapCommand.cs` (Routing, trivial)
5. CLI-Integration (`CliOptions`, `AppShell`/`Program`)
6. `Docs/agent-api.md` und `Docs/configuration.md` updaten
7. `Research/Evals/E02` und `E03` Evidence-Abschnitte updaten
8. `README.md` updaten
9. Linter-Lauf auf eigene Implementierung → Zero Violations

---

## 12. Commit-Vorschlag

```
feat: füge --map vocabulary/structure/hotspots Befehle für Drift-Erkennung hinzu

Drei neue Map-Befehle erzeugen Markdown-Landkarten der Codebase:
- --map vocabulary: Typ-Identifier nach Suffix gruppiert (Input für E02)
- --map structure:  Verzeichnisbaum mit Dateigrößen (Input für E03)
- --map hotspots:   Dateien >80%/>95% des MaxLineCount-Limits

Dokumentiert in agent-api.md und configuration.md.
Eval-Prompts E02/E03 verweisen auf --map als primären Evidence-Befehl.
```
