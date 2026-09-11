# Usage-Audit – konsolidierter Agentenbefund

Stand: 11.09.2026 · Produktstand: `v1.0.193` · Prüfart: read-only Live-MCP-Audit

## Urteil

**Der Task ist nicht vollständig gemäß `tasks/usage-audit/Konzept.md` umgesetzt.** Die Grundarchitektur ist deutlich erkennbar und viele der wichtigsten Agentenabläufe funktionieren: stateless Source-Handoffs, strukturiertes Chaining, Scope-/Ranking bei Symbolsuche, Graph-Projektion, Budgetierung, feldgenaue Standardfehler und Source-/Assembly-Moduswechsel.

Es bleiben jedoch **neun unabhängige Major-Abweichungen** im heute veröffentlichten Verhalten. Sie verletzen insbesondere die harten Konzeptanforderungen für ID-Entduplizierung, eindeutige Status-/Leer-Semantik, gemeinsamen Scope, strikte Fehlerverträge und belastbare Budget-Hinweise. Damit ist die Abschlussbedingung („alle 12 Muss-Kriterien, 37 Akzeptanzkriterien … ohne offene auftragsbezogene Findings“) nicht erreicht.

Kein Code, keine Tests, keine Builds und keine Konfigurationsdateien wurden in diesem Audit verändert. Externe Testassemblies wurden nur read-only geprüft; alle Befunddateien anonymisieren deren Namen, Pfade und Symbole.

## Prüfgrundlage

Fünf getrennte Agentenperspektiven nutzten die echten MCP-Tools gegen die Solution sowie anonymisierte Assembly-Testfälle:

- [Health und Handshake](01-health-handshake.md)
- [Discovery](02-discovery-tools.md)
- [Symbol-Chaining](03-symbol-chaining.md)
- [Fehlerbehandlung](04-error-handling.md)
- [Cross-Tool-Konsistenz](05-cross-tool-consistency.md)

Der Konsolidierer reproduzierte die drei zentralen Source-Fälle zusätzlich selbst: `find_symbol → get_feature_context`, exakter Namespace-Drilldown und `get_test_context` mit dem Minimalbudget. Die Befunde sind damit Verhaltensbefunde, keine aus Code-Lektüre abgeleiteten Vermutungen.

## Offene Major-Abweichungen

| Nr. | Agentensicht | Belegter Ist-Zustand | Konzeptbezug |
|---|---|---|---|
| M1 | Handoff-ID leakt erneut in Markdown | `get_feature_context` gibt die kanonische `s:`-ID in `DocCommentId` im Text aus, zusätzlich zur zulässigen strukturierten Kopie. | Muss 2; AK 6, 8 |
| M2 | Exakter Namespace ist nicht zuverlässig verkettbar | Ein existierender Prefix liefert selbst bei maximalem Budget gleichzeitig `totalCount=0`, keine Entries und `truncated=maxResponseBytes`. | Muss 10, 11; AK 31, 35, 37 |
| M3 | Assembly-Teilvollständigkeit ist mehrdeutig | Fachpayload meldet `partial`, Navigation/Text je nach Tool aber `complete` oder `truncated`; Herkunft und Antwortvollständigkeit vermischen sich. | Muss 3, 10; AK 8, 31, 37 |
| M4 | Gemeinsamer Scope endet bei Composite-/Klassen-Tools | `get_class_structure`, `get_feature_context` und `get_test_context` bieten keinen konsistenten `scopeType`-/`includeGenerated`-Vertrag. Generierte Klassendateien können unmarkiert erscheinen. | Muss 5, 8, 10; AK 18–22, 31 |
| M5 | Budgetfehler sieht im Envelope wie Erfolg aus | `RESPONSE_BUDGET_TOO_SMALL` enthält zwar Code, Mindestwert und Feldpfad, aber zugleich `navigation.status.operation=ok` und `completeness=complete`. | Muss 5, 6, 10; AK 14, 15, 31 |
| M6 | Ein Teil der `find_symbol`-Validierung ist nicht feldgenau | Ungültiges `kind` und leere Elemente in `namePatterns` liefern `INVALID_ARGUMENT`, jedoch keinen stabilen `fieldPath`. | strikter Umsetzungsvertrag; AK 35, 36 |
| M7 | Ungültiges Assembly-`detailLevel` wird still übernommen | Assembly-Tools akzeptieren einen unbekannten Enumwert und führen die Analyse statt eines feldgenauen `INVALID_ARGUMENT` aus. | strikter Umsetzungsvertrag; AK 31, 35 |
| M8 | Assembly-Suche mit Referenzen ist mit Defaultbudget nicht nutzbar | `find_symbol(includeReferences=true)` scheitert beim Defaultbudget; der ausgegebene Mindestwert bleibt mehrfach zu klein und führt zu weiteren Fehlern. | Muss 6; AK 14–16, 37 |
| M9 | Assembly-Referenzscope ist nicht steuerbar | `find_references(includeReferences=false)` meldete und nutzte dennoch die Referenzsuche, identisch zum `true`-Fall. | Muss 8; AK 19, 37 |

## Weitere Qualitätsbefunde

- **Minor – wiederholte Status-/Folgetexte:** Begrenzte Feature- und Testkontexte nennen denselben Folgeschritt mehrfach. Das schmälert die geforderte semantische Dichte (Muss 4–6).
- **Minor – Discovery-Populationen:** File-Tree und Index-Scope messen unterschiedliche, technisch plausible Populationen. Die Benennung erklärt das Verhältnis aber nicht unmittelbar genug (AK 31).
- **Minor – zielgebundener Health-Text:** Er wiederholt globalen Daemon-Kontext, obwohl nur ein Ziel angefragt wurde; unnötiges Rauschen.
- **Minor – uneinheitlich knappe Assembly-Fehlerantworten:** Einzelne ungültige Optionen geraten in umfangreiche Analyseprojektionen statt in den ansonsten guten knappen Fehlervertrag.

Die in der Health-Prüfung festgestellten fehlenden `capabilities`-/Lint-Felder zähle ich **nicht** als Konzeptabweichung: Das Konzept fordert im normalisierten Envelope ausdrücklich das Entfernen tautologischer Capability-Felder. Es bleibt eine Abweichung vom älteren allgemeinen UX-Prüfkatalog, nicht vom Usage-Audit-Zielvertrag.

Ebenso zähle ich die fehlenden Methodennamen bei `directTypeUse` **nicht** als Major: Das Konzept erlaubt konkrete Methoden nur für die Evidenzstufen 0–2; `directTypeUse` ist die nachgelagerte Type-Use-Evidenz. Die Darstellung kann verbessert werden, verletzt den festgelegten Testevidenzvertrag aber nicht allein dadurch.

## Bestätigt umgesetzt

- Handoff-IDs aus `find_symbol`, Skeleton und Symbolbody haben das kompakte Source-Format mit zwei 22-Zeichen-Tokens und lassen sich ohne Text-Parsing weiterreichen.
- Source-Chaining `find_symbol → get_symbol_body → find_references → get_impact` funktioniert mit StructuredContent-IDs.
- `find_symbol` signalisiert Trunkierung ehrlich; Production wird bei gleicher Relevanz vor Tests gerankt, Scopes sind dort disjunkt und Generated ist im geprüften Pfad markiert.
- Call-Tree liefert eindeutige Nodes/Edges; ASCII, Mermaid und Structured Content nutzten im geprüften Fall dieselbe Menge. Kleinere Budgets kürzten vollständige Graph-Einheiten monoton.
- Standard-Target-, Typ-, Array- und Limitfehler sind überwiegend `INVALID_ARGUMENT` mit Feldpfad, Korrekturhinweis und ohne Stacktrace. Ein korrekter Folgecall nach Fehler blieb funktionsfähig.
- Assembly-Routen führen die getesteten Dateien nicht aus; nicht verwaltete Inputs werden recoverable abgelehnt. Source-/Assembly-Wechsel kontaminieren den Source-Snapshot im geprüften Ablauf nicht.

## Empfohlene Priorisierung für die Nacharbeit

1. **M1, M2, M3 und M5 zuerst:** Sie brechen zentrale, globale Vertragsinvarianten (ID-Platzierung, disjunkte Statuswerte, Namespace-Empty/Truncation, Fehler-vs.-Erfolg) und können Toolnutzer zu falschen Folgeentscheidungen führen.
2. **M4, M8 und M9 danach:** Diese blockieren reproduzierbare und kostengesteuerte Agentenabläufe im Composite- und Assembly-Kontext.
3. **M6 und M7 anschließend:** Sie schließen den strikten Schema-/Recovery-Vertrag. Danach die Minor-SNR-Befunde im selben Response-Review mitprüfen.

Nach jeder Korrektur sollten die jeweiligen Live-Dogfood-Sequenzen aus den fünf Einzelbefunden erneut laufen. Der Release-Gate aus dem Konzept ist erst sinnvoll, wenn kein Major-Befund offen ist.
