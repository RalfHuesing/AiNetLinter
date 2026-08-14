---
status: done
type: step-review
task: magic-values-in-mcp
step: 001
epic: EPIC-1
step_type: single
reviewed_by: kritiker
reviewed_by_model: MiniMax-M3
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-14T21:55:00+02:00
verdict: issues
tech_debt_ids: [TD-001]
---

# Review Step 001: find_magic_values — Tool-Core, Basis-Klassifizierung & Doku-Sync (EPIC-1)

## Verdict

- [ ] **approved** — alle vier Prüfebenen ok
- [x] **issues** — Korrektur-Step `step-002` angelegt (`corrects: step-001`)
- [ ] **blocked** — Nutzer-Entscheidung nötig (siehe Frage unten)

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `.agents/rules/**` eingehalten
- [ ] Logische Korrektheit: Code macht was er soll, nicht nur „grün" — **MAJOR-Fund**
- [ ] Konzept-Treue: passt die Umsetzung zu `konzept.md` (Scope, Non-Goals, Muss-Haven) — **MAJOR-Fund**
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün (oder Baselines beachtet)

## Befund

### Plan-Erfüllung

Alle 11 im Plan genannten Dateien umgesetzt (Datei 1-9 Produktion+Doku, 10-11 Tests) inkl. der explizit vom Plan geforderten Aufteilung wegen Lint-Limits und der Bonus-Datei `OverviewResourceRegistration.cs` (Konsistenz mit `OverviewResourceRegistrationTests`). Status-Update `done (pending audit)` korrekt gesetzt. Abweichungen im `step-result.md` transparent dokumentiert.

### Rules-Konformität

`TreatWarningsAsErrors` Build grün, `#nullable enable` in allen neuen Dateien, `internal sealed` an Klassen und Records, kein `step-001`-Verweis in Code-Kommentaren (`grep` ohne Match), `MaxMethodParameterCount` durch `MagicValueClassifierOptions`/`MagicValueWalkerContext`-Records entlastet. Keine Rules-Verletzung im Prod-Code.

### Logische Korrektheit

URL-Heuristik (`http://`/`https://`/`ftp://`), Windows-Pfad-Heuristik (`C:\` + UNC), Format-String-Heuristik, Connection-String-Keywords, Trivial-Filter (`0`/`1`/`-1`, `""`/`" "`/`"\n"`, `true`/`false`/`null`), Attribut-Isolierung, `GetHashCode`-Sonderfall und Index/Loop-Ausnahme sind semantisch sauber. `MagicValueSyntaxWalker.VisitInterpolatedStringExpression` ist jedoch **kein „dokumentierter EPIC-2-Hook"**, sondern ein **echter No-op** (Zeile 342: `_ = node;`) — die statischen `InterpolatedStringText`-Segmente werden in EPIC-1 **nicht** klassifiziert.

### Konzept-Treue (Ebene 4)

Konzept `konzept.md` §„Muss-Haven" nennt **drei** exemplarische Anwendungsfälle; **Beispiel 2** ist „In-String-Magic-Values & Interpolations-Fragmente (`$"..."`)" und verweist explizit auf `private const double WarnThreshold = 0.80;` in `HotspotMapBuilder.cs:23`/`GetHotspotsScanner.cs:27` plus das inline hartcodierte `">80% des Limits"` in diesen Dateien. Konzept §„Wie" Punkt 1 verlangt zusätzlich verbatim: *„`SyntaxWalker` durchläuft alle C#-Syntaxbäume der Solution für `LiteralExpressionSyntax` (inkl. Raw String Literals & **static text in `InterpolatedStringExpressionSyntax`**)."* — `find_magic_values` verfehlt damit ein zentrales Muss-Haven-Beispiel. Der Coder beruft sich auf eine Auslegung („interpolierte Strings semantisch fragwürdig"), die das Konzept nicht hergibt; der Plan selbst nennt die Verarbeitung als Muss (`FindMagicValuesScanner.cs` Datei-3-Beschreibung). **Konzept-Kern teilweise verfehlt.**

### Build-/Test-Status

```
dotnet build → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress → grün (1303 Tests, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → grün (310 Tests, 0 Fehler)
```

## Findings (nur bei `issues` — Abschnitt sonst weglassen)

1. `src/AiNetLinter/Mcp/Tools/MagicValues/FindMagicValuesScanner.cs:334-343` — [MAJOR] [Konzept-Treue] `VisitInterpolatedStringExpression` ist ein No-op-Hook (`_ = node;`). Konzept §„Muss-Haven" Beispiel 2 + §„Wie" Punkt 1 verlangen die Klassifikation der statischen `InterpolatedStringText`-Segmente in `$"..."`-Strings (genau das use-case im Konzept: `">80% des Limits"` in `HotspotMapBuilder.cs`/`GetHotspotsScanner.cs`). **Fix:** In `VisitInterpolatedStringExpression` die `node.Contents` durchlaufen, für jedes `InterpolatedStringTextSyntax` einen synthetischen `LiteralExpressionSyntax` bauen (alternativ den String-Text direkt in `MagicValuesClassifier.ClassifyString` einspeisen) und durch den Classifier schicken. Trivial-/Attribut-/Index-/Loop-Filterung muss weiterhin greifen — bei der Synthese den Parent-Pfad künstlich auf das `InterpolatedStringExpressionSyntax` setzen, damit `IsLoopInitializer`/`IsInsideGetHashCode` konsistent entscheiden. Verifikationstest: `ScanAsync_InterpolatedString_StaticTextSegmentsClassified` (vom Plan explizit verlangt, fehlt) nachziehen — Test-Source mit `$"Schwelle {80} %"` o. ä., Erwartung: `config_candidates`/`constant_candidates`-Fund.

2. `src/AiNetLinter/Mcp/Tools/MagicValues/FindMagicValuesScanner.cs:319` — [MINOR] [Logik] `internal sealed class MagicValueSyntaxWalker` ist als `class` (nicht `sealed record`/`readonly struct`) deklariert; alle anderen neuen Klassen in dieser Datei-Klasse sind `internal static` oder `internal sealed record`. Konsistenz: `sealed class` reicht, aber `internal sealed class` mit fehlender `sealed`-Annotation wäre sonst Lint-Verstoß. Hier bereits `sealed` — kein Fix nötig, nur als Konsistenz-Bemerkung.

## Sonstige Beobachtungen / MINOR / NITPICK

- `MagicValuesClassifier.NotMagic()` (Z. 119-120) liefert als Dummy-Kategorie `MagicValueCategory.ConfigCandidates` für `IsMagic=false`-Fälle. Semantisch unscharf (sieht aus wie „war Config", obwohl „kein Magic" gemeint) — Aufrufer in `FindMagicValuesScanner.ProcessLiteral` (Z. 356) liest `classification.IsMagic` und filtert, also harmlos. Erwähnenswert nur, falls EPIC-2 mal `IsMagic=false`-Fälle separat reporten will.
- `FindMagicValuesToolArgs` hat 9 Felder — `MaxMethodParameterCount: 4` greift nicht (Record-Felder zählen nicht als Methoden-Parameter), sauber.

## Tech-Debt-Einträge aus diesem Review

- `TD-001` (siehe `tech-debt.md`) — Tool-Count-Test-Drift: nach jedem Tool-Add müssen drei Test-Dateien (`OverviewResourceRegistrationTests`, `McpDocumentationSmokeTests`, `McpServerCommandContractTests`) manuell mit-aktualisiert werden; zentrale Konstante fehlt.
