# Gruppe A: Health & Handshake — Befundbericht

## Geprüfte Tools & Ressourcen
- `get_server_health`
- `report_observability_feedback`
- Resource `ainetlinter://agent-guide`
- Resource `ainetlinter://overview{?targetPath}`
- Resource `ainetlinter://rules{?targetPath}`

---

### Status Gruppe A
Alle Befunde in Gruppe A (einschließlich F-05: On-Demand Leasing bei ungeladenem Source-Target in `get_server_health`) wurden erfolgreich behoben und verifiziert. Aktuell liegen keine offenen Befunde in Gruppe A vor.

---

### Beobachtungen aus Freier Erkundung (Gruppe A)
- **Positiv**: `get_server_health` ohne Target liefert eine saubere globale Übersicht mit Uptime, Daemon-Keys, PID und Status aller residenten Sessions ohne Fehler.
- **Positiv**: Resource `ainetlinter://agent-guide` ist offline und ohne Target lesbar und bietet perfekten Bootstrap-Kontext für neue Agenten.
- **Hinweis zu Resources**: Die Resources `overview` und `rules` verlangen RFC-6570 Query-Expansion (`ainetlinter://overview?targetPath=...`). Agenten, die MCP-Resources über standardisiertes URI-Matching lesen, müssen den Query-String URL-encoden. Dies ist in den Schemas korrekt dokumentiert.
