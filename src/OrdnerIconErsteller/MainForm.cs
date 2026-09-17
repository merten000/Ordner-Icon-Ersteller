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
        private readonly Button _btnRegisterMenu = new() { Text = "Ins Rechtsklick-Menü integrieren" };
        private readonly Button _btnUnregisterMenu = new() { Text = "Aus Rechtsklick-Menü entfernen" };
        private readonly Label _lblMenuStatus = new() { AutoSize = true };
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
            Width = 640;
            Height = 480;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(560, 420);

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
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                Padding = new Padding(12),
                RowCount = 6
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
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

            // Zeile 5: Icon per KI erzeugen (optional)
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

            // Zeile 6: Log
            _txtLog.Dock = DockStyle.Fill;

            root.Controls.Add(folderPanel, 0, 0);
            root.Controls.Add(imagePanel, 0, 1);
            root.Controls.Add(actionPanel, 0, 2);
            root.Controls.Add(menuGroup, 0, 3);
            root.Controls.Add(aiGroup, 0, 4);
            root.Controls.Add(_txtLog, 0, 5);

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
            _btnAiGenerate.Click += async (_, _) => await GenerateAiIconAsync();
            _cmbAiProvider.SelectedIndexChanged += (_, _) => RefreshAiKeyField();
            _chkShowAiKey.CheckedChanged += (_, _) => _txtAiKey.UseSystemPasswordChar = !_chkShowAiKey.Checked;
            _btnAiSaveKey.Click += (_, _) => SaveAiKey();
            _btnAiDeleteKey.Click += (_, _) => DeleteAiKey();
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
            // gewaehlten Ordner landet. Zusaetzlich werden waehrend des Auftrags
            // alle konkurrierenden Ordner-/Icon-Aktionen im Fenster gesperrt.
            string targetFolder = _txtFolder.Text;
            AiImageProvider provider = _cmbAiProvider.SelectedIndex == 1 ? AiImageProvider.Google : AiImageProvider.OpenAi;
            string enteredPrompt = _txtAiPrompt.Text.Trim();

            // "Icon ohne Text" (Merten-Feedback nach v2.1.4): ist keine Beschreibung
            // eingegeben, wird nicht mehr abgebrochen, sondern der Zielordner selbst
            // durchsucht (Dateinamen als Kontext, ein evtl. vorhandenes Bild als
            // Referenz für Google) - siehe FolderContentScanner.
            string prompt;
            AiIconService.ReferenceImage? referenceImage = null;
            if (string.IsNullOrWhiteSpace(enteredPrompt))
            {
                FolderContentScanner.ScanResult scan;
                try
                {
                    scan = FolderContentScanner.Scan(targetFolder);
                }
                catch (Exception ex)
                {
                    Log($"FEHLER beim Durchsuchen des Ordners für \"Icon ohne Text\": {ex.Message}");
                    MessageBox.Show(this, $"Der Ordner konnte nicht durchsucht werden:\n{ex.Message}", "Fehler",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                prompt = scan.ImplicitPrompt;
                if (scan.ReferenceImagePath is not null)
                {
                    try
                    {
                        referenceImage = AiIconService.LoadReferenceImage(scan.ReferenceImagePath);
                    }
                    catch (Exception ex)
                    {
                        // Ein nicht lesbares Referenzbild darf die Erzeugung nicht komplett
                        // verhindern - dann eben ohne Bild-Referenz weitermachen, nur mit
                        // der bereits gebauten Text-Beschreibung.
                        Log($"Hinweis: Referenzbild \"{scan.ReferenceImagePath}\" konnte nicht gelesen werden " +
                            $"({ex.Message}) - Erzeugung läuft ohne Bild-Referenz weiter.");
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
            string generatedImagePath = BuildUniqueAiIconImagePath(targetFolder);

            SetAiOperationControlsEnabled(false);
            _aiOperationInProgress = true;
            Cursor = Cursors.WaitCursor;
            try
            {
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
                await Task.Run(() => FolderIconService.ReplaceGeneratedImageAndSetIcon(targetFolder, imageBytes, generatedImagePath));

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
        /// Baut einen garantiert noch nicht existierenden Dateinamen für ein neu zu
        /// erzeugendes KI-Icon im Zielordner (Merten-Feedback nach v2.1.4: "Icon ohne
        /// Überschreiben" - jede Erzeugung bekommt ihre eigene Datei, ältere Versionen
        /// bleiben zum Vergleichen erhalten). Zeitstempel bis auf die Sekunde genau
        /// sorgt für eine sprechende, nach Erzeugungszeit sortierbare Reihenfolge; ein
        /// zusätzliches Suffix deckt den (praktisch nie vorkommenden) Fall ab, dass
        /// zweimal innerhalb derselben Sekunde erzeugt wird.
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
