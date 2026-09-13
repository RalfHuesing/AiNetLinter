# MCP-Audit – Gruppe E: Cross-Tool-Konsistenz, Handoff-Vertrag & Recovery

> **Zuständigkeit:** Subagent E
> **Tools:** Querschnittsprüfung aller 33 MCP-Tools
> **Regeln:** Nur negative Befunde protokollieren. Keine echten Produktnamen/Pfade, nur Ziel-Labels (`SOURCE-01`, `LOCAL-01`, etc.)!

---

## 1. Übersicht & Testergebnis

- **Geprüfte Querschnittsbereiche:** Frühvalidierung, Typfehler, ungültige Pfade/Enums, Response-Budget-Treue, deterministisches Retry, Parameter-Naming, Session-Isolation
- **Geprüfte Prüffälle:** TC-E01 bis TC-E06 aus `FlightPlan.md`
- **Gefundene Befunde:** 0 Critical, 0 Major, 2 Minor

Live-Katalog: 33 Fach-Tools plus Cursor-internes `mcp_auth` (generische Auth-Description, nicht als AiNetLinter-Fach-Tool beworben — kein Befund). Quality-Gate ist `verify` (`targetPath`, `scope`); keine Alt-Filter `scopeFilter` / `minScore` / `maxViolations` im Schema.

Ohne Befund geblieben: Typfehler (handlungsweisende JSON-Typ-Meldung, kein Stacktrace); `kind=invalidKind` (gültige Enum-Werte genannt); Ordner-statt-Datei; Session-Isolation SOURCE-01 → LOCAL-01 → LOCAL-02 → FALSE-01 → SOURCE-02 → SOURCE-01 (keine Cache-Kontamination, `TARGET_MISMATCH` bei fremder Handoff-ID, FALSE-01 zerstört SOURCE-Session nicht).

---

## 2. Negative Befunde

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
