using System.Drawing;

namespace UOTinker;

public interface IEditorControl
{
    event Action? Edited;
    event Action<int>? GoTo;
}

public sealed class TileEditor : UserControl, IEditorControl
{
    private readonly TiledataProvider _prov;
    private readonly TileData _t;
    private readonly int _key;
    private readonly bool _land;
    private readonly int _id;
    private bool _loading = true;

    private readonly TextBox _name = new() { MaxLength = 20, Width = 220 };
    private readonly Dictionary<string, NumericUpDown> _num = new();
    private readonly List<(CheckBox box, ulong bit)> _flags = new();
    private readonly Label _status = new() { AutoSize = false, Height = 38, Dock = DockStyle.Bottom };
    private readonly Button _save = Theme.FlatButton("Speichern", true);
    private readonly Button _revert = Theme.FlatButton("Eintrag zurücksetzen");
    private readonly Button _revertAll = Theme.FlatButton("Alle verwerfen");
    private readonly Button _copy = Theme.FlatButton("Werte kopieren ...");
    private readonly Button _new = Theme.FlatButton("Neues Item anlegen ...");
    private readonly Button _rules = Theme.FlatButton("Problemregeln ...");
    private readonly Button _def =Theme.FlatButton("ITEMDEF ...");
    private readonly Label _head = new() { AutoSize = true, Font = Theme.UiBold, ForeColor = Theme.Gold };

    public event Action? Edited;
    public event Action<int>? GoTo;

    public TileEditor(TiledataProvider prov, int key)
    {
        _prov = prov;
        _t = prov.Ctx.Tile;
        _key = key;
        _land = prov.KeyIsLand(key);
        _id = prov.KeyId(key);
        Dock = DockStyle.Fill;
        BackColor = Theme.Bg;

        var table = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4, Padding = new Padding(4, 4, 4, 0) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _head.Text = _land ? $"Land-Tile {_id} ({Gfx.Hex(_id)}) bearbeiten" : $"Item {_id} ({Gfx.Hex(_id)}) bearbeiten";
        table.Controls.Add(_head, 0, 0);
        table.SetColumnSpan(_head, 4);

        table.Controls.Add(Lbl("Name"), 0, 1);
        table.Controls.Add(_name, 1, 1);
        table.SetColumnSpan(_name, 3);

        var fields = _land
            ? new (string, long)[] { ("TexID", 65535) }
            : new (string, long)[] { ("Gewicht", 255), ("Hoehe", 255), ("Layer", 255), ("Menge", uint.MaxValue), ("AnimID", 65535), ("Hue", 65535), ("Licht", 65535) };
        int row = 2;
        int col = 0;
        foreach (var (label, max) in fields)
        {
            var n = new NumericUpDown { Minimum = 0, Maximum = max, Width = 80, BackColor = Theme.Input, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle };
            _num[label] = n;
            table.Controls.Add(Lbl(label), col, row);
            table.Controls.Add(n, col + 1, row);
            n.ValueChanged += (_, _) => Changed();
            col += 2;
            if (col >= 4)
            {
                col = 0;
                row++;
            }
        }

        var flagPanel = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Padding = new Padding(4, 8, 4, 4), Width = 440 };
        var flagHead = new Label { Text = "Flags", Font = Theme.UiBold, ForeColor = Theme.Muted, AutoSize = false, Width = 420, Height = 20 };
        flagPanel.Controls.Add(flagHead);
        flagPanel.SetFlowBreak(flagHead, true);
        foreach (var (bit, name) in TileData.FlagNames)
        {
            var cb = new CheckBox { Text = name, Width = 135, ForeColor = Theme.Text, AutoSize = false, Height = 20 };
            cb.CheckedChanged += (_, _) => Changed();
            _flags.Add((cb, bit));
            flagPanel.Controls.Add(cb);
        }

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4) };
        _save.Width = 150;
        _revert.Width = 150;
        _revertAll.Width = 120;
        buttons.Controls.Add(_save);
        buttons.Controls.Add(_revert);
        buttons.Controls.Add(_revertAll);

        var tools = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4, 0, 4, 4) };
        _copy.Width = 150;
        _new.Width = 170;
        tools.Controls.Add(_copy);
        if (!_land)
        {
            tools.Controls.Add(_new);
        }
        _rules.Width = 160;
        tools.Controls.Add(_rules);
        _rules.Click += (_, _) => DoRules();
        _def.Width = 150;
        if (!_land)
        {
            tools.Controls.Add(_def);
        }
        _copy.Click += (_, _) => DoCopy();
        _new.Click += (_, _) => DoNew();
        _def.Click += (_, _) => ShowDefMenu();

        Controls.Add(_status);
        Controls.Add(tools);
        Controls.Add(buttons);
        Controls.Add(flagPanel);
        Controls.Add(table);
        EditorScroll.Attach(this);

        _name.TextChanged += (_, _) => Changed();
        _save.Click += (_, _) => DoSave();
        _revert.Click += (_, _) =>
        {
            _t.Revert(_key);
            _prov.Recompute(_key);
            LoadValues();
            Edited?.Invoke();
        };
        _revertAll.Click += (_, _) =>
        {
            if (_t.Dirty.Count == 0 || MessageBox.Show($"{_t.Dirty.Count} ungespeicherte Änderungen verwerfen?", "UO Tinker", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }
            var keys = _t.Dirty.ToList();
            _t.RevertAll();
            foreach (int k in keys)
            {
                _prov.Recompute(k);
            }
            LoadValues();
            Edited?.Invoke();
        };

        Theme.Style(this);
        foreach (var (cb, _) in _flags)
        {
            cb.ForeColor = Theme.Text;
        }
        LoadValues();
        _loading = false;
        UpdateState();
    }

    private static Label Lbl(string t) => new() { Text = t, AutoSize = true, Margin = new Padding(6, 6, 2, 0), ForeColor = Theme.Muted };

    private void LoadValues()
    {
        _loading = true;
        if (_land)
        {
            _name.Text = _t.LandName[_id];
            _num["TexID"].Value = _t.LandTex[_id];
        }
        else
        {
            _name.Text = _t.ItemName[_id];
            _num["Gewicht"].Value = _t.Weight[_id];
            _num["Hoehe"].Value = _t.Height[_id];
            _num["Layer"].Value = _t.Layer[_id];
            _num["Menge"].Value = _t.Count[_id];
            _num["AnimID"].Value = _t.AnimId[_id];
            _num["Hue"].Value = _t.Hue[_id];
            _num["Licht"].Value = _t.Light[_id];
        }
        ulong f = _land ? _t.LandFlags[_id] : _t.ItemFlags[_id];
        foreach (var (cb, bit) in _flags)
        {
            cb.Checked = (f & bit) != 0;
        }
        _loading = false;
        UpdateState();
    }

    private void Changed()
    {
        if (_loading)
        {
            return;
        }
        ulong f = _land ? _t.LandFlags[_id] : _t.ItemFlags[_id];
        foreach (var (cb, bit) in _flags)
        {
            f = cb.Checked ? (f | bit) : (f & ~bit);
        }
        if (_land)
        {
            _t.LandName[_id] = _name.Text;
            _t.LandTex[_id] = (ushort)_num["TexID"].Value;
            _t.LandFlags[_id] = f;
        }
        else
        {
            _t.ItemName[_id] = _name.Text;
            _t.Weight[_id] = (byte)_num["Gewicht"].Value;
            _t.Height[_id] = (byte)_num["Hoehe"].Value;
            _t.Layer[_id] = (byte)_num["Layer"].Value;
            _t.Count[_id] = (uint)_num["Menge"].Value;
            _t.AnimId[_id] = (ushort)_num["AnimID"].Value;
            _t.Hue[_id] = (ushort)_num["Hue"].Value;
            _t.Light[_id] = (ushort)_num["Licht"].Value;
            _t.ItemFlags[_id] = f;
        }
        _t.Dirty.Add(_key);
        _status.Tag = null;
        _prov.Recompute(_key);
        UpdateState();
        Edited?.Invoke();
    }

    private void UpdateState()
    {
        bool can = _t.CanWrite;
        foreach (Control c in Controls)
        {
            if (c is TableLayoutPanel or FlowLayoutPanel)
            {
                SetEnabled(c, can);
            }
        }
        _copy.Enabled = can;
        _new.Enabled = can;
        _save.Enabled = can && _t.Dirty.Count > 0;
        _save.Text = $"Speichern ({_t.Dirty.Count})";
        _revert.Enabled = can && _t.Dirty.Contains(_key);
        _revertAll.Enabled = can && _t.Dirty.Count > 0;
        if (!can)
        {
            _status.ForeColor = Theme.Red;
            _status.Text = "Schreibschutz aktiv: Zahnrad unten links, 'Schreibschutz' ausschalten.";
        }
        else if (_status.Tag as string != "saved")
        {
            _status.ForeColor = Theme.Muted;
            _status.Text = _t.Dirty.Count > 0 ? $"{_t.Dirty.Count} Änderung(en) noch nicht gespeichert." : "Keine ungespeicherten Änderungen.";
        }
    }

    private static void SetEnabled(Control c, bool on)
    {
        foreach (Control ch in c.Controls)
        {
            if (ch is not Button)
            {
                ch.Enabled = on;
            }
        }
    }

    private static bool ParseId(string s, out int id)
    {
        s = s.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return int.TryParse(s[2..], System.Globalization.NumberStyles.HexNumber, null, out id);
        }
        return int.TryParse(s, out id);
    }

    private int KeyFor(int id) => _land ? id : _t.LandCount + id;

    private int Max => _land ? _t.LandCount : _t.ItemCount;

    private string Describe(int id)
    {
        if (id < 0 || id >= Max)
        {
            return "ID außerhalb des Bereichs";
        }
        var c = _prov.Ctx;
        string name = _land ? _t.LandName[id] : _t.ItemName[id];
        bool art = _land ? c.Art.LandValid(id) : c.Art.StaticValid(id);
        bool empty = _prov.IsFree(KeyFor(id));
        return $"{(name.Length > 0 ? name : "(ohne Name)")}  |  Tiledata {(empty ? "leer" : "belegt")}  |  Art {(art ? "vorhanden" : "fehlt")}";
    }

    private static Form NewDialog(string title, int w, int h)
    {
        var f = new Form
        {
            Text = title, Width = w, Height = h, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false, BackColor = Theme.Bg, ForeColor = Theme.Text, Font = Theme.Ui,
        };
        f.HandleCreated += (_, _) => Theme.DarkTitleBar(f);
        return f;
    }

    private void DoRules()
    {
        var s = _prov.Ctx.Settings;
        using var f = NewDialog("Problemregeln", 420, 140 + TiledataProvider.Rules.Count * 26);
        f.Controls.Add(new Label { Text = "Welche Prüfungen sollen als Problem gemeldet werden?", Left = 16, Top = 14, Width = 380, Height = 36, ForeColor = Theme.Muted });
        var boxes = new List<(CheckBox cb, int bit)>();
        int y = 52;
        foreach (var (bit, name) in TiledataProvider.Rules)
        {
            var cb = new CheckBox { Text = name, Left = 16, Top = y, Width = 370, Checked = (s.DisabledProblems & bit) == 0, ForeColor = Theme.Text };
            f.Controls.Add(cb);
            boxes.Add((cb, bit));
            y += 26;
        }
        var ok = Theme.FlatButton("Übernehmen", true);
        ok.SetBounds(16, y + 12, 140, 34);
        ok.DialogResult = DialogResult.OK;
        var cancel = Theme.FlatButton("Abbrechen");
        cancel.SetBounds(166, y + 12, 120, 34);
        cancel.DialogResult = DialogResult.Cancel;
        f.Controls.Add(ok);
        f.Controls.Add(cancel);
        f.AcceptButton = ok;
        f.CancelButton = cancel;
        if (f.ShowDialog(FindForm()) != DialogResult.OK)
        {
            return;
        }
        int mask = 0;
        foreach (var (cb, bit) in boxes)
        {
            if (!cb.Checked)
            {
                mask |= bit;
            }
        }
        s.DisabledProblems = mask;
        _prov.ApplyRules();
        LoadValues();
        Edited?.Invoke();
        _status.Text = mask == 0 ? "Alle Prüfungen aktiv." : $"{boxes.Count(b => b.cb.Checked)} von {boxes.Count} Prüfungen aktiv.";
    }

    private void ShowDefMenu()
    {
        var menu = new ContextMenuStrip();
        if (_prov.Ctx.Catalog.Items.TryGetValue(_id, out var defs))
        {
            foreach (var d in defs)
            {
                string label = d.DefName.Length > 0 ? d.DefName : d.Header;
                var def = d;
                menu.Items.Add($"{label}  ({def.File}:{def.Line}) öffnen", null, (_, _) => OpenDef(def));
                menu.Items.Add("   → in der ITEMDEF-Liste zeigen", null, (_, _) => AppNav.Request?.Invoke("itemdef", _prov.Ctx.Catalog.ItemDefs.IndexOf(def)));
                if (def.HeaderId >= 0 && def.HeaderId != _id && def.HeaderId < _t.ItemCount)
                {
                    int target = def.HeaderId;
                    menu.Items.Add($"   → zu Item {target} ({Gfx.Hex(target)}) springen", null, (_, _) => GoTo?.Invoke(_t.LandCount + target));
                }
            }
        }
        else
        {
            menu.Items.Add("Kein ITEMDEF für dieses Item gefunden").Enabled = false;
        }
        menu.Show(_def, new Point(0, _def.Height));
    }

    private void OpenDef(DefInfo d)
    {
        string? err = DefTools.Open(d);
        if (err != null)
        {
            _status.Text = err;
        }
    }

    private void DoCopy()
    {
        using var f = NewDialog("Werte kopieren", 470, _land ? 300 : 440);
        var src = new TextBox { Left = 150, Top = 16, Width = 120 };
        var info = new Label { Left = 16, Top = 46, Width = 430, Height = 36, ForeColor = Theme.Muted };
        f.Controls.Add(new Label { Text = _land ? "Land-Tile (ID/0x)" : "Quell-Item (ID/0x)", Left = 16, Top = 20, AutoSize = true });
        f.Controls.Add(src);
        f.Controls.Add(info);
        var parts = _land
            ? new (string, TileParts, bool)[] { ("Name", TileParts.Name, false), ("Flags", TileParts.Flags, true), ("TexID", TileParts.Tex, true) }
            : new (string, TileParts, bool)[]
            {
                ("Name", TileParts.Name, false), ("Flags", TileParts.Flags, true), ("Gewicht", TileParts.Weight, true), ("Höhe", TileParts.Height, true),
                ("Layer", TileParts.Layer, true), ("Menge", TileParts.Count, true), ("AnimID", TileParts.Anim, false), ("Hue", TileParts.Hue, false), ("Licht", TileParts.Light, true),
            };
        var boxes = new List<(CheckBox cb, TileParts p)>();
        int y = 90;
        foreach (var (label, part, on) in parts)
        {
            var cb = new CheckBox { Text = label, Left = 16, Top = y, Width = 200, Checked = on, ForeColor = Theme.Text };
            f.Controls.Add(cb);
            boxes.Add((cb, part));
            y += 26;
        }
        var ok = Theme.FlatButton("Kopieren", true);
        ok.SetBounds(16, y + 12, 130, 34);
        var cancel = Theme.FlatButton("Abbrechen");
        cancel.SetBounds(156, y + 12, 130, 34);
        f.Controls.Add(ok);
        f.Controls.Add(cancel);
        cancel.DialogResult = DialogResult.Cancel;
        f.CancelButton = cancel;
        src.TextChanged += (_, _) => info.Text = ParseId(src.Text, out int id) ? Describe(id) : "";
        ok.Click += (_, _) =>
        {
            if (!ParseId(src.Text, out int id) || id < 0 || id >= Max || KeyFor(id) == _key)
            {
                info.ForeColor = Theme.Red;
                info.Text = "Bitte eine gültige, andere ID eingeben.";
                return;
            }
            var sel = boxes.Where(b => b.cb.Checked).Aggregate(TileParts.None, (a, b) => a | b.p);
            if (sel == TileParts.None)
            {
                return;
            }
            _t.CopyParts(KeyFor(id), _key, sel);
            _status.Tag = null;
            _prov.Recompute(_key);
            LoadValues();
            Edited?.Invoke();
            f.DialogResult = DialogResult.OK;
        };
        Theme.Style(f);
        f.ShowDialog(FindForm());
    }

    private void DoNew()
    {
        using var f = NewDialog("Neues Item anlegen", 500, 500);
        var id = new TextBox { Left = 150, Top = 16, Width = 100 };
        var info = new Label { Left = 16, Top = 46, Width = 460, Height = 22, ForeColor = Theme.Muted };
        var tpl = new TextBox { Left = 150, Top = 112, Width = 100 };
        var tinfo = new Label { Left = 16, Top = 142, Width = 460, Height = 22, ForeColor = Theme.Muted };
        var name = new TextBox { Left = 150, Top = 182, Width = 220, MaxLength = 20 };
        var withArt = Theme.FlatButton("Nächster mit Art, ohne Tiledata");
        withArt.SetBounds(16, 72, 220, 30);
        var free = Theme.FlatButton("Nächster ganz freier");
        free.SetBounds(246, 72, 190, 30);
        f.Controls.Add(new Label { Text = "Neue Item-ID (ID/0x)", Left = 16, Top = 20, AutoSize = true });
        f.Controls.Add(new Label { Text = "Vorlage (optional)", Left = 16, Top = 116, AutoSize = true });
        f.Controls.Add(new Label { Text = "Name", Left = 16, Top = 186, AutoSize = true });
        foreach (var c in new Control[] { id, info, tpl, tinfo, name, withArt, free })
        {
            f.Controls.Add(c);
        }
        var img = new TextBox { Left = 150, Top = 222, Width = 240, ReadOnly = true };
        var pick = Theme.FlatButton("Bild wählen ...");
        pick.SetBounds(396, 220, 86, 28);
        var saveNow = new CheckBox { Text = "Sofort speichern (Tiledata und Art)", Left = 16, Top = 262, Width = 440, ForeColor = Theme.Text };
        f.Controls.Add(new Label { Text = "Bild (optional)", Left = 16, Top = 226, AutoSize = true });
        f.Controls.Add(img);
        f.Controls.Add(pick);
        f.Controls.Add(saveNow);
        f.Controls.Add(new Label
        {
            Text = "Die Vorlage liefert Flags, Gewicht, Höhe, Layer, Menge, AnimID, Hue und Licht. Mit einem Bild wird die Grafik im selben Schritt in art.mul vorgemerkt; ohne Bild meldet die Problemliste 'Tiledata ohne Art'. Ohne 'Sofort speichern' bleibt beides als ungespeicherte Änderung stehen.",
            Left = 16, Top = 296, Width = 460, Height = 70, ForeColor = Theme.Muted,
        });
        pick.Click += (_, _) =>
        {
            using var dlg = new OpenFileDialog { Title = "Bild für die Item-Art wählen", Filter = "Bilder (*.png;*.bmp;*.gif;*.jpg)|*.png;*.bmp;*.gif;*.jpg|Alle Dateien|*.*" };
            if (dlg.ShowDialog(f) == DialogResult.OK)
            {
                img.Text = dlg.FileName;
            }
        };
        int start = _id;
        withArt.Click += (_, _) => id.Text = "0x" + _prov.NextFreeItem(start + 1, true).ToString("X");
        free.Click += (_, _) => id.Text = "0x" + _prov.NextFreeItem(start + 1, false).ToString("X");
        id.TextChanged += (_, _) => info.Text = ParseId(id.Text, out int v) ? Describe(v) : "";
        tpl.TextChanged += (_, _) => tinfo.Text = ParseId(tpl.Text, out int v) ? Describe(v) : "";
        var ok = Theme.FlatButton("Anlegen", true);
        ok.SetBounds(16, 408, 130, 34);
        var cancel = Theme.FlatButton("Abbrechen");
        cancel.SetBounds(156, 408, 130, 34);
        f.Controls.Add(ok);
        f.Controls.Add(cancel);
        f.CancelButton = cancel;
        cancel.DialogResult = DialogResult.Cancel;
        ok.Click += (_, _) =>
        {
            if (!ParseId(id.Text, out int nid) || nid < 0 || nid >= _t.ItemCount)
            {
                info.ForeColor = Theme.Red;
                info.Text = "Bitte eine gültige ID eingeben.";
                return;
            }
            int tid = -1;
            if (tpl.Text.Trim().Length > 0 && (!ParseId(tpl.Text, out tid) || tid < 0 || tid >= _t.ItemCount))
            {
                tinfo.ForeColor = Theme.Red;
                tinfo.Text = "Ungültige Vorlage.";
                return;
            }
            int key = _t.LandCount + nid;
            var artStore = _prov.Ctx.Art;
            byte[]? artData = null;
            if (img.Text.Length > 0)
            {
                if (!artStore.CanWrite)
                {
                    MessageBox.Show(f, "Die Art kann nicht geschrieben werden, der Schreibschutz ist aktiv.", "UO Tinker", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                try
                {
                    using var ms = new MemoryStream(File.ReadAllBytes(img.Text));
                    using var im = Image.FromStream(ms);
                    using var bm = new Bitmap(im);
                    artData = ArtStore.EncodeStatic(bm, out string encErr);
                    if (artData == null)
                    {
                        MessageBox.Show(f, "Bild nicht verwendbar: " + encErr, "UO Tinker", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(f, "Bild konnte nicht gelesen werden: " + ex.Message, "UO Tinker", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            if (!_prov.IsFree(key) && MessageBox.Show(f, $"Item {nid} (0x{nid:X}) ist in der Tiledata bereits belegt ({_t.ItemName[nid]}). Überschreiben?", "UO Tinker", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }
            if (artData != null && artStore.StaticValid(nid) && MessageBox.Show(f, $"Slot {nid} (0x{nid:X}) enthält in art.mul bereits eine Grafik. Ersetzen?", "UO Tinker", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }
            if (tid >= 0)
            {
                _t.CopyParts(_t.LandCount + tid, key, TileParts.AllItem & ~TileParts.Name);
            }
            if (name.Text.Length > 0)
            {
                _t.ItemName[nid] = name.Text;
            }
            _t.Dirty.Add(key);
            if (artData != null)
            {
                artStore.SetStatic(nid, artData);
            }
            _prov.Recompute(key);
            if (saveNow.Checked)
            {
                string backups = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UOTinker", "backups");
                string? e1 = _t.Save(backups, out _);
                string? e2 = artStore.ArtPendingCount > 0 ? artStore.SaveArt(backups, out _) : null;
                if (e1 != null || e2 != null)
                {
                    MessageBox.Show(f, $"Teilweise nicht gespeichert (Änderungen bleiben im Programm erhalten):\n{(e1 != null ? "Tiledata: " + e1 + "\n" : "")}{(e2 != null ? "Art: " + e2 : "")}", "UO Tinker", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    _status.Tag = "saved";
                    MessageBox.Show(f, "Tiledata" + (artData != null ? " und Art" : "") + " gespeichert. Sicherungen liegen unter %APPDATA%\\UOTinker\\backups.", "UO Tinker", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            f.DialogResult = DialogResult.OK;
            Edited?.Invoke();
            GoTo?.Invoke(key);
        };
        Theme.Style(f);
        f.ShowDialog(FindForm());
    }

    private void DoSave()
    {
        string backups = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UOTinker", "backups");
        int n = _t.Dirty.Count;
        string? err = _t.Save(backups, out string backup);
        if (err != null)
        {
            _status.Tag = "saved";
            _status.ForeColor = Theme.Red;
            _status.Text = "Speichern fehlgeschlagen: " + err;
            return;
        }
        _status.Tag = "saved";
        _status.ForeColor = Theme.Green;
        _status.Text = $"{n} Änderung(en) in tiledata.mul gespeichert. Sicherung: {backup}\nDie Datei muss zusätzlich dort verteilt werden, wo sie gebraucht wird (Client, Server).";
        UpdateState();
        Edited?.Invoke();
    }
}
