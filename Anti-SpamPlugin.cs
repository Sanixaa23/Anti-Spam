using Rocket.API;
using Rocket.Core.Plugins;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Events;
using Rocket.Unturned.Player;
using SDG.Unturned;
using ShimmyMySherbet.DiscordWebhooks;
using ShimmyMySherbet.DiscordWebhooks.Models;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using RocketLogger = Rocket.Core.Logging.Logger;

namespace Sanixaa.Anti_Spam
{
    public class AntiSpamPlugin : RocketPlugin<Anti_SpamConfiguration>
    {
        private class SpamState
        {
            public readonly Queue<float> MessageTimes = new Queue<float>();
            public float MutedUntil;
            public int WarnCount;
        }

        private readonly Dictionary<ulong, SpamState> _states = new Dictionary<ulong, SpamState>();

        protected override void Load()
        {
            UnturnedPlayerEvents.OnPlayerChatted += OnPlayerChatted;

            RocketLogger.Log($"[Sanixaa Anti-Spam] Loaded. Window={Configuration.Instance.WindowSeconds}s Max={Configuration.Instance.MaxMessagesInWindow} Mute={Configuration.Instance.MuteSeconds}s");
        }

        protected override void Unload()
        {
            UnturnedPlayerEvents.OnPlayerChatted -= OnPlayerChatted;
            _states.Clear();

            RocketLogger.Log("[Sanixaa Anti-Spam] Unloaded.");
        }

        private void OnPlayerChatted(UnturnedPlayer player, ref Color color, string message, EChatMode chatMode, ref bool cancel)
        {
            if (player == null) return;

            // bypass permission
            if (!string.IsNullOrEmpty(Configuration.Instance.BypassPermission) &&
                player.HasPermission(Configuration.Instance.BypassPermission))
                return;

            if (string.IsNullOrWhiteSpace(message)) return;

            ulong id = player.CSteamID.m_SteamID;

            if (!_states.TryGetValue(id, out var state))
            {
                state = new SpamState();
                _states[id] = state;
            }

            float now = Time.realtimeSinceStartup;
            if (now < state.MutedUntil)
            {
                cancel = true;
                int remain = Mathf.CeilToInt(state.MutedUntil - now);
                UnturnedChat.Say(player, Configuration.Instance.MutedMessage.Replace("{sec}", remain.ToString()));
                return;
            }

            float window = Configuration.Instance.WindowSeconds;
            while (state.MessageTimes.Count > 0 && (now - state.MessageTimes.Peek()) > window)
                state.MessageTimes.Dequeue();

            state.MessageTimes.Enqueue(now);

            if (state.MessageTimes.Count > Configuration.Instance.MaxMessagesInWindow)
            {
                cancel = true;
                state.WarnCount++;

                if (state.WarnCount <= 1)
                {
                    UnturnedChat.Say(player, Configuration.Instance.WarningMessage);

                    if (Configuration.Instance.LogToConsole)
                        RocketLogger.Log($"[Sanixaa Anti-Spam] Warn {player.CharacterName} ({id})");

                    LogDiscord($"⚠️ Spam warning: {player.CharacterName} ({id})");
                }
                else
                {
                    state.MutedUntil = now + Configuration.Instance.MuteSeconds;
                    state.MessageTimes.Clear();

                    UnturnedChat.Say(player, Configuration.Instance.MutedMessage.Replace("{sec}", Configuration.Instance.MuteSeconds.ToString()));

                    if (Configuration.Instance.LogToConsole)
                        RocketLogger.Log($"[Sanixaa Anti-Spam] Muted {player.CharacterName} ({id}) for {Configuration.Instance.MuteSeconds}s");

                    LogDiscord($"🔇 Muted: {player.CharacterName} ({id}) for {Configuration.Instance.MuteSeconds}s");
                }
            }
        }

        private void LogDiscord(string text)
        {
            var cfg = Configuration.Instance;

            if (!cfg.LogToDiscord) return;
            if (string.IsNullOrWhiteSpace(cfg.DiscordWebhookUrl)) return;

            // mention support
            if (!string.IsNullOrWhiteSpace(cfg.DiscordMention))
                text = cfg.DiscordMention + " " + text;

            // Fire-and-forget so we don't block main thread
            _ = Task.Run(() => SendDiscordSafe(cfg.DiscordWebhookUrl, cfg.DiscordUsername, text));
        }

        private void SendDiscordSafe(string webhookUrl, string username, string content)
        {
            try
            {
                // 1) create message instance (try WebhookMessage from Models)
                object msgObj = CreateWebhookMessage(username, content);
                if (msgObj == null)
                {
                    if (Configuration.Instance.LogToConsole)
                        RocketLogger.LogWarning("[Anti-Spam] WebhookMessage type not found in ShimmyMySherbet.DiscordWebhooks.Models");
                    return;
                }

                
                var svcType = typeof(DiscordWebhookService);
                MethodInfo target = null;

                foreach (var m in svcType.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    var p = m.GetParameters();
                    if (p.Length != 2) continue;

                    if (p[0].ParameterType != typeof(string)) continue;

                    
                    if (!p[1].ParameterType.IsAssignableFrom(msgObj.GetType())) continue;

                    target = m;
                    break;
                }

                if (target == null)
                {
                    if (Configuration.Instance.LogToConsole)
                        RocketLogger.LogWarning("[Anti-Spam] No compatible DiscordWebhookService method found (expected static method: (string, WebhookMessage)).");
                    return;
                }

                
                object result = target.Invoke(null, new object[] { webhookUrl, msgObj });

                
                if (result is Task t)
                {
                    
                    t.Wait(TimeSpan.FromSeconds(3));
                }
            }
            catch (Exception ex)
            {
                if (Configuration.Instance.LogToConsole)
                    RocketLogger.LogWarning("[Anti-Spam] Discord webhook error: " + ex.Message);
            }
        }

        private object CreateWebhookMessage(string username, string content)
        {
            
            try
            {
                var msg = new WebhookMessage();

                
                msg.Username = string.IsNullOrWhiteSpace(username) ? "Anti-Spam" : username;
                msg.Content = content;

                return msg;
            }
            catch
            {
                
                try
                {
                    var asm = typeof(DiscordWebhookService).Assembly;
                    var t = asm.GetType("ShimmyMySherbet.DiscordWebhooks.Models.WebhookMessage", throwOnError: false);
                    if (t == null) return null;

                    var obj = Activator.CreateInstance(t);

                    SetPropIfExists(obj, "Username", string.IsNullOrWhiteSpace(username) ? "Anti-Spam" : username);
                    SetPropIfExists(obj, "Content", content);

                    return obj;
                }
                catch
                {
                    return null;
                }
            }
        }

        private void SetPropIfExists(object obj, string propName, object value)
        {
            var p = obj.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
            if (p != null && p.CanWrite)
                p.SetValue(obj, value, null);
        }
    }
}