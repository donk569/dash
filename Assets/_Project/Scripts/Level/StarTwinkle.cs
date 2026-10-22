using UnityEngine;

namespace TiantaiDash
{
    /// <summary>单个星星的呼吸闪烁。</summary>
    public class StarTwinkle : MonoBehaviour
    {
        public float phase;
        public float speed = 1.5f;
        public float minAlpha = 0.12f;

        SpriteRenderer _sr;

        void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
        }

        void Update()
        {
            if (_sr == null) return;
            float a = Mathf.Lerp(minAlpha, 1f, 0.5f + 0.5f * Mathf.Sin(Time.time * speed + phase));
            Color c = _sr.color;
            c.a = a;
            _sr.color = c;
        }
    }
}
