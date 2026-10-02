; Inno Setup Skript fuer den Ordner-Icon-Ersteller.
;
; WICHTIG - bewusste Entscheidungen fuer dieses Skript (bitte beim Aendern
; im Hinterkopf behalten, damit sie nicht versehentlich rueckgaengig gemacht
; werden):
;
; 1. PrivilegesRequired=lowest: Das Programm selbst braucht nirgends
;    Administratorrechte (alles laeuft ueber HKEY_CURRENT_USER). Der
;    Installer soll das nicht widersprechen - er installiert deshalb
;    ausschliesslich fuer den aktuellen Benutzer und fragt NICHT nach einer
;    UAC-Rechteerhoehung. Bitte nicht auf "admin" oder "poweruser" aendern.
;    WICHTIG (ChatGPT-Pruefung des Installers, Commit 621f37d): bewusst KEIN
;    PrivilegesRequiredOverridesAllowed setzen. Mit "dialog" koennte der
;    Benutzer trotz lowest in den administrativen Modus wechseln bzw. den
;    Schalter /ALLUSERS verwenden - dann wuerden {group}/{autodesktop} auf
;    die gemeinsamen Bereiche aller Benutzer umschalten, waehrend das Skript
;    weiterhin mit {localappdata} und HKCU arbeitet. Ausserdem behandelt Inno
;    Setup eine administrative und eine nicht-administrative Installation
;    trotz gleicher AppId nicht als dieselbe Installation - es koennte ein
;    zweiter Eintrag in "Apps & Features" entstehen.
;
; 2. Installationsort {autopf} (Programme-Ordner) wuerde Admin-Rechte
;    voraussetzen. Stattdessen wird nach {localappdata}\Programs installiert -
;    das ist der uebliche Ort fuer Ein-Benutzer-Installationen ohne
;    Admin-Rechte (z.B. auch von VS Code so verwendet).
;
; 3. AppId ist eine feste GUID (siehe unten). Sie darf sich zwischen
;    Versionen NICHT aendern - sonst legt eine neue Installation einen
;    zweiten Eintrag in "Apps & Features" an, statt die alte Version sauber
;    zu ersetzen.
;
; 4. Der Explorer-Rechtsklick-Menue-Eintrag (HKCU\Software\Classes\Directory\
;    shell\OrdnerIconErsteller) wird normalerweise vom Programm selbst
;    gesetzt, wenn der Benutzer im Programm unter "Einstellungen" auf "Ins
;    Rechtsklick-Menue integrieren" klickt. Seit Merten-Feedback ("Installer-
;    Haekchen zusaetzlich zum Programm-Schalter") gibt es ZUSAETZLICH die
;    Installer-Aufgabe "contextmenu" (siehe [Tasks]/[Registry] unten): ist sie
;    beim Installieren angehakt, traegt der Installer denselben Eintrag direkt
;    ein - mit exakt denselben Registry-Werten, die auch ContextMenuService.
;    Register() im Programm setzen wuerde, damit IsRegistered() danach den
;    Eintrag korrekt erkennt und die Checkbox in "Einstellungen" beim naechsten
;    Programmstart automatisch als aktiviert anzeigt. Ist die Aufgabe NICHT
;    angehakt, fasst der Installer diesen Registry-Zweig beim Installieren gar
;    nicht an (die separate, davon unabhaengige "dontcreatekey"-Zeile unten
;    dient ausschliesslich der Deinstallations-Aufraeumung). Beim
;    DEINSTALLIEREN wird der Eintrag in jedem Fall vollstaendig entfernt
;    (Flags: uninsdeletekey), egal ob er beim Installieren per Haekchen oder
;    spaeter im Programm selbst gesetzt wurde - damit kein toter Menue-Eintrag
;    auf eine geloeschte EXE zeigt.
;
; 5. Die eingebundene EXE wird ausschliesslich mit "dotnet publish -r win-x64"
;    erzeugt (siehe PublishDir unten) - sie ist eine reine x64-Anwendung.
;    ArchitecturesAllowed schliesst deshalb aus, dass der Installer ueberhaupt
;    auf einem Windows startet, auf dem eine x64-EXE anschliessend nicht
;    laufen wuerde (z.B. reines 32-Bit-Windows).

#define MyAppName "Ordner-Icon-Ersteller"
#define MyAppVersion "2.7.4"
#define MyAppPublisher "Merten"
#define MyAppExeName "OrdnerIconErsteller.exe"

; Quelle: der komplette Publish-Ordner (normales self-contained Publish, KEIN
; Single-File). Erzeugt wird er von build-release.bat im Repository-Hauptordner.
#define PublishDir "..\src\OrdnerIconErsteller\publish"

[Setup]
AppId={{4EE874AA-4BEA-4012-AA6B-42E11B16D943}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppCopyright=Copyright (C) {#MyAppPublisher}
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
DisableDirPage=no
DisableWelcomePage=no
PrivilegesRequired=lowest
; PrivilegesRequiredOverridesAllowed bleibt absichtlich leer/nicht gesetzt
; (siehe Hinweis 1 oben) - kein Admin-Modus, kein /ALLUSERS moeglich.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2/ultra64
SolidCompression=yes
OutputDir=.\output
OutputBaseFilename=OrdnerIconErsteller-Setup-{#MyAppVersion}
WizardStyle=modern
; Kein eigenes Icon vorhanden (siehe csproj-Kommentar zum MVP) - Windows-
; Standardicon wird fuer Installer und Programm verwendet, bis eines
; ergaenzt wird.
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

[Tasks]
Name: "desktopicon"; Description: "Zusaetzlich ein Symbol auf dem Desktop anlegen"; GroupDescription: "Zusaetzliche Symbole:"; Flags: unchecked
; Standardmaessig NICHT angehakt (Flags: unchecked) - der Benutzer entscheidet
; bewusst, ob der Explorer-Eintrag gesetzt wird. Kann jederzeit auch spaeter
; im Programm selbst unter "Einstellungen" ein-/ausgeschaltet werden, siehe
; Hinweis 4 oben.
Name: "contextmenu"; Description: "Ins Explorer-Rechtsklick-Menue integrieren (kann spaeter im Programm unter ""Einstellungen"" jederzeit ein-/ausgeschaltet werden)"; GroupDescription: "Zusaetzliche Optionen:"; Flags: unchecked

[Files]
; Der gesamte Publish-Ordner (EXE, .NET-Laufzeitdateien, legal\*, LIESMICH.md,
; rechtliche PDFs). Debug-Symbole werden nicht mitinstalliert.
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Pruefbericht v2.7.3 (Gesamtdurchlauf), kleinerer Punkt 5: ohne diesen Schritt blieben
; die im Windows-Anmeldeinformations-Manager gespeicherten API-Schluessel (OpenAI/
; Google) sowie der Zustimmungs-Registrywert nach einer Deinstallation zurueck. Laeuft
; VOR dem Entfernen der Dateien ([UninstallRun] wird vor [Files] abgearbeitet), ohne
; sichtbares Fenster (runhidden) und ohne cmd.exe/powershell.exe - siehe Program.cs,
; RunUninstallCleanup(). "waituntilterminated" stellt sicher, dass die Aufraeumung
; abgeschlossen ist, bevor die EXE gleich danach geloescht wird.
; Pruefbericht v2.7.4 (ChatGPT-Gegenpruefung zu Commit 7a37a28), weiterer Punkt 1:
; "skipifdoesntexist" ergaenzt - ohne dieses Flag wuerde Inno Setup eine Warnung
; anzeigen, falls die EXE zum Zeitpunkt der Deinstallation bereits fehlt (z.B. durch
; einen Virenscanner in Quarantaene verschoben oder manuell geloescht). Die
; eigentliche Deinstallation (Entfernen der uebrigen Dateien) darf dadurch nicht
; verhindert werden.
Filename: "{app}\{#MyAppExeName}"; Parameters: "--uninstall-cleanup"; Flags: runhidden waituntilterminated skipifdoesntexist; RunOnceId: "OrdnerIconErstellerUninstallCleanup"

[Registry]
; Siehe Hinweis 4 oben: soll nur beim Deinstallieren wirksam sein (Loeschung),
; nicht beim Installieren. Ohne ValueType gilt zwar ValueType: none, aber Inno
; Setup legt den Schluessel dabei trotzdem beim Installieren an (nur ein Wert
; wird nicht gesetzt) - das widersprach dem Kommentar hier (ChatGPT-Pruefung
; des Installers, Commit 621f37d). "dontcreatekey" verhindert das Anlegen
; beim Installieren; "uninsdeletekey" entfernt den kompletten, ausschliesslich
; programmeigenen Unterschluessel samt "command"-Eintrag beim Deinstallieren.
Root: HKCU; Subkey: "Software\Classes\Directory\shell\OrdnerIconErsteller"; Flags: uninsdeletekey dontcreatekey

; Nur aktiv, wenn beim Installieren die Aufgabe "contextmenu" angehakt wurde
; (siehe [Tasks] oben). Bildet dieselben Registry-Werte nach, die
; ContextMenuService.Register() im Programm selbst setzen wuerde - siehe
; Hinweis 4 oben. "{app}\{#MyAppExeName}" ist zur Installationszeit bereits
; der endgueltige, feste Pfad der EXE (kein Platzhalter mehr wie im Programm-
; Code, wo der Pfad erst zur Laufzeit ermittelt wird). Fuer IsRegistered() im
; Programm ist ausschliesslich der "command"-Wert entscheidend (siehe dortiger
; Vergleich) - der reine Anzeigetext unten ("Ordner-Icon aendern...", bewusst
; ohne Umlaut, da diese Datei komplett in ASCII gehalten ist wie der Rest des
; Skripts) hat darauf keinen Einfluss und weicht deshalb unschaedlich vom
; Programm-Text ("Ordner-Icon ändern...") nur in der Schreibweise ab.
Root: HKCU; Subkey: "Software\Classes\Directory\shell\OrdnerIconErsteller"; ValueType: string; ValueName: ""; ValueData: "Ordner-Icon aendern..."; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\OrdnerIconErsteller"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName}"; Tasks: contextmenu
Root: HKCU; Subkey: "Software\Classes\Directory\shell\OrdnerIconErsteller\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: contextmenu
