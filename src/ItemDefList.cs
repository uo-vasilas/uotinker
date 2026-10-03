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
            return Loc.T("ITEMDEF konnte nicht geöffnet werden: ") + ex.Message;
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

    public override string[] Columns => new[] { Loc.T("Defname"), Loc.T("Kopf"), Loc.T("Item-ID"), Loc.T("Hex"), Loc.T("Name (Sphere)"), Loc.T("Tiledata-Name"), Loc.T("Kategorie"), Loc.T("Datei"), Loc.T("Zeile"), Loc.T("Problem") };
    public override int[] Widths => new[] { 190, 110, 65, 70, 180, 150, 180, 220, 55, 200 };
    public override int Count => _d.Count;

    private const int PrUnresolved = 0;
    private const int PrOutside = 1;
    private const int PrNoName = 2;
    private const int PrNoArt = 3;

    private List<int> ProblemCodes(DefInfo d)
    {
        var p = new List<int>();
        if (d.ResolvedId < 0)
        {
            p.Add(PrUnresolved);
            return p;
        }
        if (d.ResolvedId >= _c.Tile.ItemCount)
        {
            p.Add(PrOutside);
            return p;
        }
        if (_c.Tile.ItemName[d.ResolvedId].Length == 0)
        {
            p.Add(PrNoName);
        }
        if (!_c.Art.StaticValid(d.ResolvedId))
        {
            p.Add(PrNoArt);
        }
        return p;
    }

    private string Problem(DefInfo d) => string.Join(", ", ProblemCodes(d).Select(c => c switch
    {
        PrUnresolved => Loc.T("Item-ID nicht aufgelöst"),
        PrOutside => Loc.T("ID außerhalb der Tiledata"),
        PrNoName => Loc.T("Tiledata ohne Name"),
        _ => Loc.T("Art fehlt"),
    }));

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
        new FilterDef { Label = Loc.T("Problem"), Options = new[] { Loc.T("Alle"), Loc.T("Irgendein Problem"), Loc.T("Ohne Problem"), Loc.T("Tiledata ohne Name"), Loc.T("Art fehlt"), Loc.T("ID nicht aufgelöst") }, Pass = (i, o) =>
        {
            var p = ProblemCodes(_d[i]);
            return o switch
            {
                0 => true,
                1 => p.Count > 0,
                2 => p.Count == 0,
                3 => p.Contains(PrNoName),
                4 => p.Contains(PrNoArt),
                _ => p.Contains(PrUnresolved),
            };
        } },
    };

    public override string Summary => Loc.F("{0:N0} ITEMDEFs in {1} Skriptdateien", _d.Count, _c.Catalog.FileCount);

    public override PreviewData Preview(int i)
    {
        var d = _d[i];
        var p = new PreviewData();
        var sb = new StringBuilder();
        sb.AppendLine($"ITEMDEF {(d.DefName.Length > 0 ? d.DefName : d.Header)}");
        sb.AppendLine(Loc.F("Kopf:      [ITEMDEF {0}]", d.Header));
        sb.AppendLine(Loc.F("Datei:     {0}:{1}", d.File, d.Line));
        sb.AppendLine(Loc.F("Name:      {0}", d.Name));
        sb.AppendLine(Loc.F("Kategorie: {0}", d.Category));
        if (d.ResolvedId >= 0)
        {
            sb.AppendLine(Loc.F("Item-ID:   {0} ({1})", d.ResolvedId, Gfx.Hex(d.ResolvedId)));
            p.Image = _c.Art.GetStatic(d.ResolvedId, out _);
            if (d.ResolvedId < _c.Tile.ItemCount)
            {
                sb.AppendLine(Loc.F("Tiledata:  {0}", _c.Tile.ItemName[d.ResolvedId].Length > 0 ? _c.Tile.ItemName[d.ResolvedId] : Loc.T("(Name leer)")));
            }
            if (_c.Catalog.Items.TryGetValue(d.ResolvedId, out var same) && same.Count > 1)
            {
                sb.AppendLine(Loc.F("Weitere ITEMDEFs mit dieser ID: {0}", string.Join(", ", same.Where(x => x != d).Select(x => x.DefName.Length > 0 ? x.DefName : x.Header).Take(6))));
            }
        }
        string prob = Problem(d);
        if (prob.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine(Loc.T("PROBLEME:"));
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
        var open = Theme.FlatButton(Loc.T("Skript öffnen"), true);
        open.Width = 150;
        open.Click += (_, _) => status.Text = DefTools.Open(d) ?? Loc.F("{0}:{1} geöffnet.", d.File, d.Line);
        row.Controls.Add(open);
        var tile = Theme.FlatButton(Loc.T("Im Tiledata zeigen"));
        tile.Width = 170;
        bool ok = d.ResolvedId >= 0 && d.ResolvedId < c.Tile.ItemCount;
        tile.Enabled = ok;
        tile.Click += (_, _) => AppNav.Request?.Invoke("tiledata", c.Tile.LandCount + d.ResolvedId);
        row.Controls.Add(tile);
        var art = Theme.FlatButton(Loc.T("Bei Items (Art) zeigen"));
        art.Width = 190;
        art.Enabled = d.ResolvedId >= 0;
        art.Click += (_, _) => AppNav.Request?.Invoke("art", d.ResolvedId);
        row.Controls.Add(art);
        Controls.Add(status);
        Controls.Add(row);
    }
}
