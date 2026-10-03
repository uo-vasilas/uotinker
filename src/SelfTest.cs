using System.Diagnostics;
using System.Drawing.Imaging;
using System.Text;

namespace UOTinker;

public static class SelfTest
{
    public static string Run()
    {
        var sb = new StringBuilder();
        var sw = Stopwatch.StartNew();
        var ctx = Context.Load(Settings.Load());
        sb.AppendLine($"Kontext geladen in {sw.ElapsedMilliseconds} ms, Ordner {ctx.Folder}");
        sb.AppendLine($"Sphere: {ctx.Catalog.FileCount} Dateien, Chars {ctx.Catalog.Chars.Count}, Items {ctx.Catalog.Items.Count}");
        sb.AppendLine($"Tiledata: loaded={ctx.Tile.Loaded} old={ctx.Tile.IsOld} land={ctx.Tile.LandCount} items={ctx.Tile.ItemCount}");
        sb.AppendLine($"Art: idx={ctx.Art.ArtIdx.Count} statics={ctx.Art.StaticCount}; Gump idx={ctx.Art.GumpIdx.Count}; Hues={ctx.Hues.Count}; Skills={ctx.Skills.Count}");
        sb.AppendLine($"UOP-Dateien: {string.Join(", ", ctx.Anim.Uop.Files)}  Eintraege {ctx.Anim.Uop.Entries.Count}");
        for (int f = 0; f < 6; f++)
        {
            sb.AppendLine($"  {AnimStore.FileNames[f]}: MaxId {ctx.Anim.MaxId(f)}");
        }

        string outDir = Path.Combine(Path.GetTempPath(), "uotinker_selftest");
        Directory.CreateDirectory(outDir);

        foreach (int body in new[] { 12, 197, 198, 829, 692, 666, 23, 705, 320 })
        {
            var srcs = ctx.Anim.SourcesFor(body);
            sb.AppendLine($"Body {body}: {srcs.Count} Quellen; Chardef: {ctx.Catalog.CharNames(body)}");
            foreach (var s in srcs)
            {
                int act = s.Actions[0];
                var frames = ctx.Anim.Decode(s, act, 0, false, out string err);
                int ok = frames.Count(f => f.Bmp != null);
                sb.AppendLine($"   - {s.Label}: Aktionen {s.Actions.Length}, Aktion {act} Richtung 0 -> {frames.Count} Frames ({ok} mit Bild) {err}");
                var first = frames.FirstOrDefault(f => f.Bmp != null);
                if (first?.Bmp != null)
                {
                    first.Bmp.Save(Path.Combine(outDir, $"anim_{body}_{s.Kind}_{s.File}.png"), ImageFormat.Png);
                }
            }
        }

        var art = ctx.Art.GetStatic(0x0F60, out string e1);
        sb.AppendLine($"Art 0x0F60: {(art == null ? e1 : art.Width + "x" + art.Height)}");
        art?.Save(Path.Combine(outDir, "art_0F60.png"), ImageFormat.Png);
        var land = ctx.Art.GetLand(3, out string e2);
        sb.AppendLine($"Land 3: {(land == null ? e2 : land.Width + "x" + land.Height)}");
        land?.Save(Path.Combine(outDir, "land_3.png"), ImageFormat.Png);
        var gump = ctx.Art.GetGump(0xC350 / 1 - 50000 + 50000, out string e3);
        sb.AppendLine($"Gump 50000: {(gump == null ? e3 : gump.Width + "x" + gump.Height)}");
        gump?.Save(Path.Combine(outDir, "gump_50000.png"), ImageFormat.Png);

        int freeArt = Enumerable.Range(0, ctx.Art.StaticCount).Count(i => !ctx.Art.StaticValid(i));
        int freeGump = Enumerable.Range(0, ctx.Art.GumpIdx.Count).Count(i => !ctx.Art.GumpIdx.Valid(i));
        sb.AppendLine($"Art frei {freeArt}/{ctx.Art.StaticCount}, Gumps frei {freeGump}/{ctx.Art.GumpIdx.Count}");

        sb.AppendLine($"Hue 1: '{ctx.Hues.Names[1]}'  Skill 0: '{ctx.Skills.Names[0]}'");
        foreach (var f in Directory.GetFiles(ctx.Folder, "cliloc.*").Where(x => !x.Contains(".bak")).OrderBy(x => x))
        {
            var cl = new ClilocData(f);
            sb.AppendLine($"Cliloc {Path.GetFileName(f)}: {cl.Numbers.Length} Eintraege, Komprimiert {cl.Compressed}, Fehler '{cl.Error}', erster: {(cl.Numbers.Length > 0 ? cl.Numbers[0] + " = " + cl.Texts[0] : "")}");
        }

        foreach (var (name, prov, start) in new (string, IThumbProvider, int)[]
                 {
                     ("art", new ArtProvider(ctx), 0x0F40), ("gump", new GumpProvider(ctx), 0), ("land", new LandProvider(ctx), 0),
                     ("radar", new RadarProvider(ctx), 0x4000 + 0x0F40), ("hues", new HuesProvider(ctx), 0), ("tiledata", new TiledataProvider(ctx), ctx.Tile.LandCount + 0x0F40),
                 })
        {
            var grid = new ThumbGrid { Width = 900, Height = 520, Loader = prov.Thumb, IsFree = ((IProvider)prov).IsFree, LabelOf = prov.ThumbLabel };
            var view = Enumerable.Range(start, 400).ToList();
            var form = new Form();
            form.Controls.Add(grid);
            grid.CreateControl();
            grid.SetView(view);
            using var bmp = new Bitmap(900, 520);
            grid.DrawToBitmap(bmp, new Rectangle(0, 0, 900, 520));
            bmp.Save(Path.Combine(outDir, $"grid_{name}.png"), ImageFormat.Png);
            sb.AppendLine($"Raster {name} gerendert");
        }

        var tdp = new TiledataProvider(ctx);
        sb.AppendLine($"Tiledata: {tdp.Summary}");

        string tmp = Path.Combine(Path.GetTempPath(), "uotinker_savetest");
        if (Directory.Exists(tmp))
        {
            Directory.Delete(tmp, true);
        }
        Directory.CreateDirectory(tmp);
        File.Copy(Path.Combine(ctx.Folder, "tiledata.mul"), Path.Combine(tmp, "tiledata.mul"));
        var orig = File.ReadAllBytes(Path.Combine(tmp, "tiledata.mul"));
        var td = new TileData(tmp);
        int id = 0x0F43;
        td.ItemName[id] = "Testaxt";
        td.Weight[id] = 7;
        td.AnimId[id] = 1234;
        td.ItemFlags[id] |= 0x400000UL;
        td.LandName[5] = "TestLand";
        td.Dirty.Add(td.LandCount + id);
        td.Dirty.Add(5);
        string? saveErr = td.Save(Path.Combine(tmp, "bk"), out string bk);
        var after = File.ReadAllBytes(Path.Combine(tmp, "tiledata.mul"));
        var td2 = new TileData(tmp);
        int diff = 0;
        for (int k = 0; k < orig.Length; k++)
        {
            if (orig[k] != after[k])
            {
                diff++;
            }
        }
        sb.AppendLine($"Speichertest: Fehler={saveErr ?? "keiner"}, Groesse gleich={orig.Length == after.Length}, geaenderte Bytes={diff}, Name='{td2.ItemName[id]}', Gewicht={td2.Weight[id]}, Anim={td2.AnimId[id]}, Wearable={(td2.ItemFlags[id] & 0x400000UL) != 0}, Land='{td2.LandName[5]}', Backup gleich={File.ReadAllBytes(bk).SequenceEqual(orig)}, Dirty danach={td.Dirty.Count}");
        td.ItemName[id] = "xx";
        td.Dirty.Add(td.LandCount + id);
        td.Revert(td.LandCount + id);
        sb.AppendLine($"Revert: Name='{td.ItemName[id]}'");

        string atmp = Path.Combine(Path.GetTempPath(), "uotinker_arttest");
        if (Directory.Exists(atmp))
        {
            Directory.Delete(atmp, true);
        }
        Directory.CreateDirectory(atmp);
        File.Copy(Path.Combine(ctx.Folder, "artidx.mul"), Path.Combine(atmp, "artidx.mul"));
        File.Copy(Path.Combine(ctx.Folder, "art.mul"), Path.Combine(atmp, "art.mul"));
        var origIdx = File.ReadAllBytes(Path.Combine(atmp, "artidx.mul"));
        long origArtLen = new FileInfo(Path.Combine(atmp, "art.mul")).Length;
        string artReport;
        {
            var a1 = new ArtStore(atmp);
            using var srcBmp = a1.GetStatic(0x0F43, out _)!;
            var enc = ArtStore.EncodeStatic(srcBmp, out string encErr);
            int freeId = 0x48E4;
            a1.SetStatic(freeId, enc);
            a1.SetStatic(0x0F44, null);
            bool validPending = a1.StaticValid(freeId) && !a1.StaticValid(0x0F44);
            string? aerr = a1.SaveArt(Path.Combine(atmp, "bk"), out string abk);
            var a2 = new ArtStore(atmp);
            using var back = a2.GetStatic(freeId, out string gerr);
            int pdiff = -1;
            if (back != null && back.Width == srcBmp.Width && back.Height == srcBmp.Height)
            {
                pdiff = 0;
                for (int yy = 0; yy < back.Height; yy++)
                {
                    for (int xx = 0; xx < back.Width; xx++)
                    {
                        if (back.GetPixel(xx, yy).ToArgb() != srcBmp.GetPixel(xx, yy).ToArgb())
                        {
                            pdiff++;
                        }
                    }
                }
            }
            var newIdx = File.ReadAllBytes(Path.Combine(atmp, "artidx.mul"));
            int idxDiffEntries = 0;
            for (int e = 0; e < origIdx.Length / 12; e++)
            {
                if (!origIdx.AsSpan(e * 12, 12).SequenceEqual(newIdx.AsSpan(e * 12, 12)))
                {
                    idxDiffEntries++;
                }
            }
            bool prefixSame;
            using (var f1 = File.OpenRead(Path.Combine(ctx.Folder, "art.mul")))
            using (var f2 = File.OpenRead(Path.Combine(atmp, "art.mul")))
            {
                prefixSame = true;
                var b1 = new byte[1 << 20];
                var b2 = new byte[1 << 20];
                long left = origArtLen;
                while (left > 0 && prefixSame)
                {
                    int n = (int)Math.Min(left, b1.Length);
                    f1.ReadExactly(b1, 0, n);
                    f2.ReadExactly(b2, 0, n);
                    prefixSame = b1.AsSpan(0, n).SequenceEqual(b2.AsSpan(0, n));
                    left -= n;
                }
            }
            long grown = new FileInfo(Path.Combine(atmp, "art.mul")).Length - origArtLen;
            artReport = $"Arttest: Kodierfehler='{encErr}', Pending ok={validPending}, Speicherfehler={aerr ?? "keiner"}, Rueckleseproblem='{gerr}', Pixelabweichungen={pdiff} ({srcBmp.Width}x{srcBmp.Height}), geaenderte Index-Eintraege={idxDiffEntries} (erwartet 2), Originalteil von art.mul unveraendert={prefixSame}, art.mul gewachsen um {grown} Bytes (Daten {enc?.Length}), Idx-Backup vorhanden={File.Exists(abk)}, Slot 0xF44 danach valid={a2.StaticValid(0x0F44)}";
        }
        sb.AppendLine(artReport);

        static int PixDiff(Bitmap a, Bitmap b)
        {
            if (a.Width != b.Width || a.Height != b.Height)
            {
                return -1;
            }
            int d = 0;
            for (int yy = 0; yy < a.Height; yy++)
            {
                for (int xx = 0; xx < a.Width; xx++)
                {
                    if (a.GetPixel(xx, yy).ToArgb() != b.GetPixel(xx, yy).ToArgb())
                    {
                        d++;
                    }
                }
            }
            return d;
        }

        {
            var l1 = new ArtStore(atmp);
            using var landSrc = l1.GetLand(4, out _)!;
            var landEnc = ArtStore.EncodeLand(landSrc, out string landErr);
            l1.SetLand(0x1B00, landEnc);
            string? lerr = l1.SaveArt(Path.Combine(atmp, "bk"), out _);
            var l2 = new ArtStore(atmp);
            using var landBack = l2.GetLand(0x1B00, out string lberr);
            var gsrcDir = ctx.Folder;
            File.Copy(Path.Combine(gsrcDir, "gumpidx.mul"), Path.Combine(atmp, "gumpidx.mul"), true);
            File.Copy(Path.Combine(gsrcDir, "gumpart.mul"), Path.Combine(atmp, "gumpart.mul"), true);
            var origGidx = File.ReadAllBytes(Path.Combine(gsrcDir, "gumpidx.mul"));
            var g1 = new ArtStore(atmp);
            using var gSrc = g1.GetGump(1, out _)!;
            using var pdSrc = g1.GetGump(50500, out _)!;
            var gEnc = ArtStore.EncodeGump(gSrc, out string gErr);
            var pdEnc = ArtStore.EncodeGump(pdSrc, out _);
            g1.SetGump(0x1D, gEnc, gSrc.Width, gSrc.Height);
            g1.SetGump(0x21, pdEnc, pdSrc.Width, pdSrc.Height);
            g1.SetGump(0x22, null, 0, 0);
            string? gerr2 = g1.SaveGumps(Path.Combine(atmp, "bk"), out _);
            var g2 = new ArtStore(atmp);
            using var gBack = g2.GetGump(0x1D, out string gberr);
            using var pdBack = g2.GetGump(0x21, out _);
            var newGidx = File.ReadAllBytes(Path.Combine(atmp, "gumpidx.mul"));
            int gdiff = 0;
            for (int e = 0; e < origGidx.Length / 12; e++)
            {
                if (!origGidx.AsSpan(e * 12, 12).SequenceEqual(newGidx.AsSpan(e * 12, 12)))
                {
                    gdiff++;
                }
            }
            sb.AppendLine($"Landtest: Fehler='{landErr}{lerr}{lberr}', Pixelabweichungen={(landBack == null ? -2 : PixDiff(landSrc, landBack))}");
            sb.AppendLine($"Gumptest: Groesse Gump1={g1.GumpSize(1)}, Paperdoll 50500={g1.GumpSize(50500)}, Fehler='{gErr}{gerr2}{gberr}', Abweichungen Gump={(gBack == null ? -2 : PixDiff(gSrc, gBack))}, Paperdoll={(pdBack == null ? -2 : PixDiff(pdSrc, pdBack))}, geaenderte Index-Eintraege={gdiff} (erwartet 3), 0x22 danach valid={g2.GumpValid(0x22)}, 0x1D size={g2.GumpSize(0x1D)}, Datenlaenge neu/alt={gEnc?.Length}/{ctx.Art.GumpLength(1)}");
        }

        int srcId = 0x0F45;
        td.CopyParts(td.LandCount + srcId, td.LandCount + 0x0F44, TileParts.AllItem & ~TileParts.Name);
        sb.AppendLine($"Kopiertest: Ziel Gewicht={td.Weight[0x0F44]} (Quelle {td.Weight[srcId]}), Flags gleich={td.ItemFlags[0x0F44] == td.ItemFlags[srcId]}, Name unveraendert='{td.ItemName[0x0F44]}', Dirty={td.Dirty.Contains(td.LandCount + 0x0F44)}, naechster freier mit Art ab 0x0F40: 0x{tdp.NextFreeItem(0x0F40, true):X}, ganz freier: 0x{tdp.NextFreeItem(0x0F40, false):X}");

        string ttmp = Path.Combine(Path.GetTempPath(), "uotinker_tabletest");
        if (Directory.Exists(ttmp))
        {
            Directory.Delete(ttmp, true);
        }
        Directory.CreateDirectory(ttmp);
        foreach (var fn in new[] { "hues.mul", "radarcol.mul", "skills.idx", "skills.mul" })
        {
            if (File.Exists(Path.Combine(ctx.Folder, fn)))
            {
                File.Copy(Path.Combine(ctx.Folder, fn), Path.Combine(ttmp, fn));
            }
        }
        string? clSrc = Directory.GetFiles(ctx.Folder, "cliloc.*").Where(x => !x.Contains(".bak")).OrderBy(x => x).FirstOrDefault();
        if (clSrc != null)
        {
            File.Copy(clSrc, Path.Combine(ttmp, Path.GetFileName(clSrc)));
        }
        var oHue = File.ReadAllBytes(Path.Combine(ttmp, "hues.mul"));
        var oRad = File.ReadAllBytes(Path.Combine(ttmp, "radarcol.mul"));
        var hd = new HueData(ttmp);
        hd.Touch(5);
        hd.Names[5] = "Testhue";
        hd.Colors[5][3] = 0x1234;
        hd.TableStart[5] = 11;
        hd.Touch(1000);
        hd.Colors[1000][31] = 0x7FFF;
        string? he = hd.Save(out string hbk);
        var hd2 = new HueData(ttmp);
        var aHue = File.ReadAllBytes(Path.Combine(ttmp, "hues.mul"));
        int hdiff = Enumerable.Range(0, oHue.Length).Count(k => oHue[k] != aHue[k]);
        sb.AppendLine($"Huetest: Fehler={he ?? "keiner"}, Groesse gleich={oHue.Length == aHue.Length}, geaenderte Bytes={hdiff}, Name='{hd2.Names[5]}', Farbe3=0x{hd2.Colors[5][3]:X4}, Start={hd2.TableStart[5]}, Hue1000/31=0x{hd2.Colors[1000][31]:X4}, Nachbar unberuehrt={hd2.Names[6] == ctx.Hues.Names[6] && hd2.Names[4] == ctx.Hues.Names[4]}, Backup gleich={File.ReadAllBytes(hbk).SequenceEqual(oHue)}");
        hd.Touch(7);
        hd.Names[7] = "weg";
        hd.Revert(7);
        sb.AppendLine($"Hue-Revert: Name='{hd.Names[7]}' Dirty={hd.Dirty.Count}");
        var rd = new RadarData(ttmp);
        rd.Set(0x4000 + 0x0F43, 0x1111);
        rd.Set(7, 0x2222);
        string? re = rd.Save(out string rbk);
        var rd2 = new RadarData(ttmp);
        var aRad = File.ReadAllBytes(Path.Combine(ttmp, "radarcol.mul"));
        sb.AppendLine($"Radartest: Fehler={re ?? "keiner"}, Groesse gleich={oRad.Length == aRad.Length}, geaenderte Bytes={Enumerable.Range(0, oRad.Length).Count(k => oRad[k] != aRad[k])} (max 4), Item=0x{rd2.Col[0x4000 + 0x0F43]:X4}, Land7=0x{rd2.Col[7]:X4}, Backup gleich={File.ReadAllBytes(rbk).SequenceEqual(oRad)}");
        var sd = new SkillData(ttmp);
        string firstName = sd.Names[0];
        int freeSkill = Enumerable.Range(0, sd.Count).FirstOrDefault(k => !sd.Valid[k], -1);
        sd.Touch(0);
        sd.Names[0] = firstName + "X";
        sd.Button[0] = !sd.Button[0];
        if (freeSkill >= 0)
        {
            sd.Touch(freeSkill);
            sd.Valid[freeSkill] = true;
            sd.Names[freeSkill] = "Testskill";
        }
        string? se2 = sd.Save(out _);
        var sd2 = new SkillData(ttmp);
        int namesSame = Enumerable.Range(1, sd.Count - 1).Count(k => k != freeSkill && sd2.Names[k] == ctx.Skills.Names[k] && sd2.Valid[k] == ctx.Skills.Valid[k] && sd2.Button[k] == ctx.Skills.Button[k]);
        sb.AppendLine($"Skilltest: Fehler={se2 ?? "keiner"}, Skill0='{sd2.Names[0]}' (vorher '{firstName}'), Button gedreht={sd2.Button[0] != ctx.Skills.Button[0]}, freier Slot {freeSkill}='{(freeSkill >= 0 ? sd2.Names[freeSkill] : "")}', unveraenderte Skills={namesSame}/{sd.Count - 1 - (freeSkill >= 0 ? 1 : 0)}");
        if (clSrc != null)
        {
            string clTmp = Path.Combine(ttmp, Path.GetFileName(clSrc));
            var oCl = File.ReadAllBytes(clTmp);
            var cd = new ClilocData(clTmp);
            if (cd.Numbers.Length > 0)
            {
                int n0 = cd.Numbers[0];
                string t0 = cd.Texts[0];
                cd.SetText(n0, t0 + "!");
                cd.SetText(999999999, "Neuer Eintrag");
                string? ce = cd.Save(out string cbk);
                var cd2 = new ClilocData(clTmp);
                int mism = 0;
                for (int k = 1; k < cd.Numbers.Length - 1; k++)
                {
                    if (cd2.Numbers[k] != cd.Numbers[k] || cd2.Texts[k] != cd.Texts[k])
                    {
                        mism++;
                    }
                }
                sb.AppendLine($"Cliloctest: Fehler={ce ?? (cd.CanWrite ? "keiner" : cd.WriteBlocker)}, komprimiert={cd.Compressed}, Eintraege {cd.Numbers.Length - 1}->{cd2.Numbers.Length}, Text0 geaendert={cd2.Texts[0] == t0 + "!"}, neuer Eintrag='{(cd2.IndexOf(999999999) >= 0 ? cd2.Texts[cd2.IndexOf(999999999)] : "fehlt")}', Abweichungen={mism}, Backup gleich={(ce == null && File.ReadAllBytes(cbk).SequenceEqual(oCl))}");
                cd.Revert(999999999);
                sb.AppendLine($"Cliloc-Revert: Eintraege={cd.Numbers.Length}, Dirty={cd.Dirty.Count}");
            }
        }

        foreach (var (shot, make) in new (string, Func<Control>)[]
                 {
                     ("radar", () => new RadarEditor(ctx, 0x4000 + 0x0F43)),
                     ("hue", () => new HueEditor(ctx, 5)),
                     ("skill", () => new SkillEditor(ctx, 0)),
                     ("cliloc", () => new ClilocEditor(ctx.Cliloc(clSrc ?? ""), ctx.Cliloc(clSrc ?? "").Numbers.FirstOrDefault())),
                 })
        {
            if (shot == "cliloc" && clSrc == null)
            {
                continue;
            }
            var shotForm = new Form { Width = 520, Height = 520 };
            shotForm.Controls.Add(make());
            shotForm.Show();
            Application.DoEvents();
            Application.DoEvents();
            using var shotBmp = new Bitmap(shotForm.Width, shotForm.Height);
            shotForm.DrawToBitmap(shotBmp, new Rectangle(0, 0, shotBmp.Width, shotBmp.Height));
            shotBmp.Save(Path.Combine(outDir, $"editor_{shot}.png"), ImageFormat.Png);
            shotForm.Close();
        }

        var helpForm = new HelpForm { Width = 1000, Height = 700, StartPosition = FormStartPosition.Manual };
        helpForm.Show();
        helpForm.Open("tiledata-edit");
        Application.DoEvents();
        Thread.Sleep(1500);
        Application.DoEvents();
        using (var hb = new Bitmap(helpForm.Width, helpForm.Height))
        {
            helpForm.DrawToBitmap(hb, new Rectangle(0, 0, hb.Width, hb.Height));
            hb.Save(Path.Combine(outDir, "help.png"), ImageFormat.Png);
        }
        helpForm.Close();

        var edForm = new Form { Width = 520, Height = 760 };
        var ed = new TileEditor(tdp, ctx.Tile.LandCount + 0x0F43);
        edForm.Controls.Add(ed);
        edForm.Show();
        Application.DoEvents();
        using (var eb = new Bitmap(edForm.Width, edForm.Height))
        {
            edForm.DrawToBitmap(eb, new Rectangle(0, 0, eb.Width, eb.Height));
            eb.Save(Path.Combine(outDir, "editor.png"), ImageFormat.Png);
        }
        edForm.Close();

        var smallForm = new Form { Width = 420, Height = 360 };
        var small = new TileEditor(tdp, ctx.Tile.LandCount + 0x0F43);
        smallForm.Controls.Add(small);
        smallForm.Show();
        Application.DoEvents();
        Application.DoEvents();
        sb.AppendLine($"Editor klein: Client {small.ClientSize.Width}x{small.ClientSize.Height}, Scroll-Hoehe {small.VerticalScroll.Maximum}, Scrollbalken sichtbar={small.VerticalScroll.Visible}");
        small.VerticalScroll.Value = small.VerticalScroll.Maximum - small.VerticalScroll.LargeChange + 1;
        small.PerformLayout();
        using (var sbm = new Bitmap(smallForm.Width, smallForm.Height))
        {
            smallForm.DrawToBitmap(sbm, new Rectangle(0, 0, sbm.Width, sbm.Height));
            sbm.Save(Path.Combine(outDir, "editor_small_bottom.png"), ImageFormat.Png);
        }
        smallForm.Close();

        try
        {
            var newer = Updater.CheckAsync().GetAwaiter().GetResult();
            sb.AppendLine($"Update-Pruefung: installiert {Updater.Current}, neuer={(newer == null ? "nein" : newer.Version.ToString())}");
            var latest = Updater.CheckAsync(false).GetAwaiter().GetResult();
            if (latest != null)
            {
                string dl = Updater.DownloadAsync(latest, new Progress<int>(_ => { })).GetAwaiter().GetResult();
                sb.AppendLine($"Update-Download: {latest.Tag} {latest.Name} {new FileInfo(dl).Length} Bytes, Pruefsumme ok");
                File.Delete(dl);
                latest.Sha256 = new string('0', 64);
                try
                {
                    Updater.DownloadAsync(latest, new Progress<int>(_ => { })).GetAwaiter().GetResult();
                    sb.AppendLine("Update-Falschsumme: NICHT abgelehnt");
                }
                catch (InvalidOperationException)
                {
                    sb.AppendLine("Update-Falschsumme: abgelehnt, Datei geloescht=" + !File.Exists(Path.Combine(Path.GetTempPath(), latest.Name)));
                }
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine("Update-Test Fehler: " + ex.Message);
        }

        var mon = new BodyAnimProvider(ctx, BodyMode.Monster);
        var itm = new BodyAnimProvider(ctx, BodyMode.ItemAnim);
        sb.AppendLine($"Monster-Tab: {mon.Summary}");
        sb.AppendLine($"Item-Anim-Tab: {itm.Summary}");
        sb.AppendLine($"Gesamtzeit {sw.ElapsedMilliseconds} ms");
        return sb.ToString();
    }
}
