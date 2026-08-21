---
status: ready
type: konzept
project_kind: brownfield
estimated_scope: large
priority: P2
agent_role: .agents/Agent-Scaffolding/dev-loop/planning/orchestrator.md
rules_dir: .agents/rules
last_updated: 2026-08-21
open_questions: []
herkunft: "1:1 uebernommen aus tasks/features/04-similar-names.md (Konsolidierung 2026-08-21)"
---

# Konzept: `similar_names` (Naming-Drift, Namensfamilien & Semantische Ähnlichkeit)

## 1. Problemstellung & Motivation

In längeren agentischen Entwicklungssessions tritt häufig **Naming-Drift** auf:

- Verschiedene Agenten oder Prompts erfinden leicht variierende Namen für dieselbe Domänen-Entität (z. B. `UserDto`, `UserData`, `UserInfoModel`, `UserModel`, `UserResponse`).
- **Methoden-Drift bei Hilfsfunktionen:** Verschiedene Hilfsmethoden werden mehrfach angelegt mit leicht abweichenden Namen, obwohl sie denselben Parameter-Typ transformieren (z. B. `GetTypeKindDescription`, `DescribeTypeKind`, `GetNamedTypeKindString`, `DescribeKind` — reale Erkenntnis aus Session 2026-08-19).
- Dieser Drift wird durch syntaktische Linter-Regeln und Token-Clone-Detection nicht erfasst, da jede Klasse für sich syntaktisch und architektonisch gültig ist und unterschiedliche Strings/ASTs nutzt.

Dies stellt die logische **Schicht 4** der Drift-Audit-Initiative dar:

* **Schicht 1:** `find_duplicates(mode="clone")` — Syntaktische Klone (Typ 1–3)
* **Schicht 2:** `find_duplicates(mode="refactoring-drift")` — Helper-Nichtverwendung ("Absence-of-Calls")
* **Schicht 3:** `find_duplicates(mode="structural")` — Semantische AST- & Typ-Ebene (Typ-4-Drift / Zwillingsmethoden)
* **Schicht 4:** `similar_names` — Lexikalischer & Signatur-basierter Naming-Drift (Typen und Methoden)

## 2. Zielsetzung

Ein MCP-Audit-Tool `similar_names`, das den residenten Roslyn-Symbolgraphen nach Namens-Clustern und verdächtigen Benennungs-Familien durchsucht — sowohl auf Typ-Ebene als auch auf Methoden-/Signatur-Ebene.

> Hinweis aus der Konsolidierung 2026-08-21: Da dies ein **neues MCP-Tool** wäre, gilt die
> Evidenzregel aus `90_bewusst-nicht-umsetzen/Konzept.md`. Die Abgrenzung zu verworfenen Ideen ist
> gegeben (deterministisch, kein Ranking-Heuristik-Tool wie das verworfene `locate_task`),
> aber die Empfehlung lautet: erst Aufgabe 01 (Call-Log-Analyse) abschließen und damit
> belegen, dass Naming-Drift-Anfragen real auftreten bzw. `find_duplicates`-Modi diese
> Lücke nicht schon decken.

## 3. Methodischer Ansatz (Kein RAG / Kein Vektor-Index)

* **Rein deterministisch & lexikalisch:**
  * Symbol-Namen aus der geladenen Solution sammeln (gefiltert nach Typ oder Methode).
  * **Identifier-Tokenisierung:** Zerlegung von Identifiern in Tokens via CamelCase/PascalCase Splitting (z. B. `Get`, `Type`, `Kind`, `Description` vs. `Describe`, `Type`, `Kind`).
  * **Signatur-Kopplung (für Methoden):** Verknüpfung von Methoden-Tokens mit den Parametertypen (z. B. Methode mit Token `Type`/`Kind`, die ein `INamedTypeSymbol` als Parameter nimmt).
  * Berechnung von Ähnlichkeiten über:
    1. **Token-Jaccard-Ähnlichkeit:** Ähnliche Token-Sets (z. B. `UserDetailDto` vs. `UserInfoDto` oder `GetTypeKindDescription` vs. `DescribeTypeKind`).
    2. **Levenshtein-Distanz / Damerau-Levenshtein:** Tippfehler oder Mini-Variationen (`FormatReport` vs. `FormatReports`).
    3. **Synonym-Wörterbuch (optional/schlank):** Mappings wie `Get` ↔ `Describe` ↔ `Format` oder `Data` ↔ `Info` ↔ `Model`.
    4. **Gemeinsame Präfixe / Suffixe mit abweichenden Mittelstücken.**
* **Gruppierung zu Kandidaten-Clustern:** Ausgabe als verdächtige Namensfamilien mit Dateipfaden, damit der Agent oder Entwickler entscheiden kann, ob es sich um gewollte Differenzierung oder Drift handelt.

## 4. Werkzeug-Spezifikation

* **Tool-Name:** `similar_names` (oder Modus in einem Drift-Tool)
* **Parameter:**
  * `kind` (string, optional, Default "all"): `type`, `method`, `all`.
  * `scopeFilter` (string, optional): Projektname oder Pfad-Substring.
  * `similarityThreshold` (string, optional, Default "high"): `high` (sehr nah / fast identisch), `medium` (Verdacht auf Familie).
  * `includeSignatures` (bool, optional, Default true): Bezieht Parametertypen bei Methoden-Clustern ein.
  * `maxResults` (int, optional, Default 30): Begrenzung der angezeigten Cluster.
* **Output-Beispiel:**

```text
2 Namens-Cluster identifiziert:

1. Methoden-Cluster: [Type, Kind] mit Parameter 'INamedTypeSymbol' (3 Fundstellen)
   - src/AiNetLinter/Mcp/Tools/FileStructure/GetClassStructureTool.cs:144 -> GetTypeKindDescription(INamedTypeSymbol)
   - src/AiNetLinter/Mcp/Tools/FileStructure/GetNamespaceTreeScanner.cs:411 -> DescribeTypeKind(INamedTypeSymbol)
   - src/AiNetLinter/Mcp/Tools/DeadCode/DeadCodeFilters.cs:77 -> GetNamedTypeKindString(INamedTypeSymbol)
   Hinweis: Hohe Wahrscheinlichkeit für semantische Duplikation (Typ-4-Drift).

2. Typ-Cluster: [User, Dto/Model] (3 Fundstellen)
   - src/Domain/Models/UserDto.cs:5 -> UserDto
   - src/Domain/Models/UserData.cs:8 -> UserData
   - src/Domain/Models/UserModel.cs:12 -> UserModel
```

## 5. Akzeptanzkriterien

1. Erkennt Typ-Cluster wie `[UserDto, UserData, UserModel]` zuverlässig.
2. Erkennt Methoden-Cluster mit ähnlichem semantischen Namenskern und identischen Parametertypen (z. B. `GetTypeKindDescription` vs. `DescribeTypeKind`).
3. Filtert Standard-Framework-Namen und Trivialitäten (z. B. `ToString`, `Dispose`, `Create`) aus.
4. Funktioniert rein im RAM auf dem geladenen Roslyn-Workspace ohne externe Abhängigkeiten.
5. Unit-Tests verifizieren bekannte Drift-Cluster und schirmen gegen False-Positives ab.
