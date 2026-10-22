using System;
using UnityEngine;

namespace TiantaiDash
{
    /// <summary>
    /// 居合拔刀蓄力斩。**只管时序与阶段**：
    /// 判定交给 <see cref="PlayerCombat"/>（复用剑身采样），
    /// 剑的动作交给 <see cref="SwordSwing"/>（拔刀 → 持剑蓄力 → 上撩横斩 → 收刀），
    /// 停跑只调 <see cref="PlayerRunner.SetHold"/>（不动 timeScale）。
    ///
    /// 流程：
    /// Idle → 按下左键 → Charging（拔刀 0.09s，拔完才开始涨蓄力）→ 松开 / 满蓄 →
    /// Releasing（上撩斩，判定生效）→ Recovery（收刀）→ Idle
    ///
    /// 关键规则：
    /// - 蓄力**不用 timeScale**，所以编辑器暂停时 deltaTime 变 0，蓄力自动冻结；
    /// - 游戏状态离开 Playing（暂停 / 选卡 / 商店 / 失败 / 通关）一律取消蓄力；
    /// - 满蓄自动释放后置 `_waitRelease`，必须真的松开左键才允许下一次；
    /// - **支持空中蓄力 / 蓄力中起跳**：停跑只锁水平速度，竖直方向交给原有跳跃与重力。
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
            [Tooltip("上撩斩时长（秒），越短越利落")]
            public float slashTime = 0.13f;
            [Tooltip("斩击终点角：98 = 剑尖朝正前方偏下；越大越往下收")]
            public float endAngle = 98f;
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
        public float maxChargeTime = 0.75f;
        [Tooltip("摆出\"手握刀柄\"姿势的起步延迟，这段时间不涨蓄力（不会短于拔刀时长）")]
        public float drawDelay = 0.05f;
        [Tooltip("收刀后额外停顿，避免动作被打断")]
        public float recoveryTime = 0.05f;
        [Tooltip("蓄力期间的移动系数：0.35 = 慢速滑行（推荐）；0 = 完全停住；1 = 不减速")]
        [Range(0f, 1f)] public float chargeMoveFactor = 0.35f;

        [Header("蓄力跳")]
        [Tooltip("蓄力中按跳的冷却（秒）。冷却期间按跳会被吞掉，蓄力继续，防止无限取消")]
        public float chargeJumpCooldown = 1.0f;

        [Header("三段参数")]
        public Stage[] stages =
        {
            new Stage { name = "快斩", threshold = 0f,   damage = 1, slashTime = 0.14f, endAngle = 92f,  lengthScale = 1f,   trailCount = 6, shake = 0.25f, hitStop = 0.05f },
            new Stage { name = "强斩", threshold = 0.4f, damage = 2, slashTime = 0.18f, endAngle = 100f, lengthScale = 1.15f, trailCount = 8, shake = 0.5f,  hitStop = 0.07f },
            new Stage { name = "奥义", threshold = 0.8f, damage = 3, slashTime = 0.24f, endAngle = 108f, lengthScale = 1.3f,  trailCount = 8, shake = 0.85f, hitStop = 0.12f },
        };

        [Header("引用（留空自动找）")]
        public InputHandler input;
        public PlayerRunner runner;
        public PlayerJump jump;
        public DashController dash;
        public PlayerCombat combat;
        public SwordSwing sword;

        [Header("音效")]
        public string chargeSfx = "sfx_iai_charge";
        public string fullSfx = "sfx_iai_full";
        public string releaseSfx = "sfx_iai_slash";

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
        bool _releaseQueued;      // 锁存"松手"：一次性事件不能在拔刀延迟里被吞掉
        bool _holdUntilLand;      // 蓄力中被跳跃打断：减速滑行保留到落地
        float _holdReleaseAt;     // 兜底时间，防止一直没落地
        float _nextChargeJumpTime;
        SkyEvents _sky;

        /// <summary>蓄力跳是否可用（给 UI / 调试看）。</summary>
        public bool ChargeJumpReady => Time.time >= _nextChargeJumpTime;
        /// <summary>蓄力跳剩余冷却（秒）。</summary>
        public float ChargeJumpCooldownLeft => Mathf.Max(0f, _nextChargeJumpTime - Time.time);

        public Phase CurrentPhase => _phase;
        public float ChargeRatio => maxChargeTime <= 0f ? 1f : Mathf.Clamp01(_charge / maxChargeTime);
        public bool IsCharging => _phase == Phase.Charging && _delayLeft <= 0f;
        /// <summary>蓄力阶段（含拔刀那一小段），UI 与姿势帧用这个判断。</summary>
        public bool IsChargePhase => _phase == Phase.Charging;
        public bool IsFull => ChargeRatio >= 1f;
        public bool IsAttacking => _phase == Phase.Releasing || _phase == Phase.Recovery;
        public int StageCount => stages != null ? stages.Length : 0;
        public Stage[] Stages => stages;
        public int CurrentStage { get; private set; }

        void Awake()
        {
            if (input == null) input = GetComponentInParent<InputHandler>();
            if (input == null) input = FindObjectOfType<InputHandler>();   // InputHandler 挂在场景根的 Game 上，不在 Player 父级
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
            if (jump != null) jump.OnLanded += HandleJumpLanded;
        }

        void OnDestroy()
        {
            var gm = GameManager.Instance;
            if (gm != null) gm.OnStateChanged -= HandleGameState;
            if (jump != null) jump.OnLanded -= HandleJumpLanded;
        }

        /// <summary>由 <see cref="GameBootstrap"/> 注入（InputHandler 在 Game 上，不是 Player 的父级）。</summary>
        public void SetInput(InputHandler handler)
        {
            if (handler != null) input = handler;
        }

        /// <summary>天空事件引用只找一次，别在每帧的取消路径里 FindObjectOfType。</summary>
        SkyEvents Sky
        {
            get
            {
                if (_sky == null) _sky = FindObjectOfType<SkyEvents>();
                return _sky;
            }
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying)
            {
                ForceCancel();
                return;
            }

            // 冲刺优先级最高，但**只打断蓄力**：
            // 击杀会立刻触发冲刺，那时剑正在斩，不能把它强行拽回腰间。
            if (dash != null && dash.IsDashing && _phase == Phase.Charging)
            {
                ForceCancel();
                return;
            }

            // 跳跃取消蓄力后保留的减速滑行：落地（或超时）就还回全速
            if (_holdUntilLand && Time.time >= _holdReleaseAt) ReleaseJumpHold();

            switch (_phase)
            {
                case Phase.Idle:
                    if (_waitRelease)
                    {
                        // 满蓄自动释放后，必须真的松开才允许下一次
                        if (input == null || !input.AttackHeld) _waitRelease = false;
                        break;
                    }

                    // 冲刺中按蓄力：先取消冲刺，再起手蓄力（用户要的"冲刺→蓄力"衔接）
                    if (input != null && input.AttackPressed) TryStartCharge();
                    break;

                case Phase.Charging:
                    // 蓄力中按跳：要么跳出去（带冷却），要么被冷却吞掉、继续蓄力。
                    // 用 ConsumeJump 独占这一帧的跳，PlayerJump 在蓄力期间不抢输入。
                    if (input != null && input.ConsumeJump()) TryChargeJump();
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

        /// <summary>起手条件：剑不在忙、不在冲刺、游戏在跑。空中也允许（支持蓄力中起跳）。</summary>
        bool CanStart()
        {
            if (sword != null && sword.IsBusy) return false;
            if (dash != null && dash.IsDashing) return false;
            return true;
        }

        void StartCharge()
        {
            _phase = Phase.Charging;
            _charge = 0f;
            _releaseQueued = false;
            // 拔刀动画走完之前不涨蓄力，否则进度条会先涨后拔
            float draw = sword != null ? sword.DrawTime : 0.05f;
            _delayLeft = Mathf.Max(drawDelay, draw);
            _fullPlayed = false;
            CurrentStage = 0;

            // 局部减速：玩家慢下来 → 相机跟着慢 → 视差背景跟着慢，不动 timeScale
            if (runner != null) runner.SetHold(true, chargeMoveFactor);

            // 手握刀柄（刀还在鞘里）
            if (sword != null) sword.PlayDraw(NormalizedSpeed());

            var sky = Sky;
            if (sky != null) sky.SetPaused(true);

            AudioManager.Play(chargeSfx, 0.35f, 0.75f);

            if (OnChargeStarted != null) OnChargeStarted(maxChargeTime);
            if (OnStageChanged != null) OnStageChanged(0);
        }

        /// <summary>
        /// 按下攻击键的统一入口：冲刺中先取消冲刺，再起手蓄力。
        /// </summary>
        void TryStartCharge()
        {
            if (dash != null && dash.IsDashing) dash.CancelDash();
            if (CanStart()) StartCharge();
        }

        void TickCharging()
        {
            // 松手是**一次性事件**，必须先锁存，否则拔刀延迟里的那一帧会被直接吞掉，
            // 表现为"点一下左键，1.2 秒后自己蹦出一刀"。这是之前长按蓄力失效的根因。
            if (input != null && input.AttackReleased) _releaseQueued = true;
            if (_delayLeft > 0f)
            {
                _delayLeft -= Time.deltaTime;
                // 拔刀没走完就松手 → 一拔完立刻按第一阶段出刀，而不是干等蓄满
                if (_delayLeft <= 0f && _releaseQueued) Release(false);
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

            // 刀身随蓄力冷光变亮 + 微微出鞘（纯表现，不影响判定）
            if (sword != null) sword.SetChargeVisual(ChargeRatio);

            bool full = _charge >= maxChargeTime;

            if (_releaseQueued || full) Release(full);
        }

        void Release(bool auto)
        {
            int idx = StageOf(ChargeRatio);
            CurrentStage = idx;
            Stage stage = stages[Mathf.Clamp(idx, 0, stages.Length - 1)];

            // 先恢复运动，再出刀（斩击期间玩家照常往前跑）
            if (runner != null) runner.SetHold(false);
            var sky = Sky;
            if (sky != null) sky.SetPaused(false);
            if (sword != null) sword.SetChargeVisual(0f);
            _releaseQueued = false;

            // 刀光强度与新月光按阶段区分（奥义额外叠十字闪光）
            if (sword != null) sword.SetStage(idx);

            if (combat != null)
            {
                combat.hitStop = stage.hitStop;
                combat.BeginAttack(
                    stage.damage,
                    NormalizedSpeedFor(stage.slashTime),
                    stage.lengthScale * combat.RangeLengthScale,
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

        /// <summary>拔刀用的速度倍率（1 = 基准时长）。</summary>
        float NormalizedSpeed()
        {
            return 1f;
        }

        /// <summary>
        /// 目标斩击时长 → SwordSwing 的速度倍率（它的基准时长是 sword.slashTime）。
        /// 例：基准 0.17s，目标 0.13s → 倍率 1.31（更快）。
        /// </summary>
        float NormalizedSpeedFor(float targetSlashTime)
        {
            if (sword == null) return 1f;
            float baseTime = Mathf.Max(0.01f, sword.slashTime);
            return baseTime / Mathf.Max(0.04f, targetSlashTime);
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

        /// <summary>强制取消（死亡 / 通关 / 暂停 / 冲刺 / 商店）。恢复速度、清蓄力、收剑。可重复调用。</summary>
        public void ForceCancel()
        {
            bool dirty = _phase != Phase.Idle
                         || _charge > 0f
                         || (runner != null && runner.IsHeld)
                         || (sword != null && sword.IsBusy);

            if (!dirty) return;

            _phase = Phase.Idle;
            _charge = 0f;
            _delayLeft = 0f;
            CurrentStage = 0;
            _waitRelease = true;
            _releaseQueued = false;

            if (runner != null) runner.SetHold(false);

            var sky = Sky;
            if (sky != null) sky.SetPaused(false);

            if (sword != null && sword.IsBusy) sword.Cancel();

            _holdUntilLand = false;

            if (OnCancelled != null) OnCancelled();
        }

        /// <summary>
        /// 蓄力跳：冷却好了才允许。跳出去的同时取消蓄力，但保留减速滑行到落地。
        /// 冷却中返回 false，调用方应该把这次跳吞掉（蓄力继续）。
        /// </summary>
        public bool TryChargeJump()
        {
            if (!ChargeJumpReady) return false;

            _nextChargeJumpTime = Time.time + chargeJumpCooldown;
            if (jump != null) jump.RequestJump();
            CancelChargeForJump();
            return true;
        }

        /// <summary>
        /// 蓄力中被跳跃打断：取消本次蓄力（不出刀），
        /// 但**保留蓄力的减速滑行**，落地才恢复全速 —— 这样"压缩力→慢速起跳"能稳定跳过障碍。
        /// </summary>
        void CancelChargeForJump()
        {
            _phase = Phase.Idle;
            _charge = 0f;
            _delayLeft = 0f;
            CurrentStage = 0;
            _waitRelease = true;          // 得先松开左键，才能再蓄下一次
            _releaseQueued = false;

            if (sword != null && sword.IsBusy) sword.Cancel();

            var sky = Sky;
            if (sky != null) sky.SetPaused(false);

            _holdUntilLand = true;
            _holdReleaseAt = Time.time + 1.2f;   // 兜底：万一这一跳没落地
            if (runner != null) runner.SetHold(true, chargeMoveFactor);

            if (OnCancelled != null) OnCancelled();
        }

        void HandleJumpLanded()
        {
            if (_holdUntilLand) ReleaseJumpHold();
        }

        void ReleaseJumpHold()
        {
            if (!_holdUntilLand) return;
            _holdUntilLand = false;
            if (runner != null) runner.SetHold(false);
        }

        // ---------------- 调试接口（MCP / 自动化测试用，鼠标输入无法脚本模拟） ----------------

        public void DebugStartCharge()
        {
            if (_phase == Phase.Idle) StartCharge();
        }

        /// <summary>调试接口：等价于"这一帧按下攻击键"（鼠标输入无法脚本模拟）。</summary>
        public void DebugPressAttack()
        {
            _waitRelease = false;
            TryStartCharge();
        }

        /// <summary>把蓄力值直接设到某个比例（0~1），便于测各阶段伤害。</summary>
        public void DebugSetCharge(float ratio)
        {
            if (_phase != Phase.Charging) return;
            _delayLeft = 0f;
            _charge = maxChargeTime * Mathf.Clamp01(ratio);
            CurrentStage = StageOf(ChargeRatio);
            if (sword != null) sword.SetChargeVisual(ChargeRatio);
        }

        public void DebugRelease()
        {
            if (_phase == Phase.Charging) Release(false);
        }

        void HandleGameState(GameState next)
        {
            // 这些状态都不该保留蓄力（暂停 = 取消，恢复后必须重新按下左键）
            if (next != GameState.Playing) ForceCancel();
        }
    }
}
