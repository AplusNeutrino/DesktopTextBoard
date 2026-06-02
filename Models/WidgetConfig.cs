namespace DesktopTextBoard.Models;

public sealed class WidgetConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Desktop Board";
    public WidgetMode Mode { get; set; } = WidgetMode.Grid;
    public bool IsLocked { get; set; } = true;
    public string MonitorDeviceName { get; set; } = "primary";
    public BoundsConfig Bounds { get; set; } = new();
    public AppearanceConfig Appearance { get; set; } = AppearanceConfig.DarkTranslucent();
    public GridConfig Grid { get; set; } = GridConfig.Default();
    public List<CellConfig> Cells { get; set; } = new();

    public static WidgetConfig CreateDefault()
    {
        var widget = new WidgetConfig
        {
            Id = "widget-default",
            Name = "Desktop Board",
            Mode = WidgetMode.Grid,
            IsLocked = true,
            Bounds = new BoundsConfig
            {
                X = 1245,
                Y = 8,
                Width = 425,
                Height = 680
            },
            Grid = new GridConfig
            {
                Rows = 2,
                Columns = 1,
                RowWeights = new List<double> { 0.55, 0.45 },
                ColumnWeights = new List<double> { 1.0 }
            }
        };
        widget.EnsureCells();
        return widget;
    }

    public void EnsureCells()
    {
        Grid.Normalize();
        var required = new HashSet<string>();

        for (var row = 0; row < Grid.Rows; row++)
        {
            for (var column = 0; column < Grid.Columns; column++)
            {
                required.Add(CellKey(row, column));
                if (Cells.Any(x => x.Row == row && x.Column == column))
                {
                    continue;
                }

                Cells.Add(new CellConfig
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Row = row,
                    Column = column
                });
            }
        }

        Cells.RemoveAll(x => !required.Contains(CellKey(x.Row, x.Column)));
        Cells.Sort((a, b) =>
        {
            var rowCompare = a.Row.CompareTo(b.Row);
            return rowCompare != 0 ? rowCompare : a.Column.CompareTo(b.Column);
        });
    }

    public CellConfig GetSingleCell()
    {
        if (Cells.Count == 0)
        {
            Cells.Add(new CellConfig { Row = 0, Column = 0 });
        }

        return Cells[0];
    }

    public WidgetConfig Clone()
    {
        return new WidgetConfig
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = $"{Name} Copy",
            Mode = Mode,
            IsLocked = IsLocked,
            MonitorDeviceName = MonitorDeviceName,
            Bounds = Bounds.CloneOffset(24, 24),
            Appearance = Appearance.Clone(),
            Grid = Grid.Clone(),
            Cells = Cells.Select(x => x.Clone()).ToList()
        };
    }

    private static string CellKey(int row, int column) => $"{row}:{column}";
}

public enum WidgetMode
{
    Single,
    Grid
}
