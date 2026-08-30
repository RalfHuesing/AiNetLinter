---
name: concept-planner
description: Schärfe ein neues AiNetLinter-Vorhaben interaktiv zu einem umsetzbaren Konzept mit Muss-Kriterien, Non-Goals und begrenztem Betriebsmodell, ohne den Code umzusetzen.
---

# Interaktiver Konzept-Planer für AiNetLinter

## Zweck

Verwende diesen Skill vor größeren oder noch unklaren Vorhaben. Er führt ein
Sparring mit dem Nutzer, untersucht den vorhandenen Projektkontext und formt
die Idee schrittweise zu einem belastbaren `Konzept.md`.

Er ist kein Implementierungsworkflow, kein Orchestrator und kein Ersatz für
den Implementierungs- oder Review-Skill. Während der Klärungsphase werden
keine Code-, Build- oder Teständerungen vorgenommen.

## Projektkontext und Regeln

- Lies `AGENTS.md` und die für das Vorhaben relevanten Dateien unter
  `.agents/rules/`.
- Lies `.agents/rules/AiNetLinter-McpWorkflow.mdc` vor semantischen Fragen zu
  C#-Symbolen, Referenzen, Abhängigkeiten oder bestehender Architektur. Nutze
  die aktuellen MCP-Toolschemas mit `targetType` und absolutem `targetPath`;
  `rg` bleibt für Nicht-C#-Dateien und exakte Textsuche ergänzend erlaubt.
- Konsultiere bei fachlich relevanten Vorhaben die passende Dokumentation,
  bestehende Verträge und vorhandene Implementierung. Behaupte keine
  Architektur oder Fähigkeit, die nicht aus dem Projekt oder der
  Nutzerentscheidung belegt ist.

## Interaktives Vorgehen

1. Spiegle zu Beginn das verstandene Ziel in wenigen Sätzen und nenne die
   wichtigste noch offene Entscheidung.
2. Untersuche den relevanten Projektkontext, bevor du Detailfragen stellst.
3. Stelle pro Runde nur ein bis vier entscheidungsrelevante Fragen. Gib zu
   jeder Frage, soweit möglich, eine konkrete Empfehlung und die Konsequenz
   der Alternative.
4. Arbeite nacheinander Ziel, Motivation, Scope, betroffene Bereiche,
   Betriebs-/Bedrohungsmodell, Muss-Kriterien, Non-Goals, Risiken und
   Verifikation heraus. Trenne harte Anforderungen von Ideen und späteren
   Erweiterungen.
5. Fasse nach jeder Nutzerantwort die getroffenen Entscheidungen und die
   verbleibenden offenen Punkte knapp zusammen. Wiederhole eine bereits
   bewusst verschobene Frage nicht.

## Konzeptqualität

Ein umsetzbares Konzept enthält mindestens:

- Ziel und Nutzen in wenigen Sätzen;
- betroffene Projektbereiche und relevante bestehende Strukturen;
- Muss-Kriterien und überprüfbare Akzeptanzkriterien;
- explizite Non-Goals und bewusste Scope-Grenzen;
- Betriebs- und Bedrohungsmodell, einschließlich akzeptierter Annahmen;
- Fehler-, Fallback-, Ownership- und Lebenszeitsemantik, soweit relevant;
- geplante Verifikation und notwendige Dokumentationsänderungen;
- offene Punkte, die für den aktuellen Scope tatsächlich noch blockieren.

Für AiNetLinter gilt als Standardannahme ein lokales Entwickler-Hilfstool mit
MCP-Stdio-Server. Mögliche Parallelität innerhalb eines Daemons,
Credential-Schutz, fehlerhafte Eingaben, Cancellation und korrekte
Source-Herkunft bleiben relevante Normalbetriebsanforderungen. Ein
bösartiger lokaler Administrator oder mehrere konkurrierende Daemons mit
gemeinsamem Cache sind nicht automatisch Teil des Scopes.

Fordere keine stärkeren Garantien als das vereinbarte Betriebsmodell. Wenn das
Vorhaben andere Annahmen braucht, mache den Unterschied explizit und lasse ihn
vom Nutzer entscheiden.

## Datei und Abschluss

Wenn kein Taskpfad eindeutig erkennbar ist, frage nach dem gewünschten
Tasknamen bzw. Zielpfad. Bei einem vorhandenen Konzept lese und verbessere die
bestehende Datei, statt eine zweite konkurrierende Datei zu erzeugen. Bei einem
neuen Task verwende die im Repository übliche Konzeptdatei, bevorzugt
`Konzept.md`.

Während des Sparrings schreibst du die Konzeptdatei erst nach ausdrücklicher
Bestätigung eines abgestimmten Zwischenstands. Danach darfst du sie iterativ
aktualisieren, wenn der Nutzer die jeweilige Entscheidung bestätigt. Setze
den Status erst auf `ready`, wenn alle für den aktuellen Scope notwendigen
Entscheidungen getroffen sind; bewusst zurückgestellte Erweiterungen bleiben
als solche gekennzeichnet.

Erzeuge keine Roadmap, Step-Dateien, Task-State-Dateien oder Codeänderungen.
Beende mit einer kompakten Zusammenfassung der Entscheidung und einer klaren
Empfehlung, ob das Vorhaben jetzt an den Implementierungs-Skill übergeben
werden kann.
