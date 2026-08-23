---
status: ready
type: konzept (epic-roadmap, autonom umsetzbar)
project_kind: brownfield
estimated_scope: large
priority: P1
agent_role: .agents/Agent-Scaffolding/dev-loop/drift-loop/orchestrator.md
rules_dir: .agents/rules
last_updated: 2026-08-23
open_questions: []
herkunft: "Diskussion 2026-08-22 (ox-alpha + Nutzer): Global registrierter MCP-Server mit hartkodierter --path-Bindung versagt im Multi-Projekt-/Multi-Agent-Alltag (stille Fehl-Bindung, wiederholte Solution-Loads, Loading-Waits). Epic-Roadmap in zwei Epics: A Projektregistry (transportneutral), B Daemon-Modus."
---

# Konzept 11 (Epic-Roadmap): Projektregistry + Daemon-Modus

## Zweck dieses Dokuments

Dieses Dokument ist die **alleinige Grundlage** für die autonome Umsetzung. Es enthält beide Epics in
Abhängigkeitsreihenfolge, verifizierte Code-Findings mit Belegen, konkrete Umsetzungspfade, Testkatalog,
Doku-Pflichten und harte Schnittstellenverträge. Der umsetzende Agent muss keine weiteren Kontexte
beschaffen; Widersprüche zwischen diesem Dokument und dem Code sind zugunsten des Codes zu melden
(als Blocker), nicht still aufzulösen.

## Intention

Der Nutzer arbeitet täglich an ~4 C#/.NET-Solutions mit **mehreren** Agent-Clients gleichzeitig
(Claude Code, Cline, Hermes Desktop, weitere). Heute gilt:

- Jeder Client (bzw. je nach Client jeder Chat/jede Session) spawnt einen eigenen Stdio-MCP-Prozess.
- Jeder Prozess lädt seine Solution komplett neu (Roslyn/MSBuild: Sekunden bis Minuten).
- Agenten warten regelmäßig auf „Solution wird noch geladen".
- Bei Hermes teilt sich ein Host alle Chats in EINEN Prozess mit EINER hart eingerosteten Bindung —
  arbeitet der Nutzer in Projekt B, liefert der Server korrekt aussehende, aber falsche Ergebnisse
  für Projekt B, ohne jeden Fehlerhinweis.

**Zielbild nach beiden Epics:** Ein langlebiger lokaler Daemon hält pro Projekt genau einen warmen
Roslyn-Workspace (letzte N Projekte, Idle-basiert verwaltet). Alle Clients verbinden sich über einen
Thin-Client-Stdio-Prozess mit ihm. Die Projektbindung ist **deterministisch pro Aufruf** über eine
explizite Definitionsdatei im Projektroot — nie über cwd, Heuristik oder Chat-Kontext. Die
Client-Konfiguration reduziert sich auf den Binary-Aufruf ohne projektspezifische Parameter und wird
nie wieder angefasst.

### Warum überhaupt so weit? (Wiedereröffnung des Entscheidungsregisters)

Zwei Festlegungen aus `90_bewusst-nicht-umsetzen/Konzept.md` werden hiermit **belegt wiedergeöffnet**
(die dort geforderte Bedingung „praxis-belegtes Scheitern"/„neue Hostdaten" ist erfüllt):

| Festlegung | Belegtes Scheitern | Folge |
|---|---|---|
| **§D.4 Multi-Solution zurückgestellt** („Eine Solution pro Prozess ist sauber; mehrere Server-Instanzen") | Hermes-Realität: ein Prozess pro Host, von allen Chats geteilt — mehrere Instanzen pro Profil erzeugen doppelte Toolkataloge, und Chats desselben Profils teilen sich trotzdem eine Bindung. Multi-Server-Setup scheitert praktisch. | Epic A |
| **§C.5 Kein Detached-Daemon** („stdio reicht") | Mehrere Clients spawnen unkoordiniert Prozesse (pro Chat/Session/Fenster — clientabhängig); jede Spawnpolitik zahlt volle Solution-Loads und Loading-Waits. stdio bleibt als Client-Vertrag erhalten, aber der residente Zustand wandert in einen geteilten Daemon. | Epic B |

Beide Wiederöffnungen sind dokumentiert (dieses Dokument); das Register selbst wird im Zuge der
Umsetzung ergänzt.

## Was wir NICHT machen (globale Non-Goals)

| Non-Goal | Begründung |
|---|---|
| HTTP/TCP-Transport, Ports, Auth, Token | Named Pipes decken lokal alles ab (ACL auf aktuellen User); kein Firewall-/Angriffs-Thema. `90 §C.6`. |
| Windows-Service / Autostart / Taskplaner | Idle-Exit macht Immortalität unnötig; Autostart erst bei belegtem Ärgernis. |
| Remote-/Multi-User-Fähigkeit | Lokales Einzelnutzer-Werkzeug. |
| Auto-Init aus cwd, Heuristiken, Rate-Vorschläge | Reproduziert die stille Fehl-Bindung; Fehler sind deterministisch mit Bauanleitung. |
| Lock-/Claim-Files zwischen Prozessen | Stale-Locks, Cross-Process-Konflikte, kein Nutzen. |
| Deprecations-, Warn- oder Kompatibilitätsschichten | **Harte Cuts**: alte Parameter = unbekanntes Argument = harter Fehler. Interne Toolkette, alle Client-Konfigurationen sind selbst verwaltet und werden im selben Task umgestellt. |
| Umbau der Batch-Pipeline | Batch-Lint (`--path`/`--config`) bleibt unverändert; alle Schnitte betreffen nur den MCP-Modus. |
| Neue Analyse-Tools, Tool-Removal, mutierende Tools | Toolbestand (26) bleibt; read-only bleibt; nur Verträge ändern sich (`90 §A/B/D`). |
| RAG/Embeddings/Vektorspeicher, DI-Container | Weiter gültig (`00_uebersicht` Architektur-Entscheidungen). |
| FileSystemWatcher für Definitions-/State-Reload | Lesen nur beim Key-Load bzw. Daemon-Start; Refresh über Eviction/Neustart (konsistent mit Konzept 02: Messen vor Watchern). |
| `init`-Generator-Kommando | Template im Fehlertext macht es überflüssig (siehe Self-Service-Vertrag). |

## Verifizierte Code-Findings (Stand 2026-08-22, gegen HEAD geprüft)

Diese Findings wurden direkt im Quellcode verifiziert; sie bestimmen den Umsetzungsweg:

| # | Finding | Beleg |
|---|---|---|
| F1 | **`McpCodeGraphServer` ist bereits vollständig instanzbasiert.** Sämtlicher Zustand (`_catalog`, `_fileState`, `_lock`, Config-Trio, Staleness-Zähler) liegt in Instanzfeldern; kein statisches Mutable-State. Die Klasse wird für N Projekte NICHT umgebaut, sondern N-mal instanziiert. | `McpCodeGraphServer.cs:25-75, 27-41` |
| F2 | **Die heutige „Globalität" steckt ausschließlich im Wiring, an zwei Stellen:** `McpServerCommand.RunAsync` erzeugt eine Instanz prozessweit; `McpServerOptionsFactory.Create(mcpState)` bäckt sie per Closure in alle Tool-Lambdas und die Resource-Collection. | `McpServerCommand.cs:62-70`; `McpServerOptionsFactory.cs:27-57` |
| F3 | **Tools erreichen den State per Delegate-Closure** (`McpServerTool.Create((args…) => XxxTool.ExecuteAsync(mcpState, …))`). Ersetzt man das eingefangene Objekt durch einen Resolver und ergänzt `projectRoot` als nicht-defaulteten Lambda-Parameter, erscheint dieser automatisch als Pflichtfeld im JSON-Schema — der SDK-Kontrakt ändert sich nicht. | `SymbolGraphToolRegistrations.cs:44-51` (find_symbol als Muster) |
| F4 | **TestKit kann bereits N Server bauen:** `McpInMemoryTestContext.CreateServer()` konstruiert Instanzen per Options-Record — Registry-Tests laufen in-memory ohne Prozessstart. | `FastTests/Fixtures/McpInMemoryTestContext.cs:25-29` |
| F5 | **Lifecycle-Bausteine existieren pro Instanz:** atomarer Config-Hot-Swap (`ReloadConfig`), Solution-Reload mit Dispose-nach-Swap (`ReloadSolutionAsync`), volles `DisposeAsync` inkl. LoadTask-Abbruch, Health-Zähler je Instanz (`RefreshCount`, `LastStalenessStats`, `Uptime`). Eviction = Registry entfernt Key + ruft `DisposeAsync`. | `McpCodeGraphServer.cs:150-158, 165-198, 249-295, 129-141` |
| F6 | **Single-Source-of-Truth für agentensichtbare Verträge:** `ServerInstructions.Text` geht in den initialize-Handshake — projectRoot-Vertrag und Definitionsdatei-Vertrag stehen einmalig dort, nicht 26× in Description-Duplikaten. | `ServerInstructions.cs:9-13`; `McpServerOptionsFactory.cs:31` |
| F7 | **Lint-Grenzen:** `MaxAIContextFootprint` auf `McpCodeGraphServer` (Refresh/Factory/Registrations deshalb bereits ausgelagert) und `MaxConstructorDependencies: 5` (Options-Record statt langer Parameterlisten). Neue Klassen entsprechend schlank bzw. Options-Record. | Klassendoku `McpCodeGraphServer.cs:15-24`; `McpCodeGraphServerOptions.cs:71-74`; Kommentar `:43-45` |
| F8 | **`ResolveSolutionPathOrError` wird im MCP-Pfad nicht mehr gebraucht:** Die Definitionsdatei benennt Solution UND Rules explizit; die projectRoot-Prüfung ist streng und trivial (Verzeichnis existiert + Definitionsdatei existiert). Auto-Suche/Mehrdeutigkeit bleiben Batch-only. (Korrektur zum ersten Entwurf dieses Konzepts.) | `McpServerCommand.cs:232-280` |
| F9 | **Observability-Grundlage vorhanden:** `RalfHuesing.Mcp.Observability` schreibt Call-Logs unter `%LOCALAPPDATA%\RalfHuesing\McpObservability\ainetlinter\<Datum>\`; `get_server_health` liest sie aus. Erweiterung (Connection-ID, Modus) baut darauf auf. | `csproj` Paketliste; Health-Output vom Live-Test 2026-08-22 |

### Abhängigkeiten (Entscheidung: BCL-only, kein neues NuGet)

Für beide Epics ist **alles im BCL-Lieferumfang von .NET 9 enthalten** — es werden KEINE neuen
NuGet-Pakete eingeführt:

- `System.Text.Json` — Definitionsdatei, MRU-State, Handshake-Framing (bereits im Einsatz).
- `System.IO.Pipes` (inkl. ACL-Support, auf Windows inbox) — Daemon-Transport.
- `System.Diagnostics.Process` — detached Daemon-Spawn durch den Thin-Client.
- `TimeProvider` (BCL seit .NET 8) — injizierbare Clock für TTL-/Idle-Exit-Tests.
- `ModelContextProtocol` (bereits referenziert) — wird im DAEMON je Verbindung als MCP-Session gegen
  die geteilte Registry wiederverwendet ⇒ Tool-Schemas können nicht driften.

Bewusst abgelehnt: `StreamJsonRpc` oder vergleichbare RPC-Frameworks — der Thin-Client pumpt opake
Bytes (kein RPC-Bedarf), der Daemon spricht MCP nativ über das bestehende SDK. Einziger externer
Touchpunkt: `RalfHuesing.Mcp.Observability` (eigenes Paket) benötigt ggf. eine kleine Erweiterung für
`connectionId`/`mode`-Felder. Vorgehen: prüfen, ob die bestehende API beliebige Metadaten erlaubt;
wenn ja, auf Anwendungsebene anreichern; wenn nein, Paket-Version minor-bumpen (eigener Scope, eigener
Commit).

### Verzeichnis- und Klassenstruktur (verbindlich)

Namenskonventionen wie im Bestand: `internal sealed`, file-scoped namespaces, Konstruktor-Abhängigkeiten
über Options-Records (F7).

```text
src/AiNetLinter/
├── Program.cs                          [ÄNDERN] Routing: --mcp-server | --daemon-start | Batch (unverändert)
├── Commands/
│   └── McpServerCommand.cs             [ÄNDERN] hält ProjectRegistry statt McpCodeGraphServer;
│                                                --path/--config im MCP-Zweig entfernen; neue statische Flags
└── Mcp/
    ├── McpServerOptionsFactory.cs      [ÄNDERN] Create(ProjectRegistry registry) statt (McpCodeGraphServer)
    ├── ServerInstructions.cs           [ÄNDERN] projectRoot-/Definitionsdatei-Vertrag (einmalig, F6)
    ├── Projects/                        [NEU — Epic A]
    │   ├── ProjectDefinition.cs              record(SolutionPath, RulesPath) — absolut + existenzgeprüft
    │   ├── ProjectDefinitionLoader.cs        liest ainetlinter.project.json; Fehlerverträge A.5; kein Fallback
    │   ├── ProjectEntry.cs                   RootPath, Definition, Server, LastUsedUtc, PendingEviction
    │   ├── ProjectLease.cs                   { Server } + Dispose => Lease-Ende (InFlight-Tracking)
    │   ├── ProjectInstanceFactory.cs         Materialisiert Server-Options aus Definition (Config-Pipeline, geteilt mit Batch)
    │   └── ProjectRegistry.cs                Lease/LRU/TTL-Timer/Pending-Adoption/IAsyncDisposable
    └── Daemon/                          [NEU — Epic B]
        ├── DaemonConstants.cs                 Pipe-Name (inkl. Username-Suffix), Protokollversion
        ├── DaemonHandshake.cs                 hello/welcome-Records + Versionsvergleichslogik
        ├── DaemonPipeServer.cs                NamedPipeServerStream-Akzeptanz-Loop je Verbindung
        ├── DaemonHost.cs                      Registry + MCP-Session je Verbindung; Idle-Exit; MRU-Warmup
        ├── MruStateStore.cs                   daemon-state.json (debounced schreiben, tolerant lesen)
        ├── ThinClientProxy.cs                 Connect-or-Start → Handshake → opake Byte-Pump (stdio⇄Pipe)
        └── ThinClientLauncher.cs              detached Spawn der eigenen EXE mit --daemon-start

src/AiNetLinter.FastTests/Mcp/Projects/        [NEU] Registry-/Loader-/Eviction-Unit-Tests (F4: in-memory)
src/AiNetLinter.FastTests/Mcp/Daemon/          [NEU] Handshake-/Idle-Exit-/MRU-/Race-Unit-Tests (in-proc)
src/AiNetLinter.IntegrationTests/Mcp/Daemon/   [NEU] echte Zwei-Prozess-E2E (sparsam, siehe B.6)
```

Bewusst KEINE neuen Top-Level-Namespaces außer `AiNetLinter.Mcp.Projects` und `AiNetLinter.Mcp.Daemon`;
keine Änderungen unter `Rules/`, `Generators/`, `Core/`.

**Zielplattform (Final-Pass, Nutzerentscheidung):** AiNetLinter wird aktuell AUSSCHLIESSLICH für
Windows entwickelt und betrieben. Pipe-ACL via `PipeSecurity` ist damit Windows-only und korrekt so
gebaut (Review 9); POSIX-Portabilität (Unix Domain Sockets, ACL-Guards) ist kein Ziel dieser Epics
und wird nirgends vorbereitet. Der Daemon-Spawn nutzt `ProcessStartInfo` mit `UseShellExecute=false`,
`CreateNoWindow=true`, ohne stdout/stderr-Redirect (Daemon schreibt ins Observability-Log) — Review 10.

### Self-Audit (2026-08-22): geschlossene Lücken

Das eigene Konzept wurde gegen Laufzeit-/Betriebsrealität auditiert; folgende Punkte sind als
verbindliche Verträge ergänzt (Details an den jeweiligen Stellen):

1. Load-Dedupe: parallele Erst-Calls auf denselben Root teilen sich EINEN Load; der Registry-Lock
   deckt nie einen Solution-Load (A.4).
2. Busy-Guard für Eviction: Keys mit laufendem Call werden nicht disposet (A.7).
3. `projectRoot` muss absolut sein; relative Pfade bekommen einen harten Fehler (A.3) — der Daemon-cwd
   ist für Agenten bedeutungslos, Auflösung wäre Nichtdeterminismus.
4. Ausnahmeregelung `get_server_health`: optionaler Filter-Parameter statt Pflicht (A.3) — wohldefiniert,
   kein Raten; alle Analyse-Tools bleiben ausnahmslos pflichtparametrig.
5. Stdio-Purity: Der Thin-Client schreibt AUSSCHLIESSLICH Protokollbytes auf stdout; Diagnose geht nach
   stderr bzw. Observability (B.3).
6. Pipe-Name enthält den Benutzernamen (Multi-User-Maschine) (B.2).
7. Cancellation: Client-Disconnect bricht in-flight Calls der Verbindung ab (B.2).
8. Escape-Hinweis: Hermes filtert Env-Vars von MCP-Subprozessen — `AINETLINTER_NO_DAEMON` erreicht den
   Prozess nur via `env:`-Block der Registrierung (B.3, Doku).
9. Konfigurationsdrift: daemon-level Flags gelten daemon-weit; Divergenz wird per welcome-Handshake
   sichtbar gemacht und laut gemeldet (Details B.2).
10. Idle-Exit während laufender Loads ist ausgeschlossen (graceful, Details B.3).
11. Kalt-Load-Rennen mit parallelen Builds (`dotnet build` hält Dateien gerade offen) endet im
    bestehenden PROJECT_LOAD_FAILED-Vertrag mit Retry — bewusst KEINE Locking-Gegenmaßnahmen
    (Details End-to-End-Abschnitt).

---

# Epic A — Projektregistry (transportneutral)

## A.1 Ziel

Der MCP-Server hält **mehrere** Projekte (Solution + Rules) gleichzeitig vor, deterministisch adressiert
pro Aufruf. Kein Projektbezug mehr in der Client-Konfiguration. Dieses Epic ist vollständig
transportunabhängig — Epic B ändert am Ergebnis nichts, nur wo es läuft.

## A.2 Definitionsdatei `ainetlinter.project.json`

Liegtpflichtig im Projektroot (= Wert von `projectRoot`). Format **JSON** (keine neue Abhängigkeit:
`System.Text.Json` + bestehendes `ConfigLoader`-Muster; Konsistenz mit `rules.json`):

```json
{
  "solution": "src/AiNetLinter/AiNetLinter.slnx",
  "rules": "config/rules.json"
}
```

- `solution` (**Pflicht**): Pfad zur `.slnx`/`.sln`, relativ zur **Definitionsdatei** aufgelöst (nicht
  zum cwd — dieselbe Datei funktioniert auf jedem Checkout) oder absolut.
- `rules` (**Pflicht**): gleiche Ankerregel. **Kein Fallback, kein Raten**: Feld fehlt oder Datei
  fehlt → harter Fehler. Der Nachbar-Fallback (`TryResolveRulesJsonPath`) stirbt im MCP-Pfad ersatzlos;
  `UsedDefaultConfig` wird im MCP-Modus bedeutungslos.
- Dateiname **`ainetlinter.project.json`** — ohne führenden Punkt (Windows-Explorer blendet Dotfiles
  ohnehin nicht aus; maschinell wird exakt per Pfad geprüft; Präzedenz `global.json`, `nuget.config`).
  Suffix `-project`, weil die Datei Ziel **und** Regelwerk definiert.
- Kein `$schema`-Feld, keine optionalen Felder in v1.

## A.3 Parameter-Strategie (harter Vertrag)

| Ebene | Neu | Entfällt |
|---|---|---|
| Client-Konfiguration | `command` (+ statische, projektunabhängige Parameter wie `--mcp-log`) | `--path`, `--config` im MCP-Modus (**harter Cut**: unbekanntes Argument = harter Fehler) |
| Pro Tool-/Resource-Call | `projectRoot` (**ausnahmslos Pflicht**, string, nicht-defaultet) | — |

**Keine bedingten Ausnahmen:** Auch bei genau einem geladenen Key ist `projectRoot` Pflicht — ein
bedingter Vertrag wäre für Agenten nicht entscheidbar, weil sie den Registry-Stand des (ggf. geteilten)
Prozesses nicht kennen. Uniforme Pflicht = null Inferenzlast, deterministische Fehler, einfachere Tests.
Kosten: ~5 Tokens pro Call.

**projectRoot muss ABSOLUT sein** (Self-Audit 3): Relative Pfade → harter Fehler (`PROJECT_ROOT_INVALID`).
Der cwd des Serverprozesses ist für einen Agenten bedeutungslos (beim Daemon erst recht) — jede
relative Auflösung wäre Nichtdeterminismus. Die einzigen bewussten Ausnahmen von der Pflicht:
- `get_server_health` erhält einen OPTIONALEN Filter-Parameter (`projectRoot` angegeben → nur dieser
  Key; fehlt → alle Keys). Wohldefinierte Semantik, kein Raten; alle Analyse-Tools bleiben
  ausnahmslos pflichtparametrig.

## A.4 Architektur & Umsetzungspfad

Wegen F1/F2 ist Epic A ein **Wiring-Change plus neue Registry**, kein Refactoring der Serverklasse.

```csharp
// neu: src/AiNetLinter/Mcp/Projects/ProjectDefinition.cs
internal sealed record ProjectDefinition(string SolutionPath, string RulesPath); // absolut + validiert

// neu: src/AiNetLinter/Mcp/Projects/ProjectRegistry.cs (schlank wegen F7)
internal sealed class ProjectRegistry : IAsyncDisposable
{
    // Key = kanonisierter Root-Pfad (OrdinalIgnoreCase), Value = geladenes Projekt
    private readonly Dictionary<string, ProjectEntry> _projects = new(StringComparer.OrdinalIgnoreCase);

    // Sync-Rueckgabe (Review 1): HIT -> Touch + Lease zurueckgeben. MISS -> Definition
    // laden (harte Fehler HIER, A.5), Instanz MIT Hintergrund-Load erzeugen (LoadFunc,
    // LoadState=Loading) und sofort zurueckgeben. Bei maxProjects zuerst LRU-Eviction.
    internal ProjectLease Lease(string projectRoot);
}

// neu: src/AiNetLinter/Mcp/Projects/ProjectDefinitionLoader.cs
// liest <root>/ainetlinter.project.json, verlangt beide Felder, loest relativ zur Datei auf,
// prueft Existenz beider Zieldateien. Kein Fallback-Zweig.
```

Wiring (mechanisch, identisches Muster je Klasse):

```csharp
// VORHER (SymbolGraphToolRegistrations.cs:44-46)
(string? namePattern = null, ...) => FindSymbolTool.ExecuteAsync(mcpState, namePattern, ...)

// NACHER — Lease per 'using': Increment/Decrement strukturell paarweise (Review 7)
(// WICHTIG (Review R2/A): Das Lambda MUSS async sein und AWAiTEN — ein nacktes
 // 'return ExecuteAsync(...)' würde das using am Ende des SYNCHRONEN Scopes disposen,
 // also VOR Task-Abschluss. InFlightCount fiele auf 0, während der Call noch läuft;
 // der Busy-Guard wäre wirkungslos. 26× mechanisch repliziert — Muster unverändert lassen.)
async (string projectRoot, string? namePattern = null, ...) =>
{
    using var lease = _registry.Lease(projectRoot);
    return await FindSymbolTool.ExecuteAsync(lease.Server, namePattern, ...);
}
```

- Alle sechs Registration-Klassen (`SymbolGraph`, `FileStructure`, `Analysis`, `SymbolBody`,
  `ServerMaintenance`, `DuplicateDetection`) sowie `OverviewResourceRegistration` umstellen.
- `McpServerOptionsFactory.Create(McpCodeGraphServer mcpState)` → `Create(ProjectRegistry registry)`.
- `McpServerCommand.RunAsync` hält keine Serverinstanz mehr, sondern die Registry (+ Eviction-Timer);
  `reload_config` und `get_server_health` routen über denselben Resolver; Health aggregiert pro Key
  (F5 liefert Pro-Instanz-Werte: Root/Solution/Rules/lastUsed/RefreshCount/Staleness/Uptime).
- `reload_config` ist ein ganz normales Tool unter dem Vertrag: es wirkt auf den EINEN per
  `projectRoot` adressierten Key (Config-Hot-Swap, F5), nicht prozessweit.
  Ohne `configPath` liest es den `rules`-Pfad AUS DER Definitionsdatei des Keys neu ein (Review 4) —
  keine Nachbar-Suche, konsistent mit dem Kein-Fallback-Vertrag; mit `configPath` überschreibt es für
  diesen einen Hot-Swap.
- projectRoot-Vertrag einmalig in `ServerInstructions.Text` (F6).
  **Byte-Budget (Review 12):** `ServerInstructions` hat ein bewusstes Limit (`MaxUtf8Bytes ≈ 2557`).
  Der neue Vertragsblock wird KOMPRIMIERT gefasst, um ins Budget zu passen (Budget-Rechnung gehört
  in den Task); eine Limit-Erhöhung ist nur mit Begründung im Commit erlaubt — das Limit schützt die
  Größe des initialize-Payloads.
- Lint-Grenzen F7 einhalten (Registry-Klassen klein, Options-Records).

**Load-Dedupe & Lock-Hygiene (Review 1, entscheidungstragend):** `Lease` kehrt SYNCHRON zurück und
liefert die Instanz im `Loading`-Zustand — der Dedupe lebt im BESTEHENDEN Instanzmuster (`_loadTask`,
Adoption beim ersten Dispatch, `McpToolResults.Loading()` solange der Load läuft), NICHT in der
Registry. Der Registry-Lock deckt nur Dictionary-Zugriffe, nie einen Solution-Load. Parallele
Erst-Calls auf denselben Root erhalten dieselbe Instanz ⇒ genau EIN Load; der bestehende Tool-Dispatch
und sein Loading-Antwortmuster bleiben unangetastet. Schlägt der Hintergrund-Load fehl, antwortet der
Dispatch mit PROJECT_LOAD_FAILED; der tote Eintrag wird beim nächsten Hit an
`LoadState == LoadFailed` erkannt, entfernt und frisch geladen (kein negatives Caching). Die früher
skizzierte Registry-eigene Task-Dedupe-Map entfällt damit ersatzlos.

**Config-Materialisierung (Review 3):** Die Pipeline „rules.json laden → `ConfigLoader.TryLoadConfig` →
MaxLineCount/MetricsConfig-Defaults" existiert heute in `McpServerCommand` (Batch). Sie wandert in
eine gemeinsame Helper-Klasse (`ProjectInstanceFactory`, Baum oben), die sowohl das Batch-Kommando
als auch die Registry je Definition aufrufen — null Duplizierung, identische Semantik. Die
Existenzprüfung der rules bleibt im Loader (RULES_NOT_FOUND), das Laden in der Factory.

**Key-Kanonisierung (Final-Pass):** Key = `Path.GetFullPath(projectRoot)` mit abschließenden
Trennern (`\` und `/`) entfernt. GetFullPath vereinheitlicht Groß/Kleinschreibung-Normalisierung
nicht selbst — der Dictionary-Comparer `OrdinalIgnoreCase` (oben) deckt sie; entscheidend ist, dass
`C:/repos/foo` und `C:\repos\foo` (unterschiedliche Clients schreiben das unterschiedlich) auf
denselben Key mappen. Unit-Test fixiert genau diese Äquivalenz.

**Hinweis zur Erreichbarkeit von PROJECT_ROOT_REQUIRED (Final-Pass):** Das MCP-SDK validiert
Pflichtparameter üblicherweise bereits am JSON-Schema und lehnt fehlendes `projectRoot` selbst ab —
der eigene Fehlercode ist Defense-in-Depth für Hosts mit laxer Validierung, kein Normalfall. Der
Contract-Test prüft das Schema (required), NICHT die Erreichbarkeit des Codes über den SDK-Pfad.

**Overview-Ressource (Final-Pass-Entscheidung):** MCP-Resources nehmen keine Tool-Argumente; die
bisherige statische URI ist bei mehreren Projekten nicht adressierbar. Die Ressource erhält ein
URI-Template mit Query-Parameter: `ainetlinter://overview?projectRoot=<url-encoded>` (URL-kodierter,
absoluter Pfad; fehlt/ungültig → gleiche Fehlerverträge wie bei Tools). KEIN neuer Tool-Ersatz — die
Toolanzahl bleibt eingefroren (Non-Goal „keine Tool-Removal/neue Tools" bleibt unangetastet).
**Rückfallplan (Review 5):** URI-Template-Unterstützung variiert je MCP-Client. Der Umsetzende prüft
beim Epic-A-Bau das SDK-Matching (Resource-Template-Expansion) und verifiziert in Hermes + Claude Code
live. Scheitert ein Host am Query-Parameter, wird die Overview als TOOL exponiert (einzige erlaubte
Ausnahme vom Tool-Freeze — besser als eine kaputte Resource); die Entscheidung wird im Task-Log
dokumentiert.

## A.5 Fehlerverträge (alle deterministisch, englisch, mit Bauanleitung)

| Fall | Code (neu) | Textinhalt |
|---|---|---|
| `projectRoot` fehlt | `PROJECT_ROOT_REQUIRED` | Parameter ist ausnahmslos Pflicht |
| `projectRoot` relativ | `PROJECT_ROOT_INVALID` | Absoluten Pfad verlangen (Server-cwd ist bedeutungslos) |
| Definitionsdatei fehlt | `PROJECT_NOT_INITIALIZED` | Erwarteter Pfad + **kopierfähiges Minimal-Template** (unten) |
| Feld fehlt / JSON defekt | `PROJECT_DEFINITION_INVALID` | Betroffenes Feld + Definitionsdatei-Pfad |
| Solution nicht gefunden | `SOLUTION_NOT_FOUND` | Aufgelöster absoluter Pfad (Anker: Definitionsdatei) |
| Rules nicht vorhanden | `RULES_NOT_FOUND` | Aufgelöster absoluter Pfad; kein Default |

Vorgeschriebener Template-Block in `PROJECT_NOT_INITIALIZED` (exakt dieser Aufbau):

```text
Create <root>/ainetlinter.project.json with:
{
  "solution": "<path/to/your.slnx or .sln>",  // relative to this file, or absolute
  "rules":    "<path/to/rules.json>"          // relative to this file, or absolute; MUST exist
}
Then retry the call with the same projectRoot.
```

`AMBIGUOUS_SOLUTION` entfällt im MCP-Pfad (konkrete Dateiangaben schließen Mehrdeutigkeit konstruktiv
aus; bestehende Logik bleibt Batch-only).

## A.6 Self-Service: Agenten erzeugen die Definitionsdatei selbst

Ein Coding-Agent soll die Datei **ohne menschliche Hilfe** anlegen können. Wissenstransport über drei
Kanäle:

1. **Fehlertext (primär, in-band):** Template oben → deterministischer Selbstheilungs-Loop:
   Call scheitert → Agent findet Solution/Rules selbst im Baum, legt Datei an → Retry gelingt.
2. **`ServerInstructions.Text`:** eine Zeile zum Dateivertrag vor dem ersten Aufruf.
3. **`Docs/agent-api.md`** (Referenzabschnitt „ainetlinter.project.json": Feldtabelle, Ankerregel,
   Beispiele) plus AGENTS.md-Abschnitt dieses Repos.

## A.7 Eviction & RAM-Hygiene

- **Idle-TTL:** Timer (Default 5 Min Takt) disposed Keys mit `lastUsed > 45 Min`
  (konfigurierbar: `--mcp-project-ttl-minutes`, `--mcp-max-projects` — statische Parameter, erlaubt
  in Client-Konfiguration, da projektagnostisch). Reload beim nächsten Call = frischer Stand garantiert.
  Flag-Werte werden als **decimal Minuten** geparst (InvariantCulture, z. B. `0.05` ≈ 3 s) — so können
  Integrationstests kurze TTLs setzen; ungültiger Wert → harter Startfehler.
- **maxProjects (Default 4) + LRU:** neuer Key bei vollem Registry verdrängt ältesten.
- **FAILED-Einträge räumen sich schnell auf (Review R2/B):** Der TTL-Tick entfernt Einträge mit
  `LoadState == LoadFailed` SOFORT (unabhängig von lastUsed) — es gibt keinen Grund, einen toten
  Eintrag 45 Min zu halten. Zwischen zwei Ticks bleibt er als FAILED-Marker adressierbar (Hit-Pfad).
- Caveats (dokumentiert): .NET gibt GC-RAM träge ans OS (Kurve sägt); Solution-Reload kostet Sekunden
  bis Minuten — TTL nicht aggressiv setzen; Dispose muss Workspace-/Catalog-Referenzen konsequent
  freigeben (Muster F5).
- Bestehende Prozesshygiene unberührt: `--parent-pid`-Reaper bleibt (in Epic B bewusst NICHT im Daemon).

**Busy-Guard für Eviction** (Self-Audit 2): Ein Key mit laufendem Call (`InFlightCount > 0`) wird weder
von TTL noch LRU disposet — eine ObjectDisposedException mitten in einer Analyse ist der schlimmstmögliche
Fehlermodus. Umsetzung: Eviction markiert Key als „eviction pending" und disposed erst nach dem letzten
in-flight Call (oder beim nächsten Idle-Tick). Ein neuer Call gegen einen pending-Key ADOPTIERT den
Eintrag (siehe unten) — er startet keinen frischen Load.

**InFlight-Tracking-Mechanismus (Review 7, verbindlich):** Das Zählen passiert STRUKTURELL im
Lease-Lifetime, nie manuell: `ProjectRegistry.Lease()` inkrementiert, `ProjectLease.Dispose()`
dekrementiert; jedes Tool-/Resource-Lambda nutzt `using var lease = …` (Muster oben) — vergessen kann
das Paar nicht werden. Kein Wrapper-Subsystem nötig, kein try/finally im Tool-Code.

**Pending-Eviction: ADOPTION statt Doppel-Load (Review 8):** Ein neuer Call gegen einen eviction-
pending Key ADOPTIERT den Eintrag: Pending-Flag zurücksetzen, Touch erneuern, Lease ausgeben — der
residente Workspace bleibt, es entsteht KEIN zweiter paralleler Roslyn-Workspace für dasselbe Projekt
(RAM-Risiko bei großen Solutions vermieden). Disposed wird ein pending Entry nur, wenn bis zum
nächsten Idle-Tick keine Adoption erfolgte und InFlightCount 0 ist. Unit-Test fixiert beide Wege.

### Solution-Zustand: zweistufiger Fehlervertrag (Build-Fehler, kaputte Projekte)

Unterschieden wird, WOHER der Zustandswechsel kommt:

| Pfad | Fehlerfall | Vertrag |
|---|---|---|
| **Kalt-Load** (Registry-Miss, Eviction-Reload, Warmup) | Solution/Rules nicht ladbar (kaputtes `.sln`/`.csproj`, fehlende Packages) | Dispatch antwortet `PROJECT_LOAD_FAILED` mit Ursprungsmeldung (+ Restore-Hint). Der Eintrag bleibt als FAILED-Marker in der Registry (Widerspruch aus früheren Fassungen aufgelöst, Review R2/B: mit dem Sync-Lease-Design entsteht der Eintrag sofort, der Load läuft im Hintergrund); nächster Hit erkennt `LoadState == LoadFailed`, entfernt ihn und lädt frisch — kein negatives Caching über den Fehlschlag hinaus. |
| **Inkrementeller Staleness-Refresh** (bekannte/neue/geänderte Dateien, csproj-Änderung) | Re-Evaluation schlägt fehl | **LETZTER GUTER STAND bleibt resident**; Analyse läuft weiter; Antworten auf diesem Key tragen bis zur erfolgreichen Aktualisierung einen `[WARN]`-Kopf; Health führt pro Key `LastGoodStateUtc` + `LastLoadError`. |

Syntaxfehler in einzelnen `.cs`-Dateien sind KEIN Load-Fehler (Roslyn toleriert sie; Diagnose erscheint
ohnehin in Tool-Antworten) — der Vertrag greift erst auf MSBuild-/Solution-Ebene. Das Praxis-Szenario
„ein Agent baut Mist, die Solution lädt nicht neu" ist damit deterministisch abgedeckt: Weiterarbeiten
auf last-good (sichtbar markiert), Reparatur durch den schreibenden Agent, nächster Refresh heilt.

### Gleichzeitigkeit & Snapshot-Semantik

### Threading-Modell (verbindlich)

„Ein Daemon" heißt NICHT „ein Thread": Der Prozess enthält viele Threads, aber wir bauen keine selbst —
es gilt das .NET-Standardmuster **async I/O + Thread Pool**:

- **Pipe-Verbindungen:** Je Verbindung ein async Read/Write-Loop (`ReadAsync`/`WriteAsync`). Eine
  wartende Verbindung blockiert keinen Thread; N verbundene Clients kosten fast null Threads.
- **Tool-Calls:** Das MCP-SDK dispatcht jeden Aufruf als async-Invocation auf einem Thread-Pool-
  Thread. Calls VERSCHIEDENER Clients laufen damit echt gleichzeitig; Calls derselben Verbindung
  je nach Host-Verhalten (z. B. `supports_parallel_tool_calls`) seriell oder parallel.
- **Roslyn-Analyse:** bleibt normaler synchroner Code INNERHALB der async-Methoden (CPU-Arbeit,
  nichts zu awaiten, kein `Task.Run`, keine eigenen Threads).

Regel: **async an den I/O-Grenzen** (Pipe, Solution-Hintergrund-Load, MRU-Schreiben, Timer),
**sync im Compute** — alles andere wäre Overhead. Bewusst serialisierte Stellen bleiben wie spezifiziert:
Staleness-Check am selben Key unter dem Instanz-Lock (Review-2-Trade-off), kurzer Registry-Lock,
Lease-Zähler per Interlocked. Verschiedene Keys blockieren sich nie.

Mehrere Clients am SELBEN Key teilen sich dieselbe Serverinstanz: Der Staleness-Check serialisiert
unter dem Instanz-Lock, Analysen laufen außerhalb des Locks auf unveränderlichen Roslyn-Solution-
Snapshots (thread-sicher lesend). Gewollte Konsequenz: Ein nur-lesender Zusatz-Agent sieht die
Änderungen des schreibenden Agenten spätestens mit seinem nächsten Call — geteilter warmer Stand
statt veralteter Prozesskopien. Konsistenzgrenze ist bewusst PRO CALL (Snapshot), nie innerhalb
eines Calls.
**Explizit dokumentierter Trade-off (Review 2):** Der Instanz-Lock umfasst den kompletten
Staleness-Check + Refresh (`GetCurrentSolution` hält ihn über den gesamten Check) — parallele Clients
am SELBEN Key warten hier also aufeinander (Konsistenz > Throughput, bewusst). Verschiedene Keys
blockieren sich NICHT. Ein Staleness-Throttle (max. 1 Check/Sekunde/Key, Review 14) ist als
bekanntes Follow-up nach Epic B notiert und wird in v1 NICHT gebaut.

## A.8 Tests (Epic A)

Unit (FastTests, Category=Unit):

- Registry: Key-Normalisierung (case-insensitive, Trailing-Slashes), HIT/MISS, lastUsed/Touch.
- Loader: Pflichtfelder beide; relative→absolute Auflösung (Anker Definitionsdatei); defektes JSON/
  fehlendes Feld → Fehler, keine Teil-Initialisierung; KEINE Auto-Suche im MCP-Pfad (F8).
- Uniforme Pflicht: fehlendes `projectRoot` → PROJECT_ROOT_REQUIRED bei beliebigem Registry-Stand.
- Root-Validierung (Audit 3): relativer projectRoot → PROJECT_ROOT_INVALID; get_server_health ohne
  Filter liefert alle Keys.
- Kein-Fallback-Vertrag: rules fehlt → RULES_NOT_FOUND (Nachbar-Suche greift nie).
- Self-Service-Vertrag: PROJECT_NOT_INITIALIZED-Text enthält den vorgeschriebenen Template-Block (Text-Assertion).
- Load-Dedupe (Audit 1): zwei parallele Erst-Calls auf denselben Root erzeugen genau EINEN Load;
  während des Loads bleiben Calls auf ANDERE Roots bedienbar (Lock-Hygiene).
- Busy-Guard (Audit 2): laufender Call schützt den Key vor TTL/LRU-Eviction; danach greift sie.
- Eviction: TTL mit injizierbarer Clock; LRU-Reihenfolge; maxProjects-Grenze; Dispose wird gerufen.
- Contract-Tests: jedes Tool-Schema enthält `projectRoot` als required (tools/list-Assertion).
- Zweistufiger Zustandsvertrag: Kalt-Load-Fehler → `PROJECT_LOAD_FAILED` ohne Registry-Eintrag;
  inkrementeller Refresh-Fehler → last-good bleibt resident, `[WARN]`-Kopf gesetzt, Health-Felder
  (`LastGoodStateUtc`/`LastLoadError`) gefüllt; erfolgreicher Refresh heilt die Markierung.
- Snapshot-Semantik: parallele Calls mehrerer simulierter Clients auf denselben Key liefern je Call
  konsistente Ergebnisse; eine zwischen zwei Calls erfolgte Änderung ist im Folge-Call sichtbar.
- Pending-Adoption (Review 8/13): Call gegen eviction-pending Key adoptiert den Eintrag (kein neuer
  Load, kein zweiter Workspace); ohne Adoption bis zum nächsten Tick wird disposed.
- Lease-Disziplin: Lease.Dispose senkt InFlightCount genau einmal; Doppel-Dispose ist no-op.
- Lease-Lifetime (Review R2/A): Mit async/await-Wiring bleibt InFlightCount während des GESAMTEN
  Tool-Calls > 0 (verzögerter Test-Task) und fällt erst nach Abschluss auf 0 — das nacktes-
  return-Muster würde diesen Test NICHT bestehen.
- FAILED-Marker (Review R2/B): Hit nach LoadFailed entfernt den Eintrag und startet frischen Load;
  TTL-Tick räumt FAILED-Einträge sofort weg.

Integration (Category=Integration):

- Zwei Projekte (neutrale kleine Fixtures gemäß Übersicht) aktivieren, Calls routen korrekt je Key;
  Bindungsverifikation über `get_server_health` (pro-Key-Zustände).
- Lazy-Init: erster Call gegen neuen Key messbar länger; zweiter sofort.
- Staleness-Walk bleibt auf Projektgrenzen (keine Regression zu Konzept 02).
- Observability: Call-Log enthält projectRoot/Key.
- Reaper unverändert.

## A.9 DoD (Epic A)

- Build grün; FastTests + IntegrationTests ohne Stress grün.
- Alle A.8-Tests implementiert und grün; Contract-Tests fixieren den neuen Schema-Vertrag.
- Harter Cut aktiv: MCP-Modus lehnt `--path`/`--config` ab; Batch unverändert.
- Eigenes Repo migriert (gleicher Task): `ainetlinter.project.json` im Root, AGENTS.md-Abschnitt
  „AiNetLinter-MCP: Initialisierung", Repo-`.mcp.json` und eigene Hermes-Registrierung (config.yaml)
  auf `command + --mcp-server` reduziert.
- Doku (siehe Sammelabschnitt Doku) aktualisiert; `.agents/rules/AiNetLinter.mdc` via
  `--sync-agent-rules-only` synchronisiert.
- `90_bewusst-nicht-umsetzen.md` §D.4-Eintrag um Wiederöffnungs-Vermerk erweitert.

---

# Epic B — Daemon-Modus (geteilter, langlebiger Analysekern)

**Voraussetzung: Epic A ist abgeschlossen und grün.** Epic B verschiebt die fertig gebaute Registry
in einen geteilten Prozess; am Toolvertrag ändert sich nichts.

## B.1 Zielbild

```
Claude Code ─┐                          ┌─ Registry (max N Keys, TTL/LRU)
Cline       ─┤─ Thin-Client (stdio) ◄──►│─ McpCodeGraphServer je Key (warm)
Hermes      ─┘        Named Pipe          ├─ MRU-State (%LOCALAPPDATA%)
                     (JSON-RPC-Pump)      └─ Observability (Call-Log + Health)
```

- **Daemon:** eigener Prozess, hält Registry + Workspaces. Startet lazy, beendet sich selbst
  (Idle-Exit) — **läuft NICHT „für immer“** (bewusste Abkehr von „nie beendet“: Zombie-Historie des
  Projekts, siehe `--parent-pid`-Reaper; Selbstheilung schlägt Unsterblichkeit).
- **Thin-Client:** derselbe `AiNetLinter.exe`-Aufruf wie heute (`--mcp-server`), agiert als reiner
  Proxy: MCP über stdio ⇄ JSON-RPC über Pipe. Enthält keine Analyselogik, kein SDK-State.
- Für Clients ändert sich nichts: erste Nutzung kann Daemon-Kaltstart enthalten (Sekunden), danach
  ist jede Verbindung warm — auch wenn Clients „komisch“ spawnen (pro Chat/pro Session/pro Fenster).

## B.2 Transport & Protokoll

- **Named Pipes** (.NET `NamedPipeServerStream`/`NamedPipeClientStream`; Windows-first, POSIX-kompatibel
  benennbar): Pipe-Name `ainetlinter.analyzer.v1.<username>` — Username-Suffix wegen Mehrnutzer-
  Maschinen mit geteilten Pipe-Namespaces (Self-Audit 6); ACL zusätzlich auf aktuellen User.
  Kein Port, kein Auth-Protokoll, kein Firewall-Thema.
- **Verbindungsmodell:** Daemon bedient mehrere gleichzeitige Clients (je Verbindung eine eigene
  MCP-Session gegen die geteilte Registry). Registry-Resolve unter Lock; Serverinstanzen intern
  gesichert (F1/F5).
  **Cancellation** (Self-Audit 7): Trennt sich ein Client, werden seine in-flight Calls abgebrochen
  (CancellationToken je Verbindung bis in den Tool-Dispatch) — Registry und Keys bleiben davon
  unberührt und warm für andere Clients.
- **Handshake (vor dem MCP-Durchsatz), JSON über die Pipe:**
  - Client → Daemon: `{ "hello": { "protocolVersion": 1, "exeVersion": "1.0.x" } }`
  - Daemon → Client: `{ "welcome": { "protocolVersion": 1, "daemonVersion": "1.0.y", "pid": n } }`
  - **Versions-Handshake:** `daemonVersion != exeVersion` → Client sendet `shutdown`, wartet auf Exit,
    startet Daemon neu (löst das „alter Daemon nach Update“-Problem sauber statt per Kill).
  `shutdown` ist ein EIGENES Handshake-Protokoll-Kommando auf Pipe-Ebene (Review 11) — zu diesem
  Zeitpunkt existiert noch keine MCP-Session; es gibt kein MCP-Level-shutdown. Framing wie gehabt
  (newline-delimited JSON).
  **Anti-Ping-Pong** (Self-Audit 11): Stehen beim Versions-Mismatch noch ANDERE Verbindungen, fährt
  der Client den Daemon NICHT herunter, sondern bricht mit `VERSION_CONFLICT` ab (macht die
  Konfigurations-Inkonsistenz sichtbar statt einen Neustart-Wettlauf zu verlieren). Shutdown nur bei
  null weiteren Verbindungen.
  **Konfigurations-Sichtbarkeit** (Self-Audit 14): Das `welcome` trägt zusätzlich die EFFEKTIVE
  Daemon-Konfiguration der daemon-level Flags (`maxProjects`, `idleExitMinutes`, Log-Ziel). Der
  Thin-Client vergleicht sie mit seinen eigenen Argumenten; bei Divergenz `[WARN]` auf stderr +
  Observability-Ereignis — nie still. Regel: daemon-level Flags gehören zum DAEMON-Leben (wirksam ist,
  wer ihn gestartet hat); per-Call-Verträge (`projectRoot`, Definitionsdatei) sind davon unberührt.
  - Danach: opake Byte-/JSON-RPC-Pump in beide Richtungen; der Thin-Client interpretiert MCP-Inhalte
    NICHT (Decoupling vom SDK-Standalone).
- **Pipe-Abbruch mitten im Call** (Self-Audit 12): Stirbt der Daemon zwischen Request und Response,
  wiederholt DER Thin-Client denselben Call GENAU EINMAL automatisch (Connect-or-Start neu) — zulässig,
  weil alle Tools read-only und damit idempotent sind. Ein zweiter Fehlschlag wird roh an den Agenten
  durchgereicht (kein stiller Retry-Loop).
- **Reaper-Erbe** (Self-Audit 13): Der Thin-Client erbt den bestehenden `--parent-pid`-Reaper gegen
  SEINEN Agent-Prozess: stirbt der Agent, stirbt der Thin-Client, die Pipe-Verbindung endet — der
  Daemon sieht den Disconnect und kann idle-exiten. Damit gibt es keine Waisen-Verbindungen, die den
  Daemon ewig festhalten; der DAEMON selbst nutzt weiterhin keinen Reaper.
- **Single-Instance-Race:** Client versucht zuerst Connect (kurzes Timeout); scheitert er, spawnt er
  den Daemon (detached, ohne Parent-Bindung) und retried bis zu N Sekunden. Zwei gleichzeitige Starter:
  der Verlierer des Pipe-Greifens verbindet sich einfach.
- **Transport-Boundary (Review 6):** Die Byte-Pump ist bewusst opak und gilt NUR für stdio als
  Client-Vertrag. Ein künftiger Transportwechsel des MCP-Standards (z. B. Streamable HTTP als
  Client-Pflicht) oder client-seitige SDK-Features (sampling/elicitation im Thin-Client) würden den
  Thin-Client-Ansatz fundamental ändern — das ist akzeptiert und gehört nicht in diese Epics.

## B.3 Lifecycle

| Mechanismus | Regel |
|---|---|
| Start | Lazy durch ersten Client; liest MRU-State und wärmt die letzten ≤ maxProjects Keys im Hintergrund — **über denselben Resolve-/Dedupe-Pfad wie interaktive Calls, mit gebundener Konkurrenz (max 2 parallele Warmup-Loads)**. Ein interaktiver Load wartet NIE hinter der Warmup-Queue (Self-Audit 9). Tote Pfade (Projekt gelöscht, Definitionsdatei weg) werden verworfen UND aus dem MRU-State entfernt (Self-Audit 10); fehlgeschlagene Warmups blockieren den Daemonbetrieb nicht. |
| Idle-Exit | Keine verbundene Clients UND Idle ≥ `--mcp-daemon-idle-exit-minutes` (Default 10) → graceful Shutdown inkl. Dispose aller Keys und MRU-Persistierung. LAUFENDE Loads/Warmups verschieben den Exit (Self-Audit 15): Shutdown beginnt erst, nachdem keine Load-Tasks mehr aktiv sind — niemals Dispose unter halbfertigem Load. |
| Hänger-Schutz | Thin-Client-Ping mit Timeout; bei Hänger darf der Client den Daemon terminieren und neu starten (er hängt ja für alle) — Ereignis ins Call-Log. |
| Kein Parent-Bindung | Der Daemon nutzt den `--parent-pid`-Reaper bewusst NICHT (er überlebt einzelne Clients gewollt); seine Sicherheit ist Idle-Exit + Versions-Handshake. |
| Debug-Escape | Env `AINETLINTER_NO_DAEMON=1` → Thin-Client läuft klassisch in-proc (für Fehlersuche). Explizit dokumentiertes Debug-Ventil, KEIN Konfigurationsfeature. |

**Stdio-Purity** (Self-Audit 5): Der Thin-Client-Prozess schreibt AUSSCHLIESSLICH MCP-Protokollbytes auf
stdout — ein einziges streunendes `Console.WriteLine` zerschießt die Client-Session. Diagnoseausgaben
gehen ausschließlich nach stderr oder ins Observability-Log; per Contract-Test absichern.

## B.4 MRU-State

`%LOCALAPPDATA%\RalfHuesing\AiNetLinter\daemon-state.json`:
Array `{ rootPath, lastUsedUtc }`, max maxProjects, geschrieben bei jedem Touch (debounced) und beim
Shutdown. Nur ein Warmstart-Hinweis, niemals Wahrheitsquelle: Definitionsdatei ist immer der Vertrag.
Debounce grob: frühestens 30 s nach dem letzten Touch schreiben (ein Timer, kein Per-Touch-Spawn);
Schreibfehler (gesperrte Datei) werden geloggt und ignoriert — der State ist verzichtbar.
Schreiben ATOMAR: temp-Datei + `File.Move` mit Überschreiben (Review 15); eine defekte/leere Datei
bewirkt schlicht „kein Warmup“, niemals einen Fehler.

## B.5 Umsetzungspfad (Epic B)

1. **Transport-Layer** (`Mcp/Daemon/`): Pipe-Server/-Client, Handshake, Pump. Protokollversion als
   Konstante; Framing newline-delimited JSON.
2. **Daemon-Host:** `AiNetLinter.exe --daemon-start` (intern, nicht für Clients gedacht): hostet
   `ProjectRegistry` + `McpServerOptionsFactory`-Stack über Pipe-Transport; Observability mit
   `connectionId` + `mode=daemon`.
   CLI-Details (Review R2/C): `--daemon-start` erscheint in `--help`, als `[internal]` markiert
   (versteckte Argumente erschweren Fehlersuche). Doppelstart bei bereits laufendem Daemon: die Pipe
   ist belegt → sauberer Fehler auf stderr + Exit-Code ≠ 0 — kein Ersetzen des laufenden Daemons,
   keine unbehandelte Exception.
3. **Thin-Client-Modus:** `--mcp-server` verhält sich nach außen identisch wie heute, intern:
   Connect-or-Start → Handshake → Pump. Statistische Parameter (`--mcp-log`, TTL/MaxProjects) reicht
   der Thin-Client beim Daemon-Spawn durch.
4. **Health-Erweiterung:** Verbindungen, PID, Uptime, Modus, Keys (aus Epic A), Daemon-Version.
5. **Tests/Doku/Migration** (unten).

## B.6 Tests (Epic B)

Unit (in-proc, Category=Unit):

- Handshake-Handler: kompatible/unbekannte Protokollversion; Versionsvergleich-Logik.
- Idle-Exit-Timer mit injizierbarer Clock (Clients offen vs. geschlossen).
- MRU-State: Schreiben/Lesen, Korrupt-Datei → ignorieren, fehlende Definitionsdateien → verwerfen.
- Race-Logik: Connect-or-Start-State-Machine (Mock-Pipe).

Integration (Category=Integration):

- Echter Daemon-Prozess (Test spawnt EXE): zwei Thin-Clients parallel, Calls je `projectRoot` korrekt;
  zweiter Client profitiert (kein zweiter vollständiger Load — RefreshCount/Staleness-Zähler belegen
  Shared-Warmth).
- Idle-Exit: Clients trennen → Daemon beendet sich innerhalb TTL; MRU-State geschrieben.
- Kaltstart-Warmup: Daemon-Start wärmt MRU-Keys; erster Client-Call gegen gewärmten Key ist schnell.
- Hänger-Pfad: nicht reagierender Daemon → Client killt/neu startet, Call-Log-Eintrag. Umsetzung:
  Stellvertreter-Prozess (Test startet einen Prozess, der die Pipe bindet und nie antwortet) statt
  Injektion in die echte EXE — deterministisch, kein Timing-Glück.
- Escape: `AINETLINTER_NO_DAEMON=1` → In-proc-Modus ohne Daemon-Prozess.

Versions-Mismatch wird NICHT als Zwei-Prozess-Integrationstest geführt (es gibt keine alte EXE zum
Spawnen) — die Versionsvergleichslogik ist über einen injizierbaren Versionsprovider vollständig
unit-getestet (kompatibel / Mismatch / Anti-Ping-Pong-Fall mit simulierten Verbindungen).

Stress-frei halten: Prozess-Orchestrierungstests bewusst klein halten (wenige E2E), Daemon-Logik
selbst in-proc testen (Richtlinien §2: lastintensive Parallelläufe gehören nach `Category=Stress`,
werden hier NICHT angelegt).

## B.7 DoD (Epic B)

- Build grün; FastTests + IntegrationTests ohne Stress grün.
- Alle B.6-Tests grün; Epic-A-Suite weiterhin grün (Contract unverändert).
- Live-Check (Dogfood): eigene Hermes-Registrierung + Repo-`.mcp.json` nutzen den Daemon-Modus;
  `get_server_health` weist Modus/Verbindungen/PID/Keys aus.
- Doku aktualisiert (siehe Sammelabschnitt); `90_bewusst-nicht-umsetzen.md` §C.5-Eintrag um
  Wiederöffnungs-Vermerk erweitert.

---

# Epics-Reihenfolge & Abhängigkeiten

| Reihenfolge | Epic | Abhängigkeit | Umfang |
|---|---|---|---|
| 1 | **A Projektregistry** | keine | Wiring-Umbau (6 Registrationsklassen + Resource), Registry/Loader neu, harter Cut, Migration eigener Konfigurationen |
| 2 | **B Daemon-Modus** | Epic A komplett | Transport-Layer, Daemon-Host, Thin-Client-Modus, MRU, Lifecycle |

Innerhalb jedes Epics: Contract-Tests zuerst, dann Implementierung, dann Migration der eigenen
Konfigurationen, dann Doku. Kein Epic wird halb verlassen — jedes endet mit eigenem DoD.

# Doku (Sammelpflicht, beide Epics)

| Datei | Inhalt |
|---|---|
| `Docs/agent-api.md` | Neuer Init-Vertrag (`projectRoot`-Pflicht), Referenzabschnitt „ainetlinter.project.json“ (Feldtabelle, Ankerregel, Beispiele, Template), neue Fehlercodes, entfernte MCP-Parameter, Binding-Prüfung via `get_server_health`; Epic B: Transport-/Lifecycle-Abschnitt (Daemon, Idle-Exit, Escape-Variablen) |
| `Docs/configuration.md` | CLI-Parameteränderungen MCP-Modus (entfernte Flags, neue statische Flags `--mcp-project-ttl-minutes`, `--mcp-max-projects`, `--mcp-daemon-idle-exit-minutes`) |
| `Docs/integration.md` | Registrierungsbeispiele ohne `--path`/`--config` für Hermes/Claude Code/Cline; Abschnitt „Daemon-Modus“ (Verhalten, Update-Handling, Debug-Escape) |
| `Docs/ROADMAP.md` | Epic-Abschluss je Stand |
| `README.md` | Kurzer Abschnitt zum neuen Nutzungsmodell |
| `AGENTS.md` (Repo-Root) | Abschnitt „AiNetLinter-MCP: Initialisierung“ (projectRoot-Ritual + Template + Verweis agent-api.md) |
| `.agents/rules/AiNetLinter.mdc` | Sync via `dotnet run --project src/AiNetLinter -- --sync-agent-rules-only` falls Regel-/CLI-Texte betroffen |
| `tasks/mcp-server-weiterentwicklung/00_uebersicht-und-entscheidungen.md` | Zeile 11 + Statuspflege |
| `90_bewusst-nicht-umsetzen/Konzept.md` | Wiederöffnungsvermerke D.4 (Epic A) und C.5 (Epic B) mit Verweis auf dieses Konzept |

# Definition of Done (gesamt)

- `dotnet build` fehler-/warnungsfrei; `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`
  und `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` grün (Richtlinien §2).
- Beide Epics vollständig umgesetzt; keine Deprecationsschichten; harte Cuts aktiv.
- Eigene Registrierungen (Hermes config.yaml, Repo-`.mcp.json`) nutzen den Endstand; AGENTS.md-Ritual
  vorhanden; Dogfood-Lauf gegen dieses Repo erfolgreich.
- Doku-Tabelle vollständig abgearbeitet; Sync-Lauf der Agentenregeln erfolgt.
- Kein Verstoß gegen Lint-Grenzen (F7); Observability zeigt Calls mit `projectRoot`/Modus.

# Risiken & Mitigationen

| Risiko | Mitigation |
|---|---|
| Wiring-Umbau über 6 Registration-Klassen + Resource | Mechanisch identische Änderung je Klasse (F3); Contract-Tests zuerst, Klassen einzeln umstellen. Kein Serverklasse-Refactoring nötig (F1). |
| Vergessenes `projectRoot` durch Agent | Harter deterministischer Fehler; Ritual in AGENTS.md + ServerInstructions. |
| RAM-Wachstum langer Host-Sessions | TTL + maxProjects (Epic A), im Daemon zusätzlich Idle-Exit + Dispose-all. |
| Daemon hängt/leakt | Ping-Timeout + Client-seitiger Kill/Restart; Ereignisse im Call-Log; Idle-Exit begrenzt Lebenszeit grundsätzlich. |
| Alter Daemon nach Update | Versions-Handshake mit sauberem Restart (B.2), nicht Kill-by-hope. |
| Race zweier Erststarter | Connect-first/spawn-second Pattern; Pipe-Greifen entscheidet. |
| IPC-Fehlersuche schwer | Observability in BEIDEN Prozessen mit gemeinsamer Connection-ID; Debug-Escape `AINETLINTER_NO_DAEMON=1`. |
| GC-Trägheit verfälscht RAM-Erwartung | Dokumentiert; Monitoring über Health statt Task-Manager-Impressionen. |

# End-to-End-Durchlauf (Referenzszenario, verifiziert gegen die Verträge)

Frischer Rechner, 4 Solutions, 4 verschiedene Agent-Clients werden gestaffelt (~10 s Abstand) gestartet,
dazu später ein fünfter Nur-Lese-Agent auf Solution 1:

| T | Ereignis | Ablauf laut Vertrag |
|---|---|---|
| +0s | Rechner frisch | Kein Daemon. Keine Prozesse. |
| +10s | Agent 1 (S1) startet | Thin-Client: Pipe fehlt → detached Daemon-Spawn → Retry bis Handshake ok. Erster Call: Registry-Miss → interaktiver Load S1 (Warmup konkurriert gebunden, wartet nie vor ihm). |
| +20–40s | Agents 2–4 (S2–S4) | Jeweils eigener Thin-Client (Clients spawnen unabhängig — gewollt), Connect zum lebenden Daemon, MISS → je ein Load. Registry bei 4/4. |
| +50s | Agent 5 (Nur-Lese, S1) | HIT: warm, sofortige Antwort. Teilt Serverinstanz mit Agent 1; Snapshot-Semantik pro Call (siehe Gleichzeitigkeit). |
| später | Schreib-Agent macht csproj kaputt | Inkrementeller Refresh schlägt fehl → last-good bleibt resident, `[WARN]`-Kopf, Health zeigt LastLoadError. Lesen weiter möglich; Reparatur heilt beim nächsten Refresh. |
| später | Solution 1 tagelang unbenutzt | TTL evictet Key (busy-safe); nächster Call = Kalt-Load. |
| Ende des Tages | Letzter Agent zu | Alle Verbindungen zu → Idle-Timer läuft ab → graceful Shutdown, MRU-State geschrieben. Nächster Morgen: Kaltstart einmalig, dann warm via MRU. |

Bewusst akzeptierte Grenzfälle (dokumentiert, keine Behandlung): derselbe physische Pfad unter zwei
Schreibweisen/Subst-Laufwerken erzeugt zwei Keys (praxisfern; Kanonisierung deckt Groß/Klein und
Slash-Richtung ab); >4 wirklich gleichzeitige aktive Solutions erzeugen LRU-Churn (bewusstes Limit);
Kaltstart-Latenz des allerersten Calls (Host-Timeouts 120–300 s reichen locker; Retry trifft deduplizierten/warmen
Load); AV/EDR könnte detached Spawn oder Named Pipes einschränken → Escape-Variablen + stderr-Diagnose
machen es sichtbar.

# Bewusst später (nicht Teil dieses Epics)

- Windows-Service/Autostart (erst wenn Idle-Exit-Kaltstarts im Alltag stören).
- HTTP-/Remote-Transport, Multi-User.
- Benannte Mehrfach-Projekte pro Definitionsdatei (v1: eine Solution+Rules je Datei).
- Generator-Kommando für die Definitionsdatei (Template im Fehler reicht).
- Staleness-Throttle pro Key (max. 1 Check/Sekunde, Review 14) — erst bei belegtem Bottleneck im
  Multi-Client-Daemon; Konzept 02 liefert die Messzähler als Evidenzbasis.
