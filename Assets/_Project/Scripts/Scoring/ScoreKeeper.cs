using System.Collections.Generic;
using UnityEngine;

namespace TiantaiDash
{
    /// <summary>
    /// 唯一计分器（玩法 A：连击与评级）。
    ///
    /// 规则：
    /// - 事件 → 连击链 +1（满蓄击杀 +2）→ 倍率 = 1 + 0.25 × (链 - 1)，上限 5.0
    /// - 得分 = 事件基础分 × 当前倍率（先加链，再用新倍率结算）
    /// - 3.5 秒内没有新事件 → 断链（分数保留）
    /// - 受伤 → 断链（分数保留）
    /// - 评级只看"受伤次数 + 最高连击"，不看分数 —— 分数会被关卡长度和金币经济带偏
    ///
    /// 这一项不新增 GameState、不碰 timeScale，纯叠加在现有流程上。
    /// </summary>
    public class ScoreKeeper : MonoBehaviour
    {
        [Header("连击链")]
        [Tooltip("多久没有新事件就断链（秒）")]
        public float chainWindow = 3.5f;
        [Tooltip("断链前多少秒开始告警（数字抖动 + 变红）")]
        public float warnWindow = 0.6f;
        [Tooltip("每级连击增加的倍率")]
        public float multiplierStep = 0.25f;
        [Tooltip("倍率上限")]
        public float multiplierMax = 5f;

        [Header("擦身判定")]
        [Tooltip("小于这个距离算撞上，不算擦身（世界单位）")]
        public float nearMissInner = 0.9f;
        [Tooltip("大于这个距离就不算了")]
        public float nearMissOuter = 1.6f;
        [Tooltip("同一个敌人多少秒内只算一次擦身")]
        public float nearMissRepeat = 2f;
        [Tooltip("敌人列表刷新间隔（秒）")]
        public float scanInterval = 0.15f;

        [Header("通关奖励")]
        [Tooltip("无伤通关")]
        public int clearNoDamage = 1000;
        [Tooltip("无伤且未掉落通关（掉落即失败，所以目前等同无伤）")]
        public int clearNoDamageNoFall = 1500;

        [Header("最高分")]
        public string highScoreKey = "HighScore";

        [Header("引用（留空自动找）")]
        public PlayerHealth health;
        public PlayerJump jump;
        public DashController dash;
        public IaiCharge iai;

        public int Score { get; private set; }
        public int Chain { get; private set; }
        public int BestChain { get; private set; }
        public int HighScore { get; private set; }
        /// <summary>本局是否刷新了历史最高分（结算时判定）。</summary>
        public bool NewRecord { get; private set; }
        public bool ClearBonusGiven { get; private set; }

        public int HurtCount => health != null ? health.DamageTakenCount : _hurtCount;

        /// <summary>断链剩余时间（秒）。</summary>
        public float ChainTimeLeft => Chain > 0 ? Mathf.Max(0f, _chainExpireAt - Time.time) : 0f;

        /// <summary>断链剩余比例（1 = 刚续上，0 = 马上断）。</summary>
        public float ChainRatio => chainWindow <= 0f ? 0f : Mathf.Clamp01(ChainTimeLeft / chainWindow);

        /// <summary>是否进入"要断了"的告警状态（HUD 抖动变红）。</summary>
        public bool ChainWarning => Chain > 0 && ChainTimeLeft <= warnWindow;

        public float Multiplier =>
            Mathf.Min(multiplierMax, 1f + multiplierStep * Mathf.Max(0, Chain - 1));

        /// <summary>评级：只看受伤次数 + 最高连击。</summary>
        public ScoreRank Rank
        {
            get
            {
                int hurt = HurtCount;
                if (hurt == 0 && BestChain >= 15) return ScoreRank.S;
                if (hurt <= 1 && BestChain >= 10) return ScoreRank.A;
                if (hurt <= 2 && BestChain >= 5) return ScoreRank.B;
                return ScoreRank.C;
            }
        }

        float _chainExpireAt;
        int _hurtCount;
        int _lastSfxStep;
        int _lastDashSerial = -1;
        int _dashKillsInSerial;

        EnemyHealth[] _enemies;
        float _nextScan;
        readonly Dictionary<int, float> _nearMissUntil = new Dictionary<int, float>();

        void Awake()
        {
            if (health == null) health = FindObjectOfType<PlayerHealth>();
            if (jump == null) jump = FindObjectOfType<PlayerJump>();
            if (dash == null) dash = FindObjectOfType<DashController>();
            if (iai == null) iai = FindObjectOfType<IaiCharge>();

            HighScore = PlayerPrefs.GetInt(highScoreKey, 0);
        }

        void Start()
        {
            Subscribe();
        }

        void OnDestroy()
        {
            Unsubscribe();
        }

        bool IsRunning
        {
            get
            {
                var gm = GameManager.Instance;
                return gm != null && gm.IsPlaying;
            }
        }

        void Subscribe()
        {
            Unsubscribe();

            _enemies = FindObjectsOfType<EnemyHealth>(true);
            for (int i = 0; i < _enemies.Length; i++)
                if (_enemies[i] != null) _enemies[i].OnKilledByPlayer += HandleKilled;

            if (health != null) health.OnDamaged += HandleHurt;

            _nextScan = Time.time + scanInterval;
        }

        void Unsubscribe()
        {
            if (_enemies != null)
            {
                for (int i = 0; i < _enemies.Length; i++)
                    if (_enemies[i] != null) _enemies[i].OnKilledByPlayer -= HandleKilled;
                _enemies = null;
            }

            if (health != null) health.OnDamaged -= HandleHurt;
        }

        void Update()
        {
            // 断链不依赖 IsPlaying：暂停时 timeScale=0、Time.time 不走，链自然不会断；
            // 失败/通关后时间继续走，链该断就断（否则结算面板会挂着一条过期连击）。
            if (Chain > 0 && Time.time >= _chainExpireAt) Break();

            if (!IsRunning) return;

            // 擦身：定期刷新敌人列表，然后每帧比距离
            if (Time.time >= _nextScan)
            {
                _nextScan = Time.time + Mathf.Max(0.05f, scanInterval);
                _enemies = FindObjectsOfType<EnemyHealth>();
            }

            CheckNearMiss();
        }

        // ============================================================ 事件入口

        /// <summary>记一次计分事件：先加链，再按新倍率结算分数。</summary>
        public void AddEvent(ScoreEvent type)
        {
            if (!IsRunning) return;

            int baseScore = BaseScore(type);
            int chainGain = type == ScoreEvent.KillIai ? 2 : 1;

            Chain += chainGain;
            if (Chain > BestChain) BestChain = Chain;

            Score += Mathf.RoundToInt(baseScore * Multiplier);
            _chainExpireAt = Time.time + chainWindow;

            // 每跨 5 档升一次 pitch，给"越连越爽"的听觉反馈
            int step = Chain / 5;
            if (step > _lastSfxStep)
            {
                _lastSfxStep = step;
                AudioManager.Play("sfx_select", 0.45f, Mathf.Min(1.6f, 1f + 0.08f * step));
            }
        }

        /// <summary>断链：分数保留，链归零。受伤也走这里。</summary>
        public void Break()
        {
            if (Chain == 0) return;
            Chain = 0;
            _chainExpireAt = 0f;
            _lastSfxStep = 0;
        }

        /// <summary>通关奖励。只在第一次调用时生效，避免重复入账。</summary>
        public void AddClearBonus()
        {
            if (ClearBonusGiven) return;
            ClearBonusGiven = true;

            int bonus = HurtCount == 0 ? clearNoDamageNoFall : 0;
            if (bonus <= 0) return;

            Score += bonus;
            AudioManager.Play("sfx_portal", 0.7f, 1.15f);
        }

        /// <summary>结算时判定是否刷新最高分，并写档。返回是否新纪录。</summary>
        public bool CommitHighScore()
        {
            NewRecord = Score > 0 && Score > HighScore;
            if (NewRecord)
            {
                HighScore = Score;
                PlayerPrefs.SetInt(highScoreKey, HighScore);
                PlayerPrefs.Save();
            }
            return NewRecord;
        }

        public void ResetState()
        {
            Score = 0;
            Chain = 0;
            BestChain = 0;
            _chainExpireAt = 0f;
            _lastSfxStep = 0;
            _lastDashSerial = -1;
            _dashKillsInSerial = 0;
            _hurtCount = 0;
            ClearBonusGiven = false;
            NewRecord = false;
            _nearMissUntil.Clear();
        }

        // ============================================================ 内部

        void HandleKilled(EnemyHealth enemy)
        {
            if (!IsRunning || enemy == null) return;

            AddEvent(ResolveEvent(enemy));
        }

        /// <summary>把"敌人怎么死的 + 玩家当时的状态"翻译成计分事件。</summary>
        ScoreEvent ResolveEvent(EnemyHealth enemy)
        {
            EnemyHealth.HitSource src = enemy.LastHitSource;

            if (src == EnemyHealth.HitSource.Dash) return ScoreEvent.KillDash;
            if (src == EnemyHealth.HitSource.Stomp) return ScoreEvent.Stomp;

            // 挥砍类：满蓄 > 空中 > 普通
            bool fullIai = iai != null && iai.IsAttacking
                           && iai.stages != null && iai.stages.Length > 0
                           && iai.CurrentStage >= iai.stages.Length - 1;
            if (fullIai) return ScoreEvent.KillIai;

            bool airborne = jump != null && !jump.IsGrounded;
            if (airborne) return ScoreEvent.KillAir;

            return ScoreEvent.KillSword;
        }

        /// <summary>基础分。冲刺撞死在同一段冲刺里递减，防刷分。</summary>
        int BaseScore(ScoreEvent type)
        {
            switch (type)
            {
                case ScoreEvent.KillSword: return 100;
                case ScoreEvent.KillDash:
                    {
                        int serial = dash != null ? dash.DashCount : -1;
                        if (serial != _lastDashSerial)
                        {
                            _lastDashSerial = serial;
                            _dashKillsInSerial = 0;
                        }
                        _dashKillsInSerial++;
                        return _dashKillsInSerial <= 1 ? 150 : 75;
                    }
                case ScoreEvent.KillAir: return 200;
                case ScoreEvent.KillIai: return 300;
                case ScoreEvent.NearMiss: return 50;
                case ScoreEvent.Stomp: return 80;
                case ScoreEvent.Clear: return 0;
                default: return 0;
            }
        }

        void HandleHurt()
        {
            if (!IsRunning) return;
            _hurtCount++;
            Break();
        }

        /// <summary>擦身：从敌人身边 0.9–1.6u 掠过且没撞死它，算一次。</summary>
        void CheckNearMiss()
        {
            if (_enemies == null || health == null) return;

            Vector2 p = health.transform.position;
            for (int i = 0; i < _enemies.Length; i++)
            {
                EnemyHealth e = _enemies[i];
                if (e == null || !e.IsAlive) continue;

                float d = Vector2.Distance(p, e.transform.position);
                if (d < nearMissInner || d > nearMissOuter) continue;

                int id = e.GetInstanceID();
                float until;
                if (_nearMissUntil.TryGetValue(id, out until) && Time.time < until) continue;

                _nearMissUntil[id] = Time.time + nearMissRepeat;
                AddEvent(ScoreEvent.NearMiss);
            }
        }
    }
}
