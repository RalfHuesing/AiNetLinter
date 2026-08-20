---
status: ready
type: konzept
project_kind: brownfield
estimated_scope: small
priority: P0
agent_role: .agents/Agent-Scaffolding/dev-loop/planning/orchestrator.md
rules_dir: .agents/rules
last_updated: 2026-08-20
open_questions: []
---

# Dokumentations- und Begriffsdrift beseitigen

## Ziel

Alle öffentlichen Beschreibungen des MCP-Servers müssen denselben, überprüfbaren Vertrag wiedergeben. Statische Test-Zuordnung darf nicht länger als Code-Coverage bezeichnet werden. Hartcodierte Toolanzahlen werden dort entfernt, wo sie keinen funktionalen Wert haben.

## Warum / Kontext

Der aktuelle Server registriert 26 Tools. Gleichzeitig stehen in versionierten Quellen:

- `README.md`: 25 Tools,
- `Docs/integration.md`: 23 Tools,
- `Docs/agent-api.md`: einleitend 20, später 26 und bei der Overview-Resource erneut 20,
- `Tasks/features/00-uebersicht.md`: 25 Tools.

`TestCoverageScanner` führt keine instrumentierten Tests aus und liest keine Coverage-Dateien. Die Implementierung in `src/AiNetLinter/Core/TestCoverageScanner.cs` ordnet Tests statisch über vier Evidenzarten zu: direkte Invocation, Namenskonvention, `@covers` und `typeof`/`nameof`. Der Begriff „Test-Abdeckung“ suggeriert daher eine nicht vorhandene Laufzeitmessung.

## Scope

### Must-have

- Toolanzahlen in Fließtext und Überschriften aus `README.md`, `Docs/integration.md`, `Docs/agent-api.md` und `Tasks/features/00-uebersicht.md` entfernen.
- Die vollständige Tabelle in `Docs/agent-api.md` als „Tool-Referenz“ statt „Die 26 Tools“ überschreiben.
- Die Overview-Resource darf ihre Anzahl weiterhin dynamisch aus `ToolSummaries.Count` ausgeben.
- Öffentliche Texte von „Test-Coverage“, „Test-Abdeckung“ und „Test-Coverage-Awareness“ auf „statische Test-Zuordnung“, „Testbezug“ oder „Test-Kontext“ umstellen.
- In `Docs/agent-api.md` einmal explizit dokumentieren: keine instrumentierte Laufzeit-Coverage, keine Aussage darüber, ob ein Test den Zielpfad tatsächlich ausführt oder Assertions enthält.
- Tool-Descriptions in `AnalysisToolRegistrations.cs`, `ServerInstructions.cs`, `OverviewResourceRegistration.cs` und Formatter-Überschriften entsprechend korrigieren.
- Betroffene Tests auf die neue Terminologie anpassen.
- Registrierungs-/Overview-Parität über Mengenvergleich testen; keine neue hartcodierte Zahl einführen.

### Nice-to-have

- `TestCoverageMatchReasons` in der Dokumentation als Evidenzarten auflisten.
- Bei null Treffern „keine Tests statisch zugeordnet“ statt „ungetestet“ ausgeben. Das Feld `IsUntested` darf intern aus Kompatibilitätsgründen bestehen bleiben, soll aber öffentlich nicht als bewiesene Testlosigkeit formuliert werden.

### Non-Goals

- Keine Umbenennung des öffentlichen C#-Typs `TestCoverageScanner` in diesem Task; das wäre unnötiger API-Churn.
- Keine dynamische Coverage-Erfassung, kein Coverlet-Aufruf und kein Lesen von `.coverage`/Cobertura-Dateien.
- Keine Änderung der Toolnamen `get_test_context` oder `get_feature_context`.
- Keine Funktionserweiterung der Zuordnungsheuristik.

## Technischer Rahmen

Betroffene Hauptstellen:

- `README.md`
- `Docs/agent-api.md`
- `Docs/integration.md`
- `Docs/ROADMAP.md` nur dort, wo aktive Produktbeschreibung statt historischem Eintrag vorliegt
- `Tasks/features/00-uebersicht.md`
- `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs`
- `src/AiNetLinter/Mcp/ServerInstructions.cs`
- `src/AiNetLinter/Mcp/OverviewResourceRegistration.cs`
- `src/AiNetLinter/Mcp/Tools/TestContext/TestContextFormatter.cs`
- `src/AiNetLinter/Mcp/Tools/FeatureContext/FeatureContextFormatter.cs`
- zugehörige FastTests

Historische Roadmap-Einträge dürfen ihre damalige Zahl behalten, wenn Datum und historischer Zustand eindeutig sind. Aktive Referenzabschnitte dürfen keine veraltete Zahl enthalten.

## Umsetzungsschritte

1. Mit `rg` alle Kombinationen aus Zahl + `Tool`, `Test-Coverage`, `Test-Abdeckung`, `Coverage-Awareness` und „ungetestet“ inventarisieren.
2. Jede Fundstelle klassifizieren: aktiver Vertrag, historischer Eintrag, interner Typname oder Testfixture.
3. Nur aktive Verträge und Nutzertexte ändern. Interne Typnamen und historische Aussagen nicht mechanisch global ersetzen.
4. `McpServerOptionsFactoryTests` so ändern, dass registrierte Namen und `OverviewResourceRegistration.ToolSummaries` als eindeutige Mengen identisch sind. Die Anzahl ergibt sich aus den Collections; kein `Assert.Equal(26, ...)`.
5. Formatter-Tests auf neue Überschriften und die Einschränkung „statische Zuordnung“ aktualisieren.
6. Dokumentationslinks und Tabellenzeilen manuell prüfen.

## Akzeptanztests

- Ein Test weist nach, dass alle registrierten Toolnamen genau einmal in `ToolSummaries` vorkommen.
- Ein Test weist nach, dass `get_test_context` im Text „statische Test-Zuordnung“ enthält.
- Ein Test für den Nulltreffer-Fall enthält keine unbelegte Behauptung „Symbol ist ungetestet“.
- `rg -n "20 Tools|23 Tools|25 Tools|Die 26 Tools" README.md Docs Tasks/features` liefert für aktive Verträge keine Treffer.
- `rg -n "Test-Coverage|Test-Abdeckung|Coverage-Awareness" README.md Docs src/AiNetLinter/Mcp` liefert nur explizit historische/erklärende Fundstellen oder keine Treffer.

## Definition of Done

- Alle Must-haves umgesetzt.
- Keine öffentliche Behauptung verwechselt statische Zuordnung mit Runtime-Coverage.
- Toolnamen-Parität ist ohne hartcodierte Toolanzahl getestet.
- `dotnet build` sowie beide Nicht-Stress-Testprojekte sind grün.

