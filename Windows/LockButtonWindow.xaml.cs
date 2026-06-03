using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DesktopTextBoard.Models;
using DesktopTextBoard.Services;

namespace DesktopTextBoard.Windows;

public partial class LockButtonWindow : Window
{
    private readonly WidgetConfig _widget;
    private readonly Action _toggle;
    private bool _forceClose;

    public LockButtonWindow(WidgetConfig widget, Action toggle)
    {
        InitializeComponent();
        _widget = widget;
        _toggle = toggle;
        SourceInitialized += (_, _) => NativeMethods.HideFromAltTab(this);
        Loaded += (_, _) => ApplyConfig();
    }

    public void ApplyConfig()
    {
        var path = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse(_widget.IsLocked
                ? "M 8 14 L 8 10 C 8 6.7 10.7 4 14 4 C 17.3 4 20 6.7 20 10 L 20 14 M 6 14 L 22 14 L 22 25 L 6 25 Z M 14 18 L 14 22"
                : "M 8 14 L 8 10 C 8 6.7 10.7 4 14 4 C 16.4 4 18.5 5.4 19.4 7.5 M 6 14 L 22 14 L 22 25 L 6 25 Z M 14 18 L 14 22"),
            Stroke = Brushes.White,
            StrokeThickness = 2,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Stretch = Stretch.Uniform
        };

        LockButton.Content = new Viewbox
        {
            Width = 18,
            Height = 18,
            Child = path
        };
        LockButton.ToolTip = _widget.IsLocked ? "解除锁定，允许移动和缩放" : "锁定小组件并启用点击穿透";
    }

    public void ForceClose()
    {
        _forceClose = true;
        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_forceClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    private void LockButton_Click(object sender, RoutedEventArgs e)
    {
        _toggle();
    }
}
