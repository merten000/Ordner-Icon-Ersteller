# Datenschutzerklärung

## Grundsatz

Dieses Programm arbeitet vollständig offline, mit einer einzigen Ausnahme: der
optionalen Funktion "Icon per KI erzeugen" (siehe unten). Es gibt keine
Telemetrie, keine Analyse-/Tracking-Dienste und keine automatischen
Update-Abfragen über das Internet.

## Was lokal auf deinem Computer gespeichert wird

- **API-Schlüssel** (OpenAI/Google) werden lokal im Windows-
  Anmeldeinformations-Manager (Credential Manager) verschlüsselt auf deinem
  eigenen Rechner gespeichert und niemals in einer Klartext-Datei sowie
  niemals an den Autor übermittelt. Bei Nutzung der KI-Funktion wird der
  Schlüssel ausschließlich zur Authentifizierung direkt an den von dir
  ausgewählten Anbieter (OpenAI beziehungsweise Google) gesendet - das ist
  technisch notwendig, damit der Anbieter deine Anfrage überhaupt bearbeiten
  kann (siehe auch den folgenden Abschnitt).
- **Die Zustimmung zum Hinweis über Internetübertragung** (siehe unten) wird
  als einfacher Wert unter `HKEY_CURRENT_USER\Software\OrdnerIconErsteller`
  in der Windows-Registrierung gespeichert.
- **Von der KI erzeugte Icon-Bilder** werden als Bilddatei direkt im von dir
  gewählten Zielordner gespeichert - wie jedes andere von dir ausgewähltes
  Bild auch.

Diese Angaben werden vom Programm nirgendwo sonst hin übertragen, außer im
folgenden Fall (Nutzung der KI-Funktion, dabei auch der API-Schlüssel selbst
- siehe oben).

## Datenübertragung bei "Icon per KI erzeugen"

Nutzt du die optionale Funktion zur KI-gestützten Icon-Erzeugung, werden
Daten an den von dir gewählten Anbieter (OpenAI oder Google) übertragen:

- der von dir eingegebene Text (Prompt), **oder**, falls du das Textfeld
  leer lässt ("Icon ohne Text"), stattdessen die Namen der Dateien/Unterordner
  im Zielordner als Ersatz-Beschreibung;
- bei Google (Gemini) zusätzlich - nur nach deiner ausdrücklichen Bestätigung
  des jeweils konkreten Dateinamens - ein bereits im Zielordner vorhandenes
  Bild als Gestaltungsvorlage.

Diese Übertragung erfolgt direkt von deinem Rechner an den jeweiligen
Anbieter, mit deinem eigenen API-Schlüssel. Für diese Übertragung gilt
zusätzlich die Datenschutzerklärung des jeweiligen Anbieters:

- OpenAI: https://openai.com/policies/privacy-policy
- Google: https://policies.google.com/privacy

Dieses Programm selbst speichert die an den Anbieter gesendeten Inhalte
nicht dauerhaft. Im Programm-eigenen, rein lokalen Log-Fenster erscheint
während der laufenden Sitzung nur eine kurze Statuszeile mit Anbieter und
Vorgang (z. B. "KI-Icon wird erzeugt (Anbieter: Google)...") - Prompt- und
Ordnerinformationen erscheinen dort nicht.

## Deine Kontrolle

- Die Funktion "Icon per KI erzeugen" ist vollständig optional - ohne
  hinterlegten API-Schlüssel wird nichts übertragen.
- Vor der ersten Nutzung erscheint ein Hinweis, der genau beschreibt, was
  übertragen wird; ohne deine Zustimmung wird nichts gesendet.
- Wird im Zielordner ein Bild als Referenz gefunden, fragt das Programm vor
  jeder Übertragung ausdrücklich mit dem konkreten Dateinamen nach - du
  kannst die Übertragung dieses Bildes jederzeit einzeln ablehnen.
- Gespeicherte API-Schlüssel können jederzeit über den Knopf "Löschen" im
  Programm wieder entfernt werden.
