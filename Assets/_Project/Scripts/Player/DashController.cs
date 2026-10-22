using System;
using System.Collections.Generic;
using UnityEngine;

namespace TiantaiDash
{
    /// <summary>冲刺：方向固定向前，时长可被增益延长，结束可选冲击波。提供起止事件给特效层。</summary>
    public class DashController : MonoBehaviour
    {
        public float baseDuration = 0.55f;
        public float durationBonusPerLevel = 0.4f;
        public float speedMultiplier = 2.6f;
        public float shockwaveRadius = 4f;
        public float impactRadius = 2.2f;
        [Tooltip("冲刺中撞到怪物直接造成 1 点伤害，并且这段时间不吃伤害")]
        public bool ramDamage = true;
        public bool invincibleWhileDashing = true;

        public PlayerRunner runner;

        [Header("手动冲刺（鼠标中键）")]
        [Tooltip("允许玩家用中键主动冲刺")]
        public bool allowManualDash = true;
        [Tooltip("中键冲刺冷却（秒）")]
        public float manualCooldown = 5f;
        public InputHandler input;

        [Header("撞击特效（留空用素材总表）")]
        public Sprite hitFx;

        public event Action OnDashStarted;
        public event Action OnDashEnded;

        public int DistanceLevel { get; private set; }
        public bool ShockwaveUnlocked { get; private set; }
        public bool IsDashing { get; private set; }
        public int DashCount { get; private set; }

        float _dashEndTime;
        readonly HashSet<int> _rammedThisDash = new HashSet<int>();

        public float CurrentDuration => baseDuration * (1f + durationBonusPerLevel * DistanceLevel);

        /// <summary>中键冲刺是否可用。</summary>
        public bool ManualDashReady => Time.time >= _manualReadyAt;
        /// <summary>中键冲刺剩余冷却（秒）。</summary>
        public float ManualDashCooldownLeft => Mathf.Max(0f, _manualReadyAt - Time.time);
        /// <summary>中键冲刺总冷却（UI 归一化用）。</summary>
        public float ManualCooldown => Mathf.Max(0.01f, manualCooldown);
        float _manualReadyAt;

        // 运行时统计（自测用，不影响玩法）
        public float LastDashPeakSpeed { get; private set; }
        public float LastDashDistance { get; private set; }
        public int RamCount { get; private set; }
        float _dashStartX;
        Rigidbody2D _rb;

        public void SetRunner(PlayerRunner playerRunner) { runner = playerRunner; }
        public void SetInput(InputHandler handler) { input = handler; }

        /// <summary>中键主动冲刺：冷却好了、没在冲、游戏在跑才允许。返回 true 表示冲出去了。</summary>
        public bool TryManualDash()
        {
            if (!allowManualDash || IsDashing || !ManualDashReady) return false;

            var gm = GameManager.Instance;
            if (gm != null && !gm.IsPlaying) return false;

            _manualReadyAt = Time.time + ManualCooldown;
            TriggerDash();
            return true;
        }

        void Update()
        {
            // 中键主动冲刺：冷却好了、没在冲、游戏在跑才允许
            if (input == null) input = FindObjectOfType<InputHandler>();
            if (input != null && input.DashPressed) TryManualDash();

            if (!IsDashing) return;

            var gm = GameManager.Instance;
            if (gm == null || gm.IsTerminal)
            {
                EndDash(false);
                return;
            }

            if (_rb != null) LastDashPeakSpeed = Mathf.Max(LastDashPeakSpeed, Mathf.Abs(_rb.velocity.x));
            if (ramDamage) RamEnemies();

            if (Time.time >= _dashEndTime) EndDash(true);
        }

        public void TriggerDash()
        {
            if (IsDashing) return;

            var gm = GameManager.Instance;
            if (gm != null && !gm.IsPlaying) return;

            IsDashing = true;
            DashCount++;
            _rammedThisDash.Clear();
            RamCount = 0;
            LastDashPeakSpeed = 0f;
            _dashStartX = transform.position.x;
            if (_rb == null) _rb = GetComponent<Rigidbody2D>();
            _dashEndTime = Time.time + CurrentDuration;
            if (runner != null) runner.SetDashActive(true, speedMultiplier);

            if (OnDashStarted != null) OnDashStarted();
        }

        /// <summary>
        /// 击杀奖励冲刺。**冲刺中直接返回 false，不重置计时器、不叠加速度**；
        /// 返回 true 表示这次真的开了一段新冲刺。
        /// </summary>
        public bool RequestKillDash()
        {
            if (IsDashing) return false;

            var gm = GameManager.Instance;
            if (gm != null && !gm.IsPlaying) return false;

            TriggerDash();
            return IsDashing;
        }

        /// <summary>提前结束冲刺（不触发冲击波）。蓄力起手时用来让位。</summary>
        public void CancelDash()
        {
            if (!IsDashing) return;
            EndDash(false);
        }

        void EndDash(bool fireShockwave)
        {
            IsDashing = false;
            LastDashDistance = transform.position.x - _dashStartX;
            if (runner != null) runner.SetDashActive(false, speedMultiplier);
            if (fireShockwave && ShockwaveUnlocked) Shockwave();

            if (OnDashEnded != null) OnDashEnded();
        }

        /// <summary>冲刺撞击：撞到的怪**直接死亡**（无视血量），一冲一次。</summary>
        void RamEnemies()
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position + Vector3.up * 1.2f,
                impactRadius, GameLayers.EnemyMask);

            for (int i = 0; i < hits.Length; i++)
            {
                int id = hits[i].GetInstanceID();
                if (_rammedThisDash.Contains(id)) continue;

                IDamageable target = hits[i].GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive) continue;

                _rammedThisDash.Add(id);
                RamCount++;

                // 撞到即死：直接按当前血量结算，走正常的死亡流程（喷血 / 击杀奖励 / 音效）
                EnemyHealth enemy = hits[i].GetComponentInParent<EnemyHealth>();
                if (enemy != null) enemy.TakeDamage(Mathf.Max(1, enemy.CurrentHealth), true);
                else target.TakeDamage(9999);

                CameraFollow.Shake(0.3f);
                AudioManager.Play("sfx_hit", 1f, 0.9f);

                Sprite spark = ResolveHitFx();
                if (spark != null)
                {
                    FxSprite fx = FxSprite.Spawn(spark, hits[i].bounds.center, new Vector2(6f, 3f),
                        new Color(1f, 0.9f, 0.5f, 1f), 0.22f, 2f, 14);
                    fx.drag = 2f;
                }
            }
        }

        Sprite ResolveHitFx()
        {
            if (hitFx != null) return hitFx;
            ArtLibrary art = ArtLibrary.Instance;
            return art != null ? art.FirstFrame(art.hitFrames) : null;
        }

        void Shockwave()
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position + Vector3.up * 1.2f,
                shockwaveRadius, GameLayers.EnemyMask);

            for (int i = 0; i < hits.Length; i++)
            {
                IDamageable target = hits[i].GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive) continue;

                EnemyHealth enemy = hits[i].GetComponentInParent<EnemyHealth>();
                if (enemy != null) enemy.TakeDamage(1, true);
                else target.TakeDamage(1);
            }
        }

        public void AddDistanceLevel() { DistanceLevel++; }
        public void UnlockShockwave() { ShockwaveUnlocked = true; }

        public void ResetState()
        {
            IsDashing = false;
            DistanceLevel = 0;
            ShockwaveUnlocked = false;
            DashCount = 0;
            _manualReadyAt = 0f;
            if (runner != null) runner.SetDashActive(false, speedMultiplier);
        }
    }
}
