using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TiantaiDash
{
    /// <summary>
    /// 给交互物挂一盏 2D 点光（印记 / 商店 / 终点）。
    /// 只做"半径 + 强度 + 脉冲"三件事，颜色由挂载方决定。
    /// </summary>
    public class PointGlow : MonoBehaviour
    {
        public Color color = new Color(0.44f, 0.90f, 1f, 1f);
        public float outerRadius = 5f;
        public float innerRadius = 0.8f;
        public float intensity = 1.2f;

        [Header("脉冲（0 = 常亮）")]
        public float pulseSpeed = 1f;
        public float pulseMin = 0.8f;
        public float pulseMax = 1.4f;

        Light2D _light;
        float _phase;

        void Awake()
        {
            _phase = Random.Range(0f, 6.28f);

            _light = gameObject.GetComponent<Light2D>();
            if (_light == null) _light = gameObject.AddComponent<Light2D>();

            _light.lightType = Light2D.LightType.Point;
            _light.shadowsEnabled = false;
        }

        /// <summary>
        /// 参数放在 Start 应用：这个组件是 AddComponent 出来的，
        /// 调用方拿不到引用后再赋值，而 AddComponent 会立刻跑 Awake ——
        /// 如果 Awake 里就写入，会全部用上默认值。
        /// </summary>
        void Start()
        {
            if (_light == null) return;

            _light.color = color;
            _light.intensity = intensity;
            _light.pointLightOuterRadius = outerRadius;
            _light.pointLightInnerRadius = innerRadius;
        }

        void Update()
        {
            if (_light == null || pulseSpeed <= 0f) return;

            float k = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f + _phase);
            _light.intensity = Mathf.Lerp(pulseMin, pulseMax, k);
        }
    }
}
