# MCP-Agent-UX-Audit: Gesamtbewertung

## Urteil

Der laufende MCP-Server ist im Normalfall erreichbar und seine Source-Ketten
sind überwiegend gut nutzbar. Die im Konzept beabsichtigte Härtung ist jedoch
aus Sicht eines konsumierenden Agenten **noch nicht insgesamt sinnvoll
abgeschlossen**: Drei reproduzierbare Critical-Befunde brechen zentrale
automatisierte Abläufe. Insbesondere darf ein Agent weder einen serverseitig
ausgegebenen Assembly-Referenztreffer zuverlässig weiterverfolgen noch jeden
frühen Validierungsfehler am MCP-Envelope erkennen.

Die fünf Teilberichte enthalten die sanitisierte, konkrete Reproduktion. Sie
sind gegen die bereits laufende Runtime erhoben; sie ersetzen keinen Nachweis
gegen einen frisch aus dem Arbeitsstand erzeugten Host.

## Priorisierte Befundlage

| Priorität | Befund | Wirkung für Agenten | Bericht |
|---|---|---|---|
| Critical | `get_index_scope` verweist mit `fileFilter` auf ein bei `search_pattern` nicht existentes Schemafeld. | Der strukturierte Non-C#-Handoff schlägt unmittelbar fehl. | Gruppe B, B-01 |
| Critical | Ein Referenztreffer aus Assembly-`find_symbol(includeReferences=true)` wird in den Folgewerkzeugen als `TARGET_MISMATCH` abgewiesen. | Die zentrale Assembly-Handoff-Kette ist nicht ausführbar. | Gruppe C, R2 |
| Critical | Frühvalidierung meldet `INVALID_ARGUMENT`, aber `isError=false` und keinen v2-Statusowner. | Clients können einen Fehler als erfolgreichen Tool-Call behandeln. | Gruppe D, D-01 |
| Major | Budget-Recovery liefert bei Symbolsuche trotz positiver Gesamtmenge einen erfolgreichen leeren Trefferblock; File Tree ignoriert sehr kleine Budgets. | Der angegebene Retrywert ist nicht zuverlässig handlungsfähig bzw. nicht budgettreu. | Gruppen B/C/E |
| Major | Assembly-`find_symbol` weist angefragte und effektive Referenzsuchbreite nicht aus. | Herkunft, Kosten und Verlässlichkeit eines Treffers bleiben unklar. | Gruppe C |
| Major | Target-Health fehlt eine maschinenlesbare Capability-/Lint-Entscheidung; globaler Health enthält darüber hinaus unnötige Einzel-Betriebsdaten. | Der Handshake gibt keine klare Tool-Routenentscheidung und erzeugt Rauschen. | Gruppe A |
| Major | Assembly-Health vermischt fachliche und Response-Completeness; Klassenstruktur-Budgetfehler besitzt keinen exakten Retry; Assembly-Fehler spiegeln den Zielpfad. | Statusauswertung, deterministisches Retry und Datensparsamkeit sind nicht durchgängig. | Gruppen A/D |
| Minor | Klassenstruktur liefert für Member keinen direkten Body-Handoff. | Clientseitige Identifikator-Konstruktion bleibt erforderlich. | Gruppe C |

Der Befund zum Symbol-Mindestbudget wurde unabhängig in Gruppe C und Gruppe E
reproduziert und ist in der Tabelle nur einmal gezählt.

## Gegen die Konzeptbereiche abgeglichen

| Konzeptbereich | Agentisches Urteil |
|---|---|
| R1 Budgetprojektion | Teilweise gelungen: Assembly-Discovery bietet exakte Mindestwerte und vollständige Einheiten. Für Symbolsuche, File Tree und Klassenstruktur ist der Recoveryvertrag noch verletzt. |
| R2 Assembly-Referenzscope und Handoffs | Teilweise gelungen: Root-/Owner-only-Signale und Closure-Diagnostics sind sichtbar. Der Referenz-Handoff selbst ist kritisch gebrochen; `find_symbol` zeigt seinen effektiven Scope nicht. |
| R3 Text und IDs | Im geprüften Scope gelungen: kanonische Handoff-IDs lagen strukturiert vor und waren nicht im regulären Text wiederholt. |
| R4 Target-Health | Teilweise gelungen: Ziel-Health enthielt keine PID-, Connection-, Daemon-, Uptime- oder Versionsdaten. Es fehlt jedoch die Capability-Entscheidung; globaler Health ist nicht ausreichend datensparsam. |
| R5 Populationen | Gelungen: physische Dateien, Roslyn-Dokumente, generierte/Test-Dokumente sowie Ausschluss-/Skip-Einheiten sind getrennt und verständlich ausgewiesen. |
| R6 Restbereinigung | Nicht per Runtime-Audit beurteilbar. Die gefundenen Contract-Brüche zeigen aber, dass der öffentliche Endzustand noch nicht erreicht ist. |

## Positiv bestätigte Eigenschaften

- Source-Symbolsuche, Symbol-Body, Feature-Kontext, Referenzen und Impact waren
  in einer realistischen Kette direkt verkettbar.
- Erfolgreiche zielgebundene Standardantworten enthielten strukturierte
  Navigation, getrennte Status- und Analyseachsen sowie sinnvolle
  Trunkierungs-Recovery.
- Die geprüften Assembly-Discovery-Tools lieferten bei zu kleinen Budgets einen
  ausführbaren Mindestwert; der nicht verwaltete Negativfall führte nicht zur
  Ausführung oder zu einem Stacktrace.
- Feldpfade, Typ- und Enumhinweise sind überwiegend präzise; ein korrekter
  Folgeaufruf nach einem Fehler funktionierte stabil.
- Der Wechsel Source → Assembly → Source zeigte in den geprüften Standardfällen
  keine beobachtbare Session-Kontamination.

## Grenzen und Durchführung

Alle Aufrufe waren read-only. Es wurden keine Produkt- oder Testdateien
geändert, kein Build und keine Tests ausgeführt. Externe Assemblyfälle wurden
nur lokal und read-only verwendet; die Teilberichte enthalten ausschließlich
die zugelassenen Labels und keine externen Namen, Pfade oder Rohantworten.

Die einzelnen Gruppenberichte:

- [Gruppe A – Health & Handshake](gruppe-a-health-handshake.md)
- [Gruppe B – Discovery](gruppe-b-discovery.md)
- [Gruppe C – Symbol-Chaining](gruppe-c-symbol-chaining.md)
- [Gruppe D – Fehlerbehandlung](gruppe-d-fehlerbehandlung.md)
- [Gruppe E – Cross-Tool-Konsistenz](gruppe-e-konsistenz.md)
