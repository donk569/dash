using UnityEngine;

namespace TiantaiDash
{
    /// <summary>
    /// 接地阴影：每帧向下打一条射线找到脚下地面，把一枚椭圆软影贴上去。
    /// 离地越高 → 越小越淡；找不到地面 → 直接隐藏。
    ///
    /// 贴图程序生成（32×8 椭圆径向渐变），不走素材库 —— 这张图纯黑带 alpha，
    /// 用代码生成比放进 ArtLibrary 更省事。
    /// </summary>
    public class GroundShadow : MonoBehaviour
    {
        [Header("探测")]
        [Tooltip("最远探多深；超过就认为悬空")]
        public float maxDistance = 10f;

        [Header("表现")]
        [Tooltip("贴地时的影子宽度")]
        public float baseWidth = 1.9f;
        [Tooltip("贴地时的不透明度")]
        [Range(0f, 1f)] public float baseAlpha = 0.45f;
        [Tooltip("离地多高时开始明显变淡")]
        public float fadeHeight = 4f;
        [Tooltip("离地多高时完全消失（比 fadeHeight 大）")]
        public float vanishHeight = 6f;
        [Tooltip("冲刺时横向拉长的倍率")]
        public float dashStretch = 1.6f;

        static Sprite _sprite;

        SpriteRenderer _sr;
        Collider2D _col;
        DashController _dash;

        /// <summary>32×8 的椭圆径向渐变（中心不透明 → 边缘透明）。</summary>
        static Sprite ShadowSprite
        {
            get
            {
                if (_sprite != null) return _sprite;

                const int W = 32;
                const int H = 8;
                var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);

                for (int y = 0; y < H; y++)
                {
                    for (int x = 0; x < W; x++)
                    {
                        float nx = ((x + 0.5f) / W) * 2f - 1f;
                        float ny = ((y + 0.5f) / H) * 2f - 1f;
                        float d = Mathf.Sqrt(nx * nx + ny * ny);
                        float a = Mathf.Clamp01(1f - d);
                        a = Mathf.Pow(a, 0.75f);
                        tex.SetPixel(x, y, new Color(0f, 0f, 0f, a));
                    }
                }

                tex.filterMode = FilterMode.Bilinear;   // 影子要柔，这里不追求像素阶梯
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.Apply();

                // PPU = H → 贴图高 1 世界单位，宽 = 4
                _sprite = Sprite.Create(tex, new Rect(0f, 0f, W, H), new Vector2(0.5f, 0.5f), H);
                _sprite.name = "GroundShadow";
                return _sprite;
            }
        }

        void Awake()
        {
            _col = GetComponent<Collider2D>();
            if (_col == null) _col = GetComponentInChildren<Collider2D>();
            _dash = GetComponent<DashController>();
            if (_dash == null) _dash = GetComponentInParent<DashController>();

            var go = new GameObject("Shadow");
            go.transform.SetParent(transform.parent, false);

            _sr = go.AddComponent<SpriteRenderer>();
            _sr.sprite = ShadowSprite;
            _sr.color = new Color(0f, 0f, 0f, 0f);
            _sr.sortingOrder = 5;   // 压在平台(0)之上、角色(20)之下
        }

        void LateUpdate()
        {
            if (_sr == null) return;

            var gm = GameManager.Instance;
            if (gm != null && !gm.IsPlaying && !gm.IsTerminal)
            {
                _sr.enabled = false;
                return;
            }

            float footY = _col != null ? _col.bounds.min.y : transform.position.y;
            float x = transform.position.x;

            // 射线起点取"角色中心 + 0.6"，不要用 collider 底边：
            // 玩家贴地站立时底边常常已经陷进平台里，从底边往下打会直接穿过地面，永远打不到。
            float originY = Mathf.Max(footY, transform.position.y) + 0.6f;

            RaycastHit2D hit = Physics2D.Raycast(
                new Vector2(x, originY), Vector2.down,
                maxDistance + 2f, GameLayers.GroundMask);

            if (hit.collider == null)
            {
                _sr.enabled = false;
                return;
            }

            float height = Mathf.Max(0f, footY - hit.point.y);
            if (height >= vanishHeight)
            {
                _sr.enabled = false;
                return;
            }

            _sr.enabled = true;
            _sr.transform.position = new Vector3(x, hit.point.y + 0.04f, 0f);

            // 高度 → 尺寸 / 透明度
            float t = Mathf.Clamp01(height / Mathf.Max(0.01f, vanishHeight));
            float w = baseWidth * Mathf.Lerp(1f, 0.55f, t);
            float h = Mathf.Lerp(0.55f, 0.30f, t);
            float a = baseAlpha * Mathf.Lerp(1f, 0.10f, Mathf.Clamp01(height / Mathf.Max(0.01f, fadeHeight)));

            // 冲刺时横向拉长，像速度拖影
            if (_dash != null && _dash.IsDashing) w *= dashStretch;

            _sr.transform.localScale = new Vector3(w / 4f, h, 1f);
            _sr.color = new Color(0f, 0f, 0f, a);
        }

        void OnDestroy()
        {
            if (_sr != null) Destroy(_sr.gameObject);
        }
    }
}
