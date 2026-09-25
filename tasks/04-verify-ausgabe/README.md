# Verify-Advisories: aktueller Stand

`get_verify_advisories(targetPath, category="dead_code")` liefert einen neuen,
solutionweiten Dead-Code-Scan. Die Content-Antwort enthält höchstens 65.536
UTF-8-Bytes, nur vollständige Einträge und die Zähler `candidates`, `shown`
und `truncatedBy`. `verify` bleibt das Qualitäts-Gate; Dead-Code-Kandidaten
ändern sein Verdict nicht.

Der [Audit vom 25.09.2026](audit-2026-09-25.md) bestätigt in der realen
SAN-Solution **714 Kandidaten**, aber nicht 714 nachweislich tote Stellen.
Der Advisory-Abruf zeigt 522 und meldet 192 als gekürzt. In zwei Stichproben
wurden mehrere echte Nutzungen über Razor, Dependency Injection und
Framework-Callbacks gefunden, die die statische Referenzsuche übersieht.
Diese Kandidaten dürfen nicht ungeprüft entfernt werden.

Der Taskordner enthält den aktuellen Befund und die Grenzen der Prüfung.
Die frühere Implementierungsplanung und die historischen Rohberichte wurden
nach Abschluss der Umsetzung entfernt.
