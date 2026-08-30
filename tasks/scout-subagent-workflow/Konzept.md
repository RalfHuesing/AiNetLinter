---
status: draft
type: konzept
project_kind: brownfield
estimated_scope: medium
rules_dir: .agents/rules
---

# Konzept: Bedingter Scout-Subagent für verifizierbaren Recherche-Handoff

## Ziel und Nutzen

In komplexen, semantisch anspruchsvollen Codebasen (insbesondere bei Roslyn-basierten Analysen, Typgraphen und Assembly-Strukturen) kann die vorbereitende Recherche – bestehend aus MCP-Abfragen, AST-Dumps, Referenzsuchen und Datei-Inspektionen – einen großen Teil des Kontextfensters belegen. Ein monolithischer Ablauf, in dem derselbe Agent erst recherchiert und anschließend implementiert, erhöht dadurch das Risiko von Attention Dilution und veralteten Rechercheannahmen.

Dieses Konzept führt eine dedizierte **Scout-Rolle** (`scout`-Skill) als isolierten Subagenten in den AiNetLinter-Workflow ein:

1. **Kontext-Isolation:** Der Scout führt die semantische Tiefenrecherche in einer eigenen, unabhängigen Subagent-Conversation durch. Rohdaten, JSON-Payloads und explorative Fehlversuche werden nicht als Übergabearchiv an den Implementierer kopiert.
2. **Kuratierter Wissenstransfer über `code-map.md`:** Der Scout verdichtet seine Erkenntnisse präzise und quellenbezogen in die task-lokale `code-map.md` (Navigationsanker, exakte Symbol-Signaturen, betroffene Dateien, Aufruferbeziehungen, Invarianten und zugehörige Tests). Die Karte bleibt eine Navigationshilfe und wird vom Implementierer gegen Working Tree und MCP verifiziert.
3. **Fokussierter Implementierer:** Der Implementierer-Subagent erhält nur den für das Epic erforderlichen Übergabekontext, liest die `code-map.md` als Einstiegspunkt und darf bei Lücken gezielte eigene Recherche ausführen. Seine Implementierungslogik und Verifikation bleiben von der vollständigen Scout-Recherche getrennt.

Der erwartete Nutzen ist eine klarere Rollentrennung und ein kleinerer Übergabekontext bei komplexen Refactorings. Eine Qualitäts- oder Token-Verbesserung wird nicht als unbelegte Garantie vorausgesetzt, sondern anhand der Verifikationskriterien geprüft.

## Vorläufiges Urteil aus der Forschung

Die Grundidee ist **nicht unsinnig**, aber die starke Form „vor jedem komplexen Task einen zusätzlichen LLM-Scout einsetzen“ ist wissenschaftlich nicht ausreichend begründet. Der belastbare Kern ist ein **verifizierbarer, begrenzter Recherche-Handoff**: Repository-Exploration, Kontextauswahl, Unsicherheiten und Arbeitsstand werden aus dem Implementierungskontext herausgehalten und als kompakte, überprüfbare Arbeitsgrundlage übergeben.

Ob dieser Handoff durch einen separaten Scout, durch einen einzelnen Agenten mit aktivem Context-Workspace oder durch deterministische semantische Retrieval-Schritte erzeugt wird, ist eine offene empirische Designentscheidung. Das Vorhaben sollte deshalb zunächst als **bedingter Pilot mit Single-Agent-Baseline** umgesetzt werden. Ohne positiven Nachweis bei Qualität und Gesamtkosten darf der Scout nicht zum Default werden.

## Forschungsabgleich (Stand: 30.08.2026)

| Befund | Aussage für dieses Konzept |
| --- | --- |
| Kontextmanagement ist ein reales Problem: *Context as a Tool* beschreibt bei append-only Verläufen Kontextwachstum, semantische Drift und nachlassendes Reasoning; ein strukturierter, aktiv gepflegter Context-Workspace verbessert in der Studie die Langhorizont-Verarbeitung. ([Quelle](https://arxiv.org/abs/2512.22087)) | Unterstützt `code-map.md` bzw. einen begrenzten Kontext-Workspace. Es beweist nicht, dass dafür ein zweiter Subagent erforderlich ist. |
| Repository-Exploration ist eine eigenständige, messbare Fähigkeit: *SWE-Explore* trennt sie von Patch-Synthese und bewertet Coverage, Ranking und Context-Effizienz; diese Größen verfolgen dort das nachgelagerte Reparaturverhalten. ([Quelle](https://arxiv.org/abs/2606.07297)) | Unterstützt die Trennung von Recherche und Implementierung sowie die Messung der Kartenqualität. Die Karte muss möglichst line-/symbolnah und priorisiert sein, nicht nur eine Dateiliste. |
| Ein einfacher, hierarchischer Ansatz (*Agentless*) lokalisiert Dateien, Klassen/Funktionen und Editierstellen ohne komplexe Agentenschleife und reduziert dabei den Kontext deutlich. ([Quelle](https://arxiv.org/abs/2407.01489)) | Warnung vor unnötiger Multi-Agent-Komplexität. Deterministische oder semantische Retrieval-Schritte sind eine ernsthafte, wahrscheinlich günstigere Baseline. |
| Rollenbasierte Multi-Agent-Systeme können durch SOPs und Zwischenprüfungen kohärenter werden (*MetaGPT*), nennen aber auch Kaskadenfehler durch naive Agentenketten. ([Quelle](https://arxiv.org/abs/2308.00352)) | Ein Scout kann helfen, aber ein falscher Handoff kann den Implementierer ankern. Evidenz, Unsicherheit, Verifikation und Fallback sind zwingend. |
| Prozessqualität wird zunehmend separat bewertet: *ProcCtrlBench* erfasst unter anderem Interpretierbarkeit, Unterbrechbarkeit, Korrigierbarkeit, Reversibilität und die Rückgabe von Kontrolle. ([Quelle](https://arxiv.org/abs/2605.20251)) | Der Workflow braucht nachvollziehbare Übergaben, Ownership, Abbruch- und Rückfallsemantik; der Endtest allein reicht nicht. |
| In gemeinsam veränderten Arbeitsbereichen verschlechtern konfliktäre User-Edits die Lösungsrate in *SWE-Touch* im Mittel um 7,7 Prozentpunkte. ([Quelle](https://arxiv.org/abs/2608.02499)) | Ein Git-Diff nachträglich zu prüfen genügt bei echter Nebenläufigkeit nicht. Für den Scout braucht es exklusive Workspace-Ownership oder einen Lease; sonst muss die Phase ausfallen. |
| *Coding Agents Don't Know When to Act* findet bei 35–65 % der Fälle unerwünschte Änderungen, obwohl keine Änderung nötig wäre. ([Quelle](https://arxiv.org/abs/2605.07769)) | „Skip“ und „No-op“ müssen explizite erfolgreiche Ergebnisse sein, nicht nur Sonderfälle der Promptlogik. |
| Bei Whole-Repository-Migrationen bestehen Verhaltenskorrektheit und tatsächliche Migration getrennt; in *SWE Refactor Bench* bestehen nur 5,4 % der Läufe alle drei Prüfphasen. ([Quelle](https://arxiv.org/abs/2608.23564)) | Für Refactorings müssen neben Tests auch Scope-/Migrationsvollständigkeit und unabhängige Architekturprüfungen gemessen werden. |
| Benchmarkwerte sind nicht automatisch belastbar: OpenAI berichtet 2026 über Testfehler und Kontamination in SWE-bench Verified und empfiehlt für aktuelle Vergleiche SWE-bench Pro. ([Quelle](https://openai.com/index/why-we-no-longer-evaluate-swe-bench-verified/)) | Der Pilot darf sich nicht auf einen einzelnen öffentlichen Benchmark oder reine Passraten stützen. Frische, repräsentative AiNetLinter-Aufgaben und Prozessmetriken sind notwendig. |

## Architekturentscheidung (vorläufig)

Der Scout wird nicht als pauschale Pflichtphase angenommen. Die bevorzugte Reihenfolge für die Umsetzung lautet:

1. **Single-Agent-Baseline:** Der bestehende Implementierer erhält einen begrenzten, aktiv gepflegten Kontext-Workspace beziehungsweise eine Code-Map und verifiziert ihn selbst.
2. **Bedingter Scout:** Nur wenn die Recherche den erwarteten Implementierungsaufwand dominiert, mehrere semantische Grenzen überschreitet oder der Scope unbekannt ist, wird ein frischer Scout vorgeschaltet.
3. **Deterministische Unterstützung:** Skeletons, Symbolsuche, Referenzgraphen und hierarchische Lokalisierung werden gegenüber zusätzlicher LLM-Prosa bevorzugt, wenn sie dieselbe Information belastbarer und günstiger liefern.
4. **Keine Parallel-Scouts im selben Working Tree:** Ein gemeinsamer Arbeitsbereich erhält genau einen aktiven Besitzer. Bei Änderungen durch einen anderen Agenten oder Benutzer wird der Scout nicht gestartet beziehungsweise sofort angehalten; fremde Änderungen werden nicht zurückgesetzt.

Der eigentliche Erfolgskandidat ist somit nicht „mehr Agenten“, sondern **weniger ungeprüfte Kontextübergabe bei nachweisbar geringerer Gesamtlast**.

## Alternativen und Trade-offs

| Variante | Vorteil | Hauptrisiko | Vorläufige Einordnung |
| --- | --- | --- | --- |
| Einzelagent mit Context-Workspace | Geringste Latenz, kein Handoff zwischen Modellen | Langer Verlauf kann erneut anwachsen oder driften | Pflicht-Baseline |
| Scout → Implementierer | Klare Kontextgrenze und eigene Rechercheperspektive | Zusätzliche Kosten, Anchoring, fehlerhafte/zu grobe Karte | Experimentelle Option |
| Deterministisches Retrieval → Implementierer | Reproduzierbar, günstig, weniger Halluzinationen | Erkennt semantische Invarianten und Risiken nur begrenzt | Starke Vergleichsvariante |
| Mehrere parallele Scouts | Diversität und mögliche Redundanz | Merge-/Ownership-Konflikte, höhere Kosten, widersprüchliche Karten | Für diesen Workflow verwerfen |

## Pilot- und Entscheidungslogik

Vor einer dauerhaften Integration werden mindestens drei Varianten auf einer
geschichteten, repräsentativen Menge frischer AiNetLinter-Aufgaben verglichen:

- Single-Agent-Baseline ohne Scout;
- Single-Agent mit begrenztem Context-Workspace beziehungsweise Code-Map;
- Scout → Implementierer mit identischem Modell, identischen Tools und identischem fachlichem Auftrag.

Die Auswertung erfasst nicht nur den finalen Build-/Testausgang, sondern auch
Gesamttoken und Kosten, Wall-Clock-Zeit, MCP-/Tool-Aufwand, korrekte
Symbol-/Zeilenlokalisierung, Kartenpräzision und -recall, fehlerhafte oder
veraltete Handoff-Angaben, Fallback-Rate, Korrekturschleifen, P0/P1-Findings,
No-op-/Skip-Entscheidungen und unerlaubte Working-Tree-Änderungen. Die
Varianten werden mit frischen Sessions und getrennten Workspaces ausgeführt;
der Auftrag beziehungsweise die Aufgabenreihenfolge darf die Ergebnisse nicht
systematisch verzerren.

Der Scout wird nur dann als Standardphase übernommen, wenn er gegenüber der
einfachsten gleichwertigen Variante einen nachweisbaren Netto-Nutzen liefert:
keine Verschlechterung der fachlichen Korrektheit, keine unvertretbare
Kosten-/Latenzerhöhung und weniger relevante Recherchedrift beziehungsweise
Nacharbeit. Andernfalls bleibt die Code-Map als Single-Agent- oder
deterministisches Retrieval-Artefakt bestehen und der Scout wird verworfen.

---

## Betroffene Projektbereiche und bestehende Strukturen

- **`.agents/skills/scout/SKILL.md` (Neu):** Rollenbeschreibung und Handlungsanweisungen für den Scout-Subagenten (MCP-first-Recherche, präzise Pflege der `code-map.md`, kein Produktions-/Testcode).
- **`.agents/prompts/orchestrator.md` (Erweiterung):** Integration der Scout-Phase in den sequentiellen Epic-Ablauf, einschließlich Aktivierungsentscheidung, Diff-Schutz und Checkpoint-Sicherung.
- **`.agents/skills/implement/SKILL.md` (Präzisierung):** Implementierer nutzt die `code-map.md` als verifizierbaren Einstiegspunkt; ergänzende MCP-Abfragen bleiben bei Lücken ausdrücklich erlaubt.
- **Task-lokales Artefakt (`code-map.md`):** Der Orchestrator legt vor dem ersten Rollenaufruf ein minimales Gerüst an. Scout, Implementierer, Reviewer und Audit pflegen dieselbe Karte gemäß dem bestehenden Orchestrator-Vertrag; es wird keine zweite Rechercheablage eingeführt.

---

## Muss-Kriterien und Akzeptanzkriterien

1. **Getrennte Recherche und Übergabe:**
   - Der Scout wird als frischer, unabhängiger Subagent und strikt nacheinander vor dem Implementierer des betreffenden Epics aufgerufen.
   - Der Implementierer erhält keinen vollständigen Scout-Thread, keine unstrukturierten Tooltranskripte und kein Recherche-Dump-Archiv, sondern die aktuelle `code-map.md`, den Epic-Kontext, die für seine Rolle nötigen Regeln und einen knappen Hand-off.
2. **Keine Code-Modifikationen durch den Scout:**
   - Vor dem Scout-Aufruf wird der auftragsbezogene Working-Tree-Baselinepunkt festgehalten.
   - Nach dem Scout-Aufruf darf gegenüber dieser Baseline ausschließlich die task-lokale `code-map.md` geändert sein. Bei weiteren Änderungen stoppt der Orchestrator mit einem konkreten Befund und überschreibt oder verwirft keine fremden Änderungen.
   - Der Scout verändert zu keinem Zeitpunkt Produktionscode, Testcode, Projektdateien oder globale Konfigurationen. Das Schreibverbot ist eine Workflow-Grenze; eine echte Betriebssystem-Sandbox ist damit nicht zugesichert.
3. **Präzise, quellenbezogene Strukturierung:**
   - Der Scout dokumentiert konkrete relative Dateipfade, exakte Symbol-Identitäten oder Fundstellen, Signaturen beziehungsweise Typinformationen, relevante Aufrufer/Abhängigkeiten, Invarianten, Tests und Unsicherheiten.
   - Die Karte enthält keine seitenlangen Codekopien und behauptet keine Beziehung ohne passende MCP- oder Dateievidenz. Unvollständige oder nicht entscheidbare Ergebnisse werden sichtbar markiert.
4. **Pragmatische Skip-Regel (Kein künstlicher Overhead):**
   - Bei einfachen, lokal begrenzten Aufgaben (z. B. Ein-Datei-Fixes, kleine Dokumentationsanpassungen) wird der Scout übersprungen, sofern kein semantischer Recherchebedarf besteht.
   - Für komplexe Epics mit unbekannten Codebereichen, Architektur-Refactorings oder semantischen C#/Assembly-Fragen wird der Scout eingesetzt, sofern die Aktivierungsentscheidung dies begründet.
5. **Anti-Pollution-Grenze (Keine Step-Dateien, kein Grep-Ballast):**
   - Der Scout erzeugt **keine separaten Step-Dateien, Recherche-Dumps oder Unterverzeichnisse**.
   - Die gesamte Recherche wird ausschließlich **in-place in die genau eine task-lokale `code-map.md`** synchronisiert.
   - Dadurch werden historische Recherche-Dumps und veraltete Code-Zitate nicht als zusätzliche Task-Artefakte weitergereicht.
6. **Konformität mit dem AiNetLinter-MCP-Vertrag:**
   - Der Scout arbeitet strikt nach `.agents/rules/AiNetLinter-McpWorkflow.mdc` (MCP-first für semantische C#-Abfragen, `targetType=project|assembly`, token-schonende Parameter für `get_file_tree`).
7. **Graceful Fallback für den Implementierer:**
   - Sollte eine Information in `code-map.md` fehlen oder fehlerhaft sein, darf der Implementierer weiterhin selbstständig gezielte MCP- oder Leseabfragen ausführen. Er ist niemals blind an den Scout gebunden.
8. **Lebenszeit und Staleness der Karte:**
   - Vor jeder Verwendung prüft die nachfolgende Rolle die relevanten Kartenangaben gegen den aktuellen Working Tree.
   - Nach Codeänderungen aktualisiert der Implementierer die betroffenen Kartenabschnitte; Reviewer und Audit entfernen oder korrigieren nachweislich veraltete Navigationsangaben.

---

## Non-Goals und Scope-Grenzen

- **Keine neuen Zwischenformate oder Step-Archive:** Es werden keine neuen Step-, JSON- oder Cache-Dateien angelegt. Historische Dumps werden nicht als Task-Artefakte im Dateisystem belassen.
- **Keine Code-Kopien in `code-map.md`:** `code-map.md` enthält nur kompakte, quellenbezogene Navigationsdaten, keine seitenlangen Code-Duplikate.
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
                  │  - Führt bedarfsgerechte MCP-Analyse aus │
                  │  - Isoliertes Kontextfenster            │
                  │  - Schreibt nur die Code-Map             │
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
         kuratiertem Epic-Kontext     ▼
                  ┌─────────────────────────────────────────┐
                  │          Implementierer-Rolle           │
                  │  - Liest Konzept + code-map.md          │
                  │  - Verifiziert und ergänzt Recherche    │
                  │  - Volle Aufmerksamkeit auf Logik/Tests │
                  │  - Führt get_violations & Tests aus     │
                  └─────────────────────────────────────────┘
```

### 1. Scout-Subagent (`.agents/skills/scout/SKILL.md`)
- **Input:** Task-Pfad, Primäraufgabe, Epics aus `Konzept.md` / `roadmap.md`, initiales `code-map.md`-Gerüst.
- **Ablauf:**
  1. Liest den Scout-Skill, die relevanten Regeln, das Konzept, den Epic-Kontext und die vorhandene `code-map.md`.
  2. Identifiziert Einstiegspunkte mit `find_symbol`, `get_feature_context` oder — bei einer lokalen DLL — `inspect_assembly`.
  3. Ermittelt Aufrufer und Abhängigkeiten mit `find_references`, `get_call_tree` und/oder `dependency_graph`.
  4. Identifiziert zugehörige Tests mit `get_test_context` und prüft relevante Dokumentations-/Konfigurationsbereiche ergänzend per gezielter Textsuche.
  5. Extrahiert nur belegte Invarianten, Architekturvorgaben, Risiken und offene technische Unsicherheiten.
  6. Aktualisiert ausschließlich die strukturierte Navigation in `code-map.md` und entfernt dadurch veraltete Einträge.
  7. Beendet die Session mit einem kurzen Hand-off-Bericht: geänderte Karte, erkannte Zieldateien/Symbole, Unsicherheiten und ausgeführte Prüfungen.

### 2. Implementierer-Subagent (`.agents/skills/implement/SKILL.md`)
- **Input:** Task-Pfad, Zielauftrag, `Konzept.md`, die kuratierte `code-map.md`.
- **Ablauf:** Startet als frischer Subagent mit dem Epic-Kontext. Verifiziert die in `code-map.md` ausgewiesenen Einstiegspunkte, liest die tatsächlich relevanten Dateien, ergänzt bei Lücken gezielte Recherche, schreibt Code und führt Tests sowie gezielte MCP-Prüfungen (`get_violations`) aus.

### 3. Orchestrator-Steuerung (`.agents/prompts/orchestrator.md`)
- Vor dem Scout-Aufruf legt der Orchestrator die Aktivierungsentscheidung und den Diff-Baselinepunkt fest.
- Nach Abschluss des Scouts prüft der Orchestrator den zulässigen Diff, protokolliert den Hand-off und sichert die `code-map.md` in einem taskbezogenen Checkpoint.
- Danach ruft er den Implementierer auf; Scout und Implementierer laufen niemals parallel im selben Working Tree.

---

## Betriebs- und Bedrohungsmodell

- **Betriebsmodell:** Lokaler Entwickler-Agenten-Workflow im Rahmen der bestehenden AiNetLinter-Toolchain.
- **Kontext-Hygiene:** Der Scout soll den Übergabekontext auf belegte Navigationsdaten begrenzen und explorative Rohdaten im eigenen Subagent-Thread belassen.
- **Betriebsgrenze:** Der Scout darf im Workflow nur die task-lokale `code-map.md` schreiben. Diese Grenze wird über Rollenauftrag, Baseline-/Diff-Prüfung und Checkpoint gesteuert, nicht als vollständige Sicherheits-Sandbox behauptet.
- **Nebenläufigkeit:** Der Scout wird nur bei stabilem, eindeutig abgegrenztem Working-Tree-Baselinepunkt gestartet. Fremde oder parallel entstehende Änderungen werden nicht überschrieben, automatisch zurückgesetzt oder in den Scout-Commit aufgenommen.
- **Fehlersemantik:** Fehlende MCP-Fähigkeit, unvollständige Ergebnisse oder eine nicht zulässige Dateiveränderung werden im Hand-off sichtbar gemeldet; der Orchestrator stoppt vor dem Implementierer oder überspringt den Scout mit dokumentierter Begründung.

---

## Verifikation und Qualitätskriterien

1. **Übergabe-Isolation:** An einem repräsentativen komplexen Epic wird nachgewiesen, dass Scout und Implementierer frische, getrennte Subagent-Sessions verwenden und der Implementierer keinen vollständigen Scout-Thread oder Tooltranskript erhält. Eine konkrete Tokenquote ist nur ein optionaler Messwert, falls die Plattform belastbare Zähler bereitstellt.
2. **Zulässiger Diff:** Der Orchestrator erkennt nach einem Scout-Lauf jede Änderung außerhalb der task-lokalen `code-map.md`, ohne fremde Änderungen zu überschreiben oder zu committen.
3. **Qualität der `code-map.md`:** Die Karte enthält die vereinbarten Pflichtabschnitte, konkrete Pfade/Symbole, Evidenz beziehungsweise Unsicherheiten sowie relevante Tests und ist gegen den aktuellen Code verifizierbar.
4. **Fallback und Staleness:** Ein absichtlich unvollständiger Kartenabschnitt führt dazu, dass der Implementierer gezielt nachrecherchiert und die Karte aktualisiert; veraltete Einträge werden entfernt.
5. **Skip-Verhalten:** Ein trivialer Task durchläuft keine Scout-Phase; die Entscheidung und ihre Begründung sind im Orchestrator-Log beziehungsweise Checkpoint nachvollziehbar.
6. **Konsistenzprüfungen:** Markdown-/Pfadkonsistenz, Einhaltung von `AGENTS.md` und `.agents/rules/` sowie — falls durch die Umsetzung C# oder Tests geändert werden — die dort vorgeschriebenen Build- und Nicht-Stress-Test-Gates werden geprüft.

---

## Offene Punkte und Entscheidungsbedarf

1. **Grundsatzentscheidung — Empfehlung: als bedingten Pilot weiterverfolgen.** Soll das Vorhaben ausdrücklich als A/B-Pilot für einen verifizierbaren Recherche-Handoff fortgeführt werden, statt den separaten Scout bereits als Zielarchitektur festzuschreiben? Die Forschung stützt Kontextmanagement und Repository-Exploration, aber nicht die Notwendigkeit eines zweiten LLM-Agenten.
2. **Technische Variante — Empfehlung: drei Varianten vergleichen.** Soll der Pilot Single-Agent mit Context-Workspace, deterministisches Retrieval und Scout → Implementierer unter gleichen Bedingungen vergleichen? Ohne diese Baseline wäre ein möglicher Nutzen des Scouts nicht von allgemeinem Retrieval- oder Prompting-Gewinn zu trennen.
3. **Granularität des Aufrufs — Empfehlung: pro Epic mit Scope-Prüfung.** Falls der Scout den Pilot besteht: Soll er einmalig pro Task vor dem ersten Epic laufen, oder vor jedem Epic, das neue beziehungsweise veränderte Codebereiche untersucht? Für einen Lauf pro Epic sprechen die Staleness-Regeln; bei mehreren Epics im selben unveränderten Bereich kann die Roadmap den Scout begründet überspringen.
4. **Schema der `code-map.md` — Empfehlung: verbindliche Minimalstruktur.** Falls der Handoff weiterverfolgt wird: Soll die Karte feste Pflichtabschnitte wie `## Primäre Einstiegspunkte`, `## Betroffene Dateien und Symbole`, `## Aufrufer und Abhängigkeiten`, `## Relevante Tests`, `## Invarianten, Risiken und Unsicherheiten` sowie `## Verifikation` erhalten? Die Inhalte sollten kompakt bleiben, aber die Abschnitte würden Übergabe und Review prüfbarer machen.
5. **Aktivierung — Empfehlung: explizite Roadmap-Entscheidung mit Heuristik.** Falls der Scout den Pilot besteht: Soll der Orchestrator pro Epic einen sichtbaren Modus `required`, `optional` oder `skip` führen, wobei `estimated_scope` nur als Hinweis dient? Eine reine Scope-Automatik wäre zu grob; eine explizite Epic-Entscheidung bleibt nachvollziehbar und erlaubt eine begründete Ausnahme.

---

## Arbeitsgedächtnis (nur Draft)

### Bestätigte Entscheidungen
- **Trennung von Recherche und Coden:** Recherche wird in einen isolierten Subagenten (`scout`) ausgelagert, um den Implementierer vor Kontext-Verschmutzung zu schützen.
- **Austauschformat:** Kein neues Datei-Format; wir nutzen und stärken die bereits im Orchestrator spezifizierte task-lokale `code-map.md`. Der Orchestrator legt das Gerüst an; sie muss nicht vorab im Task-Verzeichnis existieren.
- **Anti-Pollution & Single-File-In-Place-Prinzip:** Keine Step-Dateien oder Recherche-Unterordner. Es existiert immer nur genau eine `code-map.md`, die in-place aktualisiert wird. Damit werden Dateileichen und irreführende Treffer bei späteren `rg`/`grep`-Suchen komplett verhindert.
- **Pragmatismus:** Bei Trivial-Tasks bleibt der Scout optional / überspringbar.
- **Karte ist nicht Source of Truth:** Jede Rolle verifiziert relevante Kartenangaben gegen Working Tree und MCP; der Implementierer bleibt bei Lücken handlungsfähig.
- **Schreibgrenze als Workflow-Vertrag:** Nach dem Scout darf gegenüber der Baseline nur `code-map.md` verändert sein. Fremde Änderungen werden nicht überschrieben oder automatisch bereinigt.
- **Vorläufiges Forschungsurteil:** Der Bedarf an Kontextmanagement und fokussierter Repository-Exploration ist plausibel; ein separater Scout ist nur eine zu prüfende Implementierungsvariante und kein bestätigter Default.
- **Pilot statt voreilige Standardisierung:** Vor einer dauerhaften Skill-/Orchestrator-Integration müssen Single-Agent-Context-Workspace, deterministisches Retrieval und Scout → Implementierer unter vergleichbaren Bedingungen gegeneinander evaluiert werden.
- **Prozessqualität als Erfolgskriterium:** Bewertung umfasst neben fachlicher Korrektheit auch Handoff-Fehler, Staleness, Kosten, Latenz, Fallbacks, Korrekturschleifen, No-op-Entscheidungen und Workspace-Ownership.

### Geprüfte Evidenz und Konsequenzen
- `.agents/prompts/orchestrator.md` sieht bereits genau eine task-lokale `code-map.md` vor, die vor dem ersten Rollenaufruf als minimales Gerüst angelegt und von Rollen gepflegt wird. Das Konzept baut darauf auf, führt aber keinen zweiten Ablageort ein.
- `.agents/skills/implement/SKILL.md` verpflichtet den Implementierer bereits zum Lesen und Verifizieren der Karte, erlaubt ergänzende MCP-Recherche und verlangt nach Codeänderungen gezielte Verifikation. Die Scout-Erweiterung darf diesen Fallback nicht einschränken.
- Im Zielverzeichnis existiert derzeit nur `Konzept.md`; eine `code-map.md` wird erst im späteren Orchestrator-Lauf erzeugt.
- Ein Rollenprompt allein erzwingt keine echte Dateisystem-Sandbox. Die Umsetzung benötigt daher mindestens Baseline-/Diff-Prüfung und darf bei unerlaubten Änderungen keine fremden Änderungen zurücksetzen.
- Aktuelle Forschung trennt zunehmend Repository-Exploration und Prozessqualität vom finalen Pass/Fail-Ergebnis; der Pilot soll diese Zwischenmetriken instrumentieren, ohne öffentliche Benchmarkwerte als alleinige Wahrheit zu verwenden.
- Forschungsbefunde zu Multi-Agenten sind aufgaben- und benchmarkabhängig. Positive Ergebnisse zu Rollen/SOPs existieren, aber auch Kaskadenfehler, Anchoring und zusätzliche Kommunikationskosten; für dieses konkrete C#/Roslyn-Workflowdesign liegt kein direkter Wirksamkeitsnachweis vor.
- Bei parallelen Änderungen im selben Working Tree reicht ein nachträglicher Diff nicht zuverlässig zur Attribution. Exklusive Ownership beziehungsweise ein Lease ist Voraussetzung; andernfalls ist `skip` das sichere Verhalten.

### Relevante Dateien
- `.agents/prompts/orchestrator.md`
- `.agents/skills/implement/SKILL.md`
- `.agents/skills/concept-planner/SKILL.md`
- `.agents/rules/AiNetLinter-McpWorkflow.mdc`

### Nächster Planungsschritt
- Zuerst ist die Grundsatzfrage zu beantworten, ob der Scout als bedingter, instrumentierter Pilot gegen einfachere Varianten geprüft werden soll. Erst danach werden Aufrufgranularität, Kartenformat und Aktivierungsmodus endgültig festgelegt.
