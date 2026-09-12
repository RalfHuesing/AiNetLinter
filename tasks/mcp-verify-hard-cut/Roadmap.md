# Roadmap – MCP-Qualitätsprüfung auf `verify` konsolidieren

- [X] Slice 01 – Öffentlichen `verify`-Vertrag festlegen und rot absichern
- [X] Slice 02 – Gate-Kern mit festen `10.0`/`0`-Invarianten implementieren
- [X] Slice 03 – Kontextgebundene Kandidaten in `changes` integrieren
- [ ] Slice 04 – Harter Schnitt durch Registrierung, Produktion und Tests
- [ ] Slice 05 – Dokumentation, Regeln und Endverifikation synchronisieren
- [ ] Gesamtaudit – gesamten Scope prüfen und Findings beheben
- [ ] Abschlussgate – Build, Non-Stress-Tests und Diff-Prüfung

## Durchführungsprotokoll

### Slice 01

- Vertragsmodelle und rote Inventory-/E2E-Verträge für `verify` ergänzt.
- Modelltests und Build grün; Runtime-Verträge erwartungsgemäß rot, da `verify` noch nicht registriert ist.
- Commit: `f9315ba1d85b4e5ab605786ea4193efd12cf60d5`.

### Slice 02

- `verify` führt den festen Score-/Violation-Gatekern inklusive Contract-v2-Content-Projektion aus.
- Build und sechs fokussierte E2E-Verträge grün; Legacy-Inventar bleibt bis Slice 04 absichtlich rot.
- Commit: `d8194926043b7e4742b667728be417eed2dacf63`.

### Slice 03

- Dead-Code- und Magic-Value-Scanner liefern nur für `changes` advisory-Evidenz mit expliziter Unsicherheit.
- Deterministische Projektion, vollständige Counts, Nichtblockierung und Solution-Ausschluss fokussiert grün geprüft.
- Commit: `7c6884588bd9c8f956d23b18088fae18bed2cd37`.

### Vorbereitung

- Roadmap aus dem freigegebenen Konzept angelegt.
- Baseline: `72ac838c1e9aab6fe68ba6295f86d21206269dd5`; Working Tree war sauber.
- Commit: ausstehend.
