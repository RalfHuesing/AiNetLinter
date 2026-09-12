# MCP-Agent-UX-Audit – Gruppe E: Cross-Tool-Konsistenz

## Ziel und Scope

Prüfung des laufenden MCP-Servers aus Sicht eines konsumierenden Agenten. Getestet wurden kleine, reale Source- und Assembly-Aufrufe, Contract-v2-Navigation, Handoff-Ketten, Budget-Recovery sowie ein Source → Assembly → Source-Wechsel. Die externen read-only Fälle sind ausschließlich als `LOCAL-01` und `FALSE-01` bezeichnet. Es wurden weder Code geändert noch Build oder Tests ausgeführt.

## Vergleichsmatrix

| Szenario | Ergebnis für Agenten | Bewertung |
|---|---|---|
| Source: Health, File Tree, Index Scope, Symbolsuche, Symbol-Body | Einheitliche `navigation` mit Target, Snapshot, Statusowner, Analyseachse und Next; `complete`, `truncated`, `empty` und `not_applicable` sind sauber getrennt. | Positiv |
| Source: Symbolsuche → Symbol-Body | Die einzige strukturierte Handoff-ID wurde direkt übernommen; der Body-Aufruf war erfolgreich. Im Text erschien diese ID nicht. | Positiv (R3) |
| `LOCAL-01`: Symbolsuche → Symbol-Body | Assembly-Handoff war direkt nutzbar; `navigation.target.origin=assembly` und `analysis.mode=decompiled` blieben in beiden Aufrufen konsistent. | Positiv |
| `LOCAL-01`: Source-only-Tool | Strukturierter, recoverable-fähiger `ASSEMBLY_TARGET_UNSUPPORTED`-Fehler statt Silent-Empty oder Stack Trace. | Positiv |
| `FALSE-01`: Assembly-Inspektion | Strukturierter `INVALID_ASSEMBLY`-Fehler mit konkretem nächsten Schritt, ohne Stack Trace. | Positiv |
| Source → `LOCAL-01` → Source | Die zwei äquivalenten Source-Suchen hatten gleiche Counts, Trunkierungsgründe und Source-Navigation. Keine beobachtbare Modus-Kontamination. | Positiv (R2) |
| Source: `find_symbol` bei kleinem Budget | Fehler liefert Feldpfad, angefordertes Budget und Mindestwert; der Retry mit genau diesem Wert liefert jedoch eine leere Trefferliste trotz bekannter Treffer. | Major (R1) |

## Positive Nachweise

- **Parameter-Naming:** `pattern`/`namePatterns` bezeichnet die Suchanfrage, während `symbolIdentifier` beziehungsweise `symbolIdentifiers` die konkrete Adressierung und Batch-Form beschreiben. Die Schema-Beschreibungen erklären diese Abgrenzung; die getestete Übergabe der ID funktionierte ohne Parsing.
- **Statusowner und Vollständigkeit:** Alle geprüften zielgebundenen Antworten hatten genau `navigation.status` mit `operation`, `completeness` und Fehlercode. Fachliche Analysequalität blieb getrennt unter `navigation.analysis`.
- **R3 – Text-ID-Verbot:** Für je einen Source- und `LOCAL-01`-Handoff war genau eine strukturierte `id` vorhanden und keine dieser IDs im regulären Text wiederholt.
- **R4 – Target-Health:** Der zielgebundene Health-Call enthielt keine PID-, Connection-, Daemon-, Uptime- oder Versionsangaben. Globale Betriebsdaten blieben beim globalen Health-Call.
- **R5 – Populationen:** Index Scope benennt `physicalFileCount`, `roslynDocumentCount`, `generatedDocumentCount` und `testDocumentCount`; File Tree trennt physische Dateien von ausgeschlossenen Dateien und übersprungenen Verzeichnissen. Die Textdarstellung nennt die jeweilige Einheit.
- **Capabilities/Route:** Statt einer losgelösten Capability-Behauptung wurden Source- und Assembly-Routen durch echte Tool-Aufrufe verifiziert. Nicht unterstützte Assembly-Nutzung war explizit, nicht als leerer Erfolg, markiert.

## Findings

### [Major] E-01: Gemeldetes Mindestbudget führt zu semantisch leerem Symbol-Retry

- **Tool:** `find_symbol`
- **Betroffener Vertrag:** R1 Budget-Recovery und Mindestprojektion
- **Minimale Evidenz:**
  1. Source-Suche mit einem Treffer-Muster, `maxResults=1`, Budget `512` → `RESPONSE_BUDGET_TOO_SMALL`, Feldpfad `$.maxResponseBytes`, Mindestwert `1132`.
  2. Identischer Retry mit `maxResponseBytes=1132` → `operation=ok`, `completeness=truncated`.
  3. Gleichzeitig: `totalCount=221`, `returnedCount=0`, Matchliste leer; Text behauptet „Keine Treffer“.
- **Problem aus Agentensicht:** Der vorgeschlagene Recovery-Wert ist zwar technisch ausführbar, aber nicht fachlich handlungsfähig. Ein Agent darf bei einer erfolgreichen Suche mit „keine Treffer“ keine Suche fortsetzen, obwohl dieselbe Antwort 221 Treffer signalisiert. Das verletzt die zugesagte Mindestprojektion und macht die Retry-Konvention unzuverlässig.
- **Reproduktion:** Genau den Fehler-Retry mit unverändertem Target, Muster und `maxResults` wiederholen; nur das gemeldete Mindestbudget einsetzen.
- **Empfehlung:** Den Mindestwert auf die kleinste vollständige, sichtbare Symbol-Einheit inklusive finaler Navigation berechnen. Falls selbst diese Einheit nicht passt, weiterhin den Budgetfehler statt eines `ok`-Empty mit widersprüchlichen Counts liefern.

## Freie Beobachtungen

- Die Navigation unterscheidet beim Assembly-Fall sinnvoll zwischen Zielart (`origin=assembly`) und Analyseweg (`mode=decompiled`). Diese Aufteilung blieb konsistent; Agenten sollten beide Felder auswerten.
- Bei vollständigen Antworten ist `next` teilweise `null`, bei File Tree dagegen ein strukturierter `none`-Hinweis. Das war in den geprüften Calls nicht störend, aber ein Client sollte `next` als optional behandeln.

## Grenzen

- Geprüft wurde ausschließlich der bereits laufende Server, nicht ein frisch gebauter Prozesshost.
- Die Assembly-Prüfung blieb read-only und nutzt im Bericht keine externen Namen, Pfade oder Rohantworten.
- Keine Builds, Tests oder Codeänderungen; die Bewertung beruht auf den hier dokumentierten MCP-Interaktionen.
