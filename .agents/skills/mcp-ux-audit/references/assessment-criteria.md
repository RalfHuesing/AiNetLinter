# Bewertungskriterien: MCP Agent-UX-Audit

Dieses Dokument definiert die sieben Bewertungskriterien für den MCP Agent-UX-Audit.
Es ist das Referenzdokument für Subagenten in Phase 3.

---

## 1. Parameter-Naming

**Kernfrage:** Heißt dasselbe konzeptuelle Ding in verschiedenen Tools unterschiedlich,
ohne dass die Unterschiede semantisch begründet sind?

**Was zu prüfen ist:**
- Dasselbe Konzept „Symbol-Bezeichner" heißt in manchen Tools `symbolIdentifier`,
  in anderen `pattern`, in wieder anderen `namePattern` oder `name`. Ist das absichtlich
  und dokumentiert, oder historisch gewachsen und verwirrend?
- Gleiche Konzepte, gleiche Namen? Unterschiedliche Konzepte, unterschiedliche Namen?

**Was OK ist:**
- `pattern` in `find_symbol` vs. `symbolIdentifier` in `get_feature_context`:
  bewusste Unterscheidung — `pattern` sucht, `symbolIdentifier` adressiert exakt.
  Das ist semantisch begründet.

**Was ein Befund ist:**
- `scopeFilter` in `find_magic_values` vs. `scopeDir` in `find_duplicates` —
  wenn beide denselben Zweck (Unterverzeichnis einschränken) haben, aber verschiedene
  Namen tragen ohne Erklärung in den Descriptions.

**Severity:** Minor wenn nur eine Kleinigkeit, Major wenn es einen Agenten
regelmäßig zur falschen Annahme verleitet.

---

## 2. Signal-Rausch-Verhältnis (SNR)

**Kernfrage:** Hat der Agent nach dieser Antwort eine neue, handlungsrelevante
Erkenntnis — oder primär Rauschen?

**Wichtig:** Die absolute Token-Zahl ist KEIN Kriterium. 10.000 Token können
vollständig sinnvoll sein (vollständiger Call-Graph einer komplexen Methode).
100 Token können pures Rauschen sein. Beurteile inhaltlich.

**Bekannte Rausch-Muster:**

| Muster | Beschreibung |
|--------|-------------|
| **Redundante Navigation-Blöcke** | Der `navigation`-Block ist bei jedem Call fast identisch und wiederholt Informationen die der Agent bereits hat |
| **Doppelte Dateilisten** | Eine Antwort enthält dieselbe Dateiliste die der Agent bereits aus `get_file_tree` kennt |
| **Inhaltsleere Boilerplate** | Feste Einleitungstexte die bei jedem Call identisch sind und keine neue Information tragen |
| **Binärdaten im Textkanal** | Binäre Daten oder Base64 im Textantwort-Teil statt in structuredContent |
| **Wiederholte Typdeklarationen** | Bei jedem Member wird der vollständige Klassenkontext wiederholt |
| **Ungenutzte Metadaten** | Felder in structuredContent die kein Tool je nutzt und nie in Dokumentation erwähnt werden |

**Was ein Befund ist:**
- Eine häufig verwendete Tool-Antwort enthält systematisch Blöcke, die ein Agent
  ignorieren muss weil sie keine actionable Information tragen.
- Die Antwort-Größe wächst mit der Codebasis, obwohl die angefragte Information
  konstant bleibt.

**Was OK ist:**
- Eine große Antwort bei einer breiten Abfrage (`find_symbol(namePattern=*)`)
  ist kein Befund wenn der Content informativ ist.
- Der `navigation`-Block mit `next`-Hinweis bei Trunkierung ist sinnvolles Signal,
  kein Rauschen.

---

## 3. Fehlerqualität

**Kernfrage:** Kann ein Agent allein aus der Fehlermeldung verstehen was falsch war
und wie er es korrigiert?

**Mindestanforderungen an eine gute Fehlermeldung:**
- Konkreter Feldname wenn ein Pflichtfeld fehlt oder falsch ist
- Hinweis auf gültige Werte wenn ein Enum-Wert ungültig ist
- Klarer Unterschied zwischen „Feld fehlt" und „Feld hat falschen Typ"
- Kein Stack-Trace im normalen Fehlerfall (nur in echten unerwarteten Exceptions)
- Kein „null reference exception" — das ist eine interne Implementierungsdetail

**Fehler-Code-Semantik:**
- `invalid_argument` → Eingabevalidierungsfehler (Client-Fehler, Agent kann korrigieren)
- `PROJECT_LOAD_FAILED` → Zieldatei nicht ladbar (Agent braucht anderen `targetPath`)
- Andere Fehlercodes → müssen selbsterklärend sein

**Was ein Befund ist:**
- Fehlermeldung enthält Stack-Trace oder interne Exception-Details
- „Value cannot be null" ohne Angabe welches Feld
- „An error occurred" ohne weitere Information
- Unterschiedliche Fehlercodes für dasselbe Szenario in verschiedenen Tools

---

## 4. Response-Struktur

**Kernfrage:** Liefert das Tool eine konsistente, vorhersehbare Antwortstruktur
die ein Agent programmgesteuert auswerten kann?

**Erwarteter `navigation`-Block (zielgebundene Tools):**
```json
{
  "navigation": {
    "target": "<absoluter Pfad>",
    "origin": "source | decompiled",
    "snapshot": "<snapshot-id>",
    "capabilities": { "lint": "...", ... },
    "operationStatus": "...",
    "result": "...",
    "completeness": "full | truncated | partial",
    "next": "<optionaler Hinweis>"
  }
}
```

**Was zu prüfen ist:**
- Ist `structuredContent` vorhanden (nicht nur `content[].text`)?
- Sind alle `navigation`-Felder konsistent über alle Tools?
- Sind die Wertesets der Felder (z.B. `completeness`, `origin`) über alle Tools gleich?
- Ist `result` vs. `operationStatus` klar unterschieden?

**Was ein Befund ist:**
- Ein Tool liefert `completeness=truncated` im Text aber `completeness=full` in `structuredContent`
- `navigation.next` fehlt obwohl `completeness=truncated`
- Unterschiedliche Feldnamen für dasselbe Konzept zwischen Tools (z.B. `status` vs. `operationStatus`)

---

## 5. Chaining-Kompatibilität

**Kernfrage:** Kann ein Agent den Output von Tool A direkt als Input für Tool B
verwenden — ohne eigene Transformation oder Parsing?

**Konkrete Prüfung:**
- `find_symbol` gibt eine Symbol-ID zurück → Kann diese direkt als `symbolId` in
  `get_symbol_body` verwendet werden? (Format muss stimmen)
- `get_feature_context` enthält Caller-Namen → Können diese direkt als `symbolIdentifier`
  in `find_references` verwendet werden?
- `get_file_tree` enthält Dateipfade → Können diese direkt als `targetFile` in
  `get_file_skeleton` verwendet werden?

**Was ein Befund ist:**
- Eine Symbol-ID aus `find_symbol` hat ein anderes Format als das was `get_symbol_body` erwartet
- Pfade in Tool-Antworten sind relativ aber Tools erwarten absolute Pfade
- Ein Tool gibt Namen zurück die das nächste Tool nicht direkt akzeptiert
  (z.B. `Namespace.ClassName.MethodName(param)` statt `ClassName.MethodName`)

**Was OK ist:**
- Der Agent muss zwischen `find_symbol.pattern` (Suche) und `find_references.symbolIdentifier`
  (exakte Adressierung) unterscheiden — das ist dokumentiertes semantisches Unterschied.

---

## 6. Schema-Vollständigkeit

**Kernfrage:** Kann ein Agent aus dem Tool-Schema allein verstehen wie das Tool
korrekt aufgerufen wird?

**Was zu prüfen ist:**
- Hat jeder Parameter eine aussagekräftige `description` in `tools/list`?
- Sind `required` und optionale Parameter korrekt deklariert?
- Gibt es Parameter die in der Beschreibung fehlen aber vom Server akzeptiert werden?
- Gibt es Parameter die in der Beschreibung stehen aber mit `invalid_argument` abgelehnt werden?

**Was ein Befund ist:**
- `description` ist leer, fehlt oder enthält nur den Parameternamen
- Ein `required: true` Parameter ist tatsächlich optional (oder umgekehrt)
- Enum-Werte sind nicht in der `description` aufgelistet
- Phantom-Parameter: existiert in Schema, wirft `invalid_argument` wenn übergeben

---

## 7. Completeness-Signaling

**Kernfrage:** Weiß ein Agent ob er alle Daten hat, oder ob es mehr gibt?

**Pflicht-Verhalten bei Trunkierung:**
- `completeness` muss `truncated` oder `partial` sein (nicht `full`)
- `next` muss einen konkreten Hinweis enthalten wie der Agent mehr Daten bekommt
- Die Antwort muss die tatsächliche Anzahl der Gesamtergebnisse angeben wenn möglich

**Was ein Befund ist:**
- `completeness=full` obwohl Ergebnisse durch `maxResults` abgeschnitten wurden
- Trunkierung nur im Fließtext erkennbar, aber `completeness=full` in structuredContent
- `next` fehlt bei `completeness=truncated` — Agent weiß nicht wie er weiter kommt
- Kein Hinweis auf Gesamtanzahl — Agent kann nicht abschätzen wie viele Ergebnisse fehlen

---

## Severity-Entscheidungsmatrix

| Situation | Critical | Major | Minor |
|-----------|----------|-------|-------|
| Blockiert Standardaufgabe komplett | ✓ | | |
| Fehler bei gültigem Call | ✓ | | |
| Kaputtes Chaining in Standardsequenz | ✓ | | |
| Schlechtes SNR bei sehr häufigen Calls | | ✓ | |
| Inkonsistentes Parameter-Naming (häufiges Konzept) | | ✓ | |
| Fehlendes Completeness-Signal | | ✓ | |
| Unleserliche aber ausweichbare Fehlermeldung | | ✓ | |
| Verbesserungswürdige Description | | | ✓ |
| Inkonsistenz die nur bei seltenen Calls auftritt | | | ✓ |
| Beobachtung aus Freier Erkundung ohne klares Problem | | | ✓ |
| Überraschend interessantes Verhalten (positiv oder neutral) | | | ✓ |
