# 02 – Dead-Code-Advisories korrekt einordnen

**Status:** mehrere statische Befunde bestätigt; kein sicherer Löschentscheid. **Schweregrad:** mittel. **Kernfrage:** Welche Aussage bezieht sich auf Typ, Konstruktor, Member oder produktive Referenz?

## Bestätigte Stichproben

- AiNetLinter: `h:ciD2` (`ResidentCount`) hat drei Testreferenzen und keine produktive statische Referenz; `test_only` ist für das Member bestätigt. `h:ciIN` (`DetermineImpactStatus`) hat keine gefundenen statischen Referenzen. [Protokoll](AiNetLinter.md)
- SqlToAi: `ITokenVault.Store` hat 15 Testreferenzen, keine beobachtete produktive Referenz; `test_only` ist statisch plausibel. `ConstraintRow.ConstraintType` hat keinen beobachteten Property-Lesezugriff; Dapper-Befüllung ist als dynamischer Gegencheck berücksichtigt. [Protokoll](SqlToAi.md)
- SAN: `_persistence` wird laut Stichprobe zugewiesen, aber statisch nicht gelesen. `OpenDialog()` hat einen Markup-Aufruf; die `test_only`-Einordnung bleibt möglich, weil er in einer Test-Layoutseite liegt. [Protokoll](SAN.md)

## Korrektur: `DashboardQuery` ist bislang kein bestätigter Fehlalarm

Im [KnowHowToAI-Bericht](KnowHowToAI.md) wird eine produktive Referenz `DashboardService.cs:50` als Widerlegung des `test_only`-Advisorys gewertet. Die fehlgeschlagene Handoff-Auflösung nennt aber `DashboardQuery.#ctor`. Die Produktionsstelle ist ein Parameter vom Typ `DashboardQuery`; sie konstruiert keinen Wert. Eine erneute Textgegenprobe fand `new DashboardQuery()` nur in sieben Teststellen und den Parametertyp in Produktion. MCP `find_symbol(pattern:"DashboardQuery")` bestätigte den Record; `find_references` auf den Typnamen zeigte die Parameterstelle. Damit bleibt die **Konstruktor-Klassifikation offen bzw. statisch plausibel**, während der Handoff-Fehler aus [01](01-Konstruktor-Handoffs.md) bestätigt ist.

## Weitere unklare Fälle

- KnowHowToAI `CurrentUser.Id`: keine C#-Lesestelle gefunden, der Typ wird aber produktiv konstruiert. Serialisierung/Reflection sind offen.
- KnowHowToAI `IReleaseRepository.FindAsync`: Test-Call-Site und produktive Implementierung; Referenz-/Implementierungs-Handoffs lieferten auffällige Zuordnungen. Das ist kein gesicherter Advisory-Fehlalarm.
- Analyzer-Member wie `Initialize` können durch Roslyn-Konventionen genutzt werden, ohne reguläre Call-Site. Der Audit zeigte Integrationshinweise, aber keinen vollständigen End-to-End-Nachweis für jeden Member.

## Grundlage für ein mögliches Umsetzungskonzept

Eine spätere Genauigkeitsarbeit sollte eine Fallmatrix mit Symbolart, statischen Produktions-/Testreferenzen, Markup, DI/Reflection und externem API-Vertrag verwenden. Erwartete Ausgabe pro Fall: `bestätigt`, `falsch positiv` oder `unklar`, jeweils auf **derselben Symbol-Ebene**. Vorher sind keine Kandidaten zu löschen.

**Offen:** Ein wirklich bestätigter `test_only`-Fehlalarm auf identischem Symbol ist in der v3-Stichprobe nach dieser Korrektur nicht gesichert. Die nicht angezeigten Kandidaten sind unbewertet.
