# AllowTryPatternOutParameters (R04)

**Kategorie:** Boolean-Regel  
**Aktueller Wert:** true | **Status:** Aktiv (Ausnahme zu R03)  
**Severity:** n.a. (Ausnahmeregel)  
**Paper-Cluster genutzt:** D

---

## Bewertung

🟢 **WERTVOLL**

**Fazit:** Die Ausnahme für das Try-Pattern ist der einzige breit akzeptierte legitime Einsatz von `out`-Parametern in C# und entspricht exakt dem Community- und Framework-Konsens; Behalten.

---

## Empfehlung

**Aktion:** Aktiviert lassen  
**Begründung:** Das Try-Pattern (`bool TryX(out T value)`) ist die kanonische C#-Konvention für erwartbare Konvertierungsfehler ohne Exception-Overhead (analog zu `int.TryParse`, `Dictionary.TryGetValue`). Es ist semantisch klar definiert (Präfix `Try`, bool-Rückgabe, exakt ein `out`-Parameter) und stellt keine Mehrdeutigkeit dar. Die Ausnahme ist eng gefasst und bereitet keine falschen positiven Übertragungen vor.

---

## Wissenschaftliche / Empirische Grundlage

Das Try-Pattern ist eine in den offiziellen .NET Design Guidelines dokumentierte Designkonvention für Methoden, die entweder ein Ergebnis produzieren oder signalisieren dass kein Ergebnis verfügbar ist — ohne dabei eine Exception zu werfen. Die `int.TryParse`-, `DateTime.TryParse`- und `Dictionary.TryGetValue`-Methoden der .NET-Standardbibliothek sind die bekanntesten Vertreter.

Die Design-Richtlinien definieren das Muster explizit: Der Methodenname beginnt mit `Try`, der Rückgabetyp ist `bool`, und der `out`-Parameter enthält das Ergebnis im Erfolgsfall. Diese Eindeutigkeit unterscheidet das Try-Pattern von allgemeinen `out`-Verwendungen: Es gibt keine Ambiguität im Aufrufkontext, die Variable wird immer initialisiert (entweder mit dem Ergebnis oder dem Defaultwert), und der Aufrufer wird durch die bool-Rückgabe explizit zur Prüfung des Erfolgs gezwungen.

Dedizierte empirische Studien zum Try-Pattern existieren nicht — die Evidenz ist normativ (Microsoft Design Guidelines) und durch die breite Adoption in der .NET-Standardbibliothek und im Community-Konsens gestützt.

## KI-Agenten-Perspektive

Das Try-Pattern ist eines der am konsistentesten reproduzierten .NET-Muster in LLM-generierten Code. Agenten kennen `TryParse`, `TryGetValue` und das semantische Muster dahinter aus dem Vortraining sehr gut. Die Erlaubnis via R04 stellt sicher, dass ein Agent der legitimen Try-Pattern-Code generiert nicht durch R03 blockiert wird und zu unnötigen Wrappern umgeleitet wird. Das Muster ist für LLMs besonders "safe" weil:
1. Die Signatur vollständig deterministisch ist (`bool Try*(out T)`)
2. Der Name (`Try`-Präfix) semantisch eindeutig ist
3. Das Muster aus tausenden .NET-Quellen im Training bekannt ist

(Ableitung; kein direktes Paper zu Try-Pattern und LLM-Fehlerrate.)

## Zeitliche Einordnung

**Grundlagenstabilität:** Zeitlos

Das Try-Pattern ist eine fundamentale .NET-Sprachkonvention die seit .NET Framework 1.0 existiert und in der gesamten .NET-Standardbibliothek verwendet wird. Es wird nicht verschwinden.

## Risiken / Gegenargumente

Das einzige Risiko ist eine zu lockere Ausnahmebedingung — z.B. wenn Methoden den `Try`-Präfix missbräuchlich verwenden, ohne das Muster korrekt zu implementieren. Dies ist ein Code-Review-Problem, kein Linter-Problem. AiNetLinters Implementierung sollte prüfen, ob der Methodenname tatsächlich mit `Try` beginnt (und nicht nur `Try` enthält). Falls nur ein syntaktischer Präfix-Check stattfindet, ist die Ausnahme ausreichend eng. Eine Validierung dass genau ein `out`-Parameter vorhanden ist und der Rückgabetyp `bool` ist, würde die Regel robuster machen.

---

## Quellen

- Microsoft .NET Design Guidelines — Try-Pattern (Member Design Guidelines), 2024 (https://learn.microsoft.com/en-us/dotnet/csharp/)
- C# Community & Blog-Konsens — Out Parameters Code Smells, albertherd.com, 2017 (https://albertherd.com/2017/10/10/on-the-usage-of-out-parameters/)
