using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using Xunit;

namespace OrdnerIconErsteller.Tests
{
    public class FolderIconServiceTests
    {
        private static string CreateTestImage(string directory, string fileName)
        {
            string path = Path.Combine(directory, fileName);
            using var bmp = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.Tomato);
            bmp.Save(path, ImageFormat.Png);
            return path;
        }

        [Fact]
        public void SetUndRemove_OhneVorherigeAnpassung_RaeumtWiederKomplettAuf()
        {
            string targetFolder = Directory.CreateTempSubdirectory("OrdnerIconErsteller_Ziel_").FullName;
            string imageDir = Directory.CreateTempSubdirectory("OrdnerIconErsteller_Bild_").FullName;
            try
            {
                string image = CreateTestImage(imageDir, "bild.png");

                FolderIconService.SetFolderIcon(targetFolder, image);

                Assert.True(File.Exists(Path.Combine(targetFolder, "desktop.ini")));
                Assert.True(File.Exists(Path.Combine(targetFolder, "FolderIcon.ico")));
                Assert.True((new DirectoryInfo(targetFolder).Attributes & FileAttributes.ReadOnly) != 0);

                FolderIconService.RemoveFolderIcon(targetFolder);

                Assert.False(File.Exists(Path.Combine(targetFolder, "desktop.ini")));
                Assert.False(File.Exists(Path.Combine(targetFolder, "FolderIcon.ico")));
                Assert.False(File.Exists(Path.Combine(targetFolder, ".ordnericonersteller-state")));
                Assert.True((new DirectoryInfo(targetFolder).Attributes & FileAttributes.ReadOnly) == 0);
            }
            finally
            {
                TryDeleteDirectory(targetFolder);
                TryDeleteDirectory(imageDir);
            }
        }

        [Fact]
        public void SetUndRemove_MitVorhandenerDesktopIni_StelltOriginalUndAttributeExaktWieder()
        {
            string targetFolder = Directory.CreateTempSubdirectory("OrdnerIconErsteller_Ziel_").FullName;
            string imageDir = Directory.CreateTempSubdirectory("OrdnerIconErsteller_Bild_").FullName;
            try
            {
                // Ordner hat schon eine eigene, fremde Anpassung (z.B. von einem anderen
                // Tool) mit UNGEWÖHNLICHEN Attributen - genau der von ChatGPT gemeldete
                // Fall, bei dem die alte Implementierung pauschal Hidden|System gesetzt
                // hat, statt die echten Original-Attribute zu sichern.
                string originalIniContent = "[.ShellClassInfo]" + Environment.NewLine +
                                             "IconResource=EinAnderesIcon.ico,0" + Environment.NewLine;
                string desktopIniPath = Path.Combine(targetFolder, "desktop.ini");
                File.WriteAllText(desktopIniPath, originalIniContent, Encoding.Unicode);
                File.SetAttributes(desktopIniPath, FileAttributes.Archive);

                string originalIcoPath = Path.Combine(targetFolder, "FolderIcon.ico");
                byte[] originalIcoBytes = { 0, 0, 1, 0, 1, 2, 3, 4, 5 };
                File.WriteAllBytes(originalIcoPath, originalIcoBytes);
                File.SetAttributes(originalIcoPath, FileAttributes.ReadOnly);

                var dirInfo = new DirectoryInfo(targetFolder);
                dirInfo.Attributes |= FileAttributes.ReadOnly;

                string newImage = CreateTestImage(imageDir, "neues-bild.png");

                FolderIconService.SetFolderIcon(targetFolder, newImage);

                // Unser neues Icon soll jetzt aktiv sein und sich vom Original unterscheiden.
                byte[] afterSetBytes = File.ReadAllBytes(originalIcoPath);
                Assert.NotEqual(originalIcoBytes, afterSetBytes);

                FolderIconService.RemoveFolderIcon(targetFolder);

                // Nach dem Entfernen muss exakt der ursprüngliche Zustand zurück sein -
                // Inhalt UND die tatsächlichen ursprünglichen Attribute (nicht pauschal
                // Hidden|System).
                Assert.Equal(originalIniContent, File.ReadAllText(desktopIniPath, Encoding.Unicode));
                Assert.Equal(FileAttributes.Archive, File.GetAttributes(desktopIniPath));

                Assert.Equal(originalIcoBytes, File.ReadAllBytes(originalIcoPath));
                Assert.Equal(FileAttributes.ReadOnly, File.GetAttributes(originalIcoPath));

                Assert.True((new DirectoryInfo(targetFolder).Attributes & FileAttributes.ReadOnly) != 0);
                Assert.False(File.Exists(Path.Combine(targetFolder, ".ordnericonersteller-state")));
                Assert.False(File.Exists(desktopIniPath + ".before-ordnericonersteller.bak"));
            }
            finally
            {
                TryDeleteDirectory(targetFolder);
                TryDeleteDirectory(imageDir);
            }
        }

        [Fact]
        public void Remove_OhneStatusdatei_WirftUndLaesstFremdeDateienUnangetastet()
        {
            // Der in der 2. ChatGPT-Prüfung gemeldete Fehler: ein Ordner, den das
            // Programm nie bearbeitet hat, darf beim "Icon entfernen" nicht angefasst
            // werden - auch wenn zufällig eine fremde desktop.ini/Icon-Datei dort liegt.
            string targetFolder = Directory.CreateTempSubdirectory("OrdnerIconErsteller_Ziel_").FullName;
            try
            {
                string desktopIniPath = Path.Combine(targetFolder, "desktop.ini");
                byte[] fremdeIniBytes = Encoding.Unicode.GetBytes("[.ShellClassInfo]\r\nIconResource=Fremd.ico,0\r\n");
                File.WriteAllBytes(desktopIniPath, fremdeIniBytes);

                string icoPath = Path.Combine(targetFolder, "FolderIcon.ico");
                byte[] fremdeIcoBytes = { 9, 9, 9, 9 };
                File.WriteAllBytes(icoPath, fremdeIcoBytes);

                Assert.Throws<InvalidOperationException>(() => FolderIconService.RemoveFolderIcon(targetFolder));

                Assert.Equal(fremdeIniBytes, File.ReadAllBytes(desktopIniPath));
                Assert.Equal(fremdeIcoBytes, File.ReadAllBytes(icoPath));
            }
            finally
            {
                TryDeleteDirectory(targetFolder);
            }
        }

        [Fact]
        public void Set_SchlaegtBeimZweitenTauschFehl_RolltAllesZurueck()
        {
            // Fehlerpfadtest aus der 2. ChatGPT-Prüfung: das Schreiben von desktop.ini
            // wird durch ein gleichnamiges Verzeichnis verhindert, nachdem das Icon
            // bereits eingesetzt wurde. Erwartet wird ein vollständiges Rollback: kein
            // Icon, keine Statusdatei - statt eines halbfertigen Zustands.
            string targetFolder = Directory.CreateTempSubdirectory("OrdnerIconErsteller_Ziel_").FullName;
            string imageDir = Directory.CreateTempSubdirectory("OrdnerIconErsteller_Bild_").FullName;
            try
            {
                string desktopIniPath = Path.Combine(targetFolder, "desktop.ini");
                Directory.CreateDirectory(desktopIniPath); // blockiert das Schreiben der Datei

                string image = CreateTestImage(imageDir, "bild.png");

                // Windows meldet das Verschieben einer Datei auf einen Pfad, an dem
                // bereits ein Verzeichnis liegt, als UnauthorizedAccessException (nicht
                // als IOException) - der Fehlertyp ist hier zweitrangig, entscheidend ist
                // das vollständige Rollback unten.
                Assert.Throws<UnauthorizedAccessException>(() => FolderIconService.SetFolderIcon(targetFolder, image));

                Assert.False(File.Exists(Path.Combine(targetFolder, "FolderIcon.ico")));
                Assert.False(File.Exists(Path.Combine(targetFolder, ".ordnericonersteller-state")));
                Assert.True(Directory.Exists(desktopIniPath), "Das blockierende Verzeichnis darf nicht angetastet werden.");
            }
            finally
            {
                TryDeleteDirectory(targetFolder);
                TryDeleteDirectory(imageDir);
            }
        }

        [Fact]
        public void Set_SchlaegtBeiSicherungFehl_LaesstOriginaleUnveraendert()
        {
            // Fehlerpfadtest aus der 2. ChatGPT-Prüfung: der Sicherungs-Zielpfad für
            // die Icon-Datei ist blockiert. Die Original-Attribute (weder ini noch
            // ico) dürfen dadurch nicht verändert werden, und es darf keine
            // Statusdatei zurückbleiben.
            string targetFolder = Directory.CreateTempSubdirectory("OrdnerIconErsteller_Ziel_").FullName;
            string imageDir = Directory.CreateTempSubdirectory("OrdnerIconErsteller_Bild_").FullName;
            try
            {
                string desktopIniPath = Path.Combine(targetFolder, "desktop.ini");
                File.WriteAllText(desktopIniPath, "[.ShellClassInfo]\r\nIconResource=Fremd.ico,0\r\n", Encoding.Unicode);
                File.SetAttributes(desktopIniPath, FileAttributes.Archive);

                string icoPath = Path.Combine(targetFolder, "FolderIcon.ico");
                File.WriteAllBytes(icoPath, new byte[] { 1, 2, 3, 4 });
                File.SetAttributes(icoPath, FileAttributes.ReadOnly);

                // Der Sicherungs-Zielpfad für das Icon ist durch ein Verzeichnis blockiert.
                string icoBackupPath = icoPath + ".before-ordnericonersteller.bak";
                Directory.CreateDirectory(icoBackupPath);

                string image = CreateTestImage(imageDir, "bild.png");

                Assert.Throws<IOException>(() => FolderIconService.SetFolderIcon(targetFolder, image));

                Assert.Equal(FileAttributes.Archive, File.GetAttributes(desktopIniPath));
                Assert.Equal(FileAttributes.ReadOnly, File.GetAttributes(icoPath));
                Assert.False(File.Exists(Path.Combine(targetFolder, ".ordnericonersteller-state")));
                Assert.False(File.Exists(desktopIniPath + ".before-ordnericonersteller.bak"));
            }
            finally
            {
                TryDeleteDirectory(targetFolder);
                TryDeleteDirectory(imageDir);
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                foreach (var file in new DirectoryInfo(path).GetFiles())
                {
                    file.Attributes = FileAttributes.Normal;
                }
                Directory.Delete(path, recursive: true);
            }
            catch
            {
                // Aufräumen im Test ist best-effort.
            }
        }
    }
}
