# MCP-Audit – Gruppe D: Codequalität, Linter, Metriken & Verify

> **Zuständigkeit:** Subagent D
> **Tools:** `verify`, `get_hotspots`, `find_duplicates`, `pattern_detect`, `metrics_tree`, `metrics_lookup`, `dependency_graph`, `search_pattern`
> **Regeln:** Nur negative Befunde protokollieren. Keine echten Produktnamen/Pfade, nur Ziel-Labels (`SOURCE-01`, `LOCAL-01`, etc.)!

---

## 1. Übersicht & Testergebnis

- **Geprüfte Tools:** `verify`, `get_hotspots`, `find_duplicates`, `pattern_detect`, `metrics_tree`, `metrics_lookup`, `dependency_graph`, `search_pattern` (kontextuell `get_feature_context` / `get_impact`; Live-Schema ohne Alt-Namen)
- **Geprüfte Prüffälle:** TC-D01 bis TC-D10
- **Schema-Check Alt-Tools:** `safeguard`, `get_violations`, `find_dead_code`, `find_magic_values` und `minLines` sind im Live-Katalog **nicht** gelistet (kein Befund).
- **Gefundene Befunde:** 0 Critical, 3 Major, 1 Minor

Kurz positiv (kein Befund): `verify` auf `LOCAL-01`/`FALSE-01` recoverable `unsupported` ohne Crash und ohne `pass`; `get_hotspots` Assembly-Modus klar; `find_duplicates` respektiert `minTokens`/`mode`; `pattern_detect` gruppiert Heuristiken; `metrics_lookup` konsumiert `h:…`; `dependency_graph` akzeptiert `symbolIdentifier` aus Handoff; kontextuelle Violations ohne Gate-Verdict.

---

## 2. Negative Befunde

### [Major] D-01: `verify`-Gate nicht ohne Freitext-Parsing auswertbar

- **Betroffenes Tool / Schema**: `verify` (Parameter: `scope`)
- **Ziel-Label**: `SOURCE-01` (Modus: Source); Formvergleich `SOURCE-02`
- **Konkreter Aufruf**: `verify(targetPath=<SOURCE-01>)`; `verify(targetPath=<SOURCE-01>, scope=solution)`
- **Beobachtung / Ist-Verhalten**:
  - Default (`changes`): kompakte Zeilen `verdict: incomplete`, `completeness: incomplete`, `reason: emptychangecontext`, `recovery: scope: solution verwenden.` — **kein** eigenes `score`, **kein** `violationCount`.
  - `scope=solution`: `verdict: failed`, `completeness: complete`, zusammengesetzte Zeile `gate: score=8.9; violations=1`, danach `findings: count=1/1`. Advisory-Kandidaten sind nicht vom Gate getrennt maschinenlesbar ausgewiesen; die Felder heißen nicht `score` / `violationCount` als Geschwister von `verdict`.
  - `SOURCE-02` / `scope=solution`: anderes Incomplete-Profil (`reason: notconfigured`) ebenfalls ohne Gate-Tripel.
  - Kein JSON-Objekt mit den dokumentierten Keys; Agent muss `gate:`-Freitext und abweichende Verdict-Tokens (`failed` vs. dokumentierte Prüfung auf `pass`) interpretieren.
- **Soll-Verhalten / Problem aus Agentensicht**:
  - Gate-Regel ist `pass` nur bei `verdict=pass`, `score=10.0`, `violationCount=0`. Ohne stabile, getrennte Felder kann ein Agent das Tripel nicht ohne Parsing der Content-Prosa prüfen. Ein hypothetisches `verdict=pass` bei Score &lt; 10 wäre so nicht hart abweisbar.
  - Target-Fail (`failed`, Score &lt; 10) ist **kein** Server-Befund; der Kontraktbruch ist die fehlende maschinenlesbare Gate-Form.
- **Empfehlung**:
  - Im Content (oder Envelope) feste Keys `verdict`, `score`, `violationCount` als eigene Zeilen/Felder liefern; `gate:`-Sammelzeile höchstens zusätzlich. Incomplete-Fälle explizit `isGateResult=false` (oder weglassen der Score-Keys mit klarem `verdict=incomplete`).

### [Major] D-02: `search_pattern` verletzt Budget-Retry-Vertrag

- **Betroffenes Tool / Schema**: `search_pattern` (Parameter: `maxResponseBytes`)
- **Ziel-Label**: `SOURCE-01` (Modus: Source)
- **Konkreter Aufruf**: `search_pattern(targetPath=<SOURCE-01>, pattern=…, maxResponseBytes=200)`; Vergleich `maxResponseBytes=500`
- **Beobachtung / Ist-Verhalten**:
  - Bei 200 Bytes: Meta-Zeilen „2451 Treffer gesamt, 0 gezeigt“ und „Antwort wegen maxResponseBytes begrenzt“ — **kein** `RESPONSE_BUDGET_TOO_SMALL`, **kein** `minimumResponseBytes`, **keine** Evidenzzeile.
  - Bei 500 Bytes: 1 Treffer gezeigt, Rest abgeschnitten; erneut nur Prosa-Hinweis, kein Retry-Wert.
  - Fehlendes `pattern`: korrektes `[ERROR]: INVALID_ARGUMENT` mit Hint (kein Befund).
- **Soll-Verhalten / Problem aus Agentensicht**:
  - Budgetbruch: Agent kann nicht deterministisch mit `maxResponseBytes = minimumResponseBytes` wiederholen. 0 gezeigte Treffer bei bekanntem Totaltreffer ist eine leere Nutzlast (Hülle ohne Einheit).
- **Empfehlung**:
  - Bei Unterschreitung: `RESPONSE_BUDGET_TOO_SMALL` plus deterministisches `minimumResponseBytes`, das beim Retry mindestens eine Treffereinheit liefert; nicht still auf 0 Treffer kollabieren.

### [Major] D-03: `find_duplicates` `scopeType=production` mixcht Test-Cluster in die Top-Treffer

- **Betroffenes Tool / Schema**: `find_duplicates` (Parameter: `scopeType`)
- **Ziel-Label**: `SOURCE-01` (Modus: Source)
- **Konkreter Aufruf**: `find_duplicates(targetPath=<SOURCE-01>, minTokens=40, mode=clone, scopeType=production)` vs. `scopeType=tests`
- **Beobachtung / Ist-Verhalten**:
  - Schema-Default und explizites `production`: 80 Cluster / 3503 Methoden; die **Top-Cluster** enthalten dennoch Test-Projekt-Pfade.
  - `scopeType=tests`: 374 Cluster / 4418 Methoden, andere Top-Menge — der Filter existiert, `production` schließt Tests aber nicht aus der Rangliste aus.
  - `mode=structural` ohne gesetztes `scopeType` zeigt ebenfalls Test-Support in den Top-3. `minTokens`/`mode` werden ansonsten respektiert; Treffer sind als `candidate` gekennzeichnet.
- **Soll-Verhalten / Problem aus Agentensicht**:
  - Dokumentierter Default `production` führt zu Signal-Rausch: Agent hält Test-Klone für Produktionsfunde. Das ist ein Server-Kontrakt-/Filterproblem, kein Urteil über den Zielcode.
- **Empfehlung**:
  - `production` strikt auf Nicht-Test-Kompilationseinheiten begrenzen oder effektiv angewandten `scopeType` plus Zählung (production vs. tests) maschinenlesbar in der Kopfzeile ausweisen, wenn gemischt.

### [Minor] D-05: Qualitäts-Trefferlisten ohne konsumierbare `h:…`

- **Betroffenes Tool / Schema**: `get_hotspots`, `find_duplicates`, `pattern_detect`, `metrics_tree`, `dependency_graph`, `search_pattern` (Parameter: `enrichCSharp`)
- **Ziel-Label**: `SOURCE-01` (Modus: Source)
- **Konkreter Aufruf**: jeweilige Default-/Schema-Aufrufe auf `<SOURCE-01>`; `search_pattern(..., enrichCSharp=true)`
- **Beobachtung / Ist-Verhalten**:
  - Treffer sind Pfad-/FQ-Name-Zeilen. Keine `h:…` in Hotspot-Tabelle, Duplikat-Clustern, Pattern-Hits, Metrik-Baum, Graph-Kanten.
  - `enrichCSharp=true` ergänzte keine Symbol-IDs/`h:…`.
  - `metrics_lookup(symbolIdentifiers=["h:…"])` und `dependency_graph(symbolIdentifier="h:…")` **konsumieren** Handoffs aus `find_symbol` korrekt (kein zusätzlicher Major).
- **Soll-Verhalten / Problem aus Agentensicht**:
  - Folgeaufrufe (`metrics_lookup`, `helperSymbol`, `get_symbol_body`) erfordern manuelles Kopieren von Pfaden/Namen statt unveränderter `h:…`. Ergonomie, kein Crash.
- **Empfehlung**:
  - Pro Trefferzeile eine `h:…` ausgeben (Datei oder Symbol); `enrichCSharp=true` muss Symbol-Evidenz plus Handoff liefern oder den Flag-Effekt klar als „nicht anwendbar“ markieren.

---

## 3. Prüffall-Abgleich (ohne Target-Urteil)

| TC | Ergebnis für den Server |
|---|---|
| TC-D01 | Envelope kompakt, kein Crash, kein falsches `pass`; Gate-Tripel fehlt (D-01). |
| TC-D02 | `scope=solution` Abschlussform mit `verdict`/`gate`; Semantik Fail am Target erwartbar; Maschinenlesbarkeit D-01. |
| TC-D03 | `LOCAL-01` und `FALSE-01`: `verdict: error`, `code: ASSEMBLY_TARGET_UNSUPPORTED`, Recovery auf Source-Ziel; kein Crash, kein `pass`. JSON-RPC-`isError` in der sichtbaren Tool-Antwort nicht belegt. |
| TC-D04 | Source: MaxLineCount-Tabelle nachvollziehbar. `LOCAL-01`: `[ASSEMBLY] capability=unsupported` plus recoverable Fehler, kein Crash. |
| TC-D05 | `minTokens`/`mode` (`clone`, `structural`, `refactoring-drift`) greifen; kein `minLines` im Schema. Filter-Mangel D-03. |
| TC-D06 | Gruppierte Heuristiken (god-class, async-void, long-method, empty-catch); `not_configured`/`not_decidable` ohne globalen Clean-Claim. |
| TC-D07 | Baum hierarchisch; Lookup konsumiert `h:…`. Emission von `h:…` im Baum fehlt (D-05). |
| TC-D08 | `symbolIdentifier` aus Handoff akzeptiert; Kanten ohne `h:…` (D-05). |
| TC-D09 | Schema-Suche funktioniert; leeres Pattern recoverable. Budgetvertrag D-02. |
| TC-D10 | `get_feature_context` / `get_impact`: Arbeitsevidenz, Violations-Abschnitt ohne `verdict`/`score`. Schema bewirbt nicht `safeguard`/`get_violations`. |
