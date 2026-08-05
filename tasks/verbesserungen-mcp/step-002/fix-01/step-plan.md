---
status: done (pending audit)
type: step-plan
task: verbesserungen-mcp
step: 002/fix-01
title: "SkeletonSyntaxWalker: semantischen Fallback fuer Basistyp bei fehlender Basisliste ergaenzen"
epic: EPIC-01
estimated_risk: low
step_type: single
items: []
created_by: planer
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-05
related_to: [step-002/step-review.md]
---

# Step 002/fix-01: SkeletonSyntaxWalker: semantischen Fallback fuer Basistyp bei fehlender Basisliste ergaenzen

## Bezug

- **Task:** `verbesserungen-mcp`
- **Epic:** `EPIC-01` (Fix-Modus — dieser Step korrigiert `step-002`, legt
  kein neues Epic an, siehe `../../roadmap.md`, unveraendert).
- **Konzept-Referenz:** siehe `step-002/step-review.md` Finding —
  `Konzept.md` „Definition of Done" Schnell-Check-Punkt 2:
  „`get_file_skeleton(SiteView.razor.cs)` → kein `CS0115`, Basisklasse
  `ComponentBase` sichtbar". Nur dieses eine MAJOR-Finding ist Scope
  (Fix-Modus-Scope-Disziplin — `Konzept.md`/`roadmap.md` selbst werden
  nicht angefasst).

## Aktueller Projektzustand (JIT-Kontext)

- `SkeletonSyntaxWalker` (`src/AiNetLinter/Maps/Skeleton/
  SkeletonSyntaxWalker.cs`) hat bereits ein `SemanticModel`-Feld
  (`_semanticModel`, Konstruktor-Parameter) — es muss **nichts** neu
  verdrahtet werden. `BuildTypeInfo` (Zeile 110-143) ruft bereits
  `_semanticModel.GetDeclaredSymbol(node)` auf (Zeile 114, `typeSymbol`)
  — genau dieses Symbol (nicht die einzelne Syntax-Deklaration, sondern
  das über alle Partial-Deklarationen der Compilation gemergte Symbol)
  liefert bereits heute korrekt `BaseType == ComponentBase` fuer
  `SiteView` (siehe `step-002/step-plan.md` „Aktueller Projektzustand":
  `compilation.GetTypeByMetadataName("BlazorPartialMini.SiteView")
  .BaseType` = `Microsoft.AspNetCore.Components.ComponentBase`,
  identisches Symbol wie `GetDeclaredSymbol(node)` liefert). Der Fix ist
  daher eine reine Ergaenzung der bestehenden Zeile 113
  (`baseTypes`-Berechnung), kein struktureller Umbau, keine neue
  Abhaengigkeit.
- `SkeletonMapBuilder.ExtractFromDocumentAsync`
  (`src/AiNetLinter/Maps/Skeleton/SkeletonMapBuilder.cs:78-97`) uebergibt
  bereits `document.GetSemanticModelAsync()` an den Walker — auch
  `GetFileSkeletonTool` (nutzt laut Kommentar in `SkeletonMapBuilder.cs:75`
  denselben Pfad) hat also bereits Zugriff auf die volle `Compilation`
  ueber das `SemanticModel`. Kein Aenderungsbedarf an
  `GetFileSkeletonTool.cs`/`SkeletonMapBuilder.cs`.
- **Blast-Radius empirisch geprueft (nicht nur vermutet):**
  - Repo-weite Suche nach `partial class`/`partial record`/`partial
    struct` in `src/AiNetLinter/**` ergab ausschliesslich `static
    partial class` (Registry-/Utility-Klassen ohne jede Basisliste in
    irgendeiner Datei — `BaseType` waere dort ohnehin `object`, durch den
    unten beschriebenen Guard bereits ausgefiltert).
  - Repo-weite Suche nach `partial` in `tests/Fixtures/**` und
    `src/AiNetLinter.Tests/Fixtures/**` ergab **ausschliesslich**
    `SiteView.razor.cs` (`tests/Fixtures/BlazorPartialMini/...`) als
    Datei mit einer über mehrere Partial-Deklarationen gesplitteten
    Basisliste. Keine andere Fixture ist von dieser Aenderung betroffen.
  - `SkeletonSyntaxWalkerTests.cs`
    (`src/AiNetLinter.Tests/Maps/Skeleton/SkeletonSyntaxWalkerTests.cs`)
    enthaelt **keinen** Test, der `SkeletonTypeInfo.BaseTypes` fuer eine
    Klasse mit implizitem `object`-/`ValueType`-Basistyp auf `null`
    prueft — keiner der 13 bestehenden Tests dort ist von dieser
    Aenderung betroffen (verifiziert durch Volltext-Lesen der Datei).
  - `FilterCliIntegrationTests.cs`
    (`src/AiNetLinter.Tests/Cli/FilterCliIntegrationTests.cs`, nutzt
    `SkeletonMapBuilder` ueber die CLI) prueft nur Namespace-/Projekt-/
    Public-Only-Filterung anhand von Typnamen, nie den `BaseTypes`-Text
    — nicht betroffen.
  - `McpLiveRepositoryTests.cs`/`McpServerAllToolsE2ETests.cs`
    (Dogfooding von `get_file_skeleton` gegen `Program.cs` bzw.
    Fehlerfaelle) pruefen nur Nicht-Leerheit/Fehlercodes, keine
    Basistyp-Strings — nicht betroffen.
  - **Fazit:** Der einzige beobachtbare Effekt dieser Aenderung im
    gesamten Repo ist die gewuenschte Aenderung an `SiteView.razor.cs`.

## Intention

`SkeletonSyntaxWalker.BuildTypeInfo` bekommt einen semantischen Fallback:
Wenn eine Typdeklaration **keine** syntaktische Basisliste hat
(`node.BaseList == null`), aber das ueber `_semanticModel.
GetDeclaredSymbol(node)` bereits vorhandene Symbol einen `BaseType`
liefert, der weder `object` noch `System.ValueType` ist (also ein
Basistyp, der nur in einer **anderen** Partial-Deklaration derselben
Klasse ausserhalb dieser Datei steht), wird dieser Basistyp trotzdem
angezeigt — mit einem Hinweis-Suffix, dass er aus einer anderen
Partial-Deklaration stammt. Der `object`/`ValueType`-Ausschluss
verhindert, dass fuer **jede** gewoehnliche Klasse/jeden gewoehnlichen
Struct ohne explizite Basisliste ploetzlich ein impliziter Basistyp
angezeigt wird (das waere eine ungewollte, breite Verhaltensaenderung).
Nach diesem Fix erfuellt `get_file_skeleton(SiteView.razor.cs)` den
DoD-Punkt 2 aus `Konzept.md` vollstaendig: kein `CS0115` **und**
Basisklasse `ComponentBase` sichtbar.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Maps/Skeleton/SkeletonSyntaxWalker.cs` (Zeile 110-143, neuer Private Helper)

- **Was:**
  - `BuildTypeInfo` umbauen: `typeSymbol`-Zuweisung (aktuell Zeile 114)
    vor die `baseTypes`-Berechnung ziehen (aktuell Zeile 113), da der
    neue Fallback `typeSymbol` braucht.
  - Neue private static Methode `BuildBaseTypesDisplay(TypeDeclarationSyntax node, ISymbol? typeSymbol)`:
    ```csharp
    private static string? BuildBaseTypesDisplay(TypeDeclarationSyntax node, ISymbol? typeSymbol)
    {
        if (node.BaseList != null)
            return ": " + node.BaseList.Types.ToString();

        if (typeSymbol is INamedTypeSymbol { BaseType.SpecialType: not (SpecialType.System_Object or SpecialType.System_ValueType) } named)
            return $": {named.BaseType!.ToDisplayString()} (aus anderer Partial-Deklaration)";

        return null;
    }
    ```
  - `BuildTypeInfo` ruft statt der bisherigen Inline-Berechnung
    `BuildBaseTypesDisplay(node, typeSymbol)` auf.
  - **Guard-Begruendung (wichtig fuer den Coder, nicht abschwaechen):**
    Der `SpecialType.System_Object`/`System_ValueType`-Ausschluss ist
    kein Detail, sondern der Kern der Risikoeindaemmung — ohne ihn
    wuerde **jede** Klasse ohne explizite Basisliste ploetzlich `:
    System.Object` und **jeder** Struct/jedes Record-Struct ohne
    explizite Basisliste ploetzlich `: System.ValueType` anzeigen. Bitte
    beim Umsetzen nicht vereinfachen zu einem reinen
    `typeSymbol?.BaseType != null`-Check.
- **Warum:** Behebt das Finding vollstaendig, ohne den bestehenden
  syntaktischen Pfad (BaseList vorhanden) zu veraendern — dieser bleibt
  Zeile fuer Zeile identisch zum bisherigen Verhalten.

### Datei 2: `src/AiNetLinter.Tests/Baseline/SourceFileCatalogBlazorPartialTests.cs` (Zeile 13-20 Klassenkommentar, Zeile 54-68 Test 3)

- **Was:**
  - Klassenkommentar (aktuell Zeile 16-20, endet mit „get_file_skeleton
    zeigt den Basistyp fuer diese Datei weiterhin nicht an: ...") auf den
    jetzt korrekten Zustand umformulieren — dieser Satz ist nach dem Fix
    schlicht falsch und muss weg bzw. umgekehrt werden (z. B.: „...
    get_file_skeleton zeigt den Basistyp jetzt ebenfalls an, semantisch
    aufgeloest ueber das gemergte Partial-Symbol, mit einem Hinweis, dass
    er aus einer anderen Partial-Deklaration stammt.").
  - Test `GetFileSkeleton_SiteViewRazorCs_NoLongerReportsCompileError`
    (Zeile 54-68) umbenennen (z. B.
    `GetFileSkeleton_SiteViewRazorCs_ShowsComponentBaseAndNoCompileError`)
    und um eine Assertion ergaenzen, die den Basistyp prueft:
    `Assert.Contains("ComponentBase", text, System.StringComparison.Ordinal);`
    (bewusst nur auf den Typnamen pruefen, nicht auf ein exaktes Format
    wie `": ComponentBase"` — ob der Coder voll- oder minimal-qualifiziert
    rendert (`ToDisplayString()` liefert laut Test 1 bereits
    nachgewiesen den fully-qualified Namen
    `Microsoft.AspNetCore.Components.ComponentBase`), ist ein
    Formatierungsdetail, keine Verhaltensfrage). Bestehende zwei
    Assertions (`DoesNotContain("Compile-Fehler")`,
    `DoesNotContain("CS0115")`) bleiben unveraendert.
- **Warum:** Test 3 ist exakt die Stelle, an der das Finding festgemacht
  wurde (`step-002/step-review.md` Finding-Zeile 187-188) — muss die
  Vervollstaendigung des DoD-Punkts direkt belegen.

## Tests

- [ ] `SourceFileCatalogBlazorPartialTests.GetFileSkeleton_SiteViewRazorCs_ShowsComponentBaseAndNoCompileError`
      (umbenannt + neue Basistyp-Assertion, siehe oben — exakter Name
      kann beim Schreiben leicht abweichen, Kernaussage muss erhalten
      bleiben)
- [ ] `SkeletonSyntaxWalkerTests` (alle 13 bestehenden Tests) weiterhin
      gruen — explizit gegenpruefen, auch wenn die Analyse oben keinen
      betroffenen Test findet (keiner prueft `BaseTypes` fuer
      objektbasige/ValueType-basige implizite Basistypen)
- [ ] `FilterCliIntegrationTests` weiterhin gruen (nutzt
      `SkeletonMapBuilder`/denselben Walker ueber die CLI)
- [ ] Voller `dotnet test`-Lauf weiterhin gruen (dieselbe
      Sandbox-Flakiness wie in TD-003 dokumentiert ist bekannt und kein
      neuer Befund dieses Fix-Steps — bei Testhost-Absturz ohne
      Einzeltestfehler: Wiederholung, nicht als neuer Bug werten)

## Definition of Done

- [ ] `SkeletonSyntaxWalker.cs`-Aenderung umgesetzt (Datei 1)
- [ ] Test 3 umbenannt + neue Assertion, Klassenkommentar aktualisiert (Datei 2)
- [ ] `dotnet build` grün (0 Fehler/Warnungen)
- [ ] `dotnet test` (Volllauf) grün
- [ ] Commit auf aktuellem Branch (Conventional Commit, Suffix `[verbesserungen-mcp]`)
- [ ] `step-002/fix-01/step-result.md` geschrieben
- [ ] `status` in diesem `step-plan.md` von `in_progress` auf `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc#4` — xUnit-Pflicht pro
  Logik-Aenderung (hier: die neue Assertion in Test 3 ist die geforderte
  Test-Anpassung); Commit-Vorschlag-Pflicht am Ende der Antwort.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5` — Zero-Warning-Direktive
  (`TreatWarningsAsErrors`); Symptom-Fixing-Verbot ist hier **nicht**
  einschlaegig in die andere Richtung — dies ist die Behebung der
  eigentlichen Ursache, keine Abschwaechung einer Assertion.
- `.agents/rules/AiNetLinter.mdc` — Grenzwerte: `BuildTypeInfo` bleibt
  klein (Auslagerung in eigene Methode `BuildBaseTypesDisplay` haelt
  `MaxMethodLineCount`/`MaxCyclomaticComplexity`/`MaxCognitiveComplexity`
  weiterhin deutlich unter den Grenzwerten 60/12/15); keine neuen
  Kommentare mit Task-/Step-Bezug im Produktionscode (der Hinweistext
  „aus anderer Partial-Deklaration" ist Tool-**Ausgabetext** fuer den
  LLM-Konsumenten, kein Code-Kommentar — Regel nicht einschlaegig).

## Bekannte Ausnahmen

- Keine.

## Code-Skizze (optional)

```csharp
// src/AiNetLinter/Maps/Skeleton/SkeletonSyntaxWalker.cs
private SkeletonTypeInfo BuildTypeInfo(string typeKind, TypeDeclarationSyntax node)
{
    var fullName = node.Identifier.Text + (node.TypeParameterList?.ToString() ?? "");
    var typeSymbol = _semanticModel.GetDeclaredSymbol(node);
    var baseTypes = BuildBaseTypesDisplay(node, typeSymbol);
    var typeId = TryCreateDeclarationId(typeSymbol);
    // ... Rest unveraendert (ExtractMembers, Record-Parameter-Block, return) ...
}

private static string? BuildBaseTypesDisplay(TypeDeclarationSyntax node, ISymbol? typeSymbol)
{
    if (node.BaseList != null)
        return ": " + node.BaseList.Types.ToString();

    if (typeSymbol is INamedTypeSymbol { BaseType.SpecialType: not (SpecialType.System_Object or SpecialType.System_ValueType) } named)
        return $": {named.BaseType!.ToDisplayString()} (aus anderer Partial-Deklaration)";

    return null;
}
```

```csharp
// src/AiNetLinter.Tests/Baseline/SourceFileCatalogBlazorPartialTests.cs — Skizze Test 3 nach Fix
[Fact]
public async Task GetFileSkeleton_SiteViewRazorCs_ShowsComponentBaseAndNoCompileError()
{
    using var fixture = new BlazorPartialMiniFixtureWorkspace();
    var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
    using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(catalog)));

    var result = await GetFileSkeletonTool.ExecuteAsync(
        state, "src/BlazorPartialMini/SiteView.razor.cs", CancellationToken.None);

    Assert.NotEqual(true, result.IsError);
    var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
    Assert.DoesNotContain("Compile-Fehler", text, System.StringComparison.Ordinal);
    Assert.DoesNotContain("CS0115", text, System.StringComparison.Ordinal);
    Assert.Contains("ComponentBase", text, System.StringComparison.Ordinal);
}
```

## Notes

- **Warum kein weiterer Umbau von `SkeletonMapBuilder`/`GetFileSkeletonTool`
  noetig ist:** Beide reichen bereits ein vollwertiges `SemanticModel`
  (aus `document.GetSemanticModelAsync()`) an den Walker durch — die
  Compilation, aus der dieses Modell stammt, enthaelt bereits (seit
  `step-002`, dem Roslyn-Paket-Bump) die vom Razor-Generator erzeugte
  zweite Partial-Deklaration. Der Fix hier ist rein lokal in
  `SkeletonSyntaxWalker.BuildTypeInfo`.
- **Warum der Guard `SpecialType.System_Object`/`System_ValueType` und
  nicht z. B. eine Typkind-Fallunterscheidung (`class` vs. `struct`)
  ist:** `INamedTypeSymbol.BaseType` ist fuer gewoehnliche Structs/Record-
  Structs implizit immer `System.ValueType`, fuer gewoehnliche Klassen/
  Records implizit immer `System.Object`, fuer Interfaces immer `null`.
  Der SpecialType-Check filtert alle drei Faelle einheitlich raus, ohne
  dass `BuildBaseTypesDisplay` wissen muss, ob es ein `class`/`struct`/
  `interface`/`record` ist — das ist die einfachere, robustere Loesung
  (`Einfachheit vor Abstraktion`, `AiNetLinterRichtlinien.mdc#1`)
  gegenueber einer expliziten Fallunterscheidung nach `typeKind`.
- **Randfall bewusst nicht abgedeckt (kein Blocker, nur Transparenz):**
  Hat eine Partial-Deklaration eine Basisliste, die **nur** Interfaces
  auffuehrt (z. B. `partial class Foo : IDisposable`), waehrend eine
  andere Partial-Deklaration derselben Klasse eine Basisklasse angibt
  (z. B. `partial Foo : ComponentBase`), greift der Fallback nicht (weil
  `node.BaseList != null`) — es wird dann weiterhin nur `: IDisposable`
  angezeigt, ohne `ComponentBase`. Dieser Randfall tritt im gesamten
  Repo aktuell nirgends auf (siehe Blast-Radius-Pruefung oben) und ist
  nicht Teil des vom Kritiker gemeldeten Findings (das explizit den
  Fall „`node.BaseList == null`" beschreibt) — bewusst nicht mit
  abgedeckt, um den Fix-Step eng am gemeldeten Finding zu halten.
