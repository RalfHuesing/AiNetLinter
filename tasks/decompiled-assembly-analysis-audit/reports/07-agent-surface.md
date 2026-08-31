# Linse 07 — Agentenoberfläche, Progressive Exploration und Token-Effizienz

- Reviewstatus: Orchestrator-Fallback; kein unabhängiger Reviewer verfügbar (`collab spawn failed: agent thread limit reached`).
- Revision: `3154c527`; Produktionsquellen blieben seit der Audit-Baseline unverändert.
- MCP-Parameter: `targetType=project`, `targetPath=<repo-root-redacted>`, Scope `src/AiNetLinter/Mcp/Assemblies`; Assembly-Probe `targetType=assembly`, `targetPath=<neutral-built-dll>`. Keine geheimen oder lokalen Umgebungswerte werden ausgegeben.

## Abdeckung

Geprüft wurden Toolbeschreibungen, Defaults, strukturierte Payloads, `completeness`-/`truncated`-Metadaten, Diagnose-Samples, Assembly-Herkunft/Generation, die progressive MCP-Arbeitsanleitung sowie die Audit-Abfragen für Duplikate, Dead Code und Magic Values.

## Befund UX-001

- Schweregrad: S2
- Umfang: U2 — Registry-/Exploration-Kontext
- Konfidenz: hoch
- Bereich: AI-Context-Footprint
- Evidenz: `get_feature_context` für `AssemblyAnalysisRegistry` meldet Type LOC `648 > 500`; die zentrale Implementierung liegt in `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisRegistry.cs:24-499` und bindet 25 Aufrufer sowie 18 zugeordnete Tests. Der Safeguard-Report bestätigt gleichzeitig einen transitive-Footprint-Hinweis im MCP-Bereich.
- Auswirkung: Zentrale Registry-Invarianten liegen über dem projektierten Agenten-Kontextbudget; bei Exploration/Refactoring steigt das Risiko, Lebenszyklus- und Ownership-Details zu übersehen. Das ist ein Wartbarkeits-/Agentenrisiko, kein Laufzeitfehler.
- Reproduktion: `get_feature_context` mit `targetType=project`, `targetPath=<repo-root-redacted>`, Symbol `AssemblyAnalysisRegistry`, `includeMetrics=true`, `includeCallers=true`, `includeTests=true` ausführen; anschließend die gemeldeten LOC-/Footprint-Metriken gegen `rules.json` lesen.
- Disposition: Als technisches Debt zurückgestellt; eine spätere, scope-nahe Aufteilung der Registry bzw. kleinere Interfaces muss Ownership- und Generation-Invarianten unverändert lassen.

## Beobachtungen mit Querverweisen

1. Die Antwortoberfläche ist grundsätzlich progressiv: Toolbeschreibungen weisen auf Scope-Verfeinerung, bounded `maxResults`, Herkunft, `generation`, `status`, `completeness`, `truncated` und nächste Schritte hin. `inspect_assembly` liefert strukturierte Parameter-/Memberdetails; `find_symbol`/`find_references` liefern Herkunft und bounded Navigation.
2. Die Default-Referenzexpansion ist jedoch mit dieser Progressionsidee inkonsistent und wird in `reports/01-assembly.md` als ASM-001 behandelt. Der Wire-Duplikationsbefund ist in `reports/06-mcp-contracts.md` als MCP-001 behandelt.
3. `find_duplicates` meldete drei Kandidatencluster: zwei ähnliche Metadata-Reader in `AssemblyReferenceResolver`, zwei transportbezogene Methoden in Acquirer/Refresh und zwei `WritePointer`-Implementierungen in Decompilation-/Repository-Cache. Die Scores lagen zwischen ca. 0,70 und 0,82. Die Cluster sind Audit-Kandidaten, aber ohne unabhängige Refactoringprüfung kein bestätigter DRY-Verstoß.
4. `find_dead_code` meldete nur niedrig-konfidente, intern sichtbare oder platform-nahe Kandidaten (u. a. Health-Property und native Job-Object-Felder) mit ausdrücklich genannten `internalsVisibleTo`-/Framework-Limits. Kein Kandidat wurde als sicher entfernbar eingestuft.
5. `find_magic_values` meldete einen einzelnen lokalisierten User-Message-Kandidaten; die Fundstelle ist bereits als feste Prozessfehlernachricht eingeordnet und kein Sicherheits- oder Wire-Budget-Befund.

## Abdeckungsgrenze UX-001

- Typ: Mess-/Live-Abdeckung, kein zusätzlicher bestätigter Produktdefekt
- Schweregrad: S3
- Umfang: U3 — reale Modell-/Client-Tokenkosten
- Konfidenz: mittel
- Evidenz: Die Reports und Metriktools liefern strukturierte Zähler und Limits; ein realer MCP-Client-Tokenverbrauch unter maximaler Referenz-/Diagnoselast wurde nicht gemessen.
- Auswirkung: Die qualitative Payload-Wiederholung ist statisch bestätigt, der genaue Tokenverbrauch hängt aber vom Client-Serializer und Modelltokenizer ab.
- Reproduktion: Maximale Assembly-Referenz-/Diagnosefixture über JSON-RPC ausführen, Bytegröße und clientseitige Tokenisierung getrennt messen und mit den `truncatedBy`-/`completeness`-Feldern korrelieren.
- Disposition: Mit MCP-001 verknüpft und zurückgestellt; keine Antwortvertragsänderung im Audit.
