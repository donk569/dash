using System.Collections.Generic;
using UnityEngine;

namespace TiantaiDash
{
    /// <summary>
    /// 追逐段的推进屏障：**一排竖直激光柱**（赛博光柱，参考信标那种垂直光束）。
    ///
    /// 和上一版"崩塌墙"的区别：不再是实心板，而是一列间隔排列的光柱，
    /// 每根柱 = 外晕（贴图 alpha 渐变）+ 中层 + 高亮内芯 + 一段上下流动的能量段。
    /// 高度默认 90（屏幕只有 33.75 高），所以永远看不到柱子的上下端点。
    ///
    /// 只负责视觉与自身动画，推进/判定全在 <see cref="ChaseZone"/>。
    /// </summary>
    public class ChaseWall : MonoBehaviour
    {
        public float width = 13f;
        public float height = 90f;

        [Header("柱体")]
        [Tooltip("排几根激光柱")]
        public int beamCount = 8;
        [Tooltip("单根光柱的外晕宽度")]
        public float glowWidth = 2.6f;
        [Tooltip("内芯宽度")]
        public float coreWidth = 0.24f;
        [Tooltip("能量段上下流动的速度")]
        public float flowSpeed = 9f;

        class Beam
        {
            public Transform root;
            public SpriteRenderer core;
            public Transform pulse;
            public float x;
            public float phase;
        }

        static Sprite _unit;
        static Sprite _glow;

        readonly List<Beam> _beams = new List<Beam>();
        float _time;
        float _fadeAlpha = 1f;
        float _fadeStart;

        // 赛博紫蓝：外晕偏紫、内芯淡紫白，整体比纯蓝更"赛博"
        static readonly Color32 CGlow = new Color32(122, 106, 255, 255);
        static readonly Color32 CMid = new Color32(96, 82, 244, 255);
        static readonly Color32 CCore = new Color32(202, 186, 255, 255);
        static readonly Color32 CPulse = new Color32(234, 226, 255, 255);

        /// <summary>1×1 世界单位的纯白方块（PPU=4）。</summary>
        static Sprite Unit
        {
            get
            {
                if (_unit != null) return _unit;

                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                var px = new Color32[16];
                for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(px);
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.Apply();

                _unit = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
                _unit.name = "BeamUnit";
                return _unit;
            }
        }

        /// <summary>
        /// 外晕：横向 alpha 渐变（中心 1 → 边缘 0），6×1 世界单位。
        /// **alpha 烘在贴图里**，不靠 SpriteRenderer.color.a —— 那个在 URP 2D 会被渲染成实心（项目老坑）。
        /// </summary>
        static Sprite Glow
        {
            get
            {
                if (_glow != null) return _glow;

                const int W = 24;
                const int H = 4;
                var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);

                for (int x = 0; x < W; x++)
                {
                    float t = Mathf.Abs(x - (W - 1) * 0.5f) / ((W - 1) * 0.5f);
                    float a = Mathf.Pow(Mathf.Clamp01(1f - t), 2.1f);
                    var c = new Color(1f, 1f, 1f, a);
                    for (int y = 0; y < H; y++) tex.SetPixel(x, y, c);
                }

                tex.filterMode = FilterMode.Bilinear;   // 光晕允许柔和，这里不追求像素阶梯
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.Apply();

                _glow = Sprite.Create(tex, new Rect(0f, 0f, W, H), new Vector2(0.5f, 0.5f), H);
                _glow.name = "BeamGlow";
                return _glow;
            }
        }

        public static ChaseWall Create(Transform parent, Vector3 at, float width = 13f, float height = 90f)
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
            int count = Mathf.Max(1, beamCount);
            float spacing = width / count;

            for (int i = 0; i < count; i++)
            {
                float x = -width * 0.5f + spacing * (i + 0.5f);
                _beams.Add(MakeBeam(x, i));
            }
        }

        Beam MakeBeam(float x, int index)
        {
            var root = new GameObject("Beam" + index);
            root.transform.SetParent(transform, false);
            root.transform.localPosition = new Vector3(x, 0f, 0f);

            // 外晕
            var glow = NewPart(root.transform, "Glow", CGlow, 28);
            glow.transform.localScale = new Vector3(glowWidth / 6f, height, 1f);

            // 中层
            var mid = NewPart(root.transform, "Mid", CMid, 29);
            mid.transform.localScale = new Vector3(0.62f, height, 1f);

            // 内芯（最亮）
            var core = NewPart(root.transform, "Core", CCore, 30);
            core.transform.localScale = new Vector3(coreWidth, height, 1f);

            // 流动能量段：一段短的高亮沿着柱子上下跑
            var pulse = NewPart(root.transform, "Pulse", CPulse, 31);
            pulse.transform.localScale = new Vector3(coreWidth * 2.1f, 4.5f, 1f);

            var beam = new Beam();
            beam.root = root.transform;
            beam.core = core;
            beam.pulse = pulse.transform;
            beam.x = x;
            beam.phase = index * 0.9f + Random.Range(0f, 1.4f);
            return beam;
        }

        SpriteRenderer NewPart(Transform parent, string name, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = name == "Glow" ? Glow : Unit;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        void Update()
        {
            _time += Time.deltaTime;

            float span = height;   // 能量段跑完整根柱子

            for (int i = 0; i < _beams.Count; i++)
            {
                Beam b = _beams[i];
                if (b.root == null) continue;

                // 能量段：从下往上循环
                float y = Mathf.Repeat(_time * flowSpeed + b.phase * 12f, span) - span * 0.5f;
                if (b.pulse != null)
                {
                    b.pulse.localPosition = new Vector3(0f, y, 0f);
                    b.pulse.localScale = new Vector3(coreWidth * 2.1f, 4.5f * _fadeAlpha, 1f);
                }

                // 每根柱子的内芯亮度轻微错相脉动，整片看起来在"呼吸"
                if (b.core != null)
                {
                    float k = 0.5f + 0.5f * Mathf.Sin(_time * 3.4f + b.phase);
                    Color c = Color.Lerp(new Color(0.66f, 0.60f, 1f), new Color(0.88f, 0.85f, 1f), k);
                    c.a = _fadeAlpha;
                    b.core.color = c;
                }
            }
        }

        public void BeginFade()
        {
            _fadeStart = Time.time;
        }

        /// <summary>结束阶段：整体淡出（贴图自带 alpha 的那层也跟着变淡）。</summary>
        public void FadeOut(float dt, float duration)
        {
            if (duration <= 0f) return;
            _fadeAlpha = Mathf.Clamp01(1f - Mathf.Clamp01(Time.time - _fadeStart) / duration);

            // 外晕/中层是纯色 + 贴图 alpha，这里用 color.a 配合，能正常变淡
            var renderers = GetComponentsInChildren<SpriteRenderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer sr = renderers[i];
                if (sr == null) continue;

                Color c = sr.color;
                c.a = _fadeAlpha;
                sr.color = c;
            }
        }
    }
}
