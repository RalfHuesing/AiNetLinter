# Bedingt sinnvoll & Nischen-Bedarf (Sammeldokument)

Dieses Dokument sammelt Ideen, die architektonisch denkbar sind oder für Spezialfälle einen Mehrwert bieten, aktuell aber eine **niedrigere Priorität** haben oder von Vorstufen abhängen.

---

## 1. `feature_context` (Composite One-Shot-Tool)

* **Idee:** Ein mächtiges One-Shot-Tool, das für ein Ziel-Symbol (z. B. eine Kernklasse) folgendes auf einmal aggregiert:
  1. Symbol-Definition & Skelett (`get_file_skeleton` / `get_class_structure`)
  2. Direkte Callers & Callees (`get_call_tree` depth=1)
  3. Aktuelle Metriken & AIContextFootprint (`metrics_lookup`)
  4. Zugehörige Tests (`get_test_context`)
  5. Offene Linter-Violations auf dieser Datei (`get_violations`)
* **Status / Bewertung:**
  * **Bedingt sinnvoll:** Sehr mächtig für den ersten Schritt bei großen Refactorings ("Give me all context for class X").
  * **Abhängigkeit:** Baut direkt auf `metrics_lookup` und `get_test_context` auf. Erst wenn diese beiden Basis-Tools existieren, kann `feature_context` als schlanker Composite-Orchestrator gebaut werden.

---

## 2. ASP.NET-Framework-Analyzer-Suite

* **Idee:** 6 hochspezifische Roslyn-Analyzer für ASP.NET Core & Web-APIs:
  1. `AspNetControllerRouteAnalyzer` (fehlende/ungültige Route-Attribute)
  2. `MinimalApiEndpointAnalyzer` (MapGet/MapPost-Validierung)
  3. `MiddlewarePipelineAnalyzer` (Prüfung der Aufruf-Reihenfolge in `Program.cs`, z. B. Auth vor MapControllers)
  4. `DependencyInjectionAnalyzer` (Erkennung von zirkulären DI-Registrierungen)
  5. `GrpcServiceAnalyzer` (Contract-Validierung)
  6. `RouteConflictAnalyzer` (Doppelte Routes / Konflikte)
  * Plus 2 MCP-Tools (`aspnet_routes`, `aspnet_pipeline`).
* **Status / Bewertung:**
  * **Nischen-Bedarf:** Enormer Mehrwert für reine ASP.NET-Web-APIs und dort ein starkes Alleinstellungsmerkmal gegenüber textuellen Lintern.
  * **Aufwand:** Hoch (ca. 2 Wochen Implementierung & Tests). Sollte nur priorisiert werden, wenn ASP.NET-Projekte im Fokus stehen (im Vergleich zu CLI-Tools oder Bibliotheken).
