using UnityEngine;

namespace TiantaiDash
{
    /// <summary>
    /// 屋顶 / 悬浮平台。只调 <see cref="width"/> 和 <see cref="thickness"/>，碰撞体与平铺贴图自动同步，
    /// 不用手动去改 BoxCollider 和 SpriteRenderer.size（那是这里最容易出错的地方）。
    /// 在编辑器里（非播放态）改数值也会立刻生效。
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(BoxCollider2D))]
    public class PlatformPiece : MonoBehaviour
    {
        [Header("尺寸")]
        [Tooltip("平台宽度（世界单位，1 = 1 格 = 16px）")]
        public float width = 12f;
        [Tooltip("地表厚度")]
        public float thickness = 1f;
        [Tooltip("楼体向下延伸的深度；悬浮平台会关掉楼体")]
        public float bodyDepth = 14f;

        [Header("类型")]
        [Tooltip("悬浮平台：不画楼体，只画 1 格厚的地表")]
        public bool isFloating;
        [Tooltip("增益节点平台：地表换成暖色调 + 顶部霓虹线")]
        public bool isNode;

        [Header("子物体引用（留空按名字自动找）")]
        public BoxCollider2D solid;
        public SpriteRenderer slab;
        public SpriteRenderer body;

        static readonly Color RoofTint = Color.white;
        static readonly Color RoofTintNode = new Color(1f, 0.94f, 0.66f);

        void Reset()
        {
            solid = GetComponent<BoxCollider2D>();
            Apply();
        }

        void OnValidate()
        {
            if (solid == null) solid = GetComponent<BoxCollider2D>();
            ResolveChildren();
            // 不能在 OnValidate 里直接改 SpriteRenderer.size（Unity 会报
            // "SendMessage cannot be called during OnValidate/Awake"）。
            // 一律延迟到下一次编辑器更新，播放进场的 OnValidate 也不会再刷警告。
            DelayedApply();
        }

        void DelayedApply()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= Apply;
            UnityEditor.EditorApplication.delayCall += Apply;
#else
            Apply();
#endif
        }

        void Awake()
        {
            ResolveChildren();
            // 这里刻意不调 Apply()：尺寸在烘焙时就写进序列化数据了，
            // 运行时再改 SpriteRenderer.size 会触发 "SendMessage cannot be called during Awake"。
            // 需要动态改尺寸时，改完 width 手动调一次 Apply() 即可。
        }

        void ResolveChildren()
        {
            if (solid == null) solid = GetComponent<BoxCollider2D>();
            if (slab == null)
            {
                Transform t = transform.Find("Slab");
                if (t != null) slab = t.GetComponent<SpriteRenderer>();
            }
            if (body == null)
            {
                Transform t = transform.Find("Body");
                if (t != null) body = t.GetComponent<SpriteRenderer>();
            }
        }

        /// <summary>把 width / thickness / bodyDepth 同步到碰撞体与贴图。</summary>
        public void Apply()
        {
            width = Mathf.Max(0.5f, width);
            thickness = Mathf.Max(0.1f, thickness);
            bodyDepth = Mathf.Max(0f, bodyDepth);

            if (solid != null)
            {
                solid.size = new Vector2(width, thickness);
                solid.offset = new Vector2(0f, -thickness * 0.5f);
            }

            if (slab != null)
            {
                slab.drawMode = SpriteDrawMode.Tiled;
                slab.tileMode = SpriteTileMode.Continuous;
                slab.size = new Vector2(width, thickness);
                slab.transform.localPosition = new Vector3(0f, -thickness * 0.5f, 0f);
                slab.color = isNode ? RoofTintNode : RoofTint;
            }

            if (body != null)
            {
                body.gameObject.SetActive(!isFloating);
                if (!isFloating)
                {
                    body.drawMode = SpriteDrawMode.Tiled;
                    body.tileMode = SpriteTileMode.Continuous;
                    body.size = new Vector2(width, bodyDepth);
                    body.transform.localPosition = new Vector3(0f, -thickness - bodyDepth * 0.5f, 0f);
                }
            }
        }

        /// <summary>给贴图的 Z 层做整体微调（烘焙时用）。</summary>
        public void SetSortingOrder(int slabOrder, int bodyOrder)
        {
            if (slab != null) slab.sortingOrder = slabOrder;
            if (body != null) body.sortingOrder = bodyOrder;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.35f);
            Vector3 center = transform.position + new Vector3(0f, -thickness * 0.5f, 0f);
            Gizmos.DrawWireCube(center, new Vector3(width, thickness, 0.2f));
        }
#endif
    }
}
