using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
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

        [Fact]
        public void Remove_BeiGesperrterDesktopIni_WirftUndLaesstAllesUnveraendertUndBleibtWiederholbar()
        {
            // Regressionstest aus der 3. ChatGPT-Prüfung (Fehler #1: "stiller"
            // Löschfehler galt bisher als Erfolg). Ist eine Datei beim Entfernen
            // gerade exklusiv gesperrt (z. B. durch einen anderen Prozess), muss der
            // Fehler sichtbar werden, ALLES unverändert bleiben und der Vorgang danach
            // erneut (erfolgreich) versucht werden können.
            string targetFolder = Directory.CreateTempSubdirectory("OrdnerIconErsteller_Ziel_").FullName;
            string imageDir = Directory.CreateTempSubdirectory("OrdnerIconErsteller_Bild_").FullName;
            try
            {
                string image = CreateTestImage(imageDir, "bild.png");
                FolderIconService.SetFolderIcon(targetFolder, image);

                string desktopIniPath = Path.Combine(targetFolder, "desktop.ini");
                string icoPath = Path.Combine(targetFolder, "FolderIcon.ico");
                string markerPath = Path.Combine(targetFolder, ".ordnericonersteller-state");

                using (new FileStream(desktopIniPath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    Assert.ThrowsAny<IOException>(() => FolderIconService.RemoveFolderIcon(targetFolder));
                }

                // Nach dem fehlgeschlagenen Versuch muss alles unverändert vorhanden sein -
                // insbesondere darf die Statusdatei nicht schon vorzeitig gelöscht worden sein.
                Assert.True(File.Exists(desktopIniPath));
                Assert.True(File.Exists(icoPath));
                Assert.True(File.Exists(markerPath));

                // Sobald die Sperre weg ist, muss der Vorgang normal (wieder) funktionieren.
                FolderIconService.RemoveFolderIcon(targetFolder);
                Assert.False(File.Exists(desktopIniPath));
                Assert.False(File.Exists(icoPath));
                Assert.False(File.Exists(markerPath));
            }
            finally
            {
                TryDeleteDirectory(targetFolder);
                TryDeleteDirectory(imageDir);
            }
        }

        [Fact]
        public void Remove_MitFehlenderZweiterSicherung_BrichtVorJederAenderungAb()
        {
            // Regressionstest aus der 3. ChatGPT-Prüfung (Fehler #2: die erste Datei
            // wurde bereits wiederhergestellt - und ihre Sicherung dabei "verbraucht" -
            // bevor festgestellt wurde, dass die zweite Sicherung fehlt). Jetzt muss VOR
            // jeder Änderung geprüft werden, dass ALLE laut Statusdatei nötigen
            // Sicherungsdateien existieren.
            string targetFolder = Directory.CreateTempSubdirectory("OrdnerIconErsteller_Ziel_").FullName;
            string imageDir = Directory.CreateTempSubdirectory("OrdnerIconErsteller_Bild_").FullName;
            try
            {
                // Ordner hat ursprünglich sowohl eine eigene desktop.ini als auch eine
                // eigene Icon-Datei - beide werden beim "Icon setzen" gesichert.
                string desktopIniPath = Path.Combine(targetFolder, "desktop.ini");
                File.WriteAllText(desktopIniPath, "[.ShellClassInfo]\r\nIconResource=Alt.ico,0\r\n", Encoding.Unicode);
                File.SetAttributes(desktopIniPath, FileAttributes.Archive);

                string icoPath = Path.Combine(targetFolder, "FolderIcon.ico");
                File.WriteAllBytes(icoPath, new byte[] { 1, 2, 3, 4 });
                File.SetAttributes(icoPath, FileAttributes.ReadOnly);

                string image = CreateTestImage(imageDir, "bild.png");
                FolderIconService.SetFolderIcon(targetFolder, image);

                // Die Icon-Sicherung geht nachträglich "verloren" (z. B. versehentlich
                // gelöscht) - die Statusdatei behauptet aber weiterhin, es gäbe eine.
                string icoBackupPath = icoPath + ".before-ordnericonersteller.bak";
                File.SetAttributes(icoBackupPath, FileAttributes.Normal);
                File.Delete(icoBackupPath);

                byte[] aktiveIniBytesVorher = File.ReadAllBytes(desktopIniPath);
                byte[] aktiveIcoBytesVorher = File.ReadAllBytes(icoPath);

                Assert.Throws<InvalidDataException>(() => FolderIconService.RemoveFolderIcon(targetFolder));

                // Weder die aktuell aktive desktop.ini/Icon-Datei noch die (an sich
                // vorhandene und intakte) ini-Sicherung dürfen angetastet worden sein.
                Assert.Equal(aktiveIniBytesVorher, File.ReadAllBytes(desktopIniPath));
                Assert.Equal(aktiveIcoBytesVorher, File.ReadAllBytes(icoPath));
                string iniBackupPath = desktopIniPath + ".before-ordnericonersteller.bak";
                Assert.True(File.Exists(iniBackupPath), "Die vorhandene ini-Sicherung darf nicht verbraucht werden.");
                Assert.True(File.Exists(Path.Combine(targetFolder, ".ordnericonersteller-state")),
                    "Die Statusdatei darf beim Abbruch vor jeder Änderung nicht gelöscht werden.");
            }
            finally
            {
                TryDeleteDirectory(targetFolder);
                TryDeleteDirectory(imageDir);
            }
        }

        [Fact]
        public void Remove_MitFalscherFormatversionInStatusdatei_WirftUndLaesstFremdeDateienUnangetastet()
        {
            // Regressionstest aus der 3. ChatGPT-Prüfung (Fehler #3: eine beschädigte
            // oder manipulierte Statusdatei durfte nie unbesehen akzeptiert werden).
            string targetFolder = Directory.CreateTempSubdirectory("OrdnerIconErsteller_Ziel_").FullName;
            try
            {
                string desktopIniPath = Path.Combine(targetFolder, "desktop.ini");
                byte[] fremdeIniBytes = Encoding.Unicode.GetBytes("[.ShellClassInfo]\r\nIconResource=Fremd.ico,0\r\n");
                File.WriteAllBytes(desktopIniPath, fremdeIniBytes);

                string icoPath = Path.Combine(targetFolder, "FolderIcon.ico");
                byte[] fremdeIcoBytes = { 9, 9, 9, 9 };
                File.WriteAllBytes(icoPath, fremdeIcoBytes);

                string markerPath = Path.Combine(targetFolder, ".ordnericonersteller-state");
                File.WriteAllText(markerPath,
                    "FormatVersion=1" + Environment.NewLine +
                    "FolderHadReadOnly=0" + Environment.NewLine +
                    "DesktopIni.HasBackup=0" + Environment.NewLine +
                    "DesktopIni.Attributes=0" + Environment.NewLine +
                    "Icon.HasBackup=0" + Environment.NewLine +
                    "Icon.Attributes=0" + Environment.NewLine,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                Assert.Throws<InvalidDataException>(() => FolderIconService.RemoveFolderIcon(targetFolder));

                Assert.Equal(fremdeIniBytes, File.ReadAllBytes(desktopIniPath));
                Assert.Equal(fremdeIcoBytes, File.ReadAllBytes(icoPath));
            }
            finally
            {
                TryDeleteDirectory(targetFolder);
            }
        }

        [Fact]
        public void Remove_MitUngueltigemBooleanInStatusdatei_WirftUndLaesstFremdeDateienUnangetastet()
        {
            // Weiterer Fall von Fehler #3: kein exaktes "0"/"1" darf stillschweigend
            // als "false" durchgehen.
            string targetFolder = Directory.CreateTempSubdirectory("OrdnerIconErsteller_Ziel_").FullName;
            try
            {
                string desktopIniPath = Path.Combine(targetFolder, "desktop.ini");
                byte[] fremdeIniBytes = Encoding.Unicode.GetBytes("[.ShellClassInfo]\r\nIconResource=Fremd.ico,0\r\n");
                File.WriteAllBytes(desktopIniPath, fremdeIniBytes);

                string markerPath = Path.Combine(targetFolder, ".ordnericonersteller-state");
                File.WriteAllText(markerPath,
                    "FormatVersion=2" + Environment.NewLine +
                    "FolderHadReadOnly=ja" + Environment.NewLine + // weder "0" noch "1" - ungültig
                    "DesktopIni.HasBackup=0" + Environment.NewLine +
                    "DesktopIni.Attributes=0" + Environment.NewLine +
                    "Icon.HasBackup=0" + Environment.NewLine +
                    "Icon.Attributes=0" + Environment.NewLine,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                Assert.Throws<InvalidDataException>(() => FolderIconService.RemoveFolderIcon(targetFolder));

                Assert.Equal(fremdeIniBytes, File.ReadAllBytes(desktopIniPath));
            }
            finally
            {
                TryDeleteDirectory(targetFolder);
            }
        }

        [Fact]
        public void Remove_MitUnzulaessigenAttributBitsInStatusdatei_WirftUndLaesstFremdeDateienUnangetastet()
        {
            // Weiterer Fall von Fehler #3: Attribut-Bits, die dieses Programm selbst nie
            // sichern würde (hier: ReparsePoint = 1024), dürfen nicht akzeptiert werden.
            string targetFolder = Directory.CreateTempSubdirectory("OrdnerIconErsteller_Ziel_").FullName;
            try
            {
                string desktopIniPath = Path.Combine(targetFolder, "desktop.ini");
                byte[] fremdeIniBytes = Encoding.Unicode.GetBytes("[.ShellClassInfo]\r\nIconResource=Fremd.ico,0\r\n");
                File.WriteAllBytes(desktopIniPath, fremdeIniBytes);

                string markerPath = Path.Combine(targetFolder, ".ordnericonersteller-state");
                File.WriteAllText(markerPath,
                    "FormatVersion=2" + Environment.NewLine +
                    "FolderHadReadOnly=0" + Environment.NewLine +
                    "DesktopIni.HasBackup=1" + Environment.NewLine +
                    "DesktopIni.Attributes=1024" + Environment.NewLine +
                    "Icon.HasBackup=0" + Environment.NewLine +
                    "Icon.Attributes=0" + Environment.NewLine,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                Assert.Throws<InvalidDataException>(() => FolderIconService.RemoveFolderIcon(targetFolder));

                Assert.Equal(fremdeIniBytes, File.ReadAllBytes(desktopIniPath));
            }
            finally
            {
                TryDeleteDirectory(targetFolder);
            }
        }

        [Fact]
        public void Set_SchlaegtBeiBilderzeugungNachErfolgreicherSicherungFehl_HinterlaesstKeineReste()
        {
            // Regressionstest aus der 3. ChatGPT-Prüfung (Fehler #4: unvollständige
            // Rollback-Abdeckung). Schlägt die Bilderzeugung fehl, NACHDEM Sicherung und
            // Statusdatei bereits erfolgreich angelegt wurden, dürfen weder Statusdatei
            // noch Sicherungsdateien noch Temp-/Safety-Dateien zurückbleiben, und die
            // Originaldatei muss unverändert bleiben.
            string targetFolder = Directory.CreateTempSubdirectory("OrdnerIconErsteller_Ziel_").FullName;
            string imageDir = Directory.CreateTempSubdirectory("OrdnerIconErsteller_Bild_").FullName;
            try
            {
                string desktopIniPath = Path.Combine(targetFolder, "desktop.ini");
                File.WriteAllText(desktopIniPath, "[.ShellClassInfo]\r\nIconResource=Alt.ico,0\r\n", Encoding.Unicode);
                File.SetAttributes(desktopIniPath, FileAttributes.Archive);

                // Eine "Bilddatei", die existiert, aber kein gültiges Bildformat ist -
                // die Bilderzeugung schlägt damit fehl, nachdem Sicherung + Statusdatei
                // bereits vollständig angelegt wurden.
                string invalidImage = Path.Combine(imageDir, "kaputt.png");
                File.WriteAllText(invalidImage, "Das ist kein Bild.");

                Assert.ThrowsAny<Exception>(() => FolderIconService.SetFolderIcon(targetFolder, invalidImage));

                Assert.False(File.Exists(Path.Combine(targetFolder, ".ordnericonersteller-state")));
                Assert.False(File.Exists(desktopIniPath + ".before-ordnericonersteller.bak"));
                Assert.Equal(FileAttributes.Archive, File.GetAttributes(desktopIniPath));

                // Keine übrig gebliebenen Temp-/Safety-/Lock-Dateien im Zielordner.
                var übrigeDateien = new DirectoryInfo(targetFolder).GetFiles()
                    .Where(f => f.Name != "desktop.ini")
                    .ToArray();
                Assert.Empty(übrigeDateien);
            }
            finally
            {
                TryDeleteDirectory(targetFolder);
                TryDeleteDirectory(imageDir);
            }
        }

        [Fact]
        public void Set_WaehrendAndereInstanzDenOrdnerSperrt_WirftSofortKlarenFehlerUndBleibtDanachFunktionsfaehig()
        {
            // Regressionstest aus der 3. ChatGPT-Prüfung (Fehler #5: zwei gleichzeitige
            // Instanzen konnten sich gegenseitig Sicherung/Zustand zerstören). Hält eine
            // "andere Instanz" die Ordner-Sperrdatei, muss diese Instanz sofort einen
            // klaren Fehler bekommen, statt parallel loszulaufen - und der Ordner muss
            // danach ganz normal nutzbar bleiben.
            string targetFolder = Directory.CreateTempSubdirectory("OrdnerIconErsteller_Ziel_").FullName;
            string imageDir = Directory.CreateTempSubdirectory("OrdnerIconErsteller_Bild_").FullName;
            try
            {
                string image = CreateTestImage(imageDir, "bild.png");
                string lockPath = Path.Combine(targetFolder, ".ordnericonersteller.lock");

                using (new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    Assert.Throws<IOException>(() => FolderIconService.SetFolderIcon(targetFolder, image));
                }

                Assert.False(File.Exists(Path.Combine(targetFolder, "desktop.ini")),
                    "Während die Sperre gehalten wird, darf keine Änderung stattfinden.");

                // Nach Freigabe der Sperre muss der Vorgang ganz normal funktionieren.
                FolderIconService.SetFolderIcon(targetFolder, image);
                Assert.True(File.Exists(Path.Combine(targetFolder, "desktop.ini")));
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
