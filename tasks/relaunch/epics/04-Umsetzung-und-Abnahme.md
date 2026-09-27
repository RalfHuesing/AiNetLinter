# Epic 4 – Umsetzung und Abnahme

Dieses Epic ordnet die Implementierung und definiert die prüfbaren Abnahmekriterien. Die Verträge der [Eingaben](01-Eingaben-und-Host.md), [Findings](02-Regel-und-Findings.md) und [Speicherung](03-Storage-und-Berichte.md) sind verbindlich; Tests dürfen keine davon still verändern.

## Reihenfolge

1. Neues Repository mit `AiNetReview.slnx`, `src/AiNetReview.Core`, ausführbarem `src/AiNetReview`, `tests/AiNetReview.Tests` und `tests/AiNetReview.IntegrationTests` erstellen. .NET-10-SDK pinnen, Nullable und Warnungen als Fehler aktivieren, Build und Tests in CI ausführen. Die README erklärt MCP-Start, CLI-Aufruf und versionierbare Daten.
2. Gemeinsame validierte Konfiguration, Roslyn-Solution-Lader, statische Regelregistry und Kataloggenerator implementieren. Ein `NoOpFindingStore` darf für diesen Zwischenschritt verwendet werden, muss `storage=disabled` melden und darf keine Entscheidung als gespeichert bestätigen.
3. Die eine Startregel samt Score-Evidenz, Identität, Fingerprint, Source-Snapshot und Zustandsautomat implementieren.
4. Vollständigen JSON-Store, atomare Veröffentlichung, Markdown-Berichte, CLI und drei MCP-Tools anschließen. Ab hier ist `NoOpFindingStore` nur noch Test-Double.
5. Integration, Dogfooding und Lasttest durchführen. Das bisherige AiNetLinter wird durch diese Arbeit weder deaktiviert noch archiviert.

Root-Namespaces sind `AiNetReview.Core` für Konfiguration, Roslyn, Regeln, Fingerprints, Zustände, Storage und Berichtsdaten sowie `AiNetReview` für CLI- und MCP-Adapter. Unter `AiNetReview.Core.Rules.MaxCognitiveComplexity` liegen ausschließlich die Dateien der Startregel. Der Core hängt nicht von CLI oder MCP ab; beide Adapter rufen denselben Runner auf. Test-Namespaces entsprechen ihren Projekt-Roots. Die Regelregistrierung und der Kataloggenerator liegen im Core.

Als geprüfte Vorlagen aus AiNetLinter dienen [SourceFileCatalogLoader](../../../src/AiNetLinter/Baseline/SourceFileCatalogLoader.cs) für MSBuild/Roslyn, [LongRunningToolCallStore](../../../src/AiNetLinter/Mcp/LongRunningToolCallStore.cs) für Polling, [DuplicateMethodCollector](../../../src/AiNetLinter/Core/DuplicateDetection/DuplicateMethodCollector.cs) für Syntax-Traversierung und [RuleRegistry](../../../src/AiNetLinter/Core/RuleRegistry.cs) für statische Registrierung. Die Regelvorlagen stehen in Epic 2. Es gibt keinen DI-Container, kein dynamisches Assembly-Laden, keine dynamische Regelsuche und keinen zusätzlichen Daemon. Die Vorlagen werden fachlich angepasst, nicht 1:1 kopiert.

## Automatische Tests

Unit-Tests decken die Regel mit Grenzwerten `15` und `16`, langem linearem Mapper ohne Befund, verschachtelter Methode, Switch-Dispatcher, Null-Coalescing-Initialisierer, jeder definierten Art generierter Datei und mehreren Methoden in einer Datei ab. Evidenzbeiträge müssen sich zum Score summieren. Die Tests decken zudem generische Methoden, explizite Interface-Implementierungen, partielle Typen und doppelte Zuordnungsschlüssel ab.

Fingerprint-Tests prüfen gleiche Hashes nach reiner Formatierung, Kommentaränderung und sicherer Umbenennung von Parametern/lokalen Variablen; unterschiedliche Hashes nach fachlicher Änderung, Literaländerung, `nameof`-Umbenennung und `CallerArgumentExpression`-Fall. Shadowing, Lambda-/lokale Funktionsparameter, Dekonstruktion, `out`- und Pattern-Variablen sowie der konservative Modus bei unklarer Bindung gehören dazu. Snapshots müssen vollständigen Quelltext mit UTF-8/LF enthalten.

Zustandstests prüfen jede Zeile der Tabelle in Epic 2, identische und korrigierte Urteile, unterdrückte akzeptierte Fälle, Konfigurations- und Verhaltensversionswechsel, verschwundene und wiederkehrende Methoden sowie deaktivierte Regeln. Mindestens zwei Findings derselben Regel in **verschiedenen** Methoden müssen unabhängig behandelt werden. Ein abgebrochener oder inkonsistenter Scan darf keinen Run veröffentlichen. Beschädigte JSON-Dateien, doppelte Elternereignisse und zwei IDs für denselben Schlüssel müssen deterministisch fehlschlagen.

Integrationstests starten den echten CLI- und MCP-Prozess gegen kleine temporäre C#-Solutions und vergleichen Finding-IDs, Zustände, Counts und Berichtsinhalte. Sie prüfen Polling ohne Tool-Timeout, MCP-Neustart mit unbekanntem Token aber weiter nutzbarem Storage, konkurrierenden CLI/MCP-Aufruf, Stale-Verdikt nach Dateiveränderung, Lock-Freigabe nach Prozessabsturz, ungültige Solution, fehlende NuGet-Referenzen, Pfadflucht, Git-losen Ordner sowie `catalog` mit fehlendem `Docs`-Ordner. Für ungültige Quellen darf kein „0 Findings“-Erfolg entstehen.

## Lasttest

Ein deterministischer Generator erzeugt zur Testlaufzeit in einem temporären Ordner eine C#-Solution mit mindestens 180.000 belegten Codezeilen, mehreren Projekten und einer Mischung aus linearen und komplexen Methoden. Als belegt zählt eine physische Zeile mit mindestens einem C#-Token außerhalb von Kommentartrivia; Leerzeilen und reine Kommentarzeilen zählen nicht. Die generierte Solution wird nicht eingecheckt. Der Lasttest läuft getrennt von der regulären PR-CI als Kategorie `Performance` und muss vor dem ersten Release auf einem Windows-Host mit mindestens 4 vCPU und 16 GiB RAM bestanden sein. Grenze: vollständiger Review in höchstens 10 Minuten und Peak-Private-Bytes des Prozesses höchstens 6 GiB. Gemessen werden Laufzeit, Peak-Speicher, Zahl analysierter Methoden und Zahl der Findings; der Test scheitert auch bei unvollständigem Roslyn-Load oder fehlendem Run-Manifest. Reguläre CI führt Build, Unit- und kleine Integrationstests bei jedem Commit aus.

## Definition of Done

Der erste nutzbare Stand ist fertig, wenn:

- die einzige Regel `max-cognitive-complexity` mit `maxScore = 15` konfigurierbar ist und bei Score `> 15` nachvollziehbare Findings mit stabilen IDs erzeugt;
- MCP und CLI dieselbe `ainetreview.json` verwenden und bei gleichem Quellstand dieselben Findings liefern;
- `accepted` und `false-positive` dauerhaft gespeichert, bei unverändertem Code unterdrückt und bei fachlicher Änderung, wirksamer Konfigurationsänderung oder neuer Verhaltensversion erneut geöffnet werden;
- reines Formatieren und sichere lokale Umbenennung nicht erneut öffnen, die in Epic 2 benannten konservativen Fälle aber schon;
- vollständige Run-Pakete, Snapshots, Berichte und Entscheidungsereignisse exakt den Epic-3-Verträgen entsprechen und fehlgeschlagene Läufe keinen gültigen Zustand verändern;
- alle oben genannten Unit-, Integrations- und Lasttests grün sind, das Werkzeug seine eigene Solution analysiert und `catalog` eine zur Registry passende Regelreferenz und JSON-Vorlage erzeugt;
- der echte Store aktiv ist. `storage=disabled` erfüllt dieses DoD nie.

Die Dokumentation gilt als implementierbar, wenn jedes Feld, jeder Zustand und jeder Fehlerfall aus den drei Vertrags-Epics in mindestens einem Testfall oder einer eindeutigen Validierungsregel vorkommt. Bei Widerspruch wird zuerst der Vertrag korrigiert und erst dann Code geschrieben.
