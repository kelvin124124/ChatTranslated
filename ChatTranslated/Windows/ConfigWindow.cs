using ChatTranslated.Utils;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Numerics;

namespace ChatTranslated.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    public ConfigWindow(Plugin plugin) : base(
        "A Wonderful Configuration Window",
        ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
        ImGuiWindowFlags.NoScrollWithMouse)
    {
        Size = new Vector2(232, 75);
        SizeCondition = ImGuiCond.Always;

        configuration = Service.configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        // Enabled
        bool _Enabled = configuration.enabled;
        if (ImGui.Checkbox("Enable", ref _Enabled))
        {
            configuration.enabled = _Enabled;
            configuration.Save();
        }
    }
}
