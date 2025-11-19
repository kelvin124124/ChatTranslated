using ChatTranslated.Localization;
using ChatTranslated.Utils;
using Dalamud.Bindings.ImGui;

namespace ChatTranslated.Windows.ConfigTabs.TranslationEngineTabs;

public static class DeepLSettings
{
    private static string DeepLApiKeyInput = Service.configuration.DeepL_API_Key;

    public static void Draw(Configuration configuration)
    {
        ImGui.TextUnformatted(Resources.DeepLAPIKey);
        ImGui.InputText("##APIKey", ref DeepLApiKeyInput, 100);

        ImGui.SameLine();
        if (ImGui.Button(Resources.Apply + "###DeepL_API_Key"))
        {
            configuration.DeepL_API_Key = DeepLApiKeyInput;
            configuration.Save();

            Plugin.OutputChatLine($"DeepL API Key {configuration.DeepL_API_Key[..12]}... saved successfully.");
        }
    }
}
