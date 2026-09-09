---
name: mcp-ux-audit
description: Prüfe alle MCP-Tools des AiNetLinter-Servers aus Agentensicht. Rufe Tools systematisch und kreativ auf, beurteile Responses qualitativ, dokumentiere Befunde und behebe sie direkt via Red-Test-First. Nutze Subagenten pro Tool-Gruppe um das Kontextfenster sauber zu halten.
---

# AiNetLinter MCP Agent-UX-Audit

Verwende diesen Skill um den AiNetLinter MCP-Server aus der Perspektive eines
konsumierenden Agenten vollständig zu prüfen. Das Ziel ist nicht Vollständigkeit
im Sinne einer Checkliste, sondern echte Qualitätssicherung: Finde was einen
Agenten in der Praxis behindert, und behebe es direkt.

Nutzeranweisungen haben Vorrang. Stoppe nicht nach der ersten Befund-Gruppe.

## Aufruf

Ohne `targetPath` — Skill sucht automatisch nach der ersten `.sln`- oder `.slnx`-Datei
im Repository-Root und führt den Audit im Source-Modus durch (Dogfood-Betrieb
auf dem eigenen Projekt):

```text
$mcp-ux-audit
```

Mit explizitem Pfad:

```text
$mcp-ux-audit

targetPath: C:\Projects\MyApp\AiNetLinter.exe
# oder:
targetPath: C:\Projects\MyApp\AiNetLinter.slnx
```

Der `targetPath` bestimmt den Modus:
- `.sln` / `.slnx` → **Source-Modus**: voller Roslyn-Index, echte Symbol-IDs, Chaining vollständig möglich
- `.dll` / `.exe` → **Assembly-Modus**: Decompile-Stubs, `find_symbol` nur snapshot-basiert, Chaining eingeschränkt ohne `includeReferences=true`

---

## Subagenten-Architektur

Der Audit-Orchestrator hält sich **kontextleicht**: Er akkumuliert nur komprimierte
Befundlisten und den Phasenstatus — keine rohen Tool-Responses.
Die schwere Arbeit (viele Tool-Calls + Response-Texte) läuft in frischen Subagenten.

```
Audit-Orchestrator (leichter Kontext)
│
│  Phase 1: Discovery (inline, kein Subagent)
│
├── Subagent Gruppe A: Health & Handshake
│   └── Ergebnis: komprimierte Befundliste A
├── Subagent Gruppe B: Discovery-Tools
│   └── Ergebnis: komprimierte Befundliste B
├── Subagent Gruppe C: Symbol-Chaining
│   └── Ergebnis: komprimierte Befundliste C
├── Subagent Gruppe D: Fehlerbehandlung
│   └── Ergebnis: komprimierte Befundliste D
├── Subagent Gruppe E: Cross-Tool-Konsistenz
│   └── Ergebnis: komprimierte Befundliste E
│
│  Orchestrator priorisiert: Critical → Major → Minor
│
├── Pro Befund (seriell):
│   ├── Subagent Red-Test-Writer → failing Test + Nachweis rot
│   └── Subagent Implementer    → Fix + Nachweis grün + Diff-Summary
│
└── Abschluss (inline): Build, Nicht-Stress-Tests, Commit, Audit-Report
```

**Jeder Subagent erhält:** `targetPath`, seine Tool-Gruppe aus dem Prüfkatalog,
die Bewertungskriterien und den Modus (Source/Assembly).

**Jeder Subagent gibt zurück:** Nur die strukturierte Befundliste — kein
Rohantworttext, keine vollständigen Tool-Responses.

---

## Phase 1 — Discovery (Orchestrator, inline)

1. `tools/list` auswerten → vollständige Tool-Liste mit Schemas
2. Modus bestimmen (Source vs. Assembly) aus `targetPath`-Endung
3. Modusabhängige Erwartungsanpassungen festhalten (Assembly-Einschränkungen)
4. Subagenten für Gruppen A–E starten (seriell, eine nach der anderen)

---

## Phase 2 — Systematische Tool-Calls (je Subagent)

Jeder Gruppen-Subagent arbeitet seinen Prüfkatalog durch und führt danach
eine **Freie Erkundung** durch. Beide Teile sind Pflicht.

### Pflicht-Prüfkatalog

Siehe Abschnitt „Prüfkatalog Gruppen A–E" weiter unten.

### Freie Erkundung (nach dem Pflichtblock)

Nach den definierten Prüffällen erkundet der Subagent **kreativ und neugierig**:

- **Unerwartete Parameterkombinationen**: Kombiniere Parameter die normalerweise nicht
  zusammen verwendet werden. Beispiel: `get_file_tree` mit `view=files` + `fileFilter`
  auf einem tiefen Unterpfad + `maxResults=1` — was passiert?
- **Verkettete Überraschungen**: Nutze den Output eines Tools als Input für ein Tool,
  das dafür nicht primär gedacht ist. Beispiel: Nimm einen Namespace-Namen aus
  `get_namespace_tree` und übergib ihn direkt als `symbolIdentifier` an `get_feature_context`.
- **Grenzwerte austesten**: Setze `maxResults=0`, `depth=0`, `depth=99`, leere
  Strings wo Optional erwartet wird, sehr lange Strings, Unicode-Zeichen.
- **Deterministik prüfen**: Ruf dasselbe Tool zweimal mit identischen Parametern auf —
  ist die Antwort konsistent und deterministisch?
- **Partiell invalide Eingaben**: Mische gültige und leicht ungültige Parameterwerte
  (z.B. `namePatterns=["ValidClass", ""]`) — partieller Treffer oder Gesamtfehler?
- **Sequenz nach Fehler**: Ruf nach einem `invalid_argument`-Fehler dasselbe Tool
  sofort korrekt auf — ist der Server-State unbeeinträchtigt?
- **Agent-Ersteindruck**: Was würde ein Agent tun, der das Tool zum ersten Mal ohne
  Dokumentation erkundet? Was wirkt überraschend, inkonsistent oder verwirrend?
- **Eigene Ideen**: Der Subagent ist ausdrücklich eingeladen, eigene Prüfideen
  einzubringen, die über diesen Katalog hinausgehen.

Befunde aus der Freien Erkundung werden gleich bewertet wie Pflicht-Befunde.
Interessante Beobachtungen ohne klares Problem werden als `[Minor]` dokumentiert.

---

## Phase 3 — Bewertung

Lies die detaillierten Bewertungskriterien aus `references/assessment-criteria.md`.

Kurzfassung der sieben Kriterien:

| Kriterium | Kernfrage |
|-----------|-----------|
| **Parameter-Naming** | Heißt dasselbe Konzept in verschiedenen Tools unterschiedlich ohne semantische Begründung? |
| **Signal-Rausch-Verhältnis (SNR)** | Hat der Agent nach dieser Antwort eine neue, handlungsrelevante Erkenntnis — oder primär Rauschen? |
| **Fehlerqualität** | Enthält der Fehler Feldname + Korrekturhinweis? Handlungsweisend ohne Stack-Trace? |
| **Response-Struktur** | `structuredContent` + vollständiger `navigation`-Block konsistent vorhanden? |
| **Chaining-Kompatibilität** | Kann der Agent Output von Tool A direkt (ohne Transformation) als Input für Tool B verwenden? |
| **Schema-Vollständigkeit** | Haben alle Parameter `description`? `required` korrekt? Keine Phantom-Properties? |
| **Completeness-Signaling** | Trunkierungen explizit signalisiert? `next`-Hinweis wenn Daten fehlen? |

### Severity-Einstufung

- **Critical**: Blockiert einen Agenten bei Standardaufgaben. Falsches Error-Format,
  Fehler bei gültigen Calls, kaputtes Chaining in Standardsequenzen.
- **Major**: Erschwert Agenten-Arbeit erheblich. Schlechtes SNR bei häufigen Calls,
  inkonsistente Parameter-Naming, fehlendes Completeness-Signal.
- **Minor**: Qualitätsverbesserung, kein Blocker. Inkonsistente Beschreibungstexte,
  unvollständige Schema-Descriptions, interessante Beobachtungen aus Freier Erkundung.

---

## Phase 4 — Red-Test-First pro Befund

Pro Befund (seriell, Critical zuerst):

1. **Befund dokumentieren**: Severity, betroffene Tool(s), Evidenz (konkreter Call +
   Response-Ausschnitt), Reproduktionsschritte
2. **Subagent Red-Test-Writer**: Schreibt failing xUnit-v3-Test
   - Kategorie `Dogfood` für Live-MCP-Calls gegen echten Server
   - Kategorie `Unit` für in-memory-Logik
   - Bevorzugt in bestehende Testdateien einfügen; neue Datei nur wenn kein
     passendes Partial vorhanden
   - Test läuft **rot** — Nachweis durch Testlauf-Ausgabe
3. **Subagent Implementer**: Fix in Tool-Handler, Response-Formatter oder Schema
   - Scope: Tool-Handler, Response-Formatting, Parameter-Naming, Schema-Descriptions
   - Kein Scope: Architektur, neue Features, Breaking-Change-Umbenennung öffentlicher API
   - Test läuft **grün** — Nachweis durch Testlauf-Ausgabe + Diff-Summary
4. Nächster Befund — erst nach abgeschlossenem Fix

### Guardrails für den Fix-Scope

- **Breaking Changes**: Öffentlich dokumentierte Parameter-Umbenennung →
  Befund dokumentieren, Fix **nur** wenn backward-compatible oder explizit
  vom Nutzer freigegeben.
- **Keine Architektur**: Abstraktionen, DI, neue Interfaces → außerhalb des Scopes,
  als Empfehlung im Audit-Report vermerken.
- **Kein `git add .`**: Nur explizite Pfade stagen, Index + Diff vor Commit prüfen.

---

## Phase 5 — Abschluss

1. `dotnet build` (alle 4 Projekte, kein Warning/Error bei `TreatWarningsAsErrors`)
2. `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`
3. `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`
4. Commit pro behobener Befund-Gruppe (Conventional Commit auf Deutsch, imperativ)
5. Audit-Report schreiben: `src/test-output/mcp-ux-audit-<YYYY-MM-DD>.md`

Der Orchestrator beendet sich **nicht** nach der ersten Befund-Gruppe. Er
läuft bis alle Gruppen abgearbeitet, alle Critical/Major-Befunde behoben
und der Abschluss-Gate grün ist.

---

## Prüfkatalog Gruppen A–E

### Gruppe A: Health & Handshake

**Pflicht-Prüffälle:**
- `get_server_health` ohne `targetPath` → globale Antwort, kein `invalid_argument`
- `get_server_health` mit gültigem `.sln`-Target → `capabilities` enthält Lint-Status
- `get_server_health` mit `.dll`-Target → `origin=decompiled`, `lint=unsupported`
- `get_server_health` mit `includeDiagnostics=true` → Diagnostics-Sample vorhanden
- `report_observability_feedback` (kein Target) → kein Fehler, sinnvolle Bestätigung

**Freie Erkundung:** s. Phase 2 — Freie Erkundung

---

### Gruppe B: Discovery-Tools

**Pflicht-Prüffälle:**
- `get_file_tree(view=summary)` → kompakte Übersicht, keine vollständige Dateiliste
- `get_file_tree(view=files, root=.)` ohne `fileFilter` → Truncation oder Warnung signalisiert
- `get_file_tree(view=tree, treeDepth=2)` → sinnvolle Baumansicht
- `get_file_tree(fileFilter=*.cs)` → nur `.cs`-Dateien
- `get_namespace_tree` → sinnvolle Hierarchie, nicht leer
- `get_index_scope` → zeigt `.cs`-Anteil, strukturierte Ausgabe
- Assembly-Modus: `inspect_assembly(targetPath=<dll>)` → public API, kein Fehler
- Assembly-Modus: `find_assembly_extensions(targetPath=<dll>)` → Ergebnis oder leere Liste, kein Fehler

**Freie Erkundung:** s. Phase 2 — Freie Erkundung

---

### Gruppe C: Symbol-Chaining

**Pflicht-Prüffälle (Source-Modus):**
- `find_symbol(namePattern=Get, maxResults=1)` → Truncation-Signal, `next`-Hinweis
- `find_symbol` → Symbol-ID extrahieren → `get_symbol_body(symbolId=<id>)` → Body vorhanden
- `find_symbol` → Symbol-Identifier → `get_feature_context(symbolIdentifier=<name>)` → `declaration.name` im Text
- `get_feature_context` → Caller-Symbol → `find_references(symbolIdentifier=<caller>)` → Treffer
- `find_symbol` → `find_references` → `get_impact` (vollständige Chaining-Sequenz)
- `get_file_skeleton(targetFile=<path>)` → Signaturen ohne Bodies
- `get_class_structure` → Members → `get_symbol_body` für einzelnes Member

**Pflicht-Prüffälle (Assembly-Modus):**
- `find_symbol` ohne `includeReferences` → snapshot-basiert, kein Fehler, reduzierte Erwartung
- `get_symbol_body` mit Assembly-Symbol-ID → Body aus Decompile-Stub vorhanden
- Sequenz-Abbruch bei fehlenden Referenzen klar signalisiert (nicht silent-empty)

**Freie Erkundung:** s. Phase 2 — Freie Erkundung

---

### Gruppe D: Fehlerbehandlung

**Pflicht-Prüffälle:**
- Fehlendes Pflichtfeld (`targetPath` weglassen) → `invalid_argument` mit konkretem Feldnamen
- Nicht-existenter `targetPath` → klare Fehlermeldung, kein Stack-Trace
- Falscher Typ: String statt Array für `namePatterns` → Typ-Fehlermeldung handlungsweisend
- Ungültiger Enum-Wert (z.B. `kind=unknownKind`) → sprechende Fehlermeldung mit Hinweis auf gültige Werte
- Leerer String für Pflichtfeld → klare Fehlermeldung, kein NullReference
- `targetPath` ist ein Verzeichnis statt Datei → `invalid_argument`, kein Crash

**Freie Erkundung:** s. Phase 2 — Freie Erkundung

---

### Gruppe E: Cross-Tool-Konsistenz

**Pflicht-Prüffälle:**
- Parameter-Naming für „Symbol": `symbolIdentifier` vs. `pattern` vs. `namePattern` vs. `name` —
  Unterschiede semantisch begründet und in Schema-Descriptions erklärt?
- Alle zielgebundenen Tools: `structuredContent.navigation`-Block vorhanden und konsistent?
- `completeness`-Feld: Wertemenge über alle Tools gleich (z.B. `full`, `truncated`, `partial`)?
- `capabilities`-Felder in `get_server_health`-Antwort stimmen mit tatsächlichem Tool-Verhalten überein?
- Source vs. Assembly: Gleiche Tools, andere `origin` → Fehlermeldungen bei Nicht-Unterstütztem klar?
- Modusübergänge im gleichen Session-Call → sauberer Reset oder Verschmutzung?

**Freie Erkundung:** s. Phase 2 — Freie Erkundung

---

## Befund-Ausgabeformat

Jeder Gruppen-Subagent gibt zurück:

```
## Befundliste Gruppe <X>: <Name>

### [Severity] Befund <N>: <Kurztitel>
- **Tool(s)**: `<toolname>`, `<toolname>`
- **Evidenz**: Konkreter Call mit Parametern + Response-Ausschnitt (max. 5 Zeilen)
- **Problem**: Was genau ist das Problem aus Agent-Perspektive?
- **Reproduktion**: Minimale Schritte
- **Empfehlung**: Konkreter Fix-Hinweis

### Freie-Erkundungs-Beobachtungen
- <Interessante Beobachtung ohne klares Problem-Flag>
```

Severity: `[Critical]`, `[Major]`, `[Minor]`

---

## Commit-Format

```
fix(mcp-ux): <was behoben, imperativ, Deutsch>

Befunde: <Liste der Befund-Kurztitel>
Kategorie: <Gruppe A/B/C/D/E>
Red-Tests: <Testdatei(en) und Testnamen>
```

---

## Aufruf-Beispiel

```text
$mcp-ux-audit

targetPath: C:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\bin\Release\net10.0\AiNetLinter.exe
```

oder für Quellcode-Modus:

```text
$mcp-ux-audit

targetPath: C:\Daten\Entwicklung\Ralf\AiNetLinter\AiNetLinter.slnx
```

