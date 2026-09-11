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

        public static void SetFolderIcon(string folderPath, string sourceImagePath)
        {
            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException($"Ordner nicht gefunden: {folderPath}");
            if (!File.Exists(sourceImagePath))
                throw new FileNotFoundException($"Bilddatei nicht gefunden: {sourceImagePath}");

            string icoPath = Path.Combine(folderPath, IconFileName);
            string desktopIniPath = Path.Combine(folderPath, DesktopIniName);

            // Falls vorhanden: Attribute entfernen, damit die Dateien überschrieben werden können
            ClearAttributesIfExists(icoPath);
            ClearAttributesIfExists(desktopIniPath);

            IconWriter.CreateIcoFromImage(sourceImagePath, icoPath);

            string content =
                "[.ShellClassInfo]" + Environment.NewLine +
                "IconResource=" + IconFileName + ",0" + Environment.NewLine +
                "IconFile=" + IconFileName + Environment.NewLine +
                "IconIndex=0" + Environment.NewLine;

            File.WriteAllText(desktopIniPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            File.SetAttributes(icoPath, FileAttributes.Hidden | FileAttributes.System);
            File.SetAttributes(desktopIniPath, FileAttributes.Hidden | FileAttributes.System);

            // Windows liest desktop.ini nur, wenn der Ordner als "angepasst" markiert ist
            // (klassisch über das ReadOnly-Bit auf Ordnern - hat NICHTS mit Schreibschutz zu tun).
            var dirInfo = new DirectoryInfo(folderPath);
            dirInfo.Attributes |= FileAttributes.ReadOnly;

            RefreshShellIcon(folderPath);
        }

        public static void RemoveFolderIcon(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException($"Ordner nicht gefunden: {folderPath}");

            string icoPath = Path.Combine(folderPath, IconFileName);
            string desktopIniPath = Path.Combine(folderPath, DesktopIniName);

            ClearAttributesIfExists(icoPath);
            ClearAttributesIfExists(desktopIniPath);

            if (File.Exists(icoPath)) File.Delete(icoPath);
            if (File.Exists(desktopIniPath)) File.Delete(desktopIniPath);

            var dirInfo = new DirectoryInfo(folderPath);
            dirInfo.Attributes &= ~FileAttributes.ReadOnly;

            RefreshShellIcon(folderPath);
        }

        private static void ClearAttributesIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
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
