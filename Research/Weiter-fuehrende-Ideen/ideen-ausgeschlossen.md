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
