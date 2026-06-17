using System.IO;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using MediaBrush = System.Windows.Media.Brush;

namespace DesktopTextBoard.Services;

public static class RichTextSerializer
{
    private const double CompactListLeftMargin = 0;
    private const double CompactListMarkerGap = 0;
    private const string HiddenDividerToken = "[[DTB_DIVIDER]]";
    private const char DividerGlyph = '\u2501';
    private const string LegacyDividerText = "\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500";
    private const string HeavyDividerText = "\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501";
    private const string VisibleDividerText = "\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501";
    private const double DividerFontSize = 7;
    private static readonly Color DividerColor = Color.FromArgb(210, 214, 221, 232);

    public static string Save(FlowDocument document)
    {
        ApplyDesktopLayout(document);
        var range = new TextRange(document.ContentStart, document.ContentEnd);
        using var stream = new MemoryStream();
        range.Save(stream, DataFormats.XamlPackage);
        return Convert.ToBase64String(stream.ToArray());
    }

    public static FlowDocument Load(string content, MediaBrush foreground, double fontSize)
    {
        var document = CreateEmpty(foreground, fontSize);
        if (string.IsNullOrWhiteSpace(content))
        {
            return document;
        }

        try
        {
            var bytes = Convert.FromBase64String(content);
            using var stream = new MemoryStream(bytes);
            var range = new TextRange(document.ContentStart, document.ContentEnd);
            range.Load(stream, DataFormats.XamlPackage);
        }
        catch
        {
            document = CreateEmpty(foreground, fontSize);
            document.Blocks.Clear();
            document.Blocks.Add(new Paragraph(new Run(content)));
        }

        ApplyDefaults(document, foreground, fontSize);
        return document;
    }

    public static FlowDocument Clone(FlowDocument document, MediaBrush foreground, double fontSize)
    {
        return Load(Save(document), foreground, fontSize);
    }

    public static string MergeCellContents(IEnumerable<string> contents, MediaBrush foreground, double fontSize)
    {
        var documents = contents
            .Select(content => Load(content, foreground, fontSize))
            .Where(HasVisibleContent)
            .ToList();

        if (documents.Count == 0)
        {
            return string.Empty;
        }

        var merged = CreateEmpty(foreground, fontSize);
        merged.Blocks.Clear();

        foreach (var document in documents)
        {
            if (merged.Blocks.Count > 0)
            {
                merged.Blocks.Add(CreateEmptyParagraph());
            }

            while (document.Blocks.FirstBlock is { } block)
            {
                document.Blocks.Remove(block);
                merged.Blocks.Add(block);
            }
        }

        return Save(merged);
    }

    public static void ApplyDesktopLayout(FlowDocument document)
    {
        NormalizeBlocks(document.Blocks);
    }

    public static void FitDividersToWidth(FlowDocument document, double availableWidth)
    {
        if (double.IsNaN(availableWidth) || double.IsInfinity(availableWidth) || availableWidth <= 0)
        {
            return;
        }

        var dividerText = CreateDividerText(availableWidth);
        FitDividersToWidth(document.Blocks, dividerText);
    }

    public static void ToggleCompactBullets(FlowDocument document, TextSelection selection)
    {
        InsertCompactBullets(document, selection);
    }

    public static void ToggleCompactNumbering(FlowDocument document, TextSelection selection)
    {
        InsertCompactNumbering(document, selection);
    }

    public static bool HandleCompactListEnter(TextSelection selection)
    {
        var paragraph = selection.Start.Paragraph;
        if (paragraph is null)
        {
            return false;
        }

        var text = new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text.TrimEnd('\r', '\n');
        if (text.StartsWith("• ", StringComparison.Ordinal))
        {
            return ContinueBulletList(selection, paragraph, text);
        }

        var numberPrefix = GetNumberPrefix(text);
        if (numberPrefix is not null)
        {
            return ContinueNumberedList(selection, paragraph, text, numberPrefix.Value.Number, numberPrefix.Value.PrefixLength);
        }

        return false;
    }

    public static bool HandleCompactDividerEnter(TextSelection selection)
    {
        var paragraph = selection.Start.Paragraph;
        if (paragraph is null)
        {
            return false;
        }

        var text = new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text.Trim();
        if (text != "---")
        {
            return false;
        }

        InsertDividerNear(selection, paragraph, replaceCurrent: true);
        return true;
    }

    public static void InsertCompactDivider(FlowDocument document, TextSelection selection)
    {
        var paragraph = selection.Start.Paragraph;
        if (paragraph is null)
        {
            var divider = CreateDividerBlock();
            var next = CreateEmptyParagraph();
            document.Blocks.Add(divider);
            document.Blocks.Add(next);
            MoveSelectionToParagraphEnd(selection, next);
            return;
        }

        InsertDividerNear(selection, paragraph, replaceCurrent: false);
    }

    private static FlowDocument CreateEmpty(MediaBrush foreground, double fontSize)
    {
        var document = new FlowDocument
        {
            PagePadding = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = foreground,
            FontSize = fontSize
        };
        document.Blocks.Add(new Paragraph());
        return document;
    }

    private static bool HasVisibleContent(FlowDocument document)
    {
        var text = new TextRange(document.ContentStart, document.ContentEnd)
            .Text
            .Trim();
        return text.Length > 0;
    }

    private static void ApplyDefaults(FlowDocument document, MediaBrush foreground, double fontSize)
    {
        document.PagePadding = new Thickness(0);
        document.Background = Brushes.Transparent;
        document.Foreground = foreground;
        document.FontSize = fontSize;
        ApplyDesktopLayout(document);
    }

    private static void NormalizeBlocks(BlockCollection blocks)
    {
        foreach (var block in blocks.ToList())
        {
            switch (block)
            {
                case Paragraph paragraph:
                    if (IsDividerParagraph(paragraph))
                    {
                        ApplyDividerStyle(paragraph, dividerText: null);
                    }
                    else
                    {
                        paragraph.Margin = new Thickness(0, 0, 0, 4);
                    }
                    break;
                case List list:
                    ReplaceListWithCompactParagraphs(blocks, list);
                    break;
                case BlockUIContainer container when IsLegacyDividerContainer(container):
                    blocks.InsertBefore(container, CreateDividerBlock());
                    blocks.Remove(container);
                    break;
                case BlockUIContainer container when IsVisualDividerContainer(container):
                    blocks.InsertBefore(container, CreateDividerBlock());
                    blocks.Remove(container);
                    break;
                case Section section:
                    section.Margin = new Thickness(0);
                    NormalizeBlocks(section.Blocks);
                    break;
            }
        }
    }

    private static void FitDividersToWidth(BlockCollection blocks, string dividerText)
    {
        foreach (var block in blocks.ToList())
        {
            switch (block)
            {
                case Paragraph paragraph when IsDividerParagraph(paragraph):
                    ApplyDividerStyle(paragraph, dividerText);
                    break;
                case Section section:
                    FitDividersToWidth(section.Blocks, dividerText);
                    break;
            }
        }
    }

    private static void ReplaceListWithCompactParagraphs(BlockCollection parentBlocks, List list)
    {
        var index = list.StartIndex <= 0 ? 1 : list.StartIndex;
        var useNumbers = list.MarkerStyle is TextMarkerStyle.Decimal
            or TextMarkerStyle.LowerLatin
            or TextMarkerStyle.UpperLatin
            or TextMarkerStyle.LowerRoman
            or TextMarkerStyle.UpperRoman;

        foreach (var item in list.ListItems.ToList())
        {
            var text = new TextRange(item.ContentStart, item.ContentEnd).Text.Trim();
            var prefix = useNumbers ? $"{index}. " : "• ";
            var paragraph = new Paragraph(new Run($"{prefix}{text}"))
            {
                Margin = new Thickness(0, 0, 0, 4)
            };
            parentBlocks.InsertBefore(list, paragraph);
            index++;
        }

        parentBlocks.Remove(list);
    }

    private static void InsertCompactBullets(FlowDocument document, TextSelection selection)
    {
        var paragraphs = GetSelectedParagraphs(document, selection);
        if (paragraphs.Count == 0)
        {
            document.Blocks.Add(new Paragraph(new Run("• ")));
            return;
        }

        foreach (var paragraph in paragraphs)
        {
            InsertCompactPrefix(paragraph, "• ");
        }
    }

    private static void InsertCompactNumbering(FlowDocument document, TextSelection selection)
    {
        var paragraphs = GetSelectedParagraphs(document, selection);
        if (paragraphs.Count == 0)
        {
            document.Blocks.Add(new Paragraph(new Run("1. ")));
            return;
        }

        var number = 1;
        foreach (var paragraph in paragraphs)
        {
            InsertCompactPrefix(paragraph, $"{number}. ");
            number++;
        }
    }

    private static List<Paragraph> GetSelectedParagraphs(FlowDocument document, TextSelection selection)
    {
        if (selection.IsEmpty)
        {
            return selection.Start.Paragraph is { } current && !IsDividerParagraph(current)
                ? new List<Paragraph> { current }
                : new List<Paragraph>();
        }

        var paragraphs = new List<Paragraph>();
        CollectSelectedParagraphs(document.Blocks, selection.Start, selection.End, paragraphs);

        if (paragraphs.Count == 0 && selection.Start.Paragraph is { } fallback && !IsDividerParagraph(fallback))
        {
            paragraphs.Add(fallback);
        }

        return paragraphs;
    }

    private static void CollectSelectedParagraphs(
        BlockCollection blocks,
        TextPointer selectionStart,
        TextPointer selectionEnd,
        List<Paragraph> paragraphs)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case Paragraph paragraph when !IsDividerParagraph(paragraph)
                    && paragraph.ContentEnd.CompareTo(selectionStart) > 0
                    && paragraph.ContentStart.CompareTo(selectionEnd) < 0:
                    paragraphs.Add(paragraph);
                    break;
                case Section section:
                    CollectSelectedParagraphs(section.Blocks, selectionStart, selectionEnd, paragraphs);
                    break;
            }
        }
    }

    private static void InsertCompactPrefix(Paragraph paragraph, string prefix)
    {
        var lineRange = new TextRange(paragraph.ContentStart, paragraph.ContentEnd);
        var text = lineRange.Text.TrimStart();
        if (text.StartsWith(prefix, StringComparison.Ordinal)
            || (prefix == "• " && text.StartsWith("• ", StringComparison.Ordinal))
            || (prefix != "• " && GetNumberPrefix(text) is not null))
        {
            return;
        }

        var prefixRun = new Run(prefix);
        if (paragraph.Inlines.FirstInline is null)
        {
            paragraph.Inlines.Add(prefixRun);
        }
        else
        {
            paragraph.Inlines.InsertBefore(paragraph.Inlines.FirstInline, prefixRun);
        }
        paragraph.Margin = new Thickness(0, 0, 0, 4);
    }

    private static void InsertDividerNear(TextSelection selection, Paragraph paragraph, bool replaceCurrent)
    {
        var divider = CreateDividerBlock();
        var next = CreateEmptyParagraph();

        switch (paragraph.Parent)
        {
            case FlowDocument document:
                if (replaceCurrent)
                {
                    document.Blocks.InsertBefore(paragraph, divider);
                    document.Blocks.InsertAfter(divider, next);
                    document.Blocks.Remove(paragraph);
                }
                else
                {
                    document.Blocks.InsertAfter(paragraph, divider);
                    document.Blocks.InsertAfter(divider, next);
                }
                break;
            case Section section:
                if (replaceCurrent)
                {
                    section.Blocks.InsertBefore(paragraph, divider);
                    section.Blocks.InsertAfter(divider, next);
                    section.Blocks.Remove(paragraph);
                }
                else
                {
                    section.Blocks.InsertAfter(paragraph, divider);
                    section.Blocks.InsertAfter(divider, next);
                }
                break;
            default:
                return;
        }

        MoveSelectionToParagraphEnd(selection, next);
    }

    private static Paragraph CreateDividerBlock()
    {
        var paragraph = new Paragraph(new Run(VisibleDividerText));
        ApplyDividerStyle(paragraph, dividerText: null);
        return paragraph;
    }

    private static Paragraph CreateEmptyParagraph()
    {
        return new Paragraph
        {
            Margin = new Thickness(0, 0, 0, 4)
        };
    }

    private static bool IsDividerParagraph(Paragraph paragraph)
    {
        var text = new TextRange(paragraph.ContentStart, paragraph.ContentEnd)
            .Text
            .Trim();
        return text == HiddenDividerToken
            || text == LegacyDividerText
            || text == HeavyDividerText
            || text == VisibleDividerText
            || IsDividerText(text);
    }

    private static bool IsLegacyDividerContainer(BlockUIContainer container)
    {
        return container.Child is Border border
            && border.Height <= 2
            && border.Margin.Top <= 3
            && border.Margin.Bottom <= 3;
    }

    private static bool IsVisualDividerContainer(BlockUIContainer container)
    {
        return container.Child is FrameworkElement element
            && element.Tag is string tag
            && tag == HiddenDividerToken;
    }

    private static void ApplyDividerStyle(Paragraph paragraph, string? dividerText)
    {
        var text = new TextRange(paragraph.ContentStart, paragraph.ContentEnd)
            .Text
            .Trim();
        var normalizedText = dividerText ?? (IsDividerText(text) ? text : VisibleDividerText);
        if (text != normalizedText)
        {
            paragraph.Inlines.Clear();
            paragraph.Inlines.Add(new Run(normalizedText));
        }

        SetIfDifferent(paragraph, Block.MarginProperty, new Thickness(0, 1, 0, 2));
        SetIfDifferent(paragraph, Block.PaddingProperty, new Thickness(0));
        SetIfDifferent(paragraph, TextElement.FontSizeProperty, DividerFontSize);
        SetIfDifferent(paragraph, Block.LineHeightProperty, DividerFontSize);
        if (!IsSolidColor(paragraph.Foreground, DividerColor))
        {
            paragraph.Foreground = CreateFrozenBrush(DividerColor);
        }
        SetIfDifferent(paragraph, Block.BorderThicknessProperty, new Thickness(0));
        if (!IsSolidColor(paragraph.BorderBrush, Colors.Transparent))
        {
            paragraph.BorderBrush = Brushes.Transparent;
        }
    }

    private static void SetIfDifferent<T>(DependencyObject target, DependencyProperty property, T value)
    {
        if (!Equals(target.GetValue(property), value))
        {
            target.SetValue(property, value);
        }
    }

    private static SolidColorBrush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static bool IsSolidColor(MediaBrush brush, Color color)
    {
        return brush is SolidColorBrush solid
            && solid.Color == color
            && Math.Abs(solid.Opacity - 1) < 0.001;
    }

    private static bool IsDividerText(string text)
    {
        return text.Length >= 3
            && text.All(ch => ch is DividerGlyph or '\u2500' or '-');
    }

    private static string CreateDividerText(double availableWidth)
    {
        var targetWidth = Math.Max(24, availableWidth - 42);
        var glyphWidth = MeasureDividerGlyph();
        var count = (int)Math.Floor(targetWidth / Math.Max(1, glyphWidth));
        count = Math.Clamp(count, 8, 160);
        return new string(DividerGlyph, count);
    }

    private static double MeasureDividerGlyph()
    {
        var typeface = new Typeface(
            new FontFamily("Segoe UI"),
            FontStyles.Normal,
            FontWeights.Normal,
            FontStretches.Normal);
        var formatted = new FormattedText(
            DividerGlyph.ToString(),
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            typeface,
            DividerFontSize,
            Brushes.White,
            1);
        return formatted.WidthIncludingTrailingWhitespace;
    }

    private static void MoveSelectionToParagraphEnd(TextSelection selection, Paragraph paragraph)
    {
        var caret = paragraph.ContentEnd.GetInsertionPosition(LogicalDirection.Backward);
        if (caret is not null)
        {
            selection.Select(caret, caret);
        }
        paragraph.BringIntoView();
    }

    private static bool ContinueBulletList(TextSelection selection, Paragraph paragraph, string text)
    {
        if (text.Trim() == "•")
        {
            paragraph.Inlines.Clear();
            return true;
        }

        InsertParagraphAfter(selection, paragraph, "• ");
        return true;
    }

    private static bool ContinueNumberedList(TextSelection selection, Paragraph paragraph, string text, int number, int prefixLength)
    {
        if (text[prefixLength..].Trim().Length == 0)
        {
            paragraph.Inlines.Clear();
            return true;
        }

        InsertParagraphAfter(selection, paragraph, $"{number + 1}. ");
        return true;
    }

    private static void InsertParagraphAfter(TextSelection selection, Paragraph current, string text)
    {
        var next = new Paragraph(new Run(text))
        {
            Margin = new Thickness(0, 0, 0, 4)
        };

        switch (current.Parent)
        {
            case FlowDocument document:
                document.Blocks.InsertAfter(current, next);
                break;
            case Section section:
                section.Blocks.InsertAfter(current, next);
                break;
            default:
                return;
        }

        MoveSelectionToParagraphEnd(selection, next);
    }

    private static (int Number, int PrefixLength)? GetNumberPrefix(string text)
    {
        var dot = text.IndexOf('.');
        if (dot <= 0 || dot > 4)
        {
            return null;
        }

        var numberText = text[..dot];
        if (!numberText.All(char.IsDigit) || !int.TryParse(numberText, out var number))
        {
            return null;
        }

        var prefixLength = dot + 1;
        if (text.Length > prefixLength && text[prefixLength] == ' ')
        {
            prefixLength++;
        }

        return (number, prefixLength);
    }
}
