using UnityEngine;

namespace TiantaiDash
{
    /// <summary>
    /// 击杀奖励：玩家每杀死一个敌人，自动向前冲刺一次。
    ///
    /// 三条规则（都为了"稳"）：
    /// 1. **只认玩家击杀**：订阅 <see cref="EnemyHealth.OnKilledByPlayer"/>，
    ///    环境伤害 / 敌人互殴不会触发；
    /// 2. **合并**：同一次攻击的连杀（默认 0.2s 内）只给一次冲刺，
    ///    否则一刀三杀会连冲三次、直接穿过半个关卡；
    /// 3. **不打断**：等斩击收尾动作播完再冲；如果已经在冲刺，
    ///    直接丢弃这次奖励（`TriggerDash` 本身也不会重置计时器）。
    /// </summary>
    public class KillReward : MonoBehaviour
    {
        [Tooltip("同一次连杀合并窗口（秒）")]
        public float mergeWindow = 0.2f;

        [Header("引用（留空自动找）")]
        public DashController dash;
        public IaiCharge iai;

        public int KillCount { get; private set; }
        public int DashCount { get; private set; }

        EnemyHealth[] _enemies;
        float _windowStart = -99f;
        bool _pending;

        void Awake()
        {
            if (dash == null) dash = FindObjectOfType<DashController>();
            if (iai == null) iai = FindObjectOfType<IaiCharge>();
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

            // 已在合并窗口内 → 只算击杀数，不重复开窗口
            if (Time.unscaledTime - _windowStart < mergeWindow) return;

            _windowStart = Time.unscaledTime;
            _pending = true;
        }

        void Update()
        {
            if (!_pending) return;
            if (dash == null) { _pending = false; return; }

            var gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying) { _pending = false; return; }

            // 等合并窗口结束，把连杀算成一次
            if (Time.unscaledTime - _windowStart < mergeWindow) return;

            // 斩击还在播（下劈 + 收刀）→ 等它播完再冲，别打断动作
            if (iai != null && iai.IsAttacking) return;

            // 已经在冲 → 这次奖励作废，绝不重置计时器
            if (dash.IsDashing) { _pending = false; return; }

            _pending = false;
            if (dash.RequestKillDash()) DashCount++;
        }

        /// <summary>重开时调用：清掉未兑现的奖励。</summary>
        public void ResetState()
        {
            _pending = false;
            _windowStart = -99f;
            KillCount = 0;
            DashCount = 0;
        }
    }
}
