using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using DesktopTextBoard.Models;
using DesktopTextBoard.Services;
using WpfRichTextBox = System.Windows.Controls.RichTextBox;

namespace DesktopTextBoard.Windows;

public partial class EditorWindow : Window
{
    private readonly BoardDocument _document;
    private readonly BoardStore _boardStore;
    private readonly DesktopWidgetManager _widgetManager;
    private readonly List<MonitorInfo> _monitors;
    private WidgetConfig? _selectedWidget;
    private WpfRichTextBox? _activeEditor;
    private bool _isLoading;
    private bool _forceClose;

    public EditorWindow(BoardDocument document, BoardStore boardStore, DesktopWidgetManager widgetManager)
    {
        InitializeComponent();
        _document = document;
        _boardStore = boardStore;
        _widgetManager = widgetManager;
        _monitors = MonitorService.GetMonitors().ToList();
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
        FontSizeBox.ItemsSource = new[] { 10, 12, 14, 16, 18, 20, 24, 28, 32 };
        FontSizeBox.SelectedItem = 16;

        TextColorBox.ItemsSource = new[]
        {
            "Default", "White", "Black", "Gray", "Red", "Orange", "Yellow", "Green", "Cyan", "Blue", "Purple"
        };
        TextColorBox.SelectedIndex = 0;

        HighlightColorBox.ItemsSource = new[]
        {
            "None", "Yellow", "Green", "Cyan", "Pink", "Orange"
        };
        HighlightColorBox.SelectedIndex = 0;
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
        _activeEditor = null;
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
        RowsBox.Text = widget.Grid.Rows.ToString();
        ColumnsBox.Text = widget.Grid.Columns.ToString();
        LockCheck.IsChecked = widget.IsLocked;
        ShowFrameCheck.IsChecked = widget.Appearance.ShowFrame;
        MonitorCombo.SelectedItem = _monitors.FirstOrDefault(x => x.DeviceName == widget.MonitorDeviceName)
                                    ?? _monitors.FirstOrDefault(x => x.IsPrimary);
        LoadAppearanceControls(widget);
        _isLoading = false;

        BuildEditorCanvas(widget);
        SetStatus($"正在编辑：{widget.Name}");
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
        CanvasHost.Children.Clear();
        CanvasHost.RowDefinitions.Clear();
        CanvasHost.ColumnDefinitions.Clear();

        CanvasShell.Width = Math.Max(360, Math.Min(900, widget.Bounds.Width));
        CanvasShell.Height = Math.Max(260, Math.Min(650, widget.Bounds.Height));
        CanvasShell.Background = BrushFactory.Solid(widget.Appearance.BackgroundColor, widget.Appearance.BackgroundOpacity);
        CanvasShell.BorderBrush = BrushFactory.Solid(widget.Appearance.BorderColor, Math.Max(0.18, widget.Appearance.BorderOpacity));
        CanvasShell.BorderThickness = new Thickness(widget.Appearance.ShowFrame ? Math.Max(1, widget.Appearance.BorderThickness) : 0);
        CanvasShell.CornerRadius = new CornerRadius(widget.Appearance.CornerRadius);

        if (widget.Mode == WidgetMode.Single)
        {
            var editor = CreateEditor(widget.GetSingleCell());
            CanvasHost.Children.Add(editor);
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
            CanvasHost.Children.Add(editor);
        }

        AddGridSplitters(widget);
        _isLoading = false;
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
        editor.GotKeyboardFocus += (_, _) => _activeEditor = editor;
        editor.PreviewKeyDown += Editor_PreviewKeyDown;
        editor.TextChanged += Editor_TextChanged;
        return editor;
    }

    private void Editor_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not WpfRichTextBox editor)
        {
            return;
        }

        if (!RichTextSerializer.HandleCompactListEnter(editor.Selection))
        {
            return;
        }

        e.Handled = true;
        SaveActiveEditor();
    }

    private void AddGridSplitters(WidgetConfig widget)
    {
        for (var column = 0; column < widget.Grid.Columns - 1; column++)
        {
            var splitter = new GridSplitter
            {
                Width = 5,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = Brushes.Transparent,
                ResizeBehavior = GridResizeBehavior.PreviousAndNext,
                ShowsPreview = false
            };
            splitter.DragCompleted += (_, _) => CaptureGridWeights(widget);
            Panel.SetZIndex(splitter, 20);
            Grid.SetColumn(splitter, column);
            Grid.SetRowSpan(splitter, widget.Grid.Rows);
            CanvasHost.Children.Add(splitter);
        }

        for (var row = 0; row < widget.Grid.Rows - 1; row++)
        {
            var splitter = new GridSplitter
            {
                Height = 5,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Bottom,
                Background = Brushes.Transparent,
                ResizeBehavior = GridResizeBehavior.PreviousAndNext,
                ShowsPreview = false
            };
            splitter.DragCompleted += (_, _) => CaptureGridWeights(widget);
            Panel.SetZIndex(splitter, 20);
            Grid.SetRow(splitter, row);
            Grid.SetColumnSpan(splitter, widget.Grid.Columns);
            CanvasHost.Children.Add(splitter);
        }
    }

    private void CaptureGridWeights(WidgetConfig widget)
    {
        widget.Grid.RowWeights = CanvasHost.RowDefinitions.Select(x => Math.Max(0.1, x.ActualHeight)).ToList();
        widget.Grid.ColumnWeights = CanvasHost.ColumnDefinitions.Select(x => Math.Max(0.1, x.ActualWidth)).ToList();
        _widgetManager.RefreshWidget(widget);
        _boardStore.SaveSoon(_document);
    }

    private void Editor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoading || sender is not WpfRichTextBox editor || editor.Tag is not CellConfig cell)
        {
            return;
        }

        cell.Content = RichTextSerializer.Save(editor.Document);
        if (_selectedWidget is not null)
        {
            _widgetManager.RefreshWidget(_selectedWidget);
        }
        _boardStore.SaveSoon(_document);
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

        _selectedWidget.Name = string.IsNullOrWhiteSpace(NameBox.Text) ? "Desktop Board" : NameBox.Text.Trim();
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

        if (int.TryParse(RowsBox.Text, out var rows))
        {
            _selectedWidget.Grid.Rows = rows;
        }

        if (int.TryParse(ColumnsBox.Text, out var columns))
        {
            _selectedWidget.Grid.Columns = columns;
        }

        _selectedWidget.Grid.Normalize();
        _selectedWidget.EnsureCells();
        RowsBox.Text = _selectedWidget.Grid.Rows.ToString();
        ColumnsBox.Text = _selectedWidget.Grid.Columns.ToString();
        BuildEditorCanvas(_selectedWidget);
        _widgetManager.RefreshWidget(_selectedWidget);
        _boardStore.SaveSoon(_document);
        SetStatus($"网格已更新为 {_selectedWidget.Grid.Rows} x {_selectedWidget.Grid.Columns}");
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

        ApplyAppearanceFromControls();
    }

    private void ApplyAppearanceFromControls()
    {
        if (_isLoading || _selectedWidget is null)
        {
            return;
        }

        var appearance = _selectedWidget.Appearance;
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
        BuildEditorCanvas(_selectedWidget);
        _widgetManager.RefreshWidget(_selectedWidget);
        _boardStore.SaveSoon(_document);
        SetStatus("外观已更新");
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
    private void AlignLeftButton_Click(object sender, RoutedEventArgs e) => Execute(EditingCommands.AlignLeft);
    private void AlignCenterButton_Click(object sender, RoutedEventArgs e) => Execute(EditingCommands.AlignCenter);
    private void AlignRightButton_Click(object sender, RoutedEventArgs e) => Execute(EditingCommands.AlignRight);

    private void StrikethroughButton_Click(object sender, RoutedEventArgs e)
    {
        _activeEditor?.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, TextDecorations.Strikethrough);
        SaveActiveEditor();
    }

    private void FontSizeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_activeEditor is null || FontSizeBox.SelectedItem is not int size)
        {
            return;
        }

        _activeEditor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, (double)size);
        SaveActiveEditor();
    }

    private void TextColorBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_activeEditor is null || TextColorBox.SelectedItem is not string name || name == "Default")
        {
            return;
        }

        _activeEditor.Selection.ApplyPropertyValue(TextElement.ForegroundProperty, ColorNameToBrush(name));
        SaveActiveEditor();
    }

    private void HighlightColorBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_activeEditor is null || HighlightColorBox.SelectedItem is not string name)
        {
            return;
        }

        var brush = name == "None" ? Brushes.Transparent.Clone() : ColorNameToBrush(name);
        brush.Opacity = name == "None" ? 0 : 0.55;
        _activeEditor.Selection.ApplyPropertyValue(TextElement.BackgroundProperty, brush);
        SaveActiveEditor();
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

        command.Execute(null, _activeEditor);
        RichTextSerializer.ApplyDesktopLayout(_activeEditor.Document);
        SaveActiveEditor();
    }

    private void TryApplyList(Action action)
    {
        try
        {
            action();
            SaveActiveEditor();
        }
        catch (Exception ex)
        {
            SetStatus($"列表格式失败：{ex.Message}");
        }
    }

    private void SaveActiveEditor()
    {
        if (_activeEditor?.Tag is not CellConfig cell)
        {
            return;
        }

        RichTextSerializer.ApplyDesktopLayout(_activeEditor.Document);
        cell.Content = RichTextSerializer.Save(_activeEditor.Document);
        if (_selectedWidget is not null)
        {
            _widgetManager.RefreshWidget(_selectedWidget);
        }
        _boardStore.SaveSoon(_document);
        SetStatus("内容已同步");
    }

    private void SetStatus(string message)
    {
        StatusText.Text = message;
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
