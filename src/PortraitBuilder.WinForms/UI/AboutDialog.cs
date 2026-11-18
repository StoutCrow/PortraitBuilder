using System;
using System.Drawing;
using System.Windows.Forms;

namespace PortraitBuilder.UI
{
    /// <summary>What this is, who made it, and how it was made. Plain about the AI part, because it matters to anyone deciding whether to trust the code.</summary>
    internal sealed class AboutDialog : Form
    {
        /// <summary>Set to the project page once there is one; the link is hidden while this is empty.</summary>
        private const string RepoUrl = "";

        public AboutDialog(Font font, Color back, bool medieval)
        {
            Text = "About Portrait Builder";
            Font = font;
            BackColor = back;
            ForeColor = medieval ? Color.FromArgb(0xe8, 0xd9, 0xb3) : SystemColors.ControlText;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.None;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            float k = DeviceDpi / 96f;
            Padding = new Padding((int)(24 * k));
            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };

            Color accent = medieval ? Color.FromArgb(0xc4, 0x99, 0x3a) : ForeColor;
            Color muted = medieval ? Color.FromArgb(0xb9, 0xa7, 0x7f) : SystemColors.GrayText;
            int textWidth = (int)(500 * k);

            Label Para(string text, Font f = null, Color? color = null, int after = 10) => new()
            {
                Text = text, AutoSize = true, MaximumSize = new Size(textWidth, 0), Font = f ?? Font,
                ForeColor = color ?? ForeColor, Margin = new Padding(0, 0, 0, (int)(after * k))
            };

            var stack = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0) };

            var head = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Margin = new Padding(0, 0, 0, (int)(14 * k)) };
            var logo = new PictureBox { Size = new Size((int)(88 * k), (int)(88 * k)), SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(0, 0, (int)(16 * k), 0), BackColor = Color.Transparent };
            using (var s = typeof(AboutDialog).Assembly.GetManifestResourceStream("logo-256.png"))
                if (s != null) logo.Image = Image.FromStream(s);
            var titles = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Margin = new Padding(0) };
            string version = Application.ProductVersion; int plus = version.IndexOf('+'); if (plus > 0) version = version[..plus];
            titles.Controls.Add(Para("Portrait Builder", new Font(Font.FontFamily, 20F, FontStyle.Bold), accent, 2));
            titles.Controls.Add(Para("A modernized fork of the CK2 Portrait Builder", null, muted, 2));
            titles.Controls.Add(Para("Version " + version, null, muted, 0));
            head.Controls.Add(logo, 0, 0);
            head.Controls.Add(titles, 1, 0);
            stack.Controls.Add(head);

            var heading = new Font(Font, FontStyle.Bold);
            stack.Controls.Add(Para("Coded entirely by AI", heading, accent, 2));
            stack.Controls.Add(Para("Every change that makes this version different from the code it started from was written by an AI model, Claude, made by Anthropic: the new interface, the picture tiles, the saving and export features, the upgrade to current .NET, and this logo. " +
                "The project owner gave the instructions and tried the results; they did not write the code. It was tested by running the program, and has not had a line-by-line review by a person. Treat it as AI-generated software: useful, but not audited."));
            stack.Controls.Add(Para("What a person wrote", heading, accent, 2));
            stack.Controls.Add(Para("The older code underneath, which the AI changed but did not create, was written by people. It started from github.com/scorpdx/portraitbuilder (J. Zebedee), which builds on rquinio's 2.x and 3.x and Measter's original 1.x. " +
                "The full git history is kept, so each person is credited on their own commits. See CREDITS.md."));
            stack.Controls.Add(Para("Licence and art", heading, accent, 2));
            stack.Controls.Add(Para("Free software under the GNU GPL v3 (COPYING), with the LGPL v3 (COPYING.LESSER) as shipped upstream. " +
                "Not affiliated with Paradox Interactive. Portraits are drawn from your own Crusader Kings II install; that art belongs to Paradox and is not part of this project.", null, null, 14));

            if (RepoUrl.Length > 0)
            {
                var link = new LinkLabel { Text = RepoUrl, AutoSize = true, Margin = new Padding(0, 0, 0, (int)(14 * k)) };
                link.LinkClicked += (s, e) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(RepoUrl) { UseShellExecute = true });
                stack.Controls.Add(link);
            }

            var close = new Chip { Text = "Close", Size = new Size((int)(96 * k), (int)(34 * k)), Margin = new Padding(0) };
            close.Click += (s, e) => Close();
            stack.Controls.Add(close);
            CancelButton = close;
            Controls.Add(stack);
        }
    }
}
