using System.Drawing;
using System.Net;

namespace UOTinker;

public sealed class HelpTopic
{
    public string Key = "";
    public string Title = "";
    public string Html = "";
    public List<HelpTopic> Children = new();
}

public static class HelpContent
{
    private static string H(string name, string text) => $"<p><a name=\"{name}\"></a><span class=\"h\">{WebUtility.HtmlEncode(name)}</span><br>{text}</p>";
    private static string Note(string text) => $"<div class=\"note\">*{Loc.T("Hinweis")}* {text}</div>";
    private static string Link(string key, string text) => $"<a href=\"topic:{key}\">{text}</a>";

    public static List<HelpTopic> Build()
    {
        HelpTopic T(string key, string title, string html, params HelpTopic[] kids) =>
            new() { Key = key, Title = title, Html = $"<p class=\"title\">*{WebUtility.HtmlEncode(title)}*</p>{html}", Children = kids.ToList() };

        return new List<HelpTopic>
        {
            T("welcome", Loc.T("Willkommen"),
                Loc.T("<p>UO Tinker zeigt die Dateien eines Ultima-Online-Clients an und pflegt Tiledata, Item-Art, Landtiles und Gumps. Du wählst einen beliebigen Ordner mit den Client-Dateien (art.mul, tiledata.mul, hues.mul ...) als Datenordner; angezeigt werden Items, Gumps, Tiledata, Landtiles, Radarcolor, Hues, Animationen, Skills und Cliloc, jeweils bis zur höchsten ID, auch freie Slots. Optional lässt sich zusätzlich der Skriptordner eines Sphere-Servers angeben, damit Items mit ihren ITEMDEFs abgeglichen werden.</p>") +
                Loc.T("<p>Links in der Seitenleiste wählst du den Bereich. Die meisten Bereiche haben oben eine Suche, Filter und die Umschaltung zwischen Raster und Liste. Rechts steht die Vorschau des gewählten Eintrags.</p>") +
                Note(Loc.T("Ändern lassen sich Tiledata, Item-Art, Landtiles und Gumps. Vor jedem Schreiben wird eine Sicherung angelegt. Arbeite trotzdem am besten an einer Kopie deines Client-Ordners. Wer nur ansehen will, schaltet im Zahnrad-Menü unten links den Schreibschutz ein. Alles andere ist lesend.")) +
                Loc.F("<p>Weiter mit {0}, {1} oder {2}.</p>", Link("bedienung", Loc.T("Bedienung")), Link("tiledata", Loc.T("Tiledata")), Link("tiledata-edit", Loc.T("Tiledata bearbeiten")))),

            T("about", Loc.T("Über UOTinker"),
                $"<p><b>UOTinker {AppInfo.Version}</b><br>© {AppInfo.Years} {AppInfo.Author}</p>" +
                Loc.T("<p>Betrachter und Editor für die Dateien eines Ultima-Online-Clients. Das Programm ist frei und nicht an einen bestimmten Shard gebunden. Das Fenster 'Über UOTinker' (Symbol ⓘ unten links) zeigt Version, Erstelldatum und Lizenz.</p>") +
                H(Loc.T("Lizenz"), AppInfo.License1 + " " + AppInfo.License2 + " " + AppInfo.License3) +
                H(Loc.T("Unterstützen"), Loc.T("Wenn dir UOTinker nützt, kannst du die Entwicklung freiwillig auf Ko-fi unterstützen (Herz-Symbol unten links). Das Programm bleibt kostenlos und frei."))),

            T("bedienung", Loc.T("Bedienung"),
                H(Loc.T("Suche"), Loc.T("Mehrere Wörter, alle müssen vorkommen. Gesucht wird in ID (dezimal und hex, z. B. 0xf60), Namen, Sphere-Defnames und Problemtext.")) +
                H(Loc.T("Filter"), Loc.T("Die Auswahlfelder neben der Suche. Sie wirken zusammen mit der Suche (UND). Bei Tiledata gibt es zum Beispiel Problem, Flag, Art und Sphere.")) +
                H(Loc.T("Raster und Liste"), Loc.T("Oben rechts schaltest du zwischen Thumbnail-Raster und Liste um. Im Raster stellt Zelle die Größe ein; Pfeiltasten, Bild auf/ab, Pos1/Ende und Mausrad bewegen die Auswahl.")) +
                H(Loc.T("CSV exportieren"), Loc.T("Der Knopf oben rechts speichert die aktuell angezeigte Liste (also mit Suche und Filtern) als CSV-Datei: Semikolon als Trenner, UTF-8 mit Kennung, damit Excel Umlaute richtig liest. Die Spalten entsprechen der Liste; bei der Tiledata ist auch die Problem-Spalte dabei.")) +
                H(Loc.T("Gehe zu ID"), Loc.T("ID eingeben (dezimal oder 0x hex) und Enter. Der Eintrag muss in der aktuellen Auswahl liegen, sonst Filter zurücksetzen.")) +
                H(Loc.T("Updates"), Loc.T("Beim Start fragt UOTinker bei GitHub nach der neuesten Version (abschaltbar im Zahnrad-Menü; dort auch 'Jetzt nach Updates suchen'). Gibt es eine neuere Version, erscheint auf der Übersicht oben eine Zeile mit 'Jetzt aktualisieren'. Das Programm lädt dann den Installer, vergleicht seine SHA-256-Prüfsumme mit der des Releases, startet ihn und beendet sich. Stimmt die Prüfsumme nicht, wird nichts installiert. Einstellungen und Sicherungen bleiben erhalten.")) +
                H(Loc.T("Datenordner"), Loc.T("Zahnrad unten links: Datenordner wählen, optional einen Sphere-Skriptordner angeben und den Schreibschutz umschalten. Beim ersten Start fragt das Programm nach dem Datenordner. Neu laden (Pfeil) liest alles erneut ein.")) +
                H(Loc.T("Zuletzt angesehen"), Loc.T("Auf der Übersicht stehen die zuletzt geöffneten Einträge; Doppelklick springt wieder hin.")) +
                H(Loc.T("Items mit Lücken"), Loc.T("Die rote Karte auf der Übersicht zählt Items mit mindestens einem Tiledata-Problem (Art ohne Tiledata, Tiledata ohne Art, Animations- und Layer-Probleme, ITEMDEF ohne Namen). Ein Klick öffnet die Tiledata-Liste mit dem Filter 'Irgendein Problem'. In den Schnellaktionen steht jede Problemart einzeln mit ihrer Anzahl; Doppelklick öffnet die gefilterte Liste. Die Zahlen aktualisieren sich nach Änderungen, sobald du zur Übersicht zurückkehrst.")) +
                Note(Loc.T("Freie Slots sind grau markiert. 'Frei' heißt: kein Inhalt in der Datei, nicht unbedingt ungenutzt im Spiel."))),

            T("pages", Loc.T("Bereiche"),
                Loc.T("<p>Jeder Bereich in der Seitenleiste hat eine eigene Seite:</p>") +
                $"<p>{Link("art", Loc.T("Items (Art)"))} · {Link("land", Loc.T("Landtiles"))} · {Link("gump", Loc.T("Gumps"))} · {Link("radar", Loc.T("Radarcolor"))} · {Link("hues", Loc.T("Hues"))}<br>" +
                $"{Link("monster", Loc.T("Bodies / Monster"))} · {Link("itemanim", Loc.T("Item-AnimIDs"))} · {Link("raw", Loc.T("Rohdateien"))}<br>" +
                $"{Link("tiledata", Loc.T("Tiledata"))} · {Link("itemdef", Loc.T("Sphere-ITEMDEF"))} · {Link("skills", Loc.T("Skills"))} · {Link("cliloc", Loc.T("Cliloc"))}</p>",
                T("art", Loc.T("Items (Art)"),
                    Loc.T("<p>Alle Item-Grafiken aus art.mul bis zur höchsten Tiledata-ID.</p>") +
                    H(Loc.T("Status"), Loc.T("Belegt, frei oder außerhalb. Frei heißt: kein Bild in art.mul.")) +
                    H(Loc.T("Tiledata-Name"), Loc.T("Name aus der Tiledata. Ist er leer, fehlt die Pflege.")) +
                    H(Loc.T("Sphere-ITEMDEF"), Loc.T("Nur mit angegebenem Sphere-Skriptordner: zeigt die Definitionen, die diese ID benutzen.")) +
                    H(Loc.T("AnimID"), Loc.T("Animations-ID aus der Tiledata, falls das Item am Körper getragen wird.")) +
                    Loc.F("<p>Bearbeiten: siehe {0}.</p>", Link("art-edit", Loc.T("Item-Art bearbeiten"))),
                    T("art-edit", Loc.T("Item-Art bearbeiten"),
                        Loc.T("<p>Rechts neben der Liste steht ein Formular zum gewählten Slot. Änderungen sammeln sich im Programm und werden erst mit 'Art speichern' in die Dateien geschrieben.</p>") +
                        H(Loc.T("Aus Bild importieren"), Loc.T("Wählt ein Bild (PNG, BMP, GIF oder JPG) und wandelt es in das Item-Format um. Pixel mit weniger als 50 % Deckkraft werden transparent, die Farben werden auf 15 Bit reduziert (reines Schwarz wird zu einem sehr dunklen Grau, weil Schwarz im Format 'durchsichtig' heißt). Größe 1 bis 1024 Pixel je Seite; die Bildgröße bestimmt die Item-Größe im Spiel.")) +
                        H(Loc.T("Als PNG exportieren"), Loc.T("Speichert die aktuelle Grafik des Slots als PNG, auch bei noch nicht gespeicherten Änderungen.")) +
                        H(Loc.T("Slot leeren"), Loc.T("Entfernt die Grafik beim Speichern. Nach Rückfrage.")) +
                        H(Loc.T("Eintrag zurücksetzen / Alle verwerfen"), Loc.T("Nimmt die ungespeicherte Änderung dieses Slots bzw. aller Slots zurück.")) +
                        H(Loc.T("Art speichern"), Loc.T("Hängt die neuen Grafiken hinten an art.mul an und trägt sie in artidx.mul ein. Bereits vorhandene Daten in art.mul werden nicht verändert; ersetzte oder entfernte Grafiken bleiben als ungenutzter Platz in der Datei. Vor dem Schreiben wird artidx.mul unter %APPDATA%\\UOTinker\\backups gesichert, dazu eine Textdatei mit der ursprünglichen Größe von art.mul.")) +
                        Note(Loc.T("Nicht bei aktivem Schreibschutz. art.mul, artidx.mul und tiledata.mul gehören zusammen: wer die Dateien weitergibt (Client, Spieler, Server), gibt alle drei gemeinsam weiter. Server lesen die Grafikdatei oft nicht, aber meist die Tiledata.")) +
                        H(Loc.T("Land-Kacheln"), Loc.T("Im Tab Landtiles gilt dasselbe. Das Bild muss genau 44 x 44 Pixel groß sein; gespeichert wird nur die Raute in der Mitte (1012 Pixel), die Ecken werden ignoriert. Land hat keine Transparenz, Schwarz bleibt Schwarz. Gespeichert wird wie bei Items in art.mul und artidx.mul.")) +
                        H(Loc.T("Gumps"), Loc.T("Im Tab Gumps gilt dasselbe mit Bildern von 1 bis 2048 Pixeln je Seite. Pixel mit weniger als 50 % Deckkraft werden transparent. Gespeichert wird in gumpart.mul und gumpidx.mul; die Sicherung betrifft gumpidx.mul. Paperdoll-Gumps liegen bei 50000 + AnimID (männlich) und 60000 + AnimID (weiblich) und sind in der Praxis 260 x 237 Pixel groß.")) +
                        Note(Loc.T("Das Programm sollte nicht laufen, während der Spielclient die Dateien geöffnet hat; dann meldet es einen Fehler und ändert nichts. Art-Änderungen und Gump-Änderungen werden getrennt gespeichert (Art und Land zusammen, Gumps für sich).")))),
                T("land", Loc.T("Landtiles"), Loc.T("<p>Bodenkacheln (Index 0 bis 0x3FFF). Zu jeder Kachel stehen Name, TexID und Flags aus der Tiledata.")),
                T("gump", Loc.T("Gumps"),
                    Loc.T("<p>Alle Gumps aus gumpart.mul bis zur höchsten ID.</p>") +
                    H(Loc.T("Bedeutung"), Loc.T("Ab 50000 sind es männliche, ab 60000 weibliche Paperdoll-Gumps; die AnimID ist die ID minus 50000 bzw. 60000."))),
                T("radar", Loc.T("Radarcolor"),
                    Loc.T("<p>Radarfarbe je Land- und Item-Grafik (radarcol.mul), ein 16-Bit-Wert pro Art-Index. Rechts neben der Liste steht ein Formular zum gewählten Eintrag.</p>") +
                    H(Loc.T("Farbe"), Loc.T("Der Wert als 15-Bit-Hexzahl (Rot, Grün und Blau je 5 Bit). Das Farbfeld daneben zeigt das Ergebnis; 'Farbe wählen' öffnet den Farbdialog.")) +
                    H(Loc.T("Aus der Grafik berechnen"), Loc.T("Setzt den Mittelwert aller nicht transparenten Pixel der Grafik.")) +
                    H(Loc.T("Fehlende Item-Farben füllen"), Loc.T("Berechnet die Farbe für alle Items, die eine Grafik, aber die Radarfarbe 0 haben, und merkt sie als Änderung vor.")) +
                    H(Loc.T("Speichern"), Loc.T("Schreibt alle geänderten Einträge dieser Datei. Zuvor wird die vorherige Datei unter %APPDATA%\\UOTinker\\backups gesichert. Die Zahl in Klammern ist die Menge der geänderten Einträge. Bei aktivem Schreibschutz ist der Knopf gesperrt.")) +
                    H(Loc.T("Eintrag zurücksetzen / Alle verwerfen"), Loc.T("Stellt den Eintrag oder alle ungespeicherten Änderungen auf den Stand der Datei zurück."))),
                T("hues", Loc.T("Hues"),
                    Loc.T("<p>Alle Farben aus hues.mul mit je 32 Farbwerten. Das Raster zeigt die Farbverläufe. Rechts neben der Liste steht ein Formular zum gewählten Hue.</p>") +
                    H(Loc.T("Name"), Loc.T("Höchstens 19 Zeichen, Latin-1.")) +
                    H(Loc.T("Tabelle von / bis"), Loc.T("Die beiden Tabellenwerte des Eintrags, bleiben meist unverändert.")) +
                    H(Loc.T("Farbfelder"), Loc.T("Ein Klick auf eines der 32 Felder öffnet den Farbdialog. Die Farben werden auf 15 Bit reduziert.")) +
                    H(Loc.T("Verlauf"), Loc.T("Verteilt die Farben dazwischen gleichmäßig zwischen Farbe 1 und Farbe 32.")) +
                    H(Loc.T("Farben von Hue kopieren"), Loc.T("Übernimmt alle 32 Farben eines anderen Hues, Name und Tabellenwerte bleiben.")) +
                    H(Loc.T("Speichern"), Loc.T("Schreibt alle geänderten Einträge dieser Datei. Zuvor wird die vorherige Datei unter %APPDATA%\\UOTinker\\backups gesichert. Die Zahl in Klammern ist die Menge der geänderten Einträge. Bei aktivem Schreibschutz ist der Knopf gesperrt.")) +
                    H(Loc.T("Eintrag zurücksetzen / Alle verwerfen"), Loc.T("Stellt den Eintrag oder alle ungespeicherten Änderungen auf den Stand der Datei zurück."))),
                T("monster", Loc.T("Bodies / Monster"),
                    Loc.T("<p>Alle Bodies mit ihren Quellen: anim.mul bis anim6.mul, bodyconv.def, body.def und UOP.</p>") +
                    H(Loc.T("Quellen"), Loc.T("Mul (A1 bis A6) oder UOP. Eine Zeile kann mehrere Quellen haben.")) +
                    H(Loc.T("Chardef ohne Animation"), Loc.T("Filter für Sphere-Chardefs (nur mit Skriptordner), deren Body keine Animationsdaten hat und die deshalb unsichtbar wären.")) +
                    H(Loc.T("Vorschau"), Loc.T("Quelle, Aktion und Richtung oben in der Vorschau wählen. Zoom und Hintergrund sind einstellbar."))),
                T("itemanim", Loc.T("Item-AnimIDs"), Loc.T("<p>Animations-IDs, die Items in der Tiledata benutzen, samt Paperdoll-Gumps und den Items, die sie verwenden.")),
                T("raw", Loc.T("Rohdateien"), Loc.T("<p>Jede Animationsdatei einzeln, ohne body.def und bodyconv.def, dazu die UOP-Dateien. Nützlich, um zu sehen, was wirklich in einer Datei steht.")),
                T("itemdef", Loc.T("Sphere-ITEMDEF"),
                    Loc.T("<p>Nur nutzbar, wenn im Zahnrad-Menü ein Sphere-Skriptordner angegeben ist (Sphere-Skriptformat mit <code>[ITEMDEF ...]</code>-Abschnitten). Jede Definition steht als eine Zeile: Defname, Kopf, aufgelöste Item-ID, Name im Skript, Name in der Tiledata, Kategorie, Datei und Zeile. Das ist der Rückweg zu den Items: Du siehst, welche Definitionen es gibt und wo in den Tiledata oder der Art etwas fehlt.</p>") +
                    H(Loc.T("Problem"), Loc.T("Zeigt 'Tiledata ohne Name', 'Art fehlt', 'ID nicht aufgelöst' (die ID= der Definition verweist auf etwas, das UOTinker nicht auflösen konnte) und 'ID außerhalb der Tiledata'. Der Filter 'Problem' grenzt danach ein.")) +
                    H(Loc.T("Skript öffnen"), Loc.T("Öffnet die Datei an der Zeile der Definition (VS Code, sonst Notepad++, sonst Standardprogramm).")) +
                    H(Loc.T("Im Tiledata zeigen / Bei Items (Art) zeigen"), Loc.T("Wechselt auf die jeweilige Seite und springt zum Eintrag dieser Item-ID. Filter und Suche der Zielseite werden dafür zurückgesetzt.")) +
                    Note(Loc.T("Die Skripte werden nur gelesen, UOTinker ändert sie nie. Die Liste entsteht beim Laden; nach Änderungen an den Skripten mit 'Neu laden' aktualisieren. Der Sprung Item -> ITEMDEF sitzt im Tiledata-Editor ('ITEMDEF ...' und 'in der ITEMDEF-Liste zeigen').")) ),
                T("skills", Loc.T("Skills"),
                    Loc.T("<p>Skills aus skills.idx/skills.mul mit Name und Aktiv-Kennzeichen.</p>") +
                    H(Loc.T("Skill vorhanden"), Loc.T("Belegt einen freien Slot oder gibt einen Slot wieder frei.")) +
                    H(Loc.T("Skill-Button"), Loc.T("Ob der Skill im Skillfenster einen Knopf hat.")) +
                    H(Loc.T("Speichern"), Loc.T("Schreibt skills.mul und skills.idx neu (Reihenfolge der Daten in der mul-Datei ändert sich, die Zuordnung nicht). Zuvor werden beide Dateien unter %APPDATA%\\UOTinker\\backups gesichert."))),
                T("cliloc", Loc.T("Cliloc"),
                    Loc.T("<p>Alle Texte der Cliloc-Dateien je Sprache (komprimierte Dateien werden entpackt). Suche nach Nummer oder Text.</p>") +
                    H(Loc.T("Text bearbeiten"), Loc.T("Das Feld rechts ändert den Text des gewählten Eintrags. Zeilenumbrüche gibt es in Cliloc-Texten nicht, sie werden zu Leerzeichen.")) +
                    H(Loc.T("Neuer Eintrag"), Loc.T("Legt eine neue Nummer mit Text an (am Dateiende).")) +
                    H(Loc.T("Speichern"), Loc.T("Schreibt alle geänderten Einträge dieser Datei. Zuvor wird die vorherige Datei unter %APPDATA%\\UOTinker\\backups gesichert. Die Zahl in Klammern ist die Menge der geänderten Einträge. Bei aktivem Schreibschutz ist der Knopf gesperrt.")) +
                    H(Loc.T("Eintrag zurücksetzen / Alle verwerfen"), Loc.T("Stellt den Eintrag oder alle ungespeicherten Änderungen auf den Stand der Datei zurück.")) +
                    Note(Loc.T("Komprimierte (BWT) Dateien lassen sich anzeigen, aber nicht schreiben.")))),

            T("tiledata", Loc.T("Tiledata"),
                Loc.T("<p>Die Tiledata (tiledata.mul) legt für jedes Land- und Item-Tile fest, was es im Spiel ist: Name, Flags, Gewicht, Höhe, Layer, Menge, AnimID, Hue und Licht. Das Item-Bild steht in art.mul, seine Eigenschaften hier. Der Client liest die Datei, Server-Emulatoren meist ebenfalls.</p>") +
                H(Loc.T("Raster und Liste"), Loc.T("Standard ist das Raster mit der Grafik jedes Eintrags. Die Beschriftung 'I' steht für Item, 'L' für Land.")) +
                H(Loc.T("Problem"), Loc.F("Zeigt automatisch erkannte Lücken. Mit dem Filter 'Problem' wählst du einzelne Arten aus. Die Arten stehen unter {0}.", Link("tiledata-probleme", Loc.T("Problemarten")))) +
                H(Loc.T("Filter"), Loc.T("Typ, Eintrag (leer oder gefüllt), Problem, jedes der 32 Flags einzeln, Art vorhanden und Sphere-ITEMDEF vorhanden.")) +
                H(Loc.T("Ändern"), Loc.F("Wähle einen Eintrag und ändere die Werte rechts. Siehe {0}.", Link("tiledata-edit", Loc.T("Tiledata bearbeiten")))),
                T("tiledata-probleme", Loc.T("Problemarten"),
                    H(Loc.T("Art ohne Tiledata"), Loc.T("Es gibt eine Grafik, aber weder Name noch Flags.")) +
                    H(Loc.T("Tiledata ohne Art"), Loc.T("Name oder Flags sind gepflegt, aber es gibt keine Grafik.")) +
                    H(Loc.T("Wearable ohne AnimID"), Loc.T("Das Item hat das Wearable-Flag, aber keine AnimID und wird am Körper nicht gezeichnet.")) +
                    H(Loc.T("AnimID ohne Animation"), Loc.T("Die AnimID zeigt auf eine Body-ID, zu der es in keiner Quelle Frames gibt.")) +
                    H(Loc.T("Paperdoll-Gump fehlt"), Loc.T("Ein tragbares Item mit AnimID hat weder männlichen (50000 + AnimID) noch weiblichen Gump (60000 + AnimID).")) +
                    H(Loc.T("Wearable ohne Layer"), Loc.T("Das Wearable-Flag ist gesetzt, aber der Layer ist 0.")) +
                    H(Loc.T("ITEMDEF ohne Tiledata-Name"), Loc.T("Die Sphere-Skripte (falls angegeben) definieren das Item, die Tiledata hat aber keinen Namen dafür.")) +
                    Note(Loc.T("Nicht jedes Problem ist ein Fehler. Offizielle Bereiche sind teils absichtlich leer. Die Liste soll zeigen, wo man hinschauen muss."))),
                T("tiledata-edit", Loc.T("Tiledata bearbeiten"),
                    Loc.T("<p>Rechts neben der Liste steht ein Formular zum gewählten Eintrag. Änderungen gelten sofort im Programm, sind aber erst nach dem Speichern in der Datei.</p>") +
                    H(Loc.T("Name"), Loc.T("Höchstens 20 Zeichen, Latin-1.")) +
                    H(Loc.T("Gewicht"), Loc.T("0 bis 255. 255 bedeutet 'nicht aufhebbar'.")) +
                    H(Loc.T("Hoehe"), Loc.T("Höhe des Tiles, bestimmt Laufbarkeit und Stapeln.")) +
                    H(Loc.T("Layer"), Loc.T("Körperslot bei tragbaren Items.")) +
                    H(Loc.T("Menge"), Loc.T("Stapelmenge.")) +
                    H(Loc.T("AnimID"), Loc.T("Body-ID der Animation beim Tragen; zugleich Basis der Paperdoll-Gumps.")) +
                    H(Loc.T("Hue"), Loc.T("Standardfarbe.")) +
                    H(Loc.T("Licht"), Loc.T("Lichtart bei Lichtquellen.")) +
                    H(Loc.T("TexID"), Loc.T("Nur bei Land: Textur der Kachel.")) +
                    H(Loc.T("Flags"), Loc.T("Die 32 unteren Flag-Bits als Kästchen. Die oberen 32 Bits der 64-Bit-Flags bleiben unverändert erhalten.")) +
                    H(Loc.T("Speichern"), Loc.T("Schreibt alle geänderten Einträge in tiledata.mul. Zuvor wird die vorherige Datei unter %APPDATA%\\UOTinker\\backups gesichert. Die Zahl in Klammern ist die Menge der geänderten Einträge.")) +
                    Note(Loc.T("Der Button ist bei aktivem Schreibschutz gesperrt. Geänderte Einträge sind in der Liste mit * markiert.")) +
                    H(Loc.T("Werte kopieren"), Loc.T("Übernimmt Werte eines anderen Eintrags (ID dezimal oder 0x hex). Im Dialog wählst du, welche Felder kopiert werden: Name, Flags, Gewicht, Höhe, Layer, Menge, AnimID, Hue, Licht (bei Land: Name, Flags, TexID). Vorbelegt sind alles außer Name, AnimID und Hue. Items kopieren nur von Items, Land nur von Land.")) +
                    H(Loc.T("Neues Item anlegen"), Loc.T("Belegt einen freien Slot der Tiledata. Mit 'Nächster mit Art, ohne Tiledata' findest du Grafiken, die noch keine Eigenschaften haben; 'Nächster ganz freier' sucht einen Slot ohne Grafik und ohne Tiledata. Optional liefert eine Vorlage alle Werte außer dem Namen. Optional wählst du ein Bild; es wird im selben Schritt als Item-Art für diese ID vorgemerkt (Format und Hinweise siehe Item-Art bearbeiten). Mit 'Sofort speichern' werden Tiledata und Art gleich geschrieben, jeweils mit Sicherung; ohne bleiben beide als ungespeicherte Änderung stehen. Danach springt die Liste zum neuen Eintrag.")) +
                    Note(Loc.T("Ohne Bild meldet die Problemliste 'Tiledata ohne Art'. Ein belegter Slot (Tiledata oder Art) wird nur nach Rückfrage überschrieben. Ist das Bild nicht verwendbar, wird nichts geändert.")) +
                    H(Loc.T("ITEMDEF ..."), Loc.T("Nur bei Items. Öffnet ein Menü mit allen Sphere-Definitionen, die zu dieser ID gehören (Defname, Datei und Zeile). Ein Klick öffnet die Skriptdatei an der Zeile, bevorzugt in VS Code, sonst in Notepad++, sonst im Standardprogramm (dort ohne Zeilensprung). Gehört eine Definition zu einer anderen Item-ID (z. B. ein Alias mit ID= auf diese Grafik), gibt es zusätzlich 'zu Item ... springen', das in der Liste zu dem Item wechselt. Gibt es keine Definition, steht das im Menü. UOTinker ändert die Skripte nie selbst.")) +
                    H(Loc.T("Problemregeln ..."), Loc.T("Legt fest, welche Prüfungen als Problem gemeldet werden: Art ohne Tiledata, Tiledata ohne Art, Wearable ohne AnimID, AnimID ohne Animation, Paperdoll-Gump fehlt, Wearable ohne Layer und ITEMDEF ohne Tiledata-Name. Abgewählte Prüfungen verschwinden aus der Problem-Spalte, den Filtern, der Karte 'Items mit Lücken' und den Schnellaktionen. Das ist sinnvoll, wenn eine Prüfung bei dir bewusst nicht zutrifft, etwa 'AnimID ohne Animation' bei Items, die nur teilweise Animationen haben. Die Auswahl wird gespeichert und gilt für alle Datenordner; die Daten selbst ändern sich nicht.")) +
                    H(Loc.T("Eintrag zurücksetzen"), Loc.T("Stellt den gewählten Eintrag auf den Stand der Datei zurück.")) +
                    H(Loc.T("Alle verwerfen"), Loc.T("Verwirft alle ungespeicherten Änderungen.")) +
                    Note(Loc.T("Andere Kopien der Datei (zum Beispiel beim Server oder bei den Spielern) werden nicht automatisch angepasst; sie müssen nach dem Speichern wie gewohnt verteilt werden. Beim Beenden oder Neuladen fragt das Programm, wenn ungespeicherte Änderungen vorhanden sind.")))),
        };
    }
}

public sealed class HelpForm : Form
{
    private readonly TreeView _tree = new() { Dock = DockStyle.Left, Width = 250, BorderStyle = BorderStyle.None, BackColor = Theme.Sidebar, ForeColor = Theme.Text, Font = Theme.Ui, ShowLines = false, FullRowSelect = true, HideSelection = false, ItemHeight = 26 };
    private readonly WebBrowser _web = new() { Dock = DockStyle.Fill, ScriptErrorsSuppressed = true, IsWebBrowserContextMenuEnabled = false, AllowWebBrowserDrop = false };
    private readonly Dictionary<string, TreeNode> _nodes = new();
    private readonly Dictionary<string, HelpTopic> _topics = new();

    public HelpForm()
    {
        Text = Loc.T("UO Tinker - Hilfe");
        Width = 1000;
        Height = 720;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Theme.Bg;
        ShowInTaskbar = false;
        HandleCreated += (_, _) => Theme.DarkTitleBar(this);

        var edge = new Panel { Dock = DockStyle.Left, Width = 1, BackColor = Theme.Line };
        Controls.Add(_web);
        Controls.Add(edge);
        Controls.Add(_tree);
        Theme.DarkScroll(_tree);

        foreach (var t in HelpContent.Build())
        {
            AddNode(null, t);
        }
        _tree.ExpandAll();
        _tree.DrawMode = TreeViewDrawMode.OwnerDrawText;
        _tree.DrawNode += (_, e) =>
        {
            bool sel = (e.State & TreeNodeStates.Selected) != 0;
            var r = new Rectangle(0, e.Bounds.Y, _tree.ClientSize.Width, e.Bounds.Height);
            using (var b = new SolidBrush(sel ? Theme.Active : Theme.Sidebar))
            {
                e.Graphics.FillRectangle(b, r);
            }
            TextRenderer.DrawText(e.Graphics, e.Node!.Text, Theme.Ui, e.Bounds, sel ? Theme.Gold : Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
        };
        _tree.AfterSelect += (_, e) =>
        {
            if (e.Node?.Tag is string key && _topics.TryGetValue(key, out var t))
            {
                _web.DocumentText = Page(t.Html);
            }
        };
        _web.Navigating += (_, e) =>
        {
            string u = e.Url?.ToString() ?? "";
            if (u.StartsWith("topic:", StringComparison.OrdinalIgnoreCase))
            {
                e.Cancel = true;
                Open(u["topic:".Length..]);
            }
        };
        Shown += (_, _) =>
        {
            if (_tree.SelectedNode?.Tag is string k)
            {
                _web.DocumentText = Page(_topics[k].Html);
            }
        };
        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
            }
        };
    }

    private void AddNode(TreeNode? parent, HelpTopic t)
    {
        var n = new TreeNode(t.Title) { Tag = t.Key };
        (parent == null ? _tree.Nodes : parent.Nodes).Add(n);
        _nodes[t.Key] = n;
        _topics[t.Key] = t;
        foreach (var c in t.Children)
        {
            AddNode(n, c);
        }
    }

    public void Open(string key)
    {
        if (_nodes.TryGetValue(key, out var n))
        {
            bool same = _tree.SelectedNode == n;
            _tree.SelectedNode = n;
            n.EnsureVisible();
            if (same)
            {
                _web.DocumentText = Page(_topics[key].Html);
            }
        }
    }

    private static string Page(string body) => """
        <html><head><meta http-equiv="X-UA-Compatible" content="IE=edge"><meta charset="utf-8">
        <style>
        body { background:#1B1720; color:#E6E1EC; font-family:'Segoe UI',Tahoma,sans-serif; font-size:10pt; margin:22px 30px; line-height:1.45; word-wrap:break-word; overflow-x:hidden; }
        p { margin:0 0 14px 0; }
        .title { font-family:Georgia,serif; font-size:16pt; font-weight:bold; color:#E6E1EC; margin-bottom:18px; }
        .h { font-weight:bold; text-decoration:underline; color:#E0B563; }
        .note { font-style:italic; color:#6FCF8F; margin:0 0 14px 0; }
        a { color:#E0B563; }
        </style></head><body>
        """ + body + "</body></html>";
}
