using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Input;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DataStructurePlugin.Features.AhoCorasick;
using DataStructurePlugin.Features.AhoCorasick.Controls;
using DataStructurePlugin.Features.AhoCorasick.Domain;
using DataStructurePlugin.Plugin;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DataStructurePlugin.Tests;

public sealed class ClassroomTestApplication : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<ClassroomTestApplication>()
        .UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

public sealed class AhoCorasickViewTests
{
    [Fact]
    public async Task 画布节点点击与拖动使用实际指针坐标()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(ClassroomTestApplication));
        await session.Dispatch(() =>
        {
            using var document = TestDocuments.Create();
            document.KeywordText = "a";
            document.SearchText = "a";
            document.BuildCommand.Execute(null);
            document.FinishCommand.Execute(null);
            document.ExploreModeCommand.Execute(null);
            var view = new AhoCorasickView { DataContext = document };
            var window = new Window { Width = 1100, Height = 850, Content = view };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                var graph = view.FindControl<AutomatonCanvas>("Graph")!;
                using (window.CaptureRenderedFrame()) { }
                var root = graph.TranslatePoint(new Point(graph.Bounds.Width / 2, 38), window)!.Value;
                window.MouseDown(root, MouseButton.Left);
                window.MouseUp(root, MouseButton.Left);
                Assert.Equal(0, document.SelectedNodeId);
                document.SelectedNodeId = 1;
                window.MouseDown(root, MouseButton.Left);
                window.MouseMove(root + new Vector(45, 0), RawInputModifiers.LeftMouseButton);
                window.MouseUp(root + new Vector(45, 0), MouseButton.Left);
                Assert.Equal(1, document.SelectedNodeId);
                using (window.CaptureRenderedFrame()) { }
                window.MouseDown(root + new Vector(45, 0), MouseButton.Left);
                window.MouseUp(root + new Vector(45, 0), MouseButton.Left);
                Assert.Equal(0, document.SelectedNodeId);
            }
            finally { window.Close(); }
        }, CancellationToken.None);
    }

    [Fact]
    public async Task 真实输入绑定和结果选择能够更新图与游标()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(ClassroomTestApplication));
        await session.Dispatch(() =>
        {
            using var document = TestDocuments.Create();
            var view = new AhoCorasickView { DataContext = document };
            var window = new Window { Width = 1100, Height = 850, Content = view };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                var inputs = view.GetVisualDescendants().OfType<TextBox>().ToArray();
                inputs.Single(input => AutomationProperties.GetName(input) == "关键词，每行一个").Text = "🙂\n🙂a\na";
                inputs.Single(input => AutomationProperties.GetName(input) == "待匹配文本").Text = "🙂a🙂";
                Dispatcher.UIThread.RunJobs();
                Assert.True(document.IsDraft);
                Assert.Equal("🙂a🙂", document.SearchText);
                var build = view.GetVisualDescendants().OfType<Button>().Single(b => b.Content?.ToString() == "构建并开始实验 →");
                build.Command!.Execute(build.CommandParameter);
                document.FinishCommand.Execute(null);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(4, document.VisibleMatches.Count);
                Assert.Same(document.Trace, view.FindControl<AutomatonCanvas>("Graph")!.Trace);
                var results = view.GetVisualDescendants().OfType<ListBox>()
                    .Single(list => AutomationProperties.GetName(list) == "已发现的匹配结果");
                results.SelectedItem = document.VisibleMatches[0];
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(AcAction.Match, document.CurrentStep.Action);
                Assert.Equal(1, document.CurrentStep.MatchCount);
                Assert.Same(document.CurrentStep, view.FindControl<TextTape>("Tape")!.Step);
                window.Width = 640;
                window.Height = 580;
                Dispatcher.UIThread.RunJobs();
                Assert.True(view.FindControl<AutomatonCanvas>("Graph")!.Bounds.Width > 300);
                Capture(window, "minimum-window");
            }
            finally { window.Close(); }
        }, CancellationToken.None);
    }

    [Fact]
    public async Task 替换DataContext与移除View都会暂停此前的Document()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(ClassroomTestApplication));
        await session.Dispatch(() =>
        {
            using var first = TestDocuments.Create();
            using var second = TestDocuments.Create();
            var view = new AhoCorasickView { DataContext = first };
            var window = new Window { Width = 900, Height = 700, Content = view };
            try
            {
                window.Show();
                first.TogglePlaybackCommand.Execute(null);
                Assert.True(first.IsPlaying);
                view.DataContext = second;
                Assert.False(first.IsPlaying);
                second.TogglePlaybackCommand.Execute(null);
                Assert.True(second.IsPlaying);
                window.Content = null;
                Assert.False(second.IsPlaying);
            }
            finally { window.Close(); }
        }, CancellationToken.None);
    }

    [Fact]
    public async Task ClassroomRendersAtDesktopAndNarrowWidthsAndStopsPlaybackOnDetach()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(ClassroomTestApplication));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await session.Dispatch(async () =>
        {
            using var services = new ServiceCollection().AddDataStructurePluginServices()
                .AddScoped<AhoCorasickDocument>().BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
            using var scope = services.CreateScope();
            var document = scope.ServiceProvider.GetRequiredService<AhoCorasickDocument>();
            var view = new AhoCorasickView { DataContext = document };
            var window = new Window { Width = 1440, Height = 980, Content = view };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                Assert.True(view.FindControl<AutomatonCanvas>("Graph")!.Bounds.Width > 300);
                Capture(window, "reading");
                document.ExploreModeCommand.Execute(null);
                document.GoSearchCommand.Execute(null);
                document.NextEventCommand.Execute(null);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(1, document.CurrentStep.MatchCount);
                Assert.Equal("she", document.Trace.Matches[document.CurrentStep.MatchCount - 1].Pattern);
                Assert.Same(document.CurrentStep, view.FindControl<TextTape>("Tape")!.Step);
                Capture(window, "experiment");
                Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "命中「she」");
                var buildButton = view.GetVisualDescendants().OfType<Button>().Single(b => b.Content?.ToString() == "构建并开始实验 →");
                Assert.Same(document.BuildCommand, buildButton.Command);
                window.Width = 760;
                Dispatcher.UIThread.RunJobs();
                Assert.False(view.FindControl<ScrollViewer>("ChapterRail")!.IsVisible);
                Assert.Equal(1, Grid.GetRow(view.FindControl<StackPanel>("InspectorPanel")!));
                Capture(window, "narrow");
                document.RestartCommand.Execute(null);
                document.Speed = 4;
                // 等待实际 UI 时钟造成的状态变化，以超时作为失败边界，不用固定延迟猜测是否已推进。
                var advanced = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                document.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(document.FrameIndex) && document.FrameIndex > 0) advanced.TrySetResult();
                };
                document.TogglePlaybackCommand.Execute(null);
                await advanced.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.True(document.FrameIndex > 0);
                window.Content = null;
                Assert.False(document.IsPlaying);
            }
            finally { window.Close(); }
            return true;
        }, timeout.Token);
    }

    private static void Capture(Window window, string name)
    {
        using var bitmap = window.CaptureRenderedFrame();
        Assert.NotNull(bitmap);
        var output = Environment.GetEnvironmentVariable("AC_SCREENSHOT_DIR");
        if (string.IsNullOrEmpty(output)) return;
        Directory.CreateDirectory(output);
        bitmap.Save(Path.Combine(output, $"{name}.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }
}
