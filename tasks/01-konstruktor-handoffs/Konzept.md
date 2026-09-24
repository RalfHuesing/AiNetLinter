---
status: ready
---

# Konzept: Konstruktor-Handoffs

## Intention

Ein aus einer MCP-Ausgabe übernommenes Konstruktor-Handoff soll ohne Umformung wieder zu genau demselben Konstruktor führen. So können Agenten insbesondere Dead-Code-Kandidaten auf Symbol-Ebene prüfen, ohne auf eine ungenauere Typnamensuche ausweichen zu müssen.

## Befund und Abgrenzung

Die [Task-Evidenz](01-Konstruktor-Handoffs.md) zeigt `SYMBOL_NOT_FOUND` für mehrere Konstruktor-Handoffs aus `verify` und `get_file_skeleton` in SqlToAi sowie für einen Record-Konstruktor in KnowHowToAI. Typ-Handoffs funktionieren dort. Im aktuellen Code erzeugt `AnalysisSymbolIdentity.FormatHandoff` die ID aus der Roslyn-Dokumentations-ID; `SymbolIdentifierResolver.TryResolveByStableIdAsync` sucht sie über Source-Deklarationen. `FindReferencesTool.ResolveSymbolAsync` ist der gemeinsame Einstieg für die drei hier relevanten Folgetools. **Die technische Ursache ist damit eingegrenzt, aber nicht nachgewiesen.**

Die produktive Referenz auf den Typ `DashboardQuery` in `DashboardService.cs` belegt für sich genommen keinen Aufruf seines Konstruktors. Ob das zugehörige `test_only`-Advisory falsch ist, bleibt eine eigenständige Symbolprüfung; der Handoff-Fehler darf nicht als Beleg dafür dienen.

Die [Rot-Tests](../../src/AiNetLinter.FastTests/Mcp/Tools/SymbolGraph/ConstructorHandoffLifecycleTests.cs) erzeugen Handoffs für vier Konstruktorfälle aus `get_class_structure` und reichen sie unverändert an alle drei Folgetools weiter. Der Build ist grün; der gezielte Testlauf ist in allen vier Fällen erwartungsgemäß rot, weil jedes Folgetool `SYMBOL_NOT_FOUND` meldet. Die Tests bleiben für die spätere Korrektur im Code.

## Muss

- Von MCP ausgegebene Handoffs für explizite Konstruktoren mit und ohne Parameter sowie für den primären Konstruktor eines positional Records unverändert in `find_references`, `get_symbol_body` und `get_feature_context` verwenden können. Alle drei lösen dieselbe Konstruktor-Identität auf.
- Die Auflösung unterscheidet überladene Konstruktoren anhand ihrer Signatur und behält die bestehende Projekt-, Ziel- und Snapshot-Bindung sowie eindeutige Fehler bei unbekannten oder veralteten Handoffs bei.
- Ein reproduzierender xUnit-v3-Test wird vor der Korrektur rot; nach der Korrektur belegen Tests Erzeugung, Übergabe und Auflösung der betroffenen Konstruktor-Handoffs. Ein bestehender Handoff für einen gewöhnlichen Typ oder eine Methode bleibt als Gegenprobe navigierbar.

## Nicht

- Eine allgemeine Änderung der Dead-Code-Klassifikation oder eine Neubewertung aller Advisories in den beiden externen Solutions.
- Eine Erweiterung der Handoff-Semantik für andere Symbolarten ohne reproduzierten Fehler.
- Änderungen an SqlToAi oder KnowHowToAI.

## Verifikation

Die Regressionstests verwenden die tatsächlich ausgegebene Handoff-ID als Eingabe, nicht eine aus Namen nachgebaute ID. Die vorhandenen Rot-Tests prüfen Konstruktor-Signatur und passende Aufrufstelle; bei einer späteren Korrektur sind außerdem die Fehlerfälle für nicht vorhandene und veraltete Handoffs sowie die bestehenden Build-, Verify- und FastTests-Gates zu prüfen. Der aktuelle rote Teststand ist der vom Nutzer beauftragte Reproduktionsschritt, kein abgeschlossenes Quality-Gate.
