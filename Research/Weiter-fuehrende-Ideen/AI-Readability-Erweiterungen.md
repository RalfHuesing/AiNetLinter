# AI-Readability-Erweiterungen für AiNetLinter

> **Ausgangsfrage:** Welche weiteren Dinge könnten wir mit anderen Tools implementieren,
> um "guten" (für LLMs verständlichen) Code zu erzwingen?
> Das Tool soll lokal und deterministisch laufen.
> Aktuell verwenden wir Roslyn mit statistischen Analysen.

---

## Kontext: Was haben wir bereits?

| Implementiert | Regel / Metrik |
| :--- | :--- |
| ✅ | `MaxAIContextFootprint` (max. 5.000 transitive Zeilen eigener Typen) |
| ✅ | `MaxCyclomaticComplexity`, `MaxCognitiveComplexity` |
| ✅ | `MaxLineCount`, `MaxMethodLineCount` |
| ✅ | `MaxConstructorDependencies` |
| ✅ | `EnforceSealedClasses`, `EnforceNullableEnable` |
| ✅ | `EnforceSemanticNaming` (verbietet `data`, `temp`, `obj`) |
| ✅ | `EnforceNoSilentCatch` |
| ✅ | `EnableTestSentinel` |
| ⚠️ deaktiviert | `DetectAndBanPhantomDependencies`, `EnforceExplicitStateImmutability`, `PreventContextDependentOverloads`, `EnforceStrictBoundaryForBusinessLogic`, `RequireExplicitTruncationHandling` |

---

## Validierte Ideen (ursprüngliche Analyse)

Wenn wir uns anschauen, wie LLMs technisch funktionieren (Tokenisierung, Attention-Mechanismen, begrenztes Kontextfenster), gibt es deterministische, lokal messbare Aspekte, die man mit Roslyn oder einfachen Ergänzungstools implementieren könnte.

---

### 1. "Context Fan-Out" Metrik

**Status: ✅ Bereits implementiert als `MaxAIContextFootprint`**
**Impact: War hoch — ist abgedeckt**

Zähle die Anzahl einzigartiger, projekteigener Typen die transitiv in eine Datei hineingezogen werden. Existiert als `MaxAIContextFootprint: 5000` (transitive Zeilen eigener Typen). Die Idee ist vollständig implementiert.

---

### 2. Typo- & Wording-Checks (Tokenisierungs-Optimierung)

**Status: ⚠️ Teilweise — Ubiquitous Language fehlt noch**
**Impact: Hoch** (besonders der Vokabular-Konsistenz-Teil)

LLMs lesen keine Buchstaben, sondern Tokens. Ein Tippfehler oder schlechte Abkürzung wie `CstmrDta` zerfällt in bedeutungslose Sub-Tokens und zerstört die semantische Leistungsfähigkeit des Modells massiv.

**Teilaspekt A — Spell-Checking:**
- Tool: **CSpell** (lokal, deterministisch)
- Scope: außerhalb von AiNetLinter, als separater CI-Schritt
- Umsetzbarkeit: Hoch

**Teilaspekt B — Ubiquitous Language → siehe neue Idee #A unten**

---

### 3. Lack of Cohesion of Methods (LCOM4)

**Status: ❌ Nicht implementiert**
**Impact: Mittel-Hoch | Implementierungsaufwand: Hoch**

LCOM4 prüft ob Methoden in einer Klasse dieselben Felder/Properties nutzen. Wenn Methode A/B nur Feld X nutzen und Methode C/D nur Feld Y, ist LCOM4 > 1 → die Klasse sollte aufgeteilt werden. Selbst eine 200-Zeilen-Klasse kann zwei völlig unabhängige Dinge tun und damit LLMs überfordern.

**Ehrliche Einschätzung:** Der Algorithmus erfordert Graphanalyse über den Roslyn-AST (Datenfluss von Feldzugriffen zu Methoden). Hoher Implementierungsaufwand. Praktische Alternative: `MaxPublicMembersPerType` (neue Idee #B) liefert ähnliche Garantien mit deutlich weniger Aufwand.

---

### 4. Deterministische "Purity"-Erzwingung

**Status: ⚠️ Teilweise — `DetectAndBanPhantomDependencies` deaktiviert, externer Ansatz ungenutzt**
**Impact: Hoch**

LLMs sind exzellent bei puren Funktionen (Input → Output). Versteckter State und Nebeneffekte führen zu Halluzinationen.

**Externer Ansatz (empfohlen, unabhängig von AiNetLinter):**
- `Microsoft.CodeAnalysis.BannedApiAnalyzers` (offizielles MS-Paket)
- Konfigurierbar über `BannedSymbols.txt`: verbiete `DateTime.Now`, `Guid.NewGuid()`, `Environment.GetEnvironmentVariable()`, `Task.Run` in Domain-Schichten
- Deterministisch, lokal, gut gepflegt
- Zwingt den Code, Zeit und Zufall als injizierte Parameter zu empfangen

Diese Regel intern nachzubauen lohnt sich nicht — das MS-Paket macht es besser.

---

### 5. Layout- & Syntax-Normalisierung

**Status: ⚠️ `dotnet format` Standard, Member-Reihenfolge nicht erzwungen**
**Impact: Mittel**

Konsistente Dateistruktur reduziert die Entropie im Prompt massiv. LLMs sagen das nächste Token voraus — je vorhersagbarer der Aufbau, desto weniger Syntaxfehler.

- `dotnet format` / StyleCop.Analyzers: als CI-Pflicht empfohlen
- Strikte Member-Reihenfolge: 1. Konstanten, 2. Felder, 3. Konstruktoren, 4. Public Properties, 5. Public Methods, 6. Private Methods

---

## Neue Ideen (ergänzt 2026-06-17)

Die folgenden Ideen sind noch **nicht** in AiNetLinter implementiert und auch nicht in den
bestehenden Forschungsdokumenten enthalten. Sie adressieren spezifische LLM-Failure-Patterns
die in aktueller Forschung (DAPLab Columbia, SWE-Bench, Karpathy-Analysen) dokumentiert sind.

---

### A. Ubiquitous Language — Vokabular-Konsistenz-Erzwingung

**Impact: 🔴 Hoch**
**Roslyn-Umsetzung: Mittel (Identifier-Scan)**

`EnforceSemanticNaming` verbietet generische Begriffe (`data`, `temp`, `obj`). Das hier ist
etwas fundamental anderes: **Synonyme für denselben Domain-Begriff** zerstören die semantische
Kohärenz eines LLM-Prompts noch stärker.

**Das Problem:**

Wenn eine Codebasis sowohl `Customer` als auch `Client` und `Account` für dasselbe Domain-Konzept
verwendet, baut das LLM drei separate Konzept-Repräsentationen auf. Prompt: "Erstelle eine Methode
die Kundendaten liest" → das LLM kann mit `Customer`, `Client`, oder `Account` antworten — und
*alle drei* sind potentiell "richtig" laut Training. Das führt zu inkonsistentem generierten Code.

**Technisches LLM-Argument:**

Sprachmodelle sind in ihrer Trainingsdaten hochsensibel auf semantische Co-Occurrence. `Customer`
und `Client` teilen ähnliche Token-Kontexte aus dem Training, aber innerhalb einer spezifischen
Codebasis können sie sehr unterschiedliche Konzepte sein. Wenn ein Agent `Customer.Update()` und
`ClientRepository.Save()` in derselben Codebasis sieht, entstehen ambige Embeddings.

**Umsetzung:**

Neue Konfigurierbare Struktur in `rules.json`:
```json
"UbiquitousLanguage": {
  "BannedSynonyms": {
    "Customer": ["Client", "Account", "User"],
    "Order": ["Purchase", "Transaction", "Cart"],
    "Product": ["Item", "Good", "Article"]
  }
}
```

Roslyn-Scan über: Typ-Namen, Property-Namen, Methoden-Namen.
Flag: "Verwende stets `Customer` statt `Client`."

**Unterschied zu CSpell:** CSpell prüft Rechtschreibung. Dies prüft Domain-Konsistenz.
**Unterschied zu EnforceSemanticNaming:** Das verbietet generische Namen. Dies verbietet Synonyme für project-spezifische Konzepte.

---

### B. MaxPublicMembersPerType — API-Oberflächen-Limiter

**Impact: 🔴 Hoch**
**Roslyn-Umsetzung: Einfach**

`MaxLineCount` limitiert die Dateigröße. `MaxMethodLineCount` limitiert die Methodenlänge.
Aber eine 600-Zeilen-Klasse mit 5 fokussierten Methoden ist fundamental anders als eine
600-Zeilen-Klasse mit 25 kleinen public Methoden.

**Das Problem:**

Wenn ein LLM eine Klasse mit 25 public Methoden und 8 public Properties sieht (33 öffentliche
Members), muss sein Attention-Mechanismus alle 33 Members gleichzeitig berücksichtigen wenn es
eine Änderung durchführt. Die cognitive load explodiert quadratisch mit der Anzahl der Members.

SWE-Bench-Daten (2026): Agenten "reimplementieren etablierte Hilfsmethoden neu" wenn die
API-Oberfläche zu breit ist. Sie sehen 20+ Methoden und *verpassen* die relevante.

**Umsetzung:**

```json
"MaxPublicMembersPerType": 12
```

Roslyn: zähle öffentliche Methoden + öffentliche Properties per Typ-Deklaration.
Exklusion: Properties mit nur einem Getter (reine Value-Objects können mehr haben).

**Ergänzend zu LCOM4:** LCOM4 misst interne Kohäsion (teilen Methoden dieselben Felder?).
`MaxPublicMembersPerType` limitiert die extern-sichtbare "Breite" unabhängig davon.

---

### C. Bool-Parameter-Limit (MaxBoolParameterCount)

**Impact: 🔴 Hoch**
**Roslyn-Umsetzung: Sehr einfach**

Bool-Parameter sind die LLM-feindlichsten Parameter-Typen. Das Problem ist nicht die
Methodendefinition, sondern der **Aufruf-Site**:

```csharp
// Definition (lesbar):
void SendEmail(bool includeAttachments, bool isHtml, bool requireReadReceipt)

// Aufruf (völlig opak für LLM und Mensch):
SendEmail(true, false, true)
```

**LLM-Forschungs-Backing:**

DAPLab Columbia: "Data Management Errors" (Failure Category 4) — Argument-Reihenfolgen-Halluzination
tritt massiv auf wenn primitive Typen ohne semantischen Unterschied in der Signatur auftauchen.
Bool-Parameter sind der Extremfall: `true` und `false` sind semantisch völlig leer.

Andrej Karpathy (2026): Bool-Flags in Methoden sind ein Signal für "fehlende Abstraktion" — das
LLM wählt beim Aufruf zufällig zwischen `true/false` wenn es den Kontext nicht vollständig erfasst.

**Lösung durch den Linter:**

```json
"MaxBoolParameterCount": 1
```

Erzwingt die Verwendung von Enums oder Parameter-Records statt mehrerer Bool-Flags:
```csharp
// Stattdessen:
void SendEmail(EmailOptions options)
record EmailOptions(bool IncludeAttachments, bool IsHtml, bool RequireReadReceipt);
```

Das `record` macht den Call-Site-Code semantisch explizit:
`SendEmail(new EmailOptions(IncludeAttachments: true, IsHtml: false, RequireReadReceipt: true))`

**Umsetzung:** Roslyn: zähle `bool`-typed Parameter (und `bool?`) pro Methoden-Signatur.
Exklusion: private Methoden, Test-Files (optional).

---

### D. Nested Type Prohibition (BanNestedTypes)

**Impact: 🟡 Mittel-Hoch**
**Roslyn-Umsetzung: Einfach**

Verschachtelte Klassen, Enums, und Records sind für Agenten-Harnesses unsichtbar.

**Das konkrete Problem:**

Ein Agent führt `grep -rn "PaymentStatus"` aus oder nutzt das File-Search-Tool. Wenn
`PaymentStatus` ein nested enum innerhalb von `PaymentProcessor.cs` ist, findet der Agent
die *Datei*, aber muss sie komplett lesen um den Typen zu finden. Das:
1. lädt unnötig großen Kontext
2. versteckt Abhängigkeiten (ist `PaymentStatus` in `PaymentProcessor.cs` oder woanders?)
3. durchbricht die 1-Typ-pro-Datei-Intuition die Agenten bevorzugen

**Warum das in Agentic Workflows schlimmer ist als für Menschen:**

Menschen kennen die Codebasis und erinnern sich an nested types. LLM-Agenten müssen jedes Mal
neu navigieren und verlassen sich auf file-system-basierte Discovery-Tools.

**Umsetzung:**

```json
"BanNestedTypes": true,
"NestedTypeExemptKinds": ["record"]  // Parameter-Object-Records erlaubt
```

Roslyn: Prüfe ob `TypeDeclarationSyntax` innerhalb einer anderen `TypeDeclarationSyntax` sitzt.
Ausnahme: private nested records die als lokale Parameter-Objekte dienen (kurz, no own methods).

---

### E. MaxGenericTypeParameters

**Impact: 🟡 Mittel**
**Roslyn-Umsetzung: Sehr einfach**

Generische Typ-Parameter sind "unbenannte Typ-Argumente auf Klassen-Ebene". Mit jedem weiteren
generischen Parameter multipliziert sich die Reasoning-Komplexität für das LLM.

```csharp
// 2 Type-Parameter: klar, BCL-Standard (Dictionary<TKey, TValue>)
Repository<TEntity, TKey>

// 4 Type-Parameter: das LLM muss 4 Typ-Variablen gleichzeitig tracken
Repository<TEntity, TKey, TContext, TFilter>
```

**Forschungs-Backing:**

Das .NET BCL verwendet standardmäßig max. 2 generische Typ-Parameter für seine Kerntypen
(`Func<>` ist eine Ausnahme für spezifische FP-Patterns). Models trained on idiomatic C# have
strongly weighted priors on 1-2 generic parameters.

**Umsetzung:**

```json
"MaxGenericTypeParameters": 2
```

Roslyn: Prüfe `TypeParameterListSyntax.Parameters.Count` bei Klassen und Methoden.

---

## Gesamteinschätzung: Was hat wirklich Impact?

| Idee | Impact | Aufwand | Priorität |
| :--- | :---: | :---: | :---: |
| A. Ubiquitous Language / Synonym-Verbot | 🔴 Hoch | Mittel | 1 |
| C. Bool-Parameter-Limit | 🔴 Hoch | Niedrig | 1 |
| B. MaxPublicMembersPerType | 🔴 Hoch | Niedrig | 2 |
| D. Nested Type Prohibition | 🟡 Mittel-Hoch | Niedrig | 2 |
| 3. LCOM4 | 🟡 Mittel-Hoch | Hoch | 3 |
| E. MaxGenericTypeParameters | 🟡 Mittel | Niedrig | 3 |
| 2. CSpell (extern) | 🟡 Mittel | Niedrig (extern) | 2 |
| 4. BannedApiAnalyzers (extern) | 🔴 Hoch | Niedrig (extern) | 1 |
| 5. dotnet format / Member-Reihenfolge | 🟢 Mittel | Niedrig | 3 |

---

## Fazit

Die ursprüngliche Einschätzung "ihr habt 80-90% erreicht" gilt weiterhin für die Roslyn-Seite.
Die größten noch offenen Gewinne sind:

1. **Bool-Parameter-Limit** (MaxBoolParameterCount ≤ 1): Sofort umsetzbar, hoher Impact.
   Der Call-Site-Code wird für LLMs drastisch lesbarer.

2. **Ubiquitous Language** (BannedSynonyms in rules.json): Hoher Domain-spezifischer Impact,
   erfordert projekt-individuell gefüllte Konfiguration. Der Linter liefert den Mechanismus,
   das Projekt liefert den Glossar.

3. **BannedApiAnalyzers** (extern, nicht in AiNetLinter): Das MS-Paket gibt es bereits.
   `DateTime.Now`, `Guid.NewGuid()`, `Environment.GetEnvironmentVariable()` in Domain-Schichten
   zu verbieten erzwingt deterministischen, testbaren Code — ideal für LLM-generierte Logik.

4. **MaxPublicMembersPerType**: Ergänzt MaxLineCount sinnvoll für die "Breite" eines Typs.

**Was man nicht tun sollte:**
- KI-basierte Linter (LLM-Reviewer im CI) einbauen — langsam, teuer, nicht deterministisch
- LCOM4 jetzt implementieren — hoher Aufwand, andere Regeln decken ähnliches ab
- LanguageExt oder Heavy FP-Libraries — flutet den Context mit unbekannten Monaden-Mustern

> Weiterführende Tiefenanalyse mit Quellenangaben:
> `Research/DeepResearch/20260613/AiNetLinter_ LLM-Code-Optimierung und Agenten-Workflows.md`
