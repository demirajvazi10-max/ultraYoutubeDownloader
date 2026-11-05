using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace UltraYoutubeDownloader
{
    /// <summary>
    /// Sve što radi sa yt-dlp, FFmpeg i Deno: pronalaženje, automatska instalacija,
    /// ažuriranje, čitanje informacija o linku i samo preuzimanje. Bez UI-ja.
    /// yt-dlp podržava preko 1000 sajtova (YouTube, TikTok, Instagram, Facebook, X,
    /// Vimeo, SoundCloud, Twitch, Dailymotion, Bandcamp...), pa i ova aplikacija.
    /// </summary>
    public static class DownloadEngine
    {
        public static string AppData => AppSettings.AppDataFolder;

        private static readonly HttpClient Http = CreateHttp();
        private static HttpClient CreateHttp()
        {
            var h = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
            h.DefaultRequestHeaders.Add("User-Agent", "UltraYoutubeDownloader/2.0");
            return h;
        }

        // ════════════════════════════════════════════════════════════════
        // ALATI
        // ════════════════════════════════════════════════════════════════
        public static string FindYtdlp()
        {
            foreach (var p in new[]
            {
                Path.Combine(AppData, "yt-dlp.exe"),                                   // naša kopija (ažurira se)
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "yt-dlp.exe"),
            })
                if (File.Exists(p)) return p;
            return CanRun("yt-dlp", "--version") ? "yt-dlp" : null;
        }

        public static string FindFfmpeg()
        {
            foreach (var p in new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Ffmpeg", "ffmpeg.exe"),
                Path.Combine(AppData, "Ffmpeg", "ffmpeg.exe"),
            })
                if (File.Exists(p)) return p;
            return CanRun("ffmpeg", "-version") ? "ffmpeg" : null;
        }

        /// <summary>Deno — JavaScript okruženje koje yt-dlp od kraja 2025. traži za pouzdano preuzimanje sa YouTube-a.</summary>
        public static string FindDeno()
        {
            string ours = Path.Combine(AppData, "Deno", "deno.exe");
            if (File.Exists(ours)) return ours;
            return CanRun("deno", "--version") ? "deno" : null;
        }

        private static bool CanRun(string exe, string args)
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo(exe, args)
                {
                    UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
                });
                if (p == null) return false;
                p.WaitForExit(4000);
                return p.HasExited && p.ExitCode == 0;
            }
            catch { return false; }
        }

        /// <summary>Verzija yt-dlp-a, npr. "2026.10.22" (prazno ako ne može da se pročita).</summary>
        public static string YtdlpVersion(string ytdlp)
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo(ytdlp, "--version")
                {
                    UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true
                });
                string v = p?.StandardOutput.ReadToEnd().Trim() ?? "";
                p?.WaitForExit(4000);
                return v;
            }
            catch { return ""; }
        }

        /// <summary>--js-runtimes postoji od yt-dlp 2025.11; starije verzije bi prijavile nepoznatu opciju.</summary>
        public static bool SupportsJsRuntimes(string version)
        {
            var m = Regex.Match(version ?? "", @"^(\d{4})\.(\d{2})");
            if (!m.Success) return false;
            int y = int.Parse(m.Groups[1].Value), mo = int.Parse(m.Groups[2].Value);
            return y > 2025 || (y == 2025 && mo >= 11);
        }

        public static async Task DownloadFileAsync(string url, string dest, Action<double, string> progress, string label, double scale = 1.0)
        {
            using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            long total = response.Content.Headers.ContentLength ?? -1;
            await using var stream = await response.Content.ReadAsStreamAsync();
            await using var file = File.Create(dest);
            var buffer = new byte[81920];
            long done = 0; int read; double lastReported = -1;
            while ((read = await stream.ReadAsync(buffer)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, read));
                done += read;
                if (total > 0)
                {
                    double pct = done * 100.0 / total * scale;
                    if (pct - lastReported >= 1) { lastReported = pct; progress?.Invoke(pct, $"{label}: {pct:F0}%  ({done / 1048576.0:F1} MB)"); }
                }
            }
        }

        public static async Task InstallYtdlpAsync(Action<double, string> progress)
        {
            Directory.CreateDirectory(AppData);
            string tmp = Path.Combine(AppData, "yt-dlp.exe.download");
            await DownloadFileAsync("https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe", tmp, progress, "yt-dlp");
            File.Move(tmp, Path.Combine(AppData, "yt-dlp.exe"), true);
        }

        public static async Task InstallFfmpegAsync(Action<double, string> progress, string extractingLabel)
        {
            string zip = Path.Combine(Path.GetTempPath(), $"ffmpeg_{Guid.NewGuid():N}.zip");
            try
            {
                await DownloadFileAsync("https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip", zip, progress, "FFmpeg", 0.9);
                progress?.Invoke(92, extractingLabel);
                string dir = Path.Combine(AppData, "Ffmpeg");
                Directory.CreateDirectory(dir);
                await Task.Run(() =>
                {
                    using var archive = ZipFile.OpenRead(zip);
                    // ffprobe treba yt-dlp-u za ugradnju omota i za neke konverzije
                    foreach (var name in new[] { "ffmpeg.exe", "ffprobe.exe" })
                    {
                        var entry = archive.Entries.FirstOrDefault(en =>
                            en.FullName.Replace('\\', '/').EndsWith("/bin/" + name, StringComparison.OrdinalIgnoreCase));
                        if (entry == null && name == "ffmpeg.exe")
                            throw new InvalidOperationException("ffmpeg.exe was not found inside the downloaded archive.");
                        entry?.ExtractToFile(Path.Combine(dir, name), overwrite: true);
                    }
                });
            }
            finally { try { File.Delete(zip); } catch { } }
        }

        public static async Task InstallDenoAsync(Action<double, string> progress)
        {
            string zip = Path.Combine(Path.GetTempPath(), $"deno_{Guid.NewGuid():N}.zip");
            try
            {
                await DownloadFileAsync("https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip", zip, progress, "Deno", 0.95);
                string dir = Path.Combine(AppData, "Deno");
                Directory.CreateDirectory(dir);
                await Task.Run(() =>
                {
                    using var archive = ZipFile.OpenRead(zip);
                    var entry = archive.Entries.FirstOrDefault(e => e.Name.Equals("deno.exe", StringComparison.OrdinalIgnoreCase))
                                ?? throw new InvalidOperationException("deno.exe was not found inside the downloaded archive.");
                    entry.ExtractToFile(Path.Combine(dir, "deno.exe"), overwrite: true);
                });
            }
            finally { try { File.Delete(zip); } catch { } }
        }

        /// <summary>Ažurira yt-dlp (sajtovi se stalno menjaju, pa je ovo najčešći lek kad preuzimanje prestane da radi).</summary>
        public static async Task<string> UpdateYtdlpAsync(string ytdlp, CancellationToken ct = default)
        {
            if (ytdlp == "yt-dlp") // iz PATH-a — ne diramo tuđu instalaciju, već pravimo svoju, najnoviju kopiju
            {
                await InstallYtdlpAsync(null);
                return YtdlpVersion(Path.Combine(AppData, "yt-dlp.exe"));
            }
            var sb = new StringBuilder();
            await RunAsync(ytdlp, new[] { "-U" }, l => sb.AppendLine(l), ct);
            return YtdlpVersion(ytdlp);
        }

        // ════════════════════════════════════════════════════════════════
        // INFORMACIJE O LINKU
        // ════════════════════════════════════════════════════════════════
        public static readonly Regex UrlRegex = new(@"https?://[^\s""'<>]+", RegexOptions.IgnoreCase);

        /// <summary>Svi linkovi iz bilo kog teksta (jedan po redu, razdvojeni razmakom, usred rečenice...). Bez duplikata.</summary>
        public static List<string> ExtractUrls(string text)
        {
            var list = new List<string>();
            foreach (Match m in UrlRegex.Matches(text ?? ""))
            {
                string u = m.Value.TrimEnd('.', ',', ';', ')', ']', '!', '?');
                if (!list.Contains(u, StringComparer.OrdinalIgnoreCase)) list.Add(u);
            }
            return list;
        }

        public static bool UrlHasSpecificVideoId(string url) =>
            Regex.IsMatch(url, @"[?&]v=[\w-]{6,}") || Regex.IsMatch(url, @"youtu\.be/[\w-]{6,}") || Regex.IsMatch(url, @"/shorts/[\w-]{6,}");

        /// <summary>Ime sajta iz adrese, za prikaz pre nego što stigne odgovor od yt-dlp-a.</summary>
        public static string SiteFromUrl(string url)
        {
            try
            {
                string host = new Uri(url).Host.ToLowerInvariant();
                if (host.StartsWith("www.")) host = host[4..];
                if (host.StartsWith("m.")) host = host[2..];
                return host switch
                {
                    "youtube.com" or "youtu.be" or "music.youtube.com" => "YouTube",
                    "tiktok.com" or "vm.tiktok.com" => "TikTok",
                    "instagram.com" => "Instagram",
                    "facebook.com" or "fb.watch" => "Facebook",
                    "x.com" or "twitter.com" => "X",
                    "vimeo.com" => "Vimeo",
                    "soundcloud.com" => "SoundCloud",
                    "twitch.tv" => "Twitch",
                    "dailymotion.com" => "Dailymotion",
                    "reddit.com" => "Reddit",
                    _ when host.EndsWith(".bandcamp.com") => "Bandcamp",
                    _ => host
                };
            }
            catch { return ""; }
        }

        public static async Task FetchInfoAsync(string ytdlp, DownloadItem item, AppSettings s, CancellationToken ct)
        {
            var args = new List<string> { "--dump-single-json", "--flat-playlist", "--no-warnings" };
            AddCookies(args, s);
            args.Add(item.Url);
            var sb = new StringBuilder();
            var err = new StringBuilder();
            int code = await RunAsync(ytdlp, args, null, ct, sb, err);
            string json = sb.ToString().Trim();
            if (code != 0 || json.Length == 0 || json[0] != '{')
                throw new InvalidOperationException(CleanError(err.ToString()) ?? "no response");

            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            item.Title = Str(r, "title");
            item.Channel = Str(r, "uploader") is { Length: > 0 } u ? u : Str(r, "channel");
            string extractor = Str(r, "extractor_key");
            if (!string.IsNullOrEmpty(extractor)) item.Site = PrettySite(extractor);
            if (Str(r, "_type") == "playlist" && r.TryGetProperty("entries", out var entries) && entries.ValueKind == JsonValueKind.Array)
            {
                item.IsPlaylist = true;
                item.EntryTitles.Clear();
                foreach (var e in entries.EnumerateArray())
                    item.EntryTitles.Add(Str(e, "title") is { Length: > 0 } t ? t : Str(e, "url"));
                item.Count = item.EntryTitles.Count;
                if (item.Count == 0 && r.TryGetProperty("playlist_count", out var pc) && pc.TryGetInt32(out int n)) item.Count = n;
            }
            else if (r.TryGetProperty("duration", out var d) && d.ValueKind == JsonValueKind.Number)
                item.DurationSec = d.GetDouble();
        }

        private static string PrettySite(string extractor)
        {
            // "Youtube", "YoutubeTab", "TikTok", "Instagram"... -> lepše ime
            string e = extractor;
            if (e.StartsWith("Youtube", StringComparison.OrdinalIgnoreCase)) return "YouTube";
            if (e.StartsWith("Twitter", StringComparison.OrdinalIgnoreCase)) return "X";
            foreach (var suffix in new[] { "Tab", "Playlist", "User", "Album", "Set", "Channel", "Search" })
                if (e.Length > suffix.Length && e.EndsWith(suffix)) { e = e[..^suffix.Length]; break; }
            return e;
        }

        private static string Str(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

        // ════════════════════════════════════════════════════════════════
        // PREUZIMANJE
        // ════════════════════════════════════════════════════════════════
        public sealed class Progress
        {
            public double Percent;
            public string Speed = "", Eta = "";
            public int Index, Total;        // za plejlistu: koji je po redu
            public string CurrentTitle = "";
        }

        /// <summary>Argumenti za yt-dlp. Odvojeno od pokretanja da bi se moglo proveriti (testovi).</summary>
        public static List<string> BuildArgs(DownloadItem item, AppSettings s, string ffmpeg, string deno, bool jsRuntimesSupported)
        {
            var f = item.Format;
            var a = new List<string>();

            if (f.AudioOnly)
            {
                a.AddRange(new[] { "--format", "bestaudio/best", "--extract-audio", "--audio-format", f.AudioFormat });
                if (f.AudioFormat is not ("flac" or "wav")) a.AddRange(new[] { "--audio-quality", f.AudioQuality });
            }
            else
            {
                string h = f.VideoQuality == "best" ? "" : $"[height<={f.VideoQuality}]";
                string fmt = f.VideoContainer switch
                {
                    // MP4: H.264/AAC radi svuda (telefon, TV, Windows plejer) — zato ga prvo tražimo
                    "mp4" => $"bv*{h}[ext=mp4]+ba[ext=m4a]/b{h}[ext=mp4]/bv*{h}+ba/b{h}",
                    "webm" => $"bv*{h}[ext=webm]+ba[ext=webm]/bv*{h}+ba/b{h}",
                    _ => $"bv*{h}+ba/b{h}"
                };
                // Ako sajt ne kaže visinu snimka (ili nema traženu), uzmi najbolje što postoji umesto greške
                if (h.Length > 0) fmt += "/bv*+ba/b";
                a.AddRange(new[] { "--format", fmt, "--merge-output-format", f.VideoContainer });
                // Kad sajt da gotov fajl u drugom formatu (bez spajanja), prepakuj ga bez gubitka kvaliteta
                if (f.VideoContainer is "mp4" or "mkv") a.AddRange(new[] { "--remux-video", f.VideoContainer });
            }

            if (s.EmbedMetadata) a.Add("--embed-metadata");
            // Omot ne može u WAV, a WebM ne podržava ugrađenu sličicu — tu ga preskačemo umesto da yt-dlp prijavi grešku
            bool thumbOk = f.AudioOnly ? f.AudioFormat is "mp3" or "m4a" or "flac" or "opus" : f.VideoContainer is "mp4" or "mkv";
            if (s.EmbedThumbnail && thumbOk) a.Add("--embed-thumbnail");

            if (!f.AudioOnly && s.Subtitles != "off")
            {
                a.Add("--write-subs");
                if (s.AutoSubtitles) a.Add("--write-auto-subs");
                string langs = string.Join(",", (s.SubtitleLanguages ?? "en").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(l => l.Contains('*') ? l : l + ".*"));
                a.AddRange(new[] { "--sub-langs", langs });
                if (s.Subtitles == "embed") a.Add("--embed-subs");
                else a.AddRange(new[] { "--convert-subs", "srt" });
            }

            // Plejlista
            if (item.IsPlaylist)
            {
                if (item.PlaylistScope == "single") a.Add("--no-playlist");
                else if (item.PlaylistScope != null && item.PlaylistScope != "all") a.AddRange(new[] { "--playlist-items", item.PlaylistScope });
                else a.Add("--yes-playlist");
            }
            else a.Add("--no-playlist");

            // Ime fajla i folder
            bool playlistDownload = item.IsPlaylist && item.PlaylistScope != "single";
            string name = s.FileNaming switch
            {
                "title_id" => "%(title)s [%(id)s]",
                "uploader_title" => "%(uploader,channel|Unknown)s - %(title)s",
                _ => "%(title)s"
            };
            if (playlistDownload && s.NumberPlaylistItems) name = "%(playlist_index)02d - " + name;
            string folder = "";
            if (s.SiteSubfolder) folder += "%(extractor_key)s" + Path.DirectorySeparatorChar;
            if (playlistDownload && s.PlaylistSubfolder) folder += "%(playlist_title,playlist_id|Playlist)s" + Path.DirectorySeparatorChar;
            a.AddRange(new[] { "--paths", f.SaveFolder, "--output", folder + name + ".%(ext)s" });
            a.Add("--windows-filenames");
            a.AddRange(new[] { "--trim-filenames", "180" });

            if (s.SkipAlreadyDownloaded)
                a.AddRange(new[] { "--download-archive", Path.Combine(AppData, "downloaded.txt") });

            AddCookies(a, s);

            if (ffmpeg != null && !ffmpeg.Equals("ffmpeg", StringComparison.OrdinalIgnoreCase))
                a.AddRange(new[] { "--ffmpeg-location", Path.GetDirectoryName(ffmpeg) });
            if (deno != null && jsRuntimesSupported)
                a.AddRange(new[] { "--js-runtimes", deno == "deno" ? "deno" : "deno:" + deno });

            // Čitljiv napredak i tačne putanje gotovih fajlova (umesto pogađanja iz teksta)
            a.AddRange(new[]
            {
                "--newline", "--no-warnings", "--progress", "--quiet", "--no-colors",
                "--progress-template", "download:UYDP|%(progress.downloaded_bytes)s|%(progress.total_bytes)s|%(progress.total_bytes_estimate)s|%(progress.speed)s|%(progress.eta)s",
                "--print", "before_dl:UYDS|%(playlist_index|)s|%(n_entries|)s|%(title)s",
                "--print", "after_move:UYDF|%(filepath)s",
            });
            a.Add("--");
            a.Add(item.Url);
            return a;
        }

        private static void AddCookies(List<string> a, AppSettings s)
        {
            if (!string.IsNullOrWhiteSpace(s.CookiesBrowser)) a.AddRange(new[] { "--cookies-from-browser", s.CookiesBrowser });
        }

        /// <summary>Pokreće preuzimanje jednog linka. Vraća listu gotovih fajlova; baca grešku sa porukom yt-dlp-a.</summary>
        public static async Task<List<string>> DownloadAsync(string ytdlp, List<string> args, Action<Progress> onProgress, CancellationToken ct)
        {
            var files = new List<string>();
            var prog = new Progress();
            var err = new StringBuilder();
            int code = await RunAsync(ytdlp, args, line =>
            {
                if (ParseLine(line, prog, files)) onProgress?.Invoke(prog);
            }, ct, null, err);
            if (code != 0 && files.Count == 0)
                throw new InvalidOperationException(CleanError(err.ToString()) ?? $"yt-dlp exit code {code}");
            return files;
        }

        /// <summary>Tumači jedan red izlaza. true = napredak se promenio.</summary>
        public static bool ParseLine(string line, Progress p, List<string> files)
        {
            if (string.IsNullOrEmpty(line)) return false;
            int i = line.IndexOf("UYD", StringComparison.Ordinal);
            if (i < 0) return false;
            line = line[i..].Trim();
            var parts = line.Split('|');
            switch (parts[0])
            {
                case "UYDP" when parts.Length >= 6:
                {
                    double done = Num(parts[1]), total = Num(parts[2]);
                    if (total <= 0) total = Num(parts[3]);
                    if (total > 0 && done >= 0) p.Percent = Math.Clamp(done * 100.0 / total, 0, 100);
                    double speed = Num(parts[4]);
                    p.Speed = speed > 0 ? FormatBytes(speed) + "/s" : "";
                    double eta = Num(parts[5]);
                    p.Eta = eta > 0 ? FormatTime(eta) : "";
                    return true;
                }
                case "UYDS" when parts.Length >= 4:
                    p.Index = (int)Math.Max(0, Num(parts[1]));
                    p.Total = (int)Math.Max(0, Num(parts[2]));
                    p.CurrentTitle = string.Join("|", parts.Skip(3));
                    p.Percent = 0; p.Speed = ""; p.Eta = "";
                    return true;
                case "UYDF" when parts.Length >= 2:
                {
                    string path = string.Join("|", parts.Skip(1)).Trim();
                    if (path.Length > 0 && !files.Contains(path)) files.Add(path);
                    p.Percent = 100;
                    return true;
                }
            }
            return false;
        }

        private static double Num(string s) =>
            double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : -1;

        public static string FormatBytes(double b) =>
            b >= 1073741824 ? $"{b / 1073741824:F1} GB" : b >= 1048576 ? $"{b / 1048576:F1} MB" : b >= 1024 ? $"{b / 1024:F0} KB" : $"{b:F0} B";

        public static string FormatTime(double secs)
        {
            if (secs <= 0) return "";
            var ts = TimeSpan.FromSeconds(secs);
            return ts.TotalHours >= 1 ? $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}" : $"{ts.Minutes}:{ts.Seconds:D2}";
        }

        /// <summary>Poslednja ERROR poruka yt-dlp-a, bez tehničkog prefiksa.</summary>
        public static string CleanError(string stderr)
        {
            var lines = (stderr ?? "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            var errLine = lines.LastOrDefault(l => l.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase)) ?? lines.LastOrDefault();
            if (errLine == null) return null;
            errLine = Regex.Replace(errLine, @"^ERROR:\s*", "");
            errLine = Regex.Replace(errLine, @"^\[[^\]]+\]\s*[\w-]+:\s*", "");
            return errLine.Length > 300 ? errLine[..300] + "…" : errLine;
        }

        // ════════════════════════════════════════════════════════════════
        // POKRETANJE PROCESA
        // ════════════════════════════════════════════════════════════════
        /// <summary>Spaja argumente u komandnu liniju po Windows pravilima navodnika.</summary>
        public static string Join(IEnumerable<string> args) => string.Join(" ", args.Select(Quote));

        private static string Quote(string a)
        {
            if (a.Length > 0 && a.IndexOfAny(new[] { ' ', '\t', '"', '&', '|', '<', '>', '^' }) < 0) return a;
            var sb = new StringBuilder("\"");
            int bs = 0;
            foreach (char c in a)
            {
                if (c == '\\') { bs++; continue; }
                if (c == '"') { sb.Append('\\', bs * 2 + 1); sb.Append('"'); bs = 0; continue; }
                sb.Append('\\', bs); bs = 0; sb.Append(c);
            }
            sb.Append('\\', bs * 2);
            sb.Append('"');
            return sb.ToString();
        }

        /// <summary>
        /// Pokreće proces, čita stdout i stderr red po red. Otkazivanje ODMAH gasi
        /// ceo stablo procesa (yt-dlp + ffmpeg koji je on pokrenuo) — u verziji 1.0
        /// "Otkaži" je čekao da se preuzimanje završi.
        /// </summary>
        public static async Task<int> RunAsync(string exe, IEnumerable<string> args, Action<string> onLine, CancellationToken ct,
            StringBuilder stdout = null, StringBuilder stderr = null)
        {
            var psi = new ProcessStartInfo(exe)
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
            };
            foreach (var a in args) psi.ArgumentList.Add(a); // .NET sam brine o navodnicima i razmacima
            psi.Environment["PYTHONIOENCODING"] = "utf-8"; // ćirilica i č/ć/š u naslovima

            using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            p.OutputDataReceived += (_, e) => { if (e.Data == null) return; lock (p) stdout?.AppendLine(e.Data); onLine?.Invoke(e.Data); };
            p.ErrorDataReceived += (_, e) => { if (e.Data == null) return; lock (p) stderr?.AppendLine(e.Data); onLine?.Invoke(e.Data); };
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            try
            {
                await p.WaitForExitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                try { p.Kill(entireProcessTree: true); } catch { }
                throw;
            }
            p.WaitForExit(); // isprazni ostatak izlaza
            return p.ExitCode;
        }
    }
}
