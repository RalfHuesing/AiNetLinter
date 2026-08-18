# Bedingt sinnvoll & Nischen-Bedarf (Sammeldokument)

Dieses Dokument sammelt Ideen, die architektonisch denkbar sind oder für Spezialfälle einen Mehrwert bieten, aktuell aber eine **niedrigere Priorität** haben oder von Vorstufen abhängen.

---

## 1. ASP.NET-Framework-Analyzer-Suite

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
