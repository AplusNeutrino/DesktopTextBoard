using System.Windows.Forms;
using DesktopTextBoard.Models;

namespace DesktopTextBoard.Services;

public static class MonitorService
{
    public static IReadOnlyList<MonitorInfo> GetMonitors()
    {
        return Screen.AllScreens.Select((screen, index) => new MonitorInfo
        {
            DeviceName = screen.DeviceName,
            DisplayName = screen.Primary ? $"Primary ({index + 1})" : $"Monitor {index + 1}",
            X = screen.WorkingArea.Left,
            Y = screen.WorkingArea.Top,
            Width = screen.WorkingArea.Width,
            Height = screen.WorkingArea.Height,
            IsPrimary = screen.Primary
        }).ToList();
    }

    public static MonitorInfo GetTargetMonitor(string? deviceName)
    {
        var monitors = GetMonitors();
        var monitor = monitors.FirstOrDefault(x => x.DeviceName == deviceName);
        return monitor ?? monitors.FirstOrDefault(x => x.IsPrimary) ?? monitors[0];
    }

    public static void MoveToMonitor(WidgetConfig widget, MonitorInfo monitor)
    {
        widget.MonitorDeviceName = monitor.DeviceName;
        widget.Bounds.X = monitor.X + Math.Max(24, (monitor.Width - widget.Bounds.Width) / 2);
        widget.Bounds.Y = monitor.Y + Math.Max(24, (monitor.Height - widget.Bounds.Height) / 2);
    }

    public static void KeepVisible(WidgetConfig widget)
    {
        var monitor = GetTargetMonitor(widget.MonitorDeviceName);
        widget.MonitorDeviceName = monitor.DeviceName;

        widget.Bounds.Width = Math.Min(Math.Max(160, widget.Bounds.Width), monitor.Width);
        widget.Bounds.Height = Math.Min(Math.Max(120, widget.Bounds.Height), monitor.Height);
        widget.Bounds.X = Math.Clamp(widget.Bounds.X, monitor.X, monitor.X + monitor.Width - widget.Bounds.Width);
        widget.Bounds.Y = Math.Clamp(widget.Bounds.Y, monitor.Y, monitor.Y + monitor.Height - widget.Bounds.Height);
    }
}
