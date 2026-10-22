using UnityEngine;

namespace TiantaiDash
{
    /// <summary>
    /// 击杀奖励：玩家每杀死一个敌人，自动向前冲刺一次。
    ///
    /// 规则（都为"爽"和"稳"服务）：
    /// 1. **只认玩家击杀**：订阅 <see cref="EnemyHealth.OnKilledByPlayer"/>；
    /// 2. **按"刀"合并**：同一次挥砍（<see cref="PlayerCombat.AttackCount"/> 不变）里的连杀
    ///    只给一次冲刺。用刀号而不是纯时间窗，才能既"一刀三杀只冲一次"又"立刻冲"；
    /// 3. **立刻冲**：不等收刀播完，斩击会把动作自己演完（冲刺只接管移动），
    ///    这样"冲刺 → 撞死 → 再冲"才连得起来；
    /// 4. **冲刺中又击杀 → 记 1 次待补**：当前冲刺结束后补一次（最多 1 次）。
    /// </summary>
    public class KillReward : MonoBehaviour
    {
        [Tooltip("同一刀内连杀的兜底合并窗口（秒）；主要靠刀号判定，这里只是给冲刺撞击兜底")]
        public float mergeWindow = 0.25f;
        [Tooltip("冲刺过程中又击杀时，允许在当前冲刺结束后补冲的次数上限")]
        public int queuedDashLimit = 1;

        [Header("击杀手感")]
        [Tooltip("击杀瞬间的顿帧时长（0 = 不顿）")]
        public float hitStop = 0.07f;
        [Tooltip("击杀瞬间的时间倍率")]
        [Range(0.01f, 0.5f)] public float hitStopScale = 0.12f;
        [Tooltip("击杀瞬间的震屏强度")]
        public float killShake = 0.25f;

        [Header("引用（留空自动找）")]
        public DashController dash;
        public IaiCharge iai;
        public PlayerCombat combat;

        [Header("击杀回血")]
        [Tooltip("每击杀多少个回复 1 点血（0 = 关闭）")]
        public int killsPerHeal = 3;
        [Tooltip("留空自动找 PlayerHealth")]
        public PlayerHealth health;

        public int KillCount { get; private set; }
        public int DashCount { get; private set; }
        /// <summary>因为奖励没兑现而补冲的次数（调试用）。</summary>
        public int QueuedDashCount { get; private set; }

        /// <summary>距离下一次回血还差几个击杀（0..killsPerHeal-1）。</summary>
        public int KillProgress => killsPerHeal <= 0 ? 0 : KillCount % killsPerHeal;

        EnemyHealth[] _enemies;
        int _lastRewardSwing = -1;
        float _lastRewardTime = -99f;
        bool _pending;
        int _queued;

        void Awake()
        {
            if (dash == null) dash = FindObjectOfType<DashController>();
            if (iai == null) iai = FindObjectOfType<IaiCharge>();
            if (combat == null) combat = FindObjectOfType<PlayerCombat>();
            if (health == null) health = FindObjectOfType<PlayerHealth>();
        }

        void Start()
        {
            Subscribe();
        }

        void OnDestroy()
        {
            Unsubscribe();
        }

        void Subscribe()
        {
            _enemies = FindObjectsOfType<EnemyHealth>(true);
            for (int i = 0; i < _enemies.Length; i++)
                if (_enemies[i] != null) _enemies[i].OnKilledByPlayer += HandleKilled;
        }

        void Unsubscribe()
        {
            if (_enemies == null) return;
            for (int i = 0; i < _enemies.Length; i++)
                if (_enemies[i] != null) _enemies[i].OnKilledByPlayer -= HandleKilled;
            _enemies = null;
        }

        void HandleKilled(EnemyHealth enemy)
        {
            KillCount++;

            // 每 killsPerHeal 杀回 1 点血（满血时不浪费）
            if (killsPerHeal > 0 && KillCount % killsPerHeal == 0
                && health != null && health.CurrentHealth < health.maxHealth)
            {
                health.Heal(1);
            }

            int swing = combat != null ? combat.AttackCount : -1;
            bool sameSwing = swing == _lastRewardSwing
                             && (Time.unscaledTime - _lastRewardTime) < mergeWindow;

            // 击杀瞬间的"重"：顿帧 + 轻微慢镜 + 震屏（同一刀的后续连杀不再重复）
            if (!sameSwing)
            {
                if (hitStop > 0f) TimeJuice.HitStop(hitStop, hitStopScale);
                if (killShake > 0f) CameraFollow.Shake(killShake);
            }

            // 同一刀里的第二个及以后的击杀 → 只算人头，不再给冲刺
            if (sameSwing) return;

            _lastRewardSwing = swing;
            _lastRewardTime = Time.unscaledTime;
            _pending = true;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying)
            {
                // 失败 / 通关 / 暂停：奖励作废
                _pending = false;
                _queued = 0;
                return;
            }

            if (dash == null)
            {
                _pending = false;
                _queued = 0;
                return;
            }

            if (_pending)
            {
                _pending = false;

                // 已经在冲 → 不打断当前冲刺，改成"排队补一次"
                if (dash.IsDashing)
                {
                    if (_queued < queuedDashLimit) _queued++;
                    return;
                }

                if (dash.RequestKillDash()) DashCount++;
                return;
            }

            // 补冲：当前冲刺真的结束了才补，不叠加速度、不重置计时器
            if (_queued <= 0 || dash.IsDashing) return;

            _queued--;
            QueuedDashCount++;
            if (dash.RequestKillDash()) DashCount++;
        }

        /// <summary>重开时调用：清掉未兑现的奖励。</summary>
        public void ResetState()
        {
            _pending = false;
            _queued = 0;
            _lastRewardSwing = -1;
            _lastRewardTime = -99f;
            KillCount = 0;
            DashCount = 0;
            QueuedDashCount = 0;
        }
    }
}
