---
type: konzept-vorstufe
status: draft
depends_on: tasks/codegraph-mcp-server   # muss abgeschlossen sein, bevor hieraus ein Task wird
last_updated: 2026-08-01
---

# AiNetLinter MCP Codegraph Server — P2-Backlog („später")

Reiner Backlog für Fähigkeiten, die echten Mehrwert hätten, ohne die aber
kein Schaden entsteht — bewusst niedrigere Priorität als der laufende Task.

## 0. Lesehinweis

Dieses Dokument ist die entschlankte Fassung einer früheren, umfassenderen
Konzept-Verfeinerung. Alle P0- und P1-Punkte der Vorfassung (Trunkierung,
Textformat, Regel-ID in `get_violations`, sichtbare neue/gelöschte
`.cs`-Dateien, `rules.json`-Auto-Discovery, Kaltstart-Entkopplung,
Staleness-Sweep-Optimierung, stdout-Schutz, Last-Fixture, Call-Log,
Registrierungsempfehlung) wurden am 2026-08-01 als bereits entschieden,
unstrittig ins verbindliche Scope von
[`tasks/codegraph-mcp-server/konzept.md`](../codegraph-mcp-server/konzept.md)
übernommen — dort unter „Erweiterungen ins Scope" nachlesbar, inkl.
Begründung und Belegstellen. Ebenso die dortige Tabelle „Bewusst gestrichen"
(Ideen, die geprüft und verworfen wurden) — siehe
`../codegraph-mcp-server/konzept.md` Abschnitt „Verworfene Alternativen".

Übrig bleiben hier ausschließlich die **P2-Punkte**: echter Mehrwert, aber
ohne entsteht kein Schaden — daher bewusst zurückgestellt, bis der laufende
Task (inkl. der übernommenen P0/P1-Erweiterungen) abgeschlossen ist.

## 1. P2-1 · `get_symbol_body` + stabile Symbol-IDs — **ein** System, nicht zwei Features

`get_symbol_body` (Serena-Vorbild) und stabile `DocumentationCommentId`s
ergeben nur zusammen Sinn — und zusammen sind sie der größte verbleibende
Token-Hebel in diesem Backlog:

1. Der Agent holt `get_file_skeleton` — Signaturen ohne Bodies, günstig.
2. Das Skelett liefert pro Member die stabile Symbol-ID gleich mit.
3. Der Agent holt mit genau einer weiteren Abfrage die 15 Zeilen Body, die er
   wirklich braucht — statt einer 500-Zeilen-Datei.

Die stabile ID trägt zwei Lasten gleichzeitig: sie überlebt Zeilenverschiebungen
durch die eigenen Edits des Agenten, und sie disambiguiert Overloads
(`ProcessOrder(int)` vs. `ProcessOrder(OrderDto)`) ohne Ratespiel.

**Umsetzungsempfehlung.**
- Zuerst `get_file_skeleton` um die ID pro Member erweitern (kleiner Eingriff,
  sofort nützlich, auch ohne das neue Tool).
- Dann `get_symbol_body`, das **beide** Identifikator-Formen akzeptiert: die
  stabile ID *und* das bereits etablierte `Datei:Zeile:Spalte`-Format, das
  `SymbolIdentifierResolver` heute schon auflöst. Der bestehende Resolver ist
  die richtige Stelle für die zusätzliche ID-Form — kein zweiter Auflösungsweg.
- Ausgabe hart begrenzen (siehe `maxResults`-Mechanik im Hauptkonzept); ein
  Body kann eine 800-Zeilen-Methode sein, und genau die will man nicht
  ungefiltert im Context haben.

**Basis:** `DocumentationCommentId.CreateDeclarationId`/
`GetFirstSymbolForDeclarationId` (Microsoft.CodeAnalysis), siehe Referenzen.

## 2. P2-2 · Blast-Radius als `depth`-Parameter, nicht als neues Tool

Transitive Auswirkungsanalyse ist wertvoll („wenn ich diese Signatur ändere,
was bricht über N Ebenen?"). Sie gehört aber als optionaler `depth`-Parameter
an `find_references`/`get_impact` — **nicht** als zusätzliches Tool. Je mehr
ähnliche Tools ein Server anbietet, desto häufiger greift das LLM zum
falschen; ein Parameter mehr ist billiger als ein Tool mehr.

**Umsetzungsempfehlung.**
- Default `depth = 1` (heutiges Verhalten, keine Verhaltensänderung),
  Obergrenze fest verdrahtet (z. B. 3) statt frei wählbar.
- Zusätzlich ein Knotenlimit, unabhängig von `maxResults` — transitive Suche
  kann exponentiell wachsen, bevor überhaupt formatiert wird.
- **Ab `depth > 1` aggregiert ausgeben**, nicht flach: „37 Aufrufer in 12
  Dateien, davon 9 in 3 anderen Projekten", danach die Top-N nach Betroffenheit.
  Eine flache Liste bei Tiefe 3 ist der nächste Token-Brand.

## 3. P2-3 · DI-Registrierung als Zusatzzeile in `get_type_hierarchy`

„Welche konkrete Klasse steckt hinter `IFoo`?" ist zu ~80 % bereits von
`get_type_hierarchy` beantwortet (`FindImplementationsAsync` wird dort schon
genutzt). Der fehlende Teil ist die DI-Registrierung — und der ist **keine
Roslyn-Hierarchiefrage**, sondern eine Textsuche nach
`AddScoped<IFoo`/`AddSingleton<IFoo`/`AddTransient<IFoo`.

**Umsetzungsempfehlung.** Kein eigenes Tool. `get_type_hierarchy` hängt bei
Interfaces eine Zeile an, sofern eine Registrierung gefunden wurde („Registriert
in `Program.cs:42` als Scoped"). Als reine Textsuche implementiert, mit klarer
Kennzeichnung, dass es sich um einen heuristischen Fund handelt — Factory-
Registrierungen und Convention-based-Scanning erkennt das bewusst nicht.

## 4. Referenzen

### Microsoft .NET / Roslyn Compiler API

1. **`DocumentationCommentId.CreateDeclarationId`** — erzeugt den eindeutigen
   XML-Signatur-String für ein `ISymbol` (Basis für P2-1):
   https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.documentationcommentid.createdeclarationid
2. **`DocumentationCommentId.GetFirstSymbolForDeclarationId`** — löst eine
   Symbol-ID deterministisch gegen eine `Compilation` auf:
   https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.documentationcommentid.getfirstsymbolfordeclarationid
3. **`SymbolFinder.FindImplementationsAsync`** — Interface → konkrete
   Implementierungen (bereits in `get_type_hierarchy` genutzt, Kontext für P2-3):
   https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.findsymbols.symbolfinder

### Forschung & vergleichbare Implementierungen

4. **„RepoGraph: Repository-Level Code Graph for AI Software Engineering"
   (ICLR 2025)** — deterministisches Context-Engineering über Code Property
   Graphs schlägt reines Modell-Scaling auf SWE-bench:
   https://arxiv.org/abs/2410.02678
5. **Sourcegraph SCIP** — Standard für symbolbasierte Navigation und eindeutige
   Symbol-Bezeichner; konzeptioneller Nachbar zu P2-1:
   https://github.com/sourcegraph/scip
6. **Serena** — Vorbild für Symbol-Level Body Reading (P2-1).
7. **kirograph** — Vorbild für Blast-Radius-Traversal (P2-2).
8. **coa-codenav-mcp** — Roslyn-basierter MCP-Server, Call Hierarchy und
   C#-Inheritance-Navigation.

> ⚠ **Hinweis zu 6-8:** Die Repository-URLs der Vorfassung waren nicht
> verifiziert; mindestens eine sah falsch aus. Die Projektnamen stimmen — vor
> Verwendung als Referenz die tatsächlichen Repository-Pfade einmal
> nachschlagen, statt alte URLs weiterzutragen.
