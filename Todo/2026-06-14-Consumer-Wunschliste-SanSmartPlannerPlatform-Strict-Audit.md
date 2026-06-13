# AiNetLinter — Consumer-Wunschliste (Strict-Audit)

**Datum:** 2026-06-14  
**Quelle:** `San.smart.Planner.Platform` — `platform-ai-strict-wave-ready.txt`  
**AiNetLinter:** `1.0.23+093dc9aa`  
**Config:** `platform-ai-strict.rules.json` + `--wave-ready` (nur freigeschaltete Dateien)  
**Kontext:** Erster Consumer mit produktivem Zwei-Stufen-Modell (`platform-default` aktiv, `platform-ai-strict` als Zielbild-Teleskop)

---

## 1. Big Picture — Was AiNetLinter für uns leisten soll

AiNetLinter ist für uns **kein klassischer „alles grün“-Linter**, sondern ein **Agenten-Leitplanken-System**:

| Ebene | Rolle |
| --- | --- |
| **Produktion (`platform-default`)** | Pragmatische Regeln für **neuen Code** + LLM-relevante Heuristiken (Footprint, Phantom-Deps, Truncation) |
| **Zielbild (`platform-ai-strict`)** | Teleskop: zeigt **Richtung**, ohne jeden Verstoß sofort beheben zu müssen |
| **Artefakte** | `AiNetLinter.mdc`, `playbook.mdc`, Codegraph, SARIF — **Kontext für Cursor/Agenten** |
| **Migration** | Wellen (`disable all` → refactoren → `--wave-ready`), optional Baseline-Ratchet |

**Erfolg** heißt: Ein Agent (oder Mensch) versteht schneller **wo** Architektur-Risiken liegen und **was** beim Editieren wirklich zählt — nicht: 1.400 mechanische Verstöße abarbeiten.

**Aktueller Strict-Report:** **1.398 Verstöße** auf wave-ready-Dateien. Davon **~74 %** allein `EnforceNoMagicValues` (1.033). Das verzerrt das Signal massiv.

---

## 2. Kurzfazit (Executive Summary)

| Regel / Bereich | Signal für LLM? | Strict-Report | Empfehlung |
| --- | --- | ---: | --- |
| Footprint, Phantom-Deps, Truncation, Silent-Catch | **Hoch** | wenige in default, sinnvoll | Behalten, weiter schärfen |
| Komplexität / Konstruktor-Deps | **Mittel–hoch** | 71+47+32 | Grenzwerte konfigurierbar; „Near-miss“-Modus |
| `EnforceNoMagicValues` (Ist) | **Niedrig** (Noise) | **1.033** | **Stark differenzieren** (siehe §4.1) |
| `EnforceResultPatternOverExceptions` | **Mittel** (Kontext) | 53 | Opt-in / Allowlist / Projekt-`Result<T>` |
| `EnforceExplicitStateImmutability` | **Mittel** (Blazor!) | 118 | UI-/Lifecycle-Ausnahmen |
| `EnforceNamespaceDirectoryMapping` | **Hoch** (wenn korrekt) | 44 | Feature-Folder-Modi |
| Codegraph (1.0.23) | **Hoch** | — | Behalten; optional Mermaid-Export |

**Kernbotschaft an Maintainer:** Der Strict-Stack ist wertvoll, aber **`EnforceNoMagicValues` in der jetzigen Form skaliert nicht** — er bestraft genau die Muster, die in metadata-getriebenen und API-lastigen Codebasen **unvermeidlich** sind (JSON-Keys, Routen, Fehlermeldungen, Log-Templates).

---

## 3. Was bereits gut funktioniert (beibehalten)

Aus Round 1–3 und Strict-Lauf:

- **`--wave-ready`** + `disable all`-Migration — ehrliches Inkrement statt Big-Bang
- **`--sync-cursor-rules` + `--playbook`** in einem Lauf (≥ 1.0.22)
- **Intent-Tags** in Summary (`agent-context`, `architecture`, …) — hilft Priorisierung
- **`RequireExplicitTruncationHandling`** — Async-Flow ab **1.0.23** ohne Suppression (Fix bestätigt)
- **Codegraph** statt Mermaid — für Agenten oft **lesbarer** (Namespaces, Typen, Kanten in Textform)
- **`ProjectOverrides`** (`*.Tests`) — essentiell
- **`ImmutabilityExemptPatterns`** — richtige Richtung für DTO/Options-Noise
- **Report-Modus** (Exit 0/1 OK) — Consumer können Zielbild reporten ohne CI-Blocker

---

## 4. Regel-für-Regel: Analyse & Wunsch-Optionen

### 4.1 `EnforceNoMagicValues` — **P0: differenzieren oder Strict entwertet**

**Ist-Zustand** (`LinterAnalyzer.MagicValues.cs`):

- Jeder `string`- und `numeric`-Literal im Methodenbody ist „magic“, außer `""`, `0`, `1`, `-1`
- Keine Unterscheidung nach **Semantik des Literals**
- `const`-Deklaration am selben Ort ist erlaubt — führt zu Const-Wäldern ohne echten Gewinn

**Typische False Positives im Strict-Report (mit Beispielen):**

| Kategorie | Beispiel aus Report | Warum kein echter Magic Value |
| --- | --- | --- |
| Nutzer-/API-Texte | `"Der Benutzername ist ungültig."` | Text *ist* die Semantik |
| RFC 9457 / ProblemDetails | `title: "Conflict"` | Protokoll-Feld |
| Minimal-API-Routen | `"/data/{site}/{componentId}"` | Routing-Deklaration |
| Log-Templates | `"Aktive UI-Sitzungen: {UserCount} …"` | Serilog-Structured-Logging |
| **JSON/Metadata-Keys** | `"sqlFile"`, `"columns"`, `"rowVersion"` | Metadata-over-Code — Keys sind Schema |
| OAuth/Form-Felder | `"grant_type"`, `"password"` | Protokoll-Spezifikation |
| SQL-Parser-Lexeme | `"ORDER"`, `"BY"` | Fachlicher Token-Vergleich |
| Format-/CSS-Literale | `"N2"`, `"yyyy-MM-dd"`, `"site-ui-datatable-align-right"` | Darstellungskonstanten |
| Kurz-Pfade | `"/"`, `"/login"`, `"ok"` | Selbsterklärend oder Idiom |
| Separator | `", "` | Formatierung |

**Top-Dateien nach Verstoßzahl** sind symptomatisch:

- `TimelineViewLabLoadTestData.cs` (170) — Test-/Seed-Daten
- `AppSettingsFieldHints.Exacts.cs` (130) — UI-Hilfetexte
- `DataTableHandlerSettings.cs` (62) — JSON-Key-Mapping
- `SiteModels.cs` (48) — Metadaten-Modelle

**Wunsch: konfigurierbare Profile** (JSON unter `Global` oder eigene Sektion `MagicValues`):

```json
"MagicValues": {
  "Mode": "numeric-and-threshold",   // "all" | "numeric-only" | "numeric-and-threshold" | "off"
  "MinStringLength": 0,              // z. B. 3: "/" nicht melden, lange Texte schon
  "MaxStringLength": 80,             // darüber: eher Message/Template → ignorieren
  "IgnoreNumericInRange": [-1, 1], // erweitern: auch 2, 32 für bekannte Idiome
  "IgnoreStringPatterns": [
    "^/[\\w/{}/-]+$",                // Routen
    "^[a-z][a-zA-Z0-9]*$"            // camelCase JSON-Keys (optional strenger)
  ],
  "IgnoreInArgumentPositions": [
    "detail", "detailDev", "detailProd", "title", "message", "name"
  ],
  "IgnoreWhenParentInvocationContains": [
    "Log.", "LogError", "LogWarning", "LogInformation",
    "MapGet", "MapPost", "MapGroup",
    "GetSection", "GetProperty", "TryGetProperty",
    "TypedResults.Problem", "ApiProblems."
  ],
  "IgnoreAttributeArguments": true,  // bereits teilweise; dokumentieren
  "IgnoreCollectionExpressions": true // `["grant_type"] = "password"` in Initializern
}
```

**Alternativ (einfacher): Intent-basierte Presets:**

| Preset | Verhalten |
| --- | --- |
| `strict-llm` | Heute: alles string/number |
| `pragmatic` | Nur numerische Literale außer 0/1/-1; Strings nur wenn Länge ≤ 2 **und** nicht in Allowlist |
| `metadata-aware` | Zusätzlich: String-Literal als Argument von `nameof`, JSON-Accessor, `GetSection` ignorieren |
| `off` | Regel aus |

**Report-DX:** Sub-Kategorien in Details, z. B.:

`EnforceNoMagicValues | kind=user-message | literal="…"`

**Für LLM:** Nur `kind=numeric-threshold` und `kind=protocol-ambiguous` wären echte Agenten-Hinweise.

---

### 4.2 `EnforceResultPatternOverExceptions` — **P1: Kontext-sensitiv**

**Ist:** Jedes `throw` außerhalb von Konstruktor oder `*Guard`/`*Validate` ist Verstoß. `AllowedExceptions`-Liste hilft nur bei **Typ**, nicht bei **Kontext**.

**Beispiele aus Report:**

- `SqlLoginAuthHandler` — `throw` bei fehlendem Connection-String (Infrastruktur)
- `DataTableHandlerSettings` — `throw` bei ungültigem JSON (Validierung beim Deserialisieren)
- `PlantafelKalenderBackgroundBuilder` — `throw` nach SQL-Fehler

**Spannung:** Projekt nutzt **bewusst** Exceptions für unerwartete Fehler (Serilog + ProblemDetails) und hat parallel **~83 `Result`/`Result<T>`-Methoden** (Playbook). Regel suggeriert „alles Result“, was ASP.NET-/C#-Idiom widerspricht.

**Wünsche:**

```json
"ResultPattern": {
  "Mode": "suggest",                    // "off" | "suggest" | "enforce"
  "RecognizeProjectResultTypes": [        // nicht nur BCL Result
    "San.smart.Planner.Platform.*.Result",
    "San.smart.Planner.Platform.*.Result`1"
  ],
  "AllowThrowIn": [
    "Infrastructure",                    // Namespace-Suffix-Patterns
    "Program",
    "*Endpoints"
  ],
  "AllowThrowWhenEnclosingMethodMatches": [
    "*Async",                            // optional kontrovers
    "Parse*", "TryParse*"
  ],
  "AllowInCatchRethrow": true,
  "CountOnlyBusinessLogicPaths": true     // Verknüpfung mit EnforceStrictBoundary
}
```

**Guidance verbessern:** Statt pauschal „Result statt throw“ → „Fachlicher **erwartbarer** Fehlerpfad: `Result<T>`; **unerwarteter**/Infrastruktur-Fehler: throw + log + ProblemDetails“.

---

### 4.3 `EnforceExplicitStateImmutability` — **P1: UI-/Blazor-Modus**

**118 Verstöße**, z. B. `PlatformAuthStateProvider._cachedState` — Felder die **absichtlich** mutieren (Cache, UI-Flags, Debounce-CTS).

**Wunsch:**

```json
"Immutability": {
  "AllowMutableFieldsIn": ["*Component", "*Provider", "*Store", "*HubClient"],
  "AllowWhenFieldNamePrefix": ["_"],
  "AllowBlazorInjectFields": true,       // bereits EnforceReadonlyFields-Ausnahme für @ref
  "ExemptBaseTypes": ["ComponentBase", "BackgroundService"]
}
```

Oder: Regel nur auf **nicht-UI**-Namespaces anwenden (`EnforceStrictBoundary`-Synergie).

---

### 4.4 `EnforceNamespaceDirectoryMapping` — **P1: Feature-Folder-Strategien**

**44 Verstöße** — typisch wenn Ordner `Handlers/Domains/Firmenkalender/` aber Namespace flacher ist.

**Wunsch:**

```json
"NamespaceDirectoryMapping": {
  "Mode": "segment-count",              // "exact" | "segment-count" | "suffix-match"
  "RequiredTrailingSegments": 2,        // letzte N Ordnersegmente müssen Namespace-Suffix sein
  "IgnorePathPrefixes": ["Handlers/Domains/"],
  "AllowFileScopedNamespaceMismatch": false
}
```

**Report:** Verstoß sollte **Soll-Namespace** und **Ist-Namespace** + **vorgeschlagener Pfad** zeigen.

---

### 4.5 Komplexität & Konstruktor-Deps — **P2: Near-Miss & Partial-Class**

**Strict:** Cyclomatic/Cognitive **5**; Default: **8/7**. Viele Verstöße sind **6 vs. 5** — Grenzfall, kein katastrophales Design.

**Wünsche:**

```json
"Metrics": {
  "ComplexityNearMissTolerance": 1,     // 6 bei Limit 5 → warning, 8 → error
  "AggregatePartialClassComplexity": true, // Komplexität über partials summieren oder splitten
  "ExcludeSwitchDispatcherCases": true
}
```

#### Switch-Dispatcher-Ausnahme (`ExcludeSwitchDispatcherCases`)

**Hinweis:** Der frühere Verweis „wie in Consumer-Docs beschrieben“ meinte **kein separates Dokument** (weder Round-1–3-Feedback noch ein Maintainer-PDF), sondern eine **Projekt-Konvention** in `San.smart.Planner.Platform`: Handler routen UI-Befehle über `ExecuteCommandAsync` als flachen Dispatcher. Die Regel war in `CodeQualitaet.mdc` kurz als Absicht formuliert, ist dort inzwischen nicht mehr explizit — **daher hier vollständig** für den AiNetLinter-Maintainer.

**Muster:** Eine Methode routet nur nach einem Diskriminator (typisch `commandName` in `ExecuteCommandAsync`) — per `switch` oder äquivalenter `if`-Kette. Jeder Zweig ist **ausschließlich**:

- ein **Hilfsmethodenaufruf** (`return HandleExtendItem(...)`), oder
- ein **Einzeiler** (`return Task.FromResult(...)`), oder
- ein **weiterer Delegationsaufruf** (`return base.ExecuteCommandAsync(...)`).

**Keine** komplexe Inline-Logik in den Cases (keine verschachtelten `if`/`try`/`foreach` mit eigener Verzweigungstiefe).

**Beispiel im Repo** (`TimelineViewLabSchedulerHandler.ExecuteCommandAsync`):

```csharp
if (cmd.Equals("extend-item", StringComparison.OrdinalIgnoreCase))
    return Task.FromResult(HandleExtendItem(resolution, parameters));

if (cmd.Equals("move-item", StringComparison.OrdinalIgnoreCase)
    || cmd.Equals("update-item", StringComparison.OrdinalIgnoreCase))
    return Task.FromResult(BuildLabMutationEcho(parameters));
// … weitere Befehle → jeweils private Hilfsmethode
```

**Warum das für LLM-Agenten gut ist:** Die Dispatch-Methode bleibt eine **lesbare Routing-Tabelle**; Fachlogik liegt in benannten privaten Methoden — genau das Muster, das `StaticTestSentinel` und Refactorings erleichtern.

**Problem heute:** Jeder `case`/`if`-Zweig erhöht zyklomatische und kognitive Komplexität. Ein Handler mit 10–15 Metadaten-Befehlen überschreitet Strict **5/5** trivial, obwohl die Methode strukturell trivial bleibt.

**Wunsch an AiNetLinter** — wenn `ExcludeSwitchDispatcherCases: true`:

| Kriterium | Erwartung |
| --- | --- |
| Erkennung | `switch` auf `string`/Enum **oder** homogene `if (x.Equals("…"))`-Kette auf dieselbe Variable |
| Qualifikation | Case-Body ≤ N Zeilen (z. B. 3) und nur `return`/`await` + Methodenaufruf |
| Metrik | Case-Arms **nicht** zur Cyclomatic/Cognitive Complexity der Dispatcher-Methode zählen |
| Grenze | Verschachtelte Logik **im** Case → normal mitzählen (kein Freifahrtschein) |
| Report | Optional: `complexity-exempt: switch-dispatcher (N arms)` in Guidance |

**Abgrenzung:** `FormSiteComponentHandler.ExecuteCommandAsync` mit Validierungs-`if` **innerhalb** eines Zweigs ist **kein** reiner Switch-Dispatcher — dort soll die Metrik greifen.

---

`MaxConstructorDependencies: 5` bei Primary-Constructor-DI (Blazor, Handlers mit 8–16 Params) — **Record-Parameter-Objekte** sind richtig, aber:

- Meldung sollte vorschlagen: **welches Record** (`AiChatTurnRequest`-Muster)
- Option: `CountOnlyReferenceTypes: true` oder `IgnoreFrameworkTypes: ["ILogger", "IOptions", "IHostEnvironment"]`

---

### 4.6 Regeln im Strict-Stack ohne Treffer im Top-Report

In `platform-ai-strict` aktiv, aber **nicht** in den Top-6 der 1.398 Verstöße:

- `EnforceStrictBoundaryForBusinessLogic`
- `PreventContextDependentOverloads`
- `AIContextFootprint` (Limit 4000 vs. 5000 default)
- `MaxDirectoryDepth`, `MaxMethodOverloads`

**Wunsch:** `--summary-by-rule` auch **0-count** Regeln listen (Strict-Katalog vollständig sichtbar). Optional `--only-rules EnforceNoMagicValues` zum Fokussieren.

---

## 5. Report- & DX-Wünsche (regelübergreifend)

### 5.1 Signal-Rauschen-Filter

```bash
ainetlinter --wave-ready --min-severity error --intent agent-context,architecture
ainetlinter --wave-ready --exclude-rules EnforceNoMagicValues
ainetlinter --wave-ready --group-by kind   # bei MagicValues sub-kind
```

### 5.2 Strict als Teleskop, nicht als Auftrag

CLI-Hinweis wenn `> N` Verstöße einer Regel:

> „73 % der Verstöße sind EnforceNoMagicValues. Erwäge Preset `metadata-aware` oder `--exclude-rules`.“

### 5.3 Codegraph-Erweiterungen (1.0.23+)

Aktuell gut: Text-Codegraph mit Namespaces und `→`-Kanten.

**Wünsche:**

| Option | Nutzen |
| --- | --- |
| `--graph-format text\|mermaid\|both` | Mermaid für Menschen, Text für Agenten |
| `--graph-max-types 200` | Token-Budget |
| `--graph-footprint` | Nur Typen über Footprint-Schwelle |
| `--graph-slice Handlers/Admin` | Ordner-Slice |

### 5.4 Playbook / Cursor-Sync

- **Regel-Presets** in generierter `AiNetLinter.mdc` dokumentieren („Strict nutzt MagicValues: numeric-only“)
- **Violation-Heatmap** im Playbook: Top-3 Regeln mit **Anteil %** (nicht nur Counts)

### 5.5 Baseline + Strict kombinieren

```bash
ainetlinter --baseline platform-baseline.json --only-changed --config platform-ai-strict.rules.json
```

Dokumentieren: Wann Baseline-Ratchet vs. Wellen vs. Strict-Report.

---

## 6. Vorgeschlagene Config-Architektur (Flexibilität)

Ziel: **Ein Tool, viele Consumer-Profile** — nicht jeder Consumer forked `rules.json`.

```
rules.json
├── Global          (bool-Schalter, wie heute)
├── Metrics         (Zahlen, wie heute)
├── RuleMetadata    (severity, intent)
├── ProjectOverrides
├── Presets         (NEU: benannte Profile)
│   ├── "llm-pragmatic"
│   ├── "llm-strict"
│   └── "metadata-heavy"    // SanSmartPlannerPlatform-ähnlich
└── RuleExtensions  (NEU: regel-spezifische Unter-Optionen)
    ├── MagicValues { ... }
    ├── ResultPattern { ... }
    ├── Immutability { ... }
    └── NamespaceMapping { ... }
```

**CLI:**

```bash
ainetlinter --preset llm-strict --config rules.json   # Preset merged über rules.json
ainetlinter --config platform-default.rules.json        # explizit, wie heute
```

Consumer behält:

- `platform-default` = Produktion + Agenten
- `platform-ai-strict` = Teleskop / Migrations-Inventar
- optional `platform-metadata` = Preset mit `MagicValues.Mode: metadata-aware`

---

## 7. Priorisierte Roadmap (aus Consumer-Sicht)

| Prio | Thema | Aufwand (Schätzung) | Impact |
| --- | --- | --- | --- |
| **P0** | `EnforceNoMagicValues` Profile / Ignore-Kontexte | mittel | **Sehr hoch** — Strict-Report von 1.400 auf <300 sinnvolle Treffer |
| **P0** | MagicValues Sub-Kinds im Report | klein | Hoch — Agenten-Priorisierung |
| **P1** | `ResultPattern` Mode + Projekt-`Result<T>` | mittel | Mittel — weniger Ideologie, mehr Signal |
| **P1** | Immutability UI-/Blazor-Ausnahmen | klein | Mittel |
| **P1** | Namespace Mapping Feature-Folder-Modi | mittel | Mittel |
| **P2** | Complexity Near-Miss / Partial-Aggregation | klein | Mittel |
| **P2** | `--graph-format both`, Slice-Filter | klein | Mittel |
| **P2** | CLI `--exclude-rules`, `--intent` Filter | klein | Hoch für DX |
| **P3** | Preset-System in rules.json | größer | Langfristig Multi-Consumer |

---

## 8. Was wir als Consumer **nicht** wollen

- Dass Strict-Report mechanisch **grün gemacht** wird (Const-Wälder, Fake-Async, Suppressions-Flut)
- Dass dieselbe Regel in **default und strict** identisch ist ohne Preset-Differenzierung
- Dass Metadata-Keys und API-Texte als „Magic“ behandelt werden wie `timeoutMs = 30000`
- Dass `throw` in ASP.NET-Infrastruktur pauschal als „schlecht für LLM“ gilt

---

## 9. Referenz-Zahlen (Repro)

```powershell
& "C:\Daten\AiNetLinter-win-x64\AiNetLinter.exe" `
  --config "San.smart.Planner.Platform.Tests\AiNetLinter\rules\platform-ai-strict.rules.json" `
  --path "C:\Daten\Entwicklung\SAN\San.smart.Planner.Platform" `
  --wave-ready
```

**Ergebnis 2026-06-14:**

| Regel | Count |
| --- | ---: |
| EnforceNoMagicValues | 1.033 |
| EnforceExplicitStateImmutability | 118 |
| MaxCyclomaticComplexity | 71 |
| EnforceResultPatternOverExceptions | 53 |
| MaxCognitiveComplexity | 47 |
| EnforceNamespaceDirectoryMapping | 44 |
| **Gesamt** | **1.398** |

**Vergleich:** `platform-default` + `--wave-ready` → **0 Verstöße** (nach 1.0.23-Fixes). Das zeigt: **Strict ist bewusst ein anderes Zielbild** — aber 74 % MagicValues sind kein Zielbild, sondern Noise.

---

## 10. Kontakt / Follow-up

Bereit für:

- Pairing-Session zu `LinterAnalyzer.MagicValues.cs` (gemeinsam Ignore-Heuristiken definieren)
- PR mit `platform-metadata`-Preset als Fixture-Config für AiNetLinter-Tests
- Re-Eval nach nächster Version mit gleicher Metrik-Tabelle

**Vorgänger-Dokumente:**

- `C:\Users\Ralf\Downloads\2026-06-13-Consumer-Feedback-SanSmartPlannerPlatform.md` (Round 1)
- `C:\Users\Ralf\Downloads\2026-06-13-Consumer-Feedback-SanSmartPlannerPlatform-Round2.md`
- `C:\Users\Ralf\Downloads\2026-06-13-Consumer-Feedback-SanSmartPlannerPlatform-Round3.md`

---

*Erstellt von Consumer-Agent (San.smart.Planner.Platform) auf Basis des vollständigen `platform-ai-strict-wave-ready.txt`.*
