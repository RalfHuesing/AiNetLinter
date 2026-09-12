# MCP-Audit – Gruppe D: Codequalität, Linter, Metriken & Safeguard

> **Zuständigkeit:** Subagent D
> **Tools:** `get_violations`, `safeguard`, `get_hotspots`, `find_dead_code`, `find_duplicates`, `find_magic_values`, `pattern_detect`, `metrics_tree`, `metrics_lookup`, `dependency_graph`, `search_pattern`
> **Regeln:** Nur negative Befunde protokollieren. Keine echten Produktnamen/Pfade, nur Ziel-Labels (`SOURCE-01`, `LOCAL-01`, etc.)!

---

## 1. Übersicht & Testergebnis

- **Geprüfte Tools:** 11 Analyse- und Qualitäts-Tools (TC-D01 bis TC-D11)
- **Geprüfte Prüffälle:** Linter (Source + Assembly), Safeguard/`minScore`, Hotspots, Dead-Code, Duplikate (`minTokens`/`scopeDir`/`scopeType`), Magic Values (`scopeFilter`), Pattern-Detect, Metriken, Graph, `search_pattern`/`includePatterns`
- **Gefundene Befunde:** 0 Critical, 2 Major, 2 Minor

---

## 2. Negative Befunde

### [Major] D-01: `scopeType=production` liefert Top-Treffer aus Testprojekten

- **Betroffenes Tool / Schema**: `find_duplicates` (Parameter: `scopeType`)
- **Ziel-Label**: `SOURCE-01` (Modus: Source)
- **Konkreter Aufruf**: `find_duplicates(targetPath=SOURCE-01, scopeType="production", minTokens=30, maxResults=3)`
- **Beobachtung / Ist-Verhalten**:
  - Explizites `scopeType="production"` scannt 4018 Methoden und 98 Cluster; die drei angezeigten Top-Cluster liegen sämtlich unter `*.Tests.*`-Pfaden (Hilfsmethoden/Factories, keine `[Fact]`-Bodies).
  - `scopeType="tests"`: 4788 Methoden / 393 Cluster; `scopeType="all"`: 8806 Methoden / 504 Cluster. 4018 + 4788 = 8806 — die Mengen sind disjunkt, aber „production“ enthält Testhilfen aus Testprojekten.
  - Die Antwort weist den effektiven `scopeType` nicht aus (im Gegensatz zu `get_hotspots`, das `Scope-Typ: 'production'` setzt, und `find_magic_values`, das `Tests ausgeschlossen` schreibt).
  - `minTokens` und `scopeDir` greifen; Phantom-`minLines`/`scopeFilter` werden mit `INVALID_ARGUMENT` + `fieldPath` abgewiesen (kein eigener Befund).
- **Soll-Verhalten / Problem aus Agentensicht**:
  - Agenten interpretieren `production` analog zu den anderen Audit-Tools als Ausschluss von Testprojekten. Sie erhalten dann Testhilfs-Duplikate statt Produktionsfunde und können den Filter in der Antwort nicht gegenprüfen.
- **Empfehlung**:
  - `scopeType=production` datei-/projektbasiert wie `get_hotspots` / `find_magic_values` filtern **oder** die abweichende Semantik (Nicht-Testmethoden inkl. Testhilfen) im Schema und in der Antwort (`effectiveScope`) maschinenlesbar machen.

### [Major] D-02: `enrichCSharp=true` liefert keine Symbolevidenz

- **Betroffenes Tool / Schema**: `search_pattern` (Parameter: `enrichCSharp`)
- **Ziel-Label**: `SOURCE-01` (Modus: Source)
- **Konkreter Aufruf**: `search_pattern(targetPath=SOURCE-01, pattern="public static void RemoveSite", includePatterns=["*.cs"], scope="San.smart.Planner.Platform.Contracts", enrichCSharp=true, maxResults=1)`
- **Beobachtung / Ist-Verhalten**:
  - Schema-Beschreibung: `enrichCSharp` „ergaenzt C#-Symbolevidenz (Default false)“.
  - Treffer auf einer echten Methodendeklaration und auf `public sealed class SiteMetadata` bleiben reine `Datei:Zeile: Text`-Zeilen. Keine Symbol-ID, kein Kind, kein Handoff, kein Status `enrichment=skipped`.
  - `includePatterns` existiert im Schema und filtert (z. B. `*.cs` vs. `*.md`) — kein Phantom-Feld.
- **Soll-Verhalten / Problem aus Agentensicht**:
  - Opt-in für Folgetools (`get_symbol_body`, `find_references`) ist beworben, bleibt aber ein stiller No-Op. Der Agent verkettet nicht und erhält keinen Hinweis, dass die Anreicherung unterblieben ist.
- **Empfehlung**:
  - Pro C#-Treffer kanonische Handoff-ID liefern — oder `enrichCSharp` als `unsupported` / `skipped` mit Grund markieren, statt still zu ignorieren.

### [Minor] D-03: Schema-`required` weicht von der Runtime-Pflicht ab

- **Betroffenes Tool / Schema**: `metrics_lookup` (`symbolIdentifiers`), `dependency_graph` (`filePath` XOR `symbolIdentifier`), `search_pattern` (`pattern`)
- **Ziel-Label**: `SOURCE-01` (Modus: Source)
- **Konkreter Aufruf**: `metrics_lookup(targetPath=SOURCE-01)` · `dependency_graph(targetPath=SOURCE-01)` · `search_pattern(targetPath=SOURCE-01)`
- **Beobachtung / Ist-Verhalten**:
  - JSON-Schema `required` enthält jeweils nur `targetPath`. `symbolIdentifiers` / `filePath` / `pattern` sind optional (`default: null`).
  - Runtime: `INVALID_ARGUMENT` („Pflichtparameter 'symbolIdentifiers' fehlt“, „filePath und symbolIdentifier … genau einen angeben, nie beide oder keins“, „pattern darf nicht leer sein“) — jeweils mit brauchbarem `hint`.
- **Soll-Verhalten / Problem aus Agentensicht**:
  - Schema-first-Aufrufe sind laut `tools/list` gültig, scheitern aber. Ein Extra-Roundtrip ist nötig; `anyOf`/`required` im Schema würde das verhindern. Die Runtime-Fehler selbst sind klar (kein Critical).
- **Empfehlung**:
  - Schema an die Runtime anpassen: `metrics_lookup.symbolIdentifiers` required; `dependency_graph` `anyOf` filePath|symbolIdentifier; `search_pattern.pattern` required.

### [Minor] D-04: `pattern_detect` markiert vollständige Läufe als `partiell`

- **Betroffenes Tool / Schema**: `pattern_detect` (Parameter: `patterns`)
- **Ziel-Label**: `SOURCE-01` (Modus: Source)
- **Konkreter Aufruf**: `pattern_detect(targetPath=SOURCE-01)` bzw. `pattern_detect(targetPath=SOURCE-01, scopeFilter="San.smart.Planner.Platform.Contracts")`
- **Beobachtung / Ist-Verhalten**:
  - Default-Lauf (6 Detectoren): `Vollstaendigkeitsstatus: partiell`, obwohl Dateiscan durch ist. Ursache: `public-without-doc` = `not_configured`, `feature-envy` = `not_decidable`.
  - Eingeschränkt auf `patterns=["god-class","async-void"]`: klare Leer-Antwort ohne `partiell`.
  - Ungültige IDs (`Repository`, `Factory`, `Singleton`) → `INVALID_ARGUMENT` mit gültiger Liste. Die gültigen Werte stehen nicht als JSON-Schema-`enum` an `patterns`.
- **Soll-Verhalten / Problem aus Agentensicht**:
  - Globales `partiell` wird wie Truncation/`RESPONSE_BUDGET_TOO_SMALL` gelesen; Agenten erhöhen Limits statt Regeln zu konfigurieren. Ohne Schema-Enum ist der erste Default- oder Rate-Versuch spekulativ.
- **Empfehlung**:
  - Gesamstatus `complete`, wenn alle angeforderten Detectoren entschieden sind (`checked` / `empty` / `not_configured` / `not_decidable`). `partiell` nur bei Truncation. `patterns.items.enum` im Schema setzen.
