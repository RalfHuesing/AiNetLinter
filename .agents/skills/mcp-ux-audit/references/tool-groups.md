# Tool-Gruppen: Sequenzen und Chaining-Muster

Dieses Dokument dient als Nachschlagewerk für Subagenten während des Audits.
Es beschreibt die natürlichen Abhängigkeiten und empfohlenen Sequenzen zwischen
den MCP-Tools aus Agentensicht.

---

## Natürliche Tool-Gruppen

### Gruppe A: Health & Handshake
Kein `targetPath` nötig (bzw. optional) — diese Tools funktionieren global.

| Tool | Zweck | Besonderheit |
|------|-------|--------------|
| `get_server_health` | Serverstatus, Capabilities, Diagnostics | Optional `targetPath`; global ohne Target |
| `report_observability_feedback` | Feedback an Server senden | Kein Target, immer ungebunden |

### Gruppe B: Discovery-Tools
Erster Schritt in jeder Agenten-Session — orientieren, bevor man sucht.

| Tool | Zweck | Typischer Einstieg |
|------|-------|--------------------|
| `get_file_tree` | Physische Projektstruktur | `view=summary` für Überblick |
| `get_namespace_tree` | Namespace-/Typhierarchie | `namespacePrefix`, `depth` |
| `get_index_scope` | Indexabdeckung nach Dateityp | Kein Filter nötig |
| `inspect_assembly` | Public API einer .dll/.exe | Assembly-Modus Pflicht |
| `find_assembly_extensions` | Extension-Methods in Assembly | Assembly-Modus |

### Gruppe C: Symbol-Tools (Chaining-Sequenzen)
Diese Tools bauen aufeinander auf — Output eines Tools ist Input des nächsten.

| Tool | Primärer Input | Output für nächstes Tool |
|------|---------------|--------------------------|
| `find_symbol` | `namePattern`, `kind` | Symbol-ID, Symbol-Identifier |
| `get_feature_context` | `symbolIdentifier` | Caller-Symbole, Test-Symbole |
| `get_symbol_body` | `symbolId` (aus find_symbol) | Vollständiger Quellcode |
| `get_class_structure` | `symbolIdentifier` | Member-Liste |
| `get_file_skeleton` | `targetFile` | Datei-Signaturen |
| `find_references` | `symbolIdentifier` | Referenz-Fundstellen |
| `get_call_tree` | `symbolIdentifier` | Aufrufbaum (incoming/outgoing) |
| `get_impact` | `symbolIdentifier` | Impact-Analyse |
| `get_type_hierarchy` | `symbolIdentifier` | Basisklassen, Interfaces |
| `find_implementations` | `symbolIdentifier` | Konkrete Implementierungen |
| `resolve_type_origin` | `symbolIdentifier` | Definierende Assembly |

### Gruppe D: Fehlerbehandlung
Kein eigenes Tool — quer durch alle Tools prüfen.

Jedes Tool der Gruppen A–C und E muss folgende Fehlerfälle korrekt behandeln:
- Fehlendes Pflichtfeld
- Falscher Feldtyp
- Nicht-existenter `targetPath`
- `targetPath` auf Verzeichnis statt Datei
- Ungültige Enum-Werte

### Gruppe E: Cross-Tool-Konsistenz
Kein eigenes Tool — vergleichende Analyse über Gruppen A–C.

---

## Standard-Chaining-Sequenzen

### Sequenz 1: Symbol-Deep-Dive (häufigste Agenten-Sequenz)
```
find_symbol(namePattern=<name>)
  → Symbol-ID extrahieren
  → get_symbol_body(symbolId=<id>)         # Implementierung lesen
  → get_feature_context(symbolIdentifier=<name>)  # Kontext: Caller, Tests
  → find_references(symbolIdentifier=<name>)       # Alle Verwendungsstellen
  → get_impact(symbolIdentifier=<name>)            # Impact-Analyse
```

### Sequenz 2: Klassen-Erkundung
```
find_symbol(namePattern=<class>, kind=class)
  → get_class_structure(symbolIdentifier=<class>)  # Members
  → get_symbol_body(symbolId=<member-id>)          # Einzelne Members
  → get_type_hierarchy(symbolIdentifier=<class>)   # Vererbung
```

### Sequenz 3: Datei-Erkundung
```
get_file_tree(view=tree, treeDepth=2)
  → Unterverzeichnis wählen
  → get_file_tree(view=files, root=<subdir>)
  → get_file_skeleton(targetFile=<file>)   # Signaturen
  → get_symbol_body(symbolId=<method-id>) # Implementierung einzelner Methode
```

### Sequenz 4: Assembly-Erkundung (Assembly-Modus)
```
inspect_assembly(targetPath=<dll>)
  → Public Types identifizieren
  → find_symbol(namePattern=<type>)        # Snapshot-basiert
  → get_symbol_body(symbolId=<id>)        # Decompile-Stub
  → find_assembly_extensions(targetPath=<dll>)  # Extension-Methods
```

---

## Bekannte Abhängigkeiten und Einschränkungen

### Assembly-Modus vs. Source-Modus

| Eigenschaft | Source-Modus (.sln) | Assembly-Modus (.dll) |
|-------------|---------------------|-----------------------|
| `find_symbol` | Vollständiger Roslyn-Index | Snapshot-basiert |
| `find_references` | Vollständig | Nur mit `includeReferences=true` |
| `get_call_tree` | Vollständig | Eingeschränkt |
| `get_impact` | Vollständig | Eingeschränkt |
| Lint (`get_violations`) | Verfügbar wenn konfiguriert | `unsupported` |
| `get_symbol_body` | Quellcode | Decompile-Stub |
| `search_pattern` | Alle Dateien | Nicht verfügbar |

### Tool-Einschränkungen die klar signalisiert werden müssen
- Assembly-Modus ohne `includeReferences`: Referenzsuche eingeschränkt → Muss im Response erklärt werden, nicht silent-empty
- `get_violations` ohne konfigurierte Rules: `RULES_INVALID` → Muss klar kommuniziert werden
- Sehr tiefe Suchbäume: Trunkierung → `completeness=truncated` + `next`-Hinweis
