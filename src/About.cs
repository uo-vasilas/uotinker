using System.Diagnostics;
using System.Drawing;

namespace UOTinker;

public static class AppInfo
{
    public const string Version = "0.1.1";
    public const string Author = "Daniel Kowarek";
    public const string Repo = "uo-vasilas/uotinker";
    public const string DonateUrl = "https://ko-fi.com/uoschattenwelt";

    public static string License1 => Loc.T("Dieses Programm ist freie Software: Sie können es unter den Bedingungen der GNU General Public License, wie von der Free Software Foundation veröffentlicht, weitergeben und/oder ändern, entweder gemäß Version 3 der Lizenz oder (nach Ihrer Wahl) jeder späteren Version.");
    public static string License2 => Loc.T("Dieses Programm wird in der Hoffnung verbreitet, dass es nützlich ist, aber OHNE JEDE GEWÄHRLEISTUNG; sogar ohne die implizite Gewährleistung der MARKTFÄHIGKEIT oder EIGNUNG FÜR EINEN BESTIMMTEN ZWECK. Details finden Sie in der GNU General Public License.");
    public static string License3 => Loc.T("Sie sollten eine Kopie der GNU General Public License zusammen mit diesem Programm erhalten haben. Falls nicht, siehe <https://www.gnu.org/licenses/>. Enthält Code von ClassicUO unter der BSD-2-Clause-Lizenz (siehe THIRD-PARTY-NOTICES.md).");

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
        Text = Loc.T("Über UOTinker");
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
            Text = Loc.F("© {0} {1}\r\nErstellt am {2}\r\n\r\nBetrachter und Editor für die Dateien eines Ultima-Online-Clients (Tiledata, Art, Land, Gumps und mehr). Für alle Shards gedacht.", AppInfo.Years, AppInfo.Author, AppInfo.BuildDate),
        };
        var license = new TextBox
        {
            Left = 20, Top = 146, Width = 510, Height = 220, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.FixedSingle,
            BackColor = Theme.Card, ForeColor = Theme.Muted, TabStop = false,
            Text = AppInfo.License1 + "\r\n\r\n" + AppInfo.License2 + "\r\n\r\n" + AppInfo.License3,
        };
        var donate = Theme.FlatButton(Loc.T("UOTinker unterstützen (Ko-fi)"));
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
