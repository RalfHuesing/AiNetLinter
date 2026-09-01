# Epic 3 — Referenzen, Source-Auswahl und Diagnosen

## Findings

### Bugs

#### E3-BUG-01 — Starke Assembly-Identität wird bei Referenzkandidaten nicht vollständig geprüft

- Priorität: P1
- Größe: M
- Vertrauen: hoch; direkt aus dem aktuellen Resolver-Code abgeleitet, nicht durch einen positiven GIT-01-Source-Run reproduziert.
- Aktuelle Belege: `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisModels.cs:63-78` (`AssemblyIdentityDto`, `AssemblyReferenceDto`), `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyReferenceResolver.cs:319-351` (`ReadMetadata`, `ReadIdentity`, `IdentityMatches`).
- MCP-Parameter/Evidence: `get_feature_context({targetType:"project", targetPath:"<absoluter Repo-Root>", symbolIdentifier:"AssemblyReferenceResolver", includeCallers:true, includeTests:true, includeViolations:true, maxCallers:30, maxTests:30})`; die Antwort war für die relevante Symboldeklaration nicht gekürzt und meldete keine Regelverletzung. Ergänzend `inspect_assembly({targetType:"assembly", targetPath:"<absoluter Matrixpfad von LOCAL-01>", includeReferences:true, maxMembers:50, maxResults:20, publicOnly:true})`: `isError=false`, aber `origin=decompiled`, `confidence=medium`, `trust=untrusted`, `completeness=partial`, Referenzen und Sessions wegen MCP-Budgets gekürzt. Das ist Korrelations-Evidence, kein direkter Token-Mismatch-Nachweis.
- Befund: `AssemblyReferenceDto` transportiert Name, Version und Kultur, aber keinen erwarteten Public-Key-Token. `ReadMetadata` verwirft dieses Identitätsmerkmal für Assembly-Referenzen; `IdentityMatches` vergleicht anschließend nur Name, Version und Kultur, obwohl `AssemblyIdentityDto` den Token für die Assembly-Definition berechnet.
- Auswirkung: Ein Kandidat mit gleicher sichtbarer Kurzidentität, aber anderer starker Identität kann als `resolved` in Graph, Roslyn-Referenzen und transitive Sessions gelangen. Dadurch können Symbolauflösung, Source-Selection und Diagnoseprojektion auf der falschen Dependency basieren.
- Empfehlung: Erwarteten Public-Key-Token inklusive der erforderlichen Reference-Flags aus den Metadaten übernehmen, bei der Kandidatenprüfung vergleichen und Token-Mismatch als eigene redigierte Diagnose neben `missing`/`version_mismatch` projizieren. Die Bounded-Grenzen und die vorhandene Redaction beibehalten.
- Abgrenzung: Es wurden keine externen Assembly-Identitäten materialisiert oder in den Bericht übernommen. Die vorhandenen GIT-01-/LOCAL-Antworten zeigen nur bereits redigierte allgemeine `missing`-/`version_mismatch`-Zustände.
- Offene Unsicherheit: Nicht ausgeführte Testfälle decken die Kombination aus gleichem Namen/Version/Kultur und abweichendem Public-Key-Token noch nicht ab.

#### E3-BUG-02 — Referenzknoten-Limit hinterlässt inkonsistente oder stille Zustände

- Priorität: P1
- Größe: M
- Vertrauen: hoch; Kontrollflussfehler direkt aus dem Code ableitbar.
- Aktuelle Belege: `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyReferenceResolver.cs:76-129` (`VisitNode`, `VisitChild`) und `:172-181` (`NormalizeReference`); `src/AiNetLinter/Mcp/Assemblies/Analysis/References/AssemblyReferenceSessionExpander.cs:33-79` behandelt `node_limit` erst korrekt, wenn dieser Zustand bereits gesetzt wurde.
- MCP-Parameter/Evidence: `inspect_assembly({targetType:"assembly", targetPath:"<absoluter Matrixpfad von LOCAL-03>", includeReferences:true, maxMembers:50, maxResults:20, publicOnly:true})`; tatsächlich `completeness=partial`, Root-Diagnosen `200` gesamt, Referenzsumme `137`, Session-Summe `1519`, jeweils budgetbedingt gekürzt. Das bestätigt die Bounded-Situation, nicht allein den spezifischen Kontrollflussfehler. Gelesene Abdeckung: `src/AiNetLinter/FastTests/Mcp/Assemblies/AssemblyAnalysisDispatcherCapabilityTests.cs:100-127` prüft den Expander-Knotenlimitpfad, nicht die Resolver-Normalisierung.
- Befund: `VisitNode` beendet sich bei `Visited.Count >= MaxReferenceNodes` in Zeile 82 ohne Diagnose. Erreicht `VisitChild` die Grenze in Zeile 107, wird zwar eine Diagnose geschrieben, der zuvor eingefügte Kandidat aber nicht auf `ResolutionState="node_limit"` und `Resolved=false` ersetzt. `NormalizeReference` kann anschließend nur das Bool/den Pfad korrigieren und lässt Status und Diagnose widersprüchlich.
- Auswirkung: Verbraucher können einen nicht nutzbaren Verweis mit Zustand `resolved` sehen oder einen Grenzabbruch ohne sichtbares Boundary-Signal erhalten. `AssemblyReferenceSessionExpander.TryAddUnresolved` kann daraus eine Session mit falschem Status projizieren; `partial` hängt dann zufällig an einem anderen Aggregatdiagnosepfad.
- Empfehlung: Den Kandidaten beim Eintritt in die Grenze atomar als `node_limit` mit Diagnose markieren; auch den frühen `VisitNode`-Return mit einem einmaligen Boundary-Signal versehen. Danach `NormalizeReference` nur noch für den Ladeerfolg verwenden. Die Zustandswerte, Root-/Transitivdiagnosen und Sessionprojektion mit einem >128-Knoten-Fall konsistent abgleichen.
- Abgrenzung: Die feste Begrenzung selbst ist kein Befund; sie ist als Sicherheitsinvariante gewollt. Befund ist ausschließlich die uneinheitliche Zustands-/Diagnoseprojektion an der Grenze.
- Offene Unsicherheit: Wegen der Antwortbudgets wurde im MCP keine vollständige Liste bis genau zum 128./129. Knoten ausgeleitet; die Inkonsistenz ist aus dem Quellkontrollfluss belegt.

### Optimierungen

#### E3-OPT-01 — `AssemblyReferenceSessionExpander` überschreitet den AIContext-Footprint

- Priorität: P2
- Größe: M
- Vertrauen: hoch; tatsächlicher MCP-Regelbefund.
- Aktuelle Belege: `src/AiNetLinter/Mcp/Assemblies/Analysis/References/AssemblyReferenceSessionExpander.cs:13-164`.
- MCP-Parameter/Evidence: `get_violations({targetType:"project", targetPath:"<absoluter Repo-Root>", scopeFilter:"src/AiNetLinter/Mcp/Assemblies/Analysis", maxResults:200, contextLines:2, includeSnippet:false})`; Antwort vollständig, zwei Verstöße im Scope, davon der Epic-3-relevante Befund an Zeile 13: `AIContextFootprint`, `2513 > 2500`. Der zweite Scope-Treffer betrifft einen anderen Epic und wurde nicht bewertet.
- Befund: Die Klasse verbindet Root-Lease, bounded Traversierung, Child-Leases, Provider-/Source-Origin und Diagnoseaggregation in einem Kontext mit messbarem Footprint-Überhang.
- Auswirkung: Mehr Kontextdrift und höhere Wartungs-/Review-Kosten genau an der Stelle, die Referenzstatus, Session-Origin und Partial-Diagnosen zusammenführt. Semantische Falschheit wurde durch diesen Befund nicht nachgewiesen.
- Empfehlung: Eine schmale Traversierungs-/Lease-Fassade oder getrennte Projektion für Sessionstatus und Diagnosen einziehen; die bestehenden Grenzen, Cancellation- und Dispose-Verträge unverändert lassen. Danach denselben MCP-Verstoßcheck erneut ausführen.
- Abgrenzung: Keine Produktionsänderung vorgenommen; dies ist ein struktureller Optimierungsbefund, kein Anlass für eine ungeprüfte Refaktorierung im Audit.
- Offene Unsicherheit: Ohne Build/Test darf nicht behauptet werden, dass eine konkrete Aufteilung compile- und verhaltensneutral wäre.

#### E3-OPT-02 — Kandidaten werden bei der bounded Metadatenauflösung mehrfach gelesen

- Priorität: P2
- Größe: M
- Vertrauen: mittel-hoch; direkter Codebeleg, Laufzeitkosten nur indirekt durch die redigierten MCP-Summen gestützt.
- Aktuelle Belege: `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyReferenceResolver.cs:183-218` (`FindReferencePath`) liest jeden Kandidaten über `TryReadIdentity`; `:100-128` (`VisitChild`) liest den gewählten Kandidaten erneut über `TryReadMetadata`; `:240-266` (`EnumerateCandidatePaths`) enumeriert pro Referenz erneut das Assembly-Verzeichnis.
- MCP-Parameter/Evidence: `inspect_assembly({targetType:"assembly", targetPath:"<absoluter Matrixpfad von GIT-01>", includeReferences:true, maxMembers:5, maxResults:5, publicOnly:true})`; tatsächlich `334` Referenzen und `9318` Sessions, `completeness=partial`, Rootdiagnosen `121` gesamt mit gekürzter Projektion. LOCAL-01/02/03 zeigten ebenfalls große Session-/Diagnosesummen und Truncation. Diese Antworten belegen den Bedarf an Boundedness, nicht einen isolierten Performance-Benchmark.
- Befund: Kandidatenpfad, Identität und vollständige Metadaten haben keinen auf eine einzelne `Resolve`-Session begrenzten Cache. Bei vielen Referenzen wiederholen sich Verzeichnisenumeration, PE-Öffnung und Metadatenlesen.
- Auswirkung: Höhere I/O- und CPU-Kosten, längere Provider-/Consumer-Pfade und erhöhte Wahrscheinlichkeit, dass der Antwortbudgetpfad vor fachlich relevanten Diagnosen gekürzt wird.
- Empfehlung: Pro Resolver-Session einen bounded Cache für kanonischen Pfad → Identität/Metadaten/Ladefehler und eine stabile Kandidatenindexierung einführen. Cachegröße, Pfadanzahl und Diagnoselimits an die vorhandenen `MaxReferenceDepth`-/`MaxReferenceNodes`-Grenzen binden; keine globalen oder unbounded Caches einführen.
- Abgrenzung: Kein Beleg, dass die beobachteten Truncations ausschließlich durch diese Wiederholungen entstehen; Providerverfügbarkeit, Dependency-Mismatch und Response-Budget bleiben getrennte Ursachen.
- Offene Unsicherheit: Ohne Performance-Lauf und ohne Builds/Tests ist keine quantitative Verbesserung zusicherbar.

### Missing Features

#### E3-MISSING-01 — Der registrierte Assembly-Dispatch kann keinen Consumer-Kontext an die Extension-Prüfung binden

- Priorität: P1
- Größe: L
- Vertrauen: hoch; aktueller Route-Code plus tatsächliche MCP-Projektion.
- Aktuelle Belege: `src/AiNetLinter/Mcp/Registration/AssemblyAnalysisToolRegistrations.cs:100-130` registriert `find_assembly_extensions` über den Assembly-Lease-Dispatch und beschreibt selbst, dass dort kein Consumer-Projekt verwendet wird. `src/AiNetLinter/Mcp/Assemblies/Analysis/Factories/AssemblyAnalysisRegistryEntryFactory.cs:149-162` erzeugt den Registry-Kontext mit `ConsumerSolution:null` und `ReceiverType:null`; `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisService.cs:133-163` setzt ohne Receiver `not_decidable`. Die source-aware Overloads in `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/Dispatch/FindAssemblyExtensionsToolDispatch.cs:22-29` und `InspectAssemblyToolDispatch.cs:22-29` existieren, werden aber vom registrierten Assembly-Route-Aufbau nicht mit einem Consumer-Ziel gespeist.
- MCP-Parameter/Evidence: `find_assembly_extensions({targetType:"assembly", targetPath:"<absoluter Matrixpfad von LOCAL-03>", maxResults:5})` sowie derselbe redigierte Call für GIT-01; `isError=false`, `consumerProject=null`, die gezeigten Extension-Einträge `not_decidable`, Origin jeweils `decompiled`, Trust `untrusted`, Completeness `partial`. Ein Diagnose-/Providerfehler wurde dabei nicht als Erfolg interpretiert.
- Befund: `receiverType` allein liefert keinen Consumer-Solution-Kontext. Der öffentliche Assembly-Pfad kann daher Roslyn-Applicability nicht gegen ein tatsächliches Consumer-Projekt reduzieren, obwohl die Factory und source-aware Dispatch-Overloads die notwendige Semantik prinzipiell besitzen.
- Auswirkung: Anwender erhalten für Extensions keine belastbare Trennung zwischen `applicable` und `not_applicable`; fehlende Dependencies und fehlender Consumer-Kontext vermischen sich in `not_decidable`.
- Empfehlung: Einen optionalen, absolut adressierten Consumer-Projekt-/Solution-Kontext in den Assembly-Dispatch aufnehmen oder den bestehenden source-aware Dispatch verbindlich routebar machen. Consumer-Compilation, Source-Project-References, Trust/Origin und Partial-Diagnosen müssen weiterhin bounded und redigiert projiziert werden.
- Abgrenzung: Die deklarative Semantik `not_decidable` ist korrekt, wenn wirklich kein Consumer geliefert wurde. Missing Feature ist die fehlende öffentliche Möglichkeit, diesen Kontext im selben Assembly-Aufruf bereitzustellen.
- Offene Unsicherheit: Die alternate Dispatch-Overloads wurden statisch gelesen, aber nicht durch einen ausgeführten MCP-Aufruf mit Consumer-Solution aktiviert.

#### E3-MISSING-02 — Source-Match hat keine Binary-zu-Source-Identitätsattestierung

- Priorität: P1
- Größe: L
- Vertrauen: hoch für den statischen Befund; keine positive Source-backed-Laufzeitbeobachtung im GIT-01-Fall.
- Aktuelle Belege: `src/AiNetLinter/Mcp/Assemblies/Analysis/SourceSelection/AssemblySourceMatchResolver.cs:82-106` prüft Snapshot-/Mapping-Identität und Alias; `:109-176` matcht anschließend auf eindeutigen Project-/Assembly-Namen. `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextFactory.cs:280-309` setzt für den source-backed Kontext die Source-Compilation als Roslyn-Assembly, verwendet aber die Identität des Zielartefakts als `AssemblyContext.Identity`.
- MCP-Parameter/Evidence: `get_feature_context({targetType:"project", targetPath:"<absoluter Repo-Root>", symbolIdentifier:"AssemblySourceMatchResolver", includeCallers:true, includeTests:true, includeViolations:true, maxCallers:30, maxTests:30})` und derselbe aktuelle Schema-Call für `AssemblyAnalysisContextFactory`; beide relevanten Antworten waren nicht gekürzt und meldeten keine Regelverletzung. GIT-01 `inspect_assembly` meldete tatsächlich `provider-unavailable`, `origin=decompiled`, `trust=untrusted`, ohne Source-Snapshot; daher ist GIT-01 kein source-backed Erfolg.
- Befund: Nach Repository-/Solution-Identität und Alias wird nicht geprüft, ob das gematchte Source-Projekt zur konkreten Binärversion, starken Identität oder einem attestierten Build-/Output-Fingerprint des Zielartefakts gehört.
- Auswirkung: Ein formal eindeutiger Source-Match kann bei gleichnamigen, aber inkompatiblen Binaries mit `confidence=high` und `trust=verified-clean` als source-backed Kontext erscheinen. Dadurch wäre die Herkunft technisch vertrauenswürdig, die semantische Zuordnung aber nicht hinreichend belegt.
- Empfehlung: Source-Mappings um eine redigiert vergleichbare Binary-/Output-Identität oder eine attestierte Build-Zuordnung erweitern; mindestens Version, Culture und Public-Key-Token berücksichtigen. Bei fehlender Übereinstimmung auf decompiled/fallback mit sichtbarer Diagnose und niedrigerer Confidence wechseln.
- Abgrenzung: Checkout-Trust und Provider-Attestierung selbst sind nicht der Befund; `GiteaExternalSourceProvider` und `ExternalSourceProviderResult` erzwingen bereits Snapshot, Checkout-Zugehörigkeit und Attestation, bevor eine Source-Selection nutzbar wird.
- Offene Unsicherheit: Das konkrete Mappingformat und die verfügbare Build-Identität der externen Source-Snapshots wurden nur über die redigierte lokale Matrix-/Providerkonfiguration aufgelöst, nicht in den Bericht übernommen.

#### E3-MISSING-03 — Bounded Referenzauflösung kennt keine konfigurierten Source-/Dependency-Probe-Wurzeln

- Priorität: P2
- Größe: L
- Vertrauen: hoch; aktueller Code begrenzt die Kandidatensuche sichtbar, Laufzeitwirkung durch Partial-Diagnosen belegt.
- Aktuelle Belege: `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyReferenceResolver.cs:183-218` und `:240-266` suchen im Verzeichnis des Zielartefakts sowie in Trusted-Platform-Assemblies. `src/AiNetLinter/Mcp/Assemblies/Analysis/References/SourceProjectReferenceGraph.cs:42-105` ergänzt Source-Project-Referenzen, bietet aber keine allgemeine Suche in Snapshot-/Projekt-Output-Verzeichnissen.
- MCP-Parameter/Evidence: `inspect_assembly({targetType:"assembly", targetPath:"<absoluter Matrixpfad von LOCAL-01>", includeReferences:true, maxMembers:50, maxResults:20, publicOnly:true})` → `195` Root-/Transitivdiagnosen gesamt, `1` Sample, `completeness=partial`, Referenzsumme `203`; LOCAL-02 → `195` Diagnosen, `159` Referenzen; LOCAL-03 → `200` Diagnosen, `137` Referenzen, erster gezeigter Referenzzustand `missing`. GIT-01 → `121` Diagnosen, `334` Referenzen und ein gezeigter `version_mismatch`; alle Antworten waren budgetbedingt partiell/gekürzt. Die Fehlersignale sind echte Diagnosen, keine Erfolgsmarker.
- Befund: Abhängigkeiten außerhalb des Zielverzeichnisses und der TPA-Liste können trotz vorhandener Source-Snapshot-/Projektinformation nicht als Kandidaten berücksichtigt werden. Source-Project-Referenzen decken nur den Projektgraphen ab, nicht beliebige binäre Dependencies.
- Auswirkung: Inkompatible oder fehlende Dependencies führen früh zu unvollständigem Roslyn-Kontext, `not_decidable`-Applicability und gekürzten Root-/Transitivdiagnosen, obwohl ein bounded zusätzlicher Suchraum die Auflösung verbessern könnte.
- Empfehlung: Explizite, vertrauensgebundene Probe-Wurzeln aus Source-Snapshot-/Projekt-Outputs und optionaler Request-Konfiguration zulassen; Pfade kanonisieren, nur metadata-only lesen, Anzahl/Tiefe/Bytes begrenzen und Herkunft pro Reference beibehalten. Kein globaler Dateisystemscan.
- Abgrenzung: Nicht jeder aktuelle `missing`-/`version_mismatch` ist dadurch erklärbar; Providerfehler, echte Inkompatibilität und Budgettruncation bleiben separat sichtbar.
- Offene Unsicherheit: Die positiven Matrixfälle zeigen keinen vollständigen Source-Snapshot, daher konnte keine Aussage über die tatsächlich verfügbare externe Probe-Struktur getroffen werden.

## Evidence und Scope

### Ausgeführte redigierte MCP-Abfragen

Alle Assembly-Abfragen nutzten das aktuelle Schema mit `targetType:"assembly"` und einem absoluten `targetPath`; konkrete Matrixpfade werden hier absichtlich als `<absoluter Matrixpfad von LABEL>` redigiert. Die lokale Matrix wurde ausschließlich zur Label-/Pfadauflösung gelesen.

| Falllabel | Tatsächlich ausgeführter Check | Tatsächliches redigiertes Ergebnis |
|---|---|---|
| GIT-01 | `inspect_assembly({targetType:"assembly", targetPath:"<absoluter Matrixpfad von GIT-01>", includeReferences:true, maxMembers:5, maxResults:5, publicOnly:true})` | `isError=false`, aber kein nutzbarer Source-Snapshot/kein Source-Projekt; `origin=decompiled`, `confidence=medium`, `trust=untrusted`, `contentMode=decompiledSignatureOnly`, `fallbackReason=provider-unavailable`, `status/completeness=partial`; Source-Diagnosen `2/2` gezeigt; Root-Diagnosen `121` gesamt, Referenzen `334` gesamt, Sessions `9318` gesamt, jeweils mit Truncation. Der erste gezeigte Referenzzustand war `version_mismatch` und nicht aufgelöst. |
| LOCAL-01 | `inspect_assembly({targetType:"assembly", targetPath:"<absoluter Matrixpfad von LOCAL-01>", includeReferences:true, maxMembers:50, maxResults:20, publicOnly:true})` | `isError=false`; `origin=decompiled`, `confidence=medium`, `trust=untrusted`, `contentMode=decompiledSignatureOnly`, `fallbackReason=mapping-not-found`, `status/completeness=partial`; Source-Diagnosen `1/1`; Root-/Transitivdiagnosen `195` gesamt, Referenzen `203` gesamt, Sessions `4039` gesamt, budgetbedingt gekürzt. |
| LOCAL-02 | Derselbe aktuelle `inspect_assembly`-Call mit `<absoluter Matrixpfad von LOCAL-02>` | `isError=false`; dieselbe decompiled-/untrusted-/partial-Projektion, `fallbackReason=mapping-not-found`; Source-Diagnosen `1/1`, Root-/Transitivdiagnosen `195` gesamt, Referenzen `159` gesamt, Sessions `1482` gesamt, budgetbedingt gekürzt. |
| LOCAL-03 | Derselbe aktuelle `inspect_assembly`-Call mit `<absoluter Matrixpfad von LOCAL-03>` | `isError=false`; decompiled, medium, untrusted, `fallbackReason=mapping-not-found`, partial; Source-Diagnosen `1/1`, Root-/Transitivdiagnosen `200` gesamt, Referenzen `137` gesamt, Sessions `1519` gesamt, budgetbedingt gekürzt. Der erste gezeigte Referenzzustand war `missing`, `resolved=false`, mit Diagnose. |
| GIT-01, LOCAL-01, LOCAL-02, LOCAL-03 | `find_assembly_extensions({targetType:"assembly", targetPath:"<absoluter Matrixpfad von LABEL>", maxResults:5})` | Die Provider-/Origin-Projektion blieb decompiled/partial. Bei GIT-01 wurden Extensions gefunden, aber die gezeigten Applicability-Einträge waren `not_decidable` und `consumerProject=null`; LOCAL-03 zeigte ebenfalls `consumerProject=null` und `not_decidable`. Das ist eine fachliche Negativ-/Unbestimmtheitsaussage, kein Providererfolg. |
| FALSE-01 | `inspect_assembly({targetType:"assembly", targetPath:"<absoluter Matrixpfad von FALSE-01>", includeReferences:false, maxMembers:1, maxResults:1, publicOnly:true})` | `isError=false`, strukturiertes `WORKSPACE_DIAGNOSTIC`, `recoverable=true`, keine Origin-/Snapshotdaten. Das Ziel wurde nicht ausgeführt und erzeugte keinen Assembly-Snapshot. |

Für GIT-01 wurde ausschließlich der bestehende External-Source-Provider über den laufenden MCP angesprochen. Ein nachgelagerter Versuch gegen den aus der Matrix gelesenen Konfigurationspfad wurde vom Assembly-Schema als `INVALID_ARGUMENT` abgewiesen und nicht als Origin-/Provider-Nachweis gewertet. Der danach mit dem korrekten, absolut aufgelösten GIT-01-Assemblypfad ausgeführte `inspect_assembly`-Call (`includeReferences=true`, `maxMembers=1`, `maxResults=1`, `publicOnly=true`) antwortete terminal mit `isError=false`, aber erneut `provider-unavailable`, `origin=decompiled`, ohne Snapshot/Source-Projekt, `trust=untrusted` und `completeness=partial`; auch das ist kein Materialisierungs-, Checkout- oder Source-backed-Nachweis. Es wurden keine manuellen Git-Kommandos und kein eigener Checkout ausgeführt.

### Gelesene Nachweise (nicht als ausgeführte Abfragen ausgeben)

- Vollständig gelesen: `AGENTS.md`, die relevanten `.agents/rules/*.mdc`, `tasks/decompiled-assembly-audit/Konzept.md`, `roadmap.md`, die bestehende `code-map.md` und `.agents/skills/implement/SKILL.md`.
- Source-/Providerpfad gelesen: `AssemblyReferenceResolver`, `SourceProjectReferenceGraph`, `AssemblyReferenceSessionExpander`, `AssemblySourceSelectionOrchestrator`, `AssemblySourceMatchResolver`, `AssemblySourceProviderCoordinator`, `GiteaExternalSourceProvider`, `ExternalSourceProviderResult`, `AssemblyAnalysisRegistryEntryFactory`, `AssemblyAnalysisContextFactory`, `AssemblyAnalysisSourceToolSupport`, `AssemblyAnalysisService`, Response-Limits und die Assembly-Registrierung.
- Test-/Abdeckungsnachweise nur read-only gelesen: `AssemblyAnalysisDispatcherCapabilityTests`, `AssemblyAnalysisSessionTests`, `AssemblyAnalysisToolTests`, `AssemblySourceMatchResolverTests`, `AssemblyAnalysisContextFactoryTests`, `AssemblyAnalysisRouteTests` sowie die relevanten External-Source-Provider-/Snapshot-Tests. Keine dieser Tests wurde ausgeführt.
- Projektweite MCP-Lesebelege: `get_index_scope`, Assembly-Unterbaum über `get_file_tree`, `get_server_health`, `find_symbol`, `get_feature_context`, `get_symbol_body` und `get_violations`. Der Assembly-Unterbaum wurde vollständig innerhalb der gewählten Baumgrenze geliefert; der relevante `get_violations`-Scope war nicht gekürzt.

### Scope, Grenzen und Sicherheitsvertrag

- Im Scope: Referenzidentität/-auflösung, lokale und Source-Project-Referenzen, bounded Reference-Sessions, External-Source-Provider/Checkout-/Snapshot-Gates, Source-Match und Source-Context, Consumer-/Applicability-Pfad, fehlende oder inkompatible Dependencies sowie Origin/Trust/Confidence/Completeness/Partial und Diagnoseprojektion.
- Nicht im Scope: Decompilation-/Body-/Cache-Details aus Epic 2 außer dort, wo sie als Origin-/Fallback-Vertrag einfließen; der zweite `get_violations`-Treffer im Assembly-Analysebereich; Tests, Builds, Produktionscode, Konfiguration, Produktdokumentation und Commits.
- Alle Ausgaben sind auf die fünf erlaubten Falllabels und redigierte Status-/Budgetsignale beschränkt. Keine externen Assembly-Namen, Namespaces, Pfade, URLs, Hashes oder dekompilierten Inhalte werden wiederholt.
- `isError=false` wurde nur als Transportstatus gelesen. Provider-, Workspace- und `WORKSPACE_DIAGNOSTIC`-Ergebnisse wurden fachlich als Fehler/negative Diagnose behandelt, nicht als Analyseerfolg.

## Handoff

- Geändert wurden ausschließlich `tasks/decompiled-assembly-audit/epic-03-referenzen-source-diagnosen.md` und `tasks/decompiled-assembly-audit/code-map.md`.
- Keine Produktionscode-, Test-, Konfigurations- oder Produktdokumentationsänderung; keine Builds, Tests oder Commits.
- Nach der letzten Code-Map-Änderung wurden gezielte redigierte MCP-Nachweise erneut ausgeführt: Projekt-Symbol-/Violation-Spotchecks, `inspect_assembly` für LOCAL-01 bis LOCAL-03, `find_assembly_extensions` für LOCAL-03, `inspect_assembly` für FALSE-01 sowie ein zunächst abgewiesener und danach korrekt begrenzter GIT-01-Wiederholungsaufruf. Die lokalen Origin-/Partial-/Diagnosewerte blieben konsistent; FALSE-01 blieb ein recoverable `WORKSPACE_DIAGNOSTIC`; GIT-01 blieb terminal `provider-unavailable`/decompiled/partial ohne Snapshot und wurde nicht als Source-backed-Erfolg gewertet. Keine weitere Datei wurde danach geändert.

### Commit-Vorschlag

`docs: Epic-3-Referenz- und Source-Diagnoseaudit dokumentieren`
