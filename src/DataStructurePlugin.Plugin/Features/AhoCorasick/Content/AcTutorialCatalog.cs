using System.Collections.Immutable;
using Markdig;
using Markdig.Syntax;

namespace DataStructurePlugin.Features.AhoCorasick.Content;

public sealed record AcTutorialChapter(string Title, string Markdown);

/// <summary>只读教材来自唯一的 Markdown 嵌入资源；目录按真正的二级标题生成，代码围栏不参与分章。</summary>
public static class AcTutorialCatalog
{
    public static MarkdownPipeline Pipeline { get; } = new MarkdownPipelineBuilder().UsePipeTables().DisableHtml().Build();
    private static readonly Lazy<ImmutableArray<AcTutorialChapter>> Chapters = new(Load);
    public static ImmutableArray<AcTutorialChapter> All => Chapters.Value;

    private static ImmutableArray<AcTutorialChapter> Load()
    {
        using var stream = typeof(AcTutorialCatalog).Assembly.GetManifestResourceStream("DataStructurePlugin.AhoCorasick.Tutorial.md")
            ?? throw new InvalidOperationException("AC 完整教程资源缺失。");
        using var reader = new StreamReader(stream);
        var markdown = reader.ReadToEnd();
        var headings = Markdown.Parse(markdown, Pipeline).OfType<HeadingBlock>().Where(h => h.Level == 2).ToArray();
        if (headings.Length == 0) throw new InvalidOperationException("AC 完整教程必须包含二级章节标题。");
        return headings.Select((heading, index) => new AcTutorialChapter(
            markdown[heading.Span.Start..(heading.Span.End + 1)].Trim().TrimStart('#').Trim(),
            markdown[heading.Span.Start..(index + 1 < headings.Length ? headings[index + 1].Span.Start : markdown.Length)].Trim()))
            .ToImmutableArray();
    }
}
