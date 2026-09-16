# Ultra YouTube Downloader

> Standalone accessible YouTube downloader (video/MP4 or audio/MP3). Part of the Ultra Creative Suite — the same download engine that's built into UltraVideoEditor, as its own single-purpose app.

---

## What is this?

A small WPF desktop app (.NET 8) that downloads a video or audio track from a YouTube link using `yt-dlp`, with a UI built accessibility-first for JAWS and NVDA (live-region status announcements, full keyboard operation, high-contrast/light/dark themes, adjustable text size) and equally usable for sighted users.

- **Video (MP4)** or **Audio (MP3)**, with quality selection
- **Playlist handling**: pasting a link that's part of a playlist (very common — YouTube adds `&list=...` to a lot of links) never silently downloads the whole playlist. You get an explicit choice: just this video (the default when a specific video is in the link), the entire playlist, or hand-picked tracks
- **Self-installing dependencies**: if `yt-dlp` or `FFmpeg` aren't found, the app offers to download them automatically — no manual setup required

---

## Requirements

| Component | Note |
|---|---|
| Windows | 10 / 11 |
| .NET | 8.0 (`net8.0-windows`) — no extra NuGet packages needed |
| yt-dlp | Auto-downloaded on first use if missing |
| FFmpeg | Auto-downloaded on first use if missing (required for MP3 conversion and video/audio merging) |

## Download

Ready-to-run builds are published under [Releases](https://github.com/demirajvazi10-max/ultraYoutubeDownloader/releases) — download the installer from the latest `v*` release and run it. No separate .NET install needed (self-contained build).

## Build from source

```bash
git clone https://github.com/demirajvazi10-max/ultraYoutubeDownloader.git
cd ultraYoutubeDownloader
dotnet build -c Release
```

Or open `UltraYoutubeDownloader.slnx` in Visual Studio.

## Accessibility

- Every control has an explicit `AutomationProperties.Name`; status and progress use `LiveSetting="Polite"` so JAWS/NVDA announce updates without needing focus to move.
- **View menu**: Dark / Light / High-contrast theme, text/control size (also `Ctrl +` / `Ctrl -` / `Ctrl 0`), and **Language** (English / Serbian).
- On startup, the app checks for an active screen reader and switches to the high-contrast theme automatically (JAWS, NVDA, Narrator, ZoomText, Fusion, Dolphin/Supernova). This can always be overridden from the View menu, and the last chosen theme and language are remembered for next time.
- A global error handler shows unexpected errors instead of letting the app close silently — important for a blind user, who otherwise gets no signal at all that something went wrong.

---

## Author

Created by **Demir Ajvazi** — part of the [Ultra Creative Suite](https://github.com/demirajvazi10-max/Ultra-Creative-suite).

## License

GPL-3.0 License — see the [LICENSE](./LICENSE) file.
