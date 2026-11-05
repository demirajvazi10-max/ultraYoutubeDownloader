# Ultra YouTube Downloader

> Accessible downloader for video and audio from YouTube and 1000+ other sites — one link or a whole list at once. Part of the Ultra family of apps.

**Version 2.0.0**

---

## What is this?

A WPF desktop app (.NET 8) that downloads video or audio using `yt-dlp`, with a UI built for the keyboard and screen readers (JAWS, NVDA, Narrator) and equally comfortable with a mouse.

Paste one link or ten, press Enter, and they download one after another into the folder and format you chose — no questions per link.

## Features

### Many links, one list
- Paste **one or many links** at once — one per line, or any text that contains links (an e-mail, a chat message, a web page). Duplicates are ignored.
- **Ctrl+Shift+V** adds every link from the clipboard from anywhere in the window; **File > Import links** reads them from a text file.
- Each link goes into the **download list** with its title, site, format and status ("in queue", "downloading 45%", "done", "failed: reason"). Downloads start right away (can be turned off in Settings), 1 to 3 at a time.
- **Stop**, **Remove**, **Retry failed**, **Clear finished**, and **Options for selected** (a different format or folder just for those links). Enter on a finished item opens its folder with the file selected.
- Spoken feedback when an item starts, finishes or fails, and one summary when the whole list is done — no constant chatter about percentages. Progress is in each item's text and in the overall progress bar.

### Set it once — it stays
- **Video or audio, quality, format and destination folder are remembered immediately** and used for every new link, also after restarting the app, until you change them.
- Video: best available, 4K, 1440p, 1080p, 720p, 480p, 360p — as **MP4** (plays everywhere), **MKV** or **WebM**.
- Audio: **MP3**, **M4A/AAC**, **Opus**, **FLAC** or **WAV**, with quality where it applies.
- Every item remembers the format that was active when it was added, so changing the defaults never changes links already in the list.

### More than YouTube
- YouTube (videos, Shorts, playlists, channels, YouTube Music), TikTok, Instagram, Facebook, X/Twitter, Vimeo, SoundCloud, Twitch, Dailymotion, Bandcamp, Reddit and [everything else yt-dlp supports](https://github.com/yt-dlp/yt-dlp/blob/master/supportedsites.md).
- **Login from your browser** (Settings > Login) for Instagram, Facebook, private and age-restricted videos — the app uses the sign-in you already have in Firefox, Edge, Chrome, Brave, Opera or Vivaldi.

### Playlists
- When a link is part of a playlist you choose, in Settings, once: **ask me every time**, **just that video**, or **the whole playlist**. When asked, you can also pick individual items.
- Playlist items can be numbered (01, 02, 03…) and saved in their own folder.

### Settings (Tools > Settings, Ctrl+,)
File name pattern (title, title with video ID, channel – title), a separate folder per site, embedded cover image and metadata (title, artist, date), subtitles (as .srt files or embedded, with languages and auto-generated subtitles), skip videos downloaded before, simultaneous downloads, a message and/or opening the folder when the list is finished, and weekly automatic yt-dlp updates. Everything is saved in `%LOCALAPPDATA%\UltraYoutubeDownloader\settings.json`.

### Tools install themselves
- **yt-dlp**, **FFmpeg** (with ffprobe) and **Deno** are downloaded automatically when needed, after asking once. Deno is the JavaScript runtime yt-dlp now uses for reliable YouTube downloads in every quality.
- yt-dlp is updated automatically once a week, and **Tools > Update yt-dlp** updates it on demand — the usual fix when a site changes and downloads start failing.
- Error messages explain the likely fix (update yt-dlp, use browser login, unsupported link).
- **Stop** stops a download immediately, including the FFmpeg process it started.

## Keyboard

| Shortcut | Action |
|---|---|
| Enter (in the links box) | Add the links to the list |
| Shift+Enter (in the links box) | New line |
| Ctrl+Shift+V | Add links from the clipboard |
| Enter (in the list) | Options for the item; on a finished item, open its folder |
| Delete (in the list) | Remove the item |
| Ctrl+, | Settings |
| Ctrl+ / Ctrl- / Ctrl+0 | Text size |
| F1 | Shortcut list |

Fields and buttons also have Alt+letter access keys (underlined letters).

## Requirements

| Component | Note |
|---|---|
| Windows | 10 / 11, 64-bit |
| .NET | 8.0 (`net8.0-windows`) — included in the installer build, no NuGet packages needed |
| yt-dlp | Auto-downloaded on first use |
| FFmpeg | Auto-downloaded on first use (audio conversion, joining video and audio) |
| Deno | Auto-downloaded on first YouTube download if you agree (recommended) |

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

- Every control has an accessible name; the download list reads each item as one sentence (number, title, site, format, status).
- Status messages are raised as live-region events, so JAWS and NVDA read them without moving focus.
- **View menu**: Dark / Light / High-contrast theme, text and control size, and **Language** (English / Serbian). On first start with a screen reader running, the high-contrast theme is chosen automatically.
- Unexpected errors are shown instead of the app closing silently.

## Supporting the project

The app is free. If you'd like to support its development, use [GitHub Sponsors](https://github.com/sponsors/demirajvazi10-max) — monthly or one-time. **No PayPal needed:** GitHub Sponsors is paid with an ordinary Visa or Mastercard payment card, credit or debit. You only need a free GitHub account.

The same step-by-step guide is inside the app: **Help > How to support this project** (with buttons to open the page or copy the link). The About window links to it too.

## Upgrading from 1.0

Theme and language settings carry over. The old "Load info → Download" steps are gone: adding a link is enough.

---

## Author

Created by **Demir Ajvazi**. Support development on [GitHub Sponsors](https://github.com/sponsors/demirajvazi10-max).

## License

GPL-3.0 License — see the [LICENSE](./LICENSE) file.
