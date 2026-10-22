using UnityEngine;

namespace TiantaiDash
{
    /// <summary>
    /// 自动前跑：只在本模块写水平速度，冲刺用倍率修正。
    ///
    /// <see cref="SetHold"/> 是"居合蓄力"用的局部减速：把目标速度乘一个系数（默认 0.35），
    /// **不动 timeScale、不改 bodyType**，所以相机、背景、UI、输入、蓄力计时全部正常工作。
    /// 早期版本是直接归零，站着蓄力太像急刹车，改成滑行后节奏连贯得多；
    /// 需要完全停住时把系数传 0 即可。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerRunner : MonoBehaviour
    {
        public float baseSpeed = 18f;

        Rigidbody2D _rb;
        bool _dashing;
        bool _hold;
        float _holdFactor;
        float _dashMultiplier = 1f;

        public float CurrentSpeed => _hold ? baseSpeed * Mathf.Clamp01(_holdFactor)
                                           : baseSpeed * (_dashing ? _dashMultiplier : 1f);

        /// <summary>蓄力减速中。</summary>
        public bool IsHeld => _hold;

        /// <summary>当前减速系数（1 = 不减速）。</summary>
        public float HoldFactor => _hold ? Mathf.Clamp01(_holdFactor) : 1f;

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

        /// <summary>
        /// 蓄力期间减速。factor = 0 完全停住，0.35 慢速滑行（推荐），1 = 不减速。
        /// 竖直速度不动，所以站着不会掉。
        /// </summary>
        public void SetHold(bool hold, float factor = 0f)
        {
            _hold = hold;
            _holdFactor = Mathf.Clamp01(factor);

            // 立刻把水平速度对上去，避免固定帧之间还往前滑一小段
            if (hold && _rb != null)
                _rb.velocity = new Vector2(CurrentSpeed, _rb.velocity.y);
        }

        public void SetDashActive(bool active, float multiplier)
        {
            _dashing = active;
            _dashMultiplier = multiplier;
        }
    }
}
