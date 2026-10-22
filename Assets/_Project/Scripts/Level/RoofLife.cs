using UnityEngine;

namespace TiantaiDash
{
    /// <summary>
    /// 屋顶生活：在已有屋顶上程序化摆装饰（晾衣绳 / 空调 / 水桶 / 盆栽 / 桌子 / 猫 / 霓虹招牌）
    /// 和会循环 idle 的 NPC —— 不用重烘焙关卡，种子固定所以每次布局一致。
    /// </summary>
    public class RoofLife : MonoBehaviour
    {
        [Tooltip("固定种子：保证每次重开装饰位置一致")]
        public int seed = 20261022;
        [Tooltip("每个屋顶不摆东西的概率")]
        [Range(0f, 1f)] public float emptyChance = 0.2f;
        [Tooltip("出现 NPC 的概率")]
        [Range(0f, 1f)] public float npcChance = 0.35f;
        [Tooltip("装饰和 NPC 的整体缩放")]
        public float scale = 0.8f;

        Sprite[] _props, _signs, _npcA, _npcB;

        void Start()
        {
            var art = ArtLibrary.Instance;
            if (art == null) return;

            _props = art.roofProps;
            _signs = art.roofSigns;
            _npcA = art.npcBiker;
            _npcB = art.npcCyborg;

            var rng = new System.Random(seed);
            var pieces = FindObjectsOfType<PlatformPiece>();

            for (int i = 0; i < pieces.Length; i++)
            {
                var p = pieces[i];
                if (p == null || p.isFloating || p.width < 7f) continue;
                if (rng.NextDouble() < emptyChance) continue;

                // 固定槽位 + 随机取用：同一屋顶的东西不会叠在一起
                float[] slots = { -0.62f, -0.2f, 0.2f, 0.62f };
                for (int s = slots.Length - 1; s > 0; s--)
                {
                    int j = rng.Next(s + 1);
                    float tmp = slots[s]; slots[s] = slots[j]; slots[j] = tmp;
                }

                float halfW = Mathf.Max(0f, p.width * 0.5f - 2f);
                int used = 0;

                int props = 1 + rng.Next(2);
                for (int k = 0; k < props && used < slots.Length; k++)
                    SpawnProp(p, rng, slots[used++] * halfW);

                if (used < slots.Length && rng.NextDouble() < npcChance)
                    SpawnNpc(p, rng, slots[used++] * halfW);
            }
        }

        void SpawnProp(PlatformPiece p, System.Random rng, float x)
        {
            bool useSign = _signs != null && _signs.Length > 0 && rng.NextDouble() < 0.3;
            Sprite[] pool = useSign ? _signs : _props;
            if (pool == null || pool.Length == 0) return;

            Sprite sprite = pool[rng.Next(pool.Length)];
            if (sprite == null) return;

            var go = new GameObject(useSign ? "RoofSign" : "RoofProp");
            go.transform.SetParent(p.transform, false);
            go.transform.localScale = Vector3.one * scale;

            float h = sprite.bounds.size.y * scale;
            go.transform.localPosition = new Vector3(x, h * 0.5f + 0.02f, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = (p.slab != null ? p.slab.sortingOrder : 0) + 2;

            // 招牌/Sign 都会呼吸式闪一下，像真正的霓虹灯
            if (useSign)
            {
                var glow = go.AddComponent<SignFlicker>();
                glow.baseColor = Color.white;
            }
        }

        void SpawnNpc(PlatformPiece p, System.Random rng, float x)
        {
            Sprite[] frames = rng.NextDouble() < 0.5 ? _npcA : _npcB;
            if (frames == null || frames.Length == 0) return;

            var go = new GameObject("RoofNpc");
            go.transform.SetParent(p.transform, false);
            go.transform.localScale = Vector3.one * (scale * 0.95f);
            go.transform.localPosition = new Vector3(x, 0.02f, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = frames[0];
            sr.sortingOrder = (p.slab != null ? p.slab.sortingOrder : 0) + 3;

            var npc = go.AddComponent<RoofNpc>();
            npc.frames = frames;
            npc.faceLeft = rng.NextDouble() < 0.5;
            npc.fps = 2.2f + (float)rng.NextDouble() * 1.6f;
        }
    }
}
