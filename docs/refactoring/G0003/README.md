# G0003：AC 完整教程与 Document 内预览

日期：2026-10-21。本专项提供本地阅读与开发验证，不代表真实 Host 或发布验收。

## 结果

- 在原有 Document 增加“完整教程”阅读模式，含目录、前后章、窄窗口下拉导航与可复制正文。
- [唯一 Markdown 正文](../../tutorials/aho-corasick-tutorial.md) 包含 16 章，覆盖历史、形式化定义、算法推导、正确性、复杂度、完整 C# 示例与练习。
- 8 张 Mermaid 图支持主题切换、适应宽度、缩放和源码展开；运行时不执行图形脚本。
- 进入教程暂停播放，切回课堂保留轨迹、草稿、结果与步骤；教程模式往返保留滚动位置，换章回到顶部。
- 宽窗口保持原课堂的单行页头；窄窗口将模式入口放到下一行。

## 源码与生成物

Git 保存 Markdown、预览代码、生成脚本、Node 依赖锁文件及更新后的 NuGet 锁文件。
PNG、SVG 和图示清单生成到 Plugin 的 `obj/ac-tutorial-assets`，不进入 Git。
普通构建按输入时间戳生成并嵌入图示；首次或缓存失效时需要 Node/npm 和本机 Microsoft Edge。
终端用户不需要这些构建工具，阅读不访问外网。

维护方法见 [图示生成说明](../../../tools/tutorial-assets/README.md)。

## 验证

- `pwsh -NoProfile -File tools/verify-g0001.ps1 -Topic G0003`：锁定还原、Debug 零警告构建、58/58 测试、格式与 diff 检查通过。
- 新增覆盖：章节资源完整性、每张 Mermaid 的两套主题图、导航边界、Document 隔离、暂停与进度保留、16 章真实 Skia 渲染、图示主题更新、窄窗口与滚动行为。
- 原课堂与教程的 UI 测试放入同一禁止并行的 xUnit collection，避免多个 Headless Application 争用进程内的 Avalonia Dispatcher；领域测试仍可独立运行。
- `npm run check`：8 张图的源码与 PNG/SVG 摘要一致。
- 从正文直接提取 `TutorialAc` 编译运行：6 个固定案例、300 组随机朴素对照、非法代理序列拒绝通过。该示例验证没有替换插件内核。
- 已检查宽窗口浅色与窄窗口深色截图。完整本地证据在被 Git 忽略的 `TestResults/G0003`。

没有执行 Windows CI、Release/ZIP 门禁、部署或真实 Host 联调。
