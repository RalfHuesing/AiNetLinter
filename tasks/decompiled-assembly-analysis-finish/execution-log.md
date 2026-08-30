# Ausführungsprotokoll: Einheitlicher Roslyn-Analysepfad

Dieses Protokoll ist append-only. Es enthält den für einen Resume-Lauf
relevanten Ereignis- und Feedbackstand; die knappe Ausführungssteuerung bleibt
in `roadmap.md`.

## 2026-08-30 — Resume-Stand und Blockerpersistenz

- Run-ID: `resume-2026-08-30-assembly-analysis`
- Betriebsart: Großkonzept
- Epic: 1 — Gemeinsame Target-, Session- und Roslyn-Route
- Status: `blocked`
- Baseline des Implementierungsstands: `a0d02cef`
- Letzte auftragsbezogene Commits: `109210f7`, `51d8f1ff`, `d99a7d98`,
  `366e2c33`
- Der aktuelle Working Tree wurde in sinnvolle Checkpoint-Commits aufgeteilt;
  es bestehen keine uncommitteten Änderungen.

### Letztes Review-Urteil: `issues`

Der unabhängige Reviewer hat den Epic-Commit nicht freigegeben. Die folgenden
beiden P1-Befunde sind der aktuelle Resume-Einstiegspunkt und müssen vor jeder
Fortsetzung von Epic 1 behoben und fokussiert erneut reviewed werden.

#### P1 — Cancellation-Propagation

Betroffene Stellen:

- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisSession.cs:77-79`
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisSession.cs:250-252`
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisRegistry.cs:128-135`

In `AssemblyAnalysisSession.RefreshAsync`, beim Aufbau des Roslyn-Snapshots
und beim Warten auf die Registry-Creation wird `OperationCanceledException`
abgefangen und in ein normales Failure-Ergebnis umgewandelt. Ein abgebrochener
oder timeoutender MCP-Aufruf erscheint dadurch als regulärer Analysefehler,
statt die kooperative Cancellation an den aufrufenden Layer weiterzugeben.

Der beabsichtigte Vertrag ist:

1. bereits erworbene Ressourcen best-effort und isoliert bereinigen,
2. danach `OperationCanceledException` weiterwerfen,
3. bei einem shared Creation-Wait nur den abbrechenden Caller vom Warten lösen;
   die gemeinsame Creation darf weiterlaufen, sofern sie nicht selbst beendet
   wurde.

Erforderliche Korrektur: Die Cancellation-Catches müssen nach der notwendigen
Cleanup-Logik erneut werfen beziehungsweise die Caller-Cancellation von einem
echten Creation-Abbruch unterscheiden. Ergänzende Tests müssen mindestens
Session-Refresh, Registry-Lease/Creation-Wait und Cleanup-Verhalten abdecken.

#### P1 — Assembly-Identität bei `get_type_hierarchy`

Betroffene Stellen:

- `src/AiNetLinter/Mcp/Tools/SymbolGraph/GetTypeHierarchyTool.cs:38`
- `src/AiNetLinter/Mcp/Registration/SymbolGraphToolRegistrations.cs:163-170`

Assembly-Symbol-IDs sind im gemeinsamen Pfad an Hash und Generation gebunden.
Die `get_type_hierarchy`-Ausführung ruft den Resolver derzeit jedoch ohne
`state.AssemblySymbolIdentity` auf:

```csharp
ResolveSymbolAsync(solution, symbolIdentifier, ct)
```

Der Assembly-Dispatcher übergibt zwar den Lease, aber nicht die Identität bis
zum Resolver. Dadurch können gültige verpackte Assembly-IDs abgelehnt werden;
unverpackte oder alte IDs können die Hash-/Generation-Prüfung umgehen.

Erforderliche Korrektur:

```csharp
ResolveSymbolAsync(solution, symbolIdentifier, ct, state.AssemblySymbolIdentity)
```

Zusätzlich ist ein Route-Test erforderlich, der eine aktuelle Assembly-ID
akzeptiert und eine alte ID nach A→B→A als stale ablehnt. Projekt-IDs müssen
unverändert bleiben.

### Bereits verifizierte Invarianten

- Registry-Fingerprint wird je Retry neu gelesen; Churn ist begrenzt und
  fail-closed.
- Registry-Generationen sind über Entry-Ersetzungen hinweg monoton; A→B→A-
  Stale-ID-Schutz ist direkt getestet.
- Creation Barrier, Lease-Drain, aktive Leases bei Dispose und Cleanup-
  Isolation sind in gezielten Tests grün.
- mtime-only-Reuse, DLL-Refresh und Trust-Prüfungen sind vorhanden.
- `metrics_tree` und gemeinsame MCP-/Roslyn-Routen sind vorhanden.
- AIContextFootprints liegen nach der Host-Kompositionsaufteilung unter den
  Grenzwerten; `safeguard` meldete zuletzt 10/10 und `get_violations` 0.

### Letzte Verifikation

- `dotnet build AiNetLinter.slnx --no-restore`: erfolgreich, 0 Warnungen,
  0 Fehler.
- FastTests Non-Stress: 2.202 bestanden, 2 übersprungen.
- Gezielte IntegrationTests: 63 bestanden.
- Ein früherer vollständiger Integration-Non-Stress-Lauf meldete 372
  bestandene Tests; der jüngste vollständige Lauf blieb bei Long-Running-
  Daemon-/JSON-RPC-Tests ohne Abschluss und ist daher nicht als aktueller
  vollständiger grüner Abschlussnachweis zu werten.
- `git diff --check`: sauber.
- Keine untersuchte Assembly wurde ausgeführt; keine externen Repositories
  oder Source-Repositories wurden verändert.

### Resume-Vertrag

1. Roadmap und diesen Blocker zuerst lesen; `correction_round` bleibt bei 5,
   `cycle_state` bleibt `blocked`.
2. Nicht stillschweigend einen sechsten Epic-1-Korrekturversuch starten. Eine
   Fortsetzung benötigt eine explizite Nutzerentscheidung oder einen neuen
   Lauf mit bewusst zurückgesetztem Budget.
3. Bei autorisierter Fortsetzung: frischen Implementierer und danach frischen
   Reviewer starten, die beiden P1s gezielt testen, anschließend die
   vollständigen Abschluss-Gates erneut ausführen.

## 2026-08-30 — Blocker durch gezielten Follow-up behoben

- Der Nutzer hat drei Feedback-Dateien aus frischen, sequenziellen Chats
  bereitgestellt: zwei Implementierungsberichte und einen unabhängigen
  Reviewbericht.
- `feedback-P1-Cancellation-Fix.md` dokumentiert die Weitergabe von
  `OperationCanceledException`, Cleanup und Shared-Creation-Verhalten.
- `feedback-P1-Fix-get_type_hierarchy.md` dokumentiert die Durchreichung von
  `AssemblySymbolIdentity`, den A→B→A-Test und die unveränderten Projekt-IDs.
- `feedback-review-P1-fixes.md` enthält das unabhängige Urteil `approved`.
- Die Fixes sind in `148ac0c3` und `b1a461f3` committed; die Feedback-Dateien
  sind in `cc860c1f` und `ee743610` committed.
- Build, gezielte Tests, `safeguard` 10/10 und `get_violations` 0 sind für den
  Follow-up-Scope dokumentiert.

### Resume-Entscheidung

Der fachliche Blocker von Epic 1 ist geschlossen. Die historische Grenze von
fünf Korrekturrunden bleibt nachvollziehbar erhalten; sie wird nicht gelöscht
oder rückwirkend umetikettiert. Für das nun gestartete Epic 2 beginnt ein neuer
Korrekturzähler bei `0`. Vor dem nächsten Implementierer sind Roadmap und dieses
Ereignis committed; Epic 2 läuft mit frischen, strikt sequenziellen Rollen
weiter.
