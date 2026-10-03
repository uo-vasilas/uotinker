using System.Drawing;

namespace UOTinker;

public abstract class StoreEditor : UserControl, IEditorControl
{
    private readonly Label _head = new() { AutoSize = true, Font = Theme.UiBold, ForeColor = Theme.Gold };
    private readonly Label _status = new() { AutoSize = false, Height = 52, Dock = DockStyle.Bottom };
    private readonly Button _save = Theme.FlatButton(Loc.T("Speichern"), true);
    private readonly Button _revert = Theme.FlatButton(Loc.T("Eintrag zurücksetzen"));
    private readonly Button _revertAll = Theme.FlatButton(Loc.T("Alle verwerfen"));
    private string _saved = "";
    private bool _savedFailed;

    protected readonly FlowLayoutPanel Body = new() { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(4, 4, 4, 0) };
    protected bool Loading = true;

    public event Action? Edited;
    public event Action<int>? GoTo;

    public bool RefreshPreview { get; private set; }

    protected abstract string FileName { get; }
    protected abstract int Pending { get; }
    protected abstract bool EntryDirty { get; }
    protected abstract bool Writable { get; }
    protected abstract string Blocker { get; }
    protected abstract string? Persist(out string backup);
    protected abstract void RevertEntry();
    protected abstract void RevertEverything();
    protected abstract void LoadValues();

    protected void Init(string head)
    {
        Dock = DockStyle.Fill;
        BackColor = Theme.Bg;
        _head.Text = head;
        var top = new Panel { Dock = DockStyle.Top, Height = 28, Padding = new Padding(4, 6, 4, 0) };
        top.Controls.Add(_head);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4) };
        _save.Width = 150;
        _revert.Width = 150;
        _revertAll.Width = 120;
        buttons.Controls.Add(_save);
        buttons.Controls.Add(_revert);
        buttons.Controls.Add(_revertAll);
        Controls.Add(_status);
        Controls.Add(buttons);
        Controls.Add(Body);
        Controls.Add(top);
        EditorScroll.Attach(this);
        _save.Click += (_, _) => DoSave();
        _revert.Click += (_, _) =>
        {
            RevertEntry();
            LoadValues();
            Notify(true);
        };
        _revertAll.Click += (_, _) =>
        {
            int n = Pending;
            if (n == 0 || MessageBox.Show(FindForm(), Loc.F("{0} ungespeicherte Änderungen verwerfen?", n), "UO Tinker", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }
            RevertEverything();
            LoadValues();
            Notify(true);
        };
    }

    protected void Finish()
    {
        Theme.Style(this);
        LoadValues();
        Loading = false;
        UpdateState();
    }

    protected void Notify(bool refreshPreview)
    {
        _saved = "";
        RefreshPreview = refreshPreview;
        UpdateState();
        Edited?.Invoke();
    }

    protected void JumpTo(int key) => GoTo?.Invoke(key);

    protected static NumericUpDown Num(long max, int width = 80) => new() { Minimum = 0, Maximum = max, Width = width, BackColor = Theme.Input, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle };

    protected static Label Lbl(string text) => new() { Text = text, AutoSize = true, ForeColor = Theme.Muted, Margin = new Padding(3, 8, 8, 0) };

    private void DoSave()
    {
        int n = Pending;
        string? err = Persist(out string backup);
        _savedFailed = err != null;
        _saved = err != null ? Loc.T("Speichern fehlgeschlagen: ") + err : Loc.F("{0} Änderung(en) in {1} gespeichert. Sicherung: {2}\nDie Datei muss zusätzlich dort verteilt werden, wo sie gebraucht wird (Client, Server).", n, FileName, backup);
        UpdateState();
        if (err == null)
        {
            Edited?.Invoke();
        }
    }

    protected void UpdateState()
    {
        bool can = Writable;
        foreach (Control c in Body.Controls)
        {
            c.Enabled = can;
        }
        _save.Enabled = can && Pending > 0;
        _save.Text = Loc.F("Speichern ({0})", Pending);
        _revert.Enabled = can && EntryDirty;
        _revertAll.Enabled = can && Pending > 0;
        if (!can)
        {
            _status.ForeColor = Theme.Red;
            _status.Text = Blocker;
        }
        else if (_saved.Length > 0)
        {
            _status.ForeColor = _savedFailed ? Theme.Red : Theme.Green;
            _status.Text = _saved;
        }
        else
        {
            _status.ForeColor = Theme.Muted;
            _status.Text = Pending > 0 ? Loc.F("{0} Änderung(en) noch nicht gespeichert.", Pending) : Loc.T("Keine ungespeicherten Änderungen.");
        }
    }

    protected static ushort To555(Color c) => (ushort)(((c.R >> 3) << 10) | ((c.G >> 3) << 5) | (c.B >> 3));

    protected static bool PickColor(IWin32Window owner, ushort current, out ushort picked)
    {
        using var dlg = new ColorDialog { FullOpen = true, Color = Gfx.C16Color(current) };
        if (dlg.ShowDialog(owner) == DialogResult.OK)
        {
            picked = To555(dlg.Color);
            return true;
        }
        picked = current;
        return false;
    }
}

public sealed class RadarEditor : StoreEditor
{
    private readonly Context _c;
    private readonly RadarData _r;
    private readonly int _i;
    private readonly bool _land;
    private readonly int _id;
    private readonly Panel _swatch = new() { Width = 64, Height = 28, BorderStyle = BorderStyle.FixedSingle };
    private readonly TextBox _hex = new() { Width = 80, MaxLength = 6 };
    private readonly Button _pick = Theme.FlatButton(Loc.T("Farbe wählen ..."));
    private readonly Button _fromArt = Theme.FlatButton(Loc.T("Aus der Grafik berechnen"));
    private readonly Button _fillAll = Theme.FlatButton(Loc.T("Fehlende Item-Farben füllen ..."));

    protected override string FileName => "radarcol.mul";
    protected override int Pending => _r.Dirty.Count;
    protected override bool EntryDirty => _r.Dirty.Contains(_i);
    protected override bool Writable => _r.CanWrite;
    protected override string Blocker => Settings.IsReadOnly ? StoreIo.ReadOnlyMessage : Loc.T("radarcol.mul fehlt im Datenordner.");
    protected override string? Persist(out string backup) => _r.Save(out backup);
    protected override void RevertEntry() => _r.Revert(_i);
    protected override void RevertEverything() => _r.RevertAll();

    public RadarEditor(Context c, int index)
    {
        _c = c;
        _r = c.Radar;
        _i = index;
        _land = index < ArtStore.LandCount;
        _id = _land ? index : index - ArtStore.LandCount;
        Init(Loc.F("Radarfarbe {0} {1} ({2}) bearbeiten", _land ? "Land" : "Item", _id, Gfx.Hex(_id)));
        var flow = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Width = 460 };
        flow.Controls.Add(Lbl(Loc.T("Farbe (15 Bit, hex)")));
        flow.Controls.Add(_hex);
        flow.Controls.Add(_swatch);
        _pick.Width = 150;
        _fromArt.Width = 190;
        _fillAll.Width = 230;
        var row = new FlowLayoutPanel { AutoSize = true };
        row.Controls.Add(_pick);
        row.Controls.Add(_fromArt);
        var row2 = new FlowLayoutPanel { AutoSize = true };
        row2.Controls.Add(_fillAll);
        Body.Controls.Add(flow);
        Body.Controls.Add(row);
        Body.Controls.Add(row2);
        _hex.TextChanged += (_, _) =>
        {
            if (Loading || !TryParse(_hex.Text, out ushort v))
            {
                return;
            }
            Apply(v, false);
        };
        _pick.Click += (_, _) =>
        {
            if (PickColor(FindForm()!, _r.Col[_i], out ushort v))
            {
                Apply(v, true);
                LoadValues();
            }
        };
        _fromArt.Click += (_, _) =>
        {
            ushort? v = Average(_id, _land);
            if (v == null)
            {
                MessageBox.Show(FindForm(), Loc.T("Für diesen Eintrag gibt es keine Grafik."), "UO Tinker", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Apply(v.Value, true);
            LoadValues();
        };
        _fillAll.Click += (_, _) => FillMissing();
        Finish();
    }

    private static bool TryParse(string text, out ushort v)
    {
        v = 0;
        string t = text.Trim();
        if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            t = t[2..];
        }
        return t.Length > 0 && ushort.TryParse(t, System.Globalization.NumberStyles.HexNumber, null, out v);
    }

    private void Apply(ushort v, bool refresh)
    {
        _r.Set(_i, v);
        _swatch.BackColor = Gfx.C16Color(v);
        Notify(refresh);
    }

    protected override void LoadValues()
    {
        bool was = Loading;
        Loading = true;
        _hex.Text = _r.Col[_i].ToString("X4");
        _swatch.BackColor = Gfx.C16Color(_r.Col[_i]);
        Loading = was;
    }

    private ushort? Average(int id, bool land)
    {
        using var bmp = land ? _c.Art.GetLand(id, out _) : _c.Art.GetStatic(id, out _);
        if (bmp == null)
        {
            return null;
        }
        long r = 0, g = 0, b = 0, n = 0;
        for (int y = 0; y < bmp.Height; y++)
        {
            for (int x = 0; x < bmp.Width; x++)
            {
                var p = bmp.GetPixel(x, y);
                if (p.A > 0)
                {
                    r += p.R;
                    g += p.G;
                    b += p.B;
                    n++;
                }
            }
        }
        return n == 0 ? null : To555(Color.FromArgb((int)(r / n), (int)(g / n), (int)(b / n)));
    }

    private void FillMissing()
    {
        var todo = new List<int>();
        for (int id = 0; id < _c.Art.StaticCount && ArtStore.LandCount + id < _r.Col.Length; id++)
        {
            if (_c.Art.StaticValid(id) && _r.Col[ArtStore.LandCount + id] == 0)
            {
                todo.Add(id);
            }
        }
        if (todo.Count == 0)
        {
            MessageBox.Show(FindForm(), Loc.T("Alle Items mit Grafik haben schon eine Radarfarbe."), "UO Tinker", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (MessageBox.Show(FindForm(), Loc.F("{0} Items haben eine Grafik, aber die Radarfarbe 0. Farben aus den Grafiken berechnen und als Änderung vormerken?", todo.Count), "UO Tinker", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }
        int set = 0;
        foreach (int id in todo)
        {
            ushort? v = Average(id, false);
            if (v != null && v.Value != 0)
            {
                _r.Set(ArtStore.LandCount + id, v.Value);
                set++;
            }
        }
        LoadValues();
        Notify(true);
    }
}

public sealed class HueEditor : StoreEditor
{
    private readonly HueData _h;
    private readonly int _i;
    private readonly TextBox _name = new() { MaxLength = 19, Width = 240, BackColor = Theme.Input, ForeColor = Theme.Text };
    private readonly NumericUpDown _start = Num(65535);
    private readonly NumericUpDown _end = Num(65535);
    private readonly Button[] _cells = new Button[32];
    private readonly NumericUpDown _from = Num(65535);

    protected override string FileName => "hues.mul";
    protected override int Pending => _h.Dirty.Count;
    protected override bool EntryDirty => _h.Dirty.Contains(_i);
    protected override bool Writable => _h.CanWrite;
    protected override string Blocker => Settings.IsReadOnly ? StoreIo.ReadOnlyMessage : Loc.T("hues.mul fehlt im Datenordner.");
    protected override string? Persist(out string backup) => _h.Save(out backup);
    protected override void RevertEntry() => _h.Revert(_i);
    protected override void RevertEverything() => _h.RevertAll();

    public HueEditor(Context c, int index)
    {
        _h = c.Hues;
        _i = index;
        Init(Loc.F("Hue {0} ({1}) bearbeiten", index, Gfx.Hex(index)));
        var top = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Width = 560 };
        top.Controls.Add(Lbl(Loc.T("Name")));
        top.Controls.Add(_name);
        top.Controls.Add(Lbl(Loc.T("Tabelle von")));
        top.Controls.Add(_start);
        top.Controls.Add(Lbl(Loc.T("bis")));
        top.Controls.Add(_end);
        var grid = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Width = 440, Padding = new Padding(4, 8, 4, 4) };
        for (int k = 0; k < 32; k++)
        {
            int slot = k;
            var b = new Button { Width = 26, Height = 26, FlatStyle = FlatStyle.Flat, Margin = new Padding(2), Cursor = Cursors.Hand };
            b.FlatAppearance.BorderColor = Theme.Line;
            b.Click += (_, _) =>
            {
                if (PickColor(FindForm()!, _h.Colors[_i][slot], out ushort v))
                {
                    _h.Touch(_i);
                    _h.Colors[_i][slot] = v;
                    LoadValues();
                    Notify(true);
                }
            };
            _cells[k] = b;
            grid.Controls.Add(b);
        }
        var tools = new FlowLayoutPanel { AutoSize = true, Padding = new Padding(4, 0, 4, 4) };
        var ramp = Theme.FlatButton(Loc.T("Verlauf zwischen Farbe 1 und 32"));
        ramp.Width = 250;
        ramp.Click += (_, _) =>
        {
            _h.Touch(_i);
            var a = _h.Colors[_i][0];
            var z = _h.Colors[_i][31];
            for (int k = 1; k < 31; k++)
            {
                double t = k / 31.0;
                int r = (int)Math.Round(((a >> 10) & 31) * (1 - t) + ((z >> 10) & 31) * t);
                int g = (int)Math.Round(((a >> 5) & 31) * (1 - t) + ((z >> 5) & 31) * t);
                int bl = (int)Math.Round((a & 31) * (1 - t) + (z & 31) * t);
                _h.Colors[_i][k] = (ushort)((r << 10) | (g << 5) | bl);
            }
            LoadValues();
            Notify(true);
        };
        var copyRow = new FlowLayoutPanel { AutoSize = true, Padding = new Padding(4, 0, 4, 4) };
        var copy = Theme.FlatButton(Loc.T("Farben von Hue kopieren"));
        copy.Width = 200;
        _from.Maximum = Math.Max(0, c.Hues.Count - 1);
        copy.Click += (_, _) =>
        {
            int src = (int)_from.Value;
            if (src == _i)
            {
                return;
            }
            _h.Touch(_i);
            _h.Colors[src].CopyTo(_h.Colors[_i], 0);
            LoadValues();
            Notify(true);
        };
        tools.Controls.Add(ramp);
        copyRow.Controls.Add(copy);
        copyRow.Controls.Add(_from);
        Body.Controls.Add(top);
        Body.Controls.Add(grid);
        Body.Controls.Add(tools);
        Body.Controls.Add(copyRow);
        _name.TextChanged += (_, _) =>
        {
            if (!Loading)
            {
                _h.Touch(_i);
                _h.Names[_i] = _name.Text;
                Notify(false);
            }
        };
        _start.ValueChanged += (_, _) =>
        {
            if (!Loading)
            {
                _h.Touch(_i);
                _h.TableStart[_i] = (ushort)_start.Value;
                Notify(false);
            }
        };
        _end.ValueChanged += (_, _) =>
        {
            if (!Loading)
            {
                _h.Touch(_i);
                _h.TableEnd[_i] = (ushort)_end.Value;
                Notify(false);
            }
        };
        Finish();
    }

    protected override void LoadValues()
    {
        bool was = Loading;
        Loading = true;
        _name.Text = _h.Names[_i];
        _start.Value = _h.TableStart[_i];
        _end.Value = _h.TableEnd[_i];
        for (int k = 0; k < 32; k++)
        {
            _cells[k].BackColor = Gfx.C16Color(_h.Colors[_i][k]);
        }
        Loading = was;
    }
}

public sealed class SkillEditor : StoreEditor
{
    private readonly SkillData _s;
    private readonly int _i;
    private readonly CheckBox _valid = new() { Text = Loc.T("Skill vorhanden"), AutoSize = true, ForeColor = Theme.Text };
    private readonly CheckBox _button = new() { Text = Loc.T("Skill-Button im Fenster"), AutoSize = true, ForeColor = Theme.Text };
    private readonly TextBox _name = new() { MaxLength = 40, Width = 280, BackColor = Theme.Input, ForeColor = Theme.Text };

    protected override string FileName => Loc.T("skills.mul und skills.idx");
    protected override int Pending => _s.Dirty.Count;
    protected override bool EntryDirty => _s.Dirty.Contains(_i);
    protected override bool Writable => _s.CanWrite;
    protected override string Blocker => Settings.IsReadOnly ? StoreIo.ReadOnlyMessage : Loc.T("skills.idx fehlt im Datenordner.");
    protected override string? Persist(out string backup) => _s.Save(out backup);
    protected override void RevertEntry() => _s.Revert(_i);
    protected override void RevertEverything() => _s.RevertAll();

    public SkillEditor(Context c, int index)
    {
        _s = c.Skills;
        _i = index;
        Init(Loc.F("Skill {0} ({1}) bearbeiten", index, Gfx.Hex(index)));
        var f = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Width = 460 };
        f.Controls.Add(Lbl(Loc.T("Name")));
        f.Controls.Add(_name);
        var f2 = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Width = 460, Padding = new Padding(4, 4, 4, 0) };
        f2.Controls.Add(_valid);
        f2.Controls.Add(_button);
        Body.Controls.Add(f);
        Body.Controls.Add(f2);
        _name.TextChanged += (_, _) => Edit(() => _s.Names[_i] = _name.Text);
        _valid.CheckedChanged += (_, _) => Edit(() => _s.Valid[_i] = _valid.Checked);
        _button.CheckedChanged += (_, _) => Edit(() => _s.Button[_i] = _button.Checked);
        Finish();
    }

    private void Edit(Action change)
    {
        if (Loading)
        {
            return;
        }
        _s.Touch(_i);
        change();
        Notify(false);
    }

    protected override void LoadValues()
    {
        bool was = Loading;
        Loading = true;
        _name.Text = _s.Names[_i];
        _valid.Checked = _s.Valid[_i];
        _button.Checked = _s.Button[_i];
        Loading = was;
    }
}

public sealed class ClilocEditor : StoreEditor
{
    private readonly ClilocData _d;
    private int _number;
    private readonly TextBox _text = new() { Multiline = true, Width = 440, Height = 150, ScrollBars = ScrollBars.Vertical, BackColor = Theme.Input, ForeColor = Theme.Text, AcceptsReturn = true };
    private readonly Button _new = Theme.FlatButton(Loc.T("Neuer Eintrag ..."));

    protected override string FileName => System.IO.Path.GetFileName(_d.Path);
    protected override int Pending => _d.Dirty.Count;
    protected override bool EntryDirty => _d.Dirty.Contains(_number);
    protected override bool Writable => _d.CanWrite;
    protected override string Blocker => _d.WriteBlocker;
    protected override string? Persist(out string backup) => _d.Save(out backup);
    protected override void RevertEntry() => _d.Revert(_number);
    protected override void RevertEverything() => _d.RevertAll();

    public ClilocEditor(ClilocData d, int number)
    {
        _d = d;
        _number = number;
        Init(Loc.F("Cliloc {0} ({1}) bearbeiten", number, Gfx.Hex(number)));
        var row = new FlowLayoutPanel { AutoSize = true, Padding = new Padding(4, 4, 4, 4) };
        _new.Width = 170;
        row.Controls.Add(_new);
        Body.Controls.Add(_text);
        Body.Controls.Add(row);
        _text.TextChanged += (_, _) =>
        {
            if (Loading)
            {
                return;
            }
            _d.SetText(_number, _text.Text.Replace("\r\n", "\n").Replace('\n', ' '));
            Notify(false);
        };
        _new.Click += (_, _) => DoNew();
        Finish();
    }

    protected override void LoadValues()
    {
        bool was = Loading;
        Loading = true;
        int i = _d.IndexOf(_number);
        _text.Text = i >= 0 ? _d.Texts[i] : "";
        Loading = was;
    }

    private void DoNew()
    {
        using var f = new Form
        {
            Text = Loc.T("Neuer Cliloc-Eintrag"), Width = 500, Height = 290, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false, BackColor = Theme.Bg, ForeColor = Theme.Text, Font = Theme.Ui,
        };
        var num = new TextBox { Left = 16, Top = 40, Width = 140, BackColor = Theme.Input, ForeColor = Theme.Text };
        var txt = new TextBox { Left = 16, Top = 100, Width = 450, Height = 80, Multiline = true, BackColor = Theme.Input, ForeColor = Theme.Text };
        var info = new Label { Left = 170, Top = 42, Width = 300, Height = 20, ForeColor = Theme.Red };
        var ok = Theme.FlatButton(Loc.T("Anlegen"), true);
        ok.SetBounds(16, 196, 120, 36);
        var cancel = Theme.FlatButton(Loc.T("Abbrechen"));
        cancel.SetBounds(146, 196, 120, 36);
        f.Controls.Add(new Label { Text = Loc.T("Nummer (dezimal oder 0x)"), Left = 16, Top = 18, AutoSize = true });
        f.Controls.Add(new Label { Text = Loc.T("Text"), Left = 16, Top = 78, AutoSize = true });
        f.Controls.Add(num);
        f.Controls.Add(txt);
        f.Controls.Add(info);
        f.Controls.Add(ok);
        f.Controls.Add(cancel);
        cancel.Click += (_, _) => f.DialogResult = DialogResult.Cancel;
        int made = -1;
        ok.Click += (_, _) =>
        {
            string t = num.Text.Trim();
            bool hex = t.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
            if (!int.TryParse(hex ? t[2..] : t, hex ? System.Globalization.NumberStyles.HexNumber : System.Globalization.NumberStyles.Integer, null, out int n) || n <= 0)
            {
                info.Text = Loc.T("Bitte eine gültige Nummer eingeben.");
                return;
            }
            if (_d.IndexOf(n) >= 0)
            {
                info.Text = Loc.T("Diese Nummer gibt es schon.");
                return;
            }
            _d.SetText(n, txt.Text.Replace("\r\n", "\n").Replace('\n', ' '));
            made = n;
            f.DialogResult = DialogResult.OK;
        };
        Theme.Style(f);
        if (f.ShowDialog(FindForm()) == DialogResult.OK && made > 0)
        {
            Notify(false);
            JumpTo(made);
        }
    }
}
