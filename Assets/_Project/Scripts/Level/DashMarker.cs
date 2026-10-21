using UnityEngine;

namespace TiantaiDash
{
    /// <summary>疾行印记：Trigger 触发一次，自动给玩家冲刺；箭头上下浮动 + 脉动，消耗后变暗。</summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class DashMarker : MonoBehaviour
    {
        public float triggerWidth = 0.6f;
        public float triggerHeight = 2.2f;
        public float bobAmplitude = 0.09f;
        public float bobSpeed = 3f;

        bool _consumed;
        Transform _arrow;
        SpriteRenderer _arrowSr;
        SpriteRenderer _glow;
        Vector3 _arrowBase;
        Color _glowBase;

        void Awake()
        {
            Transform arrowT = transform.Find("Arrow");
            _arrow = arrowT;
            if (_arrow != null)
            {
                _arrowSr = _arrow.GetComponent<SpriteRenderer>();
                _arrowBase = _arrow.localPosition;
            }

            Transform glowT = transform.Find("Glow");
            if (glowT != null) _glow = glowT.GetComponent<SpriteRenderer>();
            if (_glow != null) _glowBase = _glow.color;
        }

        void Update()
        {
            if (_consumed || _arrow == null) return;

            float t = Time.time * bobSpeed;
            _arrow.localPosition = _arrowBase + new Vector3(0f, Mathf.Sin(t) * bobAmplitude, 0f);

            if (_glow != null)
            {
                float pulse = 0.75f + 0.25f * Mathf.Sin(t * 1.7f);
                _glow.color = new Color(_glowBase.r, _glowBase.g, _glowBase.b, 0.13f * pulse);
            }
        }

        public void Configure(float width, float height)
        {
            triggerWidth = width;
            triggerHeight = height;

            var col = GetComponent<BoxCollider2D>();
            if (col == null) col = gameObject.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(width, height);
            col.offset = new Vector2(0f, height * 0.5f);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (_consumed) return;

            DashController dash = other.GetComponentInParent<DashController>();
            if (dash == null) return;

            var gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying) return;

            _consumed = true;
            if (_arrowSr != null) _arrowSr.color = new Color(1f, 1f, 1f, 0.25f);
            if (_glow != null) _glow.color = new Color(_glowBase.r, _glowBase.g, _glowBase.b, 0.03f);

            dash.TriggerDash();
        }

        public void ResetState()
        {
            _consumed = false;
            if (_arrowSr != null) _arrowSr.color = Color.white;
            if (_glow != null) _glow.color = _glowBase;
            if (_arrow != null) _arrow.localPosition = _arrowBase;
        }
    }
}
