# Drittanbieter-Hinweise

## Pillow (Bildverarbeitung)

Ordner-Icon-Ersteller nutzt intern die Python-Bibliothek **Pillow**
(https://python-pillow.org/, ein Fork der aelteren "Python Imaging
Library") fuer die Umwandlung von Bildern in Windows-Icon-Dateien. Pillow
wird beim Bauen der `.exe` fest mit eingepackt.

- Lizenz: MIT-CMU-Lizenz (eine sehr freizuegige Open-Source-Lizenz, dem
  fruehen HPND-Lizenztext von PIL nachempfunden)
- Quelle/Lizenztext: https://github.com/python-pillow/Pillow/blob/main/LICENSE

> Copyright © 1997-2011 by Secret Labs AB, Copyright © 1995-2011 by
> Fredrik Lundh und Mitwirkende; Pillow selbst zusaetzlich Copyright ©
> 2010 by Jeffrey A. Clark und Mitwirkende. Lizenziert unter der oben
> genannten MIT-CMU-Lizenz, siehe Link.

## Tkinter (Programmoberflaeche)

Die Bedienoberflaeche laeuft ueber Tkinter, das fest zur
Python-Standardbibliothek gehoert und unter derselben Lizenz wie Python
selbst (Python Software Foundation License) steht - dafuer ist kein
gesonderter Lizenzhinweis noetig, wird hier der Vollstaendigkeit halber
trotzdem erwaehnt.

## Alle anderen Bestandteile

Der restliche Code von Ordner-Icon-Ersteller (`ordner_icon_ersteller.py`,
`icon_logic.py`, `ai_icon_openai.py`, `ai_icon_google.py`, `ai_common.py`,
das Installationsskript) ist eigener Code ohne weitere
Drittanbieter-Abhaengigkeiten ausser der Python-Standardbibliothek und
Pillow (siehe oben). Die Anbindung an OpenAI und Google (fuer die
optionale KI-Bilderzeugung) erfolgt ueber deren oeffentliche
Programmierschnittstellen (REST-APIs) direkt aus eigenem Code, ohne
zusaetzliche Software-Pakete dieser Anbieter.
