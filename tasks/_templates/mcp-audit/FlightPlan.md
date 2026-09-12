# MCP-Audit: Flight Plan (Prüfmatrix & Checkliste)

> **Regeln beachten:** Siehe [Prompt.md](Prompt.md). Strikter Read-Only-Betrieb. Niemals echte Produkt- oder Firmennamen verwenden; nur die folgenden anonymen Ziel-Labels!

## 1. Testziel-Zuordnung (Vor Start durch Orchestrator ausfüllen)

| Label | Modus | Zweck / Beschreibung | Lokaler Pfad (NUR HIER, NIE IN COMMITS/FINDINGS!) |
|---|---|---|---|
| `SOURCE-01` | Source (`.slnx` / `.sln`) | Eigenes Repo oder Referenz-Projekt | *(Pfad hier eintragen)* |
| `LOCAL-01` | Assembly (`.dll`) | Lokale komplexe Fremd-Assembly | *(Pfad hier eintragen)* |
| `LOCAL-02` | Assembly (`.dll`) | Zweite Fremd-Assembly für Moduswechsel | *(Pfad hier eintragen)* |
| `LOCAL-03` | Assembly (`.exe`) | Verwaltete .NET-Executable | *(Pfad hier eintragen)* |
| `FALSE-01` | Negativfall (`.exe` / `.dll`) | Nicht verwaltete Native-Binary | *(Pfad hier eintragen)* |

---

## 2. Gesamtfortschritt der Audit-Gruppen

- [ ] **Gruppe A: Health, Handshake & Runtime-Config** (Tools 1–3) -> Bericht: [gruppe-a-health-handshake.md](gruppe-a-health-handshake.md)
- [ ] **Gruppe B: Discovery, Scope & Assembly-Inspektion** (Tools 4–10) -> Bericht: [gruppe-b-discovery.md](gruppe-b-discovery.md)
- [ ] **Gruppe C: Semantische Symbol-Tools & Chaining-Ketten** (Tools 11–22) -> Bericht: [gruppe-c-symbol-chaining.md](gruppe-c-symbol-chaining.md)
- [ ] **Gruppe D: Codequalität, Linter, Metriken & Safeguard** (Tools 23–33) -> Bericht: [gruppe-d-qualitaet-metriken.md](gruppe-d-qualitaet-metriken.md)
- [ ] **Gruppe E: Cross-Tool-Konsistenz, Handoff-Vertrag & Recovery** (Querschnitt) -> Bericht: [gruppe-e-konsistenz-recovery.md](gruppe-e-konsistenz-recovery.md)
- [ ] **Abschlussbericht & Priorisierung** -> Bericht: [README.md](README.md)

---

## 3. Detaillierte Prüffälle nach Tool-Gruppen

### Gruppe A: Health, Handshake & Runtime-Config
*Zuständiger Subagent: Schreibt ausschließlich in `gruppe-a-health-handshake.md`*

- [ ] **TC-A01 (`get_server_health` ungebunden)**: Aufruf ohne `targetPath`.
  - *Erwartung*: Liefert globale Servergesundheit, Version, Uptime; keine ungefilterten Prozess-/Host-IDs (Datensparsamkeit).
- [ ] **TC-A02 (`get_server_health` auf `SOURCE-01`)**: Mit Quellcode-Target.
  - *Erwartung*: `capabilities` weist Syntax- und Lint-Fähigkeit maschinenlesbar aus; Status des Roslyn-Index klar erkennbar.
- [ ] **TC-A03 (`get_server_health` auf `LOCAL-01`)**: Mit Assembly-Target.
  - *Erwartung*: `origin=decompiled`, `lint=unsupported`, keine widersprüchlichen Completeness-Signale zwischen Fach- und Envelope-Ebene.
- [ ] **TC-A04 (`get_server_health` auf `FALSE-01`)**: Negativfall (unmanaged Binary).
  - *Erwartung*: Saubere recoverable Diagnose, kein Prozessabsturz, kein interner Stacktrace.
- [ ] **TC-A05 (`report_observability_feedback`)**: Feedback-Kanal.
  - *Erwartung*: Nimmt Feedback entgegen, liefert strukturierte Quittung, kein Crash bei leeren/großen Payloads.
- [ ] **TC-A06 (`reload_config`)**: Konfigurations-Reload.
  - *Erwartung*: Lädt Konfiguration neu, bestätigt Status strukturiert.

---

### Gruppe B: Discovery, Scope & Assembly-Inspektion
*Zuständiger Subagent: Schreibt ausschließlich in `gruppe-b-discovery.md`*

- [ ] **TC-B01 (`get_index_scope` auf `SOURCE-01`)**: Indexabdeckung.
  - *Erwartung*: Ausweisung von physischen Dateien, C#-Dateien, generierten/Test-Dateien; Non-C#-Routing verweist mit exakten Parametern auf `search_pattern` (keine Phantom-Felder).
- [ ] **TC-B02 (`get_file_tree` Summary & Tree auf `SOURCE-01`)**:
  - *Erwartung*: `view=summary` liefert kompakte Verteilung; `view=tree` mit `treeDepth=2` liefert navigierbaren Verzeichnisbaum.
- [ ] **TC-B03 (`get_file_tree` Budgettreue)**: Aufruf mit extrem kleinem `maxResponseBytes` (z. B. 200 Bytes).
  - *Erwartung*: Hält Wire-Budget ein oder meldet `RESPONSE_BUDGET_TOO_SMALL` mit deterministischem `minimumResponseBytes`.
- [ ] **TC-B04 (`get_file_tree` auf `LOCAL-01`)**: Assembly-Modus.
  - *Erwartung*: Meldet klar verständlichen Decompiled-Fallback oder Modus-Hinweis, keine fehlerhafte Physisch-Simulation.
- [ ] **TC-B05 (`get_namespace_tree` auf `SOURCE-01` & `LOCAL-01`)**:
  - *Erwartung*: Liefert strukturierte Namespace-Hierarchie; Filter `namespacePrefix` schränkt korrekt ein.
- [ ] **TC-B06 (`inspect_assembly` auf `LOCAL-01`)**:
  - *Erwartung*: Exponiert Public API, Exported Types, Namespaces; bei Budgetgrenzen sauberer Retry-Wert; keine Leaks voller Systempfade.
- [ ] **TC-B07 (`inspect_assembly` auf `FALSE-01`)**:
  - *Erwartung*: Erkennt non-.NET / unmanaged Binary strukturiert; kein Crash.
- [ ] **TC-B08 (`search_assembly` auf `LOCAL-01`)**: Typ- und Mustersuche in Assembly.
  - *Erwartung*: Findet bekannte Typen/Methoden, liefert strukturierte Match-Liste mit Handoff-IDs.
- [ ] **TC-B09 (`find_assembly_extensions` auf `LOCAL-01`)**:
  - *Erwartung*: Findet Extension-Methods oder leere Treffermenge mit klarem Status.
- [ ] **TC-B10 (`get_assembly_context` auf `LOCAL-01`)**:
  - *Erwartung*: Liefert Verweise, Zielframework, Assembly-Metadaten strukturiert.

---

### Gruppe C: Semantische Symbol-Tools & Chaining-Ketten
*Zuständiger Subagent: Schreibt ausschließlich in `gruppe-c-symbol-chaining.md`*

#### Einzel-Tool-Funktionalität:
- [ ] **TC-C01 (`find_symbol` auf `SOURCE-01`)**: Namens- und Mustersuche.
  - *Erwartung*: Exakte und Prefix-Suche liefert Treffer inklusive kanonischer Symbol-ID und `navigation`.
- [ ] **TC-C02 (`find_symbol` auf `LOCAL-01`)**: Assembly-Symbolsuche mit und ohne `includeReferences`.
  - *Erwartung*: Snapshot-Ergebnis; bei gesetztem `includeReferences` wird der effektive Scope in `navigation` ausgewiesen.
- [ ] **TC-C03 (`get_symbol_body` auf `SOURCE-01`)**: Quelltextabfrage via `symbolId`.
  - *Erwartung*: Liefert vollständigen Quelltext der Methode/Klasse.
- [ ] **TC-C04 (`get_symbol_body` auf `LOCAL-01`)**: Decompile-Stub.
  - *Erwartung*: Liefert dekompilierten C#-Code; Signaturen und Typen lesbar.
- [ ] **TC-C05 (`get_file_skeleton`)**: Skelettansicht einer Datei.
  - *Erwartung*: Signaturen ohne Implementierungs-Bodies, Token-sparend.
- [ ] **TC-C06 (`get_class_structure`)**: Struktur einer Klasse.
  - *Erwartung*: Vollständige Memberliste (Properties, Methods, Events); jedes Member enthält direkt konsumierbare Handoff-IDs für Folgetools.
- [ ] **TC-C07 (`find_references`)**: Referenzsuche nach SymbolIdentifier.
  - *Erwartung*: Liefert Aufrufstellen; im Assembly-Modus klare Statusaussage bzgl. Leases/Scope.
- [ ] **TC-C08 (`get_call_tree`)**: Aufrufbaum.
  - *Erwartung*: Incoming- und Outgoing-Aufrufe navigierbar hierarchisch dargestellt.
- [ ] **TC-C09 (`get_impact`)**: Auswirkungsanalyse.
  - *Erwartung*: Direkte und transitive Abhängige mit Risiko-Einstufung.
- [ ] **TC-C10 (`get_type_hierarchy`)**: Vererbungshierarchie.
  - *Erwartung*: Basisklassen und implementierte Interfaces.
- [ ] **TC-C11 (`find_implementations`)**: Interface-Implementierungen.
  - *Erwartung*: Findet konkrete Typen, die ein Interface implementieren.
- [ ] **TC-C12 (`resolve_type_origin`)**: Typursprung.
  - *Erwartung*: Ermittelt definierende Quell- oder Bibliotheks-Assembly.
- [ ] **TC-C13 (`get_feature_context` & `get_test_context`)**:
  - *Erwartung*: Kontextuelle Verknüpfung von Feature-Code zu zugehörigen Tests und Callern.

#### Durchgängige Chaining- und Kombinations-Prüfungen:
- [ ] **CHAIN-01 (Symbol Deep-Dive Kette)**:
  `find_symbol` -> `symbolId` -> `get_symbol_body` -> `symbolIdentifier` -> `find_references` -> `get_impact`.
  - *Prüffrage*: Kann der Output von Schritt N direkt ohne manuelle Manipulation in Schritt N+1 übergeben werden?
- [ ] **CHAIN-02 (Klassen-Exploration zu Member-Body)**:
  `find_symbol(kind=class)` -> `get_class_structure` -> Member auswählen -> `get_symbol_body`.
  - *Prüffrage*: Liefert `get_class_structure` Member-IDs, die `get_symbol_body` sofort akzeptiert, oder muss der Agent String-Konstruktion betreiben?
- [ ] **CHAIN-03 (Typ-Hierarchie zu Implementierungen)**:
  `get_type_hierarchy` -> Interface identifizieren -> `find_implementations` -> `resolve_type_origin`.
  - *Prüffrage*: Sind Typnamen über die Tools hinweg konsistent qualifiziert (`Namespace.Type`)?
- [ ] **CHAIN-04 (Assembly-Exploration zu Decompile-Stub)**:
  `inspect_assembly` -> Typ wählen -> `search_assembly` -> `symbolId` -> `get_symbol_body`.
  - *Prüffrage*: Funktioniert die Kette auf Fremd-Assemblies ohne `TARGET_MISMATCH`?

---

### Gruppe D: Codequalität, Linter, Metriken & Safeguard
*Zuständiger Subagent: Schreibt ausschließlich in `gruppe-d-qualitaet-metriken.md`*

- [ ] **TC-D01 (`get_violations` auf `SOURCE-01`)**:
  - *Erwartung*: Liefert aktive Regelverstöße mit Regel-ID, Schweregrad, Datei, Zeile und Korrekturhinweis.
- [ ] **TC-D02 (`get_violations` auf `LOCAL-01`)**:
  - *Erwartung*: Im Decompiled-Modus klare Meldung `unsupported` / `RULES_INVALID`, kein Absturz.
- [ ] **TC-D03 (`safeguard` auf `SOURCE-01`)**:
  - *Erwartung*: Liefert Gesamt-Score (0-10), Detailprüfungen (Tests, Warnungen, Architektur); `minScore`-Filter funktioniert.
- [ ] **TC-D04 (`get_hotspots`)**:
  - *Erwartung*: Identifiziert komplexe/änderungsintensive Codestellen mit Metriken.
- [ ] **TC-D05 (`find_dead_code`)**:
  - *Erwartung*: Findet ungenutzte private Member/Klassen mit genauer Fundstelle.
- [ ] **TC-D06 (`find_duplicates`)**:
  - *Erwartung*: Erkennt Duplikate/Ähnlichkeiten, `minLines`-Parameter wird respektiert.
- [ ] **TC-D07 (`find_magic_values`)**:
  - *Erwartung*: Findet unbenannte Literale; `scopeDir`-Filter greift.
- [ ] **TC-D08 (`pattern_detect`)**:
  - *Erwartung*: Erkennt Architekturmuster (z. B. Repository, Factory, Singleton).
- [ ] **TC-D09 (`metrics_tree` & `metrics_lookup`)**:
  - *Erwartung*: `metrics_tree` liefert hierarchische Komplexitätsübersicht; `metrics_lookup` liefert gezielte Einzelwerte für Symbole.
- [ ] **TC-D10 (`dependency_graph`)**:
  - *Erwartung*: Liefert Projekt- oder Namespace-Abhängigkeiten in navigierbarer Struktur.
- [ ] **TC-D11 (`search_pattern`)**:
  - *Erwartung*: Semantische/syntaktische Mustersuche in C#-Dateien; Parameter `includePatterns` funktioniert wie im Schema definiert.

---

### Gruppe E: Cross-Tool-Konsistenz, Handoff-Vertrag & Recovery
*Zuständiger Subagent: Schreibt ausschließlich in `gruppe-e-konsistenz-recovery.md`*

- [ ] **TC-E01 (Frühvalidierung & Envelope-Konsistenz)**:
  - Pflichtfeld weglassen (z. B. `targetPath` bei zielgebundenen Tools).
  - *Erwartung*: `isError=true` im JSON-RPC Envelope; Contract v2 `navigation.status` (`operation=error`); sprechender `field`-Hinweis.
- [ ] **TC-E02 (Typfehler-Validierung)**:
  - String statt Array (z. B. `includePatterns="foo"` statt `["foo"]`).
  - *Erwartung*: Handlungsweisende Typ-Fehlermeldung, kein Stacktrace.
- [ ] **TC-E03 (Ungültige Pfade & Enums)**:
  - Nicht-existenter Pfad `C:\nicht\existent.dll`, Ordner statt Datei, unbekannter Enum-Wert (`kind=invalidKind`).
  - *Erwartung*: Selbsterklärende Fehlercodes (`FILE_NOT_FOUND`, `INVALID_ARGUMENT`), Erwähnung gültiger Enum-Werte.
- [ ] **TC-E04 (Response-Budget & Deterministischer Retry)**:
  - Teste `maxResponseBytes=500` auf `find_symbol`, `get_file_tree`, `inspect_assembly`, `get_class_structure`.
  - *Erwartung*: Entweder saubere Wire-Budget-Einhaltung ODER `RESPONSE_BUDGET_TOO_SMALL` mit deterministischem `minimumResponseBytes`, der bei erneutem Aufruf garantiert mindestens 1 Einheit liefert (keine leeren Hüllen!).
- [ ] **TC-E05 (Cross-Tool Parameter-Naming Konsistenz)**:
  - Prüfe Benennung über alle 33 Tools: `symbolIdentifier` vs. `pattern` vs. `namePattern` vs. `name`.
  - *Erwartung*: Unterschiede sind semantisch begründet oder im Schema klar dokumentiert.
- [ ] **TC-E06 (Session-Isolation bei Target-Wechsel)**:
  - Aufrufsequenz: `SOURCE-01` -> `LOCAL-01` -> `LOCAL-02` -> zurück zu `SOURCE-01`.
  - *Erwartung*: Keine Cache-Kontamination, saubere Trennung der Roslyn- bzw. Assembly-Kontexte.
