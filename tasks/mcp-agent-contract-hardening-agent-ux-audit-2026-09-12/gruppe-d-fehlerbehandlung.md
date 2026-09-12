# Gruppe D – Fehlerbehandlung und Recovery

## Ziel und Scope

Prüfung des laufenden MCP-Servers aus Sicht eines aufrufenden Agenten. Geprüft wurden
Validierungsfehler, strukturierte Recovery und Zustandsisolation mit dem Source-Target.
Es wurden weder Code noch Tests oder Builds verändert bzw. ausgeführt.

## Szenarien (sanitisiert)

- Fehlendes, nicht vorhandenes und auf ein Verzeichnis zeigendes `targetPath`.
- Fehlendes oder leeres `symbolIdentifier`, falscher JSON-Typ für `namePatterns`,
  ungültiger `kind` und ein leeres Element in einem Symbol-Suchbatch.
- `maxResponseBytes` unterhalb der Mindestprojektion sowie ein langer Identifikator.
- Nicht verwaltetes Assembly-Ziel `FALSE-01`.
- Nach **jedem** Fehler ein korrekter Folgeaufruf auf das Source-Target.

## Positive Nachweise

- Feldvalidierungen enthalten durchgängig `code=INVALID_ARGUMENT`, einen konkreten
  `fieldPath` (einschließlich `$.namePatterns[1]`) und einen umsetzbaren `hint`.
- Der ungültige `kind` nennt die zulässigen Werte; falscher Array-Typ nennt erwarteten
  und erhaltenen JSON-Typ. Es erschienen keine Stacktraces.
- Leeres `symbolIdentifier` und der nicht verwaltete Fall `FALSE-01` lieferten
  `isError=true`, `operation=error`, `completeness=not_applicable` und einen Code.
- Ein 12.000 Zeichen langer Identifikator wurde nicht vollständig zurückgespiegelt
  (Fehlertext 774 Zeichen). Alle korrekten Folgeaufrufe lieferten wieder `operation=ok`;
  ein Fehler kontaminierte somit weder Snapshot noch Analysezustand.

## Findings

### [Critical] D-01: Frühe Validierungsfehler sind im MCP-Envelope als Erfolg markiert

- **Tools:** `find_symbol`, `get_feature_context`
- **Evidenz:**
  1. `find_symbol` ohne `targetPath` -> `code=INVALID_ARGUMENT`, `fieldPath=$.targetPath`, aber `isError=false`.
  2. `get_feature_context` ohne `symbolIdentifier` -> `code=INVALID_ARGUMENT`, `fieldPath=$.symbolIdentifier`, aber `isError=false`.
  3. Beide Antworten haben keinen Contract-v2-`navigation.status` mit `operation=error` und `completeness=not_applicable`.
  4. Der unmittelbar folgende korrekte Aufruf ist erfolgreich; dies ist kein Session- oder Targetproblem.
- **Problem:** Ein programmatisch arbeitender Agent kann die Antwort auf MCP-Ebene als
  erfolgreichen Call werten und die enthaltene Fehlermeldung übergehen. Das verletzt den
  Contract-v2-Erhaltungsvertrag (`isError=true`, `operation=error`, Code,
  `completeness=not_applicable`) und verhindert zuverlässige automatische Recovery.
- **Reproduktion:** Einen der oben genannten Pflichtparameter auslassen.
- **Empfehlung:** Den Frühvalidierungs-Response-Adapter mit dem regulären Contract-v2-
  Fehlerformatter vereinheitlichen und `isError=true` setzen; bei fehlendem Target darf
  die Navigation targetlos sein, der Statusowner darf aber nicht fehlen.

### [Major] D-02: Budgetfehler liefert keinen ausführbaren exakten Retry

- **Tools:** `get_class_structure`
- **Evidenz:**
  1. Gültiger Request mit `maxResponseBytes=512` -> `isError=true`, `operation=error`.
  2. Response: `code=INVALID_ARGUMENT`, `fieldPath=$.maxResponseBytes`.
  3. Es fehlen `requestedBytes` und `minimumResponseBytes`; der Hint fordert nur ein Erhöhen.
  4. Ein späterer Aufruf mit großzügigem Budget ist erfolgreich.
- **Problem:** Der Konzeptvertrag fordert `RESPONSE_BUDGET_TOO_SMALL` sowie einen beim
  identischen Snapshot direkt ausführbaren `minimumResponseBytes`. Ein Agent muss hier
  raten oder das Budget entfernen und verliert den deterministischen Budget-Recoverypfad.
- **Reproduktion:** Einen gültigen Klassenstruktur-Request mit Budget 512 Bytes ausführen.
- **Empfehlung:** Den Fehler als `RESPONSE_BUDGET_TOO_SMALL` mit `requestedBytes`,
  `minimumResponseBytes` und genau diesem Retrywert ausgeben.

### [Major] D-03: Assembly-Fehler spiegelt den vollständigen vertraulichen Zielpfad zurück

- **Tools:** `inspect_assembly`
- **Evidenz:**
  1. `inspect_assembly(targetPath=FALSE-01)` -> `isError=true`, `code=INVALID_ASSEMBLY`.
  2. Der Textkanal enthält zusätzlich unter `context` den vollständig übergebenen Zielpfad.
  3. Die Diagnose ist ansonsten recoverable und enthält keinen Stacktrace.
- **Problem:** Der Vertrag verlangt begrenzte lange bzw. vertrauliche Eingaben in
  Fehlermeldungen. Die vollständige Pfadspiegelung kann externe Installations- und
  Kundeninformationen in Agentenkontexte oder Telemetrie tragen.
- **Reproduktion:** Den Negativfall `FALSE-01` an `inspect_assembly` übergeben.
- **Empfehlung:** Zielpfade in Fehlern auf einen datensparsamen Anzeigenamen bzw. eine
  begrenzte, maskierte Form reduzieren; den strukturierten Code und Hint beibehalten.

## Freie Beobachtungen

- Der leere String im Batch wird präzise dem Index `$.namePatterns[1]` zugeordnet; das
  ist für selektive Batch-Recovery deutlich besser als ein pauschaler Arrayfehler.
- Für `maxResponseBytes=511` wird die formale Untergrenze verständlich validiert. Der
  darüber liegende, aber zu kleine Wert 512 zeigt jedoch den separaten Budgetvertrag
  aus D-02 nicht.

## Grenzen

Dies ist ein Laufzeit-Audit gegen den verbundenen Server, kein Nachweis gegen einen
frisch aus dem Taskstand gebauten Host. Die Prüfung umfasst keine Codeinspektion,
keine Builds und keine Testausführung. `FALSE-01` wurde nur als lokaler,
nicht-ausführender Negativfall verwendet; externe Namen, Pfade und Rohantworten sind
absichtlich nicht dokumentiert.
