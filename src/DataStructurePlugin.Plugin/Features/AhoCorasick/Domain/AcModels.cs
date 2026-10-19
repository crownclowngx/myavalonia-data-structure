using System.Collections.Immutable;

namespace DataStructurePlugin.Features.AhoCorasick.Domain;

/// <summary>教学过程的三个阶段；独立于窗口、控件及播放速度。</summary>
public enum AcStage { Trie, Failure, Search }

/// <summary>可回放的算法事件。一次字符处理可以产生多次回退和多条输出。</summary>
public enum AcAction { Ready, Insert, Reuse, Terminal, Dequeue, Candidate, SetFailure, Read, Fallback, Transition, Match, Complete }

/// <summary>构建完成后的只读状态。边以 Unicode 码点为键，状态身份由插入次序决定。</summary>
public sealed record AcNode(int Id, string Prefix, int Depth, ImmutableDictionary<int, int> Edges, int PatternId);

/// <summary>原文中的一次匹配。Start/End 为 UTF-16 半开区间，EndSymbol 为最后一个码点的序号。</summary>
public sealed record AcMatch(int PatternId, string Pattern, int Start, int End, int EndSymbol);

/// <summary>扫描用的一个 Unicode 码点及其原文位置；Offset/Length 均按 UTF-16 单元计数。</summary>
public sealed record AcSymbol(string Text, int Offset, int Length);

/// <summary>
/// 一个逻辑步骤的完整观察状态。集合使用不可变容器，调用方不能改写 fail、队列或其他历史记录。
/// 节点及边本身由 AcTrace 共享，VisibleNodes/CompletedPatterns 决定当前已经建立的部分，
/// 因而回看 Trie 生长时不会提前看到尚未创建的节点或终止标记。
/// </summary>
public sealed record AcStep(
    AcStage Stage, AcAction Action, string Title, string Explanation,
    int State, int? Candidate, int VisibleNodes, int CompletedPatterns,
    ImmutableArray<int> Failures, ImmutableArray<int> Queue,
    int ConsumedSymbols, int ActiveSymbol, int MatchCount,
    int? EdgeFrom = null, int? EdgeTo = null);

/// <summary>一次成功实验的不可变结果。仅包含数据，可以同时供 UI、测试或将来的其他呈现端读取。</summary>
public sealed record AcTrace(ImmutableArray<string> Patterns, string Text, ImmutableArray<AcSymbol> Symbols,
    ImmutableArray<AcNode> Nodes, ImmutableArray<AcStep> Steps, ImmutableArray<AcMatch> Matches)
{
    public int StageStart(AcStage stage) => FindStep(0, step => step.Stage == stage);

    /// <summary>查找一个语义事件；无匹配返回 -1，允许起点等于步骤数量以表达“已经到末尾”。</summary>
    public int FindStep(int start, Func<AcStep, bool> predicate)
    {
        for (var i = Math.Clamp(start, 0, Steps.Length); i < Steps.Length; i++)
            if (predicate(Steps[i])) return i;
        return -1;
    }
}

/// <summary>
/// Document 需要的最小算法能力：给定输入，返回完整轨迹或抛出输入异常。
/// 不要求 UI 了解构建过程、队列、输出链接或具体实现，测试也可注入会失败的构建器检查提交边界。
/// </summary>
public interface IAcTraceBuilder
{
    AcTrace Build(string keywordText, string text);
}
