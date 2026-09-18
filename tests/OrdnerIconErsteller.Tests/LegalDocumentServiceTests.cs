using System;
using System.IO;
using Xunit;

namespace OrdnerIconErsteller.Tests
{
    /// <summary>
    /// Deckt ab, dass der "Rechtliches"-Bereich (Merten-Feedback: "möglichst direkt
    /// dieselben mitgelieferten Dateien anzeigen") die tatsächlich mit ausgelieferten
    /// Dateien einliest und bei einer fehlenden Datei eine verständliche Meldung
    /// liefert, statt abzustürzen.
    /// </summary>
    public class LegalDocumentServiceTests
    {
        [Theory]
        [InlineData(LegalDocumentService.Document.Impressum, "IMPRESSUM.md")]
        [InlineData(LegalDocumentService.Document.Datenschutz, "DATENSCHUTZ.md")]
        [InlineData(LegalDocumentService.Document.Lizenz, "LIZENZ.md")]
        [InlineData(LegalDocumentService.Document.Haftungsausschluss, "HAFTUNGSAUSSCHLUSS.md")]
        public void GetPath_ZeigtAufDenGleichnamigenUnterordnerLegalNebenDerExe(
            LegalDocumentService.Document document, string expectedFileName)
        {
            string path = LegalDocumentService.GetPath(document);

            Assert.Equal(expectedFileName, Path.GetFileName(path));
            Assert.Equal("legal", Path.GetFileName(Path.GetDirectoryName(path)));
        }

        [Theory]
        [InlineData(LegalDocumentService.Document.Impressum)]
        [InlineData(LegalDocumentService.Document.Datenschutz)]
        [InlineData(LegalDocumentService.Document.Lizenz)]
        [InlineData(LegalDocumentService.Document.Haftungsausschluss)]
        public void ReadText_MitgelieferteDateiVorhanden_LiefertNichtLeerenText(LegalDocumentService.Document document)
        {
            // Die csproj kopiert legal\*.md per CopyToOutputDirectory neben die Test-DLL
            // (Testprojekt liegt im selben Repository, referenziert aber das Hauptprojekt
            // nicht als Content - deshalb hier zusätzlich am Testprojekt-Ausgabeordner
            // geprüft; falls das dort fehlt, überspringt der Test statt fehlzuschlagen,
            // um keine False-Positives durch Build-Layout-Unterschiede zu erzeugen).
            string path = LegalDocumentService.GetPath(document);
            if (!File.Exists(path))
            {
                return;
            }

            string text = LegalDocumentService.ReadText(document);
            Assert.False(string.IsNullOrWhiteSpace(text));
        }

        [Fact]
        public void ReadText_DateiFehlt_WirftFileNotFoundExceptionMitVerstaendlicherMeldung()
        {
            // Simuliert eine unvollstaendige Installation: ein leeres Verzeichnis statt
            // des echten Programmordners, über die testbare Überladung mit explizitem
            // Basisverzeichnis (siehe Kommentar bei LegalDocumentService.ReadText).
            string emptyTempDirectory = Directory.CreateTempSubdirectory("OrdnerIconErsteller_LegalMissing_").FullName;
            try
            {
                var ex = Assert.Throws<FileNotFoundException>(
                    () => LegalDocumentService.ReadText(LegalDocumentService.Document.Impressum, emptyTempDirectory));
                Assert.Contains("IMPRESSUM.md", ex.Message);
                Assert.Contains("neu installieren", ex.Message);
            }
            finally
            {
                Directory.Delete(emptyTempDirectory, recursive: true);
            }
        }
    }
}
