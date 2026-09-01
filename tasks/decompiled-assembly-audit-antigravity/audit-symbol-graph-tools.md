# 360-Grad-Audit: Symbol Graph & Navigation Tools

## Scope und untersuchte MCP-Tools

- `find_symbol`: Namens-, Muster- und Kind-basierte Symbolsuche (`type`, `method`, `property`, `interface`, etc.) über Projekt- und Assembly-Ziele.
- `get_symbol_body`: Punktgenaue Extraktion des Quellcodes oder on-demand dekompilierten Rumpfs eines Symbols.
- `find_references`: Transitive Verwendungsstellen-Suche mit Bounded Depth und Vollständigkeitsanzeige.
- `get_call_tree`: Transitive Call-Graph-Traversierung (eingehende/ausgehende Aufrufe, Zyklen-Erkennung, DI-Heuristik).
- `get_type_hierarchy`: Vererbungs- und Implementierungsbäume (Basisklassen, abgeleitete Klassen, Schnittstellen).
- `dependency_graph`: Typ- und Namespace-Abhängigkeitsgraph mit Zyklen-Erkennung.
- `get_impact`: Auswirkungsanalyse bei Änderungen an Dateien oder Symbolen.

---

## Befunde & Begründungen

### 1. Bugs

#### FINDING-SG-01: `get_symbol_body` stürzt bei Top-Level-Typen in dekompilierten Snapshots mit `InvalidOperationException` ab

- **Kategorie:** Bug
- **Priorität:** P1
- **Größe:** S
- **Vertrauensgrad:** Hoch
- **Betroffene Komponenten:**
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/Bodies/AssemblyDecompiledBodyResolver.cs` (Zeilen 72–73)
- **Soll-Ist-Abweichung:**
  Wird `get_symbol_body` für ein `INamedTypeSymbol` (Klasse, Struct, Interface, Record, Enum) aufgerufen, greift `AssemblyDecompiledBodyResolver` auf `symbol.ContainingType` zu, um den Reflection-Typnamen zu ermitteln. Bei Top-Level-Typen ist `ContainingType` jedoch `null`. `ToReflectionTypeName(null)` liefert `""`, woraufhin `decompiler.DecompileTypeAsString(new FullTypeName(""))` eine `InvalidOperationException` wirft.
- **Evidenz:**
  - Live-Aufruf für Klassensymbole auf `LOCAL-01` und `LOCAL-02` meldet reproduzierbar:
    `bodyAvailability: unavailable; contentMode: decompiledSignatureOnly`
    `Hinweis: Body-Dekomposition fehlgeschlagen: InvalidOperationException`
- **Auswirkung:**
  Agenten können den dekompilierten Code ganzer Typen nicht einsehen, obwohl `INamedTypeSymbol` im Tool-Schema ausdrücklich unterstützt wird.
- **Empfehlung & Wunsch:**
  In `AssemblyDecompiledBodyResolver.DecompileBodyAsync` prüfen, ob `symbol` selbst ein `INamedTypeSymbol` ist; falls ja, direkt `ToReflectionTypeName(type)` verwenden.
- **Abgrenzung:** Klarer Implementierungsfehler im On-Demand-Body-Resolver.

#### FINDING-SG-02: `find_references` gibt irreführende Vollständigkeits-Garantie bei dekompilierten Assemblies

- **Kategorie:** Bug
- **Priorität:** P2
- **Größe:** S
- **Vertrauensgrad:** Hoch
- **Betroffene Komponenten:**
  - `src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblyFindReferencesTool.cs` (Zeilen 68–71)
  - `src/AiNetLinter/Mcp/Tools/SymbolGraph/TransitiveCallGraphFormatter.cs`
- **Soll-Ist-Abweichung:**
  In dekompilierten Assemblies ohne dekompilierte Methodenrümpfe (`contentMode=decompiledSignatureOnly`) findet Roslyn keine Aufrufe innerhalb von Methoden. `TransitiveCallGraphFormatter` wertet 0 gefundene Aufrufer fälschlicherweise als "vollständige Suche" und hängt an:
  `[HINWEIS]: Diese Daten sind vollstaendig fuer den angefragten Scope — kein zusaetzliches Read/Grep noetig.`
- **Evidenz:**
  - Live-Aufruf von `find_references` auf `LOCAL-01` erzeugt diesen Text.
- **Auswirkung:**
  Der Agent glaubt mit höchster Konfidenz, dass eine Methode im gesamten Code niemals aufgerufen wird (False Negative), obwohl die Aufrufe in Wirklichkeit gar nicht analysiert werden konnten.
- **Empfehlung & Wunsch:**
  Bei dekompilierten Snapshots im Modus `decompiledSignatureOnly` muss der Vollständigkeitshinweis unterdrückt werden. Stattdessen ist ein erklärender Hinweis auszugeben:
  `Hinweis: In dekompilierten Signature-Only-Sessions werden Methodenrümpfe nicht auf Aufrufe durchsucht.`
- **Abgrenzung:** Semantischer Fehler in der Ergebnis-Projektion.

---

### 2. Optimierungen

#### FINDING-SG-03: `find_symbol` ohne `includeReferences` liefert bei externen Typen 0 Treffer ohne Navigationshilfe

- **Kategorie:** Optimierung
- **Priorität:** P2
- **Größe:** S
- **Vertrauensgrad:** Hoch
- **Betroffene Komponenten:**
  - `src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblyFindSymbolTool.cs`
- **Soll-Ist-Abweichung:**
  Wird ein Typ gesucht, der aus einer referenzierten DLL stammt, liefert `find_symbol` im Standardaufruf (`includeReferences=false`) 0 Treffer. Es erfolgt kein Hinweis, dass das Symbol möglicherweise in den referenzierten Bibliotheken existiert.
- **Evidenz:**
  - Suche nach externen Schnittstellen (z. B. aus Core/Framework) schlägt stillschweigend fehl.
- **Auswirkung:**
  Erhöhte Rundenanzahl für den Agenten.
- **Empfehlung & Wunsch:**
  Bei 0 Treffern und `targetType='assembly'` einen Hinweis einblenden:
  `Tipp: Bei targetType='assembly' kann 'includeReferences=true' gesetzt werden, um auch Referenz-Assemblies zu durchsuchen.`
- **Abgrenzung:** UX- und Discoverability-Optimierung.

---

### 3. Missing Features

#### FINDING-SG-04: `get_impact` unterstützt keine Assembly-Targets

- **Kategorie:** Missing Feature
- **Priorität:** P2
- **Größe:** M
- **Vertrauensgrad:** Hoch
- **Betroffene Komponenten:**
  - `src/AiNetLinter/Mcp/Registration/SymbolGraphToolRegistrations.cs` (Zeilen 143–163)
  - `src/AiNetLinter/Mcp/Tools/SymbolGraph/GetImpactTool.cs`
- **Soll-Ist-Abweichung:**
  `get_impact` mit `symbolIdentifier` (Auswirkungsanalyse für ein konkretes Symbol) wird bei `targetType='assembly'` pauschal mit `ASSEMBLY_TARGET_UNSUPPORTED` abgewiesen.
- **Evidenz:**
  - Live-Aufruf von `get_impact` auf `LOCAL-01` liefert `ASSEMBLY_TARGET_UNSUPPORTED`.
- **Auswirkung:**
  Agenten können die hierarchischen und transitiven Auswirkungen eines Symbols in einer Assembly nicht mit einem einzigen Befehl erfassen.
- **Empfehlung & Wunsch:**
  `AssemblySessionCall` für den `symbolIdentifier`-Modus von `get_impact` bereitstellen.
- **Abgrenzung:** Funktionale Lücke im Symbolgraph-Werkzeugkasten.

---

## Verifikations-Matrix der Symbol Graph Tools

| Werkzeug | Getestete Szenarien | Verifiziertes Verhalten | Bewertung |
|---|---|---|---|
| `find_symbol` | Multi-Pattern (`Speichern`, `Save`), Kind-Filter (`class`, `method`), Regex | Schnelle und präzise Trefferliste mit Dateipfaden, Zeilen und stabilen IDs. | **Sehr gut** |
| `get_symbol_body` | Methoden, Konstruktoren, Top-Level-Klassen, Properties | Funktional für Methoden im Projekt; Bugs bei Top-Level-Typen & Accessors in Assemblies. | **Verbesserungsbedarf** (Bugs P1 & P2) |
| `find_references` | Exakte DocCommentIds, Methoden-Symbole, transitive Aufrufer | Präzise Lokalisierung im Projekt; fehlerhafter Sufficiency-Hint bei dekompilierten Snapshots. | **Gut** (Bug P2) |
| `get_call_tree` | Eingehende & ausgehende Aufrufe, Depth 1–3, Rekursions-Guard | Solide Traversierung mit Zyklen-Erkennung; bounded depth schützt zuverlässig vor Token-Explosion. | **Sehr gut** |
| `get_type_hierarchy` | Basisklassen, abgeleitete Klassen, Schnittstellen | Liefert saubere hierarchische Bäume über Vererbungs- und Implementierungsbeziehungen. | **Sehr gut** |
| `dependency_graph` | Typ- und Namespace-Ebene, Zyklenprüfung | Strukturierte Graph-Ausgabe; visualisiert Koppelung und Architekturabhängigkeiten. | **Sehr gut** |
| `get_impact` | Projektmodus (Git-Diff-Impact & Symbol-Impact) | Leistungsfähige Auswirkungsanalyse für Quellcode; fehlt für Assembly-Targets. | **Gut** (Missing Feature P2) |
