# MCP-Agent-UX-Audit: Gesamtbewertung

> **Audit-Durchführung:** Reiner Read-Only-Audit. Keine Quellcode-Änderungen, kein Build, keine Tests. Alle Fremd-Targets sind ausschließlich über anonyme Labels referenziert (`SOURCE-01`, `LOCAL-01`–`LOCAL-03`, `FALSE-01`).

**Stand:** alle fünf Gruppen abgeschlossen. **19 Befunde:** 1 Critical, 13 Major, 5 Minor. Kein Server-Crash.

---

## 1. Urteil

Der MCP-Server ist **stabil erreichbar** (Daemon-Health, Uptime, Version). Source- und Assembly-Sessions lassen sich wechseln, **ohne Cache-Kontamination** (`SOURCE-01` → `LOCAL-01` → `LOCAL-02` → `SOURCE-01`). Negativfälle (`FALSE-01`, fehlende Pflichtfelder, Typfehler, nicht existente Pfade) sind **recoverable**: kein Prozessabsturz, kein interner Stacktrace.

**Source-Kette CHAIN-01 funktioniert:** `find_symbol` → `get_symbol_body` → `find_references` → `get_impact` (und `get_feature_context`) mit **unveränderter** kanonischer Handoff-ID. Assembly-Direktpfad `inspect_assembly` → `get_symbol_body` ohne `TARGET_MISMATCH`.

**Blocker:** [E-01](gruppe-e-konsistenz-recovery.md) — `get_class_structure` bei Floor-Budget (`maxResponseBytes=512`) liefert `0 von 12` Member und den Erfolgtext „Keine Member gefunden.“ Das ist ein **falscher Erfolgspfad** (`isError=false` bei fachlichem Budgetfehler) und bricht Member-Ketten.

**Querschnittsmuster (höchste Hebelwirkung):**

1. **Budget-Vertrag ist nicht einheitlich.** Tool-spezifische Floors verhindern eine kanonische 500-Byte-Probe (E-03); `get_class_structure` liefert dabei eine leere Hülle (E-01).
2. **Handoff-IDs fehlen oder werden abgewiesen**, sobald die Kette über `get_class_structure`, `search_assembly`, `find_implementations` oder `resolve_type_origin` läuft (C-01–C-03, B-06, E-04). CHAIN-02–04 brauchen manuelles Parsen.

Lint-/Metrik-Tools auf `SOURCE-01` sind grundsätzlich nutzbar; Assembly-Lint wird als nicht unterstützt erkannt. Qualitäts-Tools haben weniger Blocker als Discovery/Chaining, aber zwei klare Agentenfallen (`scopeType=production` vs. Testhilfen, stilles `enrichCSharp`).

---

## 2. Priorisierte Gesamttabelle aller Befunde

Verwandte IDs aus mehreren Gruppen sind nicht zusammengelegt; die Wirkungsspalte verweist auf Duplikate.

| Priorität | ID | Tool(s) | Kurzbeschreibung des Befunds | Wirkung auf konsumierende Agenten | Bericht |
|---|---|---|---|---|---|
| Critical | E-01 | `get_class_structure` | Floor-Budget 512: leere Member-Hülle als Erfolg (`0 von 12`, „Keine Member gefunden“) | Falscher Envelope; Member-Ketten werden als „klasse leer“ abgebrochen | [E](gruppe-e-konsistenz-recovery.md) |
| Major | E-03 | `find_symbol`, `get_class_structure`, `inspect_assembly` | `maxResponseBytes=500` trifft Tool-spezifische Floors (512 vs. 2048 vs. akzeptieren) | Kanonischer 500-Byte-Probe nicht ausführbar | [E](gruppe-e-konsistenz-recovery.md) |
| Major | C-01 | `get_class_structure` | Keine Member-`handoffId`; Namens-/Signatur-Übernahme → `AMBIGUOUS_SYMBOL` / `SYMBOL_NOT_FOUND` | CHAIN-02 braucht String-Bau oder Umweg `get_file_skeleton` | [C](gruppe-c-symbol-chaining.md) |
| Major | C-03 | `search_assembly` | Treffer ohne `symbolId`/`handoffId` | CHAIN-04 Schritt 3→4 nicht geschlossen | [C](gruppe-c-symbol-chaining.md) |
| Major | B-06 | `search_assembly` | Dieselbe Lücke (Match nur `Pfad:Zeile`) | Wie C-03 | [B](gruppe-b-discovery.md) |
| Major | C-02 | `resolve_type_origin`, `find_implementations` | Handoff-ID → `SYMBOL_NOT_FOUND`; Implementierer ohne IDs | CHAIN-03 bricht in Schritt 3 | [C](gruppe-c-symbol-chaining.md) |
| Major | E-04 | `resolve_type_origin` | Kanonische Handoff-ID als Typname über 427 Referenzen gesucht | Irreführend `SYMBOL_NOT_FOUND` statt `INVALID_ARGUMENT`/Annahme der ID | [E](gruppe-e-konsistenz-recovery.md) |
| Major | C-05 | `get_call_tree` | Keine Node-Handoffs; Incoming vermischt Overrides als Calls | Baum nicht kettenfähig, semantisch irreführend | [C](gruppe-c-symbol-chaining.md) |
| Major | C-04 | `get_impact` | Keine Risiko-Stufe; Projektliste unvollständig (Host fehlt) | Unterschätzung der Produktionswirkung | [C](gruppe-c-symbol-chaining.md) |
| Major | C-06 | `find_symbol`, `find_references`, `get_symbol_body` | Assembly: `partial` vs. Body-`complete`; `includeReferences` ohne Scope-Objekt | Lease/„keine Treffer“/Cap nicht unterscheidbar | [C](gruppe-c-symbol-chaining.md) |
| Major | B-05 | `get_assembly_context` | `includeMetrics` → Solution-`NOT_CONFIGURED` | Irreführender Rules-Hint am Assembly-Target | [B](gruppe-b-discovery.md) |
| Major | D-01 | `find_duplicates` | `scopeType=production` zeigt Testhilfen; effektiver Scope nicht ausgewiesen | Falsche Priorisierung von „Produktions“-Duplikaten | [D](gruppe-d-qualitaet-metriken.md) |
| Major | D-02 | `search_pattern` | `enrichCSharp=true` stiller No-Op (keine Symbol-ID) | Beworbenes Opt-in für Folgetools fehlt | [D](gruppe-d-qualitaet-metriken.md) |
| Major | E-05 | `find_dead_code` | Ungültiges `kind`/`accessibility` ohne Werteliste | Agent muss Enum raten | [E](gruppe-e-konsistenz-recovery.md) |
| Minor | C-07 | `find_references`, `get_feature_context`, `get_test_context` | Call-Sites/Tests ohne eigene Handoff-IDs; 0/29 Evidenzzeilen | Vertiefen in Caller braucht Parsing | [C](gruppe-c-symbol-chaining.md) |
| Minor | D-03 | `metrics_lookup`, `dependency_graph`, `search_pattern` | Schema-`required` nur `targetPath`, Runtime verlangt weitere Felder | Extra-Roundtrip nach Schema-first | [D](gruppe-d-qualitaet-metriken.md) |
| Minor | D-04 | `pattern_detect` | Vollständiger Lauf als `partiell` wegen `not_configured`; kein Schema-`enum` | Agent erhöht Limits statt Regeln zu setzen | [D](gruppe-d-qualitaet-metriken.md) |
| Minor | E-06 | mehrere | `scope` / `scopeDir` / `scopeFilter` / `root` und `includeTests` vs. `scopeType` undokumentiert | Copy-Paste-Parameter scheitern | [E](gruppe-e-konsistenz-recovery.md) |
| Minor | E-07 | mehrere | Teil der Enum-Fehler ohne `fieldPath` | Feldname nur aus Prosa | [E](gruppe-e-konsistenz-recovery.md) |

### Empfohlene Bearbeitungsreihenfolge (Produkt)

1. **E-01** — leere Erfolgs-Hülle von `get_class_structure` (einziger Critical).
2. **Einheitlicher Budget-Vertrag** — E-03 (ein Code, ein `minimumResponseBytes`, ein Floor).
3. **Handoff schließen** — C-01, C-03/B-06, C-02/E-04 (Member-, Search- und Origin-IDs).

---

## 3. Positiv bestätigte Eigenschaften

- Server-Health ungebunden und zielgebunden **ohne Crash**; `FALSE-01` liefert recoverable Diagnose (`INVALID_ASSEMBLY` / Health-Negativfall), kein Stacktrace (TC-A04, TC-B07).
- **Session-Isolation** bestätigt: nach Assembly-Zielen keine Fremdtypen in der Source-Antwort (TC-E06); `LOCAL-03` (verwaltete EXE) analog zu `LOCAL-01` ladbar.
- **CHAIN-01** mit unveränderter `handoffId` inkl. `get_feature_context`.
- `inspect_assembly` → `get_symbol_body` auf `LOCAL-01` **ohne** `TARGET_MISMATCH`; Decompile-Stubs mit lesbaren Signaturen (TC-C04).
- `get_file_skeleton` trägt Member-Handoffs (brauchbarer Umweg, solange C-01 offen ist).
- `get_type_hierarchy` liefert FQCN + Handoffs; Anzeigenamen `Namespace.Type` sind über Hierarchie-Tools konsistent.
- Frühvalidierung zielgebundener Tools: fehlendes `targetPath`/`symbolIdentifier` → `[ERROR] INVALID_ARGUMENT` + `fieldPath`; `get_server_health` ohne Target bleibt erlaubt (TC-E01).
- Typfehler String-statt-Array: klare Typmeldung, kein Stacktrace (TC-E02).
- `find_symbol` nach `RESPONSE_BUDGET_TOO_SMALL` + Retry mit `minimumResponseBytes` liefert ≥1 Einheit (TC-E04, Tool-Pfad `find_symbol` ab Floor 512).
- `get_violations` auf `LOCAL-01` ohne Absturz (Decompiled/unsupported-Pfad, TC-D02).
- `includePatterns` an `search_pattern` existiert und filtert; `minTokens`/`scopeDir` an `find_duplicates` greifen; Phantomfelder werden mit `fieldPath` abgewiesen.
- `namespacePrefix` schränkt Source- und Assembly-Bäume korrekt ein; `includeTypes=true` liefert bei Source-Trees Typart, relativen Pfad und Zeile.

---

## 4. Referenzierte Teilberichte der Subagenten

- [Gruppe A – Health, Handshake & Runtime-Config](gruppe-a-health-handshake.md) — 0 / 0 / 0
- [Gruppe B – Discovery, Scope & Assembly-Inspektion](gruppe-b-discovery.md) — 0 / 2 / 0
- [Gruppe C – Semantische Symbol-Tools & Chaining-Ketten](gruppe-c-symbol-chaining.md) — 0 / 6 / 1
- [Gruppe D – Codequalität, Linter, Metriken & Safeguard](gruppe-d-qualitaet-metriken.md) — 0 / 2 / 2
- [Gruppe E – Cross-Tool-Konsistenz, Handoff-Vertrag & Recovery](gruppe-e-konsistenz-recovery.md) — 1 / 3 / 2
