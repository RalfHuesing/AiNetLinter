# Startarchitektur – Arbeitsentwurf

Dieser Entwurf konkretisiert die [Roadmap](Relaunch-Roadmap.md) für das neue Repository. `AiNetReview` ist hier ein Arbeitsname, noch keine endgültige Namensentscheidung. Regeln und Speicherformat werden später festgelegt.

## Projekte und Verantwortung

```text
AiNetReview.slnx
src/AiNetReview.Core/              Konfiguration, Roslyn-Analyse, Regeln, Findings, Berichte, Storage-Vertrag
src/AiNetReview/                   ausführbarer Host mit MCP- und CLI-Modus
tests/AiNetReview.Tests/           schnelle Tests für Verträge, Analyse und Bericht
tests/AiNetReview.IntegrationTests/ Host-Prozess und kleine .slnx-Testprojekte
```

Namensräume orientieren sich an den Aufgaben: `AiNetReview.Configuration`, `.Analysis`, `.Rules`, `.Findings`, `.Reporting`, `.Storage` sowie im Host `AiNetReview.Mcp`, `.Mcp.Operations` und `.Cli`. Beide Modi rufen denselben Core-Runner auf. Erst konkrete gemeinsame Logik rechtfertigt weitere Projekte oder Abstraktionen.

Zur Repo-Grundausstattung gehören ein festgelegtes .NET-10-SDK (`global.json`), gemeinsame Compiler-Einstellungen, `.gitignore` für Build- und generierte Review-Ausgaben, README, eine neue `AGENTS.md` ohne das alte harte Linter-Gate sowie ein Build-/Testlauf in CI. Das neue Projekt übernimmt die alten Agentenregeln nicht unverändert.

Dogfooding: Das Werkzeug analysiert später seine eigene Solution mit einer eigenen Review-Konfiguration. Gezielte kleine Testprojekte liefern bekannte positive und negative Befunde; ein Selbstscan allein beweist keine Regelwirkung. Audit-Findings bleiben Hinweise und werden nicht zum Build-Gate.

## Konfiguration und MCP-Vertrag

Ein fest benanntes JSON liegt im analysierten Repository; als Arbeitsname dient `ainetreview.json`. `start_review(configPath)` erhält **nur** den absoluten Pfad dieser Datei. Die Konfiguration enthält eine Schema-Version, den Pfad zu einer vorhandenen `.sln`/`.slnx`, aktivierte Regeln samt Parametern, Ausgabeordner und Speicherordner. Diese Pfade sind relativ zum Repository; alle dauerhaft ausgegebenen Quellorte ebenfalls. Vor dem Start werden Dateiname, Schema, Pfade und Regelkonfiguration geprüft. Ausgabe und Speicherung liegen im analysierten Repository und dürfen nicht als C#-Quellen mitanalysiert werden. `rules: []` darf beim Aufbau der Infrastruktur vorkommen, darf aber keinen scheinbar erfolgreichen Qualitätsbefund erzeugen.

Der absolute Konfigurationspfad dient nur zur Lokalisierung beim Aufruf. Der Host löst die repo-relativen JSON-Pfade intern gegen das Verzeichnis der Konfigurationsdatei auf. Er speichert und berichtet diese absoluten Arbeitsverzeichnisse nicht.

`start_review` gibt schnell ein `operationToken` zurück. `get_review_status(operationToken)` meldet `running`, `completed` oder `failed`; bei Abschluss nennt es kompakt Berichtspfade, Zählwerte und Analyse-Vollständigkeit, nicht alle Findings im MCP-Ergebnis. Wiederholtes Polling startet die Analyse nicht erneut. Ein zweiter Start für dieselbe Konfiguration darf keine konkurrierende Analyse und keine kollidierenden Ausgabedateien erzeugen.

Es gibt keinen zusätzlichen Daemon. Der MCP-Server selbst bleibt während der Client-Sitzung am Leben und hält die laufende Aufgabe im Prozess. Deshalb endet zwar der **Toolaufruf** schnell, der **Serverprozess** aber erst nach der Sitzung. Ein Server-Neustart verliert zunächst laufende Tokens; ihr Status ist danach `unknown/expired`. Dauerhafte Jobs wären eine spätere eigene Funktion. Beim Beenden werden laufende Aufgaben sauber abgebrochen. MCP-Protokollausgaben gehen nicht mit Logs auf `stdout` durcheinander.

Berichte werden erst nach vollständiger Erzeugung als fertig gemeldet; ein fehlerhafter oder unvollständiger Solution-Load darf nicht als „0 Findings“ erscheinen. Die Analyse soll einen konsistenten Quellstand verwenden oder eine zwischenzeitliche Änderung erkennen und den Lauf als unvollständig markieren.

Der Storage-Vertrag wird beim Aufbau angelegt. Seine anfängliche Dummy-Implementierung meldet ausdrücklich `storage=disabled`; eine Review-Entscheidung darf dadurch nicht als gespeichert bestätigt werden. Erst ein späterer vollständiger Durchlauf mit echter Speicherung kann akzeptierte Findings über mehrere Läufe hinweg ausblenden.

## Regelmodell und generischer Ablauf

Ein statisches `RuleRegistry` führt alle verfügbaren Regeln mit stabiler Regel-ID auf. Eine Regel kann intern aus mehreren Klassen und Dateien bestehen; nur ihre Registrierung und ihre eigenen Dateien sind regelbezogen. Der Runner lädt die Solution einmal und führt die aktivierten Regeln nach demselben Vertrag aus. Keine Schalterketten für einzelne Regeln in Konfiguration, Bericht, CLI oder MCP.

Der Arbeitsvertrag ist sinngemäß `IReviewRule`: Metadaten und Konfigurationsbeschreibung bereitstellen sowie `ExecuteAsync(ReviewContext, RuleOptions)` ausführen. `ReviewContext` enthält die Roslyn-`Solution`, Abbruchsignal und gemeinsam nutzbare, teure Analysezugriffe. Die Regeln wählen ihre Suchräume selbst; sie sollen die Solution nicht jeweils neu laden oder identische Syntax- und Semantikdaten ohne Grund neu aufbauen. Der Runner führt sie anfangs nacheinander aus und kann Fortschritt pro Regel melden.

Das `RuleResult` enthält strukturierte Finding-Entwürfe: Regel-ID, betroffene Einheit, repo-relativen Quellort, Grund und Messwerte, für das Urteil nötige Evidenz, relevante Quellcode-Snapshots und Material für den Änderungsvergleich. Die Regel bestimmt, **welcher** Code ursächlich ist. Das gemeinsame Finding-Modell gibt vor, **wie** diese Daten an Bericht und Storage gehen. Die Regel erzeugt weder eigene Markdown-Dateien noch Finding-IDs oder Storage-Einträge. Erst der generische Ablauf gleicht Befunde mit früheren Entscheidungen ab, vergibt IDs, schreibt Berichte und speichert Verlauf. Regelbezogene Detailfelder sind möglich, ohne das gemeinsame Mindestformat zu ersetzen. Fehler einer Regel machen den Lauf unvollständig und werden nicht als leere Trefferliste behandelt.

Eine zentrale Fingerprint-Komponente berechnet Hashes aus dem von der Regel gelieferten Ausschnitt und einer gewählten Normalisierung. Die Regel wählt die Vergleichsgrenze, etwa eine vollständige Methode, und die passende Normalisierung; ein einziger globaler Text-Hash wäre für alle Regeln zu grob. Der rohe Quellcode bleibt zusätzlich als Snapshot erhalten. Die Gültigkeit einer früheren Entscheidung muss auch zur wirksamen Regelkonfiguration und zur Regelversion passen. Finding-Identität und Änderungs-Fingerprint sind verschiedene Größen.

Jede Regel beschreibt ihre Konfigurationsfelder samt Typ, Standardwert und Validierung sowie die für eine generierte Regelreferenz nötigen Angaben. Daraus entstehen eine JSON-Vorlage und eine Regelreferenz, ohne zentrale Pro-Regel-Logik. Die Vorlage überschreibt keine bearbeitete Repository-Konfiguration. Erläuternde Beispiele und fachliche Begründungen können in regel-eigenen Texten stehen; die Generierung sollte keine komplette Dokumentation aus Methodennamen erraten.

Der CLI-Modus erzeugt zunächst dieselben Berichte aus derselben validierten internen Anfrage. Er nimmt Solution, Regelauswahl, Ausgabe- und Speicherziel über direkte CLI-Parameter entgegen; ob `--config` zusätzlich möglich ist, bleibt offen. Der Storage-Abgleich gilt auch für CLI-Berichte, damit akzeptierte Findings nicht erneut als offen erscheinen. Die CLI meldet zunächst keine Review-Entscheidungen zurück. Ein Fehler bei Konfiguration oder Analyse führt zu einem Fehlerstatus, ein Finding allein nicht. CLI und MCP bekommen keine getrennten Regel- oder Berichtspfade.

## Nützliche Vorlagen aus AiNetLinter

Diese Dateien sind geprüfte Ausgangspunkte, keine 1:1-Übernahme des alten Produkts:

- [SourceFileCatalogLoader](../../src/AiNetLinter/Baseline/SourceFileCatalogLoader.cs): Registrierung von MSBuild und Laden einer Solution mit Roslyn. Die bestehende Sonderbehandlung für lokale BuildHosts braucht im neuen Repo eine eigene Prüfung.
- [AnalysisTargetResolver](../../src/AiNetLinter/Mcp/AnalysisTargetResolver.cs): Validierung und Kanonisierung absoluter Zieldateien; im neuen Tool auf JSON-Konfiguration und `.sln`/`.slnx` zuschneiden.
- [LongRunningToolCallStore](../../src/AiNetLinter/Mcp/LongRunningToolCallStore.cs) und [zugehörige Tests](../../src/AiNetLinter.FastTests/Mcp/LongRunningToolCallStoreTests.cs): Token, einmaliger Start, Polling und Begrenzung laufender Aufgaben. Im neuen Tool reichen zunächst zwei schmale MCP-Tools und ein einfacherer Operation-Store.
- [DuplicateMethodCollector](../../src/AiNetLinter/Core/DuplicateDetection/DuplicateMethodCollector.cs): Roslyn-Traversierung und Filter für generierten Code; nur passende Teile für spätere Regeln übernehmen.
- [PatternDetectScanner](../../src/AiNetLinter/Mcp/Tools/PatternDetect/PatternDetectScanner.cs): Trennung von Muster-Treffern und Prüfhinweisen. Der neue Finding-/Review-Vertrag ersetzt dessen Linter- und Score-Abhängigkeiten.
- [RuleRegistry](../../src/AiNetLinter/Core/RuleRegistry.cs): statische Liste mit Regel-IDs und Metadaten als Vorbild für zentrale Registrierung. Im neuen Werkzeug sollten Ausführung, Konfigurationsbeschreibung und Referenztext an der jeweiligen Regel zusammenlaufen.
- [ConfigLoader](../../src/AiNetLinter/Configuration/ConfigLoader.cs): vorhandener JSON-Ladepfad als Beispiel; die neue Version braucht feste Repo-Verankerung und Validierung der von Regeln beschriebenen Parameter.

## Vor dem ersten Code-Slice noch festlegen

- Arbeitsname des neuen Repositories und primäre MCP-Toolnamen.
- CLI-Parameteroberfläche; der feste JSON-Dateiname folgt dem gewählten Produktnamen.

Die ersten konkreten Regeln und das Speicherformat sind für das Grundgerüst nicht nötig. Sie werden am ersten fachlichen Vertical Slice festgelegt.
