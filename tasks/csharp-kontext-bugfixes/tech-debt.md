# Tech Debt

## TD-001 — Bestehende Duplikat-Cluster außerhalb des Feature-Context-Scopes

- Schweregrad: P3
- Beschreibung: Der Audit meldet 13 bestehende Duplikat-Cluster.
- Scope/Fundstelle: durch `find_duplicates` im Projekt; keine konkrete eigene
  Änderung des aktuellen Feature-Context-Pakets erforderlich.
- Evidenz: Implementierer-Audit am 2026-09-07; Cluster sind bestehender,
  scope-ferner bzw. semantisch unklarer Altbestand.
- Disposition: accepted-deferred
- Nächster Schritt: Bei einer passenden Änderung der betroffenen Bereiche
  erneut fachlich bewerten; kein breiter Cleanup in diesem Task.
- Log-Anker: Implementiererbericht vom 2026-09-07.

## TD-002 — Low-Confidence-Dead-Code-Kandidaten außerhalb des Scopes

- Schweregrad: P3
- Beschreibung: Der Audit meldet 3 bestehende Low-Confidence-Kandidaten.
- Scope/Fundstelle: durch `find_dead_code` im Projekt; nicht Bestandteil der
  geänderten Feature-Context-/Caller-Pfade.
- Evidenz: Implementierer-Audit am 2026-09-07; geringe Konfidenz und daher
  keine sichere Entfernung ohne separate Referenz-/Vertragsprüfung.
- Disposition: accepted-deferred
- Nächster Schritt: In einem passenden Cleanup-/API-Scope mit
  `find_references` und Reflection-/Vertragsprüfung bewerten.
- Log-Anker: Implementiererbericht vom 2026-09-07.
