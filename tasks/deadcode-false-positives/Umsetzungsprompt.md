# Umsetzungsprompt

Implementiere im Repository `C:\Daten\Entwicklung\Ralf\AiNetLinter` das
freigegebene Konzept `tasks/deadcode-false-positives/Konzept.md`
(`status: ready`) vollständig. Das Konzept ist die verbindliche fachliche
Entscheidung; treffe normale Implementierungsentscheidungen selbst.

Lies zuerst das Konzept, `Analyse.md`, die dort genannten Evidenzdateien und
die geltenden `AGENTS.md`-/`.agents/rules/`-Anweisungen. Frühere abweichende
Empfehlungen in `Entscheidungen-und-Tests.md` sind verworfen. Falls eine
benötigte Evidenzdatei nicht zugänglich ist, benenne genau diese Datei;
erfinde keine Befunde. Externe Repositories bleiben unverändert.

Ziel ist ein kleiner, begründeter MCP-Advisory mit Prüfkandidaten, keine
automatische Löschfreigabe. Erhalte insbesondere `test_only`, verwaiste
Wrapper, ganze Typen und ungelesene Member. Verhindere belegte Framework-,
Markup- und Reflection-Fehlalarme durch konkrete Bindungen statt pauschaler
Symbol-, Sichtbarkeits- oder Projektausschlüsse. Behandle konkrete
unaufgelöste Grenzen getrennt als `undecidable`. Produktnamen oder Pfade
dürfen weder Whitelist noch Analyseheuristik werden.

Arbeite pro False-Positive-Ursache mit einem zuerst nachgewiesenen roten,
isolierten xUnit-v3-Reproduktionstest und anschließender Korrektur. Verwende
vorhandene passende Tests; bereits korrektes Verhalten braucht keinen
künstlichen Rot-Nachweis. Ergänze positive Erkennungstests gemäß Abschnitt 10
des Konzepts. Erzeuge dafür kleine produktunabhängige C#-Beispiel-/Dummy-
Solutions beziehungsweise Roslyn-Fixtures mit der vorhandenen Infrastruktur.
Keine Planner-/SAN-Abhängigkeiten, externen Dienste oder Entwicklerpfade.
Frameworkbindung muss semantisch echt sein; gleichnamige Dummy-Attribute
ersetzen keine Framework-API. Unterdrückung und erhaltene positive Kandidaten
jeweils gemeinsam absichern. Keine Tests abschwächen, um Grün zu erreichen.

Setze auch Scan-Budgets, Referenzprüfung über die Solution, Erkennung bisheriger
Ziele entfernter Nutzungen, Gruppierung, Ausgabegrenzen und getrennte Scan-/
Ausgabevollständigkeit um. Unfertige Analyse ist kein Negativbefund. Keine
Vollständigkeit oder Performance behaupten, die nicht nachgewiesen wurde.

Synchronisiere verpflichtend die in Abschnitt 9 genannten Docs, Konfiguration,
Schema-/MCP-Beschreibungen und Agentenregeln mit dem implementierten Verhalten.
Erkläre pro Regel konkret, welches Signal sie auslöst, welchen False Positive
sie verhindert und welche positiven Kandidaten erhalten bleiben oder bewusst
nicht erfasst werden. „False Positives vermeiden“ allein ist keine Erklärung.

Nutze AiNetLinter-MCP proaktiv und primär für C#-Semantik mit dem absoluten
`targetPath: C:\Daten\Entwicklung\Ralf\AiNetLinter\AiNetLinter.slnx`.
Halte Testebenen und Gate-Reihenfolge aus den Projektregeln ein. Keine
Integration-/Stress-Läufe ohne ausdrücklichen Auftrag. Arbeite seriell,
ohne Subagenten. Erfasse den Git-Ausgangsstand, erhalte fremde Änderungen
und committe ausschließlich die auftragsbezogenen Änderungen mit deutschen
Conventional Commits. Kein Push.

Führe die Umsetzung bis zu den vollständigen Abschlusskriterien des Konzepts
und den bestandenen vorgeschriebenen Gates durch. Berichte am Ende knapp:
umgesetztes Verhalten, Rot-/Grün- und positive Nachweise, Laufzeit-/
Abdeckungsnachweis, verbleibende Grenzen und Commits.
