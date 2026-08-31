# Linse 01 — Assembly-Routing, Decompilation und Metadaten

- Reviewstatus: Orchestrator-Fallback; kein unabhängiger Reviewer verfügbar (`collab spawn failed: agent thread limit reached`). Der Bericht ist keine unabhängige Zweitprüfung.
- Revision: `ec97fa84`; Produktionsquellen blieben seit der Audit-Baseline unverändert.
- MCP-Parameter: `targetType=project`, `targetPath=<repo-root-redacted>` für Scope-/Symbolabfragen; `targetType=assembly`, `targetPath=<neutral-built-dll>` für die DLL-Probe. Keine lokalen Installationspfade oder externen URLs werden wiedergegeben.

## Abdeckung

Geprüft wurden `AnalysisTargetResolver`, `AssemblyAnalysisDispatcher`, `AssemblyAnalysisSession`, `AssemblyAnalysisLease`, `AssemblyAnalysisService`, die Assembly-Symbolnavigation sowie die Registrierungen für `inspect_assembly`, `find_assembly_extensions`, `find_symbol` und `find_references`. Zusätzlich wurde ein lokales Build-Artefakt metadata-only mit `inspect_assembly` und `find_assembly_extensions` abgefragt.

## Befund ASM-001

- Schweregrad: S1
- Umfang: U3 — zentraler Dispatcher, mehrere Assembly-Tools
- Konfidenz: hoch
- Bereich: Referenzauflösung und Default-Semantik
- Evidenz: `src/AiNetLinter/Mcp/AnalysisToolCall.cs:161-172` ruft `lease.ExpandReferencesAsync(...)` vor jedem `assemblyCall` auf. Die sichtbaren `includeReferences`-Defaults liegen in `src/AiNetLinter/Mcp/Registration/SymbolGraphToolRegistrations.cs:49`, `:80` und `:112` jeweils bei `false`. Der veröffentlichte Vertrag beschreibt in `Docs/agent-api.md:460` für den Assembly-Symbol-Branch den Root-Snapshot als Default bei `includeReferences=false`.
- Auswirkung: Ein Default-Aufruf kann Referenz-Sessions öffnen, externe bzw. fehlende Abhängigkeiten diagnostizieren und den Status auf `partial` setzen, obwohl der Agent keine Referenznavigation angefordert hat. Das vergrößert Kosten und verändert die Semantik der Default-Antwort. `inspect_assembly` und `find_assembly_extensions` sind ebenfalls betroffen, da sie denselben Dispatcher verwenden.
- Reproduktion: Einen Assembly-Route-Call mit `includeReferences=false` und einer absichtlich nicht auflösbaren Referenz ausführen; vor dem Handler-Aufruf werden die Referenzexpansion und deren Diagnosen bereits erzeugt. Der bestehende Test `AssemblyAnalysisRouteTests.AssemblyRoute_IncludeReferencesNavigatesSymbolsReferencesAndCallTree` deckt den positiven `true`-Pfad ab, aber keinen Negativtest auf „keine Expansion bei false“.
- Disposition: Für die Folgeimplementierung zurückgestellt; Audit-only-Auftrag verbietet die Änderung. Empfohlene Regression: Dispatcher soll `includeReferences` bzw. eine explizite Expansion-Capability bis zum Handler durchreichen und bei `false` keine Child-Leases öffnen.

## Befund ASM-002

- Schweregrad: S2
- Umfang: U2 — Assembly-`find_references`-Navigation
- Konfidenz: hoch
- Bereich: Vollständigkeitslabel bei Namensauflösung über mehrere Sessions
- Evidenz: `src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblySymbolResolver.cs:30-61` sammelt für jede Lease ohne Treffer den von `FindReferencesTool.ResolveSymbolAsync` gelieferten `SymbolNotFound`-Text als Diagnose, sofern kein Assembly-Identifikator verwendet wird. `AssemblyNavigationSupport.CreateSummary` setzt in `:41-55` bei jeder nichtleeren Diagnose `completeness=partial`. Ein Nichttreffer in einer anderen durchsuchen Lease ist dabei nicht automatisch ein Auflösungsfehler.
- Auswirkung: Ein erfolgreich in einer Session gefundenes Symbol kann für einen qualifizierten oder teilqualifizierten Namen als `partial` erscheinen, weil andere Sessions den Namen erwartbar nicht enthalten. Agenten können dadurch eine echte Referenzlücke mit bloßer Nichtzuständigkeit anderer Assemblies verwechseln.
- Reproduktion: `find_references` mit `targetType=assembly`, `includeReferences=true` und einem Namen aufrufen, der nur in der Root-Assembly existiert; die Resolver-Schleife erzeugt für die übrigen Sessions `SymbolNotFound`-Diagnosen und projiziert sie in `navigation.completeness`.
- Disposition: Als Folgearbeit zurückgestellt. Nichttreffer anderer Assemblies sollten von echten Session-/Referenzdiagnosen getrennt werden; die vorhandene Herkunfts- und Sessiondiagnostik soll erhalten bleiben.

## Beobachtung ohne bestätigten Defekt

Die DTOs in `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisModels.cs:79-117` liefern Member-Signatur, Parameter, Generics, Constraints und Attribute, aber keine eigene Member-ID. Die Symbolgraph-APIs besitzen dagegen stabile IDs. Das ist eine offene Konsistenzfrage, jedoch kein bestätigter Vertragsverstoß, weil die Assembly-Inspection-Dokumentation keine Member-ID verspricht.

Die neutrale Decompilation-Probe (`inspect_assembly`, exakter Typfilter, `publicOnly=false`) war `isError=false`, aber `completeness=partial`, `sessionStatus=partial`, `totalTypes=0` und enthielt semantische Decompiler-/Referenzdiagnosen. Ursache waren nicht identische Referenzversionen im Analyseumfeld; daraus wird kein Produktdefekt abgeleitet. Eine source-backed Live-Probe war in diesem Lauf nicht verfügbar.

## Verifikation

Die direkten Assembly- und Session-Tests sind im Repository vorhanden (`AssemblyAnalysisToolTests`, `AssemblyAnalysisSessionTests`, `AssemblyAnalysisDispatcherCapabilityTests`). Der abschließende vollständige Nicht-Stress-Testlauf wird separat im Orchestrator-Log protokolliert.
