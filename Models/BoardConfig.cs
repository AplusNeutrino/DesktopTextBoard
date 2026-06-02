namespace DesktopTextBoard.Models;

public sealed class BoardConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Default";
    public List<WidgetConfig> Widgets { get; set; } = new();

    public static BoardConfig CreateDefault()
    {
        return new BoardConfig
        {
            Id = "board-default",
            Name = "Default",
            Widgets = new List<WidgetConfig>
            {
                WidgetConfig.CreateDefault()
            }
        };
    }
}
