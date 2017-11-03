﻿using Aurora.Shared.Helpers;
using System;
using Windows.Storage;

namespace Aurora.Music.Core
{
    public static class Consts
    {
        public static StorageFolder ArtworkFolder = AsyncHelper.RunSync(async () =>
        {
            return await ApplicationData.Current.LocalFolder.CreateFolderAsync("Artworks", CreationCollisionOption.OpenIfExists);
        });

        public const string ID = "ID";

        public const string BlackPlaceholder = "ms-appx:///Assets/Images/placeholder_b.png";
        public const string WhitePlaceholder = "ms-appx:///Assets/Images/placeholder.png";

        public const string Duration = "Duration";

        public const string Artwork = "Artwork";

        public const string ArtistPageInAnimation = "ARTIST_PAGE_IN";
        public const string AlbumDetailPageInAnimation = "ALBUM_DETAIL_IN";
    }
}