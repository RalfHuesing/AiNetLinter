# AiNetLinter MCP — Analyse-Findings & Verbesserungspotenziale bei externen Assemblys

Dieses Dokument fasst die bei der praktischen Untersuchung einer externen .NET-Assembly (`targetType='assembly'`, Prüffall `LOCAL-01`) aufgetretenen Schwächen, Lücken und Reibungspunkte der AiNetLinter-MCP-Tools zusammen.

---

## 1. Fehlende Volltext- und Mustersuche (`search_pattern`)

### Was versucht wurde
Suche nach spezifischen String-Literalen, Datenbank-Tabellennamen, SQL-Schlüsselwörtern (`INSERT INTO`, `UPDATE`), Fehlercodes und Transaktions-Aufrufen innerhalb der dekompilierten Quelltexte einer externen Assembly.

### Tatsächliches Ergebnis
Das Tool bricht sofort mit einem Fehler ab:
```text
[ERROR]: ASSEMBLY_TARGET_UNSUPPORTED: Dieses Tool unterstützt das Assembly-Ziel nicht.
```
Da die MCP-Schnittstelle keine Alternative für die Textsuche in dekompilierten Assemblys bereitstellt, kann ein Agent nicht ermitteln, welche Tabellen oder SQL-Statements in einer Assembly adressiert werden, ohne die MCP-Tools zu verlassen und mit dateibasierten Werkzeugen direkt auf internen Cache-Verzeichnissen zu operieren.

### Vorschlag zur Verbesserung
* Freischaltung von `search_pattern` für `targetType='assembly'`.
* Das Tool sollte die im lokalen Dekompilierungs-Cache erzeugten Quelltextdateien (`.cs`) nach dem angegebenen Textmuster oder Regex durchsuchen und Treffer zeilenbasiert ausgeben.

---

## 2. Haupteinstiegspunkt `get_feature_context` nicht verfügbar

### Was versucht wurde
Aufruf von `get_feature_context`, um für ein identifiziertes Schlüssel-Symbol in einem einzigen Turn die Deklaration, Metriken und direkte Aufrufer zu erhalten (gemäß der allgemeinen MCP-Workflow-Empfehlung „Start: get_feature_context“).

### Tatsächliches Ergebnis
Das Tool verweigert den Aufruf mit `ASSEMBLY_TARGET_UNSUPPORTED`.
Ein Agent muss dadurch 4 bis 5 separate Werkzeuge nacheinander ausführen (`find_symbol`, `get_class_structure`, `get_symbol_body`, `get_call_tree`, `metrics_lookup`), was die Roundtrips und den Kontextverbrauch vervielfacht.

### Vorschlag zur Verbesserung
* Unterstützung von `targetType='assembly'` in `get_feature_context`.
* Dimensionen, die bei einer externen Assembly naturgemäß nicht existieren (zugeordnete Unit-Tests, Linter-Violations der Projektkonfiguration), sollten als `not_applicable` oder leer ausgewiesen werden, anstatt das gesamte Werkzeug zu blockieren.
* Signatur, Dekompilierungs-Ausschnitt, Code-Metriken und Aufrufer stehen im Roslyn-Workspace bereits zur Verfügung und sollten kompakt gebündelt geliefert werden.

---

## 3. Keine physische Struktur- und Dateiübersicht (`get_file_tree`)

### Was versucht wurde
Verschaffen eines Überblicks über die Datei- und Modulstruktur der dekompilierten Assembly, um festzustellen, welche Dateien vorhanden sind und wie die Quelltexte gegliedert wurden.

### Tatsächliches Ergebnis
Das Tool bricht mit `ASSEMBLY_TARGET_UNSUPPORTED` ab, obwohl in der Tool-Beschreibung explizit „dekompilierten SourceRoots“ aufgeführt ist.
Als Behelf musste auf `metrics_tree` ausgewichen werden, das jedoch primär auf Code-Metriken ausgelegt ist und keine gezielten Dateimasken oder flexiblen Ansichten bietet.

### Vorschlag zur Verbesserung
* Freischaltung von `get_file_tree` für `targetType='assembly'`.
* Anzeige der dekompilierten Ordner- und Dateistruktur mit den gewohnten Filtern (`root`, `fileFilter`, `view='tree'|'summary'|'files'`).

---

## 4. Typ-Hierarchie-Abfrage kollidiert mit Member-Eigenschaften (`get_type_hierarchy`)

### Was versucht wurde
Abfrage der Vererbungs- und Schnittstellenhierarchie einer Klasse über die Übergabe des Klassennamens (`symbolIdentifier: "SampleClass"`).

### Tatsächliches Ergebnis
Das Tool brach mit `AMBIGUOUS_SYMBOL` ab, weil in zwei anderen Klassen jeweils eine Eigenschaft (Property) mit demselben Namen existierte:
```text
[ERROR]: AMBIGUOUS_SYMBOL: Identifikator 'SampleClass' ist mehrdeutig — mehrere Symbole gefunden.
- Klasse: Namespace.SampleClass
- Property: Namespace.OtherClassA.SampleClass
- Property: Namespace.OtherClassB.SampleClass
```
Der Agent musste die Abfrage mit dem vollqualifizierten Typnamen wiederholen.

### Vorschlag zur Verbesserung
* Bei `get_type_hierarchy` handelt es sich fachlich ausschließlich um eine Abfrage von Typen.
* Die interne Symbolauflösung muss hier strikt auf `INamedTypeSymbol` (Klassen, Interfaces, Structs, Enums) gefiltert sein. Member wie Eigenschaften oder Methoden dürfen bei einer Typ-Hierarchie-Abfrage niemals als Namenskonflikt gewertet werden.

---

## 5. Aufrufbäume werden von Property-Gettern überflutet (`get_call_tree`)

### Was versucht wurde
Erstellen eines ausgehenden Aufrufbaums (`direction="outgoing"`), um ausgehend von einer zentralen Orchestrierungs- oder Speichermethode zu sehen, welche Untermethoden und Verarbeitungsschritte aufgerufen werden.

### Tatsächliches Ergebnis
Da Entitätsklassen in .NET typischerweise viele Datenfelder als Properties abbilden und diese intern Methoden (`get_...`) darstellen, wurde der Baum von Dutzenden trivialen Property-Zugriffen überflutet.
Wegen der Begrenzung über `topN` (Standard: 10) wurden die tatsächlich relevanten Untermethoden (z. B. Sub-Routinen, Datenbank-Transaktionen) vollständig aus der sichtbaren Baumstruktur verdrängt (`... und 10 weitere`).

### Vorschlag zur Verbesserung
* Einführung eines Parameters wie `excludeProperties: true` (oder `kindFilter: ["Method"]`).
* Dadurch können triviale Getter/Setter ausgeblendet werden, sodass der semantische Ablauf der eigentlichen Logikmethoden unmittelbar sichtbar wird.

---

## 6. Fehlende Paginierung und Zeilen-Offsets bei Methodenrümpfen (`get_symbol_body`)

### Was versucht wurde
Lesen des vollständigen Codes längerer Methoden (z. B. 120 bis 180 Zeilen), um den detaillierten Ablauf eines Speichervorgangs nachzuvollziehen.

### Tatsächliches Ergebnis
Das Tool schneidet den Rumpf bei Erreichen von `maxBodyLines` (Standard: 80) ab.
Im Gegensatz zu Dateilese-Tools gibt es keinen Parameter für `startLine` oder `offset`. Ein selektives Nachladen der Folgezeilen (z. B. Zeilen 81–160) ist nicht möglich. Der Agent muss `maxBodyLines` auf Verdacht stark vergrößern.

### Vorschlag zur Verbesserung
* Ergänzung von Parametern wie `startLine` / `lineCount` (oder `offset` / `limit`) in `get_symbol_body`, um auch bei langen Methoden gezielt Ausschnitte oder Folgeseiten anfordern zu können.

---

## 7. Strikte Verweigerung bei Methoden-Überladungen (`get_symbol_body`)

### Was versucht wurde
Lesen des Methodenrumpfes mit der üblichen Kurzform `SampleClass.SampleMethod`.

### Tatsächliches Ergebnis
Existieren mehrere Überladungen mit unterschiedlichen Parameterlisten (z. B. parameterlos, mit einem Flag, mit zwei Flags), bricht das Tool mit `AMBIGUOUS_SYMBOL` ab und listet die überlangen internen DocComment-IDs auf. Dies erzwingt einen zusätzlichen Abfrage-Schritt.

### Vorschlag zur Verbesserung
* Tolerantere Behandlung von Überladungen: Sofern das Zeilenbudget ausreicht, sollten alle Überladungen der Methode direkt gebündelt zurückgegeben werden.
* Alternativ: Unterstützung von Kurzsignaturen mit Parameteranzahl oder Basistypen (z. B. `SampleClass.SampleMethod(2)` oder `SampleClass.SampleMethod(bool, bool)`), ohne die vollständige XML-Doc-Signatur verlangen zu müssen.

---

## 8. Unzureichende Member-Sichtbarkeit und Filterung (`inspect_assembly`)

### Was versucht wurde
Erkunden einer unbekannten externen Assembly bzw. einer großen Klasse, um herauszufinden, welche Verarbeitungs- und Speichermethoden vorhanden sind.

### Tatsächliches Ergebnis
1. **Truncation-Bias:** Bei großen Klassen (über 100 Member) werden standardmäßig nur 3 Member gezeigt. Da die Sortierung alphabetisch erfolgt, erscheinen häufig nur Ereignisse (`Event...`), während sämtliche Kernmethoden trunkiert werden.
2. **Standardfilter `publicOnly: true`:** Interne und private Methoden (in denen bei vielen Bibliotheken die eigentliche Speicher- und Datenbanklogik gekapselt ist) werden standardmäßig komplett verschwiegen.
3. **Substring-Rauschen:** Die Suche mit `typeName="Sample"` listet alle Typen auf, die diesen Textteil enthalten (oft Dutzende Hilfs- und Exception-Typen), anstatt exakt die gewünschte Klasse zu selektieren.

### Vorschlag zur Verbesserung
* Methoden bei der Truncation priorisieren (z. B. Methoden vor Events und Feldern listen).
* Deutlicherer Hinweis im Antwort-Header, wenn durch `publicOnly: true` interne Member ausgeblendet wurden.
* Heuristische Exaktsuche oder besseres Defaulting für `exactTypeName`, wenn ein eindeutiger Klassenname übergeben wird.

---

## 9. Token-Ineffizienz durch redundante absolute Cache-Pfade (`find_symbol` u. a.)

### Was versucht wurde
Suche nach Symbolen über Namensteile via `find_symbol`.

### Tatsächliches Ergebnis
Für jedes gefundene Symbol wird der vollständige, rund 160 Zeichen lange absolute Pfad im internen Server-Cache ausgegeben:
```text
C:\Daten\Tools\AiNetLinter-win-x64\cache\asm\...\generation-...\Namespace\SampleClass.cs:2270
```
Bei 20 bis 50 Treffern führt dies zu einer massiven Verschwendung von Tokens für wiederholte Verzeichnispfade.

### Vorschlag zur Verbesserung
* Ausgabe von relativen Pfaden bezogen auf den Dekompilierungs-Wurzelordner (z. B. `Namespace/SampleClass.cs:2270`).
* Nennung des absoluten Cache-Stammverzeichnisses einmalig im Session-Header.

---

## 10. Pfad-Verstümmelung im Abhängigkeitsgraphen (`dependency_graph`)

### Was versucht wurde
Ermitteln aller ein- und ausgehenden Abhängigkeiten einer Klasse über `dependency_graph`.

### Tatsächliches Ergebnis
Die analysierte Ausgangsklasse wurde mit dem korrekten Cache-Pfad aufgeführt. Sämtliche abhängigen Kind-Dateien wurden jedoch fehlerhaft gekürzt und direkt unter das Basis-Installationsverzeichnis des Servers gehängt:
```text
- C:\Daten\Tools\AiNetLinter-win-x64\SampleDependency.cs (1 Typ: SampleDependency)
```
Der gesamte Cache-Unterordnerpfad ging bei den Kind-Einträgen verloren.

### Vorschlag zur Verbesserung
* Korrektur der Pfadberechnung/Normalisierung in `dependency_graph`, damit relative oder vollständige Pfade innerhalb der dekompilierten Workspace-Struktur intakt bleiben.

---

## 11. Hohe Latenz und funktionale Redundanz (`get_impact`)

### Was versucht wurde
Prüfung der Auswirkungen und Aufrufer eines Symbols in einer externen Assembly mittels `get_impact`.

### Tatsächliches Ergebnis
Die Ausführung benötigte zwischen 18 und 100 Sekunden.
Da für externe DLLs weder ein lokaler Git-Diff noch ausführbare Testsuites existieren, lieferte das Tool im Ergebnis exakt dieselben Aufrufstellen wie das in weniger als einer Sekunde antwortende `find_references`.

### Vorschlag zur Verbesserung
* Optimierung der internen Ausführungspfade für Assembly-Ziele: Unnötige Vorabprüfungen oder Diff-Mechanismen überspringen und direkt auf die schnelle Referenzauflösung durchgreifen.
* Dokumentation schärfen: Agenten darauf hinweisen, für externe Assemblys primär `find_references` statt `get_impact` zu verwenden.

---

## II. Befunde beim Git-Source-Mapping (Prüffall `GIT-01`)

## 12. Fallback auf Dekompilierung trotz konfiguriertem Git-Repository (`provider-unavailable`)

### Was versucht wurde
Analyse einer Assembly, für die in `external-sources.json` ein Git-Repository mit Quellcode hinterlegt ist (`targetType='assembly'`), mit dem Ziel, die semantische Analyse auf dem echten Quellcode (`origin=source`) statt auf dekompiliertem Code durchzuführen.

### Tatsächliches Ergebnis
Das Source-Mapping greift nicht für die Analyse. Der MCP-Server fällt stillschweigend auf die Dekompilierung zurück:
```text
[ASSEMBLY] origin=decompiled; sourcePath=none; fallbackReason=provider-unavailable; sourceDiagnostics=2/2
```
Obwohl der Git-Clone-Prozess im Hintergrund gestartet wurde und das Repository in den Cache geladen hat, wird der dekompilierte Quelltext als Workspace verwendet. Die Analyse profitiert somit nicht vom tatsächlichen Quellcode des Repositories.

### Vorschlag zur Verbesserung
* Der Server sollte transparente Statusmeldungen liefern, an welchem Schritt des Source-Provider-Workflows der Übergang von Git-Source zu Dekompilierung erfolgt ist.
* Ein harter Modus oder Schalter (z. B. `preferSourceOnly: true` oder `requireSource: true`), der fehlschlägt oder detailliert Auskunft gibt, anstatt opportunistisch und ohne Warnung auf Dekompilierung auszuweichen, würde die Fehlersuche bei fehlerhaften Git-Mappings drastisch erleichtern.

---

## 13. Fehlschlag der Git-Checkout-Verifikation (`external-source-repository-checkout-unverified`)

### Was versucht wurde
Herunterladen und Verifizieren eines externen Git-Repositories im Hintergrund durch den MCP-Server, um einen sauberen, vertrauenswürdigen Quellcode-Stand bereitzustellen.

### Tatsächliches Ergebnis
Nach dem Klonen des Repositories führt der Server eine Verifikationsprüfung (`git status --porcelain=v1 --untracked-files=all --ignored=all`) in einer isolierten Prozessumgebung aus (`GIT_CONFIG_NOSYSTEM=1`, `GIT_CONFIG_GLOBAL=NUL`, `GIT_CONFIG_SYSTEM=NUL`).
Auf Windows-Systemen schlägt dieser Statusaufruf fehl oder erzeugt Ausgaben auf dem Standardfehlerkanal (`fatal: not a git repository` / dubiose Ownership / unvollständige Git-Konfiguration).
Der Server bricht daraufhin die Bereitstellung ab und stuft den Checkout als nicht verifizierbar ein:
```text
External-Source-Diagnose [error] external-source-repository-checkout-unverified: Der Repository-Checkout konnte nicht als sauber verifiziert werden. ($repository)
```

### Vorschlag zur Verbesserung
* Robuste Handhabung der Git-Umgebung unter Windows: Die vollständige Abriegelung aller Git-Konfigurationen (`GIT_CONFIG_GLOBAL=NUL`) führt unter Windows häufig zu Problemen mit `safe.directory` und Pfadtrennersicherheitsprüfungen von Git.
* Für die Status- und Head-Prüfung sollte der Server sicherstellen, dass die Arbeitsverzeichnis-Parameter (`-C <Pfad>` bzw. `--git-dir` und `--work-tree`) explizit und Windows-resilient an den Git-Prozess übergeben werden.
* Wenn Git auf `stderr` unkritische Warnungen (z. B. CRLF-Hinweise) ausgibt, darf dies nicht pauschal als Vertrauensverlust und Fehler gewertet werden.

---

## 14. Abbruch der Bereinigung bei schreibgeschützten Git-Dateien unter Windows (`external-source-repository-cleanup-failed`)

### Was versucht wurde
Automatisches Bereinigen und Löschen eines unvollständigen oder fehlgeschlagenen Repository-Checkouts durch den Server (`TryCleanup`).

### Tatsächliches Ergebnis
Die Bereinigungsroutine versucht, Dateien im Checkout-Verzeichnis rekursiv via `File.Delete(path)` zu entfernen.
Git setzt bei Pack-Dateien (`.git/objects/pack/*.idx`, `*.pack`, `*.rev`) unter Windows standardmäßig das Dateiattribut `ReadOnly`.
In .NET wirft `File.Delete` bei schreibgeschützten Dateien eine `UnauthorizedAccessException`. Die Bereinigung bricht mittendrin ab:
```text
External-Source-Diagnose [error] external-source-repository-cleanup-failed: Der Repository-Checkout konnte nicht sicher bereinigt werden. ($repository)
```
Das Verzeichnis verbleibt in einem halb gelöschten Zustand (Konfigurations- und Kopfdateien wurden gelöscht, schreibgeschützte Binärdateien bleiben liegen). Nachfolgende Wiederholungsversuche schlagen fehl, weil das Verzeichnis blockiert oder korrumpiert ist.

### Vorschlag zur Verbesserung
* Vor dem Löschen von Dateien in `ExternalSourceRepositoryPathGuard.TryDeleteEntry` muss das Schreibschutz-Attribut unter Windows explizit entfernt werden:
  ```csharp
  File.SetAttributes(path, FileAttributes.Normal);
  File.Delete(path);
  ```
* Alternativ sollte eine robuste Löschroutine verwendet werden, die `ReadOnly`-Attribute rekursiv zurücksetzt, bevor das Verzeichnis entfernt wird.

---

## 15. Intransparente Diagnoseanzeige bei Provider-Fehlschlägen (verborgene `sourceDiagnostics`)

### Was versucht wurde
Verstehen, warum eine Quellcode-Zuordnung für eine Assembly nicht funktioniert hat, anhand der Rückgabe von `inspect_assembly` oder `find_symbol`.

### Tatsächliches Ergebnis
Die Werkzeuge zeigen im Header lediglich summarisch an:
```text
[ASSEMBLY] ... fallbackReason=provider-unavailable; sourceDiagnostics=2/2
```
Welche 2 Diagnosen aufgetreten sind (`checkout-unverified`, `cleanup-failed`), wird in der Tool-Antwort komplett verschwiegen. Der aufrufende Agent erhält keinen Hinweis auf den tatsächlichen Grund und kann das Problem nicht eingrenzen. Erst ein tiefer Abruf über `get_server_health` mit `includeDiagnostics=true` und `maxDiagnostics=100` fördert die Fehlermeldungen zutage.

### Vorschlag zur Verbesserung
* Wenn `fallbackReason` aktiv ist und `sourceDiagnostics > 0` vorliegen, sollten die Fehlerbeschreibungen der Source-Diagnosen direkt im Header oder in einem Diagnose-Abschnitt des jeweiligen Analyse-Tools mit ausgegeben werden.
* Ein Agent oder Entwickler sollte unmittelbar sehen können: *„Fallback auf Dekompilierung, weil Git-Status mit Exit-Code 1 fehlschlug: [Fehlertext]“*.
