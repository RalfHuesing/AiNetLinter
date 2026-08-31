# MCP-Live-/Vertragsaudit: verbleibende Werkzeuge und Dokumentation

Datum: 2026-08-31  
Status: abgeschlossen; ausschließlich read-only MCP-Proben. Keine Produktions-
oder Testcodeänderung, kein Build, kein Testlauf und kein Push.

## Rahmen und Anonymisierung

Die Proben verwendeten `project target`, `repo-provided assembly` sowie
`installed vendor assembly A`. Zielpfade, Produkt-, Hersteller-, Repository-,
Assembly-, Namespace-, Typ-, Member- und Dateinamen bleiben redigiert.
`Type_1`, `Member_1` und `File_1` stehen ausschließlich für aus einer kleinen
vorherigen Antwort übernommene Folgeparameter. Größen sind gerundete
Textzeichen als Response-/Tokenbudget-Indikator, keine Rohantworten.

Es wurden weder DRY-, Dead-Code-, Magic-Value- noch Safeguard-Prüfungen
aufgerufen. Ein vorhandener Assembly-Inspektor diente nur zur eng begrenzten
Ermittlung eines gültigen anonymisierten Symbol-Identifiers.

## Tool-Coverage-Matrix

| Tool | Zielklasse und kleine Parameterproben | Status und Responseform | Origin, Completeness, Truncation, Diagnostics | Response-/Tokenverhalten und Agentennutzbarkeit | Evidenz, Finding, Umfang, Priorität, Disposition |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `get_hotspots` | `project target`: Default sowie enger `scopeFilter`; `repo-provided assembly`: gleicher Minimalaufruf | Projekt: Erfolg, `isError=false`, knapper Text plus StructuredContent. Assembly: regulär `unsupported`, `isError=false`, nur Text | Projektantwort enthält keine Origin-/Completeness- oder Truncation-Projektion; bei Assembly nur der Unsupported-Status, keine Diagnose- oder strukturierte Fehlerprojektion | Default war deutlich größer als der enge Scope; der enge Scope reduzierte den Scan sichtbar. Als Vorab-Orientierung gut, Assembly-Fallback aber nur textuell klassifizierbar | Positive Scope-Wirkung; Unsupported bestätigt RMT-001. Tool-lokal bzw. systemischer Fehlerpfad; P1; Produktionsfix erforderlich |
| `pattern_detect` | `project target`: ein Pattern, zwei Patterns, Default, enger Scope, `maxResultsPerPattern=1/0`, unbekanntes Pattern; `repo-provided assembly`: ein Pattern | Gültige Projektproben: Erfolg, `isError=false`, StructuredContent mit Gruppen und Summary. Unbekanntes Pattern: reguläres `INVALID_ARGUMENT`, `isError=false`, nur Text. Assembly: regulär `unsupported`, nur Text | Erfolgsfall liefert den kleinen gruppierten Slice; für die geprüften Nullmengen keine Trunkierung oder Diagnostics. Fehler/Unsupported ohne StructuredContent | Einzel- und Batch-Pattern bleiben mit Limit 1 klein; Default ist merklich größer, aber noch kompakt. Gute Progressive-Disclosure-Einstiegsreihenfolge, wenn nur gültige IDs genutzt werden | Parameter-, Scope- und Limitvertrag bestätigt; Fehlerklassifikation bestätigt RMT-001. Systemischer Fehlerpfad; P1; Produktionsfix erforderlich |
| `get_violations` | `project target`: `maxResults=1/0`, `includeSnippet=false/true`, `contextLines=1/5`, enger Scope; `repo-provided assembly`: Minimalaufruf | Projekt: Erfolg, `isError=false`, StructuredContent. Assembly: regulär `unsupported`, `isError=false`, nur Text | Die geprüfte Projektmenge war leer und vollständig markiert; deshalb kein positiver Nachweis für Snippet-/Context-Zeilen oder Resultattrunkierung möglich. Assembly hat weder Origin noch Fehlerobjekt | Leere, kleine Slices sind sehr kompakt. Für nichtleere Slices bleibt eine gezielte Wiederholungsprobe nötig, bevor Snippet-Text in den Agentenkontext übernommen wird | Leer- und Scopevertrag bestätigt; Snippetwirkung offen. Unsupported bestätigt RMT-001. Systemischer Fehlerpfad; P1; Produktionsfix erforderlich |
| `get_namespace_tree` | `project target`: Overview mit `depth=1/2`, dann Projekt-Drilldown mit `includeTypes=false/true`, `kind=class`, `maxResults=1/0`; `repo-provided assembly` und `installed vendor assembly A`: `depth=1`, keine Typen, Limit 2 | Alle gültigen Proben: Erfolg, `isError=false`, StructuredContent. Ungültiges `kind`: reguläres `INVALID_ARGUMENT`, `isError=false`, nur Text | Projekt-Drilldown trennt `totalCount`, `shownCount` und Truncation. Assemblys zeigen `origin=decompiled`, partiellen Zustand sowie strukturierte Analyse-/Completeness-Daten | Die Overview ignoriert Detailoptionen erwartbar, solange kein Projekt-/Namespace-Drilldown gewählt ist. Kleine Limits erzeugen gut nutzbare Zoomstufen; `maxResults=0` verhielt sich wie der Default statt als Fehler | Assembly- und Projektstrukturvertrag bestätigt. Untergrenze 0 nur implizit, kein belastbarer Vertragsverstoß. Kein Befund; lokal, P3; beobachten |
| `metrics_lookup` | `project target`: ein und zwei anonymisierte Symbol-Identifier; `repo-provided assembly` und `installed vendor assembly A`: jeweils ein gültiger Identifier; fehlender sowie nicht auflösbarer Identifier | Gültige Proben: Erfolg, `isError=false`, StructuredContent mit Ergebnisbatch; Assemblys zusätzlich Analyseblock. Fehlend/nicht auflösbar: regulär `INVALID_ARGUMENT` beziehungsweise `SYMBOL_NOT_FOUND`, `isError=false`, nur Text | Assembly-Ergebnisse enthalten dekompilierte Herkunft und partiellen Analyse-/Completeness-Kontext. Projektbatch selbst braucht keine Origin-Projektion | Ein Identifier blieb klein, zwei erhöhten die Antwort annähernd proportional. Dadurch als gezielter One-Shot gut einsetzbar; Fehler müssen aus Text geparst werden | Batch-, Assembly- und Symbolfehlervertrag bestätigt; Fehlerform bestätigt RMT-001. Systemischer Fehlerpfad; P1; Produktionsfix erforderlich |
| `metrics_tree` | `project target`: `code_size`/`complexity`, `depth=1/2`, `topN=1/2`, enger `fileFilter`; fehlendes `mode`, `topN=0`, ungültiger Filter; `repo-provided assembly` und `installed vendor assembly A`: `code_size`/`complexity`, Tiefe 1, Top-N 1 | Gültige Proben: Erfolg, `isError=false`, ASCII-Text ohne StructuredContent. Fehlparameter: reguläres `INVALID_ARGUMENT`, `isError=false`, nur Text | Assembly-Text weist dekompilierte Herkunft und partiellen Zustand aus; diese Daten sind nicht typisiert. Top-N begrenzt die sichtbaren Kinder; kein maschinenlesbares Truncation-/Diagnostics-Objekt | Kleine depth-/topN-Werte ergeben sehr kleine Antworten. Für automatische Folgeentscheidungen ist der Projektbaum lesbar, der Assemblyzustand jedoch ohne Textparser nicht robust verwertbar | Limit- und Fehlervertrag bestätigt. Assembly-Origin/Completeness nur im Text: RMT-002. Tool und Dokumentation; P2; Dokumentation plus typisierte Projektion nachschärfen |

## Fehler- und Zielvertrag

Die drei projektgebundenen Werkzeuge (`get_hotspots`, `pattern_detect`,
`get_violations`) antworten gegen ein Assemblyziel konsistent mit
`ASSEMBLY_TARGET_UNSUPPORTED` und `isError=false`. Die drei
Snapshot-Werkzeuge (`get_namespace_tree`, `metrics_lookup`, `metrics_tree`)
arbeiten gegen beide geprüften Assemblyklassen mit dekompilierter, partieller
Herkunft. Bei `get_namespace_tree` und `metrics_lookup` ist sie strukturiert;
bei `metrics_tree` nur im Text.

Fehlende oder ungültige Filter der repräsentativen projektgebundenen Werkzeuge
ergaben reguläre Fehler mit `isError=false`, ebenso fehlende Identifier und
ungültige Tree-Parameter. Das entspricht der dokumentierten Regel für
Anwendungsfehler. Es reicht aber nicht für Clients, die ohne Textparser
zwischen Erfolg, Unsupported und Fehler unterscheiden müssen.

## Findings

### RMT-001 – reguläre Fehler und Unsupported bleiben nicht typisiert

- Klassifikation: bestätigt; Umfang: systemisch über die sechs Werkzeuge.
- Evidenz: unbekannte Pattern-ID, fehlender/nicht auflösbarer Identifier,
  ungültiger Kind-/Tree-Parameter und drei Assembly-Unsupported-Proben liefern
  jeweils lesbaren Fehlertext bei `isError=false`, jedoch kein
  StructuredContent-Fehlerobjekt.
- Wirkung: Clients können die dokumentierte `isError=false`-Semantik nur mit
  Textmustererkennung von einem erfolgreichen Textresultat trennen.
- Dringlichkeit: P1.
- Disposition: Produktionsfix erforderlich. Ein einheitliches, redigiertes
  StructuredContent mit mindestens Code, Kategorie und Zielklasse ergänzen;
  bisherigen Text als menschenlesbaren Fallback behalten.

### RMT-002 – `metrics_tree` projiziert Assemblyzustand nur als Text

- Klassifikation: bestätigt; Umfang: lokales Tool plus Dokumentationsvertrag.
- Evidenz: Beide Assemblyproben waren erfolgreich und enthielten dekompilierte
  Herkunft sowie partielle Analyseinformation im Text, während
  StructuredContent fehlte. `metrics_lookup` liefert für dieselbe Zielklasse
  einen typisierten Analyseblock.
- Wirkung: Der kleine Baum ist lesbar, aber eine agentische Folgeentscheidung
  über Origin, Completeness, Truncation und Diagnostics ist nicht stabil
  automatisierbar.
- Dringlichkeit: P2.
- Disposition: Dokumentation zuerst um den Text-only-Grenzfall ergänzen;
  anschließend eine typisierte Analyseprojektion angleichen.

### RMT-003 – Progressive Disclosure nennt keine explizite Kleinlimit-Strategie

- Klassifikation: Dokumentationslücke; Umfang: lokale Agentendokumentation.
- Evidenz: Die Referenz dokumentiert Defaults und Caps der einzelnen Tools,
  und die Integration beschreibt die semantische Reihenfolge. Sie empfiehlt
  aber nicht ausdrücklich, bei `pattern_detect`, `get_violations`,
  `get_namespace_tree` und `metrics_tree` zunächst Resultat-/Tiefenlimits von
  eins bis zwei zu wählen und erst nach sichtbarer Truncation zu erweitern.
- Wirkung: Agenten erhalten zwar gültige Defaults, aber keine einheitliche
  Budgetstrategie für die vier breiteren Listen-/Baumwerkzeuge.
- Dringlichkeit: P3.
- Disposition: Dokumentation ergänzen; kein Produktionsrisiko.

## Dokumentationsabgleich

| Dokumentationsaspekt | Live-Abgleich | Ergebnis / Disposition |
| :--- | :--- | :--- |
| Toolnamen, Parameter und Zieltypen | Die sichtbaren Schemas und die Capability-Matrix entsprechen allen sechs Live-Tools: drei project-only, drei Snapshot-fähig. Defaults, Pattern-Batch, Snippet-/Kontextparameter, Namespace-Zoom und Metrics-Modi waren adressierbar. | Kein Befund |
| Unsupported-Grenze | Die drei project-only Werkzeuge weisen Assemblyziele explizit als unsupported aus, wie dokumentiert. Live ist dies aber nur Text ohne strukturierten Fehlerzustand. | RMT-001 |
| Limits und Trunkierung | Kleine Resultat-, Tiefen- und Top-N-Proben reduzierten die sichtbaren Antworten. Projekt-Namespace-Drilldown liefert Counts und Truncation. Die leere Violations-Probe konnte Snippet/Truncation nicht positiv belegen; `maxResults=0` beim Namespacebaum war nur implizit Defaultverhalten. | Kein dokumentierter Vertragswiderspruch; RMT-003 als Nutzbarkeitshinweis |
| Origin, Fallback und Completeness | Snapshot-fähige Tools arbeiteten auf beiden Assemblyklassen dekompiliert und partiell. Namespacebaum und Metrics-Lookup liefern das typisiert; Metrics-Tree nur im Text. | RMT-002 |
| Error-Klassifikation | Die Referenz beschreibt reguläre `INVALID_ARGUMENT`-Ergebnisse mit `isError=false`; das wurde bestätigt. Sie erklärt jedoch nicht die notwendige Textauswertung mangels StructuredContent. | RMT-001 |
| Progressive Disclosure | Die Integrationsreihenfolge ist fachlich nachvollziehbar: Symbol-/Metrics-Lookup vor breiter Struktur, Pattern und Violations. Kleine Slices erwiesen sich live als ausreichend; Default-/breite Antworten sind größer. | RMT-003 |
| README und Konfiguration | Zielpaar, Toolkatalog-Verweis, Assembly-Fallback und Ressourcen-/Diagnosegrenzen widersprechen den Proben nicht. Für die sechs Werkzeuge keine weitere Abweichung. | Kein Befund |

## Agentische Reihenfolge

1. Bei bekanntem Symbol `metrics_lookup` mit einem Identifier; bei unbekannter
   Struktur `get_namespace_tree` zuerst nur mit einem Projekt-/Namespace-
   Drilldown und Limit eins bis zwei.
2. Für Verzeichnis-/Komplexitätstendenzen `metrics_tree` mit `depth=1` und
   `topN=1`; Assemblyantworten nur als textbasiert gekennzeichneten Fallback
   verwenden.
3. Für Regel-/Patternübersichten zuerst ein Pattern beziehungsweise
   `maxResults=1`; Snippets nur gezielt als Folgeaufruf aktivieren.
4. Vor jeder Assemblyinterpretation Origin, partielle Completeness, Truncation
   und Diagnostics prüfen. Bei project-only Werkzeugen Unsupported nicht als
   Leermenge interpretieren; bis RMT-001 behoben ist, den redigierten
   Fehlertext auswerten.

Die vorhandene Dokumentation erklärt Zieltypen, Fallback und die allgemeine
Fehlerklasse ausreichend für Menschen. Für agentische Automatisierung fehlen
die klare Kleinlimit-Empfehlung und ein dokumentierter Text-only-Fallback für
fehlende strukturierte Fehler-/Analyseobjekte.

### Commit-Vorschlag

docs: dokumentiere verbleibende MCP-Werkzeugproben
