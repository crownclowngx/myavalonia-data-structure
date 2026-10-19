# 数据结构交互课堂

基于 Avalonia 的数据结构与算法学习插件。首个独立业务域是 **AC 自动机**，以一个
`AhoCorasickDocument` 提供章节阅读、数学说明、预测练习和真实算法驱动的交互实验。

- 8 个章节：问题、Trie、fail、BFS、输出、扫描、数学与实现边界。
- 自定义词典和句子；经典、重叠、中文、连续回退、Unicode、无匹配预设。
- Trie 生长、fail 构建、文本扫描；播放、暂停、前后单步、阶段与时间轴跳转。
- 节点检查、后缀解释、BFS 队列、文本带、匹配结果定位。
- 跟随 Host 的深色与浅色主题，实时切换；画布旁单步观察节点生长、字符比较、fail 回退及逐条输出。
- 每个数据结构或算法独立业务域、独立 Document，不把后续算法放进 AC 的模型中。

使用方式、输入语义和域边界见 [AC 自动机课堂](docs/aho-corasick.md)。真实交付物是
`src/DataStructurePlugin.Plugin`；`Standalone` 只负责快速预览同一份 View、Document 与业务代码。

本次 SOLID 再审核、修复与测试映射见 [G0001 专项档案](docs/refactoring/G0001/README.md)。
深浅主题与逐步比较见 [G0002 专项档案](docs/refactoring/G0002/README.md)。
本地开发门禁：`pwsh -NoProfile -File tools/verify-g0001.ps1`，只运行 Debug 与开发检查，不运行 Windows CI 或发布门禁。

> 第一次开始开发前，请先阅读 [项目文档与快速开始](docs/README.md)。其中说明了三个子项目和
> Standalone 窗口的职责、接入真实 Host 的边界，以及临时部署和正式 ZIP 发布流程。

```powershell
dotnet restore
dotnet build
dotnet run --project src/DataStructurePlugin.Standalone
dotnet msbuild src/DataStructurePlugin.Plugin/DataStructurePlugin.Plugin.csproj -t:BuildManagedPluginPackage -p:Configuration=Release
```

要在真实 Host 中调试，请显式提供 Host 的 `Controls` 目录：

```powershell
dotnet msbuild src/DataStructurePlugin.Plugin/DataStructurePlugin.Plugin.csproj `
  -t:DeployManagedPlugin `
  -p:ManagedPluginDeployRoot=C:\Path\To\Host\Controls
```

Standalone 只能验证界面和插件自身对象图；manifest、加载上下文、Document Scope、Dock、Tool 和
生命周期必须使用真实 Host 做最终验收。

当前只注册 AC 自动机一个 Document，不注册 Tool、全局快捷键或模板示例命令。
教学内容和核心算法均位于 `Features/AhoCorasick` 内，不引用 Host 内部实现。
