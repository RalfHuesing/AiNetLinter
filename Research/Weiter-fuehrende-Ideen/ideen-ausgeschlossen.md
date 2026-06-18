# Ausgeschlossene Ideen — Nicht umsetzbar oder nicht wartbar

Diese Ideen wurden analysiert und bewusst verworfen. Die Begründungen sind dokumentiert
damit wir dieselbe Diskussion nicht erneut führen müssen.

---

## A. Ubiquitous Language — Vokabular-Konsistenz

**Warum verworfen:** Nicht wartbar ohne externe Pflege-Infrastruktur.

**Die Idee:** Ein konfigurierbares Glossar in `rules.json` verhindert, dass `Customer`
und `Client` für dasselbe Domain-Konzept verwendet werden. LLMs bauen pro Begriff eigene
Konzept-Repräsentationen — Synonyme erzeugen Ambiguität bei der Code-Generierung.

**Das fundamentale Problem:**

Wer pflegt das Glossar? Diese Frage hat keine gute Antwort:

- Manuell durch Entwickler → wird in der Praxis nicht gemacht. Kein Team pflegt aktiv
  ein Synonym-Wörterbuch neben dem Code.
- Vom LLM generiert → verletzt das Constraint "lokal und deterministisch". Außerdem:
  Welches LLM entscheidet, ob `Auftrag` und `Bestellung` Synonyme sind oder bewusst
  unterschiedliche Domain-Konzepte?

Die Domäne ist nicht vorhersagbar: PPS, MES, RM, Fertigmeldung, Rückmeldung — jedes
Projekt hat sein eigenes Vokabular. Ein generischer Linter kann keinen Brockhaus mitliefern.

**Eine abgeschwächte Variante** wäre: der Linter meldet, wenn ein bereits existierender
Typname als Substring in einem anderen Typnamen vorkommt, der aber einen anderen Namespace
hat. `CustomerService` und `ClientService` → automatisch erkennbar. Aber das produziert
sehr viele False Positives (`OrderService` und `SalesOrderService` — bewusst unterschiedlich).

**Fazit:** Nur sinnvoll für Teams die bereits DDD mit expliziter Bounded-Context-Disziplin
betreiben und ihr Ubiquitous-Language-Glossar sowieso bereits pflegen. Dann wäre AiNetLinter
nur der Enforcement-Mechanismus — aber der Aufwand liegt woanders. Für den allgemeinen Fall:
gestrichen.

---

## G. File Proximity Budget (FPB)

**Warum verworfen:** Bricht Standard-.NET-Testprojekt-Konventionen.

**Die Idee:** Klasse, ihr Test und ihr Interface sollen maximal 2 Verzeichnis-Hops
voneinander entfernt liegen — erzwingt Co-Location im Vertical-Slice-Stil.

**Das fundamentale Problem:**

Standard-.NET-Convention ist ein **separates `.Tests`-Projekt**. Das liegt per Definition
`../` weiter weg — jedes bestehende .NET-Projekt würde mit tausenden Violations starten.

Dazu kommt: "strukturelle Partner" lässt sich nicht allgemein deterministisch bestimmen.
Interface → Implementierung: ja, via Roslyn. Test → Klasse: ja, via Naming-Convention.
DTO-zu-Service-Zugehörigkeit: nein — das ist Domänenwissen.

Die Idee funktioniert **nur** für Vertical-Slice-Projekte, in denen Tests physisch neben
dem Produktionscode liegen. Das ist eine sehr spezifische Architekturentscheidung, die
nicht als allgemeine Regel erzwungen werden kann.

**Fazit:** Nur für Greenfield-Projekte mit expliziter Co-Location-Entscheidung sinnvoll.
Für den allgemeinen Fall: nicht implementieren.

---

## H. Sling-Shot-Index (Assembly Boundary Jump Count)

**Warum verworfen:** Erfordert Solution-level Analyse — fundamentaler Architektursprung.

**Die Idee:** Misst wie viele `.csproj`-Grenzen ein Call-Graph für eine fachliche Transaktion
durchquert. Limit: max. 1 Projektsprung.

**Drei fundamentale Probleme:**

**1. "Fachliche Transaktion" ist nicht statisch definierbar.**
Wo beginnt sie? Am Controller-Endpoint? An der Domain-Methode? Ein Compiler kennt keine
Transaktionsgrenzen. Das müsste manuell annotiert werden — hoher Pflegeaufwand.

**2. Implementierungsaufwand sehr hoch.**
AiNetLinter analysiert einzelne `.csproj`-Projekte. Cross-Project-Callgraph-Analyse
erfordert Solution-level Roslyn: mehrere Projekte gleichzeitig laden, Symbol-Auflösung über
Projektgrenzen. Das ist ein fundamentaler Architektursprung für das Tool — eine neue
Kategorie von Komplexität.

**3. Würde Clean Architecture vollständig bannen.**
Domain → Application → Infrastructure = schon 2 Sprünge by design. Die Regel würde
die am weitesten verbreitete .NET-Architektur für illegal erklären.

**Was bereits abgedeckt ist:**
`MaxAIContextFootprint` misst transitive Kopplung in Code-Zeilen — derselbe Intent,
aber innerhalb eines Projekts.

**Fazit:** In dieser Form nicht implementierbar. Der Intent ist berechtigt, der Mechanismus
zu teuer und zu aggressiv.

---

## 3. LCOM4 — Lack of Cohesion of Methods

**Warum verworfen:** Hoher Implementierungsaufwand, sehr hohes False-Positive-Risiko.

**Die Idee:** Prüft ob Methoden in einer Klasse dieselben Felder nutzen. LCOM4 > 1 →
Klasse sollte aufgeteilt werden.

**Die Probleme:**

**False-Positive-Risiko sehr hoch:**
Builder-Pattern, Command-Objekte, Visitor-Pattern — alles würde fälschlich flaggen.
LCOM4 kennt keine Absicht, nur Feldzugriffe. Selbst gut designte Klassen können hohe
LCOM4-Werte haben. Ein `OrderBuilder` hat viele Methoden die jeweils verschiedene Felder
setzen — LCOM4 würde ihn als inkohärent markieren, obwohl er perfektes Design zeigt.

**Implementierungsaufwand hoch:**
Erfordert Graphanalyse über den Roslyn-AST: Datenfluss von Feldzugriffen zu Methoden
konstruieren, dann Graphkomponenten zählen. Deutlich komplexer als alle anderen Regeln.

**Bessere Alternative:**
`MaxPublicMembersPerType` (separate Datei) liefert in der Praxis ähnliche Garantien
mit einem Bruchteil des Aufwands — Klassen mit zu breiter API sind oft auch inkohärent.

**Fazit:** Aufwand > Nutzen. `MaxPublicMembersPerType` ist der pragmatische Ersatz.

---

## B. Purity via BannedApiAnalyzers / IClock

**Warum verworfen:** LLM-Impact nicht belegt; löst Test-Engineering-Problem ohne klaren LLM-Mehrwert.

**Die Idee:** Nicht-deterministische APIs (`DateTime.Now`, `Guid.NewGuid()`) in Domain-Schichten per NuGet `Microsoft.CodeAnalysis.BannedApiAnalyzers` + `IClock`/`IGuidGenerator`-Pattern verbieten. Forschungsdoku argumentierte mit „LLM-feindlichem Code", „Reasoning-Ambiguität" und „Seiteneffekt-Unsichtbarkeit".

**Das fundamentale Problem — die LLM-Begründung hält nicht stand:**

1. **`DateTime.Now` ist allgegenwärtiges C#-Idiom** — in Millionen Trainingsbeispielen der LLMs. Es ist kein versteckter Side-Effect, sondern explizite API-Semantik. Jeder LLM erkennt es und verhält sich korrekt.

2. **LLMs behandeln `Guid.NewGuid()` korrekt als nicht-deterministisch.** Niemand versucht, Guid-Returns zu assertieren — das ist semantisch offensichtlich. Kein LLM-Paper identifiziert Guid-Generierung als Fehlerquelle.

3. **`IClock` fügt Indirektion hinzu, die das LLM erst mental tracen muss.** Ein simpler `DateTime.Now`-Aufruf ist für ein LLM leichter zu verstehen als `IClock.UtcNow` + DI + Clock-Mock. Die Idee macht Code für LLMs also eher schwerer, nicht einfacher.

**Was die Idee tatsächlich löst — und wo der Aufwand wirklich hingehört:**

Echtes Engineering-Argument ist Testbarkeit (zeitabhängigen Code deterministisch testen). Das ist aber **Test-Engineering**, nicht LLM-Readability. Die moderne Lösung dafür ist seit .NET 8 der `TimeProvider` — eigene `IClock`-Interfaces zu erfinden ist 2010er-Stil.

**AiNetLinter-Codebase-Audit ergab:**

- Genau **2 `DateTime.Now`-Aufrufe** in Production (`Program.cs` Header-Timestamp, `PerformanceProfiler.cs` Messungs-Timestamp) — beide rein als Format-String verwendet, keine fachliche Zeitabhängigkeit
- `Guid.NewGuid()` einmal in Production (Performance-Messungs-IDs) und ~40× in Tests (Tempfile-Naming) — Test-Idiom, soll bleiben
- Keine `Thread.Sleep`, `Random`, `Environment.GetEnvironmentVariable`, `Environment.MachineName`

**Was bereits abgedeckt ist:**

AiNetLinter hat keine eigene Purity-Regel und braucht keine. `IClock`/`TimeProvider`/Purity ist eine **Architekturentscheidung** des End-Users für seine Domain-Schicht — nicht etwas, das der Linter global erzwingen sollte. End-User mit echtem Purity-Bedarf installieren `Microsoft.CodeAnalysis.BannedApiAnalyzers` direkt in ihrer Solution und schreiben ihre eigene `BannedSymbols.txt`. Das Forschungsdokument selbst empfahl bereits: „Umsetzung als NuGet-Paket, nicht in AiNetLinter".

**Aufwand-Nutzen:**

- Aufwand: 5–8 Dateien (Refactoring + Tests + Doku + NuGet-Setup + .editorconfig), ~3–4 Stunden
- Nutzen: 0 messbare Verbesserung (kein LLM-Problem gelöst, kein echter Code-Smell beseitigt)

**Fazit:** Echtes Engineering-Argument, falsche Zielgruppe adressiert. Die Idee ist als **Best-Practice-Hinweis für User** in deren eigenen Projekten sinnvoll — nicht als AiNetLinter-Feature. Wer Purity in seiner Domain will: NuGet-Paket + TimeProvider. Nicht unser Problem.

---

## I. CSpell — Tippfehler in Bezeichnern (externer CI-Schritt)

**Warum verworfen:** Bereits vom Vorschlag selbst als externer CI-Schritt klassifiziert; Nachbau in AiNetLinter würde das Architektur-Constraint „schlankes, monolithisches Roslyn-CLI" durch Duplikation eines reifen Ökosystem-Tools verletzen.

**Die Idee:** CSpell prüft Bezeichner (`Costomr` statt `Customer`, `Valdi` statt `Valid`) gegen ein projektspezifisches `cspell.json` mit Domain-Wörterbuch. Argument: LLM-Token-Embeddings bevorzugen bekannte Wörter aus den Trainingsdaten; Tippfehler und opake Abkürzungen besitzen keine starke semantische Repräsentation und zerstören die Vorhersagequalität bei Code-Generierung.

**Das fundamentale Problem:**

- **Architektur-Constraint:** `AiNetLinter` bleibt ein monolithisches Roslyn-basiertes CLI ohne Plugin-System und ohne DI-Overhead (siehe `.cursor/rules/AiNetLinterRichtlinien.mdc`). CSpell ist eine eigenständige JavaScript/Node-Toolchain — Einbettung würde den Tool-Footprint massiv aufblähen.
- **Reifes Ökosystem verfügbar:** CSpell hat englische Wörterbücher eingebaut, wächst organisch mit `cspell.json` und wird in Tausenden Projekten produktiv genutzt. Nachbau wäre Wartungs-Overhead ohne Mehrwert.
- **Der Vorschlag selbst empfiehlt die externe Variante explizit:** „Umsetzung: als CI-Schritt, nicht in AiNetLinter".

**Fazit:** Hinweis in `Docs/configuration.md` (komplementärer CI-Schritt) ist ausreichend — keine Notwendigkeit, dies in `AiNetLinter` zu duplizieren.

---

## J. StyleCop / `dotnet format` — Member-Order & Formatierung (extern)

**Warum verworfen:** Bereits durch das .NET-SDK und `StyleCop.Analyzers` abgedeckt; AiNetLinter soll etablierte externe Tools nicht nachbauen.

**Die Idee:** Erzwingt die trainierte C#-Member-Reihenfolge `Konstanten → statische Felder → Instanzfelder → Konstruktoren → Properties → Public Methods → Private Methods` (StyleCop `SA1201`/`SA1202`/`SA1203`/`SA1204`). Argument: Verletzung dieser Konvention verwirrt das LLM beim Navigieren und Refactoren und bläht die Kontextaufnahme auf.

**Das fundamentale Problem:**

- **`dotnet format` ist im .NET-SDK integriert** — `dotnet format --verify-no-changes` liefert exakt diese Funktionalität ohne zusätzliche Abhängigkeit, im CI direkt nutzbar.
- **`StyleCop.Analyzers` ist das reife etablierte Tool** für Member-Order (`SA1201`–`SA1204`); wird in unzähligen Produktionsprojekten genutzt.
- **Der Vorschlag selbst sagt explizit „nicht in AiNetLinter nachbauen"**: „Nicht in AiNetLinter nachbauen — die Tools sind ausgereift".
- **Brownfield-Strategie ist gut dokumentiert:** `dotnet format` einmalig, dann `SA1201` als Warning, schrittweise zu Error.

**Fazit:** Empfehlung in `Docs/configuration.md` (Member-Order via `StyleCop.Analyzers` + `dotnet format`) ist ausreichend. Brownfield-Rollout-Plan im Vorschlag bereits enthalten.

---

## K. MaxChainedCallDepth — LINQ/Fluent-Kettenlänge (opt-in)

**Warum verworfen:** Haltbarkeit nur 1–2 Jahre (LLM-Typ-Inferenz verbessert sich rapide) und Konflikt mit idiomatischen LINQ-Chains machen das Aufwand/Nutzen-Verhältnis für eine AiNetLinter-Regel ungünstig.

**Die Idee:** Begrenzung der Method-Chain-Tiefe (z. B. ≤ 6) für LINQ/Fluent-Ketten. Argument: Bei jedem Glied ändert sich der Typ unsichtbar (`Where → Select → GroupBy → OrderBy → Take → ToList`); ab Tiefe 6 verliert das LLM die Übersicht über den aktuellen Typ oben in der Kette. Symptome: halluzinierte Properties, falsche Einsprungpunkte (`Where` nach `GroupBy`), fehlerhafte Lambdas.

**Das fundamentale Problem:**

- **Haltbarkeit „kurzfristig (1–2 Jahre)":** Der Vorschlag selbst stuft die Regel so ein — LLM-Typ-Reasoning verbessert sich rapide, die Regel verliert ihre Daseinsberechtigung. Passt nicht zum Hochimpact-Profil dauerhafter Regeln in AiNetLinter.
- **Konflikt mit idiomatischen LINQ:** `.Where().Select().OrderBy().Take().ToList()` ist Länge 5 — bereits alltägliches C#. Nur als striktes Opt-in sinnvoll, was die Linter-Relevanz weiter mindert.
- **Aufwand vs. Impact:** Checker + Tests + Konfiguration (`MaxChainedCallDepth` + `MaxChainedCallDepthEnabled` + `MaxChainedCallDepthExemptIfBuilderPattern`) + Doku + `RuleMetadata` für eine Regel, die nur in sehr AI-fokussierten Projekten aktiviert würde.

**Fazit:** In einer zukünftigen Linter-Generation ggf. als reines Opt-in (Default off) nachrüstbar — derzeit nicht prioritär. Argumentationslinie im Vorschlag selbst stützt die Ablehnung.

---

## L. MaxGenericTypeParameters — Generische Typ-Parameter-Anzahl

**Warum verworfen:** Haltbarkeit nur 1–2 Jahre; der Vorschlag selbst stuft die Regel als „Priorität 3, Nice-to-have, kein Must-have" ein.

**Die Idee:** Begrenzung der Anzahl generischer Typ-Parameter (z. B. ≤ 3 für Typen, ≤ 2 für Methoden). Argument: Jeder zusätzliche Typ-Parameter erhöht die Reasoning-Komplexität des LLM; BCL-Standard ist max. 2 (`Dictionary<TKey, TValue>`, `Func<T, TResult>`). Refactoring-Aufrufe erzeugen oft falsche Reihenfolgen der Typ-Argumente.

**Das fundamentale Problem:**

- **Haltbarkeit „kurzfristig (1–2 Jahre)":** Typ-Reasoning verbessert sich bei neueren Modellen schnell; der Vorschlag räumt das selbst ein: „Diese Regel adressiert primär Typ-Inferenzprobleme — in 2 Jahren vermutlich weniger kritisch."
- **Vorschlag selbst: „Implementieren, Priorität 3. Niedrig-hängende Frucht, wenig Impact. Nice-to-have, kein Must-have. Macht mehr Sinn als Qualitäts-Cleanup als als AI-Metrik."** Damit passt die Regel nicht in das Hochimpact-Profil der bereits umgesetzten Regeln (`MaxCyclomaticComplexity`, `MaxCognitiveComplexity`, `MaxConstructorDependencies`).
- **Geringe Signalrate:** In typischem Anwendungscode sind 3+ generische Typ-Parameter selten; der Linter hätte in realen Codebasen kaum Findings. `Either<TLeft, TRight, TError>` ist die seltene Ausnahme, die die Regel ad absurdum führen würde.

**Fazit:** Sinnvoller als allgemeiner Code-Qualitäts-Hinweis in einem Linter-Playbook, nicht als LLM-spezifische Hochimpact-Regel für AiNetLinter.

---

## M. MaxTupleNestingDepth — Verschachtelte Tupel und anonyme Typen

**Warum verworfen:** Sehr seltenes Pattern, Haltbarkeit 1–2 Jahre; Vorschlag selbst räumt minimale praktische Relevanz ein.

**Die Idee:** Begrenzung der Tupel-Verschachtelungstiefe (z. B. ≤ 2). Argument: Anonyme Typen und Tupel haben keine stabilen Namen; Agenten referenzieren sie über Positionsindizes oder implizite Property-Namen. Bei Tiefe 3 verliert der Agent beim Editieren die Struktur und generiert fehlerhafte Zugriffe (`data.Item2.Item2.Item1` statt `data.inner.deep.x`).

**Das fundamentale Problem:**

- **Haltbarkeit „kurzfristig (1–2 Jahre)":** C# 12+ Primary Records machen das Muster ohnehin seltener; LLM-Typ-Reasoning verbessert sich.
- **Vorschlag selbst: „Tritt so selten auf, dass der praktische Nutzen begrenzt ist. Wenn man `MaxPublicMembersPerType` und `MaxBoolParameterCount` implementiert hat, ist diese Regel das kleinste verbleibende Problem."** Trivial-Implementierung rechtfertigt keine Linter-Regel.
- **Geringe Signalrate:** Tiefe 3+ bei Tupeln ist in realem Code praktisch nicht anzutreffen — sobald ein Entwickler Tiefe 2+ sieht, schreibt er einen `record` (was auch der Vorschlag selbst als FIX empfiehlt).

**Fazit:** Pure Trivial-Implementierung, aber kaum Findings im realen Code — Linter-Overhead ohne operativen Nutzen. Höchstens als Sekundär-Check in einem allgemeineren `MaxAnonymousNesting`-Framework sinnvoll, nicht als eigenständige Regel.

---

## N. MaxImplicitConversions — Implizite Konversions-Operatoren

**Warum verworfen:** Sehr niedrige Signalrate (in modernem Code fast immer 0–1 implizite Operatoren pro Typ) bei nur mittelfristiger Haltbarkeit.

**Die Idee:** Begrenzung der Anzahl impliziter `operator`-Deklarationen pro Typ (z. B. ≤ 1). Argument: Mehrere implizite Konversionen erzeugen unsichtbare Typwechsel an Call-Sites, die das LLM beim Generieren verwechselt. Beispiel: `Money → decimal` (OK), `Money → string` zusätzlich (Violation).

**Das fundamentale Problem:**

- **Seltenes Pattern:** In modernem C#-Code haben 99 % der Typen 0 implizite Operatoren; `Money → decimal` ist das häufigste gerechtfertigte Muster. Eine Regel mit Default-Wert 1 hätte in der Praxis fast keine Findings.
- **Haltbarkeit „mittelfristig":** Der Vorschlag räumt ein: „Typ-Reasoning verbessert sich schnell bei neueren Modellen. `MaxImplicitConversions` wird vermutlich früher irrelevant als andere Regeln."
- **Konflikt mit bestehender Regel `EnforceValueObjectContracts`:** Value Objects, die `Money`/`Distance`-artige primitive Darstellung brauchen, sind bereits gut abgedeckt; ein zusätzlicher impliziter Operator ist dort idiomatisch und sollte nicht zweimal geprüft werden.
- **Aufwand minimal, Nutzen ebenso:** Reine `ConversionOperatorDeclarationSyntax`-Zählung — fast ein One-Liner, aber ohne ausreichend LLM-Impact, um die Regel-Komplexität in `rules.json`/`RuleMetadata`/Doku zu rechtfertigen.

**Fazit:** Die Regel ist als allgemeiner Code-Quality-Hinweis sinnvoll, hat aber zu wenig LLM-spezifischen Impact, um ins Hochimpact-Regelprofil von AiNetLinter zu passen.
