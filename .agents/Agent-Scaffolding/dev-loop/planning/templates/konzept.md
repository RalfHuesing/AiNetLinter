---
status: draft  # draft | ready
type: konzept
project_kind: unknown  # unknown | greenfield | brownfield — vom Workflow erkannt (Schritt 2)
estimated_scope: unknown  # unknown | small | medium | large — Selbsteinschätzung des Workflows, steuert Frage-Tiefe
rules_dir: unknown  # unknown | .agents/rules | .cursor/rules | <custom-pfad> | keins — vom Workflow erkannt (Schritt 2), von drift-loop übernommen statt neu erkannt
last_updated: <ISO-8601>
open_questions:
  - <kurze offene Frage/Unschärfe — leer, wenn status: ready>
---

# Konzept: <Titel>

## Ziel (Was)

<2-5 Sätze: Was soll entstehen? Auf den Punkt, nicht die ganze Diskussion.>

## Warum / Kontext

<Hintergrund, Motivation, Constraints, für wen ist das.>

## Scope

### Muss-Haben

- <Punkt 1>
- <Punkt 2>

### Nice-to-Have (optional, spätere Iteration)

- <Punkt 1>

### Non-Goals (bewusst NICHT Teil davon)

- <Punkt 1 — mit kurzer Begründung, warum bewusst draußen>

## Zielplattformen / Technischer Rahmen

<Stack-/Plattform-Entscheidungen, jeweils mit Begründung — nicht nur
"was", sondern "warum genau das".>

## Verworfene Alternativen

<Was wurde erwogen und warum verworfen? Verhindert, dass dieselbe Frage
später (z. B. beim Planer im drift-loop) nochmal aufkommt.>

- **<Alternative 1>:** verworfen, weil <Grund>

## Wo im Projekt

<Bei `project_kind: brownfield`: konkret betroffene Module/Dateien/
Bereiche des Bestandscodes. Bei `project_kind: greenfield`: geplante
Grobstruktur (Verzeichnisse, Hauptkomponenten) — kein Detailplan, das
macht später der Planer.

**Pointer-Prinzip:** Dies ist eine Liste von Fundstellen (Datei/Modul +
ein Satz, warum relevant), keine Beschreibung, wie der Code dort
funktioniert — solche Behauptungen veralten. Der Planer im `drift-loop`
prüft an diesen Fundstellen den dann aktuellen Stand selbst nach, statt
sich auf den hier festgehaltenen Inhalt zu verlassen.>

## Entdeckte Mängel/Redundanzen

<Während der Konzeption aktiv gefundene Redundanzen (z. B. eine bereits
bestehende, ähnliche Struktur, die statt eines Neubaus wiederverwendet/
generalisiert werden könnte) oder tatsächliche Mängel im betroffenen
Bestandscode (z. B. Verstoß gegen `<rules_dir>/**`) — siehe
`../orchestrator.md` Schritt 3a. Nur relevant bei `project_kind:
brownfield`, im Normalfall leer bei greenfield.

**Pointer-Prinzip wie bei „Wo im Projekt":** Fundstelle (Datei/Modul +
Zeile falls sinnvoll) + ein Satz, kein Verhaltens-Anspruch, der später
veralten kann.

Jeder Fund unabhängig von der Nutzer-Entscheidung dokumentieren — auch
abgelehnte, damit dieselbe Frage nicht in einer späteren Runde/Session
erneut aufkommt:

- **<Kurztitel>**
  - **Gefunden:** <was, wo>
  - **Bezug:** <verletzte Regel aus `<rules_dir>` mit Fundstelle, oder
    „kein `rules_dir` — offensichtliches Duplikat" bei unstrittigen
    Fällen ohne kodifizierte Regeln>
  - **Vorschlag:** <bestehende Struktur X wiederverwenden/generalisieren
    statt Neubau, oder konkreter Fix-Vorschlag>
  - **Entscheidung:** übernommen ins Scope (→ siehe Muss-Haben „<Punkt>")
    | bewusst verschoben (später) | abgelehnt (<kurze Begründung>)

## Wie (grober Ansatz)

<Grobe Skizze des Lösungswegs — Detailplanung mit Datei+Zeile-Genauigkeit
ist NICHT hier, das übernimmt der Planer im drift-loop.>

## Definition of Done / Erfolgskriterien

<Woran erkennt man, dass das fertig ist? Konkret genug, dass der Planer
daraus direkt Steps mit Definition of Done ableiten kann — siehe
`../../drift-loop/spec.md` §3.2.>

## Offene Punkte

<Nur falls trotz `status: ready` noch etwas bewusst offen bleibt, z. B.
"später klären" markierte Punkte. Im Normalfall leer.>
