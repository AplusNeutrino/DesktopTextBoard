using DesktopTextBoard.Models;
using DesktopTextBoard.Windows;

namespace DesktopTextBoard.Services;

public sealed class DesktopWidgetManager
{
    private readonly BoardDocument _document;
    private readonly BoardStore _boardStore;
    private readonly Dictionary<string, DesktopWidgetWindow> _windows = new();

    public DesktopWidgetManager(BoardDocument document, BoardStore boardStore)
    {
        _document = document;
        _boardStore = boardStore;
    }

    public event Action? WidgetsChanged;

    public void ShowActiveBoard()
    {
        CloseAll();
        foreach (var widget in _document.ActiveBoard.Widgets)
        {
            ShowWidget(widget);
        }
        WidgetsChanged?.Invoke();
    }

    public void ShowWidget(WidgetConfig widget)
    {
        if (_windows.ContainsKey(widget.Id))
        {
            return;
        }

        MonitorService.KeepVisible(widget);
        var window = new DesktopWidgetWindow(widget, () =>
        {
            _boardStore.SaveSoon(_document);
            RefreshWidget(widget);
        });
        _windows[widget.Id] = window;
        window.Show();
    }

    public void RefreshWidget(WidgetConfig widget)
    {
        if (_windows.TryGetValue(widget.Id, out var window))
        {
            window.ApplyConfig();
        }
        else
        {
            ShowWidget(widget);
        }
        WidgetsChanged?.Invoke();
    }

    public void RefreshAll()
    {
        foreach (var widget in _document.ActiveBoard.Widgets)
        {
            RefreshWidget(widget);
        }
    }

    public void SetAllLocked(bool locked)
    {
        foreach (var widget in _document.ActiveBoard.Widgets)
        {
            widget.IsLocked = locked;
            RefreshWidget(widget);
        }
        _boardStore.SaveSoon(_document);
    }

    public void CloseWidget(string widgetId)
    {
        if (!_windows.Remove(widgetId, out var window))
        {
            return;
        }

        window.ForceClose();
        WidgetsChanged?.Invoke();
    }

    public void CloseAll()
    {
        foreach (var window in _windows.Values.ToList())
        {
            window.ForceClose();
        }
        _windows.Clear();
    }
}
