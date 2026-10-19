# G0001 审核发现与实施设计

## 审核结论与修复

此次再次审核以当前未提交的首版改动为对象，发现并修正以下问题。这里记录的是已经落地的
代码与回归约束；完整门禁结果以 [测试记录](testing.md) 和生成的 summary.json 为准。

| 问题 | 原实现的影响 | 修复及对应回归 |
| --- | --- | --- |
| P2：轨迹公开可写数组 | 外部调用方可修改历史 fail、队列、步骤或结果，破坏回放事实 | 全部公开集合改为 ImmutableArray/ImmutableDictionary；测试普通 IList/IDictionary 写入会拒绝 |
| P2：旧匹配结果可导航新实验 | SelectedMatch 不验证归属，旧绑定通知可以把新轨迹跳到错误位置 | 保持结果集合身份，增量维护可见前缀；仅当前可见的同一行对象有导航权 |
| P2：Document 同时拥有定时器和多种投影 | UI 计时难以独立测试，页面模型修改理由过多 | 提取 AcPlaybackSession、IAcPlaybackClock、AcTraceProjection；Document 负责页面协调 |
| P2：游标和节点入口未规范化 | NaN/无穷可造成非法索引或计时间隔，负节点号可越界 | 播放会话拒绝非有限值，有限值有界化；检查节点号限制在当前可见范围 |
| P2：View 更换模型未暂停旧实例 | 复用 View 时，旧 Document 仍可能后台播放 | DataContext 更换和 View 移除都暂停此前的实例；加入真实 View 回归 |
| P2：资源释放与迟到节拍覆盖不足 | 暂停/关闭后排队的节拍可能被误当成有效输入 | 播放推进检查 IsPlaying/disposed；释放成对退订，构造失败也清理时钟 |
| P3：按章节编号写导航分支 | 章节重排需要修改 Document | 演示锚点随内容定义；调整 Number 后仍定位同一语义事件 |
| P3：渲染依赖字典次序及固定命中半径 | 结构可能重排，缩放后点击可能选到邻近节点 | 按创建编号固定布局，按实际半径寻找最近节点，捕获丢失时结束拖动 |
| P2：缺少可重复开发门禁 | 分散命令不足以保存失败与验证范围 | 新增 verify-g0001.ps1，任何一步失败即停止并写出失败摘要，测试跳过或零测试不能通过 |

## 朴素职责划分

```text
Features/AhoCorasick/
  Domain/
    AcModels.cs                 不可变状态、步骤、结果；最小构建器契约
    AcLimits.cs                 本域教学预算
    AcInput.cs                  输入语义、Unicode 解码与预检
    AhoCorasickTraceBuilder.cs   Trie → BFS fail → 扫描与输出链接
  Application/
    IAcPlaybackClock.cs         只表达节拍、开始、停止与释放
    AcPlaybackSession.cs        游标、倍速、播放与生命周期
    AcTraceProjection.cs        匹配行索引与检查器文字的纯投影
  Infrastructure/
    DispatcherAcPlaybackClock.cs  Avalonia 时钟适配器
  Content/
    AcExampleCatalog.cs         不可变预设
    LessonCatalog.cs            不可变正文、练习和演示语义锚点
  Controls/                    本域图与文本带呈现
  AhoCorasickDocument.cs         草稿、阅读、练习、命令与提交协调
  AhoCorasickView.axaml[.cs]     绑定、布局和输入转发
  AhoCorasickIds.cs              独立 Document 身份
```

Domain 和 Application 不依赖 Avalonia。构建器是无状态服务，每次调用的数据局部化，
可以安全共享。时钟每次解析独立，Document 不从全局容器查找依赖。
Host 的 Document Scope 负责实际对象图；Standalone 复用同一服务注册并建立一个独立作用域。
该组合不模拟 Host 的 Dock、ALC 或生命周期协议。

## SOLID 对应关系

| 原则 | 当前约束 |
| --- | --- |
| S：单一职责 | 解析、算法、播放、状态投影、内容和控件各有单一主要修改理由；Document 只协调页面 |
| O：开放封闭 | 调整章节编号或演示锚点不修改 Document 分支；未来算法建立自己的域和 Document |
| L：行为替换 | IAcTraceBuilder 返回完整轨迹或明确失败；生产/测试时钟遵守同一节拍契约，播放会话不识别具体实现 |
| I：接口隔离 | 构建器仅 Build；时钟仅 Tick/Start/Stop/Dispose；普通内容与投影没有额外接口 |
| D：依赖倒置 | Document 注入构建器和时钟契约，Application 不直接创建 DispatcherTimer，适配器留在 Infrastructure |

这里只采用构造注入和时钟适配，未引入策略工厂、事件总线、仓储、算法基类或“所有算法通吃”的引擎。
教学事件保留标题和解释，是“生成可解释轨迹”这一领域用例的输出；动画只读取事实，不能自行改变算法。

## 状态与提交设计

1. 输入校验和整份轨迹先在局部完成，再生成结果行索引。
2. 成功后暂停旧播放、清除旧选择并统一载入新轨迹；提交期间禁止结果选择回写游标。
3. 失败时保留旧轨迹、步骤和已发现结果，显示错误与草稿提示，不返回部分构建结果。
4. fail 使用不可变更新，旧步骤保留旧版本；队列、节点、步骤、结果和课堂内容也使用不可变集合。
5. 结果列表是稳定的只读可观察集合，只增加/移除已发现的前缀，避免每帧重建 ListBox 选择状态。
6. 时钟只产生节拍。游标使用整数，手动跳转暂停；同值的绑定回写不暂停自动播放。
7. 暂停、终点、移除 View、更换 DataContext 和释放均阻止后续推进；排队节拍仍有状态防护。

Document 拥有播放会话，会话负责停止、退订并释放专属时钟；生产时钟和会话都支持幂等释放，
因此容器作用域随后清理已解析的瞬时服务也安全。未引入跨 Document 的可变单例。

## 当前边界

仍使用内存实验和 Unicode 数学文本，暂不增加持久化、Java 执行、网页发布或在线分享。
输入规模有显式预算，构建同步完成。纯匹配算法复杂度与教学快照、字符串说明、绘制开销分别说明；
本阶段不把 UI 时间作为性能基准，也不扩展为大规模生产词典引擎。
