# Analyse-Findings & Architektur-Überlegungen: Robuste Assembly-Analyse (Paket 1)

**Erstellt am:** 2026-09-03  
**Status:** Hauptursachen identifiziert; 4 Detailfragen (siehe Abschnitt 6) vor Codeänderungen noch offen.  
**Zweck dieses Dokuments:** Vollständiges Festhalten aller ermittelten Fakten, Code-Stellen, Root Causes und Architekturentscheidungen dieses Chats. Verhindert erneutes Einlesen und Token-Verbrauch in nachfolgenden Chats.

---

## 1. Ursache der vorigen Blockade & Agenten-Umgebung

- **Was geschah vorher:** Drei Versuche eines Implementierer-Subagents scheiterten mit `404 Not Found` an `https://chatgpt.com/backend-api/codex/responses`. Dies war ein externer Infrastrukturfehler des vorherigen KI-Clients/Harnesses.
- **Aktuelle Umgebung (Google Antigravity):** In dieser IDE/Umgebung existiert keine externe Coding-Subagent-Schnittstelle (nur `browser_subagent`).
- **Konsequenz für Orchestrator & Workflows:**
  - Gemäß `orchestrator.md` (Zeilen 109–111): *„Wenn keine unabhängige Delegation möglich ist, behaupte keinen unabhängigen Review, sondern melde diese Einschränkung.“*
  - Ausführung von Paket 1 und Paket 2 erfolgt im direkten Single-Agent-Modus (Inline-Implementierung nach `implement/SKILL.md`, Selbstprüfung nach `review/SKILL.md` mit deklarierter Einschränkung, `dotnet test`, MCP-Violations, Audit).

---

## 2. Root Cause 1: Git-Checkout vorhanden, aber Decompilation statt Source gewählt

### Symptom
Ein externer Quellstand wird erfolgreich per Git geclont und als Snapshot materialisiert, die Assembly-Analyse liefert am Ende jedoch ein `decompiled`-Ergebnis statt `source-backed`.

### Ermittelte Ursachen im Code

1. **`AssemblyAnalysisContextFactory.cs` (Zeilen 130–183 & 184–198):**
   - `TryCreateSourceBackedContextAsync`:
     - Ruft `PrepareSourceContext(request)` auf.
     - `IsSourceSelectionUsable(selection)` stellt extrem strenge Vorbedingungen:
       - `selection.IsAttested == true`
       - `selection.ProviderHealth == ExternalSourceRepositoryHealth.Verified`
       - `selection.CheckoutTrust == ExternalSourceCheckoutTrust.Clean`
       - `match.State == ExternalSourceMatchState.Matched`
       - `match.MatchedCandidate != null`
       - Snapshot-Identity-Match.
     - Wenn `project.GetCompilationAsync()` Compile-Warnungen oder unkritische Roslyn-Workspace-Diagnosen meldet, liefert Roslyn `compilation.Assembly == null` oder bricht ab.
     - Bei jedem Fehler fällt `TryCreateSourceBackedContextAsync` auf `null` zurück.
   - Wenn `sourceAttempt.Context == null`:
     - Zeilen 37–47: Es wird **stillschweigend eine neue `AssemblyAnalysisSession` mit Decompilation erzeugt!**
     - Die Decompilation überschreibt den Status mit `ApplyFallback(FromGeneration(...), fallback)`.

2. **`AssemblyAnalysisRegistryEntryFactory.cs` (Zeilen 164–183):**
   - `TryCreateSourceEntryAsync`:
     - Wenn `sourceResult.Context.Origin.IsDecompiled` wahr ist, liefert `TryCreateSourceEntryAsync` `Entry = null`.
     - In Zeile 73 wird dann `CreateFallbackEntryAsync` aufgerufen, was wiederum eine vollständige Dekompilierung in den Cache schreibt und publiziert!

### Lösung / To-Do für Paket 1:
- Source-Projekt-Compilation muss auch bei Roslyn-Diagnosen/Warnungen als `source-backed` mit angehängten Diagnosen erhalten bleiben, solange der Syntaxbaum/Symbole lesbar sind.
- Kein unberechtigter Rückfall auf Decompilation, wenn ein gültiger attestierter Source-Snapshot existiert.
- Strukturierte Fallback-Gründe (`AssemblySourceFallbackReasons`) nur dann vergeben, wenn der Quellstand wirklich unlesbar oder ungültig ist.

---

## 3. Root Cause 2: Artefakt- und Checkout-Explosion (136 DLL-Artefakte, 17 Checkouts)

### Symptom
In einer Session entstanden 136 redundante Generation-Verzeichnisse unter `cache/asm` und 17 identische Checkouts unter `cache/checkouts`.

### Ermittelte Ursachen im Code

1. **Checkout-Ebene (`ExternalSourceRepositoryAcquirer.cs`, Zeilen 70–91):**
   - Bei Cache-Miss rufen parallele/mehrfache Anfragen für **denselben** Repositorieschlüssel (URL + Revision) sofort `ExternalSourceRepositoryCheckoutReservation.TryCreate(stagingRoot, out var ownership)` auf.
   - `TryCreate` generiert immer einen neuen Pfad `checkout-<Guid>` und startet den Git-Klon/Transport parallel.
   - Es gibt **keine pro Schlüssel exklusive Lock-Datei** während der Beschaffung/Materialisierung.
   - Jeder gleichzeitige Aufruf zieht ein eigenes Repo-Verzeichnis hoch!

2. **Decompilation-Cache-Ebene (`AssemblyDecompilationCache.cs` & `.Locking.cs`):**
   - `PublishLocks` ist eine rein prozessinterne `AssemblyCacheKeyLockRegistry` mit `System.Threading.Monitor.Enter`.
   - Bei mehreren Prozessen (z.B. MCP-Server-Prozess + Runner/CLI) oder Session-Überlappungen schützt dieser Monitor überhaupt nicht prozessübergreifend.
   - Es fehlt ein OS-Level FileLock (`FileStream` mit `FileShare.None`) auf den Artefaktschlüssel während der gesamten Dekompilierung/Staging-Phase.
   - Daher erzeugen konkurrierende Aufrufe jeweils eigene Staging-Ordner `gen-<Guid>.staging` und schreiben wiederholt neue Generationen `gen-<N>` ins Dateisystem.

### Lösung / To-Do für Paket 1 (gemäß Konzept.md):
- **OS-Level FileLock pro Artefaktschlüssel:**
  - Aktiver Erzeuger hält eine Lock-Datei (z. B. `build.lock` bzw. Schlüssel-gebunden) mit `FileShare.None` geöffnet, bis die Generierung/Veröffentlichung abgeschlossen ist.
  - Bei Prozessabsturz gibt das OS den Lock automatisch frei.
  - Zweiter Erzeuger versucht nicht parallel zu bauen, sondern wartet cancellierbar mit konfigurierbarem Timeout (Default 10 min) auf die Freigabe bzw. das fertige Manifest.
  - Stall-Erkennung nach 10 min (Diagnose melden, kein automatisches Lock-Stehlen).
- **Checkout-Wiederverwendung & Exklusivität:**
  - Vor dem Reservieren eines neuen Checkouts wird per FileLock pro (RepoUrl + Commit/Branch) sichergestellt, dass maximal ein Klonvorgang läuft.
  - Alle weiteren Aufrufer warten auf das Completion-Manifest des ersten Aufrufers und nutzen dieses.

---

## 4. Relevante Dateien & Einstiegspunkte

| Datei | Relevanz |
|---|---|
| `src/AiNetLinter/Mcp/Assemblies/ExternalSource/Repository/ExternalSourceRepositoryAcquirer.cs` | Checkout-Akquise; Lock vor `CheckoutReservation.TryCreate` einfügen. |
| `src/AiNetLinter/Mcp/Assemblies/ExternalSource/Repository/ExternalSourceRepositoryCheckoutReservation.cs` | Reservierung von Staging-Ordnern. |
| `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyDecompilationCache.cs` | Cache-Erzeugung, Veröffentlichung und Staging. |
| `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyDecompilationCache.Locking.cs` | Ersetzen/Erweitern des reinen Monitor-Locks durch OS-FileStream-Lock. |
| `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextFactory.cs` | `TryCreateSourceBackedContextAsync` & Fallback-Prüfungen (Source-first statt vorschneller Decompilation). |
| `src/AiNetLinter/Mcp/Assemblies/Analysis/Factories/AssemblyAnalysisRegistryEntryFactory.cs` | Registry-Eintragserzeugung, Fallback-Entscheidung bei Decompilation. |
| `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextFactoryTests.cs` | Unit-Tests für Source-first und Fallback-Gründe. |
| `src/AiNetLinter.IntegrationTests/Mcp/Assemblies/ExternalSourceSnapshotMaterializerTests.cs` | Integrationstests für Checkouts und Snapshots. |

---

## 5. Konkrete nächste Umsetzungsschritte für Paket 1

1. **Locking-Infrastruktur (OS-File-Lock):**
   - Einen echten Datei-basierten Lock-Mechanismus (`KeyedFileLock` / `FileStream` mit `FileShare.None`) implementieren, der pro Artefaktschlüssel (Assembly-Fingerprint bzw. Repo+Revision) arbeitet.
   - Sicherstellen: Warten mit `CancellationToken`, Stall-Timeout-Erkennung, autom. Release bei Dispose/Crash.
2. **Checkout-Exklusivität einbinden:**
   - In `ExternalSourceRepositoryAcquirer.cs` vor dem Anlegen eines neuen Checkouts den Lock auf den Schlüssel prüfen/halten.
   - Bereits vorhandenen Cache oder laufenden Klon wiederverwenden.
3. **Source-First in ContextFactory absichern:**
   - In `AssemblyAnalysisContextFactory.cs` sicherstellen, dass bei vorhandenem Source-Snapshot nicht fälschlicherweise auf Dekompilierung zurückgefallen wird, solange das Projekt im Roslyn-Workspace vorhanden ist.
4. **Verifikation:**
   - `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`
   - Parallele Tests (keine Duplikat-Ordner mehr)
   - `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`

---

## 6. Status der Analyse: Noch offene Detailanalysen vor Codeänderungen

Die funktionale Ursachenanalyse (warum dekompiliert wird und wo Locks fehlen) ist abgeschlossen. Folgende Detailfragen müssen vor der eigentlichen Implementierung vom nächsten Modell gezielt (per MCP!) geprüft werden:

1. **Bestehende File-Lock-Primitive im Codebase:**
   - Prüfen via MCP (`find_symbol` / `get_symbol_body`), wie `ExternalSourceRepositoryCheckoutReservation.TryCreateOwnership` oder `AssemblyCacheKeyLockRegistry` aufgebaut sind: Existiert bereits ein unvollständiger oder wiederverwendbarer OS-Lock, oder muss ein neuer `KeyedFileLock` (auf `FileStream`-Basis mit `FileShare.None`) unter `src/AiNetLinter/Mcp/Assemblies/Infrastructure/` angelegt werden?
2. **Stall-Erkennung & Timeout-Verhalten (10 Minuten):**
   - Wie wird das 10-Minuten-Stall-Kriterium an wartende Aufrufer signalisiert (welche Diagnose-Codes werden dafür vergeben, z. B. unter `ExternalSourceConfigurationDiagnosticCodes` / `AssemblyDiagnosticCodes`)?
3. **Negative Source-Ergebnisse (TTL):**
   - Wo wird das negative Ergebnis gecacht, wenn ein Source-Projekt nicht matcht (z. B. `AssemblySourceFallbackReasons.SourceProjectNotFound`), damit nicht bei jedem Aufruf ein erneuter Checkout-Versuch gestartet wird?
4. **Test-Hooks:**
   - Prüfen, wo die bestehenden Tests für parallele Aufrufe liegen (`AssemblyAnalysisRegistryTests`, `ExternalSourceSnapshotMaterializerTests`), um den neuen Lock- und Wait-Mechanismus ohne künstliche Testkopplung abzusichern.
