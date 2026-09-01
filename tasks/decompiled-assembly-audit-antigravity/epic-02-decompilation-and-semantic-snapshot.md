# Audit-Bericht: Epic 02 — Decompilation und semantischer Snapshot

## Scope und Evidenz

### Untersuchte Komponenten und Verträge

- **Decompilation-Adapter:** `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyDecompilationAdapter.cs`.
- **On-Demand-Body-Resolver:** `src/AiNetLinter/Mcp/Assemblies/Analysis/Bodies/AssemblyDecompiledBodyResolver.cs`.
- **Quelltext-Vorverarbeitung:** `src/AiNetLinter/Mcp/Assemblies/Analysis/Bodies/AssemblyDecompilationSourceText.cs`.
- **Roslyn-Workspace & Snapshot:** `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyRoslynWorkspaceFactory.cs`.
- **Symbol-Identität:** `src/AiNetLinter/Mcp/AnalysisSymbolIdentity.cs`.
- **Live-MCP-Abfragen:**
  - `get_symbol_body` für Methoden, Properties und Klassensymbole auf `LOCAL-01` und `LOCAL-02`.
  - `find_symbol` und `get_class_structure` zur Verifikation der dekompilierten Symbol-Signaturen und stabilen Symbol-IDs.

---

## Befunde

### 1. Bugs

#### FINDING-EPIC02-01: `get_symbol_body` wirft `InvalidOperationException` bei Top-Level-Typen

- **Kategorie:** Bug
- **Priorität:** P1
- **Größe:** S
- **Vertrauensgrad:** Hoch
- **Betroffene Komponenten:**
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/Bodies/AssemblyDecompiledBodyResolver.cs` (Zeilen 72–73, 103–111)
- **Soll-Ist-Abweichung:**
  Wenn `get_symbol_body` für ein NamedType-Symbol (Klasse, Struct, Interface, Record, Enum) aufgerufen wird, versucht `DecompileBodyAsync`, den Typnamen über `symbol.ContainingType` zu ermitteln:
  ```csharp
  var typeName = new ICSharpCode.Decompiler.TypeSystem.FullTypeName(ToReflectionTypeName(symbol.ContainingType));
  var source = decompiler.DecompileTypeAsString(typeName);
  ```
  Bei einem Top-Level-Typ ist `symbol.ContainingType` jedoch `null`. `ToReflectionTypeName(null)` liefert `string.Empty`. Der Aufruf von `decompiler.DecompileTypeAsString(new FullTypeName(""))` wirft daraufhin eine `InvalidOperationException`.
- **Evidenz:**
  - Live-Aufruf von `get_symbol_body` mit dem Klassen-Symbol auf `LOCAL-01`:
    ```
    bodyAvailability: unavailable; contentMode: decompiledSignatureOnly
    Hinweis: Body-Dekomposition fehlgeschlagen: InvalidOperationException
    ```
  - Code-Analyse in `AssemblyDecompiledBodyResolver.cs` (Zeilen 72–73):
    Wird ein `INamedTypeSymbol` übergeben, muss direkt `ToReflectionTypeName(type)` statt `symbol.ContainingType` verwendet werden.
- **Auswirkung:**
  Agenten können den dekompilierten Quellcode ganzer Typen nicht über `get_symbol_body` abrufen, obwohl das Werkzeug `INamedTypeSymbol` im Schema unterstützt.
- **Empfehlung:**
  In `DecompileBodyAsync` prüfen, ob `symbol` selbst ein `INamedTypeSymbol` ist; falls ja, direkt diesen Typnamen dekompilieren und die gesamte Typdeklaration zurückliefern:
  ```csharp
  var targetType = symbol as INamedTypeSymbol ?? symbol.ContainingType;
  var typeName = new ICSharpCode.Decompiler.TypeSystem.FullTypeName(ToReflectionTypeName(targetType));
  ```
- **Abgrenzung:** Klarer Implementierungsfehler im On-Demand-Body-Resolver.

#### FINDING-EPIC02-02: `AssemblyDecompiledBodyResolver` schlägt bei Property-Accessor-Methoden fehl

- **Kategorie:** Bug
- **Priorität:** P2
- **Größe:** S
- **Vertrauensgrad:** Hoch
- **Betroffene Komponenten:**
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/Bodies/AssemblyDecompiledBodyResolver.cs` (Zeilen 150–185)
- **Soll-Ist-Abweichung:**
  Wenn ein Symbol als Roslyn-`IMethodSymbol` vom Kind `MethodKind.PropertyGet` oder `PropertySet` übergeben wird (oder ein Getter/Setter aus VB.NET/IL-Metadaten), sucht `MatchesMember` ausschließlich nach einem `MethodDeclarationSyntax`. Im dekompilierten C#-AST dekompiliert ICSharpCode die Eigenschaft jedoch als `PropertyDeclarationSyntax`. Dadurch findet `FindMember` keinen Treffer und liefert:
  `"Für das dekompilierte Symbol wurde kein Member-Body gefunden."`
- **Evidenz:**
  - Live-Aufruf von `get_symbol_body` auf Getter-Methoden bei `LOCAL-01` lieferte trotz vorhandenem Property-Code `unavailable`.
  - Code in `AssemblyDecompiledBodyResolver.cs` (Zeilen 159–172): `MatchesMethod` matcht nur `MethodDeclarationSyntax` und `ConstructorDeclarationSyntax`, ignoriert aber `PropertyDeclarationSyntax` mit passenden `AccessorDeclarationSyntax`.
- **Auswirkung:**
  Bodies von Property-Gettern und -Settern können nicht punktgenau aus dekompilierten Assemblies ausgelesen werden.
- **Empfehlung:**
  `MatchesMember` erweitern, sodass bei `IMethodSymbol` mit `MethodKind.PropertyGet`/`PropertySet` oder `get_`/`set_`-Präfix auch in `PropertyDeclarationSyntax.AccessorList` nach dem passenden Accessor gesucht wird.
- **Abgrenzung:** Semantischer Fehler beim Syntax-Matching.

---

### 2. Optimierungen

#### FINDING-EPIC02-03: Synthetische Roslyn-Compilation generiert Hunderte `CS0501`-Fehler

- **Kategorie:** Optimierung
- **Priorität:** P2
- **Größe:** M
- **Vertrauensgrad:** Hoch
- **Betroffene Komponenten:**
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyDecompilationAdapter.cs` (Zeilen 132–146)
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyRoslynWorkspaceFactory.cs`
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyDiagnosticCodes.cs` (Zeile 55)
- **Soll-Ist-Abweichung:**
  Im Standardmodus (`decompileMemberBodies: false`) erzeugt ICSharpCode C#-Deklarationen ohne Rumpf (z. B. `public void Foo();`). Da die Methoden weder `abstract`, `extern` noch `partial` sind, erzeugt der Roslyn-CSharp-Compiler für jede Methode den Fehler `CS0501: 'Foo()' muss einen Textkörper deklarieren`.
  In `AssemblyDiagnosticCodes.IsExpectedDeclarationOnlyDiagnostic` wird `CS0501` zwar herausgefiltert, die Roslyn-Compilation enthält jedoch intern hunderte Fehlerobjekte, die bei jeder Diagnose- und Symbol-Traversierung mitgeführt werden.
- **Evidenz:**
  - Bei `LOCAL-01` meldet der Roslyn-Workspace: `45 Dateien haben Compile-Fehler (503 Errors gesamt im aktuellen Roslyn-Workspace)`.
  - `AssemblyDiagnosticCodes.cs` Zeile 55:
    ```csharp
    internal static bool IsExpectedDeclarationOnlyDiagnostic(string id) => id is EmptyEventAccessor or EmptyMemberBody;
    ```
- **Auswirkung:**
  Unnötiger Overhead bei Compilation-GetDiagnostics-Läufen und irreführende Hinweise in MCP-Antworten über angebliche Compile-Fehler.
- **Empfehlung:**
  Generierung von leeren Methodenrümpfen (`{ throw null!; }`) oder `partial`-Modifizierern in `AssemblyDecompilationSourceText` oder ICSharpCode-Settings, um eine saubere Roslyn-Compilation ohne synthetische Syntaxfehler zu erhalten.
- **Abgrenzung:** Performance- und Qualitätsoptimierung.

---

### 3. Missing Features

#### FINDING-EPIC02-04: Keine XML-Dokumentationsanreicherung aus Begleitdateien

- **Kategorie:** Missing Feature
- **Priorität:** P3
- **Größe:** S
- **Vertrauensgrad:** Hoch
- **Betroffene Komponenten:**
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyDecompilationAdapter.cs` (Zeile 135)
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyRoslynWorkspaceFactory.cs`
- **Soll-Ist-Abweichung:**
  In `AssemblyDecompilationAdapter.CreateDecompiler` ist `ShowXmlDocumentation = false` fest gesetzt. Liegt neben einer untersuchten `.dll` eine gleichnamige `.xml`-Dokumentationsdatei vor, werden deren Doc-Comments nicht in die dekompilierte Syntax oder den Roslyn-Snapshot übernommen.
- **Evidenz:**
  - `AssemblyDecompilationAdapter.cs` Zeile 135: `ShowXmlDocumentation = false`.
- **Auswirkung:**
  Informationen aus XML-Dokumentationskommentaren von ausgelieferten NuGet- oder Vendor-Assemblies stehen im Symbolgraphen nicht zur Verfügung.
- **Empfehlung:**
  Prüfen, ob eine Begleit-XML existiert, und optional `ShowXmlDocumentation = true` bzw. `XmlDocumentationProvider` in Roslyn einbinden.
- **Abgrenzung:** Funktionale Lücke bei der Dokumentationsanalyse.

---

## Offene Unsicherheiten

1. **Performance bei vollen Bodies:** Das Einfügen von `{ throw null!; }` verändert die Dateigröße der dekompilierten Snapshots leicht; dies muss gegen die Einsparungen bei Compiler-Fehler-Filtern abgewogen werden.
