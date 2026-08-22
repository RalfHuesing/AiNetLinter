---
status: offen
type: konzept
project_kind: brownfield
estimated_scope: medium-large
priority: P1
agent_role: .agents/Agent-Scaffolding/dev-loop/planning/orchestrator.md
rules_dir: .agents/rules
last_updated: 2026-08-22
open_questions: []
herkunft: "Diskussion 2026-08-22 (ox-alpha + Nutzer): MCP-Server wurde global mit hartkodierter --path/--config-Registrierung genutzt; in Multi-Projekt-/Multi-Agent-Setups bindet er still an die falsche Solution. Nachschärfung 2026-08-22: harter Cut statt Migration, alle Parameter Pflicht, Definitionsdatei ainetlinter.project.json."
---

# Konzept 11: Projektregistry — deterministische Mehrfach-Solution-Bindung für den MCP-Server

## Intention

Der MCP-Server wird heute pro Prozess an **eine** Solution gebunden (`--path`/`--config` beim Start,
`McpServerCommand.cs:46-52`). Das kollidiert mit der Realität moderner Agent-Hosts:

1. **Hosts teilen sich den Serverprozess unterschiedlich.** Claude Code/Cline starten typischerweise
   pro Session/Fenster einen eigenen Serverprozess; Hermes (Desktop/Gateway) verbindet jeden MCP-Server
   **einmal pro Host** und teilt ihn über alle Chats (dokumentiert: „Server connections are persistent and
   shared across all conversations"). Der Server kann sich auf den Lebenszyklus also **nicht verlassen**.
2. **Falsche Bindung fällt nicht auf.** Wer in Projekt B arbeitet, während der Server mit `--path`
   auf Projekt A eingerostet ist, erhält korrekt aussehende, aber inhaltlich falsche Analyseergebnisse —
   ohne jeden Fehlerhinweis.
3. **Agenten-Konfiguration soll projektagnostisch werden.** Zielbild: In der Client-Konfiguration steht
   nur noch `AiNetLinter.exe --mcp-server` (ggf. plus statische, projektunabhängige Parameter). Alles
   Projektspezifische übergibt der Agent pro Aufruf.

**Kernidee:** Die Zuordnung „welche Solution" wandert aus der Prozess-Umgebung in den **Tool-Aufruf**
selbst — als `projectRoot`-Parameter. Der Server hält eine Registry mehrerer geladener Projekte
(`Dictionary<canonicalRootPath, LoadedProject>`), lädt Projekte lazy per Key-Miss und wirft unbenutzte
Projekte per Idle-TTL wieder aus dem Speicher. Damit ist die Bindung **deterministisch per Call** statt
per Chat-Kontext oder Heuristik.

### Warum jetzt? (Wiedereröffnung von 90_bewusst-nicht-umsetzen D.4)

`90_bewusst-nicht-umsetzen/Konzept.md §D.4` stellt „Multi-Solution-Unterstützung" zurück: „Eine Solution
pro Prozess ist ein sauberes Muster (mehrere Server-Instanzen). Hohe Beweislast: Nur bei belegtem Scheitern
des Multi-Server-Setups in der Praxis." Genau dieses Scheitern liegt jetzt belegt vor:

- **Belegt:** Hermes Desktop verbindet den Server einmal pro Host; mehrere parallele Chats zu
  verschiedenen Projekten können über separate Server-Instanzen im selben Profil **nicht** abgebildet
  werden, ohne dass Chats desselben Profils sich eine Instanz teilen. Mehrere Registrierungen unter
  verschiedenen Namen wären möglich, aber: doppelte Toolkataloge, Kontextballast, Modell muss wählen.
- **Belegt:** Die Registrierung braucht projektspezifische Parameter (`--path`, `--config`) — genau das
  widerspricht dem Projektziel „allgemeiner MCP-Server in beliebigen C#/.NET-Codebasen"
  (`00_uebersicht-und-entscheidungen.md`: „Nicht auf dieses Repository optimieren").
- Die Bedingung „belegtes Scheitern des Multi-Server-Setups in der Praxis" ist damit erfüllt; diese
  Aufgabe öffnet D.4 unter Wahrung der Beweislast-Dokumentation hier explizit wieder.

## Was wir NICHT machen (Non-Goals)

Angelehnt an und weiter konsistent mit `90_bewusst-nicht-umsetzen.md`:

| Non-Goal | Begründung |
|---|---|
| **Kein HTTP-MCP / zentraler Daemon** | Session-Zuordnung müsste der Server selbst verwalten (Verbindung→Solution); Port-/Firewall-/Auth-Themen; Lifecycle nach Updates unklar. Stdio gibt Isolation gratis. Ebenso `90 §C.5`. |
| **Kein Auto-Init aus cwd** | Reproduziert exakt die stille Fehl-Bindung, die dieses Konzept beseitigt. Init nur durch explizite Übergabe von `projectRoot` im Call. |
| **Kein Lock-/Claim-File zwischen Prozessen** | Stale-Locks nach Abstürzen, Cross-Process-Konflikte, kein Nutzen für Single-User-Linter. |
| **Keine Heuristiken/„magische" Lösungsvorschläge bei Fehlern** | Fehlermeldungen sind deterministisch und nennen den Fix; keine Rate-Vorschläge des Servers. |
| **Kein FileSystemWatcher-basiertes Definitions-Reload** | Die Definitionsdatei wird nur beim Key-Load gelesen; Refresh = Key-Eviction/Neuladen. Konsistent mit 02-Staleness-Entscheidung (Messung vor Watcher). |
| **Kein Umbau der Batch-Pipeline** | `--path`/`--config` bleiben für Batch-Lint vollständig erhalten; Änderungen betreffen ausschließlich den MCP-Modus. |
| **Keine Entfernung bestehender Tools** | Alle 26 Tools bleiben; es kommt ein Initialisierungsvertrag hinzu, keine Removal (konsistent mit `90 §B.1`). |
| **Keine Deprecations-/Übergangsphase** | Harter Cut: interne Toolkette, alle Client-Konfigurationen sind selbst verwaltet; alte Parameter = unbekanntes Argument = harter Fehler. |
| **Kein Multi-Agent-Installer / Detached-Daemon / Cloud** | Weiter gültig (`90 §C.5`, `§C.6`). |

## Architektur

### Verifizierte Code-Findings (Stand 2026-08-22, gegen HEAD geprüft)

Diese Findings wurden direkt im Quellcode verifiziert und bestimmen den Umsetzungsweg:

| # | Finding | Beleg |
|---|---|---|
| F1 | **`McpCodeGraphServer` ist bereits vollständig instanzbasiert.** Sämtlicher Zustand (`_catalog`, `_fileState`, `_lock`, `Config`/`UsedDefaultConfig`/`ResolvedConfigPath`, Staleness-Zähler) liegt als Instanzfelder vor; es gibt kein statisches Mutable-State. Die Klasse muss für N Projekte NICHT umgebaut werden — sie wird einfach N-mal instanziiert. | `McpCodeGraphServer.cs:25-75` (Felder + Konstruktor), `:27-41` |
| F2 | **Die „Globalität" steckt ausschließlich im Wiring, an genau zwei Stellen:** (1) `McpServerCommand.RunAsync` erzeugt EINE Instanz prozessweit, (2) `McpServerOptionsFactory.Create(mcpState)` bäckt diese eine Instanz per Closure in alle Tool-Lambdas und die Resource-Collection ein. | `McpServerCommand.cs:62-70`; `McpServerOptionsFactory.cs:27-57` |
| F3 | **Tools erreichen den State per Delegate-Closure:** Jede Registrierung erzeugt `McpServerTool.Create((args…) => XxxTool.ExecuteAsync(mcpState, …))`. Ersetzt man das eingefangene `mcpState` durch einen Resolver-Delegaten und ergänzt `projectRoot` im Lambda, ändert sich am SDK-Kontrakt nichts — der Parameter erscheint automatisch als Pflichtfeld im JSON-Schema (nicht-defaulteter Parameter). | `SymbolGraphToolRegistrations.cs:44-51` (find_symbol als Muster) |
| F4 | **TestKit kann bereits N Server bauen:** `McpInMemoryTestContext.CreateServer()` konstruiert Instanzen per Options-Record — Registry-Tests sind ohne Prozessstart in-memory möglich. | `FastTests/Fixtures/McpInMemoryTestContext.cs:25-29` |
| F5 | **Lifecycle-Bausteine existieren pro Instanz:** atomarer Config-Hot-Swap (`ReloadConfig`), Solution-Reload mit Dispose nach Swap (`ReloadSolutionAsync`), volles `DisposeAsync` inkl. LoadTask-Abbruch, Health-Zähler je Instanz (`RefreshCount`, `LastStalenessStats`, `Uptime`). Eviction = Registry entfernt Key und ruft `DisposeAsync` der Instanz. | `McpCodeGraphServer.cs:150-158, 165-198, 249-295, 129-141` |
| F6 | **Agenten-sichtbarer Init-Vertrag hat eine Single-Source-of-Truth:** `ServerInstructions.Text` geht in den initialize-Handshake; dort gehört der projectRoot-Vertrag hin (einmalig statt 26 Tool-Descriptions volltextlich zu duplizieren). | `ServerInstructions.cs:9-13`; `McpServerOptionsFactory.cs:31` |
| F7 | **Lint-Grenzen beachten:** `MaxAIContextFootprint` auf `McpCodeGraphServer` (deshalb sind Refresh/Factory/Registrations bereits ausgelagert) sowie `MaxConstructorDependencies: 5` (deshalb Options-Record statt langer Parameterliste). Neue Klassen (Registry) entsprechend schlank halten bzw. Options-Record nutzen. | Klassendoku `McpCodeGraphServer.cs:15-24`; `McpCodeGraphServerOptions.cs:71-74`; Kommentar `McpCodeGraphServer.cs:43-45` |
| F8 | **`ResolveSolutionPathOrError` wird im MCP-Pfad NICHT mehr gebraucht:** Da die Definitionsdatei Solution UND Rules explizit benennt, ist die projectRoot-Prüfung streng und trivial — Verzeichnis muss existieren und `<root>/ainetlinter.project.json` muss vorhanden sein. Auto-Suche/Mehrdeutigkeitslogik bleiben allein dem Batch-Modus. (Korrektur zum ersten Entwurf dieses Konzepts.) | `McpServerCommand.cs:232-280` bleibt Batch-only |

### Minimaler Umsetzungspfad (konkrete Skizzen)

Wegen F1/F2 ist der Umbau ein **Wiring-Change plus neue Registry**, kein Refactoring der Serverklasse.
Reihenfolge:

**1) Neue `ProjectRegistry`** (schlank wegen F7):

```csharp
internal sealed record ProjectDefinition(string SolutionPath, string RulesPath); // beide absolut, validiert

internal sealed class ProjectRegistryOptions
{
    public required int MaxProjects { get; init; }        // Default 4
    public required TimeSpan IdleTtl { get; init; }       // Default 45 min
    public required ILintConsole Console { get; init; }
}

internal sealed class ProjectRegistry : IAsyncDisposable
{
    // Key = kanonisierter Root-Pfad (OrdinalIgnoreCase), Value = geladenes Projekt
    private readonly Dictionary<string, ProjectEntry> _projects = new(StringComparer.OrdinalIgnoreCase);
    // Resolve: HIT -> entry.Touch(); MISS -> Definition laden (hart), Server instanziieren,
    //          bei maxProjects zuerst LRU-Eviction (DisposeAsync), dann Eintrag anlegen.
    internal McpCodeGraphServer Resolve(string? projectRoot);
}
```

**2) Definitionsdatei laden** — neuer kleiner Loader (`ProjectDefinitionLoader`): liest
`ainetlinter.project.json`, verlangt beide Felder, löst Pfade relativ zur Datei auf, prüft Existenz
beider Zieldateien. Kein Fallback-Zweig, exakt die Fehlerverträge aus der Tabelle oben.

**3) Wiring umstellen** (F2/F3) — vorher/nachher am Muster `find_symbol`:

```csharp
// VORHER (SymbolGraphToolRegistrations.cs:44-46): Closure auf DIE EINE Instanz
(string? namePattern = null, ...) => FindSymbolTool.ExecuteAsync(mcpState, namePattern, ...)

// NACHER: Closure auf den Resolver; projectRoot ist nicht-defaultet => Pflichtfeld im Schema
(string projectRoot, string? namePattern = null, ...)
    => FindSymbolTool.ExecuteAsync(registry.Resolve(projectRoot), namePattern, ...)
```

- Alle sechs Registration-Klassen analog ändern; `OverviewResourceRegistration` bekommt denselben
  Resolver (Resource braucht ebenfalls projectRoot).
- `McpServerOptionsFactory.Create(McpCodeGraphServer mcpState)` wird zu
  `Create(ProjectRegistry registry)` — der Parameter-Typ dokumentiert die neue Welt.
- `McpServerCommand.RunAsync` hält keine `mcpState`-Instanz mehr, sondern die `ProjectRegistry`
  (plus TTL-Timer); `reload_config`/`get_server_health` werden über denselben Resolver geroutet,
  Health aggregiert über alle Keys (F5 liefert die Pro-Instanz-Werte).

**4) Instructions erweitern** (F6): Der projectRoot-Vertrag („Pflicht bei jedem Tool- und Resource-
Aufruf; Verzeichnis muss `ainetlinter.project.json` enthalten") steht einmalig in
`ServerInstructions.Text` — nicht 26× in Description-Duplikaten.

**5) Tests** (F4): Registry-Unit-Tests bauen N In-Memory-Server via `McpInMemoryTestContext`;
Eviction mit injizierbarer Clock; Contract-Tests für jeden Fehlercode der Tabelle.

### Datenmodell

```csharp
// Pseudostruktur — Platzierung: src/AiNetLinter/Mcp/ProjectRegistry.cs (neu)
Dictionary<string, LoadedProject> _projects   // Key = kanonisierter Root-Pfad (OrdinalIgnoreCase)
record LoadedProject(
    string RootPath,            // kanonisierter projectRoot
    string DefinitionPath,      // aufgelöster Pfad der Definitionsdatei
    string SolutionPath,        // aus Definitionsdatei (relativ → absolut zur Definitionsdatei)
    string RulesPath,           // aus Definitionsdatei (Pflichtfeld, kein Fallback)
    McpCodeGraphServer Server)  // bestehender residenter Server ZU DIESEM Projekt (kein Globalzustand mehr)
```

**Zentraler Refactoring-Schritt:** `McpCodeGraphServer` ist heute prozessglobal (`_catalog`, `_lock`,
`Config`, `UsedDefaultConfig`, `ResolvedConfigPath` als Instanzfelder eines global gehaltenen Servers).
Die Registry macht daraus **eine Instanz pro Projekt**: Jeder Tool-Dispatch löst `projectRoot` →
`LoadedProject` auf und dispatcht gegen `LoadedProject.Server`. Globale Aspekte (Observability-Log,
Call-Log) bleiben prozessweit; projektbezogene Aspekte (Catalog, Config, Staleness-Walk-Grenzen,
Health-Zähler) wandern unter den Projekt-Key. `get_server_health` aggregiert pro Key.

### Ablauf eines Tool-Calls (Zielbild)

```
Tool-Call(projectRoot="C:/repos/foo", ...)
  1. projectRoot normalisieren/kanonisieren (vollständig qualifiziert; OrdinalIgnoreCase-Key)
  2. Registry-Lookup:
     - HIT  → lastUsed aktualisieren → Dispatch gegen registrierten Server
     - MISS → Definitionsdatei prüfen:
         a) <projectRoot>/ainetlinter.project.json vorhanden → laden (solution + rules beide Pflicht)
         b) fehlt → [ERROR] PROJECT_NOT_INITIALIZED (kein Raten!)
  3. Lazy-Init: Solution+Rules laden (bestehende LoadAsync-Pipeline),
     maxProjects geprüft (sonst LRU-Eviction), Registry-Eintrag anlegen
  4. Dispatch gegen den registrierten Projekt-Server; keine Bindungs-Metadaten in
     Analyse-Antworten (Registry-Sichtbarkeit ausschließlich über get_server_health)
```

### Definitionsdatei `ainetlinter.project.json`

**Formatentscheidung JSON, nicht YAML** — Gründe:

1. **Keine neue Abhängigkeit:** Das Projekt hat keinen YAML-Parser; `System.Text.Json` ist via
   Framework verfügbar und `ConfigLoader` existiert bereits als Muster für tolerant-lesenden JSON-Config-Load.
2. **Konsistenz:** `rules.json` ist ebenfalls JSON; ein gemischter Config-Zoo (yaml + json) wäre
   inkonsistent.
3. **Kommentarlosigkeit ist hier akzeptabel:** Bei drei Feldern und zentraler Doku entfällt der
   Hauptvorteil von YAML (Inline-Kommentare) weitgehend.

```json
{
  "solution": "src/AiNetLinter/AiNetLinter.slnx",
  "rules": "config/rules.json"
}
```

- `solution` (**Pflicht**): relativ zur **Definitionsdatei**, nicht zum cwd — dieselbe Datei funktioniert
  auf jedem Checkout. Absoluter Pfad ebenfalls erlaubt.
- `rules` (**Pflicht**): relativ zur Definitionsdatei aufgelöst; kein Fallback, kein Raten — Feld fehlt
  oder Datei existiert nicht → harter Fehler. Der Nachbar-Fallback (`TryResolveRulesJsonPath`) stirbt
  damit im MCP-Pfad ersatzlos.
- Dateiname: **`ainetlinter.project.json`** — ohne führenden Punkt (Windows-Explorer blendet Dotfiles
  ohnehin nicht aus; maschinell wird exakt per Pfad geprüft, Sichtbarkeit hilft Menschen beim Review;
  Präzedenz: `global.json`, `nuget.config`). Suffix `-project`, weil die Datei Ziel **und** Regelwerk
  definiert — sie beschreibt das Lint-Projekt, nicht nur eine Solution.

### Self-Service: Agenten erzeugen die Definitionsdatei selbst

Die Definitionsdatei ist bewusst so einfach (zwei Pflichtfelder), dass ein Coding-Agent sie **ohne
menschliche Hilfe** anlegen kann. Der Wissenstransport läuft über drei Kanäle, ohne dass der Agent
unsere Docs gelesen haben muss:

1. **Fehlertext als primärer Kanal (in-band):** `PROJECT_NOT_INITIALIZED` enthält den erwarteten
   Pfad UND das kopierfähige Minimal-Template mit Feldsemantik. Deterministischer Selbstheilungs-Loop:
   Call schlägt fehl → Agent legt Datei an (Solution/Rules findet er selbst im Verzeichnisbaum) →
   Retry gelingt. Vorgeschriebener Hinweisteil des Fehlertexts (exakt dieser Block, englisch):

   ```text
   Create <root>/ainetlinter.project.json with:
   {
     "solution": "<path/to/your.slnx or .sln>",  // relative to this file, or absolute
     "rules":    "<path/to/rules.json>"          // relative to this file, or absolute; MUST exist
   }
   Then retry the call with the same projectRoot.
   ```

2. **`ServerInstructions.Text`** (F6): eine Zeile zum Dateivertrag im initialize-Handshake — der
   Agent weiß vor dem ersten Aufruf, dass `projectRoot` auf ein Verzeichnis mit
   `ainetlinter.project.json` zeigen muss.
3. **`Docs/agent-api.md`**: Referenzabschnitt „ainetlinter.project.json" (Feldtabelle, relativer Anker,
   Beispiele) für Menschen und Agents mit Doc-Zugriff; zusätzlich AGENTS.md-Abschnitt in diesem Repo
   (Migrationsplan).

Keine Magie nötig: Die Datei ist einmalige Projekt-Infrastruktur (wie `global.json`) — vorhanden heißt
gelesen, fehlend heißt deterministischer Fehler mit Bauanleitung. Bewusst KEIN `init`-Generator-Kommando
(Non-Goal): Das Template im Fehlertext macht einen Generator überflüssig; bei real Bedarf wiedervorlegen.

### Parameter-Strategie

| Ebene | Neu | Bestehend |
|---|---|---|
| **Client-Konfiguration** | nur `command` (+ optional statisches `--mcp-log`) | `--path`/`--config` **entfallen im MCP-Modus** |
| **Pro Call** | `projectRoot` (ausnahmslos Pflicht — kein bedingter Vertrag) | — |

**Keine Ausnahmen:** Auch bei genau einem geladenen Key ist `projectRoot` Pflicht. Ein bedingter Vertrag
(„darf entfallen, wenn …") wäre für Agenten nicht entscheidbar — der Agent weiß nicht, wie viele Keys der
geteilte Prozess aktuell hält. Uniforme Pflicht bedeutet null Inferenzlast, deterministische Fehler und
einfachere Tests. Mehraufwand: ~5 Tokens pro Call.

### Eviction & RAM-Hygiene

Roslyn-Workspaces sind speicherintensiv; ein langlebiger geteilter Host-Prozess (Hermes) akkumuliert
sonst Keys unbegrenzt. Zwei Mechanismen:

1. **Idle-TTL:** Timer (Default: alle 5 Min) disposed Keys mit `lastUsed > N Minuten` (Default: 45 Min;
   konfigurierbar via statische CLI-Parameter, z. B. `--mcp-project-ttl-min`, `--mcp-max-projects`).
   Beim nächsten Call wird der Key frisch geladen — auch gut: frischer Stand garantiert.
2. **maxProjects + LRU:** Default 4; neuer Key bei vollem Registry verdrängt den ältesten.

Caveats (bewusst dokumentiert):

- .NET gibt GC-RAM träge ans OS zurück — die Speicherkurve sägt statt instant zu fallen.
- Solution-Reload kostet Sekunden bis Minuten (MSBuild-Graph); TTL nicht aggressiv setzen.
- Für sauberes Dispose müssen Workspace-/Catalog-Referenzen konsequent freigegeben werden
  (Muster existiert: `ReloadSolutionAsync` in `McpCodeGraphServer.cs:180-192` disposed alt nach Swap).
- Bestehende Prozesshygiene bleibt unberührt: `--parent-pid`-Reaper räumt tote Hosts ab; Eviction ist
  die **projektinterne** zweite Hygiene-Ebene (orthogonal, ersetzt nichts).

### Sichtbarkeit: ausschließlich get_server_health

Bewusst KEIN Binding-Echo in Analyse-Antworten: Da `projectRoot` ausnahmslos Pflicht ist, ist die Bindung
pro Call konstruktiv korrekt. Ein Echo würde bei jedem der 26 Tools Tokens kosten, wäre implementierungs-
invasiv und setzte obendrein darauf, dass ein LLM einen Fließtext vergleicht — nicht deterministisch.
Sichtbarkeit der Registry läuft ausschließlich über `get_server_health`: pro Key Root/Solution/Rules/
lastUsed sowie TTL- und maxProjects-Konfiguration.

### Fehlerverträge (Uninitialized / Defekt)

Alle Fehler strukturiert, deterministisch, mit Handlungsanweisung (englisch, konsistent mit Aufgabe 05):

| Fall | Code (neu) | Textbaustein |
|---|---|---|
| `projectRoot` fehlt | `PROJECT_ROOT_REQUIRED` | Parameter ist ausnahmslos Pflicht |
| Definitionsdatei fehlt | `PROJECT_NOT_INITIALIZED` | Erwarteter Pfad `<root>/ainetlinter.project.json` + kopierfähiges Minimal-Template (siehe „Self-Service") |
| Feld `solution`/`rules` fehlt oder JSON defekt | `PROJECT_DEFINITION_INVALID` | Betroffenes Feld + Definitionsdatei-Pfad |
| Solution laut Definitionsdatei nicht gefunden | `SOLUTION_NOT_FOUND` | Aufgelöster absoluter Pfad (Anker: Definitionsdatei) |
| rules laut Definitionsdatei nicht vorhanden | `RULES_NOT_FOUND` | Aufgeloster absoluter Pfad; kein Default, kein Raten |

`AMBIGUOUS_SOLUTION` entfällt im MCP-Pfad: Die Definitionsdatei benennt konkrete Dateien, Mehrdeutigkeit
kann gar nicht erst entstehen (bestehende Auflösungslogik bleibt dem Batch-Modus vorbehalten). Neue Codes
ergänzen den Katalog (Doku in `Docs/agent-api.md`).

## Tests

Unit (FastTests, Category=Unit):

- Registry: Key-Normalisierung (case-insensitive, Trailing-Slashes), HIT/MISS, lastUsed-Aktualisierung.
- Definitionsdatei-Parsing: Pflichtfelder `solution` UND `rules`, relative→absolute Auflösung
  (Anker = Definitionsdatei), fehlerhaftes JSON / fehlendes Feld → klarer Fehler, keine
  Teil-Initialisierung.
- projectRoot-Auflösung (F8): nicht-existentes Verzeichnis → Fehler; fehlende
  `ainetlinter.project.json` → PROJECT_NOT_INITIALIZED; KEINE Auto-Suche nach Solutions im
  MCP-Pfad (Auto-Suche bleibt Batch-only).
- Uniforme Pflicht: `projectRoot` fehlt → PROJECT_ROOT_REQUIRED — bei beliebigem Registry-Stand
  (auch bei genau einem geladenen Key).
- Kein-Fallback-Vertrag: rules nicht angegeben → RULES_NOT_FOUND (Nachbar-Suche darf nie greifen);
  Solution-Pfad existiert nicht → SOLUTION_NOT_FOUND mit aufgelöstem absolutem Pfad.
- Self-Service-Vertrag: Fehlertext von PROJECT_NOT_INITIALIZED enthält den vorgeschriebenen
  Template-Block (Unit, Text-Assertion); Integration: Call ohne Definitionsdatei → Fehler → Datei
  gemäß Template anlegen → Retry mit gleichem projectRoot gelingt.
- Eviction: TTL mit injizierbarer Clock; LRU-Reihenfolge; maxProjects-Grenze.
- Dispose-Korrektheit: Nach Eviction werden Catalog/Workspace disposet (kein Leakszenario im Test
  assertierbar, aber Dispose-Aufruf und Registry-Entfernung).

Integration (IntegrationTests, Category=Integration):

- End-to-End über MCP-Handshake: zwei Projekte (Fixtures: neutrale, kleine C#-Solutions gemäß
  Übersicht — „Fixtures verwenden neutrale, mehrprojektige C#-Solutions") per projectRoot aktivieren,
  Calls routen korrekt je Key; Bindungs-Verifikation über get_server_health (pro-Key-Zustände).
- Lazy-Init-Perf: erster Call gegen neuen Key dauert messbar länger (Reload-Pfad), zweite sofort.
- Staleness-Walk bleibt auf Projektgrenzen begrenzt (kein Regression zu 02).
- Observability: Call-Log enthält projectRoot/Key (Anschluss Aufgabe 01-Auswertung).
- Reaper unverändert: Parent-Tod terminiert Prozess auch mit mehreren Keys.

Dogfood:

- Eigenes Repo mit `ainetlinter.project.json` im Root versehen (gleicher Task, s. Migrationsplan) und
  Live-Tests
  (`McpLiveRepositoryTests`-Muster) gegen den neuen Vertrag fahren.

Definition of Done (gesamt):

- `dotnet build` grün, beide Nicht-Stress-Testprojekte grün (Richtlinien §2).
- Alle oben genannten Tests implementiert und grün.
- `get_server_health` weist pro-Key-Zustände aus (geladene Keys, lastUsed, TTL/MaxProjects).
- Doku aktualisiert: `Docs/agent-api.md` (Init-Vertrag, neue Fehlercodes),
  `Docs/configuration.md` (CLI-Parameteränderungen MCP-Modus), `Docs/integration.md`
  (Registrierungsbeispiele Hermes/Claude Code/Cline ohne `--path` + ainetlinter.project.json),
  `Docs/ROADMAP.md`,
  `README.md` (Update-Pflicht Richtlinien §4), `.agents/rules/AiNetLinter.mdc` via
  `--sync-agent-rules-only` falls Regel-/CLI-Texte betroffen sind.
- Harter Cut umgesetzt: MCP-Modus lehnt `--path`/`--config` mit unbekanntes-Argument-Fehler ab;
  Batch-Modus unverändert. Eigenes Repo in derselben Änderung migriert: `ainetlinter.project.json`
  im Repo-Root, AGENTS.md-Abschnitt „MCP-Init", Hermes-Registrierung (config.yaml) auf
  `command + --mcp-server` reduziert, Repo-`.mcp.json` entsprechend angepasst.

## Migrationsplan

**Harter Cut, keine Übergangsphase** (Entscheidung 2026-08-22): Es ist ein internes Tool; alle
Client-Konfigurationen (Hermes config.yaml, Repo-`.mcp.json`, ggf. weitere Agenten) sind selbst
verwaltet und werden **im selben Task** umgestellt. Wer die EXE danach mit alten Parametern startet,
bekommt einen harten Fehler (unbekanntes Argument) statt stiller Kompatibilität.

1. **Dieser Task:** Registry + Pflicht-`projectRoot` + `ainetlinter.project.json` + Health-Erweiterung;
   Entfernen von `--path`/`--config` aus dem MCP-Argumentparsing (Batch behält beide); Entfernen des
   Rules-Nachbar-Fallbacks (`TryResolveRulesJsonPath`) aus dem MCP-Pfad; Umstellung aller eigenen
   Registrierungen + AGENTS.md + Repo-Definitionsdatei.
2. **Bewusst nicht:** Deprecations-Warnungen, Kompatibilitäts-Flags, automatische Konvertierung alter
   Konfigurationen, YAML/TOML-Support, Multi-Solution-pro-Definitionsdatei (benannte Projekte) —
   erst bei real Bedarf.

## Risiken

| Risiko | Mitigation |
|---|---|
| Umfang des Wiring-Umbaus über 6 Registration-Klassen + Resource | Mechanische, identische Änderung je Klasse (F3); Contract-Tests zuerst, dann Klassen einzeln umstellen. Kein Refactoring der Serverklasse nötig (F1) — das ursprünglich befürchtete größte Risiko entfällt. |
| Doppelte Tools/Keys bei Hosts, die pro Chat spawnen | Kein Schaden: jeder Prozess hält meist 1 Key; TTL räumt auf. |
| Vergessene `projectRoot`-Angabe durch Agent | Harter deterministischer Fehler (PROJECT_ROOT_REQUIRED); AGENTS.md-Ritual dokumentiert die Pflicht. |
| RAM-Wachstum bei langen Host-Sessions | TTL + maxProjects + bestehender Reaper; Monitoring via get_server_health. |
