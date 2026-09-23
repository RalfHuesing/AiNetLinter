---
status: draft
execution_mode: autonomous
open_questions: []
---

# Konzept: Dead Code nach produktiver Nutzung beurteilen

## Intention

AiNetLinter soll im **einen bestehenden MCP-Aufruf `verify`** Produktionscode
sichtbar machen, der innerhalb der geladenen Solution keine produktive
statische Nutzung hat. Testaufrufe dürfen solchen Code nicht am Leben halten.
Ein aufrufender Agent soll den Befund unmittelbar als priorisierte technische
Schuld prüfen und bei bestätigter Entbehrlichkeit beheben können. Der Befund
ist eine statische Advisory, keine automatische Löschentscheidung.

Der Suchraum für Referenzen ist immer die ganze geladene Roslyn-Solution.
`verify(changes)` und `verify(solution)` unterscheiden nur, **welche
Deklarationen** untersucht werden. Sie verwenden dieselbe Definition
produktiver Nutzung und dieselbe projektbezogene API-Policy.

## Belegter Ist-Stand

- `DeadCodeAdvisoryScanner.CollectCandidateDocuments` schließt bei
  `IncludeTests = false` Testprojekte als Kandidaten aus.
- `IsSymbolUnreferencedAsync` sucht Referenzen nicht privater Symbole über die
  gesamte Solution und behandelt bislang jede Fundstelle als Nutzung.
  `HasReferencedInterfaceOrOverrideAsync` tut das auch für indirekte
  Referenzen. Daher verdeckt ein Testaufruf einen test-only
  Produktionsmember.
- Produktiv ruft nur `VerifyAdvisoryProjector.CollectAsync` den Scanner auf.
  Es existiert kein eigener Dead-Code-MCP-Aufruf. Der Projektor läuft derzeit
  nur bei `verify(changes)`, erhält keine Konfiguration und fängt
  Scannerfehler pauschal als `unavailable` ab.
- `verify` hat die Scopes `changes` und `solution`, ein Content-only-Ergebnis
  sowie einen eigenen Gate-Verdict. Advisories ändern den Gate-Verdict
  derzeit nicht. Die Antwort ist auf ein knappes Budget begrenzt.
- `ProjectOverrides` und `ProjectConfigResolver.ResolveForProject` existieren.
  Die Dead-Code-Policy ist dort noch nicht modelliert. Bei mehreren
  passenden Override-Mustern gewinnt bisher der erste Treffer.

## Scope

### Muss

1. Produktions- und Testreferenzen für jedes geprüfte Symbol im
   solutionweiten Suchraum getrennt bewerten, einschließlich
   Interface-/Override-Referenzen.
2. `verify(changes)` auf geänderte produktive Quelldateien und
   `verify(solution)` auf alle produktiven Quelldateien anwenden.
   Die Referenzsuche bleibt in beiden Fällen solutionweit.
3. Die externe API-Oberfläche pro Projekt über die geladene Konfiguration
   festlegen und vor dem Scan für jedes Kandidatenprojekt validieren.
4. Dead-Code-Kandidaten im selben `verify`-Ergebnis vorrangig und
   handlungsfähig ausgeben. Kandidaten ändern `verdict`, `score` und
   `violationCount` nicht.
5. Fehlende oder ungültige API-Einordnung als konkreten `verify`-Fehler
   mit `isError: true` ausgeben, ohne Teilergebnis.
6. Whitelist, symbolnahe Suppression, Razor-Gegenprüfung,
   Scope-Projektion und bestehende Gate-Semantik erhalten.
7. Tests und Dokumentation an den beschriebenen Vertrag anpassen.

### Nicht

- Kein zweites Dead-Code-MCP-Tool und kein zusätzlicher `verify`-Parameter.
- Kein automatisches Löschen, Umschreiben oder Gate-`fail` allein aufgrund
  eines statischen Dead-Code-Kandidaten.
- Keine Testcode-Dead-Code-Analyse im `verify`-Ergebnis. Das interne
  `IncludeTests` bleibt ausschließlich ein Kandidatendokument-Filter.
- Keine Projekt-lokale Referenzsuche, keine Erkennung externer Consumer
  aus `OutputType`, Paketnamen oder Dateinamen.
- Keine Namespace-, Klassen- oder Member-Allowlist für externe APIs.
- Keine vollständige Reflection-, DI-, Generator- oder
  `dynamic`-Analyse.
- Keine Änderung der Codequalitätsregeln oder der CLI-Schnittstelle.

## Fachlicher Vertrag

### 1. Kandidatenscope und Referenzrollen

`verify(changes)` nutzt exakt die vom bestehenden Verify-Scope aufgelösten
geänderten C#-Quelldateien als Deklarationsscope. `verify(solution)` nutzt alle
gültigen C#-Quelldateien produktiver Projekte. Testdokumente sind in beiden
Scopes keine Dead-Code-Deklarationskandidaten. Ein leerer Scope liefert
keinen globalen Clean-Claim; die bestehende Verify-Scope-Semantik bleibt
maßgeblich.

`verify` ruft den Scanner für beide Scopes mit
`Accessibility=All`, `Confidence=Both`, `Kind=All`,
`Mode=Members` und `IncludeTests=false` auf. Lokale
Compiler-/IDE-Diagnostik ist kein Bestandteil dieses Dead-Code-Vertrags.

Eine Roslyn-Referenzstelle wird anhand ihres tatsächlichen
`ReferenceLocation.Document` bewertet, nicht anhand des deklarierenden
Symbols oder eines nachträglichen Textfilters:

| Rolle | Verbindliche Klassifizierung | Wirkung |
|---|---|---|
| `test` | `TestDetector.IsTestProject(document.Project)` oder bei vorhandenem `document.FilePath` `TestDetector.IsTestFile(document.FilePath)` ist wahr | Zählt nicht als produktive Nutzung. |
| `production` | `document.Project.SupportsCompilation` ist wahr, `document.FilePath` ist vorhanden und die Testregel trifft nicht zu | Hält das Symbol am Leben. |
| `unknown` | Dokument oder Projekt fehlt oder die Stelle lässt sich nicht verlässlich zuordnen | Verhindert einen Dead-Code-Kandidaten für dieses Symbol; erhöht den Zähler unentscheidbarer Symbole. |

Die Testregel hat Vorrang vor der Produktionsregel. Dieselbe physische Datei
kann als Linked File in mehreren Roslyn-Projekten vorkommen; jede
Referenzstelle wird anhand ihrer eigenen Dokument-/Projektidentität
klassifiziert. Eine produktive Einbindung hält das Symbol am Leben.
Mehrfachtreffer derselben Stelle dürfen den Nutzungsstatus nicht verändern
oder Zähler künstlich erhöhen.

Für ein Produktionssymbol gilt, auch bei `InternalsVisibleTo`:

| Fundstellen nach Ausschluss von Deklarationsstellen | Ergebnis |
|---|---|
| Mindestens eine produktive Referenz | `live`; kein Dead-Code-Kandidat. |
| Keine produktive, mindestens eine unbekannte Referenz | `undecidable`; kein Dead-Code-Kandidat, Advisory-Status `partial`. |
| Keine produktive oder unbekannte, mindestens eine Testreferenz | Kandidat `test_only`. |
| Keine Referenz | Kandidat `unreferenced`. |

`SymbolFinder` erhält weiterhin die vollständige Solution. Eine
Roslyn-Optimierung für private Symbole ist nur zulässig, wenn sie zum
solutionweiten Ergebnis äquivalent ist. `scopeFiles` darf ausschließlich
Deklarationen begrenzen. Dieselbe Rollenbewertung gilt bei Referenzen
auf Basismethoden und Interface-Member; ein test-only Aufruf über ein
Interface oder eine Basisklasse schützt die Implementierung nicht.
Bestehende Schutzregeln für tatsächliche Interface-/Override-Nutzung und
Framework-Einstiegspunkte bleiben erhalten.

Ein von `TestDetector` übersehenes Testprojekt erzeugt einen
**fehlenden Kandidaten**, weil seine Referenzen als produktiv zählen.
Eine fälschliche Testklassifizierung eines produktiven Aufrufers kann
dagegen einen falschen Kandidaten erzeugen. Das ist eine Grenze der
bestehenden heuristischen Testerkennung und verlangt die agentische
Gegenprüfung; der Dead-Code-Scanner behauptet deshalb nie sichere
Löschbarkeit. Unbekannte Referenzstellen dürfen nicht als unbenutzt
interpretiert werden.

### 2. Projektbezogene API-Policy

Die Konfiguration erhält genau diese Properties und Werte:

```json
{
  "DeadCode": {
    "DefaultApiSurface": "unknown"
  },
  "ProjectOverrides": {
    "PublicSdk": {
      "DeadCode": {
        "ApiSurface": "external_library"
      }
    },
    "Application": {
      "DeadCode": {
        "ApiSurface": "closed_solution"
      }
    }
  }
}
```

`DeadCode.DefaultApiSurface` ist der solutionweite Default.
Fehlt die Property, gilt `unknown`. `ProjectOverrides.<Muster>.DeadCode.ApiSurface`
überschreibt sie für das über `ProjectConfigResolver.ResolveForProject`
aufgelöste Roslyn-Projekt. Ein Override ohne `DeadCode.ApiSurface` erbt
den Default. Die bestehende First-Match-Reihenfolge der
`ProjectOverrides` bleibt gültig. `PathOverrides` ändern die
Projekt-API-Policy nicht.

Die einzigen gültigen Werte sind exakt `unknown`, `closed_solution`
und `external_library`. Groß-/Kleinschreibung, leere Werte und
abweichende Schreibweisen sind ungültig und werden diagnostisch
wie `unknown` behandelt; es gibt keinen stillen Rückfall auf
`closed_solution`. Die Konfigurations-Deserialisierung muss solche Werte
bis zur projektbezogenen Validierung erhalten können. Ein Ladefehler
der gesamten Regelkonfiguration bleibt unter der bestehenden
Verify-Konfigurationsfehlerbehandlung.

| Effektiver Wert | Fachliche Wirkung |
|---|---|
| `external_library` | Effektiv extern sichtbare Symbole sind API-geschützt und werden nicht als Dead-Code-Kandidaten ausgegeben. Nicht extern sichtbare Symbole werden geprüft. |
| `closed_solution` | Auch effektiv extern sichtbare Symbole werden geprüft. Ohne produktive statische Referenz sind sie nur `low` confidence; externe bzw. dynamische Nutzung bleibt Gegenprüfung. |
| `unknown` | `verify` bricht vor Gate- und Advisory-Analyse mit Konfigurationsfehler ab. |

Extern sichtbar wird **semantisch** über Roslyn-`Accessibility` und die
gesamte enthaltende Typkette bestimmt. Ein öffentlicher Member in einem
internen Typ ist nicht extern sichtbar. Öffentliche Typen und Member,
`protected` und `protected internal` (`ProtectedOrInternal`) sind bei
extern sichtbarer Typkette geschützt. `internal` und `private protected`
(`ProtectedAndInternal`) sind es nicht. Auch bei
`external_library` bleiben private und interne Member grundsätzlich
prüfbar; `InternalsVisibleTo` führt weiterhin zu niedriger Sicherheit,
solange externe Friend-Nutzung nicht ausgeschlossen ist.

Für die mitgelieferte `ainetlinter-rules.json` wird
`DeadCode.DefaultApiSurface = closed_solution` ausdrücklich gesetzt.
Das ist eine Aussage über diese Solution, **nicht** der Produktdefault
für fremde Solutions.

### 3. Preflight und Fehler

Vor dem Gate-Scan ermittelt `verify` zunächst die produktiven
Kandidatendokumente seines effektiven Scopes. Es prüft die effektive
`ApiSurface` für jedes darin vertretene Roslyn-Projekt. Projekte ohne
Kandidatendokument und Testprojekte brauchen keine API-Einordnung.

Falls mindestens ein Kandidatenprojekt `unknown` oder einen ungültigen
Wert hat, liefert `verify` **eine** Content-only-Fehlerantwort mit
`isError: true`, `verdict: error` und Code
`DEAD_CODE_API_SURFACE_NOT_CONFIGURED`. Sie enthält alle betroffenen
Projektnamen in stabiler alphabetischer Reihenfolge, den Konfigurationsort
`ainetlinter-rules.json`, die beiden gültigen operativen Werte und ein
kopierbares `ProjectOverrides`-Beispiel für beide Werte. Die Antwort
fordert den Agenten ausdrücklich auf, die Einordnung **beim Nutzer zu
erfragen**, wenn sie aus Projektwissen nicht bereits eindeutig feststeht.
Sie enthält weder Score/Violations noch partielle Dead-Code-Kandidaten.
Eine ungültige projektbezogene Angabe nennt zusätzlich den betroffenen
Feldpfad und Wert.

Der Fehler ist eine Konfigurationsentscheidung, kein Dead-Code-Kandidat.
Er darf nicht im `VerifyAdvisoryProjector` als `unavailable` verschluckt
werden. Andere Analysefehler behalten ihre bisherige Zuständigkeit;
Abbruch durch `CancellationToken` wird nicht als Konfigurationsfehler
umgedeutet.

### 4. Einheitlicher `verify`-Output

Bei gültiger Konfiguration berechnet `verify` den bisherigen
Qualitäts-Gate-Verdict. Dead Code ist eine **vorrangige Advisory** im
selben Content-Ergebnis. Ein Kandidat ändert `verdict`, `score` und
`violationCount` nicht. Die Antwort darf ein Gate-`pass` und zugleich
Dead-Code-Kandidaten enthalten.

Der Dead-Code-Teil enthält im knappen Antwortbudget immer eine
separate Zusammenfassung:

```text
deadCode: status=complete|partial|unavailable; candidates=<N|unknown>;
          testOnly=<N|unknown>; unreferenced=<N|unknown>;
          apiProtected=<N|unknown>; undecidable=<N|unknown>;
          shown=<N>; truncatedBy=<N|unknown>; next=review_now|none
```

`candidates` zählt alle statischen Kandidaten im Deklarationsscope,
auch wenn nur die ersten Einträge gezeigt werden. `apiProtected` zählt
alle im Deklarationsscope wegen `external_library` vor der
Referenzsuche zurückgehaltenen Symbole, unabhängig davon, ob sie sonst
einen Kandidaten ergeben hätten;
`undecidable` zählt Symbole mit unbekannter Referenzrolle.
`status=partial` gilt genau dann, wenn bei vollständig durchlaufenem
Scan unentscheidbare Symbole vorliegen; andernfalls `complete`.
`shown` und `truncatedBy` beziehen sich nur auf Dead-Code-Kandidaten.
Bei `changes` ist `complete` ausschließlich eine Aussage über den
geänderten Deklarationsscope, kein globaler Clean-Claim.

Jeder gezeigte Dead-Code-Kandidat enthält mindestens
`category=dead_code`,
`symbolIdentifier=<h:...>` als opake Kennung aus demselben
Handoff-Mechanismus wie `find_symbol`, nicht aus Anzeigename oder
Dateiposition abgeleitet,
`ref=<relativer Pfad>:<Zeile>`, `usage=test_only|unreferenced`,
`confidence=high|low` und den Grund
`no_production_static_reference`. Bei `test_only` steht die Anzahl
der Testreferenzen dabei. Der Agent erhält außerdem eine kurze
Gegenprüfungsangabe für Reflection, DI, Generatoren, `dynamic`,
Markup/Konfiguration und externe Consumer. Der Text behauptet niemals
„keine Referenzen in der Solution“, wenn Testreferenzen existieren.
API-geschützte Symbole sind keine Kandidateneinträge; ihre Anzahl
bleibt in der Zusammenfassung sichtbar.

Dead-Code-Kandidaten stehen im Advisory-Ranking **vor**
Magic-Value-Advisories; innerhalb von Dead Code zuerst `high`,
dann `low`, danach stabil nach Pfad, Zeile und Symbolkennung.
Das bestehende Gesamt-Evidence-Limit und Content-Budget gelten weiter.
Die Zusammenfassung einschließlich voller Zähler und Trunkierung
darf beim Kürzen von Einträgen nicht verschwinden. `next=review_now`
gilt bei mindestens einem Kandidaten. Ein unentscheidbarer Scope ohne
Kandidaten liefert `next=none` und `status=partial`.

Ein Scannerfehler ohne Konfigurationsursache liefert
`status=unavailable` mit knapper Ursache. Noch nicht bestimmtere
Zähler und `truncatedBy` lauten `unknown`, `shown=0` und
`next=none`. Der Gate-Verdict folgt weiterhin dem Gate-Kern.
`unavailable` darf nicht als „kein Dead Code“ formuliert werden.
Die bestehende übergeordnete Advisory-`completeness` lautet
`complete` nur, wenn Dead Code und Magic Values vollständig sind;
`unavailable` bei nicht ausführbarem Dead-Code-Scan und sonst
`partial`, wenn mindestens eine Advisory unvollständig ist.

### 5. Agentische Verwendung

Ein Agent ruft `verify` einmal für den passenden Scope auf und
behandelt `deadCode.next=review_now` als unmittelbaren Arbeitsauftrag
nach den Gate-Verstößen. Er benötigt keinen zweiten Linter- oder
Dead-Code-Aufruf. Für **gezielte Gegenprüfung einzelner Kandidaten**
darf er semantische Folgetools wie `find_references`,
`get_feature_context` und `search_pattern` nutzen. Dabei prüft er
insbesondere Test-only-Nutzung, Interface-/Override-Aufrufe,
Reflection, DI, Generatoren, `dynamic`, Razor/Markup,
Konfiguration und mögliche externe Consumer.

Er klassifiziert jeden bearbeiteten Kandidaten als bestätigt,
falsch positiv oder unklar. Bestätigten entbehrlichen Code kann er
im normalen Änderungsworkflow entfernen und anschließend erneut
`verify` ausführen. Bei falschem Positiv behebt er die
Dead-Code-Erkennung oder dokumentiert eine begründete symbolnahe
Suppression; bei unklarer Laufzeit-/API-Nutzung holt er die fehlende
fachliche Information ein. `confidence=high` ist niemals ein
Löschbeweis. Ein `verify`-Fehler wegen `ApiSurface` wird vor jeder
Dead-Code-Bearbeitung durch Konfiguration oder Nutzerentscheidung
aufgelöst.

## Akzeptanz

- Ein `internal`-Produktionsmember mit ausschließlich Testaufruf,
  auch über `InternalsVisibleTo`, erscheint als `test_only`.
  Derselbe Member mit mindestens einem produktiven Aufruf erscheint
  nicht als Kandidat.
- Ein produktiver Aufruf aus einem anderen Solution-Projekt zählt;
  ein testseitiger Interface- oder Basismethodenaufruf zählt nicht.
  Eine produktive Interface-/Override-Nutzung schützt weiterhin.
- Die Klassifizierung deckt Referenzen aus erkannten Testprojekten,
  Testpfaden, Produktionsprojekten und Friend-Projekten ab.
  Eine nicht zuordenbare Referenz führt zu `undecidable`, nicht zu
  einem Dead-Code-Kandidaten.
- `external_library` schützt effektiv öffentliche, geschützte und
  `protected internal`-Symbole, nicht öffentliche Member in
  internen Typen. `closed_solution` meldet ungenutzte öffentliche
  Symbole mit `low` confidence.
- Fehlende oder ungültige Policy in irgendeinem Kandidatenprojekt
  liefert den definierten `verify`-Fehler mit allen Projekten und
  ohne Gate- oder Advisory-Teilergebnis. Ein anderes korrekt
  konfiguriertes Projekt wird dadurch nicht falsch klassifiziert.
- `verify(changes)` und `verify(solution)` liefern bei identischem
  Deklarationsscope dieselbe Liveness-Beurteilung. `solution`
  enthält Dead-Code-Advisories; `changes` behauptet keine
  globale Vollständigkeit.
- Gate-`pass` bleibt bei Dead-Code-Kandidaten möglich. Kandidaten
  erscheinen vor Magic Values. Vollständige Zähler,
  `status` und Trunkierung bleiben auch bei knappen Antworten
  sichtbar. `symbolIdentifier` funktioniert unverändert als
  Folgeparameter von `find_references`, auch bei Linked Files und
  gleichnamigen Symbolen in verschiedenen Projekten. Die Antwort nennt für
  test-only ausdrücklich die fehlende **produktive** statische
  Referenz.
- Whitelist, begründete Suppression, Razor-Evidenz,
  Interface-/Override-Schutz und Content-only-Vertrag funktionieren
  unverändert.

## Verifikation und Dokumentation

Die spätere Umsetzung erhält zuerst einen roten xUnit-v3-Regressionsfall
für test-only verwendeten Produktionscode. FastTests decken
Referenzrollen, Linked Files, Interface/Override, API-Sichtbarkeit,
Projekt-Overrides, Preflight, Verify-Projektion, Ranking,
Trunkierung und Fehlertext ab. Ein repräsentativer
Verify-Vertragstest prüft `isError`, Content-only,
Projektliste, Beispiel und fehlende Teilergebnisse. Der Fall
`MarkdownBuilder.BulletList` wird als Regression mit seinem
Testaufruf abgebildet.

Die Implementierung synchronisiert `Docs/linter/configuration.md`,
`Docs/linter/cli.md`, die MCP-/Verify-Dokumentation,
`ainetlinter-rules.json` und betroffene Agentenregeln. Dokumentiert
werden der eine `verify`-Aufruf, beide Scopes, produktive
Referenzsemantik, API-Werte und Default, Fehlerreaktion,
Priorisierung sowie die Grenze zwischen Advisory und Löschentscheidung.
Die Prüfzeitpunkte und Testgates richten sich ausschließlich nach
`.agents/rules/AiNetLinter-Richtlinien.mdc` und
`AiNetLinter-TestRichtlinien.mdc`.

Dieses Dokument bleibt `draft` bis zur ausdrücklichen Freigabe des
Nutzers. Die Freigabe dieses Konzepts startet weder Roadmap noch
Umsetzung automatisch.
