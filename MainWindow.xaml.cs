using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using WpfMessageBox = System.Windows.MessageBox;
using WinForms = System.Windows.Forms;

namespace UltraYoutubeDownloader
{
    /// <summary>
    /// Glavni prozor: polje za linkove, podrazumevani format i folder (pamte se
    /// odmah i zauvek, dok ih korisnik ne promeni) i lista za preuzimanje.
    /// Nalepiš jedan ili deset linkova, pritisneš Enter — i to je sve.
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly AppSettings _s = AppSettings.Load();
        private readonly ObservableCollection<DownloadItem> _items = new();
        private string _currentLanguage = "en";
        private string _currentTheme = "dark";
        private bool _loadingDefaults;

        private readonly SemaphoreSlim _toolsLock = new(1, 1);
        private string _ytdlp, _ffmpeg, _deno, _ytdlpVersion;
        private bool _infoLoopRunning, _askingPlaylist;
        private int _runDone, _runFailed;  // brojači za trenutni "krug" preuzimanja

        public MainWindow()
        {
            InitializeComponent();
            UiScaling.Register(this);

            Directory.CreateDirectory(AppSettings.AppDataFolder);
            lstQueue.ItemsSource = _items;

            _currentLanguage = _s.Language is "sr" or "en" ? _s.Language : "en";
            _currentTheme = _s.Theme is "dark" or "light" or "contrast" ? _s.Theme : "dark";
            ApplyTheme(save: false);
            if (string.IsNullOrEmpty(_s.Theme)) DetectAndApplyInitialMode();
            SetupKeyboardShortcuts();
            ApplyLanguage();

            Loaded += (_, _) =>
            {
                txtUrl.Focus();
                _ = BackgroundUpdateYtdlpAsync();
            };
        }

        private string L(string key) => LanguageManager.GetText(key, _currentLanguage);

        // ═════════════════════════════════════════════════════════════════════
        //  JEZIK, TEMA, VELIČINA
        // ═════════════════════════════════════════════════════════════════════
        private void ApplyLanguage()
        {
            Title = L("window_title");
            AutomationProperties.SetName(this, L("window_aria"));
            txbAppTitle.Text = L("window_title");
            txbAppSubtitle.Text = L("app_subtitle");

            mnuFileRoot.Header = L("menu_file");
            mnuPasteLinks.Header = L("menu_paste_links");
            mnuImportLinks.Header = L("menu_import_links");
            mnuExit.Header = L("menu_exit");
            mnuViewRoot.Header = L("menu_view");
            mnuThemeRoot.Header = L("menu_theme");
            mnuThemeDark.Header = L("theme_dark");
            mnuThemeLight.Header = L("theme_light");
            mnuThemeContrast.Header = L("theme_contrast");
            mnuTextSizeRoot.Header = L("menu_textsize");
            mnuLarger.Header = L("text_larger");
            mnuSmaller.Header = L("text_smaller");
            mnuReset.Header = L("text_reset");
            mnuLanguageRoot.Header = L("menu_language");
            mnuLangEn.Header = L("lang_en"); mnuLangEn.IsChecked = _currentLanguage == "en";
            mnuLangSr.Header = L("lang_sr"); mnuLangSr.IsChecked = _currentLanguage == "sr";
            mnuOpenFolderItem.Header = L("menu_open_folder");
            mnuToolsRoot.Header = L("menu_tools");
            mnuSettings.Header = L("menu_settings");
            mnuUpdateYtdlp.Header = L("menu_update_ytdlp");
            mnuInstallDeno.Header = L("menu_install_deno");
            mnuHelpRoot.Header = L("menu_help");
            mnuShortcuts.Header = L("menu_shortcuts");
            mnuSites.Header = L("menu_sites");
            mnuSupport.Header = L("menu_support");
            mnuAboutItem.Header = L("menu_about");

            lblUrl.Content = L("url_label");
            AutomationProperties.SetName(txtUrl, L("url_aria"));
            AutomationProperties.SetHelpText(txtUrl, L("url_help"));
            btnAdd.Content = L("btn_add");

            grpDefaults.Header = L("defaults_group");
            txbDefaultsHint.Text = L("defaults_hint");
            rbVideo.Content = L("video_option");
            rbAudio.Content = L("audio_option");
            lblQuality.Content = L("quality_label");
            lblContainer.Content = L("container_label");
            lblAudioFormat.Content = L("audio_format_label");
            lblAudioQuality.Content = L("audioq_label");
            lblSaveFolder.Content = L("savefolder_label");
            AutomationProperties.SetName(txtSaveFolder, L("savefolder_aria"));
            btnBrowseFolder.Content = L("browse_btn");
            btnOpenFolder.Content = L("open_btn");
            AutomationProperties.SetName(btnOpenFolder, L("open_btn_aria"));

            lblQueue.Content = L("queue_label");
            AutomationProperties.SetName(lstQueue, L("queue_aria"));
            AutomationProperties.SetHelpText(lstQueue, L("queue_help"));
            btnStartAll.Content = L("btn_start_all");
            btnStopAll.Content = L("btn_stop");
            btnItemOptions.Content = L("btn_item_options");
            btnRemove.Content = L("btn_remove");
            btnRetry.Content = L("btn_retry");
            btnClearDone.Content = L("btn_clear_done");
            AutomationProperties.SetName(prgOverall, L("progress_aria"));

            txbAppStatus.Text = $"{L("window_title")} {GetAppVersion()}  •  {L("status_by")}";

            LoadDefaultsIntoControls();
            foreach (var it in _items) it.Refresh();
        }

        private void LanguageMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem mi) return;
            _currentLanguage = mi.Tag?.ToString() ?? "en";
            _s.Language = _currentLanguage; _s.Save();
            ApplyLanguage();
            Announce(string.Format(L("language_announce"), (_currentLanguage == "sr" ? L("lang_sr") : L("lang_en")).Replace("_", "")));
        }

        private void ThemeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem mi) return;
            _currentTheme = mi.Tag?.ToString() ?? "dark";
            ApplyTheme(save: true);
            Announce(string.Format(L("theme_announce"), L("theme_" + _currentTheme + "_plain")));
        }

        private void ApplyTheme(bool save)
        {
            string file = _currentTheme switch { "contrast" => "Themes/Theme.Contrast.xaml", "light" => "Themes/Theme.Light.xaml", _ => "Themes/Theme.Dark.xaml" };
            try
            {
                var merged = System.Windows.Application.Current.Resources.MergedDictionaries;
                merged.Clear();
                merged.Add(new ResourceDictionary { Source = new Uri(file, UriKind.Relative) });
                mnuThemeDark.IsChecked = _currentTheme == "dark";
                mnuThemeLight.IsChecked = _currentTheme == "light";
                mnuThemeContrast.IsChecked = _currentTheme == "contrast";
                if (save) { _s.Theme = _currentTheme; _s.Save(); }
            }
            catch (Exception ex) { Announce(ex.Message); }
        }

        /// <summary>Prvo pokretanje sa čitačem ekrana: tema visokog kontrasta (dok korisnik sam ne izabere drugu).</summary>
        private void DetectAndApplyInitialMode()
        {
            bool sr = false;
            try { sr = ScreenReaderDetector.IsScreenReaderActive(); } catch { }
            if (sr) { _currentTheme = "contrast"; ApplyTheme(save: false); }
        }

        private void LargerText_Click(object sender, RoutedEventArgs e) { UiScaling.SetScale(UiScaling.CurrentScale + 0.1); Announce(string.Format(L("textsize_announce"), UiScaling.CurrentScale.ToString("P0"))); }
        private void SmallerText_Click(object sender, RoutedEventArgs e) { UiScaling.SetScale(UiScaling.CurrentScale - 0.1); Announce(string.Format(L("textsize_announce"), UiScaling.CurrentScale.ToString("P0"))); }
        private void ResetText_Click(object sender, RoutedEventArgs e) { UiScaling.SetScale(1.0); Announce(string.Format(L("textsize_announce"), "100%")); }

        private void SetupKeyboardShortcuts()
        {
            PreviewKeyDown += (s, e) =>
            {
                var mods = Keyboard.Modifiers;
                if (e.Key == Key.F1 && mods == ModifierKeys.None) { MnuShortcuts_Click(s, e); e.Handled = true; return; }
                if (mods == ModifierKeys.Control)
                {
                    if (e.Key == Key.OemPlus || e.Key == Key.Add) { LargerText_Click(s, e); e.Handled = true; }
                    else if (e.Key == Key.OemMinus || e.Key == Key.Subtract) { SmallerText_Click(s, e); e.Handled = true; }
                    else if (e.Key == Key.D0 || e.Key == Key.NumPad0) { ResetText_Click(s, e); e.Handled = true; }
                    else if (e.Key == Key.OemComma) { MnuSettings_Click(s, e); e.Handled = true; }
                }
                else if (mods == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.V)
                {
                    MnuPasteLinks_Click(s, e); e.Handled = true;
                }
            };
        }

        private static string GetAppVersion()
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                if (!string.IsNullOrWhiteSpace(info)) { int plus = info.IndexOf('+'); return "v" + (plus > 0 ? info[..plus] : info); }
                var v = asm.GetName().Version;
                return v == null ? "" : $"v{v.Major}.{v.Minor}.{v.Build}";
            }
            catch { return ""; }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  PODRAZUMEVANI FORMAT I FOLDER — pamti se ODMAH pri svakoj promeni
        // ═════════════════════════════════════════════════════════════════════
        private static readonly string[] VideoQualities = { "best", "2160", "1440", "1080", "720", "480", "360" };
        private static readonly string[] Containers = { "mp4", "mkv", "webm" };
        private static readonly string[] AudioFormats = { "mp3", "m4a", "opus", "flac", "wav" };
        private static readonly string[] AudioQualities = { "0", "320K", "192K", "128K" };

        private void Fill(ComboBox c, IEnumerable<(string label, string tag)> items, string selectedTag)
        {
            c.Items.Clear();
            foreach (var (label, tag) in items) c.Items.Add(new ComboBoxItem { Content = label, Tag = tag });
            c.SelectedIndex = Math.Max(0, c.Items.Cast<ComboBoxItem>().ToList().FindIndex(i => (string)i.Tag == selectedTag));
        }

        private IEnumerable<(string, string)> QualityItems() => VideoQualities.Select(q => (q == "best" ? L("q_best") : q == "2160" ? "2160p (4K)" : q == "1440" ? "1440p (2K)" : q + "p", q));
        private IEnumerable<(string, string)> ContainerItems() => Containers.Select(c => (L("container_" + c), c));
        private IEnumerable<(string, string)> AudioFormatItems() => AudioFormats.Select(f => (L("afmt_" + f), f));
        private IEnumerable<(string, string)> AudioQualityItems() => AudioQualities.Select(q => (L("aq_" + q.ToLowerInvariant()), q));

        private void LoadDefaultsIntoControls()
        {
            _loadingDefaults = true;
            try
            {
                rbVideo.IsChecked = !_s.AudioOnly;
                rbAudio.IsChecked = _s.AudioOnly;
                Fill(cmbQuality, QualityItems(), _s.VideoQuality);
                AutomationProperties.SetName(cmbQuality, L("quality_aria"));
                Fill(cmbContainer, ContainerItems(), _s.VideoContainer);
                AutomationProperties.SetName(cmbContainer, L("container_aria"));
                Fill(cmbAudioFormat, AudioFormatItems(), _s.AudioFormat);
                AutomationProperties.SetName(cmbAudioFormat, L("audio_format_aria"));
                Fill(cmbAudioQuality, AudioQualityItems(), _s.AudioQuality);
                AutomationProperties.SetName(cmbAudioQuality, L("audioq_aria"));
                txtSaveFolder.Text = _s.SaveFolder;
                UpdateFormatPanels();
            }
            finally { _loadingDefaults = false; }
        }

        private void UpdateFormatPanels()
        {
            bool audio = rbAudio.IsChecked == true;
            pnlVideo.Visibility = audio ? Visibility.Collapsed : Visibility.Visible;
            pnlAudio.Visibility = audio ? Visibility.Visible : Visibility.Collapsed;
            string af = (cmbAudioFormat.SelectedItem as ComboBoxItem)?.Tag as string ?? "mp3";
            cmbAudioQuality.IsEnabled = af is not ("flac" or "wav"); // bez gubitka — kvalitet nema smisla
        }

        private void FormatType_Changed(object sender, RoutedEventArgs e)
        {
            if (_loadingDefaults || pnlVideo == null) return;
            UpdateFormatPanels();
            SaveDefaults();
        }

        private void Defaults_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_loadingDefaults || pnlVideo == null) return;
            UpdateFormatPanels();
            SaveDefaults();
        }

        private void SaveDefaults()
        {
            _s.AudioOnly = rbAudio.IsChecked == true;
            _s.VideoQuality = (cmbQuality.SelectedItem as ComboBoxItem)?.Tag as string ?? "best";
            _s.VideoContainer = (cmbContainer.SelectedItem as ComboBoxItem)?.Tag as string ?? "mp4";
            _s.AudioFormat = (cmbAudioFormat.SelectedItem as ComboBoxItem)?.Tag as string ?? "mp3";
            _s.AudioQuality = (cmbAudioQuality.SelectedItem as ComboBoxItem)?.Tag as string ?? "192K";
            _s.Save();
        }

        private void BtnBrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            string chosen = PickFolder(_s.SaveFolder);
            if (chosen == null) return;
            _s.SaveFolder = chosen;
            _s.Save();
            txtSaveFolder.Text = chosen;
            Announce(string.Format(L("folder_set"), chosen));
        }

        public string PickFolder(string current)
        {
            using var dlg = new WinForms.FolderBrowserDialog
            {
                Description = L("browse_title"), UseDescriptionForTitle = true,
                SelectedPath = Directory.Exists(current) ? current : AppSettings.DefaultDownloadFolder
            };
            return dlg.ShowDialog() == WinForms.DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.SelectedPath) ? dlg.SelectedPath : null;
        }

        private void MnuOpenFolder_Click(object sender, RoutedEventArgs e) => OpenFolder(_s.SaveFolder, null);

        private void OpenFolder(string folder, string selectFile)
        {
            try
            {
                if (selectFile != null && File.Exists(selectFile))
                    Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{selectFile}\"") { UseShellExecute = true });
                else
                {
                    Directory.CreateDirectory(folder);
                    Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
                }
            }
            catch (Exception ex) { ShowError(string.Format(L("err_open_folder"), ex.Message)); }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  DODAVANJE LINKOVA
        // ═════════════════════════════════════════════════════════════════════
        private void TxtUrl_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Enter = dodaj u listu; Shift+Enter = novi red (ako se linkovi kucaju ručno)
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
            {
                e.Handled = true;
                AddFromTextBox();
            }
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e) => AddFromTextBox();

        private void AddFromTextBox()
        {
            int added = AddLinks(txtUrl.Text);
            if (added > 0) txtUrl.Clear();
            txtUrl.Focus();
        }

        private void MnuPasteLinks_Click(object sender, RoutedEventArgs e)
        {
            string text = "";
            try { if (Clipboard.ContainsText()) text = Clipboard.GetText(); } catch { }
            AddLinks(text);
        }

        private void MnuImportLinks_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Title = L("menu_import_links").Replace("_", ""), Filter = L("import_filter") };
            if (dlg.ShowDialog(this) != true) return;
            try { AddLinks(File.ReadAllText(dlg.FileName)); }
            catch (Exception ex) { ShowError(string.Format(L("err_prefix"), ex.Message)); }
        }

        /// <summary>Dodaje sve linkove iz teksta. Svaki pamti format i folder koji važe SADA.</summary>
        private int AddLinks(string text)
        {
            var urls = DownloadEngine.ExtractUrls(text);
            if (urls.Count == 0) { Announce(L("err_no_links")); return 0; }
            foreach (var u in urls)
            {
                var item = new DownloadItem
                {
                    Url = u, Format = _s.CurrentFormat(), Site = DownloadEngine.SiteFromUrl(u),
                    UrlHasSpecificVideo = DownloadEngine.UrlHasSpecificVideoId(u), Describe = DescribeItem,
                    State = ItemState.Checking
                };
                _items.Add(item);
            }
            Announce(urls.Count == 1
                ? string.Format(L("added_one"), urls[0], _s.CurrentFormat().Short(L))
                : string.Format(L("added_many"), urls.Count, _s.CurrentFormat().Short(L)));
            _ = InfoLoopAsync();
            return urls.Count;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  ALATI (yt-dlp, FFmpeg, Deno) — pita jednom, instalira sam
        // ═════════════════════════════════════════════════════════════════════
        private async Task<bool> EnsureToolsAsync(bool needYoutube)
        {
            await _toolsLock.WaitAsync();
            try
            {
                _ytdlp ??= DownloadEngine.FindYtdlp();
                if (_ytdlp == null)
                {
                    if (WpfMessageBox.Show(this, L("ytdlp_missing_msg"), L("ytdlp_missing_title"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                        return false;
                    Announce(L("ytdlp_downloading"));
                    try { await DownloadEngine.InstallYtdlpAsync(ToolProgress); }
                    catch (Exception ex) { ShowError(string.Format(L("ytdlp_install_fail"), ex.Message)); return false; }
                    _ytdlp = DownloadEngine.FindYtdlp();
                    _s.LastYtdlpUpdate = DateTime.Now; _s.Save();
                    Announce(L("ytdlp_ready"));
                    if (_ytdlp == null) return false;
                }
                _ytdlpVersion ??= await Task.Run(() => DownloadEngine.YtdlpVersion(_ytdlp));

                _ffmpeg ??= DownloadEngine.FindFfmpeg();
                if (_ffmpeg == null)
                {
                    if (WpfMessageBox.Show(this, L("ffmpeg_missing_msg"), L("ffmpeg_missing_title"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                        return false;
                    Announce(L("ffmpeg_downloading"));
                    try { await DownloadEngine.InstallFfmpegAsync(ToolProgress, L("ffmpeg_extracting")); }
                    catch (Exception ex) { ShowError(string.Format(L("ffmpeg_install_fail"), ex.Message, Path.Combine(AppSettings.AppDataFolder, "Ffmpeg"))); return false; }
                    _ffmpeg = DownloadEngine.FindFfmpeg();
                    Announce(L("ffmpeg_ready"));
                    if (_ffmpeg == null) return false;
                }

                _deno ??= DownloadEngine.FindDeno();
                if (_deno == null && needYoutube && !_s.DenoDeclined)
                {
                    var r = WpfMessageBox.Show(this, L("deno_missing_msg"), L("deno_missing_title"), MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (r == MessageBoxResult.Yes) await InstallDenoAsync();
                    else { _s.DenoDeclined = true; _s.Save(); Announce(L("deno_declined")); }
                }
                return true;
            }
            finally { _toolsLock.Release(); }
        }

        private async Task InstallDenoAsync()
        {
            Announce(L("deno_downloading"));
            try
            {
                await DownloadEngine.InstallDenoAsync(ToolProgress);
                _deno = DownloadEngine.FindDeno();
                _s.DenoDeclined = false; _s.Save();
                Announce(L("deno_ready"));
            }
            catch (Exception ex) { ShowError(string.Format(L("deno_install_fail"), ex.Message)); }
        }

        private void ToolProgress(double pct, string label) =>
            Dispatcher.InvokeAsync(() => { prgOverall.Value = pct; txbProgress.Text = label; });

        private async Task BackgroundUpdateYtdlpAsync()
        {
            // Jednom nedeljno, tiho: sajtovi se stalno menjaju, a zastareli yt-dlp je najčešći uzrok greške
            if (!_s.AutoUpdateYtdlp || (DateTime.Now - _s.LastYtdlpUpdate).TotalDays < 7) return;
            string own = Path.Combine(AppSettings.AppDataFolder, "yt-dlp.exe");
            if (!File.Exists(own)) return;
            try
            {
                await DownloadEngine.UpdateYtdlpAsync(own);
                _s.LastYtdlpUpdate = DateTime.Now; _s.Save();
                _ytdlpVersion = null;
            }
            catch { /* bez interneta i sl. — pokušaće sledeći put */ }
        }

        private async void MnuUpdateYtdlp_Click(object sender, RoutedEventArgs e)
        {
            if (_items.Any(i => i.IsActive)) { Announce(L("update_wait")); return; }
            Announce(L("updating_ytdlp"));
            try
            {
                string y = DownloadEngine.FindYtdlp();
                string v = y == null
                    ? await Task.Run(async () => { await DownloadEngine.InstallYtdlpAsync(null); return DownloadEngine.YtdlpVersion(Path.Combine(AppSettings.AppDataFolder, "yt-dlp.exe")); })
                    : await DownloadEngine.UpdateYtdlpAsync(y);
                _ytdlp = null; _ytdlpVersion = null;
                _s.LastYtdlpUpdate = DateTime.Now; _s.Save();
                Announce(string.Format(L("ytdlp_updated"), v));
            }
            catch (Exception ex) { ShowError(string.Format(L("err_prefix"), ex.Message)); }
        }

        private async void MnuInstallDeno_Click(object sender, RoutedEventArgs e)
        {
            string d = DownloadEngine.FindDeno();
            if (d != null) { _deno = d; Announce(L("deno_already")); return; }
            await InstallDenoAsync();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  PROVERA LINKOVA (naslov, sajt, plejlista) — jedan po jedan, u pozadini
        // ═════════════════════════════════════════════════════════════════════
        private async Task InfoLoopAsync()
        {
            if (_infoLoopRunning) return;
            _infoLoopRunning = true;
            try
            {
                while (true)
                {
                    var item = _items.FirstOrDefault(i => i.State == ItemState.Checking);
                    if (item == null) break;
                    bool yt = item.Site == "YouTube";
                    if (!await EnsureToolsAsync(yt))
                    {
                        foreach (var it in _items.Where(i => i.State == ItemState.Checking).ToList())
                        { it.State = ItemState.Failed; it.Detail = L("err_tools_missing"); }
                        break;
                    }
                    try
                    {
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
                        await DownloadEngine.FetchInfoAsync(_ytdlp, item, _s, cts.Token);
                    }
                    catch (Exception ex)
                    {
                        // Neki sajtovi ne daju podatke unapred, a preuzimanje ipak radi — ne odustajemo ovde
                        item.Detail = string.Format(L("info_unavailable"), ex.Message);
                    }
                    if (!_items.Contains(item)) continue; // uklonjen u međuvremenu

                    if (item.IsPlaylist)
                    {
                        string policy = _s.PlaylistPolicy;
                        if (policy == "single" && item.UrlHasSpecificVideo) item.PlaylistScope = "single";
                        else if (policy == "all" || (policy == "single" && !item.UrlHasSpecificVideo)) item.PlaylistScope = "all";
                        else { item.State = ItemState.NeedsChoice; _ = AskPlaylistChoicesAsync(); continue; }
                    }
                    MarkReady(item);
                }
            }
            finally { _infoLoopRunning = false; }
        }

        private void MarkReady(DownloadItem item)
        {
            item.State = _s.AutoStart ? ItemState.Waiting : ItemState.Ready;
            if (item.State == ItemState.Waiting) Pump();
            else Announce(string.Format(L("ready_announce"), ItemName(item)));
        }

        /// <summary>Plejliste kod kojih je podešeno "pitaj me" — dijalog za svaku, redom.</summary>
        private async Task AskPlaylistChoicesAsync()
        {
            if (_askingPlaylist) return;
            _askingPlaylist = true;
            try
            {
                await System.Windows.Threading.Dispatcher.Yield();
                DownloadItem item;
                while ((item = _items.FirstOrDefault(i => i.State == ItemState.NeedsChoice)) != null)
                {
                    var dlg = new PlaylistChoiceWindow(this, item, L);
                    if (dlg.ShowDialog() == true)
                    {
                        item.PlaylistScope = dlg.Scope;
                        MarkReady(item);
                    }
                    else
                    {
                        item.State = ItemState.Cancelled;
                        item.Detail = L("playlist_skipped");
                    }
                }
            }
            finally { _askingPlaylist = false; }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  PREUZIMANJE
        // ═════════════════════════════════════════════════════════════════════
        private void Pump()
        {
            int active = _items.Count(i => i.IsActive);
            foreach (var item in _items.Where(i => i.State == ItemState.Waiting).ToList())
            {
                if (active >= _s.SimultaneousDownloads) break;
                active++;
                _ = DownloadItemAsync(item);
            }
            UpdateOverall();
        }

        private async Task DownloadItemAsync(DownloadItem item)
        {
            item.State = ItemState.Downloading;
            item.Percent = 0;
            item.Detail = "";
            item.Files.Clear();
            item.Cts = new CancellationTokenSource();
            Announce(string.Format(L("started_announce"), ItemName(item)));

            try
            {
                if (!await EnsureToolsAsync(item.Site == "YouTube"))
                    throw new InvalidOperationException(L("err_tools_missing"));
                Directory.CreateDirectory(item.Format.SaveFolder);

                var args = DownloadEngine.BuildArgs(item, _s, _ffmpeg, _deno, DownloadEngine.SupportsJsRuntimes(_ytdlpVersion));
                DateTime lastUi = DateTime.MinValue;
                var files = await DownloadEngine.DownloadAsync(_ytdlp, args, p =>
                {
                    // Napredak se osvežava najviše 4 puta u sekundi — lista ostaje mirna za čitač ekrana
                    if ((DateTime.Now - lastUi).TotalMilliseconds < 250 && p.Percent < 100) return;
                    lastUi = DateTime.Now;
                    double pct = p.Percent; string speed = p.Speed, eta = p.Eta, title = p.CurrentTitle;
                    int idx = p.Index, total = p.Total;
                    Dispatcher.InvokeAsync(() =>
                    {
                        if (item.State != ItemState.Downloading) return;
                        if (!item.IsPlaylist && !string.IsNullOrEmpty(title) && string.IsNullOrEmpty(item.Title)) item.Title = title;
                        double overall = total > 1 && idx > 0 ? ((idx - 1) * 100 + pct) / total : pct;
                        item.Percent = overall;
                        var parts = new List<string>();
                        if (total > 1 && idx > 0) parts.Add(string.Format(L("pl_progress"), idx, total, title));
                        if (!string.IsNullOrEmpty(speed)) parts.Add(speed);
                        if (!string.IsNullOrEmpty(eta)) parts.Add(string.Format(L("eta_left"), eta));
                        item.Detail = string.Join(", ", parts);
                        UpdateOverall();
                    });
                }, item.Cts.Token);

                item.Files.AddRange(files);
                item.Percent = 100;
                item.State = ItemState.Done;
                item.Detail = files.Count > 1 ? string.Format(L("files_count"), files.Count) : files.Count == 1 ? Path.GetFileName(files[0]) : L("already_downloaded");
                _runDone++;
                Announce(string.Format(L("done_announce"), ItemName(item)));
            }
            catch (OperationCanceledException)
            {
                item.State = ItemState.Cancelled;
                item.Detail = "";
                Announce(string.Format(L("stopped_announce"), ItemName(item)));
            }
            catch (Exception ex)
            {
                item.State = ItemState.Failed;
                item.Detail = FriendlyError(ex.Message);
                _runFailed++;
                Announce(string.Format(L("failed_announce"), ItemName(item), item.Detail));
            }
            finally
            {
                item.Cts?.Dispose();
                item.Cts = null;
                Pump();
                CheckQueueFinished();
            }
        }

        private string FriendlyError(string msg)
        {
            string m = msg ?? "";
            if (m.Contains("Sign in to confirm", StringComparison.OrdinalIgnoreCase) || m.Contains("login required", StringComparison.OrdinalIgnoreCase)
                || m.Contains("cookies", StringComparison.OrdinalIgnoreCase) || m.Contains("private", StringComparison.OrdinalIgnoreCase))
                return m + " " + L("hint_cookies");
            if (m.Contains("Unsupported URL", StringComparison.OrdinalIgnoreCase)) return L("err_unsupported");
            if (m.Contains("HTTP Error 403", StringComparison.OrdinalIgnoreCase) || m.Contains("nsig", StringComparison.OrdinalIgnoreCase))
                return m + " " + L("hint_update");
            return m;
        }

        private void CheckQueueFinished()
        {
            if (_items.Any(i => i.State is ItemState.Downloading or ItemState.Waiting or ItemState.Checking or ItemState.NeedsChoice)) return;
            if (_runDone + _runFailed == 0) return;
            string summary = _runFailed == 0
                ? string.Format(L("all_done"), _runDone)
                : string.Format(L("all_done_with_errors"), _runDone, _runFailed);
            int done = _runDone, failed = _runFailed;
            _runDone = _runFailed = 0;
            prgOverall.Value = 100;
            Announce(summary);
            if (_s.OpenFolderWhenDone && done > 0) OpenFolder(_s.SaveFolder, null);
            if (_s.MessageWhenDone)
                WpfMessageBox.Show(this, summary, L("download_complete_title"), MessageBoxButton.OK,
                    failed == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }

        private void UpdateOverall()
        {
            var relevant = _items.Where(i => i.State is ItemState.Downloading or ItemState.Waiting or ItemState.Done).ToList();
            if (relevant.Count == 0) return;
            prgOverall.Value = relevant.Average(i => i.State == ItemState.Done ? 100 : i.Percent);
            int done = _items.Count(i => i.State == ItemState.Done), total = _items.Count(i => i.State != ItemState.Cancelled);
            var active = _items.FirstOrDefault(i => i.IsActive);
            txbProgress.Text = active != null
                ? string.Format(L("overall_status"), done, total, ItemName(active), active.Percent.ToString("0"))
                : string.Format(L("overall_idle"), done, total);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  LISTA: opis stavke i komande
        // ═════════════════════════════════════════════════════════════════════
        private string ItemName(DownloadItem i) =>
            !string.IsNullOrWhiteSpace(i.Title) ? i.Title : i.Url;

        private string DescribeItem(DownloadItem i)
        {
            int n = _items.IndexOf(i) + 1;
            string name = ItemName(i);
            if (i.IsPlaylist) name = string.Format(L("pl_name"), name, i.Count);
            string state = i.State switch
            {
                ItemState.Checking => L("st_checking"),
                ItemState.Ready => L("st_ready"),
                ItemState.NeedsChoice => L("st_needs_choice"),
                ItemState.Waiting => L("st_waiting"),
                ItemState.Downloading => string.Format(L("st_downloading"), i.Percent.ToString("0")),
                ItemState.Done => L("st_done"),
                ItemState.Failed => L("st_failed"),
                _ => L("st_cancelled")
            };
            string site = string.IsNullOrEmpty(i.Site) ? "" : $" — {i.Site}";
            string detail = string.IsNullOrEmpty(i.Detail) ? "" : $": {i.Detail}";
            return $"{n}. {name}{site} — {i.Format.Short(L)} — {state}{detail}";
        }

        private List<DownloadItem> Selected() => lstQueue.SelectedItems.Cast<DownloadItem>().ToList();

        private void LstQueue_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Delete) { BtnRemove_Click(sender, e); e.Handled = true; }
            else if (e.Key == Key.Enter) { ActivateSelected(); e.Handled = true; }
        }

        private void LstQueue_MouseDoubleClick(object sender, MouseButtonEventArgs e) => ActivateSelected();

        /// <summary>Enter na stavci: gotova -> otvori folder sa označenim fajlom; ostale -> opcije stavke.</summary>
        private void ActivateSelected()
        {
            var it = lstQueue.SelectedItem as DownloadItem;
            if (it == null) return;
            if (it.State == ItemState.Done)
            {
                string file = it.Files.FirstOrDefault();
                OpenFolder(file != null ? Path.GetDirectoryName(file) : it.Format.SaveFolder, file);
            }
            else BtnItemOptions_Click(this, new RoutedEventArgs());
        }

        private void BtnStartAll_Click(object sender, RoutedEventArgs e)
        {
            int n = 0;
            foreach (var it in _items.Where(i => i.State is ItemState.Ready or ItemState.Cancelled))
            {
                if (it.IsPlaylist && it.PlaylistScope == null) { it.State = ItemState.NeedsChoice; continue; }
                it.State = ItemState.Waiting; it.Detail = ""; n++;
            }
            _ = AskPlaylistChoicesAsync();
            if (n == 0 && !_items.Any(i => i.IsActive)) { Announce(L("nothing_to_start")); return; }
            Announce(string.Format(L("starting_n"), n));
            Pump();
        }

        private void BtnStopAll_Click(object sender, RoutedEventArgs e)
        {
            var sel = Selected().Where(i => i.IsActive || i.State == ItemState.Waiting).ToList();
            // Ako je izabrana stavka koja se preuzima — zaustavi samo nju; inače sve
            var targets = sel.Count > 0 ? sel : _items.Where(i => i.IsActive || i.State == ItemState.Waiting).ToList();
            if (targets.Count == 0) { Announce(L("nothing_running")); return; }
            foreach (var it in targets)
            {
                if (it.State == ItemState.Waiting) it.State = ItemState.Ready;
                else it.Cts?.Cancel();
            }
            Announce(sel.Count > 0 ? L("stopping_selected") : L("stopping_all"));
        }

        private void BtnRemove_Click(object sender, RoutedEventArgs e)
        {
            var sel = Selected();
            if (sel.Count == 0) { Announce(L("select_first")); return; }
            int idx = lstQueue.SelectedIndex;
            foreach (var it in sel) { it.Cts?.Cancel(); _items.Remove(it); }
            foreach (var it in _items) it.Refresh(); // brojevi stavki
            if (_items.Count > 0) { lstQueue.SelectedIndex = Math.Min(idx, _items.Count - 1); FocusSelectedItem(); }
            Announce(string.Format(L("removed_n"), sel.Count));
        }

        private void BtnRetry_Click(object sender, RoutedEventArgs e)
        {
            var failed = _items.Where(i => i.State is ItemState.Failed or ItemState.Cancelled).ToList();
            if (failed.Count == 0) { Announce(L("nothing_failed")); return; }
            foreach (var it in failed)
            {
                if (it.IsPlaylist && it.PlaylistScope == null) { it.State = ItemState.NeedsChoice; continue; }
                it.State = ItemState.Waiting; it.Detail = ""; it.Percent = 0;
            }
            _ = AskPlaylistChoicesAsync();
            Announce(string.Format(L("retrying_n"), failed.Count));
            Pump();
        }

        private void BtnClearDone_Click(object sender, RoutedEventArgs e)
        {
            var done = _items.Where(i => i.State == ItemState.Done).ToList();
            foreach (var it in done) _items.Remove(it);
            foreach (var it in _items) it.Refresh();
            Announce(string.Format(L("cleared_n"), done.Count));
        }

        private void BtnItemOptions_Click(object sender, RoutedEventArgs e)
        {
            var sel = Selected().Where(i => !i.IsActive && i.State != ItemState.Done).ToList();
            if (sel.Count == 0) { Announce(L("options_none")); return; }
            var dlg = new ItemOptionsWindow(this, sel, L, PickFolder);
            if (dlg.ShowDialog() != true) return;
            foreach (var it in sel) it.Refresh();
            Announce(string.Format(L("options_applied"), sel.Count, sel[0].Format.Short(L)));
            FocusSelectedItem();
        }

        private void FocusSelectedItem()
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (lstQueue.SelectedItem != null && lstQueue.ItemContainerGenerator.ContainerFromItem(lstQueue.SelectedItem) is ListBoxItem li) li.Focus();
                else lstQueue.Focus();
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  MENI: podešavanja, pomoć, izlaz
        // ═════════════════════════════════════════════════════════════════════
        private void MnuSettings_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SettingsWindow(this, _s, L);
            if (dlg.ShowDialog() == true)
            {
                _s.Save();
                Announce(L("settings_saved"));
                Pump();
            }
        }

        private void MnuShortcuts_Click(object sender, RoutedEventArgs e) =>
            new TextWindow(this, L("menu_shortcuts").Replace("_", ""), L("shortcuts_text"), L).ShowDialog();

        private void MnuSites_Click(object sender, RoutedEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo("https://github.com/yt-dlp/yt-dlp/blob/master/supportedsites.md") { UseShellExecute = true }); }
            catch { }
        }

        private const string SponsorsUrl = "https://github.com/sponsors/demirajvazi10-max";

        private void MnuAbout_Click(object sender, RoutedEventArgs e) =>
            new TextWindow(this, L("about_title"), string.Format(L("about_text"), GetAppVersion()), L,
                (L("btn_how_support"), () => MnuSupport_Click(this, new RoutedEventArgs()))).ShowDialog();

        /// <summary>Kako podržati projekat: korak po korak, sa naglaskom da se plaća običnom karticom (PayPal nije potreban).</summary>
        private void MnuSupport_Click(object sender, RoutedEventArgs e) =>
            new TextWindow(this, L("support_title"), string.Format(L("support_text"), SponsorsUrl), L,
                (L("btn_open_sponsors"), () =>
                {
                    try { Process.Start(new ProcessStartInfo(SponsorsUrl) { UseShellExecute = true }); }
                    catch (Exception ex) { ShowError(string.Format(L("err_prefix"), ex.Message)); }
                }),
                (L("btn_copy_link"), () =>
                {
                    try { Clipboard.SetText(SponsorsUrl); Announce(L("link_copied")); } catch { }
                })).ShowDialog();

        private void MnuExit_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_items.Any(i => i.IsActive)) return;
            if (WpfMessageBox.Show(this, L("confirm_exit"), L("window_title"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            { e.Cancel = true; return; }
            foreach (var it in _items) it.Cts?.Cancel();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  NAJAVE
        // ═════════════════════════════════════════════════════════════════════
        private void Announce(string msg)
        {
            Dispatcher.InvokeAsync(() =>
            {
                txbProgress.Text = msg;
                // LiveSetting sam ne najavljuje ništa — događaj mora da se podigne izričito
                var peer = UIElementAutomationPeer.FromElement(txbProgress) ?? UIElementAutomationPeer.CreatePeerForElement(txbProgress);
                peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
            });
        }

        private void ShowError(string msg)
        {
            Announce(msg);
            WpfMessageBox.Show(this, msg, L("error_title"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
