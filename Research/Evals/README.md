# Spec-Evals — Drift-Erkennung durch Prompt-Audits

## Worum geht es

Wenn Code überwiegend von LLM-Agenten geschrieben wird, entsteht mit der Zeit **semantischer Drift**: Der Code entfernt sich schrittweise vom ursprünglichen Intent — ohne dass dabei eine Regel verletzt wird. Statische Analyzer sehen das nicht, weil `IFooBarHandlerService` valides C# ist.

Diese Evals prüfen **Spec gegen Observable Behavior** — nicht Code gegen Regeln.

## Das Kernprinzip

```
Spec (was es tun soll)  +  Evidence (was es tatsächlich tut)  →  Lücken
```

Du liest keinen Code. Du vergleichst Dokumentation mit beobachtbarem Verhalten.

## Die vier Audit-Typen

| Datei | Frage | Evidence-Quelle |
|---|---|---|
| [E01-Feature-Completeness.md](E01-Feature-Completeness.md) | Tut Feature N noch was die Spec sagt? | CLI-Output, Screenshot, Testlauf |
| [E02-Naming-Drift.md](E02-Naming-Drift.md) | Haben sich Domain-Begriffe im Code von der Spec entfernt? | `rg`-Output über Klassen/Methoden |
| [E03-Architecture-Intent.md](E03-Architecture-Intent.md) | Entspricht die Struktur noch dem Design-Intent? | `Get-ChildItem`-Output, Dateiliste |
| [E04-Behavioral-Regression.md](E04-Behavioral-Regression.md) | Gibt das System bei bekanntem Input noch den richtigen Output? | Direkter Programmaufruf |

## Wann laufen lassen

- Nach einem größeren Batch von KI-Iterationen (z.B. 20+ Prompts)
- Bevor ein Feature als "fertig" gilt
- Wenn sich etwas "komisch anfühlt" aber du nicht weißt warum
- Monatliches Routine-Audit (reicht E01 + E02)

## Wie die Prompts funktionieren

Jede Datei enthält einen vollständigen Prompt mit Platzhaltern in `[GROSSBUCHSTABEN]`.

1. Platzhalter durch deinen konkreten Inhalt ersetzen
2. Gesamten Prompt an ein LLM schicken (neuer, leerer Chat — kein Kontext)
3. Output als Audit-Protokoll speichern oder direkt nacharbeiten

**Wichtig:** Frischen Chat verwenden. Das LLM soll keine Vorannahmen über dein Projekt haben.

## Evidence sammeln — Schnellreferenz (PowerShell 7)

```powershell
# Klassen-, Interface-, Record-, Enum-Namen (für E02)
rg "(class|interface|record|enum)\s+(\w+)" src\ -o --no-filename -g "*.cs" | Sort-Object -Unique

# Vollständige Typ-Deklarationen mit Modifier (für E02, detaillierter)
rg "^\s*(public|internal|private|protected|sealed|static|abstract).*?(class|interface|record|enum)\s+\w+" src\ --no-filename -g "*.cs" |
    ForEach-Object { $_.Trim() } | Sort-Object -Unique

# Verzeichnisstruktur ohne bin/obj (für E03)
Get-ChildItem -Path src\ -Recurse -Filter "*.cs" |
    Where-Object { $_.FullName -notmatch "\\(bin|obj)\\" } |
    Select-Object -ExpandProperty FullName | Sort-Object

# Dateigröße als Hinweis auf "God Classes" (für E03)
Get-ChildItem -Path src\ -Recurse -Filter "*.cs" |
    Where-Object { $_.FullName -notmatch "\\(bin|obj)\\" } |
    Select-Object Name, @{N="Lines";E={(Get-Content $_.FullName | Measure-Object -Line).Lines}} |
    Sort-Object Lines -Descending

# Programmaufruf-Output (für E01, E04)
# → Dein Programm mit einem bekannten Testfall aufrufen und Output kopieren
```

## Begriffe

- **Eval** (Evaluation): Strukturierter Prompt der prüft ob ein System einer Spec entspricht
- **LLM-as-Judge**: LLM bewertet Output eines anderen LLM oder eines Systems
- **Spec Conformance**: Übereinstimmung zwischen Spezifikation und Implementierung
- **Semantic Drift**: Langsame Entfernung vom ursprünglichen Intent ohne Regelverstoß
