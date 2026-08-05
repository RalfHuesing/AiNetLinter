---
status: done (pending audit)
type: step-plan
task: mcp-call-logging-fuer-agenten-analyse
step: 005
title: "Tech-Debt-Aufräumaktion: TD-001, TD-002, TD-003 in einem Aufwasch"
epic: none
estimated_risk: medium
step_type: batch
items:
  - id: item-01
    title: "TD-001: tech-debt.md Status auf 'erledigt' setzen (Inhalt in step-004 item-04 bereits gefixt)"
    source: "tech-debt.md TD-001"
  - id: item-02
    title: "TD-002: MetricsConfig durch Extract-Helper-Klasse und separate Records schlanker machen (Option 1)"
    source: "tech-debt.md TD-002"
  - id: item-03
    title: "TD-002-Bonus: 5 PathOverrides in rules.json auf Originalwerte zurueckrollen (post-Refactor)"
    source: "tech-debt.md TD-002 + Item-02-Ergebnis"
  - id: item-04
    title: "TD-003: Docs/ROADMAP.md:482 Test-Count von 5 auf 9 angleichen"
    source: "tech-debt.md TD-003"
  - id: item-05
    title: "step-005/step-result.md erstellen mit Verifikations-Output pro Commit"
    source: "Drift-Loop-Workflow"
  - id: item-06
    title: "Finaler dotnet build / dotnet test / dotnet run --path . Verifikations-Lauf"
    source: "Drift-Loop-Workflow Verifikations-Output"
created_by: planer
created_by_model: MiniMax-M3
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-05T15:50:00+02:00
related_to:
  - "tech-debt.md#TD-001"
  - "tech-debt.md#TD-002"
  - "tech-debt.md#TD-003"
  - "step-004/step-plan.md"  # step-004 item-04 = Inhalt von TD-001
  - "step-004/fix-01/step-review.md"  # Beobachtung TD-003 durch fix-01-Reviewer
---
