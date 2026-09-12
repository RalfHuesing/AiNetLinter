# 19 – find_magic_values

## 1. Tool-Steckbrief & Agenten-Rolle
- **Name:** `find_magic_values`
- **Agenten-Priorität:** Prio 19 (Mittel – Clean-Code-Audit für unbenannte Literale und Magic Numbers)
- **Hauptzweck:** Sucht nach wiederholten String- und Zahlen-Literalen, die als Konstanten, Enums oder Konfigurationswerte ausgelagert werden sollten.
- **Wichtigste Parameter:**
  - `targetPath` (Pflicht, String): Absoluter Pfad zur `.sln`/`.slnx`.
  - `scopeFilter` (Optional, String): Pfadfilter.
  - `minOccurrences` (Optional, Int, Default 2): Mindesthäufigkeit.
  - `categoryFilter` (Optional, String, Default `all`): `constant_candidates`, `config_candidates`, `enum_candidates` etc.
  - `valueType` (Optional, String, Default `all`): `numbers`, `strings`, `all`.

---

## 2. Test-Samples & Rohe Tool-Outputs

### Sample A: Audit im Scope `src/AiNetLinter/Core` (`minOccurrences: 2`)
- **Aufruf-Parameter:**
```json
{
  "targetPath": "c:\\Daten\\Entwicklung\\Ralf\\AiNetLinter\\AiNetLinter.slnx",
  "scopeFilter": "src/AiNetLinter/Core",
  "minOccurrences": 2
}
```
- **Tool-Output (Rohdaten):**
```text
Magic-Value-Audit: 3 Treffer gesamt in 1 eindeutigen Einträgen über 75 Dateien im Scope
Status: checked; resultType=candidate; Confidence: high
Scope: 75 analysierbare C#-Dateien (scopeFilter='src/AiNetLinter/Core'; Tests ausgeschlossen; alle passenden Dateien)
Ursache: Alle 1 Kandidaten im angeforderten Scope wurden geprüft.
Truncation: total=1, returnedCount=1, truncatedBy=0

Kategorien:
- config_candidates: status=empty, total=0, returnedCount=0, truncatedBy=0, confidence=high
  Ursache: Keine Kandidaten in den 75 geprüften Dateien; dies ist keine globale Abwesenheitsbehauptung.
  Evidence: Statisches URL-/Pfad-/Connection-String-/Timeout-Muster im angeforderten C#-Scope; kein Nachweis einer konkreten Laufzeitkonfiguration.
  Scope: 75 analysierbare C#-Dateien (scopeFilter='src/AiNetLinter/Core'; Tests ausgeschlossen; alle passenden Dateien)
  Empfehlung: 
- constant_candidates: status=checked, total=1, returnedCount=1, truncatedBy=0, confidence=high
  Ursache: Alle 1 Kandidaten im angeforderten Scope wurden geprüft.
  Evidence: Statisches Format-String-/Schwellenwert-/Wiederholungs- oder const-Duplikat-Muster im angeforderten C#-Scope; keine Aussage über fachliche Änderungsfrequenz.
  Scope: 75 analysierbare C#-Dateien (scopeFilter='src/AiNetLinter/Core'; Tests ausgeschlossen; alle passenden Dateien)
  Empfehlung: Wert als benannte Konstante in einer fachlich passenden Constants-Klasse bündeln.
- enum_candidates: status=empty, total=0, returnedCount=0, truncatedBy=0, confidence=high
  Ursache: Keine Kandidaten in den 75 geprüften Dateien; dies ist keine globale Abwesenheitsbehauptung.
  Evidence: Statische Folge von mindestens drei Vergleichen desselben Identifiers im angeforderten C#-Scope; keine Aussage über weitere dynamische Werte.
  Scope: 75 analysierbare C#-Dateien (scopeFilter='src/AiNetLinter/Core'; Tests ausgeschlossen; alle passenden Dateien)
  Empfehlung: 
- nameof_candidates: status=empty, total=0, returnedCount=0, truncatedBy=0, confidence=high
  Ursache: Keine Kandidaten in den 75 geprüften Dateien; dies ist keine globale Abwesenheitsbehauptung.
  Evidence: Statischer exakter Abgleich eines String-Literals mit einem Symbolnamen im angeforderten C#-Scope; keine Laufzeit- oder Serialisierungssemantik.
  Scope: 75 analysierbare C#-Dateien (scopeFilter='src/AiNetLinter/Core'; Tests ausgeschlossen; alle passenden Dateien)
  Empfehlung: 
- localization_candidates: status=empty, total=0, returnedCount=0, truncatedBy=0, confidence=high
  Ursache: Keine Kandidaten in den 75 geprüften Dateien; dies ist keine globale Abwesenheitsbehauptung.
  Evidence: Statischer langer Text als Argument eines Exception-Konstruktors im angeforderten C#-Scope; kein Nachweis der tatsächlichen Benutzeroberfläche.
  Scope: 75 analysierbare C#-Dateien (scopeFilter='src/AiNetLinter/Core'; Tests ausgeschlossen; alle passenden Dateien)
  Empfehlung: 
- standard_candidates: status=empty, total=0, returnedCount=0, truncatedBy=0, confidence=high
  Ursache: Keine Kandidaten in den 75 geprüften Dateien; dies ist keine globale Abwesenheitsbehauptung.
  Evidence: Statischer HTTP-Statuscode oder kontextgebundene Buffer-Größe im angeforderten C#-Scope; kein Nachweis der konkreten Framework-Version.
  Scope: 75 analysierbare C#-Dateien (scopeFilter='src/AiNetLinter/Core'; Tests ausgeschlossen; alle passenden Dateien)
  Empfehlung: 
- security_candidates: status=empty, total=0, returnedCount=0, truncatedBy=0, confidence=high
  Ursache: Keine Kandidaten in den 75 geprüften Dateien; dies ist keine globale Abwesenheitsbehauptung.
  Evidence: Statisches Secret-/Credential-Muster im angeforderten C#-Scope; kein Nachweis, ob ein Secret-Store zur Laufzeit verwendet wird.
  Scope: 75 analysierbare C#-Dateien (scopeFilter='src/AiNetLinter/Core'; Tests ausgeschlossen; alle passenden Dateien)
  Empfehlung: 

src/AiNetLinter/Core/TestDetector.cs:230 - constant_candidates: "decompiled-assembly" (3x, Empfehlung: Constants.cs (Header-/Identifier-Konstante); Evidenz: Statisches Format-String-/Schwellenwert-/Wiederholungs- oder const-Duplikat-Muster im angeforderten C#-Scope; keine Aussage über fachliche Änderungsfrequenz.; Scope: 75 analysierbare C#-Dateien (scopeFilter='src/AiNetLinter/Core'; Tests ausgeschlossen; alle passenden Dateien))
```

### Sample B: Gefilterte Abfrage (`categoryFilter: "constant_candidates"`)
- **Aufruf-Parameter:**
```json
{
  "targetPath": "c:\\Daten\\Entwicklung\\Ralf\\AiNetLinter\\AiNetLinter.slnx",
  "scopeFilter": "src/AiNetLinter/Core",
  "categoryFilter": "constant_candidates",
  "minOccurrences": 2
}
```
- **Tool-Output (Rohdaten):**
*(Identisch zu Sample A: Trotz explizitem Filter werden alle 7 leeren Kategorien ungefiltert mit voller Erklärung ausgegeben)*

### Sample C: Leerer Scope (0 Treffer)
- **Aufruf-Parameter:**
```json
{
  "targetPath": "c:\\Daten\\Entwicklung\\Ralf\\AiNetLinter\\AiNetLinter.slnx",
  "scopeFilter": "src/AiNetLinter/Diagnostics"
}
```
- **Tool-Output (Rohdaten):**
```text
Magic-Value-Audit: 0 Kandidaten in 3 Dateien im angeforderten Scope. Status: empty; resultType=candidate; Confidence: high. Scope: 3 analysierbare C#-Dateien (scopeFilter='src/AiNetLinter/Diagnostics'; Tests ausgeschlossen; alle passenden Dateien). Ursache: Keine Kandidaten in den 3 geprüften Dateien; dies ist keine globale Abwesenheitsbehauptung..
```

---

## 3. Effizienz- & SNR-Bewertung (gemäß AiNetLinter-Richtlinien)
- **Token-Footprint:**
  - Sample A & B (1 einziger Treffer): **4.3 KB (~1.000 Token!)**.
  - Sample C (0 Treffer): 315 Bytes (~75 Token).
- **SCHWERWIEGENDER BEFUND (Signal-to-Noise Ratio):**
  - **SNR ist extrem schlecht (< 5% Signal!)**.
  - Von 44 Zeilen Output in Sample A entfallen **43 Zeilen auf Boilerplate**: Es werden für sämtliche 6 leeren Kategorien lange Paragraphen mit `Evidence`, `Ursache`, `Scope` und leerer `Empfehlung:` ausgegeben.
  - Der eigentliche Fund (`TestDetector.cs:230`) ist nur eine einzige Zeile ganz am Ende.
  - Selbst wenn der Aufrufer explizit `categoryFilter: "constant_candidates"` setzt (Sample B), unterdrückt der Server die 6 leeren Kategorien **nicht**.
  - Dies widerspricht direkt der Vorgabe in `AiNetLinter-Richtlinien.mdc`: *„Tool-Payloads sind radikal token-sparsam zu halten (kein Fülltext, keine einleitenden Erläuterungen, keine redundanten Pfad-/Scope-Wiederholungen in Listen).“*
- **Content-Only-Hard-Cut:** Konform.

---

## 4. Composability & Chaining-Validierung (Input/Output-Passgenauigkeit)
- **Erzeugte Ausgabefelder:**
  - Zeile 44 liefert: `src/AiNetLinter/Core/TestDetector.cs:230`
- **Chaining-Passgenauigkeit:**
  | Ausgabefeld | Ziel-Tool | Ziel-Parameter | Status |
  |---|---|---|---|
  | `Pfad:Zeile` | `get_symbol_body` | `symbolIdentifiers` | ✅ Direkt übergebbar |
  | `Pfad:Zeile` | Editor | `replace_file_content` | ✅ Für gezielte Konstantenextraktion |

---

## 5. Fazit & Architekturentscheidung (Offener Punkt)
- **Prädikat:** **Funktional stark, aber hoher Token-Footprint (Kontraktfrage)**
- **Stärken:**
  1. Treffgenauigkeit und fachliche Empfehlungen (z. B. Bündelung in Constants-Klasse) sind inhaltlich wertvoll.
  2. Granulare Heuristiken decken Config, Secrets, Enums und Konstanten sauber ab.
- **Architektonische Weichenstellung (Zur Abstimmung):**
  - **Befund:** Bei 1 Treffer werden 43 Zeilen Erklärungen für alle 6 leeren Kategorien ausgegeben (~4.3 KB). Auch bei explizitem `categoryFilter: "constant_candidates"` bleiben die leeren Kategorien im Text-Output enthalten.
  - **Hintergrund & API-Kontrakt:**
    - Dies ist kein zufälliger Bug, sondern in `Docs/agent-api.md` (Zeile 419 & 896ff) als expliziter Vertrag spezifiziert: *„Kandidaten mit sieben disjunkten Kategorien und je Kategorie status, cause, confidence, next, truncatedBy, evidenceBoundary, scope und recommendation im Content...“*.
    - Auch `FindMagicValuesPrecisionContractTests.cs` prüft vertraglich, dass stets alle 7 Kategorien mit ihren Evidenzgrenzen ausgewiesen werden.
  - **Optionen zur Behebung:**
    1. **Text-Projektion verschlanken (Empfohlen):** Im Text-Report leere Kategorien (`total=0`) unterdrücken oder auf eine knappe Statuszeile reduzieren, während das interne Datenmodell/Payload unverändert bleibt.
    2. **Strikte Filterung bei `categoryFilter`:** Wenn ein spezifischer Filter übergeben wird, nur diese eine Kategorie im Report darstellen.
    3. **Kontrakt beibehalten:** Wenn Clients zwingend die vollständige Disjunktheits-Erklärung aller 7 Kategorien erwarten.
  - **Entscheidung:** Erfordert formale Freigabe zur Anpassung des in `Docs/agent-api.md` definierten Content-Vertrags. Keine unautorisierte Vorab-Änderung.
