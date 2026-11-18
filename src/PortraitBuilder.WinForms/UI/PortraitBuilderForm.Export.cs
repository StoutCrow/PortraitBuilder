using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using PortraitBuilder.Engine;
using PortraitBuilder.Model.Portrait;
using SkiaSharp;

namespace PortraitBuilder.UI
{
    /// <summary>
    /// The "More" menu: export options (transparent background, no frame), batches of random portraits, appearance, shortcuts.
    /// </summary>
    public partial class PortraitBuilderForm
    {
        private ContextMenuStrip moreMenu;
        private ToolStripMenuItem miTransparent, miFrameless, miFollow, miLight, miDark, miClassic, miMedieval;
        private string themeMode = "system";

        private void BuildMoreMenu()
        {
            moreMenu = new ContextMenuStrip();

            // Saving
            miTransparent = new ToolStripMenuItem("Transparent background") { CheckOnClick = true, ToolTipText = "Saved images leave out the scenery behind the figure" };
            miFrameless = new ToolStripMenuItem("Without frame") { CheckOnClick = true, ToolTipText = "Saved images leave out the rank and government ring" };
            var size = new ToolStripMenuItem("Image size");
            for (int i = 0; i < ExportScales.Length; i++)
            {
                int index = i;
                var item = new ToolStripMenuItem($"{ExportScales[i]}x  ({176 * ExportScales[i]} px)") { Checked = cbExportScale.SelectedIndex == i };
                item.Click += (s, e) => cbExportScale.SelectedIndex = index;
                size.DropDownItems.Add(item);
            }
            cbExportScale.SelectedIndexChanged += (s, e) =>
            {
                for (int i = 0; i < size.DropDownItems.Count; i++) ((ToolStripMenuItem)size.DropDownItems[i]).Checked = i == cbExportScale.SelectedIndex;
                toolTip.SetToolTip(btnSave, $"Save the portrait as a {176 * ExportScale} px PNG (Ctrl+S). Change the size in More.");
            };
            moreMenu.Items.Add(size);
            moreMenu.Items.Add(miTransparent);
            moreMenu.Items.Add(miFrameless);
            moreMenu.Items.Add("Save 10 random portraits to a folder…", null, (s, e) => SaveBatch(10));
            moreMenu.Items.Add("Save a contact sheet of 20…", null, (s, e) => SaveContactSheet(20));
            moreMenu.Items.Add(new ToolStripSeparator());

            // What Randomize may change besides the ticked features. These mirror two tick boxes that hold the setting.
            ToolStripMenuItem Mirror(string text, CheckBox box)
            {
                var item = new ToolStripMenuItem(text) { CheckOnClick = true, Checked = box.Checked };
                item.CheckedChanged += (s, e) => box.Checked = item.Checked;
                box.CheckedChanged += (s, e) => item.Checked = box.Checked;
                return item;
            }
            moreMenu.Items.Add(Mirror("Randomize also changes the race", chkRandomType));
            moreMenu.Items.Add(Mirror("Randomize also changes rank and government", chkRandomRank));
            moreMenu.Items.Add(new ToolStripSeparator());

            // DNA text: for pasting into character files, or taking a portrait from one.
            moreMenu.Items.Add("Copy DNA", null, (s, e) => OnClickCopy(this, EventArgs.Empty));
            moreMenu.Items.Add("Paste DNA\tCtrl+V", null, (s, e) => { if (!TryPasteDna()) SetStatus("The clipboard has no dna and properties to paste"); });
            moreMenu.Items.Add("Import DNA…", null, (s, e) => OnClickImport(this, EventArgs.Empty));
            moreMenu.Items.Add(new ToolStripSeparator());

            moreMenu.Items.Add("DLC and mods…", null, (s, e) => ShowContent());
            miFollow = new ToolStripMenuItem("Follow Windows") { Checked = true };
            miLight = new ToolStripMenuItem("Light");
            miDark = new ToolStripMenuItem("Dark");
            var appearance = new ToolStripMenuItem("Appearance");
            appearance.DropDownItems.AddRange(new ToolStripItem[] { miFollow, miLight, miDark });
            foreach (var (item, mode) in new[] { (miFollow, "system"), (miLight, "light"), (miDark, "dark") })
            {
                var m = mode;
                item.Click += (s, e) => SetTheme(m);
            }
            // The two looks: plain Windows, or parchment and gilt.
            miClassic = new ToolStripMenuItem("Plain Windows") { Checked = true };
            miMedieval = new ToolStripMenuItem("Medieval");
            miClassic.Click += (s, e) => ApplyLook(Look.Classic);
            miMedieval.Click += (s, e) => ApplyLook(Look.Medieval);
            var style = new ToolStripMenuItem("Style");
            style.DropDownItems.AddRange(new ToolStripItem[] { miClassic, miMedieval });
            moreMenu.Items.Add(style);
            moreMenu.Items.Add(appearance);
            moreMenu.Items.Add("Keyboard shortcuts\tF1", null, (s, e) => ShowShortcuts());
            moreMenu.Items.Add("About and credits…", null, (s, e) => { using var about = new AboutDialog(Font, BackColor, look == Look.Medieval); about.ShowDialog(this); });

            // Windows can change between light and dark while the app is open.
            SystemEvents.UserPreferenceChanged += OnWindowsPreferenceChanged;
        }

        // ---------------------------------------------------------------- DLC and mods

        private Form contentDialog;

        /// <summary>The DLC and mod lists, in a small window of their own. Rarely needed, so they no longer take a tab.</summary>
        private void ShowContent()
        {
            if (contentDialog == null)
            {
                float k = DeviceDpi / 96f;
                contentDialog = new Form
                {
                    Text = "DLC and mods",
                    StartPosition = FormStartPosition.CenterParent,
                    ShowInTaskbar = false,
                    MinimizeBox = false,
                    AutoScaleMode = AutoScaleMode.None,
                    ClientSize = new Size((int)(520 * k), (int)(560 * k)),
                    Padding = new Padding((int)(12 * k))
                };
                contentLayout.AutoSize = false; // fill the window; an auto-sized table grows past it and hides the buttons
                contentLayout.ColumnStyles.Clear();
                contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                contentLayout.Dock = DockStyle.Fill;
                contentLayout.Visible = true;
                contentDialog.Controls.Add(contentLayout);
                // Closing only hides it, so the lists keep their ticks; closing the main window still closes it.
                contentDialog.FormClosing += (s, e) => { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; contentDialog.Hide(); } };
            }
            contentDialog.Font = Font;
            contentDialog.BackColor = look == Look.Medieval ? Wood : SystemColors.Control;
            contentDialog.ForeColor = look == Look.Medieval ? Vellum : SystemColors.ControlText;
            StyleTree(contentLayout, true);
            if (!contentDialog.Visible) contentDialog.Show(this); else contentDialog.Activate();
        }
        private void ShowMoreMenu(Control anchor) =>
            moreMenu.Show(anchor, new Point(anchor.Width, anchor.Height), ToolStripDropDownDirection.BelowLeft);

        // ---------------------------------------------------------------- appearance

#pragma warning disable WFO5001 // dark mode support is still marked experimental
        private void ApplyColorMode() =>
            Application.SetColorMode(themeMode switch { "light" => SystemColorMode.Classic, "dark" => SystemColorMode.Dark, _ => SystemColorMode.System });
#pragma warning restore WFO5001

        private void SetTheme(string mode)
        {
            themeMode = mode;
            miFollow.Checked = mode == "system"; miLight.Checked = mode == "light"; miDark.Checked = mode == "dark";
            ReapplyTheme();
            SetStatus(mode == "system" ? "Following the Windows light or dark setting" : $"{(mode == "dark" ? "Dark" : "Light")} appearance");
        }

        /// <summary>Re-read the colours and redraw.</summary>
        private void ReapplyTheme()
        {
            ApplyColorMode();
            ApplyLook(look);
            // Making the windows again can raise change events; keep them from redrawing or editing the portrait.
            bool wasStarted = started;
            started = false; suppressDraw = true;
            SuspendLayout();
            try { RecreateNativeControls(this); }
            finally { ResumeLayout(true); started = wasStarted; suppressDraw = false; }
            SetTitleBarDark(IsDarkNow());
            Invalidate(true);
        }

        private static readonly System.Reflection.MethodInfo RecreateHandle =
            typeof(Control).GetMethod("RecreateHandle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        /// <summary>Drop-downs, tick boxes and text boxes take their theme when their window is made; make it again so they follow a change.</summary>
        private static void RecreateNativeControls(Control root)
        {
            foreach (Control c in root.Controls)
            {
                if (c is ComboBox or CheckBox or TextBoxBase && c.IsHandleCreated) RecreateHandle?.Invoke(c, null);
                RecreateNativeControls(c);
            }
        }

        private static bool IsDarkNow()
        {
#pragma warning disable WFO5001
            return Application.IsDarkModeEnabled;
#pragma warning restore WFO5001
        }

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        private void SetTitleBarDark(bool dark)
        {
            if (!IsHandleCreated) return;
            int value = dark ? 1 : 0;
            DwmSetWindowAttribute(Handle, 20, ref value, sizeof(int)); // DWMWA_USE_IMMERSIVE_DARK_MODE
        }

        private void OnWindowsPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (themeMode != "system" || e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Color)) return;
            if (IsHandleCreated) BeginInvoke(new Action(ReapplyTheme));
        }

        // ---------------------------------------------------------------- what gets saved

        /// <summary>Draw the current portrait the way it will be saved. May return the preview image itself; use <see cref="Release"/>.</summary>
        private SKImage RenderForExport(Character c)
        {
            if (!miTransparent.Checked && !miFrameless.Checked && ReferenceEquals(c, character)) return previewImage;
            var steps = PortraitBuilder.Engine.PortraitBuilder
                .BuildCharacter(c, loader.ActivePortraitData.Sprites, !miTransparent.Checked, !miFrameless.Checked)
                .Where(s => s != null).ToList();
            Parallel.ForEach(steps, s => s.Tile = loader.Cache.Get(s.Def)[s.TileIndex]);
            using var bmp = PortraitRenderer.DrawPortrait(steps);
            return SKImage.FromBitmap(bmp);
        }

        /// <summary>Dispose an image unless it is the one the window still shows.</summary>
        private void Release(SKImage image) { if (!ReferenceEquals(image, previewImage)) image.Dispose(); }

        private void OnClickSave(object sender, EventArgs e)
        {
            string file = Snippets.SaveFileDialog("Save Image", "PNG|*.png", null);
            if (string.IsNullOrEmpty(file)) return;

            var source = RenderForExport(character);
            var export = PortraitRenderer.Upscale(source, ExportScale);
            try
            {
                using var fs = File.Create(file);
                export.Encode().SaveTo(fs);
                var notes = new List<string>();
                if (miTransparent.Checked) notes.Add("no background");
                if (miFrameless.Checked) notes.Add("no frame");
                SetStatus($"Saved {export.Width} x {export.Height} px{(notes.Count > 0 ? ", " + string.Join(", ", notes) : "")}: {Path.GetFileName(file)}");
            }
            finally
            {
                if (!ReferenceEquals(export, source)) export.Dispose();
                Release(source);
            }
        }

        // ---------------------------------------------------------------- batches

        /// <summary>
        /// Make <paramref name="count"/> random portraits using the same ticks as Randomize (features, type, rank), handing each to <paramref name="each"/>.
        /// The portrait on screen is put back afterwards and the history is left alone.
        /// </summary>
        private void RandomPortraits(int count, int scale, Action<int, SKImage> each)
        {
            var keep = new HistoryEntry(character.DNA, character.Properties, cbPortraitTypes.SelectedItem as string, cbCulturePortraitTypes.SelectedItem as string,
                Math.Max(0, cbRank.SelectedIndex), Math.Max(0, cbGovernment.SelectedIndex));
            UseWaitCursor = true;
            suppressDraw = true;
            try
            {
                for (int i = 0; i < count; i++)
                {
                    label22.Text = $"Making portrait {i + 1} of {count}…";
                    label22.Refresh();
                    if (chkRandomType.Checked && cbPortraitTypes.Items.Count > 1)
                        cbPortraitTypes.SelectedIndex = Random.Shared.Next(cbPortraitTypes.Items.Count);
                    started = false;
                    try { RandomizeDisplayCharacteristics(chkRandomRank.Checked); }
                    finally { started = true; }

                    var source = RenderForExport(character);
                    var image = PortraitRenderer.Upscale(source, scale);
                    try { each(i, image); }
                    finally
                    {
                        if (!ReferenceEquals(image, source)) image.Dispose();
                        Release(source);
                    }
                }
            }
            finally
            {
                suppressDraw = false;
                UseWaitCursor = false;
                ApplyState(keep);
            }
        }

        private void SaveBatch(int count)
        {
            using var dialog = new FolderBrowserDialog { Description = $"Choose a folder for {count} random portraits", UseDescriptionForTitle = true };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            RandomPortraits(count, ExportScale, (i, image) =>
            {
                using var fs = File.Create(Path.Combine(dialog.SelectedPath, $"portrait-{stamp}-{i + 1:00}.png"));
                image.Encode().SaveTo(fs);
            });
            SetStatus($"Saved {count} portraits to {dialog.SelectedPath}");
        }

        private void SaveContactSheet(int count)
        {
            string file = Snippets.SaveFileDialog("Save contact sheet", "PNG|*.png", null);
            if (string.IsNullOrEmpty(file)) return;
            const int columns = 5;
            int rows = (count + columns - 1) / columns, cell = 176 * 2;
            using var sheet = new SKBitmap(columns * cell, rows * cell, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(sheet))
            {
                canvas.Clear(SKColors.Transparent);
                RandomPortraits(count, 2, (i, image) => canvas.DrawImage(image, (i % columns) * cell, (i / columns) * cell));
            }
            using var fs = File.Create(file);
            using var picture = SKImage.FromBitmap(sheet);
            picture.Encode().SaveTo(fs);
            SetStatus($"Saved a contact sheet of {count}: {Path.GetFileName(file)}");
        }
    }
}
