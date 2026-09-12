# Gruppe C – Symbol-Chaining und Assembly-Handoffs

## Ziel und Scope

Read-only-Agentenprüfung des laufenden MCP-Servers gegen die in `Konzept.md`
beschriebenen Restbefunde R1 bis R3. Geprüft wurden Source-Symbolketten,
Scope-/Generated-Parameter, Antwortbudgets, deterministische Wiederholung sowie
Assembly-Handoffs. Es wurden weder Produkt- oder Testcode geändert noch Build
oder Tests ausgeführt. Externe Prüffälle sind ausschließlich über die unten
verwendeten zulässigen Labels referenziert; deren Identitäten, Pfade und
Rohantworten sind nicht Teil dieses Berichts.

## Szenarien

| Bereich | Ausgeführte Agentenkette | Ergebnis |
|---|---|---|
| Source | `find_symbol(maxResults=1)` → kanonische ID → `get_symbol_body` | Body direkt erreichbar; Trunkierung, Gesamt-/Shown-Counts und Recovery sind strukturiert sichtbar. |
| Source | Fund-ID → `get_feature_context` → Caller-ID → `find_references` und `get_impact` | Kette funktionierte; Caller, Hand-off-Folgetools und Impact sind strukturiert. |
| Source | Dateiskelett → Typ → Klassenstruktur → Memberbody | Skelett liefert stabile IDs. Klassenstruktur benötigt jedoch die Zusammensetzung eines Positionsbezeichners, siehe Finding. |
| Source | Ungültiges `maxResults=0` → derselbe gültige Aufruf; zwei identische gültige Aufrufe | Feldpfad und Recovery waren konkret; der Serverzustand blieb nutzbar, die gültigen Antworten waren bytegleich strukturiert. |
| Source | `scopeType=all`, `includeGenerated=true` | Angeforderter Scope wird in StructuredContent gespiegelt und vor dem Ergebnislimit angewandt. |
| Assembly `LOCAL-01` | Root-Suche mit `includeReferences=false` → ID → Body, References, Impact | Owner-only-Modus, eine untersuchte Assembly und vollständige lokale Evidenz sind strukturiert ausgewiesen. |
| Assembly `LOCAL-01` | Referenztreffer aus `includeReferences=true` → ID → Body/References/Impact mit `false` und `true` | Referenz-Handoff wird trotz gleichem Target abgewiesen, siehe Critical Finding. |
| Assembly `LOCAL-02` | Symbolsuche mit `includeReferences=false` und `true` | Beide Wege laufen, doch der effektive Suchmodus ist im Ergebnis nicht maschinenlesbar sichtbar, siehe Finding. |
| Assembly `FALSE-01` | Symbol- und Referenzaufruf | Strukturierter, nicht ausführender Negativfall mit klarer Ursache und Handlungshinweis. |

## Positive Nachweise

- **Contract v2 / Navigation:** Alle erfolgreichen zielgebundenen Source- und
  Assemblyaufrufe enthielten `structuredContent.navigation` mit getrenntem
  `status.operation`, `status.completeness`, Target-Herkunft und Snapshot.
- **R2 Root-Owner-only:** Für einen Root-Handoff von `LOCAL-01` zeigte
  `includeReferences=false` explizit `symbol_owner_only`, eine untersuchte
  Assembly, keine Diagnostics und vollständige Call-Site-Evidenz. Mit `true`
  wurden Closure, Anzahl untersuchter Assemblies, partielle Diagnostics und
  deren Trunkierung ausgewiesen.
- **R3 ID-Signal:** Die geprüften regulären Textantworten der Source- und
  Assembly-Suche sowie des Feature-Kontexts enthielten keine kanonische
  Handoff-ID. Die IDs lagen im StructuredContent und konnten dort direkt
  übernommen werden.
- **SNR / Completeness:** Source-`find_symbol(maxResults=1)` signalisiert
  Trunkierung mit Gesamtzahl, Rückgabezahl und einer strukturierten Recovery.
  Die direkte Body-Abfrage liefert Verfügbarkeit, Darstellungsmodus und
  Zeilenfenster statt Dateioverhead.

## Findings

### [Critical] R2 – Referenz-Handoff aus der Suchantwort ist nicht verkettbar

**Betroffen:** `find_symbol`, `get_symbol_body`, `find_references`, `get_impact`

Sanitisierte Evidenz:

```text
LOCAL-01: find_symbol(includeReferences=true) liefert einen Referenztreffer mit kanonischer ID.
Direkte Folgeaufrufe mit derselben ID und demselben Target schlagen bei false wie true fehl.
Code: TARGET_MISMATCH; Hinweis fordert fälschlich eine ID aus demselben Target.
```

Ein Agent kann einen vom Server selbst ausgegebenen Referenztreffer nicht zu
Body, References oder Impact weiterverfolgen. Das verletzt den R2-Vertrag für
explizite Owner-Handoffs und blockiert die zentrale Assembly-Kette. Der Fehler
ist nicht recoverable markiert, obwohl die vorgeschlagene Korrektur bereits
erfüllt ist. **Empfehlung:** Handoff-Owner aus dem signierten ID-Envelope gegen
das Root-Target auflösen und bei `false` ausschließlich diesen Owner öffnen;
bei `true` dessen bounded Closure. Target-Mismatch nur für tatsächlich fremde
Targets verwenden.

### [Major] R1 – Budget-„Mindestwert“ liefert eine leere Erfolgsprojektion

**Betroffen:** `find_symbol`

Sanitisierte Evidenz:

```text
Source: Budget 512 meldet Mindestwert 1132 mit Feldpfad und Retry-Hinweis.
Exakter Retry mit 1132: operation=ok, completeness=truncated, 0 Treffer bei positiver Gesamtmenge.
LOCAL-01: beim niedrigsten akzeptierten Assemblybudget ebenfalls 0 Treffer bei positiver Gesamtmenge.
```

Der exakte Retry ist technisch erfolgreich, enthält aber keine vollständige
fachliche Einheit und damit keine verwertbare Symbol-ID. Ein Agent kann weder
arbeiten noch die nächstnötige Budgethöhe ableiten. Dies erfüllt die R1-Idee
einer ausführbaren Mindestprojektion nicht und erzeugt Silent-Empty unter
`operation=ok`. **Empfehlung:** Wenn kein kompletter Symbol-Entry plus
Navigation passt, `RESPONSE_BUDGET_TOO_SMALL` mit einem Retrywert für mindestens
einen vollständigen Entry liefern; den exakten Retry gegen das finale Wirebudget
prüfen. Die Fehlerantwort sollte als recoverable klassifiziert sein.

### [Major] R2 / Contract v2 – Assembly-Suche spiegelt Referenz-Scope nicht

**Betroffen:** `find_symbol`

Sanitisierte Evidenz:

```text
LOCAL-02: false und true liefern unterschiedlich große Ergebnispopulationen.
Beide Resultate enthalten scopeType, aber weder requestedIncludeReferences
noch effectiveSearchMode, untersuchte Assemblyzahlen oder Diagnostic-Counts.
```

Aus einer gespeicherten oder weitergereichten Suchantwort kann ein Agent nicht
feststellen, ob sie root-only oder aus einer bounded Closure stammt. Das macht
Kosten, Herkunft, Vollständigkeit und die Gültigkeit eines folgenden Handoffs
nicht zuverlässig bewertbar. **Empfehlung:** Für Assembly-`find_symbol` den
angefragten Referenz-Schalter, effektiven Suchmodus, Assembly-Counts und
partielle Diagnostics analog zu `find_references`/`get_impact` in
StructuredContent ausweisen.

### [Minor] Chaining – Klassenstruktur enthält keine direkt konsumierbare Member-ID

**Betroffen:** `get_class_structure`, `get_symbol_body`

Sanitisierte Evidenz:

```text
Die Klassenstruktur liefert Membername, relativen Dateipfad und Startzeile, aber kein id/handoffKind.
Ein Body kann nur nach clientseitigem Zusammensetzen eines Positionsbezeichners gelesen werden.
```

Der Fallback funktioniert bei eindeutiger Zeile, aber ein Agent muss eigene
String-Transformation und bei Mehrdeutigkeit zusätzliche Auflösung
implementieren. Das ist schwächer als die ansonsten verwendeten direkten
StructuredContent-Handoffs. **Empfehlung:** Pro Member die kanonische ID und
`handoffKind` ergänzen oder ein ausdrücklich strukturiertes
`bodyIdentifier`-Feld anbieten.

## Freie Beobachtungen

- Die Source-Suche blieb nach einem Validierungsfehler deterministisch und
  lieferte anschließend ohne Session-Verschmutzung wieder denselben Snapshot.
- Die Assembly-Reference-Closure von `LOCAL-01` meldete vorhandene lokale
  Call-Sites trotz partieller Decompilerdiagnostik; das ist gegenüber
  Silent-Empty die richtige Richtung. Die Diagnostics sind gezählt und ihre
  Trunkierung erkennbar.
- `FALSE-01` wird ohne Ausführung als nicht verwaltetes Ziel zurückgewiesen.
  Fehlercode, Ursache und Handlungsalternative sind ausreichend konkret.
- Der laufende Server belegt die oben genannten Live-Eigenschaften. Ob seine
  Implementation exakt dem aktuellsten Repository-Commit entspricht, wurde
  gemäß Auftrag nicht durch Build oder Testhost verifiziert.

## Grenzen

Keine Builds, Tests, Codeänderungen, Prozessausführungen oder Cache-/Neustart-
Versuche. Damit sind insbesondere R1-Monotonie über mehrere Budgets sowie die
R2-Lebenszeit eines Handoffs nach Eviction/Serverneustart nicht ausreichend
nachweisbar. Die dokumentierten Live-Gegenbefunde sind jedoch direkt über die
betroffenen MCP-Toolketten reproduzierbar.
