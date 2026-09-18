using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
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

        /// <summary>
        /// Erzeugt echte, gültige PNG-Bytes für Referenzbild-Tests - seit Pruefbericht
        /// v2.2.1 (Freigabeblocker 2) lädt/validiert der Scanner Bildkandidaten
        /// tatsächlich vollständig über AiIconService.LoadReferenceImage, eine beliebige
        /// Text-/Platzhalterdatei mit Bild-Endung genügt dafür nicht mehr.
        /// </summary>
        private static byte[] CreateValidPngBytes()
        {
            using var bmp = new System.Drawing.Bitmap(8, 8, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var g = System.Drawing.Graphics.FromImage(bmp);
            g.Clear(System.Drawing.Color.CornflowerBlue);
            using var stream = new MemoryStream();
            bmp.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            return stream.ToArray();
        }

        [Fact]
        public void Scan_LeererOrdner_ErwaehntDasImPromptUndLiefertKeinReferenzbild()
        {
            string folder = CreateTempFolder();
            try
            {
                var result = FolderContentScanner.Scan(folder);

                Assert.Null(result.ReferenceImagePath);
                Assert.Null(result.ReferenceImage);
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
        public void Scan_OrdnerMitGueltigemBild_LaedtEsAlsValidiertesReferenzbildErwaehntEsAberNochNichtImPrompt()
        {
            // Pruefbericht v2.2.1, weiterer Fehler 3/4: der Scanner selbst darf den
            // Referenz-Hinweis nicht mehr direkt in den Prompt einbauen - das entscheidet
            // erst MainForm, NACHDEM der Benutzer der Übertragung genau dieses Bildes
            // ausdrücklich zugestimmt hat (AiConsentService.ConfirmReferenceImage). Der
            // Scanner liefert deshalb hier bereits das vollständig geladene/validierte
            // ReferenceImage plus Pfad, der Prompt selbst bleibt aber unverändert.
            string folder = CreateTempFolder();
            try
            {
                string imagePath = Path.Combine(folder, "Urlaubsfoto.png");
                File.WriteAllBytes(imagePath, CreateValidPngBytes());

                var result = FolderContentScanner.Scan(folder);

                Assert.Equal(imagePath, result.ReferenceImagePath);
                Assert.NotNull(result.ReferenceImage);
                Assert.Equal("image/png", result.ReferenceImage!.Value.MimeType);
                Assert.DoesNotContain("Beispielbild", result.ImplicitPrompt);

                string withMention = FolderContentScanner.WithReferenceImageMention(result.ImplicitPrompt);
                Assert.Contains("Beispielbild", withMention);
            }
            finally
            {
                TryDeleteDirectory(folder);
            }
        }

        [Fact]
        public void Scan_OrdnerMitBeschaedigtemBild_UeberspringtEsUndSuchtWeiter()
        {
            // Pruefbericht v2.2.1, weiterer Fehler 3: ein ungültiger/beschädigter erster
            // Kandidat darf nicht einfach zu "kein Referenzbild" führen, wenn danach noch
            // ein gültiges zweites Bild im Ordner liegt.
            string folder = CreateTempFolder();
            try
            {
                File.WriteAllBytes(Path.Combine(folder, "a-kaputt.png"), Encoding.UTF8.GetBytes("kein bild"));
                string validPath = Path.Combine(folder, "b-gueltig.png");
                File.WriteAllBytes(validPath, CreateValidPngBytes());

                var result = FolderContentScanner.Scan(folder);

                Assert.Equal(validPath, result.ReferenceImagePath);
                Assert.NotNull(result.ReferenceImage);
            }
            finally
            {
                TryDeleteDirectory(folder);
            }
        }

        [Fact]
        public void Scan_AllowReferenceImageFalse_LiefertKeinReferenzbildUndErwaehntEsNichtImPrompt()
        {
            // Pruefbericht v2.2.0 (weiterer Fehler: "bei OpenAI behauptet der Prompt,
            // ein Referenzbild werde mitgesendet, obwohl OpenAI es vollstaendig
            // verwirft") - MainForm uebergibt jetzt allowReferenceImage:false, wenn der
            // gewaehlte Anbieter (aktuell OpenAI) kein Referenzbild entgegennimmt.
            string folder = CreateTempFolder();
            try
            {
                File.WriteAllBytes(Path.Combine(folder, "Urlaubsfoto.png"), CreateValidPngBytes());

                var result = FolderContentScanner.Scan(folder, allowReferenceImage: false);

                Assert.Null(result.ReferenceImagePath);
                Assert.Null(result.ReferenceImage);
                Assert.DoesNotContain("Beispielbild", result.ImplicitPrompt);
                Assert.Contains("Urlaubsfoto.png", result.ImplicitPrompt);
            }
            finally
            {
                TryDeleteDirectory(folder);
            }
        }

        [Fact]
        public void Scan_MehrAlsFuenfundzwanzigDateien_ListetNurDieErstenFuenfundzwanzigMitHinweisAufWeitere()
        {
            // Pruefbericht v2.2.0 (weiterer Fehler: "speichert trotz der 25-Namen-
            // Grenze zunaechst saemtliche Eintraege") - Regressionstest dafuer, dass
            // die Namensliste im Prompt weiterhin bei 25 endet und "und weitere"
            // erscheint, obwohl Scan() jetzt intern nicht mehr alle Eintraege in eine
            // Liste sammelt.
            string folder = CreateTempFolder();
            try
            {
                for (int i = 0; i < 30; i++)
                {
                    File.WriteAllText(Path.Combine(folder, $"Datei-{i:00}.txt"), "x");
                }

                var result = FolderContentScanner.Scan(folder);

                // Enumerationsreihenfolge ist nicht garantiert alphabetisch - deshalb
                // wird hier bewusst NICHT auf einen konkreten enthaltenen/fehlenden
                // Dateinamen geprüft, sondern nur darauf, dass genau 25 der 30 Namen
                // aufgelistet werden.
                int listedCount = Regex.Matches(result.ImplicitPrompt, @"Datei-\d\d\.txt").Count;
                Assert.Equal(25, listedCount);
                Assert.Contains("und weitere", result.ImplicitPrompt);
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
