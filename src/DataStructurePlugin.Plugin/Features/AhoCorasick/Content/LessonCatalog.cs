using System.Collections.Immutable;
using DataStructurePlugin.Features.AhoCorasick.Domain;

namespace DataStructurePlugin.Features.AhoCorasick.Content;

/// <summary>一个章节的演示锚点。语义锚点跟随内容定义，调整章节编号不需要修改 Document 分支。</summary>
public sealed record AcDemoTarget(AcAction Action, string? StatePrefix = null, string? Pattern = null);

/// <summary>不可变的课堂内容，独立于用户实验；演示时显式载入经典输入，再定位到语义锚点。</summary>
public sealed record AcLesson(string Number, string Title, string Eyebrow, string Question,
    string Introduction, string Explanation, string Formula, string FormulaExplanation,
    string Proof, string Challenge, ImmutableArray<string> Answers, int CorrectAnswer, string Feedback,
    AcStage Stage, string Pseudocode, AcDemoTarget? Demo = null)
{
    public int FindDemoStep(AcTrace trace)
    {
        var index = Demo is null ? -1 : trace.FindStep(0, step => step.Action == Demo.Action
            && (Demo.StatePrefix is null || trace.Nodes[step.State].Prefix == Demo.StatePrefix)
            && (Demo.Pattern is null || step.MatchCount > 0 && trace.Matches[step.MatchCount - 1].Pattern == Demo.Pattern));
        return index >= 0 ? index : Math.Max(0, trace.StageStart(Stage));
    }
}

public static class LessonCatalog
{
    public static ImmutableArray<AcLesson> All { get; } = [
        new("01", "从问题出发", "THE BIG PICTURE", "如何一次找出一句话里的多个关键词？",
            "把关键词词典预处理成一台带输出的状态机，再让文本从左到右经过它。课堂贯穿使用 he、she、his、hers 与文本 ushers。",
            "逐词搜索会对相同文本重复做工作。AC 自动机把公共前缀存成 Trie，再利用后缀关系复用已读信息。这里默认精确、区分大小写匹配，允许重叠，并报告全部出现位置。",
            "P = {he, she, his, hers}     T = ushers\n结果：she [1,4) · he [2,4) · hers [2,6)",
            "区间从 0 开始，右端不包含在结果中。扫描按 Unicode 码点前进；结果索引采用原文 UTF-16 单元，与 C# / Java 字符串切片对应。",
            "设 M 是关键词总长度，N 是文本长度，Z 是报告的匹配次数。预处理词典的代价可以由多次扫描分摊；是否值得使用 AC，还取决于词典规模和复用次数。",
            "课堂例子 ushers 一共有多少次匹配？", ["1 次，只报告最长词", "3 次，包含后缀与重叠", "4 次，每个关键词一次"], 1,
            "she、he、hers 各出现一次；his 没有出现。重叠不应自动排除。", AcStage.Search,
            "automaton = build(patterns)\nstate = root\nfor character in text:\n    state = advance(state, character)\n    report all outputs(state)"),
        new("02", "让 Trie 生长", "SHARED PREFIXES", "多个关键词如何共享一条路径？",
            "沿字符从根向下走。存在的边直接复用，不存在的边才创建。走到一个完整关键词的结尾时，给节点加上终止标记。",
            "节点上的字符串表示从根走到这里的整条路径；边上的字符表示一次读入。he 的结尾同时也是 hers 的中途，因此“是终止节点”和“还有孩子”可以同时成立。",
            "Q = {ε} ∪ Prefixes(P)\n|Q| ≤ M + 1",
            "ε 是空前缀。每插入一个字符，最多新建一个状态，因此所有状态数不超过关键词总长度加一。",
            "从空树开始归纳：插入一条长度为 m 的词最多增加 m 个节点；复用已有边只会让节点数量更少。终止标记属于关键词身份，不是额外的字符边。",
            "插入 he 后再插入 hers，需要新建几个节点？", ["4 个", "2 个：her 与 hers", "0 个"], 1,
            "h 和 he 已存在，只需沿 r、s 各建立一个节点。", AcStage.Trie,
            "for word in patterns:\n    state = root\n    for character in word:\n        state = existing edge or new node\n    mark state as terminal"),
        new("03", "理解 fail", "KEEP THE USEFUL SUFFIX", "为什么 fail(she) 指向 he？",
            "匹配失败后，保留的应该是已经读过的内容中，还可能继续匹配的后缀。fail 寻找“同时也是词典前缀的最长真后缀”。",
            "she 的真后缀依次是 he、e、ε。其中 he 是词典前缀，因此 fail(she)=he。普通父节点 sh 是前缀而不是后缀，无法代表文本末尾已经读到的内容。",
            "L(fail(q)) = 最长的 s\n满足：s 是 L(q) 的真后缀，且 s ∈ Prefixes(P) ∪ {ε}",
            "L(q) 表示状态 q 的路径字符串。根节点是特殊约定：fail(root)=root。点选图中的节点，可以在右侧检查它的后缀关系。",
            "非根状态的 fail 总是指向严格更短的路径，因此连续回退必然到达根。保留最长候选后，再次失配时仍可沿 fail 枚举更短候选，不会跳过可能的后缀。",
            "she 在读取 r 时没有可用边，应该先去哪里？", ["sh，回到父节点", "he，保留有用后缀", "重新读取文本开头"], 1,
            "先沿 fail 到 he，再尝试同一个 r，就可以到达 her；文本游标不后退。", AcStage.Failure,
            "candidate = fail(parent)\nwhile candidate ≠ root and no edge(c):\n    candidate = fail(candidate)\nfail(child) = edge(candidate, c) or root", new(AcAction.SetFailure, StatePrefix: "she")),
        new("04", "按层构建", "BREADTH FIRST", "为什么用 BFS 构建 fail？",
            "深层节点的 fail 依赖较短前缀的后缀信息。BFS 先处理浅层，再处理深层，保证要使用的 fail 已经确定。",
            "先让根的直接子节点指向根并入队。每次取出父节点，沿它的每条边为子节点寻找 fail。实验中的队列从左到右表示即将处理的顺序。",
            "对 Trie 边 u ─c→ v：\nfail(v) = δ(fail(u), c)    （u ≠ root）",
            "δ 包含必要的 fail 回退。根的子节点需单独初始化，不能直接套用一般递推式，否则可能得到自身链接。",
            "fail(u) 的深度小于 u；从 fail(u) 尝试字符 c 所依赖的边和 fail 都位于已建立的层级。BFS 的层序保证这些依赖先于 v 被处理。",
            "构建某个节点的 fail 前，最关键的已知信息是什么？", ["父节点的 fail 已确定", "全部文本已扫描", "所有叶节点已处理"], 0,
            "BFS 保证父节点及更浅层的后缀信息先就绪。构建自动机不依赖待扫描文本。", AcStage.Failure,
            "queue = root.children\nwhile queue is not empty:\n    u = queue.dequeue()\n    for (c, v) in u.children:\n        build fail(v) from fail(u)\n        queue.enqueue(v)"),
        new("05", "一次多个输出", "SUFFIX MATCHES", "到了 she，为什么还要报告 he？",
            "当前路径的结尾可能同时结束多个关键词。到达 she 时，she 是自身终止词，he 是它的后缀终止词，两者都应报告。",
            "不能只检查当前节点。实现使用输出链接跳到下一个终止后缀节点，逐项枚举结果，避免把完整继承输出列表复制到每一个节点。",
            "Out(q) = Own(q) ∪ Out(fail(q))\nOut(she) = {she} ∪ {he}",
            "这是输出集合的概念定义。实现可以通过输出链接求得同一组结果，不要求真的复制集合。根的输出为空，本版不接收空关键词。",
            "如果词 w 在当前位置结束，它一定是当前已读文本的后缀。状态不变量与 fail 链会覆盖仍是词典前缀的后缀；枚举其中的终止节点，就能报告全部关键词。",
            "P={a, aa, aaa}，T=aaa，共应报告几次？", ["3 次", "6 次", "1 次"], 1,
            "a 出现 3 次、aa 出现 2 次、aaa 出现 1 次，共 6 次。", AcStage.Search,
            "if state is terminal: emit(state)\noutput = outputLink(state)\nwhile output exists:\n    emit(output)\n    output = outputLink(output)", new(AcAction.Match, Pattern: "he")),
        new("06", "逐字符扫描", "THE STATE INVARIANT", "回退了状态，为什么不用回退文本？",
            "读入一个字符时先尝试当前 Trie 边。没有边就沿 fail 换一个较短后缀，继续尝试同一个字符；成功转移或在根处失败后，才消费这个字符。",
            "扫描 ushers 的 r 时，she 无法继续，先退到 he，再沿 r 到 her。之前读过的字符所包含的信息已经编码在状态里，无需重新扫描。",
            "读完 T[0..i) 后：\nL(q) = 已读文本后缀中，同时也是词典前缀的最长者",
            "这是核心状态不变量。实验中的“已消费字符数”与“当前尝试字符”分开显示，回退阶段前者保持不变。",
            "初始状态 ε 满足不变量。若能直接延伸，它就是最长候选；若不能，则依次尝试 fail 链上的较短候选，首次能延伸者最长；全部失败时回到 ε。因此每次消费字符后不变量保持成立。",
            "沿 fail 连续回退两次时，文本游标会怎样？", ["后退两格", "保持不动，仍尝试当前字符", "跳过当前字符"], 1,
            "只有状态回退。字符必须等转移完成或根节点确认无边后才被消费。", AcStage.Search,
            "for c in text:\n    while state ≠ root and no edge(c):\n        state = fail(state)\n    state = edge(state, c) or root\n    emit own and suffix matches", new(AcAction.Fallback)),
        new("07", "数学与代价", "WHY IT WORKS", "一次可以回退很多步，整体为什么仍然快？",
            "成功沿 Trie 边前进一步，状态深度最多增加 1；沿 fail 回退，深度至少减少 1。深度不能小于 0，因此总回退次数受总前进次数约束。",
            "在转移查询为常数时间、通过输出链接枚举命中的条件下，扫描代价为 O(N+Z)。这里的 Z 不能省略：重复字符和重叠关键词可以产生大量结果。",
            "Φ(q) = depth(q) ≥ 0\n扫描：O(N + Z)    状态数：|Q| ≤ M + 1",
            "哈希转移提供期望常数查询；有序映射等表示需要额外考虑查询成本。构建代价取决于边表示和回退策略，不能把所有实现都笼统写成 O(M)。",
            "可靠性：每次报告的关键词都对应当前位置结尾的真实路径。完备性：状态不变量配合输出链接覆盖全部终止后缀。势能分析说明，扫描中的总回退次数为线性量级。UI 轨迹还包含教学事件和快照，生成、绘制的开销不等于匹配器本身的算法代价。",
            "为什么复杂度中需要保留 Z？", ["因为输出每条匹配都需要工作", "因为 BFS 队列长度叫 Z", "因为图形动画总是慢"], 0,
            "即使扫描文本很快，也至少要为报告的 Z 条匹配付出相应代价。", AcStage.Search,
            "each consumed character: depth increases ≤ 1\neach fallback: depth decreases ≥ 1\nnumber of fallbacks ≤ total increases\nreporting costs O(Z)"),
        new("08", "实现与边界", "FROM MODEL TO CODE", "怎样把图、数学对象和工程字段对应起来？",
            "节点对应状态，children 对应 Trie 边，fail 对应最长真后缀，outputLink 对应下一终止后缀。图与伪代码描述同一个实际执行过程。",
            "本版在 C# 中执行确定性教学内核，伪代码用于语言无关的对照。Java 工程实现可以采用同样的 Builder、只读 Automaton、每次扫描独立 Matcher 的职责划分。",
            "Match = (patternId, start, end)\ntext[start..end] = matched pattern",
            "start/end 为 UTF-16 半开区间。🙂 是一个码点、两个 UTF-16 单元。本版不做大小写折叠或 Unicode 规范化，也不把组合字形合并为一个扫描单元。",
            "词典每行一个，去掉两端空白并合并重复词；空行忽略，空文本合法。限制 40 个词、160 个状态、600 个文本码点和 12,000 个教学步骤，超限明确拒绝，不返回部分结果。后续 Java 运行、流式分块和网页发布可在这一语义基础上增加。",
            "文本 🙂a 中，关键词 a 的 UTF-16 区间是什么？", ["[1,2)", "[2,3)", "[0,1)"], 1,
            "🙂 占两个 UTF-16 单元，因此 a 的起点为 2；按码点计数时它是第二个字符。", AcStage.Search,
            "class Node { children; fail; outputLink; terminal; }\nclass Match { patternId; start; end; }\nBuilder: construct a read-only automaton\nMatcher: hold one scan's state\nJava String indices also use UTF-16")
    ];
}
