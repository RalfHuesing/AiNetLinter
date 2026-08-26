---
status: ready
type: konzept
project_kind: brownfield
estimated_scope: medium
rules_dir: .agents/rules
last_updated: 2026-08-26
open_questions: []
---

# Konzept: Externe .NET-Assemblies analysieren

## Ziel (Was)

AiNetLinter soll externe .NET-Assemblies (`.dll`) metadatenbasiert untersuchen können. Ein Agent soll dadurch erkennen, welche öffentlichen Typen, Methoden und insbesondere Extension-Methoden in einer eigenen DLL oder einem NuGet-Paket vorhanden sind.

Zusätzlich soll die Analyse — sofern ein Consumer-Projekt angegeben ist — beurteilen können, ob eine gefundene Extension auf einen konkreten Typ dieses Projekts anwendbar ist. Die DLL wird dabei niemals ausgeführt.

## Warum / Kontext

In realen .NET-Projekten liegen relevante APIs nicht vollständig im analysierten Quellcode. Externe Abhängigkeiten können aus NuGet-Paketen stammen oder eigene, nicht öffentlich dokumentierte Assemblies sein. Trainingswissen oder eine Websuche ist für die konkrete Version und die konkrete private DLL keine verlässliche API-Quelle.

Der aktuelle Roslyn-basierte Symbolgraph kennt externe Referenzen zwar über die `Compilation`, die bestehende Namenssuche verwendet an relevanten Stellen jedoch `FindSourceDeclarationsAsync`. Dadurch werden externe Metadaten-Symbole bei der allgemeinen Quellcode-Suche nicht automatisch als Entdeckungsquelle behandelt.

Die Funktion soll deshalb eine gezielte, kontrollierte Ergänzung der bestehenden Projektanalyse werden — kein Plugin-System und kein Mechanismus zum Laden oder Ausführen fremder .NET-Komponenten.

## Scope

### Muss-Haben

- Eine Analysefunktion mit einem expliziten lokalen `assemblyPath`.
- Auflistung der öffentlichen API der angegebenen Assembly:
  - Assembly-Identität und Assembly-Referenzen
  - öffentliche Namespaces und Typen
  - Methoden inklusive Rückgabetyp, Parametern, Generic-Parametern und Constraints
  - relevante Properties, Felder, Events und Attribute
- Eine gezielte Extension-Suche mit Filtern für mindestens Methodenname, Namespace und optionalen Zieltyp.
- Erkennung klassischer C#-Extension-Methoden über Roslyn-Symbole, nicht über Namenskonventionen.
- Bei vorhandenem Consumer-Projekt: Auflösung des Zieltyps gegen dessen `Compilation` und Prüfung der tatsächlichen Anwendbarkeit der Extension.
- Nutzung bereits geladener Projekt-Referenzen, wenn die Assembly in der Consumer-`Compilation` enthalten ist; keine unnötige zweite Analyse derselben Referenz.
- Explizite Meldung über nicht auflösbare Abhängigkeiten und eine Kennzeichnung, wenn die Antwort deshalb nur teilweise vollständig ist.
- Begrenzte und deterministische Ergebnisse durch öffentliche-API-Standardfilter, Suchfilter und ein Ergebnislimit.
- Tests für öffentliche API, Overloads, generische Methoden, Extension-Methoden, fehlende Abhängigkeiten, ungültige Pfade und Consumer-Kontext.

### Nice-to-Have (Zwischenspeicher — vor `status: ready` aufgelöst)

Keine; die offenen Erweiterungen sind als Non-Goals oder spätere, separate Vorhaben abgegrenzt.

### Non-Goals (bewusst NICHT Teil davon)

- **Kein `Type.GetType()` als Einstieg:** Diese API löst keinen beliebigen DLL-Pfad auf, sondern arbeitet mit Typnamen und bereits verfügbaren Assemblies.
- **Kein `Assembly.Load`, `Assembly.LoadFrom` oder `AssemblyLoadContext`:** Die fremde Assembly wird nicht in den Runtime-Prozess geladen. Damit entstehen weder Plugin-Verhalten noch Runtime-Binding- und Load-Context-Probleme. Die Architekturregel gegen dynamisches Assembly-Laden bleibt unberührt.
- **Keine Methodenausführung und keine Instanziierung:** Es werden keine Methoden aufgerufen, keine Typen konstruiert und keine Plugins aktiviert. Das reine Lesen von Metadaten ist der maximale Zugriff dieser Funktion.
- **Keine Decompilierung:** Methodenimplementierungen, IL, rekonstruierter Quellcode, PDB-Auswertung und Source-Link-Navigation gehören nicht zum ersten Feature.
- **Keine automatische Vollständigkeitsbehauptung:** Wenn Abhängigkeiten oder Referenzassemblies fehlen, darf das Ergebnis nicht stillschweigend als vollständig ausgegeben werden.
- **Keine Analyse beliebiger Rechnerpfade oder URLs:** Es werden nur explizit angegebene lokale Assembly-Pfade akzeptiert und validiert.
- **Keine internen APIs als normale Empfehlung:** Standardmäßig wird die öffentliche Oberfläche betrachtet. `internal`-Symbole sollen nicht als regulär verwendbare Agenten-API ausgegeben werden.
- **Kein eigener Cache als Teil des MVP:** Ein Cache mit eigener Invalidierungslogik wird erst dann erwogen, wenn Messungen einen konkreten Bedarf zeigen.
- **Keine große Menge einzelner Spezialtools:** Der erste Schnitt bleibt bei einer API-Oberflächenanalyse und einer Extension-Suche, statt für jede Symbolart ein eigenes MCP-Tool zu veröffentlichen.

## Zielplattformen / Technischer Rahmen

### Primärentscheidung: Roslyn-Metadaten

Die Assembly wird als Metadatenreferenz eingebunden, beispielsweise über [`MetadataReference.CreateFromFile`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.metadatareference.createfromfile?view=roslyn-dotnet-5.0.0) oder über [`AssemblyMetadata.GetReference`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.assemblymetadata.getreference?view=roslyn-dotnet-4.13.0). Anschließend erfolgt die Auswertung über `IAssemblySymbol`, `INamedTypeSymbol` und `IMethodSymbol`.

Das passt zur bestehenden Architektur, weil Roslyn bereits die semantische Analyse und die `Compilation` des Consumer-Projekts liefert. Die DLL wird nicht als ausführbare Runtime-Komponente behandelt, sondern als weitere Symbolquelle.

### Extension-Erkennung und Verwendbarkeit

Für Extension-Methoden wird `IMethodSymbol.IsExtensionMethod` verwendet. Die Prüfung auf einen konkreten Empfängertyp soll nicht nur anhand des Methodennamens oder des ersten Parametertyps erfolgen. Mit dem Symbol des konkreten Consumer-Typs muss Roslyn die Typkompatibilität, Generics, Constraints und Konvertierungen beurteilen. Dafür kommt insbesondere die reduzierte Extension-Repräsentation über `ReduceExtensionMethod(...)` beziehungsweise die entsprechende semantische Roslyn-Auflösung infrage.

Ohne Consumer-`Compilation` kann die Funktion nur sagen: „Diese Assembly enthält eine Extension mit dieser Signatur.“ Sie kann dann nicht zuverlässig sagen: „Diese Extension ist an dieser konkreten Stelle aufrufbar.“ Diese Unterscheidung muss im Ergebnis sichtbar bleiben.

### Abhängigkeiten und Referenzauflösung

Die Analyse benötigt neben der Zielassembly gegebenenfalls deren abhängige Assemblies sowie Framework-Referenzen. Die Auflösungsstrategie soll in dieser Reihenfolge arbeiten:

1. vorhandene Referenzen der Consumer-`Compilation`, falls `projectRoot` verwendet wird;
2. explizit angegebene zusätzliche Assembly-Pfade, falls die Tool-Schnittstelle diese zulässt;
3. naheliegende Referenzen aus dem Verzeichnis der Zielassembly beziehungsweise der Projektkonfiguration;
4. bei nicht auflösbaren Referenzen: Teilergebnis plus strukturierte Diagnose.

Die angegebene Datei wird exakt analysiert. Es soll nicht stillschweigend eine andere DLL mit gleichem Namen aus einem weiteren Suchpfad gewählt werden.

### Vorgeschlagene MCP-Schnittstelle

#### `inspect_assembly`

Pflichtparameter:

- `assemblyPath`

Optionale Parameter:

- `projectRoot`, wenn die Assembly gegen ein konkretes Projekt eingeordnet werden soll;
- `namespace`, `typeName` und `memberName` als Filter;
- `publicOnly`, standardmäßig `true`;
- `maxResults` mit einem sicheren Standardlimit.

Das Ergebnis enthält die Assembly-Identität, die gefilterte API-Oberfläche, Referenzen, Diagnosen und den Vollständigkeitsstatus.

#### `find_assembly_extensions`

Pflichtparameter:

- `assemblyPath`

Optionale Parameter:

- `projectRoot` für die semantische Prüfung im Consumer-Projekt;
- `receiverType`, bevorzugt als auflösbarer Typname im Consumer-Projekt;
- `extensionName` oder Namensmuster;
- `namespace`;
- `maxResults`.

Das Ergebnis unterscheidet zwischen gefundenen Extension-Methoden, tatsächlich auf den Receiver-Typ reduzierbaren Extensions und nicht entscheidbaren Fällen wegen fehlender Typen oder Abhängigkeiten.

Die MCP-Schnittstelle sollte ein absolutes `projectRoot` gemäß dem bestehenden Server-Vertrag verwenden. `assemblyPath` muss ebenfalls als eindeutig auflösbarer lokaler Pfad behandelt werden; relative Pfade sollten nicht anhand eines impliziten Arbeitsverzeichnisses geraten werden.

### Reflection als begrenzte Alternative

Normales Reflection über `Assembly.LoadFrom` wäre für eine einfache Liste von Typen und Methoden technisch möglich, lädt die Assembly aber in den Runtime-Kontext. Das bringt Abhängigkeits-, Versions- und Load-Context-Fragen in den MCP-Prozess und widerspricht der bestehenden Vorgabe gegen dynamisches Laden.

[`MetadataLoadContext`](https://learn.microsoft.com/en-us/dotnet/standard/assembly/inspect-contents-using-metadataloadcontext) wäre eine metadata-only Reflection-Alternative. Sie ist für reine Oberflächenlisten brauchbar, benötigt aber einen eigenen `MetadataAssemblyResolver` und liefert nicht automatisch die semantische Einordnung in die Roslyn-`Compilation` des Consumer-Projekts. Deshalb wird sie nicht als Primärpfad eingeführt.

## Verworfene Alternativen

- **`Type.GetType()`**: verworfen, weil kein Assembly-Pfad analysiert wird und die API keine projektbezogene Symbolauflösung bietet.
- **`Assembly.LoadFrom` plus `Assembly.GetTypes()`/`GetMethods()`**: verworfen, weil Runtime-Laden für eine statische Analyse unnötige Prozess- und Binding-Risiken einführt und die Roslyn-Semantik für Anwendbarkeit, Generics und Consumer-Typidentität fehlt.
- **`MetadataLoadContext` als Hauptimplementierung**: verworfen, weil es zwar sicherer als Runtime-Reflection ist, aber einen zusätzlichen Resolver benötigt und die bereits vorhandene Roslyn-Infrastruktur nicht nutzt.
- **Decompilation als Lösung für private DLLs**: verworfen, weil die Frage zunächst die veröffentlichte API und ihre Verwendbarkeit betrifft. Decompilation wäre ein separates Feature mit anderen rechtlichen, technischen und Qualitätsfragen.
- **Nur Trainingswissen oder Websuche**: verworfen, weil private Assemblies, lokale Builds und exakte Paketversionen dadurch nicht zuverlässig abgedeckt werden.
- **Generische Erweiterung des bestehenden `find_symbol` ohne klare Assembly-Abfrage**: vorerst verworfen, weil die Nutzerabsicht einen expliziten Assembly-Pfad und eine nachvollziehbare Diagnose über Abhängigkeiten benötigt. Eine spätere gemeinsame Source-/Metadata-Symbolauflösung kann nach dem MVP geprüft werden.

## Wo im Projekt

- `src/AiNetLinter/Baseline/SourceFileCatalog.cs` — bestehender Einstiegspunkt für geladene Workspace-/Solution-Strukturen und Projektkontext.
- `src/AiNetLinter/Baseline/SourceFileCatalogLoader.cs` — bestehender MSBuildWorkspace-Ladepfad, der für `projectRoot`-gebundene Analysen wiederverwendet werden soll.
- `src/AiNetLinter/Mcp/Tools/SymbolGraph/FindSymbolScanner.cs` — bestehende Symbolsuche; die Abgrenzung zwischen Quellcode- und Metadaten-Symbolen ist für die Integration relevant.
- `src/AiNetLinter/Mcp/Tools/SymbolGraph/FindReferencesTool.cs` — bestehende symbolische Auflösung und Referenzsuche; nicht ungeprüft durch eine zweite Resolverlogik duplizieren.
- `src/AiNetLinter/Core/RoslynSymbolExtensions.cs` — vorhandene Roslyn-Symbolhilfen, die bei der Normalisierung und Darstellung von Ergebnissen berücksichtigt werden sollen.
- `src/AiNetLinter/Mcp/Tools/` — vorgesehener Bereich für die neuen MCP-Tools nach dem bestehenden Tool-/Registrierungsmuster.
- `src/AiNetLinter.FastTests/` und `src/AiNetLinter.IntegrationTests/` — Testbereiche für metadata-only Tests sowie MCP-/Consumer-Projekt-Szenarien.

## Entdeckte Mängel/Redundanzen

- **Externe Metadaten-Symbole werden bei der bestehenden Namenssuche nicht entdeckt**
  - **Gefunden:** `FindSymbolScanner` und die namensbasierte Auflösung in `FindReferencesTool` verwenden `FindSourceDeclarationsAsync`; externe Assembly-Symbole sind damit keine gleichwertige Suchquelle.
  - **Bezug:** Kein dokumentierter Regelverstoß, sondern eine funktionale Lücke für genau diesen Anwendungsfall.
  - **Vorschlag:** Das neue Feature zunächst über einen klar abgegrenzten metadata-basierten Assembly-Resolver aufbauen. Nach dem MVP prüfen, ob eine gemeinsame Source-/Metadata-Symbolauflösung für bestehende Tools sinnvoll ist.
  - **Entscheidung:** Bewusst im neuen Feature berücksichtigt, aber keine ungeplante Generalüberarbeitung des bestehenden Symbolgraphen.

## Wie (grober Ansatz)

1. Pfad, Dateityp, Zugriffsrechte und Ergebnisgrenzen validieren.
2. Für einen vorhandenen Consumer-Kontext die bestehende Solution/Compilation verwenden; andernfalls eine isolierte Roslyn-Metadaten-Compilation mit den benötigten Referenzen erzeugen.
3. Assembly-Identität, Referenzen und Roslyn-Symbole auslesen. Nicht auflösbare Referenzen und Roslyn-Diagnosen strukturiert sammeln.
4. Für `inspect_assembly` die öffentliche Oberfläche filtern und in ein begrenztes MCP-Ergebnis überführen.
5. Für `find_assembly_extensions` Extension-Symbole erkennen und — bei Consumer-Kontext — gegen den tatsächlichen Receiver-Typ prüfen.
6. Metadatenreferenzen und temporäre Analyseobjekte deterministisch freigeben; kein Runtime-Assembly-Laden einführen.
7. Unit- und Integrationstests mit eigenen Testassemblies und mindestens einem fehlenden Dependency-Fall ergänzen.

## Definition of Done / Erfolgskriterien

- Ein Agent kann eine konkrete lokale eigene DLL angeben und deren öffentliche Typen und Methoden strukturiert abfragen.
- Eine externe DLL mit Extension-Methoden wird erkannt, auch wenn keine Quellcodedatei der DLL im Workspace liegt.
- Mit `projectRoot` kann die Funktion zwischen „Extension vorhanden“, „auf den Consumer-Typ anwendbar“ und „wegen fehlender Informationen nicht entscheidbar“ unterscheiden.
- Das Ergebnis benennt nicht auflösbare Abhängigkeiten und behauptet in diesem Fall keine unberechtigte Vollständigkeit.
- Die Analyse führt keinen Code aus und verwendet weder `Assembly.Load*` noch `AssemblyLoadContext`.
- Ungültige Pfade, große/unbegrenzte Ergebnisse und typische Metadatenfehler werden kontrolliert behandelt.
- Tests decken die fachlich relevanten Fälle ab; bestehende Fast- und Integrationstests bleiben grün.
- Die MCP-Dokumentation beschreibt Parameter, Grenzen und den Unterschied zwischen API-Auflistung und Consumer-bezogener Verwendbarkeitsprüfung.

## Offene Punkte

Keine für die Konzeptfreigabe. Die genaue Referenzauflösung für spezielle Build-Setups und die endgültige JSON-Ausgabe sind Teil der späteren Detailplanung und Implementierung.
