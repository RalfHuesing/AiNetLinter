Überlege mal bitte mit welche anderen Dinge wir mit evtl. anderen Tools wir noch implementieren könnten um "guten" (für LLMs verständlichen Code) generierten zu lassen.
Das muss natürlich realistisch umsetzbar sein.
Das Tool soll lokal und deterministisch laufen.

Aktuell verwenden wir Roslyn mit statistischen Analysen.
Wir haben oft False-Positives: Das liegt aber an fehlenden Features in AiNetLinter und/oder an Konfigurations-Problemen. Das beheben wir gerade und soweit passt das.

Wenn es sonst nichts weiter gibt wäre das auch eine Faire Antwort!
Erfinde nicht künstlich irgendwas was insbesondere keinen Sinn macht.

---

Das ist eine hervorragende Frage. Da ihr mit Roslyn bereits den Goldstandard für deterministische, lokale C#-Code-Analyse nutzt und Dinge wie Komplexität (Cyclomatic/Cognitive), Zeilenlimits und strikte Typisierung (`sealed`, etc.) abdeckt, habt ihr vermutlich schon 80-90 % dessen erreicht, was Code für ein LLM "verständlich" macht.

Wenn wir uns anschauen, wie LLMs technisch funktionieren (Tokenisierung, Attention-Mechanismen, begrenztes Kontextfenster), gibt es noch ein paar **deterministische, lokal messbare** Aspekte, die man mit Roslyn oder einfachen Ergänzungstools implementieren könnte.

Hier sind realistische und sinnvolle Ansätze, die keinen künstlichen Overhead erzeugen:

### 1. "Context Fan-Out" Metrik (Abhängigkeits-Limitierung)

**Warum es LLMs hilft:** Ein LLM hat ein begrenztes Kontextfenster (Context Window). Wenn es eine Klasse ändern soll, muss es idealerweise alle Abhängigkeiten dieser Klasse "kennen". Wenn eine Klasse 15 verschiedene andere Domain-Klassen referenziert, explodiert der benötigte Kontext im Prompt, was zu Halluzinationen führt.
**Die Umsetzung (via Roslyn in AiNetLinter):**

- Zähle nicht nur die Parameter eines Konstruktors, sondern die **Anzahl der einzigartigen, projekteigenen Typen**, die in einer Datei referenziert werden (Imports/Usings auf eigene Namespaces oder referenzierte Syntaxknoten).
- **Regel:** `MaxDomainTypeDependencies: 5` (als Beispiel).
- Das erzwingt kleine, isolierte Bausteine, die ein LLM perfekt in einem Zero-Shot-Prompt ohne großen "Include-Overhead" verstehen kann.

### 2. Typo- & Wording-Checks (Tokenisierungs-Optimierung)

**Warum es LLMs hilft:** LLMs lesen keine Buchstaben, sie lesen Tokens (z. B. via Byte Pair Encoding). Ein Wort wie `CustomerData` ist vielleicht 2 Tokens lang und stark semantisch in den Gewichten des LLMs verankert. Ein Tippfehler oder eine schlechte Abkürzung wie `CustDat` oder `CstmrDta` zerfällt in 4-5 bedeutungslose Sub-Tokens. Das zerstört die semantische Leistungsfähigkeit des LLMs massiv.
**Die Umsetzung:**

- **Tool:** Integration von **CSpell** (lokal, rasend schnell, deterministisch) in die CI-Pipeline.
- **Erweiterung:** Eine deterministische "Ubiquitous Language" (Glossar) via Roslyn. Wenn ein Projekt `Invoice` nutzt, darf nicht an anderer Stelle `Bill` im Code vorkommen (Roslyn Analyzer, der Typ- und Eigenschaftsnamen gegen eine JSON-Glossar-Liste prüft).

### 3. Lack of Cohesion of Methods (LCOM4)

**Warum es LLMs hilft:** Ihr habt aktuell Limits für Dateigröße und Methodenkomplexität. Aber eine 200-Zeilen-Klasse kann trotzdem zwei völlig unabhängige Dinge tun (z. B. Daten parsen UND in die Datenbank schreiben). LLMs tun sich extrem schwer mit "God Classes", weil der Attention-Mechanismus verwässert wird.
**Die Umsetzung (via Roslyn in AiNetLinter):**

- Implementierung der LCOM4-Metrik (Lack of Cohesion of Methods).
- Der Algorithmus prüft deterministisch, ob Methoden in einer Klasse dieselben Klassen-Felder/Properties nutzen. Wenn Methode A und B nur Feld X nutzen, und Methode C und D nur Feld Y, hat die Klasse eine LCOM-Score > 1 (sie sollte in zwei Klassen aufgeteilt werden).
- Eine LCOM von 1 bedeutet maximale Kohäsion: Das LLM weiß genau, "was der eine Zweck dieser Klasse ist".

### 4. Deterministische "Purity"-Erzwingung (Verbieten von verstecktem State)

**Warum es LLMs hilft:** LLMs sind exzellent darin, pure Funktionen (Input -> Output) zu generieren. Sie halluzinieren, wenn es versteckte Nebeneffekte oder globalen State gibt.
**Die Umsetzung:**

- **Tool:** Das offizielle Microsoft-Paket `Microsoft.CodeAnalysis.BannedApiAnalyzers`.
- **Konfiguration:** Verbannt in den Core-Domain-Schichten explizit Aufrufe wie `DateTime.Now`, `Guid.NewGuid()`, `Environment.GetEnvironmentVariable()`, oder `Task.Run`.
- Zwingt den (von der KI generierten) Code dazu, Zeit und Zufall als Parameter (`TimeProvider`, ID-Generatoren) injiziert zu bekommen. Das macht den KI-Code zu 100 % deterministisch testbar.

### 5. Layout- & Syntax-Normalisierung

**Warum es LLMs hilft:** LLMs sagen das nächste Token voraus. Je vorhersagbarer und konsistenter der Aufbau einer Datei ist, desto seltener macht das LLM Syntaxfehler.
**Die Umsetzung:**

- Wenn nicht schon geschehen: Strikte Nutzung von `dotnet format` oder _StyleCop.Analyzers_.
- Erzwingt eine starre Reihenfolge: 1. Konstanten, 2. Private `readonly` Felder, 3. Konstruktoren, 4. Public Properties, 5. Public Methods, 6. Private Methods.
- Das reduziert die Entropie in den Prompts massiv.

---

### Ein faires Fazit: Gibt es sonst noch was?

**Ganz ehrlich: Nein.** Wenn ihr die oben genannten Punkte (oder Teile davon) abdeckt und eure aktuellen Roslyn-Regeln (wie im Code-Snippet aus `rules.json` ersichtlich: `EnforceSealedClasses`, `MaxCognitiveComplexity`, MVVM/UI-Trennung) weiter verfeinert und die False-Positives eliminiert, **seid ihr bereits am Limit dessen, was statisch und sinnvoll machbar ist.**

Versucht **nicht**, KI-basierte Linter (z. B. "LLM-Reviewer" im CI-Pipeline-Schritt) einzubauen, um Code "noch besser" zu machen. Diese sind langsam, teuer und verletzen eure Anforderung der _Lokalität und des Determinismus_.

Bleibt bei AST-basierter (Roslyn) Analyse. Optimiert euren Code auf **maximale Kohäsion**, **minimale Abhängigkeiten** und **klare Semantik (Vokabular)**. Das ist das Beste, was ihr für LLMs tun könnt.
