using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using PortraitBuilder.Engine;
using PortraitBuilder.Model;
using PortraitBuilder.Model.Portrait;
using SkiaSharp;

namespace PortraitBuilder.UI
{
    /// <summary>
    /// Rank, government, race and flavor as tiles. Each tile is the current portrait with that one option applied,
    /// so you see what would change before you click. Tiles are rendered on a background thread, one at a time.
    /// </summary>
    public partial class PortraitBuilderForm
    {
        private TabStrip mainStrip;
        private Panel racePage, framePage;
        private TilePicker raceTiles, flavorTiles, rankTiles, govTiles, roleTiles;
        private CheckBox chkRollLook;

        /// <summary>
        /// Roles: a rank and a government that read as a kind of person. Government changes the clothes by itself (Theocracy dresses a priest,
        /// Merchant Republic a merchant); rank sets the frame. The rest of the look is rolled fresh, so each click is a new person of that kind.
        /// Rank indices: 0 none, 1 baron, 2 count, 3 duke, 4 king, 5 emperor. Government indices follow the dropdown order.
        /// </summary>
        private sealed record Role(string Name, int Rank, int Gov);
        private static readonly Role[] Roles =
        {
            new("Commoner", 0, 0), new("Knight", 1, 0), new("Lord", 2, 0), new("Duke", 3, 0), new("King", 4, 0), new("Emperor", 5, 0),
            new("Priest", 0, 2), new("Bishop", 2, 2), new("Monk", 0, 7), new("Merchant", 0, 4), new("Patrician", 2, 3),
            new("Warlord", 2, 5), new("Khan", 4, 6), new("Emir", 2, 1), new("Mandarin", 2, 9), new("Chinese emperor", 5, 8),
        };

        private void ApplyRole(Role role) => Recorded("role: " + role.Name, () =>
        {
            suppressDraw = true;
            try
            {
                cbRank.SelectedIndex = role.Rank;
                cbGovernment.SelectedIndex = role.Gov;
                if (chkRollLook.Checked)
                {
                    started = false;
                    try { RandomizeDisplayCharacteristics(false); } finally { started = true; }
                }
            }
            finally { suppressDraw = false; }
            DrawPortrait();
        });
        private Label flavorHeading;
        private readonly System.Windows.Forms.Timer pickTimer = new() { Interval = 220 };
        private int pickVersion;

        private void BuildPickers()
        {
            raceTiles = new TilePicker { Dock = DockStyle.Top };
            flavorTiles = new TilePicker { Dock = DockStyle.Top };
            rankTiles = new TilePicker { Dock = DockStyle.Top, TileWidth = 68 };
            govTiles = new TilePicker { Dock = DockStyle.Top };
            roleTiles = new TilePicker { Dock = DockStyle.Top, TileWidth = 68 };
            chkRollLook = new CheckBox { Text = "Roll a new look with each role", Checked = true, AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(0, Gap, 0, 0) };
            toolTip.SetToolTip(chkRollLook, "On: each role is a new person (hair, clothes, face rolled for the ticked features). Off: only the frame and government change.");
            flavorHeading = Heading("Variants");
            pickTimer.Tick += (s, e) => { pickTimer.Stop(); RunPickers(); };
            var scrollWatch = new System.Windows.Forms.Timer { Interval = 300 };
            scrollWatch.Tick += (s, e) => WatchScroll();
            scrollWatch.Start();
        }

        /// <summary>Ask for the visible tiles to be redrawn shortly; a burst of changes (stepping a value) renders once.</summary>
        private void SchedulePickers()
        {
            pickTimer.Stop();
            pickTimer.Start();
        }

        // ---------------------------------------------------------------- choosing

        /// <summary>Keep the portrait before and after in the recent strip, so a tile pick can be undone like any other change.</summary>
        private void Recorded(string label, Action apply)
        {
            PushHistory("Before " + label);
            apply();
            PushHistory(label);
        }

        private void ChooseRank(int index) => Recorded("rank: " + cbRank.Items[index], () =>
        {
            cbRank.SelectedIndex = index;
            if (character.Rank != (TitleRank)index) { character.Rank = (TitleRank)index; DrawPortrait(); } // in case the hidden box raised no event
        });

        private void ChooseGovernment(int index) => Recorded("government: " + cbGovernment.Items[index], () =>
        {
            cbGovernment.SelectedIndex = index;
            if (character.Government != (GovernmentType)index) { character.Government = (GovernmentType)index; DrawPortrait(); }
        });

        // ---------------------------------------------------------------- one feature = one section of tiles

        /// <summary>
        /// A feature section: a header (name, current value, dice, include-in-randomize tick) and a grid of pictures, one per value.
        /// The drop-down is only where the value is stored; it is never shown.
        /// </summary>
        private sealed class FeatureRow
        {
            public Characteristic Ch;
            public ComboBox Cb;
            public TilePicker Strip;
            public Chip Dice;
            public HoverRow Row;
            public TableLayoutPanel Box; // holds Row above Strip
            public Label Value;
            public int Page; // 1 face, 2 look, 4 rare: which block of the Look tab it sits in
        }

        private readonly Dictionary<Characteristic, FeatureRow> featureRows = new();
        private readonly FlowLayoutPanel panelExtras = new();
        private Panel lookPage, fineHost;
        private TableLayoutPanel contentLayout;
        private readonly Dictionary<Panel, int> scrollSeen = new();

        /// <summary>A section shows when its feature has more than one value to choose from.</summary>
        private void RefreshRowVisibility(FeatureRow f)
        {
            bool show = f.Cb.Enabled && f.Cb.Items.Count > 1;
            f.Row.Visible = f.Strip.Visible = show;
            if (f.Box != null) f.Box.Visible = show;
            f.Value.Text = f.Cb.SelectedIndex >= 0 ? $"{f.Cb.SelectedIndex + 1} of {f.Cb.Items.Count}" : "";
            if (f.Dice != null) f.Dice.Enabled = show;
        }

        private static string LettersWith(Dictionary<Characteristic, ComboBox> cbs, Characteristic ch, int index)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var pair in cbs)
                sb.Append(pair.Key == ch ? Character.GetLetter(index) : pair.Value != null ? GetLetter(pair.Value) : '0');
            return sb.ToString();
        }

        /// <summary>The dna string with one feature set to value <paramref name="index"/>, everything else as it is now.</summary>
        private string VariantDna(Characteristic ch, int index) =>
            dnaComboBoxes.ContainsKey(ch) ? LettersWith(dnaComboBoxes, ch, index) : GetDisplayCharacteristicsString(dnaComboBoxes);

        private string VariantProps(Characteristic ch, int index) =>
            (propertiesComboBoxes.ContainsKey(ch) ? LettersWith(propertiesComboBoxes, ch, index) : GetDisplayCharacteristicsString(propertiesComboBoxes))
            + (customPropertiesComboBoxes.ContainsKey(ch) ? LettersWith(customPropertiesComboBoxes, ch, index) : GetDisplayCharacteristicsString(customPropertiesComboBoxes));

        private void ChooseFeature(FeatureRow row, int index) =>
            Recorded($"{row.Ch}: {index + 1}", () => row.Cb.SelectedIndex = index);

        /// <summary>The square of the 176 px portrait to zoom into for each face feature, so the part that changes fills the tile.</summary>
        private static SKRectI FaceCrop(Characteristic ch) => ch.ToString().Split(' ')[0] switch
        {
            "Neck" => SKRectI.Create(46, 92, 84, 84),
            "Chin" => SKRectI.Create(58, 84, 60, 60),
            "Mouth" => SKRectI.Create(58, 74, 60, 60),
            "Nose" => SKRectI.Create(58, 60, 60, 60),
            "Cheeks" => SKRectI.Create(46, 62, 84, 84),
            "Eyes" => SKRectI.Create(50, 48, 76, 76),
            _ => SKRectI.Create(40, 38, 96, 96),
        };

        /// <summary>Scroll position of a features page, so tiles scrolled into view get their pictures.</summary>
        private void WatchScroll()
        {
            foreach (var page in new[] { lookPage })
            {
                if (page == null || !page.Visible) continue;
                int y = page.VerticalScroll.Value;
                if (!scrollSeen.TryGetValue(page, out int seen) || seen != y) { scrollSeen[page] = y; SchedulePickers(); }
            }
        }
        /// <summary>The type a race tile stands for: the same flavor as the current portrait when the race has it, else its first.</summary>
        private string RepresentativeFor(string race, List<(string Raw, string Race, string Flavor)> all, string flavorNow)
        {
            var inRace = all.Where(n => n.Race == race).ToList();
            var same = inRace.FirstOrDefault(n => n.Flavor == flavorNow);
            return same.Raw ?? inRace.OrderBy(n => n.Flavor, StringComparer.OrdinalIgnoreCase).First().Raw;
        }

        private void ChooseRace(string race)
        {
            var all = TypeNames();
            var (_, flavorRaw) = SplitName(cbPortraitTypes.SelectedItem as string);
            Recorded("race: " + race, () => SelectRaw(RepresentativeFor(race, all, flavorRaw.Length == 0 ? "Default" : flavorRaw)));
        }

        // ---------------------------------------------------------------- rendering

        /// <summary>One tile to draw: the base type and clothing, rank, government, and the dna and properties to draw them with.</summary>
        private sealed record TileJob(TilePicker Picker, string Key, string Base, string Clothing, int Rank, int Government, string Dna, string Props, SKRectI? Crop = null)
        {
            public string Stamp => $"{Base}|{Clothing}|{Rank}|{Government}|{Dna}|{Props}|{Crop}";
        }

        private SKImage RenderVariant(TileJob job)
        {
            var type = string.IsNullOrEmpty(job.Clothing)
                ? loader.GetPortraitType("PORTRAIT_" + job.Base)
                : loader.GetPortraitType("PORTRAIT_" + job.Base, "PORTRAIT_" + job.Clothing);
            var c = new Character
            {
                PortraitType = type,
                FallbackPortraitTypes = character.FallbackPortraitTypes,
                IsChild = character.IsChild,
                ReligiousClothingOverride = character.ReligiousClothingOverride,
                Rank = (TitleRank)job.Rank,
                Government = (GovernmentType)job.Government
            };
            c.Import(job.Dna, job.Props);
            var steps = PortraitBuilder.Engine.PortraitBuilder.BuildCharacter(c, loader.ActivePortraitData.Sprites).Where(s => s != null).ToList();
            Parallel.ForEach(steps, s => s.Tile = loader.Cache.Get(s.Def)[s.TileIndex]);
            using var bmp = PortraitRenderer.DrawPortrait(steps);
            var full = SKImage.FromBitmap(bmp);
            if (job.Crop is not { } area) return full;
            var part = full.Subset(area);
            full.Dispose();
            return part;
        }

        /// <summary>The tabs that hold tiles: 0 Race, 1 Look (look, face and the folded rare features), 2 Role, 3 Saved.</summary>
        private void RunPickers()
        {
            if (mainStrip == null) return;
            if (cbPortraitTypes.SelectedItem is not string baseRaw || character?.PortraitType == null) return;

            string clothing = cbCulturePortraitTypes.SelectedItem as string ?? "";
            string dna = character.DNA, props = character.Properties;
            int rank = Math.Max(0, cbRank.SelectedIndex), government = Math.Max(0, cbGovernment.SelectedIndex);
            var jobs = new List<TileJob>();
            TileJob Job(TilePicker p, string key, string b, int r, int g) => new(p, key, b, clothing, r, g, dna, props);

            switch (mainStrip.SelectedIndex)
            {
                case 2:
                {
                    var ranks = cbRank.Items.Cast<object>().Select((o, i) => ($"rank:{i}", o.ToString())).ToList();
                    rankTiles.Set(ranks, $"rank:{rank}", key => ChooseRank(int.Parse(key[5..])));
                    for (int i = 0; i < ranks.Count && fineHost.Visible; i++) jobs.Add(Job(rankTiles, ranks[i].Item1, baseRaw, i, government));

                    var governments = cbGovernment.Items.Cast<object>().Select((o, i) => ($"gov:{i}", o.ToString())).ToList();
                    govTiles.Set(governments, $"gov:{government}", key => ChooseGovernment(int.Parse(key[4..])));
                    for (int i = 0; i < governments.Count && fineHost.Visible; i++) jobs.Add(Job(govTiles, governments[i].Item1, baseRaw, rank, i));

                    var roles = Roles.Select((r, i) => ($"role:{i}", r.Name)).ToList();
                    int current = Array.FindIndex(Roles, r => r.Rank == rank && r.Gov == government);
                    roleTiles.Set(roles, current >= 0 ? $"role:{current}" : "", key => ApplyRole(Roles[int.Parse(key[5..])]));
                    for (int i = 0; i < Roles.Length; i++) jobs.Add(Job(roleTiles, roles[i].Item1, baseRaw, Roles[i].Rank, Roles[i].Gov));
                    break;
                }
                case 0:
                {
                    var all = TypeNames();
                    var (race, flavorRaw) = SplitName(baseRaw);
                    string flavorNow = flavorRaw.Length == 0 ? "Default" : flavorRaw;

                    var races = all.Select(n => n.Race).Distinct().OrderBy(r => r, StringComparer.OrdinalIgnoreCase).ToList();
                    raceTiles.Set(races.Select(r => ($"race:{r}", r)).ToList(), $"race:{race}", key => ChooseRace(key[5..]));
                    foreach (var r in races) jobs.Add(Job(raceTiles, $"race:{r}", RepresentativeFor(r, all, flavorNow), rank, government));

                    var flavors = all.Where(n => n.Race == race).OrderBy(n => n.Flavor, StringComparer.OrdinalIgnoreCase).ToList();
                    flavorHeading.Text = flavors.Count > 1 ? $"{race}: {flavors.Count} variants" : race;
                    flavorTiles.Set(flavors.Select(n => ($"flavor:{n.Raw}", n.Flavor)).ToList(), $"flavor:{baseRaw}", key => Recorded("variant: " + flavors.First(n => "flavor:" + n.Raw == key).Flavor, () => SelectRaw(key[7..])));
                    foreach (var n in flavors) jobs.Add(Job(flavorTiles, $"flavor:{n.Raw}", n.Raw, rank, government));
                    break;
                }
                case 1:
                {
                    // Sections of the Look page. Every visible section keeps its tiles; only those near the screen get pictures.
                    var scroller = lookPage;
                    var view = scroller.RectangleToScreen(scroller.ClientRectangle);
                    view.Inflate(0, 360);
                    var rows = featureRows.Values.Where(r => r.Strip.Visible && r.Cb.Items.Count > 1 && (r.Page != 4 || panelExtras.Visible))
                        .OrderBy(r => r.Strip.PointToScreen(Point.Empty).Y).ToList();
                    foreach (var row in rows)
                    {
                        var values = Enumerable.Range(0, row.Cb.Items.Count).Select(i => ($"feat:{i}", row.Page == 4 && i == 0 ? "Off" : (i + 1).ToString())).ToList();
                        row.Strip.Set(values, $"feat:{row.Cb.SelectedIndex}", key => ChooseFeature(row, int.Parse(key[5..])));
                        if (!view.IntersectsWith(row.Strip.RectangleToScreen(row.Strip.ClientRectangle))) continue;
                        // Face shapes barely show on the whole portrait, so those tiles zoom in on the face.
                        SKRectI? crop = row.Ch.Type == CharacteristicType.DNA ? FaceCrop(row.Ch) : null;
                        for (int i = 0; i < values.Count; i++)
                            jobs.Add(new TileJob(row.Strip, values[i].Item1, baseRaw, clothing, rank, government, VariantDna(row.Ch, i), VariantProps(row.Ch, i), crop));
                    }
                    break;
                }
                case 3:
                {
                    var items = savedList.Select((s, i) => ($"saved:{i}", s.Name)).ToList();
                    savedTiles.Set(items, "", key => LoadSaved(int.Parse(key[6..])));
                    for (int i = 0; i < savedList.Count; i++)
                    {
                        var s = savedList[i];
                        jobs.Add(new TileJob(savedTiles, $"saved:{i}", s.Type, s.Culture, s.Rank, s.Government, s.Dna, s.Props));
                    }
                    break;
                }
                default: return;
            }

            jobs = jobs.Where(j => j.Picker.NeedsImage(j.Key, j.Stamp)).ToList(); // skip tiles whose picture is still right
            int version = ++pickVersion;
            Task.Run(() =>
            {
                foreach (var job in jobs)
                {
                    if (version != pickVersion) return; // newer request; stop quietly
                    Image picture;
                    try
                    {
                        using var image = RenderVariant(job);
                        picture = Image.FromStream(image.Encode().AsStream(true));
                    }
                    catch (Exception e)
                    {
                        logger.LogDebug("Tile {key} could not be rendered: {message}", job.Key, e.Message);
                        continue;
                    }
                    try
                    {
                        BeginInvoke(new Action(() =>
                        {
                            if (version == pickVersion) job.Picker.SetImage(job.Key, picture, job.Stamp); else picture.Dispose();
                        }));
                    }
                    catch (InvalidOperationException) { picture.Dispose(); return; } // the window is closing
                }
            });
        }
    }
}
