using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using DesktopTextBoard.Models;
using DesktopTextBoard.Services;
using WpfRichTextBox = System.Windows.Controls.RichTextBox;

namespace DesktopTextBoard.Windows;

public partial class DesktopWidgetWindow : Window
{
    private readonly WidgetConfig _widget;
    private readonly Action _changed;
    private readonly LockButtonWindow _lockButtonWindow;
    private bool _forceClose;

    public DesktopWidgetWindow(WidgetConfig widget, Action changed)
    {
        InitializeComponent();
        _widget = widget;
        _changed = changed;
        _lockButtonWindow = new LockButtonWindow(widget, () =>
        {
            _widget.IsLocked = !_widget.IsLocked;
            ApplyConfig();
            _changed();
        });

        SourceInitialized += (_, _) =>
        {
            NativeMethods.HideFromAltTab(this);
            ApplyConfig();
        };
        Loaded += (_, _) =>
        {
            _lockButtonWindow.Owner = this;
            _lockButtonWindow.Show();
            UpdateLockButtonPlacement();
            ApplyConfig();
        };
        LocationChanged += (_, _) =>
        {
            SaveBounds();
            UpdateLockButtonPlacement();
        };
        SizeChanged += (_, _) =>
        {
            SaveBounds();
            UpdateLockButtonPlacement();
        };
        Closed += (_, _) => _lockButtonWindow.ForceClose();
    }

    public void ApplyConfig()
    {
        MonitorService.KeepVisible(_widget);
        Left = _widget.Bounds.X;
        Top = _widget.Bounds.Y;
        Width = _widget.Bounds.Width;
        Height = _widget.Bounds.Height;

        var appearance = _widget.Appearance;
        ShellBorder.Background = BrushFactory.Solid(appearance.BackgroundColor, appearance.BackgroundOpacity);
        ShellBorder.BorderBrush = BrushFactory.Solid(appearance.BorderColor, appearance.BorderOpacity);
        ShellBorder.BorderThickness = new Thickness(appearance.ShowFrame ? appearance.BorderThickness : 0);
        ShellBorder.CornerRadius = new CornerRadius(appearance.CornerRadius);
        ShellBorder.Padding = new Thickness(appearance.Padding);
        ShellBorder.ClipToBounds = true;

        BuildContent();
        SetUnlockedChrome(!_widget.IsLocked);
        NativeMethods.SetClickThrough(this, _widget.IsLocked);
        _lockButtonWindow.ApplyConfig();
        UpdateLockButtonPlacement();
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

    private void BuildContent()
    {
        ContentGrid.Children.Clear();
        ContentGrid.RowDefinitions.Clear();
        ContentGrid.ColumnDefinitions.Clear();

        if (_widget.Mode == WidgetMode.Single)
        {
            ContentGrid.Children.Add(CreateDisplayBox(_widget.GetSingleCell()));
            return;
        }

        _widget.EnsureCells();
        for (var row = 0; row < _widget.Grid.Rows; row++)
        {
            ContentGrid.RowDefinitions.Add(new RowDefinition
            {
                Height = new GridLength(_widget.Grid.RowWeights[row], GridUnitType.Star)
            });
        }

        for (var column = 0; column < _widget.Grid.Columns; column++)
        {
            ContentGrid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(_widget.Grid.ColumnWeights[column], GridUnitType.Star)
            });
        }

        foreach (var cell in _widget.Cells)
        {
            var cellBorder = new Border
            {
                BorderBrush = BrushFactory.Solid(_widget.Appearance.BorderColor, Math.Min(0.18, _widget.Appearance.BorderOpacity + 0.06)),
                BorderThickness = new Thickness(_widget.Appearance.ShowFrame ? 0.5 : 0),
                Padding = new Thickness(6),
                ClipToBounds = true,
                Child = CreateDisplayBox(cell)
            };

            Grid.SetRow(cellBorder, cell.Row);
            Grid.SetColumn(cellBorder, cell.Column);
            Grid.SetRowSpan(cellBorder, cell.RowSpan);
            Grid.SetColumnSpan(cellBorder, cell.ColumnSpan);
            ContentGrid.Children.Add(cellBorder);
        }
    }

    private WpfRichTextBox CreateDisplayBox(CellConfig cell)
    {
        var foreground = BrushFactory.Solid(_widget.Appearance.DefaultTextColor);
        var displayBox = new WpfRichTextBox
        {
            Document = RichTextSerializer.Load(cell.Content, foreground, _widget.Appearance.DefaultFontSize),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            IsReadOnly = true,
            IsHitTestVisible = false,
            Focusable = false,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden
        };
        displayBox.Loaded += (_, _) => FitDisplayDividers(displayBox);
        displayBox.SizeChanged += (_, _) => FitDisplayDividers(displayBox);
        return displayBox;
    }

    private static void FitDisplayDividers(WpfRichTextBox displayBox)
    {
        if (displayBox.ActualWidth <= 0)
        {
            return;
        }

        var availableWidth = displayBox.ActualWidth
            - displayBox.Padding.Left
            - displayBox.Padding.Right
            - 12;
        RichTextSerializer.FitDividersToWidth(displayBox.Document, availableWidth);
    }

    private void SetUnlockedChrome(bool visible)
    {
        var visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        UnlockedOutline.Visibility = visibility;
        TopLeftHandle.Visibility = visibility;
        TopRightHandle.Visibility = visibility;
        BottomLeftHandle.Visibility = visibility;
        BottomRightHandle.Visibility = visibility;

        foreach (var thumb in new[] { TopLeftHandle, TopRightHandle, BottomLeftHandle, BottomRightHandle })
        {
            thumb.Background = visible ? Brushes.White : Brushes.Transparent;
            thumb.Opacity = 0.72;
        }
    }

    private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_widget.IsLocked || e.OriginalSource is Thumb)
        {
            return;
        }

        try
        {
            DragMove();
            SaveBounds();
            _changed();
        }
        catch
        {
            // DragMove can throw if WPF loses the mouse capture during a quick click.
        }
    }

    private void TopLeftHandle_DragDelta(object sender, DragDeltaEventArgs e)
    {
        ResizeFrom(leftDelta: e.HorizontalChange, topDelta: e.VerticalChange, rightDelta: 0, bottomDelta: 0);
    }

    private void TopRightHandle_DragDelta(object sender, DragDeltaEventArgs e)
    {
        ResizeFrom(leftDelta: 0, topDelta: e.VerticalChange, rightDelta: e.HorizontalChange, bottomDelta: 0);
    }

    private void BottomLeftHandle_DragDelta(object sender, DragDeltaEventArgs e)
    {
        ResizeFrom(leftDelta: e.HorizontalChange, topDelta: 0, rightDelta: 0, bottomDelta: e.VerticalChange);
    }

    private void BottomRightHandle_DragDelta(object sender, DragDeltaEventArgs e)
    {
        ResizeFrom(leftDelta: 0, topDelta: 0, rightDelta: e.HorizontalChange, bottomDelta: e.VerticalChange);
    }

    private void ResizeFrom(double leftDelta, double topDelta, double rightDelta, double bottomDelta)
    {
        if (_widget.IsLocked)
        {
            return;
        }

        var newLeft = Left + leftDelta;
        var newTop = Top + topDelta;
        var newWidth = Width - leftDelta + rightDelta;
        var newHeight = Height - topDelta + bottomDelta;

        if (newWidth < MinWidth)
        {
            newLeft -= MinWidth - newWidth;
            newWidth = MinWidth;
        }

        if (newHeight < MinHeight)
        {
            newTop -= MinHeight - newHeight;
            newHeight = MinHeight;
        }

        Left = newLeft;
        Top = newTop;
        Width = newWidth;
        Height = newHeight;
        SaveBounds();
        _changed();
    }

    private void SaveBounds()
    {
        if (!IsLoaded)
        {
            return;
        }

        _widget.Bounds.X = Left;
        _widget.Bounds.Y = Top;
        _widget.Bounds.Width = Width;
        _widget.Bounds.Height = Height;
    }

    private void UpdateLockButtonPlacement()
    {
        _lockButtonWindow.Left = Left + Width - 42;
        _lockButtonWindow.Top = Top + 6;
    }
}
