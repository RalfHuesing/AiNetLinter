# Feature-Idee: Vollständige Assembly-Solution materialisieren

## Status

Grobe Idee / Feature-Wunsch; noch kein ausformuliertes Konzept

## Motivation

Der AiNetLinter-MCP-Server soll Assemblys grundsätzlich wie SourceToAI als
vollständige ILSpy-Projekte materialisieren. Für eine fachliche Analyse, zum
Beispiel die Suche nach allen DCM-Aufrufen in einem Fertigungsauftrag, soll der
Agent danach dieselben Möglichkeiten wie bei einem lokalen Source-Repository
haben.

Der aktuelle Assembly-Cache enthält überwiegend Signaturen beziehungsweise
parsierbare `throw null!`-Stubs. Die vollständigen Methodenkörper werden
On-Demand für ein konkretes Symbol dekompiliert. Dadurch kann der Agent nicht
einfach mit `rg` im vollständigen Decompilat suchen und Roslyn analysiert nicht
die komplette body-reiche Assembly-Solution.

## Gewünschtes Verhalten

Bei jedem Assembly-Laden soll die komplette Ziel-Assembly als synthetische
Solution beziehungsweise mindestens als synthetisches Projekt erzeugt werden:

```text
cache/assembly/<hash>/<generation>/
├── decompile/
│   ├── <viele dekompilierte .cs-Dateien>
│   └── ...
├── DecompiledAssembly.csproj
├── DecompiledAssembly.slnx   (optional, falls für die Navigation erforderlich)
└── manifest.json
```

Der MCP-Server soll in seinen Antworten die navigierbaren Pfade zu
`sourceRoot`, `.csproj` und gegebenenfalls `.slnx` zurückgeben. Der Agent kann
dann bei Bedarf selbst mit `rg`, Dateibaum-Navigation oder anderen lokalen
Werkzeugen im Cache arbeiten.

## Technischer Vorschlag

- ILSpy `WholeProjectDecompiler` verwenden und die vollständige Ziel-Assembly
  mit Methodenkörpern in den Assembly-Cache schreiben.
- Das erzeugte Projekt anschließend mit allen dekompilierten C#-Dokumenten in
  Roslyn laden und als vollständige Assembly-Solution in der Session halten.
- Vor dem Roslyn-Parsing dieselbe Bereinigung wie im bestehenden Body-Resolver
  anwenden, insbesondere für compiler-generierte VB-Typen wie `_Closure$__...`.
- `sourceRoot`, Projekt-/Solution-Pfad, Assembly-Hash und Generation in den
  MCP-Antworten ausweisen.
- Cache-Einträge über Assembly-Hash, Decompiler-Version, Optionen und relevante
  Referenzinformationen versionieren, damit die Dateien reproduzierbar und
  wiederverwendbar sind.
- Decompiler- und Referenzdiagnosen nicht verschweigen; der Decompilat-Inhalt
  bleibt als `untrusted` und gegebenenfalls `partial` gekennzeichnet.

## Folgefunktion: Suche im Decompilat

Auf Basis der materialisierten Dateien kann der Agent direkt mit `rg` suchen.
Zusätzlich könnte der MCP-Server später eine serverseitige Suche über die
gesamte dekompilierte Solution anbieten. Damit wären Suchvorgänge wie die
Ermittlung aller `ExecuteDCM`- beziehungsweise `DcmListId`-Vorkommen möglich,
ohne jeden Methodenkörper einzeln anzufordern.

## Abgrenzung zum aktuellen Ablauf

Der aktuelle Ablauf ist auf gezielte Abfragen ausgelegt:

```text
inspect_assembly
  → find_symbol
  → get_symbol_body für ein oder mehrere stabile Symbol-IDs
```

Für eine vollständige Analyse soll der zukünftige Ablauf stattdessen lauten:

```text
inspect_assembly
  → komplette ILSpy-Projektdecompilation in den Cache
  → generiertes Projekt in Roslyn laden
  → MCP-Antwort mit sourceRoot/projectPath
  → Agent nutzt MCP-Semantik und bei Bedarf rg auf dem Cache
```

## Bewusste offene Punkte für die spätere Ausarbeitung

- Ob die vollständige Decompilation synchron beim ersten Laden oder als
  sichtbarer Warmup-Schritt erfolgt.
- Wie große Assemblys, Speicherverbrauch, Disk-Quotas und Cancellation behandelt
  werden.
- Ob Referenz-Assemblys nur als Metadata-References dienen oder ebenfalls als
  separate Projekte materialisiert werden.
- Ob eine synthetische `.slnx` zusätzlich zur von ILSpy erzeugten `.csproj`
  benötigt wird.
- Wie Original-Decompilat, Roslyn-bereinigte Quelle und Diagnosen im Manifest
  voneinander unterschieden werden.
