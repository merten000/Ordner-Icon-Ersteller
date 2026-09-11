using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace OrdnerIconErsteller
{
    public class MainForm : Form
    {
        private readonly TextBox _txtFolder = new() { ReadOnly = true };
        private readonly TextBox _txtImage = new() { ReadOnly = true };
        private readonly Button _btnBrowseFolder = new() { Text = "Ordner wählen..." };
        private readonly Button _btnBrowseImage = new() { Text = "Bild wählen..." };
        private readonly Button _btnSetIcon = new() { Text = "Icon setzen" };
        private readonly Button _btnRemoveIcon = new() { Text = "Icon entfernen" };
        private readonly Button _btnRegisterMenu = new() { Text = "Ins Rechtsklick-Menü integrieren" };
        private readonly Button _btnUnregisterMenu = new() { Text = "Aus Rechtsklick-Menü entfernen" };
        private readonly Label _lblMenuStatus = new() { AutoSize = true };
        private readonly TextBox _txtLog = new()
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9f)
        };

        public MainForm(string? startFolder)
        {
            Text = "Ordner-Icon-Ersteller";
            Width = 640;
            Height = 480;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(560, 420);

            BuildLayout();
            WireEvents();
            RefreshMenuStatus();

            if (!string.IsNullOrEmpty(startFolder))
            {
                _txtFolder.Text = startFolder;
                Log($"Ordner aus Explorer-Kontextmenü übernommen: {startFolder}");
            }
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                Padding = new Padding(12),
                RowCount = 5
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // Zeile 1: Ordnerauswahl
            var folderPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true };
            folderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            folderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _txtFolder.Dock = DockStyle.Fill;
            _btnBrowseFolder.Width = 140;
            folderPanel.Controls.Add(new Label { Text = "Ordner:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 0, 0) }, 0, 0);
            folderPanel.SetColumnSpan(folderPanel.Controls[0], 2);
            folderPanel.Controls.Add(_txtFolder, 0, 1);
            folderPanel.Controls.Add(_btnBrowseFolder, 1, 1);

            // Zeile 2: Bildauswahl
            var imagePanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true };
            imagePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            imagePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _txtImage.Dock = DockStyle.Fill;
            _btnBrowseImage.Width = 140;
            imagePanel.Controls.Add(new Label { Text = "Bild für das Icon:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 0, 0) }, 0, 0);
            imagePanel.SetColumnSpan(imagePanel.Controls[0], 2);
            imagePanel.Controls.Add(_txtImage, 0, 1);
            imagePanel.Controls.Add(_btnBrowseImage, 1, 1);

            // Zeile 3: Icon setzen/entfernen
            var actionPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            _btnSetIcon.Width = 150;
            _btnRemoveIcon.Width = 150;
            actionPanel.Controls.Add(_btnSetIcon);
            actionPanel.Controls.Add(_btnRemoveIcon);

            // Zeile 4: Kontextmenü-Integration
            var menuGroup = new GroupBox { Text = "Explorer-Rechtsklick-Menü", Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(8) };
            var menuPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            _btnRegisterMenu.Width = 220;
            _btnUnregisterMenu.Width = 200;
            _lblMenuStatus.Margin = new Padding(10, 8, 0, 0);
            menuPanel.Controls.Add(_btnRegisterMenu);
            menuPanel.Controls.Add(_btnUnregisterMenu);
            menuPanel.Controls.Add(_lblMenuStatus);
            menuGroup.Controls.Add(menuPanel);

            // Zeile 5: Log
            _txtLog.Dock = DockStyle.Fill;

            root.Controls.Add(folderPanel, 0, 0);
            root.Controls.Add(imagePanel, 0, 1);
            root.Controls.Add(actionPanel, 0, 2);
            root.Controls.Add(menuGroup, 0, 3);
            root.Controls.Add(_txtLog, 0, 4);

            Controls.Add(root);
        }

        private void WireEvents()
        {
            _btnBrowseFolder.Click += (_, _) =>
            {
                using var dlg = new FolderBrowserDialog { Description = "Ordner auswählen, der ein neues Icon bekommen soll" };
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _txtFolder.Text = dlg.SelectedPath;
                }
            };

            _btnBrowseImage.Click += (_, _) =>
            {
                using var dlg = new OpenFileDialog
                {
                    Title = "Bild für das Ordner-Icon auswählen",
                    Filter = "Bilder (*.png;*.jpg;*.jpeg;*.bmp;*.ico)|*.png;*.jpg;*.jpeg;*.bmp;*.ico|Alle Dateien (*.*)|*.*"
                };
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    _txtImage.Text = dlg.FileName;
                }
            };

            _btnSetIcon.Click += (_, _) => SetIcon();
            _btnRemoveIcon.Click += (_, _) => RemoveIcon();
            _btnRegisterMenu.Click += (_, _) => RegisterMenu();
            _btnUnregisterMenu.Click += (_, _) => UnregisterMenu();
        }

        private void SetIcon()
        {
            if (string.IsNullOrWhiteSpace(_txtFolder.Text) || !Directory.Exists(_txtFolder.Text))
            {
                MessageBox.Show(this, "Bitte zuerst einen gültigen Ordner auswählen.", "Ordner fehlt",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(_txtImage.Text) || !File.Exists(_txtImage.Text))
            {
                MessageBox.Show(this, "Bitte zuerst ein gültiges Bild auswählen.", "Bild fehlt",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                FolderIconService.SetFolderIcon(_txtFolder.Text, _txtImage.Text);
                Log($"Icon gesetzt für: {_txtFolder.Text}");
                MessageBox.Show(this, "Icon wurde gesetzt. Falls der Explorer es nicht sofort anzeigt, Ansicht kurz aktualisieren (F5).",
                    "Fertig", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log($"FEHLER beim Setzen des Icons: {ex.Message}");
                MessageBox.Show(this, $"Fehler beim Setzen des Icons:\n{ex.Message}", "Fehler",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RemoveIcon()
        {
            if (string.IsNullOrWhiteSpace(_txtFolder.Text) || !Directory.Exists(_txtFolder.Text))
            {
                MessageBox.Show(this, "Bitte zuerst einen gültigen Ordner auswählen.", "Ordner fehlt",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                FolderIconService.RemoveFolderIcon(_txtFolder.Text);
                Log($"Icon entfernt von: {_txtFolder.Text}");
            }
            catch (Exception ex)
            {
                Log($"FEHLER beim Entfernen des Icons: {ex.Message}");
                MessageBox.Show(this, $"Fehler beim Entfernen des Icons:\n{ex.Message}", "Fehler",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RegisterMenu()
        {
            try
            {
                ContextMenuService.Register();
                Log("Rechtsklick-Menü-Eintrag wurde eingetragen (nur für diesen Benutzer, keine Admin-Rechte nötig).");
                RefreshMenuStatus();
            }
            catch (Exception ex)
            {
                Log($"FEHLER beim Eintragen des Menüs: {ex.Message}");
                MessageBox.Show(this, $"Fehler beim Eintragen ins Kontextmenü:\n{ex.Message}", "Fehler",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UnregisterMenu()
        {
            try
            {
                ContextMenuService.Unregister();
                Log("Rechtsklick-Menü-Eintrag wurde entfernt.");
                RefreshMenuStatus();
            }
            catch (Exception ex)
            {
                Log($"FEHLER beim Entfernen des Menüs: {ex.Message}");
                MessageBox.Show(this, $"Fehler beim Entfernen aus dem Kontextmenü:\n{ex.Message}", "Fehler",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshMenuStatus()
        {
            _lblMenuStatus.Text = ContextMenuService.IsRegistered()
                ? "Status: aktuell im Kontextmenü eingetragen"
                : "Status: nicht eingetragen";
        }

        private void Log(string message)
        {
            _txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }
    }
}
