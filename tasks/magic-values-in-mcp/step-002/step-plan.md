---
status: open
type: step-plan
task: magic-values-in-mcp
step: 002
corrects: step-001
title: "Korrektur step-001 — VisitInterpolatedStringExpression aktivieren"
epic: EPIC-1
estimated_risk: low
step_type: single
items: []
created_by: orchestrator
created_by_model: MiniMax-M3
created_by_model_knowledge_cutoff: 2026-01
related_to:
  - step-001/step-review.md
---

# Step 002: Korrektur step-001 — VisitInterpolatedStringExpression aktivieren

## Bezug

- **Task:** `magic-values-in-mcp`
- **Epic:** `EPIC-1` (vom korrigierten `step-001` übernommen — Roadmap wird in Fix-Modus nicht angefasst)
- **Konzept-Referenz:** `konzept.md` §„Muss-Haven" Beispiel 2 (In-String-Magic-Values & Interpolations-Fragmente) + §„Wie" Punkt 1 (SyntaxWalker für `LiteralExpressionSyntax` inkl. Raw String Literals & static text in `InterpolatedStringExpressionSyntax`).
- **related_to:** `step-001/step-review.md` (Finding 1, MAJOR, Konzept-Treue + Logik)

## Aktueller Projektzustand (JIT-Kontext)

`FindMagicValuesScanner.MagicValueSyntaxWalker.VisitInterpolatedStringExpression` (Zeile 334-343) ist ein No-op (`_ = node;`). Der Coder hat das in `step-001/step-result.md` §„Abweichungen vom Plan" als „dokumentierter EPIC-2-Hook" markiert — der Kritiker hat aufgedeckt, dass das Konzept die Verarbeitung statischer `InterpolatedStringText`-Segmente **explizit in EPIC-1 verlangt** (kein EPIC-2-Aufschub möglich). Konzept-Beispiel nennt verbatim `">80% des Limits"` in `HotspotMapBuilder.cs`/`GetHotspotsScanner.cs` als erwarteten Fund.

Trivial-/Attribut-/Index-/Loop-/`GetHashCode`-Filter müssen weiterhin greifen — bei der Synthese muss der Parent-Pfad künstlich auf das `InterpolatedStringExpressionSyntax` zeigen, damit `MagicValuesClassifier` konsistent entscheidet. Der Coder hat zwei Vorgehensweisen vorgeschlagen (synthetisches `LiteralExpressionSyntax` aus `InterpolatedStringTextSyntax` bauen, oder direkter String-Pfad in `MagicValuesClassifier.ClassifyString`); die Wahl ist Architektur-Ermessen auf Implementierungs-Ebene und kann vom Coder selbst getroffen werden.

## Intention

`VisitInterpolatedStringExpression` verarbeitet die statischen `InterpolatedStringTextSyntax`-Segmente in `$"..."`-Strings durch den `MagicValuesClassifier`. Der im Plan für `step-001` explizit verlangte Verifikationstest `ScanAsync_InterpolatedString_StaticTextSegmentsClassified` wird nachgezogen. Erweiterte Heuristiken (`nameof_candidates`, `enum_candidates`, `security_candidates`, duplizierte `private const`-Felder), Suppression-Granularität via `SyntaxTrivia` und `changedOnly` bleiben unverändert EPIC-2.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Mcp/Tools/MagicValues/FindMagicValuesScanner.cs` (Zeile 334-343)

- **Was:** `VisitInterpolatedStringExpression` implementiert die Verarbeitung der statischen Text-Segmente. Zwei gleichwertige Vorgehensweisen (Coder wählt):
  1. **Synthetisches `LiteralExpressionSyntax`:** `node.Contents` durchlaufen, für jedes `InterpolatedStringTextSyntax` ein `LiteralExpression` mit dem `TextToken.ValueText` synthetisieren und in `ProcessLiteral` einspeisen. `node.SyntaxTree` als künstlicher Parent-Pfad ist nicht nötig, weil die Heuristik in `MagicValuesClassifier.Classify` rein auf dem `LiteralExpressionSyntax.Parent`/`Ancestors` arbeitet; das interpolierte `InterpolatedStringExpressionSyntax` ist dann der Parent. **Wichtig:** `node.GetLocation()` durchreichen, damit `line`/`column` auf die echte Quellcode-Position zeigen.
  2. **Direkter Classifier-Aufruf:** `MagicValuesClassifier` um eine `ClassifyString(string text, Location location, ...)`-Überladung erweitern, die das gleiche Resultat liefert wie die `LiteralExpression`-Variante. Vorteil: keine Syntax-Synthese; Nachteil: zusätzliche API-Oberfläche.
- **Warum:** Konzept §„Muss-Haven" Beispiel 2 + §„Wie" Punkt 1 verlangen die Verarbeitung explizit in EPIC-1.
- **Hinweis:** Dynamische Segmente (`{x}`-Interpolationen) werden weiterhin übersprungen (Konzept §„Wie" implizit — Auflösung wäre semantisch fragwürdig). Die Trivial-/Attribut-/Index-/Loop-/`GetHashCode`-Filterung greift weiterhin, weil das `InterpolatedStringExpressionSyntax` selbst in einem Attribut oder `GetHashCode`-Body vorkommen kann; die Parent-Prüfung im Classifier bleibt wirksam.

### Datei 2: `src/AiNetLinter.FastTests/Mcp/Tools/FindMagicValuesScannerTests.cs` (oder neue Helper-Datei)

- **Was:** Neuen Test `ScanAsync_InterpolatedString_StaticTextSegmentsClassified` anlegen, der:
  - Eine Test-Solution mit einer Datei enthält, die einen interpolierten String mit statischem Text verwendet: z. B. `var msg = $"Schwelle {80} % des Limits";` oder das Konzept-Beispiel `private static readonly string WarnMsg = $">80% des Limits ({count})";`
  - `FindMagicValuesToolArgs` ohne `categoryFilter` und ohne `valueType`-Filter aufruft (Defaults).
  - Im Payload einen Eintrag mit `value = ">80% des Limits"` (oder dem tatsächlichen statischen Fragment), `category = constant_candidates` oder `config_candidates` erwartet.
- **Warum:** Der Plan für `step-001` (Datei 3 „Verarbeitung in EPIC-1 explizit verlangt") hat diesen Test vorgesehen, der Coder hat ihn nicht implementiert. Ohne Verifikationstest ist die Korrektur nicht abgesichert.
- **Hinweis:** Wenn die Implementierung statt synthetischem `LiteralExpressionSyntax` den direkten Classifier-Aufruf wählt, muss der Test ggf. den genauen String-Wert anpassen (Whitespace-Handling, exakte Fragment-Extraktion). Test dokumentiert dann den beobachteten Wert.

## Tests

- [ ] **`ScanAsync_InterpolatedString_StaticTextSegmentsClassified`:** statisches Text-Segment in `$"..."` wird gemeldet (Konzept-Beispiel `">80% des Limits"` als Mindest-Variante).

## Definition of Done

- [ ] `VisitInterpolatedStringExpression` verarbeitet `InterpolatedStringTextSyntax`-Segmente (Vorgehen siehe Datei 1).
- [ ] Neuer Verifikationstest in `src/AiNetLinter.FastTests/Mcp/Tools/` (oder Helper-Datei) ist grün.
- [ ] Bestehende Tests bleiben grün (insbesondere `ScanAsync_AllFilters_ReportsEverything` und die Filter-Tests, die mit interpolierten Strings nichts zu tun haben sollten).
- [ ] `dotnet build` grün — `TreatWarningsAsErrors=true`.
- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` grün.
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` grün.
- [ ] Code-Commit auf Branch `main` (kein Push): Conventional-Commit auf Deutsch, imperativ, Subject ≤ 72 Zeichen, Suffix `[magic-values-in-mcp]`, Trailer `Refs: tasks/magic-values-in-mcp/step-002`.
- [ ] `tasks/magic-values-in-mcp/step-002/step-result.md` geschrieben.
- [ ] `status` in `step-plan.md` (dieser Datei) von `open` auf `done (pending audit)` gesetzt.
- [ ] `task-state.md`-Steps-Tabelle aktualisiert: `step-001` von `in_progress` auf `done (Korrektur ausstehend)`, dann auf `done`; `step-002` auf `done (pending audit)`.

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc#5` (Qualitätsdrift-Prävention) — `TreatWarningsAsErrors=true`, keine ungenutzten Parameter, `sealed` für konkrete Klassen, sparsame Code-Kommentare, kein Task-Artefakt-Verweis (`step-002`) in Code-Kommentaren.
- `.agents/rules/AiNetLinterRichtlinien.mdc#1` (Grundprinzipien) — Immutability & Performance: `SyntaxTree`/`SemanticModel`-Zugriffe sparsam; die Synthese darf nicht zu Mehrfach-`GetRoot()`-Aufrufen führen, `SyntaxNode` ist unveränderlich, ein synthetisches `LiteralExpression` lässt sich mit `SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(valueText))` erzeugen.

## Bekannte Ausnahmen

- **Wahl des Vorgehens (synthetisches `LiteralExpression` vs. Classifier-Überladung):** Architektur-Ermessen auf Implementierungs-Ebene. Beide Wege sind konzepttreu; der Coder entscheidet anhand der bestehenden `MagicValuesClassifier`-API-Form (Records, statische Methoden, Param-Records). Kein Rückfragen-Bedarf beim Planer.
- **Dynamische Segmente (`{x}`):** werden weiterhin nicht ausgewertet. Konzept lässt das implizit offen; eine semantische Auflösung wäre teuer und fragwürdig. Keine Maßnahme nötig.
- **MINOR-Fund im Review (Z. 319, `MagicValueSyntaxWalker` als `class` statt `record`):** „kein Fix nötig, nur als Konsistenz-Bemerkung" — nicht im Scope dieses Korrektur-Steps.
- **Tech-Debt `TD-001` (Tool-Count-Drift über 3 Test-Dateien):** außerhalb des Scopes (`auto_fixable: nein`), wird nicht in diesem Korrektur-Step mitgenommen. Bleibt offen in `tech-debt.md`.

## Notes

- `SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(valueText, valueText))` baut ein gültiges syntaktisches `LiteralExpression`; `GetLocation()` muss manuell gesetzt werden, wenn die `line`/`column` exakt auf die Quellcode-Position des interpolierten Strings zeigen soll — sonst einfach den Original-`node.GetLocation()` des `InterpolatedStringExpressionSyntax` verwenden.
- `node.Contents` liefert `SyntaxNode?`-Instanzen, die per Pattern-Match auf `InterpolatedStringTextSyntax` gefiltert werden; jeder Treffer hat `.TextToken` mit `ValueText` (ohne Anführungszeichen) als property.
- Bei der Synthese-Variante darauf achten, dass `LiteralExpressionSyntax.Parent` während der Verarbeitung im Walker noch nicht gesetzt ist (Roslyn setzt das erst nach dem Besuch). `MagicValuesClassifier.Classify` muss robust gegen fehlenden Parent sein oder den Parent per `node.Parent` aus dem Original-Subtree beziehen.
- Reihenfolge der Commits: Code-Commit (Hook-Implementierung + Test) → Doku-Commit (Status + step-result + ggf. codemap-Update). Kein Push.
