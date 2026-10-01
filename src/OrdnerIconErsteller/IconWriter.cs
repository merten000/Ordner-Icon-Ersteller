using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Collections.Generic;

namespace OrdnerIconErsteller
{
    /// <summary>
    /// Erstellt eine .ico-Datei aus einem beliebigen Bild (PNG/JPG/BMP/...).
    /// Es werden mehrere Größen eingebettet (16/32/48/256 px), jeweils als
    /// PNG-komprimierte Icon-Frames (seit Windows Vista unterstützt), damit
    /// Windows in allen Explorer-Ansichten ein sauberes Icon anzeigen kann.
    /// Reine .NET-Implementierung, kein externer Aufruf.
    /// </summary>
    internal static class IconWriter
    {
        private static readonly int[] Sizes = { 16, 32, 48, 256 };

        public static void CreateIcoFromImage(string sourceImagePath, string targetIcoPath)
        {
            // Runde 2 (Befund F-003): vorher wurde jedes manuell gewaehlte Bild ohne jede
            // Grenze mit Image.FromFile geoeffnet und voll in den Speicher gezeichnet. Jetzt
            // werden zuerst Dateigroesse und - ohne die Bilddaten zu dekodieren
            // (validateImageData: false liest nur den Kopf) - die Abmessungen geprueft;
            // erst danach wird tatsaechlich gezeichnet. Die Grenzen entsprechen denen der
            // KI-Bilder (siehe ImageLimits).
            long fileBytes = new FileInfo(sourceImagePath).Length;
            string? sizeProblem = ImageLimits.Check(fileBytes, -1, -1);
            if (sizeProblem is not null)
            {
                throw new InvalidOperationException(sizeProblem);
            }

            using var sourceStream = new FileStream(sourceImagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var original = Image.FromStream(sourceStream, useEmbeddedColorManagement: false, validateImageData: false);

            string? dimensionProblem = ImageLimits.Check(fileBytes, original.Width, original.Height);
            if (dimensionProblem is not null)
            {
                throw new InvalidOperationException(dimensionProblem);
            }

            var frames = new List<(int size, byte[] pngBytes)>();
            foreach (var size in Sizes)
            {
                using var resized = ResizeSquare(original, size);
                using var ms = new MemoryStream();
                resized.Save(ms, ImageFormat.Png);
                frames.Add((size, ms.ToArray()));
            }

            WriteIco(frames, targetIcoPath);
        }

        private static Bitmap ResizeSquare(Image source, int size)
        {
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            // Bild proportional einpassen, Rest transparent lassen
            double scale = Math.Min((double)size / source.Width, (double)size / source.Height);
            int w = (int)Math.Round(source.Width * scale);
            int h = (int)Math.Round(source.Height * scale);
            int x = (size - w) / 2;
            int y = (size - h) / 2;
            g.DrawImage(source, x, y, w, h);

            return bmp;
        }

        private static void WriteIco(List<(int size, byte[] pngBytes)> frames, string targetIcoPath)
        {
            using var fs = new FileStream(targetIcoPath, FileMode.Create, FileAccess.Write);
            using var bw = new BinaryWriter(fs);

            // ICONDIR
            bw.Write((short)0);              // reserved
            bw.Write((short)1);              // type = 1 (icon)
            bw.Write((short)frames.Count);   // Anzahl Bilder

            int headerSize = 6 + (16 * frames.Count);
            int offset = headerSize;

            // ICONDIRENTRY je Bild
            foreach (var frame in frames)
            {
                byte sizeByte = frame.size >= 256 ? (byte)0 : (byte)frame.size;
                bw.Write(sizeByte);              // Breite (0 = 256)
                bw.Write(sizeByte);              // Höhe (0 = 256)
                bw.Write((byte)0);               // Farbpalette (0 = kein Palette)
                bw.Write((byte)0);               // reserved
                bw.Write((short)1);              // Farbebenen
                bw.Write((short)32);             // Bits pro Pixel
                bw.Write(frame.pngBytes.Length); // Datengröße
                bw.Write(offset);                // Offset im File
                offset += frame.pngBytes.Length;
            }

            // Bilddaten
            foreach (var frame in frames)
            {
                bw.Write(frame.pngBytes);
            }
        }
    }
}
