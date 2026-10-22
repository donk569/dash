using UnityEngine;

namespace TiantaiDash
{
    /// <summary>
    /// 屋顶生活（v2）：平台 = 屋顶，所以装饰分两处 ——
    /// 1. **外墙**：窗户 + 窗里的 NPC 头肩 + 挂墙霓虹招牌；
    /// 2. **屋顶面**：晾衣绳 / 空调 / 水塔 / 盆栽 / 桌子 / 猫（不再放招牌和站立 NPC）。
    /// 种子固定，每次重开布局一致。
    /// </summary>
    public class RoofLife : MonoBehaviour
    {
        [Tooltip("固定种子：保证每次重开装饰位置一致")]
        public int seed = 20261022;
        [Range(0f, 1f)] public float emptyChance = 0.12f;
        [Range(0f, 1f)] public float windowPersonChance = 0.45f;
        [Range(0f, 1f)] public float wallSignChance = 0.4f;
        [Range(0f, 1f)] public float roofPropChance = 0.6f;
        public float scale = 0.9f;

        Sprite[] _props, _signs, _windowNpc;
        Sprite _window;

        void Start()
        {
            var art = ArtLibrary.Instance;
            if (art == null) return;

            _props = art.roofProps;
            _signs = art.roofSigns;
            _window = art.windowProp;
            _windowNpc = art.windowNpc;

            var rng = new System.Random(seed);
            var pieces = FindObjectsOfType<PlatformPiece>();

            for (int i = 0; i < pieces.Length; i++)
            {
                var p = pieces[i];
                if (p == null || p.isFloating || p.width < 5f) continue;
                if (rng.NextDouble() < emptyChance) continue;

                int baseOrder = p.body != null ? p.body.sortingOrder : (p.slab != null ? p.slab.sortingOrder - 1 : 0);
                float halfW = Mathf.Max(0.6f, p.width * 0.5f - 1.4f);

                // ---- 外墙：1~2 扇窗（可能有人在窗里）
                int windows = 1 + rng.Next(2);
                for (int k = 0; k < windows; k++)
                {
                    float x = Mathf.Lerp(-halfW, halfW, (float)rng.NextDouble());
                    float y = -2.6f - (float)rng.NextDouble() * 2.2f;
                    SpawnWindow(p, x, y, baseOrder, rng);
                }

                // ---- 外墙：挂墙招牌（不放在屋顶上）
                if (_signs != null && _signs.Length > 0 && rng.NextDouble() < wallSignChance)
                {
                    float x = Mathf.Lerp(-halfW, halfW, (float)rng.NextDouble());
                    SpawnWallSign(p, x, -3.4f - (float)rng.NextDouble(), baseOrder, rng);
                }

                // ---- 屋顶面：小物件
                if (_props != null && _props.Length > 0 && rng.NextDouble() < roofPropChance)
                {
                    float x = Mathf.Lerp(-halfW, halfW, (float)rng.NextDouble());
                    SpawnRoofProp(p, x, rng);
                }
            }
        }

        void SpawnWindow(PlatformPiece p, float x, float y, int baseOrder, System.Random rng)
        {
            if (_window == null) return;

            var go = new GameObject("WallWindow");
            go.transform.SetParent(p.transform, false);
            go.transform.localScale = Vector3.one * scale;
            go.transform.localPosition = new Vector3(x, y, 0f);

            // 窗里的人（在窗框后面，透过玻璃看得出来）
            if (_windowNpc != null && _windowNpc.Length > 0 && rng.NextDouble() < windowPersonChance)
            {
                Sprite head = _windowNpc[rng.Next(_windowNpc.Length)];
                if (head != null)
                {
                    var person = new GameObject("WindowPerson");
                    person.transform.SetParent(go.transform, false);
                    person.transform.localPosition = new Vector3(0f, -0.02f, 0f);
                    var psr = person.AddComponent<SpriteRenderer>();
                    psr.sprite = head;
                    psr.sortingOrder = baseOrder + 1;
                    psr.flipX = rng.NextDouble() < 0.5;
                }
            }

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _window;
            sr.sortingOrder = baseOrder + 2;   // 窗框压在人物前面
        }

        void SpawnWallSign(PlatformPiece p, float x, float y, int baseOrder, System.Random rng)
        {
            Sprite sprite = _signs[rng.Next(_signs.Length)];
            if (sprite == null) return;

            var go = new GameObject("WallSign");
            go.transform.SetParent(p.transform, false);
            go.transform.localScale = Vector3.one * scale;
            go.transform.localPosition = new Vector3(x, y, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = baseOrder + 2;

            var glow = go.AddComponent<SignFlicker>();
            glow.baseColor = Color.white;
        }

        void SpawnRoofProp(PlatformPiece p, float x, System.Random rng)
        {
            Sprite sprite = _props[rng.Next(_props.Length)];
            if (sprite == null) return;

            var go = new GameObject("RoofProp");
            go.transform.SetParent(p.transform, false);
            go.transform.localScale = Vector3.one * scale;
            go.transform.localPosition = new Vector3(x, sprite.bounds.size.y * scale * 0.5f + 0.02f, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = (p.slab != null ? p.slab.sortingOrder : 0) + 2;

            // 猫会跑：玩家靠近就窜走
            if (sprite.name.StartsWith("prop_cat"))
                go.AddComponent<CatFlee>();
        }
    }
}
