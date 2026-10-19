# G0001 测试映射与本地门禁

## 一键执行

在项目根目录执行：

```powershell
pwsh -NoProfile -File tools/verify-g0001.ps1
```

脚本依次执行：

```powershell
dotnet restore DataStructurePlugin.slnx --locked-mode
dotnet build DataStructurePlugin.slnx -c Debug --no-restore -warnaserror
dotnet test DataStructurePlugin.slnx -c Debug --no-build --no-restore --logger "trx;LogFileName=tests.trx" --results-directory TestResults/G0001/local-gate
dotnet format DataStructurePlugin.slnx --verify-no-changes --no-restore
git -c core.safecrlf=false diff --check
```

日志、TRX、summary.json 与截图保存在忽略目录 `TestResults/G0001/local-gate/`。
每次开始先写 running，失败立即写 failed 并返回非零退出码，成功才写 passed；
任何测试跳过、零测试或执行数量不一致都不能通过。脚本恢复调用前的截图环境变量。
测试集合和检查顺序固定，不根据测试名称筛选掉失败项。

## 覆盖映射

| 文件 | 验证内容 |
| --- | --- |
| AhoCorasickTests | 经典词典、后缀输出、重叠、中文、Unicode、空文本、非法代理序列、预算、180 组随机 ASCII 对照、最长后缀与状态不变量 |
| AcDomainRegressionTests | 100 组 Unicode/组合字符随机对照、所有公开集合不可写、后插短词的终止标记、BFS/阶段快照、预算边界、共享构建器的并行隔离 |
| AcPlaybackSessionTests | 确定性节拍、暂停、终点、重播、载入、释放、迟到回调、NaN/无穷/越界游标和倍速、同值滑块回写 |
| AcDocumentRegressionTests | 稳定结果集合、旧选择隔离、构建失败保留旧实验、初始失败清理、节点边界、空章节回写、语义锚点、初始化取消、释放后命令、结果行定位 |
| AcCompositionTests | 真实 Module 的唯一 AC Document 注册、图标引用、零额外贡献、容器验证、构建器/时钟寿命和多 Scope 隔离 |
| AhoCorasickViewTests | 真实 AXAML/Skia 渲染、真实输入双向绑定、按钮命令、结果选择、宽窄布局、640×580 窗口、实际指针点击和拖动、生产 UI 时钟、换模型和移除 View 暂停 |

纯播放测试使用显式节拍，不用睡眠推测执行。唯一生产 UI 时钟测试等待实际 FrameIndex 通知，
设置 5 秒失败超时。Headless/Skia 直接承载生产 View，截图可通过 `AC_SCREENSHOT_DIR` 指定输出目录。

## 本次执行结果

2026-10-19 执行 `pwsh -NoProfile -File tools/verify-g0001.ps1`，最终状态为 **passed**。

| 检查 | 结果 |
| --- | --- |
| 锁定还原 | 通过，未改动 SDK 版本 |
| Debug 构建 | 通过，0 警告、0 错误 |
| 全量测试 | 47 通过、0 失败、0 跳过，相对首版 15 项增加 32 项 |
| 格式只读检查 | 通过 |
| git diff --check | 通过 |
| 实际图像检查 | 已检查宽屏实验、窄屏与最小窗口截图，未发现阻断使用的排版问题 |

测试按类计数：核心 14、领域回归 6、播放会话 15、Document 回归 7、注册组合 1、真实 View 4，共 47。
图像证据位于 `TestResults/G0001/local-gate/screenshots/`，包含 reading、experiment、narrow、minimum-window。
可复查摘要：`TestResults/G0001/local-gate/summary.json`；逐项结果：同目录 `tests.trx`。

另在隔离目录 `TestResults/G0001/gate-failure-probe/` 用只返回 23 的本地 dotnet 替身进行失败注入：
脚本返回 1，摘要为 failed，步骤只包含 restore=23，未继续 build/tests/format/diff-check。
该探针未改变真实项目包或成功摘要，验证了失败立即终止及失败证据落盘。

门禁范围仅限本地开发；真实 Host、Dock、ALC、ZIP 和发布尚未验收。

未运行 Windows CI；未执行 Release、打包、安装、部署、签名、上传或发布门禁。
