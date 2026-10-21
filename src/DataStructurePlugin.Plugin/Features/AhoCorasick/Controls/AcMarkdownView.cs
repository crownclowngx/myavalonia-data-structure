using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using DataStructurePlugin.Features.AhoCorasick.Content;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace DataStructurePlugin.Features.AhoCorasick.Controls;

/// <summary>
/// 本域教材的原生 Markdown 预览。Markdig 负责语法树；此处只将用到的 CommonMark 与表格节点投影为控件。
/// 不运行 HTML、脚本或远程资源；正文、公式与代码均可选择复制，外链仅在用户点击时打开。
/// </summary>
public sealed class AcMarkdownView : ContentControl
{
    public static readonly StyledProperty<string> MarkdownProperty = AvaloniaProperty.Register<AcMarkdownView, string>(nameof(Markdown), "");
    private static readonly FontFamily CodeFont = new("Cascadia Code, Consolas, Microsoft YaHei UI");

    public string Markdown { get => GetValue(MarkdownProperty); set => SetValue(MarkdownProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MarkdownProperty)
            Content = RenderBlocks(Markdig.Markdown.Parse(Markdown ?? "", AcTutorialCatalog.Pipeline));
    }

    private static StackPanel RenderBlocks(ContainerBlock blocks)
    {
        var panel = new StackPanel { Spacing = 15, HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var block in blocks) panel.Children.Add(RenderBlock(block));
        return panel;
    }

    private static Control RenderBlock(Block block)
    {
        switch (block)
        {
            case HeadingBlock heading:
                var title = Text(heading.Inline);
                title.FontSize = heading.Level <= 2 ? 27 : heading.Level == 3 ? 21 : 17;
                title.FontWeight = FontWeight.SemiBold;
                title.Margin = new Thickness(0, 14, 0, 3);
                return title;
            case ParagraphBlock paragraph:
                return Text(paragraph.Inline);
            case CodeBlock code:
                if (code is FencedCodeBlock { Info: "mermaid" }) return new AcMermaidDiagram(code.Lines.ToString());
                var codeText = new SelectableTextBlock { Text = code.Lines.ToString(), FontFamily = CodeFont, FontSize = 13, LineHeight = 23 };
                var codePanel = new StackPanel { Spacing = 8 };
                if (code is FencedCodeBlock { Info: { Length: > 0 } } fenced)
                    codePanel.Children.Add(new TextBlock { Text = fenced.Info, FontSize = 11, Opacity = 0.7 });
                codePanel.Children.Add(new ScrollViewer { Content = codeText, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto });
                return new Border { Classes = { "md-code" }, Child = codePanel };
            case QuoteBlock quote:
                return new Border { Classes = { "md-quote" }, Child = RenderBlocks(quote) };
            case ListBlock list:
                var items = new StackPanel { Spacing = 9 };
                var number = int.TryParse(list.OrderedStart, out var start) ? start : 1;
                foreach (var item in list.OfType<ListItemBlock>())
                {
                    var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 10 };
                    row.Children.Add(new TextBlock { Text = list.IsOrdered ? $"{number++}." : "•", MinWidth = 18, FontSize = 15, Margin = new Thickness(0, 3, 0, 0) });
                    var body = RenderBlocks(item);
                    Grid.SetColumn(body, 1);
                    row.Children.Add(body);
                    items.Children.Add(row);
                }
                return items;
            case Table table:
                return RenderTable(table);
            case ThematicBreakBlock:
                return new Border { Classes = { "md-rule" }, Height = 1 };
            case ContainerBlock container:
                return RenderBlocks(container);
            default:
                throw new NotSupportedException($"AC 教材尚未支持 Markdown 块：{block.GetType().Name}。");
        }
    }

    private static Control RenderTable(Table table)
    {
        var grid = new Grid { MinWidth = Math.Max(560, table.ColumnDefinitions.Count * 135) };
        foreach (var _ in table.ColumnDefinitions) grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        var rowIndex = 0;
        foreach (var row in table.OfType<TableRow>())
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var columnIndex = 0;
            foreach (var cell in row.OfType<TableCell>())
            {
                var border = new Border { Classes = { "md-cell" }, Child = RenderBlocks(cell) };
                if (row.IsHeader) border.Classes.Add("md-header");
                Grid.SetRow(border, rowIndex);
                // PipeTable 的 ColumnIndex 保持 -1；本预览按实际单元格顺序排布。
                Grid.SetColumn(border, columnIndex);
                Grid.SetColumnSpan(border, Math.Max(1, cell.ColumnSpan));
                columnIndex += Math.Max(1, cell.ColumnSpan);
                grid.Children.Add(border);
            }
            rowIndex++;
        }
        return new ScrollViewer { Content = grid, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
    }

    private static SelectableTextBlock Text(ContainerInline? inline)
    {
        var text = new SelectableTextBlock { FontSize = 15, LineHeight = 28, TextWrapping = TextWrapping.Wrap };
        if (inline is not null) RenderInlines(inline, text.Inlines!);
        return text;
    }

    private static void RenderInlines(ContainerInline container, InlineCollection target)
    {
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    target.Add(new Run(literal.Content.ToString()));
                    break;
                case CodeInline code:
                    target.Add(new Run(code.Content) { FontFamily = CodeFont });
                    break;
                case EmphasisInline emphasis:
                    var span = new Span();
                    if (emphasis.DelimiterCount >= 2) span.FontWeight = FontWeight.Bold;
                    else span.FontStyle = FontStyle.Italic;
                    RenderInlines(emphasis, span.Inlines);
                    target.Add(span);
                    break;
                case LineBreakInline line:
                    target.Add(new Run(line.IsHard ? "\n" : " "));
                    break;
                case LinkInline link:
                    var label = new TextBlock { TextWrapping = TextWrapping.Wrap, Inlines = new InlineCollection() };
                    RenderInlines(link, label.Inlines);
                    var button = new HyperlinkButton { Content = label, Padding = new Thickness(0), Classes = { "md-link" } };
                    if (!link.IsImage && Uri.TryCreate(link.Url, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http")
                        button.NavigateUri = uri;
                    target.Add(new InlineUIContainer(button));
                    break;
                case ContainerInline child:
                    RenderInlines(child, target);
                    break;
                default:
                    throw new NotSupportedException($"AC 教材尚未支持 Markdown 行内元素：{inline.GetType().Name}。");
            }
        }
    }
}
