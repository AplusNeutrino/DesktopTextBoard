using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace DesktopTextBoard.Services;

public static class WindowIconService
{
    public static void Apply(Window window)
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AkashaNotesTaskbar.png");
        if (!File.Exists(iconPath))
        {
            return;
        }

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(iconPath, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        window.Icon = image;
    }
}
