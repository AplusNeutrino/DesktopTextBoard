using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace DesktopTextBoard.Services;

public sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _startupItem;
    private readonly Func<bool> _isStartupEnabled;

    public TrayService(
        Action showEditor,
        Action lockAll,
        Action unlockAll,
        Action importBackup,
        Action exportBackup,
        Action toggleStartup,
        Action exit,
        Func<bool> isStartupEnabled)
    {
        _isStartupEnabled = isStartupEnabled;
        _startupItem = new ToolStripMenuItem("Start with Windows", null, (_, _) => toggleStartup())
        {
            CheckOnClick = false
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open editor", null, (_, _) => showEditor());
        menu.Items.Add("Lock all widgets", null, (_, _) => lockAll());
        menu.Items.Add("Unlock all widgets", null, (_, _) => unlockAll());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Import backup...", null, (_, _) => importBackup());
        menu.Items.Add("Export backup...", null, (_, _) => exportBackup());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => exit());

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadAppIcon(),
            Text = AppInfo.DisplayName,
            Visible = true,
            ContextMenuStrip = menu
        };
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                showEditor();
            }
        };
        RefreshStartupMenu();
    }

    public void RefreshStartupMenu()
    {
        _startupItem.Checked = _isStartupEnabled();
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }

    private static Icon LoadAppIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AkashaNotes.ico");
        if (File.Exists(iconPath))
        {
            return new Icon(iconPath);
        }

        return !string.IsNullOrWhiteSpace(Environment.ProcessPath)
            ? Icon.ExtractAssociatedIcon(Environment.ProcessPath) ?? SystemIcons.Application
            : SystemIcons.Application;
    }
}
