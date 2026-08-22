---
status: offen
type: konzept (epic-roadmap, autonom umsetzbar)
project_kind: brownfield
estimated_scope: large
priority: P1
agent_role: .agents/Agent-Scaffolding/dev-loop/planning/orchestrator.md
rules_dir: .agents/rules
last_updated: 2026-08-22
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

    // HIT  -> entry.Touch(); Server zurueckgeben
    // MISS -> ProjectDefinitionLoader laden (hart, Fehlervertraege unten),
    //         bei maxProjects zuerst LRU-Eviction (DisposeAsync), dann Eintrag anlegen
    internal McpCodeGraphServer Resolve(string projectRoot);
}

// neu: src/AiNetLinter/Mcp/Projects/ProjectDefinitionLoader.cs
// liest <root>/ainetlinter.project.json, verlangt beide Felder, loest relativ zur Datei auf,
// prueft Existenz beider Zieldateien. Kein Fallback-Zweig.
```

Wiring (mechanisch, identisches Muster je Klasse):

```csharp
// VORHER (SymbolGraphToolRegistrations.cs:44-46)
(string? namePattern = null, ...) => FindSymbolTool.ExecuteAsync(mcpState, namePattern, ...)

// NACHER
(string projectRoot, string? namePattern = null, ...)
    => FindSymbolTool.ExecuteAsync(_registry.Resolve(projectRoot), namePattern, ...)
```

- Alle sechs Registration-Klassen (`SymbolGraph`, `FileStructure`, `Analysis`, `SymbolBody`,
  `ServerMaintenance`, `DuplicateDetection`) sowie `OverviewResourceRegistration` umstellen.
- `McpServerOptionsFactory.Create(McpCodeGraphServer mcpState)` → `Create(ProjectRegistry registry)`.
- `McpServerCommand.RunAsync` hält keine Serverinstanz mehr, sondern die Registry (+ Eviction-Timer);
  `reload_config` und `get_server_health` routen über denselben Resolver; Health aggregiert pro Key
  (F5 liefert Pro-Instanz-Werte: Root/Solution/Rules/lastUsed/RefreshCount/Staleness/Uptime).
- projectRoot-Vertrag einmalig in `ServerInstructions.Text` (F6).
- Lint-Grenzen F7 einhalten (Registry-Klassen klein, Options-Records).

## A.5 Fehlerverträge (alle deterministisch, englisch, mit Bauanleitung)

| Fall | Code (neu) | Textinhalt |
|---|---|---|
| `projectRoot` fehlt | `PROJECT_ROOT_REQUIRED` | Parameter ist ausnahmslos Pflicht |
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
- **maxProjects (Default 4) + LRU:** neuer Key bei vollem Registry verdrängt ältesten.
- Caveats (dokumentiert): .NET gibt GC-RAM träge ans OS (Kurve sägt); Solution-Reload kostet Sekunden
  bis Minuten — TTL nicht aggressiv setzen; Dispose muss Workspace-/Catalog-Referenzen konsequent
  freigeben (Muster F5).
- Bestehende Prozesshygiene unberührt: `--parent-pid`-Reaper bleibt (in Epic B bewusst NICHT im Daemon).

## A.8 Tests (Epic A)

Unit (FastTests, Category=Unit):

- Registry: Key-Normalisierung (case-insensitive, Trailing-Slashes), HIT/MISS, lastUsed/Touch.
- Loader: Pflichtfelder beide; relative→absolute Auflösung (Anker Definitionsdatei); defektes JSON/
  fehlendes Feld → Fehler, keine Teil-Initialisierung; KEINE Auto-Suche im MCP-Pfad (F8).
- Uniforme Pflicht: fehlendes `projectRoot` → PROJECT_ROOT_REQUIRED bei beliebigem Registry-Stand.
- Kein-Fallback-Vertrag: rules fehlt → RULES_NOT_FOUND (Nachbar-Suche greift nie).
- Self-Service-Vertrag: PROJECT_NOT_INITIALIZED-Text enthält Template-Block (Text-Assertion).
- Eviction: TTL mit injizierbarer Clock; LRU-Reihenfolge; maxProjects-Grenze; Dispose wird gerufen.
- Contract-Tests: jedes Tool-Schema enthält `projectRoot` als required (tools/list-Assertion).

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
  benennbar): Pipe-Name fest `ainetlinter.analyzer.v1`. ACL auf aktuellen User. Kein Port, kein Auth-
  Protokoll, kein Firewall-Thema.
- **Verbindungsmodell:** Daemon bedient mehrere gleichzeitige Clients (je Verbindung eine eigene
  MCP-Session gegen die geteilte Registry). Registry-Resolve unter Lock; Serverinstanzen intern
  gesichert (F1/F5).
- **Handshake (vor dem MCP-Durchsatz), JSON über die Pipe:**
  - Client → Daemon: `{ "hello": { "protocolVersion": 1, "exeVersion": "1.0.x" } }`
  - Daemon → Client: `{ "welcome": { "protocolVersion": 1, "daemonVersion": "1.0.y", "pid": n } }`
  - **Versions-Handshake:** `daemonVersion != exeVersion` → Client sendet `shutdown`, wartet auf Exit,
    startet Daemon neu (löst das „alter Daemon nach Update“-Problem sauber statt per Kill).
  - Danach: opake Byte-/JSON-RPC-Pump in beide Richtungen; der Thin-Client interpretiert MCP-Inhalte
    NICHT (Decoupling vom SDK-Standalone).
- **Single-Instance-Race:** Client versucht zuerst Connect (kurzes Timeout); scheitert er, spawnt er
  den Daemon (detached, ohne Parent-Bindung) und retried bis zu N Sekunden. Zwei gleichzeitige Starter:
  der Verlierer des Pipe-Greifens verbindet sich einfach.

## B.3 Lifecycle

| Mechanismus | Regel |
|---|---|
| Start | Lazy durch ersten Client; lädt MRU-State und wärmt die letzten ≤ maxProjects Keys **sequenziell im Hintergrund** (Definitionsdateien werden erneut gelesen; fehlt eine → Eintrag verwerfen, kein Fehler). |
| Idle-Exit | Keine verbundene Clients UND Idle ≥ `--mcp-daemon-idle-exit-minutes` (Default 10) → graceful Shutdown inkl. Dispose aller Keys und MRU-Persistierung. |
| Hänger-Schutz | Thin-Client-Ping mit Timeout; bei Hänger darf der Client den Daemon terminieren und neu starten (er hängt ja für alle) — Ereignis ins Call-Log. |
| Kein Parent-Bindung | Der Daemon nutzt den `--parent-pid`-Reaper bewusst NICHT (er überlebt einzelne Clients gewollt); seine Sicherheit ist Idle-Exit + Versions-Handshake. |
| Debug-Escape | Env `AINETLINTER_NO_DAEMON=1` → Thin-Client läuft klassisch in-proc (für Fehlersuche). Explizit dokumentiertes Debug-Ventil, KEIN Konfigurationsfeature. |

## B.4 MRU-State

`%LOCALAPPDATA%\RalfHuesing\AiNetLinter\daemon-state.json`:
Array `{ rootPath, lastUsedUtc }`, max maxProjects, geschrieben bei jedem Touch (debounced) und beim
Shutdown. Nur ein Warmstart-Hinweis, niemals Wahrheitsquelle: Definitionsdatei ist immer der Vertrag.

## B.5 Umsetzungspfad (Epic B)

1. **Transport-Layer** (`Mcp/Daemon/`): Pipe-Server/-Client, Handshake, Pump. Protokollversion als
   Konstante; Framing newline-delimited JSON.
2. **Daemon-Host:** `AiNetLinter.exe --daemon-start` (intern, nicht für Clients gedacht): hostet
   `ProjectRegistry` + `McpServerOptionsFactory`-Stack über Pipe-Transport; Observability mit
   `connectionId` + `mode=daemon`.
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
- Versions-Mismatch: Daemon alter Version → Client löst sauberen Neustart aus, danach kompatibel.
- Idle-Exit: Clients trennen → Daemon beendet sich innerhalb TTL; MRU-State geschrieben.
- Kaltstart-Warmup: Daemon-Start wärmt MRU-Keys; erster Client-Call gegen gewärmten Key ist schnell.
- Hänger-Pfad: nicht reagierender Daemon (Test-Injektion) → Client killt/neu startet, Call-Log-Eintrag.
- Escape: `AINETLINTER_NO_DAEMON=1` → In-proc-Modus ohne Daemon-Prozess.

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

# Bewusst später (nicht Teil dieses Epics)

- Windows-Service/Autostart (erst wenn Idle-Exit-Kaltstarts im Alltag stören).
- HTTP-/Remote-Transport, Multi-User.
- Benannte Mehrfach-Projekte pro Definitionsdatei (v1: eine Solution+Rules je Datei).
- Generator-Kommando für die Definitionsdatei (Template im Fehler reicht).
