using System;
using System.IO;
using System.Linq;
using Xunit;
using System.Drawing;
using System.Drawing.Imaging;

namespace OrdnerIconErsteller.Tests
{
    // Runde 2, Befund F-002: sicherer, minimaler Wiederherstellungsweg nach hartem Prozessende.
    // Grundsatz: nie loeschen, nie raten. Nur eine Sicherung ohne Statusdatei, die Byte fuer Byte
    // mit der noch vorhandenen Originaldatei uebereinstimmt, darf auf Bestaetigung beiseite gelegt
    // (umbenannt) werden; alles andere bleibt eine manuelle Pruefung.
    public class FolderIconServiceOrphanBackupTests
    {
        private const string Bak = ".before-ordnericonersteller.bak";

        private static string NewFolder() => Directory.CreateTempSubdirectory("OrdnerIconErsteller_Orphan_").FullName;

        private static string CreateTestImage(string directory, string fileName)
        {
            string path = Path.Combine(directory, fileName);
            using var bmp = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.SteelBlue);
            bmp.Save(path, ImageFormat.Png);
            return path;
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

        [Fact]
        public void Diagnose_LeererOrdner_KeinProblem()
        {
            string folder = NewFolder();
            try
            {
                Assert.Equal(FolderIconService.OrphanBackupState.NoProblem,
                    FolderIconService.DiagnoseOrphanBackups(folder).State);
            }
            finally { TryDeleteDirectory(folder); }
        }

        [Fact]
        public void Diagnose_MitGueltigerStatusdatei_KeinProblem()
        {
            string folder = NewFolder();
            string imageDir = NewFolder();
            try
            {
                FolderIconService.SetFolderIcon(folder, CreateTestImage(imageDir, "bild.png"));
                Assert.Equal(FolderIconService.OrphanBackupState.NoProblem,
                    FolderIconService.DiagnoseOrphanBackups(folder).State);
            }
            finally { TryDeleteDirectory(folder); TryDeleteDirectory(imageDir); }
        }

        [Fact]
        public void Diagnose_IdentischeSicherungOhneStatusdatei_IstSicherBeiseiteZuLegen()
        {
            string folder = NewFolder();
            try
            {
                string ini = Path.Combine(folder, "desktop.ini");
                File.WriteAllText(ini, "[.ShellClassInfo]\r\nIconResource=x.ico,0\r\n");
                File.Copy(ini, ini + Bak);

                var diagnosis = FolderIconService.DiagnoseOrphanBackups(folder);

                Assert.Equal(FolderIconService.OrphanBackupState.SafeToSetAside, diagnosis.State);
                Assert.Single(diagnosis.FileNames);
                Assert.DoesNotContain(folder, diagnosis.Explanation);
            }
            finally { TryDeleteDirectory(folder); }
        }

        [Fact]
        public void SetFolderIcon_MitUebrigerSicherung_VerweigertWeiterhin()
        {
            // Der bestehende Schutz bleibt unveraendert: ohne ausdrueckliche Bestaetigung
            // (SetAsideOrphanBackups) aendert SetFolderIcon in diesem Zustand nichts.
            string folder = NewFolder();
            string imageDir = NewFolder();
            try
            {
                string ini = Path.Combine(folder, "desktop.ini");
                File.WriteAllText(ini, "ORIGINAL");
                File.Copy(ini, ini + Bak);

                Assert.Throws<InvalidDataException>(() => FolderIconService.SetFolderIcon(folder, CreateTestImage(imageDir, "bild.png")));

                Assert.Equal("ORIGINAL", File.ReadAllText(ini));
                Assert.True(File.Exists(ini + Bak));
            }
            finally { TryDeleteDirectory(folder); TryDeleteDirectory(imageDir); }
        }

        [Fact]
        public void SetAside_BenenntNurUmUndLoeschtNichts_DanachFunktioniertSetzenUndEntfernen()
        {
            string folder = NewFolder();
            string imageDir = NewFolder();
            try
            {
                string ini = Path.Combine(folder, "desktop.ini");
                string ico = Path.Combine(folder, "FolderIcon.ico");
                File.WriteAllText(ini, "[.ShellClassInfo]\r\nIconResource=x.ico,0\r\n");
                File.WriteAllBytes(ico, new byte[] { 0, 0, 1, 0, 1, 2, 3, 4, 5 });
                File.Copy(ini, ini + Bak);
                File.Copy(ico, ico + Bak);
                string iniBefore = File.ReadAllText(ini);
                byte[] icoBefore = File.ReadAllBytes(ico);
                int fileCountBefore = Directory.GetFiles(folder).Length;

                var moved = FolderIconService.SetAsideOrphanBackups(folder);

                Assert.Equal(2, moved.Count);
                Assert.Equal(fileCountBefore, Directory.GetFiles(folder).Length);   // nichts geloescht
                Assert.False(File.Exists(ini + Bak));
                Assert.False(File.Exists(ico + Bak));
                Assert.All(moved, name => Assert.True(File.Exists(Path.Combine(folder, name))));
                Assert.Equal(iniBefore, File.ReadAllText(ini));                      // Originale unberuehrt
                Assert.Equal(icoBefore, File.ReadAllBytes(ico));
                Assert.Equal(FolderIconService.OrphanBackupState.NoProblem,
                    FolderIconService.DiagnoseOrphanBackups(folder).State);

                // Danach laeuft der normale Ablauf wieder: frische Sicherung, Statusdatei, exakte Wiederherstellung.
                FolderIconService.SetFolderIcon(folder, CreateTestImage(imageDir, "bild.png"));
                Assert.True(File.Exists(Path.Combine(folder, ".ordnericonersteller-state")));
                FolderIconService.RemoveFolderIcon(folder);
                Assert.Equal(iniBefore, File.ReadAllText(ini));
                Assert.Equal(icoBefore, File.ReadAllBytes(ico));
            }
            finally { TryDeleteDirectory(folder); TryDeleteDirectory(imageDir); }
        }

        [Fact]
        public void Diagnose_AbweichendeSicherung_NurManuellUndSetAsideVerweigert()
        {
            string folder = NewFolder();
            try
            {
                string ini = Path.Combine(folder, "desktop.ini");
                File.WriteAllText(ini, "AKTUELL");
                File.WriteAllText(ini + Bak, "ANDERS");

                Assert.Equal(FolderIconService.OrphanBackupState.NeedsManualReview,
                    FolderIconService.DiagnoseOrphanBackups(folder).State);
                Assert.Throws<InvalidOperationException>(() => FolderIconService.SetAsideOrphanBackups(folder));

                Assert.Equal("AKTUELL", File.ReadAllText(ini));
                Assert.Equal("ANDERS", File.ReadAllText(ini + Bak));
            }
            finally { TryDeleteDirectory(folder); }
        }

        [Fact]
        public void Diagnose_OriginalFehlt_NurManuellUndSicherungBleibt()
        {
            string folder = NewFolder();
            try
            {
                string bak = Path.Combine(folder, "desktop.ini" + Bak);
                File.WriteAllText(bak, "NUR-NOCH-HIER");

                Assert.Equal(FolderIconService.OrphanBackupState.NeedsManualReview,
                    FolderIconService.DiagnoseOrphanBackups(folder).State);
                Assert.Throws<InvalidOperationException>(() => FolderIconService.SetAsideOrphanBackups(folder));
                Assert.Equal("NUR-NOCH-HIER", File.ReadAllText(bak));
            }
            finally { TryDeleteDirectory(folder); }
        }

        [Fact]
        public void Diagnose_NurEineSicherungAbweichend_GesamtNurManuell()
        {
            string folder = NewFolder();
            try
            {
                string ini = Path.Combine(folder, "desktop.ini");
                string ico = Path.Combine(folder, "FolderIcon.ico");
                File.WriteAllText(ini, "A");
                File.Copy(ini, ini + Bak);
                File.WriteAllText(ico, "B");
                File.WriteAllText(ico + Bak, "C");

                Assert.Equal(FolderIconService.OrphanBackupState.NeedsManualReview,
                    FolderIconService.DiagnoseOrphanBackups(folder).State);
            }
            finally { TryDeleteDirectory(folder); }
        }

        [Fact]
        public void Diagnose_GrosseDateienMitEinemAbweichendenByteAmEnde_NurManuell()
        {
            string folder = NewFolder();
            try
            {
                string ini = Path.Combine(folder, "desktop.ini");
                byte[] data = new byte[300_000];
                new Random(1).NextBytes(data);
                File.WriteAllBytes(ini, data);
                File.Copy(ini, ini + Bak);
                Assert.Equal(FolderIconService.OrphanBackupState.SafeToSetAside,
                    FolderIconService.DiagnoseOrphanBackups(folder).State);

                data[^1] ^= 1;
                File.WriteAllBytes(ini + Bak, data);
                Assert.Equal(FolderIconService.OrphanBackupState.NeedsManualReview,
                    FolderIconService.DiagnoseOrphanBackups(folder).State);
            }
            finally { TryDeleteDirectory(folder); }
        }

        [Fact]
        public void SetAside_BelegteOrdnersperre_MeldetFehlerUndAendertNichts()
        {
            string folder = NewFolder();
            try
            {
                string ini = Path.Combine(folder, "desktop.ini");
                File.WriteAllText(ini, "A");
                File.Copy(ini, ini + Bak);
                using var held = new FileStream(Path.Combine(folder, ".ordnericonersteller.lock"),
                    FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

                Assert.Throws<IOException>(() => FolderIconService.SetAsideOrphanBackups(folder));
                Assert.True(File.Exists(ini + Bak));
            }
            finally { TryDeleteDirectory(folder); }
        }
    }
}
