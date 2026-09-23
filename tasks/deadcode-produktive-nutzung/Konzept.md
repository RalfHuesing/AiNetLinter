---
status: draft
execution_mode: autonomous
open_questions: []
---

# Konzept: Dead Code anhand produktiver Nutzung erkennen

## 1. Ziel

AiNetLinter soll Dead Code als Code erkennen, der innerhalb der gesamten
Solution keine produktive Nutzung besitzt.

Die Solution bleibt immer der vollständige statische Suchraum. Es gibt keinen
fachlichen Modus „nur Projekt“ versus „solutionweit“. Entscheidend ist allein,
welche Fundstellen als produktive Referenzen gelten und welche Deklarationen
wegen einer ausdrücklich konfigurierten externen API geschützt werden.

Die zentrale Regel lautet:

> Eine Referenz aus einem Test ist keine produktive Nutzung. Eine Referenz aus
> produktivem Code innerhalb der Solution ist eine produktive Nutzung.

Eine `public`- oder `protected`-Deklaration ist zusätzlich nur dann von der
Dead-Code-Aussage auszunehmen, wenn das zugehörige Projekt eine externe API
bereitstellt. Diese API-Oberfläche muss definierbar sein; sie darf nicht
unkritisch aus jedem einzelnen `public`-Modifier abgeleitet werden.

Das Ergebnis bleibt eine statische, vorsichtige Analyse und keine automatische
Löschentscheidung. Reflection, DI, Source Generatoren, `dynamic` und andere
Laufzeitbindungen bleiben Gegenprüfungen beziehungsweise Unsicherheiten.

## 2. Problem und aktuelle Lücke

Die bestehende Implementierung untersucht Kandidatendokumente ohne Tests, sucht
Referenzen für nicht-private Symbole jedoch über die gesamte Roslyn-Solution.
`IncludeTests = false` begrenzt damit derzeit den Kandidatenbestand, nicht den
Referenzbestand.

Folge:

```text
Produktionsmember BulletList
    keine produktive Referenz
    Referenzen ausschließlich aus MarkdownBuilderTests
    -> SymbolFinder findet Referenzen
    -> Member gilt als benutzt
    -> kein Dead-Code-Kandidat
```

Das widerspricht der fachlichen Definition. Würde `BulletList` gelöscht, würde
nicht der Produktivcode, sondern nur sein direkter Unit-Test-Aufruf den Build
brechen. Genau solche test-only verwendeten Produktionsmember sollen sichtbar
werden.

Die aktuelle Referenzsuche liegt in
`src/AiNetLinter/Mcp/Tools/Verify/DeadCode/DeadCodeAdvisoryScanner.cs`.
Die Kandidatenauswahl liegt dort separat in `CollectCandidateDocuments` und
`ShouldScanProject`. Die Korrektur muss diese beiden Ebenen semantisch
zusammenführen, ohne den solutionweiten Suchraum aufzugeben.

## 3. Fachliche Definition

### 3.1 Suchraum

Für jedes geprüfte Symbol wird weiterhin die gesamte geladene Solution als
Roslyn-Suchraum verwendet:

- alle Projekte der Solution;
- alle passenden Source-Dokumente;
- Projektgrenzen und Referenzbeziehungen gemäß Roslyn;
- keine künstliche Begrenzung auf das deklarierende Projekt.

Das ist notwendig, damit ein produktiver Aufrufer aus einem anderen Projekt
den deklarierenden Code zuverlässig am Leben hält.

### 3.2 Produktive Referenz

Eine gefundene Referenz ist produktiv, wenn ihre Referenzstelle in einem
Produktionsprojekt beziehungsweise einer produktiven Quelldatei liegt.

Für ein Produktionssymbol gelten insbesondere:

- Referenzen aus Testprojekten zählen nicht.
- Referenzen aus typischen Testpfaden zählen nicht.
- Referenzen aus produktiven Projekten zählen.
- Eine produktive Referenz aus einem anderen Projekt zählt unabhängig davon,
  ob das deklarierende Projekt selbst eine Bibliothek oder ein Executable ist.
- Eine `InternalsVisibleTo`-Beziehung macht eine Testreferenz nicht produktiv.
  Eine Referenz aus einem nicht als Test erkannten Friend-Projekt bleibt eine
  produktive Referenz.

Die Klassifizierung soll anhand der vorhandenen Projekt- und Pfaderkennung
erfolgen, insbesondere über `TestDetector` und die vorhandene Dokumenten-
 beziehungsweise Scope-Logik. Eine bloße Textsuche nach Testnamen ist kein
Ersatz für die semantische Projektklassifizierung.

### 3.3 Testcode

Tests werden nicht als produktive Verbraucher behandelt. Das gilt auch dann,
wenn ein Test absichtlich einen `internal`-Member direkt aufruft und über
`InternalsVisibleTo` Zugriff erhält.

`IncludeTests` darf weiterhin steuern, ob Testdokumente selbst als
Dead-Code-Kandidaten untersucht werden. Es darf jedoch nicht dazu führen,
dass Testreferenzen Produktionssymbole vor dem Dead-Code-Hinweis schützen.

Die primäre Dead-Code-Aussage bezieht sich auf Produktionscode. Ein optionaler
Testcode-Scan ist ein separater Wartungsfall und darf nicht die Aussage über
Produktionscode verändern.

### 3.4 API-Schutz

Eine externe API ist eine Eigenschaft eines Projekts beziehungsweise einer
explizit konfigurierten Projektoberfläche, nicht automatisch jedes `public`
Symbol in jeder Solution.

Empfohlene API-Semantik:

| API-Status des Projekts | `public`/`protected` ohne Solution-Referenz |
|---|---|
| `external_library` | Extern sichtbare Symbole sind API-geschützt; externe Consumer sind möglich. |
| `closed_solution` | Extern sichtbare Symbole dürfen als Kandidat erscheinen, aber nur mit niedriger Sicherheit und Laufzeit-Gegenprüfung. |
| `unknown` | Konfiguration unvollständig; der Dead-Code-MCP-Aufruf bricht mit einem verständlichen Konfigurationsfehler ab. |

Der Default soll sicher sein: `unknown`. `unknown` ist dabei kein zulässiger
operativer Analysezustand, sondern ein Sentinel für „Projekt-API noch nicht
entschieden“. Für AiNetLinter selbst wird die eigene Solution explizit als
`closed_solution` konfiguriert. Ein anderes Projekt kann seine DLL-Projekte
als `external_library` markieren.

Die Information wird nicht als Aufrufparameter und nicht als globaler
Boolean geführt, sondern als Teil der geladenen Projektkonfiguration. Ein
Boolean `PublicApi: true/false` wäre zu grob und hätte keinen sicheren dritten
Zustand für unbekannte externe Consumer. Außerdem würde `true` entweder alle
öffentlichen Symbole schützen oder eine zusätzliche Ausnahmelogik benötigen.
Die Policy macht die Sicherheitsentscheidung explizit und wird über die
vorhandenen `ProjectOverrides` automatisch anhand des Roslyn-Projekts
aufgelöst. Bleibt der effektive Wert für ein im Dead-Code-Scope liegendes
Projekt `unknown`, darf der Scanner kein Ergebnis erzeugen.

Geschützt werden mindestens:

- öffentliche Typen und Member;
- `protected`-Member;
- extern sichtbare `protected internal`-Member.

`private protected` und rein `internal` bleiben grundsätzlich analysierbar,
werden aber durch echte produktive Friend-Projekte am Leben gehalten. Die
genaue Roslyn-Accessibility muss an dieser Stelle semantisch ausgewertet
werden; Stringvergleiche auf Modifier sind unzulässig.

Die Konfiguration soll projektbezogen sein und einen Solution-weiten Default
besitzen. Das nutzt die bestehende Override-Struktur, damit die Policy nicht
bei jedem Analyseaufruf mitgegeben oder von einem Agenten erinnert werden
muss. Ein konzeptioneller Vertrag ist:

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
    "AiNetLinter": {
      "DeadCode": {
        "ApiSurface": "closed_solution"
      }
    }
  }
}
```

`external_library` schützt in der ersten Umsetzung alle extern sichtbaren
Symbole des Projekts. Eine feinere Eingrenzung über Namespaces oder einzelne
Klassen ist ausdrücklich nicht Bestandteil dieses Scopes. Die konkreten
Property-Namen müssen bei der Umsetzung an die bestehende
Konfigurationsstruktur und deren Namenskonventionen angepasst werden; die
Semantik dieses Vertrags bleibt verbindlich.

### 3.5 Laufzeit- und externe Nutzung

Statische Referenzlosigkeit beweist nicht, dass ein Symbol gefahrlos gelöscht
werden kann. Die Analyse muss daher weiterhin zwischen folgenden Aussagen
unterscheiden:

- **keine produktive statische Referenz gefunden:** Kandidat für Dead Code;
- **API-geschützt:** keine sichere Aussage wegen möglicher externer Consumer;
- **Laufzeitbindung möglich:** statisch unreferenziert, aber Gegenprüfung nötig;
- **Konfiguration fehlt:** Dead-Code-Scan abgebrochen; der MCP-Aufrufer muss
  die API-Einordnung beim Nutzer erfragen.

Bekannte Framework- und Einstiegspunktmarker bleiben über die bestehende
Whitelist geschützt. Reflection, DI, Generatoren und `dynamic` bleiben als
Countercheck im Ergebnis sichtbar. Ein `public`-Symbol außerhalb einer
geschützten externen API darf bei `closed_solution` weiterhin als Kandidat
erscheinen, sollte wegen möglicher Laufzeitbindung aber mindestens niedrige
Sicherheit erhalten.

### 3.6 Fehlende API-Konfiguration als MCP-Fehler

Vor Beginn eines Dead-Code-Scans wird die effektive `ApiSurface`-Konfiguration
für jedes Projekt im angeforderten Kandidatenscope aufgelöst. Ist mindestens
ein solcher Wert `unknown`, bricht der MCP-Aufruf vor der eigentlichen
Referenzanalyse mit einem Fehler ab.

Der Fehler muss mindestens enthalten:

- die Kennung beziehungsweise den Namen jedes nicht konfigurierten Projekts;
- den Hinweis, dass `unknown` nicht für einen Dead-Code-Scan genügt;
- den erwarteten Konfigurationsort in `ainetlinter-rules.json`;
- ein kopierbares Beispiel für `closed_solution` und
  `external_library`;
- die Aufforderung an den aufrufenden Agenten, die Einordnung beim Nutzer zu
  erfragen.

Der Aufruf darf in diesem Fall keine partiellen Dead-Code-Kandidaten liefern.
Das ist ein Konfigurationsfehler des Analyseauftrags und kein leerer
Analyseausschnitt. Die MCP-Fehlerantwort bleibt dabei im geltenden
Content-only-Vertrag und setzt `isError`.

## 4. Architektur und betroffene Bereiche

### 4.1 Source of Truth

Die fachliche Entscheidung liegt in der Dead-Code-Analyse und nicht im
Formatter:

- `DeadCodeAdvisoryScanner` bestimmt Kandidaten und Referenzstatus.
- `DeadCodeFilters` bestimmt Art und Accessibility-Filter.
- `DeadCodeWhitelist` schützt bekannte Framework- und Einstiegspunktfälle.
- `DeadCodeSuppression` verarbeitet bewusste symbolnahe Ausnahmen.
- `TestDetector` und die Dokumentklassifizierung bestimmen, ob eine
  Referenz produktiv oder testseitig ist.
- Die Dead-Code-Konfiguration bestimmt die API-Oberfläche je Projekt.
- `VerifyAdvisoryProjector` projiziert lediglich das Ergebnis in die
  Verify-Antwort und darf die Fachsemantik nicht erneut interpretieren.

### 4.2 Referenzprüfung

Die bestehende Roslyn-Referenzsuche bleibt solutionweit. Nach der Suche werden
Referenzstellen semantisch klassifiziert:

```text
Symbol deklarieren
    -> Whitelist / Suppression / Accessibility / API-Schutz prüfen
    -> SymbolFinder über die gesamte Solution
    -> jede ReferenceLocation klassifizieren
         Testreferenz       -> für Produktionssymbol ignorieren
         Produktionsreferenz -> Symbol ist produktiv benutzt
         unklassifizierbar   -> Ergebnis nicht sicher als dead behaupten
    -> keine produktive Referenz
         -> Dead-Code-Kandidat gemäß API- und Laufzeitregeln
```

Die Filterung muss auf der tatsächlichen Referenz-Dokument- beziehungsweise
Projektidentität basieren. Eine globale Nachbearbeitung der Textausgabe oder
ein Ausschluss des gesamten Testprojekts aus der Solution ist nicht zulässig.

### 4.3 Typen und Member

Die bisherige Containerlogik bleibt erhalten, wird aber mit produktiven
Referenzen bewertet:

- Ein vollständig unproduktiver privater Typ darf weiterhin als Container
  erkannt werden.
- Ein Typ mit produktiver Nutzung darf nicht allein deshalb als dead gelten,
  weil seine einzelnen Member intern nicht von Tests aufgerufen werden.
- Öffentliche API-Typen eines `external_library`-Projekts sind geschützt.
- Private und interne Member eines API-Typs können weiterhin Dead-Code-
  Kandidaten sein, wenn keine produktive Referenz existiert.
- Interface-Implementierungen, Overrides und Framework-Lebenszyklus bleiben
  über die bestehende Sonderlogik geschützt.

## 5. Muss-Kriterien

- Die Referenzsuche bleibt immer solutionweit.
- Testreferenzen halten Produktionssymbole nicht am Leben.
- `IncludeTests` steuert Kandidatendokumente, nicht die Definition
  produktiver Nutzung eines Produktionssymbols.
- Eine produktive Referenz aus jedem anderen Solution-Projekt hält ein Symbol
  am Leben.
- Friend-Referenzen aus Produktionsprojekten zählen; Friend-Referenzen aus
  Testprojekten zählen für Produktionssymbole nicht.
- Die API-Oberfläche ist explizit konfigurierbar, projektbezogen und wird nicht
  als wiederkehrender Analyseaufruf-Parameter verlangt.
- `external_library` schützt extern sichtbare API-Symbole vor einer
  Dead-Code-Aussage.
- `closed_solution` ermöglicht die Prüfung öffentlicher Symbole innerhalb
  einer geschlossenen Solution.
- `unknown` führt beim Dead-Code-MCP-Aufruf zu einem verständlichen
  Konfigurationsfehler statt zu einem geschützten oder partiellen Ergebnis.
- Der Fehler nennt alle betroffenen Projekte und zeigt den erwarteten
  `ProjectOverrides`-Eintrag mit der Aufforderung zur Nutzerentscheidung.
- Ungültige oder nicht auflösbare API-Konfiguration fällt sicher auf
  `unknown` zurück und wird diagnostisch sichtbar.
- Reflection, DI, Generatoren, `dynamic` und externe Consumer bleiben als
  Gegenprüfung beziehungsweise Unsicherheit sichtbar.
- Whitelist und Suppression behalten ihre bestehende Bedeutung.
- Der Output unterscheidet zwischen fehlender produktiver Referenz,
  API-Schutz und nicht entscheidbarer Laufzeitbindung.
- Es werden keine Symbole automatisch gelöscht oder umgeschrieben.

## 6. Akzeptanzkriterien

### 6.1 Produktions- und Testreferenzen

- Ein `internal`-Produktionsmember, das ausschließlich aus einem Unit-Test
  aufgerufen wird, wird als Dead-Code-Kandidat gefunden.
- Ein `internal`-Produktionsmember mit einem produktiven Aufruf und beliebigen
  Testaufrufen wird nicht als dead gemeldet.
- Ein Produktionsmember mit Referenzen aus zwei verschiedenen
  Produktionsprojekten bleibt lebendig.
- Ein Testprojekt, das über `InternalsVisibleTo` auf einen Produktionsmember
  zugreift, ändert dessen Produktions-Liveness nicht.
- Die gleiche Datei- oder Projektreferenz wird nicht doppelt als produktive
  Nutzung und Testnutzung gewertet.
- Ein Test-only Helper wird nur dann als Testcode-Kandidat untersucht, wenn
  der optionale Testscan ausdrücklich aktiviert ist.

### 6.2 API-Oberfläche

- Ein unreferenzierter öffentlicher Member in einem `external_library`-
  Projekt wird nicht als Dead Code gemeldet.
- Ein unreferenzierter geschützter Member in einem `external_library`-
  Projekt wird nicht als Dead Code gemeldet.
- Ein unreferenzierter öffentlicher Member in einem `closed_solution`-
  Projekt kann als Kandidat erscheinen und wird nicht fälschlich als sicher
  löschbar dargestellt.
- Ein Dead-Code-Scan mit einem `unknown`-Projekt bricht mit `isError` ab und
  liefert keine Kandidaten.
- Der Fehler nennt das unbekannte Projekt, verweist auf
  `ProjectOverrides.<Projekt>.DeadCode.ApiSurface` und fordert die
  Nutzerentscheidung an.
- Die Konfiguration eines einzelnen API-Projekts beeinflusst nicht die
  Klassifizierung anderer Projekte.
- Der Default ohne API-Konfiguration ist sicher und erzeugt keine
  unbegründeten Public-API-Löschvorschläge.
- Ein `external_library`-Projekt schützt alle extern sichtbaren Symbole.
- Eine identische Konfiguration lässt sich über einen Projekt-Override
  automatisch für jedes betroffene Projekt verwenden, ohne CLI- oder
  MCP-Aufrufparameter.

### 6.3 Sonderfälle

- Eine echte Interface-Nutzung schützt die konkrete Implementierung weiterhin.
- Ein Override mit produktiver Nutzung der Basismethode bleibt geschützt.
- Whitelist-Attribute und begründete Suppressions funktionieren unverändert.
- Nicht entscheidbare Referenzstellen führen nicht zu einem unberechtigten
  High-Confidence-Fund.
- Die Antwort nennt bei einem Test-only Fall ausdrücklich, dass keine
  produktive Referenz gefunden wurde.

### 6.4 Verify-Integration

- `verify` projiziert die Dead-Code-Kandidaten ohne erneute, abweichende
  Test- oder API-Semantik.
- `scopeFiles` begrenzt weiterhin die geprüften Deklarationen, nicht den
  solutionweiten Referenzsuchraum.
- Ein leerer Kandidatenscope wird weiterhin als Scope-Einschränkung und nicht
  als globaler Clean-Claim ausgewiesen.
- Die bestehenden Content-only-, Advisory- und Ranking-Verträge bleiben
  erhalten.

## 7. Fehler-, Unsicherheits- und Fallback-Semantik

Die Analyse darf bei fehlender Klassifizierbarkeit keinen sicheren
Löschvorschlag erzeugen.

- Ist ein Referenzprojekt sicher als Testprojekt erkannt, wird die Referenz
  für Produktionsliveness ignoriert.
- Ist ein Referenzprojekt sicher produktiv, zählt die Referenz.
- Kann eine Referenzstelle nicht zuverlässig klassifiziert werden, wird der
  Kandidat als nicht entscheidbar beziehungsweise mit niedriger Sicherheit
  ausgegeben oder aus dem sicheren Ergebnis zurückgehalten.
- Ist die API-Oberfläche eines im Scan-Scope liegenden Projekts unbekannt,
  bricht der Dead-Code-MCP-Aufruf vor der Analyse mit `isError` ab.
- Fehler beim Laden der Dead-Code-Konfiguration führen nicht zu einem
  stillen Wechsel auf `closed_solution`; der Fehler wird diagnostisch sichtbar
  und fordert eine Nutzerentscheidung an.
- Bestehende Roslyn-, Target-, Snapshot- und Analysefehler bleiben unter der
  bisherigen Fehlerzuständigkeit.

Ein einmaliges Nicht-Erkennen eines Testprojekts darf nicht still dazu führen,
dass ein Produktionsmember als sicher tot gemeldet wird. Die Klassifizierung
  muss daher entweder eine belastbare Testdetektion oder eine konservative
  Unsicherheitsmarkierung liefern.

## 8. Alternativen und Empfehlung

### 8.1 API-Konfigurationsvarianten

| Variante | Vorteil | Nachteil | Bewertung |
|---|---|---|---|
| Globales `PublicApi: true/false` | Sehr wenig Konfiguration | Kein sicherer `unknown`-Zustand; schützt in Bibliotheken zu viel oder lässt in geschlossenen Solutions zu viel offen | Nicht empfohlen |
| Projektstatus ohne Feinabgrenzung | Automatisch, wenig Pflege, passt zu `ProjectOverrides` | Bei gemischter API werden öffentliche Nicht-API-Symbole mitgeschützt | Empfohlener Default |
| Namespace- oder Klassen-Allowlist | Feinere Eingrenzung der API | Zusätzliche Pflege, Drift bei Umstrukturierungen und höheres Fehlkonfigurationsrisiko | Nicht im aktuellen Scope |

Die Empfehlung lautet daher: `ApiSurface` projektbezogen und automatisch aus
`ProjectOverrides` auflösen, mit sicherem globalem Default `unknown`. Für eine
vollständig externe Bibliothek genügt `external_library` ohne weitere Pflege.
Eine einzelne globale Boolean-Eigenschaft wird nicht eingeführt.

### Alternative A: Tests aus der gesamten Solution entfernen

Nicht empfohlen. Dadurch gingen produktive Referenzen aus einer normalen
Solution-Sicht verloren, und Projekte, die Tests oder Testfixtures als
kompilierte Abhängigkeit enthalten, würden falsch bewertet. Außerdem wäre der
Suchraum nicht mehr wirklich solutionweit.

### Alternative B: `IncludeTests` auch auf Referenzen anwenden

Als alleinige Lösung nicht ausreichend. Das Flag ist heute ein
Kandidatendokument-Filter. Es vermischt zwei unabhängige Fragen:

1. Welche Deklarationen werden untersucht?
2. Welche Referenzen gelten als produktive Nutzung?

Die Referenzrolle muss unabhängig klassifiziert werden.

### Alternative C: Testreferenzen nur als Low-Confidence-Nutzung behandeln

Besser als das aktuelle Verhalten, aber für die gewünschte Definition nicht
präzise genug. Ein Test-only Member würde weiterhin nicht als Dead Code
erscheinen, obwohl seine produktive Nutzung fehlt.

### Alternative D: Solutionweite Referenzsuche mit Produktionsfilter

Empfohlen. Sie erhält den vollständigen Cross-Project-Suchraum und trennt die
fachlich richtige Bedeutung der Fundstelle. Zusammen mit einer expliziten,
projektbezogenen API-Oberfläche ist sie für AiNetLinter und externe DLL-
Projekte gleichermaßen verwendbar.

## 9. Non-Goals

- Keine automatische Löschung von Dead-Code-Kandidaten.
- Keine Änderung an Roslyn-Referenzauflösung oder Symbolidentität.
- Kein „nur lokales Projekt“-Modus.
- Kein „solutionweit versus nicht solutionweit“-Schalter.
- Keine Annahme, dass jeder `public`-Modifier automatisch externe API ist.
- Keine automatische, unzuverlässige Erkennung externer Consumer aus
  `OutputType`, Paketmetadaten oder Dateinamen allein.
- Keine vollständige Laufzeit- oder Reflection-Analyse.
- Keine Garantie, dass ein Low-Confidence-Kandidat gefahrlos gelöscht werden
  kann.
- Keine Namespace- oder Klassen-Allowlist für die Public API in dieser ersten
  Umsetzung.
- Keine Änderung der Linter-Regeln für Codequalität außerhalb der
  Dead-Code-Advisory-Pipeline.
- Keine Aufwertung von Testcode zu Produktionscode.

## 10. Verifikation

Die spätere Umsetzung muss mindestens folgende Nachweise liefern:

- Unit-Tests für reine Referenzklassifizierung:
  Produktionsreferenz, Testreferenz, Friend-Produktionsreferenz,
  Friend-Testreferenz und unbekannte Referenz.
- Unit-Tests für `external_library`, `closed_solution` und `unknown` je
  Projekt sowie für öffentliche, geschützte, interne und private Symbole.
- Konfigurations- und Integrations-Tests für den Solution-weiten Default,
  Projekt-Overrides und die automatische Zuordnung zur richtigen Roslyn-
  Projektkonfiguration.
- Vertragstest für einen Dead-Code-MCP-Aufruf mit `unknown`: `isError`,
  betroffene Projektnamen, kopierbarer Konfigurationshinweis und keine
  Dead-Code-Kandidaten.
- Tests für Interface-Implementierungen, Overrides, Whitelist und Suppression.
- Verify- beziehungsweise Integrationstests mit realer Solution und
  mindestens einem Testprojekt.
- Regressionstest für `MarkdownBuilder.BulletList`: nur Testreferenzen dürfen
  den Produktionsmember nicht länger als benutzt markieren.
- Prüfung, dass Testreferenzen weiterhin im solutionweiten Roslyn-Suchraum
  gefunden, aber fachlich korrekt klassifiziert werden.
- Prüfung der Antworttexte, insbesondere „keine produktiven Referenzen“ statt
  einer irreführenden Aussage „keine Referenzen in der Solution“.
- Vollständige Non-Stress-Testgates, `dotnet build` und der projektweite
  MCP-Verify-Gate gemäß `AGENTS.md` nach Produktionsänderungen.

Für dieses Konzept selbst sind wegen der Dokumentationsausnahme kein Build und
keine Tests erforderlich. Eine Referenzprüfung, `git diff --check` und eine
saubere Diff-Prüfung reichen aus.

## 11. Dokumentationsbedarf

Bei der späteren Implementierung sind bei neuen Konfigurations- oder
CLI-Optionen zu synchronisieren:

- `Docs/linter/configuration.md` mit API-Surface-Werten und Defaults;
- `Docs/linter/cli.md` mit Dead-Code-Parametern und der Testsemantik;
- `ainetlinter-rules.json` beziehungsweise die aktive Konfigurationsstruktur;
- MCP-/Verify-Dokumentation mit solutionweitem Suchraum,
  produktiver Referenzdefinition, API-Schutz und Unsicherheitssemantik.

Zusätzlich muss die Dokumentation die automatische Auflösung über
`ProjectOverrides` erklären. Ein Analyseaufruf darf die Projekt-API nicht als
vergesslichen Pflichtparameter voraussetzen.

Die Dokumentation muss ausdrücklich festhalten, dass `IncludeTests = false`
nicht bedeutet, dass Testprojekte aus der Solution entfernt werden. Es bedeutet
für Produktions-Dead-Code, dass Testreferenzen keine produktive Nutzung sind.

## 12. Release-Gate

Das Vorhaben ist erst umsetzungsfertig, wenn:

- Produktions- und Testreferenzen im selben solutionweiten Suchraum getrennt
  klassifiziert werden;
- die API-Oberfläche pro Projekt definierbar ist und ein sicherer Default
  existiert;
- Test-only verwendete Produktionsmember als solche gefunden werden;
- externe API-Symbole nicht fälschlich als Dead Code gemeldet werden;
- unbekannte Laufzeit- und API-Bindungen nicht als sichere Löschbehauptung
  erscheinen;
- die Verify-Ausgabe die produktive Referenzsemantik korrekt beschreibt;
- ein Dead-Code-Aufruf mit nicht konfigurierter API-Oberfläche deterministisch
  mit `isError` und einer handlungsfähigen Nutzeraufforderung abbricht;
- die vollständigen produktionsbezogenen Test-, Build- und MCP-Gates grün
  sind;
- die Konfigurations-, CLI- und MCP-Dokumentation synchronisiert ist.

Der Status dieses Dokuments bleibt `draft`, bis der Nutzer das Konzept
ausdrücklich zur Umsetzung freigibt. Die Freigabe startet die Umsetzung nicht
automatisch.
