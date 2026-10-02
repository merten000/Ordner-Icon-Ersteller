using System;
using System.Drawing;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OrdnerIconErsteller
{
    public class MainForm : Form
    {
        private readonly TextBox _txtFolder = new() { ReadOnly = true };
        private readonly TextBox _txtImage = new() { ReadOnly = true };
        private readonly Button _btnBrowseFolder = new ModernButton { Text = "Ordner wählen...", Glyph = Glyph.Folder };
        private readonly Button _btnBrowseImage = new ModernButton { Text = "Bild wählen...", Glyph = Glyph.Picture };
        private readonly Button _btnSetIcon = new ModernButton { Text = "Icon setzen", Kind = ButtonKind.Primary, Glyph = Glyph.Check, CornerColor = Theme.Background };
        private readonly Button _btnRemoveIcon = new ModernButton { Text = "Icon entfernen", Glyph = Glyph.Delete, CornerColor = Theme.Background };
        private readonly CheckBox _chkContextMenu = new() { Text = "Ins Rechtsklick-Menü integrieren", AutoSize = true };
        private readonly Label _lblMenuStatus = new() { AutoSize = true };
        // Merten-Feedback (v2.5.0-Test): gemeint war kein gemeinsames "Menü"-Dropdown,
        // sondern zwei eigene Knöpfe ("Einstellungen" links, "Rechtliches" rechts),
        // bei denen sich beim Klick jeweils darunter ein aufklappendes Menü öffnet -
        // also das ganz normale Windows-Standardverhalten eines Dropdown-Menüs unter
        // einem Knopf (ContextMenuStrip.Show), NICHT ein eigenes Popup-Fenster und
        // NICHT eine gemeinsame ComboBox. Die beiden ContextMenuStrip-Objekte werden
        // einmalig in BuildLayout() befüllt und danach nur noch wiederverwendet -
        // dadurch entfällt auch das Dispose-Problem der vorherigen Fenster-Lösung
        // (v2.4.0/v2.4.1), weil hier nichts pro Klick neu erzeugt/verworfen wird.
        private readonly Button _btnEinstellungen = new ModernButton { Text = "Einstellungen", Glyph = Glyph.Settings, TrailingGlyph = Glyph.Chevron, CornerColor = Theme.Background };
        private readonly Button _btnRechtliches = new ModernButton { Text = "Rechtliches", Glyph = Glyph.Page, TrailingGlyph = Glyph.Chevron, CornerColor = Theme.Background };
        private readonly ContextMenuStrip _settingsMenu = new();
        private readonly ContextMenuStrip _rechtlichesMenu = new();
        private readonly TextBox _txtAiPrompt = new();
        private readonly ComboBox _cmbAiProvider = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly TextBox _txtAiKey = new() { UseSystemPasswordChar = true };
        private readonly CheckBox _chkShowAiKey = new() { Text = "anzeigen" };
        private readonly Button _btnAiSaveKey = new ModernButton { Text = "Speichern", Glyph = Glyph.Save };
        private readonly Button _btnAiDeleteKey = new ModernButton { Text = "Löschen", Glyph = Glyph.Delete };
        private readonly Label _lblAiKeyStatus = new StatusLabel();
        private readonly Button _btnAiGenerate = new ModernButton { Text = "Icon per KI erzeugen", Kind = ButtonKind.Primary, Glyph = Glyph.Star };
        // Runde 2 (Befund F-001): kontrollierte Abbruchmoeglichkeit fuer laufende KI-Arbeit.
        private readonly Button _btnAiCancel = new ModernButton { Text = "Abbrechen", Glyph = Glyph.Cancel, Enabled = false };
        private readonly TextBox _txtLog = new()
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9f)
        };

        // Phase der aktuell laufenden Operation (Runde 2, Befund F-001; vorher nur ein
        // bool "_aiOperationInProgress", das beim Schliessen pauschal alles blockierte).
        //  - Cancellable: Ordner-Scan, Zustimmung, Netzwerkanfrage an den KI-Anbieter. Hier
        //    ist im Zielordner noch nichts veraendert, ein Abbruch ueber den "Abbrechen"-Knopf
        //    oder beim Schliessen ist jederzeit gefahrlos moeglich.
        //  - Committing: Dateischritt im Zielordner (Icon setzen/entfernen bzw. erzeugtes
        //    Bild schreiben und Icon setzen). Diese Transaktion in FolderIconService hat ein
        //    eigenes Rollback und wird bewusst nicht mittendrin abgebrochen - das Schliessen
        //    durch den Benutzer wird deshalb in dieser (kurzen) Phase abgewiesen, bei
        //    Windows-Herunterfahren/Task-Manager wird begrenzt auf ihr Ende gewartet
        //    (ChatGPT-Gegenpruefung von v2.1.2, Freigabeblocker 3, bleibt damit erfuellt).
        // Die Entscheidung beim Schliessen trifft OperationGuard.DecideClose.
        private OperationPhase _phase = OperationPhase.Idle;
        private CancellationTokenSource? _aiCts;
        private Task? _commitTask;
        private bool _closeAfterOperation;

        // Runde 3 (Befund F-008): rein lesende Dateisystemzugriffe der abbrechbaren Phase (Ordner-Scan,
        // Sicherungs-Diagnose) laufen ueber dieses Gate, damit ein Abbruch/Schliessen auch dann sofort
        // wirkt, wenn ein einzelner Dateisystemzugriff haengt. Es ist hoechstens eine zurueckgelassene
        // Arbeit moeglich; eine neue KI-Operation wird abgelehnt, solange sie noch haengt.
        private readonly AbandonableWorkGate _readGate = new();

        public MainForm(string? startFolder)
        {
            Text = "Ordner-Icon-Ersteller";
            // Oberflaechen-Modernisierung: Segoe UI als ruhige Standardschrift und DPI-Skalierung,
            // damit feste Knopfbreiten bei 125 %/150 % Windows-Skalierung mitwachsen (Program.cs
            // schaltet SystemAware-DPI ein, ohne Skalierung blieben die Pixelmasse zu klein).
            Font = new Font("Segoe UI", 9f);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Width = 760;
            Height = 720;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(680, 480);
            BackColor = Theme.Background;

            BuildLayout();
            WireEvents();
            RefreshMenuStatus();
            RefreshAiKeyField();

            if (!string.IsNullOrEmpty(startFolder))
            {
                _txtFolder.Text = startFolder;
                // Pruefbericht v2.7.3 (Gesamtdurchlauf), Freigabeblocker 3: kein vollstaendiger
                // Ordnerpfad mehr im Log (siehe DATENSCHUTZ.md). Der Pfad steht ohnehin bereits
                // sichtbar im Feld "Ordner" direkt darueber.
                Log("Ordner aus Explorer-Kontextmenü übernommen.");
            }
        }

        // Gemeinsame Mass- und Stilwerte der Oberflaeche (Abstaende in 96-DPI-Pixeln, werden ueber
        // AutoScaleMode.Dpi mitskaliert).
        private const int SectionGap = 12;
        private const int ButtonHeight = 34;

        private static Label MakeCaption(string text) => new Label
        {
            Text = text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            ForeColor = Theme.Text,
            Margin = new Padding(0, 0, 0, 4)
        };

        private static void StyleButton(Button button, int width, bool primary = false)
        {
            button.Width = width;
            button.Height = ButtonHeight;
            button.Margin = new Padding(0, 0, 8, 0);
            if (primary)
            {
                button.Font = new Font(button.Font, FontStyle.Bold);
            }
        }

        // Eingabezeile einer Karte: abgerundetes Eingabefeld links, Knopf rechts.
        private static TableLayoutPanel MakeInputRow(Control input, Control button)
        {
            var row = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, AutoSize = true };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            input.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            input.Margin = new Padding(0, 0, 8, 0);
            button.Margin = new Padding(0);
            row.Controls.Add(input, 0, 0);
            row.Controls.Add(button, 1, 0);
            return row;
        }

        private void BuildLayout()
        {
            // Aufbau (von oben nach unten):
            //   Kopfzeile   Titel + Kurzbeschreibung links, "Einstellungen"/"Rechtliches" rechts
            //               (aufklappende Menues direkt unter dem jeweiligen Knopf - Standard-
            //               Windows-Verhalten, siehe WireEvents)
            //   Abschnitt   "Ordner und Bild"
            //   Aktionen    "Icon setzen" (hervorgehoben) / "Icon entfernen"
            //   Abschnitt   "Icon per KI erzeugen (optional)"
            //   Abschnitt   "Protokoll" (nimmt den restlichen Platz)
            // Nur das Layout wurde ueberarbeitet; Steuerelemente, Ereignisse und Logik sind unveraendert.
            var content = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                Padding = new Padding(16, 12, 16, 16),
                RowCount = 5
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // ---- Karte "Ziel-Ordner"
            StyleButton(_btnBrowseFolder, 150);
            var folderCard = new CardPanel("Ziel-Ordner", Glyph.Folder);
            _txtFolder.PlaceholderText = "Noch kein Ordner gewählt";
            folderCard.SetBody(MakeInputRow(new InputBox(_txtFolder, ButtonHeight), _btnBrowseFolder));

            // ---- Karte "Bild für das Icon"
            StyleButton(_btnBrowseImage, 150);
            var imageCard = new CardPanel("Bild für das Icon", Glyph.Picture, "PNG, JPG, BMP oder ICO");
            _txtImage.PlaceholderText = "Noch kein Bild gewählt";
            imageCard.SetBody(MakeInputRow(new InputBox(_txtImage, ButtonHeight), _btnBrowseImage));

            // ---- Aktionen: Icon setzen (Hauptaktion, blau) / entfernen (zurueckhaltend)
            var actionPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0, 0, 0, 12)
            };
            StyleButton(_btnSetIcon, 170, primary: true);
            StyleButton(_btnRemoveIcon, 170);
            actionPanel.Controls.Add(_btnSetIcon);
            actionPanel.Controls.Add(_btnRemoveIcon);

            // ---- Karte "Icon per KI erzeugen (optional)"
            var aiCard = new CardPanel("Icon per KI erzeugen (optional)", Glyph.Star);
            var aiLayout = new TableLayoutPanel { ColumnCount = 1, AutoSize = true };
            aiLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // Anbieter + API-Schluessel direkt im Hauptfenster (bewusst kein separater Dialog).
            var aiKeyPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 2, AutoSize = true };
            aiKeyPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            aiKeyPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            aiKeyPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            aiKeyPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            aiKeyPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            aiKeyPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            aiKeyPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _cmbAiProvider.Width = 120;
            _cmbAiProvider.Anchor = AnchorStyles.Left;
            _cmbAiProvider.Margin = new Padding(0, 0, 8, 0);
            _cmbAiProvider.FlatStyle = FlatStyle.Flat;
            _cmbAiProvider.BackColor = Theme.Card;
            _cmbAiProvider.Items.Add("OpenAI");
            _cmbAiProvider.Items.Add("Google");
            _cmbAiProvider.SelectedIndex = 0;
            _txtAiKey.PlaceholderText = "API-Schlüssel für den gewählten Anbieter";
            // Auge im Schluesselfeld: nur eine zweite Bedienmoeglichkeit fuer das vorhandene
            // Kontrollkaestchen "anzeigen" (keine neue Funktion).
            var eyeButton = new ModernButton { Kind = ButtonKind.Ghost, Glyph = Glyph.Eye, Width = 28, Height = 26, TabStop = false };
            eyeButton.Click += (_, _) => _chkShowAiKey.Checked = !_chkShowAiKey.Checked;
            var keyInput = new InputBox(_txtAiKey, ButtonHeight) { Adornment = eyeButton };
            keyInput.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            keyInput.Margin = new Padding(0, 0, 8, 0);
            _chkShowAiKey.AutoSize = true;
            _chkShowAiKey.Anchor = AnchorStyles.Left;
            _chkShowAiKey.Margin = new Padding(0, 0, 12, 0);
            StyleButton(_btnAiSaveKey, 110);
            StyleButton(_btnAiDeleteKey, 100);
            _btnAiDeleteKey.Margin = new Padding(0);
            var providerCaption = MakeCaption("Anbieter:");
            var keyCaption = MakeCaption("API-Schlüssel für den gewählten Anbieter:");
            aiKeyPanel.Controls.Add(providerCaption, 0, 0);
            aiKeyPanel.Controls.Add(keyCaption, 1, 0);
            aiKeyPanel.SetColumnSpan(keyCaption, 4);
            aiKeyPanel.Controls.Add(_cmbAiProvider, 0, 1);
            aiKeyPanel.Controls.Add(keyInput, 1, 1);
            aiKeyPanel.Controls.Add(_chkShowAiKey, 2, 1);
            aiKeyPanel.Controls.Add(_btnAiSaveKey, 3, 1);
            aiKeyPanel.Controls.Add(_btnAiDeleteKey, 4, 1);

            _lblAiKeyStatus.Anchor = AnchorStyles.Left;
            _lblAiKeyStatus.ForeColor = Theme.Text;
            _lblAiKeyStatus.Margin = new Padding(0, 10, 0, 10);

            var promptCaption = MakeCaption("Beschreibung (Prompt) – optional:");
            _txtAiPrompt.PlaceholderText = "z. B. \"ein minimalistisches blaues Wolken-Symbol\" - leer lassen, damit der Ordnerinhalt selbst als Grundlage dient";
            var promptInput = new InputBox(_txtAiPrompt, ButtonHeight)
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Margin = new Padding(0, 0, 0, 12)
            };

            var aiActionPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            StyleButton(_btnAiGenerate, 200, primary: true);
            StyleButton(_btnAiCancel, 120);
            aiActionPanel.Controls.Add(_btnAiGenerate);
            aiActionPanel.Controls.Add(_btnAiCancel);

            aiLayout.Controls.Add(aiKeyPanel, 0, 0);
            aiLayout.Controls.Add(_lblAiKeyStatus, 0, 1);
            aiLayout.Controls.Add(promptCaption, 0, 2);
            aiLayout.Controls.Add(promptInput, 0, 3);
            aiLayout.Controls.Add(aiActionPanel, 0, 4);
            aiCard.SetBody(aiLayout);

            // ---- Karte "Ausgabeprotokoll" (nimmt den restlichen Platz)
            var logCard = new CardPanel("Ausgabeprotokoll", Glyph.Page)
            {
                MinimumSize = new Size(0, 150),
                Margin = new Padding(0)
            };
            _txtLog.Multiline = true;
            logCard.SetBody(new InputBox(_txtLog, 80));

            // ---- Kopfzeile: Titel links, "Einstellungen"/"Rechtliches" rechts
            // Kopfzeile und Inhalt liegen in EINEM gemeinsamen Raster (root), damit das Ergebnis nicht von
            // der Reihenfolge angedockter Steuerelemente abhaengt.
            var topBar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, AutoSize = true };
            topBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            topBar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            topBar.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));
            var separator = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0), BackColor = Theme.Border };
            var headerLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                AutoSize = true,
                Padding = new Padding(16, 10, 16, 10)
            };
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            headerLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var titleTexts = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                Margin = new Padding(0)
            };
            titleTexts.Controls.Add(new Label
            {
                Text = "Ordner-Icon-Ersteller",
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 14f),
                ForeColor = Theme.Text,
                Margin = new Padding(0)
            });
            titleTexts.Controls.Add(new Label
            {
                Text = "Ordner-Icons setzen – mit eigenem Bild oder per KI",
                AutoSize = true,
                ForeColor = Theme.Muted,
                Margin = new Padding(1, 0, 0, 0)
            });
            var titleLayout = new FlowLayoutPanel
            {
                Anchor = AnchorStyles.Left,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true
            };
            titleLayout.Controls.Add(new IconBadge(Glyph.Picture, 40));
            titleLayout.Controls.Add(titleTexts);

            var topBarButtons = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Anchor = AnchorStyles.Right
            };
            StyleButton(_btnEinstellungen, 140);
            StyleButton(_btnRechtliches, 130);
            _btnRechtliches.Margin = new Padding(0);
            topBarButtons.Controls.Add(_btnEinstellungen);
            topBarButtons.Controls.Add(_btnRechtliches);

            headerLayout.Controls.Add(titleLayout, 0, 0);
            headerLayout.Controls.Add(topBarButtons, 1, 0);
            topBar.Controls.Add(headerLayout, 0, 0);
            topBar.Controls.Add(separator, 0, 1);

            // Inhalt des "Einstellungen"-Dropdowns: Ueberschrift, Rechtsklick-Menue-Schalter und Status.
            // ContextMenuStrip/ToolStripControlHost werden nur EINMAL hier aufgebaut und danach fuer
            // die gesamte Laufzeit wiederverwendet (nichts wird pro Klick neu erzeugt oder disposed).
            _lblMenuStatus.ForeColor = SystemColors.GrayText;
            _settingsMenu.Items.Add(new ToolStripLabel("Explorer-Rechtsklick-Menü")
            {
                Font = new Font("Segoe UI Semibold", 9f),
                Margin = new Padding(8, 4, 8, 0)
            });
            _chkContextMenu.Margin = new Padding(4, 2, 4, 2);
            _lblMenuStatus.Margin = new Padding(4, 0, 4, 4);
            _settingsMenu.Items.Add(new ToolStripControlHost(_chkContextMenu));
            _settingsMenu.Items.Add(new ToolStripControlHost(_lblMenuStatus));

            // Inhalt des "Rechtliches"-Dropdowns: die mitgelieferten Dokumente als normale Menuepunkte.
            _rechtlichesMenu.Items.Add("Impressum", null, (_, _) => ShowLegalDocument(LegalDocumentService.Document.Impressum));
            _rechtlichesMenu.Items.Add("Datenschutz", null, (_, _) => ShowLegalDocument(LegalDocumentService.Document.Datenschutz));
            _rechtlichesMenu.Items.Add("Lizenz", null, (_, _) => ShowLegalDocument(LegalDocumentService.Document.Lizenz));
            _rechtlichesMenu.Items.Add("Haftungsausschluss", null, (_, _) => ShowLegalDocument(LegalDocumentService.Document.Haftungsausschluss));
            // Mitgelieferte .NET-Runtime-Rechtstexte (siehe LegalDocumentService).
            _rechtlichesMenu.Items.Add(new ToolStripSeparator());
            _rechtlichesMenu.Items.Add("Drittanbieterhinweise (.NET)", null, (_, _) => ShowLegalDocument(LegalDocumentService.Document.Drittanbieterhinweise));
            _rechtlichesMenu.Items.Add(".NET-Lizenz", null, (_, _) => ShowLegalDocument(LegalDocumentService.Document.DotNetLizenz));

            content.Controls.Add(folderCard, 0, 0);
            content.Controls.Add(imageCard, 0, 1);
            content.Controls.Add(actionPanel, 0, 2);
            content.Controls.Add(aiCard, 0, 3);
            content.Controls.Add(logCard, 0, 4);

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.Controls.Add(topBar, 0, 0);
            root.Controls.Add(content, 0, 1);

            // Kleine Fenster: Inhalt bleibt vollstaendig erreichbar (Scrollleiste statt Abschneiden, F-001).
            // Die Mindest-Scrollhoehe wird nach dem DPI-Skalieren aus dem echten Layout berechnet
            // (keine feste Pixelzahl, F-002/F-003), siehe UpdateScrollBedarf().
            scrollContent = content;
            scrollLog = logCard;
            scrollHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            scrollHost.Controls.Add(root);
            Controls.Add(scrollHost);
            Shown += (s, e) => UpdateScrollBedarf();
            scrollHost.SizeChanged += (s, e) => UpdateScrollBedarf();
        }

        private Panel scrollHost = null!;
        private TableLayoutPanel scrollContent = null!;
        private Control scrollLog = null!;

        // Tatsaechlicher Platzbedarf: Unterkante des Protokollbereichs in seiner Mindesthoehe
        // plus unteres Content-Padding (F-003). GetPreferredSize des Root liefert hier zu wenig,
        // weil die Protokollzeile prozentual ist.
        private void UpdateScrollBedarf()
        {
            int bedarf = scrollContent.Top + scrollLog.Top + scrollLog.MinimumSize.Height + scrollContent.Padding.Bottom;
            if (scrollHost.AutoScrollMinSize.Height != bedarf)
                scrollHost.AutoScrollMinSize = new Size(0, bedarf);
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

            _btnSetIcon.Click += async (_, _) => await SetIconAsync();
            _btnRemoveIcon.Click += async (_, _) => await RemoveIconAsync();
            _chkContextMenu.CheckedChanged += (_, _) => OnContextMenuCheckedChanged();
            _btnAiGenerate.Click += async (_, _) => await GenerateAiIconAsync();
            _btnAiCancel.Click += (_, _) => RequestAiCancel();
            _cmbAiProvider.SelectedIndexChanged += (_, _) => RefreshAiKeyField();
            _chkShowAiKey.CheckedChanged += (_, _) => _txtAiKey.UseSystemPasswordChar = !_chkShowAiKey.Checked;
            _btnAiSaveKey.Click += (_, _) => SaveAiKey();
            _btnAiDeleteKey.Click += (_, _) => DeleteAiKey();

            // Klassisches aufklappendes Dropdown-Menü direkt unter dem jeweiligen
            // Knopf (Merten-Feedback nach v2.5.0-Test) - der Menü-Inhalt selbst
            // wurde bereits einmalig in BuildLayout() befüllt.
            _btnEinstellungen.Click += (_, _) => _settingsMenu.Show(_btnEinstellungen, new Point(0, _btnEinstellungen.Height));
            _btnRechtliches.Click += (_, _) => _rechtlichesMenu.Show(_btnRechtliches, new Point(0, _btnRechtliches.Height));
        }

        /// <summary>
        /// Liest das angeforderte Dokument über <see cref="LegalDocumentService"/> ein
        /// und zeigt es in einem einfachen Anzeigefenster an. Fehlt die Datei (z.B.
        /// unvollständige Installation), erscheint statt eines unklaren Absturzes eine
        /// verständliche Fehlermeldung (Merten-Feedback: "Fehlt eine Datei, soll eine
        /// verständliche Meldung erscheinen").
        /// </summary>
        private void ShowLegalDocument(LegalDocumentService.Document document)
        {
            string title = LegalDocumentService.TitleFor(document);
            string content;
            try
            {
                content = LegalDocumentService.ReadText(document);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Dokument nicht gefunden",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var viewer = new LegalDocumentViewerForm(title, content);
            viewer.ShowDialog(this);
        }

        // Runde 2 (Befund F-003): "Icon setzen"/"Icon entfernen" liefen bisher synchron im
        // UI-Thread - ein grosses Bild oder ein langsamer Datentraeger liess das Fenster
        // einfrieren ("Keine Rueckmeldung"), und der Benutzer beendete es evtl. hart. Jetzt
        // laeuft der Dateischritt im Thread-Pool (Ordner/Bild werden vorher festgehalten),
        // die Bedienelemente sind waehrenddessen gesperrt. Die Dateitransaktion selbst
        // (Rollback, Sperre) ist unveraendert; sie wird nicht abgebrochen, siehe _phase.
        private async Task SetIconAsync()
        {
            if (_phase != OperationPhase.Idle)
            {
                return;
            }

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

            string folder = _txtFolder.Text;
            string image = _txtImage.Text;

            BeginOperation(OperationPhase.Committing);
            try
            {
                if (!await PreflightOrphanBackupsAsync(folder))
                {
                    return;
                }

                _commitTask = Task.Run(() => FolderIconService.SetFolderIcon(folder, image));
                await _commitTask;
                EndOperation();
                // Pruefbericht v2.7.3 (Gesamtdurchlauf), Freigabeblocker 3: kein vollstaendiger
                // Ordnerpfad mehr im Log (siehe DATENSCHUTZ.md). Der Pfad steht ohnehin bereits
                // sichtbar im Feld "Ordner" direkt darueber.
                Log("Icon wurde gesetzt.");
                MessageBox.Show(this, "Icon wurde gesetzt. Falls der Explorer es nicht sofort anzeigt, Ansicht kurz aktualisieren (F5).",
                    "Fertig", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                EndOperation();
                // Pruefbericht v2.7.3 (Gesamtdurchlauf), Freigabeblocker 3: FolderIconService-
                // Ausnahmen enthalten haeufig den vollstaendigen Ordner-/Dateipfad (siehe z.B.
                // DirectoryNotFoundException/FileNotFoundException dort) - ex.Message deshalb
                // nicht mehr ins Log schreiben. Der Dialog unten zeigt die Detailmeldung dem
                // Benutzer weiterhin direkt an, landet aber nicht im kopierbaren Log-Fenster.
                Log("FEHLER beim Setzen des Icons (siehe Fehlermeldung im Dialogfenster).");
                MessageBox.Show(this, $"Fehler beim Setzen des Icons:\n{ex.Message}", "Fehler",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                EndOperation();
            }
        }

        private async Task RemoveIconAsync()
        {
            if (_phase != OperationPhase.Idle)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_txtFolder.Text) || !Directory.Exists(_txtFolder.Text))
            {
                MessageBox.Show(this, "Bitte zuerst einen gültigen Ordner auswählen.", "Ordner fehlt",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string folder = _txtFolder.Text;

            BeginOperation(OperationPhase.Committing);
            try
            {
                _commitTask = Task.Run(() => FolderIconService.RemoveFolderIcon(folder));
                await _commitTask;
                EndOperation();
                Log("Icon wurde entfernt.");
            }
            catch (Exception ex)
            {
                EndOperation();
                Log("FEHLER beim Entfernen des Icons (siehe Fehlermeldung im Dialogfenster).");
                MessageBox.Show(this, $"Fehler beim Entfernen des Icons:\n{ex.Message}", "Fehler",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                EndOperation();
            }
        }

        /// <summary>
        /// Runde 2 (Befund F-002): prueft VOR dem eigentlichen Vorgang (und damit bei KI-Icons
        /// vor jeder kostenpflichtigen Anfrage), ob in einem vorherigen, hart beendeten Lauf
        /// eine Sicherungsdatei ohne Statusdatei liegen geblieben ist. Liefert true, wenn der
        /// Vorgang fortgesetzt werden darf. Ist die Sicherung nachweislich unkritisch (Byte
        /// fuer Byte identisch mit der vorhandenen Datei), wird dem Benutzer angeboten, sie
        /// beiseite zu legen (umbenennen, NICHT loeschen); sonst wird nur erklaert und
        /// abgebrochen. Es wird nie automatisch geloescht oder geraten.
        /// </summary>
        private async Task<bool> PreflightOrphanBackupsAsync(string folder, CancellationToken cancellationToken = default)
        {
            // Die Diagnose liest nur. In der abbrechbaren Phase (KI-Icon) laeuft sie ueber das Gate
            // (Runde 3, Befund F-008), damit ein haengender Zugriff den Abbruch nicht aufhaelt. Die
            // manuellen Wege (Committing-Phase, kein Token) bleiben unveraendert.
            FolderIconService.OrphanBackupDiagnosis diagnosis = cancellationToken.CanBeCanceled
                ? await _readGate.RunAsync(_ => FolderIconService.DiagnoseOrphanBackups(folder), cancellationToken)
                : await Task.Run(() => FolderIconService.DiagnoseOrphanBackups(folder));

            if (diagnosis.State == FolderIconService.OrphanBackupState.NoProblem)
            {
                return true;
            }

            if (diagnosis.State == FolderIconService.OrphanBackupState.NeedsManualReview)
            {
                Log("Übrig gebliebene Sicherungsdatei gefunden - manuelle Prüfung nötig, nichts verändert.");
                MessageBox.Show(this, diagnosis.Explanation + "\n\nDer Vorgang wurde nicht gestartet.",
                    "Manuelle Prüfung nötig", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            DialogResult answer = MessageBox.Show(this,
                diagnosis.Explanation + "\n\n" +
                "Soll die Sicherungsdatei beiseite gelegt werden (sie wird nur umbenannt, NICHT gelöscht) " +
                "und der Vorgang danach fortgesetzt werden?",
                "Übrig gebliebene Sicherung", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes)
            {
                Log("Übrig gebliebene Sicherungsdatei nicht angefasst - Vorgang nicht gestartet.");
                return false;
            }

            IReadOnlyList<string> moved = await Task.Run(() => FolderIconService.SetAsideOrphanBackups(folder));
            Log($"{moved.Count} übrig gebliebene Sicherungsdatei(en) beiseite gelegt (umbenannt, nicht gelöscht).");
            return true;
        }

        // Verhindert, dass RefreshMenuStatus() (das den Haken programmatisch auf den
        // tatsächlichen Registry-Zustand setzt) selbst wieder OnContextMenuCheckedChanged
        // auslöst und damit ungewollt ein zweites Mal Register()/Unregister() aufruft.
        private bool _suppressContextMenuCheckedEvent;

        private void OnContextMenuCheckedChanged()
        {
            if (_suppressContextMenuCheckedEvent)
            {
                return;
            }

            if (_chkContextMenu.Checked)
            {
                try
                {
                    ContextMenuService.Register();
                    Log("Rechtsklick-Menü-Eintrag wurde eingetragen (nur für diesen Benutzer, keine Admin-Rechte nötig).");
                }
                catch (Exception ex)
                {
                    Log($"FEHLER beim Eintragen des Menüs: {ex.Message}");
                    MessageBox.Show(this, $"Fehler beim Eintragen ins Kontextmenü:\n{ex.Message}", "Fehler",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                try
                {
                    ContextMenuService.Unregister();
                    Log("Rechtsklick-Menü-Eintrag wurde entfernt.");
                }
                catch (Exception ex)
                {
                    Log($"FEHLER beim Entfernen des Menüs: {ex.Message}");
                    MessageBox.Show(this, $"Fehler beim Entfernen aus dem Kontextmenü:\n{ex.Message}", "Fehler",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            RefreshMenuStatus();
        }

        private bool IsGoogleSelected => _cmbAiProvider.SelectedIndex == 1;

        private void SaveAiKey()
        {
            string value = _txtAiKey.Text.Trim();
            if (string.IsNullOrEmpty(value) || value == PasswordPlaceholder)
            {
                MessageBox.Show(this, "Bitte einen gültigen API-Schlüssel eingeben.", "Schlüssel fehlt",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // WICHTIG: EnsureGiven() steht jetzt INNERHALB des try-Blocks (vorher
            // davor) - ein Fehler beim Speichern der Zustimmung selbst (z.B. ein
            // fehlgeschlagenes CreateSubKey in AiConsentService.SetGiven) wurde
            // sonst nicht kontrolliert abgefangen, sondern verließ den
            // Click-Handler ungehandelt (Pruefbericht Punkt 3).
            try
            {
                if (!AiConsentService.EnsureGiven(this))
                {
                    return;
                }

                if (IsGoogleSelected)
                {
                    ApiKeyStore.SaveGoogleKey(value);
                }
                else
                {
                    ApiKeyStore.SaveOpenAiKey(value);
                }

                RefreshAiKeyField();
                Log($"API-Schlüssel für {(IsGoogleSelected ? "Google" : "OpenAI")} gespeichert (im Windows-Anmeldeinformations-Manager).");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Fehler beim Speichern des Schlüssels:\n{ex.Message}", "Fehler",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DeleteAiKey()
        {
            try
            {
                if (IsGoogleSelected)
                {
                    ApiKeyStore.DeleteGoogleKey();
                }
                else
                {
                    ApiKeyStore.DeleteOpenAiKey();
                }

                RefreshAiKeyField();
                Log($"API-Schlüssel für {(IsGoogleSelected ? "Google" : "OpenAI")} gelöscht.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Fehler beim Löschen des Schlüssels:\n{ex.Message}", "Fehler",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private const string PasswordPlaceholder = "••••••••";

        private void RefreshAiKeyField()
        {
            // Pruefbericht v2.7.3 (Gesamtdurchlauf), weiterer Fehler 4: die werfenden
            // Has...Key()-Methoden wurden hier bewusst durch die nichtwerfenden Try...-
            // Varianten ersetzt, da dieser Aufruf auch direkt aus dem MainForm-Konstruktor
            // erfolgt - ein nicht erreichbarer Credential Manager (z.B. per Richtlinie
            // gesperrt) durfte den Programmstart nicht mehr ungefangen abbrechen lassen.
            bool couldCheck;
            bool hasKey;
            if (IsGoogleSelected)
            {
                couldCheck = ApiKeyStore.TryHasGoogleKey(out hasKey);
            }
            else
            {
                couldCheck = ApiKeyStore.TryHasOpenAiKey(out hasKey);
            }

            string providerLabel = IsGoogleSelected ? "Google" : "OpenAI";
            if (!couldCheck)
            {
                _txtAiKey.Text = string.Empty;
                _lblAiKeyStatus.Text = $"Status ({providerLabel}): Schlüsselstatus konnte nicht gelesen werden " +
                    "(Windows-Anmeldeinformations-Manager nicht erreichbar).";
                return;
            }

            _txtAiKey.Text = hasKey ? PasswordPlaceholder : string.Empty;
            _lblAiKeyStatus.Text = hasKey
                ? $"Status ({providerLabel}): Schlüssel hinterlegt"
                : $"Status ({providerLabel}): kein Schlüssel hinterlegt";
        }

        private async Task GenerateAiIconAsync()
        {
            // Pruefbericht v2.2.1, Freigabeblocker 1: vorher wurden die Bedienelemente
            // erst NACH dem (jetzt per Task.Run im Hintergrund laufenden) Ordner-Scan,
            // dem Referenzbild-Laden, der Zustimmung und der Namensberechnung gesperrt.
            // Waehrend ein langsamer Ordner/Netzlaufwerk durchsucht wurde, blieb der
            // Knopf aktiv - ein zweiter Klick konnte einen parallelen zweiten Ablauf
            // starten, der sich mit dem ersten gegenseitig ueber
            // SetAiOperationControlsEnabled/_phase entsperrt haette.
            // Deshalb jetzt: fruehzeitiger Ausstieg, falls bereits eine Operation laeuft
            // (Sicherheitsnetz - die deaktivierten Bedienelemente sollten das ohnehin
            // verhindern), und Sperre/Deaktivierung GANZ AM ANFANG setzen, noch vor dem
            // ersten await, mit Scan/Zustimmung/Netzwerkanfrage/Dateitransaktion
            // zusammen in einem einzigen try/finally.
            if (_phase != OperationPhase.Idle)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_txtFolder.Text) || !Directory.Exists(_txtFolder.Text))
            {
                MessageBox.Show(this, "Bitte zuerst einen gültigen Ordner auswählen.", "Ordner fehlt",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Ordner, Anbieter und Prompt EINMALIG hier festhalten und danach nur
            // noch diese lokalen Werte verwenden - NICHT mehr _txtFolder.Text usw.
            // Vorher wurde der Zielordner zweimal gelesen, sodass ein waehrend der
            // 20-120 Sekunden dauernden Anfrage geaenderter Ordner dazu fuehren
            // konnte, dass die PNG-Datei im urspruenglichen, das Icon aber im neu
            // gewaehlten Ordner landet.
            string targetFolder = _txtFolder.Text;
            AiImageProvider provider = _cmbAiProvider.SelectedIndex == 1 ? AiImageProvider.Google : AiImageProvider.OpenAi;
            string enteredPrompt = _txtAiPrompt.Text.Trim();

            // Runde 2 (Befund F-001): die Phase "abbrechbar" beginnt hier - bis kurz vor dem
            // Dateischritt kann der Benutzer ueber "Abbrechen" (oder beim Schliessen) kontrolliert
            // abbrechen; der Token wird an Ordner-Scan und Netzwerkanfrage durchgereicht.
            // Runde 3 (Befund F-008): haengt noch ein frueher abgebrochener, rein lesender Zugriff auf
            // einen Ordner im Hintergrund, wird keine neue Operation gestartet (sonst koennten sich bei
            // wiederholtem Abbrechen beliebig viele haengende Tasks ansammeln).
            if (_readGate.HasAbandonedWork)
            {
                MessageBox.Show(this,
                    "Ein früher abgebrochener Zugriff auf einen Ordner hängt noch (das Laufwerk antwortet nicht). " +
                    "Bitte warten, bis das Laufwerk wieder reagiert, oder das Programm neu starten.",
                    "Zugriff hängt noch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _aiCts = new CancellationTokenSource();
            CancellationToken cancellationToken = _aiCts.Token;
            BeginOperation(OperationPhase.Cancellable);
            try
            {
                // Runde 2 (Befund F-002): uebrig gebliebene Sicherungsdateien eines hart beendeten
                // frueheren Laufs VOR der (kostenpflichtigen) KI-Anfrage erkennen, nicht erst
                // danach beim Setzen des Icons.
                if (!await PreflightOrphanBackupsAsync(targetFolder, cancellationToken))
                {
                    return;
                }

                cancellationToken.ThrowIfCancellationRequested();

                // "Icon ohne Text" (Merten-Feedback nach v2.1.4): ist keine Beschreibung
                // eingegeben, wird nicht mehr abgebrochen, sondern der Zielordner selbst
                // durchsucht (Dateinamen als Kontext, ein evtl. vorhandenes, bereits
                // vollstaendig validiertes Bild als Referenz für Google) - siehe
                // FolderContentScanner.
                string prompt;
                AiIconService.ReferenceImage? referenceImage = null;
                if (string.IsNullOrWhiteSpace(enteredPrompt))
                {
                    FolderContentScanner.ScanResult scan;
                    try
                    {
                        // Referenzbild nur suchen, wenn der gewaehlte Anbieter es ueberhaupt
                        // entgegennimmt - siehe FolderContentScanner.Scan (Pruefbericht v2.2.0:
                        // "bei OpenAI behauptet der Prompt, ein Referenzbild werde mitgesendet,
                        // obwohl OpenAI es vollstaendig verwirft").
                        //
                        // Laeuft in einem Thread-Pool-Thread (Pruefbericht v2.2.0, weiterer
                        // Fehler: "Der Ordner-Scan läuft auf dem UI-Thread") - bei einem sehr
                        // grossen Ordner oder einem langsamen Netzlaufwerk wuerde ein
                        // synchroner Aufruf hier sonst das Fenster einfrieren lassen. Die
                        // Bedienelemente sind zu diesem Zeitpunkt (siehe oben) bereits gesperrt.
                        bool allowReferenceImage = provider == AiImageProvider.Google;
                        // Runde 3 (Befund F-008): ueber das Gate statt Task.Run(..., token) - dessen await wartet
                        // trotz Abbruch, bis ein haengender Dateisystemzugriff zurueckkehrt.
                        scan = await _readGate.RunAsync(
                            token => FolderContentScanner.Scan(targetFolder, allowReferenceImage, token), cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        // Vom Benutzer angeforderter Abbruch - kein Fehler (siehe aeusserer catch).
                        throw;
                    }
                    catch (Exception ex)
                    {
                        // Pruefbericht v2.7.3 (Gesamtdurchlauf), Freigabeblocker 3: ex.Message kann
                        // hier Datei-/Ordnernamen aus dem gescannten Ordner enthalten - nicht mehr
                        // ins Log schreiben (siehe DATENSCHUTZ.md).
                        Log("FEHLER beim Durchsuchen des Ordners für \"Icon ohne Text\" (siehe Fehlermeldung im Dialogfenster).");
                        MessageBox.Show(this, $"Der Ordner konnte nicht durchsucht werden:\n{ex.Message}", "Fehler",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    prompt = scan.ImplicitPrompt;

                    // Pruefbericht v2.2.1, weiterer Fehler 4: der Scanner hat das Bild zwar
                    // bereits vollstaendig geladen/validiert, aber noch NICHT tatsaechlich
                    // zur Uebertragung freigegeben - dafuer erst hier ausdruecklich den
                    // konkreten Dateinamen anzeigen und einzeln bestaetigen/ablehnen lassen.
                    // Nur bei Zustimmung wird das Bild tatsaechlich verwendet UND der
                    // Prompt-Text um den Referenz-Hinweis ergaenzt - sonst bleibt der Prompt
                    // unveraendert (Pruefbericht v2.2.1, weiterer Fehler 3: der Prompt darf
                    // nie ein Bild behaupten, das am Ende gar nicht mitgesendet wird).
                    if (scan.ReferenceImage is AiIconService.ReferenceImage validatedImage && scan.ReferenceImagePath is not null)
                    {
                        if (AiConsentService.ConfirmReferenceImage(this, scan.ReferenceImagePath))
                        {
                            referenceImage = validatedImage;
                            prompt = FolderContentScanner.WithReferenceImageMention(prompt);
                        }
                        else
                        {
                            // Pruefbericht v2.7.2, verbliebener Freigabeblocker: auch hier keinen
                            // vollstaendigen Dateipfad mehr loggen (siehe DATENSCHUTZ.md).
                            Log("Referenzbild auf Wunsch nicht mitgesendet.");
                        }
                    }

                    // Pruefbericht v2.7.1, Punkt 3: hier bewusst KEIN Prompt-/Zielordner-Text
                    // mehr ins Log schreiben (der volle Ersatz-Prompt kann bis zu 25 Datei-/
                    // Unterordnernamen enthalten) - nur Status, wie es DATENSCHUTZ.md
                    // ("kurze Statuszeile") tatsaechlich beschreibt.
                    Log("Kein Text eingegeben - Ordnerinhalt wird stattdessen als Grundlage verwendet" +
                        (referenceImage is not null ? " (mit Referenzbild aus dem Ordner)." : "."));
                }
                else
                {
                    prompt = enteredPrompt;
                }

                // Zusaetzliche Sicherheitspruefung unmittelbar vor der ersten
                // tatsaechlichen Netzwerkanfrage - falls z. B. ein Credential ausserhalb
                // der Oberflaeche angelegt wurde, ohne dass der Hinweis je bestaetigt
                // wurde. Innerhalb eines eigenen try/catch: ein Fehler beim Speichern
                // der Zustimmung selbst (z.B. ein fehlgeschlagenes CreateSubKey) wurde
                // vorher nicht kontrolliert abgefangen (Pruefbericht Punkt 3).
                try
                {
                    if (!AiConsentService.EnsureGiven(this))
                    {
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Log($"FEHLER beim Speichern der Zustimmung: {ex.Message}");
                    MessageBox.Show(this, $"Fehler beim Speichern der Zustimmung:\n{ex.Message}", "Fehler",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Das erzeugte Bild wird dauerhaft im Zielordner selbst gespeichert, damit
                // es als Quelle erhalten bleibt - genau wie bei einem manuell ausgewählten
                // Bild. Seit "Icon ohne Überschreiben" (Merten-Feedback nach v2.1.4) bekommt
                // JEDE Erzeugung einen eigenen, eindeutigen Dateinamen (Zeitstempel) statt
                // eine evtl. schon vorhandene Datei zu ersetzen - siehe
                // BuildUniqueAiIconImagePath. Ältere Versionen bleiben dadurch zum
                // Vergleichen im Ordner liegen, bis der Benutzer sie selbst löscht.
                // Nur ein WUNSCH-Dateiname - FolderIconService entscheidet die tatsaechliche
                // Eindeutigkeit endgueltig innerhalb seiner ordnerweiten Sperre (siehe dortige
                // Erlaeuterung, Pruefbericht v2.2.0 P1) und liefert unten den tatsaechlich
                // verwendeten Pfad zurueck.
                string preferredImagePath = BuildUniqueAiIconImagePath(targetFolder);

                // Pruefbericht v2.7.1, Punkt 3: nur Anbieter und Status protokollieren, nicht
                // den vollstaendigen Prompt oder den Zielordnerpfad (DATENSCHUTZ.md spricht
                // bewusst nur von einer "kurzen Statuszeile").
                Log($"KI-Icon wird erzeugt (Anbieter: {provider})...");

                // AiIconService liefert an dieser Stelle bereits vollstaendig als
                // gueltiges Bild dekodierte und in ein kanonisches PNG neu
                // encodierte Bytes zurueck (siehe dortige DecodeAndReencodeAsPng).
                byte[] imageBytes = await AiIconService.GenerateIconImageAsync(provider, prompt, referenceImage, cancellationToken);

                // Letzte Abbruchstelle: ab hier wird in den Zielordner geschrieben. Ein bis hierher
                // angeforderter Abbruch verwirft das (evtl. bereits bezahlte) Bild, ohne den Ordner
                // zu veraendern; danach laeuft die Transaktion bewusst bis zum Ende.
                cancellationToken.ThrowIfCancellationRequested();
                _phase = OperationPhase.Committing;
                _btnAiCancel.Enabled = false;

                // Schreiben der PNG-Datei UND das anschliessende Setzen des Icons
                // laufen jetzt als EINE Transaktion unter derselben
                // prozessuebergreifenden Ordnersperre wie beim manuellen
                // "Icon setzen"/"Icon entfernen" - siehe
                // FolderIconService.ReplaceGeneratedImageAndSetIcon. Das schliesst
                // sowohl den Fall ab, dass eine zweite Programminstanz denselben
                // Ordner gleichzeitig bearbeitet, als auch den Fall, dass ein
                // fehlgeschlagenes Wiederherstellen der alten Datei faelschlich als
                // "kann geloescht werden" behandelt wird - beides nutzt dieselben,
                // bereits mehrfach gepruefte Sicherungs-/Rollback-Bausteine wie
                // Icon.ico/desktop.ini. Läuft in einem Thread-Pool-Thread, weil es
                // sich um blockierende Datei-I/O (kein async I/O) handelt.
                Task<string> commitTask = Task.Run(
                    () => FolderIconService.ReplaceGeneratedImageAndSetIcon(targetFolder, imageBytes, preferredImagePath));
                _commitTask = commitTask;
                string generatedImagePath = await commitTask;
                EndOperation();

                _txtImage.Text = generatedImagePath;

                // Pruefbericht v2.7.2, verbliebener Freigabeblocker: kein vollstaendiger
                // Ziel-/Bildpfad mehr im Log (siehe DATENSCHUTZ.md, "Prompt- und
                // Ordnerinformationen erscheinen dort nicht"). Der Pfad steht ohnehin schon
                // sichtbar im Feld "Bild für das Icon" (_txtImage), das direkt darueber liegt.
                Log("KI-Icon wurde erfolgreich erzeugt und gesetzt.");
                MessageBox.Show(this, "Das von der KI erzeugte Icon wurde gesetzt und als Bilddatei im Ordner gespeichert. " +
                    "Falls der Explorer es nicht sofort anzeigt, Ansicht kurz aktualisieren (F5).",
                    "Fertig", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Runde 2 (Befund F-001): kontrollierter Abbruch durch den Benutzer. Im Zielordner
                // wurde nichts veraendert (siehe Phase "abbrechbar").
                EndOperation();
                Log("KI-Icon-Erzeugung wurde abgebrochen - im Ordner wurde nichts verändert.");
            }
            catch (Exception ex)
            {
                EndOperation();
                // Pruefbericht v2.7.3 (Gesamtdurchlauf), Freigabeblocker 3: ex.Message kann hier
                // die rohe Antwort des KI-Anbieters oder Datei-/Ordnernamen enthalten - nicht mehr
                // ins Log schreiben (siehe DATENSCHUTZ.md und AiIconService).
                Log("FEHLER bei der KI-Icon-Erzeugung (siehe Fehlermeldung im Dialogfenster).");
                MessageBox.Show(this, $"Fehler bei der KI-Icon-Erzeugung:\n{ex.Message}", "Fehler",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                EndOperation();
            }
        }

        /// <summary>
        /// Baut einen zum Zeitpunkt des Aufrufs noch nicht existierenden WUNSCH-
        /// Dateinamen für ein neu zu erzeugendes KI-Icon im Zielordner (Merten-Feedback
        /// nach v2.1.4: "Icon ohne Überschreiben" - jede Erzeugung bekommt ihre eigene
        /// Datei, ältere Versionen bleiben zum Vergleichen erhalten). Zeitstempel bis
        /// auf die Sekunde genau sorgt für eine sprechende, nach Erzeugungszeit
        /// sortierbare Reihenfolge; ein zusätzliches Suffix deckt den (praktisch nie
        /// vorkommenden) Fall ab, dass zweimal innerhalb derselben Sekunde erzeugt wird.
        ///
        /// Diese Prüfung hier dient nur der Anzeige/Protokollierung VOR der
        /// eigentlichen (u.U. 20-120 Sekunden dauernden) KI-Anfrage - sie ist NICHT
        /// die endgültige Eindeutigkeits-Garantie. Die entscheidet erst
        /// FolderIconService.ReplaceGeneratedImageAndSetIcon innerhalb seiner
        /// ordnerweiten Sperre (Pruefbericht v2.2.0, P1), damit zwei nahezu
        /// gleichzeitig gestartete Erzeugungen im selben Ordner nicht denselben Namen
        /// "gewinnen" und eine davon verloren geht.
        /// </summary>
        private static string BuildUniqueAiIconImagePath(string targetFolder)
        {
            string baseName = "Icon-Original (von KI erzeugt) " + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string candidate = Path.Combine(targetFolder, baseName + ".png");
            int suffix = 2;
            while (File.Exists(candidate))
            {
                candidate = Path.Combine(targetFolder, $"{baseName} ({suffix}).png");
                suffix++;
            }
            return candidate;
        }

        /// <summary>
        /// Sperrt/entsperrt waehrend einer laufenden Operation alle
        /// Aktionen, die denselben oder einen anderen Ordner betreffen wuerden -
        /// verhindert, dass der Benutzer zwischendurch den Ordner wechselt oder
        /// das Icon manuell setzt/entfernt.
        /// </summary>
        private void SetAiOperationControlsEnabled(bool enabled)
        {
            _btnAiGenerate.Enabled = enabled;
            _btnBrowseFolder.Enabled = enabled;
            _btnSetIcon.Enabled = enabled;
            _btnRemoveIcon.Enabled = enabled;
        }

        /// <summary>
        /// Beginnt eine Operation: Phase setzen, Bedienelemente sperren, Wartecursor. Der
        /// "Abbrechen"-Knopf ist nur in der abbrechbaren Phase aktiv.
        /// </summary>
        private void BeginOperation(OperationPhase phase)
        {
            _phase = phase;
            SetAiOperationControlsEnabled(false);
            _btnAiCancel.Enabled = phase == OperationPhase.Cancellable;
            Cursor = Cursors.WaitCursor;
        }

        /// <summary>
        /// Beendet die Operation (mehrfach aufrufbar): Phase zurueck auf "Idle", Token/Task
        /// freigeben, Bedienelemente entsperren. Wurde waehrend der Operation das Schliessen
        /// gewuenscht (<see cref="_closeAfterOperation"/>), wird das Fenster jetzt geschlossen.
        /// </summary>
        private void EndOperation()
        {
            _phase = OperationPhase.Idle;
            _commitTask = null;
            CancellationTokenSource? cts = _aiCts;
            _aiCts = null;
            try { cts?.Dispose(); }
            catch (ObjectDisposedException) { /* bereits freigegeben */ }

            Cursor = Cursors.Default;
            SetAiOperationControlsEnabled(true);
            _btnAiCancel.Enabled = false;

            if (_closeAfterOperation)
            {
                _closeAfterOperation = false;
                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke(new Action(Close));
                }
            }
        }

        /// <summary>
        /// "Abbrechen"-Knopf: fordert den kontrollierten Abbruch der laufenden KI-Arbeit an
        /// (nur in der abbrechbaren Phase; danach laeuft die Dateitransaktion zu Ende).
        /// </summary>
        private void RequestAiCancel()
        {
            if (_phase != OperationPhase.Cancellable)
            {
                return;
            }

            _btnAiCancel.Enabled = false;
            Log("Abbruch angefordert ...");
            try { _aiCts?.Cancel(); }
            catch (ObjectDisposedException) { /* Operation ist gerade beendet worden */ }
        }

        /// <summary>
        /// Entscheidet beim Schliessen anhand von Phase und Grund (siehe
        /// <see cref="OperationGuard.DecideClose"/>), statt wie vorher waehrend einer KI-
        /// Operation pauschal alles zu blockieren (Runde 2, Befund F-001):
        ///  - Benutzer schliesst waehrend abbrechbarer Arbeit: Rueckfrage, bei "Ja" Abbruch und
        ///    danach automatisches Schliessen.
        ///  - Benutzer schliesst waehrend des Dateischritts: kurz abweisen (Rollback-Sicherheit).
        ///  - Windows-Herunterfahren/Task-Manager: nie blockieren. Abbrechbare Arbeit wird
        ///    abgebrochen; beim Dateischritt wird hoechstens wenige Sekunden auf dessen Ende
        ///    gewartet (best effort - ein hartes Beenden von aussen laesst sich nicht verhindern,
        ///    dafuer gibt es den Wiederherstellungsweg aus Befund F-002).
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            CloseAction action = OperationGuard.DecideClose(_phase, e.CloseReason == CloseReason.UserClosing);

            switch (action)
            {
                case CloseAction.AskCancelThenClose:
                    e.Cancel = true;
                    DialogResult answer = MessageBox.Show(this,
                        "Gerade läuft noch eine KI-Icon-Erzeugung. Soll sie abgebrochen und das Programm " +
                        "danach beendet werden? Im Ordner wird dabei nichts verändert.",
                        "Vorgang läuft noch", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                    if (answer == DialogResult.Yes)
                    {
                        _closeAfterOperation = true;
                        RequestAiCancel();
                        // Hat die Operation den abbrechbaren Teil inzwischen verlassen, beendet sie
                        // sich selbst und schliesst das Fenster danach (EndOperation).
                    }
                    return;

                case CloseAction.BlockCommitting:
                    e.Cancel = true;
                    MessageBox.Show(this,
                        "Es wird gerade in den Ordner geschrieben. Das dauert nur wenige Sekunden und darf " +
                        "nicht unterbrochen werden. Bitte kurz warten und dann erneut schließen.",
                        "Vorgang läuft noch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;

                case CloseAction.CancelAndAllow:
                    try { _aiCts?.Cancel(); }
                    catch (ObjectDisposedException) { /* bereits beendet */ }
                    break;

                case CloseAction.WaitForCommitAndAllow:
                    try { _commitTask?.Wait(TimeSpan.FromSeconds(8)); }
                    catch (Exception) { /* Fehler meldet die Operation selbst; hier nur begrenzt warten */ }
                    break;
            }

            base.OnFormClosing(e);
        }

        private void RefreshMenuStatus()
        {
            bool isRegistered = ContextMenuService.IsRegistered();

            // Zustand des Kontrollkästchens IMMER an den tatsächlichen Registry-Zustand
            // angleichen, statt ihm blind zu vertrauen - z.B. falls Register()/Unregister()
            // oben fehlgeschlagen ist, damit die Checkbox dann nicht einen Zustand zeigt,
            // der gar nicht eingetreten ist.
            _suppressContextMenuCheckedEvent = true;
            try
            {
                _chkContextMenu.Checked = isRegistered;
            }
            finally
            {
                _suppressContextMenuCheckedEvent = false;
            }

            _lblMenuStatus.Text = isRegistered
                ? "Status: aktuell im Kontextmenü eingetragen"
                : "Status: nicht eingetragen";
        }

        private void Log(string message)
        {
            _txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }
    }
}
