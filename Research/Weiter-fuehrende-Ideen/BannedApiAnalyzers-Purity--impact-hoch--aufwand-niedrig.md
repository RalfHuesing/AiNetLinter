# BannedApiAnalyzers — Deterministische Purity-Erzwingung (extern)

**Impact: hoch | Aufwand: niedrig | Haltbarkeit: dauerhaft**
**Umsetzung: als NuGet-Paket, nicht in AiNetLinter**

---

## Problem

Nicht-deterministische APIs in Domain-Schichten erzeugen LLM-feindlichen Code auf mehreren Ebenen:

1. **Testbarkeit:** `DateTime.Now` in Domain-Methoden erzeugt Tests die an Uhrzeiten
   scheitern — Agenten die Tests generieren, schreiben oft Tests die beim nächsten Run
   fehlschlagen.
2. **Reasoning-Ambiguität:** Ein Agent der `ProcessOrder()` liest und sieht `DateTime.Now`,
   weiß nicht, welchen Wert die Methode zur Laufzeit produziert — er kann keine zuverlässigen
   Invarianten ableiten.
3. **Seiteneffekt-Unsichtbarkeit:** `Guid.NewGuid()` in Domain-Logik erzeugt bei
   jedem Aufruf ein anderes Ergebnis — Agenten generieren Code der das Ergebnis als
   deterministisch behandelt.

---

## Verbotene APIs nach Layer

### Domain-Schicht (strikt):

```
System.DateTime.Now
System.DateTime.UtcNow
System.DateTime.Today
System.Guid.NewGuid
System.Environment.GetEnvironmentVariable
System.Environment.MachineName
System.Random (new Random(), Random.Shared)
System.Threading.Thread.Sleep
System.IO.File (direkte Dateioperationen)
```

### Application-Schicht (empfohlen):

```
System.Console.WriteLine (außer in CLI-Entry-Points)
System.Diagnostics.Debug.Assert (außer in Test-Projekten)
```

---

## Umsetzung: Microsoft.CodeAnalysis.BannedApiAnalyzers

Das offizielle NuGet-Paket erledigt das vollständig. In AiNetLinter selbst nachbauen
lohnt sich nicht.

### Installation

```xml
<!-- Directory.Build.props oder Projektdatei: -->
<PackageReference Include="Microsoft.CodeAnalysis.BannedApiAnalyzers" Version="3.3.4">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
</PackageReference>
```

### BannedSymbols.txt

```
T:System.DateTime;Inject IClock instead of using DateTime.Now directly
P:System.DateTime.Now;Use IClock.UtcNow() instead
P:System.DateTime.UtcNow;Use IClock.UtcNow() instead
P:System.DateTime.Today;Use IClock.Today() instead
M:System.Guid.NewGuid;Inject IGuidGenerator instead
M:System.Environment.GetEnvironmentVariable(System.String);Read config at startup, inject via options pattern
```

### .editorconfig-Integration

```ini
[*.cs]
dotnet_diagnostic.RS0030.severity = error   # BannedApiAnalyzers
```

---

## Architekturmuster: IClock-Abstraktion

```csharp
// Schnittstelle im Domain-Projekt:
public interface IClock
{
    DateTimeOffset UtcNow { get; }
    DateOnly Today { get; }
}

// Produktion: SystemClock.cs in Infrastructure
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    public DateOnly Today => DateOnly.FromDateTime(DateTime.Today);
}

// Test: FakeClock.cs in Tests
public sealed class FakeClock(DateTimeOffset startTime) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = startTime;
    public DateOnly Today => DateOnly.FromDateTime(UtcNow.LocalDateTime);
}
```

Der Agent der `ProcessOrder(IClock clock)` sieht, kann zuverlässige Zeitpunkt-Tests
generieren — und weiß, dass `clock.UtcNow` einen testbaren, injizierten Wert liefert.

---

## Praxis-Bewertung

| Dimension | Bewertung |
| :--- | :--- |
| Wartungsaufwand | Initial `BannedSymbols.txt` anlegen, danach wartungsfrei |
| False-Positive-Risiko | Niedrig — die verbotenen APIs sind klar definiert |
| Adoptionsbarriere | Niedrig — ein NuGet-Paket und eine TXT-Datei |

**Empfehlung:** Direkt nutzbar. Kein AiNetLinter-Entwicklungsaufwand.

---

## Haltbarkeit

Nicht-Determinismus ist ein fundamentales Qualitätsmerkmal, unabhängig von LLMs.
Dauerhaft relevant.
