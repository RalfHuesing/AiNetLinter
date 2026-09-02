# Tech-Debt-Register

Primäraufgabe: Behebe und konsolidiere die dekompilierte Assembly-Analyse gemäß dem freigegebenen Konzept.

Aktuell sind noch keine Befunde aus einem Rollenbericht triagiert.

## TD-QL-001 – AIContextFootprint-Warnungen in Assembly-Modulen

- Schweregrad: P2
- Ursache/Scope: Sechs bestehende `AIContextFootprint`-Warnungen in den Assembly-Coordinators/Navigators unter `src/AiNetLinter/Mcp`.
- Evidenz: Paket-1-Implementierer meldet `get_violations` mit 0 Fehlern und 6 Warnungen sowie `safeguard` 1,0/10; die Warnungen lagen bereits außerhalb des Paket-1-Änderungsbereichs.
- Disposition: `accepted-deferred`
- Attempts: 0
- Nächster Schritt: Im vorgesehenen Paket 3 die betroffenen transitive Footprints gezielt untersuchen und auf `AIContextFootprint <= 2500` bringen.
- Log-Anker: `execution-log.md`, 2026-09-02 Paket 1 Implementierer.
