using UnityEngine;

namespace TiantaiDash
{
    /// <summary>
    /// 自动前跑：只在本模块写水平速度，冲刺用倍率修正。
    ///
    /// <see cref="SetHold"/> 是"居合蓄力"用的局部暂停：把目标速度设 0，
    /// **不动 timeScale、不改 bodyType**，所以相机、背景、UI、输入、蓄力计时全部正常工作。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerRunner : MonoBehaviour
    {
        public float baseSpeed = 18f;

        Rigidbody2D _rb;
        bool _dashing;
        bool _hold;
        float _dashMultiplier = 1f;

        public float CurrentSpeed => _hold ? 0f : baseSpeed * (_dashing ? _dashMultiplier : 1f);

        /// <summary>蓄力时置 true：目标速度 0，角色原地站住。</summary>
        public bool IsHeld => _hold;

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

        /// <summary>蓄力期间停跑。竖直速度不动，所以站着不会掉。</summary>
        public void SetHold(bool hold)
        {
            _hold = hold;

            // 立刻把水平速度归零，避免固定帧之间还往前滑一小段
            if (hold && _rb != null)
                _rb.velocity = new Vector2(0f, _rb.velocity.y);
        }

        public void SetDashActive(bool active, float multiplier)
        {
            _dashing = active;
            _dashMultiplier = multiplier;
        }
    }
}
