using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace OrdnerIconErsteller.Tests
{
    // Runde 2, Befund F-001: kontrollierter Abbruch laufender KI-Arbeit. Es findet kein echter
    // Netzwerkaufruf statt - getestet werden nur die abbrechbaren, von HTTP entkoppelten Teile.
    public class CancellationTests
    {
        private sealed class NieEndenderStream : Stream
        {
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
                return 0;
            }
        }

        [Fact]
        public async Task GenerateIconImageAsync_BereitsAbgebrochen_WirftOperationCanceledException()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Wird abgebrochen, bevor ein API-Schluessel gelesen oder eine Anfrage gesendet wird.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => AiIconService.GenerateIconImageAsync(AiImageProvider.OpenAi, "ein blaues Icon", null, cts.Token));
        }

        [Fact]
        public async Task GenerateIconImageAsync_LeererPrompt_BleibtArgumentExceptionAuchBeiAbbruch()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<ArgumentException>(
                () => AiIconService.GenerateIconImageAsync(AiImageProvider.OpenAi, " ", null, cts.Token));
        }

        [Fact]
        public async Task ReadResponseTextWithLimitAsync_BenutzerAbbruch_WirdNichtAlsZeitueberschreitungGemeldet()
        {
            using var response = new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StreamContent(new NieEndenderStream()),
            };
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            // Langer Lese-Timeout: nur der Benutzer-Abbruch kann hier greifen.
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => AiIconService.ReadResponseTextWithLimitAsync(response, TimeSpan.FromSeconds(60), cts.Token));
            stopwatch.Stop();

            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), "Der Abbruch muss sofort greifen, nicht erst nach dem Lese-Timeout.");
        }

        [Fact]
        public void Scan_BereitsAbgebrochen_WirftOperationCanceledException()
        {
            string folder = Directory.CreateTempSubdirectory("OrdnerIconErsteller_ScanCancel_").FullName;
            try
            {
                File.WriteAllText(Path.Combine(folder, "datei.txt"), "x");
                using var cts = new CancellationTokenSource();
                cts.Cancel();

                Assert.Throws<OperationCanceledException>(() => FolderContentScanner.Scan(folder, true, cts.Token));
            }
            finally
            {
                Directory.Delete(folder, recursive: true);
            }
        }

        [Fact]
        public void Scan_OhneAbbruch_VerhaeltSichWieBisher()
        {
            string folder = Directory.CreateTempSubdirectory("OrdnerIconErsteller_ScanCancel_").FullName;
            try
            {
                File.WriteAllText(Path.Combine(folder, "datei.txt"), "x");

                var result = FolderContentScanner.Scan(folder, true, CancellationToken.None);

                Assert.Contains("datei.txt", result.ImplicitPrompt);
            }
            finally
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }
}
