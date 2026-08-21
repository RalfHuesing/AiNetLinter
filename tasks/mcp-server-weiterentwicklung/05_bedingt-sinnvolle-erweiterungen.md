---
status: vorschlag (je mit Evidenzbedarf)
priority: P2-P3
last_updated: 2026-08-21
kompatibilitaet: widerspricht keiner Entscheidung in 90_bewusst-nicht-umsetzen.md
---

# 05 — Bedingt sinnvolle Erweiterungen (jede mit Evidenzbedarf)

Vier Kandidaten, bewusst klein und additiv gehalten. Jeder nennt die Bedingung, unter der
er umgesetzt werden sollte — analog zur Entscheidungsregel der 90er-Datei.

## 1. Effektive Regelkonfiguration als MCP-Resource (`ainetlinter://rules`)

**Lücke:** Ein Agent sieht Violations (`get_violations`), aber nicht die aktiven Schwellwerte
(MaxMethodLineCount=42? MaxCyclomaticComplexity=?). Um "darf ich diese Methode so lassen?"
zu beantworten, muss der Agent raten oder `rules.json` selbst suchen/lesen — außerhalb des
Servers, mit Pfad-Raten (Default vs. projekteigene Config).

**Vorschlag:** Resource `ainetlinter://rules` (Markdown, analog `ainetlinter://overview`,
frisch pro Read generiert via `GetConfigSnapshot()`): aktive Regeln + effektive Schwellwerte +
Config-Herkunft (Default oder Pfad). Alternativ als Sektion im bestehenden Overview.

**Aufwand:** klein (eine Registration + Formatter + Paritätstest). Kein neues Tool.
**Evidenzbedarf:** gering — die Information existiert serverseitig bereits, sie ist nur
nicht exponiert. Eher Vollendung denn Neuerung.

## 2. MCP Prompts Primitive (standardisierte Workflows)

**Verifiziert:** 0 Treffer auf `McpServerPrompt` im Code — das dritte MCP-Primitive neben
Tools und Resources bleibt ungenutzt.

**Idee:** 2–3 Prompts (`pre-edit-context`, `quality-gate`, `refactor-drift-check`), die die
in `OverviewResourceRegistration` empfohlenen Workflow-Ketten als wiederverwendbare
Vorlagen exponieren.

**Dagegen spricht:** Host-Support für `prompts/list` ist heterogen; viele Coding-Agenten
(Cursor, Copilot) ignorieren Prompts komplett. Die Workflows sind heute schon in Instructions
(2557 Bytes) und Overview transportiert.

**Bedingung:** Nur umsetzen, wenn die Call-Log-Analyse (Datei 02) zeigt, dass Agenten die
empfohlenen Ketten **nicht** von selbst laufen, oder wenn ein konkreter Ziel-Host Prompts
unterstützt. Sonst: verworfene Idee, in 90er-Datei nachtragen.

## 3. Progress-Notification statt Loading-Poll-Schleife

**Verifiziert:** Während des Hintergrund-Loads antwortet jeder Tool-Call mit
`McpToolResults.Loading()` ("Bitte in wenigen Sekunden erneut versuchen"). Der Client muss
pollen. Wie oft das real passiert, ist unbekannt — genau das könnte die Log-Analyse aus
Datei 02 messbar machen (Loading-Retry-Bursts).

**Idee:** SDK-seitig prüfen, ob Fortschritt/Status als Notification sendbar ist; alternativ
im Loading-Text eine konkrete Metadaten-Zusage ("typisch <N s, Solution <Name>") statt der
vagen Formulierung.

**Bedingung:** Erst Messung (Datei 02), dann SDK-Fähigkeitscheck. Bei seltenen Loading-Fällen:
nur den Text präzisieren (Minimaleingriff).

## 4. Multi-Solution-Unterstützung

**Heutiger Vertrag:** Eine Solution pro Prozess (`--solution` beim Start, resident).
Cross-Repo-Agenten starten mehrere Server-Instanzen — das ist ein sauberes, dokumentiertes
Muster und hält jede Instanz einfach (ein `_lock`, ein Catalog, ein Refresh-Zyklus).

**Idee (bewusst zurückgestellt):** `load_solution`-Tool zur Laufzeit. Wäre state-changing,
bräuchte Invalidierung aller laufenden Scans, neue Fehlerzustände (Load während Scan),
und vergrößert den Toolkatalog.

**Bedingung:** Nur wenn Call-Log/Doku-Anfragen zeigen, dass Multi-Server-Setup in der Praxis
scheitert (Port-/Prozess-Limits eines Hosts, Konfigurationshürden). Bis dahin: nicht
umsetzen — nah an Verworfenem, daher hohe Beweislast.

## Zusammenfassung

| # | Vorschlag | Aufwand | Evidenzbedarf | Entscheidungsempfehlung |
|---|---|---|---|---|
| 1 | `ainetlinter://rules` | klein | gering | **umsetzen** |
| 2 | Prompts | mittel | hoch | zurückstellen bis Nutzungsdaten |
| 3 | Progress/Loading-Präzisierung | klein-mittel | mittel | Messung aus Datei 02 abwarten |
| 4 | Multi-Solution | groß | sehr hoch | zurückstellen (hohe Beweislast) |
