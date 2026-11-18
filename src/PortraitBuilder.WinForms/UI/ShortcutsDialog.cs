using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace PortraitBuilder.UI
{
    /// <summary>A key drawn as a small rounded cap, like the key hints in UniverseView's shortcut sheet.</summary>
    internal sealed class KeyCap : Control
    {
        public KeyCap(string text)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Text = text;
            AutoSize = true; // sized by GetPreferredSize below; a bare Control would be 0 x 0
            Margin = new Padding(0, 4, 12, 4);
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            var text = TextRenderer.MeasureText(Text, Font);
            float k = DeviceDpi / 96f;
            return new Size(text.Width + (int)(18 * k), text.Height + (int)(8 * k));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var t = ChipTheme.Current;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent?.BackColor ?? t.Bg);
            float k = DeviceDpi / 96f;
            var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using var path = Chip.Rounded(rect, Math.Max(3, t.Radius * k * .7f));
            using (var fill = new SolidBrush(t.Bg)) g.FillPath(fill, path);
            using (var pen = new Pen(t.Border, Math.Max(1f, k))) g.DrawPath(pen, path);
            // A thicker lower edge makes it read as a key.
            using (var base1 = new Pen(ChipTheme.Mix(t.Border, t.Text, .25), Math.Max(2f, 2 * k)))
                g.DrawLine(base1, rect.Left + 4 * k, rect.Bottom - 1, rect.Right - 4 * k, rect.Bottom - 1);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 0, Width, Height - (int)(2 * k)), t.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        }
    }

    /// <summary>Every key and click, in one place, grouped by what you are doing.</summary>
    internal sealed class ShortcutsDialog : Form
    {
        public ShortcutsDialog(Font font, Color back, bool medieval)
        {
            Text = "Shortcuts";
            Font = font;
            BackColor = back;
            ForeColor = medieval ? Color.FromArgb(0xe8, 0xd9, 0xb3) : SystemColors.ControlText;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            // Sized in code from the display scale; the layout is small and fixed, so no automatic scaling is needed.
            AutoScaleMode = AutoScaleMode.None;
            float k = DeviceDpi / 96f;
            Padding = new Padding((int)(24 * k), (int)(20 * k), (int)(24 * k), (int)(20 * k));
            ClientSize = new Size((int)(580 * k), (int)(560 * k));
            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };

            var sections = new (string Title, (string Keys, string What)[] Rows)[]
            {
                ("Make a portrait", new[]
                {
                    ("Ctrl+R", "Randomize the ticked features. F5 does the same."),
                    ("Right-click", "The portrait menu: randomize, undo, copy and save."),
                }),
                ("Go back", new[]
                {
                    ("Ctrl+Z", "Back to the previous portrait."),
                    ("Ctrl+Y", "Forward again."),
                    ("Recent portraits", "Click one to return to it. Rest the pointer on it for a larger preview."),
                }),
                ("Copy and save", new[]
                {
                    ("Ctrl+D", "Keep this portrait in the Saved tab."),
                    ("Ctrl+S", "Save the image at the size chosen in the toolbar."),
                    ("Ctrl+Shift+C", "Copy the image."),
                    ("Ctrl+V", "Paste a copied dna and properties pair."),
                }),
                ("Change one feature", new[]
                {
                    ("‹  ›", "Step the value down or up, wrapping around at the ends."),
                    ("Mouse wheel", "Over a value box, the same."),
                    ("↻", "Pick a random value for that feature only."),
                    ("▦", "Show every value as a picture, then click one."),
                }),
                ("Help", new[] { ("F1", "This sheet."), ("Esc", "Close it.") }),
            };

            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoScroll = true, Padding = new Padding(0) };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            foreach (var (title, rows) in sections)
            {
                var heading = new Label
                {
                    Text = title,
                    AutoSize = true,
                    Font = new Font(medieval ? "Palatino Linotype" : "Segoe UI Semibold", medieval ? 12F : 10F, medieval ? FontStyle.Bold : FontStyle.Regular),
                    ForeColor = medieval ? Color.FromArgb(0xc4, 0x99, 0x3a) : ForeColor,
                    Margin = new Padding(0, (int)(14 * k), 0, (int)(6 * k))
                };
                table.Controls.Add(heading);
                table.SetColumnSpan(heading, 2);
                foreach (var (keys, what) in rows)
                {
                    table.Controls.Add(new KeyCap(keys) { Font = Font });
                    table.Controls.Add(new Label { Text = what, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, (int)(6 * k), 0, (int)(6 * k)), MaximumSize = new Size((int)(400 * k), 0) });
                }
            }

            var close = new Chip { Text = "Close", Size = new Size((int)(96 * k), (int)(34 * k)), Dock = DockStyle.Right };
            close.Click += (s, e) => Close();
            var footer = new Panel { Dock = DockStyle.Bottom, Height = (int)(46 * k) };
            footer.Controls.Add(close);
            Controls.Add(table);
            Controls.Add(footer);
            CancelButton = close;
        }
    }
}
