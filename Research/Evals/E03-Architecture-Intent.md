# E03 — Architecture Intent Audit

**Frage:** Entspricht die aktuelle Struktur noch dem ursprünglichen Design-Intent?

## Worum geht es

Architektur-Drift ist subtiler als Naming-Drift. Ein Layer der "nur kurz" eine Abhängigkeit aufbaut die er nicht haben sollte. Eine Klasse die langsam Verantwortlichkeiten ansammelt. Ein Muster das an einer Stelle umgangen wird — und dann an zwei weiteren. Kein Regelverstoß. Trotzdem falsch.

## Evidence vorbereiten

**INTENT:** Architektur-Beschreibung aus der Doku — Prinzipien, explizite Verbote, gewollte Schichtung, Design-Entscheidungen mit Begründung.

**STRUCTURE:** Datei- und Verzeichnisstruktur (PowerShell 7):

```powershell
# Dateiliste ohne bin/obj (kompakt — für kleinere Projekte)
Get-ChildItem -Path src\ -Recurse -Filter "*.cs" |
    Where-Object { $_.FullName -notmatch "\\(bin|obj)\\" } |
    Select-Object -ExpandProperty FullName | Sort-Object

# Verzeichnisbaum (nur Verzeichnisse, ohne Dateien — für Überblick)
Get-ChildItem -Path src\ -Recurse -Directory |
    Where-Object { $_.FullName -notmatch "\\(bin|obj)\\" } |
    Select-Object -ExpandProperty FullName | Sort-Object

# Dateigröße als Hinweis auf "God Classes" (sortiert nach Zeilenzahl)
Get-ChildItem -Path src\ -Recurse -Filter "*.cs" |
    Where-Object { $_.FullName -notmatch "\\(bin|obj)\\" } |
    Select-Object Name, @{N="Lines";E={(Get-Content $_.FullName | Measure-Object -Line).Lines}} |
    Sort-Object Lines -Descending
```

---

## Prompt

```
Du bist ein erfahrener Software-Architekt der ein fremdes Projekt reviewt.
Du kennst nur den ursprünglichen Design-Intent und die aktuelle Struktur — keinen Code.
Deine Aufgabe: Finde strukturelle Abweichungen vom Intent.

---

## Ursprünglicher Design-Intent

[INTENT: Füge hier die Architektur-Beschreibung ein — Prinzipien, Verbote, gewollte Struktur, Design-Rationale]

---

## Aktuelle Struktur

[STRUCTURE: Füge hier die Verzeichnis- und Dateiliste ein]

---

## Deine Aufgabe

### Erfüllte Prinzipien
Was in der Struktur entspricht klar dem Intent? (Konkret, keine Pauschalaussagen)

### Strukturelle Abweichungen
Was in der Struktur widerspricht dem Intent oder passt nicht dazu?
Format: "Intent sagt X — Struktur zeigt Y — Datei/Verzeichnis: Z"

### Anti-Patterns
Strukturen die explizit vermieden werden sollten aber trotzdem sichtbar sind?
(Hinweis: Namen wie `Manager`, `Helper`, `Utils`, `Misc`, sehr tiefe Verschachtelung, sehr große Einzeldateien)

### Emergente Strukturen
Was ist entstanden das der Intent nicht erwähnt?
Bewerte: Sinnvolle Evolution oder ungeplanter Drift?

### Verdächtige Konzentration
Gibt es Verzeichnisse oder Dateien die unverhältnismäßig groß oder komplex wirken?
(Hinweis auf "God Classes" oder angesammelten Scope Creep)

### Urteil
Ist die Architektur noch im Einklang mit dem ursprünglichen Intent?
Skala: Vollständig konform / Kleiner Drift / Signifikanter Drift / Starker Drift
```

---

## Was mit dem Output machen

- **Kleiner Drift:** Dokumentieren, beim nächsten Refactoring-Prompt als Kontext mitgeben.
- **Signifikanter Drift:** Einen gezielten Refactoring-Prompt erstellen der die Abweichung korrigiert.
- **Starker Drift:** Zuerst den Intent in der Doku aktualisieren (hat er sich vielleicht bewusst geändert?) — dann entscheiden.
