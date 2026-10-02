using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace OrdnerIconErsteller
{
    // Reine Oberflaechen-Bausteine (Karten, Knoepfe, Eingabefelder, Statuspunkt) fuer die
    // modernisierte Optik. Sie enthalten KEINE Programmlogik und keine neuen Funktionen:
    // Die eigentlichen Steuerelemente (TextBox, Button, CheckBox, ComboBox) und ihre
    // Ereignisse bleiben unveraendert, es aendert sich nur die Darstellung.
    // Alle Masse werden ueber DeviceDpi skaliert (Theme.U), damit 100/125/150 % passen.

    internal static class Theme
    {
        public static readonly Color Background = Color.FromArgb(237, 243, 251);
        public static readonly Color Card = Color.White;
        public static readonly Color Border = Color.FromArgb(217, 225, 236);
        public static readonly Color Shadow = Color.FromArgb(222, 230, 241);
        public static readonly Color Text = Color.FromArgb(31, 41, 55);
        public static readonly Color Muted = Color.FromArgb(107, 114, 128);
        public static readonly Color Accent = Color.FromArgb(30, 115, 204);
        public static readonly Color AccentHover = Color.FromArgb(26, 98, 176);
        public static readonly Color AccentPressed = Color.FromArgb(22, 82, 148);
        public static readonly Color AccentSoft = Color.FromArgb(225, 238, 252);
        public static readonly Color Disabled = Color.FromArgb(156, 163, 175);

        private static string? _iconFont;
        private static bool _iconFontSearched;

        // Symbolschrift von Windows (Windows 11: "Segoe Fluent Icons", Windows 10: "Segoe MDL2 Assets").
        // Ist keine von beiden vorhanden, werden einfach keine Symbole gezeichnet.
        public static string? IconFont
        {
            get
            {
                if (!_iconFontSearched)
                {
                    _iconFontSearched = true;
                    try
                    {
                        using var fonts = new InstalledFontCollection();
                        bool fluent = false, mdl2 = false;
                        foreach (var family in fonts.Families)
                        {
                            if (family.Name == "Segoe Fluent Icons") fluent = true;
                            else if (family.Name == "Segoe MDL2 Assets") mdl2 = true;
                        }
                        _iconFont = fluent ? "Segoe Fluent Icons" : mdl2 ? "Segoe MDL2 Assets" : null;
                    }
                    catch
                    {
                        _iconFont = null;
                    }
                }
                return _iconFont;
            }
        }

        // Pixelmass bei 96 DPI -> aktuelle Bildschirmskalierung.
        public static int U(Control control, float value) =>
            (int)Math.Round(value * control.DeviceDpi / 96.0);

        public static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            float d = Math.Min(radius * 2f, Math.Min(r.Width, r.Height));
            var path = new GraphicsPath();
            if (d <= 0.5f)
            {
                path.AddRectangle(r);
                return path;
            }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void DrawGlyph(Graphics g, string glyph, Color color, RectangleF rect, float points)
        {
            string? family = IconFont;
            if (family == null) return;
            using var font = new Font(family, points);
            using var brush = new SolidBrush(color);
            using var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.DrawString(glyph, font, brush, rect, format);
        }
    }

    // Segoe-Symbolcodes (in beiden Symbolschriften vorhanden).
    internal static class Glyph
    {
        public const string Folder = "";
        public const string Picture = "";
        public const string Check = "";
        public const string Delete = "";
        public const string Eye = "";
        public const string Star = "";
        public const string Settings = "";
        public const string Page = "";
        public const string Chevron = "";
        public const string Save = "";
        public const string Cancel = "";
    }

    internal enum ButtonKind
    {
        Secondary,
        Primary,
        Ghost
    }

    // Moderner, flacher Knopf mit abgerundeten Ecken. Bleibt ein normaler Button
    // (Tastatur, Klick-Ereignis, Enabled, Fokus).
    internal sealed class ModernButton : Button
    {
        private bool _hover;
        private bool _down;

        public ButtonKind Kind { get; set; } = ButtonKind.Secondary;
        public string? Glyph { get; set; }
        public string? TrailingGlyph { get; set; }
        // Farbe hinter den abgerundeten Ecken (Karte = weiss, Fenster = Hintergrundblau).
        public Color CornerColor { get; set; } = Theme.Card;

        public ModernButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            _down = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            if (mevent.Button == MouseButtons.Left) _down = true;
            Invalidate();
            base.OnMouseDown(mevent);
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            _down = false;
            Invalidate();
            base.OnMouseUp(mevent);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            Invalidate();
            base.OnGotFocus(e);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            Invalidate();
            base.OnLostFocus(e);
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            var g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var cornerBrush = new SolidBrush(CornerColor))
            {
                g.FillRectangle(cornerBrush, ClientRectangle);
            }

            bool enabled = Enabled;
            Color fill, border, textColor, glyphColor;
            switch (Kind)
            {
                case ButtonKind.Primary:
                    fill = !enabled ? Color.FromArgb(160, 190, 225)
                        : _down ? Theme.AccentPressed
                        : _hover ? Theme.AccentHover : Theme.Accent;
                    border = Color.Empty;
                    textColor = Color.White;
                    glyphColor = Color.White;
                    break;
                case ButtonKind.Ghost:
                    fill = enabled && _down ? Color.FromArgb(232, 238, 247)
                        : enabled && _hover ? Color.FromArgb(240, 244, 250) : Color.Empty;
                    border = Color.Empty;
                    textColor = enabled ? Theme.Muted : Theme.Disabled;
                    glyphColor = enabled ? Theme.Muted : Theme.Disabled;
                    break;
                default:
                    fill = !enabled ? Color.FromArgb(247, 248, 250)
                        : _down ? Color.FromArgb(232, 240, 251)
                        : _hover ? Color.FromArgb(243, 247, 253) : Theme.Card;
                    border = !enabled ? Color.FromArgb(229, 233, 240)
                        : _hover ? Color.FromArgb(176, 198, 226) : Theme.Border;
                    textColor = enabled ? Theme.Text : Theme.Disabled;
                    glyphColor = enabled ? Theme.Accent : Theme.Disabled;
                    break;
            }

            var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            float radius = Theme.U(this, 7);
            using (var path = Theme.RoundRect(rect, radius))
            {
                if (fill != Color.Empty)
                {
                    using var fillBrush = new SolidBrush(fill);
                    g.FillPath(fillBrush, path);
                }
                if (border != Color.Empty)
                {
                    using var pen = new Pen(border, 1f);
                    g.DrawPath(pen, path);
                }
            }

            if (Focused && ShowFocusCues)
            {
                var ring = new RectangleF(2.5f, 2.5f, Width - 5f, Height - 5f);
                using var ringPath = Theme.RoundRect(ring, Math.Max(1, radius - 2));
                using var ringPen = new Pen(Color.FromArgb(140, 170, 215), 1.5f);
                g.DrawPath(ringPen, ringPath);
            }

            // Inhalt (Symbol, Text, Pfeil) mittig anordnen.
            bool hasIcon = Glyph != null && Theme.IconFont != null;
            bool hasTrailing = TrailingGlyph != null && Theme.IconFont != null;
            int iconWidth = hasIcon ? Theme.U(this, 18) : 0;
            int trailingWidth = hasTrailing ? Theme.U(this, 14) : 0;
            int gap = Theme.U(this, 6);
            var flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            Size textSize = string.IsNullOrEmpty(Text)
                ? Size.Empty
                : TextRenderer.MeasureText(Text, Font, new Size(int.MaxValue, int.MaxValue), flags);

            int total = iconWidth + textSize.Width + trailingWidth;
            if (hasIcon && textSize.Width > 0) total += gap;
            if (hasTrailing && (textSize.Width > 0 || hasIcon)) total += gap;
            int x = Math.Max(Theme.U(this, 6), (Width - total) / 2);

            if (hasIcon)
            {
                Theme.DrawGlyph(g, Glyph!, glyphColor, new RectangleF(x, 0, iconWidth, Height), 10f);
                x += iconWidth;
                if (textSize.Width > 0) x += gap;
            }
            if (textSize.Width > 0)
            {
                var textRect = new Rectangle(x, 0, textSize.Width + 2, Height);
                TextRenderer.DrawText(g, Text, Font, textRect, textColor,
                    flags | TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
                x += textSize.Width;
            }
            if (hasTrailing)
            {
                if (textSize.Width > 0 || hasIcon) x += gap;
                Theme.DrawGlyph(g, TrailingGlyph!, Theme.Muted, new RectangleF(x, 0, trailingWidth, Height), 7f);
            }
        }
    }

    // Abgerundeter Rahmen um eine normale TextBox (die TextBox selbst kann keine runden Ecken).
    // Die TextBox bleibt das eigentliche Eingabeelement mit allen Eigenschaften und Ereignissen.
    internal sealed class InputBox : Panel
    {
        private readonly TextBox _textBox;
        private Control? _adornment;
        private bool _focused;

        public Color CornerColor { get; set; } = Theme.Card;

        public InputBox(TextBox textBox, int height = 34)
        {
            _textBox = textBox;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = height;
            Margin = new Padding(0);
            BackColor = Theme.Card;
            textBox.BorderStyle = BorderStyle.None;
            textBox.BackColor = Theme.Card;
            textBox.ForeColor = Theme.Text;
            textBox.Enter += (_, _) => { _focused = true; Invalidate(); };
            textBox.Leave += (_, _) => { _focused = false; Invalidate(); };
            Controls.Add(textBox);
        }

        // Kleines Zusatzelement rechts im Feld (z. B. das Auge beim Schluesselfeld).
        public Control? Adornment
        {
            get => _adornment;
            set
            {
                if (_adornment != null) Controls.Remove(_adornment);
                _adornment = value;
                if (value != null) Controls.Add(value);
                PerformLayout();
            }
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            int pad = Theme.U(this, 10);
            int reserve = _adornment != null ? _adornment.Width + Theme.U(this, 4) : 0;
            if (_textBox.Multiline)
            {
                int vpad = Theme.U(this, 8);
                _textBox.SetBounds(pad, vpad, Math.Max(10, Width - 2 * pad - reserve), Math.Max(10, Height - 2 * vpad));
            }
            else
            {
                _textBox.Width = Math.Max(10, Width - 2 * pad - reserve);
                _textBox.Location = new Point(pad, Math.Max(0, (Height - _textBox.Height) / 2));
            }
            if (_adornment != null)
            {
                _adornment.Location = new Point(
                    Width - _adornment.Width - Theme.U(this, 4),
                    Math.Max(0, (Height - _adornment.Height) / 2));
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var cornerBrush = new SolidBrush(CornerColor))
            {
                g.FillRectangle(cornerBrush, ClientRectangle);
            }
            var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            using var path = Theme.RoundRect(rect, Theme.U(this, 6));
            using (var fillBrush = new SolidBrush(Theme.Card))
            {
                g.FillPath(fillBrush, path);
            }
            using var pen = new Pen(_focused ? Theme.Accent : Theme.Border, _focused ? 1.5f : 1f);
            g.DrawPath(pen, path);
        }
    }

    // Weisse Karte mit weicher Kante, Symbol-Kreis und Ueberschrift (ersetzt die graue GroupBox).
    internal sealed class CardPanel : Panel
    {
        private readonly Panel _header = new() { Dock = DockStyle.Top, Height = 44 };
        private readonly string _title;
        private readonly string? _subtitle;
        private readonly string _glyph;
        private Font? _titleFont;
        private bool _bodyAdded;

        public CardPanel(string title, string glyph, string? subtitle = null)
        {
            _title = title;
            _glyph = glyph;
            _subtitle = subtitle;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            // Hintergrund = Fensterfarbe (hinter den runden Ecken); die Kartenflaeche wird in OnPaint gezeichnet.
            BackColor = Theme.Background;
            Padding = new Padding(16, 0, 16, 16);
            Margin = new Padding(0, 0, 0, 12);
            Dock = DockStyle.Fill;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
        }

        // Der Inhalt (Body) liegt unter der Kopfzeile. Der Body wird zuerst, die Kopfzeile danach
        // hinzugefuegt, damit die Kopfzeile oben andockt und der Body den Rest fuellt.
        public void SetBody(Control body)
        {
            if (_bodyAdded) return;
            _bodyAdded = true;
            body.Dock = DockStyle.Fill;
            body.BackColor = Theme.Card;
            Controls.Add(body);
            Controls.Add(_header);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float radius = Theme.U(this, 10);
            int lift = Theme.U(this, 2);

            // Angedeuteter weicher Schatten: dieselbe Form leicht nach unten versetzt.
            var shadowRect = new RectangleF(0.5f, 1.5f, Width - 1f, Height - 2f);
            using (var shadowPath = Theme.RoundRect(shadowRect, radius))
            using (var shadowBrush = new SolidBrush(Theme.Shadow))
            {
                g.FillPath(shadowBrush, shadowPath);
            }
            var cardRect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f - lift);
            using (var cardPath = Theme.RoundRect(cardRect, radius))
            {
                using (var cardBrush = new SolidBrush(Theme.Card))
                {
                    g.FillPath(cardBrush, cardPath);
                }
                using var pen = new Pen(Theme.Border, 1f);
                g.DrawPath(pen, cardPath);
            }

            // Kopfzeile: Symbol-Kreis, Ueberschrift, optionaler Hinweis in Grau.
            int headerHeight = _header.Height;
            int diameter = Theme.U(this, 30);
            int badgeX = Theme.U(this, 16);
            int badgeY = (headerHeight - diameter) / 2 + Theme.U(this, 2);
            var badge = new RectangleF(badgeX, badgeY, diameter, diameter);
            using (var badgeBrush = new SolidBrush(Theme.AccentSoft))
            {
                g.FillEllipse(badgeBrush, badge);
            }
            Theme.DrawGlyph(g, _glyph, Theme.Accent, badge, 11f);

            _titleFont ??= new Font("Segoe UI Semibold", 10.5f);
            int textX = badgeX + diameter + Theme.U(this, 10);
            var flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            Size titleSize = TextRenderer.MeasureText(_title, _titleFont, new Size(int.MaxValue, int.MaxValue), flags);
            var titleRect = new Rectangle(textX, badgeY, titleSize.Width + 2, diameter);
            TextRenderer.DrawText(g, _title, _titleFont, titleRect, Theme.Text,
                flags | TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            if (!string.IsNullOrEmpty(_subtitle))
            {
                var hintRect = new Rectangle(titleRect.Right + Theme.U(this, 8), badgeY, Math.Max(10, Width - titleRect.Right - Theme.U(this, 24)), diameter);
                TextRenderer.DrawText(g, _subtitle, Font, hintRect, Theme.Muted,
                    flags | TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _titleFont?.Dispose();
                _header.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // Runder Symbol-Kreis (Kopfzeile des Hauptfensters).
    internal sealed class IconBadge : Control
    {
        private readonly string _glyph;

        public IconBadge(string glyph, int size)
        {
            _glyph = glyph;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Width = size;
            Height = size;
            Margin = new Padding(0, 0, 10, 0);
            TabStop = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var cornerBrush = new SolidBrush(Parent?.BackColor ?? Theme.Background))
            {
                g.FillRectangle(cornerBrush, ClientRectangle);
            }
            var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            using (var brush = new SolidBrush(Theme.AccentSoft))
            {
                g.FillEllipse(brush, rect);
            }
            Theme.DrawGlyph(g, _glyph, Theme.Accent, rect, 14f);
        }
    }

    // Statuszeile mit farbigem Punkt (gruen = Schluessel hinterlegt, grau = keiner, gelb = nicht lesbar).
    // Die Farbe leitet sich nur aus dem vorhandenen Statustext ab; die Statuslogik bleibt unveraendert.
    internal sealed class StatusLabel : Label
    {
        public StatusLabel()
        {
            AutoSize = true;
            Padding = new Padding(18, 0, 0, 0);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            string text = Text ?? string.Empty;
            Color dot = text.Contains("konnte nicht", StringComparison.OrdinalIgnoreCase)
                ? Color.FromArgb(217, 160, 30)
                : text.Contains("kein", StringComparison.OrdinalIgnoreCase)
                    ? Color.FromArgb(156, 163, 175)
                    : Color.FromArgb(34, 160, 90);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int d = Theme.U(this, 9);
            using var brush = new SolidBrush(dot);
            g.FillEllipse(brush, new RectangleF(Theme.U(this, 2), (Height - d) / 2f, d, d));
        }
    }
}
