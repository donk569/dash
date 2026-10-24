using System.Collections.Generic;
using UnityEngine;

namespace TiantaiDash
{
    /// <summary>
    /// 崩塌墙本体。**完全程序生成，不依赖任何素材**（避开"半透明色块在 URP 2D 渲染成实心"的老坑，
    /// 这里全部用不透明纯色竖条叠层次）。
    ///
    /// 结构从后到前 5 层：暗紫 → 紫红 → 橙红 → 亮橙，越靠前越窄越亮，
    /// 每层带不同的相位/振幅做轻微上下抖动，看起来像"整面墙在塌"。
    /// </summary>
    public class ChaseWall : MonoBehaviour
    {
        public float width = 13f;
        public float height = 46f;
        /// <summary>推进速度（由 ChaseZone 每帧写入）</summary>
        public float speed;

        class Slab
        {
            public Transform t;
            public SpriteRenderer sr;
            public float baseX;
            public float baseY;
            public float phase;
            public float amp;
            public float wobble;
        }

        class Mote
        {
            public Transform t;
            public float y;
            public float baseX;
            public float speed;
            public float phase;
            public float sway;
        }

        static Sprite _unitSprite;
        readonly List<Slab> _slabs = new List<Slab>();
        readonly List<Mote> _motes = new List<Mote>();
        Slab _core;
        SpriteRenderer _scanA;
        SpriteRenderer _scanB;
        float _time;

        // 赛博紫蓝配色：深靛蓝打底 → 亮蓝紫前沿 → 青白火花
        // （和项目色板同族：#6FE6FF 青 / #BA8CFF 紫，这里取更偏蓝的区间）
        static readonly Color32 CBack = new Color32(10, 14, 38, 255);      // 近黑蓝
        static readonly Color32 CBody = new Color32(22, 32, 90, 255);      // 深靛
        static readonly Color32 CGlow = new Color32(42, 58, 168, 255);     // 靛蓝
        static readonly Color32 CHot = new Color32(74, 104, 232, 255);    // 亮蓝紫
        static readonly Color32 CCore = new Color32(122, 148, 224, 255);   // 前沿亮边（不刺眼）
        static readonly Color32 CSpark = new Color32(143, 230, 255, 255);  // 青白火花
        static readonly Color32 CScan = new Color32(168, 220, 255, 255);   // 扫描线（偏青，对比强）
        static readonly Color32 CChunk = new Color32(184, 200, 255, 255);  // 前沿碎块

        /// <summary>1×1 世界单位的纯白 sprite，用 color 染色。</summary>
        static Sprite UnitSprite
        {
            get
            {
                if (_unitSprite != null) return _unitSprite;

                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                var px = new Color32[16];
                for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(px);
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.Apply();

                // PPU = 4 → 4px 贴图 = 1 世界单位，之后全靠 localScale 拉
                _unitSprite = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
                _unitSprite.name = "ChaseUnit";
                return _unitSprite;
            }
        }

        public static ChaseWall Create(Transform parent, Vector3 at, float width = 13f, float height = 46f)
        {
            var go = new GameObject("ChaseWall");
            go.transform.SetParent(parent, false);
            go.transform.position = at;

            var wall = go.AddComponent<ChaseWall>();
            wall.width = width;
            wall.height = height;
            wall.Build();
            return wall;
        }

        void Build()
        {
            // 赛博紫蓝：深靛 → 亮紫 → 近白紫。右侧（前沿）对齐、左侧逐层缩进，
            // 看起来是"一整堵墙 + 发光的推进面"，而不是几条并排的色条。
            AddSlab("Back", CBack, 1.00f, 1.00f, 0.00f, 0.00f);
            AddSlab("Body", CBody, 0.94f, 0.99f, 0.50f, 0.05f);
            AddSlab("Glow", CGlow, 0.86f, 0.98f, 1.00f, 0.09f);
            AddSlab("Hot", CHot, 0.70f, 0.96f, 1.50f, 0.13f);
            _core = AddSlab("Core", CCore, 0.42f, 0.86f, 2.00f, 0.19f);

            // 前沿碎块：小方块甩在最亮的边缘外，做"正在崩"的毛边（大小/位置都拉开差距才不像规则图案）
            for (int i = 0; i < 11; i++)
            {
                float h = height * Random.Range(0.05f, 0.27f);
                var chunk = MakeSlab("Chunk", CChunk, width * Random.Range(0.035f, 0.16f), h);
                chunk.baseX = width * Random.Range(0.43f, 0.63f);
                chunk.baseY = Random.Range(-height * 0.46f, height * 0.46f);
                chunk.phase = Random.Range(0f, 6.28f);
                chunk.amp = Random.Range(0.3f, 1.2f);
                chunk.wobble = Random.Range(2.5f, 6f);
            }

            // 扫描线：两条不同速度、不同方向的亮线扫过墙面（赛博感的主力）
            _scanA = MakeDecor("ScanA", CScan, 33);
            _scanA.transform.localScale = new Vector3(width * 0.96f, 0.34f, 1f);

            _scanB = MakeDecor("ScanB", new Color32(206, 186, 255, 255), 33);
            _scanB.transform.localScale = new Vector3(width * 0.82f, 0.18f, 1f);

            // 前沿光点：从墙里往上飘，越靠中间越亮越大
            for (int i = 0; i < 8; i++)
            {
                SpriteRenderer sr = MakeDecor("Mote" + i, CSpark, 34);
                sr.transform.localScale = Vector3.one * 0.4f;

                var m = new Mote();
                m.t = sr.transform;
                m.baseX = width * Random.Range(0.28f, 0.56f);
                m.y = Random.Range(-height * 0.5f, height * 0.5f);
                m.speed = Random.Range(3.5f, 9f);
                m.phase = Random.Range(0f, 6.28f);
                m.sway = Random.Range(0.6f, 1.6f);
                _motes.Add(m);
            }
        }

        /// <summary>纯装饰块（不参与 Slab 的抖动）。</summary>
        SpriteRenderer MakeDecor(string name, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = UnitSprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        Slab AddSlab(string name, Color color, float wScale, float hScale, float xOffset, float amp)
        {
            Slab s = MakeSlab(name, color, width * wScale, height * hScale);
            s.baseX = xOffset;
            s.baseY = 0f;
            s.phase = Random.Range(0f, 6.28f);
            s.amp = amp * height * 0.1f;
            s.wobble = 1.6f;
            return s;
        }

        Slab MakeSlab(string name, Color color, float w, float h)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(w, h, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = UnitSprite;
            sr.color = color;
            sr.sortingOrder = 30;   // 压在角色(20)/场景之上

            var s = new Slab();
            s.t = go.transform;
            s.sr = sr;
            _slabs.Add(s);
            return s;
        }

        void Update()
        {
            _time += Time.deltaTime;

            for (int i = 0; i < _slabs.Count; i++)
            {
                Slab s = _slabs[i];
                if (s.t == null) continue;

                // 每层相位不同 → 整体像在流动，而不是一块板在平移
                float y = s.baseY + Mathf.Sin(_time * s.wobble + s.phase) * s.amp;
                s.t.localPosition = new Vector3(s.baseX, y, 0f);
            }

            // Core 呼吸：亮度脉动，看起来像"能量墙还在供电"。alpha 跟随淡出，不要覆盖掉。
            if (_core != null && _core.sr != null)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(_time * 5.5f);
                Color c = Color.Lerp(new Color(0.34f, 0.44f, 0.86f), new Color(0.56f, 0.68f, 0.98f), k);
                c.a = _core.sr.color.a;
                _core.sr.color = c;
            }

            // 两条扫描线，速度/方向不同，扫过整面墙
            float span = height * 1.15f;
            if (_scanA != null)
            {
                float y = Mathf.Repeat(_time * 16f, span) - span * 0.5f;
                _scanA.transform.localPosition = new Vector3(width * 0.01f, y, 0f);
            }
            if (_scanB != null)
            {
                float y = Mathf.Repeat(-_time * 9.5f, span) - span * 0.5f;
                _scanB.transform.localPosition = new Vector3(width * 0.07f, y, 0f);
            }

            // 前沿光点：往上飘，越靠中间越大越亮；用缩放做明暗（避开半透明色块在 URP 2D 渲染成实心的坑）
            for (int i = 0; i < _motes.Count; i++)
            {
                Mote m = _motes[i];
                if (m.t == null) continue;

                m.y += m.speed * Time.deltaTime;
                if (m.y > height * 0.5f) m.y = -height * 0.5f;

                float x = m.baseX + Mathf.Sin(_time * m.sway + m.phase) * 0.7f;
                m.t.localPosition = new Vector3(x, m.y, 0f);

                float fade = Mathf.Clamp01(1f - Mathf.Abs(m.y) / (height * 0.5f));
                float s = (0.16f + 0.62f * fade) * _fadeAlpha;
                m.t.localScale = new Vector3(s, s, 1f);
            }

            if (_scanA != null)
                _scanA.transform.localScale = new Vector3(width * 0.96f, 0.34f * _fadeAlpha, 1f);
            if (_scanB != null)
                _scanB.transform.localScale = new Vector3(width * 0.82f, 0.18f * _fadeAlpha, 1f);
        }

        float _fadeAlpha = 1f;

        /// <summary>结束阶段：墙停在原地、整体淡出（不瞬间消失在玩家眼前）。</summary>
        public void FadeOut(float dt, float duration)
        {
            if (duration <= 0f) return;

            _fadeAlpha = Mathf.Clamp01(1f - Mathf.Clamp01(Time.time - _fadeStart) / duration);

            // Slab 用 alpha 淡出（Core 的 alpha 每帧会跟着保住，不会被呼吸覆盖）
            for (int i = 0; i < _slabs.Count; i++)
            {
                SpriteRenderer sr = _slabs[i].sr;
                if (sr == null) continue;
                Color c = sr.color;
                c.a = _fadeAlpha;
                sr.color = c;
            }
        }

        float _fadeStart;
        public void BeginFade()
        {
            _fadeStart = Time.time;
        }
    }
}
