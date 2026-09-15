using System.Collections.Generic;

namespace UltraYoutubeDownloader
{
    /// <summary>
    /// Prevodi za UI. Isti obrazac kao UltraVideoEditor.LanguageManager (GetText(key, language)),
    /// samo sa manjim skupom ključeva — dovoljno za ovu jednu, namensku aplikaciju.
    /// Srpski je uvek na latinici (nikad ćirilica).
    /// </summary>
    public static class LanguageManager
    {
        private static readonly Dictionary<string, Dictionary<string, string>> _translations;

        static LanguageManager()
        {
            _translations = new Dictionary<string, Dictionary<string, string>>();

            // ==================== ENGLESKI (podrazumevani) ====================
            var en = new Dictionary<string, string>();

            en["window_title"]      = "Ultra YouTube Downloader";
            en["window_aria"]       = "Ultra YouTube Downloader — download video or audio from YouTube";
            en["app_subtitle"]      = "Download video (MP4) or audio (MP3) from a YouTube link.";
            en["status_by"]         = "by Demir Ajvazi";

            en["menu_view"]         = "_View";
            en["menu_theme"]        = "Theme";
            en["theme_dark"]        = "_Dark (default)";
            en["theme_light"]       = "_Light";
            en["theme_contrast"]    = "_High contrast";
            en["theme_dark_plain"]     = "Dark";
            en["theme_light_plain"]    = "Light";
            en["theme_contrast_plain"] = "High contrast";
            en["menu_textsize"]     = "Text size";
            en["text_larger"]       = "_Larger text (Ctrl +)";
            en["text_smaller"]      = "_Smaller text (Ctrl -)";
            en["text_reset"]        = "_Reset text size (Ctrl 0)";
            en["menu_language"]     = "_Language";
            en["lang_en"]           = "_English";
            en["lang_sr"]           = "_Serbian (Srpski)";
            en["menu_open_folder"]  = "_Open destination folder";
            en["menu_help"]         = "_Help";
            en["menu_about"]        = "_About Ultra YouTube Downloader";
            en["menu_view_aria"]    = "View menu";
            en["menu_help_aria"]    = "Help menu";
            en["menu_language_aria"]= "Language menu";
            en["larger_text_aria"]  = "Increase text and control size";
            en["smaller_text_aria"] = "Decrease text and control size";
            en["reset_text_aria"]   = "Reset text and control size to default";
            en["open_folder_menu_aria"] = "Open the destination folder in File Explorer";
            en["about_aria"]        = "About Ultra YouTube Downloader";
            en["theme_dark_aria"]      = "Dark theme";
            en["theme_light_aria"]     = "Light theme";
            en["theme_contrast_aria"]  = "High contrast theme";
            en["lang_en_aria"]      = "Switch language to English";
            en["lang_sr_aria"]      = "Switch language to Serbian";

            en["url_label"]  = "YouTube URL (video or playlist):";
            en["url_aria"]   = "YouTube URL — enter link to video or playlist";
            en["url_help"]   = "Accepts a link to a single video or an entire playlist";

            en["filetype_group"] = "File type";
            en["filetype_aria"]  = "File type selection";
            en["video_option"]   = "Video (MP4)";
            en["video_aria"]     = "Video, MP4 format";
            en["audio_option"]   = "Audio (MP3)";
            en["audio_aria"]     = "Audio, MP3 format";

            en["quality_label"]  = "Video quality:";
            en["quality_aria"]   = "Video quality selection";
            en["q_best"]         = "Best available";
            en["q_1080"]         = "1080p";
            en["q_720"]          = "720p";
            en["q_480"]          = "480p";
            en["q_360"]          = "360p";

            en["audioq_label"] = "MP3 quality:";
            en["audioq_aria"]  = "MP3 audio quality selection";
            en["aq_best"]      = "Best (VBR, largest file)";
            en["aq_320"]       = "320 kbps";
            en["aq_192"]       = "192 kbps (recommended)";
            en["aq_128"]       = "128 kbps (smaller file)";

            en["savefolder_label"] = "Save to folder:";
            en["savefolder_aria"]  = "Destination folder for downloaded file";
            en["browse_btn"]       = "Browse...";
            en["browse_aria"]      = "Choose a different destination folder";
            en["open_btn"]         = "Open";
            en["open_btn_aria"]    = "Open the destination folder in File Explorer";

            en["playlist_prompt"]   = "This link is part of a playlist. What do you want to download?";
            en["scope_group"]       = "Download scope";
            en["scope_group_aria"]  = "Choose whether to download just this video, the entire playlist, or specific tracks";
            en["scope_single"]      = "Just this video (ignore the rest of the playlist)";
            en["scope_single_aria"] = "Just this video, ignore the rest of the playlist";
            en["scope_all"]         = "Entire playlist";
            en["scope_all_aria"]    = "Entire playlist";
            en["scope_choose"]      = "Choose specific tracks...";
            en["scope_choose_aria"] = "Choose specific tracks from the playlist";
            en["tracks_aria"]       = "Playlist tracks — select one or more tracks to download";
            en["choose_tracks_hint"]= "Select one or more tracks from the list to download only those.";

            en["progress_aria"] = "Download progress";
            en["preparing"]     = "Preparing...";
            en["cancelled"]     = "Cancelled.";

            en["btn_loadinfo"]         = "Load info";
            en["btn_loadinfo_loading"] = "Loading...";
            en["btn_loadinfo_aria"]    = "Load video or playlist information";
            en["btn_download"]         = "Download";
            en["btn_download_aria"]    = "Download video or audio";
            en["btn_cancel"]           = "Cancel";
            en["btn_cancel_aria"]      = "Cancel the current download";

            en["err_url_empty"]        = "Error: URL field is empty.";
            en["err_prefix"]           = "Error: {0}";
            en["err_ytdlp_not_found"]  = "yt-dlp not found.";
            en["err_ytdlp_not_found_after_install"] = "yt-dlp not found even after installation.";
            en["err_ffmpeg_not_found_after_install"] = "FFmpeg not found even after installation.";
            en["err_no_response"]      = "No response from yt-dlp. Check the URL.";
            en["err_download_no_files"]= "Download finished but no files were found.";
            en["err_select_track"]     = "Please select at least one track from the list, or choose \"Entire playlist\"";
            en["err_select_track_or_single"] = " or \"Just this video\".";
            en["err_select_track_period"]    = ".";
            en["err_open_folder"]      = "Could not open the folder: {0}";
            en["err_during_download"]  = "Error during download: {0}";

            en["playlist_found_specific"] = "This link is part of a playlist: {0}, {1} videos. Defaulting to just this video — a \"Download scope\" option below lets you switch to the entire playlist or pick specific tracks.";
            en["playlist_found_generic"]  = "Playlist found: {0}, {1} videos. A \"Download scope\" option below lets you download the whole playlist or pick specific tracks.";
            en["video_found"]             = "Video found: {0}, duration {1}.";
            en["info_playlist_title"]     = "🎵 Playlist: {0}  ({1} videos)";
            en["info_video_title"]        = "🎬 {0}";
            en["info_meta_playlist"]      = "Channel: {0}";
            en["info_meta_video"]         = "Channel: {0}   Duration: {1}";

            en["downloading_file"]       = "Downloading: {0}";
            en["download_complete_file"] = "Download complete: {0}";
            en["download_cancelled"]     = "Download cancelled.";
            en["ytdlp_error"]            = "yt-dlp error: {0}";

            en["download_complete_title"]   = "Download complete";
            en["saved_to"]                  = "Saved to: {0}";
            en["saved_multi"]               = "Saved {0} file(s) to: {1}";
            en["download_complete_announce"]= "Download complete. {0}.";

            en["ytdlp_missing_title"] = "yt-dlp not found";
            en["ytdlp_missing_msg"]   = "yt-dlp not found on this system.\n\nYt-dlp is a free tool required for downloading from YouTube.\n\nWould you like to download it automatically? (~10 MB, one time)";
            en["ffmpeg_missing_title"]= "FFmpeg not found";
            en["ffmpeg_missing_msg"]  = "FFmpeg not found on this system.\n\nFFmpeg is a free tool required to convert audio to MP3 and to merge video/audio streams.\n\nWould you like to download it automatically? (~80 MB, one time)";
            en["ffmpeg_install_fail"] = "Unable to install FFmpeg: {0}\n\nYou can also install it manually: download a Windows build from https://www.gyan.dev/ffmpeg/builds/ and place ffmpeg.exe in:\n{1}";
            en["ytdlp_install_fail"]  = "Unable to download yt-dlp: {0}\n\nDownload manually from: https://github.com/yt-dlp/yt-dlp/releases";
            en["ytdlp_downloading"]   = "Downloading yt-dlp, please wait...";
            en["ytdlp_downloaded"]    = "yt-dlp downloaded successfully.";
            en["ytdlp_ready"]         = "yt-dlp ready.";
            en["ffmpeg_downloading"]  = "Downloading FFmpeg, please wait — this file is larger, it may take a few minutes...";
            en["ffmpeg_extracting"]   = "Extracting FFmpeg...";
            en["ffmpeg_installed"]    = "FFmpeg installed successfully.";
            en["ffmpeg_ready"]        = "FFmpeg ready.";

            en["error_title"]   = "Error";
            en["about_title"]   = "About Ultra YouTube Downloader";
            en["about_text"]    = "Ultra YouTube Downloader {0}\n\nPart of the Ultra Creative Suite.\nDownload video or audio from YouTube — accessible for JAWS and NVDA.\n\nCreated by Demir Ajvazi.";

            en["theme_announce"]    = "Theme: {0}";
            en["textsize_announce"] = "Text size: {0}";
            en["language_announce"] = "Language: {0}";

            _translations["en"] = en;

            // ==================== SRPSKI (latinica) ====================
            var sr = new Dictionary<string, string>();

            sr["window_title"]      = "Ultra YouTube Downloader";
            sr["window_aria"]       = "Ultra YouTube Downloader — preuzmite video ili audio sa YouTube-a";
            sr["app_subtitle"]      = "Preuzmi video (MP4) ili audio (MP3) sa YouTube linka.";
            sr["status_by"]         = "autor: Demir Ajvazi";

            sr["menu_view"]         = "_Prikaz";
            sr["menu_theme"]        = "Tema";
            sr["theme_dark"]        = "_Tamna (podrazumevano)";
            sr["theme_light"]       = "_Svetla";
            sr["theme_contrast"]    = "_Visok kontrast";
            sr["theme_dark_plain"]     = "Tamna";
            sr["theme_light_plain"]    = "Svetla";
            sr["theme_contrast_plain"] = "Visok kontrast";
            sr["menu_textsize"]     = "Veličina teksta";
            sr["text_larger"]       = "_Uvećaj tekst (Ctrl +)";
            sr["text_smaller"]      = "_Umanji tekst (Ctrl -)";
            sr["text_reset"]        = "_Podrazumevana veličina (Ctrl 0)";
            sr["menu_language"]     = "_Jezik";
            sr["lang_en"]           = "_Engleski";
            sr["lang_sr"]           = "_Srpski";
            sr["menu_open_folder"]  = "_Otvori odredišni folder";
            sr["menu_help"]         = "_Pomoć";
            sr["menu_about"]        = "_O aplikaciji Ultra YouTube Downloader";
            sr["menu_view_aria"]    = "Meni Prikaz";
            sr["menu_help_aria"]    = "Meni Pomoć";
            sr["menu_language_aria"]= "Meni Jezik";
            sr["larger_text_aria"]  = "Uvećaj tekst i kontrole";
            sr["smaller_text_aria"] = "Umanji tekst i kontrole";
            sr["reset_text_aria"]   = "Vrati podrazumevanu veličinu teksta i kontrola";
            sr["open_folder_menu_aria"] = "Otvorite odredišni folder u Windows Explorer-u";
            sr["about_aria"]        = "O aplikaciji Ultra YouTube Downloader";
            sr["theme_dark_aria"]      = "Tamna tema";
            sr["theme_light_aria"]     = "Svetla tema";
            sr["theme_contrast_aria"]  = "Tema visokog kontrasta";
            sr["lang_en_aria"]      = "Promeni jezik na engleski";
            sr["lang_sr_aria"]      = "Promeni jezik na srpski";

            sr["url_label"]  = "YouTube URL (video ili plejlista):";
            sr["url_aria"]   = "YouTube URL — unesite link ka videu ili plejlisti";
            sr["url_help"]   = "Prihvata link ka pojedinačnom videu ili celoj plejlisti";

            sr["filetype_group"] = "Tip fajla";
            sr["filetype_aria"]  = "Izbor tipa fajla";
            sr["video_option"]   = "Video (MP4)";
            sr["video_aria"]     = "Video, MP4 format";
            sr["audio_option"]   = "Audio (MP3)";
            sr["audio_aria"]     = "Audio, MP3 format";

            sr["quality_label"]  = "Kvalitet videa:";
            sr["quality_aria"]   = "Izbor kvaliteta videa";
            sr["q_best"]         = "Najbolji dostupan";
            sr["q_1080"]         = "1080p";
            sr["q_720"]          = "720p";
            sr["q_480"]          = "480p";
            sr["q_360"]          = "360p";

            sr["audioq_label"] = "MP3 kvalitet:";
            sr["audioq_aria"]  = "Izbor MP3 kvaliteta";
            sr["aq_best"]      = "Najbolji (VBR, najveći fajl)";
            sr["aq_320"]       = "320 kbps";
            sr["aq_192"]       = "192 kbps (preporučeno)";
            sr["aq_128"]       = "128 kbps (manji fajl)";

            sr["savefolder_label"] = "Sačuvaj u folder:";
            sr["savefolder_aria"]  = "Odredišni folder za preuzeti fajl";
            sr["browse_btn"]       = "Izaberi...";
            sr["browse_aria"]      = "Izaberite drugi odredišni folder";
            sr["open_btn"]         = "Otvori";
            sr["open_btn_aria"]    = "Otvorite odredišni folder u Windows Explorer-u";

            sr["playlist_prompt"]   = "Ovaj link je deo plejliste. Šta želite da preuzmete?";
            sr["scope_group"]       = "Obim preuzimanja";
            sr["scope_group_aria"]  = "Izaberite da li preuzimate samo ovaj video, celu plejlistu, ili određene numere";
            sr["scope_single"]      = "Samo ovaj video (zanemari ostatak plejliste)";
            sr["scope_single_aria"] = "Samo ovaj video, zanemari ostatak plejliste";
            sr["scope_all"]         = "Cela plejlista";
            sr["scope_all_aria"]    = "Cela plejlista";
            sr["scope_choose"]      = "Biram numere...";
            sr["scope_choose_aria"] = "Izaberite određene numere iz plejliste";
            sr["tracks_aria"]       = "Numere iz plejliste — izaberite jednu ili više numera za preuzimanje";
            sr["choose_tracks_hint"]= "Izaberite jednu ili više numera sa liste da biste preuzeli samo njih.";

            sr["progress_aria"] = "Napredak preuzimanja";
            sr["preparing"]     = "Priprema...";
            sr["cancelled"]     = "Otkazano.";

            sr["btn_loadinfo"]         = "Učitaj informacije";
            sr["btn_loadinfo_loading"] = "Učitavanje...";
            sr["btn_loadinfo_aria"]    = "Učitajte informacije o videu ili plejlisti";
            sr["btn_download"]         = "Preuzmi";
            sr["btn_download_aria"]    = "Preuzmite video ili audio";
            sr["btn_cancel"]           = "Otkaži";
            sr["btn_cancel_aria"]      = "Otkažite trenutno preuzimanje";

            sr["err_url_empty"]        = "Greška: polje za URL je prazno.";
            sr["err_prefix"]           = "Greška: {0}";
            sr["err_ytdlp_not_found"]  = "yt-dlp nije pronađen.";
            sr["err_ytdlp_not_found_after_install"] = "yt-dlp nije pronađen ni posle instalacije.";
            sr["err_ffmpeg_not_found_after_install"] = "FFmpeg nije pronađen ni posle instalacije.";
            sr["err_no_response"]      = "Nema odgovora od yt-dlp. Proverite URL.";
            sr["err_download_no_files"]= "Preuzimanje je završeno, ali fajlovi nisu pronađeni.";
            sr["err_select_track"]     = "Izaberite bar jednu numeru sa liste, ili izaberite „Cela plejlista“";
            sr["err_select_track_or_single"] = " ili „Samo ovaj video“.";
            sr["err_select_track_period"]    = ".";
            sr["err_open_folder"]      = "Nije moguće otvoriti folder: {0}";
            sr["err_during_download"]  = "Greška prilikom preuzimanja: {0}";

            sr["playlist_found_specific"] = "Ovaj link je deo plejliste: {0}, {1} video(a). Podrazumevano je izabrano samo ovaj video — opcija „Obim preuzimanja“ ispod ti omogućava da izabereš celu plejlistu ili određene numere.";
            sr["playlist_found_generic"]  = "Pronađena je plejlista: {0}, {1} video(a). Opcija „Obim preuzimanja“ ispod ti omogućava da preuzmeš celu plejlistu ili određene numere.";
            sr["video_found"]             = "Video pronađen: {0}, trajanje {1}.";
            sr["info_playlist_title"]     = "🎵 Plejlista: {0}  ({1} video(a))";
            sr["info_video_title"]        = "🎬 {0}";
            sr["info_meta_playlist"]      = "Kanal: {0}";
            sr["info_meta_video"]         = "Kanal: {0}   Trajanje: {1}";

            sr["downloading_file"]       = "Preuzimanje: {0}";
            sr["download_complete_file"] = "Preuzimanje završeno: {0}";
            sr["download_cancelled"]     = "Preuzimanje otkazano.";
            sr["ytdlp_error"]            = "yt-dlp greška: {0}";

            sr["download_complete_title"]   = "Preuzimanje završeno";
            sr["saved_to"]                  = "Sačuvano u: {0}";
            sr["saved_multi"]               = "Sačuvano {0} fajl(ova) u: {1}";
            sr["download_complete_announce"]= "Preuzimanje završeno. {0}.";

            sr["ytdlp_missing_title"] = "yt-dlp nije pronađen";
            sr["ytdlp_missing_msg"]   = "yt-dlp nije pronađen na ovom sistemu.\n\nYt-dlp je besplatan alat neophodan za preuzimanje sa YouTube-a.\n\nDa li želite da ga automatski preuzmete? (~10 MB, jednokratno)";
            sr["ffmpeg_missing_title"]= "FFmpeg nije pronađen";
            sr["ffmpeg_missing_msg"]  = "FFmpeg nije pronađen na ovom sistemu.\n\nFFmpeg je besplatan alat neophodan za konverziju audija u MP3 i spajanje video/audio zapisa.\n\nDa li želite da ga automatski preuzmete? (~80 MB, jednokratno)";
            sr["ffmpeg_install_fail"] = "Nije moguće instalirati FFmpeg: {0}\n\nMožete ga instalirati i ručno: preuzmite Windows verziju sa https://www.gyan.dev/ffmpeg/builds/ i stavite ffmpeg.exe u:\n{1}";
            sr["ytdlp_install_fail"]  = "Nije moguće preuzeti yt-dlp: {0}\n\nPreuzmite ručno sa: https://github.com/yt-dlp/yt-dlp/releases";
            sr["ytdlp_downloading"]   = "Preuzimam yt-dlp, sačekajte...";
            sr["ytdlp_downloaded"]    = "yt-dlp je uspešno preuzet.";
            sr["ytdlp_ready"]         = "yt-dlp je spreman.";
            sr["ffmpeg_downloading"]  = "Preuzimam FFmpeg, sačekajte — ovaj fajl je veći, može potrajati par minuta...";
            sr["ffmpeg_extracting"]   = "Raspakivanje FFmpeg-a...";
            sr["ffmpeg_installed"]    = "FFmpeg je uspešno instaliran.";
            sr["ffmpeg_ready"]        = "FFmpeg je spreman.";

            sr["error_title"]   = "Greška";
            sr["about_title"]   = "O aplikaciji Ultra YouTube Downloader";
            sr["about_text"]    = "Ultra YouTube Downloader {0}\n\nDeo Ultra Creative Suite-a.\nPreuzmi video ili audio sa YouTube-a — prilagođeno za JAWS i NVDA.\n\nAutor: Demir Ajvazi.";

            sr["theme_announce"]    = "Tema: {0}";
            sr["textsize_announce"] = "Veličina teksta: {0}";
            sr["language_announce"] = "Jezik: {0}";

            _translations["sr"] = sr;
        }

        public static string GetText(string key, string language = "en")
        {
            if (_translations.TryGetValue(language, out var dict) && dict.TryGetValue(key, out var value))
                return value;

            if (_translations["en"].TryGetValue(key, out var fallback))
                return fallback;

            return key;
        }
    }
}
