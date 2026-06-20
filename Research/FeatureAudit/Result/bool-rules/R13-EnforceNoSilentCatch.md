# EnforceNoSilentCatch (R13)

**Kategorie:** Boolean-Regel  
**Aktueller Wert:** true | **Status:** Aktiv  
**Severity:** error  
**Paper-Cluster genutzt:** D, F, C

---

## Bewertung

🟢 **WERTVOLL**

**Fazit:** Breit anerkanntes Antipattern mit solider empirischer Grundlage — leere Catch-Blöcke unterdrücken Fehlersignale, verschlechtern Systembeobachtbarkeit und erzeugen für LLM-Agenten tote Winkel in der Fehlerdiagnose; die Pflicht zur expliziten Behandlung (Log + Throw oder Rethrow) ist Best Practice in der gesamten C#-Community.

---

## Empfehlung

**Aktion:** Aktiviert lassen  
**Begründung:** Das Silent-Catch-Antipattern ist in Längsschnittstudien als persistent und schädlich belegt (Casalnuovo et al. 2019); für LLM-Agenten verschlimmert sich das Problem, da stille Exceptions dem Agenten im Edit-Loop Feedback vorenthalten — der Agent kann sein Vorgehen nicht korrigieren.

---

## Wissenschaftliche / Empirische Grundlage

**Casalnuovo et al. (2019 — Journal of the Brazilian Computer Society)** untersuchten in einer Längsschnittstudie Exception-Handling-Antipatterns in einem großen kommerziellen Java-Projekt über mehrere Release-Zyklen. Kernbefund: Leere Catch-Blöcke und Catch-Exception-Muster nehmen ohne aktive Gegenmaßnahmen (Richtlinien, Tools) über die Projektlaufzeit zu — sie sind persistent und selbstverstärkend. Entwickler-Awareness und explizite Richtlinien reduzierten die Antipattern-Prävalenz messbar.

**Harness.io / Demiri (2022)** schätzt, dass ca. 20 % der Fehler nie in Logs erscheinen, wenn Exceptions "verschluckt" werden. Diese Zahl ist nicht peer-reviewed, aber der qualitative Befund ist konsistent: Stille Exceptions verhindern Monitoring, Debugging und Incident-Response.

**Fowler & Beck (1999/2018, "Refactoring")** nennen leere Catch-Blöcke als klassisches Antipattern, da sie Code-Pfade einführen die bewusst ignorieren, was passiert ist — dies verletzt das Prinzip der minimalen Überraschung.

**Casalnuovo et al. (2019)** zeigen zudem: Das Pattern entsteht oft aus Zeitdruck — Entwickler "schließen" einen Compiler-Fehler mit einem leeren Catch, statt die Ursache zu beheben. AiNetLiners `error`-Severity stellt sicher, dass dieses Pattern nicht unbeabsichtigt im Commit landet.

## KI-Agenten-Perspektive

Für LLM-Agenten im Edit-Loop (z.B. Claude Code, Cursor) ist das Silent-Catch-Antipattern besonders schädlich: Wenn eine Exception still schluckt wird, erhält der Agent im Tool-Feedback-Zyklus kein Fehler-Signal. Er "sieht" einen grünen Build, obwohl eine Fehlerbehandlungspfad nie ausgeführt oder getestet wurde.

Aus Cluster C ("Inside the Scaffold: Agent Failure Taxonomy", arXiv:2604.03515): **Fehlende Fehlertoleranz im Feedback-Loop** ist die zweithäufigste Agenten-Fehlerquelle. Silent Catches verstärken dieses Problem systematisch: Der Agent kann sich nach einem stillen Fehler nicht selbst korrigieren, weil er gar nicht weiß, dass ein Fehler aufgetreten ist.

Zudem erzeugen LLM-Agenten beim Generieren von Fehlerbehandlungscode häufig Stub-Code mit leeren Catch-Blöcken — exakt das Muster, das R13 verbietet. Ein Linter-Fehler auf dieses Pattern zwingt den Agenten, explizite Fehlerbehandlung zu implementieren, was die Code-Qualität im nächsten Iterationsschritt verbessert.

Die Ausnahmen (`AllowedSilentCatchExceptionTypes` und R05 für `OperationCanceledException`/`ObjectDisposedException`) sind fachlich korrekt: Diese Exceptions signalisieren geordnetes Shutdown, nicht unerwartete Fehler — ein stilles Ignorieren ist dort semantisch vertretbar.

## Zeitliche Einordnung

**Grundlagenstabilität:** Zeitlos

Exceptions als primärer Fehlerkommunikationskanal in .NET sind ein strukturelles Sprachkonzept. Das Unterdrücken dieses Kanals schadet unabhängig von der Modellgeneration — weder bessere LLMs noch bessere .NET-Versionen ändern daran etwas. Das Problem ist in der Semantik der Exception-Behandlung verankert, nicht in der Modellkapazität.

## Risiken / Gegenargumente

**Legitime stille Ignores in engem Kontext:** Gelegentlich gibt es Szenarien, in denen eine Exception wirklich ignoriert werden soll (z.B. Best-Effort-Cleanup in Finalizer-Kontexten). AiNetLinters `AllowedSilentCatchExceptionTypes`-Whitelist löst diesen Fall; der Mechanismus ist korrekt.

**Overly broad catch-all:** Die Regel zwingt zum Explizieren — aber nicht zum Differenzieren. Ein `catch (Exception ex) { _logger.LogError(ex, "..."); throw; }` erfüllt die Regel, ist aber immer noch nicht ideal (zu breit). Hier wäre eine ergänzende Regel (z.B. Verbot von `catch (Exception)` ohne engere Exception-Typen) sinnvoll — das liegt aber außerhalb des Scope von R13.

---

## Quellen

- Casalnuovo et al., 2019, "Studying the Evolution of Exception Handling Anti-Patterns" — https://link.springer.com/article/10.1186/s13173-019-0095-5
- Harness.io / Demiri, 2022, "Swallowed Exceptions: The Silent Killer of Java Applications" — https://www.harness.io/blog/swallowed-exceptions-java-applications
- Fowler & Beck, 1999/2018, "Refactoring: Improving the Design of Existing Code" — Addison-Wesley
- arXiv, 2025, "Inside the Scaffold: Agent Failure Taxonomy" — arXiv:2604.03515
