# Audit-Befunde, Live-Test & Konzeptdiskussion: Paginierung, Filter & Agent-UX

> **Datum**: 2026-09-04  
> **Task**: [tasks/assembly-analyse-verbesserungen](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/tasks/assembly-analyse-verbesserungen)  
> **Geprüfter Git-Stand**: `ff36df76` / Release `v1.0.166`  
> **Rolle**: Unabhängiger Auditor & Agent-UX-Tester  
> **Hinweis zum Datenschutz / IP**: Keine Nennung geschützter Produktnamen oder externer proprietärer Quellcodes; alle Testfälle sind anonymisiert und verallgemeinert.

---

## 1. Übersicht & Kontext

Über Nacht hat ein autonomes Multi-Agenten-System den Task *Assembly-Analyse-Verbesserungen* in drei Epics durchlaufen und 6.551 Zeilen Code hinzugefügt. Das Kernproblem (unbemerkter Fallback auf Dekompilation trotz konfigurierter Git-Quellen, fehlende Mehrmandanten-/Cache-Sicherheit und fehlende Suchfunktionen) wurde angegangen.

Dieses Dokument bündelt:
1. Die **statischen Code-Audit-Befunde** (Architektur, Git-Output, IPC).
2. Die **Live-Test-Befunde aus Agenten-Sicht (360° Usage Review)** beim praktischen Aufruf der MCP-Tools gegen reale externe Assemblies.
3. Die **Konzeptbewertung und Ausarbeitung** zu **Paginierung, deterministischer Sortierung und Filterung (Kategorie/Regex)**.
4. Einen **harmonisierten Implementierungs-Standard** (`PaginationArgs` / `PagedResult<T>`).

---

## 2. Statische Code-Audit-Befunde (Ist-Zustand)

### Übersicht

| Priorität | ID | Bereich / Datei | Kernproblem | Auswirkung im Betrieb |
|:---|:---|:---|:---|:---|
| **P0** | `GIT-PROGRESS-ABORT` | [ExternalSourceGitProcessOutputPolicy.cs:38-42](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/ExternalSource/ProcessExecution/ExternalSourceGitProcessOutputPolicy.cs#L38-L42) | Git-Clone wertet normalen Netzwerk-Progress auf `stderr` als Fehler. | **Jeder echte Remote-Clone bricht ab**, obwohl Exit-Code 0 ist. |
| **P1** | `GIT-LOCALE-DEPENDENCY` | [GiteaGitRepositoryTransport.cs:420-430](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/ExternalSource/Providers/GiteaGitRepositoryTransport.cs#L420-L430) | Keine `LC_ALL=C` / `LANG=C` Umgebungsvariablen für Git-Child-Prozesse. | Auf nicht-englischen Systemen weichen Git-Strings ab (z. B. `Klone nach...`). |
| **P2** | `JSON-DOM-TRIM-LOOP` | [AssemblyAnalysisResponse.cs:203-207](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResponse.cs#L203-L207) | Iterative Byte-Serialisierung in While-Schleife mit nachträglicher Envelope-Rekonstruktion. | Hohe CPU-/Memory-Last; hat 5 Korrekturschleifen im nächtlichen Lauf verursacht. |
| **P2** | `PROD-TEST-BACKDOOR` | [Program.cs:69-74](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Program.cs#L69-L74) / [ExternalSourceCacheLeaseProbeCommand.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Commands/ExternalSourceCacheLeaseProbeCommand.cs) | 243 Zeilen Test-Probe direkt im CLI-Haupteinstiegspunkt verdrahtet. | Verletzung von Clean Architecture; Testcode in der Produktiv-Binary. |

---

## 3. Live-Test-Befunde & 360°-Usage-Review (Agenten-Perspektive)

Die neuen und erweiterten MCP-Tools wurden in Version `1.0.166` interaktiv gegen zwei reale externe Assemblies getestet:
- **Testfall A (externe Assembly A - mit erwarteter Git-Quellcode-Bindung)**: Suche nach Datenbank-Zugriffsfunktionen.
- **Testfall B (externe Assembly B - rein dekompiliert)**: Lokalisierung der Logik zum Speichern von Belegen/Aufträgen.

### Befund-Matrix des Live-Tests

| Priorität | ID | Betroffenes Tool | Beobachtetes Verhalten | Auswirkung auf den Agenten |
|:---|:---|:---|:---|:---|
| **P0** | `MCP-TEXT-BLACK-HOLE` | `search_assembly`, `get_assembly_context`, `get_call_tree` | Sobald das 16-KiB-Wire-Budget überschritten wird, ersetzt der Server den gesamten Textinhalt durch: `"[ASSEMBLY] StructuredContent ist die kanonische Nutzlast; die Textdarstellung wurde wegen des gemeinsamen Wire-Budgets gekürzt."` | **Vollständige Erblindung des Agenten**: Die meisten LLM-Clients (Cursor, Antigravity, Claude Desktop) lesen primär `content[0].text`. Der Agent sieht **0 Treffer**, obwohl Treffer vorhanden sind. |
| **P0** | `EXTERNAL-SOURCE-HEALTH-MISLEADING` | [AssemblyHealthProjection.cs:72](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/ServerMaintenance/Projection/AssemblyHealthProjection.cs#L72) & [ExternalSourceGitProcessOutputPolicy.cs:38-42](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/ExternalSource/ProcessExecution/ExternalSourceGitProcessOutputPolicy.cs#L38-L42) | DLL A meldet `Mapping-Status: not-configured` und `fallbackReason: provider-unavailable`, obwohl `external-sources.json` vollkommen korrekt konfiguriert ist. | **Irreführung des Nutzers & stiller Fallback**: 1. `AssemblyHealthProjection` setzt bei fehlgeschlagener Snapshot-Erzeugung pauschal `source == null ? "not-configured" : "verified"`. 2. Der eigentliche Git-Clone scheitert am `stderr`-Progress-Check (`GIT-PROGRESS-ABORT`). 3. Der Daemon lädt geänderte JSON-Konfigurationen nicht zur Laufzeit neu. |
| **P1** | `COMPOSITE-TEXT-EMPTY` | `get_assembly_context` | Die Textdarstellung (`RenderText`) gibt nur Eigenschaftsnamen aus (`Abschnitt: metrics`), aber keinerlei fachlichen Inhalt (weder Typen, Signaturen noch Metrikwerte). | Das Composite-Tool liefert im Textmodus keinen Mehrwert. Der Agent muss erst recht Einzeltools aufrufen. |
| **P1** | `DATA-ACCESS-LINQ-POLLUTION` | `search_assembly (data_access)` | Der Regex-Filter enthält `\bSELECT\b` (case-insensitive). In C# matcht das gewöhnliche LINQ-Aufrufe (`.Select(...)` und `select x`). | 80 % der Suchergebnisse für `data_access` waren einfache LINQ-Listenoperationen und keine Datenbankaufrufe. |
| **P2** | `FIND-SYMBOL-ABSOLUTE-PATH` | `find_symbol` (Assembly-Modus) | Gibt vor jedem Treffer den vollen ~180 Zeichen langen internen Cache-Pfad (`C:\Daten\Tools\AiNetLinter-win-x64\cache\asm...`) aus. | Hoher unnötiger Token-Verbrauch; `search_assembly` macht es mit relativen Pfaden bereits besser. |
| **P2** | `SEARCH-FILEFILTER-ERGONOMICS` | `search_assembly` (`fileFilter`) | Akzeptiert ausschließlich Regex (z. B. `(?<!Resources)\.cs$`). | Für LLMs fehleranfällig; einfache Glob-Muster (`*.cs`, `!*Resources*`) wären robuster. |
| **P3** | `BODY-TRUNCATION-NO-OFFSET` | `get_symbol_body` | Bricht bei 82 Zeilen mit Hinweis auf `maxBodyLines` ab. | Kein Paging (`offset` / `startLine`) vorhanden; um Zeile 80-100 zu lesen, müssen alle 100 Zeilen erneut übertragen werden. |

---

### Detailanalyse der Live-Findings

#### 1. `MCP-TEXT-BLACK-HOLE` (P0 – Höchste Dringlichkeit)
* **Beobachtung**:
  Wird `search_assembly` mit `searchKind: "data_access"` ohne explizites Limit aufgerufen, liefert das Tool:
  ```text
  [ASSEMBLY] StructuredContent ist die kanonische Nutzlast; die Textdarstellung wurde wegen des gemeinsamen Wire-Budgets gekürzt.
  ```
  Dasselbe geschieht bei `get_assembly_context` und `get_call_tree` (selbst bei `maxDepth: 1`).
* **Ursache**:
  Der in Epic 1 eingeführte Wire-Budget-Trimmer misst die aggregierte Größe von Text und JSON. Überschreitet JSON das Budget, wird der Text schlicht weggeworfen, um Bytes zu sparen.
* **Architektur-Fehler**:
  Das MCP-Protokoll erlaubt zwar `StructuredContent`, aber fast alle LLM-Agenten interpretieren das Textfeld als primären Prompt-Kontext. Wird der Text gelöscht, ist die Funktion für den Agenten unbrauchbar.
* **[VORSCHLAG]**:
  Niemals den Text komplett löschen. Wenn das Budget knapp ist, wird der Text **zeilenweise gekürzt** und mit einem klaren Fortsetzungshinweis versehen:
  `"Treffer 1-5 von 120 (Text gekürzt; nutze cursor=5 für die nächsten Treffer)"`.

#### 2. `DATA-ACCESS-LINQ-POLLUTION` (P1)
* **Beobachtung**:
  Bei der Suche nach Datenzugriffen in Assembly A tauchten Treffer wie `return new DataContainerSet(liste.Select((T k) => ...));` oder `select P;` auf.
* **Ursache**:
  [AssemblySearchTool.cs:37](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblySearchTool.cs#L37) enthält `SELECT`. Da Regex case-insensitive läuft, wird jedes LINQ-Statement gematcht.
* **[VORSCHLAG]**:
  In C#-Codebases sollte `SELECT` nur gematcht werden, wenn es von typischen SQL-Strukturen begleitet ist (z. B. `\bSELECT\s+.*?\s+FROM\b` oder innerhalb von String-Literalen), während LINQ-Keywords ignoriert werden.

#### 3. `EXTERNAL-SOURCE-HEALTH-MISLEADING` & `GIT-PROGRESS-ABORT` (P0 – Root-Cause-Analyse)
* **Beobachtung**:
  Die Konfigurationen in `c:\Daten\Tools\AiNetLinter-win-x64\appsettings.json` und `external-sources.json` sind syntaktisch und semantisch einwandfrei gepflegt:
  ```json
  // appsettings.json
  {
    "ExternalSources": {
      "MappingsPath": "external-sources.json",
      "CacheRoot": "cache",
      "RefreshIntervalMinutes": 60,
      "MaxDiskBytes": 536870912,
      "MaxMemoryBytes": 536870912,
      "MaxParallelOperations": 4,
      "MaxResidentResources": 128,
      "IdleTtlMinutes": 45
    }
  }
  ```
  ```json
  // external-sources.json
  {
    "repositories": [
      {
        "url": "http://git-server:3000/SAN/San.OfficeLine.Core",
        "solutionPath": "San.OfficeLine.Core.sln",
        "assemblies": [
          "San.OfficeLine.Core.dll",
          "San.OfficeLine.Core.Test.dll"
        ]
      }
    ]
  }
  ```
  Trotzdem meldet `get_server_health`:
  ```text
  Mapping-Status: not-configured
  Checkout-Status: not-applicable
  Next-Action: Source-Mapping/Provider prüfen.
  ```
* **Ursachen-Kette (3 interagierende Faktoren)**:
  1. **Falsche Health-Projektion ([AssemblyHealthProjection.cs:72](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/ServerMaintenance/Projection/AssemblyHealthProjection.cs#L72))**:
     `MappingStatus: source is null ? "not-configured" : "verified"`.
     `source` ist die `origin.SourceSnapshotIdentity`. Wenn die Source-Bereitstellung fehlschlägt, ist `source == null` und der Server fällt auf Decompilation zurück (`origin.IsDecompiled = true`). Die Health-Ausgabe gibt daraufhin fälschlich `"not-configured"` aus – selbst wenn das Mapping sauber existiert!
  2. **Echter Clone-Abbruch wegen Git-Progress ([ExternalSourceGitProcessOutputPolicy.cs:38-42](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/ExternalSource/ProcessExecution/ExternalSourceGitProcessOutputPolicy.cs#L38-L42))**:
     `GiteaGitRepositoryTransport.ExecuteCloneAsync` ruft `git clone` ohne `--quiet` / `--no-progress` auf. Git schreibt Fortschritt auf `stderr` (`remote: Enumerating objects...`, `Receiving objects...`). Die Policy verwirft jede Zeile außer der ersten (`Cloning into '.ainetlinter-git-clone'...`) und markiert den Clone trotz ExitCode 0 als fehlerhaft (`InvalidResponse`), wodurch der Provider als `provider-unavailable` deklariert wird.
  3. **Kein Config-Reload im Daemon ([AssemblyAnalysisHostComposition.cs:201](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisHostComposition.cs#L201))**:
     Der Daemon liest `appsettings.json` und `external-sources.json` nur einmalig beim Prozessstart. Nachträgliche Änderungen in den Dateien erfordern zwingend einen Neustart des Daemon-Prozesses; `reload_config` lädt nur `rules.json` neu. Zudem cacht der Orchestrator negative Ergebnisse (`cachedNegativeFallback`) für die Dauer der Session.
* **[VORSCHLAG]**:
  1. `ExternalSourceGitProcessOutputPolicy`: Git-Progress-Zeilen auf `stderr` tolerieren oder `git clone` mit `-q` / `--no-progress` ausführen.
  2. `AssemblyHealthProjection`: Saubere Unterscheidung zwischen `"not-configured"`, `"configured-checkout-failed"` und `"verified"`.
  3. Daemon-Lebenszyklus: CLI- oder Tool-Kommando bereitstellen, um externe Source-Konfigurationen und Caches ohne Daemon-Kill neu einzulesen.

---

## 4. Konzeptdiskussion: Paginierung, Filter & Agent-UX

### Deine Ausgangsthese:
> *„Wir haben an vielen (allen?) Stellen sinnvollerweise Truncatierung, damit wir das Kontextfenster von Agenten nicht sprengen. Einfach abschneiden, ohne dass der Agent eine Chance hat, auf die Informationen 'ganz hinten' zuzugreifen, ist Mist. [...] Ich stelle mir Pagination vor [...] deterministisch sortiert [...] Agent weiß wie viele Pages es gibt (1 von N) [...] jeder Aufruf mit Filter (Kategorie / Text / Regex).“*

### Beurteilung aus Sicht des Kritikers & Auditors

#### 1. Volle Zustimmung: „Abschneiden ohne Fortsetzung ist Mist“
Ein harter Cut (`isTruncated = true`) ohne Fortsetzungsmöglichkeit führt bei LLMs zu **Halluzinationen oder falschen Negativurteilen**:
- Der Agent sucht eine Speicherroutine.
- Der Server bricht bei 50 Methoden ab. Die gesuchte Routine lag an Position 52.
- Der Agent meldet dem Nutzer: *„Die Assembly enthält keine Speicherlogik.“*
- **Fazit**: Jedes begrenzte Ergebnis **MUSS** paginierbar oder per Cursor fortsetzbar sein.

---

#### 2. Offset/Page-Nummerierung (`page: 1 von N`) vs. Cursor (`continuationToken`)

| Aspekt | Klassische Paginierung (`page=1`, `totalPages=N`) | Opaque Cursor / Continuation-Token (`cursor="abc"`) |
|:---|:---|:---|
| **Verständlichkeit für LLM** | **Sehr hoch**: LLM versteht sofort `page: 2`. | **Hoch**: LLM übergibt einfach `cursor: token`. |
| **Gezieltes Vorblättern** | **Möglich**: LLM kann direkt auf Seite 5 springen. | **Nein**: Nur sequenzielles Vorwärtsblättern möglich. |
| **Performance bei großen Daten** | **Schlecht**: Um `totalPages` zu berechnen, muss das System *alle* 100.000 Dateien/Symbole vorab filtern und zählen, bevor Seite 1 geliefert wird. | **Sehr gut**: Das System scannt nur bis `pageSize + 1` und liefert sofort zurück (Streaming/Lazy). |
| **Gefahr für Agent-Loops** | **Hoch**: Sieht ein LLM `Seite 1 von 80`, neigt es dazu, in einer Schleife alle 80 Seiten abzurufen, und sprengt sein Kontextfenster. | **Geringer**: Token signalisiert gezieltes Fortsetzen bei Bedarf. |

* **[VORSCHLAG / SYNTHESE]**:
  Ein **hybrides Modell**:
  - Wo die Gesamtmenge schnell ermittelbar ist (InMemory Roslyn / Cache), liefern wir `totalItems` und `totalPages`.
  - Wo ein Vollscan teuer wäre, liefern wir `hasMore: true` und einen `nextCursor`.

---

#### 3. Deterministische Sortierung (Das Fundament)
Ohne deterministische Sortierung ist Paginierung wertlos:
1. **Dateien/Pfade**: Immer `OrderBy(p => p, StringComparer.OrdinalIgnoreCase)`.
2. **Symbole**: Zuerst nach kanonischem Symbol-Identifier bzw. vollqualifiziertem Typnamen `OrderBy(s => s.ToDisplayString(), StringComparer.Ordinal)`.
3. **Verstöße/Diagnostics**: Zuerst nach Dateipfad, dann Zeilennummer, dann Regel-ID.

---

#### 4. Filter-First-Strategie: Warum Filtern wichtiger ist als Blättern
Ein Agent sollte **fast nie** durch 10 Seiten blättern müssen. Jeder Tool-Call kostet 2–10 Sekunden Roundtrip-Zeit und Token-Budget.
Deine Idee mit **Kategorie-Filtern und Text-/Regex-Filtern** ist der entscheidende Hebel:

* **[VORSCHLAG: Universelle Filter-Matrix für Listen-Tools]**:
  1. **Struktur-/Kategorie-Filter (`kind` / `category`)**:
     - Symbole: `kind = "Class" | "Method" | "Property"`.
     - Dateien: `category = "source" | "test" | "config"` oder Dateiendung.
     - Violations: `severity = "Error" | "Warning"`, `ruleId = "NoAsyncVoid"`.
  2. **Namens- / Pfadfilter (`pattern` / `namePattern`)**:
     - Standard: Case-Insensitive Substring-Match.
  3. **Opt-in Regex (`isRegex: true`)**:
     - **Sicherheits-Pflicht**: Jeder Regex-Aufruf muss mit einem Timeout (z. B. 100 ms) versehen sein, um ReDoS-Hänger zu verhindern.

---

#### 5. Das Composite-Dilemma (`get_assembly_context`)
`get_assembly_context` bündelt `types`, `metrics`, `callers` und `body`. Hier kann ein einzelner Parameter `page` nicht greifen.
* **[VORSCHLAG: 2-Stufen-Prinzip]**:
  1. **Stufe 1 (Composite)** liefert eine **Kompakt-Vorschau (Top 5)** + Zähler + Verweis auf das Spezialtool.
  2. **Stufe 2 (Spezialtool)**: Erst in `get_call_tree` oder `find_references` greift das vollständige Paging (`page`, `pageSize`, `filter`).

---

## 5. Konkreter Umsetzungsvorschlag (Harmonisierter Standard)

### 1. Einheitliches Request-Record
```csharp
// [VORSCHLAG] In AiNetLinter.Mcp.Common
public sealed record PaginationArgs(
    int Page = 1,
    int PageSize = 50,
    string? Cursor = null,
    string? Filter = null,
    bool IsRegex = false,
    string? Category = null)
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 250;

    public int NormalizedPage => Math.Max(1, Page);
    public int NormalizedPageSize => Math.Clamp(PageSize <= 0 ? DefaultPageSize : PageSize, 1, MaxPageSize);
    public int Offset => (NormalizedPage - 1) * NormalizedPageSize;
}
```

### 2. Einheitliches Envelope-Response-Record
```csharp
// [VORSCHLAG] In AiNetLinter.Mcp.Common
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    bool HasMore,
    string? NextCursor = null,
    string? FilterApplied = null);
```

---

## 6. Zusammenfassende Handlungsempfehlungen

1. **Tech-Debt-Entrümpelung (Priorität 1 - Fundament)**:
   - Radikaler Rückbau der 10 Abstraktionsschichten rund um Git-Download und Caching.
   - Einführung einer einzigen, schlanken `GitEngine` (~150-200 Zeilen C#).
   - Echte Integrationstests mit `git.exe` gegen lokale Bare-Repositories (keine reinen Mocks mehr).
2. **Sofort-Fixes (P0 & P1)**:
   - **`MCP-TEXT-BLACK-HOLE`**: Textdarstellung niemals komplett durch den Einzeiler ersetzen, sondern Text gekürzt mit Paging-Hinweis ausgeben.
   - **`GIT-PROGRESS-ABORT`**: `--quiet` bei `git clone` übergeben und `LC_ALL=C` setzen.
   - **`EXTERNAL-SOURCE-HEALTH-MISLEADING`**: Saubere Status-Meldungen statt pauschalem `not-configured`.
   - **`DATA-ACCESS-LINQ`**: C#-LINQ-Keywords aus der SQL-Regex von `search_assembly` entfernen.
3. **Architektur-Umbau (P2)**:
   - Ersetzen der nächtlichen JSON-DOM-Trimm-Schleife durch echte Paginierung an den Datenquellen.
4. **Paging-Standard einführen**:
   - `PaginationArgs` und `PagedResult<T>` als verbindliches Muster für alle listenbasierten MCP-Tools ausrollen.

---

## 7. Architektur-Refactoring & Tech-Debt: Radikale Entrümpelung des Git-Subsystems

### 7.1 Das Kernproblem: 10 Schichten Enterprise-Overkill für `git clone`
Der nächtliche autonome Agent hat für den simplen Vorgang, ein Git-Repository bereitzustellen, eine monumentale 10-Stufen-Kaskade geschaffen:
```text
GiteaGitRepositoryTransport
  → ExternalSourceRepositoryAcquirer
    → ExternalSourceRepositoryCache
      → ExternalResourceRegistry
        → SourceSnapshotRegistry
          → AssemblySourceProviderCoordinator
            → AssemblySourceSelectionOrchestrator
              → AssemblyAnalysisHostComposition
                → AssemblyAnalysisSession
                  → AssemblyAnalysisHostSessionRegistry
```
**Die negativen Konsequenzen:**
- **Verwischte Fehlerursachen**: Schlägt `git clone` fehl, verliert sich der Fehler über 8 Schichten und kommt in `AssemblyHealthProjection` als `source == null` an, was als `not-configured` ausgegeben wird.
- **Wartungsalbtraum**: 1500 Zeilen Boilerplate, Leases, Overrides, Quarantänen und Fake-Memory-Counter.
- **Pseudo-Sicherheit**: Ein aufwändiges stderr-Zeilen-Whitelisting, das in der Realität sofort abbricht, sobald Git eine Standard-Progress-Zeile ausgibt.

### 7.2 Fundamentale Erkenntnis: „Das Dateisystem ist die Single Source of Truth“
Am Ende des Tages liegt irgendwo im Dateisystem schlicht ein Verzeichnis mit einem Git-Repository.
- Wir benötigen **keine** In-Memory-Lease-Tracker, Quarantäne-Zustandsmaschinen oder simulierte RAM-Byte-Counter für Dateien auf der Festplatte.
- **Zustandslogik**:
  1. Ist der Zielordner da und enthält ein valides `.git`? -> Fertig (optional `git pull` / `git fetch`).
  2. Fehlt der Ordner? -> `git clone --quiet <url> <zielordner>`.
  3. Ist die Festplatte voll? -> Ein simples 20-Zeilen-LRU-Cleanup (lösche Verzeichnisse mit ältestem `LastAccessTimeUtc`).

### 7.3 Lösungsoptionen: NuGet (LibGit2Sharp) vs. Schlanke `GitEngine` (CLI)

| Kriterium | Option A: `LibGit2Sharp` (NuGet) | Option B: Schlanke `GitEngine` (CLI-Wrapper via `ProcessStartInfo`) |
|:---|:---|:---|
| **Abhängigkeiten** | Schwergewichtig: Bringt native `libgit2.dll`/`.so`-Binaries mit; potenzielle Plattform-/Architektur-Probleme (x64, ARM, Linux). | **Leichtgewichtig**: 0 externe Dependencies; nutzt das vorhandene System-`git`. |
| **Authentifizierung** | Oft sperrig mit internen C-Callbacks / Credential-Helpers. | **Standardisiert**: Nutzt automatisch Git-Credential-Manager oder übergebene Tokens via `GIT_ASKPASS`/Env. |
| **Code-Umfang** | ~100 Zeilen C# (aber 50 MB Native-Binaries). | **~150–200 Zeilen C# in genau 1 Klasse** (keine weiteren Dateien nötig). |
| **Fehler-Transparenz** | Exzellent (native C#-Exceptions). | Sehr gut, wenn ExitCode != 0 ungefiltert die vollständige `stderr`-Ausgabe liefert. |

> **Empfehlung:** **Option B (`GitEngine`)**. Sie vermeidet native DLL-Abhängigkeiten in der Linter-Binary und lässt sich in einer einzigen, testbaren Klasse kapseln.

### 7.4 Vorgehensmodell: Schmale vertikale Slices statt „Mega-Nacht-Runs“
Statt ein riesiges 3-Epics-Dokument über Nacht laufen zu lassen, wird das Thema in strikt getrennte, testgetriebene Slices zerlegt:

```text
Slice 1: Die schlanke GitEngine (Isoliert & Real)
- Erstelle eine einzige Klasse `GitEngine.cs` (Clone, Fetch, HeadCommit, Clean).
- Implementiere echte Integrationstests in `AiNetLinter.IntegrationTests`, die mit `git init --bare`
  ein lokales Test-Repository aufsetzen und klonen (KEINE Mocks!).
- Tor-Kriterium: Echter Git-Clone funktioniert lokal deterministisch in < 500 ms.

Slice 2: Bereinigung & Austausch
- Ersetze die 10 alten Klassen durch Aufrufe der neuen `GitEngine`.
- Lösche den Alt-Code (`ExternalSourceRepositoryAcquirer`, `ExternalResourceRegistry`, etc.).
- Tor-Kriterium: Alle bestehenden Fast- & Integrationstests laufen fehlerfrei.

Slice 3: Roslyn-Projekt-Matcher & Live-Smoke-Test
- Direkte Zuordnung der Assembly zu Projekten im geklonten Verzeichnis.
- Tor-Kriterium: Live-Abfrage einer Test-DLL gegen das lokale Gitea-Repository liefert
  den echten Quellcode statt Dekompilierung.
```
