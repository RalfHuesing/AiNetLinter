# Initialprompt: AiNetLinter MCP-UX-Audit

Du bist der Orchestrator für einen vollständigen MCP-UX-Audit des
AiNetLinter-MCP-Servers.

Nutze den Skill `$mcp-ux-audit` als verbindlichen Arbeitsrahmen. Du führst den
Audit autonom bis zur definierten Abschlussbedingung durch und darfst Ablauf,
Priorisierung, Anzahl der Iterationen sowie Hilfsdateien selbst wählen.

## Rollen und Ausführung

- Du bist der steuernde Orchestrator und arbeitest mit Terra.
- Audit-, Red-Test- und Implementierungs-Subagenten arbeiten mit Luna High.
- Instruiere Luna-Subagenten konkret, eng abgegrenzt und mit eindeutigem
  erwarteten Ergebnis.
- Arbeite seriell: Höchstens ein Subagent, Build, Test oder MCP-Prüflauf ist
  gleichzeitig aktiv.
- Subagenten liefern nur verdichtete Ergebnisse zurück; keine vollständigen
  MCP-Responses.
- Du darfst unter `tasks/audit/` beliebige Markdown-Dateien zur Steuerung
  erstellen.
- Verwende eindeutige, stabile Dateinamen und Befund-IDs, beispielsweise
  `A-001-...`; Dateien dürfen sich nie gegenseitig überschreiben.

## Ziel und Scope

Prüfe bestehende MCP-Tool-Funktionen aus Sicht eines konsumierenden
Coding-Agenten:

- Erfüllen Tools und ihre Responses ihr bestehendes Versprechen?
- Sind Tool-Schemas, Parameternamen, Fehler, Navigation, Chaining und
  Trunkierungs-Signale konsistent, verständlich und token-effizient?
- Entsteht hoher Tokenverbrauch durch handlungsrelevante Information oder durch
  vermeidbares Rauschen?

Denke im Produktkontext von AiNetLinter. Beurteile insbesondere, was ein Agent
ohne zusätzliche Dokumentation vernünftigerweise erwartet.

Baue keine neuen Features und führe keine Architektur-Refactorings ein. Behebe
nur klar abgegrenzte Qualitäts- oder Verhaltensfehler bestehender Funktionen.
Öffentliche Parameter dürfen nicht breaking umbenannt werden; Harmonisierung
ist nur rückwärtskompatibel zulässig.

## Arbeitsartefakte

Führe mindestens diese Dateien:

- `tasks/audit/STATUS.md`: Phasenstatus, nächste Aktion und offene
  Entscheidungen.
- `tasks/audit/FINDINGS.md`: Eindeutige Befund-ID, Schweregrad, Status
  (`open`, `testing`, `fixed`, `deferred`), betroffene Tools und knappe
  Begründung.
- `tasks/audit/RESOLVED.md`: Knappe, abstrahierte Zusammenfassung je behobenem
  Befund, inklusive Testreferenz. Diese Datei verhindert erneute
  Doppelmeldungen.

Halte Artefakte knapp. Speichere keine Rohantworten oder großen Logauszüge.

## Schutz externer Referenzassemblys

Externe Referenzassemblys dürfen ausschließlich als flüchtige Inputs für
MCP-Tool-Aufrufe verwendet werden. Die konkreten drei Pfade werden nur in der
auslösenden, nicht persistierten Nachricht als Referenzassembly A, B und C
übergeben.

Ihre Dateinamen, Pfade sowie alle daraus ableitbaren Inhalte -- insbesondere
Namespaces, Typnamen, Signaturen, Quellcode, dekompilierte Ausschnitte oder
Response-Ausschnitte -- dürfen niemals in Commits, Markdown-Dateien, Tests,
Testnamen, Logs, Diffs oder Abschlussantworten auftauchen.

Verwende in dauerhaften Artefakten ausschließlich neutrale Bezeichnungen wie
„externe Referenzassembly A“, „B“ und „C“.

Tests dürfen niemals von diesen lokal installierten Assemblys oder ihren Pfaden
abhängen. Reproduktionen müssen mit repo-eigenen Fixtures oder synthetischen
Testdaten erfolgen.

## Fix-Loop

Priorisiere Critical vor Major vor Minor.

Bearbeite zunächst alle umsetzbaren Critical- und Major-Befunde vollständig
per Red-Test-First. Minors werden währenddessen dokumentiert, aber nicht sofort
unterbrechend bearbeitet.

Für jeden umsetzbaren Critical- oder Major-Befund:

1. Befund abstrakt dokumentieren.
2. Einen gezielten Red-Test erstellen und dessen Fehlschlag nachweisen.
3. Den minimalen, architektonisch sauberen Fix implementieren.
4. Den gezielten Test grün nachweisen.
5. `FINDINGS.md` und `RESOLVED.md` aktualisieren.

Wenn alle Tool-Gruppen auditiert sowie alle Critical- und Major-Befunde
abgeschlossen sind, arbeite die dokumentierten Minors in einem abschließenden
Minor-Pass ab.

Behebe dabei grundsätzlich jeden Minor, sofern der Fix klar lokal und
risikoarm ist, ohne neues Feature, Architekturumbau oder Breaking Change
möglich ist und sich gezielt verifizieren lässt.

Wenn sich ein Minor als unverhältnismäßig, unklar oder scope-erweiternd
herausstellt, dokumentiere ihn als `deferred` mit kurzer Begründung und fahre
fort. Verbringe keine unverhältnismäßige Zeit mit kosmetischen Einzelheiten,
wenn kein klarer, lokal verifizierbarer Fix erkennbar ist.

Ein Minor wird nicht als erledigt markiert, nur weil er dokumentiert wurde. Er
ist entweder `fixed`, `deferred` oder weiterhin `open`.

Passe bei jedem tatsächlichen MCP-Vertrags- oder Verhaltensfix die betroffene
Dokumentation und, wenn sie betroffen ist, die README an. Dokumentiere nur
belegbares aktuelles Verhalten.

## Version und Verifikation

Die installierte AiNetLinter-Version entspricht nicht zwingend dem aktuellen
Quellcode. Verwende den MCP-Server daher für Navigation, Exploration und
zusätzliche Plausibilisierung, aber nicht als Nachweis, dass
Quellcodeänderungen bereits im installierten Server aktiv sind.

Erstelle keinen Release, kein Deployment und ändere keine installierte Version.

Nach allen Codeänderungen müssen diese Gates grün sein:

- `dotnet build`
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`
- `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`

Erstelle nur auftragsbezogene Commits, mit expliziten Pfaden und vorheriger
Diff-Prüfung. Commit-Nachrichten folgen den bestehenden deutschen
Conventional-Commit-Regeln.

## Abschlussbedingung

Beende erst, wenn:

- alle im Skill vorgesehenen Tool-Gruppen geprüft wurden;
- alle umsetzbaren Critical-, Major- und Minor-Befunde behoben und verifiziert
  sind;
- nur nachvollziehbar begründete `deferred`-Befunde verbleiben;
- die Abschluss-Gates grün sind; und
- ein abstrahierter Audit-Report vollständig ist.

Triff innerhalb dieser Leitplanken eigenständig Entscheidungen. Frage nur nach,
wenn eine Entscheidung einen Breaking Change, ein neues Feature, einen
Architekturumbau oder eine Ausweitung des Scopes erfordern würde.
