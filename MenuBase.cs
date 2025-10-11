using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using System.IO;
using UnityEngine.UI;

namespace Oxide.Plugins
{
	[Info("MenuBase", "pluginfuel.ru", "1.0.5")]
	class MenuBase : RustPlugin
	{
		#region Classes

		internal class Button
		{
			[JsonProperty("Ключ перевода из lang-файла")]
			public string LangKey;
			[JsonProperty("Команда (консольная)")]
			public string Command;
		}
		internal class Section
		{
			[JsonProperty("Позиция")] public int Order;
			[JsonProperty("Ключ перевода из lang-файла")]
			public string LangKey;

			[JsonProperty("Команда для открытия раздела")]
			public string Command;

			[JsonProperty("Нужно рисовать background этим плагином?")]
			public bool NeedDrawBG = true;
		}
		#endregion

		#region Fields

		[PluginReference] private Plugin ImageLibrary, IQEconomic, Volts;

		private readonly Dictionary<ulong, int> onlinePlayersPageByViewer = new();
		private const int ONLINE_PAGE_SIZE = 8;

		private const string Layer = "ui.MenuBase.bg";
		private const string Layer_BLUR = "ui.MenuBase.bg.blur";

		private const string GRADIENT_RIGHT = "assets/content/ui/ui.background.transparent.linearltr.tga";
		private const string GRADIENTDOWN_COLOR = "0 0 0 0.7";
		
		private const string WHITE_TRANSPARENT_BACKGROUND = "1 1 1 0.3";
		private const string ORANGE_COLOR = "0.9490196 0.5019608 0.05490196 1";
		private const string BACKGROUND_COLOR = "0.3568628 0.3568628 0.3568628 0.75";

		private const string TEXT_COLOR = "1 1 1 1";

		private const string RED_COLOR = "0.6901961 0.3490196 0.3490196 0.8";
		
		#endregion

		#region Hooks

		private void OnServerInitialized()
		{
			var images = new List<string>();

			foreach (var x in cfg.BaseSettings.Sections)
			{
				images.Add($"Icon_{x.Key}");
			}
			foreach (var x in cfg.MainSettings.Banners)
				images.Add(x.Value);
			images.Add("volts");
			images.Add("coins");
			images.Add("banner_shop");
			images.Add("volts_shop_btn");
			
			GuiManager.LoadImages(images);
		}

		private void Unload()
		{
			GuiManager.Clear();
			foreach (var x in BasePlayer.activePlayerList)
				CuiHelper.DestroyUi(x, Layer_BLUR);
			onlinePlayersPageByViewer.Clear();
		}
		#endregion

		#region Methods

		private DateTime GetLastWipeDate()
		{
			return SaveRestore.SaveCreatedTime;
		}

		private DateTime GetNextWipeDate()
		{
			return GetLastWipeDate() + TimeSpan.FromDays(7);
		}

		private string GetMonthName(int month)
		{
			return month switch
			{
				1 => "ЯНВАРЯ",
				2 => "ФЕВРАЛЯ",
				3 => "МАРТА",
				4 => "АПРЕЛЯ",
				5 => "МАЯ",
				6 => "ИЮНЯ",
				7 => "ИЮЛЯ",
				8 => "АВГУСТА",
				9 => "СЕНТЯБРЯ",
				10 => "ОКТЯБРЯ",
				11 => "НОЯБРЯ",
				12 => "ДЕКАБРЯ",
				_ => "UNKNOWN"
			};
		}

		private string GetValidLastWipeData()
		{
			var dateTime = GetLastWipeDate();

			return $"{dateTime.Day} {GetMonthName(dateTime.Month)}";
		}

		private string GetValidNextWipeData()
		{
			var dateTime = GetNextWipeDate();

			return $"{dateTime.Day} {GetMonthName(dateTime.Month)}";
		}
		
		private int GetBalanceVolts(BasePlayer player)
		{
			return (int)IQEconomic.Call("API_GET_BALANCE", player.UserIDString);
		}

		private int GetBalanceCoins(BasePlayer player)
		{
			if (!IQEconomic)
				return -1;
			return (int)IQEconomic.Call("API_GET_BALANCE", player.UserIDString);
		}
		private static class GuiManager
		{
			public static void Clear()
			{
				iconImageInfos.Clear();
				FailedLoad.Clear();
			}

			public static string Get(string key)
			{
				if (iconImageInfos.TryGetValue(key, out var id))
					return id.ToString();
				return "";
			}

			private static Dictionary<string, uint> iconImageInfos = new();

			private static List<string> FailedLoad = new();

			internal static void LoadImages(List<string> imageFiles)
			{
				foreach (var x in imageFiles)
				{
					if (iconImageInfos.ContainsKey(x))
						continue;
					iconImageInfos.Add(x, 0);
				}

				ServerMgr.Instance.StartCoroutine(LoadIconsCoroutine());
			}

			private static IEnumerator LoadIconsCoroutine()
			{
				for (int i = 0; i < iconImageInfos.Count; i++)
				{
					var imageInfo = iconImageInfos.ElementAtOrDefault(i);
					string url = "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar +
					             "/MenuBase/Images/" +
					             imageInfo.Key + ".png";

					using (WWW www = new WWW(url))
					{
						yield return www;

						if (www.error != null)
						{
							FailedLoad.Add(imageInfo.Key);
						}
						else
						{
							var texture = www.texture;
							var imageId = FileStorage.server.Store(texture.EncodeToPNG(), FileStorage.Type.png,
								CommunityEntity.ServerInstance.net.ID);
							iconImageInfos[imageInfo.Key] = imageId;
							GameObject.DestroyImmediate(texture);
						}
					}
				}

				if (FailedLoad.IsNullOrEmpty())
					yield break;
				
				Debug.LogError($"\n\nFailed for loading {FailedLoad.Count} images in plugin MenuBase");
				Debug.LogError("__________________________________");
				for (int i = 0; i < FailedLoad.Count; i++)
					Debug.LogWarning($"[{i + 1}]" + $"{FailedLoad[i]}".PadLeft(31 - (i + 1 >= 10 ? 1 : 0), ' '));
				Debug.LogError("__________________________________\n\n");
					
			}
		}
		#endregion

		private void OnPlayerConnected(BasePlayer player)
		{
			// Refresh online list only for viewers who have it open
			foreach (var kvp in onlinePlayersPageByViewer.ToList())
			{
				var viewer = BasePlayer.FindByID(kvp.Key);
				if (viewer == null) continue;
				UI_DrawOnlinePlayers(viewer, kvp.Value);
			}
		}

		private void OnPlayerDisconnected(BasePlayer player, string reason)
		{
			// Clean up stored page and refresh viewers who have the list open
			onlinePlayersPageByViewer.Remove(player.userID);
			foreach (var kvp in onlinePlayersPageByViewer.ToList())
			{
				var viewer = BasePlayer.FindByID(kvp.Key);
				if (viewer == null) continue;
				UI_DrawOnlinePlayers(viewer, kvp.Value);
			}
		}

		#region UI

		#region BaseUI
		private void UI_DrawMain(BasePlayer player)
		{
			CuiHelper.DestroyUi(player, Layer_BLUR);
			var container = new CuiElementContainer();

			container.Add(new CuiPanel()
			{
				Image = { Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat", Color = "0.169 0.162 0.143 0.4", ImageType = Image.Type.Tiled },
				RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
			}, "OverlayNonScaled", Layer_BLUR);
			container.Add(new CuiPanel()
			{
				Image = { Sprite = "assets/content/ui/ui.background.transparent.radial.psd", Color = "0.21 0.21 0.21 1.00"},
				RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
			}, Layer_BLUR, Layer_BLUR + ".radial");
			
			container.Add(new CuiPanel
			{
				CursorEnabled = true,
				Image = { Color = "1 1 1 0" },
				RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-388.376 -229.226", OffsetMax = "410.776 229.243" }
			}, Layer_BLUR, Layer, Layer);
			
			container.Add(new CuiPanel
			{
				CursorEnabled = false,
				Image = { Color = BACKGROUND_COLOR },
				RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-399.575 -229.235", OffsetMax = "-234.292 229.235" }
			}, Layer, Layer + ".sections.div");
			
			CuiHelper.AddUi(player, container);

			var activeSection = "";

			if (cfg.BaseSettings.Sections.ContainsKey(cfg.BaseSettings.DefaultSection))
				activeSection = cfg.BaseSettings.DefaultSection;
			
			UI_DrawMainDiv(player, cfg.BaseSettings.Sections[activeSection].NeedDrawBG);
			
			UI_DrawSections(player, activeSection);
			
			if (!string.IsNullOrEmpty(activeSection))
				player.SendConsoleCommand(cfg.BaseSettings.Sections[activeSection].Command);
		}

		private void UI_DrawMainDiv(BasePlayer player, bool needDrawBG)
		{
			var container = new CuiElementContainer();
			container.Add(new CuiPanel
			{
				CursorEnabled = false,
				Image = { Color = needDrawBG ? BACKGROUND_COLOR : "0 0 0 0" },
				RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-229.802 -229.232", OffsetMax = "399.578 229.228" }
			}, Layer, Layer + ".main", Layer + ".main");
			container.Add(new CuiPanel
			{
				CursorEnabled = false,
				Image = { Color = "0 0 0 0" },
				RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-229.801 -229.232", OffsetMax = "399.579 229.228" }
			}, Layer, Layer + ".main.div", Layer + ".main.div");
			if (needDrawBG)
				container.Add(new CuiButton
				{
					Button = { Color = RED_COLOR, Close = Layer_BLUR },
					Text = { Text = "X", Font = "permanentmarker.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = TEXT_COLOR },
					RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "285.139 199.674", OffsetMax = "314.69 229.226" }
				}, Layer + ".main.div", Layer + ".main.div" + ".close");
			CuiHelper.AddUi(player, container);
		}
		
		private void UI_DrawSections(BasePlayer player, string activeSection)
		{
			var container = new CuiElementContainer();

			container.Add(new CuiPanel
			{
				CursorEnabled = false,
				Image = { Color = "1 1 1 0" },
				RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-82.64 -229.233", OffsetMax = "82.64 229.237" },
			}, Layer + ".sections.div", Layer + ".sections.div" + ".items", Layer + ".sections.div" + ".items");

			float minxNonActive = -82.64f;
			float maxxNonActive = 82.64f; 
			float miny = 193.244f;
			float maxy = 229.2341f;

			float minxActive = -109.0284f;
			float maxxActive = 80.63995f;


			string imagePosNonActiveMin = "-81.792 -9.342";
			string imagePosNonActiveMax = "-58.108 9.342";
			
			
			string imagePosActiveMin = "-86.792 -9.342";
			string imagePosActiveMax = "-63.108 9.342";

			float TextNonActiveOffset = 20f;
			
			foreach (var x in cfg.BaseSettings.Sections.OrderBy(x => x.Value.Order))
			{
				bool isActive = x.Key == activeSection;

				container.Add(new CuiElement()
				{
					Name = Layer + ".sections.div" + ".items" + $".{x.Key}" + ".bg",
					Parent = Layer + ".sections.div" + ".items",
					Components =
					{
						new CuiImageComponent() { Color = isActive ? GRADIENTDOWN_COLOR : "0 0 0 0" },
						new CuiRectTransformComponent() { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{(isActive ? minxActive : minxNonActive)} {miny}", OffsetMax =  $"{(isActive ? maxxActive : maxxNonActive)} {maxy}" }
					}
				});
				container.Add(new CuiButton
				{
					Button = { Color = "0 0 0 0", Command = isActive ? "" : $"mb.section {x.Key}" },
					RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
				}, Layer + ".sections.div" + ".items" + $".{x.Key}" + ".bg", Layer + ".sections.div" + ".items" + $".{x.Key}");
				
				container.Add(new CuiPanel
				{
					Image = { Color = isActive ? ORANGE_COLOR : "1 1 1 0" },
					RectTransform =
					{
						AnchorMin = "0 0",
						AnchorMax = "1 1",
						OffsetMin = "0 3",
						OffsetMax = "0 0"
					}
				}, Layer + ".sections.div" + ".items" + $".{x.Key}", Layer + ".sections.div" + ".items" + $".{x.Key}" + ".background");


				container.Add(new CuiElement()
				{
					Parent = Layer + ".sections.div" + ".items" + $".{x.Key}" + ".background",
					Components =
					{
						new CuiRawImageComponent() { Png = GuiManager.Get($"Icon_{x.Key}") },
						new CuiRectTransformComponent() { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = isActive ? imagePosActiveMin : imagePosNonActiveMin, OffsetMax = isActive ? imagePosActiveMax : imagePosNonActiveMax }
					}
				});

				container.Add(new CuiElement
				{
					Parent = Layer + ".sections.div" + ".items" + $".{x.Key}" + ".background",
					Components = {
						new CuiTextComponent { Text = GetMsg(x.Value.LangKey, player), Font = "robotocondensed-regular.ttf", FontSize = 12, Align = isActive ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft, Color = TEXT_COLOR },
						new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{(-73.317 + (!isActive ? TextNonActiveOffset : 0))} -16.182", OffsetMax = "94.834 16.182" }
					}
				});

				
				miny -= 35.989f;
				maxy -= 35.989f;
			}
			CuiHelper.AddUi(player, container);
		}
		#endregion

		#region Main page

		private void UI_DrawMainPage(BasePlayer player)
		{
			var container = new CuiElementContainer();
			
			container.Add(new CuiPanel
			{
				CursorEnabled = false,
				Image = { Color = "1 1 1 0" },
				RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-309.405 130.694", OffsetMax = "61.589 222.516" }
			}, Layer + ".main.div", Layer + ".main.div" + ".player.div");

			container.Add(new CuiPanel
			{
				CursorEnabled = false,
				Image = { Color = "1 1 1 0.3", Sprite = GRADIENT_RIGHT },
				RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-185.502 -40.583", OffsetMax = "185.498 44.005" }
			}, Layer + ".main.div" + ".player.div", Layer + ".main.div" + ".player.div" + ".gradient.sprite");

			container.Add(new CuiElement()
			{
				Name = Layer + ".main.div" + ".player.div" + ".avatar.div",
				Parent = Layer + ".main.div" + ".player.div",
				Components =
				{
					new CuiRawImageComponent() { Color = GRADIENTDOWN_COLOR },
					new CuiRectTransformComponent() { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-185.5 -45.911", OffsetMax = "-98.004 45.911" }
				}
			});


			container.Add(new CuiElement()
			{
				Parent = Layer + ".main.div" + ".player.div" + ".avatar.div",
				Components =
				{
					new CuiImageComponent() { Color = ORANGE_COLOR },
					new CuiOutlineComponent()
					{
						Distance	= "2 2", Color = ORANGE_COLOR
					},
					new CuiRectTransformComponent()
					{
						AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-41.901 -41.422",
						OffsetMax = "42.171 44.182"
					}
				}
			});
			container.Add(new CuiElement()
			{
				Parent = Layer + ".main.div" + ".player.div" + ".avatar.div",
				Components =
				{
					new CuiRawImageComponent() { SteamId = player.UserIDString },
					new CuiRectTransformComponent() { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-41.901 -41.422", OffsetMax = "42.171 44.182" }
				}
			});

			container.Add(new CuiElement
			{
				Parent = Layer + ".main.div" + ".player.div",
				Components = {
								new CuiTextComponent { Text = player.displayName, Font = "robotocondensed-bold.ttf", FontSize = 19, Align = TextAnchor.UpperLeft, Color = TEXT_COLOR },
								new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-92.4 18.375", OffsetMax = "187.4 44.005" }
							}
			});

			container.Add(new CuiPanel
			{
				CursorEnabled = false,
				Image = { Color = "1 1 1 0" },
				RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-90.2 -31.717", OffsetMax = "27.637 14.6" }
			}, Layer + ".main.div" + ".player.div", Layer + ".main.div" + ".player.div" + ".balance.div");

			container.Add(new CuiPanel
			{
				CursorEnabled = false,
				Image = { Color = "1 1 1 0" },
				RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-58.92 0", OffsetMax = "58.92 23.159" }
			}, Layer + ".main.div" + ".player.div" + ".balance.div", Layer + ".main.div" + ".player.div" + ".balance.div" + ".coins.div");
			
			// container.Add(new CuiElement()
			// {
			// 	Parent = Layer + ".main.div" + ".player.div" + ".balance.div" + ".coins.div",
			// 	Components =
			// 	{
			// 		new CuiRawImageComponent() { Png = GuiManager.Get("coins") },
			// 		new CuiRectTransformComponent()
			// 		{
			// 			AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-58.243 -8.443", OffsetMax = "-38.957 10.843"
			// 		}
			// 	}
			// });
			// ❍
			container.Add(new CuiElement()
			{
				Parent = Layer + ".main.div" + ".player.div" + ".balance.div" + ".coins.div",
				Components =
				{
					new CuiTextComponent() { Text = "\u274d", FontSize = 14, Align = TextAnchor.LowerCenter },
					new CuiRectTransformComponent() { 	AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-58.243 -9.443", OffsetMax = "-38.957 9.843" }
				}
			});
			container.Add(new CuiElement
			{
				Parent = Layer + ".main.div" + ".player.div" + ".balance.div" + ".coins.div",
				Components = {
								new CuiTextComponent { Text = GetBalanceCoins(player).ToString(), Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
								new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-33.132 -12.729", OffsetMax = "58.92 9.129" }
							}
			});

			container.Add(new CuiPanel
			{
				CursorEnabled = false,
				Image = { Color = "1 1 1 0" },
				RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-58.918 -23.16", OffsetMax = "58.922 0" }
			}, Layer + ".main.div" + ".player.div" + ".balance.div", Layer + ".main.div" + ".player.div" + ".balance.div" + ".volts.div");

			container.Add(new CuiElement()
			{
				Parent = Layer + ".main.div" + ".player.div" + ".balance.div" + ".volts.div",
				Components =
				{
					new CuiTextComponent() { Text = "<color=orange><size=14><b>ϟ</b></size></color>", FontSize = 10, Align = TextAnchor.MiddleCenter},
					new CuiRectTransformComponent() { 	AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-58.243 -10.643",
									OffsetMax = "-38.957 8.643" }
				}
			});
			
			// container.Add(new CuiElement()
			// {
			// 	Parent = Layer + ".main.div" + ".player.div" + ".balance.div" + ".volts.div",
			// 	Components =
			// 	{
			// 		new CuiRawImageComponent() { Png = GuiManager.Get("volts") },
			// 		new CuiRectTransformComponent()
			// 		{
			// 			AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-58.243 -9.643",
			// 			OffsetMax = "-38.957 9.643"
			// 		}
			// 	}
			// });

			container.Add(new CuiElement
			{
				Parent = Layer + ".main.div" + ".player.div" + ".balance.div" + ".volts.div",
				Components = {
								new CuiTextComponent { Text = GetBalanceVolts(player).ToString(), Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleLeft, Color = "1 1 1 1" },
								new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-33.132 -12.729", OffsetMax = "58.92 9.129" }
							}
			});
			
			
			container.Add(new CuiPanel
			{
				CursorEnabled = false,
				Image = { Color = "1 1 1 0" },
				RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-272.49 8.31", OffsetMax = "270.01 105.097" }
			}, Layer + ".main.div", Layer + ".main.div" + ".serverinfo.div");

			container.Add(new CuiPanel
			{
				CursorEnabled = false,
				Image = { Color = WHITE_TRANSPARENT_BACKGROUND },
				RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-271.25 11.264", OffsetMax = "271.25 27.129" }
			}, Layer + ".main.div" + ".serverinfo.div", Layer + ".main.div" + ".serverinfo.div" + ".online.bg");

			container.Add(new CuiPanel
			{
				CursorEnabled = false,
				Image = { Color = "0.345098 0.7843138 0.345098 1" },
				RectTransform = { AnchorMin = "0 0", AnchorMax = $"{BasePlayer.activePlayerList.Count / (float)ConVar.Server.maxplayers} 1", OffsetMin = "0 0", OffsetMax = "0 0" }
			}, Layer + ".main.div" + ".serverinfo.div" + ".online.bg");

			container.Add(new CuiElement
			{
				Parent = Layer + ".main.div" + ".serverinfo.div" + ".online.bg",
				Components = {
								new CuiTextComponent { Text = $"ОНЛАЙН: {BasePlayer.activePlayerList.Count}/{ConVar.Server.maxplayers}", Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.UpperLeft, Color = TEXT_COLOR },
								new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-271.246 9.013", OffsetMax = "-27.834 30.387" }
							}
			});
			
			container.Add(new CuiElement
			{
				Parent = Layer + ".main.div" + ".serverinfo.div" + ".online.bg",
				Components = {
								new CuiTextComponent { Text = ConVar.Server.hostname.ToUpper(), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.UpperRight, Color = TEXT_COLOR },
								new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-127.834 9.013", OffsetMax = "271.246 30.387" }
							}
			});

			container.Add(new CuiPanel
			{
				CursorEnabled = false,
				Image = { Color = "1 1 1 0" },
				RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-271.25 -48.394", OffsetMax = "271.25 6.5" }
			}, Layer + ".main.div" + ".serverinfo.div", Layer + ".main.div" + ".serverinfo.div" + ".wipe.div");

			container.Add(new CuiPanel
			{
				CursorEnabled = false,
				Image = { Color = "1 1 1 0" },
				RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-271.25 1.557", OffsetMax = "271.25 27.447" }
			}, Layer + ".main.div" + ".serverinfo.div" + ".wipe.div", Layer + ".main.div" + ".serverinfo.div" + ".wipe.div" + ".current.div");

			container.Add(new CuiPanel
			{
				CursorEnabled = false,
				Image = { Color = WHITE_TRANSPARENT_BACKGROUND },
				RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-271.25 -12.946", OffsetMax = "68.097 12.945" }
			}, Layer + ".main.div" + ".serverinfo.div" + ".wipe.div" + ".current.div", Layer + ".main.div" + ".serverinfo.div" + ".wipe.div" + ".current.div" + ".text.bg");

			container.Add(new CuiElement
			{
				Parent = Layer + ".main.div" + ".serverinfo.div" + ".wipe.div" + ".current.div" + ".text.bg",
				Components = {
								new CuiTextComponent { Text = "ВАЙП БЫЛ:", Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = TEXT_COLOR },
								new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-169.672 -12.946", OffsetMax = "169.668 12.945" }
							}
			});

			container.Add(new CuiPanel
			{
				CursorEnabled = false,
				Image = { Color = "0.3568628 0.3568628 0.3568628 0.8" },
				RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "73.986 -12.946", OffsetMax = "271.254 12.945" }
			}, Layer + ".main.div" + ".serverinfo.div" + ".wipe.div" + ".current.div", Layer + ".main.div" + ".serverinfo.div" + ".wipe.div" + ".current.div" + ".date.bg");

			container.Add(new CuiElement
			{
				Parent = Layer + ".main.div" + ".serverinfo.div" + ".wipe.div" + ".current.div" + ".date.bg",
				Components = {
								new CuiTextComponent { Text = GetValidLastWipeData(), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = TEXT_COLOR },
								new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-98.635 -12.946", OffsetMax = "98.635 12.945" }
							}
			});

			container.Add(new CuiPanel
			{
				CursorEnabled = false,
				Image = { Color = "1 1 1 0" },
				RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-271.25 -27.447", OffsetMax = "271.25 -1.557" }
			}, Layer + ".main.div" + ".serverinfo.div" + ".wipe.div", Layer + ".main.div" + ".serverinfo.div" + ".wipe.div" + ".next.div");

			container.Add(new CuiPanel
			{
				CursorEnabled = false,
				Image = { Color = WHITE_TRANSPARENT_BACKGROUND },
				RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-271.25 -12.946", OffsetMax = "68.097 12.945" }
			}, Layer + ".main.div" + ".serverinfo.div" + ".wipe.div" + ".next.div", Layer + ".main.div" + ".serverinfo.div" + ".wipe.div" + ".next.div" + ".text.bg");

			container.Add(new CuiElement
			{
				Parent = Layer + ".main.div" + ".serverinfo.div" + ".wipe.div" + ".next.div" + ".text.bg",
				Components = {
								new CuiTextComponent { Text = "СЛЕДУЮЩИЙ ВАЙП:", Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = TEXT_COLOR },
								new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-169.672 -12.946", OffsetMax = "169.668 12.945" }
							}
			});

			container.Add(new CuiPanel
			{
				CursorEnabled = false,
				Image = { Color = "0.3568628 0.3568628 0.3568628 0.8" },
				RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "73.986 -12.946", OffsetMax = "271.254 12.945" }
			}, Layer + ".main.div" + ".serverinfo.div" + ".wipe.div" + ".next.div", Layer + ".main.div" + ".serverinfo.div" + ".wipe.div" + ".next.div" + ".date.bg");

			container.Add(new CuiElement
			{
				Parent = Layer + ".main.div" + ".serverinfo.div" + ".wipe.div" + ".next.div" + ".date.bg",
				Components = {
								new CuiTextComponent { Text = GetValidNextWipeData(), Font = "robotocondensed-bold.ttf", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = TEXT_COLOR },
								new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-98.635 -12.946", OffsetMax = "98.635 12.945" }
							}
			});

			
			CuiHelper.AddUi(player, container);
			
			UI_DrawOnlinePlayers(player, 0);
			UI_DrawButtons(player);
			
			var banner = cfg.MainSettings.Banners.First();
			UI_DrawBanner(player, banner.Key, true);
			UI_DrawBannerPages(player, banner.Key);
		}

		private void UI_DrawButtons(BasePlayer player)
		{
			var container = new CuiElementContainer();
			container.Add(new CuiPanel
			{
				CursorEnabled = false,
				Image = { Color = "1 1 1 0" },
				RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-223.67 -62.21", OffsetMax = "233.024 -29.937" }
			}, Layer + ".main.div", Layer + ".main.div" + ".buttons.div");

			float minx = -228.35f;
			float maxx = -84.743f;  
			float miny = -16.13659f;
			float maxy = 16.13641f;

			foreach (var x in cfg.MainSettings.Buttons)
			{
				container.Add(new CuiButton
				{
					Button = { Color = WHITE_TRANSPARENT_BACKGROUND, Command = $"mb.info.openapi {x.Value.Command}" },
					Text = { Text = GetMsg(x.Value.LangKey, player), Font = "robotocondensed-bold.ttf", FontSize = 13, Align = TextAnchor.MiddleCenter, Color = TEXT_COLOR },
					RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = $"{minx} {miny}", OffsetMax = $"{maxx} {maxy}" }
				}, Layer + ".main.div" + ".buttons.div");
				
				minx += 156.547f;
				maxx += 156.547f;
			}

			CuiHelper.AddUi(player, container);
		}

		private void UI_DrawBanner(BasePlayer player, int bannerID, bool first = false)
		{
			var container = new CuiElementContainer();
			if (first)
			{
				container.Add(new CuiPanel
				{
					CursorEnabled = false,
					Image = { Color = "1 1 1 0" },
					RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-308.7 -229.225", OffsetMax = "308.747 -72.995" }
				}, Layer + ".main.div", Layer + ".main.div" + ".banners.div");
			}

			container.Add(new CuiElement()
			{
				Name = Layer + ".main.div" + ".banners.div" + ".banner",
				Parent = Layer + ".main.div" + ".banners.div",
				DestroyUi = Layer + ".main.div" + ".banners.div" + ".banner",
				Components =
				{
					new CuiRawImageComponent() { Png = GuiManager.Get(cfg.MainSettings.Banners[bannerID]) },
					new CuiRectTransformComponent() { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-279.072 -78.115", OffsetMax = "279.078 78.115" }
				}
			});

			CuiHelper.AddUi(player, container);
		}

		private void UI_DrawBannerPages(BasePlayer player, int bannerID)
		{
			var container = new CuiElementContainer();
			container.Add(new CuiButton
			{
				Button = { Color = WHITE_TRANSPARENT_BACKGROUND, Command = $"mb.bannerpage {bannerID - 1}" },
				Text = { Text = "<", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = TEXT_COLOR },
				RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-308.72 -78.115", OffsetMax = "-279.071 78.115" }
			}, Layer + ".main.div" + ".banners.div", Layer + ".main.div" + ".banners.div" + ".previousbutton.div", Layer + ".main.div" + ".banners.div" + ".previousbutton.div");

			container.Add(new CuiButton
			{
				Button = { Color = WHITE_TRANSPARENT_BACKGROUND, Command = $"mb.bannerpage {bannerID + 1}" },
				Text = { Text = ">", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = TEXT_COLOR },
				RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "279.075 -78.115", OffsetMax = "308.725 78.115" }
			}, Layer + ".main.div" + ".banners.div", Layer + ".main.div" + ".banners.div" + ".nextbutton.div", Layer + ".main.div" + ".banners.div" + ".nextbutton.div");
			CuiHelper.AddUi(player, container);
		}

		private void UI_DrawOnlinePlayers(BasePlayer player, int page)
		{
			if (page < 0) page = 0;
			var all = BasePlayer.activePlayerList
				.OrderBy(p => p.displayName, StringComparer.OrdinalIgnoreCase)
				.ToList();
			int total = all.Count;
			int maxPage = Mathf.Max(0, Mathf.CeilToInt(total / (float)ONLINE_PAGE_SIZE) - 1);
			if (page > maxPage) page = maxPage;
			onlinePlayersPageByViewer[player.userID] = page;

			int start = page * ONLINE_PAGE_SIZE;
			var slice = all.Skip(start).Take(ONLINE_PAGE_SIZE).ToList();

			var container = new CuiElementContainer();
			// Root container for list
			container.Add(new CuiPanel
			{
				CursorEnabled = false,
				Image = { Color = "1 1 1 0" },
				RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "79 -229", OffsetMax = "399 229" }
			}, Layer + ".main.div", Layer + ".main.div" + ".online.div", Layer + ".main.div" + ".online.div");

			// Header
			container.Add(new CuiPanel
			{
				CursorEnabled = false,
				Image = { Color = WHITE_TRANSPARENT_BACKGROUND },
				RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 189", OffsetMax = "0 213" }
			}, Layer + ".main.div" + ".online.div", Layer + ".main.div" + ".online.div" + ".header.bg");

			container.Add(new CuiElement
			{
				Parent = Layer + ".main.div" + ".online.div" + ".header.bg",
				Components = {
					new CuiTextComponent { Text = $"ОНЛАЙН-ИГРОКИ ({total})", Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = TEXT_COLOR },
					new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
				}
			});

			// List entries area
			float rowHeight = 24f;
			float startY = 160f;
			for (int i = 0; i < slice.Count; i++)
			{
				var p = slice[i];
				float yMin = startY - i * (rowHeight + 2f) - rowHeight;
				float yMax = startY - i * (rowHeight + 2f);

				container.Add(new CuiPanel
				{
					CursorEnabled = false,
					Image = { Color = i % 2 == 0 ? "1 1 1 0.06" : "1 1 1 0.03" },
					RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = $"0 {yMin}", OffsetMax = $"0 {yMax}" }
				}, Layer + ".main.div" + ".online.div", Layer + ".main.div" + ".online.div" + $".row.{i}");

				// Avatar
				container.Add(new CuiElement
				{
					Parent = Layer + ".main.div" + ".online.div" + $".row.{i}",
					Components = {
						new CuiRawImageComponent { SteamId = p.UserIDString },
						new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "0 1", OffsetMin = "6 2", OffsetMax = "34 -2" }
					}
				});

				// Name
				container.Add(new CuiElement
				{
					Parent = Layer + ".main.div" + ".online.div" + $".row.{i}",
					Components = {
						new CuiTextComponent { Text = p.displayName, Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.MiddleLeft, Color = TEXT_COLOR },
						new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "40 4", OffsetMax = "-10 -4" }
					}
				});
			}

			// Pagination controls
			container.Add(new CuiButton
			{
				Button = { Color = WHITE_TRANSPARENT_BACKGROUND, Command = $"mb.online.page {page - 1}" },
				Text = { Text = "<", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = TEXT_COLOR },
				RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 6", OffsetMax = "-350 30" }
			}, Layer + ".main.div" + ".online.div");

			container.Add(new CuiElement
			{
				Parent = Layer + ".main.div" + ".online.div",
				Components = {
					new CuiTextComponent { Text = $"Стр. {page + 1}/{(maxPage + 1)}", Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.LowerCenter, Color = TEXT_COLOR },
					new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 0", OffsetMin = "0 6", OffsetMax = "0 30" }
				}
			});

			container.Add(new CuiButton
			{
				Button = { Color = WHITE_TRANSPARENT_BACKGROUND, Command = $"mb.online.page {page + 1}" },
				Text = { Text = ">", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = TEXT_COLOR },
				RectTransform = { AnchorMin = "1 0", AnchorMax = "1 0", OffsetMin = "-350 6", OffsetMax = "0 30" }
			}, Layer + ".main.div" + ".online.div");

			CuiHelper.AddUi(player, container);
		}
		#endregion

		#endregion

		#region Commands

		[ConsoleCommand("mb.info.openapi")]
		private void cmdOpenAPI(ConsoleSystem.Arg arg)
		{
			if (arg.Player() == null || arg.Args.IsNullOrEmpty())
				return;

			string command = arg.Args[0];

			arg.Player().SendConsoleCommand("mb.info.open");
			UI_DrawMainDiv(arg.Player(), true);
			UI_DrawSections(arg.Player(), "info");
			arg.Player().Command(string.Join(" ", arg.Args));
		}

		[ConsoleCommand("mb.bannerpage")]
		private void cmdBannerPage(ConsoleSystem.Arg arg)
		{
			if (arg.Player() == null || arg.Args.IsNullOrEmpty())
				return;

			if (!int.TryParse(arg.Args[0], out var bannerID))
				return;

			if (!cfg.MainSettings.Banners.ContainsKey(bannerID))
				bannerID = cfg.MainSettings.Banners.First().Key;

			var player = arg.Player();
			
			UI_DrawBanner(player, bannerID);
			UI_DrawBannerPages(player, bannerID);
		}

		[ConsoleCommand("mb.section")]
		private void cmdSection(ConsoleSystem.Arg arg)
		{
			if (arg.Player() == null || arg.Args.IsNullOrEmpty())
				return;

			var sectionKey = arg.Args[0];
			if (!cfg.BaseSettings.Sections.TryGetValue(sectionKey, out var section))
				return;

			Effect x = new Effect("assets/bundled/prefabs/fx/notice/loot.drag.grab.fx.prefab", arg.Player(), 0, new Vector3(), new Vector3());
			EffectNetwork.Send(x, arg.Player().Connection);

			Interface.CallHook("OnSectionChanged", arg.Player(), sectionKey);

			var player = arg.Player();

			player.SendConsoleCommand(section.Command);
			UI_DrawMainDiv(player, section.NeedDrawBG);
			UI_DrawSections(player, sectionKey);
		}

		[ConsoleCommand("mb.openmain")]
		private void cmdOpenMain(ConsoleSystem.Arg arg)
		{
			if (arg.Player() == null)
				return;
			
			UI_DrawMainPage(arg.Player());
		}

		[ConsoleCommand("mb.online.page")]
		private void cmdOnlinePage(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null || arg.Args == null || arg.Args.Length == 0)
				return;
			if (!int.TryParse(arg.Args[0], out var page))
				return;
			if (page < 0) page = 0;
			UI_DrawOnlinePlayers(player, page);
		}

		[ConsoleCommand("menu.open")]
		private void cmdOpenConsole(ConsoleSystem.Arg arg)
		{
			if (arg.Player() == null)
				return;
			
			cmdOpen(arg.Player());
		}
		[ChatCommand("menu")]
		private void cmdOpen(BasePlayer player)
		{
			UI_DrawMain(player);
		}

		#endregion
		
		#region Config
		private ConfigData cfg;

		public class ConfigData
		{
			[JsonProperty("Общие настройки", Order = 0)] public BaseUISettings BaseSettings;
			[JsonProperty("Настройки главного меню", Order = 1)] public MainPageSettings MainSettings;
			
			internal class BaseUISettings
			{
				[JsonProperty("Какой раздел открывать по умолчанию?", Order = 0)]
				public string DefaultSection;
				[JsonProperty("Разделы", Order = 1)] public Dictionary<string, Section> Sections;		
			}

			internal class MainPageSettings
			{
				[JsonProperty("Баннеры (номер - файл изображения)")] public Dictionary<int, string> Banners;
				[JsonProperty("Кнопки (max 3)")] public Dictionary<string, Button> Buttons;		
			}
		}

		protected override void LoadDefaultConfig()
		{
			var config = new ConfigData
			{
				BaseSettings = new()
				{
					DefaultSection = "menu",
					Sections = new()
					{
						["menu"] = new()
						{
							LangKey = "section_menu",
							Order = 0,
							
							Command = "mb.openmain"
						},
						["info"] = new()
						{
							LangKey = "section_info",
							Order = 8,
							Command = "mb.info.open"
						},
						["case"] = new()
						{
							LangKey = "section_case",
							Order = 3,
							NeedDrawBG = false,
							Command = "mb.case.open"
						},
						["rewards"] = new()
						{
							LangKey = "section_rewards",
							Order = 1,
							
							Command = "mb.daily.open"
						},
						["shop"] = new()
						{
							LangKey = "section_shop",
							Order = 6,
							Command = "mb.shop",
							NeedDrawBG = false
						},
						["skin"] = new()
						{
							LangKey = "section_skin",
							Order = 2,
							
							Command = "mb.skins"
						},
						["block"] = new()
						{
							LangKey = "section_block",
							Order = 7,
							Command = "mb.block.open"
						},
						["store"] = new()
						{
							LangKey = "section_store",
							Order = 9,
							Command = "mb.store.open"
						},
						["wipe"] = new()
						{
							LangKey = "section_wipe",
							Order = 5,
							Command = "mb.wipe.open"
						}
					}
				},
				MainSettings = new()
				{
					Banners = new()
					{
						[0] = "imagelink1",
						[1] = "imagelink2"
					},
					Buttons = new()
					{
						["commands"] = new()
						{
							LangKey = "btn.commands",
							Command = "mb.info.category commands"
						},
						["faq"] = new()
						{
							LangKey = "btn.faq",
							Command = "mb.info.category faq"
						},
						["info"] = new()
						{
							LangKey = "btn.info",
							Command = "mb.info.category help"
						}
					}
				}
			};
			SaveConfig(config);
		}

		protected override void LoadConfig()
		{
			base.LoadConfig();
			cfg = Config.ReadObject<ConfigData>();
			SaveConfig(cfg);

			cfg.MainSettings.Banners = cfg.MainSettings.Banners.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value);
		}

		private void SaveConfig(object config)
		{
			Config.WriteObject(config, true);
		}
		#endregion

		#region Langs
		private void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["section_menu"] = "MAIN",
				["section_rewards"] = "DAILY REWARDS",
				["section_skin"] = "SKINS",
				["section_case"] = "CASES",
				["section_kits"] = "KITS",
				["section_wipe"] = "RAID ALERTS",
				["section_shop"] = "SHOP",
				["section_block"] = "BLOCK",
				["section_info"] = "INFO",
				["section_store"] = "STORE",
				["btn.faq"] = "FAQ",
				["btn.commands"] = "COMMANDS",
				["btn.info"] = "INFO"
			}, this);

			lang.RegisterMessages(new Dictionary<string, string>
			{
				["section_menu"] = "ГЛАВНАЯ",
				["section_rewards"] = "ЕЖЕДНЕВНЫЕ НАГРАДЫ",
				["section_skin"] = "СКИНЫ",
				["section_case"] = "КЕЙСЫ",
				["section_kits"] = "НАБОРЫ",
				["section_wipe"] = "ОПОВЕЩЕНИЯ О РЕЙДЕ",
				["section_shop"] = "МАГАЗИН",
				["section_block"] = "БЛОКИРОВКА",
				["section_info"] = "ИНФОРМАЦИЯ",
				["section_store"] = "КОРЗИНА",
				["btn.faq"] = "ВОПРОСЫ",
				["btn.commands"] = "КОМАНДЫ",
				["btn.info"] = "ИНФО"
			}, this, "ru");
		}
		private string GetMsg(string key, ulong id, params object[] args) =>
			string.Format(lang.GetMessage(key, this, id.ToString()), args);

		private string GetMsg(string key, BasePlayer player, params object[] args) =>
			GetMsg(key, player.userID, args);

		#endregion

		#region API

		[HookMethod("API_GetImage")]
		private string API_GetImage(string key) => GuiManager.Get(key);

		#endregion
	}
}