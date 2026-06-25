# E02 — Naming & Vocabulary Drift Audit

**Frage:** Haben sich die Domain-Begriffe im Code von der Spezifikation entfernt?

## Worum geht es

LLMs erfinden bei jeder Session neue Namen wenn sie kein klares Vokabular-Vorbild haben. Das Ergebnis: `Process`, `Handle`, `Apply`, `Execute` und `Evaluate` existieren alle für dieselbe Operation. Oder aus `Analyzer` wird `AnalysisProvider` wird `AnalysisProviderService`. Kein Regelverstoß — trotzdem Drift.

## Evidence vorbereiten

**SPEC:** README oder Doku-Abschnitt der die Kernkonzepte beschreibt — wo Domain-Begriffe das erste Mal eingeführt werden.

**IDENTIFIERS:** Klassen- und Methoden-Namen aus dem Code extrahieren (PowerShell 7):

```powershell
# Klassen, Interfaces, Records, Enums — nur die Namen
rg "(class|interface|record|enum)\s+(\w+)" src\ -o --no-filename -g "*.cs" | Sort-Object -Unique

# Vollständige Typ-Deklarationen mit Modifier (empfohlen — zeigt mehr Kontext)
rg "^\s*(public|internal|private|protected|sealed|static|abstract).*?(class|interface|record|enum)\s+\w+" src\ --no-filename -g "*.cs" |
    ForEach-Object { $_.Trim() } | Sort-Object -Unique

# Public-Methoden (optional, für tieferen Audit)
rg "^\s*public\s+\w.*\(.*\)" src\ --no-filename -g "*.cs" |
    ForEach-Object { $_.Trim() } | Sort-Object -Unique
```

---

## Prompt

```
Du bist ein Vokabular-Auditor für Software-Projekte. Du kennst dieses Projekt nicht.
Deine Aufgabe: Semantischen Naming-Drift zwischen Spezifikation und Code-Identifiers erkennen.

---

## Spezifikation (Domain-Vokabular)

[SPEC: Füge hier den Dokumentations-Abschnitt ein der die Kernkonzepte des Projekts beschreibt]

---

## Code-Identifiers

[IDENTIFIERS: Füge hier die extrahierten Klassen-/Interface-/Record-Namen ein]

---

## Deine Aufgabe

**Schritt 1 — Kanonisches Vokabular extrahieren**
Lies die Spezifikation und liste die 10–20 zentralen Domain-Begriffe auf (Substantive für Kernkonzepte, Verben für Kernoperationen).

**Schritt 2 — Vergleich**
Analysiere die Code-Identifiers gegen das kanonische Vokabular:

### Synonyme (höchste Priorität)
Verschiedene Namen für dasselbe Konzept?
Format: "Konzept X" → gefundene Varianten: `Name1`, `Name2`, `Name3`

### Aufgeblähte Namen
Namen die offensichtlich akkumuliert sind (>3 Segmente in PascalCase, wiederholte Wörter, `...Provider`, `...Service`, `...Manager` Suffixe ohne Notwendigkeit)?
Liste: Name → Warum verdächtig

### Verwaiste Spec-Begriffe
Welche kanonischen Begriffe tauchen in den Code-Identifiers gar nicht auf?
Diese könnten fehlen — oder unter einem anderen Namen versteckt sein.

### Fremde Begriffe
Code-Namen die in der Spec nirgendwo auftauchen und kein offensichtliches technisches Hilfskonstrukt sind?

### Urteil
Skala 1–5: Wie stark ist der Naming-Drift? (1 = kein Drift, 5 = starker Drift)
Ein Satz Begründung.
```

---

## Was mit dem Output machen

- **Synonyme gefunden:** Glossar anlegen (15 Minuten) und in CLAUDE.md / AGENTS.md eintragen — verhindert Drift in zukünftigen Sessions wirksam.
- **Aufgeblähte Namen:** Refactoring-Kandidaten. Niedriger Aufwand, großer Effekt auf LLM-Lesbarkeit.
- **Urteil 4–5:** Glossar ist dringend. Ohne Vokabular-Anker verstärkt sich Drift bei jeder weiteren KI-Iteration.
