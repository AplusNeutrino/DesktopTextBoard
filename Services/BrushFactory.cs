using System.Globalization;
using System.Windows.Media;

namespace DesktopTextBoard.Services;

public static class BrushFactory
{
    public static SolidColorBrush Solid(string value, double opacity = 1)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(value);
            return new SolidColorBrush(color)
            {
                Opacity = Math.Clamp(opacity, 0, 1)
            };
        }
        catch
        {
            return new SolidColorBrush(Colors.Transparent);
        }
    }

    public static string NormalizeHex(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var text = value.Trim();
        if (!text.StartsWith("#", StringComparison.Ordinal))
        {
            text = $"#{text}";
        }

        if (text.Length is not (7 or 9))
        {
            return fallback;
        }

        var hex = text[1..];
        return hex.All(Uri.IsHexDigit) ? text.ToUpper(CultureInfo.InvariantCulture) : fallback;
    }
}
