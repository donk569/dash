using UnityEngine;

namespace TiantaiDash
{
    /// <summary>无人机悬停：在初始高度上做正弦浮动。</summary>
    public class HoverBob : MonoBehaviour
    {
        public float amplitude = 0.55f;
        public float speed = 1.6f;
        public float phase;

        float _baseY;

        void Start()
        {
            _baseY = transform.position.y;
            phase = Random.Range(0f, Mathf.PI * 2f);
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm != null && !gm.IsPlaying) return;

            transform.position = new Vector3(transform.position.x,
                _baseY + Mathf.Sin(Time.time * speed + phase) * amplitude, 0f);
        }
    }
}
