using System.Collections.Immutable;
using System.Text;

namespace DataStructurePlugin.Features.AhoCorasick.Domain;

/// <summary>
/// 无状态的教学轨迹构建器：每次调用独享节点、队列和输出链接，可安全复用服务实例。
/// 朴素地按 Trie、BFS fail、扫描三个阶段执行；不引入策略工厂或通用算法框架。
/// 教学事件记录的是当前执行事实，播放层只消费它们，不参与算法决策。
/// </summary>
public sealed class AhoCorasickTraceBuilder : IAcTraceBuilder
{
    private sealed class Node(int id, string prefix, int depth)
    {
        public int Id { get; } = id;
        public string Prefix { get; } = prefix;
        public int Depth { get; } = depth;
        public Dictionary<int, int> Edges { get; } = [];
        public int PatternId { get; set; } = -1;
    }

    public AcTrace Build(string keywordText, string text)
    {
        var input = AcInput.Parse(keywordText, text);
        var patterns = input.Patterns;
        var patternSymbols = input.PatternSymbols;
        var symbols = input.Symbols;

        var nodes = new List<Node> { new(0, "", 0) };
        var steps = new List<AcStep>();
        var matches = new List<AcMatch>();
        var fail = Enumerable.Repeat(-1, AcLimits.MaximumNodes).ToImmutableArray().SetItem(0, 0);
        var outputLink = Enumerable.Repeat(-1, AcLimits.MaximumNodes).ToArray();
        var queue = new Queue<int>();
        var completed = 0;
        var consumed = 0;
        var active = -1;

        void Emit(AcStage stage, AcAction action, string title, string explanation, int state = 0,
            int? candidate = null, int? from = null, int? to = null)
        {
            if (steps.Count >= AcLimits.MaximumSteps)
                throw new ArgumentException("演示超过 12,000 步，请减少关键词或文本。未生成截断结果。");
            steps.Add(new(stage, action, title, explanation, state, candidate, nodes.Count, completed,
                fail, queue.ToImmutableArray(), consumed, active, matches.Count, from, to));
        }

        void SetFailure(int node, int target)
        {
            // 不可变更新允许旧步骤安全共享旧版本；更新新节点不会改变之前的 fail 快照。
            fail = fail.SetItem(node, target);
            // 输出链接只跳向终止后缀，扫描时按实际结果数枚举，避免复制继承输出集合。
            outputLink[node] = nodes[target].PatternId >= 0 ? target : outputLink[target];
        }

        // 阶段一：节点按照实际创建次序编号，已存在的前缀只复用边。
        Emit(AcStage.Trie, AcAction.Ready, "从一个根节点开始", "根节点 ε 代表空前缀。依次插入关键词，共同前缀只保存一份。");
        for (var p = 0; p < patterns.Length; p++)
        {
            var state = 0;
            foreach (var symbol in patternSymbols[p])
            {
                var key = Rune.GetRuneAt(symbol.Text, 0).Value;
                var previous = state;
                if (!nodes[state].Edges.TryGetValue(key, out var next))
                {
                    if (nodes.Count >= AcLimits.MaximumNodes)
                        throw new ArgumentException("教学画布最多 160 个状态，请减少关键词或公共前缀以外的字符。");
                    next = nodes.Count;
                    nodes[state].Edges.Add(key, next);
                    nodes.Add(new(next, nodes[state].Prefix + symbol.Text, nodes[state].Depth + 1));
                    state = next;
                    Emit(AcStage.Trie, AcAction.Insert, $"插入「{patterns[p]}」· 新建 {symbol.Text}",
                        $"从 {Display(nodes[previous].Prefix)} 沿字符「{symbol.Text}」建立新节点。节点表示前缀 {Display(nodes[state].Prefix)}。",
                        state, from: previous, to: state);
                }
                else
                {
                    state = next;
                    Emit(AcStage.Trie, AcAction.Reuse, $"插入「{patterns[p]}」· 复用前缀",
                        $"路径 {Display(nodes[state].Prefix)} 已经存在，直接复用。共享前缀不需要重复创建。", state, from: previous, to: state);
                }
            }
            nodes[state].PatternId = p;
            completed++;
            Emit(AcStage.Trie, AcAction.Terminal, $"标记完整关键词「{patterns[p]}」",
                "双圆环代表关键词的结尾。一个节点可以同时是完整关键词和更长关键词的前缀。", state);
        }

        // 阶段二：根的孩子单独初始化。BFS 保证处理一个孩子前，所依赖的较短后缀已就绪。
        Emit(AcStage.Failure, AcAction.Ready, "按 BFS 层序构建 fail", "先确定浅层状态，再处理深层状态。根节点的 fail 指向自身；根的直接子节点指向根。");
        foreach (var child in nodes[0].Edges.Values)
        {
            SetFailure(child, 0);
            queue.Enqueue(child);
            Emit(AcStage.Failure, AcAction.SetFailure, $"fail({Display(nodes[child].Prefix)}) = ε",
                "长度为 1 的前缀没有非空真后缀，因此 fail 指向根，并将该节点加入 BFS 队列。", child, 0, child, 0);
        }
        while (queue.TryDequeue(out var parent))
        {
            Emit(AcStage.Failure, AcAction.Dequeue, $"出队 {Display(nodes[parent].Prefix)}",
                "检查它的每条 Trie 边。父节点的 fail 已经确定，可以沿已知后缀寻找更长的候选。", parent);
            foreach (var (key, child) in nodes[parent].Edges)
            {
                var candidate = fail[parent];
                Emit(AcStage.Failure, AcAction.Candidate, $"为 {Display(nodes[child].Prefix)} 寻找后缀",
                    $"从父节点的 fail：{Display(nodes[candidate].Prefix)} 开始，检查能否接上字符「{new Rune(key)}」。", child, candidate);
                while (candidate != 0 && !nodes[candidate].Edges.ContainsKey(key))
                {
                    var old = candidate;
                    candidate = fail[candidate];
                    Emit(AcStage.Failure, AcAction.Candidate, "候选无法延伸，继续检查更短后缀",
                        $"{Display(nodes[old].Prefix)} 没有字符「{new Rune(key)}」的边，沿它的 fail 到 {Display(nodes[candidate].Prefix)}。",
                        child, candidate, old, candidate);
                }
                var target = nodes[candidate].Edges.TryGetValue(key, out var found) ? found : 0;
                SetFailure(child, target);
                queue.Enqueue(child);
                Emit(AcStage.Failure, AcAction.SetFailure, $"fail({Display(nodes[child].Prefix)}) = {Display(nodes[target].Prefix)}",
                    "找到同时也是词典前缀的最长真后缀。保存 fail 和输出链接，再把当前节点入队。", child, target, child, target);
            }
        }

        // 阶段三：只有 Transition 事件消费字符；Fallback 事件绝不改变文本游标。
        Emit(AcStage.Search, AcAction.Ready, "自动机已就绪，从左到右扫描文本",
            "每次读取一个 Unicode 码点。沿 fail 回退时，文本游标不后退；匹配结果使用原文 UTF-16 半开区间 [start, end)。");
        var current = 0;
        for (var i = 0; i < symbols.Length; i++)
        {
            active = i;
            var symbol = symbols[i];
            var key = Rune.GetRuneAt(symbol.Text, 0).Value;
            Emit(AcStage.Search, AcAction.Read, $"读取第 {i + 1} 个字符「{symbol.Text}」",
                $"当前状态 {Display(nodes[current].Prefix)}。先尝试 Trie 边；本字符原文区间 [{symbol.Offset}, {symbol.Offset + symbol.Length})。", current);
            // 比较本身也是可以暂停的教学事件。每次沿 fail 换状态后，必须重新比较同一个字符，
            // 不能把整个 while 的多次尝试压缩成一次“跳到最终状态”，否则学习者看不到判断依据。
            while (true)
            {
                var hasEdge = nodes[current].Edges.TryGetValue(key, out var target);
                Emit(AcStage.Search, AcAction.Compare, $"比较「{symbol.Text}」与 {Display(nodes[current].Prefix)} 的出边",
                    hasEdge
                        ? $"存在「{symbol.Text}」的 Trie 边，可以到达 {Display(nodes[target].Prefix)}。下一步才执行转移并消费字符。"
                        : current == 0
                            ? $"根节点没有「{symbol.Text}」的边。下一步留在根并消费此字符。"
                            : $"当前没有「{symbol.Text}」的边。下一步沿 fail 回退，然后继续比较同一个字符；文本游标保持不动。",
                    current, hasEdge ? target : null, hasEdge ? current : null, hasEdge ? target : null);
                if (hasEdge || current == 0) break;
                var previous = current;
                current = fail[current];
                Emit(AcStage.Search, AcAction.Fallback, $"失配：{Display(nodes[previous].Prefix)} → {Display(nodes[current].Prefix)}",
                    $"没有字符「{symbol.Text}」的边，保留仍有用的后缀 {Display(nodes[current].Prefix)}。文本游标不动，继续尝试同一个字符。",
                    current, from: previous, to: current);
            }
            var from = current;
            current = nodes[current].Edges.TryGetValue(key, out var next) ? next : 0;
            consumed = i + 1;
            Emit(AcStage.Search, AcAction.Transition, current == 0 ? "根节点也无法延伸，消费此字符" : $"转移到 {Display(nodes[current].Prefix)}",
                "本字符处理完成。当前状态表示已读文本的后缀中，同时也是词典前缀的最长者。", current, from: from, to: current);
            for (var output = current; output >= 0; output = outputLink[output])
            {
                var id = nodes[output].PatternId;
                if (id < 0) continue;
                var end = symbol.Offset + symbol.Length;
                matches.Add(new(id, patterns[id], end - patterns[id].Length, end, i));
                Emit(AcStage.Search, AcAction.Match, $"命中「{patterns[id]}」",
                    output == current
                        ? $"当前节点本身是终止节点，报告 [{end - patterns[id].Length}, {end})。继续检查输出链接，允许重叠和后缀命中。"
                        : $"从后缀终止节点 {Display(nodes[output].Prefix)} 找到额外命中，报告 [{end - patterns[id].Length}, {end})。扫描状态仍保留在 {Display(nodes[current].Prefix)}。",
                    current, output);
            }
        }
        active = -1;
        Emit(AcStage.Search, AcAction.Complete, $"扫描完成 · 共 {matches.Count} 次命中",
            "已报告全部重叠匹配。可以点击结果回到产生它的步骤，或编辑输入开始新的实验。", current);
        return new(patterns, text, symbols,
            nodes.Select(n => new AcNode(n.Id, n.Prefix, n.Depth, n.Edges.ToImmutableDictionary(), n.PatternId)).ToImmutableArray(),
            steps.ToImmutableArray(), matches.ToImmutableArray());
    }

    private static string Display(string prefix) => prefix.Length == 0 ? "ε" : $"「{prefix}」";
}
