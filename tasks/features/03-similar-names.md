# Konzept: `similar_names` (Naming-Drift & Namensfamilien)

## 1. Problemstellung & Motivation
In längeren agentischen Entwicklungssessions tritt häufig **Naming-Drift** auf:
- Verschiedene Agenten oder Prompts erfinden leicht variierende Namen für dieselbe Domänen-Entität (z. B. `UserDto`, `UserData`, `UserInfoModel`, `UserModel`, `UserResponse`).
- Ähnlich benannte Hilfsmethoden werden mehrfach angelegt (`ParseDate`, `ParseDateTime`, `TryParseDateString`).
- Dieser Drift wird durch Linter-Regeln nicht erfasst, da jede Klasse für sich syntaktisch und architektonisch gültig ist.

Dies stellt die logische **Schicht 3** der Drift-Audit-Initiative dar (nachdem DRY mit `find_duplicates` gelöst wurde).

## 2. Zielsetzung
Ein MCP-Audit-Tool `similar_names`, das den residenten Roslyn-Symbolgraphen rein lexikalisch nach Namens-Clustern und verdächtigen Benennungs-Familien durchsucht.

## 3. Methodischer Ansatz (Kein RAG / Kein Vektor-Index)
* **Rein deterministisch & lexikalisch:**
  * Symbol-Namen aus der geladenen Solution sammeln (gefiltert nach Typ oder Methode).
  * Zerlegung von Identifiern in Tokens (CamelCase / PascalCase Splitting, z. B. `User`, `Info`, `Dto`).
  * Berechnung von Ähnlichkeiten über:
    1. **Token-Jaccard-Ähnlichkeit:** Ähnliche Token-Sets (z. B. `UserDetailDto` vs. `UserInfoDto`).
    2. **Levenshtein-Distanz / Damerau-Levenshtein:** Tippfehler oder Mini-Variationen (`FormatReport` vs. `FormatReports`).
    3. **Gemeinsame Präfixe / Suffixe mit abweichenden Mittelstücken.**
* **Gruppierung zu Kandidaten-Clustern:** Ausgabe als verdächtige Namensfamilien, damit der Agent oder Entwickler entscheiden kann, ob es sich um gewollte Differenzierung oder Drift handelt.

## 4. Werkzeug-Spezifikation

* **Tool-Name:** `similar_names` (oder Modus in einem Drift-Tool)
* **Parameter:**
  * `kind` (string, optional, Default "type"): `type`, `method`, `all`.
  * `scopeFilter` (string, optional): Projektname oder Pfad-Substring.
  * `similarityThreshold` (string, optional, Default "high"): `high` (sehr nah / fast identisch), `medium` (Verdacht auf Familie).
  * `maxResults` (int, optional, Default 30): Begrenzung der angezeigten Cluster.
* **Output:**
  * Markdown-Tabelle / Liste von Clustern mit Fundorten (`File:Line`).
  * `StructuredContent` mit Rohdaten für LLM-Verarbeitung.

## 5. Akzeptanzkriterien
1. Erkennt Cluster wie `[UserDto, UserData, UserModel]` zuverlässig.
2. Filtert Standard-Framework-Namen und Trivialitäten aus.
3. Funktioniert rein im RAM auf dem geladenen Roslyn-Workspace ohne externe Abhängigkeiten.
4. Unit-Tests verifizieren bekannte Drift-Cluster und schirmen gegen False-Positives ab.
