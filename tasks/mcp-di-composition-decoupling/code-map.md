# Code Map: MCP-Komposition entkoppeln und Qualitätsgrenzen wiederherstellen

## Primäre Einstiegspunkte

- `Konzept.md`: verbindlicher fachlicher Vertrag.
- Epic 1 startet an `AssemblyAnalysisLease` und der Zustandsübergabe aus dem
  MCP-Host in die Assembly-Analyse.

## Betroffene Dateien und Symbole

- `src/AiNetLinter/Mcp/McpCodeGraphServer.cs` — vorgesehene Implementierung
  der schlanken Zustandsgrenze.
- `src/AiNetLinter/Mcp/Assemblies/Analysis/References/AssemblyAnalysisLease.cs`
  — konkrete Server-Referenz in der Lease; zu verifizieren.
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblySymbolResolver.cs` —
  vorgesehene Methodengrößen-Extraktion; zu verifizieren.
- `AssemblyAnalysisToolSupport`,
  `AssemblyAnalysisRegistryEntryCreation` und ihre exakten Pfade — durch den
  Implementierer per MCP zu verifizieren.

## Aufrufer und Abhängigkeiten

- Die Lease-Aufrufer und ihre verwendeten Server-Member sind vor einer
  Typänderung semantisch zu erfassen.
- `ProjectLease` und `ProjectRegistry` gehören ausdrücklich nicht zur
  Typumstellung.

## Relevante Tests, Konfiguration und Dokumentation

- `AssemblyAnalysisSessionTests.cs` ist laut Konzept über dem Dateilimit;
  exakter Pfad und Testverantwortung sind zu verifizieren.
- Relevante Assembly-Analyse- und Concurrency-Tests, einschließlich
  `AssemblyAnalysisRegistryRetirementRaceTests`, sind per MCP zu ermitteln.
- `rules.json` ist eine explizite Non-Goal-Grenze; keine Änderung vorgesehen.

## Invarianten, Risiken und Unsicherheiten

- Kein DI-Container oder Service Locator; Constructor-/Factory-Injection
  bleibt erhalten.
- Die Lease behält ihre Locking-, Cancellation- und Body-Resolution-Semantik.
- Das neue Interface enthält nur tatsächlich benötigte Capabilities und kappt
  den transitiven Footprint zum konkreten Server.

## Verifikation

- Je Implementierungs-Epic: passende FastTests sowie ein frischer gezielter
  MCP-`get_violations`-Nachweis.
- Abschluss: Build, beide Nicht-Stress-Testsuiten, die drei Scope-Audits,
  projektweite Violations und Safeguard gemäß `roadmap.md`.
