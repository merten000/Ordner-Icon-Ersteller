using System;
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
    /// </summary>
    internal static class FolderIconService
    {
        private const string IconFileName = "FolderIcon.ico";
        private const string DesktopIniName = "desktop.ini";
        private const string BackupSuffix = ".before-ordnericonersteller.bak";
        private const string MarkerFileName = ".ordnericonersteller-state";

        /// <summary>
        /// Setzt das Ordner-Icon. Vorhandene desktop.ini/Icon-Datei/Ordnerattribute
        /// werden vor der ersten Änderung gesichert (siehe <see cref="RemoveFolderIcon"/>),
        /// und die eigentlichen Dateien werden erst über temporäre Dateien geschrieben
        /// und dann per Copy-Overwrite eingesetzt, damit bei einem Fehler mittendrin
        /// kein halbfertiger Zustand im Zielordner zurückbleibt.
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

            // Nur beim allerersten Aufruf für diesen Ordner den Ursprungszustand sichern -
            // ein zweites "Icon setzen" darf nicht die (schon einmal gesicherte) Originaldatei
            // mit unserer eigenen, zwischenzeitlich gesetzten Version überschreiben.
            if (!File.Exists(markerPath))
            {
                bool hadReadOnly = (new DirectoryInfo(folderPath).Attributes & FileAttributes.ReadOnly) != 0;
                BackupIfExists(desktopIniPath, iniBackupPath);
                BackupIfExists(icoPath, icoBackupPath);
                WriteMarker(markerPath, hadReadOnly);
            }

            string tempIco = icoPath + ".tmp";
            string tempIni = desktopIniPath + ".tmp";
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

                ClearAttributesIfExists(icoPath);
                ClearAttributesIfExists(desktopIniPath);

                File.Copy(tempIco, icoPath, overwrite: true);
                File.Copy(tempIni, desktopIniPath, overwrite: true);

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
            }
        }

        /// <summary>
        /// Entfernt das von diesem Programm gesetzte Ordner-Icon wieder. War vor dem
        /// ersten "Icon setzen" bereits eine eigene desktop.ini/Icon-Datei vorhanden,
        /// wird genau die wiederhergestellt statt nur gelöscht.
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

            bool markerExists = File.Exists(markerPath);
            bool hadReadOnlyBeforeUs = markerExists && ReadMarkerHadReadOnly(markerPath);

            RestoreOrDelete(icoPath, icoBackupPath);
            RestoreOrDelete(desktopIniPath, iniBackupPath);

            if (markerExists)
            {
                ClearAttributesIfExists(markerPath);
                TryDelete(markerPath);
            }

            var dirInfo = new DirectoryInfo(folderPath);
            if (!hadReadOnlyBeforeUs)
            {
                dirInfo.Attributes &= ~FileAttributes.ReadOnly;
            }
            // War der Ordner schon vor uns "angepasst" (ReadOnly-Bit gesetzt), lassen wir
            // das Attribut unangetastet, statt fremde Anpassungen zu zerstören.

            RefreshShellIcon(folderPath);
        }

        private static void RestoreOrDelete(string currentPath, string backupPath)
        {
            ClearAttributesIfExists(currentPath);
            if (File.Exists(backupPath))
            {
                ClearAttributesIfExists(backupPath);
                File.Copy(backupPath, currentPath, overwrite: true);
                File.SetAttributes(currentPath, FileAttributes.Hidden | FileAttributes.System);
                TryDelete(backupPath);
            }
            else if (File.Exists(currentPath))
            {
                File.Delete(currentPath);
            }
        }

        private static void BackupIfExists(string path, string backupPath)
        {
            if (File.Exists(path))
            {
                ClearAttributesIfExists(path);
                File.Copy(path, backupPath, overwrite: true);
                File.SetAttributes(backupPath, FileAttributes.Hidden | FileAttributes.System);
            }
        }

        private static void WriteMarker(string markerPath, bool hadReadOnly)
        {
            File.WriteAllText(markerPath, hadReadOnly ? "HadReadOnly=1" : "HadReadOnly=0", Encoding.ASCII);
            File.SetAttributes(markerPath, FileAttributes.Hidden | FileAttributes.System);
        }

        private static bool ReadMarkerHadReadOnly(string markerPath)
        {
            try
            {
                return File.ReadAllText(markerPath).Contains("HadReadOnly=1");
            }
            catch
            {
                return false;
            }
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
