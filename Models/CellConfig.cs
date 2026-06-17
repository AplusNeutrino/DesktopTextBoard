namespace DesktopTextBoard.Models;

public sealed class CellConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public int Row { get; set; }
    public int Column { get; set; }
    public int RowSpan { get; set; } = 1;
    public int ColumnSpan { get; set; } = 1;
    public string ContentFormat { get; set; } = "wpf-xaml-package-base64";
    public string Content { get; set; } = string.Empty;

    public CellConfig Clone()
    {
        return new CellConfig
        {
            Id = Guid.NewGuid().ToString("N"),
            Row = Row,
            Column = Column,
            RowSpan = RowSpan,
            ColumnSpan = ColumnSpan,
            ContentFormat = ContentFormat,
            Content = Content
        };
    }
}
