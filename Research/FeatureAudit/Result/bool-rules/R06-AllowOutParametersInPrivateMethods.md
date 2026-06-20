# AllowOutParametersInPrivateMethods (R06)

**Kategorie:** Boolean-Regel  
**Aktueller Wert:** true | **Status:** Aktiv (Ausnahme zu R03)  
**Severity:** n.a. (Ausnahmeregel)  
**Paper-Cluster genutzt:** D

---

## Bewertung

🟡 **UNPRAKTIKABEL** (in der aktuellen Form zu weit gefasst)

**Fazit:** Die vollständige Ausnahme für alle privaten Methoden untergräbt den zentralen Zweck von R03 zu sehr — private Methoden mit `out`-Parametern sind in modernem C#-Code ein Code-Smell der genauso vermieden werden sollte, aber die pragmatische Entscheidung ist verständlich und macht den Linter in Legacy-Codebases einsetzbar.

---

## Empfehlung

**Aktion:** Aktiviert lassen, aber langfristig enger fassen  
**Begründung:** Die vollständige Ausnahme für private Methoden verhindert zwar False-Positives bei privaten Hilfsmethoden, gibt aber gleichzeitig implizit frei, dass die gesamte private API des Projekts beliebig `out`-Parameter verwenden kann. Eine engere Fassung (z.B. nur in `private static`-Methoden oder nur in Methoden mit `Try`-Präfix) würde die Ausnahme präziser machen. Kurzfristig ist die aktuelle Konfiguration pragmatisch und sollte nicht geändert werden.

---

## Wissenschaftliche / Empirische Grundlage

Private Methoden sind nicht Teil der öffentlichen API eines Typs; Verstöße gegen Designprinzipien dort haben geringere Auswirkungen auf den externen Kontrakt. Dies ist die Begründung hinter der Ausnahme: Das Verbot von `out`-Parametern in öffentlichen Methoden ist aus Lesbarkeitsgründen für Konsumenten des APIs wichtig; bei privaten Methoden ist der Kreis der "Konsumenten" auf die Klasse selbst begrenzt.

Das Argument ist designbasiert und folgt dem Prinzip "Public API ist Vertrag, private Implementierung ist flexibel". Dedizierte empirische Studien die zwischen privaten und öffentlichen `out`-Parametern unterscheiden, existieren nicht. Die C#-Community-Literatur (albertherd.com 2017) behandelt `out`-Parameter generell als Code-Smell ohne diese Unterscheidung zu machen.

Das pragmatische Gegenargument: Privaten Methoden mit `out`-Parametern existieren häufig in Pattern-Match-Hilfsmethoden, in `ParseX`-Privat-Helfern oder in tight-loops wo Tuple-Allokationen vermieden werden sollen. Das Verbot ohne Ausnahme würde hier entweder unnötige Abstraktionsschichten erzwingen oder den Linter für Performance-sensitiven Code unbrauchbar machen.

## KI-Agenten-Perspektive

Für LLM-Agenten ist die Unterscheidung public/private im Kontext von `out`-Parametern weniger relevant als für menschliche Entwickler — der Agent sieht den gesamten Code und reproduziert Muster unabhängig von der Sichtbarkeit. Eine Ausnahme für private Methoden kann dazu führen, dass ein Agent der `out`-Parameter in privaten Methoden vorfindet, dieses Muster für neue Methoden übernimmt — auch für öffentliche. Der Linter würde dann nur die öffentlichen Verwendungen abfangen, was zu einem inkonsistenten Muster im Projekt führt (Ableitung; kein direktes Paper).

## Zeitliche Einordnung

**Grundlagenstabilität:** Zeitlos

Die Designprinzipien dahinter — öffentliche API-Klarheit vs. private Implementierungsflexibilität — sind zeitstabile Software-Engineering-Prinzipien.

## Risiken / Gegenargumente

Das Hauptrisiko ist Schleichpfad-Proliferation: `out`-Parameter wachsen in privaten Methoden unkontrolliert, weil der Linter dort nicht eingreift. In großen Codebasen kann dies dazu führen, dass R03 primär als "public API"-Regel wahrgenommen wird, statt als generelles Code-Qualitätsprinzip. Eine Alternative wäre eine "Opt-in"-Ausnahme per `PathOverrides` für spezifische Performance-kritische Klassen, statt eine globale Ausnahme für alle privaten Methoden.

---

## Quellen

- C# Community & Blog-Konsens — On the Usage of Out Parameters, albertherd.com, 2017 (https://albertherd.com/2017/10/10/on-the-usage-of-out-parameters/)
- Microsoft Roslyn Code Quality Analyzers — CA1021: Avoid out parameters (https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1021)
