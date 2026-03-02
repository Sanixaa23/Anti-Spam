using Rocket.API;

namespace Sanixaa.Anti_Spam
{
    public class Anti_SpamConfiguration : IRocketPluginConfiguration
    {
        // Second
        public float WindowSeconds = 4f;

        // Max message in WindowSeconds
        public int MaxMessagesInWindow = 3;

        // Mute seconds
        public int MuteSeconds = 15;

        // mute message
        public string MutedMessage = "Dont Spam! You have been muted from chat for spamming time: {sec} seconds";

        // spam warning
        public string WarningMessage = "Spam detected, please dont spam or you'll be punished!";

        // bypass perm
        public string BypassPermission = "antispam.bypass";

        public bool LogToConsole = true;

        // Discord webhook logging
        public bool LogToDiscord = false;
        public string DiscordWebhookUrl = "";
        public string DiscordUsername = "Anti-Spam";
        public string DiscordMention = ""; // "<@&ROLE_ID>" veya "<@USER_ID>"

        public void LoadDefaults()
        {
            WindowSeconds = 4f;
            MaxMessagesInWindow = 3;
            MuteSeconds = 15;
            MutedMessage = "Dont Spam! You have been muted from chat for spamming time: {sec} seconds";
            WarningMessage = "Spam detected, please dont spam or you'll be punished!";
            BypassPermission = "antispam.bypass";
            LogToConsole = true;

            LogToDiscord = false;
            DiscordWebhookUrl = "";
            DiscordUsername = "Anti-Spam";
            DiscordMention = "";
        }
    }
}