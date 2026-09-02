# Befundbericht: Decompiler-Body-Matching bei optionalen Parametern & VB.NET-Binaries

**Datum:** 2026-09-02  
**Gegenstand:** AiNetLinter MCP-Server – On-Demand-Dekompilierung via `get_symbol_body`  
**Kategorie:** `[Agenten-Sackgasse / Roslyn-Decompiler-Bruch]`  
**Schweregrad:** **P1**  
**Datei:** `tasks/using-audit-funktionstest/findings-decompiled-body.md`

---

## 1. Problembeschreibung & Live-Beweise

Der MCP-Server ist prinzipiell in der Lage, Methoden-Bodies aus externen/dekompilierten Assemblies (`targetType="assembly"`) on-demand im C#-Quellcode bereitzustellen. Dies wurde live an folgenden Symbolen nachgewiesen:

### Was erfolgreich funktioniert:
1. `Helper.SetAktivBeleg(Beleg faBeleg)` in `Sagede.OfficeLine.Pps.Fertigungsauftrag.dll`:
   ```csharp
   public static bool SetAktivBeleg(Beleg faBeleg)
   {
       bool flag = true;
       if (faBeleg != null)
       {
           faBeleg.IstAktivSet(aktiv: true, mitTerminierung: true, mitEinlasten: false, mitParentPosition: false);
           return faBeleg.Errors.Count == 0;
       }
       return false;
   }
   ```
2. `Helper.EinlastungUndo(Beleg faBeleg, bool withSubFa)` in `Sagede.OfficeLine.Pps.Fertigungsauftrag.dll`:
   ```csharp
   public static void EinlastungUndo(Beleg faBeleg, bool withSubFa)
   {
       faBeleg.EinlastenUndo();
       foreach (Beleg item in faBeleg.Belege)
       {
           EinlastungUndo(item, withSubFa);
       }
   }
   ```
3. `Aufteilungsbuchung.Dispose()` in `Sagede.OfficeLine.Rewe.Buchungserfassung.dll`:
   ```csharp
   public void Dispose()
   {
       Dispose(disposing: true);
       GC.SuppressFinalize(this);
   }
   ```

---

### Was systematisch fehlschlägt:
Sobald Methoden mit **optionalen Parametern** (Standard in VB.NET-Assemblies wie Sage 100 PPS/Rewe) oder externen Typen angefragt werden (z. B. `Beleg.Save()`, `Beleg.Initialize()`, `Tools.VerursacherVKBelegnummerGet(Mandant, int)`), meldet das Tool:

```text
bodyAvailability: unavailable; contentMode: decompiledSignatureOnly
Hinweis: Für das dekompilierte Symbol wurde kein Member-Body gefunden.

// Für dieses Symbol ist kein dekompilierbarer Body verfügbar.
```

Obwohl der ICSharpCode-Decompiler den Quelltext der gesamten Klasse fehlerfrei dekompiliert, kann der Linter den konkreten Member-Knoten im dekompilierten C#-Syntaxbaum nicht finden.

---

## 2. Root-Cause-Analyse im Code

Betroffene Datei: [`src/AiNetLinter/Mcp/Assemblies/Analysis/Bodies/AssemblyDecompiledBodyResolver.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/Bodies/AssemblyDecompiledBodyResolver.cs)

### Ursache A: Strikte Parameter-Längengleichheit bei optionalen Parametern
In VB.NET deklarierte Methoden mit optionalen Parametern:
```vb
Public Function Save(Optional includeSubBelege As Boolean = False, Optional saveStrukturAll As Boolean = False) As Boolean
```
werden in den Roslyn-Metadaten als Überladungen mit 0, 1 und 2 Parametern abgebildet (`Save()`, `Save(bool)`, `Save(bool, bool)`).

Der ICSharpCode-Decompiler erzeugt im C#-Quelltext jedoch eine **einzige** C#-Methode mit Standardwerten:
```csharp
public bool Save(bool includeSubBelege = false, bool saveStrukturAll = false)
{
    // ... Methodeninhalt ...
}
```

In [`AssemblyDecompiledBodyResolver.cs:257-261`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/Bodies/AssemblyDecompiledBodyResolver.cs#L257-L261) prüft `MatchesParameters`:
```csharp
private static bool MatchesParameters(
    SeparatedSyntaxList<ParameterSyntax> syntaxParameters,
    ImmutableArray<IParameterSymbol> symbolParameters)
{
    if (syntaxParameters.Count != symbolParameters.Length) return false;
    // ...
}
```
- Für `Save()` (0 Parameter): `syntaxParameters.Count (2) != symbolParameters.Length (0)` -> **Match schlägt fehl!**
- Für `Save(bool)` (1 Parameter): `syntaxParameters.Count (2) != symbolParameters.Length (1)` -> **Match schlägt fehl!**
- Ergebnis: Der Knoten wird verworfen und `FindMember` liefert `null`.

### Ursache B: Parameter-Typ-Auflösung bei unvollständigen Abhängigkeiten
In [`MatchesParameterType`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/Bodies/AssemblyDecompiledBodyResolver.cs#L285-L295) wird der Typname aus dem Syntaxbaum mit dem vollqualifizierten Roslyn-Typ verglichen. Wenn eine referenzierte Assembly (z. B. `Mandant` aus `Sagede.OfficeLine.Shared`) nicht im Scope aufgelöst werden kann, weichen die Normalisierungs-Strings (`Mandant` vs `Sagede.OfficeLine.Shared.Mandant`) voneinander ab, wodurch `MatchesParameterType` `false` liefert.

---

## 3. Konkreter Lösungsvorschlag & Implementierung

### 1. `MatchesParameters` um optionale Parameter erweitern
Wenn die Anzahl der Syntax-Parameter größer ist als die Anzahl der Symbol-Parameter (`syntaxParameters.Count >= symbolParameters.Length`), muss geprüft werden, ob alle überschüssigen Parameter Standardwerte besitzen:

```csharp
private static bool MatchesParameters(
    SeparatedSyntaxList<ParameterSyntax> syntaxParameters,
    ImmutableArray<IParameterSymbol> symbolParameters)
{
    // Wenn Syntax-Parameter vorhanden sind, die im Symbol fehlen:
    // Diese dürfen nur matchen, wenn alle überschüssigen Syntax-Parameter optional sind (Default-Wert haben)
    if (syntaxParameters.Count < symbolParameters.Length) return false;

    for (var index = 0; index < syntaxParameters.Count; index++)
    {
        var syntaxParameter = syntaxParameters[index];
        if (index < symbolParameters.Length)
        {
            var symbolParameter = symbolParameters[index];
            if (!string.Equals(
                    GetParameterModifier(syntaxParameter),
                    symbolParameter.RefKind.ToString(),
                    StringComparison.OrdinalIgnoreCase)
                || !MatchesParameterType(syntaxParameter.Type?.ToString(), symbolParameter.Type))
            {
                return false;
            }
        }
        else
        {
            // Überschüssiger Syntax-Parameter: MUSS einen Default-Wert besitzen (z. B. "= false", "= null")
            if (syntaxParameter.Default is null)
            {
                return false;
            }
        }
    }

    return true;
}
```

### 2. Resilientes Fallback für unaufgelöste Parametertypen
In `MatchesParameterType` ein Fallback auf den einfachen Typnamen (`ITypeSymbol.Name`) zulassen, wenn `symbolType.TypeKind == TypeKind.Error` oder die Assembly partiell ist (`completeness == partial`):

```csharp
private static bool MatchesParameterType(string? syntaxType, ITypeSymbol symbolType)
{
    if (syntaxType is null) return false;
    var normalizedSyntax = NormalizeTypeName(syntaxType);
    
    // Standardvergleich
    if (new[]
        {
            symbolType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
            symbolType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            symbolType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
        }.Select(NormalizeTypeName).Contains(normalizedSyntax, StringComparer.Ordinal))
    {
        return true;
    }

    // Fallback bei unaufgelösten Metadatentypen (CS0246 / Missing References):
    var simpleSyntaxName = normalizedSyntax.Split('.').Last();
    return string.Equals(simpleSyntaxName, symbolType.Name, StringComparison.Ordinal);
}
```

---

## 4. Test- & Verifikationsplan

1. **Unit-Test in `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyDecompiledBodyResolverTests.cs`:**
   Test mit einer Klasse, die Methoden mit Default-Parametern deklariert:
   ```csharp
   public void Save(bool includeSub = false, bool saveAll = false) { ... }
   ```
   Verifizieren, dass `resolver` für Überladungen mit 0, 1 und 2 Parametern denselben dekompilierten Body liefert.
2. **Integration-Test auf Assembly-Target:**
   Aufruf von `get_symbol_body` auf `Beleg.Save()` in `Sagede.OfficeLine.Pps.Fertigungsauftrag.dll` muss den tatsächlichen Speichern-Code zurückgeben (`bodyAvailability: available`).
