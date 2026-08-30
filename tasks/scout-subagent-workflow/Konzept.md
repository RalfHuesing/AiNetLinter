---
status: draft
type: konzept
project_kind: brownfield
estimated_scope: medium
rules_dir: .agents/rules
---

# Konzept: Scout-Subagent für isolierte Code-Recherche und Kontextschutz

## Ziel und Nutzen

In komplexen, semantisch anspruchsvollen Codebasen (insbesondere bei Roslyn-basierten Analysen, Typgraphen und Assembly-Strukturen) verbraucht die vorbereitende Recherche – bestehend aus MCP-Abfragen, AST-Dumps, Referenzsuchen und Datei-Inspektionen – tausende bis zehntausende Tokens. In einem monolithischen Ablauf, bei dem derselbe Agent erst recherchiert und anschließend implementiert, führt dies zu **Context Pollution** (Attention Dilution / Context Rot). Die kognitiv anspruchsvollste Aufgabe – das Schreiben und Verifizieren von fehlerfreiem Produktionscode – findet dann in einem überfüllten, "verschmutzten" Kontextfenster statt.

Dieses Konzept führt eine dedizierte **Scout-Rolle** (`scout`-Skill) als isolierten Subagenten in den AiNetLinter-Workflow ein:

1. **Kontext-Isolation (Garbage Collection):** Der Scout führt die gesamte semantische Tiefenrecherche (MCP-Tools, Symbolbäume, Call-Trees, File-Skeletons) in einer eigenen, isolierten Subagent-Conversation durch. Sämtliche Rohdaten, JSON-Payloads und explorativen Fehlversuche verbleiben in diesem isolierten Thread und belasten den nachfolgenden Implementierer nicht.
2. **Kuratierter Wissenstransfer über `code-map.md`:** Der Scout verdichtet seine Erkenntnisse verlustfrei in die bereits etablierte task-lokale `code-map.md` (Navigationsanker, exakte Symbol-Signaturen, betroffene Dateien, Call-Chains, Invarianten und zugehörige Tests).
3. **Fokussierter Implementierer:** Der Implementierer-Subagent startet mit einem frischen, minimalen Kontextfenster, liest die vorbereitete `code-map.md` und gezielt nur die tatsächlichen Zieldateien. Er verfügt über die maximale kognitive Aufmerksamkeit für Implementierungslogik, Edge-Case-Behandlung und Verifikations-Gates (`get_violations`, Tests).

Der Nutzen liegt in einer höheren Implementierungsqualität, weniger Syntax- und Logik-Halluzinationen bei komplexen Refactorings, signifikant reduzierter Attention-Dilution und klarerer Rollentrennung ohne Einführung unnötiger neuer Dateiformate.

---

## Betroffene Projektbereiche und bestehende Strukturen

- **`.agents/skills/scout/SKILL.md` (Neu):** Rollenbeschreibung und Handlungsanweisungen für den Scout-Subagenten (Fokus: semantische MCP-first-Recherche, Strukturierung der `code-map.md`, striktes Schreibverbot für Produktions-/Testcode).
- **`.agents/prompts/orchestrator.md` (Erweiterung):** Integration der Scout-Phase in den Epic- und Task-Ablauf vor dem Aufruf des Implementierers (inklusive Checkpoint-Sicherung und Skip-Regel für Trivial-Tasks).
- **`.agents/skills/implement/SKILL.md` (Präzisierung):** Implementierer nutzt die vom Scout kuratierte `code-map.md` als primären Einstiegspunkt und führt nur noch bei konkreten Lücken ergänzende MCP-Abfragen durch.
- **Task-lokale Artefakte (`code-map.md`):** Dient als das standardisierte, mensch- und maschinenlesbare Übergabemedium zwischen Scout und Implementierer.

---

## Muss-Kriterien und Akzeptanzkriterien

1. **Vollständige Kontext-Isolation:**
   - Der Scout wird als eigenständiger Subagent aufgerufen.
   - Der Roh-Token-Ballast der Recherche verbleibt in der Subagent-Session und wird nicht an den Implementierer übertragen.
2. **Keine Code-Modifikationen durch den Scout:**
   - Der Scout verändert zu keinem Zeitpunkt Produktionscode, Testcode, Projektdateien oder globale Konfigurationen.
   - Einziges zulässiges Schreibziel des Scouts ist die task-lokale `code-map.md`.
3. **Verlustfreie Strukturierung statt unpräziser Text-Zusammenfassungen:**
   - Der Scout darf Code nicht durch vage Prosa-Nacherzählungen ersetzen ("Lossy Compression").
   - Er erfasst konkrete Dateipfade, exakte Symbol-Identitäten, Schnittstellen/Signaturen, Aufruferbeziehungen, Invarianten und die relevanten Testklassen.
4. **Pragmatische Skip-Regel (Kein künstlicher Overhead):**
   - Bei einfachen, lokal begrenzten Aufgaben (z. B. Ein-Datei-Fixes, kleine Dokumentationsanpassungen) kann der Orchestrator oder Nutzer den Scout überspringen.
   - Der Scout ist primär für mehrstufige Epics, unbekannte Codebereiche, Architektur-Refactorings und semantische C#/Assembly-Analysen vorgesehen.
5. **Konformität mit dem AiNetLinter-MCP-Vertrag:**
   - Der Scout arbeitet strikt nach `.agents/rules/AiNetLinter-McpWorkflow.mdc` (MCP-first für semantische C#-Abfragen, `targetType=project|assembly`, token-schonende Parameter für `get_file_tree`).
6. **Graceful Fallback für den Implementierer:**
   - Sollte eine Information in `code-map.md` fehlen oder fehlerhaft sein, darf der Implementierer weiterhin selbstständig gezielte MCP- oder Leseabfragen ausführen. Er ist niemals blind an den Scout gebunden.

---

## Non-Goals und Scope-Grenzen

- **Keine neuen Zwischenformate:** Es werden keine neuen Step-, JSON- oder Cache-Dateien erfunden (`code-map.md` bleibt das alleinige Navigationsartefakt).
- **Kein interaktiver Nutzer-Dialog durch den Scout:** Der Scout agiert vollautonom im Hintergrund und stellt keine Zwischenfragen an den Benutzer.
- **Keine Vorab-Code-Generierung (No Drafting):** Der Scout erzeugt keine Code-Snippets, Patches oder Pseudo-Implementierungen. Das Design und die Codierung liegen zu 100 % beim Implementierer.
- **Kein Ersatz für bestehende Rollen:** Der Scout ersetzt weder den `concept-planner` (fachliche Verträge), noch den `implement`-Skill (Code), noch den `review`-Skill (unabhängige Prüfung), noch den `audit`-Skill (Qualitäts-Gates).

---

## Rollen- und Interaktionsmodell

```
                  ┌─────────────────────────────────────────┐
                  │              Orchestrator               │
                  └───────────────────┬─────────────────────┘
                                      │
           1. Startet Scout-Subagent  │ (bei komplexen Epics / unklarem Scope)
                                      ▼
                  ┌─────────────────────────────────────────┐
                  │              Scout-Rolle                │
                  │  - Führt 10-30 MCP-Tools aus            │
                  │  - Isoliertes Kontextfenster            │
                  │  - Schreibt KEINEN Code                 │
                  └───────────────────┬─────────────────────┘
                                      │
                       2. Aktualisiert│ code-map.md
                                      ▼
                  ┌─────────────────────────────────────────┐
                  │          task-lokale code-map.md        │
                  │  - Exakte Symbole & Dateipfade          │
                  │  - Call-Trees, Abhängigkeiten, Tests   │
                  │  - Relevante Invarianten                │
                  └───────────────────┬─────────────────────┘
                                      │
      3. Startet Implementierer mit   │
         frischem/sauberem Kontext    ▼
                  ┌─────────────────────────────────────────┐
                  │          Implementierer-Rolle           │
                  │  - Liest nur Konzept + code-map.md      │
                  │  - Liest gezielt 2-3 Zieldateien        │
                  │  - Volle Aufmerksamkeit auf Logik/Tests │
                  │  - Führt get_violations & Tests aus     │
                  └─────────────────────────────────────────┘
```

### 1. Scout-Subagent (`.agents/skills/scout/SKILL.md`)
- **Input:** Task-Pfad, Primäraufgabe, Epics aus `Konzept.md` / `roadmap.md`, initiales `code-map.md`-Gerüst.
- **Ablauf:**
  1. Identifiziert Einstiegspunkte mit `find_symbol`, `get_feature_context` oder `inspect_assembly`.
  2. Ermittelt Aufrufer und Abhängigkeiten mit `find_references`, `get_call_tree`, `dependency_graph`.
  3. Identifiziert zugehörige Testklassen mit `get_test_context`.
  4. Extrahiert Invarianten und Architekturvorgaben aus den betroffenen Bereichen.
  5. Schreibt die strukturierte Navigation in `code-map.md`.
  6. Beendet die Session mit einem kurzen Hand-off-Bericht (z. B. "Recherche abgeschlossen, 4 Zieldateien und 2 Testklassen in `code-map.md` erfasst").

### 2. Implementierer-Subagent (`.agents/skills/implement/SKILL.md`)
- **Input:** Task-Pfad, Zielauftrag, `Konzept.md`, die kuratierte `code-map.md`.
- **Ablauf:** Startet mit nahezu leerem Kontext. Öffnet nur die in `code-map.md` ausgewiesenen Zieldateien. Schreibt Code, führt Tests und gezielte MCP-Prüfungen (`get_violations`) aus.

### 3. Orchestrator-Steuerung (`.agents/prompts/orchestrator.md`)
- Der Orchestrator sichert nach Abschluss des Scouts einen Zwischen-Checkpoint (`docs(<task>): Erfasse Code-Map durch Scout`).
- Danach ruft er den Implementierer auf.

---

## Betriebs- und Bedrohungsmodell

- **Betriebsmodell:** Lokaler Entwickler-Agenten-Workflow im Rahmen der bestehenden AiNetLinter-Toolchain.
- **Kontext-Hygiene:** Der Scout reduziert das Risiko von "Silent Degenerations" bei LLM-Codegenerierungen.
- **Sicherheits- & Bedrohungsmodell:** Der Scout hat nur Lesezugriff auf das Repository und Schreibzugriff auf `code-map.md`. Keine Ausführung von fremdem Binärcode, keine Manipulation von Build-Artefakten.

---

## Verifikation und Qualitätskriterien

1. **Token-Effizienz-Vergleich:** Verifikation anhand eines komplexen Epics (z. B. Assembly-Analyse), dass der Implementierer-Subagent mit < 15% der bisherigen Kontextgröße startet.
2. **Qualität der `code-map.md`:** Sicherstellung, dass alle vom Implementierer tatsächlich benötigten Pfade und Symbole in der generierten Karte vorhanden sind.
3. **Konsistenzprüfungen:** Validierung der Markdown-Dateien, Einhaltung aller Vorgaben aus `AGENTS.md` und `.agents/rules/`.

---

## Arbeitsgedächtnis (nur Draft)

### Bestätigte Entscheidungen
- **Trennung von Recherche und Coden:** Recherche wird in einen isolierten Subagenten (`scout`) ausgelagert, um den Implementierer vor Kontext-Verschmutzung zu schützen.
- **Austauschformat:** Kein neues Datei-Format; wir nutzen und stärken die bereits im Orchestrator spezifizierte `code-map.md`.
- **Pragmatismus:** Bei Trivial-Tasks bleibt der Scout optional / überspringbar.

### Relevante Dateien
- [.agents/prompts/orchestrator.md](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/.agents/prompts/orchestrator.md)
- [.agents/skills/implement/SKILL.md](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/.agents/skills/implement/SKILL.md)
- [.agents/skills/concept-planner/SKILL.md](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/.agents/skills/concept-planner/SKILL.md)
- [.agents/rules/AiNetLinter-McpWorkflow.mdc](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/.agents/rules/AiNetLinter-McpWorkflow.mdc)

### Offene Fragen für das Sparring
1. **Granularität des Aufrufs:** Soll der Scout **einmalig pro Task** vor dem ersten Epic laufen, oder **vor jedem einzelnen Epic** eines Großkonzepts? (Empfehlung: Vor jedem Epic, wenn das Epic neue/andere Codebereiche betrifft; bei kleinen Folge-Epics optional).
2. **Schema der `code-map.md`:** Soll `code-map.md` eine feste, verbindliche Markdown-Tabellen-/Abschnittsstruktur erhalten (z. B. `## Primäre Einstiegspunkte`, `## Zieldateien & Zeilen`, `## Aufrufer & Referenzen`, `## Relevante Tests`, `## Invarianten & Risiken`), damit der Implementierer die Daten deterministisch parsen kann?
3. **Automatische vs. Manuelle Aktivierung:** Soll der Orchestrator anhand des geschätzten Scopes (`estimated_scope: large` vs `small`) automatisch entscheiden, ob der Scout gestartet wird, oder entscheidet das die Roadmap pro Epic?
