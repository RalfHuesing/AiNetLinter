## Primäre Einstiegspunkte

- Assembly-Analyse-Einstieg und Session-Komposition unter `src/AiNetLinter/Mcp/Assemblies/`.
- Assembly-MCP-Verträge für Inspection, Symbolsuche, Bodies und Navigation unter `src/AiNetLinter/Mcp/Tools/` sowie den Assembly-spezifischen Tooldateien.

## Betroffene Dateien und Symbole

- Zu verifizieren: `AssemblyAnalysisRegistry`, `AssemblyDecompilationCache`, Assembly-Snapshots/Generationen und External-Source-/Repository-Akquisition.
- Zu verifizieren: Response-Modelle und Formatter für Provenienz, `bodyAvailability`, `contentMode`, Root-Scope, Referenznavigation und Symbol-Handles.

## Aufrufer und Abhängigkeiten

- MCP-Server-Komposition und Assembly-Tool-Registrierung.
- External-Source-Mapping, Git-Checkout-/Snapshot-Leases und Roslyn-Workspace-Erzeugung.
- Bestehende Daemon-/Thin-Client-Aufrufpfade und gemeinsame Cache-Wurzeln.

## Relevante Tests, Konfiguration und Dokumentation

- Fast-Tests unter `src/AiNetLinter.FastTests/Mcp/Assemblies/`.
- Integrationstests unter `src/AiNetLinter.IntegrationTests/Mcp/Assemblies/` und Assembly-Navigation.
- `TestTempDirectory` aus `src/AiNetLinter.TestKit/`.
- `Docs/agent-api.md`, `Docs/integration.md`, `Docs/configuration.md`, optional `Docs/ROADMAP.md`.

## Invarianten, Risiken und Unsicherheiten

- Nur vollständig manifestierte und atomar veröffentlichte Artefakte dürfen gelesen werden.
- Ein noch gehaltener Betriebssystem-Lock darf nicht automatisch übernommen werden.
- Source-backed ist nur bei validem Mapping, Attestierung, Assembly-Identität und lesbarer Roslyn-Quelle zulässig; jeder Fallback braucht einen konkreten strukturierten Grund.
- Standardantworten bleiben im Root-Scope; Referenzen werden nur gezielt und begrenzt geöffnet.
- Die konkreten Symbole, Aufrufer und Testlücken werden durch den Implementierer MCP-first gegen den aktuellen Working Tree verifiziert.

## Verifikation

- Noch nicht ausgeführt; Implementierer und Reviewer ergänzen konkrete MCP-/Testnachweise.
