using ChatTranslated.Chat;
using ChatTranslated.Localization;
using ChatTranslated.Translate;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ChatTranslated.Utils
{
    internal partial class ChatHandler
    {
        private Dictionary<string, int> lastMessageTime = [];

        public ChatHandler()
        {
            Service.chatGui.ChatMessage += OnChatMessage;
        }

        private void OnChatMessage(XivChatType type, int _, ref SeString sender, ref SeString message, ref bool isHandled)
        {
            if (isHandled) return;
            HandleChatMessage(type, sender, message);
        }

        private async void HandleChatMessage(XivChatType type, SeString sender, SeString message)
        {
            if (!Service.configuration.Enabled || sender.TextValue.Contains("[CT]") || !Service.configuration.SelectedChatTypes.Contains(type))
                return;

            if (!Service.configuration.EnabledInDuty && Service.condition[ConditionFlag.BoundByDuty])
                return;

            var playerPayload = sender.Payloads.OfType<PlayerPayload>().FirstOrDefault();
            string playerName = Sanitize(playerPayload?.PlayerName ?? sender.ToString()).Trim();
            string localPlayerName = Sanitize(Service.clientState.LocalPlayer?.Name.ToString() ?? string.Empty).Trim();
            if (type == XivChatType.TellOutgoing)
                playerName = localPlayerName;

            if (playerName == localPlayerName)
            {
                Service.mainWindow.PrintToOutput($"{playerName}: {message.TextValue}");
                return;
            }

            var chatMessage = new Message(playerName, MessageSource.Chat, message, type);
            chatMessage.Context = GetChatMessageContext();

            if (IsFilteredMessage(playerName, chatMessage.CleanedContent) || IsJPFilteredMessage(chatMessage))
            {
                Service.mainWindow.PrintToOutput($"{playerName}: {message.TextValue}");
                return;
            }

            bool needsTranslation = Service.configuration.SelectedLanguageSelectionMode switch
            {
                Configuration.LanguageSelectionMode.Default => ChatRegex.NonEnglishRegex().IsMatch(chatMessage.CleanedContent),
                Configuration.LanguageSelectionMode.CustomLanguages => await IsCustomSourceLanguage(chatMessage),
                Configuration.LanguageSelectionMode.AllLanguages => true,
                _ => false
            };

            if (needsTranslation)
            {
                await Translator.TranslateMessage(chatMessage);
                OutputMessage(chatMessage, type);
            }
            else
            {
                Service.mainWindow.PrintToOutput($"{chatMessage.Sender}: {chatMessage.CleanedContent}");
            }
        }

        public unsafe string GetChatMessageContext()
        {
            try
            {
                var chatLogPtr = Service.gameGui.GetAddonByName("ChatLogPanel_0");
                if (chatLogPtr != 0)
                {
                    var chatLog = (AddonChatLogPanel*)chatLogPtr;
                    var lines = SeString.Parse(chatLog->ChatText->GetText()).TextValue
                        .Split('\r')
                        .TakeLast(15)
                        .ToList();

                    if (Service.condition[ConditionFlag.BoundByDuty])
                        lines.Add("In instanced area: true");
                    if (Service.condition[ConditionFlag.InCombat])
                        lines.Add("In combat: true");

                    return string.Join('\n', lines);
                }
            }
            catch (Exception ex)
            {
                Service.pluginLog.Error(ex, "Failed to read chat context.");
            }

            return string.Empty;
        }

        private async Task<bool> IsCustomSourceLanguage(Message chatMessage)
        {
            var language = await Translator.DetermineLanguage(chatMessage.CleanedContent);
            return Service.configuration.SelectedSourceLanguages.Contains(language);
        }

        internal static void OutputMessage(Message chatMessage, XivChatType type = XivChatType.Say)
        {
            if (chatMessage.OriginalContent.TextValue == chatMessage.TranslatedContent 
                && chatMessage.Source != MessageSource.MainWindow) // no need to output if translation is the same
            {
                Service.pluginLog.Debug("Translation is the same as original. Skipping output.");
                return;
            }

            string outputStr = Service.configuration.ChatIntegration_HideOriginal
                ? chatMessage.TranslatedContent!
                : $"{chatMessage.OriginalContent.TextValue} || {chatMessage.TranslatedContent}";

            Service.mainWindow.PrintToOutput($"{chatMessage.Sender}: {outputStr}");

            if (Service.configuration.ChatIntegration)
            {
                Plugin.OutputChatLine(type, chatMessage.Sender, outputStr);
            }
        }

        private bool IsFilteredMessage(string sender, string message)
        {
            if (message.Trim().Length < 2 || IsMacroMessage(sender))
            {
                Service.pluginLog.Debug("Message filtered: " + (message.Trim().Length < 2
                    ? "Single character or empty message."
                    : "Macro messages."));
                return true;
            }
            return false;
        }

        private bool IsMacroMessage(string playerName)
        {
            int now = Environment.TickCount;
            if (lastMessageTime.TryGetValue(playerName, out int lastMsgTime) && now - lastMsgTime < 600)
            {
                lastMessageTime[playerName] = now;
                return true;
            }
            lastMessageTime[playerName] = now;
            return false;
        }

        private static bool IsJPFilteredMessage(Message chatMessage)
        {
            if (ChatRegex.JPWelcomeRegex().IsMatch(chatMessage.CleanedContent))
            {
                chatMessage.TranslatedContent = Resources.WelcomeStr;

                OutputMessage(chatMessage);
                return true;
            }
            if (ChatRegex.JPByeRegex().IsMatch(chatMessage.CleanedContent))
            {
                chatMessage.TranslatedContent = Resources.GGstr;

                OutputMessage(chatMessage);
                return true;
            }
            if (ChatRegex.JPDomaRegex().IsMatch(chatMessage.CleanedContent))
            {
                chatMessage.TranslatedContent = Resources.DomaStr;

                OutputMessage(chatMessage);
                return true;
            }

            return false;
        }

        public static string Sanitize(string input) => ChatRegex.SpecialCharacterRegex().Replace(input, " ");

        public void Dispose() => Service.chatGui.ChatMessage -= OnChatMessage;
    }
}
