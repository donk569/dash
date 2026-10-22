using UnityEngine;

namespace TiantaiDash
{
    /// <summary>
    /// 少女主角动画状态机：待机 / 跑 / 上升 / 下落 / 落地缓冲 / 挥击 / 受伤。
    ///
    /// 三个关键点（之前的版本跳起来"很怪"就是因为缺这些）：
    /// 1. **上升和下落之间加滞回**：顶点附近竖直速度会在 ±0 反复穿阈值，没滞回就会来回闪帧。
    /// 2. **落地缓冲**：从空中落回地面的瞬间插一段蹲姿（landHold），不然会从"下落"直接硬切"跑步"。
    /// 3. **挤压拉伸**：起跳纵向拉长、落地压扁，靠 Visual 子物体的 localScale 做，不影响碰撞体。
    ///
    /// 视觉放在子物体 Visual 上（本脚本挂在 Player 根节点），所以拉伸不会把碰撞体一起拉变形。
    /// </summary>
    public class PlayerVisual : MonoBehaviour
    {
        [Header("帧序列（留空则用素材总表）")]
        public Sprite[] idleFrames;
        public Sprite[] runFrames;
        public Sprite[] riseFrames;
        public Sprite[] fallFrames;
        public Sprite[] landFrames;
        public Sprite[] shootFrames;
        public Sprite[] hurtFrames;
        [Tooltip("居合拔刀蓄力的姿势帧（留空回退 idle）。建议用下蹲/低姿态的帧")]
        public Sprite[] chargeFrames;

        [Header("帧率")]
        public float runFps = 13f;
        public float idleFps = 4f;
        [Tooltip("跑速基准，用来让跑步动画跟随移动速度")]
        public float speedReference = 18f;

        [Header("空中姿态（滞回阈值，越大越不容易闪）")]
        [Tooltip("竖直速度超过它才算上升")]
        public float riseEnter = 2.5f;
        [Tooltip("竖直速度低于它才算下落")]
        public float fallEnter = -2.5f;

        [Header("落地 / 待机")]
        [Tooltip("落地蹲姿持续多久（秒）")]
        public float landHold = 0.09f;
        [Tooltip("水平速度低于它算站住，播待机")]
        public float minRunSpeed = 1.2f;

        [Header("体型")]
        [Tooltip("视觉缩放，1 = 原始像素大小")]
        [Range(0.5f, 1.5f)] public float visualScale = 0.78f;

        [Header("挥剑发力")]
        [Tooltip("斩击时身体前倾的角度（负 Z 为前方）")]
        [Range(0f, 20f)] public float attackLean = 8f;
        [Tooltip("拔刀蓄力时身体前倾的角度（比斩击小，保持蓄势的稳定感）")]
        [Range(0f, 20f)] public float chargeLean = 3f;
        [Tooltip("前倾跟随速度")]
        public float leanSpeed = 900f;

        [Header("挤压拉伸")]
        public bool squashEnabled = true;
        [Range(0f, 0.4f)] public float squashAmount = 0.16f;
        public float squashRecover = 1.1f;

        enum Pose { Idle, Run, Rise, Fall, Land, Shoot, Charge, Dead }

        SpriteRenderer _sr;
        Transform _visual;
        Rigidbody2D _rb;
        PlayerJump _jump;
        PlayerHealth _health;
        PlayerRunner _runner;
        PlayerCombat _combat;
        IaiCharge _iai;

        [Header("调试")]
        [Tooltip("打开后在 Inspector 能看到最近经历的姿态序列")]
        public bool logPoses;

        /// <summary>最近 12 次姿态变化，逗号分隔。调动画时看这个比看画面准。</summary>
        public string PoseTrace { get; private set; } = "";

        /// <summary>本次运行中出现过的最大挤压/拉伸幅度（0.16 = 16%）。用来确认手感参数真的生效了。</summary>
        public float MaxSquashSeen { get; private set; }

        Pose _pose = Pose.Idle;
        float _clock;
        float _landUntil;
        float _squash;
        float _lean;

        void Awake()
        {
            // 视觉在子物体 Visual 上；老预制体如果直接挂在根节点也能兼容
            _sr = GetComponentInChildren<SpriteRenderer>();
            _visual = _sr != null ? _sr.transform : transform;

            _rb = GetComponent<Rigidbody2D>();
            _jump = GetComponent<PlayerJump>();
            _health = GetComponent<PlayerHealth>();
            _runner = GetComponent<PlayerRunner>();
            _combat = GetComponent<PlayerCombat>();
            _iai = GetComponent<IaiCharge>();

            if (idleFrames == null || idleFrames.Length == 0) FillFromLibrary();

            if (_jump != null)
            {
                _jump.OnJumped += HandleJumped;
                _jump.OnLanded += HandleLanded;
            }
        }

        void OnDestroy()
        {
            if (_jump == null) return;
            _jump.OnJumped -= HandleJumped;
            _jump.OnLanded -= HandleLanded;
        }

        void FillFromLibrary()
        {
            ArtLibrary art = ArtLibrary.Instance;
            if (art == null) return;

            idleFrames = art.playerIdle;
            runFrames = art.playerRun;
            riseFrames = art.playerRise;
            fallFrames = art.playerFall;
            landFrames = art.playerLand;
            shootFrames = art.playerShoot;
            hurtFrames = art.playerHurt;
            chargeFrames = art.playerCharge;
        }

        void HandleJumped()
        {
            if (!squashEnabled) return;
            _squash = squashAmount;       // 纵向拉长
            SetPose(Pose.Rise);
            _clock = 0f;
        }

        void HandleLanded()
        {
            _landUntil = Time.time + landHold;
            SpawnDust();
            if (!squashEnabled) return;
            _squash = -squashAmount;      // 压扁
        }

        /// <summary>落地扬尘：几颗小方块往两侧散开，配合压扁一起做"踩实了"的重量感。</summary>
        void SpawnDust()
        {
            ArtLibrary art = ArtLibrary.Instance;
            if (art == null || art.dustFrames == null || art.dustFrames.Length == 0) return;

            Vector3 basePos = transform.position + Vector3.up * 0.08f;

            for (int i = 0; i < 4; i++)
            {
                Sprite s = art.dustFrames[Random.Range(0, art.dustFrames.Length)];
                FxSprite fx = FxSprite.Spawn(
                    s,
                    basePos + new Vector3(Random.Range(-0.35f, 0.35f), Random.Range(0f, 0.12f), 0f),
                    new Vector2(Random.Range(2.5f, 7f) * (i % 2 == 0 ? 1f : -1f), Random.Range(0.8f, 2.6f)),
                    new Color(0.78f, 0.86f, 0.96f, 0.75f),
                    Random.Range(0.22f, 0.34f),
                    Random.Range(1.2f, 1.8f),
                    15);

                fx.drag = 3.5f;
                fx.shrink = true;
            }
        }

        /// <summary>当前身体前倾角（度）。SwordSwing 用它让剑跟着身体转，避免"身体动了剑没动"。</summary>
        public float CurrentLean => _lean;

        /// <summary>当前姿势名（调试/自测用）。</summary>
        public string CurrentPose => _pose.ToString();

        void SetPose(Pose next)
        {
            if (next == _pose) return;
            _pose = next;

            if (!logPoses) return;
            PoseTrace = string.IsNullOrEmpty(PoseTrace) ? next.ToString() : (PoseTrace + "," + next);
            string[] parts = PoseTrace.Split(',');
            if (parts.Length > 12) PoseTrace = string.Join(",", parts, parts.Length - 12, 12);
        }

        void Update()
        {
            if (_sr == null) return;

            _clock += Time.deltaTime;
            UpdateSquash();

            if (_health != null && _health.IsDead)
            {
                SetPose(Pose.Dead);
                ApplyFrame();
                return;
            }

            // 居合蓄力（拔刀 + 持剑蓄力）。
            // 蓄力现在是"减速滑行"，所以还在跑的时候用跑步循环（边跑边蓄，不僵），
            // 慢下来才换成低姿态的蓄力帧。
            if (_iai != null && _iai.IsChargePhase)
            {
                float spd = Mathf.Abs(_rb != null ? _rb.velocity.x : 0f);
                if (spd > minRunSpeed && Has(runFrames))
                {
                    SetPose(Pose.Run);
                }
                else
                {
                    SetPose(Pose.Charge);
                }
                ApplyFrame();
                return;
            }

            // 斩击 / 收刀：插"双手发力"帧
            // 素材里的 shoot 是双手前伸，正好当挥剑发力的姿态
            if (_combat != null && _combat.IsBusy && Has(shootFrames))
            {
                SetPose(Pose.Shoot);
                ApplyFrame();
                return;
            }

            bool grounded = _jump != null && _jump.IsGrounded;
            float vy = _rb != null ? _rb.velocity.y : 0f;

            if (!grounded)
            {
                // 滞回：已经在上冲就一直保持上升，直到明显开始掉
                if (_pose == Pose.Rise)
                {
                    if (vy < fallEnter) SetPose(Pose.Fall);
                }
                else if (_pose == Pose.Fall)
                {
                    if (vy > riseEnter) SetPose(Pose.Rise);
                }
                else
                {
                    SetPose(vy > riseEnter ? Pose.Rise : Pose.Fall);
                }
            }
            else if (Time.time < _landUntil)
            {
                SetPose(Pose.Land);
            }
            else
            {
                float speed = Mathf.Abs(_rb != null ? _rb.velocity.x : 0f);
                SetPose(speed < minRunSpeed ? Pose.Idle : Pose.Run);
            }

            ApplyFrame();
        }

        void ApplyFrame()
        {
            Sprite frame = null;

            switch (_pose)
            {
                case Pose.Dead:
                    frame = First(hurtFrames);
                    break;

                case Pose.Shoot:
                    frame = First(shootFrames);
                    break;

                case Pose.Charge:
                    frame = First(chargeFrames) ?? ArtLibrary.Pick(idleFrames, _clock, idleFps) ?? First(runFrames);
                    break;

                case Pose.Land:
                    frame = First(landFrames) ?? First(fallFrames);
                    break;

                case Pose.Rise:
                    frame = First(riseFrames);
                    break;

                case Pose.Fall:
                    // 掉得很快时用第二帧（收腿），慢速下落用第一帧
                    float vy = _rb != null ? _rb.velocity.y : 0f;
                    frame = (vy < -16f && Has(fallFrames)) ? fallFrames[fallFrames.Length - 1] : First(fallFrames);
                    break;

                case Pose.Idle:
                    frame = ArtLibrary.Pick(idleFrames, _clock, idleFps) ?? First(runFrames);
                    break;

                default:
                    float speed = _runner != null ? _runner.CurrentSpeed : speedReference;
                    float fps = runFps * Mathf.Clamp(speed / Mathf.Max(0.01f, speedReference), 0.6f, 2.0f);
                    frame = ArtLibrary.Pick(runFrames, _clock, fps);
                    break;
            }

            if (frame != null) _sr.sprite = frame;
        }

        void UpdateSquash()
        {
            if (!squashEnabled || _visual == null) return;

            _squash = Mathf.MoveTowards(_squash, 0f, squashRecover * Time.deltaTime);

            MaxSquashSeen = Mathf.Max(MaxSquashSeen, Mathf.Abs(_squash));

            float sy = (1f + _squash) * visualScale;
            float sx = (1f - _squash * 0.6f) * visualScale;
            _visual.localScale = new Vector3(sx, sy, 1f);

            // 压扁时脚会陷进平台，往上补一点（pivot 在脚底，0.045 是脚底到画布底的距离）
            float lift = _squash < 0f ? -_squash * 0.9f : 0f;
            _visual.localPosition = new Vector3(0f, lift, 0f);

            // 挥剑时身体前压（只转视觉，碰撞体不动）；蓄力时压得少一点，保持"蓄势站定"的感觉
            float wantLean = 0f;
            if (_iai != null && _iai.IsChargePhase) wantLean = chargeLean;
            else if (_combat != null && _combat.IsBusy) wantLean = attackLean;

            _lean = Mathf.MoveTowards(_lean, wantLean, leanSpeed * Time.deltaTime);
            _visual.localRotation = Quaternion.Euler(0f, 0f, -_lean);
        }

        static bool Has(Sprite[] set) { return set != null && set.Length > 0; }

        static Sprite First(Sprite[] set) { return Has(set) ? set[0] : null; }
    }
}
