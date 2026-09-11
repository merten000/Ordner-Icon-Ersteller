# Ordner-Icon-Ersteller (Version 2.0.3 – komplette Neuentwicklung)

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
- **Kein Internetzugriff, kein Nachladen von Code zur Laufzeit.** Alles läuft
  lokal und offline.
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

**Noch nicht enthalten** (laut deiner Auswahl bewusst auf später verschoben):
KI-generierte Icons, automatischer Icon-Vorschlag, fertiger Installer/Setup.exe.

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
│       └── ContextMenuService.cs
└── tests/
    └── OrdnerIconErsteller.Tests/
        ├── OrdnerIconErsteller.Tests.csproj
        ├── IconWriterTests.cs
        └── FolderIconServiceTests.cs
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

## Automatisierte Tests ausführen

Vom Projekt-Hauptordner aus (nicht aus `src\OrdnerIconErsteller`):

```powershell
dotnet test
```

Das baut Haupt- und Testprojekt und führt die xUnit-Tests aus (13 Tests
insgesamt): der Sicherung/Wiederherstellung-Rundlauf mit exakter
Attribut-Wiederherstellung, die Fehlerpfadtests aus der 2. ChatGPT-Prüfung
(Entfernen ohne Statusdatei, fehlgeschlagener Dateitausch, fehlgeschlagene
Sicherung) sowie die neuen Regressionstests aus der 3. ChatGPT-Prüfung
(gesperrte Datei beim Entfernen, fehlende zweite Sicherung, beschädigte
Statusdatei in drei Varianten, fehlgeschlagene Bilderzeugung nach
erfolgreicher Sicherung, zwei parallele Instanzen auf demselben Ordner).

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

## Nächste Schritte (nach deiner Freigabe)

- ChatGPT-Prüfung dieser Version
- danach ggf. Installer (Inno Setup o.ä., ohne CMD-Tricks) für die
  Veröffentlichung
- danach optional: KI-Icon-Erzeugung, automatischer Vorschlag
