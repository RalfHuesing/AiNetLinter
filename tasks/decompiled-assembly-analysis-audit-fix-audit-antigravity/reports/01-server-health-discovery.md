# Audit-Report 01: Server Health, Lifecycle, Resource-URIs & Projekt-Discovery

**SubAgent:** SubAgent 1 (Discovery & Lifecycle)  
**Status:** Abgeschlossen  
**Prüfdatum:** 2026-08-31  
**Geprüfte Tools & Ressourcen:** `get_server_health`, `get_file_tree`, `get_index_scope`, `reload_config`, `ainetlinter://agent-guide`, `ainetlinter://overview`, `ainetlinter://rules`

---

## 1. Getestete Szenarien & Ergebnisse

### 1.1 `get_server_health`
- **Globaler Aufruf (ohne Target):**
  - Liefert Version (`1.0.157`), Modus (`daemon`), Uptime, PID, Connection-Zähler, registrierte Projekt-Keys (`SqlToAi`, `AiNetLinter`) und aktive Assembly-Sessions.
  - Antwortzeit: < 15 ms, extrem schlank und token-effizient (~180 Tokens).
- **Projektbezogener Aufruf (`targetType="project"`):**
  - Liefert LoadState (`Loaded`), Solution-Pfad, Config-Pfad, Staleness-Check-Statistiken (kumulierte Millisekunden) und Zeitstempel des letzten guten Zustands.
- **Assemblybezogener Aufruf (`targetType="assembly"`):**
  - Liefert LoadState, Vollständigkeit (`partial`), Origin (`decompiled`), Generation, SHA-256 Hash, GeneratedPath, Confidence (`medium`), Trust (`untrusted`) sowie gekürzte Diagnosen (z. B. 5 von 116 mit `maxDiagnostics=5`).
- **Negativtests & Fehlervalidierung:**
  - `targetType` ohne `targetPath` -> `INVALID_ARGUMENT: Der Parameter 'targetPath' ist erforderlich.`
  - Ungültiger `targetType="foo"` -> `INVALID_ARGUMENT: Der Parameter 'targetType' muss exakt 'project' oder 'assembly' sein.`
  - Nicht-existenter DLL-Pfad -> `INVALID_ARGUMENT: Der Assembly-Pfad muss auf eine vorhandene Datei zeigen.`
  - Alle Fehler enthalten strukturierte `hint`-Blöcke mit konkreter Korrekturanweisung.

### 1.2 `get_file_tree`
- **Summary-View (`view="summary"`):**
  - Liefert eine hochgradig token-effiziente Projektübersicht (1033 Dateien, 6,4 MB) mit Extension-Aufschlüsselung (`.cs: 877`, `.md: 103`, etc.) und Verzeichnis-Baum aggregiert nach Dateien und KB.
  - Token-Footprint: ~250 Tokens (entspricht perfekt den Richtlinien in `AiNetLinter-McpWorkflow.mdc`).
- **Tree-View (`view="tree"`, `treeDepth=1..2`):**
  - Zeigt Verzeichnishierarchie bis zur gewünschten Tiefe.
- **Assembly-Target-Abweisung:**
  - Aufruf mit `targetType="assembly"` liefert `[ASSEMBLY] capability=unsupported; status=unsupported` und Fehlercode `ASSEMBLY_TARGET_UNSUPPORTED` mit klarem Hinweis auf Roslyn-Abfragen oder Projekt-Target.

### 1.3 `get_index_scope`
- Liefert schnelle Übersicht über die Roslyn-Symbolgraph-Abdeckung (856 `.cs` Dateien voll abgedeckt, Nicht-C#-Dateien als nicht abgedeckt gekennzeichnet).

### 1.4 `reload_config`
- Führt atomaren Reload von `rules.json` durch und meldet Vorher-/Nachher-Zustand der aktivierten Regeln (z. B. 17 aktivierte Regeln, unverändert).

### 1.5 Resource-URIs (`read_resource`)
- `ainetlinter://agent-guide`:
  - Enthält Onboarding-Schritte, Definitionsdatei-Template (`ainetlinter.project.json`), dauerhafte Agentenregel (`AiNetLinter-McpWorkflow.mdc`) und dynamisch aufgelösten Laufzeit-Pfad der Binary.
- `ainetlinter://overview?projectRoot=...`:
  - Liefert kompakten Markdown-Projektstatus mit Verweisen auf nachfolgende Analyseschritte.
- `ainetlinter://rules?projectRoot=...`:
  - Liefert eine vollständige, tabellarische Markdown-Darstellung aller aktiven Linter-Regeln, Absichten (Intents), Schweregrade, Beschreibungen, Schwellwerte, deaktivierten Regeln und Pfad-Overrides. Hervorragende Orientierung für Agenten!

---

## 2. Befunde & Beobachtungen

### Befund DISCO-001 (S3 / U0 / P3): Schema vs. Beschreibung Diskrepanz bei `get_file_tree.maxResults`
- **Beschreibung:** Im JSON-Schema von `get_file_tree.json` ist `"maxResults": { "default": 200, "type": "integer" }` hinterlegt. Die Freitext-Beschreibung im selben Toolschema besagt jedoch: `"maxResults: Begrenzung (Default 100, Maximum 2000)"`.
- **Auswirkung:** Geringfügige Diskrepanz zwischen Schema-Default (200) und Dokumentationstext (100). Verwirrt Agenten nicht kritisch, sollte aber konsolidiert werden (beides auf 100 oder 200).
- **Klassifizierung:** Schweregrad `S3` (Minor), Umfang `U0` (Lokal), Dringlichkeit `P3`.

---

## 3. Fazit SubAgent 1
Discovery, Server Health, Lifecycle und Resource-URIs arbeiten äußerst zuverlässig, robust und token-ökonomisch. Die Fehlerbehandlung und Hint-Struktur ist vorbildlich agentengerecht.
