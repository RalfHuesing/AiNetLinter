# Startarchitektur – Arbeitsentwurf

Dieser Entwurf konkretisiert die [Roadmap](Relaunch-Roadmap.md) für das neue Repository. `AiNetReview` ist hier ein Arbeitsname, noch keine endgültige Namensentscheidung. Regeln und Speicherformat werden später festgelegt.

## Projekte und Verantwortung

```text
AiNetReview.slnx
src/AiNetReview.Core/              Konfiguration, Roslyn-Analyse, Regeln, Findings, Berichte, Storage-Vertrag
src/AiNetReview.Mcp/               lokaler MCP-Host, Tools, laufende Review-Operationen
tests/AiNetReview.Tests/           schnelle Tests für Verträge, Analyse und Bericht
tests/AiNetReview.IntegrationTests/ echter MCP-Prozess und kleine .slnx-Testprojekte
```

Namensräume orientieren sich an den Aufgaben: `AiNetReview.Configuration`, `.Analysis`, `.Rules`, `.Findings`, `.Reporting`, `.Storage` und `AiNetReview.Mcp.Operations`. Erst konkrete gemeinsame Logik rechtfertigt weitere Projekte oder Abstraktionen.

Zur Repo-Grundausstattung gehören ein festgelegtes .NET-10-SDK (`global.json`), gemeinsame Compiler-Einstellungen, `.gitignore` für Build- und generierte Review-Ausgaben, README, eine neue `AGENTS.md` ohne das alte harte Linter-Gate sowie ein Build-/Testlauf in CI. Das neue Projekt übernimmt die alten Agentenregeln nicht unverändert.

Dogfooding: Das Werkzeug analysiert später seine eigene Solution mit einer eigenen Review-Konfiguration. Gezielte kleine Testprojekte liefern bekannte positive und negative Befunde; ein Selbstscan allein beweist keine Regelwirkung. Audit-Findings bleiben Hinweise und werden nicht zum Build-Gate.

## Konfiguration und MCP-Vertrag

Ein fest benanntes JSON liegt im analysierten Repository; als Arbeitsname dient `ainetreview.json`. `start_review(configPath)` erhält **nur** den Pfad dieser Datei. Die Konfiguration enthält eine Schema-Version, den Pfad zu einer vorhandenen `.sln`/`.slnx`, aktivierte Regeln samt Parametern, Ausgabeordner und Speicherordner. Vor dem Start werden Dateiname, Schema, Pfade und Regelkonfiguration geprüft. Das Speicherverzeichnis liegt im analysierten Repository; Ausgabe und Speicherdateien dürfen nicht als C#-Quellen mitanalysiert werden. `rules: []` darf beim Aufbau der Infrastruktur vorkommen, darf aber keinen scheinbar erfolgreichen Qualitätsbefund erzeugen.

Der geforderte absolute Solution-Pfad in einer versionierten JSON ist geräteabhängig. Mein Vorschlag: Der MCP-Parameter ist absolut, Pfade **innerhalb** der JSON sind relativ zu ihr und werden vor dem Lauf in absolute Pfade aufgelöst; absolute Pfade können bei Bedarf zusätzlich erlaubt werden. Falls absolute Pfade in der JSON fest bleiben, sollte die Datei lokal erzeugt und nicht als gemeinsame Repo-Konfiguration verstanden werden. Das ist vor dem ersten festen Schema zu entscheiden.

`start_review` gibt schnell ein `operationToken` zurück. `get_review_status(operationToken)` meldet `running`, `completed` oder `failed`; bei Abschluss nennt es kompakt Berichtspfade, Zählwerte und Analyse-Vollständigkeit, nicht alle Findings im MCP-Ergebnis. Wiederholtes Polling startet die Analyse nicht erneut. Ein zweiter Start für dieselbe Konfiguration darf keine konkurrierende Analyse und keine kollidierenden Ausgabedateien erzeugen.

Es gibt keinen zusätzlichen Daemon. Der MCP-Server selbst bleibt während der Client-Sitzung am Leben und hält die laufende Aufgabe im Prozess. Deshalb endet zwar der **Toolaufruf** schnell, der **Serverprozess** aber erst nach der Sitzung. Ein Server-Neustart verliert zunächst laufende Tokens; ihr Status ist danach `unknown/expired`. Dauerhafte Jobs wären eine spätere eigene Funktion. Beim Beenden werden laufende Aufgaben sauber abgebrochen. MCP-Protokollausgaben gehen nicht mit Logs auf `stdout` durcheinander.

Berichte werden erst nach vollständiger Erzeugung als fertig gemeldet; ein fehlerhafter oder unvollständiger Solution-Load darf nicht als „0 Findings“ erscheinen. Die Analyse soll einen konsistenten Quellstand verwenden oder eine zwischenzeitliche Änderung erkennen und den Lauf als unvollständig markieren.

Der Storage-Vertrag wird beim Aufbau angelegt. Seine anfängliche Dummy-Implementierung meldet ausdrücklich `storage=disabled`; eine Review-Entscheidung darf dadurch nicht als gespeichert bestätigt werden. Erst ein späterer vollständiger Durchlauf mit echter Speicherung kann akzeptierte Findings über mehrere Läufe hinweg ausblenden.

## Nützliche Vorlagen aus AiNetLinter

Diese Dateien sind geprüfte Ausgangspunkte, keine 1:1-Übernahme des alten Produkts:

- [SourceFileCatalogLoader](../../src/AiNetLinter/Baseline/SourceFileCatalogLoader.cs): Registrierung von MSBuild und Laden einer Solution mit Roslyn. Die bestehende Sonderbehandlung für lokale BuildHosts braucht im neuen Repo eine eigene Prüfung.
- [AnalysisTargetResolver](../../src/AiNetLinter/Mcp/AnalysisTargetResolver.cs): Validierung und Kanonisierung absoluter Zieldateien; im neuen Tool auf JSON-Konfiguration und `.sln`/`.slnx` zuschneiden.
- [LongRunningToolCallStore](../../src/AiNetLinter/Mcp/LongRunningToolCallStore.cs) und [zugehörige Tests](../../src/AiNetLinter.FastTests/Mcp/LongRunningToolCallStoreTests.cs): Token, einmaliger Start, Polling und Begrenzung laufender Aufgaben. Im neuen Tool reichen zunächst zwei schmale MCP-Tools und ein einfacherer Operation-Store.
- [DuplicateMethodCollector](../../src/AiNetLinter/Core/DuplicateDetection/DuplicateMethodCollector.cs): Roslyn-Traversierung und Filter für generierten Code; nur passende Teile für spätere Regeln übernehmen.
- [PatternDetectScanner](../../src/AiNetLinter/Mcp/Tools/PatternDetect/PatternDetectScanner.cs): Trennung von Muster-Treffern und Prüfhinweisen. Der neue Finding-/Review-Vertrag ersetzt dessen Linter- und Score-Abhängigkeiten.

## Vor dem ersten Code-Slice noch festlegen

- Arbeitsname des neuen Repositories und primäre MCP-Toolnamen.
- Portable oder ausschließlich absolute Pfade in der JSON; der feste Dateiname folgt dem gewählten Produktnamen.

Die ersten konkreten Regeln und das Speicherformat sind für das Grundgerüst nicht nötig. Sie werden am ersten fachlichen Vertical Slice festgelegt.
