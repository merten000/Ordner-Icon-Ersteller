using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Xunit;

namespace OrdnerIconErsteller.Tests
{
    public class IconWriterTests
    {
        [Fact]
        public void CreateIcoFromImage_ErzeugtGueltigeIcoDatei()
        {
            string tempDir = Directory.CreateTempSubdirectory("OrdnerIconErsteller_Tests_").FullName;
            try
            {
                string sourcePng = Path.Combine(tempDir, "quelle.png");
                using (var bmp = new Bitmap(64, 64, PixelFormat.Format32bppArgb))
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.CornflowerBlue);
                    bmp.Save(sourcePng, ImageFormat.Png);
                }

                string targetIco = Path.Combine(tempDir, "ziel.ico");

                IconWriter.CreateIcoFromImage(sourcePng, targetIco);

                Assert.True(File.Exists(targetIco));

                byte[] bytes = File.ReadAllBytes(targetIco);

                // ICONDIR-Header: reserved=0, type=1 (Icon)
                Assert.Equal(0, BitConverter.ToInt16(bytes, 0));
                Assert.Equal(1, BitConverter.ToInt16(bytes, 2));

                short imageCount = BitConverter.ToInt16(bytes, 4);
                Assert.Equal(4, imageCount); // 16/32/48/256 px

                Assert.True(bytes.Length > 100, "ICO-Datei wirkt zu klein, um echte Bilddaten zu enthalten.");
            }
            finally
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
