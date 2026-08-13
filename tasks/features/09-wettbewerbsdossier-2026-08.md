---
type: dossier
erstellt: 2026-08-12
zweck: "Kritische Standortbestimmung von AiNetLinter im Wettbewerbsvergleich, inkl. Verifikation des eigenen wissenschaftlichen Differenzierungs-Claims"
praemisse: "find_magic_values (tasks/magic-values-in-mcp/) und validate_file (tasks/validate-file/) werden fuer diese Bewertung als bereits umgesetzt unterstellt (Stand: beide Konzepte fertig, noch nicht implementiert)"
scope: "Roslyn/C#-MCP-Server + generische Code-Intelligence-MCP-Server UND die etablierte, nicht-MCP-basierte C#-Analyzer-Konkurrenz (SonarQube/SonarLint, Roslynator, StyleCop.Analyzers, Meziantou.Analyzer, NDepend) — AiNetLinter ist laut README primaer ein CLI-Linter, der MCP-Server ist der zweite, gleichrangige Modus. Cloud-PR-Review-Bots (CodeRabbit, Qodo, Greptile, Codacy, DeepSource) bewusst ausgeklammert (andere Kategorie/Layer, explizite Nutzer-Entscheidung dieser Runde)."
correction: "Erste Fassung fokussierte zu stark auf die zwei unterstellten Features (validate_file/find_magic_values) und die MCP-Ebene, liess die CLI-/Analyzer-Ebene aus — auf Nutzer-Hinweis nachgezogen (Abschnitt 2)."
---

# Wettbewerbsdossier: Wo steht AiNetLinter? (Stand 2026-08-12)

## Executive Summary

AiNetLinter steht in seiner engeren Nische — Roslyn-basierte MCP-Server für C#/.NET-Coding-Agenten — **inhaltlich vorn, aber nicht so eindeutig, wie das Projekt selbst glaubt**. Die tatsächliche Stärke ist nicht der in `Docs/rationale.md` behauptete "jede Regel wissenschaftlich fundiert"-Claim (der einer Stichproben-Verifikation nur teilweise standhält, siehe unten), sondern die schiere **Regeltiefe** (46+ produktive Lint-Regeln, Epics 1–33 in `Docs/ROADMAP.md`), die **Token-Disziplin im MCP-Design** (`McpTruncation` durchgängig, wo die Konkurrenz laut eigener Marktrecherche "vertraut, dass der Agent filtert") und eine **belegte Dogfooding-Praxis** (zwei dokumentierte Bug-Fixes aus echten Cross-Project-Sessions, nicht nur Behauptung).

Die zwei zur Bewertung unterstellten Features stehen sehr unterschiedlich da: `find_magic_values` ist nach heutigem Stand eine **echte, unbesetzte Marktlücke** — kein geprüfter Konkurrent exponiert Magic-Value-/Secret-Klassifizierung als MCP-Tool. `validate_file` ist **enger differenziert als der Konzept-Entwurf annahm** — zwei Konkurrenten decken Teile der Idee bereits ab, einer davon (`roslyn-codelens-mcp`) wurde am Tag dieser Recherche selbst aktualisiert. Der Markt konsolidiert sich nicht, er wächst weiter — bei gleichzeitig hoher Sterberate unter den Einzelkämpfer-Projekten.

**Wichtiger als beide Einzelfeatures ist aber die Gesamteinordnung von AiNetLinter als Produkt** (die erste Fassung dieses Dossiers hatte sich zu sehr auf die zwei Features verengt — siehe `correction` im Frontmatter): AiNetLinter ist laut eigenem README primär ein **CLI-Linter** (46+ Regeln), der MCP-Server ist der zweite Modus, nicht der erste. Gegen die etablierte Analyzer-Konkurrenz (SonarQube/SonarLint: 380+ C#-Regeln, Roslynator: 500+ Analyzer, StyleCop.Analyzers, Meziantou.Analyzer, NDepend) ist AiNetLinters **"AI-Readability"-Framing tatsächlich unbesetzt** — kein geprüftes Konkurrenzprodukt bewirbt ein eigenständiges, für LLM-Coding-Agenten optimiertes Regel-Framework in vergleichbarer Form. Das ist der ehrlich stärkste Differenzierungs-Claim des Projekts, stärker als der wissenschaftliche Zitat-Claim. Gleichzeitig steht dem eine nüchterne Realität gegenüber: AiNetLinter hat aktuell **praktisch keine messbare Marktdurchdringung** — selbst das kleinste der geprüften Konkurrenztools (Meziantou.Analyzer) hat über tausendmal mehr Downloads.

## Methodik & Prämissen

- **Basis:** `tasks/features/03-market-research.md` (2026-08-06, 10 benannte Roslyn/C#-MCP-Konkurrenten + breitere Landschaft), heute punktuell nachverifiziert statt komplett neu recherchiert (6 Tage Altersunterschied, siehe Nutzer-Entscheidung dieser Runde) — ergänzt um eine zweite Recherche-Runde zur etablierten, nicht-MCP-basierten Analyzer-Konkurrenz (Abschnitt 2), nachdem die erste Fassung dieses Dossiers sich zu eng auf die MCP-Ebene und die zwei unterstellten Features verengt hatte.
- **Scope-Grenze (explizite Nutzer-Entscheidung):** zwei Ebenen — Roslyn/C#-MCP-Server + generische Code-Intelligence-MCP-Server (Abschnitt 1) UND etablierte C#-Analyzer/Linter unabhängig von MCP (Abschnitt 2). **Nicht** verglichen: Cloud-PR-Review-Bots (CodeRabbit, Qodo, Greptile, Codacy, DeepSource) — andere Kategorie (Cloud, PR-Zeitpunkt, oft sprachübergreifend), kein Äpfel-Birnen-Vergleich hier.
- **Unterstellung:** `find_magic_values` und `validate_file` (beide Konzepte fertig, `status: draft`, noch nicht implementiert) werden für diese Bewertung als **umgesetzt** behandelt — Ziel ist "wo stünden wir, wenn beide live sind", nicht "wo stehen wir heute wörtlich".
- **Maßstab:** AiNetLinters eigenes Leitbild aus `Docs/rationale.md` und `tasks/features/05-roadmap.md` §0 — ein deterministischer, lokaler, C#-nativer Verifikations-Layer für agentisches Coden. **Nicht** bewertet: fehlende Mehrsprachigkeit, fehlende Cloud-/SaaS-Features — das sind bewusste Nicht-Ziele, keine Lücken (Analogie des Auftraggebers: "wir wollen keinen Assembler-Linter machen").
- **Zitat-Verifikation:** stichprobenartig 8 von 13 Referenzen aus `Docs/rationale.md` per Websuche/Volltext-Abruf geprüft (nicht alle 13 — Ressourcen-Fokus auf die auffälligsten/neuesten Quellen).

---

## 1. Wo AiNetLinter tatsächlich steht — quantitativ

| Projekt | Tools | Aktivität (Stand 2026-08-12) | Transport | Eigene Lint-Regeln jenseits Navigation/Refactoring | Magic-Value/Secret als MCP-Tool | Kompakte Datei-Validierung |
|---|---|---|---|---|---|---|
| **AiNetLinter (mit validate_file + find_magic_values)** | 20 | **aktiv, taeglich** (dieses Repo) | stdio | **46+ Regeln** (Epics 1-33), inkl. Immutability/Complexity/Coupling/Suppression-System | **Ja** (geplant, echte Luecke) | **Ja** (geplant, Git-Diff-Batch + nextSteps) |
| sharplens-mcp | 67 | aktiv (Push 2026-08-08) | stdio | keine dedizierten Regeln (Navigation/Refactoring/Analysis) | Nein | Teilweise (`validate_code` = nur Compile-Check) |
| **roslyn-codelens-mcp** (neu) | ~70 | **aktiv, Push heute (2026-08-12)** | stdio | `get_project_health` (7-Dimensionen-Audit, aehnlich `safeguard`), `find_god_objects`, `check_architecture` | Nein | **Ja, aber ohne nextSteps/Batch** (`get_diagnostics`) |
| BifrostMCP (ex-CSharpLangMCPServer) | k.A. | aktiv, 223⭐ | stdio | k.A. | Nein | k.A. |
| RoslynMcpServer (JoshuaRamirez) | 41 | **inaktiv seit 2026-03** | stdio | keine, aber Preview+Rollback fuer Refactorings | Nein | Nein |
| sailro/RoslynMcpExtension | 7 | aktiv, VS-Extension | HTTP/SSE | keine | Nein | **Ja** (`roslyn_validate_file`, aber kein Batch/nextSteps, VS-only) |
| dotnet-roslyn-mcp (vs-ide-mcp) | 18+ | **inaktiv seit 2025-11** | stdio | keine | Nein | Teilweise (`get_diagnostics`, getrennt von Fixes) |
| SonarQube MCP (offiziell) | k.A. | aktiv, **Push heute**, 621⭐ | stdio | Ja (SonarQube-Regelwerk), aber **C# nicht in Secrets-Tool-Sprachliste** | Teilweise (nicht fuer C#) | Nein (serverseitiger Scan, kein On-Demand-Call) |
| egorpavlikhin/roslyn-mcp | 2 | inaktiv seit 2025-05 | stdio | keine | Nein | Nein |
| carquiza/RoslynMCP | ~5 | inaktiv seit 2025-06 | stdio | keine | Nein | Nein |

**Einordnung der Tabelle:** Rohe Tool-Zahlen sind irreführend, wenn man sie isoliert liest — sharplens-mcp und roslyn-codelens-mcp haben 3-4× mehr Tools als AiNetLinter, aber praktisch keine eigene Regel-Substanz; sie sind Navigations-/Refactoring-Suiten, keine Qualitäts-Gatekeeper. AiNetLinters 46+ Regeln sind der eigentliche, schwer kopierbare Asset — kein Konkurrenzprodukt bringt eine vergleichbar tiefe, konfigurierbare Regelbasis mit.

**Wichtiger Kontrapunkt zur eigenen Position:** Die Hälfte der ursprünglich 10 gelisteten Konkurrenten ist inzwischen inaktiv/verwaist (egorpavlikhin, carquiza, JoshuaRamirez/RoslynMcpServer, dotnet-roslyn-mcp). Das ist selbst ein Marktsignal — Solo-MCP-Server in dieser Nische sterben schnell. AiNetLinters durchgehende Aktivität (tägliche Commits, dokumentierte Dogfooding-Bugfixes) ist damit selbst ein Differenzierer, unabhängig von Feature-Vergleichen.

---

## 2. Die andere Hälfte: AiNetLinter als Analyzer (CLI-Ökosystem-Vergleich)

Die erste Fassung dieses Dossiers hat diesen Vergleich ausgelassen, obwohl er eigentlich der zentralere ist — AiNetLinter ist zuerst ein Linter, dann ein MCP-Server (README: *"AiNetLinter ist ein .NET 10 CLI-Tool, das C#-Code per Roslyn-Syntaxanalyse gegen konfigurierbare Qualitätsregeln prüft"*, MCP-Server-Modus als zweiter, gleichrangiger Modus).

| Tool | C#-Regeln (ca.) | Lizenz | Aktivität | Auto-Fix | "AI-Agent"-Framing | Adoption (NuGet-Downloads, Proxy) |
|---|---|---|---|---|---|---|
| **AiNetLinter** | 46+ | MIT/OSS (GitHub) | aktiv, täglich | Ja (`--fix`, triviale Regeln: sealed/readonly/nullable) | **Eigenständiges "AI-Readability"-Regel-Framework** — nach dieser Recherche ohne direktes Gegenstück | **~0** (kein NuGet-Release, keine nennenswerten Stars) |
| SonarQube/SonarLint (`sonar-dotnet`) | **380+** | Community Build kostenlos (self-hosted); Paid Tiers $2,5k–$100k/Jahr | sehr aktiv, kommerziell getragen | Teilweise (Quick Fixes) | "AI Code Assurance" — **prüft AI-generierten Output strenger**, ist nicht für AI-Konsum optimiert (anderes Konzept) | ~103 Mio. |
| Roslynator | **500+** Analyzer/Refactorings/Fixes | MIT, komplett kostenlos | sehr aktiv (Release 2026-08-08, 4 Tage vor diesem Dossier) | Ja (`roslynator fix`, solutionweit) | Keins | ~55,5 Mio. |
| StyleCop.Analyzers | Formatierung/Style, keine Architektur-/Komplexitätsregeln | Apache 2.0 | Haupt-Maintenance schleppend, Community-Fork existiert | Ja (Formatierung) | Keins | **~263 Mio.** |
| Meziantou.Analyzer | ~100+ | MIT | aktiv | Ja | *"helping developers **and AI** write more reliable code"* — nächstliegende bestehende Positionierung, aber Marketing-Zusatz, kein eigenes Rule-Framework | ~12,1 Mio. / 1,2k⭐ |
| NDepend (CQLinq) | Metrik-/Architektur-Queries statt klassischer Regelzahl | Kommerziell, ab ~$1.000/Jahr | aktiv | Nein (Analyse-/Trend-Tool) | Keins im Rule-Sinn, aber **eigener MCP-Server seit 2026** (`ndepend/NDepend.MCP.Server`) — bisher nicht in der MCP-Konkurrenzliste (Abschnitt 1) erfasst, gehört dort ergänzt | k.A. (kommerziell, keine NuGet-Metrik) |

**Drei Kernbefunde:**

1. **Der "AI-Readability"-Claim hält als Positionierung stand**, auch nach gezielter Suche nach direkten Gegenkonzepten. Das ist wichtiger als der einzelne Zitat-Befund in Abschnitt 4.1 — die *Idee*, ein Regelwerk für LLM-Agenten statt für menschliche Reviewer zu designen, ist am Markt (Stand heute) unbesetzt. Nur JetBrains Rider 2026.2 ("Quality Check Hooks for Claude Code") bewegt sich in eine ähnliche Richtung, aber als IDE-Hook, nicht als portables Regelwerk.
2. **NDepend hat 2026 einen eigenen MCP-Server veröffentlicht** — ein in der ursprünglichen MCP-Konkurrenzrecherche (`03-market-research.md`, Abschnitt 1 dieses Dossiers) fehlender Eintrag. Fokus liegt auf NDepends Architektur-/Metrik-Stärken (Dependency-Matrizen, Tech-Debt-in-Tagen), nicht auf einem AI-Readability-Regelwerk — überschneidet sich also eher mit AiNetLinters `dependency_graph`/`metrics_tree` als mit dem Kern-Linting.
3. **Die Adoptions-Realität relativiert alle bisherigen Stärke-Aussagen.** Technische Differenzierung und Marktposition sind zwei verschiedene Achsen. Selbst Meziantou.Analyzer — das kleinste der fünf etablierten Tools — hat eine drei- bis vierstellige Vielfache an Verbreitung gegenüber AiNetLinter. "Wo stehen wir" muss ehrlich zwischen *"technisch differenziert"* und *"am Markt angekommen"* unterscheiden — Ersteres trifft zu, Zweiteres (noch) nicht.

---

## 3. Stärken (evidenzbasiert)

1. **Regeltiefe ist unerreicht in der Nische.** Kein geprüfter Konkurrent bringt eine vergleichbare, konfigurierbare Lint-Regelbasis (Immutability, Kopplungsgrenzen, Suppression-System mit Compound-Suppression/Severity-Override, Web-Asset-Linting für Razor/CSS/JS) mit. Die Konkurrenz ist fast ausschließlich Navigation/Refactoring, keine Qualitäts-Policy.
2. **Token-Budget-Design ist durchgängig, nicht nachträglich.** `tasks/features/03-market-research.md` §1.3 stellt fest: "Keiner [der Konkurrenten] hat ein klares Token-Budget-Design." AiNetLinters `McpTruncation` mit einheitlicher Meta-Zeile zieht sich konsistent durch praktisch alle 18(+2) Tools — verifizierbar im Code, keine Behauptung.
3. **Belegte Dogfooding-Disziplin.** `Docs/ROADMAP.md` dokumentiert zwei konkrete Bugfixes aus echten Cross-Project-Sessions (Restore-Erkennung, Symbolgraph-Positionsauflösung) mit Root-Cause-Analyse und Regressionstests — nicht nur Selbstlint auf dem eigenen Repo. Das ist in der geprüften Konkurrenzliste nirgends in vergleichbarer Tiefe dokumentiert.
4. **Deterministisches Quality-Contract-Pattern (`safeguard`).** Reproduzierbarer 0–10-Score mit Schwellenwert-Gate — bewusst ohne LLM-Bewertung. `roslyn-codelens-mcp`s `get_project_health` zeigt, dass die Idee "Composite-Qualitäts-Score als Tool" richtig liegt (unabhängige Konvergenz zweier Projekte), aber AiNetLinter war zuerst und hat den Determinismus-Bug bereits selbst gefunden und gefixt (siehe Memory `project-mcp-dogfood-2026-08-10`).
5. **`find_magic_values` bleibt nach heutiger Prüfung eine echte Lücke.** Weder Roslyn-MCP-Konkurrenz noch die offizielle SonarQube-MCP-Integration (die für C# explizit keine Secrets-Erkennung anbietet) exponieren eine fachliche Magic-Value/Secret-Klassifizierung als abrufbares Tool.
6. **Die "AI-Readability"-Positionierung selbst ist unbesetzt** (Abschnitt 2) — auch gegen die etablierte, viel größere Analyzer-Konkurrenz (SonarQube 380+ Regeln, Roslynator 500+) gibt es kein Tool, das sein Regelwerk explizit für LLM-Coding-Agenten statt menschliche Reviewer designt. Das ist der am robustesten belegte Differenzierungs-Claim des gesamten Projekts.

## 4. Schwächen — ehrlich benannt

### 4.1 Der zentrale wissenschaftliche Claim hält einer Prüfung nur teilweise stand

`Docs/rationale.md` behauptet: *"Die Regeln sind keine rein ästhetischen Konventionen, sondern basieren auf architektonischen Grenzen von Transformer-Modellen und empirischen Erkenntnissen."* Stichprobe von 8 der 13 Referenzen per Websuche/Volltext-Prüfung:

| # | Referenz | Existiert? | Stützt die konkrete Regel? |
|---|---|---|---|
| Lost in the Middle (Liu et al. 2023) → 500-Zeilen-Limit | Ja, korrekt zitiert | Nur das allgemeine Phänomen (Attention-Abfall in langen Kontexten); Paper testet Multi-Doc-QA, **nicht** Code oder eine 500-Zeilen-Schwelle |
| Campbell 2018 (SonarSource) → CC/CogC ≤ 5 | Ja, aber **Titel falsch zitiert** ("misdirection" statt "understandability") | SonarQube selbst nutzt Default 15, nicht 5 — Schwellenwert nicht aus der Quelle ableitbar |
| Bubeck et al. 2023 "Sparks of AGI" → Komplexität→Halluzination | Ja, korrekt zitiert | **Nein** — keine Diskussion von Komplexitätsschwellen im Paper auffindbar |
| DAPLab (2026) Columbia → Immutability-Regel | Ja, real, aber **Blogpost einer Einzelautorin**, keine begutachtete Publikation | Bedingt — thematisch grob passend |
| DAPLab Kategorie 4 → Overload-Verbot | Ja (s.o.) | **Nein** — Kategorie 4 behandelt laut Volltext DB-Schema-Unkenntnis, nicht Methodenüberladung |
| Scale AI "SWE Atlas" → Phantom-Dependency-Regel | Ja, real | **Nein** — misst Codebase-QnA/Test-Writing/Refactoring, keine Halluzinations-Metrik |
| Chroma "Context Rot" → Namespace/Ordnertiefe-Regel | Ja, korrekt zusammengefasst | Nur das allgemeine Phänomen; Ordnertiefe/`cd`-Latenz nicht Teil der Studie |
| Palomba et al. 2018 → "~70% niedrigere Defektwahrscheinlichkeit bei CC≤3" | **Titel falsch zitiert**, Paper existiert | **Nein — die konkrete 70%-Zahl ist im Paper nicht auffindbar**, wirkt frei zugeordnet |

**Befund:** Keine der acht Quellen ist frei erfunden — das ist die gute Nachricht. Aber: zwei Titel sind falsch wiedergegeben, zwei Zuordnungen verfehlen das tatsächliche Thema der Quelle komplett, und eine sehr spezifische Zahl (~70%) ist nicht auffindbar und wirkt konfabuliert. Der Claim *"jede Regel wissenschaftlich begründet"* ist damit **überzeichnet** — die Regeln selbst mögen als Engineering-Heuristiken sinnvoll sein (das prüft dieses Dossier nicht), aber die Zitat-Ebene hält einer Peer-Review-artigen Prüfung nicht durch. Das ist ein Reputationsrisiko: Jeder, der genauso nachprüft wie hier, findet dieselben Lücken.

**Konstruktiver Gegenbefund:** Es gibt bereits einen besseren Präzedenzfall im eigenen Projekt — `Docs/ROADMAP.md` Epic 27 ("Feature-Audit 2026-06 — Default-Kalibrierung") kalibriert mehrere Schwellenwerte empirisch nach, z. B. `MaxLineCount: 500` mit Verweis auf *"Ardito et al. 2020"* als Industriestandard-Mittelwert. Diese Referenz wurde in dieser Runde nicht mitgeprüft (Fokus lag auf den auffälligeren `rationale.md`-Zitaten), aber der Ansatz — Schwellenwerte gegen echte Kalibrierungsstudien statt gegen thematisch nur lose verwandte LLM-Paper zu begründen — ist der richtige Weg und sollte auf `rationale.md` übertragen werden, nicht nur in `ROADMAP.md` stehen.

### 4.2 `validate_file` ist eine schmalere Lücke als der Konzept-Entwurf annahm

`tasks/validate-file/konzept.md` positioniert das Tool als fehlendes Primitiv. Nach heutiger Recherche: `sailro/RoslynMcpExtension` hat bereits ein `roslyn_validate_file`-Tool (schwächer: kein Batch, kein `nextSteps`, VS-only, Analyzer-Diagnostics nur optional) und `roslyn-codelens-mcp` (heute aktualisiert) hat `get_diagnostics`/`get_file_overview` (Compiler+Analyzer kombiniert, aber ohne Freitext-Empfehlungen oder Git-Diff-Batch). Die *Kombination* aus Git-Diff-Batch + `nextSteps`-Prosa + stdio-first (kein VS-Zwang) bleibt differenzierend, aber "wir schließen die einzige Lücke am Markt" wäre falsch — mehrere Teams sind unabhängig auf dieselbe Grundidee gekommen. Das ist eher ein Signal, dass die Idee richtig ist, als ein Alleinstellungsmerkmal.

### 4.3 Dokumentations-Drift ist ein wiederkehrendes Muster, nicht ein Einzelfall

Drei unabhängige Funde in dieser und vorangegangenen Sessions zeigen dasselbe Muster — Doku behauptet mehr, als der Code liefert:
- `Docs/ROADMAP.md` Epic 14 markiert eine Magic-Value-Regel (`EnforceNoMagicValues`) als `[x]` erledigt — tatsächlich existiert sie im Code nicht mehr; ein Test (`ConfigSyncerTests.cs`) bestätigt sogar explizit, dass sie als *obsolet* behandelt und aus Nutzer-Configs entfernt wird.
- `README.md` listete zuletzt 14 MCP-Tools in seiner Tabelle, tatsächlich sind es 18 (vor den zwei neuen Features).
- Der jetzt geprüfte `rationale.md`-Zitat-Befund (Abschnitt 4.1) ist strukturell dasselbe Muster: eine Dokumentations-Ebene, die mit mehr Autorität auftritt, als die zugrundeliegende Substanz hergibt.

Kein einzelner Befund ist gravierend, aber das wiederholte Muster spricht für einen fehlenden Doku-Konsistenz-Check (z. B. ein periodischer Abgleich ROADMAP-Status gegen tatsächlich vorhandene RuleIds, analog zu einem künftigen `find_magic_values`-artigen Self-Audit).

### 4.4 Adoptions-Realität (Ergänzung aus Abschnitt 2)

Unabhängig von jedem Feature-Vergleich: AiNetLinter hat **kein NuGet-Release, keine nennenswerte Stern-Zahl, keine externe Nutzerbasis außerhalb dieses einen Kontexts** (Stand dieser Recherche). Selbst das kleinste geprüfte Analyzer-Tool (Meziantou.Analyzer, 12,1 Mio. Downloads) übertrifft das um Größenordnungen. Für ein internes Werkzeug, das primär den eigenen agentischen Workflow absichern soll (siehe `05-roadmap.md` §0: *"kein Multi-Team-/Multi-Agent-Produkt"*), ist das kein Mangel per se — aber es relativiert jede Aussage über "Marktposition", die über die enge technische Nische hinausgeht. Eine ehrliche Formulierung ist: *technisch differenziert, ökosystemisch nicht existent* — beides gleichzeitig wahr.

### 4.5 Strukturelle Schwächen ohne eigenes Verschulden (geteilt mit der ganzen Nische)

- **Kaltstart via `MSBuildWorkspace`** (`Docs/rationale.md` §13 bestätigt die eigene Nutzung) — dieselbe Schwäche, die `03-market-research.md` bei "den meisten" Konkurrenten findet. Der residente Server mit inkrementeller Staleness-Invalidierung (mtime+Hash) mindert das auf einen einmaligen Start-Kosten, ist aber kein Alleinstellungsmerkmal — `sailro` bleibt bei reiner VS-Latenz überlegen (allerdings nur für VS-Nutzer).
- **Kein Streamable-HTTP-Transport** — bewusstes Nicht-Ziel (kein Cloud-Anspruch, siehe `05-roadmap.md` §0), aber real: kein Multi-Client/Team-Sharing-Szenario möglich, anders als bei `sailro`/`AmazingMCP`.
- **Kein Refactoring-Preview/Rollback** — explizit als Non-Goal abgelehnt (`06-nicht-umsetzen.md`: *"widerspricht read-only-Architektur, Agent+Git decken das ab"*). Das ist eine vertretbare Wette, aber unbelegt: anders als die Lint-Regeln trägt diese Entscheidung keine zitierte Evidenz, nur eine Architektur-Präferenz. Sollte als Annahme, nicht als bewiesene Überlegenheit behandelt werden.

---

## 5. Marktdynamik — warum "Stand heute" fragil ist

Zwei der wichtigsten Datenpunkte dieses Dossiers sind **am selben Tag entstanden, an dem dieses Dossier geschrieben wird**: `roslyn-codelens-mcp` (neuer, sehr aktiver ~70-Tool-Konkurrent mit Quality-Audit-Composite) und die offizielle SonarQube-MCP-Server-Aktualisierung (621⭐, Marktmacht-Faktor auch wenn C#-Secrets-Detection fehlt) hatten beide einen GitHub-Push am 2026-08-12. Die Nische wächst weiter (mehrere bisher ungelistete Neuzugänge gefunden: `MadQ/RoslynMcp`, `tekinozan/roslyn-mcp-server`, `jfmeyers/roslyn-lens`), sie konsolidiert sich nicht. Gleichzeitig ist die Hälfte der vor 6 Tagen noch aktiven Liste inzwischen als inaktiv einzustufen.

**Konsequenz:** Jede "wir sind die einzigen, die X können"-Aussage in diesem Projekt hat eine kurze Halbwertszeit. Ein Alleinstellungsmerkmal wie `find_magic_values` heute kann in Wochen von einem der aktiven ~70-Tool-Konkurrenten nachgebaut werden — die Eintrittsbarriere für ein einzelnes neues Tool in einem bestehenden Server ist niedrig. Das größte strukturelle Risiko bleibt latent: Microsoft hat weiterhin **keinen** offiziellen C# Dev Kit MCP-Server veröffentlicht (Stand heute unverändert seit 2026-08-06), aber liefert das offizielle MCP-C#-SDK, auf dem praktisch die gesamte Konkurrenz aufbaut — ein einziger offizieller Analyzer-Server könnte die gesamte Drittanbieter-Nische inklusive AiNetLinter in ihrer Navigations-/Diagnostics-Rolle entwerten (die Regel-Substanz bliebe davon unberührt).

---

## 6. Fazit

AiNetLinter als Gesamtprodukt (nicht nur die zwei geprüften Features) ist **substanziell differenziert, aber auf zwei unterschiedlichen Achsen unterschiedlich stark — und ökosystemisch praktisch unsichtbar**:

- **Achse 1 — Positionierung/Idee:** *"Regelwerk speziell für LLM-Coding-Agenten designt"* ist nach Prüfung sowohl der MCP- als auch der klassischen Analyzer-Konkurrenz **unbesetzt**. Das ist der stärkste, robusteste Claim des Projekts.
- **Achse 2 — Wissenschaftliche Begründungstiefe:** Der Claim *"jede Regel wissenschaftlich fundiert"* ist **überzeichnet** — reale Quellen, aber falsche Titel, thematisch verfehlte Zuordnungen und mindestens eine nicht auffindbare Zahl.
- **Achse 3 — Technische Substanz:** 46+ Lint-Regeln + 18-20 MCP-Tools mit durchgehender Token-Disziplin und belegter Cross-Project-Dogfooding-Praxis sind **real und überdurchschnittlich tief** — sowohl gegenüber der MCP-Nische (fast nur Navigation/Refactoring ohne eigene Regeln) als auch relativ zur eigenen Größe (Solo-Projekt) bemerkenswert.
- **Achse 4 — Markt/Adoption:** **Praktisch bei null**, um Größenordnungen hinter selbst dem kleinsten etablierten Analyzer-Tool.

Die beiden zur Bewertung unterstellten Features fügen sich in dieses Bild ein, verändern es aber nicht grundlegend: `find_magic_values` verteidigt eine echte, noch offene Lücke; `validate_file` schließt eine Lücke, die inzwischen mehrere Wettbewerber gleichzeitig zu schließen versuchen — beide sind richtig priorisiert (siehe `08-prioritaet-agentische-programmierung.md`), aber keines davon ist der Kern der Wettbewerbsposition. Der Kern ist Achse 1 und 3 — und die These, dass Regeltiefe + AI-Readability-Fokus + Dogfooding-Disziplin einen echten Vorsprung ergeben, den ein neues Tool oder ein neuer Blogpost-Fund nicht sofort einholt.

**Handlungsempfehlungen aus diesem Dossier** (keine neuen Features, sondern Korrekturen am Status quo):

1. **[Umgesetzt 2026-08-13]** `Docs/rationale.md` überarbeitet: falsche Zitat-Titel korrigiert, DAPLab Kat. 4 und Scale AI SWE Atlas durch thematisch passende Quellen ersetzt (Wu et al. 2024 Preprint bzw. Spracklen et al. 2025, USENIX Security), die nicht-auffindbare 70%-Zahl entfernt und durch eine ehrliche Einordnung ersetzt (keine passende Studie für den exakten Schwellenwert gefunden). *Korrektur an dieser Stelle:* Die hier ursprünglich genannten Palomba-Zahlen waren selbst ungenau — korrekt sind **+283 % Change-Proneness bei God Class** (nicht +28 %, ein Verlesen der Originalzahl) und **+350 %/+300 % bei drei gleichzeitigen Code-Smells** gegenüber smell-freien Klassen; die "+21 % Spaghetti Code"-Zahl war nicht verifizierbar und wurde nicht übernommen. Das hier gelobte "bessere Vorbild" (Epic 27, `Ardito et al. 2020`) erwies sich bei der Umsetzung selbst als Fehlzuordnung (Paper ist ein reiner Metrik-Survey ohne LOC-Schwellenwert-Aussage, zudem 3 Jahre vor Liu et al. 2023 erschienen — kann "Lost in the Middle" chronologisch nicht stützen) und wurde in `ROADMAP.md` Epic 27 ebenfalls korrigiert.
2. **[Umgesetzt 2026-08-13]** `README.md`-Tool-Tabelle (14→18 Tools ergänzt) und `ROADMAP.md` Epic 14 korrigiert (Doku-Drift-Muster aus Abschnitt 4.3) — inkl. Beleg des tatsächlichen Entfernungs-Commits (`764281a`, 2026-06-19).
3. Bei der Priorisierung von `validate_file` gegenüber `find_magic_values` den engeren Wettbewerbs-Vorsprung von `validate_file` einpreisen — Umsetzungsdruck ist höher als angenommen, weil Konkurrenz hier nicht nur beobachtet, sondern aktiv nachzieht.
4. Die "AI-Readability"-Positionierung (Achse 1) explizit und prominent als eigenständigen, von der Zitat-Dichte unabhängigen Claim kommunizieren — sie ist der eigentliche Burggraben, nicht die einzelnen Fußnoten.
5. **[Umgesetzt 2026-08-13]** `NDepend.MCP.Server` als Abschnitt 4.7 in `03-market-research.md` nachgetragen — inhaltlich näher an `dependency_graph`/`metrics_tree` als an den übrigen Roslyn-MCP-Servern, kein AI-Readability-Regelwerk im Sinne von AiNetLinter.

---

## Quellen

- `tasks/features/03-market-research.md` (2026-08-06, Basisrecherche)
- `Docs/rationale.md`, `Docs/ROADMAP.md`, `README.md`, `Docs/agent-api.md`, `Docs/integration.md` (Eigenpositionierung)
- [sailro/RoslynMcpExtension](https://github.com/sailro/RoslynMcpExtension) inkl. [ValidateFileTool.cs](https://github.com/sailro/RoslynMcpExtension/blob/main/src/RoslynMcpExtension.Server/Tools/ValidateFileTool.cs)
- [MarcelRoozekrans/roslyn-codelens-mcp](https://github.com/MarcelRoozekrans/roslyn-codelens-mcp)
- [SonarSource/sonarqube-mcp-server](https://github.com/SonarSource/sonarqube-mcp-server) + [Tools-Doku](https://docs.sonarsource.com/sonarqube-mcp-server/reference/tools)
- [biegehydra/BifrostMCP](https://github.com/biegehydra/BifrostMCP) (ex-CSharpLangMCPServer)
- [brendankowitz/dotnet-roslyn-mcp](https://github.com/brendankowitz/dotnet-roslyn-mcp)
- [MCP C# SDK v2.0 Announcement](https://devblogs.microsoft.com/dotnet/announcing-v20-of-the-official-mcp-csharp-sdk/)
- [sonar-dotnet](https://github.com/SonarSource/sonar-dotnet) · [SonarQube C# Docs](https://docs.sonarsource.com/sonarqube-server/analyzing-source-code/languages/csharp) · [SonarQube Pricing 2026](https://dev.to/rahulxsingh/sonarqube-pricing-in-2026-community-developer-enterprise-and-cloud-costs-explained-bdg) · [Sonar AI Code Assurance](https://www.sonarsource.com/solutions/ai/ai-code-assurance/)
- [Roslynator](https://github.com/dotnet/roslynator) · [Roslynator Fix-All](https://josefpihrt.github.io/docs/roslynator/how-to-fix-all-diagnostics/) · [Roslynator.Analyzers NuGet](https://www.nuget.org/packages/roslynator.analyzers/)
- [StyleCop.Analyzers](https://github.com/DotNetAnalyzers/StyleCopAnalyzers) · [NuGet](https://www.nuget.org/packages/stylecop.analyzers/)
- [Meziantou.Analyzer](https://github.com/meziantou/Meziantou.Analyzer)
- [NDepend CQLinq](https://www.ndepend.com/features/cqlinq) · [NDepend Technical Debt](https://www.ndepend.com/docs/technical-debt) · [NDepend Purchase](https://www.ndepend.com/purchase) · [NDepend.MCP.Server](https://github.com/ndepend/NDepend.MCP.Server) · [NDepend MCP Blog](https://blog.ndepend.com/developing-an-mcp-server-with-c-a-complete-guide/)
- [JetBrains Rider 2026.2 — Quality Check Hooks for AI Agents](https://blog.jetbrains.com/dotnet/2026/06/08/rider-2026-2-code-quality-check-hooks-for-ai-agents/)
- Liu, N. F. et al. (2023). "Lost in the Middle". arXiv:2307.03172
- Campbell, G. D. (2018). "Cognitive Complexity: A new way of measuring understandability". SonarSource Whitepaper
- Bubeck, S. et al. (2023). "Sparks of Artificial General Intelligence". arXiv:2303.12712
- DAPLab / Reya Vir (2026). "9 Critical Failure Patterns of Coding Agents" (Blogpost, daplab.cs.columbia.edu)
- Scale AI (2026). "SWE Atlas" (scale.com/blog/swe-atlas)
- Chroma Research (2025). "Context Rot" (research.trychroma.com/context-rot)
- Palomba, F. et al. (2018). "On the diffuseness and the impact on maintainability of code smells". ICSE'18/EMSE
