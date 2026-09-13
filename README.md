# Ordner-Icon-Ersteller (Version 2.1.1 – komplette Neuentwicklung)

## Änderungen nach der ChatGPT-Prüfung der KI-Icon-Funktion (v2.1.0 → v2.1.1)

Die Prüfung von Commit `6cff972` (KI-Icon-Funktion) fand vier
freigabeblockierende Punkte sowie mehrere kleinere Punkte. Alle wurden
behoben:

1. **Zielordner-Wechsel während laufender Anfrage** (`MainForm.cs`):
   `GenerateAiIconAsync()` liest Ordner, Anbieter und Prompt jetzt EINMAL zu
   Beginn in lokale Variablen und verwendet ausschließlich diese – nicht mehr
   `_txtFolder.Text` nach dem `await`. Zusätzlich sind während der laufenden
   Anfrage „Ordner wählen", „Icon setzen" und „Icon entfernen" gesperrt
   (`SetAiOperationControlsEnabled`).
2. **Ungesichertes Überschreiben des KI-Originals** (`MainForm.cs`): Die
   Antwort wird zuerst in eine eindeutige temporäre Datei geschrieben, ein
   vorhandenes altes Original wird nur umbenannt (Sicherungsdatei) statt
   gelöscht, und erst danach wird die neue Datei an die endgültige Stelle
   verschoben. Schlägt das Setzen des Icons anschließend fehl, wird das alte
   PNG-Original aus der Sicherung wiederhergestellt, damit gespeichertes
   „Original" und tatsächlich gesetztes Icon nicht auseinanderlaufen.
3. **Tests löschten echte API-Schlüssel** (`ApiKeyStoreTests.cs`,
   `ApiKeyStore.cs`): `ApiKeyStore` stellt jetzt interne, nur für Tests
   gedachte `...ForTesting`-Methoden mit frei wählbarem Target-Namen bereit.
   Die Tests verwenden ausschließlich einen pro Testlauf per GUID eindeutigen
   Namen (`OrdnerIconErsteller:UnitTest:<guid>`) und fassen die echten
   Produkt-Ziele `OrdnerIconErsteller:OpenAI`/`OrdnerIconErsteller:Google` nie
   mehr an.
4. **Veraltetes OpenAI-Modell**: `gpt-image-1` (laut aktueller OpenAI-
   Dokumentation „Deprecated") wurde durch `gpt-image-2.5-flare` ersetzt (für
   schnelle Alltags-Bilderzeugung empfohlen). Der nicht mehr zutreffende
   Parameter `response_format` wurde entfernt, stattdessen wird `output_format:
   "png"` gesetzt.

Kleinere, ebenfalls behobene Punkte:

- API-Antworten werden jetzt defensiv mit `TryGetProperty`/Typprüfungen
  gelesen statt mit `GetProperty`, eine maximale Antwortgröße (30 MB) wird
  durchgesetzt, und jedes gelieferte Bild wird vor dem dauerhaften Speichern
  vollständig dekodiert/geprüft (`AiIconService.ValidateDecodableImage`).
- Der Gemini-Parser überspringt jetzt ausdrücklich „Thinking"-Bildteile
  (`thought: true`) und akzeptiert nur `inlineData` mit einem `image/*`-
  MIME-Typ, statt ungeprüft den ersten Bildteil zu verwenden.
- Die P/Invoke-Deklaration von `CredFree` wurde von `bool` auf `void`
  korrigiert (die native Windows-Funktion hat den Rückgabetyp `VOID`).
  Zusätzlich werden verwaltete Kopien des Klartext-Schlüssels jetzt mit
  `CryptographicOperations.ZeroMemory` sofort überschrieben, `Has...Key()`
  kopiert den Klartext dafür nicht mehr, und die maximale Credential-Blob-
  Größe (2560 Byte) wird vor dem Speichern geprüft.
- Der Zustimmungsdialog (`AiConsentService`) nennt jetzt ausdrücklich, dass
  eine einzelne Google-Erzeugung bis zu zwei kostenpflichtige Anfragen
  auslösen kann, und ein fehlgeschlagenes `CreateSubKey` wird nicht mehr
  stillschweigend ignoriert.

## Neue Funktion: Icon per KI erzeugen (v2.0.5 → v2.1.0)

Neu hinzugekommen: ein Ordner-Icon kann jetzt statt eines eigenen Bildes auch
per Texteingabe ("Prompt") von einer KI erzeugt werden, wahlweise über OpenAI
oder Google (Gemini). Wichtig dabei:

- **Das ist die einzige Stelle im gesamten Programm, die bewusst Daten über
  das Internet an einen externen Dienst sendet** (den eingegebenen Prompt-
  Text) – alle anderen Funktionen bleiben vollständig offline. Das steht nicht
  im Widerspruch zur Grundregel "kein externer Prozessaufruf" (siehe unten):
  es wird weiterhin niemals `cmd.exe`, `powershell.exe` o. ä. gestartet,
  sondern ausschließlich die eingebaute .NET-Klasse `HttpClient` für die
  HTTPS-Anfrage direkt an die APIs von OpenAI bzw. Google verwendet.
- Die API-Schlüssel für beide Anbieter werden **ausschließlich im
  Windows-Anmeldeinformations-Manager** gespeichert (`ApiKeyStore.cs`, per
  P/Invoke auf `advapi32.dll` – `CredWrite`/`CredRead`/`CredDelete`), nicht
  als Klartextdatei im Programmordner.
- Anbieter-Auswahl und API-Schlüssel-Eingabe sitzen **direkt im Hauptfenster**
  im Bereich "Icon per KI erzeugen" (kein separater Dialog) - nach Merten's
  praktischem Test wurde das bewusst so vereinfacht.
- Beim allerersten Speichern eines Schlüssels zeigt das Programm **einmalig**
  einen Hinweis, dass dabei Daten übertragen werden und je nach Anbieter
  Kosten entstehen können (`AiConsentService.cs`). Der Zustimmungs-Status
  wird dauerhaft unter `HKCU\Software\OrdnerIconErsteller` gemerkt, damit der
  Hinweis danach nicht bei jedem Mal erneut erscheint.
- Das erzeugte Bild wird **dauerhaft im Zielordner selbst gespeichert**
  (Dateiname `Icon-Original (von KI erzeugt).png`) und durchläuft anschließend
  exakt dieselbe, bereits bestehende und getestete Pipeline wie ein manuell
  ausgewähltes Bild (`FolderIconService.SetFolderIcon`) – an dieser Kernlogik
  wurde nichts geändert. Ein erneuter Lauf für denselben Ordner überschreibt
  die vorherige KI-erzeugte Datei.
- **Zuverlässigere Google-Bilderzeugung bei kurzen Prompts**: Gemini-Bildmodelle
  antworten bei sehr kurzen/mehrdeutigen Eingaben (z. B. nur "Katze") manchmal
  nur mit Text statt einem Bild (kein Fehler, HTTP 200 - das Modell "fragt
  nach" statt zu erzeugen). Dagegen: der Prompt wird automatisch in einen
  ausformulierten Satz eingebettet (laut Googles eigenem Prompting-Leitfaden
  zuverlässiger als Stichworte), zusätzlich verlangt eine `system_instruction`
  ausdrücklich ausschließlich ein Bild als Antwort. Kommt trotzdem nur Text
  zurück, wird EINMAL automatisch mit einer noch deutlicheren Formulierung
  wiederholt; scheitert auch das, zeigt die Fehlermeldung die Text-Antwort
  des Modells zur Einordnung an.
- Neue Dateien: `ApiKeyStore.cs` (Credential-Manager-Zugriff),
  `AiIconService.cs` (HTTP-Anbindung an OpenAI/Google), `AiConsentService.cs`
  (einmaliger Hinweis, Zustimmungs-Flag unter HKCU). Geänderte Datei:
  `MainForm.cs` (neuer Bereich "Icon per KI erzeugen (optional)" mit
  Anbieter-Wahl, Schlüssel-Eingabe und Prompt-Feld direkt im Hauptfenster).

**Bewusst nicht automatisiert testbar:** Die eigentlichen KI-API-Aufrufe
(`AiIconService`) senden echte, potenziell kostenpflichtige Anfragen an
OpenAI/Google und sind deshalb nicht Teil der automatisierten Tests. Der
Speicher-Rundlauf (`ApiKeyStore`: Speichern/Lesen/Löschen im
Anmeldeinformations-Manager) wird dagegen automatisiert getestet
(`ApiKeyStoreTests.cs`).

## Änderungen nach der 5. ChatGPT-Prüfung (v2.0.4 → v2.0.5)

Die Gegenprüfung von v2.0.4 (Commit `28c8758`) bestätigte beide vorherigen
Fixes, fand aber einen seltenen, aber weiterhin datenverlustgefährdenden
Restfehler:

1. **Safety-Datei konnte gelöscht werden, wenn die Selbstheilung des
   Dateitauschs SELBST scheitert**: `SwapIntoPlace`/`SwapFromBackupIntoPlace`
   holen die vorherige Datei bei einem gescheiterten zweiten Schritt zwar
   selbst zurück – schlägt aber auch dieser Rückhol-Versuch fehl (z. B. weil
   inzwischen zusätzlich eine Sperre dazukommt), wirft die Hilfsfunktion eine
   `AggregateException`, BEVOR die aufrufende Methode ihr „getauscht"/
   „behandelt"-Merkmal setzt. Der äußere Rollback-Code führte deshalb für
   diese Datei keinen weiteren Schritt aus, und der äußere `finally`-Block
   löschte anschließend die Safety-Datei als „nicht mehr benötigt" – obwohl
   sie die einzige verbliebene Kopie der vorherigen Datei war. Behoben durch
   einen zusätzlichen `out`-Parameter (`safetyPathMustBePreserved`), den die
   Tausch-Hilfsfunktionen auch beim Werfen einer Ausnahme zuverlässig setzen
   (C# aktualisiert „out"-Parameter unabhängig vom Kontrollfluss) – der
   äußere Code wertet dieses Signal jetzt unabhängig von den bisherigen
   „erfolgreich getauscht"-Merkmalen aus und bewahrt die betroffene
   Safety-Datei in diesem Fall zuverlässig auf, statt sie zu löschen.

**Bekannte Testabdeckungs-Lücke:** Dieser doppelte Fehlerpfad (zweiter
Dateitausch-Schritt UND die anschließende interne Selbstheilung scheitern
beide) lässt sich ohne eine grundlegende Testinfrastruktur-Änderung (z. B.
eine austauschbare Abstraktion über `File.Move`) nicht zuverlässig und ohne
zeitbasierte, potenziell instabile Tricks in einem automatisierten Test
erzwingen. Die Korrektur wurde stattdessen durch sorgfältige Code-Analyse
verifiziert (der `out`-Parameter wird in jedem Codepfad vor jedem möglichen
`throw` gesetzt). Diese Lücke ist bewusst dokumentiert statt mit einem
unzuverlässigen Test „simuliert".

## Änderungen nach der 4. ChatGPT-Prüfung (v2.0.3 → v2.0.4)

Die Gegenprüfung von v2.0.3 (Commit `0067c3b`) bestätigte alle 4 vorherigen
Fixes, fand aber 2 neue freigabeblockierende Fehler in `FolderIconService`:

1. **Eine vorhandene Statusdatei wurde beim Setzen nie validiert**: „Icon
   setzen" prüfte bislang nur `File.Exists(markerPath)` und übersprang bei
   jeder vorhandenen Datei mit diesem Namen die komplette Sicherungsphase –
   auch wenn die Datei beschädigt, fremd oder manipuliert war. Dadurch
   konnten fremde `desktop.ini`/`FolderIcon.ico`-Dateien ohne jede Sicherung
   überschrieben werden. Jetzt wird ein vorhandener Marker – genau wie beim
   Entfernen – zwingend mit `ReadMarker()` validiert, inklusive Prüfung,
   dass alle darin referenzierten Sicherungsdateien tatsächlich existieren.
   Jede Abweichung bricht sofort ab, bevor irgendetwas verändert wird.
2. **Ein Fehler mitten in einem zweistufigen Dateitausch wurde vom äußeren
   Rollback nicht erkannt**: Scheiterte beim Tauschen oder Wiederherstellen
   der zweite von zwei internen Schritten (nachdem die vorherige Datei
   bereits erfolgreich beiseite verschoben wurde), setzte die aufrufende
   Methode ihr „erfolgreich getauscht"-Merkmal nie, wodurch das äußere
   Rollback genau diesen begonnenen Tausch übersprang – die vorherige Datei
   landete dann nur noch in einer Sicherungskopie, die anschließend als
   „nicht mehr benötigt" gelöscht wurde. Die betroffenen Tausch-Bausteine
   sind jetzt selbst ausnahmesicher: Scheitert der zweite Schritt, holen sie
   die vorherige Datei sofort selbst zurück, bevor der Fehler weitergegeben
   wird.

Drei neue automatisierte Tests decken beide Fälle gezielt ab: eine
beschädigte vorhandene Statusdatei beim Setzen (inkl. fremder Dateien und
einer strukturell gültigen, aber auf eine fehlende Sicherung verweisenden
Statusdatei), sowie ein gezielt in der Mitte eines Wiederherstellungs-
Tauschs erzwungener Fehlschlag beim Entfernen.

## Änderungen nach der 3. ChatGPT-Prüfung (v2.0.2 → v2.0.3)

Die Gegenprüfung von v2.0.2 (Commit `491544b`) hat bestätigt, dass die vier
vorherigen Fixes für ihre jeweiligen Reproduktionsfälle funktionieren, aber
vier neue freigabeblockierende Fehler gefunden – alle rund um die
Vollständigkeit der Transaktion und der Zustandsprüfung. `FolderIconService`
wurde daraufhin grundlegend überarbeitet:

1. **Ein Löschfehler beim Entfernen konnte stillschweigend als Erfolg
   gelten**: War z. B. eine Datei gerade exklusiv gesperrt, wurde das
   frühere `TryDelete` den Fehler verschlucken. Löschungen laufen jetzt über
   reversible „Verschieben statt Löschen"-Bausteine
   (`MoveToDeleteSafetyIfExists`/`UndoMoveToDeleteSafety`); ein Fehlschlag
   wird nicht mehr abgefangen, sondern wirft sichtbar und lässt den Ordner
   unverändert – der Vorgang bleibt danach wiederholbar.
2. **„Icon entfernen" war nicht transaktional**: bisher wurde z. B. die
   desktop.ini bereits wiederhergestellt (und ihre Sicherung dabei
   „verbraucht"), bevor geprüft wurde, ob auch die zweite Sicherung (Icon)
   überhaupt existiert. Jetzt prüft eine Vorabkontrolle zwingend, dass ALLE
   laut Statusdatei nötigen Sicherungsdateien vorhanden sind, bevor
   überhaupt irgendetwas verändert wird.
3. **Eine beschädigte oder manipulierte Statusdatei wurde unbesehen
   akzeptiert**: Jetzt wird die Formatversion exakt geprüft, boolesche Werte
   müssen exakt `"0"`/`"1"` sein (kein stiller `false`-Fallback), und
   Attribut-Werte werden gegen eine Whitelist plausibler `FileAttributes`
   geprüft – jede Abweichung bricht kontrolliert ab, ohne etwas zu
   verändern.
4. **Das Zurückrollen bei „Icon setzen" deckte nicht alle Schritte ab**:
   Bilderzeugung, das Schreiben der desktop.ini, beide Dateitausche und
   beide Attributänderungen laufen jetzt als EINE zusammenhängende
   Transaktion mit vollständigem, in umgekehrter Reihenfolge ablaufendem
   Rollback bei jedem Fehlschlag – nicht nur bei einem fehlgeschlagenen
   Dateitausch.
5. **Zwei gleichzeitig gestartete Instanzen konnten sich gegenseitig
   Sicherung/Zustand zerstören** (z. B. über zwei Rechtsklick-Menü-Aufrufe
   kurz hintereinander): Pro Ordner wird jetzt während „Icon setzen"/„Icon
   entfernen" eine eigene Sperrdatei exklusiv gehalten; eine zweite Instanz
   erhält sofort einen klaren Fehler statt loszulaufen.

Schlägt das automatische Zurückrollen selbst fehl (z. B. weil eine Datei
gerade zusätzlich gesperrt ist), werden die dafür nötigen Sicherungskopien
jetzt bewusst NICHT gelöscht – der Fehler benennt das explizit und weist auf
eine manuelle Prüfung hin, statt Datenverlust zu riskieren
(`System.AggregateException`).

Sieben neue automatisierte Tests decken die aus dem Prüfbericht geforderten
Regressionsfälle ab: gesperrte Datei beim Entfernen, fehlende zweite
Sicherung, beschädigte Statusdatei (falsche Formatversion, ungültiger
boolescher Wert, unzulässige Attribut-Bits), fehlgeschlagene Bilderzeugung
nach bereits erfolgreicher Sicherung, sowie zwei parallel gestartete
Instanzen auf demselben Ordner.

Außerdem behoben: die Kontextmenü-Statusprüfung
(`ContextMenuService.IsRegistered`) vergleicht den hinterlegten Befehl jetzt
exakt statt nur als Teilstring, damit ein fremder Befehl, der den EXE-Pfad
zufällig irgendwo enthält, nicht fälschlich als „registriert" gilt.

## Änderungen nach der 2. ChatGPT-Prüfung (v2.0.1 → v2.0.2)

Die Gegenprüfung von v2.0.1 (Commit `c44156d`) hat bestätigt, dass die
`desktop.ini`-Kodierung jetzt korrekt ist, aber vier weitere freigabeblockierende
Fehler in `FolderIconService` gefunden, die sich alle um denselben Kernpunkt
drehen: der behauptete Sicherungs-/Rollback-Schutz griff in der Praxis noch
nicht vollständig. Alle vier sind jetzt behoben:

1. **„Icon entfernen" konnte fremde Dateien löschen**: In einem Ordner, den das
   Programm nie bearbeitet hat, löschte „Icon entfernen" trotzdem eine
   zufällig vorhandene fremde `desktop.ini`/`FolderIcon.ico`. Jetzt prüft
   `RemoveFolderIcon` zuerst zwingend, ob eine von diesem Programm angelegte
   Statusdatei existiert – fehlt sie, wird **gar nichts** verändert und eine
   klare Fehlermeldung angezeigt.
2. **Das Einsetzen war nicht atomar**: `File.Copy(..., overwrite: true)` kann
   mittendrin abbrechen und eine gekürzte Datei hinterlassen. Jetzt werden
   neue Dateien über `File.Move` (Umbenennen statt Kopieren) eingesetzt – auf
   demselben Laufwerk ein einziger, praktisch unteilbarer Schritt. Schlägt
   einer von zwei nötigen Tauschvorgängen fehl, wird der bereits erfolgte
   zurückgerollt, sodass nie ein halbfertiger Zustand zurückbleibt.
3. **Die wiederhergestellten Dateien verloren ihre echten Attribute**: Beim
   Entfernen wurden Dateien bisher pauschal auf `Hidden|System` gesetzt statt
   auf ihre tatsächlichen ursprünglichen Attribute. Die echten Attribute
   werden jetzt exakt in der Statusdatei gespeichert und beim Entfernen exakt
   wiederhergestellt.
4. **Ein Fehler bei der Sicherung selbst konnte die Originaldatei verändern**:
   Bisher wurden die Attribute der Originaldatei entfernt, bevor die Sicherung
   überhaupt angelegt war – schlug das Kopieren fehl, blieb die Originaldatei
   in verändertem Zustand zurück. Jetzt wird die Originaldatei zu keinem
   Zeitpunkt angefasst; erst wenn die Sicherungskopie vollständig und sicher
   an ihrem endgültigen Platz liegt, gilt der Vorgang als abgeschlossen.

Außerdem wurden die in der Prüfung genannten kleineren Punkte behoben: die
Kontextmenü-Statusprüfung (`ContextMenuService.IsRegistered`) erkennt jetzt
auch einen veralteten Eintrag (z. B. nach Verschieben der EXE) korrekt als
„nicht registriert", und die README-Formulierung zur alten Version wurde
neutraler beschrieben (siehe unten). Fünf neue automatisierte Tests decken
gezielt die gemeldeten Fehlerpfade ab (Entfernen ohne Statusdatei, fehlgeschlagener
Tausch, fehlgeschlagene Sicherung).

**Offen, bewusst zurückgestellt (kein Blocker laut Prüfbericht):** Beim Setzen
eines neuen Icons wird eine vorhandene `desktop.ini` komplett ersetzt statt nur
die Icon-Schlüssel darin gezielt zu ändern. Andere, unabhängig von diesem
Programm gesetzte Shell-Anpassungen in derselben Datei blieben dadurch bis zum
Entfernen unwirksam (der Inhalt wird aber weiterhin vollständig gesichert und
später exakt wiederhergestellt – es geht nichts verloren).

## Änderungen nach der 1. ChatGPT-Prüfung (v2.0.0 → v2.0.1)

ChatGPT hat drei freigabeblockierende Fehler in v2.0.0 (Commit `f9a1f0e`) gefunden,
alle sind jetzt behoben:

1. **`desktop.ini` wurde nicht erkannt**: sie wurde als UTF-8 geschrieben, die
   klassische Windows-INI-API (`GetPrivateProfileString`) erkennt UTF-8 aber
   nicht zuverlässig. Jetzt wird Unicode (UTF-16LE mit BOM) verwendet.
2. **Vorhandene `desktop.ini`/`FolderIcon.ico`/Ordnerattribute wurden ohne
   Sicherung überschrieben**: `FolderIconService` sichert jetzt vor der
   allerersten Änderung an einem Ordner dessen Originalzustand (versteckte
   `.bak`-Dateien + ein kleines Marker-File) und stellt beim „Icon entfernen"
   genau diesen Originalzustand wieder her, statt einfach zu löschen.
3. **Kein Rollback bei Schreibfehlern**: Icon und `desktop.ini` werden jetzt
   erst vollständig in temporäre Dateien geschrieben und erst danach an ihren
   endgültigen Platz kopiert, damit bei einem Fehler mittendrin kein
   halbfertiger Zustand im Zielordner zurückbleibt.

Außerdem: die Registry-Statusabfrage (`ContextMenuService.IsRegistered`) kann
jetzt nicht mehr den Programmstart abbrechen (try/catch ergänzt), die zwei
unnötigen NuGet-Pakete (`System.Drawing.Common`, `Microsoft.Win32.Registry` –
beide bereits über das Windows-Desktop-Framework verfügbar) wurden entfernt,
und es gibt jetzt ein Testprojekt mit automatisierten Tests (siehe unten).

Windows-Tool, um einem Ordner per eigenem Bild ein individuelles Icon zu geben,
inklusive Eintrag im Explorer-Rechtsklick-Menü.

Diese Version ist ein **kompletter Neuanfang** (kein Code aus v1.0.0 wiederverwendet)
mit dem Ziel, die Antivirus-Fehlalarme der Vorversion nicht zu wiederholen.

## Warum die alte Version Ärger gemacht hat

Die alte Setup.exe rief zur Installation externe Prozesse wie `cmd.exe`,
`powershell.exe` und `reg.exe` auf und nahm Registry-Änderungen über solche
Subprozesse vor, teilweise mit Anforderung von Admin-Rechten. Unabhängig von
der tatsächlichen Absicht ist genau das ein Muster, das Virenscanner
zuverlässig als verdächtig einstufen.

## Was in dieser Version bewusst anders ist

- **Kein einziger externer Prozessaufruf.** Weder `cmd.exe` noch `powershell.exe`
  noch `reg.exe` werden irgendwo gestartet.
- **Keine Administratorrechte.** Der Rechtsklick-Menü-Eintrag wird ausschließlich
  unter `HKEY_CURRENT_USER\Software\Classes\...` angelegt (gilt nur für das
  eigene Benutzerkonto), direkt über die .NET-Registry-API
  (`Microsoft.Win32.Registry`), nicht über einen Registry-Editor-Aufruf.
- **Kein Internetzugriff, kein Nachladen von Code zur Laufzeit** – mit einer
  bewussten, klar abgegrenzten Ausnahme: die optionale KI-Icon-Funktion sendet
  den eingegebenen Prompt-Text an OpenAI bzw. Google, wenn der Benutzer sie
  aktiv nutzt (siehe Abschnitt oben). Alle anderen Funktionen bleiben
  vollständig offline, und es wird dabei zu keinem Zeitpunkt Code
  nachgeladen oder ausgeführt – nur ein fertiges Bild wird empfangen.
- **Standard-Windows-Mechanismus für Ordner-Icons** (`desktop.ini` +
  versteckte `.ico`-Datei im Zielordner) statt irgendwelcher Tricks.

## Funktionsumfang Version 1 (MVP)

1. Ordner auswählen (oder per Rechtsklick-Menü direkt übergeben)
2. Bild auswählen (PNG, JPG, BMP, ICO)
3. „Icon setzen" → erzeugt eine mehrstufige `.ico`-Datei (16/32/48/256 px) aus
   dem Bild und hinterlegt sie im Zielordner
4. „Icon entfernen" → macht die Änderung rückgängig
5. „Ins Rechtsklick-Menü integrieren" / „entfernen" → fügt einen Eintrag
   „Ordner-Icon ändern..." zum Rechtsklick-Menü von Ordnern hinzu (nur für den
   aktuellen Benutzer)
6. „Icon per KI erzeugen (optional)" → Beschreibung eingeben, Anbieter
   (OpenAI/Google) wählen, „Icon per KI erzeugen" klicken – nutzt danach
   dieselbe Icon-Pipeline wie ein manuell ausgewähltes Bild (siehe Abschnitt
   "Neue Funktion: Icon per KI erzeugen" oben)

**Noch nicht enthalten**: automatischer Icon-Vorschlag (ohne eigenen Prompt).
Ein Installer (Inno Setup) ist vorhanden - siehe Abschnitt "Installer bauen"
weiter unten. Das README/Disclaimer-Tab im Programm selbst ist bewusst auf
später verschoben (siehe "Nächste Schritte" unten).

## Projektstruktur

```
OrdnerIconErsteller/
├── src/
│   └── OrdnerIconErsteller/
│       ├── OrdnerIconErsteller.csproj
│       ├── Program.cs
│       ├── MainForm.cs
│       ├── FolderIconService.cs
│       ├── IconWriter.cs
│       ├── ContextMenuService.cs
│       ├── ApiKeyStore.cs
│       ├── AiIconService.cs
│       └── AiConsentService.cs
└── tests/
    └── OrdnerIconErsteller.Tests/
        ├── OrdnerIconErsteller.Tests.csproj
        ├── IconWriterTests.cs
        ├── FolderIconServiceTests.cs
        └── ApiKeyStoreTests.cs
```

## Bauen (auf deinem Windows-PC, .NET 8 SDK vorausgesetzt)

Wichtiger Hinweis: Ich konnte dieses Projekt in meiner Cloud-Umgebung **nicht
kompilieren/testen**, weil mein Sandbox-Netzwerk keinen Zugriff auf
Microsoft-/NuGet-/Ubuntu-Paketserver hat (alle Downloadversuche wurden vom
Sicherheits-Proxy mit 403 blockiert). Der Code ist sorgfältig geschrieben und
sollte sich normal bauen lassen – bitte einmal `dotnet build` laufen lassen und
mir eventuelle Fehlermeldungen schicken, falls etwas nicht sofort durchläuft.

```powershell
cd src\OrdnerIconErsteller
dotnet build
```

(Es gibt jetzt auch `OrdnerIconErsteller.sln` im Hauptordner - `dotnet build`
direkt dort baut Haupt- und Testprojekt zusammen.)

Fertige, startbare EXE erzeugen (einzelne Datei, keine separate .NET-Installation
beim Nutzer nötig):

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\publish
```

Die EXE liegt danach unter `src\OrdnerIconErsteller\publish\OrdnerIconErsteller.exe`.

## Installer bauen (Inno Setup)

Voraussetzung: [Inno Setup](https://jrsoftware.org/isinfo.php) ist auf dem
Windows-Rechner installiert (kostenlos).

1. Zuerst die self-contained EXE wie oben beschrieben erzeugen:
   ```powershell
   cd src\OrdnerIconErsteller
   dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\publish
   ```
2. Danach das Installer-Skript kompilieren, entweder per Rechtsklick auf
   `installer\OrdnerIconErsteller.iss` -> "Compile", oder über die
   Kommandozeile:
   ```powershell
   cd installer
   iscc OrdnerIconErsteller.iss
   ```
3. Der fertige Installer liegt danach unter
   `installer\output\OrdnerIconErsteller-Setup-2.1.1.exe`.

Bewusste Entscheidungen im Installer-Skript (Details als Kommentare direkt in
`installer\OrdnerIconErsteller.iss`):

- Installation nur für den aktuellen Benutzer nach
  `%LOCALAPPDATA%\Programs\Ordner-Icon-Ersteller` - **keine
  Administratorrechte, keine UAC-Abfrage**, passend zum Grundprinzip des
  gesamten Programms (alles läuft über HKCU).
- Der Explorer-Rechtsklick-Menü-Eintrag wird weiterhin ausschließlich über
  den Knopf "Ins Rechtsklick-Menü integrieren" im Programm selbst gesetzt -
  der Installer registriert bei der Installation nichts automatisch.
- Beim Deinstallieren wird der Registry-Zweig des Menü-Eintrags
  (`HKCU\Software\Classes\Directory\shell\OrdnerIconErsteller`) vollständig
  entfernt, damit kein toter Menüeintrag auf eine gelöschte EXE zeigt.
- Eine feste `AppId` (GUID) im Skript sorgt dafür, dass eine neue Version die
  alte sauber ersetzt, statt einen zweiten Eintrag in "Apps & Features"
  anzulegen - diese GUID darf sich zwischen Versionen nicht ändern.

**Praktisch getestet:** Merten hat den Installer unter Windows erfolgreich
getestet (Installation ohne Sicherheitsabfrage, Virenscanner-Reaktion,
Funktion nach Installation, Deinstallation inkl. Registry-Aufräumung).
Zusätzlich hat ChatGPT das Skript selbst code-geprüft (Commit `621f37d`,
Nachprüfung nach Korrekturen: Commit `65b88b6`) - keine offenen Blocker mehr.

## Automatisierte Tests ausführen

Vom Projekt-Hauptordner aus (nicht aus `src\OrdnerIconErsteller`):

```powershell
dotnet test
```

Das baut Haupt- und Testprojekt und führt die xUnit-Tests aus (19 Tests
insgesamt): der Sicherung/Wiederherstellung-Rundlauf mit exakter
Attribut-Wiederherstellung, die Fehlerpfadtests aus der 2. ChatGPT-Prüfung
(Entfernen ohne Statusdatei, fehlgeschlagener Dateitausch, fehlgeschlagene
Sicherung), die Regressionstests aus der 3. ChatGPT-Prüfung (gesperrte Datei
beim Entfernen, fehlende zweite Sicherung, beschädigte Statusdatei in drei
Varianten, fehlgeschlagene Bilderzeugung nach erfolgreicher Sicherung, zwei
parallele Instanzen auf demselben Ordner), die Regressionstests aus der
4. ChatGPT-Prüfung (beschädigte/fremde Statusdatei beim Setzen in zwei
Varianten, gezielt in der Mitte eines Wiederherstellungs-Tauschs erzwungener
Fehlschlag) sowie drei Tests für den Credential-Manager-Rundlauf der
KI-Icon-Funktion (`ApiKeyStoreTests.cs`: Speichern/Lesen/Löschen im Rundlauf,
doppeltes Löschen ohne Fehler, Ablehnung eines zu langen Schlüssels – alle
drei ausschließlich über die internen `...ForTesting`-Methoden mit einem pro
Testlauf eindeutigen Target-Namen, sodass sie nie die echten gespeicherten
OpenAI-/Google-Schlüssel berühren, siehe oben "Änderungen nach der
ChatGPT-Prüfung der KI-Icon-Funktion").

## Test-Checkliste vor der ChatGPT-Prüfung

- [ ] `dotnet build` läuft ohne Fehler durch
- [ ] Programm startet ohne Argumente (leeres Formular)
- [ ] Ordner + Bild auswählen, „Icon setzen" → Explorer zeigt neues Icon (ggf. F5)
- [ ] „Icon entfernen" macht es rückgängig
- [ ] „Ins Rechtsklick-Menü integrieren" → Rechtsklick auf einen beliebigen
      Ordner zeigt „Ordner-Icon ändern...", Klick öffnet das Programm mit
      bereits eingetragenem Ordner
- [ ] „Aus Rechtsklick-Menü entfernen" entfernt den Eintrag wieder
- [ ] Kein UAC-Admin-Dialog erscheint an irgendeiner Stelle
- [ ] Im Bereich „Icon per KI erzeugen" einen (ggf. Test-)API-Schlüssel
      eingeben und „Speichern" klicken → beim allerersten Mal erscheint der
      Kosten-/Datenübertragungs-Hinweis, danach nicht mehr
- [ ] „Icon per KI erzeugen" mit einem echten API-Schlüssel und einer
      Beschreibung testen (einmal OpenAI, einmal Google) → Icon wird gesetzt,
      Bilddatei „Icon-Original (von KI erzeugt).png" liegt im Zielordner
- [ ] Ohne hinterlegten Schlüssel „Icon per KI erzeugen" klicken → klare
      Fehlermeldung statt Absturz

## Nächste Schritte (nach deiner Freigabe)

- Installer (Inno Setup) ist vorhanden und von Merten praktisch getestet
  (Installation, Virenscanner, Funktion, Deinstallation) sowie von ChatGPT
  code-geprüft
- KI-Icon-Erzeugung (OpenAI + Google, Credential Manager, einmaliger Hinweis)
  ist implementiert und wartet auf praktisches Testen durch Merten sowie die
  ChatGPT-Codeprüfung
- danach: README/Disclaimer als eigener Reiter im Programm (vorgemerkt in
  Notion, bewusst kurz vor Projektende geplant)
