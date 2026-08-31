# Linse 01 – Assembly-Zielrouting, Decompilation und Navigation

## Reviewurteil

**issues** – ein bestätigter S1-Befund verletzt den Assembly-Zielvertrag für die Standard-Symbolnavigation; zusätzlich besteht ein S2-Befund zur Batch-Vollständigkeit.

## Review-Metadaten

- **Linse:** Assembly-Zielrouting, `targetType=assembly`, absolute DLL-Pfade, Pfadvalidierung, Metadata-only-Grenze, statische Decompilation, Source-Mapping sowie Referenz- und Symbolnavigation.
- **Geprüfter Scope:** `src/AiNetLinter/Mcp/AnalysisToolCall.cs`, `src/AiNetLinter/Mcp/AnalysisTarget*.cs`, `src/AiNetLinter/Mcp/Assemblies/Analysis/**`, `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/**`, `src/AiNetLinter/Mcp/Tools/SymbolGraph/**`, `src/AiNetLinter/Mcp/Tools/CallTree/**`, die zugehörigen Registrierungen und Assembly-Fast-Tests.
- **Revision:** `c942350d…` (der Commit enthält nur parallele Audit-Artefakte; der geprüfte Produktions- und Testcode blieb gegenüber der Recherche-Revision unverändert).
- **Working Tree:** Bei meiner Ausgangsprüfung waren Source-, Test-, Konfigurations- und Dokumentationsdateien unverändert. Während der parallelen Audit-Welle kamen fremde Änderungen an `code-map.md` und mehreren anderen Audit-Reports hinzu; sie wurden von mir nicht bearbeitet. Meine einzige Schreibaktion war dieser Report.
- **MCP-Parameter (redigiert):** projektgebundene Abfragen mit `targetType=project`, `targetPath=<absoluter Repository-Pfad>`; DLL-Proben mit `targetType=assembly`, `targetPath=<absoluter Pfad zu einer lokalen Test-DLL>`. Keine Installationspfade, Zugangsdaten oder vollständigen externen URLs werden wiedergegeben.
- **Code-Map:** Der aktuelle Inhalt von `tasks/decompiled-assembly-analysis-audit/code-map.md` ist für diese Linse korrekt. Die dort genannten Assembly-, Source-Mapping- und Symbolgraph-Bereiche wurden im MCP-Symbolgraphen wiedergefunden; auch die nachgeschärfte Aussage, dass die Referenzexpansion vor jedem Assembly-Handler erfolgt, stimmt. Ich habe keine Zeile der Datei geändert.

### Nicht geprüfte Bereiche

Nicht Gegenstand waren die übrigen Audit-Linsen, Stress-Tests, nicht assemblybezogene CLI-/Regeländerungen sowie eine echte providerbasierte Live-Source-Zuordnung. Ein früher gezielter Integrationstestlauf lief unter konkurrierenden Test-/Serverprozessen und verlor den MCP-Transport; der danach isoliert ausgeführte vollständige Nicht-Stress-Lauf war grün.

## Executive Summary

### Befunde

1. **ASM-001 – Referenzexpansion läuft unbedingt vor jedem Assembly-Handler.** Der Dispatcher expandiert auch bei `includeReferences=false` vor dem Handleraufruf. Dadurch werden Referenzsessions, Diagnosen und Kosten erzeugt, obwohl der dokumentierte Default eine Root-only-Abfrage beschreibt.
2. **ASM-002 – `find_symbol` verliert bei mehreren Mustern frühere Trunkierungsdiagnosen.** `BuildResponseAsync` überschreibt die Navigation pro Muster und gibt nur die Navigation des letzten Musters zurück. Ein früheres Muster kann auf `maxResults` begrenzt sein, während das letzte Muster die Batch-Antwort ohne diese Information abschließt.
3. **ASM-003 – Namensauflösung behandelt erwartbare Nichttreffer anderer Sessions als Partialdiagnose.** Bei einer nicht identitätsqualifizierten Suche werden `SymbolNotFound`-Ergebnisse aus Sessions, die das Symbol nicht deklarieren, in die gemeinsame Diagnosemenge aufgenommen; dadurch kann ein in einer Session gefundenes Symbol global als `partial` erscheinen.

### Orchestrator-Abgleich eines unabhängigen Probehinweises

Der unabhängige Reviewer hatte den Unterschied zwischen `includeReferences=false` und `true` mit `CancellationToken` demonstriert. Dieser Name bezeichnet im verwendeten Probeaufruf einen referenzierten Basistyp und isoliert daher nicht die Root-Symbolmenge der geprüften DLL; dass `includeReferences=true` dort zusätzliche Treffer liefert, ist für sich kein Nachweis eines falschen Assembly-Routings. Dieser Probehinweis wird deshalb nicht als eigener Tech-Debt-Befund übernommen. Der bestätigte Default-Vertragsbefund ist die unbedingte Referenzexpansion im gemeinsamen Dispatcher; der `lease.Server`-Root selbst bleibt als dekompilierte Root-Solution bestehen.

### Bestätigte Erwartungen

- Die Dispatcher-Route validiert `targetType` und `targetPath`; Assembly-Ziele müssen absolut, existent und auf eine DLL-Datei zeigen (`AnalysisTargetResolver.Resolve` sowie `AssemblyAnalysisService.TryValidatePath`).
- Die Assembly-Analyse liest PE-Metadaten aus einem Dateistream, erzeugt dekompilierte Dokumente mit festen Budgets und markiert fehlende Referenzen, Syntax-/Semantikdiagnosen und Budgetgrenzen als `partial`. Die Live-Antworten enthielten `origin=decompiled`, `snapshot=none`, `trust=untrusted` und `status/completeness=partial`.
- Die Decompilation-Konfiguration deaktiviert Member-Bodies sowie Debug-/XML-Dokumente; im geprüften Adapter ist kein Laufzeitladen der Zielassembly zu sehen. Der Test `InspectAssembly_RejectsRelativeAndMissingPathsWithoutRuntimeLoading()` bestätigt diese Grenze.
- Source-backed Mapping wird nur bei attested Auswahl, verifiziertem Provider, sauberem Checkout, passendem Snapshot und identischer Snapshot-Identität verwendet; andernfalls fällt die Factory deterministisch auf Decompilation zurück. Die einschlägigen Component-Tests bestätigen beide Pfade.
- Der positive Pfad `includeReferences=true` für Symbol-, Referenz- und Call-Tree-Navigation ist durch `AssemblyAnalysisRouteTests.AssemblyRoute_IncludeReferencesNavigatesSymbolsReferencesAndCallTree()` abgedeckt.

### Abdeckungsgrenzen

- Die direkte DLL-Probe konnte wegen der vorhandenen Referenz-/Decompilerdiagnosen keine vollständige Assembly-Compilation herstellen. Das beweist keinen Fehler der Decompilation; es bestätigt aber die vorgesehene sichtbare Partial-Semantik.
- Die source-backed Route wurde über Source-Code und vorhandene Fast-Tests
  geprüft; der nachträgliche Live-MCP-Aufruf hat zusätzlich den Checkout-
  Download bestätigt, aber keine source-backed Assembly-Antwort erreicht.
- Ein erster gezielter MCP-Integrationstestlauf endete unter konkurrierender Testlast mit 34 Fehlern und 6 Erfolgen (`MCP server process exited unexpectedly`); derselbe vollständige Nicht-Stress-Integrationslauf war anschließend isoliert mit 377/377 erfolgreich. Das war eine Testumgebungsgrenze, keine reproduzierbare Produktursache der Befunde.

## Nicht bestätigter Probehinweis ASM-ROUTING-01

**Titel:** Probe mit einem referenzierten Basistyp war nicht geeignet, falsches Root-Routing zu beweisen.

- **Komponente:** `SymbolGraphToolRegistrations`, `AssemblyFindSymbolTool`, `AssemblyFindReferencesTool`, `AssemblyGetCallTreeTool`.
- **Schweregrad:** keiner; verworfene Hypothese.
- **Umfang:** nicht klassifiziert.
- **Beweissicherheit:** niedrig für die behauptete Ursache.
- **Umgebungsabhängigkeit:** nein für die Ursache; die Live-Gegenprobe benötigt nur eine vorhandene DLL und kann durch deren Referenzdiagnosen zusätzlich `partial` sein.
- **Erwartung:** Die Root-Solution der Lease wird bei `includeReferences=false` durchsucht; Referenz-Sessions werden nur bei `true` einbezogen.
- **Beobachtung:** Die verwendete Probe mit `CancellationToken` zeigte bei `true` mehr Treffer als bei `false`, aber der Name ist in dieser DLL nicht zwingend im Root deklariert. Der Kontrollfluss über `lease.Server` repräsentiert die dekompilierte Root-Solution und ist daher kein Beleg für einen Projekt-Workspace-Fehler.
- **Auswirkung:** Keine bestätigte Produktwirkung. Die ursprüngliche S1-Einstufung wird verworfen.

### Konkrete Reproduktion

Mit demselben redigierten DLL-Ziel wurden folgende MCP-Aufrufe ausgeführt:

```text
find_symbol(targetType="assembly", targetPath=<absoluter DLL-Pfad>,
            namePatterns=["CancellationToken"], maxResults=10,
            includeReferences=false)
```

Ergebnis: `isError=false`, `analysis.targetType=assembly`, `analysis.origin=decompiled`, aber `results[0].matches=[]` und Text `Keine Treffer`. Der Gegenaufruf mit identischem Ziel und `includeReferences=true` lieferte sechs Treffer aus dekompilierten Assembly-Dokumenten.

Die analoge Probe mit `find_references(..., symbolIdentifier="CancellationToken", includeReferences=false)` endete mit `SYMBOL_NOT_FOUND`; mit `includeReferences=true` wurde das Symbol aufgelöst und eine strukturierte Navigation mit `visitedNodeCount=2` geliefert. `get_call_tree` zeigte dasselbe Muster: `false` → `SYMBOL_NOT_FOUND`, `true` mit einem vollqualifizierten Property-Symbol → strukturierte Root-/Navigation-Antwort.

### Belege und Disposition

- **MCP-/Code-Abgleich:** `AssemblyFind*Tool` delegiert im `false`-Branch an `lease.Server`; das ist die Root-Solution der Lease, nicht automatisch ein fremder Projekt-Workspace.
- **Probe:** `includeReferences=true` fand für den referenzierten Typ zusätzliche Treffer; die Probe konnte damit nur die erwartete Referenzaufnahme zeigen.
- **Disposition:** `rejected/not-applicable` als eigenständiger Root-Routing-Befund. Der separate Dispatcher-Befund `ASM-001` folgt unmittelbar.

### Nicht umgesetzte Remediation-Hypothese

Keine Änderung wurde im Audit umgesetzt. Die offene, valide Folgearbeit für `ASM-001` ist im nachfolgenden Befund beschrieben.

## Befund ASM-001

**Titel:** Unbedingte Referenzexpansion vor Assembly-Handlern trotz `includeReferences=false`.

- **Komponente:** `AssemblyAnalysisDispatcher.ExecuteAsync` und die Assembly-Registrierungen für `find_symbol`, `find_references`, `get_call_tree`, `inspect_assembly` und `find_assembly_extensions`.
- **Schweregrad:** S1.
- **Umfang:** U3 – gemeinsamer Dispatcher mit mehreren öffentlichen Assembly-Tools.
- **Beweissicherheit:** hoch; Kontrollfluss, Registrierungsdefaults, Dokumentation und sichtbare Lease-Diagnosen stimmen überein.
- **Erwartetes Verhalten:** Bei `includeReferences=false` bleibt die Analyse auf dem Root-Snapshot; bounded Referenzsessions werden nur bei expliziter Referenznavigation erzeugt.
- **Beobachtetes Verhalten:** `AssemblyAnalysisDispatcher.ExecuteAsync` ruft in `src/AiNetLinter/Mcp/AnalysisToolCall.cs:161-172` immer `lease.ExpandReferencesAsync(cancellationToken)` auf, bevor der konkrete Handler ausgeführt wird. Die sichtbaren Defaults in `src/AiNetLinter/Mcp/Registration/SymbolGraphToolRegistrations.cs:49`, `:80` und `:112` sind `false`. Auch Assembly-Inspection und Extension-Suche teilen den Dispatcher.
- **Auswirkung:** Standardabfragen öffnen Referenzsessions und übernehmen deren Fehler-/Partialdiagnosen, obwohl der Agent keine Referenzsuche angefordert hat. Das erhöht Kosten und kann die Vollständigkeitssemantik der Root-Antwort verschlechtern.
- **Konkrete Reproduktion:** Assembly-Tool mit `includeReferences=false` und einer nicht auflösbaren Referenz aufrufen; vor dem Handleraufruf sind Referenzexpansion und `ReferenceExpansionDiagnostics` bereits angelegt. Der bestehende positive `includeReferences=true`-Test deckt die explizite Variante ab, aber keinen Negativtest auf fehlende Expansion bei `false`.
- **Belege:** `AnalysisToolCall.cs:161-172`; `SymbolGraphToolRegistrations.cs:49,80,112`; `AssemblyAnalysisLease.ExpandReferencesAsync`; `Docs/agent-api.md:460`; `AssemblyAnalysisRouteTests.AssemblyRoute_IncludeReferencesNavigatesSymbolsReferencesAndCallTree()`.
- **Nicht umgesetzte Remediation-Hypothese:** Expansion-Capability bis zum Handler durchreichen und bei `false` keine Child-Leases eröffnen; Response-/Statusverträge müssen dabei erhalten bleiben.
- **Disposition:** `promoted-to-project-debt`; Audit-only, keine Änderung.

## Befund ASM-002

**Titel:** `find_symbol` gibt bei mehreren Namensmustern nur die Navigation des letzten Musters aus.

- **Komponente:** `AssemblyFindSymbolTool.BuildResponseAsync` und `AssemblySymbolSearch.FindMatchesAsync`.
- **Schweregrad:** S2.
- **Umfang:** U2 – Batch-Antwort von Assembly-`find_symbol`.
- **Beweissicherheit:** hoch.
- **Umgebungsabhängigkeit:** nein für den Kontrollfluss; ob zusätzlich andere Referenzdiagnosen die Antwort überdecken, ist umgebungsabhängig.
- **Erwartetes Verhalten:** Die strukturierte Batch-Antwort muss die Vollständigkeit aller angeforderten Muster erkennen lassen. Eine Begrenzung eines früheren Musters darf nicht aus der abschließenden Navigation verschwinden.
- **Beobachtetes Verhalten:** `AssemblySymbolSearch.FindMatchesAsync` fügt bei `distinct.Count > shown.Count` eine musterbezogene Begrenzungsdiagnose hinzu (`src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblySymbolSearch.cs:47-65`). `AssemblyFindSymbolTool.BuildResponseAsync` setzt dagegen in jeder Schleife `navigation = search.Navigation` und gibt nach der Schleife ausschließlich diese letzte Navigation zurück (`src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblyFindSymbolTool.cs:68-96`). Die per-Muster-Trefferlisten bleiben erhalten, der Top-Level-Status/Diagnosekontext des vorigen Musters nicht.
- **Auswirkung:** Ein Client kann bei `maxResults=1` nur einen Treffer des ersten Musters erhalten, während die abschließende Navigation keine entsprechende Begrenzungsdiagnose mehr ausweist. Die Batch-Antwort ist dann unvollständig, ohne dies auf Top-Level zuverlässig zu signalisieren.

### Konkrete Reproduktion

```text
find_symbol(targetType="assembly", targetPath=<absoluter DLL-Pfad>,
            namePatterns=["CancellationToken", "zz-neutral-absent-pattern"],
            maxResults=1, includeReferences=true)
```

Die erste Einzelmusterprobe mit `CancellationToken` fand sechs Kandidaten; mit `maxResults=1` wurde einer angezeigt. In der Mehrmusterprobe enthielt `results[0]` ebenfalls nur einen Treffer und `results[1]` keinen Treffer, während die gemeinsame `navigation` nur die abschließende Suche repräsentierte und keine Begrenzungsdiagnose für das erste Muster enthielt. Die Live-Session war wegen unabhängiger Referenzdiagnosen bereits `partial`; der Verlust der musterbezogenen Begrenzungsdiagnose ist zusätzlich durch den statischen Kontrollfluss bestätigt.

### Belege

- **MCP-Symbol:** `AssemblyFindSymbolTool.BuildResponseAsync` in `src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblyFindSymbolTool.cs:62-96`, insbesondere `navigation = search.Navigation` in `:82` und der abschließende `summary`-Aufbau in `:90-96`.
- **MCP-Symbol:** `AssemblySymbolSearch.FindMatchesAsync` in `src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblySymbolSearch.cs:18-67`, insbesondere die `maxResults`-Begrenzung in `:54-58` und die Übergabe der Diagnose an `CreateSummary` in `:60-65`.
- **Redigierte strukturierte Felder:** zwei `results`-Einträge mit `shown=1` und `shown=0`; abschließende `navigation.includeReferences=true`, `totalAssemblyCount=2`, `searchedAssemblyCount=2`, `assembliesTruncated=false`, `completeness=partial` sowie Diagnosen ohne die Begrenzungsdiagnose des ersten Musters.

### Nicht umgesetzte Remediation-Hypothese

Die Antwort könnte Navigation und Trunkierungsdiagnosen über alle Muster akkumulieren oder pro Muster eine eigene Vollständigkeit ausgeben. Keine Änderung wurde im Audit umgesetzt.

## Befund ASM-003

**Titel:** Namensauflösung projiziert erwartbare Nichttreffer anderer Assembly-Sessions als globale Partialdiagnose.

- **Komponente:** `AssemblySymbolResolver.ResolveAsync`, `AssemblyNavigationSupport.CreateSummary` und `FindReferencesTool.ResolveByNameAsync`.
- **Schweregrad:** S2.
- **Umfang:** U2 – bounded Navigation über Root- und Referenz-Sessions.
- **Beweissicherheit:** hoch für den Kontrollfluss; die konkrete Nutzerwirkung hängt von der Anzahl und Diagnosequalität der Referenz-Sessions ab.
- **Erwartetes Verhalten:** Ein Nichttreffer in einer Session, die das gesuchte Symbol erwartbar nicht deklariert, sollte nicht allein die Vollständigkeit einer erfolgreichen anderen Session auf `partial` setzen. Echte Lade-, Analyse- oder Trunkierungsdiagnosen müssen erhalten bleiben.
- **Beobachtetes Verhalten:** `AssemblySymbolResolver.ResolveAsync` sammelt für jede nicht identitätsqualifizierte Session das `SymbolNotFound`-Ergebnis als Diagnose. `AssemblyNavigationSupport.CreateSummary` projiziert jede Diagnose direkt auf `completeness=partial`, auch wenn eine andere Session einen eindeutigen Kandidaten liefert.
- **Auswirkung:** Agenten können eine erfolgreiche Symbolauflösung als unvollständig interpretieren und erwartbare Nichtzuständigkeit mit einer tatsächlichen Referenz-/Analyse-Lücke verwechseln.
- **Konkrete Reproduktion:** Mit `includeReferences=true` nach einem Namen suchen, der ausschließlich in der Root-Assembly deklariert ist; die Referenz-Sessions liefern `SymbolNotFound`, während der Root einen Kandidaten liefert. Die gemeinsame Navigation enthält anschließend die Nichttrefferdiagnosen und `partial`.
- **Belege:** `src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblySymbolResolver.cs:30-61`; `src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblyNavigationSupport.cs:41-55`; `src/AiNetLinter/Mcp/Tools/SymbolGraph/FindReferencesTool.cs:191-208`.
- **Nicht umgesetzte Remediation-Hypothese:** Erwartbare Session-Nichttreffer aus der globalen Diagnosemenge herausfiltern oder separat als Suchabdeckung ausweisen; echte Expansion-/Sessiondiagnosen unverändert weiterreichen.
- **Disposition:** `promoted-to-project-debt`; Audit-only, keine Änderung.

## Bestätigte Detailprüfungen

### Routing und Pfadvalidierung

- `AnalysisTargetResolver.Resolve` in `src/AiNetLinter/Mcp/AnalysisTargetResolver.cs:10-58` verlangt `targetType`, akzeptiert nur `project` oder `assembly`, kanonisiert `targetPath`, prüft bei Assembly-Zielen Existenz und `.dll`-Endung.
- `AssemblyAnalysisService.TryValidatePath` in `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisService.cs:21-59` wiederholt die lokale DLL-Validierung für die Assembly-Tool-Preparation.
- `AssemblyAnalysisDispatcher.ExecuteAsync` in `src/AiNetLinter/Mcp/AnalysisToolCall.cs:145-195` trennt Project- und Assembly-Route und erzeugt für Assembly-Fehler den kanonisierten Zielkontext.

### Metadata-only und statische Decompilation

- `AssemblyDecompilationAdapter.ReadTopLevelTypes` in `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyDecompilationAdapter.cs:304-326` liest aus `File.OpenRead`/PE-Metadaten und überspringt verschachtelte bzw. compiler-generierte Top-Level-Knoten.
- `AssemblyDecompilationAdapter.CreateDecompiler` in `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyDecompilationAdapter.cs:280-302` konfiguriert den statischen Adapter; eine ergänzende MCP-Textsuche nach `Assembly.Load` im Assembly-Analysebereich hatte 0 Treffer.
- `AssemblyAnalysisSession` begrenzt Bytes, Typen, Member, Dokumentzeichen und Laufzeit; `AssemblyAnalysisSessionTests` decken Cache, Generation, Cancellation, Größenlimit, alte Snapshots und Partial-Status ab.
- Live-`inspect_assembly` mit `targetType=assembly`, absolutem DLL-Pfad, `publicOnly=false`, begrenzten Typ-/Memberlimits lieferte `isError=false`, `origin=decompiled`, `sourcePath=none`, `snapshot=none`, `status=partial`, `completeness=partial`, strukturierte Typen und sichtbare Diagnosen. Das wurde als erwartete Partial-Semantik bewertet.

### Source-Mapping und Herkunft

- `AssemblyAnalysisContextFactory.IsSourceSelectionUsable` in `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextFactory.cs:216-233` verlangt Attestation, verifizierte Provider-Gesundheit, sauberes Checkout, matched candidate und identische Snapshot-Identität.
- `TryCreateSourceBackedContextAsync` in derselben Datei `:135-196` erzeugt nur bei erfüllter Auswahl einen source-backed Context mit tatsächlicher Ziel-Fingerprint- und Snapshot-Herkunft.
- `AssemblyAnalysisToolSupportTests.ExecuteAsync_WithConfiguredMappingPassesMatchedSelectionToFactory()` und `ExecuteAsync_WithoutMappingSkipsProviderAndUsesDecompilationFallback()` decken Mapping und Fallback ab; `AssemblyAnalysisToolSupportDegradedTests.ExecuteAsync_DegradedProviderShowsLastGoodAndUsesDecompilationFallback()` deckt den degradierenden Providerpfad ab.
- Die nachträgliche Live-Probe gegen eine konfigurierte gemappte DLL führte
  über den MCP-Server zu einem Gitea-Checkout mit der konfigurierten Solution
  und Source-Dateien. Die anschließenden `inspect_assembly`- und
  `find_assembly_extensions`-Antworten blieben jedoch `origin=decompiled`,
  `sourcePath=none` und `snapshot=none`, jeweils mit `status=partial` und
  `completeness=partial`. Der Cache-Download ist damit belegt, die
  Source-backed-Bereitstellung aber nicht.

## Verifikation

- **Fast-Test-Slice:**

  ```powershell
  dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~AssemblyAnalysis" --no-restore
  ```

  Ergebnis: 88 erfolgreich, 0 fehlgeschlagen.

- **Gezielter Integrationstestlauf:**

  ```powershell
  dotnet test src/AiNetLinter.IntegrationTests --filter "FullyQualifiedName~McpServerAssemblyHealthE2ETests|FullyQualifiedName~McpServerCommandContractTests|FullyQualifiedName~McpServerCommandFindReferencesTests|FullyQualifiedName~McpServerToolBehaviorE2ETests" --no-restore
  ```

  Initiales Ergebnis unter konkurrierender Testlast: 40 gesamt, 6 erfolgreich, 34 fehlgeschlagen. Alle protokollierten Fehler waren `System.IO.IOException: MCP server process exited unexpectedly` im gemeinsamen Testhost-/Stdio-Transport; deshalb keine Zuordnung zu ASM-001 oder ASM-002.

- **Vollständiger Integrations-Nicht-Stress-Lauf:**

  ```powershell
  dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress --no-restore
  ```

  Ergebnis zum unabhängigen Reviewerzeitpunkt: 377 erfolgreich, 0 fehlgeschlagen, 0 übersprungen. Ein späterer Orchestrator-Abschlusslauf unter anderer Prozesslast endete mit 307 Erfolgen und 70 MCP-/Daemon-Prozessfehlern; die Abweichung ist in `reports/08-tests-documentation.md` als Umgebungsgrenze dokumentiert.

- **Build:** `dotnet build` wurde nach Ende der konkurrierenden Testprozesse vollständig erfolgreich ausgeführt: 0 Warnungen, 0 Fehler.

## Coverage-/Limitations-Tabelle

| Prüfaspekt | MCP-/Testbeleg | Ergebnis | Grenze |
|---|---|---|---|
| `targetType`/absoluter DLL-Pfad | `AnalysisTargetResolver.Resolve`; `InspectAssembly_RejectsRelativeAndMissingPathsWithoutRuntimeLoading()` | bestätigt | kein negativer Live-Aufruf mit absichtlich ungültigem Pfad notwendig |
| Metadata-only und statische Decompilation | `AssemblyDecompilationAdapter.*`; Session-/Tool-Tests; Live-`inspect_assembly` | bestätigt, Live-Status `partial` | Referenzdiagnosen verhindern vollständige Live-Compilation |
| Source-backed Mapping/Fallback | `AssemblyAnalysisToolSupportTests.*`; `AssemblyAnalysisContextFactory.*`; Live-MCP-Probe | Download/Checkout bestätigt, Source-backed-Antwort nicht erreicht | Solution-Materialisierung bzw. Source-Auswahl bleibt offen; MCP fällt sicher auf Decompilation zurück |
| Unbedingte Referenzexpansion bei Default-Symbolnavigation | `AssemblyAnalysisDispatcher.ExecuteAsync`; Registrierungsdefaults und Lease-Expansion | ASM-001 bestätigt | Live-DLL liefert zusätzlich umgebungsabhängige Partialdiagnosen |
| Referenz-/Call-Tree-Navigation mit `includeReferences=true` | `AssemblyAnalysisRouteTests.AssemblyRoute_IncludeReferencesNavigatesSymbolsReferencesAndCallTree()` | positiver Pfad bestätigt | nur bounded/partial Referenzumgebungen geprüft |
| Mehrmuster-Vollständigkeit | `AssemblyFindSymbolTool.BuildResponseAsync`; Live-Batch mit erstem begrenztem Muster | ASM-002 bestätigt | keine vollständige Umgebung ohne Basisdiagnosen verfügbar |
| Nichttreffer anderer Sessions | `AssemblySymbolResolver.ResolveAsync`; `AssemblyNavigationSupport.CreateSummary` | ASM-003 bestätigt | konkrete Ausprägung hängt von den geladenen Referenzen ab |
| MCP-Integration/E2E | gezielter Lauf zunächst 34/40 Prozessabbrüche; anschließender vollständiger Nicht-Stress-Lauf 377/377 grün | bestätigt, initiale Prozessabbrüche als Umgebungsgrenze | kein Stress-Lauf |
| Stress-Kategorie | bewusst nicht ausgeführt | außerhalb Scope | keine Lastaussage |

## Cross-Lens-Überschneidungen

- **Source-Mapping/Checkout:** Die Source-backed Auswahlprüfung und Snapshot-Vertrauenslogik gehört primär zu einer Source-/Checkout-Linse; hier wurde nur ihre Auswirkung auf Assembly-Herkunft und Fallback geprüft.
- **Git/Transport:** Referenzauflösung kann Pfad-/Transportdiagnosen weiterreichen; ihre Transportursache wurde nicht bewertet.
- **Performance/Resource-Budgets:** Decompilation und Referenzexpansion besitzen feste Caps; hier wurde nur die sichtbare Partial-Semantik, nicht die Laufzeitoptimierung bewertet.
- **Test-/Wiring-Linse:** Die fehlenden Defaultpfad-Regressionen sind eine Testabdeckungslücke, der bestätigte ASM-001-Befund liegt aber im Produktions-Dispatch und nicht nur im Testaufbau.

### Commit-Vorschlag

Kein Commit erstellt (read-only Audit). Bericht ausschließlich in `tasks/decompiled-assembly-analysis-audit/reports/01-assembly.md`.
