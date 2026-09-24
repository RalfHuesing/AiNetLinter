# AiNetLinter-MCP Nutzungs-Audit v3 – Token-Effizienz und Alltagsergonomie

## Umfang und Messmethode

Read-only-Stichprobe mit dem aktualisierten MCP-Server. Es wurden keine Builds oder Tests gestartet und keine Ziel-Repositories geändert. Die einzigen hier ausgeführten MCP-Abfragen waren get_index_scope, get_namespace_tree, find_symbol, get_feature_context und get_file_tree.

Die Laufzeiten sind Wandzeit zwischen Aufruf und Rückgabe in der Tool-Ausführung. Die Bytezahlen messen (1) den UTF-8-Text der sichtbaren MCP-Content-Blöcke und (2) zusätzlich die UTF-8-kodierte JSON-Serialisierung des von der Tool-Schicht zurückgegebenen Result-Objekts. Das ist eine reproduzierbare Messung der Antwort im Aufrufer, keine Messung des MCP-Netzwerk-Framings. Tokenwerte sind nur grobe Orientierung mit etwa 4 UTF-8-Bytes pro Token; deutsche Texte, Code-IDs und JSON verhalten sich dabei unterschiedlich. Es wurde kein Tokenizer eingesetzt.

## Stichproben und Evidenz

### Lösungseinstiege über get_namespace_tree

Reproduzierbarer Aufruf je Lösung:

get_namespace_tree(targetPath=<absoluter .slnx-Pfad>, depth=1, includeTypes=false, maxResults=3, maxResponseBytes=4096)

| Lösung | Laufzeit | sichtbarer Text | JSON-Result | grobe Token-Näherung (JSON-Bytes / 4) |
|---|---:|---:|---:|---:|
| AiNetLinter | 72 ms | 419 B | 483 B | 121 |
| SqlToAi | 35 ms | 272 B | 336 B | 84 |
| KnowHowToAI | 114 ms | 797 B | 869 B | 217 |
| SAN | 202 ms | 1.387 B | 1.459 B | 365 |

Die Antworten sind kompakt und nennen Projekte, Typ-/Test-Klassifikation sowie Namespace-/Typzahlen. Für die SAN-Solution mit 15 Projekten blieb die Solution-Übersicht trotz maxResults=3, depth=1 vollständig; dieser Parameter begrenzt dort offenbar nicht die Projektliste. Die Antworten wiederholen jeweils denselben Tipp zur projektweisen Verfeinerung und die angefragte/effektive Tiefe. Das kostet in diesen kurzen Antworten 70–80 B; **Schweregrad niedrig**, Auswirkung gering, da die Wiederholung Orientierung gibt und nicht dominiert.

### Symbolsuche und Handoff-IDs

Reproduzierbare Aufrufe:

- AiNetLinter: find_symbol(targetPath="C:\Daten\Entwicklung\Ralf\AiNetLinter\AiNetLinter.slnx", pattern="Verify", maxResults=2, maxResponseBytes=4096, scopeType="production")
- SAN: find_symbol(targetPath="C:\Daten\Entwicklung\SAN\San.smart.Planner.Platform\San.smart.Planner.Platform.slnx", pattern="Planner", maxResults=2, maxResponseBytes=4096, scopeType="production")

AiNetLinter: 253 ms, 555 B sichtbarer Text / 619 B JSON-Result. SAN: 250 ms, 428 B / 492 B. Beide Ausgaben enthalten die zwei gezeigten Treffer mit Pfad/Position und handoffId sowie die Gesamtzahl (31 bzw. 35) und den Hinweis auf die Begrenzung. **Positiver Ergonomiebefund, Schweregrad keiner:** Die Suche ist sparsam und liefert direkt kopierbare Handoffs; Trefferzahl und Trunkierung sind nicht verborgen.

### Composite-Kontext und Trunkierung

Reproduzierbarer Aufruf AiNetLinter:

get_feature_context(targetPath="C:\Daten\Entwicklung\Ralf\AiNetLinter\AiNetLinter.slnx", symbolIdentifier="h:cjhH", maxCallers=2, maxTests=2, maxResponseBytes=4096, scopeType="production")

Laufzeit 13.599 ms; 1.808 B sichtbarer Text / 1.892 B JSON-Result (grob 473 Token). Der Composite-Aufruf war vollständig. Er enthält Deklaration, Metriken, Call-Sites, Teststatus und Violations in einem Aufruf. Das spart Folgeaufrufe. Kleine Redundanz: Abschnittsüberschriften nennen bereits Zähler/Status und die jeweilige Counts-Zeile wiederholt den Zähler; bei den leeren Testkandidaten wird zusätzlich nochmals „Keine statischen Testkandidaten“ ausgegeben. **Schweregrad niedrig**, auf dieser vollständigen Antwort etwa 200 B wiederholte Status-/Zählinformation.

Reproduzierbarer Aufruf SAN mit absichtlich enger Caller-Grenze:

get_feature_context(targetPath="C:\Daten\Entwicklung\SAN\San.smart.Planner.Platform\San.smart.Planner.Platform.slnx", symbolIdentifier="h:ciCA", maxCallers=2, maxTests=2, maxResponseBytes=4096, scopeType="production")

Laufzeit 3.333 ms; 2.265 B sichtbarer Text / 2.349 B JSON-Result. Die Antwort markierte Composite-Completeness: truncated, meldete 2 von 5 Referenzen und TruncatedBy: maxCallers; der „Nächster sicherer Schritt“ nannte die Erweiterung der Grenze. Das zeigt einen brauchbaren Vollständigkeitsmarker. Der begrenzte Aufruf erfordert aber eine gezielte Folgeabfrage, wenn alle Call-Sites benötigt werden.

Kontrollaufruf mit erhöhter Grenze:

get_feature_context(targetPath="C:\Daten\Entwicklung\SAN\San.smart.Planner.Platform\San.smart.Planner.Platform.slnx", symbolIdentifier="h:ciCA", maxCallers=10, maxTests=2, maxResponseBytes=8192, scopeType="production")

Laufzeit 4.959 ms; 2.608 B sichtbarer Text / 2.692 B JSON-Result (grob 673 Token), Composite-Completeness: complete, alle 5 von 5 Call-Sites ausgegeben. In diesem Beispiel vermeidet eine ausreichende Caller-Grenze den Folgeaufruf für nur 343 B mehr sichtbaren Text. **Schweregrad niedrig:** Die API markiert die unvollständige Antwort klar; Anwender müssen den Marker aber aktiv beachten. Bei dieser SAN-Stichprobe reicht maxCallers=10.

### Dateibaum-Summary der großen SAN-Solution

Reproduzierbarer Aufruf:

get_file_tree(targetPath="C:\Daten\Entwicklung\SAN\San.smart.Planner.Platform\San.smart.Planner.Platform.slnx", view="summary", includeExtensions=[".cs",".razor"], maxDepth=2, maxResults=5, maxResponseBytes=4096)

Laufzeit 48 ms; 719 B sichtbarer Text / 784 B JSON-Result (grob 196 Token). Der Server meldete 1.252 gescannte physische Dateien, 956 Treffer und 4,0 MB, darunter 950 .cs- und 6 .razor-Dateien. Die Ausgabe war wegen der Grenzen unvollständig: vier Top-Level-Aggregate wurden ausgegeben; WARN, HINWEIS und NEXT: refine_scope weisen auf Begrenzung und nächste Schritte hin. Das ist kurz und sichtbar gekennzeichnet, aber die drei direkt aufeinanderfolgenden Hinweise wiederholen die Begrenzung. **Schweregrad niedrig**, keine relevante Tokenlast; für gezielte Exploration ist der konkrete nächste Schritt nützlich.

### Umfangsübersicht und Laufzeit

Reproduzierbarer Aufruf:

get_index_scope(targetPath="C:\Daten\Entwicklung\Ralf\AiNetLinter\AiNetLinter.slnx")

Gemessene Laufzeit 81.570 ms; 991 B sichtbarer Text / 1.056 B JSON-Result (grob 264 Token). Ergebnis: 929 physische Dateien nach Extension, 938 Roslyn-Dokumente, darunter 23 generierte und 425 Testdokumente. Der Text ist kompakt und enthält zugleich Routing-Hinweise zu Nicht-C#-Dateitypen. **Schweregrad mittel für Alltagsergonomie:** knapp 82 s Wandzeit für rund 1 KB Antwort ist in dieser Sitzung auffällig und kann interaktive Exploration merklich bremsen. Es ist nur ein AiNetLinter-Aufruf gemessen; Kaltstart/Cache-/Serverzustand sind nicht isoliert, daher lässt sich die Ursache oder typische Laufzeit daraus nicht ableiten. Die anderen Lösungen wurden für diesen teureren Aufruf nicht dupliziert; deren Agenten prüfen die jeweiligen Lösungsspezifika.

## Gesamtbefund

Die getesteten begrenzten Such- und Kontextantworten blieben klein (unter 2,7 KB JSON-Result) und transportierten Handoffs, Trefferzahlen sowie Vollständigkeitsmarker. Der stärkste konkrete Ergonomiepunkt ist die gemessene Laufzeit von get_index_scope auf AiNetLinter; eine einzelne Messung erlaubt keine verlässliche Generalisierung. Bei Composite-Abfragen zeigt get_feature_context in SAN Trunkierung und nächsten Schritt klar an; eine Caller-Grenze von 10 erfasste in der Stichprobe alle fünf Call-Sites. Geringe Wiederholungen in Status-/Zählertexten und Dateibaum-Warnungen sind sichtbar, aber kein relevanter Tokenverbrauch.

## Grenzen

- Diese Stichprobe bewertet Antwortgröße und typische Handoffs, nicht die Dead-Code-Advisories oder das Gate-Verhältnis; dafür sind die lösungsspezifischen Auditberichte maßgeblich.
- Für SAN wurden nur begrenzte repräsentative Queries genutzt. Die 180k-LOC-Angabe wurde nicht durch Quelltextzählung verifiziert.
- get_file_tree erfasste in der Summary 1.252 Dateien und markierte Filter/Ausschlüsse; es wurde keine vollständige Dateiliste angefordert.
- Die gemessenen Laufzeiten enthalten Server-/Session-/Cache-Einflüsse und sind keine Benchmarkwerte.
