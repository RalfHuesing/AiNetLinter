# AI-Readability-Erweiterungen für AiNetLinter

> **Ausgangsfrage:** Welche weiteren Dinge könnten wir implementieren,
> um für LLMs verständlichen Code zu erzwingen?
> Constraint: lokal, deterministisch, Roslyn-basiert.

---

## Was haben wir bereits?

| Implementiert | Regel / Metrik |
| :--- | :--- |
| ✅ | `MaxAIContextFootprint` (max. 5.000 transitive Zeilen eigener Typen) |
| ✅ | `MaxCyclomaticComplexity`, `MaxCognitiveComplexity` |
| ✅ | `MaxLineCount`, `MaxMethodLineCount`, `MaxConstructorDependencies` |
| ✅ | `EnforceSealedClasses`, `EnforceNullableEnable`, `EnforceSemanticNaming` |
| ✅ | `EnforceNoSilentCatch`, `EnableTestSentinel` |
| ⚠️ deaktiviert | `DetectAndBanPhantomDependencies`, `EnforceExplicitStateImmutability`, `PreventContextDependentOverloads`, u.a. |

---

## Bewertungsrahmen für jeden Vorschlag

Jede Idee wird entlang von vier Praxis-Dimensionen bewertet:

- **Wartungsaufwand** — Muss jemand etwas pflegen, oder läuft es vollautomatisch?
- **False-Positive-Risiko** — Wie viele unberechtigte Violations entstehen in echten Projekten?
- **Adoptionsbarriere** — Wie leicht ist die erste Nutzung?
- **Realitätsurteil** — ✅ Praktikabel / ⚠️ Bedingt / ❌ Nicht empfohlen

---

## Validierte Ideen (ursprüngliche Analyse)

---

### 1. "Context Fan-Out" Metrik

**Impact: War hoch — ✅ bereits implementiert als `MaxAIContextFootprint`**

Zählt transitive Abhängigkeiten zu eigenen Typen. Vollständig abgedeckt.

---

### 2. Typo- & Wording-Checks (Tokenisierung)

LLMs lesen Tokens, keine Buchstaben. Abkürzungen und Tippfehler zerstören die semantische
Leistungsfähigkeit des Modells.

**Teilaspekt A — CSpell (externes Tool)**

> **Praxis-Check**
>
> - Wartungsaufwand: Niedrig — CSpell bringt eigene englische Wörterbücher mit. Eine
>   projektspezifische `cspell.json` mit Domain-Begriffen muss initial angelegt werden,
>   wächst aber organisch.
> - False-Positive-Risiko: Mittel — Domain-Abkürzungen (`MRP`, `BOM`, `PPS`) müssen
>   als erlaubt markiert werden. Einmalaufwand.
> - Adoptionsbarriere: Niedrig — `npx cspell "**/*.cs"` im CI, fertig.
> - **Realitätsurteil: ✅ Praktikabel** — der einzige externe Vorschlag der wirklich
>   wartungsarm funktioniert.

---

### 3. Lack of Cohesion of Methods (LCOM4)

Prüft ob Methoden in einer Klasse dieselben Felder nutzen. LCOM4 > 1 → Klasse sollte aufgeteilt werden.

> **Praxis-Check**
>
> - Wartungsaufwand: Keiner (rein metrisch).
> - False-Positive-Risiko: **Hoch.** Builder-Pattern, Command-Objekte, Visitor-Pattern —
>   alles würde fälschlich flagged. LCOM4 kennt keine Absicht, nur Feldzugriffe.
>   Selbst gut designte Klassen können hohe LCOM4-Werte haben.
> - Adoptionsbarriere: Hoch — Implementierung erfordert Graphanalyse über den Roslyn-AST
>   (Datenfluss von Feldzugriffen zu Methoden). Deutlich komplexer als alle anderen Regeln.
> - **Realitätsurteil: ⚠️ Bedingt.** Der Implementierungsaufwand ist hoch, das
>   False-Positive-Risiko ist hoch. `MaxPublicMembersPerType` (→ neue Idee B) liefert
>   in der Praxis ähnliche Garantien mit einem Bruchteil des Aufwands.

---

### 4. Deterministische "Purity"-Erzwingung

Verbietet `DateTime.Now`, `Guid.NewGuid()`, `Environment.GetEnvironmentVariable()` in Domain-Schichten.

> **Praxis-Check**
>
> - Wartungsaufwand: Initial eine `BannedSymbols.txt` anlegen, danach wartungsfrei.
> - False-Positive-Risiko: Niedrig — die verbotenen APIs sind klar definiert.
> - Adoptionsbarriere: Niedrig — `Microsoft.CodeAnalysis.BannedApiAnalyzers` (offizielles
>   MS-NuGet-Paket) erledigt das vollständig. In AiNetLinter selbst nachbauen lohnt sich
>   nicht.
> - **Realitätsurteil: ✅ Praktikabel** — aber als externes NuGet, nicht in AiNetLinter.

---

### 5. Layout- & Syntax-Normalisierung

Strikte Member-Reihenfolge (Konstanten → Felder → Ctor → Properties → Public Methods → Private Methods).

> **Praxis-Check**
>
> - Wartungsaufwand: Keiner — einmalig StyleCop.Analyzers konfigurieren.
> - False-Positive-Risiko: Mittel — legacy Codebasen haben Zehntausende Verstöße beim
>   ersten Durchlauf. Nicht als Fehler einführen, nur als Warnung sinnvoll.
> - Adoptionsbarriere: Niedrig für Greenfield, hoch für Brownfield.
> - **Realitätsurteil: ✅ Praktikabel via `dotnet format` und StyleCop** — nicht in
>   AiNetLinter nachzubauen, schon erledigt durch Standard-Tools.

---

## Neue Ideen

---

### A. Ubiquitous Language — Vokabular-Konsistenz

**Theoretischer Impact: Hoch | Praktischer Impact: Fraglich**

Die Idee: Ein konfigurierbares Glossar in `rules.json` verhindert, dass `Customer` und `Client`
für dasselbe Domain-Konzept verwendet werden. LLMs bauen pro Begriff eigene
Konzept-Repräsentationen — Synonyme erzeugen Ambiguität bei der Code-Generierung.

> **Praxis-Check — und hier ist der Haken:**
>
> **Wer pflegt das Glossar?**
>
> Das ist die Kernfrage — und sie hat keine gute Antwort.
>
> - Manuell durch Entwickler → wird in der Praxis nicht gemacht. Kein Team pflegt aktiv
>   ein Synonym-Wörterbuch neben dem Code. Besonders bei tausenden Codezeilen und
>   wachsenden Projekten ist das nicht realistisch.
> - Vom LLM generiert (Audit-Task) → verletzt das Constraint "lokal und deterministisch".
>   Außerdem: Welches LLM entscheidet, ob `Auftrag` und `Bestellung` Synonyme sind oder
>   bewusst unterschiedliche Domain-Konzepte?
>
> **Das Domain-Problem:**
>
> Die jeweilige Domäne ist nicht vorhersagbar. PPS, MES, RM, Fertigmeldung, Rückmeldung,
> ERP, FM — jedes Projekt hat sein eigenes Vokabular. Ein generischer Linter kann keinen
> Brockhaus mitliefern. Und die Frage "Sind `Auftrag` und `FertigungsAuftrag` Synonyme oder
> bewusst unterschiedliche Konzepte?" lässt sich nur domain-spezifisch beantworten.
>
> **Was tatsächlich deterministisch funktionieren würde:**
>
> Eine abgeschwächte Variante: Der Linter meldet, wenn ein *bereits existierender* Typname
> als Substring in einem anderen Typnamen vorkommt, der aber einen anderen Namespace hat.
> `CustomerService` und `ClientService` in derselben Solution → automatisch erkennbar ohne
> Glossar, weil beide Typen im AST stehen.
>
> Das ist aber ein sehr spezieller Fall und würde viele False Positives produzieren
> (z.B. `OrderService` und `SalesOrderService` — bewusst unterschiedlich).
>
> - Wartungsaufwand: **Sehr hoch** (manuelles Glossar für jedes Projekt)
> - False-Positive-Risiko: **Sehr hoch** ohne perfektes Glossar
> - Adoptionsbarriere: **Sehr hoch** — leere Config bringt nichts, befüllte Config ist Arbeit
> - **Realitätsurteil: ❌ Nicht empfohlen** für allgemeine Projekte.
>   Nur sinnvoll für Teams die bereits DDD mit expliziter Bounded-Context-Disziplin betreiben
>   und ihr Ubiquitous-Language-Glossar *sowieso bereits pflegen*. Dann wäre AiNetLinter
>   nur der Enforcement-Mechanismus — aber der Aufwand liegt woanders.
>
> **Fazit zum "Customer"-Beispiel:** Dein Einwand trifft genau den Kern.
> Die Idee ist theoretisch solide, aber in der Praxis nicht wartbar.
> Die Idee wird **gestrichen**.

---

### B. MaxPublicMembersPerType — API-Oberflächen-Limiter

**Impact: 🔴 Hoch | Wartungsaufwand: Null**

Eine Klasse mit 25 public Methoden zwingt ein LLM, alle 25 simultaneously zu berücksichtigen wenn es
eine Änderung generiert. SWE-Bench-Daten zeigen: Agenten "reimplementieren etablierte Hilfsmethoden
neu" wenn die API-Oberfläche zu breit ist — sie sehen 20+ Methoden und *übersehen* die relevante.

`MaxLineCount` limitiert die Dateigröße. `MaxPublicMembersPerType` limitiert die semantische "Breite".
Eine 600-Zeilen-Klasse mit 5 Methoden ist fundamental anders als eine mit 25 kleinen Methoden.

> **Praxis-Check**
>
> - Wartungsaufwand: **Keiner** — rein zählend, keine Domain-Kenntnisse nötig.
> - False-Positive-Risiko: **Mittel.**
>   Extension-Method-Klassen, Mapper-Klassen, Utility-Klassen haben oft viele öffentliche
>   Methoden by design. Benötigt `MaxPublicMembersExemptSuffixes: ["Extensions", "Mapper"]`
>   oder Anpassung über `rules.json`-Overrides.
> - Adoptionsbarriere: **Niedrig** — ein Zahlenwert, kein Konzeptwissen nötig.
> - **Realitätsurteil: ✅ Praktikabel.** Die Ausnahmeliste für Mapper/Extensions ist
>   überschaubar. In Brownfield-Projekten initial als Warnung einführen.

**Implementierungsnotiz:** Roslyn: `PublicMemberDeclaration.Count` (Methoden + Properties) per
Typ-Deklaration. Empfohlenes Limit: 12–15. Konfigurierbar über `rules.json`.

---

### C. Bool-Parameter-Limit (MaxBoolParameterCount)

**Impact: 🔴 Hoch | Wartungsaufwand: Null**

Bool-Parameter sind die LLM-feindlichsten Parameter-Typen. Das Problem liegt nicht in der
Methodendefinition sondern im Aufruf:

```csharp
// Definition (lesbar):
void SendEmail(bool includeAttachments, bool isHtml, bool requireReadReceipt)

// Call site (für LLM völlig opak):
SendEmail(true, false, true)
```

DAPLab Columbia: "Data Management Errors" (Failure Category 4) — Argument-Reihenfolgen-Halluzination
tritt besonders bei gleichartigen primitiven Typen auf. Bei drei Bool-Parametern tippt das Modell
faktisch `true/false` nach Gefühl.

Erzwungene Alternative via Parameter-Record:
```csharp
SendEmail(new EmailOptions(IncludeAttachments: true, IsHtml: false, RequireReadReceipt: true))
```

> **Praxis-Check**
>
> - Wartungsaufwand: **Keiner.**
> - False-Positive-Risiko: **Mittel** bei Limit = 1, **Niedrig** bei Limit = 2.
>   Framework-Callbacks, Event-Handler, Override-Methoden haben manchmal bool-Parameter
>   durch Konvention. Benötigt Ausnahme für private Methoden (bereits abgedeckt durch
>   ähnliche Mechanismen in anderen Regeln).
>   Limit 1 ist streng aber vertretbar für public APIs; Limit 2 ist konservativer Einstieg.
> - Adoptionsbarriere: **Sehr niedrig** — unmittelbar verständliche Regel.
> - **Realitätsurteil: ✅ Praktikabel.** Einer der wenigen Vorschläge wo der
>   Implementierungsaufwand minimal und der LLM-Impact messbar hoch ist.

**Implementierungsnotiz:** Roslyn: zähle `bool`- und `bool?`-typed Parameter per
Methoden-Signatur. Empfohlenes Limit: `MaxBoolParameterCount: 1` für public, Ausnahme für private.

---

### D. Nested Type Prohibition (BanNestedTypes)

**Impact: 🟡 Mittel-Hoch | Wartungsaufwand: Null**

Verschachtelte Klassen sind für Agent-Harness-Navigation unsichtbar. Wenn ein Agent `grep -n "PaymentStatus"`
ausführt und `PaymentStatus` ein nested enum in `PaymentProcessor.cs` ist, findet er die *Datei*,
muss sie aber komplett lesen — Context-Overhead, den kleine Dateien verhindern sollen.

> **Praxis-Check**
>
> - Wartungsaufwand: **Keiner.**
> - False-Positive-Risiko: **Mittel.**
>   Builder-Pattern (`Builder` ist oft nested), State-Machine-Enums die eng an die Klasse
>   gebunden sind, private Hilfsklassen die nur intern existieren. Hier ist eine
>   Ausnahmeliste oder Einschränkung auf public nested types sinnvoller als ein Totalverbot.
>   Pragmatisch: Regel nur für `public` und `internal` nested types, private dürfen bleiben.
> - Adoptionsbarriere: **Niedrig** für Greenfield, **Mittel** für Brownfield
>   (nested enums sind weit verbreitet).
> - **Realitätsurteil: ⚠️ Bedingt.** Als `BanPublicNestedTypes` (nur öffentlich sichtbare)
>   statt Totalverbot deutlich praktikabler.

---

### E. MaxGenericTypeParameters

**Impact: 🟡 Mittel | Wartungsaufwand: Null**

Generische Typen mit vielen Typ-Parametern (`Repository<TEntity, TKey, TContext, TFilter>`)
erhöhen die Reasoning-Komplexität pro Typ-Parameter. Das .NET BCL-Standard: max 2 generische
Typ-Parameter für Kerntypen (`Dictionary<TKey, TValue>`, `KeyValuePair<TKey, TValue>`).

> **Praxis-Check**
>
> - Wartungsaufwand: **Keiner.**
> - False-Positive-Risiko: **Niedrig** bei Limit = 3, **Sehr niedrig** bei Limit = 4.
>   Im eigenen Code braucht man selten 3+ generische Parameter. Ausnahmen sind meist
>   Framework-Typen (die man eh nicht schreibt) oder spezifische Result/Either-Typen.
> - Adoptionsbarriere: **Sehr niedrig.**
> - **Realitätsurteil: ✅ Praktikabel** — aber Impact ist geringer als B und C.
>   Nice-to-have, kein Must-have.

---

## Gesamtbewertung nach Praxis-Realismus

| Idee | Theor. Impact | Praxis-Tauglichkeit | Empfehlung |
| :--- | :---: | :---: | :--- |
| C. Bool-Parameter-Limit | 🔴 Hoch | ✅ Praktikabel | **Implementieren** |
| B. MaxPublicMembersPerType | 🔴 Hoch | ✅ Praktikabel | **Implementieren** |
| 4. BannedApiAnalyzers (extern) | 🔴 Hoch | ✅ Praktikabel | **Als NuGet nutzen** |
| 2. CSpell (extern) | 🟡 Mittel | ✅ Praktikabel | **Als CI-Schritt nutzen** |
| D. BanPublicNestedTypes | 🟡 Mittel | ⚠️ Bedingt | Nur public, nicht private |
| E. MaxGenericTypeParameters | 🟡 Mittel | ✅ Praktikabel | Niedrige Prio |
| 3. LCOM4 | 🟡 Mittel | ⚠️ Bedingt | Aufwand > Nutzen |
| 5. dotnet format / StyleCop | 🟡 Mittel | ✅ Praktikabel | **Extern, nicht in AiNetLinter** |
| A. Ubiquitous Language | 🔴 Theor. | ❌ Nicht empfohlen | **Gestrichen** |

---

## Ehrliches Fazit

Die ursprüngliche Einschätzung "ihr habt 80–90 % erreicht" gilt weiterhin.

**Was wirklich umsetzbar ist und Impact hat:**

1. **Bool-Parameter-Limit** — sofort implementierbar in AiNetLinter, kein Pflegeaufwand,
   messbarer LLM-Impact. `MaxBoolParameterCount: 1` für public Methoden.

2. **MaxPublicMembersPerType** — rein metrisch, kein Domain-Wissen nötig, adressiert das
   "API surface too wide"-Problem aus SWE-Bench-Daten.

3. **BannedApiAnalyzers (MS NuGet)** — `DateTime.Now`, `Guid.NewGuid()` in Domain-Schichten
   verbieten. Nicht in AiNetLinter nachbauen — das MS-Paket ist fertig und gut.

**Was gestrichen wird:**

- **Ubiquitous Language** — gute Theorie, in der Praxis nicht wartbar. Der Einwand
  "wer pflegt das für ein PPS/MES/ERP-Projekt mit unbekannter Domain?" ist berechtigt
  und hat keine gute Antwort. Der Mechanismus wäre implementierbar, aber er würde leer
  bleiben oder falsch befüllt werden.

**Was man nicht tun sollte:**

- KI-basierte Linter (LLM-Reviewer im CI) — langsam, teuer, nicht deterministisch
- LanguageExt — flutet den Context mit Monaden-Mustern die das Modell nicht aus dem
  eigenen Code-Kontext kennt

> Tiefenanalyse mit wissenschaftlichen Quellen:
> `Research/DeepResearch/20260613/AiNetLinter_ LLM-Code-Optimierung und Agenten-Workflows.md`
