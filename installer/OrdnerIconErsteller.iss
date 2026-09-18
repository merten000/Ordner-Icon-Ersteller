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
;    shell\OrdnerIconErsteller) wird vom Programm selbst gesetzt, wenn der
;    Benutzer im Programm auf "Ins Rechtsklick-Menue integrieren" klickt -
;    NICHT automatisch bei der Installation. Der Installer fasst diesen
;    Registry-Zweig beim Installieren/Aktualisieren deshalb nicht an (siehe
;    "dontcreatekey" unten). Beim DEINSTALLIEREN wird er aber vollstaendig
;    entfernt (Flags: uninsdeletekey), damit kein toter Menue-Eintrag auf
;    eine geloeschte EXE zeigt.
;
; 5. Die eingebundene EXE wird ausschliesslich mit "dotnet publish -r win-x64"
;    erzeugt (siehe PublishDir unten) - sie ist eine reine x64-Anwendung.
;    ArchitecturesAllowed schliesst deshalb aus, dass der Installer ueberhaupt
;    auf einem Windows startet, auf dem eine x64-EXE anschliessend nicht
;    laufen wuerde (z.B. reines 32-Bit-Windows).

#define MyAppName "Ordner-Icon-Ersteller"
#define MyAppVersion "2.6.0"
#define MyAppPublisher "Merten"
#define MyAppExeName "OrdnerIconErsteller.exe"

; Quelle der fertig veroeffentlichten (self-contained, single-file) EXE.
; Muss vorher erzeugt werden mit (aus src\OrdnerIconErsteller):
;   dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\publish
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

[Files]
Source: "{#PublishDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
; Die im Programm unter "Rechtliches" angezeigten Dokumente (siehe
; LegalDocumentService.cs) werden zur Laufzeit ueber AppContext.BaseDirectory
; gesucht - deshalb muessen sie hier in denselben relativen Unterordner
; "legal" neben die EXE installiert werden wie im Entwicklungs-Build
; (csproj kopiert sie per CopyToOutputDirectory in denselben PublishDir).
Source: "{#PublishDir}\legal\*.md"; DestDir: "{app}\legal"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Registry]
; Siehe Hinweis 4 oben: soll nur beim Deinstallieren wirksam sein (Loeschung),
; nicht beim Installieren. Ohne ValueType gilt zwar ValueType: none, aber Inno
; Setup legt den Schluessel dabei trotzdem beim Installieren an (nur ein Wert
; wird nicht gesetzt) - das widersprach dem Kommentar hier (ChatGPT-Pruefung
; des Installers, Commit 621f37d). "dontcreatekey" verhindert das Anlegen
; beim Installieren; "uninsdeletekey" entfernt den kompletten, ausschliesslich
; programmeigenen Unterschluessel samt "command"-Eintrag beim Deinstallieren.
Root: HKCU; Subkey: "Software\Classes\Directory\shell\OrdnerIconErsteller"; Flags: uninsdeletekey dontcreatekey
