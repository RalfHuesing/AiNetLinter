frage zu CompoundSuppression:

es wird nur "MaxMethodLineCount" als "Compound Suppressions" implementiert?

andere dinge werden nicht beachtet?

(keine änderungen, erkläre es mir und erkläre mir auch was noch sinnvoll wäre)

---

## Was der Plan tatsächlich implementiert

**Die Infrastruktur ist generisch** — `CompoundSuppressionEvaluator` und das Datenmodell (`CompoundSuppression`, `MetricCondition`) funktionieren für jede Regel. Ein Nutzer könnte theoretisch beliebige `TargetRule`-Werte in seine `rules.json` schreiben.

**Aber: Jede Regel braucht einen "Hook" im Code.**

Der Plan implementiert konkret nur folgende Hooks:

| Phase | Regel | Hook-Ort | Status |
|:--|:--|:--|:--|
| 1 | `MaxMethodLineCount` | `ComplexityChecker.CheckMethodLineCount` | Vollständig mit 3-Szenarien-Guidance |
| 1 | `MaxMethodParameterCount` | `ComplexityChecker.CheckParamCount` | Erwähnt, kein Detail |
| 2 | `MaxPublicMembersPerType` | `PublicMembersChecker.Check` | Nur Architektur skizziert |
| 2 | `MaxConstructorDependencies` | `StateChecker.Check*` | Nur Architektur skizziert |

Der ausgelieferte **Default in `rules.json`** enthält nur **einen einzigen** vorkonfigurierten Eintrag: `MaxMethodLineCount` bei CC≤3/CogC≤5.

---

## Was sinnvoll wäre aber nicht drin ist

**1. `MaxCognitiveComplexity` + Methoden-Tiefe**

Eine Methode mit CogC=18 aber LineCount=15 ist ein anderes Problem als CogC=18 bei LineCount=80. Kurze, hochkomplexe Methoden sind oft Ausdruck von viel verschachtelter Business-Logik die schwer extrahierbar ist — das Feedback sollte anders lauten als bei langen komplexen Methoden. Kein Suppression-Case, aber die Guidance-Texte könnten das differenzieren.

**2. `MaxLineCount` (Datei-Ebene) + durchschnittliche Methodenkomplexität**

Eine 650-Zeilen-Datei die nur `const`-Definitionen oder Enum-Mappings enthält (durchschnittliche CC aller Methoden < 2) ist ein anderes Problem als eine 650-Zeilen-Datei mit 10 komplexen Methoden. Der Plan ignoriert das komplett.

**3. `MaxConstructorDependencies` + `MaxPublicMembersPerType` als bidirektionales Compound**

Zwei Metriken in Kombination sind im Plan nur einseitig gedacht. Eine tiefere Variante wäre: wenn BEIDE Metriken gleichzeitig im oberen Drittel ihrer Limits sind (z.B. 12 Members bei Limit 15 UND 4 Deps bei Limit 5), dann eskaliere statt zu supprimieren — Violation mit erhöhter Severity. Das war die ursprünglich diskutierte "Eskalations"-Richtung, die bewusst ausgeklammert wurde.

**4. `MaxMethodParameterCount` + keine echte Config**

Steht als "supprimierbar" im Plan aber ohne konkreten Default-Wert. Ein sinnvoller Default wäre: wenn `ParameterCount=5` aber alle Parameter sind Primitives ohne Flags (BoolParameterCount=0) könnte man relaxen. Allerdings deckt das `MaxBoolParameterCount` bereits indirekt ab.

**5. Kein "Warnstufen"-Konzept**

Aktuell ist alles binär: supprimiert oder nicht. Was fehlt ist ein `Severity`-Override — also: "wenn Bedingungen X erfüllt, dann ändere Severity von `error` auf `warning`". Das wäre orthogonal zur Suppression und würde ermöglichen, dass Violations sichtbar bleiben aber den Build nicht blocken. Passt gut zu dem was das `RuleMetadata`-System bereits hat.

---

**Kurz zusammengefasst:** Der Plan ist schlanker als er wirkt — der generische Teil ist solide, aber der konkret angebundene Scope ist eng. Ob das okay ist, hängt davon ab wie oft du `MaxPublicMembersPerType` als False Positive siehst. Wenn selten: reicht Phase 1 vollständig. Der Warnstufen-Gedanke (#5) wäre am nützlichsten was im Plan fehlt.
