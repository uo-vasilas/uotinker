using System.Text;

namespace UOTinker;

public static class StoreIo
{
    public static string BackupDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UOTinker", "backups");

    public static string ReadOnlyMessage => Loc.T("Der Schreibschutz ist aktiv (Zahnrad unten links: Schreibschutz ausschalten).");

    public static string Backup(string path, string prefix)
    {
        Directory.CreateDirectory(BackupDir);
        string target = Path.Combine(BackupDir, $"{prefix}-{DateTime.Now:yyyyMMdd-HHmmss}{Path.GetExtension(path)}");
        File.Copy(path, target, true);
        return target;
    }

    public static void WriteAtomic(string path, byte[] data)
    {
        string tmp = path + ".uotinker-tmp";
        File.WriteAllBytes(tmp, data);
        File.Move(tmp, path, true);
    }
}

public sealed class RadarData
{
    private readonly string _path;
    private readonly Dictionary<int, ushort> _orig = new();

    public ushort[] Col { get; private set; }
    public HashSet<int> Dirty { get; } = new();
    public bool Exists { get; }

    public RadarData(string folder)
    {
        _path = Path.Combine(folder, "radarcol.mul");
        Exists = File.Exists(_path);
        if (!Exists)
        {
            Col = Array.Empty<ushort>();
            return;
        }
        var b = File.ReadAllBytes(_path);
        Col = new ushort[b.Length / 2];
        for (int i = 0; i < Col.Length; i++)
        {
            Col[i] = BitConverter.ToUInt16(b, i * 2);
        }
    }

    public bool CanWrite => !Settings.IsReadOnly && Exists;

    public void Set(int i, ushort value)
    {
        if (!_orig.ContainsKey(i))
        {
            _orig[i] = Col[i];
        }
        Col[i] = value;
        if (_orig[i] == value)
        {
            _orig.Remove(i);
            Dirty.Remove(i);
        }
        else
        {
            Dirty.Add(i);
        }
    }

    public void Revert(int i)
    {
        if (_orig.TryGetValue(i, out var v))
        {
            Col[i] = v;
            _orig.Remove(i);
        }
        Dirty.Remove(i);
    }

    public void RevertAll()
    {
        foreach (int i in Dirty.ToArray())
        {
            Revert(i);
        }
    }

    public string? Save(out string backup)
    {
        backup = "";
        if (!CanWrite)
        {
            return Settings.IsReadOnly ? StoreIo.ReadOnlyMessage : Loc.T("radarcol.mul fehlt.");
        }
        try
        {
            backup = StoreIo.Backup(_path, "radarcol");
            var b = new byte[Col.Length * 2];
            for (int i = 0; i < Col.Length; i++)
            {
                BitConverter.GetBytes(Col[i]).CopyTo(b, i * 2);
            }
            StoreIo.WriteAtomic(_path, b);
            Dirty.Clear();
            _orig.Clear();
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}

public sealed partial class HueData
{
    private readonly Dictionary<int, (ushort[] colors, ushort start, ushort end, string name)> _orig = new();

    public HashSet<int> Dirty { get; } = new();

    public bool CanWrite => !Settings.IsReadOnly && Count > 0;

    public void Touch(int i)
    {
        if (!_orig.ContainsKey(i))
        {
            _orig[i] = (Colors[i].ToArray(), TableStart[i], TableEnd[i], Names[i]);
        }
        Dirty.Add(i);
    }

    public void Revert(int i)
    {
        if (_orig.TryGetValue(i, out var o))
        {
            o.colors.CopyTo(Colors[i], 0);
            TableStart[i] = o.start;
            TableEnd[i] = o.end;
            Names[i] = o.name;
            _orig.Remove(i);
        }
        Dirty.Remove(i);
    }

    public void RevertAll()
    {
        foreach (int i in Dirty.ToArray())
        {
            Revert(i);
        }
    }

    public bool IsFree(int i) => Names[i].Length == 0 && Colors[i].All(x => x == 0);

    public string? Save(out string backup)
    {
        backup = "";
        if (!CanWrite)
        {
            return Settings.IsReadOnly ? StoreIo.ReadOnlyMessage : Loc.T("hues.mul fehlt.");
        }
        try
        {
            backup = StoreIo.Backup(_path, "hues");
            var b = (byte[])_raw.Clone();
            foreach (int i in Dirty)
            {
                int p = (i / 8) * (4 + 8 * 88) + 4 + (i % 8) * 88;
                for (int k = 0; k < 32; k++)
                {
                    BitConverter.GetBytes(Colors[i][k]).CopyTo(b, p + k * 2);
                }
                BitConverter.GetBytes(TableStart[i]).CopyTo(b, p + 64);
                BitConverter.GetBytes(TableEnd[i]).CopyTo(b, p + 66);
                Array.Clear(b, p + 68, 20);
                var nb = Encoding.Latin1.GetBytes(Names[i]);
                Array.Copy(nb, 0, b, p + 68, Math.Min(19, nb.Length));
            }
            StoreIo.WriteAtomic(_path, b);
            Buffer.BlockCopy(b, 0, _raw, 0, b.Length);
            Dirty.Clear();
            _orig.Clear();
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}

public sealed partial class SkillData
{
    private readonly Dictionary<int, (bool valid, bool button, string name)> _orig = new();

    public HashSet<int> Dirty { get; } = new();

    public bool CanWrite => !Settings.IsReadOnly && Count > 0;

    public void Touch(int i)
    {
        if (!_orig.ContainsKey(i))
        {
            _orig[i] = (Valid[i], Button[i], Names[i]);
        }
        Dirty.Add(i);
    }

    public void Revert(int i)
    {
        if (_orig.TryGetValue(i, out var o))
        {
            Valid[i] = o.valid;
            Button[i] = o.button;
            Names[i] = o.name;
            _orig.Remove(i);
        }
        Dirty.Remove(i);
    }

    public void RevertAll()
    {
        foreach (int i in Dirty.ToArray())
        {
            Revert(i);
        }
    }

    public string? Save(out string backup)
    {
        backup = "";
        if (!CanWrite)
        {
            return Settings.IsReadOnly ? StoreIo.ReadOnlyMessage : Loc.T("skills.idx fehlt.");
        }
        string idxPath = Path.Combine(_folder, "skills.idx");
        string mulPath = Path.Combine(_folder, "skills.mul");
        try
        {
            string b1 = File.Exists(mulPath) ? StoreIo.Backup(mulPath, "skills") : "";
            string b2 = StoreIo.Backup(idxPath, "skills");
            backup = b1.Length > 0 ? Loc.F("{0} und {1}", b1, b2) : b2;
            var mul = new MemoryStream();
            var idx = new byte[Count * 12];
            for (int i = 0; i < Count; i++)
            {
                if (Valid[i])
                {
                    var nb = Encoding.Latin1.GetBytes(Names[i]);
                    int start = (int)mul.Position;
                    mul.WriteByte((byte)(Button[i] ? 1 : 0));
                    mul.Write(nb, 0, nb.Length);
                    mul.WriteByte(0);
                    BitConverter.GetBytes(start).CopyTo(idx, i * 12);
                    BitConverter.GetBytes((int)mul.Position - start).CopyTo(idx, i * 12 + 4);
                    BitConverter.GetBytes(_idx.Extra[i]).CopyTo(idx, i * 12 + 8);
                }
                else
                {
                    BitConverter.GetBytes(-1).CopyTo(idx, i * 12);
                    BitConverter.GetBytes(-1).CopyTo(idx, i * 12 + 4);
                    BitConverter.GetBytes(_idx.Extra[i]).CopyTo(idx, i * 12 + 8);
                }
            }
            StoreIo.WriteAtomic(mulPath, mul.ToArray());
            StoreIo.WriteAtomic(idxPath, idx);
            Dirty.Clear();
            _orig.Clear();
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}

public sealed partial class ClilocData
{
    private readonly Dictionary<int, string?> _orig = new();

    public HashSet<int> Dirty { get; } = new();

    public bool CanWrite => !Settings.IsReadOnly && Error.Length == 0 && !Compressed;

    public string WriteBlocker => Settings.IsReadOnly ? StoreIo.ReadOnlyMessage
        : Compressed ? Loc.T("Diese Datei ist BWT-komprimiert. Komprimierte Cliloc-Dateien kann UOTinker nicht schreiben.")
        : Error.Length > 0 ? Loc.T("Die Datei konnte nicht gelesen werden.") : "";

    public int IndexOf(int number) => Array.IndexOf(Numbers, number);

    public void SetText(int number, string text)
    {
        int i = IndexOf(number);
        if (i < 0)
        {
            Numbers = Numbers.Append(number).ToArray();
            Texts = Texts.Append(text).ToArray();
            _flags = _flags.Append((byte)1).ToArray();
            _orig[number] = null;
            Dirty.Add(number);
            return;
        }
        if (!_orig.ContainsKey(number))
        {
            _orig[number] = Texts[i];
        }
        Texts[i] = text;
        if (_orig[number] == text)
        {
            _orig.Remove(number);
            Dirty.Remove(number);
        }
        else
        {
            Dirty.Add(number);
            if (_flags[i] == 0)
            {
                _flags[i] = 2;
            }
        }
    }

    public void Revert(int number)
    {
        if (_orig.TryGetValue(number, out var o))
        {
            int i = IndexOf(number);
            if (o == null)
            {
                var n = Numbers.ToList();
                var t = Texts.ToList();
                var f = _flags.ToList();
                n.RemoveAt(i);
                t.RemoveAt(i);
                f.RemoveAt(i);
                Numbers = n.ToArray();
                Texts = t.ToArray();
                _flags = f.ToArray();
            }
            else if (i >= 0)
            {
                Texts[i] = o;
            }
            _orig.Remove(number);
        }
        Dirty.Remove(number);
    }

    public void RevertAll()
    {
        foreach (int n in Dirty.ToArray())
        {
            Revert(n);
        }
    }

    public string? Save(out string backup)
    {
        backup = "";
        if (!CanWrite)
        {
            return WriteBlocker;
        }
        try
        {
            backup = StoreIo.Backup(Path, System.IO.Path.GetFileNameWithoutExtension(Path));
            var ms = new MemoryStream();
            ms.Write(_header, 0, 6);
            for (int i = 0; i < Numbers.Length; i++)
            {
                var tb = Encoding.UTF8.GetBytes(Texts[i]);
                ms.Write(BitConverter.GetBytes(Numbers[i]), 0, 4);
                ms.WriteByte(_flags[i]);
                ms.Write(BitConverter.GetBytes((ushort)tb.Length), 0, 2);
                ms.Write(tb, 0, tb.Length);
            }
            StoreIo.WriteAtomic(Path, ms.ToArray());
            Dirty.Clear();
            _orig.Clear();
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
