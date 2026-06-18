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
