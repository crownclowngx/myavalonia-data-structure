# 教程中的 Mermaid 图

唯一源码为 `docs/tutorials/aho-corasick-tutorial.md` 中的 Mermaid 围栏。生成工具使用 Mermaid 11.17.2
校验语法并生成深浅主题的 2 倍分辨率 PNG 与 SVG；插件嵌入 PNG，以原生控件显示、缩放并提供源码展开。
PNG、SVG 与清单均生成到 Plugin 项目的 `obj/ac-tutorial-assets`，不提交 Git。
终端用户阅读不需要 Node、浏览器控件或外网。

正常 `dotnet build` 会按输入时间戳增量生成并嵌入图示；首次或输入变化后会校验缓存、按锁文件安装
Node 开发依赖，再用本机 Microsoft Edge 离线渲染。构建机器需要 Node/npm 和 Edge；后续未变化的构建跳过生成。
若需单独生成或检查，在本目录执行：

```powershell
npm ci --ignore-scripts
npm run build
npm run check
```

`obj` 中的 `manifest.json` 记录源码顺序、生成器与依赖哈希及输出文件摘要。插件通过规范化 Mermaid 源码的 SHA-256
前 16 个十六进制字符定位资源；C# 测试检查每张图的深浅主题资源都存在，防止旧图与新正文混用。
`npm run check` 还会校验生成文件内容。修改生成配置或 Mermaid 版本后也应完整重新生成。

Markdown 预览支持正文、标题、强调、行内代码、列表、引用、分隔线、代码块、外链和管道表格。
Mermaid 图在开发时渲染，不在插件运行时执行脚本。当前没有通用的用户 Markdown 编辑器或在线图形执行接口。
