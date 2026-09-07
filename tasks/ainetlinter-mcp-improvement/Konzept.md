---
status: draft
type: konzept
project_kind: brownfield
estimated_scope: large
rules_dir: .agents/rules
last_updated: 2026-09-07
open_questions:
  - Soll das konkretisierte Konzept jetzt ausdrücklich als `ready` freigegeben werden?
depends_on:
  - tasks/ainetlinter-mcp-usage-audit/Konzept.md
related_tasks:
  - tasks/ainetlinter-mcp-usage-audit
supersedes: null
---

# Konzept: AiNetLinter-MCP-Verbesserung

## Ziel

Die AiNetLinter-MCP-Oberfläche soll für Coding-Agenten verlässlich, Folge-Call-tauglich und tokenbewusst werden. Grundlage ist das read-only Audit unter `tasks/ainetlinter-mcp-usage-audit/`.

Das Audit bleibt Evidenzarchiv. Dieses Konzept bündelt wiederkehrende Root Causes, priorisiert den erwarteten Nutzen und bereitet verifizierbare Umsetzungspakete vor.

## Verbindliche Scope-Grenze

- `tasks/ainetlinter-mcp-usage-audit/` bleibt unverändert und read-only.
- Audit-Findings werden nicht gelöscht, umbenannt oder inhaltlich überschrieben.
- Es entstehen keine Einzel-Tasks pro Finding.
- Produktionsänderungen erfolgen erst in den priorisierten Umsetzungspaketen.
- Die bestehende Roslyn-/statische Analysegrenze bleibt erhalten.

## Priorisierte Umsetzungspakete

| Prio | Paket | Hauptziel | Ausgangsbefunde |
| --- | --- | --- | --- |
| 01 | Discovery Contract | Vertrauenswürdige Dateilandkarte, Schema-Entdeckung und C#/Nicht-C#-Fallbacks | `get_file_tree`, `tool-discovery`, `get_index_scope`, `search_pattern`, `combo-discovery-fallback` |
| 02 | Symbol IDs & Navigation | Kanonische, zwischen Folge-Tools nutzbare Symbol-IDs | `find_symbol`, `get_file_skeleton`, `get_symbol_body`, `get_class_structure`, Referenz-/Navigations-Tools, `combo-batch-ids`, `combo-cross-target` |
| 03 | Completeness & Paging | Ehrliche Vollständigkeit, Truncation und deterministische Fortsetzung | toolübergreifende Truncation-/Completeness-Befunde |
| 04 | Core Agent Workflows | Verlässlicher Kontext-, Impact-, Lint- und Test-Workflow | `get_feature_context`, `get_impact`, `get_violations`, `metrics_lookup`, `get_test_context`, Combo-Findings |
| 05 | Assembly & Cross-Target | Robuster Vertrag für externe DLL-/EXE-Snapshots | Assembly-Tools, `resolve_type_origin`, `combo-assembly`, `combo-cross-target` |
| 06 | Ranking, Scope & Precision | Relevante Treffer und weniger irreführende False Negatives/Positives | Caller-Ranking, Partial-Klassen, Glob-/Scope-, Metrik- und Pattern-Befunde |
| 07 | Schema & Documentation | JSON-Schema, Fehlertexte, Runtime-Instructions und Referenzdoku synchronisieren | `tool-discovery`, Resources, `Docs/agent-api.md`, README, MCP-Regeln |
| 08 | Keep & Defer | Funktionierendes schützen, Wünsche und geringe Risiken bewusst zurückstellen | `ok`-Befunde, kosmetische Reibung, unbelegte `wish`-Forderungen |

Die Pakete 01–06 enthalten jeweils ihre eigene Schema-/Dokumentations- und Testarbeit. Paket 07 ist nur für verbleibende, nicht paketgebundene Bereinigung vorgesehen. Alle Pakete gehören zu diesem einen Verbesserungs-Task; die Nummern beschreiben Abhängigkeiten und Liefer-Slices, keine getrennten Produktvorhaben.

## 360°-Bewertung aus Agentensicht

### 1. Erreichbarkeit und Routing

Der Agent muss zuerst wissen, welches Tool für Projektstruktur, C#-Symbole, Nicht-C#-Dateien, Assemblys und Betriebsstatus zuständig ist. Falsche Dateibäume, unvollständige Tool-Kataloge oder fehlende Fallback-Hints sind deshalb höher zu bewerten als ein einzelner Spezialfehler.

### 2. Semantisches Vertrauen

Eine formal erfolgreiche Antwort ist gefährlich, wenn sie falsche Elternbeziehungen, falsche Scopes, Testtreffer statt Produktionsnutzer, widersprüchliche Lint-/Metrik-Werte oder „keine Treffer“ trotz unvollständiger Analyse zeigt. Die Definition von „leer“, „vollständig“, „nicht unterstützt“ und „teilweise“ ist ein Produktvertrag, kein reiner Formatter-Aspekt.

### 3. Folge-Call und Identität

Der zentrale Agentenwert ist nicht die erste Antwort, sondern die Fähigkeit, daraus den nächsten Call ohne Raten zu bauen. Jede relevante Ausgabe braucht daher entweder eine kopierbare kanonische ID oder einen expliziten Hinweis, warum kein Handoff möglich ist. Projekt- und Assembly-Identitäten dürfen nicht stillschweigend austauschbar sein.

### 4. Token- und Latenzbudget

Große Antworten sind nicht automatisch besser. Defaults müssen die fachlich relevanten Treffer priorisieren, Diagnose-/Referenzrauschen begrenzen und eine sichere Fortsetzung anbieten. Composite-Tools dürfen keine mehrfachen Vollberichte erzeugen, wenn ein gezielter Drilldown genügt.

### 5. Projekt- und Assembly-Semantik

Eine dekompilierte Assembly ist ein statischer Snapshot ohne Consumer-Projekt und ohne Laufzeitbeweis. Diese Grenze ist akzeptabel, muss aber in Herkunft, `partial`, `not_decidable`, BCL-/Referenzfiltern und Capability-Fehlern sichtbar sein. Projekt-Tools dürfen nicht so tun, als hätten sie Assembly-Parität, wenn sie sie nicht besitzen.

### 6. Betriebs- und Lebenszeitsemantik

Health, Overview, Rules, Bootstrap, Daemon/stdio und Session-Generationen müssen einen konsistenten Betriebszustand beschreiben. Ein Agent darf aus einer Pfadbestätigung nicht auf „ready“ schließen und darf einen Bootstrap mit potenziellen Schreibschritten nicht ungefragt starten.

### 7. Sicherheits- und Schadensgrenze

Die Analyse bleibt read-only und führt Assemblys nicht aus. Besonders gefährlich sind nicht nur technische Crashes, sondern Agentenentscheidungen wie falsches Löschen von angeblich totem Code, falsche Lint-Entwarnung, falsche Produktions-Impact-Annahmen oder ungefragte Projektintegration. Diese Fälle brauchen konservative Beschreibungen und explizite Confidence-/Scope-Hinweise.

### 8. Wartbarkeit und Vertragsdrift

Schema, Registrierung, Formatter, StructuredContent, Runtime-Instructions, `Docs/agent-api.md` und Tests müssen aus derselben Verhaltensentscheidung aktualisiert werden. Ein einzelner Fix im Formatter ohne Vertrags- und Regressionstest würde die beobachtete Drift nur verschieben.

## Agententaugliche Begrenzung großer Ergebnismengen

Ein klassisches „Seite 4 von 10“-Modell ist nicht das primäre UX-Ziel. Ein Agent wird bei zehn Seiten mit je zehn Einträgen meist nicht sinnvoll durchblättern. Der Server muss deshalb zwischen **Begrenzung** und **Navigation** unterscheiden:

- **Bounded default:** Jeder Standardcall liefert eine kleine, fachlich gerankte und für die nächste Entscheidung ausreichende Antwort.
- **Ehrlicher Status:** Die Antwort nennt `totalCount`, `returnedCount`, `completeness` und `truncatedBy`. Ein begrenzter Ausschnitt darf niemals als globale Negativaussage oder als vollständig geprüft erscheinen.
- **Gezielter Drilldown:** Der bevorzugte nächste Schritt ist ein engerer Scope, eine Richtung, ein Symbolkind, ein Detaillevel oder eine Zusammenfassung – nicht das blinde Laden der nächsten Seite.
- **Continuation nur als Werkzeug:** Wo eine geordnete Restmenge fachlich sinnvoll ist, gibt es einen opaken `continuationToken` für einen weiteren begrenzten Ausschnitt. Der Agent muss damit nicht die gesamte Datenmenge konsumieren; der Token ist ein gezielter Drilldown, kein UI-Seitenbrowser.
- **Übergroße offene Abfragen:** Wenn ohne Filter keine sinnvolle Top-Menge bestimmbar ist, soll das Tool eine kompakte Zusammenfassung oder `requiresScope`/einen vergleichbaren maschinenlesbaren Hinweis liefern. Es darf nicht willkürlich die ersten zehn Treffer als repräsentativ verkaufen.
- **Gemeinsames Prinzip, nicht zwangsläufig identische Parameter:** Alle Tools teilen die Statussemantik und das Fortsetzungsprinzip. Die konkrete Filter- und Drilldown-Sprache darf fachlich je Tool verschieden bleiben.

Damit wird das eigentliche Problem adressiert: nicht „wie kann der Agent Millionen Treffer lesen?“, sondern „wie bekommt er eine belastbare nächste Entscheidung, ohne dass der Kontext flutet oder ein Ausschnitt als Wahrheit missverstanden wird?“

## Entscheidungsmodell für Findings

Jeder Befund erhält in der Synthese genau eine Disposition:

- `fix` — reproduzierbarer Produktfehler oder systemische Agenten-Reibung
- `document` — Verhalten ist absichtlich oder technisch unvermeidbar, aber unklar beschrieben
- `defer` — sinnvoll, aber nachgelagert oder abhängig von einem früheren Vertrag
- `keep` — funktioniert ausreichend und erhält Regressionstests
- `discard` — nicht belastbar, redundant oder außerhalb des Produktziels; Begründung erforderlich

Severity und Priorität werden getrennt behandelt. Ein `friction`-Befund kann wegen seiner Wirkung auf den ersten Agenten-Call hoch priorisiert werden; ein `wish`-Befund ist nicht automatisch umzusetzen.

## Arbeitsablauf der Synthese

Die Ausarbeitung erfolgt in fünf Planungsrunden, bevor Produktionscode geändert wird:

1. **Inventar und Normalisierung:** Alle Audit-Dateien werden read-only gelesen. Pro Finding werden primäre Schwere, Verdict, Nutzbarkeit, konkrete Evidenz, vermutete Root Cause und betroffene Umsetzungspakete extrahiert. Nicht-kanonische Angaben wie `Mittel` oder fehlende Primär-Schwere werden markiert, nicht stillschweigend umgedeutet.
2. **Deduplizierung und Clustering:** Einzelbefunde und Combos werden nicht addiert, wenn sie dieselbe Ursache belegen. Ein Root Cause erscheint einmal als Problem, die übrigen Findings bleiben als Evidenzverweise erhalten.
3. **Nutzen-/Risiko-Bewertung:** Jede Problemgruppe wird nach Agentenwirkung, Reichweite, Risiko falscher Schlussfolgerungen, Abhängigkeiten und Umsetzungsaufwand bewertet. Severity bleibt dabei ein Befundmerkmal und wird nicht zur alleinigen Prioritätsformel.
4. **Paketverträge:** Für jedes `fix`-Paket werden Scope, Non-Goals, betroffene Symbole/Dateien, API-Vertrag, Regressionstests, Dokumentationsänderungen und Abnahmekriterien festgelegt. `keep`, `document`, `defer` und `discard` werden begründet.
5. **Nutzerfreigabe:** Erst wenn Matrix, Prioritäten und Paketgrenzen konsistent sind, wird dieses Konzept ausdrücklich auf `ready` gesetzt. Danach entscheidet der Nutzer, ob Paket 01 umgesetzt oder zunächst ein anderes Paket gestartet wird.

Das dauerhafte Synthese-Artefakt ist eine Befundmatrix im neuen Verbesserungs-Task. Sie enthält mindestens `Finding`, `Root Cause`, `Disposition`, `Paket`, `Priorität`, `Nutzen`, `Risiko`, `Evidence`, `Quellbereich`, `Testbedarf` und `Doku-Bedarf`.

## Entscheidungs- und Übergabegates

- **Gate A – Evidenz:** Jede Aussage in der Synthese verweist auf mindestens ein Audit-Finding; das Audit selbst bleibt unverändert.
- **Gate B – Root Cause:** Kein Umsetzungspaket enthält nur Symptome oder doppelte Befunde ohne gemeinsame Ursache.
- **Gate C – Priorität:** Die Reihenfolge erklärt sich aus Agentennutzen und Abhängigkeiten, nicht aus der Dateireihenfolge.
- **Gate D – Umsetzung:** Kein Paket startet ohne überprüfbare Akzeptanzkriterien und passende Verifikation.
- **Gate E – Abschluss:** Nach der Umsetzung gelten Build, Nicht-Stress-Testläufe, MCP-Nachweise und der passende Audit gemäß `AGENTS.md`.

## Getroffene Produktentscheidungen

Die folgenden Entscheidungen stammen aus dem Sparring und gelten für dieses Konzept:

1. **Keine Migrations- oder Kompatibilitätslast:** Die MCP-Verträge dürfen für den sauberen Zielzustand geändert werden. Es muss keine alte ID- oder Response-Form parallel gepflegt werden. Die Umsetzung muss trotzdem einen konsistenten Endzustand herstellen; halbe Übergangsformen sind kein Ziel.
2. **Projekt und Assembly gehören in denselben Task:** Assembly ist kein späteres separates Produktvorhaben. Die Umsetzung darf aber in abhängigen Slices erfolgen: gemeinsame Verträge zuerst, danach Projekt- und Assembly-Adapter bzw. gezielte Parität.
3. **Paging ist kein Seitenbrowser:** Es gilt der oben beschriebene bounded-/drilldown-orientierte Vertrag. Ein Cursor ist nur dort Pflicht, wo eine geordnete Restmenge tatsächlich sinnvoll fortgesetzt werden kann; 10 kleine Seiten als Standard-UX sind ausdrücklich nicht das Ziel.
4. **Text und StructuredContent:** Beide werden fachlich gleichwertig aus derselben typisierten Ergebnisaggregation erzeugt, soweit das im jeweiligen Kontext sinnvoll ist. Keine kritische Folgeinformation darf nur in einem für den Agenten unsichtbaren Kanal liegen.
5. **Harte Agenten-Abnahme:** „Kein manuelles ID-Raten“ und „kein falsches Grün bei `complete`/leer“ sind Release-Kriterien, nicht bloße Wünsche.

## Heuristik-Scope für die erste Lieferung

Die erste Lieferung soll keine vollständige Neuentwicklung aller Heuristiken erzwingen. Sie muss aber alle beobachteten Heuristiken entschärfen, die zu einer schädlichen Agentenentscheidung führen können:

### Muss in die erste Lieferung

- Produktions-/Test-Ranking und `scopeType=production` müssen tatsächlich den versprochenen Scope liefern.
- `get_impact`, `get_feature_context`, `get_violations` und `metrics_lookup` dürfen für denselben Anker keine widersprüchliche Vollständigkeit oder Violation-Wahrheit ausgeben.
- `safeguard` muss scoped Top-Befunde tatsächlich auf den Scope begrenzen.
- `pattern_detect` muss leere Kategorien sichtbar bzw. als geprüft/keine Treffer kennzeichnen; ein Default darf nicht fünf Kategorien stillschweigend grün erscheinen lassen.
- `find_dead_code` muss Confidence, Reflection/DI-/Routing-Grenzen und „Kandidat, nicht Löschauftrag“ so ausgeben, dass kein direktes Löschen nahegelegt wird.
- `find_magic_values` muss Zahlen-/String-/Security-Kategorien fachlich korrekt trennen; irreführende Security-Labels sind zu entfernen oder zu begrenzen.
- `find_duplicates` muss Scope und Kandidatenstatus klar machen und Test-/Artefaktfluten verhindern.

### Nachgelagert, sofern kein Release-Kriterium betroffen

- neue oder feinere Ranking-Algorithmen ohne reproduzierten Agentenschaden;
- vollständige Recall-Verbesserungen für Reflection, Source Generators oder dynamische DI;
- kosmetische Tabellen-, Nummerierungs- oder Formatierungsverbesserungen;
- zusätzliche Komfort-Wishes, die weder falsche Entscheidungen noch unnötige Roundtrips verursachen.

Die gefährlichen Präzisionsfixes bleiben damit im Scope dieses Gesamt-Tasks, werden aber nach den Vertragsgrundlagen umgesetzt und nicht zum Vorwand für eine unendliche Heuristik-Neuentwicklung.

## Entscheidungen, die vor der Umsetzung benötigt werden

Die fachlichen Entscheidungen sind damit getroffen. Vor der Umsetzung bleibt nur die formale Freigabe dieses Drafts als `ready`.

## Nächster Planungsschritt

Die Befundmatrix ist aufgebaut. Nach der Freigabe startet die Umsetzung als ein Gesamt-Task in abhängigen Slices: zuerst Discovery/Vertragsgrundlagen, dann IDs und bounded Completeness, anschließend die Core-Workflows und Assembly-Pfade; die gefährlichen Heuristikfixes folgen innerhalb desselben Gesamtumfangs.

## Muss-Kriterien

- Jeder nicht-triviale Audit-Befund ist genau einer Problemgruppe oder `keep/defer/discard` zugeordnet.
- Wiederkehrende Root Causes werden nur einmal als Umsetzungsthema geführt.
- Positive Befunde werden als kompakte Baseline erhalten, damit funktionierende Verträge nicht regressieren.
- Jedes Umsetzungspaket benennt betroffene Quellbereiche, API-Verträge, Tests, Dokumentation und Abhängigkeiten.
- Die finale AiNetLinter-Verifikation folgt den Repository-Gates aus `AGENTS.md`.

## Vorläufige Akzeptanzkriterien

- Ein Agent kann vom Discovery-Call bis zum relevanten Folge-Call ohne manuelles ID-Raten navigieren.
- Antworten unterscheiden belastbar zwischen leer, vollständig, abgeschnitten, nicht unterstützt und fehlerhaft.
- Truncation liefert entweder einen brauchbaren Fortsetzungsmechanismus oder eine klare, überprüfbare Begrenzung.
- Änderungen an MCP-Verträgen sind in Schema, Runtime-Instructions und `Docs/agent-api.md` konsistent.
- Die als `keep` markierten Happy Paths bleiben durch gezielte Tests abgesichert.

## Geplante Verifikation

- pro Paket: gezielte Fast-/Integration-/MCP-Tests entsprechend dem Risiko;
- nach dem Gesamtumfang: `dotnet build` sowie beide vollständigen Nicht-Stress-Testläufe gemäß `AGENTS.md`;
- nach größeren Änderungen: passender MCP-/Safeguard-Nachweis und abschließender Audit-Skill;
- Dokuprüfung gegen den aktuellen Code, nicht gegen alte Audittexte.

## Arbeitsgedächtnis (nur Draft)

- Das Audit-Konzept ist `status: ready` und bleibt unverändert.
- Die neue Struktur ist als Folge-Task angelegt; noch keine produktive Änderung.
- Die vorläufige Reihenfolge priorisiert zuerst Vertrauenswürdigkeit und Folge-Call-Verträge, danach Vollständigkeit und fachliche Workflows.
- Findings sind normalisiert und auf die Problemgruppen abgebildet.
- Die Befundmatrix ersetzt keine Umsetzung und ändert das read-only Audit nicht.
- Die Synthese wird iterativ mit dem Nutzer abgestimmt; das Konzept bleibt bis zur ausdrücklichen Freigabe `draft`.
- Die 45/45-Befunde sind in `Befundmatrix.md` genau einmal einer primären Problemgruppe und Disposition zugeordnet.
- Die Produktentscheidungen zu Kompatibilität, Targets, bounded Paging, Antwortkanälen, Abnahmeschwelle und Heuristik-Scope sind im Draft eingearbeitet.
- Die einzige verbleibende Blockade ist die ausdrückliche Freigabe des Konzepts als `ready`.
