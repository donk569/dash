using UnityEngine;

namespace TiantaiDash
{
    /// <summary>极简特效粒子：速度 + 阻力 + 淡出/缩放，生命周期后自毁。</summary>
    public class FxSprite : MonoBehaviour
    {
        public Vector2 velocity;
        public float life = 0.3f;
        public float drag = 2.5f;
        public float gravity;
        public bool shrink = true;
        [Tooltip("每秒放大比例（>0 边淡出边铺开，刀光用）")]
        public float grow;
        [Tooltip("每秒自转角度")]
        public float spin;

        SpriteRenderer _sr;
        Color _base;
        float _t;
        float _scale = 1f;

        public static FxSprite Spawn(Sprite sprite, Vector3 pos, Vector2 vel, Color color,
                                     float life, float scale, int order)
        {
            var go = new GameObject("Fx");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;

            go.transform.position = pos;
            go.transform.localScale = new Vector3(scale, scale, 1f);

            var fx = go.AddComponent<FxSprite>();
            fx._sr = sr;
            fx._base = color;
            fx.velocity = vel;
            fx.life = Mathf.Max(0.02f, life);
            fx._scale = scale;
            return fx;
        }

        void Update()
        {
            _t += Time.deltaTime;
            float k = _t / life;
            if (k >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            transform.position += (Vector3)(velocity * Time.deltaTime);
            velocity *= Mathf.Max(0f, 1f - drag * Time.deltaTime);
            velocity.y -= gravity * Time.deltaTime;

            Color c = _base;
            c.a = _base.a * (1f - k);
            if (_sr != null) _sr.color = c;

            if (spin != 0f) transform.Rotate(0f, 0f, spin * Time.deltaTime);

            if (grow != 0f)
            {
                float g = _scale * (1f + grow * k);
                transform.localScale = new Vector3(g, g, 1f);
            }
            else if (shrink)
            {
                transform.localScale = Vector3.one * (_scale * (1f - 0.4f * k));
            }
        }
    }
}
