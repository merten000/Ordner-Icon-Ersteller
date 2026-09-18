using System.Drawing;
using System.Windows.Forms;

namespace OrdnerIconErsteller
{
    /// <summary>
    /// Einfaches, schreibgeschütztes Anzeigefenster für eines der mitgelieferten
    /// rechtlichen Dokumente (siehe <see cref="LegalDocumentService"/>). Zeigt den
    /// Text direkt im Programm an, statt eine externe Anwendung (z.B. Notepad)
    /// als eigenen Prozess zu starten - siehe Projektregel, keine unnötigen
    /// Prozessaufrufe vorzunehmen.
    /// </summary>
    internal sealed class LegalDocumentViewerForm : Form
    {
        public LegalDocumentViewerForm(string title, string content)
        {
            Text = title;
            Width = 640;
            Height = 520;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = true;
            ShowInTaskbar = false;

            var textBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10f),
                Text = content.Replace("\n", "\r\n").Replace("\r\r\n", "\r\n"),
            };

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                Padding = new Padding(8),
            };
            var closeButton = new Button { Text = "Schließen", Width = 100 };
            closeButton.Click += (_, _) => Close();
            buttonPanel.Controls.Add(closeButton);

            Controls.Add(textBox);
            Controls.Add(buttonPanel);
            AcceptButton = closeButton;
        }
    }
}
