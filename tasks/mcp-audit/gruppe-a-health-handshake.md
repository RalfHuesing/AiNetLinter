# MCP-Audit – Gruppe A: Health, Handshake & Runtime-Config

> **Zuständigkeit:** Subagent A
> **Tools:** `get_server_health`, `reload_config`
> **Regeln:** Nur negative Befunde protokollieren. Keine echten Produktnamen/Pfade, nur Ziel-Labels (`SOURCE-01`, `LOCAL-01`, etc.)!

---

## 1. Übersicht & Testergebnis

- **Geprüfte Tools:** `get_server_health`, `reload_config`
- **Geprüfte Prüffälle:** TC-A01, TC-A02, TC-A03 (inkl. Stichprobe `LOCAL-03` und `includeDiagnostics`), TC-A04, TC-A05 (inkl. `SOURCE-02` und Negativfall `LOCAL-01`)
- **Zusatzreiz:** Fehlbedienung `targetPath` auf nicht existente Provider-JSON (`GIT-01`) — recoverable `INVALID_ARGUMENT`
- **Gefundene Befunde:** 0 Critical, 0 Major, 1 Minor

---

## 2. Negative Befunde

### [Minor] A-03: Schema und Beschreibung widersprechen sich bei `maxDiagnostics`

- **Betroffenes Tool / Schema**: `get_server_health` (Parameter: `maxDiagnostics`)
- **Ziel-Label**: `LOCAL-01` (Modus: Assembly) / ungebunden (Schema)
- **Konkreter Aufruf**: `get_server_health(targetPath=LOCAL-01, includeDiagnostics=true)` ohne `maxDiagnostics`
- **Beobachtung / Ist-Verhalten**:
  - Tool-Beschreibung: `maxDiagnostics: Default 50`.
  - JSON-Schema: `"default": 20`.
  - Live-Antwort: `Diagnosen: 20 von 111 (gekürzt)` — das Schema-Default gilt, die Beschreibung nicht.
- **Soll-Verhalten / Problem aus Agentensicht**:
  - Agenten, die der Beschreibung folgen, erwarten 50 Samples und können Diagnosen als unvollständig oder budgetgestutzt fehlinterpretieren. Zwei konkurrierende Defaults im Handshake-Schema.
- **Empfehlung**:
  - Beschreibung und `inputSchema.properties.maxDiagnostics.default` auf denselben Wert ziehen (20 oder 50). Ein Default, eine Quelle.
