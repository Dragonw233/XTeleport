using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Teleport
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;
        public Vector3 EasyTP { get; set; } = new Vector3(0, 0, 0);
        public uint EasyTPMap { get; set; } = 0;
        public string LocalActivation { get; set; } = "";
        public string LocalActivationPro { get; set; } = "";
        // 传送列表
        public List<TPList> TPLists { get; set; } = new List<TPList>();
        // 解析聊天
        public bool UseFPT { get; set; } = false;
        public bool UseTeleport { get; set; } = false;
        public bool UseCommand { get; set; } = false;
        public bool ExeAllCommand { get; set; } = false;
        public bool UseQuickTp { get; set; } = false;
        public bool UseSkillSpeed { get; set; } = false;
        public bool UseSpellSpeed { get; set; } = false;
        public ushort SkillSpeed { get; set; } = 400;
        public ushort SpellSpeed { get; set; } = 400;
        public double SpeedRefreshInterval { get; set; } = 0.5;
        public bool HideXYZ { get; set; } = true;
        public int XCountThreshold { get; set; } = 0;

        // 遁地位移相关
        public bool UseUndergroundMove { get; set; } = false;
        public bool UseCtrlWToMove { get; set; } = false;
        public float UnderMoveSpeedRatio { get; set; } = 1.0f;
        // 快速遁地
        public bool UseQuickDive { get; set; } = false;
        // 快速升天
        public bool UseQuickFly { get; set; } = false;
        // 快速转身
        public bool UseQuickFace { get; set; } = false;
        // 接受AETP指令
        public bool RecvAeTpCmd { get; set; } = false;
        // 安全模式
        public bool SafeMode { get; set; } = true;
        // 触发器
        public bool EnableTrigger { get; set; } = false;
        public List<IftttTrigger> IftttTriggers { get; set; } = new List<IftttTrigger>();

        [NonSerialized]
        private IDalamudPluginInterface? PluginInterface;

        public void Initialize(IDalamudPluginInterface pluginInterface) => PluginInterface = pluginInterface;

        public void Save() => PluginInterface!.SavePluginConfig(this);
    }
}
