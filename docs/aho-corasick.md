# AC 自动机 · 交互课堂

## 使用

运行 `dotnet run --project src/DataStructurePlugin.Standalone`，或在 Host 的 Document 入口打开
“AC 自动机 · 交互课堂”。Standalone 复用同一份 Document 与 View，不复制算法或教学逻辑。

1. **跟着学**：选一个章节，阅读例子、公式，按需展开证明，并回答预测题。
   **完整教程**：Document 顶部提供独立阅读区，包含 16 章长文、目录与前后章导航。
   正文维护在 [完整 Markdown 教程](tutorials/aho-corasick-tutorial.md)，随插件嵌入，离线可读；
   8 张 Mermaid 图支持深浅主题、缩放与展开源码。进入教程暂停播放，返回课堂保留实验、草稿与进度。
2. **演示这一节**：显式载入经典词典 `he / she / his / hers` 与 `ushers`，定位到相应关键事件。
   教材和练习始终讲述课堂例子；自定义实验不会偷偷替换教材中的例子与结论。
3. **自己试**：每行输入一个关键词，输入待匹配文本，再点击“应用输入 · 从根开始”。
4. 用阶段按钮、时间轴、前后单步、关键事件跳转或播放观察过程。点击节点检查 fail、
   自身终止词和后缀输出；根节点的 fail 连线只在相关节点选中或当前回退时显示。
5. 点击已发现的匹配结果，跳回它的报告步骤。“扫描至结尾”展示全部结果。

画布支持滚轮缩放、鼠标拖动平移和“适应画布”；画布获得焦点后按 0 复位。
宽窗口显示章节侧栏与检查器；窄窗口改用章节下拉框并将输入与检查器排列到画布下方。
播放条固定在底部。编辑输入、手动跳转、切换章节或移除 View 会暂停播放。

画布旁也有“从根重新构建”“上一步”和单步按钮。应用输入后停在只有根节点的第 1 步，
按词典顺序逐字符新建或复用前缀节点，词尾标记单独出现；BFS 阶段再逐层确定 fail。
统计显示“已长出 / 总状态数”，总数仅作为进度参考，不代表节点已经全部显示。
扫描时将读取、比较、fail 回退、再次比较、消费字符和每一条输出拆开；
同一位置的 she、he 会分两步报告。回看时会撤去后来建立的节点、fail 和结果。
“下一次命中”和“扫描至结尾”是显式快进；想看完整判断过程请使用“下一步”。

课堂继承 Host 的深色或浅色主题；Standalone 继承 Avalonia 的应用主题。
页面、卡片、输入、公式、提示、图中节点/连线及文本带都会实时更新，不重置实验进度。
配色限定在 AC 的 View 内，其他 Document 不受影响。

## 输入与匹配语义

- 每行一个关键词；去除两端空白，忽略空行，以 ordinal 语义合并重复词。
- 精确、区分大小写，允许重叠，报告全部出现位置；不做 Unicode 规范化或大小写折叠。
- 按 Unicode 码点扫描，结果采用原文 UTF-16 半开区间 `[start,end)`。
  例如 `🙂a` 中 `a` 的结果为 `[2,3)`；组合字形仍可能包含多个码点。
- 空文本合法；空词典、非法代理序列和超限输入明确报错。
- 上限：40 个去重关键词、2,000 个原始词典 UTF-16 单元、160 个状态、600 个文本码点、
  2,400 个原始文本 UTF-16 单元、12,000 个教学步骤。超限不提交半成品。
- 编辑仅产生草稿；成功构建前，画布仍对应上次应用的输入，并显示明确提示。
  构建失败保留上一份成功轨迹。

## 独立域与 Document 边界

```text
Features/AhoCorasick/
  AhoCorasickIds.cs             # 本域 Document 身份
  AhoCorasickDocument.cs        # 本实例的编辑、阅读、回放与选择状态
  AhoCorasickView.axaml[.cs]    # 展示、响应式布局、输入转发
  Domain/                      # 输入、预算、不可变模型、AC 轨迹构建器
  Application/                 # 播放会话与状态投影，不依赖 Avalonia
  Infrastructure/              # Avalonia UI 时钟适配器
  Content/AcExampleCatalog.cs  # 不可变课堂预设
  Content/LessonCatalog.cs     # 本域章节、数学、伪代码和练习
  Controls/AutomatonCanvas.cs  # AC 图绘制与节点命中检查
  Controls/TextTape.cs         # Unicode 文本带与最近一次命中标注
  Controls/AcThemeResources.* # 本域深浅主题的语义画刷
  Controls/AcDrawingPalette.cs # 两个自绘控件的不可变画笔集合
```

Document ID 为 `myavalonia.plugin.data.structure.document.aho-corasick`；插件 ID 维持
`myavalonia.plugin.data.structure`。模板 `.document.main` 入口已在本首版替换。
每个 Document 都有自己的输入、轨迹、播放时钟、章节、选择与结果。
没有跨 Document 可变单例、Tool 或全局算法状态。

后续 AVL、排序、图、动态规划等分别建立 `Features/<算法域>` 与自己的 Document。
AC 的节点、fail、输出、章节和控件只属于 AC；先由第二个专题验证真实共性，再提取必要的
公共播放或教学组件，不建立一个包含所有算法分支的 MainDocument。

领域算法与播放会话不引用 Avalonia：每次运行生成语义步骤，记录当时的可见节点、终止词、fail、
队列、文本游标、输出数量和解释。所有公开集合不可变，fail 更新产生新版本，历史步骤不会被改变。
搜索使用输出链接枚举终止后缀，避免在节点上复制完整继承输出集合。
界面按步骤投影；回看不会重新运行随机过程或只撤销一部分画面。

## 当前范围

第一版提供可运行的本地学习与演示。数学以可选择复制的 Unicode 公式呈现，使用 Cambria Math
等字体，不依赖浏览器或外部公式服务。代码区是语言无关伪代码，实际算法在 C# 执行；
未宣称执行 Java 代码。练习是预先编写的课堂题目，不会对任意自定义输入自动出题。

当前使用内存会话，不提供实验文件保存、Host 内容快照恢复、网页发布或在线分享；
“匹配结果跳转”与任意时间轴回放已可用。播放逐个展示逻辑事件，尚未增加边运动插值动画。
教学轨迹和渲染有额外开销，其耗时不作为纯匹配器的性能基准。

完整教程是本 Document 的一个阅读模式，不增加第二个 Document 或 Tool。窄窗口使用章节下拉框；
切换模式保留阅读位置，换章回到章首。教程使用 Markdig 解析并通过 Avalonia 原生控件展示，
Mermaid 图从 Markdown 源码预生成后嵌入，维护方法见 [图示生成说明](../tools/tutorial-assets/README.md)。
公式为 Unicode 文本，不宣称支持任意 LaTeX；运行时不执行 Markdown 内的 HTML 或脚本。

设计参考：[Red Blob Games](https://www.redblobgames.com/pathfinding/a-star/introduction.html)、
[VisuAlgo](https://visualgo.net/en/dfsbfs)、[Brilliant](https://brilliant.org/cs/)、
[Algorithm Visualizer](https://github.com/algorithm-visualizer/algorithm-visualizer)、
[modern-ahocorasick](https://aho.icebreaker.top/visualization)。本版为独立 C#/Avalonia 实现，
没有复制上述项目的源码、文章或配图。

## 验证

完整本地 Debug 门禁与审核修复见 [G0001 测试记录](refactoring/G0001/testing.md)；
SOLID 边界与所有权设计见 [G0001 实施设计](refactoring/G0001/implementation.md)。
深浅主题、逐步比较及后续本地验证见 [G0002 专项说明](refactoring/G0002/README.md)。

```powershell
dotnet build DataStructurePlugin.slnx -c Debug -warnaserror
dotnet test tests/DataStructurePlugin.Tests/DataStructurePlugin.Tests.csproj -c Debug --no-build
```

测试包括经典/中文/重叠/Unicode/空文本、随机朴素算法对照、fail 最长后缀性质、
扫描状态不变量、回退时游标不变、资源预算、Document 实例隔离、错误保留旧轨迹、
章节与结果定位，以及真实 AXAML 的 Headless/Skia 渲染和页面移除时停止播放。

设置 `AC_SCREENSHOT_DIR` 可让 UI 测试输出 `reading.png`、`experiment.png` 和 `narrow.png`。
这些验证不替代真实 Host 的发现、Dock、生命周期和正式 ZIP 验收。

2026-10-19 首版验证曾通过 15 项测试；再次审核的最终计数以 G0001 测试记录为准。
首次还原发现模板锁文件中的两个 SDK 3.4.1 哈希与本机包内容不同，使用
`dotnet restore --force-evaluate` 重新生成锁文件；未改变 SDK 版本。
