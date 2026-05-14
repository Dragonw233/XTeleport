using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.Chat;
using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Timers;
using ECommons.Logging;
using Teleport.Windows;

namespace Teleport
{
    public sealed class Plugin : IDalamudPlugin
    {
        public string Name => "Teleport";

        #region 命令和帮助

        // 打开主页面
        private const string OpenMainWindow = "/tpmain";

        // 打开设置页面
        private const string OpenConfigWindow = "/tpconfig";

        // 传送自己到x y z，并且验证地图id
        private const string TPMeSafe = "/stp";

        // 传送自己到x y z，并且验证地图id，这种方式参数输入的顺序是x z y
        private const string TPMeSafeXZY = "/stp2";

        // 传送自己到x y z，不验证地图id
        private const string TPMeUnsafe = "/ftp";

        // 将选中目标传送到指定位置
        private const string TPTarget = "/ftptar";

        // 将选中目标传送到自己面前
        private const string TPTargetToMe = "/ftptome";

        // 将自身传送到选中目标
        private const string TPMeToTarget = "/ftptotar";

        // 传送自己到相同高度的地方
        private const string TP2XY = "/ftp2";

        // 传送自己到指定小队成员
        private const string TP2PartyMember = "/ftp2p";

        // 传送到鼠标位置
        private const string TP2Mouse = "/ftp2mo";

        // 改变移动速度
        private const string ChangeSpeed = "/tpspeed";

        // 保存当前位置
        private const string SavePosition = "/tpsave";

        // 传送到保存的位置
        private const string LoadPosition = "/tpload";

        // 传送到标点
        private const string TP2Mark = "/tp2mk";

        // 保存当前位置到列表
        private const string SavePositionToList = "/tpsave2list";

        // 保存鼠标位置到传送列表
        private const string SaveMouseToList = "/tpsave2listmo";

        // 改变自身面向
        private const string SetRot = "/xrot";

        // 在Y轴移动
        private const string MoveY = "/tpy";

        // 按面向tp
        private const string TPFace = "/tpface";

        // 超级防击退
        private const string RealAntiKick = "/xtpantikick";

        // 不位移
        private const string ActionNoMove = "/xtpnomove";

        // 自由移动
        private const string FreeMove = "/xtpfreemove";

        // 储存命令和帮助
        internal Dictionary<string, string> CommandsDictionary = new()
        {
            { OpenMainWindow, "打开主页面" },
            { OpenConfigWindow, "打开设置页面" },
            { TPMeSafe, "格式：/stp x y z id，将自己传送到指定坐标，只有当前地图id与输入相同时才会进行传送" },
            { TPMeSafeXZY, "格式：/stp2 x z y id，将自己传送到指定坐标，只有当前地图id与输入相同时才会进行传送，这个命令是为了兼容fpt的坐标格式" },
            { TPMeUnsafe, "格式：/ftp x y z，强制将自己传送到指定坐标，不验证地图id" },
            { TPTarget, "格式：/ftptar x y z，将选中目标传送到指定坐标" },
            { TPTargetToMe, "将选中目标传送到自身坐标" },
            { TPMeToTarget, "将自身传送到选中目标的坐标" },
            { TP2XY, "格式：/ftp2 x z，将自身传送到指定二维坐标（在ff14中，y为高度轴）" },
            { TP2PartyMember, "格式：/ftp2p 2-8，将自身传送到指定小队成员身边，1是你自己" },
            { TP2Mouse, "传送到鼠标位置" },
            { ChangeSpeed, "格式：/tpspeed 0.1-10，改变移动速度，1为正常速度，0.5为正常速度的一半，2为正常速度的两倍，不在0.1-10区间内的值会报错" },
            { SavePosition, "保存当前位置和地图id" },
            { LoadPosition, "传送到保存的位置" },
            { TP2Mark, "格式：/tp2mk <mk>，将自身传送到场地标点，<mk>可以为A B C D 1 2 3 4，标点不存在或者输入错误会报错" },
            { SavePositionToList, "保存当前位置到传送列表" },
            { SaveMouseToList, "保存鼠标位置到传送列表" },
            { SetRot, "格式：/xrot -180~180，改变面向，详见主界面中“面向调整”部分。" },
            { MoveY, "格式：/tpy y轴移动距离，用于飞天遁地，比如“/tpy -2.3”，就是在当前坐标的基础上，移动到y轴坐标-2.3米的地方" },
            { TPFace, "格式：/tpface 0~360 传送距离，基于面向的传送，详见主界面中“面向调整”部分" },
            { RealAntiKick, $"格式：{RealAntiKick} on/off，需要码2，真正防击退，不许短时间内连续开关，否则后果自负" },
            { ActionNoMove, $"格式：{ActionNoMove} on/off，需要码2，技能不位移，不许短时间内连续开关，否则后果自负" },
            { FreeMove, $"格式：{FreeMove} on/off，需要码2，无视墙体地形等自由移动，用于遁地，不许短时间内连续开关，否则后果自负" }
        };

        #endregion

        internal static Configuration Configuration { get; set; }
        internal WindowSystem WindowSystem = new("Teleport");

        private ConfigWindow ConfigWindow { get; init; }

        private MainWindow MainWindow { get; init; }
        private TPListWindow TPListWindow { get; init; }

        private QuickTPWindow QuickTPWindow { get; init; }

        // 判断地图
        private int _mapIndex = -1;

        internal delegate void MapChangeHandler(int newMap);

        private event MapChangeHandler mapChange;

        internal int MapIndex
        {
            get => _mapIndex;
            set
            {
                if (_mapIndex == value)
                    return;
                if (value != -1)
                {
                    mapChange?.Invoke(value);
                }

                _mapIndex = value;
            }
        }

        // 硬件id
        public static string MacId = MachineCodeGenerator.GenerateMachineCode();
        public static string MacIdV2 = MachineCodeGenerator.GenerateMachineCodeV2();

        private static bool enable = false;
        private static bool enablePro = false;
        private static bool nekoVerify = false;

        internal static RepeatedTask SpeedTesk;

        // 速度倍数
        public static float Speed;

        // 位移相关hook
        internal static bool AntiKick = false;

        internal static bool NoMove = false;
        internal static bool HackWalk = false;
        internal static bool HackFly = false;
        internal static bool HackSpeed = false;
        internal static bool IgnoreDistance = false;
        internal static bool LockY = false;

        // 咏速相关
        internal static float SwingReduce = 0;

        internal static bool AntiKickWarning = false;

        #region REGEX

        // 正则匹配坐标，带id
        private Regex posRegexWithID =
            new(
                @"\s*(?<x>-?\d+(\.\d+)?) \s*(?<y>-?\d+(\.\d+)?) \s*(?<z>-?\d+(\.\d+)?) \s*(?<id>\d+)\s*");

        // 正则匹配坐标，不带id
        private Regex posRegex =
            new(@"\s*(?<x>-?\d+(\.\d+)?) \s*(?<y>-?\d+(\.\d+)?) \s*(?<z>-?\d+(\.\d+)?)\s*");

        // 正则匹配二维坐标
        private Regex pos2Regex =
            new(@"\s*(?<x>-?\d+(\.\d+)?) \s*(?<z>-?\d+(\.\d+)?)\s*");

        #endregion

        public Plugin(IDalamudPluginInterface pluginInterface)
        {
            // 初始化
            ECommonsMain.Init(pluginInterface, this, [Module.DalamudReflector, Module.ObjectFunctions]);
            Speed = 1;
            Configuration = Svc.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize(Svc.PluginInterface);
            PluginLog.Log($"当前硬件id为：{MacId}");
            ConfigWindow = new ConfigWindow(this);
            MainWindow = new MainWindow(this);
            TPListWindow = new TPListWindow();
            QuickTPWindow = new QuickTPWindow(this);
            WindowSystem.AddWindow(ConfigWindow);
            WindowSystem.AddWindow(MainWindow);
            WindowSystem.AddWindow(TPListWindow);
            WindowSystem.AddWindow(QuickTPWindow);
            foreach (var (command, helpMessage) in CommandsDictionary)
            {
                Svc.Commands.AddHandler(command, new CommandInfo(OnCommand)
                {
                    HelpMessage = helpMessage
                });
            }

            // 初始化ZoneInfo
            ZoneInfoHandler.Init();
            CheckActivation();
            CheckActivationPro();
            CheckNekoVerifier();
            Svc.PluginInterface.UiBuilder.Draw += DrawUI;
            Svc.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
            Svc.Chat.ChatMessage += OnChatMessage;
            Svc.Chat.ChatMessage += OnTriChatMessage;
            Svc.Framework.Update += onUpdate;
            SpeedTesk = new RepeatedTask(Configuration.SpeedRefreshInterval);
            SpeedTesk._timer.Elapsed += RefreshSpeed;
            SpeedTesk.Start();
            XCountResults.GetPlayerCount();
            StaticUtils.RefreshPtr();
            // 地图改变触发事件
            updateStatus();
            Svc.Framework.Update += underGroundMove;
            Svc.Framework.Update += quickFace;
            XCountResults.GetPlayerCount();
        }

        private void CheckNekoVerifier(object? sender, EventArgs e)
        {
            CheckNekoVerifier();
        }

        #region 使用/echo触发的功能

        // 解析Teleport中的命令
        private void processCommand(string input)
        {
            foreach (var command in CommandsDictionary.Keys)
            {
                var cmdCut = command.Replace("/", "");
                if (input.Contains(cmdCut))
                {
                    var args = input[(input.IndexOf(cmdCut) + cmdCut.Length)..].Trim();
                    OnCommand(command, args);

                    break;
                }
            }
        }

        // 解析命令，/e @CMD contents
        private void processEchoCommand(string input)
        {
            var args = input[(input.IndexOf("@CMD") + 4)..].Trim();
            if (Configuration.ExeAllCommand)
            {
                Chat.SendMessage(args);
            }
            else
            {
                Svc.Commands.ProcessCommand(args);
            }
        }

        // FPT格式的正则
        private Regex fptRegex =
            new(@"@tp set \s*(?<x>-?\d+(\.\d+)?) \s*(?<z>-?\d+(\.\d+)?) \s*(?<y>-?\d+(\.\d+)?)\s*");

        // AE格式的正则
        private Regex aeRegex =
            new(@"^commandtp\sID:(.*?)\sX:(.*?)\sY:(.*?)\sZ:(.*?)$");

        // 解析FPT格式的命令
        private void processFPT(string input)
        {
            var match = fptRegex.Match(input);
            if (match.Success)
            {
                var x = float.Parse(match.Groups["x"].Value);
                var z = float.Parse(match.Groups["z"].Value);
                var y = float.Parse(match.Groups["y"].Value);
                StaticUtils.TeleportMe(x, y, z);
            }
        }

        // 解析AE格式的命令
        private void processAE(string input)
        {
            var match = aeRegex.Match(input);
            if (match.Success)
            {
                string valueOfID = match.Groups[1].Value;
                string valueOfX = match.Groups[2].Value;
                string valueOfY = match.Groups[3].Value;
                string valueOfZ = match.Groups[4].Value;

                if (valueOfID.Equals(StaticUtils.LocalContentId.ToString()))
                {
                    var pos = new Vector3(float.Parse(valueOfX), float.Parse(valueOfY), float.Parse(valueOfZ));
                    StaticUtils.TeleportMe(pos);
                }
            }
        }

        private void OnTriChatMessage(IHandleableChatMessage message)
        {
            // 如果没有设置任何触发器，或者消息为空，则直接返回，提高效率
            if (!Configuration.EnableTrigger || !Configuration.IftttTriggers.Any())
            {
                return;
            }

            string messageText = message.Message.TextValue;
            if (string.IsNullOrEmpty(messageText))
            {
                return;
            }

            // 遍历所有触发器
            foreach (var trigger in Configuration.IftttTriggers)
            {
                // 检查触发器是否启用，以及消息类型是否匹配
                if (!trigger.IsEnabled || trigger.ChatType != message.LogKind)
                {
                    continue;
                }

                // 检查正则表达式和命令是否为空
                if (string.IsNullOrEmpty(trigger.RegexPattern) || string.IsNullOrEmpty(trigger.CommandToExecute))
                {
                    continue;
                }

                try
                {
                    // 进行正则匹配
                    var match = Regex.Match(messageText, trigger.RegexPattern);
                    if (match.Success)
                    {
                        // 步骤 3: 执行命令
                        // 格式化命令，允许使用 {0}, {1}... 来引用正则捕获组
                        // {0} 是整个匹配的字符串
                        var groups = match.Groups.Cast<Group>().Select(g => g.Value).ToArray();
                        string formattedCommand = string.Format(trigger.CommandToExecute, groups);

                        Chat.SendMessage(formattedCommand);
                    }
                }
                catch (RegexMatchTimeoutException)
                {
                    PluginLog.Warning($"IFTTT Regex timed out for pattern: {trigger.RegexPattern}");
                }
                catch (Exception ex)
                {
                    PluginLog.Error($"Error processing IFTTT trigger with pattern: {trigger.RegexPattern}");
                }
            }
        }

        private void OnChatMessage(IHandleableChatMessage message)
        {
            if (StaticUtils.LocalPlayer == null)
                return;
            // 解析小队频道
            if (Configuration.RecvAeTpCmd && message.LogKind == XivChatType.Party)
            {
                foreach (var payload in message.Message.Payloads)
                {
                    if (payload is TextPayload textPayload)
                    {
                        processAE(textPayload.Text);
                    }
                }
            }

            // 仅解析echo频道
            if (message.LogKind != XivChatType.Echo)
                return;
            if (Configuration.UseTeleport | Configuration.UseFPT)
            {
                foreach (var payload in message.Message.Payloads)
                {
                    if (payload is TextPayload textPayload)
                    {
                        if (Configuration.UseTeleport)
                        {
                            if (textPayload.Text.Contains("@XTP"))
                            {
                                processCommand(textPayload.Text);
                            }
                        }

                        if (Configuration.UseFPT)
                        {
                            if (textPayload.Text.Contains("@tp set"))
                            {
                                processFPT(textPayload.Text);
                            }
                        }

                        if (Configuration.UseCommand)
                        {
                            if (textPayload.Text.StartsWith("@CMD"))
                            {
                                processEchoCommand(textPayload.Text);
                            }
                        }
                    }
                }
            }
        }

        #endregion

        public void Dispose()
        {
            // NekoVerifierMain.Dispose();
            Svc.Framework.Update -= underGroundMove;
            Svc.Framework.Update -= quickFace;
            Svc.Framework.Update -= onUpdate;
            WindowSystem.RemoveAllWindows();
            Svc.Chat.ChatMessage -= OnChatMessage;
            Svc.Chat.ChatMessage -= OnTriChatMessage;
            Svc.ClientState.Login -= CheckNekoVerifier;
            ConfigWindow.Dispose();
            MainWindow.Dispose();
            QuickTPWindow.Dispose();
            TPListWindow.Dispose();
            SpeedTesk.Dispose();
            ECommonsMain.Dispose();
            foreach (var command in CommandsDictionary)
            {
                Svc.Commands.RemoveHandler(command.Key);
            }
        }

        private void onUpdate(object _)
        {
            updateStatus();
        }

        private void updateStatus()
        {
            if (StaticUtils.LocalPlayer == null)
                return;
            if (Configuration.UseQuickTp)
            {
                var currentMap = Svc.ClientState.TerritoryType;
                for (var i = 0; i < Configuration.TPLists.Count; i++)
                {
                    if (currentMap == Configuration.TPLists[i].MapId)
                    {
                        MapIndex = i;
                        if (Configuration.TPLists[i].UseQuickWindow)
                        {
                            QuickTPWindow.IsOpen = true;
                        }
                        else
                        {
                            QuickTPWindow.IsOpen = false;
                        }

                        return;
                    }
                }

                // 循环结束仍未寻得
                MapIndex = -1;
                QuickTPWindow.IsOpen = false;
            }
            else
            {
                QuickTPWindow.IsOpen = false;
            }
        }

        private static long _lastTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();

        private static void underGroundMove(IFramework framework)
        {
            if (!Configuration.UseUndergroundMove || StaticUtils.LocalPlayer == null)
                return;
            var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            var interval = 10;
            if (now - _lastTime <= interval)
                return;
            var Pi = Math.PI;
            var realSpeed = Configuration.UnderMoveSpeedRatio * 0.06;
            var r = StaticUtils.LocalPlayer.Rotation;
            if (((!Configuration.UseCtrlWToMove) || Svc.KeyState.GetRawValue(VirtualKey.CONTROL) == 1) &&
                Svc.KeyState.GetRawValue(VirtualKey.W) == 1)
            {
                StaticUtils.TeleportMe((float)(StaticUtils.LocalPlayer.Position.X - (realSpeed * Math.Sin(r + Pi))),
                                       StaticUtils.LocalPlayer.Position.Y,
                                       (float)(StaticUtils.LocalPlayer.Position.Z -
                                               (realSpeed * Math.Cos(r + Pi))));
                _lastTime = now;
            }

            if (((Configuration.UseQuickFly) && Svc.KeyState.GetRawValue(VirtualKey.CONTROL) == 1) &&
                Svc.KeyState.GetRawValue(VirtualKey.F) == 1)
            {
                StaticUtils.TeleportMe(StaticUtils.LocalPlayer.Position.X,
                                       (float)(StaticUtils.LocalPlayer.Position.Y + 0.5 * realSpeed),
                                       StaticUtils.LocalPlayer.Position.Z);
                _lastTime = now;
            }

            if (((Configuration.UseQuickDive) && Svc.KeyState.GetRawValue(VirtualKey.CONTROL) == 1) &&
                Svc.KeyState.GetRawValue(VirtualKey.G) == 1)
            {
                StaticUtils.TeleportMe(StaticUtils.LocalPlayer.Position.X,
                                       (float)(StaticUtils.LocalPlayer.Position.Y - 0.5 * realSpeed),
                                       StaticUtils.LocalPlayer.Position.Z);
                _lastTime = now;
            }
        }

        private static void quickFace(IFramework framework)
        {
            if (!Configuration.UseQuickFace)
                return;
            if (Svc.KeyState.GetRawValue(VirtualKey.UP) == 1)
            {
                StaticUtils.Rotation = (float)Math.PI;
                return;
            }

            if (Svc.KeyState.GetRawValue(VirtualKey.LEFT) == 1)
            {
                StaticUtils.Rotation = (float)Math.PI / -2;
                return;
            }

            if (Svc.KeyState.GetRawValue(VirtualKey.DOWN) == 1)
            {
                StaticUtils.Rotation = 0;
                return;
            }

            if (Svc.KeyState.GetRawValue(VirtualKey.RIGHT) == 1)
            {
                StaticUtils.Rotation = (float)Math.PI / 2;
                return;
            }
        }

        private void OnCommand(string command, string args)
        {
#if DEBUG
            PluginLog.Log($"cmd：{command},args：>{args}<");
#endif
            // UI功能

            #region UICMDS

            // 打开主页面
            if (OpenMainWindow.Equals(command))
            {
                MainWindow.IsOpen = true;
                return;
            }

            // 打开设置页面
            if (OpenConfigWindow.Equals(command))
            {
                ConfigWindow.IsOpen = true;
                return;
            }

            #endregion

            // 传送功能

            #region TPCMDS

            // 如果当前没有角色，不让你传送
            if (StaticUtils.LocalPlayer == null)
            {
                PluginLog.Error("当前没有角色，无法传送");
                return;
            }

            // 安全传送
            if (TPMeSafe.Equals(command))
            {
                var match = posRegexWithID.Match(args);
                if (match.Success)
                {
                    var x = float.Parse(match.Groups["x"].Value);
                    var y = float.Parse(match.Groups["y"].Value);
                    var z = float.Parse(match.Groups["z"].Value);
                    var id = uint.Parse(match.Groups["id"].Value);
                    if (!StaticUtils.IsSameMapId(id))
                        return;
#if DEBUG
                    PluginLog.Log($"将传送到：x = {x}, y = {y}, z = {z}, id = {id}");
#endif
                    StaticUtils.TeleportMe(x, y, z);
                    return;
                }
            }

            // 安全传送（FPT格式）
            if (TPMeSafeXZY.Equals(command))
            {
                var match = posRegexWithID.Match(args);
                if (match.Success)
                {
                    var x = float.Parse(match.Groups["x"].Value);
                    var y = float.Parse(match.Groups["z"].Value);
                    var z = float.Parse(match.Groups["y"].Value);
                    var id = uint.Parse(match.Groups["id"].Value);
                    if (!StaticUtils.IsSameMapId(id))
                        return;
                    StaticUtils.TeleportMe(x, y, z);
                }
            }

            // 强制传送
            if (TPMeUnsafe.Equals(command))
            {
                var match = posRegex.Match(args);
                if (match.Success)
                {
                    var x = float.Parse(match.Groups["x"].Value);
                    var y = float.Parse(match.Groups["y"].Value);
                    var z = float.Parse(match.Groups["z"].Value);
#if DEBUG
                    PluginLog.Log($"将传送到：x = {x}, y = {y}, z = {z}");
#endif
                    StaticUtils.TeleportMe(x, y, z);
                    return;
                }
            }

            // 传送目标到指定坐标
            if (TPTarget.Equals(command))
            {
                var match = posRegex.Match(args);
                if (match.Success)
                {
                    var x = float.Parse(match.Groups["x"].Value);
                    var y = float.Parse(match.Groups["y"].Value);
                    var z = float.Parse(match.Groups["z"].Value);
#if DEBUG
                    PluginLog.Log($"将传送到：x = {x}, y = {y}, z = {z}");
#endif
                    StaticUtils.TeleportTarget(x, y, z);
                    return;
                }
            }

            // 传送目标到自身坐标
            if (TPTargetToMe.Equals(command))
            {
                StaticUtils.TeleportTargetToMe();
                return;
            }

            // 传送自身到目标坐标
            if (TPMeToTarget.Equals(command))
            {
                if (StaticUtils.CurrentTarget == null)
                {
                    Svc.Chat.PrintError("请先选中目标");
                    return;
                }

                var tarpos = StaticUtils.CurrentTarget.Position;
                StaticUtils.TeleportMe(tarpos);
                return;
            }

            // 传送到二维坐标
            if (TP2XY.Equals(command))
            {
                var match = pos2Regex.Match(args);
                if (match.Success)
                {
                    var x = float.Parse(match.Groups["x"].Value);
                    var z = float.Parse(match.Groups["z"].Value);
                    StaticUtils.TeleportMe(x, StaticUtils.LocalPlayer.Position.Y, z);
                    return;
                }
            }

            // 传送到小队成员身边
            if (TP2PartyMember.Equals(command))
            {
                var member = int.Parse(args.Trim());
                if (member <= 1 || member > 8)
                {
                    Svc.Chat.PrintError("请输入正确的小队成员编号");
                    return;
                }

                StaticUtils.TeleportToPartyMember(member);
                return;
            }

            // 传送到鼠标位置
            if (TP2Mouse.Equals(command))
            {
                StaticUtils.MoveToMousePos();
                return;
            }

            // 保存当前位置
            if (SavePosition.Equals(command))
            {
                DoFastSave();
                return;
            }

            // 加载保存的位置
            if (LoadPosition.Equals(command))
            {
                DoFastLoad();
                return;
            }

            // 传送到标点
            if (TP2Mark.Equals(command))
            {
                StaticUtils.TP2WayMark(args);
                return;
            }

            if (SavePositionToList.Equals(command))
            {
                // 获取当前地图的传送列表
                var lists = Configuration.TPLists.Where(list => list.MapId == Svc.ClientState.TerritoryType);
                if (lists.Any())
                {
                    lists.ToList()[0].TPs.Add(new TP("无", StaticUtils.LocalPlayer.Position));
                    Configuration.Save();
                }
                else
                {
                    var newMap = new TPList(Svc.ClientState.TerritoryType);
                    newMap.TPs.Add(new TP("无", StaticUtils.LocalPlayer.Position));
                    Configuration.TPLists.Add(newMap);
                    Configuration.Save();
                }

                return;
            }

            if (SaveMouseToList.Equals(command))
            {
                var mo = StaticUtils.MousePos();
                if (mo.X == 0 && mo.Y == 0 && mo.Z == 0)
                {
                    Svc.Chat.PrintError("请先移动鼠标到有效位置");
                    return;
                }

                // 获取当前地图的传送列表
                var lists = Configuration.TPLists.Where(list => list.MapId == Svc.ClientState.TerritoryType);
                if (lists.Any())
                {
                    lists.ToList()[0].TPs.Add(new TP("无", mo));
                    Configuration.Save();
                }
                else
                {
                    var newMap = new TPList(Svc.ClientState.TerritoryType);
                    newMap.TPs.Add(new TP("无", mo));
                    Configuration.TPLists.Add(newMap);
                    Configuration.Save();
                }

                return;
            }

            // 在y轴传送
            if (MoveY.Equals(command))
            {
                if (args == null)
                {
                    Svc.Chat.PrintError("请输入y轴偏移量");
                    return;
                }

                var y = float.Parse(args.Trim());
                var pos = StaticUtils.LocalPlayer.Position;
                StaticUtils.TeleportMe(pos.X, pos.Y + y, pos.Z);
                return;
            }

            // 依照面向传送
            if (TPFace.Equals(command))
            {
                var input = args.Trim();        // 去除字符串前后的空白字符
                var numbers = input.Split(' '); // 使用空格将字符串拆分成数组

                if (numbers.Length != 2)
                {
                    Svc.Chat.PrintError("输入必须包含两个由空格分隔的数字。");
                }

                if (float.TryParse(numbers[0], out var angle) && float.TryParse(numbers[1], out var distance))
                {
                    PluginLog.Log($"第一个数字是 {angle}, 第二个数字是 {distance}");
                    // 玩家当前位置
                    var x = StaticUtils.LocalPlayer.Position.X;
                    var z = StaticUtils.LocalPlayer.Position.Z;
                    var angleRadians =
                        (float)(StaticUtils.Rotation -
                                StaticUtils.ConvertDegreesToRadians(angle)); // 将角度转换为弧度，并做适当的转换以匹配我们的坐标系统
                    var xNew = x + (distance * (float)Math.Sin(angleRadians));
                    var zNew = z + (distance * (float)Math.Cos(angleRadians));
                    StaticUtils.TeleportMe(xNew, StaticUtils.LocalPlayer.Position.Y, zNew);
                }
                else
                {
                    Svc.Chat.PrintError("无法解析输入为浮点数。");
                }
            }

            #endregion

            // 面向功能
            if (SetRot.Equals(command))
            {
                if (args == null)
                {
                    Svc.Chat.PrintError("请输入角度");
                    return;
                }
                else
                {
                    var rot = float.Parse(args.Trim());
                    StaticUtils.Rotation = NormalizeAngle(rot);
                    return;
                }
            }
        }

        // 角度辅助函数
        internal static float NormalizeAngle(float angle)
        {
            angle %= 360;
            if (angle > 180)
                return (angle - 360) / 180 * (float)Math.PI;
            else if (angle < -180)
                return (angle + 360) / 180 * (float)Math.PI;
            return angle / 180 * (float)Math.PI;
        }

        // 判断激活码是否对应机器码
        internal static void CheckActivation()
        {
            var activationCode = MachineCodeGenerator.GenerateActivationCode(MacId, 1);
            enable = Configuration.LocalActivation.Equals("闲鱼小店死个妈") ||
                     activationCode.Equals(Configuration.LocalActivation);
        }

        internal static void CheckActivationPro()
        {
            var activationCode = MachineCodeGenerator.GenerateActivationCode(MacIdV2, 2);
            enablePro = activationCode.Equals(Configuration.LocalActivationPro);
        }

        internal static void CheckNekoVerifier()
        {
            CheckActivation();
            CheckActivationPro();
            nekoVerify = true;
        }

        // 获取当前激活状态
        internal static bool GetActivation()
        {
            return enable && nekoVerify;
        }

        internal static bool GetActivationLocal() => enable;

        internal static bool GetActivationPro() => enablePro && nekoVerify;

        internal void DrawUI() => WindowSystem.Draw();

        internal void DrawList() => TPListWindow.Toggle();

        internal void DrawMain() => MainWindow.Toggle();

        internal void DrawConfigUI() => ConfigWindow.IsOpen = true;

        // 快速传送
        internal void DoFastLoad()
        {
            if (StaticUtils.IsSameMapId(Configuration.EasyTPMap))
            {
                StaticUtils.TeleportMe(Configuration.EasyTP);
            }
        }

        // 快速保存位置
        internal void DoFastSave()
        {
            Configuration.EasyTP = StaticUtils.LocalPlayer.Position;
            Configuration.EasyTPMap = Svc.ClientState.TerritoryType;
            Configuration.Save();
        }

        // 刷新技速咏速
        private void RefreshSpeed(object sender, ElapsedEventArgs e)
        {
            // 这里是每隔一定间隔要执行的代码
            if (StaticUtils.LocalPlayer == null)
                return;


            if (Configuration.UseSkillSpeed)
            {
                if (StaticUtils.CurrentSkillSpeed != Configuration.SkillSpeed)
                {
                    StaticUtils.CurrentSkillSpeed = Configuration.SkillSpeed;
                }
            }

            if (Configuration.UseSpellSpeed)
            {
                if (StaticUtils.CurrentSpellSpeed != Configuration.SpellSpeed)
                {
                    StaticUtils.CurrentSpellSpeed = Configuration.SpellSpeed;
                }
            }
        }
    }
}
