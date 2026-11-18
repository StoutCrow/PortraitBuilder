using Microsoft.Extensions.Logging;
using PortraitBuilder.Engine;
using PortraitBuilder.Model;
using PortraitBuilder.Model.Content;
using PortraitBuilder.Model.Portrait;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace PortraitBuilder.UI
{

    /// <summary>
    /// Controller class
    /// </summary>
    public partial class PortraitBuilderForm : Form
    {

        private static readonly ILogger logger = LoggingHelper.CreateLogger<PortraitBuilderForm>();

        private SKImage previewImage;

        private bool started = false;

        private Boolean nextToogleIsSelectAll = true;

        private GameLoader loader;

        private User _user;

        /// <summary>
        /// List of all available DLCs and Mods, indexed by their corresponding checkbox
        /// </summary>
        private readonly Dictionary<CheckBox, Content> usableContents = new();

        /// <summary>
        /// The portrait being previewed. 
        /// 
        /// This is the primary Model object, whose state is modified by UI inputs, and used to display the output.
        /// </summary>
        private readonly Character character = new();

        /// <summary>
        /// ComboBox for vanilla dna, ordered by their dna index.
        /// </summary>
        private readonly Dictionary<Characteristic, ComboBox> dnaComboBoxes = new();

        /// <summary>
        /// ComboBox for vanilla properties, ordered by their properties index.
        /// </summary>
        private readonly Dictionary<Characteristic, ComboBox> propertiesComboBoxes = new();

        /// <summary>
        /// ComboBox for custom mod properties, ordered by their properties index, and dynamically refreshed per portraitType.
        /// </summary>
        private readonly Dictionary<Characteristic, ComboBox> customPropertiesComboBoxes = new();

        private readonly ToolTip toolTip = new();

        private ComboBox cbExportScale;
        private static readonly int[] ExportScales = { 1, 2, 4, 8 };
        private int ExportScale => ExportScales[Math.Max(0, cbExportScale.SelectedIndex)];

        private CheckBox chkRandomType, chkRandomRank;
        private Chip btnBack, btnForward;

        /// <summary>Everything needed to redraw a portrait exactly: DNA/properties are only meaningful for their portrait type.</summary>
        private readonly record struct HistoryEntry(string Dna, string Props, string Type, string Culture, int Rank, int Government);

        /// <summary>Portraits seen so far, so Randomize can be undone.</summary>
        private readonly List<HistoryEntry> history = new();
        private int historyPos = -1;

        /// <summary>While set, DrawPortrait is skipped; used so a multi-step change renders once at the end.</summary>
        private bool suppressDraw;

        public PortraitBuilderForm()
        {
            InitializeComponent();
            using (var iconStream = typeof(PortraitBuilderForm).Assembly.GetManifestResourceStream("app.ico"))
                if (iconStream != null) Icon = new Icon(iconStream); // the window and taskbar icon; the exe icon comes from ApplicationIcon
            AddFeatureControls();
            ApplyModernStyle();
            BuildLayout();

            foreach (var dna in DefaultCharacteristics.DNA)
            {
                RegisterDisplayCharacteristic(panelDNA, dna);
            }
            foreach (var property in DefaultCharacteristics.PROPERTIES)
            {
                RegisterDisplayCharacteristic(panelProperties, property);
            }

            ShowDeferredSection();

            InitializeForm();
            LoadGame(false);
            started = true;
            RestoreLast();
            PushHistory("Starting portrait"); // the starting portrait is the first entry in the strip
        }

        /// <summary>
        /// Flat buttons, one accent on the primary action, roomier panels. Colours come from the system theme so dark mode works.
        /// </summary>
        private void ApplyModernStyle()
        {
            void Walk(Control parent)
            {
                foreach (Control c in parent.Controls)
                {
                    if (c is Button b)
                    {
                        b.FlatStyle = FlatStyle.Flat;
                        b.FlatAppearance.BorderColor = SystemColors.ControlDark;
                        b.UseVisualStyleBackColor = false;
                        b.BackColor = SystemColors.ControlLight;
                        b.ForeColor = SystemColors.ControlText;
                        b.Cursor = Cursors.Hand;
                    }
                    else if (c is FlowLayoutPanel p)
                    {
                        p.Padding = new Padding(6);
                    }
                    Walk(c);
                }
            }
            Walk(this);
            foreach (var primary in new[] { btnRandom })
            {
                primary.BackColor = Color.FromArgb(0, 95, 184);
                primary.ForeColor = Color.White;
                primary.FlatAppearance.BorderColor = primary.BackColor;
            }
            pbPortrait.SizeMode = PictureBoxSizeMode.Zoom;
            foreach (var tabs in new[] { tabControl1, tabContent })
            {
                tabs.SizeMode = TabSizeMode.Fixed;
                tabs.ItemSize = new Size(110, 26);
            }
            foreach (var panel in new[] { panelDNA, panelProperties, panelDLCs, panelMods })
            {
                panel.BorderStyle = BorderStyle.None;
            }
        }

        /// <summary>
        /// Controls added in code: randomize options, undo/redo, and a resizable layout.
        /// </summary>
        private void AddFeatureControls()
        {
            chkRandomType = new CheckBox { Text = "Random portrait type", Location = new Point(230, 24), AutoSize = true };
            chkRandomRank = new CheckBox { Text = "Random rank and government", Location = new Point(230, 52), AutoSize = true };
            groupBox1.Controls.Add(chkRandomType);
            groupBox1.Controls.Add(chkRandomRank);

            btnBack = new Chip { Text = "Undo", Size = new Size(72, 36) };
            btnForward = new Chip { Text = "Redo", Size = new Size(72, 36) };
            btnBack.Click += (s, e) => GoHistory(-1);
            btnForward.Click += (s, e) => GoHistory(1);
            groupBox2.Controls.Add(btnBack);
            groupBox2.Controls.Add(btnForward);

            cbExportScale = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(224, 512), Width = 130, Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
            cbExportScale.Items.AddRange(ExportScales.Select(s => $"Export {s}x ({176 * s} px)").ToArray());
            cbExportScale.SelectedIndex = 2; // 4x
            groupBox1.Controls.Add(cbExportScale);
            toolTip.SetToolTip(cbExportScale, "Size of the saved PNG. The game art is 176 px, so larger sizes are smoothly resampled, not more detailed.");

            // The "online" comparison image points at a local Azure Function that is not part of this app; hide it and give the real portrait the room.
            pbPortraitOnline.Visible = false;
            lnkLblRemotePortrait.Visible = false;
            pbPortrait.Location = new Point(7, 90);
            pbPortrait.Size = new Size(426, 255);
            tbDNA.Size = new Size(426, 75);

            // Resizable: the output panel takes extra width, all panels take extra height.
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimumSize = Size;
            const AnchorStyles tl = AnchorStyles.Top | AnchorStyles.Left, tlb = tl | AnchorStyles.Bottom, all = tlb | AnchorStyles.Right, bl = AnchorStyles.Bottom | AnchorStyles.Left;
            groupBox3.Anchor = tlb; groupBox2.Anchor = tlb; groupBox1.Anchor = all;
            tabContent.Anchor = all; tabControl1.Anchor = all;
            btnToogleAll.Anchor = bl; btnReload.Anchor = bl; btnImport.Anchor = bl; btnRandom.Anchor = bl; btnSave.Anchor = bl; btnCopy.Anchor = bl;
        }

        private readonly List<string> histLabels = new();
        private readonly List<DateTime> histTimes = new();

        private void PushHistory(string label = "Edited")
        {
            if (character?.DNA == null) return;
            var state = new HistoryEntry(character.DNA, character.Properties,
                cbPortraitTypes.SelectedItem?.ToString(), cbCulturePortraitTypes.SelectedItem?.ToString(),
                cbRank.SelectedIndex, cbGovernment.SelectedIndex);
            if (historyPos >= 0 && history[historyPos] == state) return;
            // Pushing after going back drops the portraits that were "ahead".
            for (int j = historyPos + 1; j < thumbs.Count; j++) thumbs[j].Dispose();
            if (historyPos + 1 < thumbs.Count)
            {
                thumbs.RemoveRange(historyPos + 1, thumbs.Count - historyPos - 1);
                histLabels.RemoveRange(historyPos + 1, histLabels.Count - historyPos - 1);
                histTimes.RemoveRange(historyPos + 1, histTimes.Count - historyPos - 1);
            }
            history.RemoveRange(historyPos + 1, history.Count - historyPos - 1);
            history.Add(state);
            thumbs.Add(MakeThumb());
            histLabels.Add(label);
            histTimes.Add(DateTime.Now);
            if (history.Count > 60) { history.RemoveAt(0); thumbs[0].Dispose(); thumbs.RemoveAt(0); histLabels.RemoveAt(0); histTimes.RemoveAt(0); }
            historyPos = history.Count - 1;
            RefreshHistoryStrip();
            logger.LogDebug("History push #{n}: {type} {dna}", historyPos, state.Type, state.Dna);
        }

        private static void SelectByText(ComboBox cb, string text)
        {
            int index = text == null ? -1 : cb.Items.IndexOf(text);
            if (index >= 0 && cb.SelectedIndex != index) cb.SelectedIndex = index; // fires OnChangePortraitType for the type boxes
        }

        private void GoHistory(int delta)
        {
            PushHistory(); // so going back from a fresh portrait can come forward again
            int i = historyPos + delta;
            if (i < 0 || i >= history.Count) return;
            historyPos = i;
            var h = history[i];
            logger.LogDebug("History go {delta} -> #{i}/{count}: {type} {dna}", delta, i, history.Count, h.Type, h.Dna);

            ApplyState(h);
            RefreshHistoryStrip();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Control | Keys.R:
                case Keys.F5:
                    OnClickRandomize(this, EventArgs.Empty); return true;
                case Keys.Control | Keys.V:
                    // Leave Ctrl+V alone while typing in a field; otherwise paste a dna/properties pair.
                    if (ActiveControl is not (TextBoxBase or ComboBox) && TryPasteDna()) return true;
                    break;
                case Keys.F1: ShowShortcuts(); return true;
                case Keys.Control | Keys.Z: GoHistory(-1); return true;
                case Keys.Control | Keys.Y: GoHistory(1); return true;
                case Keys.Control | Keys.S: OnClickSave(this, EventArgs.Empty); return true;
                case Keys.Control | Keys.D: KeepCurrent(); return true;
                case Keys.Control | Keys.Shift | Keys.C:
                    if (pbPortrait.Image != null) Clipboard.SetImage(pbPortrait.Image);
                    return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void InitializeForm()
        {
            logger.LogInformation("Portrait Builder Version {version}", Application.ProductVersion);
            // Add the version to title
            this.Text += $" {Application.ProductVersion}";

            InitializeTooltip();

            var gameDir = ReadGameDir();
            _user = new User
            {
                GameDir = gameDir,
                ModDir = ReadModDir(gameDir),
                DlcDir = Path.Combine(Environment.CurrentDirectory, "dlc/")
            };
            logger.LogInformation("Configuration: {user}", _user);
            logger.LogInformation("----------------------------");

            loader = new GameLoader();
        }

        private void InitializeTooltip()
        {
            // Set up the delays for the ToolTip.
            toolTip.AutoPopDelay = 5000;
            toolTip.InitialDelay = 1000;
            toolTip.ReshowDelay = 500;
            // Force the ToolTip text to be displayed whether or not the form is active.
            toolTip.ShowAlways = true;

            // Set up the ToolTip text for form controls.
            toolTip.SetToolTip(this.btnToogleAll, "Check or uncheck checkboxes in active tab");
            toolTip.SetToolTip(this.btnReload, "Reload all data from folders");
            toolTip.SetToolTip(this.btnImport, "Import DNA and Properties strings");
            toolTip.SetToolTip(this.btnRandom, "Choose random values for dna/properties that are checked (Ctrl+R or F5). Undo: Ctrl+Z, redo: Ctrl+Y.");
            toolTip.SetToolTip(this.chkRandomType, "Randomize also picks a random portrait type (culture look)");
            toolTip.SetToolTip(this.chkRandomRank, "Randomize also picks a random rank and government");
            toolTip.SetToolTip(this.btnBack, "Previous portrait (Ctrl+Z)");
            toolTip.SetToolTip(this.btnForward, "Next portrait (Ctrl+Y)");
            toolTip.SetToolTip(this.btnSave, "Save the portrait as PNG (Ctrl+S). Ctrl+Shift+C copies the image.");
            toolTip.SetToolTip(this.btnSave, "Save portrait as a .png image");
            toolTip.SetToolTip(this.btnCopy, "Copy DNA & Properties to use for character history");

            toolTip.SetToolTip(this.cbPortraitTypes, "Select portraitType to render as base");
            toolTip.SetToolTip(this.cbCulturePortraitTypes, "Select portraitType to render for clothing override");
            toolTip.SetToolTip(this.cbRank, "Select rank to use for rendering portrait border");
            toolTip.SetToolTip(this.cbGovernment, "Select government to use for rendering. Theocracy and Merchant Republic use special sprites for headgear and clothing.");
        }

        private void LoadGame(bool clean)
        {
            logger.LogInformation("----------------------------");
            logger.LogInformation("(Re-)loading data");

            loader.LoadVanilla(_user.GameDir);
            LoadDLCs(_user.GameDir, _user.DlcDir, clean);
            LoadMods(_user.ModDir);

            LoadPortraitTypes();
            FillDisplayCharacteristicComboBoxes();
            RandomizeDisplayCharacteristics(true);

            DrawPortrait();
        }

        private void LoadMods(string modDir)
        {
            List<Mod> mods = loader.LoadMods(modDir);
            panelMods.Controls.Clear();

            foreach (var mod in mods)
            {
                RegisterContent(panelMods, mod);
            }
        }

        private void LoadDLCs(string gameDir, string dlcDir, bool clean)
        {
            var dlcs = loader.LoadDLCs(gameDir, dlcDir, clean);
            panelDLCs.Controls.Clear();

            foreach (var dlc in dlcs)
            {
                if (dlc.HasPortraitData)
                {
                    RegisterContent(panelDLCs, dlc);
                }
                else
                {
                    // List every installed DLC so the list matches the game folder; only portrait packs can be toggled.
                    var row = new CheckBox { Text = dlc.Name, Width = 240, AutoCheck = false, AutoEllipsis = true, ForeColor = Color.Gray, Margin = new Padding(0) };
                    toolTip.SetToolTip(row, $"{dlc.Name}: installed, but it has no portrait data");
                    panelDLCs.Controls.Add(row);
                }
            }
            logger.LogInformation("{count} DLC(s) installed, {withPortraits} with portrait data", dlcs.Count, dlcs.Count(d => d.HasPortraitData));
        }

        private void RegisterContent(Control container, Content content)
        {
            CheckBox checkbox = new();
            checkbox.Text = content.Name;
            checkbox.AutoEllipsis = true;
            checkbox.Width = 240; // Force overflow
            toolTip.SetToolTip(checkbox, content.Name); // full name when the label is ellipsized
            checkbox.CheckedChanged += this.OnCheckContent;
            checkbox.Padding = new Padding(0);
            checkbox.Margin = new Padding(0);

            container.Controls.Add(checkbox);
            usableContents.Add(checkbox, content);

            if (content is Mod)
            {
                if (content.Enabled)
                {
                    toolTip.SetToolTip(checkbox, "Toggle activation and file watching of this mod");
                    content.Watcher = CreateModFilesWatcher(content);
                }
                else
                {
                    // Note: can't use checkbox.Enabled since it disables tooltips as well !
                    checkbox.ForeColor = Color.Gray; // Read-only appearance
                    checkbox.AutoCheck = false; // Read-only behavior
                    toolTip.SetToolTip(checkbox, content.DisabledReason);
                }
            }
            else
            {
                toolTip.SetToolTip(checkbox, "Toggle activation of this DLC");
            }
        }

        /// <summary>
        /// One section per feature: a header (name, "3 of 12", reroll, include-in-randomize tick) over a grid of pictures.
        /// The drop-down only holds the value; it is never shown, so there is nothing to open or step through.
        /// </summary>
        private void RegisterDisplayCharacteristic(Control container, Characteristic characteristic)
        {
            ComboBox combobox = new() { DropDownStyle = ComboBoxStyle.DropDownList };
            combobox.SelectedValueChanged += this.OnChangeDisplayCharacteristic;

            // Face features (DNA) on the Face tab, the main look (hair, clothes...) on Look, everything else on Extras.
            var feature = new FeatureRow { Ch = characteristic, Cb = combobox };
            feature.Page = characteristic.Type == CharacteristicType.DNA ? 1 : (characteristic.Randomizable || characteristic.Custom) ? 2 : 4;
            FlowLayoutPanel host = feature.Page == 1 ? panelDNA : feature.Page == 2 ? panelProperties : panelExtras;

            var name = new Label
            {
                Text = System.Text.RegularExpressions.Regex.Replace(characteristic.ToString(), @"\s*\([dp]\d+\)$", ""),
                Tag = "heading", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0)
            };
            toolTip.SetToolTip(name, characteristic.ToString());
            feature.Value = new Label { Tag = "muted", AutoSize = true, Anchor = AnchorStyles.Right, Margin = new Padding(0, 0, Px(10), 0) };

            feature.Dice = new Chip { Kind = ChipKind.Ghost, Text = "↻", Size = new Size(Px(34), Px(34)), Anchor = AnchorStyles.None, Margin = new Padding(0), TabStop = false, Font = new Font("Segoe UI Symbol", 13F) };
            toolTip.SetToolTip(feature.Dice, "Pick a random value for this feature only");
            feature.Dice.Click += (s, e) =>
            {
                if (combobox.Items.Count < 2) return;
                int next;
                do next = Random.Shared.Next(combobox.Items.Count); while (next == combobox.SelectedIndex);
                Recorded($"{characteristic}: {next + 1}", () => combobox.SelectedIndex = next);
            };

            CheckBox randomizable = new() { AutoSize = true, Text = "", Anchor = AnchorStyles.None, Margin = new Padding(Px(8), 0, Px(6), 0) };
            randomizable.DataBindings.Add("Checked", characteristic, "Randomizable");
            toolTip.SetToolTip(randomizable, "Include this feature when randomizing");

            feature.Strip = new TilePicker { TileWidth = feature.Page == 1 ? 64 : feature.Page == 4 ? 56 : 68, Margin = new Padding(0, 0, 0, Px(12)), Tag = characteristic };

            var row = new HoverRow { Height = Px(38), Margin = new Padding(0, 0, 0, Px(2)), Tag = characteristic };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int c = 0; c < 3; c++) row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            row.ColumnCount = 4;
            row.Controls.Add(name, 0, 0);
            row.Controls.Add(feature.Value, 1, 0);
            row.Controls.Add(feature.Dice, 2, 0);
            row.Controls.Add(randomizable, 3, 0);
            feature.Row = row;

            // Background is the longest section (25 tiles) and the least often changed, so it is shown last (see ShowDeferredSection).
            // Only the place on screen changes; the dna and properties letters still follow the order the features were registered in.
            if (characteristic == DefaultCharacteristics.BACKGROUND) deferredSection = (host, feature);
            else AddSection(host, feature);

            combobox.EnabledChanged += (s, e) => RefreshRowVisibility(feature);
            combobox.SelectedIndexChanged += (s, e) => RefreshRowVisibility(feature);
            featureRows[characteristic] = feature;
            RefreshRowVisibility(feature);

            if (characteristic.Type == CharacteristicType.DNA)
            {
                dnaComboBoxes.Add(characteristic, combobox);
            }
            else if (characteristic.Custom)
            {
                customPropertiesComboBoxes.Add(characteristic, combobox);
            }
            else
            {
                propertiesComboBoxes.Add(characteristic, combobox);
            }
        }
        private (FlowLayoutPanel Host, FeatureRow Section)? deferredSection;

        private void AddSection(FlowLayoutPanel host, FeatureRow feature)
        {
            // Row and strip travel together in one box, so the page can never pair a header with another feature's tiles.
            var box = new TableLayoutPanel { ColumnCount = 1, RowCount = 2, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0), Padding = new Padding(0), Tag = feature.Ch };
            box.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            box.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            box.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            box.Controls.Add(feature.Row, 0, 0);
            box.Controls.Add(feature.Strip, 0, 1);
            feature.Box = box;
            host.Controls.Add(box);
            host.SetFlowBreak(box, true);
            feature.Box.Visible = feature.Row.Visible || feature.Strip.Visible;
            FitRows(host);
        }

        private void ShowDeferredSection()
        {
            if (deferredSection is not { } d) return;
            deferredSection = null;
            AddSection(d.Host, d.Section);
        }

        private void UnregisterCustomProperties()
        {
            // Each custom feature added a row and a strip of tiles, both tagged with its characteristic.
            foreach (var custom in customPropertiesComboBoxes.Keys)
            {
                featureRows.Remove(custom);
                foreach (Control c in panelProperties.Controls.Cast<Control>().Where(c => c.Tag == (object)custom).ToList())
                {
                    panelProperties.Controls.Remove(c);
                    c.Dispose();
                }
            }
            customPropertiesComboBoxes.Clear();
        }

        private static string ReadGameDir()
        {
            var gameDir = File.ReadAllText("gamedir.txt");
            logger.LogInformation("Read gamedir: {gameDir}", gameDir);
            return gameDir;
        }

        /// <summary>
        /// Read userdir.txt in Steam directory for the path to mod dir, or default to pre-defined location
        /// </summary>
        private static string ReadModDir(string gameDir)
        {
            string userDir = GetDefaultUserDir();
            string userdirFilePath = Path.Combine(gameDir, "userdir.txt");
            if (File.Exists(userdirFilePath))
            {
                logger.LogInformation("Reading userdir.txt to determine the mod directory.");
                Stream stream = new FileStream(userdirFilePath, FileMode.Open);
                StreamReader reader = new(stream, Encoding.Default);
                userDir = reader.ReadLine() + Path.DirectorySeparatorChar;
                logger.LogInformation("Found userdir.txt with path: {userDir}", userDir);
            }
            return Path.Combine(userDir, "mod");
        }

        private static string GetDefaultUserDir()
        {
            var myDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return Path.Combine(myDocuments, "Paradox Interactive", "Crusader Kings II");
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                // "Environment.SpecialFolder.MyDocuments does not add /Documents/ on Mac"
                // JZ: verify if this is true in corefx
                return Path.Combine(myDocuments, "Documents", "Paradox Interactive", "Crusader Kings II");
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return Path.Combine(myDocuments, ".paradoxinteractive", "Crusader Kings II");
            else
            {
                logger.LogError("Unknown operating system, cannot lookup user dir. OS: {os}", RuntimeInformation.OSDescription);
                return null;
            }
        }

        /// <summary>
        /// Entry point for re-drawing based on updated portrait.
        /// </summary>
        private void DrawPortrait()
        {
            if (suppressDraw) return;
            pbPortrait.Image?.Dispose();

            try
            {
                var steps = PortraitBuilder.Engine.PortraitBuilder.BuildCharacter(character, loader.ActivePortraitData.Sprites)
                    .Where(step => step != null)
                    .ToList();

                // A new portrait type decodes ~40 sprite sheets on first use; the cache is thread-safe, so do them in parallel.
                System.Threading.Tasks.Parallel.ForEach(steps, step =>
                {
                    step.Tile = loader.Cache.Get(step.Def)[step.TileIndex];
                });

                var rendered = PortraitRenderer.DrawPortrait(steps);
                previewImage = SKImage.FromBitmap(rendered);
            }
            catch (Exception e)
            {
                logger.LogError("Error encountered rendering portrait", e);
                return;
            }

            // Show a smooth 4x version; GDI+ would otherwise stretch the 176 px image with blurry bilinear scaling.
            using var big = PortraitRenderer.Upscale(previewImage, 4);
            pbPortrait.Image = Image.FromStream(big.Encode().AsStream(true));
            if (!statusTimer.Enabled) label22.Text = DefaultNote;
            SchedulePickers();
        }

        private string GetOnlinePortraitUrl()
        {
            var (@base, clothing) = GetSelectedGraphicalCultures();
            string host;
#if DEBUG
            host = "http://localhost:7071/api";
#else
            host = "https://portrait.ga";
#endif
            var baseUrl = $"{host}/portrait?dna={character.DNA}&properties={character.Properties}&base={@base}";
            var clothingUrl = !string.IsNullOrEmpty(clothing) ? $"&clothing={clothing}" : string.Empty;
            var govtUrl = $"&government={character.Government}";
            var titleUrl = character.Rank != TitleRank.None ? $"&titlerank={character.Rank}" : string.Empty;
            return baseUrl + clothingUrl + govtUrl + titleUrl;
        }

        private void OnRemotePortraitLinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (sender is not LinkLabel lnkLbl)
                return;

            string url;
            if (e.Link.LinkData != null)
                url = e.Link.LinkData.ToString();
            else
                url = lnkLbl.Text.Substring(e.Link.Start, e.Link.Length);

            Debug.Assert(url.Contains("://"));

            var si = new ProcessStartInfo(url) { UseShellExecute = true };
            Process.Start(si);
            lnkLbl.LinkVisited = true;
        }

        private static string GetDisplayCharacteristicsString(Dictionary<Characteristic, ComboBox> characteristics)
        {
            StringBuilder sb = new();
            foreach (ComboBox cb in characteristics.Values)
            {
                char letter = '0';
                if (cb != null)
                {
                    letter = GetLetter(cb);
                }
                sb.Append(letter);
            }
            return sb.ToString();
        }

        // Needs to be called each time portrait object is modified
        private void OutputDNA()
        {
            logger.LogDebug(" --Outputting DNA and Property strings.");
            StringBuilder dnaPropOutput = new();

            dnaPropOutput.Append("  dna=\"");
            dnaPropOutput.Append(character.DNA);
            dnaPropOutput.AppendLine("\"");

            dnaPropOutput.Append("  properties=\"");
            dnaPropOutput.Append(character.Properties);
            dnaPropOutput.AppendLine("\"");
            tbDNA.Text = dnaPropOutput.ToString();
        }

        private static char GetLetter(ComboBox cb) => Character.GetLetter(cb.SelectedIndex);

        /// <summary>
        /// Some very specific characristics are not randomized: scars, red dots, boils, prisoner, blinded.
        /// </summary>
        /// <param name="doRank"></param>
        private void RandomizeDisplayCharacteristics(bool doRank)
        {
            logger.LogDebug("Randomizing UI");
            if (doRank)
            {
                RandomizeComboBox(cbGovernment);
                RandomizeComboBox(cbRank);
            }

            RandomizeDisplayCharacteristics(dnaComboBoxes);
            RandomizeDisplayCharacteristics(propertiesComboBoxes);
            RandomizeDisplayCharacteristics(customPropertiesComboBoxes);

            UpdatePortrait(GetDisplayCharacteristicsString(dnaComboBoxes), GetDisplayCharacteristicsString(propertiesComboBoxes), GetDisplayCharacteristicsString(customPropertiesComboBoxes));
        }

        private static void RandomizeDisplayCharacteristics(Dictionary<Characteristic, ComboBox> cbs)
        {
            foreach (var pair in cbs.Where(kvp => kvp.Key.Randomizable))
            {
                RandomizeComboBox(pair.Value);
            }
        }

        private static void RandomizeComboBox(ComboBox cb)
        {
            if (cb.Items.Count > 0)
            {
                cb.SelectedIndex = Random.Shared.Next(cb.Items.Count);
            }
        }

        private static void FillComboBox(ComboBox cb, int count)
        {
            for (int i = 0; i < count; i++)
                cb.Items.Add(i);
        }

        private void FillComboBox(ComboBox cb, Characteristic characteristic)
        {
            cb.Items.Clear();
            PortraitType portraitType = character.PortraitType;
            if (portraitType == null)
                return;

            int frameCount = loader.ActivePortraitData.GetFrameCount(portraitType, characteristic);
            if (frameCount > 0)
            {
                logger.LogDebug("Item count for {portraitType} {characteristic} : {frameCount}", portraitType, characteristic, frameCount);
                cb.Enabled = true;
                FillComboBox(cb, frameCount);
                if (frameCount == 1)
                {
                    cb.Enabled = false; // Disable if only 1 frame, as there's no selection to do, for instance head (p2)
                }
            }
            else
            {
                // Normal: this portrait type has no sprite for the slot, so the dropdown is just disabled.
                logger.LogDebug("No frames for {portraitType} and {characteristic}, disabling dropdown.", portraitType, characteristic);
                cb.Enabled = false;
            }
        }

        private (string @base, string clothing) GetSelectedGraphicalCultures()
            => (cbPortraitTypes.SelectedItem?.ToString(), cbCulturePortraitTypes.SelectedItem?.ToString());

        private PortraitType GetSelectedPortraitType()
        {
            PortraitType selectedPortraitType = null;
            object selectedItem = cbPortraitTypes.SelectedItem;
            object selectedItem2 = cbCulturePortraitTypes.SelectedItem;
            if (selectedItem != null)
            {
                if (selectedItem2 != null && !selectedItem2.ToString().Equals(""))
                {
                    return loader.GetPortraitType("PORTRAIT_" + selectedItem.ToString(), "PORTRAIT_" + selectedItem2.ToString());
                }
                else
                {
                    return loader.GetPortraitType("PORTRAIT_" + selectedItem.ToString());
                }
            }
            return selectedPortraitType;
        }

        private void FillDisplayCharacteristicComboBoxes()
        {
            FillDisplayCharacteristicComboBoxes(dnaComboBoxes);
            FillDisplayCharacteristicComboBoxes(propertiesComboBoxes);
            FillDisplayCharacteristicComboBoxes(customPropertiesComboBoxes);
        }

        private void FillDisplayCharacteristicComboBoxes(Dictionary<Characteristic, ComboBox> cbs)
        {
            foreach (var pair in cbs.Where(kvp => kvp.Value != null))
            {
                FillComboBox(pair.Value, pair.Key);
                if (featureRows.TryGetValue(pair.Key, out var section)) RefreshRowVisibility(section);
            }
        }

        private void LoadPortraitTypes()
        {
            object previouslySelectedBasePortrait = null;
            object previouslySelectedOverridePortrait = null;

            if (cbPortraitTypes.SelectedItem != null)
            {
                previouslySelectedBasePortrait = cbPortraitTypes.Items[cbPortraitTypes.SelectedIndex];
            }
            if (cbCulturePortraitTypes.SelectedItem != null)
            {
                previouslySelectedOverridePortrait = cbCulturePortraitTypes.Items[cbCulturePortraitTypes.SelectedIndex];
            }
            cbPortraitTypes.Items.Clear();
            cbCulturePortraitTypes.Items.Clear();

            loader.LoadPortraits();

            if (loader.ActivePortraitData.PortraitTypes.Count == 0)
            {
                logger.LogCritical("No portrait types found.");
                return;
            }

            cbCulturePortraitTypes.Items.Add(""); // Empty = no override
            foreach (KeyValuePair<string, PortraitType> pair in loader.ActivePortraitData.PortraitTypes)
            {
                PortraitType portraitType = pair.Value;
                String portraitName = portraitType.Name.Replace("PORTRAIT_", "");
                if (portraitType.IsBasePortraitType())
                {
                    cbPortraitTypes.Items.Add(portraitName);
                }
                cbCulturePortraitTypes.Items.Add(portraitName);
            }

            if (previouslySelectedBasePortrait != null)
            {
                cbPortraitTypes.SelectedIndex = cbPortraitTypes.Items.IndexOf(previouslySelectedBasePortrait);
            }
            if (previouslySelectedOverridePortrait != null)
            {
                cbCulturePortraitTypes.SelectedIndex = cbCulturePortraitTypes.Items.IndexOf(previouslySelectedOverridePortrait);
            }
            if (cbPortraitTypes.SelectedIndex == -1)
            {
                cbPortraitTypes.SelectedIndex = 0;
            }
            if (cbCulturePortraitTypes.SelectedIndex == -1)
            {
                cbCulturePortraitTypes.SelectedIndex = 0;
            }
            character.PortraitType = GetSelectedPortraitType();
        }

        private void UpdateActiveAdditionalContent()
        {
            List<Content> activeContent = new();
            activeContent.AddRange(GetSelectedContent(panelDLCs));
            activeContent.AddRange(GetSelectedContent(panelMods));
            loader.UpdateActiveAdditionalContent(activeContent);
        }

        private List<Content> GetSelectedContent(Panel panel)
        {
            List<Content> selectedContent = new();
            foreach (Control control in panel.Controls)
            {
                CheckBox checkbox = (CheckBox)control;
                if (checkbox.Checked)
                {
                    selectedContent.Add(usableContents[checkbox]);
                }
            }
            return selectedContent;
        }

        private void UpdateSelectedDisplayCharacteristicValues(Character character)
        {
            foreach (var pair in dnaComboBoxes.Where(kvp => kvp.Value != null))
            {
                pair.Value.SelectedIndex = Character.GetIndex(character.DNA[pair.Key.Index], pair.Value.Items.Count);
            }

            foreach (var pair in propertiesComboBoxes.Where(kvp => kvp.Value != null))
            {
                pair.Value.SelectedIndex = Character.GetIndex(character.Properties[pair.Key.Index], pair.Value.Items.Count);
            }
        }

        private void UpdatePortrait(string dna, string properties, string customProperties)
        {
            character.Import(dna, properties + customProperties);
            OutputDNA();
        }

        private FileSystemWatcher CreateModFilesWatcher(Content content)
        {
            FileSystemWatcher watcher = new();

            watcher.Path = content.AbsolutePath;
            watcher.IncludeSubdirectories = true;

            // Do the filtering in event handlers, as watchers do not support Filter such as "*.gfx|*.txt|*.dds"
            watcher.Filter = "*.*";

            watcher.NotifyFilter = NotifyFilters.LastWrite;

            // Have the callbacks execute in Main thread
            watcher.SynchronizingObject = this;
            watcher.Changed += new FileSystemEventHandler(OnContentFileChanged);
            watcher.Created += new FileSystemEventHandler(OnContentFileChanged);
            watcher.Deleted += new FileSystemEventHandler(OnContentFileChanged);
            watcher.Renamed += new RenamedEventHandler(OnContentFileRenamed);

            watcher.Error += new ErrorEventHandler(OnWatcherError);

            // Begin watching.
            watcher.EnableRaisingEvents = true;
            return watcher;
        }

        private Content GetAssociatedContent(FileSystemWatcher watcher)
        {
            Content content = null;

            foreach (KeyValuePair<CheckBox, Content> pair in usableContents)
            {
                if (pair.Value.Watcher == watcher)
                {
                    content = pair.Value;
                    break;
                }
            }
            return content;
        }

        private void UpdateSelectedContent(List<CheckBox> cbs)
        {
            started = false;
            UpdateActiveAdditionalContent();
            LoadPortraitTypes();
            RefreshCustomDisplayCharacteristics();

            FillDisplayCharacteristicComboBoxes();
            // TODO No refresh of DNA/Properties needed (if ComboBox has less options ?)
            started = true;

            foreach (CheckBox cb in cbs)
            {
                if (usableContents[cb] is Mod content)
                {
                    content.Watcher.EnableRaisingEvents = cb.Checked;
                }
            }

            DrawPortrait();
        }

        private void RefreshCustomDisplayCharacteristics()
        {
            UnregisterCustomProperties();
            foreach (var dc in GetSelectedPortraitType().CustomCharacteristics)
            {
                if (!customPropertiesComboBoxes.ContainsKey(dc))
                {
                    RegisterDisplayCharacteristic(panelProperties, dc);
                }
            }
        }

        ///////////////////
        // Event handlers
        ///////////////////

        private void OnChangeDisplayCharacteristic(object sender, EventArgs e)
        {
            if (started)
            {
                // Assumption: customPropertiesComboBoxes are contiguous !
                UpdatePortrait(GetDisplayCharacteristicsString(dnaComboBoxes), GetDisplayCharacteristicsString(propertiesComboBoxes), GetDisplayCharacteristicsString(customPropertiesComboBoxes));
                DrawPortrait();
            }
        }

        private void OnChangeRank(object sender, EventArgs e)
        {
            character.Rank = (TitleRank)cbRank.SelectedIndex;
            DrawPortrait();
        }

        private void OnChangeGovernment(object sender, EventArgs e)
        {
            character.Government = (GovernmentType)cbGovernment.SelectedIndex;
            DrawPortrait();
        }

        private void OnClickCopy(object sender, EventArgs e)
        {
            Clipboard.SetText(tbDNA.Text);
            SetStatus("DNA copied. Ctrl+V pastes it back.");
        }

        private void OnClickImport(object sender, EventArgs e)
        {
            ImportDialog dialog = new();

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                started = false;

                UpdatePortrait(dialog.character.DNA, dialog.character.Properties, "");

                // Reflect on dropdown
                UpdateSelectedDisplayCharacteristicValues(character);

                started = true;

                DrawPortrait();
            }
        }

        private void OnClickRandomize(object sender, EventArgs e)
        {
            // A new portrait type loads and decodes its sprite sheets on first use (about a second), so show a busy cursor.
            UseWaitCursor = true;
            try { Randomize(); }
            finally { UseWaitCursor = false; }
        }

        private void Randomize()
        {
            PushHistory("Before randomizing");

            if (chkRandomType.Checked && cbPortraitTypes.Items.Count > 1)
            {
                // Fires OnChangePortraitType, which refills the dropdowns for the new type. Skip its redraw; we draw once below.
                suppressDraw = true;
                try { cbPortraitTypes.SelectedIndex = Random.Shared.Next(cbPortraitTypes.Items.Count); }
                finally { suppressDraw = false; }
            }

            started = false;
            RandomizeDisplayCharacteristics(chkRandomRank.Checked);
            started = true;

            DrawPortrait();
            PushHistory("Randomized");
        }

        /// <summary>
        /// Called each time a content CheckBox is ticked/unticked
        /// </summary>
        private void OnCheckContent(object sender, EventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            List<CheckBox> cbs = new();
            cbs.Add(cb);
            UpdateSelectedContent(cbs);
        }

        private void OnChangePortraitType(object sender, EventArgs e)
        {
            if (!started)
                return;

            started = false;

            PortraitType selectedPortraitType = GetSelectedPortraitType();
            character.PortraitType = selectedPortraitType;

            RefreshCustomDisplayCharacteristics();

            FillDisplayCharacteristicComboBoxes();
            UpdateSelectedDisplayCharacteristicValues(character);

            started = true;

            DrawPortrait();
        }

        private void OnClickReload(object sender, EventArgs e)
        {
            usableContents.Clear();
            LoadGame(true);
        }

        private void OnClickToggleAll(object sender, EventArgs e)
        {
            TabPage tabPage = tabContent.SelectedTab;
            List<CheckBox> cbs = new();
            foreach (CheckBox checkbox in tabPage.Controls[0].Controls)
            {
                // Remove handler so it doesn't trigger
                checkbox.CheckedChanged -= OnCheckContent;
                checkbox.Checked = nextToogleIsSelectAll;
                checkbox.CheckedChanged += OnCheckContent;
                cbs.Add(checkbox);
            }
            nextToogleIsSelectAll = !nextToogleIsSelectAll;
            UpdateSelectedContent(cbs);
        }

        private void OnContentFileChanged(object sender, FileSystemEventArgs e)
        {
            FileSystemWatcher watcher = sender as FileSystemWatcher;
            OnContentChange(watcher, e.FullPath);
        }

        private void OnContentFileRenamed(object sender, RenamedEventArgs e)
        {
            FileSystemWatcher watcher = sender as FileSystemWatcher;
            OnContentChange(watcher, e.OldFullPath);
        }

        private void OnContentChange(FileSystemWatcher watcher, string path)
        {
            // Workaround same change firing multiple events
            watcher.EnableRaisingEvents = false;

            Content content = GetAssociatedContent(watcher);

            if (content != null)
            {
                logger.LogInformation("Content change for {path} in content {content}", path, content);
                loader.RefreshContent(content);
                LoadPortraitTypes();
                FillDisplayCharacteristicComboBoxes();
                DrawPortrait();
            }
            else
            {
                logger.LogError("No content matched for watcher on file {path}", path);
            }

            watcher.EnableRaisingEvents = true;
        }

        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            logger.LogError(e.GetException(), "FileSystemWatcher unable to continue");
        }
    }
}



