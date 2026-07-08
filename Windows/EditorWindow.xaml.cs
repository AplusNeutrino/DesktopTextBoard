using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopTextBoard.Models;
using DesktopTextBoard.Services;
using WpfRichTextBox = System.Windows.Controls.RichTextBox;

namespace DesktopTextBoard.Windows;

public partial class EditorWindow : Window
{
    private static readonly double[] FontSizeOptions = { 10, 12, 14, 16, 18, 20, 24, 28, 32, 36, 48, 64 };
    private const double WidgetMinWidth = 160;
    private const double WidgetMinHeight = 120;
    private const double PreviewMaxWidth = 900;
    private const double PreviewMaxHeight = 650;
    private const double CellMinWidth = 52;
    private const double CellMinHeight = 42;

    private readonly BoardDocument _document;
    private readonly BoardStore _boardStore;
    private readonly DesktopWidgetManager _widgetManager;
    private readonly List<MonitorInfo> _monitors;
    private readonly DispatcherTimer _appearanceUpdateTimer;
    private readonly List<UIElement> _resizeOverlays = new();
    private WidgetConfig? _selectedWidget;
    private CellConfig? _selectedCell;
    private CellRange? _selectedRange;
    private CellRange? _selectionAnchorRange;
    private WpfRichTextBox? _activeEditor;
    private bool _isLoading;
    private bool _isApplyingSelectionFormat;
    private bool _isSavingEditorContent;
    private bool _isMutatingEditorContent;
    private bool _isUpdatingToolbarState;
    private bool _forceClose;

    private enum ResizeEdge
    {
        Left,
        Right,
        Top,
        Bottom
    }

    private sealed record CellRange(int Row, int Column, int RowSpan, int ColumnSpan)
    {
        public int LastRow => Row + RowSpan - 1;
        public int LastColumn => Column + ColumnSpan - 1;

        public bool Contains(CellRange other)
        {
            return other.Row >= Row
                && other.Column >= Column
                && other.LastRow <= LastRow
                && other.LastColumn <= LastColumn;
        }

        public bool Intersects(CellRange other)
        {
            return Row <= other.LastRow
                && LastRow >= other.Row
                && Column <= other.LastColumn
                && LastColumn >= other.Column;
        }
    }

    private sealed record CellResizeTarget(CellRange Range, ResizeEdge Edge);

    public EditorWindow(BoardDocument document, BoardStore boardStore, DesktopWidgetManager widgetManager)
    {
        InitializeComponent();
        WindowIconService.Apply(this);
        _document = document;
        _boardStore = boardStore;
        _widgetManager = widgetManager;
        _monitors = MonitorService.GetMonitors().ToList();
        _appearanceUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(180)
        };
        _appearanceUpdateTimer.Tick += (_, _) =>
        {
            _appearanceUpdateTimer.Stop();
            FlushAppearanceUpdate();
        };
        SourceInitialized += (_, _) => NativeMethods.UseImmersiveDarkMode(this);

        InitializeToolbar();
        InitializeSettingsLists();
        LoadWidgetList();
        if (_document.ActiveBoard.Widgets.Count > 0)
        {
            WidgetList.SelectedIndex = 0;
        }
        SetStatus("已就绪");
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
            _boardStore.SaveNow(_document);
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    private void InitializeToolbar()
    {
        FontSizeBox.ItemsSource = FontSizeOptions;
        SetFontSizeControlValue(16);

        TextColorBox.ItemsSource = new[]
        {
            "Default", "White", "Black", "Gray", "Red", "Orange", "Yellow", "Green", "Cyan", "Blue", "Purple", "混合"
        };
        SetComboBoxValue(TextColorBox, "Default");

        HighlightColorBox.ItemsSource = new[]
        {
            "None", "Yellow", "Green", "Cyan", "Pink", "Orange", "混合"
        };
        SetComboBoxValue(HighlightColorBox, "None");
    }

    private void InitializeSettingsLists()
    {
        ModeCombo.ItemsSource = new[] { "Single", "Grid" };
        PresetCombo.ItemsSource = AppearancePresetService.Names;
        MonitorCombo.ItemsSource = _monitors;
    }

    private void LoadWidgetList()
    {
        WidgetList.ItemsSource = null;
        WidgetList.ItemsSource = _document.ActiveBoard.Widgets;
    }

    private void WidgetList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectWidget(WidgetList.SelectedItem as WidgetConfig);
    }

    private void SelectWidget(WidgetConfig? widget)
    {
        _selectedWidget = widget;
        _selectedCell = null;
        _selectedRange = null;
        _selectionAnchorRange = null;
        _activeEditor = null;
        _resizeOverlays.Clear();
        CanvasHost.Children.Clear();
        CanvasHost.RowDefinitions.Clear();
        CanvasHost.ColumnDefinitions.Clear();

        if (widget is null)
        {
            SetStatus("未选择小组件");
            return;
        }

        _isLoading = true;
        NameBox.Text = widget.Name;
        ModeCombo.SelectedItem = widget.Mode == WidgetMode.Single ? "Single" : "Grid";
        LoadGridControls(widget);
        LockCheck.IsChecked = widget.IsLocked;
        ShowFrameCheck.IsChecked = widget.Appearance.ShowFrame;
        MonitorCombo.SelectedItem = _monitors.FirstOrDefault(x => x.DeviceName == widget.MonitorDeviceName)
                                    ?? _monitors.FirstOrDefault(x => x.IsPrimary);
        LoadAppearanceControls(widget);
        SetFontSizeControlValue(widget.Appearance.DefaultFontSize);
        _isLoading = false;

        BuildEditorCanvas(widget);
        SetStatus($"正在编辑：{widget.Name}");
    }

    private void LoadGridControls(WidgetConfig widget)
    {
        widget.Grid.Normalize();
        RowsBox.Text = widget.Grid.Rows.ToString(CultureInfo.CurrentCulture);
        ColumnsBox.Text = widget.Grid.Columns.ToString(CultureInfo.CurrentCulture);
        RowWeightsBox.Text = GridWeightService.Format(widget.Grid.RowWeights);
        ColumnWeightsBox.Text = GridWeightService.Format(widget.Grid.ColumnWeights);
    }

    private void LoadAppearanceControls(WidgetConfig widget)
    {
        var appearance = widget.Appearance;
        BackgroundColorBox.Text = appearance.BackgroundColor;
        BackgroundOpacitySlider.Value = appearance.BackgroundOpacity;
        BorderColorBox.Text = appearance.BorderColor;
        BorderOpacitySlider.Value = appearance.BorderOpacity;
        BorderThicknessSlider.Value = appearance.BorderThickness;
        CornerRadiusSlider.Value = appearance.CornerRadius;
        PaddingSlider.Value = appearance.Padding;
        DefaultTextColorBox.Text = appearance.DefaultTextColor;
        DefaultFontSizeSlider.Value = appearance.DefaultFontSize;
    }

    private void BuildEditorCanvas(WidgetConfig widget)
    {
        _isLoading = true;
        widget.EnsureCells();
        _selectedCell = ResolveSelectedCell(widget);
        _selectedRange = ResolveSelectedRange(widget);
        _selectionAnchorRange = ResolveSelectionAnchorRange(widget);
        _resizeOverlays.Clear();
        CanvasHost.Children.Clear();
        CanvasHost.RowDefinitions.Clear();
        CanvasHost.ColumnDefinitions.Clear();

        ApplyEditorPreviewBounds(widget);
        ApplyEditorPreviewAppearance(widget);

        if (widget.Mode == WidgetMode.Single)
        {
            CanvasHost.RowDefinitions.Add(new RowDefinition
            {
                Height = new GridLength(1, GridUnitType.Star)
            });
            CanvasHost.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star)
            });

            var editor = CreateEditor(widget.GetSingleCell());
            Grid.SetRow(editor, 0);
            Grid.SetColumn(editor, 0);
            Panel.SetZIndex(editor, 5);
            CanvasHost.Children.Add(editor);
            AddSelectedCellResizeOverlays(widget);
            _isLoading = false;
            return;
        }

        for (var row = 0; row < widget.Grid.Rows; row++)
        {
            CanvasHost.RowDefinitions.Add(new RowDefinition
            {
                Height = new GridLength(widget.Grid.RowWeights[row], GridUnitType.Star)
            });
        }

        for (var column = 0; column < widget.Grid.Columns; column++)
        {
            CanvasHost.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(widget.Grid.ColumnWeights[column], GridUnitType.Star)
            });
        }

        foreach (var cell in widget.Cells)
        {
            var editor = CreateEditor(cell);
            Grid.SetRow(editor, cell.Row);
            Grid.SetColumn(editor, cell.Column);
            Grid.SetRowSpan(editor, cell.RowSpan);
            Grid.SetColumnSpan(editor, cell.ColumnSpan);
            Panel.SetZIndex(editor, 5);
            CanvasHost.Children.Add(editor);
        }

        AddSelectedCellResizeOverlays(widget);
        _isLoading = false;
    }

    private void ApplyEditorPreviewBounds(WidgetConfig widget)
    {
        CanvasShell.Width = Math.Max(WidgetMinWidth, Math.Min(PreviewMaxWidth, widget.Bounds.Width));
        CanvasShell.Height = Math.Max(WidgetMinHeight, Math.Min(PreviewMaxHeight, widget.Bounds.Height));
    }

    private CellConfig? ResolveSelectedCell(WidgetConfig widget)
    {
        if (_selectedCell is null)
        {
            return null;
        }

        if (widget.Mode == WidgetMode.Single)
        {
            return widget.GetSingleCell();
        }

        return widget.Cells.FirstOrDefault(x => x.Id == _selectedCell.Id);
    }

    private CellRange? ResolveSelectedRange(WidgetConfig widget)
    {
        if (_selectedRange is not null)
        {
            return ClampRangeToWidget(widget, _selectedRange);
        }

        return _selectedCell is null ? null : GetCellRange(widget, _selectedCell);
    }

    private CellRange? ResolveSelectionAnchorRange(WidgetConfig widget)
    {
        return _selectionAnchorRange is null ? null : ClampRangeToWidget(widget, _selectionAnchorRange);
    }

    private CellRange ClampRangeToWidget(WidgetConfig widget, CellRange range)
    {
        var rows = GetEffectiveRows(widget);
        var columns = GetEffectiveColumns(widget);
        var row = Math.Clamp(range.Row, 0, rows - 1);
        var column = Math.Clamp(range.Column, 0, columns - 1);
        var lastRow = Math.Clamp(range.LastRow, row, rows - 1);
        var lastColumn = Math.Clamp(range.LastColumn, column, columns - 1);
        return new CellRange(row, column, lastRow - row + 1, lastColumn - column + 1);
    }

    private WpfRichTextBox CreateEditor(CellConfig cell)
    {
        var foreground = BrushFactory.Solid(_selectedWidget?.Appearance.DefaultTextColor ?? "#F2F2F2");
        var fontSize = _selectedWidget?.Appearance.DefaultFontSize ?? 16;
        var editor = new WpfRichTextBox
        {
            Tag = cell,
            Document = RichTextSerializer.Load(cell.Content, foreground, fontSize),
            Margin = new Thickness(4),
            Padding = new Thickness(8),
            Background = Brushes.Transparent,
            Foreground = foreground,
            CaretBrush = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            AcceptsTab = true
        };
        editor.PreviewMouseLeftButtonDown += (_, _) =>
            SelectCellForResize(cell, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
        editor.GotKeyboardFocus += (_, _) =>
        {
            _activeEditor = editor;
            SelectCellForResize(cell, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
            UpdateToolbarControlsFromSelection(editor);
        };
        editor.SelectionChanged += Editor_SelectionChanged;
        editor.Loaded += (_, _) => FitEditorDividers(editor);
        editor.SizeChanged += (_, _) => FitEditorDividers(editor);
        editor.PreviewKeyDown += Editor_PreviewKeyDown;
        editor.TextChanged += Editor_TextChanged;
        return editor;
    }

    private void Editor_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoading || _isUpdatingToolbarState || sender is not WpfRichTextBox editor || editor != _activeEditor)
        {
            return;
        }

        UpdateToolbarControlsFromSelection(editor);
    }

    private void FitEditorDividers(WpfRichTextBox editor)
    {
        if (editor.ActualWidth <= 0)
        {
            return;
        }

        ApplyDocumentMutation(() =>
        {
            var availableWidth = editor.ActualWidth
                - editor.Padding.Left
                - editor.Padding.Right
                - 14;
            RichTextSerializer.FitDividersToWidth(editor.Document, availableWidth);
            return true;
        });
    }

    private void Editor_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not WpfRichTextBox editor)
        {
            return;
        }

        var handled = ApplyDocumentMutation(() =>
            RichTextSerializer.HandleCompactDividerEnter(editor.Selection)
            || RichTextSerializer.HandleCompactListEnter(editor.Selection));
        if (!handled)
        {
            return;
        }

        e.Handled = true;
        SaveActiveEditor();
    }

    private void SelectCellForResize(CellConfig cell, bool extendSelection)
    {
        if (_selectedWidget is null)
        {
            return;
        }

        _selectedCell = cell;
        var cellRange = GetCellRange(_selectedWidget, cell);
        if (extendSelection && _selectionAnchorRange is not null && _selectedWidget.Mode == WidgetMode.Grid)
        {
            _selectedRange = CreateRangeFromBounds(_selectionAnchorRange, cellRange);
        }
        else
        {
            _selectedRange = cellRange;
            _selectionAnchorRange = cellRange;
        }

        RemoveResizeOverlays();
        AddSelectedCellResizeOverlays(_selectedWidget);
    }

    private void RemoveResizeOverlays()
    {
        foreach (var overlay in _resizeOverlays.ToList())
        {
            CanvasHost.Children.Remove(overlay);
        }

        _resizeOverlays.Clear();
    }

    private void AddSelectedCellResizeOverlays(WidgetConfig widget)
    {
        if (_selectedRange is null)
        {
            return;
        }

        var range = ClampRangeToWidget(widget, _selectedRange);
        _selectedRange = range;
        var outline = new Border
        {
            Style = (Style)FindResource("SelectedCellOutline")
        };
        Grid.SetRow(outline, range.Row);
        Grid.SetColumn(outline, range.Column);
        Grid.SetRowSpan(outline, range.RowSpan);
        Grid.SetColumnSpan(outline, range.ColumnSpan);
        Panel.SetZIndex(outline, 24);
        AddResizeOverlay(outline);

        AddCellResizeThumb(widget, range, ResizeEdge.Left);
        AddCellResizeThumb(widget, range, ResizeEdge.Right);
        AddCellResizeThumb(widget, range, ResizeEdge.Top);
        AddCellResizeThumb(widget, range, ResizeEdge.Bottom);
    }

    private void AddCellResizeThumb(WidgetConfig widget, CellRange range, ResizeEdge edge)
    {
        var isVertical = edge is ResizeEdge.Left or ResizeEdge.Right;
        var thumb = new Thumb
        {
            Style = (Style)FindResource(isVertical ? "VerticalCellEdgeThumb" : "HorizontalCellEdgeThumb"),
            Tag = new CellResizeTarget(range, edge),
            ToolTip = GetResizeToolTip(widget, range, edge),
            HorizontalAlignment = edge switch
            {
                ResizeEdge.Left => HorizontalAlignment.Left,
                ResizeEdge.Right => HorizontalAlignment.Right,
                _ => HorizontalAlignment.Stretch
            },
            VerticalAlignment = edge switch
            {
                ResizeEdge.Top => VerticalAlignment.Top,
                ResizeEdge.Bottom => VerticalAlignment.Bottom,
                _ => VerticalAlignment.Stretch
            },
            Margin = edge switch
            {
                ResizeEdge.Left => new Thickness(-9, 4, 0, 4),
                ResizeEdge.Right => new Thickness(0, 4, -9, 4),
                ResizeEdge.Top => new Thickness(4, -9, 4, 0),
                _ => new Thickness(4, 0, 4, -9)
            }
        };

        thumb.DragDelta += CellResizeThumb_DragDelta;
        thumb.DragCompleted += CellResizeThumb_DragCompleted;
        Grid.SetRow(thumb, range.Row);
        Grid.SetColumn(thumb, range.Column);
        Grid.SetRowSpan(thumb, range.RowSpan);
        Grid.SetColumnSpan(thumb, range.ColumnSpan);
        Panel.SetZIndex(thumb, 30);
        AddResizeOverlay(thumb);
    }

    private void AddResizeOverlay(UIElement element)
    {
        _resizeOverlays.Add(element);
        CanvasHost.Children.Add(element);
    }

    private string GetResizeToolTip(WidgetConfig widget, CellRange range, ResizeEdge edge)
    {
        var rows = GetEffectiveRows(widget);
        var columns = GetEffectiveColumns(widget);
        var adjustsWidget = edge switch
        {
            ResizeEdge.Left => range.Column == 0,
            ResizeEdge.Right => range.LastColumn == columns - 1,
            ResizeEdge.Top => range.Row == 0,
            _ => range.LastRow == rows - 1
        };

        if (adjustsWidget)
        {
            return edge is ResizeEdge.Left or ResizeEdge.Right
                ? "拖动调整小组件宽度"
                : "拖动调整小组件高度";
        }

        return edge is ResizeEdge.Left or ResizeEdge.Right
            ? "拖动调整列宽"
            : "拖动调整行高";
    }

    private void CellResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (_selectedWidget is null || sender is not Thumb { Tag: CellResizeTarget target })
        {
            return;
        }

        var changed = ResizeCellEdge(_selectedWidget, target.Range, target.Edge, e.HorizontalChange, e.VerticalChange);
        if (!changed)
        {
            return;
        }

        LoadGridControls(_selectedWidget);
    }

    private void CellResizeThumb_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (_selectedWidget is null)
        {
            return;
        }

        MonitorService.KeepVisible(_selectedWidget);
        ApplyEditorPreviewBounds(_selectedWidget);
        LoadGridControls(_selectedWidget);
        _widgetManager.RefreshWidget(_selectedWidget);
        _boardStore.SaveSoon(_document);
        SetStatus("单元格边界已更新");
    }

    private bool ResizeCellEdge(WidgetConfig widget, CellRange range, ResizeEdge edge, double horizontalDelta, double verticalDelta)
    {
        var rows = GetEffectiveRows(widget);
        var columns = GetEffectiveColumns(widget);

        return edge switch
        {
            ResizeEdge.Left when columns > 1 && range.Column > 0 => ResizeColumnBoundary(widget, range.Column - 1, range.Column, horizontalDelta),
            ResizeEdge.Right when columns > 1 && range.LastColumn < columns - 1 => ResizeColumnBoundary(widget, range.LastColumn, range.LastColumn + 1, horizontalDelta),
            ResizeEdge.Top when rows > 1 && range.Row > 0 => ResizeRowBoundary(widget, range.Row - 1, range.Row, verticalDelta),
            ResizeEdge.Bottom when rows > 1 && range.LastRow < rows - 1 => ResizeRowBoundary(widget, range.LastRow, range.LastRow + 1, verticalDelta),
            ResizeEdge.Left or ResizeEdge.Right => ResizeWidgetWidth(widget, edge, horizontalDelta),
            _ => ResizeWidgetHeight(widget, edge, verticalDelta)
        };
    }

    private bool ResizeColumnBoundary(WidgetConfig widget, int leftIndex, int rightIndex, double delta)
    {
        if (widget.Mode == WidgetMode.Single || rightIndex >= CanvasHost.ColumnDefinitions.Count)
        {
            return false;
        }

        var widths = GetColumnSizes(widget.Grid.Columns);
        if (!ResizePair(widths, leftIndex, rightIndex, delta, CellMinWidth))
        {
            return false;
        }

        widget.Grid.ColumnWeights = GridWeightService.Fit(widths, widget.Grid.Columns);
        ApplyColumnWeights(widget);
        return true;
    }

    private bool ResizeRowBoundary(WidgetConfig widget, int topIndex, int bottomIndex, double delta)
    {
        if (widget.Mode == WidgetMode.Single || bottomIndex >= CanvasHost.RowDefinitions.Count)
        {
            return false;
        }

        var heights = GetRowSizes(widget.Grid.Rows);
        if (!ResizePair(heights, topIndex, bottomIndex, delta, CellMinHeight))
        {
            return false;
        }

        widget.Grid.RowWeights = GridWeightService.Fit(heights, widget.Grid.Rows);
        ApplyRowWeights(widget);
        return true;
    }

    private static bool ResizePair(IList<double> sizes, int leadingIndex, int trailingIndex, double delta, double preferredMinSize)
    {
        if (leadingIndex < 0 || trailingIndex >= sizes.Count)
        {
            return false;
        }

        var leading = sizes[leadingIndex];
        var trailing = sizes[trailingIndex];
        var pairTotal = leading + trailing;
        var minSize = Math.Min(preferredMinSize, Math.Max(16, pairTotal / 3));
        var lower = minSize - leading;
        var upper = trailing - minSize;
        if (lower > upper)
        {
            return false;
        }

        var applied = Math.Clamp(delta, lower, upper);
        if (Math.Abs(applied) < 0.5)
        {
            return false;
        }

        sizes[leadingIndex] = leading + applied;
        sizes[trailingIndex] = trailing - applied;
        return true;
    }

    private bool ResizeWidgetWidth(WidgetConfig widget, ResizeEdge edge, double delta)
    {
        if (Math.Abs(delta) < 0.5)
        {
            return false;
        }

        var oldWidth = widget.Bounds.Width;
        var newWidth = Math.Max(WidgetMinWidth, edge == ResizeEdge.Right ? oldWidth + delta : oldWidth - delta);
        if (Math.Abs(newWidth - oldWidth) < 0.5)
        {
            return false;
        }

        if (edge == ResizeEdge.Left)
        {
            widget.Bounds.X += oldWidth - newWidth;
        }

        widget.Bounds.Width = newWidth;
        ApplyEditorPreviewBounds(widget);
        return true;
    }

    private bool ResizeWidgetHeight(WidgetConfig widget, ResizeEdge edge, double delta)
    {
        if (Math.Abs(delta) < 0.5)
        {
            return false;
        }

        var oldHeight = widget.Bounds.Height;
        var newHeight = Math.Max(WidgetMinHeight, edge == ResizeEdge.Bottom ? oldHeight + delta : oldHeight - delta);
        if (Math.Abs(newHeight - oldHeight) < 0.5)
        {
            return false;
        }

        if (edge == ResizeEdge.Top)
        {
            widget.Bounds.Y += oldHeight - newHeight;
        }

        widget.Bounds.Height = newHeight;
        ApplyEditorPreviewBounds(widget);
        return true;
    }

    private List<double> GetColumnSizes(int count)
    {
        return Enumerable.Range(0, count)
            .Select(index =>
            {
                if (index >= CanvasHost.ColumnDefinitions.Count)
                {
                    return 1.0;
                }

                var definition = CanvasHost.ColumnDefinitions[index];
                return definition.ActualWidth > 0 ? definition.ActualWidth : Math.Max(1, definition.Width.Value);
            })
            .ToList();
    }

    private List<double> GetRowSizes(int count)
    {
        return Enumerable.Range(0, count)
            .Select(index =>
            {
                if (index >= CanvasHost.RowDefinitions.Count)
                {
                    return 1.0;
                }

                var definition = CanvasHost.RowDefinitions[index];
                return definition.ActualHeight > 0 ? definition.ActualHeight : Math.Max(1, definition.Height.Value);
            })
            .ToList();
    }

    private void ApplyColumnWeights(WidgetConfig widget)
    {
        for (var column = 0; column < widget.Grid.Columns && column < CanvasHost.ColumnDefinitions.Count; column++)
        {
            CanvasHost.ColumnDefinitions[column].Width = new GridLength(widget.Grid.ColumnWeights[column], GridUnitType.Star);
        }
    }

    private void ApplyRowWeights(WidgetConfig widget)
    {
        for (var row = 0; row < widget.Grid.Rows && row < CanvasHost.RowDefinitions.Count; row++)
        {
            CanvasHost.RowDefinitions[row].Height = new GridLength(widget.Grid.RowWeights[row], GridUnitType.Star);
        }
    }

    private int GetEffectiveRows(WidgetConfig widget)
    {
        return widget.Mode == WidgetMode.Single ? 1 : widget.Grid.Rows;
    }

    private int GetEffectiveColumns(WidgetConfig widget)
    {
        return widget.Mode == WidgetMode.Single ? 1 : widget.Grid.Columns;
    }

    private int GetCellRow(WidgetConfig widget, CellConfig cell)
    {
        return widget.Mode == WidgetMode.Single ? 0 : Math.Clamp(cell.Row, 0, widget.Grid.Rows - 1);
    }

    private int GetCellColumn(WidgetConfig widget, CellConfig cell)
    {
        return widget.Mode == WidgetMode.Single ? 0 : Math.Clamp(cell.Column, 0, widget.Grid.Columns - 1);
    }

    private CellRange GetCellRange(WidgetConfig widget, CellConfig cell)
    {
        if (widget.Mode == WidgetMode.Single)
        {
            return new CellRange(0, 0, 1, 1);
        }

        var row = Math.Clamp(cell.Row, 0, widget.Grid.Rows - 1);
        var column = Math.Clamp(cell.Column, 0, widget.Grid.Columns - 1);
        var rowSpan = Math.Clamp(cell.RowSpan <= 0 ? 1 : cell.RowSpan, 1, widget.Grid.Rows - row);
        var columnSpan = Math.Clamp(cell.ColumnSpan <= 0 ? 1 : cell.ColumnSpan, 1, widget.Grid.Columns - column);
        return new CellRange(row, column, rowSpan, columnSpan);
    }

    private CellRange CreateRangeFromBounds(CellRange first, CellRange second)
    {
        var row = Math.Min(first.Row, second.Row);
        var column = Math.Min(first.Column, second.Column);
        var lastRow = Math.Max(first.LastRow, second.LastRow);
        var lastColumn = Math.Max(first.LastColumn, second.LastColumn);
        return new CellRange(row, column, lastRow - row + 1, lastColumn - column + 1);
    }

    private CellRange ExpandRangeToWholeCells(WidgetConfig widget, CellRange range)
    {
        var expanded = ClampRangeToWidget(widget, range);
        bool changed;
        do
        {
            changed = false;
            foreach (var cell in widget.Cells)
            {
                var cellRange = GetCellRange(widget, cell);
                if (!expanded.Intersects(cellRange) || expanded.Contains(cellRange))
                {
                    continue;
                }

                expanded = ClampRangeToWidget(widget, CreateRangeFromBounds(expanded, cellRange));
                changed = true;
            }
        }
        while (changed);

        return expanded;
    }

    private CellConfig? FindCellCovering(WidgetConfig widget, int row, int column)
    {
        return widget.Cells.FirstOrDefault(cell => GetCellRange(widget, cell) is { } range
            && row >= range.Row
            && row <= range.LastRow
            && column >= range.Column
            && column <= range.LastColumn);
    }

    private void Editor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoading
            || _isApplyingSelectionFormat
            || _isSavingEditorContent
            || _isMutatingEditorContent
            || sender is not WpfRichTextBox editor
            || editor.Tag is not CellConfig cell)
        {
            return;
        }

        SaveEditorContent(editor, cell, updateStatus: false);
    }

    private void AddWidgetButton_Click(object sender, RoutedEventArgs e)
    {
        var widget = WidgetConfig.CreateDefault();
        widget.Id = Guid.NewGuid().ToString("N");
        widget.Name = $"Widget {_document.ActiveBoard.Widgets.Count + 1}";
        _document.ActiveBoard.Widgets.Add(widget);
        LoadWidgetList();
        WidgetList.SelectedItem = widget;
        _widgetManager.ShowWidget(widget);
        _boardStore.SaveSoon(_document);
        SetStatus($"已新增：{widget.Name}");
    }

    private void DuplicateWidgetButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWidget is null)
        {
            return;
        }

        var clone = _selectedWidget.Clone();
        _document.ActiveBoard.Widgets.Add(clone);
        LoadWidgetList();
        WidgetList.SelectedItem = clone;
        _widgetManager.ShowWidget(clone);
        _boardStore.SaveSoon(_document);
        SetStatus($"已复制：{clone.Name}");
    }

    private void DeleteWidgetButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWidget is null || _document.ActiveBoard.Widgets.Count <= 1)
        {
            if (_document.ActiveBoard.Widgets.Count <= 1)
            {
                MessageBox.Show(this, "至少需要保留一个小组件。", "无法删除", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            return;
        }

        var result = MessageBox.Show(
            this,
            $"确定删除“{_selectedWidget.Name}”吗？此操作无法撤销。",
            "删除小组件",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        var deletedName = _selectedWidget.Name;
        var id = _selectedWidget.Id;
        _document.ActiveBoard.Widgets.Remove(_selectedWidget);
        _widgetManager.CloseWidget(id);
        LoadWidgetList();
        WidgetList.SelectedIndex = 0;
        _boardStore.SaveSoon(_document);
        SetStatus($"已删除：{deletedName}");
    }

    private void NameBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoading || _selectedWidget is null)
        {
            return;
        }

        _selectedWidget.Name = string.IsNullOrWhiteSpace(NameBox.Text) ? "阿卡夏便笺" : NameBox.Text.Trim();
        WidgetList.Items.Refresh();
        _boardStore.SaveSoon(_document);
        SetStatus("名称已更新");
    }

    private void ModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || _selectedWidget is null || ModeCombo.SelectedItem is not string value)
        {
            return;
        }

        _selectedWidget.Mode = value == "Single" ? WidgetMode.Single : WidgetMode.Grid;
        BuildEditorCanvas(_selectedWidget);
        _widgetManager.RefreshWidget(_selectedWidget);
        _boardStore.SaveSoon(_document);
        SetStatus("模式已更新");
    }

    private void ApplyGridButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWidget is null)
        {
            return;
        }

        var rowWeightFallback = _selectedWidget.Grid.RowWeights.ToList();
        var columnWeightFallback = _selectedWidget.Grid.ColumnWeights.ToList();

        if (int.TryParse(RowsBox.Text, out var rows))
        {
            _selectedWidget.Grid.Rows = rows;
        }

        if (int.TryParse(ColumnsBox.Text, out var columns))
        {
            _selectedWidget.Grid.Columns = columns;
        }

        _selectedWidget.Grid.Normalize();
        _selectedWidget.Grid.RowWeights = GridWeightService.Parse(RowWeightsBox.Text, _selectedWidget.Grid.Rows, rowWeightFallback);
        _selectedWidget.Grid.ColumnWeights = GridWeightService.Parse(ColumnWeightsBox.Text, _selectedWidget.Grid.Columns, columnWeightFallback);
        _selectedWidget.Grid.Normalize();
        _selectedWidget.EnsureCells();
        LoadGridControls(_selectedWidget);
        BuildEditorCanvas(_selectedWidget);
        _widgetManager.RefreshWidget(_selectedWidget);
        _boardStore.SaveSoon(_document);
        SetStatus($"网格已更新为 {_selectedWidget.Grid.Rows} x {_selectedWidget.Grid.Columns}");
    }

    private void MergeCellsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWidget is null || _selectedWidget.Mode == WidgetMode.Single)
        {
            SetStatus("单格模式无需合并");
            return;
        }

        var selection = _selectedRange
            ?? (_selectedCell is null ? null : GetCellRange(_selectedWidget, _selectedCell));
        if (selection is null)
        {
            SetStatus("请先选择单元格");
            return;
        }

        var range = ExpandRangeToWholeCells(_selectedWidget, selection);
        if (range.RowSpan == 1 && range.ColumnSpan == 1)
        {
            SetStatus("选区只有一个单元格");
            return;
        }

        var cells = _selectedWidget.Cells
            .Where(cell => range.Contains(GetCellRange(_selectedWidget, cell)))
            .OrderBy(cell => cell.Row)
            .ThenBy(cell => cell.Column)
            .ToList();
        if (cells.Count == 0)
        {
            SetStatus("选区无可合并单元格");
            return;
        }

        var primary = cells.First();
        var foreground = BrushFactory.Solid(_selectedWidget.Appearance.DefaultTextColor);
        primary.Content = RichTextSerializer.MergeCellContents(
            cells.Select(cell => cell.Content),
            foreground,
            _selectedWidget.Appearance.DefaultFontSize);
        primary.ContentFormat = "wpf-xaml-package-base64";
        primary.Row = range.Row;
        primary.Column = range.Column;
        primary.RowSpan = range.RowSpan;
        primary.ColumnSpan = range.ColumnSpan;

        _selectedWidget.Cells.RemoveAll(cell => cell.Id != primary.Id && range.Contains(GetCellRange(_selectedWidget, cell)));
        _selectedWidget.EnsureCells();

        _selectedCell = _selectedWidget.Cells.FirstOrDefault(cell => cell.Id == primary.Id)
            ?? FindCellCovering(_selectedWidget, range.Row, range.Column);
        _selectedRange = _selectedCell is null ? range : GetCellRange(_selectedWidget, _selectedCell);
        _selectionAnchorRange = _selectedRange;

        BuildEditorCanvas(_selectedWidget);
        _widgetManager.RefreshWidget(_selectedWidget);
        _boardStore.SaveSoon(_document);
        SetStatus($"已合并为 {range.RowSpan} x {range.ColumnSpan}");
    }

    private void SplitCellButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWidget is null || _selectedWidget.Mode == WidgetMode.Single)
        {
            SetStatus("单格模式无需拆分");
            return;
        }

        var range = _selectedRange is null ? null : ClampRangeToWidget(_selectedWidget, _selectedRange);
        var cell = _selectedCell
            ?? (range is null ? null : FindCellCovering(_selectedWidget, range.Row, range.Column));
        if (cell is null)
        {
            SetStatus("请先选择单元格");
            return;
        }

        var cellRange = GetCellRange(_selectedWidget, cell);
        if (cellRange.RowSpan == 1 && cellRange.ColumnSpan == 1)
        {
            SetStatus("当前单元格未合并");
            return;
        }

        cell.RowSpan = 1;
        cell.ColumnSpan = 1;
        _selectedWidget.EnsureCells();
        _selectedCell = _selectedWidget.Cells.FirstOrDefault(x => x.Id == cell.Id)
            ?? FindCellCovering(_selectedWidget, cellRange.Row, cellRange.Column);
        _selectedRange = _selectedCell is null ? null : GetCellRange(_selectedWidget, _selectedCell);
        _selectionAnchorRange = _selectedRange;

        BuildEditorCanvas(_selectedWidget);
        _widgetManager.RefreshWidget(_selectedWidget);
        _boardStore.SaveSoon(_document);
        SetStatus("单元格已拆分");
    }

    private void LockCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading || _selectedWidget is null)
        {
            return;
        }

        _selectedWidget.IsLocked = LockCheck.IsChecked == true;
        _widgetManager.RefreshWidget(_selectedWidget);
        _boardStore.SaveSoon(_document);
        SetStatus(_selectedWidget.IsLocked ? "已锁定桌面交互" : "已解锁，可移动和缩放");
    }

    private void ShowFrameCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoading || _selectedWidget is null)
        {
            return;
        }

        _selectedWidget.Appearance.ShowFrame = ShowFrameCheck.IsChecked == true;
        BuildEditorCanvas(_selectedWidget);
        _widgetManager.RefreshWidget(_selectedWidget);
        _boardStore.SaveSoon(_document);
        SetStatus(_selectedWidget.Appearance.ShowFrame ? "桌面边框已显示" : "桌面边框已隐藏");
    }

    private void MoveToMonitorButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWidget is null || MonitorCombo.SelectedItem is not MonitorInfo monitor)
        {
            return;
        }

        MonitorService.MoveToMonitor(_selectedWidget, monitor);
        _widgetManager.RefreshWidget(_selectedWidget);
        _boardStore.SaveSoon(_document);
        SetStatus($"已移动到：{monitor.DisplayName}");
    }

    private void PresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || _selectedWidget is null || PresetCombo.SelectedItem is not string preset)
        {
            return;
        }

        AppearancePresetService.Apply(_selectedWidget, preset);
        SelectWidget(_selectedWidget);
        _widgetManager.RefreshWidget(_selectedWidget);
        _boardStore.SaveSoon(_document);
        SetStatus($"已应用预设：{preset}");
    }

    private void AppearanceBox_LostFocus(object sender, RoutedEventArgs e)
    {
        ApplyAppearanceFromControls();
    }

    private void AppearanceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading)
        {
            return;
        }

        QueueAppearanceUpdate();
    }

    private void ApplyAppearanceFromControls()
    {
        if (_isLoading || _selectedWidget is null)
        {
            return;
        }

        UpdateAppearanceFromControls(_selectedWidget);
        ApplyEditorPreviewAppearance(_selectedWidget);
        FlushAppearanceUpdate();
    }

    private void QueueAppearanceUpdate()
    {
        if (_isLoading || _selectedWidget is null)
        {
            return;
        }

        UpdateAppearanceFromControls(_selectedWidget);
        ApplyEditorPreviewAppearance(_selectedWidget);
        _appearanceUpdateTimer.Stop();
        _appearanceUpdateTimer.Start();
        SetStatus("外观更新待同步");
    }

    private void FlushAppearanceUpdate()
    {
        if (_isLoading || _selectedWidget is null)
        {
            return;
        }

        _widgetManager.RefreshWidget(_selectedWidget);
        _boardStore.SaveSoon(_document);
        SetStatus("外观已更新");
    }

    private void UpdateAppearanceFromControls(WidgetConfig widget)
    {
        var appearance = widget.Appearance;
        appearance.BackgroundColor = BrushFactory.NormalizeHex(BackgroundColorBox.Text, appearance.BackgroundColor);
        appearance.BackgroundOpacity = BackgroundOpacitySlider.Value;
        appearance.BorderColor = BrushFactory.NormalizeHex(BorderColorBox.Text, appearance.BorderColor);
        appearance.BorderOpacity = BorderOpacitySlider.Value;
        appearance.BorderThickness = BorderThicknessSlider.Value;
        appearance.CornerRadius = CornerRadiusSlider.Value;
        appearance.Padding = PaddingSlider.Value;
        appearance.DefaultTextColor = BrushFactory.NormalizeHex(DefaultTextColorBox.Text, appearance.DefaultTextColor);
        appearance.DefaultFontSize = DefaultFontSizeSlider.Value;

        BackgroundColorBox.Text = appearance.BackgroundColor;
        BorderColorBox.Text = appearance.BorderColor;
        DefaultTextColorBox.Text = appearance.DefaultTextColor;
    }

    private void ApplyEditorPreviewAppearance(WidgetConfig widget)
    {
        var appearance = widget.Appearance;
        CanvasShell.Background = BrushFactory.Solid(appearance.BackgroundColor, appearance.BackgroundOpacity);
        CanvasShell.BorderBrush = BrushFactory.Solid(appearance.BorderColor, Math.Max(0.18, appearance.BorderOpacity));
        CanvasShell.BorderThickness = new Thickness(appearance.ShowFrame ? Math.Max(1, appearance.BorderThickness) : 0);
        CanvasShell.CornerRadius = new CornerRadius(appearance.CornerRadius);
        CanvasShell.Padding = new Thickness(appearance.Padding);

        var foreground = BrushFactory.Solid(appearance.DefaultTextColor);
        foreach (var editor in CanvasHost.Children.OfType<WpfRichTextBox>())
        {
            editor.Foreground = foreground;
            editor.FontSize = appearance.DefaultFontSize;
            editor.Document.Foreground = foreground;
            editor.Document.FontSize = appearance.DefaultFontSize;
        }

        if (_activeEditor is not null)
        {
            UpdateToolbarControlsFromSelection(_activeEditor);
        }
    }

    private void BoldButton_Click(object sender, RoutedEventArgs e) => Execute(EditingCommands.ToggleBold);
    private void ItalicButton_Click(object sender, RoutedEventArgs e) => Execute(EditingCommands.ToggleItalic);
    private void UnderlineButton_Click(object sender, RoutedEventArgs e) => Execute(EditingCommands.ToggleUnderline);
    private void BulletsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeEditor is null)
        {
            return;
        }

        TryApplyList(() => RichTextSerializer.ToggleCompactBullets(_activeEditor.Document, _activeEditor.Selection));
    }

    private void NumbersButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeEditor is null)
        {
            return;
        }

        TryApplyList(() => RichTextSerializer.ToggleCompactNumbering(_activeEditor.Document, _activeEditor.Selection));
    }

    private void DividerButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeEditor is null)
        {
            return;
        }

        TryApplyList(() => RichTextSerializer.InsertCompactDivider(_activeEditor.Document, _activeEditor.Selection));
    }

    private void AlignLeftButton_Click(object sender, RoutedEventArgs e) => ExecuteAlignment(EditingCommands.AlignLeft, "已左对齐");
    private void AlignCenterButton_Click(object sender, RoutedEventArgs e) => ExecuteAlignment(EditingCommands.AlignCenter, "已居中");
    private void AlignRightButton_Click(object sender, RoutedEventArgs e) => ExecuteAlignment(EditingCommands.AlignRight, "已右对齐");

    private void StrikethroughButton_Click(object sender, RoutedEventArgs e)
    {
        ApplySelectionFormat(() => _activeEditor?.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, TextDecorations.Strikethrough));
    }

    private void FontSizeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingToolbarState)
        {
            return;
        }

        if (FontSizeBox.SelectedItem is double selectedSize)
        {
            ApplyFontSizeToSelection(selectedSize);
            return;
        }

        ApplyFontSizeFromToolbar();
    }

    private void FontSizeBox_LostFocus(object sender, RoutedEventArgs e)
    {
        ApplyFontSizeFromToolbar();
    }

    private void FontSizeBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        ApplyFontSizeFromToolbar();
        _activeEditor?.Focus();
        e.Handled = true;
    }

    private void ApplyFontSizeFromToolbar()
    {
        if (_isUpdatingToolbarState || _activeEditor is null || !TryGetToolbarFontSize(out var size))
        {
            return;
        }

        ApplyFontSizeToSelection(size);
    }

    private void ApplyFontSizeToSelection(double size)
    {
        if (_activeEditor is null)
        {
            return;
        }

        var normalized = NormalizeFontSize(size);
        ApplySelectionFormat(() => _activeEditor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, normalized));
        SetFontSizeControlValue(normalized);
    }

    private bool TryGetToolbarFontSize(out double size)
    {
        size = 0;
        var text = FontSizeBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(text))
        {
            return TryParseFontSize(text, out size);
        }

        return FontSizeBox.SelectedItem switch
        {
            double selected => IsAllowedFontSize(selected, out size),
            int selected => IsAllowedFontSize(selected, out size),
            string selected => TryParseFontSize(selected, out size),
            _ => false
        };
    }

    private void UpdateFontSizeControlFromSelection(WpfRichTextBox editor)
    {
        var value = editor.Selection.GetPropertyValue(TextElement.FontSizeProperty);
        if (value == DependencyProperty.UnsetValue)
        {
            if (editor.Selection.IsEmpty)
            {
                SetFontSizeControlValue(editor.Document.FontSize);
            }
            else
            {
                SetFontSizeControlMixed();
            }
            return;
        }

        if (value is double size)
        {
            SetFontSizeControlValue(size);
            return;
        }

        SetFontSizeControlMixed();
    }

    private void UpdateToolbarControlsFromSelection(WpfRichTextBox editor)
    {
        UpdateFontSizeControlFromSelection(editor);
        UpdateTextColorControlFromSelection(editor);
        UpdateHighlightControlFromSelection(editor);
    }

    private void UpdateTextColorControlFromSelection(WpfRichTextBox editor)
    {
        var value = editor.Selection.GetPropertyValue(TextElement.ForegroundProperty);
        if (value == DependencyProperty.UnsetValue)
        {
            SetComboBoxValue(TextColorBox, editor.Selection.IsEmpty ? "Default" : "混合");
            return;
        }

        var defaultBrush = editor.Document.Foreground ?? editor.Foreground;
        SetComboBoxValue(TextColorBox, BrushValueToTextColorName(value, defaultBrush));
    }

    private void UpdateHighlightControlFromSelection(WpfRichTextBox editor)
    {
        var value = editor.Selection.GetPropertyValue(TextElement.BackgroundProperty);
        if (value == DependencyProperty.UnsetValue)
        {
            SetComboBoxValue(HighlightColorBox, editor.Selection.IsEmpty ? "None" : "混合");
            return;
        }

        SetComboBoxValue(HighlightColorBox, BrushValueToHighlightName(value));
    }

    private void SetFontSizeControlValue(double size)
    {
        var normalized = NormalizeFontSize(size);
        var matching = FontSizeOptions.FirstOrDefault(option => Math.Abs(option - normalized) < 0.01);
        _isUpdatingToolbarState = true;
        try
        {
            FontSizeBox.SelectedItem = matching > 0 ? matching : null;
            FontSizeBox.Text = FormatFontSize(normalized);
        }
        finally
        {
            _isUpdatingToolbarState = false;
        }
    }

    private void SetFontSizeControlMixed()
    {
        _isUpdatingToolbarState = true;
        try
        {
            FontSizeBox.SelectedItem = null;
            FontSizeBox.Text = "混合";
        }
        finally
        {
            _isUpdatingToolbarState = false;
        }
    }

    private void SetComboBoxValue(ComboBox comboBox, string value)
    {
        _isUpdatingToolbarState = true;
        try
        {
            comboBox.SelectedItem = value;
            comboBox.Text = value;
        }
        finally
        {
            _isUpdatingToolbarState = false;
        }
    }

    private static bool TryParseFontSize(string text, out double size)
    {
        text = text.Trim();
        if (text == "混合")
        {
            size = 0;
            return false;
        }

        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out size)
            && !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out size))
        {
            return false;
        }

        size = NormalizeFontSize(size);
        return true;
    }

    private static bool IsAllowedFontSize(double value, out double size)
    {
        size = NormalizeFontSize(value);
        return true;
    }

    private static double NormalizeFontSize(double size)
    {
        if (double.IsNaN(size) || double.IsInfinity(size))
        {
            return 16;
        }

        return Math.Clamp(Math.Round(size * 2, MidpointRounding.AwayFromZero) / 2, 6, 96);
    }

    private static string FormatFontSize(double size)
    {
        return Math.Abs(size - Math.Round(size)) < 0.01
            ? ((int)Math.Round(size)).ToString(CultureInfo.CurrentCulture)
            : size.ToString("0.#", CultureInfo.CurrentCulture);
    }

    private void TextColorBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingToolbarState
            || _activeEditor is null
            || TextColorBox.SelectedItem is not string name
            || name == "混合")
        {
            return;
        }

        var brush = name == "Default"
            ? CloneBrush(_activeEditor.Document.Foreground ?? _activeEditor.Foreground)
            : ColorNameToBrush(name);
        ApplySelectionFormat(() => _activeEditor.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, brush));
        SetComboBoxValue(TextColorBox, name);
    }

    private void HighlightColorBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingToolbarState
            || _activeEditor is null
            || HighlightColorBox.SelectedItem is not string name
            || name == "混合")
        {
            return;
        }

        var brush = name == "None" ? Brushes.Transparent.Clone() : ColorNameToBrush(name);
        brush.Opacity = name == "None" ? 0 : 0.55;
        ApplySelectionFormat(() => _activeEditor.Selection.ApplyPropertyValue(TextElement.BackgroundProperty, brush));
        SetComboBoxValue(HighlightColorBox, name);
    }

    private void SaveNowButton_Click(object sender, RoutedEventArgs e)
    {
        _boardStore.SaveNow(_document);
        _widgetManager.RefreshAll();
        SetStatus("已保存");
    }

    private void Execute(RoutedUICommand command)
    {
        if (_activeEditor is null)
        {
            return;
        }

        ApplyDocumentMutation(() =>
        {
            command.Execute(null, _activeEditor);
            RichTextSerializer.ApplyDesktopLayout(_activeEditor.Document);
            return true;
        });
        SaveActiveEditor();
    }

    private void ExecuteAlignment(RoutedUICommand command, string status)
    {
        Execute(command);
        SetStatus(status);
    }

    private void ApplySelectionFormat(Action action)
    {
        if (_activeEditor is null)
        {
            return;
        }

        _isApplyingSelectionFormat = true;
        try
        {
            action();
        }
        finally
        {
            _isApplyingSelectionFormat = false;
        }

        SaveActiveEditor();
        UpdateToolbarControlsFromSelection(_activeEditor);
    }

    private void TryApplyList(Action action)
    {
        try
        {
            ApplyDocumentMutation(() =>
            {
                action();
                return true;
            });
            SaveActiveEditor();
        }
        catch (Exception ex)
        {
            SetStatus($"列表格式失败：{ex.Message}");
        }
    }

    private bool ApplyDocumentMutation(Func<bool> action)
    {
        _isMutatingEditorContent = true;
        try
        {
            return action();
        }
        finally
        {
            _isMutatingEditorContent = false;
        }
    }

    private void SaveActiveEditor()
    {
        if (_activeEditor?.Tag is not CellConfig cell)
        {
            return;
        }

        SaveEditorContent(_activeEditor, cell, updateStatus: true);
    }

    private void SaveEditorContent(WpfRichTextBox editor, CellConfig cell, bool updateStatus)
    {
        if (_isSavingEditorContent)
        {
            return;
        }

        _isSavingEditorContent = true;
        try
        {
            cell.Content = RichTextSerializer.Save(editor.Document);
        }
        finally
        {
            _isSavingEditorContent = false;
        }

        if (_selectedWidget is not null)
        {
            _widgetManager.RefreshWidget(_selectedWidget);
        }
        _boardStore.SaveSoon(_document);
        if (updateStatus)
        {
            SetStatus("内容已同步");
        }
    }

    private void SetStatus(string message)
    {
        StatusText.Text = message;
    }

    private static string BrushValueToTextColorName(object value, Brush defaultBrush)
    {
        if (value is not SolidColorBrush brush)
        {
            return "混合";
        }

        if (BrushesMatch(brush, defaultBrush))
        {
            return "Default";
        }

        return BrushToKnownName(brush) ?? "混合";
    }

    private static string BrushValueToHighlightName(object value)
    {
        if (value is null)
        {
            return "None";
        }

        if (value is not SolidColorBrush brush)
        {
            return "混合";
        }

        if (brush.Opacity <= 0.01 || brush.Color.A == 0 || brush.Color == Colors.Transparent)
        {
            return "None";
        }

        return BrushToKnownName(brush) ?? "混合";
    }

    private static string? BrushToKnownName(SolidColorBrush brush)
    {
        foreach (var name in new[] { "White", "Black", "Gray", "Red", "Orange", "Yellow", "Green", "Cyan", "Blue", "Purple", "Pink" })
        {
            if (BrushesMatch(brush, ColorNameToBrush(name)))
            {
                return name;
            }
        }

        return null;
    }

    private static bool BrushesMatch(Brush left, Brush right)
    {
        if (left is not SolidColorBrush leftSolid || right is not SolidColorBrush rightSolid)
        {
            return false;
        }

        return leftSolid.Color.R == rightSolid.Color.R
            && leftSolid.Color.G == rightSolid.Color.G
            && leftSolid.Color.B == rightSolid.Color.B;
    }

    private static SolidColorBrush CloneBrush(Brush brush)
    {
        return brush is SolidColorBrush solid
            ? solid.Clone()
            : Brushes.White.Clone();
    }

    private static SolidColorBrush ColorNameToBrush(string name)
    {
        var brush = name switch
        {
            "White" => Brushes.White,
            "Black" => Brushes.Black,
            "Gray" => Brushes.Gray,
            "Red" => Brushes.IndianRed,
            "Orange" => Brushes.Orange,
            "Yellow" => Brushes.Gold,
            "Green" => Brushes.LightGreen,
            "Cyan" => Brushes.Cyan,
            "Blue" => Brushes.LightSkyBlue,
            "Purple" => Brushes.Plum,
            "Pink" => Brushes.HotPink,
            _ => Brushes.White
        };
        return brush.Clone();
    }
}
