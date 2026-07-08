# 阿卡夏便笺 Akasha Notes

阿卡夏便笺 Akasha Notes is a lightweight Windows WPF utility for transparent desktop text widgets.

## Current First-Version Features

- Tray-resident background app.
- Transparent desktop widgets.
- Lock button on each widget.
- Locked widget body is click-through.
- Unlocked widgets can be moved and resized.
- Editor window opens from the tray icon.
- Multiple widgets per board.
- Single-text mode and grid mode.
- Rich text editing with WPF RichTextBox.
- Bold, italic, underline, strikethrough, colors, highlight, bullets, numbering, alignment, and font size.
- Appearance controls for opacity, border, radius, padding, default color, and font size.
- Basic appearance presets.
- Multi-monitor placement and switch-monitor action.
- JSON auto-save under `%LOCALAPPDATA%\AkashaNotes\boards.json`.
- Import and export backup from the tray menu.
- Optional start with Windows toggle.

## Build

This project targets .NET 8 Windows Desktop:

```powershell
dotnet build .\DesktopTextBoard.csproj
```

The machine needs the .NET SDK, not just the .NET runtime.

## Run

```powershell
dotnet run --project .\DesktopTextBoard.csproj
```

After launch, use the tray icon to open the editor.

## Notes

The desktop widget is intentionally display-only. Text is edited in the editor window, then reflected onto the transparent desktop widget.

The lock button is implemented as a small companion window so the widget body can be click-through while the lock control remains clickable.
