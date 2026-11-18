using System;
using System.Drawing;
using System.Windows.Forms;

namespace PortraitBuilder.UI
{
    /// <summary>
    /// Layout of the main window, built in code. A toolbar on top of the portrait, one sidebar with underline tabs.
    /// The designer file still creates the controls (the logic uses them); this re-parents them into a docked layout.
    /// </summary>
    public partial class PortraitBuilderForm
    {
        private const int Gap = 8, Section = 16, SidebarWidth = 480;

        private static Label Heading(string text) => new()
        {
            Text = text,
            AutoSize = true,
            Tag = "heading",
            Font = new Font("Segoe UI Semibold", 10F),
            Margin = new Padding(0, 0, 0, Gap)
        };

        private static Label Muted(string text) => new()
        {
            Text = text,
            AutoSize = true,
            Tag = "muted",
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(0, 0, 0, Gap)
        };

        private static Label RowLabel(string text) => new()
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0)
        };

        private static Panel Hairline() => new()
        {
            Height = 1,
            Dock = DockStyle.Fill,
            Tag = "hairline",
            Margin = new Padding(0, Gap, 0, Section)
        };

        private static TableLayoutPanel Table(int columns) => new()
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = columns,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };

        /// <summary>A label and a control on one row; the control takes the rest of the width.</summary>
        private static void AddRow(TableLayoutPanel t, string label, Control control)
        {
            int row = t.RowCount++;
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            control.Dock = DockStyle.Fill;
            control.Margin = new Padding(0, 3, 0, 3);
            t.Controls.Add(RowLabel(label), 0, row);
            t.Controls.Add(control, 1, row);
        }

        private static void AddFull(TableLayoutPanel t, Control control, SizeType type = SizeType.AutoSize)
        {
            int row = t.RowCount++;
            t.RowStyles.Add(new RowStyle(type, type == SizeType.Percent ? 100 : 0));
            t.Controls.Add(control, 0, row);
            t.SetColumnSpan(control, t.ColumnCount);
        }

        private static TableLayoutPanel LabelColumns()
        {
            var t = Table(2);
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            t.Margin = new Padding(0);
            return t;
        }

        private static Chip SizedChip(Chip chip, string text, int width, int height = 36, ChipKind kind = ChipKind.Normal)
        {
            chip.Text = text;
            chip.Kind = kind;
            chip.Size = new Size(width, height);
            chip.Anchor = AnchorStyles.None;
            chip.Margin = new Padding(0, 0, Gap, 0);
            return chip;
        }

        private void BuildLayout()
        {
            SuspendLayout();
            BuildNameControls();
            BuildPortraitMenu();
            EnsureCards();
            BuildPickers();
            BuildMoreMenu();

            // Scale by display DPI only. The look changes the font at runtime, and font-based scaling would resize the form each time.
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);

            // Old group boxes go; their children are re-parented below. Controls the new layout does not show are kept hidden.
            foreach (var g in new Control[] { groupBox1, groupBox2, groupBox3 }) Controls.Remove(g);
            foreach (var hidden in new Control[] { pbPortraitOnline, lnkLblRemotePortrait }) { hidden.Visible = false; Controls.Add(hidden); }

            Padding = new Padding(Section);
            ClientSize = new Size(1320, 720);
            MinimumSize = new Size(1280, 660);
            Text = Text.Replace("Portrait Builder", "CK2 Portrait Builder");

            // ---- left: toolbar, the portrait, the strip of recent portraits, then the DNA text ----
            SizedChip(btnRandom, "Randomize", 124, 36, ChipKind.Primary);
            SizedChip(btnBack, "Undo", 72);
            SizedChip(btnForward, "Redo", 72);
            SizedChip(btnSave, "Save image", 112);
            btnSave.Margin = new Padding(0);
            toolTip.SetToolTip(btnBack, "Previous portrait (Ctrl+Z)");
            toolTip.SetToolTip(btnForward, "Next portrait (Ctrl+Y)");

            var toolsLeft = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0), Anchor = AnchorStyles.Left };
            var btnKeep = SizedChip(new Chip(), "Keep", 64, 36);
            btnKeep.Click += (s, e) => KeepCurrent();
            toolTip.SetToolTip(btnKeep, "Keep this portrait in the Saved tab (Ctrl+D)");
            toolsLeft.Controls.AddRange(new Control[] { btnRandom, btnBack, btnForward, btnKeep });

            // Less used actions (export options, batch export, appearance, shortcuts) live in one menu.
            var btnMore = SizedChip(new Chip(), "More ▾", 76, 36);
            btnMore.Click += (s, e) => ShowMoreMenu(btnMore);
            toolTip.SetToolTip(btnMore, "Export options, batches, appearance and shortcuts");
            var moreGap = new Panel { Dock = DockStyle.Right, Width = Gap };

            toolsLeft.Dock = DockStyle.Left;
            btnSave.Dock = DockStyle.Right; btnSave.Margin = new Padding(0);
            var toolbar = new Panel { Dock = DockStyle.Fill, Height = 36, Margin = new Padding(0, 0, 0, Section) };
            toolbar.Controls.Add(toolsLeft);
            toolbar.Controls.Add(btnSave); // later controls dock first, so More is rightmost, then a gap, then Save
            btnMore.Dock = DockStyle.Right; btnMore.Margin = new Padding(0);
            toolbar.Controls.Add(moreGap);
            toolbar.Controls.Add(btnMore);
            pbPortrait.Dock = DockStyle.Fill;
            pbPortrait.BackColor = Color.Transparent; // the frame behind it supplies the colour, so a theme change needs no repaint rule
            pbPortrait.Margin = new Padding(0);
            pbPortrait.SizeMode = PictureBoxSizeMode.Zoom;
            portraitFrame = new FramePanel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            portraitFrame.Controls.Add(pbPortrait);
            portraitFrame.Paint += PaintFrame;

            historyStrip = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true, Margin = new Padding(0, Gap, 0, 0) };
            toolTip.SetToolTip(historyStrip, "Recent portraits. Click one to go back to it.");

            // The DNA text is still kept up to date (tbDNA is never shown); copy, paste and import are in the More menu.
            label22.AutoSize = false;
            label22.Dock = DockStyle.Fill;
            label22.TextAlign = ContentAlignment.MiddleLeft;
            label22.Tag = "muted";
            label22.ForeColor = SystemColors.GrayText;
            label22.Text = DefaultNote;
            statusTimer.Tick += (s, e) => { statusTimer.Stop(); label22.Text = DefaultNote; };
            var dna = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            dna.Controls.Add(label22);
            var stage = Table(1);
            stage.AutoSize = false; // fill the column; an auto-sized table grows to its widest child and then overflows the window
            stage.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            stage.RowCount = 0;
            stage.Margin = new Padding(0, 0, Section, 0);
            AddFull(stage, toolbar);
            AddFull(stage, portraitFrame, SizeType.Percent);
            int stripRow = stage.RowCount++;
            stage.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
            stage.Controls.Add(historyStrip, 0, stripRow);
            int dnaRow = stage.RowCount++;
            stage.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            stage.Controls.Add(dna, 0, dnaRow);

            // ---- right: portrait type and frame, then DNA / Properties / Content tabs ----
            rbClassic = SizedChip(new Chip { IsToggle = true, Checked = true }, "Classic", 84, 28);
            rbMedieval = SizedChip(new Chip { IsToggle = true }, "Medieval", 84, 28);
            rbMedieval.Margin = new Padding(0);
            rbClassic.CheckedChanged += (s, e) => { if (rbClassic.Checked && !applyingLook) ApplyLook(Look.Classic); };
            rbMedieval.CheckedChanged += (s, e) => { if (rbMedieval.Checked && !applyingLook) ApplyLook(Look.Medieval); };
            toolTip.SetToolTip(rbClassic, "Plain Windows look; follows the system light or dark setting");
            toolTip.SetToolTip(rbMedieval, "Parchment, leather and gilt");
            var portraitHeading = Heading("Portrait");
            portraitHeading.Anchor = AnchorStyles.Left;
            portraitHeading.Margin = new Padding(0);
            var headTable = new Panel { Dock = DockStyle.Fill, Height = 30, Margin = new Padding(0, 0, 0, Gap) };
            portraitHeading.Dock = DockStyle.Left; portraitHeading.AutoSize = true;
            rbClassic.Dock = DockStyle.Right; rbClassic.Margin = new Padding(0);
            rbMedieval.Dock = DockStyle.Right; rbMedieval.Margin = new Padding(0);
            var chipGap = new Panel { Dock = DockStyle.Right, Width = Gap };
            headTable.Controls.Add(portraitHeading);
            headTable.Controls.Add(rbClassic);
            headTable.Controls.Add(chipGap);
            headTable.Controls.Add(rbMedieval); // last added docks first: Medieval is rightmost, then the gap, then Classic

            var headBlock = LabelColumns();
            AddFull(headBlock, headTable);

            // Folds: a chip that opens or closes a block. Rare things stay out of the way until asked for.
            Panel Fold(string label, Control target, Action after)
            {
                var chip = new Chip { Text = label + " ▸", Size = new Size(label.Length * 8 + 40, 32), Dock = DockStyle.Left, Margin = new Padding(0) };
                var host = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(0, Gap, 0, 0) };
                host.Controls.Add(chip);
                target.Visible = false;
                chip.Click += (s, e) =>
                {
                    target.Visible = !target.Visible;
                    chip.Text = label + (target.Visible ? " ▾" : " ▸");
                    after?.Invoke();
                };
                return host;
            }

            // Race tab: tiles for race and its variants first; the raw type and clothing boxes are folded away at the bottom.
            var typeBlock = LabelColumns();
            AddRow(typeBlock, "Type", cbPortraitTypes);
            AddRow(typeBlock, "Clothing", cbCulturePortraitTypes);
            typeBlock.Dock = DockStyle.Top;

            racePage = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0), AutoScroll = true, Padding = new Padding(0, Gap, 0, Gap), Tag = "paper" };
            var raceHeading = Heading("Race");
            raceHeading.Dock = DockStyle.Top;
            flavorHeading.Dock = DockStyle.Top; flavorHeading.Margin = new Padding(0, Section, 0, Gap);
            // Dock.Top stacks in reverse order of adding: the last control added ends up on top.
            racePage.Controls.Add(typeBlock);
            racePage.Controls.Add(Fold("Type and clothing boxes", typeBlock, null));
            racePage.Controls.Add(flavorTiles);
            racePage.Controls.Add(flavorHeading);
            racePage.Controls.Add(raceTiles);
            racePage.Controls.Add(raceHeading);

            // Role tab: roles first; rank and government (which roles set) are a fold for fine-tuning. The dropdowns still hold the values (parked off-screen), so history, randomize and import work unchanged.
            framePage = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0), AutoScroll = true, Padding = new Padding(0, Gap, 0, Gap), Tag = "paper" };
            var rankHeading = Heading("Rank"); rankHeading.Dock = DockStyle.Top;
            var govHeading = Heading("Government"); govHeading.Dock = DockStyle.Top; govHeading.Margin = new Padding(0, Section, 0, Gap);
            fineHost = new Panel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0), Padding = new Padding(0, 0, 0, Gap) };
            fineHost.Controls.Add(govTiles);
            fineHost.Controls.Add(govHeading);
            fineHost.Controls.Add(rankTiles);
            fineHost.Controls.Add(rankHeading);
            var roleHeading = Heading("Role"); roleHeading.Dock = DockStyle.Top;
            framePage.Controls.Add(fineHost);
            framePage.Controls.Add(Fold("Fine-tune rank and government", fineHost, SchedulePickers));
            framePage.Controls.Add(chkRollLook);
            framePage.Controls.Add(roleTiles);
            framePage.Controls.Add(roleHeading);
            foreach (var parked in new Control[] { cbRank, cbGovernment })
            {
                parked.Location = new Point(-600, -600); parked.Size = new Size(120, 28); parked.TabStop = false;
                Controls.Add(parked);
            }

            foreach (var page in new[] { panelDLCs, panelMods })
            {
                page.Dock = DockStyle.Fill; page.AutoScroll = true; page.Padding = new Padding(0, Gap, 0, Gap);
            }

            // Look tab: the main look (hair, clothes...), then the face, then the rare features (folded). One scrolling page of tile sections.
            foreach (var group in new[] { panelDNA, panelProperties, panelExtras })
            {
                group.Dock = DockStyle.Top; group.AutoScroll = false; group.AutoSize = true; group.AutoSizeMode = AutoSizeMode.GrowAndShrink;
                group.WrapContents = true; group.Padding = new Padding(0, 0, 0, Section); group.Margin = new Padding(0);
            }
            var faceHeading = Heading("Face"); faceHeading.Dock = DockStyle.Top;
            lookPage = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0), AutoScroll = true, Padding = new Padding(0, Gap, 0, Gap), Tag = "paper" };
            lookPage.Controls.Add(panelExtras);
            lookPage.Controls.Add(Fold("Rare features (scars, boils, eyepatch...)", panelExtras, SchedulePickers));
            lookPage.Controls.Add(panelDNA);
            lookPage.Controls.Add(faceHeading);
            lookPage.Controls.Add(panelProperties);

            // DLC and mods: a small window opened from the More menu; set up once and rarely touched.
            SizedChip(btnToogleAll, "Toggle all", 104, 30);
            SizedChip(btnReload, "Reload", 88, 30);
            btnReload.Margin = new Padding(0);
            var contentButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false, Margin = new Padding(0), Padding = new Padding(0, Gap, 0, 0) };
            contentButtons.Controls.Add(btnToogleAll);
            contentButtons.Controls.Add(btnReload);

            var contentHost = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0), Tag = "paper" };
            contentHost.Controls.Add(panelDLCs);
            contentHost.Controls.Add(panelMods);
            var contentStrip = new TabStrip(("DLCs", panelDLCs), ("Mods", panelMods)) { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, Gap) };
            contentLayout = Table(1);
            contentLayout.RowCount = 0;
            contentLayout.Visible = false;
            AddFull(contentLayout, contentStrip);
            AddFull(contentLayout, contentHost, SizeType.Percent);
            AddFull(contentLayout, contentButtons);

            var pages = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0), Tag = "paper" };
            pages.Controls.Add(lookPage);
            pages.Controls.Add(racePage);
            pages.Controls.Add(framePage);
            var savedHost = BuildSaved();
            pages.Controls.Add(savedHost);
            mainStrip = new TabStrip(("Race", racePage), ("Look", lookPage), ("Role", framePage), ("Saved", savedHost)) { Dock = DockStyle.Fill, Margin = new Padding(0) };
            mainStrip.SelectedIndexChanged += (s, e) => SchedulePickers();
            mainStrip.SelectedIndex = 1; // open on Look
            var side = Table(1);
            side.AutoSize = false;
            side.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            side.RowCount = 0;
            side.Margin = new Padding(0);
            AddFull(side, mainStrip);
            AddFull(side, pages, SizeType.Percent);

            var root = Table(2);
            root.AutoSize = false;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, SidebarWidth));
            root.RowCount = 1;
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.Controls.Add(stage, 0, 0);
            root.Controls.Add(side, 1, 0);
            Controls.Add(root);

            banner = new FramePanel { Dock = DockStyle.Top, Height = 52, Visible = false };
            banner.Paint += PaintBanner;
            Controls.Add(banner); // added after root, so it docks first and root takes the rest

            // Rows added later (features per portrait type, DLC and mod lists) pick up the current look.
            foreach (var panel in new Control[] { panelDNA, panelProperties, panelDLCs, panelMods, racePage, framePage, lookPage, savedPage, panelExtras })
            {
                var p = panel;
                p.ControlAdded += (s, e) => StyleTree(e.Control, true);
                if (p is FlowLayoutPanel flowPanel) flowPanel.SizeChanged += (s, e) => FitRows(flowPanel);
            }

            LoadSettings();
            ApplySettings();
            ApplyLook(settings.TryGetValue("look", out var savedLook) && Enum.TryParse(savedLook, out Look saved) ? saved : Look.Classic);

            ResumeLayout(true);
        }
    }
}
