# MCP-Audit – Gruppe E: Cross-Tool-Konsistenz, Handoff-Vertrag & Recovery

> **Zuständigkeit:** Subagent E
> **Tools:** Querschnittsprüfung aller 33 MCP-Tools
> **Regeln:** Nur negative Befunde protokollieren. Keine echten Produktnamen/Pfade, nur Ziel-Labels (`SOURCE-01`, `LOCAL-01`, etc.)!

---

## 1. Übersicht & Testergebnis

- **Geprüfte Querschnittsbereiche:** Frühvalidierung, Typfehler, ungültige Pfade/Enums, Response-Budget-Treue, deterministisches Retry, Parameter-Naming, Session-Isolation
- **Geprüfte Prüffälle:** TC-E01 bis TC-E06 aus `FlightPlan.md`
- **Gefundene Befunde:** 1 Critical, 4 Major, 2 Minor

---

## 2. Negative Befunde

### [Critical] E-01: `get_class_structure` liefert bei Floor-Budget eine leere Member-Hülle als Erfolg

- **Betroffener Querschnittsbereich**: Response-Budget / leere Hülle / Envelope (`isError=false` bei fachlichem Budgetfehler)
- **Betroffenes Tool / Schema**: `get_class_structure` (Parameter: `maxResponseBytes`, `symbolIdentifier`)
- **Ziel-Label**: `SOURCE-01` (Modus: Source)
- **Konkreter Aufruf**: `get_class_structure(targetPath=SOURCE-01, symbolIdentifier=<Handoff einer 12-Member-Klasse>, maxResponseBytes=512)`
- **Beobachtung / Ist-Verhalten**:
  - CallDynamicTool-Ergebnis ohne `[ERROR]` (Erfolgspfad).
  - `Member Count: 0 von 12` plus Fließtext „Keine Member gefunden.“
  - Kein `RESPONSE_BUDGET_TOO_SMALL`, kein `minimumResponseBytes`, kein Hinweis auf Budgetkürzung.
  - Kontrollaufruf ohne `maxResponseBytes`: `Member Count: 12 von 12` mit vollständiger Member-Tabelle.
  - Kontrollaufruf mit `maxResponseBytes=512` auf `find_symbol` (gleicher Floor): korrekt `[ERROR] RESPONSE_BUDGET_TOO_SMALL` inkl. `minimumResponseBytes` und `operation=error`.
  - Bei `maxResponseBytes=1024` dieselbe Klasse: `2 von 12` plus Hinweis „maxResponseBytes erhöhen“ — der Floor 512 fällt hinter dieses Kürzungssignal zurück.
- **Soll-Verhalten / Problem aus Agentensicht**:
  - Unter dem dokumentierten Source-Floor (512) ist die fachliche Mindestprojektion nicht darstellbar. Vertrag: `RESPONSE_BUDGET_TOO_SMALL` mit deterministischem Retry, nicht ein erfolgreicher Envelope, der die Klasse als member-los verkauft.
  - Ein Agent liest „Keine Member gefunden“ als Wahrheit und bricht Member-Ketten (`get_symbol_body`, `find_references`) ab.
- **Empfehlung**:
  - Wenn nicht mindestens ein vollständiger Member-Eintrag plus Envelope in `maxResponseBytes` passt: `RESPONSE_BUDGET_TOO_SMALL` mit `minimumResponseBytes` (wie `find_symbol` bei 512).
  - Den Erfolgspfad „Keine Member gefunden“ nur verwenden, wenn die Klasse tatsächlich 0 Member hat.

### [Major] E-02: `get_file_tree` akzeptiert 500/512 Bytes und überschreitet das Wire-Budget ohne Budget-Fehler

- **Betroffener Querschnittsbereich**: Response-Budget / Wire-Treue
- **Betroffenes Tool / Schema**: `get_file_tree` (Parameter: `maxResponseBytes`)
- **Ziel-Label**: `SOURCE-01` (Modus: Source)
- **Konkreter Aufruf**: `get_file_tree(targetPath=SOURCE-01, maxResponseBytes=500)` sowie identisch mit `maxResponseBytes=512`
- **Beobachtung / Ist-Verhalten**:
  - Beide Aufrufe laufen als Erfolg (kein `[ERROR]`, kein `RESPONSE_BUDGET_TOO_SMALL`).
  - Payload enthält Scan-Kopf, vollständige Extensions-Liste (30+ Endungen), Warn-/NEXT-Zeilen; sichtbare Antwortlänge klar über 500 bzw. 512 Bytes (grob ≥750 Zeichen plus Newlines/Umlaute).
  - WARN nennt `maxResponseBytes` als Kürzungsgrund („1 gezeigt“), der Kopf inkl. Extensions-Inventar wird trotzdem vollständig ausgeliefert.
  - Gegensatz: `find_symbol` lehnt 500 als `INVALID_ARGUMENT` ab (Floor 512) und liefert bei 512 einen echten Budget-Fehler.
- **Soll-Verhalten / Problem aus Agentensicht**:
  - Entweder Wire-Budget einhalten oder `RESPONSE_BUDGET_TOO_SMALL` mit `minimumResponseBytes`. Eine „gekürzte“ Antwort, deren Hülle bereits über dem Limit liegt, macht Token-Budgets und Retry-Logik unbrauchbar.
- **Empfehlung**:
  - Extensions-Inventar und Warnblock in das Wire-Budget einrechnen.
  - Passt die Mindestprojektion nicht: `RESPONSE_BUDGET_TOO_SMALL` plus maschinenlesbares `minimumResponseBytes` (nicht nur WARN-Prosa).

### [Major] E-03: `maxResponseBytes=500` trifft Tool-spezifische Floors statt des Budget-Retry-Vertrags

- **Betroffener Querschnittsbereich**: Response-Budget / deterministischer Retry
- **Betroffenes Tool / Schema**: `find_symbol`, `get_class_structure`, `inspect_assembly` (Parameter: `maxResponseBytes`)
- **Ziel-Label**: `SOURCE-01` (Source) / `LOCAL-01` (Assembly)
- **Konkreter Aufruf**: `find_symbol(targetPath=SOURCE-01, pattern="Handler", maxResponseBytes=500)`; `get_class_structure(..., maxResponseBytes=500)`; `inspect_assembly(targetPath=LOCAL-01, maxResponseBytes=500)`
- **Beobachtung / Ist-Verhalten**:
  - Source (`find_symbol`, `get_class_structure`): `[ERROR] INVALID_ARGUMENT` — „maxResponseBytes muss zwischen 512 und 65536 Bytes liegen“, `fieldPath: $.maxResponseBytes`. Kein `minimumResponseBytes`, kein `RESPONSE_BUDGET_TOO_SMALL`.
  - Assembly (`inspect_assembly`): `[ERROR] INVALID_ARGUMENT` — Floor **2048** Bytes („maschinenlesbarer Assembly-Envelope“). Kein `fieldPath`, kein `minimumResponseBytes`. Hinweis nennt konfiguriertes Default 16384.
  - `get_file_tree` akzeptiert 500 (siehe E-02) — dritter, widersprüchlicher Pfad.
  - Nach Anhebung auf 512: `find_symbol` liefert korrekt `RESPONSE_BUDGET_TOO_SMALL` mit `minimumResponseBytes: 16685`; Retry mit 16685 liefert ≥1 Symboleinheit (50 von 732). Der dokumentierte Retry greift also erst **nach** dem Floor, nicht beim geforderten 500-Byte-Probe.
- **Soll-Verhalten / Problem aus Agentensicht**:
  - Der Integrationsvertrag („bei Budgetfehler denselben Aufruf mit `maxResponseBytes=minimumResponseBytes` wiederholen“) ist für den kanonischen 500-Byte-Probe nicht ausführbar: Agent muss deutsche Prosa parsen und je Tool 512 vs. 2048 vs. „einfach akzeptieren“ raten.
  - Schema-Default `0` bei mehreren Assembly-Tools steht zusätzlich quer zum Source-Floor (explizites `0` auf `find_symbol` = `INVALID_ARGUMENT`, auf `inspect_assembly` = Server-Default).
- **Empfehlung**:
  - Einheitlicher Floor, als JSON-Feld `minimumResponseBytes` auch bei Unterschreitung des Floors (nicht nur als `INVALID_ARGUMENT`-Prosa).
  - Schema-`default` und Laufzeit-Floor angleichen; `fieldPath` auch auf dem Assembly-Pfad setzen.

### [Major] E-04: `resolve_type_origin` weist kanonische Handoff-IDs als `SYMBOL_NOT_FOUND` ab

- **Betroffener Querschnittsbereich**: Handoff-Vertrag / irreführende Validierung
- **Betroffenes Tool / Schema**: `resolve_type_origin` (Parameter: `typeName`; kein `symbolIdentifier`)
- **Ziel-Label**: `SOURCE-01` (Modus: Source)
- **Konkreter Aufruf**: `resolve_type_origin(targetPath=SOURCE-01, typeName=<kanonische find_symbol-Handoff-ID derselben Klasse>)`
- **Beobachtung / Ist-Verhalten**:
  - `[ERROR] SYMBOL_NOT_FOUND`: die gesamte Handoff-ID wird als Typname in referenzierten Assemblies gesucht (Hinweis „Durchsuchte Referenzen (427): …“).
  - Kontrollaufruf mit einfachem Typnamen derselben Klasse: Erfolg (Vollqualifizierter Name, Symbol-Art, Herkunft).
  - Schema verlangt `typeName` (string, required); `symbolIdentifier` existiert hier nicht. Andere Typ-Tools (`get_type_hierarchy`, `find_implementations`, `get_class_structure`, `get_feature_context`) akzeptieren genau die Handoff-ID.
- **Soll-Verhalten / Problem aus Agentensicht**:
  - Handoff-Vertrag: Output von Schritt N ohne manuelle String-Zerlegung in Schritt N+1. Hier muss der Agent die DocComment-/Typ-Suffixe aus der Handoff-ID selbst extrahieren.
  - `SYMBOL_NOT_FOUND` nach 427-Referenz-Suche ist irreführend: das Argument ist syntaktisch eine Handoff-ID, kein fehlender Typ. Erwartet wäre `INVALID_ARGUMENT` mit Hinweis „Handoff-ID nicht akzeptiert, `typeName` als Typname übergeben“ **oder** Annahme der kanonischen Handoff-ID.
- **Empfehlung**:
  - Dieselbe Handoff-ID wie die übrigen Symbol-Tools akzeptieren (`symbolIdentifier` oder `typeName` als Union).
  - Andernfalls früh `INVALID_ARGUMENT` mit `fieldPath` und Beispiel, statt einer Origin-Suche über die Handoff-Zeichenkette.

### [Major] E-05: `find_dead_code` nennt bei ungültigen Enums keine gültigen Werte

- **Betroffener Querschnittsbereich**: Enum-Validierung (TC-E03)
- **Betroffenes Tool / Schema**: `find_dead_code` (Parameter: `kind`, `accessibility`)
- **Ziel-Label**: `SOURCE-01` (Modus: Source)
- **Konkreter Aufruf**: `find_dead_code(targetPath=SOURCE-01, kind="invalidKind")`; `find_dead_code(targetPath=SOURCE-01, accessibility="nope")`
- **Beobachtung / Ist-Verhalten**:
  - `kind`: `[ERROR] INVALID_ARGUMENT: Unbekannter kind-Wert.` Hint: „Parameter pruefen und gemaess Spezifikation uebergeben.“ `fieldPath: $.kind`. Keine Werteliste.
  - `accessibility`: analog „Unbekannter accessibility-Wert“, Hint identisch generisch, `fieldPath: $.accessibility`. Keine Werteliste.
  - Input-Schema listet für beide Felder kein `enum`, nur Defaults (`kind=all`, `accessibility=private_internal`).
  - Gegensatz: `find_symbol` (`kind=invalidKind`) listet gültige Werte; ebenso `get_namespace_tree`, `get_file_tree` (`view`), `get_call_tree` (`direction`), `get_hotspots` (`scopeType`), `find_duplicates` (`mode`), `search_assembly` (`searchKind`).
- **Soll-Verhalten / Problem aus Agentensicht**:
  - Erwartung TC-E03: gültige Enum-Werte in der Fehlermeldung. Ohne Schema-Enum und ohne Hint-Liste muss der Agent raten oder andere Tools imitieren (`kind`-Werte von `find_symbol` sind nicht dieselben).
- **Empfehlung**:
  - Dieselbe Fehlerform wie `find_symbol`: gültige Werte im Hint, plus `enum` im JSON-Schema.

### [Minor] E-06: Drei Scope-Namen plus zwei Test-Filter-Modelle ohne Querschnittsdoku

- **Betroffener Querschnittsbereich**: Cross-Tool Parameter-Naming (TC-E05)
- **Betroffenes Tool / Schema**: `search_pattern` (`scope`), `find_duplicates` (`scopeDir`), Audit-Tools (`scopeFilter`), `get_file_tree`/`metrics_tree` (`root`); zusätzlich `includeTests` vs. `scopeType`
- **Ziel-Label**: Schema-Vergleich über alle 33 Tools (kein Laufzeit-Target)
- **Konkreter Aufruf**: GetDynamicTools Namespace-Schemas; stichprobenartig ungültiges `scopeType` auf `get_hotspots`
- **Beobachtung / Ist-Verhalten**:
  - Pfad-/Projekt-Einschränkung heißt je Tool `scope` (relativer Pfad), `scopeDir` (Verzeichnis), `scopeFilter` (Projektname oder Pfad-Substring) oder `root` (Tree-Wurzel). Keine Schema-Beschreibung, dass die Namen bewusste Aliase oder bewusste Semantik-Differenzen eines gemeinsamen Vertrags sind.
  - Test-Einbeziehung: `find_dead_code` / `find_magic_values` nutzen Boolean `includeTests`; `get_hotspots` / `find_duplicates` / viele Symbol-Tools nutzen Tri-State `scopeType` (`production`|`tests`|`all`). `get_violations`, `pattern_detect`, `safeguard` haben weder `scopeType` noch `includeTests`.
- **Soll-Verhalten / Problem aus Agentensicht**:
  - Agent kopiert `scopeFilter` an `find_duplicates` oder `includeTests` an `get_hotspots` und erhält „Unbekanntes Argument“ bzw. stilles Ignorieren, obwohl die fachliche Absicht identisch ist (Suche eingrenzen / Tests ein- oder ausschließen).
- **Empfehlung**:
  - Entweder kanonische Namen (`scopeFilter` + `scopeType`) mit Aliasen oder in jeder Beschreibung die bewusste Abweichung und das Mapping nennen.

### [Minor] E-07: `fieldPath` fehlt bei einem Teil der Enum-Fehler

- **Betroffener Querschnittsbereich**: Envelope-Konsistenz der Validierung
- **Betroffenes Tool / Schema**: `get_namespace_tree` (`kind`), `get_file_tree` (`view`), `get_call_tree` (`direction`), `get_hotspots` (`scopeType`), `inspect_assembly` (`maxResponseBytes`-Floor, siehe E-03)
- **Ziel-Label**: `SOURCE-01`
- **Konkreter Aufruf**: `get_namespace_tree(kind="bogus")`, `get_file_tree(view="notAView")`, `get_call_tree(direction="sideways")`, `get_hotspots(scopeType="invalidScope")`
- **Beobachtung / Ist-Verhalten**:
  - Gültige Werte werden genannt (gut), aber ohne `fieldPath`.
  - `find_symbol` / `find_dead_code` / Pflichtfeld- und Typfehler setzen durchgängig `fieldPath: $.…`.
- **Soll-Verhalten / Problem aus Agentensicht**:
  - Maschinenlesbare Korrektur (`fieldPath`) ist sonst Teil des v2-Fehlervertrags. Fehlt sie, muss der Agent den Feldnamen aus Prosa rekonstruieren (`kind-Filter`, `view muss …`).
- **Empfehlung**:
  - `fieldPath` auf allen `INVALID_ARGUMENT`-Pfaden setzen, analog zu Pflichtfeld- und Typfehlern.

---

## 3. Kurznotiz zu Prüffällen ohne Befund

Nicht als positive Wertung, nur zur Abgrenzung: TC-E01 (fehlendes `targetPath` / `symbolIdentifier` → `[ERROR] INVALID_ARGUMENT` + `fieldPath`; `get_server_health` ohne `targetPath` erlaubt), TC-E02 (String statt Array → Typmeldung ohne Stacktrace), TC-E03-Pfade (`C:\nicht\existent.dll` / Ordner statt Datei, kein Crash), TC-E04-Retry von `find_symbol` nach `minimumResponseBytes=16685` (≥1 Einheit), TC-E06 (SOURCE-01 → LOCAL-01 → LOCAL-02 → SOURCE-01, plus FALSE-01: keine Assembly-Typen in der Source-Antwort, keine Source-Typen als Cache in LOCAL-01).
