namespace DesktopTextBoard.Models;

public sealed class BoundsConfig
{
    public double X { get; set; } = 100;
    public double Y { get; set; } = 100;
    public double Width { get; set; } = 420;
    public double Height { get; set; } = 320;

    public BoundsConfig CloneOffset(double dx, double dy)
    {
        return new BoundsConfig
        {
            X = X + dx,
            Y = Y + dy,
            Width = Width,
            Height = Height
        };
    }
}
