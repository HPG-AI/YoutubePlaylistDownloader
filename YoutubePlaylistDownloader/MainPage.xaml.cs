﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using YoutubeExplode;
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;
using YoutubeExplode.Channels;
using YoutubePlaylistDownloader.Utilities;
using YoutubePlaylistDownloader.Objects;

namespace YoutubePlaylistDownloader
{
    public partial class MainPage : UserControl
    {
        private YoutubeClient client;
        private FullPlaylist list = null;
        private IEnumerable<Video> VideoList;
        private Channel channel = null;
        private readonly Dictionary<string, VideoQuality> Resolutions = new Dictionary<string, VideoQuality>()
        {
            { "144p", VideoQuality.Low144 },
            { "240p", VideoQuality.Low240 },
            { "360p", VideoQuality.Medium360 },
            { "480p", VideoQuality.Medium480 },
            { "720p", VideoQuality.High720 },
            { "1080p", VideoQuality.High1080 },
            { "1440p", VideoQuality.High1440 },
            { "2160p", VideoQuality.High2160 },
            { "2880p", VideoQuality.High2880 },
            { "3072p", VideoQuality.High3072 },
            { "4320p", VideoQuality.High4320 }
        };

        private readonly string[] FileTypes = { "mp3", "aac", "opus", "wav", "flac", "m4a", "ogg", "webm" };

        public MainPage()
        {
            InitializeComponent();

            GlobalConsts.HideHomeButton();
            GlobalConsts.ShowSettingsButton();
            GlobalConsts.ShowAboutButton();
            GlobalConsts.ShowHelpButton();
            GlobalConsts.ShowSubscriptionsButton();

            VideoList = new List<Video>();
            client = GlobalConsts.YoutubeClient;

            void UpdateSize(object s, SizeChangedEventArgs e)
            {
                double height = GlobalConsts.GetOffset() - (HeadlineStackPanel.ActualHeight + 75);
                GridScrollViewer.Height = height;
                QueueScrollViewer.Height = height;
                BulkScrollViewer.Height = height - 110;
                GridScrollViewer.UpdateLayout();
                QueueScrollViewer.UpdateLayout();
                BulkScrollViewer.UpdateLayout();
            }

            GlobalConsts.Current.SizeChanged += UpdateSize;
            Unloaded += (s, e) => GlobalConsts.Current.SizeChanged -= UpdateSize;

            GlobalConsts.MainPage = this;
        }

        public MainPage Load()
        {
            GlobalConsts.HideHomeButton();
            GlobalConsts.ShowSettingsButton();
            GlobalConsts.ShowAboutButton();
            GlobalConsts.ShowHelpButton();
            GlobalConsts.ShowSubscriptionsButton();
            return this;
        }

        private async void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (YoutubeHelpers.TryParsePlaylistId(PlaylistLinkTextBox.Text, out string playlistId))
                {
                    _ = Task.Run(async () =>
                    {
                        Playlist basePlaylist = await client.Playlists.GetAsync(playlistId).ConfigureAwait(false);
                        list = new FullPlaylist(basePlaylist, await client.Playlists.GetVideosAsync(basePlaylist.Id).BufferAsync().ConfigureAwait(false));
                        VideoList = new List<Video>();
                        await UpdatePlaylistInfo(Visibility.Visible, list.BasePlaylist.Title, list.BasePlaylist.Author, list.BasePlaylist.Engagement.ViewCount.ToString(),list.Videos.Count().ToString(), $"https://img.youtube.com/vi/{list?.Videos?.FirstOrDefault()?.Id}/0.jpg", true, true);
                    }).ConfigureAwait(false);
                }
                else if (YoutubeHelpers.TryParseChannelId(PlaylistLinkTextBox.Text, out string channelId))
                {
                    _ = Task.Run(async () =>
                    {
                        channel = await client.Channels.GetAsync(channelId).ConfigureAwait(false);
                        list = new FullPlaylist(null, null);
                        VideoList = await client.Channels.GetUploadsAsync(channel.Id).BufferAsync().ConfigureAwait(false); ;
                        await UpdatePlaylistInfo(Visibility.Visible, channel.Title, totalVideos: VideoList.Count().ToString(), imageUrl: channel.LogoUrl, downloadEnabled: true, showIndexes: true);
                    }).ConfigureAwait(false);
                }
                else if (YoutubeHelpers.TryParseUsername(PlaylistLinkTextBox.Text, out string username))
                {
                    _ = Task.Run(async () =>
                    {
                        var channel = await client.Channels.GetByUserAsync(username).ConfigureAwait(false);
                        list = new FullPlaylist(null, null);
                        VideoList = await client.Channels.GetUploadsAsync(channel.Id).BufferAsync().ConfigureAwait(false);
                        await UpdatePlaylistInfo(Visibility.Visible, channel.Title,totalVideos: VideoList.Count().ToString(), imageUrl: channel.LogoUrl, downloadEnabled: true, showIndexes: true);
                    }).ConfigureAwait(false);
                }
                else if (YoutubeHelpers.TryParseVideoId(PlaylistLinkTextBox.Text, out string videoId))
                {
                    _ = Task.Run(async () =>
                    {
                        var video = await client.Videos.GetAsync(videoId);
                        VideoList = new List<Video> { video };
                        list = new FullPlaylist(null, null);
                        await UpdatePlaylistInfo(Visibility.Visible, video.Title, video.Author, video.Engagement.ViewCount.ToString(), string.Empty, $"https://img.youtube.com/vi/{video.Id}/0.jpg", true, false);

                    }).ConfigureAwait(false);
                }
                else
                {
                    await UpdatePlaylistInfo().ConfigureAwait(false);
                }
            }

            catch (Exception ex)
            {
                await GlobalConsts.Log(ex.ToString(), "MainPage TextBox_TextChanged");
                await GlobalConsts.ShowMessage((string)FindResource("Error"), ex.Message);
            }
        }

        private void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (list != null || VideoList.Any())
            {
                GlobalConsts.LoadPage(new DownloadPage(list, GlobalConsts.DownloadSettings.Clone(), videos: VideoList));
                VideoList= new List<Video>();
                PlaylistLinkTextBox.Text = string.Empty;
            }
        }

        private async Task UpdatePlaylistInfo(Visibility vis = Visibility.Collapsed, string title = "", string author = "", string views = "", string totalVideos = "", string imageUrl = "", bool downloadEnabled = false, bool showIndexes = false)
            => await Dispatcher.InvokeAsync(() =>
            {
                if (!string.IsNullOrWhiteSpace(imageUrl))
                {
                    PlaylistInfoImage.Source = new BitmapImage(new Uri(imageUrl));
                    PlaylistInfoImage.Visibility = Visibility.Visible;
                }
                else
                    PlaylistInfoImage.Visibility = Visibility.Collapsed;

                PlaylistInfoGrid.Visibility = vis;
                PlaylistTitleTextBlock.Text = title;
                PlaylistAuthorTextBlock.Text = author;
                PlaylistViewsTextBlock.Text = views;

                if (!string.IsNullOrWhiteSpace(totalVideos))
                {
                    PlaylistTotalVideosTextBlockText.Visibility = Visibility.Visible;
                    PlaylistTotalVideosTextBlock.Visibility = Visibility.Visible;
                    PlaylistTotalVideosTextBlock.Text = totalVideos;
                }
                else
                {
                    PlaylistTotalVideosTextBlockText.Visibility = Visibility.Collapsed;
                    PlaylistTotalVideosTextBlock.Visibility = Visibility.Collapsed;
                }

                DownloadButton.IsEnabled = downloadEnabled;
                DownloadInBackgroundButton.IsEnabled = downloadEnabled;

            });

        private void DownloadInBackgroundButton_Click(object sender, RoutedEventArgs e)
        {
            if (list != null || VideoList.Any())
            {
                new DownloadPage(list, GlobalConsts.DownloadSettings.Clone(), silent: true, videos: VideoList);
                VideoList= new List<Video>();
                PlaylistLinkTextBox.Text = string.Empty;
            }
        }

        private void Tile_Click(object sender, RoutedEventArgs e)
        {
            GlobalConsts.LoadFlyoutPage(new DownloadSettingsControl());
        }

        private void BulkDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            var links = BulkLinksTextBox.Text.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            _ = DownloadPage.SequenceDownload(links, GlobalConsts.DownloadSettings.Clone(), silent: true);
            BulkLinksTextBox.Text = string.Empty;
            MetroAnimatedTabControl.SelectedItem = QueueMetroTabItem;
        }

        private void TextBox_TextChanged_1(object sender, TextChangedEventArgs e)
        {
            BulkDownloadButton.IsEnabled = !string.IsNullOrWhiteSpace(BulkLinksTextBox.Text);
        }
    }
}