# Orchestrator-Taskvertrag

```text
Task:

Task scope:

Execution mode: autonomous
Release status: ready

Explicit exclusions:

Task completion condition:

Current slice:

Objective:

Context documents:

Allowed paths:

Acceptance criteria:
-

Required checks:
-

Dependencies:

Continue condition:

Slice stop condition:

Task-level stop condition:

Autonomous continuation rule: Continue with the next incomplete slice until
the task completion condition and all release gates are satisfied. Do not
return an intermediate completion result after a single slice.

Slice result: pass | findings | blocked
Task result: complete | blocked | in_progress
```

Der Vertrag bleibt knapp. Fachliche Details gehören in die zuständigen
Projekt- und Fachdokumente, nicht in einen künstlichen Workflow-Status.
