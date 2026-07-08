# 阿卡夏便笺 Akasha Notes Design

## 1. Product Goal

Build a lightweight Windows desktop text-board utility that displays transparent, customizable text widgets on the desktop.

The tool is not a task manager, reminder app, calendar, or planner. Its core purpose is to provide a calm desktop writing surface for formatted text, while keeping the desktop widget visually integrated with the wallpaper and icons.

## 2. Core Concept

The application has three main parts:

1. A tray-resident background process.
2. One or more read-only transparent desktop widgets.
3. A normal editor window for writing and formatting text.

The desktop widget only displays content. Text editing happens in the editor window.

Closing the editor window hides it but does not exit the application. The app remains running in the system tray.

## 3. Confirmed First-Version Scope

### 3.1 Desktop Widgets

- A board configuration can contain multiple desktop widgets.
- A new board starts with one widget by default.
- Each widget can be positioned and resized independently.
- Each widget can use either:
  - Single large text area mode.
  - Multi-cell grid mode.
- Grid mode supports custom row and column counts.
- Grid row and column proportions can be adjusted in the editor.
- The first version does not support merged cells.

### 3.2 Shape And Appearance

First-version shapes:

- Rectangle.
- Rounded rectangle.

First-version appearance controls:

- Background color.
- Background opacity.
- Border color.
- Border opacity.
- Border thickness.
- Corner radius.
- Inner padding.
- Default text color.
- Default font size.

The first version does not support arbitrary hand-drawn shapes, polygon masks, ellipse widgets, or image masks.

### 3.3 Rich Text

The editor supports basic WPF RichTextBox formatting:

- Bold.
- Italic.
- Underline.
- Strikethrough.
- Font size.
- Text color.
- Highlight color.
- Bullet lists.
- Numbered lists.
- Alignment.
- Undo and redo inside the editor.

The first version does not support:

- Images.
- Tables.
- Attachments.
- Calendar logic.
- Task reminders.
- Completion statistics.
- Complex Notion-style block editing.

### 3.4 Desktop Layering

Desktop widgets should:

- Display above desktop icons.
- Be coverable by normal application windows.
- Not stay above all other windows.
- Not appear in the taskbar.
- Not appear in Alt+Tab.

In locked mode:

- The widget body is click-through.
- It cannot be dragged, resized, selected, or scrolled.
- Only the small lock button remains clickable.

In unlocked mode:

- The widget can be moved.
- The widget can be resized.
- A subtle border and four corner resize handles are shown.
- Text is still not editable directly on the desktop.
- Grid proportions are not adjusted on the desktop; they are adjusted in the editor.

### 3.5 Lock Button

Each desktop widget has a small lock button.

Behavior:

- The button toggles full widget interaction lock.
- Locked means the entire widget body becomes non-interactive and click-through.
- The lock button is the only clickable region in locked mode.
- The editor is not opened from the desktop widget.
- The editor is opened only from the tray icon.
- The tray menu also includes lock/unlock actions as a fallback.

### 3.6 Editor Window

The editor window uses a canvas plus sidebar layout.

Structure:

- Left area or sidebar: widget list.
- Center: WYSIWYG canvas matching the desktop widget layout.
- Right sidebar: settings for the currently selected widget.

Sidebar controls:

- Widget mode: single text area or grid.
- Row and column count.
- Background opacity.
- Border opacity.
- Corner radius.
- Default font settings.
- Current monitor.
- Lock state.
- Appearance preset selection.

Cell-level settings are handled through canvas right-click menus or small popups, not by overloading the main sidebar.

The rich text toolbar appears in the editor window, not on the desktop widget.

### 3.7 Content Overflow

Desktop widgets do not show scrollbars.

If content exceeds a cell's visible region:

- The desktop widget clips the overflow.
- A fade-out effect may be added if it remains visually clean.
- Full content remains visible and editable in the editor.

Font size is adjusted manually in the editor.

### 3.8 Data Storage

The app supports:

- Multiple board configurations.
- Auto-save.
- Import backup.
- Export backup.

Data should include:

- Board list.
- Widget list per board.
- Widget positions and sizes.
- Monitor assignment.
- Grid layout.
- Rich text content.
- Appearance settings.
- Lock state.

Recommended first-version storage:

- Local JSON project file.
- Rich text stored as XAML package fragments or another WPF-compatible serialized format.

SQLite is not needed for the first version.

### 3.9 Save And Preview Behavior

- Editor changes update the desktop widget in real time or with a short debounce.
- Auto-save occurs after a short delay, for example 1 second after editing stops.
- Closing the editor forces a save.
- Switching boards forces a save.
- Exiting from the tray forces a save.
- No history/version system in the first version.

### 3.10 Multi-Monitor Support

The first version supports:

- Dragging widgets to any monitor.
- Saving monitor, position, and size.
- Restoring widgets to the previous monitor on startup.
- Moving a widget back to the primary monitor if its saved monitor is unavailable.
- A switch-monitor button in the editor/main program.

The first version does not need advanced cross-monitor layout management.

### 3.11 Startup And Tray Behavior

- Program startup shows the desktop widgets.
- Tray icon is always available while the app is running.
- Tray left-click opens or hides the editor.
- Tray right-click menu includes:
  - Open editor.
  - Lock/unlock widgets.
  - Switch board.
  - Import backup.
  - Export backup.
  - Settings.
  - Exit.
- Closing the editor hides it.
- Exiting from the tray fully exits the application.
- Start on Windows login is optional and disabled by default.

### 3.12 Appearance Presets

The first version includes a small set of presets:

- Dark translucent.
- Light translucent.
- Paper-like light.
- Minimal borderless.
- High contrast text.

Presets are starting points. Users can still manually adjust appearance.

## 4. Technical Direction

### 4.1 Chosen Stack

Use:

- C#.
- WPF.
- Native WPF RichTextBox.
- Native Windows tray integration.

Avoid:

- Electron.
- WebView2 for the first version.

Reasoning:

- Lower memory usage.
- Better native transparent-window behavior.
- Stronger Windows desktop integration.
- The required rich text feature set is within WPF RichTextBox's practical range.

### 4.2 Main Components

Proposed components:

- `AppHost`
  - Owns startup, shutdown, tray icon, settings, and board loading.
- `BoardStore`
  - Loads, saves, imports, and exports board data.
- `DesktopWidgetWindow`
  - Transparent, borderless display window for one widget.
  - Handles lock state, click-through behavior, move, resize, and rendering.
- `EditorWindow`
  - Main editing interface with canvas and sidebar.
- `WidgetCanvas`
  - WYSIWYG representation of selected widgets.
- `RichTextCellEditor`
  - Wraps WPF RichTextBox formatting commands.
- `MonitorService`
  - Enumerates monitors and restores widget placement.
- `AppearancePresetService`
  - Provides and applies visual presets.

## 5. Data Model Draft

```json
{
  "version": 1,
  "activeBoardId": "board-default",
  "boards": [
    {
      "id": "board-default",
      "name": "Default",
      "widgets": [
        {
          "id": "widget-1",
          "name": "阿卡夏便笺",
          "mode": "grid",
          "isLocked": true,
          "monitorId": "primary",
          "bounds": {
            "x": 1245,
            "y": 8,
            "width": 425,
            "height": 680
          },
          "appearance": {
            "backgroundColor": "#181A1F",
            "backgroundOpacity": 0.72,
            "borderColor": "#FFFFFF",
            "borderOpacity": 0.08,
            "borderThickness": 1,
            "cornerRadius": 6,
            "padding": 16,
            "defaultTextColor": "#F2F2F2",
            "defaultFontSize": 16
          },
          "grid": {
            "rows": 2,
            "columns": 1,
            "rowWeights": [0.55, 0.45],
            "columnWeights": [1.0]
          },
          "cells": [
            {
              "id": "cell-1",
              "row": 0,
              "column": 0,
              "contentFormat": "wpf-xaml",
              "content": ""
            },
            {
              "id": "cell-2",
              "row": 1,
              "column": 0,
              "contentFormat": "wpf-xaml",
              "content": ""
            }
          ]
        }
      ]
    }
  ]
}
```

## 6. MVP Build Plan

### Phase 1: Shell And Tray

- Create WPF app.
- Add tray icon.
- Implement editor show/hide.
- Implement tray exit.
- Make editor close button hide instead of exit.

### Phase 2: Transparent Desktop Widget

- Create borderless transparent widget window.
- Remove taskbar and Alt+Tab presence.
- Place above desktop icons but below normal windows.
- Implement locked and unlocked state.
- Add click-through body in locked mode.
- Add lock button as the only clickable locked-region control.

### Phase 3: Basic Editor

- Add canvas plus sidebar layout.
- Add WPF RichTextBox editor.
- Add formatting toolbar.
- Bind editor content to desktop widget display.
- Add single text area mode.

### Phase 4: Grid Mode

- Add row and column settings.
- Add grid rendering.
- Add independent RichTextBox content per cell.
- Add row and column proportion adjustment in editor.
- Add desktop display clipping.

### Phase 5: Persistence

- Add JSON board storage.
- Add auto-save debounce.
- Add import/export backup.
- Save and restore widget bounds and monitor.

### Phase 6: Appearance And Monitors

- Add appearance presets.
- Add opacity, border, radius, padding controls.
- Add switch-monitor button.
- Restore widgets safely when monitors change.

## 7. Risks And Open Technical Questions

### 7.1 Click-Through With One Clickable Lock Button

The app must make the widget body click-through while keeping the lock button clickable.

Likely implementation:

- Use layered window styles for click-through on the body.
- Split the lock button into a small separate overlay window, or dynamically control hit testing.

This needs early prototyping.

### 7.2 Desktop Layer Placement

The widget should sit above desktop icons but below normal application windows.

This is less invasive than embedding into the wallpaper/WorkerW layer, but still needs testing to confirm stable behavior across Windows versions.

### 7.3 Rich Text Serialization

WPF RichTextBox content can be serialized, but the final format should be chosen carefully.

Options:

- XAML package fragments.
- RTF fragments.
- Plain XAML text ranges.

The first prototype should test reliable save/load for colors, highlights, lists, and text decoration.

### 7.4 Memory Footprint

Pure WPF should be lighter than Electron or WebView2, but multiple RichTextBox instances may still add overhead.

The desktop display should avoid using editable RichTextBox controls when locked or display-only if a lighter read-only renderer is practical.

## 8. Explicit Non-Goals For Version 1

- No task scheduling.
- No reminders.
- No calendar.
- No image insertion.
- No tables.
- No attachments.
- No arbitrary hand-drawn shapes.
- No merged grid cells.
- No Markdown mode.
- No web sync.
- No complex history/version system.
- No always-on-top behavior over normal windows.

## 9. Recommended Next Step

Build a small WPF proof of concept before implementing the full app.

The proof of concept should validate:

- Transparent widget window.
- Correct desktop layering.
- Click-through locked body.
- Clickable lock button.
- Move and resize in unlocked mode.
- Basic RichTextBox save/load.
- Real-time editor-to-widget preview.

Only after those are confirmed should the full board/config/editor architecture be built.
