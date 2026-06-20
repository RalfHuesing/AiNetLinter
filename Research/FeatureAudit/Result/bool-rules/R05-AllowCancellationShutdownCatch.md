# AllowCancellationShutdownCatch (R05)

**Kategorie:** Boolean-Regel  
**Aktueller Wert:** true | **Status:** Aktiv (Ausnahme zu R13)  
**Severity:** n.a. (Ausnahmeregel)  
**Paper-Cluster genutzt:** D, F

---

## Bewertung

🟢 **WERTVOLL**

**Fazit:** Die Ausnahme für `OperationCanceledException` und `ObjectDisposedException` beim Shutdown ist technisch korrekt und notwendig — stilles Abfangen dieser Exceptions ist in genau diesem Kontext kein Antipattern, sondern das erwünschte Verhalten; Behalten.

---

## Empfehlung

**Aktion:** Aktiviert lassen  
**Begründung:** `OperationCanceledException` und `ObjectDisposedException` beim kontrollierten Shutdown sind konzeptionell keine "echten" Fehler — sie sind erwartete Signale im Lebenszyklusmanagement von .NET-Anwendungen. Stilles Abfangen ist hier nicht nur akzeptabel, sondern die korrekte Handlungsweise laut .NET-Designguidelines und Community-Konsens.

---

## Wissenschaftliche / Empirische Grundlage

In der .NET-Laufzeit gibt es zwei Exception-Typen die semantisch keine Fehler darstellen, sondern Ablaufsteuerungs-Signale:

**OperationCanceledException:** Wird geworfen wenn ein `CancellationToken` abgebrochen wurde. In `IHostedService.StopAsync`, in gRPC-Stream-Handlern und generell im Shutdown-Flow ist das Abfangen und stilles Ignorieren dieser Exception das korrekte Verhalten — das Programm soll ja stoppen. Jedes andere Verhalten (Logging als Fehler, Weiterwerfen) erzeugt False-Positive-Alerts im Monitoring.

**ObjectDisposedException:** Tritt auf wenn auf ein bereits entsorgtes Objekt zugegriffen wird. Im Shutdown-Kontext, wenn Ressourcen in einer definierten Reihenfolge freigegeben werden, kann es unvermeidlich zu Zugriffen auf bereits disponierten Objekten kommen (Race Conditions im Dispose-Flow). Das stille Abfangen ist hier die standardmäßige .NET-Empfehlung.

Microsoft dokumentiert dieses Muster in den Hosting-Guidelines für `IHostedService`. Die Exception-Handling-Literatur (Casalnuovo et al. 2019, Harness.io 2022) warnt zwar vor "swallowed exceptions", bezieht sich aber explizit auf unerwartete Exceptions in Geschäftslogik — nicht auf geplante Lifecycle-Exceptions. Der Unterschied ist konzeptionell: R13 (EnforceNoSilentCatch) bekämpft unbeabsichtigte Fehler-Vertuschung; R05 erlaubt bewusstes, semantisch korrektes stilles Abfangen.

## KI-Agenten-Perspektive

Für LLM-Agenten ist diese Ausnahme wichtig um False-Positive-Warnungen zu vermeiden. Wenn ein Agent Standard-Hosting-Code generiert (z.B. `IHostedService.ExecuteAsync` mit `stoppingToken`), wird er ein `try/catch (OperationCanceledException)` mit leerem Body schreiben — das ist das korrekte Muster. Ohne R05 würde R13 hier anschlagen und den Agenten in einen Korrektur-Loop schicken, in dem er versucht, korrekte Exception-Behandlung in einem Kontext hinzuzufügen, wo sie semantisch falsch wäre. Das würde zu komplexerem, falschen Code führen (Ableitung; kein direktes Paper zu diesem spezifischen Fall).

## Zeitliche Einordnung

**Grundlagenstabilität:** Zeitlos

`CancellationToken`-basiertes Lifecycle-Management ist das .NET-Standard-Paradigma für alle modernen Hosting-Szenarien (.NET Generic Host, ASP.NET Core, gRPC). Es wird nicht ersetzt. Die Ausnahme bleibt dauerhaft notwendig.

## Risiken / Gegenargumente

Das einzige Risiko ist Missbrauch: Entwickler (oder LLM-Agenten) könnten die R05-Ausnahme als Rechtfertigung nutzen, um `OperationCanceledException` auch außerhalb von Shutdown-Kontexten still abzufangen — also in Geschäftslogik, wo sie ein tatsächliches Problem signalisiert. Dies ist eine implementierungsseitige Frage: Prüft der Linter tatsächlich den Kontext (Shutdown-Handler) oder erlaubt er das stille Fangen von `OperationCanceledException` überall? Falls die Prüfung kontextfrei ist, besteht eine kleine aber reale Missbrauchsmöglichkeit. Eine scope-basierte Einschränkung (z.B. nur in Methoden die `CancellationToken` als Parameter haben) würde die Regel präziser machen.

---

## Quellen

- Microsoft .NET Hosting Guidelines — IHostedService & BackgroundService, 2024 (https://learn.microsoft.com/en-us/dotnet/core/extensions/hosted-services)
- Casalnuovo et al. — Studying the Evolution of Exception Handling Anti-Patterns, Journal of the Brazilian Computer Society, 2019 (https://link.springer.com/article/10.1186/s13173-019-0095-5)
- Harness.io / Rigerta Demiri — Swallowed Exceptions: The Silent Killer of Java Applications, 2022 (https://www.harness.io/blog/swallowed-exceptions-java-applications)
