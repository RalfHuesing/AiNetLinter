# Review Runde 2: Konzept 11 — Projektregistry + Daemon-Modus

**Datum:** 2026-08-23  
**Grundlage:** Konzept.md nach Einarbeitung aller 15 Punkte aus Review Runde 1  
**Ergebnis:** Alle 15 Punkte aus Runde 1 sind sauber eingearbeitet. Drei neue Punkte gefunden.

---

## 🔴 A. Wiring-Muster: `using var lease` + async Return — Lifetime-Bug

Das verbindliche Wiring-Muster (Konzept Zeile 258–263) ist **fehlerhaft**:

```csharp
// FEHLERHAFT — so steht es im Konzept
(string projectRoot, string? namePattern = null, ...) =>
{
    using var lease = _registry.Lease(projectRoot);
    return FindSymbolTool.ExecuteAsync(lease.Server, namePattern, ...);
}
```

`return` gibt den `Task` zurück und das `using` disposed den Lease **sofort** (Ende des
synchronen Scopes), **bevor der Task abgeschlossen ist**. `InFlightCount` geht auf 0,
während der Tool-Call noch läuft — der Busy-Guard ist wirkungslos.

**Korrektur:**

```csharp
// KORREKT — async/await hält den Lease bis zum Task-Ende
async (string projectRoot, string? namePattern = null, ...) =>
{
    using var lease = _registry.Lease(projectRoot);
    return await FindSymbolTool.ExecuteAsync(lease.Server, namePattern, ...);
}
```

> [!IMPORTANT]
> Das Muster steht als „verbindlich" im Konzept und wird 26× mechanisch repliziert.
> Ohne `async`/`await` ist der gesamte InFlight-Tracking-Mechanismus (Review 7, Runde 1)
> wirkungslos.

---

## 🟡 B. `LoadFailed`-Einträge und TTL-Timer: Verhalten unterspezifiziert

Die Load-Dedupe-Beschreibung (Konzept Zeile 289–292) sagt: Bei `LoadState == LoadFailed` wird
der tote Eintrag „beim nächsten Hit" erkannt, entfernt und frisch geladen.

**Offene Frage:** Was macht der TTL-Timer mit einem `LoadFailed`-Eintrag?

- Er hat `InFlightCount == 0` und ist nicht `PendingEviction`
- Der TTL-Timer würde ihn nach 45 Minuten normal evicten — aber 45 Minuten auf einen
  Fehlschlag warten ist sinnlos

**Empfehlung:** `LoadFailed`-Einträge werden vom TTL-Timer **sofort** entfernt (kein
45-Min-Warten). Alternativ: gar nicht erst in die Registry eintragen (das Konzept sagt
bereits „KEIN Registry-Eintrag" bei Kalt-Load-Fehler — dann ist der Punkt hinfällig, aber
das steht im Widerspruch zur Load-Dedupe-Beschreibung, die von einem „toten Eintrag" spricht,
der „beim nächsten Hit erkannt" wird).

> [!NOTE]
> Hier besteht ein Widerspruch im Text: „KEIN Registry-Eintrag" (Zeile 394) vs.
> „der tote Eintrag wird beim nächsten Hit erkannt und entfernt" (Zeile 290–291).
> Eines von beiden muss stimmen — klären, ob der fehlgeschlagene Load einen Eintrag
> hinterlässt oder nicht.

---

## 🟢 C. `--daemon-start`: Doppelaufruf und Sichtbarkeit

Das Konzept sagt `--daemon-start` ist „intern, nicht für Clients gedacht" (Zeile 562), aber
es wird ein reales CLI-Argument, das `Program.cs` routen muss. Zwei Detailfragen:

1. **`--help`-Sichtbarkeit:** Erscheint `--daemon-start` in der CLI-Hilfe?
   Empfehlung: ja, aber als `[internal]` markiert (Konsistenz mit Entwickler-Erwartungen;
   versteckte Argumente erschweren Fehlersuche).

2. **Doppelter Aufruf:** Was passiert bei `--daemon-start`, wenn ein Daemon bereits läuft?
   Die Pipe ist belegt → `NamedPipeServerStream`-Konstruktor wirft.
   Vertrag sollte sein: sauberer Fehler auf stderr + Exit-Code ≠ 0 (nicht: unbehandelte
   Exception auf stderr). Kein Versuch, den laufenden Daemon zu ersetzen.

---

## Fazit

Das Konzept ist nach Einarbeitung der Runde-1-Punkte **umsetzungsreif**. Punkt A (async/await
im Lease-Muster) muss noch korrigiert werden — ohne ihn ist der Busy-Guard strukturell
wirkungslos. Punkt B ist eine Textinkonsistenz, die beim Implementieren auffallen würde.
Punkt C ist ein Detail, das der umsetzende Agent im Task klären kann.
