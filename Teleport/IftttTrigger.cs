using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin;
using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Configuration;
using Dalamud.Game.Gui;
using Dalamud.Logging;

namespace Teleport
{
    [Serializable]
    public class IftttTrigger
    {
        public XivChatType ChatType { get; set; } = XivChatType.Echo;
        public string RegexPattern { get; set; } = ".*";
        public string CommandToExecute { get; set; } = "/e IFTTT Triggered: {0}";
        public bool IsEnabled { get; set; } = true;

        // 用于ImGui编辑时临时存储的变量，避免在输入时频繁进行Regex编译
        [NonSerialized]
        public string TestMessage = string.Empty;
        [NonSerialized]
        public string TestResult = string.Empty;
    }
}
