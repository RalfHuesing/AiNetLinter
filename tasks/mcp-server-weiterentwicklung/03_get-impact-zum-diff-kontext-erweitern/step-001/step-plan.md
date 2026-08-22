---
status: open
type: step-plan
task: 03_get-impact-zum-diff-kontext-erweitern
step: 001               # flach, Task-weite Sequenz — auch Korrekturen liegen hier, nie in einem Unterordner
corrects: null             # <null | step-NNN> — nur gesetzt, wenn dieser Step eine Korrektur ist (treibt das Kettenbudget, siehe ../spec.md §10.5/§10.6)
title: "Traversierungs-Korrektur (EnqueueChildren) & Sufficiency-Hint-Parität"
epic: EPIC-1          # Bezug zum Epic in roadmap.md, dem dieser Step zuarbeitet (bei corrects: vom korrigierten Step übernommen)
estimated_risk: medium  # Einschätzung des Planers, siehe skills/planer/SKILL.md
step_type: single  # single (Default) | batch — siehe ../spec.md §10.6. Bei batch: items-Liste unten füllen.
items: []  # nur bei step_type: batch. Ein Eintrag pro gebündeltem Mini-Befund innerhalb des Epics (oder pro opportunistisch angehängtem auto_fixable-Tech-Debt, siehe ../spec.md §9.1/§10.6):
# items:
#   - id: item-01
#     title: "<Kurztitel des Befunds>"
#     source: "<Quelle, z. B. konzept.md-Referenz oder tech-debt.md#TD-NNN>"
created_by: planer  # planer | orchestrator (nur bei mechanischem Korrektur-Transkript ohne Ermessen, siehe ../spec.md §6.2.1)
created_by_model: stealth/ox-alpha
created_by_model_knowledge_cutoff: unbekannt
created_at: 2026-08-22
related_to: []  # Pointer auf andere step-NNN (Task-interne Abhängigkeiten) oder auf step-review.md (Fix-Modus) — nie Fakten cachen, nur verweisen. Siehe ../spec.md §10.6. Nicht zu verwechseln mit `corrects` oben (eigene, budget-relevante Semantik).
---

# Step 001: Traversierungs-Korrektur (EnqueueChildren) & Sufficiency-Hint-Parität

## Bezug

- **Task:** `03_get-impact-zum-diff-kontext-erweitern`
- **Epic:** `EPIC-1` aus `roadmap.md` — Traversierungs-Korrektur &
  Hint-Parität im Symbolgraph; Epic ist noch vollständig offen (kein
  vorheriger Step), dieser Step deckt es komplett ab.
- **Konzept-Referenz:** Muss-Have „Traversierungs-Korrektur in
  `CallGraphTraversal.ExpandAsync`" und „Sufficiency-Hint Parität"
  (`Konzept.md` §Scope Must-have); Audit A.1 (EnqueueChildren-Defekt),
  A.3 (Hint-Lücke), B (Bestandsverhalten ändert sich — Depth2-Tests
  bewusst reviewen, Symptom-Fixing-Verbot), F (dokumentierte Entscheidung
  je Depth2-Test); Testforderung „Echte Methoden-Aufruferkette … depth=2
  in find_references und get_impact" (`Konzept.md` §Tests).

## Aktueller Projektzustand (JIT-Kontext)

Gegen den echten Codestand verifiziert (MCP-Tools `find_references`,
Datei-Lesezugriffe; Git-Stand `main`, ahead 1, keine Step-Commits):

1. **Defekt exakt bestätigt** (`CallGraphTraversal.cs:126-134`):
   `EnqueueChildren` enqueued `reference.Definition`. Für
   `SymbolFinder.FindReferencesAsync(current)` ist `Definition` meist
   `current` selbst — steht bereits in `_seen`
   (`SymbolEqualityComparer.Default`), wird also nie enqueued.
   `depth > 1` expandiert heute faktisch nur über Override-/Interface-
   Definitionen, nicht über Aufruferketten.
2. **Wiederverwendbares Pattern existiert bereits im selben File:** Der
   Tree-Pfad (`BuildTreeAsync`) löst in `AddLocationToGroupAsync`
   (`CallGraphTraversal.cs:354-382`) pro `ReferenceLocation` bereits
   `SemanticModel.GetEnclosingSymbol(...).NormalizeToOwningMember()` auf —
   inklusive Null-Behandlung für Top-Level-Statements („nicht weiter
   auflösbares Blatt"). Der Fix soll dieses Pattern nutzen (gemeinsamer
   Helper oder bewusst begründete Duplikation, siehe Notes) statt neue
   Logik zu erfinden.
3. **Async-Implikation:** `AppendReferenceLocations`/`EnqueueChildren`
   sind aktuell sync; die Enclosing-Symbol-Auflösung braucht
   `Document.GetSemanticModelAsync(ct)` → die private Traversal-Kette
   wird in diesem Zweig async. Roslyn-Mehrkosten bleibt durch den
   bestehenden Node-Cap (`MaxRecursionNodes = 200`) gedeckelt.
4. **Konsumenten von `ExpandAsync` verifiziert** (per `find_references`
   auf beide Overloads): genau `FindReferencesTool.cs:59`,
   `GetImpactTool.cs:55` (Symbol-Branch) und Tests — Audit B stimmt; der
   Fix ändert sichtbar das `depth>1`-Verhalten beider Tools (intendiert).
5. **Hint-Paritäts-Lücke exakt bestätigt:**
   `FindReferencesTool.ExecuteAsync` (Z.67-69) hängt bei
   `TransitiveCallGraphFormatter.IsComplete(traversal)`
   `McpSufficiencyHints.Append(body)` an;
   `GetImpactTool.ExecuteSymbolBranchAsync` (Z.47-65) tut das nicht.
   `IsComplete` = `!TruncatedByMaxResults && !TruncatedByNodeLimit &&
   !DepthWasClamped` (`TransitiveCallGraphFormatter.cs:10-16`).
   `McpSufficiencyHints.Append` wird von 15 Tool-Stellen genutzt —
   `GetImpactTool` ist der Ausreißer unter den Traversal-Tools.
6. **Bestands-Depth2-Tests sind schwach, kodifizieren den Defekt aber
   nicht in Assertions:** `ExpandAsync_Depth2_FormatsWithDepthMarker`
   und `ExpandAsync_DepthAboveCap_ClampsToThree`
   (`CallGraphTraversalTests.cs:37-62`) asserten nur „Caller.cs" im Text
   — sie bleiben auch nach dem Fix grün, beweisen ihn aber nicht.
   `ExpandAsync_NodeLimit_ReportsNodeTruncationSeparately` (Z.65-79)
   prüft Cap-Mechanik, die vom Fix unberührt bleibt.
   `GetImpactToolTests.ExecuteAsync_SymbolIdentifierWithDepth2_
   StillReturnsCallSite` assertet `effectiveDepth == 2` + nicht-leere
   callSites (überlebt den Fix).
7. **Fixture-Lücke:** Die Default-Fixture (`Greeter.Greet` ←
   `Run`/`RunTwice`/`RunThrice`; niemand ruft diese) hat KEINE mehrstufige
   Kette. Für Ketten-Tests existiert bereits
   `McpInMemoryTestContext.CreateScenario(ProjectSpec)` (genutzt in
   `BuildTreeAsync_Both_TopNShowsBothDirectionsBeforeOverflow`,
   Z.214 ff.) — Ad-hoc-Szenario inline, keine neue Fixture-Infrastruktur.
8. **Struktur trägt die Kette bereits:** `TransitiveCallSiteEntry` hat
   `Depth` und `ReachedFromSymbolId`;
   `GetStableSymbolId` (DocCommentId + Fallback) existiert — für den
   Ketten-Nachweis ist keine Modell-/Record-Änderung nötig.

Einfluss auf den Plan: Fix als Austausch dessen, was enqueued wird
(Enclosing-Caller statt Definition), Wiederverwendung des
Tree-Pfad-Patterns, Tests über bestehende Szenario-Infrastruktur,
keine Änderung an Ergebnis-Records. `GetImpactInput` bleibt unberührt
(EPIC-6), `Docs/agent-api.md` bleibt außen vor (EPIC-7 weist die
Verhaltenskorrektur dort aus).

## Intention

Nach diesem Step liefern `find_references` und der `get_impact`-Symbol-
Branch bei `depth > 1` echte mehrstufige Aufruferketten (`A -> B -> C`)
mit korrekter `Depth`/`ReachedFromSymbolId` statt faktisch leerem
Override-only-Expandieren. Die Bestands-Depth2-Tests sind je Test bewusst
reviewed mit dokumentierter Entscheidung (bestätigt ODER als Kodifikation
des Defekts umgestellt — kein mechanisches Grünzwingen). Beide Tools
hängen bei vollständigen Ergebnissen konsistent den Sufficiency-Hint an.
Risiko `medium`: beabsichtigtes Bestandsverhaltensänderung zweier Tools +
async-Umbau privater Helfer, aber ohne Vertragsschnitt, ohne Record-
Änderungen, in-memory testbar.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Mcp/Tools/SymbolGraph/CallGraphTraversal.cs` (EnqueueChildren Z.126-134, private Helfer Z.61-134)

- **Was:** BFS-Kindknoten nicht mehr als `reference.Definition` enqueuen,
  sondern pro Referenzlocation das tatsächliche einschließende
  Aufrufer-Symbol: `Document.GetSemanticModelAsync(ct)` →
  `semanticModel.GetEnclosingSymbol(location.SourceSpan.Start, ct)
  .NormalizeToOwningMember()`. Locations ohne auflösbares Enclosing-
  Symbol (Top-Level-Statements) erscheinen weiterhin als Call-Site der
  aktuellen Ebene, werden aber nicht expandiert. Die private Helferkette
  (`TraverseAsync` → `AppendReferenceLocations`/`EnqueueChildren`) wird
  dafür async; `_seen`-Cycle-Schutz über `SymbolEqualityComparer.Default`
  bleibt bestehen, `MarkSeenAndEnqueue` benennt künftig korrekt das
  Enqueued-Symbol. Enclosing-Auflösung als Helper neben dem identischen
  Pattern aus `AddLocationToGroupAsync` (Tree-Pfad) stellen — gemeinsame
  Konsolidierung bevorzugen (DRY), siehe Notes.
- **Warum:** Konzept Muss-Have + Audit A.1 — sonst liefert `depth > 1`
  keine echten Aufruferketten.

### Datei 2: `src/AiNetLinter/Mcp/Tools/SymbolGraph/GetImpactTool.cs` (`ExecuteSymbolBranchAsync` Z.47-65)

- **Was:** Exakte Parität zu `FindReferencesTool.cs:67-69`: nach der
  Formatierung (inkl. Empty-Result-Ersatztext)
  `var finalBody = TransitiveCallGraphFormatter.IsComplete(traversal)
  ? McpSufficiencyHints.Append(body) : body;` und `finalBody` an
  `FindSymbolTool.PrependWarning` reichen. Gleiche Bedingung, gleiche
  Reihenfolge (Hint auch beim leeren, aber vollständigen Ergebnis — wie
  in `find_references`). Keine weiteren Änderungen an dieser Datei
  (Git-Branch, `GetImpactInput`, `CancellationToken.None`-Fund D.7 → EPIC-6).
- **Warum:** Konzept Muss-Have „Sufficiency-Hint Parität" + Audit A.3.

### Datei 3: `src/AiNetLinter.FastTests/Mcp/Tools/CallTree/CallGraphTraversalTests.cs`

- **Was:** Audit-F-Pflicht — dokumentierte Entscheidung je Bestands-Test:
  - `ExpandAsync_Depth1_FormatsCallSiteFromCaller`: unverändert korrekt
    (Ebene 1 war nie betroffen) → bestätigen.
  - `ExpandAsync_Depth2_FormatsWithDepthMarker`: schwache Assertion
    (nur „Caller.cs") — kodifiziert den Defekt nicht, beweist den Fix
    aber auch nicht → stärken: auf der Default-Fixture ruft niemand
    Run/RunTwice/RunThrice → explizit asserten, dass KEINE Depth-2-
    Einträge entstehen (echter Kettenabschluss); die eigentliche
    Kettenabdeckung kommt aus dem neuen Ketten-Test unten.
  - `ExpandAsync_DepthAboveCap_ClampsToThree`: Clamp-Mechanik unabhängig
    vom Enqueue-Fix → bestätigen.
  - `ExpandAsync_NodeLimit_ReportsNodeTruncationSeparately`:
    Node-Cap bricht vor dem Besuch enqueuer Kinder — Mechanik unverändert
    → bestätigen und nach dem Fix erneut verifizieren.
  - Neu: `ExpandAsync_Depth2_RealCallerChain_ResolvesBothLevels` —
    via `McpInMemoryTestContext.CreateScenario(ProjectSpec)` Szenario
    `MethodA ← MethodB ← MethodC` (B ruft A, C ruft B); Asserts gegen
    `result.CallSites`: Ebene-1-Eintrag in MethodB (`Depth = 1`,
    `ReachedFromSymbolId` = stabile ID von MethodA), Ebene-2-Eintrag in
    MethodC (`Depth = 2`, `ReachedFromSymbolId` = stabile ID von MethodB).
- **Warum:** Audit B/F — Bestandstests bewusst reviewen statt mechanisch
  grün zwingen; Konzept-Testforderung „echte Methoden-Aufruferkette".

### Datei 4: `src/AiNetLinter.FastTests/Mcp/Tools/SymbolGraph/FindReferencesToolTests.cs`

- **Was:** Neu: Tool-Level-Test `ExecuteAsync_Depth2_RealCallerChain_
  ReturnsBothLevels` (gleiches Ketten-Szenario wie Datei 3) —
  `find_references` mit `depth=2` liefert Aufrufstellen auf Ebene 1 UND 2
  (structuredContent `callSites` inkl. `depth`/`reachedFromSymbolId`).
- **Warum:** Konzept-Testforderung nennt ausdrücklich `find_references`.

### Datei 5: `src/AiNetLinter.FastTests/Mcp/Tools/SymbolGraph/GetImpactToolTests.cs`

- **Was:** Zwei Ergänzungen:
  1. `ExecuteAsync_SymbolIdentifier_Depth2RealCallerChain_ReturnsBothLevels`
     — get_impact-Symbol-Branch auf dem Ketten-Szenario, Ebene 1+2 im
     structuredContent mit korrekter `Depth`/`ReachedFromSymbolId`.
  2. Hint-Parität: vollständig (Default-Fixture, `maxResults` hoch,
     `depth=1`) → Text enthält `[HINWEIS]: Diese Daten sind vollstaendig`;
     trunkiert (bestehendes Muster:
     `ExecuteAsync_SymbolIdentifierWithManyCallSites_...` nutzt
     `maxResults=2` bei 6 Call-Sites) → KEIN Hinweis, stattdessen
     Trunkierungs-Meta-Zeile.
- **Warum:** Hint-Parität absichern (Audit A.3) und get_impact als
  zweiten Konsumenten des Fixes abdecken.

## Tests

- [ ] `ExpandAsync_Depth2_RealCallerChain_ResolvesBothLevels` (neu, Datei 3) — Ebene 1 in B, Ebene 2 in C, korrekte `Depth`/`ReachedFromSymbolId`
- [ ] `ExecuteAsync_Depth2_RealCallerChain_ReturnsBothLevels` (neu, Datei 4) — find_references-Parität
- [ ] `ExecuteAsync_SymbolIdentifier_Depth2RealCallerChain_ReturnsBothLevels` (neu, Datei 5) — get_impact-Parität
- [ ] `ExecuteAsync_SymbolIdentifierCompleteResult_AppendsSufficiencyHint` (neu, Datei 5)
- [ ] `ExecuteAsync_SymbolIdentifierTruncatedResult_OmitsSufficiencyHint` (neu, Datei 5)
- [ ] Bestands-Tests `ExpandAsync_Depth1_*`, `ExpandAsync_Depth2_FormatsWithDepthMarker`, `ExpandAsync_DepthAboveCap_ClampsToThree`, `ExpandAsync_NodeLimit_*` grün — mit dokumentierter Review-Entscheidung je Test (in Testkommentar und `step-result.md`)
- [ ] Alle übrigen bestehenden FastTests (Unit/Component) bleiben grün — insbesondere `GetImpactToolTests.*` und die BuildTree*-Suite (Tree-Pfad unverändert)

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt
- [ ] Build-Command aus Tech-Stack-Notiz (`roadmap.md`) grün: `dotnet build` (fehler- und warnungsfrei, `TreatWarningsAsErrors=true`)
- [ ] Test-Command aus Tech-Stack-Notiz grün — Abschluss-Gate: `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` UND `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`; Schnelliteration über `Category=Unit` / `Category=Component`
- [ ] Dokumentierte Review-Entscheidung je Bestands-Depth2-Test liegt vor (Audit F)
- [ ] Commit auf aktuellem Branch (Conventional Commit)
- [ ] `step-001/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc` §5 Qualitätsdrift-Prävention — Symptom-Fixing-Verbot (Depth2-Tests bewusst umstellen/bestätigen, nicht abschwächen), Zero-Warning, DRY (Enclosing-Helper konsolidieren statt duplizieren)
- `.agents/rules/AiNetLinterRichtlinien.mdc` §4 Updates & Tests — xUnit-v3-Pflicht für jede Logik-Änderung; keine zwangsserialisierende Collection
- `.agents/rules/AiNetLinterRichtlinien.mdc` §5 Kommentar-Disziplin — Why-Kommentare am Fix ohne Task-/Step-/EPIC-ID-Referenzen
- `.agents/rules/AiNetLinter.mdc` Kurz-Stil/Grenzwerte — `sealed`, Methoden ≤60 Zeilen (async-Helfer klein halten, ggf. extrahieren), CC ≤12

## Bekannte Ausnahmen

- Keine bekannten flaky Tests im Berührbereich. Beobachtungspunkt: nach
  dem Fix kann `ExpandAsync_Depth2_FormatsWithDepthMarker` (falls NICHT
  gestärkt) weiterhin grün bleiben — genau deshalb wird er umgestellt;
  ein stiller Revert dieser Stärkung wäre Symptom-Fixing.

## Code-Skizze (optional)

```
// Richtung der Änderung in EnqueueChildren (Details beim Coder):
private static async Task EnqueueChildrenAsync(
    IEnumerable<ReferencedSymbol> refs, int currentLevel, TraversalState state, CancellationToken ct)
{
    if (currentLevel >= state.Depth) return;
    foreach (var reference in refs)
        foreach (var referenceLocation in reference.Locations)
        {
            var caller = await ResolveEnclosingMemberAsync(referenceLocation.Document,
                referenceLocation.Location.SourceSpan.Start, ct); // Pattern: AddLocationToGroupAsync
            if (caller is not null)
                state.MarkSeenAndEnqueue(caller, currentLevel + 1);
        }
}
```

## Notes

- **Bewusste Verhaltensentscheidung (Audit B):** Overrides/Interface-
  Implementierungen werden nicht mehr als eigene BFS-Knoten verfolgt —
  ihre Aufrufstellen erscheinen weiterhin als Einträge der aktuellen
  Ebene (`SymbolName` bleibt der referenzierten `Definition`). Der
  Ausweis als Verhaltenskorrektur in `Docs/agent-api.md` ist EPIC-7,
  gehört hier NICHT in den Diff.
- **DRY:** Das Enclosing-Pattern steht bereits in
  `AddLocationToGroupAsync` (Tree-Pfad). Erste Wahl: kleiner gemeinsamer
  Helper (flacher BFS + Tree-Pfad). Falls eine Konsolidierung die
  Tree-Gruppierungslogik verbiegen würde: Duplikation bewusst begründen
  (DuplicateCode-Begründung), nicht erzwingen.
- **Wiederverwendung statt Neubau:** Ketten-Szenarien über
  `McpInMemoryTestContext.CreateScenario(ProjectSpec)` (existierendes
  Muster); Sufficiency-Hint über bestehendes `McpSufficiencyHints.Append`
  + `TransitiveCallGraphFormatter.IsComplete` — nichts Neues erfinden.
- **Roslyn-Kosten:** Pro besuchtem Knoten weiterhin genau ein
  `FindReferencesAsync`; zusätzlich pro Location ein SemanticModel-Zugriff
  (vom Workspace gecacht) — durch `MaxRecursionNodes` (200) gedeckelt.
- **Determinismus:** Sortierung/Dedup in `TraversalState.CreateResult`
  unverändert lassen; `Locations.Distinct()` greift weiter (Records).
- **Nicht in diesem Step:** `GetImpactInput`-Wachstum, `detailLevel`,
  Caps (EPIC-6); `Docs/agent-api.md`/README (EPIC-7);
  `CancellationToken.None` in `ExecuteGitRefBranchAsync` (Audit D.7, EPIC-6);
  Git-Branch insgesamt unberührt.
- **Anti-Loop-Check:** CodeMap enthält keine frühere, widersprechende
  Entscheidung (alle Einträge „(zuletzt: roadmap)"); der Fix richtet sich
  nach Konzept Muss-Have, nicht dagegen.
