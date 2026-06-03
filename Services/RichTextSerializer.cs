using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using MediaBrush = System.Windows.Media.Brush;

namespace DesktopTextBoard.Services;

public static class RichTextSerializer
{
    private const double CompactListLeftMargin = 0;
    private const double CompactListMarkerGap = 0;

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

    public static void ApplyDesktopLayout(FlowDocument document)
    {
        NormalizeBlocks(document.Blocks);
    }

    public static void ToggleCompactBullets(FlowDocument document, TextSelection selection)
    {
        InsertCompactPrefix(document, selection, "• ");
    }

    public static void ToggleCompactNumbering(FlowDocument document, TextSelection selection)
    {
        InsertCompactPrefix(document, selection, "1. ");
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
                    paragraph.Margin = new Thickness(0, 0, 0, 4);
                    break;
                case List list:
                    ReplaceListWithCompactParagraphs(blocks, list);
                    break;
                case Section section:
                    section.Margin = new Thickness(0);
                    NormalizeBlocks(section.Blocks);
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

    private static void InsertCompactPrefix(FlowDocument document, TextSelection selection, string prefix)
    {
        var paragraph = selection.Start.Paragraph;
        if (paragraph is null)
        {
            document.Blocks.Add(new Paragraph(new Run(prefix)));
            return;
        }

        var lineRange = new TextRange(paragraph.ContentStart, paragraph.ContentEnd);
        var text = lineRange.Text.TrimStart();
        if (text.StartsWith(prefix, StringComparison.Ordinal))
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

        var caret = next.ContentEnd.GetInsertionPosition(LogicalDirection.Backward);
        if (caret is not null)
        {
            selection.Select(caret, caret);
        }
        next.BringIntoView();
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
