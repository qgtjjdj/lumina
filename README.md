# Lumina

Moderner Bilderbetrachter für **Windows** und **Ubuntu** (und macOS). Eine Oberfläche, dieselbe Bedienung, nativ über [.NET 10](https://dotnet.microsoft.com/) und [Avalonia 12](https://avaloniaui.net/).

Dunkle Galerie, warmes Gold, Filmstreifen, Zoom am Cursor, Diashow, EXIF, Favoriten. Dateien werden nie gelöscht.

## Was es kann

- Ordner, einzelne Dateien, Drag-and-drop und Start per Dateipfad
- Nachbarbilder im selben Ordner, auch wenn du nur eine Datei öffnest
- Filmstreifen mit Vorschaubildern, Suche, natürliche Sortierung (`img2` vor `img10`)
- Unterordner, Zuletzt-geöffnet, Favoriten und Sterne
- Sterne und Favoriten liegen als `bild.jpg.lumina.xmp` neben der Datei. Eine vorhandene `bild.xmp` wird gelesen und nicht angefasst. Die Bilddatei selbst wird nie verändert
- Einpassen, Füllen, 100&nbsp;% (echte Dateipixel). Große Bilder kommen zuerst als Vorschau, die volle Auflösung lädt beim Heranzoomen
- Eingebettete Farbprofile werden nach sRGB gewandelt, der Farbraum steht in den Infos
- Weicher Übergang zwischen Bildern, der Filmstreifen rückt das aktuelle Bild in die Mitte
- Drehen, spiegeln, Stufe: Nacht, Tinte, Papier, Raster
- Diashow mit Tempo und Wiederholung, Fortschrittsbalken
- Animierte GIFs
- EXIF (Kamera, Objektiv, Belichtung, Blende, ISO, Brennweite, Farbraum), Histogramm, Farbpunkte
- Hell / Dunkel, Vollbild, eigene Fensterleiste
- Neue und gelöschte Dateien im Ordner rücken nach, ohne das aktuelle Bild wegzuschieben
- Einstellungen unter dem Benutzerprofil (`Lumina/settings.json`)

Formate: JPEG, PNG, GIF, WebP, BMP, TIFF, ICO, AVIF, HEIC, HEIF und RAW (CR2, CR3, NEF, ARW, ORF, RW2, DNG, RAF und weitere). HEIC und RAW laufen über die mitgelieferte ImageMagick-Bibliothek, dafür ist kein extra Paket nötig.

## Tasten

| Taste | Aktion |
| --- | --- |
| ← → | vorheriges / nächstes Bild |
| Leertaste | Diashow |
| Mausrad | Zoom am Cursor |
| Ziehen | Verschieben |
| Doppelklick | Einpassen ↔ 100 % |
| Mittelklick | Einpassen |
| + / − | Zoom |
| 0 / 1 / 2 | Einpassen / 100 % / Füllen |
| R, Umschalt+R | drehen |
| H / V | spiegeln |
| F, F11 | Vollbild |
| I | Infos |
| Entf | nur aus der Sitzung nehmen |
| Strg+O | Dateien |
| Strg+Umschalt+O | Ordner |
| Strg+C | Pfad kopieren |
| F1 | diese Hilfe |
| F5 | Ordner neu lesen |

## Starten

[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) reicht.

```bash
dotnet run --project src/Lumina
```

Eine Datei oder einen Ordner direkt öffnen:

```bash
dotnet run --project src/Lumina -- ~/Bilder
dotnet run --project src/Lumina -- ~/Bilder/urlaub.jpg
```

Unter Windows dieselbe Zeile, nur mit Windows-Pfad.

### Ubuntu

Avalonia spricht X11 und Wayland direkt an. Einmalig die Bibliotheken installieren:

```bash
sudo apt install -y libx11-6 libice6 libsm6 libfontconfig1 libxcursor1 libxext6 libxrandr2 libxrender1 libxi6 libgl1
```

Wayland ist in Ordnung. Wenn ein Fenster leer bleibt: die Sitzung auf X11 stellen oder `export AVALONIA_RENDERER=glx` versuchen.

## Veröffentlichen

Framework-abhängig (auf dem Zielrechner ist das .NET-10-Runtime nötig):

```bash
dotnet publish src/Lumina -c Release -r win-x64 --self-contained false -o publish/win-x64
dotnet publish src/Lumina -c Release -r linux-x64 --self-contained false -o publish/linux-x64
```

Ohne installiertes Runtime, dafür größer:

```bash
dotnet publish src/Lumina -c Release -r linux-x64 --self-contained true -o publish/linux-x64-full
dotnet publish src/Lumina -c Release -r win-x64 --self-contained true -o publish/win-x64-full
```

Start unter Linux: `./publish/linux-x64/Lumina`

## Tests

```bash
dotnet test
```

GitHub Actions baut und testet auf `ubuntu-latest` und `windows-latest`.

## Projekt

```
src/Lumina.Core     Katalog, natürliche Sortierung, EXIF, Größen
src/Lumina          Oberfläche, Decoder (Skia), Ansicht
tests/Lumina.Tests
```

Bewertungen liegen neben dem Bild (`name.jpg.lumina.xmp`) und zusätzlich im lokalen Profil, falls der Ordner schreibgeschützt ist. EXIF wird gelesen, nicht zurückgeschrieben. Bilddateien werden nie gelöscht oder überschrieben.
