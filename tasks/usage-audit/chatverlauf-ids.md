was sind Opaque-IDs? diese sha512 oder was auch immer das ist? rauschen? zuviel? zu lang?
---
Das sind **keine SHA-512-Hashes als Nutzinhalt**, sondern **Maschinen-IDs für Folge-Calls**. Im Markdown sind sie für mich meist Rauschen: zu lang, oft redundant, und laut Workflow sowieso nicht zum Abtippen gedacht.

Eine typische ID aus dem Test sah so aus:

`source:` + Base64-Pfad der slnx + `:` + Snapshot-Fingerprint + `:` + DocCommentId

Konkret drei Teile hinter `source:`:

1. **Base64 des Solution-Pfads**
   `YzpcRGF0ZW5c…LnNsbng` ist schlicht `c:\Daten\Entwicklung\SAN\San.smart.Planner.Platform\San.smart.Planner.Platform.slnx`. Kein Hash, nur der Pfad noch einmal, den ich bei jedem Call sowieso als `targetPath` mitschicke.

2. **Snapshot-Fingerprint (64 Hex-Zeichen)**
   Das ist **SHA-256**, nicht SHA-512. Er bindet die Antwort an den aktuellen Analyse-Stand. Nützlich für den Server; für mich in jeder Trefferzeile wiederholt unnötig, weil derselbe Fingerprint schon im Navigation-Block steht.

3. **DocCommentId**
   z. B. `M:….SiteComponentHandler.ExecuteCommandAsync(…volle Signatur…)`. Das ist die eigentliche Symbol-Identität. Die brauche ich *manchmal* für `get_symbol_body` / `get_call_tree`. In derselben Zeile steht aber schon `Datei.cs:Zeile` und der Kurzname — die volle Signatur hängt dann noch einmal als 200–400 Zeichen hinten dran.

**Zu lang?** Ja, im Markdown. Eine Referenzzeile hat oft **mehr ID als Inhalt**. Beim Incoming-Call-Tree waren das 114 KB vor allem wegen dieser Wiederholung unter jedem Knoten.

**Rauschen?** Im sichtbaren Text ja. Technisch nicht nutzlos: StructuredContent soll genau diese IDs an Folge-Tools durchreichen. Die Workflow-Rule sagt ausdrücklich, Markdown nicht als ID-Quelle zu parsen, *wenn* StructuredContent da ist. Trotzdem werden die IDs **zusätzlich** in den Markdown-Text geschrieben — und der landet in meinem Kontext.

Kurz: **Stabiler Handoff-Schlüssel, falsch platziert.** Einmal pro Antwort oder nur in StructuredContent wäre richtig. In jeder Listenzeile ist es zu viel.
---
die ids braucht der ainetlinter ja intern

WAS da steht ist ja eigentlich egal, oder? der müsste das nur intern irgendwie mappen können?
wenn der die id's wirklich braucht könnte er mappen: "a" = "riesige id" .. und dann halt hochzählen, was meinst du? wäre das irgendwie sinnvoll?
---
Dein Kern stimmt: **für den Agenten ist der Innenaufbau egal.** Der Server muss die Zeichenkette nur eindeutig auf ein Symbol + Snapshot zurückführen können. SHA, Base64-Pfad und volle Signatur in jeder Markdown-Zeile sind dafür nicht nötig.

Zwei Ebenen trennen:

**Intern (Server/StructuredContent)**
Die lange ID darf bleiben: Target, Snapshot, DocCommentId. Das ist Buchhaltung.

**Nach außen (Markdown, und was ich in Folge-Calls tippe)**
Da reicht ein Handle, den der Server wiedererkennt.

Wichtig: Die Tools akzeptieren **heute schon** freundliche Angaben (`Datei.cs:28`, `Klasse.Methode`, `M:…`). Die Riesen-ID ist also kein Muss für den nächsten Call. Sie ist ein *zusätzlicher* Snapshot-gebundener Schlüssel. Deshalb ist „IDs ins Markdown knallen“ die teure Variante, nicht die einzige.

### Alias `a`, `b`, `c` — sinnvoll, aber nicht so roh

Hochzählen ist richtig, nacktes `"a"` eher nicht:

- kollidiert mit Code (`a` als Variable)
- ist sessionsgebunden: in einem neuen Chat oder nach Snapshot-Refresh ist `"a"` tot
- bei parallelen Agenten am selben Daemon gibt es Kollisionen

Besser, immer noch winzig:

- **Antwort-lokal:** `[1]`, `[2]` plus eine Zeile Legende — reicht für denselben Turn
- **Session-lokal:** `s12` / `#12`, gebunden an `connectionId` + Snapshot; bei neuem Snapshot klarer Fehler „Handle ungültig, bitte neu suchen“
- **Nicht:** die volle ID *und* den Alias in dieselbe Zeile (dann hast du null gespart)

### Noch einfacher als Alias-Map

Oft reicht: Markdown nur `Datei:Zeile — Kurzname`. Ich rufe danach `get_symbol_body` mit `SiteComponentHandler.cs:28` auf. Das hat im Test funktioniert. Dann braucht es **gar keine** sichtbare ID.

Eine Map lohnt sich vor allem, wenn du Snapshot-Bindung erzwingen willst (verhindern, dass ich ein Handle aus einem veralteten Baum weiterverwende). Das ist ein echtes Server-Interesse, kein Agenten-Interesse an SHA-256.

### Was an der aktuellen ID *nicht* egal ist

Nur der **DocCommentId-Teil** (`M:Namespace.Typ.Methode(…)`). Der ist eine öffentliche Roslyn-Konvention und als `symbolIdentifier` direkt verwendbar. Base64-slnx und Fingerprint in jeder Zeile sind dagegen reines Duplikat: `targetPath` und Navigation-Block haben dasselbe schon.

**Empfehlung:** Markdown ohne Riesen-IDs. StructuredContent behält die volle ID. Optional kurze Handles `#1`, `#2`, falls du maschinelle Stabilität willst. `"a" = riesige ID` intern ja — nach außen nummeriert und kurz, nicht das Alphabet als API.