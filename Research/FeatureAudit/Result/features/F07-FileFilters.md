# FileFilters (F07)

**Kategorie:** Konfigurationsfeature  
**CLI-Flag / Konfiguration:** `rules.json → FileFilters`  
**Status:** Vorhanden

---

## Bewertung

🟢 **WERTVOLL**

**Fazit:** FileFilters ist eine Grundvoraussetzung für die praktische Einsetzbarkeit des Linters — ohne den Ausschluss generierter Dateien (*.g.cs, AssemblyInfo.cs, obj/, bin/) würde AiNetLinter in jedem .NET-Projekt tausende Falsch-Positive für Code erzeugen, den kein Entwickler schreibt.

---

## Empfehlung

**Aktion:** Beibehalten  
**Begründung:** Der Ausschluss von Build-Artefakten (`obj/`, `bin/`), Codegenerator-Ausgaben (`*.g.cs`, `*.generated.cs`) und versionierten Assembly-Dateien (`AssemblyInfo.cs`) ist eine universelle Best Practice aller vergleichbaren Linter-Tools. Das `[GeneratedCode]`-Attribut-Matching ist eine C#-spezifische Ergänzung, die das Feature für .NET-Projekte komplett macht.

---

## Nutzen-Analyse

FileFilters deckt vier Kategorien ab:

1. **Build-Ausgabeverzeichnisse:** `obj/` und `bin/` enthalten kompilierte Artefakte und intermediäre Build-Ergebnisse. Diese zu linten wäre bedeutungslos (Entwickler können sie nicht ändern) und extrem langsam.

2. **Dateinamen-basierte Generierungs-Muster:** `*.g.cs`, `*.generated.cs` — Standardkonventionen für Roslyn Source Generators und T4-Vorlagen. Der Entwickler hat keinen direkten Einfluss auf diesen Code.

3. **Spezifische Dateien:** `AssemblyInfo.cs` ist eine Legacy-.NET-Datei, die automatisch generiert oder per MSBuild-Property verwaltet wird.

4. **Attribut-basierter Ausschluss:** Klassen mit `[System.CodeDom.Compiler.GeneratedCode]`-Attribut werden ausgeschlossen — das ist die offizielle .NET-Konvention für generierten Code und deckt auch Fälle ab, die durch Dateinamen-Muster nicht erkannt werden.

**Szenarien wo es kritisch ist:**
- Projekte mit EF Core Migrations (`*.Designer.cs`, `[GeneratedCode]`)
- Projekte mit Protobuf/gRPC (`*.pb.cs`)
- Projekte mit Swagger-generierten Clients
- Projekte mit Source Generators (z.B. `record`-Generierungen)

Ohne FileFilters würden all diese Fälle zu massiven Falsch-Positiven führen, die den Linter unbrauchbar machen.

---

## Vergleich: Andere Tools

| Tool | Generierte Dateien ausschließen | Ansatz |
|------|--------------------------------|--------|
| **ESLint** | `.eslintignore` / `ignorePatterns` | Glob-basierter Ausschluss; manuelle Konfiguration |
| **dotnet format** | Automatisch (Roslyn) | Übernimmt Roslyn-Generierungshinweise; semi-automatisch |
| **SonarQube** | Source Exclusions + `[GeneratedCode]` | Konfigurierbar; auch `[GeneratedCode]`-Unterstützung |
| **StyleCop** | `stylecop.json` ExcludedFiles | Dateinamen- und Pattern-basiert |
| **Roslyn Analyzers** | `[GeneratedCode]`-Attribut | Standard-Mechanismus; IDE-integriert |
| **AiNetLinter** | FileFilters in `rules.json` | Dateinamen + Verzeichnisse + Attribut; vollständig |

AiNetLinters Ansatz entspricht dem Industriestandard. Die Kombination aus Dateinamen-Mustern, Verzeichnis-Ausschlüssen und dem `[GeneratedCode]`-Attribut-Matching deckt alle relevanten Szenarien ab.

---

## KI-Agenten-Perspektive

FileFilters hat für LLM-Agenten eine besondere Bedeutung:

1. **Rauschunterdrückung:** Ein Agent der nach einem Code-Generierungslauf `AiNetLinter` ausführt, sollte ausschließlich Fehler in seinem eigenen Code sehen, nicht in generierten Dateien. Ohne FileFilters müsste der Agent erst herausfiltern, welche Fehler er beheben kann und welche er ignorieren soll — das ist kognitiv aufwändig und fehleranfällig.

2. **Fokussierter Kontext:** Wenn der Agent das Linter-Output als Feedback nutzt (um seinen generierten Code zu korrigieren), muss der Output präzise sein. Jeder Falsch-Positiv in generierten Dateien macht den Output weniger verlässlich.

3. **Keine Agenten-Aktion nötig:** Generierter Code kann vom Agenten nicht "gefixt" werden (er wird bei jedem Build neu generiert). Würde der Agent versuchen, ihn zu ändern, würde dies zu endlosen Zyklen führen. FileFilters verhindert dieses Szenario.

---

## Zeitliche Einordnung

**Grundlagenstabilität:** Zeitlos

Solange .NET und C#-Build-Systeme generierte Dateien erzeugen (was strukturell unvermeidbar ist), bleibt der Bedarf nach ihrem Ausschluss aus der Lint-Analyse bestehen. Das ist unabhängig von Modellgenerationen ein dauerhaftes strukturelles Erfordernis.

---

## Quellen

- Microsoft Documentation (2024) — GeneratedCode Attribute: https://learn.microsoft.com/en-us/dotnet/api/system.codedom.compiler.generatedcodeattribute
- Roslyn Source Generators (2024) — Naming Conventions: https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview
- ESLint Documentation (2024) — Ignoring Files: https://eslint.org/docs/latest/use/configure/ignore
- SonarQube Documentation (2024) — Excluding Generated Code: https://docs.sonarsource.com/sonarqube/latest/project-administration/analysis-scope/
