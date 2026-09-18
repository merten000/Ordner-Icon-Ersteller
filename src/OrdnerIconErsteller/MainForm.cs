using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
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
        private readonly Button _btnEinstellungen = new() { Text = "Einstellungen" };
        private readonly Button _btnRechtliches = new() { Text = "Rechtliches" };
        private readonly ContextMenuStrip _settingsMenu = new();
        private readonly ContextMenuStrip _rechtlichesMenu = new();
        private readonly TextBox _txtAiPrompt = new();
        private readonly ComboBox _cmbAiProvider = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly TextBox _txtAiKey = new() { UseSystemPasswordChar = true };
        private readonly CheckBox _chkShowAiKey = new() { Text = "anzeigen" };
        private readonly Button _btnAiSaveKey = new() { Text = "Speichern" };
        private readonly Button _btnAiDeleteKey = new() { Text = "Löschen" };
        private readonly Label _lblAiKeyStatus = new() { AutoSize = true };
        private readonly Button _btnAiGenerate = new() { Text = "Icon per KI erzeugen" };
        private readonly TextBox _txtLog = new()
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9f)
        };

        // Waehrend eine KI-Icon-Transaktion laeuft (Netzwerkanfrage + anschliessendes
        // Schreiben/Icon-Setzen in FolderIconService), darf das Fenster nicht
        // geschlossen werden: ein Prozessende mitten im Datei-Commit wuerde Rollback
        // und finally-Bloecke in FolderIconService moeglicherweise nie ausfuehren
        // (ChatGPT-Gegenpruefung von v2.1.2, Freigabeblocker 3).
        private bool _aiOperationInProgress;

        public MainForm(string? startFolder)
        {
            Text = "Ordner-Icon-Ersteller";
            Width = 660;
            Height = 500;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(600, 440);

            BuildLayout();
            WireEvents();
            RefreshMenuStatus();
            RefreshAiKeyField();

            if (!string.IsNullOrEmpty(startFolder))
            {
                _txtFolder.Text = startFolder;
                Log($"Ordner aus Explorer-Kontextmenü übernommen: {startFolder}");
            }
        }

        private void BuildLayout()
        {
            // Layout: eine obere Werkzeugleiste (Dock=Top, volle Fensterbreite)
            // mit den zwei Knöpfen "Einstellungen"/"Rechtliches" rechtsbündig, und
            // darunter der restliche Inhalt (Dock=Fill). Jeder Knopf öffnet beim
            // Klick ein aufklappendes Dropdown-Menü (ContextMenuStrip) direkt
            // darunter - siehe die Kommentare weiter unten für die verworfenen
            // Zwischenschritte (Band, Popup-Fenster, ComboBox).
            var leftContent = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                Padding = new Padding(12),
                RowCount = 5
            };
            leftContent.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            leftContent.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            leftContent.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            leftContent.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            leftContent.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

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

            // Zeile 4: Icon per KI erzeugen (optional)
            var aiGroup = new GroupBox { Text = "Icon per KI erzeugen (optional)", Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(8) };
            var aiLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, AutoSize = true };

            // Anbieter + API-Schlüssel direkt im Hauptfenster (bewusst kein
            // separater Dialog - siehe Rückmeldung nach dem ersten praktischen Test).
            var aiKeyPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, AutoSize = true };
            aiKeyPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            aiKeyPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _cmbAiProvider.Width = 100;
            _cmbAiProvider.Items.Add("OpenAI");
            _cmbAiProvider.Items.Add("Google");
            _cmbAiProvider.SelectedIndex = 0;
            _txtAiKey.Dock = DockStyle.Fill;
            _txtAiKey.PlaceholderText = "API-Schlüssel für den gewählten Anbieter";
            _btnAiSaveKey.Width = 100;
            _btnAiDeleteKey.Width = 100;
            aiKeyPanel.Controls.Add(_cmbAiProvider, 0, 0);
            aiKeyPanel.Controls.Add(_txtAiKey, 1, 0);
            aiKeyPanel.Controls.Add(_chkShowAiKey, 2, 0);
            aiKeyPanel.Controls.Add(_btnAiSaveKey, 3, 0);
            aiKeyPanel.Controls.Add(_btnAiDeleteKey, 4, 0);
            _lblAiKeyStatus.Margin = new Padding(0, 4, 0, 4);

            var aiPromptPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, AutoSize = true };
            _txtAiPrompt.Dock = DockStyle.Fill;
            _txtAiPrompt.PlaceholderText = "z. B. \"ein minimalistisches blaues Wolken-Symbol\" - leer lassen, damit der Ordnerinhalt selbst als Grundlage dient";
            aiPromptPanel.Controls.Add(new Label { Text = "Beschreibung (Prompt) - optional:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 4, 0, 0) }, 0, 0);
            aiPromptPanel.Controls.Add(_txtAiPrompt, 0, 1);

            var aiActionPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            _btnAiGenerate.Width = 170;
            aiActionPanel.Controls.Add(_btnAiGenerate);

            aiLayout.Controls.Add(aiKeyPanel, 0, 0);
            aiLayout.Controls.Add(_lblAiKeyStatus, 0, 1);
            aiLayout.Controls.Add(aiPromptPanel, 0, 2);
            aiLayout.Controls.Add(aiActionPanel, 0, 3);
            aiGroup.Controls.Add(aiLayout);

            // Obere Werkzeugleiste, über die volle Fensterbreite (Merten-Feedback:
            // "musst du es oben mit den Tools und Rechtlichen nicht alles in eine
            // extra Leiste einfügen, dass es bündig bleibt"). Zwei eigene Knöpfe
            // ("Einstellungen" links, "Rechtliches" rechts) - jeweils mit einem
            // klassischen aufklappenden Dropdown-Menü darunter (ContextMenuStrip),
            // genau das Standard-Verhalten, das Merten nach dem v2.5.0-Test
            // beschrieben hat ("dass die Menübox sich eben nach unten hin dann
            // eben aufblättert, wie es eigentlich sonst so immer Standard ist").
            // Frühere, verworfene Ansätze: ein seitliches "Band" mit ein-/
            // ausklappbaren Panels (v2.3.3-v2.3.6, blieb beim Anklicken
            // unsichtbar), je ein eigenes Popup-Fenster per Form.ShowDialog
            // (v2.4.0/v2.4.1, funktionierte, aber nicht die gewünschte Optik),
            // und eine gemeinsame ComboBox mit Platzhaltertext (v2.5.0, falsch
            // verstanden - siehe Rückmeldung).
            var topBar = new Panel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(0, 4, 12, 4) };
            var topBarButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            const int toggleButtonWidth = 130;
            _btnEinstellungen.Width = toggleButtonWidth;
            _btnRechtliches.Width = toggleButtonWidth;
            topBarButtons.Controls.Add(_btnEinstellungen);
            topBarButtons.Controls.Add(_btnRechtliches);
            topBar.Controls.Add(topBarButtons);

            // Inhalt des "Einstellungen"-Dropdowns: Erklärungstext (reines Label,
            // braucht kein eigenes Feld) + der Rechtsklick-Menü-Schalter mit
            // Status. ContextMenuStrip/ToolStripControlHost wird nur EINMAL hier
            // aufgebaut und danach für die gesamte Laufzeit wiederverwendet -
            // anders als bei der vorherigen Fenster-Lösung wird hier also nichts
            // pro Klick disposed, das Dispose-Problem von v2.4.0 kann hier gar
            // nicht erst auftreten.
            _settingsMenu.Items.Add(new ToolStripLabel("Explorer-Rechtsklick-Menü:") { Margin = new Padding(8, 4, 8, 0) });
            _chkContextMenu.Margin = new Padding(4, 2, 4, 2);
            _lblMenuStatus.Margin = new Padding(4, 0, 4, 4);
            _settingsMenu.Items.Add(new ToolStripControlHost(_chkContextMenu));
            _settingsMenu.Items.Add(new ToolStripControlHost(_lblMenuStatus));

            // Inhalt des "Rechtliches"-Dropdowns: die vier Dokumente als normale
            // Menüpunkte (kein eigenes Button-Feld mehr nötig, da sie nur hier
            // gebraucht werden).
            _rechtlichesMenu.Items.Add("Impressum", null, (_, _) => ShowLegalDocument(LegalDocumentService.Document.Impressum));
            _rechtlichesMenu.Items.Add("Datenschutz", null, (_, _) => ShowLegalDocument(LegalDocumentService.Document.Datenschutz));
            _rechtlichesMenu.Items.Add("Lizenz", null, (_, _) => ShowLegalDocument(LegalDocumentService.Document.Lizenz));
            _rechtlichesMenu.Items.Add("Haftungsausschluss", null, (_, _) => ShowLegalDocument(LegalDocumentService.Document.Haftungsausschluss));

            // Log
            _txtLog.Dock = DockStyle.Fill;

            leftContent.Controls.Add(folderPanel, 0, 0);
            leftContent.Controls.Add(imagePanel, 0, 1);
            leftContent.Controls.Add(actionPanel, 0, 2);
            leftContent.Controls.Add(aiGroup, 0, 3);
            leftContent.Controls.Add(_txtLog, 0, 4);

            // Reihenfolge: die oben angedockte Leiste zuerst, danach das
            // Dock=Fill-Element, das dann zuverlässig den kompletten Rest der
            // Fensterhöhe bekommt.
            Controls.Add(topBar);
            Controls.Add(leftContent);
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
            _chkContextMenu.CheckedChanged += (_, _) => OnContextMenuCheckedChanged();
            _btnAiGenerate.Click += async (_, _) => await GenerateAiIconAsync();
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
            bool hasKey = IsGoogleSelected ? ApiKeyStore.HasGoogleKey() : ApiKeyStore.HasOpenAiKey();
            _txtAiKey.Text = hasKey ? PasswordPlaceholder : string.Empty;
            _lblAiKeyStatus.Text = hasKey
                ? $"Status ({(IsGoogleSelected ? "Google" : "OpenAI")}): Schlüssel hinterlegt"
                : $"Status ({(IsGoogleSelected ? "Google" : "OpenAI")}): kein Schlüssel hinterlegt";
        }

        private async Task GenerateAiIconAsync()
        {
            // Pruefbericht v2.2.1, Freigabeblocker 1: vorher wurden die Bedienelemente
            // erst NACH dem (jetzt per Task.Run im Hintergrund laufenden) Ordner-Scan,
            // dem Referenzbild-Laden, der Zustimmung und der Namensberechnung gesperrt.
            // Waehrend ein langsamer Ordner/Netzlaufwerk durchsucht wurde, blieb der
            // Knopf aktiv - ein zweiter Klick konnte einen parallelen zweiten Ablauf
            // starten, der sich mit dem ersten gegenseitig ueber
            // SetAiOperationControlsEnabled/_aiOperationInProgress entsperrt haette.
            // Deshalb jetzt: fruehzeitiger Ausstieg, falls bereits eine Operation laeuft
            // (Sicherheitsnetz - die deaktivierten Bedienelemente sollten das ohnehin
            // verhindern), und Sperre/Deaktivierung GANZ AM ANFANG setzen, noch vor dem
            // ersten await, mit Scan/Zustimmung/Netzwerkanfrage/Dateitransaktion
            // zusammen in einem einzigen try/finally.
            if (_aiOperationInProgress)
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

            SetAiOperationControlsEnabled(false);
            _aiOperationInProgress = true;
            Cursor = Cursors.WaitCursor;
            try
            {
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
                        scan = await Task.Run(() => FolderContentScanner.Scan(targetFolder, allowReferenceImage));
                    }
                    catch (Exception ex)
                    {
                        Log($"FEHLER beim Durchsuchen des Ordners für \"Icon ohne Text\": {ex.Message}");
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
                            Log($"Referenzbild \"{scan.ReferenceImagePath}\" auf Wunsch nicht mitgesendet.");
                        }
                    }

                    Log($"Kein Text eingegeben - Ordnerinhalt wird stattdessen als Grundlage verwendet " +
                        $"(Zielordner: {targetFolder}" +
                        (referenceImage is not null ? ", mit Referenzbild aus dem Ordner)." : ")."));
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

                Log($"KI-Icon wird erzeugt (Anbieter: {provider}, Prompt: \"{prompt}\", Zielordner: {targetFolder})...");

                // AiIconService liefert an dieser Stelle bereits vollstaendig als
                // gueltiges Bild dekodierte und in ein kanonisches PNG neu
                // encodierte Bytes zurueck (siehe dortige DecodeAndReencodeAsPng).
                byte[] imageBytes = await AiIconService.GenerateIconImageAsync(provider, prompt, referenceImage);

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
                string generatedImagePath = await Task.Run(
                    () => FolderIconService.ReplaceGeneratedImageAndSetIcon(targetFolder, imageBytes, preferredImagePath));

                _txtImage.Text = generatedImagePath;

                Log($"KI-Icon gesetzt für: {targetFolder} (Bilddatei: {generatedImagePath})");
                MessageBox.Show(this, "Das von der KI erzeugte Icon wurde gesetzt und als Bilddatei im Ordner gespeichert. " +
                    "Falls der Explorer es nicht sofort anzeigt, Ansicht kurz aktualisieren (F5).",
                    "Fertig", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log($"FEHLER bei der KI-Icon-Erzeugung: {ex.Message}");
                MessageBox.Show(this, $"Fehler bei der KI-Icon-Erzeugung:\n{ex.Message}", "Fehler",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
                SetAiOperationControlsEnabled(true);
                _aiOperationInProgress = false;
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
        /// Sperrt/entsperrt waehrend einer laufenden KI-Icon-Erzeugung alle
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
        /// Verhindert, dass das Fenster (und damit der ganze Prozess) waehrend einer
        /// laufenden KI-Icon-Transaktion geschlossen wird - siehe
        /// <see cref="_aiOperationInProgress"/>. Ein Benutzer-Klick auf "X" waehrend
        /// der Wartezeit wuerde sonst den Prozess mitten in FolderIconService.
        /// ReplaceGeneratedImageAndSetIcon beenden koennen, bevor deren Rollback-/
        /// finally-Logik greifen kann.
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_aiOperationInProgress)
            {
                e.Cancel = true;
                MessageBox.Show(this,
                    "Gerade läuft noch eine KI-Icon-Erzeugung. Bitte warten, bis sie abgeschlossen ist, " +
                    "bevor das Programm beendet wird.",
                    "Vorgang läuft noch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
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
