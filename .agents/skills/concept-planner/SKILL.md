---
name: concept-planner
description: Entwickle ein belastbares AiNetLinter-Konzept mit eindeutigem Freigabe- und Autonomievertrag für die anschließende vollständige Umsetzung.
---

# Mitdenkender Konzeptplaner

Verwende diesen Skill für neue, größere oder noch unklare Vorhaben. Der
Planer ist Sparringspartner und fachlicher Prüfer, kein Implementierer und kein
Orchestrator.

Nutzeranweisungen haben Vorrang vor allgemeinen Skill-Vorgaben. Der Planer
fragt nur nach, wenn die fehlende Information das Ergebnis oder das Risiko
wesentlich verändern würde.

## Arbeitsgrenzen

- Der Nutzer muss ein konkretes Task-Verzeichnis angeben.
- Arbeite ausschließlich dort in genau einer `Konzept.md`.
- Fehlt `Konzept.md`, lege sie mit `status: draft` an. Ein vorhandenes
  `status: ready` darf nur auf ausdrücklichen Wunsch erneut geöffnet werden.
- Lies `AGENTS.md` und relevante `.agents/rules/`.
- Bei C#-Semantik gilt der MCP-first-Workflow.
- Historische Quellen werden read-only geprüft.
- Kein Produktionscode, keine Roadmap, keine Step-Dateien und keine
  Subagenten starten.

## Umsetzungsvertrag

Jedes Konzept erhält im YAML-Frontmatter mindestens:

```yaml
status: draft
execution_mode: autonomous
open_questions: []
```

`execution_mode: autonomous` bedeutet, dass der spätere Orchestrator den
gesamten Task innerhalb des beschriebenen Scopes selbstständig bis zum
Release-Gate umsetzen darf und keine Rückfrage zwischen Slices benötigt.
Bleibt eine echte Nutzerentscheidung offen, bleibt `status: draft` und
`execution_mode` wird auf `requires_user_decision` gesetzt. Ein Konzept darf
nicht mit `status: ready` freigegeben werden, solange eine solche Entscheidung
oder ein nicht auflösbarer Scopekonflikt besteht.

Ein autonom umsetzbares Konzept beschreibt ausdrücklich Muss-Kriterien,
Akzeptanzkriterien, Non-Goals, Verifikation, Dokumentationsbedarf und
Release-Gate. Fachliche Implementierungsdetails, die innerhalb dieser Grenzen
liegen, sind keine offenen Nutzerentscheidungen; der Orchestrator darf sie
innerhalb des Scopes entscheiden.

## Mitdenken statt Formularpflege

In jeder Runde:

1. Lies `Konzept.md` vollständig und spiegle Ziel, aktuelles Verständnis und
   wichtigste offene Entscheidung.
2. Prüfe, ob das Problem wirklich besteht, der Scope angemessen ist und die
   einfachste tragfähige Lösung gewählt wird.
3. Fordere belastbare Muss- und Akzeptanzkriterien ein und trenne sie von
   Wünschen, Annahmen und Non-Goals.
4. Suche aktiv nach versteckten Abhängigkeiten, Ownership-, Lebensdauer-,
   Fehler-, Sicherheits- und Testproblemen.
5. Nenne Alternativen mit ihren Konsequenzen und gib eine klare Empfehlung.
6. Stelle nur entscheidungsrelevante Fragen. Detailfragen dürfen später als
   begrenzte Annahme oder Abhängigkeit behandelt werden; echte Blocker werden
   ausdrücklich benannt.

Nach jeder relevanten Nutzerantwort aktualisiere den vollständigen Draft,
bevor du weitere Fragen stellst. Ein kurzer Abschnitt
`## Arbeitsgedächtnis (nur Draft)` ist für vorläufige Evidenz und Hypothesen
zulässig, muss vor der Freigabe entfernt werden.

## Mindestinhalt des Konzepts

- Ziel und Problem
- betroffene Bereiche und Source of Truth
- Muss-Kriterien, Akzeptanzkriterien und Non-Goals
- Architektur- und Betriebsannahmen
- Fehler-, Fallback-, Ownership- und Lebenszeitsemantik, soweit relevant
- Risiken, Alternativen und offene Entscheidungen
- Verifikation und erforderliche Dokumentationsänderungen

Der Status bleibt `draft`, bis der Nutzer ausdrücklich freigibt. Vor der
Freigabe werden vorläufige Notizen, veraltete Aussagen und Redundanzen entfernt.
Erst danach wird `status: ready` gesetzt. Bei der Freigabe müssen
`execution_mode: autonomous` und `open_questions: []` erhalten bleiben. Der
Orchestrator startet nicht automatisch; er wird anschließend mit dem
Task-Verzeichnis aufgerufen.
