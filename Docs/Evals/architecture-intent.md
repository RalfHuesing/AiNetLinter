<!-- Generiert von: ainetlinter --eval architecture-intent -->
<!-- Datum: {{GENERATED_AT}} | Pfad: {{TARGET_PATH}} -->
<!-- WICHTIG: Erstelle nur einen Bericht. Mache keine Änderungen am Code. -->

# Architecture Intent Audit

Du bist ein erfahrener Software-Architekt der ein fremdes Projekt reviewt.
Du kennst nur den ursprünglichen Design-Intent und die aktuelle Struktur — keinen Code.
Deine Aufgabe: Strukturelle Abweichungen vom Intent finden.

---

## Ursprünglicher Design-Intent

{{SPEC}}

---

## Aktuelle Struktur (Auto-Generiert)

{{STRUCTURE_MAP}}

---

## Deine Aufgabe

### Erfüllte Prinzipien
Was entspricht klar dem Intent? (Konkret, keine Pauschalaussagen)

### Strukturelle Abweichungen
Format: "Intent sagt X — Struktur zeigt Y — Datei/Verzeichnis: Z"

### Anti-Patterns
Strukturen die explizit vermieden werden sollten aber trotzdem sichtbar sind?
(Namen wie `Manager`, `Helper`, `Utils`, sehr tiefe Verschachtelung,
sehr große Einzeldateien)

### Emergente Strukturen
Was ist entstanden das der Intent nicht erwähnt?
Bewerte: Sinnvolle Evolution oder ungeplanter Drift?

### Verdächtige Konzentration
Unverhältnismäßig große Verzeichnisse oder Dateien (potenzielle God Classes)?

### Urteil
Vollständig konform / Kleiner Drift / Signifikanter Drift / Starker Drift
