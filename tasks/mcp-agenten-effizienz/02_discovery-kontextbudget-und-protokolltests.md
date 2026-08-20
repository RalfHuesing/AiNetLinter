---
status: ready
type: konzept
project_kind: brownfield
estimated_scope: medium
priority: P0
agent_role: .agents/Agent-Scaffolding/dev-loop/planning/orchestrator.md
rules_dir: .agents/rules
last_updated: 2026-08-20
open_questions: []
---

# Discovery-Kontext budgetieren und beide MCP-Protokollpfade testen

## Ziel

Die globalen Server-Instructions werden auf wirklich globale Regeln reduziert. Tool-spezifische Informationen bleiben in `tools/list`, die vollständige Kurzliste in `ainetlinter://overview`. Gleichzeitig werden Legacy-MCP und MCP 2026-07-28 auf dem echten stdio-Wire getestet und ihre Payloadgrößen reproduzierbar gemessen.

## Warum / Kontext

Der aktuelle `ServerInstructions.Text` zählt 6.380 Zeichen bzw. 6.393 UTF-8-Bytes und listet alle 26 Tools einzeln auf. Dieselben Namen und Zwecke stehen bereits in `tools/list` und der Overview-Resource.

Reproduzierte Wire-Baseline vom 2026-08-20:

- Legacy `initialize`: 6.380 Instructions-Zeichen.
- Modern `server/discover`: 6.920 UTF-8-Bytes Gesamtantwort, ebenfalls 6.380 Instructions-Zeichen.
- `tools/list`: 20.836 UTF-8-Bytes, 11.850 Tool-Description-Zeichen, 6.711 Input-Schema-Zeichen.

Der aktuelle Codex-Host exponiert den 6.382-Zeichen-Präfix zusätzlich an jeder Toolbeschreibung. Das ist hostspezifisch, aber real reproduziert. Die serverseitige Kürzung spart garantiert Wire-Bytes im Discovery-Pfad und spart in Hosts mit dieser Exposition zusätzlich wiederholten Modellkontext.

MCP 2026-07-28 hat `initialize`/`initialized` durch `server/discover` und Request-Metadaten ersetzt. Der eingebundene C#-SDK-Build 2.2.0 unterstützt diesen Pfad bereits; der aktuelle Server antwortet erfolgreich. Quelle: [MCP 2026-07-28 Release](https://blog.modelcontextprotocol.io/posts/2026-07-28/) und lokale SDK-Dokumentation `ModelContextProtocol.Core.xml` zu `DiscoverRequestParams`/`DiscoverResult`.

## Scope

### Must-have

- `ServerInstructions.Text` darf keine vollständige Tool-Aufzählung mehr enthalten.
- Erhalten bleiben, kompakt formuliert:
  - C#-Symbolgraph-Grenze und `search_pattern`-Fallback,
  - Hinweis auf `tools/list` und `ainetlinter://overview`,
  - Sufficiency-/Truncation-Regel,
  - kurze `isError`-Policy,
  - höchstens drei kompakte Startworkflows.
- UTF-8-Größe von `ServerInstructions.Text` gegenüber der Baseline um mindestens 60 % reduzieren; damit gilt ein Maximalbudget von 2.557 Bytes.
- Raw-Wire-Integrationstest für Legacy-`initialize` beibehalten.
- Raw-Wire-Integrationstest für `server/discover` mit Protokollversion `2026-07-28` ergänzen.
- Für beide Pfade prüfen, dass Instructions vorhanden, inhaltlich gleich und innerhalb des Budgets sind.
- Raw-Wire-`tools/list` für beide Protokollpfade prüfen: 26 ist nicht hartzucodieren; stattdessen gegen die registrierte Collection bzw. eindeutige Namen vergleichen.
- Einen wiederverwendbaren Payload-Messhelper für Zeichen und UTF-8-Bytes anlegen.
- `Docs/agent-api.md` und `Docs/integration.md` müssen beide Protokollgenerationen beschreiben.

### Nice-to-have

- Testausgabe bei Budgetüberschreitung soll Ist-Bytes, Budget und größten Abschnitt nennen.
- Die Wire-Messung kann zusätzlich Summe der Tool-Descriptions, Summe der Input-Schemas und Zahl der Output-Schemas ermitteln, aber nur Instructions erhalten zunächst ein hartes Budget.

### Non-Goals

- Kein modellabhängiger Tokenizer und keine Behauptung einer exakten Tokenersparnis.
- Keine Kürzung einzelner Tool-Descriptions allein nach Zeichenanzahl. Parametergrenzen und Auswahlhinweise dürfen nicht verloren gehen.
- Kein Entfernen der Overview-Resource.
- Kein Tool-Profil in diesem Task.
- Keine Änderung des MCP-SDK-Pakets.

## Zielinhalt der Server-Instructions

Die konkrete Formulierung darf variieren, muss aber semantisch ungefähr folgendes enthalten:

```text
AiNetLinter analysiert die resident geladene .NET-Solution mit Roslyn.
C#-Symbole über die semantischen Tools abfragen; für Text/Namen außerhalb von .cs search_pattern verwenden.
Schemas und Toolzwecke: tools/list. Kompakter Status und Workflows: ainetlinter://overview.
Vollständige Ergebnisse nicht redundant per Read/Grep prüfen; bei truncated Limits/Scope verfeinern.
isError=true ist für nicht geladene Solution, Sicherheitsverweigerung oder Malfunction reserviert.
Start: Edits get_feature_context -> get_symbol_body; Impact find_symbol -> find_references/get_impact; Gate safeguard -> get_violations.
```

Die Instructions dürfen keine Zeilenserie `- <toolname>:` enthalten.

## Änderungen an bestehenden Tests

In `src/AiNetLinter.FastTests/Mcp/McpServerOptionsFactoryTests.cs`:

- `Create_ServerInstructionsContainsAllRegisteredTools` entfernen oder durch `Create_ServerInstructionsStaysWithinUtf8Budget` ersetzen.
- `ServerInstructions_MatchesOverviewResourceTools` darf nicht länger Instructions gegen alle Namen prüfen; stattdessen Registration und Overview direkt als Mengen vergleichen.
- Kernhinweise `.cs`, `search_pattern`, Sufficiency und Error-Policy weiter prüfen.

In `src/AiNetLinter.IntegrationTests/Mcp/McpServerCommandJsonRpcFramingTests.cs`:

- Legacy-Test weiter mit einer initialize-fähigen Version ausführen.
- Modernen Test mit `server/discover` und `_meta.io.modelcontextprotocol/protocolVersion = 2026-07-28` ergänzen.
- Anschließend einen modernen `tools/list`-Request mit derselben Request-Meta senden.
- Nicht nur JSON-Gültigkeit, sondern `supportedVersions`, Instructions und Toolnamens-Eindeutigkeit prüfen.

## Mess- und Qualitätsregeln

- Bytezahl immer mit `Encoding.UTF8.GetByteCount` bestimmen.
- JSON kompakt serialisieren; Whitespace darf keinen Budgettest beeinflussen.
- Baselinewerte in der Dokumentation als datierte Messung führen, nicht als dauerhafte Sollwerte.
- Der 60-%-Schwellwert ist ein explizites Engineering-Budget, keine wissenschaftliche Naturkonstante.
- Deterministische Reihenfolge der Toolliste beibehalten; die aktuelle MCP-Spezifikation empfiehlt dies für reproduzierbares Caching und Prompt-Cache-Treffer.

## Definition of Done

- Instructions höchstens 2.557 UTF-8-Bytes und ohne Tool-Vollständigkeitsliste.
- Legacy- und Modern-Wire-Tests sind grün.
- Overview bleibt die vollständige Kurzliste; `tools/list` bleibt die Schemaquelle.
- Dokumentation unterscheidet `initialize` und `server/discover` korrekt.
- Keine exakte Tokenersparnis wird behauptet.
- `dotnet build` sowie beide Nicht-Stress-Testprojekte sind grün.

