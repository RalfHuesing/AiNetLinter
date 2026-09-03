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
4. Hohe Fehlertoleranz und Ausfallsicherheit gegenüber fehlerhaftem Decompilat, langen Pfaden und Datei-Locks.

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
4. **Keine hartcodierten Limits:**
   Es gibt weder für Dateigrößen noch für Member- oder Typmengen starre Grenzwerte. Alle Obergrenzen und Timeouts sind konfigurierbar.
5. **Fehlertoleranz ("Partial Decompilation"):**
   Kann eine einzelne Datei oder ein Typ nicht fehlerfrei dekompiliert werden oder enthält Syntaxfehler, wird der Snapshot **nicht** verworfen. Das System arbeitet resilient und stellt alle verfügbaren Dateien (auch wenn z. B. nur 70 % fehlerfrei sind) bereit.
6. **Token-sparsame, transparente Pfadausgabe für Agenten:**
   - Der Einstiegs-Toolcall `inspect_assembly` weist im Header und im JSON-Payload kompakt die absoluten Pfade (`decompiledSourceRoot`, `decompiledProjectPath`, `decompiledProjectDirectory`) aus.
   - Mit dem ausgewiesenen `decompiledSourceRoot` kann der Agent direkt mit `rg` suchen oder `get_file_tree(projectRoot: decompiledSourceRoot)` ausführen.
   - Nachfolgende Abfragen (z. B. `get_symbol_body`, `find_symbol`) wiederholen den Projekt-Header nicht redundant (Token-Sparsamkeit), verweisen aber in ihren Symbol-Headings/Locations direkt auf die konkrete `.cs`-Datei auf der Festplatte.
7. **Modulare Nachbar-Referenzen:**
   Nachbar-DLLs im Verzeichnis der Ziel-DLL werden dem Resolver als Metadaten bereitgestellt, aber nicht kaskadierend volldekompiliert. Benötigt der Agent den Quellcode einer referenzierten DLL, ruft er für diese gezielt `inspect_assembly` auf. AiNetLinter hält mehrere Sessions parallel im Registry-Cache.
8. **Pragmatisches Vorgehen:**
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
  Wird auf `WholeProjectDecompiler` umgestellt (analog zu SourceToAI). Führt `DecompileProject` in ein Staging-Verzeichnis der Generation aus (`generation-<guid>.tmp/decompile/`).
- **`AssemblyRoslynWorkspaceFactory`**:
  Initialisiert einen `AdhocWorkspace`, hinterlegt die von `WholeProjectDecompiler` erzeugte `.csproj` als Projektpfad und registriert alle erzeugten `.cs`-Dateien mit ihren realen absoluten Dateipfaden auf der Festplatte.
- **Resilienz in `ValidateCompilation`**:
  Syntaxfehler im generierten C#-Code führen **nicht** zum Verwerfen (`Dispose()`) des Snapshots, sondern stufen den Session-Status auf `Partial` oder `Degraded` herab. Die fehlerfreien Typen und alle Dateien auf der Platte bleiben voll nutzbar.
- **Konfigurierbarer Cache-Root & Timeouts**:
  - Konfigurierbar in `appsettings.json` (z. B. Sektion `AssemblyAnalysis` mit `CacheRoot` und `DecompilationTimeoutSeconds`).
  - Standard-Cache-Root wird als relativer, kompakter Pfad (z. B. `cache/asm`) ausgeliefert, um die 260-Zeichen-Grenze (`MAX_PATH`) unter Windows zu schonen.
  - Unterstützt relative Pfade (z. B. `../../cache`) und absolute Pfade (z. B. `C:\cache`).
  - Timeouts sind großzügig bemessen (z. B. Default 180s) und konfigurierbar.

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
- [ ] **M7:** Treten in einzelnen dekompilierten Dateien Syntax- oder Typfehler auf, wird der Snapshot nicht verworfen; die Session verbleibt im Status `Partial`/`Degraded` mit abfragbaren Teilergebnissen.
- [ ] **M8:** Der Cache-Pfad und Dekompilierungs-Timeouts sind über `appsettings.json` konfigurierbar (relative und absolute Pfade).
- [ ] **M9:** Dekompilierung erfolgt atomar über Staging (`.tmp`); unvollständige Läufe werden nicht als gültiger Cache veröffentlicht.
- [ ] **M10:** Cache-Bereinigung fängt Dateisperren (z. B. durch `rg`) als Best-Effort ab, ohne den Server zum Absturz zu bringen.
- [ ] **M11:** Der bisherige On-Demand-Code (`AssemblyDecompiledBodyResolver`, `AssemblyBodySyntax`, `AssemblyDecompilationSourceText`, Stub-Generierung) ist restlos gelöscht.
- [ ] **M12:** Alle FastTests und IntegrationTests (Kategorie `!Stress`) sind grün; `dotnet build` baut mit 0 Fehlern und 0 Warnungen (`TreatWarningsAsErrors`).

## 7. Non-Goals

- Kein On-Demand-Dekompilieren einzelner Typen oder Methoden mehr.
- Keine künstlichen DLL-Größenbegrenzungen.
- Kein MSBuild-Projekt-Laden (`MSBuildWorkspace`) zur Laufzeit – Roslyn-Projekt wird schlank über `AdhocWorkspace` mit den auf Platte geschriebenen Dateien initialisiert.
- Keine automatische, rekursive Volldekompilierung aller referenzierten Fremd-DLLs (referenzierte DLLs dienen als Metadaten für den Typ-Resolver; Volldekompilierung erfolgt bei Bedarf per separatem `inspect_assembly`-Aufruf).

## 8. Betriebs-, Fehler- und Resilienzmodell

- **Cache-Lebenszeit & Persistenz:**
  Der Cache bleibt über Assembly-Fingerprint (Hash/Größe/Timestamp) persistent auf der Festplatte. Wiederholte Aufrufe derselben DLL verwenden das bestehende Projekt ohne Neudekompilierung.
- **Atomare Staging-Veröffentlichung:**
  Die Dekompilierung schreibt zunächst in ein temporäres Staging-Verzeichnis (`generation-<guid>.tmp`). Erst nach erfolgreichem Abschluss wird es umbenannt und der Pointer `current.json` atomar aktualisiert.
- **Resiliente Cache-Bereinigung (Lock-Toleranz):**
  Wenn `AssemblyCacheCleanup` veraltete Generationen löschen will und Dateien durch Hintergrundwerkzeuge (`rg`, Virenscanner, Editor) gelockt sind, wird die `IOException` abgefangen und die Löschung auf den nächsten Zyklus vertagt.
- **Fehlertoleranz bei unvollständiger Dekompilierung:**
  Auch unvollständig oder fehlerhaft dekompilierte Typen (z. B. bei exotischen VB.NET- oder Obfuskationsstrukturen) bringen den Roslyn-Snapshot nicht zum Absturz. Die Session wechselt in `Partial`/`Degraded`, markiert Diagnosen und liefert alle funktionierenden Typen und Dateien aus.
- **BCL- & Framework-Resilienz:**
  `UniversalAssemblyResolver` läuft mit `throwOnError: false`. Divergenzen zwischen .NET 9 Runtime und Ziel-Frameworks (.NET Framework 4.8 etc.) werden als Warnungen protokolliert, blockieren aber nicht die Code-Analyse.
- **Saubere Cancellation:**
  Abbrüche (z. B. Client-Timeout) brechen `WholeProjectDecompiler` sofort via `CancellationToken` ab und räumen Staging-Verzeichnisse auf.

## 9. Geplante Verifikation

1. **FastTests (Unit/Component):**
   - Test mit Minimal-Assembly: Prüft, dass `WholeProjectDecompiler` das Projekt auf die Platte schreibt und `.csproj` sowie `.cs`-Dateien existieren.
   - Test für Pfadausgabe: `inspect_assembly` liefert absolute Pfade im Text und Payload.
   - Test für `get_symbol_body`: Liefert den echten Body aus der dekompilierten Roslyn-Solution.
   - Test für Resilienz/Fehlertoleranz: Assembly mit nicht-kompilierbaren Syntaxelementen stürzt nicht ab, sondern liefert `Partial`/`Degraded`-Snapshot.
   - Test für Cache-Konfiguration: Konfigurierter `CacheRoot` (relativ und absolut) aus `appsettings.json` wird korrekt aufgelöst.
   - Test für atomare Veröffentlichung: Abgebrochener Staging-Lauf publiziert keinen `current.json`-Pointer.
   - Regressionstests: Alle bestehenden `inspect_assembly`-, Navigation- und Symboltests anpassen und grün halten.
2. **Build-Verifikation:**
   - `dotnet build` (warnungsfrei).
3. **Gesamttestlauf:**
   - `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`
   - `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`
