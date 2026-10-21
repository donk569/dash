using System.Collections.Generic;
using UnityEngine;

namespace TiantaiDash
{
    /// <summary>
    /// 视差背景。天空挂在相机子节点；三层城市剪影是场景里的真实对象。
    ///
    /// 每层由若干个 **Simple 模式的并排小 sprite** 拼成（不是 Tiled）——
    /// Tiled 在 size 远超贴图尺寸时平铺会失效（后半段没背景就是这个原因之一）。
    /// 这里改成"几块并排 + 父节点按平铺周期取模滚动"，贴图横向无缝，所以整格跳跃看不出来。
    /// </summary>
    public class ParallaxBackdrop : MonoBehaviour
    {
        [Header("跟随的相机（留空自动取 Main Camera）")]
        public Camera cam;

        [Header("层引用（场景里直接拖）")]
        public Transform skyLayer;
        public Transform farLayer;
        public Transform midLayer;
        public Transform nearLayer;

        [Header("视差因子：0 = 完全跟相机不动，1 = 跟场景同速")]
        public float farFactor = 0.10f;
        public float midFactor = 0.22f;
        public float nearFactor = 0.40f;

        [Header("自动横向跟随（关闭后位置由你手动控制）")]
        public bool autoFollow = true;

        [Header("纵向：背景贴着相机走，不然爬到高处背景就掉出画面了")]
        public bool followCameraY = true;
        [Tooltip("相对相机中心的高度，负数 = 在画面下方")]
        public float farYOffset = -15.5f;
        public float midYOffset = -14.5f;
        public float nearYOffset = -13.5f;

        class Layer
        {
            public Transform Tr;
            public float Factor;
            public float TileWidth;
            public float YOffset;
        }

        readonly List<Layer> _layers = new List<Layer>();

        void Awake()
        {
            if (cam == null) cam = Camera.main;
            Rebuild();
        }

        /// <summary>结构改过之后调用一次，重新读每层的平铺周期。</summary>
        public void Rebuild()
        {
            _layers.Clear();
            Add(farLayer, farFactor, farYOffset);
            Add(midLayer, midFactor, midYOffset);
            Add(nearLayer, nearFactor, nearYOffset);
        }

        void Add(Transform t, float factor, float yOffset)
        {
            if (t == null) return;

            // 平铺周期 = 第一个和第二个子块的间距；只有一个子块时退化成贴图宽度
            float tileWidth = 30f;
            if (t.childCount >= 2)
            {
                tileWidth = Mathf.Abs(t.GetChild(1).localPosition.x - t.GetChild(0).localPosition.x);
            }
            else if (t.childCount == 1)
            {
                var sr = t.GetChild(0).GetComponent<SpriteRenderer>();
                if (sr != null && sr.sprite != null)
                    tileWidth = sr.sprite.rect.width / sr.sprite.pixelsPerUnit;
            }

            _layers.Add(new Layer
            {
                Tr = t,
                Factor = factor,
                TileWidth = Mathf.Max(1f, tileWidth),
                YOffset = yOffset,
            });
        }

        void LateUpdate()
        {
            if (!autoFollow || cam == null) return;

            float camX = cam.transform.position.x;
            for (int i = 0; i < _layers.Count; i++)
            {
                Layer l = _layers[i];

                // 相对相机的"落后量"按平铺周期取模 → 层永远落在相机附近一格之内
                float scroll = camX * (1f - l.Factor);
                float x = camX - Mathf.Repeat(scroll, l.TileWidth);

                Vector3 p = l.Tr.position;
                float y = followCameraY ? cam.transform.position.y + l.YOffset : p.y;
                l.Tr.position = new Vector3(x, y, p.z);
            }
        }

        /// <summary>给编辑器工具用：一次性把层铺到相机起始处。</summary>
        public void SnapToCamera(float camX)
        {
            if (_layers.Count == 0) Rebuild();
            LateUpdate();
        }

        void OnValidate()
        {
            if (!Application.isPlaying) Rebuild();
        }
    }
}
