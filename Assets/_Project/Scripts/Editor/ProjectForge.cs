using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TiantaiDash.EditorTools
{
    /// <summary>
    /// 编辑器生产工具：把「程序化生成」变成「一次性烘焙成可手改的资产」。
    ///
    /// ① 重建素材总表  → ArtLibrary.asset（所有贴图的直接引用）
    /// ② 生成预制体    → Prefabs/*.prefab（玩家 / 敌人 / 平台 / 印记 / 终点）
    /// ③ 烘焙关卡      → 把整个关卡铺进当前场景，之后纯手工调
    ///
    /// 都是幂等的，随时可以重跑；重跑 ③ 会覆盖旧的 Level / Player / Backdrop。
    /// </summary>
    public static class ProjectForge
    {
        const string SpriteRoot = "Assets/_Project/Art/Sprites";
        const string ArtRoot = "Assets/_Project/Art";
        const string LibPath = ArtRoot + "/ArtLibrary.asset";
        const string PrefabRoot = "Assets/_Project/Prefabs";
        const string AudioRoot = "Assets/_Project/Art/Audio";
        const string MaterialPath = ArtRoot + "/PlayerNoFriction.physicsMaterial2D";

        /// <summary>脚本 / 批处理调用时置 true，跳过确认弹窗。</summary>
        public static bool skipConfirm;

        // =============================================================== 菜单

        [MenuItem("Tools/天台疾行/① 重建素材总表", false, 1)]
        public static void RebuildArtLibrary()
        {
            ArtLibrary lib = RebuildLibrary();
            Selection.activeObject = lib;
            Debug.Log("[Forge] 素材总表已更新：" + LibPath);
        }

        [MenuItem("Tools/天台疾行/② 生成预制体", false, 2)]
        public static void BuildPrefabs()
        {
            ArtLibrary lib = LoadOrCreateLibrary();
            EnsureFolder(PrefabRoot);

            MakePlayer(lib);
            MakeDrone(lib);
            MakeTurret(lib);
            MakePlatform(lib, false);
            MakePlatform(lib, true);
            MakeMarker(lib);
            MakePortal(lib);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Forge] 预制体已生成到 " + PrefabRoot);
        }

        [MenuItem("Tools/天台疾行/③ 烘焙关卡到当前场景", false, 3)]
        public static void BakeLevel()
        {
            ArtLibrary lib = LoadOrCreateLibrary();

            if (LoadPrefab("RoofPlatform") == null || LoadPrefab("Player") == null)
            {
                BuildPrefabs();
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("烘焙关卡", "当前场景还没保存过，先 Ctrl+S 存一下再烘焙。", "好");
                return;
            }

            if (!skipConfirm && !EditorUtility.DisplayDialog("烘焙关卡",
                    "会用新生成的关卡替换场景里现有的 Level / Player / Backdrop。\n继续？", "烘焙", "取消"))
            {
                return;
            }

            // ---- 清理旧东西 ----
            DestroyRoot("Level");
            DestroyRoot("Player");
            DestroyRoot("Backdrop");

            // ---- 关卡 ----
            var builderGo = new GameObject("__LevelBuilder");
            LevelBuilder builder = builderGo.AddComponent<LevelBuilder>();
            builder.art = lib;
            builder.roofPrefab = LoadPrefab("RoofPlatform");
            builder.floatPrefab = LoadPrefab("FloatPlatform");
            builder.dronePrefab = LoadPrefab("Drone");
            builder.turretPrefab = LoadPrefab("Turret");
            builder.markerPrefab = LoadPrefab("DashMarker");
            builder.portalPrefab = LoadPrefab("Portal");
            builder.allowBuild = true;

            LevelBuildResult result = builder.Build();
            if (result.Root != null) result.Root.SetParent(null, true);
            Object.DestroyImmediate(builderGo);

            // ---- 玩家 ----
            GameObject playerPrefab = LoadPrefab("Player");
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            player.transform.position = new Vector3(result.StartX, result.StartY + 0.02f, 0f);

            // ---- 背景 ----
            Camera cam = SetupCamera(result.StartX);
            ParallaxBackdrop backdrop = BuildBackdrop(lib, cam);

            // ---- Game 物体：所有系统组件都摆上去，Inspector 里能改 ----
            GameObject game = GameObject.Find("Game");
            if (game == null) game = new GameObject("Game");

            GameManager gm = GetOrAdd<GameManager>(game);
            InputHandler input = GetOrAdd<InputHandler>(game);
            LevelManager levelManager = GetOrAdd<LevelManager>(game);
            DistanceTracker tracker = GetOrAdd<DistanceTracker>(game);
            UpgradeManager upgrades = GetOrAdd<UpgradeManager>(game);
            FallDetector fall = GetOrAdd<FallDetector>(game);
            TimeJuice juice = GetOrAdd<TimeJuice>(game);
            PrototypeUI ui = GetOrAdd<PrototypeUI>(game);
            AudioManager audio = GetOrAdd<AudioManager>(game);
            GameBootstrap boot = GetOrAdd<GameBootstrap>(game);

            PlayerWallet wallet = GetOrAdd<PlayerWallet>(game);
            wallet.coins = 0;

            ui.wallet = wallet;

            // 击杀奖励（击杀 → 自动冲刺），挂 Game 上
            KillReward reward = GetOrAdd<KillReward>(game);
            reward.dash = null;
            reward.iai = null;
            ui.iai = GetOrAdd<IaiCharge>(GameObject.Find("Player"));
            ui.heart = lib.heart;
            ui.digitSheet = lib.uiDigits;
            ui.scanline = lib.uiScanline;
            FillAudio(audio);

            tracker.player = player.transform;
            fall.player = player.transform;

            boot.gameManager = gm;
            boot.input = input;
            boot.levelManager = levelManager;
            boot.tracker = tracker;
            boot.upgrades = upgrades;
            boot.fallDetector = fall;
            boot.audioManager = audio;
            boot.ui = ui;
            boot.cameraFollow = cam.GetComponent<CameraFollow>();
            boot.backdrop = backdrop;

            boot.playerHealth = player.GetComponent<PlayerHealth>();
            boot.playerRunner = player.GetComponent<PlayerRunner>();
            boot.playerJump = player.GetComponent<PlayerJump>();
            boot.playerCombat = player.GetComponent<PlayerCombat>();
            boot.dashController = player.GetComponent<DashController>();

            PlacePickups(lib, result, result.Root != null ? result.Root : game.transform);
            MakeSomeChasers(result);

            tracker.CollectFromScene();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = result.Root != null ? result.Root.gameObject : game;

            Debug.Log(string.Format(
                "[Forge] 烘焙完成：{0} 段平台 / {1} 个敌人 / {2} 个印记 / 终点 X={3:F0} / 关卡长 {4:F0}u",
                result.Platforms.Count, result.Enemies.Count, result.Markers.Count, result.PortalX, result.PortalX));
        }

        [MenuItem("Tools/天台疾行/④ 一键重建（总表 → 预制体 → 烘焙）", false, 4)]
        public static void RebuildAll()
        {
            RebuildLibrary();
            BuildPrefabs();
            BakeLevel();
        }

        [MenuItem("Tools/天台疾行/校验素材引用", false, 20)]
        public static void Validate()
        {
            ArtLibrary lib = LoadOrCreateLibrary();
            var missing = new List<string>();

            void Check(Object o, string label) { if (o == null) missing.Add(label); }
            void CheckSet(Sprite[] set, string label) { if (set == null || set.Length == 0 || set.Any(s => s == null)) missing.Add(label); }

            CheckSet(lib.playerIdle, "playerIdle");
            CheckSet(lib.playerRun, "playerRun");
            CheckSet(lib.playerRise, "playerRise");
            CheckSet(lib.playerFall, "playerFall");
            CheckSet(lib.playerShoot, "playerShoot");
            CheckSet(lib.playerHurt, "playerHurt");
            CheckSet(lib.droneFrames, "droneFrames");
            CheckSet(lib.turretFrames, "turretFrames");
            Check(lib.tileRoof, "tileRoof");
            Check(lib.tileWall, "tileWall");
            Check(lib.tileFloat, "tileFloat");
            Check(lib.tileNeon, "tileNeon");
            Check(lib.tilePipe, "tilePipe");
            Check(lib.dashArrow, "dashArrow");
            Check(lib.antenna, "antenna");
            CheckSet(lib.banners, "banners");
            CheckSet(lib.bigBanners, "bigBanners");
            Check(lib.openSign, "openSign");
            Check(lib.controlBox, "controlBox");
            CheckSet(lib.hitFrames, "hitFrames");
            CheckSet(lib.explosionFrames, "explosionFrames");
            Check(lib.heart, "heart");
            Check(lib.sky, "sky");
            Check(lib.bgFar, "bgFar");
            Check(lib.bgMid, "bgMid");
            Check(lib.bgNear, "bgNear");

            if (missing.Count == 0) Debug.Log("[Forge] 素材总表完整，没有空引用。");
            else Debug.LogWarning("[Forge] 以下字段缺图：" + string.Join(", ", missing));
        }

        // =============================================================== 素材总表

        public static ArtLibrary LoadOrCreateLibrary()
        {
            var lib = AssetDatabase.LoadAssetAtPath<ArtLibrary>(LibPath);
            if (lib == null) lib = RebuildLibrary();
            return lib;
        }

        public static ArtLibrary RebuildLibrary()
        {
            EnsureFolder(ArtRoot);

            var lib = AssetDatabase.LoadAssetAtPath<ArtLibrary>(LibPath);
            bool isNew = lib == null;
            if (isNew)
            {
                lib = ScriptableObject.CreateInstance<ArtLibrary>();
                AssetDatabase.CreateAsset(lib, LibPath);
            }

            lib.playerIdle = Set("Player", "player_idle");
            lib.playerRun = Set("Player", "player_run");
            // 原包里 player_rise / player_fall 是空翻和"坐姿"，跑酷看着很怪；
            // 改用跑步循环里的腾空帧：run_1 = 大跨步伸展（起跳），run_3 = 收腿（下落）
            lib.playerRise = Take(lib.playerRun, 1);
            lib.playerFall = Take(lib.playerRun, 3);
            lib.playerLand = new Sprite[0];   // 落地只做压扁形变，不换姿势
            lib.playerShoot = Set("Player", "player_shoot");
            lib.playerHurt = Set("Player", "player_hurt");

            lib.droneFrames = Set("Enemy", "enemy_drone");
            lib.turretFrames = Set("Enemy", "enemy_turret");

            lib.tileRoof = One("Tiles/tile_roof");
            lib.tileWall = One("Tiles/tile_wall");
            lib.tileFloat = One("Tiles/tile_float");
            lib.tileNeon = One("Tiles/tile_neon");
            lib.tilePipe = One("Tiles/tile_pipe");

            lib.dashArrow = One("Props/prop_arrow");
            lib.antenna = One("Props/antenna");
            lib.banners = new[]
            {
                One("Props/banner_neon_0"),
                One("Props/banner_coke_0"),
                One("Props/banner_sushi_0"),
            }.Where(s => s != null).ToArray();

            lib.flagFrames = Set("Props", "prop_flag");
            lib.torchFrames = Set("Props", "prop_torch");

            lib.openSign = One("Props/prop_open");
            lib.controlBox = One("Props/control_box_1");
            lib.bigBanners = Set("Props", "banner_big");

            lib.hitFrames = Set("Fx", "fx_hit");
            lib.explosionFrames = Set("Fx", "fx_explosion");
            lib.heart = One("UI/ui_heart");
            lib.uiDigits = One("UI/ui_digits");
            lib.uiScanline = One("UI/ui_scanline");

            lib.sword = One("Props/prop_sword");
            lib.coin = One("UI/ui_coin");
            lib.shopIcon = One("Props/prop_shop");

            lib.moonGlow = One("Props/prop_moon_glow");
            lib.meteorHead = One("Fx/fx_meteor");
            lib.lightCone = One("Fx/fx_lightcone");
            lib.billboard = One("Props/banner_side_0");
            lib.dustFrames = Set("Fx", "fx_dust");
            lib.white = One("UI/ui_white");

            lib.sky = One("Bg/sky_full");
            lib.bgFar = One("Bg/bg_far");
            lib.bgMid = One("Bg/bg_mid");
            lib.bgNear = One("Bg/bg_near");

            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            return lib;
        }

        static Sprite[] Set(string folder, string prefix)
        {
            string dir = SpriteRoot + "/" + folder;
            if (!Directory.Exists(dir)) return new Sprite[0];

            var files = Directory.GetFiles(dir, prefix + "_*.png")
                .Select(f => f.Replace('\\', '/'))
                .OrderBy(NaturalIndex)
                .ToList();

            var result = new List<Sprite>(files.Count);
            foreach (string f in files)
            {
                var s = AssetDatabase.LoadAssetAtPath<Sprite>(f);
                if (s != null) result.Add(s);
            }
            return result.ToArray();
        }

        static Sprite[] Take(Sprite[] src, int index)
        {
            if (src == null || src.Length == 0) return new Sprite[0];
            return new[] { src[Mathf.Clamp(index, 0, src.Length - 1)] };
        }

        static int NaturalIndex(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            int i = name.LastIndexOf('_');
            if (i < 0) return 0;
            int n;
            return int.TryParse(name.Substring(i + 1), out n) ? n : 0;
        }

        static Sprite One(string relative)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(SpriteRoot + "/" + relative + ".png");
        }

        // =============================================================== 预制体

        static void MakePlayer(ArtLibrary art)
        {
            var go = new GameObject("Player");
            go.layer = LayerMask.NameToLayer("Player");

            // 视觉单独放到 Visual 子物体：挤压拉伸只改子物体 scale，不会把碰撞体一起拉变形
            var visualGo = new GameObject("Visual");
            visualGo.transform.SetParent(go.transform, false);
            var sr = visualGo.AddComponent<SpriteRenderer>();
            sr.sprite = art.FirstFrame(art.playerRun);
            sr.sortingOrder = 20;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3f;
            rb.mass = 1f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var col = go.AddComponent<BoxCollider2D>();
            // 视觉缩到 0.78，碰撞体同步收窄，不然会出现"看不见的墙"
            col.size = new Vector2(0.78f, 1.87f);
            col.offset = new Vector2(0f, 0.95f);
            col.edgeRadius = 0.02f;
            col.sharedMaterial = GetOrCreateNoFriction();

            // 剑：**待机时插在背后**（层级 19，低于角色的 20 → 被身体挡住一半），
            // 挥砍时由 SwordSwing 抽到身前（22）下劈，劈完再收回背后。
            var swordGo = new GameObject("Sword");
            swordGo.transform.SetParent(go.transform, false);
            swordGo.transform.localPosition = new Vector3(-0.46f, 0.94f, 0f);
            swordGo.transform.localRotation = Quaternion.Euler(0f, 0f, 37f);

            var swordSr = swordGo.AddComponent<SpriteRenderer>();
            swordSr.sprite = art.sword;
            swordSr.sortingOrder = 19;
            swordSr.enabled = true;

            var swing = swordGo.AddComponent<SwordSwing>();
            swing.blade = swordSr;
            swing.baseLength = 1.75f;

            var combat = go.AddComponent<PlayerCombat>();
            combat.sword = swing;
            combat.hitFx = art.FirstFrame(art.hitFrames);

            go.AddComponent<PlayerRunner>();
            go.AddComponent<PlayerJump>();
            go.AddComponent<PlayerHealth>();
            go.AddComponent<DashController>();

            var visual = go.AddComponent<PlayerVisual>();
            visual.idleFrames = art.playerIdle;
            visual.runFrames = art.playerRun;
            visual.riseFrames = art.playerRise;
            visual.fallFrames = art.playerFall;
            visual.shootFrames = art.playerShoot;
            visual.hurtFrames = art.playerHurt;
            visual.landFrames = art.playerLand;

            go.AddComponent<DashFx>();

            // 居合拔刀蓄力（组件自己会在 Awake 里找 runner/jump/dash/combat/sword，
            // 这里只挂上，参数用脚本默认值；后续在 Inspector 里调三段手感）
            go.AddComponent<IaiCharge>();

            SavePrefab(go, "Player");
        }

        static void MakeDrone(ArtLibrary art)
        {
            var go = new GameObject("Drone");
            go.layer = LayerMask.NameToLayer("Enemy");

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(1.6f, 1.2f);

            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = art.FirstFrame(art.droneFrames);
            sr.sortingOrder = 5;

            var vis = visual.AddComponent<EnemyVisual>();
            vis.frames = art.droneFrames;
            vis.variant = 0;
            vis.fps = 8f;

            var hp = go.AddComponent<EnemyHealth>();
            hp.SetMaxHealth(2);

            var ctrl = go.AddComponent<EnemyController>();
            ctrl.patrolRange = 4f;
            ctrl.patrolSpeed = 2.2f;

            go.AddComponent<HoverBob>();

            SavePrefab(go, "Drone");
        }

        static void MakeTurret(ArtLibrary art)
        {
            var go = new GameObject("Turret");
            go.layer = LayerMask.NameToLayer("Enemy");

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(1.4f, 1.8f);

            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = art.FirstFrame(art.turretFrames);
            sr.sortingOrder = 5;

            var vis = visual.AddComponent<EnemyVisual>();
            vis.frames = art.turretFrames;
            vis.variant = 1;
            vis.fps = 10f;

            var hp = go.AddComponent<EnemyHealth>();
            hp.SetMaxHealth(3);

            var ctrl = go.AddComponent<EnemyController>();
            ctrl.patrolRange = 0f;
            ctrl.patrolSpeed = 0f;

            SavePrefab(go, "Turret");
        }

        static void MakePlatform(ArtLibrary art, bool isFloat)
        {
            var go = new GameObject(isFloat ? "FloatPlatform" : "RoofPlatform");
            go.layer = LayerMask.NameToLayer("Ground");

            var col = go.AddComponent<BoxCollider2D>();

            var slabGo = new GameObject("Slab");
            slabGo.transform.SetParent(go.transform, false);
            var slab = slabGo.AddComponent<SpriteRenderer>();
            slab.sprite = isFloat ? art.tileFloat : art.tileRoof;
            slab.drawMode = SpriteDrawMode.Tiled;
            slab.tileMode = SpriteTileMode.Continuous;
            slab.size = new Vector2(12f, 1f);
            slab.sortingOrder = 0;

            SpriteRenderer bodySr = null;
            if (!isFloat)
            {
                var bodyGo = new GameObject("Body");
                bodyGo.transform.SetParent(go.transform, false);
                bodySr = bodyGo.AddComponent<SpriteRenderer>();
                bodySr.sprite = art.tileWall;
                bodySr.drawMode = SpriteDrawMode.Tiled;
                bodySr.tileMode = SpriteTileMode.Continuous;
                bodySr.size = new Vector2(12f, 14f);
                bodySr.color = new Color(0.72f, 0.76f, 0.95f);
                bodySr.sortingOrder = -6;
            }

            var piece = go.AddComponent<PlatformPiece>();
            piece.solid = col;
            piece.slab = slab;
            piece.body = bodySr;
            piece.width = 12f;
            piece.thickness = 1f;
            piece.bodyDepth = 14f;
            piece.isFloating = isFloat;
            piece.Apply();

            SavePrefab(go, isFloat ? "FloatPlatform" : "RoofPlatform");
        }

        static void MakeMarker(ArtLibrary art)
        {
            var go = new GameObject("DashMarker");
            go.layer = LayerMask.NameToLayer("Trigger");

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(1.2f, 4.2f);
            col.offset = new Vector2(0f, 2.1f);

            var arrow = new GameObject("Arrow");
            arrow.transform.SetParent(go.transform, false);
            arrow.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            var sr = arrow.AddComponent<SpriteRenderer>();
            sr.sprite = art.dashArrow;
            sr.sortingOrder = 3;

            var marker = go.AddComponent<DashMarker>();
            marker.triggerWidth = 1.2f;
            marker.triggerHeight = 4.2f;

            SavePrefab(go, "DashMarker");
        }

        static void MakePortal(ArtLibrary art)
        {
            var go = new GameObject("Portal");
            go.layer = LayerMask.NameToLayer("Trigger");

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(3f, 8f);
            col.offset = new Vector2(0f, 4f);

            AddDecor(go.transform, "Banner", new Vector3(0f, 7f, 0f), art.FirstFrame(art.bigBanners), 2, 2f);
            AddDecor(go.transform, "Open", new Vector3(-5f, 3f, 0f), art.openSign, 2, 2f);
            AddDecor(go.transform, "Box", new Vector3(5f, 1.2f, 0f), art.controlBox, 1, 1f);

            var portal = go.AddComponent<PortalExit>();
            portal.Configure(3f, 8f);

            SavePrefab(go, "Portal");
        }

        static void AddDecor(Transform parent, string name, Vector3 pos, Sprite sprite, int order, float scale)
        {
            if (sprite == null) return;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
        }

        static void SavePrefab(GameObject go, string name)
        {
            string path = PrefabRoot + "/" + name + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        static GameObject LoadPrefab(string name)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + "/" + name + ".prefab");
        }

        static PhysicsMaterial2D GetOrCreateNoFriction()
        {
            var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(MaterialPath);
            if (mat != null) return mat;

            mat = new PhysicsMaterial2D("PlayerNoFriction") { friction = 0f, bounciness = 0f };
            AssetDatabase.CreateAsset(mat, MaterialPath);
            return mat;
        }

        // =============================================================== 场景

        /// <summary>沿平台撒金币和商店招牌。金币间距按平台宽度自动排，不会挤在边上。</summary>
        static void PlacePickups(ArtLibrary art, LevelBuildResult result, Transform parent)
        {
            if (result == null || result.Platforms == null) return;

            var rng = new System.Random(20261021);
            var root = new GameObject("Pickups").transform;
            root.SetParent(parent, false);

            for (int i = 0; i < result.Platforms.Count; i++)
            {
                PlatformPiece p = result.Platforms[i];
                if (p == null) continue;

                float halfW = p.width * 0.5f;

                // 金币：隔一块平台撒一次，2~3 枚
                if (i % 2 == 0 && p.width > 8f)
                {
                    int n = rng.Next(2, 4);
                    for (int c = 0; c < n; c++)
                    {
                        float t = (c + 0.5f) / n;
                        float x = p.transform.position.x - halfW * 0.7f + halfW * 1.4f * t;
                        SpawnCoin(root, art, new Vector3(x, p.transform.position.y + 1.7f, 0f));
                    }
                }

                // 商店：每 7 块平台一个，要够宽
                if (i % 7 == 4 && p.width >= 16f)
                {
                    SpawnShop(root, art, new Vector3(p.transform.position.x + halfW - 5f, p.transform.position.y, 0f));
                }
            }
        }

        static void SpawnCoin(Transform parent, ArtLibrary art, Vector3 pos)
        {
            var go = new GameObject("Coin");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = art.coin;
            sr.sortingOrder = 16;

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(1.1f, 1.1f);

            go.AddComponent<Coin>();
        }

        static void SpawnShop(Transform parent, ArtLibrary art, Vector3 pos)
        {
            var go = new GameObject("Shop");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            var icon = new GameObject("Icon");
            icon.transform.SetParent(go.transform, false);
            icon.transform.localPosition = new Vector3(0f, 2.6f, 0f);
            icon.transform.localScale = Vector3.one * 3.2f;

            var sr = icon.AddComponent<SpriteRenderer>();
            sr.sprite = art.shopIcon;
            sr.sortingOrder = 16;

            var glow = new GameObject("Glow");
            glow.transform.SetParent(icon.transform, false);
            glow.transform.localScale = Vector3.one * 2.2f;
            var gsr = glow.AddComponent<SpriteRenderer>();
            gsr.sprite = art.moonGlow;
            gsr.color = new Color(1f, 0.9f, 0.5f, 0.16f);
            gsr.sortingOrder = 15;

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(2.6f, 3.4f);
            col.offset = new Vector2(0f, 1.6f);

            var shop = go.AddComponent<ShopIcon>();
            shop.price = 5;
        }

        /// <summary>把 40% 的敌人改成追踪型，剩下的保持巡逻。</summary>
        static void MakeSomeChasers(LevelBuildResult result)
        {
            if (result == null || result.Enemies == null) return;

            for (int i = 0; i < result.Enemies.Count; i++)
            {
                EnemyController e = result.Enemies[i];
                if (e == null) continue;

                if (i % 5 == 1 || i % 5 == 3)
                {
                    e.chase = true;
                    e.chaseSpeed = 6.2f + (i % 3) * 0.9f;
                    e.chaseLift = 0.9f;
                    e.name = "Chaser_" + i;
                }
            }
        }

        static Camera SetupCamera(float startX)
        {
            GameObject camGo = GameObject.FindWithTag("MainCamera");
            if (camGo == null)
            {
                camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                if (Object.FindObjectOfType<AudioListener>() == null) camGo.AddComponent<AudioListener>();
            }

            var cam = camGo.GetComponent<Camera>();
            if (cam == null) cam = camGo.AddComponent<Camera>();

            cam.orthographic = true;
            cam.orthographicSize = 16.875f;   // 33.75u 高 = 540px @PPU16
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.04f, 0.09f);
            cam.transform.rotation = Quaternion.identity;
            cam.transform.position = new Vector3(startX + 12f, 5f, -10f);

            var follow = camGo.GetComponent<CameraFollow>();
            if (follow == null) follow = camGo.AddComponent<CameraFollow>();
            follow.offset = new Vector3(12f, 5f, -10f);
            follow.minY = 5f;
            follow.maxY = 20f;

            return cam;
        }

        static ParallaxBackdrop BuildBackdrop(ArtLibrary art, Camera cam)
        {
            var root = new GameObject("Backdrop");
            var backdrop = root.AddComponent<ParallaxBackdrop>();
            backdrop.cam = cam;
            backdrop.autoFollow = true;

            // 天空：挂相机子节点，永远铺满画面
            var sky = new GameObject("Sky");
            sky.transform.SetParent(cam.transform, false);
            sky.transform.localPosition = new Vector3(0f, 0f, 30f);
            sky.transform.localScale = Vector3.one * 1.25f;
            var skySr = sky.AddComponent<SpriteRenderer>();
            skySr.sprite = art.sky;
            skySr.sortingOrder = -400;
            backdrop.skyLayer = sky.transform;

            backdrop.farLayer = MakeLayer(root.transform, "Bg_Far", art.bgFar, -320, -9f).transform;
            backdrop.midLayer = MakeLayer(root.transform, "Bg_Mid", art.bgMid, -315, -7f).transform;
            backdrop.nearLayer = MakeLayer(root.transform, "Bg_Near", art.bgNear, -310, -5f).transform;

            BuildSkyFx(art, cam);
            backdrop.SnapToCamera(cam.transform.position.x);
            return backdrop;
        }

        /// <summary>
        /// 一层背景 = 若干个并排的 Simple sprite。宽度按"够盖住一屏 + 一个平铺周期"算，
        /// 父节点由 ParallaxBackdrop 按周期取模滚动，所以接缝看不出来。
        /// </summary>
        /// <summary>
        /// 天空层动态内容：月亮光晕（挂在相机上做呼吸式明暗）、流星 + 飞行卡车、昼夜色调。
        /// 全部是相机子物体，跟着相机走，不依赖关卡长度。
        /// </summary>
        static void BuildSkyFx(ArtLibrary art, Camera cam)
        {
            // ---- 月亮光晕：位置对着 sky_full.png 里那颗月亮 ----
            GameObject moon = GameObject.Find("MoonGlow");
            if (moon != null) Object.DestroyImmediate(moon);

            moon = new GameObject("MoonGlow");
            moon.transform.SetParent(cam.transform, false);
            moon.transform.localPosition = new Vector3(17.2f, 10.9f, 29f);
            moon.transform.localScale = Vector3.one * 9f;

            var moonSr = moon.AddComponent<SpriteRenderer>();
            moonSr.sprite = art.moonGlow;
            moonSr.color = new Color(1f, 1f, 1f, 0.18f);
            moonSr.sortingOrder = -390;

            // ---- 流星 + 卡车 ----
            GameObject skyFx = GameObject.Find("SkyEvents");
            if (skyFx != null) Object.DestroyImmediate(skyFx);

            skyFx = new GameObject("SkyEvents");
            skyFx.transform.SetParent(cam.transform, false);
            skyFx.transform.localPosition = Vector3.zero;

            var events = skyFx.AddComponent<SkyEvents>();
            events.meteorSprite = art.meteorHead;
            events.moonGlowSprite = art.moonGlow;
            events.lightConeSprite = art.lightCone;
            events.billboardSprite = art.billboard;
            events.droneFrames = art.droneFrames;
            events.moonGlow = moon.transform;

            // 昼夜功能已下线：保留夜间固定基调
            GameObject dayNight = GameObject.Find("DayNight");
            if (dayNight != null) Object.DestroyImmediate(dayNight);
        }

        static GameObject MakeLayer(Transform parent, string name, Sprite sprite, int order, float y)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(0f, y, 0f);

            if (sprite == null) return go;

            float tileWidth = sprite.rect.width / sprite.pixelsPerUnit;
            // 取模后层左边缘最远会落后一个周期：向左右各多铺一块，保证两边都不露空
            int count = Mathf.CeilToInt((70f + tileWidth * 2f) / tileWidth) + 2;

            for (int i = 0; i < count; i++)
            {
                var t = new GameObject("Tile" + i);
                t.transform.SetParent(go.transform, false);
                t.transform.localPosition = new Vector3((i - 1) * tileWidth, 0f, 0f);

                var sr = t.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = order;
                sr.drawMode = SpriteDrawMode.Simple;
            }

            return go;
        }

        static void FillAudio(AudioManager audio)
        {
            if (audio == null) return;

            audio.bgm = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioRoot + "/bgm_loop.wav");

            string[] ids =
            {
                "sfx_dash", "sfx_impact", "sfx_hit", "sfx_kill", "sfx_jump",
                "sfx_land", "sfx_hurt", "sfx_select", "sfx_portal"
            };

            var list = new List<AudioManager.SfxClip>();
            foreach (string id in ids)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioRoot + "/" + id + ".wav");
                list.Add(new AudioManager.SfxClip { id = id, clip = clip, volumeScale = 1f });
            }
            audio.sfx = list.ToArray();
        }

        // =============================================================== 通用

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        static void DestroyRoot(string name)
        {
            GameObject go = GameObject.Find(name);
            if (go != null) Object.DestroyImmediate(go);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
