using System.Text;
using DataStructurePlugin.Features.AhoCorasick.Application;
using DataStructurePlugin.Features.AhoCorasick.Domain;
using Xunit;

namespace DataStructurePlugin.Tests;

public sealed class AcDomainRegressionTests
{
    [Fact]
    public void 随机中文补充平面字符和组合字符与朴素匹配一致()
    {
        var random = new Random(9371);
        string[] alphabet = ["a", "中", "🙂", "e", "\u0301"];
        string Word(int count) => string.Concat(Enumerable.Range(0, count).Select(_ => alphabet[random.Next(alphabet.Length)]));
        for (var trial = 0; trial < 100; trial++)
        {
            var patterns = Enumerable.Range(0, 8).Select(_ => Word(random.Next(1, 5))).Distinct().ToArray();
            var text = Word(random.Next(0, 35));
            var trace = new AhoCorasickTraceBuilder().Build(string.Join('\n', patterns), text);
            var expected = patterns.SelectMany(pattern => Enumerable.Range(0, text.Length)
                    .Where(offset => text.AsSpan(offset).StartsWith(pattern, StringComparison.Ordinal))
                    .Select(offset => (pattern, offset, offset + pattern.Length)))
                .OrderBy(m => m.offset).ThenBy(m => m.pattern, StringComparer.Ordinal).ToArray();
            var actual = trace.Matches.Select(m => (pattern: m.Pattern, offset: m.Start, m.End))
                .OrderBy(m => m.offset).ThenBy(m => m.pattern, StringComparer.Ordinal).ToArray();
            Assert.Equal(expected, actual);
            foreach (var step in trace.Steps.Where(s => s.Action == AcAction.Transition))
            {
                var last = trace.Symbols[step.ConsumedSymbols - 1];
                var read = text[..(last.Offset + last.Length)];
                var longest = trace.Nodes.Where(n => read.EndsWith(n.Prefix, StringComparison.Ordinal)).MaxBy(n => n.Depth)!;
                Assert.Equal(longest.Id, step.State);
            }
        }
    }

    [Fact]
    public void 轨迹所有公开集合均无法通过普通集合接口写入()
    {
        var trace = new AhoCorasickTraceBuilder().Build("he\nshe", "she");
        Assert.Throws<NotSupportedException>(() => ((IList<string>)trace.Patterns)[0] = "changed");
        Assert.Throws<NotSupportedException>(() => ((IList<AcNode>)trace.Nodes)[0] = trace.Nodes[^1]);
        Assert.Throws<NotSupportedException>(() => ((IList<AcStep>)trace.Steps)[0] = trace.Steps[^1]);
        Assert.Throws<NotSupportedException>(() => ((IList<AcSymbol>)trace.Symbols)[0] = new("x", 0, 1));
        Assert.Throws<NotSupportedException>(() => ((IList<AcMatch>)trace.Matches)[0] = trace.Matches[^1]);
        Assert.Throws<NotSupportedException>(() => ((IList<int>)trace.Steps[^1].Failures)[1] = 999);
        var queued = trace.Steps.First(step => !step.Queue.IsEmpty);
        Assert.Throws<NotSupportedException>(() => ((IList<int>)queued.Queue)[0] = 999);
        Assert.Throws<NotSupportedException>(() => ((IDictionary<int, int>)trace.Nodes[0].Edges)[1] = 999);
        Assert.Equal(-1, trace.Steps[0].Failures[1]);
        Assert.Equal(0, trace.Steps[^1].Failures[1]);
    }

    [Fact]
    public void 后插入的短词不会提前显示为终止词()
    {
        var trace = new AhoCorasickTraceBuilder().Build("hers\nhe", "hers");
        var he = trace.Nodes.Single(n => n.Prefix == "he");
        var before = trace.Steps.First(s => s.Action == AcAction.Terminal && s.CompletedPatterns == 1);
        Assert.Equal("∅", AcTraceProjection.Inspect(trace, before, he.Id).OwnOutput);
        var after = trace.Steps.First(s => s.Action == AcAction.Terminal && s.CompletedPatterns == 2);
        Assert.Equal("he", AcTraceProjection.Inspect(trace, after, he.Id).OwnOutput);
    }

    [Fact]
    public void 关键阶段与BFS队列快照始终一致()
    {
        var trace = new AhoCorasickTraceBuilder().Build("he\nshe\nhis\nhers", "ushers");
        for (var i = 0; i < trace.Steps.Length; i++)
        {
            var step = trace.Steps[i];
            if (i > 0) Assert.True(step.Stage >= trace.Steps[i - 1].Stage);
            Assert.Equal(step.Queue.Length, step.Queue.Distinct().Count());
            Assert.All(step.Queue, id => Assert.InRange(id, 1, step.VisibleNodes - 1));
            if (step.Stage != AcStage.Search) Assert.Equal(0, step.ConsumedSymbols);
            for (var node = 1; node < step.VisibleNodes; node++)
            {
                var target = step.Failures[node];
                if (target < 0) continue;
                Assert.True(trace.Nodes[target].Depth < trace.Nodes[node].Depth);
                Assert.EndsWith(trace.Nodes[target].Prefix, trace.Nodes[node].Prefix, StringComparison.Ordinal);
            }
        }
    }

    [Fact]
    public void 接受预算边界并拒绝空白词典与空参数()
    {
        var builder = new AhoCorasickTraceBuilder();
        Assert.Equal(160, builder.Build(new string('a', 159), "").Nodes.Length);
        Assert.Equal(600, builder.Build("🙂", string.Concat(Enumerable.Repeat("🙂", 600))).Matches.Length);
        Assert.Equal(40, builder.Build(string.Join('\n', Enumerable.Range(0, 40).Select(i => $"w{i:D2}")), "").Patterns.Length);
        Assert.Single(builder.Build("a" + new string(' ', 1999), "a").Patterns);
        Assert.Throws<ArgumentException>(() => builder.Build(" \r\n\t ", "a"));
        Assert.Throws<ArgumentException>(() => builder.Build("a" + new string(' ', 2000), "a"));
        Assert.Throws<ArgumentException>(() => builder.Build("a", new string('x', 2401)));
        Assert.Throws<ArgumentNullException>(() => builder.Build(null!, ""));
        Assert.Throws<ArgumentNullException>(() => builder.Build("a", null!));
    }

    [Fact]
    public void 单例构建器的并行调用互不污染()
    {
        var builder = new AhoCorasickTraceBuilder();
        Parallel.For(0, 30, index =>
        {
            var word = $"词{index}";
            var trace = builder.Build(word, "🙂" + word);
            var match = Assert.Single(trace.Matches);
            Assert.Equal(word, match.Pattern);
            Assert.Equal(2, match.Start);
        });
    }
}
