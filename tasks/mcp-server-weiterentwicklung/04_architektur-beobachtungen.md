---
status: beobachtung (ueberwiegend positiv)
priority: P3
last_updated: 2026-08-21
evidence: find_duplicates live ueber src/AiNetLinter/Mcp (517 Methoden), Dateigraessen-Inventar
---

# 04 — Architektur-Beobachtungen: Scanner-Muster, Hotspots, Boilerplate

## Positiv-Befund: ungewöhnlich niedrige Duplikationsdichte

Live-`find_duplicates` über `src/AiNetLinter/Mcp` (517 Methoden gescannt, Threshold "near"):
**genau 1 Cluster** — `DuplicateDetectionScanner.ScanAsync` vs.
`StructuralDuplicateScanner.ScanAsync` (Score 0,91, je 115 Tokens). Beide sind die
Modus-Einstiegspunkte zweier bewusst getrennter Erkennungsverfahren; die strukturelle
Ähnlichkeit ist by-design (gleicher Eingabevertrag), keine Drift. Kein Handlungsbedarf,
höchstens ein Kommentar-Verweis der beiden aufeinander, um künftige Drift sichtbar zu machen.

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

Das Muster ist nirgends als Template dokumentiert — jeder neue Agent (und jeder neue
Mensch) leitet es aus Nachbar-Tools ab. Empfehlung: eine kurze
`Docs/mcp-tool-template.md` (oder Sektion in agent-api.md) mit dem Quartett, den Pflicht-
Elementen (LoadState-Guard, Recoverable/Error-Policy-Verweis, StructuredContent-Wrap-Regel
aus `McpToolResults.Text<T>`) und einem Verweis auf `IsErrorPolicy.md`. Billig, senkt
Einstiegs-Reibung und verhindert, dass das 27. Tool das Muster erstmals bricht.

## Beobachtung 2: Größen-Hotspots nahe den Limits

Größte Dateien im Mcp-Baum (KB, Stand 2026-08-21):

| Datei | KB |
|---|---:|
| DependencyGraph/DependencyGraphScanner.cs | 22,8 |
| Safeguard/SafeguardScanner.cs | 22,1 |
| SymbolGraph/CallGraphTraversal.cs | 19,8 |
| Analysis/SearchPatternScanner.cs | 18,8 |
| FileStructure/GetNamespaceTreeScanner.cs | 17,6 |
| DeadCode/FindDeadCodeScanner.cs | 17,3 |
| MagicValues-Cluster (8 Dateien gesamt) | ~62 |

Die `MaxLineCount`-Regel erzwingt Aufteilung; auffällig ist, dass die Aufteilung bevorzugt
**horizontal** (Scanner + Records + Walker + Completeness als Geschwister) statt **funktional**
(Subsysteme) erfolgt. Risiko: Die Scanner-Klassen bleiben inhaltlich God-Classes mit vielen
privaten Helfern; der AI-Footprint pro Symbol bleibt hoch, auch wenn die Datei klein ist.

**Empfehlung:** keine sofortige Aktion, sondern Monitoring: `get_hotspots` +
`metrics_tree` (mode: complexity) quartalsweise über den Mcp-Baum laufen lassen und
`MaxCognitiveComplexity`-Violations in genau diesen Dateien priorisiert behandeln. Die
MagicValues-Zerlegung (Classifier/Walker/Heuristics getrennt) ist dabei das gelungene
Vorbild.

## Beobachtung 3: Result-Policy ist dokumentiert, aber nicht mechanisch erzwungen

`IsErrorPolicy.md` ist ein exzellentes Policy-Dokument — und rein textuell. Jeder
Tool-Handler wiederholt individuell: LoadState-Guard, Argument-Validierung →
`Recoverable(...)`, defensives try/catch → `CompilationError(...)`. Der EPIC-09-Wrapper
deckt Logging ab, nicht die Result-Policy.

**Risiko:** Die Policy-Datei dokumentiert einen Audit-Fund ("vor diesem Audit abweichend
von der Policy") — d. h. die Abweichung ist bereits einmal passiert und wurde manuell
gefunden. Beim 27./28. Tool passiert sie wieder, wenn kein Test sie fängt.

**Empfehlung (klein statt abstrakt):** kein Middleware-Framework bauen, sondern ein
statischer Konventionstest, der über alle registrierten Tools reflektiert:
- ruft jeder Handler bei `LoadState != Loaded` garantiert `SolutionNotLoaded()`?
- mappt jeder catch-Block auf `Error`/`CompilationError` (nie still geschluckt)?

Damit wird die Policy testbar erzwingbar, ohne neue Laufzeit-Abstraktion — konsistent mit
der No-DI-Entscheidung und dem Verzicht auf unbelegte Frameworks.

## Fazit

Keine akute Tech-Debt. Die drei Handlungsempfehlungen sind billig (Template-Doc,
Monitoring-Rhythmus, Konventionstest) und sichern die derzeit sehr gute Architektur gegen
Wachstum auf 30+ Tools.
