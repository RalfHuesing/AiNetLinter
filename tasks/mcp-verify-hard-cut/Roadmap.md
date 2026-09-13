# Roadmap – MCP-Qualitätsprüfung auf `verify` konsolidieren

- [X] Slice 01 – Öffentlichen `verify`-Vertrag festlegen und rot absichern
- [X] Slice 02 – Gate-Kern mit festen `10.0`/`0`-Invarianten implementieren
- [X] Slice 03 – Kontextgebundene Kandidaten in `changes` integrieren
- [X] Slice 04 – Harter Schnitt durch Registrierung, Produktion und Tests
- [X] Slice 05 – Dokumentation, Regeln und Endverifikation synchronisieren
- [X] Gesamtaudit – gesamten Scope prüfen und Findings beheben
- [X] Abschlussgate – Build, Non-Stress-Tests und Diff-Prüfung
- [X] Folgeaudit 01 – feste UTF-8-Budgetprojektion mit vollständigen Evidenzeinheiten
- [X] Folgeaudit 02 – Advisory-Scans für Mehrdatei-Änderungen bündeln
- [X] Folgeaudit 03 – Fehlervertrag und Legacy-Namensreste bereinigen
- [X] Folgeaudit – Scope erneut prüfen und Abschlussgate ausführen

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

### Slice 04

- Vier öffentliche Legacy-Tools samt Registrierungen, Adaptern und positiven Tooltests entfernt; Scanner gehören nun `verify`.
- Inventar- und Runtime-Negativtests grün; Explorations- und Metrikwerkzeuge bleiben erhalten.
- Commit: `7a10be9054ca454a800c745fb27e92c7e07d5b7a`.

### Slice 05

- MCP-Referenz, Integrationsguide, Laufzeithinweise, Agentenregeln und Audit-Prompt auf den Endvertrag umgestellt.
- Doku-Smoke und Legacy-Negativsuche grün; vollständiges Gate folgt nach Gesamtaudit.
- Commit: `10af0ca5367d210370f74a6029ef2ff6da35397b`.

### Gesamtaudit

- Frischer Audit korrigierte Mehrdatei-/Git-Population, konservative Konfigurationserweiterung und verbleibende Content-only-Texte.
- Frische Hosttests bestätigen Inventar, Scope, Advisory und Content-only; vollständiges Gate steht noch aus.
- Commit: `5b95b8be7f190a6d70ac2b1b6cf8c4f4dfc0c6b2`.

### Abschlussgate

- Nachlauf entfernte einen obsoleten Legacy-Tooltest (`47223793`) und verflachte Verify-Hinweisquellen (`6b98b410`).
- `dotnet build`: 0 Warnungen/Fehler; FastTests: 2473/2473; IntegrationTests: 230/230 (ohne `Stress`).
- `git diff --check` und Working Tree sauber; Commit: `d4c145a290353932eebb7586b53824dc9560d42e`.

### Folgeaudit – Vorbereitung

- Read-only-Audit identifizierte fehlende finale UTF-8-Budgetmessung und wiederholte Advisory-Scans pro Datei.
- Öffentliche Legacytools bleiben entfernt; keine Rückkehr zu granularen Legacy-Einstiegspunkten.
- Commit: `2b3eac32bcf61c5df4cb1abedd6ea3dcce2f00f7`.

### Folgeaudit 01

- Finale `verify`-Projektion misst den UTF-8-Content gegen ein festes 4-KiB-Budget und behält nur ganze Evidenzeinheiten.
- Counts, Trunkierungsgrund und `failed`-Evidenzschutz sind per Fast- und frischem Hosttest abgesichert.
- Commit: `487babd11cc7c646a2613ca7d0d8611b132e506b`.

### Folgeaudit 02

- Advisory-Scanner verarbeiten die gesamte geänderte Dateimenge jeweils einmal statt pro Datei erneut zu traversieren.
- Mehrdatei-Regression sichert aggregierte Counts, deterministische Reihenfolge und Gate-Isolation.
- Commit: `ab0b342c5d5488449600e88c937782364d3dc622`.

### Folgeaudit 03

- Fehlende, ungültige und nicht vorhandene `targetPath`-Ziele liefern vor dem Lease Contract-v2-Fehler; ein Folgeaufruf bleibt nutzbar.
- Interne Scanner tragen Verify-/Advisory-Namen; frühere Toolnamen erscheinen nur noch in Negativinventaren.
- Commit: `a986e74bcf21e7fb80c4ab3a5acd94249f048494`.

### Folgeaudit – Abschluss

- Frischer Audit korrigierte die groß-/kleinschreibungsunabhängige Assembly-Zielklassifikation; nachgezogene Verträge und interne Namen sind konsistent.
- Abschlussgate: Build 0 Warnungen/Fehler, FastTests 2481/2481 und IntegrationTests 235/235 (ohne `Stress`).
- Nachträge: `107508228e17f389d909616fe86102df217c356b`, `50460f1fba0ef8ff457e02ec572936e642b00a84`, `cf04cd73d5423afd93c746d4e38a9e9531bfc48b`.

### Vorbereitung

- Roadmap aus dem freigegebenen Konzept angelegt.
- Baseline: `72ac838c1e9aab6fe68ba6295f86d21206269dd5`; Working Tree war sauber.
- Commit: `ca037a1b742ec4b2c8479c949f1a5836a247197a`.
