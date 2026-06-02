using System.Windows;
using DesktopTextBoard.Models;
using DesktopTextBoard.Services;
using DesktopTextBoard.Windows;

namespace DesktopTextBoard;

public partial class App : System.Windows.Application
{
    private BoardStore? _boardStore;
    private BoardDocument? _document;
    private DesktopWidgetManager? _widgetManager;
    private EditorWindow? _editorWindow;
    private TrayService? _trayService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _boardStore = new BoardStore();
        _document = _boardStore.LoadOrCreate();
        _widgetManager = new DesktopWidgetManager(_document, _boardStore);
        _widgetManager.ShowActiveBoard();

        _editorWindow = new EditorWindow(_document, _boardStore, _widgetManager);
        _trayService = new TrayService(
            showEditor: ToggleEditor,
            lockAll: () => _widgetManager.SetAllLocked(true),
            unlockAll: () => _widgetManager.SetAllLocked(false),
            importBackup: ImportBackup,
            exportBackup: ExportBackup,
            toggleStartup: ToggleStartup,
            exit: ExitApplication,
            isStartupEnabled: StartupService.IsEnabled);
    }

    private void ToggleEditor()
    {
        if (_editorWindow is null)
        {
            return;
        }

        if (_editorWindow.IsVisible)
        {
            _editorWindow.Hide();
            return;
        }

        _editorWindow.Show();
        _editorWindow.Activate();
    }

    private void ImportBackup()
    {
        if (_boardStore is null)
        {
            return;
        }

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Desktop Text Board backup (*.json)|*.json|All files (*.*)|*.*",
            Title = "Import Desktop Text Board backup"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _document = _boardStore.Import(dialog.FileName);
        RestartRuntimeObjects();
    }

    private void ExportBackup()
    {
        if (_boardStore is null || _document is null)
        {
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Desktop Text Board backup (*.json)|*.json",
            FileName = $"desktop-text-board-{DateTime.Now:yyyyMMdd-HHmm}.json",
            Title = "Export Desktop Text Board backup"
        };

        if (dialog.ShowDialog() == true)
        {
            _boardStore.Export(_document, dialog.FileName);
        }
    }

    private void ToggleStartup()
    {
        StartupService.SetEnabled(!StartupService.IsEnabled());
        _trayService?.RefreshStartupMenu();
    }

    private void RestartRuntimeObjects()
    {
        if (_document is null || _boardStore is null)
        {
            return;
        }

        _widgetManager?.CloseAll();
        _widgetManager = new DesktopWidgetManager(_document, _boardStore);
        _widgetManager.ShowActiveBoard();

        var wasVisible = _editorWindow?.IsVisible == true;
        _editorWindow?.ForceClose();
        _editorWindow = new EditorWindow(_document, _boardStore, _widgetManager);
        if (wasVisible)
        {
            _editorWindow.Show();
        }
    }

    private void ExitApplication()
    {
        _boardStore?.SaveNow(_document);
        _trayService?.Dispose();
        _widgetManager?.CloseAll();
        _editorWindow?.ForceClose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _boardStore?.SaveNow(_document);
        _trayService?.Dispose();
        base.OnExit(e);
    }
}
