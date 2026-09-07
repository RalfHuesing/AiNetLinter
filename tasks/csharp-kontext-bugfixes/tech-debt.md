# Tech Debt

## TD-001 — Bestehende Duplikat-Cluster außerhalb des Feature-Context-Scopes

- Schweregrad: P3
- Beschreibung: Der Audit meldet 13 bestehende Duplikat-Cluster.
- Scope/Fundstelle: durch `find_duplicates` im Projekt; keine konkrete eigene
  Änderung des aktuellen Feature-Context-Pakets erforderlich.
- Evidenz: Implementierer-Audit und Korrektur-Audit am 2026-09-07; zuletzt
  2 bestehende fuzzy Cluster, scope-fern bzw. semantisch unklarer Altbestand.
- Disposition: accepted-deferred
- Nächster Schritt: Bei einer passenden Änderung der betroffenen Bereiche
  erneut fachlich bewerten; kein breiter Cleanup in diesem Task.
- Log-Anker: Implementiererbericht vom 2026-09-07.

## TD-003 — MCP-Toolbeschreibung des Feature-Context veraltet

- Schweregrad: P1
- Beschreibung: `AddGetFeatureContext` beschreibt statische Referenzen und
  Methoden-Caps nicht korrekt.
- Scope/Fundstelle: `src/AiNetLinter/Mcp/Registration/AnalysisToolRegistrations.cs`.
- Evidenz: unabhängiger Review und Korrektur-Implementierer am 2026-09-07;
  die Beschreibung wurde synchronisiert; der frische Review bestätigt die
  Korrektur fachlich.
- Disposition: fixed
- Nächster Schritt: Abschluss-Review und Gesamt-Gates.
- Log-Anker: Reviewerbericht und Korrekturbericht vom 2026-09-07.

## TD-004 — Testmethoden-Truncation-Gründe bei kombinierten Caps falsch

- Schweregrad: P1
- Beschreibung: Der Scanner kennzeichnet Caps als ausgelöst, obwohl nur ein
  anderer Cap die Auswahl begrenzt hat.
- Scope/Fundstelle: `src/AiNetLinter/Mcp/Tools/FeatureContext/FeatureContextScanner.cs`.
- Evidenz: unabhängiger Review und Korrektur-Implementierer am 2026-09-07;
  tatsächliche Auslösung pro Datei/global wurde korrigiert; der frische Review
  bestätigt die Korrektur fachlich.
- Disposition: fixed
- Nächster Schritt: Abschluss-Review und Gesamt-Gates.
- Log-Anker: Reviewerbericht und Korrekturbericht vom 2026-09-07.

## TD-002 — Low-Confidence-Dead-Code-Kandidaten außerhalb des Scopes

- Schweregrad: P3
- Beschreibung: Der Audit meldet 3 bestehende Low-Confidence-Kandidaten.
- Scope/Fundstelle: durch `find_dead_code` im Projekt; nicht Bestandteil der
  geänderten Feature-Context-/Caller-Pfade.
- Evidenz: Implementierer-Audit und Korrektur-Audit am 2026-09-07; geringe
  Konfidenz und daher keine sichere Entfernung ohne separate
  Referenz-/Vertragsprüfung.
- Disposition: accepted-deferred
- Nächster Schritt: In einem passenden Cleanup-/API-Scope mit
  `find_references` und Reflection-/Vertragsprüfung bewerten.
- Log-Anker: Implementiererbericht vom 2026-09-07.

## TD-005 — Bestehende Magic-Value-Funde außerhalb des Scopes

- Schweregrad: P3
- Beschreibung: Der Korrektur-Audit meldet 10 bestehende Magic-Value-Funde.
- Scope/Fundstelle: durch `find_magic_values` im Projekt; außerhalb des
  Feature-Context-Änderungsbereichs.
- Evidenz: Korrektur-Audit am 2026-09-07; keine sichere scope-nahe
  Zentralisierung aus dem aktuellen Auftrag ableitbar.
- Disposition: accepted-deferred
- Nächster Schritt: Bei einer passenden Änderung mit fachlicher Identitäts-
  und Vertragsprüfung bewerten.
- Log-Anker: Korrekturbericht vom 2026-09-07.

## TD-006 — FastTest-Datei überschreitet MaxLineCount

- Schweregrad: P1
- Beschreibung: Die Testdatei `GetFeatureContextToolTests.cs` hat 527 statt
  maximal 500 Zeilen und verletzt damit den Abschluss-Qualitätscheck.
- Scope/Fundstelle: `src/AiNetLinter.FastTests/Mcp/Tools/FeatureContext/GetFeatureContextToolTests.cs`.
- Evidenz: Frischer Review am 2026-09-07; 1 Test-Scope-Violation.
- Disposition: fixed
- Nächster Schritt: Abschluss-Review und Gesamt-Gates.
- Log-Anker: Reviewerbericht Korrekturrunde 1 und Korrekturbericht 2 vom
  2026-09-07.

## TD-007 — Direkte kombinierte Cap-Regression fehlt

- Schweregrad: P2
- Beschreibung: Die korrekte kombinierte Cap-Logik ist nicht durch eine
  direkte Regressionstest-Matrix abgesichert.
- Scope/Fundstelle: `GetFeatureContextToolTests` und
  `FeatureContextScanner.CollectTestsAsync`.
- Evidenz: Frischer Review am 2026-09-07; isolierte Caps sind getestet,
  Kombinationen nicht.
- Disposition: fixed
- Nächster Schritt: Abschluss-Review und Gesamt-Gates.
- Log-Anker: Reviewerbericht Korrekturrunde 1 und Korrekturbericht 2 vom
  2026-09-07.

## TD-008 — Zusätzliche Symbolauflösungs-Aliase nicht vollständig dokumentiert

- Schweregrad: P2
- Beschreibung: Die Dokumentation nennt nur den Alias `symbol`, die
  Registrierung akzeptiert zusätzlich `identifier` und `name`.
- Scope/Fundstelle: `Docs/agent-api.md`, `Docs/integration.md` sowie die
  Feature-Context-Registrierung.
- Evidenz: Abschluss-Review am 2026-09-07; kein Einfluss auf die korrigierte
  Feature-Context-Fehlersemantik, aber Doku-/Vertragsdrift.
- Disposition: promoted-to-project-debt
- Nächster Schritt: In einem separaten Dokumentations-/API-Vertragstask
  synchronisieren; nicht in diesem Bugfix-Scope erweitern.
- Log-Anker: Reviewerbericht Korrekturrunde 2 vom 2026-09-07.
