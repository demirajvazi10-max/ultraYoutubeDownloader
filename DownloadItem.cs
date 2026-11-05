using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;

namespace UltraYoutubeDownloader
{
    public enum ItemState { Checking, Ready, NeedsChoice, Waiting, Downloading, Done, Failed, Cancelled }

    /// <summary>Jedan link u listi za preuzimanje.</summary>
    public class DownloadItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Changed(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            if (name != nameof(Display)) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Display)));
        }

        public string Url { get; set; } = "";
        public FormatChoice Format { get; set; } = new();

        private string _title = "";
        public string Title { get => _title; set { _title = value; Changed(nameof(Title)); } }

        private string _site = "";
        public string Site { get => _site; set { _site = value; Changed(nameof(Site)); } }

        public string Channel { get; set; } = "";
        public double DurationSec { get; set; }

        // Plejlista
        public bool IsPlaylist { get; set; }
        public bool UrlHasSpecificVideo { get; set; }
        public int Count { get; set; }
        public List<string> EntryTitles { get; set; } = new();
        /// <summary>null = još nije odlučeno; "single", "all" ili lista brojeva "1,4,7".</summary>
        public string PlaylistScope { get; set; }

        private ItemState _state = ItemState.Checking;
        public ItemState State { get => _state; set { _state = value; Changed(nameof(State)); } }

        private double _percent;
        public double Percent { get => _percent; set { _percent = value; Changed(nameof(Percent)); } }

        private string _detail = "";
        /// <summary>Brzina/preostalo vreme, "3 od 12", ili poruka greške.</summary>
        public string Detail { get => _detail; set { _detail = value; Changed(nameof(Detail)); } }

        public List<string> Files { get; } = new();
        public CancellationTokenSource Cts { get; set; }

        /// <summary>Tekst stavke u listi — ovo JAWS čita kad strelica dođe na nju.</summary>
        public Func<DownloadItem, string> Describe { get; set; }
        public string Display => Describe?.Invoke(this) ?? Url;
        public override string ToString() => Display;

        public bool IsActive => State is ItemState.Downloading;
        public bool IsFinished => State is ItemState.Done or ItemState.Failed or ItemState.Cancelled;

        public void Refresh() => Changed(nameof(Display));
    }
}
