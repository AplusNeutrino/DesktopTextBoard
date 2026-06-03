using System.IO;
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
    private const string LegacyDividerText = "\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500";
    private const string HeavyDividerText = "\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501\u2501";
    private const string VisibleDividerText = "------------------------";

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
                        ApplyDividerStyle(paragraph);
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
        var paragraph = new Paragraph(new Run(VisibleDividerText))
        {
            Margin = new Thickness(0, 1, 0, 2),
            Padding = new Thickness(0),
            FontSize = 8,
            LineHeight = 8,
            Foreground = new SolidColorBrush(Color.FromRgb(190, 196, 205))
        };
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
            || text == VisibleDividerText;
    }

    private static bool IsLegacyDividerContainer(BlockUIContainer container)
    {
        return container.Child is Border border
            && border.Height <= 2
            && border.Margin.Top <= 3
            && border.Margin.Bottom <= 3;
    }

    private static void ApplyDividerStyle(Paragraph paragraph)
    {
        paragraph.Inlines.Clear();
        paragraph.Inlines.Add(new Run(VisibleDividerText));
        paragraph.Margin = new Thickness(0, 1, 0, 2);
        paragraph.Padding = new Thickness(0);
        paragraph.FontSize = 8;
        paragraph.LineHeight = 8;
        paragraph.Foreground = new SolidColorBrush(Color.FromRgb(190, 196, 205));
        paragraph.BorderThickness = new Thickness(0);
        paragraph.BorderBrush = Brushes.Transparent;
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
