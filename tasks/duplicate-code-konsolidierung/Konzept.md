---
status: ready
type: konzept
project_kind: brownfield
estimated_scope: small
rules_dir: .agents/rules
last_updated: 2026-08-11
open_questions: []
---

# Konzept: Echte DuplicateCode-Funde im eigenen Repo konsolidieren

## Kurzfassung

Das MCP-Feature M9 (`find_duplicates`-Tool + `DuplicateCodeChecker`-Linter-Regel) hat im Dogfooding
gegen das eigene Repo 9 echte `exact`-Cluster (Jaccard 1,00) gefunden. Dieses Konzept legt für jeden
Cluster **verbindlich** fest, wie er aufgelöst wird — entweder durch eine konkrete Konsolidierung
(Heimat, Signatur, Sichtbarkeit) oder durch eine begründete Suppression. Kein Cluster bleibt offen.

**Was wir tun:** 9 minimale, saubere Refactorings. Jeder Cluster bekommt eine klare Heimat
(`internal static class` in einem sinnvollen Namespace) und einen Commit.

**Was wir NICHT tun:** Keine erzwungenen Interface- oder Generic-Constraint-Konstrukte. Keine
Pauschal-Suppression. Keine breite architektonische Umorganisation. Keine Änderungen an
`DuplicateDetectionEngine`/`DuplicateCodeChecker` selbst. Keine Test-Refactorings "weil wir gerade
dabei sind".

## Ziel (Was & Warum)

Diese Duplikate existierten überwiegend schon vor M9 — das neue Tool macht sie nur sichtbar. Sie
wurden bewusst nicht im M9-Scope mitkorrigiert (9 Cluster über ~18 unbeteiligte Dateien, kein
M9-Code), sondern als eigenständiger Task hier nachgezogen. M9 ist abgeschlossen, jetzt kommt
die Aufräumung.

Aktueller Nachweis (jederzeit reproduzierbar):
```bash
dotnet run --project src/AiNetLinter -c Release -- -p AiNetLinter.slnx -c rules.json
```

## Scope

### Muss-Haben

- Für jeden der 9 unten aufgeführten Cluster: Konsolidierung gemäß unten dokumentierter Entscheidung
  ODER begründete Suppression (genau ein Fall: Cluster 5).
- Nach jedem Cluster gezielter Test-Lauf für die berührte(n) Klasse(n):
  `dotnet test AiNetLinter.slnx -c Release --filter "(FullyQualifiedName~<Klasse>)&Category!=Stress"`.
- Abschluss-Verifikation: `find_duplicates`/`DuplicateCodeChecker` zeigt für diese 9 Fälle keine
  offenen Funde mehr.
- Finaler Volllauf `dotnet test AiNetLinter.slnx -c Release --filter "Category!=Stress"` grün.
- Pro Cluster ein eigener Conventional-Commit auf Deutsch.

### Non-Goals (bewusst NICHT Teil davon)

- **Weitere Duplicate-Code-Suche über diese 9 Cluster hinaus** — neuer, unbegrenzter Scope.
  Künftige Funde gehören in einen eigenen Folge-Task (idealerweise über den Drift-Audit-Skill vor
  dem nächsten Epic-Abschluss).
- **`near`-/`fuzzy`-Cluster** — `DuplicateCodeChecker` meldet ohnehin nur `exact` automatisch
  (M9-Entscheidung). `near`/`fuzzy` bleiben informell über `find_duplicates` einsehbar.
- **Änderungen an `DuplicateDetectionEngine`/`DuplicateCodeChecker` selbst** — M9-Code,
  abgeschlossen.
- **`SyncAgentRulesCommand` ↔ `AgentRulesGenerator.Sync(AgentRulesSyncOptions)` Angleichen** —
  die beiden Klassen rufen im Kern dieselbe Pipeline (`ResolveBaseDirectory` → `ResolveAgentRulesPath`
  → `DetectBaselineUsage` → `GenerateContent` → Schreiben) mit kleinen Abweichungen auf. Das ist
  ein eigenes Architektur-Thema, **nicht** Teil dieses Tasks. Wird hier nur erwähnt, damit klar
  ist, dass es gesehen wurde.
- **README-/ROADMAP-Updates für rein interne Refactorings** — Regeln verlangen Updates nur bei
  Features/Konfiguration, nicht bei reinen Code-Internas. ROADMAP-Eintrag gibt's schon (M9).
- **`git commit --amend`** — Spezifikation §10.3 und Memory-Eintrag verbieten es.

## Verworfene Alternativen (allgemein)

- **Alles pauschal per `// ainetlinter-disable DuplicateCode` unterdrücken:** verworfen. Bei den
  zwei 3-fachen Klonen (`ResolveSeverity`, `CountLines`) ist echte Konsolidierung klar besser
  (drei statt eine Stelle bei künftigen Änderungen pflegen ist ein reales Risiko, kein Stilproblem).
  Pauschale Suppression würde das Tool selbst entwerten.
- **Alles pauschal konsolidieren, auch über künstliche Interfaces / generische Constraints:**
  verworfen. Wenn Element-Typen nicht strukturell kompatibel sind, würde ein gemeinsames Interface
  oder ein `<T>`-Constraint den Code verkomplizieren, ohne echten Wiederverwendungs-Gewinn. Eine
  ehrliche Suppression mit echtem *Why*-Kommentar ist dann sauberer (genau ein Fall: Cluster 5,
  `GetHotspotsScanner` dokumentiert diese Entscheidung in seinem XML-Doc schon heute).

## Entscheidungen pro Cluster (verbindlich)

Jeder Cluster hat eine konkrete Empfehlung mit Ziel-Signatur/Heimat. Reihenfolge der Abarbeitung
siehe „Konkrete Umsetzungsschritte".

### Cluster 1 — `FindGitRoot` (2-fach)

- **IST:** identische `private static string? FindGitRoot(string startPath)` in
  `src/AiNetLinter/Core/DiffImpactAnalyzer.cs:77` und
  `src/AiNetLinter/Scope/GitChangedFilesResolver.cs:26`. Beide byte-für-byte gleich.
- **ZIEL:** neue Utility in `src/AiNetLinter/Core/GitRootLocator.cs`:
  ```csharp
  internal static class GitRootLocator
  {
      internal static string? Find(string startPath)
      {
          var current = File.Exists(startPath) ? Path.GetDirectoryName(startPath) : startPath;
          while (!string.IsNullOrEmpty(current))
          {
              if (Directory.Exists(Path.Combine(current, ".git"))) return current;
              current = Path.GetDirectoryName(current);
          }
          return null;
      }
  }
  ```
- **Aufrufer:** `DiffImpactAnalyzer.FindGitRoot(...)` → `GitRootLocator.Find(...)` (lokal ersetzen,
  alte `private static`-Methode löschen). `GitChangedFilesResolver.FindGitRoot(...)` →
  `GitRootLocator.Find(...)` (lokal ersetzen, alte `private static`-Methode löschen).
- **Begründung:** kleine, semantisch eigenständige Hilfsfunktion, klare Heimat in `Core/`
  (passt zu `DiffImpactAnalyzer` als Hauptnutzer; `Scope/` ist Konsument).
- **Sichtbarkeit:** `internal static class`, `internal static string? Find(string)`. Beide
  Aufrufer sind im selben Assembly, `internal` reicht.

### Cluster 2 — `ResolveBaseDirectory` (2-fach)

- **IST:** identische Methode in
  `src/AiNetLinter/Commands/SyncAgentRulesCommand.cs:88` (`internal static`) und
  `src/AiNetLinter/Generators/AgentRulesGenerator.cs:131` (`private static`).
- **Verifiziert:** `SyncAgentRulesCommand.Run` ruft bereits drei Methoden auf `AgentRulesGenerator`
  auf (`ResolveAgentRulesPath`, `DetectBaselineUsage`, `GenerateContent`) — die
  Abhängigkeitsrichtung `Commands → Generators` ist bereits etabliert.
- **ZIEL:** Methode in `AgentRulesGenerator` von `private static` auf `internal static` hochziehen.
  `SyncAgentRulesCommand` ersetzt die lokale Methode durch `AgentRulesGenerator.ResolveBaseDirectory(...)`.
- **Entscheidung (statt neue Utility-Klasse):** bewusst **keine** neue Utility, weil die
  Abhängigkeit `Commands → Generators` schon besteht und `AgentRulesGenerator` ohnehin die
  Generator-Pipeline koordiniert. Eine separate Utility würde nur eine weitere Indirektion
  einführen, ohne die Abhängigkeit zu reduzieren.
- **Sichtbarkeit:** `internal static string ResolveBaseDirectory(string targetPath)`.
- **Out-of-Scope-Note:** `AgentRulesGenerator.Sync(AgentRulesSyncOptions)` (siehe
  `Generators/AgentRulesGenerator.cs:33`) implementiert die gleiche Pipeline wie
  `SyncAgentRulesCommand.Run` mit kleinen Abweichungen (Verbose-Output, existierende-Datei-Check,
  Directory-Create). Das ist ein eigenes Refactoring-Thema und **nicht** Teil dieses Tasks.

### Cluster 3 — `BoolParameterChecker.CheckMethod`/`CheckConstructor` (2-fach, gleiche Klasse)

- **IST:** zwei Wrapper in `src/AiNetLinter/Core/Checkers/BoolParameterChecker.cs:12` und `:18`,
  body-byte-für-byte identisch außer den Eingabe-Typen (`MethodDeclarationSyntax` vs.
  `ConstructorDeclarationSyntax`):
  ```csharp
  internal static void CheckMethod(MethodDeclarationSyntax node, CheckerContext ctx)
  {
      if (IsPrivateOrProtected(node.Modifiers) && ctx.Config.Metrics.MaxBoolParameterCountAllowPrivate) return;
      Check(node.ParameterList, node.Identifier.Text, node, ctx);
  }
  internal static void CheckConstructor(ConstructorDeclarationSyntax node, CheckerContext ctx)
  {
      if (IsPrivateOrProtected(node.Modifiers) && ctx.Config.Metrics.MaxBoolParameterCountAllowPrivate) return;
      Check(node.ParameterList, node.Identifier.Text, node, ctx);
  }
  ```
- **ZIEL:** Expression-bodied Wrapper + private Helper:
  ```csharp
  internal static void CheckMethod(MethodDeclarationSyntax node, CheckerContext ctx)
      => CheckMember(node.Modifiers, node.ParameterList, node.Identifier.Text, node, ctx);

  internal static void CheckConstructor(ConstructorDeclarationSyntax node, CheckerContext ctx)
      => CheckMember(node.Modifiers, node.ParameterList, node.Identifier.Text, node, ctx);

  private static void CheckMember(
      SyntaxTokenList modifiers,
      ParameterListSyntax paramList,
      string memberName,
      SyntaxNode node,
      CheckerContext ctx)
  {
      if (IsPrivateOrProtected(modifiers) && ctx.Config.Metrics.MaxBoolParameterCountAllowPrivate) return;
      Check(paramList, memberName, node, ctx);
  }
  ```
- **Begründung:** Parameter sind strukturell identisch (nur Typen am Entry-Point unterschiedlich).
  Ein gemeinsamer Helper, der die gemeinsame Schnittmenge nimmt, ist **ehrlicher** als eine
  Suppression: die zwei öffentlichen Entry-Points bleiben klar lesbar (ihre Signatur dokumentiert
  die Anwendungsfälle), die Logik ist einmal.
- **Kein Breaking Change:** Signaturen von `CheckMethod`/`CheckConstructor` bleiben identisch
  (Aufrufer bleiben unverändert). `Check`, `CountBoolParameters`, `IsBoolType`,
  `IsPrivateOrProtected` bleiben unverändert.

### Cluster 4 — `FindDocumentByPath` (2-fach)

- **IST:** identische `private static Document? FindDocumentByPath(Solution, string)` in
  `src/AiNetLinter/Core/DiffImpactAnalyzer.cs:217` (laut Konzept; siehe Commit-Referenz) und
  `src/AiNetLinter/Core/LinterAutoFixer.cs:63`.
- **ZIEL:** Methode in `DiffImpactAnalyzer` von `private static` auf `internal static` hochziehen.
  `LinterAutoFixer` ruft `DiffImpactAnalyzer.FindDocumentByPath(...)` auf, lokale Kopie löschen.
- **Begründung:** `DiffImpactAnalyzer` ist die „Solution-Walk"-Heimat (`ParseGitDiffHunks`,
  `ProcessDiffLine`, etc. — allesamt Solution-/Doc-Pfad-Operationen), `LinterAutoFixer` ist
  Konsument. Eine neue Utility wäre zusätzliche Indirektion.
- **Sichtbarkeit:** `internal static Document? FindDocumentByPath(Solution solution, string filePath)`.

### Cluster 5 — `AppendSection` (2-fach) — **Suppression**

- **IST:** identische `private static void AppendSection(StringBuilder, string heading,
  IReadOnlyList<...> files, int maxLineCount)` in `src/AiNetLinter/Maps/HotspotMapBuilder.cs:87`
  (Element-Typ `StructureFileInfo` mit `RelativePath`, `Lines`, `Directory`) und
  `src/AiNetLinter/Mcp/Tools/FileStructure/GetHotspotsScanner.cs:125` (Element-Typ `HotspotFileInfo`
  mit `RelativePath`, `Lines`).
- **Entscheidung: ehrliche Suppression, keine Konsolidierung.** Begründung:
  1. Die Element-Records sind **semantisch unabhängig** (`StructureFileInfo` hat zusätzlich
     `Directory`, kommt aus `Maps/` und wird vom CLI-Map-Workflow konsumiert; `HotspotFileInfo`
     kommt aus `Mcp/Tools/FileStructure/` und wird über MCP gelesen). Ein gemeinsames Interface
     wäre nur ein Marker ohne Verhalten.
  2. Eine generische Methode mit `Func<T,string>`/`Func<T,int>`-Selektoren würde die *Aufrufseite*
     komplizieren, nicht vereinfachen — das ist genau die Sorte „cleverer Code", die wir nicht
     wollen.
  3. `GetHotspotsScanner` dokumentiert diese Architekturentscheidung **bereits explizit** im
     XML-Doc-Kommentar der Klasse: „Die zwei Schwellwert-Konstanten sind bewusst aus
     `AiNetLinter.Maps.HotspotMapBuilder` dupliziert (dessen Formatierungs-Methoden sind `private`,
     eine Abhängigkeit dorthin würde keinen echten Wiederverwendungs-Gewinn bringen)." Diese
     Begründung trifft auf `AppendSection` gleichermaßen zu.
  4. Der Code ist 13 Zeilen, gut lesbar, und die beiden Aufrufer-Klassen bleiben unabhängig
     testbar.
- **Konkrete Umsetzung:** in `GetHotspotsScanner.AppendSection`:
  ```csharp
  // ainetlinter-disable DuplicateCode — AppendSection ist hier eine private
  // Formatierungshilfe; Strukturgleichheit zu HotspotMapBuilder.AppendSection ist gewollt.
  // Die Element-Typen (HotspotFileInfo vs. StructureFileInfo) sind semantisch unabhängig,
  // eine gemeinsame Schnittstelle wäre nur ein Marker ohne Verhalten. Siehe Klasse-XML-Doc.
  private static void AppendSection(...)
  ```
  In `HotspotMapBuilder.AppendSection` **kein** Disable-Kommentar, weil der Tool nur die
  Duplikate in `GetHotspotsScanner` markiert (Vergleich läuft von beiden Seiten, aber das
  Disable auf einer Seite reicht zur Auflösung; siehe `DuplicateCodeChecker`-Logik aus M9).
  Falls die Verifikation am Ende zeigt, dass auch `HotspotMapBuilder` markiert wird, wird der
  Disable-Kommentar dort analog ergänzt.
- **Test-Strategie:** nach der Änderung `find_duplicates`/`DuplicateCodeChecker` einmal laufen
  lassen, um zu prüfen, ob der Disable auf einer Seite reicht.

### Cluster 6 — `ResolveSeverity` (3-fach) — höchste Priorität

- **IST:** identische `private static string ResolveSeverity(RuleViolation v)` in
  `src/AiNetLinter/Mcp/Tools/Analysis/GetViolationsScanner.cs:176`,
  `src/AiNetLinter/Mcp/Tools/MetricsTree/MetricsTreeRoslynScanner.cs:96`,
  `src/AiNetLinter/Mcp/Tools/Safeguard/SafeguardScanner.cs:290`. Body:
  ```csharp
  if (!string.IsNullOrEmpty(v.EffectiveSeverity)) return v.EffectiveSeverity;
  return RuleRegistry.TryResolve(v.RuleName)?.Severity ?? "warning";
  ```
- **ZIEL:** neue Utility in `src/AiNetLinter/Mcp/RuleSeverityResolver.cs`:
  ```csharp
  internal static class RuleSeverityResolver
  {
      internal static string Resolve(RuleViolation violation)
      {
          if (!string.IsNullOrEmpty(violation.EffectiveSeverity)) return violation.EffectiveSeverity;
          return RuleRegistry.TryResolve(violation.RuleName)?.Severity ?? "warning";
      }
  }
  ```
- **Aufrufer:** alle drei Klassen ersetzen ihre `private static` Methode durch
  `RuleSeverityResolver.Resolve(v)` (Aufrufseite unverändert in Semantik).
- **Begründung:** 3-facher Klon in MCP-Scanner-Kern, klar zusammengehörige Domäne. Eigene
  Utility ist hier besser als auf einer der drei Klassen, weil keine der Scanner-Klassen die
  „Heimat" ist (alle drei sind gleichberechtigte Konsumenten). Namespace `Mcp/` ist die
  gemeinsame Heimat aller drei.
- **Sichtbarkeit:** `internal static class`, `internal static string Resolve(RuleViolation)`.
- **Reihenfolge:** **zuerst** (höchster Wartungsgewinn, 3 Aufrufer).

### Cluster 7 — `CountLines` (3-fach)

- **IST:** identische `private static int CountLines(string content)` in
  `src/AiNetLinter/Web/CssAnalyzer.cs:137`, `src/AiNetLinter/Web/JsAnalyzer.cs:195`,
  `src/AiNetLinter/Web/RazorAnalyzer.Parsing.cs:262`. Body 8 Zeilen (`n=1` + Loop über `\n`).
- **ZIEL:** neue Utility in `src/AiNetLinter/Web/LineCounter.cs`:
  ```csharp
  internal static class LineCounter
  {
      internal static int Count(string content)
      {
          if (string.IsNullOrEmpty(content)) return 0;
          var n = 1;
          for (int i = 0; i < content.Length; i++)
          {
              if (content[i] == '\n') n++;
          }
          return n;
      }
  }
  ```
- **Aufrufer:** alle drei ersetzen `CountLines(content)` durch `LineCounter.Count(content)` und
  löschen ihre lokale `CountLines`-Methode.
- **Begründung:** 3-facher Klon, semantisch zusammengehörig (alle Web-Analyzer), einfache
  Logik, klar. `Web/` als Heimat passt — alle Aufrufer sind in `Web/`.
- **Reihenfolge:** **zweite** (nach Cluster 6).
- **Achtung:** `RazorAnalyzer.Parsing.cs` enthält zusätzlich eine `GetLineNumber(string, int)`
  Methode, die *nicht* dupliziert ist — die bleibt unverändert. Konsolidierung nur für
  `CountLines`.

### Cluster 8 — `FindSlnxFile` (Test-Helper, 2-fach)

- **IST:** identische `private static string? FindSlnxFile()` in
  `src/AiNetLinter.Tests/Commands/PlaybookCheckCommandTests.cs:57` und
  `src/AiNetLinter.Tests/Maps/Skeleton/SkeletonMapBuilderTests.cs:52`. Body: aufwärts von
  `AppContext.BaseDirectory` nach `*.slnx` suchen.
- **Verifiziert:** `src/AiNetLinter.Tests/TestHelper.cs` existiert bereits als `internal static`
  class mit `CreateDefaultConfig`, `ParseCode`, `CreateContext`.
- **ZIEL:** Methode nach `TestHelper` verschieben:
  ```csharp
  internal static class TestHelper
  {
      // ... bestehende Methoden ...

      internal static string? FindSlnxFile()
      {
          var dir = new DirectoryInfo(AppContext.BaseDirectory);
          while (dir != null)
          {
              var files = dir.GetFiles("*.slnx");
              if (files.Length > 0) return files[0].FullName;
              dir = dir.Parent;
          }
          return null;
      }
  }
  ```
- **Aufrufer:** beide Test-Klassen ersetzen lokales `FindSlnxFile()` durch
  `TestHelper.FindSlnxFile()` und löschen die lokale Methode. `using AiNetLinter.Tests;` ist
  ggf. schon da (gleicher Root-Namespace, je nach Test-Klasse prüfen).
- **Begründung:** bestehende Test-Fixture wiederverwenden (vermeidet Duplikations-Wildwuchs).
  `TestHelper` ist `internal static`, also die kanonische Heimat für Test-Helper in diesem
  Projekt.

### Cluster 9 — `CreateSemanticModel` (Test-Helper, 3-fach)

- **IST:** identische `private static SemanticModel CreateSemanticModel(string source)` in
  `src/AiNetLinter.Tests/Core/Checkers/MaxSwitchArmsTests.cs:19`,
  `src/AiNetLinter.Tests/Core/Checkers/SwitchDispatcherDetectorTests.cs:15`,
  `src/AiNetLinter.Tests/Core/NullCoalescingInitializerClassifierTests.cs:17`. Body: 8 Zeilen,
  `typeof(object).Assembly` als einzige Reference.
- **Wichtiger Verhaltens-Hinweis:** die drei Helper nehmen **nur** `typeof(object).Assembly`
  als Reference. Die existierende `TestHelper.ParseCode(string)` nimmt dagegen *alle* Assemblies
  aus `AppDomain.CurrentDomain.GetAssemblies()`. Ein direkter Aufruf von `ParseCode` würde
  also das Test-Verhalten ändern (mehr References, potenziell andere Bindungen).
- **ZIEL:** neue Methode in `TestHelper`, die das exakte Verhalten der drei duplizierten Helper
  beibehält:
  ```csharp
  internal static SemanticModel CreateSemanticModel(string source)
  {
      var tree = CSharpSyntaxTree.ParseText(source);
      var compilation = CSharpCompilation.Create("TestAssembly")
          .AddSyntaxTrees(tree)
          .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
          .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
      return compilation.GetSemanticModel(tree);
  }
  ```
  Die bestehende `ParseCode`-Methode bleibt **unverändert** (anderes Verhalten, anderer
  Anwendungsfall — sie liefert `(tree, semanticModel)`-Tupel und bindet alle geladenen
  Assemblies).
- **Aufrufer:** alle drei Test-Klassen ersetzen lokales `CreateSemanticModel(source)` durch
  `TestHelper.CreateSemanticModel(source)`. `using AiNetLinter.Tests;` ist je nach
  Test-Klasse ggf. zu ergänzen (Namespace-Hierarchie: `AiNetLinter.Tests.Core.Checkers` und
  `AiNetLinter.Tests.Core` → `AiNetLinter.Tests` ist jeweils ein Parent, also
  using-Directive nötig).
- **Begründung:** bestehende Test-Fixture wiederverwenden. Verhalten 1:1 erhalten (gleiche
  Reference-Liste), keine versteckte Test-Semantik-Änderung.

## Konkrete Umsetzungsschritte

### Reihenfolge

Priorisierung nach Risiko/Wartungsgewinn:

1. **Cluster 6** — `ResolveSeverity` (3-fach, MCP-Scanner-Kern, höchster Hebel)
2. **Cluster 7** — `CountLines` (3-fach, Web-Analyzer)
3. **Cluster 1** — `FindGitRoot` (2-fach, neue `Core/GitRootLocator.cs`)
4. **Cluster 2** — `ResolveBaseDirectory` (Methode auf `AgentRulesGenerator` hochziehen)
5. **Cluster 4** — `FindDocumentByPath` (Methode auf `DiffImpactAnalyzer` hochziehen)
6. **Cluster 5** — `AppendSection` (Suppression mit *Why*-Kommentar, danach `find_duplicates`-
   Verifikation ob einseitig ausreichend)
7. **Cluster 8** — `FindSlnxFile` (Test-Helper nach `TestHelper`)
8. **Cluster 9** — `CreateSemanticModel` (Test-Helper nach `TestHelper`)
9. **Cluster 3** — `BoolParameterChecker` (letzter, weil Einzelfall-Entscheidung im Konzept
   schon dokumentiert ist, hier nur noch formale Umsetzung der Wrapper-Konsolidierung)

### Test-Strategie

- **Pro Cluster:** gezielter Test-Lauf der berührten Klasse(n):
  `dotnet test AiNetLinter.slnx -c Release --filter "(FullyQualifiedName~<Klasse>)&Category!=Stress"`.
- **Abschluss:** einmal Volllauf
  `dotnet test AiNetLinter.slnx -c Release --filter "Category!=Stress"`.
- **Verifikation:** nach Cluster 5 und am Ende
  `dotnet run --project src/AiNetLinter -c Release -- -p AiNetLinter.slnx -c rules.json` —
  muss für die 9 Cluster keine `DuplicateCode`-Funde mehr zeigen.

### Commit-Strategie

- Pro Cluster **ein** eigener Conventional-Commit auf Deutsch, imperativ:
  - `refactor(duplicate-code): ResolveSeverity in RuleSeverityResolver extrahieren (3-fach)`
  - `refactor(duplicate-code): CountLines in LineCounter extrahieren (3-fach)`
  - `refactor(duplicate-code): FindGitRoot in GitRootLocator extrahieren`
  - `refactor(duplicate-code): ResolveBaseDirectory auf AgentRulesGenerator hochziehen`
  - `refactor(duplicate-code): FindDocumentByPath auf DiffImpactAnalyzer hochziehen`
  - `chore(duplicate-code): AppendSection in GetHotspotsScanner bewusst unterdruecken`
  - `refactor(tests): FindSlnxFile in TestHelper verschieben`
  - `refactor(tests): CreateSemanticModel in TestHelper verschieben`
  - `refactor(duplicate-code): BoolParameterChecker CheckMethod/CheckConstructor auf gemeinsamen Helper`
- **Kein** `git commit --amend`. Folge-Commits für Korrekturen, falls nötig.
- Am Ende optional ein `chore(duplicate-code): Konzept-Update mit konkreten Code-Empfehlungen`
  (das ist dieser Konzept-Commit).

### Was pro Cluster konkret zu tun ist (Template)

1. Ziel-Datei(en) erstellen oder ändern (siehe Empfehlungen oben).
2. Aufrufer umstellen.
3. Alte Methode/Klasse löschen.
4. Build prüfen: `dotnet build`.
5. Gezielter Test-Lauf (siehe oben).
6. Bei `// ainetlinter-disable` (nur Cluster 5): ehrlichen *Why*-Kommentar dranschreiben.
7. Commit.
8. Zurück zum Plan, nächster Cluster.

## Was wir bewusst NICHT tun (prominent)

- **Keine breite architektonische Umorganisation** aus diesen 9 Clonen. Keine „passen wir das
  Verzeichnis-Layout gleich mit an". Keine „nehmen wir das zum Anlass für ein neues
  Service-Konzept". 9 kleine, saubere Refactorings — fertig.
- **Keine `// ainetlinter-disable`-Pflichterfüllung**, wenn Konsolidierung klar besser ist.
  Suppression ist **kein** Ziel, sondern eine erlaubte Lösung **wenn** begründet (genau ein
  Fall: Cluster 5).
- **Keine `interface`-/Reflection-/`<T>`-Constraints-Konstrukte**, die das Problem nur verstecken
  statt lösen. Wenn Element-Typen nicht zusammenpassen, dann ehrlich lassen oder ehrlich
  unterdrücken.
- **Keine Änderungen an `DuplicateDetectionEngine`/`DuplicateCodeChecker`**. M9-Code bleibt
  unangetastet.
- **Keine `Rundumschlag-Test-Refactorings** („weil wir gerade dabei sind"). Tests werden nur
  angefasst, wenn sie zu einem der Refactorings gehören (Cluster 8, 9 — und auch dort nur die
  jeweilige Helper-Methode, nicht die Tests selbst).
- **Keine `// ainetlinter-disable`-Kommentare, die das *Was* statt das *Warum* dokumentieren.**
  Nur echtes *Why* — und nur, wo es nötig ist. In den 8 Konsolidierungs-Clustern braucht es gar
  keinen Kommentar, weil das Refactoring selbsterklärend ist.
- **Keine `Rundumschlag`-Sync-Agent-Rules-Aufräumaktion** in `SyncAgentRulesCommand` (siehe
  Out-of-Scope-Note Cluster 2).
- **Kein `git commit --amend`** — weder pro Cluster noch am Ende.
- **Keine README/ROADMAP-Updates** für rein interne Refactorings. Regeln verlangen Updates nur
  bei Features/Konfiguration. M9 ist in der ROADMAP schon eingetragen.

## Definition of Done / Erfolgskriterien

- Alle 9 Cluster bearbeitet (8 Konsolidierungen, 1 begründete Suppression).
- `dotnet run --project src/AiNetLinter -c Release -- -p AiNetLinter.slnx -c rules.json` zeigt
  für diese 9 Fälle keine offenen `DuplicateCode`-Funde mehr. Suppression-Fall (Cluster 5) ist
  im Code sichtbar begründet.
- `dotnet test AiNetLinter.slnx -c Release --filter "Category!=Stress"` (Volllauf) grün.
- 9 Conventional-Commits auf Deutsch, einer pro Cluster, in passender Reihenfolge (Cluster 6
  zuerst, Cluster 3 zuletzt).
- Kein `--amend`, keine Force-Pushes.

## Offene Punkte

(keine — `status: ready`)
