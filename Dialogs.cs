using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

namespace UltraYoutubeDownloader
{
    /// <summary>Zajednički izgled dijaloga: boje iz trenutne teme, Escape zatvara, svako polje ima ime za čitač ekrana.</summary>
    public class DialogBase : Window
    {
        protected readonly Func<string, string> L;
        protected readonly StackPanel Body = new() { Margin = new Thickness(18) };

        protected DialogBase(Window owner, string title, Func<string, string> l, double width = 520, double height = 560)
        {
            L = l;
            Owner = owner;
            Title = title;
            Width = width; Height = height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            SetResourceReference(BackgroundProperty, "MainWindowBg");
            SetResourceReference(ForegroundProperty, "TextPrimary");
            KeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; Close(); } };
        }

        protected void Finish(params Button[] buttons)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
            foreach (var b in buttons) { b.Margin = new Thickness(8, 0, 0, 0); row.Children.Add(b); }
            Body.Children.Add(row);
            Content = new ScrollViewer { Content = Body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            UiScaling.Register(this);
        }

        protected TextBlock Text(string text, bool bold = false, bool secondary = false)
        {
            var t = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 6), FontSize = 13 };
            if (bold) t.FontWeight = FontWeights.Bold;
            t.SetResourceReference(TextBlock.ForegroundProperty, secondary ? "TextSecondary" : "TextPrimary");
            Body.Children.Add(t);
            return t;
        }

        protected TextBlock Heading(string text)
        {
            var t = Text(text, bold: true);
            t.Margin = new Thickness(0, 12, 0, 4);
            AutomationProperties.SetHeadingLevel(t, AutomationHeadingLevel.Level2);
            return t;
        }

        protected ComboBox Combo(string label, IEnumerable<(string label, string tag)> items, string selected)
        {
            var lbl = new Label { Content = label, Padding = new Thickness(0, 4, 0, 3) };
            var c = new ComboBox { Margin = new Thickness(0, 0, 0, 6) };
            foreach (var (l, tag) in items) c.Items.Add(new ComboBoxItem { Content = l, Tag = tag });
            c.SelectedIndex = Math.Max(0, c.Items.Cast<ComboBoxItem>().ToList().FindIndex(i => (string)i.Tag == selected));
            lbl.Target = c;
            AutomationProperties.SetName(c, label.Replace("_", "").TrimEnd(':'));
            Body.Children.Add(lbl);
            Body.Children.Add(c);
            return c;
        }

        protected static string SelTag(ComboBox c) => (c.SelectedItem as ComboBoxItem)?.Tag as string ?? "";

        protected CheckBox Check(string label, bool value)
        {
            var c = new CheckBox { Content = label, IsChecked = value };
            Body.Children.Add(c);
            return c;
        }

        protected TextBox Field(string label, string value)
        {
            var lbl = new Label { Content = label, Padding = new Thickness(0, 4, 0, 3) };
            var t = new TextBox { Text = value, Margin = new Thickness(0, 0, 0, 6) };
            lbl.Target = t;
            AutomationProperties.SetName(t, label.Replace("_", "").TrimEnd(':'));
            Body.Children.Add(lbl);
            Body.Children.Add(t);
            return t;
        }

        protected Button Btn(string text, Action click, bool primary = false, bool isDefault = false, bool isCancel = false)
        {
            var b = new Button { Content = text, IsDefault = isDefault, IsCancel = isCancel, MinWidth = 90 };
            if (primary) b.Style = (Style)System.Windows.Application.Current.Resources["PrimaryBtn"];
            b.Click += (_, _) => click();
            return b;
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // PLEJLISTA — samo ovaj video, cela plejlista ili izabrane numere
    // ════════════════════════════════════════════════════════════════════
    public class PlaylistChoiceWindow : DialogBase
    {
        public string Scope { get; private set; } = "all";

        public PlaylistChoiceWindow(Window owner, DownloadItem item, Func<string, string> l)
            : base(owner, l("playlist_dialog_title"), l, 600, 600)
        {
            Text(string.Format(L("playlist_dialog_text"), string.IsNullOrEmpty(item.Title) ? item.Url : item.Title, item.Count), bold: true);

            var single = new RadioButton { Content = L("scope_single"), GroupName = "scope", Margin = new Thickness(0, 4, 0, 4) };
            var all = new RadioButton { Content = L("scope_all"), GroupName = "scope", Margin = new Thickness(0, 4, 0, 4) };
            var choose = new RadioButton { Content = L("scope_choose"), GroupName = "scope", Margin = new Thickness(0, 4, 0, 4) };
            if (item.UrlHasSpecificVideo) Body.Children.Add(single);
            Body.Children.Add(all);
            if (item.EntryTitles.Count > 0) Body.Children.Add(choose);

            var list = new ListBox { SelectionMode = SelectionMode.Multiple, Height = 220, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 6, 0, 0) };
            for (int i = 0; i < item.EntryTitles.Count; i++) list.Items.Add($"{i + 1}. {item.EntryTitles[i]}");
            AutomationProperties.SetName(list, L("tracks_aria"));
            Body.Children.Add(list);
            var hint = Text("", secondary: true);
            hint.SetValue(AutomationProperties.LiveSettingProperty, AutomationLiveSetting.Polite);

            choose.Checked += (_, _) => { list.Visibility = Visibility.Visible; hint.Text = L("choose_tracks_hint"); };
            single.Checked += (_, _) => list.Visibility = Visibility.Collapsed;
            all.Checked += (_, _) => list.Visibility = Visibility.Collapsed;
            (item.UrlHasSpecificVideo ? single : all).IsChecked = true;

            Finish(
                Btn(L("btn_ok"), () =>
                {
                    if (single.IsChecked == true) Scope = "single";
                    else if (choose.IsChecked == true)
                    {
                        var idx = list.SelectedItems.Cast<string>().Select(s => list.Items.IndexOf(s) + 1).OrderBy(i => i).ToList();
                        if (idx.Count == 0) { hint.Text = L("err_select_track"); list.Focus(); return; }
                        Scope = string.Join(",", idx);
                    }
                    else Scope = "all";
                    DialogResult = true;
                }, primary: true, isDefault: true),
                Btn(L("btn_skip"), () => DialogResult = false, isCancel: true));
            Loaded += (_, _) => ((item.UrlHasSpecificVideo ? single : all) as UIElement).Focus();
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // OPCIJE STAVKE — drugačiji format/folder samo za izabrane linkove
    // ════════════════════════════════════════════════════════════════════
    public class ItemOptionsWindow : DialogBase
    {
        public ItemOptionsWindow(Window owner, List<DownloadItem> items, Func<string, string> l, Func<string, string> pickFolder)
            : base(owner, l("item_options_title"), l, 540, 640)
        {
            var f = items[0].Format;
            Text(items.Count == 1
                ? string.Format(L("item_options_one"), string.IsNullOrEmpty(items[0].Title) ? items[0].Url : items[0].Title)
                : string.Format(L("item_options_many"), items.Count), bold: true);
            Text(L("item_options_hint"), secondary: true);

            var type = Combo(L("type_label"), new[] { (L("video_option"), "video"), (L("audio_option"), "audio") }, f.AudioOnly ? "audio" : "video");
            var q = Combo(L("quality_label"), new[] { "best", "2160", "1440", "1080", "720", "480", "360" }.Select(x => (x == "best" ? L("q_best") : x + "p", x)), f.VideoQuality);
            var cont = Combo(L("container_label"), new[] { "mp4", "mkv", "webm" }.Select(x => (L("container_" + x), x)), f.VideoContainer);
            var af = Combo(L("audio_format_label"), new[] { "mp3", "m4a", "opus", "flac", "wav" }.Select(x => (L("afmt_" + x), x)), f.AudioFormat);
            var aq = Combo(L("audioq_label"), new[] { "0", "320K", "192K", "128K" }.Select(x => (L("aq_" + x.ToLowerInvariant()), x)), f.AudioQuality);
            var folder = Field(L("savefolder_label"), f.SaveFolder);
            var browse = Btn(L("browse_btn"), () => { var p = pickFolder(folder.Text); if (p != null) folder.Text = p; });
            browse.HorizontalAlignment = HorizontalAlignment.Left;
            Body.Children.Add(browse);

            void Sync()
            {
                bool audio = SelTag(type) == "audio";
                q.IsEnabled = cont.IsEnabled = !audio;
                af.IsEnabled = audio;
                aq.IsEnabled = audio && SelTag(af) is not ("flac" or "wav");
            }
            type.SelectionChanged += (_, _) => Sync();
            af.SelectionChanged += (_, _) => Sync();
            Sync();

            ComboBox scope = null;
            var playlist = items.Count == 1 && items[0].IsPlaylist ? items[0] : null;
            if (playlist != null)
            {
                Heading(L("scope_group"));
                var opts = new List<(string, string)>();
                if (playlist.UrlHasSpecificVideo) opts.Add((L("scope_single"), "single"));
                opts.Add((L("scope_all"), "all"));
                if (playlist.PlaylistScope is not (null or "single" or "all")) opts.Add((string.Format(L("scope_custom"), playlist.PlaylistScope), playlist.PlaylistScope));
                scope = Combo(L("scope_group"), opts, playlist.PlaylistScope ?? "all");
            }

            Finish(
                Btn(L("btn_ok"), () =>
                {
                    foreach (var it in items)
                    {
                        it.Format = new FormatChoice
                        {
                            AudioOnly = SelTag(type) == "audio", VideoQuality = SelTag(q), VideoContainer = SelTag(cont),
                            AudioFormat = SelTag(af), AudioQuality = SelTag(aq),
                            SaveFolder = string.IsNullOrWhiteSpace(folder.Text) ? it.Format.SaveFolder : folder.Text.Trim()
                        };
                    }
                    if (playlist != null && scope != null) playlist.PlaylistScope = SelTag(scope);
                    DialogResult = true;
                }, primary: true, isDefault: true),
                Btn(L("btn_cancel"), () => DialogResult = false, isCancel: true));
            Loaded += (_, _) => type.Focus();
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // PODEŠAVANJA
    // ════════════════════════════════════════════════════════════════════
    public class SettingsWindow : DialogBase
    {
        public SettingsWindow(Window owner, AppSettings s, Func<string, string> l)
            : base(owner, l("settings_title"), l, 600, 720)
        {
            Text(L("settings_hint"), secondary: true);

            Heading(L("set_playlists"));
            var policy = Combo(L("set_playlist_policy"), new[] { (L("pol_ask"), "ask"), (L("pol_single"), "single"), (L("pol_all"), "all") }, s.PlaylistPolicy);

            Heading(L("set_queue"));
            var auto = Check(L("set_autostart"), s.AutoStart);
            var sim = Combo(L("set_simultaneous"), new[] { ("1", "1"), ("2", "2"), ("3", "3") }, s.SimultaneousDownloads.ToString());
            var msg = Check(L("set_message_done"), s.MessageWhenDone);
            var open = Check(L("set_open_done"), s.OpenFolderWhenDone);
            var skip = Check(L("set_skip_downloaded"), s.SkipAlreadyDownloaded);

            Heading(L("set_files"));
            var naming = Combo(L("set_naming"), new[] { (L("name_title"), "title"), (L("name_title_id"), "title_id"), (L("name_uploader_title"), "uploader_title") }, s.FileNaming);
            var number = Check(L("set_number"), s.NumberPlaylistItems);
            var plFolder = Check(L("set_playlist_folder"), s.PlaylistSubfolder);
            var siteFolder = Check(L("set_site_folder"), s.SiteSubfolder);
            var thumb = Check(L("set_thumbnail"), s.EmbedThumbnail);
            var meta = Check(L("set_metadata"), s.EmbedMetadata);

            Heading(L("set_subtitles"));
            var subs = Combo(L("set_subtitles_mode"), new[] { (L("subs_off"), "off"), (L("subs_files"), "files"), (L("subs_embed"), "embed") }, s.Subtitles);
            var langs = Field(L("set_sub_langs"), s.SubtitleLanguages);
            var autoSubs = Check(L("set_auto_subs"), s.AutoSubtitles);

            Heading(L("set_login"));
            Text(L("set_cookies_hint"), secondary: true);
            var cookies = Combo(L("set_cookies"), new[]
            {
                (L("cookies_none"), ""), ("Firefox", "firefox"), ("Microsoft Edge", "edge"), ("Google Chrome", "chrome"),
                ("Brave", "brave"), ("Opera", "opera"), ("Vivaldi", "vivaldi")
            }, s.CookiesBrowser ?? "");

            Heading(L("set_tools"));
            var upd = Check(L("set_auto_update"), s.AutoUpdateYtdlp);

            Finish(
                Btn(L("btn_ok"), () =>
                {
                    s.PlaylistPolicy = SelTag(policy);
                    s.AutoStart = auto.IsChecked == true;
                    s.SimultaneousDownloads = int.TryParse(SelTag(sim), out int n) ? n : 1;
                    s.MessageWhenDone = msg.IsChecked == true;
                    s.OpenFolderWhenDone = open.IsChecked == true;
                    s.SkipAlreadyDownloaded = skip.IsChecked == true;
                    s.FileNaming = SelTag(naming);
                    s.NumberPlaylistItems = number.IsChecked == true;
                    s.PlaylistSubfolder = plFolder.IsChecked == true;
                    s.SiteSubfolder = siteFolder.IsChecked == true;
                    s.EmbedThumbnail = thumb.IsChecked == true;
                    s.EmbedMetadata = meta.IsChecked == true;
                    s.Subtitles = SelTag(subs);
                    s.SubtitleLanguages = string.IsNullOrWhiteSpace(langs.Text) ? "en" : langs.Text.Trim();
                    s.AutoSubtitles = autoSubs.IsChecked == true;
                    s.CookiesBrowser = SelTag(cookies);
                    s.AutoUpdateYtdlp = upd.IsChecked == true;
                    DialogResult = true;
                }, primary: true, isDefault: true),
                Btn(L("btn_cancel"), () => DialogResult = false, isCancel: true));
            Loaded += (_, _) => policy.Focus();
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // TEKST — prečice i "O programu" (read-only polje, čita se red po red)
    // ════════════════════════════════════════════════════════════════════
    public class TextWindow : DialogBase
    {
        public TextWindow(Window owner, string title, string text, Func<string, string> l, params (string label, Action action)[] extra)
            : base(owner, title, l, 600, 560)
        {
            var box = new TextBox
            {
                Text = text, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MinHeight = 360
            };
            AutomationProperties.SetName(box, title);
            Body.Children.Add(box);
            var buttons = extra.Select(x => Btn(x.label, x.action, primary: true)).ToList();
            buttons.Add(Btn(L("btn_close"), Close, isCancel: true));
            Finish(buttons.ToArray());
            Loaded += (_, _) => { box.Focus(); box.CaretIndex = 0; };
        }
    }
}
