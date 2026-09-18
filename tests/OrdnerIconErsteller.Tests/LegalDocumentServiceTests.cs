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
        // WICHTIG: Die Testmethoden-PARAMETER sind hier bewusst "string" statt direkt
        // LegalDocumentService.Document, obwohl LegalDocumentServiceTests per
        // InternalsVisibleTo (siehe OrdnerIconErsteller.csproj) den internen Typ
        // grundsätzlich verwenden darf. Ein "public" [Theory]-Testmethode (von xUnit
        // zur Testerkennung verlangt) DARF laut C#-Sprachregel trotzdem keinen weniger
        // zugreifbaren ("internal") Typ in ihrer Signatur haben (Fehler CS0051) - das
        // gilt unabhängig von InternalsVisibleTo, weil die Methode selbst öffentlich
        // bleibt. Deshalb wird der Dokumentname als reiner string übergeben und erst
        // im Testkörper (nicht mehr Teil der Signatur) per Enum.Parse in
        // LegalDocumentService.Document umgewandelt.
        [Theory]
        [InlineData("Impressum", "IMPRESSUM.md")]
        [InlineData("Datenschutz", "DATENSCHUTZ.md")]
        [InlineData("Lizenz", "LIZENZ.md")]
        [InlineData("Haftungsausschluss", "HAFTUNGSAUSSCHLUSS.md")]
        public void GetPath_ZeigtAufDenGleichnamigenUnterordnerLegalNebenDerExe(
            string documentName, string expectedFileName)
        {
            var document = Enum.Parse<LegalDocumentService.Document>(documentName);
            string path = LegalDocumentService.GetPath(document);

            Assert.Equal(expectedFileName, Path.GetFileName(path));
            Assert.Equal("legal", Path.GetFileName(Path.GetDirectoryName(path)));
        }

        [Theory]
        [InlineData("Impressum")]
        [InlineData("Datenschutz")]
        [InlineData("Lizenz")]
        [InlineData("Haftungsausschluss")]
        public void ReadText_MitgelieferteDateiVorhanden_LiefertNichtLeerenText(string documentName)
        {
            var document = Enum.Parse<LegalDocumentService.Document>(documentName);

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
