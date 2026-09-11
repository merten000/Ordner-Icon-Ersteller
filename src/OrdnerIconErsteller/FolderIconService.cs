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
    /// Sicherheitsprinzipien (nach der 2. ChatGPT-Prüfung von v2.0.1):
    /// - Ein bereits vorhandener, fremder Zustand wird nur gesichert, nie durch
    ///   bloßes Ändern von Attributen an der Originaldatei "vorbereitet". Die
    ///   Originaldatei bleibt unangetastet, bis eine vollständige Sicherung an
    ///   ihrem endgültigen Platz liegt.
    /// - Jeder Dateiaustausch im Zielordner geschieht über <see cref="File.Move"/>
    ///   (Umbenennen), nicht über Copy-Overwrite - das ist auf demselben Volume
    ///   ein einziger, praktisch unteilbarer Schritt statt eines kopierten
    ///   Bytestroms, der mittendrin abbrechen kann.
    /// - Schlägt irgendein Schritt fehl, werden alle in diesem Aufruf bereits
    ///   vorgenommenen Änderungen in umgekehrter Reihenfolge zurückgerollt.
    /// - "Icon entfernen" verändert nur Ordner, für die eine von diesem Programm
    ///   angelegte Statusdatei existiert. Ohne diese Statusdatei wird nichts
    ///   gelöscht oder überschrieben.
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

        /// <summary>
        /// Setzt das Ordner-Icon. Vorhandene desktop.ini/Icon-Datei/Ordnerattribute
        /// werden vor der ersten Änderung gesichert (siehe <see cref="RemoveFolderIcon"/>).
        /// Die eigentlichen Dateien werden über temporäre Dateien im Zielordner
        /// vorbereitet und dann per atomarem Umbenennen eingesetzt; schlägt dabei
        /// etwas fehl, wird der komplette Aufruf zurückgerollt.
        /// </summary>
        public static void SetFolderIcon(string folderPath, string sourceImagePath)
        {
            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException($"Ordner nicht gefunden: {folderPath}");
            if (!File.Exists(sourceImagePath))
                throw new FileNotFoundException($"Bilddatei nicht gefunden: {sourceImagePath}");

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

            string tempIco = UniqueTempPath(folderPath, ".ico.tmp");
            string tempIni = UniqueTempPath(folderPath, ".ini.tmp");
            string icoSafety = UniqueTempPath(folderPath, ".ico.safety");
            string iniSafety = UniqueTempPath(folderPath, ".ini.safety");

            bool icoSwapped = false;
            bool iniSwapped = false;
            try
            {
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

                try
                {
                    SwapIntoPlace(icoPath, tempIco, icoSafety);
                    icoSwapped = true;

                    SwapIntoPlace(desktopIniPath, tempIni, iniSafety);
                    iniSwapped = true;
                }
                catch
                {
                    // Mindestens einer der beiden Tauschvorgänge ist fehlgeschlagen (z.B.
                    // weil am Zielpfad bereits ein gleichnamiger Ordner liegt). Alle in
                    // diesem Aufruf bereits vorgenommenen Änderungen in umgekehrter
                    // Reihenfolge zurückrollen, damit kein halbfertiger Zustand zurückbleibt.
                    if (iniSwapped)
                        UndoSwap(desktopIniPath, iniSafety);
                    if (icoSwapped)
                        UndoSwap(icoPath, icoSafety);

                    if (createdBackupsThisCall)
                    {
                        ClearAttributesIfExists(markerPath);
                        TryDelete(markerPath);
                        TryDelete(iniBackupPath);
                        TryDelete(icoBackupPath);
                    }
                    throw;
                }

                File.SetAttributes(icoPath, FileAttributes.Hidden | FileAttributes.System);
                File.SetAttributes(desktopIniPath, FileAttributes.Hidden | FileAttributes.System);

                // Windows liest desktop.ini nur, wenn der Ordner als "angepasst" markiert ist
                // (klassisch über das ReadOnly-Bit auf Ordnern - hat NICHTS mit Schreibschutz zu tun).
                var dirInfo = new DirectoryInfo(folderPath);
                dirInfo.Attributes |= FileAttributes.ReadOnly;

                RefreshShellIcon(folderPath);
            }
            finally
            {
                TryDelete(tempIco);
                TryDelete(tempIni);
                TryDelete(icoSafety);
                TryDelete(iniSafety);
            }
        }

        /// <summary>
        /// Entfernt das von diesem Programm gesetzte Ordner-Icon wieder. Das geschieht
        /// ausschließlich anhand der Statusdatei, die beim ersten "Icon setzen" für
        /// diesen Ordner angelegt wurde - ohne diese Statusdatei wird nichts verändert,
        /// damit ein nie von diesem Programm bearbeiteter Ordner niemals versehentlich
        /// fremde Dateien verliert. War vor dem ersten "Icon setzen" bereits eine eigene
        /// desktop.ini/Icon-Datei vorhanden, wird genau diese inklusive der ursprünglichen
        /// Datei-Attribute wiederhergestellt statt nur gelöscht.
        /// </summary>
        public static void RemoveFolderIcon(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException($"Ordner nicht gefunden: {folderPath}");

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

            BackupState state = ReadMarker(markerPath);

            RestoreFile(desktopIniPath, iniBackupPath, state.DesktopIniHasBackup, state.DesktopIniAttributes);
            RestoreFile(icoPath, icoBackupPath, state.IconHasBackup, state.IconAttributes);

            var dirInfo = new DirectoryInfo(folderPath);
            if (state.FolderHadReadOnly)
                dirInfo.Attributes |= FileAttributes.ReadOnly;
            else
                dirInfo.Attributes &= ~FileAttributes.ReadOnly;

            ClearAttributesIfExists(markerPath);
            TryDelete(markerPath);

            RefreshShellIcon(folderPath);
        }

        // --- Austausch einzelner Dateien: atomar + zurückrollbar ---

        /// <summary>
        /// Ersetzt <paramref name="targetPath"/> durch <paramref name="newContentPath"/>.
        /// Eine vorhandene Zieldatei wird zuerst per <see cref="File.Move"/> (nicht
        /// kopiert) nach <paramref name="safetyPath"/> verschoben, damit sie bei einem
        /// Fehlschlag über <see cref="UndoSwap"/> unverändert (gleiche Bytes, gleiche
        /// Attribute) zurückgeholt werden kann.
        /// </summary>
        private static void SwapIntoPlace(string targetPath, string newContentPath, string safetyPath)
        {
            if (File.Exists(targetPath))
            {
                File.Move(targetPath, safetyPath, overwrite: true);
            }

            File.Move(newContentPath, targetPath, overwrite: true);
        }

        private static void UndoSwap(string targetPath, string safetyPath)
        {
            TryDelete(targetPath);
            if (File.Exists(safetyPath))
            {
                File.Move(safetyPath, targetPath, overwrite: true);
            }
        }

        private static void RestoreFile(string currentPath, string backupPath, bool hasBackup, int originalAttributes)
        {
            if (hasBackup)
            {
                if (!File.Exists(backupPath))
                {
                    throw new InvalidDataException(
                        $"Die erwartete Sicherungsdatei fehlt: {backupPath}. Aus Sicherheitsgründen " +
                        "wird nichts gelöscht - bitte den Ordnerinhalt manuell prüfen.");
                }

                ClearAttributesIfExists(backupPath);
                // Atomarer Tausch statt Copy+Overwrite: die Sicherung ersetzt die aktuelle
                // (von uns gesetzte) Datei in einem einzigen Schritt.
                File.Move(backupPath, currentPath, overwrite: true);
                File.SetAttributes(currentPath, (FileAttributes)originalAttributes);
            }
            else
            {
                // Der Ordner besaß vor dem ersten "Icon setzen" keine solche Datei -
                // also gehört sie uns, und wir dürfen sie entfernen.
                TryDelete(currentPath);
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
                "FormatVersion=2" + Environment.NewLine +
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

        /// <summary>
        /// Liest die Statusdatei robust ein. Fehlt sie, ist sie unlesbar oder
        /// unvollständig/beschädigt, wird bewusst eine Ausnahme geworfen statt zu
        /// raten - damit im Zweifel nichts automatisch gelöscht oder überschrieben wird.
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

            try
            {
                return new BackupState
                {
                    FolderHadReadOnly = values["FolderHadReadOnly"] == "1",
                    DesktopIniHasBackup = values["DesktopIni.HasBackup"] == "1",
                    DesktopIniAttributes = int.Parse(values["DesktopIni.Attributes"]),
                    IconHasBackup = values["Icon.HasBackup"] == "1",
                    IconAttributes = int.Parse(values["Icon.Attributes"]),
                };
            }
            catch (Exception ex) when (ex is KeyNotFoundException or FormatException)
            {
                throw new InvalidDataException(
                    $"Statusdatei ist unvollständig oder beschädigt: {markerPath}. Aus " +
                    "Sicherheitsgründen wird nichts automatisch gelöscht oder wiederhergestellt.", ex);
            }
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
    }
}
