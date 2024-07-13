using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Shapes;

//https://go.microsoft.com/fwlink/?LinkId=234236 上介绍了“用户控件”项模板

namespace Isolation.Uwp
{
    public sealed partial class IsolationControl : UserControl
    {
        private PixelShaderEffect effect;
        
        public void SetColor(Vector3 color1, Vector3 color2, Vector3 color3, Vector3 color4)
        {
            effect.Properties["color1"] = color1;
            effect.Properties["color2"] = color2;
            effect.Properties["color3"] = color3;
            effect.Properties["color4"] = color4;
        }


        public List<Vector3> Colors
        {
            get => (List<Vector3>)GetValue(ColorsProperty);
            set => SetValue(ColorsProperty, value);
        }

        public static readonly DependencyProperty ColorsProperty =
            DependencyProperty.Register(nameof(Colors), typeof(List<Vector3>), typeof(IsolationControl), new PropertyMetadata(default, ColorsChanged));

        public bool IsPlaying
        {
            get => (bool)GetValue(IsPlayingProperty);
            set => SetValue(IsPlayingProperty, value);
        }

        public static readonly DependencyProperty IsPlayingProperty =
            DependencyProperty.Register(nameof(IsPlaying), typeof(bool), typeof(IsolationControl), new PropertyMetadata(true, IsPlayingChanged));



        public int Fps
        {
            get => (int)GetValue(FpsProperty);
            set => SetValue(FpsProperty, value);
        }

        public static readonly DependencyProperty FpsProperty =
            DependencyProperty.Register(nameof(Fps), typeof(int), typeof(IsolationControl), new PropertyMetadata(60,FpsChanged));

        private static void FpsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (IsolationControl)d;
            control.MainCanvas.TargetElapsedTime = TimeSpan.FromMilliseconds(16.6 * (60d / (int)e.NewValue));
        }

        private static void IsPlayingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (IsolationControl)d;
            control.MainCanvas.Paused = !(bool)e.NewValue;         
        }

        private static void ColorsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (IsolationControl)d;
            if (control.effect is null) return;
            control.effect.Properties["color1"] = control.Colors?[0];
            control.effect.Properties["color2"] = control.Colors?[1];
            control.effect.Properties["color3"] = control.Colors?[2];
            control.effect.Properties["color4"] = control.Colors?[3];
        }

        public IsolationControl()
        {
            this.InitializeComponent();
            this.Loaded += OnLoaded;
            this.Unloaded += OnUnloaded;
        }


        private void OnUnloaded(object sender, RoutedEventArgs e)
        {

            MainCanvas.CreateResources -= MainCanvas_CreateResources;
            MainCanvas.Update -= MainCanvas_Update;
            MainCanvas.Draw -= MainCanvas_Draw;
            MainCanvas.SizeChanged -= MainCanvas_SizeChanged;
            this.Loaded -= OnLoaded;
            this.Unloaded -= OnUnloaded;
            MainCanvas.RemoveFromVisualTree();
            MainCanvas = null;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            MainCanvas.CreateResources += MainCanvas_CreateResources;
            MainCanvas.Update += MainCanvas_Update;
            MainCanvas.Draw += MainCanvas_Draw;
            MainCanvas.SizeChanged += MainCanvas_SizeChanged;
        }


        private void MainCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (effect is null) return;
            effect.Properties["Width"] = (float)MainCanvas.ActualWidth;
            effect.Properties["Height"] = (float)MainCanvas.ActualHeight;
        }

        private void MainCanvas_Draw(Microsoft.Graphics.Canvas.UI.Xaml.ICanvasAnimatedControl sender, Microsoft.Graphics.Canvas.UI.Xaml.CanvasAnimatedDrawEventArgs args)
        {
            if (effect is null)
                return;
            args.DrawingSession.DrawImage(effect);
        }

        private void MainCanvas_Update(Microsoft.Graphics.Canvas.UI.Xaml.ICanvasAnimatedControl sender, Microsoft.Graphics.Canvas.UI.Xaml.CanvasAnimatedUpdateEventArgs args)
        {
            if (effect is null)
                return;
            effect.Properties["iTime"] = Convert.ToSingle(args.Timing.TotalTime.TotalSeconds);
        }

        private async void MainCanvas_CreateResources(Microsoft.Graphics.Canvas.UI.Xaml.CanvasAnimatedControl sender, Microsoft.Graphics.Canvas.UI.CanvasCreateResourcesEventArgs args)
        {
            var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Shaders/effect.bin"));
            var buffer = await FileIO.ReadBufferAsync(file);
            var bytes = buffer.ToArray();
            effect = new PixelShaderEffect(bytes);
            effect.Properties["Width"] = (float)MainCanvas.ActualWidth;
            effect.Properties["Height"] = (float)MainCanvas.ActualHeight;
        }
    }
}
