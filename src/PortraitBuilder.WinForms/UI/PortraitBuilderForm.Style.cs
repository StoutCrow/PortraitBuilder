using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace PortraitBuilder.UI
{
    /// <summary>
    /// Two looks (Classic follows the Windows theme; Medieval is a parchment-and-leather palette), a history strip,
    /// a status line, paste-to-import, and remembered settings.
    /// </summary>
    public partial class PortraitBuilderForm
    {
        private enum Look { Classic, Medieval }

        private Look look = Look.Classic;
        private bool applyingLook;
        private Chip rbClassic, rbMedieval;
        private Panel banner, portraitFrame;
        private FlowLayoutPanel historyStrip;

        // Palette for the Medieval look. Colours are mixed from pigments: walnut wood, tanned leather, vellum, iron-gall ink, gilding, madder red.
        private static readonly Color Wood = ColorTranslator.FromHtml("#241810");
        private static readonly Color Leather = ColorTranslator.FromHtml("#44301f");
        private static readonly Color Paper = ColorTranslator.FromHtml("#dccaa0");
        private static readonly Color PaperLight = ColorTranslator.FromHtml("#f3e9cc");
        private static readonly Color Vellum = ColorTranslator.FromHtml("#e8d9b3");
        private static readonly Color Ink = ColorTranslator.FromHtml("#2a1b10");
        private static readonly Color Gold = ColorTranslator.FromHtml("#c4993a");
        private static readonly Color GoldDim = ColorTranslator.FromHtml("#8a6c2c");
        private static readonly Color Madder = ColorTranslator.FromHtml("#7b1f1f");
        private static readonly Color Classic = Color.FromArgb(0, 95, 184);

        /// <summary>Panel that repaints cleanly when resized; used for the portrait frame and banner.</summary>
        private sealed class FramePanel : Panel
        {
            public FramePanel() { DoubleBuffered = true; ResizeRedraw = true; }
        }

        // ---------------------------------------------------------------- look

        private static ChipTheme MedievalChipTheme() => new()
        {
            Bg = Leather, BgHover = ControlPaint.Light(Leather, .3f), BgDown = ControlPaint.Dark(Leather, .1f),
            Border = Gold, Text = Vellum, Muted = Color.FromArgb(0xb9, 0xa7, 0x7f),
            AccentBg = Madder, AccentBorder = Gold, AccentText = Gold,
            PrimaryBg = Madder, PrimaryHover = ControlPaint.Light(Madder, .25f), PrimaryText = Gold,
            Underline = Gold, Radius = 2, BorderWidth = 2, BoldText = true
        };

        private void ApplyLook(Look l)
        {
            look = l;
            bool m = l == Look.Medieval;
            SuspendLayout();

            ChipTheme.Current = m ? MedievalChipTheme() : ChipTheme.Classic();
            foreach (var menu in new[] { portraitMenu, savedMenu, moreMenu })
            {
                if (menu == null) continue;
                if (m) { menu.BackColor = Leather; menu.ForeColor = Vellum; }
                else { menu.ResetBackColor(); menu.ResetForeColor(); }
            }
            Font = m ? new Font("Palatino Linotype", 10F) : new Font("Segoe UI", 9F);
            if (m) { BackColor = Wood; ForeColor = Vellum; } else { ResetBackColor(); ResetForeColor(); }

            foreach (Control c in Controls.Cast<Control>().ToList()) StyleTree(c, false);

            banner.Visible = m;
            portraitFrame.Padding = m ? new Padding(26) : new Padding(0);
            portraitFrame.Invalidate();
            banner.Invalidate();

            if (miClassic != null) { miClassic.Checked = !m; miMedieval.Checked = m; }
            applyingLook = true;
            rbClassic.Checked = !m;
            rbMedieval.Checked = m;
            applyingLook = false;

            ResumeLayout(true);
        }

        private void StyleTree(Control c, bool paper)
        {
            paper |= c is TabPage || (c.Tag as string) == "paper" || c == panelDNA || c == panelProperties || c == panelExtras || c == panelDLCs || c == panelMods;
            StyleOne(c, paper);
            foreach (Control child in c.Controls.Cast<Control>().ToList()) StyleTree(child, paper);
        }

        private void StyleOne(Control c, bool paper)
        {
            bool m = look == Look.Medieval;
            switch (c)
            {
                case Chip:
                    c.Invalidate();
                    break;

                case TabStrip:
                    c.Invalidate();
                    break;

                case Panel hairline when (hairline.Tag as string) == "hairline":
                    hairline.BackColor = ChipTheme.Current.Border;
                    break;

                case ComboBox cb:
                    if (m) { cb.FlatStyle = FlatStyle.Flat; cb.BackColor = PaperLight; cb.ForeColor = Ink; }
                    else { cb.FlatStyle = FlatStyle.Standard; cb.ResetBackColor(); cb.ResetForeColor(); }
                    break;

                case CheckBox or RadioButton:
                    if (m) c.ForeColor = paper ? Ink : Vellum; else c.ResetForeColor();
                    break;

                case Label l:
                    StyleLabel(l, paper);
                    break;

                case TextBoxBase tb:
                    tb.BackColor = BackColor;
                    tb.ForeColor = m ? Color.FromArgb(0xb9, 0xa7, 0x7f) : SystemColors.GrayText;
                    break;

                case TabControl tc:
                    tc.Invalidate();
                    break;

                case PictureBox:
                    break;

                case TabPage tp:
                    tp.UseVisualStyleBackColor = false;
                    tp.BackColor = m ? Paper : SystemColors.Control;
                    break;

                case Panel or TableLayoutPanel:
                    if (m) c.BackColor = paper ? Paper : Wood;
                    else if (c == panelDNA || c == panelProperties || c == panelExtras || c == panelDLCs || c == panelMods) c.BackColor = SystemColors.Control;
                    else c.ResetBackColor();
                    break;
            }
        }

        private void StyleLabel(Label l, bool paper)
        {
            bool m = look == Look.Medieval;
            switch (l.Tag as string)
            {
                case "heading":
                    l.Font = m ? new Font("Palatino Linotype", 12.5F, FontStyle.Bold) : new Font("Segoe UI Semibold", 9.5F);
                    if (m) l.ForeColor = Gold; else l.ResetForeColor();
                    l.Paint -= PaintHeadingRule;
                    l.Paint += PaintHeadingRule;
                    break;
                case "muted":
                    l.ForeColor = m ? (paper ? Color.FromArgb(0x6b, 0x5a, 0x3c) : Color.FromArgb(0xb9, 0xa7, 0x7f)) : SystemColors.GrayText;
                    break;
                default:
                    if (m) l.ForeColor = paper ? Ink : Vellum; else l.ResetForeColor();
                    break;
            }
        }

        private void PaintHeadingRule(object sender, PaintEventArgs e)
        {
            if (look != Look.Medieval) return;
            var l = (Label)sender;
            using var pen = new Pen(GoldDim, 1);
            e.Graphics.DrawLine(pen, 0, l.Height - 1, l.Width, l.Height - 1);
        }

        private static void Diamond(Graphics g, Brush brush, float x, float y, float r) =>
            g.FillPolygon(brush, new[] { new PointF(x, y - r), new PointF(x + r, y), new PointF(x, y + r), new PointF(x - r, y) });

        /// <summary>A gilt double rule with diamonds at the corners and edge middles, drawn around the portrait.</summary>
        private void PaintFrame(object sender, PaintEventArgs e)
        {
            if (look != Look.Medieval) return;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = ((Control)sender).ClientRectangle;
            r.Inflate(-6, -6);
            using var outer = new Pen(Gold, 3);
            using var inner = new Pen(GoldDim, 1);
            using var gilt = new SolidBrush(Gold);
            g.DrawRectangle(outer, r);
            var r2 = r; r2.Inflate(-9, -9);
            g.DrawRectangle(inner, r2);
            foreach (var (x, y) in new[] { (r.Left, r.Top), (r.Right, r.Top), (r.Left, r.Bottom), (r.Right, r.Bottom),
                                           (r.Left + r.Width / 2, r.Top), (r.Left + r.Width / 2, r.Bottom),
                                           (r.Left, r.Top + r.Height / 2), (r.Right, r.Top + r.Height / 2) })
            {
                Diamond(g, gilt, x, y, 6);
            }
        }

        private void PaintBanner(object sender, PaintEventArgs e)
        {
            if (look != Look.Medieval) return;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = ((Control)sender).ClientRectangle;
            using var font = new Font("Palatino Linotype", 20F, FontStyle.Bold);
            const string title = "Portrait Builder";
            var size = TextRenderer.MeasureText(g, title, font);
            int cy = rect.Height / 2;
            TextRenderer.DrawText(g, title, font, new Point((rect.Width - size.Width) / 2, cy - size.Height / 2), Gold);
            using var pen = new Pen(GoldDim, 1);
            using var gilt = new SolidBrush(Gold);
            int gap = 18, left = (rect.Width - size.Width) / 2 - gap, right = (rect.Width + size.Width) / 2 + gap;
            g.DrawLine(pen, 24, cy, left - 8, cy);
            g.DrawLine(pen, right + 8, cy, rect.Width - 24, cy);
            Diamond(g, gilt, left, cy, 5);
            Diamond(g, gilt, right, cy, 5);
            Diamond(g, gilt, 24, cy, 4);
            Diamond(g, gilt, rect.Width - 24, cy, 4);
            g.DrawLine(pen, 24, rect.Height - 1, rect.Width - 24, rect.Height - 1);
        }

        // ---------------------------------------------------------------- history strip

        private readonly List<Image> thumbs = new();

        private Image MakeThumb()
        {
            var bmp = new Bitmap(96, 96);
            using var g = Graphics.FromImage(bmp);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            if (pbPortrait.Image != null) g.DrawImage(pbPortrait.Image, 0, 0, 96, 96);
            return bmp;
        }

        private void RefreshHistoryStrip()
        {
            if (historyStrip == null) return;
            historyStrip.SuspendLayout();
            foreach (Control old in historyStrip.Controls.Cast<Control>().ToList()) old.Dispose();
            for (int i = 0; i < thumbs.Count; i++)
            {
                int index = i;
                var tile = new Panel
                {
                    Size = new Size(60, 60),
                    Padding = new Padding(2),
                    Margin = new Padding(0, 0, Gap, 0),
                    BackColor = i == historyPos ? ChipTheme.Current.AccentBorder : Color.Transparent,
                    Cursor = Cursors.Hand
                };
                var pic = new PictureBox { Image = thumbs[i], SizeMode = PictureBoxSizeMode.Zoom, Dock = DockStyle.Fill, BackColor = Color.Transparent };
                pic.Click += (s, e) => GoHistory(index - historyPos);
                tile.Click += (s, e) => GoHistory(index - historyPos);
                pic.Tag = index;
                cardTip.SetToolTip(pic, "preview"); // drawn as a hover card
                tile.Controls.Add(pic);
                historyStrip.Controls.Add(tile);
            }
            historyStrip.ResumeLayout();
            if (historyPos >= 0 && historyPos < historyStrip.Controls.Count)
                historyStrip.ScrollControlIntoView(historyStrip.Controls[historyPos]);
        }

        // ---------------------------------------------------------------- status line and paste

        private readonly System.Windows.Forms.Timer statusTimer = new() { Interval = 4000 };
        private string DefaultNote => Summary();

        private void SetStatus(string text)
        {
            label22.Text = text;
            statusTimer.Stop();
            statusTimer.Start();
        }

        private static readonly Regex DnaRx = new(@"dna\s*=\s*""([a-z0-9]+)""", RegexOptions.IgnoreCase);
        private static readonly Regex PropRx = new(@"properties\s*=\s*""([a-z0-9]+)""", RegexOptions.IgnoreCase);

        /// <summary>Ctrl+V: apply a copied dna/properties pair. Returns false when the clipboard holds something else.</summary>
        private bool TryPasteDna()
        {
            if (!Clipboard.ContainsText()) return false;
            var text = Clipboard.GetText();
            var d = DnaRx.Match(text);
            var p = PropRx.Match(text);
            if (!d.Success || !p.Success) return false;
            try
            {
                PushHistory();
                started = false;
                UpdatePortrait(d.Groups[1].Value, p.Groups[1].Value, "");
                UpdateSelectedDisplayCharacteristicValues(character);
                started = true;
                DrawPortrait();
                PushHistory();
                SetStatus("Pasted DNA from the clipboard");
            }
            catch (ArgumentException)
            {
                started = true;
                SetStatus("The DNA on the clipboard is too short to use");
            }
            return true;
        }

        // ---------------------------------------------------------------- remembered settings

        private static string SettingsPath => Path.Combine(AppContext.BaseDirectory, "settings.txt");
        private readonly Dictionary<string, string> settings = new();

        private void LoadSettings()
        {
            try
            {
                foreach (var line in File.ReadAllLines(SettingsPath))
                {
                    int i = line.IndexOf('=');
                    if (i > 0) settings[line[..i].Trim()] = line[(i + 1)..].Trim();
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private int SettingInt(string key, int fallback) =>
            settings.TryGetValue(key, out var v) && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : fallback;

        private void ApplySettings()
        {
            int scale = SettingInt("scale", cbExportScale.SelectedIndex);
            if (scale >= 0 && scale < cbExportScale.Items.Count) cbExportScale.SelectedIndex = scale;
            chkShortNames.Checked = false; // the short names option is gone: tiles use short names, the raw boxes show raw names
            chkRandomType.Checked = SettingInt("randomType", 0) == 1;
            chkRandomRank.Checked = SettingInt("randomRank", 0) == 1;
            miTransparent.Checked = SettingInt("exportBg", 0) == 1;
            miFrameless.Checked = SettingInt("exportFrame", 0) == 1;
            if (settings.TryGetValue("theme", out var theme) && theme is "light" or "dark") themeMode = theme;
            miFollow.Checked = themeMode == "system"; miLight.Checked = themeMode == "light"; miDark.Checked = themeMode == "dark";
            ApplyColorMode(); // before the window exists, so it is made with the right theme

            // Sizes are stored at 96 DPI (w96/h96) so they survive a change of display scaling. Older "w/h" keys were saved by a DPI-unaware build and are ignored.
            int w96 = SettingInt("w96", 0), h96 = SettingInt("h96", 0), x = SettingInt("x", int.MinValue), y = SettingInt("y", int.MinValue);
            float dpiScale = DeviceDpi / 96f;
            if (w96 >= 1040 && h96 >= 700)
            {
                var wanted = new Size((int)(w96 * dpiScale), (int)(h96 * dpiScale));
                var area = Screen.FromPoint(new Point(x + 60, y + 20)).WorkingArea;
                if (Screen.AllScreens.Any(s => s.WorkingArea.Contains(new Point(x + 60, y + 20))))
                {
                    wanted = new Size(Math.Min(wanted.Width, area.Width), Math.Min(wanted.Height, area.Height));
                    StartPosition = FormStartPosition.Manual;
                    Location = new Point(x, y);
                }
                Size = wanted;
            }
        }

        private void SaveSettings()
        {
            try
            {
                var b = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
                var lines = new List<string>
                {
                    $"look={look}",
                    $"theme={themeMode}",
                    $"exportBg={(miTransparent.Checked ? 1 : 0)}",
                    $"exportFrame={(miFrameless.Checked ? 1 : 0)}",
                    $"scale={cbExportScale.SelectedIndex}",
                    $"shortNames={(chkShortNames.Checked ? 1 : 0)}",
                    $"randomType={(chkRandomType.Checked ? 1 : 0)}",
                    $"randomRank={(chkRandomRank.Checked ? 1 : 0)}",
                    $"w96={(int)(b.Width * 96f / DeviceDpi)}", $"h96={(int)(b.Height * 96f / DeviceDpi)}", $"x={b.X}", $"y={b.Y}"
                };
                if (LastPortraitLine() is { } last) lines.Add(last);
                File.WriteAllLines(SettingsPath, lines);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            uiScale = DeviceDpi / 96f; // rows made from now on are sized for the real scale

            // Never open larger than the screen's usable area (the design size is in 96 DPI units and grows with display scaling).
            var area = Screen.FromControl(this).WorkingArea;
            if (Width > area.Width || Height > area.Height)
            {
                Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
                Location = new Point(area.X + (area.Width - Width) / 2, area.Y + (area.Height - Height) / 2);
            }

            // Auto-sized panels measured themselves before DPI scaling finished; measure them again.
            static void Remeasure(Control c)
            {
                foreach (Control child in c.Controls)
                {
                    if (child is FlowLayoutPanel or TableLayoutPanel && child.AutoSize) { child.AutoSize = false; child.AutoSize = true; }
                    Remeasure(child);
                }
            }
            Remeasure(this);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveSettings();
            Microsoft.Win32.SystemEvents.UserPreferenceChanged -= OnWindowsPreferenceChanged;
            base.OnFormClosing(e);
        }
    }
}



