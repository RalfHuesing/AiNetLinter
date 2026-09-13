# MCP-Audit – Gruppe A: Health, Handshake & Runtime-Config

> **Zuständigkeit:** Subagent A
> **Tools:** `get_server_health`, `reload_config`
> **Regeln:** Nur negative Befunde protokollieren. Keine echten Produktnamen/Pfade, nur Ziel-Labels (`SOURCE-01`, `LOCAL-01`, etc.)!

---

## 1. Übersicht & Testergebnis

- **Geprüfte Tools:** `get_server_health`, `reload_config`
- **Geprüfte Prüffälle:** TC-A01, TC-A02, TC-A03 (inkl. Stichprobe `LOCAL-03` und `includeDiagnostics`), TC-A04, TC-A05 (inkl. `SOURCE-02` und Negativfall `LOCAL-01`)
- **Zusatzreiz:** Fehlbedienung `targetPath` auf nicht existente Provider-JSON (`GIT-01`) — recoverable `INVALID_ARGUMENT`
- **Gefundene Befunde:** 0 Critical, 0 Major, 0 Minor

---

## 2. Negative Befunde
