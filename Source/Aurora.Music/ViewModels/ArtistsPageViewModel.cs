// Copyright (c) Aurora Studio. All rights reserved.
//
// Licensed under the MIT License. See LICENSE in the project root for license information.
using Aurora.Music.Core;
using Aurora.Music.Core.Storage;
using Aurora.Shared.MVVM;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.System.Threading;
using Windows.UI.StartScreen;

namespace Aurora.Music.ViewModels
{
    partial class ArtistsPageViewModel : ViewModelBase
    {
        [ObservableProperty]
        private ObservableCollection<GroupedItem<ArtistViewModel>> artistList;

        [RelayCommand]
        public async Task PlayAll()
        {
            await MainPageViewModel.Current.InstantPlayAsync(await FileReader.GetAllSongAsync());
        }

        [ObservableProperty]
        private string artistsCount;

        [ObservableProperty]
        private string songsCount;

        public ArtistsPageViewModel()
        {
            ArtistList = new ObservableCollection<GroupedItem<ArtistViewModel>>();
        }

        public async Task GetArtists()
        {
            var opr = SQLOperator.Current();
            var artists = await opr.GetArtistsAsync();
            var grouped = GroupedItem<ArtistViewModel>.CreateGroupsByAlpha(artists.ConvertAll(x => new ArtistViewModel
            {
                Name = x.AlbumArtists,
                SongsCount = x.Count
            }));

            await CoreApplication.MainView.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, () =>
            {
                ArtistList.Clear();

                long sum = 0;
                foreach (var item in grouped)
                {
                    ArtistList.Add(item);
                    sum += item.Sum(x => x.SongsCount);
                }
                ArtistsCount = SmartFormat.Smart.Format(Consts.Localizer.GetString("SmartArtists"), artists.Count);
                SongsCount = SmartFormat.Smart.Format(Consts.Localizer.GetString("SmartSongs"), sum);

                var t = ThreadPool.RunAsync(async x =>
                {
                    foreach (var item in ArtistList)
                    {
                        foreach (var art in item)
                        {
                            var uri = await opr.GetAvatarAsync(art.RawName);
                            if (Uri.TryCreate(uri, UriKind.Absolute, out var u))
                            {
                                await CoreApplication.MainView.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Low, () =>
                                {
                                    art.Avatar = u;
                                });
                            }
                            else
                            {

                            }
                        }
                    }
                });
            });
        }


        public string PinnedToGlyph(bool b)
        {
            return b ? "\uE196" : "\uE141";
        }
        public string PinnedToText(bool b)
        {
            return b ? Consts.Localizer.GetString("UnPinText") : Consts.Localizer.GetString("PinText");
        }
        [RelayCommand]
        public async Task PinToStart()
        {
            // Construct a unique tile ID, which you will need to use later for updating the tile
            var tileId = $"artists";
            if (SecondaryTile.Exists(tileId))
            {
                // Initialize a secondary tile with the same tile ID you want removed
                var toBeDeleted = new SecondaryTile(tileId);

                // And then unpin the tile
                await toBeDeleted.RequestDeleteAsync();
            }
            else
            {
                // Use a display name you like
                var displayName = "Artists";

                // Provide all the required info in arguments so that when user
                // clicks your tile, you can navigate them to the correct content
                var arguments = $"as-music:///library/artists";

                // Initialize the tile with required arguments
                var tile = new SecondaryTile
                {
                    Arguments = arguments,
                    TileId = tileId,
                    DisplayName = displayName
                };
                tile.VisualElements.Square150x150Logo = new Uri("ms-appx:///Assets/Square150x150Logo.png");
                // Enable wide and large tile sizes
                tile.VisualElements.Wide310x150Logo = new Uri("ms-appx:///Assets/Wide310x150Logo.png");
                tile.VisualElements.Square310x310Logo = new Uri("ms-appx:///Assets/LargeTile.png");

                // Add a small size logo for better looking small tile
                tile.VisualElements.Square71x71Logo = new Uri("ms-appx:///Assets/SmallTile.png");

                // Add a unique corner logo for the secondary tile
                tile.VisualElements.Square44x44Logo = new Uri("ms-appx:///Assets/Square44x44Logo.png");

                tile.VisualElements.ShowNameOnSquare150x150Logo = true;
                tile.VisualElements.ShowNameOnWide310x150Logo = true;
                tile.VisualElements.ShowNameOnSquare310x310Logo = true;

                // Pin the tile
                await tile.RequestCreateAsync();
            }

            IsPinned = SecondaryTile.Exists(tileId);
        }

        [ObservableProperty]
        private bool isPinned;
    }
}
