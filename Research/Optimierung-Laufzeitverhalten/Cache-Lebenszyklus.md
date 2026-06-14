# Cache-Lebenszyklus: TTL-basierte Eviction & Bereinigung

## Big Picture

Der Analyse-Cache spart 80–95 % der Analysezeit bei wiederholten Runs. Falsche
Cache-Treffer (stale data) wären schlimmer als kein Cache — deshalb mehrere Schutzschichten:

| Schicht | Was schützt sie vor | Status |
| :--- | :--- | :--- |
| Filename-Hash über Solution-Pfad + rules.json | Regeländerungen, andere Solution | ✅ fertig |
| Build-Timestamp im Dateinamen | Logikänderungen im Linter selbst | ✅ fertig |
| `SchemaVersion` in der JSON | Strukturänderungen am Cache-Format | ✅ fertig |
| Checksum-Vergleich pro Datei | Dateiinhalt geändert (der Haupt-Use-Case) | ✅ fertig |
| **TTL-basierte Eviction** | Zeitlich veraltete Daten, Leichen aus alten Runs | 🔲 geplant |

---

## Ist-Zustand (nach letzter Session)

### Dateiname-Schema

```
cache\San.smart.Planner.Platform2-a1b2c3d4-20250614143022.json
      ↑ SolutionName               ↑ hash8   ↑ Build-Timestamp (UTC, Assembly-LastWriteTime)
```

- **hash8**: SHA-256 über `(absoluterSolutionPfad + rulesJsonInhalt)` — invalidiert bei Regeländerung
- **Build-Timestamp**: Last-Write-Zeit der `AiNetLinter.dll` — jeder neue Build legt neuen Cache an,
  `CleanupOldCacheFiles()` löscht dabei die Dateien gleichen Prefixes (anderer Timestamp)
- **Bestehende Option**: `--no-cache` — Cache komplett deaktivieren

### Was fehlt

`CleanupOldCacheFiles()` räumt nur Dateien desselben Prefixes auf (gleiche Solution + gleiche Rules).
Wechselt jemand die Solution oder ändert die rules.json mehrfach, sammeln sich Leichen an.
Außerdem gibt es keine zeitbasierte Grenze — ein 3 Monate alter Cache würde still benutzt.

---

## Soll-Zustand

### Konzept

Beim Start jedes echten Analyse-Runs (nicht `--help`, `--readme`, `--footprint` etc.):

1. **Globale Bereinigung**: Alle `*.json`-Dateien im `cache/`-Verzeichnis, deren
   `LastWriteTimeUtc` älter als TTL ist, werden gelöscht — unabhängig von Prefix.
2. **Cache-Load wie bisher**: Der für diese Session passende Cache wird geladen.
   Fehlt die Datei (weil gerade gelöscht oder neu), startet die Analyse frisch.

```
Startup
  └─► PurgeStale(cacheDir, ttl)         ← löscht alle *.json älter als TTL
        └─► AnalysisCacheManager.Load()  ← lädt (oder startet neu) den passenden Cache
              └─► Analyse ...
                    └─► SaveIfDirty()    ← schreibt Cache; LastWriteTime = jetzt
```

**Warum `LastWriteTimeUtc` statt Filename-Timestamp?**
Der Filename-Timestamp kodiert *wann der Linter gebaut wurde*, nicht *wann die Daten
zuletzt geschrieben wurden*. `SaveIfDirty()` setzt `LastWriteTimeUtc` auf "jetzt" —
das ist die richtige Uhr für "wie frisch sind die Analyseergebnisse".

### Neuer CLI-Parameter

```
--cache-ttl <Minuten>    Cache-Lebensdauer in Minuten. Standard: 60. 0 = unbegrenzt.
```

Kombination mit `--no-cache` ist gültig: `--no-cache` deaktiviert den Cache für diesen Run,
`--cache-ttl` steuert die Bereinigung. Die Bereinigung läuft auch bei `--no-cache`.

---

## Ergänzende Ideen

### A: Verbose-Ausgabe der Bereinigung

Bei `--verbose` ausgeben, was bereinigt wurde:

```
[Cache] 3 Datei(en) bereinigt (älter als 60 Min): OtherSolution-…-20250610.json, …
[Cache] Geladen: San.smart.Planner.Platform2-a1b2c3d4-20250614143022.json (847/1019 Einträge)
```

Minimal-Aufwand, hilfreich beim Debugging — sofort erkennbar ob der Cache aktiv ist.

### B: Cache-Trefferquote loggen (--verbose)

In `LinterEngine` einen einfachen Zähler mitführen (`cacheHits`, `cacheMisses`) und am
Ende ausgeben:

```
[Cache] 847 Treffer / 172 Misses (83 % Hitrate) — 0,4 s Analysezeit gespart
```

Kein Umbau nötig — `AnalysisCacheManager.TryGet()` gibt bereits `bool` zurück.

### C: LRU-Touch (optional, nachrangig)

Bei erfolgreichem Cache-Treffer die `LastWriteTime` der Datei aktualisieren:

```csharp
File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
```

Effekt: Häufig benutzte Caches bleiben auch bei kurzem TTL am Leben.
**Bewertung**: Overhead (ein File-Write pro Run), Nutzen gering solange TTL ≥ 60 Min.
Erstmal weglassen, bei Bedarf nachrüsten.

### D: Maximale Dateianzahl als Fallback

Als ergänzende Grenze neben TTL: wenn nach der TTL-Bereinigung noch > N Dateien
im Cache-Verzeichnis liegen, die ältesten löschen bis N erreicht ist.
**Bewertung**: Edge-Case (wäre nur relevant bei extrem vielen Projekten oder TTL=0).
Erstmal weglassen.

---

## Konkrete Code-Vorschläge

### 1. `CliOptionFactory` — neue Option

```csharp
internal static Option<int> CreateCacheTtlOption()
{
    var option = new Option<int>("--cache-ttl")
    {
        Description = "Cache-Lebensdauer in Minuten (0 = unbegrenzt). Standard: 60.",
        DefaultValueFactory = _ => 60,
    };
    option.AddValidator(r =>
    {
        if (r.GetValueOrDefault<int>() < 0)
            r.ErrorMessage = "--cache-ttl darf nicht negativ sein.";
    });
    return option;
}
```

### 2. `LinterArgs` — neues Property

```csharp
/// <summary>
/// Cache-Lebensdauer in Minuten. 0 = unbegrenzt. Standard: 60.
/// </summary>
public int CacheTtlMinutes { get; init; } = 60;
```

### 3. `CliCommandBuilder` — Option einbinden

```csharp
// Im Options-Record ergänzen:
Option<int> CacheTtl,

// In Build() ergänzen:
options.CacheTtl,   // zur RootCommand hinzufügen

// In CreateOptions() ergänzen:
CliOptionFactory.CreateCacheTtlOption(),

// In Parse() ergänzen:
CacheTtlMinutes: parseResult.GetValue(options.CacheTtl),
```

### 4. `AnalysisCacheManager.Load()` — TTL-Parameter + globale Bereinigung

```csharp
public static AnalysisCacheManager Load(
    string exeDir, string solutionPath, string rulesJsonContent, TimeSpan cacheTtl)
{
    var cacheDir = Path.Combine(exeDir, "cache");
    Directory.CreateDirectory(cacheDir);

    PurgeStale(cacheDir, cacheTtl);                                    // ← NEU: global

    var prefix = BuildCacheFilePrefix(solutionPath, rulesJsonContent);
    var fileName = $"{prefix}-{GetBuildTimestamp()}.json";
    var cachePath = Path.Combine(cacheDir, fileName);

    CleanupOldCacheFiles(cacheDir, prefix, fileName);                  // ← bleibt: same-prefix

    var cache = TryReadCache(cachePath) ?? new AnalysisCacheFile();
    return new AnalysisCacheManager(cachePath, cache);
}

private static void PurgeStale(string cacheDir, TimeSpan ttl)
{
    if (ttl == TimeSpan.Zero) return;   // 0 Minuten = unbegrenzt, nichts löschen

    var cutoff = DateTime.UtcNow - ttl;
    foreach (var file in Directory.EnumerateFiles(cacheDir, "*.json"))
    {
        try
        {
            if (File.GetLastWriteTimeUtc(file) < cutoff)
                File.Delete(file);
        }
        catch { /* ignorieren — wird beim nächsten Run erneut versucht */ }
    }
}
```

### 5. `LinterEngine.BuildCache()` — TTL durchreichen

```csharp
// Signatur anpassen:
private AnalysisCacheManager? BuildCache(SourceFileCatalog? catalog, string path, int cacheTtlMinutes)
{
    if (string.IsNullOrEmpty(_rulesJsonContent)) return null;

    var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
    var solutionPath = catalog?.Solution?.FilePath ?? path;
    var ttl = cacheTtlMinutes > 0
        ? TimeSpan.FromMinutes(cacheTtlMinutes)
        : TimeSpan.Zero;

    return AnalysisCacheManager.Load(exeDir, solutionPath, _rulesJsonContent, ttl);
}
```

### 6. `LinterEngine.RunAsync()` — Signatur anpassen

Alle drei Überladungen erhalten `int cacheTtlMinutes = 60`:

```csharp
public async Task<IReadOnlyCollection<RuleViolation>> RunAsync(
    string path, bool noCache = false, int cacheTtlMinutes = 60)

public async Task<IReadOnlyCollection<RuleViolation>> RunAsync(
    SourceFileCatalog catalog, bool noCache = false, int cacheTtlMinutes = 60)

public async Task<IReadOnlyCollection<RuleViolation>> RunAsync(
    Solution solution, bool noCache = false, int cacheTtlMinutes = 60)
```

### 7. `Program.cs` — Aufrufe anpassen

Alle drei `engine.RunAsync(..., args.NoCache)`-Stellen werden zu:

```csharp
engine.RunAsync(..., args.NoCache, args.CacheTtlMinutes)
```

---

## Tests

### Unit-Test: PurgeStale löscht alte Dateien

```csharp
[Fact]
public void PurgeStale_DeletesFilesOlderThanTtl()
{
    // Arrange: Cache-Datei anlegen, LastWriteTime künstlich in die Vergangenheit setzen
    var cacheDir = Path.Combine(_tempDir, "cache");
    Directory.CreateDirectory(cacheDir);
    var oldFile = Path.Combine(cacheDir, "old-cache.json");
    File.WriteAllText(oldFile, "{}");
    File.SetLastWriteTimeUtc(oldFile, DateTime.UtcNow.AddMinutes(-90));

    var freshFile = Path.Combine(cacheDir, "fresh-cache.json");
    File.WriteAllText(freshFile, "{}");
    // freshFile: LastWriteTime = jetzt (Standard)

    // Act: Load mit TTL 60 Minuten
    AnalysisCacheManager.Load(_tempDir, "Dummy.sln", "{}", TimeSpan.FromMinutes(60));

    // Assert
    Assert.False(File.Exists(oldFile));    // alt → gelöscht
    Assert.True(File.Exists(freshFile));  // frisch → bleibt
}
```

### Unit-Test: TTL=0 löscht nichts

```csharp
[Fact]
public void PurgeStale_WithZeroTtl_DeletesNothing()
{
    var cacheDir = Path.Combine(_tempDir, "cache");
    Directory.CreateDirectory(cacheDir);
    var oldFile = Path.Combine(cacheDir, "old.json");
    File.WriteAllText(oldFile, "{}");
    File.SetLastWriteTimeUtc(oldFile, DateTime.UtcNow.AddDays(-7));

    AnalysisCacheManager.Load(_tempDir, "Dummy.sln", "{}", TimeSpan.Zero);

    Assert.True(File.Exists(oldFile));   // TTL=0 → nichts löschen
}
```

---

## Umsetzungsreihenfolge

1. `AnalysisCacheManager.Load()` — TTL-Parameter + `PurgeStale()` (inkl. Unit-Tests)
2. `LinterArgs.CacheTtlMinutes` + `CliOptionFactory.CreateCacheTtlOption()`
3. `CliCommandBuilder` — Option und Parsing verdrahten
4. `LinterEngine.RunAsync()` + `BuildCache()` — Signatur anpassen
5. `Program.cs` — alle drei Aufruf-Stellen anpassen
6. Integrations-Test: zwei Runs, zwischen denen `LastWriteTime` manipuliert wird —
   zweiter Run darf den manipulierten Cache nicht laden

---

## Offene Fragen / Empfehlungen

| Frage | Empfehlung |
| :--- | :--- |
| Default TTL 60 Min sinnvoll? | Für Agent-Loops (jede Minute) ja. Für manuelle Nutzung eher 4 h. 60 Min als Kompromiss OK. |
| TTL auch in rules.json konfigurierbar? | Nein — ist Tool-Laufzeit-Concern, kein Projekt-Concern (analog `--no-cache`). |
| Verbose-Ausgabe für Bereinigung? | Ja, aber nachrangig — nach Basis-Implementierung als separater Commit. |
| `PurgeStale` auch bei `--no-cache`? | Ja — Bereinigung und Cache-Nutzung sind orthogonale Concerns. |
