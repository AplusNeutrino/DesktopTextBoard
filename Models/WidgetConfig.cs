namespace DesktopTextBoard.Models;

public sealed class WidgetConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "阿卡夏便笺";
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
            Name = "阿卡夏便笺",
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
        var covered = new bool[Grid.Rows, Grid.Columns];
        var normalized = new List<CellConfig>();

        foreach (var cell in Cells
                     .OrderBy(x => x.Row)
                     .ThenBy(x => x.Column)
                     .ToList())
        {
            NormalizeCellBounds(cell);
            if (covered[cell.Row, cell.Column])
            {
                continue;
            }

            if (SpanIntersectsCovered(covered, cell))
            {
                cell.RowSpan = 1;
                cell.ColumnSpan = 1;
            }

            MarkCovered(covered, cell);
            normalized.Add(cell);
        }

        for (var row = 0; row < Grid.Rows; row++)
        {
            for (var column = 0; column < Grid.Columns; column++)
            {
                if (covered[row, column])
                {
                    continue;
                }

                var cell = new CellConfig
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Row = row,
                    Column = column
                };
                normalized.Add(cell);
                covered[row, column] = true;
            }
        }

        Cells = normalized;
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

    private void NormalizeCellBounds(CellConfig cell)
    {
        cell.Row = Math.Clamp(cell.Row, 0, Grid.Rows - 1);
        cell.Column = Math.Clamp(cell.Column, 0, Grid.Columns - 1);
        cell.RowSpan = Math.Clamp(cell.RowSpan <= 0 ? 1 : cell.RowSpan, 1, Grid.Rows - cell.Row);
        cell.ColumnSpan = Math.Clamp(cell.ColumnSpan <= 0 ? 1 : cell.ColumnSpan, 1, Grid.Columns - cell.Column);
    }

    private static bool SpanIntersectsCovered(bool[,] covered, CellConfig cell)
    {
        for (var row = cell.Row; row < cell.Row + cell.RowSpan; row++)
        {
            for (var column = cell.Column; column < cell.Column + cell.ColumnSpan; column++)
            {
                if (covered[row, column])
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void MarkCovered(bool[,] covered, CellConfig cell)
    {
        for (var row = cell.Row; row < cell.Row + cell.RowSpan; row++)
        {
            for (var column = cell.Column; column < cell.Column + cell.ColumnSpan; column++)
            {
                covered[row, column] = true;
            }
        }
    }
}

public enum WidgetMode
{
    Single,
    Grid
}
