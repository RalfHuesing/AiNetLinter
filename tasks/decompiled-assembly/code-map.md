# Code Map

## Primäre Einstiegspunkte

- `src/AiNetLinter/Mcp/Assemblies/Analysis/Bodies/AssemblyDecompiledBodyResolver.cs` — Body-Auflösung, direkt aufgerufen durch `AssemblyDecompilationAdapter`.
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyDecompilationCache.cs` und `AssemblyDecompilationCache.PointerPublishing.cs` — Generation-Publish, Current-Pointer und Cache-Lebenszyklus, genutzt von `AssemblyAnalysisSession` und `AssemblyDecompilationAdapter`.
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyReferenceResolver.cs` — Assembly-Identitäts-/Referenzkandidatenauflösung, genutzt von Assembly-Sessions, Referenzexpansion und Source-Reference-Graph.
- `src/AiNetLinter/Mcp/Tools/SymbolGraph/SymbolIdentifierResolver.cs` — stabile Symbol-ID-Auflösung, genutzt von `FindReferencesTool`, Hierarchie- und Assembly-Registry-Pfaden.
- `src/AiNetLinter/Mcp/Daemon/DaemonRuntimeContext.cs`, `DaemonRegistryAdapter.cs`, `DaemonHost.cs` — Daemon-Snapshot-Provider im Verbindungs-Kontext.
- `src/AiNetLinter/Mcp/Tools/ServerMaintenance/GetServerHealthTool.cs` sowie `src/AiNetLinter/Mcp/Registration/ServerMaintenanceToolRegistrations.cs` — Health-Ausführung und Registrierungs-/Routingpfad.

## Betroffene Dateien und Symbole

- `AssemblyDecompiledBodyResolver` (`FindMember`, `MatchesMember`, `MatchesAccessor`, Typnamensauflösung; aktuell `:16-297`) unterstützt Top-Level-`INamedTypeSymbol`, Struct/Enum/Record/Interface und Property-/Event-Accessor-Syntax.
- `AssemblyDecompilationCache.Publish` (aktuell `:66-114`) unterscheidet eigene Publikation, konkurrierend vorhandene Generation und Fehler; `TryPublishPointer`/`PublishPointerAttempt` liegen für die Größeninvariante in `AssemblyDecompilationCache.PointerPublishing.cs` (`:9-147`). Bei Cache-Hit wird die tatsächliche Current-Pointer-Generation zurückgegeben, das lokale temporäre Verzeichnis bleibt cleanup-fähig.
- `AssemblyReferenceResolver.IdentityMatches` (`:356-360`) toleriert Versionen nur für `mscorlib`, `System.*`, `Microsoft.*` und `WindowsBase*`; Kultur und Drittanbieter-Versionen bleiben strikt. Die Prefix-/Namensentscheidung liegt in `IsVersionTolerantFrameworkAssembly` (`:362-364`).
- `SymbolIdentifierResolver.TryResolveByStableIdAsync` (`:131-178`) nutzt für Assembly-IDs nach exaktem Match einen eindeutigen, marker-/shape-toleranten Fallback; Parsing und Parameterzählung liegen in den privaten Helfern `:180-266`.
- `DaemonRuntimeContext.FindProjectSnapshot`, `IDaemonRegistry.FindSnapshot`, `DaemonHost.CreateRuntimeContext`, `GetServerHealthTool.ExecuteDaemonProjectAsync` und `ServerMaintenanceToolRegistrations.ExecuteGetServerHealthAsync` bilden die explizite Daemon-Projekt-Route.
- Paket-2/3/4-Bereiche sind Non-Goal; die Änderungen bleiben auf Paket 1 und die dafür nötige Daemon-Kontext-Schnittstelle beschränkt.

## Aufrufer und Abhängigkeiten

- `AssemblyDecompilationAdapter` ruft den Body-Resolver auf; `AssemblyAnalysisSession` bindet Cache und Reference-Resolver in den Assembly-Snapshot-Lifecycle ein.
- Der Cache wird zusätzlich von `AssemblyDiagnosticCodes`, Session-Tests und der Adapter-Pipeline verwendet; `Publish` löscht im `finally` nur die eigene, noch nicht als publiziert markierte Generation. `RetainGenerations` kennt jedoch keine in-flight Publisher und kann eine Generation zwischen Pointer-Erfolg und Return eines konkurrierenden Publishers entfernen.
- `AssemblyReferenceSessionExpander` und `SourceProjectReferenceGraph` verwenden `AssemblyReferenceResolver`; die vollständige MCP-Caller-Liste war auf 20/39 gekappt und wird lokal nicht als vollständig behauptet.
- `FindReferencesTool` und `GetTypeHierarchyFormatter` verwenden `SymbolIdentifierResolver`; der Fallback läuft nur nach erfolgreicher Assembly-Identitätsprüfung.
- `DaemonHost` reicht den Snapshot-Provider in jede `DaemonRuntimeContext`-Instanz; Health-Registrierung und `GetServerHealthTool` verwenden ihn für gezielte Projektziele.
- `GetServerHealthResponseBuilder` bleibt reine Projektion; Cache-/Resolver-/Health-Änderungen verändern keine Assembly-Lade- oder Ausführungssemantik.

## Relevante Tests, Konfiguration und Dokumentation

- Cache-/Session-Bereich: `AssemblyAnalysisSessionTests.cs` enthält jetzt den gezielten parallelen Publish-Test neben den bestehenden Session-/Cache-Tests; der Test startet mit einem initialen Publish und verifiziert zwei parallele Cache-Hits sowie ausschließlich existierende Generation-Verzeichnisse. Konkurrierende Erst-Publisher mit demselben Cache-Key, aber unterschiedlichen Fingerprints, sind nicht abgedeckt.
- Body-/Navigation-Bereich: `AssemblyAnalysisPathContractTests.cs` enthält den Top-Level-/Struct-/Enum-/Record-/Interface-/Getter-/Setter-Test; `AssemblyDecompiledBodyResolver` ist als Coverage-Symbol markiert.
- Framework-Unification: neue `AssemblyReferenceResolverTests.cs` (`:19-52`) deckt `mscorlib`, `System.*`, `Microsoft.*`, `WindowsBase`/`WindowsBase.*`, Kulturbindung, Drittanbieter-Versionen und Nicht-Präfix-Ähnlichkeit ab; der bestehende falsche Drittanbieter-Versionsfall bleibt erhalten.
- Stable-ID: `SymbolIdentifierResolverTests.cs` enthält jetzt die Marker-Regression neben den bestehenden 11 Tests.
- Daemon-Health: `WiringProjectContractTests.cs` enthält den Tool-Level-Proxy-Kontext-Test mit einem daemon-residenten Snapshot; die private `ServerMaintenanceToolRegistrations`-Closure wird dabei nicht direkt ausgeführt. `GetServerHealthToolTests.cs` behält die 7 Integrationstests für die öffentliche Health-Projektion.
- Testverträge und Kategorien bleiben im bestehenden Fast-/Integration-Testaufbau; der neue Cache-Test ist gezielt und nicht als Stress-Test markiert.
- `tasks/decompiled-assembly/Konzept.md` und `roadmap.md` sind Navigationshilfe; `rules.json`, `Docs/`, `README.md` und `instructions.md` sind für Paket 1 nur zu ändern, falls die tatsächliche Umsetzung einen dort dokumentierten Vertrag berührt.

## Invarianten, Risiken und Unsicherheiten

- Fremde Assemblies bleiben metadata-only; kein dynamisches Laden oder Ausführen.
- Framework-Unification bleibt auf `mscorlib`, `System.*`, `Microsoft.*` und `WindowsBase*` begrenzt; Drittanbieter-Versionen bleiben strikt, Kultur bleibt bindend.
- Der Cache darf kein erfolgreich publiziertes oder zurückgegebenes Generation-Verzeichnis im Fehlerpfad löschen; lokal temporäre Generationen werden von `Publish` bereinigt, während `RetainGenerations` ausschließlich die übergebene aktuelle Generation und höchstens einen Vorgänger schützt und keine weiteren in-flight Publisher kennt.
- Skeleton-ID-Fallback akzeptiert keine stale Assembly-Identität und liefert bei fehlender oder mehrdeutiger Zuordnung weiterhin `null` für den bestehenden Fallbackpfad.
- Der Daemon-Kontext referenziert für gezielte Projekt-Health den residenten `ProjectSnapshot`; ohne Snapshot bleibt `PROJECT_NOT_INITIALIZED` korrekt.
- Die MCP-Caller-Liste des `AssemblyReferenceResolver` war bei 20/39 gekappt; dieser bekannte Evidenzrest bleibt als Risiko dokumentiert.

## Verifikation

- MCP-first-Kontext am 2026-09-02: `get_feature_context` mit `targetType=project` und absolutem Projektroot für die fünf Ausgangssymbole; alle wurden aufgelöst und meldeten 0 offene Violations. Nach den Änderungen bestätigten die fünf zentralen Feature-Kontexte erneut 0 Datei-/Symbol-Violations; `find_references` meldete vollständige, nicht trunkierte direkte Aufruferlisten für Publish (3), Health-Proxy (2), Snapshot-Zugriff (2), IdentityMatches (5) und Stable-ID (5).
- Nach der Änderung: fokussierte Paket-1-Tests 52/52 grün; vollständige `FastTests --filter Category!=Stress` 2360 bestanden, 2 übersprungen; gezielte Health-Integration 7/7 grün; `dotnet build --no-restore` grün mit 0 Warnungen/0 Fehlern. Der vorgeschriebene vollständige Integration-Lauf endete mit 377/379 bestanden und zwei Fehlern außerhalb der Paket-1-Assertions: `PROJECT_NOT_RESTORED` im Whole-Solution-CLI-Dogfood sowie der bekannte Live-Safeguard-Korridor.
- Abschluss-Audit mit `targetType=project`, absolutem Projektroot und MCP-Scope `src/AiNetLinter/Mcp`: `find_duplicates` 0 Cluster bei 1504 Methoden; `find_magic_values` 0 Treffer; `find_dead_code` 37 Low-Confidence-Kandidaten, 0 High-Confidence-Kandidaten (nicht sicher löschbar); `safeguard` 1,0/10 mit 6 bestehenden `AIContextFootprint`-Warnungen in Paket-3-nahen Typen. Diese Befunde wurden nicht in Paket 1 hineingezogen.
- Der abschließende gezielte `get_violations`-Check nach der letzten Codeänderung bleibt der letzte MCP-Nachweis im terminalen Hand-off; er wird mit `scopeFilter=src/AiNetLinter/Mcp`, `maxResults=200`, `includeSnippet=false` und `contextLines=0` ausgeführt und vollständig berichtet.
