using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace PortraitBuilder.UI
{
    /// <summary>
    /// Short portrait names (race and flavor), the one-line summary, hover cards for recent portraits,
    /// the right-click menu and the shortcuts sheet.
    /// </summary>
    public partial class PortraitBuilderForm
    {
        // ---------------------------------------------------------------- scale for controls made after the window is shown

        private float uiScale = 1f; // 1 until the form is shown; the form's own DPI scaling handles everything built before that
        private int Px(int v) => (int)Math.Round(v * uiScale);

        private void FitRows(FlowLayoutPanel p)
        {
            // Leave room for the vertical scrollbar so a horizontal one never appears.
            int width = Math.Max(Px(160), p.ClientSize.Width - p.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
            foreach (Control c in p.Controls)
            {
                if (c is TableLayoutPanel box && box.Tag is PortraitBuilder.Model.Portrait.Characteristic)
                {
                    // One feature section: its header row and its strip of tiles share the width.
                    box.MinimumSize = new Size(width, 0);
                    box.MaximumSize = new Size(width, 0);
                    foreach (Control inner in box.Controls)
                    {
                        if (inner is HoverRow) inner.Width = width - inner.Margin.Horizontal;
                        else if (inner is TilePicker tiles) { tiles.MinimumSize = new Size(width, 0); tiles.MaximumSize = new Size(width, 0); }
                    }
                }
                else if (c is HoverRow) c.Width = width - c.Margin.Horizontal;
                else if (c is TilePicker strip)
                {
                    // An auto-sized flow panel wraps its tiles at the widest width it is allowed.
                    int w = width - c.Margin.Horizontal;
                    strip.MinimumSize = new Size(w, 0);
                    strip.MaximumSize = new Size(w, 0);
                }
            }
        }

        // ---------------------------------------------------------------- race and flavor

        /// <summary>
        /// "andalusiangfx_male2" becomes race "Andalusian" and flavor "Male 2". Names follow &lt;race&gt;gfx_&lt;flavor&gt;; a name without that shape is all race.
        /// </summary>
        internal static (string Race, string Flavor) SplitName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return ("", "");
            int i = raw.IndexOf("gfx_", StringComparison.Ordinal);
            string race = i >= 0 ? raw[..i] : raw.EndsWith("gfx", StringComparison.Ordinal) ? raw[..^3] : raw;
            string rest = i >= 0 ? raw[(i + 4)..] : "";
            string flavor = string.Join(" ", rest.Split('_', StringSplitOptions.RemoveEmptyEntries).Select(PrettyToken));
            return (Title(race.Replace('_', ' ')), flavor);
        }

        private static string Title(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

        private static string PrettyToken(string token)
        {
            var m = Regex.Match(token, @"^([a-z]+?)(\d+)$");
            return m.Success ? $"{Title(m.Groups[1].Value)} {m.Groups[2].Value}" : Title(token);
        }

        internal static string ShortName(string raw)
        {
            var (race, flavor) = SplitName(raw);
            return flavor.Length == 0 ? race : $"{race} · {flavor}";
        }

        private CheckBox chkShortNames;
        private ComboBox cbRace, cbFlavor;
        private TableLayoutPanel typeSplit;
        private bool syncingNames;

        private void BuildNameControls()
        {
            chkShortNames = new CheckBox { Text = "Short names: race, then flavor", AutoSize = true, Margin = new Padding(0, 2, 0, 0) };
            toolTip.SetToolTip(chkShortNames, "Show \"Andalusian\" and \"Male 2\" instead of the file name andalusiangfx_male2. Race is the culture before \"gfx\"; flavor is the rest (gender, variant, age).");

            cbRace = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DropDownHeight = 400, IntegralHeight = false, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 4, 0) };
            cbFlavor = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DropDownHeight = 400, IntegralHeight = false, Dock = DockStyle.Fill, Margin = new Padding(4, 0, 0, 0) };
            toolTip.SetToolTip(cbRace, "Race (the culture of the portrait set)");
            toolTip.SetToolTip(cbFlavor, "Flavor: the sub-race, such as male, female 2 or male late");

            typeSplit = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0), Visible = false };
            typeSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
            typeSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
            typeSplit.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            typeSplit.Controls.Add(cbRace, 0, 0);
            typeSplit.Controls.Add(cbFlavor, 1, 0);

            cbRace.SelectedIndexChanged += (s, e) => { if (!syncingNames) PickFromRace(); };
            cbFlavor.SelectedIndexChanged += (s, e) => { if (!syncingNames) PickFromFlavor(); };
            cbPortraitTypes.SelectedIndexChanged += (s, e) => RefreshRaceFlavor();
            chkShortNames.CheckedChanged += (s, e) => ApplyShortNames();

            // The clothing list keeps the raw names as items (selection and undo depend on them); only the text shown changes.
            cbCulturePortraitTypes.Format += (s, e) =>
            {
                if (chkShortNames.Checked && e.ListItem is string raw && raw.Length > 0) e.Value = ShortName(raw);
            };
        }

        private List<(string Raw, string Race, string Flavor)> TypeNames() =>
            cbPortraitTypes.Items.Cast<object>().Select(o => o.ToString()).Select(raw =>
            {
                var (race, flavor) = SplitName(raw);
                return (raw, race, flavor.Length == 0 ? "Default" : flavor);
            }).ToList();

        /// <summary>Make the race and flavor boxes show the portrait type that is actually selected.</summary>
        private void RefreshRaceFlavor()
        {
            if (syncingNames || cbRace == null || cbPortraitTypes.Items.Count == 0) return;
            syncingNames = true;
            try
            {
                var all = TypeNames();
                var races = all.Select(n => n.Race).Distinct().OrderBy(r => r, StringComparer.OrdinalIgnoreCase).Cast<object>().ToArray();
                if (cbRace.Items.Count != races.Length || !cbRace.Items.Cast<object>().SequenceEqual(races))
                {
                    cbRace.Items.Clear();
                    cbRace.Items.AddRange(races);
                }
                var current = cbPortraitTypes.SelectedItem as string;
                var (race, flavorRaw) = SplitName(current);
                string flavor = flavorRaw.Length == 0 ? "Default" : flavorRaw;
                cbRace.SelectedItem = race;
                var flavors = all.Where(n => n.Race == race).Select(n => n.Flavor).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).Cast<object>().ToArray();
                cbFlavor.Items.Clear();
                cbFlavor.Items.AddRange(flavors);
                cbFlavor.SelectedItem = flavor;
            }
            finally
            {
                syncingNames = false;
            }
        }

        private void SelectRaw(string raw)
        {
            int index = cbPortraitTypes.Items.IndexOf(raw);
            if (index >= 0 && cbPortraitTypes.SelectedIndex != index) cbPortraitTypes.SelectedIndex = index; // fires the normal type-change handling
        }

        private void PickFromRace()
        {
            if (cbRace.SelectedItem is not string race) return;
            var inRace = TypeNames().Where(n => n.Race == race).ToList();
            if (inRace.Count == 0) return;
            string wanted = cbFlavor.SelectedItem as string; // keep the flavor when the new race has it
            var pick = inRace.FirstOrDefault(n => n.Flavor == wanted);
            if (pick.Raw == null) pick = inRace.OrderBy(n => n.Flavor, StringComparer.OrdinalIgnoreCase).First();
            SelectRaw(pick.Raw);
            RefreshRaceFlavor();
        }

        private void PickFromFlavor()
        {
            if (cbRace.SelectedItem is not string race || cbFlavor.SelectedItem is not string flavor) return;
            var pick = TypeNames().FirstOrDefault(n => n.Race == race && n.Flavor == flavor);
            if (pick.Raw != null) SelectRaw(pick.Raw);
        }

        private void ApplyShortNames()
        {
            bool on = chkShortNames.Checked;
            typeSplit.Visible = on;
            cbPortraitTypes.Visible = !on;
            if (on) RefreshRaceFlavor();
            // Make the clothing list re-read its display text.
            typeof(ComboBox).GetMethod("RefreshItems", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(cbCulturePortraitTypes, null);
            cbCulturePortraitTypes.Invalidate();
            if (!statusTimer.Enabled) label22.Text = DefaultNote;
        }

        // ---------------------------------------------------------------- summary line

        /// <summary>What is on screen, in a line: "Andalusian · Male 2 · Baron · Feudal".</summary>
        private string Summary()
        {
            if (cbPortraitTypes == null || cbPortraitTypes.SelectedItem is not string raw) return "";
            var parts = new List<string> { ShortName(raw) };
            if (cbCulturePortraitTypes.SelectedItem is string cloth && cloth.Length > 0) parts.Add("dressed as " + ShortName(cloth));
            if (cbRank.SelectedIndex > 0 && cbRank.SelectedItem is string rankName) parts.Add(rankName);
            if (cbGovernment.SelectedItem is string governmentName) parts.Add(governmentName);
            return string.Join(" · ", parts);
        }

        // ---------------------------------------------------------------- hover cards for recent portraits

        private readonly ToolTip cardTip = new() { OwnerDraw = true, InitialDelay = 300, ReshowDelay = 100, AutoPopDelay = 30000 };
        private bool cardsReady;

        private static string Ago(DateTime at)
        {
            double s = Math.Max(0, (DateTime.Now - at).TotalSeconds);
            if (s < 45) return "just now";
            double m = Math.Round(s / 60);
            if (m < 60) return $"{m} min ago";
            double h = Math.Round(m / 60);
            return h < 24 ? $"{h} h ago" : at.ToString("g");
        }

        private void EnsureCards()
        {
            if (cardsReady) return;
            cardsReady = true;
            cardTip.Popup += (s, e) =>
            {
                float k = DeviceDpi / 96f;
                e.ToolTipSize = new Size((int)(224 * k), (int)(284 * k));
            };
            cardTip.Draw += DrawCard;
        }

        private void DrawCard(object sender, DrawToolTipEventArgs e)
        {
            if (e.AssociatedControl?.Tag is not int i || i < 0 || i >= thumbs.Count) { e.DrawBackground(); e.DrawText(); return; }
            var t = ChipTheme.Current;
            var g = e.Graphics;
            float k = DeviceDpi / 96f;
            using (var back = new SolidBrush(ChipTheme.Mix(BackColor, t.Text, .08))) g.FillRectangle(back, e.Bounds);
            using (var edge = new Pen(t.Border, Math.Max(1f, k))) g.DrawRectangle(edge, 0, 0, e.Bounds.Width - 1, e.Bounds.Height - 1);

            int pad = (int)(14 * k), size = e.Bounds.Width - pad * 2;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(thumbs[i], pad, pad, size, size);

            using var bold = new Font(Font, FontStyle.Bold);
            var flags = TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
            TextRenderer.DrawText(g, ShortName(history[i].Type), bold, new Rectangle(pad, pad + size + (int)(10 * k), size, (int)(22 * k)), t.Text, flags);
            TextRenderer.DrawText(g, $"{histLabels[i]} · {Ago(histTimes[i])}", Font, new Rectangle(pad, pad + size + (int)(34 * k), size, (int)(22 * k)), t.Muted, flags);
        }

        // ---------------------------------------------------------------- right-click menu

        private ContextMenuStrip portraitMenu;

        private void BuildPortraitMenu()
        {
            portraitMenu = new ContextMenuStrip();
            ToolStripMenuItem Item(string text, string keys, Action run)
            {
                var item = new ToolStripMenuItem(text) { ShortcutKeyDisplayString = keys };
                item.Click += (s, e) => run();
                return item;
            }
            portraitMenu.Items.Add(Item("Randomize", "Ctrl+R", () => OnClickRandomize(this, EventArgs.Empty)));
            portraitMenu.Items.Add(Item("Undo", "Ctrl+Z", () => GoHistory(-1)));
            portraitMenu.Items.Add(Item("Redo", "Ctrl+Y", () => GoHistory(1)));
            portraitMenu.Items.Add(Item("Keep in Saved", "Ctrl+D", KeepCurrent));
            portraitMenu.Items.Add(new ToolStripSeparator());
            portraitMenu.Items.Add(Item("Copy image", "Ctrl+Shift+C", () => { if (pbPortrait.Image != null) { Clipboard.SetImage(pbPortrait.Image); SetStatus("Image copied"); } }));
            portraitMenu.Items.Add(Item("Copy DNA", "", () => OnClickCopy(this, EventArgs.Empty)));
            portraitMenu.Items.Add(Item("Save image...", "Ctrl+S", () => OnClickSave(this, EventArgs.Empty)));
            portraitMenu.Items.Add(new ToolStripSeparator());
            portraitMenu.Items.Add(Item("Shortcuts", "F1", ShowShortcuts));
            pbPortrait.ContextMenuStrip = portraitMenu;
        }

        // ---------------------------------------------------------------- shortcuts sheet

        private void ShowShortcuts()
        {
            using var dialog = new ShortcutsDialog(Font, BackColor, look == Look.Medieval);
            dialog.ShowDialog(this);
        }
    }
}
