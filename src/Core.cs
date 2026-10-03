using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Text.Json;

namespace UOTinker;

public sealed class Settings
{
    public string DataFolder { get; set; } = "";
    public string SphereScripts { get; set; } = "";
    public int DisabledProblems { get; set; }
    public bool ReadOnly { get; set; }
    public bool CheckUpdates { get; set; } = true;

    public static bool IsReadOnly;

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UOTinker", "settings.json");

    public static Settings Load()
    {
        var s = new Settings();
        try
        {
            if (File.Exists(FilePath))
            {
                s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
            }
        }
        catch
        {
        }
        IsReadOnly = s.ReadOnly;
        return s;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
        }
    }
}

public static class Gfx
{
    public static uint C16(ushort c)
    {
        int r = (c & 0x7C00) >> 10;
        int g = (c & 0x03E0) >> 5;
        int b = c & 0x001F;
        r = (r << 3) | (r >> 2);
        g = (g << 3) | (g >> 2);
        b = (b << 3) | (b >> 2);
        return 0xFF000000u | (uint)(r << 16) | (uint)(g << 8) | (uint)b;
    }

    public static Color C16Color(ushort c) => Color.FromArgb((int)C16(c));

    public static unsafe Bitmap ToBitmap(uint[] px, int w, int h)
    {
        var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        var bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            fixed (uint* src = px)
            {
                Buffer.MemoryCopy(src, (void*)bd.Scan0, (long)bd.Stride * h, (long)w * h * 4);
            }
        }
        finally
        {
            bmp.UnlockBits(bd);
        }
        return bmp;
    }

    public static string Hex(int v) => "0x" + v.ToString("X");

    public static Bitmap Solid(Color c, int w, int h)
    {
        var px = new uint[w * h];
        Array.Fill(px, (uint)c.ToArgb());
        return ToBitmap(px, w, h);
    }
}

public sealed class MulFile : IDisposable
{
    private readonly FileStream? _fs;
    public string Path { get; }
    public long Length => _fs?.Length ?? 0;
    public bool Exists => _fs != null;

    public MulFile(string path)
    {
        Path = path;
        if (File.Exists(path))
        {
            _fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        }
    }

    public byte[]? Read(long offset, int length)
    {
        if (_fs == null || offset < 0 || length < 0 || offset + length > _fs.Length)
        {
            return null;
        }
        var buf = new byte[length];
        lock (_fs)
        {
            _fs.Seek(offset, SeekOrigin.Begin);
            _fs.ReadExactly(buf);
        }
        return buf;
    }

    public void Dispose() => _fs?.Dispose();
}

public sealed class IdxTable
{
    public int Count { get; }
    public int[] Lookup { get; }
    public int[] Length { get; }
    public uint[] Extra { get; }

    public IdxTable(string path)
    {
        if (!File.Exists(path))
        {
            Lookup = Array.Empty<int>();
            Length = Array.Empty<int>();
            Extra = Array.Empty<uint>();
            return;
        }
        var b = File.ReadAllBytes(path);
        Count = b.Length / 12;
        Lookup = new int[Count];
        Length = new int[Count];
        Extra = new uint[Count];
        for (int i = 0; i < Count; i++)
        {
            Lookup[i] = BitConverter.ToInt32(b, i * 12);
            Length[i] = BitConverter.ToInt32(b, i * 12 + 4);
            Extra[i] = BitConverter.ToUInt32(b, i * 12 + 8);
        }
    }

    public bool Valid(int i) => i >= 0 && i < Count && Lookup[i] >= 0 && Length[i] > 0;
}

public sealed class ArtStore
{
    public const int LandCount = 0x4000;
    public IdxTable ArtIdx { get; private set; }
    public MulFile ArtMul { get; }
    public IdxTable GumpIdx { get; private set; }
    public MulFile GumpMul { get; }
    public bool CanWrite => !Settings.IsReadOnly;
    public readonly Dictionary<int, byte[]?> Pending = new();
    private readonly string _folder;

    public ArtStore(string folder)
    {
        _folder = folder;
        ArtIdx = new IdxTable(System.IO.Path.Combine(folder, "artidx.mul"));
        ArtMul = new MulFile(System.IO.Path.Combine(folder, "art.mul"));
        GumpIdx = new IdxTable(System.IO.Path.Combine(folder, "gumpidx.mul"));
        GumpMul = new MulFile(System.IO.Path.Combine(folder, "gumpart.mul"));
    }

    public int StaticCount => Math.Max(0, ArtIdx.Count - LandCount);

    public bool StaticValid(int id) => Pending.TryGetValue(id, out var d) ? d != null : ArtIdx.Valid(id + LandCount);

    public bool LandValid(int id) => PendingLand.TryGetValue(id, out var d) ? d != null : ArtIdx.Valid(id);

    public static byte[]? EncodeStatic(Bitmap src, out string error)
    {
        error = "";
        int w = src.Width, h = src.Height;
        if (w < 1 || h < 1 || w > 1024 || h > 1024)
        {
            error = $"Groesse {w}x{h} nicht erlaubt (1 bis 1024).";
            return null;
        }
        var px = new uint[w * h];
        using (var tmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
        {
            using (var g = Graphics.FromImage(tmp))
            {
                g.DrawImageUnscaled(src, 0, 0);
            }
            var bd = tmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                for (int y = 0; y < h; y++)
                {
                    var row = new int[w];
                    System.Runtime.InteropServices.Marshal.Copy(bd.Scan0 + y * bd.Stride, row, 0, w);
                    for (int x = 0; x < w; x++)
                    {
                        px[y * w + x] = (uint)row[x];
                    }
                }
            }
            finally
            {
                tmp.UnlockBits(bd);
            }
        }

        var words = new List<ushort>();
        var offsets = new ushort[h];
        bool any = false;
        for (int y = 0; y < h; y++)
        {
            if (words.Count > ushort.MaxValue)
            {
                error = "Bild ist zu komplex fuer das Format.";
                return null;
            }
            offsets[y] = (ushort)words.Count;
            int x = 0;
            int prevEnd = 0;
            while (x < w)
            {
                if ((px[y * w + x] >> 24) < 128)
                {
                    x++;
                    continue;
                }
                int x0 = x;
                while (x < w && (px[y * w + x] >> 24) >= 128)
                {
                    x++;
                }
                words.Add((ushort)(x0 - prevEnd));
                words.Add((ushort)(x - x0));
                for (int k = x0; k < x; k++)
                {
                    uint c = px[y * w + k];
                    int r = (int)((c >> 16) & 0xFF) >> 3;
                    int gg = (int)((c >> 8) & 0xFF) >> 3;
                    int b = (int)(c & 0xFF) >> 3;
                    int v = (r << 10) | (gg << 5) | b;
                    if (v == 0)
                    {
                        v = 0x0421;
                    }
                    words.Add((ushort)(v | 0x8000));
                }
                prevEnd = x;
                any = true;
            }
            words.Add(0);
            words.Add(0);
        }
        if (!any)
        {
            error = "Das Bild ist komplett transparent.";
            return null;
        }
        if (words.Count > ushort.MaxValue)
        {
            error = "Bild ist zu komplex fuer das Format.";
            return null;
        }

        var buf = new byte[8 + h * 2 + words.Count * 2];
        BitConverter.GetBytes(1234).CopyTo(buf, 0);
        BitConverter.GetBytes((short)w).CopyTo(buf, 4);
        BitConverter.GetBytes((short)h).CopyTo(buf, 6);
        for (int y = 0; y < h; y++)
        {
            BitConverter.GetBytes(offsets[y]).CopyTo(buf, 8 + y * 2);
        }
        int p = 8 + h * 2;
        foreach (var wd in words)
        {
            BitConverter.GetBytes(wd).CopyTo(buf, p);
            p += 2;
        }
        return buf;
    }

    public void SetStatic(int id, byte[]? data)
    {
        Pending[id] = data;
    }

    public void RevertStatic(int id) => Pending.Remove(id);

    public string? SaveArt(string backupDir, out string backup)
    {
        backup = "";
        if (!CanWrite)
        {
            return "Der Schreibschutz ist aktiv (Zahnrad unten links: Schreibschutz ausschalten).";
        }
        string idxPath = System.IO.Path.Combine(_folder, "artidx.mul");
        string artPath = System.IO.Path.Combine(_folder, "art.mul");
        long origLen = new FileInfo(artPath).Length;
        FileStream? fs = null;
        try
        {
            Directory.CreateDirectory(backupDir);
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            backup = System.IO.Path.Combine(backupDir, $"artidx-{stamp}.mul");
            File.Copy(idxPath, backup, true);
            File.WriteAllText(System.IO.Path.Combine(backupDir, $"artidx-{stamp}.txt"), $"art.mul hatte vor dem Speichern {origLen} Bytes. Zum Zuruecksetzen artidx.mul aus dieser Sicherung zurueckkopieren; die angehaengten Daten in art.mul bleiben ungenutzt.");

            var idx = File.ReadAllBytes(idxPath);
            var entries = new SortedDictionary<int, byte[]?>();
            foreach (var kv0 in PendingLand)
            {
                entries[kv0.Key] = kv0.Value;
            }
            foreach (var kv0 in Pending)
            {
                entries[kv0.Key + LandCount] = kv0.Value;
            }
            int maxEntry = entries.Keys.Max();
            if ((maxEntry + 1) * 12 > idx.Length)
            {
                int old = idx.Length;
                Array.Resize(ref idx, (maxEntry + 1) * 12);
                for (int o = old; o < idx.Length; o += 12)
                {
                    BitConverter.GetBytes(-1).CopyTo(idx, o);
                    BitConverter.GetBytes(-1).CopyTo(idx, o + 4);
                }
            }

            fs = new FileStream(artPath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
            foreach (var kv in entries)
            {
                int o = kv.Key * 12;
                if (kv.Value == null)
                {
                    BitConverter.GetBytes(-1).CopyTo(idx, o);
                    BitConverter.GetBytes(-1).CopyTo(idx, o + 4);
                    BitConverter.GetBytes(0u).CopyTo(idx, o + 8);
                    continue;
                }
                long pos = fs.Seek(0, SeekOrigin.End);
                if (pos > int.MaxValue - kv.Value.Length)
                {
                    throw new IOException("art.mul waere groesser als 2 GB.");
                }
                fs.Write(kv.Value, 0, kv.Value.Length);
                BitConverter.GetBytes((int)pos).CopyTo(idx, o);
                BitConverter.GetBytes(kv.Value.Length).CopyTo(idx, o + 4);
                BitConverter.GetBytes(0u).CopyTo(idx, o + 8);
            }
            fs.Flush();
            fs.Dispose();
            fs = null;
            File.WriteAllBytes(idxPath, idx);
            ArtIdx = new IdxTable(idxPath);
            Pending.Clear();
            PendingLand.Clear();
            return null;
        }
        catch (Exception ex)
        {
            try
            {
                fs?.SetLength(origLen);
            }
            catch
            {
            }
            fs?.Dispose();
            return ex.Message;
        }
    }

    public (int w, int h) StaticSize(int id)
    {
        if (!StaticValid(id))
        {
            return (0, 0);
        }
        if (Pending.TryGetValue(id, out var pd) && pd != null)
        {
            return (BitConverter.ToInt16(pd, 4), BitConverter.ToInt16(pd, 6));
        }
        var head = ArtMul.Read(ArtIdx.Lookup[id + LandCount], 8);
        if (head == null)
        {
            return (0, 0);
        }
        return (BitConverter.ToInt16(head, 4), BitConverter.ToInt16(head, 6));
    }

    public readonly Dictionary<int, (byte[]? data, int w, int h)> PendingGump = new();
    public readonly Dictionary<int, byte[]?> PendingLand = new();

    public int ArtPendingCount => Pending.Count + PendingLand.Count;

    public bool GumpValid(int id) => PendingGump.TryGetValue(id, out var d) ? d.data != null : GumpIdx.Valid(id);

    public int GumpLength(int id) => PendingGump.TryGetValue(id, out var d) ? (d.data?.Length ?? 0) : (GumpIdx.Valid(id) ? GumpIdx.Length[id] : 0);

    public (int w, int h) GumpSize(int id)
    {
        if (PendingGump.TryGetValue(id, out var d))
        {
            return d.data == null ? (0, 0) : (d.w, d.h);
        }
        if (!GumpIdx.Valid(id))
        {
            return (0, 0);
        }
        uint e = GumpIdx.Extra[id];
        return ((int)((e >> 16) & 0xFFFF), (int)(e & 0xFFFF));
    }

    public static byte[]? EncodeLand(Bitmap src, out string error)
    {
        error = "";
        if (src.Width != 44 || src.Height != 44)
        {
            error = $"Land-Kacheln muessen genau 44 x 44 Pixel gross sein (das Bild hat {src.Width} x {src.Height}).";
            return null;
        }
        var px = ReadPixels(src);
        var buf = new byte[2024];
        int p = 0;
        int xOff = 21;
        int xRun = 2;
        for (int y = 0; y < 22; y++, xOff--, xRun += 2)
        {
            for (int x = 0; x < xRun; x++, p += 2)
            {
                BitConverter.GetBytes(To15(px[y * 44 + xOff + x])).CopyTo(buf, p);
            }
        }
        xOff = 0;
        xRun = 44;
        for (int y = 22; y < 44; y++, xOff++, xRun -= 2)
        {
            for (int x = 0; x < xRun; x++, p += 2)
            {
                BitConverter.GetBytes(To15(px[y * 44 + xOff + x])).CopyTo(buf, p);
            }
        }
        return buf;
    }

    private static ushort To15(uint argb)
    {
        int r = (int)((argb >> 16) & 0xFF) >> 3;
        int g = (int)((argb >> 8) & 0xFF) >> 3;
        int b = (int)(argb & 0xFF) >> 3;
        return (ushort)((r << 10) | (g << 5) | b);
    }

    private static uint[] ReadPixels(Bitmap src)
    {
        int w = src.Width, h = src.Height;
        var px = new uint[w * h];
        using var tmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(tmp))
        {
            g.DrawImageUnscaled(src, 0, 0);
        }
        var bd = tmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var row = new int[w];
            for (int y = 0; y < h; y++)
            {
                System.Runtime.InteropServices.Marshal.Copy(bd.Scan0 + y * bd.Stride, row, 0, w);
                for (int x = 0; x < w; x++)
                {
                    px[y * w + x] = (uint)row[x];
                }
            }
        }
        finally
        {
            tmp.UnlockBits(bd);
        }
        return px;
    }

    public static byte[]? EncodeGump(Bitmap src, out string error)
    {
        error = "";
        int w = src.Width, h = src.Height;
        if (w < 1 || h < 1 || w > 2048 || h > 2048)
        {
            error = $"Groesse {w}x{h} nicht erlaubt (1 bis 2048).";
            return null;
        }
        var px = ReadPixels(src);
        var pairs = new List<(ushort value, ushort run)>();
        var rowStart = new int[h];
        bool any = false;
        for (int y = 0; y < h; y++)
        {
            rowStart[y] = pairs.Count;
            int x = 0;
            while (x < w)
            {
                uint c = px[y * w + x];
                ushort v = (c >> 24) < 128 ? (ushort)0 : To15(c);
                if (v == 0 && (c >> 24) >= 128)
                {
                    v = 0x0421;
                }
                int x0 = x;
                while (x < w)
                {
                    uint c2 = px[y * w + x];
                    ushort v2 = (c2 >> 24) < 128 ? (ushort)0 : To15(c2);
                    if (v2 == 0 && (c2 >> 24) >= 128)
                    {
                        v2 = 0x0421;
                    }
                    if (v2 != v || x - x0 >= 65535)
                    {
                        break;
                    }
                    x++;
                }
                pairs.Add((v, (ushort)(x - x0)));
                if (v != 0)
                {
                    any = true;
                }
            }
        }
        if (!any)
        {
            error = "Das Bild ist komplett transparent.";
            return null;
        }
        var buf = new byte[(h + pairs.Count) * 4];
        for (int y = 0; y < h; y++)
        {
            BitConverter.GetBytes(h + rowStart[y]).CopyTo(buf, y * 4);
        }
        int p = h * 4;
        foreach (var (value, run) in pairs)
        {
            BitConverter.GetBytes(value).CopyTo(buf, p);
            BitConverter.GetBytes(run).CopyTo(buf, p + 2);
            p += 4;
        }
        return buf;
    }

    public void SetLand(int id, byte[]? data) => PendingLand[id] = data;

    public void SetGump(int id, byte[]? data, int w, int h) => PendingGump[id] = (data, w, h);

    public string? SaveGumps(string backupDir, out string backup)
    {
        backup = "";
        if (!CanWrite)
        {
            return "Der Schreibschutz ist aktiv (Zahnrad unten links: Schreibschutz ausschalten).";
        }
        string idxPath = System.IO.Path.Combine(_folder, "gumpidx.mul");
        string mulPath = System.IO.Path.Combine(_folder, "gumpart.mul");
        long origLen = new FileInfo(mulPath).Length;
        FileStream? fs = null;
        try
        {
            Directory.CreateDirectory(backupDir);
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            backup = System.IO.Path.Combine(backupDir, $"gumpidx-{stamp}.mul");
            File.Copy(idxPath, backup, true);
            File.WriteAllText(System.IO.Path.Combine(backupDir, $"gumpidx-{stamp}.txt"), $"gumpart.mul hatte vor dem Speichern {origLen} Bytes. Zum Zuruecksetzen gumpidx.mul aus dieser Sicherung zurueckkopieren; die angehaengten Daten in gumpart.mul bleiben ungenutzt.");

            var idx = File.ReadAllBytes(idxPath);
            int maxEntry = PendingGump.Keys.Max();
            if ((maxEntry + 1) * 12 > idx.Length)
            {
                int old = idx.Length;
                Array.Resize(ref idx, (maxEntry + 1) * 12);
                for (int o = old; o < idx.Length; o += 12)
                {
                    BitConverter.GetBytes(0).CopyTo(idx, o);
                    BitConverter.GetBytes(-1).CopyTo(idx, o + 4);
                    BitConverter.GetBytes(0).CopyTo(idx, o + 8);
                }
            }

            fs = new FileStream(mulPath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
            foreach (var kv in PendingGump.OrderBy(k => k.Key))
            {
                int o = kv.Key * 12;
                var (data, w, h) = kv.Value;
                if (data == null)
                {
                    BitConverter.GetBytes(0).CopyTo(idx, o);
                    BitConverter.GetBytes(-1).CopyTo(idx, o + 4);
                    BitConverter.GetBytes(0).CopyTo(idx, o + 8);
                    continue;
                }
                long pos = fs.Seek(0, SeekOrigin.End);
                if (pos > int.MaxValue - data.Length)
                {
                    throw new IOException("gumpart.mul waere groesser als 2 GB.");
                }
                fs.Write(data, 0, data.Length);
                BitConverter.GetBytes((int)pos).CopyTo(idx, o);
                BitConverter.GetBytes(data.Length).CopyTo(idx, o + 4);
                BitConverter.GetBytes(((uint)w << 16) | (uint)h).CopyTo(idx, o + 8);
            }
            fs.Flush();
            fs.Dispose();
            fs = null;
            File.WriteAllBytes(idxPath, idx);
            GumpIdx = new IdxTable(idxPath);
            PendingGump.Clear();
            return null;
        }
        catch (Exception ex)
        {
            try
            {
                fs?.SetLength(origLen);
            }
            catch
            {
            }
            fs?.Dispose();
            return ex.Message;
        }
    }

    public Bitmap? GetStatic(int id, out string error)
    {
        error = "";
        if (!StaticValid(id))
        {
            error = "Slot ist frei (kein Eintrag).";
            return null;
        }
        int ix = id + LandCount;
        byte[]? buf = Pending.TryGetValue(id, out var pd) ? pd : ArtMul.Read(ArtIdx.Lookup[ix], ArtIdx.Length[ix]);
        if (buf == null || buf.Length < 8)
        {
            error = "Daten ausserhalb von art.mul oder zu kurz.";
            return null;
        }

        short width = BitConverter.ToInt16(buf, 4);
        short height = BitConverter.ToInt16(buf, 6);
        if (width <= 0 || height <= 0 || width > 1024 || height > 1024)
        {
            error = $"Ungueltige Groesse {width}x{height}.";
            return null;
        }

        var pixels = new uint[width * height];
        long dataStart = 8;
        if (dataStart + height * 2 > buf.Length)
        {
            error = "Zeilentabelle ausserhalb der Daten.";
            return null;
        }

        var lineOffsets = new ushort[height];
        for (int i = 0; i < height; i++)
        {
            lineOffsets[i] = (ushort)(buf[dataStart + i * 2] | (buf[dataStart + i * 2 + 1] << 8));
        }

        long rowDataStart = dataStart + height * 2;
        int y = 0;
        int x = 0;
        long readPos = rowDataStart + lineOffsets[0] * 2L;

        while (y < height && readPos + 4 <= buf.Length)
        {
            ushort xoffs = (ushort)(buf[readPos] | (buf[readPos + 1] << 8));
            ushort run = (ushort)(buf[readPos + 2] | (buf[readPos + 3] << 8));
            readPos += 4;

            if (xoffs + run >= 2048)
            {
                break;
            }

            if (xoffs + run != 0)
            {
                x += xoffs;
                int pos = y * width + x;
                for (int j = 0; j < run && readPos + 2 <= buf.Length; j++, pos++)
                {
                    ushort raw = (ushort)(buf[readPos] | (buf[readPos + 1] << 8));
                    readPos += 2;
                    ushort val = (ushort)(raw ^ 0x8000);
                    if (val != 0 && pos >= 0 && pos < pixels.Length)
                    {
                        pixels[pos] = Gfx.C16(val);
                    }
                }
                x += run;
            }
            else
            {
                x = 0;
                y++;
                if (y >= height)
                {
                    break;
                }
                readPos = rowDataStart + lineOffsets[y] * 2L;
            }
        }

        return Gfx.ToBitmap(pixels, width, height);
    }

    public Bitmap? GetLand(int id, out string error)
    {
        error = "";
        if (!LandValid(id))
        {
            error = "Slot ist frei (kein Eintrag).";
            return null;
        }
        byte[]? buf = PendingLand.TryGetValue(id, out var pl) ? pl : ArtMul.Read(ArtIdx.Lookup[id], ArtIdx.Length[id]);
        if (buf == null)
        {
            error = "Daten ausserhalb von art.mul.";
            return null;
        }
        if (buf.Length < 2024)
        {
            error = $"Land-Eintrag hat {buf.Length} Byte (erwartet 2024) - kein klassisches Land-Format.";
            return null;
        }

        var px = new uint[44 * 44];
        int p = 0;
        int xOff = 21;
        int xRun = 2;
        for (int y = 0; y < 22; y++, xOff--, xRun += 2)
        {
            for (int x = 0; x < xRun; x++, p += 2)
            {
                px[y * 44 + xOff + x] = Gfx.C16((ushort)(BitConverter.ToUInt16(buf, p) & 0x7FFF)) | 0xFF000000u;
            }
        }
        xOff = 0;
        xRun = 44;
        for (int y = 22; y < 44; y++, xOff++, xRun -= 2)
        {
            for (int x = 0; x < xRun; x++, p += 2)
            {
                px[y * 44 + xOff + x] = Gfx.C16((ushort)(BitConverter.ToUInt16(buf, p) & 0x7FFF)) | 0xFF000000u;
            }
        }
        return Gfx.ToBitmap(px, 44, 44);
    }

    public Bitmap? GetGump(int id, out string error)
    {
        error = "";
        if (!GumpValid(id))
        {
            error = "Slot ist frei (kein Eintrag).";
            return null;
        }
        var (width, height) = GumpSize(id);
        if (width <= 0 || height <= 0)
        {
            error = $"Ungueltige Groesse {width}x{height}.";
            return null;
        }
        int length = GumpLength(id);
        byte[]? buf = PendingGump.TryGetValue(id, out var pg) ? pg.data : GumpMul.Read(GumpIdx.Lookup[id], length);
        if (buf == null)
        {
            error = "Daten ausserhalb von gumpart.mul.";
            return null;
        }
        if (length < height * 4)
        {
            error = "Daten zu kurz fuer die Zeilentabelle.";
            return null;
        }

        var px = new uint[width * height];
        int halfLen = length / 4;
        var rowLookup = new int[height];
        for (int y = 0; y < height; y++)
        {
            rowLookup[y] = BitConverter.ToInt32(buf, y * 4);
        }

        for (int y = 0; y < height; y++)
        {
            long rowStart = (long)rowLookup[y] * 4;
            if (rowStart < 0 || rowStart > buf.Length)
            {
                continue;
            }
            int gsize = (y < height - 1) ? rowLookup[y + 1] - rowLookup[y] : halfLen - rowLookup[y];
            int pos = (int)rowStart;
            int xx = 0;
            for (int i = 0; i < gsize && pos + 4 <= buf.Length; i++, pos += 4)
            {
                ushort value = BitConverter.ToUInt16(buf, pos);
                ushort run = BitConverter.ToUInt16(buf, pos + 2);
                uint col = value == 0 ? 0u : Gfx.C16(value);
                for (int k = 0; k < run && xx < width; k++, xx++)
                {
                    if (value != 0)
                    {
                        px[y * width + xx] = col;
                    }
                }
            }
        }
        return Gfx.ToBitmap(px, width, height);
    }
}

[Flags]
public enum TileParts
{
    None = 0, Name = 1, Flags = 2, Weight = 4, Height = 8, Layer = 16, Count = 32, Anim = 64, Hue = 128, Light = 256, Tex = 512,
    AllItem = Name | Flags | Weight | Height | Layer | Count | Anim | Hue | Light,
}

public sealed class TileData
{
    public bool Loaded { get; }
    public bool IsOld { get; }
    public int LandCount { get; }
    public int ItemCount { get; }

    public ulong[] LandFlags = Array.Empty<ulong>();
    public ushort[] LandTex = Array.Empty<ushort>();
    public string[] LandName = Array.Empty<string>();

    public ulong[] ItemFlags = Array.Empty<ulong>();
    public byte[] Weight = Array.Empty<byte>();
    public byte[] Layer = Array.Empty<byte>();
    public uint[] Count = Array.Empty<uint>();
    public ushort[] AnimId = Array.Empty<ushort>();
    public ushort[] Hue = Array.Empty<ushort>();
    public ushort[] Light = Array.Empty<ushort>();
    public byte[] Height = Array.Empty<byte>();
    public string[] ItemName = Array.Empty<string>();

    public static readonly (ulong bit, string name)[] FlagNames =
    {
        (0x1, "Background"), (0x2, "Weapon"), (0x4, "Transparent"), (0x8, "Translucent"),
        (0x10, "Wall"), (0x20, "Damaging"), (0x40, "Impassable"), (0x80, "Wet"),
        (0x100, "Unknown1"), (0x200, "Surface"), (0x400, "Bridge"), (0x800, "Generic"),
        (0x1000, "Window"), (0x2000, "NoShoot"), (0x4000, "ArticleA"), (0x8000, "ArticleAn"),
        (0x10000, "Internal"), (0x20000, "Foliage"), (0x40000, "PartialHue"), (0x80000, "NoHouse"),
        (0x100000, "Map"), (0x200000, "Container"), (0x400000, "Wearable"), (0x800000, "LightSource"),
        (0x1000000, "Animation"), (0x2000000, "NoDiagonal"), (0x4000000, "Unknown2"), (0x8000000, "Armor"),
        (0x10000000, "Roof"), (0x20000000, "Door"), (0x40000000, "StairBack"), (0x80000000, "StairRight"),
    };

    public static string FlagText(ulong f)
    {
        var parts = new List<string>();
        foreach (var (bit, name) in FlagNames)
        {
            if ((f & bit) != 0)
            {
                parts.Add(name);
            }
        }
        ulong rest = f & ~0xFFFFFFFFUL;
        if (rest != 0)
        {
            parts.Add("High:0x" + (rest >> 32).ToString("X"));
        }
        return string.Join(", ", parts);
    }

    public TileData(string folder)
    {
        string path = Path.Combine(folder, "tiledata.mul");
        if (!File.Exists(path))
        {
            return;
        }
        var b = File.ReadAllBytes(path);
        _raw = b;
        Path_ = path;

        foreach (bool old in new[] { false, true })
        {
            int fs = old ? 4 : 8;
            int landEntry = fs + 2 + 20;
            int itemEntry = fs + 1 + 1 + 4 + 2 + 2 + 2 + 1 + 20;
            long landSection = 512L * (4 + 32 * landEntry);
            long itemGroup = 4 + 32L * itemEntry;
            long rest = b.Length - landSection;
            if (rest <= 0 || rest % itemGroup != 0)
            {
                continue;
            }

            IsOld = old;
            LandCount = 512 * 32;
            ItemCount = (int)(rest / itemGroup) * 32;
            LandFlags = new ulong[LandCount];
            LandTex = new ushort[LandCount];
            LandName = new string[LandCount];
            LandOff = new int[LandCount];
            int p = 0;
            for (int g = 0; g < 512; g++)
            {
                p += 4;
                for (int e = 0; e < 32; e++)
                {
                    int i = g * 32 + e;
                    LandOff[i] = p;
                    ReadLand(i);
                    p += landEntry;
                }
            }

            ItemFlags = new ulong[ItemCount];
            Weight = new byte[ItemCount];
            Layer = new byte[ItemCount];
            Count = new uint[ItemCount];
            AnimId = new ushort[ItemCount];
            Hue = new ushort[ItemCount];
            Light = new ushort[ItemCount];
            Height = new byte[ItemCount];
            ItemName = new string[ItemCount];
            ItemOff = new int[ItemCount];
            for (int g = 0; g < ItemCount / 32; g++)
            {
                p += 4;
                for (int e = 0; e < 32; e++)
                {
                    int i = g * 32 + e;
                    ItemOff[i] = p;
                    ReadItem(i);
                    p += itemEntry;
                }
            }
            Loaded = true;
            return;
        }
    }

    private byte[] _raw = Array.Empty<byte>();
    private int[] LandOff = Array.Empty<int>();
    private int[] ItemOff = Array.Empty<int>();

    public string Path_ { get; private set; } = "";
    public bool CanWrite => !Settings.IsReadOnly;
    public readonly HashSet<int> Dirty = new();

    private int Fs => IsOld ? 4 : 8;

    private ulong ReadFlags(int p) => IsOld ? BitConverter.ToUInt32(_raw, p) : BitConverter.ToUInt64(_raw, p);

    private void ReadLand(int i)
    {
        int p = LandOff[i];
        LandFlags[i] = ReadFlags(p);
        p += Fs;
        LandTex[i] = BitConverter.ToUInt16(_raw, p);
        LandName[i] = Name(_raw, p + 2);
    }

    private void ReadItem(int i)
    {
        int p = ItemOff[i];
        ItemFlags[i] = ReadFlags(p);
        p += Fs;
        Weight[i] = _raw[p++];
        Layer[i] = _raw[p++];
        Count[i] = BitConverter.ToUInt32(_raw, p);
        p += 4;
        AnimId[i] = BitConverter.ToUInt16(_raw, p);
        p += 2;
        Hue[i] = BitConverter.ToUInt16(_raw, p);
        p += 2;
        Light[i] = BitConverter.ToUInt16(_raw, p);
        p += 2;
        Height[i] = _raw[p++];
        ItemName[i] = Name(_raw, p);
    }

    public void Revert(int key)
    {
        if (key < LandCount)
        {
            ReadLand(key);
        }
        else
        {
            ReadItem(key - LandCount);
        }
        Dirty.Remove(key);
    }

    public void CopyParts(int fromKey, int toKey, TileParts parts)
    {
        bool fromLand = fromKey < LandCount;
        bool toLand = toKey < LandCount;
        if (fromLand != toLand || fromKey == toKey)
        {
            return;
        }
        if (toLand)
        {
            int f = fromKey, t = toKey;
            if (parts.HasFlag(TileParts.Name))
            {
                LandName[t] = LandName[f];
            }
            if (parts.HasFlag(TileParts.Flags))
            {
                LandFlags[t] = LandFlags[f];
            }
            if (parts.HasFlag(TileParts.Tex))
            {
                LandTex[t] = LandTex[f];
            }
        }
        else
        {
            int f = fromKey - LandCount, t = toKey - LandCount;
            if (parts.HasFlag(TileParts.Name))
            {
                ItemName[t] = ItemName[f];
            }
            if (parts.HasFlag(TileParts.Flags))
            {
                ItemFlags[t] = ItemFlags[f];
            }
            if (parts.HasFlag(TileParts.Weight))
            {
                Weight[t] = Weight[f];
            }
            if (parts.HasFlag(TileParts.Height))
            {
                Height[t] = Height[f];
            }
            if (parts.HasFlag(TileParts.Layer))
            {
                Layer[t] = Layer[f];
            }
            if (parts.HasFlag(TileParts.Count))
            {
                Count[t] = Count[f];
            }
            if (parts.HasFlag(TileParts.Anim))
            {
                AnimId[t] = AnimId[f];
            }
            if (parts.HasFlag(TileParts.Hue))
            {
                Hue[t] = Hue[f];
            }
            if (parts.HasFlag(TileParts.Light))
            {
                Light[t] = Light[f];
            }
        }
        Dirty.Add(toKey);
    }

    public void RevertAll()
    {
        foreach (int k in Dirty.ToList())
        {
            Revert(k);
        }
    }

    private void WriteFlags(int p, ulong f)
    {
        if (IsOld)
        {
            BitConverter.GetBytes((uint)f).CopyTo(_raw, p);
        }
        else
        {
            BitConverter.GetBytes(f).CopyTo(_raw, p);
        }
    }

    private void WriteName(int p, string s)
    {
        Array.Clear(_raw, p, 20);
        var bytes = Encoding.Latin1.GetBytes(s.Length > 20 ? s[..20] : s);
        bytes.CopyTo(_raw, p);
    }

    private void Flush(int key)
    {
        if (key < LandCount)
        {
            int p = LandOff[key];
            WriteFlags(p, LandFlags[key]);
            p += Fs;
            BitConverter.GetBytes(LandTex[key]).CopyTo(_raw, p);
            WriteName(p + 2, LandName[key]);
        }
        else
        {
            int i = key - LandCount;
            int p = ItemOff[i];
            WriteFlags(p, ItemFlags[i]);
            p += Fs;
            _raw[p++] = Weight[i];
            _raw[p++] = Layer[i];
            BitConverter.GetBytes(Count[i]).CopyTo(_raw, p);
            p += 4;
            BitConverter.GetBytes(AnimId[i]).CopyTo(_raw, p);
            p += 2;
            BitConverter.GetBytes(Hue[i]).CopyTo(_raw, p);
            p += 2;
            BitConverter.GetBytes(Light[i]).CopyTo(_raw, p);
            p += 2;
            _raw[p++] = Height[i];
            WriteName(p, ItemName[i]);
        }
    }

    public string? Save(string backupDir, out string backupPath)
    {
        backupPath = "";
        if (!CanWrite)
        {
            return "Der Schreibschutz ist aktiv (Zahnrad unten links: Schreibschutz ausschalten).";
        }
        try
        {
            Directory.CreateDirectory(backupDir);
            backupPath = System.IO.Path.Combine(backupDir, $"tiledata-{DateTime.Now:yyyyMMdd-HHmmss}.mul");
            File.Copy(Path_, backupPath, true);
            var copy = (byte[])_raw.Clone();
            var saved = _raw;
            _raw = copy;
            foreach (int k in Dirty)
            {
                Flush(k);
            }
            try
            {
                File.WriteAllBytes(Path_, _raw);
            }
            catch
            {
                _raw = saved;
                throw;
            }
            Dirty.Clear();
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    private static string Name(byte[] b, int p)
    {
        int n = 0;
        while (n < 20 && b[p + n] != 0)
        {
            n++;
        }
        return Encoding.Latin1.GetString(b, p, n);
    }
}

public sealed partial class HueData
{
    private readonly byte[] _raw = Array.Empty<byte>();
    private readonly string _path;

    public int Count { get; }
    public ushort[][] Colors { get; }
    public ushort[] TableStart { get; }
    public ushort[] TableEnd { get; }
    public string[] Names { get; }

    public HueData(string folder)
    {
        string path = Path.Combine(folder, "hues.mul");
        _path = path;
        if (!File.Exists(path))
        {
            Colors = Array.Empty<ushort[]>();
            TableStart = Array.Empty<ushort>();
            TableEnd = Array.Empty<ushort>();
            Names = Array.Empty<string>();
            return;
        }
        var b = File.ReadAllBytes(path);
        _raw = b;
        int groups = b.Length / (4 + 8 * 88);
        Count = groups * 8;
        Colors = new ushort[Count][];
        TableStart = new ushort[Count];
        TableEnd = new ushort[Count];
        Names = new string[Count];
        int p = 0;
        for (int g = 0; g < groups; g++)
        {
            p += 4;
            for (int e = 0; e < 8; e++)
            {
                int i = g * 8 + e;
                var c = new ushort[32];
                for (int k = 0; k < 32; k++, p += 2)
                {
                    c[k] = BitConverter.ToUInt16(b, p);
                }
                Colors[i] = c;
                TableStart[i] = BitConverter.ToUInt16(b, p);
                TableEnd[i] = BitConverter.ToUInt16(b, p + 2);
                p += 4;
                int n = 0;
                while (n < 20 && b[p + n] != 0)
                {
                    n++;
                }
                Names[i] = Encoding.Latin1.GetString(b, p, n);
                p += 20;
            }
        }
    }
}

public sealed partial class SkillData
{
    private readonly IdxTable _idx;
    private readonly string _folder;

    public int Count { get; }
    public bool[] Valid { get; }
    public bool[] Button { get; }
    public string[] Names { get; }

    public SkillData(string folder)
    {
        var idx = new IdxTable(Path.Combine(folder, "skills.idx"));
        _idx = idx;
        _folder = folder;
        string mulPath = Path.Combine(folder, "skills.mul");
        Count = idx.Count;
        Valid = new bool[Count];
        Button = new bool[Count];
        Names = new string[Count];
        if (!File.Exists(mulPath))
        {
            return;
        }
        var mul = File.ReadAllBytes(mulPath);
        for (int i = 0; i < Count; i++)
        {
            Names[i] = "";
            if (!idx.Valid(i) || idx.Lookup[i] + idx.Length[i] > mul.Length)
            {
                continue;
            }
            Valid[i] = true;
            Button[i] = mul[idx.Lookup[i]] != 0;
            int start = idx.Lookup[i] + 1;
            int end = idx.Lookup[i] + idx.Length[i];
            int n = 0;
            while (start + n < end && mul[start + n] != 0)
            {
                n++;
            }
            Names[i] = Encoding.Latin1.GetString(mul, start, n);
        }
    }
}

public sealed partial class ClilocData
{
    private byte[] _header = new byte[6];
    private byte[] _flags = Array.Empty<byte>();

    public string Path { get; }
    public string Error { get; } = "";
    public int[] Numbers { get; private set; } = Array.Empty<int>();
    public string[] Texts { get; private set; } = Array.Empty<string>();
    public bool Compressed { get; }

    public ClilocData(string path)
    {
        Path = path;
        try
        {
            var buf = File.ReadAllBytes(path);
            if (buf.Length > 4 && buf[3] == 0x8E)
            {
                Compressed = true;
                buf = ClassicUO.Utility.BwtDecompress.Decompress(buf);
            }

            var nums = new List<int>();
            var txt = new List<string>();
            var flg = new List<byte>();
            _header = buf.Take(6).ToArray();
            int p = 6;
            while (p + 7 <= buf.Length)
            {
                int num = BitConverter.ToInt32(buf, p);
                int len = BitConverter.ToUInt16(buf, p + 5);
                p += 7;
                if (p + len > buf.Length)
                {
                    break;
                }
                nums.Add(num);
                flg.Add(buf[p - 3]);
                txt.Add(Encoding.UTF8.GetString(buf, p, len));
                p += len;
            }
            Numbers = nums.ToArray();
            Texts = txt.ToArray();
            _flags = flg.ToArray();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }
}
