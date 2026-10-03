using System.Drawing;
using System.Drawing.Drawing2D;

namespace UOTinker;

public sealed class Canvas : Panel
{
    public Bitmap? Still;
    public Color[]? Palette;
    public Color? Swatch;
    public List<AnimFrame>? Frames;
    public int FrameIndex;
    public int Zoom = 2;
    public Color Bg = Theme.Card;

    private int _minX, _minY, _maxX, _maxY;

    public Canvas()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
    }

    public void SetFrames(List<AnimFrame>? frames)
    {
        Frames = frames;
        FrameIndex = 0;
        _minX = _minY = int.MaxValue;
        _maxX = _maxY = int.MinValue;
        if (frames != null)
        {
            foreach (var f in frames)
            {
                if (f.Bmp == null)
                {
                    continue;
                }
                _minX = Math.Min(_minX, -f.Cx);
                _minY = Math.Min(_minY, -f.Cy - f.H);
                _maxX = Math.Max(_maxX, -f.Cx + f.W);
                _maxY = Math.Max(_maxY, -f.Cy);
            }
        }
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Bg);
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;

        if (Palette != null)
        {
            int n = Palette.Length;
            float w = (float)(Width - 20) / n;
            for (int i = 0; i < n; i++)
            {
                using var b = new SolidBrush(Palette[i]);
                g.FillRectangle(b, 10 + i * w, 10, w + 1, 80);
            }
            return;
        }

        if (Frames != null && Frames.Count > 0 && _minX != int.MaxValue)
        {
            var f = Frames[FrameIndex % Frames.Count];
            int z = Math.Max(1, Zoom);
            int bw = (_maxX - _minX) * z;
            int bh = (_maxY - _minY) * z;
            int ox = (Width - bw) / 2 - _minX * z;
            int oy = (Height - bh) / 2 - _minY * z;
            if (f.Bmp != null)
            {
                g.DrawImage(f.Bmp, ox + (-f.Cx) * z, oy + (-f.Cy - f.H) * z, f.W * z, f.H * z);
            }
            using var pen = new Pen(Color.FromArgb(90, Color.Red));
            g.DrawLine(pen, ox - 6, oy, ox + 6, oy);
            g.DrawLine(pen, ox, oy - 6, ox, oy + 6);
            using var br = new SolidBrush(Color.Gainsboro);
            g.DrawString($"Frame {FrameIndex % Frames.Count + 1}/{Frames.Count}", Font, br, 6, 4);
            return;
        }

        int top = 10;
        if (Swatch.HasValue)
        {
            using var b = new SolidBrush(Swatch.Value);
            g.FillRectangle(b, 10, 10, 120, 50);
            top = 70;
        }
        if (Still != null)
        {
            int z = Zoom <= 0 ? 1 : Zoom;
            int w = Still.Width * z;
            int h = Still.Height * z;
            int x = Swatch.HasValue ? 10 : Math.Max(10, (Width - w) / 2);
            int y = Swatch.HasValue ? top : Math.Max(10, (Height - h) / 2);
            g.DrawImage(Still, x, y, w, h);
        }
    }
}

public sealed class PreviewHost : UserControl
{
    private readonly Canvas _canvas = new() { Dock = DockStyle.Fill };
    private readonly TextBox _info = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Bottom, Height = 190, Font = new Font("Consolas", 9f) };
    private readonly FlowLayoutPanel _extras = new() { Dock = DockStyle.Bottom, AutoScroll = true, Height = 0, WrapContents = false };
    private readonly FlowLayoutPanel _bar = new() { Dock = DockStyle.Top, AutoSize = true, Visible = false, WrapContents = true };
    private readonly ComboBox _src = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 330 };
    private readonly ComboBox _act = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
    private readonly ComboBox _dir = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 50 };
    private readonly ComboBox _zoom = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
    private readonly ComboBox _bg = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
    private readonly CheckBox _play = new() { Text = Loc.T("Abspielen"), Checked = true, AutoSize = true };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 110 };
    private AnimRef? _anim;
    private bool _busy;
    private Control? _editor;

    private static readonly (string, Color)[] Bgs =
    {
        (Loc.T("Standard"), Theme.Card), (Loc.T("Schwarz"), Color.Black), (Loc.T("Weiss"), Color.White),
        (Loc.T("Hellgrau"), Color.Silver), ("Magenta", Color.Magenta),
    };

    public PreviewHost()
    {
        _src.Items.Clear();
        _zoom.Items.AddRange(new object[] { "1x", "2x", "3x", "4x", "6x", "8x" });
        _zoom.SelectedIndex = 1;
        foreach (var (n, _) in Bgs)
        {
            _bg.Items.Add(n);
        }
        _bg.SelectedIndex = 0;
        for (int d = 0; d < 5; d++)
        {
            _dir.Items.Add(d.ToString());
        }
        _dir.SelectedIndex = 0;

        _bar.Controls.Add(Lbl(Loc.T("Quelle"))); _bar.Controls.Add(_src);
        _bar.Controls.Add(Lbl(Loc.T("Aktion"))); _bar.Controls.Add(_act);
        _bar.Controls.Add(Lbl(Loc.T("Richtung"))); _bar.Controls.Add(_dir);
        _bar.Controls.Add(Lbl("Zoom")); _bar.Controls.Add(_zoom);
        _bar.Controls.Add(Lbl(Loc.T("Hintergrund"))); _bar.Controls.Add(_bg);
        _bar.Controls.Add(_play);

        Controls.Add(_canvas);
        Controls.Add(_extras);
        Controls.Add(_info);
        Controls.Add(_bar);

        _src.SelectedIndexChanged += (_, _) => { if (!_busy) { FillActions(); Reload(); } };
        _act.SelectedIndexChanged += (_, _) => { if (!_busy) { Reload(); } };
        _dir.SelectedIndexChanged += (_, _) => { if (!_busy) { Reload(); } };
        _zoom.SelectedIndexChanged += (_, _) => { ApplyZoom(); };
        _bg.SelectedIndexChanged += (_, _) => { _canvas.Bg = Bgs[Math.Max(0, _bg.SelectedIndex)].Item2; _canvas.Invalidate(); };
        _play.CheckedChanged += (_, _) => { _timer.Enabled = _play.Checked && (_canvas.Frames?.Count ?? 0) > 1; };
        _timer.Tick += (_, _) =>
        {
            if (_canvas.Frames is { Count: > 1 })
            {
                _canvas.FrameIndex = (_canvas.FrameIndex + 1) % _canvas.Frames.Count;
                _canvas.Invalidate();
            }
        };
        ApplyZoom();
    }

    private static Label Lbl(string t) => new() { Text = t, AutoSize = true, Margin = new Padding(6, 6, 2, 0) };

    private void ApplyZoom()
    {
        _canvas.Zoom = int.Parse(((string)_zoom.SelectedItem!).TrimEnd('x'));
        _canvas.Invalidate();
    }

    public void Show(PreviewData? p)
    {
        _timer.Stop();
        _anim = null;
        foreach (Control c in _extras.Controls)
        {
            c.Dispose();
        }
        _extras.Controls.Clear();
        if (_editor != null)
        {
            Controls.Remove(_editor);
            _editor.Dispose();
            _editor = null;
        }
        _canvas.Dock = DockStyle.Fill;
        _info.Height = 190;
        _canvas.Still = null;
        _canvas.Palette = null;
        _canvas.Swatch = null;
        _canvas.SetFrames(null);
        _bar.Visible = false;
        if (p == null)
        {
            _info.Text = "";
            _extras.Height = 0;
            return;
        }

        _info.Text = p.Info.Replace("\r\n", "\n").Replace("\n", "\r\n");
        _canvas.Still = p.Image;
        _canvas.Palette = p.Palette;
        _canvas.Swatch = p.Swatch;

        foreach (var (title, bmp) in p.Extras)
        {
            var box = new Panel { Width = Math.Max(120, bmp.Width + 10), Height = Math.Min(bmp.Height, 180) + 22 };
            box.Controls.Add(new PictureBox { Image = bmp, SizeMode = PictureBoxSizeMode.Zoom, Dock = DockStyle.Fill });
            box.Controls.Add(new Label { Text = title, Dock = DockStyle.Top, Height = 18 });
            _extras.Controls.Add(box);
            Theme.Style(box);
        }
        _extras.Height = p.Extras.Count > 0 ? 205 : 0;

        if (p.Editor != null)
        {
            _editor = p.Editor;
            _canvas.Dock = DockStyle.Top;
            _canvas.Height = 150;
            _info.Height = 110;
            Controls.Add(_editor);
            _editor.BringToFront();
        }

        if (p.Anim != null && p.Anim.Sources.Count > 0)
        {
            _anim = p.Anim;
            _busy = true;
            _src.Items.Clear();
            foreach (var s in p.Anim.Sources)
            {
                _src.Items.Add(s.Label);
            }
            _src.SelectedIndex = 0;
            _busy = false;
            _bar.Visible = true;
            FillActions();
            Reload();
        }
        _canvas.Invalidate();
    }

    private void FillActions()
    {
        if (_anim == null || _src.SelectedIndex < 0)
        {
            return;
        }
        int prev = _act.SelectedItem is string s && int.TryParse(s[(s.LastIndexOf(' ') + 1)..], out int pa) ? pa : -1;
        _busy = true;
        _act.Items.Clear();
        var src = _anim.Sources[_src.SelectedIndex];
        foreach (int a in src.Actions)
        {
            _act.Items.Add(Loc.F("Aktion {0}", a));
        }
        int sel = Array.IndexOf(src.Actions, prev);
        if (sel < 0)
        {
            sel = Math.Max(0, Array.IndexOf(src.Actions, 0));
        }
        _act.SelectedIndex = Math.Min(sel, _act.Items.Count - 1);
        _busy = false;
    }

    private void Reload()
    {
        if (_anim == null || _src.SelectedIndex < 0 || _act.SelectedIndex < 0)
        {
            return;
        }
        var src = _anim.Sources[_src.SelectedIndex];
        int action = src.Actions[_act.SelectedIndex];
        var frames = _anim.Store.Decode(src, action, _dir.SelectedIndex, _anim.Equipment, out string err);
        _canvas.Still = null;
        _canvas.SetFrames(frames);
        if (frames.Count == 0 || frames.All(f => f.Bmp == null))
        {
            _info.AppendText(Loc.F("\r\n[{0}, Aktion {1}, Richtung {2}] keine Frames. {3}", src.Label, action, _dir.SelectedIndex, err));
        }
        _timer.Enabled = _play.Checked && frames.Count > 1;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
        }
        base.Dispose(disposing);
    }
}

public sealed class BrowserControl : UserControl
{
    private readonly IProvider _p;
    private readonly ListView _lv = new() { Dock = DockStyle.Fill, View = View.Details, VirtualMode = true, FullRowSelect = true, GridLines = true, HideSelection = false, MultiSelect = false };
    private readonly PreviewHost _host = new() { Dock = DockStyle.Fill };
    private readonly TextBox _txt = new() { Width = 230 };
    private readonly TextBox _goto = new() { Width = 80 };
    private readonly Label _count = new() { AutoSize = true, Margin = new Padding(10, 6, 0, 0) };
    private readonly List<ComboBox> _combos = new();
    private readonly System.Windows.Forms.Timer _debounce = new() { Interval = 250 };
    private List<int> _view = new();
    private string[]? _search;
    private ThumbGrid? _grid;
    private readonly Label _summary = new() { Dock = DockStyle.Bottom, Height = 22, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ComboBox _mode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
    private readonly ComboBox _cellSize = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 60 };

    public BrowserControl(IProvider p)
    {
        _p = p;
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Padding = new Padding(4) };
        top.Controls.Add(new Label { Text = Loc.T("Suche:"), AutoSize = true, Margin = new Padding(3, 6, 0, 0) });
        top.Controls.Add(_txt);
        foreach (var f in p.Filters)
        {
            top.Controls.Add(new Label { Text = f.Label + ":", AutoSize = true, Margin = new Padding(10, 6, 0, 0) });
            var cb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = Math.Max(110, f.Options.Max(o => o.Length) * 7 + 30) };
            cb.Items.AddRange(f.Options);
            cb.SelectedIndex = 0;
            cb.SelectedIndexChanged += (_, _) => Apply();
            _combos.Add(cb);
            top.Controls.Add(cb);
        }
        top.Controls.Add(new Label { Text = Loc.T("Gehe zu ID:"), AutoSize = true, Margin = new Padding(10, 6, 0, 0) });
        top.Controls.Add(_goto);
        top.Controls.Add(_count);
        var csv = Theme.FlatButton(Loc.T("CSV exportieren"));
        csv.Height = 26;
        csv.Width = 120;
        csv.Margin = new Padding(10, 2, 0, 0);
        csv.Click += (_, _) => ExportCsv();
        top.Controls.Add(csv);

        var sc = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel2 };
        bool splitSet = false;
        sc.SizeChanged += (_, _) =>
        {
            if (!splitSet && sc.Width > 700)
            {
                splitSet = true;
                sc.SplitterDistance = Math.Max(320, sc.Width - 470);
            }
        };
        sc.Panel1.Controls.Add(_lv);
        sc.Panel2.Controls.Add(_host);

        if (p is IThumbProvider tp)
        {
            _grid = new ThumbGrid { Dock = DockStyle.Fill, Loader = tp.Thumb, IsFree = p is RadarProvider or TiledataProvider ? (_ => false) : p.IsFree, LabelOf = tp.ThumbLabel };
            _grid.Selected += OnGridSelect;
            sc.Panel1.Controls.Add(_grid);

            _mode.Items.AddRange(new object[] { Loc.T("Raster"), Loc.T("Liste") });
            _mode.SelectedIndex = 0;
            _cellSize.Items.AddRange(new object[] { "48", "64", "96", "128", "192" });
            _cellSize.SelectedIndex = 2;
            top.Controls.Add(new Label { Text = Loc.T("Ansicht:"), AutoSize = true, Margin = new Padding(10, 6, 0, 0) });
            top.Controls.Add(_mode);
            top.Controls.Add(new Label { Text = Loc.T("Zelle:"), AutoSize = true, Margin = new Padding(6, 6, 0, 0) });
            top.Controls.Add(_cellSize);
            _mode.SelectedIndexChanged += (_, _) => ApplyMode();
            _cellSize.SelectedIndexChanged += (_, _) => { _grid.Cell = int.Parse((string)_cellSize.SelectedItem!); };
            _grid.Cell = 96;
            ApplyMode();
        }

        _summary.Text = " " + p.Summary;
        Controls.Add(sc);
        Controls.Add(_summary);
        Controls.Add(top);

        for (int i = 0; i < p.Columns.Length; i++)
        {
            _lv.Columns.Add(p.Columns[i], p.Widths[i]);
        }

        _lv.OwnerDraw = true;
        _lv.GridLines = false;
        _lv.DrawColumnHeader += (_, e) =>
        {
            using var b = new SolidBrush(Theme.Button);
            e.Graphics.FillRectangle(b, e.Bounds);
            using var pen = new Pen(Theme.Line);
            e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            e.Graphics.DrawLine(pen, e.Bounds.Right - 1, e.Bounds.Top + 4, e.Bounds.Right - 1, e.Bounds.Bottom - 4);
            TextRenderer.DrawText(e.Graphics, e.Header?.Text ?? "", Theme.UiBold, new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height), Theme.Muted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        };
        _lv.DrawItem += (_, e) => e.DrawDefault = false;
        _lv.DrawSubItem += (_, e) =>
        {
            bool sel = e.Item != null && _lv.SelectedIndices.Contains(e.ItemIndex);
            bool free = e.ItemIndex >= 0 && e.ItemIndex < _view.Count && _p.IsFree(_view[e.ItemIndex]);
            using (var b = new SolidBrush(sel ? Theme.GoldDark : Theme.Card))
            {
                e.Graphics.FillRectangle(b, e.Bounds);
            }
            using (var pen = new Pen(Theme.Line))
            {
                e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            }
            var col = sel ? Theme.Gold : (free ? Color.FromArgb(120, 112, 130) : Theme.Text);
            TextRenderer.DrawText(e.Graphics, e.SubItem?.Text ?? "", Theme.Ui, new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height), col,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        };
        _lv.RetrieveVirtualItem += (_, e) => e.Item = MakeItem(_view[e.ItemIndex]);
        _lv.SelectedIndexChanged += (_, _) => OnSelect();
        _txt.TextChanged += (_, _) => { _debounce.Stop(); _debounce.Start(); };
        _debounce.Tick += (_, _) => { _debounce.Stop(); Apply(); };
        _goto.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                GoTo();
            }
        };

        BackColor = Theme.Bg;
        Theme.Style(this);
        Apply();
    }

    private ListViewItem MakeItem(int i)
    {
        var item = new ListViewItem(_p.Row(i));
        if (_p.IsFree(i))
        {
            item.ForeColor = Color.Gray;
        }
        return item;
    }

    private void Apply()
    {
        string q = _txt.Text.Trim().ToLowerInvariant();
        var tokens = q.Length == 0 ? Array.Empty<string>() : q.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length > 0 && _search == null)
        {
            Cursor = Cursors.WaitCursor;
            _search = new string[_p.Count];
            for (int i = 0; i < _search.Length; i++)
            {
                _search[i] = _p.SearchText(i);
            }
            Cursor = Cursors.Default;
        }

        var filters = _p.Filters;
        var view = new List<int>(_p.Count);
        for (int i = 0; i < _p.Count; i++)
        {
            bool ok = true;
            for (int f = 0; f < filters.Length && ok; f++)
            {
                int o = _combos[f].SelectedIndex;
                if (o > 0 && !filters[f].Pass(i, o))
                {
                    ok = false;
                }
            }
            if (ok && tokens.Length > 0)
            {
                string s = _search![i];
                foreach (var t in tokens)
                {
                    if (!s.Contains(t, StringComparison.Ordinal))
                    {
                        ok = false;
                        break;
                    }
                }
            }
            if (ok)
            {
                view.Add(i);
            }
        }
        _view = view;
        _lv.VirtualListSize = 0;
        _lv.VirtualListSize = _view.Count;
        _grid?.SetView(_view);
        _count.Text = Loc.F("{0} von {1} angezeigt", _view.Count, _p.Count);
        _host.Show(null);
    }

    private void ApplyMode()
    {
        bool raster = _mode.SelectedIndex == 0;
        _grid!.Visible = raster;
        _lv.Visible = !raster;
        _cellSize.Enabled = raster;
    }

    private void OnGridSelect(int i) => ShowEntry(i);

    private void ShowEntry(int i, bool record = true)
    {
        try
        {
            var pv = _p.Preview(i);
            _host.Show(pv);
            if (pv.Editor is IEditorControl ec)
            {
                ec.Edited += () =>
                {
                    _lv.Invalidate();
                    _grid?.Invalidate();
                    _summary.Text = " " + _p.Summary;
                    if (_p is RadarProvider or HuesProvider)
                    {
                        _grid?.ClearCache();
                        if (ec is StoreEditor se && se.RefreshPreview)
                        {
                            BeginInvoke(() => ShowEntry(i, false));
                        }
                    }
                    if (_p is ArtProvider or LandProvider or GumpProvider)
                    {
                        _grid?.ClearCache();
                        GraphicsEdited?.Invoke();
                        BeginInvoke(() => ShowEntry(i, false));
                    }
                };
                ec.GoTo += key => BeginInvoke(() =>
                {
                    _grid?.ClearCache();
                    ResetFilters();
                    GoToKey(key);
                });
            }
            if (record)
            {
                Viewed?.Invoke(pv.Info.Split('\n')[0].Trim(), _p.KeyOf(i));
            }
        }
        catch (Exception ex)
        {
            _host.Show(new PreviewData { Info = Loc.T("Fehler bei der Vorschau: ") + ex.Message });
        }
    }

    private void OnSelect()
    {
        if (_lv.SelectedIndices.Count == 0)
        {
            return;
        }
        ShowEntry(_view[_lv.SelectedIndices[0]]);
    }

    private void GoTo()
    {
        string t = _goto.Text.Trim();
        int n;
        if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(t[2..], System.Globalization.NumberStyles.HexNumber, null, out n))
            {
                return;
            }
        }
        else if (!int.TryParse(t, out n))
        {
            return;
        }
        for (int k = 0; k < _view.Count; k++)
        {
            if (_p.KeyOf(_view[k]) == n)
            {
                if (_grid is { Visible: true })
                {
                    _grid.SelectKey(n, _p.KeyOf);
                    _grid.Focus();
                    return;
                }
                _lv.SelectedIndices.Clear();
                _lv.SelectedIndices.Add(k);
                _lv.EnsureVisible(k);
                _lv.Focus();
                return;
            }
        }
        _count.Text = Loc.F("ID {0} in der aktuellen Auswahl nicht gefunden", n);
    }

    private void ExportCsv()
    {
        using var dlg = new SaveFileDialog { Title = Loc.T("Angezeigte Liste als CSV speichern"), Filter = "CSV (*.csv)|*.csv", FileName = $"uotinker_{DateTime.Now:yyyyMMdd-HHmm}.csv" };
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK)
        {
            return;
        }
        try
        {
            Cursor = Cursors.WaitCursor;
            static string Q(string s) => s.IndexOfAny(new[] { ';', '"', '\r', '\n' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
            using var w = new StreamWriter(dlg.FileName, false, new System.Text.UTF8Encoding(true));
            w.WriteLine(string.Join(';', _p.Columns.Select(Q)));
            foreach (int i in _view)
            {
                w.WriteLine(string.Join(';', _p.Row(i).Select(Q)));
            }
            _count.Text = Loc.F("{0} Zeilen nach {1} exportiert", _view.Count, dlg.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(FindForm(), Loc.T("Export fehlgeschlagen: ") + ex.Message, "UO Tinker", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    public event Action<string, int>? Viewed;

    public event Action? GraphicsEdited;

    public void SetFilter(string label, int option)
    {
        var filters = _p.Filters;
        for (int f = 0; f < filters.Length; f++)
        {
            if ((filters[f].Label == label || filters[f].Label == Loc.T(label)) && option < _combos[f].Items.Count)
            {
                _combos[f].SelectedIndex = option;
                return;
            }
        }
    }

    public void ResetFilters()
    {
        _txt.Text = "";
        foreach (var c in _combos)
        {
            c.SelectedIndex = 0;
        }
    }

    public void JumpTo(int key)
    {
        _grid?.ClearCache();
        ResetFilters();
        GoToKey(key);
    }

    public void GoToKey(int key)
    {
        _goto.Text = key.ToString();
        GoTo();
    }

}

