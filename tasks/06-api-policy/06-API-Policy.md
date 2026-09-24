# 06 – API-Policy: beobachtete Nutzung und offene Validierung

**Status:** aktueller `closed_solution`-Pfad beobachtet; Alternativen nicht zur Laufzeit geprüft. **Schweregrad:** niedrig als Audit-Lücke, potenziell höher bei künftiger Policy-Regression.

## Evidenz

Alle vier vorhandenen `ainetlinter-rules.json` setzen `DeadCode.DefaultApiSurface` explizit auf `closed_solution`; alle vier Solution-Verify-Läufe gingen durch und meldeten `apiProtected=0`. Im AiNetLinter-Quelltext hat der Config-Default `closed_solution`, und `IsKnown` akzeptiert nur `closed_solution` und `external_library`; vorhandene Tests prüfen, dass explizites `unknown` erhalten bleibt und ungültig ist. [AiNetLinter-Protokoll](AiNetLinter.md)

## Nicht geprüft

- Laufzeitverhalten bei **fehlendem** Policy-Feld in einer der vier Ziel-Solutions (alle setzen es explizit).
- `external_library` und Projekt-Overrides im Verify-Output.
- Der konkrete MCP-Preflight-Fehler `DEAD_CODE_API_SURFACE_NOT_CONFIGURED` bei explizitem `unknown`.

Die Einschränkung folgt aus dem Read-only-Auftrag: `verify` nimmt keinen Policy-Override entgegen; eine Änderung an Zielkonfigurationen war nicht erlaubt.

## Grundlage für ein mögliches Umsetzungskonzept

Falls diese Policy separat abgesichert werden soll, eine isolierte, reproduzierbare Fixture mit drei Fällen planen: Feld fehlt; explizit `external_library`; explizit `unknown`. Für `external_library` ist zusätzlich ein öffentliches, intern unreferenziertes Symbol nötig, um API-Schutz und Zähler sichtbar zu prüfen. Keine Schlussfolgerung aus `apiProtected=0` der vier geschlossenen Solutions auf den Bibliotheksmodus ziehen.
