# MCP-Audit – Gruppe E: Cross-Tool-Konsistenz, Handoff-Vertrag & Recovery

> **Zuständigkeit:** Subagent E
> **Tools:** Querschnittsprüfung aller 33 MCP-Tools
> **Regeln:** Nur negative Befunde protokollieren. Keine echten Produktnamen/Pfade, nur Ziel-Labels (`SOURCE-01`, `LOCAL-01`, etc.)!

---

## 1. Übersicht & Testergebnis

- **Geprüfte Querschnittsbereiche:** Frühvalidierung, Typfehler, ungültige Pfade/Enums, Response-Budget-Treue, deterministisches Retry, Parameter-Naming, Session-Isolation
- **Geprüfte Prüffälle:** TC-E01 bis TC-E06 aus `FlightPlan.md`
- **Gefundene Befunde:** 0 Critical, 2 Major, 3 Minor

Live-Katalog: 33 Fach-Tools plus Cursor-internes `mcp_auth` (generische Auth-Description, nicht als AiNetLinter-Fach-Tool beworben — kein Befund). Quality-Gate ist `verify` (`targetPath`, `scope`); keine Alt-Filter `scopeFilter` / `minScore` / `maxViolations` im Schema.

Ohne Befund geblieben: Typfehler (handlungsweisende JSON-Typ-Meldung, kein Stacktrace); `kind=invalidKind` (gültige Enum-Werte genannt); Ordner-statt-Datei; Session-Isolation SOURCE-01 → LOCAL-01 → LOCAL-02 → FALSE-01 → SOURCE-02 → SOURCE-01 (keine Cache-Kontamination, `TARGET_MISMATCH` bei fremder Handoff-ID, FALSE-01 zerstört SOURCE-Session nicht).

---

## 2. Negative Befunde

### [Major] E-02: `maxResponseBytes=500` bricht das Budget-Protokoll

- **Betroffener Querschnittsbereich**: Response-Budget-Recovery
- **Betroffenes Tool / Schema**: `find_symbol`, `get_class_structure` (Parameter: `maxResponseBytes`)
- **Ziel-Label**: `SOURCE-01` (Modus: Source)
- **Konkreter Aufruf**: `find_symbol(targetPath=SOURCE-01, pattern=…, maxResponseBytes=500)`; `get_class_structure(targetPath=SOURCE-01, symbolIdentifier=h:…, maxResponseBytes=500)`
- **Beobachtung / Ist-Verhalten**:
  - Beide Tools: `INVALID_ARGUMENT` — `maxResponseBytes muss zwischen 512 und 65536 Bytes liegen.` JSON-Schema der Properties enthält **kein** `minimum: 512`.
  - Dieselben 500 Bytes bei `get_file_tree(targetPath=SOURCE-01, view=summary)` und `inspect_assembly(targetPath=LOCAL-01)`: `RESPONSE_BUDGET_TOO_SMALL` mit `minimumResponseBytes` und Retry-Hinweis.
  - Ab 512 Bytes: `find_symbol` → `RESPONSE_BUDGET_TOO_SMALL` (`minimumResponseBytes=10054`, inkl. `Status: operation=error`); `get_class_structure` → Erfolg mit 1 Member (Budget-Kürzung, mindestens 1 Einheit).
- **Soll-Verhalten / Problem aus Agentensicht**:
  - Der dokumentierte Probe-Wert 500 soll entweder Wire-treu beantwortet oder mit `RESPONSE_BUDGET_TOO_SMALL` + deterministischem `minimumResponseBytes` quittiert werden. Ein hartes `INVALID_ARGUMENT` unterhalb eines undokumentierten Floors verhindert den einheitlichen Retry-Pfad.
- **Empfehlung**:
  - Floor 512 im JSON-Schema exponieren **oder** Werte &lt; 512 wie bei `get_file_tree`/`inspect_assembly` in `RESPONSE_BUDGET_TOO_SMALL` überführen (`minimumResponseBytes` ≥ 512). Verhalten über alle budgetfähigen Tools angleichen.

### [Major] E-03: Retry mit `minimumResponseBytes` hält das Wire-Budget nicht ein

- **Betroffener Querschnittsbereich**: Deterministisches Budget-Retry
- **Betroffenes Tool / Schema**: `inspect_assembly`, `get_file_tree` (Parameter: `maxResponseBytes`)
- **Ziel-Label**: `LOCAL-01` (Assembly) / `SOURCE-01` (Source)
- **Konkreter Aufruf**: `inspect_assembly(targetPath=LOCAL-01, maxResponseBytes=500)` → Retry exakt `maxResponseBytes=2048`; `get_file_tree(targetPath=SOURCE-01, view=summary, maxResponseBytes=500)` → Retry exakt `maxResponseBytes=2090`
- **Beobachtung / Ist-Verhalten**:
  - Erster Call jeweils korrekt `RESPONSE_BUDGET_TOO_SMALL` mit `requestedBytes`, `minimumResponseBytes`, `retry`.
  - Retry `inspect_assembly` mit 2048: **Erfolg**, mindestens 1 API-Typ — aber Nutzlast weit oberhalb 2048 (gekürzte Typen plus lange Referenz-/Diagnoseblöcke, Continuation). Kein erneutes `RESPONSE_BUDGET_TOO_SMALL`.
  - Retry `get_file_tree` mit 2090: **Erfolg** mit vollständiger Summary-Landkarte plus Warn-/Next-Zeilen; sichtbare Nutzlast über dem gemeldeten Minimum. Der erste Hinweis versprach die „vollständige wertvolle Dateilandkarte“ bei genau diesem Wert.
  - Kontrast: `get_class_structure(maxResponseBytes=512)` kürzt auf 1 Member und bleibt nah am Cap.
- **Soll-Verhalten / Problem aus Agentensicht**:
  - `minimumResponseBytes` muss die kleinste **wire-konforme** Nutzlast mit ≥1 Einheit beschreiben. Liefert der Retry mehr als `maxResponseBytes` ohne neuen Budget-Fehler, ist das Cap unbrauchbar und das Minimum nicht deterministisch.
- **Empfehlung**:
  - Retry hart auf `maxResponseBytes` kappen oder erneut `RESPONSE_BUDGET_TOO_SMALL` mit höherem, ehrlichem `minimumResponseBytes` liefern. Referenz-/Diagnoseblöcke nicht ungekürzt in die Minimalprojektion ziehen.

### [Minor] E-04: `navigation.status` nur bei einem Teil der Fehler

- **Betroffener Querschnittsbereich**: Envelope / Contract v2
- **Betroffenes Tool / Schema**: Validierungsfehler vs. Budget-Fehler (`find_symbol`, `inspect_assembly`, `reload_config`, `verify`, `get_file_tree`)
- **Ziel-Label**: ungebunden / `SOURCE-01` / `LOCAL-01`
- **Konkreter Aufruf**: Pflichtfeld weglassen; Typfehler; `kind=invalidKind`; `maxResponseBytes=500|512`
- **Beobachtung / Ist-Verhalten**:
  - Budget-Fehler `get_file_tree` und `find_symbol` (512): Textzeile `Status: operation=error, completeness=not_applicable, analysisQuality=complete`.
  - Budget-Fehler `inspect_assembly` (500): maschinenlesbare Budget-Felder, **keine** Status-Zeile.
  - Pflichtfeld-/Typ-/Enum-Fehler: `INVALID_ARGUMENT` + `fieldPath`/`hint`, **kein** `navigation.status`.
  - `verify`: `verdict: error` statt `navigation.status`.
- **Soll-Verhalten / Problem aus Agentensicht**:
  - Contract v2 verlangt `navigation.status.operation=error` einheitlich. Agenten können Fehler nicht über ein Feld routen.
- **Empfehlung**:
  - Jede Fehlerantwort (Validierung, Budget, Target) mit demselben `navigation.status`-Block ausstatten; `verify` nicht als Sonderform belassen (siehe E-01).

### [Minor] E-05: MASK-01 als `targetPath` wie fehlende Datei

- **Betroffener Querschnittsbereich**: Ungültige Pfade
- **Betroffenes Tool / Schema**: `inspect_assembly` (Parameter: `targetPath`)
- **Ziel-Label**: `MASK-01` (Verzeichnismaske)
- **Konkreter Aufruf**: `inspect_assembly(targetPath=MASK-01)`
- **Beobachtung / Ist-Verhalten**:
  - `INVALID_ARGUMENT`: Parameter müsse auf eine **vorhandene Datei** zeigen; Hint nennt `.sln/.slnx/.dll/.exe`.
  - Kein Hinweis, dass Wildcards/Masken kein gültiges `targetPath` sind und Discovery anders zu adressieren ist. Dieselbe Code-Familie wie bei einer schlicht nicht existenten Datei (kein `FILE_NOT_FOUND`).
- **Soll-Verhalten / Problem aus Agentensicht**:
  - Eine Maske ist kein fehlender Dateiname. Ohne Glob-Diagnose sucht der Agent weiter nach „der Datei“ statt ein Discovery-Tool zu wählen.
- **Empfehlung**:
  - Wildcard/`*` in `targetPath` als eigenen, selbsterklärenden Code behandeln (z. B. `INVALID_ARGUMENT` mit Hint „keine Globs; konkrete Datei oder Discovery-Tool“). Fehlende Dateien optional als `FILE_NOT_FOUND` unterscheiden.

### [Minor] E-06: `symbolIdentifier` an Batch-Tools ohne Alias-Hinweis

- **Betroffener Querschnittsbereich**: Parameter-Naming
- **Betroffenes Tool / Schema**: `get_symbol_body` (Parameter: `symbolIdentifiers`); Kontrast `resolve_type_origin` (`typeName`)
- **Ziel-Label**: `SOURCE-01` (Modus: Source)
- **Konkreter Aufruf**: `get_symbol_body(targetPath=SOURCE-01, symbolIdentifier=h:…)` (Singular, wie bei fast allen Gruppe-C-Tools)
- **Beobachtung / Ist-Verhalten**:
  - `INVALID_ARGUMENT: Unbekanntes Argument: symbolIdentifier`. Hint: nur Schema-Argumente verwenden. **Kein** Verweis auf `symbolIdentifiers` (Array).
  - Derselbe `h:…`-Token ist bei `get_class_structure` / `find_references` / `get_feature_context` der Parameter `symbolIdentifier`, bei `resolve_type_origin` Pflichtfeld `typeName` (im Description-Text erwähnt).
  - Übrige Naming-Unterschiede (`pattern`/`namePatterns` vs. `symbolIdentifier`, `helperSymbol`, `filePath`/`filePaths`) sind im Live-Schema semantisch begründet oder beschrieben. `verify` bewirbt keine Alt-Filter — kein zusätzlicher Defekt.
- **Soll-Verhalten / Problem aus Agentensicht**:
  - Naheliegendes Chaining (`h:…` → `symbolIdentifier`) scheitert an einem unbekannten Schlüsselnamen ohne Korrekturvorschlag. Das erhöht Reibung, obwohl das Schema den Plural kennt.
- **Empfehlung**:
  - Bei unbekanntem `symbolIdentifier` explizit `symbolIdentifiers` (Array) vorschlagen; optional Alias akzeptieren und intern zu `["h:…"]` mappen. `resolve_type_origin` auf `symbolIdentifier` angleichen oder in jeder Handoff-Zeile den Zielparameter `typeName` nennen.
