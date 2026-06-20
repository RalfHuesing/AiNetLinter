# AiNetLinter Feature Audit — Aufgaben-Checklist

**Gestartet:** 2026-06-20  
**Abgeschlossen:** —  
**Offene Items:** 53

> Lies zuerst `Prompt.md` vollständig, dann führe die Phasen **in Reihenfolge** aus.  
> Nach jedem abgeschlossenen Item: Checkbox auf `[x]` setzen und Fortschritt oben aktualisieren.

---

## Phase 1 — Paper-Bibliothek aufbauen

Alle sieben Cluster abarbeiten, **bevor** eine einzige Feature-Evaluation (Phase 2–4) startet. Papers werden nur einmal gesucht und dann in Phase 2–4 referenziert.

- [x] **papers-A** — Komplexitätsmetriken (McCabe, Halstead, Cognitive Complexity) → `temp\papers\papers-A-komplexitaet.md`
- [x] **papers-B** — Datei- und Methodengrößen, Fragmentierung, "Lost in the Middle" → `temp\papers\papers-B-groessen.md`
- [x] **papers-C** — LLM-Agenten & Code-Qualität 2023–2026 (SWE-bench, Anthropic, OpenAI, Microsoft) → `temp\papers\papers-C-llm-agenten.md`
- [x] **papers-D** — C#-Idiome, .NET Design Guidelines, Microsoft Docs → `temp\papers\papers-D-csharp.md`
- [ ] **papers-E** — Architekturmetriken (Kopplung, Kohäsion, DIT, CBO) → `temp\papers\papers-E-architektur.md`
- [ ] **papers-F** — Code Smells & Fehleranfälligkeit (Palomba, Yamashita, Fowler) → `temp\papers\papers-F-smells.md`
- [ ] **papers-G** — Test-Coverage & Testbarkeit → `temp\papers\papers-G-tests.md`
- [ ] **papers-H** — Meta-Hypothese: Verbessert AiNetLinter-Compliance die Agenten-Performance? → `temp\papers\papers-H-meta-hypothese.md`

---

## Phase 2 — Numerische Metriken evaluieren

- [ ] M01 — MaxLineCount → `Result\metrics\M01-MaxLineCount.md`
- [ ] M02 — MaxMethodLineCount → `Result\metrics\M02-MaxMethodLineCount.md`
- [ ] M03 — MaxMethodParameterCount → `Result\metrics\M03-MaxMethodParameterCount.md`
- [ ] M04 — MaxCyclomaticComplexity → `Result\metrics\M04-MaxCyclomaticComplexity.md`
- [ ] M05 — MaxCognitiveComplexity → `Result\metrics\M05-MaxCognitiveComplexity.md`
- [ ] M06 — MaxInheritanceDepth → `Result\metrics\M06-MaxInheritanceDepth.md`
- [ ] M07 — MaxMethodOverloads → `Result\metrics\M07-MaxMethodOverloads.md`
- [ ] M08 — MaxConstructorDependencies → `Result\metrics\M08-MaxConstructorDependencies.md`
- [ ] M09 — MaxDirectoryDepth → `Result\metrics\M09-MaxDirectoryDepth.md`
- [ ] M10 — MaxDirectoryChildren → `Result\metrics\M10-MaxDirectoryChildren.md`
- [ ] M11 — MaxBoolParameterCount → `Result\metrics\M11-MaxBoolParameterCount.md`
- [ ] M12 — MaxPartialClassFiles → `Result\metrics\M12-MaxPartialClassFiles.md`
- [ ] M13 — MaxPublicMembersPerType → `Result\metrics\M13-MaxPublicMembersPerType.md`
- [ ] M14 — MaxAIContextFootprint → `Result\metrics\M14-MaxAIContextFootprint.md`
- [ ] M15 — MaxSwitchArms → `Result\metrics\M15-MaxSwitchArms.md`
- [ ] M16 — MinCognitiveComplexityForTest → `Result\metrics\M16-MinCognitiveComplexityForTest.md`
- [ ] M17 — CompoundSuppressions (Mechanismus) → `Result\metrics\M17-CompoundSuppressions.md`

---

## Phase 3 — Boolean-Regeln evaluieren

- [ ] R01 — EnforceSealedClasses → `Result\bool-rules\R01-EnforceSealedClasses.md`
- [ ] R02 — AllowDynamic (Verbot) → `Result\bool-rules\R02-AllowDynamic.md`
- [ ] R03 — AllowOutParameters (Verbot) → `Result\bool-rules\R03-AllowOutParameters.md`
- [ ] R04 — AllowTryPatternOutParameters (Ausnahme) → `Result\bool-rules\R04-AllowTryPatternOutParameters.md`
- [ ] R05 — AllowCancellationShutdownCatch (Ausnahme) → `Result\bool-rules\R05-AllowCancellationShutdownCatch.md`
- [ ] R06 — AllowOutParametersInPrivateMethods (Ausnahme) → `Result\bool-rules\R06-AllowOutParametersInPrivateMethods.md`
- [ ] R07 — EnforceValueObjectContracts → `Result\bool-rules\R07-EnforceValueObjectContracts.md`
- [ ] R08 — EnableTestSentinel → `Result\bool-rules\R08-EnableTestSentinel.md`
- [ ] R09 — EnforcePascalCase → `Result\bool-rules\R09-EnforcePascalCase.md`
- [ ] R10 — EnforceXmlDocumentation (deaktiviert) → `Result\bool-rules\R10-EnforceXmlDocumentation.md`
- [ ] R11 — EnforceSemanticNaming → `Result\bool-rules\R11-EnforceSemanticNaming.md`
- [ ] R12 — EnforceNullableEnable → `Result\bool-rules\R12-EnforceNullableEnable.md`
- [ ] R13 — EnforceNoSilentCatch → `Result\bool-rules\R13-EnforceNoSilentCatch.md`
- [ ] R14 — EnforceMinimalApiAsParameters (deaktiviert) → `Result\bool-rules\R14-EnforceMinimalApiAsParameters.md`
- [ ] R15 — EnforceResultPatternOverExceptions (deaktiviert) → `Result\bool-rules\R15-EnforceResultPatternOverExceptions.md`
- [ ] R16 — EnforceExplicitStateImmutability (deaktiviert) → `Result\bool-rules\R16-EnforceExplicitStateImmutability.md`
- [ ] R17 — PreventContextDependentOverloads (deaktiviert) → `Result\bool-rules\R17-PreventContextDependentOverloads.md`
- [ ] R18 — EnforceNamespaceDirectoryMapping → `Result\bool-rules\R18-EnforceNamespaceDirectoryMapping.md`
- [ ] R19 — DetectAndBanPhantomDependencies → `Result\bool-rules\R19-DetectAndBanPhantomDependencies.md`
- [ ] R20 — BanPublicNestedTypes → `Result\bool-rules\R20-BanPublicNestedTypes.md`

---

## Phase 4 — System- und CLI-Features evaluieren

- [ ] F01 — Baseline / Ratchet-Mechanismus → `Result\features\F01-Baseline-Ratchet.md`
- [ ] F02 — Auto-Fix (`--fix` / `--dry-run`) → `Result\features\F02-AutoFix.md`
- [ ] F03 — Discovery-Commands (`--list-rules`, `--describe-rule`, `--docs`) → `Result\features\F03-Discovery.md`
- [ ] F04 — ProjectOverrides → `Result\features\F04-ProjectOverrides.md`
- [ ] F05 — PathOverrides → `Result\features\F05-PathOverrides.md`
- [ ] F06 — UiSeparation (Blazor / WPF) → `Result\features\F06-UiSeparation.md`
- [ ] F07 — FileFilters → `Result\features\F07-FileFilters.md`
- [ ] F08 — ForbiddenNamespaceDependencies → `Result\features\F08-ForbiddenNamespaceDependencies.md`
- [ ] F09 — EnablePerformanceProfiling → `Result\features\F09-EnablePerformanceProfiling.md`

---

## Phase 5 — Gesamtindex erstellen

- [ ] `Result\index.md` — Zusammenfassungsmatrix aller 46 Bewertungen + Top-Empfehlungen

---

## Phase 6 — Neue Feature-Vorschläge

Erst starten wenn Phase 5 abgeschlossen ist. Synthetisierende Recherche: Welche C#-Muster fehlen in AiNetLinter, sind aber empirisch belegt als Problem für LLM-Agenten?

- [ ] N00 — Gesamtrecherche + Proposals-Übersicht → `Result\new-features\proposals.md`
- [ ] Individuelle Vorschläge mit starker Evidenz (Agent entscheidet Anzahl, je Vorschlag eine Datei) → `Result\new-features\N[XX]-[Name].md`
