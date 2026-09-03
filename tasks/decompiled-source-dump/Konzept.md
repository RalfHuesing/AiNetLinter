---
status: ready
---

# Konzept: Vollständige sofortige Assembly-Projekt-Materialisierung

## 1. Ziel und Nutzen

Der AiNetLinter-MCP-Server materialisiert geladene .NET-Assemblies (z. B. via `inspect_assembly` oder direkte Assembly-Routen) ab sofort **vollständig, sofort und direkt** als reales C#-Projekt auf der Festplatte.

Bisher erzeugte AiNetLinter synthetische Signature-Stubs mit `throw null!;` und dekompilierte Methodenkörper erst On-Demand bei Aufruf von `get_symbol_body`. Dies verhinderte Volltextsuchen (z. B. mit `rg`/ripgrep nach DCM-Aufrufen oder IDs) im Dateisystem und verhinderte vollständige Roslyn-Call-Tree- und Syntax-Analysen.

Mit der Umstellung erhält der Agent:
1. Ein vollwertiges dekompiliertes C#-Projekt (`.csproj` + vollständige `.cs`-Dateien mit allen Methodenkörpern) im Cache-Verzeichnis.
2. Explizit in `inspect_assembly` ausgewiesene absolute Pfade zum Quellcode- und Projektverzeichnis für lokale Werkzeuge (`rg`, grep, `get_file_tree`, Dateibaum-Navigation).
3. Vollständige Roslyn-Code-Analyse (inkl. Body-Syntax, Call-Trees und Referenzen) ohne On-Demand-Nachladen.

## 2. Referenzimplementierung: SourceToAI

Als Vorlage und bewährte Referenz dient das Projekt:
- **Quelle:** `C:\Daten\Entwicklung\Ralf\SourceToAI`
- **Kernkomponente:** `SourceToAI.CLI.Services.Decompilation.AssemblyDecompilerService`
- **Engine:** `ICSharpCode.Decompiler` (`WholeProjectDecompiler`)

In `SourceToAI` ist dieser Mechanismus produktiv getestet und etabliert:
```csharp
using var module = new PEFile(assemblyFullPath);
var targetFrameworkId = module.DetectTargetFrameworkId();
var resolver = new UniversalAssemblyResolver(
    assemblyFullPath,
    throwOnError: false,
    targetFrameworkId);

var assemblyDir = Path.GetDirectoryName(assemblyFullPath);
if (!string.IsNullOrEmpty(assemblyDir))
    resolver.AddSearchDirectory(assemblyDir);

var settings = new DecompilerSettings
{
    RemoveDeadCode = true,
    YieldReturn = true,
    AsyncAwait = true,
};

var decompiler = new WholeProjectDecompiler(
    settings,
    resolver,
    projectWriter: null,
    assemblyReferenceClassifier: null,
    debugInfoProvider: null);

decompiler.DecompileProject(module, targetFullPath, cancellationToken);
```
Dieser Ablauf wird direkt in AiNetLinter übernommen.

## 3. Kern-Architektur & Leitprinzipien

1. **Sofortige Volldekompilierung (Eager Decompilation):**
   Beim Laden einer Fremd-DLL wird diese unmittelbar und vollständig als Projekt dekompiliert.
2. **Kein On-Demand-Dekompilieren:**
   Keine zweistufige Trennung mehr zwischen Signature-Stubs und nachgeladenen Bodies. Alle Symbole besitzen von Beginn an ihren echten Body.
3. **Rigid Cleanup (Rigorose Löschung von Altcode):**
   Code und Tests, die der bisherigen On-Demand-Body-Auflösung und Stub-Erzeugung dienten, werden vollständig entfernt. Keine verwaisten Adapter oder Kompatibilitätsbrücken.
4. **Keine künstlichen Größenbeschränkungen:**
   Es gibt keine feste Dateigrößen-Obergrenze für DLLs. Beliebig große DLLs werden nach demselben robusten Ablauf dekompiliert.
5. **Token-sparsame, transparente Pfadausgabe für Agenten:**
   - Der Einstiegs-Toolcall `inspect_assembly` weist im Header und im JSON-Payload kompakt die absoluten Pfade (`decompiledSourceRoot`, `decompiledProjectPath`, `decompiledProjectDirectory`) aus.
   - Mit dem ausgewiesenen `decompiledSourceRoot` kann der Agent direkt mit `rg` suchen oder `get_file_tree(projectRoot: decompiledSourceRoot)` ausführen.
   - Nachfolgende Abfragen (z. B. `get_symbol_body`, `find_symbol`) wiederholen den Projekt-Header nicht redundant (Token-Sparsamkeit), verweisen aber in ihren Symbol-Headings/Locations direkt auf die konkrete `.cs`-Datei auf der Festplatte.
6. **Modulare Nachbar-Referenzen:**
   Nachbar-DLLs im Verzeichnis der Ziel-DLL werden dem Resolver als Metadaten bereitgestellt, aber nicht kaskadierend volldekompiliert. Benötigt der Agent den Quellcode einer referenzierten DLL, ruft er für diese gezielt `inspect_assembly` auf. AiNetLinter hält mehrere Sessions parallel im Registry-Cache.
7. **Pragmatisches Vorgehen:**
   Sollten bei speziellen DLLs später Kanten oder Probleme auftreten, werden diese gezielt mit neuen Features gelöst, statt vorab spekulative Sonderbehandlungen zu bauen.

## 4. Rigorose Entfernung von obsoletem Code

Folgende Komponenten und Hilfsklassen des bisherigen On-Demand- und Stub-Systems werden ersatzlos gelöscht:

- `AiNetLinter.Mcp.Assemblies.Analysis.Bodies.AssemblyDecompiledBodyResolver`: **Löschen**
- `AiNetLinter.FastTests.Mcp.Tools.AssemblyAnalysis.AssemblyDecompiledBodyResolverTests`: **Löschen** (durch Tests der Projektdekompilierung ersetzen)
- `AiNetLinter.Mcp.Assemblies.Analysis.Bodies.AssemblyBodySyntax`: **Löschen**
- `AiNetLinter.Mcp.Assemblies.Analysis.Bodies.AssemblyDecompilationSourceText`: **Löschen** (die Methoden `MakeSignatureOnlyMethodsParsable`, `CreateStubBody`, etc. entfallen komplett; `WholeProjectDecompiler` erzeugt direkt parsbaren C#-Code)
- `IAssemblyBodyContext.ResolveBodyAsync` & Delegaten: **Entfernen / Vereinfachen** (da `GetSymbolBodyTool` nun direkt `SourceSymbolBodyResolver` auf den Roslyn-Syntaxbäumen der dekompilierten Dokumente ausführt)
- Typ-für-Typ-Dekompilationsschleife in `AssemblyDecompilationAdapter`: **Ersetzen** durch Projektdekompilation via `WholeProjectDecompiler`.

## 5. Betroffene Projektbereiche & Anpassungen

### 5.1 Decompilation & Cache (`AiNetLinter.Mcp.Assemblies.Analysis`)
- **`AssemblyDecompilationAdapter`**:
  Wird auf `WholeProjectDecompiler` umgestellt (analog zu SourceToAI). Führt `DecompileProject` in das Zielverzeichnis der Generation aus (`generation-<guid>/decompile/`).
- **`AssemblyRoslynWorkspaceFactory`**:
  Initialisiert einen `AdhocWorkspace`, hinterlegt die von `WholeProjectDecompiler` erzeugte `.csproj` als Projektpfad und registriert alle erzeugten `.cs`-Dateien mit ihren realen absoluten Dateipfaden auf der Festplatte.
- **`AssemblyDecompilationOptions`**:
  Entfernung von Stubbing-bezogenen Optionen. Timeout und Abbruchtoken bleiben wirksam.

### 5.2 MCP-Tools & Pfadausgabe (`AiNetLinter.Mcp.Tools`)
- **`InspectAssemblyTool` & `InspectAssemblyFormatter`**:
  - Im Text-Header kompakt (1–2 Zeilen) und im JSON-Payload (`InspectAssemblyPayload`) werden explizit ausgewiesen:
    - `decompiledProjectDirectory`: Absoluter Pfad zum Verzeichnis des dekompilierten Projekts.
    - `decompiledProjectPath`: Absoluter Pfad zur generierten `.csproj`-Datei.
    - `decompiledSourceRoot`: Absoluter Pfad zum Quellcode-Ordner (für `rg` und `get_file_tree`).
- **`GetSymbolBodyTool`**:
  - Nutzt für dekompilierte Assembly-Dokumente denselben direkten Syntax-Extraktionspfad (`SourceSymbolBodyResolver`) wie für gewöhnliche Quellcode-Projekte.
  - Symbol-Standorte (`Location.SourceTree.FilePath`) verweisen auf die reale Datei auf der Festplatte.
- **Zusammenspiel mit `get_file_tree`**:
  - Der Agent kann mit `get_file_tree(projectRoot: <decompiledSourceRoot>)` die dekompilierte Projektstruktur untersuchen.

## 6. Muss- und Akzeptanzkriterien

- [ ] **M1:** Fremd-DLLs werden beim ersten Laden vollständig mit allen Methodenkörpern in das Cache-Verzeichnis dekompiliert.
- [ ] **M2:** Nach der Dekompilierung liegen eine gültige `.csproj` und alle `.cs`-Dateien im Cache-Ordner auf der Festplatte.
- [ ] **M3:** Die MCP-Antwort von `inspect_assembly` enthält die absoluten Pfade (`decompiledProjectDirectory`, `decompiledProjectPath`, `decompiledSourceRoot`).
- [ ] **M4:** Der Agent kann mit `rg` im ausgewiesenen `decompiledSourceRoot` nach Code und Textstellen suchen.
- [ ] **M5:** Der Agent kann mit `get_file_tree` auf dem `decompiledSourceRoot` den physischen Dateibaum abfragen.
- [ ] **M6:** `get_symbol_body` liefert den echten dekompilierten Methodenrumpf aus dem geladenen Roslyn-Workspace ohne On-Demand-Zusatzdekompilierung.
- [ ] **M7:** Der bisherige On-Demand-Code (`AssemblyDecompiledBodyResolver`, `AssemblyBodySyntax`, `AssemblyDecompilationSourceText`, Stub-Generierung) ist restlos gelöscht.
- [ ] **M8:** Alle FastTests und IntegrationTests (Kategorie `!Stress`) sind grün; `dotnet build` baut mit 0 Fehlern und 0 Warnungen (`TreatWarningsAsErrors`).

## 7. Non-Goals

- Kein On-Demand-Dekompilieren einzelner Typen oder Methoden mehr.
- Keine künstlichen DLL-Größenbegrenzungen.
- Kein MSBuild-Projekt-Laden (`MSBuildWorkspace`) zur Laufzeit – Roslyn-Projekt wird schlank über `AdhocWorkspace` mit den auf Platte geschriebenen Dateien initialisiert.
- Keine automatische, rekursive Volldekompilierung aller referenzierten Fremd-DLLs (referenzierte DLLs dienen als Metadaten für den Typ-Resolver; Volldekompilierung erfolgt bei Bedarf per separatem `inspect_assembly`-Aufruf).

## 8. Betriebs-, Fehler- und Lebenszeitmodell

- **Cache-Lebenszeit:**
  Der Cache bleibt über Assembly-Fingerprint (Hash/Größe/Timestamp) persistent auf der Festplatte. Wiederholte Aufrufe derselben DLL verwenden das bestehende Projekt ohne Neudekompilierung.
- **Fehlerbehandlung:**
  - DLL nicht gefunden oder ungültig: Schneller Abbruch mit Validierungsfehler.
  - Decompiler-Fehler: Fehlerdiagnose im Session-Status; keine unvollständigen Rumpf-Dateien im `current`-Pointer.
- **Concurrency & Locking:**
  Die bestehende generationenbasierte Veröffentlichung (`generation-<guid>`) und Locks in `AssemblyDecompilationCache` stellen sicher, dass parallele Zugriffe nicht kollidieren.

## 9. Geplante Verifikation

1. **FastTests (Unit/Component):**
   - Test mit Minimal-Assembly: Prüft, dass `WholeProjectDecompiler` das Projekt auf die Platte schreibt und `.csproj` sowie `.cs`-Dateien existieren.
   - Test für Pfadausgabe: `inspect_assembly` liefert absolute Pfade im Text und Payload.
   - Test für `get_symbol_body`: Liefert den echten Body aus der dekompilierten Roslyn-Solution.
   - Regressionstests: Alle bestehenden `inspect_assembly`-, Navigation- und Symboltests anpassen und grün halten.
2. **Build-Verifikation:**
   - `dotnet build` (warnungsfrei).
3. **Gesamttestlauf:**
   - `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`
   - `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`
