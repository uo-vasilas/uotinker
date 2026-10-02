using System.Drawing;
using System.Runtime.InteropServices;

namespace UOTinker;

public static class Theme
{
    public static readonly Color Bg = ColorTranslator.FromHtml("#1B1720");
    public static readonly Color Sidebar = ColorTranslator.FromHtml("#151119");
    public static readonly Color Card = ColorTranslator.FromHtml("#1E1924");
    public static readonly Color Border = ColorTranslator.FromHtml("#3A3341");
    public static readonly Color Line = ColorTranslator.FromHtml("#2A2530");
    public static readonly Color Active = ColorTranslator.FromHtml("#2C2632");
    public static readonly Color Input = ColorTranslator.FromHtml("#2C2632");
    public static readonly Color Button = ColorTranslator.FromHtml("#241F2B");
    public static readonly Color Gold = ColorTranslator.FromHtml("#E0B563");
    public static readonly Color GoldDark = ColorTranslator.FromHtml("#3B3320");
    public static readonly Color Text = ColorTranslator.FromHtml("#E6E1EC");
    public static readonly Color Muted = ColorTranslator.FromHtml("#9A93A5");
    public static readonly Color Green = ColorTranslator.FromHtml("#6FCF8F");
    public static readonly Color Red = ColorTranslator.FromHtml("#E0707A");

    public static readonly Font Ui = new("Segoe UI", 9f);
    public static readonly Font UiBold = new("Segoe UI", 9f, FontStyle.Bold);
    public static readonly Font Small = new("Segoe UI", 7.5f, FontStyle.Bold);
    public static readonly Font Heading = new("Georgia", 15f, FontStyle.Bold);
    public static readonly Font Number = new("Georgia", 20f, FontStyle.Bold);
    public static readonly Font Mono = new("Consolas", 9f);
    public static readonly Font Icon = new("Segoe UI Symbol", 10f);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string? appName, string? idList);

    public static void DarkTitleBar(Form f)
    {
        int on = 1;
        if (AppInfo.AppIcon != null)
        {
            f.Icon = AppInfo.AppIcon;
        }
        DwmSetWindowAttribute(f.Handle, 20, ref on, sizeof(int));
        DwmSetWindowAttribute(f.Handle, 19, ref on, sizeof(int));
    }

    public static void DarkScroll(Control c)
    {
        if (c.IsHandleCreated)
        {
            SetWindowTheme(c.Handle, "DarkMode_Explorer", null);
        }
        else
        {
            c.HandleCreated += (_, _) => SetWindowTheme(c.Handle, "DarkMode_Explorer", null);
        }
    }

    public static void Style(Control root)
    {
        foreach (Control c in root.Controls)
        {
            StyleOne(c);
            Style(c);
        }
        StyleOne(root);
    }

    private static void StyleOne(Control c)
    {
        switch (c)
        {
            case TextBox tb:
                tb.BackColor = Input;
                tb.ForeColor = Text;
                tb.BorderStyle = BorderStyle.FixedSingle;
                DarkScroll(tb);
                break;
            case ComboBox cb:
                cb.FlatStyle = FlatStyle.Flat;
                cb.BackColor = Input;
                cb.ForeColor = Text;
                break;
            case ListView lv:
                lv.BackColor = Card;
                lv.ForeColor = Text;
                lv.BorderStyle = BorderStyle.None;
                if (lv.Name != "simple")
                {
                    DarkScroll(lv);
                }
                break;
            case VScrollBar vs:
                DarkScroll(vs);
                break;
            case Label l:
                if (l.BackColor != Color.Transparent)
                {
                    l.BackColor = Color.Transparent;
                }
                if (l.ForeColor == SystemColors.ControlText)
                {
                    l.ForeColor = Muted;
                }
                break;
            case CheckBox ck:
                ck.ForeColor = Text;
                ck.BackColor = Color.Transparent;
                break;
            case SplitContainer sc:
                sc.BackColor = Line;
                sc.Panel1.BackColor = Bg;
                sc.Panel2.BackColor = Bg;
                break;
            case ThumbGrid:
            case Canvas:
                break;
            case Panel or UserControl or FlowLayoutPanel:
                if (c.BackColor == SystemColors.Control)
                {
                    c.BackColor = Bg;
                }
                c.ForeColor = Text;
                break;
        }
        if (c.Font.Name != "Georgia" && c.Font == Control.DefaultFont)
        {
            c.Font = Ui;
        }
    }

    public static Button FlatButton(string text, bool primary = false)
    {
        var b = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Gold : Button,
            ForeColor = primary ? ColorTranslator.FromHtml("#1B1720") : Text,
            Font = Ui,
            Height = 36,
            Cursor = Cursors.Hand,
        };
        b.FlatAppearance.BorderColor = primary ? Gold : Line;
        b.FlatAppearance.MouseOverBackColor = primary ? ColorTranslator.FromHtml("#EBC47A") : Active;
        return b;
    }
}

public sealed class NavButton : Control
{
    private bool _hover;
    public string Glyph { get; set; } = "";
    public bool IsActive { get; set; }

    public NavButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Height = 35;
        Cursor = Cursors.Hand;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(IsActive ? Theme.Active : (_hover ? Color.FromArgb(34, 29, 40) : Theme.Sidebar));
        var col = IsActive ? Theme.Text : Theme.Muted;
        TextRenderer.DrawText(g, Glyph, Theme.Icon, new Rectangle(14, 0, 24, Height), IsActive ? Theme.Gold : Theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, Text, Theme.Ui, new Rectangle(40, 0, Width - 44, Height), col, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
    }
}

public sealed class CardPanel : Panel
{
    public CardPanel()
    {
        BackColor = Theme.Card;
        Padding = new Padding(16);
        DoubleBuffered = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Theme.Border);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }
}
