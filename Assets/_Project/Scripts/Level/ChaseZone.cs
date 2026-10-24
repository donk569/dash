using System.Collections.Generic;
using UnityEngine;

namespace TiantaiDash
{
    /// <summary>
    /// 追逐压场段（玩法 B）。放到关卡里就是一个空物体，参数全在 Inspector 调。
    ///
    /// 流程：玩家到达 startX → **3s 预警**（顶部红条 + 屏边红渐变）→ 墙出现在身后 initialGap 处
    /// → 墙以 wallSpeed 推进（落后超过 lagBoost 时提速到 catchUpSpeed）→ 追上就 Failed（死因"被城市吞没"）
    /// → 玩家到 endX 收尾：墙减速 + 淡出，规则与相机复原。
    ///
    /// 段内规则（都由这里临时改、结束恢复）：
    /// - 中键冲刺 CD 5s → 2.5s
    /// - 击杀奖励冲刺不受 0.25s 合并窗口限制
    /// - 商店禁用（ShopIcon 查 <see cref="Active"/>）
    /// </summary>
    public class ChaseZone : MonoBehaviour
    {
        /// <summary>当前是否有任何追逐段处于预警/追逐中（商店据此禁用）。</summary>
        public static bool Active { get; private set; }
        public static ChaseZone Current { get; private set; }

        [Header("范围")]
        [Tooltip("段入口 X（留 0 = 用本物体位置）")]
        public float startX;
        [Tooltip("段出口 X（玩家到这里就安全）；留 0 = startX + 140")]
        public float endX;

        [Header("预警")]
        [Tooltip("进段后多久墙真的出现（给玩家反应时间）")]
        public float warnTime = 3f;

        [Header("追逐时长")]
        [Tooltip("墙真正追你的秒数（>0 时按时间结束，优先于 endX）。5 = 追 5 秒后放你走")]
        public float chaseDuration = 5f;

        [Header("墙")]
        [Tooltip("墙一开始出现在玩家身后多远（要小于半屏宽约 18u，否则玩家看不见墙）")]
        public float initialGap = 14f;
        [Tooltip("墙面推进速度（跑速约 18，18.9 表示每秒只逼近 0.9u）")]
        public float wallSpeed = 18.9f;
        [Tooltip("落后超过 lagBoost 时的加速速度（防挂机）")]
        public float catchUpSpeed = 27f;
        [Tooltip("落后多少开始加速")]
        public float lagBoost = 22f;
        [Tooltip("墙速至少跟上玩家速度的这个比例 —— 玩家冲刺时墙也跟着加速，不会被甩出画面")]
        [Range(0.5f, 1f)] public float keepUpRatio = 0.92f;
        [Tooltip("墙追上玩家到前方多少判定死亡")]
        public float catchMargin = 0.5f;
        [Tooltip("墙相对玩家的高度偏移（让墙覆盖屏幕）")]
        public float verticalOffset = 5f;

        [Header("段内规则")]
        public float chaseDashCooldown = 2.5f;
        [Tooltip("段内是否禁用商店")]
        public bool disableShops = true;

        [Header("无人机（副威胁）")]
        [Tooltip("追逐段内生几架（0 = 不用；文档建议只放在第二段）")]
        public int droneCount = 0;
        [Tooltip("第一架在玩家前方多远（必须在前方，身后会被墙挡住）")]
        public float droneAheadMin = 2f;
        [Tooltip("最后一架在玩家前方多远")]
        public float droneAheadMax = 7f;
        public float droneHeightBase = 6.5f;

        [Header("情绪")]
        public float zoomIn = -0.6f;
        public float shakeInterval = 2f;
        public float shakeAmount = 0.12f;
        [Tooltip("出段后墙淡出时长（不能瞬间消失）")]
        public float endFadeTime = 1.2f;

        enum Phase { Idle, Warning, Chasing, Ending, Done }

        Phase _phase = Phase.Idle;
        ChaseWall _wall;
        float _wallX;
        float _wallY;
        float _warnLeft;
        float _shakeTimer;
        float _endTimer;
        float _wallSlide;
        float _chaseTimer;
        /// <summary>Abort 之后要求玩家先退回段口左侧，才允许再次触发（避免失败恢复后立刻重进）。</summary>
        bool _reentryRequired;

        Transform _player;
        Rigidbody2D _playerRb;
        DashController _dash;
        KillReward _killReward;
        readonly List<ChaseDrone> _drones = new List<ChaseDrone>();

        float _oldCooldown;
        float _oldMergeWindow;
        bool _rulesOverridden;

        public float StartX => startX > 0f ? startX : transform.position.x;
        public float EndX => endX > StartX ? endX : StartX + 140f;

        /// <summary>预警中（UI 闪红条但墙还没出来）。</summary>
        public bool IsWarning => _phase == Phase.Warning;
        /// <summary>墙正在追。</summary>
        public bool IsChasing => _phase == Phase.Chasing;
        /// <summary>预警或追逐中（UI 要一直提示）。</summary>
        public bool IsHot => _phase == Phase.Warning || _phase == Phase.Chasing;
        /// <summary>预警剩余秒数。</summary>
        public float WarnLeft => _warnLeft;
        /// <summary>墙落后玩家多少距离（墙还没出现时返回 0）。</summary>
        public float WallDistance =>
            _wall == null || _player == null ? 0f : _player.position.x - _wallX;

        void Awake()
        {
            ResolveRefs();
        }

        void ResolveRefs()
        {
            if (_player == null)
            {
                var hp = FindObjectOfType<PlayerHealth>();
                if (hp != null) _player = hp.transform;
            }
            if (_playerRb == null && _player != null) _playerRb = _player.GetComponent<Rigidbody2D>();
            if (_dash == null) _dash = FindObjectOfType<DashController>();
            if (_killReward == null) _killReward = FindObjectOfType<KillReward>();
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            if (_player == null) ResolveRefs();

            switch (_phase)
            {
                case Phase.Idle:
                    if (!gm.IsPlaying || _player == null) break;

                    // Abort 过：玩家必须先退回段口左侧，才允许重新触发
                    if (_reentryRequired)
                    {
                        if (_player.position.x < StartX - 3f) _reentryRequired = false;
                        break;
                    }

                    if (_player.position.x >= StartX) BeginWarning();
                    break;

                case Phase.Warning:
                case Phase.Chasing:
                    // 暂停只冻结，不算中断（timeScale=0 时 deltaTime 为 0，本来也不会推进）
                    if (IsOver(gm)) { Abort(); return; }
                    if (!gm.IsPlaying) return;

                    if (_phase == Phase.Warning)
                    {
                        _warnLeft -= Time.deltaTime;
                        if (_warnLeft <= 0f) BeginChase();
                    }
                    else
                    {
                        TickChase(Time.deltaTime);
                    }
                    break;

                case Phase.Ending:
                    TickEnding(Time.deltaTime);
                    break;
            }
        }

        static bool IsOver(GameManager gm)
        {
            return gm.CurrentState == GameState.Failed || gm.CurrentState == GameState.Completed;
        }

        // ============================================================ 流程

        void BeginWarning()
        {
            _phase = Phase.Warning;
            _warnLeft = Mathf.Max(0.1f, warnTime);
            Active = true;
            Current = this;

            ResolveRefs();
            ApplyRules();

            CameraFollow.SetZoom(zoomIn);
            AudioManager.Play("sfx_iai_full", 0.75f, 1.15f);
            AudioManager.PlayChaseBgm(true, 0.5f);   // 预警就开始切，等墙真的出现时音乐已经到位
            _shakeTimer = shakeInterval;
        }

        void BeginChase()
        {
            _phase = Phase.Chasing;

            float px = _player.position.x;
            _wallX = px - initialGap;
            _wallY = _player.position.y + verticalOffset;

            _wall = ChaseWall.Create(null, new Vector3(_wallX, _wallY, 0f));
            _wallSlide = wallSpeed;
            _chaseTimer = 0f;

            SpawnDrones();

            AudioManager.Play("sfx_impact", 1f, 0.75f);
            CameraFollow.Shake(0.5f);
        }

        /// <summary>按 droneCount 生成无人机：距离和高度都错开，避免三架排成一排。</summary>
        void SpawnDrones()
        {
            ClearDrones();
            if (droneCount <= 0 || _player == null) return;

            var health = _player.GetComponent<PlayerHealth>();

            for (int i = 0; i < droneCount; i++)
            {
                float t = droneCount <= 1 ? 0f : i / (float)(droneCount - 1);
                float ahead = Mathf.Lerp(droneAheadMin, droneAheadMax, t);
                float height = droneHeightBase + i * 1.2f;

                Vector3 at = new Vector3(
                    _player.position.x + ahead,
                    _player.position.y + height,
                    0f);

                ChaseDrone drone = ChaseDrone.Spawn(at, _player, ahead, height);
                drone.SetPlayer(_player, health);
                _drones.Add(drone);
            }
        }

        void ClearDrones()
        {
            for (int i = 0; i < _drones.Count; i++)
                if (_drones[i] != null) Destroy(_drones[i].gameObject);
            _drones.Clear();
        }

        void TickChase(float dt)
        {
            float px = _player.position.x;

            // 基础速度 → 落后太多就提速（防挂机）→ 再兜一个"跟速下限"：
            // 玩家冲刺 46.8 时墙也跟着到 43，只慢不到 4u/s，冲刺甩不开也看不到"墙消失"，
            // 这样整段都在视野内，压迫感是连续的。
            float spd = (px - _wallX > lagBoost) ? catchUpSpeed : wallSpeed;

            if (_playerRb != null)
            {
                float pvx = Mathf.Abs(_playerRb.velocity.x);
                spd = Mathf.Max(spd, pvx * keepUpRatio);
            }

            _wallSlide = spd;
            _wallX += spd * dt;

            if (_wall != null)
            {
                _wallY = Mathf.Lerp(_wallY, _player.position.y + verticalOffset, 6f * dt);
                _wall.transform.position = new Vector3(_wallX, _wallY, 0f);
            }

            // 像地面在塌：每 2s 一次震屏
            _shakeTimer -= dt;
            if (_shakeTimer <= 0f)
            {
                _shakeTimer = Mathf.Max(0.4f, shakeInterval);
                CameraFollow.Shake(shakeAmount);
            }

            if (_wallX >= px + catchMargin)
            {
                FailCaught();
                return;
            }

            // 按时间收尾优先：追满 chaseDuration 就放人，不管玩家跑到哪
            _chaseTimer += dt;
            if (chaseDuration > 0f && _chaseTimer >= chaseDuration)
            {
                BeginEnding();
                return;
            }

            if (px >= EndX) BeginEnding();
        }

        void BeginEnding()
        {
            _phase = Phase.Ending;
            _endTimer = endFadeTime;
            if (_wall != null) _wall.BeginFade();
            RestoreRules();
            CameraFollow.SetZoom(0f);
            AudioManager.PlayChaseBgm(false, 0.6f);
            AudioManager.Play("sfx_portal", 0.7f, 1.3f);
        }

        void TickEnding(float dt)
        {
            // 墙减速滑出去，不瞬间消失
            _wallSlide = Mathf.MoveTowards(_wallSlide, 0f, 34f * dt);
            _wallX += _wallSlide * dt;

            if (_wall != null)
            {
                _wall.FadeOut(dt, endFadeTime);
                _wall.transform.position = new Vector3(_wallX, _wallY, 0f);
            }

            _endTimer -= dt;
            if (_endTimer <= 0f) Finish();
        }

        void Finish()
        {
            if (_wall != null) Destroy(_wall.gameObject);
            _wall = null;
            ClearDrones();
            _phase = Phase.Done;
            if (Current == this) Current = null;
            Active = false;
        }

        /// <summary>墙抓到玩家：单独死因，不用通用的"掉落屋顶"。</summary>
        void FailCaught()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            RestoreRules();
            CameraFollow.SetZoom(0f);
            CameraFollow.Shake(0.6f);
            AudioManager.PlayChaseBgm(false, 0.3f);
            AudioManager.Play("sfx_hurt", 1f, 0.7f);

            ClearDrones();

            if (Current == this) Current = null;
            Active = false;
            _phase = Phase.Done;

            gm.FailReason = "被城市吞没";
            gm.SetState(GameState.Failed);
        }

        /// <summary>玩家中途死亡/通关：复原规则，别把临时参数留在世界上。允许之后重新触发。</summary>
        void Abort()
        {
            RestoreRules();
            CameraFollow.SetZoom(0f);
            AudioManager.PlayChaseBgm(false, 0.4f);

            if (_wall != null) Destroy(_wall.gameObject);
            _wall = null;

            ClearDrones();

            if (Current == this) Current = null;
            Active = false;
            _phase = Phase.Idle;
            _reentryRequired = true;
        }

        /// <summary>调试 / 重开用：把这一段落彻底复位，立刻可再次触发。</summary>
        public void ResetZone()
        {
            if (_wall != null) Destroy(_wall.gameObject);
            _wall = null;

            ClearDrones();

            RestoreRules();
            if (Current == this) Current = null;
            Active = false;
            _phase = Phase.Idle;
            _reentryRequired = false;
            _warnLeft = 0f;
            _endTimer = 0f;
            _wallSlide = 0f;
            _chaseTimer = 0f;
        }

        // ============================================================ 段内规则

        void ApplyRules()
        {
            if (_rulesOverridden) return;
            _rulesOverridden = true;

            if (_dash != null)
            {
                _oldCooldown = _dash.manualCooldown;
                _dash.manualCooldown = chaseDashCooldown;
            }

            if (_killReward != null)
            {
                // 0 = 同一刀不再合并，每次击杀都给冲刺（"越杀越快"的链条）
                _oldMergeWindow = _killReward.mergeWindow;
                _killReward.mergeWindow = 0f;
            }
        }

        void RestoreRules()
        {
            if (!_rulesOverridden) return;
            _rulesOverridden = false;

            if (_dash != null) _dash.manualCooldown = _oldCooldown;
            if (_killReward != null) _killReward.mergeWindow = _oldMergeWindow;
        }

        /// <summary>商店是否该被禁用（ShopIcon 每帧查这个）。</summary>
        public static bool ShopsBlocked =>
            Active && Current != null && Current.disableShops;

        void OnDrawGizmos()
        {
            float sx = StartX;
            float ex = EndX;
            Gizmos.color = new Color(1f, 0.32f, 0.36f, 0.85f);
            Gizmos.DrawLine(new Vector3(sx, -4f, 0f), new Vector3(sx, 12f, 0f));
            Gizmos.DrawLine(new Vector3(sx, 12f, 0f), new Vector3(ex, 12f, 0f));
            Gizmos.DrawLine(new Vector3(ex, 12f, 0f), new Vector3(ex, -4f, 0f));
        }
    }
}
