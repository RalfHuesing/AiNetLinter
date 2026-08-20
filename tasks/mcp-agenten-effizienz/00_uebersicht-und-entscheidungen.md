---
status: ready
type: konzept
project_kind: brownfield
estimated_scope: large
priority: P0
agent_role: .agents/Agent-Scaffolding/dev-loop/planning/orchestrator.md
rules_dir: .agents/rules
last_updated: 2026-08-20
open_questions: []
---

# MCP-Agenten-Effizienz: überprüfte Findings und Ausführungsreihenfolge

## Rolle und Arbeitsmodus

Dieses Konzept wurde nach `.agents/Agent-Scaffolding/dev-loop/planning/orchestrator.md` erstellt. Es ist der Einstiegspunkt für die darunter priorisierten Einzelaufgaben. Jede Einzelaufgabe ist eigenständig ausführbar; Abhängigkeiten müssen trotzdem in der hier festgelegten Reihenfolge eingehalten werden.

## Ziel

AiNetLinter soll als allgemeiner MCP-Server in beliebigen C#/.NET-Codebasen einem Coding-Agenten den kleinsten **hinreichenden** Kontext liefern. Optimiert werden:

1. übertragene und vom Host exponierte Kontextmenge,
2. Eindeutigkeit und maschinelle Auswertbarkeit der Antworten,
3. Anzahl notwendiger Folgeaufrufe,
4. faktische Korrektheit der Tool-Verträge.

Nicht auf dieses Repository optimieren. Fixtures und Akzeptanztests müssen neutrale, mehrprojektige C#-Solutions verwenden.

## Methodik und belastbare Ausgangsdaten

Die Neubewertung beruht auf vier getrennten Evidenzklassen:

- Quellcode und Tests des aktuellen Repositories,
- Live-Aufrufe des laufenden AiNetLinter-MCP-Servers,
- rohe stdio-JSON-RPC-Probes gegen den gebauten Server,
- dokumentierte Fähigkeiten des eingebundenen `ModelContextProtocol`-SDK 2.2.0 und der MCP-Spezifikation.

Am 2026-08-20 wurden gegen den aktuellen Debug-Build reproduziert:

| Messpunkt | Reproduzierter Wert | Aussagegrenze |
|---|---:|---|
| Registrierte Tools | 26 | Serverbestand, keine Qualitätsaussage |
| Legacy-`initialize.instructions` | 6.380 Zeichen / 6.393 UTF-8-Bytes | wird pro Verbindung übertragen |
| Modernes `server/discover` | 6.920 UTF-8-Bytes, davon dieselben 6.380 Instructions-Zeichen | MCP 2026-07-28 wird vom aktuellen Server bereits beantwortet |
| Legacy-`tools/list` | 20.836 UTF-8-Bytes | enthält 11.850 Description-Zeichen und 6.711 Zeichen Input-Schemas |
| Advertisierte Output-Schemas | 0 | obwohl mehrere Tools `structuredContent` liefern |
| Modernes `tools/list` | `ttlMs: 0`, `cacheScope: private` | Antwort ist für standardkonforme Clients sofort veraltet |
| Aktuelle Codex-Tool-Exposition | 184.297 Description-Zeichen | hostspezifisch; gemeinsamer Präfix von 6.382 Zeichen wird bei allen 26 Tools wiederholt |

Wichtig: Die 184.297 Zeichen sind **kein** MCP-Wire-Befund. Der Server sendet `ServerInstructions` bei Legacy-MCP einmal in `initialize` und bei MCP 2026-07-28 in `server/discover`. Der aktuelle Host fügt diesen Text zusätzlich jeder exponierten Toolbeschreibung voran. Deshalb darf keine allgemeine Aussage wie „der MCP-Server sendet 46k Tokens pro Aufruf“ verwendet werden. Tokenzahlen werden nicht geschätzt, weil sie vom Modell-Tokenizer abhängen; gemessen werden Zeichen und UTF-8-Bytes.

Die wissenschaftliche Begründung ist bewusst begrenzt: Liu et al. zeigen, dass Modelle relevante Information in langen Kontexten positionsabhängig und nicht robust nutzen. Das rechtfertigt, irrelevanten Kontext zu vermeiden; es beweist weder einen universellen optimalen Byte-Grenzwert noch, dass jede Kürzung die Coding-Qualität erhöht. Quelle: [Liu et al., „Lost in the Middle“, TACL 2024](https://direct.mit.edu/tacl/article/doi/10.1162/tacl_a_00638/119630/Lost-in-the-Middle-How-Language-Models-Use-Long).

## Bestätigte Defizite

1. **Dokumentationsdrift:** `README.md`, `Docs/integration.md`, `Docs/agent-api.md` und `Tasks/features/00-uebersicht.md` nennen widersprüchlich 20, 23, 25 und 26 Tools.
2. **Falscher Coverage-Begriff:** `get_test_context` und `get_feature_context` liefern statische Test-Zuordnungen über Namenskonvention, `@covers`, `typeof`/`nameof` und direkte Symbolaufrufe. Das ist keine instrumentierte Laufzeit-Code-Coverage.
3. **Zu große globale Instructions:** Die globale Tool-Aufzählung dupliziert Informationen aus `tools/list` und `ainetlinter://overview`.
4. **Nur Legacy-Protokolltests:** Der Produktivserver beantwortet bereits `server/discover` für MCP 2026-07-28, die vorhandenen Raw-Wire-Tests decken aber nur den Legacy-Handshake ab.
5. **Strukturverlust bei Transitiver Analyse:** `find_references` und symbolbasiertes `get_impact` liefern bei `depth > 1` absichtlich nur Text, weil `CallGraphTraversal` intern Strings sammelt.
6. **Diff-Kontext ist verteilt:** `get_impact` berechnet geänderte Symbole bereits intern, gibt im Git-Modus aber primär Call-Sites zurück. Tests und direkte Violations müssen anschließend symbolweise über andere Tools gesammelt werden.
7. **Keine nutzbaren Cache-Hinweise:** Die statische Toolliste wird im modernen Protokoll als sofort veraltet und privat ausgeliefert.

## Priorisierte Umsetzung

| Reihenfolge | Aufgabe | Status | Priorität | Abhängigkeit | Erwarteter, überprüfbarer Effekt |
|---:|---|---|---|---|---|
| 1 | [01_dokumentations-und-begriffsdrift-beseitigen.md](01_dokumentations-und-begriffsdrift-beseitigen.md) | **erledigt** | P0 | keine | Verträge und Begriffe werden faktisch korrekt; keine veralteten hartcodierten Toolzahlen |
| 2 | [02_discovery-kontextbudget-und-protokolltests.md](02_discovery-kontextbudget-und-protokolltests.md) | **erledigt** | P0 | Aufgabe 01 | mindestens 60 % weniger globale Instructions-Bytes; Legacy und MCP 2026-07-28 auf dem Wire abgesichert |
| 3 | [03_transitive-symbolgraph-ausgaben-strukturieren.md](03_transitive-symbolgraph-ausgaben-strukturieren.md) | offen | P1 | Aufgabe 02 | `depth > 1` bleibt maschinell auswertbar und kommuniziert Vollständigkeit explizit |
| 4 | [04_get-impact-zum-diff-kontext-erweitern.md](04_get-impact-zum-diff-kontext-erweitern.md) | offen | P2 | Aufgabe 03 | ein deterministischer Aufruf liefert für einen Diff Symbole, Impact, Test-Zuordnung und direkte Violations |
| 5 | [05_tool-annotations-korrekt-setzen.md](05_tool-annotations-korrekt-setzen.md) | offen | P2 | Aufgabe 02 | Hosts erhalten korrekte Side-Effect- und Trust-Metadaten statt SDK-Defaults |
| 6 | [06_tools-list-cachehinweise-setzen.md](06_tools-list-cachehinweise-setzen.md) | offen | P3 | Aufgabe 02 | standardkonforme Clients dürfen die statische Toolliste zwischenspeichern |
| 7 | [90_bewusst-nicht-umsetzen.md](90_bewusst-nicht-umsetzen.md) | Entscheidung | P9 | fortlaufend | verhindert Tool-Proliferation und unbelegte Optimierungen |

## Architekturentscheidungen

- Kein RAG, keine Embeddings, kein Semantic Kernel und kein zusätzlicher Vektorspeicher.
- Kein neues `get_change_context`: `get_impact` wird additiv erweitert, damit Toolwahl und Toolkatalog nicht weiter wachsen.
- Keine modellabhängigen Token-Schätzungen in Tests. UTF-8-Bytes und JSON-Zeichen sind reproduzierbare Proxies.
- Kein Ranking mit unbelegtem „Relevanz“-Score. Sortierungen müssen deterministisch und fachlich erklärbar sein.
- Keine Breaking Removal bestehender Tools ohne Nutzungsdaten und Deprecation-Pfad.
- Text bleibt für abwärtskompatible Clients erhalten; strukturierte Daten sind die maschinenlesbare Primärform.

## Definition of Done für die Initiative

- Alle sechs Umsetzungsaufgaben erfüllen ihre jeweilige Definition of Done.
- `README.md`, `Docs/integration.md`, `Docs/agent-api.md`, Overview-Resource und Toolregistrierung widersprechen einander nicht.
- Raw-Wire-Tests decken Legacy-`initialize` und modernes `server/discover`/`tools/list` ab.
- Kein neuer externer Retrieval-/AI-Stack wurde eingeführt.
- Die vollständigen Nicht-Stress-Testläufe und `dotnet build` sind grün.
