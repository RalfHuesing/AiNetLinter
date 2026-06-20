# AiNetLinter Feature Audit — Aufgaben-Checklist

**Gestartet:** 2026-06-20  
**Abgeschlossen:** —  
**Offene Items:** 3

> Lies zuerst `Prompt.md` vollständig, dann führe die Phasen **in Reihenfolge** aus.  
> Nach jedem abgeschlossenen Item: Checkbox auf `[x]` setzen und Fortschritt oben aktualisieren.

---

## Phase 1 — Paper-Bibliothek aufbauen

Alle sieben Cluster abarbeiten, **bevor** eine einzige Feature-Evaluation (Phase 2–4) startet. Papers werden nur einmal gesucht und dann in Phase 2–4 referenziert.

- [x] **papers-A** — Komplexitätsmetriken (McCabe, Halstead, Cognitive Complexity) → `temp\papers\papers-A-komplexitaet.md`
- [x] **papers-B** — Datei- und Methodengrößen, Fragmentierung, "Lost in the Middle" → `temp\papers\papers-B-groessen.md`
- [x] **papers-C** — LLM-Agenten & Code-Qualität 2023–2026 (SWE-bench, Anthropic, OpenAI, Microsoft) → `temp\papers\papers-C-llm-agenten.md`
- [x] **papers-D** — C#-Idiome, .NET Design Guidelines, Microsoft Docs → `temp\papers\papers-D-csharp.md`
- [x] **papers-E** — Architekturmetriken (Kopplung, Kohäsion, DIT, CBO) → `temp\papers\papers-E-architektur.md`
- [x] **papers-F** — Code Smells & Fehleranfälligkeit (Palomba, Yamashita, Fowler) → `temp\papers\papers-F-smells.md`
- [x] **papers-G** — Test-Coverage & Testbarkeit → `temp\papers\papers-G-tests.md`
- [x] **papers-H** — Meta-Hypothese: Verbessert AiNetLinter-Compliance die Agenten-Performance? → `temp\papers\papers-H-meta-hypothese.md`

---

## Phase 2 — Numerische Metriken evaluieren

- [x] M01 — MaxLineCount → `Result\metrics\M01-MaxLineCount.md`
- [x] M02 — MaxMethodLineCount → `Result\metrics\M02-MaxMethodLineCount.md`
- [x] M03 — MaxMethodParameterCount → `Result\metrics\M03-MaxMethodParameterCount.md`
- [x] M04 — MaxCyclomaticComplexity → `Result\metrics\M04-MaxCyclomaticComplexity.md`
- [x] M05 — MaxCognitiveComplexity → `Result\metrics\M05-MaxCognitiveComplexity.md`
- [x] M06 — MaxInheritanceDepth → `Result\metrics\M06-MaxInheritanceDepth.md`
- [x] M07 — MaxMethodOverloads → `Result\metrics\M07-MaxMethodOverloads.md`
- [x] M08 — MaxConstructorDependencies → `Result\metrics\M08-MaxConstructorDependencies.md`
- [x] M09 — MaxDirectoryDepth → `Result\metrics\M09-MaxDirectoryDepth.md`
- [x] M10 — MaxDirectoryChildren → `Result\metrics\M10-MaxDirectoryChildren.md`
- [x] M11 — MaxBoolParameterCount → `Result\metrics\M11-MaxBoolParameterCount.md`
- [x] M12 — MaxPartialClassFiles → `Result\metrics\M12-MaxPartialClassFiles.md`
- [x] M13 — MaxPublicMembersPerType → `Result\metrics\M13-MaxPublicMembersPerType.md`
- [x] M14 — MaxAIContextFootprint → `Result\metrics\M14-MaxAIContextFootprint.md`
- [x] M15 — MaxSwitchArms → `Result\metrics\M15-MaxSwitchArms.md`
- [x] M16 — MinCognitiveComplexityForTest → `Result\metrics\M16-MinCognitiveComplexityForTest.md`
- [x] M17 — CompoundSuppressions (Mechanismus) → `Result\metrics\M17-CompoundSuppressions.md`

---

## Phase 3 — Boolean-Regeln evaluieren

- [x] R01 — EnforceSealedClasses → `Result\bool-rules\R01-EnforceSealedClasses.md`
- [x] R02 — AllowDynamic (Verbot) → `Result\bool-rules\R02-AllowDynamic.md`
- [x] R03 — AllowOutParameters (Verbot) → `Result\bool-rules\R03-AllowOutParameters.md`
- [x] R04 — AllowTryPatternOutParameters (Ausnahme) → `Result\bool-rules\R04-AllowTryPatternOutParameters.md`
- [x] R05 — AllowCancellationShutdownCatch (Ausnahme) → `Result\bool-rules\R05-AllowCancellationShutdownCatch.md`
- [x] R06 — AllowOutParametersInPrivateMethods (Ausnahme) → `Result\bool-rules\R06-AllowOutParametersInPrivateMethods.md`
- [x] R07 — EnforceValueObjectContracts → `Result\bool-rules\R07-EnforceValueObjectContracts.md`
- [x] R08 — EnableTestSentinel → `Result\bool-rules\R08-EnableTestSentinel.md`
- [x] R09 — EnforcePascalCase → `Result\bool-rules\R09-EnforcePascalCase.md`
- [x] R10 — EnforceXmlDocumentation (deaktiviert) → `Result\bool-rules\R10-EnforceXmlDocumentation.md`
- [x] R11 — EnforceSemanticNaming → `Result\bool-rules\R11-EnforceSemanticNaming.md`
- [x] R12 — EnforceNullableEnable → `Result\bool-rules\R12-EnforceNullableEnable.md`
- [x] R13 — EnforceNoSilentCatch → `Result\bool-rules\R13-EnforceNoSilentCatch.md`
- [x] R14 — EnforceMinimalApiAsParameters (deaktiviert) → `Result\bool-rules\R14-EnforceMinimalApiAsParameters.md`
- [x] R15 — EnforceResultPatternOverExceptions (deaktiviert) → `Result\bool-rules\R15-EnforceResultPatternOverExceptions.md`
- [x] R16 — EnforceExplicitStateImmutability (deaktiviert) → `Result\bool-rules\R16-EnforceExplicitStateImmutability.md`
- [x] R17 — PreventContextDependentOverloads (deaktiviert) → `Result\bool-rules\R17-PreventContextDependentOverloads.md`
- [x] R18 — EnforceNamespaceDirectoryMapping → `Result\bool-rules\R18-EnforceNamespaceDirectoryMapping.md`
- [x] R19 — DetectAndBanPhantomDependencies → `Result\bool-rules\R19-DetectAndBanPhantomDependencies.md`
- [x] R20 — BanPublicNestedTypes → `Result\bool-rules\R20-BanPublicNestedTypes.md`

---

## Phase 4 — System- und CLI-Features evaluieren

- [x] F01 — Baseline / Ratchet-Mechanismus → `Result\features\F01-Baseline-Ratchet.md`
- [x] F02 — Auto-Fix (`--fix` / `--dry-run`) → `Result\features\F02-AutoFix.md`
- [x] F03 — Discovery-Commands (`--list-rules`, `--describe-rule`, `--docs`) → `Result\features\F03-Discovery.md`
- [x] F04 — ProjectOverrides → `Result\features\F04-ProjectOverrides.md`
- [x] F05 — PathOverrides → `Result\features\F05-PathOverrides.md`
- [x] F06 — UiSeparation (Blazor / WPF) → `Result\features\F06-UiSeparation.md`
- [x] F07 — FileFilters → `Result\features\F07-FileFilters.md`
- [x] F08 — ForbiddenNamespaceDependencies → `Result\features\F08-ForbiddenNamespaceDependencies.md`
- [x] F09 — EnablePerformanceProfiling → `Result\features\F09-EnablePerformanceProfiling.md`

---

## Phase 5 — Gesamtindex erstellen

- [ ] `Result\index.md` — Zusammenfassungsmatrix aller 46 Bewertungen + Top-Empfehlungen

---

## Phase 6 — Neue Feature-Vorschläge

Erst starten wenn Phase 5 abgeschlossen ist. Synthetisierende Recherche: Welche C#-Muster fehlen in AiNetLinter, sind aber empirisch belegt als Problem für LLM-Agenten?

- [ ] N00 — Gesamtrecherche + Proposals-Übersicht → `Result\new-features\proposals.md`
- [ ] Individuelle Vorschläge mit starker Evidenz (Agent entscheidet Anzahl, je Vorschlag eine Datei) → `Result\new-features\N[XX]-[Name].md`
