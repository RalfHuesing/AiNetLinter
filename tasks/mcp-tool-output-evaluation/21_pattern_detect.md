# 21 – pattern_detect

## 1. Tool-Steckbrief & Agenten-Rolle
- **Name:** `pattern_detect`
- **Agenten-Priorität:** Prio 21 (Mittel – Erkennung von Code Smells und Anti-Patterns)
- **Hauptzweck:** Sucht nach architekturellen und methodischen Anti-Patterns wie `god-class`, `async-void`, `long-method`, `empty-catch`, `feature-envy` und `public-without-doc`.
- **Wichtigste Parameter:**
  - `targetPath` (Pflicht, String): Absoluter Pfad zur `.sln`/`.slnx`.
  - `scopeFilter` (Optional, String): Pfadfilter.
  - `patterns` (Optional, Array von Strings): Gezielte Auswahl bestimmter Patterns.
  - `maxResultsPerPattern` (Optional, Int, Default 20).

---

## 2. Test-Samples & Rohe Tool-Outputs

### Sample A: Ungefilterter Scan über Scope (`src/AiNetLinter/Core`)
- **Aufruf-Parameter:**
```json
{
  "targetPath": "c:\\Daten\\Entwicklung\\Ralf\\AiNetLinter\\AiNetLinter.slnx",
  "scopeFilter": "src/AiNetLinter/Core"
}
```
- **Tool-Output (Rohdaten):**
```text
Pattern-Detect: 0 von 6 Patterns mit Treffern, 0 Treffer gesamt in 75 Dateien im Scope | Scope-Filter: 'src/AiNetLinter/Core'
Vollstaendigkeitsstatus: partiell

## god-class — Klassen mit zu grossem AI-Context-Footprint, zu vielen Public-Members oder zu vielen Zeilen. [empty, confidence=high]
Ursache: 

Keine Treffer in diesem Scope; daraus folgt kein globaler Clean-Claim.

## async-void — async void Methoden oder Local Functions statt async Task — Exceptions koennen nicht awaited/gefangen werden. [empty, confidence=high]
Ursache: 

Keine Treffer in diesem Scope; daraus folgt kein globaler Clean-Claim.

## long-method — Methoden mit zu vielen Zeilen oder zu hoher zyklomatischer/kognitiver Komplexitaet. [empty, confidence=high]
Ursache: 

Keine Treffer in diesem Scope; daraus folgt kein globaler Clean-Claim.

## public-without-doc — Oeffentliche Member ohne XML-Dokumentationskommentar. [not_configured, confidence=low]
Ursache: Keine zugeordnete Regel ist in der effektiven Projekt-/Pfad-Konfiguration aktiviert (EnforceXmlDocumentation).
Naechster Schritt: configure — Zugeordnete Regel aktivieren oder Regelkonfiguration pruefen.

Keine Treffer in diesem Scope; daraus folgt kein globaler Clean-Claim.

## empty-catch — Catch-Bloecke, die eine Exception stillschweigend verschlucken. [empty, confidence=high]
Ursache: 

Keine Treffer in diesem Scope; daraus folgt kein globaler Clean-Claim.

## feature-envy — Klassen, die ueberwiegend Aufrufe an ein anderes Objekt weiterleiten (Middle-Man — die naechste existierende Naeherung, kein 1:1-Match zum klassischen Feature-Envy-Begriff). [empty, confidence=high]
Ursache: 

Keine Treffer in diesem Scope; daraus folgt kein globaler Clean-Claim.
```

### Sample B: Gezielter Pattern-Filter (`patterns: ["long-method"]`)
- **Aufruf-Parameter:**
```json
{
  "targetPath": "c:\\Daten\\Entwicklung\\Ralf\\AiNetLinter\\AiNetLinter.slnx",
  "scopeFilter": "src/AiNetLinter/Core",
  "patterns": ["long-method"]
}
```
- **Tool-Output (Rohdaten):**
```text
Pattern-Detect: 0 Treffer in 75 Dateien im angeforderten Scope. Gepruefte Patterns: long-method.
```

---

## 3. Effizienz- & SNR-Bewertung (gemäß AiNetLinter-Richtlinien)
- **Token-Footprint:**
  - Sample A (0 Treffer, alle Patterns): ~1.8 KB (~430 Token).
  - Sample B (0 Treffer, 1 Pattern gefiltert): 88 Bytes (~20 Token).
- **SNR-Befund:**
  - Ähnlich wie bei `find_magic_values`: Wenn kein Pattern-Filter gesetzt ist und 0 Treffer vorliegen, werden 6 große Abschnitte für leere Patterns mit Textbausteinen wie `Keine Treffer in diesem Scope; daraus folgt kein globaler Clean-Claim.` ausgegeben.
  - Im gefilterten Modus (Sample B) verhält sich das Tool hingegen vorbildlich und gibt nur einen präzisen Einzeiler zurück.
- **Content-Only-Hard-Cut:** Konform.

---

## 4. Composability & Chaining-Validierung (Input/Output-Passgenauigkeit)
- **Erzeugte Ausgabefelder:**
  - Bei Treffern: Typ-/Methodenname mit `Pfad:Zeile`.
  - Bei nicht konfigurierten Regeln: `Naechster Schritt: configure — Zugeordnete Regel aktivieren oder Regelkonfiguration pruefen.` (guter Hinweis auf Konfigurationsdateien).
- **Chaining-Passgenauigkeit:**
  | Ausgabefeld | Ziel-Tool | Status |
  |---|---|---|
  | `Pfad:Zeile` | `get_symbol_body` | ✅ Direkt übergebbar |
  | `Regelname` | `ainetlinter-rules.json` | ✅ Zur Aktivierung |

---

## 5. Fazit & Design-Überlegung (Offener Punkt)
- **Prädikat:** **Gut (im gefilterten Modus exzellent)**
- **Stärken:**
  1. Wertvoller Hinweis `[not_configured]` bei deaktivierten Regeln (`EnforceXmlDocumentation`).
  2. Sehr effizient bei expliziter `patterns`-Filterung (Sample B liefert präzisen 88-Byte-Einzeiler).
- **Design-Weichenstellung (Zur Abstimmung):**
  - **Befund:** Bei ungescopten 0-Treffer-Läufen über alle Patterns werden 6 große Abschnitte mit identischen Textbausteinen gerendert (~1.8 KB).
  - **Vorschlag:** Leere Pattern-Blöcke bei 0 Treffern in eine zusammenfassende Zeile überführen (z. B. `0 Treffer für: god-class, async-void, long-method, empty-catch, feature-envy`), statt für jedes leere Pattern eine separate H2-Überschrift zu öffnen.
  - **Entscheidung:** UX-Formatierungsentscheidung zur Abstimmung; erfordert keine sofortige Codeänderung.
