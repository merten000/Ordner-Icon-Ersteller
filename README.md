# Ordner-Icon-Ersteller (Version 2.7.4 – komplette Neuentwicklung)

> **Release-Hinweis:** Gebaut wird ausschließlich mit `build-release.bat`
> (Ordner-Publish + Inno-Installer, siehe Abschnitt "Release und Installer
> bauen" und `RELEASE.md`). Frühere Beschreibungen von `publish.bat`,
> Single-File-Publish und `IncludeNativeLibrariesForSelfExtract` weiter unten
> sind Versionsgeschichte und gelten nicht mehr.

## Oberfläche modernisiert (Layout, Abstände, Beschriftungen)

Nur das Layout des Hauptfensters wurde überarbeitet (`MainForm.cs`: Konstruktor und
`BuildLayout()`); Programmlogik, Ereignisse und Dienste sind unverändert.

- Kopfzeile mit Titel und Kurzbeschreibung; „Einstellungen“ und „Rechtliches“ rechts
  (weiterhin aufklappende Menüs direkt unter dem Knopf).
- Klare Abschnitte: „Ordner und Bild“, „Icon setzen / entfernen“, „Icon per KI erzeugen
  (optional)“ mit Anbieter, API-Schlüssel und Beschreibung, sowie „Protokoll“.
- Einheitliche Knopfhöhe und -abstände, die Hauptaktionen („Icon setzen“, „Icon per KI
  erzeugen“) sind hervorgehoben, leere Felder zeigen einen Hinweistext.
- Segoe UI und DPI-Skalierung (`AutoScaleMode.Dpi`), größere Standardgröße des Fensters;
  in kleinen Fenstern erscheint eine Scrollleiste, damit nichts abgeschnitten wird.
- Helle Karten-Optik nach der Design-Referenz: weiße Karten mit weicher Kante und
  Symbol-Kreis („Ziel-Ordner“, „Bild für das Icon“, „Icon per KI erzeugen“,
  „Ausgabeprotokoll“), blaue Hauptknöpfe, zurückhaltende Nebenknöpfe mit kleinen Symbolen
  (Windows-Symbolschrift, ohne Bilddateien), abgerundete Eingabefelder und ein Statuspunkt.
  Die Bausteine stehen in `ModernUi.cs`; Steuerelemente, Ereignisse und Logik sind unverändert.
- Versionsnummer, Installer und Build-Weg sind unverändert.

## ChatGPT-Gegenprüfung v2.7.3 (Gesamtdurchlauf): zwei Freigabeblocker + zwei weitere Punkte behoben (v2.7.3 → v2.7.4)

ChatGPT hat Commit `fa2cc8a` (v2.7.3) im ersten kompletten Gesamtdurchlauf
geprüft und dabei 3 Freigabeblocker sowie 2 weitere Punkte gefunden. Alle
5 Punkte wurden adressiert; die anschließende Gegenprüfung von Commit
`7a37a28` bestätigte 3 davon als vollständig behoben und fand bei einem
vierten Punkt (Sicherungslogik) noch eine Lücke sowie eine vergessene
Versionsnummer:

1. **Native Laufzeit-DLLs fehlten im Installer.** Ein self-contained
   Single-File-Publish bündelte ohne `IncludeNativeLibrariesForSelfExtract`
   nur verwaltete DLLs - native Bibliotheken (u.a. `vcruntime140_cor3.dll`,
   `wpfgfx_cor3.dll`) blieben separat liegen und wurden vom Installer nicht
   mitinstalliert. Jetzt in `OrdnerIconErsteller.csproj` gesetzt.
2. **Sicherungsdatei-Kollision beim ersten „Icon setzen" für einen Ordner.**
   Fehlte der Marker, aber eine Sicherungsdatei mit reserviertem Namen
   existierte bereits, wurde sie bisher überschrieben statt geschützt zu
   werden. Zunächst per Vorprüfung in `SetFolderIconLocked` behoben; die
   Gegenprüfung zu `7a37a28` fand darin noch ein sehr kurzes Zeitfenster
   (TOCTOU) zwischen Prüfung und tatsächlicher Sicherung - `BackupIfExists`
   verschiebt die Sicherung jetzt ausschließlich mit `overwrite:false`
   (wirft statt zu überschreiben) und der Fehlerpfad löscht nur noch
   Sicherungen, die dieser Aufruf selbst nachweislich angelegt hat
   (`FolderIconService.cs`).
3. **Mehrere Log-Meldungen zeigten weiterhin volle Pfade oder
   ungefilterte Ausnahmetexte** (Startordner, manuelles Setzen/Entfernen,
   Ordnerscan- und KI-Erzeugungs-Ausnahme in `MainForm.cs`, Google-Textantwort
   in `AiIconService.cs`) - entgegen der Datenschutzerklärung. Bereinigt.
4. **Ein Credential-Manager-Fehler beim Start konnte das Hauptfenster
   abstürzen lassen.** `RefreshAiKeyField()` nutzt jetzt die nichtwerfenden
   `TryHasOpenAiKey`/`TryHasGoogleKey` aus `ApiKeyStore.cs` statt der
   werfenden `KeyExists`-Methode.
5. **Deinstallation ließ API-Schlüssel und Zustimmung zurück.** Ein neuer
   `--uninstall-cleanup`-Aufruf (`Program.cs`, ohne sichtbares Fenster) löscht
   jetzt vor dem Entfernen der Dateien die Credential-Manager-Einträge sowie
   den Zustimmungs-Registrywert (`installer/OrdnerIconErsteller.iss`,
   `[UninstallRun]`); ergänzt um `skipifdoesntexist`, damit eine fehlende
   EXE (z.B. durch Virenscanner-Quarantäne) die Deinstallation nicht behindert.
6. **`publish.bat` übernahm die neuen `release-extras` nicht** und lief bei
   einem Fehler scheinbar erfolgreich weiter. Kopiert die Dateien jetzt nach
   dem Publish automatisch mit und beendet sich bei einem Fehler mit
   Fehlercode.
7. **Versionsnummer war in `csproj`/Installer/README nicht mit angehoben
   worden**, obwohl der Commit bereits als 2.7.4 geführt wurde - jetzt an
   allen Stellen synchron auf 2.7.4 gesetzt.

## ChatGPT-Gegenprüfung v2.7.2: letzter Freigabeblocker + zwei kleinere Punkte behoben (v2.7.2 → v2.7.3)

ChatGPT hat Commit `73cbeb0` (v2.7.2) gegengeprüft: vier der fünf
vorherigen Freigabeblocker waren vollständig behoben, ein Blocker blieb
teilweise bestehen, dazu zwei kleinere Verbesserungen:

1. **Zwei weitere Log-Meldungen zeigten noch vollständige Pfade**, obwohl
   die Datenschutzerklärung das Gegenteil behauptet - übersehen worden
   waren die Meldung beim Ablehnen eines Referenzbildes
   ("Referenzbild ... auf Wunsch nicht mitgesendet") und die Meldung
   nach erfolgreicher KI-Icon-Erzeugung ("KI-Icon gesetzt für: ...").
   Beide zeigen jetzt nur noch den Status, ohne Datei-/Ordnerpfad
   (`MainForm.cs`).
2. **.NET-Runtime-Version war im Projekt nicht festgelegt** - ein
   späterer Build auf einem aktualisierten Rechner hätte eine andere
   Runtime-Patchversion bündeln können, während die fest eingecheckten
   Lizenzdateien weiterhin 8.0.31 beschreiben. Jetzt über
   `RuntimeFrameworkVersion` fest auf 8.0.31 gepinnt
   (`OrdnerIconErsteller.csproj`) - bei einem bewussten
   Runtime-Sicherheitsupdate müssen diese Zeile und die Dateien in
   `legal/dotnet-runtime/` zusammen aktualisiert werden.
3. **README verwies auf einen nicht mitgelieferten Prüfbericht** - der
   Verweis auf die nur lokal bei Merten liegende Datei wurde durch eine
   direkte Zusammenfassung ersetzt (dieser Abschnitt hier).

## ChatGPT-Prüfbericht v2.7.1 behoben: fünf Freigabeblocker + zwei weitere Punkte (v2.7.1 → v2.7.2)

ChatGPT hat Commit `6609cf1` (v2.7.1) geprüft und fünf Freigabeblocker
sowie zwei weitere Fehler/Verbesserungen gefunden (der volle Prüfbericht
liegt nur lokal bei Merten, nicht im Repository - deshalb hier bewusst
keine Datei referenziert, sondern die Punkte direkt zusammengefasst).
Alle fünf Blocker sowie Punkt 6 und 7 wurden behoben:

1. **.NET-Lizenz/Drittanbieterhinweise fehlten im Installer.** Als
   selbstenthaltene .NET-8-Anwendung bündelt das Programm Teile der
   .NET-Runtime mit aus - nach dem offiziellen .NET Asset Licensing
   Model müssen deren `LICENSE.TXT` und `THIRD-PARTY-NOTICES.TXT`
   (Runtime-Version `Microsoft.NETCore.App.Runtime.win-x64 8.0.31`,
   Originaldateien unverändert übernommen) mitgeliefert werden. Liegen
   jetzt in `src/OrdnerIconErsteller/legal/dotnet-runtime/`, werden vom
   Installer mit ausgeliefert und sind im Programm unter "Rechtliches"
   als eigene Einträge ("Drittanbieterhinweise (.NET)", ".NET-Lizenz")
   einsehbar (`LegalDocumentService.cs`, neue `Document`-Werte
   `DotNetLizenz`/`Drittanbieterhinweise`).
2. **Datenschutzerklärung behauptete fälschlich, API-Schlüssel würden
   nie an Dritte übertragen** - korrigiert: der Schlüssel wird zur
   Authentifizierung an den gewählten Anbieter (OpenAI/Google) gesendet,
   niemals an den Autor (`legal/DATENSCHUTZ.md`).
3. **Lokales Log zeigte den vollständigen Prompt und Zielordnerpfad**,
   obwohl die Datenschutzerklärung nur eine kurze Statuszeile erwähnt -
   das Log zeigt jetzt nur noch Anbieter und Status
   (`MainForm.cs`, z.B. "KI-Icon wird erzeugt (Anbieter: Google)...").
4. **Pauschales Dekompilierungsverbot** in `LIZENZ.md` berücksichtigte
   die zwingende gesetzliche Ausnahme nach § 69e UrhG nicht - jetzt mit
   "soweit gesetzlich zulässig" versehen.
5. **Zu weit gefasster Haftungsausschluss** in `HAFTUNGSAUSSCHLUSS.md` -
   zwingende gesetzliche Haftung (Vorsatz, grobe Fahrlässigkeit, Leben/
   Körper/Gesundheit) jetzt ausdrücklich ausgenommen.
6. **Auslieferungstest war wirkungslos**: `ReadText_MitgelieferteDatei­
   Vorhanden_LiefertNichtLeerenText` sprang bei fehlender Datei per
   `return` einfach über den Test statt fehlzuschlagen - jetzt mit
   `Assert.True(File.Exists(...))` (`LegalDocumentServiceTests.cs`).
7. **FolderContentScanner sammelte bei der Referenzbild-Suche weiterhin
   alle Bildpfade des ganzen Ordners im Speicher**, bevor sie geprüft
   wurden - Kandidaten werden jetzt direkt während der Enumeration
   geladen/validiert; sobald ein gültiges Bild gefunden ist, werden
   keine weiteren mehr gesammelt (`FolderContentScanner.cs`).

Rechtliche Hinweise bleiben weiterhin Standardtexte ohne anwaltliche
Prüfung - bei echten rechtlichen Zweifeln (insbesondere vor einer
öffentlichen Veröffentlichung) sollte ein Fachanwalt für IT-Recht
hinzugezogen werden.

## Build-Fehler in den Tests behoben (v2.7.0 → v2.7.1)

Merten-Test: `dotnet build` (diesmal für die ganze Projektmappe inkl.
Tests, nicht nur `src\OrdnerIconErsteller`) schlug mit zwei CS0051-
Fehlern in `LegalDocumentServiceTests.cs` fehl: "Inkonsistenter
Zugriff: Parametertyp 'LegalDocumentService.Document' ist weniger
zugreifbar als Methode...".

Ursache: `LegalDocumentService.Document` ist absichtlich `internal`
(siehe `LegalDocumentService.cs`) und für das Testprojekt nur über
`InternalsVisibleTo` sichtbar. Zwei `[Theory]`-Testmethoden nahmen
diesen internen Enum-Typ aber direkt als Parameter entgegen - das ist
laut C#-Sprachregel ein Fehler (CS0051), weil die Testmethoden selbst
`public` sein müssen (das verlangt xUnit zur Testerkennung), eine
`public`-Methode aber grundsätzlich keinen weniger zugreifbaren
("internal") Typ in ihrer Signatur haben darf - unabhängig davon, ob
`InternalsVisibleTo` gesetzt ist. Dieser Fehler bestand vermutlich
schon länger, ist aber nur aufgefallen, weil vorher meist gezielt nur
`dotnet build` innerhalb von `src\OrdnerIconErsteller` lief (ohne das
Testprojekt).

Fix: die beiden betroffenen Testmethoden nehmen den Dokumentnamen jetzt
als reinen `string` entgegen (z.B. `"Impressum"`) und wandeln ihn erst
im Testkörper (nicht mehr Teil der öffentlichen Signatur) per
`Enum.Parse<LegalDocumentService.Document>(...)` um - dort ist die
Verwendung des internen Typs unproblematisch.

## Installer-Häkchen für die Rechtsklick-Menü-Integration (v2.6.0 → v2.7.0)

Merten hatte früher schon erwähnt, dass er zusätzlich zum Schalter im
Programm selbst ("Einstellungen") auch beim Installieren direkt ein
Häkchen dafür haben möchte. Umgesetzt als neue, standardmäßig NICHT
angehakte Installer-Aufgabe "Ins Explorer-Rechtsklick-Menü integrieren"
(`installer/OrdnerIconErsteller.iss`, `[Tasks]`).

Ist das Häkchen beim Installieren gesetzt, trägt der Installer denselben
Registry-Eintrag ein, den auch `ContextMenuService.Register()` im
Programm selbst setzen würde (gleicher "command"-Wert, der für die
Erkennung durch `IsRegistered()` entscheidend ist) - die Checkbox unter
"Einstellungen" zeigt den Eintrag danach beim ersten Programmstart
automatisch als bereits aktiviert an. Ist das Häkchen NICHT gesetzt,
fasst der Installer diesen Registry-Zweig beim Installieren gar nicht
an, genau wie bisher. Beim Deinstallieren wird der Eintrag weiterhin in
jedem Fall vollständig entfernt, egal ob er per Installer-Häkchen oder
später im Programm gesetzt wurde.

## "Einstellungen"/"Rechtliches" als aufklappendes Dropdown-Menü unter dem Knopf (v2.5.0 → v2.6.0)

Merten-Feedback nach dem v2.5.0-Test (die gemeinsame "Menü ▾"-ComboBox
war nicht gemeint): "Oben rechts steht das Rechtliche, links davon die
Einstellung. Man tippt auf den Button, und darunter klappt automatisch
der jeweilige Inhalt zum Anklicken auf - genau das normale
Windows-Standardverhalten eines Dropdown-Menüs unter einem Knopf."

Umgesetzt mit der dafür vorgesehenen WinForms-Technik: zwei eigene
Knöpfe ("Einstellungen" links, "Rechtliches" rechts, wie ursprünglich in
v2.4.x) mit je einem `ContextMenuStrip`, der per `Show(button, new
Point(0, button.Height))` direkt unterhalb des jeweiligen Knopfes
aufklappt - das ist die eingebaute Standardtechnik für genau dieses
Verhalten (kein selbstgebautes Panel, kein Popup-Fenster). Die beiden
Menüs werden nur EINMAL in `BuildLayout()` befüllt und danach für die
gesamte Laufzeit wiederverwendet: "Einstellungen" enthält den
Rechtsklick-Menü-Schalter mit Status (über `ToolStripControlHost`
eingebettet), "Rechtliches" die vier Dokumente als normale Menüpunkte.
Weil hier - anders als bei der vorherigen Fenster-Lösung (v2.4.0/
v2.4.1) - nichts pro Klick neu erzeugt oder disposed wird, kann das
"Cannot access a disposed object"-Problem von v2.4.1 hier gar nicht
erst auftreten.

## "Einstellungen"/"Rechtliches" als Dropdown-Liste statt zweier Knöpfe (v2.4.1 → v2.5.0)

Merten-Feedback (Foto vom laufenden v2.4.1-Test, mit geöffneter
Anbieter-Auswahlliste OpenAI/Google): "Das mit dem Rechtlichen und
Einstellungen soll genauso aufgelistet werden wie bei der API-Key-
Wechsel-Liste." Gemeint ist die ComboBox (DropDownList), mit der weiter
unten zwischen OpenAI und Google gewechselt wird.

Die beiden einzelnen Knöpfe in der oberen Leiste wurden deshalb durch
eine einzige ComboBox derselben Art ersetzt (`_cmbTopMenu`). Sie zeigt
als ersten, reinen Platzhaltereintrag "Menü ▾" und danach "Einstellungen"
sowie "Rechtliches". Nach jeder Auswahl öffnet sich sofort das jeweilige
Fenster (dieselbe `ShowSettingsWindow()`/`ShowRechtlichesWindow()`-Technik
wie zuvor) und die ComboBox springt automatisch zurück auf den
Platzhalter - so bleibt sie ein wiederholt benutzbares Menü statt einer
dauerhaften Auswahl wie bei der Anbieter-Liste.

## Absturz beim zweiten Öffnen von "Einstellungen"/"Rechtliches" behoben (v2.4.0 → v2.4.1)

Merten-Test von v2.4.0: die neuen Fenster erschienen zwar (Leiste oben
bündig, korrekt), aber beim (zweiten) Öffnen stürzte das Programm mit
"Unbehandelte Ausnahme... Cannot access a disposed object. Object name:
'System.Windows.Forms.Button'." ab (Fehlerdialog-Screenshot).

Ursache: `ShowSettingsWindow()`/`ShowRechtlichesWindow()` fügen die
dauerhaften Feld-Controls (`_chkContextMenu`, `_lblMenuStatus` bzw.
`_btnImpressum`/`_btnDatenschutz`/`_btnLizenz`/`_btnHaftungsausschluss`)
direkt in den `content`-Container des jeweiligen Dialog-Fensters ein. Beim
Schließen des Dialogs löst das umschließende `using`-Statement
`dlg.Dispose()` aus - das entsorgt rekursiv ALLE noch enthaltenen
Kind-Controls, also auch diese eigentlich wiederverwendbaren, dauerhaften
Buttons/Checkbox/Label. Beim zweiten Klick auf "Einstellungen" oder
"Rechtliches" versuchte das Programm dann, bereits entsorgte
(`disposed`) Controls erneut anzuzeigen - daher der Absturz.

Fix: in beiden Methoden werden die dauerhaften Feld-Controls jetzt in
einem `finally`-Block direkt nach `ShowDialog()` wieder aus dem
`content`-Container entfernt (`Controls.Remove(...)`), BEVOR der Dialog
am Ende der Methode disposed wird. `Controls.Remove()` löst nur die
Eltern-Kind-Beziehung, ohne das Control selbst zu entsorgen - beim
nächsten Öffnen sind die Controls dadurch wieder unversehrt verfügbar.

## Einstellungen/Rechtliches als eigene Fenster statt Klapp-Panel (v2.3.6 → v2.4.0)

Merten-Test von v2.3.6: Klick auf "Einstellungen"/"Rechtliches" zeigte
IMMER NOCH nichts an - identisch zum Verhalten in v2.3.3 bis v2.3.6, trotz
mehrerer grundverschiedener Lösungsansätze (TableLayoutPanel-Spalte,
schwebendes Overlay, eingebettetes FlowLayoutPanel, Dock=Right-Container).
Merten-Vorschlag: die Knöpfe in eine eigene Leiste oben einfügen, damit es
bündig bleibt.

Statt eines weiteren Versuchs mit einem ein-/ausklappbaren Panel (dessen
Sichtbarkeits-/Größenberechnung sich in dieser Entwicklungsumgebung
wiederholt nicht zuverlässig nachstellen ließ) wird jetzt dieselbe,
bereits nachweislich funktionierende Technik verwendet wie bei den vier
Rechtliches-Dokumenten: ein Klick öffnet ein eigenes kleines Fenster
(`Form.ShowDialog`, wie schon bei `LegalDocumentViewerForm`).

- **Obere Werkzeugleiste** (`MainForm.cs`, neuer `topBar`): Die zwei
  Knöpfe "Einstellungen" und "Rechtliches" sitzen jetzt in einer über die
  volle Fensterbreite angedockten Leiste (`Dock = DockStyle.Top`) oben,
  rechtsbündig ausgerichtet - mit demselben rechten Randabstand (12px)
  wie der übrige Inhalt darunter, damit es bündig wirkt.
- **`ShowSettingsWindow()`**: öffnet ein eigenes kleines Fenster mit dem
  Kontrollkästchen "Ins Rechtsklick-Menü integrieren" und dem Status-Text.
- **`ShowRechtlichesWindow()`**: öffnet ein eigenes kleines Fenster mit
  den vier bekannten Knöpfen (Impressum, Datenschutz, Lizenz,
  Haftungsausschluss), die wie gehabt die jeweiligen Dokumente anzeigen.

Das komplette bisherige "Band"-Konzept (Panel/FlowLayoutPanel-Container,
Visible-Umschaltung) wurde entfernt. Fensterbreite beim Start wieder auf
660 reduziert (die obere Leiste braucht keine zusätzliche Fensterbreite
mehr wie das seitliche Band). Version auf 2.4.0 angehoben (größerer
Konzeptwechsel, deshalb Minor- statt Patch-Version).

## Grundlegend robusteres Layout: Docking statt TableLayoutPanel-Spalten (v2.3.5 → v2.3.6)

Merten-Test von v2.3.5: Klick auf "Einstellungen"/"Rechtliches" zeigte
IMMER NOCH nichts an - identisch zum Verhalten in v2.3.3/v2.3.4. Zusätzlich
neu aufgefallen: Der API-Schlüssel-Bereich war zu eng geworden, der
"Löschen"-Knopf praktisch unsichtbar/abgeschnitten.

Ursache für beides: Die gemeinsame `TableLayoutPanel`-Konstruktion mit
einer `SizeType.AutoSize`-Spalte für das rechte Band UND einer festen
`SizeType.Absolute`-Breite für den linken Bereich (aus v2.3.3) hat sich
als zu fragil erwiesen - zum einen berechnet eine `AutoSize`-Spalte ihre
Breite beim späteren Auf-/Zuklappen von Kindelementen (Visible-Wechsel)
nicht zuverlässig neu (das erklärt, warum trotz mehrerer Korrekturversuche
beim Klick nichts erschien), zum anderen war die feste Breite (480px) für
den linken Bereich zu knapp bemessen.

Das Layout wurde grundlegend umgebaut: Statt einer gemeinsamen
`TableLayoutPanel` mit mehreren Spalten gibt es jetzt zwei unabhängig
angedockte Bereiche direkt im Fenster:

- `rightBandContainer`: rechts angedockt (`Dock = DockStyle.Right`), mit
  FESTER Breite (300px) - dadurch muss beim Auf-/Zuklappen der Felder
  keine Spaltenbreite mehr neu berechnet werden, nur der Inhalt innerhalb
  der bereits vorhandenen, unveränderlichen Breite. Enthält weiterhin die
  zwei Umschalt-Knöpfe sowie die beiden auf-/zuklappbaren Felder
  (unverändert aus v2.3.5: normale `FlowLayoutPanel`, `Visible`-Umschaltung).
- `leftContent`: füllt den gesamten Rest (`Dock = DockStyle.Fill`) - exakt
  die Struktur (einspaltige `TableLayoutPanel`, `Percent(100)`-Breite),
  die vor der Einführung des rechten Bands schon zuverlässig funktioniert
  hat. Der API-Schlüssel-Bereich bekommt dadurch wieder genug Platz.

Bei Docking bestimmt die Hinzufüge-Reihenfolge die Aufteilung: `leftContent`
(Dock=Fill) wird ZUERST hinzugefügt, danach `rightBandContainer`
(Dock=Right), damit dieser seinen festen Platz vom Fill-Bereich
"abzwackt". Fensterbreite beim Start auf 900 angehoben (600 für den
linken Bereich + 300 für das Band), Version auf 2.3.6.

## Fix: Overlay-Panels durch bewährte eingebettete Panels ersetzt (v2.3.4 → v2.3.5)

Merten-Test von v2.3.4: Auch nach dem `Panel` → `FlowLayoutPanel`-Fix blieb
beim Klick auf "Einstellungen"/"Rechtliches" weiterhin nichts sichtbar.
Das schwebende Overlay-Konzept (Panels frei im Fenster mit über
`PointToScreen`/`PointToClient` berechneter Position, `BringToFront()`)
ließ sich in dieser Entwicklungsumgebung nicht gegenprüfen und war damit
zu unsicher - vermutlich griff dabei ein weiterer WinForms-Sonderfall
(Layout-Berechnung eines nie sichtbar gewesenen, freistehenden Steuerelements).

`_settingsPanel`/`_legalPanel` sitzen jetzt wieder ganz regulär IM
selben `FlowLayoutPanel` wie die zwei Umschalt-Knöpfe (statt frei im
Fenster) - Sichtbarkeit einfach über `Visible = true/false` auf einem
normal eingebetteten `FlowLayoutPanel`, eine bewährte, zuverlässig
funktionierende Technik. `ToggleOverlayPanel()` wurde entfernt.

Einziger Unterschied zum ursprünglich gewünschten "Layer": Der
Log-Bereich kann sich beim Aufklappen minimal nach unten verschieben,
statt dass sich das Panel darüber legt - laut Merten-Feedback ("kann
ruhig...") unkritisch. Die bündige Positionierung oben rechts (v2.3.3)
bleibt unverändert. Version auf 2.3.5 angehoben.

## Fix: Panels blieben unsichtbar (v2.3.3 → v2.3.4)

Merten-Test von v2.3.3: Klick auf "Einstellungen"/"Rechtliches" zeigte gar
nichts an. Ursache: `_settingsPanel`/`_legalPanel` waren als einfaches
`System.Windows.Forms.Panel` angelegt - ein einfaches `Panel` berechnet
seine Größe bei `AutoSize=true` NICHT automatisch anhand seines Inhalts
(offiziell dokumentierte Einschränkung dieser Klasse), wodurch die Panels
praktisch in ihrer Standardgröße/-position blieben statt sich um ihren
tatsächlichen Inhalt zu legen.

Beide Felder sind jetzt selbst `FlowLayoutPanel` (wie an anderen Stellen
im Programm bereits verwendet) statt eines einfachen `Panel` mit
zusätzlichem `FlowLayoutPanel`-Wrapper darin - `FlowLayoutPanel`
unterstützt echtes `AutoSize` zuverlässig. Die bisherige Logik (bündige
Positionierung neben der linken Bedienung, Aufklappen als Layer über der
Oberfläche via `ToggleOverlayPanel`) bleibt unverändert, nur die
Klassenwahl der beiden Felder wurde korrigiert. Version auf 2.3.4
angehoben.

## Band bündig ausgerichtet, Panels als Layer statt Platzhalter (v2.3.2 → v2.3.3)

Zwei weitere Rückmeldungen nach dem Test von v2.3.2 umgesetzt:

1. **Band bündig statt am Fensterrand** (`MainForm.cs`): Bei einem breiter
   gezogenen Fenster klaffte zwischen der linken Bedienung und den beiden
   Knöpfen "Einstellungen"/"Rechtliches" eine große Lücke, weil die linke
   Spalte den gesamten verfügbaren Platz beanspruchte (`Percent(100)`) und
   die rechte Spalte dadurch an den Fensterrand gedrückt wurde. Das
   Hauptlayout hat jetzt drei Spalten: Spalte 0 (linke Bedienung) hat eine
   feste Breite, Spalte 1 (die zwei Knöpfe) sitzt direkt daneben, und eine
   neue, inhaltsleere Füllspalte 2 nimmt bei einem breiteren Fenster den
   übrigen Platz auf. Dadurch bleiben Bedienung und Knöpfe bündig
   zusammen, unabhängig von der Fenstergröße. Fensterbreite beim Start von
   640 auf 760 angehoben, damit dafür genug Platz vorhanden ist.
2. **Aufgeklappte Panels liegen jetzt als Layer über der Oberfläche**
   (`MainForm.cs`): Die beiden Panels (Einstellungen, Rechtliches) waren
   bisher Teil des Tabellenlayouts und haben beim Aufklappen andere
   Bereiche nach unten weggeschoben. Sie hängen jetzt direkt im Fenster
   (nicht mehr im TableLayoutPanel) mit einer beim Klick berechneten
   Position direkt unter dem jeweiligen Knopf (neue Methode
   `ToggleOverlayPanel`) und werden per `BringToFront()` über die übrige
   Oberfläche gelegt, statt Platz im Layout zu beanspruchen.

Version auf 2.3.3 angehoben.

## Rechtes Band jetzt einklappbar (v2.3.1 → v2.3.2)

Nach dem ersten Test des Bands ("Rechtliches" oben rechts, Checkbox fürs
Rechtsklick-Menü) eine weitere Rückmeldung umgesetzt: Beide Bereiche waren
dauerhaft sichtbar, sollen aber erst nach Klick erscheinen.

Das rechte Band besteht jetzt aus zwei Umschalt-Knöpfen nebeneinander -
"Einstellungen" (links) und "Rechtliches" (rechts) - und darunter zwei
Panels, die standardmäßig eingeklappt (unsichtbar) sind:

- Klick auf "Einstellungen" klappt das Panel mit dem Kontrollkästchen
  "Ins Rechtsklick-Menü integrieren" auf/zu (Inhalt unverändert aus
  v2.3.1 - siehe unten - nur jetzt hinter diesem Knopf versteckt statt
  dauerhaft sichtbar).
- Klick auf "Rechtliches" klappt das Panel mit den vier Knöpfen
  (Impressum, Datenschutz, Lizenz, Haftungsausschluss) auf/zu.

Beide Panels lassen sich unabhängig voneinander auf-/zuklappen. Version
auf 2.3.2 angehoben.

## Layout & Rechtsklick-Schalter überarbeitet (v2.3.0 → v2.3.1)

Nach dem ersten praktischen Test des Rechtliches-Bereichs zwei
Rückmeldungen umgesetzt:

1. **Rechtliches-Band oben rechts** (`MainForm.cs`): Die vier Knöpfe
   (Impressum, Datenschutz, Lizenz, Haftungsausschluss) saßen bisher als
   breite Knopfreihe zwischen den übrigen Bereichen. Das Hauptlayout ist
   jetzt zweispaltig: Die linke Spalte enthält weiterhin die komplette
   bisherige Bedienung, in der rechten, schmalen Spalte sitzen die vier
   Knöpfe UNTEREINANDER als eigenes "Band" in der oberen rechten Ecke des
   Fensters. Das Log bleibt unten über die volle Fensterbreite.
2. **Rechtsklick-Menü als Kontrollkästchen statt zwei Knöpfen**
   (`MainForm.cs`): "Ins Rechtsklick-Menü integrieren" und "Aus
   Rechtsklick-Menü entfernen" sind zu einem einzigen Kontrollkästchen
   zusammengefasst, das direkt den gewünschten Zustand einstellt (an =
   eingetragen, aus = entfernt). Der Haken wird nach jedem Register()/
   Unregister()-Aufruf sowie beim Programmstart immer an den tatsächlichen
   Registry-Zustand angeglichen (`RefreshMenuStatus`), damit er nie einen
   Zustand zeigt, der wegen eines Fehlers gar nicht eingetreten ist; ein
   Sperr-Flag verhindert, dass dieses programmatische Setzen selbst
   nochmal Register()/Unregister() auslöst.

Außerdem die Platzhalter im Impressum durch die tatsächlichen Angaben
ersetzt (Anschrift, Kontakt-E-Mail). Version auf 2.3.1 angehoben.

Noch offen: eine Installer-Checkbox, mit der die Rechtsklick-Integration
bereits bei der Installation aktiviert werden kann (bisher weiterhin
ausschließlich über das Kontrollkästchen im Programm selbst einstellbar,
wie es aus Sicherheitsgründen bereits vorher so war) - das folgt bei
Bedarf als nächster, separater Schritt.

## Rechtliches-Bereich ergänzt (v2.2.2 → v2.3.0)

Neuer Bereich "Rechtliches" im Hauptfenster mit vier Schaltflächen (Impressum,
Datenschutz, Lizenz, Haftungsausschluss), die die im Unterordner `legal/`
mitgelieferten Dokumente direkt im Programm anzeigen (`LegalDocumentService.cs`,
`LegalDocumentViewerForm.cs`) - bewusst keine zweite, unabhängig gepflegte
Textkopie im Code: Programm und mitgelieferte Dateien können dadurch nicht
auseinanderlaufen. Fehlt eine Datei (z. B. bei unvollständiger Installation),
erscheint eine verständliche Meldung statt eines Absturzes.

Das Impressum enthält nach Rücksprache das vollständige Set (Name,
ladungsfähige Anschrift, Kontakt-E-Mail, keine Telefonnummer) gemäß § 5 DDG,
da das Programm öffentlich angeboten werden soll - Anschrift und E-Mail sind
aktuell als Platzhalter markiert und werden ergänzt, sobald die konkreten
Angaben vorliegen. Datenschutz und Haftungsausschluss beschreiben konkret die
einzige Datenübertragung des Programms (die optionale KI-Icon-Funktion).

Die Dateien werden per `csproj` (`CopyToOutputDirectory`) neben die EXE
kopiert und vom Installer in denselben relativen Unterordner `legal`
installiert, damit `AppContext.BaseDirectory` sie in Entwicklungs-Build und
installierter Version gleichermaßen zuverlässig findet. Version auf 2.3.0
angehoben. Tests ergänzt (`LegalDocumentServiceTests`: Pfadaufbau, Einlesen
vorhandener Dateien, verständliche Fehlermeldung bei fehlender Datei).

Noch offen aus der ursprünglichen Planung: die "Einstellungen"-Ansicht mit
dem verschobenen Rechtsklick-Menü-Schalter (an/ausschaltbar) plus
Installer-Checkbox für die Rechtsklick-Integration - das folgt als
nächster, separater Schritt.

## 7. ChatGPT-Gegenprüfung behoben (v2.2.1 → v2.2.2)

Die 7. ChatGPT-Gegenprüfung (Commit 1a04de8, v2.2.1) fand 2 Freigabeblocker
und 2 weitere Restpunkte:

1. **Reentrancy-Race beim Hintergrund-Scan** (`MainForm.cs`): Seit der 6.
   Gegenprüfung läuft `FolderContentScanner.Scan` über `Task.Run`, die
   Bedienelemente wurden aber erst NACH diesem Scan, dem Referenzbild-Laden,
   der Zustimmung und der Namensberechnung gesperrt. Ein zweiter Klick auf
   "Icon per KI erzeugen" während eines langsamen Scans konnte dadurch einen
   parallelen zweiten Ablauf starten, der sich mit dem ersten gegenseitig über
   `_aiOperationInProgress`/die Bedienelement-Sperre entsperrt hätte.
   `GenerateAiIconAsync` sperrt jetzt ganz am Anfang - noch vor dem Scan -
   und legt Scan, Zustimmung, Netzwerkanfrage und Dateitransaktion zusammen in
   ein einziges `try/finally`; ein früher Ausstieg, falls bereits eine
   Operation läuft, dient als zusätzliches Sicherheitsnetz.
2. **Referenzbildprüfung akzeptierte abgeschnittene Bilder (TOCTOU)**
   (`AiIconService.cs`): `LoadReferenceImage` las Größe und Inhalt bisher über
   zwei getrennte Zugriffe (`FileInfo.Length`, dann `File.ReadAllBytes`) - dazwischen
   konnte die Datei ausgetauscht/vergrößert werden, und `Image.FromStream(...,
   validateImageData:true)` allein erkannte eine um 1.200 Byte gekürzte, sonst
   gültige PNG nicht (GDI+ dekodiert dabei teils nur Header/Metadaten). Jetzt
   wird die Datei über ein einziges `FileStream`-Handle gelesen (Länge und
   Inhalt vom selben Handle, zusätzlich hart auf die Obergrenze begrenzt) und
   danach - genau wie bei von der KI gelieferten Bildern
   (`DecodeAndReencodeAsPng`) - vollständig dekodiert und als PNG neu
   encodiert; nur diese neu encodierten Bytes werden weitergegeben.
3. **Prompt behauptete Bildübertragung auch bei ungültigem/keinem Bild**
   (`FolderContentScanner.cs`, `MainForm.cs`): Der Scanner wählte bisher allein
   anhand von Endung/Größe den ersten Bildkandidaten und baute sofort den
   Referenz-Satz in den Prompt ein; scheiterte das Laden später, blieb der
   Satz trotzdem stehen, und ein gültiges zweites Bild wurde nie gesucht.
   `Scan` sammelt jetzt alle Bildkandidaten und lädt/validiert sie der Reihe
   nach vollständig, bis das erste gültige gefunden ist (ungültige werden
   übersprungen); der Referenz-Satz wird dem Prompt erst nachträglich
   angehängt (`WithReferenceImageMention`), nachdem feststeht, dass das Bild
   tatsächlich verwendet wird.
4. **Keine Bestätigung des konkreten Bildnamens** (`AiConsentService.cs`,
   `MainForm.cs`): Die allgemeine, versionierte Zustimmung deckte nur pauschal
   ab, dass irgendein Bild übertragen werden könnte, ohne den Dateinamen zu
   nennen oder eine Ablehnung im Einzelfall zu erlauben. Eine neue, nicht
   dauerhaft gespeicherte Abfrage (`AiConsentService.ConfirmReferenceImage`)
   zeigt jetzt vor jeder Erzeugung mit gefundenem Referenzbild dessen
   konkreten Pfad an und lässt die Übertragung einzeln bestätigen oder
   ablehnen.

Tests ergänzt: `AiIconServiceTests` (abgeschnittene echte PNG wird abgelehnt,
gültiges Bild liefert vollständig dekodierbares neu encodiertes PNG statt
Byte-Identität), `FolderContentScannerTests` (Referenzbild wird vollständig
geladen/validiert, aber nicht mehr automatisch im Prompt erwähnt; ein
beschädigter erster Kandidat wird übersprungen und ein gültiger zweiter
gefunden). Von Merten lokal gebaut und getestet. Die Reentrancy-Sperre selbst
ist - wie andere reine UI-Thread-Logik in `MainForm` - nicht durch einen
automatisierten Test abgedeckt, sondern per Code-Review verifiziert.

## 6. ChatGPT-Gegenprüfung behoben (v2.2.0 → v2.2.1)

Die 6. ChatGPT-Gegenprüfung (Commit fcf5a31, v2.2.0) fand 3 Freigabeblocker
und 2 weitere Fehler bei den beiden neuen "Icon ohne Text"/"Icon ohne
Überschreiben"-Funktionen:

1. **Zustimmungshinweis deckte neuen Umfang nicht ab** (`AiConsentService.cs`):
   Der Hinweis erwähnte nur den eingegebenen Text (Prompt). Seit v2.2.0 werden
   bei leerem Prompt zusätzlich Ordner-/Dateinamen und bei Google ein
   vorhandenes Bild aus dem Ordner übertragen, ohne dass der Hinweis das
   erwähnte. Eine bereits gespeicherte alte Zustimmung hätte das nie mehr
   angezeigt. Der Hinweistext wurde entsprechend ergänzt, und eine neue
   Versionsnummer (`CurrentConsentVersion`) sorgt dafür, dass eine früher unter
   der alten Fassung gespeicherte Zustimmung nicht mehr zählt - der
   aktualisierte Hinweis erscheint dadurch einmalig erneut.
2. **Dateiname wurde vor der ordnerweiten Sperre reserviert**
   (`FolderIconService.cs`, `MainForm.cs`): Zwei nahezu gleichzeitig gestartete
   Erzeugungen im selben Ordner konnten denselben, vorab per Zeitstempel
   berechneten "eindeutigen" Dateinamen wählen; die zweite, ebenfalls bereits
   bezahlte Erzeugung wurde beim Schreiben dann komplett verworfen
   (`InvalidOperationException`). `ReplaceGeneratedImageAndSetIcon` erhält jetzt
   nur noch einen WUNSCH-Dateinamen und entscheidet die tatsächliche
   Eindeutigkeit erst innerhalb derselben Sperre wie das Schreiben selbst -
   bei einer Kollision wird automatisch ein neuer Dateiname gewählt, statt das
   Bild zu verlieren. Der tatsächlich verwendete Pfad wird zurückgegeben.
3. **Referenzbild wurde nicht als Bild validiert** (`AiIconService.cs`):
   `LoadReferenceImage` prüfte bisher weder Größe noch Bildgültigkeit selbst
   (nur der aufrufende `FolderContentScanner` tat das). Jetzt begrenzt die
   Methode die Dateigröße selbst (15 MB) und dekodiert die Datei tatsächlich
   als Bild, bevor sie an Google geschickt wird; der MIME-Typ kommt danach vom
   erkannten Bildformat statt blind von der Dateiendung.
4. **OpenAI-Prompt behauptete fälschlich eine Bildübertragung**
   (`FolderContentScanner.cs`, `MainForm.cs`): Der Hinweis auf ein
   mitgeliefertes Referenzbild stand bisher unabhängig vom gewählten Anbieter
   im Prompt-Text, obwohl OpenAI kein Bild entgegennimmt und es vollständig
   verwirft. `FolderContentScanner.Scan` bekommt jetzt `allowReferenceImage`
   vom Aufrufer übergeben (nur `true` bei Google) und erwähnt/liefert ein
   Referenzbild nur noch dann.
5. **Ordner-Scan lief auf dem UI-Thread und sammelte trotz 25er-Grenze alle
   Einträge** (`FolderContentScanner.cs`, `MainForm.cs`): `FolderContentScanner
   .Scan` läuft jetzt in `GenerateAiIconAsync` per `Task.Run` in einem
   Thread-Pool-Thread statt auf dem UI-Thread, und die interne Namensliste
   wird von vornherein bei 25 Einträgen gedeckelt, statt erst alle Einträge
   gesammelt zu haben und anschließend nur die ersten 25 zu verwenden.

Neue/angepasste Tests dafür in `AiIconServiceTests.cs` (4 neue Tests für
`LoadReferenceImage`), `FolderContentScannerTests.cs` (2 neue Tests für
`allowReferenceImage` und die 25er-Grenze) und `FolderIconServiceTests.cs`
(bestehender Test für Punkt 2 umgeschrieben, prüft jetzt das Ausweichen auf
einen neuen Dateinamen statt der alten Exception). Noch nicht von Merten
gebaut/getestet - steht noch aus, bevor diese Runde erneut an ChatGPT geht.

## KI-Icon: "ohne Text" und "ohne Überschreiben" (v2.1.4 → v2.2.0)

Zwei von Merten nach dem erfolgreichen praktischen End-to-End-Test von v2.1.4
gewünschte Erweiterungen der KI-Icon-Funktion, noch vor dem für v2.1.4 geplant
gewesenen Installer-/Release-Test:

1. **Icon-Erzeugung ohne Text möglich** (`FolderContentScanner.cs` neu,
   `AiIconService.cs`, `MainForm.cs`): Das Feld "Beschreibung (Prompt)" ist
   jetzt optional. Bleibt es leer, durchsucht das Programm stattdessen selbst
   den Zielordner (`FolderContentScanner.Scan`) und baut daraus eine
   Beschreibung: Ordnername plus die enthaltenen Datei-/Unterordnernamen
   (eigene Zustands-/Sicherungsdateien und frühere KI-Icons werden dabei
   ignoriert). Enthält der Ordner bereits ein Bild, wird dessen Pfad
   zusätzlich als Referenzbild vorgeschlagen. Bei Google (Gemini) wird dieses
   Referenzbild als echte Bild-Eingabe in derselben Anfrage mitgeschickt
   (`AiIconService.BuildGoogleRequestBody`), sodass sich das neue Icon
   inhaltlich/stilistisch daran orientieren kann. Bei OpenAI wirkt sich das
   Referenzbild aktuell nur über die Text-Beschreibung aus - die dort
   verwendete Bild-API (`images/generations`) nimmt keine Bild-Eingabe
   entgegen; eine echte Bild-Eingabe auch für OpenAI (über den separaten
   `images/edits`-Endpunkt) wäre bei Bedarf eine spätere Erweiterung.
2. **Kein Überschreiben mehr bei mehreren Erzeugungen** (`FolderIconService.cs`,
   `MainForm.cs`): Bisher hat jede neu erzeugte KI-Icon-Version die vorherige
   Bilddatei im Zielordner ersetzt. Jetzt bekommt jede Erzeugung einen eigenen,
   eindeutigen Dateinamen mit Zeitstempel
   (`MainForm.BuildUniqueAiIconImagePath`, z.B.
   "Icon-Original (von KI erzeugt) 2026-09-17_14-32-05.png"). Ältere Versionen
   bleiben dadurch im Ordner erhalten, damit mehrere Versuche verglichen und
   bei Bedarf selbst gelöscht werden können. Das zuletzt erzeugte Bild wird
   automatisch als aktuelles Ordner-Icon gesetzt.
   `FolderIconService.ReplaceGeneratedImageAndSetIcon` wurde dafür vereinfacht:
   da der übergebene Zielpfad jetzt garantiert noch nicht existiert, entfällt
   die bisherige Swap-/Rollback-Logik für die Bilddatei selbst (kein
   `SwapIntoPlace`/`UndoSwap` mehr dafür) - ein bereits vorhandener Zielpfad
   wird stattdessen sicherheitshalber mit einer `InvalidOperationException`
   abgelehnt. Schlägt das anschließende Setzen des Ordner-Icons fehl (z.B. weil
   `desktop.ini` gerade gesperrt ist), bleibt die bereits geschriebene
   Bilddatei jetzt bewusst erhalten, statt wie bisher zurückgerollt zu werden -
   ein erzeugtes Bild soll nie allein wegen eines fehlgeschlagenen Icon-Schritts
   verloren gehen.

Neue/angepasste Tests: `FolderContentScannerTests.cs` (neu, 4 Tests: leerer
Ordner, gewöhnliche Dateien, Ordner mit Bild als Referenz, Ignorieren eigener
Zustands-/früherer KI-Icon-Dateien), `AiIconServiceTests.cs` (neuer Test für
`BuildGoogleRequestBody` mit Referenzbild), `FolderIconServiceTests.cs`
(bestehende Überschreib-Tests an das neue Nicht-Überschreiben-Verhalten
angepasst: zweiter Aufruf mit demselben Zielpfad wirft jetzt statt zu
überschreiben, mehrere Aufrufe mit unterschiedlichen Dateinamen behalten alle
Bilder, neuer Test bestätigt, dass die Bilddatei bei fehlgeschlagenem
Icon-Setzen erhalten bleibt).

Von Merten noch zu bestätigen: Build/`dotnet test` auf seinem Rechner, danach
praktischer Test von "Icon ohne Text" (mit und ohne vorhandenes Bild im
Zielordner) sowie mehrfacher Erzeugung im selben Ordner.

## Regressionstests nach der 4. ChatGPT-Gegenprüfung der KI-Icon-Funktion (v2.1.3 → v2.1.4)

Die 4. ChatGPT-Gegenprüfung von Commit `c01977a` (v2.1.3) fand **keinen neuen
freigabeblockierenden Fehler** mehr - Build und 44/44 Tests bestätigt, alle 5
Korrekturen aus der 3. Gegenprüfung technisch bestätigt. Ein nicht
blockierender Punkt blieb offen: nur die Sperrkonflikt-Korrektur hatte einen
eigenen Regressionstest, die vier anderen Korrekturen (Rollbackfehler,
Antwortkörper-Timeout, Bildziel-Begrenzung, Fensterschließen) noch nicht.
Ergänzt:

- **Rollbackfehler** (`FolderIconServiceTests.cs`): ein echter, wiederholbarer
  Test über eine externe Datei-Sperre wäre nur zeitabhängig (Race Condition)
  und damit unzuverlässig - genau wie die bereits dokumentierte
  Testabdeckungs-Lücke aus v2.0.5. Stattdessen bekam `FolderIconService` einen
  internen, nur für Tests gedachten Haken (`TestHook_SimulateDeleteFailure`,
  standardmäßig `null` und im normalen Betrieb ohne jede Wirkung), über den der
  neue Test `ReplaceGeneratedImageAndSetIcon_RollbackLoeschenSchlaegtOhneVorherigesBildFehl_...`
  einen fehlschlagenden Löschvorgang beim Rollback deterministisch auslöst und
  prüft, dass der Fehler jetzt sichtbar wird (statt verschluckt) und die Datei
  absichtlich liegen bleibt.
- **Antwortkörper-Timeout** (`AiIconServiceTests.cs`): `ReadResponseTextWithLimitAsync`
  bekam eine testbare interne Überladung mit explizitem Timeout-Parameter
  (die eigentliche Anwendung nutzt weiterhin ausschließlich die Überladung mit
  dem festen 120-Sekunden-Timeout). Ein neuer Test lässt einen nie endenden
  Test-Stream mit einem sehr kurzen Timeout laufen und prüft, dass der Fehler
  innerhalb weniger Millisekunden statt erst nach der vollen Zeit geworfen wird.
- **Bildziel-Begrenzung** (`FolderIconServiceTests.cs`): neuer Test prüft, dass
  ein Bildziel außerhalb des Zielordners sofort mit `ArgumentException`
  abgelehnt wird, ohne dass irgendetwas geschrieben wird.
- **Fensterschließen**: bewusst OHNE automatisierten Test. `OnFormClosing`
  würde bei laufender KI-Operation einen echten, modalen `MessageBox.Show`-
  Dialog öffnen - das würde einen automatisierten Testlauf dauerhaft blockieren
  statt ihn abzuschließen. Dieses Verhalten bleibt eine bewusst dokumentierte
  Testabdeckungs-Lücke (wie schon die v2.0.5-Lücke) und wurde stattdessen durch
  Code-Pfad-Analyse verifiziert sowie von Merten praktisch mitgetestet.

`dotnet test` erwartet: 47/47 bestanden (44 vorherige + 3 neue). Gepusht auf
`v2.0.0-csharp-neuentwicklung`.

## Änderungen nach der 3. ChatGPT-Gegenprüfung der KI-Icon-Funktion (v2.1.2 → v2.1.3)

Die Gegenprüfung von Commit `94906d2` bestätigte 43/43 bestandene Tests, einen
fehlerfreien Build und saubere Credential-Manager-Korrekturen, fand aber 3
freigabeblockierende Punkte sowie 2 weitere, nicht blockierende Punkte. Alle
wurden behoben:

1. **Rollbackfehler wurde verschluckt** (`FolderIconService.cs`): War beim
   Zurückrollen kein altes KI-Bild vorhanden und ließ sich das neu
   geschriebene, fehlerhafte Bild nicht löschen, hat `UndoSwap` diesen
   Löschfehler bisher über die alles abfangende Aufräum-Methode `TryDelete`
   stillschweigend verschluckt – das Bild blieb trotz angezeigter
   Fehlermeldung liegen. `UndoSwap` löscht jetzt in diesem Fall direkt über
   `File.Delete` (das echte Ausnahmen wirft), sodass ein fehlgeschlagener
   Rollback korrekt als Fehler bei der Sicherungswiederherstellung erkannt
   und gemeldet wird. Die Fehlermeldung in
   `ReplaceGeneratedImageAndSetIconLocked` unterscheidet dabei jetzt korrekt,
   ob überhaupt ein vorheriges Bild vorhanden war.
2. **Kein wirksamer Timeout beim Lesen des Antwortkörpers**
   (`AiIconService.cs`): Durch `HttpCompletionOption.ResponseHeadersRead`
   deckte der bisherige `HttpClient.Timeout` von 120 Sekunden nur die Zeit
   bis zum Empfang der HTTP-Header ab – beim anschließenden Einlesen des
   Antwortkörpers konnte das Programm unbegrenzt hängen bleiben.
   `ReadResponseTextWithLimitAsync` verwendet jetzt einen eigenen,
   auf 120 Sekunden begrenzten `CancellationToken` für das Lesen des
   Antwortkörpers, genau wie von Microsoft für dieses Szenario empfohlen.
3. **Fenster ließ sich während der Transaktion schließen** (`MainForm.cs`):
   Während `GenerateAiIconAsync` lief, konnte das Programmfenster
   geschlossen werden, wodurch der Prozess mitten im Datei-Commit hätte
   enden können, noch bevor Rollback und `finally` ausgeführt wurden. Ein
   neues Feld `_aiOperationInProgress` sowie eine `OnFormClosing`-Behandlung
   verhindern jetzt das Schließen des Fensters, solange eine KI-Icon-Erzeugung
   läuft, und zeigen stattdessen einen klaren Hinweis an.

Nicht blockierende, ebenfalls behobene Punkte:

- **Sequenzieller Test prüfte keine echte Konkurrenz**
  (`FolderIconServiceTests.cs`): Der bisherige Test mit fünf sequenziellen
  Aufrufen wurde umbenannt und beschreibt jetzt korrekt, dass er nur
  wiederholte, nacheinander laufende Aufrufe abdeckt. Ein neuer Test hält die
  ordnerspezifische Sperrdatei extern, ruft `ReplaceGeneratedImageAndSetIcon`
  währenddessen auf und prüft, dass sofort ein klarer `IOException`-Fehler
  geworfen wird, keine Datei verändert wurde und die Methode danach wieder
  normal funktioniert – nach demselben, bereits vorhandenen Muster wie der
  entsprechende Test für `SetFolderIcon`.
- **Bildziel war nicht auf den Zielordner begrenzt** (`FolderIconService.cs`):
  `ReplaceGeneratedImageAndSetIcon` prüft jetzt, dass der übergebene
  `generatedImagePath` tatsächlich im übergebenen `folderPath` liegt, bevor
  irgendetwas geschrieben wird, und wirft andernfalls eine
  `ArgumentException`.

## Änderungen nach der 2. ChatGPT-Gegenprüfung der KI-Icon-Funktion (v2.1.1 → v2.1.2)

Die Gegenprüfung von Commit `a6af83f` bestätigte, dass die 4 vorherigen
Freigabeblocker behoben sind, fand aber 3 NEUE freigabeblockierende Punkte
sowie 4 weitere, nicht blockierende Punkte. Alle wurden behoben:

1. **Sicherung wird nach fehlgeschlagener Wiederherstellung gelöscht**
   (`MainForm.cs`, `FolderIconService.cs`): Die komplette, handgeschriebene
   Temp-Datei/Umbenennen/Verschieben/Rollback-Logik in `MainForm.cs` wurde
   entfernt und durch einen Aufruf der neuen Methode
   `FolderIconService.ReplaceGeneratedImageAndSetIcon` ersetzt. Diese
   verwendet dasselbe, bereits mehrfach geprüfte `SwapIntoPlace`/`UndoSwap`-
   Selbstheilungsmuster mit `safetyPathMustBePreserved`-Out-Parameter, das
   `SetFolderIconLocked` für `FolderIcon.ico`/`desktop.ini` schon einsetzt –
   eine Sicherungsdatei wird jetzt nur dann gelöscht, wenn ihre eigene
   Wiederherstellung tatsächlich geglückt ist.
2. **PNG-Transaktion war nicht instanzübergreifend gesperrt**
   (`FolderIconService.cs`): `ReplaceGeneratedImageAndSetIcon` führt den
   kompletten Ablauf (PNG tauschen + Icon setzen) jetzt als EINE Transaktion
   innerhalb von `RunExclusive` aus – derselben prozessübergreifenden,
   dateibasierten Sperre (`FileShare.None` auf eine versteckte
   `.ordnericonersteller.lock`-Datei je Ordner), die `SetFolderIcon`/
   `RemoveFolderIcon` schon verwenden.
3. **Fehler beim Speichern der Zustimmung außerhalb der Fehlerbehandlung**
   (`MainForm.cs`): `AiConsentService.EnsureGiven(this)` wird in `SaveAiKey()`
   jetzt als erste Anweisung INNERHALB des bestehenden `try`-Blocks
   aufgerufen, und in `GenerateAiIconAsync()` in einem eigenen `try`/`catch`
   mit Log-Eintrag und Fehlerdialog behandelt, statt vor jeder
   Fehlerbehandlung zu stehen.

Nicht blockierende, ebenfalls behobene Punkte:

- **Unvollständige Bild-Dekodierprüfung** (`AiIconService.cs`): Die bisherige
  `ValidateDecodableImage`-Prüfung (`Image.FromStream` + Zugriff auf
  Width/Height) erkannte eine um 1.200 Byte gekürzte, sonst gültige PNG-Datei
  NICHT als beschädigt, da GDI+ dabei im Wesentlichen nur Header/Metadaten
  liest. Die neue Methode `DecodeAndReencodeAsPng` erzwingt stattdessen ein
  vollständiges Neu-Encodieren nach PNG (`Image.Save(..., ImageFormat.Png)`),
  was jede Bildzeile tatsächlich dekodiert und gleichzeitig garantiert, dass
  die gespeicherten Bytes eine echte PNG-Datei sind – unabhängig vom vom
  Anbieter behaupteten MIME-Typ.
- **30-MB-Grenze griff erst nach vollständigem Einlesen/Decodieren**
  (`AiIconService.cs`): HTTP-Antworten werden jetzt mit
  `HttpCompletionOption.ResponseHeadersRead` abgerufen und über
  `ReadResponseTextWithLimitAsync` gestückelt mit einer harten Obergrenze
  gelesen (Content-Length-Vorprüfung bzw. Abbruch während des Lesens), und
  Base64-Antworten werden über `DecodeBase64WithLimit` anhand ihrer
  Zeichenlänge geprüft, BEVOR `Convert.FromBase64String` das komplette
  Bytearray alloziert.
- **Verwaltete Schlüsselkopie wurde bei Überlänge/Allokationsfehler nicht
  genullt** (`ApiKeyStore.cs`): Die Längenprüfung und die
  `Marshal.AllocHGlobal`-Allokation liegen jetzt innerhalb eines äußeren
  `try`/`finally`, dessen `finally`-Block `blobBytes` in JEDEM Fall über
  `CryptographicOperations.ZeroMemory` löscht – auch wenn die Längenprüfung
  wirft oder die Allokation fehlschlägt.
- **Fehlende automatisierte Tests für die riskantesten neuen Codepfade**:
  Die reine Anfrage-Erstellung/Antwort-Auswertung wurde als HTTP-unabhängige,
  interne Methoden extrahiert (`BuildOpenAiRequestBody`,
  `ParseOpenAiImageResponse`, `BuildGoogleRequestBody`,
  `ParseGoogleImageResponse`, `DecodeAndReencodeAsPng`) und wird jetzt in
  `AiIconServiceTests.cs` vollständig offline getestet (gültige/beschädigte/
  übergroße/falsch typisierte Antworten, übersprungene Gemini-"Thinking"-
  Bilder, der tatsächlich erzeugte Request-Body). Zusätzlich deckt
  `FolderIconServiceTests.cs` jetzt `ReplaceGeneratedImageAndSetIcon` ab
  (erstmaliges Setzen, wiederholtes Ersetzen, fehlender Ordner, leere
  Bilddaten, mehrere sequenzielle Aufrufe auf denselben Ordner). Siehe dazu
  auch die Korrektur weiter unten im Abschnitt „Automatisierte Tests
  ausführen".

Zusätzlich beim praktischen Test von Merten gefunden und ebenfalls behoben:

- **Nicht-ASCII-Zeichen im API-Schlüssel** (`ApiKeyStore.cs`): Ein Schlüssel
  mit einem Nicht-ASCII-Zeichen (z. B. ein versehentlich beim Kopieren
  übernommener Umlaut, ein typografisches Anführungszeichen oder ein
  Gedankenstrich) ließ sich bisher speichern, scheiterte dann aber erst beim
  eigentlichen API-Aufruf mit der wenig hilfreichen, englischen .NET-Meldung
  „Request headers must contain only ASCII characters" (kein Absturz – die
  Fehlerbehandlung selbst hat korrekt funktioniert, nur die Meldung war
  unklar und kam zu spät). `ApiKeyStore.SaveKey` prüft jetzt beim Speichern
  direkt auf reine ASCII-Zeichen und zeigt sofort eine klare, deutsche
  Fehlermeldung. Dafür wurde ein neuer Test ergänzt
  (`SaveKey_NichtAsciiZeichen_WirftArgumentException`).

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

**Was automatisiert getestet wird und was nicht:** Ein ECHTER HTTP-Aufruf an
OpenAI/Google ist bewusst NICHT Teil der automatisierten Tests, da er eine
echte, potenziell kostenpflichtige Anfrage auslösen würde. Die eigentliche
Anfrage-Erstellung und Antwort-Auswertung (`BuildOpenAiRequestBody`,
`ParseOpenAiImageResponse`, `BuildGoogleRequestBody`,
`ParseGoogleImageResponse`, `DecodeAndReencodeAsPng`) ist davon unabhängig
und wurde deshalb als eigene, HTTP-entkoppelte interne Methoden ausgelagert –
diese WERDEN automatisiert getestet (`AiIconServiceTests.cs`), inklusive
gültiger/beschädigter/übergroßer/falsch typisierter Antworten und
übersprungener Gemini-„Thinking"-Bilder. (Eine frühere Version dieses
Abschnitts beschrieb die KI-API-Anbindung pauschal als „nicht automatisiert
testbar" – laut ChatGPT-Gegenprüfung war das so nicht richtig, da genau diese
Logik ohne echten Netzwerkaufruf prüfbar ist; diese Beschreibung wurde
entsprechend korrigiert.) Der Speicher-Rundlauf (`ApiKeyStore`:
Speichern/Lesen/Löschen im Anmeldeinformations-Manager) sowie der atomare
PNG-Tausch samt Icon-Setzen (`FolderIconService.ReplaceGeneratedImageAndSetIcon`)
werden ebenfalls automatisiert getestet (`ApiKeyStoreTests.cs` bzw.
`FolderIconServiceTests.cs`).

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

Fertigen Release (Publish-Ordner + Installer) erzeugen: Doppelklick auf
`build-release.bat` im Hauptordner (Details siehe unten und `RELEASE.md`). Der
Publish ist ein normales self-contained Publish in einen Ordner (keine separate
.NET-Installation beim Nutzer nötig), **kein** Single-File und keine
selbstentpackende EXE. Der Publish-Ordner liegt danach unter
`src\OrdnerIconErsteller\publish\` (startbar: `OrdnerIconErsteller.exe` darin).

## Release und Installer bauen (Stand `6e19609`)

Voraussetzung: .NET-8-SDK und [Inno Setup 6](https://jrsoftware.org/isinfo.php)
in einem der Standardordner (kostenlos).

1. Doppelklick auf `build-release.bat` im Hauptordner. Das Skript
   - erzeugt den Publish-Ordner (`dotnet publish -c Release -r win-x64
     --self-contained true`, ohne Single-File),
   - kopiert `release-extras` hinein,
   - kompiliert `installer\OrdnerIconErsteller.iss` mit Inno Setup und
   - gibt SHA-256-Prüfsummen von EXE und Installer aus.
   Es bricht bei jedem Fehler mit einer Meldung ab.
2. Der fertige Installer liegt danach unter
   `installer\output\OrdnerIconErsteller-Setup-<Version>.exe`.
3. Die Versionsnummer steht an zwei Stellen: `Version` in
   `src\OrdnerIconErsteller\OrdnerIconErsteller.csproj` und `MyAppVersion` in
   `installer\OrdnerIconErsteller.iss`.

Der Installer kopiert den gesamten Publish-Ordner (ohne `.pdb`-Dateien) nach
`{app}`. Installieren, Starten und Deinstallieren sind in `RELEASE.md`
beschrieben.

Bewusste Entscheidungen im Installer-Skript (Details als Kommentare direkt in
`installer\OrdnerIconErsteller.iss`):

- Installation nur für den aktuellen Benutzer nach
  `%LOCALAPPDATA%\Programs\Ordner-Icon-Ersteller` - **keine
  Administratorrechte, keine UAC-Abfrage**, passend zum Grundprinzip des
  gesamten Programms (alles läuft über HKCU).
- Der Explorer-Rechtsklick-Menü-Eintrag wird entweder über den Knopf "Ins
  Rechtsklick-Menü integrieren" im Programm gesetzt oder über die
  Installer-Aufgabe "Ins Explorer-Rechtsklick-Menü integrieren" (standardmäßig
  nicht angehakt).
- Beim Deinstallieren wird der Registry-Zweig des Menü-Eintrags
  (`HKCU\Software\Classes\Directory\shell\OrdnerIconErsteller`) vollständig
  entfernt, damit kein toter Menüeintrag auf eine gelöschte EXE zeigt.
- Beim Deinstallieren ruft der Installer vor dem Entfernen der Dateien
  `OrdnerIconErsteller.exe --uninstall-cleanup` auf (ohne Fenster); das
  entfernt die gespeicherten API-Schlüssel und den Zustimmungswert.
- Eine feste `AppId` (GUID) im Skript sorgt dafür, dass eine neue Version die
  alte sauber ersetzt, statt einen zweiten Eintrag in "Apps & Features"
  anzulegen - diese GUID darf sich zwischen Versionen nicht ändern.

**Praktisch getestet (Stand `6e19609`):** Merten hat unter Windows
`build-release.bat` vollständig durchlaufen lassen; Inno Setup erzeugte den
Installer, Installation und Programmstart funktionierten ohne
Virenscanner-Blockierung (AVG). **Nicht getestet** ist bisher die
Deinstallation in diesem Stand (inkl. `--uninstall-cleanup`). Die frühere
Ursache der Virenscanner-Meldung (Vorgang IOE-2026-001, F-004) ist weiterhin
unbekannt; der Erkennungsname wurde nie ermittelt. Das Programm und die EXE
sind unsigniert, ein erneuter Alarm bei anderen Virenscannern oder nach
Signaturupdates lässt sich nicht ausschließen.

Ältere Testangaben in den Abschnitten oben (Single-File-Publish,
`IncludeNativeLibrariesForSelfExtract`, `publish.bat`) beschreiben frühere
Versionsstände und sind durch den neuen Release-Weg ersetzt.

## Automatisierte Tests ausführen

Vom Projekt-Hauptordner aus (nicht aus `src\OrdnerIconErsteller`):

```powershell
dotnet test
```

Das baut Haupt- und Testprojekt und führt die xUnit-Tests aus (43 Tests
insgesamt): der Sicherung/Wiederherstellung-Rundlauf mit exakter
Attribut-Wiederherstellung, die Fehlerpfadtests aus der 2. ChatGPT-Prüfung
(Entfernen ohne Statusdatei, fehlgeschlagener Dateitausch, fehlgeschlagene
Sicherung), die Regressionstests aus der 3. ChatGPT-Prüfung (gesperrte Datei
beim Entfernen, fehlende zweite Sicherung, beschädigte Statusdatei in drei
Varianten, fehlgeschlagene Bilderzeugung nach erfolgreicher Sicherung, zwei
parallele Instanzen auf demselben Ordner), die Regressionstests aus der
4. ChatGPT-Prüfung (beschädigte/fremde Statusdatei beim Setzen in zwei
Varianten, gezielt in der Mitte eines Wiederherstellungs-Tauschs erzwungener
Fehlschlag), fünf neue Tests für `FolderIconService.ReplaceGeneratedImageAndSetIcon`
aus der 2. ChatGPT-Gegenprüfung der KI-Icon-Funktion (erstmaliges Setzen,
wiederholtes atomares Ersetzen, fehlender Zielordner, leere Bilddaten, mehrere
sequenzielle Aufrufe auf denselben Ordner ohne liegen gebliebene Temp-/
Sicherungsdateien), drei Tests für den Credential-Manager-Rundlauf der
KI-Icon-Funktion (`ApiKeyStoreTests.cs`: Speichern/Lesen/Löschen im Rundlauf,
doppeltes Löschen ohne Fehler, Ablehnung eines zu langen Schlüssels – alle
drei ausschließlich über die internen `...ForTesting`-Methoden mit einem pro
Testlauf eindeutigen Target-Namen, sodass sie nie die echten gespeicherten
OpenAI-/Google-Schlüssel berühren, siehe oben "Änderungen nach der
ChatGPT-Prüfung der KI-Icon-Funktion") sowie 18 neue, rein offline laufende
Tests für die HTTP-entkoppelten Kernmethoden der KI-Bilderzeugung
(`AiIconServiceTests.cs`: erzeugte Request-Bodys für OpenAI/Google, Parsen
gültiger/unvollständiger/falsch typisierter Antworten, übersprungene
Gemini-„Thinking"-Bilder, sowie die erzwungene Bild-Dekodierung/
Neu-Encodierung inklusive einer absichtlich abgeschnittenen PNG-Datei – siehe
oben "Änderungen nach der 2. ChatGPT-Gegenprüfung der KI-Icon-Funktion").

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

- Release-/Installer-Weg (`build-release.bat`, Inno Setup) ist vorhanden; Build,
  Installer, Installation und Start sind von Merten getestet (Stand `6e19609`),
  die Deinstallation in diesem Stand noch nicht
- KI-Icon-Erzeugung (OpenAI + Google, Credential Manager, einmaliger Hinweis)
  ist implementiert und wartet auf praktisches Testen durch Merten sowie die
  ChatGPT-Codeprüfung
- danach: README/Disclaimer als eigener Reiter im Programm (vorgemerkt in
  Notion, bewusst kurz vor Projektende geplant)
