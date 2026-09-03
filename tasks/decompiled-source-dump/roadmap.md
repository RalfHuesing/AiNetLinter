---
status: executing
current_epic: epic-2
last_commit: 418557f3
current_debt_item: null
debt_attempts: 0
---

# Roadmap: Vollständige sofortige Assembly-Projekt-Materialisierung

## Epic 1: Eager-Volldekompilierung und persistenter Cache

- Ziel: Fremd-Assemblies beim ersten Laden vollständig als reale C#-Projekte in einem konfigurierbaren Cache materialisieren.
- Abhängigkeiten: bestehende Assembly-Analyse-, Cache- und Resolver-Infrastruktur; SourceToAI als Referenz.
- Betroffene Bereiche: `Mcp/Assemblies/Analysis/AssemblyDecompilationAdapter`, `AssemblyDecompilationCache`, `AssemblyCacheCleanup`, Konfiguration und Projektpakete.
- Muss-/Akzeptanzkriterien: M1, M2, M8, M9 und M10; Nachbar-DLLs bleiben Metadaten-Referenzen ohne rekursive Volldekompilierung; Cancellation räumt Staging auf.
- Verifikation: gezielte FastTests für Projektmaterialisierung, Cache-Konfiguration, atomare Veröffentlichung, Wiederverwendung und Lock-Toleranz; gezielter MCP-Violationscheck.
- Status: done

## Epic 2: Roslyn-Snapshot und direkte Body-Auflösung

- Ziel: Das dekompilierte Projekt wird mit realen Dateipfaden in einem AdhocWorkspace analysiert und `get_symbol_body` nutzt ausschließlich die bereits geladenen Syntaxbäume.
- Abhängigkeiten: Epic 1.
- Betroffene Bereiche: `AssemblyRoslynWorkspaceFactory`, `IAssemblyBodyContext`, `SourceSymbolBodyResolver`, Symbolgraph-/Call-Tree-Navigation und obsolete Body-/Stub-Komponenten.
- Muss-/Akzeptanzkriterien: M6 und M11; echte Methodenkörper sind ohne On-Demand-Dekompilierung verfügbar; alte Stub- und Body-Auflösungspfade sind restlos entfernt.
- Verifikation: FastTests für echte Bodies, Navigation und Regressionen; semantische MCP-Prüfungen für Symbole/Referenzen/Violations.
- Status: in_progress

## Epic 3: MCP-Pfadverträge und physische Dateinavigation

- Ziel: `inspect_assembly` weist die absoluten Projekt-, Projektverzeichnis- und Quellroot-Pfade transparent aus und lokale Dateisuche/-navigation funktioniert damit.
- Abhängigkeiten: Epic 1 und Epic 2.
- Betroffene Bereiche: `InspectAssemblyTool`, Formatter, Payload-/Response-Modelle, `get_file_tree`-Integration und Assembly-Navigation.
- Muss-/Akzeptanzkriterien: M3, M4 und M5; Header und JSON-Payload enthalten die drei absoluten Pfade; nachfolgende Symbolantworten verweisen auf reale `.cs`-Dateien ohne redundante Projekt-Header.
- Verifikation: Pfad-/Payload-Tests, `rg`- und `get_file_tree`-Nachweise auf einem dekompilierten Root sowie MCP-Violationscheck.
- Status: open

## Epic 4: Partielle Snapshots, Fehlersemantik und Betriebsresilienz

- Ziel: Fehlerhafte oder unvollständige Decompilate bleiben als nutzbare partielle Sessions verfügbar und werden sicher beendet bzw. bereinigt.
- Abhängigkeiten: Epic 1 bis Epic 3.
- Betroffene Bereiche: `ValidateCompilation`, Session-Status/Diagnostik, Resolver-/Workspace-Fehlerpfade, Cache-Cleanup und Timeout/Cancellation.
- Muss-/Akzeptanzkriterien: M7; Syntax-/Typfehler verwerfen den Snapshot nicht; Status `Partial`/`Degraded`, verfügbare Dateien und funktionierende Typen bleiben abfragbar; Abbruch veröffentlicht keinen unvollständigen Cache.
- Verifikation: Resilienz-, Cancellation-, Timeout- und Cleanup-Tests sowie gezielte Status-/MCP-Prüfungen.
- Status: open

## Epic 5: Testlaufzeit und Gate-Stabilität

- Ziel: Lang laufende oder unkontrolliert hängende Tests identifizieren, reproduzieren und die betroffenen Tests/Testharnesses so bereinigen, dass die Nicht-Stress-Gates deterministisch ohne manuelles Abbrechen durchlaufen.
- Abhängigkeiten: Epic 1 bis Epic 4; bekannte Befunde sind `AssemblyAnalysisRegistryRetirementRaceTests`, `ProjectRegistryTests`, `ThinClientPumpContractTests` sowie die übersprungenen External-Source-/Symlink-Tests.
- Betroffene Bereiche: FastTests-/IntegrationTests-Testharness, Testkategorien, Prozess-/Cancellation-/Timeout-Steuerung und nur direkt betroffene Testfälle; Produktionsverträge nur, wenn die Testanalyse einen echten Produktfehler belegt.
- Muss-/Akzeptanzkriterien: Beide vollständigen Nicht-Stress-Testläufe liefern einen terminalen, reproduzierbaren Befund ohne manuelles Ctrl+C; echte Testfehler werden behoben oder mit konkreter, begründeter Disposition dokumentiert; keine künstliche Absenkung der Testabdeckung und keine Stress-Tests im normalen Gate.
- Verifikation: wiederholte vollständige `Category!=Stress`-Läufe mit Zeit-/Abbruchnachweis, gezielte Tests der gefundenen Ursachen, `dotnet build` und gezielter `get_violations`-Check nach der letzten Änderung.
- Status: open

## Epic 6: Regression, Dokumentation und Gesamtabschluss

- Ziel: Bestehende Assembly-Routen, Tests und Dokumentation auf den neuen Vertrag umstellen und den vollständigen Abschlussnachweis erbringen.
- Abhängigkeiten: Epic 1 bis Epic 5.
- Betroffene Bereiche: FastTests, IntegrationTests, `Docs/configuration.md`, bei Bedarf `Docs/ROADMAP.md`, Agent-Regelsynchronisation und Abschlussartefakte.
- Muss-/Akzeptanzkriterien: M12; `dotnet build` ohne Fehler/Warnungen sowie beide vollständigen Nicht-Stress-Testläufe grün; keine Non-Goals umgesetzt.
- Verifikation: vollständige Konzept-Checkliste einschließlich Materialisierung, Pfadausgabe, Dateibaum, echter Body, Partial/Degraded, Konfiguration, atomarer Veröffentlichung und Lock-Toleranz.
- Status: open

## Abschluss-Checkliste

- [ ] M1–M11 fachlich umgesetzt und gezielt verifiziert
- [ ] M12: `dotnet build` ohne Fehler/Warnungen
- [ ] M12: FastTests `Category!=Stress` nach Epic 5 terminal grün
- [ ] M12: IntegrationTests `Category!=Stress` nach Epic 5 terminal grün
- [ ] Konzeptbezogene MCP-/Dateisystem-Nachweise dokumentiert
- [ ] Task-lokale Tech-Debt-Queue leer oder mit Disposition ausgewiesen
