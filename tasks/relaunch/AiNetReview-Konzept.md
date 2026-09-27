# AiNetReview – Konzept und DoD

Dieses Dokument ist die verbindliche Grundlage für das neue Repository. Das ursprüngliche [Relaunch-Konzept](Relaunch-Konzept.md) bleibt als unveränderte Motivation erhalten. Änderungen an diesem Konzept ersetzen die betreffende Aussage hier; es gibt keine parallelen Entscheidungslisten.

## Ziel und Grenzen

AiNetReview ist ein lokales Werkzeug für C#/.NET-Reviews. Es findet deterministisch mögliche Qualitäts- und Architekturprobleme, stellt Quellcode und Messwerte bereit und hält Review-Entscheidungen über spätere Läufe nach. Das Urteil trifft der Agent zusammen mit dem Nutzer. Ein Finding blockiert weder Build noch Commit und ist kein automatischer Refactoring-Auftrag.

Der Entwicklungstask wird zuerst abgeschlossen. Der Review-Lauf folgt danach oder später. Eindeutige technische Fehler bleiben Aufgabe etablierter Build-Analyzer; AiNetReview behandelt Hinweise, deren Bewertung Kontext verlangt. Es ersetzt keine Code-Navigation. Eine Ablösung des bisherigen AiNetLinter erfolgt erst nach erfolgreicher Einführung des neuen Werkzeugs.

Eine Regel gehört nur dann in ein hartes Build-Gate, wenn der Verstoß unabhängig vom fachlichen Kontext ein Fehler ist und seine direkte Korrektur keine Ursachen verdeckt. Schwellwerte, Struktur- und Verantwortlichkeitsvermutungen gehören in den Review-Lauf.

Arbeitsthese: Für Agenten guter Code muss zuverlässig und mit wenig Such- und Kontextaufwand verständlich sein. Menschliche Lesbarkeit und agentische Verständlichkeit überschneiden sich, sind aber nicht deckungsgleich. „Schönheit“ ist kein Regelziel. Schwer wird Code insbesondere durch viele verschachtelte Entscheidungswege, über mehrere Dateien verstreute Abhängigkeiten, versteckten Zustand, irreführende Dokumentation und schwer auffindbaren relevanten Kontext. Das ist eine Arbeitshypothese, kein bereits kalibrierter Gesamtscore. Studien zu Coding-Agenten belegen Probleme bei der Auswahl relevanten Repository-Kontexts und beim Verfolgen von Abhängigkeiten; sie belegen keinen universellen Grenzwert für kognitive Komplexität. Siehe [ContextBench](https://arxiv.org/abs/2602.05892), [DyRetriever](https://arxiv.org/abs/2608.01927) und die [Untersuchung zu fehlerhafter Code-Dokumentation](https://aclanthology.org/2024.findings-naacl.66/).

**Empfehlung und Festlegung:** Der Produktname lautet `AiNetReview`. Ein ausführbarer Prozess bietet MCP und CLI; beide nutzen denselben Kern.

## Erster nutzbarer Umfang

Der erste vollständige Stand enthält genau eine Review-Regel:

| Regel-ID | Gegenstand | Initialer Grenzwert | Quellcode-Snapshot |
| --- | --- | ---: | --- |
| `max-cognitive-complexity` | Benannte C#-Methoden mit hoher Verzweigungs- und Verschachtelungslast | Score über 15 | vollständige Methode |

Die Regel untersucht benannte Methodendeklarationen mit Body, einschließlich generischer und asynchroner Methoden. Konstruktoren, Accessoren, lokale Funktionen und Lambdas erhalten zunächst keine eigenen Findings; lokale Funktionen und Lambdas innerhalb einer Methode fließen in deren Score ein. Der Score zählt Kontrollflusswechsel und zusätzliche Verschachtelung; bloße Länge erhöht ihn nicht. Generierte Dateien werden nicht analysiert. Flache Objektinitialisierung mit vielen Null-Coalescing-Ausdrücken und reine Switch-Dispatcher werden wie im bisherigen AiNetLinter erkannt und ausgenommen. Der Grenzwert 15 ist eine konfigurierbare Anfangshypothese, kein belegter KI-Grenzwert. Der Bericht zeigt Score, Grenzwert und die maßgeblichen Verzweigungsstellen. Er empfiehlt keine automatische Methodenzerlegung. Eine lange, lineare Mapper-Methode erzeugt keinen Treffer; komplexe Mapper-Logik wird geprüft.

**Empfehlung:** Diese Regel ist der beste erste Vertical Slice, weil die lokale Messung im AiNetLinter bereits implementiert und getestet ist. Sie deckt nur einen Teil agentischer Verständlichkeit ab. Der vorhandene [AIContextFootprint](../../src/AiNetLinter/Metrics/AIContextFootprintCalculator.cs) ist als Idee für später relevant, aber seine heutige Implementierung verfolgt vor allem Typen aus Membersignaturen und zählt ganze Quelldateien. Sein Wert ist daher kein belastbarer Maßstab für die tatsächlich nötigen Navigationsschritte eines Agenten. Konstruktorabhängigkeiten, öffentliche API-Breite, Partial-Klassen und Vererbungstiefe bleiben weitere Kandidaten außerhalb dieses DoD. Neue Regeln folgen erst nach dem vollständigen ersten Durchlauf. Der Kern enthält keinen Sonderfall für die Startregel.

## Repository und Projekte

`AiNetReview.slnx` enthält `src/AiNetReview.Core`, den ausführbaren Host `src/AiNetReview` sowie `tests/AiNetReview.Tests` und `tests/AiNetReview.IntegrationTests`. Der Host bietet `ainetreview mcp` und `ainetreview review`. Namensräume im Core: `AiNetReview.Configuration`, `.Analysis`, `.Rules`, `.Findings`, `.Reporting`, `.Storage`; im Host `AiNetReview.Mcp` und `AiNetReview.Cli`. Es gibt keinen DI-Container, keine dynamische Regelsuche und keinen zusätzlichen Daemon.

Das neue Repository erhält .NET-10-SDK-Pinning, gemeinsame Compiler-Einstellungen, CI für Build und Tests, eine knappe README und eigene Agentenanweisungen ohne hartes Review-Gate. Dogfooding prüft AiNetReview an seiner Solution; kleine Test-Solutions mit bekannten Treffern und Nichttreffern sind die maßgebliche Regelprüfung.

Geeignete Vorlagen aus AiNetLinter, selektiv zu prüfen: [SourceFileCatalogLoader](../../src/AiNetLinter/Baseline/SourceFileCatalogLoader.cs) für MSBuild/Roslyn, [LongRunningToolCallStore](../../src/AiNetLinter/Mcp/LongRunningToolCallStore.cs) für Polling, [DuplicateMethodCollector](../../src/AiNetLinter/Core/DuplicateDetection/DuplicateMethodCollector.cs) für Syntax-Traversierung und generierte Dateien, [RuleRegistry](../../src/AiNetLinter/Core/RuleRegistry.cs) für statische Registrierung sowie [ComplexityCalculator](../../src/AiNetLinter/Metrics/ComplexityCalculator.cs), [MethodClassifier](../../src/AiNetLinter/Core/Checkers/MethodClassifier.cs), [SwitchDispatcherDetector](../../src/AiNetLinter/Metrics/SwitchDispatcherDetector.cs) und [zugehörige Tests](../../src/AiNetLinter.FastTests/Metrics/CognitiveComplexityWalkerTests.cs) für die erste Regel. Keine 1:1-Übernahme des alten Linter-Vertrags.

## Eingaben und Pfade

Im Wurzelverzeichnis des analysierten Repositories liegt genau die Konfiguration `ainetreview.json`. Sie enthält `schemaVersion: 1`, `solution`, `outputDirectory`, `storageDirectory` und ein Objekt `rules` mit Regel-IDs und deren Parametern. Die erste Regel besitzt `maxScore` als positive ganze Zahl mit Standardwert 15. Mindestens eine Regel muss aktiviert sein; unbekannte Regeln und Parameter sind Fehler. Nicht angegebene Regelparameter erhalten den im Regelkatalog beschriebenen Standardwert. Eine gültige Startkonfiguration lautet:

```json
{
  "schemaVersion": 1,
  "solution": "AiNetReview.slnx",
  "outputDirectory": "audit-reporting",
  "storageDirectory": ".ainetreview",
  "rules": {
    "max-cognitive-complexity": { "maxScore": 15 }
  }
}
```

Alle Pfade **innerhalb** der Konfiguration und aller dauerhaft gespeicherten Daten sind relativ zum Repository. Ausgaben verwenden einheitlich `/` als Trennzeichen, etwa `src/Foo.cs`. Der Host löst sie intern gegen das Repository auf; Pfade, die es verlassen, und eingebundene Quelldateien außerhalb des Repositories sind Fehler. Ausgabeordner und Speicherordner liegen im Git-Repository, sind verschieden und enthalten keine analysierten C#-Quellen. Die Validierung verlangt, dass `.gitignore` den Ausgabeordner ausschließt und den Speicherordner nicht ausschließt. Absolute Maschinenpfade stehen weder in Berichten noch im Speicher. Unbekannte Konfigurations- und Storage-Schemaversionen werden abgewiesen, nicht still interpretiert.

MCP bekommt den absoluten Pfad zu `ainetreview.json`, um das Repository zu finden. Die CLI nimmt direkte Parameter: `review --repo <absolut> --solution <relativ> --output <relativ> --storage <relativ> --rule <id> [--rule <id> ...] [--set <id>.<parameter>=<wert> ...]`. `--set` verwendet die Typen und Validierung der Regeldeskriptoren. Die CLI liest keine zusätzliche Konfigurationsdatei. Beide Adapter erzeugen dieselbe validierte interne Review-Anfrage.

## Regelvertrag

Eine statische `RuleRegistry` registriert jede Regel genau einmal über ihre stabile ID. Eine Regel kann aus mehreren Dateien bestehen. Der Vertrag `IReviewRule` bietet Metadaten, Verhaltensversion, typisierte Konfigurationsbeschreibung, Referenztext und `ExecuteAsync(ReviewContext, RuleOptions)`. Der Runner lädt die Roslyn-`Solution` einmal, stellt gemeinsam nutzbare Syntax- und Semantikzugriffe bereit und führt aktivierte Regeln zunächst nacheinander aus. Abbruch und Fortschritt werden durchgereicht.

`RuleResult` enthält strukturierte Finding-Entwürfe. Jeder Entwurf nennt Regel, repo-relativen Ort, betroffene Einheit, eindeutigen Diskriminator innerhalb dieser Einheit, Messwerte, Begründung, Review-Evidenz, relevante rohe Quellcode-Snapshots und den Vergleichsumfang. Die Regel entscheidet, welcher Code ursächlich ist und welche Details der Agent zum Urteil braucht. Ein gemeinsames Mindestformat macht Berichte, Abgleich und Speicherung generisch. Regeln schreiben weder Markdown noch Storage und vergeben keine Finding-IDs.

Aus den Regeldeskriptoren erzeugt `ainetreview catalog --repo <absolut>` die Dateien `Docs/ainetreview-rules.md` und `ainetreview.example.json`. Der Befehl überschreibt `ainetreview.json` nie. Der Regeltext enthält Zweck, Messung, Parameter und Review-Fragen. Beim Löschen einer Regel entfallen ihre Dateien, Tests und genau ein Registry-Eintrag; Katalog und Vorlage werden neu erzeugt.

## Finding-Identität und Änderungsvergleich

Der stabile Zuordnungsschlüssel eines Findings besteht aus Regel-ID, repo-relativem Pfad, Symbolkennung beziehungsweise Dateikennung und dem regel-eigenen Diskriminator. Eine neue Quelle erhält eine weltweit eindeutige, zufällige Finding-ID im Format `F-<32 Hexzeichen>`. Diese ID bleibt bei Änderungen derselben zugeordneten Einheit erhalten. Bei Umbenennung, Verschiebung oder Signaturänderung entsteht konservativ ein neues Finding; die alte Zuordnung wird nach einem vollständigen Scan als verschwunden markiert. Frühere Freigaben werden dabei nicht still auf neuen Code übertragen. Mehrere Treffer derselben Regel in einer Methode brauchen verschiedene Diskriminatoren.

Die zentrale Fingerprint-Komponente erhält von der Regel den Vergleichsumfang und den Normalisierungsmodus. Die Startregel vergleicht die vollständige Methode. Die Normalisierung entfernt Formatierung und Kommentare und ersetzt nur semantisch aufgelöste lokale Variablen- und Parameternamen durch stabile Platzhalter. Literale, Operatoren, Aufrufe, Membernamen, Typen und Präprozessoranweisungen bleiben erhalten. Wenn die semantische Auflösung nicht sicher gelingt, verwendet die Komponente einen konservativen Tokenvergleich ohne Umbenennungsnormalisierung. Der rohe Quellcode wird unabhängig davon gespeichert. Eine kleine Änderung außerhalb des relevanten Umfangs öffnet ein akzeptiertes Finding nicht erneut.

Eine Freigabe gilt nur bei gleichem Fingerprint, gleicher wirksamer Regelkonfiguration und gleicher **Verhaltensversion**. Jede Änderung, die Treffer, Messung, Evidenz oder Normalisierung fachlich beeinflussen kann, erhöht die Verhaltensversion der Regel. Doku- oder reine Strukturänderungen tun das nicht. Bei geänderter Verhaltensversion oder Konfiguration wird ein noch vorhandener Befund erneut offen, auch wenn der Code gleich blieb. Diese konservative Wiederprüfung empfehle ich ausdrücklich: Eine alte Freigabe darf kein neues Regelverhalten verdecken.

## Review-Entscheidungen und Zustände

Ein Befund ist `open`, bis ein Agent ihn strukturiert als `accepted` („hier in Ordnung“) oder `false-positive` („Regel trifft sachlich nicht“) meldet. Beide Entscheidungen unterdrücken nur den unveränderten, gleich bewerteten Fall und bleiben für spätere Auswertungen unterscheidbar. Es gibt weder „später beheben“ noch eine Wiedervorlage nach Zeit. Keine Antwort lässt den Befund offen. „Behoben“ ist kein manuell setzbares Urteil: Ein vollständiger späterer Lauf, in dem die Regel den Befund nicht mehr findet, markiert ihn `resolved`. Taucht er unter demselben Zuordnungsschlüssel wieder auf, ist er erneut `open`.

Eine Änderung des Fingerprints, der wirksamen Konfiguration oder der Verhaltensversion öffnet ein akzeptiertes Finding wieder und bewahrt die alte Entscheidung im Verlauf. Ein Report-Verdikt zu einem inzwischen veralteten Scan wird abgewiesen; der Agent muss erneut scannen. Die Entscheidung benötigt Finding-ID, angezeigten Fingerprint und das Urteil. Freitext ist für die Entscheidung nicht nötig.

## Speicher und Berichte

Das versionierte `storageDirectory` ist die Quelle für den Verlauf; in der Startkonfiguration heißt es `.ainetreview`. Jeder vollständige Scan wird als unveränderliches Paket unter `<storageDirectory>/runs/<run-id>/` gespeichert. Die Run-ID besteht aus UTC-Zeitstempel und zufälligem Suffix. `manifest.json` enthält Regelversionen, Parameter, Trefferzahlen und Finding-IDs; `findings/*.json` enthält Ereignisse für neue, erneut geöffnete oder verschwundene Befunde. Ein bisheriges Finding wird nur dann als verschwunden gewertet, wenn seine Regel im vollständigen Lauf aktiv war. Roh-Snapshots liegen als Text im selben Paket und werden nur bei neuem oder erneut geöffnetem Befund geschrieben. Unveränderte Scans duplizieren weder Finding-Ereignis noch Snapshot. Ein Review-Urteil ist ein eigenes unveränderliches JSON-Ereignis unter `<storageDirectory>/decisions/<ereignis-id>.json`. Ereignisse enthalten Schema-Version, Finding-ID, Zuordnungsschlüssel, Messwerte und den Bezug zum vorherigen Zustand. Der Store lädt diese Textdateien einmal und baut einen In-Memory-Index nach Zuordnungsschlüssel auf. Widersprüchliche parallele Entscheidungsverläufe werden als Speicherkonflikt gemeldet und nie automatisch aufgelöst. SQLite wird nicht eingesetzt.

Der Runner schreibt Berichte unter `<outputDirectory>/<run-id>/index.md` und `<outputDirectory>/<run-id>/rules/<regel-id>.md`. Ein Regelbericht enthält alle aktuell offenen und erneut geöffneten Findings dieser Regel, auch wenn es vierzig sind, mit ID, Quellort, Messwerten, Begründung, Evidenz und Verweis auf Snapshots. Unverändert akzeptierte Fälle erscheinen nur als Anzahl in der Zusammenfassung. Der Bericht enthält den Fingerprint für das spätere strukturierte Urteil. Berichte sind generiert und werden nicht in Git versioniert; der versionierte Speicher erhält den Verlauf und die Laufzählungen. Ein erneuter Scan desselben unveränderten Falls erzeugt keinen neuen „Fall“ und keine neue Review-Episode.

Speicheränderungen und Berichte werden nur für einen vollständigen, konsistenten Scan finalisiert. Der Runner erkennt Änderungen der analysierten Quelldateien während des Laufs und meldet den Lauf als fehlgeschlagen; „0 Findings“ darf keinen Lade-, Regel- oder Speicherfehler verdecken. Er erstellt Bericht und Run-Paket zunächst in temporären Verzeichnissen, veröffentlicht zuerst den Bericht und zuletzt das vollständige Run-Paket durch Umbenennen. Nur veröffentlichte Pakete zählen als Zustand; ein verwaister Bericht nach einem Fehler ist kein gültiger Lauf. Entscheidungsereignisse werden ebenfalls erst nach vollständigem Schreiben veröffentlicht. Schreibfehler lassen den Lauf scheitern. Ein exklusiver Repository-Lock verhindert konkurrierende Schreibvorgänge. Der frühe Infrastruktur-Slice darf einen `NoOpFindingStore` mit sichtbar `storage=disabled` nutzen; er erfüllt das Produkt-DoD nicht und darf kein gespeichertes Urteil bestätigen.

Spätere Auswertungen können getrennt zählen: Treffer pro vollständig gespeichertem Lauf, verschiedene Finding-IDs, neue Review-Episoden, `accepted`, `false-positive` und durch erneuten Scan behobene Befunde. Der Bau eines Statistik-Dashboards gehört nicht zum ersten Stand.

## MCP und CLI

`start_review(configPath)` hat genau einen Parameter und liefert sofort ein `operationToken`. `get_review_status(operationToken)` liefert `running`, `completed`, `failed` oder `unknown`; bei Erfolg nur repo-relative Berichtspfade, Zählwerte und Vollständigkeit. Polling startet keinen neuen Lauf. Ein zweiter Start derselben Konfiguration während eines aktiven Laufs liefert dessen Token; ein abweichender Start für dasselbe Repository meldet `busy`. Tokens gelten bis zum Ende des MCP-Prozesses, nach Neustart sind sie `unknown`. Der Server lebt während der MCP-Sitzung und beendet laufende Aufgaben beim Herunterfahren sauber. Logs verwenden nicht `stdout`.

`report_review(configPath, findingId, fingerprint, verdict)` akzeptiert ausschließlich `accepted` oder `false-positive`. Der absolute Konfigurationspfad identifiziert das Repository auch nach einem Server-Neustart. Die Funktion prüft gespeicherten aktuellen Befund, Fingerprint, Regelversion, Konfiguration und unveränderte Quelldateien; bei Abweichung fordert sie einen neuen Scan. Nur eine dauerhaft geschriebene Entscheidung wird als Erfolg bestätigt.

`ainetreview review` führt dieselbe Analyse synchron aus und erstellt dieselben Berichte und Speicherbeobachtungen. Die CLI nimmt zunächst keine Review-Entscheidungen an. Findings ergeben Exit-Code 0; ungültige Eingaben, unvollständige Analyse oder Speicherfehler einen Fehlercode. Die CLI und MCP verwenden dieselben Regeln, Fingerprints und Berichtsgeneratoren.

## Umsetzung und Definition of Done

1. Neues Repository und Projektstruktur anlegen; Host, Konfiguration, Registry, gemeinsamem Runner und Kataloggenerator bauen. Der `NoOpFindingStore` ist nur für diesen technischen Zwischenschritt zulässig.
2. Die erste Regel mit gezielten Test-Solutions implementieren; Roh-Snapshots, Fingerprint und strukturierte Finding-Entwürfe anbinden.
3. JSON-Store, Zustandsabgleich, Markdown-Berichte, MCP-Reporting und CLI-Berichtslauf fertigstellen.
4. End-to-End-Tests, Dogfooding, Dokumentation und CI abschließen.

**DoD für den ersten nutzbaren Stand:**

- MCP startet und pollt lange Läufe ohne Tool-Timeout; CLI erzeugt denselben Befundbestand.
- Die Startregel ist über die Konfiguration steuerbar und erzeugt nachvollziehbare Berichte mit einer stabilen ID und einem repo-relativen Pfad je Finding.
- `accepted` und `false-positive` werden nachweisbar gespeichert; unveränderte Fälle erscheinen beim nächsten Lauf nicht erneut als offene Arbeit.
- Fachliche Codeänderung, wirksame Konfigurationsänderung und neue Verhaltensversion öffnen einen weiterhin vorhandenen Fall erneut; reine Formatierung und sicher erkannte lokale Umbenennung tun das nicht.
- Ein verschwundener Befund gilt erst nach vollständigem Scan als behoben. Fehlgeschlagene und veraltete Läufe verändern den gültigen Review-Zustand nicht.
- Testfälle decken mindestens eine lange lineare Mapper-Methode ohne Treffer, eine verschachtelte Methode mit akzeptiertem Finding und späterem Drift, mehrere Findings derselben Regel, Umbenennung/Verschiebung, Stale-Verdikt, Server-Neustart, fehlerhafte Solution und 180.000 Zeilen in einer repräsentativen Test-Solution ab.
- Generierte Regelreferenz und Konfigurationsvorlage stimmen mit den registrierten Regeln überein; Build, Tests und CI sind grün.
- Der echte Store ist aktiv. Ein Lauf mit `storage=disabled` erfüllt dieses DoD ausdrücklich nicht.

Die konkrete Implementierung darf Klassen und interne Algorithmen anders zuschneiden, solange diese Verträge und Abnahmekriterien erhalten bleiben.
