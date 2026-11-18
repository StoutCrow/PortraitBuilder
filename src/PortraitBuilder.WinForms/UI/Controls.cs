using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PortraitBuilder.UI
{
    /// <summary>
    /// Colours and shape for the custom controls. Classic derives everything from the Windows theme and accent colour,
    /// so light, dark and a changed accent all work; Medieval supplies its own palette.
    /// </summary>
    public sealed class ChipTheme
    {
        public Color Bg, BgHover, BgDown, Border, Text, Muted;
        public Color AccentBg, AccentBorder, AccentText;
        public Color PrimaryBg, PrimaryHover, PrimaryText;
        public Color Underline;
        public int Radius = 6, BorderWidth = 1;
        public bool BoldText;

        public static ChipTheme Current { get; set; } = Classic();

        public static Color Mix(Color a, Color b, double t) => Color.FromArgb(
            (int)Math.Round(a.R + (b.R - a.R) * t), (int)Math.Round(a.G + (b.G - a.G) * t), (int)Math.Round(a.B + (b.B - a.B) * t));

        private static Color Contrast(Color c) => (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) > 150 ? Color.FromArgb(24, 24, 24) : Color.White;

        /// <summary>The user's Windows accent colour (Settings, Personalization, Colors), or a blue if it cannot be read.</summary>
        private static Color WindowsAccent()
        {
            try
            {
                if (Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM", "AccentColor", null) is int abgr)
                    return Color.FromArgb(abgr & 0xFF, (abgr >> 8) & 0xFF, (abgr >> 16) & 0xFF);
            }
            catch (Exception) { }
            return Color.FromArgb(0, 95, 184);
        }

        public static ChipTheme Classic()
        {
            Color bg = SystemColors.Control, text = SystemColors.ControlText, accent = WindowsAccent();
            return new ChipTheme
            {
                Bg = Mix(bg, text, .07), BgHover = Mix(bg, text, .14), BgDown = Mix(bg, text, .20),
                Border = Mix(bg, text, .26), Text = text, Muted = SystemColors.GrayText,
                AccentBg = Mix(bg, accent, .22), AccentBorder = accent, AccentText = Mix(accent, text, .4),
                PrimaryBg = accent, PrimaryHover = Mix(accent, text, .14), PrimaryText = Contrast(accent),
                Underline = accent, Radius = 6, BorderWidth = 1, BoldText = false
            };
        }
    }

    public enum ChipKind { Normal, Primary, Ghost }

    /// <summary>A rounded button. Hover is a quiet neutral fill; a checked toggle gets an accent outline and a faint accent tint.</summary>
    public class Chip : Button
    {
        private bool hover, down, isChecked;

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public ChipKind Kind { get; set; } = ChipKind.Normal;
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool IsToggle { get; set; }
        public event EventHandler CheckedChanged;

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool Checked
        {
            get => isChecked;
            set
            {
                if (isChecked == value) return;
                isChecked = value;
                Invalidate();
                CheckedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public Chip()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnClick(EventArgs e)
        {
            if (IsToggle) Checked = true; // radio-like: the owner unchecks the others in CheckedChanged
            base.OnClick(e);
        }

        public static GraphicsPath Rounded(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = radius * 2;
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var t = ChipTheme.Current;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent?.BackColor ?? t.Bg);

            float scale = DeviceDpi / 96f;
            int bw = Math.Max(1, (int)Math.Round(t.BorderWidth * scale));
            var rect = new RectangleF(bw / 2f, bw / 2f, Width - bw, Height - bw);
            float radius = t.Radius * scale;

            Color fill, border, text;
            bool toggledOn = IsToggle && isChecked;
            if (Kind == ChipKind.Primary)
            {
                fill = down || hover ? t.PrimaryHover : t.PrimaryBg;
                border = fill; text = t.PrimaryText;
            }
            else if (toggledOn)
            {
                fill = down || hover ? ChipTheme.Mix(t.AccentBg, t.AccentBorder, .12) : t.AccentBg;
                border = t.AccentBorder; text = t.AccentText;
            }
            else if (Kind == ChipKind.Ghost)
            {
                fill = down ? t.BgDown : hover ? t.BgHover : Color.Transparent;
                border = Color.Transparent; text = t.Text;
                // A ghost chip has no fill of its own, so on a light surface (Medieval parchment) the theme's light text would vanish.
                var surface = Parent?.BackColor ?? t.Bg;
                if (0.299 * surface.R + 0.587 * surface.G + 0.114 * surface.B > 150) text = Color.FromArgb(0x2a, 0x1b, 0x10);
            }
            else
            {
                fill = down ? t.BgDown : hover ? t.BgHover : t.Bg;
                border = t.Border; text = t.Text;
            }
            if (!Enabled) { text = t.Muted; if (Kind == ChipKind.Primary) fill = border = t.BgHover; }

            using (var path = Rounded(rect, radius))
            {
                if (fill != Color.Transparent) { using var b = new SolidBrush(fill); g.FillPath(b, path); }
                if (border != Color.Transparent) { using var pen = new Pen(border, bw); g.DrawPath(pen, path); }
            }

            using var font = new Font(Font, t.BoldText ? FontStyle.Bold : FontStyle.Regular);
            TextRenderer.DrawText(g, Text, font, ClientRectangle, text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

            if (Focused && ShowFocusCues)
            {
                var ring = new RectangleF(rect.X + 2 * scale, rect.Y + 2 * scale, rect.Width - 4 * scale, rect.Height - 4 * scale);
                using var path = Rounded(ring, Math.Max(1, radius - 2 * scale));
                using var pen = new Pen(Color.FromArgb(150, t.AccentBorder), Math.Max(1f, scale)) { DashStyle = DashStyle.Dot };
                g.DrawPath(pen, path);
            }
        }
    }

    /// <summary>
    /// Underline tabs that show one panel at a time. The selected tab gets an accent underline;
    /// hover is a quiet neutral fill. (The system TabControl is avoided: it draws a light frame that ignores the theme.)
    /// </summary>
    public sealed class TabStrip : Control
    {
        private readonly (string Text, Control Page)[] items;
        private int hoverIndex = -1, selected;

        public event EventHandler SelectedIndexChanged;

        public TabStrip(params (string Text, Control Page)[] items)
        {
            this.items = items;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Height = 38;
            Cursor = Cursors.Hand;
            ShowSelected();
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int SelectedIndex
        {
            get => selected;
            set { if (value >= 0 && value < items.Length && value != selected) { selected = value; ShowSelected(); Invalidate(); SelectedIndexChanged?.Invoke(this, EventArgs.Empty); } }
        }

        private void ShowSelected()
        {
            for (int i = 0; i < items.Length; i++) items[i].Page.Visible = i == selected;
        }

        private Rectangle[] Layout()
        {
            float scale = DeviceDpi / 96f;
            using var font = new Font(Font, ChipTheme.Current.BoldText ? FontStyle.Bold : FontStyle.Regular);
            int textTotal = items.Sum(i => TextRenderer.MeasureText(i.Text, font).Width);
            // Roomy padding when it fits; with more tabs than width, squeeze it so none runs off the edge.
            int pad = (int)(16 * scale);
            if (Width > 0) pad = Math.Max((int)(4 * scale), Math.Min(pad, (Width - textTotal) / (items.Length * 2)));
            int x = 0;
            var rects = new Rectangle[items.Length];
            for (int i = 0; i < rects.Length; i++)
            {
                int w = TextRenderer.MeasureText(items[i].Text, font).Width + pad * 2;
                rects[i] = new Rectangle(x, 0, w, Height);
                x += w;
            }
            return rects;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int index = Array.FindIndex(Layout(), r => r.Contains(e.Location));
            if (index != hoverIndex) { hoverIndex = index; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { hoverIndex = -1; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            int index = Array.FindIndex(Layout(), r => r.Contains(e.Location));
            if (index >= 0) SelectedIndex = index;
            base.OnMouseDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var t = ChipTheme.Current;
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? t.Bg);
            float scale = DeviceDpi / 96f;
            int line = Math.Max(1, (int)Math.Round(scale)), accent = Math.Max(2, (int)Math.Round(2 * scale));

            using (var rule = new Pen(t.Border, line)) g.DrawLine(rule, 0, Height - line, Width, Height - line);

            var rects = Layout();
            using var font = new Font(Font, t.BoldText ? FontStyle.Bold : FontStyle.Regular);
            for (int i = 0; i < rects.Length; i++)
            {
                bool on = i == selected;
                if (!on && i == hoverIndex)
                {
                    using var hoverFill = new SolidBrush(t.BgHover);
                    g.FillRectangle(hoverFill, rects[i].X, rects[i].Y, rects[i].Width, rects[i].Height - line);
                }
                TextRenderer.DrawText(g, items[i].Text, font, rects[i], on ? t.AccentText : t.Text,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
                if (on)
                {
                    using var under = new SolidBrush(t.Underline);
                    g.FillRectangle(under, rects[i].X, Height - accent, rects[i].Width, accent);
                }
            }
        }
    }

    /// <summary>One feature row. The whole row takes a quiet neutral fill on hover, like list rows in UniverseView.</summary>
    public sealed class HoverRow : TableLayoutPanel
    {
        private bool hover;

        public HoverRow()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            RowCount = 1;
        }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            // Children get the mouse first, so watch them too; otherwise the highlight would flicker off over a child.
            e.Control.MouseEnter += (s, a) => UpdateHover();
            e.Control.MouseLeave += (s, a) => UpdateHover();
            base.OnControlAdded(e);
        }

        protected override void OnMouseEnter(EventArgs e) { UpdateHover(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { UpdateHover(); base.OnMouseLeave(e); }

        private void UpdateHover()
        {
            bool now = IsHandleCreated && ClientRectangle.Contains(PointToClient(Cursor.Position));
            if (now != hover) { hover = now; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var t = ChipTheme.Current;
            e.Graphics.Clear(Parent?.BackColor ?? t.Bg);
            if (!hover) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = Chip.Rounded(new RectangleF(0, 0, Width - 1, Height - 1), t.Radius * DeviceDpi / 96f);
            using var fill = new SolidBrush(ChipTheme.Mix(Parent?.BackColor ?? t.Bg, t.Text, .07));
            e.Graphics.FillPath(fill, path);
        }
    }

    /// <summary>
    /// A picture of the portrait with one option applied, and its name. Hover is a quiet fill; the chosen one gets the accent outline.
    /// </summary>
    public sealed class IconTile : Button
    {
        private Image image;
        private bool hover, selected;

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string Key { get; set; }

        /// <summary>What the picture was drawn from. A tile whose stamp still matches needs no new picture.</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public string Stamp { get; set; }
        public event EventHandler Picked;

        public IconTile()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Image Picture
        {
            get => image;
            set { var old = image; image = value; Invalidate(); old?.Dispose(); }
        }

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public bool Selected
        {
            get => selected;
            set { if (selected != value) { selected = value; Invalidate(); } }
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnClick(EventArgs e) { Picked?.Invoke(this, EventArgs.Empty); base.OnClick(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { image?.Dispose(); image = null; }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var t = ChipTheme.Current;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent?.BackColor ?? t.Bg);
            float k = DeviceDpi / 96f;
            int bw = Math.Max(1, (int)Math.Round(k));

            var rect = new RectangleF(bw / 2f, bw / 2f, Width - bw, Height - bw);
            using (var path = Chip.Rounded(rect, t.Radius * k))
            {
                if (selected) { using var fill = new SolidBrush(t.AccentBg); g.FillPath(fill, path); }
                else if (hover) { using var fill = new SolidBrush(t.BgHover); g.FillPath(fill, path); }
                if (selected) { using var pen = new Pen(t.AccentBorder, Math.Max(2f, 2 * k)); g.DrawPath(pen, path); }
            }

            int pad = (int)(6 * k);
            int side = Width - pad * 2;
            var pic = new Rectangle(pad, pad, side, side);
            if (image != null)
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(image, pic);
            }
            else
            {
                // Still rendering: a faint ring where the portrait will be.
                using var pen = new Pen(Color.FromArgb(90, t.Muted), Math.Max(1f, k)) { DashStyle = DashStyle.Dot };
                g.DrawEllipse(pen, pic.X + side * .08f, pic.Y + side * .08f, side * .84f, side * .84f);
            }

            using var small = new Font(Font.FontFamily, Math.Max(7f, Font.Size - 1.25f), FontStyle.Regular);
            var text = new Rectangle(pad / 2, pic.Bottom + (int)(3 * k), Width - pad, Height - pic.Bottom - (int)(5 * k));
            // The page behind the tile can be lighter than the theme's text (Medieval parchment), so pick the caption colour from it.
            var back = Parent?.BackColor ?? t.Bg;
            bool lightPage = back.GetBrightness() > .55f;
            var caption = selected ? t.AccentText : lightPage ? Color.FromArgb(0x3a, 0x28, 0x14) : t.Text;
            TextRenderer.DrawText(g, Text, small, text, caption,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);

            if (Focused && ShowFocusCues)
            {
                using var ring = new Pen(Color.FromArgb(150, t.AccentBorder), Math.Max(1f, k)) { DashStyle = DashStyle.Dot };
                using var path = Chip.Rounded(new RectangleF(2 * k, 2 * k, Width - 4 * k, Height - 4 * k), Math.Max(1, t.Radius * k - 2 * k));
                g.DrawPath(ring, path);
            }
        }
    }

    /// <summary>A wrapping grid of IconTiles. Keeps the tiles while the list of options is unchanged, so pictures can arrive one by one without flicker.</summary>
    public sealed class TilePicker : FlowLayoutPanel
    {
        private readonly Dictionary<string, IconTile> tiles = new();
        private string signature = "";

        public TilePicker()
        {
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            WrapContents = true;
            FlowDirection = FlowDirection.LeftToRight;
            Margin = new Padding(0);
        }

        /// <summary>Raised with a tile's key and the screen position when it is right-clicked.</summary>
        public event Action<string, Point> TileRightClicked;

        /// <summary>Tile width in 96-dpi pixels. Narrower tiles let a row of six (the ranks) fit without one tile wrapping alone.</summary>
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int TileWidth { get; set; } = 78;

        public void Set(IReadOnlyList<(string Key, string Caption)> items, string selectedKey, Action<string> pick)
        {
            string sig = TileWidth + ":" + string.Join("|", items.Select(i => i.Key + "=" + i.Caption));
            if (sig != signature)
            {
                signature = sig;
                SuspendLayout();
                foreach (Control c in Controls.Cast<Control>().ToList()) c.Dispose();
                Controls.Clear();
                tiles.Clear();
                float k = DeviceDpi / 96f;
                foreach (var (key, caption) in items)
                {
                    var tile = new IconTile
                    {
                        Key = key,
                        Text = caption,
                        Size = new Size((int)(TileWidth * k), (int)((TileWidth + 28) * k)),
                        Margin = new Padding(0, 0, (int)(6 * k), (int)(6 * k))
                    };
                    tile.Picked += (s, e) => pick(key);
                    tile.MouseUp += (s, e) => { if (e.Button == MouseButtons.Right) TileRightClicked?.Invoke(key, tile.PointToScreen(e.Location)); };
                    tiles[key] = tile;
                    Controls.Add(tile);
                }
                ResumeLayout(true);
            }
            foreach (var pair in tiles) pair.Value.Selected = pair.Key == selectedKey;
        }

        public void SetImage(string key, Image picture, string stamp)
        {
            if (tiles.TryGetValue(key, out var tile)) { tile.Picture = picture; tile.Stamp = stamp; } else picture.Dispose();
        }

        /// <summary>True when the tile has no picture yet, or one drawn from different data.</summary>
        public bool NeedsImage(string key, string stamp) =>
            !tiles.TryGetValue(key, out var tile) || tile.Picture == null || tile.Stamp != stamp;
    }
}


