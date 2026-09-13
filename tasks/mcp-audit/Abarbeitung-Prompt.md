Wir haben in tasks\mcp-audit Findings.

Das wurde von einem Agenten mit verschiedenen Quellen ermittelt (slnx, dll, exe).

Wir gehen jedes Finding durch.
Du prüfst inhaltlich und aus agentischer Sicht ob das Finding wirklich ein Problem ist.
Du validierst die relevanten Code stellen.
Interaktiv prüfen wir ob wir etwas Anpassen.
Du gibst erklärst mir kurz was der Agent bei dem Finding für ein Problem hat.
Und gibst eine klare Empfehlung ab ob, was und wie wir etwas umsetzen.
Es kann auch sein das wir das nicht umsetzen weil es gegen Prinzipien verstößt.
Schätze ein wie komplex die jeweilige Anforderung ist und insbesondere ob es einen Architekturbruch bedeuten würde - was grundsätzlich kein Problem ist, ich muss es aber vorher wissen und besser abschätzen können.

Wenn ich mein Okay gebe startest du einen Subagenten der das Finding umsetzt.
Idealerweise mit einem initialen Rot-Test.

Du und auch die SubAgenten beachten .agents\rules\*.mdc.

Wenn Finding erledigt oder wir uns dagegen entschieden haben LÖSCHEN wir das aus tasks\mcp-audit\*.md damit wir nur wirlich offene Punkte haben.

Integrationstests nur machen wenn essenziell notwendig (hintergrund: deren laufzeit ist sehr lang, ohne mehrwert ist das zeitverschwendung).
build + Verify + FastTests immer machen.
Commit wenn Grün.

Danach bearbeiten wir das nächste Finding.