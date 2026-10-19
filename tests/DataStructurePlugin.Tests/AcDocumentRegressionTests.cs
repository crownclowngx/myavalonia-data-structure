using DataStructurePlugin.Features.AhoCorasick;
using DataStructurePlugin.Features.AhoCorasick.Application;
using DataStructurePlugin.Features.AhoCorasick.Domain;
using MyAvaloniaManagement.PluginSdk;
using Xunit;

namespace DataStructurePlugin.Tests;

public sealed class AcDocumentRegressionTests
{
    [Fact]
    public void 结果集合身份稳定且旧结果无法导航新实验()
    {
        using var document = TestDocuments.Create();
        var list = document.VisibleMatches;
        document.FinishCommand.Execute(null);
        var old = list[0];
        document.PreviousCommand.Execute(null);
        Assert.Same(list, document.VisibleMatches);
        document.GoSearchCommand.Execute(null);
        var position = document.FrameIndex;
        document.SelectedMatch = old;
        Assert.Equal(position, document.FrameIndex);
        document.KeywordText = "x";
        document.SearchText = "xxx";
        document.BuildCommand.Execute(null);
        document.FinishCommand.Execute(null);
        Assert.Same(list, document.VisibleMatches);
        Assert.All(list, row => Assert.Equal("x", row.Pattern));
        position = document.FrameIndex;
        document.SelectedMatch = old;
        Assert.Equal(position, document.FrameIndex);
        document.SelectedMatch = list[0];
        Assert.Single(document.VisibleMatches);
        Assert.Equal(AcAction.Match, document.CurrentStep.Action);
    }

    [Fact]
    public void 构建失败只更新错误和草稿并停止旧播放()
    {
        var builder = new FailingAfterFirstBuilder();
        var clock = new TestPlaybackClock();
        using var document = new AhoCorasickDocument(builder, clock);
        document.FinishCommand.Execute(null);
        var trace = document.Trace;
        var rows = document.VisibleMatches.ToArray();
        document.Seek(document.MaximumPosition - 1);
        document.TogglePlaybackCommand.Execute(null);
        var frame = document.FrameIndex;
        document.KeywordText = "另一个输入";
        document.BuildCommand.Execute(null);
        Assert.Same(trace, document.Trace);
        Assert.Equal(frame, document.FrameIndex);
        Assert.True(document.HasError);
        Assert.True(document.IsDraft);
        Assert.False(document.IsPlaying);
        Assert.False(clock.Running);
        Assert.Equal(rows, document.VisibleMatches);
        clock.Fire();
        Assert.Equal(frame, document.FrameIndex);
    }

    [Fact]
    public void 初始构建失败也会释放时钟()
    {
        var clock = new TestPlaybackClock();
        Assert.Throws<ArgumentException>(() => new AhoCorasickDocument(new AlwaysFailBuilder(), clock));
        Assert.True(clock.Disposed);
    }

    [Fact]
    public void 节点选择有界且章节取消选择不会破坏正文()
    {
        using var document = TestDocuments.Create();
        document.SelectedNodeId = -99;
        Assert.Equal(0, document.SelectedNodeId);
        document.FinishCommand.Execute(null);
        document.SelectedNodeId = int.MaxValue;
        Assert.Equal(document.Trace.Nodes.Length - 1, document.SelectedNodeId);
        document.RestartCommand.Execute(null);
        Assert.Equal(0, document.SelectedNodeId);
        Assert.Equal("ε", document.InspectedPrefix);
        var lesson = document.SelectedLesson;
        document.SelectedLesson = null!;
        Assert.Same(lesson, document.SelectedLesson);
        document.AnswerIndex = 999;
        document.CheckAnswerCommand.Execute(null);
        Assert.Contains("选择一个答案", document.Feedback);
    }

    [Fact]
    public void 教材重排仍根据语义锚点定位而非章节编号()
    {
        using var document = TestDocuments.Create();
        document.SelectedLesson = document.Lessons[2] with { Number = "99" };
        document.DemonstrateLessonCommand.Execute(null);
        Assert.Equal(AcAction.SetFailure, document.CurrentStep.Action);
        Assert.Equal("she", document.InspectedPrefix);
        Assert.Equal("he", document.InspectedFailure);
        foreach (var lesson in document.Lessons)
        {
            document.SelectedLesson = lesson;
            document.DemonstrateLessonCommand.Execute(null);
            Assert.InRange(document.FrameIndex, 0, document.MaximumPosition);
            if (lesson.Demo is not null) Assert.Equal(lesson.Demo.Action, document.CurrentStep.Action);
            Assert.InRange(lesson.CorrectAnswer, 0, lesson.Answers.Length - 1);
        }
    }

    [Fact]
    public async Task 初始化遵守取消且释放后所有命令不会启动任务()
    {
        var clock = new TestPlaybackClock();
        using var document = new AhoCorasickDocument(new AhoCorasickTraceBuilder(), clock);
        var title = document.Presentation.Title;
        var notifications = 0;
        document.PresentationChanged += (_, _) => notifications++;
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await document.InitializeAsync(new NewDocumentActivation("不应应用"), cancelled.Token));
        Assert.Equal(title, document.Presentation.Title);
        Assert.Equal(0, notifications);
        await document.InitializeAsync(new NewDocumentActivation("当前课堂"), CancellationToken.None);
        Assert.Equal(1, notifications);
        document.Dispose();
        var trace = document.Trace;
        var frame = document.FrameIndex;
        foreach (var command in new System.Windows.Input.ICommand[] { document.BuildCommand, document.PreviousCommand,
            document.NextCommand, document.FinishCommand, document.GoSearchCommand, document.NextEventCommand,
            document.ApplyExampleCommand, document.DemonstrateLessonCommand, document.TogglePlaybackCommand,
            document.CheckAnswerCommand, document.ReadModeCommand, document.ExploreModeCommand }) command.Execute(null);
        Assert.Same(trace, document.Trace);
        Assert.Equal(frame, document.FrameIndex);
        Assert.True(clock.Disposed);
        Assert.Equal(0, clock.StartCount);
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await document.InitializeAsync(new NewDocumentActivation("释放后"), CancellationToken.None));
    }

    [Fact]
    public void 输出列表的每一行都指向自己的真实报告事件()
    {
        var trace = new AhoCorasickTraceBuilder().Build("a\naa\naaa", "aaaaa");
        var rows = AcTraceProjection.CreateMatchRows(trace);
        Assert.Equal(trace.Matches.Length, rows.Length);
        for (var i = 0; i < rows.Length; i++)
        {
            Assert.Equal(AcAction.Match, trace.Steps[rows[i].StepIndex].Action);
            Assert.Equal(i + 1, trace.Steps[rows[i].StepIndex].MatchCount);
            Assert.Equal(trace.Matches[i].Pattern, rows[i].Pattern);
        }
    }

    private sealed class FailingAfterFirstBuilder : IAcTraceBuilder
    {
        private int _calls;
        public AcTrace Build(string keywordText, string text) => ++_calls == 1
            ? new AhoCorasickTraceBuilder().Build(keywordText, text) : throw new ArgumentException("测试构建失败");
    }
    private sealed class AlwaysFailBuilder : IAcTraceBuilder
    {
        public AcTrace Build(string keywordText, string text) => throw new ArgumentException("测试初始失败");
    }
}
