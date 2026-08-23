# Review: Konzept 11 — Projektregistry + Daemon-Modus

Insgesamt ein außergewöhnlich durchdachtes Konzept — das Self-Audit hat viele typische Fallen bereits geschlossen. Die folgenden Punkte sind keine Killerargumente gegen den Ansatz, aber Stellen, an denen das Konzept vor der Umsetzung noch geschärft werden sollte.

---

## 🔴 Echte Fehler / Widersprüche zum Code

### 1. `Resolve` ist synchron spezifiziert, aber der Solution-Load ist asynchron

Das Konzept zeigt in A.4:
```csharp
internal McpCodeGraphServer Resolve(string projectRoot);
```

Ein Registry-MISS muss aber eine Solution laden — das ist ein `async`-Vorgang (Sekunden bis Minuten, siehe [`McpCodeGraphServer`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/McpCodeGraphServer.cs#L68) `Task.Run(() => loadFunc(...))`). Eine synchrone Signatur erzwingt entweder:
- `.GetAwaiter().GetResult()` (Blocking, Deadlock-Risiko unter SynchronizationContext), oder
- Die Methode gibt sofort eine Instanz zurück, die intern noch lädt (wie heute via `LoadState == Loading`)

Das Konzept beschreibt den zweiten Fall im Text (Load-Dedupe-Abschnitt: „parallele Erst-Calls awaiten dieselbe Task"), aber die Signatur und das Wiring-Beispiel im Closure (`_registry.Resolve(projectRoot)`) sind **synchron**. Das passt nicht zusammen.

> [!IMPORTANT]
> **Klären:** Entweder `Resolve` wird `async Task<McpCodeGraphServer>` (dann müssen alle Tool-Lambdas async awaiten), oder `Resolve` gibt synchron eine Instanz im `Loading`-Zustand zurück (dann bleibt das heutige `McpToolResults.Loading()`-Pattern erhalten, aber der Load-Dedupe-Text mit „awaiten dieselbe Task" ist irreführend). Empfehlung: Sync-Rückgabe + Loading-State, weil das den bestehenden Tool-Dispatch nicht aufbricht.

### 2. `GetCurrentSolution()` hält den Lock während des gesamten Refresh

[`McpCodeGraphServer.GetCurrentSolution()`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/McpCodeGraphServer.cs#L218-L247) ist `lock(_lock) { ... RefreshStaleDocuments(); ... }` — der Lock umfasst den gesamten Staleness-Check + Refresh. Das Konzept behauptet unter „Gleichzeitigkeit & Snapshot-Semantik":

> „Der Staleness-Check serialisiert unter dem Instanz-Lock, Analysen laufen außerhalb des Locks auf unveränderlichen Roslyn-Solution-Snapshots"

Das ist korrekt als Beschreibung des IST-Zustands, aber es bedeutet: **Mehrere Clients am selben Key serialisieren trotzdem auf dem Instanz-Lock** bei jedem Call (wegen `GetCurrentSolution`). Bei compute-intensiven Refreshes (viele geänderte Dateien) blockieren parallele Clients. Das ist kein Bug, aber das Konzept suggeriert mehr Parallelität als tatsächlich stattfindet.

> [!NOTE]
> **Empfehlung:** Dokumentiere explizit, dass der Staleness-Check serialisierend ist und parallele Clients am selben Key hier kurz warten. Das ist ein bewusster Trade-off (Konsistenz > Throughput), sollte aber nicht verschwiegen werden.

---

## 🟡 Architektur-Schwächen / Untersspezifiziertes

### 3. Config-Auflösung bei Registry-MISS ist unklar

Heute löst [`McpServerCommand.RunAsync`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Commands/McpServerCommand.cs#L49-L61) `rules.json` auf und baut daraus `Config`, `MaxLineCount`, `UsedDefaultConfig`, `ResolvedConfigPath`. Diese Logik ist **McpServerCommand-spezifisch** (benutzt `LinterArgs`, `TryResolveRulesJsonPath`, etc.).

Bei einem Registry-MISS muss `ProjectRegistry.Resolve` eine neue `McpCodeGraphServer`-Instanz bauen. Dafür braucht sie die komplette Config-Pipeline — aber die steckt heute in `McpServerCommand`. Das Konzept sagt nur:

> „ProjectDefinitionLoader liest `ainetlinter.project.json`, verlangt beide Felder"

Aber **wer lädt die `rules.json` und baut die `Config`/`MaxLineCount`**? Der `ProjectDefinitionLoader` gibt nur Pfade zurück. Die eigentliche Config-Materialisierung (`ConfigLoader.TryLoadConfig`, `MetricsConfig`-Defaults) muss irgendwo passieren — vermutlich in der Registry selbst oder in einer neuen Factory.

> [!IMPORTANT]
> **Klären:** Expliziter Umsetzungspfad für die Config-Materialisierung beim Registry-MISS. Sonst dupliziert der umsetzende Agent die Logik aus `McpServerCommand` oder vergisst Teile.

### 4. `reload_config` im Multi-Projekt-Kontext: welche `rules.json`?

Heute hat `reload_config` einen optionalen `configPath`-Parameter. Ohne den sucht es neben der Solution. Im neuen Modell mit `projectRoot`-Pflicht für `reload_config`:
- Der Agent schickt `projectRoot` + optional `configPath`
- Was passiert, wenn `configPath` fehlt? Das Konzept sagt „es wirkt auf den EINEN per projectRoot adressierten Key" — aber sucht es dann den `rules`-Pfad aus der Definitionsdatei oder neben der Solution?

> [!NOTE]
> **Schärfen:** `reload_config` ohne `configPath` sollte den `rules`-Pfad aus der `ainetlinter.project.json` des betroffenen Keys neu lesen (nicht die Nachbar-Suche). Das wäre konsistent mit dem „kein Fallback"-Vertrag.

### 5. Overview-Resource mit URI-Template: SDK-Kompatibilität ungeprüft

Das Konzept plant:
> `ainetlinter://overview?projectRoot=<url-encoded>`

MCP-Resources mit **URI-Templates** und Query-Parametern sind im Standard definiert, aber nicht alle MCP-Clients unterstützen sie gleich. Das Konzept sagt selbst, dass MCP-Resources „keine Tool-Argumente nehmen" — der Query-Parameter in der URI ist ein Workaround, kein first-class Feature.

> [!WARNING]
> **Risiko:** Prüfen, ob das `ModelContextProtocol` C#-SDK URI-Templates mit Query-Parametern korrekt handhabt (Resource-Matching, Template-Expansion). Ob Hermes/Claude Code/Cline den Query-Parameter korrekt befüllen, ist eine weitere offene Frage. Fallback-Plan: Overview wird ein Tool (widerspricht dem Non-Goal „keine neuen Tools", aber besser als eine kaputte Resource).

### 6. Thin-Client „opake Byte-Pump" vs. MCP Streamable HTTP Spec Drift

Das Konzept designt den Thin-Client als transparente Byte-Pump, die MCP-Inhalte nicht interpretiert. Das ist elegant, aber:
- Wenn das MCP-SDK auf der Client-Seite Framing-Änderungen bekommt (z.B. MCP 2025-xx mit SSE oder HTTP), bricht die Pump.
- Der Thin-Client kann keine client-seitigen MCP-Features implementieren (z.B. sampling, elicitation), weil er den Inhalt nicht versteht.

> [!NOTE]
> **Akzeptabel**, solange stdio der einzige MCP-Transport bleibt. Aber dokumentiere explizit, dass ein Transport-Wechsel (MCP über HTTP) den Thin-Client-Ansatz fundamental ändert.

### 7. InFlightCount-Tracking (Busy-Guard): Wer zählt hoch/runter?

Das Konzept definiert den Busy-Guard über `InFlightCount > 0`, aber beschreibt nicht, **an welcher Stelle** Increment/Decrement passiert:
- Im `Resolve`-Aufruf? Dann müsste jedes Tool-Lambda ein try/finally mit Decrement haben.
- In einem Wrapper um den Tool-Dispatch? Das existiert heute nicht.

Fehlerfall: vergessenes Decrement → Key wird nie evicted → Memory-Leak.

> [!IMPORTANT]
> **Schärfen:** Expliziter Mechanismus für InFlightCount-Tracking (z.B. `IDisposable`-Guard, den `Resolve` zurückgibt: `using var lease = registry.Resolve(projectRoot); lease.Server.DoStuff()`). Sonst ist die Implementierung fragil.

### 8. Eviction „pending" + „frischer Load bei neuem Call": Ressourcen-Risiko

> „Ein neuer Call gegen einen pending-Key startet normal einen frischen Load."

Das bedeutet: Während der alte Server auf das letzte In-Flight-Ende wartet (um disposed zu werden), lädt ein neuer Load **dieselbe Solution** parallel. Zwei Roslyn-Workspaces für dasselbe Projekt gleichzeitig im RAM — das kann bei großen Solutions GB kosten.

> [!WARNING]
> **Bewusstes Risiko?** Wenn ja, dokumentieren. Wenn nein: Alternative wäre, dass der neue Call den pending-Key „adoptiert" (Pending-Flag zurücksetzen, Touch erneuern) statt einen frischen Load zu starten.

---

## 🟢 Kleinere Punkte / Schärfungen

### 9. Named Pipe ACL: `PipeSecurity` ist Windows-only

`NamedPipeServerStream` mit ACL (`PipeSecurity`) ist ein Windows-only API. Auf Linux/macOS gibt es Unix Domain Sockets mit Dateisystem-Permissions, aber keine `PipeSecurity`. Das Konzept sagt „POSIX-kompatibel benennbar" — das Benennen ja, aber die ACL-Absicherung nicht.

> **Schärfen:** Entweder explizit „Windows-only in v1" oder den ACL-Code hinter einem `RuntimeInformation.IsOSPlatform`-Guard planen.

### 10. Daemon-Spawn: „detached, ohne Parent-Bindung"

Auf Windows heißt „detached Spawn" typischerweise `Process.Start` mit `UseShellExecute = false` und ohne stdin/stdout-Redirection. Aber:
- Wer erbt die Console? `CreateNoWindow = true`?
- Wie verhindert man, dass der Daemon an die Console des Thin-Client gebunden bleibt (Windows Job Objects)?
- Was passiert bei Terminal-Schließung?

> **Schärfen:** `ProcessStartInfo`-Konfiguration (mindestens `CreateNoWindow`, `RedirectStandard*`-Flags) explizit spezifizieren.

### 11. Handshake: `shutdown` bei Version-Mismatch — MCP-Level oder Pipe-Level?

Der Handshake ist **vor** dem MCP-Durchsatz. `shutdown` ist aber ein MCP-Lifecycle-Verb. Wenn der Thin-Client vor MCP-Session-Aufbau ein `shutdown` sendet — ist das ein Pipe-Level-Befehl (eigenes Framing) oder ein MCP-Request?

> **Klären:** Eigenes Pipe-Level-Kommando im Handshake-Protokoll (empfohlen, weil MCP-Session noch nicht besteht).

### 12. `ServerInstructions.Text`: Byte-Limit

[`ServerInstructions.MaxUtf8Bytes = 2_557`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/ServerInstructions.cs#L14) — der neue `projectRoot`-Vertrag + Definitionsdatei-Vertrag muss in dieses Budget passen. Der aktuelle Text ist schon dicht. Der Umsetzende muss eventuell kürzen oder das Limit erhöhen.

> **Hinweis:** Budget-Rechnung für den neuen Instructions-Text aufstellen.

### 13. Testkatalog: Fehlender Test für Race zwischen Eviction-Pending und neuem Load

A.8 listet umfangreiche Tests, aber das Szenario „Call gegen pending-Key startet frischen Load" (Punkt 8 oben) hat keinen expliziten Test.

> **Ergänzen:** Unit-Test für „Call gegen eviction-pending Key erzeugt neuen Load; alter Key wird nach In-Flight-Ende disposed; beide Loads koexistieren kurzfristig".

### 14. Staleness-Refresh im Daemon: N Clients × M Keys = O(N·M) Refreshes?

Jeder `GetCurrentSolution()`-Call triggert einen Staleness-Check. Bei 5 Clients, die jede Sekunde auf denselben Key zugreifen, sind das 5 Staleness-Checks/Sekunde (serialisiert unter Lock). Das ist heute mit einem Client akzeptabel, kann aber im Multi-Client-Daemon zum Bottleneck werden.

> [!NOTE]
> **Spätere Optimierung:** Staleness-Cache mit kurzer TTL (z.B. „maximal 1 Check/Sekunde pro Key") wäre eine einfache Entschärfung. Nicht zwingend in v1, aber als bekanntes Follow-up dokumentieren.

### 15. MRU-State Atomizität

> „geschrieben bei jedem Touch (debounced)"

Wenn der Daemon mitten im Schreiben abstürzt, ist die Datei korrupt. Das Konzept sagt „tolerant lesen" — aber WIE tolerant? Partial JSON? Leere Datei?

> **Schärfen:** Write-to-temp-then-rename Pattern (atomares Dateisystem-Swap) oder explizit „leere/defekte Datei = kein Warmup, kein Fehler".

---

## Zusammenfassung

| Schwere | # | Thema |
|---|---|---|
| 🔴 Fehler | 1 | `Resolve`-Signatur sync vs. async Load |
| 🔴 Unscharf | 2 | Lock-Serialisierung bei `GetCurrentSolution` in der Gleichzeitigkeits-Beschreibung |
| 🟡 Untersp. | 3 | Config-Materialisierung beim Registry-MISS |
| 🟡 Untersp. | 4 | `reload_config` ohne configPath: welcher Rules-Pfad? |
| 🟡 Risiko | 5 | URI-Template/Query-Parameter für Overview-Resource |
| 🟡 Hinweis | 6 | Byte-Pump vs. MCP-Spec-Drift |
| 🟡 Untersp. | 7 | InFlightCount-Tracking-Mechanismus |
| 🟡 Risiko | 8 | Zwei parallele Workspaces bei pending-Eviction + neuem Load |
| 🟢 Schärfung | 9 | Named Pipe ACL ist Windows-only |
| 🟢 Schärfung | 10 | ProcessStartInfo für detached Spawn |
| 🟢 Schärfung | 11 | `shutdown` im Handshake: Pipe-Level oder MCP-Level? |
| 🟢 Hinweis | 12 | ServerInstructions Byte-Budget |
| 🟢 Lücke | 13 | Fehlender Test für pending-Eviction-Race |
| 🟢 Follow-up | 14 | Staleness-Throttle bei vielen Clients |
| 🟢 Schärfung | 15 | MRU-State Atomizität |

Punkte **1, 3, 7** sind die kritischsten — sie bestimmen, wie der umsetzende Agent die Registry-Klasse tatsächlich baut. Ohne Klärung ist autonome Umsetzung fragil.
