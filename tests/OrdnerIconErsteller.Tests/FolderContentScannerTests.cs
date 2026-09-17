using System;
using System.IO;
using Xunit;

namespace OrdnerIconErsteller.Tests
{
    // Deckt "Icon ohne Text" ab (Merten-Feedback nach v2.1.4): bei leerem Prompt
    // durchsucht das Programm den Zielordner selbst, um daraus eine implizite
    // Beschreibung zu bilden und - falls vorhanden - ein bereits im Ordner
    // liegendes Bild als Referenz für die KI-Bilderzeugung vorzuschlagen.
    public class FolderContentScannerTests
    {
        private static string CreateTempFolder()
        {
            return Directory.CreateTempSubdirectory("OrdnerIconErsteller_Scan_").FullName;
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            catch
            {
                // Aufräumen ist best-effort - ein Fehlschlag hier darf den Test nicht
                // zum Absturz bringen.
            }
        }

        [Fact]
        public void Scan_LeererOrdner_ErwaehntDasImPromptUndLiefertKeinReferenzbild()
        {
            string folder = CreateTempFolder();
            try
            {
                var result = FolderContentScanner.Scan(folder);

                Assert.Null(result.ReferenceImagePath);
                Assert.Contains("leer", result.ImplicitPrompt, StringComparison.OrdinalIgnoreCase);
                Assert.Contains(new DirectoryInfo(folder).Name, result.ImplicitPrompt);
            }
            finally
            {
                TryDeleteDirectory(folder);
            }
        }

        [Fact]
        public void Scan_OrdnerMitGewoehnlichenDateien_ListetDereNamenImPromptAufOhneReferenzbild()
        {
            string folder = CreateTempFolder();
            try
            {
                File.WriteAllText(Path.Combine(folder, "Rechnung.pdf"), "x");
                File.WriteAllText(Path.Combine(folder, "Notizen.txt"), "x");

                var result = FolderContentScanner.Scan(folder);

                Assert.Null(result.ReferenceImagePath);
                Assert.Contains("Rechnung.pdf", result.ImplicitPrompt);
                Assert.Contains("Notizen.txt", result.ImplicitPrompt);
            }
            finally
            {
                TryDeleteDirectory(folder);
            }
        }

        [Fact]
        public void Scan_OrdnerMitBild_LiefertDessenPfadAlsReferenzbildUndErwaehntEsImPrompt()
        {
            string folder = CreateTempFolder();
            try
            {
                string imagePath = Path.Combine(folder, "Urlaubsfoto.jpg");
                File.WriteAllBytes(imagePath, new byte[] { 1, 2, 3 });

                var result = FolderContentScanner.Scan(folder);

                Assert.Equal(imagePath, result.ReferenceImagePath);
                Assert.Contains("Beispielbild", result.ImplicitPrompt);
            }
            finally
            {
                TryDeleteDirectory(folder);
            }
        }

        [Fact]
        public void Scan_IgnoriertEigeneZustandsUndFruehereKiIconDateien()
        {
            string folder = CreateTempFolder();
            try
            {
                // Eigene Zustands-/Sicherungsdateien und ein früher erzeugtes KI-Icon -
                // dürfen weder das Referenzbild liefern noch in der Dateiliste auftauchen.
                File.WriteAllBytes(Path.Combine(folder, "desktop.ini"), new byte[] { 1 });
                File.WriteAllBytes(Path.Combine(folder, "FolderIcon.ico"), new byte[] { 1 });
                File.WriteAllBytes(Path.Combine(folder, ".ordnericonersteller-state"), new byte[] { 1 });
                File.WriteAllBytes(Path.Combine(folder, "FolderIcon.ico.before-ordnericonersteller.bak"), new byte[] { 1 });
                File.WriteAllBytes(Path.Combine(folder, "Icon-Original (von KI erzeugt) 2026-01-01_00-00-00.png"), new byte[] { 1 });

                var result = FolderContentScanner.Scan(folder);

                Assert.Null(result.ReferenceImagePath);
                Assert.DoesNotContain("desktop.ini", result.ImplicitPrompt);
                Assert.DoesNotContain("FolderIcon.ico", result.ImplicitPrompt);
                Assert.DoesNotContain("Icon-Original", result.ImplicitPrompt);
            }
            finally
            {
                TryDeleteDirectory(folder);
            }
        }
    }
}
