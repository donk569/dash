using System;
using UnityEngine;

namespace TiantaiDash
{
    /// <summary>跳跃：单跳 + coyote，二段跳由增益解锁。世界放大后初速提到 15.7（滞空约 1.07s）。</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerJump : MonoBehaviour
    {
        public float jumpVelocity = 15.7f;
        public float secondJumpVelocity = 14f;
        public float coyoteTime = 0.1f;
        public int extraJumps;

        [SerializeField] InputHandler input;

        /// <summary>起跳瞬间（含二段跳）。给动画层做挤压拉伸用。</summary>
        public event Action OnJumped;
        /// <summary>从空中落回地面的一瞬间。</summary>
        public event Action OnLanded;

        [Header("冲刺跳")]
        [Tooltip("冲刺期间补一次跳跃，不打断冲刺；每次冲刺只给一次")]
        public bool allowDashJump = true;
        [Tooltip("冲刺跳的竖直初速相对普通跳的比例")]
        [Range(0.5f, 1.2f)] public float dashJumpScale = 0.95f;

        Rigidbody2D _rb;
        BoxCollider2D _col;
        DashController _dash;
        IaiCharge _iai;
        bool _dashJumpUsed;
        float _lastGroundedTime = -99f;
        float _lastLandFxTime = -99f;
        int _airJumpsLeft;
        bool _wasGrounded;
        bool _groundedOnce;

        public bool IsGrounded { get; private set; }

        public int ExtraJumps
        {
            get { return extraJumps; }
            set
            {
                extraJumps = value;
                if (IsGrounded) _airJumpsLeft = value;
            }
        }

        void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _col = GetComponent<BoxCollider2D>();
            _dash = GetComponent<DashController>();
            _iai = GetComponent<IaiCharge>();
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying) return;

            GroundCheck();

            // 冲刺结束就还回这一次机会
            if (_dash == null || !_dash.IsDashing) _dashJumpUsed = false;

            if (input == null) return;

            // 蓄力中的跳跃由 IaiCharge 处理（带冷却），这里不抢输入，
            // 否则冷却期内的跳会被这个脚本照常执行，等于冷却失效。
            if (_iai != null && _iai.IsChargePhase) return;

            if (input.ConsumeJump()) RequestJump();
        }

        public void SetInput(InputHandler handler) { input = handler; }

        void GroundCheck()
        {
            if (_col == null || _rb == null) return;

            Bounds b = _col.bounds;
            Vector2 origin = new Vector2(b.center.x, b.min.y + 0.08f);
            Vector2 size = new Vector2(Mathf.Max(0.1f, b.size.x * 0.8f), 0.06f);

            RaycastHit2D hit = Physics2D.BoxCast(origin, size, 0f, Vector2.down, 0.22f, GameLayers.GroundMask);
            bool grounded = hit.collider != null && _rb.velocity.y <= 0.5f;

            // 刚落地：出生第一次贴地不算（_groundedOnce），之后每次从空中回来都算
            if (grounded && !_wasGrounded && _groundedOnce && Time.time - _lastLandFxTime > 0.15f)
            {
                _lastLandFxTime = Time.time;
                AudioManager.Play("sfx_land", 0.5f, UnityEngine.Random.Range(0.95f, 1.05f));
                OnLanded?.Invoke();
            }
            if (grounded) _groundedOnce = true;

            IsGrounded = grounded;
            _wasGrounded = grounded;

            if (grounded)
            {
                _lastGroundedTime = Time.time;
                _airJumpsLeft = extraJumps;
            }
        }

        public void RequestJump()
        {
            if (Time.time - _lastGroundedTime <= coyoteTime)
            {
                _rb.velocity = new Vector2(_rb.velocity.x, jumpVelocity);
                _lastGroundedTime = -99f;
                IsGrounded = false;
                _airJumpsLeft = extraJumps;
                AudioManager.Play("sfx_jump", 0.8f, UnityEngine.Random.Range(0.96f, 1.06f));
                OnJumped?.Invoke();
            }
            else if (_airJumpsLeft > 0)
            {
                _airJumpsLeft--;
                _rb.velocity = new Vector2(_rb.velocity.x, secondJumpVelocity);
                AudioManager.Play("sfx_jump", 0.7f, UnityEngine.Random.Range(1.12f, 1.22f));
                OnJumped?.Invoke();
            }
            else if (allowDashJump && _dash != null && _dash.IsDashing && !_dashJumpUsed)
            {
                // 冲刺跳：只补竖直速度，**不动冲刺状态和水平倍率**，
                // 所以不会出现"跳一下就断掉冲刺、然后贴着坑壁往下掉"的情况。
                _dashJumpUsed = true;
                float vy = Mathf.Max(_rb.velocity.y, jumpVelocity * dashJumpScale);
                _rb.velocity = new Vector2(_rb.velocity.x, vy);
                AudioManager.Play("sfx_jump", 0.8f, UnityEngine.Random.Range(1.0f, 1.1f));
                OnJumped?.Invoke();
            }
        }

        /// <summary>
        /// 蓄力跳：**不看地面状态**，直接给一次竖直初速（空中也能跳）。
        /// 只由 IaiCharge 在"蓄力中按跳"时调用，冷却由 IaiCharge 管。
        /// </summary>
        public void ChargeJump()
        {
            if (_rb == null) _rb = GetComponent<Rigidbody2D>();

            _rb.velocity = new Vector2(_rb.velocity.x, jumpVelocity);
            IsGrounded = false;
            _lastGroundedTime = -99f;
            _wasGrounded = false;

            AudioManager.Play("sfx_jump", 0.8f, UnityEngine.Random.Range(0.98f, 1.08f));
            OnJumped?.Invoke();
        }

        public void ResetState()
        {
            IsGrounded = false;
            _wasGrounded = false;
            _groundedOnce = false;
            _lastGroundedTime = -99f;
            _lastLandFxTime = -99f;
            _dashJumpUsed = false;
            _airJumpsLeft = extraJumps;
        }
    }
}
