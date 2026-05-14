# TeleportEX

中文：`TeleportEX` 是一个基于 Dalamud 的 FFXIV 插件项目，当前代码主要围绕坐标传送、快捷传送、聊天指令解析，以及若干实验性功能展开。  
English: `TeleportEX` is a Dalamud plugin project for FFXIV focused on coordinate teleportation, quick teleport workflows, chat-driven command parsing, and several experimental features.

中文：仓库目前包含主插件项目 `Teleport`，以及一个历史遗留的辅助项目 `CodeCreater`。代码整体仍偏实验性质，部分 UI 文本和源码注释存在历史编码问题，后续仍建议继续清理。  
English: This repository currently contains the main plugin project `Teleport` and a legacy helper project `CodeCreater`. The codebase is still experimental in places, and some UI strings / comments still have legacy encoding issues that should be cleaned up over time.

## 项目概览 | Overview

中文：当前代码中可以确认的主要能力包括：  
English: The current codebase includes the following major capabilities:

- 中文：将本地角色传送到指定 `x y z` 坐标。  
  English: Teleport the local player to a specific `x y z` coordinate.
- 中文：支持带地图 ID 校验和不带校验的两种传送方式。  
  English: Support both territory-validated and non-validated teleport modes.
- 中文：保存并快速载入常用传送点。  
  English: Save and quickly reload frequently used teleport positions.
- 中文：传送到目标、鼠标指向位置、小队成员或场地标点。  
  English: Teleport to the current target, mouse world position, party members, or field markers.
- 中文：维护分地图传送列表，并可在特定地图弹出快捷窗口。  
  English: Maintain per-map teleport lists and optionally show a quick window on selected maps.
- 中文：解析聊天文本中的多种指令格式，并触发对应行为。  
  English: Parse several command formats from chat messages and trigger matching actions.
- 中文：提供基于正则表达式的聊天触发器功能。  
  English: Provide regex-based chat trigger functionality.
- 中文：包含若干实验性的移动、朝向和速度相关功能。  
  English: Include several experimental movement, facing, and speed-related features.

## 仓库结构 | Repository Layout

```text
Teleport/      Main Dalamud plugin source
CodeCreater/   Legacy helper tool for activation-code generation
icon.jpg       Plugin icon asset
Teleport.sln   Visual Studio solution
```

## 风险提示 | Warning

中文：这个插件会直接修改游戏中的位置、状态或内存相关数据。  
English: This plugin directly manipulates in-game position, state, or memory-related data.

- 中文：它可能违反游戏的使用条款。  
  English: It may violate the game's Terms of Service.
- 中文：在正式账号上使用可能带来账号风险。  
  English: Using it on a live account may carry account risk.
- 中文：源码中也有部分功能被明确标注为高风险。  
  English: Some features are explicitly marked in the source as high risk.
- 中文：如果你要构建、使用或二次分发，请先完整审查源码。  
  English: Review the source carefully before building, using, or redistributing it.

中文：如果你打算把这个项目公开发布，建议保留这一节。  
English: If you plan to publish this project publicly, keeping this section is strongly recommended.

## 构建要求 | Build Requirements

中文：当前项目文件的目标环境如下：  
English: The current project file targets:

- `.NET 10` preview: `net10.0-windows7.0`
- `x64`
- `Windows`

中文：项目还依赖本地 Dalamud 开发环境，当前默认从下面的路径寻找相关程序集：  
English: The project also relies on a local Dalamud development environment and currently expects assemblies under:

```text
%AppData%\XIVLauncherCN\Addon\Hooks\dev\
```

中文：在 Linux 下会改用：  
English: On Linux, it uses:

```text
$DALAMUD_HOME
```

中文：如果你的本地目录结构不同，请修改 `Teleport/Teleport.csproj` 中的 `DalamudLibPath`。  
English: If your local setup differs, update `DalamudLibPath` in `Teleport/Teleport.csproj`.

## 构建方式 | Building

1. 中文：克隆仓库。  
   English: Clone the repository.
2. 中文：准备好本地 Dalamud 开发依赖。  
   English: Make sure the required local Dalamud dependencies are available.
3. 中文：用 Visual Studio 或 Rider 打开 `Teleport.sln`。  
   English: Open `Teleport.sln` in Visual Studio or Rider.
4. 中文：恢复 NuGet 包。  
   English: Restore NuGet packages.
5. 中文：构建 `Teleport` 项目，配置可选 `Debug`、`Release` 或 `Release-Pro`。  
   English: Build the `Teleport` project using `Debug`, `Release`, or `Release-Pro`.

## 主要命令 | Main Commands

中文：插件当前注册了较多命令，下面列出最核心的一组：  
English: The plugin registers a relatively large command surface; these are the core ones:

| Command | 中文说明 | English Description |
| --- | --- | --- |
| `/tpmain` | 打开主界面 | Open the main window |
| `/tpconfig` | 打开设置界面 | Open the config window |
| `/stp` | 带地图校验的安全传送 | Safe teleport with territory validation |
| `/stp2` | 使用 `x z y id` 顺序的安全传送 | Safe teleport using `x z y id` order |
| `/ftp` | 不校验地图 ID 的传送 | Teleport without territory validation |
| `/ftptar` | 将目标传送到指定位置 | Teleport the current target to a position |
| `/ftptome` | 将目标拉到自己身边 | Pull the current target to your position |
| `/ftptotar` | 将自己传送到目标位置 | Teleport yourself to the current target |
| `/ftp2` | 在二维平面上进行传送 | Teleport on the `x z` plane |
| `/ftp2p` | 传送到小队成员 | Teleport to a party member |
| `/ftp2mo` | 传送到鼠标世界坐标 | Teleport to the mouse world position |
| `/tpsave` | 保存当前位置 | Save the current position |
| `/tpload` | 载入已保存位置 | Load the saved position |
| `/tp2mk` | 传送到场地标点 | Teleport to a field marker |
| `/xrot` | 调整朝向 | Adjust facing / rotation |
| `/tpy` | 调整 Y 轴位移 | Offset Y position |
| `/tpface` | 按当前朝向前移 | Move forward relative to facing |

## 聊天解析与触发器 | Chat Parsing And Triggers

中文：当前源码里可以看到几类自动化输入：  
English: The current source supports several automation-style inputs:

- 中文：`@XTP` 命令解析  
  English: `@XTP` command parsing
- 中文：`@tp set ...` 风格的 FPT 类解析  
  English: `@tp set ...` style FPT-like parsing
- 中文：`@CMD` 指令转发 / 执行  
  English: `@CMD` forwarding / execution
- 中文：基于 `XivChatType` 的正则聊天触发器  
  English: Regex-based chat triggers bound to `XivChatType`

中文：因此这个项目不只是一个窗口型插件，也包含了本地聊天自动化层。  
English: As a result, this project is not just a UI plugin; it also includes a local chat automation layer.

## 公开发布前建议 | Pre-Release Notes

中文：如果你准备把它作为一个全新的公开仓库发布，建议先检查这些点：  
English: If you plan to publish this as a fresh public repository, review these items first:

- 中文：仓库里仍保留了历史激活码相关逻辑和辅助工具。  
  English: The repository still contains historical activation-related logic and helper tooling.
- 中文：`Teleport.csproj` 里还有 `UserSecretsId`。  
  English: `Teleport.csproj` still includes a `UserSecretsId`.
- 中文：部分源码和旧文档存在编码问题。  
  English: Some source files and older docs still have text-encoding issues.
- 中文：项目元数据里还有历史作者名、图标链接和标签信息，发布前建议统一整理。  
  English: Plugin metadata still contains historical author names, icon URLs, and tags that should be reviewed before release.
- 中文：IDE 本地配置文件和打包产物不建议继续跟踪。  
  English: IDE-local files and packaged artifacts should not remain tracked.

## 建议的开源清理项 | Suggested Open-Source Cleanup

1. 中文：从新仓库中移除 IDE 本地文件、压缩包和用户专属配置。  
   English: Remove IDE-local files, archives, and user-specific config from the new repository.
2. 中文：决定是否保留激活码 / 历史验证逻辑。  
   English: Decide whether activation / legacy verification logic should remain.
3. 中文：统一转成 UTF-8 编码。  
   English: Normalize files to UTF-8.
4. 中文：补充 `LICENSE` 文件。  
   English: Add a proper `LICENSE` file.
5. 中文：如需面向外部贡献者，建议补充截图和安装说明。  
   English: If you want outside contributors, add screenshots and clearer installation instructions.

## License

中文：当前仓库还没有附带许可证文件。  
English: This repository does not include a license file yet.

中文：如果你希望它成为真正意义上的开源仓库，请在发布前补上 `LICENSE`。  
English: If you want this repository to be truly open source, add a `LICENSE` file before publishing.
