# Learnings

Learnings aus AiNetLinter.

Es gibt well-known Linter wie Microsoft.CodeAnalysis.CSharp und noch andere etablierte.
Diese klinken sich direkt in den Build Prozess ein und Melden Warnings oder Errors.

Es gibt einen Unterschied je Nach Linter-Rule was die eigentliche Ursache ist:
- Grundsätzlich eni "technischer" Fehler, ähnlich eines Syntax Fehlers.
  Dieser muss dann direkt behoben werden.
  Dafür können die etablierten Linter verwendet werden.
- Beobachtungen die eher auf Architektonische Probleme hindeuten und in einem separatem Review Prozess
  im Detail analysiert werden sollten
- Beobachtungen die sich im Gesamt-Zusammenhang als "Fehlalarme" herausstellen.

Im Aktuellen AiNetLinter sind alles sofort harte Fehler.
Auch die Well-Known Lintern haben Rules die als harte Fehler korrigiert werden müssen damit "es grün wird".

In einem Agent-Workflow ist das aber oft eher hinderlich.
Konkretes Beispiel: MaxFileLength oder MaxMethodLength. Der Agent setzt gerade einen spezifischen Roadmap Punkt um. Dann schlägt  MaxMethodLength an, das behebt er in dem er - ohne groß zu überlegen und das Gesamtbild vor Augen zu haben - dieses Problem und teil die Methode auf. Im Folgenden Lauf schlägt MaxFileLength an und das wird mit Partial Klassen gelöst weil das "schnell" geht.
Das Ursächliche Problem wird hier nicht im Gesamtkontext beleuchtet.
Es gibt hier mehrere Lösungen: entweder die Klasse komplett anders aufbauen (Interfaces, extra Klassen, etc.) oder auch zu sagen: "das passt so, kein tech debt, war ein Fehlalarm".
Das eigentliche Ziel des Agenten-Workflow sollte es sein den Task umzusetzen und "grob ordentlich" zu arbeiten. Die Agenten machen das im großen und ganzen schon sehr gut.

Ich stelle mir am Ende folgenden Workflow / folgende Infrastruktur vor:
Die well-known Linter sind integriert (oder auch nicht) aber es sind NUR die Regeln aktiv von denen man sicher sagen kann das hier ein Problem vorliegt und das gemeldete Problem keine ursächlichen Probleme überdeckt.

Die Agenten bringen ihren Task zu Ende.

Direkt danach oder auch in zeitlichem Abstand gibt es Review läufe.
Diese Review Läufe werden durch unser neues Tool unterstützt.
Das Tool ist kein klassischer Linter sondern ein Werkzeug um "Problem-Herde" deterministisch aufzuspüren.
Wie dann damit umgegangen wird obliegt der Agenten/Nutzer Interaktion.
Beispielsweise: "lassen wir so" oder "eine stumpfe mapper methode mit 100 Zeilen ist okay so" oder "das müssen wir komplett refactoren" oder "hier ist ein massiver drift entstanden der mit dem ursprünglichen Konzept nichts mehr zu tun hat".
Das neue Tool ist eine .exe, der übergibt man die sln/slx und eine config (rules) Datei oder es wird definiert das diese rules datei im gleichen verzeichnis wie die sln/slnx liegen muss (wäre das einfachste, wie bisher).
Dem Tool übergibt man zusätzlich einen Ausgabe Ordner.
Das Tool erstellt in dem Ausgabe Ordner für agenten geschriebene MD-Dateien.
Sinnvoll strukturiert.
Diese Dateien sind quasi Wegweiser für "Hier könnte ein Problem sein".
Das kann dann in Agenten/Nutzer Interaktion als Basis für ein Review genommen werden.
Das Tool blockiert nichts, das tool ist nur ein weiteres Werkzeug.
Das Tool hat einige wenige sehr spezifische Regeln die auf typische Agenten-Probleme optimiert sind.

Wenn entscheiden wird: "Dieses Finding ist okay wie es ist" muss es eine Möglichkeit geben das man dieses Finding für die nächsten Läufe ignorieren kann BIS es wieder eine Änderung dran gibt.
Aktuell kann man rules via Code-Kommentare deaktivieren. Das ist aber bullshit weil dieses deaktiveren-Flag niemals wieder entfernt wird. und wenn genau dieser Code-Teil noch mehr driftet bekommt das keiner mit.
Unser neues Tool speichert sich das.
Wir haben dann auch ein archiv und können analysen machen welche rule wann wie getriggert hat, was genau die code stellen waren. wir haben also eine basis für "mess-werte".
wie genau wir das speichern müsste man sich überlegen: vermutlich den ursprünglichen code eindampfen und darüber eine checksum machen und bei zukünftigen audits diese checksummen vergleichen.
wenn gleich wird nichts reportet - wenn ungleich dann wird es reportet.
Das "markieren" das etwas "okay" ist erfolgt über den agenten. es gibt irgendwie die möglichkeit das der agent strukturiert reporten kann.
Eventuell machen wir grundsätzlich auch wieder einen MCP-Server?
dann könnte es reporting funktionen geben.
Wir sollten nur einen weg verfolgen - entweder .exe oder mcp-server.
das tool läuft lokal beim entwickler.

Grundsätzlich nochmal zum AiNetLinter.
Bzgl. Code-Navigation werde ich perspektifisch andere tools verwenden wie "roslyn-codelens-mcp
", hier bin ich noch in der evaluierungsphase.
Ich gehe davon aus das wir AiNetLinter komplett einstellen und archivieren.