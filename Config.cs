using Exiled.API.Interfaces;
using System.ComponentModel;

namespace BubbleChat
{
    public class Config : IConfig
    {
        [Description("Is the plugin enabled?")]
        public bool IsEnabled { get; set; } = true;

        [Description("Debug mode - shows additional information in console")]
        public bool Debug { get; set; } = true;

        [Description("Chat range in meters - how far players can hear the chat")]
        public float ChatRange { get; set; } = 15f;

        [Description("Text visibility range in meters - how far players can see the chat text")]
        public float TextVisibilityRange { get; set; } = 8f;

        [Description("How long the chat message stays visible (in seconds)")]
        public float MessageDuration { get; set; } = 5f;

        [Description("Maximum characters allowed per message")]
        public int MaxMessageLength { get; set; } = 50;

        [Description("Message format - {0} will be replaced with the message")]
        public string MessageFormat { get; set; } = "{0}";

        [Description("Color for the 'CHAT:' prefix (hex color code)")]
        public string ChatPrefixColor { get; set; } = "#00FF00";

        [Description("Color for chat messages (hex color code)")]
        public string MessageColor { get; set; } = "#FFFFFF";

        [Description("Color for character counter (hex color code)")]
        public string CounterColor { get; set; } = "#888888";

        [Description("Color for hint messages (hex color code)")]
        public string HintColor { get; set; } = "#FFD700";

        [Description("Text size scale (1.0 = normal size, 0.5 = half size, 2.0 = double size)")]
        public float TextSize { get; set; } = 1.0f;

        [Description("Height offset from player head (in Unity units)")]
        public float HeightOffset { get; set; } = 0.9f;

        [Description("Enable/disable chat text bobbing animation")]
        public bool EnableBobbing { get; set; } = true;

        [Description("Bobbing animation intensity (0.0 = no bobbing, 0.01 = slight bobbing)")]
        public float BobbingIntensity { get; set; } = 0.005f;
    }
}