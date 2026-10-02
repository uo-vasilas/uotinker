# UOTinker

**UOTinker** is a viewer and editor for the data files of an Ultima Online client, built for shard operators and
content creators. Point it at any folder that contains your client files (`art.mul`, `tiledata.mul`, `hues.mul`, ...)
and browse everything in one place, including empty slots up to the highest ID, then fix what is missing.

*Deutsche Anleitung weiter unten. / German instructions below.*

![License: GPL v3 or later](https://img.shields.io/badge/license-GPLv3%2B-blue.svg)

If UOTinker is useful to you, you can support its development on
[Ko-fi](https://ko-fi.com/uoschattenwelt). This is voluntary; the tool stays free and GPL.

The user interface and the built-in help (F1) are currently in German.

---

## English

### What it does

**Browse** (thumbnail grid or list, search and filters on every page, CSV export of the current view)

- Items (`art.mul`), land tiles, gumps (`gumpart.mul`), radar colors, hues
- Animations: bodies and monsters from `anim.mul` to `anim6.mul`, `bodyconv.def`, `body.def` and the UOP animation files, item animation IDs with their paperdoll gumps, and every animation file on its own
- Tiledata (`tiledata.mul`, the 64-bit flag format), skills, cliloc (all languages, including compressed files)

**Find gaps** (Tiledata page)

- A problem column and filter: art without tiledata, tiledata without art, wearable without animation ID or layer, animation ID without animation data, missing paperdoll gump
- All 32 flags are filterable individually
- An overview card lists the items with gaps; the checks can be switched on and off

**Edit**

- Tiledata: name, weight, height, layer, quantity, animation ID, hue, light and all flags
- Copy values from another entry, create a new item (tiledata and art in one step, optionally saved immediately)
- Item art, land tiles and gumps: import from PNG/BMP/GIF/JPG, export as PNG, clear a slot
- Nothing is written until you save. Before every write a backup is made (outside the client folder, in `%APPDATA%\UOTinker\backups`)
- Art and gump data are appended to the end of the file and only the index is rewritten, so existing data stays untouched
- A read-only switch in the settings menu turns writing off completely
- Update check: on start the program asks GitHub for the latest release and offers a one-click update; the installer's SHA-256 checksum is verified before it is started (can be switched off in the settings menu)

**Optional: Sphere scripts**

If you run a Sphere server you can also point UOTinker at your script folder. It then lists all `[ITEMDEF]`
definitions with their item ID, shows tiledata names next to them, flags definitions without a tiledata name and jumps
between an item and its definition (the script opens at the right line in VS Code or Notepad++). Without a script
folder these parts stay empty.

### Requirements

- Windows
- .NET 8 SDK to build, or the .NET 8 Desktop Runtime to run

### Installer

A Windows installer (self-contained, no .NET installation needed, per-user, English/German) is built with [NSIS](https://nsis.sourceforge.io):

```n.\tools\Release-UOTinker.ps1 -Makensis <path to makensis.exe>
```

The result is `Setup\UOTinker_Setup_<version>.exe`.

### Build and run

```
dotnet build -c Release
bin\Release\net8.0-windows\UOTinker.exe
```

On the first start the program asks for the data folder. Later, the gear icon at the bottom left switches the data
folder, sets the optional script folder and toggles the read-only mode. Settings are stored in
`%APPDATA%\UOTinker\settings.json`.

### Safety notes

- Work on a copy of your client folder first. The write support is tested on copies of real files (pixel-exact round trips, only the expected index entries change), but not on every client version.
- Close the game client before saving; open files cannot be written.
- `art.mul`, `artidx.mul` and `tiledata.mul` belong together. If you change them, distribute all three.

### Self test

`UOTinker.exe --selftest <output file>` loads your data folder, renders pages and runs write round trips on temporary
copies. It never changes your real files.

### Project layout

| File | Content |
| --- | --- |
| `src/Core.cs` | Settings, readers and writers for art, gumps, tiledata |
| `src/Anim.cs` | Animation files, UOP, body definitions, Sphere script catalog |
| `src/Providers.cs`, `src/ItemDefList.cs` | One provider per page (columns, filters, preview) |
| `src/TileEditor.cs`, `src/GraphicEditor.cs` | Editors for tiledata and for art, land and gumps |
| `src/MainForm.cs`, `src/Ui.cs`, `src/Grid.cs`, `src/Theme.cs` | Window, pages, thumbnail grid, dark theme |
| `src/Help.cs` | Built-in help |
| `src/SelfTest.cs` | Self test |

### License

UOTinker is free software under the **GNU General Public License, version 3 or (at your option) any later version** (see `LICENSE`). `src/BwtDecompress.cs` comes from ClassicUO (BSD 2-Clause); see `THIRD-PARTY-NOTICES.md`.

---

## Deutsch

**UOTinker** ist ein Betrachter und Editor für die Dateien eines Ultima-Online-Clients, gedacht für Shard-Betreiber
und Content-Ersteller. Du gibst einen beliebigen Ordner mit den Client-Dateien an (`art.mul`, `tiledata.mul`,
`hues.mul` ...) und siehst alles an einem Ort, auch freie Slots bis zur höchsten ID, und kannst Fehlendes pflegen.

### Funktionen

- **Ansehen:** Items, Landtiles, Gumps, Radarcolor, Hues, Animationen (Mul und UOP), Tiledata, Skills und Cliloc, jeweils als Raster oder Liste mit Suche, Filtern und CSV-Export.
- **Lücken finden:** Die Tiledata-Seite zeigt Probleme wie Art ohne Tiledata, Tiledata ohne Art oder tragbare Items ohne AnimID, Layer oder Paperdoll. Die Prüfungen lassen sich einzeln abschalten. Auf der Übersicht zählt eine Karte die Items mit Lücken.
- **Bearbeiten:** Tiledata-Werte und Flags, Werte kopieren, neue Items anlegen (Tiledata und Art in einem Schritt), Bilder in Item-Art, Landtiles und Gumps importieren oder als PNG exportieren. Vor jedem Schreiben wird gesichert; ein Schreibschutz-Schalter im Zahnrad-Menü verhindert jede Änderung.
- **Optional Sphere-Skripte:** Mit einem Skriptordner listet UOTinker alle ITEMDEFs, vergleicht sie mit der Tiledata und springt zwischen Item und Definition.

### Starten

```
dotnet build -c Release
bin\Release\net8.0-windows\UOTinker.exe
```

Beim ersten Start fragt das Programm nach dem Datenordner. Die Hilfe öffnest du mit F1. Arbeite zuerst an einer
Kopie deines Client-Ordners und schließe den Spielclient vor dem Speichern.

### Lizenz

UOTinker steht unter der **GNU General Public License, Version 3 oder einer späteren Version** (siehe `LICENSE`). `src/BwtDecompress.cs` stammt aus ClassicUO (BSD 2-Clause), siehe `THIRD-PARTY-NOTICES.md`.
