# Agent-UX-Befunde: MCP-Server aus Agentensicht

Notizen aus einer direkten Agenten-Perspektiv-Beurteilung (September 2026).
Kein formaler Audit — direkte Beobachtungen was einen Agenten in der Praxis
behindert oder verlangsamt.

Diese Datei ist Ausgangspunkt für spätere Konzeptarbeit und mcp-ux-audit-Läufe.
Status: offene Sammlung, noch kein Konzept, noch kein Umsetzungsauftrag.

---

## Befund 1: Parameter-Naming-Inkonsistenz

**Problem:** Dasselbe Konzept „ein Symbol identifizieren" heißt je nach Tool anders:
- `symbolIdentifier` in `get_feature_context`, `find_references`, `get_call_tree`, `get_impact`
- `pattern` / `namePattern` / `namePatterns` in `find_symbol`
- `symbolId` (opake ID) in `get_symbol_body`

Die semantische Unterscheidung (Suche vs. exakte Adressierung vs. opake ID) ist
real und begründet — aber sie ist nicht aus den Parameternamen allein erkennbar.
Ein Agent der zum ersten Mal `find_symbol` und `find_references` kombiniert,
rät erst einmal.

**Wunsch:**
- Schema-Descriptions erklären den Unterschied explizit pro Tool
- Oder: einheitlichere Namensgebung wo semantisch gleich

---

## Befund 2: Zu viele Tools, unklare Übergänge

**Problem:** 30+ Tools mit überlappenden Zwecken. Die Auswahl ist nicht intuitiv:
- `get_class_structure` vs. `get_file_skeleton` vs. `get_feature_context` vs. `get_symbol_body`
- `search_pattern` vs. `find_symbol` vs. `get_namespace_tree`

Der `.agents/rules/AiNetLinter-McpWorkflow.mdc`-Guide ist sehr detailliert —
das ist ein Symptom: ohne ihn trifft ein Agent regelmäßig die falsche Wahl.

**Wunsch:**
- `tools/list`-Descriptions enthalten einen knappen „Wann statt X"-Hinweis
- Z.B. `get_class_structure`: „Für Member-Überblick. Für Implementierungsdetails `get_symbol_body` verwenden."
- Progressive Entry Points: ein „Starter-Tool" das weiteres Chaining vorschlägt

---

## Befund 3: `targetPath`-Overhead bei langen Sessions

**Problem:** Jeder einzelne MCP-Call braucht den absoluten `targetPath`. Bei
einer Analyse-Session mit 20 aufeinanderfolgenden Calls auf dieselbe Solution
ist das redundant und erzeugt Lärm im Prompt/Kontext.

**Wunsch:**
- Eine Session-Semantik oder ein `set_target`-Mechanismus der den Pfad einmalig setzt
- Alternativ: explizit dokumentieren dass das so gewollt ist und warum (Isolation,
  Multi-Target-Support) — dann ist es kein Befund sondern bewusste Entscheidung

> Hinweis: Das ist architektonisch begründet (jeder Call ist idempotent, kein
> impliziter State). Ob der Schmerz groß genug für eine Änderung ist, muss
> abgewogen werden.

---

## Befund 4: Assembly-Modus-Einschränkungen nicht offensichtlich

**Problem:** Im Assembly-Modus (`.dll`-Target) gibt `find_references` ohne
`includeReferences=true` still wenige oder keine Ergebnisse zurück. Ein Agent
der das nicht weiß zieht die falsche Schlussfolgerung: „keine Referenzen vorhanden"
statt „Referenzsuche im Assembly-Modus eingeschränkt".

Ähnlich: `get_violations` im Assembly-Modus gibt `lint=unsupported` — aber
das steht nur in `get_server_health`, nicht in der Tool-Antwort selbst.

**Wunsch:**
- Assembly-Modus-Einschränkungen proaktiv im Response signalisieren, nicht nur
  wenn der Agent explizit fragt
- Z.B. bei `find_references` auf Assembly-Target ohne `includeReferences`:
  Response enthält Hinweis „Im Assembly-Modus: `includeReferences=true` für
  erweiterte Referenzsuche verwenden"

---

## Befund 5: Kein „Ich weiß nicht was ich will"-Einstiegspunkt

**Problem:** Ein Agent der frisch auf ein unbekanntes C#-Projekt trifft, weiß
nicht wo er anfangen soll. Es gibt keinen natürlichen Einstiegspunkt der sagt
„Fang hier an". `get_server_health` gibt Capabilities aber keine Orientierung.
`get_file_tree(view=summary)` ist gut aber nicht offensichtlich als erster Call.

**Wunsch:**
- Ein `get_overview`- oder erweitertes `get_server_health`-Response das einem
  frischen Agenten sagt: „Das Projekt hat X Namespaces, Y Tests, Z Violations,
  empfohlener Einstieg: `get_namespace_tree` → `find_symbol`"
- Oder: `get_server_health` mit `includeGettingStarted=true`

---

## Querschnittsthema: Implizites Wissen

Mehrere der obigen Befunde haben dieselbe Wurzel: Der Server funktioniert
korrekt, aber ein Agent braucht Vorwissen (aus dem MCP-Workflow-Guide oder aus
Erfahrung) um ihn optimal zu nutzen. Dieses Vorwissen sollte idealerweise
aus dem Server selbst abrufbar sein — durch gute Descriptions, proaktive
Hinweise in Responses und einen guten Einstiegspunkt.

---

## Nicht-Befunde (bewusste Design-Entscheidungen die OK sind)

- `targetPath` als Pflichtfeld pro Call: architektonisch begründet, akzeptabel
- `pattern` vs. `symbolIdentifier` Unterschied: semantisch real, nur nicht dokumentiert
- 30+ Tools: Breite ist Stärke, Problem ist fehlende Orientierung, nicht die Anzahl
- Roslyn-Ladezeit beim ersten Call: unvermeidbar bei Vollanalyse

---

## Nächste Schritte (offen)

- [ ] Befunde 1, 2, 4 sind gute Kandidaten für den ersten `mcp-ux-audit`-Lauf
- [ ] Befund 5 (`get_overview`) wäre ein eigenes Feature-Konzept
- [ ] Befund 3 (Session-Semantik) braucht Architektur-Diskussion bevor Konzept
