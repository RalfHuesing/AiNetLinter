# MCP-Audit – Gruppe B: Discovery, Scope & Assembly-Inspektion

> **Zuständigkeit:** Subagent B
> **Tools:** `get_index_scope`, `get_file_tree`, `get_namespace_tree`, `inspect_assembly`, `search_assembly`, `find_assembly_extensions`, `get_assembly_context`
> **Regeln:** Nur negative Server-Befunde. Keine echten Produktnamen/Pfade, nur Ziel-Labels.

---

## 1. Übersicht & Testergebnis

- **Geprüfte Tools:** `get_index_scope`, `get_file_tree`, `get_namespace_tree`, `inspect_assembly`, `search_assembly`, `find_assembly_extensions`, `get_assembly_context`
- **Geprüfte Prüffälle:** TC-B01 bis TC-B10; optional Envelope-Vergleich `SOURCE-02`; Form-Stichprobe `LOCAL-02`/`LOCAL-03`; Fehlbedienung `MASK-01`/`MASK-02`
- **Gefundene Befunde:** 0 Critical, 2 Major, 2 Minor

Ohne Befund (kurz, kein Rauschen): TC-B01 Routing auf `search_pattern(pattern, scopeType=all, includePatterns=…)` mit Schema-treuen Parametern; TC-B02 `view=summary` und `view=tree`/`treeDepth=2` navigierbar; TC-B03 `RESPONSE_BUDGET_TOO_SMALL` mit `minimumResponseBytes=2090`, Retry lieferte nicht-leere Landkarte; TC-B04 Assembly-Header `origin=decompiled` / `contentMode=decompiledProject`; TC-B05 Prefix-Filter auf `SOURCE-01` und `LOCAL-01` schränkt ein; TC-B06 Public-API inkl. `h:…` und `continuationToken`, Budget-Retry `maxResponseBytes=2048` mind. 1 Einheit; TC-B07 `INVALID_ASSEMBLY` ohne Crash; TC-B08 Match-Liste mit `h:…` (Treffer nur als `MyNamespace.MyType`); TC-B09 leere Menge `0 von 0` mit Status; TC-B10 Verweise/Identität strukturiert, Zielframework ehrlich `nicht verfügbar`. Masken: Schema kennt kein Glob-`targetPath`; `INVALID_ARGUMENT` + `fieldPath=$.targetPath` recoverable.

---

## 2. Negative Befunde

### [Major] B-01: `inspect_assembly` exponiert absolute Cache-Pfade

- **Betroffenes Tool / Schema**: `inspect_assembly` (Feld: `decompileRoot`)
- **Ziel-Label**: `LOCAL-01`, `LOCAL-02`, `LOCAL-03` (Modus: Assembly)
- **Konkreter Aufruf**: `inspect_assembly(targetPath=<LOCAL-01|LOCAL-02|LOCAL-03>, detailLevel=compact)`
- **Beobachtung / Ist-Verhalten**:
  - Erfolgreiche Antworten enthalten ein Literal `decompileRoot` mit **absolutem Dateisystempfad** unter einem lokalen Cache-Root (sessiongebunden, generiert). Dasselbe Feld trat bei DLL- und verwalteter-EXE-Form identisch auf.
  - Der Pfad ist nicht das übergebene `targetPath`, sondern ein serverseitiger Speicherort.
- **Soll-Verhalten / Problem aus Agentensicht**:
  - TC-B06 verlangt Public API ohne Leaks voller Systempfade. Absolute Cache-Pfade verletzen Datensparsamkeit, brennen Response-Budget und können in Logs/Transkripten weiterwandern.
- **Empfehlung**:
  - `decompileRoot` weglassen oder als opake Session-/Generation-ID ohne Dateisystempfad ausweisen; Lesbarkeit über bestehende Handoffs (`h:…`) statt Pfad-Literal.

### [Major] B-02: `find_assembly_extensions` mischt Leermenge mit Pfad-Leaks und Diagnose-Rauschen

- **Betroffenes Tool / Schema**: `find_assembly_extensions`
- **Ziel-Label**: `LOCAL-01` (Modus: Assembly)
- **Konkreter Aufruf**: `find_assembly_extensions(targetPath=<LOCAL-01>, maxResults=20)`
- **Beobachtung / Ist-Verhalten**:
  - Fachliche Treffermenge klar: `Assembly-Extensions: 0 von 0` (kein Befund zur leeren Liste).
  - Daneben `Diagnosen: 20 von 22`, darunter gebündelte Decompiler-Meldungen (tausende semantische Hinweise) und Zeilen der Form „kein identitätsgleicher Kandidat … geprüft: **&lt;absoluter Installationspfad einer Nachbar-Datei&gt;**“.
- **Soll-Verhalten / Problem aus Agentensicht**:
  - Leere Extension-Menge soll statusklar und datensparsam sein. Absolute Nachbar-Pfade sind ein Leak der Installationsumgebung; das Rauschen erschwert die agentische Auswertung (Signal-Rausch-Schwäche, Datensparsamkeit).
- **Empfehlung**:
  - Bei `0` Treffern Diagnosen auf maschinenlesbare Codes ohne Dateisystempfade reduzieren (z. B. unresolved-identity, budget, completeness). Volle Pfade und Decompiler-Fluten nur hinter explizitem Diagnose-Flag.

### [Minor] B-03: `get_namespace_tree` liefert keine `h:`-Handoffs an Typknoten

- **Betroffenes Tool / Schema**: `get_namespace_tree` (Typenliste)
- **Ziel-Label**: `SOURCE-01` (Modus: Source), `LOCAL-01` (Modus: Assembly)
- **Konkreter Aufruf**: `get_namespace_tree(targetPath=<SOURCE-01>, namespacePrefix=…, depth=2)` bzw. `get_namespace_tree(targetPath=<LOCAL-01>, namespacePrefix=…)`
- **Beobachtung / Ist-Verhalten**:
  - Hierarchie und Prefix-Filter funktionieren. Typzeilen enden als `MyType (kind) — MyFile.cs:N` ohne `handoffId: h:…`.
  - `inspect_assembly` / `search_assembly` liefern dieselben Typen mit `h:…`.
- **Soll-Verhalten / Problem aus Agentensicht**:
  - Folgeaufrufe sollen kanonische Handoff-IDs aus dem Content nutzen, nicht Namen/Pfade rekonstruieren. Fehlende optionale `h:…` erzwingt Toolwechsel oder unsicheres Zusammensetzen.
- **Empfehlung**:
  - Pro sichtbarem Typ (und idealerweise Namespace-Knoten) dieselbe `h:…` ausweisen wie in `inspect_assembly`.

### [Minor] B-04: Assembly-`get_namespace_tree` ohne Prefix nennt die Namespaces nicht

- **Betroffenes Tool / Schema**: `get_namespace_tree` (ohne `namespacePrefix`)
- **Ziel-Label**: `LOCAL-01` (Modus: Assembly)
- **Konkreter Aufruf**: `get_namespace_tree(targetPath=<LOCAL-01>, depth=1)` und erneut mit `depth=2, includeTypes=false`
- **Beobachtung / Ist-Verhalten**:
  - Antwort bleibt auf Assembly-Übersicht: ein Knoten mit Zählwerten („N Namespaces, M Typen“). Auch bei `depth=2` erscheinen **keine Namespace-Namen**.
  - Erst mit bekanntem `namespacePrefix` folgt die Typliste. Vergleich: `SOURCE-01` ohne Filter listet benannte Projekte als nächste Ebene.
- **Soll-Verhalten / Problem aus Agentensicht**:
  - Strukturierte Hierarchie soll die nächste Ebene benennen, damit `namespacePrefix` nicht erraten oder aus einem anderen Tool kopiert werden muss. Das ist fehlende Navigation, kein Target-Inhaltsmangel.
- **Empfehlung**:
  - Im Assembly-Modus ohne Filter die Namespace-Namen (ggf. mit `h:…`) als Kinder ausgeben, analog zur Projektliste im Source-Modus.
