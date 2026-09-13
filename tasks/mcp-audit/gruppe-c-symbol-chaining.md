# MCP-Audit – Gruppe C: Semantische Symbol-Tools & Chaining-Ketten

> **Zuständigkeit:** Subagent C
> **Tools:** `find_symbol`, `get_symbol_body`, `get_file_skeleton`, `get_class_structure`, `find_references`, `get_call_tree`, `get_impact`, `get_type_hierarchy`, `find_implementations`, `resolve_type_origin`, `get_feature_context`, `get_test_context`; für CHAIN-04 zusätzlich `inspect_assembly`, `search_assembly`
> **Regeln:** Nur negative Befunde gegen den MCP-Server. Nur Ziel-Labels. Keine Produktnamen, Pfade oder dekompilierten Bezeichner.

---

## 1. Übersicht & Testergebnis

- **Geprüfte Tools:** TC-C01 bis TC-C13 plus FALSE-01 (`find_symbol`) und CHAIN-01 bis CHAIN-04
- **Geprüfte Chaining-Sequenzen:**
  - CHAIN-01 (`SOURCE-01`): `find_symbol` → `get_symbol_body` → `find_references` → `get_impact` (optional `get_feature_context`) — **innerhalb einer Index-Generation durchreichbar**
  - CHAIN-02 (`SOURCE-01`): `find_symbol(kind=class)` → `get_class_structure` → Member-`h:…` → `get_symbol_body` — **ohne String-Bau**
  - CHAIN-03 (`SOURCE-01`): `get_type_hierarchy` → Interface-`h:…` → `find_implementations` → `resolve_type_origin` — **qualifizierte Namen konsistent**
  - CHAIN-03 (`LOCAL-01`): Hierarchie-Basis ohne konsumierbare `h:…` — **Bruch**
  - CHAIN-04 (`LOCAL-01`): `inspect_assembly` → `search_assembly` → `get_symbol_body` — **Bruch** (Direktpfad `inspect_assembly`-`h:…` → `get_symbol_body` funktioniert)
- **FALSE-01:** recoverable `INVALID_ASSEMBLY`, kein Crash
- **Gefundene Befunde:** 0 Critical, 6 Major, 2 Minor

---

## 2. Negative Befunde

### [Major] C-03: `get_file_skeleton` akzeptiert keine Datei-Handoff-ID

- **Betroffenes Tool / Schema**: `get_file_skeleton` (Parameter: `filePaths`)
- **Ziel-Label**: `SOURCE-01` (Modus: Source)
- **Konkreter Aufruf**: `get_file_skeleton(targetPath=<SOURCE-01>, filePaths=["h:…"])` mit Klassen-ID aus `find_symbol` / `get_symbol_body`
- **Beobachtung / Ist-Verhalten**:
  - `RESOURCE_NOT_FOUND: Datei 'h:…' nicht in der Solution gefunden.`
  - Derselbe Fehler mit einer frischen Klassen-`h:…` nach Reload (kein Stale-Effekt).
  - Erfolg nur nach **manuellem Kopieren** des relativen Dateipfads aus dem Markdown des Vorgängertools.
  - Auf `LOCAL-01` analog: Dateiname aus `get_class_structure` (`Files:`) muss als Pfadstring übergeben werden; funktioniert, ist aber kein `h:…`-Handoff.
- **Soll-Verhalten / Problem aus Agentensicht**:
  - Workflow-Kontrakt: Folgeaufrufe ausschließlich mit kanonischen `h:…`. `get_file_skeleton` zwingt zum Parsen eines Pfads und behandelt `h:…` wie einen fehlenden Dateinamen.
- **Empfehlung**:
  - `filePaths` soll `h:…` (Datei oder enthaltenes Symbol) unverändert akzeptieren und auf das deklarierende Dokument mappen.
  - Alternativ eigene Datei-`h:…` in `find_symbol` / `get_symbol_body` ausweisen.

### [Major] C-04: Referenz-/Impact-Handoffs zeigen auf das abgefragte Symbol, nicht auf die Aufrufstelle

- **Betroffenes Tool / Schema**: `find_references`, `get_impact` (Parameter: `symbolIdentifier`)
- **Ziel-Label**: `SOURCE-01` (Modus: Source); gleiches Muster auf `LOCAL-01`
- **Konkreter Aufruf**: `find_references(targetPath=<SOURCE-01>, symbolIdentifier=<h:Klasse>)` bzw. `get_impact(..., symbolIdentifier=<h:Klasse>)`
- **Beobachtung / Ist-Verhalten**:
  - Trefferlisten enthalten Datei:Zeile der Aufrufstellen, aber **jede** Zeile trägt dieselbe `handoffId` wie das **Ursprungssymbol**.
  - Ein Folge-`get_symbol_body` mit dieser ID landet wieder bei der Definition, nicht bei der aufrufenden Methode.
  - `get_call_tree` liefert dagegen **knotenweise unterschiedliche** `h:…` (Soll-Verhalten).
  - `get_impact` selbst: `risk`, `completeness`, direkte/transitive Call-Sites, **kein** Verify-Verdict — dieser Teil ist kontraktgerecht.
- **Soll-Verhalten / Problem aus Agentensicht**:
  - Schema: navigierbare Treffer enthalten `h:…` für direkte Folgeparameter. Der Agent kann von einer Referenz nicht zur Call-Site ketten, ohne Pfad+Zeile selbst zu bauen.
- **Empfehlung**:
  - Pro Treffer die `h:…` des enthaltenden Members (oder der Call-Site) ausgeben; Ursprungssymbol separat halten.

### [Major] C-05: `get_feature_context` / `get_test_context` ohne konsumierbare Caller- und Test-Handoffs

- **Betroffenes Tool / Schema**: `get_feature_context` (Caller), `get_test_context`
- **Ziel-Label**: `SOURCE-01` (Modus: Source)
- **Konkreter Aufruf**: `get_feature_context(targetPath=<SOURCE-01>, symbolIdentifier=<h:Klasse>, maxCallers=8)` und `get_test_context(targetPath=<SOURCE-01>, symbolIdentifier=<h:Klasse>)`
- **Beobachtung / Ist-Verhalten**:
  - `get_feature_context` akzeptiert die Klassen-`h:…` (Chaining-Schritt selbst ok). Call-Sites in Abschnitt 3 haben Pfad/Zeile/Methodennamen, **keine** `h:…`. Schema verspricht navigierbare Caller mit `h:…`.
  - Composite-Completeness `truncated` bei `maxCallers` ist nachvollziehbar.
  - `get_test_context`: Status `complete`, „10 Evidenztreffer in 3 Testdateien“, aber **0 von 10 Evidenztreffer zurückgegeben**; nur Dateipfade, keine Testmethoden-`h:…`.
  - Auf `LOCAL-01`: beide Tools `ASSEMBLY_TARGET_UNSUPPORTED` mit `capability=unsupported` — recoverable, kein Befund.
- **Soll-Verhalten / Problem aus Agentensicht**:
  - TC-C13 verlangt Verknüpfung Feature → Tests/Caller. Ohne `h:…` muss der Agent Namen oder Pfade zusammenbauen. 10 gefundene, 0 sichtbare Methodentreffer ist ein Vollständigkeitsbruch der Nutzlast, kein leeres Target.
- **Empfehlung**:
  - Caller wie in `get_call_tree` mit `h:…`.
  - Testkandidaten methodengenau mit `h:…` ausgeben oder Counts nicht höher als die sichtbare Liste setzen.

### [Major] C-06: `includeReferences=true` verdrängt das Assembly-Target durch Referenztreffer

- **Betroffenes Tool / Schema**: `find_symbol` (Parameter: `includeReferences`, `maxResults`)
- **Ziel-Label**: `LOCAL-01` (Modus: Assembly)
- **Konkreter Aufruf**: `find_symbol(targetPath=<LOCAL-01>, pattern=<Prefix>, maxResults=8, includeReferences=true|false)`
- **Beobachtung / Ist-Verhalten**:
  - Ohne Flag: Snapshot-Banner, Treffer im Target, `Assembly-Scope: requestedIncludeReferences=false; effectiveSearchMode=root_only; leaseStatus=complete` — Scope klar.
  - Mit Flag: `effectiveSearchMode=bounded_reference_closure; assembliesSearched=16; resultsTruncated=true; scopeCompleteness=partial`. Die **ersten** `maxResults` Treffer stammen durchweg aus Referenz-/BCL-Assemblies, **kein** Target-Treffer in der sichtbaren Seite.
  - Scope-Felder sind vorhanden (TC-C02 teilweise erfüllt), das Ranking macht den effektiven Suchmodus für den Agenten trotzdem unbrauchbar.
- **Soll-Verhalten / Problem aus Agentensicht**:
  - Target-Treffer zuerst oder separates `targetHits`/`referenceHits`. Sonst wirkt `includeReferences` wie „Target verschwunden“.
- **Empfehlung**:
  - Target-first-Ranking, Quota-Split, oder Pflichtfeld `originAssembly` plus Filterdefault „root then references“.

### [Major] C-07: Widersprüchliche Completeness-Signale im Assembly-Modus

- **Betroffenes Tool / Schema**: Assembly-Envelope / `Assembly-Scope` (sichtbar u. a. in `find_symbol`, `get_symbol_body`, `find_references`, `get_call_tree`)
- **Ziel-Label**: `LOCAL-01` (Modus: Assembly)
- **Konkreter Aufruf**: semantische Tools mit `targetPath=<LOCAL-01>`
- **Beobachtung / Ist-Verhalten**:
  - Kopfzeile: `snapshotStatus=partial; snapshotCompleteness=partial; bodyAvailability=available`.
  - Fußblock oft parallel: `leaseStatus=complete; scopeCompleteness=complete` (oder `partial` bei Call-Tree/Referenzsuche).
  - Ein Agent kann nicht maschinell entscheiden, ob der Snapshot vertrauenswürdig vollständig ist.
- **Soll-Verhalten / Problem aus Agentensicht**:
  - Eine kanonische Completeness-Achse (Snapshot vs. Lease vs. Suchscope) mit klarer Semantik. Widerspruch ist irreführend, kein Target-Qualitätsurteil.
- **Empfehlung**:
  - Ein Feld `completeness` plus getrennte, gleich benannte Teildimensionen; `partial` nur wenn die **aktuelle** Operation unvollständig ist.

### [Major] C-08: Assembly-Hierarchie kennzeichnet Basistypen als `error:` ohne Handoff

- **Betroffenes Tool / Schema**: `get_type_hierarchy` → `resolve_type_origin` (Parameter: `typeName`)
- **Ziel-Label**: `LOCAL-01` (Modus: Assembly)
- **Konkreter Aufruf / Kette**: `get_type_hierarchy(targetPath=<LOCAL-01>, symbolIdentifier=<h:Klasse>)` → Basis an `resolve_type_origin(typeName=…)`
- **Beobachtung / Ist-Verhalten**:
  - Basisklasse erscheint als `error: MyNamespace.MyBase (extern, keine Datei im Repo)` **ohne** `h:…`.
  - Unveränderte Übergabe dieses Anzeigetextes: `SYMBOL_NOT_FOUND` für `error: MyNamespace.MyBase`.
  - Auch der gestrippte Kurzname ist nicht auflösbar (fehlende Referenzassembly — das Fehlen selbst ist **kein** Befund).
  - Dieselbe Kette auf `SOURCE-01` mit Interface-`h:…` funktioniert; `Namespace.Type` bleibt konsistent.
- **Soll-Verhalten / Problem aus Agentensicht**:
  - CHAIN-03 verlangt durchreichbare Qualifikation. Das `error:`-Präfix erzwingt String-Manipulation und ist keine recoverable Typ-Diagnose mit `h:…` oder `origin=external`.
- **Empfehlung**:
  - Externe Basistypen als `origin=external` mit `h:…` oder stabilem FQ-Namen **ohne** Fehlerpräfix; `resolve_type_origin` soll denselben Identifier akzeptieren und `unresolved` strukturiert melden.

### [Minor] C-09: `resolve_type_origin` nutzt `typeName` statt `symbolIdentifier`

- **Betroffenes Tool / Schema**: `resolve_type_origin` (Parameter: `typeName`)
- **Ziel-Label**: `SOURCE-01` / `LOCAL-01`
- **Konkreter Aufruf**: `resolve_type_origin(targetPath=<LABEL>, typeName=<h:…>)`
- **Beobachtung / Ist-Verhalten**:
  - `h:…` wird akzeptiert (Kette auf `SOURCE-01` und Typ-Origin auf `LOCAL-01` ok).
  - Alle anderen Symboltools heißen der Parameter `symbolIdentifier` / `symbolIdentifiers`.
- **Soll-Verhalten / Problem aus Agentensicht**:
  - Uneinheitlicher Parametername erhöht die Chance, dass ein Agent den Wert umbenennt oder als Anzeigenamen interpretiert.
- **Empfehlung**:
  - Alias `symbolIdentifier` analog zu den übrigen Tools, `typeName` als Legacy-Alias.

### [Minor] C-10: Assembly-Call-Tree mischt nutzbaren Graph mit Diagnose-Rauschen

- **Betroffenes Tool / Schema**: `get_call_tree` (Assembly-Modus)
- **Ziel-Label**: `LOCAL-01` (Modus: Assembly)
- **Konkreter Aufruf**: `get_call_tree(targetPath=<LOCAL-01>, symbolIdentifier=<h:Methode>, direction=incoming)`
- **Beobachtung / Ist-Verhalten**:
  - Graph und Knoten-`h:…` sind hierarchisch und kettenfähig (TC-C08 fachlich ok).
  - Anschließend Dutzende Decompiler-/Referenz-Diagnosen (u. a. fehlende Fremdnamespaces), obwohl `resultsTruncated=false` und der Graph klein ist.
- **Soll-Verhalten / Problem aus Agentensicht**:
  - Diagnosen gehören hinter ein Flag oder in `get_server_health(includeDiagnostics)`. Default-Antwort sollte den Graphen nicht mit Target-fremden Compilerfehlern überdecken.
- **Empfehlung**:
  - Default: Graph + `diagnosticsCount`; Samples nur bei `includeDiagnostics=true`.

---

## 3. Kontrakt-Punkte ohne Befund (kurz)

- TC-C01: Prefix- und FQ-/Kind-Suche auf `SOURCE-01` mit `h:…` und Datei:Zeile.
- TC-C03 / CHAIN-01 / CHAIN-02: `h:…` durchreichbar, solange die Index-Generation stabil bleibt.
- TC-C04: Decompile-Stub auf `LOCAL-01` mit lesbarer Signatur; `Assembly-Scope` ausgewiesen.
- TC-C06: Memberlisten mit sofort nutzbaren Member-`h:…` (Source und Assembly).
- TC-C08 / TC-C09: Incoming/Outgoing-Bäume; Impact ohne Verify-Verdict.
- TC-C10–C12 auf `SOURCE-01`: Hierarchie, Implementierungen, Origin mit konsistentem `Namespace.Type`.
- FALSE-01: `INVALID_ASSEMBLY`, recoverable, kein Crash.
- `RESPONSE_BUDGET_TOO_SMALL` trat in Gruppe C nicht auf.
