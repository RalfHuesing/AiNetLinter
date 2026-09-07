# 08 – Keep & Defer

## Ziel

Funktionierende Pfade schützen und bewusst keine Reparaturarbeit aus Befunden erzeugen, deren Risiko oder Nutzen die Kernlieferung nicht rechtfertigt.

## Keep-Baseline

- `report_observability_feedback` Happy Path und Nicht-Zielbindung;
- kompakte `resource-rules`-Zusammenfassung bei bekanntem Query;
- `get_symbol_body` mit eindeutiger ID und begrenztem Fenster;
- gezielter C#-Scope bei `get_violations`;
- kompakter Projekt-Drilldown von `metrics_tree`;
- read-only, metadata-/decompilation-basierte Assembly-Analyse.

## Defer/Document

`get_hotspots`, `metrics_tree`, reine kosmetische Formatterabweichungen, Komfort-Wishes ohne belegten Agentenschaden und Verbesserungen, die erst durch die gemeinsame Paging-/ID-Semantik sinnvoll werden.

## Abnahme

Jeder zurückgestellte Befund besitzt eine Begründung und einen Regressionstest oder eine dokumentierte Nicht-Zusage. Es entstehen keine künstlichen Produktpakete ohne konkreten Nutzerwert.
