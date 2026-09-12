# AiNetLinter — Linter-Projektintegration (Schritt-für-Schritt)

→ [CLI-Referenz](cli.md) | [Konfigurationsreferenz](configuration.md) | [MCP-Integration](../mcp/integration.md) | [README](../../README.md)

Diese Anleitung beschreibt die schrittweise Integration von `AiNetLinter` in ein beliebiges bestehendes C#/.NET-Projekt (z. B. als Architektur-Test in xUnit, NUnit oder MSTest sowie im CI/CD-Prozess).

Für die Anbindung an Cursor, Claude Code oder andere MCP-Hosts siehe [MCP-Host-Integration](../mcp/integration.md).

---

## 1. Voraussetzungen

- .NET 10 SDK auf Entwickler- und Build-Maschinen.
- Die zu prüfende Solution (`.sln` oder `.slnx`) muss mit `dotnet build` fehlerfrei kompilieren.
- Ein frischer `dotnet restore` muss erfolgt sein (`obj/project.assets.json` vorhanden).

---

## 2. Schritt-für-Schritt-Integration

### Schritt 1: Verzeichnisstruktur anlegen

Im Repository des Zielprojekts empfiehlt sich ein dedizierter Ordner für Tooling und Konfiguration:

```text
<RepositoryRoot>/
├── tools/
│   └── ainetlinter/
│       ├── AiNetLinter.exe
│       ├── BuildHost-netcore/      ← Zwingend erforderlich für MSBuildWorkspace!
│       └── BuildHost-net472/       ← Zwingend erforderlich für MSBuildWorkspace!
├── ainetlinter-rules.json          ← Projektweite Konfiguration
└── ainetlinter-baseline.json       ← Checksummen-Baseline für inkrementelle Migration
```

> [!IMPORTANT]
> **MSBuild-Abhängigkeiten (BuildHost-Ordner):**
> `MSBuildWorkspace` benötigt externe Host-Prozesse zum Parsen von Visual Studio Projektdateien. Nach dem Publish müssen zwingend die beiden Unterordner `BuildHost-netcore/` und `BuildHost-net472/` im selben Verzeichnis wie `AiNetLinter.exe` liegen. Andernfalls bricht die Analyse mit einem fatalen MSBuildWorkspace-Ladefehler ab.

---

### Schritt 2: Tool-Dokumentation exportieren

Um Entwicklern und AI-Agenten im Zielprojekt schnellen Zugriff auf die Dokumentation zu geben:

```cmd
AiNetLinter.exe --docs readme        > tools/ainetlinter/AiNetLinter-readme.md
AiNetLinter.exe --docs cli           > tools/ainetlinter/AiNetLinter-cli.md
AiNetLinter.exe --docs configuration > tools/ainetlinter/AiNetLinter-configuration.md
```

---

### Schritt 3: Startkonfiguration anlegen

Erzeuge die Standardkonfiguration direkt aus der ausführbaren Datei:

```cmd
AiNetLinter.exe --docs ainetlinter-rules-json > ainetlinter-rules.json
```

Der Linter führt beim Start einen automatischen Schema-Abgleich durch: Fehlende Schlüssel werden ergänzt, veraltete Schlüssel bereinigt.

---

### Schritt 4: .gitignore konfigurieren

Ergänze die `.gitignore` des Zielprojekts:

```gitignore
# AiNetLinter temporäre Berichte und Caches
**/cache/
**/measurements/
**/output/
*.log
```

Die `ainetlinter-rules.json` sowie die `ainetlinter-baseline.json` werden im Repository versioniert!

---

### Schritt 5: Integration in Unit Tests (Architekturtests)

Um sicherzustellen, dass Entwickler oder AI-Agenten die Architektur- und Clean-Code-Regeln nicht verletzen, empfiehlt sich die Integration als Unit-Test.

#### Beispiel mit xUnit

```csharp
using System;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace MyProject.Tests.Architecture;

public sealed class ArchitectureTests
{
    [Fact]
    public void Enforce_AiNetLinter_Rules_On_Solution()
    {
        // Pfade relativ zum Testprojekt auflösen
        var solutionPath = Path.GetFullPath("../../../MyProject.slnx");
        var configPath = Path.GetFullPath("../../../ainetlinter-rules.json");
        var baselinePath = Path.GetFullPath("../../../ainetlinter-baseline.json");
        var linterCliPath = Path.GetFullPath("../../../tools/ainetlinter/AiNetLinter.exe");

        var processInfo = new ProcessStartInfo
        {
            FileName = linterCliPath,
            Arguments = $"--config \"{configPath}\" --path \"{solutionPath}\" --baseline \"{baselinePath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(processInfo);
        Assert.NotNull(process);

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.True(process.ExitCode == 0, 
            $"AiNetLinter hat Verstoesse gefunden (Exit {process.ExitCode}):\n{output}\n{error}");
    }
}
```

#### Beispiel mit NUnit / MSTest
Das Verfahren ist identisch; ersetze `[Fact]` und `Assert.True` durch das jeweilige Test-Framework-Äquivalent (`[Test]` / `Assert.That(process.ExitCode, Is.Zero)` bzw. `[TestMethod]` / `Assert.AreEqual(0, process.ExitCode)`).

---

### Schritt 6: Ersten Lauf durchführen und Baseline anlegen

In gewachsenen Codebasen bestehen oft hunderte oder tausende Verstöße. Anstatt alle sofort zu beheben, wird eine Baseline angelegt (Ratchet-Modus):

```cmd
# Alle bestehenden Verstöße einfrieren:
AiNetLinter.exe --path MyProject.slnx --create-baseline ainetlinter-baseline.json

# Baseline versionieren:
git add ainetlinter-baseline.json
git commit -m "chore: add initial ainetlinter baseline"
```

Ab diesem Zeitpunkt schlagen Architektur-Tests nur noch bei neuem oder modifiziertem Code fehl.

---

### Schritt 7: Regeln & Schwellenwerte anpassen

Passe die Schwellenwerte in `ainetlinter-rules.json` schrittweise an. Details siehe [Linter-Konfiguration](configuration.md).

---

## 3. Lokale Warnungs-Unterdrückung (Suppression)

Sollte es notwendig sein, bestimmte Regeln für eine Zeile oder eine gesamte Datei temporär oder begründet zu deaktivieren, erfolgt dies über C#-Kommentare:

```csharp
// ainetlinter-disable all
// Deaktiviert alle AiNetLinter-Regeln für die gesamte Datei.

// ainetlinter-disable MaxLineCount
// Deaktiviert nur die MaxLineCount-Prüfung dateiweit.

public void HighParameterMethod(int a, int b, int c, int d, int e) // ainetlinter-disable MaxMethodParameterCount
{
    // Deaktiviert den Parameter-Count-Linter exklusiv für diese Zeile
}

try
{
    int.Parse("not-a-number");
}
catch (Exception) // ainetlinter-disable EnforceNoSilentCatch
{
    // Deaktiviert den Silent-Catch-Linter exklusiv für diese catch-Zeile
}
```

### Gezielter Bulk-Ausschluss (nur betroffene Dateien)

Für Legacy-Codebasen, in denen vorerst alle Dateien mit akuten Verstößen markiert werden sollen:

```bash
ainetlinter --config ainetlinter-rules.json --path ./MeinProjekt.slnx --add-disable-all
```

Fügt `// ainetlinter-disable all` am Dateianfang ein — ausschließlich bei Dateien mit Verstößen.

### Bulk-Entfernung des Disable-all-Kommentars

Nach erfolgreichem Refactoring:

```bash
ainetlinter --path ./MeinProjekt.slnx --remove-disable-all
```

Entfernt exakte `// ainetlinter-disable all`-Zeilen aus allen `.cs`-Dateien unter `--path`.

---

## 4. Consumer-Setup & Pragmatic Defaults

### Checkliste für die Produktiveinführung

1. **Konfiguration anlegen:** `ainetlinter-rules.json` im Projekt-Root versionieren.
2. **Projekt-Overrides für Tests:** Pragmatischere Schwellenwerte für Testprojekte (z. B. `*.Tests`) konfigurieren.
3. **Baseline aktivieren:** Vorhandene Codebestände per Checksummen-Baseline einfrieren.
4. **MSBuild BuildHost-Verzeichnisse prüfen:** Sicherstellen, dass `BuildHost-netcore/` und `BuildHost-net472/` neben der EXE liegen.

### Pragmatische Default-Empfehlungen

| Regel | Pragmatic | Strict | Begründung / Kontext |
| :--- | :--- | :--- | :--- |
| `DetectAndBanPhantomDependencies` | **on** | **on** | Verhindert, dass KIs nicht-existente Typen/Namespaces oder dynamische Reflektion erzeugen. |
| `MaxAIContextFootprint` | **5000** | **4000** | Schont das Kontextbudget bei Agenten-Prompts. |
| `AllowUnsealedPartialClasses` | **on** | **on** | Erforderlich für Blazor- und WPF-Komponenten. |
| `EnforceExplicitStateImmutability` | **off** | **on** | In Legacy-Projekten zunächst auslassen, bis DTOs/Entities refaktoriert sind. |
| `EnforceNamespaceDirectoryMapping` | **off** | **on** | Bei Feature-Foldern oder älteren Ordnerstrukturen deaktivieren. |
| `EnforceResultPatternOverExceptions` | **off** | **on** | Deaktivieren, falls im Altsystem weitreichend Exceptions zur Validierung genutzt werden. |
| `MaxCyclomaticComplexity` | **8** | **5** | Wert 8 verhindert übermäßiges Aufsplittern bei komplexen Altmethoden. |
