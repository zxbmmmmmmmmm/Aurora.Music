// Copyright (c) Aurora Studio. All rights reserved.
//
// Licensed under the MIT License. See LICENSE in the project root for license information.
using AudioVisualizer;
using Aurora.Music.Controls;
using Aurora.Music.Core;
using Aurora.Music.Core.Models;
using Aurora.Music.ViewModels;
using Aurora.Shared;
using Aurora.Shared.Extensions;
using Aurora.Shared.Helpers;
using LyricRenderer.Uwp.RollingCalculators;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using System;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using ALRC.Converters;
using LrcConverter = LyricRenderer.Uwp.Converters.LrcConverter;
using Windows.Storage;
using LyricRenderer.Uwp.Converters;
using Windows.Storage.Pickers;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using System.IO;
using System.Linq;
using Impressionist.Implementations;
using System.Text;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Buffer = Windows.Storage.Streams.Buffer;
using Windows.UI.Xaml.Media.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace Aurora.Music.Pages
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    [UriActivate("now", Usage = ActivateUsage.Query)]
    [ObservableObject]
    public sealed partial class NowPlayingPage : Page, IRequestGoBack
    {
        internal static NowPlayingPage Current;
        private float canvasWidth;
        private float canvasHeight;

        public bool NewSpec { get; private set; }

        public NowPlayingPage()
        {
            InitializeComponent();
            Current = this;
            Context.SongChanged += Context_SongChanged;
            Window.Current.SizeChanged += OnWindowResized;
            this.Loaded += OnLoaded;
        }

        private void OnWindowResized(object sender, WindowSizeChangedEventArgs e)
        {
            UpdateLyricSize();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            InitializeLyricView();
        }

        public void InitializeLyricView()
        {
            LyricRenderer.Context.LineRollingEaseCalculator = new ElasticEaseRollingCalculator();
            LyricRenderer.OnBeforeRender += LyricRender_OnBeforeRender; ;
            LyricRenderer.OnRequestSeek += LyricRender_OnRequestSeek; ;
            LyricRenderer.Context.LyricWidthRatio = 1;
            LyricRenderer.Context.LyricPaddingTopRatio = 10 / 100f;
            LyricRenderer.Context.CurrentLyricTime = 0;
            LyricRenderer.Context.Debug = Settings.Current.DebugModeEnabled;
            LyricRenderer.Context.Effects.Blur = true;
            LyricRenderer.Context.LineRollingEaseCalculator = 0 switch
            {
                1 => new SinRollingCalculator(),
                2 => new LyricifyRollingCalculator(),
                3 => new SyncRollingCalculator(),
                _ => new ElasticEaseRollingCalculator()
            };
            LyricRenderer.ChangeAlignment(TextAlignment.Left);
            LyricRenderer.Context.Effects.ScaleWhenFocusing = true;
            LyricRenderer.Context.Effects.FocusHighlighting = true;
            LyricRenderer.Context.Effects.TransliterationScanning = true;
            LyricRenderer.Context.Effects.SimpleLineScanning = false;
            LyricRenderer.ChangeRenderColor(Color.FromArgb(128, 255, 255, 255), Colors.White);
            //LyricRender.Context.PreferTypography.Font = Common.Setting.lyricFontFamily;
            LyricRenderer.Context.LineSpacing = 0;
            UpdateLyricSize();
        }



        private SolidColorBrush GetAccentBrush()
        {
            return Application.Current.Resources["SystemControlPageTextBaseHighBrush"] as SolidColorBrush;
        }

        private SolidColorBrush GetIdleBrush()
        {
            return Application.Current.Resources["TextFillColorTertiaryBrush"] as SolidColorBrush;
        }

        [RelayCommand]
        public async Task LoadLocalLyrics()
        {

            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".qrc");
            picker.FileTypeFilter.Add(".lrc");
            picker.FileTypeFilter.Add(".yrc");
            picker.FileTypeFilter.Add(".alrc");
            var sf = await picker.PickSingleFileAsync();
            if (sf != null)
            {
                var qrc = await FileIO.ReadTextAsync(sf);
                ILyricConverter<string> converter = sf.FileType switch
                {
                    ".qrc" => new QQLyricConverter(),
                    ".yrc" => new NeteaseYrcConverter(),
                    ".lrc" => new ALRC.Converters.LrcConverter(),
                    ".alrc" => new ALRCConverter(),
                    ".ttml" => new AppleSyllableConverter(),
                    _ => new LyricifySyllableConverter()
                };
                var lrcs = LrcConverter.Convert(converter.Convert(qrc));
                LyricRenderer.SetLyricLines(lrcs);
            }
            LyricRenderer.ReflowTime(0);
        }

        private void UpdateLyricSize()
        {
            //var lyricSize = Common.Setting.lyricSize <= 0
            //    ? Math.Max(_widget.WindowBounds.Width / 20, 40)
            //    : Common.Setting.lyricSize;
            //var translationSize = (Common.Setting.translationSize > 0) ? Common.Setting.translationSize : lyricSize / 1.8;
            var lyricSize = Math.Clamp(LyricRenderer.ActualWidth / 20, 20,64);
            var translationSize = lyricSize / 1.8;
            LyricRenderer.ChangeRenderFontSize((float)lyricSize, (float)translationSize, (float)translationSize / 1.2f);
        }

        private void LyricRender_OnRequestSeek(long time)
        {
            Context.PositionChange(TimeSpan.FromMilliseconds(time));
        }

        private void LyricRender_OnBeforeRender(LyricRenderer.Uwp.LyricRenderView view)
        {
            if (Context is null) return;
            view.Context.IsPlaying = Context.IsPlaying is true;
            if (Context.Player.MediaPlayer.PlaybackSession.Position.TotalMilliseconds < view.Context.CurrentLyricTime)
            {
                view.Context.CurrentLyricTime = (long)(Context.Player.MediaPlayer.PlaybackSession.Position.TotalMilliseconds);
                LyricRenderer.ReflowTime(0);
            }
            else
            {
                view.Context.CurrentLyricTime = (long)Context.Player.MediaPlayer.PlaybackSession.Position.TotalMilliseconds;
            }
        }

        private void PlayBtn_Click(object sender, RoutedEventArgs e)
        {
            MainPageViewModel.Current.SkiptoItem((sender as Button).DataContext as SongViewModel);
        }

        private async void Context_SongChanged(object sender, EventArgs e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
            {
                lock (this)
                {
                    

                    var s = sender as SongViewModel;
                    if (MoreMenu.Items[1] is MenuFlyoutSeparator)
                    {

                    }
                    else
                    {
                        MoreMenu.Items.RemoveAt(1);
                    }
                    if (!s.Song.IsPodcast)
                        if (!s.Song.Performers.IsNullorEmpty())
                        {
                            if (s.Song.Performers.Length == 1)
                            {
                                var menuItem = new MenuFlyoutItem()
                                {
                                    Text = $"{s.Song.Performers[0]}",
                                    Icon = new FontIcon()
                                    {
                                        Glyph = "\uE136"
                                    }
                                };
                                menuItem.Click += OpenArtistViewDialog;
                                MoreMenu.Items.Insert(1, menuItem);
                            }
                            else
                            {
                                var sub = new MenuFlyoutSubItem()
                                {
                                    Text = $"{Consts.Localizer.GetString("PerformersText")}",
                                    Icon = new FontIcon()
                                    {
                                        Glyph = "\uE136"
                                    }
                                };
                                foreach (var item in s.Song.Performers)
                                {
                                    var menuItem = new MenuFlyoutItem()
                                    {
                                        Text = item
                                    };
                                    menuItem.Click += OpenArtistViewDialog;
                                    sub.Items.Add(menuItem);
                                }
                                MoreMenu.Items.Insert(1, sub);
                            }
                        }
                }
            });
            

            if (Context.Song is null or { IsOnline:true}) return;
            var songPath = Context.Song.Song.FilePath;
            try
            {
                var lrcPath = songPath.Remove(songPath.LastIndexOf(".")) + ".alrc";
                var file = await StorageFile.GetFileFromPathAsync(lrcPath);
                var qrc = await FileIO.ReadTextAsync(file);
                ILyricConverter<string> converter = file.FileType switch
                {
                    ".qrc" => new QQLyricConverter(),
                    ".yrc" => new NeteaseYrcConverter(),
                    ".lrc" => new ALRC.Converters.LrcConverter(),
                    ".alrc" => new ALRCConverter(),
                    ".ttml" => new AppleSyllableConverter(),
                    _ => new LyricifySyllableConverter()
                };
                var lrcs = LrcConverter.Convert(converter.Convert(qrc));
                LyricRenderer.SetLyricLines(lrcs);
            }
            catch(FileNotFoundException)
            {
                LyricRenderer.SetLyricLines(null);
            }

        }

        private async void OpenArtistViewDialog(object sender, RoutedEventArgs e)
        {
            var artist = (sender as MenuFlyoutItem).Text;
            var dialog = new ArtistViewDialog(new ArtistViewModel()
            {
                Name = artist,
            });
            await dialog.ShowAsync();
        }

        private async void ClickArtistViewDialog(object sender, RoutedEventArgs e)
        {
            var artist = Context.Song.GetFormattedArtists();
            var dialog = new ArtistViewDialog(new ArtistViewModel()
            {
                Name = artist,
            });
            await dialog.ShowAsync();
        }

        public void RequestGoBack()
        {
            ConnectedAnimationService.GetForCurrentView().PrepareToAnimate(Consts.NowPlayingPageInAnimation, Artwork);
            ConnectedAnimationService.GetForCurrentView().PrepareToAnimate($"{Consts.NowPlayingPageInAnimation}_1", Title);
            ConnectedAnimationService.GetForCurrentView().PrepareToAnimate($"{Consts.NowPlayingPageInAnimation}_2", Album);
            Root.Background = new SolidColorBrush(Colors.Transparent);
            MainPage.Current.GoBackFromNowPlaying();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            MainPageViewModel.Current.Title = Consts.Localizer.GetString("NowPlayingText");
            MainPageViewModel.Current.NeedShowTitle = false;

            MainPageViewModel.Current.NeedShowBack = true;


            if (e.Parameter is SongViewModel s)
            {
                Context.Init(s);
            }
            else
            {
                throw new Exception();
            }
            return;
            Task.Run(async () =>
            {
                await Task.Delay(160);
                await Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
                {
                    var ani = ConnectedAnimationService.GetForCurrentView().GetAnimation(Consts.NowPlayingPageInAnimation);
                    if (ani != null)
                    {
                        ani.TryStart(Artwork, new UIElement[] { Root });
                    }
                    ani = ConnectedAnimationService.GetForCurrentView().GetAnimation($"{Consts.NowPlayingPageInAnimation}_1");
                    if (ani != null)
                    {
                        ani.TryStart(Title);
                    }
                    ani = ConnectedAnimationService.GetForCurrentView().GetAnimation($"{Consts.NowPlayingPageInAnimation}_2");
                    if (ani != null)
                    {
                        ani.TryStart(Album);
                    }
                });
            });
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            MainPageViewModel.Current.IsVisualizing = false;
            SizeChanged -= NowPlayingPage_SizeChanged;
            Context.SongChanged -= Context_SongChanged;
            Context?.Dispose();
            MainPageViewModel.Current.IsVisualizing = false;
            Unload();
        }

        private async void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            return;
            if (Context.Lyric.Contents.Count > 0 && (sender as ListView).SelectedIndex >= 0)
                try
                {
                    await (sender as ListView).ScrollToIndex((sender as ListView).SelectedIndex, ScrollPosition.Center);
                }
                catch (Exception)
                {
                }
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            Context?.Dispose();
            MainPageViewModel.Current.IsVisualizing = false;
            Unload();
        }

        private void Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            Context.PositionChange(Context.TotalDuration * (e.NewValue / 100d));
        }

        private async void OpenAlbumViewDialog(object sender, RoutedEventArgs e)
        {
            var dialog = new AlbumViewDialog(await Context.GetAlbumAsync());
            await dialog.ShowAsync();
        }

        internal void Unload()
        {
            Context?.Unload();
            Context = null;
        }

        private async void FindFileClick(object sender, RoutedEventArgs e)
        {
            await Context.FindFileAsync();
        }

        private async void DownloadOrModify(object sender, RoutedEventArgs e)
        {
            await Context.DowmloadOrModifyAsync();
        }

        private void Share_Click(object sender, RoutedEventArgs e)
        {
            Context.ShareCurrentAsync();
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            await Context.DeleteCurrentAsync(Windows.Storage.StorageDeleteOption.Default);
        }

        private async void Delete_Click_1(object sender, RoutedEventArgs e)
        {
            await Context.DeleteCurrentAsync(Windows.Storage.StorageDeleteOption.PermanentDelete);
        }

        private async void RatingControl_ValueChanged(RatingControl sender, object args)
        {
            await Context.WriteRatingValue(sender.Value);
        }

        private void Cast_Click(object sender, RoutedEventArgs e)
        {
            //Retrieve the location of the casting button
            GeneralTransform transform = (sender as MenuFlyoutItem).TransformToVisual(Window.Current.Content as UIElement);
            Point pt = transform.TransformPoint(new Point(0, 0));

            Context.ShowCastingUI(new Rect(pt.X, pt.Y, (sender as MenuFlyoutItem).ActualWidth, (sender as MenuFlyoutItem).ActualHeight));

        }

        internal bool IsDarkTheme()
        {
            switch (Settings.Current.Theme)
            {
                case ElementTheme.Default:
                    return Palette.IsDarkColor((Resources["SystemControlBackgroundAltHighBrush"] as SolidColorBrush).Color);
                case ElementTheme.Light:
                    return false;
                case ElementTheme.Dark:
                    return true;
                default:
                    return Palette.IsDarkColor((Resources["SystemControlBackgroundAltHighBrush"] as SolidColorBrush).Color);
            }
        }

        private void VisualStateGroup_CurrentStateChanged(object sender, VisualStateChangedEventArgs e)
        {
            ListView_SelectionChanged(LrcView, null);
            if (e.NewState.Name != "Full")
            {

            }
        }

        private void LrcHeader_Loaded(object sender, RoutedEventArgs e)
        {
            SizeChanged += NowPlayingPage_SizeChanged;
            var h = (Artwork.ActualHeight + LrcHeader.ActualHeight) / 2;
            if (h < 24)
            {
                h = 24;
            }
            LrcHeaderGrid.Height = h;
        }

        private void NowPlayingPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var h = (Artwork.ActualHeight + LrcHeader.ActualHeight) / 2;
            if (h < 24)
            {
                h = 24;
            }
            LrcHeaderGrid.Height = h;
        }

        private async void Flyout_Opened(object sender, object e)
        {
            if (NowPlayingFlyout.SelectedIndex != -1)
                await NowPlayingFlyout.ScrollToIndex(NowPlayingFlyout.SelectedIndex, ScrollPosition.Center);
        }

        private async void Artwork_ImageOpened(object sender, RoutedEventArgs e)
        {
            var service = ConnectedAnimationService.GetForCurrentView();
            var ani = service.GetAnimation(Consts.NowPlayingPageInAnimation);
            if (ani != null)
            {
                ani.TryStart(Artwork, new UIElement[] { Root });
            }
            ani = service.GetAnimation($"{Consts.NowPlayingPageInAnimation}_1");
            if (ani != null)
            {
                ani.TryStart(Title);
            }
            ani = service.GetAnimation($"{Consts.NowPlayingPageInAnimation}_2");
            if (ani != null)
            {
                ani.TryStart(Album);
            }
            await Task.Delay(200);
            ani = service.GetAnimation("DropAni");
            if (ani != null)
                ani.TryStart(SongPanel);
        }

        private void NowPlayingFlyout_ItemClick(object sender, ItemClickEventArgs e)
        {
            MainPageViewModel.Current.SkiptoItem(e.ClickedItem as SongViewModel);
        }
    }
}
