# AiNetReview – Konzept

Dieses Dokument ist der Einstieg in die verbindliche Spezifikation. Das ursprüngliche [Relaunch-Konzept](Relaunch-Konzept.md) bleibt als unveränderte Motivation erhalten. Die konkreten Verträge stehen jeweils in genau einem der unten verlinkten Epics. Bei Widersprüchen wird die Spezifikation korrigiert, bevor implementiert wird.

## Ziel

AiNetReview ist ein lokales Review-Werkzeug für C#/.NET. Es findet deterministisch mögliche Qualitäts- und Architekturprobleme, erstellt für Agenten nachvollziehbare Markdown-Berichte und speichert strukturierte Review-Entscheidungen mit dem ursächlichen Quellcode im analysierten Projekt. Ein akzeptierter Fall wird bei gleichem Code nicht immer wieder als neue Arbeit gemeldet; relevante Änderungen öffnen ihn erneut.

Der Agent schließt zuerst seinen Entwicklungstask ab. Danach oder zu einem anderen Zeitpunkt prüft er mit dem Nutzer die Findings. Das Tool entscheidet nicht über Refactoring, blockiert keinen Build und ersetzt keine Code-Navigation. Eindeutige technische Fehler bleiben bei Build- und Compilerprüfungen. Das bisherige AiNetLinter wird erst nach erfolgreicher Einführung dieses Werkzeugs gesondert bewertet.

Die Arbeitsthese lautet: Für Agenten ist Code schwer, wenn viele Entscheidungswege lokal verfolgt werden müssen, relevanter Kontext über Dateien und Aufrufketten verstreut ist, Zustand verborgen bleibt oder Dokumentation in die Irre führt. Menschliche Lesbarkeit und agentische Verständlichkeit überschneiden sich, sind aber nicht identisch. Studien zeigen Probleme beim [Finden relevanten Repository-Kontexts](https://arxiv.org/abs/2602.05892), beim [Verfolgen von Abhängigkeiten](https://arxiv.org/abs/2608.01927) und bei [falscher Code-Dokumentation](https://aclanthology.org/2024.findings-naacl.66/). Daraus folgt kein universeller KI-Komplexitätswert; der Nutzen unserer Regeln wird anhand der späteren Review-Entscheidungen geprüft.

## Erster nutzbarer Stand

Der Produktname ist `AiNetReview`. Ein ausführbarer Host bietet lokalen MCP-Server und CLI über denselben Kern. Beide verwenden die im analysierten Projekt liegende `ainetreview.json`. Git ist keine Laufzeitvoraussetzung; die Speicherdateien sind für Versionierung geeignet. MCP kann zusätzlich zu Berichten strukturierte Urteile entgegennehmen.

Der erste vollständige Stand enthält **keine fachliche Review-Regel**. Eine registrierte `template-noop`-Regel liefert bewusst keine Findings und zeigt den Erweiterungspunkt. Tests verwenden eine nur dort registrierte Fixture-Regel mit reproduzierbaren Findings, damit Reporting und Zustandswechsel bereits vollständig geprüft werden. Welche fachlichen Regeln sinnvoll sind, entscheiden wir erst anhand späterer Erfahrung. Die Architektur hält ihre Aufnahme auf eine Regelimplementierung, ihre Tests und eine Registrierungszeile begrenzt.

Ein Finding erhält eine stabile ID, strukturierten Quellort, Messwerte, Evidenz, Fingerprint und Quellcode-Snapshot. Agenten melden `accepted` oder `false-positive`; ein verschwundener Befund wird erst durch vollständigen Scan `resolved`. Eine Änderung des von der Regel gelieferten Vergleichsinhalts, ihrer wirksamen Optionen oder ihrer Verhaltensversion kann eine alte Entscheidung wieder öffnen. Wie Quellcode normalisiert wird, bestimmt die jeweilige fachliche Regel. Das Speicherformat, die genaue Zustandsmaschine und Fehlerfälle stehen in den Epics.

## Verbindliche Epics

1. [Eingaben und Host](epics/01-Eingaben-und-Host.md): JSON, CLI, MCP, Pfade, Git-Unabhängigkeit und Lock.
2. [Regel und Findings](epics/02-Regel-und-Findings.md): Regelvertrag, Template-Regel, Identität, Fingerprint und Zustandsautomat.
3. [Storage und Berichte](epics/03-Storage-und-Berichte.md): versionierbare JSON-Dateien, Snapshots, Konflikte, Retention und Markdown-Format.
4. [Umsetzung und Abnahme](epics/04-Umsetzung-und-Abnahme.md): Projekt- und Namespace-Struktur, DI, Testebenen, Lasttest und Definition of Done.

Der frühe technische Aufbau darf einen sichtbaren `NoOpFindingStore` verwenden. Das [Produkt-DoD](epics/04-Umsetzung-und-Abnahme.md#definition-of-done) verlangt echte Speicherung, vollständiges Reporting, einen wiederholbaren Review-Zyklus und grüne Tests. Ein Implementierungsagent arbeitet die Epics in dieser Reihenfolge ab; Produktentscheidungen sind dort festgelegt.
