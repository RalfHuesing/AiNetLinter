# Code Map

## Primäre Einstiegspunkte

- `src/AiNetLinter/Mcp/Assemblies/Analysis/Bodies/AssemblyDecompiledBodyResolver.cs` — Body-Auflösung, direkt aufgerufen durch `AssemblyDecompilationAdapter`; MCP bestätigte den aktuellen Scope `:16-324` und den direkten Aufrufer in `AssemblyDecompilationAdapter.cs:23`.
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyDecompilationCache.cs`, `AssemblyDecompilationCache.PointerPublishing.cs` und `AssemblyDecompilationCache.Locking.cs` — Generation-Publish, Current-Pointer, Retention-Synchronisierung und Cache-Lebenszyklus; MCP bestätigte `Publish` `:69-118`.
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyReferenceResolver.cs` — Assembly-Identitäts-/Referenzkandidatenauflösung, genutzt von Assembly-Sessions, Referenzexpansion und Source-Reference-Graph; MCP bestätigte `IdentityMatches` `:357-361` und `IsVersionTolerantFrameworkAssembly` `:363-366`.
- `src/AiNetLinter/Mcp/Tools/SymbolGraph/SymbolIdentifierResolver.cs` — stabile Symbol-ID-Auflösung, genutzt von `FindReferencesTool`, Hierarchie- und Assembly-Registry-Pfaden.
- `src/AiNetLinter/Mcp/Daemon/DaemonRuntimeContext.cs`, `DaemonRegistryAdapter.cs`, `DaemonHost.cs` — Daemon-Snapshot-Provider im Verbindungs-Kontext.
- `src/AiNetLinter/Mcp/Tools/ServerMaintenance/GetServerHealthTool.cs` sowie `src/AiNetLinter/Mcp/Registration/ServerMaintenanceToolRegistrations.cs` — Health-Ausführung und Registrierungs-/Routingpfad.

## Betroffene Dateien und Symbole

- `AssemblyDecompiledBodyResolver` (`FindMember`, `MatchesMember`, `MatchesAssociatedMember`, `FindAccessor`, `MatchesAccessor`, Typnamensauflösung; MCP aktuell `:16-324`) unterstützt Top-Level-`INamedTypeSymbol`, Struct/Enum/Record/Interface und Property-/Event-Accessor-Syntax. Accessors werden über `AssociatedSymbol` gegen einen direkten Property-/Indexer-/Event-Member und danach ausschließlich dessen Accessor-Liste aufgelöst.
- `AssemblyDecompilationCache.Publish` (MCP nach Änderung `:69-118`) unterscheidet eigene Publikation, konkurrierend vorhandene Generation und Fehler; `TryPublishPointer`/`PublishPointerAttempt` liegen in `AssemblyDecompilationCache.PointerPublishing.cs`. `AssemblyCacheKeyLockRegistry` synchronisiert Publish und Retention pro kanonischem Entry über die gesamte `Publish`-Lebensdauer; der lokale Return-Seam ermöglicht den verzögerten Race-Test.
- `AssemblyReferenceResolver.IdentityMatches` (`:357-361`) toleriert Versionen für `mscorlib`, exakt `System`, `System.*`, `Microsoft.*` und `WindowsBase*`; Kultur und Drittanbieter-Versionen bleiben strikt. `Systemish` bleibt ausgeschlossen.
- `SymbolIdentifierResolver.TryResolveByStableIdAsync` (`:131-178`) nutzt für Assembly-IDs nach exaktem Match einen eindeutigen, marker-/shape-toleranten Fallback; Parsing und Parameterzählung liegen in den privaten Helfern `:180-266`.
- `DaemonRuntimeContext.FindProjectSnapshot`, `IDaemonRegistry.FindSnapshot`, `DaemonHost.CreateRuntimeContext`, `GetServerHealthTool.ExecuteDaemonProjectAsync` und `ServerMaintenanceToolRegistrations.ExecuteGetServerHealthAsync` bilden die explizite Daemon-Projekt-Route.
- Paket-2/3/4-Bereiche sind Non-Goal; die Änderungen bleiben auf Paket 1 und die dafür nötige Daemon-Kontext-Schnittstelle beschränkt.

## Aufrufer und Abhängigkeiten

- `AssemblyDecompilationAdapter` ruft den Body-Resolver auf; `AssemblyAnalysisSession` bindet Cache und Reference-Resolver in den Assembly-Snapshot-Lifecycle ein.
- MCP meldete nach der Änderung für `Publish` sechs direkte Aufrufer (Produktionsaufruf sowie fünf direkte Teststellen); `RetainGenerations` hat weiterhin zwei direkte Aufrufer (Cleanup-Test und `Publish`). Der Cache wird zusätzlich von weiteren Cache-/Registry-/External-Source-Tests verwendet.
- Der per Entry ref-counted Lock wird vor der Publish-Validierung erworben und erst nach dem `finally`-Cleanup freigegeben. Damit können konkurrierende Publisher/Retention den Entry nicht zwischen erfolgreichem Pointer-/Retention-Abschluss und dem `Publish`-Return mutieren; unterschiedliche Entries bleiben parallel.
- `AssemblyReferenceSessionExpander` und `SourceProjectReferenceGraph` verwenden `AssemblyReferenceResolver`; die vollständige MCP-Caller-Liste war auf 20/39 gekappt und wird lokal nicht als vollständig behauptet.
- `FindReferencesTool` und `GetTypeHierarchyFormatter` verwenden `SymbolIdentifierResolver`; der Fallback läuft nur nach erfolgreicher Assembly-Identitätsprüfung.
- `DaemonHost` reicht den Snapshot-Provider in jede `DaemonRuntimeContext`-Instanz; Health-Registrierung und `GetServerHealthTool` verwenden ihn für gezielte Projektziele.
- `GetServerHealthResponseBuilder` bleibt reine Projektion; Cache-/Resolver-/Health-Änderungen verändern keine Assembly-Lade- oder Ausführungssemantik.

## Relevante Tests, Konfiguration und Dokumentation

- Cache-/Session-Bereich: `AssemblyAnalysisSessionTests.cs` enthält den bestehenden Cache-Hit-Test und `AssemblyDecompilationCache_DifferentFingerprintsKeepDelayedPublishResultUntilReturn`; letzterer startet drei unterschiedliche Fingerprints auf demselben Entry und hält den ersten Return vorübergehend an. `AssemblyDecompilationCache.Locking.cs` enthält die bounded Lock-Registry.
- Body-/Navigation-Bereich: `AssemblyAnalysisPathContractTests.cs` enthält den Top-Level-/Struct-/Enum-/Record-/Interface-/Getter-/Setter-Test sowie die Regression für zwei gleichartige Properties, einen Indexer und zwei Event-Accessorpaare; `AssemblyDecompiledBodyResolver` ist als Coverage-Symbol markiert.
- Framework-Unification: `AssemblyReferenceResolverTests.cs` prüft `mscorlib`, den exakten Namen `System`, `System.*`, `Microsoft.*`, `WindowsBase`/`WindowsBase.*`, Kulturbindung, Drittanbieter-Versionen und `Systemish`-Ähnlichkeit.
- Stable-ID: `SymbolIdentifierResolverTests.cs` enthält jetzt die Marker-Regression neben den bestehenden 11 Tests.
- Daemon-Health: `WiringProjectContractTests.cs` enthält den Tool-Level-Proxy-Kontext-Test mit einem daemon-residenten Snapshot; die private `ServerMaintenanceToolRegistrations`-Closure wird dabei nicht direkt ausgeführt. `GetServerHealthToolTests.cs` behält die 7 Integrationstests für die öffentliche Health-Projektion.
- Testverträge und Kategorien bleiben im bestehenden Fast-/Integration-Testaufbau; der neue Cache-Test ist gezielt und nicht als Stress-Test markiert.
- `tasks/decompiled-assembly/Konzept.md` und `roadmap.md` sind Navigationshilfe; `rules.json`, `Docs/`, `README.md` und `instructions.md` sind für Paket 1 nur zu ändern, falls die tatsächliche Umsetzung einen dort dokumentierten Vertrag berührt.

## Invarianten, Risiken und Unsicherheiten

- Fremde Assemblies bleiben metadata-only; kein dynamisches Laden oder Ausführen.
- Framework-Unification bleibt auf `mscorlib`, `System.*`, `Microsoft.*` und `WindowsBase*` begrenzt; Drittanbieter-Versionen bleiben strikt, Kultur bleibt bindend.
- Der Cache darf kein erfolgreich publiziertes oder erfolgreich gemeldetes Generation-Verzeichnis bis zum Return löschen; lokal temporäre Generationen werden von `Publish` bereinigt. Die bestehende Retention-Semantik (aktuelle Generation plus höchstens ein Vorgänger) bleibt erhalten und ist pro Entry gegen konkurrierende Publisher synchronisiert.
- Skeleton-ID-Fallback akzeptiert keine stale Assembly-Identität und liefert bei fehlender oder mehrdeutiger Zuordnung weiterhin `null` für den bestehenden Fallbackpfad.
- Der Daemon-Kontext referenziert für gezielte Projekt-Health den residenten `ProjectSnapshot`; ohne Snapshot bleibt `PROJECT_NOT_INITIALIZED` korrekt.
- Die MCP-Caller-Liste des `AssemblyReferenceResolver` war bei 20/39 gekappt; dieser bekannte Evidenzrest bleibt als Risiko dokumentiert.

## Verifikation

- MCP-first-Kontext am 2026-09-02: `get_feature_context` mit `targetType=project` und absolutem Projektroot für die fünf Ausgangssymbole; alle wurden aufgelöst und meldeten 0 offene Violations. Nach den Änderungen bestätigten die fünf zentralen Feature-Kontexte erneut 0 Datei-/Symbol-Violations; `find_references` meldete vollständige, nicht trunkierte direkte Aufruferlisten für Publish (3), Health-Proxy (2), Snapshot-Zugriff (2), IdentityMatches (5) und Stable-ID (5).
- Vor der abschließenden whitespace-only Codeänderung: vollständige `FastTests --filter Category!=Stress` 2363 bestanden, 2 übersprungen; `IntegrationTests --filter Category!=Stress` 378 bestanden, 1 fehlgeschlagen wegen des bestehenden Live-Safeguard-Korridors (`Score 1,154... < 5,0`). Nach der letzten Codeänderung liefen die fokussierten Body-/Cache-/Resolver-/Cleanup-Tests 33/33 grün und `dotnet build --no-restore` mit 0 Warnungen/0 Fehlern. MCP-Impact: 6 Dateien/16 Symbole/0 Violations; `find_duplicates`: 0 Cluster bei 1509 Methoden; `find_magic_values`: 0 Treffer; `find_dead_code`: 0 High- und 37 Low-Confidence-Kandidaten.
- Abschluss-Audit mit `targetType=project`, absolutem Projektroot und MCP-Scope `src/AiNetLinter/Mcp`: `find_duplicates` 0 Cluster bei 1504 Methoden; `find_magic_values` 0 Treffer; `find_dead_code` 37 Low-Confidence-Kandidaten, 0 High-Confidence-Kandidaten (nicht sicher löschbar); `safeguard` 1,0/10 mit 6 bestehenden `AIContextFootprint`-Warnungen in Paket-3-nahen Typen. Diese Befunde wurden nicht in Paket 1 hineingezogen.
- Der abschließende gezielte `get_violations`-Check wurde nach der letzten Codeänderung mit `targetType=project`, absolutem Projektroot, `scopeFilter=src/AiNetLinter/Mcp`, `maxResults=200`, `includeSnippet=false` und `contextLines=0` ausgeführt: 6 Warnungen, 0 Fehler; alle 6 Warnungen betreffen bestehende `AIContextFootprint`-Befunde außerhalb der geänderten Paket-1-Dateien.
