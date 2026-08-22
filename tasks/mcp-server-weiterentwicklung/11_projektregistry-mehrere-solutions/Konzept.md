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
herkunft: "Diskussion 2026-08-22 (ox-alpha + Nutzer): MCP-Server wurde global mit hartkodierter --path/--config-Registrierung genutzt; in Multi-Projekt-/Multi-Agent-Setups bindet er still an die falsche Solution."
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
| **Kein FileSystemWatcher-basiertes yaml-Reload** | yaml wird nur beim Key-Load gelesen; Re-Init läuft über erneutes `activate_project`. Konsistent mit 02-Staleness-Entscheidung (Messung vor Watcher). |
| **Kein Umbau der Batch-Pipeline** | `--path`/`--config` bleiben für Batch-Lint vollständig erhalten; Änderungen betreffen ausschließlich den MCP-Modus. |
| **Keine Entfernung bestehender Tools** | Alle 26 Tools bleiben; es kommt ein Initialisierungsvertrag hinzu, keine Removal (konsistent mit `90 §B.1`). |
| **Kein Multi-Agent-Installer / Detached-Daemon / Cloud** | Weiter gültig (`90 §C.5`, `§C.6`). |

## Architektur

### Datenmodell

```csharp
// Pseudostruktur — Platzierung: src/AiNetLinter/Mcp/ProjectRegistry.cs (neu)
Dictionary<string, LoadedProject> _projects   // Key = kanonisierter Root-Pfad (OrdinalIgnoreCase)
record LoadedProject(
    string RootPath,            // kanonisierter projectRoot
    string DefinitionPath,      // aufgelöster Pfad der Definitionsdatei
    string SolutionPath,        // aus Definitionsdatei (relativ → absolut zur Definitionsdatei)
    string? RulesPath,          // aus Definitionsdatei oder Fallback "neben Solution"
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
     - MISS → Definitionsdatei suchen:
         a) <projectRoot>/.ai-netlinter.json vorhanden → laden
         b) fehlt → [ERROR] PROJECT_NOT_INITIALIZED mit Schema-Hilfe (kein Raten!)
  3. Lazy-Init: Solution+Rules laden (bestehende LoadAsync-Pipeline),
     maxProjects geprüft (sonst LRU-Eviction), Registry-Eintrag anlegen
  4. Dispatch + Antwort mit Binding-Header: "[Project]: <root> · Solution: X · rules: Y"
```

### Definitionsdatei `.ai-netlinter.json`

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

(`$schema`-Verweis optional ergänzbar; ob und wo ein JSON-Schema publiziert wird, entscheidet sich bei der
Umsetzung — das Beispiel oben zeigt bewusst nur die Pflicht-/Optional-Felder.)

- `solution` (**Pflicht**): relativ zur **Definitionsdatei**, nicht zum cwd — dieselbe Datei funktioniert
  auf jedem Checkout. Absoluter Pfad ebenfalls erlaubt.
- `rules` (optional): relativ zur Definitionsdatei; fehlt es → bewährter Nachbar-Fallback („rules.json
  neben der Solution"), wie heute (`TryResolveRulesJsonPath`).
- Dateiname: **`.ai-netlinter.json`** (dot-prefixed = fällt in gängigen Explorer-Views auf, signalisiert
  „Tool-Config"; Alternativen `ainetlinter.json`/`AiNetLinter.json` verworfen, um Verwechslung mit
  `rules.json` zu vermeiden).

### Parameter-Strategie

| Ebene | Neu | Bestehend |
|---|---|---|
| **Client-Konfiguration** | nur `command` (+ optional statisches `--mcp-log`) | `--path`/`--config` **entfallen im MCP-Modus** |
| **Pro Call** | `projectRoot` (Pflicht bei ≥2 geladenen Keys; optional bei genau 1) | — |

**Boilerplate-Abmilderung:** Ist **genau ein** Projekt geladen, darf `projectRoot` entfallen (Fallback
auf das eine). Ab **zwei** geladenen Keys: Pflicht, bei Fehlen harter Fehler mit Key-Liste. Nie still raten.

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

### Sichtbarkeit (Binding-Header)

Jede Tool-Antwort trägt eine Kopfzeile mit der tatsächlichen Bindung:
`[Project]: C:/repos/foo · Solution: foo.slnx · rules: rules.json`.
Damit wird die (seltene) Restgefahr — Chat A arbeitet gegen Projekt Y, obwohl es „dacht" Projekt X —
sofort sichtbar. Strukturell zusätzlich als `structuredContent.project` ausweisen (Modelle vergleichen
strukturierte Felder verlässlicher als Fließtext).

### Fehlerverträge (Uninitialized / Defekt)

Alle Fehler strukturiert, deterministisch, mit Handlungsanweisung (englisch, konsistent mit Aufgabe 05):

| Fall | Code (neu) | Textbaustein |
|---|---|---|
| Kein `projectRoot` bei ≥2 Keys | `PROJECT_ROOT_REQUIRED` | Liste der aktiven Keys + Hinweis auf AGENTS.md-Ritual |
| Definitionsdatei fehlt | `PROJECT_NOT_INITIALIZED` | Erwarteter Pfad + minimales JSON-Schema-Beispiel |
| Solution laut json nicht gefunden | `SOLUTION_NOT_FOUND` | Aufgelöster Pfad + Hinweis Relativität zur Definitionsdatei |
| ≥2 Solutions laut json mehrdeutig | `AMBIGUOUS_SOLUTION` | Kandidatenliste (nutzt bestehende Logik) |
| rules.json weder angegeben noch findbar | `RULES_NOT_FOUND` | Bewusster Default-Rules-Hinweis |

Bestehende Fehlercodes (`ResourceNotFound`, `AmbiguousSolution`, …) werden wiederverwendet, wo sie passen;
neue Codes ergänzen den Katalog (Doku in `Docs/agent-api.md`).

## Tests

Unit (FastTests, Category=Unit):

- Registry: Key-Normalisierung (case-insensitive, Trailing-Slashes), HIT/MISS, lastUsed-Aktualisierung.
- Definitionsdatei-Parsing: Pflichtfeld `solution`, optionale `rules`, relative→absolute Auflösung
  (Anker = Definitionsdatei), fehlerhaftes JSON → klarer Fehler, keine Teil-Initialisierung.
- projectRoot-Auflösung: Datei vs. Verzeichnis vs. nicht existent (Wiederverwendung
  `ResolveSolutionPathOrError`-Semantik).
- Boilerplate-Regel: 0 Keys → PROJECT_NOT_INITIALIZED; 1 Key → projectRoot optional; ≥2 Keys ohne
  projectRoot → PROJECT_ROOT_REQUIRED mit Keyliste.
- Eviction: TTL mit injizierbarer Clock; LRU-Reihenfolge; maxProjects-Grenze.
- Dispose-Korrektheit: Nach Eviction werden Catalog/Workspace disposet (kein Leakszenario im Test
  assertierbar, aber Dispose-Aufruf und Registry-Entfernung).

Integration (IntegrationTests, Category=Integration):

- End-to-End über MCP-Handshake: zwei Projekte (Fixtures: neutrale, kleine C#-Solutions gemäß
  Übersicht — „Fixtures verwenden neutrale, mehrprojektige C#-Solutions") aktivieren, Calls routen
  korrekt je Key; Antwort-Header zeigt richtige Bindung.
- Lazy-Init-Perf: erster Call gegen neuen Key dauert messbar länger (Reload-Pfad), zweite sofort.
- Staleness-Walk bleibt auf Projektgrenzen begrenzt (kein Regression zu 02).
- Observability: Call-Log enthält projectRoot/Key (Anschluss Aufgabe 01-Auswertung).
- Reaper unverändert: Parent-Tod terminiert Prozess auch mit mehreren Keys.

Dogfood:

- Eigenes Repo mit `.ai-netlinter.json` im Root versehen (Migration, s. u.) und Live-Tests
  (`McpLiveRepositoryTests`-Muster) gegen beide Wege fahren.

Definition of Done (gesamt):

- `dotnet build` grün, beide Nicht-Stress-Testprojekte grün (Richtlinien §2).
- Alle oben genannten Tests implementiert und grün.
- `get_server_health` weist pro-Key-Zustände aus (geladene Keys, lastUsed, TTL/MaxProjects).
- Doku aktualisiert: `Docs/agent-api.md` (Init-Vertrag, neue Fehlercodes, Binding-Header),
  `Docs/configuration.md` (CLI-Parameteränderungen MCP-Modus), `Docs/integration.md`
  (Registrierungsbeispiele Hermes/Claude Code/Cline ohne `--path`), `Docs/ROADMAP.md`,
  `README.md` (Update-Pflicht Richtlinien §4), `.agents/rules/AiNetLinter.mdc` via
  `--sync-agent-rules-only` falls Regel-/CLI-Texte betroffen sind.
- Migration dieses Repos: `.ai-netlinter.json` im Repo-Root, AGENTS.md-Abschnitt „MCP-Init",
  eigene Hermes-Registrierung umgestellt.

## Migrationsplan

1. **Phase 1 (dieser Task):** Registry + `projectRoot` + `.ai-netlinter.json` + Binding-Header +
   Health-Erweiterung. `--path`/`--config` im MCP-Modus weiterhin honorieren, aber mit
   `[WARN]: Deprecation — --path/--config im MCP-Modus werden ab vNext ignoriert; nutze .ai-netlinter.json + projectRoot`.
   Grund: Bestehende Registrierungen (Hermes config.yaml, `.mcp.json` im Repo) leben schon draußen.
2. **Phase 2 (Folgetask):** Flags im MCP-Modus entfernen (Breaking, eigene Release-Note), Batch-Modus
   unverändert. AGENTS.md dieses Repos und `.mcp.json` umstellen; Hermes-Registrierung (config.yaml)
   auf `command + --mcp-server` reduzieren.
3. **Bewusst nicht Teil dieses Tasks:** Automatische Konvertierung alter Registrierungen, Support für
   YAML/TOML-Definitionsdateien, Multi-Solution-pro-yaml (benannte Projekte) — erst bei real Bedarf.

## Risiken

| Risiko | Mitigation |
|---|---|
| Refactoring-Tiefe: `McpCodeGraphServer` von Global- zu Per-Project-State | Größter Einzelposten; Delegate-Closure-Muster (Übersicht: „Tools erreichen den residenten Serverzustand per Delegate-Closure") bleibt erhalten, nur Zielobjekt wird der Key-Server. Schrittweise Umsetzung, Contract-Tests zuerst. |
| Doppelte Tools/Keys bei Hosts, die pro Chat spawnen | Kein Schaden: jeder Prozess hält meist 1 Key; TTL räumt auf. |
| Vergessene `projectRoot`-Angabe durch Agent | Binding-Header + harter Fehler bei Mehrdeutigkeit; AGENTS.md-Ritual dokumentiert. |
| RAM-Wachstum bei langen Host-Sessions | TTL + maxProjects + bestehender Reaper; Monitoring via get_server_health. |
