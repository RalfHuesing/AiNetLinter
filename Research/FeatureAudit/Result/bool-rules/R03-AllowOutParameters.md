# AllowOutParameters / Verbot von `out`-Parametern (R03)

**Kategorie:** Boolean-Regel  
**Aktueller Wert:** false (= `out`-Parameter sind verboten) | **Status:** Aktiv (mit Ausnahmen via R04 und R06)  
**Severity:** warning  
**Paper-Cluster genutzt:** D

---

## Bewertung

🟢 **WERTVOLL**

**Fazit:** Das Verbot von `out`-Parametern mit präzisen, praxiserprobten Ausnahmen (Try-Pattern via R04, private Methoden via R06) entspricht dem C#-Industriekonsens und eliminiert einen der häufigsten "async-Fälle" wo `out` tatsächlich schaden kann; Behalten.

---

## Empfehlung

**Aktion:** Aktiviert lassen  
**Begründung:** `out`-Parameter verhindern `async/await` (nicht in Methoden mit `out` nutzbar), erzwingen eine Deklaration vor dem Aufruf und führen zu weniger lesbaren Aufruf-Signaturen. Die sauberere Alternative — Tuple-Rückgabe oder Record/ValueObject — produziert klareren, für LLMs besser navigierbaren Code. Die Severity `warning` (statt `error`) ist für ein Verbot ungewöhnlich aber tolerierbar.

---

## Wissenschaftliche / Empirische Grundlage

`out`-Parameter sind ein C#-Feature aus der frühen Sprachgeschichte, das primär für das TryParse-Pattern und bestimmte interop-Szenarien gedacht war. Heute überwiegen die Nachteile im Produktionscode:

1. **Inkompatibilität mit async/await:** C# verbietet `out`- und `ref`-Parameter in `async`-Methoden. In einer modernen .NET-Codebasis, die auf asynchroner Programmierung basiert, führt `out` regelmäßig zu Refactoring-Zwängen.
2. **Erhöhte Aufruf-Komplexität:** Der Aufrufer muss eine Variable deklarieren, bevor er sie übergeben kann (`var x; if(TryGet(out x))`). Tuple-Rückgaben (`(bool success, T value) Get()`) sind lesbarer und decken denselben Anwendungsfall ab.
3. **Verletzung der "Methode hat eine Aufgabe"-Regel:** `out` ist oft ein Signal dass eine Methode mehrere Rückgabewerte produziert — was besser durch einen dedizierten Rückgabetyp ausgedrückt wird.

Microsoft Roslyn Quality Analyzer CA1021 ("Vermeiden Sie out-Parameter") und die Community-Literatur (albertherd.com 2017) bestätigen diesen Konsens. Dedizierte empirische Studien mit Fehlerraten existieren nicht — die Evidenz ist designbasiert und normativ.

Die Severity `warning` statt `error` ist bei einem Verbot ungewöhnlich. Es deutet darauf hin, dass die Regel absichtlich als Hinweis statt als harte Grenze gesetzt wurde — möglicherweise weil Legacy-Code oder Framework-Aufrufe Ausnahmen brauchen, die nicht durch R04 und R06 abgedeckt sind. Dies erscheint pragmatisch.

## KI-Agenten-Perspektive

Für LLM-Agenten ist das `out`-Parameter-Muster ein verbreitetes Muster in älterem C#-Code und .NET-Framework-APIs, das sie kennen. Das Problem liegt im Generierungskontext: Wenn ein Agent neuen Code hinzufügt, tendiert er ohne Linter-Regel dazu, Muster aus alten Teilen der Codebase zu imitieren — auch wenn diese veraltet sind. Das Verbot via Linter unterbricht diese Imitation und lenkt den Agenten zu modernen Alternativen (Tuples, Records). Da `out`-Parameter zudem async/await verhindern, kann ein Agent der fälschlicherweise `out` in einer async-Methode verwendet, einen Compilefehler produzieren (Ableitung; kein direktes Paper).

## Zeitliche Einordnung

**Grundlagenstabilität:** Zeitlos

Das strukturelle Problem — `out` blockiert async und erzeugt Lesbarkeits-Overhead — ist eine C#-Spracheigenschaft die sich nicht ändert. Auch leistungsfähigere Modelle werden mit `out`-Parametern in async-Kontexten Compilerfehler produzieren.

## Risiken / Gegenargumente

Das wichtigste Gegenargument: Einige .NET-APIs aus der Standardbibliothek und vielen NuGet-Paketen verwenden `out`-Parameter (z.B. `Dictionary.TryGetValue`, `int.TryParse`). Diese können ohne Wrapper-Code nicht vermieden werden. In der Praxis heißt das: Jeder Aufruf von Framework-Methoden mit `out` würde eine Warnung erzeugen, wenn R04 diese nicht ausreichend abdeckt. Die Ausnahmen via R04 (Try-Pattern) und R06 (private Methoden) decken die häufigsten legitimen Fälle ab; die Severity `warning` (statt `error`) dämpft den Rausch in Legacy-Grenzfällen. Ein verbleibender Rausch bei Framework-Interop-Aufrufen ist möglich und sollte beobachtet werden.

---

## Quellen

- C# Community & Blog-Konsens — On the Usage of Out Parameters, albertherd.com, 2017 (https://albertherd.com/2017/10/10/on-the-usage-of-out-parameters/)
- Microsoft Roslyn Code Quality Analyzers — CA1021: Avoid out parameters (https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1021)
- Microsoft .NET Design Guidelines, 2024 (https://learn.microsoft.com/en-us/dotnet/csharp/)
