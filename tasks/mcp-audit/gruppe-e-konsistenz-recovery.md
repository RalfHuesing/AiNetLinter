# MCP-Audit – Gruppe E: Cross-Tool-Konsistenz, Handoff-Vertrag & Recovery

> **Zuständigkeit:** Subagent E
> **Tools:** Querschnittsprüfung aller 33 MCP-Tools
> **Regeln:** Nur negative Befunde protokollieren. Keine echten Produktnamen/Pfade, nur Ziel-Labels (`SOURCE-01`, `LOCAL-01`, etc.)!

---

## 1. Übersicht & Testergebnis

- **Geprüfte Querschnittsbereiche:** Frühvalidierung, Typfehler, ungültige Pfade/Enums, Response-Budget-Treue, deterministisches Retry, Parameter-Naming, Session-Isolation
- **Geprüfte Prüffälle:** TC-E01 bis TC-E06 aus `FlightPlan.md`
- **Gefundene Befunde:** 0 Critical, 0 Major, 1 Minor

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
