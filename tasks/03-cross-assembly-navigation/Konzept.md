---
status: completed
---

# Konzept: Cross-Assembly-Navigation & Typauflösung im AiNetLinter MCP-Server

## 1. Ziel & Nutzen

Bei der statischen Analyse komplexer oder mehrteiliger .NET-Ökosysteme (wie Unternehmensanwendungen, Plugin-Architekturen oder verteilten Klassenbibliotheken) stoßen Coding-Agenten regelmäßig an Assembly-Grenzen. Bislang muss ein Agent bei unbekannten Schnittstellen (z. B. `IDataProvider`, `BaseCommand`) raten oder manuelle Text-Grep-Suchen über Dutzende referenzierte DLLs ausführen, um die definierende Assembly oder konkrete Implementierungen zu finden. Auch in normalen Source-Projekten ist das Auffinden konkreter Implementierungen von Schnittstellen oder Basisklassen oft mühsam.

**Ziel**: Der AiNetLinter MCP-Server wird um dedizierte, Roslyn-gestützte Cross-Assembly- und Typnavigationsfähigkeiten sowie Performance-Optimierungen bei Fremd-Assemblies erweitert, damit Agenten Aufrufketten und Typbeziehungen ohne Rätselraten, ohne Laden/Ausführen von Fremdbinärdateien und mit minimalem Token-/Turn-Aufwand sowohl über Assembly-Grenzen hinweg als auch im Quellcode-Projekt verfolgen können.

---

## 2. Betroffene Projektbereiche & bestehende Strukturen

- **`src/AiNetLinter/Mcp/Tools/`**:
  - `TypeResolution/`: Neues MCP-Tool `ResolveTypeOriginTool.cs` zur schnellen Ermittlung der definierenden Assembly und des Festplattenpfads eines Typs/Interfaces.
  - `SymbolGraph/OutgoingCallScanner.cs`: Erkennung von Aufrufen in referenzierte Assemblies (`ContainingAssembly != CurrentAssembly`) als strukturierte Blätter; BCL-Rauschfilter (`System.*`, `Microsoft.NETCore.*`).
  - `CallTree/`: `GetCallTreeTool.cs` & `AssemblyGetCallTreeTool.cs` (Darstellung externer Referenz-Knoten mit `[ref: Assembly]`).
  - `TypeHierarchy/`: Neues MCP-Tool `FindImplementationsTool.cs` (Interface-/Abstract-Member → konkrete Überschreibungen und Implementierungen) für `project`- und `assembly`-Ziele.
  - `AssemblyInspection/`: `SearchAssemblyTool.cs` (Erweiterung um `declarationOnly: true` und `kind`).
  - `Impact/` & `FeatureContext/`: `GetImpactTool.cs` & `GetAssemblyContextTool.cs` (Short-Circuiting von Test-Scans bei Assembly-Zielen ohne Testframework-Referenzen).
- **`src/AiNetLinter/Mcp/Assemblies/`**:
  - Wiederverwendung von `AssemblyAnalysisLease`, `AssemblyRegistry` und den bereits vorhandenen `MetadataReference`-Pfaden der Roslyn-`Compilation`.
- **Tests**:
  - `src/AiNetLinter.FastTests/`: Unit- und Component-Tests für alle Tools via InMemory-Roslyn-Workspaces mit Metadaten-Referenzen.
  - `src/AiNetLinter.IntegrationTests/`: End-to-End-Tests über MCP-Client mit realen referenzierten Assemblies und Source-Projekten.

---

## 3. Geplante Fähigkeiten & Muss-Kriterien

### Feature 1: Eigenständiges MCP-Tool `resolve_type_origin`
- **Muss-Kriterium**: Zu einem angegebenen Typnamen (z. B. `IDataProvider` oder `Vendor.Data.BaseCommand`) ermittelt das Tool über die Roslyn-`Compilation.References` sofort:
  1. Den einfachen Assembly-Namen (z. B. `Vendor.Data`),
  2. Den vollständigen, absoluten Dateipfad der DLL auf der Festplatte (`C:\...\Vendor.Data.dll`),
  3. Den vollqualifizierten Typnamen und Symbol-Kind (Interface, Class, Struct).
- **Fehlerbehandlung**: Wenn der Typ in keiner der referenzierten Assemblies auffindbar ist, liefert das Tool ein klares `SYMBOL_NOT_FOUND` mit einer Liste durchsuchter Referenz-Assemblies (gekürzt) statt einer Exception.
- **Schnittstelle**:
  - Parameter: `typeName` (Pflicht), Zielvertrag (`targetType=assembly`, `targetPath`).

### Feature 2: Outgoing Cross-Assembly Call-Leaves in `get_call_tree` mit BCL-Filterung
- **Muss-Kriterium**: In `OutgoingCallScanner` werden Aufrufe auf Methoden/Properties, die in referenzierten Assemblies deklariert sind, nicht mehr verworfen (`symbol.Locations.All(!IsInSource)`), sondern als referenzierte Blätter erfasst.
- **Darstellung**: Im gerenderten Call-Tree werden sie eindeutig als externe/referenzierte Ziele gekennzeichnet (z. B. `[ref: Vendor.Data] IBaseCommand.Execute`).
- **Rausch-Unterdrückung**: Standard-Framework-Typen (`System.*`, `Microsoft.NETCore.*`) werden standardmäßig herausgefiltert (`includeBcl: false`), um den Baum nicht mit Primitives (`string.IndexOf`, `List.Add`, `object.ToString`) zu überfluten. Über den optionalen Parameter `includeBcl: true` können sie bei Bedarf sichtbar gemacht werden.

### Feature 3: Eigenständiges MCP-Tool `find_implementations` (für Project & Assembly)
- **Muss-Kriterium**: Zu einer Schnittstelle (`interface`) oder abstrakten Klasse/Methode werden alle implementierenden Klassen und konkreten `override`-Methoden innerhalb der untersuchten Compilation/Assembly aufgelistet.
- **Dualer Zielvertrag**: Unterstützt sowohl `targetType=project` (Entwicklung im eigenen Code) als auch `targetType=assembly` (Fremdcode-Erkundung).
- **Schnittstelle**:
  - Parameter: `symbolIdentifier` (z. B. `Vendor.Data.BaseCommand.Execute` oder `IDataProvider`), Zielvertrag (`targetType=assembly|project`, `targetPath`).
  - Rückgabe: Strukturierte Liste mit implementierender Klasse, Methode, Datei- und Zeilenposition sowie Ausweisung, ob die Implementierung `concrete`, `abstract` oder `virtual` ist.

### Feature 4: `search_assembly` Deklarations- & Symbolart-Filter
- **Muss-Kriterium**: Ergänzung von `search_assembly` um optionale Filter:
  - `declarationOnly` (`boolean`, Default `false`): Schließt Treffer in Kommentaren, Strings und XML-Docs aus.
  - `kind` (`string`, optional: `method`, `type`, `property`): Schränkt Treffer auf die jeweilige Symbolart ein.

### Feature 5: Performance Short-Circuit für Test-Scans bei `targetType=assembly` (Übernahme aus Audit-Befund 2.3)
- **Problem**: Bei dekompilierten Fremd-Assemblies verursachten `get_impact` und `get_assembly_context` Latenzen von 20–30 Sekunden, weil sie die gesamte Assembly nach Testmethoden und Test-Referenzen durchforsteten, obwohl Produktions-Assemblies typischerweise keine Testframework-Referenzen besitzen.
- **Muss-Kriterium**: Prüfung, ob die untersuchte Assembly Referenzen auf bekannte Testframeworks (`xunit.*`, `nunit.*`, `Microsoft.VisualStudio.TestPlatform.*`, `MSTest.*`) besitzt. Ist keine Testreferenz vorhanden, wird der Test-Referenz-Scan sofort abgebrochen (Short-Circuit) und eine leere Test-Menge zurückgegeben.

---

## 4. Non-Goals & Scope-Grenzen

- **Keine unkontrollierte automatische Rekursion über das gesamte Dateisystem**: Das Tool dekompiliert nicht selbstständig im Hintergrund kaskadierend dutzende referenzierte DLLs auf Vorrat. Der Agent behält die Kontrolle und navigiert gezielt mit dem erhaltenen Pfad weiter.
- **Keine IL-Modifikation / kein dynamisches Profiling**: Es bleibt rein statisch auf Roslyn- und ILSpy-Metadatenbasis ohne Laden von Assemblies in die Host-Runtime.

---

## 5. Akzeptanzkriterien

1. `resolve_type_origin` beantwortet Typ-Anfragen in < 100 ms für ein Projekt mit > 50 Referenzen deterministisch mit Assembly-Name und Dateipfad.
2. `get_call_tree(direction="outgoing")` bricht nicht mehr an der Assembly-Grenze ab, sondern zeigt Calls in Fremd-Assemblies als `[ref: <Assembly>] <Typ>.<Member>` an (bei Standard `includeBcl=false` ohne System-Rauschen).
3. `find_implementations` findet sowohl in Quellcode-Projekten als auch in Assemblies konkrete Implementierungen und Overrides mit Datei- und Zeilenangabe (z. B. zu `BaseCommand.Execute` direkt `ConcreteSqlCommand.Execute`).
4. `search_assembly` liefert mit `declarationOnly=true` bei Begriffen wie `Execute` nur echte Methodensignaturen/Typen und keine Treffer in XML-Docs oder Kommentaren.
5. `get_impact` und `get_assembly_context` auf Fremd-Assemblies ohne Testreferenzen schließen durch Short-Circuiting in < 3 Sekunden statt ~28 Sekunden ab.
6. Alle FastTests (`Category=Unit`, `Category=Component`) und IntegrationTests (`Category!=Stress`) laufen warnungs- und fehlerfrei durch (`TreatWarningsAsErrors = true`).

---

## 6. Verifikation & Dokumentation

- **Unit-Tests**: InMemory-Workspaces in `AiNetLinter.FastTests` mit abhängigen Assemblies (Projekt A referenziert Metadaten-DLL B) sowie Interface-Implementierungs-Hierarchien und Test-Short-Circuiting.
- **Integration-Tests**: E2E-Szenario über den MCP-Server (Projekt- und Assembly-Target).
- **Dokumentation**:
  - `README.md`: Aktualisierung der Feature-Übersicht und Tool-Listen bei Bedarf.
  - `.agents/rules/AiNetLinter-McpWorkflow.mdc`: Aufnahme der neuen Tools (`resolve_type_origin`, `find_implementations`) in die Entscheidungsmatrix für Coding-Agenten.
  - `Docs/configuration.md`: Aktualisierung der CLI- und MCP-Tool-Referenzen.
  - `.agents/rules/AiNetLinter.mdc` via `dotnet run --project src/AiNetLinter -- --sync-agent-rules-only`.
  - Registrierung in `tools/list` und MCP-Toolschemas in `.gemini/antigravity-ide/mcp/AiNetLinter/`.
