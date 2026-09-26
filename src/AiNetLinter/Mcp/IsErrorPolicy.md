# isError-Policy fuer AiNetLinter MCP-Tools

MCP-Antworten enthalten genau einen nichtleeren Text-Content-Block. Ein Client kann
am Protokollflag und am kopierbaren Content-Status unterscheiden:

| Zustand | isError | Content | Naechster Schritt |
|---|:---:|---|---|
| Semantisches Ergebnis, auch eine definitive leere Treffermenge | false | Ergebnistext; gegebenenfalls `operation=ok` oder `completeness=empty` | Ergebnis verwenden |
| Solution laedt noch | false | `Status: operation=retry, completeness=not_applicable` und `[INFO]` ohne Trefferinhalt | Kurz warten und denselben Aufruf wiederholen |
| Langer Verify-, Advisory-, Duplikat-, Pattern- oder Assembly-Lauf der vier dedizierten Assembly-Tools | false | `Status: operation=running, completeness=not_applicable` und `operationToken` | Dasselbe Tool mit identischen Argumenten und Token erneut aufrufen; das Ergebnis folgt in einem späteren kurzen Aufruf |
| Fehlgeschlagener Aufruf, auch bei korrigierbarer Eingabe | true | `[ERROR]` mit Code; zielgebunden zusaetzlich `operation=error` | Hint oder Recovery ausfuehren |

`SYMBOL_NOT_FOUND`, `AMBIGUOUS_SYMBOL`, `INVALID_ARGUMENT`,
`RESOURCE_NOT_FOUND`, ungueltige Konfiguration oder `gitRef`,
`SOLUTION_NOT_LOADED`, Sicherheitsverweigerungen und interne
`WORKSPACE_DIAGNOSTIC`-/`ANALYSIS_FAILED`-Fehler sind fehlgeschlagene
Aufrufe. Dass ein Client die Eingabe korrigieren kann, aendert das Protokollflag
nicht. Die Hinweise im Content nennen den passenden Folgeschritt; eine definitive
leere Treffermenge bleibt hingegen ein erfolgreiches Ergebnis.

`McpToolResults.Error(...)` und `McpToolResults.Recoverable(...)` liefern beide
`isError=true`. `Recoverable` bezeichnet den vorhandenen Korrekturhinweis,
nicht den Protokollstatus. `McpToolResults.Loading()` liefert `isError=false`
mit explizitem Retry-Status. Alle Antworten bleiben content-only; ein
`structuredContent`-Vertrag besteht nicht.

`operation=running` ist ein laufender Hintergrundauftrag, keine fachliche Antwort.
Jeder Aufruf wartet höchstens 15 Sekunden; nach 30 Minuten ohne Abruf wird ein
laufender Auftrag abgebrochen. Ein fertiges Ergebnis bleibt 30 Minuten nach dem
letzten Abruf verfügbar. Das Ergebnis selbst bleibt content-only und unverändert.
