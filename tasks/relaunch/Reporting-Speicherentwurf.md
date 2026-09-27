# Reporting und Speicherung – Arbeitsentwurf

Dieser Entwurf konkretisiert die offene Speicherfrage aus der [Roadmap](Relaunch-Roadmap.md). Er ist noch keine Entscheidung über Dateinamen oder Datenformat.

## Zwei verschiedene Aufgaben

**Audit-Bericht:** Ein Markdown-Bericht zeigt, was in einem Lauf Review braucht. Der vorgeschlagene Pfad `audit-reporting/rules/max-file-length/20260927-163802-report.md` ist dafür brauchbar. Eine Datei pro Regel und Lauf kann auch 40 Findings enthalten. Pro Finding stehen dort ID, Quellort, relevante Messwerte, Grund der Meldung und ein Verweis auf den gespeicherten Quellcode. Unverändert akzeptierte Findings werden höchstens in der Zusammenfassung gezählt.

**Dauerhafter Zustand:** Frühere Markdown-Berichte werden nicht durchsucht, um den aktuellen Status zu bestimmen. Dafür gibt es pro Finding einen maschinenlesbaren Datensatz im Repository, etwa `audit-reporting/findings/F-000123.json`, mit Regel und damaligen Regelparametern, betroffenem Objekt, Quellcode-Snapshots, Fingerprints und Review-Verlauf. Die Datensätze sind die Grundlage für spätere Auswertungen. Markdown-Berichte werden aus dem aktuellen Scan und diesen Datensätzen erzeugt.

Beispiel für einen Eintrag im Regelbericht:

```text
F-000123  src/Foo.cs  734 Zeilen bei Grenzwert 600  neu
F-000127  src/Bar.cs  812 Zeilen bei Grenzwert 600  erneut offen nach Änderung
```

Der Bericht braucht keinen vollständigen Quellcode-Dump. Der ursächliche Code wird nur bei einem neuen oder relevant geänderten Fall als Snapshot gespeichert. Ob ein Bericht jedes Laufs ebenfalls in Git bleibt oder nur im gewählten Ausgabeordner liegt, ist offen.

## Abgleich beim nächsten Audit

1. Die Finding-Datensätze einmal laden und nach Regel und betroffenem Objekt auffindbar machen. Bei einer Dateiregel ist das Objekt die Datei, bei einer Methodenregel das C#-Symbol. Die genaue Identität bei Umbenennungen und Verschiebungen ist noch offen.
2. Den Code analysieren und für jeden aktuellen Befund den relevanten Fingerprint berechnen.
3. Kein bisheriger Datensatz: neues offenes Finding mit ID. Offen geblieben: wieder im Bericht. Akzeptiert und unverändert: nicht erneut zur Entscheidung vorlegen. Akzeptiert und geändert: erneut öffnen und alten Quellcode zum Vergleich anbieten.
4. Verschwindet ein Befund nach einer Behebung, erst nach einem vollständigen Scan mit derselben Regel als erledigt einordnen.

Bei 180.000 Zeilen muss die Analyse ohnehin den Code lesen. Die Zustandsabfrage durchsucht keine historischen Berichte und kann über eine einmal aufgebaute Zuordnung pro Finding erfolgen. Vierzig Findings einer Regel bedeuten vierzig Datensätze, aber nur einen lesbaren Regelbericht pro Lauf.

## Noch zu klären

- JSON-Dateien pro Finding oder ein anderes Git-taugliches Format; SQLite allenfalls als ableitbarer lokaler Index, nicht automatisch als versionierte Quelle.
- Stabile Zuordnung bei Umbenennung, Verschiebung, Aufteilung und mehreren gleichartigen Findings im selben Objekt.
- Welche Audit-Berichte dauerhaft archiviert werden und welche Daten allein über die Finding-Historie erhalten bleiben.
- Was „Regel wurde ausgelöst“ statistisch bedeutet: Scan-Treffer, verschiedene Findings oder neue Review-Episoden.
