// Copyright (c) Aurora Studio. All rights reserved.
//
// Licensed under the MIT License. See LICENSE in the project root for license information.
using Aurora.Music.Core;
using Aurora.Shared.MVVM;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppExtensions;
using Windows.Foundation.Collections;
using Windows.System;
using Windows.UI.Xaml.Media.Imaging;

namespace Aurora.Music.ViewModels
{
    partial class ExtensionViewModel : ViewModelBase
    {
        public AppExtension AppExtension { get; private set; }

        [ObservableProperty]
        private string uniqueId;

        [ObservableProperty]
        private bool isCurrent;

        [ObservableProperty]
        private bool canLaunch;

        [ObservableProperty]
        private bool canUninstall = true;

        [ObservableProperty]
        private bool available;

        private BitmapImage logo;
        public BitmapImage Logo
        {
            get => logo;
            set => SetProperty(ref logo, value);
        }

        private string name;
        public string Name
        {
            get => Available ? name : Consts.Localizer.GetString("NotAvaliableText");//typo here
            set => SetProperty(ref name, value);
        }

        [ObservableProperty]
        private string service;

        private string _launchUri;

        [ObservableProperty]
        private string description;

        [ObservableProperty]
        private ExtType type;

        public override string ToString()
        {
            return $"{Name} - {Description}";
        }

        public bool LyricEnabled(ExtType type)
        {
            return type.HasFlag(ExtType.Lyric);
        }
        public bool MusicEnabled(ExtType type)
        {
            return type.HasFlag(ExtType.OnlineMusic);
        }
        public bool MetaEnabled(ExtType type)
        {
            return type.HasFlag(ExtType.OnlineMetaData);
        }

        public DelegateCommand LaunchUri =>
            new DelegateCommand(async () =>
            {
                await Launcher.LaunchUriAsync(new Uri(_launchUri));
            });

        public ExtensionViewModel(AppExtension ext, PropertySet properties)
        {
            UniqueId = ext.AppInfo.PackageFamilyName + Consts.ArraySeparator + ext.Id;
            if (ext.AppInfo.PackageFamilyName == Consts.PackageFamilyName)
            {
                CanUninstall = false;
            }

            AppExtension = ext;
            Available = ext.Package.Status.VerifyIsOK();

            var cates = ((properties["Category"] as PropertySet)["#text"] as string).Split(';');
            if (cates != null && cates.Length > 0)
            {
                foreach (var item in cates)
                {
                    switch (item)
                    {
                        case "Lyric":
                            Type |= ExtType.Lyric; break;
                        case "OnlineMusic":
                            Type |= ExtType.OnlineMusic; break;
                        case "OnlineMeta":
                            Type |= ExtType.OnlineMetaData; break;
                        default:
                            break;
                    }
                }
            }
            else
            {
                Type = ExtType.NotSpecific;
            }

            Name = ext.DisplayName;
            Description = ext.Description;
            Service = (properties["Service"] as PropertySet)["#text"] as string;

            if (properties.TryGetValue("LaunchUri", out object uri))
            {
                if (uri is PropertySet p && p["#text"] is string s)
                {
                    if (Uri.TryCreate(s, UriKind.Absolute, out var u))
                    {
                        CanLaunch = true;
                        _launchUri = s;
                    }
                    else
                    {
                        CanLaunch = false;
                    }
                }
                else
                {
                    CanLaunch = false;
                }

            }
            else
            {
                CanLaunch = false;
            }
        }

        internal async Task Load()
        {
            // get logo 
            var filestream = await (AppExtension.AppInfo.DisplayInfo.GetLogo(new Windows.Foundation.Size(1, 1))).OpenReadAsync();
            Logo = new BitmapImage();
            logo.DecodePixelHeight = 48;
            logo.DecodePixelType = DecodePixelType.Logical;
            logo.SetSource(filestream);
        }

        internal void Unload()
        {
            Available = false;
        }

        internal async Task Update(AppExtension ext)
        {

            var properties = await ext.GetExtensionPropertiesAsync() as PropertySet;
            UniqueId = ext.AppInfo.PackageFamilyName + Consts.ArraySeparator + ext.Id;


            if (ext.AppInfo.PackageFamilyName == Consts.PackageFamilyName)
            {
                CanUninstall = false;
            }

            AppExtension = ext;
            Available = ext.Package.Status.VerifyIsOK();

            var cates = ((properties["Category"] as PropertySet)["#text"] as string).Split(';');
            if (cates != null && cates.Length > 0)
            {
                foreach (var item in cates)
                {
                    switch (item)
                    {
                        case "Lyric":
                            Type |= ExtType.Lyric; break;
                        case "OnlineMusic":
                            Type |= ExtType.OnlineMusic; break;
                        case "OnlineMeta":
                            Type |= ExtType.OnlineMetaData; break;
                        default:
                            break;
                    }
                }
            }
            else
            {
                Type = ExtType.NotSpecific;
            }

            Name = ext.DisplayName;
            Description = ext.Description;
            Service = (properties["Service"] as PropertySet)["#text"] as string;

            _launchUri = (properties["LaunchUri"] as PropertySet)["#text"] as string;
            if (string.IsNullOrEmpty(_launchUri))
            {
                CanLaunch = false;
            }
            else
            {
                if (Uri.TryCreate(_launchUri, UriKind.Absolute, out var u))
                {
                    CanLaunch = true;
                }
                else
                {
                    CanLaunch = false;
                }
            }
        }
    }

    [Flags]
    public enum ExtType { NotSpecific = 1, Lyric = 2, OnlineMusic = 4, OnlineMetaData = 8 }
}
