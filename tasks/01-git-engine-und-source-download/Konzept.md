---
status: draft
task: 01-git-engine-und-source-download
priority: 1
---

# Konzept: Schlanke GitEngine & Repository-Bereitstellung

## 1. Ziel & Nutzen

Die Bereitstellung externer Quellcode-Repositories für AiNetLinter wird von einer 10-stufigen, fragilen Abstraktionskaskade auf eine einzige, robuste und wartbare Komponente (**`GitEngine`**, ca. 150–200 Zeilen C#) zurückgeführt.

**Kernnutzen:**
- Beseitigung des P0-Abbruchfehlers (`GIT-PROGRESS-ABORT`), durch den bisher jeder echte Netzwerk-Clone über HTTP(S) scheiterte.
- 100 % verlässliche, transparente Fehlerweiterleitung bei Git-Problemen statt Verschlucken von Fehlern in tiefen Abstraktionsschichten.
- Echte Integrationstests mit der lokalen `git.exe` gegen Bare-Repositories (`git init --bare`) garantieren die Funktionsfähigkeit in der realen Welt, statt rein synthetischer Mocks.
- Drastische Reduktion von Komplexität und Wartungsaufwand (Entfernung von ca. 1.200 Zeilen Overkill-Code).

---

## 2. Betroffene Projektbereiche & Ist-Zustand

### 2.1 Aktuelle Problem-Struktur (Die 10-Schichten-Kaskade)
Bislang durchläuft ein Klon- oder Update-Vorgang folgende Schichten in `src/AiNetLinter/Mcp/Assemblies/ExternalSource/`:
1. `GiteaGitRepositoryTransport.cs`
2. `ExternalSourceRepositoryAcquirer.cs`
3. `ExternalSourceRepositoryCache.cs`
4. `ExternalResourceRegistry.cs` (mit simulierten Disk- und RAM-Byte-Countern)
5. `SourceSnapshotRegistry.cs`
6. `AssemblySourceProviderCoordinator.cs`
7. `ExternalSourceGitProcessOutputPolicy.cs` (P0-Bug: bricht bei jeder `stderr`-Progress-Zeile ab)
8. `ExternalSourceRepositoryCheckoutStatus.cs` (stuft selbst Standard-Git-Zustände als `Dirty` ein)
9. `ExternalSourceRepositoryFailurePolicy.cs`
10. `ExternalSourceCacheLeaseProbeCommand.cs` (243 Zeilen Test-Backdoor in `Program.cs` verdrahtet)

### 2.2 Ziel-Struktur (Kompakt & Direkt)
- **Neu**: `src/AiNetLinter/Mcp/Assemblies/ExternalSource/GitEngine.cs` (alleinige Verantwortung für Git-Prozesse, ~150–200 Zeilen).
- **Neu**: `src/AiNetLinter/Mcp/Assemblies/ExternalSource/ExternalSourceStorage.cs` (deterministisches Dateisystem-Mapping: URL-Hash -> Cache-Pfad + einfaches LRU-Sweep).
- **Löschen**: Die überflüssigen Koordinatoren, Leases, Quarantänen und Fake-Ressourcen-Registries.
- **Löschen**: Win32-Interop-Prozessbündel (`ExternalSourceGitProcessNativeMethods.cs`).
- **Löschen**: CLI-Test-Backdoor `external-source-cache-lease-probe` aus `Program.cs`.

---

## 3. Muss-Kriterien & Akzeptanzkriterien

### 3.1 Muss-Kriterien (Funktional)
1. **Robuster Git-Clone & Update-Workflow**:
   - `GitEngine` stellt folgende Methoden bereit:
     ```csharp
     public sealed record GitResult(bool Succeeded, string? ErrorMessage = null, int ExitCode = 0);

     public sealed class GitEngine
     {
         public Task<GitResult> EnsureRepositoryAsync(
             string repositoryUrl,
             string targetDirectory,
             string? branchOrCommit = null,
             CancellationToken cancellationToken = default);

         public Task<string?> GetHeadRevisionAsync(
             string repositoryDirectory,
             CancellationToken cancellationToken = default);

         public Task<GitResult> FetchAndResetAsync(
             string repositoryDirectory,
             CancellationToken cancellationToken = default);
     }
     ```
   - Aufruf von `git clone` und `git fetch` erfolgt stets mit `--quiet` bzw. `--no-progress`.
   - Befehlsfolge bei Clone: `git clone --quiet --single-branch --no-tags -- <url> <targetDir>`
   - Befehlsfolge bei Update/Refresh: `git fetch --quiet --no-tags` gefolgt von `git reset --hard origin/<default-branch>` oder dem konkreten Commit/Tag.
   - Umgebungsvariablen für den Child-Prozess:
     - `GIT_TERMINAL_PROMPT=0` (kein interaktives Blockieren bei fehlenden Anmeldedaten)
     - `LC_ALL=C` und `LANG=C` (stabile, englischsprachige Git-Meldungen unabhängig vom Betriebssystem)
2. **Kein Fortschritts-Abbruch**:
   - Standard-Ausgaben auf `stderr` (z. B. `remote: Counting objects...`, `Receiving objects...`) führen **niemals** zum Fehlschlag, wenn `ExitCode == 0` ist.
3. **Echte Fehler-Transparenz**:
   - Scheitert ein Git-Kommando (`ExitCode != 0` oder Timeout), wird der vollständige `stderr`-Text ungefiltert im `GitResult.ErrorMessage` zurückgegeben.
4. **Deterministische Cache-Pfade & einfaches Cleanup**:
   - Cache-Ablage: `<CacheRoot>/<RepoKey>/`, wobei `RepoKey` der SHA-256-Hash (oder URL-Slug) der normalisierten Repository-URL ist.
   - Ein Verzeichnis ist gültig, wenn es existiert und einen intakten `.git`-Ordner besitzt.
   - Kein In-Memory-Lease-Tracking für statische Git-Checkouts auf der Festplatte.
   - Ein simples LRU-Cleanup: Wenn die Gesamtfestplattengröße des Cache-Roots das konfigurierte Limit (`MaxDiskBytes`, Default 512 MB) überschreitet, werden die am längsten nicht verwendeten Repository-Ordner (sortiert nach `Directory.GetLastAccessTimeUtc`) gelöscht.
5. **Authentifizierung**:
   - Unterstützung von Standard-Git-Credentials (System-Credential-Store) sowie optionalen Token/Basic-Auth-Konfigurationen über Umgebungsvariablen (`GIT_ASKPASS`).
6. **Invariante**:
   - Der Zugriff auf externe Repositories erfolgt rein lesend (`read-only`). AiNetLinter pusht niemals.

### 3.2 Akzeptanzkriterien (Verifikation)
- [ ] Ein echter Integrationstest in `AiNetLinter.IntegrationTests` erzeugt ein lokales Git-Bare-Repo (`git init --bare`), committet eine Dummy-Datei und lässt `GitEngine.EnsureRepositoryAsync` erfolgreich dagegen laufen (< 500 ms Laufzeit).
- [ ] Ein zweiter Aufruf gegen dasselbe Verzeichnis erkennt den vorhandenen Stand und führt einen erfolgreichen Schnell-Check/Fetch durch.
- [ ] Ein Klonversuch gegen eine ungültige URL liefert eine sprechende Fehlermeldung mit der Original-Git-Meldung.
- [ ] Alle Unit- und Integrationstests laufen fehlerfrei (`TreatWarningsAsErrors = true`).

---

## 4. Non-Goals (Scope-Grenzen)

- **Kein LibGit2Sharp**: Keine Einführung schwergewichtiger nativer C-Binaries; wir bleiben bei einem schlanken CLI-Wrapper um `git.exe`.
- **Keine In-Memory-Ressourcen-Drosseln**: Keine künstlichen Byte-Zähler im RAM für Quellcodedateien auf der Festplatte.
- **Keine Roslyn-Projekt-Analyse in diesem Task**: Die Zuordnung von Assemblys zu `.csproj` und das Öffnen von Projektmappen ist Gegenstand von Task `02`.
- **Keine MCP-Tool-Änderungen**: Das MCP-Wire-Format und die Textausgabe werden in Task `03` behandelt.

---

## 5. Geplante Verifikation

1. **Automatisierte Tests**:
   - Neuer Test: `AiNetLinter.IntegrationTests/ExternalSource/GitEngineIntegrationTests.cs` (echter `git.exe`-Prozess gegen lokales Dateisystem).
   - Ausführung: `dotnet test src/AiNetLinter.IntegrationTests --filter Category=Integration`
2. **Build-Prüfung**:
   - `dotnet build` (warnungs- und fehlerfrei).

---

## 6. Arbeitsgedächtnis (nur Draft)

### Kontextanker & Evidenz
- Aus `tasks/assembly-analyse-verbesserungen/audit-findings-und-ideen.md`:
  - P0-Befund `GIT-PROGRESS-ABORT`: [ExternalSourceGitProcessOutputPolicy.cs:38-42](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/ExternalSource/ProcessExecution/ExternalSourceGitProcessOutputPolicy.cs#L38-L42) bricht ab, wenn Git Fortschritt meldet.
  - P1-Befund `GIT-LOCALE-DEPENDENCY`: Fehlendes `LC_ALL=C`.
  - P2-Befund `PROD-TEST-BACKDOOR`: `ExternalSourceCacheLeaseProbeCommand.cs` in `Program.cs`.
  - P2/LOW-Befund `Dead Code`: 34 Win32-Interop-Felder in `ExternalSourceGitProcessNativeMethods.cs`.
- Aus dem Chat:
  - Ralf: „10 Klassen für Git Clone ist eindeutig sinnfrei. [...] Am Ende liegt irgendwo im Dateisystem halt das Git Repo.“
  - Klare Entscheidung: Keine native Dependency (LibGit2Sharp), sondern eine einzige, testbare Klasse `GitEngine.cs`.

### Offene Entscheidungen & Fragen
- **Reales Netzwerk-/HTTP-Git-Test-Repository**:
  - *Hintergrund*: Der fatale `GIT-PROGRESS-ABORT`-Bug trat spezifisch bei echten HTTP-Netzwerk-Clones auf (`remote: Counting objects...`, `Receiving objects...`). Ein reiner Dateisystem-Clone (`file://`) verhält sich anders als der Git-Smart-HTTP-Transfer.
  - *Option 1*: Ein dediziertes Test-Repository auf dem internen Git-Server.
  - *Option 2*: Ein autarker lokaler HTTP-Git-Fixture-Server in `AiNetLinter.IntegrationTests` (z. B. Kestrel / `git http-backend` auf `http://127.0.0.1:<port>`), damit Tests auch offline / in CI deterministisch laufen.
  - *Option 3 (Empfohlen)*: Beides – ein autarker lokaler HTTP-Test in der Suite + ein optionaler E2E-Smoke-Test gegen ein konfiguriertes reales Git-Repository.

### Zu entfernende / zu ersetzende Alt-Dateien
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/Providers/GiteaGitRepositoryTransport.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/ProcessExecution/ExternalSourceGitProcessOutputPolicy.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/ProcessExecution/ExternalSourceGitProcessNativeMethods.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/Repository/ExternalSourceRepositoryAcquirer.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/Repository/ExternalSourceRepositoryCache.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/Repository/ExternalSourceRepositoryCacheReader.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/Repository/ExternalSourceRepositoryCacheWriter.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/Repository/ExternalSourceRepositoryCacheReuse.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/Repository/ExternalSourceRepositoryCacheMaterializer.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/Repository/ExternalSourceRepositoryCacheWriterLifecycle.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/Repository/ExternalSourceRepositoryPathGuard.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/Repository/ExternalSourceRepositoryCheckoutStatus.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/Repository/ExternalSourceRepositoryFailurePolicy.cs`
- `src/AiNetLinter/Commands/ExternalSourceCacheLeaseProbeCommand.cs`
