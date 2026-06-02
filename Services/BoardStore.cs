using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using DesktopTextBoard.Models;

namespace DesktopTextBoard.Services;

public sealed class BoardStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly DispatcherTimer _saveTimer;
    private BoardDocument? _pendingDocument;

    public BoardStore()
    {
        Directory.CreateDirectory(AppDirectory);
        _saveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            SaveNow(_pendingDocument);
        };
    }

    public string AppDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopTextBoard");

    public string DocumentPath => Path.Combine(AppDirectory, "boards.json");

    public BoardDocument LoadOrCreate()
    {
        if (!File.Exists(DocumentPath))
        {
            var created = BoardDocument.CreateDefault();
            SaveNow(created);
            return created;
        }

        try
        {
            using var stream = File.OpenRead(DocumentPath);
            var document = JsonSerializer.Deserialize<BoardDocument>(stream, JsonOptions) ?? BoardDocument.CreateDefault();
            Normalize(document);
            return document;
        }
        catch
        {
            var backupPath = Path.Combine(AppDirectory, $"boards-error-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            File.Copy(DocumentPath, backupPath, overwrite: true);
            var created = BoardDocument.CreateDefault();
            SaveNow(created);
            return created;
        }
    }

    public void SaveSoon(BoardDocument? document)
    {
        if (document is null)
        {
            return;
        }

        _pendingDocument = document;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    public void SaveNow(BoardDocument? document)
    {
        if (document is null)
        {
            return;
        }

        Normalize(document);
        Directory.CreateDirectory(AppDirectory);
        var tempPath = $"{DocumentPath}.tmp";
        using (var stream = File.Create(tempPath))
        {
            JsonSerializer.Serialize(stream, document, JsonOptions);
        }

        File.Copy(tempPath, DocumentPath, overwrite: true);
        File.Delete(tempPath);
    }

    public BoardDocument Import(string sourcePath)
    {
        using var stream = File.OpenRead(sourcePath);
        var imported = JsonSerializer.Deserialize<BoardDocument>(stream, JsonOptions) ?? BoardDocument.CreateDefault();
        Normalize(imported);
        SaveNow(imported);
        return imported;
    }

    public void Export(BoardDocument document, string targetPath)
    {
        Normalize(document);
        using var stream = File.Create(targetPath);
        JsonSerializer.Serialize(stream, document, JsonOptions);
    }

    private static void Normalize(BoardDocument document)
    {
        if (document.Boards.Count == 0)
        {
            document.Boards.Add(BoardConfig.CreateDefault());
        }

        if (!document.Boards.Any(x => x.Id == document.ActiveBoardId))
        {
            document.ActiveBoardId = document.Boards[0].Id;
        }

        foreach (var board in document.Boards)
        {
            if (board.Widgets.Count == 0)
            {
                board.Widgets.Add(WidgetConfig.CreateDefault());
            }

            foreach (var widget in board.Widgets)
            {
                widget.EnsureCells();
                widget.Bounds.Width = Math.Max(160, widget.Bounds.Width);
                widget.Bounds.Height = Math.Max(120, widget.Bounds.Height);
            }
        }
    }
}
