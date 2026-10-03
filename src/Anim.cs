using System.Drawing;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace UOTinker;

public sealed class AnimFrame
{
    public Bitmap? Bmp;
    public int Cx;
    public int Cy;
    public int W;
    public int H;
}

public sealed class UopEntry
{
    public int FileIndex;
    public long Offset;
    public int Compressed;
    public int Decompressed;
    public short Flag;
}

public sealed class UopSet
{
    public Dictionary<ulong, UopEntry> Entries { get; } = new();
    public List<string> Files { get; } = new();
    private readonly List<MulFile> _files = new();

    public UopSet(string folder)
    {
        for (int n = 1; n <= 9; n++)
        {
            string p = Path.Combine(folder, $"AnimationFrame{n}.uop");
            if (!File.Exists(p))
            {
                continue;
            }
            int fi = Files.Count;
            Files.Add($"AnimationFrame{n}.uop");
            var mf = new MulFile(p);
            _files.Add(mf);
            ReadTable(mf, fi);
        }
    }

    private void ReadTable(MulFile f, int fi)
    {
        var head = f.Read(0, 28);
        if (head == null || BitConverter.ToUInt32(head, 0) != 0x50594D)
        {
            return;
        }
        long next = BitConverter.ToInt64(head, 12);
        while (next != 0)
        {
            var bh = f.Read(next, 12);
            if (bh == null)
            {
                break;
            }
            int count = BitConverter.ToInt32(bh, 0);
            long nn = BitConverter.ToInt64(bh, 4);
            var tbl = f.Read(next + 12, count * 34);
            if (tbl == null)
            {
                break;
            }
            for (int i = 0; i < count; i++)
            {
                int o = i * 34;
                long offset = BitConverter.ToInt64(tbl, o);
                if (offset == 0)
                {
                    continue;
                }
                int hl = BitConverter.ToInt32(tbl, o + 8);
                ulong hash = BitConverter.ToUInt64(tbl, o + 20);
                Entries[hash] = new UopEntry
                {
                    FileIndex = fi,
                    Offset = offset + hl,
                    Compressed = BitConverter.ToInt32(tbl, o + 12),
                    Decompressed = BitConverter.ToInt32(tbl, o + 16),
                    Flag = BitConverter.ToInt16(tbl, o + 32),
                };
            }
            next = nn;
        }
    }

    public UopEntry? Find(int body, int action)
    {
        ulong h = Hash($"build/animationlegacyframe/{body:D6}/{action:D2}.bin");
        return Entries.TryGetValue(h, out var e) ? e : null;
    }

    public byte[]? ReadData(UopEntry e) => _files[e.FileIndex].Read(e.Offset, e.Compressed);

    public static ulong Hash(string s)
    {
        uint eax, ecx, edx, ebx, esi, edi;
        eax = ecx = edx = 0;
        ebx = edi = esi = (uint)s.Length + 0xDEADBEEF;
        int i;
        for (i = 0; i + 12 < s.Length; i += 12)
        {
            edi = (uint)((s[i + 7] << 24) | (s[i + 6] << 16) | (s[i + 5] << 8) | s[i + 4]) + edi;
            esi = (uint)((s[i + 11] << 24) | (s[i + 10] << 16) | (s[i + 9] << 8) | s[i + 8]) + esi;
            edx = (uint)((s[i + 3] << 24) | (s[i + 2] << 16) | (s[i + 1] << 8) | s[i]) - esi;
            edx = (edx + ebx) ^ (esi >> 28) ^ (esi << 4);
            esi += edi;
            edi = (edi - edx) ^ (edx >> 26) ^ (edx << 6);
            edx += esi;
            esi = (esi - edi) ^ (edi >> 24) ^ (edi << 8);
            edi += edx;
            ebx = (edx - esi) ^ (esi >> 16) ^ (esi << 16);
            esi += edi;
            edi = (edi - ebx) ^ (ebx >> 13) ^ (ebx << 19);
            ebx += esi;
            esi = (esi - edi) ^ (edi >> 28) ^ (edi << 4);
            edi += ebx;
        }

        if (s.Length - i > 0)
        {
            switch (s.Length - i)
            {
                case 12: esi += (uint)s[i + 11] << 24; goto case 11;
                case 11: esi += (uint)s[i + 10] << 16; goto case 10;
                case 10: esi += (uint)s[i + 9] << 8; goto case 9;
                case 9: esi += s[i + 8]; goto case 8;
                case 8: edi += (uint)s[i + 7] << 24; goto case 7;
                case 7: edi += (uint)s[i + 6] << 16; goto case 6;
                case 6: edi += (uint)s[i + 5] << 8; goto case 5;
                case 5: edi += s[i + 4]; goto case 4;
                case 4: ebx += (uint)s[i + 3] << 24; goto case 3;
                case 3: ebx += (uint)s[i + 2] << 16; goto case 2;
                case 2: ebx += (uint)s[i + 1] << 8; goto case 1;
                case 1: ebx += s[i]; break;
            }

            esi = (esi ^ edi) - ((edi >> 18) ^ (edi << 14));
            ecx = (esi ^ ebx) - ((esi >> 21) ^ (esi << 11));
            edi = (edi ^ ecx) - ((ecx >> 7) ^ (ecx << 25));
            esi = (esi ^ edi) - ((edi >> 16) ^ (edi << 16));
            edx = (esi ^ ecx) - ((esi >> 28) ^ (esi << 4));
            edi = (edi ^ edx) - ((edx >> 18) ^ (edx << 14));
            eax = (esi ^ edi) - ((edi >> 8) ^ (edi << 24));
            return ((ulong)edi << 32) | eax;
        }
        return ((ulong)esi << 32) | eax;
    }
}

public sealed class AnimSource
{
    public string Kind = "MUL";
    public int File;
    public int Id;
    public string Label = "";
    public int[] Actions = Array.Empty<int>();
}

public sealed class AnimStore
{
    public static readonly string[] FileNames = { "anim", "anim2", "anim3", "anim4", "anim5", "anim6" };

    public string Folder { get; }
    public uint[]?[] Pos { get; } = new uint[6][];
    public uint[]?[] Size { get; } = new uint[6][];
    public MulFile?[] Mul { get; } = new MulFile?[6];
    public UopSet Uop { get; }
    public Dictionary<int, int[]> BodyDef { get; } = new();
    public Dictionary<int, int[]> BodyConv { get; } = new();
    public Dictionary<int, (string type, string flags)> MobTypes { get; } = new();

    public AnimStore(string folder)
    {
        Folder = folder;
        for (int f = 0; f < 6; f++)
        {
            string idx = Path.Combine(folder, FileNames[f] + ".idx");
            string mul = Path.Combine(folder, FileNames[f] + ".mul");
            if (!File.Exists(idx) || !File.Exists(mul))
            {
                continue;
            }
            var b = File.ReadAllBytes(idx);
            int n = b.Length / 12;
            var pos = new uint[n];
            var size = new uint[n];
            for (int i = 0; i < n; i++)
            {
                pos[i] = BitConverter.ToUInt32(b, i * 12);
                size[i] = BitConverter.ToUInt32(b, i * 12 + 4);
            }
            Pos[f] = pos;
            Size[f] = size;
            Mul[f] = new MulFile(mul);
        }
        Uop = new UopSet(folder);
        ParseBodyDef(Path.Combine(folder, "body.def"));
        ParseBodyConv(Path.Combine(folder, "bodyconv.def"));
        ParseMobTypes(Path.Combine(folder, "mobtypes.txt"));
    }

    private void ParseBodyDef(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw;
            int h = line.IndexOf('#');
            if (h >= 0)
            {
                line = line[..h];
            }
            int o = line.IndexOf('{');
            int c = line.IndexOf('}');
            if (o < 0 || c < o)
            {
                continue;
            }
            if (!int.TryParse(line[..o].Trim(), out int src))
            {
                continue;
            }
            var t = line[(o + 1)..c].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => int.TryParse(x, out int v) ? v : -1).Where(v => v >= 0).ToArray();
            if (t.Length > 0)
            {
                BodyDef[src] = t;
            }
        }
    }

    private void ParseBodyConv(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw;
            int h = line.IndexOf('#');
            if (h >= 0)
            {
                line = line[..h];
            }
            var p = line.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (p.Length < 2 || !int.TryParse(p[0], out int body))
            {
                continue;
            }
            var cols = new int[4] { -1, -1, -1, -1 };
            for (int i = 1; i < p.Length && i <= 4; i++)
            {
                if (int.TryParse(p[i], out int v))
                {
                    cols[i - 1] = v;
                }
            }
            BodyConv[body] = cols;
        }
    }

    private void ParseMobTypes(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }
        foreach (var raw in File.ReadLines(path))
        {
            var p = raw.Split('\t');
            if (p.Length >= 3 && int.TryParse(p[0].Trim(), out int id))
            {
                MobTypes[id] = (p[1].Trim(), p[2].Trim());
            }
        }
    }

    public static (int start, int actions) Layout(int id)
    {
        if (id < 200)
        {
            return (id * 110, 22);
        }
        if (id < 400)
        {
            return (22000 + (id - 200) * 65, 13);
        }
        return (35000 + (id - 400) * 175, 35);
    }

    public static string TypeName(int id) => id < 200 ? Loc.T("Hoch (Monster)") : id < 400 ? Loc.T("Niedrig (Tier)") : Loc.T("Menschlich/Item");

    public int MaxId(int file)
    {
        var pos = Pos[file];
        if (pos == null)
        {
            return -1;
        }
        int n = pos.Length;
        if (n <= 22000)
        {
            return n / 110 - 1;
        }
        if (n <= 35000)
        {
            return 200 + (n - 22000) / 65 - 1;
        }
        return 400 + (n - 35000) / 175 - 1;
    }

    public int MaxIdOverall()
    {
        int m = 0;
        for (int f = 0; f < 6; f++)
        {
            m = Math.Max(m, MaxId(f));
        }
        return m;
    }

    public int[] MulActions(int file, int id)
    {
        var pos = Pos[file];
        var size = Size[file];
        if (pos == null || size == null)
        {
            return Array.Empty<int>();
        }
        var (start, actions) = Layout(id);
        var res = new List<int>();
        for (int a = 0; a < actions; a++)
        {
            for (int d = 0; d < 5; d++)
            {
                int ix = start + a * 5 + d;
                if (ix < pos.Length && pos[ix] != 0xFFFFFFFF && size[ix] != 0 && size[ix] != 0xFFFFFFFF)
                {
                    res.Add(a);
                    break;
                }
            }
        }
        return res.ToArray();
    }

    public int[] UopActions(int id)
    {
        var res = new List<int>();
        for (int a = 0; a < 100; a++)
        {
            if (Uop.Find(id, a) != null)
            {
                res.Add(a);
            }
        }
        return res.ToArray();
    }

    public bool UopHas(int id)
    {
        for (int a = 0; a < 100; a++)
        {
            if (Uop.Find(id, a) != null)
            {
                return true;
            }
        }
        return false;
    }

    public List<AnimSource> SourcesFor(int body)
    {
        var list = new List<AnimSource>();
        var seen = new HashSet<string>();

        void AddMul(int file, int id, string label)
        {
            if (Pos[file] == null || !seen.Add($"M{file}:{id}"))
            {
                return;
            }
            var acts = MulActions(file, id);
            if (acts.Length > 0)
            {
                list.Add(new AnimSource { Kind = "MUL", File = file, Id = id, Label = label, Actions = acts });
            }
        }

        void AddUop(int id, string label)
        {
            if (!seen.Add($"U:{id}"))
            {
                return;
            }
            var acts = UopActions(id);
            if (acts.Length > 0)
            {
                string flag = MobTypes.TryGetValue(id, out var mt) ? $"mobtypes: {mt.type} {mt.flags}" : Loc.T("kein mobtypes-Eintrag");
                list.Add(new AnimSource { Kind = "UOP", File = -1, Id = id, Label = $"{label} ({flag})", Actions = acts });
            }
        }

        void Collect(int id, string prefix)
        {
            AddMul(0, id, $"{prefix}anim.mul Body {id}");
            if (BodyConv.TryGetValue(id, out var c))
            {
                for (int k = 0; k < 4; k++)
                {
                    if (c[k] >= 0)
                    {
                        AddMul(k + 1, c[k], $"{prefix}{FileNames[k + 1]}.mul Index {c[k]} (bodyconv.def)");
                    }
                }
            }
            AddUop(id, $"{prefix}UOP Body {id}");
        }

        Collect(body, "");
        if (BodyDef.TryGetValue(body, out var defs))
        {
            foreach (int t in defs)
            {
                Collect(t, $"body.def {body}->{t}: ");
            }
        }
        return list;
    }

    public List<AnimFrame> Decode(AnimSource s, int action, int dir, bool equipment, out string error)
    {
        error = "";
        try
        {
            return s.Kind == "UOP" ? DecodeUop(s.Id, action, dir, equipment, out error) : DecodeMul(s.File, s.Id, action, dir, out error);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return new List<AnimFrame>();
        }
    }

    private List<AnimFrame> DecodeMul(int file, int id, int action, int dir, out string error)
    {
        error = "";
        var res = new List<AnimFrame>();
        var pos = Pos[file];
        var size = Size[file];
        var mul = Mul[file];
        if (pos == null || size == null || mul == null)
        {
            error = Loc.T("Datei nicht vorhanden.");
            return res;
        }
        var (start, _) = Layout(id);
        int ix = start + action * 5 + dir;
        if (ix >= pos.Length || pos[ix] == 0xFFFFFFFF || size[ix] == 0 || size[ix] == 0xFFFFFFFF)
        {
            error = Loc.T("Leerer Index-Eintrag.");
            return res;
        }
        var buf = mul.Read(pos[ix], (int)size[ix]);
        if (buf == null || buf.Length < 516)
        {
            error = Loc.T("Daten ausserhalb der Datei oder zu kurz.");
            return res;
        }
        var palette = new ushort[256];
        for (int i = 0; i < 256; i++)
        {
            palette[i] = BitConverter.ToUInt16(buf, i * 2);
        }
        int dataStart = 512;
        uint count = BitConverter.ToUInt32(buf, dataStart);
        if (count == 0 || count > 500)
        {
            error = Loc.F("Unplausible Frame-Anzahl {0}.", count);
            return res;
        }
        for (int i = 0; i < count; i++)
        {
            uint off = BitConverter.ToUInt32(buf, dataStart + 4 + i * 4);
            res.Add(ReadSprite(buf, dataStart + (int)off, palette));
        }
        return res;
    }

    private static AnimFrame ReadSprite(byte[] buf, int p, ushort[] palette)
    {
        var f = new AnimFrame();
        if (p < 0 || p + 8 > buf.Length)
        {
            return f;
        }
        short cx = BitConverter.ToInt16(buf, p);
        short cy = BitConverter.ToInt16(buf, p + 2);
        short w = BitConverter.ToInt16(buf, p + 4);
        short h = BitConverter.ToInt16(buf, p + 6);
        p += 8;
        f.Cx = cx;
        f.Cy = cy;
        f.W = w;
        f.H = h;
        if (w <= 0 || h <= 0 || w > 2000 || h > 2000)
        {
            return f;
        }
        var px = new uint[w * h];
        while (p + 4 <= buf.Length)
        {
            uint header = BitConverter.ToUInt32(buf, p);
            p += 4;
            if (header == 0x7FFF7FFF)
            {
                break;
            }
            int run = (int)(header & 0x0FFF);
            int x = (int)((header >> 22) & 0x03FF);
            if ((x & 0x0200) != 0)
            {
                x |= unchecked((int)0xFFFFFE00);
            }
            int y = (int)((header >> 12) & 0x3FF);
            if ((y & 0x0200) != 0)
            {
                y |= unchecked((int)0xFFFFFE00);
            }
            x += cx;
            y += cy + h;
            int block = y * w + x;
            for (int k = 0; k < run && p < buf.Length; k++, block++)
            {
                ushort val = palette[buf[p++]];
                if (val != 0 && block >= 0 && block < px.Length)
                {
                    px[block] = Gfx.C16(val);
                }
            }
        }
        f.Bmp = Gfx.ToBitmap(px, w, h);
        return f;
    }

    private List<AnimFrame> DecodeUop(int id, int action, int dir, bool equipment, out string error)
    {
        error = "";
        var res = new List<AnimFrame>();
        var e = Uop.Find(id, action);
        if (e == null)
        {
            error = Loc.T("Kein UOP-Eintrag fuer diese Aktion.");
            return res;
        }
        var raw = Uop.ReadData(e);
        if (raw == null)
        {
            error = Loc.T("UOP-Daten ausserhalb der Datei.");
            return res;
        }
        byte[] data = raw;
        if (e.Flag >= 1)
        {
            using var ms = new MemoryStream(raw);
            using var z = new ZLibStream(ms, CompressionMode.Decompress);
            using var outMs = new MemoryStream();
            z.CopyTo(outMs);
            data = outMs.ToArray();
            if (e.Flag == 3)
            {
                data = ClassicUO.Utility.BwtDecompress.Decompress(data);
            }
        }

        if (data.Length < 40)
        {
            error = Loc.T("UOP-Block zu kurz.");
            return res;
        }
        int fc = BitConverter.ToInt32(data, 32);
        uint dataStart = BitConverter.ToUInt32(data, 36);
        if (fc <= 0 || fc > 5000)
        {
            error = Loc.F("Unplausible Frame-Anzahl {0}.", fc);
            return res;
        }

        var recs = new List<(int pos, int frameId, int pixelOffset)>();
        long q = dataStart;
        for (int i = 0; i < fc; i++, q += 16)
        {
            if (q + 16 > data.Length)
            {
                break;
            }
            int frameId = BitConverter.ToUInt16(data, (int)q + 2);
            uint pixelOffset = BitConverter.ToUInt32(data, (int)q + 12);
            recs.Add(((int)q, frameId, (int)pixelOffset));
        }

        var list = new List<(int pos, int frameId, int pixelOffset)>();
        int last = 1;
        foreach (var r in recs)
        {
            while (r.frameId - last > 1)
            {
                last++;
                list.Add((0, last, 0));
            }
            list.Add(r);
            last = r.frameId;
        }

        int real = (int)Math.Round(list.Count / 5.0);
        if (equipment)
        {
            real = Math.Max(10, real);
        }
        if (real <= 0)
        {
            return res;
        }

        var frames = new AnimFrame?[real];
        foreach (var r in list)
        {
            int fd = (r.frameId - 1) / real;
            if (fd < dir)
            {
                continue;
            }
            if (fd > dir)
            {
                break;
            }
            int idx = (r.frameId - 1) % real;
            frames[idx] = new AnimFrame();
            if (r.pos == 0)
            {
                continue;
            }
            int sp = r.pos + r.pixelOffset;
            if (sp + 512 > data.Length)
            {
                continue;
            }
            var pal = new ushort[256];
            for (int i = 0; i < 256; i++)
            {
                pal[i] = BitConverter.ToUInt16(data, sp + i * 2);
            }
            frames[idx] = ReadSprite(data, sp + 512, pal);
        }
        foreach (var f in frames)
        {
            res.Add(f ?? new AnimFrame());
        }
        return res;
    }
}

public sealed class DefInfo
{
    public string Kind = "";
    public string Header = "";
    public string DefName = "";
    public string Name = "";
    public string Category = "";
    public string File = "";
    public string FullPath = "";
    public int Line;
    public int HeaderId = -1;
    public int ResolvedId = -1;
}

public sealed class SphereCatalog
{
    public Dictionary<int, List<DefInfo>> Chars { get; } = new();
    public Dictionary<int, List<DefInfo>> Items { get; } = new();
    public List<DefInfo> ItemDefs { get; } = new();
    public int FileCount { get; private set; }
    public bool Loaded { get; private set; }

    private static readonly Regex HeaderRx = new(@"^\[\s*(CHARDEF|ITEMDEF)\s+([^\]\s]+)\s*\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool TryNum(string t, out int v)
    {
        v = 0;
        t = t.Trim();
        if (t.Length == 0)
        {
            return false;
        }
        try
        {
            if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                v = Convert.ToInt32(t[2..], 16);
                return true;
            }
            if (t.Length > 1 && t[0] == '0' && t.All(Uri.IsHexDigit))
            {
                v = Convert.ToInt32(t, 16);
                return true;
            }
            return int.TryParse(t, out v);
        }
        catch
        {
            return false;
        }
    }

    public SphereCatalog(string scriptsFolder)
    {
        if (!Directory.Exists(scriptsFolder))
        {
            return;
        }
        var all = new List<DefInfo>();
        foreach (var file in Directory.EnumerateFiles(scriptsFolder, "*.scp", SearchOption.AllDirectories))
        {
            if (file.Contains(@"\web\", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            FileCount++;
            string rel = Path.GetRelativePath(scriptsFolder, file);
            DefInfo? cur = null;
            string? idTok = null;
            var pendingId = new List<(DefInfo d, string tok)>();
            int lineNo = 0;
            foreach (var raw in File.ReadLines(file, System.Text.Encoding.Latin1))
            {
                lineNo++;
                if (raw.Length == 0)
                {
                    continue;
                }
                var line = raw.TrimStart();
                if (line.Length > 0 && line[0] == '[')
                {
                    var m = HeaderRx.Match(line);
                    if (m.Success)
                    {
                        cur = new DefInfo { Kind = m.Groups[1].Value.ToUpperInvariant(), Header = m.Groups[2].Value, File = rel, FullPath = file, Line = lineNo };
                        all.Add(cur);
                        idTok = null;
                    }
                    else
                    {
                        cur = null;
                    }
                    continue;
                }
                if (cur == null)
                {
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }
                string key = line[..eq].Trim();
                if (key.Equals("DEFNAME", StringComparison.OrdinalIgnoreCase))
                {
                    cur.DefName = line[(eq + 1)..].Trim();
                }
                else if (key.Equals("NAME", StringComparison.OrdinalIgnoreCase) && cur.Name.Length == 0)
                {
                    cur.Name = StripComment(line[(eq + 1)..]).Trim().Trim('"');
                }
                else if (key.Equals("CATEGORY", StringComparison.OrdinalIgnoreCase))
                {
                    cur.Category = line[(eq + 1)..].Trim();
                }
                else if (key.Equals("ID", StringComparison.OrdinalIgnoreCase) && idTok == null)
                {
                    idTok = StripComment(line[(eq + 1)..]).Trim();
                    pendingId.Add((cur, idTok));
                }
            }
            _pending.AddRange(pendingId);
        }

        var charNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var itemNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in all)
        {
            if (TryNum(d.Header, out int n))
            {
                d.HeaderId = n;
                (d.Kind == "CHARDEF" ? charNames : itemNames)[d.DefName.Length > 0 ? d.DefName : d.Header] = n;
                Add(d, n);
            }
        }
        for (int pass = 0; pass < 3; pass++)
        {
            foreach (var (d, tok) in _pending)
            {
                int n;
                var dict = d.Kind == "CHARDEF" ? charNames : itemNames;
                if (TryNum(tok, out n) || dict.TryGetValue(tok, out n))
                {
                    if (!TryNum(d.Header, out _))
                    {
                        string key = d.DefName.Length > 0 ? d.DefName : d.Header;
                        if (!dict.ContainsKey(key))
                        {
                            dict[key] = n;
                            Add(d, n);
                        }
                    }
                }
            }
        }
        ItemDefs.AddRange(all.Where(d => d.Kind == "ITEMDEF"));
        Loaded = true;
    }

    private readonly List<(DefInfo d, string tok)> _pending = new();

    private static string StripComment(string s)
    {
        int c = s.IndexOf("//", StringComparison.Ordinal);
        return c >= 0 ? s[..c] : s;
    }

    private void Add(DefInfo d, int n)
    {
        var dict = d.Kind == "CHARDEF" ? Chars : Items;
        if (d.ResolvedId < 0)
        {
            d.ResolvedId = n;
        }
        if (!dict.TryGetValue(n, out var l))
        {
            dict[n] = l = new List<DefInfo>();
        }
        if (!l.Contains(d))
        {
            l.Add(d);
        }
    }

    public string CharNames(int body, int max = 3)
    {
        if (!Chars.TryGetValue(body, out var l))
        {
            return "";
        }
        return string.Join(", ", l.Select(d => d.DefName.Length > 0 ? d.DefName : d.Header).Distinct().Take(max)) + (l.Count > max ? $" (+{l.Count - max})" : "");
    }

    public string ItemNames(int id, int max = 3)
    {
        if (!Items.TryGetValue(id, out var l))
        {
            return "";
        }
        return string.Join(", ", l.Select(d => d.DefName.Length > 0 ? d.DefName : d.Header).Distinct().Take(max)) + (l.Count > max ? $" (+{l.Count - max})" : "");
    }
}
