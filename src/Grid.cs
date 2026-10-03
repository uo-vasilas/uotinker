using System.Drawing;
using System.Drawing.Drawing2D;

namespace UOTinker;

public interface IThumbProvider
{
    Bitmap? Thumb(int i);
    string ThumbLabel(int i);
}

public sealed class ThumbGrid : Control
{
    private readonly VScrollBar _sb = new() { Dock = DockStyle.Right, SmallChange = 1 };
    private readonly Dictionary<int, Bitmap?> _cache = new();
    private readonly Queue<int> _order = new();
    private const int MaxCache = 1500;

    private IReadOnlyList<int> _view = Array.Empty<int>();
    private int _cell = 96;
    private int _selected = -1;

    public Func<int, Bitmap?> Loader { get; set; } = _ => null;
    public Func<int, bool> IsFree { get; set; } = _ => false;
    public Func<int, string> LabelOf { get; set; } = i => i.ToString();
    public event Action<int>? Selected;

    public int Cell
    {
        get => _cell;
        set
        {
            _cell = value;
            Relayout();
        }
    }

    public ThumbGrid()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        SetStyle(ControlStyles.Selectable | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
        TabStop = true;
        Controls.Add(_sb);
        _sb.Scroll += (_, _) => Invalidate();
        BackColor = Theme.Bg;
    }

    private int CellH => _cell + 18;
    private int Cols => Math.Max(1, (Width - _sb.Width) / _cell);
    private int Rows => (_view.Count + Cols - 1) / Cols;
    private int VisibleRows => Math.Max(1, Height / CellH);

    public void SetView(IReadOnlyList<int> view)
    {
        _view = view;
        _selected = -1;
        _sb.Value = 0;
        Relayout();
    }

    public void ClearCache()
    {
        foreach (var b in _cache.Values)
        {
            b?.Dispose();
        }
        _cache.Clear();
        _order.Clear();
        Invalidate();
    }

    private void Relayout()
    {
        int max = Math.Max(0, Rows - VisibleRows);
        _sb.Minimum = 0;
        _sb.Maximum = max + VisibleRows - 1;
        _sb.LargeChange = VisibleRows;
        _sb.Value = Math.Min(_sb.Value, max);
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Relayout();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        int step = e.Delta > 0 ? -3 : 3;
        int max = Math.Max(0, Rows - VisibleRows);
        _sb.Value = Math.Clamp(_sb.Value + step, 0, max);
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        int col = e.X / _cell;
        int row = _sb.Value + e.Y / CellH;
        if (col >= Cols)
        {
            return;
        }
        int idx = row * Cols + col;
        if (idx >= 0 && idx < _view.Count)
        {
            Select(idx);
        }
    }

    private void Select(int idx)
    {
        _selected = idx;
        int row = idx / Cols;
        if (row < _sb.Value)
        {
            _sb.Value = row;
        }
        else if (row >= _sb.Value + VisibleRows)
        {
            _sb.Value = Math.Min(row - VisibleRows + 1, Math.Max(0, Rows - VisibleRows));
        }
        Invalidate();
        Selected?.Invoke(_view[idx]);
    }

    public void SelectKey(int key, Func<int, int> keyOf)
    {
        for (int k = 0; k < _view.Count; k++)
        {
            if (keyOf(_view[k]) == key)
            {
                Select(k);
                return;
            }
        }
    }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (_view.Count == 0)
        {
            return;
        }
        int cur = _selected < 0 ? 0 : _selected;
        int next = e.KeyCode switch
        {
            Keys.Left => cur - 1,
            Keys.Right => cur + 1,
            Keys.Up => cur - Cols,
            Keys.Down => cur + Cols,
            Keys.PageUp => cur - Cols * VisibleRows,
            Keys.PageDown => cur + Cols * VisibleRows,
            Keys.Home => 0,
            Keys.End => _view.Count - 1,
            _ => cur,
        };
        next = Math.Clamp(next, 0, _view.Count - 1);
        if (next != _selected)
        {
            Select(next);
        }
        e.Handled = true;
    }

    private Bitmap? Get(int key)
    {
        if (_cache.TryGetValue(key, out var b))
        {
            return b;
        }
        Bitmap? bmp = null;
        try
        {
            bmp = Loader(key);
        }
        catch
        {
        }
        _cache[key] = bmp;
        _order.Enqueue(key);
        return bmp;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        if (_view.Count == 0)
        {
            using var br = new SolidBrush(Theme.Muted);
            g.DrawString(Loc.T("Keine Eintraege in dieser Auswahl."), Font, br, 10, 10);
            return;
        }

        int cols = Cols;
        int firstRow = _sb.Value;
        int inner = _cell - 8;
        using var textBr = new SolidBrush(Theme.Text);
        using var freeBr = new SolidBrush(Color.FromArgb(120, 112, 130));
        using var small = new Font(Font.FontFamily, 7.5f);
        using var cellPen = new Pen(Theme.Line);
        using var selPen = new Pen(Theme.Gold, 3);

        for (int r = 0; r <= VisibleRows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                int idx = (firstRow + r) * cols + c;
                if (idx >= _view.Count)
                {
                    break;
                }
                int key = _view[idx];
                int x = c * _cell;
                int y = r * CellH;
                g.DrawRectangle(cellPen, x, y, _cell - 1, CellH - 1);

                if (IsFree(key))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(Loc.T("frei"), small, freeBr, new RectangleF(x, y, _cell, _cell), sf);
                }
                else
                {
                    var bmp = Get(key);
                    if (bmp != null && bmp.Width > 0 && bmp.Height > 0)
                    {
                        float scale = Math.Min((float)inner / bmp.Width, (float)inner / bmp.Height);
                        if (scale >= 1f)
                        {
                            scale = (float)Math.Floor(scale);
                            g.InterpolationMode = InterpolationMode.NearestNeighbor;
                        }
                        else
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        }
                        g.PixelOffsetMode = PixelOffsetMode.Half;
                        float w = bmp.Width * scale;
                        float h = bmp.Height * scale;
                        g.DrawImage(bmp, x + (_cell - w) / 2f, y + (_cell - h) / 2f, w, h);
                    }
                    else
                    {
                        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString(Loc.T("kein\nBild"), small, freeBr, new RectangleF(x, y, _cell, _cell), sf);
                    }
                }

                var lf = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
                g.DrawString(LabelOf(key), small, IsFree(key) ? freeBr : textBr, new RectangleF(x, y + _cell, _cell, 16), lf);

                if (idx == _selected)
                {
                    g.DrawRectangle(selPen, x + 1, y + 1, _cell - 3, CellH - 3);
                }
            }
        }

        while (_order.Count > MaxCache)
        {
            int old = _order.Dequeue();
            if (_cache.Remove(old, out var ob))
            {
                ob?.Dispose();
            }
        }
    }
}

