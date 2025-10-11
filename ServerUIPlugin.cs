using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Server UI Plugin", "Author", "1.0.0")]
    [Description("Плагин интерфейса сервера с русской локализацией")]
    public class ServerUIPlugin : RustPlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            public string ServerName { get; set; } = "Bird Rust — Modded 2x (MAX3) [DUEL | SKIN | CASE | EVENT]";
            public int ResourceMultiplier { get; set; } = 2;
            public string TelegramUrl { get; set; } = "https://t.me/your_channel";
            public string DiscordUrl { get; set; } = "https://discord.gg/your_server";
            public string VkUrl { get; set; } = "https://vk.com/your_group";
            public string NextWipeDate { get; set; } = "02.10.25";
            public Dictionary<string, string> Localization { get; set; } = new Dictionary<string, string>
            {
                ["main"] = "ГЛАВНАЯ",
                ["info"] = "ИНФОРМАЦИЯ",
                ["kits"] = "НАБОРЫ",
                ["daily"] = "ЕЖЕДНЕВКИ",
                ["cases"] = "КЕЙСЫ",
                ["blocking"] = "БЛОКИРОВКА",
                ["pri"] = "ПРИ",
                ["statistics"] = "СТАТИСТИКА",
                ["basket"] = "КОРЗИНА",
                ["online"] = "ОНЛАЙН",
                ["connecting"] = "ПОДКЛЮЧАЕТСЯ",
                ["queue"] = "ОЧЕРЕДЬ",
                ["team"] = "команда",
                ["player"] = "игрока",
                ["players"] = "игроков",
                ["resource_rates"] = "рейты ресурсов",
                ["social_settings"] = "НАСТРОЙКИ СОЦ. СЕТЕЙ",
                ["connect"] = "ПРИВЯЗАТЬ",
                ["wipe_schedule"] = "РАСПИСАНИЕ ВАЙПОВ",
                ["next_wipe"] = "Следующий вайп",
                ["prev_wipe"] = "Предыдущий вайп",
                ["unknown"] = "Неизвестно",
                ["wipe_from_dev"] = "Обнова от разработчиков"
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    throw new JsonException();
                }
            }
            catch
            {
                PrintWarning("Configuration file is corrupt, creating new one!");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion

        #region UI Constants

        private const string MainUIName = "ServerUI_Main";
        private const string SidebarUIName = "ServerUI_Sidebar";
        private const string ContentUIName = "ServerUI_Content";

        #endregion

        #region Hooks

        void OnServerInitialized()
        {
            LoadConfig();
            SaveConfig();
        }

        void OnPlayerConnected(BasePlayer player)
        {
            timer.Once(1f, () => ShowMainUI(player));
        }

        #endregion

        #region Commands

        [ChatCommand("ui")]
        void CmdUI(BasePlayer player, string command, string[] args)
        {
            ShowMainUI(player);
        }

        [ChatCommand("closeui")]
        void CmdCloseUI(BasePlayer player, string command, string[] args)
        {
            DestroyUI(player);
        }

        #endregion

        #region UI Creation

        void ShowMainUI(BasePlayer player)
        {
            DestroyUI(player);
            CreateSidebar(player);
            CreateMainContent(player);
        }

        void CreateSidebar(BasePlayer player)
        {
            var container = new CuiElementContainer();

            // Основная панель сайдбара
            container.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 0.95" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0.25 1" },
                CursorEnabled = true
            }, "Overlay", SidebarUIName);

            // Заголовок GRANDPASS
            container.Add(new CuiLabel
            {
                Text = { Text = "GRANDPASS", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0 0.9", AnchorMax = "1 1" }
            }, SidebarUIName);

            // Подзаголовок с опытом
            container.Add(new CuiLabel
            {
                Text = { Text = "30 EXP из 100", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "0.8 0.8 0.8 1" },
                RectTransform = { AnchorMin = "0 0.85", AnchorMax = "1 0.9" }
            }, SidebarUIName);

            // Иконка ящика
            container.Add(new CuiPanel
            {
                Image = { Color = "0.8 0.6 0.2 1" },
                RectTransform = { AnchorMin = "0.35 0.75", AnchorMax = "0.65 0.85" }
            }, SidebarUIName);

            container.Add(new CuiLabel
            {
                Text = { Text = "📦", FontSize = 24, Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = "0.35 0.75", AnchorMax = "0.65 0.85" }
            }, SidebarUIName);

            container.Add(new CuiLabel
            {
                Text = { Text = "СКОРО", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.35 0.7", AnchorMax = "0.65 0.75" }
            }, SidebarUIName);

            // Меню навигации
            string[] menuItems = { "main", "info", "kits", "daily", "cases", "blocking", "pri", "statistics", "basket" };
            string[] menuIcons = { "🏠", "ℹ️", "🎒", "📅", "🎁", "🚫", "⚙️", "📊", "🛒" };

            for (int i = 0; i < menuItems.Length; i++)
            {
                float yMin = 0.6f - (i * 0.06f);
                float yMax = yMin + 0.05f;

                var button = new CuiButton
                {
                    Button = { 
                        Color = i == 0 ? "0.3 0.3 0.3 1" : "0.2 0.2 0.2 0.8",
                        Command = $"serverui.menu {menuItems[i]}"
                    },
                    RectTransform = { AnchorMin = $"0.05 {yMin}", AnchorMax = $"0.95 {yMax}" },
                    Text = { 
                        Text = $"{menuIcons[i]} {config.Localization[menuItems[i]]}", 
                        FontSize = 12, 
                        Align = TextAnchor.MiddleLeft,
                        Color = "1 1 1 1"
                    }
                };
                container.Add(button, SidebarUIName);
            }

            CuiHelper.AddUi(player, container);
        }

        void CreateMainContent(BasePlayer player)
        {
            var container = new CuiElementContainer();

            // Основная панель контента
            container.Add(new CuiPanel
            {
                Image = { Color = "0.15 0.15 0.15 0.95" },
                RectTransform = { AnchorMin = "0.25 0", AnchorMax = "1 1" },
                CursorEnabled = true
            }, "Overlay", ContentUIName);

            // Заголовок сервера
            container.Add(new CuiLabel
            {
                Text = { Text = config.ServerName, FontSize = 18, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.05 0.9", AnchorMax = "0.95 0.95" }
            }, ContentUIName);

            // Информация о команде и ресурсах
            container.Add(new CuiLabel
            {
                Text = { Text = $"👥 {config.Localization["team"]} - {GetTeamSize(player)} {config.Localization["player"]}", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "0.9 0.7 0.3 1" },
                RectTransform = { AnchorMin = "0.05 0.85", AnchorMax = "0.5 0.9" }
            }, ContentUIName);

            container.Add(new CuiLabel
            {
                Text = { Text = $"⚡ {config.Localization["resource_rates"]} - X{config.ResourceMultiplier}", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "0.9 0.7 0.3 1" },
                RectTransform = { AnchorMin = "0.05 0.8", AnchorMax = "0.5 0.85" }
            }, ContentUIName);

            // Статистика игроков
            CreatePlayerStats(container, ContentUIName);

            // Настройки соц. сетей
            CreateSocialSettings(container, ContentUIName);

            // Календарь вайпов
            CreateWipeCalendar(container, ContentUIName);

            CuiHelper.AddUi(player, container);
        }

        void CreatePlayerStats(CuiElementContainer container, string parent)
        {
            var onlinePlayers = BasePlayer.activePlayerList.Count;
            var connectingPlayers = BasePlayer.sleepingPlayerList.Count(p => p.IsConnected);
            var queueCount = 0; // Здесь можно добавить логику очереди

            container.Add(new CuiLabel
            {
                Text = { Text = $"🟢 {config.Localization["online"]} - {onlinePlayers}", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "0.3 0.8 0.3 1" },
                RectTransform = { AnchorMin = "0.05 0.7", AnchorMax = "0.3 0.75" }
            }, parent);

            container.Add(new CuiLabel
            {
                Text = { Text = $"🟡 {config.Localization["connecting"]} - {connectingPlayers}", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "0.8 0.8 0.3 1" },
                RectTransform = { AnchorMin = "0.05 0.65", AnchorMax = "0.3 0.7" }
            }, parent);

            container.Add(new CuiLabel
            {
                Text = { Text = $"🔴 {config.Localization["queue"]} - {queueCount}", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "0.8 0.3 0.3 1" },
                RectTransform = { AnchorMin = "0.05 0.6", AnchorMax = "0.3 0.65" }
            }, parent);
        }

        void CreateSocialSettings(CuiElementContainer container, string parent)
        {
            container.Add(new CuiLabel
            {
                Text = { Text = config.Localization["social_settings"], FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.35 0.75", AnchorMax = "0.65 0.8" }
            }, parent);

            // Telegram кнопка
            var telegramBtn = new CuiButton
            {
                Button = { Color = "0.2 0.6 0.9 0.8", Command = $"serverui.social telegram" },
                RectTransform = { AnchorMin = "0.35 0.65", AnchorMax = "0.45 0.72" },
                Text = { Text = "📱", FontSize = 20, Align = TextAnchor.MiddleCenter }
            };
            container.Add(telegramBtn, parent);

            container.Add(new CuiLabel
            {
                Text = { Text = config.Localization["connect"], FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "0.8 0.8 0.8 1" },
                RectTransform = { AnchorMin = "0.35 0.6", AnchorMax = "0.45 0.65" }
            }, parent);

            // Discord кнопка
            var discordBtn = new CuiButton
            {
                Button = { Color = "0.4 0.4 0.8 0.8", Command = $"serverui.social discord" },
                RectTransform = { AnchorMin = "0.47 0.65", AnchorMax = "0.57 0.72" },
                Text = { Text = "🎮", FontSize = 20, Align = TextAnchor.MiddleCenter }
            };
            container.Add(discordBtn, parent);

            container.Add(new CuiLabel
            {
                Text = { Text = config.Localization["connect"], FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "0.8 0.8 0.8 1" },
                RectTransform = { AnchorMin = "0.47 0.6", AnchorMax = "0.57 0.65" }
            }, parent);

            // VK кнопка
            var vkBtn = new CuiButton
            {
                Button = { Color = "0.3 0.5 0.8 0.8", Command = $"serverui.social vk" },
                RectTransform = { AnchorMin = "0.59 0.65", AnchorMax = "0.69 0.72" },
                Text = { Text = "🌐", FontSize = 20, Align = TextAnchor.MiddleCenter }
            };
            container.Add(vkBtn, parent);

            container.Add(new CuiLabel
            {
                Text = { Text = config.Localization["connect"], FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "0.8 0.8 0.8 1" },
                RectTransform = { AnchorMin = "0.59 0.6", AnchorMax = "0.69 0.65" }
            }, parent);
        }

        void CreateWipeCalendar(CuiElementContainer container, string parent)
        {
            container.Add(new CuiLabel
            {
                Text = { Text = config.Localization["wipe_schedule"], FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.7 0.75", AnchorMax = "0.95 0.8" }
            }, parent);

            // Информация о вайпах
            container.Add(new CuiLabel
            {
                Text = { Text = $"{config.Localization["next_wipe"]}", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "0.8 0.8 0.8 1" },
                RectTransform = { AnchorMin = "0.7 0.7", AnchorMax = "0.85 0.75" }
            }, parent);

            container.Add(new CuiLabel
            {
                Text = { Text = config.NextWipeDate, FontSize = 12, Align = TextAnchor.MiddleRight, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.85 0.7", AnchorMax = "0.95 0.75" }
            }, parent);

            container.Add(new CuiLabel
            {
                Text = { Text = $"{config.Localization["prev_wipe"]}", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "0.8 0.8 0.8 1" },
                RectTransform = { AnchorMin = "0.7 0.65", AnchorMax = "0.85 0.7" }
            }, parent);

            container.Add(new CuiLabel
            {
                Text = { Text = config.Localization["unknown"], FontSize = 12, Align = TextAnchor.MiddleRight, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.85 0.65", AnchorMax = "0.95 0.7" }
            }, parent);

            container.Add(new CuiLabel
            {
                Text = { Text = $"{config.Localization["wipe_from_dev"]}", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = "0.8 0.8 0.8 1" },
                RectTransform = { AnchorMin = "0.7 0.6", AnchorMax = "0.85 0.65" }
            }, parent);

            container.Add(new CuiLabel
            {
                Text = { Text = config.NextWipeDate, FontSize = 12, Align = TextAnchor.MiddleRight, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.85 0.6", AnchorMax = "0.95 0.65" }
            }, parent);

            // Простой календарь
            CreateSimpleCalendar(container, parent);
        }

        void CreateSimpleCalendar(CuiElementContainer container, string parent)
        {
            container.Add(new CuiLabel
            {
                Text = { Text = "СЕНТЯБРЬ 2025", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.7 0.5", AnchorMax = "0.95 0.55" }
            }, parent);

            // Дни недели
            string[] weekDays = { "ПН", "ВТ", "СР", "ЧТ", "ПТ", "СБ", "ВС" };
            for (int i = 0; i < weekDays.Length; i++)
            {
                float xMin = 0.7f + (i * 0.035f);
                float xMax = xMin + 0.03f;
                
                container.Add(new CuiLabel
                {
                    Text = { Text = weekDays[i], FontSize = 8, Align = TextAnchor.MiddleCenter, Color = "0.8 0.8 0.8 1" },
                    RectTransform = { AnchorMin = $"{xMin} 0.45", AnchorMax = $"{xMax} 0.5" }
                }, parent);
            }

            // Дни месяца (упрощенная версия)
            int currentDay = DateTime.Now.Day;
            for (int week = 0; week < 5; week++)
            {
                for (int day = 0; day < 7; day++)
                {
                    int dayNumber = week * 7 + day + 1;
                    if (dayNumber > 30) break;

                    float xMin = 0.7f + (day * 0.035f);
                    float xMax = xMin + 0.03f;
                    float yMin = 0.4f - (week * 0.04f);
                    float yMax = yMin + 0.035f;

                    string color = dayNumber == currentDay ? "0.8 0.3 0.3 1" : "0.6 0.6 0.6 1";
                    
                    container.Add(new CuiLabel
                    {
                        Text = { Text = dayNumber.ToString(), FontSize = 8, Align = TextAnchor.MiddleCenter, Color = color },
                        RectTransform = { AnchorMin = $"{xMin} {yMin}", AnchorMax = $"{xMax} {yMax}" }
                    }, parent);
                }
            }
        }

        #endregion

        #region Console Commands

        [ConsoleCommand("serverui.menu")]
        void CmdMenu(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            string menuType = arg.GetString(0, "main");
            
            // Здесь можно добавить логику для разных разделов меню
            SendReply(player, $"Выбран раздел: {config.Localization.GetValueOrDefault(menuType, menuType)}");
        }

        [ConsoleCommand("serverui.social")]
        void CmdSocial(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            string platform = arg.GetString(0, "");
            string url = "";

            switch (platform)
            {
                case "telegram":
                    url = config.TelegramUrl;
                    break;
                case "discord":
                    url = config.DiscordUrl;
                    break;
                case "vk":
                    url = config.VkUrl;
                    break;
            }

            if (!string.IsNullOrEmpty(url))
            {
                SendReply(player, $"Ссылка на {platform}: {url}");
                // Здесь можно добавить логику для открытия ссылки в браузере игрока
            }
        }

        #endregion

        #region Helper Methods

        void DestroyUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, MainUIName);
            CuiHelper.DestroyUi(player, SidebarUIName);
            CuiHelper.DestroyUi(player, ContentUIName);
        }

        int GetTeamSize(BasePlayer player)
        {
            if (player.currentTeam == 0) return 1;
            var team = RelationshipManager.ServerInstance.FindTeam(player.currentTeam);
            return team?.members?.Count ?? 1;
        }

        #endregion

        #region Unload

        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyUI(player);
            }
        }

        #endregion
    }
}