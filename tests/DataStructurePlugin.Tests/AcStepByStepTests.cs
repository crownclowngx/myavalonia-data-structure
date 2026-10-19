using DataStructurePlugin.Features.AhoCorasick.Domain;
using Xunit;

namespace DataStructurePlugin.Tests;

/// <summary>检查教学轨迹的渐进性，避免最终自动机或结果泄漏到早期步骤。</summary>
public sealed class AcStepByStepTests
{
    [Fact]
    public void 树每次只长出一个节点且词尾和fail必须到对应步骤才出现()
    {
        var trace = new AhoCorasickTraceBuilder().Build("he\nshe\nhis\nhers", "ushers");
        Assert.Equal(1, trace.Steps[0].VisibleNodes);
        for (var i = 1; i < trace.Steps.Length; i++)
        {
            var before = trace.Steps[i - 1];
            var step = trace.Steps[i];
            Assert.Equal(step.Action == AcAction.Insert ? 1 : 0, step.VisibleNodes - before.VisibleNodes);
            Assert.Equal(step.Action == AcAction.Terminal ? 1 : 0, step.CompletedPatterns - before.CompletedPatterns);
            var changedFailures = step.Failures.Zip(before.Failures).Count(pair => pair.First != pair.Second);
            Assert.Equal(step.Action == AcAction.SetFailure ? 1 : 0, changedFailures);
        }
    }

    [Fact]
    public void 每次比较与回退转移分开且每条结果各占一步()
    {
        var trace = new AhoCorasickTraceBuilder().Build("a\naa\naaa\naaaab\nb", "aaaacaaaab");
        Assert.Contains(trace.Steps, s => s.Action == AcAction.Fallback);
        for (var i = 1; i < trace.Steps.Length - 1; i++)
        {
            var before = trace.Steps[i - 1];
            var step = trace.Steps[i];
            var after = trace.Steps[i + 1];
            if (step.Action == AcAction.Compare)
            {
                Assert.Equal(before.ConsumedSymbols, step.ConsumedSymbols);
                Assert.Equal(before.ActiveSymbol, step.ActiveSymbol);
                Assert.Equal(before.State, step.State);
                Assert.Contains(after.Action, new[] { AcAction.Fallback, AcAction.Transition });
            }
            if (step.Action == AcAction.Fallback)
            {
                Assert.Equal(AcAction.Compare, before.Action);
                Assert.Equal(AcAction.Compare, after.Action);
                Assert.Equal(before.ActiveSymbol, after.ActiveSymbol);
                Assert.Equal(before.ConsumedSymbols, after.ConsumedSymbols);
            }
            if (step.Action == AcAction.Transition) Assert.Equal(AcAction.Compare, before.Action);
            Assert.Equal(step.Action == AcAction.Match ? 1 : 0, step.MatchCount - before.MatchCount);
        }
    }
}
