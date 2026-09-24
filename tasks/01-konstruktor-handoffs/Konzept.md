---
status: draft
---

# Konzept: Konstruktor-Handoffs

## Intention

Ein aus einer MCP-Ausgabe übernommenes Konstruktor-Handoff soll ohne Umformung wieder zu genau demselben Konstruktor führen. So können Agenten insbesondere Dead-Code-Kandidaten auf Symbol-Ebene prüfen, ohne auf eine ungenauere Typnamensuche ausweichen zu müssen.

## Befund und Abgrenzung

Die [Task-Evidenz](01-Konstruktor-Handoffs.md) zeigt `SYMBOL_NOT_FOUND` für mehrere Konstruktor-Handoffs aus `verify` und `get_file_skeleton` in SqlToAi sowie für einen Record-Konstruktor in KnowHowToAI. Typ-Handoffs funktionieren dort. Im aktuellen Code erzeugt `AnalysisSymbolIdentity.FormatHandoff` die ID aus der Roslyn-Dokumentations-ID; `SymbolIdentifierResolver.TryResolveByStableIdAsync` sucht sie über Source-Deklarationen. `FindReferencesTool.ResolveSymbolAsync` ist der gemeinsame Einstieg für die drei hier relevanten Folgetools. **Die technische Ursache ist damit eingegrenzt, aber nicht nachgewiesen.**

Die produktive Referenz auf den Typ `DashboardQuery` in `DashboardService.cs` belegt für sich genommen keinen Aufruf seines Konstruktors. Ob das zugehörige `test_only`-Advisory falsch ist, bleibt eine eigenständige Symbolprüfung; der Handoff-Fehler darf nicht als Beleg dafür dienen.

## Muss

- Von MCP ausgegebene Handoffs für explizite Konstruktoren mit und ohne Parameter sowie für den primären Konstruktor eines positional Records unverändert in `find_references`, `get_symbol_body` und `get_feature_context` verwenden können. Alle drei lösen dieselbe Konstruktor-Identität auf.
- Die Auflösung unterscheidet überladene Konstruktoren anhand ihrer Signatur und behält die bestehende Projekt-, Ziel- und Snapshot-Bindung sowie eindeutige Fehler bei unbekannten oder veralteten Handoffs bei.
- Ein reproduzierender xUnit-v3-Test wird vor der Korrektur rot; nach der Korrektur belegen Tests Erzeugung, Übergabe und Auflösung der betroffenen Konstruktor-Handoffs. Ein bestehender Handoff für einen gewöhnlichen Typ oder eine Methode bleibt als Gegenprobe navigierbar.

## Nicht

- Eine allgemeine Änderung der Dead-Code-Klassifikation oder eine Neubewertung aller Advisories in den beiden externen Solutions.
- Eine Erweiterung der Handoff-Semantik für andere Symbolarten ohne reproduzierten Fehler.
- Änderungen an SqlToAi oder KnowHowToAI.

## Verifikation

Die Regressionstests verwenden die tatsächlich ausgegebene Handoff-ID als Eingabe, nicht eine aus Namen nachgebaute ID. Sie prüfen Treffer und Konstruktor-Signatur getrennt von Typreferenzen sowie den Fehlerfall eines nicht vorhandenen Symbols. Die verbindlichen Build-, Verify- und FastTests-Gates folgen `.agents/rules/AiNetLinter-Richtlinien.mdc`; für den Konzeptschritt selbst sind keine Code-Gates vorgesehen.

## Arbeitsgedächtnis (nur Draft)

**Offene Scope-Entscheidung:** Empfehlung: den bestätigten Konstruktor-Handoff-Defekt beheben und andere Symbolarten nur als Gegenprobe absichern. Eine Ausweitung auf komplexe Signaturen allgemein erfordert zusätzliche reproduzierte Fälle und vergrößert den Prüfbereich. Die Dead-Code-Klassifikation bleibt eine separate Aufgabe.
