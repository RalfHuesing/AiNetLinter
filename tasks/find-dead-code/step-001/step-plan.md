---
status: done
type: step-plan
task: find-dead-code
step: 001
corrects: null
title: "Core-Scanner, Datenmodelle & Scope-Bounding-Pipeline"
epic: EPIC-01
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: gemini-2.5-pro
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-17T17:17:45+02:00
related_to: []
---

# Step 001: Core-Scanner, Datenmodelle & Scope-Bounding-Pipeline

## Bezug

- **Task:** `find-dead-code`
- **Epic:** `EPIC-01` aus `roadmap.md` — Core-Scanner & Scope-Bounding-Pipeline implementieren
- **Konzept-Referenz:** `konzept.md` §3.1, §3.2, §3.4, §3.5, §Wie

## Aktueller Projektzustand (JIT-Kontext)

- Analyse-orientierte MCP-Tools liegen unter `src/AiNetLinter/Mcp/Tools/Analysis/` (z. B. `GetViolationsScanner`, `SearchPatternScanner`, `ViolationScopeFilter`) und `src/AiNetLinter/Mcp/Tools/MagicValues/`.
- `ViolationScopeFilter.MatchesScope` und `PathNormalizer.ToRelative` existieren und können für Scope-Prüfungen wiederverwendet werden.
- `SymbolFinder.FindReferencesAsync` wird in `DiffImpactAnalyzer` und `FindReferencesTool` genutzt. `SymbolFinder` unterstützt document-scoped Überladungen (`IImmutableSet<Document>`).
- Bisher existiert kein Dead-Code-Scanner.

## Intention

Erstellung der grundlegenden Datenmodelle (`DeadCodeModels.cs`), der Whitelist- und Heuristik-Prüfungen (`DeadCodeWhitelist.cs`), sowie des Kern-Scanners (`FindDeadCodeScanner.cs`) mit Document-Scoped Search für `private` Symbole, Top-Down-Container-Pruning, Interface- und Override-Kaskadierung und `limitsApplies`-Klassifikation. Erste Unit-Tests in `AiNetLinter.FastTests` verifizieren die Funktionalität.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Mcp/Tools/DeadCode/DeadCodeModels.cs` (neu)

- **Was:** Definiert `DeadCodeEntry`, `DeadCodeSummary`, `DeadCodeScanResult`, `DeadCodeLimits`, `FindDeadCodeArgs` sowie Enums für `accessibility`, `confidence`, `kind`, `mode`.
- **Warum:** Typsichere Datenstrukturen für Scan-Parameter und Structured Output.

### Datei 2: `src/AiNetLinter/Mcp/Tools/DeadCode/DeadCodeWhitelist.cs` (neu)

- **Was:** Enthält Regeln zum Ausschluss von Symbolen:
  - `IsImplicitlyDeclared` (Compiler-Generiertes, Record-Equality, Auto-Props Backing Fields)
  - `MethodKind.StaticConstructor`, `Destructor`, `PropertyGet`/`Set`, `EventAdd`/`Remove`, Operatoren
  - Entry-Points (`compilation.GetEntryPoint(ct)`, `Program`, `Main`)
  - Utility-Konstruktoren: private parameterlose Konstruktoren in Klassen, deren sonstige Member statisch sind
  - Attribute: `[ModuleInitializer]`, `[DllImport]`, `[Fact]`, `[Theory]`, `[Test]`, `[McpServerTool]`, `[McpTool]`, `[Inject]`, `[Parameter]`, etc.
- **Warum:** Verhindert False-Positives bei Framework-, Compiler- und Runtime-gebundenen Symbolen.

### Datei 3: `src/AiNetLinter/Mcp/Tools/DeadCode/FindDeadCodeScanner.cs` (neu)

- **Was:** Implementiert den Scan-Algorithmus:
  - Dokumenten-Auswahl (unter Beachtung von `includeTests` und `scopeFilter`).
  - Iteration über deklarierte Typen und Member.
  - Document-Scoped `SymbolFinder.FindReferencesAsync` für `private` Symbole ($O(\text{doc})$).
  - Workspace-weiter Scan für `internal`/`public` Symbole nach Token-Pre-Check.
  - Interface- und Override-Kaskadierung: Implementierungen von Interface-Methoden oder Base-Overrides gelten als lebendig, wenn das Interface- oder Basis-Symbol Referenzen hat.
  - Confidence-Zuweisung (`high` vs `low`) und `limitsApplies`-Zuweisung.
- **Warum:** Performante und semantisch korrekte Dead-Code-Erkennung.

### Datei 4: `src/AiNetLinter.FastTests/Mcp/FindDeadCodeScannerTests.cs` (neu)

- **Was:** Unit-Tests gegen In-Memory Adhoc-Workspaces:
  - Private ungenutzte Methode / Klasse -> `high` Dead Code.
  - Private genutzte Methode -> kein Dead Code.
  - Interface-Implementierung mit Interface-Aufruf -> kein Dead Code.
  - Private Utility-Konstruktor -> kein Dead Code (Whitelist).
  - Top-Down Container Pruning: ungenutzte private Klasse markiert Klasse.
- **Warum:** Verifikation der Kernlogik und False-Positive-Prävention.

## Tests

- [ ] `FindDeadCodeScannerTests.ScanAsync_PrivateUnusedMethod_ReturnsHighConfidenceDeadCode`
- [ ] `FindDeadCodeScannerTests.ScanAsync_PrivateUsedMethod_ReturnsNoDeadCode`
- [ ] `FindDeadCodeScannerTests.ScanAsync_InterfaceImplementation_WithInterfaceCall_NotDeadCode`
- [ ] `FindDeadCodeScannerTests.ScanAsync_UtilityPrivateConstructor_IsWhitelisted`
- [ ] `FindDeadCodeScannerTests.ScanAsync_FilterByAccessibility_WorksCorrectly`

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt
- [ ] Build-Command aus Tech-Stack-Notiz (`roadmap.md`) grün (`dotnet build`)
- [ ] Test-Command aus Tech-Stack-Notiz grün (`dotnet test src/AiNetLinter.FastTests --filter Category!=Stress && dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`)
- [ ] Commit auf aktuellem Branch (Conventional Commit `feat(deadcode): Core-Scanner & Scope-Bounding-Pipeline implementieren [find-dead-code]`)
- [ ] `tasks/find-dead-code/step-001/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc` — Sealed Classes, Methoden ≤60 Zeilen, `#nullable enable`, Immutability.
- `.agents/rules/AiNetLinterRichtlinien.mdc` — Monolithisch, kein DI/ALC, TreatWarningsAsErrors.

## Notes

- Bei `private`-Symbolen immer `SymbolFinder.FindReferencesAsync(symbol, solution, documents: ImmutableHashSet.Create(document))` nutzen, um teure Workspace-Scans zu vermeiden.
- Bei `ISymbol.ExplicitOrImplicitInterfaceImplementations` und `IsOverride` muss das Interface- bzw. Basissymbol geprüft werden.
