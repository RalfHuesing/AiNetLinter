# Umsetzungsplan: `--readme` CLI-Option

**Ziel:** Die `README.md` wird beim Build als eingebettete Ressource in die EXE gepackt. Ein LLM-Agent kann sie per `ainetlinter --readme` abrufen — ohne Dateisystem-Zugriff, ohne `--path`. Der Usage-Text nennt den Kontext-Footprint in Bytes, damit der Agent die Token-Last taktisch einplanen kann.

---

## Betroffene Dateien (in Reihenfolge der Umsetzung)

| # | Datei | Art der Änderung |
|---|-------|-----------------|
| 1 | `src/AiNetLinter/AiNetLinter.csproj` | `<EmbeddedResource>` Eintrag hinzufügen |
| 2 | `src/AiNetLinter/Cli/CliOptionFactory.cs` | `CreateReadmeOption()` ergänzen |
| 3 | `src/AiNetLinter/Cli/CliCommandBuilder.cs` | `Readme` in `Options`- und `ParsedArgs`-Record, `Build()`, `CreateOptions()`, `Parse()` |
| 4 | `src/AiNetLinter/Cli/LinterArgs.cs` | Property `Readme`, `Validate()` anpassen, `--path` optional machen |
| 5 | `src/AiNetLinter/Program.cs` | Early-Exit in `ExecuteLinterAsync`, `RunPrintReadme()`, Header unterdrücken |

---

## Schritt 1 — `AiNetLinter.csproj`: README einbetten

Die README liegt relativ zum Projektordner unter `..\..\README.md`.

```xml
<!-- In der bestehenden <ItemGroup> nach den PackageReferences, oder als eigene <ItemGroup> -->
<ItemGroup>
  <EmbeddedResource Include="..\..\README.md" LogicalName="README.md" />
</ItemGroup>
```

**Warum `LogicalName="README.md"`?**  
Ohne dieses Attribut erzeugt MSBuild einen Ressourcennamen aus dem Projektnamespace + Ordnerpfad (z. B. `AiNetLinter.README.md`). Mit `LogicalName` ist der Name stabil und unabhängig von der Projektstruktur, also immer `"README.md"` — das vereinfacht den `GetManifestResourceStream`-Aufruf.

---

## Schritt 2 — `CliOptionFactory.cs`: `CreateReadmeOption()`

Neue Methode am Ende der Klasse ergänzen. Die Byte-Größe wird **dynamisch zur Laufzeit** aus dem eingebetteten Stream gelesen und in die Beschreibung eingebaut:

```csharp
internal static Option<bool> CreateReadmeOption()
{
    long byteCount = 0;
    try
    {
        using var stream = typeof(CliOptionFactory).Assembly.GetManifestResourceStream("README.md");
        if (stream != null) byteCount = stream.Length;
    }
    catch { /* Fallback: 0, kein Absturz */ }

    return new Option<bool>("--readme")
    {
        Description = $"Gibt die integrierte README.md fuer KI-Agenten aus (Kontext-Footprint: ca. {byteCount} Bytes / ~{byteCount / 4} Tokens).",
    };
}
```

**Token-Schätzung:** Der Divisor `/ 4` entspricht der Faustregel ~4 Bytes pro Token (englischer/gemischter Fließtext). Das ist eine grobe Schätzung — kein exakter Wert.

---

## Schritt 3 — `CliCommandBuilder.cs`: Drei Stellen erweitern

### 3a — `Options`-Record (Zeile 12–31)

`Option<bool> Readme` ans Ende des Records hinzufügen:

```csharp
internal sealed record Options(
    Option<string?> Config,
    Option<string> Path,
    // ... alle bestehenden Felder ...
    Option<string?> Footprint,
    Option<bool> Readme);          // NEU
```

### 3b — `ParsedArgs`-Record (Zeile 58–70)

`bool Readme` ans Ende des Records hinzufügen:

```csharp
internal sealed record ParsedArgs(
    string? ConfigPath,
    string TargetPath,
    // ... alle bestehenden Felder ...
    string? Footprint,
    bool Readme);                  // NEU
```

### 3c — `Build()` (Zeile 72–84)

`options.Readme` in den `RootCommand`-Initializer einreihen:

```csharp
var root = new RootCommand("AiNetLinter - CLI-Linter für AI-optimierten .NET Code")
{
    options.Config, options.Path, options.Graph, options.Playbook, options.Format, options.Verbose,
    options.CreateBaseline, options.Baseline, options.AddDisableAll, options.RemoveDisableAll,
    options.DebtReport, options.WaveReady, options.OnlyChanged, options.GitSince,
    options.Fix, options.Impact, options.SyncCursorRules, options.Check, options.Footprint,
    options.Readme,                // NEU
};
```

### 3d — `CreateOptions()` (Zeile 86–108)

```csharp
return new Options(
    // ... alle bestehenden Aufrufe ...
    CliOptionFactory.CreateFootprintOption(),
    CliOptionFactory.CreateReadmeOption());   // NEU
```

### 3e — `Parse()` (Zeile 110–138)

```csharp
return new ParsedArgs(
    // ... alle bestehenden Aufrufe ...
    Footprint: parseResult.GetValue(options.Footprint),
    Readme: parseResult.GetValue(options.Readme));    // NEU
```

---

## Schritt 4 — `LinterArgs.cs`: Property + Validierung + `--path` optional

### 4a — `--path` optional machen

`CreatePathOption()` in `CliOptionFactory.cs` — `Required = true` entfernen:

```csharp
// VORHER:
internal static Option<string> CreatePathOption() => new("--path", "-p")
{
    Description = "Pfad zur Solution-Datei (.sln / .slnx) oder ein Verzeichnis",
    Required = true,
};

// NACHHER:
internal static Option<string?> CreatePathOption() => new Option<string?>("--path", "-p")
{
    Description = "Pfad zur Solution-Datei (.sln / .slnx) oder ein Verzeichnis (nicht erforderlich bei --readme)",
};
```

> **Achtung:** Der Rückgabetyp wechselt von `Option<string>` zu `Option<string?>`. Das zieht eine Anpassung im `Options`-Record nach sich (`Option<string>` → `Option<string?>`), und in `Parse()` muss `parseResult.GetValue(options.Path) ?? ""` stehen bleiben (ist es bereits).

### 4b — `Readme`-Property in `LinterArgs.cs`

```csharp
/// <summary>
/// Holt oder setzt einen Wert, der angibt, ob die eingebettete README ausgegeben werden soll.
/// </summary>
public bool Readme { get; init; }
```

### 4c — `Validate()` anpassen

`--path` ist jetzt optional — aber alle anderen Modi außer `--readme` brauchen ihn weiterhin. Neuer erster Guard in `Validate()`:

```csharp
public string? Validate()
{
    if (!Readme && string.IsNullOrEmpty(TargetPath))
    {
        return "[ERROR]: --path ist erforderlich (außer bei --readme).";
    }

    if (HasConflictingModeOptions())
    {
        return "[ERROR]: Wartungsmodi (--create-baseline, --add-disable-all, --remove-disable-all) sind untereinander und mit --baseline nicht kombinierbar.";
    }

    if (OnlyChanged && BaselinePath == null)
    {
        return "[ERROR]: --only-changed erfordert --baseline.";
    }

    return null;
}
```

---

## Schritt 5 — `Program.cs`: Early-Exit + `RunPrintReadme()` + Header unterdrücken

### 5a — `ToLinterArgs()` (Zeile 66–91)

```csharp
return new LinterArgs
{
    // ... alle bestehenden Mappings ...
    Footprint = parsed.Footprint,
    Readme = parsed.Readme,        // NEU
};
```

### 5b — Header-Ausgabe unterdrücken (Zeile 48–53)

Der `# Run:`-Header soll bei `--readme` nicht erscheinen. Anpassung in `Main()`:

```csharp
root.SetAction(async parseResult =>
{
    try
    {
        var linterArgs = ToLinterArgs(CliCommandBuilder.Parse(parseResult, options));
        if (!linterArgs.Readme && linterArgs.Format != "sarif")
        {
            Console.WriteLine($"# Run: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        }
        return await ExecuteLinterAsync(linterArgs);
    }
    // ...
});
```

### 5c — Early-Exit in `ExecuteLinterAsync()` (Zeile 93)

Vor `ValidateArgs` einfügen:

```csharp
private static async Task<int> ExecuteLinterAsync(LinterArgs args)
{
    if (args.Readme)
    {
        return RunPrintReadme();
    }

    var validationError = ValidateArgs(args);
    // ... Rest unverändert
}
```

### 5d — `RunPrintReadme()` (neue private Methode)

Methodensignatur passt zum bestehenden Muster (`RunSyncCursorRules`, `RunFootprintAnalysisAsync`):

```csharp
private static int RunPrintReadme()
{
    using var stream = typeof(Program).Assembly.GetManifestResourceStream("README.md");
    if (stream == null)
    {
        Console.Error.WriteLine("[ERROR]: Die README.md wurde nicht als eingebettete Ressource gefunden.");
        return 1;
    }

    using var reader = new StreamReader(stream, Encoding.UTF8);
    Console.WriteLine(reader.ReadToEnd());
    return 0;
}
```

**Exit-Codes:** 0 = Erfolg, 1 = Ressource nicht gefunden (Build-Fehler oder fehlende Embed-Config). Konsistent mit dem bestehenden Muster.

---

## Nebenbedingungen & Hinweise

### `TargetPath` bleibt `required string`

Das C#-Keyword `required` auf einer Property bedeutet, dass der Property-Initializer im `new`-Ausdruck gesetzt werden **muss** — nicht, dass der Wert nicht leer sein darf. `TargetPath = ""` ist gültig. Keine Typen-Änderung nötig.

### Kein Test für `RunPrintReadme` nötig

Die Logik ist trivial (Stream lesen, auf Konsole schreiben). Ein Integrationstest, der die EXE mit `--readme` aufruft und prüft, dass stdout nicht leer ist und Exit-Code 0 zurückkommt, deckt den relevanten Fall ab. Dieser kann analog zu bestehenden CLI-Integrationstests in `AiNetLinter.Tests` angelegt werden (optional, nicht Pflicht für diesen Schritt).

### README-Pfad in `GetManifestResourceStream`

Der `LogicalName="README.md"` im csproj-Embed ergibt den Ressourcennamen `"README.md"`. Sollte der Aufruf `null` zurückgeben (z. B. nach einem Build ohne Embed), zeigt der Fehlertext dem LLM sofort, was fehlt.

### Größenangabe ist Build-Zeit-dynamisch

Da die Byte-Größe aus dem eingebetteten Stream ausgelesen wird (nicht hartcodiert), bleibt die Usage-Beschreibung automatisch korrekt, wenn die README wächst oder schrumpft. Kein manuelles Update nötig.

---

## Ergebnis (Expected Behavior)

```
> ainetlinter --readme
# [Vollständiger Inhalt der README.md]
```

```
> ainetlinter --help
...
--readme    Gibt die integrierte README.md fuer KI-Agenten aus (Kontext-Footprint: ca. 52167 Bytes / ~13041 Tokens).
...
```

```
> ainetlinter
[ERROR]: --path ist erforderlich (außer bei --readme).
```

Exit-Code bei `--readme`: immer `0` (außer Ressource fehlt → `1`).
