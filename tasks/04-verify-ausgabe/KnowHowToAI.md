# MCP-Nutzungs-Audit – KnowHowToAI (Blazor)

**Datum:** 2026-09-24
**Ziel:** `C:\Daten\Entwicklung\Ralf\KnowHowToAI\KnowHowToAI.slnx`
**Methode:** Read-only AiNetLinter-MCP-Abfragen; kein Build, keine Tests. Die einzige Dateiänderung dieses Audits ist dieser Bericht.

## Ergebnisübersicht

- `verify(scope:"solution")` lief vollständig durch: **verdict=pass, score=10.0, violationCount=0**.
- Die Dead-Code-Suche meldete **358 Kandidaten**: 231 `test_only`, 127 `unreferenced`, 0 API-geschützt, 0 unentscheidbar. 12 Kandidaten wurden ausgegeben; 346 sind wegen `evidence_limit, response_budget` nicht sichtbar. `deadCode.next=review_now`.
- Mindestens ein konkreter Fehlalarm ist reproduziert: `DashboardQuery` wurde als `test_only` ausgegeben, obwohl die Solution eine produktive Verwendung in `DashboardService.cs:50` enthält. Zusätzlich ließ sich der ausgegebene Handoff `h:ciNc` nicht direkt nachverfolgen.
- Die vorhandene API-Policy ist `closed_solution`. Die expliziten Varianten `external_library` und ungültiges `unknown` wurden aus Read-only-Gründen nicht durch Konfigurationsänderungen ausprobiert.
- Keine Dead-Code-Advisory ist ein Löschbeweis. Das Gate-Verdict bleibt unabhängig von den Advisories auf `pass`.

## Reproduzierbare Aufrufe und Evidenz

Alle Aufrufe adressierten dieselbe absolute Solution. Das relevante API-Schema stammt aus den verfügbaren MCP-Tool-Beschreibungen.

### 1. Solution-Gate und Dead-Code-Advisories

Aufruf:

```text
verify(
  targetPath="C:\Daten\Entwicklung\Ralf\KnowHowToAI\KnowHowToAI.slnx",
  scope="solution"
)
```

Antwortkopf:

```text
verdict: pass
completeness: complete
score: 10.0
violationCount: 0
scope: solution
evidence: returned=12/362; truncation=evidence_limit, response_budget
deadCode: status=complete; candidates=358; testOnly=231; unreferenced=127;
  apiProtected=0; undecidable=0; shown=12; truncatedBy=346; next=review_now
advisories: count=12; completeness=complete; review_required; static_evidence
```

Die zwei Zähler `returned=12/362` und `shown=12` bezeichnen die sichtbare Evidenz, während `candidates=358` die Dead-Code-Kandidaten zählt. Das ist erklärbar, aber in einer einzelnen Ausgabe nicht selbsterklärend. Die 12 ausgegebenen Symbole hatten durchgehend `confidence=low` und den Hinweis, vor dem Entfernen statische Referenzen, Reflection, DI, Generatoren, `dynamic`, Markup/Konfiguration und externe Consumer gegenzuprüfen.

**Auswirkung / Schweregrad: mittel.** Die Zusammenfassung ist kompakt und kommuniziert Zähler, Trunkierung, Priorität und Advisory-Status. Die konkrete Liste deckt nur 12 von 358 Kandidaten ab; eine vollständige Review erfordert weitere Abfragen oder eine explizite Fortsetzung. Das Gate bleibt klar getrennt: 0 Verstöße / Pass, trotz 358 Review-Kandidaten.

### 2. Repräsentative Kandidaten gegenprüfen

#### Fehlalarm: `DashboardQuery` als `test_only`

In `verify` erschien:

```text
h:ciNc
ref=src/KnowHowToAI.Core/Application/Dashboard/DashboardQuery.cs:6
usage=test_only; confidence=low; reason=no_production_static_reference; testReferences=7
```

Direkter Aufruf mit dem ausgegebenen Handoff:

```text
find_references(
  targetPath="C:\Daten\Entwicklung\Ralf\KnowHowToAI\KnowHowToAI.slnx",
  symbolIdentifier="h:ciNc",
  scopeType="all",
  maxResults=50
)
```

Ergebnis war ein Fehler:

```text
SYMBOL_NOT_FOUND: Kein Symbol gefunden fuer Identifikator
'M:KnowHowToAI.Core.Application.Dashboard.DashboardQuery.#ctor~p:b9d13392c7fb4f89…'
```

Fallback über den qualifizierten Typnamen ergab produktive und Test-Aufrufstellen:

```text
find_references(...,
  symbolIdentifier="KnowHowToAI.Core.Application.Dashboard.DashboardQuery",
  scopeType="all", maxResults=50)
```

Auszug:

```text
src/KnowHowToAI.Core/Application/Dashboard/DashboardService.cs:50
  Aufruf von 'DashboardQuery' in Projekt 'KnowHowToAI.Core'
tests/KnowHowToAI.Core.Tests/Application/Dashboard/DashboardServiceTests.cs:31
  Aufruf von 'DashboardQuery..ctor' ...
[weitere Testaufrufe an Zeilen 49, 86, 103, 130, 175, 215]
```

Die Symbolsuche mit `pattern="DashboardQuery", kind="record"` bestätigte den Datensatz an der Advisory-Deklaration, Zeile 6. `get_feature_context` für den produktiven `DashboardService` bestätigte `GetDashboardAsync(DashboardQuery query, ...)` als Member. Damit ist `test_only` für diesen Datensatz/Constructor ein **bestätigter Fehlalarm**. Der direkte Handoff-Fehler ist ein zusätzlicher Nutzungsbruch; der Name-Fallback liefert die nötige Evidenz, erzeugt aber einen unnötigen Folgeaufruf.

#### Unklar: `CurrentUser.Id` als `unreferenced`

`verify` meldete `h:ciMY`, `CurrentUser.cs:6`, `usage=unreferenced`. `find_references(h:ciMY)` meldete keine Aufrufstellen für `CurrentUser.Id`. Der Body zeigt eine öffentliche positional-record-Property `string Id`.

Die Suche nach dem Typnamen fand produktive Verwendung:
- `ICurrentUserService.GetCurrentUser()` liefert den Typ.
- `DummyCurrentUserService` konstruiert ihn in Produktion und implementiert die Registrierung.
- Mehrere Tests konstruieren ebenfalls `CurrentUser`.

Gezielte Suchen in den geprüften 48 Razor-Dateien nach `CurrentUser` und `DashboardQuery` lieferten 0 Treffer. JSON/XML-basierte Serialisierungsbindungen für `CurrentUser.Id` wurden nicht einzeln geprüft; die Property kann über Serialisierung/Reflection oder Consumer außerhalb der Solution beobachtet werden. Einstufung: **unklar**, keine Löschentscheidung. Der Befund ist auf die Property `Id` bezogen und darf nicht als „Typ ohne Referenzen“ interpretiert werden.

#### Unklar / Referenzgrenze: `IReleaseRepository.FindAsync` als `test_only`

`verify` meldete `h:ciMX`, `IReleaseRepository.cs:9`, `test_only`, `testReferences=1`. Der direkte `find_references(h:ciMX)` zeigte den Aufruf in `SqlReleaseIntegrationTests.cs:52`. `find_implementations(h:ciMX)` fand zwei Implementierungen:
- `SqlReleaseRepository.FindAsync`, produktiver Code, Zeile 48
- `InMemoryReleaseRepository.FindAsync`, Test-Support, Zeile 22

Der Produktivtyp wird damit als Implementierung im Symbolgraphen erkannt. Die Referenzabfragen auf die Implementierungshandoffs `h:cjgM` und `h:cjgN` lieferten beide dieselbe Teststelle, die als Aufruf von `SqlReleaseRepository.FindAsync` beschrieben wurde. Auch der Feature-Kontext des In-Memory-Handoffs zeigte diese Teststelle. Das wirkt wie eine Alias-/Dispatch-Ungenauigkeit und ist keine ausreichende Evidenz, den Advisory-Klassifikator als korrekt oder falsch abzuhaken. Einstufung: **unklar mit auffälliger Handoff-/Referenzzuordnung**.

#### `DispatcherEscapeAnalyzer`: Deklaration mit Tests und Projektintegration

Die Advisories enthielten unter anderem `h:ciMU` (Constructor, `test_only`, zwei Testreferenzen), `h:ciMV` (`SupportedDiagnostics`, `unreferenced`) und `h:ciMW` (`Initialize`, `unreferenced`). Die Referenzabfrage für `h:ciMU` ergab zwei Aufrufe in `DispatcherEscapeAnalyzerTests.cs`; für `h:ciMW` keine Aufrufstelle.

Der Typ ist ein öffentlicher `DiagnosticAnalyzer`; sein Quelltext zeigt das Roslyn-Attribut `[DiagnosticAnalyzer(LanguageNames.CSharp)]` und `Initialize` registriert eine Operation-Analyse. Die Projektdateisuche fand eine `ProjectReference` zum Analyzer im Serverprojekt. Roslyn lädt Analyzer über Metadaten-/Attributkonventionen; der Aufrufgraph muss den Einstiegspunkt nicht als gewöhnlichen Call-Site-Verweis zeigen. Als Gesamttyp ist dies deshalb ein **wahrscheinlicher Fehlalarm** der reinen Call-Site-Interpretation. Auf Property-/Methodenebene bestätigt das Fehlen statischer Call-Sites noch nicht, dass Roslyn sie nicht über Override-/Attributmechanik verwendet. Die Policy nennt externe Consumer und dynamische Bindungen explizit als Grenzen.

### 3. Blazor-Markup und Nicht-C#-Abdeckung

Aufruf:

```text
get_index_scope(targetPath="C:\Daten\Entwicklung\Ralf\KnowHowToAI\KnowHowToAI.slnx")
```

Ergebnis: 586 `.cs`-Dateien im Symbolgraphen, insgesamt 635 Roslyn-Dokumente (53 generiert, 274 in Testprojekten), 48 physische `.razor`-Dateien außerhalb des C#-Symbolgraphen sowie 44 JSON-Dateien. Der Index weist Nicht-C#-Dateien ausdrücklich der Textsuche zu.

Ein erster `get_file_tree(view="files", includeExtensions=[".razor"], maxResults=100)`-Aufruf ließ `maxDepth` auf Default und fand nur `tests/KnowHowToAI.Web.Tests/_Imports.razor`; Antwortwarnung: Scantiefe begrenzt. Derselbe Aufruf mit `maxDepth=32`, `maxResults=500`, `maxResponseBytes=40000` listete alle 48 Razor-Dateien vollständig. Die erneute Abfrage war nötig, um die Kandidaten gegen Markup zu prüfen.

`search_pattern` mit `enrichCSharp=true` und den Kandidatennamen `CurrentUser` bzw. `DashboardQuery` in `**/*.razor` lieferte 0 Treffer. Das belegt nur die Textsuche in den durchsuchten Razor-Dateien, nicht dynamische oder anders benannte Bindungen. Für die gezielt geprüften Namen `CurrentUser` und `DashboardQuery` gab es keine konkreten Razor-/Markup-Treffer.

### 4. API-Policy

Aufruf:

```text
search_pattern(
  targetPath="C:\Daten\Entwicklung\Ralf\KnowHowToAI\KnowHowToAI.slnx",
  pattern="DefaultApiSurface",
  includePatterns=["**/ainetlinter-rules.json"],
  contextLines=4
)
```

Ausgabe: `ainetlinter-rules.json:364: "DefaultApiSurface": "closed_solution"`. Das stimmt mit dem dokumentierten Default überein. `verify` meldete `apiProtected=0`; damit gab es hier keine Kandidaten, deren API-Schutzwirkung an der Ausgabe praktisch beobachtet werden konnte.

`external_library` und explizites `unknown` wurden nicht gesetzt, weil die Prüfung strikt read-only sein sollte und die Änderung der Repository-Konfiguration dafür erforderlich wäre. Somit ist in dieser Solution der gültige Default beobachtet, aber nicht praktisch verifiziert, dass der MCP-Server `external_library` akzeptiert und `unknown` vor dem Gate als Fehler zurückweist. Die Vorgabe zu `unknown` stammt aus dem Audit-Auftrag bzw. den Agent-Regeln, nicht aus einem Versuch an dieser Solution.

### 5. Ausgabegröße, Handoffs und Alltagsergonomie

UTF-8-Größen wurden als Bytes der serialisierten MCP-Antwortobjekte gemessen (JSON-Rahmen plus Tooltext). Tokenangaben sind grobe Näherungen von etwa 1 Token je 4 Bytes; kein Tokenizer wurde ausgeführt.

| Aufruf | Antwortgröße UTF-8 | grobe Token-Näherung | Beobachtung |
|---|---:|---:|---|
| `verify(scope="solution")` | 3.891 B | ~973 | Kompakter Gate-Überblick, nur 12/358 Advisories |
| `get_index_scope` | 3.670 B | ~918 | Viele Extension-Zeilen; für Kandidatenprüfung nur teilweise nötig |
| `get_file_tree` Razor, Default-Tiefe | 485 B | ~121 | Nur 1 von 48 Dateien; Warnung, Folgeaufruf erforderlich |
| `get_file_tree` Razor, Tiefe 32 | 3.891 B | ~973 | Alle 48 Pfade vollständig |
| `get_feature_context(IReleaseRepository.FindAsync)` | 1.960 B | ~490 | Komposit mit Metriken, Tests und Call-Sites; für Usage-Review mehr als nötig |
| `get_symbol_body` für Analyzer-Symbole | 4.464 B | ~1.116 | Der Analyzer-Body wurde nach 80 von 86 Zeilen gekürzt |
| `find_references(h:ciNc)` | 521 B | ~130 | Fehler als Text mit `[ERROR]` und Status statt strukturiertem MCP-Fehler (`isError=false`) |

Konkrete Ergonomie-/Nutzungsbefunde:

- **Schweregrad mittel – Handoff-Fehler:** Das Advisory-Handoff `h:ciNc` führte zu `SYMBOL_NOT_FOUND`, obwohl ein qualifizierter Namensaufruf danach Referenzen fand. Diese Abweichung verursacht Folgearbeit und kann eine Prüfung abbrechen, wenn der Nutzer den Handoff als maßgeblich behandelt.
- **Schweregrad mittel – begrenzte Advisory-Ausgabe:** Der Gate-Aufruf nennt `346` ausgelassene Kandidaten, liefert aber keinen expliziten Fortsetzungsparameter oder einen paginierten Advisory-Aufruf im selben Ergebnis. Für eine vollständige Sichtung entstehen Folgeaufrufe.
- **Schweregrad niedrig – uneinheitliche Fehlerkennzeichnung:** Der Symbolfehler war Textinhalt mit `[ERROR]` / `Status: operation=error`, aber das MCP-Resultat setzte `isError=false`. Clients müssen den Tooltext auswerten.
- **Schweregrad niedrig – Bodies werden abgeschnitten:** Bei Analyzer-Symbolen endete die Ausgabe mit „truncated, total 86 Zeilen“; fehlende sechs Zeilen erfordern ein weiteres Windowing, falls sie relevant sind. Es wurde kein Body für die Löschentscheidung benötigt.
- **Schweregrad niedrig – uneindeutige Counts:** `returned=12/362` und `candidates=358` sind unterschiedliche Bezugsgrößen ohne Erläuterung in der Ausgabe.
- **Schweregrad niedrig – Suche über mehrere Regex-Begriffe:** Die Regex-Suche mit mehreren Alternativen lieferte 0 Treffer, obwohl separate Einzelsuchen bzw. Symbolabfragen passende Treffer fanden. Für die Kernbefunde wurde auf Einzelmuster bzw. semantische Referenzsuche ausgewichen.
- **Schweregrad niedrig – Scantiefe beim Dateibaum:** Default-Tiefe gab einen unvollständigen Dateibaum mit explizitem Warnhinweis aus; Parameter `maxDepth` korrigierte das Ergebnis.
- **Keine Handoff-ID aus Anzeigenamen abgeleitet:** Nach einem fehlerhaften Advisory-Handoff wurden ausschließlich der qualifizierte Symbolname und später von MCP gelieferte Handoffs für Folgeabfragen genutzt.

## Schweregrad und Grenzen

**Gesamtbewertung der Nutzbarkeit: mittel.** `verify` liefert klare Gate-Zähler und stellt Advisories als prüfpflichtige, niedrig-konfidente Kandidaten dar. Die konkrete Review zeigte jedoch mindestens einen echten `test_only`-Fehlalarm und einen direkten Handoff-Auflösungsfehler. Ein weiterer möglicher Fehlalarm entsteht bei Analyzer-Einstiegspunkten, die über Roslyn-Konventionen statt Call-Sites aktiviert werden.

Die Stichprobe umfasst nur die 12 sichtbaren Advisories aus 358 Kandidaten. Die verbleibenden 346 wurden nicht einzeln nachverfolgt. Reflection, Laufzeitbindung, Generatoren und externe Consumer sind mit dieser statischen MCP-Prüfung nicht ausgeschlossen. Es wurden weder Konfiguration noch Code oder Solution-Dateien verändert, daher konnte der ungültige Wert `unknown` nicht aktiv vorgeprüft werden.
