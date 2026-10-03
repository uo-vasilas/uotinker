using System.Drawing;
using System.Text;

namespace UOTinker;

public sealed class AnimRef
{
    public AnimStore Store = null!;
    public List<AnimSource> Sources = new();
    public bool Equipment;
}

public sealed class PreviewData
{
    public Bitmap? Image;
    public string Info = "";
    public Color[]? Palette;
    public Color? Swatch;
    public AnimRef? Anim;
    public List<(string title, Bitmap bmp)> Extras = new();
    public Control? Editor;
}

public sealed class FilterDef
{
    public string Label = "";
    public string[] Options = Array.Empty<string>();
    public Func<int, int, bool> Pass = (_, _) => true;
}

public interface IProvider
{
    string[] Columns { get; }
    int[] Widths { get; }
    int Count { get; }
    string[] Row(int i);
    bool IsFree(int i);
    string SearchText(int i);
    PreviewData Preview(int i);
    FilterDef[] Filters { get; }
    string Summary { get; }
    int KeyOf(int i);
}

public sealed class Context
{
    public Settings Settings = null!;
    public ArtStore Art = null!;
    public TileData Tile = null!;
    public AnimStore Anim = null!;
    public HueData Hues = null!;
    public SkillData Skills = null!;
    public RadarData Radar = null!;
    private readonly Dictionary<string, ClilocData> _clilocs = new(StringComparer.OrdinalIgnoreCase);
    public SphereCatalog Catalog = null!;
    public string Folder = "";

    public static Context Load(Settings s)
    {
        var c = new Context { Settings = s, Folder = s.DataFolder };
        c.Art = new ArtStore(s.DataFolder);
        c.Tile = new TileData(s.DataFolder);
        c.Anim = new AnimStore(s.DataFolder);
        c.Hues = new HueData(s.DataFolder);
        c.Skills = new SkillData(s.DataFolder);
        c.Radar = new RadarData(s.DataFolder);
        c.Catalog = new SphereCatalog(s.SphereScripts);
        return c;
    }

    public ClilocData Cliloc(string path)
    {
        lock (_clilocs)
        {
            if (!_clilocs.TryGetValue(path, out var d))
            {
                _clilocs[path] = d = new ClilocData(path);
            }
            return d;
        }
    }

    public int PendingOther
    {
        get
        {
            lock (_clilocs)
            {
                return Radar.Dirty.Count + Hues.Dirty.Count + Skills.Dirty.Count + _clilocs.Values.Sum(x => x.Dirty.Count);
            }
        }
    }
}

public abstract class ProviderBase : IProvider
{
    public abstract string[] Columns { get; }
    public abstract int[] Widths { get; }
    public abstract int Count { get; }
    public abstract string[] Row(int i);
    public abstract PreviewData Preview(int i);
    public virtual bool IsFree(int i) => false;
    public virtual string SearchText(int i) => string.Join(' ', Row(i)).ToLowerInvariant();
    public virtual FilterDef[] Filters => Array.Empty<FilterDef>();
    public virtual string Summary => "";
    public virtual int KeyOf(int i) => i;

    protected static FilterDef StatusFilter(Func<int, bool> free) => new()
    {
        Label = Loc.T("Status"),
        Options = new[] { Loc.T("Alle"), Loc.T("Belegt"), Loc.T("Frei") },
        Pass = (i, o) => o == 0 || (o == 1 ? !free(i) : free(i)),
    };

    protected static PreviewData Msg(string text) => new() { Info = text };
}

public sealed class ArtProvider : ProviderBase, IThumbProvider
{
    private readonly Context _c;
    private readonly (int w, int h)[] _size;
    private readonly int _count;

    public Bitmap? Thumb(int i) => _c.Art.GetStatic(i, out _);
    public string ThumbLabel(int i) => Gfx.Hex(i);

    public ArtProvider(Context c)
    {
        _c = c;
        _count = Math.Max(c.Art.StaticCount, c.Tile.ItemCount);
        _size = new (int, int)[_count];
        for (int i = 0; i < _count; i++)
        {
            _size[i] = c.Art.StaticValid(i) ? c.Art.StaticSize(i) : (0, 0);
        }
    }

    public override string[] Columns => new[] { "ID", "Hex", Loc.T("Status"), Loc.T("Groesse"), Loc.T("Tiledata-Name"), "Sphere-ITEMDEF", "AnimID" };
    public override int[] Widths => new[] { 60, 70, 60, 70, 170, 220, 60 };
    public override int Count => _count;
    public override bool IsFree(int i) => !_c.Art.StaticValid(i);

    private string TName(int i) => i < _c.Tile.ItemCount ? _c.Tile.ItemName[i] : "";

    public Context Ctx => _c;

    private (int w, int h) SizeOf(int i)
    {
        if ((_size[i] == (0, 0) || _c.Art.Pending.ContainsKey(i)) && !IsFree(i))
        {
            _size[i] = _c.Art.StaticSize(i);
        }
        return _size[i];
    }

    public void Refresh(int i)
    {
        if (i >= 0 && i < _count)
        {
            _size[i] = _c.Art.StaticValid(i) ? _c.Art.StaticSize(i) : (0, 0);
        }
    }

    public override string[] Row(int i) => new[]
    {
        i.ToString(), Gfx.Hex(i), (IsFree(i) ? (i >= _c.Art.StaticCount ? Loc.T("ausserhalb") : Loc.T("frei")) : Loc.T("belegt")) + (_c.Art.Pending.ContainsKey(i) ? "*" : ""),
        IsFree(i) ? "" : $"{SizeOf(i).w}x{SizeOf(i).h}", TName(i), _c.Catalog.ItemNames(i),
        i < _c.Tile.ItemCount && _c.Tile.AnimId[i] != 0 ? _c.Tile.AnimId[i].ToString() : "",
    };

    public override string SearchText(int i) => $"{i} 0x{i:x} {TName(i)} {_c.Catalog.ItemNames(i, 50)} {(IsFree(i) ? Loc.T("frei") : Loc.T("belegt"))}".ToLowerInvariant();

    public override FilterDef[] Filters => new[]
    {
        StatusFilter(IsFree),
        new FilterDef { Label = Loc.T("Tiledata-Name"), Options = new[] { Loc.T("Alle"), Loc.T("Mit Name"), Loc.T("Ohne Name") }, Pass = (i, o) => o == 0 || ((TName(i).Length > 0) == (o == 1)) },
        new FilterDef { Label = "Sphere", Options = new[] { Loc.T("Alle"), Loc.T("Mit ITEMDEF"), Loc.T("Ohne ITEMDEF") }, Pass = (i, o) => o == 0 || (_c.Catalog.Items.ContainsKey(i) == (o == 1)) },
        new FilterDef { Label = "AnimID", Options = new[] { Loc.T("Alle"), Loc.T("Mit AnimID"), Loc.T("Ohne") }, Pass = (i, o) => o == 0 || ((i < _c.Tile.ItemCount && _c.Tile.AnimId[i] != 0) == (o == 1)) },
    };

    public override string Summary
    {
        get
        {
            int used = 0;
            for (int i = 0; i < _count; i++)
            {
                if (!IsFree(i))
                {
                    used++;
                }
            }
            return Loc.F("{0} Slots, {1} belegt, {2} frei", _count, used, _count - used);
        }
    }

    public override PreviewData Preview(int i)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Loc.F("Item-Art {0} ({1})  -  artidx-Index {2}", i, Gfx.Hex(i), i + ArtStore.LandCount));
        var p = new PreviewData();
        p.Image = _c.Art.GetStatic(i, out string err);
        p.Editor = new GraphicEditor(_c, GKind.Static, i, Refresh);
        if (p.Image == null)
        {
            sb.AppendLine(err);
        }
        if (i < _c.Tile.ItemCount)
        {
            var t = _c.Tile;
            sb.AppendLine(Loc.F("Tiledata-Name: {0}", t.ItemName[i]));
            sb.AppendLine($"Flags: 0x{t.ItemFlags[i]:X}  {TileData.FlagText(t.ItemFlags[i])}");
            sb.AppendLine(Loc.F("Gewicht {0}, Hoehe {1}, Layer {2}, Menge {3}, AnimID {4}, Hue {5}, Licht {6}", t.Weight[i], t.Height[i], t.Layer[i], t.Count[i], t.AnimId[i], t.Hue[i], t.Light[i]));
        }
        if (_c.Catalog.Items.TryGetValue(i, out var defs))
        {
            sb.AppendLine("Sphere:");
            foreach (var d in defs)
            {
                sb.AppendLine($"  {d.DefName} \"{d.Name}\" [{d.Category}]  {d.File}");
            }
        }
        p.Info = sb.ToString();
        return p;
    }
}

public sealed class LandProvider : ProviderBase, IThumbProvider
{
    private readonly Context _c;

    public Bitmap? Thumb(int i) => _c.Art.GetLand(i, out _);
    public string ThumbLabel(int i) => Gfx.Hex(i);

    public LandProvider(Context c) => _c = c;

    public override string[] Columns => new[] { "ID", "Hex", Loc.T("Status"), Loc.T("Tiledata-Name"), "TexID", "Flags" };
    public override int[] Widths => new[] { 60, 70, 60, 170, 60, 300 };
    public override int Count => ArtStore.LandCount;
    public override bool IsFree(int i) => !_c.Art.LandValid(i);
    private string N(int i) => i < _c.Tile.LandCount ? _c.Tile.LandName[i] : "";

    public override string[] Row(int i) => new[]
    {
        i.ToString(), Gfx.Hex(i), (IsFree(i) ? Loc.T("frei") : Loc.T("belegt")) + (_c.Art.PendingLand.ContainsKey(i) ? "*" : ""), N(i),
        i < _c.Tile.LandCount ? _c.Tile.LandTex[i].ToString() : "",
        i < _c.Tile.LandCount ? TileData.FlagText(_c.Tile.LandFlags[i]) : "",
    };

    public override FilterDef[] Filters => new[]
    {
        StatusFilter(IsFree),
        new FilterDef { Label = "Name", Options = new[] { Loc.T("Alle"), Loc.T("Mit Name"), Loc.T("Ohne Name") }, Pass = (i, o) => o == 0 || ((N(i).Length > 0) == (o == 1)) },
        new FilterDef { Label = Loc.T("Textur"), Options = new[] { Loc.T("Alle"), Loc.T("Mit TexID"), Loc.T("Ohne") }, Pass = (i, o) => o == 0 || ((i < _c.Tile.LandCount && _c.Tile.LandTex[i] != 0) == (o == 1)) },
    };

    public override string Summary
    {
        get
        {
            int used = Enumerable.Range(0, Count).Count(i => !IsFree(i));
            return Loc.F("{0} Slots, {1} belegt, {2} frei", Count, used, Count - used);
        }
    }

    public override PreviewData Preview(int i)
    {
        var p = new PreviewData { Image = _c.Art.GetLand(i, out string err) };
        p.Editor = new GraphicEditor(_c, GKind.Land, i, _ => { });
        var sb = new StringBuilder(Loc.F("Land-Tile {0} ({1})\n", i, Gfx.Hex(i)));
        if (p.Image == null)
        {
            sb.AppendLine(err);
        }
        if (i < _c.Tile.LandCount)
        {
            sb.AppendLine($"Name: {_c.Tile.LandName[i]}\nFlags: 0x{_c.Tile.LandFlags[i]:X}  {TileData.FlagText(_c.Tile.LandFlags[i])}\nTexID: {_c.Tile.LandTex[i]}");
        }
        p.Info = sb.ToString();
        return p;
    }
}

public sealed class GumpProvider : ProviderBase, IThumbProvider
{
    private readonly Context _c;

    public Bitmap? Thumb(int i) => _c.Art.GetGump(i, out _);
    public string ThumbLabel(int i) => Gfx.Hex(i);

    public GumpProvider(Context c) => _c = c;

    public override string[] Columns => new[] { "ID", "Hex", Loc.T("Status"), Loc.T("Groesse"), "Bytes", Loc.T("Bedeutung") };
    public override int[] Widths => new[] { 60, 70, 60, 80, 80, 260 };
    public override int Count => _c.Art.GumpIdx.Count;
    public override bool IsFree(int i) => !_c.Art.GumpValid(i);

    private static string Meaning(int i) => i switch
    {
        >= 60000 => Loc.F("Paperdoll weiblich, AnimID {0}", i - 60000),
        >= 50000 => Loc.F("Paperdoll maennlich, AnimID {0}", i - 50000),
        _ => "",
    };

    public override string[] Row(int i)
    {
        var (w, h) = _c.Art.GumpSize(i);
        return new[]
        {
            i.ToString(), Gfx.Hex(i), (IsFree(i) ? Loc.T("frei") : Loc.T("belegt")) + (_c.Art.PendingGump.ContainsKey(i) ? "*" : ""), IsFree(i) ? "" : $"{w}x{h}",
            IsFree(i) ? "" : _c.Art.GumpLength(i).ToString(), Meaning(i),
        };
    }

    public override FilterDef[] Filters => new[]
    {
        StatusFilter(IsFree),
        new FilterDef
        {
            Label = Loc.T("Bereich"),
            Options = new[] { Loc.T("Alle"), "< 50000", Loc.T("50000-59999 (Paperdoll m)"), Loc.T(">= 60000 (Paperdoll w)") },
            Pass = (i, o) => o switch { 0 => true, 1 => i < 50000, 2 => i >= 50000 && i < 60000, _ => i >= 60000 },
        },
    };

    public override string Summary
    {
        get
        {
            int used = Enumerable.Range(0, Count).Count(i => !IsFree(i));
            return Loc.F("{0} Slots, {1} belegt, {2} frei", Count, used, Count - used);
        }
    }

    public override PreviewData Preview(int i)
    {
        var p = new PreviewData { Image = _c.Art.GetGump(i, out string err) };
        p.Editor = new GraphicEditor(_c, GKind.Gump, i, _ => { });
        string m = Meaning(i);
        p.Info = $"Gump {i} ({Gfx.Hex(i)})\n" + (p.Image == null ? err + "\n" : "") + (m.Length > 0 ? m + "\n" : "");
        return p;
    }
}

public sealed class TiledataProvider : ProviderBase, IThumbProvider
{
    private readonly Context _c;

    private const int PArtNoTile = 1, PTileNoArt = 2, PWearNoAnim = 4, PAnimNoMul = 8, PNoPaperdoll = 16, PWearNoLayer = 32, PSphereNoName = 64;

    private static readonly (int bit, string name)[] ProblemNames =
    {
        (PArtNoTile, Loc.T("Art ohne Tiledata")), (PTileNoArt, Loc.T("Tiledata ohne Art")), (PWearNoAnim, Loc.T("Wearable ohne AnimID")),
        (PAnimNoMul, Loc.T("AnimID ohne Animation")), (PNoPaperdoll, Loc.T("Paperdoll-Gump fehlt")), (PWearNoLayer, Loc.T("Wearable ohne Layer")),
        (PSphereNoName, Loc.T("ITEMDEF ohne Tiledata-Name")),
    };

    private readonly int[] _problems;

    private readonly Dictionary<int, bool> _animOk = new();

    public TiledataProvider(Context c)
    {
        _c = c;
        _problems = new int[c.Tile.LandCount + c.Tile.ItemCount];
        for (int i = 0; i < _problems.Length; i++)
        {
            _problems[i] = Compute(i);
        }
    }

    public Context Ctx => _c;

    public void Recompute(int i) => _problems[i] = Compute(i);

    public static IReadOnlyList<string> ProblemLabels => ProblemNames.Select(x => x.name).ToArray();

    public int[] ProblemCounts(bool itemsOnly)
    {
        var r = new int[ProblemNames.Length];
        for (int i = itemsOnly ? _c.Tile.LandCount : 0; i < _problems.Length; i++)
        {
            for (int k = 0; k < r.Length; k++)
            {
                if ((_problems[i] & ProblemNames[k].bit) != 0)
                {
                    r[k]++;
                }
            }
        }
        return r;
    }

    public int EntriesWithProblem(bool itemsOnly)
    {
        int n = 0;
        for (int i = itemsOnly ? _c.Tile.LandCount : 0; i < _problems.Length; i++)
        {
            if (_problems[i] != 0)
            {
                n++;
            }
        }
        return n;
    }

    private int Compute(int i)
    {
        var c = _c;
        var t = c.Tile;
        var animOk = _animOk;
        {
            int p = 0;
            if (IsLand(i))
            {
                bool art = c.Art.LandValid(i);
                bool filled = t.LandName[i].Length > 0 || t.LandFlags[i] != 0;
                if (art && !filled)
                {
                    p |= PArtNoTile;
                }
                if (!art && filled)
                {
                    p |= PTileNoArt;
                }
            }
            else
            {
                int id = Id(i);
                bool art = c.Art.StaticValid(id);
                bool filled = t.ItemName[id].Length > 0 || t.ItemFlags[id] != 0;
                bool wear = (t.ItemFlags[id] & 0x400000UL) != 0;
                int anim = t.AnimId[id];
                if (art && !filled)
                {
                    p |= PArtNoTile;
                }
                if (!art && filled)
                {
                    p |= PTileNoArt;
                }
                if (wear && anim == 0)
                {
                    p |= PWearNoAnim;
                }
                if (wear && t.Layer[id] == 0)
                {
                    p |= PWearNoLayer;
                }
                if (anim != 0)
                {
                    if (!animOk.TryGetValue(anim, out bool ok))
                    {
                        animOk[anim] = ok = c.Anim.SourcesFor(anim).Count > 0;
                    }
                    if (!ok)
                    {
                        p |= PAnimNoMul;
                    }
                    if (wear && !c.Art.GumpValid(50000 + anim) && !c.Art.GumpValid(60000 + anim))
                    {
                        p |= PNoPaperdoll;
                    }
                }
                if (c.Catalog.Items.ContainsKey(id) && t.ItemName[id].Length == 0)
                {
                    p |= PSphereNoName;
                }
            }
            return p & ~_c.Settings.DisabledProblems;
        }
    }

    public static IReadOnlyList<(int bit, string name)> Rules => ProblemNames;

    public void ApplyRules()
    {
        _c.Settings.Save();
        RecomputeAll();
    }

    public void RecomputeAll()
    {
        for (int i = 0; i < _problems.Length; i++)
        {
            _problems[i] = Compute(i);
        }
    }

    private string Mark(int i) => _c.Tile.Dirty.Contains(i) ? "*" : "";

    public int NextFreeItem(int fromId, bool needArt)
    {
        var t = _c.Tile;
        for (int id = Math.Max(0, fromId); id < t.ItemCount; id++)
        {
            if (t.ItemName[id].Length == 0 && t.ItemFlags[id] == 0 && _c.Art.StaticValid(id) == needArt)
            {
                return id;
            }
        }
        return Math.Min(Math.Max(0, fromId), t.ItemCount - 1);
    }

    public bool KeyIsLand(int i) => IsLand(i);
    public int KeyId(int i) => Id(i);

    private string ProblemText(int i) => string.Join(", ", ProblemNames.Where(x => (_problems[i] & x.bit) != 0).Select(x => x.name));

    public Bitmap? Thumb(int i) => IsLand(i) ? _c.Art.GetLand(i, out _) : _c.Art.GetStatic(Id(i), out _);
    public string ThumbLabel(int i) => (IsLand(i) ? "L " : "I ") + Gfx.Hex(Id(i));

    public override string[] Columns => new[] { Loc.T("Typ"), "ID", "Hex", "Name", "Art", Loc.T("Problem"), "Flags", Loc.T("Gew."), Loc.T("Hoehe"), "Layer", "AnimID", "Hue", Loc.T("Menge"), Loc.T("Licht") };
    public override int[] Widths => new[] { 45, 60, 70, 150, 40, 200, 100, 45, 50, 45, 60, 50, 60, 50 };
    public override int Count => _c.Tile.LandCount + _c.Tile.ItemCount;
    private bool IsLand(int i) => i < _c.Tile.LandCount;
    private int Id(int i) => IsLand(i) ? i : i - _c.Tile.LandCount;
    public override bool IsFree(int i) => IsLand(i) ? _c.Tile.LandName[i].Length == 0 && _c.Tile.LandFlags[i] == 0 : _c.Tile.ItemName[Id(i)].Length == 0 && _c.Tile.ItemFlags[Id(i)] == 0;

    public override string[] Row(int i)
    {
        var t = _c.Tile;
        int id = Id(i);
        if (IsLand(i))
        {
            return new[] { "Land" + Mark(i), id.ToString(), Gfx.Hex(id), t.LandName[id], _c.Art.LandValid(id) ? Loc.T("ja") : "-", ProblemText(i), "0x" + t.LandFlags[id].ToString("X"), "", "", "", "", "", "", "" };
        }
        return new[]
        {
            "Item" + Mark(i), id.ToString(), Gfx.Hex(id), t.ItemName[id], _c.Art.StaticValid(id) ? Loc.T("ja") : "-", ProblemText(i), "0x" + t.ItemFlags[id].ToString("X"),
            t.Weight[id].ToString(), t.Height[id].ToString(), t.Layer[id].ToString(), t.AnimId[id] != 0 ? t.AnimId[id].ToString() : "",
            t.Hue[id] != 0 ? t.Hue[id].ToString() : "", t.Count[id] != 0 ? t.Count[id].ToString() : "", t.Light[id] != 0 ? t.Light[id].ToString() : "",
        };
    }

    public override FilterDef[] Filters => new[]
    {
        new FilterDef { Label = Loc.T("Typ"), Options = new[] { Loc.T("Alle"), "Land", "Item" }, Pass = (i, o) => o == 0 || (IsLand(i) == (o == 1)) },
        new FilterDef { Label = Loc.T("Eintrag"), Options = new[] { Loc.T("Alle"), Loc.T("Leer (kein Name/Flags)"), Loc.T("Gefuellt") }, Pass = (i, o) => o == 0 || (IsFree(i) == (o == 1)) },
        new FilterDef
        {
            Label = Loc.T("Problem"),
            Options = new[] { Loc.T("Alle"), Loc.T("Irgendein Problem"), Loc.T("Kein Problem") }.Concat(ProblemNames.Select(x => x.name)).ToArray(),
            Pass = (i, o) => o switch
            {
                0 => true,
                1 => _problems[i] != 0,
                2 => _problems[i] == 0,
                _ => (_problems[i] & ProblemNames[o - 3].bit) != 0,
            },
        },
        new FilterDef
        {
            Label = "Flag",
            Options = new[] { Loc.T("Alle") }.Concat(TileData.FlagNames.Select(x => x.name)).ToArray(),
            Pass = (i, o) =>
            {
                if (o == 0)
                {
                    return true;
                }
                ulong f = IsLand(i) ? _c.Tile.LandFlags[i] : _c.Tile.ItemFlags[Id(i)];
                return (f & TileData.FlagNames[o - 1].bit) != 0;
            },
        },
        new FilterDef { Label = "Art", Options = new[] { Loc.T("Alle"), Loc.T("Mit Art"), Loc.T("Ohne Art") }, Pass = (i, o) => o == 0 || ((IsLand(i) ? _c.Art.LandValid(i) : _c.Art.StaticValid(Id(i))) == (o == 1)) },
        new FilterDef { Label = "Sphere", Options = new[] { Loc.T("Alle"), Loc.T("Mit ITEMDEF"), Loc.T("Ohne ITEMDEF") }, Pass = (i, o) => o == 0 || (!IsLand(i) && _c.Catalog.Items.ContainsKey(Id(i)) == (o == 1)) },
    };

    public override string Summary
    {
        get
        {
            var parts = ProblemNames.Select(x => $"{x.name}: {_problems.Count(p => (p & x.bit) != 0)}");
            return Loc.F("{0} Land + {1} Items, Format {2}  |  {3}", _c.Tile.LandCount, _c.Tile.ItemCount, _c.Tile.IsOld ? Loc.T("alt (32-Bit)") : Loc.T("neu (64-Bit)"), string.Join("  |  ", parts));
        }
    }

    public override PreviewData Preview(int i)
    {
        var t = _c.Tile;
        int id = Id(i);
        var p = new PreviewData();
        var sb = new StringBuilder();
        if (IsLand(i))
        {
            p.Image = _c.Art.GetLand(id, out _);
            sb.AppendLine($"Land {id} ({Gfx.Hex(id)}): {t.LandName[id]}");
            sb.AppendLine($"Flags 0x{t.LandFlags[id]:X}: {TileData.FlagText(t.LandFlags[id])}");
            sb.AppendLine($"TexID {t.LandTex[id]}");
        }
        else
        {
            p.Image = _c.Art.GetStatic(id, out _);
            sb.AppendLine($"Item {id} ({Gfx.Hex(id)})");
            sb.AppendLine($"Name:    {(t.ItemName[id].Length > 0 ? t.ItemName[id] : Loc.T("(leer)"))}");
            sb.AppendLine(Loc.F("Gewicht: {0}{1}", t.Weight[id], t.Weight[id] == 255 ? Loc.T(" (nicht aufhebbar)") : ""));
            sb.AppendLine(Loc.F("Hoehe:   {0}", t.Height[id]));
            sb.AppendLine($"Layer:   {t.Layer[id]}");
            sb.AppendLine(Loc.F("Menge:   {0}", t.Count[id]));
            sb.AppendLine($"AnimID:  {t.AnimId[id]}");
            sb.AppendLine($"Hue:     {t.Hue[id]}");
            sb.AppendLine(Loc.F("Licht:   {0}", t.Light[id]));
            sb.AppendLine($"Flags:   0x{t.ItemFlags[id]:X}");
            foreach (var (bit, name) in TileData.FlagNames)
            {
                if ((t.ItemFlags[id] & bit) != 0)
                {
                    sb.AppendLine($"   [x] {name}");
                }
            }
            int anim = t.AnimId[id];
            if (anim != 0)
            {
                foreach (int baseId in new[] { 50000, 60000 })
                {
                    var g = _c.Art.GetGump(baseId + anim, out _);
                    if (g != null)
                    {
                        p.Extras.Add((baseId == 50000 ? Loc.F("Paperdoll m ({0})", baseId + anim) : Loc.F("Paperdoll w ({0})", baseId + anim), g));
                    }
                }
            }
            if (_c.Catalog.Items.TryGetValue(id, out var defs))
            {
                foreach (var d in defs)
                {
                    sb.AppendLine($"Sphere: {d.DefName} \"{d.Name}\" [{d.Category}]");
                }
            }
        }
        p.Editor = new TileEditor(this, i);
        if (_problems[i] != 0)
        {
            sb.AppendLine();
            sb.AppendLine(Loc.T("PROBLEME:"));
            foreach (var (bit, name) in ProblemNames)
            {
                if ((_problems[i] & bit) != 0)
                {
                    sb.AppendLine($"  ! {name}");
                }
            }
        }
        p.Info = sb.ToString();
        return p;
    }
}

public sealed class RadarProvider : ProviderBase, IThumbProvider
{
    private readonly Context _c;
    private ushort[] _col => _c.Radar.Col;

    public Bitmap? Thumb(int i) => Gfx.Solid(Gfx.C16Color(_col[i]), 16, 16);
    public string ThumbLabel(int i) => i < ArtStore.LandCount ? "L" + Gfx.Hex(i) : Gfx.Hex(i - ArtStore.LandCount);

    public RadarProvider(Context c) => _c = c;

    public override string[] Columns => new[] { "Index", Loc.T("Typ"), "ID", "Hex", Loc.T("Farbe (16 Bit)"), "RGB", Loc.T("Status") };
    public override int[] Widths => new[] { 65, 45, 60, 70, 90, 80, 90 };
    public override int Count => _col.Length;
    public override bool IsFree(int i) => _col[i] == 0;

    public override string[] Row(int i)
    {
        bool land = i < ArtStore.LandCount;
        int id = land ? i : i - ArtStore.LandCount;
        uint rgb = Gfx.C16(_col[i]) & 0xFFFFFF;
        return new[] { i.ToString(), land ? "Land" : "Item", id.ToString(), Gfx.Hex(id), "0x" + _col[i].ToString("X4"), "#" + rgb.ToString("X6"), _col[i] == 0 ? Loc.T("schwarz/leer") : "" };
    }

    public override FilterDef[] Filters => new[]
    {
        new FilterDef { Label = Loc.T("Typ"), Options = new[] { Loc.T("Alle"), "Land", "Item" }, Pass = (i, o) => o == 0 || ((i < ArtStore.LandCount) == (o == 1)) },
        new FilterDef { Label = Loc.T("Farbe"), Options = new[] { Loc.T("Alle"), Loc.T("Schwarz/leer"), Loc.T("Gefuellt") }, Pass = (i, o) => o == 0 || (IsFree(i) == (o == 1)) },
    };

    public override string Summary => Loc.F("{0} Eintraege", _col.Length);

    public override PreviewData Preview(int i)
    {
        bool land = i < ArtStore.LandCount;
        int id = land ? i : i - ArtStore.LandCount;
        var p = new PreviewData
        {
            Swatch = Gfx.C16Color(_col[i]),
            Image = land ? _c.Art.GetLand(id, out _) : _c.Art.GetStatic(id, out _),
        };
        p.Editor = new RadarEditor(_c, i);
        p.Info = Loc.F("Radarfarbe {0} {1} ({2}): 0x{3:X4}", land ? "Land" : "Item", id, Gfx.Hex(id), _col[i]);
        return p;
    }
}

public sealed class HuesProvider : ProviderBase, IThumbProvider
{
    private readonly Context _c;

    public Bitmap? Thumb(int i)
    {
        var px = new uint[32 * 16];
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                px[y * 32 + x] = Gfx.C16(_c.Hues.Colors[i][x]);
            }
        }
        return Gfx.ToBitmap(px, 32, 16);
    }

    public string ThumbLabel(int i) => i.ToString();

    public HuesProvider(Context c) => _c = c;

    public override string[] Columns => new[] { "Hue", "Hex", "Name", Loc.T("TabelleStart"), Loc.T("TabelleEnde"), Loc.T("Farbe 1"), Loc.T("Farbe 32") };
    public override int[] Widths => new[] { 55, 65, 200, 85, 85, 70, 70 };
    public override int Count => _c.Hues.Count;
    public override bool IsFree(int i) => _c.Hues.IsFree(i);

    public override string[] Row(int i) => new[]
    {
        i.ToString(), Gfx.Hex(i), _c.Hues.Names[i], _c.Hues.TableStart[i].ToString(), _c.Hues.TableEnd[i].ToString(),
        "0x" + _c.Hues.Colors[i][0].ToString("X4"), "0x" + _c.Hues.Colors[i][31].ToString("X4"),
    };

    public override FilterDef[] Filters => new[]
    {
        new FilterDef { Label = Loc.T("Eintrag"), Options = new[] { Loc.T("Alle"), Loc.T("Leer"), Loc.T("Gefuellt") }, Pass = (i, o) => o == 0 || (IsFree(i) == (o == 1)) },
    };

    public override string Summary => Loc.F("{0} Hues ({1} Gruppen), {2} leer", _c.Hues.Count, _c.Hues.Count / 8, Enumerable.Range(0, Count).Count(IsFree));

    public override PreviewData Preview(int i) => new()
    {
        Editor = new HueEditor(_c, i),
        Palette = _c.Hues.Colors[i].Select(Gfx.C16Color).ToArray(),
        Info = Loc.F("Hue {0} ({1}): \"{2}\"\nTabelle {3} - {4}\n", i, Gfx.Hex(i), _c.Hues.Names[i], _c.Hues.TableStart[i], _c.Hues.TableEnd[i]) +
               string.Join(' ', _c.Hues.Colors[i].Select(x => x.ToString("X4"))),
    };
}

public sealed class SkillsProvider : ProviderBase
{
    private readonly Context _c;

    public SkillsProvider(Context c) => _c = c;

    public override string[] Columns => new[] { "ID", "Hex", "Name", "Button", Loc.T("Status") };
    public override int[] Widths => new[] { 55, 55, 240, 60, 70 };
    public override int Count => _c.Skills.Count;
    public override bool IsFree(int i) => !_c.Skills.Valid[i];

    public override string[] Row(int i) => new[]
    {
        i.ToString(), Gfx.Hex(i), _c.Skills.Names[i], _c.Skills.Valid[i] ? (_c.Skills.Button[i] ? Loc.T("ja") : Loc.T("nein")) : "", IsFree(i) ? Loc.T("frei") : Loc.T("belegt"),
    };

    public override FilterDef[] Filters => new[] { StatusFilter(IsFree) };

    public override string Summary => Loc.F("{0} Slots, {1} belegt", Count, Enumerable.Range(0, Count).Count(i => !IsFree(i)));

    public override PreviewData Preview(int i) => new()
    {
        Editor = new SkillEditor(_c, i),
        Info = Loc.F("Skill {0} ({1}): {2}\nSkill-Button: {3}", i, Gfx.Hex(i), _c.Skills.Names[i], _c.Skills.Button[i] ? Loc.T("ja") : Loc.T("nein")),
    };
}

public sealed class ClilocProvider : ProviderBase
{
    private readonly ClilocData _d;

    public ClilocProvider(ClilocData d) => _d = d;

    public override string[] Columns => new[] { Loc.T("Nummer"), "Hex", "Text" };
    public override int[] Widths => new[] { 80, 80, 700 };
    public override int Count => _d.Numbers.Length;
    public override int KeyOf(int i) => _d.Numbers[i];
    public override string[] Row(int i) => new[] { _d.Numbers[i].ToString(), Gfx.Hex(_d.Numbers[i]), _d.Texts[i] };
    public override string SearchText(int i) => $"{_d.Numbers[i]} 0x{_d.Numbers[i]:x} {_d.Texts[i]}".ToLowerInvariant();

    public override string Summary => _d.Error.Length > 0 ? Loc.T("Fehler: ") + _d.Error : Loc.F("{0} Eintraege{1}", Count, _d.Compressed ? Loc.T(" (BWT-komprimiert)") : "");

    public override PreviewData Preview(int i) => new()
    {
        Editor = new ClilocEditor(_d, _d.Numbers[i]),
        Info = $"Cliloc {_d.Numbers[i]} ({Gfx.Hex(_d.Numbers[i])})\n\n{_d.Texts[i]}",
    };
}

public enum BodyMode
{
    Monster,
    ItemAnim,
}

public sealed class BodyAnimProvider : ProviderBase, IThumbProvider
{
    public Bitmap? Thumb(int i)
    {
        var src = _rows[i].Sources.FirstOrDefault();
        if (src == null)
        {
            return null;
        }
        var frames = _c.Anim.Decode(src, src.Actions[0], 0, _mode == BodyMode.ItemAnim, out _);
        return frames.FirstOrDefault(f => f.Bmp != null)?.Bmp;
    }

    public int UsedCount => _rows.Count(r => r.Sources.Count > 0);
    public int MissingCount => _rows.Count(r => r.Sources.Count == 0 && r.CharCount > 0);
    public int UopOnlyCount => _rows.Count(r => r.HasUop && !r.HasMul);

    public string ThumbLabel(int i)
    {
        var r = _rows[i];
        string n = _mode == BodyMode.Monster ? r.Names : r.ItemExample;
        return n.Length > 0 ? $"{r.Id} {n}" : r.Id.ToString();
    }

    private sealed class Row_
    {
        public int Id;
        public List<AnimSource> Sources = new();
        public string Short = "";
        public int Actions;
        public bool HasMul;
        public bool HasUop;
        public int CharCount;
        public string Names = "";
        public bool IsItemAnim;
        public int ItemUsers;
        public string ItemExample = "";
        public string ItemDefs = "";
    }

    private readonly Context _c;
    private readonly BodyMode _mode;
    private readonly List<Row_> _rows = new();

    public BodyAnimProvider(Context c, BodyMode mode)
    {
        _c = c;
        _mode = mode;

        var animUsers = new Dictionary<int, List<int>>();
        for (int i = 0; i < c.Tile.ItemCount; i++)
        {
            if (c.Tile.AnimId[i] != 0)
            {
                if (!animUsers.TryGetValue(c.Tile.AnimId[i], out var l))
                {
                    animUsers[c.Tile.AnimId[i]] = l = new List<int>();
                }
                l.Add(i);
            }
        }

        IEnumerable<int> ids;
        if (mode == BodyMode.Monster)
        {
            int max = Math.Max(c.Anim.MaxIdOverall(), 4000);
            var set = new SortedSet<int>(Enumerable.Range(0, max + 1));
            foreach (int k in c.Catalog.Chars.Keys)
            {
                set.Add(k);
            }
            ids = set;
        }
        else
        {
            ids = animUsers.Keys.OrderBy(x => x);
        }

        foreach (int id in ids)
        {
            var r = new Row_ { Id = id };
            r.Sources = c.Anim.SourcesFor(id);
            r.HasMul = r.Sources.Any(s => s.Kind == "MUL");
            r.HasUop = r.Sources.Any(s => s.Kind == "UOP");
            r.Actions = r.Sources.Count == 0 ? 0 : r.Sources.Max(s => s.Actions.Length);
            r.Short = string.Join(", ", r.Sources.Select(s => s.Kind == "UOP" ? "UOP" : $"A{s.File + 1}").Distinct());
            r.CharCount = c.Catalog.Chars.TryGetValue(id, out var cl) ? cl.Count : 0;
            r.Names = c.Catalog.CharNames(id);
            r.IsItemAnim = animUsers.ContainsKey(id);
            if (animUsers.TryGetValue(id, out var users))
            {
                r.ItemUsers = users.Count;
                r.ItemExample = string.Join(", ", users.Select(u => c.Tile.ItemName[u]).Where(n => n.Length > 0).Distinct().Take(3));
                r.ItemDefs = string.Join(", ", users.Select(u => c.Catalog.ItemNames(u, 1)).Where(n => n.Length > 0).Distinct().Take(2));
            }
            _rows.Add(r);
        }
    }

    public override string[] Columns => _mode == BodyMode.Monster
        ? new[] { "Body", "Hex", Loc.T("Typ"), Loc.T("Status"), Loc.T("Quellen"), Loc.T("Aktionen"), "Sphere-CHARDEF", "#", "Item-Anim" }
        : new[] { "AnimID", "Hex", Loc.T("Status"), Loc.T("Quellen"), Loc.T("Aktionen"), "Items", Loc.T("Beispiele (Tiledata)"), "Sphere-ITEMDEF" };

    public override int[] Widths => _mode == BodyMode.Monster
        ? new[] { 55, 65, 110, 70, 110, 60, 330, 30, 70 }
        : new[] { 60, 65, 70, 110, 60, 50, 260, 260 };

    public override int Count => _rows.Count;
    public override int KeyOf(int i) => _rows[i].Id;
    public override bool IsFree(int i) => _rows[i].Sources.Count == 0;

    public override string[] Row(int i)
    {
        var r = _rows[i];
        string status = r.Sources.Count > 0 ? Loc.T("belegt") : (r.CharCount > 0 ? Loc.T("FEHLT") : Loc.T("frei"));
        if (_mode == BodyMode.Monster)
        {
            return new[] { r.Id.ToString(), Gfx.Hex(r.Id), AnimStore.TypeName(r.Id), status, r.Short, r.Actions > 0 ? r.Actions.ToString() : "", r.Names, r.CharCount > 0 ? r.CharCount.ToString() : "", r.IsItemAnim ? Loc.T("ja") : "" };
        }
        return new[] { r.Id.ToString(), Gfx.Hex(r.Id), status, r.Short, r.Actions > 0 ? r.Actions.ToString() : "", r.ItemUsers.ToString(), r.ItemExample, r.ItemDefs };
    }

    public override string SearchText(int i)
    {
        var r = _rows[i];
        return $"{r.Id} 0x{r.Id:x} {r.Names} {(r.CharCount > 0 ? string.Join(' ', _c.Catalog.Chars[r.Id].Select(d => d.Name + " " + d.DefName)) : "")} {r.ItemExample} {r.ItemDefs} {r.Short}".ToLowerInvariant();
    }

    public override FilterDef[] Filters
    {
        get
        {
            var list = new List<FilterDef>
            {
                new FilterDef { Label = Loc.T("Status"), Options = new[] { Loc.T("Alle"), Loc.T("Belegt"), Loc.T("Frei/fehlt"), Loc.T("Nur FEHLT (Chardef ohne Animation)") }, Pass = (i, o) => o switch { 0 => true, 1 => !IsFree(i), 2 => IsFree(i), _ => IsFree(i) && _rows[i].CharCount > 0 } },
                new FilterDef
                {
                    Label = Loc.T("Quelle"),
                    Options = new[] { Loc.T("Alle"), Loc.T("Nur Mul"), Loc.T("Nur UOP"), "Mul + UOP" },
                    Pass = (i, o) => o switch { 0 => true, 1 => _rows[i].HasMul && !_rows[i].HasUop, 2 => _rows[i].HasUop && !_rows[i].HasMul, _ => _rows[i].HasMul && _rows[i].HasUop },
                },
            };
            if (_mode == BodyMode.Monster)
            {
                list.Add(new FilterDef { Label = "Chardef", Options = new[] { Loc.T("Alle"), Loc.T("Mit Chardef"), Loc.T("Ohne Chardef") }, Pass = (i, o) => o == 0 || ((_rows[i].CharCount > 0) == (o == 1)) });
                list.Add(new FilterDef { Label = Loc.T("Typ"), Options = new[] { Loc.T("Alle"), Loc.T("Hoch (<200)"), Loc.T("Niedrig (200-399)"), Loc.T("Menschlich (400+)") }, Pass = (i, o) => o == 0 || (o == 1 ? _rows[i].Id < 200 : o == 2 ? _rows[i].Id is >= 200 and < 400 : _rows[i].Id >= 400) });
                list.Add(new FilterDef { Label = "Item-AnimID", Options = new[] { Loc.T("Alle"), Loc.T("Nur Item-AnimIDs"), Loc.T("Ohne Item-AnimIDs") }, Pass = (i, o) => o == 0 || (_rows[i].IsItemAnim == (o == 1)) });
            }
            return list.ToArray();
        }
    }

    public override string Summary
    {
        get
        {
            int used = _rows.Count(r => r.Sources.Count > 0);
            int missing = _rows.Count(r => r.Sources.Count == 0 && r.CharCount > 0);
            return Loc.F("{0} Zeilen, {1} mit Animation, {2} Chardefs ohne Animation", _rows.Count, used, missing);
        }
    }

    public override PreviewData Preview(int i)
    {
        var r = _rows[i];
        var p = new PreviewData();
        var sb = new StringBuilder();
        sb.AppendLine($"{(_mode == BodyMode.Monster ? "Body" : "AnimID")} {r.Id} ({Gfx.Hex(r.Id)}) - {AnimStore.TypeName(r.Id)}");
        if (_c.Anim.BodyDef.TryGetValue(r.Id, out var bd))
        {
            sb.AppendLine("body.def: " + string.Join(", ", bd));
        }
        if (_c.Anim.BodyConv.TryGetValue(r.Id, out var bc))
        {
            sb.AppendLine("bodyconv.def: anim2=" + bc[0] + " anim3=" + bc[1] + " anim4=" + bc[2] + " anim5=" + bc[3]);
        }
        if (_c.Anim.MobTypes.TryGetValue(r.Id, out var mt))
        {
            sb.AppendLine($"mobtypes.txt: {mt.type} {mt.flags}");
        }
        if (r.Sources.Count == 0)
        {
            sb.AppendLine(Loc.T("Keine Animationsdaten in anim.mul, anim2-6.mul oder UOP."));
        }
        if (_c.Catalog.Chars.TryGetValue(r.Id, out var defs))
        {
            sb.AppendLine("Sphere-CHARDEFs:");
            foreach (var d in defs.Take(40))
            {
                sb.AppendLine($"  {(d.DefName.Length > 0 ? d.DefName : d.Header)} \"{d.Name}\" [{d.Category}]  {d.File}");
            }
        }
        if (r.ItemUsers > 0)
        {
            sb.AppendLine(Loc.F("Item-AnimID, benutzt von {0} Items, z.B.: {1}", r.ItemUsers, r.ItemExample));
            var users = Enumerable.Range(0, _c.Tile.ItemCount).Where(x => _c.Tile.AnimId[x] == r.Id).Take(12).ToList();
            foreach (int u in users)
            {
                sb.AppendLine($"  Item {Gfx.Hex(u)} {_c.Tile.ItemName[u]} {_c.Catalog.ItemNames(u, 1)}");
            }
        }
        if (_mode == BodyMode.ItemAnim)
        {
            var gm = _c.Art.GetGump(50000 + r.Id, out _);
            if (gm != null)
            {
                p.Extras.Add((Loc.F("Paperdoll m (Gump {0})", 50000 + r.Id), gm));
            }
            var gf = _c.Art.GetGump(60000 + r.Id, out _);
            if (gf != null)
            {
                p.Extras.Add((Loc.F("Paperdoll w (Gump {0})", 60000 + r.Id), gf));
            }
        }
        p.Anim = new AnimRef { Store = _c.Anim, Sources = r.Sources, Equipment = _mode == BodyMode.ItemAnim };
        p.Info = sb.ToString();
        return p;
    }
}

public sealed class RawAnimProvider : ProviderBase, IThumbProvider
{
    public Bitmap? Thumb(int i)
    {
        if (_actions[i].Length == 0)
        {
            return null;
        }
        var src = new AnimSource { Kind = _file < 0 ? "UOP" : "MUL", File = _file, Id = i, Actions = _actions[i] };
        var frames = _c.Anim.Decode(src, _actions[i][0], 0, false, out _);
        return frames.FirstOrDefault(f => f.Bmp != null)?.Bmp;
    }

    public string ThumbLabel(int i) => i.ToString();

    private readonly Context _c;
    private readonly int _file;
    private readonly int[][] _actions;
    private readonly int _count;

    public RawAnimProvider(Context c, int file)
    {
        _c = c;
        _file = file;
        _count = file < 0 ? 4001 : Math.Max(0, c.Anim.MaxId(file) + 1);
        _actions = new int[_count][];
        for (int i = 0; i < _count; i++)
        {
            _actions[i] = file < 0 ? c.Anim.UopActions(i) : c.Anim.MulActions(file, i);
        }
    }

    public override string[] Columns => new[] { "ID", "Hex", Loc.T("Typ"), Loc.T("Status"), Loc.T("Aktionen"), Loc.T("Aktions-Nummern"), Loc.T("Sphere (gleiche Nr. als Body)") };
    public override int[] Widths => new[] { 55, 65, 110, 60, 60, 220, 300 };
    public override int Count => _count;
    public override bool IsFree(int i) => _actions[i].Length == 0;

    public override string[] Row(int i) => new[]
    {
        i.ToString(), Gfx.Hex(i), AnimStore.TypeName(i), IsFree(i) ? Loc.T("frei") : Loc.T("belegt"), _actions[i].Length > 0 ? _actions[i].Length.ToString() : "",
        string.Join(",", _actions[i]), _c.Catalog.CharNames(i),
    };

    public override FilterDef[] Filters => new[] { StatusFilter(IsFree) };

    public override string Summary => Loc.F("{0} Slots, {1} belegt (Datei-Index; bodyconv.def/body.def werden hier NICHT angewendet)", _count, _actions.Count(a => a.Length > 0));

    public override PreviewData Preview(int i)
    {
        var p = new PreviewData();
        var src = new AnimSource
        {
            Kind = _file < 0 ? "UOP" : "MUL",
            File = _file,
            Id = i,
            Label = _file < 0 ? $"UOP Body {i}" : $"{AnimStore.FileNames[_file]}.mul Index {i}",
            Actions = _actions[i],
        };
        p.Info = $"{src.Label}\n{(IsFree(i) ? Loc.T("Slot ist frei.") : Loc.F("{0} Aktionen", _actions[i].Length))}";
        if (!IsFree(i))
        {
            p.Anim = new AnimRef { Store = _c.Anim, Sources = new List<AnimSource> { src }, Equipment = false };
        }
        return p;
    }
}

