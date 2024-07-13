using Aurora.Music.Core.Models;
using Aurora.Shared.Extensions;
using Aurora.Shared.Helpers;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;

namespace Aurora.Music.Core.Storage
{
    public class LocalSettings
    {
        public static LocalSetting Current { get; set; } = new LocalSetting();
    }

    public class LocalSetting : INotifyPropertyChanged
    {
        public int LyricRendererFps
        {
            get => GetSettings(nameof(LyricRendererFps), 60);
            set
            {
                ApplicationData.Current.LocalSettings.GetContainer(Aurora.Music.Core.Models.Settings.SETTINGS_CONTAINER).Values[nameof(LyricRendererFps)] = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public async void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () => { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); });
        }

        public static T GetSettings<T>(string propertyName, T defaultValue)
        {
            try
            {
                var container = ApplicationData.Current.LocalSettings.GetContainer(Aurora.Music.Core.Models.Settings.SETTINGS_CONTAINER);
                if (container.Values.ContainsKey(propertyName) &&
                    container.Values[propertyName] != null &&
                    !string.IsNullOrEmpty(container.Values[propertyName].ToString()))
                {
                    if (typeof(T).ToString() == "System.Boolean")
                        return (T)(object)bool.Parse(container.Values[propertyName]
                            .ToString());

                    //超长的IF
                    return (T)container.Values[propertyName];
                }

                return defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }
    }
}
