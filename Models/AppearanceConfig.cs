namespace DesktopTextBoard.Models;

public sealed class AppearanceConfig
{
    public string BackgroundColor { get; set; } = "#181A1F";
    public double BackgroundOpacity { get; set; } = 0.72;
    public string BorderColor { get; set; } = "#FFFFFF";
    public bool ShowFrame { get; set; } = true;
    public double BorderOpacity { get; set; } = 0.08;
    public double BorderThickness { get; set; } = 1;
    public double CornerRadius { get; set; } = 6;
    public double Padding { get; set; } = 16;
    public string DefaultTextColor { get; set; } = "#F2F2F2";
    public double DefaultFontSize { get; set; } = 16;

    public static AppearanceConfig DarkTranslucent()
    {
        return new AppearanceConfig();
    }

    public AppearanceConfig Clone()
    {
        return new AppearanceConfig
        {
            BackgroundColor = BackgroundColor,
            BackgroundOpacity = BackgroundOpacity,
            BorderColor = BorderColor,
            ShowFrame = ShowFrame,
            BorderOpacity = BorderOpacity,
            BorderThickness = BorderThickness,
            CornerRadius = CornerRadius,
            Padding = Padding,
            DefaultTextColor = DefaultTextColor,
            DefaultFontSize = DefaultFontSize
        };
    }
}
