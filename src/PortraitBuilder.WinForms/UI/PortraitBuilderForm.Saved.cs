using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;

namespace PortraitBuilder.UI
{
    /// <summary>
    /// Portraits you keep (the Saved tab, stored in saved.txt next to the exe) and the portrait you were on when you closed the app.
    /// </summary>
    public partial class PortraitBuilderForm
    {
        private sealed record SavedPortrait(string Name, string Type, string Culture, int Rank, int Government, string Dna, string Props);

        private readonly List<SavedPortrait> savedList = new();
        private Panel savedPage;
        private TilePicker savedTiles;
        private ContextMenuStrip savedMenu;
        private static string SavedPath => Path.Combine(AppContext.BaseDirectory, "saved.txt");

        /// <summary>The Saved tab: a short note and the grid of kept portraits.</summary>
        private Panel BuildSaved()
        {
            ReadSaved();
            savedTiles = new TilePicker { Dock = DockStyle.Top };
            var heading = Heading("Saved portraits"); heading.Dock = DockStyle.Top;
            var note = Muted("Keep adds the current portrait here (Ctrl+D). Click one to bring it back; right-click to remove it.");
            note.Dock = DockStyle.Top; note.MaximumSize = new Size(SidebarWidth - 20, 0); note.AutoSize = true;

            savedPage = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0), AutoScroll = true, Padding = new Padding(0, Gap, 0, Gap), Tag = "paper" };
            savedPage.Controls.Add(savedTiles);
            savedPage.Controls.Add(note);
            savedPage.Controls.Add(heading);

            savedMenu = new ContextMenuStrip();
            int target = -1;
            savedMenu.Items.Add("Bring it back", null, (s, e) => { if (target >= 0) LoadSaved(target); });
            savedMenu.Items.Add("Copy its DNA", null, (s, e) =>
            {
                if (target < 0 || target >= savedList.Count) return;
                Clipboard.SetText($"  dna=\"{savedList[target].Dna}\"\r\n  properties=\"{savedList[target].Props}\"");
                SetStatus("DNA copied. Ctrl+V pastes it back.");
            });
            savedMenu.Items.Add("Remove from Saved", null, (s, e) => { if (target >= 0) RemoveSaved(target); });
            savedTiles.TileRightClicked += (key, at) =>
            {
                target = int.Parse(key[6..]);
                savedMenu.Show(at);
            };
            return savedPage;
        }

        // ---------------------------------------------------------------- the list on disk

        private void ReadSaved()
        {
            try
            {
                foreach (var line in File.ReadAllLines(SavedPath))
                {
                    var p = line.Split('\t');
                    if (p.Length == 7 && int.TryParse(p[3], out int rank) && int.TryParse(p[4], out int gov) && rank is >= 0 and <= 5 && gov is >= 0 and <= 9)
                        savedList.Add(new SavedPortrait(p[0], p[1], p[2], rank, gov, p[5], p[6]));
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private void WriteSaved()
        {
            try
            {
                File.WriteAllLines(SavedPath, savedList.Select(s =>
                    string.Join('\t', s.Name.Replace('\t', ' '), s.Type, s.Culture, s.Rank, s.Government, s.Dna, s.Props)));
            }
            catch (IOException e) { SetStatus("Could not write saved.txt: " + e.Message); }
            catch (UnauthorizedAccessException e) { SetStatus("Could not write saved.txt: " + e.Message); }
        }

        // ---------------------------------------------------------------- keep, bring back, remove

        private void KeepCurrent()
        {
            if (cbPortraitTypes.SelectedItem is not string type || character?.DNA == null) return;
            var (race, flavor) = SplitName(type);
            string baseName = $"{race} {flavor}".Trim();
            int number = 1;
            while (savedList.Any(x => x.Name == $"{baseName} #{number}")) number++;
            var s = new SavedPortrait($"{baseName} #{number}", type, cbCulturePortraitTypes.SelectedItem as string ?? "",
                Math.Max(0, cbRank.SelectedIndex), Math.Max(0, cbGovernment.SelectedIndex), character.DNA, character.Properties);
            if (savedList.Any(x => (x with { Name = "" }) == (s with { Name = "" }))) { SetStatus("That portrait is already in Saved"); return; }
            savedList.Add(s);
            WriteSaved();
            if (mainStrip.SelectedIndex == 3) SchedulePickers();
            SetStatus($"Kept {s.Name}. It is in the Saved tab.");
        }

        private void LoadSaved(int index)
        {
            if (index < 0 || index >= savedList.Count) return;
            var s = savedList[index];
            if (!cbPortraitTypes.Items.Contains(s.Type))
            {
                SetStatus($"{s.Type} is not available now. Switch on the DLC or mod that has it.");
                return;
            }
            Recorded("saved portrait", () =>
            {
                try { ApplyState(new HistoryEntry(s.Dna, s.Props, s.Type, s.Culture, s.Rank, s.Government)); }
                catch (ArgumentException) { SetStatus("That saved portrait no longer fits the current data."); }
            });
        }

        private void RemoveSaved(int index)
        {
            if (index < 0 || index >= savedList.Count) return;
            var gone = savedList[index];
            savedList.RemoveAt(index);
            WriteSaved();
            SchedulePickers();
            SetStatus($"Removed {gone.Name} from Saved");
        }

        // ---------------------------------------------------------------- applying a stored state, and the last portrait

        /// <summary>Show a stored portrait: type first (it rebuilds the dropdowns the DNA letters index into), then rank, government and the letters.</summary>
        private void ApplyState(HistoryEntry h)
        {
            suppressDraw = true;
            try
            {
                SelectByText(cbPortraitTypes, h.Type);
                SelectByText(cbCulturePortraitTypes, h.Culture);
                cbRank.SelectedIndex = h.Rank;
                cbGovernment.SelectedIndex = h.Government;

                started = false;
                UpdatePortrait(h.Dna, h.Props, "");
                UpdateSelectedDisplayCharacteristicValues(character);
            }
            finally
            {
                started = true;
                suppressDraw = false;
            }
            DrawPortrait();
        }

        private string LastPortraitLine() =>
            character?.DNA == null ? null
            : "last=" + string.Join('|', cbPortraitTypes.SelectedItem, cbCulturePortraitTypes.SelectedItem ?? "", Math.Max(0, cbRank.SelectedIndex), Math.Max(0, cbGovernment.SelectedIndex), character.DNA, character.Properties);

        /// <summary>Open on the portrait you closed the app with, if it still fits the installed data.</summary>
        private void RestoreLast()
        {
            if (!settings.TryGetValue("last", out var value)) return;
            var p = value.Split('|');
            if (p.Length != 6 || !int.TryParse(p[2], out int rank) || !int.TryParse(p[3], out int gov) || rank is < 0 or > 5 || gov is < 0 or > 9) return;
            if (!cbPortraitTypes.Items.Contains(p[0])) return;
            try { ApplyState(new HistoryEntry(p[4], p[5], p[0], p[1], rank, gov)); }
            catch (ArgumentException e) { logger.LogDebug("Last portrait could not be restored: {message}", e.Message); }
        }
    }
}
