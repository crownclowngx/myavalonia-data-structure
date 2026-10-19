# 本地临时部署记录

目标：`D:\data\avalonia\Controls\DataStructurePlugin`。
使用 Managed Plugin Build 的筛选部署目标，只部署该插件叶子目录，不复制普通 bin 全目录。
配置为 Debug，插件 ID 和版本保持 `myavalonia.plugin.data.structure` / `1.0.0`。

## 已完成的部署

2026-10-19，用户确认 Host 已退出，再次确认进程不存在后完成替换。
部署前已验证 `D:\data` 至目标叶子目录没有重解析点，MSBuild 解析出的部署目录与目标一致。
执行命令：

```powershell
dotnet build src/DataStructurePlugin.Plugin/DataStructurePlugin.Plugin.csproj -c Debug --no-restore -warnaserror -p:ManagedPluginDeployRoot=D:\data\avalonia\Controls -p:ManagedPluginTraceAssets=true
```

本地 Debug 全量门禁已通过：53 测试通过、0 失败、0 跳过；构建零警告零错误，格式和差异检查通过。
实际仅部署以下 5 个文件，逐一与源产物 SHA-256 比对一致：

| 文件 | 字节数 | SHA-256 |
| --- | ---: | --- |
| DataStructurePlugin.Plugin.dll | 198656 | 212B646ECD85988761B35A1215BDF7F36638BB5F7E1517BE186B2971A7C56626 |
| DataStructurePlugin.Plugin.pdb | 55288 | 3F2BFCBEFE6F82361F7D0FD9291230B81B3AF3F5D79FD73F4CA45701726D22EE |
| DataStructurePlugin.Plugin.deps.json | 11232 | 885539562225CB36BC18F4BAFC46A0D8EDC681B29EAEF2F8639E559BECC1CAA6 |
| MyAvaloniaManagement.Icons.dll | 8192 | 52AB55FB828C3F6CF404814BC22F1B7A3B8045B762C56A9E6610799C6BE922E9 |
| plugin.manifest.json | 302 | 8D279D4FD741F22B9F5CF3122A08FE460AE62F36CA56A14721406AE4A47641E7 |

manifest 为 schema 2，入口为 `DataStructurePlugin.Plugin.DataStructurePluginModule`，SDK 区间
`[3.4.1,4.0.0)`。Controls 中同一插件 ID 仅此一份，没有携带 Avalonia、SDK 等 Host 共享程序集。

部署前后，其他插件的 615 条目录/文件路径、文件大小和文件最后写入时间组成的有序 JSON 摘要一致：
`83F0866C11E26CA2B9CA23C9B1D57FC933771F6F9F3936E98DABFDA6FB2F6732`。
Host EXE 的 SHA-256 也保持
`3B8F5ECB4F5063C0C6FC6E47D69B34EA57E741A7A92F238C0B6E0616B717EF59`。
前者是元数据比对，没有声称对其他插件的每个文件做内容哈希。

重新启动 `D:\data\avalonia\MyAvaloniaManagement.exe` 后打开 AC Document 即可加载新版。
本次没有启动 Host 或声称完成真实 Dock 验收；没有执行 Windows CI、Release、ZIP、签名或发布门禁。

## 中间文件清理

已执行 `dotnet clean DataStructurePlugin.slnx -c Debug --nologo -v:quiet`，返回 0，
清除了源项目中的程序集、可执行程序及大体积运行时输出；部署目录保持完整。

自动审批此前拒绝递归删除，之后更收敛的方案（验证项目边界、拒绝重解析点、逐文件删除、
只移除空目录）也在执行前被拒绝，均仅返回 `blocked by policy`，没有提供更具体原因。
没有尝试绕过审批，未执行的删除不能视为清理完成。

剩余项目 bin/obj 中的清单和 NuGet 缓存，以及 `TestResults` 中的日志、TRX、截图和失败探针，
共 84 个文件、4,544,630 字节，保留在忽略目录中。用户源文件、锁文件和 `.aiflow/insight` 未作为中间产物删除。
