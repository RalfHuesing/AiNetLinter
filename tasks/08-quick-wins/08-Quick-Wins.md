# 08 – Quick-Win-Kandidaten

Diese Liste ist eine **Aufwandshypothese**, keine Zusage. Die v3-Runde hat Nutzung, nicht Implementierungskosten auditiert. Vor der Umsetzung sind Ursache und Testgrenze je Punkt kurz zu prüfen.

| Kandidat | Erwarteter Nutzen | Geschätzter Aufwand | Vorbehalt |
|---|---|---|---|
| `SYMBOL_NOT_FOUND` mit `isError=true` ausgeben | Clients erkennen Fehler ohne Textparsing | Eher klein | Content-only-Vertrag und bestehende Client-Erwartungen prüfen; [05](05-Fehlerstatus.md) |
| Bei `find_references` in Blazor die Markup-Grenze explizit nennen | Verhindert „0 Referenzen = unbenutzt“ | Eher klein | Erkennung von Razor-Scope/Tool-Antwort prüfen; [03](03-Razor-Referenzgrenze.md) |
| Verify-Zähler mit klaren Bezeichnern erläutern | Weniger Verwechslung von `evidence`, Kandidaten und Sichtbaren | Eher klein | Antwortbudget und Parser-Vertrag prüfen; [04](04-Verify-Ausgabe.md) |
| Doppelte Treffer/Hinweise in `search_pattern`/`get_file_tree` reduzieren | Weniger Leseaufwand und Bytes | Eher klein | Nur reproduzierte Doppelzeilen entfernen; [03](03-Razor-Referenzgrenze.md), [07](07-Laufzeit-und-Bytes.md) |

**Nicht als Quick Win einstufen, bevor die Ursache klar ist:** Konstruktor-Handoff-Auflösung ([01](01-Konstruktor-Handoffs.md)), paginierter Advisory-Zugriff ([04](04-Verify-Ausgabe.md)), semantische Razor-Referenzen ([03](03-Razor-Referenzgrenze.md)) und SAN-Verify-Performance ([07](07-Laufzeit-und-Bytes.md)). Diese können kleine Fixes sein, können aber auch Identitäts-, Index- oder API-Arbeit erfordern.

## Vorschlag für die nächste Runde

Zuerst den Konstruktor-Handoff als klaren Nutzungsbruch reproduzieren und eingrenzen. Danach den Fehlerstatus und die Markup-Grenzkennzeichnung als kleine, getrennte Änderungen schätzen. Die Advisory-Genauigkeit erst auf identischer Symbol-Ebene neu bewerten; der bisherige `DashboardQuery`-Fehlalarm ist nicht bestätigt.
