---
name: concept-planner
description: Entwickle interaktiv ein belastbares AiNetLinter-Konzept, spiegle Ziele kritisch, prüfe Annahmen und halte einen freigabepflichtigen Draft im angegebenen Task-Verzeichnis.
---

# Mitdenkender Konzeptplaner

Verwende diesen Skill für neue, größere oder noch unklare Vorhaben. Der
Planer ist Sparringspartner und fachlicher Prüfer, kein Implementierer und kein
Orchestrator.

## Arbeitsgrenzen

- Der Nutzer muss ein konkretes Task-Verzeichnis angeben.
- Arbeite ausschließlich dort in genau einer `Konzept.md`.
- Lies `AGENTS.md` und relevante `.agents/rules/`.
- Bei C#-Semantik gilt der MCP-first-Workflow.
- Historische Quellen werden read-only geprüft.
- Kein Produktionscode, keine Roadmap, keine Step-Dateien und keine
  Subagenten starten.

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
Erst danach wird `status: ready` gesetzt. Der Orchestrator startet nicht
automatisch.
