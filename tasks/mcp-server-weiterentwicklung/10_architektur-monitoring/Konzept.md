---
status: beobachtung (ueberwiegend positiv) + dauerhafter Monitoring-Auftrag
type: konzept
project_kind: brownfield
priority: P3
agent_role: .agents/Agent-Scaffolding/dev-loop/planning/orchestrator.md
rules_dir: .agents/rules
last_updated: 2026-08-21
open_questions: []
herkunft: Review-Finding 2026-08-21 (ox-alpha)
---

# Architektur-Monitoring: Scanner-Muster, Hotspots, Result-Policy-Test

## Positiv-Befund: ungewöhnlich niedrige Duplikationsdichte

Live-`find_duplicates` über `src/AiNetLinter/Mcp` (517 Methoden, Threshold "near",
2026-08-21): **genau 1 Cluster** — `DuplicateDetectionScanner.ScanAsync` vs.
`StructuralDuplicateScanner.ScanAsync` (Score 0,91, je 115 Tokens). Beide sind die
Modus-Einstiegspunkte zweier bewusst getrennter Erkennungsverfahren; die strukturelle
Ähnlichkeit ist by-design (gleicher Eingabevertrag). Kein Handlungsbedarf, höchstens ein
Kommentar-Verweis der beiden aufeinander, um künftige Drift sichtbar zu machen.

Für eine 26-Tool-Codebase ist das ein überdurchschnittlich sauberer Zustand — die
Richtlinien-Arbeit (AI-Footprint-Limits, Cognitive-Complexity-Grenzen) zahlt sichtbar ein.

## Beobachtung 1: Das Tool-Quartett als implizites Template

Jedes Tool folgt demselben Dateimuster:

```
*Registrations.cs  → SDK-Bindung + Description (Wann-nutzen-Form)
*Tool.cs           → Argument-Validierung, Orchestrierung
*Scanner.cs        → Roslyn-Walk, Fachlogik
*Models.cs/*Formatter.cs → Payload + Text-Serialisierung
```

Das Muster ist nirgends als Template dokumentiert. **Aktion:** kurze
`Docs/mcp-tool-template.md` (oder Sektion in agent-api.md) mit dem Quartett, den
Pflicht-Elementen (LoadState-Guard, Recoverable/Error-Policy-Verweis,
StructuredContent-Wrap-Regel aus `McpToolResults.Text<T>`) und Verweis auf
`IsErrorPolicy.md`. Billig, senkt Einstiegs-Reibung und verhindert, dass das 27. Tool das
Muster erstmals bricht.

## Beobachtung 2: Größen-Hotspots nahe den Limits

Größte Dateien im Mcp-Baum (Stand 2026-08-21): `DependencyGraphScanner` (22,8 KB),
`SafeguardScanner` (22,1 KB), `CallGraphTraversal` (19,8 KB), `SearchPatternScanner`
(18,8 KB), `GetNamespaceTreeScanner` (17,6 KB), `FindDeadCodeScanner` (17,3 KB),
MagicValues-Cluster (~62 KB über 8 Dateien).

Die `MaxLineCount`-Regel erzwingt Aufteilung; auffällig ist, dass die Aufteilung bevorzugt
**horizontal** (Scanner + Records + Walker als Geschwister) statt **funktional** (Subsysteme)
erfolgt. Risiko: Die Scanner-Klassen bleiben inhaltlich God-Classes; der AI-Footprint pro
Symbol bleibt hoch, auch wenn die Datei klein ist.

**Aktion:** keine sofortige Umstrukturierung, sondern quartalsweises Monitoring via
`get_hotspots` + `metrics_tree` (mode: complexity) über den Mcp-Baum;
`MaxCognitiveComplexity`-Violations in genau diesen Dateien priorisiert behandeln. Die
MagicValues-Zerlegung (Classifier/Walker/Heuristics getrennt) ist das gelungene Vorbild.

## Beobachtung 3: Result-Policy ist dokumentiert, aber nicht mechanisch erzwungen

`IsErrorPolicy.md` ist ein exzellentes Policy-Dokument — und rein textuell. Jeder
Tool-Handler wiederholt individuell: LoadState-Guard, Argument-Validierung →
`Recoverable(...)`, defensives try/catch → `CompilationError(...)`. Der EPIC-09-Wrapper
deckt Logging ab, nicht die Result-Policy.

**Risiko:** Die Policy-Datei dokumentiert selbst einen Audit-Fund ("vor diesem Audit
abweichend von der Policy") — die Abweichung ist bereits einmal passiert und wurde manuell
gefunden. Beim 27./28. Tool passiert sie wieder, wenn kein Test sie fängt.

**Aktion (klein statt abstrakt):** kein Middleware-Framework bauen, sondern ein statischer
Konventionstest, der über alle registrierten Tools reflektiert:
- ruft jeder Handler bei `LoadState != Loaded` garantiert `SolutionNotLoaded()`?
- mappt jeder catch-Block auf `Error`/`CompilationError` (nie still geschluckt)?

Damit wird die Policy testbar erzwingbar, ohne neue Laufzeit-Abstraktion — konsistent mit
der No-DI-Entscheidung.

## Zusammenfassung der Aktionen

| Aktion | Aufwand | Charakter |
|---|---|---|
| Tool-Template-Doku schreiben | klein | einmalig |
| Konventionstest für Result-Policy | mittel | einmalig |
| Hotspot-Monitoring (get_hotspots + metrics_tree) | minimal | quartalsweise wiederkehrend |
