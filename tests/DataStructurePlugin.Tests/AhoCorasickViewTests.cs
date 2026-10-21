using Avalonia;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
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
        // 在 UI 线程绘制真实 Skia 像素，避免每例重建 Application 时与上一例渲染线程的退出竞态。
        .UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false, ShouldRenderOnUIThread = true });
}

[Collection(AcUiCollection.Name)]
public sealed class AhoCorasickViewTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 首次打开继承窗口深浅主题并完整渲染(bool dark)
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(ClassroomTestApplication));
        await session.Dispatch(() =>
        {
            using var document = TestDocuments.Create();
            var theme = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            var view = new AhoCorasickView { DataContext = document };
            var window = new Window { Width = 1440, Height = 1100, RequestedThemeVariant = theme, Content = view };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(theme, view.ActualThemeVariant);
                Assert.Same(dark ? AcDrawingPalette.Dark : AcDrawingPalette.Light, view.FindControl<AutomatonCanvas>("Graph")!.Palette);
                Assert.False(window.TryFindResource("Ac.Page", out _)); // 本域配色不能泄漏成 Host 全局资源。
                AssertTextContrast(view);
                Capture(window, $"theme-{(dark ? "dark" : "light")}-reading");
                document.ExploreModeCommand.Execute(null);
                document.GoSearchCommand.Execute(null);
                document.NextEventCommand.Execute(null);
                Dispatcher.UIThread.RunJobs();
                Capture(window, $"theme-{(dark ? "dark" : "light")}-match");
                window.Width = 640;
                window.Height = 580;
                Dispatcher.UIThread.RunJobs();
                Capture(window, $"theme-{(dark ? "dark" : "light")}-narrow");
            }
            finally { window.Close(); }
        }, CancellationToken.None);
    }

    [Fact]
    public async Task 应用主题实时往返切换会重绘画布和文本带且保留实验进度()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(ClassroomTestApplication));
        await session.Dispatch(() =>
        {
            var application = Application.Current!;
            var oldTheme = application.RequestedThemeVariant;
            using var document = TestDocuments.Create();
            document.ExploreModeCommand.Execute(null);
            document.GoSearchCommand.Execute(null);
            document.NextCommand.Execute(null);
            document.TogglePlaybackCommand.Execute(null);
            var step = document.CurrentStep;
            var frame = document.FrameIndex;
            var trace = document.Trace;
            var view = new AhoCorasickView { DataContext = document };
            var window = new Window { Width = 1100, Height = 1000, Content = view };
            try
            {
                window.Show();
                foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark, ThemeVariant.Light })
                {
                    application.RequestedThemeVariant = theme;
                    Dispatcher.UIThread.RunJobs();
                    var palette = theme == ThemeVariant.Dark ? AcDrawingPalette.Dark : AcDrawingPalette.Light;
                    var graph = view.FindControl<AutomatonCanvas>("Graph")!;
                    var tape = view.FindControl<TextTape>("Tape")!;
                    Assert.Equal(theme, view.ActualThemeVariant);
                    Assert.Same(palette, graph.Palette);
                    Assert.Same(palette, tape.Palette);
                    Assert.Equal(ResourceColor(view, "Ac.Page"), ((ISolidColorBrush)view.Background!).Color);
                    // 读取实际 Skia 像素，防止只换了主题属性但自绘控件没有立即失效重绘。
                    AssertPixel(window, graph, new Point(8, 8), palette.Canvas.Color);
                    AssertPixel(window, tape, new Point(4, 4), palette.Current.Color);
                    AssertPixel(window, tape, new Point(48, 4), palette.Unread.Color);
                    Assert.Same(trace, document.Trace);
                    Assert.Same(step, document.CurrentStep);
                    Assert.Equal(frame, document.FrameIndex);
                    Assert.True(document.IsPlaying);
                }
            }
            finally { window.Close(); application.RequestedThemeVariant = oldTheme; }
        }, CancellationToken.None);
    }

    [Fact]
    public async Task 画布旁单步按钮从根长出节点并逐条显示后缀命中()
    {
        using var session = HeadlessUnitTestSession.StartNew(typeof(ClassroomTestApplication));
        await session.Dispatch(() =>
        {
            using var document = TestDocuments.Create();
            document.ExploreModeCommand.Execute(null);
            var view = new AhoCorasickView { DataContext = document };
            var window = new Window { Width = 1100, Height = 1000, Content = view };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                var next = view.GetVisualDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "画布单步演示");
                Assert.Equal(1, document.CurrentStep.VisibleNodes);
                Capture(window, "growth-root");
                next.Command!.Execute(null);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(AcAction.Insert, document.CurrentStep.Action);
                Assert.Equal(2, document.CurrentStep.VisibleNodes);
                Assert.Same(document.CurrentStep, view.FindControl<AutomatonCanvas>("Graph")!.Step);
                Assert.Contains("已长出 2 /", document.Statistics);
                Capture(window, "growth-first-node");
                next.Command.Execute(null);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(3, document.CurrentStep.VisibleNodes);
                Capture(window, "growth-second-node");
                document.GoSearchCommand.Execute(null);
                while (document.CurrentStep.Action != AcAction.Match) next.Command.Execute(null);
                Assert.Single(document.VisibleMatches);
                Assert.Equal("she", document.VisibleMatches[0].Pattern);
                next.Command.Execute(null);
                Assert.Equal(2, document.VisibleMatches.Count);
                Assert.Equal("he", document.VisibleMatches[1].Pattern);
                document.PreviousCommand.Execute(null);
                Assert.Single(document.VisibleMatches);
                document.RestartCommand.Execute(null);
                Assert.Equal(1, document.CurrentStep.VisibleNodes);
                Assert.Empty(document.VisibleMatches);
            }
            finally { window.Close(); }
        }, CancellationToken.None);
    }

    private static Color ResourceColor(Control view, string key)
    {
        Assert.True(view.TryFindResource(key, view.ActualThemeVariant, out var resource), key);
        return Assert.IsAssignableFrom<ISolidColorBrush>(resource).Color;
    }

    private static void AssertTextContrast(Control view)
    {
        foreach (var background in new[] { "Ac.Page", "Ac.Surface", "Ac.Input", "Ac.Button", "Ac.Formula", "Ac.Explanation", "Ac.Selection", "Ac.Note" })
        {
            Assert.True(Contrast(ResourceColor(view, "Ac.Text"), ResourceColor(view, background)) >= 4.5, background);
            Assert.True(Contrast(ResourceColor(view, "Ac.Muted"), ResourceColor(view, background)) >= 4.5, background);
        }
        Assert.True(Contrast(ResourceColor(view, "Ac.OnPrimary"), ResourceColor(view, "Ac.Primary")) >= 4.5);
        Assert.True(Contrast(ResourceColor(view, "Ac.WarningText"), ResourceColor(view, "Ac.WarningBackground")) >= 4.5);
        Assert.True(Contrast(ResourceColor(view, "Ac.Error"), ResourceColor(view, "Ac.Surface")) >= 4.5);
        var palette = view.FindControl<AutomatonCanvas>("Graph")!.Palette;
        foreach (var background in new[] { palette.Node, palette.Candidate, palette.Consumed, palette.Unread })
            Assert.True(Contrast(palette.Text.Color, background.Color) >= 4.5);
        Assert.True(Contrast(palette.Muted.Color, palette.Canvas.Color) >= 4.5);
        foreach (var line in new[] { palette.Edge, palette.Failure, palette.Accent, palette.Match })
            Assert.True(Contrast(line.Color, palette.Canvas.Color) >= 3);
    }

    private static double Contrast(Color left, Color right)
    {
        static double Linear(byte channel) => channel / 255d <= 0.04045 ? channel / 255d / 12.92 : Math.Pow((channel / 255d + 0.055) / 1.055, 2.4);
        static double Luminance(Color color) => 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
        var a = Luminance(left);
        var b = Luminance(right);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private static void AssertPixel(Window window, Control control, Point localPoint, Color expected)
    {
        using var bitmap = window.CaptureRenderedFrame();
        Assert.NotNull(bitmap);
        using var pixels = new WriteableBitmap(bitmap.PixelSize, bitmap.Dpi, PixelFormat.Rgba8888, AlphaFormat.Unpremul);
        using var buffer = pixels.Lock();
        bitmap.CopyPixels(buffer);
        var point = control.TranslatePoint(localPoint, window)!.Value * window.RenderScaling;
        var offset = (int)point.Y * buffer.RowBytes + (int)point.X * 4;
        var actual = Color.FromArgb(System.Runtime.InteropServices.Marshal.ReadByte(buffer.Address, offset + 3),
            System.Runtime.InteropServices.Marshal.ReadByte(buffer.Address, offset),
            System.Runtime.InteropServices.Marshal.ReadByte(buffer.Address, offset + 1),
            System.Runtime.InteropServices.Marshal.ReadByte(buffer.Address, offset + 2));
        Assert.Equal(expected, actual);
    }

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
                var build = view.GetVisualDescendants().OfType<Button>().Single(b => b.Content?.ToString() == "应用输入 · 从根开始 →");
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
                var buildButton = view.GetVisualDescendants().OfType<Button>().Single(b => b.Content?.ToString() == "应用输入 · 从根开始 →");
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
