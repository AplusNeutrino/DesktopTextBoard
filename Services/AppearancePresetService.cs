using DesktopTextBoard.Models;

namespace DesktopTextBoard.Services;

public static class AppearancePresetService
{
    public static IReadOnlyList<string> Names { get; } = new[]
    {
        "Dark translucent",
        "Light translucent",
        "Paper light",
        "Minimal borderless",
        "High contrast"
    };

    public static AppearanceConfig Create(string name)
    {
        return name switch
        {
            "Light translucent" => new AppearanceConfig
            {
                BackgroundColor = "#FFFFFF",
                BackgroundOpacity = 0.58,
                BorderColor = "#111111",
                BorderOpacity = 0.18,
                BorderThickness = 1,
                CornerRadius = 6,
                Padding = 16,
                DefaultTextColor = "#111111",
                DefaultFontSize = 16
            },
            "Paper light" => new AppearanceConfig
            {
                BackgroundColor = "#FFF8E6",
                BackgroundOpacity = 0.82,
                BorderColor = "#C7AE72",
                BorderOpacity = 0.35,
                BorderThickness = 1,
                CornerRadius = 4,
                Padding = 18,
                DefaultTextColor = "#241E16",
                DefaultFontSize = 16
            },
            "Minimal borderless" => new AppearanceConfig
            {
                BackgroundColor = "#000000",
                BackgroundOpacity = 0.18,
                BorderColor = "#FFFFFF",
                BorderOpacity = 0,
                BorderThickness = 0,
                CornerRadius = 0,
                Padding = 14,
                DefaultTextColor = "#F6F6F6",
                DefaultFontSize = 16
            },
            "High contrast" => new AppearanceConfig
            {
                BackgroundColor = "#000000",
                BackgroundOpacity = 0.86,
                BorderColor = "#FFFFFF",
                BorderOpacity = 0.64,
                BorderThickness = 1.5,
                CornerRadius = 4,
                Padding = 16,
                DefaultTextColor = "#FFFFFF",
                DefaultFontSize = 17
            },
            _ => AppearanceConfig.DarkTranslucent()
        };
    }

    public static void Apply(WidgetConfig widget, string name)
    {
        var preset = Create(name);
        preset.DefaultFontSize = widget.Appearance.DefaultFontSize;
        widget.Appearance = preset;
    }
}
