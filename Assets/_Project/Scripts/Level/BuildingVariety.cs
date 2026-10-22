using UnityEngine;

namespace TiantaiDash
{
    /// <summary>
    /// 楼体立面变体：给每栋楼随机换一种外墙贴图 + 轻微色偏，再挂消防梯 / 管道 / 霓虹条。
    /// 只在运行时改 SpriteRenderer，不动烘焙数据，所以现有场景立刻见效。
    /// </summary>
    public class BuildingVariety : MonoBehaviour
    {
        public int seed = 7717;
        [Range(0f, 1f)] public float fireEscapeChance = 0.5f;
        [Range(0f, 1f)] public float pipeChance = 0.4f;
        [Range(0f, 1f)] public float neonChance = 0f;   // 不要墙上的霓虹条

        void Start()
        {
            var art = ArtLibrary.Instance;
            if (art == null) return;

            var walls = art.wallVariants;
            if (walls == null || walls.Length == 0) return;

            var rng = new System.Random(seed);
            var pieces = FindObjectsOfType<PlatformPiece>();

            // 场景里烘焙的霓虹横幅（Banner）也不要了
            var all = FindObjectsOfType<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == "Banner") all[i].gameObject.SetActive(false);
            }

            for (int i = 0; i < pieces.Length; i++)
            {
                var p = pieces[i];
                if (p == null || p.isFloating || p.body == null) continue;

                // 楼体向下一直延伸到屏幕外，别再让人看到"平台悬空"
                if (p.bodyDepth < 40f)
                {
                    p.bodyDepth = 46f + (float)rng.NextDouble() * 24f;
                    p.Apply();
                }

                // 悬浮平台：换成警示条纹金属台面（参考图那种）
                if (p.isFloating && art.slabHazard != null && p.slab != null)
                {
                    p.slab.sprite = art.slabHazard;
                    p.slab.drawMode = SpriteDrawMode.Tiled;
                    p.slab.tileMode = SpriteTileMode.Continuous;
                }
                else
                {
                    Sprite wall = walls[rng.Next(walls.Length)];
                    if (wall != null) p.body.sprite = wall;

                    // 一点点色偏：同一张贴图也能拉开冷暖层次
                    float t = 0.82f + (float)rng.NextDouble() * 0.36f;
                    p.body.color = new Color(t * 0.94f, t, t * 1.06f, 1f);
                }

                int order = p.body.sortingOrder;
                float halfW = p.width * 0.5f;

                if (rng.NextDouble() < fireEscapeChance)
                    Attach(p, art.fireEscape, -halfW + 1.9f, -4.2f, order + 1, false);
                if (rng.NextDouble() < pipeChance)
                    Attach(p, art.pipes, halfW - 1.1f, -3.4f, order + 1, false);
                if (rng.NextDouble() < neonChance)
                    Attach(p, art.neonStrip, -halfW + 0.6f, -3.4f, order + 1, true);
            }
        }

        void Attach(PlatformPiece p, Sprite sprite, float x, float y, int order, bool flicker)
        {
            if (sprite == null) return;

            var go = new GameObject("FacadeProp");
            go.transform.SetParent(p.transform, false);
            go.transform.localPosition = new Vector3(x, y, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;

            if (flicker)
            {
                var f = go.AddComponent<SignFlicker>();
                f.baseColor = Color.white;
                f.amplitude = 0.25f;
            }
        }
    }
}
