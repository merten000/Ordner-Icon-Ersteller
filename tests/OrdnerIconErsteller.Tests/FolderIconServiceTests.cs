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
        public void SetUndRemove_MitVorhandenerDesktopIni_StelltOriginalWiederHer()
        {
            string targetFolder = Directory.CreateTempSubdirectory("OrdnerIconErsteller_Ziel_").FullName;
            string imageDir = Directory.CreateTempSubdirectory("OrdnerIconErsteller_Bild_").FullName;
            try
            {
                // Ordner hat schon eine eigene, fremde Anpassung (z.B. von einem
                // anderen Tool) - genau der in der Prüfung gemeldete Fall.
                string originalIniContent = "[.ShellClassInfo]" + Environment.NewLine +
                                             "IconResource=EinAnderesIcon.ico,0" + Environment.NewLine;
                string desktopIniPath = Path.Combine(targetFolder, "desktop.ini");
                File.WriteAllText(desktopIniPath, originalIniContent, Encoding.Unicode);
                File.SetAttributes(desktopIniPath, FileAttributes.Hidden | FileAttributes.System);

                string originalIcoPath = Path.Combine(targetFolder, "FolderIcon.ico");
                byte[] originalIcoBytes = { 0, 0, 1, 0, 1, 2, 3, 4, 5 };
                File.WriteAllBytes(originalIcoPath, originalIcoBytes);
                File.SetAttributes(originalIcoPath, FileAttributes.Hidden | FileAttributes.System);

                var dirInfo = new DirectoryInfo(targetFolder);
                dirInfo.Attributes |= FileAttributes.ReadOnly;

                string newImage = CreateTestImage(imageDir, "neues-bild.png");

                FolderIconService.SetFolderIcon(targetFolder, newImage);

                // Unser neues Icon soll jetzt aktiv sein und sich vom Original unterscheiden.
                byte[] afterSetBytes = File.ReadAllBytes(originalIcoPath);
                Assert.NotEqual(originalIcoBytes, afterSetBytes);

                FolderIconService.RemoveFolderIcon(targetFolder);

                // Nach dem Entfernen muss exakt der ursprüngliche Zustand zurück sein.
                Assert.Equal(originalIniContent, File.ReadAllText(desktopIniPath, Encoding.Unicode));
                Assert.Equal(originalIcoBytes, File.ReadAllBytes(originalIcoPath));
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
