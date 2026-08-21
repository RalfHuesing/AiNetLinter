---
status: ideen-papier (bewertet am eigenen Akzeptanzkriterium)
type: konzept
project_kind: brownfield
priority: P3
agent_role: .agents/Agent-Scaffolding/dev-loop/planning/orchestrator.md
rules_dir: .agents/rules
last_updated: 2026-08-21
open_questions: []
herkunft: Review-Finding 2026-08-21 (ox-alpha)
---

# Regel-Design: Audit-Kandidaten bewertet am LLM-Failure-Pattern-Kriterium

## Maßstab

Das Projekt hat selbst das richtige Akzeptanzkriterium etabliert (Entfernung der
Magic-Values-Build-Regel am 2026-06-19: *"Regel greift kein konkretes LLM-Failure-Pattern"*):
Ein Kandidat muss ein **konkretes Defekt-Muster von LLM-generiertem Code** treffen, sonst
erzeugt er Noise. Daraus folgt eine Stufenleiter:

1. **On-Demand-Audit** (`find_magic_values`-Stil): billig, kein CI-Friktion — erste Stufe.
2. **`pattern_detect`-Katalog**: wenn das Muster gruppierbar ist und Audit-Nutzung zeigt,
   dass es relevant ist.
3. **Build-Regel in `rules.json`**: nur nach Evidenz (Nutzungsdaten aus Aufgabe 01) und
   niedriger False-Positive-Rate.

Alle Kandidaten unten sind auf Stufe 1–2 gepinnt; keine davon sollte ohne Evidenz eine
Build-Regel werden.

## Kandidaten

### 1. Sync-over-Async (`.Result`, `.Wait()`, `.GetAwaiter().GetResult()`)

- **LLM-Failure-Pattern:** Ja — klassisch bei Migrationen/Integration von generiertem
  async-Code in synchrone Kontexte; Deadlock-/Threadpool-Starvation-Risiko.
- **Form:** On-Demand-Audit oder direkt `pattern_detect`; Erkennung trivial (Invocation auf
  Task mit bekannten Membernamen).
- **False Positive:** sehr niedrig. Höchste Umsetzungsreife aller Kandidaten.
- **Dogfooding-Beleg:** Die eigene Codebase umgeht die Fallen bewusst mit kommentierten
  Suppressions (`McpCodeGraphServer.cs`, `BanBlockingTaskAccess`) — zeigt, wie ein
  Suppressions-Wegwert aussähe.

### 2. Direkte Zeitquelle statt Abstraktion (`DateTime.Now` / `DateTime.UtcNow`)

- **LLM-Failure-Pattern:** Ja, stark. Generierter Code nutzt fast immer `DateTime.Now`
  direkt — Ergebnis: nicht testbare Logik, Zeitzonen-Bugs, nicht deterministische Tests.
- **Form:** On-Demand-Audit mit Ziel-Empfehlung (`TimeProvider` injizieren,
  `FakeTimeProvider` im Test). Klassifizierung: `testability_candidates`.
- **False Positive:** niedrig-mittel (Logging-Timestamps sind legitim → Category-Filter nötig).
- **Bonus:** passt exakt zur vorhandenen `find_magic_values`-Architektur (Classifier +
  Categories + Ziel-Empfehlung), Wiederverwendung wahrscheinlich.

### 3. Fehlende CancellationToken-Propagierung in async-Ketten

- **LLM-Failure-Pattern:** Ja. LLMs deklarieren `async`-Methoden konsequent ohne
  `CancellationToken`-Parameter oder reichen ihn nicht durch. `async-void` (bereits im
  Katalog) ist die laute Variante; fehlende Propagierung ist die leise, häufigere.
- **Form:** `pattern_detect`-Pattern `missing-cancellation` (aufrufende async-Methode mit
  ct ruft async-Methode ohne ct). Hard cap: nur innerhalb einer Assembly, depth-begrenzt.
- **False Positive:** mittel (Fire-and-forget kann intendiert sein) → Confidence-Stufen
  wie bei `find_dead_code`.

### 4. Bool-Flag-Parameter, die Verzweigungsverhalten steuern

- **LLM-Failure-Pattern:** Mittel. Generierte APIs haben häufig `bool xyzMode`-Parameter;
  Folge: unlesbare Call-Sites, Testexplosion.
- **Form:** On-Demand-Audit (`design_smell_candidates`), Schwellwert: >= 2 bool-Parameter
  oder bool + weiterer Enum-Flag an öffentlicher Methode.
- **False Positive:** mittel (legitime Fälle existieren); bewusst kein Build-Regel-Kandidat.

### 5. Stringly-typed Keys (Magic Strings als Cache-/Dictionary-Keys über Dateien hinweg)

- **Abgrenzung:** Nah an Magic Values — aber die Build-Regel dafür wurde bewusst entfernt.
  Nicht als neue Regel reopenen, sondern als **categoryFilter-Erweiterung**
  (`key_candidates`) im bestehenden `find_magic_values` prüfen. Damit bleibt die alte
  Entscheidung unangetastet.

## Priorisierung

| # | Kandidat | Stufe | Reife | Empfehlung |
|---|---|---|---|---|
| 1 | Sync-over-Async | 2 (pattern_detect/Audit) | hoch | zuerst |
| 2 | DateTime-Direktnutzung | 1 (Audit) | hoch | zweiter |
| 3 | Cancellation-Propagierung | 2 (pattern_detect) | mittel | dritter |
| 4 | Bool-Flags | 1 (Audit) | mittel | backlog |
| 5 | Stringly-typed Keys | 1 (Audit-Category) | mittel | backlog |

## Querverweis

- Nutzungsdaten-Loop: Erst wenn Aufgabe 01 umgesetzt ist, lässt sich messen, welche dieser
  Audits von Agenten wirklich genutzt werden — die Basis für jede spätere Stufe-3-Entscheidung.
- Keine Build-Regel ohne Evidenz (Lehre vom 2026-06-19).
