# Implementierungspläne: Neue Features nach Audit

Erstellt: 2026-06-20  
Basis: [Research/FeatureAudit/Result/new-features/proposals.md](../FeatureAudit/Result/new-features/proposals.md)

---

## Übersicht

| Plan | Feature | Typ | Prio | Aufwand |
|---|---|---|---|---|
| [N01-BanAsyncVoid.md](N01-BanAsyncVoid.md) | `BanAsyncVoid` | Boolean (`GlobalConfig`) | 🟢 Empfohlen | ~4–6 h |
| [N02-BanBlockingTaskAccess.md](N02-BanBlockingTaskAccess.md) | `BanBlockingTaskAccess` | Boolean (`GlobalConfig`) | 🟢 Empfohlen | ~5–7 h |
| [N03-MaxLinqChainLength.md](N03-MaxLinqChainLength.md) | `MaxLinqChainLength` | Numerisch (`MetricsConfig`) | 🟡 Prüfen | ~6–9 h |

---

## Inhalt jedes Plans

Jeder Plan enthält:
- Änderungsübersicht (welche Dateien, welche Art)
- Konfigurationsänderungen in `LinterConfig.cs` und `LinterConfigOverrides.cs`
- Vollständigen Checker-Code (`Core/Checkers/`)
- Einbindung in `LinterAnalyzer.cs`
- `RuleRegistry.cs`-Eintrag mit allen Metadaten
- Vollständige Unit-Test-Suite (xUnit v3)
- Dokumentations-Updates (`ROADMAP.md`, `configuration.md`)
- `rules.json`-Eintrag
- Commit-Vorschlag
- Offene Fragen / Risiken

---

## Empfohlene Reihenfolge

1. **N01** (BanAsyncVoid) — einfachste Implementierung, reiner Signatur-Check
2. **N02** (BanBlockingTaskAccess) — mittlere Komplexität, ergänzt N01 zu einem vollständigen Async-Anti-Pattern-Set
3. **N03** (MaxLinqChainLength) — nur wenn N01/N02 grün sind; Standard deaktiviert wegen moderater Evidenz

N01 und N02 können parallel entwickelt werden (keine geteilten Abhängigkeiten).

---

## Gemeinsames Epic in ROADMAP.md

```markdown
## Epic 26: Async/Await-Sicherheit
- [ ] BanAsyncVoid
- [ ] BanBlockingTaskAccess

## Epic 27: LINQ-Komplexitäts-Kontrolle  
- [ ] MaxLinqChainLength
```
