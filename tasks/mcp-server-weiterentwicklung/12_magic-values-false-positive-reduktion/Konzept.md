---
status: ready (auditiert, umsetzbar)
type: konzept
project_kind: brownfield
estimated_scope: medium
priority: P2
agent_role: .agents/Agent-Scaffolding/dev-loop/planning/orchestrator.md
rules_dir: .agents/rules
last_updated: 2026-08-23
open_questions: []
herkunft: "Diskussion 2026-08-23 (ox-alpha + Nutzer): find_magic_values meldet False Positives aus generischen Heuristik-Schwächen (Substring-Matches gegen gewöhnliche Wörter, kontextlose Zahlenbereiche) und erkennt zentrale Konstanten-Klassen nicht — Empfehlungen sind dadurch teils selbst-referenziell falsch. AiNetLinter ist ein allgemeines Produkt: alle Maßnahmen generisch, kein Optimieren auf Self-Scan-Treffer."
---

# Konzept 12: find_magic_values — False-Positive-Reduktion & Holder-Bewusstsein

## Zweck dieses Dokuments

Dieses Dokument ist die Grundlage für die Umsetzung. Es enthält verifizierte Code-Findings mit
Belegen (Datei:Zeile), generisch formulierte Maßnahmen mit Vorher/Nachher-Code, eine ehrliche
Machbarkeits- und FN-Bilanz (Audit/Review der eigenen Ideen), Non-Goals, Testkatalog und
Doku-Pflichten. Der umsetzende Agent muss keine weiteren Kontexte beschaffen; Widersprüche zwischen
Diesem Dokument und dem Code sind zugunsten des Codes zu melden (als Blocker), nicht still
aufzulösen.

## Intention

`find_magic_values` ist ein Agent-facing On-Demand-Audit. Sein Wert steht und fällt mit der
Signal-to-Noise-Rate: Ein Agent, der in 85 % der Meldungen falsche Empfehlungen liest
(„Format-String-Konstante" für `Password`, „Secret-Store" für `CancellationToken`), lernt,
das Tool zu ignorieren — das widerspricht dem eigenen Maßstab aus
`09_regel-design-audit-kandidaten/Konzept.md` (Kandidat muss konkretes Defekt-Muster treffen,
sonst erzeugt er Noise).

**Grundsatz (Nutzer-Vorgabe 2026-08-23):** AiNetLinter ist ein **allgemeines Produkt** für fremden
C#-Code. Alle Maßnahmen sind **generisch** zu entwerfen: keine AiNetLinter-spezifischen Whitelists,
kein Optimieren auf eigene Self-Scan-Treffer, keine neuen Flags/Parameter (harte Cuts statt
Kompatibilitätsschichten, deterministische Regeln statt Auto-Erkennung). Die Live-Evidenz unten
stammt zwar aus dem Self-Scan (einzige read-only verfügbare Solution), jeder Befund wird aber
generisch begründet — die gleichen Treffer entstehen in jeder beliebigen Codebasis.

## Verifizierte Code-Findings (Stand 2026-08-23, gegen HEAD geprüft)

Evidenzbasis: Live-Lauf `find_magic_values` (includeSuppressed=true, includeTests=false) gegen die
eigene Solution — 936 Treffer / 838 eindeutige Einträge über 315 Dateien; ausgewertet wurde eine
Stichprobe von 250 Einträgen (maxResults-Limit, keine Vollerhebung). Alle Code-Behauptungen wurden
zusätzlich direkt im Quellcode verifiziert.

| # | Finding | Beleg |
|---|---|---|
| F1 | **Format-String-Heuristik matcht Substrings mitten in gewöhnlichen Wörtern.** `LooksLikeFormatString` prüft `Contains("yyyy"/"MM"/"dd"/"HH"/"mm"/"ss")` case-sensitive irgendwo im Wert. „**mm**" in `System.Collections.Immutable.dll`, „**dd**" in `ForbiddenNamespaceDependency`, „**ss**" in `Password`/`Message`/`Address`/`Session`/`Klasse`. In der Stichprobe waren 155 von 158 Format-String-Meldungen keine Datumspatterns. Generisch: jedes gängige Wort mit doppeltem s/d/m wird zur „Constants.cs (Format-String-Konstante)" erklärt. | `MagicValuesClassifier.cs:379-400` (Substring-Checks 383-391) |
| F2 | **Security-Heuristik 3 (Wert-Substring, CWE-798) erzeugt Secret-Alarm für Bezeichner-artige Begriffe.** `literalText.Contains(keyword)` mit Keywords u. a. `token`, `auth`, `password` → Empfehlung „In Secret-Store/KeyVault auslagern" für `publicKeyToken`, `CancellationToken`, `AuthenticationStateProvider` (**auth**), CLI-Hilfetexte mit dem Wort „Token". Generisch: jeder Prosa-String/Protokollbegriff mit diesen Substrings wird zum vermeintlichen Secret. Widerspricht dem eigenen Klassifizierer-Doc „Bewusst konservativ (mehr False Negatives als False Positives)". | `MagicValuesStringHeuristics.cs:231-242` (Keywords 23-26); `MagicValuesClassifier.cs:36-37` |
| F3 | **HTTP-Statuscode-Bereich ohne Kontext.** Jeder int 100–599 wird pauschal als Statuscode gemeldet — z. B. Config-Defaults `150`/`300` → Empfehlung `StatusCodes.Status150150`/`Status300300`. Generisch: `PageSize = 200`, `MaxTimeoutSeconds = 300`, `BufferSize = 512` bekommen HTTP-Semantik angedichtet. Zusätzlich ein Formatierungs-Bug: für unbekannte Codes liefert der Fallback in `ResolveStatusCodeName` die Zahl selbst als Namen → verdoppelte Ziffern (`Status150150`). | `MagicValuesNumberClassifier.cs:38-45, 88-92` (Fallback-Bug: 113-114) |
| F4 | **Well-known-Zahlen ohne Kontext + Datenfehler.** `60`/`1000`/`24`/… werden kontextfrei als Zeitkonstante empfohlen („NamedConstant (SecondsPerMinute)") — auch für `MaxLineCount`-Defaults und N-Gram-Größen. Und: `StandardExtraNames` mappt `360 → "SecondsPerHour"` — faktisch falsch (3600). Generisch: die Bedeutung einer Zahl hängt vom Kontext ab; die feste Namensmap rät. | `MagicValuesStringHeuristics.cs:50-69` (Datenfehler Zeile 66); `MagicValuesNumberClassifier.cs:80-83` |
| F5 | **Schwellwert-Heuristik meldet Werte, die bereits zentral liegen.** double/float/decimal in Feld-Initialisierern mit const/static/readonly → immer „Constants.cs (zentrale Schwellenwert-Konstante)" — auch wenn das Feld genau das bereits ist (z. B. `static readonly double SimilarityThreshold = 0.65` in einer Konstanten-Klasse). Selbst-referenziell falsche Empfehlung. | `MagicValuesNumberClassifier.cs:61-73, 122-153` |
| F6 | **Kein Holder-Bewusstsein: der Classifier wertet nirgends den umschließenden Typ aus.** Eine dedizierte Konstanten-Klasse ist für Walker/Classifier unsichtbar (nur `LiteralExpressionSyntax` + Interpolations-Segmente werden besucht). Folge 1: Werte in Bündel-Klassen werden je nach Heuristik-Zufall erneut gemeldet. Folge 2: `DuplicateConstScanner` gruppiert nur nach `(Typ, Wert)` — ohne Feldnamen — und empfiehlt ab 2 Dateien „Hochstufung in eine gemeinsame Konstanten-Klasse", auch wenn eine der beiden bereits der zentrale Ort ist oder die Werte semantisch nichts miteinander zu tun haben (`RetriesMax = 3` vs. `NgramSize = 3`). | `FindMagicValuesScannerWalker.cs:31-56`; `DuplicateConstScanner.cs:24, 55, 72-79, 135` |
| F7 | **Tool-Beschreibung ist veraltet (Dokubug).** Die MCP-Description behauptet, `includeSuppressed` und `changedOnly` seien „No-op in aktueller Version" — beides ist implementiert und wirksam (Suppression via `// ainetlinter-disable MagicValues`, changedOnly via Git-Diff). | `AnalysisToolRegistrations.cs:249-251`; Gegenbeleg `MagicValuesClassifier.cs:85-88, 301-310` und `FindMagicValuesScanner.cs:44-48, 99-131` |
| F8 | **Positiv-Befund (Grenzmarker):** `internal static class ProjectRegistryDefaults { const int MaxProjects = 4; static readonly TimeSpan IdleTtl = TimeSpan.FromMinutes(45); … TickInterval = TimeSpan.FromMinutes(5); }` wird heute **nicht** gemeldet (`4` trifft keinen Zahlenpfad; `minutes` steht nicht in `TimeoutParameterNames`). Diese Klasse ist der Testmarker: keine Maßnahme darf sie brechen — aber auch die generischen Varianten darunter (F3–F5) müssen sauber bleiben. | Trace: `MagicValuesNumberClassifier.cs:31-86` (alle vier Zahlenpfade) |

## Maßnahmen

Alle Maßnahmen greifen ausschließlich in `src/AiNetLinter/Mcp/Tools/MagicValues/` (Classifier,
String-Heuristics, Number-Classifier, DuplicateConstScanner) plus den Description-String in
`AnalysisToolRegistrations.cs`. Keine Architekturänderung, kein neues Config-Schema, keine neuen
Tool-Parameter → `rules.json` bleibt unberührt (kein `--sync-agent-rules-only` nötig).

### M1 — Format-String-Heuristik: nur reine Pattern-Strings (behebt F1)

Statt Substring-Matching nur melden, wenn der String **ausschließlich** aus Datumspattern-Zeichen
und Separatoren besteht. Composite-Format-Strings (`{0:F2}`) bleiben wie gehabt.

```csharp
// Vorher (MagicValuesClassifier.LooksLikeFormatString): Substring → "Password" (ss) = true ❌
if (value.Contains("yyyy", ...) || value.Contains("MM", ...) || /* … */) return true;

// Nachher: reine Datumspatterns — jedes fremde Zeichen disqualifiziert
private static bool LooksLikeDateFormatString(string value)
{
    if (value.Length < 3) return false;
    var patternLetters = 0;
    foreach (var c in value)
    {
        if ("yMdHmsftzF".Contains(c)) { patternLetters++; continue; }
        if (!":/-. ,".Contains(c)) return false; // fremdes Zeichen → kein Datumspattern
    }
    return patternLetters >= 3;
}
```

Generische Wirkung: `"yyyy-MM-dd"` ✅, `"yyyyMMddHHmmss"` ✅, `"HH:mm:ss"` ✅,
`"ddd, dd MMM yyyy HH:mm:ss"` ✅ — `"Password"`, `"Message"`, `"Address"`, `"Session"`,
`"System.Collections.Immutable.dll"` ❌ (nicht gemeldet). Der `{0…}`-Composite-Check bleibt
unverändert (schmal und korrekt).

### M2 — Security-Heuristik 3: exakter Match statt Substring (behebt F2)

Heuristik 1 (Praefixe `AKIA`/`sk-`/`ghp_`/`xoxb-`) und Heuristik 2 (umgebender Symbol-Name) bleiben
unverändert — die sind präzise. Nur Heuristik 3 (Wert selbst) wird auf exakte Gleichheit
(OrdinalIgnoreCase) mit der Keyword-Liste eingegrenzt:

```csharp
// Vorher: literalText.Contains("token") → "publicKeyToken" ❌, "CancellationToken" ❌
// Nachher:
if (SecurityNameKeywords.Contains(literalText, StringComparer.OrdinalIgnoreCase))
{
    return …; // Connect("password"), var secret = "token" → weiterhin gemeldet ✅
}
```

Bewusst akzeptierte False Negative: `"password123"` ohne Prefix/Param-Kontext wird nicht mehr
gemeldet (Heuristiken 1+2 decken die realen Fälle ab). Das entspricht der dokumentierten
Klassifizierer-Doctrine (FN > FP). Explizit verworfen: Segment-Matching (`Split('-','_','.')`)
— würde FPs wie `"auth_header"` (ein Header-NAME, kein Secret-Wert) wieder einführen.

### M3 — HTTP-Statuscodes kontextbinden + Namens-Bug fixen (behebt F3)

Der Bereich 100–599 wird nur noch gemeldet, wenn der Kontext status-artig ist — mit denselben
deterministischen Mechaniken, die die Timeout-Heuristik bereits nutzt (SemanticModel fließt
bereits in `ClassifyNumber` hinein, kein neues Plumbing):

```csharp
private static bool HasStatusCodeContext(LiteralExpressionSyntax literal, SemanticModel? model)
{
    // a) Vergleichs-/Switch-Ziel heißt status/code (if (response.StatusCode == 404),
    //    switch (status) { case 404: … }) — case-insensitiver Namens-Substring, deterministisch,
    //    gleiche Regelklasse wie die existierende TimeoutParameterNames-Liste.
    // b) Aufgelöster Parameter heißt status/code (via existierendem TryResolveParameterName).
    // c) Semantischer Zieltyp ist HttpStatusCode (GetSymbolInfo auf die Zuweisungs-/Vergleichs-Operation).
}
```

Ohne Kontext: **nicht gemeldet** (harter Cut, keine „vielleicht"-Kategorie). Akzeptierte FN:
nackte `return 404;` in Kontexten ohne status-artige Namen. Empfehlungs-Building fixen: Name nur
anhängen, wenn `ResolveStatusCodeName` einen bekannten Code liefert — sonst `StatusCodes.Status{code}`
ohne Ziffern-Doppelung.

Generische Wirkung: `if (response.StatusCode == 404)` ✅ weiterhin; `PageSize = 200`,
`MaxTimeoutSeconds = 300` ❌ nicht mehr.

### M4 — Well-known-Zahlen: Zeitwerte streichen, Buffer kontextbinden (behebt F4)

- **Zeitwerte (24/60/360/1000/1440/86400): Pfad ersatzlos streichen.** Ihre Bedeutung hängt von der
  Einheit am Verwendungsort ab, die die kontextfreie Namensmap nicht kennen kann — `60` ist
  SecondsPerMinute, MaxLineCount oder eine Page-Size, je nach Kontext. Selbst-dokumentierende
  Zeitangaben (`TimeSpan.FromMinutes(45)`, `TimeSpan.FromSeconds(60)`) tragen die Einheit bereits im
  Methodennamen → Meldung wäre ohnehin wertlos. `Thread.Sleep(1000)` bleibt über den
  Timeout-Parameter-Pfad (millisecondsTimeout → config_candidates) abgedeckt.
- **Buffer-Größen (1024/2048/4096/8192):** nur melden, wenn Feld-/Parameter-/Variablenname
  buffer/chunk/size enthält (deterministischer Namens-Substring wie M3a).
- Der Datenfehler `360 → SecondsPerHour` entfällt mit dem Zeit-Pfad.

### M5 — Holder-Bewusstsein für Zahlen + DuplicateConstScanner (behebt F5, F6)

Generische Definition eines Konstanten-Holders **ohne Namenslisten**: ein Typ mit
`static`-Modifier (C#-Garantie: statische Klassen enthalten ausschließlich statische Member — das
ist die Sprach-Idiom-Form des Konstanten-Bündels).

```csharp
private static bool IsInStaticHolderType(FieldDeclarationSyntax fieldDecl)
    => fieldDecl.FirstAncestorOrSelf<TypeDeclarationSyntax>() is { } typeDecl
       && typeDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword));
```

Anwendung, bewusst **nur auf die Zahlenpfade**:

1. **Schwellwert-Pfad (F5):** double/float/decimal in Feld-Initialisierern eines statischen
   Holder-Typs → nicht melden (die Empfehlung „in Constants.cs zentral definieren" an einen Wert in
   genau so einer Klasse ist selbst-referenziell).
2. **DuplicateConstScanner (F6):** (a) Konstanten aus statischen Holder-Typen überspringen;
   (b) Gruppierungsschlüssel von `(Typ, Wert)` auf `(Typ, Feldname, Wert)` ändern — `MaxRetries = 3`
   in zwei Dateien gruppiert weiterhin (echtes Duplikat), `RetriesMax = 3` + `NgramSize = 3` nicht
   mehr (semantisch unverbunden). Harte Cuts: alter Schlüssel wird ersetzt, kein Kompat-Flag.
3. **String-Heuristiken bleiben von M5 ausgenommen** — bewusst: Für Strings ist das
   Empfehlungsziel (appsettings.json/Secret-Store) ein *anderer* Ort als die Holder-Klasse, die
   Auslagerungsempfehlung bleibt also fachlich valide; für Zahlen ist das Empfehlungsziel
   (Constants.cs) der Holder selbst.

Generische Wirkung auf die Variantenmatrix von F8: `const int MaxTimeoutSeconds = 300` im Holder →
M3 (kein Statuskontext) ❌; `static readonly double Threshold = 0.65` im Holder → M5.1 ❌;
zwei `MaxProjects = 4` (einer im Holder) → M5.2 ❌; zwei `MaxProjects = 4` in normalen Klassen → ✅
weiterhin gemeldet.

### M6 — Tool-Beschreibung korrigieren (behebt F7)

Die „No-op"-Aussagen für `includeSuppressed` und `changedOnly` aus der Description entfernen und
durch die tatsächliche Semantik ersetzen (Suppression-Marker
`// ainetlinter-disable MagicValues`; changedOnly = Git-Diff-Filter am Solution-Root). Zusätzlich
`Docs/` nach den No-op-Formulierungen durchsuchen und angleichen.

## Audit & Review der eigenen Ideen (360°)

### Ist das überhaupt machbar? — Ja, mit Begründung pro Maßnahme

| Maßnahme | Mechanik | Neues Plumbing? | Restrisiko |
|---|---|---|---|
| M1 | Reiner Char-Set-Check pro String-Literal, AST-only | Nein | Vernachlässigbar; exotische Patterns mit Fremdzeichen (z. B. `\\` in Regex-artigen Patterns) fallen raus — kein realer Datumspattern nutzt sie |
| M2 | Set-Lookup statt Substring | Nein | FN `password123` (dokumentiert, doctrine-konform) |
| M3 | Namens-Substring + `TryResolveParameterName` + `GetSymbolInfo` — alles existierende Mechaniken | Nein (SemanticModel kommt bereits in `ClassifyNumber` an) | Kontextsignale greifen nicht bei nicht aufgelösten Symbolen (dann FN statt FP — richtige Richtung) |
| M4 | Pfad-Streichung + Namens-Substring | Nein | FN für kontextfreie `1024` ohne size-Namen — bewusst |
| M5 | `FirstAncestorOrSelf<TypeDeclarationSyntax>()` + Modifier-Check; Scanner-Schlüsseländerung | Nein | Verhaltensänderung im DuplicateConstScanner — siehe Risiken |
| M6 | Text | Nein | Keine |

Keine Maßnahme ändert den MCP-Vertrag (keine neuen Parameter, gleiche Output-Struktur), keine
erzeugt neue Konfigurationsfläche, keine benötigt einen neuen Solution-Pass. Performance: M3/M4
führen semantische Lookups nur für ints 100–599 bzw. die Buffer-Liste aus — gleiche Kostenklasse
wie die existierende Timeout-Heuristik; M1 ist O(Länge) pro String-Literal.

### Ehrliche FN-Bilanz (was verlieren wir an echten Treffern?)

- **M1:** keine realen (alle gängigen Datumspatterns bleiben gemeldet).
- **M2:** Werte wie `password123` ohne Prefix/Param-Kontext. Selten; Heuristiken 1+2 bleiben.
- **M3:** nackte Statuszahlen ohne status-artige Namen/Typen (z. B. `return 404;` in `Handle()`).
  Real existieren solche Stellen — der Verzicht ist die bewusste Zahlung für kontextlose
  Config-Zahlen (`PageSize = 200`), die heute lauter falsch klassifiziert werden.
- **M4:** kontextfreie Buffer-Zahlen ohne size-Namen. Gering.
- **M5:** Same-Value-Duplikate mit unterschiedlichen Feldnamen werden nicht mehr gruppiert — das
  war exakt die FP-Form (semantisch unverbundene Werte). Kein Verlust.

Jede Zahlung ist vor dem eigenen Maßstab gerechtfertigt (FN > FP, `MagicValuesClassifier.cs:36-37`)
und wird als Testfall beidseitig festgeschrieben.

### Einwände, mit denen ich selbst gerechnet habe (und warum sie nicht tragen)

1. **„Ist FP-Reduktion relevant, wenn das Tool nur On-Demand läuft (kein CI-Friktion)?"** — Ja.
   On-Demand heißt nicht geräuschfrei: Der Agent, der das Audit anstößt, zahlt die Noise mit
   Kontextfenster und Fehlentscheidungen. Zudem sind die Empfehlungen teils aktiv falsch
   (F2: Secret-Alarm für `CancellationToken`; F5: Zentralisierungs-Aufforderung an bereits
   zentrale Werte) — das vergiftet nicht nur die Trefferliste, sondern die daraus gezogenen
   Refactoring-Entscheidungen.
2. **„Warum nicht gleich die ganzen Heuristiken löschen (harter Cut)?"** — Weil jede echte
   Trefferklassen hat: URLs, Connection-Strings, echte Secrets, echte Datumspatterns, Statuscode-
   Vergleiche. Schärfung > Streichung; gestrichen wird nur der kontextfreie Zeit-Zahlen-Pfad (M4),
   dessen Namensmap nachweislich öfter falsch als richtig rät.
3. **„Confidence-Feld statt Streichung?"** — Verworfen: neuer Output-Vertrag + Einstellfläche
   (widpricht „sparsam mit Flags") und verschiebt das Problem in den Kopf des Agenten. Deterministische
   Entfernung ist der generischere Schnitt.
4. **„Ist der Namens-Substring in M3/M4 nicht genau das ‚Rumraten', das vermieden werden soll?"**
   — Nein: es ist eine deterministische Regel mit fester Zeichenfolge, dieselbe Regelklasse wie die
   existierende, etablierte `TimeoutParameterNames`-Liste. Kein Fallback, keine Auto-Erkennung,
   kein Schwellwert-Verhandeln.
5. **„M5 könnte echte Magic Values in statischen Klassen verstecken."** — M5 greift nur auf
   *Feld-Initialisierer*; Literale in Methoden (Switch-Kaskaden, Vergleiche) bleiben voll gemeldet.
   Ein benanntes `const`-Feld in einer statischen Klasse ist per Definition bereits der empfohlene
   Zielzustand.

### Risiken & Gegenmaßnahmen

| Risiko | Gegenmaßnahme |
|---|---|
| Bestandstests schreiben heutiges FP-Verhalten fest (z. B. Substring-Format-Strings, Statuscodes ohne Kontext) | Tests werden auf die neuen Verträge **umgeschrieben**, nicht abgeschwächt (Symptom-Fixing-Verbot, Richtlinien §5); jede Umstellung ist ein expliziter Vertragswechsel |
| M5-Schlüsseländerung ändert DuplicateConst-Ergebnisse in fremden Codebasen spürbar | Reiner FP-Reduktionsschnitt; im Commit und in `Docs/` als Verhaltensänderung benannt |
| FN-Drift durch zu enge Kontextsignale (M3/M4) | Testkatalog verlangt je Maßnahme TP-Erhaltsfälle (Statuscode-Vergleich, `Thread.Sleep(1000)`, Buffer-Namen) neben den FP-Fällen |
| Dogfood-Test `McpLiveRepositoryTests` (find_magic_values-Aufruf) könnte an schrumpfenden Trefferzahlen hängen | Geprüft: der Test asserted nur auf den Report-Header „Magic-Value-Audit", nicht auf Trefferzahlen → safe |

## Was wir NICHT machen (Non-Goals)

| Non-Goal | Begründung |
|---|---|
| Holder-Erkennung über Namens-Suffixe („…Constants", „…Defaults") wie `MiddleManExemptSuffixes` | Weniger generisch als die statische-Klassen-Regel (M5); Namenslisten sind Konventionen, Sprache nicht |
| Neue Config-Parameter/Flags für jede Schärfung | Widerspricht „harte Cuts, sparsam mit Flags"; Verhalten ist deterministisch, kein Konfig-Bedarf |
| Neue Kategorie (z. B. `low_confidence`) oder Confidence-Feld im Output | Vertragsänderung ohne Nutzen — siehe Einwand 3 |
| Suppression-System umbauen | Existiert und funktioniert (F7-Gegenbeleg); nur die Description lügt (M6) |
| enum_candidates-/localization_candidates-Änderungen | Keine FP-Evidenz in der Stichprobe; ohne Beleg keine Maßnahme (Lehre 09) |
| Optimierung auf Self-Scan-Treffer / AiNetLinter-Whitelists | Nutzer-Vorgabe: generisches Produkt |
| Build-Regel aus find_magic_values ableiten | Weiterhin gebunden an Evidenz-Loop (Konzept 09, Stufenleiter) |

## Testkatalog (FastTests, Category=Component, Muster der bestehenden Helper)

Neue Testklasse `FindMagicValuesScannerFalsePositiveTests` (neben den bestehenden Heuristik-Klassen,
um `MaxPublicMembersPerType: 15` zu respektieren); je Maßnahme mindestens ein FP-Fall und ein
TP-Erhaltsfall:

1. **M1-FP:** `"Password"`, `"Message"`, `"System.Collections.Immutable.dll"` → nicht gemeldet.
2. **M1-TP:** `"yyyy-MM-dd"`, `"yyyyMMddHHmmss"`, `"ddd, dd MMM yyyy HH:mm:ss"`, `"{0:F2}"` → gemeldet.
3. **M2-FP:** `"publicKeyToken"`, `"CancellationToken"`, `"AuthenticationStateProvider"` → nicht als security_candidates.
4. **M2-TP:** `Connect("password")`, Prefix `"sk-…"` → security_candidates.
5. **M3-FP:** `const int PageSize = 200;`, `const int MaxTimeoutSeconds = 300;` → nicht gemeldet.
6. **M3-TP:** `if (response.StatusCode == 404)`, `switch (status) { case 404: … }` → standard_candidates.
7. **M4-FP:** `TimeSpan.FromMinutes(60)`, `const int MaxLineCount = 60;` → nicht gemeldet.
8. **M4-TP:** `Thread.Sleep(1000)` (Timeout-Pfad), `bufferSize: 1024` / `const int ChunkSize = 4096;` → gemeldet.
9. **M5-FP:** `internal static class Defaults { const int MaxTimeoutSeconds = 300; static readonly double Threshold = 0.65; }` → nicht gemeldet; `MaxProjects = 4` + `MaxProjects = 4` (einer im Holder) → kein Duplikat.
10. **M5-TP:** zwei `const int MaxRetries = 3` in zwei nicht-statischen Klassen → Duplikat-Gruppierung mit neuem Schlüssel.
11. **Grenzmarker (F8):** die `ProjectRegistryDefaults`-Klasse aus der Diskussion als Ganzes → 0 Meldungen (Regressionsanker).
12. **M6:** Description-Text enthält keine „No-op"-Formulierung mehr (Bestandstest `ToolAppearsInRegistrationList` erweitern).

Bestandstests, die heutiges FP-Verhalten festschreiben, werden auf die neuen Verträge umgeschrieben
(keine Assertions abschwächen). Danach Abschluss-Verifikation: `dotnet test src/AiNetLinter.FastTests
--filter Category!=Stress` und `dotnet test src/AiNetLinter.IntegrationTests --filter
Category!=Stress` (AGENTS.md §2).

## Umsetzungsreihenfolge & Doku-Pflichten

1. **M1 + M2** (größter FP-Hebel, geringstes Risiko, AST-only) → 2. **M6** (Doku-Truth) →
3. **M3 + M4** (SemanticModel-Kontext) → 4. **M5** (Holder-Regel + Scanner-Schlüssel).
Jede Stufe ist einzeln grün testbar und shippbar.

Doku: `rules.json` unverändert → keine `configuration.md`-Schema-Änderung und kein
Agent-Rules-Sync nötig; die Verhaltensänderung wird in `Docs/` (MCP-Tool-Referenz/Integration)
nebst der Description-Korrektur (M6) dokumentiert.

## Entscheidungspunkte für die Umsetzungsplanung

- M4-Zeitschnitt ist ersatzlos (Empfehlung) — Alternative wäre Kontextbindung via Namens-Substring
  auch für Zeitwerte; verworfen, weil die Namensmap (`SecondsPerMinute` für eine Delay-Zahl) auch
  im Kontextfall falsch beraten kann.
- M5.3-Grenzlinie (Holder-Regel nur für Zahlen) ist im Dokument begründet; falls der Planner sie
  breiter ziehen will, ist das ein eigener Entscheidungspunkt mit neuen Testfällen, kein Default.
