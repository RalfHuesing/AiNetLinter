# Audit-Bericht: Epic 07 — Betrieb, Sicherheit und Fehlerverhalten

## Scope und Evidenz

### Untersuchte Komponenten und Verträge

- **Sicherheits- und Pfadgrenzen:** `src/AiNetLinter/Mcp/Assemblies/ExternalSource/Repository/ExternalSourceRepositoryPathGuard.cs`.
- **PE-Validierung & Metadata-only:** `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyReferenceResolver.cs`, `AssemblyFingerprint.cs`.
- **Fehlerbehandlung & IsError-Policy:** `src/AiNetLinter/Mcp/IsErrorPolicy.md`, `src/AiNetLinter/Mcp/McpToolResults.cs`.
- **Live-MCP-Abfragen:**
  - Negativfall `FALSE-01` (native unmanaged EXE) zur Prüfung der Fail-Closed-Garantie, Nichtausführung und Fehlerstruktur.

---

## Befunde

### 1. Bugs

In dieser Kategorie wurde kein Sicherheitsleck, kein Ausbruch aus Pfadgrenzen und keine unkontrollierte Code-Ausführung festgestellt. Das System hält die Kerninvariante strikt ein: Zielassemblies werden metadata-only über Roslyn und ICSharpCode analysiert und weder via `AssemblyLoadContext` noch als eigenständiger Prozess gestartet.

---

### 2. Optimierungen

#### FINDING-EPIC07-01: Irreführender Retry-Hint bei deterministisch nicht-.NET-Dateien (`FALSE-01`)

- **Kategorie:** Optimierung
- **Priorität:** P2
- **Größe:** S
- **Vertrauensgrad:** Hoch
- **Betroffene Komponenten:**
  - `src/AiNetLinter/Mcp/McpToolResults.cs`
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyReferenceResolver.cs` (Zeilen 30–32, 358–369)
- **Soll-Ist-Abweichung:**
  Wird eine native PE-Datei (wie `FALSE-01`) übergeben, erkennt `peReader.HasMetadata == false` den Zustand korrekt und liefert einen strukturierten Fehler. Der angehängte `hint` empfiehlt dem Agenten jedoch:
  `hint: Einmal erneut versuchen; bleibt der Fehler bestehen, Datei pruefen — Compile-Fehler blockieren Symbolaufloesung.`
  Da es sich um eine native Binärdatei handelt, ist ein Retry sinnlos und verschwendet Agenten-Turns.
- **Evidenz:**
  - Live-Ausgabe bei `FALSE-01`:
    ```
    [ERROR]: WORKSPACE_DIAGNOSTIC: Die Datei enthält keine .NET-Metadaten. Hinweis: verwaltete .NET-.dll oder .exe mit IL erforderlich.
      context: ...\OLAdmin.exe
      hint:    Einmal erneut versuchen; bleibt der Fehler bestehen, Datei pruefen — Compile-Fehler blockieren Symbolaufloesung.
    ```
- **Auswirkung:**
  KI-Agenten versuchen aufgrund des Hinweises den identischen Aufruf erneut, bevor sie aufgeben.
- **Empfehlung:**
  Für `MetadataMissing` bzw. fehlende IL-Metadaten einen deterministischen Hint setzen:
  `hint: Verwaltete .NET-Assembly (.dll oder .exe) angeben; native PE-Dateien oder C++-Binaries können nicht als .NET-Assembly analysiert werden.`
- **Abgrenzung:** Fehlerhinweis- und Prompt-Optimierung.

---

### 3. Missing Features

#### FINDING-EPIC07-02: Dedizierte Diagnose für C++/CLI (Mixed-Mode) Assemblies

- **Kategorie:** Missing Feature
- **Priorität:** P3
- **Größe:** S
- **Vertrauensgrad:** Hoch
- **Betroffene Komponenten:**
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyReferenceResolver.cs`
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyDiagnosticCodes.cs`
- **Soll-Ist-Abweichung:**
  Mixed-Mode-Assemblies (C++/CLI mit nativen und verwalteten Headern) können je nach Bitbreite (x86 vs x64) und nativen Entrypoints zu `BadImageFormatException` führen. Aktuell werden diese als allgemeine `assembly-metadata-read-failed`-Fehler gemeldet, ohne auf den Mixed-Mode-Charakter hinzuweisen.
- **Evidenz:**
  - Analyse der Exception-Filter in `AssemblyReferenceResolver.cs` Zeile 47.
- **Auswirkung:**
  Schwer verständliche Fehlermeldungen bei Vendor-Bibliotheken mit C++/CLI-Anteilen.
- **Empfehlung:**
  Prüfung des PE-Headers auf Mixed-Mode/IStream-Flags und Ausgabe einer gezielten Diagnose `assembly-mixed-mode-unsupported`.
- **Abgrenzung:** Diagnose-Erweiterung.

---

## Offene Unsicherheiten

1. **Pfadlängen unter Windows:** Sehr lange Pfade (>260 Zeichen, MAX_PATH) werden durch `PathNormalizer` und `Path.GetFullPath` weitgehend gehandhabt; bei extrem tiefen Cache-Strukturen sollte die Gesamtlänge überwacht werden.
