# False-Positive-Research

Dieses Dokument beschreibt welche False-Positive-Signale AiNetLinter liefert.
Es beschreibt welche Maßnahmen wir implementieren können, damit diese False-Positive-Signale nicht mehr geliefert werden —
entweder als Konfigurationsoption oder im besten Fall durch verbesserte Code-Analyse, die das Signal automatisch vermeidet.

Es wird jeder Punkt den AiNetLinter prüft auf False-Positive-Signale analysiert, die in der Praxis vorkommen.
In der Praxis haben wir umfangreiche Projekte oft inklusive Blazor/Razor und WPF.
Zu jedem Punkt gibt es ein Kapitel.

Ziel: Basierend auf diesem Dokument später verstehen, was die falschen Signale waren, wie sie behoben werden können — inklusive Tests.

Konkretes Praxisbeispiel: `Todo\2026-06-14-Consumer-Wunschliste-SanSmartPlannerPlatform-Strict-Audit.md`.
Das Tool macht keinen Sinn wenn es Signale/Fehler liefert und wir den Code „verschlimmbessern" — ein LLM/Agent kann den
Code dann noch weniger einfach lesen/verstehen. Das wollen wir mit diesem Research erkennen und die Gegenmaßnahmen entwickeln.

Gründe warum es die Regeln gibt sind in `README.md` sowie
`Research\DeepResearch\20260613\AiNetLinter_ LLM-Code-Optimierung und Agenten-Workflows.md` beschrieben.

---

## Übersicht: Analysierte Regeln

| # | Regel | Standard | FP-Risiko | Hauptkategorie |
|---|-------|----------|-----------|----------------|
| 1 | `MaxLineCount` | 500 | Mittel | Datei-Metrik |
| 2 | `MaxMethodLineCount` | 42 | Mittel | Methoden-Metrik |
| 3 | `MaxMethodParameterCount` | 4 | Mittel | Methoden-Metrik |
| 4 | `MaxCyclomaticComplexity` | 5 | Hoch | Komplexität |
| 5 | `MaxCognitiveComplexity` | 5 | Hoch | Komplexität |
| 6 | `MaxInheritanceDepth` | 2 | **Kritisch** (WPF) | Architektur |
| 7 | `MaxMethodOverloads` | 3 | Niedrig | Methoden-Metrik |
| 8 | `MaxConstructorDependencies` | 5 | Mittel | Architektur |
| 9 | `MaxDirectoryDepth` | 4 | Mittel | Struktur |
| 10 | `MaxAIContextFootprint` | 5000 | Niedrig | Architektur |
| 11 | `EnforceSealedClasses` | aktiv | Hoch | Qualitätsregel |
| 12 | `StaticTestSentinel` | aktiv | Mittel | Qualitätsregel |
| 13 | `EnforcePascalCase` | aktiv | Niedrig | Naming |
| 14 | `EnforceSemanticNaming` | aktiv | Mittel | Naming |
| 15 | `EnforceNullableEnable` | aktiv | Niedrig | Qualitätsregel |
| 16 | `EnforceNoSilentCatch` | aktiv | Mittel | Resilienz |
| 17 | `EnforceValueObjectContracts` | aktiv | Niedrig | Architektur |
| 18 | `EnforceNoMagicValues` | deaktiviert (Strict) | **Kritisch** | Qualitätsregel |
| 19 | `EnforceResultPatternOverExceptions` | deaktiviert (Strict) | Hoch | Architektur |
| 20 | `EnforceExplicitStateImmutability` | deaktiviert (Strict) | Hoch (Blazor/WPF) | Architektur |
| 21 | `EnforceNamespaceDirectoryMapping` | deaktiviert (Strict) | Hoch | Struktur |
| 22 | `EnforceStrictBoundaryForBusinessLogic` | deaktiviert (Strict) | Mittel | Architektur |
| 23 | `DetectAndBanPhantomDependencies` | deaktiviert | Mittel | Architektur |
| 24 | `PreventContextDependentOverloads` | deaktiviert | Niedrig | Architektur |
| 25 | `RequireExplicitTruncationHandling` | deaktiviert | Mittel | Resilienz |

---

## Kapitel 1 — Metriken

### 1.1 MaxLineCount (Standard: 500)

**Was wird geprüft:** Gesamtzeilenanzahl der `.cs`-Datei.

**False-Positive-Szenarien:**

| Muster | Warum kein echter Verstoß |
|--------|--------------------------|
| WPF `*.designer.cs` | Compiler-generierter Code, nicht editierbar |
| Roslyn Source Generator (`*.g.cs`) | Auto-generiert, nicht Ausdruck von Komplexität |
| Große Konstanten-/Enum-Dateien | Zeilen sind Deklarationen, keine Logik |
| WPF/Blazor-Hilfstexte (`AppSettingsFieldHints.cs`) | Datentabelle, kein Algorithmus |
| Partial-Class-Teile (WPF Code-Behind) | Logische Einheit verteilt auf mehrere Dateien |

**WPF-Spezifika:**
- `MainWindow.xaml.cs` und Designer-Dateien wachsen durch Event-Handler und InitializeComponent-Boilerplate schnell auf 500+.
- `*.designer.cs`-Dateien sind vollständig auto-generiert und können tausende Zeilen umfassen.

**Blazor-Spezifika:**
- Bei code-behind-Trennung (`.razor.cs`-Dateien) kann die Komponentendatei durch viele `[Parameter]`-Properties und Lifecycle-Methoden groß werden.

**Gegenmaßnahmen:**
- Dateinamen-Pattern-Ausschluss konfigurierbar machen: `ExcludeFilePatterns: ["*.designer.cs", "*.g.cs", "*.generated.cs"]`
- `AggregatePartialClassLineCount: true` bereits als Option in Metrics vorhanden (noch nicht aktiv) — wenn aktiv, sollten einzelne Partials separat nicht gemeldet werden

---

### 1.2 MaxMethodLineCount (Standard: 42)

**Was wird geprüft:** Anzahl Code-Zeilen pro Methode (ohne Kommentare und Leerzeilen), via `MethodLineCounter`.

**False-Positive-Szenarien:**

| Muster | Warum kein echter Verstoß |
|--------|--------------------------|
| WPF `InitializeComponent()` | Compiler-generiert, read-only |
| Blazor `BuildRenderTree()` | Vom Razor-Compiler generiert |
| Fluent-Builder-Kette | Jede `.With...()`-Zeile ist eine Konfiguration, keine Logik |
| Data-Seed/Test-Fixture-Methode | Viele Zeilen = viele Datensätze, kein Algorithmus |
| Switch-Dispatcher (Routing-Methode) | Jeder Case ist ein trivialer Delegations-Einzeiler |
| Validierungsregel-Listen | Viele kurze Rules ohne Verzweigungstiefe |

**Konkrete Praxis-Beispiele:**

```csharp
// Fluent-Builder — 50 Zeilen, aber genau 1 logischer Vorgang:
return builder
    .SetTitle("...")
    .SetColor(...)
    .SetFont(...)
    // ... weitere 47 Zeilen .SetX(...)
    .Build();
```

```csharp
// Switch-Dispatcher — 15 Cases, jeder 1-2 Zeilen:
public Task<Result> ExecuteCommandAsync(string cmd, ...) {
    if (cmd == "extend-item") return Task.FromResult(HandleExtendItem(...));
    if (cmd == "move-item")   return Task.FromResult(HandleMoveItem(...));
    // ... weitere Cases
}
```

**Gegenmaßnahmen:**
- `ExcludeSwitchDispatcherCases: true` — Dispatch-Methoden erkennen und Case-Zeilen aus dem Count herausnehmen (Details im Consumer-Wunschliste-Dokument)
- `ExcludeFluentBuilderChains: true` — Method-Chaining-Pattern erkennen
- `ExcludeGeneratedMethods: true` — `InitializeComponent`, `BuildRenderTree` ausschließen (erkennbar am `[System.CodeDom.Compiler.GeneratedCode]`-Attribut)

---

### 1.3 MaxMethodParameterCount (Standard: 4)

**Was wird geprüft:** Anzahl Parameter einer Methode-Deklaration (`MethodDeclarationSyntax`). Konstruktoren werden separat via `MaxConstructorDependencies` geprüft.

**False-Positive-Szenarien:**

| Muster | Warum kein echter Verstoß |
|--------|--------------------------|
| ASP.NET Minimal-API-Endpoints | Route- + Query-Parameter + Body + CancellationToken = 5–6 |
| Event-Handler-Signaturen | `object sender, EventArgs e` + eigene Context-Params |
| Interface-Implementierungen mit vorgegebener Signatur | Signatur vorgegeben durch Framework/Interface |
| Factory-Methoden mit vielen konfigurierbaren Optionen | Pattern erlaubt, aber viele Parameter |

**WPF-Spezifika:**
- `IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)` — exakt 4 Parameter, am Limit; jede eigene Hilfslogik überschreitet.

**Blazor-Spezifika:**
- Minimal-API-Endpoints in Blazor Server können durch DI-Parameter (Services direkt im Endpoint) schnell 5+ Parameter erreichen.

**Gegenmaßnahmen:**
- `IgnoreWhenImplementingInterface: true` — wenn die Methode ein Interface implementiert (`override` oder `interface` erkannt), nicht melden
- `ExcludeExtensionMethods: true` — `this`-Parameter zählt häufig nicht zur fachlichen Komplexität

---

### 1.4 MaxCyclomaticComplexity (Standard: 5)

**Was wird geprüft:** McCabe-Komplexität einer Methode — zählt Entscheidungspunkte (if, else, case, while, for, foreach, &&, ||, ?:, catch).

**False-Positive-Szenarien:**

| Muster | Warum kein echter Verstoß |
|--------|--------------------------|
| Switch-Dispatcher (10+ triviale Cases) | Routing-Tabelle, kein echter Entscheidungsbaum |
| Guard-Clause-Kette am Methodenstart | Frühzeitige Rückgaben reduzieren Komplexität im Kern, erhöhen aber McCabe |
| Validation-Methode mit vielen Feldern | Jede Prüfung ist unabhängig; Gesamtlogik trivial |
| WPF `IValueConverter.Convert` mit Typ-Switch | Darstellungslogik, kein Domänenalgorithmus |
| C#-Pattern-Matching-Expressions | `switch (x) { case A a when ... }` erhöht Zähler schnell |

**Konkrete Praxis-Beispiele:**

```csharp
// Guard-Clause-Kette: Komplexität 6, aber Kernlogik ist trivial
public Result Process(Input input) {
    if (input is null) return Result.Fail("null");       // +1
    if (input.Id <= 0) return Result.Fail("bad-id");    // +1
    if (!input.IsActive) return Result.Fail("inactive"); // +1
    if (input.Name is { Length: 0 }) return Result.Fail("name"); // +1
    if (input.Budget < 0) return Result.Fail("budget"); // +1
    return DoWork(input);  // Kernlogik — 1 Zeile
}
```

**Gegenmaßnahmen:**
- `ComplexityNearMissTolerance: 1` — Wert im Bereich (Limit, Limit+1] nur als Warning, nicht Error
- `ExcludeSwitchDispatcherCases: true` — Dispatch-Arms nicht zählen (Details im Consumer-Wunschliste-Dokument)
- `ExcludeGuardClauses: true` — Frühzeitige Returns am Methodenkopf erkennen und aus dem Zähler ausklammern

---

### 1.5 MaxCognitiveComplexity (Standard: 5)

**Was wird geprüft:** Kognitive Komplexität (nach Sonar) — berücksichtigt Verschachtelungstiefe stärker als McCabe. Jede weitere Verschachtelungsebene erhöht den Beitrag eines Entscheidungspunkts.

**Unterschied zu MaxCyclomaticComplexity:** Kognitive Komplexität ist in der Regel schwerer zu unterschreiten, da Verschachtelung stärker bestraft wird. Beide Regeln können für dieselbe Methode feuern, aber mit unterschiedlichen Begründungen.

**False-Positive-Szenarien:**

| Muster | Warum kein echter Verstoß |
|--------|--------------------------|
| `async`/`await` mit mehreren `catch`-Blöcken | Exception-Handling erhöht Verschachtelung ohne echte Komplexität |
| LINQ mit `where` + `select` + `let` | Deklarativer Ausdruck, aber Sonar-Algorithmus zählt `where` |
| Nested-Null-Checks vor .NET-8-Null-Coalescing | Historischer Code; `?.`-Kette wäre äquivalent |
| WPF-Binding-Konverter mit Fallback-Logik | Mehrere Typ-Checks + Fallback als Standard-Pattern |

**Gegenmaßnahmen:**
- Gleiche Optionen wie bei `MaxCyclomaticComplexity` (`ComplexityNearMissTolerance`, `ExcludeGuardClauses`)
- `AggregatePartialClassComplexity: true` — Komplexität über Partial-Class-Teile nicht summieren, sondern einzeln bewerten

---

### 1.6 MaxInheritanceDepth (Standard: 2)

**Was wird geprüft:** Die Vererbungstiefe einer Klasse bis `System.Object` — zählt ALLE Basisklassen in der Kette, inklusive Framework-Klassen.

**Kritischer Fund aus dem Quellcode** (`LinterAnalyzer.Architecture.cs`):
```csharp
private static int GetInheritanceDepth(INamedTypeSymbol symbol)
{
    int depth = 0;
    var current = symbol.BaseType;
    while (current != null && current.SpecialType != SpecialType.System_Object)
    {
        depth++;
        current = current.BaseType;
    }
    return depth;
}
```

Die Methode zählt **alle** Stufen der Kette — Framework-Klassen inklusive.

**WPF — fatale False Positives:**

| Klasse | Tatsächliche Tiefe | Bemerkung |
|--------|-------------------|-----------|
| `MyWindow : Window` | 8 | Window → ContentControl → Control → FrameworkElement → UIElement → Visual → DependencyObject → DispatcherObject |
| `MyControl : UserControl` | 8 | Gleiche Tiefe |
| `MyPage : Page` | 9 | Page hat tiefere Kette |
| `MyViewModel : ObservableRecipient` | 2 | ObservableRecipient → ObservableObject — genau am Limit |
| `MyViewModel : ObservableObject` | 1 | OK |

**Fazit:** Jede WPF-UI-Klasse überschreitet MaxInheritanceDepth: 2 trivialerweise durch die Framework-Hierarchie. Das ist ein **systemischer False Positive** für WPF-Projekte.

**Blazor-Spezifika:**
- `MyComponent : ComponentBase` → Tiefe 1 (ComponentBase erbt direkt von Object) → OK
- `MyLayout : LayoutComponentBase` → Tiefe 2 (LayoutComponentBase → ComponentBase) → am Limit
- `MyAuthLayout : MyLayout` → Tiefe 3 → **Verstoß**, obwohl sinnvolle Hierarchie

**Gegenmaßnahmen:**
- `ExcludeFrameworkBaseTypes: ["Microsoft.UI.*", "System.Windows.*", "Microsoft.AspNetCore.Components.*"]` — Framework-Typen aus dem Tiefen-Zähler ausschließen
- `CountOnlyUserDefinedLevels: true` — Tiefe wird ab dem ersten user-definierten Typ gezählt (nicht Framework)
- Alternative: `IgnoreWhenBaseTypeInNamespace: ["System.Windows", "Microsoft.UI", "Microsoft.AspNetCore.Components"]`

**Empfehlung Prio:** Dies sollte als **P0** eingestuft werden — WPF-Projekte können derzeit nicht sinnvoll analysiert werden.

---

### 1.7 MaxMethodOverloads (Standard: 3)

**Was wird geprüft:** Anzahl der Methoden mit gleichem Namen in einer Klasse.

**False-Positive-Szenarien:**

| Muster | Warum kein echter Verstoß |
|--------|--------------------------|
| Repository mit `Find(id)`, `Find(spec)`, `Find(expr)`, `Find(criteria)` | 4 unterschiedliche Suchstrategien, semantisch klar |
| Extension-Method-Klassen | Viele Überladungen für verschiedene Typen ist das Muster |
| Fluent-Builder-Methoden | `Set(string)`, `Set(int)`, `Set(bool)`, `Set(enum)` — typisch |
| ASP.NET `ActionResult`-Methoden | `Ok(T)`, `Ok()`, `Ok(IEnumerable<T>)`, `Ok(PaginatedResult<T>)` |

**Gegenmaßnahmen:**
- `IgnoreInExtensionClasses: true` — Klassen, die nur `static`-Extension-Methoden enthalten, ausschließen
- `CountOnlyPublicOverloads: true` — private Überladungen nicht zählen

---

### 1.8 MaxConstructorDependencies (Standard: 5)

**Was wird geprüft:** Anzahl der Parameter im (Primary-)Konstruktor — Proxy für die Anzahl der Abhängigkeiten einer Klasse.

**False-Positive-Szenarien:**

| Muster | Warum kein echter Verstoß |
|--------|--------------------------|
| Framework-Dienste (`ILogger`, `IOptions<T>`, `IHostEnvironment`) | Cross-Cutting-Concerns, keine fachliche Abhängigkeit |
| ASP.NET-Handler mit mehreren Repositories | Orchestrator-Klassen sind per Design zentralisiert |
| Primary Constructor in Blazor-Handler | Mehrere Services sind in Blazor-Server unvermeidlich |
| EF-DbContext mit vielen DbSets | DbSets sind Datenzugriffspfade, keine Services |

**Konkrete Praxis:**
```csharp
// 7 Parameter — aber 3 davon sind Cross-Cutting
public sealed class PlannerHandler(
    ILogger<PlannerHandler> logger,      // Cross-cutting
    IOptions<PlannerOptions> options,    // Cross-cutting
    IHostEnvironment env,                // Cross-cutting
    IPlannerRepository repo,
    INotificationService notify,
    ICalendarService calendar,
    IAuditService audit)
```

**Gegenmaßnahmen:**
- `IgnoreFrameworkTypes: ["ILogger", "ILogger<>", "IOptions", "IOptions<>", "IHostEnvironment", "IConfiguration"]` — bekannte Framework-Dienste nicht mitzählen
- Meldung sollte konkreten Hinweis geben: „welches Record-Parameter-Objekt macht hier Sinn" (z. B. `PlannerHandlerDependencies`-Record)

---

### 1.9 MaxDirectoryDepth (Standard: 4)

**Was wird geprüft:** Ordnertiefe ab dem `*.csproj`-Verzeichnis.

**False-Positive-Szenarien:**

| Struktur | Tiefe | Warum legitim |
|---------|-------|---------------|
| `Features/Admin/Users/Commands/CreateUser/` | 5 | Vertical-Slice-Architektur (CQRS) |
| `Components/Layout/Navigation/Desktop/` | 4 | Blazor-Komponentenhierarchie |
| `Handlers/Domains/Firmenkalender/Kalender/` | 4 | DDD Feature-Gruppen |
| `Views/Pages/Settings/Advanced/` | 4 | WPF View-Organisation |
| `Infrastructure/Persistence/Repositories/Tenant/` | 4 | Clean-Architecture-Schichten |

**Gegenmaßnahmen:**
- `ExcludePathPrefixes: ["Features/", "Handlers/Domains/"]` — bekannte tiefe Strukturen ausschließen
- `Mode: warn-only` — Tiefe meldet als Warning, nicht Error, da es oft Architektur-Entscheidung ist

---

### 1.10 MaxAIContextFootprint (Standard: 5000)

**Was wird geprüft:** Transitive Codezeilen aller Klassen, von denen eine Klasse abhängt — misst den „Aufmerksamkeitsradius" einer Klasse für LLM-Agenten.

**False-Positive-Szenarien:**

| Muster | Warum kein echter Verstoß |
|--------|--------------------------|
| Service-Aggregator/Orchestrator | Per Design viele Abhängigkeiten — kein schlechtes Design |
| `Program.cs` / Composition-Root | Referenziert bewusst alles — genau die richtige Stelle |
| Repository mit vielen Entitäten | Datenzugriffs-Layer ist zentralisiert |
| Großer DbContext | Listet alle DbSets auf — strukturell notwendig |

**Gegenmaßnahmen:**
- `ExcludeCompositionRootFiles: ["Program.cs", "*Startup.cs", "*ServiceExtensions.cs"]` — bekannte Composition-Root-Dateien ausschließen
- Footprint-Report mit Top-3-Abhängigkeiten ist bereits gut — Guidance sollte konkret die größten Treiber benennen (bereits implementiert)

---

## Kapitel 2 — Globale Qualitätsregeln (Standard aktiv)

### 2.1 EnforceSealedClasses

**Was wird geprüft:** Konkrete Klassen müssen `sealed` sein. Ausgenommen sind `abstract`-, `static`- und Partial-Klassen (wenn `AllowUnsealedPartialClasses: true`, was standardmäßig **aus** ist).

**Kritischer Fund aus dem Quellcode:**
```csharp
private bool ShouldSkipSealedCheck(ClassDeclarationSyntax node)
{
    if (!_config.Global.EnforceSealedClasses) return true;
    if (IsSealedOrStaticOrAbstract(node)) return true;

    bool isPartial = node.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));
    return isPartial && _config.Global.AllowUnsealedPartialClasses;
}
```

`AllowUnsealedPartialClasses` ist standardmäßig `false` — Partial-Klassen werden also **ebenfalls** auf `sealed` geprüft.

**WPF — False Positives:**

| Klasse | Problem |
|--------|---------|
| `public partial class MainWindow : Window` | Partial + nicht sealed → Verstoß |
| `public partial class App : Application` | Partial + nicht sealed → Verstoß |
| `public partial class UserControl1 : UserControl` | Partial + nicht sealed → Verstoß |

**Wichtig:** WPF-Code-Behind-Dateien sind **immer** `partial` (da die andere Hälfte vom XAML-Compiler generiert wird). Die technisch korrekte Lösung `public sealed partial class MainWindow : Window` ist in C# syntaktisch möglich — aber es ist ungewöhnlich und wird nicht von WPF-Projekt-Templates generiert.

**Blazor-Spezifika:**
- Blazor-Komponenten können `partial`-Klassen sein (`.razor.cs` code-behind), die dann nicht sealed sind
- Basisklassen für Layouts oder Komponenten (`MyLayoutBase : LayoutComponentBase`) müssen per Design nicht sealed sein

**Weitere legitime nicht-sealed Klassen:**

| Muster | Grund |
|--------|-------|
| `abstract class BaseHandler` | Abstrakt — korrekt ausgenommen |
| `class MyServiceBase` ohne `abstract` | Wird als Basisklasse genutzt, muss geerbt werden |
| EF-Core-Entitätshierarchien (TPH) | `BaseEntity` muss erbbar sein |
| xUnit-Test-Basisklassen | Im Testprojekt teils ausgenommen, aber nicht vollständig |

**Gegenmaßnahmen:**
- `AllowUnsealedPartialClasses: true` für WPF-Projekte (bereits als Option vorhanden)
- `ExemptBaseTypeSuffixes: ["Base", "Foundation", "Core"]` — Klassen, die als Basisklasse designt sind, anhand des Namens ausnehmen
- Oder: Prüfen ob die Klasse von einer anderen Klasse im selben Projekt beerbt wird — wenn ja, kein Verstoß

---

### 2.2 StaticTestSentinel (EnableTestSentinel)

**Was wird geprüft:** Klassen mit einer maximalen kognitiven Komplexität > `MinCognitiveComplexityForTest` (Standard: 3) müssen nachweislich durch eine Testklasse abgedeckt sein.

**Erkennungsmechanismen (aus `TestSentinel`-Config):**
- Testklasse mit Name `{Name}Tests`, `{Name}Test`, `{Name}IntegrationTests`
- `typeof(MyClass)`-Referenz in einer Testklasse
- Kommentar `// @covers MyClass`

**False-Positive-Szenarien:**

| Klasse | Warum kein echter Verstoß |
|--------|--------------------------|
| Extension-Method-Klassen (`StringExtensions`) | Immer indirekt getestet durch deren Nutzer |
| WPF-Konverter (`BoolToVisibilityConverter`) | Visuell genutzt, schwer zu unit-testen |
| Blazor-Komponenten (`MyComponent`) | Unit-Tests für Blazor-Komponenten sind selten und komplex |
| DI-/Service-Registrierungsklassen | Integrations-Scope, kein Unit-Test sinnvoll |
| Mapper-Profile (`UserProfile : Profile`) | Mapping-Tests indirekt über Service-Tests |
| Seed-/Migration-Klassen | Nicht unit-testbar |

**Gegenmaßnahmen:**
- `ExemptSuffixes: ["Extensions", "Converter", "Mapper", "Profile", "Migration", "Seed", "Constants"]` — bekannte nicht-testbare Klassen-Typen
- `RecognizeTypeofReference: true` und `RecognizeCoversComment: true` sind bereits implementiert — gut
- `ExemptWhenInheritsFrom: ["ComponentBase", "IValueConverter", "Profile"]` — Framework-spezifische Basisklassen ausnehmen

---

### 2.3 EnforcePascalCase

**Was wird geprüft:** Öffentliche Typen, Methoden, Properties müssen PascalCase haben (erster Buchstabe groß).

**False-Positive-Szenarien:**

| Muster | Warum kein echter Verstoß |
|--------|--------------------------|
| Operator-Methoden (`op_Addition`, etc.) | Compiler-generiert, kein echter Name |
| Implizite Interface-Implementierungen | Name durch Interface vorgegeben |
| Auto-generierte Klassen (Source Generator) | Naming durch Generator-Template |

**Hinweis aus Quellcode:** `_isTestFile` überspringt die Prüfung in Testdateien — das ist korrekt.

**Gegenmaßnahmen:**
- `ExcludeGeneratedCode: true` — Dateien mit `[System.CodeDom.Compiler.GeneratedCode]`-Attribut ausnehmen (bereits in vielen Projekten so gemacht)
- `ExcludeWhenOverride: true` — Methoden mit `override` ausnehmen (Signatur vorgegeben durch Basisklasse)

---

### 2.4 EnforceSemanticNaming

**Was wird geprüft:** Parameter in **öffentlichen** Methoden dürfen keine generischen Namen aus der Verbotsliste haben.

**Verbotsliste (aus Quellcode):**
```csharp
private static readonly HashSet<string> ForbiddenNames = new(StringComparer.OrdinalIgnoreCase)
{
    "data", "temp", "obj", "val", "tmp", "item", "param"
};
```

Die Prüfung gilt **nur für öffentliche Methoden** (`isPublicMethod`). Private Methoden und lokale Variablen sind ausgenommen.

**False-Positive-Szenarien:**

| Parameter | Kontext | Warum legitim |
|-----------|---------|---------------|
| `object obj` | `Equals(object obj)` — Interface-Methode | Signatur durch `object.Equals` vorgegeben |
| `T item` | `IEnumerable<T>.Add(T item)` | Generischer Name ist hier semantisch korrekt |
| `object param` | WPF `ICommand.CanExecute(object param)` | Framework-Signatur |
| `object val` | Binding-Konverter-Interface | Framework-Signatur |
| `object data` | `IDataObject.GetData(string format, object data)` | Framework-Signatur |

**Gegenmaßnahmen:**
- `IgnoreWhenImplementingInterface: true` — Methoden, die ein Interface implementieren (`override`, explicit interface), ausnehmen
- `IgnoreWhenOverride: true` — `override`-Methoden grundsätzlich ausnehmen (Signatur nicht frei wählbar)

---

### 2.5 EnforceNullableEnable

**Was wird geprüft:** `#nullable enable`-Direktive muss am Dateianfang stehen.

**False-Positive-Szenarien:**

| Muster | Warum kein echter Verstoß |
|--------|--------------------------|
| Dateien mit projekt-weitem `<Nullable>enable</Nullable>` in `.csproj` | Direktive gilt global, kein Datei-Header nötig |
| Auto-generierte Dateien (`*.g.cs`) | Generatoren sind nicht Aufgabe des Entwicklers |
| GlobalUsings-Dateien | Keine Typen, die Nullable betreffen |

**Gegenmaßnahmen:**
- Prüfen ob das Projekt `<Nullable>enable</Nullable>` im `.csproj` setzt — wenn ja, per-Datei-Direktive nicht verlangen
- `ExcludeGeneratedFiles: true` — generierte Dateien ausnehmen

---

### 2.6 EnforceNoSilentCatch

**Was wird geprüft:** Ein Catch-Block gilt als „still" (swallowed), wenn er weder einen `throw`-Statement noch eine Methoden-Aufruf (`InvocationExpression`) enthält.

**Kritischer Fund aus Quellcode:**
```csharp
private static bool IsSwallowed(CatchClauseSyntax node)
{
    if (node.Block.Statements.Count == 0) return true;

    var hasThrow = node.Block.DescendantNodes().OfType<ThrowStatementSyntax>().Any();
    var hasInvoke = node.Block.DescendantNodes().OfType<InvocationExpressionSyntax>().Any();
    return !hasThrow && !hasInvoke;
}
```

Ein Catch-Block mit **nur Zuweisungen** wird als „still" gewertet — auch wenn er bewusst einen Fehlerzustand speichert.

**False-Positive-Szenarien:**

| Code-Muster | Warum kein echter Verstoß |
|------------|--------------------------|
| `catch (Ex ex) { _lastError = ex.Message; return false; }` | Speichert Fehler, gibt false zurück — bewusst, nicht still |
| `catch (FormatException) { return null; }` | Erwarteter Fehler im Parse-Pfad (`TryParse`-Muster) |
| `catch (FileNotFoundException) { isAvailable = false; }` | Explizite Behandlung durch Status-Setzung |
| Dispose-Cleanup-Pattern | `try { resource.Dispose(); } catch { }` — Dispose-Fehler sollen nicht propagieren |

**AllowCancellationShutdownCatch — weitere Einschränkung:**
```csharp
private bool IsAllowedCancellationCatch(CatchClauseSyntax node)
{
    // ...
    return node.Filter != null;  // Nur erlaubt wenn "when (condition)" vorhanden
}
```

Ein leerer `catch (OperationCanceledException)` ohne `when`-Filter wird **trotz** `AllowCancellationShutdownCatch: true` als Verstoß gemeldet. Das erzwingt künstlich einen `when`-Filter.

**Gegenmaßnahmen:**
- `IsSwallowed`-Logik erweitern: Catch-Blöcke mit Assignment + Return sollten **nicht** als swallowed gelten
- `AllowTryPatternReturnFalse: true` — `return false/null` in Catch ohne Methoden-Aufruf erlauben
- `AllowCancellationShutdownCatch` ohne Pflicht zum `when`-Filter korrigieren

**Suppression (bereits vorhanden):** `catch (Exception ignored)` oder `// ainetlinter-disable EnforceNoSilentCatch` — das ist der empfohlene Workaround für jetzt.

---

### 2.7 EnforceValueObjectContracts

**Was wird geprüft:** Klassen mit Suffix `ValueObject` müssen als `record` oder `readonly struct` deklariert sein und dürfen keine mutablen Properties (`set`-Accessor) haben.

**False-Positive-Szenarien:**

| Muster | Warum problematisch |
|--------|---------------------|
| `DatabaseValueObject` (EF-Core-Konfiguration) | Hat `set`-Properties für EF-Binding |
| Legacy-Code mit `*ValueObject`-Namen | Alt, noch nicht migriert |

**Einschätzung:** Das FP-Risiko ist hier niedrig, da der Suffix `ValueObject` in der Praxis selten ohne Absicht verwendet wird. Kein sofortiger Handlungsbedarf.

**Gegenmaßnahme:**
- `ExemptSuffixes: ["*"]` im Override für Projekte, die das Muster noch nicht durchgehend nutzen

---

## Kapitel 3 — Regeln im Strict-Profil (in platform-default deaktiviert)

Diese Regeln sind im Standard-`rules.json` **deaktiviert**, können aber in einem Strict-Profil (z. B. `platform-ai-strict.rules.json`) aktiv sein. Der Consumer-Wunschliste-Bericht (`Todo\2026-06-14-Consumer-Wunschliste-SanSmartPlannerPlatform-Strict-Audit.md`) dokumentiert 1.398 Verstöße in einem realen Projekt — 74 % davon durch `EnforceNoMagicValues` allein.

---

### 3.1 EnforceNoMagicValues

**Was wird geprüft (Ist-Stand):** Jedes String- und Numeric-Literal in einem Methoden-Body ist „magic", außer `""`, `0`, `1`, `-1`.

**Bewertung:** Diese Regel ist in der aktuellen Form **der größte False-Positive-Erzeuger im gesamten Strict-Stack** — 1.033 von 1.398 Strict-Verstößen in einem realen Projekt.

**False-Positive-Kategorien:**

| Kategorie | Beispiele | Warum kein Magic Value |
|-----------|-----------|----------------------|
| Nutzer-/Fehlertexte | `"Der Benutzername ist ungültig."` | Text *ist* die Semantik |
| RFC 9457 / ProblemDetails | `title: "Conflict"`, `type: "not-found"` | Protokoll-Definition |
| Minimal-API-Routen | `"/data/{site}/{id}"` | Routing-Deklaration |
| Serilog-Templates | `"Aktive Sitzungen: {Count} für {User}"` | Structured Logging |
| JSON/Metadata-Keys | `"sqlFile"`, `"rowVersion"`, `"columns"` | Schema-Definition |
| OAuth/Form-Felder | `"grant_type"`, `"password"`, `"client_id"` | Protokoll-Spezifikation |
| Format-Strings | `"N2"`, `"yyyy-MM-dd"`, `"HH:mm"` | Darstellungskonstanten |
| CSS-/UI-Klassen | `"site-ui-datatable-align-right"` | Styling-Referenz |
| Routen-Kurzpfade | `"/"`, `"/login"`, `"/api"` | Selbsterklärend |
| SQL-Parser-Lexeme | `"ORDER"`, `"BY"`, `"WHERE"` | Fachliche Token |
| Separator-Strings | `", "`, `"\n"`, `"\r\n"` | Formatierungs-Idiom |

**Gegenmaßnahmen** (Detail-Vorschläge im Consumer-Wunschliste-Dokument):

Konfigurierbare Profile:
```json
"MagicValues": {
  "Mode": "numeric-and-threshold",
  "MinStringLength": 3,
  "IgnoreStringPatterns": ["^/[\\w/{}/-]+$"],
  "IgnoreWhenParentInvocationContains": [
    "Log.", "MapGet", "MapPost", "GetSection", "TypedResults.Problem"
  ],
  "IgnoreAttributeArguments": true,
  "IgnoreCollectionExpressions": true
}
```

---

### 3.2 EnforceResultPatternOverExceptions

**Was wird geprüft:** Jedes `throw`-Statement außerhalb von Konstruktoren oder Methoden mit Suffix `Guard`/`Validate` ist ein Verstoß.

**False-Positive-Szenarien:**

| Kontext | Beispiel | Warum legitim |
|---------|---------|---------------|
| Infrastruktur-Fehler | `throw new InvalidOperationException("no connection string")` | Nicht-fachlicher, fataler Fehler |
| ASP.NET-Middleware | `throw new UnauthorizedAccessException()` | HTTP-Pipeline-Konvention |
| Factory-Methoden | `throw new ArgumentException(...)` | Eingabe-Validierung — Standard-C#-Idiom |
| `async`/`await`-Rethrow | `throw;` in Catch | Kein neuer Fehlerfluss — Weiterleitung |
| Infrastruktur-Endpoints | `throw` in `*Endpoints.cs` | Nicht Domänenschicht |

**Gegenmaßnahmen:**
```json
"ResultPattern": {
  "Mode": "suggest",
  "AllowThrowIn": ["Infrastructure", "Program", "*Endpoints", "*Middleware"],
  "AllowInCatchRethrow": true
}
```

---

### 3.3 EnforceExplicitStateImmutability

**Was wird geprüft:** Klassen dürfen keine mutablen Properties (`set`) oder Felder ohne `readonly` haben. Ausgenommen sind Klassen mit Suffixen `Dto`, `Entity`, `Model`, `Request`, `Response`, `Command` sowie Klassen mit bestimmten Attributen.

**False-Positive-Szenarien:**

| Muster | Warum legitim |
|--------|---------------|
| Blazor-Komponenten mit State | `_isLoading`, `_cachedResult` — Komponenten-Lebenszyklus |
| Blazor `@inject`-Properties | `[Inject] private IService _service` — DI-Pattern |
| WPF-ViewModel-Properties mit `OnPropertyChanged` | MVVM-Pattern erfordert mutable Properties |
| Background-Services | `_cancellationTokenSource`, `_isRunning` — Lifecycle-State |
| SignalR-Hub-Clients | Verbindungs-State muss mutierbar sein |
| Cache-Klassen | `_cachedValue`, `_expiresAt` — Cache ist per Definition mutable |

**WPF-Spezifika:**
- Ein MVVM-ViewModel mit INotifyPropertyChanged **muss** mutable Properties haben — die Regel erzwingt `init`, das mit MVVM-Binding inkompatibel ist.
- `{ get; set; }` mit `RaisePropertyChanged` ist der Standard — nicht vermeidbar ohne Framework-Änderung.

**Gegenmaßnahmen:**
```json
"Immutability": {
  "AllowMutableFieldsIn": ["*Component", "*Provider", "*Store", "*ViewModel", "*HubClient"],
  "ExemptBaseTypes": ["ComponentBase", "BackgroundService", "INotifyPropertyChanged"],
  "AllowBlazorInjectFields": true
}
```

---

### 3.4 EnforceNamespaceDirectoryMapping

**Was wird geprüft:** Der Namespace muss auf den physischen Ordnerpfad (ab csproj) enden.

**Implementierung:** `declaredNamespace.EndsWith(expectedSuffix)` — exaktes Suffix-Match.

**False-Positive-Szenarien:**

| Ordner-Pfad | Namespace | Problem |
|-------------|-----------|---------|
| `Handlers/Domains/Firmenkalender/` | `MyApp.Handlers.Kalender` | Ordner tiefer als Namespace |
| `Features/Admin/Users/Commands/` | `MyApp.Features.Users.Commands` | `Admin`-Segment im Namespace weggelassen |
| `src/Core/` | `MyApp.Core` | `src`-Prefix im Pfad, nicht im Namespace |
| `Components/Shared/` | `MyApp.Shared` | `Components`-Segment in Namespace nicht vorhanden |

**Feature-Folder-Strategie** (Vertical Slice): Namespaces sind bewusst flacher als die Ordnerstruktur — beides ist eine legitime Konvention.

**Gegenmaßnahmen:**
```json
"NamespaceDirectoryMapping": {
  "Mode": "segment-count",
  "RequiredTrailingSegments": 2,
  "IgnorePathPrefixes": ["src/", "Source/", "Handlers/Domains/"]
}
```

---

### 3.5 EnforceStrictBoundaryForBusinessLogic

**Was wird geprüft:** Business-Logic darf keinen direkten I/O (Datenbankzugriff, HTTP-Calls, Dateizugriff) enthalten.

**False-Positive-Szenarien:**

| Muster | Warum schwierig |
|--------|----------------|
| Rich-Domain-Services mit Repository-Calls | Hängt von DDD-Stil ab — kann legitim sein |
| Application-Services (Use-Cases) | Koordinieren I/O per Definition |
| Blazor-Components mit direktem Service-Call | Nicht immer möglich, Command/Query-Bus einzuführen |
| WPF-ViewModels mit Repository-Calls | MVVM-Simplistik ohne Mediator |

**Gegenmaßnahmen:**
- Namespace-basierte Ausnahme: `AllowInNamespaces: ["*.Application", "*.UseCases"]` — Application-Layer darf I/O aufrufen

---

### 3.6 DetectAndBanPhantomDependencies

**Was wird geprüft:** `using`-Direktiven, die nicht aufgelöst werden können (kein Symbol im Semantic Model) und Reflection-Calls (`Type.GetType`, `Assembly.Load`, `Activator.CreateInstance`).

**False-Positive-Szenarien:**

| Muster | Warum kein echter Verstoß |
|--------|--------------------------|
| Conditional `using` mit `#if NETX` | Symbol in aktueller Target-Framework-Konfiguration nicht aufgelöst |
| `using static`-Direktiven | Syntax wird manchmal nicht vollständig aufgelöst |
| Globale Using-Dateien mit internen Namespaces | Wenn Solution-Ladung partiell |
| `typeof(ExternalType)` für Serialisierung | `typeof` ist keine Reflection-Call — aber `Type.GetType(string)` schon |

**Gegenmaßnahmen:**
- `AllowConditionalUsings: true` — `using`-Direktiven unter `#if`-Präprozessor-Bedingungen ausnehmen

---

### 3.7 PreventContextDependentOverloads

**Was wird geprüft:** Methoden-Überladungen, die sich nur in primitiven Typen unterscheiden (z. B. `Process(int)` und `Process(string)`).

**False-Positive-Szenarien:**

| Muster | Warum legitim |
|--------|---------------|
| `Parse(string)` und `Parse(ReadOnlySpan<char>)` | Performance-Überladung — Standard-Pattern |
| `Add(int)` und `Add(long)` in Math-Utility | Numerische Genauigkeit erfordert Trennung |
| `Find(string id)` und `Find(Guid id)` | Verschiedene ID-Typen |

**Gegenmaßnahmen:**
- `ExcludeReadOnlySpanOverloads: true` — `ReadOnlySpan<char>`-Überladungen sind Performance-Optimierung, keine Ambiguität

---

### 3.8 RequireExplicitTruncationHandling

**Was wird geprüft:** Asynchrone Calls auf `HttpClient`, `TextReader`, `BinaryReader` etc., deren Ergebnisse nicht vollständig verarbeitet werden (Truncation-Erkennung für LLM-Agenten).

**Stand:** Seit 1.0.23 funktioniert die Erkennung für `await`-Ausdrücke korrekt (Fix aus dem Release-Commit bestätigt). Bekannte False Positives aus früheren Versionen sind behoben.

**Mögliche verbleibende False Positives:**

| Muster | Beschreibung |
|--------|-------------|
| Ergebnis wird an Methode weitergegeben | `ProcessResponse(await client.GetAsync(...))` — Truncation-Check trifft nicht zu wenn Ergebnis direkt verarbeitet wird |
| Chained-Calls | `(await client.GetAsync(...)).EnsureSuccessStatusCode()` — Ergebnis wird sofort genutzt |

**Gegenmaßnahmen:**
- Erkennung verbessern: Wenn der await-Ausdruck direkt als Argument einer weiteren Methode oder Property-Access verwendet wird, nicht als Truncation melden

---

### 3.9 AllowedEmptyReads

**Was wird geprüft:** Leere Leseoperationen (Read-Calls die kein Ergebnis nutzen).

**Status:** Standardmäßig deaktiviert (`false`). Kein bekanntes FP-Profil, da die Regel selten aktiviert wird. Dokumentation ausstehend bis zur ersten Aktivierung in einem Strict-Profil.

---

## Kapitel 4 — Zusammenfassung und Priorisierung

### 4.1 Nach Dringlichkeit

| Prio | Regel | Problem | Empfehlung |
|------|-------|---------|------------|
| **P0** | `MaxInheritanceDepth` | Jede WPF-UI-Klasse überschreitet Limit — systemischer False Positive | `CountOnlyUserDefinedLevels` oder Framework-Typ-Whitelist |
| **P0** | `EnforceNoMagicValues` (Strict) | 74 % aller Strict-Verstöße — Noise dominiert das Signal | Konfigurierbare Profile/Ignore-Kontexte |
| **P1** | `EnforceSealedClasses` | WPF Partial-Klassen immer Verstoß; `AllowUnsealedPartialClasses` muss per Projekt konfigurierbar | Projekt-Override-Dokumentation schärfen |
| **P1** | `EnforceExplicitStateImmutability` (Strict) | WPF-MVVM und Blazor-Komponenten unvereinbar mit strikter Immutability | Framework-spezifische Exemptions |
| **P1** | `EnforceNoSilentCatch` | Catch mit Assignment ohne Invocation falsch als „still" gewertet | `IsSwallowed`-Logik: Assignment + Return erlauben |
| **P2** | `MaxCyclomaticComplexity` / `MaxCognitiveComplexity` | Switch-Dispatcher und Guard-Clause-Ketten | `ExcludeSwitchDispatcherCases`, `ComplexityNearMissTolerance` |
| **P2** | `MaxConstructorDependencies` | Framework-Dienste (`ILogger`, `IOptions`) erhöhen Zähler unnötig | `IgnoreFrameworkTypes`-Liste |
| **P2** | `EnforceNamespaceDirectoryMapping` (Strict) | Feature-Folder-Strategien nicht kompatibel mit exaktem Match | `Mode: segment-count` |
| **P3** | `StaticTestSentinel` | Extension-Klassen, Konverter, Blazor-Komponenten ohne Test | `ExemptSuffixes`-Liste |
| **P3** | `EnforceResultPatternOverExceptions` (Strict) | Infrastruktur-Throws als Fehler gemeldet | `AllowThrowIn`-Namespaces |

### 4.2 Für Tests

Zu jedem korrigierten False Positive sollte ein Test-Fixture erstellt werden:

- WPF-Fixture: Klasse die von `UserControl` erbt → `MaxInheritanceDepth` sollte **nicht** feuern nach Fix
- Partial-WPF-Fixture: `public partial class MyWindow : Window` → `EnforceSealedClasses` sollte **nicht** feuern wenn `AllowUnsealedPartialClasses: true`
- Silent-Catch-Fixture: `catch (Ex ex) { _state = false; return; }` → `EnforceNoSilentCatch` sollte **nicht** feuern
- Switch-Dispatcher-Fixture: 10-Case-Dispatcher ohne Inline-Logik → Komplexitätsregeln sollten **nicht** feuern
- MagicValues-Fixture: Log-Template, Route, JSON-Key → `EnforceNoMagicValues` sollte **nicht** feuern nach Konfiguration
