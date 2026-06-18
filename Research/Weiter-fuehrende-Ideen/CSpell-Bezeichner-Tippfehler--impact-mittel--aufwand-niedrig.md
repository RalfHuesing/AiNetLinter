# CSpell — Tippfehler in Bezeichnern (extern, CI-Schritt)

**Impact: mittel | Aufwand: niedrig | Haltbarkeit: dauerhaft**
**Umsetzung: als CI-Schritt, nicht in AiNetLinter**

---

## Problem

LLMs lesen Tokens, keine Buchstaben. Abkürzungen und Tippfehler zerstören die semantische
Leistungsfähigkeit des Modells:

```csharp
// Tippfehler in Bezeichnern:
public class CostomrService { }      // "Costomr" statt "Customer"
private int _itmsPerPage;            // "itms" statt "items"
public bool IsValdiated { get; }     // "Valdi" statt "Valid"

// Schädliche Abkürzungen:
public void ProcOrd(Cstmr c) { }    // Was bedeutet das?
private List<Itm> _itms;            // Token ohne semantischen Wert
```

Das Modell hat `CustomerService`, `ItemsPerPage`, `IsValidated` aus Milliarden Zeilen
trainierter Code gelernt — diese Wörter haben starke semantische Repräsentationen.
Tippfehler und opake Abkürzungen haben diese Repräsentation nicht.

**Konsequenz:** Der Agent "versteht" den Code schlechter und generiert Code der inkonsistent
mit dem bestehenden Naming ist — manchmal korrigiert er den Tippfehler, manchmal übernimmt er ihn.

---

## Umsetzung: CSpell

CSpell ist der einzige externe Vorschlag der wirklich wartungsarm funktioniert — es bringt
eigene englische Wörterbücher mit, und eine projektspezifische `cspell.json` mit Domain-Begriffen
wächst organisch.

### Installation

```bash
npm install -g cspell
# oder im CI:
npx cspell "**/*.cs"
```

### cspell.json

```json
{
    "version": "0.2",
    "language": "en",
    "words": [
        "Auftrag", "Fertigmeldung", "Rückmeldung",
        "MRP", "BOM", "PPS", "ERP",
        "dto", "Dto", "dto"
    ],
    "ignorePaths": [
        "**/*.g.cs",
        "**/Migrations/**",
        "**/obj/**",
        "**/bin/**"
    ],
    "ignoreRegExpList": [
        "/#.*/"
    ]
}
```

**Domain-Begriffe** (`BOM`, `MRP`, `Fertigmeldung`) müssen als bekannte Wörter eingetragen werden.
Das ist einmaliger Aufwand — danach wächst die Liste organisch wenn neue Domain-Begriffe
eingeführt werden.

### CI-Integration (GitHub Actions)

```yaml
- name: Check spelling in C# files
  run: npx cspell "**/*.cs" --no-progress
  continue-on-error: false
```

### Lokale Nutzung (Pre-Commit)

```bash
# .husky/pre-commit oder lokal:
npx cspell "**/*.cs" --changed
```

---

## Was CSpell erkennt

- Tippfehler: `Costomr`, `Valdi`, `resuilt`
- Opake Abkürzungen: `itm`, `tmp`, `mgr`, `hlpr` (je nach Konfiguration)
- Mischsprachliche Bezeichner: `KundeManager`, `AuftragService` (wenn German words not in dict)

**Was CSpell nicht erkennt:** Semantisch falsche aber korrekt geschriebene Bezeichner
(`CustomerMapper` der eigentlich ein `CustomerFactory` ist).

---

## Praxis-Bewertung

| Dimension | Bewertung |
| :--- | :--- |
| Wartungsaufwand | Niedrig — `cspell.json` wächst organisch mit Domain-Begriffen |
| False-Positive-Risiko | Mittel initial — Domain-Abkürzungen müssen einmalig markiert werden |
| Adoptionsbarriere | Sehr niedrig — `npx cspell "**/*.cs"` im CI |

**Empfehlung:** Direkt nutzbar. Der einzige externe Vorschlag der wirklich wartungsarm
funktioniert. Kein AiNetLinter-Entwicklungsaufwand.

---

## Haltbarkeit

Tippfehler in Bezeichnern werden immer ein Problem sein — unabhängig von Modell-Verbesserungen.
Der Token-Embedding-Mechanismus von LLMs bevorzugt bekannte Wörter dauerhaft.
