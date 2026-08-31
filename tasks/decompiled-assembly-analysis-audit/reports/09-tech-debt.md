# Konsolidierter Tech-Debt-Report

## Auftrag und Konsolidierungsbasis

- **Primäraufgabe:** 360-Grad-Audit der externen Assembly-Analyse.
- **Scope:** acht Reviewer-Linsen für Assembly-Zielrouting und Decompilation, externe Source-Verträge, Git-Transport, Checkout-Sicherheit, Cache-/Snapshot-Lebenszeit, MCP-Verträge, Agentenoberfläche sowie Tests/Dokumentation.
- **Basis:** committed Einzelreports `reports/01-assembly.md` bis `reports/08-tests-documentation.md`; die Produktions- und Testquellen blieben im gesamten Audit unverändert.
- **Betriebsart:** Audit-only. Keine Fixes, keine Assertion-Abschwächung, keine Konfigurations- oder Dokumentationsänderung.
- **Abdeckungsgrenze der Delegation:** Die ursprünglich geplante parallele Reviewer-Welle wurde vom Agentenlimit abgewiesen. Mehrere verspätet gestartete Reviewer lieferten danach unabhängige Reports für die Linsen 01–06; Linse 07 und Linse 08 sind Orchestrator-Fallbacks. Diese Herkunft ist in den Einzelreports und im `execution-log.md` sichtbar.

## Gesamturteil

Der Audit ist abgeschlossen, aber nicht ohne technische Schulden. Zwei S1-Befunde sind belastbar: unbedingte Referenzexpansion im Assembly-Dispatcher und ein Cancellation-Fenster, das einen bereits erzeugten eigenen Checkout unbereinigt lassen kann. Mehrere S2-Befunde betreffen URL-/Credential-Verträge, Symbol-/Batch-Vollständigkeit, persistente Cache-Retention, Lock-Reclamation, Eingabefehlerprojektion und Agenten-Kontextgröße. Ein potenzielles globales Wire-Budget-Problem bleibt wegen fehlender vollständiger JSON-Messung bewusst als Validierungsauftrag und nicht als bestätigter Vertragsbruch eingestuft.

Die Tests liefern eine gemischte Abschlusslage: Build und FastTests ohne Stress sind grün; der abschließende vollständige Integration-Lauf ohne Stress endete nach Bereinigung eines eindeutig verwaisten Test-Daemons mit 70 Prozess-/Transportfehlern bei 377 Tests. Die Fehler wurden nicht auf die neuen Befunde zurückgeführt, da der Auftrag ausschließlich Reports erzeugt und keine Produktionsänderung vorgenommen wurde. Einzelne Reviewerläufe waren unter anderer Prozesslast grün oder zeigten eine andere Fehlerzahl; das wird als Umgebungsgrenze dokumentiert.

## Coverage-Matrix

| Linse | Reportstatus | Befunde | Primäre Abdeckungsgrenze |
|---|---|---|---|
| 01 Assembly/Decompilation | unabhängiger Report, nachträglich fachlich abgeglichen | `ASM-001`, `ASM-002`, `ASM-003`; ein Root-Routing-Probehinweis verworfen | neutrale DLL war wegen Referenzdiagnosen nur `partial`; keine echte Source-backed Liveprobe |
| 02 External Source | unabhängiger Report | `EXTSRC-01`, `EXTSRC-02` | kein geschützter Remote-Dienst und keine produktive Resolver-Implementierung live |
| 03 Git-Transport | unabhängiger Report | keine bestätigten S0–S3-Befunde | kein echter entfernter/authentifizierter Transport |
| 04 Checkout/Sicherheit | unabhängiger Report | `CHK-001` | zwei echte Reparse-Tests wegen fehlender Capability übersprungen; kein privilegierter TOCTOU-Lauf |
| 05 Cache/Snapshot | unabhängiger Report | `F-05-01`, `F-05-02`; `F-05-03` bedingtes Vertragsrisiko | kein mehrstündiger Langzeitlauf und keine source-backed Refresh-Probe |
| 06 MCP-Verträge | unabhängiger Report | `MCP-L6-001`, `MCP-L6-002`; Wire-Budget als offene Messung | maximale serialisierte Antwortgröße nicht gemessen |
| 07 Agentenoberfläche | Orchestrator-Fallback | `UX-001`; Wire-Duplikation nur Querverweis | reale Clienttokenisierung nicht gemessen; Clone-/Dead-Code-/Magic-Value-Kandidaten nicht bestätigt |
| 08 Tests/Dokumentation | Orchestrator-Fallback | keine neuen eigenständigen Produktbefunde | vollständiger Integration-Lauf in aktueller Prozessumgebung rot |

## Konsolidiertes Befundregister

### S1 — zuerst bearbeiten

| ID | Kurzbeschreibung | Primärbereich | Evidenz und Disposition |
|---|---|---|---|
| `ASM-001` | Referenzexpansion wird vor jedem Assembly-Handler ausgeführt, obwohl `includeReferences=false` der Default ist. | Assembly-Dispatcher | `AnalysisToolCall.cs:161-172`, Registrierungsdefaults und `Docs/agent-api.md:460`; `promoted-to-project-debt`. |
| `CHK-001` | Cancellation zwischen erfolgreicher Akquisition und lokaler Ownership-Bindung kann den Handle aus dem Cleanup-Pfad verlieren. | Checkout/Ownership | unabhängiger Kontrollflussbeleg, spätere Cancellation-Gegenprobe; `promoted-to-project-debt`. |

### S2 — priorisierte Folgearbeiten

| ID | Kurzbeschreibung | Primärbereich | Evidenz und Disposition |
|---|---|---|---|
| `EXTSRC-01` | Loader-URL-Policy und Runtime-URL-Policy akzeptieren nicht dieselbe URL-Menge. | External Source | konkrete Loader-/Runtime-Kontrollflüsse; `promoted-to-project-debt`. |
| `EXTSRC-02` | Produktive MCP-/Daemon-Einstiege verdrahten keinen Credential-Resolver. | External Source | optionale Composition-Schnittstelle versus produktive Call-Sites; `promoted-to-project-debt`. |
| `ASM-002` | Nichttreffer erwartbarer anderer Assembly-Sessions werden als globale Partialdiagnose projiziert. | Assembly-Navigation | `AssemblySymbolResolver` plus `CreateSummary`; `promoted-to-project-debt`. |
| `ASM-003` | Batch-`find_symbol` gibt nur die Navigation des letzten Musters aus und kann frühere Trunkierung verlieren. | Assembly-Navigation | `BuildResponseAsync` plus begrenzte Musterprobe; `promoted-to-project-debt`. |
| `F-05-01` | Erfolgreiche persistente Cache-Generationen werden nicht durch Retention/Sweep begrenzt. | Cache/Snapshot | Generation-Writer und Cleanup-Aufrufer; `promoted-to-project-debt`, sichere Lease-/Rollback-Prüfung erforderlich. |
| `F-05-02` | Statische Cache-Key-Lock-Tabelle entfernt Semaphore-Einträge nicht. | Cache/Snapshot | `GetOrAdd` ohne `TryRemove`/Clear/Dispose; `promoted-to-project-debt`. |
| `MCP-L6-001` | Ungültige Positionsspalte führt zu `WORKSPACE_DIAGNOSTIC`/`isError=true` statt recoverable Argumentfehler. | MCP-Fehlervertrag | direkte Spalte-0-Reproduktion und Codepfad; `promoted-to-project-debt`. |
| `UX-001` | `AssemblyAnalysisRegistry` überschreitet mit Type LOC 648 die projektierte 500er-Kontextgrenze. | Agentenoberfläche | `get_feature_context`-Metrik; `accepted-deferred`, kein Laufzeitdefekt behauptet. |
| `MCP-WIRE-001` | Diagnose-Samples werden in mehreren Structured-Content-Feldern wiederholt; globales Wirebudget ist nicht nachgewiesen. | MCP-Wire | statische Projektion, aber keine vollständige JSON-Messung; `accepted-deferred`, ausdrücklich unbestätigt. |
| `F-05-03` | Root-Byte-only-Reuse könnte reine Source-/Dependency-Änderungen nicht invalidieren. | Cache/Snapshot | Vertragsauslegung und Liveprobe offen; `accepted-deferred`, ausdrücklich kein bestätigter Defekt. |

### S3 — begrenzte strukturelle Folgearbeit

| ID | Kurzbeschreibung | Primärbereich | Evidenz und Disposition |
|---|---|---|---|
| `MCP-L6-002` | `GetServerHealthResponseBuilder` meldet `AIContextFootprint` 2502 gegenüber Grenzwert 2500. | MCP-/Agentenverträglichkeit | `get_violations` mit Datei-/Regelbeleg; `accepted-deferred`. |

Die vollständigen Pflichtfelder, Reproduktionen, MCP-Parameter und Testbelege stehen in den jeweiligen Einzelreports. `tech-debt.md` ist das kuratierte Queue-Register; dort stehen auch nächste Schritte und Log-Anker.

## Widersprüche und Deduplizierung

### Verworfenes Root-Routing-Probeergebnis

Der unabhängige Linse-01-Bericht hatte zunächst behauptet, `includeReferences=false` durchsucht den Assembly-Root nicht. Die verwendete Probe suchte jedoch nach einem referenzierten Basistyp. `AssemblyFindSymbolTool` delegiert im `false`-Branch zwar an `lease.Server`, aber diese Solution ist die dekompilierte Root-Solution der Lease; die Probe beweist daher keinen falschen Projekt-Workspace. Der Hinweis ist im Linse-01-Report als `ASM-ROUTING-01` mit `rejected/not-applicable` markiert und wurde nicht in die Queue übernommen.

Die unabhängig und durch die frühere MCP-Fallbackprüfung belegte Abweichung bleibt davon getrennt: Der gemeinsame Dispatcher expandiert Referenzen bereits vor jedem Handler, auch wenn `includeReferences=false`. Das ist `ASM-001`.

### Wire-Budget

Der Fallback-Bericht stufte die Mehrfachprojektion des 4-KiB-Sample-Caps zunächst als S1 ein. Der unabhängige MCP-Bericht bestätigte die statische DTO-/Summary-Struktur, wertete den fehlenden Nachweis eines globalen serialisierten Budgets aber als Abdeckungsgrenze. Konsolidiert wird der Punkt als `MCP-WIRE-001`, S2 mit mittlerer Beweissicherheit und `accepted-deferred`; eine echte JSON-Messung entscheidet über spätere Hochstufung oder Dokumentationskorrektur.

### Cache-/Source-Refresh

`F-05-01`/`F-05-02` sind direkte Ressourcenbefunde. `F-05-03` bleibt ein Vertragsrisiko, weil Source-/Dependency-Refresh-Anforderungen und eine belastbare source-backed Liveprobe fehlen. Es wird deshalb nicht als bestätigter Defekt formuliert.

### Testläufe

Die Einzelreports wurden zu unterschiedlichen Zeitpunkten und unter unterschiedlicher Prozesslast erstellt. Für die Abschlusscheckliste ist der Orchestratorlauf maßgeblich: FastTests grün, Integration rot mit 70 Prozess-/Transportfehlern. Reviewerberichte dürfen ihre zeitbezogenen Verifikationsnachweise behalten; die Abweichung wird nicht als Quellcodewiderspruch interpretiert.

## Qualitäts- und Auditprüfungen

- `get_violations` für den engeren External-Source-Scope meldete keine Verstöße; ein breiterer MCP-Scope meldete den Health-Builder-Befund `MCP-L6-002`.
- `safeguard` meldete einen transitive Footprint-Hinweis im MCP-Umfeld; der konkrete Health-Builder-Befund ist separat durch `get_violations` belegt.
- `find_duplicates` meldete drei Kandidatencluster in Metadata-Readern, Transport-/Refreshpfaden und Pointer-Schreibern. Ohne unabhängige Refactoringprüfung wurden sie nicht als Tech Debt aufgenommen.
- `find_dead_code` meldete nur niedrig-konfidente beziehungsweise platform-nahe Kandidaten; kein sicherer Entfernungsbefund.
- `find_magic_values` meldete einen lokalisierten User-Message-Kandidaten; kein Sicherheits- oder Wire-Budget-Befund.

## Offene Reproduktions- und Folgebedingungen

1. `ASM-001` mit einem Root-deklarierten Symbol und einer absichtlich nicht auflösbaren Referenz als Regression reproduzieren.
2. `CHK-001` mit einem kontrollierten Acquirer-Testdouble im Cancellation-Fenster deterministisch auslösen.
3. URL-Policy-Tests für Userinfo, Query, Fragment und gültige URL-Varianten gemeinsam gegen Loader und Runtime ausführen.
4. Eine ausdrücklich gewollte Credential-Quelle im produktiven Entry-Wiring festlegen und redigiert testen.
5. Cache-Generation-Retention und Lock-Reclamation über viele verschiedene Keys/Revisionen instrumentiert testen.
6. Eine vollständige Structured-Content-Maximalantwort serialisieren und die UTF-8-Größe inklusive aller Summary-/Sessionfelder messen.
7. Negative Positionsfälle für Spalte `0`, negative Werte und Überläufe ergänzen.
8. Reparse-Capability im Testhost bereitstellen und die zwei übersprungenen Sicherheitsläufe nachholen.
9. Integrationstests in einer isolierten, daemonfreien Testprozessumgebung wiederholen; `Stress` bleibt weiterhin außerhalb des Abschlusslaufs.

## Abschlussdisposition

Der gesamte Berichtssatz ist committed; die Task-lokale Queue bleibt mit `promoted-to-project-debt` und `accepted-deferred` als Arbeitsvorrat bestehen. Es gibt keinen eigenständig erfundenen globalen Backlogeintrag. Die Audit-Arbeit endet mit dokumentierten Befunden und Abdeckungsgrenzen, ohne Produktionsänderung.

### Commit-Vorschlag

`docs(decompiled-assembly-analysis-audit): Konsolidiere Tech-Debt und Abdeckungsgrenzen`
