// GPL-2.0-only. Shared typography and WPF UI controls for both windows.
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace FarmMotion {
    static class UiTheme {
        static Brush Resource(string key) { return (Brush)Application.Current.FindResource(key); }
        internal static Brush Background { get { return Resource("ApplicationBackgroundBrush"); } }
        internal static Brush Text { get { return Resource("TextFillColorPrimaryBrush"); } }
        internal static Brush Muted { get { return Resource("TextFillColorSecondaryBrush"); } }
        internal static Brush Line { get { return Resource("ControlStrokeColorDefaultBrush"); } }
        internal static Brush Accent { get { return Resource("TextFillColorPrimaryBrush"); } }
        internal static void Button(Wpf.Ui.Controls.Button button,bool primary) {
            button.Appearance=primary?Wpf.Ui.Controls.ControlAppearance.Primary:Wpf.Ui.Controls.ControlAppearance.Secondary;
            button.MinHeight=36;
        }
        internal static Wpf.Ui.Controls.Card Card(UIElement content) {
            return new Wpf.Ui.Controls.Card { VerticalAlignment=VerticalAlignment.Top,Content=content,Padding=new Thickness(16),Margin=new Thickness(0,0,0,12),HorizontalContentAlignment=HorizontalAlignment.Stretch };
        }
        internal static BitmapImage Logo() {
            using(var stream=typeof(UiTheme).Assembly.GetManifestResourceStream("FarmMotion.Logo.png")) {
                var image=new BitmapImage(); image.BeginInit(); image.CacheOption=BitmapCacheOption.OnLoad; image.StreamSource=stream; image.EndInit(); image.Freeze(); return image;
            }
        }
    }
}
