using System;
using System.IO;
using System.Text.Json;

namespace UltraYoutubeDownloader
{
    /// <summary>
    /// Sva podešavanja na jednom mestu, u settings.json u AppData folderu.
    /// Čuva se ODMAH pri svakoj izmeni (ne tek pri izlazu), pa šta god korisnik
    /// izabere — folder, video/audio, format, kvalitet — ostaje podrazumevano
    /// za svaki sledeći link i svako sledeće pokretanje, dok to sam ne promeni.
    /// </summary>
    public class AppSettings
    {
        // ── Osnovno (vidi se na glavnom prozoru) ──
        public string SaveFolder { get; set; } = "";
        public bool AudioOnly { get; set; }
        public string VideoQuality { get; set; } = "best";   // best, 2160, 1440, 1080, 720, 480, 360
        public string VideoContainer { get; set; } = "mp4";  // mp4, mkv, webm
        public string AudioFormat { get; set; } = "mp3";     // mp3, m4a, opus, flac, wav
        public string AudioQuality { get; set; } = "192K";   // 0 (najbolje), 320K, 192K, 128K

        // ── Podešavanja (Alatke > Podešavanja) ──
        public string PlaylistPolicy { get; set; } = "ask";  // ask, single, all
        public bool AutoStart { get; set; } = true;          // kreni čim se link doda
        public int SimultaneousDownloads { get; set; } = 1;  // 1..3
        public string FileNaming { get; set; } = "title";    // title, title_id, uploader_title
        public bool PlaylistSubfolder { get; set; } = true;  // plejlista u svoj podfolder
        public bool NumberPlaylistItems { get; set; } = true;// 01 - naslov
        public bool SiteSubfolder { get; set; }              // YouTube\, TikTok\...
        public bool EmbedThumbnail { get; set; } = true;     // omot u MP3/M4A/MP4
        public bool EmbedMetadata { get; set; } = true;      // naslov, izvođač, datum
        public string Subtitles { get; set; } = "off";       // off, files, embed
        public string SubtitleLanguages { get; set; } = "sr,hr,bs,en";
        public bool AutoSubtitles { get; set; }              // i automatski generisani titlovi
        public string CookiesBrowser { get; set; } = "";     // "", firefox, edge, chrome, brave, opera, vivaldi
        public bool SkipAlreadyDownloaded { get; set; }      // --download-archive
        public bool OpenFolderWhenDone { get; set; }
        public bool MessageWhenDone { get; set; } = true;    // jedna poruka kad se cela lista završi
        public bool AutoUpdateYtdlp { get; set; } = true;    // jednom nedeljno, tiho
        public DateTime LastYtdlpUpdate { get; set; } = DateTime.MinValue;
        public bool DenoDeclined { get; set; }

        // ── Izgled ──
        public string Theme { get; set; } = "";
        public string Language { get; set; } = "";

        public static string AppDataFolder { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "UltraYoutubeDownloader");

        public static string DefaultDownloadFolder { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "UltraYoutubeDownloader_Downloads");

        private static string FilePath => Path.Combine(AppDataFolder, "settings.json");

        private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

        public static AppSettings Load()
        {
            AppSettings s;
            try
            {
                s = File.Exists(FilePath)
                    ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings()
                    : new AppSettings();
            }
            catch { s = new AppSettings(); }

            // Stari .cfg fajlovi iz verzije 1.0 — da tema i jezik ne nestanu posle nadogradnje
            try
            {
                string theme = Path.Combine(AppDataFolder, "theme.cfg");
                if (string.IsNullOrEmpty(s.Theme) && File.Exists(theme)) s.Theme = File.ReadAllText(theme).Trim();
                string lang = Path.Combine(AppDataFolder, "language.cfg");
                if (string.IsNullOrEmpty(s.Language) && File.Exists(lang)) s.Language = File.ReadAllText(lang).Trim();
            }
            catch { }

            if (string.IsNullOrWhiteSpace(s.SaveFolder)) s.SaveFolder = DefaultDownloadFolder;
            s.SimultaneousDownloads = Math.Clamp(s.SimultaneousDownloads, 1, 3);
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(AppDataFolder);
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(this, JsonOpts));
                File.Move(tmp, FilePath, true);
            }
            catch { /* podešavanja nisu kritična — nikad ne ruši aplikaciju zbog njih */ }
        }

        /// <summary>Kopija samo izbora formata — svaki link u listi pamti izbor koji je važio kad je dodat.</summary>
        public FormatChoice CurrentFormat() => new()
        {
            AudioOnly = AudioOnly, VideoQuality = VideoQuality, VideoContainer = VideoContainer,
            AudioFormat = AudioFormat, AudioQuality = AudioQuality, SaveFolder = SaveFolder
        };
    }

    public class FormatChoice
    {
        public bool AudioOnly { get; set; }
        public string VideoQuality { get; set; } = "best";
        public string VideoContainer { get; set; } = "mp4";
        public string AudioFormat { get; set; } = "mp3";
        public string AudioQuality { get; set; } = "192K";
        public string SaveFolder { get; set; } = "";

        public FormatChoice Clone() => (FormatChoice)MemberwiseClone();

        /// <summary>Kratak opis za listu: "MP3 192 kbps" ili "MP4 1080p".</summary>
        public string Short(Func<string, string> L)
        {
            if (AudioOnly)
            {
                string q = AudioFormat is "flac" or "wav" ? "" :
                           AudioQuality == "0" ? " " + L("q_short_best") : " " + AudioQuality.Replace("K", " kbps");
                return AudioFormat.ToUpperInvariant() + q;
            }
            return VideoContainer.ToUpperInvariant() + " " + (VideoQuality == "best" ? L("q_short_best") : VideoQuality + "p");
        }
    }
}
