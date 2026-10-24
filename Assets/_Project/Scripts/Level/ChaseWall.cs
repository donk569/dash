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
            public float baseX;
            public float baseY;
            public float phase;
            public float amp;
            public float wobble;
        }

        static Sprite _unitSprite;
        readonly List<Slab> _slabs = new List<Slab>();
        float _time;

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
            // 由后到前：右侧（前沿）对齐、左侧逐层缩进 → 看起来是"一整堵墙 + 发光的推进面"，
            // 而不是几条并排的色条。颜色从暗紫到亮橙，纵深感靠颜色的明度差。
            AddSlab("Back", new Color32(24, 14, 40, 255), 1.00f, 1.00f, 0.00f, 0.00f);
            AddSlab("Body", new Color32(68, 24, 72, 255), 0.94f, 0.99f, 0.50f, 0.05f);
            AddSlab("Glow", new Color32(136, 42, 90, 255), 0.86f, 0.98f, 1.00f, 0.09f);
            AddSlab("Hot", new Color32(214, 82, 78, 255), 0.74f, 0.96f, 1.50f, 0.13f);
            AddSlab("Core", new Color32(255, 152, 86, 255), 0.58f, 0.93f, 2.00f, 0.17f);

            // 前沿碎块：几个小方块甩在最亮的边缘外，做出"正在崩"的毛边
            for (int i = 0; i < 9; i++)
            {
                float h = height * Random.Range(0.08f, 0.30f);
                var chunk = MakeSlab("Chunk", new Color32(255, 190, 116, 255),
                    width * Random.Range(0.05f, 0.13f), h);
                chunk.baseX = width * Random.Range(0.40f, 0.58f);
                chunk.baseY = Random.Range(-height * 0.44f, height * 0.44f);
                chunk.phase = Random.Range(0f, 6.28f);
                chunk.amp = Random.Range(0.3f, 1.0f);
                chunk.wobble = Random.Range(2.5f, 5f);
            }
        }

        void AddSlab(string name, Color color, float wScale, float hScale, float xOffset, float amp)
        {
            Slab s = MakeSlab(name, color, width * wScale, height * hScale);
            s.baseX = xOffset;
            s.baseY = 0f;
            s.phase = Random.Range(0f, 6.28f);
            s.amp = amp * height * 0.1f;
            s.wobble = 1.6f;
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
        }

        /// <summary>结束阶段：墙停在原地、整体淡出（不瞬间消失在玩家眼前）。</summary>
        public void FadeOut(float dt, float duration)
        {
            if (duration <= 0f) return;

            float a = Mathf.Clamp01(1f - Mathf.Clamp01(Time.time - _fadeStart) / duration);
            for (int i = 0; i < _slabs.Count; i++)
            {
                var sr = _slabs[i].t != null ? _slabs[i].t.GetComponent<SpriteRenderer>() : null;
                if (sr == null) continue;
                Color c = sr.color;
                c.a = a;
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
