---
status: draft
type: konzept
project_kind: brownfield
estimated_scope: large
rules_dir: .agents/rules
last_updated: 2026-09-07
open_questions: []
depends_on: []
related_tasks: []
supersedes: null
---

# Konzept: Einheitliches MCP-Analyseziel für Solutions und Assemblies

## Status und Charakter

Dieses Dokument ist ein Diskussionsentwurf. Es beschreibt ein bewusst inkompatibles, vereinfachtes MCP-Zielmodell. Es ist noch keine Implementierungsfreigabe.

Die zentrale Produktentscheidung lautet:

> Für einen Agenten ist eine Quellcode-Solution und eine zu analysierende Assembly dasselbe: ein Analyseziel. Die technische Herkunft bleibt im Analysekontext transparent sichtbar.

## Ziel

Der MCP-Server soll ohne zusätzliche Projektmanifest-Datei auskommen. Ein Agent übergibt einen absoluten Pfad zu einer Solution oder Assembly. Der Server erkennt den Zieltyp selbst, erzeugt bei einer Assembly intern den notwendigen dekompilierten Roslyn-Kontext und stellt danach dieselbe grundsätzliche Navigationsoberfläche bereit.

Die Einrichtung eines Quellcodeprojekts soll auf eine klare Konvention reduziert werden:

```text
MyProject/
  MyProject.slnx
  ainetlinter-rules.json   # optional
```

Fehlt die Regeldatei, ist der Kontext trotzdem gültig und läuft im Navigations-/Analysemodus ohne aktive Lint-Regeln.

## Harte Schnitte

Die folgenden Altverträge werden nicht kompatibel weitergeführt:

- `targetType` entfällt aus allen öffentlichen MCP-Toolparametern.
- `ainetlinter.project.json` entfällt vollständig als Nutzer- und Serververtrag.
- Ein generiertes `<assembly-name>.project.json` wird nicht erzeugt.
- Die bisherige Pflicht, `solution` und `rules` über eine Projektdefinition zu liefern, entfällt.
- Alte Projektinitialisierungs- und Fehlerpfade wie `PROJECT_NOT_INITIALIZED` sind im neuen Modell nicht mehr maßgeblich.
- Der Zieltyp wird ausschließlich aus dem übergebenen Pfad und der Dateiendung erkannt.

Der Server darf intern Dateien für Roslyn und die Dekompilierung erzeugen. Diese Dateien sind Implementierungsdetails und kein weiterer Agentenvertrag.

## Neuer Eingabevertrag

Der öffentliche Zielparameter bleibt `targetPath`, erhält aber ausschließlich einen absoluten lokalen Pfad.

Unterstützte direkte Eingaben:

- `.slnx` oder `.sln`: Quellcode-Solution
- `.dll` oder `.exe`: Assembly, die im Decompiled-Modus analysiert wird

Ein gültiger `targetPath` muss auf eine vorhandene Datei mit einer unterstützten Endung zeigen. Verzeichnisse sind kein Zielvertrag; der Server durchsucht sie nicht nach einer passenden Solution.

Die Auflösung erfolgt ausschließlich anhand des vorhandenen Pfads und seiner Endung. Bei unbekannter Endung, fehlender Datei oder relativem Pfad liefert der Server einen deterministischen Invalid-Argument-Fehler.

`targetPath` bezeichnet immer die konkrete Eingabedatei, nicht den Projektroot:

```text
targetPath = C:\\repo\\MyProject.slnx
targetPath = C:\\libs\\ThirdParty.dll
```

Damit muss ein Agent nicht aus einem Verzeichnisinhalt erraten, welche Solution gemeint ist. Die kanonische Identität des Source-Kontexts basiert auf der aufgelösten Solution-Datei; die kanonische Identität des Decompiled-Kontexts basiert auf der Assemblydatei und ihrem Inhalt.

## Agentenvertrag

Der Agent soll für jede zielgebundene MCP-Abfrage denselben einfachen Vertrag verwenden:

```json
{
  "targetPath": "C:\\repo\\MyProject.slnx"
}
```

oder:

```json
{
  "targetPath": "C:\\libs\\ThirdParty.dll"
}
```

Die Antwort jedes zielgebundenen Tools muss, direkt oder über den gemeinsamen Analyse-Envelope, ausreichend Kontext liefern:

```json
{
  "analysis": {
    "inputPath": "C:\\libs\\ThirdParty.dll",
    "origin": "decompiled",
    "rulesConfigured": false,
    "lintStatus": "unsupported",
    "analysisMode": "navigation-only",
    "completeness": "partial",
    "capabilities": {
      "symbolNavigation": true,
      "lintViolations": false,
      "gitImpact": false
    }
  }
}
```

Der Agent darf nicht aus einer leeren Ergebnisliste ableiten müssen, ob die Analyse vollständig war, die Capability fehlte oder schlicht kein Treffer existierte. `origin`, `completeness`, `rulesConfigured` und relevante Capabilities sind deshalb Vertragsbestandteile und keine reine Formatierungsfrage.

Die öffentlichen Toolschemas enthalten keine parallelen Projekt-/Assembly-Parameter. Unterschiede werden ausschließlich im normalisierten Analysekontext und in der jeweiligen Capability-Antwort dargestellt.

## Regelkonfiguration

### Kanonischer Name

Die Regeldatei erhält den AiNetLinter-spezifischen, transparenten Namen:

```text
ainetlinter-rules.json
```

Der Name ist bewusst nicht nur `rules.json`, weil `rules` in fremden Projekten zu unspezifisch ist und die Herkunft der Datei für Agenten und Menschen nicht erkennbar macht.

Der Dateiname wird im Code genau einmal als zentrale Konstante definiert und von Resolver, Tests und allen internen Dateinamensprüfungen wiederverwendet. Der Literalwert `ainetlinter-rules.json` darf nicht an vielen Stellen unabhängig als String wiederholt werden.

### Source-Modus

Bei Übergabe einer `.sln` oder `.slnx` sucht der Server ausschließlich im Verzeichnis der Solution nach der kanonischen Regeldatei.

- Datei vorhanden: Lint-Regeln sind konfiguriert.
- Datei nicht vorhanden: gültiger Navigations-/Analysekontext ohne Lint-Regeln.
- Datei vorhanden, aber ungültig: harter Konfigurationsfehler; es werden keine Default-Regeln geladen.

Es gibt keine Suche im aktuellen Arbeitsverzeichnis, in Elternverzeichnissen oder in zufälligen Unterverzeichnissen. Es gibt kein stilles Fallback auf Default-Regeln.

### Fehlende Regeln sind kein grüner Lint-Lauf

Ein fehlendes Regelwerk darf nicht als „null Verstöße“ erscheinen. Der Kontext muss maschinenlesbar ausweisen:

```text
rulesConfigured: false
analysisMode: navigation
```

Navigation, Symbolgraph, Referenzen, Call Trees, Bodies und rohe Metriken können weiterhin verfügbar sein. Schwellenwert- und regelabhängige Funktionen melden dagegen explizit `not_configured` oder eine gleichwertige maschinenlesbare Capability-Antwort.

Insbesondere darf `get_violations` bei fehlender Konfiguration nicht erfolgreich eine leere Violation-Liste als Qualitätsaussage zurückgeben.

### Decompiled-Modus

Eine Assembly hat zunächst kein automatisch zugeordnetes Regelwerk. Die Assembly wird als Analyse-/Navigationsziel behandelt.

Assemblies sind keine Lint-Ziele. Linting ist für Assembly-Kontexte deaktiviert und nicht verfügbar. Das ist eine fachliche Grenze: Regelverstöße gegen dekompilierten Code würden leicht als Aussagen über den ursprünglichen Quellcode missverstanden. Verfügbar sind Navigation, Struktur, Referenzen, Bodies, Assembly-Metadaten und rohe Metriken, soweit der Snapshot sie belastbar liefert.

Eine Assembly-Antwort muss deshalb zwischen „keine Regeln konfiguriert“ bei einem Source-Kontext und „Linting für diesen Zieltyp nicht verfügbar“ bei einem Assembly-Kontext unterscheiden. Dafür ist ein strukturierter Status wie `lintStatus: unsupported` erforderlich.

## Einheitliches internes Modell

Der externe Pfad wird in einen einheitlichen Analysekontext materialisiert:

```text
TargetPath
    ↓
TargetResolver
    ↓
ResolvedAnalysisContext
```

Der Kontext muss mindestens folgende Informationen tragen:

```text
InputPath
Origin: source | decompiled
SolutionPath
SourceRoot
RulesPath?
RulesConfigured
ContentIdentity?
Completeness
Capabilities
OriginalAssemblyPath?
```

`Origin` und `Completeness` sind keine optionalen UI-Details. Sie verhindern, dass ein Agent dekompilierten Code mit Originalquellcode oder partiellen Ergebnissen mit vollständigen Ergebnissen verwechselt.

Die gemeinsame Abstraktion bedeutet nicht, dass alle Fähigkeiten identisch sind:

| Fähigkeit | Source-Solution | Decompiled Assembly |
|---|---:|---:|
| Symbolsuche | ja | ja |
| Referenzsuche | ja | ja, ggf. partiell |
| Call Tree | ja | ja, ggf. partiell |
| Methodenrumpf | ja | ja, soweit dekompiliert |
| Git-Diff/Change-Impact | ja | nein |
| Build/Test | ja | nein |
| Originalzeilen/Source-Link | ja | nein |
| Assembly-Metadaten | indirekt | ja |
| Lint-Verstöße | mit Regeldatei | nein |

Die Toolschemas sollen nicht wieder zwei getrennte Benutzerpfade einführen. Unterschiede werden über `capabilities`, `origin`, `completeness` und klare Fehler-/Statuswerte ausgedrückt.

## Decompiled-Modus ohne Projektartefakt

Bei Übergabe einer DLL arbeitet der Server direkt im Decompiled-Modus:

```text
DLL
  ↓
metadata-only inspection / decompilation
  ↓
interner Roslyn-Solution-Kontext
  ↓
einheitliche Analysewerkzeuge
```

Es wird weder `ainetlinter.project.json` noch `<assembly-name>.project.json` erzeugt.

Falls eine physische `.slnx` für Roslyn erforderlich ist, liegt sie ausschließlich in einem internen, verwalteten Snapshot-/Cache-Verzeichnis. Sie ist kein vom Agenten zu übergebender oder zu editierender Vertrag.

Der Cache darf nicht nur auf dem Assemblynamen basieren. Die Identität muss mindestens den kanonischen Pfad und den Inhalt der Assembly berücksichtigen, vorzugsweise über einen Content-Hash. Dadurch werden gleichnamige Assemblies, Versionswechsel und veraltete Dekompilationen sauber getrennt.

Der Analysekontext muss für Assemblies transparent ausweisen:

- originale absolute Assemblyquelle
- Content-Identität oder Generation
- internen Decompiled-Status
- generierten Source-/Solution-Root, sofern für Diagnosezwecke erforderlich
- fehlende oder partielle Referenzen
- Ressourcen- und Lebensdauerstatus

## Bestehende Architektur und betroffene Bereiche

Der aktuelle Stand trennt Source- und Assemblyziele noch sichtbar:

- `src/AiNetLinter/Mcp/AnalysisTarget.cs` modelliert `Project` und `Assembly` als explizite Zieltypen.
- `src/AiNetLinter/Mcp/AnalysisTargetResolver.cs` verlangt aktuell `targetType` und einen absoluten `targetPath`.
- `src/AiNetLinter/Mcp/Projects/ProjectDefinitionLoader.cs` lädt aktuell zwingend `ainetlinter.project.json` mit `solution` und `rules`.
- `src/AiNetLinter/Mcp/Projects/ProjectInstanceFactory.cs` materialisiert Regeln und unterscheidet derzeit zwischen Default-/Batch-Semantik und strengem Registry-Pfad.
- Assembly-Registry, Decompiler und Snapshot-Lifecycle existieren bereits als eigener technischer Pfad.
- `Docs/agent-api.md`, `Docs/integration.md` und gegebenenfalls `Docs/configuration.md` dokumentieren noch den alten Vertrag.
- `.agents/rules/AiNetLinter-McpWorkflow.mdc` beschreibt derzeit `targetType`; diese Regeln müssen nach der fachlichen Freigabe synchronisiert werden.

Die Vereinfachung soll den öffentlichen Vertrag zentralisieren. Sie verlangt nicht zwingend, Assembly-Registry und Source-Registry intern physisch zusammenzulegen. Gemeinsamer Analysekontext und gemeinsamer Resolver sind wichtiger als identische Speicherverwaltung.

### Konkrete Umsetzungsleitplanken

- Es gibt genau einen öffentlichen `targetPath`-Resolver. Die MCP-Tool-Dispatcher erhalten keinen vom Agenten gelieferten Zieltyp mehr.
- Der Resolver entscheidet zunächst anhand von Existenz und Endung, danach materialisiert der passende Source- oder Decompiled-Provider einen gemeinsamen Analysekontext.
- Für eine `.sln`/`.slnx` wird ausschließlich die benachbarte `ainetlinter-rules.json` betrachtet.
- Für eine DLL wird kein Regelwerk automatisch entdeckt, Linting wird deaktiviert und kein user-facing Projektmanifest erzeugt.
- Der kanonische Regeldateiname wird über genau eine zentrale Konstante bereitgestellt; Resolver und Tests verwenden diese Konstante statt eigener String-Literale.
- `projectRoot` darf nicht als versteckter Parallelvertrag fortbestehen. Ressourcen und Tools müssen auf den neuen konkreten `targetPath`-Vertrag umgestellt werden.
- Session- und Registry-Schlüssel müssen den normalisierten Kontext identifizieren. Bei Assemblies gehören mindestens Assemblypfad und Inhaltsidentität dazu; bei Source-Kontexten mindestens Solutionpfad und die wirksame Regelkonfiguration.
- Der Resolver darf nicht durch Fallbacks, Parent-Suche oder Dateinamensraten eine andere Solution oder Regeldatei auswählen.
- Die Antwortprojektion darf Capability-Information nicht nur als Freitext liefern. Für Agenten relevante Zustände benötigen strukturierte Felder und stabile Statuswerte.

## Muss-Kriterien

- Der Agent kann eine `.sln`/`.slnx` oder `.dll`/`.exe` ausschließlich über `targetPath` adressieren.
- `targetPath` adressiert die konkrete Datei, nicht ein Verzeichnis oder einen implizit ermittelten Projektroot.
- `targetType` ist aus MCP-Schemas, Toolargumenten, Ressourcenverträgen und relevanter Dokumentation entfernt.
- `ainetlinter.project.json` wird nicht mehr gelesen, erzeugt oder als Setup-Voraussetzung beschrieben.
- Eine Source-Solution funktioniert ohne Regeldatei im Navigationsmodus.
- Der kanonische Regeldateiname ist transparent und ausschließlich relativ zum Solution-Verzeichnis auflösbar.
- Ungültige vorhandene Regeldateien führen zu einem klaren Konfigurationsfehler und nicht zu Default-Regeln.
- Eine DLL aktiviert automatisch den Decompiled-Modus.
- Assembly-Kontexte unterstützen grundsätzlich keine regulären Lint-Verstöße.
- Der Decompiled-Modus erzeugt keine user-facing Projektdefinition.
- Source- und Decompiled-Kontexte verwenden denselben öffentlichen Ziel- und Analysevertrag.
- Herkunft, Vollständigkeit, Content-Identität und Capabilities bleiben maschinenlesbar.
- Assembly-Snapshots werden nicht allein über den Dateinamen identifiziert.
- Die Antwortsemantik unterscheidet „keine Regeln konfiguriert“ von „keine Verstöße gefunden“.

## Non-Goals

- Keine automatische Migration alter Projektdefinitionen.
- Kein Kompatibilitätsmodus für `targetType`.
- Kein Fallback auf beliebige Regeldateien oder Default-Regeln.
- Keine Behauptung, dass dekompilierter Code Originalquellcode ersetzt.
- Keine automatische Ausführung oder dynamische Laufzeitladung von Assemblies.
- Keine Gleichsetzung der Toolfähigkeiten für Git, Build, Tests und Originalzeilen.
- Keine Lint-Funktionalität für Assembly-Kontexte.
- Keine Implementierung oder Freigabe dieses Konzepts in diesem Arbeitsschritt.

## Fehler- und Statussemantik

Der Resolver muss Fehler früh und verständlich melden:

- relativer Pfad: `targetPath` muss absolut sein
- Pfad existiert nicht: Ziel nicht gefunden
- unbekannte Dateiendung: unterstützte Zieltypen nennen
- `.sln`/`.slnx` ohne ladbare Solution: Source-Load-Fehler
- ungültige `ainetlinter-rules.json`: Konfigurationsfehler
- fehlende Regeldatei: kein Fehler, aber `rulesConfigured=false`
- Lint-Aufruf auf Assembly-Kontext: `unsupported`, niemals leere Erfolgsmeldung
- partielle Assembly-Referenzen: Analyseergebnis mit `completeness=partial` und Diagnostics
- nicht unterstützte Operation im jeweiligen Kontext: maschinenlesbares `unsupported` mit Begründung

Ein leerer Ergebnissatz darf nur dann als fachliche Leere gelten, wenn der Kontext vollständig und die Capability aktiv ist.

## Betriebs- und Bedrohungsmodell

- Eingaben sind lokale absolute Pfade, die vom Agenten oder Benutzer vorgegeben werden.
- Assemblies werden metadata-only verarbeitet und nicht ausgeführt.
- Decompilation kann erhebliche CPU-, Speicher- und Disk-Ressourcen verbrauchen; die bestehenden externen Ressourcenlimits und Snapshot-Lifetimes bleiben relevant.
- Assemblyinhalt, generierte Dateien, Kommentare und Typnamen sind untrusted input und dürfen keine Steueranweisungen für den Agenten darstellen.
- Cache- und Snapshotpfade müssen eindeutig, bereinigbar und gegen veraltete Inhalte geschützt sein.
- Die Einführung des neuen Vertrags darf keine zufällige Suche außerhalb des definierten Zielverzeichnisses verursachen.

## Verifikation

Nach der späteren Implementierungsfreigabe sind mindestens folgende Nachweise erforderlich:

### Resolver und Konfiguration

- `.sln` und `.slnx` mit vorhandener `ainetlinter-rules.json`
- `.sln` und `.slnx` ohne Regeldatei
- ungültige Regeldatei
- DLL und EXE als Decompiled-Eingabe
- unbekannte Endung, relativer Pfad und fehlende Datei
- keine Akzeptanz von `targetType`
- keine Akzeptanz oder Erzeugung von `ainetlinter.project.json`

### Assembly-Lifecycle

- gleichnamige Assemblies aus verschiedenen Verzeichnissen
- gleicher Assemblyname mit verändertem Inhalt
- fehlende und partielle Referenzen
- Snapshot-Wiederverwendung und Bereinigung
- transparente Herkunfts-, Hash-, Generation- und Completeness-Angaben

### Agentenvertrag

- `tools/list`, Ressourcen und Toolbeschreibungen enthalten keinen alten `targetType`-Pfad.
- Navigation funktioniert ohne Regeldatei.
- regelabhängige Tools melden fehlende Konfiguration eindeutig.
- Source- und Decompiled-Aufrufe verwenden denselben `targetPath`-Vertrag.

### Projektabschluss

Die Implementierung muss zusätzlich den in `AGENTS.md` geforderten Build und die vollständigen Nicht-Stress-Testläufe über `FastTests` und `IntegrationTests` grün durchlaufen. Wegen der MCP-Vertragsänderung sind außerdem gezielte MCP-Handshake-, Tool-Schema-, Source- und Assembly-Integrationstests erforderlich.

Betroffene Dokumentation muss nach der Implementierung aktualisiert werden, insbesondere `Docs/agent-api.md`, `Docs/integration.md`, `Docs/configuration.md` und die MCP-Arbeitsregeldatei.

## Arbeitsgedächtnis (nur Draft)

### Bestätigte Entscheidungen dieser Runde

- Brainstorming bleibt aktiv; keine Implementierung.
- Harte Schnitte sind gewünscht, keine Kompatibilitätsschicht.
- `targetType` wird vollständig entfernt.
- `ainetlinter.project.json` wird vollständig entfernt.
- Die Regeldatei heißt verbindlich `ainetlinter-rules.json`.
- Der Regeldateiname wird im Code über eine zentrale Konstante verwendet und nicht an vielen Stellen als String-Literal dupliziert.
- Die Regeldatei liegt bei Source-Projekten neben der `.sln`/`.slnx` und ist optional.
- Bei fehlender Regeldatei läuft der MCP-Kontext für Navigation/Analyse weiter.
- Eine DLL aktiviert den Decompiled-Modus automatisch; Assembly-Linting ist deaktiviert und nicht verfügbar.
- Es wird kein `<assembly-name>.project.json` erzeugt.
- Technische Dekompilierungsartefakte bleiben intern und werden über Herkunft, Vollständigkeit und Identität transparent gemacht.

### Relevante geprüfte Evidenz

- Der aktuelle MCP-Vertrag verlangt in `AnalysisTargetResolver` `targetType` und `targetPath`.
- Der aktuelle `ProjectDefinitionLoader` verlangt eine feste `ainetlinter.project.json` mit `solution` und `rules`.
- Die bestehende Assemblyanalyse besitzt bereits getrennte Registry-/Snapshot- und Ressourcenpfade.
- Die aktuelle MCP-Dokumentation beschreibt Source- und Assembly-Tools mit unterschiedlichen Capabilities.
- Die Repository-Regeln verlangen MCP-first für C#-Semantik, aber dieses Konzept betrifft primär den externen Vertrag und wurde deshalb anhand der vorhandenen Resolver-, Loader- und Dokumentationsstrukturen geprüft.

### Vorläufige Empfehlungen

- Nur bestehende konkrete Dateien mit unterstützter Endung akzeptieren: `.sln`/`.slnx` oder `.dll`/`.exe`; keine Verzeichnisheuristik.
- Fehlende Regeln als `navigation`, nicht als erfolgreiche leere Lint-Auswertung behandeln.
- Assembly-Kontexte nicht linten; die Lint-Semantik würde sonst Aussagen über generierten statt originalen Code nahelegen.
- Den Assembly-Cache content-basiert identifizieren.
- Einen gemeinsamen `ResolvedAnalysisContext` einführen, ohne alle internen Registry-Implementierungen künstlich zu verschmelzen.

### Nächste fachliche Klärung

Die fachlichen Scope-Entscheidungen sind bestätigt. Als nächster Planerschritt müssen die verbleibenden Statuswerte, Capability-Namen und die genaue Zuordnung der bestehenden Tools zum neuen Analysekontext geprüft werden, damit kein Tool stillschweigend eine alte Projekt-/Assembly-Semantik behält.
