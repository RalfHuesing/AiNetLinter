# Roadmap – MCP-Qualitätsprüfung auf `verify` konsolidieren

- [X] Slice 01 – Öffentlichen `verify`-Vertrag festlegen und rot absichern
- [ ] Slice 02 – Gate-Kern mit festen `10.0`/`0`-Invarianten implementieren
- [ ] Slice 03 – Kontextgebundene Kandidaten in `changes` integrieren
- [ ] Slice 04 – Harter Schnitt durch Registrierung, Produktion und Tests
- [ ] Slice 05 – Dokumentation, Regeln und Endverifikation synchronisieren
- [ ] Gesamtaudit – gesamten Scope prüfen und Findings beheben
- [ ] Abschlussgate – Build, Non-Stress-Tests und Diff-Prüfung

## Durchführungsprotokoll

### Slice 01

- Vertragsmodelle und rote Inventory-/E2E-Verträge für `verify` ergänzt.
- Modelltests und Build grün; Runtime-Verträge erwartungsgemäß rot, da `verify` noch nicht registriert ist.
- Commit: ausstehend.

### Vorbereitung

- Roadmap aus dem freigegebenen Konzept angelegt.
- Baseline: `72ac838c1e9aab6fe68ba6295f86d21206269dd5`; Working Tree war sauber.
- Commit: ausstehend.
