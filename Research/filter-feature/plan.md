# Implementierungsplan: Globales Projekt- & Namespace-Filtering

Dieses Dokument beschreibt das Konzept, die Architektur und die konkreten Schritte zur Einführung globaler Filter-Parameter in `AiNetLinter`. Die Filter dienen der gezielten Eingrenzung der zu analysierenden und zu kartografierenden Code-Dateien.

---

## 1. Intention & Ziele

Bei großen Software-Systemen (Enterprise-Solutions) führt eine vollständige Analyse zu zwei Problemen:
1. **Kontext-Überlastung für LLMs:** Skeleton Maps oder Reports werden zu groß (z. B. >1 MB), um sie vollständig in den Kontext von LLM-Agenten (wie Cursor oder Claude) zu laden.
2. **Performance & CI-Zeiten:** Das Linten von hundert Projekten dauert länger als das Linten eines einzelnen geänderten Projekts.

### Kernziele:
* **Selektives Linten & Mapping:** Eingrenzung der Analyse auf Projekt-Ebene (Assembly) oder Namespace-Ebene über CLI-Argumente.
* **Separation von Test- und Produktivcode:** Getrennte Analyse von Test- und Produktivprojekten (z. B. via `--exclude-tests` or `--tests-only`).
* **Globale Gültigkeit:** Die Filter greifen einheitlich in allen Modi (Linten, Skeleton Maps, Debt Reports, Playbooks, Impact-Analyse).
* **Agenten-Entdeckbarkeit:** Die CLI-Optionen müssen sich über `--help` selbst erklären, damit autonome Agenten sie eigenständig entdecken und nutzen können.

---

## 2. Architektur & Datenfluss

Um Redundanzen zu vermeiden, werden die Filter an der Quelle (beim Auflösen der Quellcode-Dateien im Katalog) oder zentral in der Ausführungsebene (Engine/Walker) angewendet.

```
       [ CLI / LinterArgs ]
               │
               ▼
     [ SourceFileCatalog ] ──(Projekt-Filter & Test-Filter)──► Gefilterte Document-Liste
               │
               ▼
       [ LinterEngine ] ───(Namespace-Filter)───────────────► Analyse & Verstöße
               │
               ▼
    [ MapCommand / Walker ] ──(Namespace- & Visibility-Filter)► Skeleton Map
```

* **Projekt- und Testfilter:** Werden auf Ebene des `SourceFileCatalog` angewendet. Dokumente aus ausgeschlossenen Projekten werden gar nicht erst eingelesen oder zur Analyse weitergegeben.
* **Namespace-Filter:** Werden auf Ebene der AST-Walker (`LinterAnalyzer` und `SkeletonSyntaxWalker`) angewendet, da Namespaces erst nach dem Parsen des Syntaxbaums präzise bestimmt werden können.
* **Sichtbarkeits-Filter (Visibility):** Wird im `SkeletonSyntaxWalker` angewendet, um private Implementierungsdetails optional auszublenden.

---

## 3. Konkrete Code-Anpassungen

### 3.1 CLI-Argumente & Konfiguration (`LinterArgs`)

In [LinterArgs.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Cli/LinterArgs.cs) werden die neuen Parameter hinzugefügt:

```csharp
public IReadOnlyList<string> IncludeProjects { get; init; } = Array.Empty<string>();
public IReadOnlyList<string> ExcludeProjects { get; init; } = Array.Empty<string>();
public IReadOnlyList<string> IncludeNamespaces { get; init; } = Array.Empty<string>();
public IReadOnlyList<string> ExcludeNamespaces { get; init; } = Array.Empty<string>();
public bool ExcludeTests { get; init; }
public bool TestsOnly { get; init; }
public bool PublicOnly { get; init; }
```

In [CliOptionFactory.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Cli/CliOptionFactory.cs) werden die Befehlszeilen-Optionen definiert. Jede Option erhält eine ausführliche, agenten-freundliche Beschreibung:

```csharp
internal static Option<string[]> CreateIncludeProjectOption() =>
    new("--project", "Filtert die Analyse auf bestimmte Projektnamen (kommagetrennt, Glob-Muster erlaubt, z. B. '*.Core,*.Domain').");

internal static Option<string[]> CreateExcludeProjectOption() =>
    new("--exclude-project", "Schließt bestimmte Projekte von der Analyse aus (kommagetrennt, Glob-Muster erlaubt, z. B. '*.Tests').");

internal static Option<string[]> CreateIncludeNamespaceOption() =>
    new("--namespace", "Filtert die Analyse auf bestimmte C#-Namespaces (kommagetrennt, Glob-Muster erlaubt, z. B. 'San.Auth*').");

internal static Option<string[]> CreateExcludeNamespaceOption() =>
    new("--exclude-namespace", "Schließt bestimmte Namespaces aus (kommagetrennt, Glob-Muster erlaubt, z. B. '*.Internal').");

internal static Option<bool> CreateExcludeTestsOption() =>
    new("--exclude-tests", "Shortcut, um alle automatisch erkannten Testprojekte auszublenden.");

internal static Option<bool> CreateTestsOnlyOption() =>
    new("--tests-only", "Shortcut, um ausschließlich Testprojekte zu analysieren.");

internal static Option<bool> CreatePublicOnlyOption() =>
    new("--public-only", "Blendet private und protected Member in Maps (wie skeleton) aus, um Token zu sparen.");
```

*Hinweis:* Die Optionen werden in [CliCommandBuilder.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Cli/CliCommandBuilder.cs) registriert und in `LinterArgs` gemappt.

---

### 3.2 Projekt- und Testfilterung in `SourceFileCatalog`

In [SourceFileCatalog.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Baseline/SourceFileCatalog.cs) werden die Filter angewendet:

1. **Glob-Abgleich:** Wir nutzen die bestehende Glob-Logik aus `ProjectConfigResolver.MatchesGlobPath` (oder lagern sie in eine Hilfsklasse `GlobMatcher` aus), um Projektnamen zu filtern.
2. **Erkennung von Testprojekten:** Wir nutzen `TestProjectDetector.IsTestProject(project, config.TestSentinel.TestProjectNameSuffixes)`.
3. **Filterung der Projekt-Liste:**
   In `CollectDocumentWorkItemsAsync` und `GetSourceFiles` filtern wir die Projekte der geladenen Mappe:

```csharp
private static bool ShouldIncludeProject(Project project, LinterArgs args, Config config)
{
    var isTest = TestProjectDetector.IsTestProject(project, config.TestSentinel.TestProjectNameSuffixes);
    
    if (args.ExcludeTests && isTest) return false;
    if (args.TestsOnly && !isTest) return false;

    if (args.IncludeProjects.Any() && !args.IncludeProjects.Any(p => MatchesGlob(project.Name, p)))
        return false;

    if (args.ExcludeProjects.Any() && args.ExcludeProjects.Any(p => MatchesGlob(project.Name, p)))
        return false;

    return true;
}
```

---

### 3.3 Namespace-Filterung in den Walkers & Analyzers

Da Namespaces Teil der AST-Struktur sind, filtern wir Typdeklarationen dynamisch während des Syntax-Scans.

#### 1. Namespace-Abgleich-Helfer:
Wir schreiben eine Hilfsklasse `NamespaceFilter`:
```csharp
internal static class NamespaceFilter
{
    public static bool IsNamespaceAllowed(
        string ns,
        IReadOnlyList<string> includes,
        IReadOnlyList<string> excludes)
    {
        if (includes.Any() && !includes.Any(p => MatchesGlob(ns, p)))
            return false;

        if (excludes.Any() && excludes.Any(p => MatchesGlob(ns, p)))
            return false;

        return true;
    }
}
```

#### 2. Anwendung in `SkeletonSyntaxWalker.cs`:
In `VisitClassDeclaration`, `VisitRecordDeclaration` etc. prüfen wir den aktuellen Namespace:
```csharp
if (!NamespaceFilter.IsNamespaceAllowed(_currentNamespace, _includeNamespaces, _excludeNamespaces))
    return; // Komplett überspringen
```

#### 3. Anwendung in `LinterAnalyzer.cs`:
Im Linter-Walker prüfen wir bei Typ-Deklarationen oder vor dem Hinzufügen von Verstößen, ob der Namespace des Typs erlaubt ist. Wenn nicht, werden keine Verstöße für diesen Typ registriert.

---

### 3.4 Sichtbarkeits-Filter (`--public-only`)

Im [SkeletonSyntaxWalker.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Maps/Skeleton/SkeletonSyntaxWalker.cs) werten wir `args.PublicOnly` aus.
Wenn `PublicOnly` active ist:
* **Private und Protected Member ignorieren:** In `ExtractMembers` überspringen wir alle Felder und Methoden, die nicht `public` oder `internal` deklariert sind.
* *Ausnahme:* Interface-Member und explizite Implementierungen bleiben sichtbar.

---

## 4. Agent-Entdeckbarkeit & Dokumentation

Ein KI-Agent, der das Tool zum ersten Mal sieht, nutzt oft Entdeckungsbefehle. Damit die neuen Parameter verstanden werden, passen wir folgendes an:

1. **CLI Help:** Umfassende Beschreibungen in `CliOptionFactory.cs` (siehe oben).
2. **Dokumentation:**
   * Ergänzung der CLI-Flag-Tabelle in [Docs/agent-api.md](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/Docs/agent-api.md).
   * Neues Kapitel "Eingrenzung des Analyse-Scopes (Filtering)" in [Docs/configuration.md](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/Docs/configuration.md) mit Beispielen für RAG-Optimierung.

---

## 5. Verifizierungs- & Testplan

Wir sichern das Feature durch Tests auf allen Ebenen ab:

### 5.1 Unit-Tests
* **`SourceFileCatalogTests`:**
  * Test: `CollectDocuments_WithExcludeTests_ExcludesTestProjects`
  * Test: `CollectDocuments_WithIncludeProject_FiltersCorrectly`
* **`NamespaceFilterTests`:**
  * Test: `IsNamespaceAllowed_WithWildcards_MatchesCorrectly`
* **`SkeletonSyntaxWalkerTests`:**
  * Test: `Walk_WithNamespaceFilter_IgnoresExcludedNamespaces`
  * Test: `Walk_WithPublicOnly_ExcludesPrivateMembers`
* **`LinterAnalyzerTests`:**
  * Test: `Analyze_WithNamespaceFilter_DoesNotReportViolationsInExcludedNamespaces`

### 5.2 Manuelle & Integrations-Verifikation
Wir führen das Tool lokal über PowerShell auf der eigenen Mappe aus, um die korrekte Filterung zu validieren:

```powershell
# 1. Nur Core-Projekt skeletonisieren:
.\src\AiNetLinter\bin\Debug\net10.0\AiNetLinter.exe --map skeleton --path .\AiNetLinter.slnx --project "*.Core"

# 2. Linten unter Ausschluss aller Test-Projekte:
.\src\AiNetLinter\bin\Debug\net10.0\AiNetLinter.exe --config rules.json --path .\AiNetLinter.slnx --exclude-tests
```
