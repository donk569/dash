using UnityEngine;

namespace TiantaiDash
{
    /// <summary>自动前跑：只在本模块写水平速度，冲刺用倍率修正。</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerRunner : MonoBehaviour
    {
        public float baseSpeed = 18f;

        Rigidbody2D _rb;
        bool _dashing;
        float _dashMultiplier = 1f;

        public float CurrentSpeed => baseSpeed * (_dashing ? _dashMultiplier : 1f);

        void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
        }

        void FixedUpdate()
        {
            if (_rb == null) _rb = GetComponent<Rigidbody2D>();

            var gm = GameManager.Instance;
            float vx = (gm != null && gm.IsPlaying) ? CurrentSpeed : 0f;
            _rb.velocity = new Vector2(vx, _rb.velocity.y);
        }

        public void SetDashActive(bool active, float multiplier)
        {
            _dashing = active;
            _dashMultiplier = multiplier;
        }
    }
}
