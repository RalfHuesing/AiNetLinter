# Audit-Bericht: Epic 04 — Session-, Cache- und Lebenszeitsemantik

## Scope und Evidenz

### Untersuchte Komponenten und Verträge

- **Assembly-Registry & Eviction:** `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisRegistry.cs`, `AssemblyAnalysisRegistryDisposal.cs`, `ExternalResourceRegistry.cs`.
- **Session-Lebenszyklus:** `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisSession.cs`.
- **Decompilation-Cache:** `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyDecompilationCache.cs`, `AssemblyCacheCleanup.cs`.
- **Fingerprinting & Freshness:** `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyFingerprint.cs`.
- **Live-MCP-Abfragen:**
  - `get_server_health` mit `includeSessions=true` zur Verifikation der residenten Sessions (90 Sessions nach Referenz-Expansion beobachtet).

---

## Befunde

### 1. Bugs

#### FINDING-EPIC04-01: `AssemblyDecompilationCache.Publish` löscht Generation-Verzeichnis im `finally`-Block bei bestehendem Cache-Eintrag

- **Kategorie:** Bug
- **Priorität:** P1
- **Größe:** M
- **Vertrauensgrad:** Hoch
- **Betroffene Komponenten:**
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyDecompilationCache.cs` (Zeilen 66–104)
- **Soll-Ist-Abweichung:**
  In `AssemblyDecompilationCache.Publish` wird vor dem Ersetzen des Current-Pointers geprüft, ob ein Cache-Eintrag bereits lesbar ist:
  ```csharp
  var readRequest = new AssemblyCacheReadRequest(request.CacheKey, request.Fingerprint, request.References);
  if (TryRead(readRequest, out _, out _))
  {
      return new AssemblyCachePublishResult(true, generationDirectory, null);
  }

  if (!TryPublishPointer(entryDirectory, generationDirectory, request, out var diagnostic))
  {
      return new AssemblyCachePublishResult(false, null, diagnostic);
  }

  isPublished = true;
  ...
  finally
  {
      if (!isPublished) AssemblyCacheCleanup.DeleteDirectory(generationDirectory);
  }
  ```
  Wenn `TryRead` erfolgreich ist (z. B. weil eine andere Session oder ein paralleler Thread die Generation bereits publiziert hat), springt die Methode mit `return new AssemblyCachePublishResult(true, generationDirectory, null)` heraus.
  Weil `isPublished` zu diesem Zeitpunkt jedoch `false` ist, wird im `finally`-Block `AssemblyCacheCleanup.DeleteDirectory(generationDirectory)` ausgeführt.
  Der Aufrufer erhält ein Ergebnis mit `Succeeded = true` und dem Pfad `generationDirectory`, obwohl dieses Verzeichnis auf der Festplatte soeben gelöscht wurde.
- **Evidenz:**
  - Code in `AssemblyDecompilationCache.cs` Zeilen 78–82 und 101–103.
  - Wenn der Caller anschließend versucht, Dokumente aus dem im Resultat genannten `generationDirectory` zu lesen, scheitert der Zugriff mit `DirectoryNotFoundException` / `FileNotFoundException`.
- **Auswirkung:**
  Sporadische `FileNotFoundException` oder fehlschlagende Snapshot-Erstellungen bei parallelen Cache-Zugriffen auf denselben Assembly-Key.
- **Empfehlung:**
  Bei vorzeitigem Return nach erfolgreichem `TryRead` entweder `isPublished = true` setzen (falls die geschriebene Generation verwendet werden soll) oder den Pfad der bereits existierenden Generation aus `TryRead` zurückgeben und die redundante neue Generation sauber verwerfen.
- **Abgrenzung:** Klarer Concurrency- und Lifecycle-Bug im Cache-Publishing.

---

### 2. Optimierungen

#### FINDING-EPIC04-02: Referenz-Sessions erzeugen hohe Session-Last ohne automatischen Cleanup

- **Kategorie:** Optimierung
- **Priorität:** P2
- **Größe:** S
- **Vertrauensgrad:** Hoch
- **Betroffene Komponenten:**
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisRegistry.cs`
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/ExternalResourceRegistry.cs`
- **Soll-Ist-Abweichung:**
  Ein einzelner Aufruf von `inspect_assembly` oder `find_assembly_extensions` mit Referenzauflösung öffnet für jede referenzierte DLL eine eigene bounded `AssemblyAnalysisSession`. Bei `LOCAL-01` führte dies zu 90 residenten Sessions im Daemon.
  Diese Sessions verbleiben bis zum Ablauf der 45-Minuten-Idle-TTL im Speicher (`ExternalResourceRegistryDefaults.IdleTtl`), wodurch AdhocWorkspaces und Roslyn-Compilations für Dutzende Assemblies resident gehalten werden.
- **Evidenz:**
  - `get_server_health` wies nach dem Lauf von `LOCAL-01` 90 aktive Assembly-Sessions aus.
- **Auswirkung:**
  Erhöhter Arbeitsspeicherverbrauch des MCP-Daemons nach Abfragen stark vernetzter Assemblies.
- **Empfehlung:**
  Transitive Referenz-Sessions mit einer kürzeren TTL versehen (z. B. 5 Minuten) oder nach Abschluss des übergeordneten Tool-Calls aggressiver freigeben, wenn keine aktiven Leases mehr vorliegen.
- **Abgrenzung:** Speicher- und Ressourcen-Optimierung.

---

### 3. Missing Features

#### FINDING-EPIC04-03: Fehlende Möglichkeit zum manuellen Invalidieren von Assembly-Sessions

- **Kategorie:** Missing Feature
- **Priorität:** P3
- **Größe:** S
- **Vertrauensgrad:** Hoch
- **Betroffene Komponenten:**
  - `src/AiNetLinter/Mcp/Registration/ServerMaintenanceToolRegistrations.cs`
- **Soll-Ist-Abweichung:**
  Für Solution-Projekte existiert `reload_config`, um geänderte Regeln neu zu laden. Für Assembly-Targets existiert kein MCP-Befehl (z. B. `purge_assembly_cache` oder `reload_assembly`), um eine Assembly-Session im Daemon manuell zu verwerfen, falls die DLL extern neu gebaut wurde und der Fingerprint-Check nicht sofort triggert.
- **Evidenz:**
  - Analyse von `ServerMaintenanceToolRegistrations.cs`: Nur `get_server_health`, `reload_config` und `report_observability_feedback` sind registriert.
- **Auswirkung:**
  Entwickler/Agenten müssen den gesamten Daemon neu starten, wenn sie einen Assembly-Cache manuell leeren wollen.
- **Empfehlung:**
  Erweiterung von `reload_config` oder Bereitstellung eines optionalen Cache-Befehls für Assembly-Pfade.
- **Abgrenzung:** Wartbarkeits-Feature.

---

## Offene Unsicherheiten

1. **Fingerprint-Performance:** Bei sehr großen DLLs (>50 MB) benötigt die SHA256-Berechnung bei jedem Leasing-Check messbare CPU-Zeit; `LastWriteTimeUtc` und Dateigröße bieten bereits einen schnellen Vorab-Check.
