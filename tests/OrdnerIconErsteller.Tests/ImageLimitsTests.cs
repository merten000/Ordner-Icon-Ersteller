using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Xunit;

namespace OrdnerIconErsteller.Tests
{
    // Runde 2, Befund F-003: Groessen-/Pixelgrenzen fuer manuell gewaehlte Bilder.
    public class ImageLimitsTests
    {
        [Fact]
        public void Grenzen_EntsprechenDenenDerKiBilder()
        {
            Assert.Equal(AiIconService.MaxImageBytes, ImageLimits.MaxFileBytes);
            Assert.Equal(50_000_000L, ImageLimits.MaxPixelCount);
        }

        [Theory]
        [InlineData(1000L, 100, 100, true)]
        [InlineData(30L * 1024 * 1024, -1, -1, true)]
        [InlineData(30L * 1024 * 1024 + 1, -1, -1, false)]
        [InlineData(10L, 10000, 5000, true)]      // genau 50 Megapixel
        [InlineData(10L, 10001, 5000, false)]     // knapp darueber
        [InlineData(10L, 0, 10, false)]
        [InlineData(10L, 10, -5, false)]
        [InlineData(10L, 100000, 100000, false)]  // darf nicht ueberlaufen
        public void Check_PrueftGroesseUndPixel(long bytes, int width, int height, bool erlaubt)
        {
            string? problem = ImageLimits.Check(bytes, width, height);
            Assert.Equal(erlaubt, problem is null);
        }

        [Fact]
        public void CreateIcoFromImage_ZuGrosseDatei_WirdAbgelehntOhneZielDatei()
        {
            string dir = Directory.CreateTempSubdirectory("OrdnerIconErsteller_Limit_").FullName;
            try
            {
                string source = Path.Combine(dir, "riesig.png");
                using (var fs = new FileStream(source, FileMode.Create, FileAccess.Write))
                {
                    fs.SetLength(ImageLimits.MaxFileBytes + 1); // Inhalt egal: Groesse wird VOR dem Dekodieren geprueft
                }
                string target = Path.Combine(dir, "ziel.ico");

                var ex = Assert.Throws<InvalidOperationException>(() => IconWriter.CreateIcoFromImage(source, target));

                Assert.Contains("zu groß", ex.Message);
                Assert.False(File.Exists(target));
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public void CreateIcoFromImage_ZuVieleMegapixel_WirdAbgelehntOhneZielDatei()
        {
            string dir = Directory.CreateTempSubdirectory("OrdnerIconErsteller_Limit_").FullName;
            try
            {
                // 8000 x 8000 = 64 Megapixel. 1 Bit pro Pixel haelt Speicher und Dateigroesse klein
                // (einfarbige Flaeche, PNG komprimiert extrem) - die Pixelgrenze greift trotzdem.
                string source = Path.Combine(dir, "viele-pixel.png");
                using (var bmp = new Bitmap(8000, 8000, PixelFormat.Format1bppIndexed))
                {
                    bmp.Save(source, ImageFormat.Png);
                }
                Assert.True(new FileInfo(source).Length < ImageLimits.MaxFileBytes);
                string target = Path.Combine(dir, "ziel.ico");

                var ex = Assert.Throws<InvalidOperationException>(() => IconWriter.CreateIcoFromImage(source, target));

                Assert.Contains("Megapixel", ex.Message);
                Assert.False(File.Exists(target));
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public void SetFolderIcon_ZuGrossesBild_LaesstOrdnerUnveraendertUndHinterlaesstNichts()
        {
            string folder = Directory.CreateTempSubdirectory("OrdnerIconErsteller_Ziel_").FullName;
            string imageDir = Directory.CreateTempSubdirectory("OrdnerIconErsteller_Bild_").FullName;
            try
            {
                string source = Path.Combine(imageDir, "riesig.png");
                using (var fs = new FileStream(source, FileMode.Create, FileAccess.Write))
                {
                    fs.SetLength(ImageLimits.MaxFileBytes + 1);
                }

                Assert.Throws<InvalidOperationException>(() => FolderIconService.SetFolderIcon(folder, source));

                // Die normale Transaktion rollt zurueck: keine Statusdatei, keine Sicherung, kein Icon.
                Assert.Empty(Directory.GetFileSystemEntries(folder));
            }
            finally
            {
                Directory.Delete(folder, recursive: true);
                Directory.Delete(imageDir, recursive: true);
            }
        }
    }
}
