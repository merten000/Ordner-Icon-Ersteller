using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace OrdnerIconErsteller.Tests
{
    /// <summary>
    /// Deckt die reinen, von HTTP entkoppelten Methoden von <see cref="AiIconService"/>
    /// ab: die erzeugten Request-Bodys, das Parsen von OpenAI-/Google-Antworten
    /// (gültig, unvollständig, falsch typisiert, Gemini-"Thinking"-Bilder) und die
    /// erzwungene Bild-Dekodierung/Neu-Encodierung. Es werden AUSSCHLIESSLICH die
    /// internen, HTTP-unabhängigen Methoden getestet - es findet zu keinem
    /// Zeitpunkt ein echter, kostenpflichtiger Netzwerkaufruf an OpenAI/Google
    /// statt (siehe 2. ChatGPT-Prüfbericht zur KI-Icon-Gegenprüfung, Commit
    /// a6af83f, Punkt 7: "risikoreichste neue Pfade haben keine automatisierten
    /// Tests" - die zuvor in README.md geäußerte Einschätzung, dies sei wegen der
    /// Kosten grundsätzlich nicht automatisiert testbar, war laut ChatGPT so
    /// nicht korrekt, da genau diese Logik ohne echten HTTP-Aufruf prüfbar ist).
    /// </summary>
    public class AiIconServiceTests
    {
        private static byte[] CreateValidPngBytes(Color color, int width = 16, int height = 16)
        {
            using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.Clear(color);
            using var stream = new MemoryStream();
            bmp.Save(stream, ImageFormat.Png);
            return stream.ToArray();
        }

        /// <summary>
        /// Erzeugt ein PNG mit zufälligem "Rauschen" statt einer einfarbigen
        /// Fläche. Eine einfarbige Fläche komprimiert (PNG-Filter + Deflate)
        /// auch bei großen Abmessungen auf wenige hundert Byte - für einen
        /// Test, der eine um eine feste Byte-Anzahl gekürzte Datei simulieren
        /// soll, muss die Ausgangsdatei aber garantiert deutlich größer als
        /// diese Kürzung sein.
        /// </summary>
        private static byte[] CreateValidNoisePngBytes(int width, int height)
        {
            using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            var random = new Random(12345);
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    bmp.SetPixel(x, y, Color.FromArgb(255, random.Next(256), random.Next(256), random.Next(256)));
                }
            }
            using var stream = new MemoryStream();
            bmp.Save(stream, ImageFormat.Png);
            return stream.ToArray();
        }

        // ---- BuildOpenAiRequestBody -------------------------------------------------

        [Fact]
        public void BuildOpenAiRequestBody_EnthaeltAktuellesModellUndPngFormat()
        {
            object body = AiIconService.BuildOpenAiRequestBody("ein rotes Herz");
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(body));

            Assert.Equal("gpt-image-2.5-flare", doc.RootElement.GetProperty("model").GetString());
            Assert.Equal("ein rotes Herz", doc.RootElement.GetProperty("prompt").GetString());
            Assert.Equal("png", doc.RootElement.GetProperty("output_format").GetString());
            // "response_format" ist bei den aktuellen GPT-Bildmodellen kein
            // gültiger Parameter mehr (nur bei DALL-E relevant) und darf daher
            // nicht mehr im Request-Body auftauchen.
            Assert.False(doc.RootElement.TryGetProperty("response_format", out _));
        }

        // ---- ParseOpenAiImageResponse -----------------------------------------------

        [Fact]
        public void ParseOpenAiImageResponse_GueltigeAntwort_LiefertBildBytes()
        {
            byte[] originalBytes = CreateValidPngBytes(Color.Tomato);
            string base64 = Convert.ToBase64String(originalBytes);
            string responseJson = $$"""{"data":[{"b64_json":"{{base64}}"}]}""";

            byte[] result = AiIconService.ParseOpenAiImageResponse(responseJson);

            Assert.Equal(originalBytes, result);
        }

        [Fact]
        public void ParseOpenAiImageResponse_FehlendesDataFeld_WirftInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(
                () => AiIconService.ParseOpenAiImageResponse("""{"unerwartet":true}"""));
        }

        [Fact]
        public void ParseOpenAiImageResponse_LeeresDataArray_WirftInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(
                () => AiIconService.ParseOpenAiImageResponse("""{"data":[]}"""));
        }

        [Fact]
        public void ParseOpenAiImageResponse_B64JsonFalschTypisiert_WirftInvalidOperationException()
        {
            // "b64_json" als Zahl statt String - defensive TryGetProperty-Prüfung
            // muss das abfangen statt eine unbehandelte Exception zu werfen.
            Assert.Throws<InvalidOperationException>(
                () => AiIconService.ParseOpenAiImageResponse("""{"data":[{"b64_json":12345}]}"""));
        }

        [Fact]
        public void ParseOpenAiImageResponse_KeinGueltigesJson_WirftInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(
                () => AiIconService.ParseOpenAiImageResponse("das ist kein JSON"));
        }

        // ---- BuildGoogleRequestBody --------------------------------------------------

        [Fact]
        public void BuildGoogleRequestBody_EnthaeltSystemanweisungUndPrompt()
        {
            object body = AiIconService.BuildGoogleRequestBody("ein blaues Quadrat");
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(body));

            string systemText = doc.RootElement
                .GetProperty("system_instruction").GetProperty("parts")[0].GetProperty("text").GetString() ?? "";
            Assert.Contains("Always respond with a", systemText);

            string promptText = doc.RootElement
                .GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString() ?? "";
            Assert.Equal("ein blaues Quadrat", promptText);

            var modalities = doc.RootElement.GetProperty("generationConfig").GetProperty("responseModalities");
            Assert.Equal("TEXT", modalities[0].GetString());
            Assert.Equal("IMAGE", modalities[1].GetString());
        }

        [Fact]
        public void BuildGoogleRequestBody_MitReferenzbild_EnthaeltBildTeilVorDemTextTeil()
        {
            // Regressionstest für "Icon ohne Text" (Merten-Feedback nach v2.1.4):
            // ist ein Referenzbild aus dem Zielordner vorhanden, muss es als
            // inline_data-Teil VOR dem Text-Teil in "contents[0].parts" stehen, damit
            // Gemini es eindeutig als Grundlage für die Bearbeitung erkennt.
            byte[] referenceBytes = { 1, 2, 3, 4 };
            var referenceImage = new AiIconService.ReferenceImage(referenceBytes, "image/png");

            object body = AiIconService.BuildGoogleRequestBody("ein blaues Quadrat", referenceImage);
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(body));

            JsonElement parts = doc.RootElement.GetProperty("contents")[0].GetProperty("parts");
            Assert.Equal(2, parts.GetArrayLength());

            JsonElement inlineData = parts[0].GetProperty("inline_data");
            Assert.Equal("image/png", inlineData.GetProperty("mime_type").GetString());
            Assert.Equal(Convert.ToBase64String(referenceBytes), inlineData.GetProperty("data").GetString());

            Assert.Equal("ein blaues Quadrat", parts[1].GetProperty("text").GetString());
        }

        // ---- ParseGoogleImageResponse --------------------------------------------------

        [Fact]
        public void ParseGoogleImageResponse_GueltigesBild_LiefertBildBytes()
        {
            byte[] originalBytes = CreateValidPngBytes(Color.SteelBlue);
            string base64 = Convert.ToBase64String(originalBytes);
            // Bewusst KEIN interpolierter Rohstring ($$"""..."""): eine
            // Base64-/JSON-Nutzlast mit mehreren aufeinanderfolgenden
            // schließenden geschweiften Klammern direkt nach der Interpolation
            // kollidiert dort mit dem {{/}}-Trennzeichen des Compilers
            // (CS9007). Stattdessen ein Platzhalter-Token in einem normalen
            // Rohstring, der per string.Replace ersetzt wird.
            string responseJson = """
                {"candidates":[{"content":{"parts":[
                    {"inlineData":{"mimeType":"image/png","data":"__BASE64__"}}
                ]}}]}
                """.Replace("__BASE64__", base64);

            (byte[]? imageBytes, string? textOnly) = AiIconService.ParseGoogleImageResponse(responseJson);

            Assert.NotNull(imageBytes);
            Assert.Equal(originalBytes, imageBytes);
            Assert.Null(textOnly);
        }

        [Fact]
        public void ParseGoogleImageResponse_ThoughtBildWirdUebersprungenFinalesBildVerwendet()
        {
            // Gemini kann ein "Thinking"-Bild (thought: true) VOR dem eigentlichen
            // finalen Bild liefern - das Thought-Bild muss übersprungen und
            // stattdessen das nachfolgende, finale Bild zurückgegeben werden.
            byte[] thoughtBytes = CreateValidPngBytes(Color.Yellow);
            byte[] finalBytes = CreateValidPngBytes(Color.SteelBlue);
            string responseJson = """
                {"candidates":[{"content":{"parts":[
                    {"thought":true,"inlineData":{"mimeType":"image/png","data":"__THOUGHT_BASE64__"}},
                    {"inlineData":{"mimeType":"image/png","data":"__FINAL_BASE64__"}}
                ]}}]}
                """
                .Replace("__THOUGHT_BASE64__", Convert.ToBase64String(thoughtBytes))
                .Replace("__FINAL_BASE64__", Convert.ToBase64String(finalBytes));

            (byte[]? imageBytes, string? textOnly) = AiIconService.ParseGoogleImageResponse(responseJson);

            Assert.NotNull(imageBytes);
            Assert.Equal(finalBytes, imageBytes);
            Assert.Null(textOnly);
        }

        [Fact]
        public void ParseGoogleImageResponse_NurText_LiefertTextOhneBild()
        {
            string responseJson = """
                {"candidates":[{"content":{"parts":[
                    {"text":"Welche Farbe soll das Symbol haben?"}
                ]}}]}
                """;

            (byte[]? imageBytes, string? textOnly) = AiIconService.ParseGoogleImageResponse(responseJson);

            Assert.Null(imageBytes);
            Assert.Equal("Welche Farbe soll das Symbol haben?", textOnly);
        }

        [Fact]
        public void ParseGoogleImageResponse_FalscherMimeType_WirdUebersprungen()
        {
            // Ein inlineData-Teil mit einem NICHT mit "image/" beginnenden
            // mimeType wird als grobes Vorfilter übersprungen - das eigentliche
            // Sicherheitsnetz ist ohnehin DecodeAndReencodeAsPng.
            byte[] bytes = CreateValidPngBytes(Color.SteelBlue);
            string responseJson = """
                {"candidates":[{"content":{"parts":[
                    {"inlineData":{"mimeType":"application/octet-stream","data":"__BASE64__"}}
                ]}}]}
                """.Replace("__BASE64__", Convert.ToBase64String(bytes));

            (byte[]? imageBytes, string? textOnly) = AiIconService.ParseGoogleImageResponse(responseJson);

            Assert.Null(imageBytes);
            Assert.Null(textOnly);
        }

        [Fact]
        public void ParseGoogleImageResponse_UnerwartetesFormat_WirftInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(
                () => AiIconService.ParseGoogleImageResponse("""{"unerwartet":true}"""));
        }

        [Fact]
        public void ParseGoogleImageResponse_KeinGueltigesJson_WirftInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(
                () => AiIconService.ParseGoogleImageResponse("das ist kein JSON"));
        }

        // ---- DecodeAndReencodeAsPng ---------------------------------------------------

        [Fact]
        public void DecodeAndReencodeAsPng_GueltigesBild_LiefertNeuEncodiertesPng()
        {
            byte[] original = CreateValidPngBytes(Color.Tomato);

            byte[] result = AiIconService.DecodeAndReencodeAsPng(AiImageProvider.OpenAi, original);

            // Das Ergebnis muss selbst wieder ein gültiges, vollständig
            // dekodierbares PNG mit denselben Abmessungen sein.
            using var stream = new MemoryStream(result);
            using var image = Image.FromStream(stream);
            Assert.Equal(16, image.Width);
            Assert.Equal(16, image.Height);
        }

        [Fact]
        public void DecodeAndReencodeAsPng_AbgeschnitteneDatei_WirftInvalidOperationException()
        {
            // Deckt genau das im 2. ChatGPT-Prüfbericht (Punkt 4) demonstrierte
            // Szenario ab: eine um mehrere hundert Byte abgeschnittene, sonst
            // gültige PNG-Datei muss erkannt werden - die vorherige
            // ValidateDecodableImage-Prüfung (nur Image.FromStream + Width/Height)
            // hat das NICHT erkannt.
            byte[] original = CreateValidNoisePngBytes(width: 64, height: 64);
            Assert.True(original.Length > 4000,
                "Das Test-Rauschbild muss deutlich größer als die simulierte Kürzung sein.");
            byte[] truncated = new byte[original.Length - 1200];
            Array.Copy(original, truncated, truncated.Length);

            Assert.Throws<InvalidOperationException>(
                () => AiIconService.DecodeAndReencodeAsPng(AiImageProvider.OpenAi, truncated));
        }

        [Fact]
        public void DecodeAndReencodeAsPng_LeereBytes_WirftInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(
                () => AiIconService.DecodeAndReencodeAsPng(AiImageProvider.Google, Array.Empty<byte>()));
        }

        [Fact]
        public void DecodeAndReencodeAsPng_KeinBild_WirftInvalidOperationException()
        {
            byte[] keinBild = Encoding.UTF8.GetBytes("das ist definitiv kein Bild, sondern nur Text");

            Assert.Throws<InvalidOperationException>(
                () => AiIconService.DecodeAndReencodeAsPng(AiImageProvider.OpenAi, keinBild));
        }

        [Fact]
        public void DecodeAndReencodeAsPng_UebergrosseBilddaten_WirftInvalidOperationException()
        {
            // Simuliert eine Antwort, die die 30-MB-Obergrenze überschreitet -
            // ohne tatsächlich 30 MB an echten Bilddaten zu erzeugen, reicht ein
            // Array in Zielgröße (der Inhalt selbst ist für diese Prüfung
            // irrelevant, da die Längenprüfung VOR dem Decodierversuch greift).
            byte[] zuGross = new byte[AiIconService.MaxImageBytes + 1];

            Assert.Throws<InvalidOperationException>(
                () => AiIconService.DecodeAndReencodeAsPng(AiImageProvider.Google, zuGross));
        }

        /// <summary>
        /// Ein Stream, dessen ReadAsync niemals von selbst zurückkehrt, sondern nur auf
        /// Abbruch über den übergebenen CancellationToken reagiert - simuliert eine
        /// Netzwerkverbindung, die nach den HTTP-Headern hängen bleibt (genau das im 3.
        /// ChatGPT-Prüfbericht beschriebene Szenario: ResponseHeadersRead deckt nur die
        /// Header ab, das anschließende Lesen des Bodys konnte vorher unbegrenzt hängen).
        /// </summary>
        private sealed class NieEndenderStream : Stream
        {
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                // Wartet absichtlich unbegrenzt - kehrt ausschließlich über eine
                // OperationCanceledException zurück, sobald der Timeout-Token abläuft.
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                return 0;
            }
        }

        [Fact]
        public async Task ReadResponseTextWithLimitAsync_AntwortkoerperHaengt_WirftNachTimeoutKlareInvalidOperationException()
        {
            // Regressionstest aus der 3. ChatGPT-Gegenprüfung (Freigabeblocker 2): wegen
            // HttpCompletionOption.ResponseHeadersRead deckte der bisherige
            // HttpClient.Timeout nur die Zeit bis zu den HTTP-Headern ab - beim
            // anschließenden Lesen des Antwortkörpers konnte das Programm unbegrenzt
            // hängen bleiben. Verwendet die testbare Überladung mit einem sehr kurzen
            // Timeout, damit der Test nicht die echten 120 Sekunden warten muss.
            using var response = new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StreamContent(new NieEndenderStream()),
            };

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => AiIconService.ReadResponseTextWithLimitAsync(response, TimeSpan.FromMilliseconds(200)));
            stopwatch.Stop();

            Assert.Contains("innerhalb der zulässigen Zeit", ex.Message);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10),
                "Der Timeout-Pfad darf nicht auf den vollen HttpClient-Timeout warten, sondern muss beim " +
                "übergebenen Lese-Timeout selbst greifen.");
        }
    }
}
