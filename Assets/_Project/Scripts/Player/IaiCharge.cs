using System;
using UnityEngine;

namespace TiantaiDash
{
    /// <summary>
    /// 居合拔刀蓄力斩。**只管时序与阶段**：
    /// 判定交给 <see cref="PlayerCombat"/>（复用剑身采样），
    /// 剑的动作交给 <see cref="SwordSwing"/>（复用四段循环），
    /// 停跑只调 <see cref="PlayerRunner.SetHold"/>（不动 timeScale）。
    ///
    /// 流程：
    /// Idle → 按下左键 → Charging（手按刀柄 0.06s 后开始涨）→ 松开 / 满蓄 →
    /// Releasing（拔刀下劈，判定生效）→ Recovery（收刀）→ Idle
    ///
    /// 关键规则：
    /// - 蓄力**不用 timeScale**，所以暂停时 deltaTime 变 0，蓄力自动冻结、恢复后继续；
    /// - 满蓄自动释放后置 `_waitRelease`，必须真的松开左键才允许下一次；
    /// - 只有在地面、非冲刺、非出刀中才能起手（空中蓄力第一版不做）。
    /// </summary>
    public class IaiCharge : MonoBehaviour
    {
        /// <summary>一个蓄力阶段的全部参数，Inspector 里逐段展开。</summary>
        [Serializable]
        public class Stage
        {
            public string name = "快斩";
            [Tooltip("进入本阶段所需的蓄力比例（0~1），必须递增")]
            [Range(0f, 1f)] public float threshold;
            public int damage = 1;
            [Tooltip("下劈时长（秒），越短越利落")]
            public float slashTime = 0.13f;
            [Tooltip("下劈终点角度：118 = 前下方，140 = 劈到脚下")]
            public float endAngle = 118f;
            [Tooltip("剑长倍率（会叠加范围增益）")]
            public float lengthScale = 1f;
            public int trailCount = 3;
            [Tooltip("镜头震动强度")]
            public float shake = 0.15f;
            [Tooltip("命中顿帧时长（0 = 不顿）")]
            public float hitStop = 0.03f;
            public Color trailColor = new Color(0.62f, 0.95f, 1f, 0.45f);
        }

        public enum Phase { Idle, Charging, Releasing, Recovery }

        [Header("蓄力")]
        [Tooltip("从 0 拉到满需要多久")]
        public float maxChargeTime = 1.2f;
        [Tooltip("按下后先摆\"手按刀柄\"的姿势，这段时间不涨蓄力")]
        public float drawDelay = 0.06f;
        [Tooltip("收刀后额外停顿，避免动作被打断")]
        public float recoveryTime = 0.05f;

        [Header("三段参数")]
        public Stage[] stages =
        {
            new Stage { name = "快斩", threshold = 0f,   damage = 1, slashTime = 0.13f, endAngle = 118f, lengthScale = 1f,   trailCount = 3, shake = 0.15f, hitStop = 0.03f },
            new Stage { name = "强斩", threshold = 0.4f, damage = 2, slashTime = 0.18f, endAngle = 126f, lengthScale = 1.15f, trailCount = 5, shake = 0.35f, hitStop = 0.05f },
            new Stage { name = "奥义", threshold = 0.8f, damage = 3, slashTime = 0.26f, endAngle = 140f, lengthScale = 1.3f,  trailCount = 7, shake = 0.6f,  hitStop = 0.08f },
        };

        [Header("引用（留空自动找）")]
        public InputHandler input;
        public PlayerRunner runner;
        public PlayerJump jump;
        public DashController dash;
        public PlayerCombat combat;
        public SwordSwing sword;

        [Header("音效")]
        public string chargeSfx = "sfx_dash";
        public string fullSfx = "sfx_select";
        public string releaseSfx = "sfx_impact";

        public event Action<float> OnChargeStarted;   // 参数：满蓄所需时间
        public event Action<int> OnStageChanged;      // 参数：阶段下标
        public event Action<int> OnReleased;          // 参数：阶段下标
        public event Action OnCancelled;

        Phase _phase = Phase.Idle;
        float _charge;
        float _delayLeft;
        float _recoverUntil;
        bool _waitRelease;
        bool _fullPlayed;

        public Phase CurrentPhase => _phase;
        public float ChargeRatio => maxChargeTime <= 0f ? 1f : Mathf.Clamp01(_charge / maxChargeTime);
        public bool IsCharging => _phase == Phase.Charging && _delayLeft <= 0f;
        public bool IsFull => ChargeRatio >= 1f;
        public bool IsAttacking => _phase == Phase.Releasing || _phase == Phase.Recovery;
        public int StageCount => stages != null ? stages.Length : 0;
        public Stage[] Stages => stages;
        public int CurrentStage { get; private set; }

        void Awake()
        {
            if (input == null) input = GetComponentInParent<InputHandler>();
            if (runner == null) runner = GetComponent<PlayerRunner>();
            if (jump == null) jump = GetComponent<PlayerJump>();
            if (dash == null) dash = GetComponent<DashController>();
            if (combat == null) combat = GetComponent<PlayerCombat>();
            if (sword == null) sword = GetComponentInChildren<SwordSwing>();

            SortStages();
        }

        void SortStages()
        {
            if (stages == null || stages.Length == 0) return;
            Array.Sort(stages, (a, b) => a.threshold.CompareTo(b.threshold));
            if (stages[0].threshold > 0f) stages[0].threshold = 0f;
        }

        void Start()
        {
            var gm = GameManager.Instance;
            if (gm != null) gm.OnStateChanged += HandleGameState;
        }

        void OnDestroy()
        {
            var gm = GameManager.Instance;
            if (gm != null) gm.OnStateChanged -= HandleGameState;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying)
            {
                ForceCancel();
                return;
            }

            // 进入冲刺就放弃蓄力（冲刺优先级更高）
            if (dash != null && dash.IsDashing)
            {
                ForceCancel();
                return;
            }

            switch (_phase)
            {
                case Phase.Idle:
                    if (_waitRelease)
                    {
                        // 满蓄自动释放后，必须真的松开才允许下一次
                        if (input == null || !input.AttackHeld) _waitRelease = false;
                        break;
                    }

                    if (input != null && input.AttackPressed && CanStart()) StartCharge();
                    break;

                case Phase.Charging:
                    TickCharging();
                    break;

                case Phase.Releasing:
                    if (combat == null || !combat.IsBusy)
                    {
                        _phase = Phase.Recovery;
                        _recoverUntil = Time.time + recoveryTime;
                    }
                    break;

                case Phase.Recovery:
                    if (Time.time >= _recoverUntil) EndAttack();
                    break;
            }
        }

        bool CanStart()
        {
            if (jump != null && !jump.IsGrounded) return false;    // 第一版只做地面蓄力
            if (sword != null && sword.IsBusy) return false;
            return true;
        }

        void StartCharge()
        {
            _phase = Phase.Charging;
            _charge = 0f;
            _delayLeft = drawDelay;
            _fullPlayed = false;
            CurrentStage = 0;

            // 局部暂停：玩家停 → 相机停 → 视差背景停，不动 timeScale
            if (runner != null) runner.SetHold(true);
            var sky = FindObjectOfType<SkyEvents>();
            if (sky != null) sky.SetPaused(true);

            AudioManager.Play(chargeSfx, 0.35f, 0.75f);

            if (OnChargeStarted != null) OnChargeStarted(maxChargeTime);
            if (OnStageChanged != null) OnStageChanged(0);
        }

        void TickCharging()
        {
            if (_delayLeft > 0f)
            {
                _delayLeft -= Time.deltaTime;
                return;
            }

            _charge += Time.deltaTime;

            int stage = StageOf(ChargeRatio);
            if (stage != CurrentStage)
            {
                CurrentStage = stage;
                if (OnStageChanged != null) OnStageChanged(stage);
                AudioManager.Play(chargeSfx, 0.3f, 1f + stage * 0.18f);
                if (sword != null) sword.SetSlashEndAngle(stages[stage].endAngle);
            }

            // 满蓄提示只响一次
            if (!_fullPlayed && ChargeRatio >= 0.999f)
            {
                _fullPlayed = true;
                AudioManager.Play(fullSfx, 0.5f, 1.35f);
                CameraFollow.Shake(0.12f);
            }

            // 刀身随蓄力一点点离鞘（纯表现，不影响判定）
            if (sword != null) sword.SetChargeVisual(ChargeRatio);

            bool released = input != null && input.AttackReleased;
            bool full = _charge >= maxChargeTime;

            if (released || full) Release(full);
        }

        void Release(bool auto)
        {
            int idx = StageOf(ChargeRatio);
            CurrentStage = idx;
            Stage stage = stages[Mathf.Clamp(idx, 0, stages.Length - 1)];

            // 先恢复运动，再出刀（避免斩击期间仍然停着）
            if (runner != null) runner.SetHold(false);
            var sky = FindObjectOfType<SkyEvents>();
            if (sky != null) sky.SetPaused(false);
            if (sword != null) sword.SetChargeVisual(0f);

            if (combat != null)
            {
                float speedScale = 0.17f / Mathf.Max(0.06f, stage.slashTime);
                combat.hitStop = stage.hitStop;
                combat.BeginAttack(
                    stage.damage,
                    speedScale,
                    stage.lengthScale * (combat.RangeLengthScale),
                    stage.endAngle,
                    stage.trailCount);
            }

            if (sword != null) sword.trailColor = stage.trailColor;

            CameraFollow.Shake(stage.shake);
            AudioManager.Play(releaseSfx, 0.9f, auto ? 1.05f : 1f - idx * 0.06f);

            // 自动释放后必须等松手，否则按住不放会立刻重来
            _waitRelease = auto;

            _phase = Phase.Releasing;
            _charge = 0f;

            if (OnReleased != null) OnReleased(idx);
        }

        void EndAttack()
        {
            _phase = Phase.Idle;
            _charge = 0f;
            _delayLeft = 0f;
        }

        /// <summary>蓄力比例 → 阶段下标（半开区间，边界不会有歧义）。</summary>
        public int StageOf(float ratio)
        {
            if (stages == null || stages.Length == 0) return 0;

            int result = 0;
            for (int i = 0; i < stages.Length; i++)
                if (ratio >= stages[i].threshold) result = i;

            return result;
        }

        /// <summary>强制取消（死亡 / 通关 / 暂停 / 冲刺 / 商店）。恢复速度、清蓄力、收剑。</summary>
        public void ForceCancel()
        {
            bool wasActive = _phase != Phase.Idle;

            _phase = Phase.Idle;
            _charge = 0f;
            _delayLeft = 0f;
            CurrentStage = 0;
            _waitRelease = true;

            if (runner != null) runner.SetHold(false);

            var sky = FindObjectOfType<SkyEvents>();
            if (sky != null) sky.SetPaused(false);

            if (sword != null && sword.IsBusy) sword.Cancel();

            if (wasActive && OnCancelled != null) OnCancelled();
        }

        // ---------------- 调试接口（MCP / 自动化测试用，鼠标输入无法脚本模拟） ----------------

        public void DebugStartCharge()
        {
            if (_phase == Phase.Idle) StartCharge();
        }

        /// <summary>把蓄力值直接设到某个比例（0~1），便于测各阶段伤害。</summary>
        public void DebugSetCharge(float ratio)
        {
            if (_phase != Phase.Charging) return;
            _delayLeft = 0f;
            _charge = maxChargeTime * Mathf.Clamp01(ratio);
            CurrentStage = StageOf(ChargeRatio);
        }

        public void DebugRelease()
        {
            if (_phase == Phase.Charging) Release(false);
        }

        void HandleGameState(GameState next)
        {
            // 这些状态都不该保留蓄力
            if (next != GameState.Playing) ForceCancel();
        }
    }
}
