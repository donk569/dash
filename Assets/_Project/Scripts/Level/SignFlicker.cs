using UnityEngine;

namespace TiantaiDash
{
    /// <summary>霓虹招牌的轻微明暗呼吸，避免整条街像一张静态图。</summary>
    public class SignFlicker : MonoBehaviour
    {
        public Color baseColor = Color.white;
        public float speed = 3.2f;
        public float amplitude = 0.18f;

        SpriteRenderer _sr;
        float _phase;

        void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _phase = Random.Range(0f, 10f);
        }

        void Update()
        {
            if (_sr == null) return;
            float k = 1f - amplitude + amplitude * Mathf.PerlinNoise(_phase, Time.time * speed);
            _sr.color = new Color(baseColor.r * k, baseColor.g * k, baseColor.b * k, 1f);
        }
    }
}
