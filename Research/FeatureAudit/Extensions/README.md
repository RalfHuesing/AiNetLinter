# Extensions — Nachträgliche Forschungsfragen

Dieses Verzeichnis nimmt Zusatzfragen auf, die nach dem initialen Audit entstehen.  
Jede Frage bekommt eine eigene Datei. Die Paper-Bibliothek aus `temp\papers\` wird weitergenutzt.

---

## Wann eine Extension anlegen?

- Du hast eine neue Frage an die Datenlage, die die bestehenden Result-Dateien nicht beantwortet
- Du willst einen bestimmten Aspekt vertiefen (z.B. "Wie verhalten sich unsere Limits im Vergleich zu SonarQube?")
- Du hast eine neue Hypothese, die eigene Paper-Recherche braucht

**Was KEINE Extension braucht:** Kleine Korrekturen oder Ergänzungen an bestehenden Result-Dateien — die kannst du direkt edieren.

---

## Template: Neue Extension

Dateiname: `Extensions\[YYYYMMDD]-[kurzer-slug].md`  
Beispiel: `Extensions\20260722-severity-review.md`

```markdown
# Extension: [Titel der Frage]

**Erstellt:** [Datum]  
**Fragestellung:** [Die konkrete Frage in 1–2 Sätzen]

---

## Benötigte Paper-Cluster

| Cluster | Pfad | Vorhanden? |
|---------|------|-----------|
| [z.B. A] | `temp\papers\papers-A-komplexitaet.md` | ✅ / ❌ |
| [Neu: H] | `temp\papers\papers-H-[slug].md` | ❌ — muss erstellt werden |

Falls ein neuer Cluster nötig ist: Suchqueries und Suchstrategie hier beschreiben, dann den Cluster erstellen, dann die Frage beantworten.

---

## Checklist

- [ ] Benötigte Paper-Cluster vorhanden oder erstellt
- [ ] [Konkrete Teilaufgabe 1] → `[Output-Pfad]`
- [ ] [Konkrete Teilaufgabe 2] → `[Output-Pfad]`

---

## Output-Dateien

[Liste der Zieldateien die diese Extension produziert]

---

## Ergebnis

[Wird vom Agenten ausgefüllt nach Abschluss]
```

---

## Bestehende Extensions

| Datei | Fragestellung | Status |
|-------|--------------|--------|
| — | — | — |

*(Tabelle bei neuen Extensions ergänzen)*
