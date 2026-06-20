# MaxDirectoryDepth (M09)

**Kategorie:** Numerische Metrik  
**Aktueller Wert:** 4 | **Severity:** error | **Status:** Aktiv  
**Paper-Cluster genutzt:** B, C, E, H

---

## Bewertung

🟢 **WERTVOLL**

**Fazit:** Der Grenzwert von 4 Verzeichnisebenen ist für typische Clean-Architecture-.NET-Projekte angemessen und verhindert übertriebene Verschachtelungsstrukturen, die Agenten beim Repository-Navigation belasten — beibehalten.

---

## Empfohlene Range

| | Wert | Begründung |
|--|------|-----------|
| **Untergrenze (sinnlos darunter)** | 2 | Unter 3 Ebenen sind typische .NET-Lösungsstrukturen (Projekt → Feature → Unterordner) nicht darstellbar |
| **Empfehlung (beste Evidenz)** | 4 | Clean Architecture: Solution → Project → Layer (Domain/Application/Infrastructure) → Feature; das sind 4 Ebenen für eine professionelle Projektstruktur |
| **Obergrenze (Nutzen geht verloren)** | 6 | Ab 7 Ebenen ist die Navigation für Menschen und Agenten deutlich erschwert; praktisch kein Szenario rechtfertigt diese Tiefe |
| **Aktueller Wert** | 4 | Angemessen — trifft den sinnvollen Mittelwert für professionelle .NET-Projekte |

---

## Wissenschaftliche Grundlage

Keine direkte empirische Studie zu optimaler Verzeichnistiefe mit quantitativen Ergebnissen wurde gefunden — dies ist eine der klaren Lücken in der Forschungsliteratur (Cluster-E-Befund). Die Evidenz ist überwiegend qualitativ:

Sandor Dargo (2023) empfiehlt, Namespace-/Verzeichnisstrukturen als Architektur-Spiegel zu nutzen: Wenn die Verzeichnisstruktur die Abhängigkeitsstruktur widerspiegelt, werden Architekturprobleme automatisch sichtbar. Zu viele verschachtelte Ebenen erschweren dieses Prinzip.

Indirekter Beleg aus Architekturstudien: Agenten scheitern häufig beim Auffinden der richtigen Datei in tief verschachtelten Strukturen (arXiv:2604.03515). Über 50 % der Agenten-Ausfälle bei komplexen Codebasen resultieren aus Repository-Kontext-Navigationsfehlern. Flachere Strukturen (weniger Ebenen) reduzieren die Suchtiefe.

Aus der RAG/Context-Engineering-Perspektive (Cluster B): Agenten die gezielt Dateien laden müssen, bauen ihre Suchanfragen aus Pfadkomponenten auf. Bei 7 Verzeichnisebenen müssen mehr Pfadsegmente verarbeitet werden, was das Kontextfenster belastet und die Treffsicherheit der Datei-Lokalisation reduziert.

## KI-Agenten-Perspektive

Verzeichnistiefe ist einer der direktesten Prädiktoren für Repository-Navigationsfehler:

1. **Pfad-Komplexität:** Ein Agent der eine Datei bei `src/Domain/Features/Orders/Handlers/Commands/CreateOrder/CreateOrderHandler.cs` sucht, muss 7 Pfadsegmente korrekt aus seinem Kontext ableiten. Bei `src/Orders/Commands/CreateOrderHandler.cs` sind es 3. Jedes weitere Segment erhöht die Wahrscheinlichkeit eines Navigationsfehlers.

2. **Import-Ketten:** Tiefe Verzeichnisstrukturen erzeugen lange Namespace-Namen. C# `using`-Direktiven mit 6-gliedrigen Namespaces sind schwieriger für LLMs zu generieren und zu verifizieren als kurze (Liu et al. 2024/2025: „Project Context Conflicts").

3. **Agenten-Scaffolding-Belastung:** Tools wie `ls` oder `glob` geben bei tiefen Verzeichnissen mehr Ergebnisse zurück, die den Kontext füllen. Anthropic Engineering Blog (2025) betont, dass kontexteffizientes Arbeiten ein Designziel für Langzeit-Agenten ist.

Die Scaffold-Fehleranalyse (arXiv:2604.03515) belegt, dass Repository-Kontext-Navigation die häufigste Fehlerursache für Coding-Agenten ist. MaxDirectoryDepth ist eine direkte Stellschraube für diese Fehlerursache.

(Evidenzebene: indirekte Ableitung — kein direktes C#-Experiment mit messbaren Ergebnissen zur optimalen Verzeichnistiefe.)

## Zeitliche Einordnung

**Grundlagenstabilität:** Zeitlos

Repository-Navigation ist ein fundamentales Problem für autonome Agenten, das aus dem Informationsstruktur-Design folgt. Bessere Retrieval-Mechanismen (RAG, BM25, semantische Suche) können navigationsbedingte Fehler reduzieren, aber nicht eliminieren — flachere Strukturen reduzieren die Baseline-Schwierigkeit dauerhaft.

---

## Empfehlung

**Aktion:** Wert beibehalten (4)  
**Begründung:** 4 Verzeichnisebenen ist für professionelle Clean-Architecture-.NET-Projekte ein ausgewogener Grenzwert, der sinnvolle Strukturierung erlaubt, ohne übertriebene Verschachtelung zu ermöglichen.

---

## Quellen

- Sandor Dargo (2023): „How to Use Your Namespaces to Their Best" — sandordargo.com/blog/2023/12/13/namespace-best-practices
- arXiv:2604.03515 (2025/2026): „Inside the Scaffold: Agent Failure Taxonomy" — Repository Context Navigation als häufigste Fehlerursache
- Liu et al. (2024/2025): „LLM Hallucinations in Practical Code Generation" — arXiv:2409.20550
- Anthropic Engineering Blog (2025): „Effective Context Engineering for AI Agents" — anthropic.com/engineering/effective-context-engineering-for-ai-agents
- arXiv:2511.00872 (2024): Empirical Agent Framework Studies — 50 %+ Ausfälle durch Kontext-Navigation
