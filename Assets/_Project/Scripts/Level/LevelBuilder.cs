using System.Collections.Generic;
using UnityEngine;

namespace TiantaiDash
{
    public struct PlatformDef
    {
        public float X;
        public float Width;
        public float Y;
        public bool IsFloat;
        public bool IsNode;
    }

    public class LevelBuildResult
    {
        public float StartX;
        public float StartY;
        public float PortalX;
        public Transform Root;
        public readonly List<EnemyController> Enemies = new List<EnemyController>();
        public readonly List<DashMarker> Markers = new List<DashMarker>();
        public readonly List<PlatformPiece> Platforms = new List<PlatformPiece>();
    }

    /// <summary>
    /// 关卡「烘焙器」。世界尺度：1 格 = 1u = 16px，镜头 60x33.75 格（960x540 @PPU16）。
    /// 跳跃：v0=15.7 / g=29.43 → 滞空 1.07s，跑速 11 时水平 11.7u，所以间隙按 ≤7u 设计。
    ///
    /// **只在编辑器里跑一次**（Tools/天台疾行/烘焙关卡到场景）：把所有东西生成成场景里的真实对象
    /// （平台是 PlatformPiece 预制体实例、敌人/印记/终点也是预制体实例）。生成完这个组件会被移除，
    /// 之后你直接在场景里拖、删、改宽度、换贴图，运行时不再生成任何东西。
    /// </summary>
    public class LevelBuilder : MonoBehaviour
    {
        [Header("素材总表")]
        public ArtLibrary art;

        [Header("预制体（留空会用代码临时搭，但建议指定，方便统一改外观）")]
        public GameObject roofPrefab;
        public GameObject floatPrefab;
        public GameObject dronePrefab;
        public GameObject turretPrefab;
        public GameObject markerPrefab;
        public GameObject portalPrefab;

        [Header("尺寸")]
        public float startX = 5f;
        public float slabThickness = 1f;
        public float bodyDepth = 14f;

        [Header("长度与节点")]
        public float levelLength = 1600f;
        public float nodeAX = 520f;
        public float nodeBX = 1080f;

        [Header("生成选项")]
        public int seed = 20261021;
        [Tooltip("窗、管线、招牌、天线这些装饰物")]
        public bool decorate = true;
        [Tooltip("默认隐藏，避免误在运行时跑生成。烘焙工具会临时打开它。")]
        public bool allowBuild;

        static readonly Color NeonTint = new Color(0.55f, 0.98f, 1f);

        readonly List<PlatformDef> _platforms = new List<PlatformDef>();

        LevelBuildResult _result;
        Transform _root;
        float _x;
        float _y;
        bool _nodeADone;
        bool _nodeBDone;
        System.Random _rng;

        public LevelBuildResult Build()
        {
            if (art == null) art = ArtLibrary.Instance;

            _rng = new System.Random(seed);
            _root = new GameObject("Level").transform;
            _root.SetParent(transform, false);
            _root.position = Vector3.zero;

            _platforms.Clear();
            _result = new LevelBuildResult { Root = _root };
            _x = 0f;
            _y = 0f;
            _nodeADone = false;
            _nodeBDone = false;

            Roof(34f, 0f);

            int section = 0;
            int guard = 0;
            while (_x < levelLength && guard++ < 400)
            {
                if (!_nodeADone && _x >= nodeAX)
                {
                    Gap(4f);
                    Roof(28f, _y, true);
                    _nodeADone = true;
                    continue;
                }
                if (!_nodeBDone && _x >= nodeBX)
                {
                    Gap(4f);
                    Roof(28f, _y, true);
                    _nodeBDone = true;
                    continue;
                }

                switch (section % 6)
                {
                    case 0: LeapGap(Range(14f, 16f)); break;   // 中缝：普通跳极限
                    case 1: FloatChain(4); break;              // 窄悬浮链
                    case 2: Gauntlet(); break;                 // 敌人密集
                    case 3: LeapGap(Range(21f, 24f)); break;   // 深渊：只能靠冲刺
                    case 4: ClimbDash(); break;                // 爬升 + 顶部深坑
                    default: DoubleDash(); break;              // 连续两个印记缝
                }
                section++;
            }

            // 终点：一段安全直道 + 霓虹门
            Gap(5f);
            _y = Mathf.Clamp(_y, -2f, 5f);
            float portalStart = _x;
            Roof(36f, _y);
            float portalX = portalStart + 22f;
            CreatePortal(portalX, _platforms[_platforms.Count - 1].Y);

            _result.StartX = startX;
            _result.StartY = _platforms[0].Y;
            _result.PortalX = portalX;

            return _result;
        }

        // ---------------- 段落（全部按"冲刺驱动"设计） ----------------

        // 跳跃能力：v0=15.7 / g=29.43 → 滞空 1.07s，跑速 18 时水平 19.2u；
        // 印记冲刺期间速度 ×2.6（46.8），持续 0.55s 推进 25.7u，期间还能补一次冲刺跳。
        // 扣掉印记到边缘的 5u：普通跳 ≤16u 稳过，17~19u 极限，
        // **20u 以上普通跳绝对过不去 → 只能靠冲刺**（这就是"冲刺专属段"）。

        /// <summary>直道 + 末尾印记 + 一道大缝。缝越宽越依赖冲刺时机。</summary>
        void LeapGap(float gap)
        {
            Gap(Range(4.5f, 6f));
            Roof(Range(20f, 26f), _y);

            PlatformDef run = _platforms[_platforms.Count - 1];
            AddMarker(run.X + run.Width - 5f, run.Y);

            // 缝前再放个无人机，冲过去时顺手撞掉
            if (_rng.NextDouble() < 0.6f) SpawnDroneAbove(_platforms.Count - 1);

            Gap(gap);
            _y = Mathf.Clamp(_y + Range(-1.5f, 2f), -2f, 9f);
            Roof(Range(18f, 24f), _y);
            SpawnDroneAbove(_platforms.Count - 1);
        }

        /// <summary>悬浮平台链：平台更窄、间隙更大，落地窗口很挤。</summary>
        void FloatChain(int count)
        {
            for (int i = 0; i < count; i++)
            {
                Gap(Range(9f, 12f));
                _y = Mathf.Clamp(_y + Range(1.5f, 2.8f), -2f, 11f);
                Float(Range(3.2f, 4.6f), _y);

                // 中段那块给个印记，用来跨越后面的长距离
                if (i == 1)
                {
                    PlatformDef f = _platforms[_platforms.Count - 1];
                    AddMarker(f.X + f.Width * 0.5f, f.Y);
                }
            }

            Gap(Range(8f, 10f));
            _y = Mathf.Clamp(_y - Range(3f, 5f), -2f, 6f);
            Roof(Range(16f, 22f), _y);
            SpawnTurret(_platforms.Count - 1);
            SpawnDroneAbove(_platforms.Count - 1);
        }

        /// <summary>爬升三级 + 顶部印记 + 大落差深坑。</summary>
        void ClimbDash()
        {
            for (int i = 0; i < 3; i++)
            {
                Gap(Range(7f, 9f));
                _y = Mathf.Clamp(_y + 2.2f, -2f, 12f);
                Roof(Range(8f, 11f), _y);
                if (i == 1) SpawnDroneAbove(_platforms.Count - 1);
            }

            PlatformDef top = _platforms[_platforms.Count - 1];
            AddMarker(top.X + top.Width - 5f, top.Y);

            Gap(Range(20f, 23f));   // 冲刺专属：普通跳 19.2u 跨不过去
            _y = Mathf.Clamp(_y - Range(7f, 10f), -2f, 4f);
            Roof(Range(20f, 26f), _y);
            SpawnTurret(_platforms.Count - 1);
        }

        /// <summary>两个紧挨的印记缝，中间只有一小块落脚点。</summary>
        void DoubleDash()
        {
            Gap(Range(4f, 5f));
            Roof(13f, _y);
            PlatformDef a = _platforms[_platforms.Count - 1];
            AddMarker(a.X + a.Width - 5f, a.Y);

            Gap(Range(18f, 20f));
            Roof(11f, _y);
            PlatformDef b = _platforms[_platforms.Count - 1];
            AddMarker(b.X + b.Width - 5f, b.Y);

            Gap(Range(20f, 22f));
            Roof(Range(18f, 24f), _y);
            SpawnDroneAbove(_platforms.Count - 1);
            if (_rng.NextDouble() < 0.6f) SpawnTurret(_platforms.Count - 1);
        }

        /// <summary>敌人密集段：双炮塔 + 无人机，落脚点窄，还留一段"冲刺穿怪"。</summary>
        void Gauntlet()
        {
            Gap(Range(5f, 6.5f));
            Roof(Range(15f, 19f), _y);
            SpawnTurret(_platforms.Count - 1);
            SpawnDroneAbove(_platforms.Count - 1);

            Gap(Range(10f, 12f));
            Float(Range(3.6f, 5f), Mathf.Clamp(_y + Range(1.5f, 2.6f), -1f, 10f));

            Gap(Range(9f, 11f));
            Roof(Range(15f, 19f), _y);
            SpawnTurret(_platforms.Count - 1);
            SpawnDroneAbove(_platforms.Count - 1);

            // 冲刺穿怪：印记在最前，后面一排炮塔
            Gap(Range(4f, 5f));
            Roof(Range(20f, 24f), _y);
            PlatformDef p = _platforms[_platforms.Count - 1];
            AddMarker(p.X + 3f, p.Y);
            SpawnTurret(_platforms.Count - 1);
        }

        // ---------------- 基础构件 ----------------

        void Gap(float gap) { _x += gap; }

        void Roof(float width, float y, bool node = false)
        {
            var def = new PlatformDef { X = _x, Width = width, Y = y, IsNode = node };
            _platforms.Add(def);
            CreatePlatform(def);
            _x += width;
        }

        void Float(float width, float y)
        {
            var def = new PlatformDef { X = _x, Width = width, Y = y, IsFloat = true };
            _platforms.Add(def);
            CreatePlatform(def);
            _x += width;
        }

        float Range(float a, float b) { return a + (float)_rng.NextDouble() * (b - a); }

        // ---------------- 生成 ----------------

        void CreatePlatform(PlatformDef def)
        {
            GameObject prefab = def.IsFloat ? floatPrefab : roofPrefab;
            string name = (def.IsFloat ? "Float_" : "Roof_") + def.X.ToString("F0");

            GameObject go = Spawn(prefab, name, new Vector3(def.X + def.Width * 0.5f, def.Y, 0f));
            go.layer = GameLayers.Ground;

            var piece = go.GetComponent<PlatformPiece>();
            if (piece == null) piece = go.AddComponent<PlatformPiece>();
            piece.width = def.Width;
            piece.thickness = slabThickness;
            piece.bodyDepth = bodyDepth;
            piece.isFloating = def.IsFloat;
            piece.isNode = def.IsNode;
            piece.Apply();
            _result.Platforms.Add(piece);

            if (decorate)
            {
                DecorateRoof(piece, def);
            }
        }

        void DecorateRoof(PlatformPiece piece, PlatformDef def)
        {
            if (art == null) return;
            Transform root = piece.transform;

            // 楼体：三排霓虹窗，越往下越稀
            if (!def.IsFloat)
            {
                int slot = 0;
                for (float wx = -def.Width * 0.5f + 2.5f; wx < def.Width * 0.5f - 2f; wx += 3.2f, slot++)
                {
                    int hash = (slot * 7 + (int)def.X) % 11;
                    if (hash < 3) AddSprite(root, "Neon", new Vector3(wx, -2.4f, 0f), art.tileNeon, NeonTint, -5);
                    if (hash < 2 && def.Width > 12f) AddSprite(root, "Neon", new Vector3(wx, -5.6f, 0f), art.tileNeon, NeonTint, -5);
                    if (hash == 7 && def.Width > 16f) AddSprite(root, "Neon", new Vector3(wx, -8.8f, 0f), art.tileNeon, NeonTint, -5);
                }

                if (def.Width >= 14f)
                {
                    AddTiled(root, "Pipe", new Vector3(0f, -4.2f, 0f), new Vector2(def.Width, 0.5f), art.tilePipe, NeonTint, -5);
                }

                // 侧面霓虹招牌（2 倍整数缩放，避免像素粗细不均）
                if (slot > 0 && (int)def.X % 4 == 0 && art.banners != null && art.banners.Length > 0)
                {
                    Sprite banner = art.banners[Mathf.Abs((int)def.X) % art.banners.Length];
                    if (banner != null)
                    {
                        GameObject b = AddSprite(root, "Banner", new Vector3(def.Width * 0.5f - 2f, -6.5f, 0f), banner, Color.white, -4);
                        b.transform.localScale = new Vector3(2f, 2f, 1f);
                    }
                }

                // 天线
                if ((int)def.X % 6 == 3 && art.antenna != null)
                {
                    GameObject a = AddSprite(root, "Antenna", new Vector3(-def.Width * 0.5f + 2f, 5f, 0f), art.antenna, Color.white, -2);
                    a.transform.localScale = new Vector3(2f, 2f, 1f);
                }
            }

            if (def.IsNode && art.tilePipe != null)
            {
                AddTiled(root, "NodeGlow", new Vector3(0f, 0.06f, 0f), new Vector2(def.Width, 0.2f), art.tilePipe, NeonTint, 1);
            }
        }

        void SpawnDroneAbove(int platformIndex)
        {
            if (platformIndex < 0 || platformIndex >= _platforms.Count) return;
            PlatformDef p = _platforms[platformIndex];

            GameObject go = Spawn(dronePrefab, "Drone", new Vector3(p.X + p.Width * 0.5f, p.Y + 4.5f, 0f));
            go.layer = GameLayers.Enemy;
            FillEnemy(go, art != null ? art.droneFrames : null, 8f, 2, 4f, 2.2f, true);

            var ctrl = go.GetComponent<EnemyController>();
            if (ctrl != null) _result.Enemies.Add(ctrl);
        }

        void SpawnTurret(int platformIndex)
        {
            if (platformIndex < 0 || platformIndex >= _platforms.Count) return;
            PlatformDef p = _platforms[platformIndex];

            GameObject go = Spawn(turretPrefab, "Turret", new Vector3(p.X + p.Width * 0.5f, p.Y + 1.1f, 0f));
            go.layer = GameLayers.Enemy;
            FillEnemy(go, art != null ? art.turretFrames : null, 10f, 3, 0f, 0f, false);

            var ctrl = go.GetComponent<EnemyController>();
            if (ctrl != null) _result.Enemies.Add(ctrl);
        }

        void FillEnemy(GameObject go, Sprite[] frames, float fps, int hp, float patrolRange, float patrolSpeed, bool hover)
        {
            var col = go.GetComponent<BoxCollider2D>();
            if (col == null) col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = hover ? new Vector2(1.6f, 1.2f) : new Vector2(1.4f, 1.8f);

            Transform visualT = go.transform.Find("Visual");
            if (visualT == null)
            {
                var v = new GameObject("Visual");
                v.transform.SetParent(go.transform, false);
                visualT = v.transform;
            }

            if (visualT.GetComponent<SpriteRenderer>() == null) visualT.gameObject.AddComponent<SpriteRenderer>();

            var vis = visualT.GetComponent<EnemyVisual>();
            if (vis == null) vis = visualT.gameObject.AddComponent<EnemyVisual>();
            vis.SetFrames(frames, fps);

            var health = go.GetComponent<EnemyHealth>();
            if (health == null) health = go.AddComponent<EnemyHealth>();
            health.SetMaxHealth(hp);

            var ctrl = go.GetComponent<EnemyController>();
            if (ctrl == null) ctrl = go.AddComponent<EnemyController>();
            ctrl.patrolRange = patrolRange;
            ctrl.patrolSpeed = patrolSpeed;

            if (hover && go.GetComponent<HoverBob>() == null) go.AddComponent<HoverBob>();
        }

        void AddMarker(float x, float y)
        {
            GameObject go = Spawn(markerPrefab, "DashMarker", new Vector3(x, y, 0f));
            go.layer = GameLayers.Trigger;

            Transform arrowT = go.transform.Find("Arrow");
            if (arrowT == null)
            {
                AddSprite(go.transform, "Arrow", new Vector3(0f, 1.05f, 0f), art != null ? art.dashArrow : null, Color.white, 3);
            }

            var marker = go.GetComponent<DashMarker>();
            if (marker == null) marker = go.AddComponent<DashMarker>();
            marker.Configure(1.2f, 4.2f);
            _result.Markers.Add(marker);
        }

        void CreatePortal(float x, float y)
        {
            GameObject go = Spawn(portalPrefab, "Portal", new Vector3(x, y, 0f));
            go.layer = GameLayers.Trigger;
            Transform root = go.transform;

            if (art != null)
            {
                if (art.bigBanners != null && art.bigBanners.Length > 0 && root.Find("Banner") == null)
                {
                    GameObject b = AddSprite(root, "Banner", new Vector3(0f, 7f, 0f), art.bigBanners[0], Color.white, 2);
                    b.transform.localScale = new Vector3(2f, 2f, 1f);
                }
                if (art.openSign != null && root.Find("Open") == null)
                {
                    GameObject s = AddSprite(root, "Open", new Vector3(-5f, 3f, 0f), art.openSign, Color.white, 2);
                    s.transform.localScale = new Vector3(2f, 2f, 1f);
                }
                if (art.controlBox != null && root.Find("Box") == null)
                {
                    AddSprite(root, "Box", new Vector3(5f, 1.2f, 0f), art.controlBox, Color.white, 1);
                }
            }

            var portal = go.GetComponent<PortalExit>();
            if (portal == null) portal = go.AddComponent<PortalExit>();
            portal.Configure(3f, 8f);
        }

        // ---------------- 小工具 ----------------

        GameObject Spawn(GameObject prefab, string name, Vector3 position)
        {
            GameObject go;
            if (prefab != null)
            {
#if UNITY_EDITOR
                // 编辑器里用 PrefabUtility，保留和预制体的关联（场景里能 Revert / Apply 改动）
                go = UnityEditor.PrefabUtility.InstantiatePrefab(prefab, _root) as GameObject;
                if (go == null) go = Instantiate(prefab, position, Quaternion.identity, _root);
                else go.transform.position = position;
#else
                go = Instantiate(prefab, position, Quaternion.identity, _root);
#endif
                go.name = name;
            }
            else
            {
                go = new GameObject(name);
                go.transform.SetParent(_root, false);
                go.transform.position = position;
            }
            return go;
        }

        static GameObject AddSprite(Transform parent, string name, Vector3 localPos, Sprite sprite, Color tint, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = tint;
            sr.sortingOrder = order;
            return go;
        }

        static GameObject AddTiled(Transform parent, string name, Vector3 localPos, Vector2 size,
                                Sprite sprite, Color tint, int order)
        {
            if (sprite == null) return null;

            var go = AddSprite(parent, name, localPos, sprite, tint, order);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            sr.size = size;
            return go;
        }
    }
}
