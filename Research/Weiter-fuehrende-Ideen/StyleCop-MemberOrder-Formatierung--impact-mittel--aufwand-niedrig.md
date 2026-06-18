# StyleCop / dotnet format — Member-Reihenfolge & Formatierung (extern)

**Impact: mittel | Aufwand: niedrig | Haltbarkeit: dauerhaft**
**Umsetzung: als NuGet / CI-Schritt, nicht in AiNetLinter**

---

## Problem

LLMs wurden auf Milliarden Zeilen Code trainiert, der einer bestimmten Konvention für
Member-Reihenfolge folgt:

```
Konstanten → statische Felder → Instanzfelder → Konstruktoren → Properties → Public Methods → Private Methods
```

Wenn in einem File Konstruktoren nach privaten Methoden stehen, oder Properties zwischen
Felder gemischt sind, verletzt das die trainierten Erwartungen des Modells. Der Agent muss
mehr Kontext lesen um die Struktur der Klasse zu verstehen — statt direkt zur gesuchten Methode
zu navigieren.

**Sekundärer Nutzen:** Konsistente Formatierung reduziert Git-Diffs und macht Code-Reviews
einfacher — sowohl für Menschen als auch für Agenten die Diffs analysieren.

---

## Umsetzung

### dotnet format (in .NET SDK integriert)

```bash
# Alle .cs-Dateien formatieren:
dotnet format

# Nur prüfen (CI):
dotnet format --verify-no-changes

# Nur bestimmte Regeln:
dotnet format --diagnostics IDE0055
```

```ini
# .editorconfig:
[*.cs]
csharp_style_namespace_declarations = file_scoped:error
dotnet_sort_system_directives_first = true
csharp_new_line_before_open_brace = all
```

### StyleCop.Analyzers (Member-Reihenfolge)

```xml
<PackageReference Include="StyleCop.Analyzers" Version="1.2.0-beta.556">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
</PackageReference>
```

```json
// stylecop.json:
{
    "settings": {
        "orderingRules": {
            "elementOrder": [
                "kind",
                "accessibility",
                "constant",
                "static",
                "readonly"
            ]
        }
    }
}
```

**Wichtigste Regel: SA1201** — Erzwingt die Reihenfolge:
Fields → Constructors → Properties → Methods

### Empfohlene StyleCop-Regeln für AI-Readability

| Regel | Beschreibung |
| :--- | :--- |
| SA1201 | Member-Reihenfolge (Fields → Ctors → Props → Methods) |
| SA1202 | Public members before private |
| SA1203 | Konstanten vor Feldern |
| SA1204 | Statische members vor Instanz-members |

---

## Brownfield-Strategie

StyleCop auf eine bestehende Codebasis loszulassen erzeugt beim ersten Durchlauf
Zehntausende Violations. Richtige Einführung:

1. **Schritt 1:** `dotnet format` einmalig laufen lassen — automatisch fix-bar, kein manueller Aufwand
2. **Schritt 2:** StyleCop als Warnung einführen (`dotnet_diagnostic.SA1201.severity = warning`)
3. **Schritt 3:** Schrittweise zu Error erhöhen, File für File

---

## Praxis-Bewertung

| Dimension | Bewertung |
| :--- | :--- |
| Wartungsaufwand | Keiner — einmalig konfigurieren, danach automatisch |
| False-Positive-Risiko | Mittel für Brownfield (initial viele Violations) |
| Adoptionsbarriere | Niedrig für Greenfield, Mittel für Brownfield |

**Empfehlung:** Direkt nutzbar. Nicht in AiNetLinter nachbauen — die Tools sind ausgereift.
`dotnet format` ist im .NET SDK bereits enthalten, kein zusätzlicher Aufwand.

---

## Haltbarkeit

Konsistente Code-Struktur ist eine fundamentale Eigenschaft die unabhängig von
Modell-Verbesserungen bleibt. Member-Reihenfolge ist Teil der trainierten C#-Konventionen —
dauerhaft relevant.
