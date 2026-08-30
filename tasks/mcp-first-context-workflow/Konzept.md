---
status: ready
type: konzept
project_kind: brownfield
estimated_scope: medium
rules_dir: .agents/rules
---

# Konzept: MCP-first-Kontextworkflow für fokussierte Implementierung

## Ziel und Einordnung

Der ursprüngliche Vorschlag war ein verpflichtender Scout-Subagent, der vor dem Implementierer eine Recherche und eine `code-map.md` erzeugt. Die fachliche Richtung ist sinnvoll — vor Änderungen soll der Agent den relevanten Kontext strukturiert und überprüfbar ermitteln. Ein dauerhaft vorgeschalteter zweiter Agent ist dafür aber nicht erforderlich und erhöht Prozess-, Token- und Koordinationsaufwand.

Dieses Konzept ersetzt deshalb den verpflichtenden Scout durch einen MCP-first-Workflow im bestehenden Implementierungsablauf:

- Der Prompt folgt bei jedem Task derselben Phasenfolge: Kontext festhalten, MCP-Kontext ermitteln, bearbeiten/diagnostizieren, verifizieren und übergeben.
- Der Implementierer ordnet den Auftrag zunächst als lokal/eindeutig, semantisch oder mehrstufig ein.
- Bei C#-Fragen verwendet er zuerst den AiNetLinter-MCP für zielgerichtete Kontextabfragen.
- `get_feature_context` ist der bevorzugte One-Shot-Einstieg für bekannte Symbole; `find_symbol` dient der Lokalisierung unbekannter Einstiegspunkte.
- Weitere MCP-Abfragen werden nur bei konkreten Informationslücken nachgezogen.
- Eine kompakte `code-map.md` ist ein fester Bestandteil jedes Task-Durchlaufs. Bei kleinen Aufgaben bleibt sie minimal; sie ist ein Arbeitsgedächtnis, keine zusätzliche Wahrheitsschicht.
- Nach der Änderung folgen MCP- und Test-Verifikation im bestehenden Lifecycle.

Damit wird nicht die Kontextarbeit abgeschafft, sondern ihr Ort und ihre Ausführung verändert: vom separaten Recherche-Agenten in einen verifizierbaren, adaptiven Arbeitsablauf des Agents, der die Änderung letztlich verantwortet.

## Architekturentscheidung

Der verpflichtende Scout-zu-Implementierer-Handoff wird verworfen. Der Standardpfad ist ein einzelner Implementierer mit einer festen Phasenfolge und abgestufter Kontextaufnahme innerhalb der MCP-Phase:

1. Auftrag klassifizieren: lokaler Text-/Konfigurationsfall, bekannte C#-Struktur, unbekannte C#-Struktur oder mehrstufiges Vorhaben.
2. Bei bekanntem Symbol `get_feature_context` mit `targetType: project` und absolutem `targetPath` aufrufen.
3. Bei unbekanntem Symbol zunächst `find_symbol` verwenden und danach den relevanten Feature-Kontext laden.
4. Nur erkannte Lücken mit `get_symbol_body`, `find_references`, `get_impact`, `dependency_graph`, `get_test_context` oder passenden Violation-Abfragen schließen.
5. Die wesentlichen Befunde in einer kleinen, standardisierten `code-map.md` persistieren; bei einfachen Aufgaben genügt eine minimale Ausfüllung der Struktur.
6. Ändern, anschließend betroffene Regeln/Tests und den Build gemäß Projektregeln verifizieren.
7. Der Implementierer entscheidet anhand der sichtbaren Evidenz, wann der Kontext ausreicht; der Orchestrator erzwingt keine starre Mindestzahl oder feste Sequenz von MCP-Aufrufen.

Ein zusätzlicher Scout kann später als gezielte Optimierung untersucht werden, ist aber weder Architekturvoraussetzung noch Standardverhalten. Insbesondere soll kein zweiter LLM-Agent eingeführt werden, nur weil Recherche als eigener Prozessschritt benannt wurde.

## Warum das kein Verzicht auf Exploration ist

Aktuelle Arbeiten zur agentischen Softwareentwicklung stützen vor allem drei Prinzipien:

1. Kontext muss begrenzt, strukturiert und auf die Aufgabe bezogen sein.
2. Exploration ist dann wertvoll, wenn sie nachweisbar die nachgelagerte Änderung verbessert.
3. Agentische Prozesse brauchen sichtbare Stop-, Übergabe- und Korrekturmöglichkeiten.

Daraus folgt nicht automatisch, dass ein separater Scout besser ist. Ein Scout kann Exploration kapseln, aber auch eine zusätzliche Übergabe- und Stale-Context-Grenze erzeugen. Im AiNetLinter-Projekt existiert bereits ein semantisches Werkzeug, das genau die relevanten Kontextdimensionen liefern kann. Die naheliegende Primärhypothese ist daher: erst den bestehenden Agenten mit diesem Werkzeug gut führen; erst bei gemessenen Engpässen eine zusätzliche Rolle einführen.

## Forschungsabgleich

| Befund | Relevanz für AiNetLinter | Konsequenz |
|---|---|---|
| [Context as a Tool](https://arxiv.org/abs/2512.22087) beschreibt Context Explosion und Semantic Drift und empfiehlt einen strukturierten, externen Kontextarbeitsbereich. | Unbegrenzte Roh-Recherche ist riskant; ein kleines, aktualisierbares Arbeitsgedächtnis ist sinnvoll. | Eine stets vorhandene, kompakte und evidenzbasierte `code-map.md` verwenden. |
| [SWE-Explore](https://arxiv.org/abs/2606.07297) trennt Repository-Exploration und Patch-Synthese und bewertet Exploration über Coverage, Ranking, Kontext-Effizienz und Downstream-Repair. | Exploration ist eine relevante Funktion, aber die Trennung muss ihren Zusatznutzen zeigen. | MCP-Kontextaufnahme als messbare Phase behalten; Scout nicht als unbelegte Pflichtarchitektur festschreiben. |
| [Agentless](https://arxiv.org/abs/2407.01489) zeigt, dass hierarchische Lokalisierung und Reparatur mit geringerer Agentenkomplexität leistungsfähig sein können. | Ein einfacherer Ablauf kann ausreichend und robuster sein. | Single-Agent-Baseline mit gezielter Lokalisierung priorisieren. |
| [MetaGPT](https://arxiv.org/abs/2308.00352) zeigt den möglichen Nutzen klarer Rollen und SOPs, weist aber implizit auf die Kosten von Übergaben und Kaskaden hin. | Rollen helfen nur bei klarer Verantwortungsgrenze und echtem Parallel-/Spezialisierungsnutzen. | Kein Scout allein wegen einer Rollenbezeichnung; Handoff muss einen messbaren Vorteil liefern. |
| [ProcCtrlBench](https://arxiv.org/abs/2605.20251) betrachtet agentische Prozesse unter anderem nach Interpretierbarkeit, Unterbrechbarkeit, Korrigierbarkeit und Rückgabe der Kontrolle. | Ein komplexer Workflow muss kontrollierbar und abbrechbar bleiben. | Stop-/Fallback-/Resume-Regeln explizit machen; kein unkontrolliertes Rechercheketten-Verhalten. |
| [SWE-Touch](https://arxiv.org/abs/2608.02499) berichtet bei gemeinsam veränderter Arbeitsumgebung schlechtere Auflösungsraten und häufige externe Änderungen. | Ein Scout-Handoff im selben Working Tree wäre besonders stale- und konfliktanfällig. | Eine aktive Ownership je Working Tree; externe Änderungen erkennen und neu baselinen, niemals überschreiben. |
| [Coding Agents Don’t Know When to Act](https://arxiv.org/abs/2605.07769) zeigt unnötige Änderungen bei No-Change-Aufgaben. | Exploration darf nicht automatisch in Aktion oder neue Dateien münden. | Explizite No-op-/Skip-Entscheidung und Änderungsrecht erst nach ausreichender Evidenz. |
| [SWE Refactor Bench](https://arxiv.org/abs/2608.23564) trennt Verhaltenserhalt von vollständiger Migrationsabdeckung und zeigt geringe End-to-End-Erfolgsraten komplexer Refactorings. | Kontextqualität allein garantiert keine vollständige Änderung. | Verifikation muss Verhalten und Scope-/Abdeckungs-Vollständigkeit getrennt prüfen. |
| [OpenAI: Why we no longer evaluate SWE-bench Verified](https://openai.com/index/why-we-no-longer-evaluate-swe-bench-verified/) warnt vor Testproblemen und Contamination in einem verbreiteten Benchmark. | Agentenprozessentscheidungen dürfen nicht mit einer einzelnen Benchmarkzahl begründet werden. | Lokale, reproduzierbare Projektmetriken und Failure-Analysen verwenden. |

Die Forschung ist heterogen und teilweise sehr aktuell; sie beweist nicht, dass ein MCP-first-Workflow für AiNetLinter besser ist. Sie stützt aber die Designentscheidung, zunächst Kontextqualität, Begrenzung, Verifikation und Kontrollierbarkeit zu optimieren und den Scout nur als spätere Vergleichsvariante zu behandeln.

## Lokale Fähigkeit des AiNetLinter-MCP

Der entscheidende projektspezifische Punkt ist, dass AiNetLinter bereits eine zusammengesetzte semantische Exploration anbietet. `get_feature_context` bündelt für ein C#-Symbol:

- Deklaration und Position,
- Metriken und Budget-/Footprint-Informationen,
- direkte Aufrufer,
- statische Testzuordnung,
- relevante Linter-Verstöße.

Die aktuelle Tool-Semantik verlangt für projektgebundene Abfragen `targetType: project` und einen absoluten `targetPath`. Ergebnisgrößen können über Parameter wie `maxCallers` und `maxTests` begrenzt werden; abgeschnittene Ergebnisse müssen als Unsicherheit behandelt und bei Bedarf gezielt vertieft werden.

Eine lokale MCP-Probe gegen `AiNetLinter.Mcp.AnalysisToolCall` lieferte genau diese fünf strukturierten Dimensionen. Die Aufruferliste wurde bei der gesetzten Grenze explizit als abgeschnitten ausgewiesen, die Testzuordnung enthielt drei Tests und die Violation-Abfrage keine Befunde. Das ist die praktische Evidenz, dass der Agent den relevanten Kontext bereits zielgerichtet und mit sichtbaren Grenzen ermitteln kann, ohne zunächst einen weiteren Agenten zu starten.

## Soll-Workflow

```text
Auftrag
  -> Einordnung (lokal / semantisch / mehrstufig / No-op möglich)
  -> MCP-first-Kontextaufnahme
       bekanntes Symbol: get_feature_context
       unbekanntes Symbol: find_symbol -> get_feature_context
       Lücken: gezielte Spezialabfragen
  -> standardisierte kompakte code-map.md
  -> Implementierung oder begründeter No-op
  -> get_violations + relevante Tests + Build
  -> Review / Übergabe mit Evidenz und offenen Unsicherheiten
```

### Phase 1: Einordnung

Der Agent entscheidet vor der Recherche:

- Ist die Aufgabe rein textuell oder konfigurationsbezogen?
- Gibt es ein bekanntes C#-Symbol oder muss es lokalisiert werden?
- Berührt die Änderung mehrere Komponenten, Verträge, Tests oder Dokumentationspflichten?
- Ist ein No-op oder eine reine Diagnose plausibel?
- Gibt es bereits Änderungen eines anderen Agents im Working Tree?

Für einfache lokale Aufgaben ist keine künstliche semantische Recherche erforderlich. Die standardisierte `code-map.md` wird trotzdem mit den kurzen, tatsächlich bekannten Befunden und der Verifikation geführt.

### Phase 2: Gezielte Kontextaufnahme

Der Agent beginnt mit der kleinsten ausreichenden Abfrage. Für ein bekanntes Symbol ist `get_feature_context` der bevorzugte Einstieg. Bei unbekannten Symbolen oder unscharfen Anforderungen wird zunächst lokalisiert. Spezialabfragen werden nur auf Basis einer konkreten Lücke ausgeführt:

- fehlender Implementierungsinhalt: `get_symbol_body`,
- unklare Auswirkungen: `find_references`, `get_impact`, `dependency_graph`,
- unklare Testabdeckung: `get_test_context`,
- unklare Regelkonformität: `get_violations` oder passender Violation-Filter.

Die Recherche endet, wenn die Änderungshypothese, betroffene Abhängigkeiten, relevante Tests und wesentliche Risiken hinreichend belegt sind. Es gibt keine fixe Mindestzahl von Tools und keine pauschale Voll-Repository-Kartierung.

Die Abschlussentscheidung trifft der Implementierer anhand dieser Evidenz. Der Orchestrator erzwingt nur die Phasenfolge und die Verifikation, nicht eine künstliche Zahl von MCP-Aufrufen.

### Phase 3: Standardisiertes Arbeitsgedächtnis

Der Agent führt in jedem Task-Durchlauf eine kompakte `code-map.md` im Task-Verzeichnis. Sie enthält ausschließlich verifizierte, für die laufende Änderung relevante Informationen. Auch bei kleinen Aufgaben bleiben die Überschriften erhalten; nicht relevante Bereiche werden knapp als nicht betroffen oder nicht geprüft gekennzeichnet:

```markdown
## Primäre Einstiegspunkte
## Betroffene Dateien und Symbole
## Aufrufer und Abhängigkeiten
## Relevante Tests, Konfiguration und Dokumentation
## Invarianten, Risiken und Unsicherheiten
## Verifikation
```

Regeln für die Map:

- keine vollständigen Tool-Rohantworten oder Transkripte,
- jede wesentliche Aussage auf Datei/Symbol/Toolbefund zurückführen,
- Trunkierung und Unsicherheit sichtbar markieren,
- nach relevanten Änderungen aktualisieren; bei einem neuen Ausgangszustand veraltete Befunde markieren,
- immer dieselbe Grundstruktur verwenden, damit Prompt, Resume und Übergabe keinen Aktivierungszweig benötigen,
- niemals als alleinige Wahrheit verwenden; vor Änderung gegen Working Tree und MCP prüfen.

### Phase 4: Implementierung und Fallback

Der Agent implementiert erst nach einer nachvollziehbaren Änderungshypothese. Falls MCP nicht verfügbar, unvollständig oder widersprüchlich ist, nutzt er die zulässigen lokalen Fallbacks (`rg`, Dateien, Build/Test) und dokumentiert die Einschränkung. Der Workflow darf nicht in einer endlosen Recherche-Schleife hängen.

Bei fehlender Evidenz wird die Änderung eingegrenzt, als Diagnose beendet oder als Rückfrage eskaliert. Bei einer No-op-Aufgabe wird keine Scheindatei und kein unnötiger Code erzeugt.

### Phase 5: Verifikation

Nach der letzten Codeänderung wird der relevante MCP-Violation-Stand erneut geprüft. Danach folgen die für den Änderungsumfang passenden Tests und der Build. Für C#- oder Teständerungen gelten die Projektvorgaben für die vollständigen Nicht-Stress-Läufe; reine Konzept-/Agentendokumentation benötigt keine Produktbuilds.

Bei Refactorings werden zwei Fragen getrennt beantwortet:

1. Ist das beobachtbare Verhalten weiterhin korrekt?
2. Sind alle vom Auftrag erfassten Stellen, Referenzen, Tests und Dokumentationspflichten abgedeckt?

### Phase 6: Ownership, Resume und Übergabe

Ein Working Tree hat zu einem Zeitpunkt genau einen aktiven schreibenden Owner. Erkennt der Agent während der Arbeit fremde Änderungen oder einen neuen Ausgangszustand, pausiert er die betroffene Änderung, beschreibt die Abweichung und erstellt eine neue Kontext-Baseline. Er verwendet weder `git reset --hard` noch pauschales Zurücksetzen oder Überschreiben.

Bei Resume liest der Agent zuerst die aktuelle Konzept-/Map-Datei und prüft danach die veränderlichen Befunde erneut. Die Übergabe enthält Änderungshypothese, bearbeitete Dateien, Verifikation, offene Unsicherheiten und einen möglichen nächsten Schritt.

## Betroffene Artefakte und Änderungsumfang

Voraussichtlich anzupassen sind:

- `.agents/prompts/orchestrator.md`: Scout-Phase und Scout-Handoff entfernen; feste Phasenfolge, MCP-first-Kontextvertrag, standardisiertes Arbeitsgedächtnis, Fallback und Ownership-Regeln ergänzen.
- `.agents/skills/implement/SKILL.md`: `get_feature_context`/`find_symbol` innerhalb der MCP-Kontextphase beschreiben; `code-map.md` als konstant und nicht-autoritativ behandeln.
- `.agents/skills/review/SKILL.md`: Aktualität der Kontextnotizen und MCP-Verifikation prüfen; keine Scout-Ausgabe voraussetzen.
- `.agents/rules/AiNetLinter-McpWorkflow.mdc`: nur ergänzen, falls die konkreten Ziel-/Fallback-/Trunkierungsregeln dort noch nicht eindeutig genug sind.
- Task-lokale `code-map.md`: in jedem Task-Durchlauf mit konstanter Struktur führen; niemals als vollständige Repository-Karte ausbauen.

Ausdrücklich nicht vorgesehen:

- keine neue `.agents/skills/scout/SKILL.md`,
- kein zweiter Standard-Agent nur für Recherche,
- keine Änderung am MCP-Protokoll oder an Produkt-/CLI-Funktionalität,
- keine parallele Schreibarbeit mehrerer Rollen im selben Working Tree.

## Muss-Kriterien

1. Der Standardworkflow enthält keine verpflichtende Scout-Rolle und keinen Scout-Handoff.
2. C#-Kontext wird MCP-first ermittelt: unbekannte Symbole werden lokalisiert, bekannte Symbole über `get_feature_context` erkundet.
3. Projektgebundene MCP-Aufrufe verwenden den korrekten Projekttyp und einen absoluten Projektpfad.
4. Weitere Abfragen sind lückenorientiert und begrenzt; Trunkierung, Widersprüche und Unsicherheit bleiben sichtbar.
5. Jeder Task-Durchlauf führt eine kompakte, aktuelle und nicht-autoritative `code-map.md` mit konstanter Grundstruktur.
6. Es existieren nachvollziehbare lokale Fallbacks und ein Abbruchpfad gegen Recherche-Schleifen.
7. No-op-/Skip-Entscheidungen sind zulässig und erzeugen keine künstlichen Änderungen.
8. Nach Änderungen werden Violations, relevante Tests und — gemäß Projektregeln — der Build verifiziert.
9. Fremde Working-Tree-Änderungen werden geschützt; Ownership und Re-Baselining sind explizit.
10. Die Wirksamkeit wird über lokale Workflow-Metriken und Fehleranalysen bewertet, nicht über eine einzelne Benchmarkzahl.

## Nicht-Ziele

- keine Pflicht, jede Aufgabe durch ein vollständiges Repository-Mapping zu schicken,
- keine festen Tool- oder Tokenquoten unabhängig von der Aufgabe,
- keine Garantie, dass MCP-Kontext allein korrekte Implementierungen erzeugt,
- keine automatische Übernahme unbestätigter Map-Inhalte,
- keine Ausweitung auf parallele Agentenkoordination im selben Working Tree,
- keine Produktänderung an AiNetLinter selbst im Rahmen dieses Konzeptes.

## Betriebs- und Risikoperspektive

Der Workflow läuft im bestehenden lokalen Agenten-/Repository-Kontext. MCP-Abfragen sind lesende Kontextoperationen; Änderungen bleiben dem bestehenden Implementierungs- und Reviewprozess unterworfen.

Wesentliche Risiken und Gegenmaßnahmen:

- **Zu wenig Kontext:** gezielte Spezialabfrage, sichtbare Unsicherheit, Review und Tests.
- **Zu viel Kontext:** kleine Einstiegsabfrage, harte Ergebnisgrenzen, keine Rohtranskripte.
- **Stale `code-map.md`:** Aktualitätskennzeichnung und MCP-Recheck vor Änderungen.
- **MCP-Ausfall:** lokaler Fallback und dokumentierte Einschränkung.
- **Recherche-Schleife:** Abschlusskriterium, Zeit-/Umfangsgrenze und Eskalation.
- **Fremde Änderungen:** aktive Ownership, Pause und Re-Baselining; kein Reset/Overwrite.
- **Kaskadierende Agentenfehler:** ein verantwortlicher Implementierer und überprüfbare Übergabepunkte.
- **Unnötige Aktion bei Diagnose/No-op:** explizite Skip-Entscheidung.

## Geplante Verifikation des Workflows

Bei der Umsetzung und Abnahme soll der Workflow an repräsentativen Fällen geprüft werden:

1. **Einfache lokale Änderung:** keine Scout-Rolle, dieselbe minimale `code-map.md`, begrenzte Toolkette.
2. **Bekannte C#-Struktur:** `get_feature_context` liefert die Startbasis; nur echte Lücken lösen Folgeabfragen aus.
3. **Unbekannte C#-Struktur:** `find_symbol` führt zu einem fokussierten Feature-Kontext statt zu Vollscan.
4. **Mehrstufiges Refactoring:** Map enthält nur relevante Einstiegspunkte, Abhängigkeiten, Tests, Risiken und Verifikation.
5. **Unvollständige MCP-Antwort:** Trunkierung wird erkannt; der Agent vertieft, begrenzt oder eskaliert.
6. **MCP-Ausfall:** Fallback funktioniert, ohne falsche Sicherheit zu behaupten.
7. **No-op-Aufgabe:** Agent beendet ohne Scheincode oder künstliche Dokumentationsänderung.
8. **Fremde Working-Tree-Änderung:** Agent pausiert/re-baselined und verändert keine fremden Dateien.

Zu erfassende lokale Metriken:

- Zeit bis zur belastbaren Änderungshypothese,
- Anzahl und Typ der MCP-Aufrufe,
- geschätzter Kontext-/Tokenumfang,
- Anzahl der Fallbacks, Trunkierungen und widersprüchlichen Befunde,
- erzeugte/aktualisierte Maps und Map-Fehler,
- unnötige Änderungen bei No-op-Fällen,
- relevante P0/P1-Befunde und Testfehler,
- nachgelagerte Korrekturschleifen und Vollständigkeit komplexer Refactorings.

## Offene Punkte

Keine fachlich blockierenden Punkte. Detailentscheidungen zur konkreten Ausgestaltung einzelner MCP-Abfragen können innerhalb der festen Kontextphase getroffen werden.

## Verworfen / bewusst nicht festgeschrieben

- verpflichtender Scout-Subagent vor jedem Implementierer,
- pauschaler Parallel-Scout,
- feste Mindest-/Höchstzahl an Tools oder Tokens,
- vollständige Repository-Karte als Pflicht; die Standard-Map bleibt taskbezogen und kompakt,
- `code-map.md` als autoritative Quelle,
- Erfolgsmessung nur über einen externen Benchmark,
- Bearbeitung fremder Working-Tree-Änderungen durch Reset oder Überschreiben.
