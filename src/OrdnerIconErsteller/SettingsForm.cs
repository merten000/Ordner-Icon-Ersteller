using System;
using System.Drawing;
using System.Windows.Forms;

namespace OrdnerIconErsteller
{
    /// <summary>
    /// Normales kleines Einstellungsfenster (modaler Dialog) fuer die vorhandene
    /// Einstellung "Explorer-Rechtsklick-Menue".
    ///
    /// Ersetzt das fruehere "Einstellungen"-Aufklappmenue (ContextMenuStrip mit eingebetteten
    /// CheckBox-/Label-Steuerelementen). Dort liess ein ToolStripDropDownMenu mit
    /// ToolStripControlHost-Eintraegen die Bildlauf-Pfeile des Menues erscheinen; deren
    /// Bildlauf (ToolStripDropDownMenu.ScrollInternal) griff ausserhalb der Eintragsliste zu und
    /// loeste auf echtem Windows eine ArgumentOutOfRangeException aus. In einem gewoehnlichen
    /// Fenster gibt es weder ein ToolStrip-Dropdown noch einen Bildlauf: alle Steuerelemente
    /// sind fest angeordnet und das Fenster ist gross genug, damit nichts abgeschnitten wird.
    ///
    /// Die eigentliche Registry-Logik bleibt unveraendert in <see cref="ContextMenuService"/>;
    /// dieses Fenster kennt sie nicht, sondern bekommt nur zwei Funktionen vom Hauptfenster
    /// (Zustand lesen, Zustand aendern). Alle Steuerelemente gehoeren diesem Fenster und werden
    /// pro Oeffnen neu erzeugt und mit dem Fenster freigegeben (nichts wird zwischen Fenster und
    /// Hauptfenster geteilt).
    /// </summary>
    internal sealed class SettingsForm : Form
    {
        private readonly Func<bool> _isRegistered;
        private readonly Action<IWin32Window, bool> _apply;

        private readonly CheckBox _chkContextMenu = new()
        {
            Text = "Ins Rechtsklick-Menü integrieren",
            AutoSize = true,
            Location = new Point(24, 150),
        };

        private readonly Label _lblStatus = new()
        {
            AutoSize = false,
            Location = new Point(44, 178),
            Size = new Size(432, 22),
            ForeColor = Theme.Muted,
        };

        // Verhindert, dass das programmatische Angleichen des Hakens an den tatsaechlichen
        // Zustand das Aendern (Eintragen/Entfernen) ein zweites Mal ausloest.
        private bool _suppressChecked;

        /// <param name="isRegistered">Liefert, ob der Rechtsklick-Menue-Eintrag aktuell eingetragen ist.</param>
        /// <param name="apply">Traegt den Eintrag ein (true) bzw. entfernt ihn (false); Fehler meldet
        /// diese Funktion selbst (Protokoll und Meldung), sie wirft nicht.</param>
        public SettingsForm(Func<bool> isRegistered, Action<IWin32Window, bool> apply)
        {
            _isRegistered = isRegistered ?? throw new ArgumentNullException(nameof(isRegistered));
            _apply = apply ?? throw new ArgumentNullException(nameof(apply));

            Text = "Einstellungen";
            Font = new Font("Segoe UI", 9f);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            BackColor = Theme.Background;
            // Feste, grosszuegige Groesse (96-DPI-Pixel, wird ueber AutoScaleMode.Dpi mitskaliert):
            // genug Platz fuer Ueberschrift, Beschreibung, Schalter, Status und Schliessen-Knopf.
            ClientSize = new Size(500, 290);

            var heading = new Label
            {
                Text = "Explorer-Rechtsklick-Menü",
                AutoSize = false,
                Font = new Font("Segoe UI Semibold", 11f),
                ForeColor = Theme.Text,
                Location = new Point(24, 20),
                Size = new Size(452, 26),
            };

            var description = new Label
            {
                Text = "Ist der Schalter an, erscheint beim Rechtsklick auf einen Ordner der " +
                       "Eintrag „Ordner-Icon ändern...“. Er gilt nur für dein Benutzerkonto " +
                       "(keine Administratorrechte nötig) und lässt sich hier jederzeit " +
                       "wieder entfernen.",
                AutoSize = false,
                ForeColor = Theme.Text,
                Location = new Point(24, 52),
                Size = new Size(452, 88),
            };

            var closeButton = new ModernButton
            {
                Text = "Schließen",
                Kind = ButtonKind.Primary,
                CornerColor = Theme.Background,
                Size = new Size(120, 34),
                Location = new Point(500 - 24 - 120, 290 - 20 - 34),
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
            };
            closeButton.Click += (_, _) => Close();

            _chkContextMenu.CheckedChanged += (_, _) => OnCheckedChanged();

            Controls.Add(heading);
            Controls.Add(description);
            Controls.Add(_chkContextMenu);
            Controls.Add(_lblStatus);
            Controls.Add(closeButton);

            // Enter und Esc schliessen das Fenster; das X oben rechts funktioniert wie gewohnt.
            AcceptButton = closeButton;
            CancelButton = closeButton;

            RefreshState();
        }

        private void OnCheckedChanged()
        {
            if (_suppressChecked)
            {
                return;
            }

            try
            {
                _apply(this, _chkContextMenu.Checked);
            }
            catch (Exception ex)
            {
                // Sicherheitsnetz: eine Ausnahme darf das Fenster nie unbehandelt verlassen.
                MessageBox.Show(this, $"Die Einstellung konnte nicht geändert werden:\n{ex.Message}", "Fehler",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            RefreshState();
        }

        // Haken und Statuszeile immer an den tatsaechlichen Registry-Zustand angleichen,
        // statt dem Haken blind zu vertrauen (z.B. falls das Eintragen/Entfernen scheiterte).
        private void RefreshState()
        {
            bool registered;
            try
            {
                registered = _isRegistered();
            }
            catch
            {
                registered = false;
            }

            _suppressChecked = true;
            try
            {
                _chkContextMenu.Checked = registered;
            }
            finally
            {
                _suppressChecked = false;
            }

            _lblStatus.Text = registered
                ? "Status: aktuell im Kontextmenü eingetragen"
                : "Status: nicht eingetragen";
        }
    }
}
