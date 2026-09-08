---
status: ready
type: konzept
project_kind: brownfield
estimated_scope: large
rules_dir: .cursor/rules
last_updated: 2026-09-07
open_questions: []
depends_on: []
related_tasks: []
supersedes: null
---

# Konzept: AiNetLinter-MCP Nutzungsprüfung aus LLM-Agenten-Sicht

## Ziel (Was)

Herausfinden, **was an der AiNetLinter-MCP-Oberfläche aus Sicht eines Coding-Agenten schlecht oder gar nicht funktioniert** — damit das anschließend im AiNetLinter-Repo behoben werden kann.

Dieser Task **behebt nichts**. Er erzeugt reproduzierbare Finding-Dateien (Live-Calls, Verdict, Schwere, Roslyn-konforme Umsetzungsansätze). Die eigentliche Reparatur ist ein **Folgetask im AiNetLinter-Repo**, gespeist aus diesen Dateien plus einem späteren Audit (Synthese).

Prüffragen je Funktion und je Kombination:

- Kann ein Agent das Tool anhand Schema/Beschreibung **richtig aufrufen**, ohne zu raten?
- Liefert die Antwort **korrekte** Fakten gegenüber Platform- bzw. Assembly-Ist?
- Ist die Antwort **klein genug** und mit **stabilen IDs/Hints** für den nächsten Call brauchbar?
- Wo lügt die Beschreibung, wo sind False Positives, wo hängt/timeoutet es, wo ist das Ergebnis formal ok aber als Agent unbrauchbar?

## Warum / Kontext

Platform-Agenten sollen C#-Semantik MCP-first lösen (`.cursor/rules/AiNetLinter-McpWorkflow.mdc`). Unit-/E2E-Tests im AiNetLinter-Repo sehen nicht, was im Agenten-Turn scheitert: Token-Flut, Truncation ohne Folgeschritt, kaputte Symbol-IDs, irreführende Schemas, `isError` vs. Leermenge, Latenz.

Technischer Rahmen (nicht verhandelbar):

- Analyse ist **statisch/statistisch mit Roslyn**. Kein RAG, kein LLM hinter den Tools.
- Wünsche und Phase-3-Vorschläge nur in diesem Rahmen.
- Keine Features, die „Verstehen“ jenseits von Roslyn brauchen.

## Scope

### Muss-Haben

- **Inventar vollständig:** alle **33 nativen MCP-Tools** plus **3 Resources**. `mcp_auth` (Cursor-Wrapper) bleibt draußen.
- **Agentensicht, keine Unit-Test-Attitüde:** realistische Platform-Fragen (Typ finden, Body lesen, Aufrufer, Impact, JS/Razor-Fallback, externe API in DLL). Zusätzlich Fehler-/Leer-/Truncation-Fall. Nicht nur „Call kommt 200 zurück“.
- **Einzelprüfung:** ein Subagent pro Funktion, **Live-Calls**. 360° je Funktion: Happy Path, Fehler/Leer, Truncation/Token-Stress wo Limits existieren; optionale Agenten-Parameter wo das Schema sie hat (`maxResults`, `cursor`/`continuationToken`, `includeReferences`, `detailLevel`, Batch-IDs). Cross-Target-Tools: Platform **und** mindestens eine externe Assembly.
- **Eine Finding-Datei pro Funktion** unter `tasks/ainetlinter-mcp-usage-audit/findings/`.
- **Kombinationsprüfung:** eigene `findings/combo-*.md`. Kombinationen sind für das Ziel oft wichtiger als Einzelcalls (ID-Übergabe, doppelte Token-Last, Beschreibung vs. Folge-Tool).
- **Phase 3 in diesem Task:** nach Welle 1+2 bestehende Dateien um AiNetLinter-Quellzeiger und konkreten Umsetzungsansatz ergänzen — für jeden Befund mit Schwere `broken`, `degraded`, `friction` oder `wish`.
- **Finding-Schema** einschließlich **Schwere** (siehe unten).
- **Parallelität:** Subagenten gleichzeitig; eine Datei pro Agent.
- **Kein Produktionscode**, kein Build, kein Testlauf, keine Finding-Synthese.

### Non-Goals

- Kein Audit der AiNetLinter-Codequalität oder Testdeckung; kein Patch in AiNetLinter oder Platform.
- Kein Build, keine Standard-Suite, kein Audit-Skill, kein `get_violations`-Abschlussgate.
- Keine Zusammenführung / Priorisierung zu einem Gesamtbericht (Folge-Audit).
- Keine Wünsche außerhalb statischer Roslyn-Analyse.
- `mcp_auth` nicht prüfen; keine neue Projektintegration.
- `reload_config` darf `ainetlinter-rules.json` nicht dauerhaft verändern.
- Standard-Prompt `.cursor/prompts/orchestrator.md` ist **nicht** das Ausführungsmodell.

## Schicht und Domain-Platzierung

Keine Platform-/Domain-/Contracts-Änderung. Artefakte nur unter `tasks/ainetlinter-mcp-usage-audit/` (Ordner `findings/`). AiNetLinter-Repo und externe Assemblies **read-only**. Reparatur später im AiNetLinter-Repo, nicht hier.

## Wo im Projekt

| Ort | Warum relevant |
| --- | --- |
| Workspace-Root `C:\Workspace\Sample.Project` | `targetType=project`. |
| `.cursor/rules/AiNetLinter-McpWorkflow.mdc` | Soll-Nutzung; Reibung messen, nicht als Server-Spec behandeln. |
| `ainetlinter.project.json` | Integriertes Projekt; Health/Overview. |
| externe Assemblies unter `C:\ExternalAssemblies\Version-9\` | `Example.External.Process.dll`, `Example.External.Business.dll`, `Example.External.Core.dll`. |
| `C:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\Mcp\` | Phase 3: Registration, Handler, Truncation, Formatter. |
| `C:\Daten\Tools\AiNetLinter-win-x64\AiNetLinter.exe` | Laufende Binary; Versionsbezug, kein Assembly-Probeziel. |

## Funktionsinventar (verbindlich)

**33 Tools** → `findings/<toolname>.md`:

- Symbolgraph: `find_symbol`, `find_references`, `get_call_tree`, `get_impact`, `get_type_hierarchy`, `dependency_graph`, `resolve_type_origin`, `find_implementations`
- Datei/Struktur: `get_file_tree`, `get_namespace_tree`, `get_class_structure`, `get_file_skeleton`, `get_index_scope`, `get_hotspots`
- Body/Kontext: `get_symbol_body`, `get_feature_context`, `get_test_context`
- Analyse: `get_violations`, `safeguard`, `search_pattern`, `metrics_tree`, `metrics_lookup`, `pattern_detect`, `find_magic_values`, `find_dead_code`, `find_duplicates`
- Assembly-only: `search_assembly`, `inspect_assembly`, `find_assembly_extensions`, `get_assembly_context`
- Wartung: `reload_config`, `get_server_health`, `report_observability_feedback`

**3 Resources** → `findings/resource-agent-guide.md`, `findings/resource-overview.md`, `findings/resource-rules.md`.

Cross-Target (u. a. `dependency_graph`, `find_references`, `find_symbol`, `get_call_tree`, `get_class_structure`, `get_file_skeleton`, `get_impact`, `get_namespace_tree`, `get_symbol_body`, `get_type_hierarchy`, `metrics_lookup`, `metrics_tree`, `get_server_health`): Platform **und** mindestens eine externe Assembly, soweit `targetType=assembly` im Schema steht.

Zusätzlich in Welle 1 (eigene Datei, weil Agenten daran scheitern bevor ein Tool-Call gelingt): `findings/tool-discovery.md` — Schema-Entdeckung über `GetDynamicTools` / `tools/list`, Namespace-Beschreibung vs. Einzelschema, Truncation der Tool-Liste, Pflicht `targetType`/`targetPath`. Das ist die 37. Einzeldatei.

## Finding-Schema (verbindlich)

Jede Finding-Datei enthält:

1. Funktion und Schema-Kurzfazit (inkl. ob die Beschreibung den Call korrekt steuert oder in die Irre führt).
2. Ausgeführte Calls: Parameter, grobe Antwortgröße (Zeichen/Zeilen), Truncation/`completeness`, grobe Wartezeit (schnell / langsam / Timeout).
3. **Verdict:** wirkt wie beschrieben / teilweise / abweichend.
4. **Schwere** (für spätere Reparatur, kein Gesamt-Ranking):
   - `broken` — falsch, Absturz, dauerhaft unbrauchbar, Agent kommt nicht zum Ziel
   - `degraded` — formal ok, aber Token, Truncation, IDs, Latenz oder False Positives machen es riskant
   - `friction` — Schema, Pflichtfelder, Aliase, Fehlertexte, Beschreibung
   - `wish` — Roslyn-möglich, fehlt, Agent müsste es haben
   - `ok` — wiederverwendbar, kein Handlungsbedarf
5. **Nutzbarkeit:** Würde ich als Agent dieses Tool wieder aufrufen? ja / nein / nur mit Workaround.
6. Bugs (reproduzierbar, mit Call). False Positives / False Negatives vs. Ist (kleine Stichprobe: MCP-Ergebnis gegen Datei/`rg`/bekanntes Symbol, kein Vollabgleich).
7. Token-Effizienz; Folge-Call-Tauglichkeit (stabile IDs, Hints, StructuredContent).
8. Roslyn-konforme Wünsche.
9. Phase-3-Abschnitt: AiNetLinter-Pfad/Symbol + Ansatz (Welle 1/2 Platzhalter; Welle 3 Pflicht außer bei reinem `ok`).

Keine Datei fasst andere Findings zusammen. Ground Truth ist Platform-Quelltext bzw. Assembly-Metadaten, nicht AiNetLinter-Unit-Tests.

## Probe-Anker

- **Projekt:** `targetType=project`, `targetPath=C:\Workspace\Sample.Project`
- **C#:** ein eindeutig treffbarer Produktionstyp (in der Finding-Datei nennen; geeignet: `DataExecutor` oder ein `[ComponentRegistration]`).
- **Nicht-C#:** `wwwroot/js` und mindestens eine `.razor`-Datei.
- **Assembly (alle drei über die Welle verteilt):**
  - `C:\ExternalAssemblies\Version-9\Example.External.Process.dll`
  - `C:\ExternalAssemblies\Version-9\Example.External.Business.dll`
  - `C:\ExternalAssemblies\Version-9\Example.External.Core.dll`
- `report_observability_feedback`: ein als Probe markierter Call oder Fehlerpfad, kein Spam.
- `reload_config`: ohne dauerhafte `ainetlinter-rules.json`-Änderung.

## Wie (grober Ansatz)

Drei Wellen. Parent orchestriert. Findings nicht mergen.

1. **Einzeltools/Resources + tool-discovery:** ein Subagent pro Datei, parallel. Zuerst Schema der **eigenen** Funktion, dann Live-Calls, dann Finding-Datei. Subagent-Brief unten.
2. **Kombinationen** (Mindestset; Parent darf weitere `combo-*.md` ergänzen, ohne Einzelbefunde zu recyceln):
   - `combo-mcp-first-context.md` — `find_symbol` → `get_feature_context` → `get_symbol_body` → `get_call_tree`
   - `combo-discovery-fallback.md` — `get_file_tree` → `get_index_scope` → `search_pattern`
   - `combo-impact-lint.md` — `get_impact` → `get_violations` → `metrics_lookup`
   - `combo-type-nav.md` — `get_namespace_tree` → `get_class_structure` → `get_type_hierarchy` → `find_implementations`
   - `combo-assembly.md` — `inspect_assembly` → `search_assembly` → `find_assembly_extensions` → `get_assembly_context`
   - `combo-cross-target.md` — dasselbe Thema `project` vs. `assembly`
   - `combo-resources-health.md` — `get_server_health` + die drei Resources
   - `combo-batch-ids.md` — Batch (`namePatterns` / mehrere `symbolIdentifiers`) und ob zurückgegebene IDs im Folgetool funktionieren
3. **Phase 3:** bestehende Dateien um Codezeiger und Umsetzungsansatz ergänzen (`C:\Daten\Entwicklung\Ralf\AiNetLinter`, read-only). Keine Synthese-Datei.

MCP-Server ist eine gemeinsame Session: paralleler Start ist gewollt; bei Timeouts/Leerantworten in Batches von etwa 6–8 wiederholen, Scope nicht kürzen.

### Subagent-Brief (Parent kopiert das in jeden Welle-1-Auftrag)

Du bist ein Coding-Agent in Sample.Project. Du darfst **nur** die dir zugewiesene MCP-Funktion (plus Schema-Lookup dafür) live nutzen. Ziel ist nicht „Test bestanden“, sondern: Kann ich damit echte Arbeit tun, und wo behindert mich AiNetLinter? Schreibe ausschließlich `tasks/ainetlinter-mcp-usage-audit/findings/<name>.md` nach Finding-Schema im Konzept. Kein anderer Code, kein Build, kein Test, keine Zusammenfassung anderer Tools.

## Ausführung (verbindlich für den umsetzenden Chat)

Nicht `.cursor/prompts/orchestrator.md`. Der Prompt verbietet parallele Subagenten, erzwingt Implement/Review/Audit und die Standard-Suite.

**Modell:** frischer Chat = Probe-Orchestrator. Liest dieses Konzept. Startet Subagenten. Prüft Dateiexistenz. Commit nur bei ausdrücklichem Nutzerwunsch.

Der Parent:

- keine `roadmap.md` / `execution-log.md` / `tech-debt.md` / `code-map.md`
- keine Skills `implement`, `review`, `audit`
- kein Build, keine Tests, keine Finding-Synthese
- Welle 2 erst bei vollständiger Welle 1 (37 Dateien: 33 Tools + 3 Resources + `tool-discovery`); Welle 3 nach Welle 2
- schreibt nur unter `tasks/ainetlinter-mcp-usage-audit/findings/`

Umsetzungspakete: Welle 1 → Welle 2 → Welle 3. Nachweis = Dateien unter `findings/`.

### Starter-Prompt für den frischen Chat

```text
@tasks/ainetlinter-mcp-usage-audit/Konzept.md

Du bist Probe-Orchestrator für diesen Task.
Lies das Konzept vollständig. Es hat Vorrang vor
.cursor/prompts/orchestrator.md und vor implement/review/audit.
Den Standard-Orchestrator nicht anwenden.

Ziel: Aus Agentensicht herausfinden, was an AiNetLinter-MCP
schlecht oder gar nicht funktioniert. Findings schreiben.
Kein Patch, kein Build, kein Test, keine Synthese.

Welle 1: 37 Subagenten parallel (33 Tools, 3 Resources,
findings/tool-discovery.md). Jeder schreibt nur seine Datei.
Nutze den Subagent-Brief aus dem Konzept.
Welle 2: combo-*.md laut Konzept, parallel.
Welle 3: bestehende Dateien um AiNetLinter-Quellzeiger und
Umsetzungsansätze ergänzen
(C:\Daten\Entwicklung\Ralf\AiNetLinter, read-only).

Inventar, Targets, externe Assemblies, Finding-Schema und Non-Goals
stehen im Konzept. Am Ende nur: welche Dateien fehlen noch,
nicht den Inhalt zusammenfassen.
```

## Verifikation und Dokumentation

| Gate | Geltung | Erwartetes Ergebnis |
| --- | --- | --- |
| Live-MCP-Call je Inventar-Funktion + tool-discovery | Welle 1 | 37 Dateien; Happy Path + Fehler/Leer/Truncation; Cross-Target wo vorgesehen; Schwere gesetzt |
| Kombinationsläufe | Welle 2 | alle `combo-*.md` des Mindestsets (8) |
| Phase 3 | nach Welle 2 | Codezeiger + Ansatz bei jeder Nicht-`ok`-Datei |
| Platform-Build / Standard-Suite / Audit-Skill | **nicht** | — |
| Gesamt-Synthese / AiNetLinter-Patch | **nicht** | Folge-Audit bzw. Folge-Task im AiNetLinter-Repo |

Dokumentation: Finding-Dateien + dieses Konzept. Keine `Docs/`- oder Rule-Änderung.

## Offene Punkte

Keine Start-Blocker. Konkreter C#-Ankertyp, Extra-Combos und Batch-Größe bei MCP-Überlast entscheidet der Parent.

## Annahmen

- Cursor ruft AiNetLinter über den Namespace `user-AiNetLinter` auf (`GetDynamicTools` / `CallDynamicTool`); das ist die reale Agenten-Oberfläche.
- Die drei externe Assemblies sind lesbar und werden nicht ausgeführt.
- Dieser Lauf liefert die Evidenz; Beheben passiert später in AiNetLinter, nicht in Platform.
