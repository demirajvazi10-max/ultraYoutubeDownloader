using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Controls;
using WpfMessageBox = System.Windows.MessageBox;
using WinForms = System.Windows.Forms;

namespace UltraYoutubeDownloader
{
    /// <summary>
    /// Ultra YouTube Downloader — standalone verzija YouTube downloader-a iz
    /// UltraVideoEditor-a. Ista funkcionalnost i pristupačnost (JAWS/NVDA), samo
    /// bez zavisnosti od timeline-a: ovo je glavni (jedini) prozor aplikacije.
    ///
    /// Za razliku od dijalog-verzije, ova aplikacija sama preuzima i FFmpeg
    /// (isto kao što već radi za yt-dlp) ako ga ne pronađe — jer je ovo jedina
    /// stvar koju ova aplikacija radi, nema smisla da korisnik ručno postavlja
    /// zavisnosti da bi je pokrenuo prvi put.
    /// </summary>
    public partial class MainWindow : Window
    {
        private CancellationTokenSource _cts;
        private bool _downloadInProgress = false;
        private bool _fetchingInfo = false;
        private string _currentTheme = "dark";
        private string _currentLanguage = "en";

        // Sopstveni AppData folder — nezavisan od UltraVideoEditor-a, da ove dve
        // aplikacije ne dele/kvare jedna drugoj yt-dlp/ffmpeg kopije ili podešavanja.
        private static readonly string AppDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UltraYoutubeDownloader");

        private static readonly string[] YtdlpSearchPaths = {
            "yt-dlp.exe",
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "yt-dlp.exe"),
            Path.Combine(AppDataFolder, "yt-dlp.exe")
        };

        // FFmpeg: prvo tražimo pored .exe-a (za korisnike koji ga ručno kopiraju,
        // isto kao UltraVideoEditor), pa u AppData (gde ga ova app sama instalira
        // ako ga ne nađe — vidi OfferFfmpegInstallAsync).
        private static readonly string[] FfmpegSearchPaths = {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Ffmpeg", "ffmpeg.exe"),
            Path.Combine(AppDataFolder, "Ffmpeg", "ffmpeg.exe")
        };

        private static readonly string DownloadFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            "UltraYoutubeDownloader_Downloads");

        private string _currentSaveFolder = DownloadFolder;
        private VideoInfo _lastInfo;
        private bool _lastUrlHasSpecificVideo;

        public MainWindow()
        {
            InitializeComponent();
            UiScaling.Register(this);

            Directory.CreateDirectory(DownloadFolder);
            Directory.CreateDirectory(AppDataFolder);
            txtSaveFolder.Text = _currentSaveFolder;

            LoadSavedLanguage();
            LoadSavedTheme();
            DetectAndApplyInitialMode();
            SetupKeyboardShortcuts();

            ApplyLanguage();
            Loaded += (_, _) => txtUrl.Focus();
        }

        private string L(string key) => LanguageManager.GetText(key, _currentLanguage);

        // ═════════════════════════════════════════════════════════════════════
        //  MENI: TEME, JEZIK, VELIČINA TEKSTA, POMOĆ
        // ═════════════════════════════════════════════════════════════════════

        private void SetupKeyboardShortcuts()
        {
            PreviewKeyDown += (s, e) =>
            {
                bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
                if (!ctrl) return;

                if (e.Key == Key.OemPlus || e.Key == Key.Add) { LargerText_Click(s, e); e.Handled = true; }
                else if (e.Key == Key.OemMinus || e.Key == Key.Subtract) { SmallerText_Click(s, e); e.Handled = true; }
                else if (e.Key == Key.D0 || e.Key == Key.NumPad0) { ResetText_Click(s, e); e.Handled = true; }
            };
        }

        private void LanguageMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem mi) return;
            _currentLanguage = mi.Tag?.ToString() ?? "en";
            ApplyLanguage();

            try { File.WriteAllText(Path.Combine(AppDataFolder, "language.cfg"), _currentLanguage); }
            catch { }

            string plainName = _currentLanguage == "sr" ? L("lang_sr").Replace("_", "") : L("lang_en").Replace("_", "");
            Announce(string.Format(L("language_announce"), plainName));
        }

        private void LoadSavedLanguage()
        {
            try
            {
                string p = Path.Combine(AppDataFolder, "language.cfg");
                if (File.Exists(p))
                {
                    string lang = File.ReadAllText(p).Trim();
                    if (lang == "en" || lang == "sr")
                        _currentLanguage = lang;
                }
            }
            catch { }
        }

        /// <summary>Postavlja sav prevodivi tekst u prozoru na trenutno izabrani jezik.
        /// Pozvano pri pokretanju i svaki put kad korisnik promeni jezik iz menija.</summary>
        private void ApplyLanguage()
        {
            Title = L("window_title");
            AutomationProperties.SetName(this, L("window_aria"));

            txbAppTitle.Text = L("window_title");
            txbAppSubtitle.Text = L("app_subtitle");

            mnuViewRoot.Header = L("menu_view");
            AutomationProperties.SetName(mnuViewRoot, L("menu_view_aria"));
            mnuThemeRoot.Header = L("menu_theme");
            mnuThemeDark.Header = L("theme_dark");
            AutomationProperties.SetName(mnuThemeDark, L("theme_dark_aria"));
            mnuThemeLight.Header = L("theme_light");
            AutomationProperties.SetName(mnuThemeLight, L("theme_light_aria"));
            mnuThemeContrast.Header = L("theme_contrast");
            AutomationProperties.SetName(mnuThemeContrast, L("theme_contrast_aria"));

            mnuTextSizeRoot.Header = L("menu_textsize");
            mnuLarger.Header = L("text_larger");
            AutomationProperties.SetName(mnuLarger, L("larger_text_aria"));
            mnuSmaller.Header = L("text_smaller");
            AutomationProperties.SetName(mnuSmaller, L("smaller_text_aria"));
            mnuReset.Header = L("text_reset");
            AutomationProperties.SetName(mnuReset, L("reset_text_aria"));

            mnuLanguageRoot.Header = L("menu_language");
            AutomationProperties.SetName(mnuLanguageRoot, L("menu_language_aria"));
            mnuLangEn.Header = L("lang_en");
            AutomationProperties.SetName(mnuLangEn, L("lang_en_aria"));
            mnuLangEn.IsChecked = _currentLanguage == "en";
            mnuLangSr.Header = L("lang_sr");
            AutomationProperties.SetName(mnuLangSr, L("lang_sr_aria"));
            mnuLangSr.IsChecked = _currentLanguage == "sr";

            mnuOpenFolderItem.Header = L("menu_open_folder");
            AutomationProperties.SetName(mnuOpenFolderItem, L("open_folder_menu_aria"));
            mnuHelpRoot.Header = L("menu_help");
            AutomationProperties.SetName(mnuHelpRoot, L("menu_help_aria"));
            mnuAboutItem.Header = L("menu_about");
            AutomationProperties.SetName(mnuAboutItem, L("about_aria"));

            lblUrl.Content = L("url_label");
            AutomationProperties.SetName(txtUrl, L("url_aria"));
            AutomationProperties.SetHelpText(txtUrl, L("url_help"));

            grpFileType.Header = L("filetype_group");
            AutomationProperties.SetName(grpFileType, L("filetype_aria"));
            rbVideo.Content = L("video_option");
            AutomationProperties.SetName(rbVideo, L("video_aria"));
            rbAudio.Content = L("audio_option");
            AutomationProperties.SetName(rbAudio, L("audio_aria"));

            lblQuality.Content = L("quality_label");
            AutomationProperties.SetName(cmbQuality, L("quality_aria"));
            cmbQBest.Content = L("q_best");
            cmbQ1080.Content = L("q_1080");
            cmbQ720.Content = L("q_720");
            cmbQ480.Content = L("q_480");
            cmbQ360.Content = L("q_360");

            lblAudioQuality.Content = L("audioq_label");
            AutomationProperties.SetName(cmbAudioQuality, L("audioq_aria"));
            cmbAQBest.Content = L("aq_best");
            cmbAQ320.Content = L("aq_320");
            cmbAQ192.Content = L("aq_192");
            cmbAQ128.Content = L("aq_128");

            lblSaveFolder.Content = L("savefolder_label");
            AutomationProperties.SetName(txtSaveFolder, L("savefolder_aria"));
            btnBrowseFolder.Content = L("browse_btn");
            AutomationProperties.SetName(btnBrowseFolder, L("browse_aria"));
            btnOpenFolder.Content = L("open_btn");
            AutomationProperties.SetName(btnOpenFolder, L("open_btn_aria"));

            txbPlaylistPrompt.Text = L("playlist_prompt");
            grpScope.Header = L("scope_group");
            AutomationProperties.SetName(grpScope, L("scope_group_aria"));
            rbScopeSingle.Content = L("scope_single");
            AutomationProperties.SetName(rbScopeSingle, L("scope_single_aria"));
            rbScopeAll.Content = L("scope_all");
            AutomationProperties.SetName(rbScopeAll, L("scope_all_aria"));
            rbScopeChoose.Content = L("scope_choose");
            AutomationProperties.SetName(rbScopeChoose, L("scope_choose_aria"));
            AutomationProperties.SetName(lstPlaylistTracks, L("tracks_aria"));

            AutomationProperties.SetName(prgDownload, L("progress_aria"));

            btnFetchInfo.Content = _fetchingInfo ? L("btn_loadinfo_loading") : L("btn_loadinfo");
            AutomationProperties.SetName(btnFetchInfo, L("btn_loadinfo_aria"));
            btnDownload.Content = L("btn_download");
            AutomationProperties.SetName(btnDownload, L("btn_download_aria"));
            btnCancel.Content = L("btn_cancel");
            AutomationProperties.SetName(btnCancel, L("btn_cancel_aria"));

            txbAppStatus.Text = $"{L("window_title")} {GetAppVersion()}  •  {L("status_by")}";
        }

        private void ThemeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem mi) return;
            _currentTheme = mi.Tag?.ToString() ?? "dark";
            ApplyTheme();

            string plainName = _currentTheme switch
            {
                "contrast" => L("theme_contrast_plain"),
                "light"    => L("theme_light_plain"),
                _          => L("theme_dark_plain")
            };
            Announce(string.Format(L("theme_announce"), plainName));
        }

        private void ApplyTheme()
        {
            string themeFile = _currentTheme switch
            {
                "contrast" => "Themes/Theme.Contrast.xaml",
                "light"    => "Themes/Theme.Light.xaml",
                _          => "Themes/Theme.Dark.xaml"
            };

            try
            {
                var newDict = new ResourceDictionary { Source = new Uri(themeFile, UriKind.Relative) };

                var merged = System.Windows.Application.Current.Resources.MergedDictionaries;
                merged.Clear();
                merged.Add(newDict);

                if (mnuThemeDark != null)
                {
                    mnuThemeDark.IsChecked = _currentTheme == "dark";
                    mnuThemeLight.IsChecked = _currentTheme == "light";
                    mnuThemeContrast.IsChecked = _currentTheme == "contrast";
                }

                try { File.WriteAllText(Path.Combine(AppDataFolder, "theme.cfg"), _currentTheme); }
                catch { }
            }
            catch (Exception ex)
            {
                Announce($"{ex.Message}");
            }
        }

        private void LoadSavedTheme()
        {
            try
            {
                string p = Path.Combine(AppDataFolder, "theme.cfg");
                if (File.Exists(p))
                {
                    string t = File.ReadAllText(p).Trim();
                    if (t == "contrast" || t == "light" || t == "dark")
                        _currentTheme = t;
                }
            }
            catch { }
            ApplyTheme();
        }

        /// <summary>Pri pokretanju: ako je čitač ekrana aktivan, prelazi na high-contrast temu
        /// (isto ponašanje kao UltraVideoEditor), sem ako je korisnik već ručno sačuvao drugu temu.</summary>
        private void DetectAndApplyInitialMode()
        {
            bool screenReaderDetected = false;
            try { screenReaderDetected = ScreenReaderDetector.IsScreenReaderActive(); }
            catch { }

            bool hasSavedTheme = File.Exists(Path.Combine(AppDataFolder, "theme.cfg"));
            if (screenReaderDetected && !hasSavedTheme)
            {
                _currentTheme = "contrast";
                ApplyTheme();
            }
        }

        private void LargerText_Click(object sender, RoutedEventArgs e)
        {
            UiScaling.SetScale(UiScaling.CurrentScale + 0.1);
            Announce(string.Format(L("textsize_announce"), UiScaling.CurrentScale.ToString("P0")));
        }

        private void SmallerText_Click(object sender, RoutedEventArgs e)
        {
            UiScaling.SetScale(UiScaling.CurrentScale - 0.1);
            Announce(string.Format(L("textsize_announce"), UiScaling.CurrentScale.ToString("P0")));
        }

        private void ResetText_Click(object sender, RoutedEventArgs e)
        {
            UiScaling.SetScale(1.0);
            Announce(string.Format(L("textsize_announce"), "100%"));
        }

        private void MnuOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(_currentSaveFolder);
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_currentSaveFolder}\"") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                ShowError(string.Format(L("err_open_folder"), ex.Message));
            }
        }

        private void MnuAbout_Click(object sender, RoutedEventArgs e)
        {
            WpfMessageBox.Show(
                string.Format(L("about_text"), GetAppVersion()),
                L("about_title"),
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private static string GetAppVersion()
        {
            try
            {
                var v = Assembly.GetExecutingAssembly().GetName().Version;
                return v == null ? "" : $"v{v.Major}.{v.Minor}.{v.Build}";
            }
            catch { return ""; }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  UI EVENT HANDLERS
        // ═════════════════════════════════════════════════════════════════════

        // Puno kvalifikovano ime je neophodno: UseWindowsForms=true (zbog FolderBrowserDialog-a)
        // uvodi System.Windows.Forms.KeyEventArgs u implicitne using-e, pa goli "KeyEventArgs"
        // postaje dvosmislen između System.Windows.Forms i System.Windows.Input.
        private void TxtUrl_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter) BtnFetchInfo_Click(sender, e);
        }

        private void BtnBrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            using var dlg = new WinForms.FolderBrowserDialog
            {
                Description = "Choose where to save downloaded files",
                UseDescriptionForTitle = true,
                SelectedPath = Directory.Exists(_currentSaveFolder) ? _currentSaveFolder : DownloadFolder
            };

            if (dlg.ShowDialog() == WinForms.DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.SelectedPath))
            {
                _currentSaveFolder = dlg.SelectedPath;
                txtSaveFolder.Text = _currentSaveFolder;
            }
        }

        private void FormatType_Changed(object sender, RoutedEventArgs e)
        {
            if (pnlQuality == null || pnlAudioQuality == null) return;
            bool isVideo = rbVideo.IsChecked == true;
            pnlQuality.Visibility      = isVideo ? Visibility.Visible : Visibility.Collapsed;
            pnlAudioQuality.Visibility = isVideo ? Visibility.Collapsed : Visibility.Visible;
        }

        /// <summary>Prikazuje/sakriva listu pojedinačnih numera kada korisnik izabere "Choose specific tracks".</summary>
        private void PlaylistScope_Changed(object sender, RoutedEventArgs e)
        {
            if (lstPlaylistTracks == null) return;
            bool choosingTracks = rbScopeChoose.IsChecked == true;
            lstPlaylistTracks.Visibility = choosingTracks ? Visibility.Visible : Visibility.Collapsed;
            if (!choosingTracks)
                lstPlaylistTracks.UnselectAll();

            if (choosingTracks)
                Announce(L("choose_tracks_hint"));
        }

        private async void BtnFetchInfo_Click(object sender, RoutedEventArgs e)
        {
            string url = txtUrl.Text.Trim();
            if (string.IsNullOrEmpty(url))
            {
                Announce(L("err_url_empty"));
                txtUrl.Focus();
                return;
            }

            SetFetchingState(true);
            pnlInfo.Visibility = Visibility.Collapsed;
            btnDownload.IsEnabled = false;

            try
            {
                string ytdlp = FindYtdlp();
                if (ytdlp == null)
                {
                    bool installed = await OfferYtdlpInstallAsync();
                    if (!installed) return;
                    ytdlp = FindYtdlp();
                    if (ytdlp == null) { ShowError(L("err_ytdlp_not_found_after_install")); return; }
                }

                var info = await FetchInfoAsync(ytdlp, url);
                if (info == null) return;
                _lastInfo = info;
                _lastUrlHasSpecificVideo = UrlHasSpecificVideoId(url);

                txbInfoTitle.Text = info.IsPlaylist
                    ? string.Format(L("info_playlist_title"), info.Title, info.Count)
                    : string.Format(L("info_video_title"), info.Title);

                txbInfoMeta.Text = info.IsPlaylist
                    ? string.Format(L("info_meta_playlist"), info.Channel)
                    : string.Format(L("info_meta_video"), info.Channel, FormatDuration(info.DurationSec));

                if (info.IsPlaylist)
                {
                    // Link sadrži "list=" — može biti pojedinačan video otvoren unutar plejliste
                    // (najčešći slučaj kad se link kopira dok se gleda video) ili prava plejlista.
                    // Dajemo eksplicitan izbor umesto da nagađamo, sa bezbednim default-om: ako
                    // link ima i konkretan video (v=...), default je "samo ovaj video" — tako da
                    // slučajno kopiran link nikad ne skine celu plejlistu bez pitanja.
                    rbScopeSingle.Visibility = _lastUrlHasSpecificVideo ? Visibility.Visible : Visibility.Collapsed;
                    rbScopeChoose.Visibility = info.EntryTitles.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

                    lstPlaylistTracks.ItemsSource = info.EntryTitles;
                    lstPlaylistTracks.Visibility = Visibility.Collapsed;
                    lstPlaylistTracks.UnselectAll();

                    if (_lastUrlHasSpecificVideo)
                        rbScopeSingle.IsChecked = true;
                    else
                        rbScopeAll.IsChecked = true;

                    pnlPlaylistTracks.Visibility = Visibility.Visible;
                }
                else
                {
                    lstPlaylistTracks.ItemsSource = null;
                    pnlPlaylistTracks.Visibility = Visibility.Collapsed;
                }

                pnlInfo.Visibility = Visibility.Visible;
                btnDownload.IsEnabled = true;

                string announcement;
                if (info.IsPlaylist && _lastUrlHasSpecificVideo)
                    announcement = string.Format(L("playlist_found_specific"), info.Title, info.Count);
                else if (info.IsPlaylist)
                    announcement = string.Format(L("playlist_found_generic"), info.Title, info.Count);
                else
                    announcement = string.Format(L("video_found"), info.Title, FormatDuration(info.DurationSec));

                Announce(announcement);
                btnDownload.Focus();
            }
            catch (Exception ex)
            {
                ShowError(string.Format(L("err_prefix"), ex.Message));
            }
            finally
            {
                SetFetchingState(false);
            }
        }

        private async void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            string url = txtUrl.Text.Trim();
            if (string.IsNullOrEmpty(url)) return;

            string ytdlp = FindYtdlp();
            if (ytdlp == null) { ShowError(L("err_ytdlp_not_found")); return; }

            bool isAudio = rbAudio.IsChecked == true;

            // MP3 ekstrakcija (i spajanje video+audio streamova) zahteva FFmpeg.
            string ffmpeg = FindFfmpeg();
            if (ffmpeg == null)
            {
                bool installed = await OfferFfmpegInstallAsync();
                if (!installed) return;
                ffmpeg = FindFfmpeg();
                if (ffmpeg == null) { ShowError(L("err_ffmpeg_not_found_after_install")); return; }
            }

            Directory.CreateDirectory(_currentSaveFolder);

            string quality = ((ComboBoxItem)cmbQuality.SelectedItem)?.Tag?.ToString() ?? "best";
            string audioQuality = ((ComboBoxItem)cmbAudioQuality.SelectedItem)?.Tag?.ToString() ?? "192K";

            bool linkIsPlaylist = _lastInfo?.IsPlaylist ?? false;

            bool wantsSingleVideo = linkIsPlaylist
                && rbScopeSingle.Visibility == Visibility.Visible
                && rbScopeSingle.IsChecked == true;

            bool isPlaylistDownload = linkIsPlaylist && !wantsSingleVideo;

            List<int> selectedTrackIndices = null;
            if (isPlaylistDownload && rbScopeChoose.IsChecked == true)
            {
                if (lstPlaylistTracks.SelectedItems.Count == 0)
                {
                    string msg = L("err_select_track") +
                        (rbScopeSingle.Visibility == Visibility.Visible ? L("err_select_track_or_single") : L("err_select_track_period"));
                    ShowError(msg);
                    return;
                }

                // yt-dlp --playlist-items is 1-based.
                selectedTrackIndices = lstPlaylistTracks.SelectedItems
                    .Cast<string>()
                    .Select(title => lstPlaylistTracks.Items.IndexOf(title) + 1)
                    .OrderBy(i => i)
                    .ToList();
            }

            _cts = new CancellationTokenSource();
            SetDownloadState(true);

            try
            {
                var files = await RunDownloadAsync(ytdlp, ffmpeg, url, isAudio, quality, audioQuality,
                    isPlaylistDownload, selectedTrackIndices, _cts.Token);

                if (files.Count > 0)
                {
                    string savedMsg = files.Count == 1
                        ? string.Format(L("saved_to"), files[0])
                        : string.Format(L("saved_multi"), files.Count, _currentSaveFolder);

                    Announce(string.Format(L("download_complete_announce"), savedMsg));
                    WpfMessageBox.Show(savedMsg, L("download_complete_title"), MessageBoxButton.OK, MessageBoxImage.Information);

                    // Za razliku od dijalog-verzije, prozor ostaje otvoren — ovo je
                    // glavna aplikacija, ne modalni dijalog. Spremamo je za sledeći download.
                    txtUrl.Clear();
                    pnlInfo.Visibility = Visibility.Collapsed;
                    btnDownload.IsEnabled = false;
                    txtUrl.Focus();
                }
                else
                {
                    ShowError(L("err_download_no_files"));
                }
            }
            catch (OperationCanceledException)
            {
                Announce(L("download_cancelled"));
                SetProgress(0, L("cancelled"));
            }
            catch (Exception ex)
            {
                ShowError(string.Format(L("err_during_download"), ex.Message));
            }
            finally
            {
                SetDownloadState(false);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            if (_downloadInProgress)
                _cts?.Cancel();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  YT-DLP LOGIKA
        // ═════════════════════════════════════════════════════════════════════

        private string FindYtdlp()
        {
            foreach (var path in YtdlpSearchPaths)
                if (File.Exists(path)) return path;

            try
            {
                var p = new Process
                {
                    StartInfo = new ProcessStartInfo("yt-dlp", "--version")
                    {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                p.Start();
                p.WaitForExit(3000);
                if (p.ExitCode == 0) return "yt-dlp";
            }
            catch { }

            return null;
        }

        private string FindFfmpeg()
        {
            foreach (var path in FfmpegSearchPaths)
                if (File.Exists(path)) return path;

            try
            {
                var p = new Process
                {
                    StartInfo = new ProcessStartInfo("ffmpeg", "-version")
                    {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                p.Start();
                p.WaitForExit(3000);
                if (p.ExitCode == 0) return "ffmpeg";
            }
            catch { }

            return null;
        }

        private async Task<bool> OfferYtdlpInstallAsync()
        {
            var result = WpfMessageBox.Show(
                L("ytdlp_missing_msg"),
                L("ytdlp_missing_title"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return false;

            SetProgress(0, L("ytdlp_downloading"));
            pnlProgress.Visibility = Visibility.Visible;

            try
            {
                string dest = Path.Combine(AppDataFolder, "yt-dlp.exe");
                Directory.CreateDirectory(AppDataFolder);

                using var http = new HttpClient();
                http.DefaultRequestHeaders.Add("User-Agent", "UltraYoutubeDownloader/1.0");

                string url = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
                Announce(L("ytdlp_downloading"));

                var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                long total = response.Content.Headers.ContentLength ?? -1;
                using var stream = await response.Content.ReadAsStreamAsync();
                using var file = File.Create(dest);

                var buffer = new byte[65536];
                long downloaded = 0;
                int read;

                while ((read = await stream.ReadAsync(buffer)) > 0)
                {
                    await file.WriteAsync(buffer.AsMemory(0, read));
                    downloaded += read;
                    if (total > 0)
                    {
                        double pct = downloaded * 100.0 / total;
                        SetProgress(pct, $"yt-dlp: {pct:F0}%  ({downloaded / 1024 / 1024:F1} MB)");
                    }
                }

                Announce(L("ytdlp_downloaded"));
                SetProgress(100, L("ytdlp_ready"));
                return true;
            }
            catch (Exception ex)
            {
                ShowError(string.Format(L("ytdlp_install_fail"), ex.Message));
                return false;
            }
        }

        /// <summary>
        /// FFmpeg nema jedan stabilan "latest.exe" link kao yt-dlp — distribuira se kao zip.
        /// gyan.dev "essentials" build ima stabilan URL koji uvek pokazuje na najnoviju verziju,
        /// pa preuzimamo taj zip i izvlačimo samo ffmpeg.exe iz njega (ne treba nam ceo build).
        /// Ovo je jedina stvar koju ova aplikacija radi, pa nema smisla tražiti od (često slepog)
        /// korisnika da ručno preuzme i raspakuje arhivu — isti princip kao auto-install za yt-dlp.
        /// </summary>
        private async Task<bool> OfferFfmpegInstallAsync()
        {
            var result = WpfMessageBox.Show(
                L("ffmpeg_missing_msg"),
                L("ffmpeg_missing_title"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return false;

            SetProgress(0, L("ffmpeg_downloading"));
            pnlProgress.Visibility = Visibility.Visible;

            string zipPath = Path.Combine(Path.GetTempPath(), $"ffmpeg_{Guid.NewGuid():N}.zip");

            try
            {
                Directory.CreateDirectory(AppDataFolder);

                using var http = new HttpClient();
                http.DefaultRequestHeaders.Add("User-Agent", "UltraYoutubeDownloader/1.0");
                http.Timeout = TimeSpan.FromMinutes(10);

                string url = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";
                Announce(L("ffmpeg_downloading"));

                var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                long total = response.Content.Headers.ContentLength ?? -1;
                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var file = File.Create(zipPath))
                {
                    var buffer = new byte[65536];
                    long downloaded = 0;
                    int read;

                    while ((read = await stream.ReadAsync(buffer)) > 0)
                    {
                        await file.WriteAsync(buffer.AsMemory(0, read));
                        downloaded += read;
                        if (total > 0)
                        {
                            double pct = downloaded * 100.0 / total * 0.9; // poslednjih 10% za raspakivanje
                            SetProgress(pct, $"FFmpeg: {pct:F0}%  ({downloaded / 1024 / 1024:F1} MB)");
                        }
                    }
                }

                SetProgress(92, L("ffmpeg_extracting"));
                Announce(L("ffmpeg_extracting"));

                string destDir = Path.Combine(AppDataFolder, "Ffmpeg");
                Directory.CreateDirectory(destDir);
                string destExe = Path.Combine(destDir, "ffmpeg.exe");

                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    // U zip-u je ugnježdeno npr. "ffmpeg-7.1-essentials_build/bin/ffmpeg.exe" —
                    // ime foldera se menja sa svakim izdanjem, pa tražimo po sufiksu putanje.
                    var entry = archive.Entries.FirstOrDefault(en =>
                        en.FullName.Replace('\\', '/').EndsWith("/bin/ffmpeg.exe", StringComparison.OrdinalIgnoreCase));

                    if (entry == null)
                        throw new InvalidOperationException("ffmpeg.exe was not found inside the downloaded archive.");

                    entry.ExtractToFile(destExe, overwrite: true);
                }

                Announce(L("ffmpeg_installed"));
                SetProgress(100, L("ffmpeg_ready"));
                return true;
            }
            catch (Exception ex)
            {
                ShowError(string.Format(L("ffmpeg_install_fail"), ex.Message, Path.Combine(AppDataFolder, "Ffmpeg")));
                return false;
            }
            finally
            {
                try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { }
            }
        }

        // ─── Info ─────────────────────────────────────────────────────────────

        private class VideoInfo
        {
            public string Title   { get; set; } = "";
            public string Channel { get; set; } = "";
            public double DurationSec { get; set; }
            public bool   IsPlaylist  { get; set; }
            public int    Count       { get; set; }
            public List<string> EntryTitles { get; set; } = new();
        }

        /// <summary>Da li URL sadrži konkretan video id (?v=... ili youtu.be/...), a ne samo plejlistu.</summary>
        private static bool UrlHasSpecificVideoId(string url)
        {
            return Regex.IsMatch(url, @"[?&]v=[\w-]{6,}") || Regex.IsMatch(url, @"youtu\.be/[\w-]{6,}");
        }

        private async Task<VideoInfo> FetchInfoAsync(string ytdlp, string url)
        {
            string args = $"--dump-single-json --flat-playlist --no-warnings \"{url}\"";

            string output = await RunProcessAsync(ytdlp, args, null, CancellationToken.None);
            if (string.IsNullOrWhiteSpace(output))
            {
                ShowError(L("err_no_response"));
                return null;
            }

            var info = new VideoInfo();
            info.Title   = ExtractJson(output, "title");
            info.Channel = ExtractJson(output, "uploader") is { Length: > 0 } u ? u : ExtractJson(output, "channel");

            string dtype = ExtractJson(output, "_type");
            if (dtype == "playlist")
            {
                info.IsPlaylist = true;
                var matches = Regex.Matches(output, "\"url\"\\s*:");
                info.Count = matches.Count;
                if (info.Count == 0)
                {
                    string countStr = ExtractJson(output, "playlist_count");
                    int.TryParse(countStr, out int cnt);
                    info.Count = cnt;
                }

                var titleMatches = Regex.Matches(output, "\"title\"\\s*:\\s*\"([^\"]*)\"");
                if (titleMatches.Count > 1)
                {
                    for (int i = 1; i < titleMatches.Count; i++)
                        info.EntryTitles.Add(titleMatches[i].Groups[1].Value);
                }
            }
            else
            {
                string durStr = ExtractJson(output, "duration");
                double.TryParse(durStr, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double dur);
                info.DurationSec = dur;
            }

            return info;
        }

        // ─── Download ─────────────────────────────────────────────────────────

        private async Task<List<string>> RunDownloadAsync(
            string ytdlp, string ffmpeg, string url, bool audio, string quality, string audioQuality,
            bool isPlaylist, List<int> selectedTrackIndices, CancellationToken ct)
        {
            string outTemplate = Path.Combine(_currentSaveFolder, "%(title)s.%(ext)s");

            string formatArg;
            string postProcess;

            if (audio)
            {
                formatArg   = "--format bestaudio/best";
                postProcess = $"--extract-audio --audio-format mp3 --audio-quality {audioQuality}";
            }
            else
            {
                if (quality == "best")
                    formatArg = "--format \"bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best\"";
                else
                    formatArg = $"--format \"bestvideo[height<={quality}][ext=mp4]+bestaudio[ext=m4a]/best[height<={quality}][ext=mp4]/best[height<={quality}]\"";

                postProcess = "--merge-output-format mp4";
            }

            // Eksplicitno kažemo yt-dlp-u gde je FFmpeg — bez ovoga traži samo u PATH-u,
            // pa MP3 ekstrakcija/spajanje streamova tiho ne uspevaju kad ffmpeg nije globalno
            // instaliran (isti bag koji je popravljen u UltraVideoEditor-ovom dijalogu).
            string ffmpegDir = ffmpeg.Equals("ffmpeg", StringComparison.OrdinalIgnoreCase)
                ? null // "ffmpeg" iz PATH-a — yt-dlp će ga sam naći, ne treba mu eksplicitna putanja
                : Path.GetDirectoryName(ffmpeg);
            string ffmpegArg = ffmpegDir != null ? $"--ffmpeg-location \"{ffmpegDir}\"" : "";

            string playlistArg;
            if (!isPlaylist)
                playlistArg = "--no-playlist";
            else if (selectedTrackIndices != null && selectedTrackIndices.Count > 0)
                playlistArg = $"--playlist-items {string.Join(",", selectedTrackIndices)}";
            else
                playlistArg = "";

            string args = $"{formatArg} {postProcess} {ffmpegArg} {playlistArg} --no-warnings --newline " +
                          $"--output \"{outTemplate}\" \"{url}\"";

            var downloadedPaths = new List<string>();

            await RunProcessStreamAsync(ytdlp, args, line =>
            {
                ParseProgressLine(line, downloadedPaths);
            }, ct);

            if (downloadedPaths.Count == 0)
            {
                var cutoff = DateTime.Now.AddMinutes(-5);
                foreach (var f in Directory.GetFiles(_currentSaveFolder))
                {
                    if (File.GetCreationTime(f) >= cutoff)
                        downloadedPaths.Add(f);
                }
            }

            return downloadedPaths;
        }

        private void ParseProgressLine(string line, List<string> paths)
        {
            if (string.IsNullOrWhiteSpace(line)) return;

            var pctMatch = Regex.Match(line, @"\[download\]\s+([\d.]+)%");
            if (pctMatch.Success)
            {
                double pct = double.Parse(pctMatch.Groups[1].Value,
                    System.Globalization.CultureInfo.InvariantCulture);

                string speed = "";
                var speedMatch = Regex.Match(line, @"at\s+([\d.]+\s*\w+/s)");
                if (speedMatch.Success) speed = speedMatch.Groups[1].Value;

                string eta = "";
                var etaMatch = Regex.Match(line, @"ETA\s+([\d:]+)");
                if (etaMatch.Success) eta = etaMatch.Groups[1].Value;

                string label = $"{pct:F1}%";
                if (!string.IsNullOrEmpty(speed)) label += $"  •  {speed}";
                if (!string.IsNullOrEmpty(eta))   label += $"  •  ETA {eta}";

                Dispatcher.Invoke(() => SetProgress(pct, label));
                return;
            }

            var destMatch = Regex.Match(line, @"\[download\] Destination:\s+(.+)");
            if (destMatch.Success)
            {
                string path = destMatch.Groups[1].Value.Trim();
                Dispatcher.Invoke(() =>
                {
                    string msg = string.Format(L("downloading_file"), Path.GetFileName(path));
                    txbProgress.Text = msg;
                    Announce(msg);
                });
                return;
            }

            var mergeMatch = Regex.Match(line, @"\[(?:Merger|ExtractAudio)\] Destination:\s+(.+)");
            if (mergeMatch.Success)
            {
                string path = mergeMatch.Groups[1].Value.Trim();
                if (File.Exists(path) && !paths.Contains(path))
                    paths.Add(path);
                Dispatcher.Invoke(() =>
                {
                    SetProgress(100, string.Format(L("download_complete_file"), Path.GetFileName(path)));
                    Announce(string.Format(L("download_complete_file"), Path.GetFileName(path)));
                });
            }

            if (line.Contains("ERROR", StringComparison.OrdinalIgnoreCase)
                && (line.Contains("ffmpeg", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("Postprocessing", StringComparison.OrdinalIgnoreCase)))
            {
                Dispatcher.Invoke(() => Announce(string.Format(L("ytdlp_error"), line.Trim())));
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  PROCESS HELPERS
        // ═════════════════════════════════════════════════════════════════════

        private async Task<string> RunProcessAsync(string exe, string args,
            Action<string> lineCallback, CancellationToken ct)
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
            };

            using var p = new Process { StartInfo = psi };
            var sb = new System.Text.StringBuilder();

            p.OutputDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                sb.AppendLine(e.Data);
                lineCallback?.Invoke(e.Data);
            };
            // yt-dlp progress lines can land on stderr depending on version/flags, and if
            // RedirectStandardError is true but nobody reads it, the OS pipe buffer can fill
            // and stall the child process.
            p.ErrorDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                lineCallback?.Invoke(e.Data);
            };

            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            await Task.Run(() => p.WaitForExit(), ct);
            if (ct.IsCancellationRequested) { try { p.Kill(); } catch { } }

            return sb.ToString();
        }

        private async Task RunProcessStreamAsync(string exe, string args,
            Action<string> lineCallback, CancellationToken ct)
        {
            await RunProcessAsync(exe, args, lineCallback, ct);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  UI HELPERS
        // ═════════════════════════════════════════════════════════════════════

        private void SetFetchingState(bool fetching)
        {
            _fetchingInfo = fetching;
            btnFetchInfo.IsEnabled = !fetching;
            btnFetchInfo.Content   = fetching ? L("btn_loadinfo_loading") : L("btn_loadinfo");
            txtUrl.IsEnabled       = !fetching;
        }

        private void SetDownloadState(bool downloading)
        {
            _downloadInProgress    = downloading;
            btnDownload.IsEnabled  = !downloading;
            btnFetchInfo.IsEnabled = !downloading;
            txtUrl.IsEnabled       = !downloading;
            btnCancel.IsEnabled    = downloading;
            pnlProgress.Visibility = Visibility.Visible;

            if (downloading)
            {
                prgDownload.Value = 0;
                txbProgress.Text  = L("preparing");
            }
        }

        private void SetProgress(double percent, string label)
        {
            prgDownload.Value = percent;
            txbProgress.Text  = label;
        }

        private void Announce(string msg)
        {
            // AutomationProperties.LiveSetting="Polite" na txbProgress
            // već šalje JAWS/NVDA obaveštenje kad se tekst promeni.
            Dispatcher.InvokeAsync(() => { txbProgress.Text = msg; });
        }

        private void ShowError(string msg)
        {
            Announce(msg);
            WpfMessageBox.Show(msg, L("error_title"), MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private static string ExtractJson(string json, string key)
        {
            var m = Regex.Match(json, $"\"{Regex.Escape(key)}\"\\s*:\\s*\"([^\"]+)\"");
            if (m.Success) return m.Groups[1].Value;

            m = Regex.Match(json, $"\"{Regex.Escape(key)}\"\\s*:\\s*([\\d.]+)");
            return m.Success ? m.Groups[1].Value : "";
        }

        private static string FormatDuration(double secs)
        {
            if (secs <= 0) return "";
            var ts = TimeSpan.FromSeconds(secs);
            return ts.Hours > 0
                ? $"{ts.Hours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
                : $"{ts.Minutes}:{ts.Seconds:D2}";
        }
    }
}
