using System.Drawing;

namespace UOTinker;

public sealed class PageDef
{
    public string Key = "";
    public string Group = "";
    public string Title = "";
    public string Glyph = "";
    public string Subtitle = "";
    public Func<Control> Factory = null!;
}

public sealed class SubTabHost : UserControl
{
    private readonly FlowLayoutPanel _bar = new() { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 0, 0, 8), WrapContents = true };
    private readonly Panel _body = new() { Dock = DockStyle.Fill };
    private readonly List<(Button btn, Func<Control> factory, Control? ctl)> _tabs = new();
    private int _active = -1;

    public SubTabHost()
    {
        BackColor = Theme.Bg;
        Controls.Add(_body);
        Controls.Add(_bar);
    }

    public void Add(string title, Func<Control> factory)
    {
        var b = Theme.FlatButton(title);
        b.Height = 30;
        b.AutoSize = true;
        b.Margin = new Padding(0, 0, 6, 4);
        int idx = _tabs.Count;
        b.Click += (_, _) => Select(idx);
        _bar.Controls.Add(b);
        _tabs.Add((b, factory, null));
    }

    public void Select(int idx)
    {
        if (idx < 0 || idx >= _tabs.Count)
        {
            return;
        }
        var (btn, factory, ctl) = _tabs[idx];
        if (ctl == null)
        {
            ctl = factory();
            ctl.Dock = DockStyle.Fill;
            _body.Controls.Add(ctl);
            _tabs[idx] = (btn, factory, ctl);
        }
        for (int i = 0; i < _tabs.Count; i++)
        {
            bool on = i == idx;
            _tabs[i].btn.BackColor = on ? Theme.Active : Theme.Button;
            _tabs[i].btn.ForeColor = on ? Theme.Gold : Theme.Text;
            _tabs[i].btn.FlatAppearance.BorderColor = on ? Theme.Gold : Theme.Line;
            if (_tabs[i].ctl != null)
            {
                _tabs[i].ctl!.Visible = on;
            }
        }
        ctl.BringToFront();
        _active = idx;
    }

    public Control? ActiveControl_ => _active >= 0 ? _tabs[_active].ctl : null;
}

public sealed class MainForm : Form
{
    private const string Version = AppInfo.Version;

    private readonly Settings _settings = Settings.Load();
    private readonly Panel _sidebar = new() { Dock = DockStyle.Left, Width = 232, BackColor = Theme.Sidebar };
    private readonly Panel _nav = new() { Dock = DockStyle.Fill, BackColor = Theme.Sidebar, AutoScroll = true };
    private readonly Panel _content = new() { Dock = DockStyle.Fill, BackColor = Theme.Bg };
    private readonly Panel _header = new() { Dock = DockStyle.Top, Height = 104, BackColor = Theme.Bg };
    private readonly Label _title = new() { AutoSize = true, Font = Theme.Heading, ForeColor = Theme.Text, Location = new Point(26, 24), BackColor = Color.Transparent };
    private readonly Label _subtitle = new() { AutoSize = true, Font = Theme.Ui, ForeColor = Theme.Muted, Location = new Point(28, 66), BackColor = Color.Transparent };
    private readonly FlowLayoutPanel _actions = new() { Dock = DockStyle.Right, Width = 700, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 22, 26, 0), BackColor = Theme.Bg };
    private readonly Panel _body = new() { Dock = DockStyle.Fill, BackColor = Theme.Bg, Padding = new Padding(26, 0, 26, 12) };
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 26, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Theme.Muted, Padding = new Padding(26, 0, 0, 0), BackColor = Theme.Bg };

    private readonly List<PageDef> _pages = new();
    private readonly Dictionary<string, NavButton> _navButtons = new();
    private readonly Dictionary<string, Control> _pageControls = new();
    private readonly List<(string page, string text, int key)> _history = new();
    private string _current = "";
    private Context? _ctx;
    private BodyAnimProvider? _monster;
    private BodyAnimProvider? _itemAnim;
    private TiledataProvider? _tiledata;
    private Control? _overview;

    public MainForm()
    {
        Text = Loc.F("UO Tinker Version {0} - F1 für Hilfe", Version);
        Width = 1560;
        Height = 920;
        MinimumSize = new Size(1100, 700);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        Font = Theme.Ui;
        KeyPreview = true;

        BuildSidebar();

        _header.Controls.Add(_actions);
        _header.Controls.Add(_subtitle);
        _header.Controls.Add(_title);
        _content.Controls.Add(_body);
        _content.Controls.Add(_status);
        _content.Controls.Add(_header);
        Controls.Add(_content);
        Controls.Add(_sidebar);

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.F1)
            {
                ShowHelp();
            }
        };
        HandleCreated += (_, _) => Theme.DarkTitleBar(this);
        FormClosing += (_, e) =>
        {
            if (!ConfirmDiscard())
            {
                e.Cancel = true;
            }
        };
        Shown += (_, _) =>
        {
            LoadAll();
            if (_settings.CheckUpdates)
            {
                _ = CheckForUpdatesAsync(false);
            }
        };
    }

    private void BuildSidebar()
    {
        var logo = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = Theme.Sidebar };
        logo.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var gold = new SolidBrush(Theme.Gold);
            using var dark = new SolidBrush(Theme.Sidebar);
            using var ring = new Pen(Theme.Gold, 2);
            var r = new Rectangle(16, 24, 28, 28);
            g.DrawEllipse(ring, r);
            g.FillPolygon(gold, new[] { new Point(30, 29), new Point(38, 38), new Point(30, 47), new Point(22, 38) });
            g.FillPolygon(dark, new[] { new Point(30, 33), new Point(34, 38), new Point(30, 43), new Point(26, 38) });
            TextRenderer.DrawText(g, "UO Tinker", new Font("Georgia", 12.5f, FontStyle.Bold), new Point(54, 26), Theme.Text);
            using var pen = new Pen(Theme.Line);
            g.DrawLine(pen, 0, logo.Height - 1, logo.Width, logo.Height - 1);
        };

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 54, BackColor = Theme.Sidebar };
        bottom.Paint += (_, e) =>
        {
            using var pen = new Pen(Theme.Line);
            e.Graphics.DrawLine(pen, 0, 0, bottom.Width, 0);
        };
        var icons = new (string glyph, string tip, Action act)[]
        {
            ("⚙", Loc.T("Ordner wählen"), () => ShowFolderMenu()),
            ("↻", Loc.T("Neu laden"), () => LoadAll()),
            ("?", Loc.T("Hilfe (F1)"), () => ShowHelp()),
            ("ⓘ", Loc.T("Über UOTinker"), () =>
            {
                using var about = new AboutForm();
                about.ShowDialog(this);
            }),
            ("♥", Loc.T("UOTinker unterstützen (Ko-fi)"), () => AppInfo.OpenDonate()),
            ("⏻", Loc.T("Beenden"), () => Close()),
        };
        var tt = new ToolTip();
        for (int i = 0; i < icons.Length; i++)
        {
            var (glyph, tip, act) = icons[i];
            var l = new Label
            {
                Text = glyph, Font = new Font("Segoe UI Symbol", 12f), ForeColor = Theme.Muted, BackColor = Theme.Sidebar,
                Size = new Size(38, 54), Location = new Point(i * 38, 0), TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand,
            };
            l.MouseEnter += (_, _) => l.ForeColor = Theme.Gold;
            l.MouseLeave += (_, _) => l.ForeColor = Theme.Muted;
            l.Click += (_, _) => act();
            tt.SetToolTip(l, tip);
            bottom.Controls.Add(l);
        }

        var edge = new Panel { Dock = DockStyle.Right, Width = 1, BackColor = Theme.Line };
        _sidebar.Controls.Add(_nav);
        _sidebar.Controls.Add(bottom);
        _sidebar.Controls.Add(logo);
        _sidebar.Controls.Add(edge);
    }

    private void ShowFolderMenu()
    {
        var m = new ContextMenuStrip { BackColor = Theme.Card, ForeColor = Theme.Text, ShowImageMargin = false };
        m.Items.Add(Loc.T("Datenordner wählen ..."), null, (_, _) => ChooseFolder(true));
        m.Items.Add(Loc.T("Sphere-Skriptordner wählen (optional) ..."), null, (_, _) => ChooseFolder(false));
        m.Items.Add(Loc.T("Sphere-Skriptordner nicht verwenden"), null, (_, _) =>
        {
            _settings.SphereScripts = "";
            _settings.Save();
            LoadAll();
        });
        m.Items.Add(new ToolStripSeparator());
        var ro = new ToolStripMenuItem(Loc.T("Schreibschutz (nichts ändern)")) { Checked = _settings.ReadOnly, CheckOnClick = true };
        ro.CheckedChanged += (_, _) =>
        {
            _settings.ReadOnly = ro.Checked;
            Settings.IsReadOnly = ro.Checked;
            _settings.Save();
            _status.Text = ro.Checked ? Loc.T("Schreibschutz aktiv: Es wird nichts geändert.") : Loc.T("Schreibschutz aus: Änderungen können gespeichert werden (mit Sicherung).");
        };
        m.Items.Add(ro);
        m.Items.Add(new ToolStripSeparator());
        m.Items.Add(Loc.T("Jetzt nach Updates suchen"), null, async (_, _) => await CheckForUpdatesAsync(true));
        var auto = new ToolStripMenuItem(Loc.T("Beim Start nach Updates suchen")) { Checked = _settings.CheckUpdates, CheckOnClick = true };
        auto.CheckedChanged += (_, _) =>
        {
            _settings.CheckUpdates = auto.Checked;
            _settings.Save();
        };
        m.Items.Add(auto);
        m.Items.Add(new ToolStripSeparator());
        var lang = new ToolStripMenuItem("Sprache / Language");
        foreach (var (code, name) in new[] { ("de", "Deutsch"), ("en", "English") })
        {
            string c = code;
            var item = new ToolStripMenuItem(name) { Checked = (c == "en") == Loc.English };
            item.Click += (_, _) =>
            {
                _settings.Language = c;
                _settings.Save();
                MessageBox.Show(this, Loc.English != (c == "en") ? "Die Sprache wird nach einem Neustart von UOTinker angewendet.\nThe language is applied after restarting UOTinker." : "Sprache gespeichert.\nLanguage saved.", "UO Tinker", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            lang.DropDownItems.Add(item);
        }
        m.Items.Add(lang);
        m.Show(Cursor.Position);
    }

    private void ShowHelp()
    {
        if (_help == null || _help.IsDisposed)
        {
            _help = new HelpForm();
            _help.Show(this);
        }
        _help.Open(_current is "" or "overview" ? "welcome" : _current);
        _help.Activate();
    }

    private HelpForm? _help;
    private UpdateInfo? _update;
    private bool _skipConfirm;

    private async Task CheckForUpdatesAsync(bool manual)
    {
        UpdateInfo? info;
        try
        {
            info = await Updater.CheckAsync();
        }
        catch (Exception ex)
        {
            if (manual)
            {
                _status.Text = Loc.F("Die Update-Prüfung ist fehlgeschlagen: {0}", ex.Message);
            }
            return;
        }
        _update = info;
        if (info == null)
        {
            if (manual)
            {
                _status.Text = Loc.F("UOTinker {0} ist aktuell.", Updater.Current.ToString(3));
            }
            return;
        }
        _status.Text = Loc.F("Neue Version {0} verfügbar (Übersicht: Jetzt aktualisieren).", info.Version.ToString(3));
        if (_ctx != null && _current == "overview")
        {
            Navigate("overview");
        }
    }

    private async void StartUpdate()
    {
        var info = _update;
        if (info == null)
        {
            return;
        }
        if (MessageBox.Show(this, Loc.F("UOTinker {0} laden und installieren?\nDas Programm wird dazu beendet.", info.Version.ToString(3)), "UO Tinker", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes || !ConfirmDiscard())
        {
            return;
        }
        UseWaitCursor = true;
        try
        {
            var progress = new Progress<int>(p => _status.Text = Loc.F("Update wird geladen ... {0} %", p));
            string path = await Updater.DownloadAsync(info, progress);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            _skipConfirm = true;
            Close();
        }
        catch (Exception ex)
        {
            UseWaitCursor = false;
            _status.Text = Loc.F("Das Update ist fehlgeschlagen: {0}", ex.Message);
        }
    }

    private bool ConfirmDiscard()
    {
        if (_skipConfirm)
        {
            return true;
        }
        int n = (_ctx?.Tile.Dirty.Count ?? 0) + (_ctx?.Art.ArtPendingCount ?? 0) + (_ctx?.Art.PendingGump.Count ?? 0) + (_ctx?.PendingOther ?? 0);
        if (n == 0)
        {
            return true;
        }
        return MessageBox.Show(this, Loc.F("{0} ungespeicherte Änderung(en) gehen verloren. Trotzdem fortfahren?", n), "UO Tinker", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
    }

    private void ChooseFolder(bool data)
    {
        using var dlg = new FolderBrowserDialog { SelectedPath = data ? _settings.DataFolder : _settings.SphereScripts };
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            if (data)
            {
                _settings.DataFolder = dlg.SelectedPath;
            }
            else
            {
                _settings.SphereScripts = dlg.SelectedPath;
            }
            _settings.Save();
            LoadAll();
        }
    }

    private async void LoadAll()
    {
        if (!ConfirmDiscard())
        {
            return;
        }
        if (!Directory.Exists(_settings.DataFolder))
        {
            using var dlg = new FolderBrowserDialog { Description = Loc.T("Ordner mit den Ultima-Online-Dateien wählen (art.mul, tiledata.mul, hues.mul ...)"), UseDescriptionForTitle = true };
            if (dlg.ShowDialog(this) != DialogResult.OK)
            {
                _status.Text = Loc.T("Kein Datenordner gewählt. Zahnrad unten links: Datenordner wählen.");
                return;
            }
            _settings.DataFolder = dlg.SelectedPath;
            _settings.Save();
        }
        _body.Visible = false;
        _status.Text = Loc.F("Lade {0} ...", _settings.DataFolder);
        UseWaitCursor = true;
        foreach (var c in _pageControls.Values)
        {
            c.Dispose();
        }
        _pageControls.Clear();
        _body.Controls.Clear();
        _overview = null;
        _history.Clear();
        _current = "";

        Context ctx;
        try
        {
            ctx = await Task.Run(() =>
            {
                var c = Context.Load(_settings);
                _monster = new BodyAnimProvider(c, BodyMode.Monster);
                _itemAnim = new BodyAnimProvider(c, BodyMode.ItemAnim);
                _tiledata = new TiledataProvider(c);
                return c;
            });
        }
        catch (Exception ex)
        {
            UseWaitCursor = false;
            _status.Text = Loc.F("Fehler beim Laden: {0}", ex.Message);
            return;
        }
        _ctx = ctx;
        UseWaitCursor = false;
        BuildPages(ctx);
        AppNav.Request = (page, key) => BeginInvoke(() => Navigate(page, b => b.JumpTo(key)));
        BuildNav();
        _body.Visible = true;
        string sphereInfo = ctx.Catalog.FileCount > 0 ? Loc.F("   |   Sphere-Skripte: {0} Dateien", ctx.Catalog.FileCount) : "";
        _status.Text = Loc.F("Datenordner: {0}{1}   |   Tiledata: {2}   |   UOP-Einträge: {3}", _settings.DataFolder, sphereInfo, ctx.Tile.Loaded ? Loc.T("ok") : Loc.T("NICHT geladen"), ctx.Anim.Uop.Entries.Count);
        Navigate(StartPage);
        StartPage = "overview";
    }

    public static string StartPage { get; set; } = "overview";

    private void BuildPages(Context ctx)
    {
        _pages.Clear();
        void Add(string key, string group, string title, string glyph, string sub, Func<Control> factory) =>
            _pages.Add(new PageDef { Key = key, Group = group, Title = title, Glyph = glyph, Subtitle = sub, Factory = factory });

        Add("overview", Loc.T("ÜBERSICHT"), Loc.T("Übersicht"), "▦", "", () => BuildOverview(ctx));
        Add("art", Loc.T("GRAFIK"), Loc.T("Items (Art)"), "◈", Loc.T("Alle Item-Grafiken aus art.mul bis zur höchsten ID, auch freie Slots."), () => Browser("art", new ArtProvider(ctx)));
        Add("land", Loc.T("GRAFIK"), Loc.T("Landtiles"), "◢", Loc.T("Bodenkacheln (Index 0 bis 0x3FFF in art.mul)."), () => Browser("land", new LandProvider(ctx)));
        Add("gump", Loc.T("GRAFIK"), "Gumps", "▣", Loc.T("Alle Gumps aus gumpart.mul bis zur höchsten ID, auch freie Slots."), () => Browser("gump", new GumpProvider(ctx)));
        Add("radar", Loc.T("GRAFIK"), "Radarcolor", "◉", Loc.T("Radarfarben je Land- und Item-Grafik (radarcol.mul)."), () => Browser("radar", new RadarProvider(ctx)));
        Add("hues", Loc.T("GRAFIK"), "Hues", "◐", Loc.T("Alle Farben aus hues.mul mit je 32 Farbwerten."), () => Browser("hues", new HuesProvider(ctx)));
        Add("monster", Loc.T("ANIMATIONEN"), "Bodies / Monster", "☠", Loc.T("Alle Bodies mit Quellen aus anim.mul bis anim6.mul, bodyconv.def, body.def und UOP."), () => Browser("monster", _monster!));
        Add("itemanim", Loc.T("ANIMATIONEN"), Loc.T("Item-AnimIDs"), "⚔", Loc.T("Animations-IDs, die Items in der Tiledata benutzen, mit Paperdoll-Gumps."), () => Browser("itemanim", _itemAnim!));
        Add("raw", Loc.T("ANIMATIONEN"), Loc.T("Rohdateien"), "☰", Loc.T("Jede Animationsdatei einzeln, ohne body.def und bodyconv.def."), () =>
        {
            var h = new SubTabHost();
            for (int f = 0; f < 6; f++)
            {
                int file = f;
                if (ctx.Anim.Pos[file] != null)
                {
                    h.Add(AnimStore.FileNames[file] + ".mul", () => Browser("raw", new RawAnimProvider(ctx, file)));
                }
            }
            if (ctx.Anim.Uop.Files.Count > 0)
            {
                h.Add("UOP (" + string.Join(", ", ctx.Anim.Uop.Files.Select(x => x.Replace("AnimationFrame", "").Replace(".uop", ""))) + ")", () => Browser("raw", new RawAnimProvider(ctx, -1)));
            }
            h.Select(0);
            return h;
        });
        Add("tiledata", Loc.T("DATEN"), "Tiledata", "▤", Loc.T("Name, Flags und Eigenschaften aller Land- und Item-Kacheln."), () => Browser("tiledata", _tiledata!));
        Add("itemdef", Loc.T("DATEN"), "Sphere-ITEMDEF", "≣", Loc.T("Alle ITEMDEFs der Sphere-Skripte mit Item-ID, Tiledata-Name und Problemen."), () => Browser("itemdef", new ItemDefProvider(ctx)));
        Add("skills", Loc.T("DATEN"), "Skills", "✦", Loc.T("Skill-Tabelle (skills.idx/skills.mul)."), () => Browser("skills", new SkillsProvider(ctx)));
        Add("cliloc", Loc.T("DATEN"), "Cliloc", "✎", Loc.T("Clientsprache: Textnummern und Texte je Sprache."), () =>
        {
            var h = new SubTabHost();
            foreach (var f in Directory.GetFiles(ctx.Folder, "cliloc.*").Where(x => !x.Contains(".bak", StringComparison.OrdinalIgnoreCase)).OrderBy(x => x))
            {
                string path = f;
                h.Add(Path.GetFileName(f), () => Browser("cliloc", new ClilocProvider(ctx.Cliloc(path))));
            }
            h.Select(0);
            return h;
        });
    }

    private BrowserControl Browser(string pageKey, IProvider p)
    {
        var b = new BrowserControl(p);
        b.Viewed += (text, key) => AddHistory(pageKey, text, key);
        b.GraphicsEdited += () => _tiledata?.RecomputeAll();
        return b;
    }

    private void AddHistory(string page, string text, int key)
    {
        if (text.Length == 0)
        {
            return;
        }
        _history.RemoveAll(h => h.page == page && h.key == key);
        _history.Insert(0, (page, text, key));
        if (_history.Count > 20)
        {
            _history.RemoveAt(_history.Count - 1);
        }
    }

    private void BuildNav()
    {
        _nav.Controls.Clear();
        _navButtons.Clear();
        string group = "";
        var items = new List<Control>();
        foreach (var pg in _pages)
        {
            if (pg.Group != group)
            {
                group = pg.Group;
                items.Add(new Label
                {
                    Text = group, Font = Theme.Small, ForeColor = Theme.Muted, BackColor = Theme.Sidebar, Height = 30,
                    TextAlign = ContentAlignment.BottomLeft, Padding = new Padding(18, 0, 0, 6),
                });
            }
            var nb = new NavButton { Text = pg.Title, Glyph = pg.Glyph, Height = 35 };
            string key = pg.Key;
            nb.Click += (_, _) => Navigate(key);
            _navButtons[key] = nb;
            items.Add(nb);
        }
        for (int i = items.Count - 1; i >= 0; i--)
        {
            items[i].Dock = DockStyle.Top;
            _nav.Controls.Add(items[i]);
        }
    }

    public void Navigate(string key, Action<BrowserControl>? setup = null)
    {
        var pg = _pages.FirstOrDefault(p => p.Key == key);
        if (pg == null)
        {
            return;
        }

        if (key == "overview")
        {
            if (_overview != null && _pageControls.ContainsKey("overview"))
            {
                _body.Controls.Remove(_pageControls["overview"]);
                _pageControls["overview"].Dispose();
                _pageControls.Remove("overview");
            }
        }

        if (!_pageControls.TryGetValue(key, out var ctl))
        {
            UseWaitCursor = true;
            try
            {
                ctl = pg.Factory();
            }
            catch (Exception ex)
            {
                ctl = new TextBox { Multiline = true, ReadOnly = true, Text = Loc.F("Fehler: {0}", ex) };
            }
            finally
            {
                UseWaitCursor = false;
            }
            if (key != "overview")
            {
                var card = new CardPanel { Padding = new Padding(1) };
                ctl.Dock = DockStyle.Fill;
                card.Controls.Add(ctl);
                ctl = card;
            }
            ctl.Dock = DockStyle.Fill;
            _body.Controls.Add(ctl);
            _pageControls[key] = ctl;
        }

        foreach (var c in _pageControls.Values)
        {
            c.Visible = c == ctl;
        }
        ctl.BringToFront();
        _current = key;

        foreach (var (k, nb) in _navButtons)
        {
            nb.IsActive = k == key;
            nb.Invalidate();
        }

        _actions.Controls.Clear();
        if (key == "overview")
        {
            _title.Text = Greeting();
            _subtitle.Text = Loc.T("UO Tinker - Betrachter und Editor für die Dateien deines Ultima-Online-Clients");
            var reload = Theme.FlatButton(Loc.T("Neu laden"), true);
            reload.Width = 190;
            reload.Click += (_, _) => LoadAll();
            var choose = Theme.FlatButton(Loc.T("Datenordner wählen"));
            choose.Width = 190;
            choose.Click += (_, _) => ChooseFolder(true);
            var pill = new Label
            {
                Text = "  ●  " + new DirectoryInfo(_settings.DataFolder).Name, ForeColor = Theme.Text, BackColor = Theme.Active,
                Height = 36, Width = 210, TextAlign = ContentAlignment.MiddleLeft,
            };
            _actions.Controls.Add(reload);
            _actions.Controls.Add(choose);
            _actions.Controls.Add(pill);
        }
        else
        {
            _title.Text = pg.Title;
            _subtitle.Text = pg.Subtitle;
        }

        if (setup != null)
        {
            var b = FindBrowser(ctl);
            if (b != null)
            {
                setup(b);
            }
        }
    }

    private static BrowserControl? FindBrowser(Control c)
    {
        if (c is BrowserControl b)
        {
            return b;
        }
        if (c is SubTabHost sh && sh.ActiveControl_ != null)
        {
            return FindBrowser(sh.ActiveControl_);
        }
        foreach (Control ch in c.Controls)
        {
            var r = FindBrowser(ch);
            if (r != null)
            {
                return r;
            }
        }
        return null;
    }

    private static string Greeting()
    {
        int h = DateTime.Now.Hour;
        return h >= 5 && h < 11 ? Loc.T("Guten Morgen") : h >= 11 && h < 17 ? Loc.T("Guten Tag") : Loc.T("Guten Abend");
    }

    private Control BuildOverview(Context ctx)
    {
        int artUsed = Enumerable.Range(0, ctx.Art.StaticCount).Count(i => ctx.Art.StaticValid(i));
        int gumpUsed = Enumerable.Range(0, ctx.Art.GumpIdx.Count).Count(i => ctx.Art.GumpIdx.Valid(i));
        var mon = _monster!;

        var root = new Panel { BackColor = Theme.Bg };
        var td = _tiledata!;
        int gaps = td.EntriesWithProblem(true);
        var gapCounts = td.ProblemCounts(true);
        var cards = new TableLayoutPanel { Dock = DockStyle.Top, Height = 150, ColumnCount = 6, RowCount = 1, BackColor = Theme.Bg, Padding = new Padding(0, 0, 0, 0) };
        for (int i = 0; i < 6; i++)
        {
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 6));
        }
        cards.Controls.Add(StatCard(Loc.T("ITEM-ART"), artUsed.ToString("N0"), Loc.F("belegt von {0:N0} Slots, {1:N0} frei", ctx.Art.StaticCount, ctx.Art.StaticCount - artUsed), Theme.Gold), 0, 0);
        cards.Controls.Add(StatCard("GUMPS", gumpUsed.ToString("N0"), Loc.F("belegt von {0:N0} Slots, {1:N0} frei", ctx.Art.GumpIdx.Count, ctx.Art.GumpIdx.Count - gumpUsed), Theme.Gold), 1, 0);
        cards.Controls.Add(StatCard(Loc.T("ANIMATIONEN"), mon.UsedCount.ToString("N0"), Loc.F("Bodies mit Animation, davon {0:N0} nur in UOP", mon.UopOnlyCount), Theme.Gold), 2, 0);
        cards.Controls.Add(StatCard(Loc.T("OHNE ANIMATION"), mon.MissingCount.ToString("N0"), Loc.T("Chardefs ohne Animationsdaten"), mon.MissingCount > 0 ? Theme.Red : Theme.Green), 3, 0);
        cards.Controls.Add(StatCard(Loc.T("ITEMS MIT LÜCKEN"), gaps.ToString("N0"), Loc.F("{0:N0} ohne Tiledata, {1:N0} ohne Art, {2:N0} Anim (Klick)", gapCounts[0], gapCounts[1], gapCounts[2] + gapCounts[3] + gapCounts[4]), gaps > 0 ? Theme.Red : Theme.Green,
            () => Navigate("tiledata", b => { b.ResetFilters(); b.SetFilter(Loc.T("Typ"), 2); b.SetFilter(Loc.T("Problem"), 1); })), 4, 0);
        cards.Controls.Add(StatCard(Loc.T("SPHERE-KATALOG"), ctx.Catalog.Chars.Count.ToString("N0"), Loc.F("Body-Nummern mit CHARDEF, {0:N0} mit ITEMDEF", ctx.Catalog.Items.Count), Theme.Gold), 5, 0);

        var lower = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Theme.Bg, Padding = new Padding(0, 16, 0, 0) };
        lower.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        lower.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        lower.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));

        var files = SimpleList(new[] { Loc.T("Datei"), Loc.T("Größe"), Loc.T("Inhalt") }, new[] { 150, 80, 200 });
        foreach (var (name, info) in FileInfos(ctx))
        {
            string path = Path.Combine(ctx.Folder, name);
            string size = File.Exists(path) ? FormatSize(new FileInfo(path).Length) : Loc.T("fehlt");
            files.Items.Add(new ListViewItem(new[] { name, size, info }));
        }
        lower.Controls.Add(Panel_(Loc.T("Dateien im Datenordner"), files), 0, 0);

        var hist = SimpleList(new[] { Loc.T("Zuletzt angesehen") }, new[] { 380 });
        foreach (var h in _history)
        {
            var pg = _pages.First(p => p.Key == h.page);
            hist.Items.Add(new ListViewItem(Loc.F("{0}   ·   {1}", h.text, pg.Title)) { Tag = h });
        }
        hist.DoubleClick += (_, _) =>
        {
            if (hist.SelectedItems.Count > 0 && hist.SelectedItems[0].Tag is ValueTuple<string, string, int> h)
            {
                Navigate(h.Item1, b => b.GoToKey(h.Item3));
            }
        };
        lower.Controls.Add(Panel_(Loc.T("Zuletzt angesehen"), hist, Loc.T("Doppelklick öffnet den Eintrag erneut.")), 1, 0);

        var quick = SimpleList(new[] { Loc.T("Schnellaktionen") }, new[] { 380 });
        var gapActions = new List<(string text, Action act)>();
        var gapLabels = TiledataProvider.ProblemLabels;
        for (int k = 0; k < gapLabels.Count; k++)
        {
            int opt = 3 + k;
            if (gapCounts[k] > 0)
            {
                gapActions.Add((Loc.F("Tiledata: {0} ({1:N0})", gapLabels[k], gapCounts[k]), () => Navigate("tiledata", b => { b.ResetFilters(); b.SetFilter(Loc.T("Typ"), 2); b.SetFilter(Loc.T("Problem"), opt); })));
            }
        }
        var actions = gapActions.Concat(new (string text, Action act)[]
        {
            (Loc.T("Chardefs ohne Animation anzeigen (Bodies)"), () => Navigate("monster", b => { b.ResetFilters(); b.SetFilter(Loc.T("Status"), 3); })),
            (Loc.T("Nur UOP-Animationen anzeigen (Bodies)"), () => Navigate("monster", b => { b.ResetFilters(); b.SetFilter(Loc.T("Quelle"), 2); })),
            (Loc.T("Bodies ohne Chardef anzeigen (Animation vorhanden)"), () => Navigate("monster", b => { b.ResetFilters(); b.SetFilter(Loc.T("Chardef"), 2); b.SetFilter(Loc.T("Status"), 1); })),
            (Loc.T("Item-AnimIDs ohne Animation"), () => Navigate("itemanim", b => { b.ResetFilters(); b.SetFilter(Loc.T("Status"), 2); })),
            (Loc.T("Freie Item-Art-Slots"), () => Navigate("art", b => { b.ResetFilters(); b.SetFilter(Loc.T("Status"), 2); })),
            (Loc.T("Freie Gump-Slots"), () => Navigate("gump", b => { b.ResetFilters(); b.SetFilter(Loc.T("Status"), 2); })),
            (Loc.T("Leere Hues"), () => Navigate("hues", b => { b.ResetFilters(); b.SetFilter(Loc.T("Eintrag"), 1); })),
        }).ToArray();
        foreach (var a in actions)
        {
            quick.Items.Add(new ListViewItem(a.text) { Tag = a.act });
        }
        quick.DoubleClick += (_, _) =>
        {
            if (quick.SelectedItems.Count > 0 && quick.SelectedItems[0].Tag is Action act)
            {
                act();
            }
        };
        lower.Controls.Add(Panel_(Loc.T("Schnellaktionen"), quick, Loc.T("Doppelklick führt aus.")), 2, 0);

        root.Controls.Add(lower);
        root.Controls.Add(cards);
        if (_update != null)
        {
            var banner = new Panel { Dock = DockStyle.Top, Height = 62, Padding = new Padding(0, 0, 0, 12), BackColor = Theme.Bg };
            var inner = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Active, Padding = new Padding(16, 0, 8, 0) };
            var go = Theme.FlatButton(Loc.T("Jetzt aktualisieren"), true);
            go.Dock = DockStyle.Right;
            go.Width = 220;
            go.Click += (_, _) => StartUpdate();
            var text = new Label
            {
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Theme.Gold, Font = Theme.UiBold,
                Text = Loc.F("Neue Version {0} verfügbar (installiert: {1})", _update.Version.ToString(3), Updater.Current.ToString(3)),
            };
            inner.Controls.Add(text);
            inner.Controls.Add(go);
            banner.Controls.Add(inner);
            root.Controls.Add(banner);
        }
        Theme.Style(root);
        return root;
    }

    private static IEnumerable<(string, string)> FileInfos(Context c)
    {
        yield return ("art.mul", Loc.F("{0:N0} Index-Einträge (Land + Items)", c.Art.ArtIdx.Count));
        yield return ("gumpart.mul", Loc.F("{0:N0} Gump-Slots", c.Art.GumpIdx.Count));
        yield return ("tiledata.mul", Loc.F("{0:N0} Land + {1:N0} Items", c.Tile.LandCount, c.Tile.ItemCount));
        yield return ("hues.mul", Loc.F("{0:N0} Hues", c.Hues.Count));
        yield return ("radarcol.mul", Loc.T("Radarfarben"));
        yield return ("skills.mul", Loc.F("{0} Skills", c.Skills.Count));
        for (int f = 0; f < 6; f++)
        {
            if (c.Anim.Pos[f] != null)
            {
                yield return (AnimStore.FileNames[f] + ".mul", Loc.F("bis Body/Index {0}", c.Anim.MaxId(f)));
            }
        }
        foreach (var u in c.Anim.Uop.Files)
        {
            yield return (u, Loc.T("UOP-Animation"));
        }
        yield return ("bodyconv.def", Loc.F("{0} Einträge", c.Anim.BodyConv.Count));
        yield return ("body.def", Loc.F("{0} Einträge", c.Anim.BodyDef.Count));
        yield return ("mobtypes.txt", Loc.F("{0} Einträge", c.Anim.MobTypes.Count));
    }

    private static string FormatSize(long b) => b >= 1 << 30 ? $"{b / (double)(1 << 30):F1} GB" : b >= 1 << 20 ? $"{b / (double)(1 << 20):F1} MB" : $"{b / 1024.0:F0} KB";

    private static Control StatCard(string caption, string value, string sub, Color valueColor, Action? click = null)
    {
        var card = new CardPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 10, 0), Padding = new Padding(18, 14, 14, 14) };
        var cap = new Label { Text = caption, Font = Theme.Ui, ForeColor = Theme.Muted, Dock = DockStyle.Top, Height = 26, BackColor = Color.Transparent };
        var subl = new Label { Text = sub, Font = new Font("Segoe UI", 8.25f), ForeColor = Theme.Muted, Dock = DockStyle.Bottom, Height = 36, BackColor = Color.Transparent };
        var val = new Label { Text = value, Font = Theme.Number, ForeColor = valueColor, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent };
        card.Controls.Add(val);
        card.Controls.Add(subl);
        card.Controls.Add(cap);
        if (click != null)
        {
            foreach (Control c in new Control[] { card, cap, subl, val })
            {
                c.Cursor = Cursors.Hand;
                c.Click += (_, _) => click();
            }
        }
        return card;
    }

    private static Control Panel_(string title, Control inner, string? footer = null)
    {
        var card = new CardPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 10, 0), Padding = new Padding(16, 14, 16, 12) };
        var t = new Label { Text = title, Font = Theme.UiBold, ForeColor = Theme.Text, Dock = DockStyle.Top, Height = 28, BackColor = Color.Transparent };
        inner.Dock = DockStyle.Fill;
        card.Controls.Add(inner);
        if (footer != null)
        {
            card.Controls.Add(new Label { Text = footer, ForeColor = Theme.Muted, Dock = DockStyle.Bottom, Height = 24, BackColor = Color.Transparent });
        }
        card.Controls.Add(t);
        return card;
    }

    private static ListView SimpleList(string[] cols, int[] widths)
    {
        var lv = new ListView
        {
            Name = "simple", View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = false, OwnerDraw = true,
            HeaderStyle = ColumnHeaderStyle.None,
            BackColor = Theme.Card, ForeColor = Theme.Text, BorderStyle = BorderStyle.None,
        };
        for (int i = 0; i < cols.Length; i++)
        {
            lv.Columns.Add(cols[i], widths[i]);
        }
        lv.DrawColumnHeader += (_, e) =>
        {
            using var b = new SolidBrush(Theme.Card);
            e.Graphics.FillRectangle(b, e.Bounds);
            TextRenderer.DrawText(e.Graphics, e.Header?.Text ?? "", Theme.UiBold, new Rectangle(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 4, e.Bounds.Height), Theme.Muted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
        };
        lv.DrawItem += (_, e) => e.DrawDefault = false;
        lv.DrawSubItem += (_, e) =>
        {
            bool sel = e.Item != null && e.Item.Selected;
            using (var b = new SolidBrush(sel ? Theme.GoldDark : Theme.Card))
            {
                e.Graphics.FillRectangle(b, e.Bounds);
            }
            TextRenderer.DrawText(e.Graphics, e.SubItem?.Text ?? "", Theme.Ui, new Rectangle(e.Bounds.X + 4, e.Bounds.Y, e.Bounds.Width - 4, e.Bounds.Height),
                sel ? Theme.Gold : (e.ColumnIndex == 0 ? Theme.Text : Theme.Muted),
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        };
        lv.Resize += (_, _) =>
        {
            int used = 0;
            for (int i = 0; i < lv.Columns.Count - 1; i++)
            {
                used += lv.Columns[i].Width;
            }
            lv.Columns[lv.Columns.Count - 1].Width = Math.Max(60, lv.ClientSize.Width - used);
        };
        return lv;
    }
}

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Loc.Init(Settings.Load().Language);
        if (args.Length > 1 && args[0] == "--selftest")
        {
            File.WriteAllText(args[1], SelfTest.Run());
            return;
        }
        int pi = Array.IndexOf(args, "--page");
        if (pi >= 0 && pi + 1 < args.Length)
        {
            MainForm.StartPage = args[pi + 1];
        }
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}

