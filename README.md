# Lumina

Moderner Bilderbetrachter für **Windows** und **Ubuntu** (und macOS). Eine Oberfläche, dieselbe Bedienung, nativ über [.NET 10](https://dotnet.microsoft.com/) und [Avalonia 12](https://avaloniaui.net/).

Dunkle Galerie, warmes Gold, Filmstreifen, Zoom am Cursor, Diashow, EXIF, Favoriten. Dateien werden nie gelöscht.

## Was es kann

- Ordner, einzelne Dateien, Drag-and-drop und Start per Dateipfad
- Nachbarbilder im selben Ordner, auch wenn du nur eine Datei öffnest
- Filmstreifen mit Vorschaubildern, Suche, natürliche Sortierung (`img2` vor `img10`)
- Unterordner, Zuletzt-geöffnet, Favoriten, lokale Sterne-Bewertung
- Einpassen, Füllen, 100&nbsp;% (echte Bildschirmpixel), Mausrad-Zoom zum Cursor, Ziehen, Doppelklick
- Drehen, spiegeln, Stufe: Nacht, Tinte, Papier, Raster
- Diashow mit Tempo und Wiederholung, Fortschrittsbalken
- Animierte GIFs
- EXIF (Kamera, Objektiv, Belichtung, Blende, ISO, Brennweite), Histogramm, Farbpunkte
- Hell / Dunkel, Vollbild, eigene Fensterleiste
- Ordner wird beobachtet und neu gelesen, wenn Dateien dazukommen
- Einstellungen unter dem Benutzerprofil (`Lumina/settings.json`)

Formate: JPEG, PNG, GIF, WebP, BMP, TIFF, ICO, AVIF (wenn der Decoder sie kann).

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

Bewertungen, Favoriten und das letzte Fenster liegen nur lokal. EXIF wird gelesen, nicht zurückgeschrieben.
