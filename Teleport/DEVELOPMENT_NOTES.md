# Teleport 持续开发文档

## 1. 文档目的

这份文档用于沉淀当前项目已经完成的改动、发布流程、已知问题与后续建议，避免后续开发过度依赖聊天记录回忆上下文。

适用场景：

- 继续开发 `Teleport` / `XTeleport`
- 回顾 `XCount` 联动改动
- 继续维护本地发布脚本与镜像仓库同步逻辑
- 重新建立 Codex / Cursor / IDA Pro MCP 开发环境


## 2. 最近一轮核心改动概览

### 2.1 XCount 联动能力补齐

本轮围绕 `SkyEye` 的思路，补了 `XCount` 相关的本地联动能力，重点不是单纯读取外部插件数字，而是补齐本地可控逻辑。

已完成：

- 本地统计周边玩家数量
- 白名单机制
- 非白名单玩家数量判断
- 队友坐标展示
- 传送安全检测接入 XCount 判断
- 独立 `XCount` 面板
- 将 `XCount` 入口迁移到“传送设置”
- 面板文案整体中文化

涉及文件：

- [Configuration.cs](D:/git back/Teleport/Configuration.cs)
- [XCountResults.cs](D:/git back/Teleport/XCountResults.cs)
- [StaticUtils.cs](D:/git back/Teleport/StaticUtils.cs)
- [Plugin.cs](D:/git back/Teleport/Plugin.cs)
- [Windows/ConfigWindow.cs](D:/git back/Teleport/Windows/ConfigWindow.cs)
- [Windows/MainWindow.cs](D:/git back/Teleport/Windows/MainWindow.cs)
- [Windows/XCountWindow.cs](D:/git back/Teleport/Windows/XCountWindow.cs)


## 3. XCount 现状说明

### 3.1 当前逻辑来源

`XCountResults` 当前是“双轨”逻辑：

- 一方面尝试读取外部 `XCount` 插件的 `CountsDict["<all>"]`
- 另一方面本地直接通过 `Svc.Objects` 统计周边玩家

当前真正用于传送安全判断的是本地统计结果，而不是完全依赖外部插件。

### 3.2 当前关键字段

在 [Configuration.cs](D:/git back/Teleport/Configuration.cs) 中已经加入：

- `XCountThreshold`
- `XCountWhitelist`
- `IgnoreUnsafePlayersForTP`

含义：

- `XCountThreshold`：人数阈值，超过则阻止传送
- `XCountWhitelist`：白名单角色名，使用 `|` 分隔
- `IgnoreUnsafePlayersForTP`：无视周边非白名单玩家，即即使附近有“绿玩”也不拦截传送

### 3.3 当前统计口径

在 [XCountResults.cs](D:/git back/Teleport/XCountResults.cs) 中：

- `NearbyPlayerCount`：本地周边人数
- `WhitelistedNearbyPlayerCount`：白名单人数
- `EffectiveNearbyPlayerCount`：非白名单人数
- `IsNearbySafe`：当非白名单人数 `<= 0` 时为安全

### 3.4 当前界面行为

在 [Windows/XCountWindow.cs](D:/git back/Teleport/Windows/XCountWindow.cs) 和 [Windows/ConfigWindow.cs](D:/git back/Teleport/Windows/ConfigWindow.cs) 中：

- 周边存在非白名单玩家时，显示 `周边绿玩：N 个`
- 周边无人或周边全是白名单时，显示 `当前状态：安全`
- 可以勾选 `无视周边绿玩`

注意：

- “无视周边绿玩”只影响传送是否被拦截
- 不会把危险环境伪装成“安全”

### 3.5 当前入口位置

`XCount` 入口已从主窗口移除，改到 `传送设置 -> XCount联动` 区域中。

对应文件：

- 主窗口入口已移除：[Windows/MainWindow.cs](D:/git back/Teleport/Windows/MainWindow.cs)
- 设置页入口保留：[Windows/ConfigWindow.cs](D:/git back/Teleport/Windows/ConfigWindow.cs)


## 4. 队友坐标相关改动

在 [StaticUtils.cs](D:/git back/Teleport/StaticUtils.cs) 中做过两项关键修复：

- `GetValidPartyMember(int index)` 返回类型修正为 `IPartyMember?`
- 新增 `GetPartyMemberPositionSummaries()` 供 `XCount` 面板展示队友坐标

这块的目的：

- 修复原先小队成员定位不严谨的问题
- 为后续“队友坐标展示 / 队友传送辅助 / 小队可视化”打基础


## 5. 传送拦截逻辑现状

当前传送入口在 [StaticUtils.cs](D:/git back/Teleport/StaticUtils.cs) 的 `SetPos(...)` 中会先执行：

- 激活码检查
- 区域风险检查
- `XCountResults.RefreshPlayerCount()`
- XCount 阈值检查

当前拦截提示文案已经改为按非白名单人数提示，例如：

- `当前周边有 N 个绿玩，超过你设定的阈值 X，不会传送`

注意：

- 当前阈值判断使用的是 `EffectiveNearbyPlayerCount`
- 如果 `IgnoreUnsafePlayersForTP = true`，即使超过阈值也允许传送


## 6. 发布脚本改造记录

### 6.1 改造目标

原先发布流程存在几个问题：

- PowerShell 主脚本承担逻辑过重
- 编码与字符串处理容易出问题
- 版本号递增和实际发布版本容易不一致
- 本地版本、GitHub Release、`repo.json` 不容易统一校验

因此已改成：

- `publish.ps1` 作为轻量包装器
- `publish.py` 作为主发布逻辑

相关文件：

- [publish.ps1](D:/git back/Teleport/publish.ps1)
- [publish.py](D:/git back/Teleport/publish.py)

### 6.2 当前发布脚本能力

`publish.py` 目前负责：

- 读取和更新版本号
- 上传 GitHub Release 资产
- 更新插件仓库 `repo.json`
- 复制 `latest.zip`
- 推送自有插件仓库
- 尝试更新镜像仓库
- 输出本地与远端版本校验信息

`publish.ps1` 目前负责：

- 组装参数
- 调用 `publish.py`
- 将运行结果追加到 `publish-run.log`

### 6.3 当前版本策略

当前策略已经改成：

- 默认保持“本地版本 == 本次发布版本”
- 不再自动把本地版本发布后立刻加到下一个版本
- 如果确实要发布后顺手 bump 下一版，使用：
  - PowerShell：`-BumpNextVersion`
  - Python：`--bump-next-version`

### 6.4 当前已发布版本

最近一次已确认发布成功并校验一致的版本为：

- `7.0.0.12`

对应文件已更新：

- [Teleport.csproj](D:/git back/Teleport/Teleport.csproj)
- [Teleport.json](D:/git back/Teleport/Teleport.json)

### 6.5 当前日志

发布日志路径：

- [publish-run.log](D:/git back/Teleport/publish-run.log)

说明：

- 用户要求每次运行发布脚本时都更新该日志
- 当前日志机制可用，但一致性仍有优化空间


## 7. 镜像仓库同步现状

镜像仓库相关路径：

- 自有插件仓库：`D:\git back\DalamudPlugins`
- 镜像仓库：`D:\git back\dalamud-plugins`

已知情况：

- 用户本地 `dalamud-plugins` 工作区曾经是脏的
- 为避免覆盖用户本地状态，后续同步时采用了更保守的处理方式
- 曾通过全新克隆校验镜像远端内容，确认远端镜像已跟上 `7.0.0.12`

后续建议：

- 镜像仓库同步最好改为“拉一份干净副本 -> 写入 -> 提交 -> 推送”
- 不直接在用户长期使用的本地镜像目录上做危险操作


## 8. 当前仓库状态提醒

当前 `Teleport` 工作区不是干净状态，存在：

- 已修改文件
- 新增文件
- 删除文件
- 邻接目录下还有其他仓库或未跟踪目录

处理原则：

- 不要因为追求“整洁”去随便 `reset --hard`
- 不要随便删除 `StaticUtils.cs.bak`、`publish-run.log`、镜像目录等，除非先确认用途
- 如果后续要发版，先明确哪些文件是本次应该纳入的改动


## 9. 已知问题与注意事项

### 9.1 .NET SDK 版本问题

本地 `dotnet build` 曾失败，原因不是本次代码改动，而是环境 SDK 不匹配。

现象：

- 当前机器安装的是 `.NET 9 SDK`
- 项目目标是 `.NET 10.0`
- 因此会报 `NETSDK1045`

结论：

- 如果要本机命令行编译，需要安装支持 `.NET 10.0` 的 SDK

### 9.2 编码问题

之前窗口文件存在过中文乱码风险，因此后续编辑包含中文的窗口文件时要特别小心。

受影响区域主要是：

- [Windows/ConfigWindow.cs](D:/git back/Teleport/Windows/ConfigWindow.cs)
- [Windows/XCountWindow.cs](D:/git back/Teleport/Windows/XCountWindow.cs)

建议：

- 保持 UTF-8 编码
- 尽量避免用会自动重编码的编辑方式批量处理这些文件

### 9.3 Token 安全

按用户要求，GitHub Token 已被写入本地发布脚本。

这意味着：

- 本地脚本使用方便
- 但脚本绝对不能误传到公开仓库

后续若要恢复更安全方式，优先改回：

- 环境变量 `GITHUB_TOKEN`
- 或本地私有配置文件


## 10. Codex / Cursor / IDA MCP 环境记录

### 10.1 当前发现结果

Cursor 中确实配置过 IDA Pro MCP。

关键文件：

- Cursor MCP 配置：[C:\Users\Administrator\.cursor\mcp.json](C:/Users/Administrator/.cursor/mcp.json)
- Codex MCP 配置：[config.toml](C:/Users/Administrator/.codex/config.toml)

### 10.2 已完成动作

已将 Cursor 中的 `ida-pro-mcp` 配置迁移到 Codex 的 [config.toml](C:/Users/Administrator/.codex/config.toml)。

当前 Codex 中新增：

- `mcp_servers.ida_pro_mcp`
- 启动命令为 Python 运行 `ida_pro_mcp/server.py`

### 10.3 已确认信息

以下路径存在且可运行：

- Python：`C:\Users\Administrator\AppData\Local\Programs\Python\Python314\python.exe`
- MCP Server：`C:\Users\Administrator\AppData\Local\Programs\Python\Python314\Lib\site-packages\ida_pro_mcp\server.py`

### 10.4 使用提醒

Codex 改完配置后通常需要：

- 完全关闭 Codex Desktop
- 重新打开
- 重新加载会话后再检查工具是否出现

### 10.5 IDA 当前打开 PDB 弹窗说明

当前 FF14 的 `ffxiv_dx11.exe` 逆向时，如果弹出“选择 PDB 文件”，通常直接取消即可。

原因：

- FFXIV 本体通常没有公开可直接配套的 PDB
- IDA 只是询问是否手动提供符号文件
- 没有 PDB 并不会阻止继续做反编译和 MCP 联动


## 11. 建议的后续开发方向

优先级较高的建议：

- 将 `StaticUtils.cs` 中其余仍使用旧 `<all>` 口径提示的地方统一改成新“绿玩 / 安全”语义
- 给 `XCount` 白名单输入做更友好的编辑体验
- 增加更清楚的“当前安全状态”主界面展示
- 补一份正式的发布说明文档，区分“本地发版”和“镜像同步”
- 清理发布目录与脚本的非必要残留文件

如果继续做逆向联动方向，可考虑：

- 将 IDA MCP 与当前项目工作流串起来
- 针对 FFXIV 结构、函数、坐标相关逻辑建立专门逆向笔记
- 记录常用地址、函数签名、结构体字段与改名历史


## 12. 建议的接手顺序

后续再次接手这个项目时，推荐按下面顺序恢复上下文：

1. 先看 [DEVELOPMENT_NOTES.md](D:/git back/Teleport/DEVELOPMENT_NOTES.md)
2. 再看 [XCountResults.cs](D:/git back/Teleport/XCountResults.cs) 了解当前安全判断
3. 再看 [Windows/XCountWindow.cs](D:/git back/Teleport/Windows/XCountWindow.cs) 与 [Windows/ConfigWindow.cs](D:/git back/Teleport/Windows/ConfigWindow.cs) 了解界面入口
4. 如果涉及发版，再看 [publish.py](D:/git back/Teleport/publish.py) 与 [publish.ps1](D:/git back/Teleport/publish.ps1)
5. 如果涉及逆向环境，再检查 [config.toml](C:/Users/Administrator/.codex/config.toml) 与 IDA / MCP 状态


## 13. 最后说明

这份文档重点记录的是“最近一轮连续开发”中真正影响后续接手效率的内容，而不是完整项目编年史。

如果后面又做了新的大改动，建议继续在这份文档里追加：

- 改动目标
- 关键文件
- 风险点
- 验证方式
- 后续待办
