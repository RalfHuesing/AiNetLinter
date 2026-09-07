---
status: ready
title: Klare Fehlerkorrekturen im C#-Feature-Kontext
created: 2026-09-07
updated: 2026-09-07
---

# Klare Fehlerkorrekturen im C#-Feature-Kontext

## 1. Ziel

`get_feature_context` soll bei Fehlern, Abbrüchen und begrenzten Ergebnissen
keine falschen fachlichen Aussagen erzeugen. Dieses Konzept bündelt sechs
kleine, voneinander abgrenzbare Korrekturen am bestehenden Feature-Context-
Vertrag. Der bisherige erfolgreiche Default bleibt erhalten; erweitert werden
nur Fehlerwahrheit, Abbruchverhalten, Reproduzierbarkeit, Antwortbegrenzung,
Beziehungssemantik und Snapshot-Hinweise.

## 2. Geltungsbereich

Betroffen sind ausschließlich:

- `src/AiNetLinter/Mcp/Tools/FeatureContext/`
- die wiederverwendete Caller-Suche in
  `src/AiNetLinter/Core/DiffImpactAnalyzer.cs`
- der gemeinsame projektgebundene Antwort-Wrapper in
  `src/AiNetLinter/Mcp/Projects/ProjectToolCall.cs`
- die dafür notwendigen FastTests, gegebenenfalls gezielte Integrationstests
  für den Live-MCP-Vertrag.

Die Korrekturen beziehen sich auf den bestehenden projektgebundenen
`get_feature_context`-Aufruf. Es wird keine neue Explore-API eingeführt.

## 3. Muss-Kriterien

### 3.1 Violations dürfen Fehler nicht als leer darstellen

Aktuell führt `CollectViolationsAsync` bei einem fehlgeschlagenen Linterlauf
zu `ViolationsReportDto(0, 0, [], false)`. Auch fehlende Quelldateien werden
als leerer erfolgreicher Bericht behandelt. Zusätzlich wird eine
`OperationCanceledException` durch den pauschalen Catch verschluckt.

Erforderlich ist:

- Cancellation wird immer weitergereicht und nicht in eine Erfolgsantwort
  umgewandelt.
- Der Violations-Bereich unterscheidet mindestens `complete`, `truncated`,
  `unavailable`, `failed` und `notApplicable`.
- Counts und Trefferlisten bleiben getrennt vom Bereichsstatus.
- `Keine Linter-Verstöße` wird nur bei einem erfolgreich abgeschlossenen,
  leeren Scan ausgegeben.
- Für maschinenlesbare Auswertung existieren stabile Reason-Codes, mindestens
  für Scanfehler, fehlende Quelldatei und Cancellation.

Die bisherigen Felder und die Bedeutung eines erfolgreich leeren Reports
bleiben für kompatible Konsumenten erhalten.

### 3.2 Caller-Suche muss Cancellation respektieren

Die Feature-Context-Aggregation reicht derzeit kein CancellationToken an
`DiffImpactAnalyzer.FindCallSiteEntriesAsync` weiter; die Roslyn-Suche läuft
dadurch mit dem Default-Token.

Erforderlich ist:

- Ein CancellationToken wird von `FeatureContextScanner` bis
  `SymbolFinder.FindReferencesAsync` durchgereicht.
- Die Caller-Schleifen brechen bei Cancellation ebenfalls ab.
- Ein abgebrochener Aufruf liefert keine partielle Erfolgs-Payload.
- Die bestehende, tokenlose interne Nutzung bleibt entweder als kompatibler
  Wrapper erhalten oder wird vollständig auf die tokenisierte Signatur
  umgestellt, ohne doppelte Suchlogik einzuführen.

### 3.3 Truncation und Reihenfolge müssen deterministisch sein

Caller werden aktuell direkt mit `Take(maxCallers)` aus einer nicht normierten
Roslyn-Reihenfolge ausgewählt. Violations werden nur nach Zeile sortiert.
Damit können gleiche Anfrage und gleicher Solution-Stand unterschiedliche
Einträge liefern.

Erforderlich ist:

- Caller vor der Begrenzung stabil nach normalisiertem Pfad, Zeile, Projekt,
  Membername und Symbolname sortieren.
- Violations stabil nach Pfad, Zeile, Regel-ID und Nachricht sortieren.
- Die Sortierung gilt identisch für Text und StructuredContent.
- Wiederholte Ausführung mit identischem Snapshot liefert dieselbe Auswahl und
  Reihenfolge.

### 3.4 `maxTests` darf keine unbounded Methodennamenliste erzeugen

`maxTests` begrenzt aktuell nur die Anzahl ausgewählter Testdateien. Alle
Methodennamen einer ausgewählten Datei werden anschließend vollständig in
Text und StructuredContent ausgegeben. Eine einzelne große Testdatei kann das
Antwortlimit dadurch faktisch umgehen.

Erforderlich ist:

- Das bestehende Verhalten von `maxTests` als Dateilimit bleibt zunächst
  kompatibel.
- Zusätzlich wird die Zahl ausgegebener Testmethoden pro Datei und insgesamt
  hart begrenzt.
- Die Begrenzung wird mit Counts und einem maschinenlesbaren
  Truncation-Grund sichtbar gemacht.
- Text und StructuredContent verwenden dieselbe bereits priorisierte Auswahl.
- Eine stille Umdeutung von `maxTests` zu einem Methodenlimit ist nicht erlaubt.

### 3.5 Statische Referenzen dürfen nicht als sichere Caller behauptet werden

Die Implementierung verwendet `SymbolFinder.FindReferencesAsync`. Das findet
statische Referenzstellen, nicht ausschließlich tatsächlich ausgeführte
Aufrufe. Die aktuelle Darstellung `Direkte Aufrufer` ist daher semantisch zu
stark.

Erforderlich ist:

- Die Ausgabe kennzeichnet die Daten als statische Referenzen/Call-Sites.
- Eine echte Laufzeit-Coverage- oder Runtime-Dispatch-Aussage wird nicht
  behauptet.
- Bestehende strukturierte Call-Site-Daten bleiben additiv kompatibel; eine
  spätere feinere `relationKind`-Klassifikation darf vorbereitet, aber nicht
  als unvollständige Scheinpräzision eingeführt werden.
- Dokumentation und Formatter verwenden dieselbe vorsichtige Semantik.

### 3.6 Freshness-/Degraded-Hinweise müssen strukturiert sichtbar sein

`ProjectToolCall.WithDegradedHeader` ergänzt bei einem veralteten, aber noch
verwendbaren Solution-Stand nur den Text. `StructuredContent` bleibt dabei
unverändert. Strukturorientierte MCP-Clients können den Warnzustand deshalb
nicht erkennen.

Erforderlich ist:

- Der Degraded-/Freshness-Zustand wird im strukturierten Ergebnis des
  `get_feature_context`-Aufrufs sichtbar.
- Text und StructuredContent beziehen sich auf denselben Zustand.
- Der bisherige Warntext bleibt für text-only Clients erhalten.
- Die Ergänzung erfolgt additiv und verändert keine bestehenden Core-Felder.

## 4. Bewusste Scope-Grenzen

Nicht Bestandteil dieses Konzepts sind:

- Source-Preview oder Einbettung von Symbolkörpern;
- automatische Callee-Traversierung;
- Multi-Symbol-Kontext;
- ein neues Compact-/Budget-Profil oder eine Default-Umstellung;
- eine grundlegende Linter-Cache- oder Snapshot-Architektur;
- Assembly-Ziele für `get_feature_context`;
- vollständige Runtime-Aufruf- oder Test-Coverage-Beweise;
- allgemeine Pfadmatching- oder andere nicht durch diese sechs Befunde
  belegte Fehler.

Die Behebung des uncached Violations-Scans selbst ist ein separates
Performance-Thema. In diesem Scope wird nur verhindert, dass seine Kosten oder
Fehler fachlich unsichtbar bleiben.

## 5. Betroffene bestehende Strukturen

- `FeatureContextScanner.ScanAsync` aggregiert die fünf bestehenden Bereiche.
- `FeatureContextModels.cs` enthält die derzeitigen Report-DTOs.
- `FeatureContextFormatter.cs` erzeugt die Markdown-Darstellung.
- `DiffImpactAnalyzer.FindCallSiteEntriesAsync` führt die Referenzsuche aus.
- `McpToolResults.Text<T>` erzeugt Text und StructuredContent additiv.
- `ProjectToolCall.WithDegradedHeader` ergänzt den allgemeinen Degraded-
  Warntext.
- Bestehende FastTests prüfen Abschnittsnamen, Counts, Aliasauflösung und
  Truncation und bleiben als Legacy-Vertrag erhalten, soweit die korrigierte
  Semantik dem nicht widerspricht.

## 6. Fehler- und Fallback-Semantik

- Cancellation ist ein Abbruch des Tool-Aufrufs, kein leerer Teilerfolg.
- Ein erfolgreich geprüfter Bereich mit null Treffern ist von `failed`,
  `unavailable`, `notApplicable` und `notRequested` unterscheidbar.
- Bei einer begrenzten Trefferliste bleiben Gesamtzahl, ausgegebene Anzahl und
  Grund der Begrenzung sichtbar.
- Ein veralteter Solution-Stand darf verwendet werden, muss aber in Text und
  StructuredContent als degraded/stale markiert sein.
- Nicht angeforderte Bereiche behalten ihre bisherige `null`-Semantik.
- Der Server darf keine versteckten Folgeabfragen einführen.

## 7. Verifikation und Akzeptanzkriterien

### FastTests

Für alle sechs Befunde werden gezielte xUnit-Tests ergänzt oder erweitert:

- gecancelter Violations-Scan propagiert Cancellation;
- fehlgeschlagener Violations-Scan ist nicht als leer erkennbar;
- fehlende Quelldatei erzeugt `notApplicable`/`unavailable` statt `0`;
- Caller-Suche respektiert Cancellation;
- wiederholte Scans liefern dieselbe Caller- und Violations-Reihenfolge;
- Caller- und Violations-Truncation wählt nach stabilen Tie-Breakern;
- große Testdatei überschreitet den Methoden-Cap nicht;
- Text und StructuredContent tragen denselben Status und dieselben Counts;
- statische Referenzen werden nicht als Laufzeit-Coverage beschrieben;
- Degraded/Freshness ist auch im StructuredContent vorhanden.

### Abschluss-Gates

Vor Übergabe der Implementierung müssen erfolgreich laufen:

```text
dotnet build
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
```

Zusätzlich ist mindestens ein Live-MCP-Vertragstest für
`get_feature_context` erforderlich, der Text- und StructuredContent-Parität
sowie den Degraded-Fall prüft. Stress-Tests sind nicht Bestandteil des
regulären Abschluss-Gates.

### Dokumentation

Da sich die beobachtbare MCP-Semantik ändert, werden nach der Implementierung
die betroffenen Beschreibungen in `Docs/agent-api.md` und gegebenenfalls
`Docs/integration.md` gegen den finalen Code synchronisiert. `Docs/configuration.md`
ist nur bei einer tatsächlichen Konfigurationsänderung betroffen.

## 8. Ergebnis und Entscheidungsstand

Der Scope ist auf sechs codebezogene Fehler begrenzt und enthält keine
blockierenden offenen Fragen. Die bestehende Ausgabe bleibt im erfolgreichen
Legacy-Fall erhalten; Änderungen an Status, Semantik und ergänzenden
Metadaten sind explizit beschrieben und testbar.

Das Konzept ist zur Umsetzung freigegeben.

### Commit-Vorschlag

docs: klare Feature-Context-Bugs als Umsetzungskonzept festhalten
