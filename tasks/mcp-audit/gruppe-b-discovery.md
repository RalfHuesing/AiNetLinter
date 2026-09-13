# MCP-Audit – Gruppe B: Discovery, Scope & Assembly-Inspektion

> **Zuständigkeit:** Subagent B
> **Tools:** `get_file_tree`, `get_namespace_tree`, `get_index_scope`, `inspect_assembly`, `search_assembly`, `find_assembly_extensions`, `get_assembly_context`
> **Regeln:** Nur negative Befunde protokollieren. Keine echten Produktnamen/Pfade, nur Ziel-Labels (`SOURCE-01`, `LOCAL-01`, etc.)!

---

## 1. Übersicht & Testergebnis

- **Geprüfte Tools:** `get_file_tree`, `get_namespace_tree`, `get_index_scope`, `inspect_assembly`, `search_assembly`, `find_assembly_extensions`, `get_assembly_context`
- **Geprüfte Prüffälle:** TC-B01 bis TC-B10 aus `FlightPlan.md`
- **Gefundene Befunde:** 0 Critical, 4 Major, 0 Minor

---

## 2. Negative Befunde

### [Major] B-03: `inspect_assembly` meldet Budget-Untergrenze als `INVALID_ARGUMENT`

- **Betroffenes Tool / Schema**: `inspect_assembly` (Parameter: `maxResponseBytes`)
- **Ziel-Label**: `LOCAL-01` (Modus: Assembly)
- **Konkreter Aufruf**: `inspect_assembly(targetPath="<LOCAL-01>", maxResponseBytes=200)`
- **Beobachtung / Ist-Verhalten**:
  - Antwort: `INVALID_ARGUMENT: maxResponseBytes muss mindestens 2048 Bytes betragen …`; Hinweis auf Default-Budget 16384.
  - Kein Fehlercode `RESPONSE_BUDGET_TOO_SMALL`, kein maschinenlesbares `minimumResponseBytes`.
  - Folgeaufruf mit `maxResponseBytes=2048` liefert Nutzinhalt (1 Typ) plus `continuationToken`.
- **Soll-Verhalten / Problem aus Agentensicht**:
  - Der dokumentierte Retry-Vertrag (`RESPONSE_BUDGET_TOO_SMALL` + `minimumResponseBytes`) greift nicht. Ein Agent muss deutschen Fließtext parsen, um 2048 zu erraten.
- **Empfehlung**:
  - Unter der Mindestgröße denselben Budget-Envelope wie bei Overflow nutzen: Code `RESPONSE_BUDGET_TOO_SMALL`, Feld `minimumResponseBytes=2048` (oder den tatsächlich benötigten Wert).

### [Major] B-04: `get_assembly_context` liefert Übersicht ohne Verweise und Zielframework

- **Betroffenes Tool / Schema**: `get_assembly_context` (Parameter: `includeReferences`, Default-Übersicht ohne `symbolIdentifier`)
- **Ziel-Label**: `LOCAL-01` (Modus: Assembly)
- **Konkreter Aufruf**: `get_assembly_context(targetPath="<LOCAL-01>", includeReferences=true, includeMetrics=true)` sowie Varianten mit `includeMetrics=false`, `detailLevel="compact"|"standard"`, `maxResponseBytes=2048|8192|16384`
- **Beobachtung / Ist-Verhalten**:
  - Nutzlast faktisch nur: Envelope plus `Assembly-Kontext: 48 von 48` / `Scope: root+references; Vollständigkeit: partial`.
  - Keine Assembly-Identität, kein Zielframework/TFM, keine Referenzliste, keine Typübersicht – trotz `includeReferences=true` und ausreichendem Budget.
  - Mit `includeBody=true` und `includeClassStructure=true` plus `symbolIdentifier` erscheinen Body/Struktur; die Übersichts-Metadaten (Verweise, TFM) fehlen weiterhin.
- **Soll-Verhalten / Problem aus Agentensicht**:
  - Composite-Einstieg soll Verweise, Zielframework und Metadaten strukturiert liefern. `48 von 48` ohne Einträge ist ein leeres Erfolgs-Envelope und zwingt zum Ausweichen auf `inspect_assembly`.
- **Empfehlung**:
  - Ohne Symbol mindestens Identität, TFM, Public-Namespaces und Referenzliste (gekürzt + Continuation) serialisieren; Zähler nur zusammen mit den zugehörigen Einträgen.

### [Major] B-05: `includeMetrics` auf Assembly ergibt irreführendes Solution-`NOT_CONFIGURED`

- **Betroffenes Tool / Schema**: `get_assembly_context` (Parameter: `includeMetrics`)
- **Ziel-Label**: `LOCAL-01` (Modus: Assembly)
- **Konkreter Aufruf**: `get_assembly_context(targetPath="<LOCAL-01>", symbolIdentifier="<Handoff-ID>", includeMetrics=true, includeReferences=true)`
- **Beobachtung / Ist-Verhalten**:
  - Abschnitt `metrics`: `NOT_CONFIGURED: Diese Operation ist fuer die Solution nicht konfiguriert: neben der Solution wurde keine ainetlinter-rules.json gefunden.`
  - Hint fordert, `ainetlinter-rules.json` neben der adressierten Solution anzulegen – Ziel ist jedoch eine Assembly, keine Solution.
- **Soll-Verhalten / Problem aus Agentensicht**:
  - Irreführender Fehler: Agent sucht eine nicht existente Solution/Rules-Datei am falschen Artefakt. Metriken sind im Decompiled-Modus erwartbar `unsupported`, nicht „fehlende Solution-Konfiguration“.
- **Empfehlung**:
  - Assembly-Modus: `unsupported` / `origin=decompiled` für Metriken; kein Solution-Rules-Hint.

### [Major] B-06: `search_assembly`-Treffer ohne Handoff-IDs

- **Betroffenes Tool / Schema**: `search_assembly` (Parameter: `pattern`, `kind`)
- **Ziel-Label**: `LOCAL-01` (Modus: Assembly)
- **Konkreter Aufruf**: `search_assembly(targetPath="<LOCAL-01>", pattern="<MyClass>", kind="type", declarationOnly=true)` und `search_assembly(targetPath="<LOCAL-01>", pattern="<MyMethod>", kind="method")`
- **Beobachtung / Ist-Verhalten**:
  - Pattern stammt aus `inspect_assembly`-Handoff (nicht geraten). Treffer strukturiert als `MyNamespace/MyClass.cs:<Zeile>: <Snippet>`.
  - Keine `handoffId` / keine kanonische Symbol-ID am Match. Status `Assembly-Suche: text; n von n`, `completeness=complete`.
- **Soll-Verhalten / Problem aus Agentensicht**:
  - TC-B08 und die Kette Suche → `get_symbol_body` verlangen konsumierbare Handoff-IDs. Ohne ID muss der Agent Pfad/Zeile oder Typnamen selbst zusammenbauen.
- **Empfehlung**:
  - Pro Match dieselbe `handoffId` wie bei `inspect_assembly` ausgeben (Typ- bzw. Member-ID), nicht nur Datei:Zeile.
