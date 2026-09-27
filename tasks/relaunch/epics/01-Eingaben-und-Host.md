# Epic 1 – Eingaben und Host-Verträge

Dieses Epic ist der verbindliche Vertrag für Konfiguration, CLI, MCP, Pfade und Parallelität. Ziele und DoD stehen im [Konzept](../AiNetReview-Konzept.md); Finding- und Speicherbegriffe stehen in Epic 2 und 3.

## Konfiguration und Projektwurzel

`ainetreview.json` liegt unmittelbar in der Projektwurzel. Beide Analysezugänge lesen ausschließlich diese Datei. `configPath` und `--config` müssen absolute Pfade auf eine Datei mit genau diesem Namen sein. Die Projektwurzel ist ihr Elternverzeichnis; ein Git-Repository ist dafür nicht erforderlich. Ein zweiter Konfigurationsmodus und direkte Rule-Overrides in der CLI existieren nicht.

Schema-Version 1 hat genau diese Felder:

```json
{
  "schemaVersion": 1,
  "solution": "Project.slnx",
  "outputDirectory": "audit-reporting",
  "storageDirectory": ".ainetreview",
  "rules": {
    "template-noop": {}
  }
}
```

`schemaVersion` ist die Zahl `1`; alle drei Pfade sind nichtleere Strings. `rules` ist ein nichtleeres Objekt. Seine Keys müssen registrierte Regel-IDs sein. Jede Regelkonfiguration ist ein Objekt mit ausschließlich den im Regeldeskriptor benannten Feldern; fehlende Felder erhalten die dort definierten Defaults. Die einzige produktive Startregel `template-noop` hat keine Parameter und daher nur `{}` als gültigen Wert. Unbekannte oder doppelte JSON-Keys, unbekannte Regel-IDs, falsche Typen und unbekannte Schemaversionen sind `INVALID_INPUT`. Die wirksame Konfiguration umfasst die normalisierten Pfade sowie für jede aktive Regel das aus Defaults und JSON-Feldern gebildete, nach Key sortierte Optionsobjekt. MCP und CLI erzeugen aus derselben Datei dieselbe wirksame Konfiguration.

Interne Pfade verwenden `/`, beginnen nicht mit einem Laufwerksbuchstaben oder Separator und enthalten keine `..`-Segmente. Die Auflösung einschließlich Symlinks/Junctions muss innerhalb der Projektwurzel bleiben. `solution` zeigt auf eine vorhandene `.sln` oder `.slnx`. Ausgabe- und Speicherverzeichnis sind verschieden, dürfen weder ineinander liegen noch C#-Quellen der Solution enthalten und werden bei Bedarf angelegt. Eine eingebundene Quelldatei außerhalb der Projektwurzel macht den Scan unvollständig und damit fehlerhaft. Vor dem Regellauf muss jedes analysierte C#-Projekt eine Compilation ohne Roslyn-Diagnosen mit `Severity.Error` liefern; Lade-, Restore- und Kompilationsfehler ergeben `ANALYSIS_FAILED`. Persistierte Pfade und Tool-Ergebnisse verwenden ausschließlich repo-relative `/`-Pfade; lokale absolute Pfade erscheinen nur in Eingabeparametern und internen Diagnosen.

Git ist keine Laufzeitabhängigkeit. Das Werkzeug ruft `git` nicht auf und verlangt weder `.git` noch `.gitignore`. Im neu angelegten AiNetReview-Repository ignoriert `.gitignore` den Ausgabeordner und lässt den Speicherordner versionieren. Für analysierte Repositories wird dieselbe Einstellung dokumentiert, aber nicht erzwungen. So funktionieren auch CI-Artefakte und temporäre Testprojekte ohne Git.

## CLI

- `ainetreview mcp` startet den lokalen MCP-Server über stdin/stdout; der Befehl nimmt keine Optionen an. Protokollausgaben gehen ausschließlich auf stdout, Diagnosen auf stderr.
- `ainetreview review --config <absoluter-pfad>` startet einen Review synchron. `--config` ist der einzige Pflichtparameter; weitere Analyseparameter werden abgewiesen.
- `ainetreview catalog --repo <absoluter-pfad>` erzeugt unter dieser Projektwurzel `Docs/ainetreview-rules.md` und `ainetreview.example.json` aus der Registry. `Docs` wird angelegt. Die beiden als generiert gekennzeichneten Dateien werden deterministisch ersetzt; `ainetreview.json` wird nie verändert. Die Beispiel-JSON enthält alle registrierten Regeln mit Defaults, `outputDirectory: "audit-reporting"`, `storageDirectory: ".ainetreview"` und `solution: ""` als vor Nutzung auszufüllendes Feld. Git ist auch hierfür nicht nötig.

`review` schreibt bei Erfolg genau eine kompakte JSON-Zeile auf stdout: `{"status":"completed","runId":"...","indexPath":"audit-reporting/<run-id>/index.md","counts":{...}}`. Fehler schreiben genau eine JSON-Zeile `{"code":"...","message":"..."}` auf stderr; keine halben Ergebnisse auf stdout. Progress und Logs gehen nur auf stderr. Exit-Codes: `0` vollständiger Lauf auch mit Findings, `2` Eingabe-/Konfigurationsfehler, `3` Analyse-/Roslyn-Fehler oder geänderte Quellen, `4` Storage-/Schreibfehler oder Speicherkonflikt, `5` belegter Repository-Lock, `130` Abbruch durch Nutzer. `catalog` verwendet `0`, `2` und `4` entsprechend.

## MCP-Tools

Jedes Tool liefert genau einen Text-Content-Block mit einem kompakten JSON-Objekt. `structuredContent` wird nicht gesetzt. Werkzeugfehler setzen `isError: true` und liefern `{"code":"...","message":"..."}`. Protokollfehler und unbekannte Tools behandelt das MCP-SDK. Die Fehlercodes sind `INVALID_INPUT`, `BUSY`, `ANALYSIS_FAILED`, `STORAGE_FAILED`, `STORAGE_CONFLICT`, `STALE_FINDING` und `UNKNOWN_FINDING`. Fehlermeldungen enthalten keine absoluten Quellpfade.

`start_review(configPath)` validiert den Eingabevertrag und reserviert den Repository-Lock, bevor es `{"operationToken":"<32 hex>"}` liefert. Solution-Laden und Analyse laufen danach im Serverprozess. Ein zweiter Aufruf mit derselben wirksamen Konfiguration während dieses Laufs liefert denselben Token. Ein anderer Aufruf für dieselbe Projektwurzel liefert `BUSY`. Nach einem Server-Neustart sind alte Tokens unbekannt.

`get_review_status(operationToken)` liefert eines dieser Objekte:

```text
{"status":"running","completedRules":0,"totalRules":1}
{"status":"completed","runId":"20260927T163802Z-a1b2c3d4","indexPath":"audit-reporting/20260927T163802Z-a1b2c3d4/index.md","ruleReports":["audit-reporting/20260927T163802Z-a1b2c3d4/rules/template-noop.md"],"counts":{"detected":0,"open":0,"new":0,"updated":0,"reopened":0,"accepted":0,"falsePositive":0,"resolved":0}}
{"status":"failed","code":"ANALYSIS_FAILED","message":"Solution konnte nicht vollständig geladen werden."}
{"status":"unknown"}
```

Die vier Zeilen sind vier alternative Antworten, kein einzelnes JSON-Dokument. `completedRules` zählt vollständig abgeschlossene Regeln; ein Prozentwert wird nicht behauptet. `completed` gibt es nur für vollständig veröffentlichte Berichte und Store-Daten. `counts` ist in Epic 3 definiert. Der Server behält Status und Tokens bis zum Prozessende; `unknown` ist ein normales Statusresultat, kein Tool-Fehler.

`report_review(configPath, findingId, fingerprint, verdict)` akzeptiert als `verdict` nur `accepted` oder `false-positive`. Erfolg liefert `{"decisionId":"D-<32 hex>","findingId":"F-<32 hex>","status":"accepted"}` beziehungsweise den anderen Status. Ein unbekanntes Finding ergibt `UNKNOWN_FINDING`, ein inzwischen geänderter Code- oder Konfigurationsstand `STALE_FINDING`, ein belegter Lock `BUSY`. Erfolg wird erst nach dauerhaft veröffentlichter Entscheidung gemeldet. Der Server lädt den Zustand bei jedem Aufruf frisch; ein vorheriger Operation-Token ist nicht nötig.

Für die Stale-Prüfung muss das Finding in den `observations` des neuesten vollständigen Runs stehen. `report_review` vergleicht den übergebenen Fingerprint mit dieser Beobachtung, `solution` aus deren Manifest sowie `behaviorVersion` und `effectiveOptions` der betroffenen Regel mit der aktuellen JSON. Es berechnet außerdem SHA-256 über jede in `sourceFiles` gespeicherte Quelldatei und vergleicht die Byte-Hashes. Eine deaktivierte Regel, ein inzwischen `resolved` stehendes Finding oder ein neuester Run ohne Beobachtung dieses Findings ergibt `STALE_FINDING`. Eine Änderung nur von `outputDirectory` ändert die fachliche Entscheidung nicht; ein anderes `storageDirectory` ist ein anderer Verlauf. Jede Veränderung einer relevanten Quelldatei seit dem Scan verlangt einen neuen Lauf, auch wenn sie außerhalb des ursächlichen Ausschnitts liegt. Zeitstempel allein genügen nicht.

## Lock und Abbruch

Der exklusive Lock liegt unter `<storageDirectory>/.lock`. Der Prozess öffnet ihn mit `FileShare.None` und hält den Handle während des gesamten Scans einschließlich Veröffentlichung sowie während einer einzelnen Review-Entscheidung. Bei belegtem Handle antwortet er sofort mit `BUSY`; es gibt kein Warten. Der Dateiname darf nach dem Schließen bestehen bleiben: Entscheidend ist der OS-Handle, der auch nach einem Prozessabsturz freigegeben wird. Ein im Leerlauf befindlicher MCP-Server hält keinen Lock; die CLI darf dann laufen. Abbruch während eines Scans veröffentlicht weder einen neuen Run noch ein positives Tool-Ergebnis.
