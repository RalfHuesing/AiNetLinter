# Gemeinsamer Rahmen für die AiNetLinter-MCP-Releases

Stand: 2026-09-07. Die Audit-Findings und das Audit-Konzept bleiben Evidenz
und werden nicht durch diesen Rahmen umklassifiziert.

## Release-Reihenfolge und Schnitt

1. `01-mcp-unified-analysis-target`: ein harter, vollständig migrierter
   Zielvertrag; kein nachträglicher Alias.
2. `02-mcp-agent-handoff`: Discovery, Suche, IDs und begrenzte Navigation auf
   diesem Vertrag.
3. `03-mcp-development-workflow`: sichere Änderungsschleife für Source.
4. `04-mcp-assembly-quality`: ehrliche, stabile Decompiled-Navigation.

Jeder Ordner ist genau ein releasebarer, testbarer Nutzen. Ein nachfolgender
Task darf weder einen früheren öffentlichen Vertrag reparieren noch dessen
Ergebnis stillschweigend voraussetzen. Dokumentation reist mit dem
vertragsändernden Release; es gibt keinen Restdokumentations-Task.

## Verbindlicher Antwortkern

Alle zielgebundenen Antworten enthalten, soweit die Capability erreichbar ist,
dieselbe maschinenlesbare `navigation`-Projektion: `target`, `origin`,
`snapshot`, `capabilities`, `operationStatus`, `result`, `completeness` und
`next`. Tool-spezifische Nutzdaten bleiben außerhalb dieses Kerns; es gibt
keinen Mega-DTO und keine künstlich gemeinsame Project-/Assembly-Session.

| Begriff | Verpflichtende Bedeutung |
| --- | --- |
| `operationStatus=ok` | Anfrage konnte ausgeführt werden; dies sagt noch nichts über die Trefferzahl. |
| `empty` | `ok`, `totalCount=0`, Analyse des angefragten Scopes vollständig; keine globale Negativaussage außerhalb des Scopes. |
| `complete` | Alle Daten des erklärten Ergebnisscopes wurden ausgewertet und projiziert. |
| `partial` | Analysegrundlage oder fachliche Aussage ist begrenzt, etwa fehlende Referenzen, dynamische Auflösung oder Decompiled-Snapshot. |
| `truncated` | Mehr passende, bereits bekannte Ergebnisse passen nicht in den Ergebnis- oder Wire-Budget; `totalCount`, `returnedCount`, `truncatedBy` und ein nächster Schritt sind Pflicht. |
| `unsupported` | Das Ziel oder Tool besitzt diese Capability strukturell nicht; nie als leere Erfolgsmenge. |
| `not_configured` | Capability wäre möglich, aber die erforderliche Konfiguration fehlt; nie als sauberes Lint-Ergebnis. |
| `not_decidable` | Statische Evidenz reicht für die behauptete Schlussfolgerung nicht; Ursache und sichere Alternative sind Pflicht. |
| Fehler | `invalid_argument`, `target_mismatch`, `stale_snapshot` und exogene Fehler haben verschiedene Codes, Feldpfade und einen sicheren nächsten Schritt. |

`complete`, `partial` und `truncated` gelten immer für ein benanntes
Ergebnisfeld bzw. einen Composite-Abschnitt, nicht pauschal für die Session.
Text und `structuredContent` werden aus derselben Aggregation erzeugt. Der
Text fasst nur Status, die besten Ergebnisse und den nächsten Schritt zusammen;
er darf weder IDs, Counts noch Status verändern oder weglassen, die für den
Folgecall nötig sind.

## Identität, Scope und Lebensdauer

- Eine ausgegebene `symbolId` ist ein vollständiger, kopierbarer Input für die
  ausdrücklich in `next.supportedTools` genannten Folge-Tools. Eine sichtbare
  Zeile, ein FQN oder ein Pfad ist keine ID, sofern `handoff=false`.
- Source-IDs basieren auf der kanonischen DocCommentId. Assembly-IDs binden
  DocCommentId an kanonischen Assemblypfad und Content-Hash; eine Cache-
  Generation bleibt intern und ist kein Agenteninput.
- Jede ID und Fortsetzung trägt `targetFingerprint`; target-fremde IDs ergeben
  `target_mismatch`. Geänderter Inhalt ergibt `stale_snapshot` mit aktueller
  Wiederholungsanweisung, nie `symbol_not_found` oder ein Generation-Rätsel.
- `scope` benennt Target, Produktions-/Testbereich, Datei/Namespace und
  Richtung, falls angewendet. Ein Ranking darf Test-, Diagnose- oder Artefakt-
  Treffer nicht vor Produktion verbergen; getrennte Counts sind Pflicht.
- Project- und Assembly-Leases, Cache, TTL und Eviction bleiben getrennt. Jede
  Antwort nennt für Assemblergebnisse `origin=decompiled`, Snapshot-Fingerprint
  und Freshness. Ein Neustart invalidiert keine semantisch gleiche ID, wohl aber
  eine Fortsetzung oder einen Snapshot-gebundenen Detailausschnitt.

## Budgets und nächste Aktion

Der Default ist maximal 20 gerankte Primärtreffer oder 8 KiB serialisierte
Nutzdaten (der kleinere Wert); Diagnose-Samples sind zusätzlich auf 3 oder
1 KiB begrenzt und kommen nach den Nutzdaten. Composites erhalten maximal
12 KiB insgesamt und maximal 4 KiB je Abschnitt. Grenzen sind serverseitig
gedeckelt und als `budget` ausweisbar. Das sind Release-Akzeptanzwerte, keine
unbelegten Performance-Versprechen; jedes betroffene Tool erhält Wire-Tests.

Bei einer Kürzung lautet `next.kind` genau eines von `refine_scope`,
`change_direction`, `request_detail` oder `continue`. `continue` ist nur bei
stabiler, deterministischer Sortierung zulässig und Token/Filter/Snapshot
bindend. Sonst gibt es keinen Cursor. Ein Empty-Ergebnis liefert nur dann
einen Fallback-Hint, wenn der aktuelle Scope das Ergebnis nicht entscheiden
kann; ein vollständiges Empty behauptet niemals Repository-weite Abwesenheit.

## Gemeinsame Architektur und Verifikation

Registrierungen bleiben explizit; kein Plugin-System, DI-Container,
Reflection-Registration oder Command-Bus. Gemeinsame Contracts zentralisieren
nur Status, Navigation und Projektion. Fachscanner, Roslyn-Resolver und die
beiden Lifecycle-Systeme bleiben getrennt.

Pro Release sind erforderlich: passende Fast- und Integrationstests,
`dotnet build`, beide Nicht-Stress-Suiten, Raw-Wire-Tests (Schema, Text und
StructuredContent), ein frisch gestarteter MCP-Dogfood-Call, Dokumentations-
abgleich, `git diff --check` und ein eigener deutscher Conventional-Commit.
Der volle 45-Finding-Nachlauf ist nur nach Task 04 Pflicht; nach jedem anderen
Release genügt der zugeordnete Szenarienkatalog plus Schutz der positiven
Baseline (`get_symbol_body`, gezielte Violations, `metrics_tree`,
`report_observability_feedback`, read-only Assemblyanalyse).
