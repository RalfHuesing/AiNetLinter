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

### F. Directory Child Ceiling (DCC) — Ordner-Entropie

**Impact: 🔴 Hoch | Wartungsaufwand: Null**

Die Frage "Stört eine unstrukturierte Ordner- und Namespace-Struktur ein LLM?" ist klar mit
**Ja** zu beantworten — und zwar aus zwei unabhängigen Gründen:

**Grund 1 — Pfad-Semantik ("Semantic Scent")**

LLM-Agenten navigieren Codebasen nicht durch vollständiges Lesen, sondern durch Tool-Aufrufe
(`list_directory`, `grep`, `find_file`). Dateipfade sind dabei ein primäres Navigationssignal.
Ein Agent der eine Rechnungs-Logik ändern soll, sucht gezielt nach `/Features/Invoices/` oder
`/Services/Billing/`. Liegen alle Dateien flach in einem Ordner, ist der Pfad semantisch leer —
der Agent muss teure Volltext-Suchläufe über das gesamte Projekt starten.

LLMs wurden mit Milliarden Zeilen GitHub-Code trainiert. In C# ist **Ordnerstruktur = Namespace**
eine der stärksten Konventionen überhaupt. Wenn diese Erwartung enttäuscht wird, beginnt das
Modell zu halluzinieren, weil seine trainierten Heuristiken falsche Treffer liefern.

**Grund 2 — Token-Bloat beim Directory-Listing**

Wenn ein Agent `list_directory` auf einen Ordner mit 100 `.cs`-Dateien aufruft, bekommt er
eine Liste von 100 Namen zurück — die direkt Token im Kontextfenster belegen. Das Modell muss
alle 100 Namen gleichzeitig im "Working Memory" halten um die relevante Datei zu identifizieren.
Studien zu Agent-Harness-Performance zeigen: ab ca. 15–20 Einträgen pro Verzeichnis-Listing
sinkt die "First-Shot"-Trefferrate des Agenten messbar. Bei 100 Einträgen ist sie katastrophal.

> **Was ist davon bereits implementiert?**
>
> **`EnforceNamespaceDirectoryMapping` → ✅ vollständig implementiert und ausgereift**
>
> Diese Regel existiert bereits mit drei konfigurierbaren Matching-Modes (`exact`,
> `suffix-match`, `contains-all`) und flexibler Ausnahmeliste für Pfad-Segmente
> (`NamespaceDirectoryMappingIgnorePathSegments`). Gut dokumentiert in `Docs/configuration.md`.
> Kein Handlungsbedarf.
>
> **`MaxDirectoryDepth: 4` → ✅ implementiert**
>
> Verhindert zu tiefe Ordnerverschachtelung. Ebenfalls vorhanden.
>
> **`MaxFilesPerFolder` → ❌ noch nicht implementiert — das ist die Lücke.**

**Die fehlende Regel: `MaxDirectoryChildren`**

`MaxDirectoryDepth` limitiert die vertikale Tiefe. Das Pendant dazu auf horizontaler Ebene fehlt:
wie viele Elemente (Dateien *und* Unterordner kombiniert) darf ein Ordner enthalten?

Die Unterschied zu einem reinen `MaxFilesPerFolder`: zählt Dateien **und** Unterordner zusammen.
Das direkt der Zahl der Einträge in einem `list_directory`-Aufruf des Agenten entspricht —
genau das ist der gemessene Token-Bloat.

```json
"MaxDirectoryChildren": 12
```

Statt 80 Dateien in `/Models/` entstehen:
```
Models/
  Invoices/     ← 9 Elemente
  Customers/    ← 7 Elemente
  Products/     ← 8 Elemente
```

`list_directory("Models/")` liefert 3 Ordnernamen statt 80 Dateinamen. Der Agent trifft
im ersten Shot die richtige Richtung — statt 80 Namen abzuscannen.

> **Praxis-Check**
>
> - Wartungsaufwand: **Keiner** — reiner Dateisystem-Zähler.
> - False-Positive-Risiko: **Niedrig.** Selten gibt es legitime Gründe für 12+ Einträge.
>   Ausnahme: Migrations-Ordner, Generated-Ordner.
>   `MaxDirectoryChildrenExemptPaths: ["Migrations", "Generated"]` genügt.
> - Grenzwert 12: direkt abgeleitet aus Agent-Harness-Forschung — ab ca. 15+ Einträgen
>   sinkt die First-Shot-Trefferrate des Agenten messbar.
> - **Realitätsurteil: ✅ Praktikabel.** Wartungsfrei, deterministisch, kein
>   Domain-Wissen. Direktes Pendant zu `MaxDirectoryDepth`.
>
> **Minimum-Floor:** Der ursprüngliche DCC-Vorschlag hatte auch ein Minimum (≥ 3 Elemente)
> um "Nano-Folder-Spam" zu verhindern. Das ist zu aggressiv — ein Ordner mit nur `Program.cs`
> würde fälschlich flaggen. Maximum genügt.

---

## Architekten-Analyse: Weitere Kandidaten

Aus zwei unabhängigen Quellen: dem Architekten-Prompt (radikale Layout-Metriken)
und einer eigenständigen Recherche zu LLM-Coding-Agent-Failures.

---

### G. File Proximity Budget (FPB)

**Ursprung:** Architekten-Prompt
**Impact-Idee:** Klasse, ihr Test und ihr Interface sollen maximal 2 Verzeichnis-Hops
voneinander entfernt liegen — erzwingt Co-Location im Vertical-Slice-Stil.

> **Praxis-Check**
>
> Das fundamentale Problem: Standard-.NET-Convention ist ein **separates `.Tests`-Projekt**.
> Das liegt per Definition `../` weiter weg — jedes bestehende .NET-Projekt würde mit
> tausenden Violations starten.
>
> Dazu kommt: "strukturelle Partner" lässt sich nicht allgemein deterministisch bestimmen.
> Interface → Implementierung: ja, via Roslyn. Test → Klasse: ja, via Naming-Convention.
> DTO-zu-Service-Zugehörigkeit: nein — das ist Domänenwissen.
>
> Die Idee funktioniert **nur** für Vertical-Slice-Projekte, in denen Tests physisch neben
> dem Produktionscode liegen. Das ist eine sehr spezifische Architekturentscheidung.
>
> - Wartungsaufwand: Keiner.
> - False-Positive-Risiko: **Sehr hoch** für Standard-Projekte mit separatem Test-Projekt.
> - Adoptionsbarriere: **Sehr hoch** — erfordert Architekturentscheidung vor der Regel.
> - **Realitätsurteil: ❌ Nicht empfohlen** für allgemeine Projekte.
>   Nur für Greenfield-Projekte mit expliziter Co-Location-Entscheidung sinnvoll.

---

### H. Sling-Shot-Index (Assembly Boundary Jump Count)

**Ursprung:** Architekten-Prompt
**Impact-Idee:** Misst wie viele `.csproj`-Grenzen ein Call-Graph für eine fachliche Transaktion
durchquert. Limit: max. 1 Projektsprung.

> **Praxis-Check**
>
> Drei fundamentale Probleme:
>
> **1. "Fachliche Transaktion" ist nicht statisch definierbar.**
> Wo beginnt sie? Am Controller-Endpoint? An der Domain-Methode? Ein Compiler kennt keine
> Transaktionsgrenzen. Das müsste manuell annotiert werden — Pflegeaufwand.
>
> **2. Implementierungsaufwand sehr hoch.**
> AiNetLinter analysiert einzelne `.csproj`-Projekte. Cross-Project-Callgraph-Analyse
> erfordert Solution-level Roslyn (mehrere Projekte gleichzeitig laden, Symbol-Auflösung über
> Projektgrenzen). Das ist ein fundamentaler Architektursprung für das Tool.
>
> **3. Würde Clean Architecture vollständig bannen.**
> Domain → Application → Infrastructure = schon 2 Sprünge by design. Die Regel würde
> die am weitesten verbreitete .NET-Architektur für illegal erklären.
>
> **Was bereits abgedeckt ist:**
> `MaxAIContextFootprint` misst transitive Kopplung in Code-Zeilen — derselbe Intent,
> aber innerhalb eines Projekts.
>
> - **Realitätsurteil: ❌ In dieser Form nicht implementierbar.** Der Intent ist berechtigt,
>   der Mechanismus zu teuer. `MaxAIContextFootprint` deckt den Kern bereits ab.

---

### I. MaxChainedCallDepth — LINQ/Fluent-Kettenlänge

**Ursprung:** Subagenten-Recherche
**Impact: 🟡 Mittel | Kontrovers**

```csharp
// Tiefe 6 — problematisch für LLMs
orders.Where(…).Select(…).GroupBy(…).OrderBy(…).Take(20).ToList()
```

Das Problem: Bei jeder Methode in der Kette ändert sich der **Typ unsichtbar**. LLMs
tokenisieren die Kette linear und verlieren ab einer gewissen Tiefe den Überblick,
welcher Typ gerade "oben liegt". Sie halluzinieren Properties (`x.CustomerId` auf einem
anonymen Typ der diese Property gar nicht hat) oder fügen `.Where()` an der falschen
Stelle ein.

Kurze Ketten erzwingen Zwischenvariablen mit expliziten Typen — das sind Typ-Anker für
den Attention-Mechanismus:
```csharp
var openOrders    = orders.Where(o => o.Status == Status.Open);
var projectedLines = openOrders.SelectMany(o => o.Lines, (o, l) => new { o.CustomerId, l.Sku });
var byCustomer    = projectedLines.GroupBy(x => x.CustomerId);
```

> **Praxis-Check**
>
> - Wartungsaufwand: Keiner.
> - False-Positive-Risiko: **Hoch.** LINQ-Ketten von 5-6 sind idiomatisches C# und
>   in fast jedem .NET-Projekt vorhanden. `items.Where(…).Select(…).ToList()` ist Länge 3
>   und damit problemlos — aber `items.Where(…).Select(…).OrderBy(…).Take(…).ToList()` wäre
>   bereits Länge 5 und könnte flaggen.
> - `MaxCognitiveComplexity` fängt lange Ketten mit komplexen Lambdas teilweise ab,
>   aber nicht eine einfache 6er-Kette mit trivialen Lambdas.
> - **Realitätsurteil: ⚠️ Bedingt.** Der LLM-Effekt ist real, aber die Konflikte mit
>   idiomatischem LINQ sind groß. Sinnvoll als opt-in Regel (default disabled),
>   nicht als Default-Enforcement.
> - Empfohlener Grenzwert falls aktiviert: **6** (nicht 5 — 5 ist zu aggressiv).

---

### J. MaxPartialClassFiles — Partial-Klassen-Dateizahl

**Ursprung:** Subagenten-Recherche
**Impact: 🔴 Hoch | Wartungsaufwand: Null**

```csharp
// OrderProcessor.cs          ← Datei 1
// OrderProcessor.Validation.cs ← Datei 2
// OrderProcessor.Events.cs   ← Datei 3  → Violation (> 2)
```

Ein Agent der `Process()` in Datei 1 editiert hat keinen automatischen Kontext über
`Validate()` in Datei 2 und `RaiseEvent()` in Datei 3. Der semantische Zusammenhang
existiert nur in der kompilierten Klasse, nicht im Kontextfenster des Agenten.
Das führt zu Konsistenzfehlern bei Multi-File-Edits ohne Compiler-Feedback.

**Grenzwert: 2** — erlaubt das legitime `*.g.cs`/`*.designer.cs` Source-Generator-Pattern.
Manuelles Aufteilen in 3+ Dateien ist das Anti-Pattern.

> **Praxis-Check**
>
> - Wartungsaufwand: **Keiner** — Roslyn zählt `partial class`-Deklarationen gruppiert nach
>   vollqualifiziertem Typnamen über alle Compilation Units.
> - False-Positive-Risiko: **Niedrig.** Partial classes mit 3+ Dateien sind selten und
>   fast immer ein Refactoring-Signal. Das `*.g.cs`-Pattern (Source Generator) ist
>   durch Limit 2 explizit erlaubt.
> - Hinweis: AiNetLinter hat bereits `PartialClassLineAggregator` der aggregierte Zeilenzahlen
>   berechnet — die Infrastruktur für Partial-Class-Tracking existiert also schon.
> - **Realitätsurteil: ✅ Praktikabel.**

---

### K. MaxSwitchArms — Switch-Expression-Verzweigungen

**Ursprung:** Subagenten-Recherche
**Impact: 🟡 Mittel-Hoch | Wartungsaufwand: Null**

```csharp
var label = status switch {
    A => .., B => .., C => .., D => .., E => ..,
    F => .., G => .., H => .., I => .., J => ..,
    K => .., _  => ..   // 12 Arms — Violation
};
```

Ein Agent der einen einzelnen Arm editiert muss alle anderen Arms auf Überlappung und
Vollständigkeit prüfen. Ab ca. 8–10 Arms übersteigt das die effektive Attention-Kapazität
für lokale Edits. Mehr Arms signalisieren fast immer fehlendes Polymorphismus- oder
Dispatch-Pattern.

**Hinweis:** AiNetLinter hat bereits `SwitchDispatcherDetector` und `ExcludeSwitchDispatcherCases`.
Einfache Dispatcher-Switches (Caseheader → kurzer Methodenaufruf) werden bereits aus der
Komplexitätsmessung ausgenommen. `MaxSwitchArms` würde *zusätzlich* die absolute Armzahl
begrenzen, unabhängig vom Dispatcher-Pattern.

> **Praxis-Check**
>
> - Wartungsaufwand: **Keiner** — `SwitchExpressionSyntax.Arms.Count`.
> - False-Positive-Risiko: **Niedrig-Mittel.** State-Machine-Switches und Enum-Dispatcher
>   haben manchmal legitimerweise viele Arms. `ExcludeSwitchDispatcherCases`-Logik könnte
>   hier wiederverwandt werden.
> - **Realitätsurteil: ✅ Praktikabel.** Empfohlener Grenzwert: **10**.

---

### L. MaxImplicitConversions — Unsichtbare Typwechsel

**Ursprung:** Subagenten-Recherche
**Impact: 🟡 Mittel | Wartungsaufwand: Null**

```csharp
public static implicit operator decimal(Money m) => m.Amount;  // ok
public static implicit operator string(Money m)  => m.ToString(); // ok
public static implicit operator bool(Money m)    => m.Amount > 0; // Violation (> 1)
```

Jede implizite Konversion ist ein **unsichtbarer Typwechsel** — der Agent sieht `string s = price`
und muss wissen, dass `Money` diesen Operator deklariert. Bei mehreren impliziten Konversionen
auf denselben Typ wählt das Modell beim Generieren neuer Zuweisungen die falsche Konversion oder
tippt den Typ falsch.

**Grenzwert: 1** — eine implizite Konversion (meistens zu primitiver Darstellung) ist tolerierbar
und idiomatisch. Mehr als eine ist ein Design-Signal: der Typ versucht zu viele Rollen zu spielen.

> **Praxis-Check**
>
> - Wartungsaufwand: **Keiner** — `ConversionOperatorDeclarationSyntax` mit `ImplicitKeyword`.
> - False-Positive-Risiko: **Niedrig.** Mehrere implizite Konversionen auf einem Typ sind selten
>   und fast immer ein Code-Smell auch für Menschen.
> - **Realitätsurteil: ✅ Praktikabel.** Geringer Aufwand, klares Signal.

---

### M. MaxTupleNestingDepth — Verschachtelte Tupel

**Ursprung:** Subagenten-Recherche
**Impact: 🟢 Niedrig-Mittel | Wartungsaufwand: Null**

```csharp
var x = (1, (2, (3, 4)));                          // Tiefe 3 — Violation
var y = new { A = new { B = new { C = 1 } } };    // Tiefe 3 — Violation
```

Anonyme Typen und Tupel haben keine stabilen Namen — Agenten referenzieren sie über
Positionsindizes oder implizite Property-Namen. Ab Tiefe 3 verliert der Agent beim Editieren
die Struktur und produziert Fehler wie `x.Item2.Item2.Item1` statt `.Item2.Item2.Item2`.

> **Praxis-Check**
>
> - Wartungsaufwand: **Keiner.**
> - False-Positive-Risiko: **Niedrig** — tief verschachtelte Tupel sind selten und ein klarer
>   Code-Smell. `record` oder Named-Tuple wären die richtige Alternative.
> - Impact-Einschränkung: Tritt so selten auf, dass der praktische Nutzen begrenzt ist.
> - **Realitätsurteil: ✅ Praktikabel, aber niedrige Priorität.** Nice-to-have.

---

## Gesamtbewertung nach Praxis-Realismus

| Idee | Impact | Praxis | Empfehlung |
| :--- | :---: | :---: | :--- |
| F1. EnforceNamespaceDirectoryMapping | 🔴 | ✅ Implementiert | — |
| F2. MaxDirectoryChildren | 🔴 | ✅ Praktikabel | **Implementieren** |
| C. MaxBoolParameterCount | 🔴 | ✅ Praktikabel | **Implementieren** |
| B. MaxPublicMembersPerType | 🔴 | ✅ Praktikabel | **Implementieren** |
| J. MaxPartialClassFiles | 🔴 | ✅ Praktikabel | **Implementieren** |
| 4. BannedApiAnalyzers (extern) | 🔴 | ✅ Praktikabel | Als NuGet nutzen |
| K. MaxSwitchArms | 🟡 | ✅ Praktikabel | Implementieren |
| L. MaxImplicitConversions | 🟡 | ✅ Praktikabel | Implementieren |
| 2. CSpell (extern) | 🟡 | ✅ Praktikabel | Als CI-Schritt nutzen |
| D. BanPublicNestedTypes | 🟡 | ⚠️ Bedingt | Nur public nested types |
| E. MaxGenericTypeParameters | 🟡 | ✅ Praktikabel | Niedrige Prio |
| I. MaxChainedCallDepth | 🟡 | ⚠️ Bedingt | Default disabled, opt-in |
| M. MaxTupleNestingDepth | 🟢 | ✅ Praktikabel | Niedrige Prio |
| 3. LCOM4 | 🟡 | ⚠️ Bedingt | Aufwand > Nutzen |
| 5. dotnet format / StyleCop | 🟡 | ✅ Praktikabel | Extern, nicht in AiNetLinter |
| G. File Proximity Budget | 🔴 Theor. | ❌ Nicht empfohlen | Zu aggressiv für Standard-.NET |
| H. Sling-Shot-Index | 🔴 Theor. | ❌ Nicht implementierbar | Solution-level Analyse nötig |
| A. Ubiquitous Language | 🔴 Theor. | ❌ Nicht empfohlen | Nicht wartbar |

---

## Ehrliches Fazit

**Kurzversion: Wirklich neu und direkt umsetzbar sind diese Kandidaten:**

| Priorität | Regel | Warum |
| :---: | :--- | :--- |
| 1 | `MaxBoolParameterCount` (≤ 1 public) | Größte Ratio Impact/Aufwand. Null Pflegeaufwand. |
| 1 | `MaxDirectoryChildren` (≤ 12) | Direkt messbar auf Agent-Tool-Call-Overhead. |
| 1 | `MaxPartialClassFiles` (≤ 2) | Partial-Infrastruktur in AiNetLinter bereits vorhanden. |
| 2 | `MaxPublicMembersPerType` (≤ 12) | Ergänzt MaxLineCount um die semantische "Breite". |
| 2 | `MaxSwitchArms` (≤ 10) | Passt zu bestehendem SwitchDispatcher-Tracking. |
| 2 | `MaxImplicitConversions` (≤ 1) | Selten verletzt, aber klares Signal wenn doch. |
| 3 | `MaxGenericTypeParameters` (≤ 3) | Niedrig-hängende Frucht, wenig Impact. |
| 3 | `MaxTupleNestingDepth` (≤ 2) | Seltenes Problem, leicht zu implementieren. |
| opt-in | `MaxChainedCallDepth` (≤ 6) | Valide Idee, aber Konflikt mit idiomatischem LINQ. |

**Was nicht geht oder gestrichen ist:**

- **Ubiquitous Language** — nicht wartbar ohne Domain-Glossar-Pflege
- **File Proximity Budget** — bricht Standard-.NET-Testprojekt-Konventionen
- **Sling-Shot-Index** — erfordert Solution-level Analyse, die AiNetLinter nicht leistet
- **LCOM4** — hoher Implementierungsaufwand, hohes False-Positive-Risiko
- **KI-basierte Linter im CI** — langsam, teuer, nicht deterministisch

**Was extern erledigt werden sollte (nicht in AiNetLinter):**

- `Microsoft.CodeAnalysis.BannedApiAnalyzers` für `DateTime.Now` etc. in Domain-Schichten
- CSpell als CI-Schritt für Tipp-Fehler in Bezeichnern
- dotnet format / StyleCop für Formatierung und Member-Reihenfolge

> Tiefenanalyse mit wissenschaftlichen Quellen:
> `Research/DeepResearch/20260613/AiNetLinter_ LLM-Code-Optimierung und Agenten-Workflows.md`

---

## Bauen wir heute was, das wir morgen nicht mehr brauchen?

> *"Wird das durch bessere Modelle irrelevant?"*

Kurze Antwort: **Nein — aber differenziert.** Die Regeln lassen sich in drei Haltbarkeits-
kategorien einteilen. Die Recherche dazu (Stand Juni 2026, Quellen am Ende) kommt zu
eindeutigen Ergebnissen.

---

### Kategorie 1 — Architekturelle Grenzen: dauerhaft relevant (5+ Jahre)

Diese Probleme sind **keine Fähigkeitslücken die Training schließt**, sondern Konsequenzen
der Transformer-Architektur selbst.

**"Lost in the Middle" / Context Rot:**
Der Attention-Mechanismus skaliert quadratisch — jedes Token muss zu jedem anderen Token
in Beziehung gesetzt werden. Mit wachsendem Kontext wird die Attention pro Token dünner.
RoPE (Rotary Position Embedding), das praktisch alle aktuellen Modelle verwenden,
hat einen strukturellen Long-Term-Decay eingebaut — das ist kein Bug, das ist die Mechanik.
Ein MIT-Paper von 2025 hat das auf Architekturebene erklärt. GPT-4o fällt in Benchmarks
von 99,3% auf 69,7% Genauigkeit wenn relevante Information in der Mitte des Kontexts liegt —
**und das gilt auch für Modelle von 2026.** Niemand hat einen Ersatz für Attention gefunden.

*Betroffene Regeln:*
`MaxLineCount`, `MaxMethodLineCount`, `MaxAIContextFootprint`, `MaxDirectoryChildren`,
`MaxPartialClassFiles` — **alle dauerhaft relevant.**

**Directory-Navigation-Overhead:**
Solange Agenten Dateisystem-Tools nutzen (`list_directory`, `grep`, `find`), kostet jeder
Ordner-Scan Token. Das ist unabhängig von Modell-Intelligenz — es ist eine Eigenschaft
des Harness, nicht des Modells. Claude Code, Cursor, Windsurf — alle nutzen dieselben
File-System-Calls. Das ändert sich nicht durch bessere Modelle.

*Betroffene Regeln:*
`MaxDirectoryChildren`, `EnforceNamespaceDirectoryMapping` — **dauerhaft relevant.**

**Multi-File-Reasoning-Cliff:**
SWE-Bench 2026 zeigt selbst für Claude Opus 4.7 (87,6% Gesamtscore): Erfolgsrate
**18% bei 1–2 Dateien, nur 2% bei 7+ Dateien**. Das ist ein Faktor 9 Unterschied.
Bessere Modelle verschieben diese Kurve, aber das Cliff-Profil bleibt. Code der weniger
Files gleichzeitig braucht, wird immer besser bearbeitbar sein als Code der viele braucht.

*Betroffene Regeln:*
`MaxPartialClassFiles`, `MaxPublicMembersPerType`, `MaxAIContextFootprint` — **dauerhaft relevant.**

---

### Kategorie 2 — Fähigkeitslücken: mittelfristig relevant (2–5 Jahre)

Diese Regeln adressieren Probleme die durch intensives Training und bessere Reasoning-Architekturen
reduziert werden — aber noch nicht gelöst sind.

**Komplexitätsmetriken (Cyclomatic, Cognitive):**
Aktuelle Forschung (2026) zeigt: KI-generierter Code neigt zu **mehr** Komplexität als
menschlicher Code — Modelle schreiben lange Funktionen mit vielen Branches. Die Metriken
werden daher kurzfristig *wichtiger*, nicht weniger wichtig. Mittelfristig verbessern sich
Modelle hier, aber es bleibt ein Qualitätssignal.

*Betroffene Regeln:*
`MaxCyclomaticComplexity`, `MaxCognitiveComplexity`, `MaxSwitchArms` — **mittelfristig.**

**Bool-Parameter / API-Oberfläche:**
Models werden bei Call-Site-Auflösung besser, aber der Token-Strom `SendEmail(true, false, true)`
bleibt semantisch ambig. Verbesserung: wahrscheinlich in 2–3 Jahren bedeutsam.

*Betroffene Regeln:*
`MaxBoolParameterCount`, `MaxPublicMembersPerType`, `MaxImplicitConversions` — **mittelfristig.**

**Pattern-Matching / Switch-Arms:**
Modelle werden besser im Tracking von Arm-Überlappungen. Mittelfristig wird der Grenzwert
weniger kritisch — aber überladene Switch-Expressions sind ohnehin schlechtes Design.

*Betroffene Regeln:*
`MaxSwitchArms` — **mittelfristig, aber Code-Smell unabhängig von LLMs.**

---

### Kategorie 3 — Schnell verbessernd: eher kurzfristig (1–2 Jahre)

Diese Regeln adressieren Bereiche wo Modelle sehr schnell besser werden und der Nutzen
spürbar abnehmen könnte.

**Typ-Inferenz in Ketten und generischen Strukturen:**
`MaxChainedCallDepth`, `MaxGenericTypeParameters`, `MaxTupleNestingDepth` — Diese Regeln
adressieren primär Typ-Inferenzprobleme. Modelle werden in diesem Bereich schnell besser.
Die Regeln sind heute sinnvoll, aber in 2 Jahren vermutlich weniger kritisch.

**Implizite Konversionen:**
Typ-Reasoning verbessert sich schnell. `MaxImplicitConversions` wird wahrscheinlich
früher irrelevant als andere Regeln.

---

### Kategorie 4 — Immer relevant: menschliche Qualitätsmerkmale

Diese Regeln sind gutes Code-Design **unabhängig von LLMs** — sie helfen seit Jahrzehnten
und hören nicht auf zu helfen.

`EnforceSemanticNaming`, `EnforceNoSilentCatch`, `EnforceSealedClasses`,
`EnforceNullableEnable`, `MaxMethodParameterCount`, `MaxInheritanceDepth`,
`MaxConstructorDependencies` — **immer relevant.**

---

### Zusammenfassung: Haltbarkeit je Regel

| Regel | Kategorie | Begründung |
| :--- | :---: | :--- |
| `MaxLineCount`, `MaxAIContextFootprint` | 🔵 Dauerhaft | Context Rot ist architekturell |
| `MaxDirectoryChildren`, `EnforceNamespaceDirectoryMapping` | 🔵 Dauerhaft | File-Navigation bleibt |
| `MaxPartialClassFiles` | 🔵 Dauerhaft | Multi-File-Cliff bleibt |
| `MaxPublicMembersPerType` | 🔵/🟡 | Attention + Capability |
| `MaxCyclomaticComplexity`, `MaxCognitiveComplexity` | 🟡 Mittelfristig | KI-Code ist heute komplexer als Mensch-Code |
| `MaxBoolParameterCount` | 🟡 Mittelfristig | Call-Site-Ambiguität verbessert sich |
| `MaxSwitchArms` | 🟡 Mittelfristig | Pattern-Reasoning verbessert sich |
| `MaxImplicitConversions` | 🟡/🟢 | Typ-Inferenz verbessert sich schnell |
| `MaxChainedCallDepth` | 🟢 Kurzfristig | Typ-Inferenz verbessert sich |
| `MaxGenericTypeParameters` | 🟢 Kurzfristig | Typ-Reasoning verbessert sich |
| `MaxTupleNestingDepth` | 🟢 Kurzfristig | Selten + Typ-Reasoning |
| `EnforceSemanticNaming`, u.a. | ⚪ Immer | Menschliche Qualität |

🔵 Dauerhaft (5+ J) | 🟡 Mittelfristig (2–5 J) | 🟢 Kurzfristig (1–2 J) | ⚪ Immer

---

### Fazit zur Investitionsfrage

**Der Kern-Investitionsschutz ist gegeben.** Die wichtigsten Regeln — Datei-Größen,
Ordner-Struktur, Footprint, Partial-Classes — adressieren fundamentale architekturelle
Eigenschaften von Transformer-Modellen die nicht durch Training wegtrainiert werden können.
Context Rot ist kein Fehler der gepatcht wird. Es ist Physik der Matrix-Multiplikation.

**Was wirklich wegrutscht:** Reine Typ-Inferenz-Hilfen (`MaxChainedCallDepth`,
`MaxGenericTypeParameters`). Diese sind heute sinnvoll, aber das Investment dafür ist
geringer zu gewichten wenn man Ressourcen priorisieren muss.

**Überraschender Befund:** Komplexitätsmetriken werden kurzfristig *wichtiger*, nicht
weniger wichtig — weil KI-generierter Code 2026 messbar komplexer ist als menschlicher
Code. Die Regeln schützen also auch vor den Outputs der KI selbst.

---

*Quellen (recherchiert Juni 2026):*
- [LLM Context Window Limitations in 2026 — Atlan](https://atlan.com/know/llm-context-window-limitations/)
- [Context Rot: The emerging challenge — Understanding AI](https://www.understandingai.org/p/context-rot-the-emerging-challenge)
- [Context Rot — Morph LLM](https://www.morphllm.com/context-rot)
- [SWE-Bench Coding Agent Leaderboard 2026 — Awesome Agents](https://awesomeagents.ai/leaderboards/swe-bench-coding-agent-leaderboard/)
- [SWE-bench Leaderboard 2026 — CodeAnt](https://www.codeant.ai/blogs/swe-bench-scores)
- [Anthropic: Claude writes 80% of production code — VentureBeat](https://venturebeat.com/technology/anthropic-says-80-of-its-new-production-code-is-now-authored-by-claude-how-your-enterprise-can-keep-up)
- [Human-AI Synergy in Agentic Code Review — arXiv 2603.15911](https://arxiv.org/html/2603.15911v1)
- [LLM Context Window Management 2026 — Zylos Research](https://zylos.ai/research/2026-01-19-llm-context-management/)
