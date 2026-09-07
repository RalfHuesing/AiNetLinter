# Finding: Resource `ainetlinter://agent-guide`

Audit-Stand: 2026-09-07. Nur `FetchMcpResource` (Server `user-AiNetLinter`). Kein Tool-Call, kein Build, kein Test, keine Projektintegration.

## 1 Funktion / Schema-Kurzfazit

MCP-Resource (kein Tool). MIME `text/markdown`. Aufgabe: einmaliger Bootstrap-Leitfaden für eine **neue** AiNetLinter-Projektintegration bzw. Wiederherstellung bei fehlender Initialisierung.

Aufruf aus Agentensicht: `FetchMcpResource` mit `server=user-AiNetLinter`, `uri=ainetlinter://agent-guide`, **ohne** `downloadPath`. Es gibt in dieser Cursor-Oberfläche **kein** `resources/list`-Tool und kein Resource-Schema analog `GetDynamicTools`. Die URI muss aus Host-Hinweisen kommen:

- Namespace-Anweisung `user-AiNetLinter`: „Neue Integration nur bei ausdrücklichem Auftrag: `ainetlinter://agent-guide` lesen“.
- Eingebettete und lokale Workflow-Rule: bei `PROJECT_NOT_INITIALIZED` / `RULES_INVALID` **nicht** ungefragt integrieren; Leitfaden nur bei ausdrücklichem Auftrag.

Ohne diese Texte müsste ein Agent die URI raten. Query-Parameter (`?projectRoot=`) sind laut eingebettetem Text **nicht** für diese Resource vorgesehen (nur `overview` / `rules`).

Der Text steuert den Call **korrekt zur Integration** (Projektroot, `ainetlinter.project.json`, `rules.json`, Host-Rule, MCP-Registrierung, Health-Check) und **warnt** vor ungefragtem Bootstrap. Er führt aber in genau die Aktionen, die dieses Audit als Non-Goal verbietet. Ein Agent, der die Resource „nur zum Verstehen“ lädt und die Schritte ausführt, integriert das Projekt.

## 2 Ausgeführte Calls

| # | Absicht | URI / Parameter | Ergebnis | Größe | Truncation | Wartezeit |
|---|---|---|---|---|---|---|
| 1 | Happy Path | `server=user-AiNetLinter`, `uri=ainetlinter://agent-guide`, kein `downloadPath` | Erfolg. Header `Resource: ainetlinter://agent-guide (text/markdown)`. Vier Blöcke: MCP-Bootstrap (Ablauf 1–6), MCP-Projektvertrag, dynamischer Laufzeitpfad, vollständige „Dauerhafte Agentenregel“ (Frontmatter + MCP-first-Text, endet mit Selbstverweis auf diese URI). | grob **~12–14k Zeichen**, **~250–300 Zeilen** (Bootstrap klein, Rule-Dump der Großteil) | keine (`truncated`/`completeness` fehlen; Dokument endet geschlossen) | **schnell** (dieser Turn; erster Fetch im Vorturn ebenfalls sofort) |
| 2 | Fehler, unbekannte URI | `uri=ainetlinter://does-not-exist` | Fehlertext: `Error reading MCP resource: MCP resource not found: ainetlinter://does-not-exist` | wenige Zeilen | n/a | **schnell** |

Kein Katalog-Call möglich: `FetchMcpResource` verlangt eine konkrete URI. Falsche URI liefert einen klaren Not-Found, keine Resource-Liste als Hint.

Dynamischer Block in Call 1 (live, nicht geraten):

```json
{
  "command": "C:\\Daten\\Tools\\AiNetLinter-win-x64\\AiNetLinter.exe",
  "args": ["--mcp-server"]
}
```

## 3 Verdict

**Teilweise wie beschrieben.** Fetch und Fehlerpfad funktionieren. Der Text ist der erwartete Bootstrap plus eingebettete Dauerregel plus Laufzeit-`command`/`args`.

Abweichungen / Reibung:

- Resource ist **ohne Raten nur findbar**, wenn Namespace-Instruktion oder Workflow-Rule schon im Kontext liegt. Keine agentenseitige Resource-Entdeckung.
- Der Leitfaden ist **nicht klein**: er hängt die gesamte always-on-Rule an. Das verdoppelt Token, die ein Platform-Agent oft schon aus `.cursor/rules/AiNetLinter-McpWorkflow.mdc` hat.
- Inhaltlich **kein Widerspruch** zur Workflow-Rule beim Integrations-Gate (beide: nur bei ausdrücklichem Auftrag). Die Resource **ist** aber die Anleitung zu den gefährlichen Schreib-/Registrierschritten.
- Formulierung „die beiden Assembly-only-Tools“ in der eingebetteten Rule wirkt gegenüber dem größeren Assembly-Inventar veraltet.
- `view_file` / `list_dir` werden in der Rule als Werkzeuge genannt; in der nativen MCP-Tool-Liste dieser Session sind sie nicht als AiNetLinter-Tools sichtbar (Host-Read vs. MCP — Agent kann das als MCP-Tool missverstehen).

## 4 Schwere

**`friction`** (Discovery, Token-Duplikat, veraltete „beide Assembly-Tools“-Formulierung).

Kein `broken`: Happy Path und Not-Found sind brauchbar. Kein `degraded` im Sinne falscher Fakten zum Bootstrap selbst. `wish` nur als Ergänzung (Katalog/Hint), nicht als Hauptschwere.

Risiko **neue Integration**: der Text allein ist kein Bug, aber ein Agent ohne striktes Non-Goal kann nach dem Fetch Dateien anlegen. Das Audit hat die Schritte **nicht** ausgeführt.

## 5 Nutzbarkeit

**Nur mit Workaround / nur bei Integrationsauftrag.**

- Bei ausdrücklichem Bootstrap: ja — Ablauf ist linear, Laufzeitblock ist konkret.
- Für normalen C#-MCP-Alltag: **nein, nicht erneut fetchen.** Die dauerhafte Rule liegt bereits im Host; ein zweiter Full-Dump verbrennt Kontext und erhöht die Chance, Integrationsschritte „mitzuerledigen“.
- Workaround Discovery: URI aus Namespace-`namespaceUseInstructions` oder aus der lokalen `AiNetLinter-McpWorkflow.mdc` kopieren, nicht raten.

## 6 Bugs + FP/FN

| Befund | Art | Bewertung |
|---|---|---|
| Keine Resource-Liste über `FetchMcpResource`; unbekannte URI → Not-Found ohne Katalog. | Discovery-FN | Agent findet `agent-guide` nicht aus dem Resource-Mechanismus selbst. URI muss extern bekannt sein. |
| Call 2: `MCP resource not found` inkl. URI. | Fehlerpfad OK | Maschinenlesbar, kein Hang, kein leeres Erfolgsergebnis. |
| Eingebettete Rule spricht von „beiden Assembly-only-Tools“. | Text / Drift | Inventar hat mehr Assembly-only-Funktionen. Kein Fetch-Bug, aber irreführend. |
| `view_file` / `list_dir` in der Rule. | FP für Toolwahl | Können als MCP-Tools gelesen werden; in dieser Session nicht als `user-AiNetLinter`-Tools sichtbar. |
| Bootstrap fordert Anlegen/Aktualisieren von `ainetlinter.project.json`, Rule-Datei, Host-Rule, MCP-Registrierung. | Gefährliche Folge, kein Lügen | Vertragstreu für Integration; **Konflikt mit Audit-Non-Goal**, wenn der Agent den Text als Pflicht-Checkliste behandelt. |
| Kein Truncation-Marker trotz Größe. | kein Bug | Dokument ist vollständig; Token-Last kommt vom Rule-Anhang, nicht von abgeschnittener Ausgabe. |

Stichprobe Laufzeitpfad vs. bekannter Binary-Ort `C:\Daten\Tools\AiNetLinter-win-x64\AiNetLinter.exe`: **Treffer**, kein FP.

## 7 Token-Effizienz / Folge-Hints

- Bootstrap + Vertrag + Laufzeitblock allein wären klein und folgetauglich.
- Der **volle Rule-Dump** macht den Großteil der ~12–14k Zeichen. Für einen bereits integrierten Workspace (diese Platform) ist das redundant.
- Folge-Hints im Text: `get_server_health`, `ainetlinter --docs rules-json` / `mcp-rule`, Resource-URIs `ainetlinter://overview{?projectRoot}` und `ainetlinter://rules{?projectRoot}` (URL-kodierter absoluter `projectRoot`). Keine Symbol-IDs, kein `continuationToken`.
- StructuredContent: nicht sichtbar; reine Markdown-Resource.
- Fehler-Call liefert **keinen** Hint „meintest du `ainetlinter://agent-guide`?“.

## 8 Roslyn-Wünsche

Keine Roslyn-Analyse. Sinnvolle Server-/Host-Wünsche im bestehenden MCP-Rahmen:

- Resource-Katalog oder Not-Found mit bekannten URIs (`agent-guide`, `overview`, `rules`), damit Agenten nicht raten.
- `agent-guide` in zwei Teile: kurzer Bootstrap + optionaler Rule-Anhang (Default ohne Full-Dump, wenn die Host-Rule schon liegt).
- „Beide Assembly-only-Tools“ an das echte Inventar anpassen; `view_file`/`list_dir` als Host-Tools kennzeichnen, nicht als AiNetLinter-MCP.

## 9 Phase 3 (AiNetLinter-Quellzeiger)

Welle 3, read-only `C:\Daten\Entwicklung\Ralf\AiNetLinter`. Kein Patch.

### Discovery-FN: unbekannte URI → Not-Found ohne Katalog

- **Pfad:** `src\AiNetLinter\Mcp\Composition\McpServerResourceCollectionFactory.cs`; `src\AiNetLinter\Mcp\Registration\McpAgentGuideRegistration.cs`; `src\AiNetLinter\Mcp\ServerInstructions.cs`
- **Symbol:** `McpServerResourceCollectionFactory.Build`; `McpAgentGuideRegistration.Register` (`UriTemplate = "ainetlinter://agent-guide"`); `ServerInstructions.Text`
- **Ansatz:** Kein Catch-All — `ainetlinter://does-not-exist` trifft keinen Handler. `resources/list` existiert protokollseitig über die Collection, Cursor-`FetchMcpResource` listet nicht. Statisch: Resource `ainetlinter://catalog` (drei URIs + Query-Beispiel) in `Build` registrieren; `ServerInstructions.Text` um `ainetlinter://rules{?projectRoot}` ergänzen (heute nur Guide + Overview). Kein LLM, keine Roslyn-Analyse.

### Token-Duplikat: Default hängt die volle Always-on-Rule an

- **Pfad:** `src\AiNetLinter\Mcp\Registration\McpAgentGuideRegistration.cs`; eingebettet `Docs\mcp-bootstrap.md`, `AgentRules\AiNetLinter-McpWorkflow.mdc` (Quelle `.agents\rules\AiNetLinter-McpWorkflow.mdc`)
- **Symbol:** `McpAgentGuideRegistration.BuildGuideText` (`bootstrap + runtimeRegistration + "## Dauerhafte Agentenregel" + workflow`); Tests in `McpAgentGuideRegistrationTests.BuildResource_IsReadableWithoutProjectAndContainsIntegrationContract` verlangen den Full-Dump
- **Ansatz:** Default nur Bootstrap + `McpRegistrationInstructions.BuildRuntimeBlock`. Workflow optional (`ainetlinter://agent-guide?includeWorkflow=true` oder zweite Resource). `EmbeddedResourceReader.ReadRequired` bleibt; kein neues Verstehen.

### Text-Drift: „beide Assembly-only-Tools“

- **Pfad:** `.agents\rules\AiNetLinter-McpWorkflow.mdc` (Zeilen mit „Für die beiden Assembly-only-Tools“ / „Für beide Assembly-Tools“); Spiegel in `ServerInstructions.Text`
- **Symbol:** eingebetteter Workflow-String in `BuildGuideText`; `ServerInstructions.Text` listet `inspect_assembly`, `find_assembly_extensions`, `search_assembly` — ohne `get_assembly_context` (`AssemblyAnalysisToolRegistrations`)
- **Ansatz:** Inventar-Literale an die Registrierungsliste angleichen (vier Assembly-only-Tools). Reine String-Pflege der eingebetteten Rule, kein Roslyn.

### FP Toolwahl: `view_file` / `list_dir` in der Rule

- **Pfad:** `.agents\rules\AiNetLinter-McpWorkflow.mdc` (Tabelle „Kleiner … Codeausschnitt“ / „Konfiguration, Projektstruktur“)
- **Symbol:** dieselben Zeilen, die `BuildGuideText` unverändert anhängt
- **Ansatz:** Host-Read vs. MCP kennzeichnen (`view_file`/`list_dir` nicht als AiNetLinter-Tools). Kein neues MCP-Tool.

### Gefährliche Folge: Bootstrap-Checkliste nach Fetch

- **Pfad:** `Docs\mcp-bootstrap.md` (Ablauf 2–5: `rules.json` erzeugen, `ainetlinter.project.json` anlegen, Host-Rule, MCP-Registrierung)
- **Symbol:** `McpAgentGuideRegistration.BootstrapResourceName`; Inhalt wird in `BuildGuideText` ungekürzt vorangestellt
- **Ansatz:** Bootstrap belassen (Integrationsauftrag). Kopf bereits „nur bei ausdrücklichem Auftrag“. Kopplung zum Rule-Dump lösen (Befund oben), damit Alltag-Fetch nicht die Schreibschritte mitsendet. Keine neue Integration in diesem Task.

### Fehler-Call ohne Hint auf bekannte URIs

- **Pfad:** kein eigener Handler — MCP-SDK 404 vor `BuildResource`
- **Symbol:** `McpAgentGuideRegistration.Register` (feste URI, kein Template)
- **Ansatz:** siehe Katalog-Resource. Not-Found unbekannter URIs ist Host/SDK; ohne Catch-All bleibt nur eine listbare Resource plus `ServerInstructions`.
