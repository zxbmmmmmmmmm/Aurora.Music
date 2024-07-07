// Copyright (c) Aurora Studio. All rights reserved.
//
// Licensed under the MIT License. See LICENSE in the project root for license information.
using Aurora.Music.Core.Models;
using Aurora.Shared.MVVM;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Aurora.Music.ViewModels
{
    [ObservableObject]
    partial class ChannelGroup : List<ChannelViewModel>, IGrouping<string, ChannelViewModel>
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Key))]
        private string name;

        public int ID { get; set; }

        public string Key => Name;

        public ChannelGroup()
        {
        }
    }

    partial class ChannelViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string name;

        [ObservableProperty]
        private string description;


        public int ID { get; set; }

        [ObservableProperty]
        private Uri cover;

        public bool NightModeEnabled { get; set; } = Settings.Current.NightMode;
    }
}
