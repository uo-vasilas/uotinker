using System.Drawing;
using System.Drawing.Imaging;

namespace UOTinker;

public enum GKind
{
    Static,
    Land,
    Gump,
}

public sealed class GraphicEditor : UserControl, IEditorControl
{
    private readonly Context _c;
    private readonly ArtStore _art;
    private readonly GKind _kind;
    private readonly int _id;
    private readonly Action<int> _refresh;

    private readonly Label _head = new() { AutoSize = true, Font = Theme.UiBold, ForeColor = Theme.Gold };
    private readonly Label _info = new() { AutoSize = false, Height = 54, Dock = DockStyle.Top, ForeColor = Theme.Muted };
    private readonly Label _status = new() { AutoSize = false, Height = 70, Dock = DockStyle.Bottom };
    private readonly Button _import = Theme.FlatButton("Aus Bild importieren ...", true);
    private readonly Button _export = Theme.FlatButton("Als PNG exportieren ...");
    private readonly Button _clear = Theme.FlatButton("Slot leeren");
    private readonly Button _revert = Theme.FlatButton("Eintrag zurücksetzen");
    private readonly Button _revertAll = Theme.FlatButton("Alle verwerfen");
    private readonly Button _save = Theme.FlatButton("Speichern", true);

    public event Action? Edited;
    public event Action<int>? GoTo
    {
        add { }
        remove { }
    }

    private static (string text, Color color)? s_msg;

    public GraphicEditor(Context c, GKind kind, int id, Action<int> refresh)
    {
        _c = c;
        _art = c.Art;
        _kind = kind;
        _id = id;
        _refresh = refresh;
        Dock = DockStyle.Fill;
        BackColor = Theme.Bg;

        _head.Text = $"{KindName} {id} ({Gfx.Hex(id)}) bearbeiten";
        var top = new Panel { Dock = DockStyle.Top, Height = 28, Padding = new Padding(4, 6, 4, 0) };
        top.Controls.Add(_head);

        var row1 = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4) };
        _import.Width = 190;
        _export.Width = 170;
        row1.Controls.Add(_import);
        row1.Controls.Add(_export);
        var row2 = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4, 0, 4, 4) };
        _clear.Width = 110;
        _revert.Width = 150;
        _revertAll.Width = 120;
        row2.Controls.Add(_clear);
        row2.Controls.Add(_revert);
        row2.Controls.Add(_revertAll);
        var row3 = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4, 0, 4, 4) };
        _save.Width = 220;
        row3.Controls.Add(_save);

        Controls.Add(_status);
        Controls.Add(row3);
        Controls.Add(row2);
        Controls.Add(row1);
        Controls.Add(_info);
        Controls.Add(top);
        EditorScroll.Attach(this);

        _import.Click += (_, _) => DoImport();
        _export.Click += (_, _) => DoExport();
        _clear.Click += (_, _) => DoClear();
        _revert.Click += (_, _) =>
        {
            RevertOne(_id);
            _refresh(_id);
            Edited?.Invoke();
        };
        _revertAll.Click += (_, _) =>
        {
            int n = PendingCount;
            if (n == 0 || MessageBox.Show($"{n} ungespeicherte Änderungen verwerfen?", "UO Tinker", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }
            foreach (int k in PendingIds())
            {
                RevertOne(k);
                _refresh(k);
            }
            Edited?.Invoke();
        };
        _save.Click += (_, _) => DoSave();

        Theme.Style(this);
        if (s_msg is { } m)
        {
            s_msg = null;
            UpdateState(m.text, m.color);
        }
        else
        {
            UpdateState();
        }
    }

    private string KindName => _kind switch { GKind.Static => "Item-Art", GKind.Land => "Land-Kachel", _ => "Gump" };

    private bool Valid(int id) => _kind switch { GKind.Static => _art.StaticValid(id), GKind.Land => _art.LandValid(id), _ => _art.GumpValid(id) };

    private (int w, int h) SizeOfId(int id) => _kind switch { GKind.Static => _art.StaticSize(id), GKind.Land => (44, 44), _ => _art.GumpSize(id) };

    private bool IsPending(int id) => _kind switch { GKind.Static => _art.Pending.ContainsKey(id), GKind.Land => _art.PendingLand.ContainsKey(id), _ => _art.PendingGump.ContainsKey(id) };

    private int PendingCount => _kind == GKind.Gump ? _art.PendingGump.Count : _art.ArtPendingCount;

    private List<int> PendingIds() => _kind switch
    {
        GKind.Gump => _art.PendingGump.Keys.ToList(),
        _ => _art.PendingLand.Keys.Concat(_art.Pending.Keys).ToList(),
    };

    private void RevertOne(int id)
    {
        switch (_kind)
        {
            case GKind.Static:
                _art.Pending.Remove(id);
                break;
            case GKind.Land:
                _art.PendingLand.Remove(id);
                break;
            default:
                _art.PendingGump.Remove(id);
                break;
        }
    }

    private Bitmap? GetBitmap(out string err) => _kind switch
    {
        GKind.Static => _art.GetStatic(_id, out err),
        GKind.Land => _art.GetLand(_id, out err),
        _ => _art.GetGump(_id, out err),
    };

    private void UpdateState(string? message = null, Color? color = null)
    {
        bool can = _art.CanWrite;
        bool has = Valid(_id);
        bool pend = IsPending(_id);
        var (w, h) = SizeOfId(_id);
        _info.Text = has
            ? $"Grafik vorhanden: {w} x {h} Pixel{(pend ? "  (geändert, noch nicht gespeichert)" : "")}"
            : (pend ? "Slot wird beim Speichern geleert." : "Slot ist frei (keine Grafik).");
        if (_kind == GKind.Land)
        {
            _info.Text += "\nLand-Kacheln sind immer 44 x 44 Pixel; nur die Raute in der Mitte wird gespeichert.";
        }
        _import.Enabled = can;
        _export.Enabled = has;
        _clear.Enabled = can && has;
        _revert.Enabled = can && pend;
        _revertAll.Enabled = can && PendingCount > 0;
        _save.Enabled = can && PendingCount > 0;
        _save.Text = _kind == GKind.Gump ? $"Gumps speichern ({PendingCount})" : $"Art speichern ({PendingCount})";
        if (message != null)
        {
            _status.ForeColor = color ?? Theme.Muted;
            _status.Text = message;
        }
        else if (!can)
        {
            _status.ForeColor = Theme.Red;
            _status.Text = "Schreibschutz aktiv: Zahnrad unten links, 'Schreibschutz' ausschalten.";
        }
        else
        {
            _status.ForeColor = Theme.Muted;
            var t = _c.Tile;
            _status.Text = _kind == GKind.Static && _id < t.ItemCount && t.ItemName[_id].Length == 0 && t.ItemFlags[_id] == 0
                ? "Die Tiledata dieses Items ist noch leer. Im Tab Tiledata ergänzen."
                : "";
        }
    }

    private void DoImport()
    {
        using var dlg = new OpenFileDialog { Title = $"Bild für {KindName} wählen", Filter = "Bilder (*.png;*.bmp;*.gif;*.jpg)|*.png;*.bmp;*.gif;*.jpg|Alle Dateien|*.*" };
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK)
        {
            return;
        }
        Bitmap src;
        try
        {
            using var ms = new MemoryStream(File.ReadAllBytes(dlg.FileName));
            using var img = Image.FromStream(ms);
            src = new Bitmap(img);
        }
        catch (Exception ex)
        {
            UpdateState("Bild konnte nicht gelesen werden: " + ex.Message, Theme.Red);
            return;
        }
        using (src)
        {
            byte[]? data;
            string err;
            switch (_kind)
            {
                case GKind.Static:
                    data = ArtStore.EncodeStatic(src, out err);
                    break;
                case GKind.Land:
                    data = ArtStore.EncodeLand(src, out err);
                    break;
                default:
                    data = ArtStore.EncodeGump(src, out err);
                    break;
            }
            if (data == null)
            {
                UpdateState("Nicht importiert: " + err, Theme.Red);
                return;
            }
            if (Valid(_id) && MessageBox.Show(FindForm(), $"Slot {_id} ({Gfx.Hex(_id)}) enthält bereits eine Grafik. Ersetzen?", "UO Tinker", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }
            SetPending(data, src.Width, src.Height);
            _refresh(_id);
            Edited?.Invoke();
        }
    }

    private void SetPending(byte[]? data, int w, int h)
    {
        switch (_kind)
        {
            case GKind.Static:
                _art.SetStatic(_id, data);
                break;
            case GKind.Land:
                _art.SetLand(_id, data);
                break;
            default:
                _art.SetGump(_id, data, w, h);
                break;
        }
    }

    private void DoExport()
    {
        using var bmp = GetBitmap(out string err);
        if (bmp == null)
        {
            UpdateState("Export nicht möglich: " + err, Theme.Red);
            return;
        }
        string stem = _kind switch { GKind.Static => "item", GKind.Land => "land", _ => "gump" };
        using var dlg = new SaveFileDialog { Title = "Als PNG speichern", Filter = "PNG (*.png)|*.png", FileName = $"{stem}_{_id:X4}.png" };
        if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
        {
            bmp.Save(dlg.FileName, ImageFormat.Png);
            UpdateState("Exportiert nach " + dlg.FileName, Theme.Green);
        }
    }

    private void DoClear()
    {
        if (MessageBox.Show(FindForm(), $"Grafik in Slot {_id} ({Gfx.Hex(_id)}) beim Speichern entfernen?", "UO Tinker", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }
        SetPending(null, 0, 0);
        _refresh(_id);
        Edited?.Invoke();
    }

    private void DoSave()
    {
        string backups = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UOTinker", "backups");
        int n = PendingCount;
        var ids = PendingIds();
        string? err = _kind == GKind.Gump ? _art.SaveGumps(backups, out string backup) : _art.SaveArt(backups, out backup);
        if (err != null)
        {
            UpdateState("Speichern fehlgeschlagen: " + err, Theme.Red);
            return;
        }
        foreach (int k in ids)
        {
            _refresh(k);
        }
        string files = _kind == GKind.Gump ? "gumpart.mul angehängt und gumpidx.mul" : "art.mul angehängt und artidx.mul";
        s_msg = ($"{n} Grafik(en) in {files} aktualisiert. Sicherung des Index: {backup}", Theme.Green);
        Edited?.Invoke();
    }
}
