# Finding: Resource `ainetlinter://overview`

Audit-Stand: 2026-09-07. Nur `FetchMcpResource` (Server `user-AiNetLinter`), URI-Muster `ainetlinter://overview{?projectRoot}`. Kein Tool-Call, kein Build, kein Test, kein `downloadPath`.

Drei Calls **strikt sequenziell**. Kein Fetch hing; nach jedem klaren Erfolg bzw. Fehler ging es sofort weiter.

---

## 1 Schema-Kurzfazit

MCP-Resource (kein Tool). MIME im Happy Path `text/markdown`. Aufgabe laut Workflow-Rule: **Projektstatus** der adressierten Source-Solution prüfen (`projectRoot` absolut und URL-kodiert). Query ist laut Rule **pflicht** und nur für `overview` / `rules` vorgesehen.

Es gibt in dieser Cursor-Oberfläche **kein** `resources/list` und kein Resource-Schema analog `GetDynamicTools`. Die URI muss aus Host-Hinweisen kommen (`namespaceUseInstructions`, `AiNetLinter-McpWorkflow.mdc`, Verweis aus `agent-guide`).

Der erfolgreiche Text bestätigt gebundenes Projekt, Solution-`.slnx` und Regeldatei plus drei Folge-Hints. Er ist **kein** Health-/Index-Report: keine Initialisierungskennung, kein `RULES_INVALID`, keine Dokument-/Symbolzahlen, keine Session-Liste. Ohne gültiges Query bzw. mit unbekanntem Pfad kommt **kein** Schema-Hinweis, sondern dieselbe Klasse `MCP resource not found`.

---

## 2 Calls

Server: `user-AiNetLinter`. Kein `downloadPath`. Projektroot-Ist: `C:\Workspace\Sample.Project`.

| # | Absicht | URI | Größe | Truncation | Wartezeit | Ergebnis |
|---|---|---|---|---|---|---|
| 1 | Happy Path, kodierter Root | `ainetlinter://overview?projectRoot=C%3A%5CDaten%5CEntwicklung%5CSample%5CSample.Project` | Header `Resource: … (text/markdown)`. Körper grob **~700–850 Zeichen**, **~15–20 Zeilen** | keine (`truncated`/`completeness` fehlen; Dokument endet geschlossen) | **schnell** (sofort im Turn) | Erfolg. Titel „Projektstatus“. Solution-Pfad `…\Sample.Project.slnx`. Regeln `…\Tests.Logic\AiNetLinter\rules\platform-default.rules.json`. „Zuletzt genutzt (UTC): 2026-09-07 09:35:06“. Block „Weiter“: `ainetlinter://agent-guide`, `tools/list`, `get_impact` / `get_violations`. Intro: Server analysiert die Solution „semantisch ueber Roslyn“. |
| 2 | Ohne Query | `ainetlinter://overview` | wenige Zeilen | n/a | **schnell** | Fehler: `Error reading MCP resource: MCP resource not found: ainetlinter://overview` |
| 3 | Falsch **und** unkodiert | `ainetlinter://overview?projectRoot=C:\DoesNotExist` | wenige Zeilen | n/a | **schnell** | Fehler: `Error reading MCP resource: MCP resource not found: ainetlinter://overview?projectRoot=C:\DoesNotExist`. Host hat die unkodierten Backslashes **nicht** vorab als URI-Syntax verworfen. |

Call 1 (Körper, vollständig — so klein, dass Kürzen unnötig ist):

- Solution: `C:\Workspace\Sample.Project\Sample.Project.slnx`
- Regeln: `C:\Workspace\Sample.Project\Sample.Project.Tests.Logic\AiNetLinter\rules\platform-default.rules.json`
- Zuletzt genutzt (UTC): `2026-09-07 09:35:06`
- Hints: `ainetlinter://agent-guide`, `tools/list`, `get_impact` und `get_violations`

Kein vierter Call. Encoded-falsch vs. unencoded-korrekt wurden **nicht** getrennt; Call 3 kombiniert beides.

---

## 3 Verdict

**Teilweise wie beschrieben.** Mit korrekt kodiertem absolutem `projectRoot` antwortet die Resource sofort, kompakt und mit plausiblen Pfaden (`.slnx` und `platform-default.rules.json` liegen so im Workspace). Query-Pflicht ist live: ohne Query gibt es die Resource nicht.

Abweichungen / Reibung:

- „Projektstatus“ ist faktisch eine **Pfadbestätigung plus Timestamp**, kein Status (Health, Index, Regeln gültig, bereit für Symbolcalls).
- Fehlerklasse **identisch** für fehlende Query, unbekannten Root und unkodierten Pfad. Kein `PROJECT_NOT_INITIALIZED`, kein „`projectRoot` fehlt / muss URL-kodiert sein“.
- Folge-Hint `tools/list` ist MCP-Protokoll, in dieser Agent-Oberfläche nicht der sichtbare Weg (`GetDynamicTools`).
- Folge-Hint `agent-guide` steht auch auf einem **bereits integrierten** Projekt — das widerspricht der Workflow-Regel „Leitfaden nur bei ausdrücklichem Integrationsauftrag“.
- Kein Truncation-Marker nötig (winzig), aber auch kein Completeness-/Freshness-Satz außer „Zuletzt genutzt“.

---

## 4 Schwere

**`friction`**

Kein `broken`: Happy Path und Not-Found kommen sofort, ohne Hang, ohne leeres Erfolgsergebnis. Kein `degraded` im Sinne nachweislich falscher Solution-/Regelpfade. Nicht nur `wish`: fehlende Query-Entdeckung und die vermischten Fehlerpfade zwingen zum Raten; der Status-Name verspricht mehr als drei Bullet-Punkte.

Risiko **falsches Grün**: ein Agent, der Overview als Health-Check nach Integration nutzt, sieht Markdown-Erfolg und kann `get_server_health` / Index überspringen.

---

## 5 Nutzbarkeit

**Nur mit Workaround**, sobald der kodierte `projectRoot` bekannt ist.

- **Ja** für: schnell prüfen, **welche** Solution und **welche** `rules.json` der Server an diesen Root bindet.
- **Nein** für: Resource-Entdeckung, Diagnose „warum geht find_symbol nicht“, Unterscheidung Query-Fehler vs. unbekanntes Projekt, Ersatz für `get_server_health` / `get_index_scope`.
- Workaround: URI aus der Workflow-Rule kopieren; `projectRoot` als `C%3A%5C…` kodieren; echten Betriebsstatus über Health/Index-Tools holen; `agent-guide` nach Overview **nicht** mitfetchen.

---

## 6 Bugs

| Befund | Art | Call |
|---|---|---|
| Ohne Query und mit falschem/unkodiertem Root dieselbe Meldung `MCP resource not found` inkl. Echo der URI | Discovery-FN / undifferenzierter Fehler | 2, 3 |
| Not-Found ohne Katalog der bekannten URIs (`overview`, `rules`, `agent-guide`) und ohne Query-Schema | Discovery-FN | 2, 3 |
| Unkodierter Pfad wird durchgereicht, nicht als Encoding-Fehler erklärt | Fehlerpfad unscharf | 3 |
| Overview enthält keinen Initialisierungs-/Regelvaliditäts-Status | Lücke vs. Name „Projektstatus“ | 1 |
| Hint `tools/list` in Cursor-Agenten-Kontext irreführend | FP für Toolwahl | 1 |
| Hint `ainetlinter://agent-guide` auf integriertem Projekt | gefährlicher Folge-Nudge | 1 |
| Intro „laeuft“ / „ueber“ (ASCII-faltiges Deutsch) | Kosmetik, kein Fetch-Bug | 1 |
| Kein Completeness-Feld | kein Bug bei dieser Größe | 1 |

Stichprobe Pfade vs. bekanntes Workspace-Layout: Solution-`.slnx` und `platform-default.rules.json` unter Tests.Logic **treffen**. Timestamp nicht unabhängig verifiziert (Auftrag: nur diese Resource).

---

## 7 Token/Hints

Call 1 ist **agententauglich klein** (~0,8k Zeichen). Kein Flood, kein Cursor, keine Symbol-IDs, kein `continuationToken`. StructuredContent nicht sichtbar; reine Markdown-Resource.

Nützliche Hints: `get_impact`, `get_violations` (nach Änderungen). Weniger nützlich hier: `agent-guide` (Bootstrap), `tools/list` (Protokoll statt Session-Tool). Fehler-Calls liefern **keinen** Hint „`projectRoot` URL-kodiert angeben“ und kein Beispiel-URI.

---

## 8 Roslyn-Wünsche

Keine Roslyn-Analyse in diesem Lauf. Sinnvolle Server-/Host-Wünsche im bestehenden MCP-/Roslyn-Rahmen:

- Overview um **Workspace-Fakten** erweitern, die der Server schon hat: Initialisiert ja/nein, Regeln gültig, Solution-Pfad, grobe Indexzahlen (Projekte/Dokumente) — ohne Tool-Inventar-Dump.
- Fehler **trennen**: fehlende Query / nicht URL-kodiert / unbekannter Root / `PROJECT_NOT_INITIALIZED` / `RULES_INVALID`.
- Not-Found mit bekannten Resource-URIs und einem kodierten Beispiel-`projectRoot`.
- `agent-guide` nur hinten, wenn das Projekt **nicht** integriert ist; sonst Health/Index.
- `tools/list` durch die in der Session sichtbaren Schema-Tools ersetzen oder als MCP-Protokoll kennzeichnen.

---

## 9 Phase 3 (AiNetLinter-Quellzeiger)

Welle 3, read-only `C:\Daten\Entwicklung\Ralf\AiNetLinter`. Kein Patch.

### Undifferenzierter Fehler: fehlende Query / unbekannter Root / unkodierter Pfad → `MCP resource not found`

- **Pfad:** `src\AiNetLinter\Mcp\Registration\OverviewResourceRegistration.cs`; `src\AiNetLinter\Mcp\Registration\ProjectResourceLease.cs`; `src\AiNetLinter\Mcp\Projects\ProjectToolCall.cs`; `src\AiNetLinter\Mcp\Projects\ProjectDefinitionLoader.cs`
- **Symbol:** `OverviewResourceRegistration.Register` (`UriTemplate = "ainetlinter://overview{?projectRoot}"`); `ProjectResourceLease.Execute` (`throw new McpException`); `ProjectToolCall.GuardRequiredAbsoluteRoot` (`PROJECT_ROOT_REQUIRED` / `_INVALID`); `ProjectRegistry.Lease` → `PROJECT_NOT_INITIALIZED`. Unit: `OverviewResourceRegistrationTests.BuildTemplatedResult_UnknownKey_ThrowsProjectNotInitialized` — Exception-Text enthält den Code, Cursor-`FetchMcpResource` kollabiert ihn zu Not-Found.
- **Ansatz:** (1) Nackte URI `ainetlinter://overview` zusätzlich registrieren, sonst bindet das Template ohne Query nie und der Guard läuft nicht. (2) Statt `throw new McpException` ein `ReadResourceResult` mit `TextResourceContents` + `LinterErrorFormatter.Format` (gleicher Code wie Tools), damit der Host den Body nicht zu 404 macht. (3) `RecoverHint` für Resources: Beispiel `ainetlinter://overview?projectRoot=` + `Uri.EscapeDataString`, nicht `NotInitializedTemplate` (der stößt Dateianlage an). Encoding: `Path.IsPathRooted` akzeptiert `C:\…`; kein Encoding-Guard nötig — Combo-Call zeigte unkodierten gültigen Root als Erfolg. Unkodierter Fake-Root ist derselbe Lease-Fehler, nicht ein URI-Syntax-Fail.

### Name „Projektstatus“ vs. Pfad+Timestamp

- **Pfad:** `src\AiNetLinter\Mcp\Registration\OverviewResourceRegistration.cs`; Vergleich `src\AiNetLinter\Mcp\Tools\ServerMaintenance\GetServerHealthFormatter.cs`, `…\Projection\ProjectHealthProjection.cs`
- **Symbol:** `BuildOverviewText` / `DescribeSolution` / `DescribeConfig` — `LoadState` und `GetConfigSnapshot` sind schon da, werden aber nur für Loading/LoadFailed bzw. Default-vs-Pfad genutzt. Health hat `LoadState`, Uptime, Staleness, `LastGoodStateUtc`.
- **Ansatz:** In `BuildOverviewText` dieselben residenten Fakten ausgeben: `snapshot.Server.LoadState`, Config-Validität aus `GetConfigSnapshot` (kein zweiter Disk-Read). Grobe Indexzahlen über Roslyn `McpCodeGraphServer.GetCurrentSolution()`: `Solution.Projects.Count` und Document-Count (`Microsoft.CodeAnalysis.Project.Documents`, optional `.cs` über `SourceFileCatalog.IsValidDocument`). Kein Filesystem-Scan wie `GetIndexScopeScanner.BuildBreakdown`; dafür Hint `get_index_scope` / `get_server_health`. `RULES_INVALID` bleibt Lease-Fehler vor dem Render.

### Hint `tools/list` und `agent-guide` auf integriertem Projekt

- **Pfad:** `OverviewResourceRegistration.cs` (`BuildOverviewText`, Block „Weiter“); Intro-Satz „laeuft als stdio-MCP-Server“
- **Symbol:** die drei `sb.AppendLine("- …")` nach `## Weiter`; Transport-Claim ist Literal, unabhängig von `DaemonHealthProjection` / Health-`Mode`
- **Ansatz:** „Weiter“ am Loaded-Key: `get_server_health`, `ainetlinter://rules?projectRoot=…` (kanonische URI über `BuildCanonicalUri`), `get_impact`/`get_violations`. `agent-guide` nur wenn Lease `PROJECT_NOT_INITIALIZED`/`RULES_INVALID` wäre (dann ohnehin Fehler-Body). `tools/list` als MCP-Protokoll kennzeichnen oder weglassen. Intro: Transport nicht hart auf stdio; Mode aus demselben Runtime-Kontext wie Health oder Satz streichen. Kein Roslyn.

### Not-Found ohne Katalog bekannter Resource-URIs

- **Pfad:** `McpServerResourceCollectionFactory.cs`; `ServerInstructions.cs`
- **Symbol:** `Build` registriert Guide/Overview/Rules; `ServerInstructions.Text` nennt Guide + Overview, nicht Rules
- **Ansatz:** wie agent-guide: Katalog-Resource plus Instructions-Zeile. Query-Beispiel mit kodiertem Absolutpfad.
