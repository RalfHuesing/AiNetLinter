# Epic 3 – Storage und Berichte

Dieses Epic definiert die versionierbaren Dateien, ihre JSON-Felder, die Zustandsableitung und die Markdown-Berichte. Zustände und Fingerprints sind in [Epic 2](02-Regel-und-Findings.md) definiert; CLI/MCP-Antworten in [Epic 1](01-Eingaben-und-Host.md).

## Grundform und IDs

Das `storageDirectory` enthält `runs/` und `decisions/`. Alle JSON-Dateien sind UTF-8 ohne BOM und mit LF; Felder stehen in der Reihenfolge der folgenden Schemabeispiele. Options- und Metrik-Maps sind alphabetisch nach Key sortiert. `rules` ist nach Regel-ID, `observations` nach Regel-ID und Finding-ID und `evidence` nach Pfad, Zeile, Label und Snippet sortiert. Unbekannte `schemaVersion`, fehlende oder zusätzliche Felder, falsche Typen, doppelte JSON-Keys oder IDs, kaputte Referenzen und nicht lesbare JSON-Dateien ergeben `STORAGE_FAILED`. Die erste Schema-Version ist die Zahl `1`. Alle persistierten Pfade sind relativ zur Projektwurzel und verwenden `/`.

Die folgenden Finding-Beispiele stammen aus der ausschließlich in Tests registrierten `fixture-finding`-Regel. Die produktive `template-noop`-Regel erzeugt leere `observations`, keine Finding- oder Entscheidungsdateien und Nullzählwerte. Das Schema und der Runner sind für beide identisch.

Eine Run-ID hat das Format `yyyyMMddTHHmmssZ-<8 kleine Hexzeichen>`. Bei Namenskollision wird eine neue Zufallskomponente gezogen. Finding-IDs sind `F-<32 kleine Hexzeichen>`, Entscheidungs-IDs `D-<32 kleine Hexzeichen>`; beide stammen aus kryptografisch zufälligen 128 Bit. Ereignis-IDs der Scan-Ereignisse lauten `R:<runId>:<findingId>`. Für jedes Finding bildet `previousEventId` eine lineare Kette seiner Zustandsänderungen; `null` steht nur beim ersten `new`. Ein Review-Entscheidungsereignis referenziert den unmittelbar vorherigen Zustandsereignis-Knoten. Der Store leitet den aktuellen Zustand aus den Ketten ab und baut einen Index nach dem in Epic 2 definierten Zuordnungsschlüssel.

## Run-Manifest

Ein vollständiger Run liegt unter `<storageDirectory>/runs/<runId>/`. Sein `manifest.json` enthält genau diese Felder:

```json
{
  "schemaVersion": 1,
  "runId": "20260927T163802Z-a1b2c3d4",
  "previousRunId": null,
  "startedAtUtc": "2026-09-27T16:37:50Z",
  "completedAtUtc": "2026-09-27T16:38:02Z",
  "solution": "Project.slnx",
  "rules": [
    {
      "ruleId": "fixture-finding",
      "behaviorVersion": 1,
      "effectiveOptions": {"scenario": "base"},
      "counts": {"detected": 1, "open": 1, "new": 1, "updated": 0, "reopened": 0, "accepted": 0, "falsePositive": 0, "resolved": 0}
    }
  ],
  "counts": {"detected": 1, "open": 1, "new": 1, "updated": 0, "reopened": 0, "accepted": 0, "falsePositive": 0, "resolved": 0},
  "observations": [
    {
      "findingId": "F-0123456789abcdef0123456789abcdef",
      "ruleId": "fixture-finding",
      "sourcePath": "src/App/Foo.cs",
      "startLine": 38,
      "fingerprint": "sha256:<64 kleine Hexzeichen>",
      "sourceFiles": [{"path": "src/App/Foo.cs", "sha256": "sha256:<64 kleine Hexzeichen>"}],
      "metrics": {"branchCount": 2},
      "rationale": "Der Fixture-Fall erfordert eine Review-Entscheidung.",
      "evidence": [
        {"sourcePath": "src/App/Foo.cs", "line": 42, "label": "Verzweigung", "detail": "Fixture-Evidenz A", "snippet": "if (condition)"}
      ],
      "state": "open"
    }
  ]
}
```

Die Beispiele mit Platzhaltern zeigen Typ und Feldnamen; echte Hashwerte bestehen vollständig aus Hexzeichen. `previousRunId` ist die ID des letzten vollständigen Runs dieser Projektwurzel, beim ersten Run `null`. `rules` ist nach Regel-ID, `observations` nach Regel-ID und Finding-ID sortiert. `observations` enthält genau einen Eintrag pro von einer aktivierten Regel in diesem Run erkanntem Finding, auch wenn es bereits akzeptiert ist. `state` ist der Zustand **nach** diesem Scan. `startLine`, `metrics`, `rationale`, `evidence` und `sourceFiles` stammen immer aus dem aktuellen Scan, damit Zeilenverschiebungen und relevante Dateihashes ohne Code-Fingerprint-Änderung richtig berichtet werden. `sourceFiles` ist eine nichtleere, nach `path` sortierte Liste ohne Duplikate und enthält die primäre `sourcePath`. Bei einer Änderung außerhalb des regeldefinierten Vergleichsumfangs werden die aktuellen Byte-Hashes hier aktualisiert, ohne ein Finding-Ereignis oder einen neuen Quell-Snapshot zu erzeugen. Bericht und neue Entscheidung verwenden die letzte vollständige Beobachtung; den Snapshot-Pfad liefert das letzte Finding-Ereignis mit Beobachtung.

`detected` zählt alle im aktuellen Scan emittierten Findings. `open` zählt alle aktuell offenen emittierten Findings, einschließlich neuer, aktualisierter und wieder geöffneter. `accepted` und `falsePositive` zählen aktuell unterdrückte emittierte Findings. Damit gilt `detected = open + accepted + falsePositive`. `new`, `updated` und `reopened` zählen Ereignisse dieses Runs und sind Teilmengen von `open`. `resolved` zählt ausschließlich neue `resolved`-Übergänge dieses Runs und gehört nicht zu `detected`. Die obersten `counts` sind Summen der Regelzähler. Für eine nicht aktivierte Regel gibt es weder Zähler noch Zustandsänderung.

Manifest und Markdown-Berichte sind Momentaufnahmen zum Ende des Scans. Ein danach gemeldetes Urteil ändert ihre Dateien und Zähler nicht rückwirkend; der aktuelle Zustand wird aus der Ereigniskette einschließlich späterer Entscheidungen gelesen. Der nächste vollständige Scan bildet das Urteil in seinen Zählern und Berichten ab.

## Finding-Ereignis und Snapshot

Jede im Run entstandene Zustandsänderung liegt unter `runs/<runId>/findings/<findingId>.json`. Pro Finding und Run gibt es höchstens eine solche Datei. Das Schema lautet:

```json
{
  "schemaVersion": 1,
  "eventId": "R:20260927T163802Z-a1b2c3d4:F-0123456789abcdef0123456789abcdef",
  "previousEventId": null,
  "eventType": "new",
  "findingId": "F-0123456789abcdef0123456789abcdef",
  "subject": {
    "ruleId": "fixture-finding",
    "projectPath": "src/App/App.csproj",
    "sourcePath": "src/App/Foo.cs",
    "subjectId": "M:App.Foo.Process(System.Int32)",
    "discriminator": "case-a"
  },
  "state": "open",
  "observation": {
    "startLine": 38,
    "fingerprintVersion": 1,
    "fingerprint": "sha256:<64 kleine Hexzeichen>",
    "behaviorVersion": 1,
    "effectiveOptions": {"scenario": "base"},
    "metrics": {"branchCount": 2},
    "rationale": "Der Fixture-Fall erfordert eine Review-Entscheidung.",
    "evidence": [
      {"sourcePath": "src/App/Foo.cs", "line": 42, "label": "Verzweigung", "detail": "Fixture-Evidenz A", "snippet": "if (condition)"}
    ],
    "snapshotPath": ".ainetreview/runs/20260927T163802Z-a1b2c3d4/snapshots/F-0123456789abcdef0123456789abcdef.txt"
  }
}
```

Zulässige `eventType`-Werte sind `new`, `updated`, `reopened` und `resolved`. Bei den ersten drei sind `state = open` und `observation` vollständig vorhanden. Bei `resolved` sind `state = resolved` und `observation = null`; `subject` und `previousEventId` bleiben Pflichtfelder. `metrics` ist eine Map von stabilen Schlüsseln auf endliche JSON-Zahlen und darf leer sein. `rationale` ist ein nichtleerer Satz, `evidence` eine nichtleere Liste mit den gezeigten typisierten Feldern. Bei `new` ist `previousEventId = null`, sonst die aktuelle letzte Ereignis-ID dieses Findings. Jeder Pfad in `snapshotPath` ist relativ zur Projektwurzel, niemals zum JSON-Dateiverzeichnis.

Der Snapshot liegt unter `runs/<runId>/snapshots/<findingId>.txt`. Er enthält den von der Regel bestimmten ursächlichen Quelltext, UTF-8 ohne BOM und LF. `resolved` erzeugt keinen Snapshot. Bei unverändertem Befund verweist die aktuelle Beobachtung weiterhin auf den letzten Snapshot in seiner Ereigniskette. Der absolute Pfad wird nicht gespeichert.

## Review-Entscheidung

`report_review` veröffentlicht ein einzelnes JSON unter `decisions/<decisionId>.json`:

```json
{
  "schemaVersion": 1,
  "eventId": "D-0123456789abcdef0123456789abcdef",
  "previousEventId": "R:20260927T163802Z-a1b2c3d4:F-0123456789abcdef0123456789abcdef",
  "findingId": "F-0123456789abcdef0123456789abcdef",
  "runId": "20260927T163802Z-a1b2c3d4",
  "decidedAtUtc": "2026-09-27T16:40:00Z",
  "verdict": "accepted",
  "fingerprint": "sha256:<64 kleine Hexzeichen>",
  "behaviorVersion": 1,
  "effectiveOptions": {"scenario": "base"},
  "sourceFiles": [{"path": "src/App/Foo.cs", "sha256": "sha256:<64 kleine Hexzeichen>"}]
}
```

`runId` ist der letzte vollständige Scan, dessen `observations` dieses Finding enthält. `previousEventId` ist die letzte aktuelle Zustandsereignis-ID; bei Korrektur eines Urteils kann das ein früheres Entscheidungsereignis sein. Der Store bezieht den Zuordnungsschlüssel aus dem ersten Finding-Ereignis, aktuelle Metriken, Evidenz und `sourceFiles` aus der letzten vollständigen Beobachtung und den Snapshot-Pfad aus dem letzten Finding-Ereignis mit Beobachtung. Nach erfolgreicher Stale-Prüfung liefert ein identisches aktuelles Verdict dessen existierende `decisionId` zurück und schreibt keine zweite Datei. Ein Wechsel des Verdicts schreibt ein neues Ereignis. Ein veralteter Fingerprint, geänderte relevante Datei oder geänderte effektive Optionen ergeben `STALE_FINDING`.

## Veröffentlichung, Konflikte und Retention

Der Runner schreibt Bericht und Run-Paket in temporäre Verzeichnisse innerhalb ihrer jeweiligen Zielverzeichnisse. Er prüft zum Schluss die Byte-Hashes aller analysierten Quell- und Projektdateien sowie der Konfiguration gegen den zu Beginn gelesenen Stand. Bei Änderung, Regel- oder Schreibfehler wird weder ein Run veröffentlicht noch ein Erfolg gemeldet. Bei Erfolg benennt er zuerst den fertigen Berichtsordner und zuletzt das fertige Run-Verzeichnis auf die finale Run-ID um. Die finale Umbenennung des Run-Verzeichnisses ist der Commit-Punkt des Scans. Ein verwaister Berichtsordner ohne Run-Manifest ist ungültig und wird beim nächsten Start entfernt. Eine Entscheidung wird in eine temporäre Datei geschrieben und dann ohne Überschreiben auf ihren endgültigen Namen verschoben. Alle diese Vorgänge erfolgen unter dem Repository-Lock aus Epic 1.

Beim Laden ist `STORAGE_CONFLICT` erreicht, wenn zwei vollständige Run-Manifeste denselben `previousRunId` haben, zwei Ereignisse desselben Findings denselben `previousEventId` als Vorgänger beanspruchen oder ein Zuordnungsschlüssel zwei Finding-IDs besitzt. In diesem Fall sind Scan und Verdict-Schreiben gesperrt; vorhandene Berichte bleiben lesbar. Der Nutzer muss die konkurrierenden Git-Zweige im Storage zu einer linearen Folge auflösen und danach neu scannen. Das Werkzeug wählt niemals still einen Zweig. Ein beschädigtes Schema oder fehlende Referenz ist dagegen `STORAGE_FAILED`.

Alle vollständigen Run-Pakete und Entscheidungen bleiben im ersten Produktstand erhalten. Das erhält Trefferzahlen und Review-Verlauf für spätere Auswertungen. AiNetReview löscht oder komprimiert versionierte Daten nicht automatisch. Generierte Berichte im Ausgabeordner werden ebenfalls nicht automatisch gelöscht; sie können unabhängig vom Store manuell entfernt werden. Die Speicherung dupliziert Roh-Snapshots nur bei neuem oder geändertem Befund. Ein Statistik-Dashboard und automatische Retention gehören nicht zum DoD.

Spätere Auswertungen können getrennt vollständige Läufe, ausgelöste Befunde pro Lauf, verschiedene Finding-IDs, `accepted`, `false-positive`, Wiederöffnungen und `resolved` zählen. `resolved` beweist nur, dass die Regel den Befund beim nächsten vollständigen Scan nicht mehr fand; ob die Änderung fachlich gut war, folgt daraus nicht automatisch.

## Markdown-Berichte

Für jeden vollständigen Run werden `<outputDirectory>/<runId>/index.md` und genau eine Datei `rules/<ruleId>.md` pro aktiver Regel erzeugt, auch wenn die Regel keine offenen Findings hat. Markdown-Dateien verwenden UTF-8, LF und repo-relative Pfade. Links sind vom Speicherort der jeweiligen Markdown-Datei aus relativ und kodieren Sonderzeichen in Pfadsegmenten.

`index.md` beginnt mit `# AiNetReview – <runId>`. Danach folgen eine Metadaten-Tabelle mit UTC-Start/Ende, Solution-Pfad und aktiven Regelversionen samt Optionen, eine Zählwert-Tabelle mit genau den Feldern `detected`, `open`, `new`, `updated`, `reopened`, `accepted`, `falsePositive` und `resolved` sowie eine Regel-Tabelle mit denselben Zählwerten und einem Link auf `rules/<ruleId>.md`. Ein kurzer Hinweis erklärt: Findings sind Review-Anlässe, keine automatisch zu behebenden Fehler.

`rules/<ruleId>.md` beginnt mit Regel-ID, Titel, Verhaltensversion, wirksamen Optionen und Regelzählwerten. Danach folgen nur aktuell `open` stehende Findings, sortiert nach Quellpfad, Zeile und Finding-ID. Jeder Eintrag beginnt mit `## <findingId>` und enthält in dieser Reihenfolge Status (`new`, `updated`, `reopened` oder weiter `open`), `sourcePath:line`, `subjectId`, alle Metriken als Key-Wert-Tabelle, Fingerprint, Begründung, eine Evidenz-Tabelle mit `Pfad | Zeile | Beleg | Detail | Code` und einen relativen Link auf den letzten Snapshot. Der Bericht listet akzeptierte und falsche positive Fälle nur in den Zählwerten. Er enthält keinen vollständigen Quellcode-Dump. Der Fingerprint kann unverändert an `report_review` übergeben werden.
