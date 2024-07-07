﻿// Copyright (c) Aurora Studio. All rights reserved.
//
// Licensed under the MIT License. See LICENSE in the project root for license information.
using Aurora.Music.Core;
using Aurora.Music.Core.Models;
using Aurora.Music.Core.Storage;
using Aurora.Shared.MVVM;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.StartScreen;

namespace Aurora.Music.ViewModels
{
    partial class AlbumsPageViewModel : ViewModelBase
    {
        [ObservableProperty]
        private ObservableCollection<GroupedItem<AlbumViewModel>> albumList;

        [ObservableProperty]
        private string artistsCount;//genres

        [ObservableProperty]
        private string songsCount;

        public AlbumsPageViewModel()
        {
            AlbumList = new ObservableCollection<GroupedItem<AlbumViewModel>>();
        }

        [RelayCommand]
        public async Task PlayAll()
        {
            await MainPageViewModel.Current.InstantPlayAsync(await FileReader.GetAllSongAsync());
        }

        public int SortIndex { get; internal set; } = 0;

        public async Task GetAlbums()
        {
            var albums = await FileReader.GetAllAlbumsAsync();

            //var grouped = GroupedItem<AlbumViewModel>.CreateGroupsByAlpha(albums.ConvertAll(x => new AlbumViewModel(x)));

            //var grouped = GroupedItem<AlbumViewModel>.CreateGroups(albums.ConvertAll(x => new AlbumViewModel(x)), x => x.GetFormattedArtists());
            IEnumerable<GroupedItem<AlbumViewModel>> grouped;

            switch (Settings.Current.AlbumsSort)
            {
                case SortMode.Alphabet:
                    grouped = GroupedItem<AlbumViewModel>.CreateGroupsByAlpha(albums.ConvertAll(x => new AlbumViewModel(x)));
                    SortIndex = 1;
                    break;
                case SortMode.Year:
                    grouped = GroupedItem<AlbumViewModel>.CreateGroups(albums.ConvertAll(x => new AlbumViewModel(x)), x => x.Year, true);
                    SortIndex = 0;
                    break;
                case SortMode.Artist:
                    grouped = GroupedItem<AlbumViewModel>.CreateGroups(albums.ConvertAll(x => new AlbumViewModel(x)), x => x.GetFormattedArtists());
                    SortIndex = 2;
                    break;
                default:
                    grouped = GroupedItem<AlbumViewModel>.CreateGroups(albums.ConvertAll(x => new AlbumViewModel(x)), x => x.Year, true);
                    SortIndex = 0;
                    break;
            }

            var aCount = await FileReader.GetArtistsCountAsync();

            await CoreApplication.MainView.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High, () =>
            {
                AlbumList.Clear();
                foreach (var item in grouped)
                {
                    AlbumList.Add(item);
                }
                SongsCount = SmartFormat.Smart.Format(Consts.Localizer.GetString("SmartAlbums"), albums.Count);
                ArtistsCount = SmartFormat.Smart.Format(Consts.Localizer.GetString("SmartArtists"), aCount);


                var tileId = "albums";
                IsPinned = SecondaryTile.Exists(tileId);

                if (IsPinned)
                {
                    Core.Tools.Tile.UpdateImage(tileId, AlbumList.SelectMany(a => a.Where(c => c.ArtworkUri != null).Select(b => b.ArtworkUri.OriginalString)).Distinct().OrderBy(x => Guid.NewGuid()).Take(10).ToList(), Consts.Localizer.GetString("AlbumsText"), Consts.Localizer.GetString("AlbumsTile"));
                }
            });
        }

        internal async Task PlayAlbumAsync(AlbumViewModel album)
        {
            var songs = await album.GetSongsAsync();
            await MainPageViewModel.Current.InstantPlayAsync(songs);
        }

        internal async void ChangeSort(int selectedIndex)
        {
            AlbumList.Clear();
            var albums = await FileReader.GetAllAlbumsAsync();
            IEnumerable<GroupedItem<AlbumViewModel>> grouped;
            switch (selectedIndex)
            {
                case 0:
                    grouped = GroupedItem<AlbumViewModel>.CreateGroups(albums.ConvertAll(x => new AlbumViewModel(x)), x => x.Year, true);
                    Settings.Current.AlbumsSort = SortMode.Year;
                    break;
                case 1:
                    grouped = GroupedItem<AlbumViewModel>.CreateGroupsByAlpha(albums.ConvertAll(x => new AlbumViewModel(x)));
                    Settings.Current.AlbumsSort = SortMode.Alphabet;
                    break;
                case 2:
                    grouped = GroupedItem<AlbumViewModel>.CreateGroups(albums.ConvertAll(x => new AlbumViewModel(x)), x => x.GetFormattedArtists());
                    Settings.Current.AlbumsSort = SortMode.Artist;
                    break;
                default:
                    grouped = GroupedItem<AlbumViewModel>.CreateGroups(albums.ConvertAll(x => new AlbumViewModel(x)), x => x.Year, true);
                    Settings.Current.AlbumsSort = SortMode.Year;
                    break;
            }
            SortIndex = selectedIndex;
            Settings.Current.Save();
            foreach (var item in grouped)
            {
                AlbumList.Add(item);
            }

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
            var tileId = "albums";
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
                var displayName = Consts.Localizer.GetString("AlbumsText");

                // Provide all the required info in arguments so that when user
                // clicks your tile, you can navigate them to the correct content
                var arguments = $"as-music:///library/albums";

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
            if (IsPinned)
            {
                Core.Tools.Tile.UpdateImage(tileId, AlbumList.SelectMany(a => a.Where(c => c.ArtworkUri != null).Select(b => b.ArtworkUri.OriginalString)).Distinct().OrderBy(x => Guid.NewGuid()).Take(10).ToList(), Consts.Localizer.GetString("AlbumsText"), Consts.Localizer.GetString("AlbumsTile"));
            }
        }

        [ObservableProperty]
        private bool isPinned;
    }
}
