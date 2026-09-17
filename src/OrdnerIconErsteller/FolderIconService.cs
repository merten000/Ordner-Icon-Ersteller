using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace OrdnerIconErsteller
{
    /// <summary>
    /// Setzt bzw. entfernt das individuelle Icon eines Ordners nach dem von
    /// Windows vorgesehenen Standardmechanismus (desktop.ini + Icon-Datei
    /// im Ordner selbst). Keine Registry-"Hacks", keine Systemdateien
    /// außerhalb des Zielordners, keine externen Prozesse.
    ///
    /// Sicherheitsprinzipien (nach der 3. ChatGPT-Prüfung von v2.0.2):
    /// - Pro Ordner läuft jeweils nur eine Änderung gleichzeitig (Dateisperre über
    ///   eine eigene Lock-Datei) - zwei gleichzeitig gestartete Instanzen können sich
    ///   dadurch nicht mehr gegenseitig die Sicherung/den Zustand zerstören.
    /// - "Icon setzen" UND "Icon entfernen" sind jeweils EINE Transaktion: entweder
    ///   gelingt der komplette Vorgang (beide Dateien, alle Attribute, der Marker),
    ///   oder es wird vollständig auf den Zustand vor dem Aufruf zurückgerollt -
    ///   unabhängig davon, an welcher Stelle etwas fehlschlägt (Bilderzeugung,
    ///   Dateitausch, Attributänderung, ...).
    /// - "Icon entfernen" prüft VOR jeder Änderung zwingend, dass eine gültige,
    ///   unbeschädigte Statusdatei existiert und alle darin als vorhanden markierten
    ///   Sicherungsdateien tatsächlich existieren. Erst wenn das feststeht, wird
    ///   überhaupt etwas verändert.
    /// - Schlägt sogar das Zurückrollen selbst fehl (z.B. weil eine Datei gerade
    ///   gesperrt ist), werden die dafür nötigen Sicherungskopien bewusst NICHT
    ///   gelöscht, und der Fehler benennt das explizit, statt Datenverlust zu
    ///   riskieren.
    /// - Ein bereits vorhandener, fremder Zustand wird nur gesichert, nie durch
    ///   bloßes Ändern von Attributen an der Originaldatei "vorbereitet". Die
    ///   Originaldatei bleibt unangetastet, bis eine vollständige Sicherung an
    ///   ihrem endgültigen Platz liegt.
    /// - Jeder Dateiaustausch im Zielordner geschieht über <see cref="File.Move"/>
    ///   (Umbenennen), nicht über Copy-Overwrite - das ist auf demselben Volume
    ///   ein einziger, praktisch unteilbarer Schritt statt eines kopierten
    ///   Bytestroms, der mittendrin abbrechen kann.
    /// - Die ursprünglichen Datei-Attribute werden exakt gespeichert und beim
    ///   Entfernen exakt wiederhergestellt, statt pauschal auf Hidden/System
    ///   zurückgesetzt zu werden.
    /// </summary>
    internal static class FolderIconService
    {
        private const string IconFileName = "FolderIcon.ico";
        private const string DesktopIniName = "desktop.ini";
        private const string BackupSuffix = ".before-ordnericonersteller.bak";
        private const string MarkerFileName = ".ordnericonersteller-state";
        private const string LockFileName = ".ordnericonersteller.lock";
        private const string ExpectedFormatVersion = "2";

        /// <summary>
        /// Setzt das Ordner-Icon als eine einzige Transaktion. Vorhandene
        /// desktop.ini/Icon-Datei/Ordnerattribute werden vor der ersten Änderung
        /// gesichert (siehe <see cref="RemoveFolderIcon"/>). Schlägt irgendein
        /// Schritt fehl - auch die Bilderzeugung oder eine Attributänderung nach
        /// dem Dateitausch - wird der komplette Aufruf zurückgerollt.
        /// </summary>
        public static void SetFolderIcon(string folderPath, string sourceImagePath)
        {
            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException($"Ordner nicht gefunden: {folderPath}");
            if (!File.Exists(sourceImagePath))
                throw new FileNotFoundException($"Bilddatei nicht gefunden: {sourceImagePath}");

            RunExclusive(folderPath, () => SetFolderIconLocked(folderPath, sourceImagePath));
        }

        private static void SetFolderIconLocked(string folderPath, string sourceImagePath)
        {
            string icoPath = Path.Combine(folderPath, IconFileName);
            string desktopIniPath = Path.Combine(folderPath, DesktopIniName);
            string markerPath = Path.Combine(folderPath, MarkerFileName);
            string icoBackupPath = icoPath + BackupSuffix;
            string iniBackupPath = desktopIniPath + BackupSuffix;

            bool markerExistedBefore = File.Exists(markerPath);
            bool createdBackupsThisCall = false;

            // Nur beim allerersten Aufruf für diesen Ordner den Ursprungszustand sichern -
            // ein zweites "Icon setzen" darf nicht die (schon einmal gesicherte) Originaldatei
            // mit unserer eigenen, zwischenzeitlich gesetzten Version überschreiben.
            if (!markerExistedBefore)
            {
                bool folderHadReadOnly = (new DirectoryInfo(folderPath).Attributes & FileAttributes.ReadOnly) != 0;
                try
                {
                    bool iniHasBackup = BackupIfExists(desktopIniPath, iniBackupPath, out int iniAttributes);
                    bool icoHasBackup = BackupIfExists(icoPath, icoBackupPath, out int icoAttributes);

                    WriteMarker(markerPath, new BackupState
                    {
                        FolderHadReadOnly = folderHadReadOnly,
                        DesktopIniHasBackup = iniHasBackup,
                        DesktopIniAttributes = iniAttributes,
                        IconHasBackup = icoHasBackup,
                        IconAttributes = icoAttributes,
                    });
                    createdBackupsThisCall = true;
                }
                catch
                {
                    // Die Sicherung ist fehlgeschlagen. BackupIfExists fasst die
                    // Originaldatei nie an, bevor die Sicherungskopie an ihrem
                    // endgültigen Platz liegt - deshalb genügt es hier, eventuell
                    // schon angelegte (unvollständige) Sicherungsdateien wieder zu
                    // entfernen, damit kein halbfertiger Zustand zurückbleibt.
                    TryDelete(iniBackupPath);
                    TryDelete(icoBackupPath);
                    TryDelete(markerPath);
                    throw;
                }
            }
            else
            {
                // Ein Marker existiert bereits - das kann unser eigener, gültiger Zustand
                // aus einem früheren "Icon setzen" für diesen Ordner sein (dann DARF nicht
                // erneut gesichert werden, sonst würde unsere eigene, zwischenzeitlich
                // gesetzte Version die echte Originalsicherung überschreiben), oder eine
                // beschädigte/fremde/manipulierte Statusdatei. Deshalb wird er - genau wie
                // bei "Icon entfernen" - vor jeder Änderung streng validiert, inklusive
                // Vorhandensein aller darin referenzierten Sicherungsdateien. Jede
                // Abweichung bricht sofort ab, BEVOR irgendetwas im Zielordner verändert
                // wird - insbesondere werden dadurch vorhandene fremde Dateien nie
                // ungesichert überschrieben, nur weil zufällig eine Datei mit dem Namen
                // der Statusdatei dort liegt.
                BackupState existingState = ReadMarker(markerPath);

                if (existingState.DesktopIniHasBackup && !File.Exists(iniBackupPath))
                {
                    throw new InvalidDataException(
                        $"Erwartete Sicherungsdatei fehlt: {iniBackupPath}. Aus Sicherheitsgründen wird " +
                        "nichts verändert - bitte den Ordnerinhalt manuell prüfen.");
                }
                if (existingState.IconHasBackup && !File.Exists(icoBackupPath))
                {
                    throw new InvalidDataException(
                        $"Erwartete Sicherungsdatei fehlt: {icoBackupPath}. Aus Sicherheitsgründen wird " +
                        "nichts verändert - bitte den Ordnerinhalt manuell prüfen.");
                }
            }

            string tempIco = UniqueTempPath(folderPath, ".ico.tmp");
            string tempIni = UniqueTempPath(folderPath, ".ini.tmp");
            string icoSafety = UniqueTempPath(folderPath, ".ico.safety");
            string iniSafety = UniqueTempPath(folderPath, ".ini.safety");

            bool icoSwapped = false;
            bool iniSwapped = false;
            bool folderAttrSetByUs = false;
            bool safetyFilesStillNeeded = false;
            bool icoSafetyMustBePreserved = false;
            bool iniSafetyMustBePreserved = false;

            try
            {
                try
                {
                    // Diese komplette innere Transaktion (Bilderzeugung bis Ordnerattribut)
                    // wird bei JEDEM Fehlschlag vollständig zurückgerollt - nicht nur ein
                    // Fehlschlag beim Dateitausch selbst.
                    IconWriter.CreateIcoFromImage(sourceImagePath, tempIco);

                    string content =
                        "[.ShellClassInfo]" + Environment.NewLine +
                        "IconResource=" + IconFileName + ",0" + Environment.NewLine +
                        "IconFile=" + IconFileName + Environment.NewLine +
                        "IconIndex=0" + Environment.NewLine;

                    // WICHTIG: Windows liest desktop.ini über die klassische Win32-INI-API
                    // (GetPrivateProfileString). Die erkennt UTF-8 (auch mit BOM) nicht
                    // zuverlässig - dadurch blieb das Icon in der ersten Fassung wirkungslos.
                    // ANSI oder Unicode (UTF-16LE mit BOM) werden zuverlässig erkannt; wir
                    // verwenden Unicode, damit auch Sonderzeichen im Pfad keine Rolle spielen.
                    File.WriteAllText(tempIni, content, Encoding.Unicode);

                    SwapIntoPlace(icoPath, tempIco, icoSafety, out icoSafetyMustBePreserved);
                    icoSwapped = true;

                    SwapIntoPlace(desktopIniPath, tempIni, iniSafety, out iniSafetyMustBePreserved);
                    iniSwapped = true;

                    File.SetAttributes(icoPath, FileAttributes.Hidden | FileAttributes.System);
                    File.SetAttributes(desktopIniPath, FileAttributes.Hidden | FileAttributes.System);

                    // Windows liest desktop.ini nur, wenn der Ordner als "angepasst" markiert
                    // ist (klassisch über das ReadOnly-Bit auf Ordnern - hat NICHTS mit
                    // Schreibschutz zu tun). Nur setzen (und später ggf. zurückrollen), wenn
                    // es nicht schon gesetzt war.
                    var dirInfo = new DirectoryInfo(folderPath);
                    if ((dirInfo.Attributes & FileAttributes.ReadOnly) == 0)
                    {
                        dirInfo.Attributes |= FileAttributes.ReadOnly;
                        folderAttrSetByUs = true;
                    }
                }
                catch (Exception primary)
                {
                    var rollbackErrors = new List<Exception>();

                    if (folderAttrSetByUs)
                    {
                        try
                        {
                            var dirInfo = new DirectoryInfo(folderPath);
                            dirInfo.Attributes &= ~FileAttributes.ReadOnly;
                        }
                        catch (Exception e) { rollbackErrors.Add(e); }
                    }
                    // Ein "echtes" Zurückrollen ist hier nur nötig, wenn der jeweilige
                    // Tausch VOLLSTÄNDIG erfolgreich war (icoSwapped/iniSwapped) - schlug
                    // er selbst fehl, hat SwapIntoPlace bereits intern versucht, sich
                    // selbst zu heilen (siehe icoSafetyMustBePreserved/
                    // iniSafetyMustBePreserved unten).
                    if (iniSwapped)
                    {
                        try { UndoSwap(desktopIniPath, iniSafety); }
                        catch (Exception e) { rollbackErrors.Add(e); }
                    }
                    if (icoSwapped)
                    {
                        try { UndoSwap(icoPath, icoSafety); }
                        catch (Exception e) { rollbackErrors.Add(e); }
                    }

                    // Wenn SwapIntoPlace selbst nicht nur fehlgeschlagen ist, sondern auch
                    // seine eigene Selbstheilung nicht abschließen konnte, MUSS die
                    // betroffene Safety-Datei erhalten bleiben - unabhängig davon, ob
                    // icoSwapped/iniSwapped je gesetzt wurden (wurden sie nicht, da der
                    // Aufruf selbst geworfen hat) und unabhängig von rollbackErrors. Sonst
                    // würde der äußere finally-Block gleich die einzige verbliebene Kopie
                    // der vorherigen Datei löschen - genau der von der 5. ChatGPT-Prüfung
                    // gemeldete Restfehler.
                    if (rollbackErrors.Count > 0 || icoSafetyMustBePreserved || iniSafetyMustBePreserved)
                    {
                        safetyFilesStillNeeded = true;
                        var all = new List<Exception> { primary };
                        all.AddRange(rollbackErrors);
                        throw new AggregateException(
                            $"\"Icon setzen\" ist fehlgeschlagen, und das automatische Zurückrollen " +
                            $"konnte nicht vollständig abgeschlossen werden. Sicherungsdateien in " +
                            $"\"{folderPath}\" (Namen mit \".safety\") bleiben deshalb bewusst " +
                            "erhalten - bitte den Ordner manuell prüfen, bevor er weiterverwendet wird.",
                            all);
                    }

                    if (createdBackupsThisCall)
                    {
                        ClearAttributesIfExists(markerPath);
                        TryDelete(markerPath);
                        TryDelete(iniBackupPath);
                        TryDelete(icoBackupPath);
                    }
                    throw;
                }

                RefreshShellIcon(folderPath);
            }
            finally
            {
                TryDelete(tempIco);
                TryDelete(tempIni);
                if (!safetyFilesStillNeeded)
                {
                    TryDelete(icoSafety);
                    TryDelete(iniSafety);
                }
            }
        }

        /// <summary>
        /// Schreibt die von der KI erzeugten Bilddaten als
        /// "Icon-Original (von KI erzeugt).png" in den Zielordner und setzt danach
        /// das Ordner-Icon daraus - beides als EINE Transaktion unter derselben
        /// prozessübergreifenden Ordnersperre wie <see cref="SetFolderIcon"/> und
        /// <see cref="RemoveFolderIcon"/> (siehe <see cref="RunExclusive"/>).
        ///
        /// Das behebt zwei von ChatGPT bei der KI-Icon-Funktion gefundene
        /// Freigabeblocker: (1) eine zweite, gleichzeitig gestartete Instanz konnte
        /// dieselbe PNG-Datei/denselben Icon-Vorgang parallel bearbeiten, weil die
        /// PNG-Sicherung/-Ersetzung bisher außerhalb jeder Sperre in MainForm lag;
        /// (2) ein fehlgeschlagenes Zurückholen der Sicherung konnte dazu führen,
        /// dass der äußere Aufräum-Code die einzige verbliebene Kopie des alten
        /// Originals löscht. Beides wird jetzt durch dieselben bereits mehrfach
        /// geprüften Bausteine (<see cref="SwapIntoPlace"/>/<see cref="UndoSwap"/>
        /// mit ihrem "safetyPathMustBePreserved"-Mechanismus) abgedeckt wie beim
        /// Setzen von Icon.ico/desktop.ini.
        /// </summary>
        public static void ReplaceGeneratedImageAndSetIcon(string folderPath, byte[] imageBytes, string generatedImagePath)
        {
            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException($"Ordner nicht gefunden: {folderPath}");
            if (imageBytes is null || imageBytes.Length == 0)
                throw new ArgumentException("Es wurden keine Bilddaten übergeben.", nameof(imageBytes));
            if (string.IsNullOrWhiteSpace(generatedImagePath))
                throw new ArgumentException("Es wurde kein Zielpfad für die Bilddatei übergeben.", nameof(generatedImagePath));

            // Verteidigung in der Tiefe: generatedImagePath kommt vom Aufrufer (aktuell
            // immer per Path.Combine mit folderPath gebildet), wurde hier bisher aber nie
            // selbst geprüft. Ein künftiger oder fehlerhafter Aufrufer könnte sonst
            // versehentlich (oder über einen manipulierten Prompt/Dateinamen mit "..")
            // eine Datei außerhalb des eigentlich gesperrten Zielordners schreiben lassen.
            string normalizedFolder = Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedImagePath = Path.GetFullPath(generatedImagePath);
            string? imageDirectory = Path.GetDirectoryName(normalizedImagePath);
            if (imageDirectory is null
                || !string.Equals(imageDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    normalizedFolder, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"Der Zielpfad für die Bilddatei (\"{normalizedImagePath}\") liegt nicht direkt im " +
                    $"Zielordner (\"{normalizedFolder}\") - aus Sicherheitsgründen wird nichts geschrieben.",
                    nameof(generatedImagePath));
            }

            RunExclusive(folderPath, () => ReplaceGeneratedImageAndSetIconLocked(folderPath, imageBytes, generatedImagePath));
        }

        private static void ReplaceGeneratedImageAndSetIconLocked(string folderPath, byte[] imageBytes, string generatedImagePath)
        {
            string tempImage = UniqueTempPath(folderPath, ".ai-icon.tmp");
            string imageSafety = UniqueTempPath(folderPath, ".ai-icon.safety");
            bool imageSwapped = false;
            bool imageSafetyMustBePreserved = false;

            try
            {
                try
                {
                    File.WriteAllBytes(tempImage, imageBytes);

                    // Vorhandenes altes "Icon-Original" wird von SwapIntoPlace zuerst
                    // nach imageSafety verschoben (nicht gelöscht) - schlägt der
                    // zweite Schritt fehl, holt sich SwapIntoPlace das selbst
                    // sofort zurück und signalisiert über
                    // imageSafetyMustBePreserved verbindlich, falls selbst das
                    // misslingt (siehe Kommentar dort).
                    SwapIntoPlace(generatedImagePath, tempImage, imageSafety, out imageSafetyMustBePreserved);
                    imageSwapped = true;

                    // Bereits innerhalb derselben Ordnersperre - kein erneutes
                    // RunExclusive (würde sich selbst blockieren).
                    SetFolderIconLocked(folderPath, generatedImagePath);
                }
                catch (Exception primary)
                {
                    if (imageSwapped)
                    {
                        try
                        {
                            UndoSwap(generatedImagePath, imageSafety);
                        }
                        catch (Exception undoEx)
                        {
                            // Entweder ist die interne Selbstheilung aus SwapIntoPlace
                            // gescheitert (die vorherige Datei liegt dann nur noch unter
                            // imageSafety), oder es gab vorher gar keine Datei und das
                            // Entfernen der gerade erst eingesetzten neuen Datei ist
                            // fehlgeschlagen (z.B. weil sie gerade von einem
                            // Virenscanner geöffnet ist). In beiden Fällen darf
                            // imageSafety nicht als "nicht mehr benötigt" gelöscht
                            // werden, und der Fehler muss sichtbar bleiben statt eines
                            // fälschlich als vollständig geltenden Rollbacks.
                            imageSafetyMustBePreserved = true;
                            bool hadPreviousImage = File.Exists(imageSafety);
                            string detail = hadPreviousImage
                                ? $"das vorherige \"Icon-Original\" konnte aus \"{imageSafety}\" nicht " +
                                  "automatisch zurückgeholt werden. Die Datei bleibt dort bewusst erhalten"
                                : $"die gerade erst geschriebene neue Bilddatei \"{generatedImagePath}\" " +
                                  "konnte beim Zurückrollen nicht entfernt werden. Sie bleibt trotz dieser " +
                                  "Fehlermeldung dort liegen";
                            throw new AggregateException(
                                $"Das Setzen des KI-Icons ist fehlgeschlagen, und das automatische " +
                                $"Zurückrollen konnte nicht vollständig abgeschlossen werden: {detail} - " +
                                "bitte manuell prüfen.",
                                new[] { primary, undoEx });
                        }
                    }
                    throw;
                }
            }
            finally
            {
                TryDelete(tempImage);
                if (!imageSafetyMustBePreserved)
                {
                    TryDelete(imageSafety);
                }
            }
        }

        /// <summary>
        /// Entfernt das von diesem Programm gesetzte Ordner-Icon wieder, als eine
        /// einzige Transaktion. Das geschieht ausschließlich anhand der Statusdatei,
        /// die beim ersten "Icon setzen" für diesen Ordner angelegt wurde - ohne diese
        /// (oder mit einer beschädigten/unplausiblen) Statusdatei wird nichts verändert.
        /// Vor jeder Änderung wird zusätzlich geprüft, dass alle laut Statusdatei nötigen
        /// Sicherungsdateien tatsächlich vorhanden sind. War vor dem ersten "Icon setzen"
        /// bereits eine eigene desktop.ini/Icon-Datei vorhanden, wird genau diese
        /// inklusive der ursprünglichen Datei-Attribute wiederhergestellt statt nur
        /// gelöscht.
        /// </summary>
        public static void RemoveFolderIcon(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException($"Ordner nicht gefunden: {folderPath}");

            RunExclusive(folderPath, () => RemoveFolderIconLocked(folderPath));
        }

        private static void RemoveFolderIconLocked(string folderPath)
        {
            string icoPath = Path.Combine(folderPath, IconFileName);
            string desktopIniPath = Path.Combine(folderPath, DesktopIniName);
            string markerPath = Path.Combine(folderPath, MarkerFileName);
            string icoBackupPath = icoPath + BackupSuffix;
            string iniBackupPath = desktopIniPath + BackupSuffix;

            if (!File.Exists(markerPath))
            {
                throw new InvalidOperationException(
                    "Für diesen Ordner wurde keine von Ordner-Icon-Ersteller gesetzte Anpassung " +
                    "gefunden. Es wurde nichts verändert.");
            }

            // Liest UND validiert die Statusdatei streng (Formatversion, echte 0/1-Werte,
            // plausible Attribut-Bits) - jede Abweichung bricht ab, BEVOR irgendetwas
            // verändert wird.
            BackupState state = ReadMarker(markerPath);

            // Vollständige Vorprüfung: alle laut Statusdatei nötigen Sicherungsdateien
            // müssen JETZT existieren. Sonst wird gar nichts angefasst - insbesondere wird
            // nicht erst die eine Datei wiederhergestellt (und ihre Sicherung dabei
            // verbraucht), um dann bei der zweiten Datei zu scheitern.
            if (state.DesktopIniHasBackup && !File.Exists(iniBackupPath))
            {
                throw new InvalidDataException(
                    $"Erwartete Sicherungsdatei fehlt: {iniBackupPath}. Aus Sicherheitsgründen wird " +
                    "nichts verändert - bitte den Ordnerinhalt manuell prüfen.");
            }
            if (state.IconHasBackup && !File.Exists(icoBackupPath))
            {
                throw new InvalidDataException(
                    $"Erwartete Sicherungsdatei fehlt: {icoBackupPath}. Aus Sicherheitsgründen wird " +
                    "nichts verändert - bitte den Ordnerinhalt manuell prüfen.");
            }

            string iniRestoreSafety = UniqueTempPath(folderPath, ".ini.restoresafety");
            string icoRestoreSafety = UniqueTempPath(folderPath, ".ico.restoresafety");
            string iniDeleteSafety = UniqueTempPath(folderPath, ".ini.deletesafety");
            string icoDeleteSafety = UniqueTempPath(folderPath, ".ico.deletesafety");

            bool iniHandled = false;
            bool icoHandled = false;
            bool folderAttrChanged = false;
            bool safetyFilesStillNeeded = false;
            bool iniSafetyMustBePreserved = false;
            bool icoSafetyMustBePreserved = false;

            try
            {
                try
                {
                    if (state.DesktopIniHasBackup)
                        SwapFromBackupIntoPlace(desktopIniPath, iniBackupPath, iniRestoreSafety,
                            out iniSafetyMustBePreserved);
                    else
                        MoveToDeleteSafetyIfExists(desktopIniPath, iniDeleteSafety);
                    iniHandled = true;

                    if (state.IconHasBackup)
                        SwapFromBackupIntoPlace(icoPath, icoBackupPath, icoRestoreSafety,
                            out icoSafetyMustBePreserved);
                    else
                        MoveToDeleteSafetyIfExists(icoPath, icoDeleteSafety);
                    icoHandled = true;

                    // Die exakten Original-Attribute erst NACH dem erfolgreichen Zurücktauschen
                    // setzen (nur relevant, wenn tatsächlich wiederhergestellt wurde).
                    if (state.DesktopIniHasBackup)
                        File.SetAttributes(desktopIniPath, (FileAttributes)state.DesktopIniAttributes);
                    if (state.IconHasBackup)
                        File.SetAttributes(icoPath, (FileAttributes)state.IconAttributes);

                    var dirInfo = new DirectoryInfo(folderPath);
                    bool currentlyReadOnly = (dirInfo.Attributes & FileAttributes.ReadOnly) != 0;
                    if (currentlyReadOnly != state.FolderHadReadOnly)
                    {
                        if (state.FolderHadReadOnly)
                            dirInfo.Attributes |= FileAttributes.ReadOnly;
                        else
                            dirInfo.Attributes &= ~FileAttributes.ReadOnly;
                        folderAttrChanged = true;
                    }

                    // Die Statusdatei wird erst entfernt, wenn wirklich ALLE vorherigen
                    // Schritte nachweislich erfolgreich waren. Ein Löschfehler hier ist ein
                    // echter Fehler (kein Best-effort) und muss sichtbar werden.
                    ClearAttributesIfExists(markerPath);
                    File.Delete(markerPath);
                }
                catch (Exception primary)
                {
                    var rollbackErrors = new List<Exception>();

                    if (folderAttrChanged)
                    {
                        try
                        {
                            var dirInfo = new DirectoryInfo(folderPath);
                            if (state.FolderHadReadOnly)
                                dirInfo.Attributes &= ~FileAttributes.ReadOnly;
                            else
                                dirInfo.Attributes |= FileAttributes.ReadOnly;
                        }
                        catch (Exception e) { rollbackErrors.Add(e); }
                    }

                    // Umgekehrte Reihenfolge: zuletzt bearbeitete Datei zuerst zurückrollen.
                    if (icoHandled)
                    {
                        try
                        {
                            if (state.IconHasBackup)
                                UndoSwapFromBackup(icoPath, icoBackupPath, icoRestoreSafety);
                            else
                                UndoMoveToDeleteSafety(icoPath, icoDeleteSafety);
                        }
                        catch (Exception e) { rollbackErrors.Add(e); }
                    }
                    if (iniHandled)
                    {
                        try
                        {
                            if (state.DesktopIniHasBackup)
                                UndoSwapFromBackup(desktopIniPath, iniBackupPath, iniRestoreSafety);
                            else
                                UndoMoveToDeleteSafety(desktopIniPath, iniDeleteSafety);
                        }
                        catch (Exception e) { rollbackErrors.Add(e); }
                    }

                    // Wie bei "Icon setzen": scheiterte SwapFromBackupIntoPlace nicht nur,
                    // sondern konnte auch seine eigene Selbstheilung nicht abschließen,
                    // MUSS die betroffene Safety-Datei erhalten bleiben - unabhängig davon,
                    // ob iniHandled/icoHandled je gesetzt wurden und unabhängig von
                    // rollbackErrors. Sonst würde der äußere finally-Block die einzige
                    // verbliebene Kopie der vorherigen (von uns gesetzten) Datei löschen.
                    if (rollbackErrors.Count > 0 || iniSafetyMustBePreserved || icoSafetyMustBePreserved)
                    {
                        safetyFilesStillNeeded = true;
                        var all = new List<Exception> { primary };
                        all.AddRange(rollbackErrors);
                        throw new AggregateException(
                            $"\"Icon entfernen\" ist fehlgeschlagen, und das automatische Zurückrollen " +
                            $"konnte nicht vollständig abgeschlossen werden. Sicherungsdateien in " +
                            $"\"{folderPath}\" bleiben deshalb bewusst erhalten - bitte den Ordner " +
                            "manuell prüfen. Die Statusdatei wurde NICHT entfernt.", all);
                    }

                    throw;
                }

                RefreshShellIcon(folderPath);
            }
            finally
            {
                if (!safetyFilesStillNeeded)
                {
                    TryDelete(iniRestoreSafety);
                    TryDelete(icoRestoreSafety);
                    TryDelete(iniDeleteSafety);
                    TryDelete(icoDeleteSafety);
                }
            }
        }

        // --- Ausschluss gleichzeitiger Zugriffe auf denselben Ordner ---

        /// <summary>
        /// Führt <paramref name="action"/> aus, während eine exklusive Dateisperre für
        /// diesen Ordner gehalten wird. Eine zweite, gleichzeitig gestartete Instanz von
        /// Ordner-Icon-Ersteller erhält sofort einen klaren Fehler, statt sich mit dieser
        /// Instanz gegenseitig Sicherungen/Zustand zu zerstören.
        /// </summary>
        private static void RunExclusive(string folderPath, Action action)
        {
            string lockPath = Path.Combine(folderPath, LockFileName);
            FileStream lockStream;
            try
            {
                lockStream = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException ex)
            {
                throw new IOException(
                    "Dieser Ordner wird gerade von einer anderen Instanz von Ordner-Icon-Ersteller " +
                    "bearbeitet. Bitte kurz warten und erneut versuchen.", ex);
            }

            try
            {
                try { File.SetAttributes(lockPath, FileAttributes.Hidden | FileAttributes.System); }
                catch { /* rein kosmetisch, nicht kritisch */ }

                action();
            }
            finally
            {
                lockStream.Dispose();
                TryDelete(lockPath); // best effort - harmlos, falls schon eine andere Instanz wartet
            }
        }

        // --- Austausch einzelner Dateien: atomar + zurückrollbar ---

        /// <summary>
        /// Ersetzt <paramref name="targetPath"/> durch <paramref name="newContentPath"/>.
        /// Eine vorhandene Zieldatei wird zuerst per <see cref="File.Move"/> (nicht
        /// kopiert) nach <paramref name="safetyPath"/> verschoben, damit sie bei einem
        /// Fehlschlag über <see cref="UndoSwap"/> unverändert (gleiche Bytes, gleiche
        /// Attribute) zurückgeholt werden kann.
        /// </summary>
        private static void SwapIntoPlace(string targetPath, string newContentPath, string safetyPath,
            out bool safetyPathMustBePreserved)
        {
            // WICHTIG: dieser Parameter wird von der aufrufenden Methode auch dann noch
            // ausgewertet, wenn dieser Aufruf mit einer Ausnahme endet (C# aktualisiert
            // "out"-Parameter, sobald sie zugewiesen werden - unabhängig davon, ob die
            // Methode danach normal zurückkehrt oder wirft). Das ist genau der Mechanismus,
            // mit dem der Aufrufer erfährt, dass diese Sicherungsdatei erhalten bleiben
            // MUSS, obwohl sein eigenes "geswapt"-Flag (das er erst nach einem erfolgreichen
            // Rückgabewert setzt) in diesem Fall nie gesetzt wird.
            safetyPathMustBePreserved = false;

            bool movedTargetToSafety = false;
            if (File.Exists(targetPath))
            {
                File.Move(targetPath, safetyPath, overwrite: true);
                movedTargetToSafety = true;
            }

            try
            {
                File.Move(newContentPath, targetPath, overwrite: true);
            }
            catch (Exception primary)
            {
                // Der zweite Schritt ist fehlgeschlagen. Diese Funktion muss den ersten
                // Schritt SOFORT selbst rückgängig machen, statt sich auf den Aufrufer zu
                // verlassen: der setzt sein "geswapt"-Flag erst NACH einem erfolgreichen
                // Rückgabewert dieser Methode und würde diesen bereits begonnenen, aber
                // fehlgeschlagenen Tausch sonst nie zurückrollen - die vorherige Datei
                // wäre dann verloren (nur noch unter dem Safety-Pfad vorhanden).
                if (movedTargetToSafety)
                {
                    try
                    {
                        File.Move(safetyPath, targetPath, overwrite: true);
                    }
                    catch (Exception undoEx)
                    {
                        // Auch die interne Selbstheilung ist gescheitert: die vorherige
                        // Datei liegt jetzt NUR NOCH unter safetyPath. Das muss dem
                        // Aufrufer unabhängig von jeder Flag-Logik verbindlich signalisiert
                        // werden, damit safetyPath nicht als "nicht mehr benötigt" gelöscht
                        // wird.
                        safetyPathMustBePreserved = true;
                        throw new AggregateException(
                            $"Der Dateitausch für \"{targetPath}\" ist fehlgeschlagen, und die vorherige " +
                            $"Datei konnte aus \"{safetyPath}\" nicht automatisch zurückgeholt werden. Die " +
                            "Datei bleibt dort bewusst erhalten - bitte manuell prüfen.",
                            new[] { primary, undoEx });
                    }
                }
                throw;
            }
        }

        /// <summary>
        /// Macht einen erfolgreichen <see cref="SwapIntoPlace"/> rückgängig. Existierte
        /// vorher eine Datei an <paramref name="targetPath"/>, liegt sie jetzt unter
        /// <paramref name="safetyPath"/> und wird zurückgeholt. Existierte vorher NICHTS,
        /// muss die gerade erst eingesetzte neue Datei wieder entfernt werden - bewusst
        /// über <see cref="File.Delete(string)"/> statt <see cref="TryDelete"/>: Ein
        /// stillschweigend verschlucktes Löschfehlschlagen (z.B. weil die Datei gerade
        /// von einem Virenscanner oder der Vorschau geöffnet ist) hätte dazu geführt,
        /// dass der Aufrufer fälschlich ein vollständiges Zurückrollen annimmt, obwohl
        /// die neue Datei trotz Fehlermeldung liegen bleibt (ChatGPT-Gegenprüfung von
        /// v2.1.2, Freigabeblocker 1).
        /// </summary>
        private static void UndoSwap(string targetPath, string safetyPath)
        {
            if (File.Exists(safetyPath))
            {
                ClearAttributesIfExists(targetPath);
                File.Move(safetyPath, targetPath, overwrite: true);
            }
            else if (File.Exists(targetPath))
            {
                ClearAttributesIfExists(targetPath);
                File.Delete(targetPath);
            }
        }

        /// <summary>
        /// Ersetzt <paramref name="targetPath"/> durch den Inhalt der SICHERUNGSDATEI
        /// <paramref name="backupPath"/> (die dabei "verbraucht" wird - genau wie bei einer
        /// erfolgreichen Wiederherstellung gewollt). Die vorherige (von uns gesetzte)
        /// Datei wird nach <paramref name="safetyPath"/> verschoben. Im Unterschied zu
        /// <see cref="UndoSwap"/> muss das Rückgängigmachen hier die Sicherungsdatei an
        /// ihrem ursprünglichen Platz wiederherstellen, nicht nur löschen - siehe
        /// <see cref="UndoSwapFromBackup"/>.
        /// </summary>
        private static void SwapFromBackupIntoPlace(string targetPath, string backupPath, string safetyPath,
            out bool safetyPathMustBePreserved)
        {
            // Siehe SwapIntoPlace: dieser "out"-Parameter wird vom Aufrufer auch dann noch
            // ausgewertet, wenn dieser Aufruf mit einer Ausnahme endet.
            safetyPathMustBePreserved = false;

            bool movedTargetToSafety = false;
            if (File.Exists(targetPath))
            {
                File.Move(targetPath, safetyPath, overwrite: true);
                movedTargetToSafety = true;
            }

            try
            {
                File.Move(backupPath, targetPath, overwrite: true);
            }
            catch (Exception primary)
            {
                // Wie bei SwapIntoPlace: der zweite Schritt ist fehlgeschlagen, also muss
                // diese Funktion den ersten Schritt SOFORT selbst rückgängig machen. Der
                // Aufrufer setzt sein "behandelt"-Flag erst nach einem erfolgreichen
                // Rückgabewert und würde diesen bereits begonnenen Restore-Versuch sonst
                // nie zurückrollen - die vorherige (von uns gesetzte) Datei wäre dann nur
                // noch unter dem Safety-Pfad vorhanden statt an ihrem eigentlichen Platz.
                if (movedTargetToSafety)
                {
                    try
                    {
                        File.Move(safetyPath, targetPath, overwrite: true);
                    }
                    catch (Exception undoEx)
                    {
                        // Auch die interne Selbstheilung ist gescheitert: verbindlich
                        // signalisieren, dass safetyPath erhalten bleiben MUSS.
                        safetyPathMustBePreserved = true;
                        throw new AggregateException(
                            $"Die Wiederherstellung aus der Sicherung für \"{targetPath}\" ist " +
                            $"fehlgeschlagen, und die vorherige Datei konnte aus \"{safetyPath}\" nicht " +
                            "automatisch zurückgeholt werden. Die Datei bleibt dort bewusst erhalten - " +
                            "bitte manuell prüfen.",
                            new[] { primary, undoEx });
                    }
                }
                throw;
            }
        }

        private static void UndoSwapFromBackup(string targetPath, string backupPath, string safetyPath)
        {
            // targetPath enthält aktuell (ganz oder teilweise) den Inhalt aus backupPath -
            // zuerst zurück in die Sicherungsdatei verschieben, damit sie erhalten bleibt...
            if (File.Exists(targetPath))
            {
                File.Move(targetPath, backupPath, overwrite: true);
            }
            // ...dann unsere eigene, vorherige Version aus dem Safety-Slot zurückholen.
            if (File.Exists(safetyPath))
            {
                File.Move(safetyPath, targetPath, overwrite: true);
            }
        }

        /// <summary>
        /// Verschiebt <paramref name="currentPath"/> (statt es direkt zu löschen) nach
        /// <paramref name="deleteSafetyPath"/>, falls die Datei existiert - reversibel über
        /// <see cref="UndoMoveToDeleteSafety"/>. Schlägt das Verschieben fehl (z.B. weil die
        /// Datei gerade exklusiv gesperrt ist), wird die Ausnahme bewusst NICHT
        /// abgefangen: ein "Löschfehler" darf nie stillschweigend als Erfolg behandelt
        /// werden.
        /// </summary>
        private static void MoveToDeleteSafetyIfExists(string currentPath, string deleteSafetyPath)
        {
            if (File.Exists(currentPath))
            {
                File.Move(currentPath, deleteSafetyPath, overwrite: true);
            }
        }

        private static void UndoMoveToDeleteSafety(string currentPath, string deleteSafetyPath)
        {
            if (File.Exists(deleteSafetyPath))
            {
                File.Move(deleteSafetyPath, currentPath, overwrite: true);
            }
        }

        // --- Sicherung des Ursprungszustands ---

        private sealed class BackupState
        {
            public bool FolderHadReadOnly;
            public bool DesktopIniHasBackup;
            public int DesktopIniAttributes;
            public bool IconHasBackup;
            public int IconAttributes;
        }

        /// <summary>
        /// Sichert <paramref name="sourcePath"/> nach <paramref name="finalBackupPath"/>,
        /// falls die Datei existiert. Die Originaldatei wird dabei zu keinem Zeitpunkt
        /// verändert (keine Attribute werden vor dem Kopieren entfernt) - schlägt die
        /// Sicherung fehl, ist von der Originaldatei nichts betroffen.
        /// </summary>
        private static bool BackupIfExists(string sourcePath, string finalBackupPath, out int originalAttributes)
        {
            if (!File.Exists(sourcePath))
            {
                originalAttributes = 0;
                return false;
            }

            originalAttributes = (int)File.GetAttributes(sourcePath);

            string tempBackup = UniqueTempPath(Path.GetDirectoryName(finalBackupPath) ?? ".", ".bak.tmp");
            try
            {
                File.Copy(sourcePath, tempBackup, overwrite: false);
                File.SetAttributes(tempBackup, FileAttributes.Normal);

                if (File.Exists(finalBackupPath))
                {
                    ClearAttributesIfExists(finalBackupPath);
                    File.Delete(finalBackupPath);
                }

                // Letzter Schritt ist ein Umbenennen (kein Copy) - das ist auf demselben
                // Volume praktisch unteilbar.
                File.Move(tempBackup, finalBackupPath);
                File.SetAttributes(finalBackupPath, FileAttributes.Hidden | FileAttributes.System);
                return true;
            }
            finally
            {
                TryDelete(tempBackup);
            }
        }

        private static void WriteMarker(string markerPath, BackupState state)
        {
            string content =
                "FormatVersion=" + ExpectedFormatVersion + Environment.NewLine +
                "FolderHadReadOnly=" + (state.FolderHadReadOnly ? "1" : "0") + Environment.NewLine +
                "DesktopIni.HasBackup=" + (state.DesktopIniHasBackup ? "1" : "0") + Environment.NewLine +
                "DesktopIni.Attributes=" + state.DesktopIniAttributes + Environment.NewLine +
                "Icon.HasBackup=" + (state.IconHasBackup ? "1" : "0") + Environment.NewLine +
                "Icon.Attributes=" + state.IconAttributes + Environment.NewLine;

            string tempMarker = UniqueTempPath(Path.GetDirectoryName(markerPath) ?? ".", ".marker.tmp");
            File.WriteAllText(tempMarker, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            try
            {
                if (File.Exists(markerPath))
                {
                    ClearAttributesIfExists(markerPath);
                    File.Delete(markerPath);
                }
                File.Move(tempMarker, markerPath);
                File.SetAttributes(markerPath, FileAttributes.Hidden | FileAttributes.System);
            }
            finally
            {
                TryDelete(tempMarker);
            }
        }

        // Nur Attribut-Bits akzeptieren, die dieses Programm selbst jemals sichern würde.
        // Alles andere (z.B. Verzeichnis- oder Reparse-Point-Bits) deutet auf eine
        // manipulierte, fremde oder beschädigte Statusdatei hin.
        private const FileAttributes AllowedAttributeMask =
            FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System |
            FileAttributes.Archive | FileAttributes.Normal | FileAttributes.NotContentIndexed |
            FileAttributes.Temporary | FileAttributes.Offline | FileAttributes.Compressed |
            FileAttributes.Encrypted | FileAttributes.SparseFile;

        /// <summary>
        /// Liest die Statusdatei robust ein und validiert sie STRENG. Fehlt sie, ist sie
        /// unlesbar, hat eine unbekannte Formatversion, enthält andere boolesche Werte als
        /// exakt "0"/"1" oder unplausible Attribut-Bits, wird bewusst eine Ausnahme
        /// geworfen statt zu raten - damit im Zweifel nichts automatisch gelöscht oder
        /// überschrieben wird (auch nicht bei einer absichtlich präparierten/fremden
        /// Statusdatei).
        /// </summary>
        private static BackupState ReadMarker(string markerPath)
        {
            string[] lines;
            try
            {
                lines = File.ReadAllLines(markerPath);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException(
                    $"Statusdatei konnte nicht gelesen werden: {markerPath}", ex);
            }

            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string line in lines)
            {
                int separatorIndex = line.IndexOf('=');
                if (separatorIndex > 0)
                {
                    values[line[..separatorIndex]] = line[(separatorIndex + 1)..];
                }
            }

            if (!values.TryGetValue("FormatVersion", out string? formatVersion)
                || formatVersion != ExpectedFormatVersion)
            {
                throw new InvalidDataException(
                    $"Statusdatei hat eine unbekannte oder fehlende Formatversion: {markerPath}. Aus " +
                    "Sicherheitsgründen wird nichts automatisch geändert oder gelöscht.");
            }

            try
            {
                return new BackupState
                {
                    FolderHadReadOnly = ParseStrictBool(values, "FolderHadReadOnly"),
                    DesktopIniHasBackup = ParseStrictBool(values, "DesktopIni.HasBackup"),
                    DesktopIniAttributes = ParseAttributes(values, "DesktopIni.Attributes"),
                    IconHasBackup = ParseStrictBool(values, "Icon.HasBackup"),
                    IconAttributes = ParseAttributes(values, "Icon.Attributes"),
                };
            }
            catch (Exception ex) when (ex is KeyNotFoundException or FormatException)
            {
                throw new InvalidDataException(
                    $"Statusdatei ist unvollständig oder beschädigt: {markerPath}. Aus " +
                    "Sicherheitsgründen wird nichts automatisch gelöscht oder wiederhergestellt.", ex);
            }
        }

        private static bool ParseStrictBool(Dictionary<string, string> values, string key)
        {
            if (!values.TryGetValue(key, out string? raw))
                throw new KeyNotFoundException(key);

            return raw switch
            {
                "1" => true,
                "0" => false,
                _ => throw new FormatException($"Ungültiger boolescher Wert für '{key}': '{raw}'"),
            };
        }

        private static int ParseAttributes(Dictionary<string, string> values, string key)
        {
            if (!values.TryGetValue(key, out string? raw))
                throw new KeyNotFoundException(key);
            if (!int.TryParse(raw, out int parsed) || parsed < 0)
                throw new FormatException($"Ungültiger Attributwert für '{key}': '{raw}'");

            var attributes = (FileAttributes)parsed;
            if ((attributes & ~AllowedAttributeMask) != 0)
                throw new FormatException($"Unzulässige Attribut-Bits für '{key}': '{raw}'");

            return parsed;
        }

        private static string UniqueTempPath(string folderPath, string suffix)
        {
            return Path.Combine(folderPath, "." + Guid.NewGuid().ToString("N") + suffix);
        }

        private static void ClearAttributesIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                    File.Delete(path);
                }
            }
            catch
            {
                // Aufräumen ist best-effort - ein fehlgeschlagenes Löschen einer
                // temporären Datei soll die eigentliche Operation nicht zum Absturz bringen.
            }
        }

        // --- Shell-Aktualisierung (nur Benachrichtigung, kein Neustart des Explorers) ---

        private const uint SHCNE_ASSOCCHANGED = 0x08000000;
        private const uint SHCNE_UPDATEITEM = 0x00002000;
        private const uint SHCNF_IDLIST = 0x0000;
        private const uint SHCNF_PATHW = 0x0005;

        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        private static void RefreshShellIcon(string folderPath)
        {
            try
            {
                IntPtr pathPtr = Marshal.StringToHGlobalUni(folderPath);
                try
                {
                    SHChangeNotify(SHCNE_UPDATEITEM, SHCNF_PATHW, pathPtr, IntPtr.Zero);
                    SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
                }
                finally
                {
                    Marshal.FreeHGlobal(pathPtr);
                }
            }
            catch
            {
                // Reine UI-Aktualisierung (Explorer-Icon-Cache) - ein Fehlschlag hier darf
                // eine ansonsten erfolgreiche Dateioperation nicht als fehlgeschlagen
                // erscheinen lassen.
            }
        }
    }
}
