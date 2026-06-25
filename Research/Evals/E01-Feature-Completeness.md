# E01 — Feature Completeness Audit

**Frage:** Tut Feature N noch das, was die Spezifikation verspricht?

## Wann nutzen

Wenn du nach mehreren KI-Iterationen prüfen willst ob ein konkretes Feature noch spec-konform ist. Ideal für Features die du "weißt dass sie existieren" aber nicht nachverfolgt hast.

## Evidence vorbereiten

**SPEC:** Relevanter Abschnitt aus README / Docs zu diesem Feature — copy-paste, kein Zusammenfassen.

**OUTPUT:** Das System tatsächlich ausführen und den Output kopieren. Kein Lesen von Code.

---

## Prompt

```
Du bist ein unabhängiger Software-Qualitäts-Auditor. Du kennst dieses Projekt nicht.
Deine Aufgabe ist ein Spec-Conformance-Check für ein einzelnes Feature.
Bewerte sachlich — keine Kommentare zur Implementierung oder Codequalität.

---

## Spezifikation des Features

[SPEC: Füge hier den relevanten Dokumentations-Abschnitt ein — z.B. README-Sektion, Docs-Datei, Anforderung]

---

## Beobachtetes Verhalten

[OUTPUT: Füge hier den tatsächlichen Output des Systems ein — CLI-Ausgabe, Screenshot-Beschreibung, Testlauf-Ergebnis, etc.]

---

## Deine Aufgabe

Vergleiche Spezifikation und beobachtetes Verhalten. Strukturiere deine Antwort exakt so:

### Vollständig implementiert
- Was tut das System exakt wie spezifiziert? (Konkrete Punkte, keine Pauschalaussagen)

### Abweichungen
- Wo weicht das Verhalten von der Spec ab? (Format: "Spec sagt X — System macht Y")

### Fehlende Features
- Was verspricht die Spec, fehlt aber im Output vollständig?

### Unerwartetes Verhalten
- Was tut das System, das die Spec nicht erwähnt? (Kann gut oder schlecht sein)

### Urteil
Ein Satz: Ist dieses Feature spec-konform, teilweise konform, oder nicht konform?
```

---

## Was mit dem Output machen

- **Vollständig konform:** Kein Handlungsbedarf. Protokoll aufbewahren als Baseline.
- **Abweichungen gefunden:** Entscheiden ob Spec oder Implementation angepasst wird.
- **Unerwartetes Verhalten:** Prüfen ob das Feature unbemerkt gewachsen ist (Scope Creep).
