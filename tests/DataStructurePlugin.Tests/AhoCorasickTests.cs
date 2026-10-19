using DataStructurePlugin.Features.AhoCorasick;
using DataStructurePlugin.Features.AhoCorasick.Domain;
using MyAvaloniaManagement.PluginSdk;
using Xunit;

namespace DataStructurePlugin.Tests;

public sealed class AhoCorasickTests
{
    [Fact]
    public void ClassicTraceReportsSuffixAndOverlappingMatches()
    {
        var trace = new AhoCorasickTraceBuilder().Build("he\nshe\nhis\nhers", "ushers");
        Assert.Equal([("she", 1, 4), ("he", 2, 4), ("hers", 2, 6)], trace.Matches.Select(m => (m.Pattern, m.Start, m.End)));
        var she = Assert.Single(trace.Nodes, n => n.Prefix == "she");
        var he = Assert.Single(trace.Nodes, n => n.Prefix == "he");
        Assert.Equal(he.Id, trace.Steps[^1].Failures[she.Id]);
        Assert.Equal(10, trace.Nodes.Length);
        Assert.Equal(1, trace.Steps[0].VisibleNodes);
        Assert.Equal(-1, trace.Steps[0].Failures[she.Id]);
    }

    [Theory]
    [InlineData("a\naa\naaa", "aaaaa", 12)]
    [InlineData("北京\n北京大学\n大学", "我在北京大学学习", 3)]
    [InlineData("🙂\n🙂a\na", "🙂a🙂", 4)]
    [InlineData("he\nshe", "xyz", 0)]
    [InlineData("he", "", 0)]
    [InlineData("a\na\n  a  \n", "a", 1)]
    public void ExamplesAndNormalization(string patterns, string text, int count)
    {
        var trace = new AhoCorasickTraceBuilder().Build(patterns, text);
        Assert.Equal(count, trace.Matches.Length);
        foreach (var match in trace.Matches) Assert.Equal(match.Pattern, text[match.Start..match.End]);
        Assert.Equal(AcAction.Complete, trace.Steps[^1].Action);
        Assert.Equal(trace.Symbols.Length, trace.Steps[^1].ConsumedSymbols);
    }

    [Fact]
    public void FailureStepsNeverConsumeOrRewindTheText()
    {
        var trace = new AhoCorasickTraceBuilder().Build("a\naa\naaa\naaaab\nb", "aaaacaaaab");
        var fallbackCount = 0;
        for (var i = 1; i < trace.Steps.Length; i++)
        {
            var step = trace.Steps[i];
            Assert.True(step.ConsumedSymbols >= trace.Steps[i - 1].ConsumedSymbols);
            if (step.Action != AcAction.Fallback) continue;
            fallbackCount++;
            Assert.Equal(trace.Steps[i - 1].ConsumedSymbols, step.ConsumedSymbols);
            Assert.Equal(trace.Steps[i - 1].ActiveSymbol, step.ActiveSymbol);
            Assert.True(trace.Nodes[step.EdgeTo!.Value].Depth < trace.Nodes[step.EdgeFrom!.Value].Depth);
        }
        Assert.True(fallbackCount >= 3);
    }

    [Fact]
    public void RandomCasesAgreeWithNaiveMatchingAndStateInvariant()
    {
        var random = new Random(4817);
        for (var trial = 0; trial < 180; trial++)
        {
            string Word(int length) => new(Enumerable.Range(0, length).Select(_ => "abc"[random.Next(3)]).ToArray());
            var patterns = Enumerable.Range(0, 6).Select(_ => Word(random.Next(1, 6))).Distinct().ToArray();
            var text = Word(random.Next(0, 40));
            var trace = new AhoCorasickTraceBuilder().Build(string.Join('\n', patterns), text);
            var expected = patterns.SelectMany(pattern => Enumerable.Range(0, text.Length)
                    .Where(i => text.AsSpan(i).StartsWith(pattern, StringComparison.Ordinal)).Select(i => (pattern, i, i + pattern.Length)))
                .OrderBy(m => m.i).ThenBy(m => m.pattern).ToArray();
            var actual = trace.Matches.Select(m => (pattern: m.Pattern, i: m.Start, end: m.End)).OrderBy(m => m.i).ThenBy(m => m.pattern).ToArray();
            Assert.Equal(expected, actual);
            foreach (var step in trace.Steps.Where(s => s.Action == AcAction.Transition))
            {
                var consumed = text[..step.ConsumedSymbols];
                var longest = trace.Nodes.Where(n => consumed.EndsWith(n.Prefix, StringComparison.Ordinal)).MaxBy(n => n.Depth)!;
                Assert.Equal(longest.Id, step.State);
            }
            foreach (var node in trace.Nodes.Skip(1))
            {
                var expectedFail = trace.Nodes.Where(n => n.Depth < node.Depth && node.Prefix.EndsWith(n.Prefix, StringComparison.Ordinal)).MaxBy(n => n.Depth)!;
                Assert.Equal(expectedFail.Id, trace.Steps[^1].Failures[node.Id]);
            }
        }
    }

    [Theory]
    [InlineData("", "abc")]
    public void InvalidInputsAreRejected(string patterns, string text) => Assert.Throws<ArgumentException>(() => new AhoCorasickTraceBuilder().Build(patterns, text));

    [Fact]
    public void BrokenSurrogateSequencesAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new AhoCorasickTraceBuilder().Build("a", new string((char)0xD800, 1)));
        Assert.Throws<ArgumentException>(() => new AhoCorasickTraceBuilder().Build(new string((char)0xDFFF, 1), "a"));
    }

    [Fact]
    public void BudgetsRejectRatherThanTruncate()
    {
        Assert.Throws<ArgumentException>(() => new AhoCorasickTraceBuilder().Build("a", new string('a', 601)));
        Assert.Throws<ArgumentException>(() => new AhoCorasickTraceBuilder().Build(new string('a', 161), "a"));
        Assert.Throws<ArgumentException>(() => new AhoCorasickTraceBuilder().Build(string.Join('\n', Enumerable.Range(1, 41).Select(i => $"word{i}")), ""));
        Assert.Throws<ArgumentException>(() => new AhoCorasickTraceBuilder().Build(string.Join('\n', Enumerable.Range(1, 40).Select(i => new string('a', i))), new string('a', 600)));
    }

    [Fact]
    public async Task DocumentsHaveIndependentStateAndRestoreEarlierFrames()
    {
        using var document = TestDocuments.Create();
        using var other = TestDocuments.Create();
        await document.InitializeAsync(new NewDocumentActivation("我的 AC 实验"), CancellationToken.None);
        Assert.Equal("我的 AC 实验", document.Presentation.Title);
        document.FinishCommand.Execute(null);
        Assert.Equal(3, document.VisibleMatches.Count);
        document.RestartCommand.Execute(null);
        Assert.Empty(document.VisibleMatches);
        Assert.Equal(1, document.CurrentStep.VisibleNodes);
        document.KeywordText = "北京";
        document.SearchText = "北京";
        Assert.True(document.IsDraft);
        document.BuildCommand.Execute(null);
        Assert.False(document.IsDraft);
        Assert.Single(document.Trace.Matches);
        Assert.Equal("ushers", other.Trace.Text);
        document.FinishCommand.Execute(null);
        var valid = document.Trace;
        document.KeywordText = "";
        document.BuildCommand.Execute(null);
        Assert.True(document.HasError);
        Assert.Same(valid, document.Trace);
        document.Dispose();
        document.BuildCommand.Execute(null);
        Assert.Same(valid, document.Trace);
    }

    [Fact]
    public void LessonDemonstrationsAndMatchLinksSeekToTheRightEvents()
    {
        using var document = TestDocuments.Create();
        document.SelectedLesson = document.Lessons[2];
        document.DemonstrateLessonCommand.Execute(null);
        Assert.Equal("she", document.Trace.Nodes[document.CurrentStep.State].Prefix);
        Assert.Equal("he", document.InspectedFailure);
        document.SelectedLesson = document.Lessons[4];
        document.DemonstrateLessonCommand.Execute(null);
        Assert.Equal("he", document.VisibleMatches[^1].Pattern);
        document.FinishCommand.Execute(null);
        document.SelectedMatch = document.VisibleMatches[0];
        Assert.Equal(AcAction.Match, document.CurrentStep.Action);
        Assert.Single(document.VisibleMatches);
        document.AnswerIndex = document.SelectedLesson.CorrectAnswer;
        document.CheckAnswerCommand.Execute(null);
        Assert.StartsWith("✓", document.Feedback);
    }
}
