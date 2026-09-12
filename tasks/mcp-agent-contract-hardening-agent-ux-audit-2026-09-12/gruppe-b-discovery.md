# MCP-Agent-UX-Audit – Gruppe B: Discovery

## Ziel und Scope

Prüfung der umgesetzten Discovery-Verträge aus Sicht eines konsumierenden
Agenten: `get_file_tree`, `get_namespace_tree`, `get_index_scope`,
`inspect_assembly` und `find_assembly_extensions`. Geprüft wurden Contract-v2-
Navigation, Populationen (R5), Response-Budgets (R1), Completeness/Recovery,
Signal-Rausch-Verhältnis, Schema und direkte Handoffs. Es wurden nur
read-only MCP-Aufrufe gegen das Source-Ziel sowie `LOCAL-01` und `FALSE-01`
ausgeführt. Kein Code, Build oder Test wurde verändert bzw. ausgeführt.

## Szenarien

- Source-Ziel: File-Tree als `summary`, `files`, `tree`, gefiltert und mit
  Grenzwerten (`maxResults`, `treeDepth`, `maxResponseBytes`); Wiederholung zur
  Determinismusprüfung.
- Source-Ziel: Namespace-Übersicht/Drilldown, Trunkierung und fehlerhafter
  Tiefenwert mit anschließendem Recovery; Index-Scope und dessen Routing.
- `LOCAL-01`: API-Inspektion und Extension-Suche, Paging sowie minimale und
  knapp bemessene Response-Budgets.
- `FALSE-01`: Negativfall für beide Assembly-Discovery-Tools.

## Positive Nachweise

- **R5 – Populationen klar getrennt:** `get_index_scope` weist 919 physische
  Dateien, 930 Roslyn-Dokumente, 23 generierte und 431 Test-Dokumente als
  separate Einheiten aus. Der File-Tree für den korrespondierenden Source-
  Teilbaum bestätigt 919 physische Dateien; Ausschluss- und Reparse-Counts sind
  eigenständige Felder.
- **Progressive Disclosure und Completeness:** `summary` liefert keine
  Dateiliste. `files` am Root signalisiert die Begrenzung sowohl fachlich
  (`maxResults`, `maxDepth`) als auch in `navigation.status.completeness` und
  mit einem konkreten `next`. Namespace-Drilldown mit fünf von 17 Ergebnissen
  verhält sich gleichartig.
- **R1 für Assembly-Antworten:** Bei zu kleinem Budget liefern beide
  Assembly-Tools einen recoverable Budgetfehler mit ausführbarem Mindestwert.
  Die Wiederholung von `inspect_assembly` mit 3334 und von
  `find_assembly_extensions` mit 2721 Bytes war jeweils erfolgreich und
  behielt vollständige Facheinheiten bei.
- **Assembly-Recovery:** `LOCAL-01` kennzeichnet dekompilierte, diagnostisch
  eingeschränkte Ergebnisse als `partial`; bei Typ-Paging ist ein
  Continuation-Token vorhanden. `FALSE-01` liefert für beide Tools einen
  strukturierten, nicht-retrybaren `INVALID_ASSEMBLY`-Fehler ohne Crash.
- **Validierung und Zustandsstabilität:** `get_namespace_tree(depth=0)` nennt
  Feld und Korrektur. Der unmittelbar folgende gültige Aufruf ist vollständig
  erfolgreich. Zwei identische File-Tree-Summaries lieferten denselben
  Snapshot und dieselbe fachliche Struktur.

## Findings

### [Critical] B-01: Index-Scope liefert kein direkt ausführbares Non-C#-Routing

- **Tools:** `get_index_scope` → `search_pattern`
- **Evidenz:**
  1. Der strukturierte Non-C#-Routing-Eintrag nennt `search_pattern`, aber das Feld `fileFilter`.
  2. Das veröffentlichte `search_pattern`-Schema hat stattdessen ausschließlich `includePatterns`.
  3. Direkte Übergabe des gelieferten Felds endet mit `INVALID_ARGUMENT`; dieselbe Suche mit `includePatterns` gelingt.
- **Problem:** Der maschinenlesbare Discovery-Handoff ist für jede nicht-C#-
  Extension kaputt. Ein Agent, der Contract-v2-StructuredContent wie vorgesehen
  weiterreicht, bekommt einen Clientfehler statt einer eingegrenzten Suche.
- **Reproduktion:** `get_index_scope` am Source-Ziel aufrufen; den
  `routing.nonCSharp.fileFilter`-Wert als `fileFilter` an `search_pattern`
  übergeben.
- **Empfehlung:** Routing auf das tatsächliche Schemafeld `includePatterns`
  (als Array) ausgeben und die Breakdown-Einträge identisch benennen; einen
  direkten Contract-Handoff-Test ergänzt absichern.

### [Major] B-02: `get_file_tree` ignoriert sehr kleine Response-Budgets stillschweigend

- **Tools:** `get_file_tree`
- **Evidenz:**
  1. `summary` mit `maxResponseBytes` 1, 64 und 512 endet jeweils erfolgreich und nur als `truncated`.
  2. Die drei serialisierten Antworten sind jeweils über 3100 Zeichen groß und inhaltlich gleich.
  3. Kein Fehler nennt ein auszuführendes Mindestbudget; `next` fordert nur allgemein zum Erhöhen auf.
- **Problem:** Damit kann ein Agent sein selbst gesetztes Wire-Budget nicht
  verlässlich einhalten und erhält – anders als bei den Assembly-Tools – keinen
  deterministisch wiederholbaren R1-Recovery-Wert.
- **Reproduktion:** `get_file_tree(view="summary", maxResponseBytes=1)` am
  Source-Ziel aufrufen und den Rückgabewert mit dem angeforderten Budget
  vergleichen.
- **Empfehlung:** Entweder das Budget bis zur kleinsten gültigen Antwort
  tatsächlich einhalten oder `RESPONSE_BUDGET_TOO_SMALL` mit einem exakten,
  erfolgreichen Mindestwert liefern.

## Freie Beobachtungen

- `find_assembly_extensions` kann bei null gefundenen Extensions aufgrund der
  dekompilierten Analyse dennoch `partial` sein. Das ist fachlich plausibel und
  transparent; ein Agent sollte die Leermenge deshalb nicht als globale
  Negativbehauptung lesen.
- Bei einem `tree`-Aufruf sind `maxDepth` und Response-Budget als getrennte
  Trunkierungsgründe sichtbar. Das verhindert die sonst typische falsche
  Annahme, allein ein höheres Ergebnislimit mache den Baum vollständig.

## Grenzen

Die Prüfung bewertet beobachtbares MCP-Verhalten in dieser laufenden Server-
und Workspace-Sitzung. Eine vollständige Schema-Inventur aller MCP-Tools, die
Qualität anderer Toolgruppen und die tatsächliche Bytegröße des Transportframes
lagen außerhalb dieses Teilscopes. Die angegebene Zeichenmessung in B-02 ist
bereits deutlich größer als die getesteten Budgets und genügt daher als
Negativnachweis, ersetzt aber keine Wire-Trace-Messung.
