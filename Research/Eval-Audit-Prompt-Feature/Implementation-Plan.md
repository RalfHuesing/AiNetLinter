# Eval-Audit-Prompt-Feature — Implementierungsplan

**Erstellt:** 2026-06-25  
**Status:** Vorschlag — noch nicht implementiert

---

## 1. Intent & Motivation

### Das Problem (Ausgangslage)

Die Eval-Prompts in `Research/Evals/` sind manuelle Markdown-Dokumente mit Platzhaltern wie `[SPEC]`
und `[IDENTIFIERS]`. Um sie zu nutzen, muss der Nutzer:

1. Die richtige PowerShell-Evidenz-Sammlung manuell ausführen
2. Die Ausgabe kopieren und in den richtigen Platzhalter einfügen
3. Die Spezifikation aus verschiedenen Docs-Dateien zusammensuchen
4. Das Ergebnis irgendwie in das LLM-Interface bringen

Das ist fehleranfällig, projektspezifisch nicht wartbar und nicht agentisch nutzbar.

### Die Lösung

Ein neuer `--eval <name>` Befehl der einen **vollständig assemblierten, sofort nutzbaren
Audit-Prompt** ausgibt — inklusive Spezifikation und frisch generierter Evidenz.

```powershell
ainetlinter --eval naming-drift --path .\src --spec .\README.md > prompt.md
# → prompt.md ist der vollständige Prompt, direkt als Cursor-Input nutzbar
```

Der Linter weiß intern welche Evidenz für welchen Eval-Typ notwendig ist. Der Nutzer denkt
nicht in `--map vocabulary` vs `--map structure` — er gibt nur `--eval naming-drift` an.

### Was dabei entfernt wird

- Der gesamte `Research/Evals/`-Ordner (E01–E04, README, Audit-Report) wird **gelöscht**.
  Ersatz: eingebettete Templates unter `Docs/Evals/`.

---

## 2. Entscheidungen (festgelegt)

| Thema | Entscheidung |
|:---|:---|
| Platzhalter-Syntax | `{{DOPPELT_GESCHWUNGEN}}` — unterscheidbar von LLM-Instruktions-Platzhaltern (`[MANUELL]`) |
| Eval-Namen | `naming-drift` (war E02), `architecture-intent` (war E03) |
| E01 / E04 | Werden **gelöscht** — kein Auto-Fill möglich, kein eigenständiger Mehrwert |
| Prompt-Text | Im Binary eingebettet (`Docs/Evals/` als EmbeddedResource) |
| `--spec` | Mehrfach angebbar, akzeptiert Dateien UND Verzeichnisse (erste Ebene, nur `.md`) |
| Spec fehlt | Fallback-Instruktion im Template (LLM fragt den Nutzer selbst) |
| Discovery | `--list-evals` (konsistent mit `--list-rules`) |
| Timestamp-Header | Unterdrückt (wie `--docs`, `--list-rules`, `--map`) |

---

## 3. Gelöschte Dateien

Folgende Dateien werden im Zuge der Implementierung **gelöscht**:

```
Research/Evals/README.md
Research/Evals/E01-Feature-Completeness.md
Research/Evals/E02-Naming-Drift.md
Research/Evals/E03-Architecture-Intent.md
Research/Evals/E04-Behavioral-Regression.md
Research/Evals/E02-Naming-Drift-Report.md
```

Der Ordner `Research/Evals/` wird vollständig entfernt.

---

## 4. Neue Dateien / Geänderte Dateien

### Neue Dateien

```
Docs/Evals/
  naming-drift.md              ← Eingebettetes Template mit Platzhaltern
  architecture-intent.md       ← Eingebettetes Template mit Platzhaltern

src/AiNetLinter/
  Commands/
    EvalCommand.cs             ← --eval Dispatch + Assembly
    ListEvalsCommand.cs        ← --list-evals
  Evals/
    EvalDefinition.cs          ← Record: Name, DisplayName, Description, EvidenceType
    EvalRegistry.cs            ← Statisches Register aller Eval-Typen
    EvalAssembler.cs           ← Kern-Assembly-Logik (Template laden + Platzhalter ersetzen)
    SpecLoader.cs              ← --spec Dateien/Verzeichnisse laden + konkatenieren

src/AiNetLinter.Tests/
  Evals/
    EvalAssemblerTests.cs
    SpecLoaderTests.cs
    ListEvalsCommandTests.cs
```

### Geänderte Dateien

| Datei | Änderung |
|:---|:---|
| `AiNetLinter.csproj` | 2 neue `<EmbeddedResource>` für die Templates |
| `Cli/CliOptions.cs` | 3 neue Properties: `Eval`, `ListEvals`, `Spec` |
| `Cli/CliParsedArgs.cs` | `EvalType`, `ListEvals`, `SpecPaths` |
| `Cli/LinterArgs.cs` | `EvalType`, `ListEvals`, `SpecPaths` |
| `Cli/CliOptionFactory.cs` | 3 neue Methoden: `CreateEvalOption`, `CreateListEvalsOption`, `CreateSpecOption` |
| `Cli/CliCommandBuilder.cs` | Neue Optionen registrieren + parsen |
| `Program.cs` | Dispatch für `--list-evals` (Standalone) + `--eval` + Header-Unterdrückung |
| `Docs/agent-api.md` | Neue Flags + Beispiele |
| `Docs/configuration.md` | Neue Sektion: Eval-Befehle |
| `README.md` | Feature-Erwähnung |
| `Docs/ROADMAP.md` | Epic 31 |

---

## 5. CLI-Interface

### Neue Flags

```
--eval <name>      Assemblierten Eval-Prompt ausgeben. Erfordert --path.
                   Namen: naming-drift | architecture-intent
                   Optional: --spec (mehrfach angebbar)

--list-evals       Alle verfügbaren Eval-Typen als Tabelle ausgeben. Kein --path nötig.

--spec <pfad>      Spezifikations-Quelle für --eval. Datei oder Verzeichnis
                   (Verzeichnis: nur erste Ebene, nur .md-Dateien).
                   Kann mehrfach angegeben werden.
```

### Nutzungsbeispiele

```powershell
# Naming-Drift: spec aus einer Datei
ainetlinter --eval naming-drift --path .\src --spec .\README.md

# Architecture-Intent: spec aus mehreren Dateien + einem Verzeichnis
ainetlinter --eval architecture-intent --path .\src --spec .\README.md --spec .\Docs

# In Datei dumpen → direkt als Cursor-Prompt verwenden
ainetlinter --eval naming-drift --path .\src --spec .\README.md > .\output\audit-prompt.md

# Ohne spec → Template enthält Fallback-Instruktion für das LLM
ainetlinter --eval naming-drift --path .\src

# Was gibt es?
ainetlinter --list-evals
```

### Fehlerverhalten

```
[ERROR]: CONFIG_REQUIRED: --path fehlt für --eval
  context: --eval naming-drift
  hint:    Pfad zur Solution oder zum Verzeichnis mit --path angeben.

[ERROR]: RESOURCE_NOT_FOUND: Unbekannter Eval-Typ 'foo'
  context: --eval foo
  hint:    Verfügbare Typen: naming-drift, architecture-intent
           Vollständige Liste mit --list-evals abrufen.
```

---

## 6. Platzhalter-System

Die eingebetteten Templates enthalten folgende Platzhalter die `EvalAssembler` ersetzt:

| Platzhalter | Ersetzt durch |
|:---|:---|
| `{{SPEC}}` | Inhalte aller `--spec` Quellen (konkateniert) oder Fallback-Instruktion |
| `{{VOCABULARY_MAP}}` | Output von `VocabularyMapBuilder.Build()` |
| `{{STRUCTURE_MAP}}` | Output von `StructureMapBuilder.Build()` |
| `{{GENERATED_AT}}` | `DateTime.Now` (Format: `yyyy-MM-dd HH:mm`) |
| `{{TARGET_PATH}}` | Wert von `--path` |

Nicht verwendete Platzhalter (z.B. `{{STRUCTURE_MAP}}` im naming-drift Template) bleiben
als leerer String — sie tauchen in der Ausgabe nicht auf.

**Fallback-Text wenn kein `--spec`:**

```
> **Spezifikation fehlt.** Verlange die Projektdokumentation (README, relevante Docs-Dateien)
> vom Nutzer und füge sie hier ein bevor du mit dem Audit fortfährst.
```

---

## 7. Template-Dateien

### `Docs/Evals/naming-drift.md`

```markdown
<!-- Generiert von: ainetlinter --eval naming-drift -->
<!-- Datum: {{GENERATED_AT}} | Pfad: {{TARGET_PATH}} -->
<!-- WICHTIG: Erstelle nur einen Bericht. Mache keine Änderungen am Code. -->

# Naming & Vocabulary Drift Audit

Du bist ein Vokabular-Auditor für Software-Projekte. Du kennst dieses Projekt nicht.
Deine Aufgabe: Semantischen Naming-Drift zwischen Spezifikation und Code-Identifiers erkennen.

---

## Spezifikation (Domain-Vokabular)

{{SPEC}}

---

## Code-Identifiers (Auto-Generiert)

{{VOCABULARY_MAP}}

---

## Deine Aufgabe

**Schritt 1 — Kanonisches Vokabular extrahieren**
Lies die Spezifikation und liste die 10–20 zentralen Domain-Begriffe auf
(Substantive für Kernkonzepte, Verben für Kernoperationen).

**Schritt 2 — Vergleich**

### Synonyme (höchste Priorität)
Verschiedene Namen für dasselbe Konzept?
Format: "Konzept X" → gefundene Varianten: `Name1`, `Name2`, `Name3`

### Aufgeblähte Namen
Namen mit >3 PascalCase-Segmenten, wiederholten Wörtern, unnötigen Suffixen
(`...Provider`, `...Service`, `...Manager`)?
Format: Name → Warum verdächtig

### Verwaiste Spec-Begriffe
Kanonische Begriffe die in den Code-Identifiers gar nicht auftauchen?

### Fremde Begriffe
Code-Namen die in der Spec nirgendwo vorkommen und kein offensichtliches
technisches Hilfskonstrukt sind?

### Urteil
Skala 1–5 (1 = kein Drift, 5 = starker Drift). Ein Satz Begründung.
```

### `Docs/Evals/architecture-intent.md`

```markdown
<!-- Generiert von: ainetlinter --eval architecture-intent -->
<!-- Datum: {{GENERATED_AT}} | Pfad: {{TARGET_PATH}} -->
<!-- WICHTIG: Erstelle nur einen Bericht. Mache keine Änderungen am Code. -->

# Architecture Intent Audit

Du bist ein erfahrener Software-Architekt der ein fremdes Projekt reviewt.
Du kennst nur den ursprünglichen Design-Intent und die aktuelle Struktur — keinen Code.
Deine Aufgabe: Strukturelle Abweichungen vom Intent finden.

---

## Ursprünglicher Design-Intent

{{SPEC}}

---

## Aktuelle Struktur (Auto-Generiert)

{{STRUCTURE_MAP}}

---

## Deine Aufgabe

### Erfüllte Prinzipien
Was entspricht klar dem Intent? (Konkret, keine Pauschalaussagen)

### Strukturelle Abweichungen
Format: "Intent sagt X — Struktur zeigt Y — Datei/Verzeichnis: Z"

### Anti-Patterns
Strukturen die explizit vermieden werden sollten aber trotzdem sichtbar sind?
(Namen wie `Manager`, `Helper`, `Utils`, sehr tiefe Verschachtelung,
sehr große Einzeldateien)

### Emergente Strukturen
Was ist entstanden das der Intent nicht erwähnt?
Bewerte: Sinnvolle Evolution oder ungeplanter Drift?

### Verdächtige Konzentration
Unverhältnismäßig große Verzeichnisse oder Dateien (potenzielle God Classes)?

### Urteil
Vollständig konform / Kleiner Drift / Signifikanter Drift / Starker Drift
```

---

## 8. Neues Quellcode-Gerüst

### 8.1 `EvalDefinition.cs`

```csharp
#nullable enable

namespace AiNetLinter.Evals;

internal enum EvalEvidenceType { Vocabulary, Structure }

/// <summary>
/// Beschreibt einen Eval-Typ: Name, Anzeige, benötigte Evidence.
/// </summary>
internal sealed record EvalDefinition(
    string Name,
    string DisplayName,
    string Description,
    EvalEvidenceType Evidence);
```

### 8.2 `EvalRegistry.cs`

```csharp
#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace AiNetLinter.Evals;

/// <summary>
/// Statisches Register aller verfügbaren Eval-Typen.
/// </summary>
internal static class EvalRegistry
{
    internal static readonly IReadOnlyList<EvalDefinition> All =
    [
        new EvalDefinition(
            Name:        "naming-drift",
            DisplayName: "Naming & Vocabulary Drift",
            Description: "Vergleicht Domain-Vokabular aus der Spec mit Code-Identifiers. Findet Synonyme, aufgeblähte Namen und verwaiste Begriffe.",
            Evidence:    EvalEvidenceType.Vocabulary),

        new EvalDefinition(
            Name:        "architecture-intent",
            DisplayName: "Architecture Intent",
            Description: "Prüft ob die Verzeichnisstruktur und Dateigrößen noch dem ursprünglichen Design-Intent entsprechen.",
            Evidence:    EvalEvidenceType.Structure),
    ];

    internal static EvalDefinition? TryResolve(string name) =>
        All.FirstOrDefault(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}
```

### 8.3 `SpecLoader.cs`

```csharp
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AiNetLinter.Evals;

/// <summary>
/// Lädt und konkateniert Spezifikations-Inhalte aus Dateien und Verzeichnissen.
/// Verzeichnisse: nur erste Ebene, nur .md-Dateien, alphabetisch sortiert.
/// Fehlt jede Quelle: standardisierter Fallback-Text für das LLM.
/// </summary>
internal static class SpecLoader
{
    private const string FallbackText =
        "> **Spezifikation fehlt.** Verlange die Projektdokumentation " +
        "(README, relevante Docs-Dateien) vom Nutzer und füge sie hier ein " +
        "bevor du mit dem Audit fortfährst.";

    internal static string Load(IReadOnlyList<string> specPaths)
    {
        if (specPaths.Count == 0)
            return FallbackText;

        var parts = new List<string>();

        foreach (var path in specPaths)
        {
            if (File.Exists(path))
            {
                parts.Add(File.ReadAllText(path, Encoding.UTF8).Trim());
            }
            else if (Directory.Exists(path))
            {
                var mdFiles = Directory
                    .EnumerateFiles(path, "*.md", SearchOption.TopDirectoryOnly)
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);

                foreach (var file in mdFiles)
                    parts.Add(File.ReadAllText(file, Encoding.UTF8).Trim());
            }
            // Nicht-existente Pfade werden stillschweigend übersprungen
        }

        return parts.Count > 0
            ? string.Join("\n\n---\n\n", parts)
            : FallbackText;
    }
}
```

### 8.4 `EvalAssembler.cs`

```csharp
#nullable enable

using System;
using System.IO;
using System.Text;
using AiNetLinter.Maps;
using AiNetLinter.Output;

namespace AiNetLinter.Evals;

/// <summary>
/// Lädt das eingebettete Template eines Eval-Typs, ersetzt alle Platzhalter
/// und gibt den assemblierten Prompt zurück.
/// </summary>
internal static class EvalAssembler
{
    internal static string Assemble(
        EvalDefinition eval,
        string targetPath,
        string spec,
        string generatedAt)
    {
        var template = LoadTemplate(eval.Name);
        var evidence = BuildEvidence(eval, targetPath);

        return template
            .Replace("{{SPEC}}",           spec)
            .Replace("{{VOCABULARY_MAP}}", eval.Evidence == EvalEvidenceType.Vocabulary ? evidence : "")
            .Replace("{{STRUCTURE_MAP}}",  eval.Evidence == EvalEvidenceType.Structure  ? evidence : "")
            .Replace("{{GENERATED_AT}}",   generatedAt)
            .Replace("{{TARGET_PATH}}",    targetPath.Replace('\\', '/'));
    }

    private static string LoadTemplate(string evalName)
    {
        var resourceName = $"Docs/Evals/{evalName}.md";
        using var stream = typeof(EvalAssembler).Assembly.GetManifestResourceStream(resourceName);

        if (stream == null)
            throw new InvalidOperationException(
                $"Eingebettetes Template '{resourceName}' nicht gefunden. " +
                "Prüfe ob die Datei in AiNetLinter.csproj als EmbeddedResource registriert ist.");

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string BuildEvidence(EvalDefinition eval, string targetPath)
    {
        var collector = new StringLintConsole();

        return eval.Evidence switch
        {
            EvalEvidenceType.Vocabulary => BuildVocabularyEvidence(targetPath, collector),
            EvalEvidenceType.Structure  => BuildStructureEvidence(targetPath, collector),
            _ => ""
        };
    }

    private static string BuildVocabularyEvidence(string targetPath, StringLintConsole collector)
    {
        VocabularyMapBuilder.Build(targetPath, collector);
        return collector.Output;
    }

    private static string BuildStructureEvidence(string targetPath, StringLintConsole collector)
    {
        // MaxLineCount: Default 500 (kein Config-Load nötig, da --eval kein --config erfordert)
        StructureMapBuilder.Build(targetPath, maxLineCount: 500, collector);
        return collector.Output;
    }

    /// <summary>
    /// Interne Konsole die alles in einen String sammelt statt auf stdout auszugeben.
    /// </summary>
    private sealed class StringLintConsole : ILintConsole
    {
        private readonly StringBuilder _sb = new();
        public string Output => _sb.ToString();
        public void WriteLine(string message) => _sb.AppendLine(message);
        public void WriteError(string message) { /* Fehler beim Evidence-Build ignorieren */ }
    }
}
```

**Hinweis zu `BuildStructureEvidence`:** Der `MaxLineCount`-Default (500) reicht für die
Evidenz-Darstellung im Eval-Prompt. Falls der Nutzer einen exakten Wert braucht, kann
`--config` optional ergänzt werden (ist aber kein Pflicht-Parameter für `--eval`).

### 8.5 `EvalCommand.cs`

```csharp
#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Cli;
using AiNetLinter.Evals;
using AiNetLinter.Output;

namespace AiNetLinter.Commands;

/// <summary>
/// Assembliert einen vollständigen Eval-Audit-Prompt und gibt ihn auf stdout aus.
/// </summary>
internal static class EvalCommand
{
    internal static Task<int> RunAsync(
        LinterArgs args,
        CancellationToken ct = default,
        ILintConsole? console = null)
    {
        var c = console ?? ConsoleLintConsole.Instance;

        if (string.IsNullOrWhiteSpace(args.TargetPath))
        {
            c.WriteError(LinterErrorFormatter.Format(
                LinterErrorCodes.ConfigRequired,
                "--path fehlt für --eval",
                context: $"--eval {args.EvalType}",
                hint: "Pfad zur Solution oder zum Verzeichnis mit --path angeben."));
            return Task.FromResult(1);
        }

        var eval = EvalRegistry.TryResolve(args.EvalType ?? "");
        if (eval == null)
        {
            c.WriteError(LinterErrorFormatter.Format(
                LinterErrorCodes.ResourceNotFound,
                $"Unbekannter Eval-Typ '{args.EvalType}'",
                context: $"--eval {args.EvalType}",
                hint: $"Verfügbare Typen: {string.Join(", ", EvalRegistry.All.Select(e => e.Name))}\n  Vollständige Liste mit --list-evals abrufen."));
            return Task.FromResult(1);
        }

        var spec    = SpecLoader.Load(args.SpecPaths ?? []);
        var prompt  = EvalAssembler.Assemble(eval, args.TargetPath, spec,
                          DateTime.Now.ToString("yyyy-MM-dd HH:mm"));

        c.WriteLine(prompt);
        return Task.FromResult(0);
    }
}
```

### 8.6 `ListEvalsCommand.cs`

```csharp
#nullable enable

using System.Text;
using AiNetLinter.Evals;
using AiNetLinter.Output;

namespace AiNetLinter.Commands;

/// <summary>
/// Gibt alle verfügbaren Eval-Typen als Tabelle aus.
/// </summary>
internal static class ListEvalsCommand
{
    internal static int Run(ILintConsole? console = null)
    {
        var c = console ?? ConsoleLintConsole.Instance;
        var sb = new StringBuilder();

        sb.AppendLine("# AiNetLinter — Eval-Übersicht");
        sb.AppendLine();
        sb.AppendLine("Assembliert vollständige Audit-Prompts inkl. Evidenz.");
        sb.AppendLine("Nutzung: `ainetlinter --eval <name> --path <pfad> [--spec <pfad> ...]`");
        sb.AppendLine();
        sb.AppendLine("| Name | Bezeichnung | Evidenz | --path |");
        sb.AppendLine("|:---|:---|:---|:---|");

        foreach (var eval in EvalRegistry.All)
        {
            var evidence = eval.Evidence switch
            {
                EvalEvidenceType.Vocabulary => "vocabulary map",
                EvalEvidenceType.Structure  => "structure map",
                _ => "-"
            };
            sb.AppendLine($"| {eval.Name} | {eval.DisplayName} | {evidence} | ja |");
        }

        sb.AppendLine();
        sb.AppendLine("Beschreibungen:");
        foreach (var eval in EvalRegistry.All)
            sb.AppendLine($"- **{eval.Name}:** {eval.Description}");

        c.WriteLine(sb.ToString().TrimEnd());
        return 0;
    }
}
```

---

## 9. CLI-Integration (Schritt für Schritt)

### 9.1 `Cli/CliOptionFactory.cs` — neue Methoden anhängen

```csharp
internal static Option<string?> CreateEvalOption() => new("--eval")
{
    Description = "Assemblierten Eval-Audit-Prompt ausgeben. Erfordert --path. Namen: naming-drift | architecture-intent",
};

internal static Option<bool> CreateListEvalsOption() => new("--list-evals")
{
    Description = "Alle verfügbaren Eval-Typen als Tabelle ausgeben",
};

internal static Option<string[]> CreateSpecOption()
{
    var opt = new Option<string[]>("--spec")
    {
        Description = "Spezifikations-Quelle für --eval: Datei oder Verzeichnis (erste Ebene, nur .md). Mehrfach angebbar.",
        AllowMultipleArgumentsPerToken = false,
    };
    opt.Arity = ArgumentArity.ZeroOrMore;
    return opt;
}
```

### 9.2 `Cli/CliOptions.cs` — drei neue Properties

```csharp
// Am Ende des Records, nach Map:
Option<string?> Eval,
Option<bool> ListEvals,
Option<string[]> Spec
```

### 9.3 `Cli/CliParsedArgs.cs` — drei neue Felder

```csharp
// Am Ende des Records:
string? EvalType,
bool ListEvals,
IReadOnlyList<string> SpecPaths
```

### 9.4 `Cli/LinterArgs.cs` — drei neue Properties

```csharp
/// <summary>
/// Holt oder setzt den Eval-Typ für --eval (naming-drift | architecture-intent).
/// </summary>
public string? EvalType { get; init; }

/// <summary>
/// Gibt an ob alle verfügbaren Eval-Typen aufgelistet werden sollen.
/// </summary>
public bool ListEvals { get; init; }

/// <summary>
/// Spezifikations-Quellen für --eval (Dateien oder Verzeichnisse).
/// </summary>
public IReadOnlyList<string> SpecPaths { get; init; } = [];
```

`Validate()` — Bedingung für `--path` erweitern:

```csharp
// Vorher:
if (Docs == null && !ListRules && DescribeRule == null && SearchRules == null && MapType == null
    && string.IsNullOrEmpty(TargetPath))

// Nachher: EvalType == null ergänzen
if (Docs == null && !ListRules && DescribeRule == null && SearchRules == null && MapType == null
    && EvalType == null && string.IsNullOrEmpty(TargetPath))
```

### 9.5 `Cli/CliCommandBuilder.cs`

**In `Build()`** — `options.Eval`, `options.ListEvals`, `options.Spec` zu `root` hinzufügen.

**In `CreateOptions()`** — drei neue Aufrufe:
```csharp
CliOptionFactory.CreateEvalOption(),
CliOptionFactory.CreateListEvalsOption(),
CliOptionFactory.CreateSpecOption()
```

**In `Parse()`** — drei neue Felder:
```csharp
EvalType:  parseResult.GetValue(options.Eval),
ListEvals: parseResult.GetValue(options.ListEvals),
SpecPaths: parseResult.GetValue(options.Spec) ?? []
```

**In `ToLinterArgs()` in `Program.cs`** — drei neue Zuweisungen:
```csharp
EvalType  = parsed.EvalType,
ListEvals = parsed.ListEvals,
SpecPaths = parsed.SpecPaths,
```

### 9.6 `Program.cs` — Dispatch + Header-Unterdrückung

**Header-Bedingung erweitern** (nach bisherigem `MapType`-Fix):
```csharp
if (linterArgs.Docs == null && linterArgs.MapType == null
    && linterArgs.EvalType == null && !linterArgs.ListEvals)
{
    Console.WriteLine($"# Run: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
}
```

**In `TryRunStandaloneCommand()`** — `--list-evals` ergänzen (kein --path nötig):
```csharp
if (args.ListEvals) return ListEvalsCommand.Run();
```

**In `ExecuteLinterAsync()`** — nach dem `--map` Dispatch:
```csharp
if (args.EvalType != null) return await EvalCommand.RunAsync(args, ct);
```

### 9.7 `AiNetLinter.csproj` — Templates einbetten

```xml
<EmbeddedResource Include="..\..\Docs\Evals\naming-drift.md"
                  LogicalName="Docs/Evals/naming-drift.md" />
<EmbeddedResource Include="..\..\Docs\Evals\architecture-intent.md"
                  LogicalName="Docs/Evals/architecture-intent.md" />
```

---

## 10. Test-Strategie

Alle Tests in `src/AiNetLinter.Tests/Evals/`. Keine Roslyn-Abhängigkeit.
Temp-Verzeichnis-Pattern wie in `Maps/`-Tests (IDisposable + Guid).

### 10.1 `SpecLoaderTests.cs`

```csharp
[Fact]
public void Load_EmptyList_ReturnsFallbackText()
{
    var result = SpecLoader.Load([]);
    Assert.Contains("Spezifikation fehlt", result);
}

[Fact]
public void Load_SingleFile_ReturnsContent()
{
    // Temp-Datei mit "# My Spec"
    var result = SpecLoader.Load([tempFile]);
    Assert.Contains("# My Spec", result);
}

[Fact]
public void Load_Directory_ReadsOnlyTopLevelMd()
{
    // Erstellt: top-level.md, subdir/nested.md
    // Erwartet: top-level.md gelesen, subdir/nested.md NICHT
    var result = SpecLoader.Load([tempDir]);
    Assert.Contains("top-level content", result);
    Assert.DoesNotContain("nested content", result);
}

[Fact]
public void Load_Directory_IgnoresNonMdFiles()
{
    // Erstellt: readme.md (gelesen), notes.txt (ignoriert)
    var result = SpecLoader.Load([tempDir]);
    Assert.DoesNotContain("txt content", result);
}

[Fact]
public void Load_MultipleSpecs_ConcatenatesInOrder()
{
    // spec-a.md und spec-b.md
    var result = SpecLoader.Load([fileA, fileB]);
    var posA = result.IndexOf("content-a", StringComparison.Ordinal);
    var posB = result.IndexOf("content-b", StringComparison.Ordinal);
    Assert.True(posA < posB);
}

[Fact]
public void Load_NonExistentPath_SkipsGracefully()
{
    // Nicht-existenter Pfad + eine valide Datei
    var result = SpecLoader.Load(["C:/does/not/exist.md", validFile]);
    Assert.Contains("valid content", result);
}
```

### 10.2 `EvalAssemblerTests.cs`

```csharp
[Fact]
public void Assemble_ReplacesGeneratedAtPlaceholder()
{
    // GENERATED_AT darf nicht wörtlich im Output erscheinen
    var result = EvalAssembler.Assemble(namingDriftEval, tempDir, "spec", "2026-01-01 12:00");
    Assert.DoesNotContain("{{GENERATED_AT}}", result);
    Assert.Contains("2026-01-01 12:00", result);
}

[Fact]
public void Assemble_NamingDrift_ContainsVocabularyMapOutput()
{
    // tempDir hat eine .cs-Datei mit "public class FooChecker {}"
    // Output muss "FooChecker" enthalten
    var result = EvalAssembler.Assemble(namingDriftEval, tempDir, "spec", "2026-01-01");
    Assert.Contains("FooChecker", result);
}

[Fact]
public void Assemble_ArchitectureIntent_ContainsStructureMapOutput()
{
    // tempDir hat eine .cs-Datei
    // Output muss ".cs" und Zeilen-Zählung enthalten
    var result = EvalAssembler.Assemble(architectureEval, tempDir, "spec", "2026-01-01");
    Assert.Contains(".cs", result);
}

[Fact]
public void Assemble_NamingDrift_DoesNotContainStructureMapPlaceholder()
{
    var result = EvalAssembler.Assemble(namingDriftEval, tempDir, "spec", "2026-01-01");
    Assert.DoesNotContain("{{STRUCTURE_MAP}}", result);
}

[Fact]
public void Assemble_SpecInlinedInOutput()
{
    var result = EvalAssembler.Assemble(namingDriftEval, tempDir, "MY_UNIQUE_SPEC", "2026-01-01");
    Assert.Contains("MY_UNIQUE_SPEC", result);
}
```

### 10.3 `ListEvalsCommandTests.cs`

```csharp
[Fact]
public void Run_OutputContainsAllEvalNames()
{
    var console = new TestLintConsole();
    ListEvalsCommand.Run(console);
    Assert.Contains("naming-drift", console.Output);
    Assert.Contains("architecture-intent", console.Output);
}

[Fact]
public void Run_ReturnsExitCodeZero()
{
    Assert.Equal(0, ListEvalsCommand.Run());
}
```

**Hinweis:** `TestLintConsole` aus `AiNetLinter.Tests.Maps` liegt in einem anderen Namespace.
Entweder ins Test-Projekt hochziehen (z.B. `AiNetLinter.Tests.Helpers`) oder in
`Evals/` lokal neu anlegen. Empfehlung: gemeinsame `TestHelpers/TestLintConsole.cs`
auf Test-Projekt-Ebene.

---

## 11. Dokumentations-Updates

### `Docs/agent-api.md`

Neue Sektion **Eval-Befehle** ergänzen:

```markdown
## Eval-Befehle (Assembled Audit Prompts)

Generieren vollständige, sofort nutzbare LLM-Audit-Prompts inkl. Evidenz.
Erfordern --path.

ainetlinter --list-evals
ainetlinter --eval naming-drift        --path <pfad> [--spec <pfad>...]
ainetlinter --eval architecture-intent --path <pfad> [--spec <pfad>...]
```

In der **Alle CLI-Flags** Tabelle ergänzen:

| Flag | Typ | Beschreibung |
|:---|:---|:---|
| `--eval <name>` | string | Assemblierten Eval-Prompt ausgeben (`naming-drift`, `architecture-intent`) |
| `--list-evals` | bool | Verfügbare Eval-Typen auflisten |
| `--spec <pfad>` | string[] | Spezifikationsquelle für `--eval`: Datei oder Verzeichnis (erste Ebene, nur .md). Mehrfach angebbar. |

### `Docs/configuration.md`

Neue Sektion **Eval-Prompts (`--eval`)**:

```markdown
## Eval-Prompts (`--eval`)

`--eval` assembliert einen vollständigen LLM-Audit-Prompt aus drei Quellen:

1. **Template** — eingebettet im Binary (`Docs/Evals/`)
2. **Spezifikation** — aus `--spec` Quellen (oder LLM-Fallback-Instruktion)
3. **Evidenz** — frisch generiert (vocabulary map oder structure map)

| Eval-Typ | Evidenz | --spec empfohlen |
|:---|:---|:---|
| `naming-drift` | VocabularyMap | README.md, Domain-Dokumentation |
| `architecture-intent` | StructureMap | Architektur-Beschreibung, Designentscheidungen |

`--spec` kann mehrfach angegeben werden. Verzeichnisse: nur erste Ebene, nur .md-Dateien.
```

### `README.md`

Im Feature-Überblick ergänzen:

```markdown
- **Eval-Prompts (`--eval`):** Vollständig assemblierte LLM-Audit-Prompts für
  Naming-Drift und Architecture-Intent — inkl. frischer Evidenz, direkt als
  Cursor-Input verwendbar.
```

### `Docs/ROADMAP.md`

Epic 31 anhängen (nach Epic 30):

```markdown
## Epic 31: Eval-Audit-Prompt-Feature (`--eval`)

Assembliert vollständige LLM-Audit-Prompts aus eingebetteten Templates, Spezifikations-
Quellen und frisch generierter Codebase-Evidenz.

- [ ] `EvalDefinition` + `EvalRegistry` (naming-drift, architecture-intent)
- [ ] `SpecLoader`: Dateien + Verzeichnisse (erste Ebene, .md), Fallback-Text
- [ ] `EvalAssembler`: Template laden, Platzhalter {{...}} ersetzen, Evidence auto-generieren
- [ ] `EvalCommand` + `ListEvalsCommand`
- [ ] CLI-Integration: --eval, --list-evals, --spec; Header-Unterdrückung
- [ ] Templates: Docs/Evals/naming-drift.md, Docs/Evals/architecture-intent.md
- [ ] csproj: EmbeddedResource für beide Templates
- [ ] Tests: SpecLoaderTests, EvalAssemblerTests, ListEvalsCommandTests
- [ ] Research/Evals/ gelöscht (ersetzt durch eingebettete Templates)
- [ ] Docs aktualisiert: agent-api.md, configuration.md, README.md, ROADMAP.md
```

---

## 12. Nicht-Ziele

- **Kein `--config` erforderlich für `--eval`** — der Default-MaxLineCount (500) reicht für
  Evidenz-Darstellung. Nutzer der exakte Grenzwerte brauchen können optional `--config`
  kombinieren, aber `EvalCommand` wertet es aktuell nicht aus.
- **Kein `--eval e01` / `--eval e04`** — gelöscht. Kein Auto-Fill möglich.
- **Kein JSON-Output** — nur Markdown (konsistent mit allen anderen Befehlen).
- **Keine Prompt-Versionierung** — Templates leben im Binary, Update = neues Release.
  Das ist gewollt (weniger Wartungsaufwand).
- **Kein `--docs eval/naming-drift`** — rohe Templates sind kein eigenständiger Use Case;
  `--list-evals` reicht für Discovery.

---

## 13. Umsetzungsreihenfolge

1. `Docs/Evals/naming-drift.md` + `architecture-intent.md` anlegen (Template-Texte)
2. `AiNetLinter.csproj` — EmbeddedResource-Einträge ergänzen
3. `EvalDefinition.cs` + `EvalRegistry.cs` (trivial)
4. `SpecLoader.cs` + `SpecLoaderTests.cs`
5. `EvalAssembler.cs` + `EvalAssemblerTests.cs`
6. `EvalCommand.cs` + `ListEvalsCommand.cs` + `ListEvalsCommandTests.cs`
7. CLI-Integration: CliOptionFactory → CliOptions → CliParsedArgs → LinterArgs → CliCommandBuilder → Program
8. `Research/Evals/` Ordner löschen
9. Docs updaten: `agent-api.md`, `configuration.md`, `README.md`, `ROADMAP.md`
10. Selbst-Audit: Linter auf eigene Implementierung laufen lassen → Zero Violations

---

## 14. Commit-Vorschlag

```
feat: füge --eval naming-drift/architecture-intent für assemblierte Audit-Prompts hinzu

Neuer --eval Befehl assembliert vollständige LLM-Audit-Prompts aus eingebetteten
Templates (Docs/Evals/), --spec Quellen und frisch generierter Codebase-Evidenz.
--list-evals listet verfügbare Eval-Typen. Research/Evals/ wird durch eingebettete
Templates ersetzt — keine manuelle Prompt-Pflege mehr notwendig.
```
