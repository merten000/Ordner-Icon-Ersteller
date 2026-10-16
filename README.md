# Ordner-Icon-Ersteller

Ein kleines Programm, um einem einzelnen Ordner ein eigenes Symbol (Icon)
zu geben - z. B. um deine Steam-Spieleordner, Projektordner oder
Video-Sammlungen auf einen Blick auseinanderzuhalten.

## Download

Die Setup-Datei gibt es hier auf GitHub unter [Releases](https://github.com/merten000/Ordner-Icon-Ersteller/releases) und identisch bei [SourceForge](https://sourceforge.net/projects/ordnericonerstellerrelease/files/). Beide Stellen enthalten dieselbe `OrdnerIconErsteller-Setup-2.7.4.exe` samt `SHA256SUMS.txt` zum Vergleich. Quellcode ist absichtlich nicht enthalten.

## Screenshots

![Hauptfenster](screenshots/1%20Titelbild.PNG)

*Hauptfenster: Ziel-Ordner, Bild oder KI-Icon.*

![Einstellungen mit Explorer-Rechtsklick-Menü](screenshots/2%20Explorer%20Men%C3%BC.PNG)

*Einstellungen: Eintrag im Explorer-Rechtsklick-Menü ein- oder ausschalten.*

![KI-Icon ohne Beschreibung: Ordnerinhalt wird geprüft](screenshots/3%20Ki%20erstellt%20Bild%20ohne%20Angaben%2C%20Pr%C3%BCft%20den%20ausgew%C3%A4hlten%20Ordner.PNG)

*KI-Icon ohne Beschreibung: Das Programm prüft den ausgewählten Ordner und fragt, ob ein gefundenes Bild als Vorlage mitgesendet werden soll.*

![Ordner mit neuem Icon im Explorer](screenshots/5%20Ordnerausschnitt.PNG)

*Ergebnis: Der Ordner hat sein eigenes Icon.*

**Das ist noch eine fruehe Version.** Sie kann schon:

- **Eigenes Bild auswaehlen** - wird automatisch in ein richtiges
  Windows-Icon umgewandelt und dem Ordner zugewiesen. Dafuer wird kein
  Internet und kein API-Key gebraucht.
- **Per Text von einer KI erzeugen lassen** - du beschreibst (aktuell per
  Tastatur, Spracheingabe folgt noch), wie das Icon aussehen soll, und eine
  Bild-KI erstellt daraus ein neues Bild, das automatisch als Ordner-Icon
  gesetzt wird. Du kannst zwischen zwei Anbietern waehlen - **OpenAI** oder
  **Google (Gemini)** - je nachdem, wo du schon einen Account/eine
  Zahlungsmethode hast, die funktioniert. Dafuer brauchst du deinen
  eigenen, optionalen API-Key des jeweiligen Anbieters (siehe unten) -
  komplett optional, das Programm funktioniert auch weiterhin ohne.
- **Ordner automatisch durchsuchen lassen (KI schlaegt Icon vor)** - du
  musst gar nichts mehr selbst beschreiben: die KI schaut sich Ordnername
  und die enthaltenen Datei-/Unterordnernamen an (keine Dateiinhalte) und
  macht daraus automatisch einen Vorschlag fuer die Bildbeschreibung, den
  du dir vor dem Erzeugen noch anschauen und anpassen kannst. Nutzt
  denselben Anbieter/API-Key wie "Per Text von KI erzeugen lassen".
- **Rechtsklick-Menue im Explorer** - rechtsklickst du auf einen Ordner
  (oder auf freie Flaeche, waehrend du selbst schon in einem Ordner drin
  bist), erscheint dort "Ordner-Icon erstellen ..." und oeffnet das
  Programm direkt mit diesem Ordner bereits ausgewaehlt. Wird bei der
  Installation automatisch eingerichtet, kein zusaetzlicher Schritt noetig.

Eine weitere Funktion ist in der Programmoberflaeche schon sichtbar, aber
noch ausgegraut, weil sie erst in einer der naechsten Versionen dazukommt:

- **Vorhandenes Bild von einer KI veraendern lassen.**

**Hinweis, falls der Eintrag nicht sofort im Rechtsklick-Menue auftaucht:**
manche Windows-Versionen zeigen zusaetzliche Eintraege wie diesen nicht
mehr direkt an, sondern erst unter "Weitere Optionen anzeigen" ganz unten
im Menue (oder per Umschalt-Taste + Rechtsklick) - das ist eine normale
Windows-Eigenheit, kein Fehler im Programm.

## Installation

`OrdnerIconErsteller-Setup-2.7.4.exe` doppelklicken -> Installations-Assistent
-> Zielordner waehlen (frei waehlbar) -> Installieren. Verknuepfungen
liegen danach im Startmenue und auf dem Desktop. Deinstallation ueber
Windows-Einstellungen -> "Apps" -> "Ordner-Icon-Ersteller", oder ueber die
Verknuepfung "Deinstallieren" im Startmenue-Ordner.

Kein Python noetig, keine weitere Installation - alles ist in der einen
Datei enthalten.

## Bedienung

**Schnellster Weg:** Rechtsklick auf den gewuenschten Ordner -> "Ordner-Icon
erstellen ..." - das Programm oeffnet sich dann direkt mit diesem Ordner
schon ausgewaehlt, Schritt 1+2 unten entfallen dann.

### Eigenes Bild verwenden

1. Programm oeffnen.
2. Ueber "Anderer Ordner ..." den Ordner auswaehlen, der ein neues Icon
   bekommen soll.
3. Auf "Eigenes Bild auswaehlen ..." klicken und ein Bild (PNG, JPG, ...)
   von deinem Rechner auswaehlen.
4. Fertig - das Bild wird automatisch passend zugeschnitten und als
   Ordner-Icon gesetzt.

### Icon per KI erzeugen lassen (optional, kostenpflichtig)

Zuerst oben im Hauptfenster einen der beiden Anbieter auswaehlen (OpenAI
oder Google) - je nachdem, wo du schon einen Account hast oder wo deine
Zahlungsmethode akzeptiert wird. Beide funktionieren technisch gleich gut,
der Unterschied liegt nur darin, welcher Anbieter fuer dich einfacher
einzurichten ist.

**Bei OpenAI:**
1. Eigenen API-Key auf [platform.openai.com](https://platform.openai.com/api-keys)
   erstellen (eigenes Konto noetig, dort auch ein kleines Guthaben
   hinterlegen - dafuer wird eine Visa-, Mastercard- oder Amex-Karte
   gebraucht, Debitkarten gehen auch). **Wichtig:** das ist ein Key von
   OpenAI (beginnt mit `sk-` oder `sk-proj-`), nicht von Anthropic (die
   Keys von dort beginnen mit `sk-ant-` und funktionieren hier nicht) -
   und auch nicht derselbe Account wie ChatGPT: ChatGPT und die API-Plattform
   sind bei OpenAI getrennte Abrechnungssysteme, ein ChatGPT-Guthaben hilft
   hier also nicht.

**Bei Google:**
1. Eigenen API-Key auf [aistudio.google.com/apikey](https://aistudio.google.com/apikey)
   erstellen (eigenes Google-Konto noetig, fuer die Bild-Erzeugung muss dort
   zusaetzlich ein Rechnungskonto mit Zahlungsmethode hinterlegt werden -
   die kostenlose Gemini-Nutzung ohne Zahlungsmethode deckt Bild-Erzeugung
   nicht ab).

**Dann in beiden Faellen:**
2. Den Key ins Feld "API-Key" im Hauptfenster eintragen (wird nirgends
   gespeichert, nur fuer die jeweilige Anfrage verwendet - beim Neustart
   des Programms ist er wieder weg).
3. Ordner auswaehlen, dann entweder auf "Per Text von KI erzeugen lassen"
   (eigene Beschreibung tippen) oder auf "Ordner automatisch durchsuchen
   lassen" (KI macht basierend auf Ordnername/-inhalt selbst einen
   Vorschlag, den du im Dialog noch anpassen kannst) klicken.
4. Beschreibung pruefen bzw. eingeben (je genauer, desto besser das
   Ergebnis), auf "Icon erzeugen" klicken und ein paar Sekunden warten.
5. Fertig - das erzeugte Bild wird automatisch als Ordner-Icon gesetzt. Das
   von der KI erzeugte Bild in normaler (nicht auf Icon-Groesse verkleinerter)
   Groesse liegt danach zusaetzlich sichtbar als
   `Icon-Original (von KI erzeugt).png` direkt im Ordner - falls du es dir
   nochmal in Ruhe angucken oder anderswo weiterverwenden willst. Diese Datei
   kannst du jederzeit loeschen, ohne dass das gesetzte Ordner-Icon
   verschwindet (die stammt aus der bereits erzeugten, separaten Icon-Datei,
   siehe unten).

**Zur Ordner-durchsuchen-Variante:** dabei werden Ordnername sowie die
Namen der obersten Ebene an Dateien/Unterordnern (max. 40 Stueck, bei mehr
mit Hinweis auf die Anzahl der weiteren) an den gewaehlten KI-Anbieter
geschickt - NICHT der Inhalt der Dateien selbst (also z.B. nicht der Text
in einem PDF oder das Bild in einem Foto), und auch keine tieferen
Unterordner-Ebenen.

**Kosten:** werden direkt zwischen deinem eigenen Konto beim gewaehlten
Anbieter und diesem Anbieter abgerechnet - nicht Teil dieses Programms. Die
genauen Preise pro Bild haengen vom gewaehlten Modell/der Qualitaet ab und
aendern sich gelegentlich, aktuelle Zahlen stehen auf der
[OpenAI-Preisseite](https://openai.com/api/pricing/) bzw. der
[Google-Gemini-Preisseite](https://ai.google.dev/gemini-api/docs/pricing).

**Falls sich das Icon im Explorer nicht sofort aendert:** einmal in einen
anderen Ordner wechseln und wieder zurueck, oder den PC neu starten -
Windows aktualisiert Ordner-Icons manchmal erst dann sichtbar. Das ist
eine Windows-Eigenheit, kein Fehler im Programm.

## Wie das technisch funktioniert

Windows erlaubt es, jedem Ordner ueber eine kleine, versteckte Datei
(`desktop.ini`) ein eigenes Icon zuzuweisen. Das Programm erzeugt dafuer:

- eine versteckte Icon-Datei (`.ordner_icon.ico`) direkt im Ordner selbst,
  in allen gaengigen Groessen (16 bis 256 Pixel), damit das Icon in jeder
  Explorer-Ansicht scharf aussieht,
- die passende `desktop.ini`, die Windows auf diese Icon-Datei verweist.

Beide Dateien liegen direkt im Ordner selbst (nicht irgendwo zentral) -
verschiebst oder kopierst du den ganzen Ordner (z. B. auf einen anderen
Rechner oder einen USB-Stick), zieht das eigene Icon automatisch mit um.

**Wichtig, falls dir die beiden neuen Dateien auffallen:** `.ordner_icon.ico`
und `desktop.ini` sind normal und gehoeren zur Icon-Funktion - lass sie
im Ordner liegen. Loeschst du sie, verschwindet einfach nur das eigene
Icon wieder, der restliche Ordnerinhalt bleibt unberuehrt.

Diese beiden Dateien werden absichtlich als "ausgeblendet" markiert, damit
sie nicht zwischen den eigentlichen Ordnerinhalten auftauchen - im Explorer
siehst du sie nur, wenn dort unter "Ansicht" -> "Ausgeblendete Elemente"
aktiviert ist. Die per KI erzeugte `Icon-Original (von KI erzeugt).png`
(siehe oben) ist dagegen bewusst NICHT ausgeblendet, die findest du also
immer ganz normal im Ordner.

## Unterstuetzte Bildformate

PNG, JPG/JPEG, BMP, GIF, WEBP - alles, was ueblicherweise als Foto oder
Grafik vorliegt. Nicht-quadratische Bilder werden automatisch mittig auf
ein Quadrat gesetzt (nicht verzerrt).

## Hinweis zu Windows-Warnungen

`OrdnerIconErsteller-Setup-2.7.4.exe` ist
nicht digital signiert. Eine SmartScreen-Warnung beim ersten Start ist
normal bei selbst gebauten Programmen, kein Hinweis auf Schadsoftware.
