---
task: decompiled-assembly-analysis
type: tech-debt-log
maintained_by: kritiker
last_updated: 2026-08-29T09:01:53+02:00
---

# Tech-Debt-Log: decompiled-assembly-analysis

Append-only. Dieser Log enthält Architektur-, Anti-Pattern- und
Duplikationsbeobachtungen außerhalb des jeweiligen Step-Scopes.

## Index

| ID | Bereich / Datei | Priorität | Auto-Fixable | Kurzfassung |
|---|---|---|---|---|
| TD-001 | `ExternalSourceMappingValidator` / `SourceSnapshotIdentity` | niedrig | nein | Identische private Drive-Path-Prüfung ist über zwei Vertragsgrenzen dupliziert. |
| TD-002 | `AssemblyOrigin` / `AssemblyAnalysisContextFactory` | niedrig | nein | Origin-Kind-Werte sind als untypisierte Zeichenketten verteilt; der neue `source-backed`-Wert ist nicht zentralisiert. |
| TD-003 | `AssemblyOrigin.Kind` | niedrig | nein | Interne Alias-Property ist im statischen Lösungsscope unreferenziert; mögliche Vertrags-/Serializer-Nutzung vor Entfernung prüfen. |
| TD-004 | `AssemblyAnalysisContextFactoryTests` / `AssemblyAnalysisToolSupportTests` | niedrig | nein | Identischer privater `CreateSnapshot`-Testfixture-Builder ist über zwei Testklassen dupliziert. |
| TD-005 | `GiteaGitRepositoryTransport` / `ExternalSourceRepositoryAcquirer` | niedrig | nein | Repository-URL-Prüfung und erfolgreicher Transport-Result-Builder sind über Vertrags-/Testgrenzen dupliziert. |

## Einträge

### TD-001 — Gemeinsame Drive-Path-Prüfung prüfen [Priorität: niedrig] [Auto-Fixable: nein]

- **Gefunden in:** step-008 (Kritiker-Review vom 2026-08-28)
- **Ort:** `src/AiNetLinter/Configuration/ExternalSourceMappingValidator.cs:374-375`; `src/AiNetLinter/Mcp/Assemblies/SourceSnapshotModels.cs:157-158`
- **Befund:** Der Exact-DRY-Audit findet dieselbe private Funktion `value.Length >= 2 && char.IsLetter(value[0]) && value[1] == ':'` zweimal; jede Kopie wird jeweils einmal im eigenen Pfadnormalisierungsvertrag verwendet.
- **Warum nicht sofort gefixt:** Die Methoden liegen außerhalb des Resolvercodes und in den bereits abgeschlossenen Konfigurations- bzw. Snapshot-Identitätsverträgen. Eine gemeinsame Ablage würde mindestens beide Vertragsgrenzen und wahrscheinlich die bestehende `PathNormalizer`-API berühren.
- **Vorschlag:** Bei einer ohnehin anstehenden, vertraglich passenden Pfadnormalisierung prüfen, ob ein gemeinsamer interner Helper ohne neue öffentliche API und ohne Semantikänderung fachlich sinnvoll ist.
- **Auto-Fixable:** nein — die geeignete gemeinsame Ablage und der zulässige Vertragszuschnitt erfordern Architektur-Ermessen.
- **Status:** offen

### TD-002 — Origin-Kind-Werte typisieren oder zentralisieren [Priorität: niedrig] [Auto-Fixable: nein]

- **Gefunden in:** step-009 (Kritiker-Review vom 2026-08-28)
- **Ort:** `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextFactory.cs:112`; verwandte Werte in `src/AiNetLinter/Mcp/Assemblies/AssemblyAnalysisSession.cs:410`, `src/AiNetLinter/Mcp/Assemblies/AssemblyRoslynWorkspaceFactory.cs:47` und `src/AiNetLinter/Mcp/Assemblies/AssemblyAnalysisSessionModels.cs:30`
- **Befund:** Der Magic-Value-Audit findet den neu eingeführten Origin-Wert `"source-backed"` einmalig. Die parallele Decompilation-Kennung ist ebenfalls als Literal in mehreren Assembly-Pfaden und als Vergleich in `IsDecompiled` hinterlegt.
- **Warum nicht sofort gefixt:** Origin-Werte sind ein bestehender maschinenlesbarer/formatierter Vertrag. Eine Umstellung auf gemeinsame Konstanten oder ein Enum muss die bestehenden Wire-/Textwerte und spätere weitere Herkunftstypen berücksichtigen und gehört nicht in diesen Review.
- **Vorschlag:** Bei der nächsten Origin-Vertragserweiterung eine zentrale, typgesicherte Herkunftsrepräsentation mit unveränderten serialisierten Werten einführen.
- **Auto-Fixable:** nein — die geeignete Form und der Wire-Vertrag erfordern Architektur-Ermessen.
- **Status:** offen

### TD-003 — Unreferenzierte `AssemblyOrigin.Kind`-Property prüfen [Priorität: niedrig] [Auto-Fixable: nein]

- **Gefunden in:** step-009 (Kritiker-Review vom 2026-08-28)
- **Ort:** `src/AiNetLinter/Mcp/Assemblies/AssemblyAnalysisSessionModels.cs:28`
- **Befund:** Der Dead-Code-Audit meldet die interne Alias-Property `Kind` mit Low Confidence als unreferenziert. Die neue Herkunftsausgabe verwendet `OriginKind` beziehungsweise `IsDecompiled`; die Property ist nicht Teil der Step-009-Änderung.
- **Warum nicht sofort gefixt:** Der statische Audit kann interne Vertrags-, Serializer- oder `InternalsVisibleTo`-Nutzung nicht vollständig ausschließen. Die Property liegt außerdem außerhalb des eigentlichen Source-/Fallback-Vertrags.
- **Vorschlag:** Bei einer gezielten Origin-Modellbereinigung Referenz-/Serialisierungsbedarf bestätigen und die Property anschließend entfernen oder bewusst als Kompatibilitätsalias behalten.
- **Auto-Fixable:** nein — vor einer Entfernung ist eine Vertragsentscheidung erforderlich.
- **Status:** offen

### TD-004 — Gemeinsamen Snapshot-Testfixture-Builder prüfen [Priorität: niedrig] [Auto-Fixable: nein]

- **Gefunden in:** step-011 (Kritiker-Review vom 2026-08-28)
- **Ort:** `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextFactoryTests.cs:271-312`; `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupportTests.cs:418-459`
- **Befund:** Der begrenzte `find_duplicates`-Audit findet einen exakten 227-Token-Klon von `CreateSnapshot`, einschließlich der AdhocWorkspace-/Solution-/Project-/Document-Erzeugung und der Snapshot-Identität. Beide privaten Kopien verwenden jeweils einen eigenen verschachtelten `SourceProjectSpec`-Typ.
- **Umsetzung:** In step-013 wurde `ExternalSourceSnapshotTestFactory` als gemeinsame test-only Hilfe eingeführt; `AssemblyAnalysisContextFactoryTests` und `AssemblyAnalysisToolSupportTests` verwenden nun diese eine `CreateSnapshot`-Implementierung und die gemeinsame `ExternalSourceProjectSpec`.
- **Ownership-Nachweis:** Die betroffenen Tests behalten ihre expliziten Lease-, Registry- und Snapshot-Dispose-Aussagen; die Factory übernimmt keine zusätzliche Ownership über den erzeugten Snapshot hinaus.
- **Auto-Fixable:** nein — die gemeinsame Ablage und der Fixture-Vertrag erfordern Architektur-Ermessen.
- **Status:** erledigt in step-013 (`1cd279f0ae7a683484cd21a32157a88b84313e95`)

### TD-005 — Repository-URL-Prüfung und Result-Builder zusammenführen [Priorität: niedrig] [Auto-Fixable: nein]

- **Gefunden in:** step-019 (Kritiker-Review vom 2026-08-29)
- **Ort:** `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs:458-464`; `src/AiNetLinter/Mcp/Assemblies/GiteaGitRepositoryTransport.cs:288-296`; der identische `Success`-Builder zusätzlich in `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryAcquirerTests.cs:453-457`
- **Befund:** Der solutionweite DRY-Audit mit `minTokens=20` findet den exakten `Success`-Builder zwischen Produktions- und Testcode. Im Step-019-Produktionsscope findet der tokenbasierte Audit außerdem den nahe Klon der URL-Prüfung; der strukturelle Audit bewertet beide URL-Methoden als exakten Kandidaten. Die Implementierungen sind bereits semantisch auseinander gelaufen: Der Transport lehnt Query und Fragment ab, der Acquirer derzeit nicht.
- **Umsetzung:** In step-020 wurden eine gemeinsame interne URL-Policy mit
  explizitem Ausschluss von Userinfo, Query und Fragment sowie die gemeinsame
  `ExternalSourceRepositoryTransportResult.Success`-Fabrik eingeführt. Acquirer,
  Transport und Testverbraucher verwenden nun dieselben Verträge.
- **Auto-Fixable:** nein — die Vertragsangleichung und die zulässige gemeinsame Ablage erfordern Architektur-Ermessen.
- **Status:** erledigt in step-020 (`2c2a2c01666e370694bc78f7b748d5a988219b4e`)
