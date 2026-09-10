# Gruppe B: Discovery & Target-Routing — Befundbericht

## Geprüfte Tools
- `get_file_tree`
- `get_namespace_tree`
- `get_index_scope`
- `inspect_assembly`
- `find_assembly_extensions`
- `search_assembly`
- `get_assembly_context`

---

### [Positiv / Best Practice] Exzellente Discovery-Architektur
1. **`get_index_scope`**:
   - Unterscheidet `.cs` (durch Symbolgraph voll abgedeckt) von Nicht-C#-Dateitypen (`.csproj`, `.json`, `.razor`, `.xaml`).
   - Liefert konkrete Routing-Empfehlungen inklusive Parametern (z. B. `routing=search_pattern(pattern, scopeType=all, includePatterns=**/*.json)`).
   - Verhindert, dass Agenten versuchen, Konfigurationsdateien über `find_symbol` zu suchen.
2. **`inspect_assembly`**:
   - Funktioniert hervorragend auf realen DLLs (`LOCAL-01`, `LOCAL-02`) und EXEs (`LOCAL-03`).
   - Trennt öffentliche API, Namespaces, Typen, Members und Referenz-Sessions deterministisch.
   - Weist dekompilationsbedingte Diagnosen transparent aus (`Diagnosen: N von M (gekürzt)`).
3. **`get_file_tree` auf Assemblies**:
   - Unterstützt sowohl Source-Solutions als auch Decompiled Assemblies nahtlos ([AssemblyGetFileTreeTool.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/FileStructure/AssemblyGetFileTreeTool.cs)).
   - Bildet den virtuellen / dekompilierten SourceRoot im Cache ab, sodass Agenten die Struktur einer Kunden-DLL wie ein lokales Quellcode-Repository erkunden können.

---

### Beobachtungen aus Freier Erkundung (Gruppe B)
- **Assembly-Decompilation-Speed**: Die Dekompilation und Indexierung von `LOCAL-01` (~48 Typen, 19k LoC) dauerte beim Erstaufruf ca. 35 Sekunden, alle nachfolgenden Aufrufe waren dank Disk- und Memory-Cache im Sub-Sekunden-Bereich.
- **EXE-Unterstützung (`LOCAL-03`)**: Verwaltete Windows-Executables mit 93 Namespaces und 737 Typen wurden einwandfrei verarbeitet; die Top-Namespaces werden sauber zusammengefasst (`Top 10 Namespaces und 83 weitere`).
- **Paging in `search_assembly`**: Funktioniert mittels `continuationToken` perfekt. Token wie `v1.5.5E1B...` lassen sich ohne Datenverlust an den Folgeaufruf übergeben.
