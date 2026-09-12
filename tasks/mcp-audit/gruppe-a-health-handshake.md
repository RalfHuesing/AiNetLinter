# MCP-Audit – Gruppe A: Health, Handshake & Runtime-Config

> **Zuständigkeit:** Subagent A
> **Tools:** `get_server_health`, `reload_config`
> **Regeln:** Nur negative Befunde protokollieren. Keine echten Produktnamen/Pfade, nur Ziel-Labels (`SOURCE-01`, `LOCAL-01`, etc.)!

---

## 1. Übersicht & Testergebnis

- **Geprüfte Tools:** `get_server_health`, `reload_config`
- **Geprüfte Prüffälle:** TC-A01 bis TC-A05 aus `FlightPlan.md`
- **Gefundene Befunde:** 0 Critical, 6 Major, 2 Minor

---

## 2. Negative Befunde

### [Major] A-01: Ungebundener Health-Call legt Prozess- und Verbindungs-IDs offen

- **Betroffenes Tool / Schema**: `get_server_health` (Parameter: ungebunden, ohne `targetPath`)
- **Ziel-Label**: ungebunden (Modus: global)
- **Konkreter Aufruf**: `get_server_health()`
- **Beobachtung / Ist-Verhalten**: Markdown-Health mit Version, Mode `daemon`, Uptime. Zusätzlich ungefilterte Felder `PID` (numerische Prozess-ID) und `connectionId`. Kein Hostnamen-Leak beobachtet. Envelope wirkte erfolgreich (`isError` nicht gesetzt).
- **Soll-Verhalten / Problem aus Agentensicht**: TC-A01 verlangt Datensparsamkeit: keine ungefilterten Prozess-/Host-IDs. Ein Agent (und jeder Log-Mitleser) erhält Betriebssystem-Identifikatoren, die für Handshake und Capability-Erkennung nicht nötig sind.
- **Empfehlung**: `PID` und rohe `connectionId` aus der Standardantwort entfernen oder hinter ein explizites Opt-in (`includeDiagnostics` / Operator-Flag) legen. Global-Health auf Version, Uptime, Session-Aggregate und Fehlerzähler beschränken.

### [Major] A-02: Source-Health ohne maschinenlesbare Capabilities und ohne klaren Roslyn-Index

- **Betroffenes Tool / Schema**: `get_server_health` (Parameter: `targetPath`)
- **Ziel-Label**: `SOURCE-01` (Modus: Source)
- **Konkreter Aufruf**: `get_server_health(targetPath="<SOURCE-01>")` sowie Wiederholung mit `includeDiagnostics=true`, `maxDiagnostics=5`
- **Beobachtung / Ist-Verhalten**: Nur ein Projektblock mit `LoadState: Loaded`, Wiederholung von `targetPath`/`Solution`/`Config`, Nutzungszeit, Refresh- und Staleness-Zählern. Weder `capabilities` (Syntax/Lint) noch Indexumfang (Dokumente, Compilation, Analyzer-Status, `lint=configured|not_configured`). `includeDiagnostics` änderte die Felder nicht; keine Diagnoseliste, kein Contract-v2-`navigation.status` im Content.
- **Soll-Verhalten / Problem aus Agentensicht**: Handshake soll Syntax- vs. Lint-Fähigkeit und Roslyn-Index maschinenlesbar ausweisen. Ein Agent muss raten, ob Lint verfügbar ist, und kann Indexreife nicht von „Loaded“ unterscheiden — das blockiert die Entscheidung Navigation vs. Lint-Tools.
- **Empfehlung**: Stabilen Block `capabilities.syntax` / `capabilities.lint` plus Indexstatus (`documents`, `compilations`, `indexState`) und `navigation.status.operation|completeness` im Content liefern. Werte als Literale (`ok`, `not_configured`, `unsupported`), nicht als Fließtext.

### [Major] A-03: Assembly-Health ohne `lint=unsupported` und mit widersprüchlichen Completeness-Signalen

- **Betroffenes Tool / Schema**: `get_server_health` (Parameter: `targetPath`)
- **Ziel-Label**: `LOCAL-01` (Modus: Assembly); gleicher Musterbefund bei `LOCAL-03` (verwaltete EXE)
- **Konkreter Aufruf**: `get_server_health(targetPath="<LOCAL-01>")` und `get_server_health(targetPath="<LOCAL-01>", includeDiagnostics=true, maxDiagnostics=5)`
- **Beobachtung / Ist-Verhalten**: `Origin: decompiled` ist gesetzt. Feld `lint=unsupported` fehlt. Parallel: `LoadState: partial`, `Vollständigkeit: partial`, `Fehlerphase: decompilation`, `Confidence: medium`, plus Handlungszeile „Scope oder Detaillevel verfeinern und die Antwort gezielt wiederholen“. Ohne `includeDiagnostics`: `Diagnosen: 0 von 111 (gekürzt)` — klingt nach Budgetkürzung, obwohl Samples nicht angefordert waren. Im Content kein getrenntes Envelope-`completeness` neben der fachlichen Partial-Qualität. Dieselbe Signatur auf `LOCAL-03`.
- **Soll-Verhalten / Problem aus Agentensicht**: Vertrag: `origin=decompiled`, `lint=unsupported`; fachliche Partial-Diagnostics getrennt von Antwort-Completeness. Agent liest `Fehlerphase` + Retry-Hinweis als Fehlschlag und wiederholt Health nutzlos, oder versucht Lint-Tools trotz Decompiled-Modus.
- **Empfehlung**: `lint=unsupported` explizit setzen. `Fehlerphase` nur bei echtem Fehler. Retry-Hinweis nicht an Health hängen. Unangeforderte Diagnosen als `omitted`/`notRequested` statt `gekürzt`. `navigation.status.completeness` (Antwort) von fachlichem `analysisCompleteness=partial` trennen.

### [Major] A-04: Global-Health zählt geladene Source-Projekte als 0, Assembly-Sessions aber mit

- **Betroffenes Tool / Schema**: `get_server_health` (Parameter: ungebunden)
- **Ziel-Label**: ungebunden, nach vorangegangenem `SOURCE-01`- und `LOCAL-01`-Health
- **Konkreter Aufruf**: `get_server_health()` (zweiter Lauf nach TC-A02/TC-A03)
- **Beobachtung / Ist-Verhalten**: `## Projekte (0)` trotz `LoadState: Loaded` im zielgebundenen Source-Health. `## Assembly-Sessions (1)` mit `Sessions gesamt: 1`, `Statusverteilung: partial=1`. Hinweis „Sessiondetails global unterdrückt“ gilt offenbar nur für Details, nicht für die Zählung — und dann inkonsistent zwischen Source und Assembly.
- **Soll-Verhalten / Problem aus Agentensicht**: Schema verspricht residente Projekt- und Assembly-Sessions. Ein Agent hält die Source-Solution für unbelegt und startet unnötige Reloads oder zweifelt am Handshake.
- **Empfehlung**: Globale Aggregate für Source- und Assembly-Sessions symmetrisch zählen (`Projekte gesamt`, `LoadState`-Verteilung). Detailunterdrückung beibehalten, Zähler nicht auf 0 setzen, wenn ein Target `Loaded` ist.

### [Major] A-05: `reload_config` bestätigt Erfolg nur als unstrukturierten Freitext

- **Betroffenes Tool / Schema**: `reload_config` (Parameter: `targetPath`, Pflichtlaut Schema)
- **Ziel-Label**: `SOURCE-01` (Modus: Source)
- **Konkreter Aufruf**: `reload_config(targetPath="<SOURCE-01>")`
- **Beobachtung / Ist-Verhalten**: Eine Prosazeile sinngemäß: Konfig neu geladen, Anzahl aktivierter Regelchecks, Anzahl Metrikgrenzwerte, „Snapshot geändert“. Keine Felder `success`, `enabledRuleCount`, `snapshotChanged`, `navigation.status`. Kein maschinenlesbarer Vorher/Nachher-Vergleich, ob sich der Snapshot wirklich geändert hat oder nur neu gelesen wurde.
- **Soll-Verhalten / Problem aus Agentensicht**: TC-A05 erwartet strukturierte Statusbestätigung. Chaining (Reload → Lint) zwingt zu Sprach-Parsing; ein stiller Teilerfolg ist nicht unterscheidbar.
- **Empfehlung**: Contract-v2-Block mit `operation=ok`, `enabledRuleChecks`, `metricLimits`, `snapshotChanged` (bool), optional `snapshotId`. Prosazeile höchstens zusätzlich.

### [Major] A-06: Diagnose-Samples im Assembly-Health enthalten volle Dritt-Installationspfade

- **Betroffenes Tool / Schema**: `get_server_health` (Parameter: `includeDiagnostics`, `maxDiagnostics`)
- **Ziel-Label**: `LOCAL-01` (Modus: Assembly)
- **Konkreter Aufruf**: `get_server_health(targetPath="<LOCAL-01>", includeDiagnostics=true, maxDiagnostics=5)`
- **Beobachtung / Ist-Verhalten**: `Diagnosen: 5 von 111 (gekürzt)`. Samples enthalten Referenzauflösungs- und Decompiler-Meldungen mit vollständigen Dateisystempfaden Dritt-Assemblies (Directory + Dateiname). `maxDiagnostics=5` wurde zahlenmäßig eingehalten.
- **Soll-Verhalten / Problem aus Agentensicht**: Datensparsamkeit: für Capability-/Health-Handshake reichen Diagnose-*Codes* und gekürzte Dateinamen. Volle Installationspfade wandern in Agent-Logs und verletzen IP-/Pfadschutz.
- **Empfehlung**: Pfade in Health-Samples auf Dateinamen (oder Label-Token) reduzieren; voller Pfad nur in einem expliziten Detail-Tool. Codes (`CS0234`, Begrenzungen) beibehalten.

### [Minor] A-07: Schema-Text und JSON-Default für `maxDiagnostics` widersprechen sich

- **Betroffenes Tool / Schema**: `get_server_health` (Parameter: `maxDiagnostics`)
- **Ziel-Label**: ungebunden / tool description vs. `inputSchema`
- **Konkreter Aufruf**: Schema via `tools/list`; Lauf `includeDiagnostics=true` ohne eigenes `maxDiagnostics` (global) bzw. mit `maxDiagnostics=5` (`LOCAL-01`)
- **Beobachtung / Ist-Verhalten**: Tool-Beschreibung nennt `maxDiagnostics: Default 50`. `inputSchema.properties.maxDiagnostics.default` ist `20`.
- **Soll-Verhalten / Problem aus Agentensicht**: Uneinheitliche Defaults führen zu falschen Retry-/Sample-Annahmen.
- **Empfehlung**: Beschreibung und Schema auf denselben Default ziehen (einer von beiden, dokumentiert).

### [Minor] A-08: Assembly-Ablehnung von `reload_config` mit irreführendem Roslyn-Hinweis

- **Betroffenes Tool / Schema**: `reload_config` (Parameter: `targetPath`)
- **Ziel-Label**: `LOCAL-01` (Modus: Assembly)
- **Konkreter Aufruf**: `reload_config(targetPath="<LOCAL-01>")`
- **Beobachtung / Ist-Verhalten**: Recoverable, kein Crash, kein Stacktrace. Content: `capability=unsupported`, `[ERROR]: ASSEMBLY_TARGET_UNSUPPORTED`. Hint empfiehlt eine „unterstützte Roslyn-Abfrage“ *oder* `targetPath` auf `.sln`/`.slnx`. Für dieses Tool ist nur Solution-Config zulässig; eine Roslyn-Abfrage ändert nichts.
- **Soll-Verhalten / Problem aus Agentensicht**: Fehlercode ist brauchbar, der Hint steuert den Agenten auf den falschen Tool-Typ.
- **Empfehlung**: Tool-spezifischen Hint: nur `targetPath` auf `.sln`/`.slnx`; keine generische „Roslyn-Abfrage“-Formel.
