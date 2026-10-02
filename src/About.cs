using System.Diagnostics;
using System.Drawing;

namespace UOTinker;

public static class AppInfo
{
    public const string Version = "0.1";
    public const string Author = "Daniel Kowarek";
    public const string DonateUrl = "https://ko-fi.com/uoschattenwelt";

    public const string License1 = "This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.";
    public const string License2 = "This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.";
    public const string License3 = "You should have received a copy of the GNU General Public License along with this program. If not, see <https://www.gnu.org/licenses/>. Contains code from ClassicUO under the BSD 2-Clause License (see THIRD-PARTY-NOTICES.md).";

    public static readonly Icon? AppIcon = LoadIcon();

    private static Icon? LoadIcon()
    {
        try
        {
            string? path = Environment.ProcessPath;
            return path == null ? null : Icon.ExtractAssociatedIcon(path);
        }
        catch
        {
            return null;
        }
    }

    public static string Years
    {
        get
        {
            int y = DateTime.Now.Year;
            return y > 2026 ? $"2026 - {y}" : "2026";
        }
    }

    public static string BuildDate
    {
        get
        {
            try
            {
                return File.GetLastWriteTime(typeof(AppInfo).Assembly.Location).ToString("dd.MM.yyyy HH:mm");
            }
            catch
            {
                return "";
            }
        }
    }

    public static void OpenDonate()
    {
        try
        {
            Process.Start(new ProcessStartInfo(DonateUrl) { UseShellExecute = true });
        }
        catch
        {
        }
    }
}

public sealed class AboutForm : Form
{
    public AboutForm()
    {
        Text = "Über UOTinker";
        Width = 560;
        Height = 470;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        Font = Theme.Ui;
        HandleCreated += (_, _) => Theme.DarkTitleBar(this);

        var head = new Label
        {
            Left = 20, Top = 16, Width = 510, Height = 28, Text = $"UOTinker {AppInfo.Version}", Font = new Font("Georgia", 15f, FontStyle.Bold), ForeColor = Theme.Gold,
        };
        var info = new Label
        {
            Left = 20, Top = 52, Width = 510, Height = 84, ForeColor = Theme.Text,
            Text = $"© {AppInfo.Years} {AppInfo.Author}\r\nErstellt am {AppInfo.BuildDate}\r\n\r\nBetrachter und Editor für die Dateien eines Ultima-Online-Clients (Tiledata, Art, Land, Gumps und mehr). Für alle Shards gedacht.",
        };
        var license = new TextBox
        {
            Left = 20, Top = 146, Width = 510, Height = 220, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.FixedSingle,
            BackColor = Theme.Card, ForeColor = Theme.Muted, TabStop = false,
            Text = AppInfo.License1 + "\r\n\r\n" + AppInfo.License2 + "\r\n\r\n" + AppInfo.License3,
        };
        var donate = Theme.FlatButton("UOTinker unterstützen (Ko-fi)");
        donate.SetBounds(20, 384, 240, 34);
        donate.Click += (_, _) => AppInfo.OpenDonate();
        var ok = Theme.FlatButton("OK", true);
        ok.SetBounds(430, 384, 100, 34);
        ok.DialogResult = DialogResult.OK;
        AcceptButton = ok;
        CancelButton = ok;
        Controls.AddRange(new Control[] { head, info, license, donate, ok });
    }
}
