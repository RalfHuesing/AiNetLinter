---
status: open
type: step-plan
task: magic-values-in-mcp
step: 003
corrects: null
title: "EPIC-2 — Erweiterte Heuristiken, Args-Aktivierungen, Suppression & Doku-Abschluss"
epic: EPIC-2
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: MiniMax-M3
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-14T22:37:14+02:00
related_to: []
---

# Step 003: EPIC-2 — Erweiterte Heuristiken, Args-Aktivierungen, Suppression & Doku-Abschluss

## Bezug

- **Task:** `magic-values-in-mcp`
- **Epic:** `EPIC-2` aus `roadmap.md` — Erweiterte Heuristiken (`nameof_candidates`, `enum_candidates`, `standard_candidates`-Erweiterung, `security_candidates`, duplizierte `const`-Felder), Args-Aktivierungen (`includeSuppressed`, `includeTests`, `changedOnly`) und Doku-Abschluss (Suppression-Sonderfall-Hinweis präzisieren).
- **Konzept-Referenz:** `konzept.md` §Muss-Haven Blöcke „Suppression", „Diff-Scope" und „restliche Kategorien", §„Verworfene Alternativen" (Suppression-Begründung), §„Wo im Projekt" (Testpfad-Korrektur aus `roadmap.md`).

## Aktueller Projektzustand (JIT-Kontext)

Beim Lesen des aktuellen Stands habe ich vorgefunden:

- **`MagicValuesCategories.cs`** (39 Zeilen) — Enum `MagicValueCategory` mit allen 7 Werten (`ConfigCandidates`/`ConstantCandidates`/`EnumCandidates`/`NameofCandidates`/`LocalizationCandidates`/`StandardCandidates`/`SecurityCandidates`) und `MagicValueCategoryExtensions.ToStringValue()`/`AllCategoryIds()`. Stabile snake_case-IDs für JSON-RPC und `categoryFilter`-Validierung.
- **`MagicValuesClassifier.cs`** (343 Zeilen) — Trivial-/Attribut-/Index-/Loop-/GetHashCode-Filter, plus die String-Heuristiken Connection-String/URL/Windows-Pfad/Format-String/Header-Identifier und die Auslagerung der Number-Heuristiken nach `MagicValuesNumberClassifier.cs`. `MagicValueClassifierOptions` ist bereits ein Record mit `IncludeTests`/`IncludeSuppressed`-Booleans, aktuell in `Classify` ein No-op (`_ = options;`). Die Heuristiken für `EnumCandidates`/`NameofCandidates`/`LocalizationCandidates`/`SecurityCandidates` liefern in der aktuellen Version 0 Treffer.
- **`MagicValuesNumberClassifier.cs`** (184 Zeilen) — HTTP-Statuscodes (100-599), Timeout-Parameter-Erkennung via `SemanticModel`, Schwellenwert-Doppelt-Konstanten in `const`/`readonly`/`static`-Feldern. `ResolveStatusCodeName` für die 200/201/204/.../504-Mappings.
- **`FindMagicValuesScanner.cs`** — `ScanAsync` orchestriert `SelectDocuments` → `WalkDocumentsAsync` → `BuildResult` (Aggregation + Trunkierung + Payload-Bau). `MagicValueSyntaxWalker.ProcessLiteral` ist die zentrale Per-Literal-Pipeline. `VisitInterpolatedStringExpression` (EPIC-1) synthetisiert `LiteralExpressionSyntax`-Knoten für statische `InterpolatedStringText`-Segmente, damit die existierenden Heuristiken ohne Doppel-Logik greifen. `MagicValueWalkerContext` ist ein 7-Feld-Record (Pflicht zur Einhaltung von `MaxConstructorDependencies: 5`).
- **`FindMagicValuesTool.cs`** (182 Zeilen) — `FindMagicValuesToolArgs`-Record akzeptiert bereits alle 9 Args inkl. `IncludeTests`/`IncludeSuppressed`/`ChangedOnly` (alle drei Default `false`, aktuell in EPIC-1 No-op im Scanner). `ResolveValueType` und `ResolveCategory` liefern recoverable `INVALID_ARGUMENT`-Errors bei unbekannten Werten. Tool-Konstruktion der `FindMagicValuesScannerParameters` durchgereicht.
- **`FindMagicValuesScannerTests.cs`** (377 Zeilen) — `ScanAsync_IncludeSuppressedFalse_IsNoOpInEpic1` ist der EPIC-1-Platzhalter-Anker, der in EPIC-2 umgedreht werden muss (Literal wird unterdrückt, statt gemeldet).
- **`Docs/agent-api.md`** Zeile 335-364 — Der Abschnitt „**`find_magic_values` — Structured Output im Detail**" und der „**Suppression-Sonderfall**"-Hinweis existieren bereits aus EPIC-1. Die Beschreibung muss in EPIC-2 präzisiert werden: `includeSuppressed: false` ist nicht mehr No-op, sondern wirksam; die `enum_candidates`/`nameof_candidates`/`security_candidates`-Heuristiken sind nicht mehr 0-Treffer-Platzhalter.
- **`HotspotMapBuilder.cs:23` + `GetHotspotsScanner.cs:27`** — Bestandsfund `private const double WarnThreshold = 0.80;` ist genau das Muster, das die neue „duplizierte const"-Heuristik in EPIC-2 melden wird (Dogfooding-Anker).
- **`DiffImpactAnalyzer.cs:122`** — `ParseGitDiffHunks(string gitDiffOutput)` liefert `Dictionary<string, List<int>>` (Datei → geänderte Zeilen); für `changedOnly` brauchen wir nur die Schlüsselmenge. `RunGitDiff` (private, Zeile 60+ mit `git diff`-Aufruf) ist die Quelle für den unparsed Output.

## Intention

Nach diesem Step liefert `find_magic_values` die volle Muss-Haven-Liste aus dem Konzept: alle 7 Heuristik-Kategorien sind aktiv, die drei Args `includeSuppressed`/`includeTests`/`changedOnly` wirken sich tatsächlich auf den Scan aus, und die Doku präzisiert den Suppression-Sonderfall als implementierte (nicht geplante) Abweichung. Die existierende Pipeline (`MagicValueSyntaxWalker.ProcessLiteral` + `MagicValueWalkerContext`) wird wiederverwendet, neue Heuristiken kommen als optionale Sub-Routinen dazu, und die Heuristik-Detail-Tests landen in `FindMagicValuesScannerHeuristicTests.cs` (neue Datei oder Erweiterung der bestehenden), damit `MaxLineCount: 500` pro Datei eingehalten wird.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Mcp/Tools/MagicValues/MagicValuesClassifier.cs` (Zeile 78-117, 226-289)

- **Was:** `ClassifyNonTrivial` ruft `ClassifyString` und `MagicValuesNumberClassifier.ClassifyNumber` — diese werden so erweitert, dass sie `nameof_candidates`, `security_candidates` und `standard_candidates` (Erweiterung um Buffer-/Zeit-Konstanten) erkennen. `IncludeSuppressed`-Respektierung wandert aus dem `_ = options`-No-op in eine echte `HasDisableComment(node)`-Prüfung am Anfang von `Classify` (Rückgabe `NotMagic()` bei `IncludeSuppressed: false` und vorhandener Suppression).
- **Warum:** EPIC-2 zentrale Heuristik-Implementierung. Aufteilung in mehrere Methoden hält die Klasse unter `MaxLineCount: 500`; deshalb werden `ClassifyNameofCandidate`, `ClassifySecurityCandidate` und `ClassifyStandardCandidateExtras` in eine neue Datei `MagicValuesStringHeuristics.cs` extrahiert (gleiche Aufteilung wie bereits `MagicValuesNumberClassifier.cs`).

### Datei 2: `src/AiNetLinter/Mcp/Tools/MagicValues/MagicValuesStringHeuristics.cs` (NEU)

- **Was:** Neue Datei mit **vier** statischen Methoden:
  - `ClassifyNameofCandidate(LiteralExpressionSyntax literal, SemanticModel? model)` — sammelt via Parent-Walk alle `ParameterSyntax`-/`VariableDeclaratorSyntax`-/`TypeDeclarationSyntax`-/`MemberDeclarationSyntax`-Identifier im umschließenden Member/Accessor/Method-Body, vergleicht mit `literal.Token.ValueText` (Ordinal), liefert bei Treffer `MagicValueClassification(IsMagic=true, Category=NameofCandidates, Recommendation="nameof(<Name>)", ContextHint="Name des Symbols im Scope")`.
  - `ClassifySecurityCandidate(LiteralExpressionSyntax literal, SemanticModel? model)` — bestimmt den Symbol-Namen (Parameter-Name via `TryResolveParameterName`; ansonsten `EqualsValueClause` → `VariableDeclarator.Identifier.Text`; ansonsten `Argument.NameColon`); prüft `lowercase(name)` gegen HashSet `password|secret|apikey|token|connectionstring|credential|auth` (OrdinalIgnoreCase) ODER prüft String-Start gegen `AKIA|sk-|ghp_|xoxb-` (Ordinal); liefert `MagicValueClassification(IsMagic=true, Category=SecurityCandidates, Recommendation="In Secret-Store/KeyVault auslagern", ContextHint="Hartcodiertes Secret/Credential (CWE-798)")`. Wird **vor** `ClassifyString` aufgerufen, damit ein Secret-URL (`https://...`) als `SecurityCandidates` und nicht als `ConfigCandidates` klassifiziert wird.
  - `ClassifyStandardCandidateExtras(LiteralExpressionSyntax literal)` — Erweiterung der `standard_candidates` um nicht-HTTP Magic Numbers: HashSet `1024|2048|4096|8192|1000|24|60|360|1440|86400` (Puffer-Größen, Zeit-Konstanten in Sekunden/Minuten/Stunden/Tagen). Liefert `MagicValueClassification(IsMagic=true, Category=StandardCandidates, Recommendation="NamedConstant (BufferSize / MillisecondsPerSecond / SecondsPerMinute / SecondsPerHour / SecondsPerDay)", ContextHint="Well-known Konstante")`. Wird **nur** in `MagicValuesNumberClassifier.ClassifyNumber` als zusätzlicher Pfad aufgerufen, wenn weder HTTP-Statuscode noch Timeout-Parameter-Kontext noch Schwellenwert greifen.
  - `ClassifyLocalizationCandidate(LiteralExpressionSyntax literal, SemanticModel? model)` — **pragmatische Heuristik** für `localization_candidates`: das Literal ist ein Argument in einem Exception-Konstruktor (`throw new ArgumentException("…")`, `throw new InvalidOperationException("…")`, …) UND die String-Länge (ohne Whitespace) > 15 Zeichen. Liefert `MagicValueClassification(IsMagic=true, Category=LocalizationCandidates, Recommendation="IStringLocalizer / .resx", ContextHint="User-Facing Exception-Message > 15 Zeichen")`. Wird **vor** `ClassifyString` aufgerufen, damit `throw new InvalidOperationException("Connection refused from server")` korrekt als `LocalizationCandidates` erkannt wird, nicht als `ConstantCandidates` (Header-Id-Pattern). **Bewusst eng gefasst:** nur Exception-Konstruktoren + Längen-Heuristik — UI-Prompts/Logins wären weitere semantische Heuristiken (Caller-Type-Erkennung), die außerhalb des EPIC-2-Scopes liegen und als Tech-Debt dokumentiert werden.
- **Warum:** EPIC-2 fordert sechs neue Heuristik-Erweiterungen (alle Konzept-Muss-Haves für die 7 Kategorien, einschließlich `localization_candidates`). Auslagerung in eigene Datei verhindert `MaxLineCount: 500`-Überschreitung in `MagicValuesClassifier.cs` und `MagicValuesNumberClassifier.cs`. Reine Funktions-Datei ohne Records, deshalb minimaler Test-API-Overhead. Die `localization_candidates`-Heuristik wird explizit als pragmatisch eng markiert, damit der globale Kritiker sie nicht als zu lasch bewertet — die Scope-Erweiterung auf UI/Logins bleibt Tech-Debt (siehe `Bekannte Ausnahmen`).

### Datei 3: `src/AiNetLinter/Mcp/Tools/MagicValues/MagicValuesNumberClassifier.cs` (Zeile 31-76)

- **Was:** `ClassifyNumber` ruft am Ende (vor dem `return NotMagic()`) `MagicValuesStringHeuristics.ClassifyStandardCandidateExtras(literal)` auf, damit Buffer- und Zeit-Konstanten korrekt als `standard_candidates` einsortiert werden.
- **Warum:** Konsolidiert die Number-spezifische Pipeline an einer Stelle, vermeidet doppelte `LiteralExpressionSyntax`-Kind-Prüfung im Aufrufer.

### Datei 4: `src/AiNetLinter/Mcp/Tools/MagicValues/MagicValuesClassifier.cs` (Zeile 78-117)

- **Was:** Neue private Methode `HasDisableComment(LiteralExpressionSyntax literal)`:
  ```text
  foreach (var trivia in literal.GetLeadingTrivia())
      if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) && trivia.ToString().Contains("ainetlinter-disable MagicValues")) return true;
  foreach (var trivia in literal.GetTrailingTrivia())  // gleiche Prüfung
  return false;
  ```
  Aufruf in `Classify` direkt nach der Attribut-Prüfung: `if (!options.IncludeSuppressed && HasDisableComment(literal)) return NotMagic();`. Block-Kommentare (`MultiLineCommentTrivia` / `StructuredTrivia`) werden NICHT unterstützt — Konzept §Muss-Haven nennt beides, aber EPIC-2 startet pragmatisch mit Single-Line (analog zu `SuppressionCommentParser`-Default), `MultiLineCommentTrivia`-Support ist Nice-to-Have.
- **Warum:** Konzept §„Verworfene Alternativen" verlangt pro-Fundstelle-Granularität via `SyntaxTrivia`, **nicht** `SuppressionScanner`-Datei-Semantik. Performance-Vorteil: kein zweiter File-I/O-Pass, Auswertung im selben AST-Walk. Der `_ = options;`-No-op verschwindet.

### Datei 5: `src/AiNetLinter/Mcp/Tools/MagicValues/FindMagicValuesScanner.cs` (Zeile 133-187, 304-311, 76-111)

- **Was (a) `SelectDocuments` / `TrySelectDocument`:** Zwei neue Filter:
  1. `includeTests: false` (Default) → `relativePath` enthält `\\Tests\\` oder `\\FastTests\\` (OrdinalIgnoreCase) → Datei überspringen. Pfad-Match gegen den relativen Pfad (zur SolutionDir), nicht den absoluten.
  2. `changedOnly: true` → vor `SelectDocuments` wird `git diff` im Solution-Root aufgerufen: `var diffOutput = DiffImpactAnalyzer.RunGitDiff(solutionDir, gitSinceRef: null);` (gibt es in `DiffImpactAnalyzer.cs` als private Methode — muss ggf. auf `internal static` hochgestuft werden, dann in EPIC-2-Step-Result dokumentieren); `var changedFiles = DiffImpactAnalyzer.ParseGitDiffHunks(diffOutput).Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);` → beim Selektions-Filter `relativePath` MUSS in `changedFiles` sein (Forward-Slash normalisiert).
- **Was (b) `WalkDocumentsAsync` / `MagicValueSyntaxWalker`:** Neue Override `VisitIfStatement` und `VisitSwitchStatement`/`VisitSwitchExpression` (siehe Datei 6 für Details), die `enum_candidates` über AST-Vergleich Identifier-gegen-Literale in Verzweigungen sammeln.
- **Was (c) `BuildResult` / neuer Pass:** Nach `WalkDocumentsAsync` läuft `DetectDuplicateConstFieldsAsync(raw, p.Solution, ct)` als Solution-weite Aggregation:
  1. Iteriere alle `Project`s/Document/s, parse jeden Tree, sammle alle `FieldDeclarationSyntax` mit `const`-Modifier.
  2. Pro Feld: `(Type, Value, FieldName, FilePath, ClassName)`.
  3. Gruppiere über `(Type, Value)` mit ≥ 2 Einträgen in ≥ 2 verschiedenen Files.
  4. Pro Gruppe: erzeuge `RawMagicValue`-Einträge mit `Category=ConstantCandidates`, `Recommendation="Hochstufung in [Klassenname]Constants.cs (aktuell dupliziert in: <Datei1>, <Datei2>)"`, `ContextHint="Dupliziertes const-Feld"`.
  5. Wird in `ScanAsync` als finaler Pass vor `BuildResult` aufgerufen; in `raw` einsortiert, damit `AggregateAndFilter` + Trunkierung den neuen Fund korrekt mit einsortieren.
- **Was (d) `MagicValueWalkerContext`:** Zwei neue Felder `IsTestPath: bool` (vorberechnet in `WalkDocumentsAsync` aus dem relativen Pfad) und `ChangedFiles: IReadOnlySet<string>?` (vorberechnet in `ScanAsync`); beide werden in `ProcessLiteral` an `MagicValuesClassifier.Classify` als Teil des `MagicValueClassifierOptions` durchgereicht — letzteres als zusätzliches Feld `IncludeTests` (existiert bereits, Wert kommt jetzt aus dem Walker-Context statt fest `false`).
- **Warum:** Alle drei Args-Aktivierungen finden hier statt. `includeTests` ist ein billiger Pfad-Substring-Filter in `TrySelectDocument`; `changedOnly` braucht einen Git-Aufruf + ein Set-Lookup; `enum_candidates` und duplizierte consts brauchen Walker-/Solution-weite Aggregation, die nicht in der Per-Literal-Pipeline passt. Der `MagicValueWalkerContext` wird von 7 auf 9 Felder wachsen — das ist über `MaxConstructorDependencies: 5`, ABER Records mit ≥ 6 Feldern sind gemäß `AiNetLinter.mdc` explizit ausgenommen (nur `class`-Konstruktoren sind limitiert). Kommentar im Record entsprechend ergänzen.

### Datei 6: `src/AiNetLinter/Mcp/Tools/MagicValues/FindMagicValuesScanner.cs` (Zeile 319-359, neuer `VisitXxx`)

- **Was:** Neue Walker-Overrides:
  - `VisitIfStatement(IfStatementSyntax node)` — sammelt alle `BinaryExpressionSyntax` der Form `IdentifierToken `==` LiteralExpression` oder umgekehrt innerhalb der `Condition` UND in den verketteten `Else.Clauses` (rekursiv bis `Else` non-null und `Else.Statement is IfStatementSyntax`); gruppiert die Literale pro Identifier-Name; bei ≥ 3 gleichen Identifier-Treffern in der gesamten Kaskade: synthetisiert für jedes Literal `RawMagicValue` mit `Category=EnumCandidates`, `Recommendation="enum <IdentifierNamePascalCase> { ... }"`, `ContextHint="Diskretes Set gleicher Identifier-Vergleiche"`. **Wichtig:** Wird VOR `ProcessLiteral` für das jeweilige Literal ausgewertet, damit das einzelne Literal nicht doppelt als `ConfigCandidates` gemeldet wird — d.h. die IfStatement-Erkennung schreibt direkt in den `Sink` und die `ProcessLiteral`-Pfade für diese Literale werden via Set übersprungen.
  - `VisitSwitchStatement(SwitchStatementSyntax node)` / `VisitSwitchExpression(SwitchExpressionSyntaxSyntax node)` — analog: sammelt alle `CaseSwitchLabelSyntax` mit `LiteralExpression` und prüft, ob gegen denselben Identifier verglichen wird; bei ≥ 3 Treffern: gleiche Behandlung.
- **Warum:** `enum_candidates` ist AST-übergreifend (mehrere Literale gegen denselben Identifier); eine Per-Literal-Heuristik kann das nicht erkennen. Die Vorab-Sammlung im Walker ist der einzige saubere Pfad.

### Datei 7: `src/AiNetLinter/Mcp/Tools/MagicValues/FindMagicValuesTool.cs` (Zeile 27, 75-77)

- **Was:** `FindMagicValuesToolArgs`/`FindMagicValuesScannerParameters` durchreichen bereits alles. **Keine Code-Änderung hier** — nur die No-op-Kommentare (`// EPIC-1 No-op (siehe Classifier)`) in Zeile 76-77 entfernen, weil das Verhalten ab EPIC-2 echt wirksam ist. Neuer Kommentar: `// EPIC-2: wirksam — siehe Classifier (includeSuppressed) und Scanner (includeTests, changedOnly)`.
- **Warum:** Ehrliche Doku. Args-Record ist seit EPIC-1 vollständig — EPIC-2 aktiviert nur die bestehenden Felder.

### Datei 8: `src/AiNetLinter/Mcp/Tools/MagicValues/MagicValuesClassifier.cs` (Zeile 17-31, Options-Record)

- **Was:** `MagicValueClassifierOptions` bekommt ein zusätzliches Feld `IsTestPath: bool` (aus dem Walker-Context), damit der Classifier nicht erneut den Pfad selbst prüfen muss. Default `false` für syntaktische Unit-Tests ohne Walker-Context.
- **Warum:** Konsolidiert die Walker-→ Classifier-Brücke. Saubere Test-Isolation: Helper-Tests ohne Pfad-Information funktionieren weiter mit `IsTestPath: false` (dann werden Test-Pfade nicht zusätzlich gefiltert — der Test ruft typischerweise direkt auf, nicht über `SelectDocuments`).

### Datei 9: `src/AiNetLinter.Core/DiffImpactAnalyzer.cs` (Zeile ~60, Sichtbarkeit)

- **Was:** Falls `RunGitDiff` aktuell `private` ist, auf `internal static` hochstufen, damit `FindMagicValuesScanner` darauf zugreifen kann. **Sichtbarkeits-Only-Change, keine Verhaltensänderung.** Im `step-003/step-result.md` dokumentieren.
- **Warum:** EPIC-2-Vorgabe verbietet Duplikation der Git-Diff-Mechanik. Alternative wäre Dependency Injection einer `IDiffProvider`-Schnittstelle — das wäre Architektur-Drift und außerhalb des Scopes.

### Datei 10: `Docs/agent-api.md` (Zeile 234, 335-364)

- **Was (a) Tool-Tabellen-Zeile (Zeile 234):** Beschreibungstext präzisieren: `includeSuppressed?` und `changedOnly?` sind nicht mehr „No-op in aktueller Version", sondern wirksam mit der in der Detail-Sektion beschriebenen Granularität. Heuristiken für `enum_candidates`/`nameof_candidates`/`security_candidates` sind nicht mehr „0 Treffer in der aktuellen Version".
- **Was (b) Structured-Output-Detail (Zeile 335):** Textblock „Heuristiken fuer `enum_candidates`/`nameof_candidates`/`localization_candidates`/`security_candidates` sind Bestandteil einer Folgeversion" entfernen/ersetzen. Stattdessen: „Alle 7 Heuristik-Kategorien sind aktiv. `localization_candidates` liefert in der Praxis selten Treffer (heuristisch auf User-Facing-Strings in Exception-Konstruktoren beschränkt) — Trefferquote ist abhängig vom Codebase-Stil."
- **Was (c) Suppression-Sonderfall (Zeile 364):** Text „In der aktuellen Version ist `includeSuppressed: false` der wirksame Default; `includeSuppressed: true` zeigt auch stummgeschaltete Funde (kein Heuristik-Unterschied)" ist bereits richtig — belassen. Aber den Hinweis ergänzen, dass die implementierte Granularität `SingleLineCommentTrivia` mit exaktem Substring `ainetlinter-disable MagicValues` ist (Block-Kommentare und andere Regeln werden in dieser EPIC nicht ausgewertet).
- **Warum:** Konzept §„Verworfene Alternativen" verlangt explizit die Doku-Abweichung als „bewusste Ausnahme". Die existierenden Texte sind aus EPIC-1 noch zu defensiv formuliert.

### Datei 11: `src/AiNetLinter.FastTests/Mcp/Tools/FindMagicValuesScannerHeuristicTests.cs` (NEU oder Erweiterung)

- **Was:** Neue Test-Methoden (alle `[Trait("Category", "Component")]`):
  - `Classify_NameofCandidate_StringMatchesParameterName` — `void M(string foo) { throw new ArgumentNullException("foo"); }` → 1 Fund mit `Category=nameof_candidates`, `Recommendation="nameof(foo)"`.
  - `Classify_NameofCandidate_StringDoesNotMatchAnySymbol_IsNotMagic` — gleiches Pattern, aber `"bar"` statt `"foo"` → 0 Funde.
  - `Classify_SecurityCandidate_ParameterNamedPassword` — `void M(string password) { Connect("sk-abc123"); }` (zwei Literale, eines unter Security-Pattern) → 1 Fund mit `Category=security_candidates`, `ContextHint` enthält „CWE-798" und „AKIA/sk-/..."-Hinweis.
  - `Classify_SecurityCandidate_AwsAccessKeyPrefix` — `var key = "AKIAIOSFODNN7EXAMPLE";` → 1 Fund `security_candidates`.
  - `Classify_StandardCandidateExtras_BufferSize1024` — `const int BufSize = 1024;` → 1 Fund `standard_candidates`, Recommendation enthält „BufferSize" o. ä.
  - `Classify_StandardCandidateExtras_TimeConstant1000` — `var ms = 1000;` (in nicht-Schwellenwert-Kontext) → 1 Fund `standard_candidates`, Recommendation enthält „MillisecondsPerSecond".
  - `Classify_DuplicateConstFields_TwoClassesSameValue` — zwei Klassen mit identischem `const double WarnThreshold = 0.80;` in verschiedenen Files (z. B. `HotspotMapBuilder.cs` + `GetHotspotsScanner.cs` als In-Memory-Fixture) → 2 Funde (einer pro Datei), `Category=constant_candidates`, Recommendation enthält beide Pfade.
  - `Classify_EnumCandidates_IfElseCascade` — `if (status == "Pending") {...} else if (status == "Active") {...} else if (status == "Failed") {...}` → 3 Funde mit `Category=enum_candidates`, Recommendation enthält „enum Status" o. ä.
  - `Classify_EnumCandidates_OnlyTwoComparisons_IsNotEnum` — `if (status == "Pending") {...} else if (status == "Active") {...}` → 0 Funde (Schwelle ≥ 3).
- **Warum:** Jede Heuristik bekommt 1-2 Pflicht-Tests; das ist die Mindest-Sicherheit, dass die Implementierung tatsächlich greift. `enum_candidates` braucht einen Negativ-Test, weil die ≥ 3-Schwelle sonst stille 2-Treffer-Fälle erzeugen würde.

### Datei 12: `src/AiNetLinter.FastTests/Mcp/Tools/FindMagicValuesScannerTests.cs` (Zeile 339-355)

- **Was:** `ScanAsync_IncludeSuppressedFalse_IsNoOpInEpic1` umbenennen in `ScanAsync_IncludeSuppressedFalse_SuppressesLiteralWithDisableComment` und umdrehen: `const string Url = "https://api.example.com";` mit direkt darüber stehendem `// ainetlinter-disable MagicValues`-Kommentar → 0 Funde (statt 1).
- **Warum:** Der Test war in EPIC-1 ein expliziter „Platzhalter-Anker, der das aktuelle Verhalten dokumentiert". In EPIC-2 wird er zum echten Suppressions-Test.

### Datei 13: `src/AiNetLinter.FastTests/Mcp/Tools/FindMagicValuesScannerTests.cs` (NEUE Tests)

- **Was:** Drei neue Test-Methoden:
  - `ScanAsync_IncludeSuppressedTrue_ReportsLiteralWithDisableComment` — gleiche Source wie oben, aber `includeSuppressed: true` → 1 Fund.
  - `ScanAsync_IncludeTestsFalse_ExcludesTestPaths` — Solution mit zwei Files: `src/Production/Foo.cs` (URL-Literal) und `tests/FastTests/Bar.cs` (URL-Literal); `includeTests: false` (Default) → nur 1 Fund aus Foo.cs.
  - `ScanAsync_IncludeTestsTrue_IncludesTestPaths` — gleiche Source, `includeTests: true` → 2 Funde.
  - `ScanAsync_ChangedOnlyTrue_LimitsToChangedFiles` — echtes Git-Fixture: `Path.GetTempPath()` + `git init` + `git add` + `git commit`; zwei `.cs`-Dateien, eine committed (=unverändert), eine nach `commit` geändert; `changedOnly: true` → 1 Fund nur in der geänderten Datei.
  - `ScanAsync_ChangedOnlyFalse_ScansAllFiles` — gleiches Git-Fixture, `changedOnly: false` → 2 Funde.
- **Warum:** Pflicht-Tests für die Args-Aktivierungen. `ChangedOnly` braucht zwingend ein echtes Git-Repo (siehe „Bekannte Ausnahmen").

### Datei 14: `src/AiNetLinter.FastTests/Mcp/Tools/FindMagicValuesTestHelpers.cs` (Zeile 27-81)

- **Was:** Die drei `RunAsync`-Overloads bekommen zwei neue optionale Parameter `includeTests: bool = false` und `changedOnly: bool = false`, die an `FindMagicValuesScannerParameters` durchgereicht werden. `ScanAsyncParams`-Record ebenfalls um beide Felder erweitert (Defaults passend zu Tool-Defaults).
- **Warum:** Konsistente Test-Helper-API: ohne diese Erweiterung müssten die neuen Tests den `FindMagicValuesScannerParameters`-Record manuell konstruieren (siehe `ScanAsync_ScopeFilterNoMatch_RetrunsTextOnlyWithoutPayload` Zeile 271-282 — das ist genau das Anti-Muster, das die Helper eigentlich vermeiden sollen).

## Tests

- [ ] `Classify_NameofCandidate_StringMatchesParameterName` (Nameof-Heuristik, Treffer)
- [ ] `Classify_NameofCandidate_StringDoesNotMatchAnySymbol_IsNotMagic` (Nameof-Heuristik, kein Treffer)
- [ ] `Classify_SecurityCandidate_ParameterNamedPassword` (Security, Name-Heuristik)
- [ ] `Classify_SecurityCandidate_AwsAccessKeyPrefix` (Security, Präfix-Heuristik)
- [ ] `Classify_StandardCandidateExtras_BufferSize1024` (Standard-Erweiterung, Buffer)
- [ ] `Classify_StandardCandidateExtras_TimeConstant1000` (Standard-Erweiterung, Zeit)
- [ ] `Classify_DuplicateConstFields_TwoClassesSameValue` (Duplizierte const)
- [ ] `Classify_EnumCandidates_IfElseCascade` (Enum, 3+ Vergleich)
- [ ] `Classify_EnumCandidates_OnlyTwoComparisons_IsNotEnum` (Enum, Schwelle)
- [ ] `Classify_LocalizationCandidate_ExceptionMessageLongerThan15` (Localization, Exception-Argument mit langer Message)
- [ ] `Classify_LocalizationCandidate_ShortExceptionMessage_IsNotMagic` (Localization, Schwelle: ≤ 15 Zeichen → kein Fund)
- [ ] `Classify_DuplicateConstFields_OnlyOneOccurrence_IsNotReported` (Duplizierte const, Schwelle: ≥ 2 Files)
- [ ] `ScanAsync_IncludeSuppressedFalse_SuppressesLiteralWithDisableComment` (Umdrehung des EPIC-1-Platzhalter-Ankers)
- [ ] `ScanAsync_IncludeSuppressedTrue_ReportsLiteralWithDisableComment` (EPIC-2 Suppression wirksam)
- [ ] `ScanAsync_IncludeTestsFalse_ExcludesTestPaths` (Test-Projekte aus, Default)
- [ ] `ScanAsync_IncludeTestsTrue_IncludesTestPaths` (Test-Projekte an)
- [ ] `ScanAsync_ChangedOnlyTrue_LimitsToChangedFiles` (Git-Fixture, geänderte Dateien)
- [ ] `ScanAsync_ChangedOnlyFalse_ScansAllFiles` (Git-Fixture, alle Dateien)
- [ ] **Bestehende Tests bleiben grün** — insbesondere `ScanAsync_IncludeSuppressedFalse_IsNoOpInEpic1` wird umbenannt und umgedreht; `ScanAsync_InterpolatedString_StaticTextSegmentsClassified` (EPIC-1 In-String-Magic-Value), `ScanAsync_TrivialLiterals_AreNeverReported`, `ScanAsync_IgnoreNumbers_ExtendsTrivialList` (kritisch: neue `standard_candidates` darf `1000` NICHT als trivial behandeln, wenn `ignoreNumbers` das nicht abdeckt — Test muss ggf. mit `ignoreNumbers: [1000]` explizit gemacht werden).

## Definition of Done

- [ ] Alle 14 „Konkrete Änderungen" umgesetzt
- [ ] `dotnet build` (Solution) grün — `TreatWarningsAsErrors=true` weiterhin halten
- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` grün
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` grün
- [ ] Conventional Commit auf Branch `main` mit Subject ≤ 72 Zeichen, Suffix `[magic-values-in-mcp]`, Trailer `Refs: tasks/magic-values-in-mcp/step-003` (Beispiel: `feat: erweiterte Heuristiken, Suppression, includeTests/changedOnly für find_magic_values [magic-values-in-mcp]`)
- [ ] `tasks/magic-values-in-mcp/step-003/step-result.md` geschrieben (Vorlage aus `step-001/step-result.md` wiederverwenden)
- [ ] `status` in `step-plan.md` von `open` auf `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc` §1 — MCP-Tools dogfooding (Symbolgraph- und `ainetlinter`-MCP statt `rg`/`grep` für `find_symbol`/`find_references`); gilt für den Coder, der in den Magic-Value-Dateien arbeitet.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §2 — Workflow-Konventionen (Step-Result-Pflicht, Commit-Vorschlag am Antwortende, No-Task-Artefakt-Verweise in Code-Kommentaren — der Kommentar im `MagicValueWalkerContext` darf NIE auf `step-003` oder `tasks/magic-values-in-mcp/...` verweisen).
- `.agents/rules/AiNetLinterRichtlinien.mdc` §4 — Commit-Konventionen (Conventional Commits auf Deutsch, imperativ, Suffix `[magic-values-in-mcp]`, Trailer `Refs: tasks/magic-values-in-mcp/step-NNN`).
- `.agents/rules/AiNetLinterRichtlinien.mdc` §5 — Antwort-Stil (Conclusions first, Evidence after).
- `.agents/rules/AiNetLinter.mdc` (auto-synced) — bestätigt: `MaxLineCount: 500` pro Datei, `MaxMethodLineCount: 60`, `MaxMethodParameterCount: 4`, `MaxCognitiveComplexity: 15`, `EnforceSealedClasses: true`, `EnforceNullableEnable: true`, `TreatWarningsAsErrors: true`, `BanPublicNestedTypes: true`. Records mit ≥ 6 Feldern sind von `MaxConstructorDependencies: 5` ausgenommen (gilt für `MagicValueWalkerContext`, der auf 9 Felder wächst).

## Bekannte Ausnahmen

- **`ScanAsync_ChangedOnlyTrue_LimitsToChangedFiles` braucht ein echtes Git-Repo** unter `Path.GetTempPath()/ainetlinter-changedonly-<guid>/`. Setup: `git init`, eine Datei committen, eine zweite Datei anlegen/ändern. Cleanup im `IDisposable` der Test-Klasse. Alternative wären LibGit2Sharp-Mocks, aber das wäre Test-Infrastruktur-Drift ohne klaren Mehrwert — die echte Git-Pfad-Abdeckung ist hier wertvoller. Test ist nicht Stress-kategorisiert (kein Parallelismus, nur eine Repo-Init-Sequenz), Laufzeit < 2s.
- **Anpassung des EPIC-1-Anker-Tests `ScanAsync_IncludeSuppressedFalse_IsNoOpInEpic1`** — der Test wird umbenannt auf `ScanAsync_IncludeSuppressedFalse_SuppressesLiteralWithDisableComment` und umgedreht (Literal wird unterdrückt, statt gemeldet). Die Quellcode-Datei selbst (`FindMagicValuesScannerTests.cs`) bleibt die gleiche; nur der Test-Inhalt ändert sich. Wenn ein Reviewer streng auf „Anker-Test aus EPIC-1 nicht ändern" besteht, ist die Alternative: neuen Test daneben anlegen und alten Test löschen. Beide Wege sind OK; der Coder entscheidet.
- **`RunGitDiff`-Sichtbarkeit in `DiffImpactAnalyzer.cs`** muss von `private` auf `internal static` hochgestuft werden (eine reine Sichtbarkeits-Änderung, kein Verhalten). Im `step-result.md` ist das mit `// Sichtbarkeit-Only-Change für EPIC-2 find_magic_values/changedOnly` zu dokumentieren.
- **`localization_candidates`-Heuristik** wird in EPIC-2 als **eng gefasste Pragmatik-Implementierung** umgesetzt (Exception-Konstruktor + String-Länge > 15; siehe Datei 2 `ClassifyLocalizationCandidate`). UI-Prompts/Logins sind außerhalb des Scopes und bleiben **offene Tech-Debt** in `tech-debt.md` (`auto_fixable: nein` — Caller-Type-Heuristik bräuchte Architektur-Entscheidung).
- **Block-Kommentar-Suppression (`/* ainetlinter-disable MagicValues */`)** ist im Konzept §Muss-Haven genannt. In EPIC-2 wird `MultiLineCommentTrivia` als **Nice-to-Have-Sekundärprüfung** in `HasDisableComment` aufgenommen, falls die Heuristik sauber bleibt (`!IsKind(SyntaxKind.SingleLineCommentTrivia) && !IsKind(SyntaxKind.MultiLineCommentTrivia)`-Filter, dann `.ToString().Contains(...)`). Falls das zu Komplexität führt, dokumentiert der Coder die Reduktion im `step-result.md` und die Block-Kommentar-Suppression bleibt als **offene Tech-Debt** in `tech-debt.md`. Single-Line ist der harte Pflicht-Pfad.
- **`StandardCandidateExtras`-Konstanten** sind hartkodiert (`1024|2048|4096|8192|1000|24|60|360|1440|86400`). Eine spätere Version könnte sie konfigurierbar machen (`ignoreNumbers` ist schon der Mechanismus dafür, aber es ist für Projekte ungewöhnlich, eigene Buffer-Größen explizit zu ignorieren — Standard-Liste ist praxisnäher).

## Code-Skizze (optional)

```
// Datei 2 — MagicValuesStringHeuristics.cs (Skizze, nicht final)
internal static class MagicValuesStringHeuristics
{
    private static readonly HashSet<string> SecurityNameKeywords = new(StringComparer.OrdinalIgnoreCase)
        { "password", "secret", "apikey", "token", "connectionstring", "credential", "auth" };
    private static readonly string[] SecurityPrefixes = { "AKIA", "sk-", "ghp_", "xoxb-" };
    private static readonly HashSet<int> StandardExtraNumbers = new() { 1024, 2048, 4096, 8192, 1000, 24, 60, 360, 1440, 86400 };

    internal static MagicValueClassification ClassifyNameofCandidate(LiteralExpressionSyntax literal, SemanticModel? model) { /* Scope-Walk + Identifier-Compare */ }
    internal static MagicValueClassification ClassifySecurityCandidate(LiteralExpressionSyntax literal, SemanticModel? model) { /* Name+Prefix-Check */ }
    internal static MagicValueClassification? ClassifyStandardCandidateExtras(LiteralExpressionSyntax literal) { /* HashSet<int>-Check, return null wenn kein Treffer */ }
}

// Datei 4 — HasDisableComment (in MagicValuesClassifier.cs)
private static bool HasDisableComment(LiteralExpressionSyntax literal)
{
    foreach (var trivia in literal.GetLeadingTrivia())
        if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) && trivia.ToString().Contains("ainetlinter-disable MagicValues", StringComparison.Ordinal))
            return true;
    foreach (var trivia in literal.GetTrailingTrivia())
        if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) && trivia.ToString().Contains("ainetlinter-disable MagicValues", StringComparison.Ordinal))
            return true;
    return false;
}

// Datei 5(c) — DetectDuplicateConstFieldsAsync (Skizze in FindMagicValuesScanner.cs)
private static async Task DetectDuplicateConstFieldsAsync(
    List<RawMagicValue> sink, Solution solution, CancellationToken ct)
{
    var byValueAndType = new Dictionary<(string Type, string Value), List<(string FieldName, string FilePath, string ClassName)>>();
    foreach (var project in solution.Projects)
    foreach (var document in project.Documents)
    {
        if (document.SourceCodeKind != SourceCodeKind.Regular) continue;
        var tree = await document.GetSyntaxTreeAsync(ct).ConfigureAwait(false);
        if (tree is null) continue;
        var root = await tree.GetRootAsync(ct).ConfigureAwait(false);
        foreach (var fieldDecl in root.DescendantNodes().OfType<FieldDeclarationSyntax>())
        {
            if (!fieldDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword))) continue;
            foreach (var variable in fieldDecl.Declaration.Variables)
            {
                if (variable.Initializer?.Value is not LiteralExpressionSyntax lit) continue;
                // ... aggregieren ...
            }
        }
    }
    // Gruppen mit >= 2 verschiedenen Files -> sink mit RawMagicValue befuellen
}
```

## Notes

- **Walker-Context wächst auf 9 Felder** — `MagicValueWalkerContext` ist ein `record`, nicht ein `class`-Konstruktor; das `MaxConstructorDependencies: 5`-Limit aus `AiNetLinter.mdc` gilt nur für Klassen-Konstruktoren. Records mit ≥ 6 Feldern sind explizit ausgenommen. Im `MagicValueWalkerContext`-Doc-Kommentar wird das entsprechend dokumentiert.
- **`enum_candidates`-Erkennung in `VisitIfStatement`** muss VOR der `ProcessLiteral`-Auswertung für die jeweiligen Literale laufen, sonst meldet der Standard-Pfad das Literal doppelt (einmal als `ConfigCandidates` via `ConnectionString`-Heuristik, einmal als `EnumCandidates`). Praktische Lösung: ein `HashSet<LiteralExpressionSyntax>` mit „bereits durch Enum-Kaskade klassifizierten" Literalen, das in `ProcessLiteral` zuerst geprüft wird.
- **`changedOnly` mit leerem Git-Output** (z. B. frisch initialisiertes Repo ohne Commits): `ParseGitDiffHunks("")` liefert ein leeres Dictionary → keine Datei im `changedFiles`-Set → `SelectDocuments` mit `changedOnly=true` und leerem Set liefert 0 Dateien → Text-Result „Keine Dateien im Scope". Das ist die richtige Semantik (kein Fund ohne Diff-Information).
- **`includeTests`-Pfad-Match** gegen `\\Tests\\` und `\\FastTests\\` (Substring) ist die Konzept-Vorgabe. Achtung: bei Test-Projekten, die nicht `Tests` im Pfad haben (z. B. `MyApp.UnitTests/`), würden sie nicht herausgefiltert. Bessere Variante wäre `*.Tests` als Suffix-Match auf den Assembly-Namen via `document.Project.AssemblyName` — aber das wäre Spec-Drift ohne klaren Auftrag. Konzept-Wortlaut beibehalten.
- **`SecurityCandidates` vs. `ConfigCandidates` für URLs:** Wenn ein Literal eine `https://...`-URL ist UND der umgebende Symbol-Name auf ein Secret hindeutet (`apiUrl`-Parameter NICHT, aber `apiKey`-Parameter JA), gewinnt `SecurityCandidates`. Reihenfolge im `ClassifyString`: Security zuerst, dann URL, dann Windows-Pfad, dann Format-String, dann Header-Identifier, dann Nameof, dann NotMagic. So entstehen keine Doppel-Meldungen.
- **Duplizierte `const`-Felder und `IncludeSuppressed`:** Die Bestandsfund-Stelle `WarnThreshold = 0.80` in `HotspotMapBuilder.cs:23` + `GetHotspotsScanner.cs:27` ist die Referenz für den Test `Classify_DuplicateConstFields_TwoClassesSameValue`. Wenn `IncludeSuppressed: false` ist und einer der beiden Files `// ainetlinter-disable MagicValues` an der Zeile hat, wird der Eintrag für DIESEN File unterdrückt — das könnte die Heuristik komplett aushebeln. Pragmatische Lösung: die Heuristik läuft auf AST-Ebene (`FieldDeclarationSyntax` direkt), nicht über `LiteralExpressionSyntax` → die `HasDisableComment`-Prüfung auf dem Literal greift hier nicht, weil wir die Field-Declaration aggregieren, nicht das Literal. Das ist semantisch korrekt: das `const` ist eine Definition, kein Anwendungs-Literal. Im `step-result.md` dokumentieren.
- **`Suppression` ohne `MagicValue`-Regelname** (z. B. `// ainetlinter-disable` ohne Folgetext): `Contains("ainetlinter-disable MagicValues")` matcht das nicht → keine Unterdrückung. Konzept §Muss-Haven spezifiziert die volle Syntax; bewusst strenger Match.
- **`ChangedOnly` und `git diff` Output-Format:** `RunGitDiff` ruft `git diff` (ohne `gitRef`-Argument = uncommittete Änderungen) auf. `ParseGitDiffHunks` parsed das Standard-Unified-Diff-Format. Edge-Cases: Binärdateien, gelöschte Dateien, umbenannte Dateien — irrelevant, weil `ChangedOnly` nur die Datei-Menge braucht und der Filter auf `.cs` ohnehin binäre/renamed Files ausschließt.
