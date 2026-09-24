# 04 – Verify-Advisories: Trunkierung, Zähler und Review-Zugriff

**Status:** reproduzierbare Ausgabegrenze. **Schweregrad:** mittel. **Gate-Semantik:** Alle vier Läufe `pass`, Score 10.0, null Lint-Verstöße; Dead-Code-Kandidaten sind Advisorys.

## Evidenz

| Solution | Kandidaten | `test_only` | `unreferenced` | gezeigt | gekürzt |
|---|---:|---:|---:|---:|---:|
| AiNetLinter | 467 | 151 | 316 | 13 | 454 |
| SqlToAi | 92 | 40 | 52 | 13 | 79 |
| KnowHowToAI | 358 | 231 | 127 | 12 | 346 |
| SAN | 714 | 403 | 311 | 11 | 703 |

Die `verify`-Antworten nennen `deadCode.status=complete`, `shown`, `truncatedBy`, `next=review_now` und `truncation=evidence_limit,response_budget`. Daneben stehen Zähler wie `evidence=returned=11/770` (SAN) und `candidates=714` ohne unmittelbare Erklärung des Bezugs. Die vier Originalberichte liegen in diesem Verzeichnis.

## Auswirkung

49 von 1.631 gemeldeten Kandidaten waren direkt sichtbar. Die Ausgabe ist kurz (ca. 3,9–4,1 KB serialisiertes Toolresultat), aber eine systematische Review der übrigen Kandidaten ist aus derselben Antwort nicht möglich. Die klare Trennung vom Gate-Verdict ist richtig; `completeness=complete` kann ohne Lesart „Scan abgeschlossen, Liste gekürzt“ missverstanden werden.

## Grundlage für ein mögliches Umsetzungskonzept

- Zuerst entscheiden, ob der Server eine paginierte Advisory-Abfrage oder einen Filter nach Projekt, `usage` und Confidence anbieten soll.
- Zähler sprachlich und maschinenlesbar trennen: Scan-Ergebnis, Lint-Evidenz, sichtbare Advisorys, ausgelassene Advisorys.
- Bei jeder Auswahl die Sortier-/Priorisierungsregel und einen überprüfbaren Fortsetzungsweg angeben.

**Abnahmekriterium:** Ein Agent kann nach dem ersten Verify alle Kandidaten einer gewählten Kategorie ohne erneuten Voll-Verify und ohne Verlust der Symbol-IDs abrufen. Das Gate bleibt von Advisories unberührt.
