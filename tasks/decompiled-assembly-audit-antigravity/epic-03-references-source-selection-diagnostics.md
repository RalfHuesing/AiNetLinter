# Audit-Bericht: Epic 03 — Referenzen, Source Selection und Diagnosen

## Scope und Evidenz

### Untersuchte Komponenten und Verträge

- **Referenz-Auflösung:** `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyReferenceResolver.cs`.
- **External Source Provider & Mapping:** `src/AiNetLinter/Mcp/Assemblies/ExternalSource/`, `src/AiNetLinter/Configuration/ExternalSourceConfigurationLoader.cs`, `src/AiNetLinter/Mcp/Assemblies/Analysis/SourceSelection/`.
- **Diagnose-Projektion & Codes:** `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyDiagnosticCodes.cs`, `AssemblyAnalysisDiagnostics.cs`.
- **Prüffälle:**
  - `GIT-01`: Verifikation des Source-Mappings, Provider-Status und Decompilation-Fallback-Gründe.
  - `LOCAL-01`, `LOCAL-02`, `LOCAL-03`: Prüfung von `origin=decompiled`, `fallbackReason=mapping-not-found`, `completeness=partial` und Referenz-Diagnosen.

---

## Befunde

### 1. Bugs

#### FINDING-EPIC03-01: Exakter Versionsabgleich in `AssemblyReferenceResolver` verhindert Framework-Assembly-Unification

- **Kategorie:** Bug
- **Priorität:** P1
- **Größe:** M
- **Vertrauensgrad:** Hoch
- **Betroffene Komponenten:**
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyReferenceResolver.cs` (Zeilen 183–214, 348–351)
- **Soll-Ist-Abweichung:**
  In `IdentityMatches` wird die Version der referenzierten Assembly mit der Version des gefundenen Kandidaten auf exakte Zeichenketten-Gleichheit verglichen:
  ```csharp
  private static bool IdentityMatches(AssemblyReferenceDto expected, AssemblyIdentityDto actual) =>
      string.Equals(expected.Name, actual.Name, StringComparison.OrdinalIgnoreCase)
      && string.Equals(expected.Version, actual.Version, StringComparison.Ordinal)
      && string.Equals(NormalizeCulture(expected.Culture), NormalizeCulture(actual.Culture), StringComparison.OrdinalIgnoreCase);
  ```
  In realen .NET-Umgebungen referenzieren Assemblies häufig ältere Versionen von Standard-Bibliotheken (z. B. `mscorlib, Version 1.0.3300.0` oder `Version 2.0.0.0` oder `System.Runtime, Version 4.0.0.0`). Auf dem Host-System liegt jedoch die vereinheitlichte Runtime-Version (z. B. Version 4.0.0.0 oder .NET Core/10 System.Private.CoreLib) vor.
  Weil `IdentityMatches` keine Binding-Redirects, Framework-Unification oder Abwärtskompatibilitäts-Regeln implementiert, scheitert der Abgleich mit `version_mismatch`.
- **Evidenz:**
  - Live-Ergebnis bei `LOCAL-01`, `LOCAL-02` und `LOCAL-03` in `get_server_health`:
    `Kein identitätsgleicher Kandidat für 'mscorlib' gefunden. Erwartet: Version 1.0.3300.0, Kultur neutral; geprüft: C:\Daten\Tools\AiNetLinter-win-x64\mscorlib.dll (4.0.0.0, neutral).`
  - In der Folge wird die Referenz als ungelöst eingestuft, was zu synthetischen Typ-Auflösungsfehlern (z. B. `CS0246`, `CS0234`) in Roslyn führt und die Session auf `status=partial` herabstuft.
- **Auswirkung:**
   Nahezu jede untersuchte .NET-Framework- oder Vendor-Assembly mit älteren Abhängigkeiten wird als fehlerhaft/unvollständig eingestuft, obwohl die Runtime-Bibliotheken auf dem System vorhanden sind.
- **Empfehlung:**
  Implementierung von Standard-Binding-Redirect- und Unification-Regeln für Core- und Framework-Assemblies (z. B. `mscorlib`, `System.*`, `Microsoft.*`), sodass eine höhere vorhandene Version bei gleicher Major-Kompatibilität oder Standard-Unification akzeptiert wird.
- **Abgrenzung:** Semantischer Fehler in der Referenzauflösungs-Logik.

---

### 2. Optimierungen

#### FINDING-EPIC03-02: Transparenz von Source-Mapping-Pfade in Diagnostics und Health

- **Kategorie:** Optimierung
- **Priorität:** P2
- **Größe:** S
- **Vertrauensgrad:** Hoch
- **Betroffene Komponenten:**
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisDiagnostics.cs`
  - `src/AiNetLinter/Configuration/ExternalSourceConfigurationLoader.cs`
- **Soll-Ist-Abweichung:**
  Wenn für eine untersuchte Assembly kein Source-Mapping gefunden wird, liefert der Server `fallbackReason=mapping-not-found` und die Warnung `external-source-assembly-mapping-not-found: Für Assembly 'X' ist kein Source-Mapping konfiguriert. ($configuration)`.
  Die Diagnose nennt jedoch nicht den aktuell untersuchten `appsettings.json`- oder `mappingsPath`-Pfad, sodass der Anwender bei Fehlkonfigurationen nicht sieht, welche Konfigurationsdatei vom Daemon herangezogen wurde.
- **Evidenz:**
  - Diagnosetext bei `LOCAL-01` und `GIT-01`:
    `External-Source-Diagnose [warning] external-source-assembly-mapping-not-found: Für Assembly '...' ist kein Source-Mapping konfiguriert. ($configuration)`
- **Auswirkung:**
  Erhöhter Aufwand bei der Fehlersuche, wenn ein Source-Mapping zwar in einer Datei existiert, diese aber vom Daemon nicht am erwarteten Ort geladen wurde.
- **Empfehlung:**
  Ergänzung des aktiven Konfigurationsdateipfads in der Diagnose-Meldung.
- **Abgrenzung:** Diagnose- und UX-Optimierung.

---

### 3. Missing Features

#### FINDING-EPIC03-03: Fehlende GAC- und Reference-Assembly-Suchpfade

- **Kategorie:** Missing Feature
- **Priorität:** P2
- **Größe:** M
- **Vertrauensgrad:** Hoch
- **Betroffene Komponenten:**
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyReferenceResolver.cs` (Zeilen 240–269)
- **Soll-Ist-Abweichung:**
  `EnumerateCandidatePaths` durchsucht nur:
  1. Das direkte Verzeichnis der untersuchten Assembly (`directory`)
  2. Die `TRUSTED_PLATFORM_ASSEMBLIES` der aktuellen .NET 10 Host-Anwendung.
  
  Klassische .NET-Framework-Referenzen (GAC unter `C:\Windows\Microsoft.NET\assembly\GAC_MSIL\...` oder Reference Assemblies unter `C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\...`) werden nicht durchsucht.
- **Evidenz:**
  - Bei `LOCAL-01` und `LOCAL-02` verbleiben Referenzen wie `Microsoft.VisualBasic` oder spezielle Framework-Komponenten ungelöst oder scheitern, wenn sie nicht im gleichen Ordner liegen.
- **Auswirkung:**
  Assemblies mit geteilten GAC- oder SDK-Referenzen können nicht vollständig aufgelöst werden.
- **Empfehlung:**
  Optionale konfigurierbare Suchpfade (`ReferenceSearchPaths`) in `appsettings.json` und standardmäßige Einbindung bekannter Framework-Pfade unter Windows.
- **Abgrenzung:** Fehlender Funktionsumfang für Legacy- und Multi-Framework-Szenarien.

---

## Origin-Nachweis der externen Prüffälle

| Prüffall | Erwarteter Ursprung | Tatsächlicher MCP-Status | Nachweis-Signale | Bewertung |
|---|---|---|---|---|
| `GIT-01` | Konfiguriertes Git/Source | `origin=decompiled` | `sourcePath=none`, `snapshot=none`, `fallbackReason=mapping-not-found`, `confidence=medium`, `trust=untrusted` | Fallback griff korrekt, da im aktuellen Daemon kein aktives Mapping für die konkrete Ziel-Assembly registriert war. |
| `LOCAL-01` | Lokale Decompilation | `origin=decompiled` | `sourcePath=none`, `snapshot=none`, `fallbackReason=mapping-not-found`, `generation=1`, `status=partial` | Bestätigt dekompiliert; synthetische Diagnosen vorhanden. |
| `LOCAL-02` | Lokale Decompilation | `origin=decompiled` | `sourcePath=none`, `snapshot=none`, `fallbackReason=mapping-not-found`, `generation=1`, `status=partial` | Bestätigt dekompiliert; dekompilierte Signaturen verifiziert. |
| `LOCAL-03` | Lokale verwaltete EXE | `origin=decompiled` | `sourcePath=none`, `snapshot=none`, `fallbackReason=mapping-not-found`, 64 Namespaces | Bestätigt dekompiliert; Managed EXE sauber metadata-only verarbeitet. |
| `FALSE-01` | Nicht-.NET EXE (Negativfall) | `isError=false`, `recoverable=true` | `WORKSPACE_DIAGNOSTIC: Die Datei enthält keine .NET-Metadaten.` | Sicher abgewiesen; keine Ausführung, kein Absturz. |

---

## Offene Unsicherheiten

1. **GAC-Sicherheit:** Das Durchsuchen des GAC erfordert strikte Pfad-Validierung, um Performance-Einbußen durch tief verschachtelte Verzeichnisse zu vermeiden.
