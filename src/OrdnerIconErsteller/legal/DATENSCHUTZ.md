# Datenschutzerklärung

## Grundsatz

Dieses Programm arbeitet vollständig offline, mit einer einzigen Ausnahme: der
optionalen Funktion "Icon per KI erzeugen" (siehe unten). Es gibt keine
Telemetrie, keine Analyse-/Tracking-Dienste und keine automatischen
Update-Abfragen über das Internet.

## Was lokal auf deinem Computer gespeichert wird

- **API-Schlüssel** (OpenAI/Google) werden ausschließlich über den
  Windows-Anmeldeinformations-Manager (Credential Manager) verschlüsselt auf
  deinem eigenen Rechner gespeichert - niemals in einer Klartext-Datei und
  niemals an uns oder Dritte übertragen.
- **Die Zustimmung zum Hinweis über Internetübertragung** (siehe unten) wird
  als einfacher Wert unter `HKEY_CURRENT_USER\Software\OrdnerIconErsteller`
  in der Windows-Registrierung gespeichert.
- **Von der KI erzeugte Icon-Bilder** werden als Bilddatei direkt im von dir
  gewählten Zielordner gespeichert - wie jedes andere von dir ausgewähltes
  Bild auch.

Keine dieser Angaben verlässt deinen Rechner, außer im folgenden Fall.

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

Dieses Programm selbst speichert oder protokolliert die an den Anbieter
gesendeten Inhalte nicht dauerhaft (nur eine kurze Statuszeile im
Programm-eigenen, rein lokalen Log-Fenster während der laufenden Sitzung).

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
