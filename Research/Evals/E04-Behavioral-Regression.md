# E04 — Behavioral Regression Audit

**Frage:** Gibt das System bei bekanntem Input noch den richtigen Output?

## Worum geht es

Unit-Tests können mitdriften — wenn der Test von demselben LLM geschrieben wurde das die Implementierung schrieb, testen sie möglicherweise das falsche Verhalten korrekt. Behavioral Regression prüft gegen die Spec, nicht gegen den Test.

Das ist das deterministischste Eval: Du rufst das Programm auf. Du vergleichst mit dem was die Doku verspricht.

## Evidence vorbereiten

**SPEC:** Beschreibung des Features aus der Doku — was soll bei welchem Input passieren.

**INPUT:** Den konkreten Testfall den du verwendest (Datei, Parameter, Kommando).

**ACTUAL OUTPUT:** Das Programm mit dem Testfall aufrufen und Output kopieren — unverändert, auch Fehler und Warnings.

**EXPECTED OUTPUT (optional):** Falls du ein "Golden"-Referenz-Output hast. Sonst leer lassen — das LLM leitet es aus der Spec ab.

---

## Prompt

```
Du bist ein QA-Ingenieur der einen Behavioral Regression Check durchführt.
Du kennst dieses Projekt nicht. Du hast keinen Zugriff auf den Code.
Deine einzige Grundlage sind Spec, Input und tatsächlicher Output.

---

## Feature-Spezifikation

[SPEC: Was soll dieses Feature bei welchem Input tun? Aus der Dokumentation, nicht paraphrasiert.]

---

## Test-Input

[INPUT: Welcher Testfall wurde verwendet? Dateiinhalt, CLI-Parameter, Eingabedaten — konkret]

---

## Tatsächlicher Output

[ACTUAL OUTPUT: Exakter Programmaufruf-Output — Copy-paste, nichts weglassen]

---

## Erwarteter Output (falls bekannt)

[EXPECTED OUTPUT: Optionaler Referenz-Output — leer lassen wenn unbekannt]

---

## Deine Aufgabe

Antworte kurz und konkret:

### Konformität
Entspricht der tatsächliche Output der Spezifikation?
Wähle: Vollständig konform / Teilweise konform / Nicht konform

### Fehlende Outputs
Was hätte gemäß Spec im Output erscheinen sollen, fehlt aber?
Liste: "Spec erwartet X — nicht vorhanden"

### Falsche Outputs
Was ist im Output das nicht zur Spec passt?
Liste: "Output enthält X — Spec sagt das nicht"

### Unerwartete Outputs
Was ist im Output das die Spec weder fordert noch verbietet?
(Zur Info — kein Fehler, aber zu dokumentieren)

### Dringlichkeit
Ist das ein kritischer Fehler (Feature broken), Minor Issue (kosmetisch/unvollständig), oder kein Problem?

Maximal 250 Wörter. Keine Vermutungen über den Code.
```

---

## Golden Scenarios anlegen (empfohlen)

Nach dem ersten erfolgreichen Audit: Input + Expected Output als "Golden Scenario" speichern:

```
Research/Evals/Scenarios/
  scenario-01-[feature-name]-input.txt
  scenario-01-[feature-name]-expected-output.txt
```

Bei zukünftigen Audits: `EXPECTED OUTPUT` mit dem Golden Scenario befüllen. Damit wird E04 fast vollständig automatisierbar.

## Was mit dem Output machen

- **Nicht konform:** Gezielter Fix-Prompt mit Spec + Input + Actual + Expected.
- **Teilweise konform:** Entscheiden ob das unvollständige Verhalten dokumentiert oder behoben wird.
- **Unerwartete Outputs häufen sich:** Zeichen für Feature Creep — Spec aktualisieren oder Verhalten entfernen.
