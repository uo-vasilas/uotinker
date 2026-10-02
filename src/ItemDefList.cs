using System.Drawing;
using System.Text;

namespace UOTinker;

public static class AppNav
{
    public static Action<string, int>? Request;
}

public static class DefTools
{
    public static string? Open(DefInfo d)
    {
        try
        {
            string path = d.FullPath;
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string? code = new[] { @"C:\Program Files\Microsoft VS Code\Code.exe", Path.Combine(local, @"Programs\Microsoft VS Code\Code.exe") }.FirstOrDefault(File.Exists);
            if (code != null)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(code, $"-g \"{path}:{d.Line}\"") { UseShellExecute = false });
                return null;
            }
            string? npp = new[] { @"C:\Program Files\Notepad++\notepad++.exe", @"C:\Program Files (x86)\Notepad++\notepad++.exe" }.FirstOrDefault(File.Exists);
            if (npp != null)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(npp, $"-n{d.Line} \"{path}\"") { UseShellExecute = false });
                return null;
            }
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            return null;
        }
        catch (Exception ex)
        {
            return "ITEMDEF konnte nicht geöffnet werden: " + ex.Message;
        }
    }
}

public sealed class ItemDefProvider : ProviderBase, IThumbProvider
{
    private readonly Context _c;
    private readonly List<DefInfo> _d;

    public ItemDefProvider(Context c)
    {
        _c = c;
        _d = c.Catalog.ItemDefs;
    }

    public Bitmap? Thumb(int i) => _d[i].ResolvedId >= 0 ? _c.Art.GetStatic(_d[i].ResolvedId, out _) : null;
    public string ThumbLabel(int i) => _d[i].ResolvedId >= 0 ? Gfx.Hex(_d[i].ResolvedId) : "?";

    public override string[] Columns => new[] { "Defname", "Kopf", "Item-ID", "Hex", "Name (Sphere)", "Tiledata-Name", "Kategorie", "Datei", "Zeile", "Problem" };
    public override int[] Widths => new[] { 190, 110, 65, 70, 180, 150, 180, 220, 55, 200 };
    public override int Count => _d.Count;

    private string Problem(DefInfo d)
    {
        if (d.ResolvedId < 0)
        {
            return "Item-ID nicht aufgelöst";
        }
        if (d.ResolvedId >= _c.Tile.ItemCount)
        {
            return "ID außerhalb der Tiledata";
        }
        var p = new List<string>();
        if (_c.Tile.ItemName[d.ResolvedId].Length == 0)
        {
            p.Add("Tiledata ohne Name");
        }
        if (!_c.Art.StaticValid(d.ResolvedId))
        {
            p.Add("Art fehlt");
        }
        return string.Join(", ", p);
    }

    public override string[] Row(int i)
    {
        var d = _d[i];
        bool ok = d.ResolvedId >= 0 && d.ResolvedId < _c.Tile.ItemCount;
        return new[]
        {
            d.DefName, d.Header, d.ResolvedId >= 0 ? d.ResolvedId.ToString() : "", d.ResolvedId >= 0 ? Gfx.Hex(d.ResolvedId) : "",
            d.Name, ok ? _c.Tile.ItemName[d.ResolvedId] : "", d.Category, d.File, d.Line.ToString(), Problem(d),
        };
    }

    public override FilterDef[] Filters => new[]
    {
        new FilterDef { Label = "Problem", Options = new[] { "Alle", "Irgendein Problem", "Ohne Problem", "Tiledata ohne Name", "Art fehlt", "ID nicht aufgelöst" }, Pass = (i, o) =>
        {
            string p = Problem(_d[i]);
            return o switch
            {
                0 => true,
                1 => p.Length > 0,
                2 => p.Length == 0,
                3 => p.Contains("ohne Name"),
                4 => p.Contains("Art fehlt"),
                _ => p.Contains("nicht aufgelöst"),
            };
        } },
    };

    public override string Summary => $"{_d.Count:N0} ITEMDEFs in {_c.Catalog.FileCount} Skriptdateien";

    public override PreviewData Preview(int i)
    {
        var d = _d[i];
        var p = new PreviewData();
        var sb = new StringBuilder();
        sb.AppendLine($"ITEMDEF {(d.DefName.Length > 0 ? d.DefName : d.Header)}");
        sb.AppendLine($"Kopf:      [ITEMDEF {d.Header}]");
        sb.AppendLine($"Datei:     {d.File}:{d.Line}");
        sb.AppendLine($"Name:      {d.Name}");
        sb.AppendLine($"Kategorie: {d.Category}");
        if (d.ResolvedId >= 0)
        {
            sb.AppendLine($"Item-ID:   {d.ResolvedId} ({Gfx.Hex(d.ResolvedId)})");
            p.Image = _c.Art.GetStatic(d.ResolvedId, out _);
            if (d.ResolvedId < _c.Tile.ItemCount)
            {
                sb.AppendLine($"Tiledata:  {(_c.Tile.ItemName[d.ResolvedId].Length > 0 ? _c.Tile.ItemName[d.ResolvedId] : "(Name leer)")}");
            }
            if (_c.Catalog.Items.TryGetValue(d.ResolvedId, out var same) && same.Count > 1)
            {
                sb.AppendLine($"Weitere ITEMDEFs mit dieser ID: {string.Join(", ", same.Where(x => x != d).Select(x => x.DefName.Length > 0 ? x.DefName : x.Header).Take(6))}");
            }
        }
        string prob = Problem(d);
        if (prob.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("PROBLEME:");
            sb.AppendLine("  ! " + prob);
        }
        p.Info = sb.ToString();
        p.Editor = new ItemDefPanel(_c, d, i);
        return p;
    }
}

public sealed class ItemDefPanel : UserControl, IEditorControl
{
#pragma warning disable CS0067
    public event Action? Edited;
    public event Action<int>? GoTo;
#pragma warning restore CS0067

    public ItemDefPanel(Context c, DefInfo d, int index)
    {
        Dock = DockStyle.Fill;
        BackColor = Theme.Bg;
        var status = new Label { Dock = DockStyle.Bottom, Height = 38, ForeColor = Theme.Muted };
        var row = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4) };
        var open = Theme.FlatButton("Skript öffnen", true);
        open.Width = 150;
        open.Click += (_, _) => status.Text = DefTools.Open(d) ?? $"{d.File}:{d.Line} geöffnet.";
        row.Controls.Add(open);
        var tile = Theme.FlatButton("Im Tiledata zeigen");
        tile.Width = 170;
        bool ok = d.ResolvedId >= 0 && d.ResolvedId < c.Tile.ItemCount;
        tile.Enabled = ok;
        tile.Click += (_, _) => AppNav.Request?.Invoke("tiledata", c.Tile.LandCount + d.ResolvedId);
        row.Controls.Add(tile);
        var art = Theme.FlatButton("Bei Items (Art) zeigen");
        art.Width = 190;
        art.Enabled = d.ResolvedId >= 0;
        art.Click += (_, _) => AppNav.Request?.Invoke("art", d.ResolvedId);
        row.Controls.Add(art);
        Controls.Add(status);
        Controls.Add(row);
    }
}
