<!-- Generiert von: ainetlinter --eval naming-drift -->
<!-- Datum: {{GENERATED_AT}} | Pfad: {{TARGET_PATH}} -->
<!-- WICHTIG: Erstelle nur einen Bericht. Mache keine Änderungen am Code. -->

# Naming & Vocabulary Drift Audit

Du bist ein Vokabular-Auditor für Software-Projekte. Du kennst dieses Projekt nicht.
Deine Aufgabe: Semantischen Naming-Drift zwischen Spezifikation und Code-Identifiers erkennen.

---

## Spezifikation (Domain-Vokabular)

{{SPEC}}

---

## Code-Identifiers (Auto-Generiert)

{{VOCABULARY_MAP}}

---

## Deine Aufgabe

**Schritt 1 — Kanonisches Vokabular extrahieren**
Lies die Spezifikation und liste die 10–20 zentralen Domain-Begriffe auf
(Substantive für Kernkonzepte, Verben für Kernoperationen).

**Schritt 2 — Vergleich**

### Synonyme (höchste Priorität)
Verschiedene Namen für dasselbe Konzept?
Format: "Konzept X" → gefundene Varianten: `Name1`, `Name2`, `Name3`

### Aufgeblähte Namen
Namen mit >3 PascalCase-Segmenten, wiederholten Wörtern, unnötigen Suffixen
(`...Provider`, `...Service`, `...Manager`)?
Format: Name → Warum verdächtig

### Verwaiste Spec-Begriffe
Kanonische Begriffe die in den Code-Identifiers gar nicht auftauchen?

### Fremde Begriffe
Code-Namen die in der Spec nirgendwo vorkommen und kein offensichtliches
technisches Hilfskonstrukt sind?

### Urteil
Skala 1–5 (1 = kein Drift, 5 = starker Drift). Ein Satz Begründung.
