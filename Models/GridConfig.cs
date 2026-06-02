namespace DesktopTextBoard.Models;

public sealed class GridConfig
{
    public int Rows { get; set; } = 1;
    public int Columns { get; set; } = 1;
    public List<double> RowWeights { get; set; } = new() { 1.0 };
    public List<double> ColumnWeights { get; set; } = new() { 1.0 };

    public static GridConfig Default()
    {
        return new GridConfig();
    }

    public void Normalize()
    {
        Rows = Math.Clamp(Rows, 1, 12);
        Columns = Math.Clamp(Columns, 1, 12);
        NormalizeWeights(RowWeights, Rows);
        NormalizeWeights(ColumnWeights, Columns);
    }

    public GridConfig Clone()
    {
        return new GridConfig
        {
            Rows = Rows,
            Columns = Columns,
            RowWeights = RowWeights.ToList(),
            ColumnWeights = ColumnWeights.ToList()
        };
    }

    private static void NormalizeWeights(List<double> weights, int count)
    {
        while (weights.Count < count)
        {
            weights.Add(1.0);
        }

        while (weights.Count > count)
        {
            weights.RemoveAt(weights.Count - 1);
        }

        for (var i = 0; i < weights.Count; i++)
        {
            if (weights[i] <= 0 || double.IsNaN(weights[i]) || double.IsInfinity(weights[i]))
            {
                weights[i] = 1.0;
            }
        }
    }
}
