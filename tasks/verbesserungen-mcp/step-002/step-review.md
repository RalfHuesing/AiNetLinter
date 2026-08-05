---
status: done
type: step-review
task: verbesserungen-mcp
step: 002
epic: EPIC-01
step_type: single
reviewed_by: kritiker
reviewed_by_model: claude-sonnet-5
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-05T09:45:00Z
verdict: issues
tech_debt_ids: [TD-002, TD-003]
---

# Review Step 002: Roslyn-Paket-Versions-Bump: Razor-Source-Generator-Integration tatsaechlich zum Laufen bringen

## Verdict

- [ ] **approved** — alle vier Prüfebenen ok
- [x] **issues** — Fix-Step `step-002/fix-01` angelegt mit Fix-Plan
- [ ] **blocked** — Nutzer-Entscheidung nötig

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `<rules_dir>/**` (referenzierte Abschnitte) eingehalten
- [x] Logische Korrektheit: Code macht was er soll
- [ ] Konzept-Treue: ein Muss-Haben-Punkt aus `konzept.md` bleibt nur zur Hälfte erfüllt
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün (Volllauf reproduzierbar grün, siehe Tech-Debt TD-003 zu Sandbox-Flakiness)

## Befund

### Plan-Erfüllung

Alle im Plan unter „Konkrete Änderungen" genannten Punkte wurden umgesetzt:

- **Datei 1 (`AiNetLinter.csproj`):** Die drei `Microsoft.CodeAnalysis.*`-Pakete
  wurden exakt wie geplant von `5.3.0` auf `5.6.0` angehoben (verifiziert
  per `git show 7f4d6ba`). Der Plan verlangte vorab eine erneute Prüfung
  von `dotnet list package --outdated` — laut `step-result.md` durchgeführt,
  `5.6.0` bestätigt als weiterhin aktuellste Stable-Version. Erfüllt.
- **Datei 2 (`SourceFileCatalogBlazorPartialTests.cs`):** Alle drei Tests
  umbenannt und Assertions umgekehrt, Klassenkommentar aktualisiert.
  Test 1 und 2 exakt wie im Plan skizziert (inkl. der stärkeren
  `compilation.GetTypeByMetadataName(...).BaseType`-Assertion in Test 1
  und dem aus `GetIndexScopeToolTests` übernommenen Muster in Test 2).
  Test 3 weicht ab — siehe „Konzept-Treue" unten, dort auch die Bewertung
  dieser Abweichung. **Teilweise erfüllt** (2 von 3 Tests exakt wie
  geplant, der dritte mit einer für sich genommen sauber begründeten,
  aber Konzept-relevanten Abweichung).
- **Datei 3 (`BlazorPartialMiniFixtureWorkspace.cs`):** Wie erwartet keine
  Änderung nötig, im `step-result.md` explizit dokumentiert. Erfüllt.
- **Definition of Done (Checkliste):** `dotnet build` grün, `dotnet test`-
  Volllauf grün, `dotnet list package --outdated`-Blick dokumentiert,
  Commit vorhanden (`7f4d6ba`, Conventional-Commit-Format, Suffix
  `[verbesserungen-mcp]`), `step-result.md` geschrieben,
  `step-plan.md`-Status auf „done (pending audit)" gesetzt (verifiziert
  per `git show a14b3cd`) — alle erfüllt.

### Rules-Konformität

Gegen die im Plan unter „Rules-Refs" zitierten Abschnitte geprüft:

- **`AiNetLinterRichtlinien.mdc#4`** (xUnit v3 Pflicht, Commit-Vorschlag):
  eingehalten — Test-Umkehrung ist die geforderte Logik-Test-Anpassung,
  Commit-Message ist ein vollständiger deutscher Conventional Commit mit
  Refs-Zeile.
- **`AiNetLinterRichtlinien.mdc#5`** (Zero-Warning-Direktive,
  Symptom-Fixing verboten): eingehalten — `dotnet build` liefert 0
  Warnungen (selbst nachgeprüft). Das Streichen der Basistyp-Assertion in
  Test 3 ist **kein** Symptom-Fixing im Sinne dieser Regel: Symptom-Fixing
  bedeutet, eine Assertion abzuschwächen, um denselben, im Scope liegenden
  Bug zu verdecken. Hier liegt die Ursache nachweislich in einer anderen,
  von diesem Step nicht berührten Komponente (`SkeletonSyntaxWalker.cs`) —
  der Coder hat das transparent unter „Abweichungen vom Plan" dokumentiert,
  statt es stillschweigend zu ändern. Prozessual korrekt.
- **`AiNetLinterRichtlinien.mdc#3`** (`TestResults/latest.trx` bei
  Fehlern/langem Output): nicht angewendet, da der Coder keine
  Testfehler hatte, die diese Diagnose erfordert hätten.
- **`AiNetLinter.mdc`** (Grenzwerte): Testklasse bleibt bei 3 Tests, 69
  Zeilen — unproblematisch.

### Logische Korrektheit

- Test 1 (`LoadAsync_BlazorPartialFixture_ResolvesComponentBaseWithoutCompileErrors`)
  ist eine aussagekräftige, starke Assertion: sie verifiziert sowohl das
  Fehlen der Diagnostics als auch direkt am `INamedTypeSymbol.BaseType`,
  dass `ComponentBase` semantisch aufgelöst wird — selbst nachvollzogen
  und grün.
- Test 2 folgt exakt dem etablierten Muster aus `GetIndexScopeToolTests`
  — konsistent, grün.
- Test 3 wurde nach der Umbenennung inhaltlich reduziert (nur noch
  Abwesenheit von „Compile-Fehler"/„CS0115"), aber diese reduzierte
  Assertion ist selbst korrekt und wahrheitsgemäß — kein Vorspiegeln
  falscher Tatsachen. Die Einordnung, ob diese Reduktion akzeptabel ist,
  gehört zu Ebene 4 (siehe unten), nicht zu dieser Ebene.
- Eigene Verifikation der Kern-Behauptung des Coders (dass
  `get_file_skeleton` den Basistyp architektonisch nicht zeigen kann):
  `SkeletonSyntaxWalker.BuildTypeInfo` (Zeile 113) liest
  `node.BaseList` rein syntaktisch vom übergebenen `TypeDeclarationSyntax`
  **dieser einen Datei**. `GetFileSkeletonTool.ExecuteAsync`
  (`GetFileSkeletonTool.cs:35`) ruft `SkeletonMapBuilder.
  ExtractFromDocumentAsync` strikt pro einzelnem `Document` auf. Die
  Fixture-Datei `tests/Fixtures/BlazorPartialMini/src/BlazorPartialMini/
  SiteView.razor.cs` deklariert `public partial class SiteView` **ohne
  jede Basisliste** — der Basistyp `ComponentBase` steht ausschließlich in
  der vom Razor-Generator erzeugten, separaten Partial-Deklaration (ein
  anderer Syntaxbaum). Damit ist zweifelsfrei bestätigt: kein
  Paket-Versions-Bump kann an dieser Struktur etwas ändern, ohne
  `SkeletonSyntaxWalker.cs` selbst um eine semantische Basistyp-Auflösung
  zu erweitern. Die Coder-Behauptung ist korrekt und war keine
  ungeprüfte Übernahme.

### Konzept-Treue (Ebene 4)

**Das ist der zentrale Befund dieses Reviews.** `Konzept.md` „Definition
of Done / Erfolgskriterien" listet als Schnell-Check-Punkt 2 explizit:
„`get_file_skeleton(SiteView.razor.cs)` → kein `CS0115`, Basisklasse
`ComponentBase` sichtbar" — zwei Teilaussagen, verbunden mit „,", beide
Teil desselben Muss-Haben-Kriteriums aus dem Scope „P1 — Blazor-Partials"
(„volle Integration, kein Workaround").

`step-002/step-plan.md` selbst zitiert unter „Konzept-Referenz" exakt
diesen Punkt 2 als einen der beiden DoD-Schnell-Check-Punkte, die dieser
Step abdecken soll („... `get_file_skeleton(SiteView.razor.cs)` kein
`CS0115`, Basisklasse `ComponentBase` sichtbar"). Der Plan hat sich damit
selbst committet, diesen Punkt **vollständig** zu erfüllen — nicht nur
zur Hälfte. Die Code-Skizze im Plan (`Assert.Contains(": ComponentBase",
text, ...)`) bestätigt das nochmal explizit als erwartetes Ergebnis.

Tatsächlich erreicht wurde nur die erste Hälfte: kein `CS0115` mehr (Test
1 und 2 belegen das solide, auch auf Compilation-Ebene). Die zweite
Hälfte — „Basisklasse `ComponentBase` sichtbar" **in `get_file_skeleton`**
— bleibt unerfüllt. Ich habe das oben unter „Logische Korrektheit"
unabhängig verifiziert: keine Trickserei des Coders, sondern eine reale,
strukturelle Grenze von `SkeletonSyntaxWalker` (rein syntaktische,
datei-lokale Basistyp-Extraktion), die nicht durch den in diesem Step
korrekt umgesetzten Paket-Bump behoben wird und laut Plan auch nicht
behoben werden sollte (`SkeletonSyntaxWalker.cs` steht nicht unter
„Konkrete Änderungen").

**Einordnung Finding vs. Tech-Debt (wie im Auftrag verlangt, mit
Begründung):** Die technische Ursache liegt unbestreitbar außerhalb des
von diesem Step geänderten Codes (`SkeletonSyntaxWalker.cs` statt
`AiNetLinter.csproj`/Testklasse) und ist eine allgemeine, von Blazor
unabhängige Eigenschaft der Datei-Skeleton-Extraktion (jede über mehrere
Dateien gesplittete `partial class` mit Basisliste nur in einer der
Dateien ist betroffen) — für sich genommen sähe das nach einem klassischen
Tech-Debt-Kandidaten aus („Architektur-Lücke außerhalb des Step-Scopes").
**Entscheidend ist aber, dass dieser Step laut seinem eigenen Plan genau
diesen DoD-Punkt vollständig abdecken sollte** — die SKILL.md-Abgrenzung
stellt explizit klar, dass ein fehlendes Muss-Haben-Kriterium tendenziell
immer ein Finding ist, auch wenn die technische Ursache in einer anderen
Komponente liegt, die dieser Step nicht ändern sollte. Genau dieser Fall
liegt hier vor: der Plan hat sich (in Unkenntnis der tatsächlichen
Roslyn-API-Struktur von `SkeletonSyntaxWalker`) zu einem DoD-Punkt
committet, den der gewählte, korrekte Lösungsansatz (reiner
Paket-Bump) strukturell nicht liefern kann. Das ist ein Plan-Fehler, kein
Umsetzungsfehler des Coders — ändert aber nichts daran, dass am Ende
dieses Steps ein im Plan selbst zugesagter Konzept-Punkt nur zur Hälfte
steht. Ich werte das daher als **MAJOR-Finding auf Ebene 4**, nicht als
Tech-Debt, und **nicht** als Verschulden des Coders (der die Abweichung
vorbildlich transparent dokumentiert und nicht künstlich kaschiert hat).

Kein Verstoß gegen ein explizites Non-Goal (Konzept.md hat keine), kein
Scope-Creep in die andere Richtung — der übrige Scope des Steps
(Paket-Bump, Test 1+2) ist exakt wie geplant.

### Build-/Test-Status

```
dotnet build AiNetLinter.slnx                                          → grün (0 Warnungen, 0 Fehler)
dotnet test --filter FullyQualifiedName~SourceFileCatalogBlazorPartialTests → grün (3 Tests, 0 Fehler)
dotnet test AiNetLinter.slnx (Volllauf, 3 Versuche)                     → grün (1257 Tests, 0 Fehler) bei 1 von 3 Versuchen;
                                                                            2 von 3 Versuchen: Testhostprozess-Absturz ohne
                                                                            Einzeltestfehler (siehe TD-003 — reproduziert
                                                                            identisch auf dem Vorgänger-Commit vor dem
                                                                            Paket-Bump, also nicht durch diesen Step verursacht)
```

## Findings

1. `tasks/verbesserungen-mcp/Konzept.md` (Definition of Done, Punkt 2) ×
   `src/AiNetLinter/Maps/Skeleton/SkeletonSyntaxWalker.cs:113`
   (`BuildTypeInfo`) × `src/AiNetLinter.Tests/Baseline/
   SourceFileCatalogBlazorPartialTests.cs:55-68` (Test 3) — **[MAJOR]
   [Konzept-Treue]** Der von `step-002/step-plan.md` selbst zitierte
   DoD-Schnell-Check-Punkt 2 („`get_file_skeleton(SiteView.razor.cs)` →
   kein `CS0115`, Basisklasse `ComponentBase` sichtbar") ist nur zur
   Hälfte erfüllt: `CS0115` ist weg, die Basisklasse wird in
   `get_file_skeleton` weiterhin nicht angezeigt, weil
   `SkeletonSyntaxWalker.BuildTypeInfo` den Basistyp ausschließlich
   syntaktisch aus `node.BaseList` **der jeweils einzelnen Datei** liest
   und damit die im separaten, generierten Razor-Partial deklarierte
   Basisklasse nie sieht — unabhängig vom in diesem Step korrekt
   umgesetzten Roslyn-Paket-Bump. **Soll-Zustand:** Entweder (a)
   `SkeletonSyntaxWalker.BuildTypeInfo` um einen semantischen Fallback
   ergänzen (wenn `node.BaseList == null`, aber `typeSymbol?.BaseType`
   ungleich `object`/`null` ist, diesen Basistyp trotzdem anzeigen,
   z. B. mit einem Hinweis „aus anderer Partial-Deklaration") — das würde
   den DoD-Punkt vollständig erfüllen; oder (b) der Nutzer entscheidet
   bewusst, den DoD-Punkt in `Konzept.md` nachträglich zu präzisieren
   (z. B. „Basisklasse in `get_index_scope`/`Compilation` sichtbar,
   `get_file_skeleton` bleibt dateilokal") und diese Lücke stattdessen
   explizit als akzeptierten Nice-to-Have/Tech-Debt zu dokumentieren.
   Beide Wege sind für mich als Kritiker plausibel — welcher gewählt wird,
   ist Nutzer-/Planer-Entscheidung, kein technischer Zwang.

## Tech-Debt-Einträge aus diesem Review

- `TD-002` (siehe `tech-debt.md`) — `GetIndexScopeScanner.FormatBreakdown`
  zeigt „1 Dateien" statt „1 Datei" bei genau einer Datei je Dateityp
  (Produktionscode, nicht Teil des step-002-Diffs).
- `TD-003` (siehe `tech-debt.md`) — Voller `dotnet test`-Lauf stürzt in
  dieser Sandbox intermittierend mit Testhost-Absturz ab, reproduziert
  identisch vor und nach dem step-002-Paket-Bump — Umgebungs-/
  Nebenläufigkeitsproblem der Testsuite, nicht durch diesen Step
  verursacht.
