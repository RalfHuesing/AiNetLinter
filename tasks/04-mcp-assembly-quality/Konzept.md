---
status: draft
type: konzept
project_kind: brownfield
estimated_scope: large
rules_dir: .agents/rules
last_updated: 2026-09-07
open_questions:
  - Keine; reale repräsentative DLL/EXE-Proben werden bei Umsetzung als Testfixtures festgelegt.
depends_on:
  - tasks/01-mcp-unified-analysis-target/Konzept.md
  - tasks/02-mcp-agent-handoff/Konzept.md
  - tasks/ainetlinter-mcp-usage-audit/shared/Befundmatrix.md
  - tasks/ainetlinter-mcp-usage-audit/shared/MCP-Verbesserungsrahmen.md
related_tasks:
  - tasks/03-mcp-development-workflow/Konzept.md
supersedes: null
---

# Task 04: Vertrauenswürdige Assembly- und Decompiled-Navigation

## Ziel und Problem

Eine externe DLL/EXE soll als statischer Snapshot für Katalog → Typ → Member/
Body/Graph brauchbar sein, ohne Source-, Consumer- oder Laufzeitwissen
vorzutäuschen. Auditbefunde zeigen instabile generationsgebundene IDs,
Diagnose-lastige Kataloge, falsche Completeness und unklare Extension-
Leermengen.

## Primärer Agentennutzen

Primärer Agentennutzen: Ein Agent kann eine Assembly sicher erkunden und
versteht, wann eine Folgerung wegen Referenzen, Decompilation oder fehlendem
Consumer nicht tragfähig ist. Dieser seltenere Pfad ist ein eigener Release,
weil sein Lifecycle fachlich nicht mit Source vereinheitlicht werden darf.

## Source of Truth und betroffene Bereiche

- Audit: Paket 05, besonders `inspect_assembly`, `search_assembly`,
  `get_assembly_context`, `find_assembly_extensions`, `resolve_type_origin`,
  `combo-assembly`, `combo-cross-target` und `combo-batch-ids`.
- Rahmen: Assembly-ID, Snapshot-/Stale-, Completeness- und Budgetsemantik.
- Code: `Mcp/Assemblies/Analysis/*`, Assemblytools/-Formatter,
  `AnalysisSymbolIdentity`, Resolver, `TypeResolution/*`, Response-Limits,
  Registry-/Lifecycle-/Concurrency-/Integrationstests.

## Scope

- Katalog, Suche, Context, Struktur, Body und Graph geben durchgängig
  kopierbare Assembly-IDs, `origin=decompiled`, Fingerprint, Scope und
  abschnittsbezogene Completeness aus.
- Content-Hash plus kanonischer Pfad identifizieren einen Snapshot. Cache-
  Generation wird nie in eine öffentliche ID eingebettet. Gleicher Name an
  anderem Pfad bleibt getrennt; veränderter Inhalt erzeugt einen neuen,
  nachvollziehbaren Snapshot.
- First-open, Mutation beim Hashing/Decompilieren, parallele Leases, TTL,
  Kapazität und Cleanup werden deterministisch. Fehlende/obfuskierte/
  getrimmte Metadaten werden `partial` mit begrenzten Diagnostics; nicht
  öffnungsfähige Roots sind Fehler, nie leeres Ergebnis.
- Budget priorisiert Katalogzeilen (Typ + ID) vor Memberdetails und Nutzdaten
  vor Diagnostics. Extension-Ergebnisse unterscheiden `none_in_assembly`,
  `receiver_unresolved`, `not_applicable` und ohne Consumer `not_decidable`.
- Source-only-Tools melden `unsupported`; keine Git-, Build-, Test-, Originalline-
  oder Runtimeaussage wird aus Decompiled-Code abgeleitet.

## Muss-Kriterien

- Ein Katalogtyp führt über eine vollständige ID bis Body/Structure/Graph;
  FQN, Datei:Zeile oder Anzeigename werden nicht als Ersatz-ID ausgegeben.
- `complete`, `partial` und `truncated` beschreiben jeweils Katalog, Treffer,
  Member, Body oder Diagnostics, niemals unklar die ganze Session.
- Target-Mismatch, Hash-Mismatch und stale Snapshot haben eigene Fehlercodes
  und sichere Wiederholung; ein Neustart produziert keine neue Agenten-ID für
  denselben Inhalt.
- Keine fehlende Referenz, leere Caller-Liste oder Extension-Entscheidung wird
  als globale Negativaussage oder Consumer-Beweis ausgegeben.

## Messbare Akzeptanzkriterien

- Zwei gleichnamige DLLs, eine mutierte DLL und parallele Erstöffnung prüfen
  Pfad-/Hashidentität, Stale-Retry, Leases und Cleanup deterministisch.
- Katalog → Type → Body/Structure/References/Extension wird in mindestens drei
  Fixtureformen kopiert getestet, einschließlich fehlender PDB/Referenz und
  nicht dekompilierbarer Datei.
- Der Default liefert maximal 20 Typzeilen mit ID und bleibt unter 8 KiB;
  bei Budgetdruck kürzt er Details vor Katalognavigation und weist das aus.

## Non-Goals

Keine Assemblyausführung/dynamische Ladung, Source- oder PDB-Ersatz,
Assembly-Linting, Consumer-/Runtime-Beweis, gemeinsame Project-/Assembly-
Registry oder erneute Target-/Source-Workflowänderung.

## Architekturannahmen

Assembly-Lease und Snapshotcache bleiben unabhängig. Der gemeinsame Rahmen
liefert nur öffentliche Projektion. Decompilertexte, Strings und Metadaten sind
untrusted Input; sie können keine Agenteninstruktionen oder Statusregeln
überschreiben.

## Fehler-, Fallback- und Lebenszeitsemantik

Fingerprintwechsel während Erzeugung ergibt atomaren Snapshot oder
`stale_snapshot` mit neuem Katalogcall. Evicted/abgelaufene Continuation
meldet Stale statt still neu zu paginieren. Missing references ergeben
`partial` nur für betroffene Aussagen und begrenzte Diagnosesamples.
Extensionanwendbarkeit ohne Consumer ist `not_decidable`, nicht negativ.

## Abhängigkeiten

Task 01 liefert Target-/Capabilityvertrag, Task 02 die Handoff-/Budget- und
ID-Projektion. Task 03 ist unabhängig bis auf den Rahmen und darf nicht auf
Assembly-Lint oder Consumerkontext warten.

## Risiken und Sackgassen

Mehr Snapshotdetails können CPU, Disk und Wirebudget dominieren; Katalog vor
Detail und harte Limits schützen die Agentenschleife. Eine Source-Parität wäre
fachlich falsch. Eine ID mit Cachegeneration ist eine Neustart-Sackgasse und
wird ausdrücklich nicht fortgeführt.

## Alternativen mit Konsequenzen

- Assemblypfad nur dokumentieren: belässt die falschen Handoffs.
- Alles als `partial` markieren: ehrlich klingend, aber keine abschnittsweise
  Entscheidung möglich.
- Project- und Assembly-Cache vereinigen: weniger Klassen, aber falsche
  Lifecyclekopplung und hohes Concurrency-Risiko.

## Konkrete Agenten-Szenarien

| Fall | Call / muss enthalten / darf nicht behaupten | Nächster Schritt |
| --- | --- | --- |
| G: seltene Assemblyanalyse | `inspect_assembly` zeigt Katalog + IDs; Suche/Context übernimmt dieselbe ID/Fingerprint; Extensions zeigen entscheidbaren Bucket. | Detailcall oder bei `not_decidable` Consumerprojekt analysieren. |
| C: große Menge | Katalog priorisiert Typ+ID, grenzt Member/Diagnostics ehrlich ein. | Pattern/Namespace verfeinern oder gebundene Continuation. |
| F: Snapshotwechsel | Detailcall unterscheidet stale vom Targetfehler und liefert Wiederholung. | Katalog des aktuellen Snapshots erneut aufrufen. |

## Token-/Antwortbudget-Annahmen

Die Rahmenlimits gelten; pro Abschnitt gilt ein eigenes Budget. Typkatalog und
Handoff-IDs dürfen nicht durch Events, Bodies oder Compilerdiagnosen verdrängt
werden. Diagnostics bleiben auf drei Samples/1 KiB begrenzt und folgen der
Nutzinformation.

## Verifikation

Unit-, Raw-Wire-, Integration- und Lifecycletests; Concurrency-Stress nur auf
ausdrückliche Anforderung; frische MCP-Dogfood-Assemblyproben, Build, beide
Nicht-Stress-Suiten, vollständiger 45-Finding-Nachlauf, Dokumentations- und
Diff-Gate.

## Dokumentationsbedarf

Agent API und MCP-Instructions dokumentieren den statischen Snapshot,
Fingerprint-/Stale-Regeln, die Capabilitymatrix, Extension-`not_decidable` und
die Nichtaussagen zu Source, Runtime und Consumer.

## Release-Gate

Ein Catalog→Detail-Handoff ohne gültige ID, generationabhängige Agenten-ID,
falsches `complete` oder eine Consumer-/Runtime-Behauptung blockiert Release.

## Nächste fachliche Entscheidung

Nach dem vollständigen Audit entscheiden, ob beobachtete reale Assemblygrößen
eine Änderung der getesteten Budgetwerte rechtfertigen; neue Capabilities
werden nur mit neuer Evidenz geplant.
