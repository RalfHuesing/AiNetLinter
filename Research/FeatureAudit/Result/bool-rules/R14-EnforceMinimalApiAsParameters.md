# EnforceMinimalApiAsParameters (R14)

**Kategorie:** Boolean-Regel  
**Aktueller Wert:** false (deaktiviert) | **Status:** Deaktiviert  
**Severity:** error (wenn aktiviert)  
**Paper-Cluster genutzt:** D, C

---

## Bewertung

🟡 **UNPRAKTIKABEL**

**Fazit:** Die Grundidee ist valide — explizite Parameter statt HttpContext-Injection verbessern Testbarkeit und LLM-Lesbarkeit — aber die Regel ist zu eng auf ASP.NET Minimal API fokussiert, um breiten Nutzen zu entfalten; im derzeitigen deaktivierten Zustand ist das vertretbar.

---

## Empfehlung

**Aktion:** Deaktiviert lassen (optional: Aktivieren, wenn Minimal-API-Nutzung im Projekt zunimmt)  
**Begründung:** Die Regel adressiert ein reales Design-Problem (implizite HttpContext-Abhängigkeiten), ist aber auf ein spezifisches Framework-Muster (ASP.NET Minimal APIs) beschränkt; ohne aktive Minimal-API-Nutzung im Projekt erzeugt sie Konfigurationsaufwand ohne Wirkung.

---

## Wissenschaftliche / Empirische Grundlage

Die Regel folgt dem allgemeinen Prinzip der **expliziten Parameterübergabe** gegenüber impliziten Kontext-Abhängigkeiten. Dieses Prinzip ist in der C#-Community breit akzeptiert:

**Microsoft .NET Design Guidelines** empfehlen, HttpContext nicht als "Magic Bag" für Daten zu nutzen. Explizite Parameter machen Methodensignaturen selbstdokumentierend: Was eine Methode braucht, steht in ihrer Signatur — nicht versteckt im HttpContext.

**Testbarkeit:** Eine Methode, die einen expliziten `ClaimsPrincipal user`-Parameter erwartet, ist direkt unit-testbar ohne ASP.NET-Pipeline-Aufbau. Eine Methode die intern `HttpContext.User` liest, erfordert einen gemockten HttpContext — erheblich aufwändiger.

**Direkter empirischer Beleg fehlt:** Es gibt keine Studie, die speziell die Verwendung von expliziten vs. impliziten Parametern in Minimal-APIs mit Fehlerrate oder Wartbarkeit korreliert. Die Evidenz ist ausschließlich aus Design-Prinzipien und Testbarkeitsargumenten abgeleitet (Ableitung, kein direktes Paper).

## KI-Agenten-Perspektive

Aus Cluster C (Liu et al. 2025, "LLM Hallucinations"): **Project Context Conflicts** entstehen, wenn ein LLM-Agent falsche Annahmen über vorhandene APIs macht. Ein Minimal-API-Endpoint der `HttpContext` direkt nutzt, erfordert vom Agenten das Wissen über die gesamte HttpContext-API — ein umfangreiches, sich änderndes Framework-Objekt. Explizite Parameter (`ClaimsPrincipal user`, `string correlationId`) sind im Kontext-Fenster des Agenten sofort sichtbar und klar typisiert.

Allerdings: ASP.NET Minimal APIs sind für AiNetLinter-Projekte (CLI-Tool, monolithisch, kein Web-Framework laut CLAUDE.md) nicht primär relevant. Die Regel hätte einen höheren Mehrwert in Web-API-Projekten, die explizit Minimal APIs nutzen.

## Zeitliche Einordnung

**Grundlagenstabilität:** Offen

Das Prinzip expliziter Parameter ist zeitlos; die Regel selbst ist an das ASP.NET Minimal API-Muster gebunden, das sich mit .NET-Versionen weiterentwickelt. Sollte Microsoft Minimal APIs zugunsten anderer Paradigmen deprecaten, wird die Regel obsolet.

## Risiken / Gegenargumente

**Sehr schmaler Anwendungsbereich:** Die Regel greift nur bei ASP.NET Minimal-API-Endpoints mit HttpContext-Zugriffen — in CLI-Tools, Domain-Layern oder Services hat sie keinerlei Wirkung.

**Mögliche Konflikte mit Framework-Patterns:** Einige Minimal-API-Patterns (z.B. Endpoint-Filter, OutputCache-Integrationen) nutzen HttpContext intern auf eine Weise, die schwer durch explizite Parameter zu ersetzen ist. Die Regel könnte hier zu strengen Umbau-Anforderungen führen.

**Aktivierungsempfehlung:** Sinnvoll einzuschalten wenn das Projekt signifikant auf Minimal APIs setzt und Testabdeckung für Endpoints wichtig ist.

---

## Quellen

- Microsoft .NET Documentation — Minimal APIs: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis
- Microsoft .NET Design Guidelines — https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/identifier-names
- Liu et al., 2024/2025, "LLM Hallucinations in Practical Code Generation" — arXiv:2409.20550
