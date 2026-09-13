# MCP-Audit – Gruppe A: Health, Handshake & Runtime-Config

> **Zuständigkeit:** Subagent A
> **Tools:** `get_server_health`, `reload_config`
> **Regeln:** Nur negative Befunde protokollieren. Keine echten Produktnamen/Pfade, nur Ziel-Labels (`SOURCE-01`, `LOCAL-01`, etc.)!

---

## 1. Übersicht & Testergebnis

- **Geprüfte Tools:** `get_server_health`, `reload_config`
- **Geprüfte Prüffälle:** TC-A01 bis TC-A05 aus `FlightPlan.md`
- **Gefundene Befunde:** 0 Critical, 2 Major, 1 Minor

---

## 2. Negative Befunde

### [Major] A-05: `reload_config` bestätigt Erfolg nur als unstrukturierten Freitext

- **Betroffenes Tool / Schema**: `reload_config` (Parameter: `targetPath`, Pflichtlaut Schema)
- **Ziel-Label**: `SOURCE-01` (Modus: Source)
- **Konkreter Aufruf**: `reload_config(targetPath="<SOURCE-01>")`
- **Beobachtung / Ist-Verhalten**: Eine Prosazeile sinngemäß: Konfig neu geladen, Anzahl aktivierter Regelchecks, Anzahl Metrikgrenzwerte, „Snapshot geändert“. Keine Felder `success`, `enabledRuleCount`, `snapshotChanged`, `navigation.status`. Kein maschinenlesbarer Vorher/Nachher-Vergleich, ob sich der Snapshot wirklich geändert hat oder nur neu gelesen wurde.
- **Soll-Verhalten / Problem aus Agentensicht**: TC-A05 erwartet strukturierte Statusbestätigung. Chaining (Reload → Lint) zwingt zu Sprach-Parsing; ein stiller Teilerfolg ist nicht unterscheidbar.
- **Empfehlung**: Contract-v2-Block mit `operation=ok`, `enabledRuleChecks`, `metricLimits`, `snapshotChanged` (bool), optional `snapshotId`. Prosazeile höchstens zusätzlich.

### [Minor] A-08: Assembly-Ablehnung von `reload_config` mit irreführendem Roslyn-Hinweis

- **Betroffenes Tool / Schema**: `reload_config` (Parameter: `targetPath`)
- **Ziel-Label**: `LOCAL-01` (Modus: Assembly)
- **Konkreter Aufruf**: `reload_config(targetPath="<LOCAL-01>")`
- **Beobachtung / Ist-Verhalten**: Recoverable, kein Crash, kein Stacktrace. Content: `capability=unsupported`, `[ERROR]: ASSEMBLY_TARGET_UNSUPPORTED`. Hint empfiehlt eine „unterstützte Roslyn-Abfrage“ *oder* `targetPath` auf `.sln`/`.slnx`. Für dieses Tool ist nur Solution-Config zulässig; eine Roslyn-Abfrage ändert nichts.
- **Soll-Verhalten / Problem aus Agentensicht**: Fehlercode ist brauchbar, der Hint steuert den Agenten auf den falschen Tool-Typ.
- **Empfehlung**: Tool-spezifischen Hint: nur `targetPath` auf `.sln`/`.slnx`; keine generische „Roslyn-Abfrage“-Formel.
