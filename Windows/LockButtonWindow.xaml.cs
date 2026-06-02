using System.Windows;
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
        LockButton.Content = _widget.IsLocked ? "LOCK" : "EDIT";
        LockButton.ToolTip = _widget.IsLocked ? "Unlock widget movement" : "Lock widget interaction";
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
