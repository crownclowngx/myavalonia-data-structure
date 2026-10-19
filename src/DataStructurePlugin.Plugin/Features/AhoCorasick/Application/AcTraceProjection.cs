using System.Collections.Immutable;
using DataStructurePlugin.Features.AhoCorasick.Domain;

namespace DataStructurePlugin.Features.AhoCorasick.Application;

/// <summary>结果列表的一行。StepIndex 是实际 Match 事件的位置，不依赖界面当前的排序或选择。</summary>
public sealed record AcMatchRow(string Pattern, string Range, int StepIndex);

/// <summary>检查器的一次只读投影，避免多个属性分别沿 fail 链重复计算或读到不同步骤。</summary>
public sealed record AcInspection(string Prefix, string Failure, string OwnOutput, string InheritedOutput, string SuffixExplanation);

/// <summary>
/// 将领域数据转换成界面需要的文字和索引；不修改领域状态，不拥有计时器或 Document。
/// 两个直接的纯函数足够表达职责，无需再为投影创建接口、工厂或可替换策略。
/// </summary>
public static class AcTraceProjection
{
    public static ImmutableArray<AcMatchRow> CreateMatchRows(AcTrace trace)
    {
        var rows = ImmutableArray.CreateBuilder<AcMatchRow>(trace.Matches.Length);
        for (var i = 0; i < trace.Steps.Length; i++)
        {
            var step = trace.Steps[i];
            if (step.Action != AcAction.Match) continue;
            var match = trace.Matches[step.MatchCount - 1];
            rows.Add(new(match.Pattern, $"[{match.Start}, {match.End})", i));
        }
        return rows.ToImmutable();
    }

    public static AcInspection Inspect(AcTrace trace, AcStep step, int selectedNodeId)
    {
        var id = Math.Clamp(selectedNodeId, 0, step.VisibleNodes - 1);
        var node = trace.Nodes[id];
        var target = step.Failures[id];
        var prefix = id == 0 ? "ε" : node.Prefix;
        var failure = target < 0 ? "尚未建立" : target == 0 ? "ε" : trace.Nodes[target].Prefix;
        var own = node.PatternId >= 0 && node.PatternId < step.CompletedPatterns ? trace.Patterns[node.PatternId] : "∅";
        var outputs = new List<string>();
        for (var next = target; next > 0; next = step.Failures[next])
        {
            var pattern = trace.Nodes[next].PatternId;
            if (pattern >= 0 && pattern < step.CompletedPatterns) outputs.Add(trace.Patterns[pattern]);
        }
        var explanation = id == 0 ? "ε 是空前缀。根的 fail 指向自身。"
            : target < 0 ? "构建 fail 阶段会寻找同时也是词典前缀的最长真后缀。"
            : $"{node.Prefix} = {node.Prefix[..(node.Prefix.Length - trace.Nodes[target].Prefix.Length)]} + {failure}\n保留后缀：{failure}";
        return new(prefix, failure, own, outputs.Count == 0 ? "∅" : string.Join(" · ", outputs), explanation);
    }
}
