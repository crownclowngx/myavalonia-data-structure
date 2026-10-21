using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DataStructurePlugin.Features.AhoCorasick;
using DataStructurePlugin.Features.AhoCorasick.Content;
using DataStructurePlugin.Features.AhoCorasick.Controls;
using Markdig;
using Markdig.Syntax;
using Xunit;

namespace DataStructurePlugin.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AcUiCollection
{
    public const string Name = "AC classroom UI";
}

[Collection(AcUiCollection.Name)]
public sealed class AcTutorialTests
{
    [Fact]
    public void 每个Mermaid源码都对应内置的深浅主题图且章节完整()
    {
        var chapters = AcTutorialCatalog.All;
        Assert.Equal(16, chapters.Length);
        var diagrams = chapters.SelectMany(chapter => Markdown.Parse(chapter.Markdown, AcTutorialCatalog.Pipeline).Descendants<FencedCodeBlock>())
            .Where(block => block.Info == "mermaid").ToArray();
        Assert.Equal(8, diagrams.Length);
        foreach (var diagram in diagrams)
        {
            var hash = AcMermaidDiagram.SourceHash(diagram.Lines.ToString());
            foreach (var theme in new[] { "light", "dark" })
            {
                using var stream = typeof(AcTutorialCatalog).Assembly.GetManifestResourceStream($"DataStructurePlugin.AhoCorasick.Diagrams.{hash}.{theme}.png");
                Assert.NotNull(stream);
                Assert.True(stream.Length > 1000);
            }
        }
        Assert.Contains("状态不变量", chapters[9].Markdown);
        Assert.Contains("public sealed class TutorialAc", chapters[11].Markdown);
    }

    [Fact]
    public void 教程暂停播放但保留实验草稿章节答案与各Document的独立选择()
    {
        var clock = new TestPlaybackClock();
        using var first = new AhoCorasickDocument(new Features.AhoCorasick.Domain.AhoCorasickTraceBuilder(), clock);
        using var second = TestDocuments.Create();
        first.ExploreModeCommand.Execute(null);
        first.GoSearchCommand.Execute(null);
        first.NextEventCommand.Execute(null);
        first.SelectedLesson = first.Lessons[2];
        first.AnswerIndex = 1;
        first.KeywordText = "尚未应用的词典";
        var trace = first.Trace;
        var step = first.CurrentStep;
        var results = first.VisibleMatches.ToArray();
        first.TogglePlaybackCommand.Execute(null);
        Assert.True(first.IsPlaying);
        first.TutorialModeCommand.Execute(null);
        Assert.True(first.IsTutorial);
        Assert.False(first.IsClassroom);
        Assert.False(first.IsPlaying);
        clock.Fire();
        first.SelectedTutorialChapter = first.TutorialChapters[5];
        Assert.Same(second.TutorialChapters[0], second.SelectedTutorialChapter);
        Assert.False(second.IsTutorial);
        first.ExploreModeCommand.Execute(null);
        Assert.False(first.IsTutorial);
        Assert.False(first.IsReading);
        Assert.Same(trace, first.Trace);
        Assert.Same(step, first.CurrentStep);
        Assert.Equal(results, first.VisibleMatches);
        Assert.True(first.IsDraft);
        Assert.Equal("尚未应用的词典", first.KeywordText);
        Assert.Same(first.Lessons[2], first.SelectedLesson);
        Assert.Equal(1, first.AnswerIndex);
        first.TutorialModeCommand.Execute(null);
        Assert.Same(first.TutorialChapters[5], first.SelectedTutorialChapter);
    }

    [Fact]
    public void 教程导航处理两端取消选择与释放后的命令()
    {
        using var document = TestDocuments.Create();
        Assert.False(document.PreviousTutorialChapterCommand.CanExecute(null));
        document.PreviousTutorialChapterCommand.Execute(null);
        Assert.Same(document.TutorialChapters[0], document.SelectedTutorialChapter);
        document.NextTutorialChapterCommand.Execute(null);
        var chapter = document.SelectedTutorialChapter;
        document.SelectedTutorialChapter = null!;
        Assert.Same(chapter, document.SelectedTutorialChapter);
        document.SelectedTutorialChapter = document.TutorialChapters[^1];
        Assert.False(document.NextTutorialChapterCommand.CanExecute(null));
        document.NextTutorialChapterCommand.Execute(null);
        chapter = document.SelectedTutorialChapter;
        document.Dispose();
        document.PreviousTutorialChapterCommand.Execute(null);
        document.SelectedTutorialChapter = document.TutorialChapters[0];
        document.TutorialModeCommand.Execute(null);
        Assert.Same(chapter, document.SelectedTutorialChapter);
        Assert.False(document.IsTutorial);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 全部教程章节真实渲染并支持窄窗口与图示主题切换(bool dark)
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(ClassroomTestApplication));
        await session.Dispatch(() =>
        {
            using var document = TestDocuments.Create();
            var view = new AhoCorasickView { DataContext = document };
            var window = new Window { Width = 1440, Height = 1050, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light, Content = view };
            try
            {
                window.Show();
                view.GetVisualDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "打开完整教程").Command!.Execute(null);
                var preview = view.FindControl<AcTutorialView>("TutorialPreview")!;
                Assert.True(preview.IsVisible);
                Assert.False(view.FindControl<Grid>("BodyGrid")!.IsVisible);
                foreach (var chapter in document.TutorialChapters)
                {
                    document.SelectedTutorialChapter = chapter;
                    Dispatcher.UIThread.RunJobs();
                    Assert.Equal(chapter.Markdown, preview.FindControl<AcMarkdownView>("TutorialMarkdown")!.Markdown);
                    using var frame = window.CaptureRenderedFrame();
                    Assert.NotNull(frame);
                    Assert.NotEmpty(preview.GetVisualDescendants().OfType<SelectableTextBlock>());
                    if (chapter == document.TutorialChapters[5]) Capture(window, $"tutorial-{(dark ? "dark" : "light")}-fail");
                    if (chapter == document.TutorialChapters[11]) Capture(window, $"tutorial-{(dark ? "dark" : "light")}-code");
                }
                document.SelectedTutorialChapter = document.TutorialChapters[5];
                Dispatcher.UIThread.RunJobs();
                var image = preview.GetVisualDescendants().OfType<Image>().Single();
                var source = image.Source;
                Assert.NotNull(source);
                window.RequestedThemeVariant = dark ? ThemeVariant.Light : ThemeVariant.Dark;
                Dispatcher.UIThread.RunJobs();
                Assert.NotSame(source, image.Source);
                window.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
                window.Width = 640;
                window.Height = 700;
                Dispatcher.UIThread.RunJobs();
                Assert.True(preview.FindControl<ComboBox>("TutorialCompactChapters")!.IsVisible);
                Assert.False(preview.FindControl<ScrollViewer>("TutorialRail")!.IsVisible);
                Assert.InRange(preview.FindControl<AcMarkdownView>("TutorialMarkdown")!.Bounds.Width, 300, 600);
                Capture(window, $"tutorial-{(dark ? "dark" : "light")}-narrow");
                var scroll = preview.FindControl<ScrollViewer>("TutorialScroll")!;
                scroll.Offset = new Vector(0, 300);
                Dispatcher.UIThread.RunJobs();
                Assert.True(scroll.Offset.Y > 0);
                document.ExploreModeCommand.Execute(null);
                document.TutorialModeCommand.Execute(null);
                Dispatcher.UIThread.RunJobs();
                Assert.True(scroll.Offset.Y > 0);
                document.NextTutorialChapterCommand.Execute(null);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(0, scroll.Offset.Y);
            }
            finally { window.Close(); }
        }, CancellationToken.None);
    }

    private static void Capture(Window window, string name)
    {
        var directory = Environment.GetEnvironmentVariable("AC_SCREENSHOT_DIR");
        if (string.IsNullOrEmpty(directory)) return;
        Directory.CreateDirectory(directory);
        using var bitmap = window.CaptureRenderedFrame();
        bitmap!.Save(Path.Combine(directory, name + ".png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }
}
