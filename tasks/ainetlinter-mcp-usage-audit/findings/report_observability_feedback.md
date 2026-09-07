# Finding: `report_observability_feedback`

Audit-Probe (Welle 2). Kein Produkt-Feedback. Genau ein als Probe gekennzeichneter Erfolgs-Call plus ein Fehlerpfad (fehlende Pflichtfelder).

## 1 Schema-Kurzfazit

`report_observability_feedback` ist **nicht zielgebunden**. Das JSON-Schema enthält **kein** `targetType` und **kein** `targetPath`. Die Tool-Beschreibung (GetDynamicTools) sagt ausdrücklich: Feedback ins System-Log schreiben, danach mit dem besten Workaround fortfahren. Namespace-Hinweis bestätigt: nicht zielgebunden, kein Target-Block.

Pflichtfelder laut `required`: `feedbackType`, `title`, `description`.

Optionale Felder (alle `string | null`):

| Feld | Default | Rolle |
|---|---|---|
| `relatedTool` | `null` | betroffenes MCP-Tool |
| `severity` | `"medium"` | Schwere |
| `expectedBehavior` | `null` | Soll |
| `actualBehavior` | `null` | Ist |
| `additionalContext` | `null` | Extra-Kontext |
| `projectRoot` | `null` | optionaler Projektpfad, **kein** Session-Target |

`feedbackType` und `severity` sind im JSON-Schema **freie Strings**, keine `enum`. Erlaubte `feedbackType`-Werte stehen nur in der Freitext-Beschreibung: `bug`, `false_positive`, `confusing_output`, `feature_request`, `performance`. Wann nutzen / wann nicht: unerwartete Tool-Fehler, verwirrende Ausgaben, False Positives, fehlende Features; **nicht** für normale Leermengen.

`projectRoot` existiert trotz Nicht-Zielbindung. Das Schema macht die Abwesenheit von `targetType`/`targetPath` klar; `projectRoot` allein könnte Agenten trotzdem zu einem Target-Block verleiten.

## 2 Calls

Hartes Limit eingehalten: **1 Erfolgs-Call (Probe)** + **1 Fehlerpfad (fehlende Pflichtfelder)**. Keine weiteren Calls.

### 2.1 Erfolgs-Call (Probe/Audit)

- Tool: `report_observability_feedback`
- `feedbackType`: `feature_request`
- `title`: `[PROBE/AUDIT] AiNetLinter-MCP-Usage-Audit: report_observability_feedback Live-Probe`
- `description` / `additionalContext`: ausdrücklich als Probe/Audit gekennzeichnet, kein echtes Produkt-Feedback
- `severity`: `low`
- `relatedTool`: `report_observability_feedback`
- `projectRoot`: `C:\Workspace\Sample.Project`
- `expectedBehavior` / `actualBehavior`: Audit-Meta (Soll vs. Live-Antwort)

**Antwort:** Erfolg.

> `[INFO]: Feedback '[PROBE/AUDIT] AiNetLinter-MCP-Usage-Audit: report_observability_feedback Live-Probe' (feature_request) erfolgreich protokolliert. Vielen Dank! Bitte mit dem besten verfügbaren Workaround fortfahren.`

Kein Target-Parameter nötig. Titel und `feedbackType` werden in der Bestätigung wiederholt. Keine StructuredContent, keine IDs, keine Truncation sichtbar.

### 2.2 Fehlerpfad (fehlende Pflichtfelder)

- Argumente: `{}` (keine `feedbackType`/`title`/`description`)
- **Antwort:** `An error occurred invoking 'report_observability_feedback'.`
- Keine Feldliste, kein JSON-Schema-Hinweis, kein `required: […]`. Host-seitiger Generic-Error statt tool-eigener Validierungsmeldung.

## 3 Verdict

Das Tool tut, was die Beschreibung verspricht: ein als Probe gekennzeichneter Feedback-Call wird akzeptiert und ins System-Log geschrieben, ohne `targetType`/`targetPath`. Die Nicht-Zielbindung ist im Schema klar (keine Target-Felder). Der Fehlerpfad existiert, ist aber für Agenten unbrauchbar generisch. `feedbackType`/`severity` fehlen als JSON-`enum`. Für den dokumentierten Zweck (Observability-Meldung, danach Workaround) reicht der Happy Path.

## 4 Schwere

**ok**

Happy Path und Nicht-Zielbindung stimmen. Die opake Validierungsantwort und fehlende Schema-Enums sind Reibung, nicht Funktionsbruch (`friction` nur für den Fehlerpfad, nicht für das Tool insgesamt).

## 5 Nutzbarkeit

- **Wann:** MCP-Tool meldet internen Fehler, False Positive, verwirrende Ausgabe oder fehlendes Feature. Danach mit Workaround weiterarbeiten.
- **Wann nicht:** normale Leermengen (Symbol/Datei existiert nicht).
- **Agent-Tauglichkeit:** hoch für den Happy Path (drei Pflichtstrings, kompakte Bestätigung). Niedrig für Recovery nach Validierungsfehler (keine Feldnamen).
- **Zielbindung:** keinesfalls `targetType`/`targetPath` mitschicken; `projectRoot` nur als optionalen Kontextstring, nicht als Session-Key.
- **Probe-Kennzeichnung:** Titel/Beschreibung als `[PROBE/AUDIT]` markieren, damit Audit-Calls nicht als echte Bugs gezählt werden.

## 6 Bugs

1. **Validierungsfehler ohne Pflichtfeld-Liste.** Leere Args liefern nur `An error occurred invoking 'report_observability_feedback'.` Agenten können daraus nicht ableiten, welche Felder fehlen.
2. **Keine JSON-`enum` für `feedbackType`.** Werte nur in der Beschreibung; falsche Strings können stillschweigend oder erst serverseitig scheitern (in dieser Probe nicht extra getestet).
3. **Keine JSON-`enum` für `severity`.** Default `"medium"` ist dokumentiert, erlaubte Werte (`low` wurde akzeptiert) stehen nicht im Schema.
4. **Kein Bug, aber Falle:** optionales `projectRoot` kann wie ein Target gelesen werden. Schema enthält trotzdem kein `targetType`.

Keine False Positives im Sinne der Tool-Beschreibung (kein Code-Analyse-Tool).

## 7 Token/IDs

- Erfolgsantwort: eine INFO-Zeile, Titel + `feedbackType` echoed, sehr tokenarm.
- Keine Symbol-IDs, keine File-IDs, keine Truncation, kein StructuredContent sichtbar.
- Fehlerantwort: eine generische Zeile, ebenfalls tokenarm, aber ohne Diagnose-Nutzen.
- Kein Roundtrip-Ballast durch Target-Blöcke (korrekt, weil nicht zielgebunden).

## 8 Roslyn-Wünsche

Keiner. Das Tool analysiert keinen Code, löst keine Symbole auf und braucht keine Roslyn-Session. Es ist der Observability-Kanal für andere AiNetLinter-Tools. Roslyn-Pfad/Symbol gehören in die jeweiligen Analyse-Tools, nicht hierher.

## 9 Phase-3-Platzhalter

Welle 3: AiNetLinter-Pfad/Symbol + Ansatz

- **Pfad:** nicht anwendbar (kein Source-/Assembly-Target).
- **Symbol:** nicht anwendbar (kein Roslyn-Symbolgraph).
- **Ansatz:** bei echten Tool-Störungen (nicht bei dieser Probe) einmal `report_observability_feedback` mit `relatedTool` = dem gestörten Tool, `feedbackType` passend (`bug` / `confusing_output` / `false_positive` / `feature_request` / `performance`), danach den dokumentierten Workaround der jeweiligen Analyse-Antwort nutzen. Kein zweiter Erfolgs-Call desselben Feedbacks.
